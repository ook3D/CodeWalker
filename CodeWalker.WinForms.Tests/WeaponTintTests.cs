using CodeWalker.Rendering;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Xunit;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace CodeWalker.WinForms.Tests;

public class WeaponTintTests
{
    [Theory]
    [InlineData("BasicPS.cso")]
    [InlineData("BasicPS_Deferred.cso")]
    public void WeaponPaletteUsesSelectedRow(string shader)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "CodeWalker.sln"))) root = root.Parent;
        Assert.NotNull(root);
        using var device = new Device(DriverType.Warp, DeviceCreationFlags.None);
        var context = device.ImmediateContext;
        using var vertexCode = ShaderBytecode.Compile("""
            struct Output {
                float4 Position : SV_POSITION; float3 Normal : NORMAL;
                float2 Texcoord0 : TEXCOORD0; float2 Texcoord1 : TEXCOORD1; float2 Texcoord2 : TEXCOORD2;
                float4 Shadows : TEXCOORD3; float4 LightShadow : TEXCOORD4;
                float4 Colour0 : COLOR0; float4 Colour1 : COLOR1; float4 Tint : COLOR2;
                float4 Tangent : TEXCOORD5; float4 Bitangent : TEXCOORD6; float3 CamRelPos : TEXCOORD7;
            };
            Output main(uint id : SV_VertexID) {
                Output o = (Output)0;
                o.Position = float4(id == 1 ? 3 : -1, id == 2 ? 3 : -1, 0, 1);
                o.Normal = float3(0,0,1); o.Colour0 = 1; o.Tint = 1;
                o.Tangent = float4(1,0,0,0); o.Bitangent = float4(0,1,0,0); o.CamRelPos = float3(0,0,-1);
                return o;
            }
            """, "main", "vs_4_0");
        using var vs = new VertexShader(device, vertexCode);
        using var ps = new PixelShader(device, File.ReadAllBytes(Path.Combine(root.FullName, "Shaders", shader)));
        using var sceneBuffer = new Buffer(device, Utilities.SizeOf<BasicShaderPSSceneVars>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
        using var geometryBuffer = new Buffer(device, Utilities.SizeOf<BasicShaderPSGeomVars>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
        var scene = new BasicShaderPSSceneVars { RenderMode = 5 };
        context.UpdateSubresource(ref scene, sceneBuffer);
        var geometry = new BasicShaderPSGeomVars { EnableTexture = 1, EnableTint = 2, AlphaMode = 3, AlphaScale = 1, IsEmissive = 1 };
        using var diffuseData = DataStream.Create(new[] { new Vector4(1, 1, 1, 32f / 255f) }, true, false);
        using var paletteData = DataStream.Create(new[] { new Vector4(1, 0, 0, 1), new Vector4(0, 1, 0, 1) }, true, false);
        var description = new Texture2DDescription { Width = 1, Height = 1, ArraySize = 1, MipLevels = 1, Format = Format.R32G32B32A32_Float, SampleDescription = new SampleDescription(1, 0), BindFlags = BindFlags.ShaderResource };
        using var diffuse = new Texture2D(device, description, new DataRectangle(diffuseData.DataPointer, 16));
        description.Height = 2;
        using var palette = new Texture2D(device, description, new DataRectangle(paletteData.DataPointer, 16));
        using var diffuseView = new ShaderResourceView(device, diffuse);
        using var paletteView = new ShaderResourceView(device, palette);
        description.Height = 1;
        description.BindFlags = BindFlags.RenderTarget;
        using var target = new Texture2D(device, description);
        using var targetView = new RenderTargetView(device, target);
        description.BindFlags = BindFlags.None;
        description.Usage = ResourceUsage.Staging;
        description.CpuAccessFlags = CpuAccessFlags.Read;
        using var staging = new Texture2D(device, description);
        using var sampler = new SamplerState(device, new SamplerStateDescription { Filter = Filter.MinMagMipPoint, AddressU = TextureAddressMode.Clamp, AddressV = TextureAddressMode.Clamp, AddressW = TextureAddressMode.Clamp, MaximumLod = float.MaxValue });
        using var rasterizer = new RasterizerState(device, new RasterizerStateDescription { FillMode = FillMode.Solid, CullMode = CullMode.None });
        context.Rasterizer.State = rasterizer;
        context.Rasterizer.SetViewport(0, 0, 1, 1);
        context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
        context.VertexShader.Set(vs);
        context.PixelShader.Set(ps);
        context.PixelShader.SetConstantBuffer(0, sceneBuffer);
        context.PixelShader.SetConstantBuffer(2, geometryBuffer);
        context.PixelShader.SetShaderResource(0, diffuseView);
        context.PixelShader.SetShaderResource(6, paletteView);
        context.PixelShader.SetSampler(0, sampler);
        context.OutputMerger.SetRenderTargets(targetView);
        for (int row = 0; row < 2; row++)
        {
            geometry.TintPaletteParams = new Vector4((row + 0.5f) / 2, 0, 0, 0);
            context.UpdateSubresource(ref geometry, geometryBuffer);
            context.ClearRenderTargetView(targetView, new Color4(0));
            context.Draw(3, 0);
            context.CopyResource(target, staging);
            var mapped = context.MapSubresource(staging, 0, MapMode.Read, MapFlags.None);
            var colour = Utilities.Read<Vector4>(mapped.DataPointer);
            context.UnmapSubresource(staging, 0);
            Assert.InRange(colour.X, row == 0 ? 0.99f : 0f, row == 0 ? 1.01f : 0.01f);
            Assert.InRange(colour.Y, row == 1 ? 0.99f : 0f, row == 1 ? 1.01f : 0.01f);
        }
        Vector4 DrawAndRead()
        {
            context.UpdateSubresource(ref scene, sceneBuffer);
            context.UpdateSubresource(ref geometry, geometryBuffer);
            context.ClearRenderTargetView(targetView, new Color4(0));
            context.Draw(3, 0);
            context.CopyResource(target, staging);
            var mapped = context.MapSubresource(staging, 0, MapMode.Read, MapFlags.None);
            var result = Utilities.Read<Vector4>(mapped.DataPointer);
            context.UnmapSubresource(staging, 0);
            return result;
        }
        foreach (float alpha in new[] { 0f, 31f, 31.75f, 161f, 255f })
        {
            using var pixels = DataStream.Create(new[] { new Vector4(1, 1, 1, alpha / 255f) }, true, false);
            context.UpdateSubresource(new DataBox(pixels.DataPointer, 16, 16), diffuse, 0);
            var colour = DrawAndRead();
            Assert.InRange(colour.X, 0.99f, 1.01f);
            Assert.InRange(colour.Y, 0.99f, 1.01f);
        }
        if (shader == "BasicPS_Deferred.cso")
        {
            using var specData = DataStream.Create(new[] { new Vector4(0.5f, 0.25f, 0.9f, 0.8f) }, true, false);
            using var specTexture = new Texture2D(device, diffuse.Description, new DataRectangle(specData.DataPointer, 16));
            using var specView = new ShaderResourceView(device, specTexture);
            context.PixelShader.SetShaderResource(3, specView);
            context.OutputMerger.SetRenderTargets((DepthStencilView?)null, new RenderTargetView[] { null!, null!, targetView });
            scene.RenderMode = 0;
            geometry.EnableSpecMap = 1;
            geometry.TintPaletteParams.Y = 1;
            geometry.specularIntensityMult = 1;
            geometry.specularFalloffMult = 512;
            geometry.specularFresnel = 0.75f;
            var packed = DrawAndRead();
            Assert.InRange(packed.X, 0.499f, 0.501f);
            Assert.InRange(packed.Y, 0.249f, 0.251f); // G is squared falloff; alpha is only the detail mask.
            Assert.InRange(packed.Z, 0.749f, 0.751f);

            context.OutputMerger.SetRenderTargets(targetView);
            using var black = DataStream.Create(new[] { new Vector4(0, 0, 0, 1) }, true, false);
            context.UpdateSubresource(new DataBox(black.DataPointer, 16, 16), diffuse, 0);
            scene.GlobalLights.LightDir = Vector3.UnitZ;
            geometry.WeaponSpecularColour = new Vector4(1, 0, 0, 2);
            geometry.TintPaletteParams.Z = 40;
            var highlight = DrawAndRead();
            // Encoded diffuse contains sqrt(2 * red^2 * Fresnel) at the highlight centre.
            Assert.InRange(highlight.X, 0.611f, 0.613f);
            Assert.InRange(highlight.Y, 0, 0.001f);
        }
        context.ClearState();
    }
}
