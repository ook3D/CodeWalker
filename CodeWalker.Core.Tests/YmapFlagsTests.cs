using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class YmapFlagsTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void InteriorOcclusionFlagRequiresOccluders(bool hasBoxes, bool hasModels)
    {
        var ymap = new YmapFile { CMloInstanceDefs = [new CMloInstanceDef()] };
        if (hasBoxes) ymap.BoxOccluders = [new YmapBoxOccluder(ymap, new BoxOccluder())];
        if (hasModels) ymap.OccludeModels = [new YmapOccludeModel(ymap, new OccludeModel())];

        ymap.CalcFlags();

        Assert.Equal(8u | (hasBoxes || hasModels ? 32u : 0u), ymap.CMapData.contentFlags);
        Assert.False(ymap.CalcFlags());

        ymap.BoxOccluders = [];
        ymap.OccludeModels = [];
        ymap.CalcFlags();
        Assert.Equal(8u, ymap.CMapData.contentFlags);
    }
}
