using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace CodeWalker.Numerics
{
    /// <summary>RAGE Vec3V: an x,y,z vector held in a Vector128 (w is padding/ignored).</summary>
    public readonly struct Vec3V
    {
        public readonly Vector128<float> V;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vec3V(Vector128<float> v) => V = v;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vec3V(float x, float y, float z) => V = Vec.Set(x, y, z, 0f);

        public float X => V.GetElement(0);
        public float Y => V.GetElement(1);
        public float Z => V.GetElement(2);

        public static Vec3V operator +(Vec3V a, Vec3V b) => new(Vec.Add(a.V, b.V));
        public static Vec3V operator -(Vec3V a, Vec3V b) => new(Vec.Subtract(a.V, b.V));
        public static Vec3V operator *(Vec3V a, Vec3V b) => new(Vec.Scale(a.V, b.V));

        public Vec3V Abs() => new(Vec.Abs(V));
        public Vec3V Normalize() => new(Vec.Normalize3(V));
        public float Mag() => Vec.Mag3(V);
        public float MagSquared() => Vec.Dot3(V, V);
        public static float Dot(Vec3V a, Vec3V b) => Vec.Dot3(a.V, b.V);
        public static Vec3V Min(Vec3V a, Vec3V b) => new(Vec.Min(a.V, b.V));
        public static Vec3V Max(Vec3V a, Vec3V b) => new(Vec.Max(a.V, b.V));

        public override string ToString() => $"({X}, {Y}, {Z})";
    }

    /// <summary>RAGE Vec4V / QuatV: x,y,z,w in a Vector128.</summary>
    public readonly struct Vec4V
    {
        public readonly Vector128<float> V;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vec4V(Vector128<float> v) => V = v;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vec4V(float x, float y, float z, float w) => V = Vec.Set(x, y, z, w);

        public float X => V.GetElement(0);
        public float Y => V.GetElement(1);
        public float Z => V.GetElement(2);
        public float W => V.GetElement(3);

        public Vec4V Normalize() => new(Vec.Normalize4(V));
        public override string ToString() => $"({X}, {Y}, {Z}, {W})";
    }

    /// <summary>
    /// RAGE Mat34V: 3x4 affine transform stored as three basis columns + translation.
    /// Transform() and the quaternion constructor reproduce the game's SSE sequences.
    /// </summary>
    public readonly struct Mat34V
    {
        public readonly Vector128<float> Col0; // rotation basis columns (a[.][0..2])
        public readonly Vector128<float> Col1;
        public readonly Vector128<float> Col2;
        public readonly Vector128<float> Col3; // translation

        public Mat34V(Vector128<float> c0, Vector128<float> c1, Vector128<float> c2, Vector128<float> c3)
        {
            Col0 = c0; Col1 = c1; Col2 = c2; Col3 = c3;
        }

        /// <summary>
        /// Build from an entity's position, orientation quaternion (x,y,z,w) and per-axis scale.
        /// Scale is local: each rotation basis column i is multiplied by scale[i] (equivalent to
        /// SharpDX's Scaling * RotationQuaternion * Translation).
        /// </summary>
        public static Mat34V FromComponents(Vec3V position, Vec4V orientation, Vec3V scale)
        {
            Vec.Mat33FromQuat(orientation.V, out var c0, out var c1, out var c2);
            c0 = Vec.Scale(c0, Vec.SplatX(scale.V));
            c1 = Vec.Scale(c1, Vec.SplatY(scale.V));
            c2 = Vec.Scale(c2, Vec.SplatZ(scale.V));
            return new Mat34V(c0, c1, c2, position.V);
        }

        /// <summary>Transform a point: p' = c3 + x*c0 + y*c1 + z*c2 (RAGE PC accumulation order).</summary>
        public Vec3V Transform(Vec3V point) => new(Vec.Transform34(Col0, Col1, Col2, Col3, point.V));

        /// <summary>Rotate/scale a direction (no translation): x*c0 + y*c1 + z*c2.</summary>
        public Vec3V Transform3x3(Vec3V dir) => new(Vec.Transform33(Col0, Col1, Col2, dir.V));
    }
}
