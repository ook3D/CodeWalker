using CodeWalker.GameFiles;
using CodeWalker.Rendering;
using System.Xml;
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

public class TreeAlphaTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TreeMipAlphaUsesMaterialScaleAndReference(bool deferred)
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
                o.Normal = float3(0,0,1); o.Colour0 = float4(0.8,0.6,0.4,1); o.Tint = 1;
                o.Tangent = float4(1,0,0,0); o.Bitangent = float4(0,1,0,0); o.CamRelPos = float3(0,0,-1);
                return o;
            }
            """, "main", "vs_4_0");
        using var vs = new VertexShader(device, vertexCode);
        using var ps = new PixelShader(device, File.ReadAllBytes(Path.Combine(root.FullName, "Shaders",
            deferred ? "BasicPS_Deferred.cso" : "BasicPS.cso")));
        using var sceneBuffer = new Buffer(device, Utilities.SizeOf<BasicShaderPSSceneVars>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
        using var geometryBuffer = new Buffer(device, Utilities.SizeOf<BasicShaderPSGeomVars>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
        using var ambientBuffer = new Buffer(device, 16, ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
        var scene = new BasicShaderPSSceneVars { GlobalLights = new ShaderGlobalLightParams {
            LightDir = Vector3.UnitZ, LightNaturalAmbUp = new Color4(1, 0, 0, 0),
            LightArtificialAmbUp = new Color4(0, 1, 0, 0) } };
        var xml = new XmlDocument();
        xml.LoadXml("""
            <Item><Name>trees_normal_spec</Name><FileName>trees_normal_spec.sps</FileName>
            <RenderBucket value="3"/><Parameters>
            <Item name="AlphaScale" type="Vector" x="2.3" y="0" z="0" w="0"/>
            <Item name="AlphaTest" type="Vector" x="0.8" y="0" z="0" w="0"/>
            </Parameters></Item>
            """);
        var material = new grcInstanceData();
        material.ReadXml(xml.DocumentElement!);
        var renderable = new RenderableGeometry();
        renderable.Init(new grmGeometryQB { Shader = material });
        Assert.Equal(2.3f, renderable.TreeAlphaScale);
        Assert.Equal(0.8f, renderable.TreeAlphaTest);
        var geometry = new BasicShaderPSGeomVars { EnableTexture = 1, AlphaMode = 6,
            AlphaScale = renderable.TreeAlphaScale * 2, HardAlphaBlend = renderable.TreeAlphaTest };
        context.UpdateSubresource(ref scene, sceneBuffer);
        context.UpdateSubresource(ref geometry, geometryBuffer);
        var description = new Texture2DDescription { Width = 1, Height = 1, ArraySize = 1, MipLevels = 1,
            Format = Format.R32G32B32A32_Float, SampleDescription = new SampleDescription(1, 0), BindFlags = BindFlags.RenderTarget };
        using var target = new Texture2D(device, description);
        using var targetView = new RenderTargetView(device, target);
        description.BindFlags = BindFlags.None;
        description.Usage = ResourceUsage.Staging;
        description.CpuAccessFlags = CpuAccessFlags.Read;
        using var staging = new Texture2D(device, description);
        using var rasterizer = new RasterizerState(device, new RasterizerStateDescription { FillMode = FillMode.Solid, CullMode = CullMode.None });
        context.Rasterizer.State = rasterizer;
        context.Rasterizer.SetViewport(0, 0, 1, 1);
        context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
        context.VertexShader.Set(vs);
        context.PixelShader.Set(ps);
        context.PixelShader.SetConstantBuffer(0, sceneBuffer);
        context.PixelShader.SetConstantBuffer(2, geometryBuffer);
        context.PixelShader.SetConstantBuffer(10, ambientBuffer);
        context.OutputMerger.SetRenderTargets(targetView);
        description.Usage = ResourceUsage.Default;
        description.CpuAccessFlags = CpuAccessFlags.None;
        description.BindFlags = BindFlags.ShaderResource;
        using var diffuse = new Texture2D(device, description);
        using var diffuseView = new ShaderResourceView(device, diffuse);
        using var sampler = new SamplerState(device, new SamplerStateDescription { Filter = Filter.MinMagMipLinear, AddressU = TextureAddressMode.Clamp, AddressV = TextureAddressMode.Clamp, AddressW = TextureAddressMode.Clamp, MaximumLod = float.MaxValue });
        context.PixelShader.SetShaderResource(0, diffuseView);
        context.PixelShader.SetSampler(0, sampler);
        // The supplied oak's smaller mips contain alpha below the old 0.5 cutoff.
        foreach (float alpha in new[] { 0f, 0.1f, 0.17f, 0.18f, 0.25f, 0.49f, 1f })
        {
            using var pixels = DataStream.Create(new[] { new Vector4(1, 1, 1, alpha) }, true, false);
            context.UpdateSubresource(new DataBox(pixels.DataPointer, 16, 16), diffuse, 0);
            context.ClearRenderTargetView(targetView, new Color4(-1f));
            context.Draw(3, 0);
            context.CopyResource(target, staging);
            var mapped = context.MapSubresource(staging, 0, MapMode.Read, MapFlags.None);
            var result = Utilities.Read<Vector4>(mapped.DataPointer);
            context.UnmapSubresource(staging, 0);
            Assert.Equal(alpha > 0.8f / 4.6f, result.W >= 0);
        }
        context.ClearState();
    }
}
