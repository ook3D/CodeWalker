using CodeWalker.GameFiles;
using CodeWalker.World;
using Xunit;

namespace CodeWalker.WinForms.Tests;

public class CutsceneSubtitleTests
{
    private static Cutscene Create() => new(new CutFile { CutsceneFile2 = new CutsceneFile2() }, null!, null!, null!)
    {
        Duration = 20,
        Gxt2File = new Gxt2File { TextEntries = [new Gxt2Entry { Hash = 123456789, Text = "~z~Hello~n~world" }, new Gxt2Entry { Hash = 987654321, Text = "Second" }] }
    };

    private static CutEvent Show(float time, float duration, uint hash = 123456789) => new()
    {
        fTime = time, iEventId = CutEventType.Subtitle,
        EventArgs = new CutSubtitleEventArgs { cName = hash, fSubtitleDuration = duration }
    };

    [Fact]
    public void SubtitleUsesLocalizedTextAndPlaybackTime()
    {
        var scene = Create();
        scene.PlayEvents = [Show(2, 4)];
        scene.Update(1);
        Assert.Null(scene.CurrentSubtitle);
        scene.Update(2);
        Assert.Equal("Hello\nworld", scene.CurrentSubtitle);
        // An unchanged playback time represents a paused viewer.
        scene.Update(2);
        Assert.Equal("Hello\nworld", scene.CurrentSubtitle);
        scene.Update(6);
        Assert.Null(scene.CurrentSubtitle);
        scene.Update(3);
        Assert.Equal("Hello\nworld", scene.CurrentSubtitle);
        scene.Update(1);
        Assert.Null(scene.CurrentSubtitle);
    }

    [Fact]
    public void LargeSeekDoesNotStartExpiredSubtitlesAgain()
    {
        var scene = Create();
        scene.PlayEvents = [Show(2, 1), Show(6, 2, 987654321)];
        scene.Update(5);
        Assert.Null(scene.CurrentSubtitle);
        scene.Update(7);
        Assert.Equal("Second", scene.CurrentSubtitle);
        scene.Update(9);
        Assert.Null(scene.CurrentSubtitle);
        scene.Update(21);
        Assert.Null(scene.CurrentSubtitle);
    }

    [Fact]
    public void HideEventAndTogglePreserveSeekableTimelineState()
    {
        var scene = Create();
        scene.PlayEvents = [Show(1, 10), new CutEvent { fTime = 5, iEventId = CutEventType.HideSubtitle }];
        scene.EnableSubtitles = false;
        scene.Update(2);
        Assert.Null(scene.CurrentSubtitle);
        scene.EnableSubtitles = true;
        Assert.Equal("Hello\nworld", scene.CurrentSubtitle);
        scene.Update(5);
        Assert.Null(scene.CurrentSubtitle);
        scene.Update(3);
        Assert.Equal("Hello\nworld", scene.CurrentSubtitle);
        Assert.Empty(scene.UnsupportedEventTypes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void InvalidOrZeroDurationsDoNotDisplayIndefinitely(float duration)
    {
        var scene = Create();
        scene.PlayEvents = [Show(1, duration)];
        scene.Update(2);
        Assert.Null(scene.CurrentSubtitle);
    }

    [Fact]
    public void MissingLabelDoesNotReplaceCurrentTextWithAHash()
    {
        var scene = Create();
        // Pick a label absent from both the scene dictionary and the global index.
        uint absent = uint.MaxValue;
        while (!string.IsNullOrEmpty(GlobalText.TryGetString(absent))) absent--;
        scene.PlayEvents = [Show(1, 10), Show(2, 5, absent)];
        scene.Update(3);
        Assert.Equal("Hello\nworld", scene.CurrentSubtitle);
    }
}
