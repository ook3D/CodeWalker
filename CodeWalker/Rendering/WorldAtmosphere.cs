using System;
using CodeWalker.GameFiles;
using CodeWalker.World;
using SharpDX;
using SharpDX.Direct3D11;
using Device = SharpDX.Direct3D11.Device;

namespace CodeWalker.Rendering
{
    internal struct WorldAtmosphereVars
    {
        public Matrix ViewProjInv;
        public Vector4 Distances, Densities, GroundColour, AtmosphereColour, HazeColour;
        public Vector4 SunColour, MoonColour, SunDirection, MoonDirection;
    }

    internal sealed class WorldAtmosphere : IDisposable
    {
        readonly PixelShader pixelShader;
        readonly VertexShader vertexShader;
        readonly InputLayout layout;
        readonly UnitQuad quad;
        readonly GpuVarsBuffer<WorldAtmosphereVars> constants;
        readonly BlendState blend;

        public WorldAtmosphere(Device device)
        {
            var vertexBytes = PathUtil.ReadAllBytes("Shaders\\PPFinalPassVS.cso");
            vertexShader = new VertexShader(device, vertexBytes);
            pixelShader = new PixelShader(device, PathUtil.ReadAllBytes("Shaders\\WorldAtmospherePS.cso"));
            quad = new UnitQuad(device, true);
            layout = new InputLayout(device, vertexBytes, quad.GetLayout());
            constants = new GpuVarsBuffer<WorldAtmosphereVars>(device);
            var description = BlendStateDescription.Default();
            description.RenderTarget[0].IsBlendEnabled = true;
            description.RenderTarget[0].SourceBlend = BlendOption.SourceAlpha;
            description.RenderTarget[0].DestinationBlend = BlendOption.InverseSourceAlpha;
            description.RenderTarget[0].SourceAlphaBlend = BlendOption.Zero;
            description.RenderTarget[0].DestinationAlphaBlend = BlendOption.One;
            blend = new BlendState(device, description);
        }

        public void Render(DeviceContext context, GpuTexture target, Camera camera, ShaderGlobalLights lights)
        {
            var weather = lights.Weather;
            if (weather?.Inited != true || target?.DepthSRV == null || camera.IsMapView) return;
            float Value(string name) => weather.GetDynamicValue(name);
            Vector4 Colour(string name, float intensity) => new Vector4(Value(name + "_r"),
                Value(name + "_g"), Value(name + "_b"), 0) * intensity;
            float falloff = Value("fog_falloff") / 1000;
            float atViewer = MathF.Exp(Math.Min(-falloff * (camera.Position.Z - Value("fog_base_height")), 20));
            float farClip = Math.Max(Value("far_clip"), Math.Max(Value("fog_start"), 1));
            float hdr = Value("fog_hdr");
            var ground = Colour("fog_near_col", hdr);
            var atmosphere = Vector4.Lerp(Colour("fog_east_col", hdr), ground,
                MathF.Pow(Math.Clamp(atViewer, 0, 1), 0.3f));
            atmosphere.W = Value("fog_horizon_tint_scale") / farClip;
            constants.Vars = new WorldAtmosphereVars
            {
                ViewProjInv = Matrix.Transpose(camera.ViewProjInvMatrix),
                Distances = new Vector4(Value("fog_start"), farClip, Value("fog_haze_start"), falloff),
                Densities = new Vector4(Math.Min(1, atViewer * Value("fog_density") / 10000),
                    Value("fog_haze_density") / 10000, Value("fog_alpha"), Value("fog_haze_alpha")),
                GroundColour = ground,
                AtmosphereColour = atmosphere,
                HazeColour = Colour("fog_haze_col", Value("fog_haze_hdr")),
                SunColour = Colour("fog_col", hdr),
                MoonColour = Colour("fog_moon_col", hdr),
                SunDirection = new Vector4(lights.CurrentSunDir, Math.Max(Value("fog_sun_lighting_calc_pow"), 0.001f)),
                MoonDirection = new Vector4(lights.CurrentMoonDir, Math.Max(Value("fog_moon_lighting_calc_pow"), 0.001f))
            };
            constants.Update(context);
            // Unbind depth before reading it, while blending into the HDR colour target.
            context.OutputMerger.SetRenderTargets(target.RTV);
            context.OutputMerger.SetBlendState(blend);
            if (target.Texture == null) return;
            var size = target.Texture.Description;
            context.Rasterizer.SetViewport(0, 0, size.Width, size.Height);
            context.VertexShader.Set(vertexShader);
            context.PixelShader.Set(pixelShader);
            constants.SetPSCBuffer(context, 0);
            context.PixelShader.SetShaderResource(0, target.DepthSRV);
            context.InputAssembler.InputLayout = layout;
            quad.Draw(context);
            context.PixelShader.SetShaderResource(0, null);
            context.PixelShader.SetConstantBuffer(0, null);
            context.OutputMerger.SetBlendState(null);
        }

        public void Dispose()
        {
            blend.Dispose(); constants.Dispose(); layout.Dispose(); quad.Dispose();
            pixelShader.Dispose(); vertexShader.Dispose();
        }
    }
}
