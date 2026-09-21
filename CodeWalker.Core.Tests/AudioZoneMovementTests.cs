using CodeWalker.GameFiles;
using CodeWalker.World;
using SharpDX;
using Xunit;

namespace CodeWalker.Core.Tests;

public class AudioZoneMovementTests
{
    [Theory]
    [InlineData(AudioZoneMoveMode.Both)]
    [InlineData(AudioZoneMoveMode.Positioning)]
    [InlineData(AudioZoneMoveMode.Activation)]
    public void MoveModeChangesOnlyTheRequestedCentresAndRoundTrips(AudioZoneMoveMode mode)
    {
        var rel = new RelFile { RelType = RelDatFileType.Dat151 };
        var zone = new Dat151AmbientZone(rel)
        {
            Shape = Dat151ZoneShape.Box,
            PositioningZoneCentre = new(10, 20, 30), ActivationZoneCentre = new(100, 200, 300),
            PositioningZoneSize = new(5, 6, 7), ActivationZoneSize = new(50, 60, 70)
        };
        rel.RelDatas = rel.RelDatasSorted = [zone];
        var placement = new AudioPlacement(rel, zone) { ZoneMoveMode = mode };
        var start = placement.MoveWidgetPosition;
        var offset = new Vector3(2, -3, 4);
        placement.SetPosition(start + offset);
        Assert.Equal(new Vector3(10, 20, 30) + (mode == AudioZoneMoveMode.Activation ? Vector3.Zero : offset), zone.PositioningZoneCentre);
        Assert.Equal(new Vector3(100, 200, 300) + (mode == AudioZoneMoveMode.Positioning ? Vector3.Zero : offset), zone.ActivationZoneCentre);
        Assert.Equal(start + offset, placement.MoveWidgetPosition);
        var saved = new RelFile();
        saved.Load(rel.Save(), null);
        var restored = Assert.IsType<Dat151AmbientZone>(Assert.Single(saved.RelDatas));
        Assert.Equal(zone.PositioningZoneCentre, restored.PositioningZoneCentre);
        Assert.Equal(zone.ActivationZoneCentre, restored.ActivationZoneCentre);
        // Undo must use the recorded mode even if the editor has since changed modes.
        placement.ZoneMoveMode = mode == AudioZoneMoveMode.Activation ? AudioZoneMoveMode.Both : AudioZoneMoveMode.Activation;
        placement.SetPosition(start, mode);
        Assert.Equal(new Vector3(10, 20, 30), zone.PositioningZoneCentre);
        Assert.Equal(new Vector3(100, 200, 300), zone.ActivationZoneCentre);
        Assert.Equal(new Vector3(5, 6, 7), zone.PositioningZoneSize);
        Assert.Equal(new Vector3(50, 60, 70), zone.ActivationZoneSize);
    }

    [Fact]
    public void MovingLinePositioningZoneTranslatesBothEndpoints()
    {
        var rel = new RelFile();
        var zone = new Dat151AmbientZone(rel)
        {
            Shape = Dat151ZoneShape.Line,
            PositioningZoneCentre = new(1, 2, 3), PositioningZoneSize = new(10, 20, 30),
            ActivationZoneCentre = new(100, 200, 300)
        };
        var placement = new AudioPlacement(rel, zone) { ZoneMoveMode = AudioZoneMoveMode.Positioning };
        placement.SetPosition(new(3, 4, 5));
        Assert.Equal(new Vector3(12, 22, 32), zone.PositioningZoneSize);
        Assert.Equal(new Vector3(100, 200, 300), zone.ActivationZoneCentre);
    }
}
