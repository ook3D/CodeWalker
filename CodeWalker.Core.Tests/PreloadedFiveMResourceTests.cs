using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class PreloadedFiveMResourceTests
{
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
