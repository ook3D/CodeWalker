using System.Reflection;
using System.Runtime.CompilerServices;
using CodeWalker.GameFiles;
using CodeWalker.Project;
using CodeWalker.World;
using SharpDX;
using Xunit;

namespace CodeWalker.WinForms.Tests;

public class AudioZoneWidgetTests
{
    [Theory]
    [InlineData(75, AudioZoneMoveMode.Positioning)]
    [InlineData(140, AudioZoneMoveMode.Activation)]
    public void CoincidentCentersLeaveBothHandlesPickable(float mouseX, AudioZoneMoveMode expected)
    {
        var rel = new RelFile();
        var audio = new AudioPlacement(rel, new Dat151AmbientZone(rel)
        {
            Shape = Dat151ZoneShape.Box,
            PositioningZoneCentre = new Vector3(0, 0, 1080),
            ActivationZoneCentre = new Vector3(0, 0, 1080),
            PositioningZoneSize = new Vector3(5), ActivationZoneSize = new Vector3(20)
        });
        var world = (WorldForm)RuntimeHelpers.GetUninitializedObject(typeof(WorldForm));
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        List<AudioZonePositionWidget> widgets =
        [new(audio, AudioZoneMoveMode.Positioning), new(audio, AudioZoneMoveMode.Activation)];
        typeof(WorldForm).GetField("audioZoneWidgets", flags)!.SetValue(world, widgets);
        typeof(WorldForm).GetField("Widget", flags)!.SetValue(world, new TransformWidget { Mode = WidgetMode.Position });
        typeof(WorldForm).GetField("ShowWidget", flags)!.SetValue(world, true);
        typeof(WorldForm).GetField("camera", flags)!.SetValue(world, new Camera(0, 1, 1)
        {
            MouseRay = new Ray(new Vector3(mouseX, 0, 0), Vector3.UnitZ)
        });
        typeof(WorldForm).GetMethod("UpdateWidgets", flags)!.Invoke(world, null);
        var hovered = Assert.IsType<AudioZonePositionWidget>(typeof(WorldForm).GetField("mousedAudioZoneWidget", flags)!.GetValue(world));
        Assert.Equal(expected, hovered.MoveMode);
        Assert.Single(widgets, w => w.IsUnderMouse);
    }

    [Theory]
    [InlineData(AudioZoneMoveMode.Positioning, 1)]
    [InlineData(AudioZoneMoveMode.Activation, 1)]
    [InlineData(AudioZoneMoveMode.Positioning, 2)]
    [InlineData(AudioZoneMoveMode.Activation, 2)]
    public void EachSelectedZoneGetsIndependentWidgetsWithUndo(AudioZoneMoveMode mode, int count)
    {
        var rel = new RelFile();
        var items = Enumerable.Range(0, count).Select(i => new MapSelection
        {
            Audio = new AudioPlacement(rel, new Dat151AmbientZone(rel)
            {
                Shape = Dat151ZoneShape.Box,
                PositioningZoneCentre = new Vector3(10 + i * 100, 20, 30),
                ActivationZoneCentre = new Vector3(40 + i * 100, 50, 60),
                PositioningZoneSize = new Vector3(5), ActivationZoneSize = new Vector3(20)
            })
        }).ToArray();
        var selection = items[0];
        if (count > 1)
        {
            selection.Clear();
            selection.SetMultipleSelectionItems(items);
        }

        // Exercise the world callbacks without starting a graphics device or game-file loader.
        var world = (WorldForm)RuntimeHelpers.GetUninitializedObject(typeof(WorldForm));
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var widgets = new List<AudioZonePositionWidget>();
        typeof(WorldForm).GetField("audioZoneWidgets", flags)!.SetValue(world, widgets);
        typeof(WorldForm).GetField("Widget", flags)!.SetValue(world, new TransformWidget());
        typeof(WorldForm).GetField("SelectedItem", flags)!.SetValue(world, selection);
        typeof(WorldForm).GetMethod("RebuildAudioZoneWidgets", flags)!.Invoke(world, null);
        Assert.Equal(count * 2, widgets.Count);

        var audio = items[^1].Audio!;
        var widget = Assert.Single(widgets, w => w.Audio == audio && w.MoveMode == mode);
        var start = widget.TargetPosition;
        var inner = audio.InnerPos;
        var outer = audio.OuterPos;
        var offset = new Vector3(2, 3, 4);
        typeof(TransformWidget).GetMethod("PositionWidget_OnPositionChange", flags)!
            .Invoke(widget, [start + offset, start]);
        Assert.Equal(inner + (mode == AudioZoneMoveMode.Positioning ? offset : Vector3.Zero), audio.InnerPos);
        Assert.Equal(outer + (mode == AudioZoneMoveMode.Activation ? offset : Vector3.Zero), audio.OuterPos);
        Assert.True(rel.HasChanged);
        if (count > 1)
        {
            Assert.Same(items, world.CurrentMapSelection.MultipleSelectionItems);
            Assert.Equal(new Vector3(10, 20, 30), items[0].Audio!.InnerPos);
            Assert.Equal(new Vector3(40, 50, 60), items[0].Audio!.OuterPos);
        }

        var undo = new AudioPositionUndoStep(audio, start, mode);
        selection = world.CurrentMapSelection;
        rel.HasChanged = false;
        undo.Undo(world, ref selection);
        Assert.True(rel.HasChanged);
        Assert.Equal(inner, audio.InnerPos);
        Assert.Equal(outer, audio.OuterPos);
        undo.Redo(world, ref selection);
        Assert.Equal(start + offset, widget.TargetPosition);
        if (count > 1) Assert.Same(items, world.CurrentMapSelection.MultipleSelectionItems);
    }
}
