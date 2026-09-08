using System.Reflection;
using Xunit;

namespace CodeWalker.WinForms.Tests;

public class ExplorerSearchTests
{
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
