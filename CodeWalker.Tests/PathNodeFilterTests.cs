using CodeWalker.Rendering;
using SharpDX;
using Xunit;

namespace CodeWalker.Tests;

public class PathNodeFilterTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(17)]
    [InlineData(10000)]
    public void FilteringMatchesPreviousPredicateAndKeepsOrder(int count)
    {
        var nodes = Enumerable.Range(0, count).Select(i => new Vector4(i % 11, i % 7, i % 3, i)).ToArray();
        var original = nodes.ToArray();
        foreach (var position in new[] { Vector3.Zero, new Vector3(100000), new Vector3(1, 2, 0) })
        {
            var expected = nodes.Where(node => (new Vector3(node.X, node.Y, node.Z) - position).LengthSquared() > 0.01f).ToArray();
            var actual = PathNodeFilter.Exclude(nodes, position);
            Assert.Equal(expected, actual);
            Assert.Equal(original, nodes);
            if (expected.Length == nodes.Length) Assert.Same(nodes, actual);
        }
    }

    [Fact]
    public void BoundaryAndNonfiniteValuesKeepPreviousRules()
    {
        Vector4[] nodes = [new(0.099f, 0, 0, 1), new(0.1f, 0, 0, 2), new(0.101f, 0, 0, 3),
            new(float.NaN, 0, 0, 4), new(float.PositiveInfinity, 0, 0, 5)];
        var expected = nodes.Where(n => new Vector3(n.X, n.Y, n.Z).LengthSquared() > 0.01f).ToArray();
        Assert.Equal(expected, PathNodeFilter.Exclude(nodes, Vector3.Zero));
        Assert.Empty(PathNodeFilter.Exclude(nodes, new Vector3(float.NaN)));
        Assert.Empty(PathNodeFilter.Exclude([Vector4.Zero, Vector4.Zero], Vector3.Zero));
    }
}
