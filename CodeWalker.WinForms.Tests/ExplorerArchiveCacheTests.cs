using System.Reflection;
using System.Text;
using CodeWalker.GameFiles;
using CodeWalker.World;
using Xunit;

namespace CodeWalker.WinForms.Tests;

public class ExplorerArchiveCacheTests
{
    [Fact]
    public void ScannedArchivesSupplyTimecycleDataToModelViewerCache()
    {
        StaThread.Run(() =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "cw-explorer-archives-" + Guid.NewGuid().ToString("N"));
            var gameDirectory = Path.Combine(directory, "game");
            var extraDirectory = Path.Combine(directory, "extra");
            Directory.CreateDirectory(gameDirectory);
            Directory.CreateDirectory(extraDirectory);
            try
            {
                var common = RpfFile.CreateNew(gameDirectory, "common.rpf");
                var data = RpfFile.CreateDirectory(common.Root!, "data");
                var levels = RpfFile.CreateDirectory(data, "levels");
                var gta5 = RpfFile.CreateDirectory(levels, "gta5");
                RpfFile.CreateFile(gta5, "time.xml", Encoding.UTF8.GetBytes(
                    "<time><suninfo sun_roll=\"15\" sun_yaw=\"30\"/><sample name=\"day\" hour=\"0\" duration=\"1\"/></time>"));
                RpfFile.CreateNew(extraDirectory, "addon.rpf");

                using var explorer = new ExploreForm();
                var scan = typeof(ExploreForm).GetMethod("RefreshMainTreeViewRoot", BindingFlags.NonPublic | BindingFlags.Instance)!;
                scan.Invoke(explorer, [new MainTreeFolder { Name = "Game", FullPath = gameDirectory + "\\" }, false]);
                scan.Invoke(explorer, [new MainTreeFolder { Name = "Extra", FullPath = extraDirectory + "\\", IsExtraFolder = true }, true]);
                var archives = (List<RpfFile>)typeof(ExploreForm).GetProperty("AllRpfs", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(explorer)!;
                Assert.Equal(2, archives.Count);

                var manager = new RpfManager();
                manager.Init(archives, false); // same pre-scanned archive initialization used by Explorer
                var cache = new GameFileCache(1024 * 1024, 10, "", false, "", false, "") { RpfMan = manager, IsInited = true };
                var timecycle = new Timecycle();
                timecycle.Init(cache, _ => { });
                Assert.True(timecycle.Inited);
                Assert.Single(timecycle.Samples);
                Assert.Equal(15f, timecycle.sun_roll);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        });
    }
}
