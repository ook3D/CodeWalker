using CodeWalker.World;
using SharpDX;
using Xunit;

namespace CodeWalker.Core.Tests;

public class OceanTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(800)]
    public void MainlandAndIslandLeaveNoUnfilledCornersOrOverlappingOcean(float islandOffset)
    {
        var camera = new Camera(0, 1, 1) { ZFar = 10000 };
        BoundingBox[] regions =
        [
            new(new Vector3(-500, -500, 0), new Vector3(200, 500, 0)),
            new(new Vector3(100 + islandOffset, -800, 0), new Vector3(600 + islandOffset, -300, 0))
        ];
        var patches = new List<BoundingBox>();
        Ocean.GetPatches(camera, regions, patches);
        for (float x = -699.5f; x < 1600; x += 25)
        for (float y = -999.5f; y < 700; y += 25)
        {
            bool Contains(BoundingBox b) => x > b.Minimum.X && x < b.Maximum.X &&
                y > b.Minimum.Y && y < b.Maximum.Y;
            Assert.Equal(regions.Any(Contains) ? 0 : 1, patches.Count(Contains));
        }
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(1000000, 0, false)]
    [InlineData(-1000000, 1000000, false)]
    [InlineData(0, -1000000, true)]
    [InlineData(0, 0, true)]
    public void OceanCoversViewOutsideAuthoredWaterWithoutOverlap(float x, float y, bool orthographic)
    {
        var camera = new Camera(0, 1, 1)
        {
            Position = new Vector3(x, y, 100), ZFar = 1000,
            IsOrthographic = orthographic, OrthographicSize = 20000
        };
        var bounds = new BoundingBox(new Vector3(-500, -700, 0), new Vector3(600, 800, 0));
        var patches = new List<BoundingBox>();
        Ocean.GetPatches(camera, [bounds], patches);
        float Overlap(BoundingBox a, BoundingBox b) =>
            MathF.Max(0, MathF.Min(a.Maximum.X, b.Maximum.X) - MathF.Max(a.Minimum.X, b.Minimum.X)) *
            MathF.Max(0, MathF.Min(a.Maximum.Y, b.Maximum.Y) - MathF.Max(a.Minimum.Y, b.Minimum.Y));
        for (int i = 0; i < patches.Count; i++)
        {
            Assert.True(patches[i].Maximum.X >= patches[i].Minimum.X);
            Assert.True(patches[i].Maximum.Y >= patches[i].Minimum.Y);
            Assert.Equal(0, Overlap(patches[i], bounds));
            for (int j = 0; j < i; j++) Assert.Equal(0, Overlap(patches[i], patches[j]));
        }
        float radius = orthographic ? camera.OrthographicSize : camera.ZFar;
        foreach (float dx in new[] { -radius, 0, radius })
        foreach (float dy in new[] { -radius, 0, radius })
        {
            var point = new Vector3(x + dx, y + dy, 0);
            bool Contains(BoundingBox b) => point.X >= b.Minimum.X && point.X <= b.Maximum.X &&
                point.Y >= b.Minimum.Y && point.Y <= b.Maximum.Y;
            Assert.True(Contains(bounds) || patches.Any(Contains));
        }
    }
}
