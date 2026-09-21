using CodeWalker.GameFiles;
using CodeWalker.World;
using SharpDX;
using Xunit;

namespace CodeWalker.Core.Tests;

public class AudioShorelineTests
{
    [Theory]
    [InlineData(Dat151RelType.ShoreLinePoolAudioSettings)]
    [InlineData(Dat151RelType.ShoreLineLakeAudioSettings)]
    [InlineData(Dat151RelType.ShoreLineRiverAudioSettings)]
    [InlineData(Dat151RelType.ShoreLineOceanAudioSettings)]
    public void RotatedBoxesFitPointsAndPreservePaddingDuringEdits(Dat151RelType type)
    {
        var rel = new RelFile { RelType = RelDatFileType.Dat151 };
        Vector2[] points = [new(0, 0), new(10, 20)];
        Dat151RelData record = type switch
        {
            Dat151RelType.ShoreLinePoolAudioSettings => new Dat151ShoreLinePoolAudioSettings(rel) { Points = points, PointsCount = 2, RotationAngle = 90 },
            Dat151RelType.ShoreLineLakeAudioSettings => new Dat151ShoreLineLakeAudioSettings(rel) { Points = points, NumShorelinePoints = 2, RotationAngle = 90 },
            Dat151RelType.ShoreLineRiverAudioSettings => new Dat151ShoreLineRiverAudioSettings(rel) { Points = [new(0, 0, 5), new(10, 20, 7)], PointsCount = 2, RotationAngle = 90 },
            _ => new Dat151ShoreLineOceanAudioSettings(rel) { Points = points, PointsCount = 2, RotationAngle = 90 }
        };
        rel.RelDatas = rel.RelDatasSorted = [record];
        var placement = new AudioPlacement(rel, record);
        var property = record.GetType().GetProperty("ActivationBox")!;
        Vector4 Box() => (Vector4)property.GetValue(record)!;
        placement.RecalculateShorelineBox(20);
        Assert.True(Vector4.Distance(new(5, 10, 60, 50), Box()) < 0.001f);
        placement.ShorelinePoints[1].SetPosition(new(20, 40, 7));
        Assert.True(Vector4.Distance(new(10, 20, 80, 60), Box()) < 0.001f);
        placement.SetPosition(placement.Position + new Vector3(100, -50, 0));
        Assert.True(Vector4.Distance(new(110, -30, 80, 60), Box()) < 0.001f);
        var inserted = placement.InsertShorelinePoint(2, new(130, 10, 7));
        Assert.True(Vector4.Distance(new(115, -20, 100, 70), Box()) < 0.001f);
        placement.RemoveShorelinePoint(inserted);
        Assert.True(Vector4.Distance(new(110, -30, 80, 60), Box()) < 0.001f);
        var saved = new RelFile();
        saved.Load(rel.Save(), null);
        Assert.Equal(Box(), (Vector4)property.GetValue(Assert.Single(saved.RelDatas))!);
    }

    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(1, 1, -1)]
    [InlineData(2, 0, -1)]
    [InlineData(3, -1, -1)]
    [InlineData(4, -1, 0)]
    [InlineData(5, -1, 1)]
    [InlineData(6, 0, 1)]
    [InlineData(7, 1, 1)]
    public void OceanDirectionUsesGameCompassMappingAndWaterSide(byte direction, float x, float y)
    {
        Vector2[] points = [new(100, 200), new(100 + x * 10, 200 + y * 10)];
        Assert.Equal(direction, AudioPlacement.CalculateOceanDirection(points, true));
        Assert.Equal((byte)((direction + 4) % 8), AudioPlacement.CalculateOceanDirection(points, false));
        Assert.Equal(direction, AudioPlacement.CalculateOceanDirection(points.Reverse().ToArray(), false));
        Assert.Equal(0, AudioPlacement.CountParallelOceanSegments(points, direction));
        Assert.Equal(1, AudioPlacement.CountParallelOceanSegments(points, (byte)((direction + 2) % 8)));
    }

    [Fact]
    public void OceanDirectionWeightsSegmentLengthsAndRejectsUndefinedSide()
    {
        Vector2[] points = [new(0, 0), new(100, 0), new(100, 0), new(100, 1)];
        Assert.Equal(0, AudioPlacement.CalculateOceanDirection(points, true));
        Assert.Equal(1, AudioPlacement.CountParallelOceanSegments(points, 0));
        Assert.Throws<InvalidOperationException>(() => AudioPlacement.CalculateOceanDirection([], true));
        Assert.Throws<InvalidOperationException>(() => AudioPlacement.CalculateOceanDirection([new(0, 0), new(10, 10), new(0, 0)], true));
    }

    [Fact]
    public void RemovingShorelineRemovesListReferencesAndLinksAndRoundTrips()
    {
        var rel = new RelFile { RelType = RelDatFileType.Dat151 };
        var ocean = new Dat151ShoreLineOceanAudioSettings(rel) { NameHash = 101, Points = [new(0, 0), new(10, 10)], PointsCount = 2 };
        var lake = new Dat151ShoreLineLakeAudioSettings(rel) { NameHash = 102, NextShoreline = 101 };
        var river = new Dat151ShoreLineRiverAudioSettings(rel) { NameHash = 103, NextShoreline = 101 };
        var other = new Dat151ShoreLineOceanAudioSettings(rel) { NameHash = 104, NextShoreline = 102 };
        var list = new Dat151ShoreLineList(rel) { NameHash = 105, ShoreLines = [101, 102, 101, 104], ShoreLineCount = 4 };
        rel.RelDatas = rel.RelDatasSorted = [ocean, lake, river, other, list];
        var placement = new AudioPlacement(rel, ocean);
        Assert.True(placement.RemoveShoreline());
        Assert.False(placement.RemoveShoreline());
        Assert.DoesNotContain(ocean, rel.RelDatas);
        Assert.DoesNotContain(ocean, rel.RelDatasSorted);
        Assert.Equal(new MetaHash[] { 102, 104 }, list.ShoreLines);
        Assert.Equal(2u, list.ShoreLineCount);
        Assert.Equal(0u, lake.NextShoreline.Hash);
        Assert.Equal(0u, river.NextShoreline.Hash);
        Assert.Equal(102u, other.NextShoreline.Hash);
        var saved = new RelFile();
        saved.Load(rel.Save(), null);
        Assert.Equal(4, saved.RelDatas.Length);
        Assert.Equal(list.ShoreLines, Assert.Single(saved.RelDatas.OfType<Dat151ShoreLineList>()).ShoreLines);
    }

    [Fact]
    public void RemovingPointRenumbersRemainingHandlesAndProtectsLastPoint()
    {
        var rel = new RelFile { RelType = RelDatFileType.Dat151 };
        var lake = new Dat151ShoreLineLakeAudioSettings(rel) { Points = [new(0, 0), new(10, 10), new(20, 20)], NumShorelinePoints = 3 };
        rel.RelDatas = rel.RelDatasSorted = [lake];
        var placement = new AudioPlacement(rel, lake);
        var last = placement.ShorelinePoints[2];
        placement.RemoveShorelinePoint(placement.ShorelinePoints[1]);
        Assert.Same(last, placement.ShorelinePoints[1]);
        Assert.Equal(1, last.ShorelinePointIndex);
        Assert.Equal(2, lake.NumShorelinePoints);
        var saved = new RelFile();
        saved.Load(rel.Save(), null);
        Assert.Equal(lake.Points, Assert.IsType<Dat151ShoreLineLakeAudioSettings>(Assert.Single(saved.RelDatas)).Points);
        placement.RemoveShorelinePoint(last);
        Assert.Throws<InvalidOperationException>(() => placement.RemoveShorelinePoint(placement.ShorelinePoints[0]));
    }

    [Theory]
    [InlineData(Dat151RelType.ShoreLinePoolAudioSettings)]
    [InlineData(Dat151RelType.ShoreLineLakeAudioSettings)]
    [InlineData(Dat151RelType.ShoreLineRiverAudioSettings)]
    [InlineData(Dat151RelType.ShoreLineOceanAudioSettings)]
    public void DuplicateMoveUndoRedoPreservesPointIdentityAndSavesCounts(Dat151RelType type)
    {
        var rel = new RelFile { RelType = RelDatFileType.Dat151 };
        Vector2[] points = [new(0, 0), new(10, 10), new(20, 20)];
        Dat151RelData record = type switch
        {
            Dat151RelType.ShoreLinePoolAudioSettings => new Dat151ShoreLinePoolAudioSettings(rel) { Points = points, PointsCount = 3 },
            Dat151RelType.ShoreLineLakeAudioSettings => new Dat151ShoreLineLakeAudioSettings(rel) { Points = points, NumShorelinePoints = 3 },
            Dat151RelType.ShoreLineRiverAudioSettings => new Dat151ShoreLineRiverAudioSettings(rel) { Points = [new(0, 0, 1), new(10, 10, 2), new(20, 20, 3)], PointsCount = 3 },
            _ => new Dat151ShoreLineOceanAudioSettings(rel) { Points = points, PointsCount = 3 }
        };
        rel.RelDatas = rel.RelDatasSorted = [record];
        var shoreline = new AudioPlacement(rel, record);
        var source = shoreline.ShorelinePoints[1];
        var last = shoreline.ShorelinePoints[2];
        var start = source.Position;
        var copy = source.DuplicateShorelinePoint();
        Assert.Equal(4, shoreline.ShorelinePoints.Length);
        Assert.Same(copy, shoreline.ShorelinePoints[2]);
        Assert.Same(last, shoreline.ShorelinePoints[3]);
        Assert.Equal(3, last.ShorelinePointIndex);
        copy.SetPosition(start + new Vector3(25, -30, 5));
        Assert.Equal(start, source.Position);
        var end = copy.Position;
        shoreline.RemoveShorelinePoint(copy);
        Assert.Equal(3, shoreline.ShorelinePoints.Length);
        Assert.Same(last, shoreline.ShorelinePoints[2]);
        Assert.Equal(2, last.ShorelinePointIndex);
        shoreline.InsertShorelinePoint(2, end, copy);
        Assert.Same(copy, shoreline.ShorelinePoints[2]);
        Assert.Equal(end, copy.Position);

        var saved = new RelFile();
        saved.Load(rel.Save(), null);
        var reloaded = new AudioPlacement(saved, Assert.IsAssignableFrom<Dat151RelData>(Assert.Single(saved.RelDatas)));
        Assert.Equal(shoreline.ShorelinePoints.Select(p => p.Position), reloaded.ShorelinePoints.Select(p => p.Position));
    }

    [Fact]
    public void DuplicatingClosingPoolPointInsertsBeforeClosureAndMovesOnlyTheCopy()
    {
        var rel = new RelFile();
        var pool = new Dat151ShoreLinePoolAudioSettings(rel) { Points = [new(0, 0), new(10, 0), new(10, 10), new(0, 0)], PointsCount = 4 };
        var shoreline = new AudioPlacement(rel, pool);
        var closingPoint = shoreline.ShorelinePoints[3];
        var copy = closingPoint.DuplicateShorelinePoint();
        Assert.Equal(3, copy.ShorelinePointIndex);
        Assert.Equal(4, closingPoint.ShorelinePointIndex);
        copy.SetPosition(new Vector3(0, 10, 0));
        Assert.Equal(Vector2.Zero, pool.Points[0]);
        Assert.Equal(Vector2.Zero, pool.Points[4]);
        Assert.Equal(new Vector2(0, 10), pool.Points[3]);
        shoreline.RemoveShorelinePoint(copy);
        Assert.Equal((ushort)4, pool.PointsCount);
        Assert.Same(closingPoint, shoreline.ShorelinePoints[3]);
    }

    [Fact]
    public void LakePointLimitRejectsDuplicationWithoutChangingData()
    {
        var rel = new RelFile();
        var lake = new Dat151ShoreLineLakeAudioSettings(rel) { Points = new Vector2[255], NumShorelinePoints = 255 };
        var shoreline = new AudioPlacement(rel, lake);
        Assert.Throws<InvalidOperationException>(() => shoreline.ShorelinePoints[0].DuplicateShorelinePoint());
        Assert.Equal(255, lake.Points.Length);
        Assert.Equal(255, shoreline.ShorelinePoints.Length);
        Assert.Equal((byte)255, lake.NumShorelinePoints);
    }

    [Theory]
    [InlineData(Dat151RelType.ShoreLinePoolAudioSettings)]
    [InlineData(Dat151RelType.ShoreLineLakeAudioSettings)]
    [InlineData(Dat151RelType.ShoreLineOceanAudioSettings)]
    public void Moving2DShorelinesAndPointsUpdatesRecordsAndCanBeReversed(Dat151RelType type)
    {
        var rel = new RelFile { RelType = RelDatFileType.Dat151 };
        Vector2[] points = [new(0, 0), new(10, 0), new(10, 10)];
        Dat151RelData data = type switch
        {
            Dat151RelType.ShoreLinePoolAudioSettings => new Dat151ShoreLinePoolAudioSettings(rel) { Points = points, PointsCount = 3 },
            Dat151RelType.ShoreLineLakeAudioSettings => new Dat151ShoreLineLakeAudioSettings(rel) { Points = points, NumShorelinePoints = 3 },
            _ => new Dat151ShoreLineOceanAudioSettings(rel) { Points = points, PointsCount = 3 }
        };
        rel.RelDatas = rel.RelDatasSorted = [data];
        var zones = new AudioZones();
        var placement = Assert.IsType<AudioPlacement>(zones.FindPlacement(rel, data));
        Assert.Same(placement, zones.FindPlacement(rel, data));
        var start = placement.Position;

        placement.SetPosition(start + new Vector3(12, 24, 100));
        Assert.Equal(new Vector2[] { new(12, 24), new(22, 24), new(22, 34) }, points);
        Assert.Equal(0, placement.Position.Z);
        Assert.Equal(new Vector3(22, 24, 0), placement.ShorelinePoints[1].Position);
        placement.SetPosition(start);
        Assert.True(Vector2.Distance(new Vector2(0, 0), points[0]) < 0.0001f);

        var point = placement.ShorelinePoints[1];
        var pointStart = point.Position;
        point.SetPosition(new Vector3(20, 5, 99));
        Assert.Equal(new Vector2(20, 5), points[1]);
        Assert.Equal(new Vector2(10, 10), points[2]);

        // XML and binary saves both read the same edited point array.
        var fromXml = XmlRel.GetRel(RelXml.GetXml(rel));
        var saved = new RelFile();
        saved.Load(fromXml.Save(), null);
        var savedPlacement = new AudioPlacement(saved, Assert.IsAssignableFrom<Dat151RelData>(Assert.Single(saved.RelDatas)));
        Assert.Equal(point.Position, savedPlacement.ShorelinePoints[1].Position);
        point.SetPosition(pointStart);
        Assert.True(Vector2.Distance(new Vector2(10, 0), points[1]) < 0.0001f);
    }

    [Fact]
    public void RiverMovementPreservesOffsetsAndIndividualPointMovementIsIndependent()
    {
        var rel = new RelFile();
        var river = new Dat151ShoreLineRiverAudioSettings(rel)
        {
            Points = [new(0, 0, 10), new(10, 10, 20)],
            ActivationBox = new Vector4(5, 5, 14, 14), DefaultHeight = 15
        };
        var placement = new AudioPlacement(rel, river);
        var start = placement.Position;
        placement.SetPosition(start + new Vector3(2, 4, 6));
        Assert.Equal(new Vector3[] { new(2, 4, 16), new(12, 14, 26) }, river.Points);
        Assert.Equal(new Vector4(7, 9, 14, 14), river.ActivationBox);
        Assert.Equal(21, river.DefaultHeight);
        placement.SetPosition(start);
        var point = placement.ShorelinePoints[1];
        point.SetPosition(new Vector3(20, 30, 40));
        Assert.Equal(new Vector3(0, 0, 10), river.Points[0]);
        Assert.Equal(new Vector3(20, 30, 40), river.Points[1]);
        Assert.Equal(new Vector4(10, 15, 24, 34), river.ActivationBox);
        point.SetPosition(new Vector3(10, 10, 20));
        Assert.Equal(new Vector4(5, 5, 14, 14), river.ActivationBox);
    }

    [Fact]
    public void MovingClosingPoolPointKeepsTheLoopClosed()
    {
        var rel = new RelFile();
        var pool = new Dat151ShoreLinePoolAudioSettings(rel) { Points = [new(0, 0), new(10, 0), new(10, 10), new(0, 0)] };
        var placement = new AudioPlacement(rel, pool);
        placement.ShorelinePoints[0].SetPosition(new Vector3(2, 3, 4));
        Assert.Equal(new Vector2(2, 3), pool.Points[0]);
        Assert.Equal(pool.Points[0], pool.Points[3]);
        placement.ShorelinePoints[3].SetPosition(Vector3.Zero);
        Assert.Equal(Vector2.Zero, pool.Points[0]);
        Assert.Equal(Vector2.Zero, pool.Points[3]);
    }

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
