using CodeWalker.Project;
using Xunit;

namespace CodeWalker.WinForms.Tests;

public class ProjectPathTests
{
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
