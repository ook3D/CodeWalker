using CodeWalker.GameFiles;
using CodeWalker.World;
using SharpDX;
using Xunit;

namespace CodeWalker.Core.Tests;

public class AudioShorelineTests
{
    [Fact]
    public void ShorelinesProduceIndependentSegmentsAtWaterLevel()
    {
        var rel = new RelFile();
        rel.RelDatas =
        [
            new Dat151ShoreLinePoolAudioSettings(rel) { Points = [new(1, 1), new(2, 1), new(2, 2)] },
            new Dat151ShoreLineLakeAudioSettings(rel) { Points = [new(3, 3), new(11, 3)] },
            new Dat151ShoreLineRiverAudioSettings(rel) { Points = [new(20, 20, 12), new(30, 30, 9), new(40, 40, 5)] },
            new Dat151ShoreLineOceanAudioSettings(rel) { Points = [new(1, 1), new(2, 2)] },
            new Dat151AmbientRule(rel),
            new Dat151ShoreLineLakeAudioSettings(rel),
            new Dat151ShoreLinePoolAudioSettings(rel) { Points = [new(1, 1)] }
        ];
        List<WaterQuad> water =
        [
            new() { minX = 100, maxX = 200, minY = 100, maxY = 200, z = 50 },
            new() { minX = 0, maxX = 10, minY = 0, maxY = 10, z = 8 }
        ];
        List<Vector3> vertices = [];

        AudioZones.AddShorelineVertices(rel, water, vertices);

        Assert.Equal(new Vector3[]
        {
            new(1, 1, 8), new(2, 1, 8),
            new(2, 1, 8), new(2, 2, 8),
            new(2, 2, 8), new(1, 1, 8),
            new(3, 3, 8), new(11, 3, 8),
            new(20, 20, 12), new(30, 30, 9),
            new(30, 30, 9), new(40, 40, 5),
            new(1, 1, 0), new(2, 2, 0)
        }, vertices);
    }

    [Fact]
    public void ClosedPoolsAreNotClosedTwiceAndMissingWaterFallsBackToSeaLevel()
    {
        var rel = new RelFile();
        rel.RelDatas =
        [
            new Dat151ShoreLinePoolAudioSettings(rel) { Points = [new(1, 1), new(2, 2), new(1, 1)] }
        ];
        List<Vector3> vertices = [];

        AudioZones.AddShorelineVertices(rel, [], vertices);

        Assert.Equal(new Vector3[] { new(1, 1, 0), new(2, 2, 0), new(2, 2, 0), new(1, 1, 0) }, vertices);
    }
}
