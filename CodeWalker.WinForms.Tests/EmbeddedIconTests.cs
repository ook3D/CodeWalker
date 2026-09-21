using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using Xunit;

namespace CodeWalker.WinForms.Tests;

public class EmbeddedIconTests
{
    [Theory]
    [InlineData("icon_google_marker_64x64.png", 64)]
    [InlineData("icon_glokon_normal_32x32.png", 32)]
    [InlineData("icon_glokon_debug_32x32.png", 32)]
    public void MapIconLoadsTextureFromEmbeddedResource(string filename, int size)
    {
        using var device = new Device(DriverType.Warp, DeviceCreationFlags.None);
        var icon = new MapIcon("Test", filename, size, size, 0, 0, 1);
        var errors = new List<string>();
        try
        {
            icon.LoadTexture(device, errors.Add);
            Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
            Assert.NotNull(icon.Tex);
            Assert.Equal(size, icon.Tex.Description.Width);
            Assert.Equal(size, icon.Tex.Description.Height);
            Assert.NotNull(icon.TexView);
        }
        finally { icon.UnloadTexture(); }
    }
}
