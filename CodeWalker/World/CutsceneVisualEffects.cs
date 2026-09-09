using CodeWalker.GameFiles;
using CodeWalker.Rendering;
using SharpDX;
using System;

namespace CodeWalker.World;

public sealed record CutsceneDepthOfField(Vector4 Planes, float Strength, float BlurRadius = 8)
{
    public bool IsValid => float.IsFinite(Planes.X) && float.IsFinite(Planes.Y) &&
        float.IsFinite(Planes.Z) && float.IsFinite(Planes.W) && float.IsFinite(Strength) &&
        float.IsFinite(BlurRadius) && BlurRadius > 0 && Planes.X >= 0 && Planes.Y > Planes.X && Planes.Z >= Planes.Y && Planes.W > Planes.Z && Strength > 0;
}

public static class CutsceneAnimationTracks
{
    public static float EvaluateBlurRadius(ClipMapEntry? clip, float time, bool useDay)
    {
        // Four-plane DOF uses CoC radius, not the legacy two-plane strength track (36).
        byte track = useDay ? (byte)49 : (byte)52;
        if (!TryEvaluate(clip, time, track, out var value)) return 8;
        return float.IsFinite(value.X) ? Math.Clamp(MathF.Floor(value.X), 1, 15) : 0;
    }

    public static uint SectionHash(uint partial, int section, bool mergedFaceAndBody = false)
    {
        static uint Append(uint hash, ReadOnlySpan<char> text)
        {
            foreach (char c in text) { hash += (byte)c; hash += hash << 10; hash ^= hash >> 6; }
            return hash;
        }
        uint h = mergedFaceAndBody ? Append(partial, "_dual") : partial;
        Span<char> suffix = stackalloc char[12];
        suffix[0] = '-';
        section.TryFormat(suffix[1..], out int written, provider: System.Globalization.CultureInfo.InvariantCulture);
        h = Append(h, suffix[..(written + 1)]);
        h += h << 3; h ^= h >> 11; h += h << 15;
        return h;
    }

    public static bool TryEvaluate(ClipMapEntry? entry, float time, byte track, out Vector4 value)
    {
        value = Vector4.Zero;
        bool found = false;
        Vector4 result = Vector4.Zero;
        void Evaluate(Animation? animation, float playbackTime)
        {
            if (animation == null || animation.Frames == 0 || animation.SequenceFrameLimit == 0 ||
                animation.Duration <= 0 || !float.IsFinite(playbackTime)) return;
            int index = animation.FindBoneIndex(0, track);
            if (index < 0) return;
            var frame = animation.GetFramePosition(playbackTime);
            result = track == 1 ? animation.EvaluateQuaternion(frame, index, true).ToVector4() : animation.EvaluateVector4(frame, index, true);
            found = true;
        }
        if (entry?.Clip is ClipAnimation single)
            Evaluate(single.Animation, single.GetPlaybackTime(time));
        else if (entry?.Clip is ClipAnimationList list && list.Animations?.Data != null)
            foreach (var part in list.Animations.Data) Evaluate(part.Animation, part.GetPlaybackTime(time));
        value = result;
        return found;
    }
}

public sealed class CutsceneLightState
{
    public CutLightObject Definition { get; }
    public bool Animated { get; }
    public RenderableLight Light { get; } = new();
    public int AttachParentId { get; set; }
    public ushort AttachBoneHash { get; set; }
    public bool Visible { get; private set; }

    public CutsceneLightState(CutLightObject definition, bool animated = false)
    {
        Definition = definition;
        Animated = animated;
        Reset();
    }

    public void Reset()
    {
        AttachParentId = Definition.AttachParentId;
        AttachBoneHash = Definition.AttachBoneHash;
        Visible = false;
    }

    public void Update(ClipMapEntry? clip, float time, bool enabled)
    {
        var d = Definition;
        Vector3 position = d.vPosition, direction = d.vDirection, colour = d.vColour;
        float intensity = d.fIntensity, falloff = d.fFallOff, cone = d.fConeAngle, exponent = d.fExponentialFallOff;
        if (Animated)
        {
            // The reference only emits an animated light when its intensity track is active.
            intensity = 0;
            if (CutsceneAnimationTracks.TryEvaluate(clip, time, 30, out var v)) intensity = v.X;
            if (CutsceneAnimationTracks.TryEvaluate(clip, time, 0, out v)) position = v.XYZ();
            if (CutsceneAnimationTracks.TryEvaluate(clip, time, 1, out v)) direction = v.ToQuaternion().Multiply(-Vector3.UnitZ);
            else if (CutsceneAnimationTracks.TryEvaluate(clip, time, 42, out v)) direction = v.XYZ();
            if (CutsceneAnimationTracks.TryEvaluate(clip, time, 29, out v)) colour = v.XYZ();
            if (CutsceneAnimationTracks.TryEvaluate(clip, time, 31, out v)) falloff = v.X;
            if (CutsceneAnimationTracks.TryEvaluate(clip, time, 32, out v)) cone = v.X;
            if (CutsceneAnimationTracks.TryEvaluate(clip, time, 47, out v)) exponent = v.X;
        }
        static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
        Visible = Finite(position) && Finite(direction) && Finite(colour) && float.IsFinite(cone) &&
            float.IsFinite(exponent) && float.IsFinite(d.fInnerConeAngle) && (Animated ? clip != null : enabled) && d.iLightType is 1 or 2 &&
            float.IsFinite(intensity) && intensity > 0 && float.IsFinite(falloff) && falloff > 0;
        Light.Position = position;
        Light.Direction = direction.LengthSquared() > 0.000001f ? Vector3.Normalize(direction) : -Vector3.UnitZ;
        var axis = Math.Abs(Light.Direction.Z) < 0.99f ? Vector3.UnitZ : Vector3.UnitY;
        Light.TangentX = Vector3.Normalize(Vector3.Cross(axis, Light.Direction));
        Light.TangentY = Vector3.Cross(Light.Direction, Light.TangentX);
        Light.Type = (LightType)d.iLightType;
        Light.Intensity = intensity;
        Light.Colour = colour * (2.0f * intensity); // Same intensity convention as RenderableLight.Init.
        Light.Falloff = falloff;
        Light.FalloffExponent = Math.Max(exponent, 0);
        // Cutscene angles are already radians (passed directly to SetSpotlight in the reference).
        Light.ConeInnerAngle = Math.Clamp(d.fInnerConeAngle, 0, Math.Max(cone, 0));
        Light.ConeOuterAngle = Math.Clamp(cone, 0, MathF.PI);
        // Cutscene flags are a separate enum; do not reinterpret them as drawable light flags.
        Light.Flags = 0;
    }
}
