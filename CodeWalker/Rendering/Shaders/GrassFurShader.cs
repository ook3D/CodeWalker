using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Device = SharpDX.Direct3D11.Device;
using Buffer = SharpDX.Direct3D11.Buffer;
using MapFlags = SharpDX.Direct3D11.MapFlags;
using SharpDX;
using CodeWalker.GameFiles;
using CodeWalker.World;

namespace CodeWalker.Rendering
{

    public struct GrassFurShaderVSSceneVars
    {
        public Matrix ViewProj;
        public Vector4 WindVector;
    }
    public struct GrassFurShaderVSEntityVars
    {
        public Vector4 CamRel;
        public Quaternion Orientation;
        public uint HasSkeleton;
        public uint HasTransforms;
        public uint TintPaletteIndex;
        public uint Pad1;
        public Vector3 Scale;
        public uint Pad2;
    }
    public struct GrassFurShaderVSModelVars
    {
        public Matrix Transform;
    }
    public struct GrassFurShaderMeshVars
    {
        public uint FurMode;
        public uint FurTintMode;
        public uint FurMaskMode;
        public uint FurLayerCount;
        public float FurLayerCountInv;
        public float FurLength;
        public float FurBumpScale;
        public float FurFadeDistMin;
        public float FurFadeDistMax;
        public float FurFadeShadow;
        public float FurPad0;
        public float FurPad1;
        public Vector4 FurUVScaling;
        public Vector4 FurThresholds1;
        public Vector4 FurThresholds2;
        public Vector4 FurThresholds3;
        public Vector4 FurThresholds4;
        public Vector4 FurShadows1;
        public Vector4 FurShadows2;
        public Vector4 FurShadows3;
        public Vector4 FurShadows4;
    }
    public struct GrassFurShaderPSSceneVars
    {
        public ShaderGlobalLightParams GlobalLights;
        public uint EnableShadows;
        public uint RenderMode;
        public uint RenderModeIndex;
        public uint RenderSamplerCoord;
    }

    public class GrassFurShader : Shader, IDisposable
    {
        bool disposed = false;

        VertexShader vs;
        PixelShader ps;
        PixelShader psdef;
        GpuVarsBuffer<GrassFurShaderVSSceneVars> VSSceneVars;
        GpuVarsBuffer<GrassFurShaderVSEntityVars> VSEntityVars;
        GpuVarsBuffer<GrassFurShaderVSModelVars> VSModelVars;
        GpuVarsBuffer<GrassFurShaderMeshVars> MeshVars;
        GpuVarsBuffer<GrassFurShaderPSSceneVars> PSSceneVars;
        SamplerState texsampler;
        SamplerState heightsampler;

        private Dictionary<VertexType, InputLayout> layouts = new Dictionary<VertexType, InputLayout>();

        public bool Deferred = false;

        // fur layer count for current geometry, used by RenderInstanced
        private int currentLayerCount = 8;

        public GrassFurShader(Device device)
        {
            byte[] vsbytes = PathUtil.ReadAllBytes("Shaders\\GrassFurVS.cso");
            byte[] psbytes = PathUtil.ReadAllBytes("Shaders\\GrassFurPS.cso");
            byte[] psdefbytes = PathUtil.ReadAllBytes("Shaders\\GrassFurPS_Deferred.cso");

            vs = new VertexShader(device, vsbytes);
            ps = new PixelShader(device, psbytes);
            psdef = new PixelShader(device, psdefbytes);

            VSSceneVars = new GpuVarsBuffer<GrassFurShaderVSSceneVars>(device);
            VSEntityVars = new GpuVarsBuffer<GrassFurShaderVSEntityVars>(device);
            VSModelVars = new GpuVarsBuffer<GrassFurShaderVSModelVars>(device);
            MeshVars = new GpuVarsBuffer<GrassFurShaderMeshVars>(device);
            PSSceneVars = new GpuVarsBuffer<GrassFurShaderPSSceneVars>(device);

            // grass_fur typically uses PNCTTTX vertex layout
            layouts.Add(VertexType.PNCTTTX, new InputLayout(device, vsbytes, VertexTypeGTAV.GetLayout(VertexType.PNCTTTX)));
            layouts.Add(VertexType.PNCTTTX_2, new InputLayout(device, vsbytes, VertexTypeGTAV.GetLayout(VertexType.PNCTTTX_2)));
            layouts.Add(VertexType.PNCTTTX_3, new InputLayout(device, vsbytes, VertexTypeGTAV.GetLayout(VertexType.PNCTTTX_3)));

            texsampler = new SamplerState(device, new SamplerStateDescription()
            {
                AddressU = TextureAddressMode.Wrap,
                AddressV = TextureAddressMode.Wrap,
                AddressW = TextureAddressMode.Wrap,
                BorderColor = Color.Black,
                ComparisonFunction = Comparison.Always,
                Filter = Filter.MinMagMipLinear,
                MaximumAnisotropy = 1,
                MaximumLod = float.MaxValue,
                MinimumLod = 0,
                MipLodBias = 0,
            });
            heightsampler = new SamplerState(device, new SamplerStateDescription()
            {
                AddressU = TextureAddressMode.Wrap,
                AddressV = TextureAddressMode.Wrap,
                AddressW = TextureAddressMode.Wrap,
                BorderColor = Color.Black,
                ComparisonFunction = Comparison.Always,
                Filter = Filter.MinMagMipLinear,
                MaximumAnisotropy = 1,
                MaximumLod = float.MaxValue,
                MinimumLod = 0,
                MipLodBias = 0,
            });
        }


        public override void SetShader(DeviceContext context)
        {
            context.VertexShader.Set(vs);
            context.PixelShader.Set(Deferred ? psdef : ps);
        }

        public override bool SetInputLayout(DeviceContext context, VertexType type)
        {
            InputLayout? l;
            if (layouts.TryGetValue(type, out l))
            {
                context.InputAssembler.InputLayout = l;
                return true;
            }
            return false;
        }

        public override void SetSceneVars(DeviceContext context, Camera camera, Shadowmap shadowmap, ShaderGlobalLights lights)
        {
            VSSceneVars.Vars.ViewProj = Matrix.Transpose(camera.ViewProjMatrix);
            VSSceneVars.Vars.WindVector = Vector4.Zero;
            VSSceneVars.Update(context);
            VSSceneVars.SetVSCBuffer(context, 0);

            PSSceneVars.Vars.GlobalLights = lights.Params;
            PSSceneVars.Vars.EnableShadows = (shadowmap != null) ? 1u : 0u;
            PSSceneVars.Vars.RenderMode = 0u;
            PSSceneVars.Vars.RenderModeIndex = 0u;
            PSSceneVars.Vars.RenderSamplerCoord = 0u;
            PSSceneVars.Update(context);
            PSSceneVars.SetPSCBuffer(context, 0);
        }

        public override void SetEntityVars(DeviceContext context, ref RenderableInst rend)
        {
            VSEntityVars.Vars.CamRel = new Vector4(rend.CamRel, 0.0f);
            VSEntityVars.Vars.Orientation = rend.Orientation;
            VSEntityVars.Vars.Scale = rend.Scale;
            VSEntityVars.Vars.HasSkeleton = rend.Renderable.HasSkeleton ? 1u : 0;
            VSEntityVars.Vars.HasTransforms = rend.Renderable.HasTransforms ? 1u : 0;
            VSEntityVars.Vars.TintPaletteIndex = rend.TintPaletteIndex;
            VSEntityVars.Update(context);
            VSEntityVars.SetVSCBuffer(context, 2);
        }

        public override void SetModelVars(DeviceContext context, RenderableModel model)
        {
            if (!model.UseTransform) return;
            VSModelVars.Vars.Transform = Matrix.Transpose(model.Transform);
            VSModelVars.Update(context);
            VSModelVars.SetVSCBuffer(context, 3);
        }

        public override void SetGeomVars(DeviceContext context, RenderableGeometry geom)
        {
            // set fur mesh vars from geometry properties
            MeshVars.Vars.FurMode = geom.FurMode;
            MeshVars.Vars.FurTintMode = geom.FurTintMode;
            MeshVars.Vars.FurMaskMode = geom.FurMaskMode;
            MeshVars.Vars.FurLayerCount = geom.FurLayerCount;
            MeshVars.Vars.FurLayerCountInv = geom.FurLayerCountInv;
            MeshVars.Vars.FurLength = geom.FurLength;
            MeshVars.Vars.FurBumpScale = geom.FurBumpScale;
            MeshVars.Vars.FurFadeDistMin = geom.FurFadeDistMin;
            MeshVars.Vars.FurFadeDistMax = geom.FurFadeDistMax;
            MeshVars.Vars.FurFadeShadow = geom.FurFadeShadow;
            MeshVars.Vars.FurUVScaling = geom.FurUVScaling;
            MeshVars.Vars.FurThresholds1 = geom.FurThresholds1;
            MeshVars.Vars.FurThresholds2 = geom.FurThresholds2;
            MeshVars.Vars.FurThresholds3 = geom.FurThresholds3;
            MeshVars.Vars.FurThresholds4 = geom.FurThresholds4;
            MeshVars.Vars.FurShadows1 = geom.FurShadows1;
            MeshVars.Vars.FurShadows2 = geom.FurShadows2;
            MeshVars.Vars.FurShadows3 = geom.FurShadows3;
            MeshVars.Vars.FurShadows4 = geom.FurShadows4;
            MeshVars.Update(context);
            MeshVars.SetVSCBuffer(context, 4);
            MeshVars.SetPSCBuffer(context, 4);

            currentLayerCount = (int)geom.FurLayerCount;
            if (currentLayerCount < 1) currentLayerCount = 8;
            if (currentLayerCount > 128) currentLayerCount = 128;

            // bind textures
            context.PixelShader.SetSampler(0, texsampler);
            context.PixelShader.SetSampler(1, heightsampler);

            if (geom.RenderableTextures != null)
            {
                for (int i = 0; i < geom.RenderableTextures.Length; i++)
                {
                    var itex = geom.RenderableTextures[i];
                    if (itex == null) continue;
                    if (itex.ShaderResourceView == null) continue;
                    var ihash = geom.TextureParamHashes[i];
                    switch (ihash)
                    {
                        case ShaderParamNames.DiffuseSampler:
                            itex.SetPSResource(context, 0);
                            break;
                        case ShaderParamNames.BumpSampler:
                            itex.SetPSResource(context, 1);
                            break;
                        case ShaderParamNames.SpecSampler:
                            itex.SetPSResource(context, 2);
                            break;
                        case ShaderParamNames.StippleSampler:
                            itex.SetPSResource(context, 3);
                            break;
                        case ShaderParamNames.DiffuseHfSampler:
                            itex.SetPSResource(context, 4);
                            break;
                        case ShaderParamNames.FurMaskSampler:
                            itex.SetPSResource(context, 5);
                            break;
                        case ShaderParamNames.ComboHeightSamplerFur01:
                            itex.SetPSResource(context, 6);
                            break;
                        case ShaderParamNames.ComboHeightSamplerFur23:
                            itex.SetPSResource(context, 7);
                            break;
                        case ShaderParamNames.ComboHeightSamplerFur45:
                            itex.SetPSResource(context, 8);
                            break;
                        case ShaderParamNames.ComboHeightSamplerFur67:
                            itex.SetPSResource(context, 9);
                            break;
                    }
                }
            }
        }

        public void RenderGeom(DeviceContext context, RenderableGeometry geom)
        {
            geom.RenderInstanced(context, currentLayerCount);
        }


        public override void UnbindResources(DeviceContext context)
        {
            context.VertexShader.SetConstantBuffer(0, null);
            context.VertexShader.SetConstantBuffer(2, null);
            context.VertexShader.SetConstantBuffer(3, null);
            context.VertexShader.SetConstantBuffer(4, null);
            context.PixelShader.SetConstantBuffer(0, null);
            context.PixelShader.SetConstantBuffer(4, null);
            context.PixelShader.SetSampler(0, null);
            context.PixelShader.SetSampler(1, null);
            for (int i = 0; i < 10; i++)
            {
                context.PixelShader.SetShaderResource(i, null);
            }
            context.VertexShader.Set(null);
            context.PixelShader.Set(null);
        }

        public void Dispose()
        {
            if (disposed) return;

            texsampler?.Dispose();
            texsampler = null;
            heightsampler?.Dispose();
            heightsampler = null;

            foreach (InputLayout layout in layouts.Values)
            {
                layout.Dispose();
            }
            layouts.Clear();

            VSSceneVars.Dispose();
            VSEntityVars.Dispose();
            VSModelVars.Dispose();
            MeshVars.Dispose();
            PSSceneVars.Dispose();

            psdef.Dispose();
            ps.Dispose();
            vs.Dispose();

            disposed = true;
        }
    }
}
