using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class PreloadedFiveMResourceTests
{
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
