using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class PreloadedFiveMResourceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FiveMResourceScanSkipsExcludedFolderTrees(bool preloaded)
    {
        var directory = Path.Combine(Path.GetTempPath(), "cw-fivem-exclusions-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            foreach (var parent in new[] { directory, Path.Combine(directory, "[maps]") })
            {
                foreach (var folder in new[] { ".git", ".github", ".githooks", ".claude", "[clothing]", "[peds]" })
                {
                    var stream = Path.Combine(parent, parent == directory ? folder : folder.ToUpperInvariant(), "resource", "stream");
                    Directory.CreateDirectory(stream);
                    File.WriteAllText(Path.Combine(stream, "excluded.ymap"), "excluded");
                }
            }
            var allowed = Path.Combine(directory, "[maps]", "[peds]-map", "stream");
            Directory.CreateDirectory(allowed);
            File.WriteAllText(Path.Combine(allowed, "included.ymap"), "included");
            File.WriteAllText(Path.Combine(directory, "root.ymap"), "included");

            var manager = new RpfManager { ExtraFolders = [directory] };
            var errors = new List<string>();
            if (preloaded) manager.Init([], false, _ => { }, errors.Add);
            else manager.Init(directory, false, _ => { }, errors.Add);

            Assert.Empty(errors);
            Assert.Equal(2, manager.ExtraRpfs.Count);
            Assert.Contains("fivem\\root.ymap", manager.EntryDict.Keys);
            Assert.Contains("fivem\\[maps]\\[peds]-map\\stream\\included.ymap", manager.EntryDict.Keys);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    public async Task FiveMResourcesCanBeToggledIndependentlyOfMods(bool initiallyEnabled, bool preloaded, bool dlc)
    {
        var directory = Path.Combine(Path.GetTempPath(), "cw-fivem-toggle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var dictionary = new TextureDictionary();
            dictionary.BuildFromTextureList([]);
            await File.WriteAllBytesAsync(Path.Combine(directory, "test_model.ytd"),
                new YtdFile { TextureDict = dictionary }.Save());
            await File.WriteAllTextAsync(Path.Combine(directory, "housing_gtxd.meta"),
                "<CMapParentTxds><txdRelationships><Item><parent>GlobalRoads</parent><child>test_model</child></Item></txdRelationships></CMapParentTxds>");
            var cache = new GameFileCache(1024 * 1024, 10, directory, false, "", false, "")
            {
                ExtraFolders = directory,
                EnableFiveMResources = initiallyEnabled,
                EnableDlc = dlc,
                LoadAudio = false,
                LoadPeds = false,
                LoadVehicles = false,
            };
            var map = new YmapFile();
            map._CMapData.name = JenkHash.GenHashLowerInvariant("test_map");
            await File.WriteAllBytesAsync(Path.Combine(directory, "test_map.ymap"), map.Save());
            if (preloaded) await cache.InitAsync(null, null, []);
            else cache.Init(_ => { }, _ => { });
            var hash = JenkHash.GenHashLowerInvariant("test_model");
            void AssertResources(bool enabled)
            {
                Assert.Equal(enabled, cache.EnableFiveMResources);
                Assert.Equal(enabled, cache.GetYtdEntry(hash) != null);
                if (!preloaded)
                    Assert.Equal(enabled, cache.YmapDict.ContainsKey(JenkHash.GenHashLowerInvariant("test_map")));
                Assert.Equal(enabled ? JenkHash.GenHashLowerInvariant("GlobalRoads") : 0u,
                    cache.TryGetParentYtdHash(hash));
            }
            AssertResources(initiallyEnabled);
            foreach (bool mods in new[] { true, false })
            {
                bool resourcesBefore = cache.EnableFiveMResources;
                cache.SetModsEnabled(mods);
                AssertResources(resourcesBefore);
                foreach (bool resources in new[] { false, true, false })
                {
                    cache.SetFiveMResourcesEnabled(resources);
                    Assert.Equal(mods, cache.EnableMods);
                    AssertResources(resources);
                }
            }
            Assert.False(cache.SetFiveMResourcesEnabled(false));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task PreloadedCacheIncludesGtxdParentingFromFiveMFolders()
    {
        var directory = Path.Combine(Path.GetTempPath(), "cw-fivem-gtxd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "housing_gtxd.meta"),
                "<CMapParentTxds><txdRelationships><Item><parent>GlobalRoads</parent><child>hns_mrp_culdesac_txd</child></Item></txdRelationships></CMapParentTxds>");

            var cache = new GameFileCache(1024 * 1024, 10, "", false, "", false, "")
            {
                ExtraFolders = directory,
                LoadAudio = false,
                LoadPeds = false,
                LoadVehicles = false,
            };

            await cache.InitAsync(null, null, []);

            Assert.Equal(
                JenkHash.GenHashLowerInvariant("GlobalRoads"),
                cache.TryGetParentYtdHash(JenkHash.GenHashLowerInvariant("hns_mrp_culdesac_txd")));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task PreloadedCacheIncludesExternalTextureDictionariesFromFiveMFolders()
    {
        var directory = Path.Combine(Path.GetTempPath(), "cw-fivem-textures-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "stream"));
        try
        {
            var dictionary = new TextureDictionary();
            dictionary.BuildFromTextureList([]);
            var path = Path.Combine(directory, "stream", "test_model.ytd");
            File.WriteAllBytes(path, new YtdFile { TextureDict = dictionary }.Save());

            var cache = new GameFileCache(1024 * 1024, 10, "", false, "", false, "")
            {
                ExtraFolders = directory,
                LoadAudio = false,
                LoadPeds = false,
                LoadVehicles = false,
            };

            await cache.InitAsync(null, null, []);

            var hash = JenkHash.GenHashLowerInvariant("test_model");
            var entry = Assert.IsType<RpfResourceFileEntry>(cache.GetYtdEntry(hash));
            Assert.IsType<LooseRpfFile>(entry.File);
            Assert.Equal(path, entry.File!.FilePath);

            var ytd = Assert.IsType<YtdFile>(cache.GetYtd(hash));
            cache.ContentThreadProc();
            Assert.True(ytd.Loaded);
            Assert.NotNull(ytd.TextureDict);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
