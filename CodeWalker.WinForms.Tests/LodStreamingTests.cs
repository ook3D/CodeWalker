using CodeWalker.GameFiles;
using CodeWalker.Rendering;
using CodeWalker.World;
using Xunit;

namespace CodeWalker.WinForms.Tests;

public class LodStreamingTests
{
    [Fact]
    public void RepeatedPartialEvictionsAndReplacementsRestoreWholeChain()
    {
        var manager = new RenderLodManager();
        var camera = new Camera(0, 1, 1);
        var random = new Random(12345);
        var maps = new Dictionary<MetaHash, YmapFile>();
        for (int cycle = 0; cycle < 200; cycle++)
        {
            for (int step = 0; step < 10; step++)
            {
                uint id = (uint)random.Next(1, 5);
                if (random.Next(2) == 0) maps.Remove(id);
                else maps[id] = Map(id, id - 1);
                manager.Update(maps, camera, 0);
            }
            // Restore in reverse order to exercise descendants arriving first.
            for (uint id = 4; id > 0; id--)
            {
                maps.TryAdd(id, Map(id, id - 1));
                manager.Update(maps, camera, 0);
            }
            Assert.Equal(new[] { maps[4].AllEntities[0] }, manager.VisibleLeaves);
            Assert.Single(manager.RootEntities);
            for (uint id = 1; id < 4; id++)
                Assert.Equal(new[] { maps[id + 1].AllEntities[0] }, maps[id].AllEntities[0].LodManagerChildren);
        }
    }

    [Fact]
    public void ReplacingParentWhileGrandparentIsUnavailableReconnectsChild()
    {
        var manager = new RenderLodManager();
        var camera = new Camera(0, 1, 1);
        var root = Map(1, 0);
        var middle = Map(2, 1);
        var leaf = Map(3, 2);
        var maps = new Dictionary<MetaHash, YmapFile> { [1] = root, [2] = middle, [3] = leaf };
        manager.Update(maps, camera, 0);

        maps.Remove(1);
        manager.Update(maps, camera, 0);
        var replacement = Map(2, 1);
        maps[2] = replacement;
        manager.Update(maps, camera, 0);
        maps[1] = root;
        manager.Update(maps, camera, 0);

        Assert.Equal(new[] { leaf.AllEntities[0] }, replacement.AllEntities[0].LodManagerChildren);
        Assert.Equal(new[] { leaf.AllEntities[0] }, manager.VisibleLeaves);
    }

    [Fact]
    public void RemovingLastChildRestoresParentWithZeroDeclaredChildren()
    {
        var manager = new RenderLodManager();
        var camera = new Camera(0, 1, 1);
        var root = Map(1, 0);
        root.AllEntities[0]._CEntityDef.numChildren = 0;
        var child = Map(2, 1);
        var maps = new Dictionary<MetaHash, YmapFile> { [1] = root, [2] = child };
        manager.Update(maps, camera, 0);
        Assert.Equal(new[] { child.AllEntities[0] }, manager.VisibleLeaves);

        maps.Remove(2);
        manager.Update(maps, camera, 0);
        Assert.Equal(new[] { root.AllEntities[0] }, manager.VisibleLeaves);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CachedHierarchySurvivesFullUnloadAndRefresh(bool childrenFirst)
    {
        var manager = new RenderLodManager();
        var camera = new Camera(0, 1, 1);
        var root = Map(1, 0);
        var child = Map(2, 1);
        var grandchild = Map(3, 2);
        var maps = new Dictionary<MetaHash, YmapFile>();
        foreach (var map in childrenFirst ? new[] { grandchild, child, root } : new[] { root, child, grandchild })
            maps.Add(map._CMapData.name, map);

        for (int cycle = 0; cycle < 5; cycle++)
        {
            manager.Update(maps, camera, 0);
            root.LodManagerUpdate = true;
            manager.Update(maps, camera, 0);
            Assert.Equal(new[] { grandchild.AllEntities[0] }, manager.VisibleLeaves);
            Assert.Equal(new[] { child.AllEntities[0] }, root.AllEntities[0].LodManagerChildren);
            Assert.Equal(new[] { grandchild.AllEntities[0] }, child.AllEntities[0].LodManagerChildren);

            manager.Update([], camera, 0);
            Assert.Empty(manager.CurrentYmaps);
            Assert.Empty(manager.RootEntities);
            Assert.Empty(manager.VisibleLeaves);
        }
    }

    [Fact]
    public void ReplacingParentReconnectsRetainedDescendants()
    {
        var manager = new RenderLodManager();
        var camera = new Camera(0, 1, 1);
        var root = Map(1, 0);
        var child = Map(2, 1);
        var grandchild = Map(3, 2);
        var maps = new Dictionary<MetaHash, YmapFile> { [1] = root, [2] = child, [3] = grandchild };
        manager.Update(maps, camera, 0);

        for (int cycle = 0; cycle < 5; cycle++)
        {
            var oldRoot = root;
            root = Map(1, 0);
            maps[1] = root;
            manager.Update(maps, camera, 0);

            Assert.Same(root.AllEntities[0], child.AllEntities[0].Parent);
            Assert.Equal(new[] { child.AllEntities[0] }, root.AllEntities[0].LodManagerChildren);
            Assert.Equal(new[] { grandchild.AllEntities[0] }, child.AllEntities[0].LodManagerChildren);
            Assert.Null(oldRoot.AllEntities[0].LodManagerChildren);
            Assert.Single(manager.RootEntities);
            Assert.Equal(new[] { grandchild.AllEntities[0] }, manager.VisibleLeaves);
        }
    }

    [Fact]
    public void ChildWaitsForEvictedParentAndReconnectsWhenItReturns()
    {
        var manager = new RenderLodManager();
        var camera = new Camera(0, 1, 1);
        var root = Map(1, 0);
        var child = Map(2, 1);
        var maps = new Dictionary<MetaHash, YmapFile> { [1] = root, [2] = child };
        manager.Update(maps, camera, 0);

        for (int cycle = 0; cycle < 5; cycle++)
        {
            maps.Remove(1);
            manager.Update(maps, camera, 0);
            Assert.Empty(manager.CurrentYmaps);
            Assert.Empty(manager.RootEntities);

            maps[1] = root;
            manager.Update(maps, camera, 0);
            Assert.Equal(2, manager.CurrentYmaps.Count);
            Assert.Equal(new[] { child.AllEntities[0] }, root.AllEntities[0].LodManagerChildren);
            Assert.Equal(new[] { child.AllEntities[0] }, manager.VisibleLeaves);
        }
    }

    [Fact]
    public void NextLodIsRequestedEarlyWithoutChangingVisibleGeometry()
    {
        var manager = new RenderLodManager();
        var camera = new Camera(0, 1, 1) { Position = new SharpDX.Vector3(110, 0, 0) };
        var root = Map(1, 0);
        var child = Map(2, 1);
        root.AllEntities[0].ChildLodDist = 100;
        child.AllEntities[0].LodDist = 100;
        var maps = new Dictionary<MetaHash, YmapFile> { [1] = root, [2] = child };
        manager.Update(maps, camera, 0);
        Assert.Equal(new[] { root.AllEntities[0] }, manager.VisibleLeaves);
        Assert.Contains(child.AllEntities[0], manager.PrefetchEntities);

        camera.Position = new SharpDX.Vector3(90, 0, 0);
        manager.Update(maps, camera, 0);
        Assert.Equal(new[] { child.AllEntities[0] }, manager.VisibleLeaves);
        Assert.Contains(root.AllEntities[0], manager.PrefetchEntities);

        camera.Position = new SharpDX.Vector3(200, 0, 0);
        manager.Update(maps, camera, 0);
        Assert.Empty(manager.PrefetchEntities);
        manager.Update([], camera, 0);
        Assert.Empty(manager.PrefetchEntities);
    }

    private static YmapFile Map(uint name, uint parent)
    {
        var map = new YmapFile { _CMapData = new CMapData { name = name, parent = parent } };
        var entity = new YmapEntityDef
        {
            Ymap = map,
            _CEntityDef = new CEntityDef { parentIndex = parent == 0 ? -1 : 0, numChildren = 1 },
            LodDist = 1000,
            ChildLodDist = 1000,
        };
        map.AllEntities = [entity];
        map.RootEntities = [entity];
        return map;
    }
}
