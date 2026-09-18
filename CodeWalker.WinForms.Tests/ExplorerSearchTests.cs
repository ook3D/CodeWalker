using CodeWalker.Properties;
using System.Reflection;
using Xunit;

namespace CodeWalker.WinForms.Tests;

public class ExplorerSearchTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExplorerScanAndSearchRespectFiveMFolderExclusions(bool fiveM)
    {
        StaThread.Run(() =>
        {
            using var form = new ExploreForm();
            _ = form.Handle;
            _ = ((System.Windows.Forms.TreeView)GetField(form, "MainTreeView")!).Handle;
            var directory = Path.Combine(Path.GetTempPath(), "cw-explorer-exclusions-" + Guid.NewGuid().ToString("N"));
            var previous = Settings.Default.FiveMResourceFolders;
            Directory.CreateDirectory(directory);
            try
            {
                Settings.Default.FiveMResourceFolders = fiveM ? directory : "";
                foreach (var parent in new[] { directory, Path.Combine(directory, "resource") })
                {
                    foreach (var excluded in new[] { ".git", ".github", ".githooks", ".claude", "[clothing]", "[peds]" })
                    {
                        var folder = Path.Combine(parent, parent == directory ? excluded : excluded.ToUpperInvariant(), "stream");
                        Directory.CreateDirectory(folder);
                        File.WriteAllText(Path.Combine(folder, "excluded.ymap"), "test");
                    }
                }
                var allowed = Path.Combine(directory, "resource", "[peds]-map");
                Directory.CreateDirectory(allowed);
                var included = Path.Combine(allowed, "included.ymap");
                File.WriteAllText(included, "test");
                var root = new MainTreeFolder { Name = "Resources", FullPath = directory + "\\", Path = directory + "\\", IsExtraFolder = true };
                SetField(form, "ExtraRootFolders", new List<MainTreeFolder> { root });
                typeof(ExploreForm).GetMethod("RefreshMainTreeViewRoot", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(form, [root, true]);
                SetField(form, "Ready", true);

                form.Search("*.ymap");

                var results = (MainTreeFolder)GetField(form, "SearchResults")!;
                Assert.Equal(fiveM ? 1 : 13, results.ListItems!.Count);
                Assert.Contains(results.ListItems, item => item.FullPath == included);
                Assert.Equal(fiveM ? 1 : 7, root.Children!.Count);
                var resource = Assert.Single(root.Children, child => child.Name == "resource");
                Assert.Equal(fiveM ? 1 : 7, resource.Children!.Count);
            }
            finally
            {
                Settings.Default.FiveMResourceFolders = previous;
                Directory.Delete(directory, true);
            }
        });
    }

    [Theory]
    [InlineData("TARGET.YMAP")]
    [InlineData("tar*.ymap")]
    public void GlobalSearchIncludesGameAndNestedExtraResourceFiles(string query)
    {
        StaThread.Run(() =>
        {
            using var form = new ExploreForm();
            _ = form.Handle;
            _ = ((System.Windows.Forms.TreeView)GetField(form, "MainTreeView")!).Handle;
            var directory = Path.Combine(Path.GetTempPath(), "cw-search-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var gameFile = Path.Combine(directory, "game_target.ymap");
                var resourceFile = Path.Combine(directory, "resource_target.ymap");
                File.WriteAllText(gameFile, "test");
                File.WriteAllText(resourceFile, "test");
                var game = new MainTreeFolder { Name = "Game", FullPath = directory, Files = [gameFile] };
                var extra = new MainTreeFolder { Name = "FiveM", FullPath = directory, IsExtraFolder = true };
                var stream = new MainTreeFolder { Name = "stream", FullPath = directory, Files = [resourceFile] };
                extra.AddChild(stream);
                SetField(form, "RootFolder", game);
                SetField(form, "ExtraRootFolders", new List<MainTreeFolder> { extra });
                SetField(form, "Ready", true);

                form.Search(query);

                var results = (MainTreeFolder)GetField(form, "SearchResults")!;
                Assert.Equal(2, results.ListItems!.Count);
                Assert.Contains(results.ListItems, item => item.FullPath == resourceFile);
                Assert.Contains(results.ListItems, item => item.FullPath == gameFile);
                Assert.False(form.Searching);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        });
    }

    private static object? GetField(ExploreForm form, string name) =>
        typeof(ExploreForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form);

    private static void SetField(ExploreForm form, string name, object value) =>
        typeof(ExploreForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(form, value);
}
