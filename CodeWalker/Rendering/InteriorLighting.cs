using CodeWalker.GameFiles;
using CodeWalker.World;
using SharpDX;
using System;
using System.Collections.Generic;

namespace CodeWalker.Rendering
{
    // Select the camera's authored room independently of individual mesh culling.
    internal sealed class InteriorLighting
    {
        private readonly HashSet<YmapEntityDef> considered = new();
        private MCMloRoomDef? room;
        private float roomVolume;

        public void Reset()
        {
            considered.Clear();
            room = null;
            roomVolume = float.PositiveInfinity;
        }

        public void Consider(YmapEntityDef? instance, Vector3 cameraPosition)
        {
            if (instance?.Archetype is not MloArchetype mlo || !considered.Add(instance)) return;
            var scale = instance.Scale;
            if (scale.X == 0 || scale.Y == 0 || scale.Z == 0) return;
            var local = Quaternion.Invert(instance.Orientation).Multiply(cameraPosition - instance.Position) / scale;
            foreach (var candidate in mlo.rooms)
            {
                // Room zero is the exterior/limbo volume, which often encloses every room.
                if (candidate.Index == 0) continue;
                var min = candidate.BBMin;
                var max = candidate.BBMax;
                // Some custom MLOs leave room bounds at zero. The asset loader
                // also computes bounds from that room's attached entities.
                if (max.X <= min.X || max.Y <= min.Y || max.Z <= min.Z)
                {
                    min = candidate.BBMin_CW;
                    max = candidate.BBMax_CW;
                }
                if (max.X <= min.X || max.Y <= min.Y || max.Z <= min.Z) continue;
                if (local.X < min.X || local.Y < min.Y || local.Z < min.Z ||
                    local.X > max.X || local.Y > max.Y || local.Z > max.Z) continue;
                var size = (max - min) * scale;
                float volume = Math.Abs(size.X * size.Y * size.Z);
                if (volume >= roomVolume) continue;
                room = candidate;
                roomVolume = volume;
            }
        }

        public void Apply(ShaderGlobalLights lights, WeatherValues weather,
            Dictionary<uint, TimecycleMod>? modifiers, bool hdr)
        {
            if (room == null || modifiers == null) return;
            modifiers.TryGetValue(room._Data.timecycleName, out var primary);
            modifiers.TryGetValue(room._Data.secondaryTimecycleName, out var secondary);
            if (primary == null && secondary == null) return;

            // TimeCycle::SetInteriorCache takes valA directly; a secondary modifier
            // replaces only the fields it supplies. valB is not a colour multiplier.
            float Value(string name, float fallback)
            {
                if (secondary?.Dict.TryGetValue(name, out var value) == true) return value.value1;
                if (primary?.Dict.TryGetValue(name, out value) == true) return value.value1;
                return fallback;
            }
            Color4 Colour(string prefix, Vector4 fallback, float alpha)
            {
                float intensity = Value(prefix + "_intensity", fallback.W);
                float Limit(float v) => hdr ? v : Math.Min(v, 0.5f);
                return new Color4(
                    Limit(Value(prefix + "_col_r", fallback.X) * intensity),
                    Limit(Value(prefix + "_col_g", fallback.Y) * intensity),
                    Limit(Value(prefix + "_col_b", fallback.Z) * intensity), alpha);
            }
            lights.InteriorAmbientUp = Colour("light_artificial_int_up", weather.lightArtificialIntUp, 0);
            lights.InteriorAmbientDown = Colour("light_artificial_int_down", weather.lightArtificialIntDown, weather.lightAmbDownWrap);
        }
    }
}
