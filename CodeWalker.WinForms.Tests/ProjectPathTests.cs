using CodeWalker.GameFiles;
using CodeWalker.Project;
using Xunit;

namespace CodeWalker.WinForms.Tests;

public class ProjectPathTests
{
    [Fact]
    public void FolderAssetsResolveWithoutFiveMResourceFolders()
    {
        var cache = new GameFileCache(1024 * 1024, 10, "", false, "", false, "") { IsInited = true };
        var project = new ProjectFile();
        var ydr = project.AddYdrFile(Asset);
        var ydd = project.AddYddFile(Asset.Replace(".ydr", ".ydd"));
        var yft = project.AddYftFile(Asset.Replace(".ydr", ".yft"));
        var ytd = project.AddYtdFile(Asset.Replace(".ydr", ".ytd"));
        foreach (var file in new GameFile?[] { ydr, ydd, yft, ytd })
        {
            Assert.NotNull(file);
            cache.AddProjectFile(file);
        }

        var hash = JenkHash.GenHash("house");
        Assert.Same(ydr, cache.GetYdr(hash));
        Assert.Same(ydd, cache.GetYdd(hash));
        Assert.Same(yft, cache.GetYft(hash));
        Assert.Same(ytd, cache.GetYtd(hash));
        Assert.Empty(cache.ExtraFolders);
    }

    [Fact]
    public void ProjectYmapUsesSameWorldLookupHashAsFiveMResource()
    {
        var project = new ProjectFile();
        var ymap = project.AddYmapFile(@"X:\resources\[maps]\stream\_Props03.ymap");
        Assert.NotNull(ymap);
        var entry = ymap.RpfFileEntry!;
        var resourceHash = JenkHash.GenHash("_props03");
        var visibleYmaps = new Dictionary<MetaHash, YmapFile>
        {
            [resourceHash] = new YmapFile()
        };

        // Match the project overlay's lazy hash initialization in GetVisibleYmaps.
        entry.ShortNameHash = JenkHash.GenHash(entry.GetShortNameLower());
        visibleYmaps[entry.ShortNameHash] = ymap;

        Assert.Equal(resourceHash, entry.ShortNameHash);
        Assert.Same(ymap, Assert.Single(visibleYmaps).Value);
    }

    private const string Asset = @"X:\resources\[housing]\stream\house.ydr";

    [Theory]
    [InlineData("")]
    [InlineData("untitled.cwproj")]
    public void ProjectWithoutAbsoluteLocationPreservesAssetPath(string location)
    {
        var project=new ProjectFile { Filepath=location };
        Assert.Equal(Asset,project.GetRelativePath(Asset));
    }

    [Fact]
    public void UnsavedProjectAcceptsEveryResourceTypeFromReportedFailure()
    {
        var project=new ProjectFile();
        Assert.NotNull(project.AddYdrFile(Asset));
        Assert.NotNull(project.AddYbnFile(Asset.Replace(".ydr",".ybn")));
        Assert.NotNull(project.AddYmapFile(Asset.Replace(".ydr",".ymap")));
        Assert.NotNull(project.AddYtypFile(Asset.Replace(".ydr",".ytyp")));
        Assert.NotNull(project.AddYtdFile(Asset.Replace(".ydr",".ytd")));
        Assert.Equal(Asset,Assert.Single(project.YdrFilenames));
    }

    [Fact]
    public void SavedProjectPathsStillRoundTrip()
    {
        var project=new ProjectFile { Filepath=@"X:\resources\[housing]\houses.cwproj" };
        var relative=project.GetRelativePath(Asset);
        Assert.Equal(@"stream\house.ydr",relative);
        Assert.Equal(Asset,project.GetFullFilePath(relative));
        Assert.Equal(relative,project.GetRelativePath(relative));
    }
}
