using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
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
    // VS cbuffers - same layout as BasicShader for PS compatibility
    public struct TreeShaderVSSceneVars
    {
        public Matrix ViewProj;
        public Vector4 WindVector;
    }
    public struct TreeShaderVSEntityVars
    {
        public Vector4 CamRel;
        public Quaternion Orientation;
        public uint HasSkeleton;
        public uint HasTransforms;
        public uint TintPaletteIndex;
        public uint Pad1;
        public Vector3 Scale;
        public uint IsInstanced;
    }
    public struct TreeShaderVSModelVars
    {
        public Matrix Transform;
    }
    public struct TreeShaderVSGeomVars
    {
        public uint EnableTint;
        public float TintYVal;
        public uint IsDecal;
        public uint EnableWind;
        public Vector4 WindOverrideParams;
        public Vector4 globalAnimUV0;
        public Vector4 globalAnimUV1;
    }
    // Tree-specific wind parameters from material
    public struct TreeShaderVSWindVars
    {
        public Vector4 umGlobalParams;   // material: X=scaleH, Y=scaleV, Z=stiffnessMultiplier
        public Vector4 WindGlobalParams; // material: X=windScale
        public float GlobalTimer;
        public float Pad0;
        public float Pad1;
        public float Pad2;
    }

    public class TreeShader : Shader, IDisposable
    {
        bool disposed = false;

        Dictionary<VertexType, VertexShader> vsDict = new Dictionary<VertexType, VertexShader>();
        Dictionary<VertexType, byte[]> vsBytesDict = new Dictionary<VertexType, byte[]>();
        PixelShader ps;
        PixelShader psdef;

        GpuVarsBuffer<TreeShaderVSSceneVars> VSSceneVars;
        GpuVarsBuffer<TreeShaderVSEntityVars> VSEntityVars;
        GpuVarsBuffer<TreeShaderVSModelVars> VSModelVars;
        GpuVarsBuffer<TreeShaderVSGeomVars> VSGeomVars;
        GpuVarsBuffer<TreeShaderVSWindVars> VSWindVars;
        // Reuse BasicShader PS structs for PS compatibility
        GpuVarsBuffer<BasicShaderPSSceneVars> PSSceneVars;
        GpuVarsBuffer<BasicShaderPSGeomVars> PSGeomVars;
        SamplerState texsampler;
        SamplerState tintsampler;

        private Dictionary<VertexType, InputLayout> layouts = new Dictionary<VertexType, InputLayout>();

        public bool Deferred = false;
        public Vector4 WindVector = Vector4.Zero;
        public float WindTimer = 0.0f;

        public uint RenderMode = 0;
        public uint RenderModeIndex = 1;
        public uint RenderSamplerCoord = 0;


        public TreeShader(Device device)
        {
            byte[] vsbytes_pnct = PathUtil.ReadAllBytes("Shaders\\TreeVS_PNCT.cso");
            byte[] vsbytes_pncct = PathUtil.ReadAllBytes("Shaders\\TreeVS_PNCCT.cso");
            // Reuse BasicPS for pixel shading (same VS_OUTPUT format)
            byte[] psbytes = PathUtil.ReadAllBytes("Shaders\\BasicPS.cso");
            byte[] psdefbytes = PathUtil.ReadAllBytes("Shaders\\BasicPS_Deferred.cso");

            var vs_pnct = new VertexShader(device, vsbytes_pnct);
            var vs_pncct = new VertexShader(device, vsbytes_pncct);
            ps = new PixelShader(device, psbytes);
            psdef = new PixelShader(device, psdefbytes);

            vsDict[VertexType.Default] = vs_pnct;       // PNCT
            vsDict[VertexType.PNCCT] = vs_pncct;
            vsBytesDict[VertexType.Default] = vsbytes_pnct;
            vsBytesDict[VertexType.PNCCT] = vsbytes_pncct;

            // Pre-create layouts for all vertex types trees may use.
            // PNCT-based types: use PNCT VS (extra components in buffer are ignored by layout)
            layouts.Add(VertexType.Default, new InputLayout(device, vsbytes_pnct, VertexTypeGTAV.GetLayout(VertexType.Default)));
            layouts.Add(VertexType.PNCTT, new InputLayout(device, vsbytes_pnct, VertexTypeGTAV.GetLayout(VertexType.PNCTT)));
            layouts.Add(VertexType.PNCTTT, new InputLayout(device, vsbytes_pnct, VertexTypeGTAV.GetLayout(VertexType.PNCTTT)));
            layouts.Add(VertexType.DefaultEx, new InputLayout(device, vsbytes_pnct, VertexTypeGTAV.GetLayout(VertexType.DefaultEx)));
            layouts.Add(VertexType.PNCTTX, new InputLayout(device, vsbytes_pnct, VertexTypeGTAV.GetLayout(VertexType.PNCTTX)));
            layouts.Add(VertexType.PNCTTTX, new InputLayout(device, vsbytes_pnct, VertexTypeGTAV.GetLayout(VertexType.PNCTTTX)));
            // PNCCT-based types: use PNCCT VS (has Colour1)
            layouts.Add(VertexType.PNCCT, new InputLayout(device, vsbytes_pncct, VertexTypeGTAV.GetLayout(VertexType.PNCCT)));
            layouts.Add(VertexType.PNCCTT, new InputLayout(device, vsbytes_pncct, VertexTypeGTAV.GetLayout(VertexType.PNCCTT)));
            layouts.Add(VertexType.PNCCTX, new InputLayout(device, vsbytes_pncct, VertexTypeGTAV.GetLayout(VertexType.PNCCTX)));
            layouts.Add(VertexType.PNCCTTX, new InputLayout(device, vsbytes_pncct, VertexTypeGTAV.GetLayout(VertexType.PNCCTTX)));
            layouts.Add(VertexType.PNCCTTX_2, new InputLayout(device, vsbytes_pncct, VertexTypeGTAV.GetLayout(VertexType.PNCCTTX_2)));
            layouts.Add(VertexType.PNCCTTTX, new InputLayout(device, vsbytes_pncct, VertexTypeGTAV.GetLayout(VertexType.PNCCTTTX)));

            VSSceneVars = new GpuVarsBuffer<TreeShaderVSSceneVars>(device);
            VSEntityVars = new GpuVarsBuffer<TreeShaderVSEntityVars>(device);
            VSModelVars = new GpuVarsBuffer<TreeShaderVSModelVars>(device);
            VSGeomVars = new GpuVarsBuffer<TreeShaderVSGeomVars>(device);
            VSWindVars = new GpuVarsBuffer<TreeShaderVSWindVars>(device);
            PSSceneVars = new GpuVarsBuffer<BasicShaderPSSceneVars>(device);
            PSGeomVars = new GpuVarsBuffer<BasicShaderPSGeomVars>(device);

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
            tintsampler = new SamplerState(device, new SamplerStateDescription()
            {
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp,
                BorderColor = Color.Black,
                ComparisonFunction = Comparison.Always,
                Filter = Filter.MinMagMipPoint,
                MaximumAnisotropy = 1,
                MaximumLod = float.MaxValue,
                MinimumLod = 0,
                MipLodBias = 0,
            });
        }



        public override void SetShader(DeviceContext context)
        {
            context.PixelShader.Set(Deferred ? psdef : ps);
        }

        public override bool SetInputLayout(DeviceContext context, VertexType type)
        {
            InputLayout? layout;
            if (!layouts.TryGetValue(type, out layout))
            {
                return false;
            }

            // Determine which VS to use based on whether type has Colour1
            VertexShader? vs;
            if (!vsDict.TryGetValue(type, out vs))
            {
                // Check if it's a PNCCT-family type (has two colour channels)
                uint flags = (uint)type;
                bool hasColour1 = ((flags >> 5) & 1) == 1; // bit 5 = Colour1
                vs = hasColour1 ? vsDict[VertexType.PNCCT] : vsDict[VertexType.Default];
            }

            context.VertexShader.Set(vs);
            context.InputAssembler.InputLayout = layout;
            return true;
        }

        public override void SetSceneVars(DeviceContext context, Camera camera, Shadowmap shadowmap, ShaderGlobalLights lights)
        {
            VSSceneVars.Vars.ViewProj = Matrix.Transpose(camera.ViewProjMatrix);
            VSSceneVars.Vars.WindVector = WindVector;
            VSSceneVars.Update(context);
            VSSceneVars.SetVSCBuffer(context, 0);

            if (shadowmap != null)
            {
                shadowmap.SetFinalRenderResources(context);
            }

            PSSceneVars.Vars.GlobalLights = lights.Params;
            PSSceneVars.Vars.EnableShadows = (shadowmap != null) ? 1u : 0u;
            PSSceneVars.Vars.RenderMode = RenderMode;
            PSSceneVars.Vars.RenderModeIndex = RenderModeIndex;
            PSSceneVars.Vars.RenderSamplerCoord = RenderSamplerCoord;
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
            VSEntityVars.Vars.IsInstanced = 0;
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
            // VS Geom vars
            uint windflag = geom.EnableWind ? 1u : 0u;

            VSGeomVars.Vars.EnableTint = 0;
            VSGeomVars.Vars.TintYVal = 0.0f;
            VSGeomVars.Vars.IsDecal = 0;
            VSGeomVars.Vars.EnableWind = windflag;
            VSGeomVars.Vars.WindOverrideParams = geom.WindOverrideParams;
            VSGeomVars.Update(context);
            VSGeomVars.SetVSCBuffer(context, 4);

            // Tree wind vars from material
            VSWindVars.Vars.umGlobalParams = geom.UmGlobalParams;
            VSWindVars.Vars.WindGlobalParams = geom.WindGlobalParams;
            VSWindVars.Vars.GlobalTimer = WindTimer;
            VSWindVars.Update(context);
            VSWindVars.SetVSCBuffer(context, 9);

            // PS Geom vars
            RenderableTexture? diffuse = null;
            RenderableTexture? tintpal = null;

            PSGeomVars.Vars.EnableTexture = 0;
            PSGeomVars.Vars.EnableTint = 0;
            PSGeomVars.Vars.EnableNormalMap = 0;
            PSGeomVars.Vars.EnableSpecMap = 0;
            PSGeomVars.Vars.EnableDetailMap = 0;
            PSGeomVars.Vars.IsDecal = 0;
            PSGeomVars.Vars.IsEmissive = 0;
            PSGeomVars.Vars.IsDistMap = 0;
            PSGeomVars.Vars.bumpiness = geom.bumpiness;
            PSGeomVars.Vars.AlphaScale = 1.0f;
            PSGeomVars.Vars.HardAlphaBlend = 0.0f;
            PSGeomVars.Vars.AlphaMode = MaterialAlpha.Mode(geom.DrawableGeom.Shader.FileName.Hash, geom.DrawableGeom.Shader.RenderBucket);
            PSGeomVars.Vars.specMapIntMask = geom.specMapIntMask;
            PSGeomVars.Vars.specularIntensityMult = geom.specularIntensityMult;
            PSGeomVars.Vars.specularFalloffMult = geom.specularFalloffMult;
            PSGeomVars.Vars.specularFresnel = geom.specularFresnel;
            PSGeomVars.Vars.wetnessMultiplier = geom.wetnessMultiplier;
            PSGeomVars.Vars.SpecOnly = 0;

            if ((geom.RenderableTextures != null) && (geom.RenderableTextures.Length > 0))
            {
                for (int i = 0; i < geom.RenderableTextures.Length; i++)
                {
                    var itex = geom.RenderableTextures[i];
                    var ihash = geom.TextureParamHashes[i];
                    switch (ihash)
                    {
                        case ShaderParamNames.DiffuseSampler:
                            diffuse = itex;
                            break;
                        case ShaderParamNames.TintPaletteSampler:
                            tintpal = itex;
                            break;
                    }
                }
            }

            bool usediff = ((diffuse != null) && (diffuse.Texture2D != null) && (diffuse.ShaderResourceView != null));
            if (usediff)
            {
                PSGeomVars.Vars.EnableTexture = 1;
                context.PixelShader.SetSampler(0, texsampler);
                diffuse.SetPSResource(context, 0);
            }

            if (tintpal != null && tintpal.Texture2D != null)
            {
                context.VertexShader.SetSampler(0, tintsampler);
                tintpal.SetVSResource(context, 0);
            }

            PSGeomVars.Update(context);
            PSGeomVars.SetPSCBuffer(context, 2);
        }

        public override void UnbindResources(DeviceContext context)
        {
            context.VertexShader.SetConstantBuffer(0, null);
            context.VertexShader.SetConstantBuffer(2, null);
            context.VertexShader.SetConstantBuffer(3, null);
            context.VertexShader.SetConstantBuffer(4, null);
            context.VertexShader.SetConstantBuffer(9, null);
            context.PixelShader.SetConstantBuffer(0, null);
            context.PixelShader.SetConstantBuffer(2, null);
            context.PixelShader.SetSampler(0, null);
            context.PixelShader.SetShaderResource(0, null);
            context.VertexShader.Set(null);
            context.PixelShader.Set(null);
        }

        public void Dispose()
        {
            if (disposed) return;

            texsampler?.Dispose();
            tintsampler?.Dispose();

            foreach (var layout in layouts.Values)
                layout.Dispose();
            layouts.Clear();

            foreach (var vs in vsDict.Values)
                vs.Dispose();
            vsDict.Clear();

            VSSceneVars.Dispose();
            VSEntityVars.Dispose();
            VSModelVars.Dispose();
            VSGeomVars.Dispose();
            VSWindVars.Dispose();
            PSSceneVars.Dispose();
            PSGeomVars.Dispose();

            psdef.Dispose();
            ps.Dispose();

            disposed = true;
        }
    }
}
