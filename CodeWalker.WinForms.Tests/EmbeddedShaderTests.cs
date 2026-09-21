using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using Xunit;

namespace CodeWalker.WinForms.Tests;

public class EmbeddedShaderTests
{
    [Fact]
    public async Task EveryEmbeddedShaderLoadsThroughBothPathsAndCreatesOnDevice()
    {
        var names = typeof(PathUtil).Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith("CodeWalker.Shaders.", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(names);
        using var device = new Device(DriverType.Warp, DeviceCreationFlags.None);
        foreach (var name in names)
        {
            var filename = name["CodeWalker.Shaders.".Length..];
            var bytes = PathUtil.ReadAllBytes("Shaders\\" + filename);
            Assert.Equal(bytes, await PathUtil.ReadAllBytesAsync("Shaders/" + filename));
            using var reflection = new ShaderReflection(bytes);
            using DeviceChild shader = (reflection.Description.Version >> 16) switch
            {
                0 => new PixelShader(device, bytes),
                1 => new VertexShader(device, bytes),
                2 => new GeometryShader(device, bytes),
                3 => new HullShader(device, bytes),
                4 => new DomainShader(device, bytes),
                5 => new ComputeShader(device, bytes),
                _ => throw new InvalidOperationException("Unknown shader type: " + filename)
            };
        }
    }
}
