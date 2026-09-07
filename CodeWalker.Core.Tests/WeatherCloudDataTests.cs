using System.Xml;
using CodeWalker.GameFiles;
using CodeWalker.World;
using SharpDX;
using Xunit;

namespace CodeWalker.Core.Tests;

public class WeatherCloudDataTests
{
    private static XmlElement Parse(string xml)
    {
        var document = new XmlDocument();
        document.LoadXml(xml);
        return document.DocumentElement ?? throw new InvalidOperationException();
    }

    [Fact]
    public void UninitializedWeatherReturnsNeutralValues()
    {
        var weather = new Weather();
        Assert.Equal(0, weather.GetDynamicValue("missing"));
        Assert.Equal(Vector3.Zero, weather.GetDynamicRGB("r", "g", "b"));
        Assert.Equal(Vector4.Zero, weather.GetDynamicRGBA("r", "g", "b", "a"));
    }

    [Theory]
    [InlineData(-1, 12)]
    [InlineData(0, 12)]
    [InlineData(1, 18)]
    [InlineData(99, 18)]
    public void WeatherSamplesClampAndKeepInterpolation(int sample, float expected)
    {
        var region = new WeatherCycleKeyframeRegion();
        region.Init(Parse("<region name='GLOBAL'><value>10 20</value><empty /></region>"));
        Assert.Equal(expected, region.GetCurrentValue("value", sample, 0.8f), 4);
        Assert.Equal(0, region.GetCurrentValue("empty", sample, 0.8f));
        Assert.Equal(0, region.GetCurrentValue("missing", sample, 0.8f));
    }

    [Fact]
    public void ReloadingWeatherWithoutTimecycleClearsPreviousData()
    {
        var weather = new WeatherType { TimeCycleData = new WeatherCycleKeyframeData() };
        weather.TimeCycleData.Regions["GLOBAL"] = new WeatherCycleKeyframeRegion();
        Assert.NotNull(weather.GetRegion("GLOBAL"));
        var cache = new GameFileCache(0, 0, string.Empty, false, string.Empty, false, string.Empty);
        weather.Init(cache, Parse("<Item><Name>CLEAR</Name></Item>"));
        Assert.Null(weather.TimeCycleData);
        Assert.Null(weather.GetRegion("GLOBAL"));
    }

    [Fact]
    public void MissingCloudSettingsClearPreviousValues()
    {
        var item = new CloudSettingsMapItem();
        item.Init(Parse("""
            <Item><Settings>
              <CloudList><mProbability>100</mProbability><mBits>FF</mBits></CloudList>
              <CloudColor><keyData><numKeyEntries value="1" />
                <keyEntryData>0&#9;1&#9;2&#9;3&#9;4</keyEntryData>
              </keyData></CloudColor>
            </Settings></Item>
            """));
        Assert.Equal([100], item.CloudList.Probability);
        Assert.Equal([255], item.CloudList.Bits);
        Assert.Equal(new Vector4(1, 2, 3, 4), item.CloudColor.keyEntryData[0]);
        item.Init(Parse("<Item />"));
        Assert.Empty(item.CloudList.Probability);
        Assert.Empty(item.CloudList.Bits);
        Assert.Empty(item.CloudColor.keyEntryData);
        Assert.Equal(0, item.CloudColor.numKeyEntries);
    }

    [Fact]
    public void EmptyCloudCollectionsLoadAndLookupSafely()
    {
        var manager = new CloudHatManager();
        Assert.Null(manager.FindFrag("missing"));
        manager.Init(Parse("<CloudHatManager />"));
        Assert.Empty(manager.CloudHatFrags);
        Assert.Null(manager.FindFrag("missing"));
        var item = new CloudSettingsMapItem();
        item.Init(Parse("<Item><Settings><CloudList /><CloudColor><keyData /></CloudColor></Settings></Item>"));
        Assert.Empty(item.CloudList.Bits);
        Assert.Empty(item.CloudColor.keyEntryData);
    }
}
