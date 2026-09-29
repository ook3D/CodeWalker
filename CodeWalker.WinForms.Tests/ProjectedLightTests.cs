using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using CodeWalker.GameFiles;
using CodeWalker.Rendering;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Xunit;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;

namespace CodeWalker.WinForms.Tests;

public class ProjectedLightTests
{
    [Fact]
    public void SpotlightSamplesTextureInteriorOffAxis()
    {
        // Run the actual shared light shader on WARP. A black border must not replace
        // the green texture interior on surfaces well inside the spotlight cone.
        var source = ReadShader("LightPS.hlsli") + """
            RWStructuredBuffer<float4> Result : register(u0);
            [numthreads(1, 1, 1)]
            void main() {
                Result[0] = DeferredLight(float3(0.2, 0.2, 1), float3(0, 0, -1),
                    float4(1, 1, 1, 1), 0, 0);
            }
            """;
        using var code = ShaderBytecode.Compile(source, "main", "cs_5_0");
        using var device = new Device(DriverType.Warp, DeviceCreationFlags.None);
        var context = device.ImmediateContext;
        using var shader = new ComputeShader(device, code);
        using var constants = Buffer.Create(device, BindFlags.ConstantBuffer, new[] {
            new DeferredLightInstVars {
                InstColour = Vector3.One, InstDirection = Vector3.UnitZ,
                InstTangentX = Vector3.UnitX, InstTangentY = Vector3.UnitY,
                InstFalloff = 10, InstFalloffExponent = 1,
                InstConeInnerAngle = 0.7f, InstConeOuterAngle = MathF.PI / 4,
                InstType = 2, InstTextureEnable = 1
            }
        });
        var pixels = new uint[16];
        pixels[5] = pixels[6] = pixels[9] = pixels[10] = 0xff00ff00;
        using var data = DataStream.Create(pixels, true, false);
        using var texture = new Texture2D(device, new Texture2DDescription {
            Width = 4, Height = 4, MipLevels = 1, ArraySize = 1,
            Format = Format.R8G8B8A8_UNorm, SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Immutable, BindFlags = BindFlags.ShaderResource
        }, new DataRectangle(data.DataPointer, 16));
        using var srv = new ShaderResourceView(device, texture);
        using var sampler = new SamplerState(device, new SamplerStateDescription {
            Filter = Filter.MinMagMipPoint, AddressU = TextureAddressMode.Clamp,
            AddressV = TextureAddressMode.Clamp, AddressW = TextureAddressMode.Clamp,
            MaximumLod = float.MaxValue
        });
        using var result = new Buffer(device, 16, ResourceUsage.Default, BindFlags.UnorderedAccess,
            CpuAccessFlags.None, ResourceOptionFlags.BufferStructured, 16);
        using var uav = new UnorderedAccessView(device, result);
        using var staging = new Buffer(device, 16, ResourceUsage.Staging, BindFlags.None,
            CpuAccessFlags.Read, ResourceOptionFlags.None, 0);
        context.ComputeShader.Set(shader);
        context.ComputeShader.SetConstantBuffer(2, constants);
        context.ComputeShader.SetShaderResource(7, srv);
        context.ComputeShader.SetSampler(0, sampler);
        context.ComputeShader.SetUnorderedAccessView(0, uav);
        context.Dispatch(1, 1, 1);
        context.CopyResource(result, staging);
        context.MapSubresource(staging, MapMode.Read, SharpDX.Direct3D11.MapFlags.None, out DataStream readback);
        var colour = readback.Read<Vector4>();
        readback.Dispose();
        context.UnmapSubresource(staging, 0);
        Assert.True(colour.Y > 0.01f, $"Expected projected green light, got {colour}");
        Assert.Equal(0, colour.X);
        Assert.Equal(0, colour.Z);
    }

    [Fact]
    public void LightOnlyDrawableRetriesRejectedTextureDictionary()
    {
        var cache = new GameFileCache(0, 10, "", false, "", false, "") { IsInited = true };
        cache.YtdDict[101] = new RpfResourceFileEntry { Name = "light.ytd", ShortNameHash = 101 };
        var renderer = new Renderer(null!, cache);
        var drawable = new gtaDrawable();
        var resolve = typeof(Renderer).GetMethod("TryGetRenderable", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Renderable Resolve() => (Renderable)resolve.Invoke(renderer,
            [new Archetype { TextureDict = 101 }, drawable, 0u, null, null])!;
        var renderable = Resolve();
        renderable.InitLights([new CLightAttr { ProjectedTextureKey = 7 }]);
        var rejected = renderable.SDtxds![0];
        var cpuCache = (Cache<GameFileCacheKey, GameFile>)typeof(GameFileCache)
            .GetField("mainCache", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(cache)!;
        cpuCache.MaxMemoryUsage = 1024 * 1024;
        Resolve();
        var ytd = renderable.SDtxds[0];
        Assert.NotSame(rejected, ytd);
        Assert.True(ytd.LoadQueued);
        var texture = new Texture { NameHash = 7 };
        ytd.TextureDict = new TextureDictionary();
        ytd.TextureDict.BuildFromTextureList([texture]);
        ytd.Loaded = true;
        Resolve();
        Assert.Same(texture, renderable.Lights[0].RenderableProjectedTexture!.Key);
    }

    private static string ReadShader(string name, [CallerFilePath] string testFile = "")
    {
        var path = Path.Combine(Path.GetDirectoryName(testFile)!, "..", "CodeWalker.Shaders", name);
        return Regex.Replace(File.ReadAllText(path), "#include \"([^\"]+)\"",
            match => ReadShader(match.Groups[1].Value, testFile));
    }
}
