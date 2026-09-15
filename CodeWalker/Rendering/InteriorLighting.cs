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
            if (instance == null || !considered.Add(instance)) return;
            var candidate = FindRoom(instance, cameraPosition, out var volume);
            if (candidate == null || volume >= roomVolume) return;
            room = candidate;
            roomVolume = volume;
        }

        private static MCMloRoomDef? FindRoom(YmapEntityDef? instance, Vector3 position, out float roomVolume)
        {
            roomVolume = float.PositiveInfinity;
            MCMloRoomDef? room = null;
            if (instance?.Archetype is not MloArchetype mlo) return null;
            var scale = instance.Scale;
            if (scale.X == 0 || scale.Y == 0 || scale.Z == 0) return null;
            var local = Quaternion.Invert(instance.Orientation).Multiply(position - instance.Position) / scale;
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
            return room;
        }

        public static Vector2 GetAmbientScale(Archetype? archetype, YmapEntityDef? entity,
            Dictionary<uint, TimecycleMod>? modifiers)
        {
            // Entity::SetupAmbientScaleFlags / CalculateAmbientScales: 255 selects
            // dynamic lighting; unflagged archetypes ignore the map's baked scales.
            if ((archetype ?? entity?.Archetype)?.UseAmbientScale != true) return Vector2.One;
            if (entity != null && entity._CEntityDef.ambientOcclusionMultiplier != 255)
            {
                return new Vector2(Math.Clamp(entity._CEntityDef.ambientOcclusionMultiplier, 0, 255),
                    Math.Clamp(entity._CEntityDef.artificialAmbientOcclusion, 0, 255)) / 255f;
            }

            // TimeCycle::CalcAmbientScale starts outdoors at (1, 0). Interior
            // multipliers come from the object's room, independently of the camera.
            // Portal feathering and spatial timecycle boxes are not evaluated here.
            var ambient = new Vector2(1, 0);
            MCMloRoomDef? room = null;
            if (entity?.MloParent?.Archetype is MloArchetype mlo)
            {
                var definition = entity.MloParent.MloInstance?.TryGetArchetypeEntity(entity);
                if (definition != null) room = mlo.GetEntityRoom(definition);
                if (room == null && definition != null && mlo.GetEntityPortal(definition) is { } portal)
                {
                    uint roomIndex = Math.Max(portal._Data.roomFrom, portal._Data.roomTo);
                    room = Array.Find(mlo.rooms, r => r.Index == roomIndex);
                }
                if (entity.MloEntitySet is { } set)
                {
                    int index = set.Entities.IndexOf(entity);
                    if (index >= 0 && index < set.Locations.Length)
                        room = Array.Find(mlo.rooms, r => r.Index == set.Locations[index]);
                }
                room ??= FindRoom(entity.MloParent, entity.Position, out _);
            }
            if (room != null && room._Data.blend > 0 && modifiers != null)
            {
                float blend = Math.Clamp(room._Data.blend, 0, 1);
                void Apply(MetaHash hash)
                {
                    if (!modifiers.TryGetValue(hash, out var modifier)) return;
                    if (modifier.Dict.TryGetValue("natural_ambient_multiplier", out var natural)) ambient.X = 1 - blend + natural.value1 * blend;
                    if (modifier.Dict.TryGetValue("artificial_int_ambient_multiplier", out var artificial)) ambient.Y = 1 - blend + artificial.value1 * blend;
                }
                Apply(room._Data.timecycleName);
                Apply(room._Data.secondaryTimecycleName);
            }
            // DynamicEntity stores byte scales before ShaderLib normalizes them.
            return new Vector2((int)(Math.Clamp(ambient.X, 0, 1) * 255),
                (int)(Math.Clamp(ambient.Y, 0, 1) * 255)) / 255f;
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
