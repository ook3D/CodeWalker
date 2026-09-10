using System;
using CodeWalker;
using CodeWalker.GameFiles;
using CodeWalker.World;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;

namespace CodeWalker.Rendering
{
    public struct ParticleShaderVSSceneVars
    {
        public Matrix ViewProj;
        public Matrix ViewInv;
        public Vector3 CamPos;
        public float Pad0;
    }

    public class ParticleShader : Shader
    {
        bool disposed = false;

        VertexShader particlevs;
        PixelShader particleps;

        InputLayout layout;
        SamplerState texsampler;
        UnitQuad quad;

        GpuVarsBuffer<ParticleShaderVSSceneVars> VSSceneVars;

        public GpuCBuffer<ParticleInstance> Instances { get; private set; }

        public ParticleShader(Device device)
        {
            byte[] vsbytes = PathUtil.ReadAllBytes("Shaders\\ParticleVS.cso");
            byte[] psbytes = PathUtil.ReadAllBytes("Shaders\\ParticlePS.cso");

            particlevs = new VertexShader(device, vsbytes);
            particleps = new PixelShader(device, psbytes);

            layout = new InputLayout(device, vsbytes, new[]
            {
                new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0, 0),
                new InputElement("TEXCOORD", 0, Format.R32G32_Float, 16, 0),
            });

            texsampler = new SamplerState(device, new SamplerStateDescription()
            {
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp,
                BorderColor = Color.Transparent,
                ComparisonFunction = Comparison.Always,
                Filter = Filter.MinMagMipLinear,
                MaximumAnisotropy = 1,
                MaximumLod = float.MaxValue,
                MinimumLod = 0,
                MipLodBias = 0,
            });

            quad = new UnitQuad(device);

            VSSceneVars = new GpuVarsBuffer<ParticleShaderVSSceneVars>(device);
            Instances = new GpuCBuffer<ParticleInstance>(device, 20000);
        }

        public override void SetShader(DeviceContext context)
        {
            context.VertexShader.Set(particlevs);
            context.PixelShader.Set(particleps);
            SetInputLayout(context, VertexType.PT);
        }
        public override bool SetInputLayout(DeviceContext context, VertexType type)
        {
            context.InputAssembler.InputLayout = layout;
            return true;
        }
        public override void SetSceneVars(DeviceContext context, Camera camera, Shadowmap? shadowmap, ShaderGlobalLights lights)
        {
            VSSceneVars.Vars.ViewProj = Matrix.Transpose(camera.ViewProjMatrix);
            VSSceneVars.Vars.ViewInv = Matrix.Transpose(camera.ViewInvMatrix);
            VSSceneVars.Vars.CamPos = camera.Position;
            VSSceneVars.Update(context);
            VSSceneVars.SetVSCBuffer(context, 0);
        }
        public override void SetEntityVars(DeviceContext context, ref RenderableInst rend) { }
        public override void SetModelVars(DeviceContext context, RenderableModel model) { }
        public override void SetGeomVars(DeviceContext context, RenderableGeometry geom) { }

        // Renders the particles currently in the Instances buffer using the given texture.
        public void RenderBatch(DeviceContext context, ShaderResourceView texSRV)
        {
            int count = Instances.CurrentCount;
            if (count <= 0) return;

            Instances.Update(context);
            Instances.SetVSResource(context, 0);
            context.PixelShader.SetShaderResource(0, texSRV);
            context.PixelShader.SetSampler(0, texsampler);

            quad.DrawInstanced(context, count);
        }

        public override void UnbindResources(DeviceContext context)
        {
            context.VertexShader.SetConstantBuffer(0, null);
            context.VertexShader.SetShaderResource(0, null);
            context.PixelShader.SetShaderResource(0, null);
            context.PixelShader.SetSampler(0, null);
            context.VertexShader.Set(null);
            context.PixelShader.Set(null);
        }

        public void Dispose()
        {
            if (disposed) return;

            Instances.Dispose();
            VSSceneVars.Dispose();
            quad.Dispose();
            texsampler.Dispose();
            layout.Dispose();
            particleps.Dispose();
            particlevs.Dispose();

            disposed = true;
        }
    }
}
