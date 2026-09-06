using System.ComponentModel;
using ST.Library.UI.NodeEditor;
using Xunit;

namespace CodeWalker.WinForms.Tests;

public class ControlLifecycleTests
{
    [Fact]
    public void ChangingSelectionRestoresPreviousObjectsMetadata() => StaThread.Run(() =>
    {
        var first = new object();
        var second = new object();
        using var grid = new ReadOnlyPropertyGrid();

        grid.SelectedObject = first;
        Assert.True(IsReadOnly(first));
        grid.SelectedObject = second;
        Assert.False(IsReadOnly(first));
        Assert.True(IsReadOnly(second));
        grid.SelectedObject = null;
        Assert.False(IsReadOnly(second));
    });

    [Fact]
    public void RepeatedReadOnlyAssignmentDoesNotLeaveProvidersBehind() => StaThread.Run(() =>
    {
        var item = new object();
        using var grid = new ReadOnlyPropertyGrid { SelectedObject = item };

        grid.ReadOnly = true;
        grid.ReadOnly = true;
        grid.ReadOnly = false;

        Assert.False(IsReadOnly(item));
    });

    [Fact]
    public void DisposingGridRestoresSelectedObjectsMetadata() => StaThread.Run(() =>
    {
        var item = new object();
        var grid = new ReadOnlyPropertyGrid { SelectedObject = item };
        Assert.True(IsReadOnly(item));

        grid.Dispose();

        Assert.False(IsReadOnly(item));
    });

    [Fact]
    public void UnparentedToolStripTextBoxCanBeMeasured() => StaThread.Run(() =>
    {
        using var textBox = new ToolStripSpringTextBox();
        var size = textBox.GetPreferredSize(new System.Drawing.Size(100, 30));
        Assert.True(size.Width > 0);
        Assert.True(size.Height > 0);
    });

    [Fact]
    public void EmptyNodeOptionHasNoConnections()
    {
        Assert.Equal(0, STNodeOption.Empty.ConnectionCount);
        Assert.Null(STNodeOption.Empty.GetConnectedOption());
        Assert.Equal(ConnectionStatus.EmptyOption,
            STNodeOption.Empty.CanConnect(new STNodeOption("Data", typeof(string), false)));
    }

    [Fact]
    public void PropertyGridConstructsEachDescriptorOnce() => StaThread.Run(() =>
    {
        CountingDescriptor.Constructions = 0;
        using var grid = new STNodePropertyGrid();

        grid.SetNode(new PropertyNode());

        Assert.Equal(1, CountingDescriptor.Constructions);
    });

    public sealed class CountingDescriptor : STNodePropertyDescriptor
    {
        public static int Constructions;
        public CountingDescriptor() => Constructions++;
    }

    private sealed class PropertyNode : STNode
    {
        [STNodeProperty("Value", "Test value", DescriptorType = typeof(CountingDescriptor))]
        public int Value { get; set; }
    }

    private static bool IsReadOnly(object value) =>
        TypeDescriptor.GetAttributes(value)[typeof(ReadOnlyAttribute)] is ReadOnlyAttribute { IsReadOnly: true };
}
