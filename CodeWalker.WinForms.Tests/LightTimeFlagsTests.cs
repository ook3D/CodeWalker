using CodeWalker.GameFiles;
using CodeWalker.Rendering;
using Xunit;

namespace CodeWalker.WinForms.Tests;

public class LightTimeFlagsTests
{
    [Theory]
    [InlineData(0x00FC003Fu)]
    [InlineData(0x0003FFC0u)]
    [InlineData(0x00FFFFFFu)]
    [InlineData(0u)]
    [InlineData(0xFF000000u)]
    public void LightUsesHourlyFlagsThroughoutTheDay(uint flags)
    {
        var light = new RenderableLight { TimeFlags = flags };
        for (int hour = 0; hour < 24; hour++)
        {
            bool night = hour < 6 || hour >= 18;
            bool expected = flags == 0x00FC003Fu ? night : flags == 0x0003FFC0u ? !night : true;
            Assert.Equal(expected, light.IsActive(hour));
            Assert.Equal(expected, light.IsActive(hour + 0.999f));
        }
        Assert.Equal(light.IsActive(0), light.IsActive(24));
    }

    [Fact]
    public void EditingSourceTimeFlagsImmediatelyChangesLightVisibility()
    {
        var source = new CLightAttr { TimeFlags = 1u << 23 };
        var light = new RenderableLight();
        light.Init(source);
        Assert.False(light.IsActive(12));
        Assert.True(light.IsActive(23));

        source.TimeFlags = 1u << 12;
        Assert.True(light.IsActive(12));
        Assert.False(light.IsActive(23));
    }
}
