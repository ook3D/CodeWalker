using CodeWalker.GameFiles;
using CodeWalker.World;
using SharpDX;
using Xunit;

namespace CodeWalker.Core.Tests;

public class SpaceMapDataStoreTests
{
    [Theory]
    [InlineData(float.MaxValue, float.MinValue)] //uncalculated extents
    [InlineData(float.NaN, float.NaN)]
    public void InitDoesNotRecurseForeverOnDegenerateExtents(float min, float max)
    {
        var nodes = new List<MapDataStoreNode>();
        for (int i = 0; i < 50; i++)
        {
            nodes.Add(new MapDataStoreNode { streamingExtentsMin = new Vector3(min), streamingExtentsMax = new Vector3(max) });
        }
        nodes.Add(new MapDataStoreNode { streamingExtentsMin = new Vector3(-10), streamingExtentsMax = new Vector3(10) });

        var store = new SpaceMapDataStore();
        store.Init(nodes);

        var p = Vector3.Zero;
        Assert.Contains(nodes[^1], store.GetItems(ref p));
    }
}
