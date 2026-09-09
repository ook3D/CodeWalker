using CodeWalker.World;
using SharpDX;
using System;

namespace CodeWalker.Rendering
{
    // CPU counterpart to the world's material lighting. Colours are linear,
    // intensity is applied once, and directions point from surfaces to lights.
    internal static class WorldLighting
    {
        internal readonly record struct Frame(ShaderGlobalLightParams Parameters,
            Vector3 Sun, Vector3 Moon, Vector3 MoonAxis, float SkyIntensity, Color4 InteriorUp, Color4 InteriorDown);

        internal static Frame Evaluate(Timecycle time, WeatherValues values, float hour,
            bool hdr, bool swapHemisphere, double cycleDays = 0)
        {
            hour = (hour % 24 + 24) % 24;
            float angle = hour < 6 ? MathF.PI * 0.5f * hour / 6
                : hour < 20 ? MathF.PI * (0.5f + (hour - 6) / 14)
                : MathF.PI * (1.5f + 0.5f * (hour - 20) / 4);
            float radians = MathF.PI / 180;
            Vector3 Slope(float degrees) => new(0, MathF.Cos(degrees * radians), MathF.Sin(degrees * radians));
            Vector3 Orbit(Vector3 slope, float phase) => slope * -MathF.Cos(phase) + Vector3.UnitX * MathF.Sin(phase);

            Vector3 sun = Vector3.Normalize(Orbit(Slope(time.sun_roll), angle));
            float yaw = time.sun_yaw * radians;
            sun = new Vector3(sun.X * MathF.Cos(yaw) - sun.Y * MathF.Sin(yaw),
                sun.X * MathF.Sin(yaw) + sun.Y * MathF.Cos(yaw), sun.Z);
            if (swapHemisphere) sun.Y = -sun.Y;

            Vector3 moonSlope = Slope(time.moon_roll);
            Vector3 moonAxis = Vector3.Cross(Vector3.UnitX, moonSlope);
            float moonAngle = angle + MathF.PI * (float)((cycleDays % 55 - 27) / 27);
            float wobble = time.moon_wobble_offset + MathF.Sin((float)(cycleDays % 27)
                * 2 * MathF.PI * time.moon_wobble_freq) * time.moon_wobble_amp;
            Vector3 moon = Vector3.Normalize(Orbit(moonSlope, moonAngle) + moonAxis * wobble);

            float sunFade = 1, moonFade = 0;
            if (hour > 20)
            {
                sunFade = 1 - Math.Clamp((hour - 21.5f) * 4, 0, 1);
                moonFade = Math.Clamp((hour - 21.75f) * 4, 0, 1);
            }
            else if (hour < 6)
            {
                sunFade = Math.Clamp((hour - 4.75f) * 4, 0, 1);
                moonFade = 1 - Math.Clamp((hour - 4.5f) * 4, 0, 1);
            }
            moonFade *= Math.Clamp(moon.Z / 0.1f, 0, 1);
            float weight = sunFade * 100 + moonFade;
            Vector3 direction = weight > 1e-6f ? (sun * (sunFade * 100) + moon * moonFade) / weight : Vector3.Zero;
            if (direction.LengthSquared() > 1e-12f) direction = Vector3.Normalize(direction);
            direction.Z = Math.Max(direction.Z, 0.33f);
            direction = Vector3.Normalize(direction);

            Color4 Colour(Vector4 value, float multiplier = 1) => new(
                value.X * value.W * multiplier, value.Y * value.W * multiplier,
                value.Z * value.W * multiplier, 0);
            Color4 Limit(Color4 colour, float ceiling) => hdr ? colour : new Color4(
                Math.Min(colour.Red, ceiling), Math.Min(colour.Green, ceiling), Math.Min(colour.Blue, ceiling), 0);

            var parameters = new ShaderGlobalLightParams
            {
                LightDir = direction,
                LightDirColour = Limit(Colour(values.lightDirCol, Math.Max(sunFade, moonFade)), 1),
                LightDirAmbColour = Limit(Colour(values.lightDirAmbCol, values.lightDirAmbIntensityMult), 0.5f),
                LightNaturalAmbUp = Limit(Colour(values.lightNaturalAmbUp, values.lightNaturalAmbUpIntensityMult), 0.5f),
                LightNaturalAmbDown = Limit(Colour(values.lightNaturalAmbDown), 0.5f),
                LightArtificialAmbUp = Limit(Colour(values.lightArtificialExtUp), 0.5f),
                LightArtificialAmbDown = Limit(Colour(values.lightArtificialExtDown), 0.5f)
            };
            parameters.LightDirAmbColour.Alpha = Math.Clamp(values.lightDirAmbBounce, 0, 1);
            parameters.LightNaturalAmbDown.Alpha = values.lightAmbDownWrap;
            parameters.LightArtificialAmbDown.Alpha = values.lightAmbDownWrap;
            return new Frame(parameters, sun, moon, Vector3.Normalize(moonAxis), hdr ? values.skyHdr : 1,
                Limit(Colour(values.lightArtificialIntUp), 0.5f),
                new Color4(Limit(Colour(values.lightArtificialIntDown), 0.5f).ToVector3(), values.lightAmbDownWrap));
        }
    }
}
