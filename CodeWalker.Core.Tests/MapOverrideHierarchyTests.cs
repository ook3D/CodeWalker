using System.Reflection;
using CodeWalker.GameFiles;
using CodeWalker.World;
using SharpDX;
using Xunit;

namespace CodeWalker.Core.Tests;

public class MapOverrideHierarchyTests
{
    [Fact]
    public void SelectedExtraMapReplacesCachedBoundsAndParent()
    {
        var cache = new GameFileCache(1024 * 1024, 10, "", false, "", false, "") { IsInited = true };
        var manager = new RpfManager();
        typeof(GameFileCache).GetField("RpfMan")!.SetValue(cache, manager);
        var stale = new MapDataStoreNode { Name = 1, ParentName = 2 };
        var oldParent = new MapDataStoreNode { Name = 2, Children = [stale] };
        var newParent = new MapDataStoreNode { Name = 3 };
        cache.AllCacheFiles.Add(new CacheDatFile { AllMapNodes = [stale, oldParent, newParent] });
        cache.YmapDict[2] = new RpfResourceFileEntry();
        cache.YmapDict[3] = new RpfResourceFileEntry();

        var winner = new MemoryMap(3, 100);
        var loser = new MemoryMap(2, 200);
        manager.ExtraRpfs.AddRange([winner, loser]);
        cache.ActiveMapRpfFiles["winner"] = winner;
        cache.ActiveMapRpfFiles["loser"] = loser; // Enumeration order must not override the selected file.
        cache.YmapDict[1] = winner.Entry;
        var space = new Space();
        typeof(Space).GetField("gameFileCache", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(space, cache);

        Invoke(space, "InitCacheData");
        Invoke(space, "InitMapDataStore");

        var actual = Assert.IsType<MapDataStoreNode>(cache.GetMapNode(1));
        Assert.Equal(3u, actual.ParentName.Hash);
        Assert.Equal(new Vector3(100), actual.streamingExtentsMin);
        Assert.Empty(oldParent.Children);
        Assert.Same(actual, Assert.Single(newParent.Children));
        var position = new Vector3(105);
        Assert.Contains(actual, space.MapDataStore.GetItems(ref position));
        Assert.Equal(1, winner.Reads);
        Assert.Equal(0, loser.Reads);
    }

    private static void Invoke(Space space, string name) =>
        typeof(Space).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(space, null);

    private sealed class MemoryMap : RpfFile
    {
        private readonly byte[] data;
        public RpfResourceFileEntry Entry { get; }
        public int Reads { get; private set; }

        public MemoryMap(uint parent, float min) : base("map", "map", 0)
        {
            var map = new YmapFile();
            map._CMapData.name = 1;
            map._CMapData.parent = parent;
            map._CMapData.streamingExtentsMin = new Vector3(min);
            map._CMapData.streamingExtentsMax = new Vector3(min + 10);
            var bytes = map.Save();
            Entry = CreateResourceFileEntry(ref bytes, 2);
            data = ResourceBuilder.Decompress(bytes);
            Entry.Name = Entry.NameLower = "test.ymap";
            Entry.ShortNameHash = 1;
            Entry.File = this;
            AllEntries = [Entry];
        }

        public override byte[] ExtractFile(RpfFileEntry entry)
        {
            Reads++;
            return data;
        }
    }
}
