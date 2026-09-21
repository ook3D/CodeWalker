using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using CodeWalker.GameFiles;
using CodeWalker.Project;
using CodeWalker.Project.Panels;
using CodeWalker.World;
using Xunit;

namespace CodeWalker.WinForms.Tests;

public class AudioSelectionTests
{
    [Fact]
    public void ProjectZoneRefreshPreservesWorldMultiSelection()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                // Avoid starting DirectX or loading GTA files for this panel callback test.
                var world = (WorldForm)RuntimeHelpers.GetUninitializedObject(typeof(WorldForm));
                var project = (ProjectForm)RuntimeHelpers.GetUninitializedObject(typeof(ProjectForm));
                typeof(ProjectForm).GetField("<WorldForm>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(project, world);
                typeof(WorldForm).GetField("ProjectForm", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(world, project);
                typeof(ProjectForm).GetProperty("WorldSelectionChangeInProcess", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(project, true);

                var rel = new RelFile();
                var first = new AudioPlacement(rel, new Dat151AmbientZone(rel) { Shape = Dat151ZoneShape.Box });
                var second = new AudioPlacement(rel, new Dat151AmbientZone(rel) { Shape = Dat151ZoneShape.Box });
                project.CurrentProjectFile = new ProjectFile();
                project.CurrentProjectFile.AudioRelFiles.Add(rel);
                MapSelection[] items = [new() { Audio = first }, new() { Audio = second }];
                var selection = new MapSelection();
                selection.SetMultipleSelectionItems(items);
                typeof(WorldForm).GetField("SelectedItem", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(world, selection);

                using var panel = new EditAudioAmbientZonePanel(project);
                panel.SetZone(second);

                Assert.Same(items, world.CurrentMapSelection.MultipleSelectionItems);
                Assert.Same(first, world.CurrentMapSelection.MultipleSelectionItems![0].Audio);
                Assert.Same(second, world.CurrentMapSelection.MultipleSelectionItems[1].Audio);
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
