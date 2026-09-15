using CodeWalker.GameFiles;
using CodeWalker.Rendering;
using CodeWalker.World;
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

public class AmbientScaleTests
{
    private static Vector2 Evaluate(Archetype? archetype, YmapEntityDef? entity,
        Dictionary<uint, TimecycleMod>? modifiers = null) => (Vector2)typeof(Renderer).Assembly
        .GetType("CodeWalker.Rendering.InteriorLighting")!.GetMethod("GetAmbientScale")!
        .Invoke(null, [archetype, entity, modifiers])!;

    [Fact]
    public void FlagUsesBakedScalesAndRespondsToLiveEdits()
    {
        var archetype = new Archetype();
        var entity = new YmapEntityDef { Archetype = archetype,
            _CEntityDef = new CEntityDef { ambientOcclusionMultiplier = 128, artificialAmbientOcclusion = 64 } };
        Assert.Equal(Vector2.One, Evaluate(archetype, entity));
        archetype._BaseArchetypeDef.flags = 1u << 29;
        Assert.True(archetype.UseAmbientScale);
        Assert.Equal(new Vector2(128, 64) / 255f, Evaluate(archetype, entity));
        Assert.Equal(new Vector2(128, 64) / 255f, Evaluate(null, entity));
        entity._CEntityDef.ambientOcclusionMultiplier = 0;
        Assert.Equal(new Vector2(0, 64) / 255f, Evaluate(archetype, entity));
        archetype._BaseArchetypeDef.flags = 1u << 28;
        Assert.Equal(Vector2.One, Evaluate(archetype, entity));
        Assert.Equal(Vector2.One, Evaluate(null, null));
    }

    [Theory]
    [InlineData(1f, 51, 204)]
    [InlineData(0.5f, 153, 229)]
    [InlineData(0f, 255, 0)]
    public void DynamicScaleUsesObjectRoomAndSecondaryOverrides(float blend, int natural, int artificial)
    {
        var archetype = new Archetype { _BaseArchetypeDef = new CBaseArchetypeDef { flags = 1u << 29 } };
        var entity = new YmapEntityDef { Archetype = archetype, Position = new Vector3(101, 1, 1),
            _CEntityDef = new CEntityDef { ambientOcclusionMultiplier = 255, artificialAmbientOcclusion = 17 } };
        Assert.Equal(new Vector2(1, 0), Evaluate(archetype, entity));
        Assert.Equal(new Vector2(1, 0), Evaluate(archetype, null));
        entity.MloParent = new YmapEntityDef { Position = new Vector3(100, 0, 0),
            Orientation = Quaternion.Identity, Scale = Vector3.One,
            Archetype = new MloArchetype { rooms = [new MCMloRoomDef { Index = 1,
                _Data = new CMloRoomDef { bbMin = Vector3.Zero, bbMax = new Vector3(3),
                    timecycleName = 1, secondaryTimecycleName = 2, blend = blend } }] } };
        var primary = new TimecycleMod();
        primary.Dict["natural_ambient_multiplier"] = new() { value1 = 0.2f, value2 = 99 };
        primary.Dict["artificial_int_ambient_multiplier"] = new() { value1 = 0.4f };
        var secondary = new TimecycleMod();
        secondary.Dict["artificial_int_ambient_multiplier"] = new() { value1 = 0.8f };
        var modifiers = new Dictionary<uint, TimecycleMod> { [1] = primary, [2] = secondary };
        Assert.Equal(new Vector2(natural, artificial) / 255f, Evaluate(archetype, entity, modifiers));
        entity.Position = Vector3.Zero;
        Assert.Equal(new Vector2(1, 0), Evaluate(archetype, entity, modifiers));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompiledShadersApplyAndResetBothAmbientChannels(bool deferred)
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
        var geometry = new BasicShaderPSGeomVars { AlphaMode = 3, AlphaScale = 1 };
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
        context.OutputMerger.SetRenderTargets(null, deferred
            ? new RenderTargetView[] { null!, null!, null!, targetView } : [targetView]);
        foreach (var scale in new[] { Vector2.One, new Vector2(0.5f, 0.25f), Vector2.Zero, Vector2.One })
        {
            var constants = new Vector4(scale, 0, 0);
            context.UpdateSubresource(ref constants, ambientBuffer);
            context.ClearRenderTargetView(targetView, new Color4(-1));
            context.Draw(3, 0);
            context.CopyResource(target, staging);
            var mapped = context.MapSubresource(staging, 0, MapMode.Read, MapFlags.None);
            var result = Utilities.Read<Vector4>(mapped.DataPointer);
            context.UnmapSubresource(staging, 0);
            float Expected(float channel) => deferred ? MathF.Sqrt(channel * 0.5f) : channel * channel;
            Assert.InRange(result.X, Expected(0.8f * scale.X) - 0.0001f, Expected(0.8f * scale.X) + 0.0001f);
            Assert.InRange(result.Y, Expected(0.6f * scale.Y) - 0.0001f, Expected(0.6f * scale.Y) + 0.0001f);
        }
        context.ClearState();
    }
}
