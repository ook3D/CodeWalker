using System.Drawing;
using ST.Library.UI.NodeEditor;
using Xunit;

namespace CodeWalker.WinForms.Tests;

public class NodeEditorLifecycleTests
{
    [Fact]
    public void UndoAndRedoAreSafeBeforeHandleCreation() => StaThread.Run(() =>
    {
        using var box = new TextBoxFix { Text = "Initial" };
        Assert.False(box.IsHandleCreated);
        box.Undo();
        box.Redo();
        Assert.Equal("Initial", box.Text);
    });

    [Fact]
    public void UndoAndRedoPreserveHistoryAfterControlCreation() => StaThread.Run(() =>
    {
        using var box = new TextBoxFix { Text = "Initial" };
        box.CreateControl();
        box.Text = "Changed";
        box.Undo();
        Assert.Equal("Initial", box.Text);
        box.Redo();
        Assert.Equal("Changed", box.Text);
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WheelEventsUseHoveredControlWithoutAnActiveControl(bool enabled) => StaThread.Run(() =>
    {
        var control = new STNodeControl { Enabled = enabled, Visable = true };
        var node = new InputNode();
        int vertical = 0, horizontal = 0;
        control.MouseWheel += (_, _) => vertical++;
        control.MouseHWheel += (_, _) => horizontal++;

        node.Hover(control);
        node.Wheel();

        Assert.Equal(enabled ? 1 : 0, vertical);
        Assert.Equal(enabled ? 1 : 0, horizontal);
    });

    [Fact]
    public void ClickingAnOptionStartsAConnection() => StaThread.Run(() =>
    {
        using var editor = new InputEditor();
        var node = new InputNode();
        editor.Nodes.Add(node);
        var rect = node.Output.DotRectangle;
        var point = editor.CanvasToControl(new Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2));

        editor.MouseDownAt(point);

        Assert.Same(node.Output, editor.ConnectingOption);
    });

    [Fact]
    public void EmptyHitTestDoesNotRetainPreviousMarkLines() => StaThread.Run(() =>
    {
        using var editor = new STNodeEditor();
        var node = new InputNode { Mark = "First\nSecond" };
        editor.Nodes.Add(node);
        var rect = node.MarkRectangle;
        var hit = editor.FindNodeFromPoint(new PointF(rect.Left + 1, rect.Top + 1));
        Assert.Equal(new[] { "First", "Second" }, hit.MarkLines);

        var miss = editor.FindNodeFromPoint(new PointF(-10000, -10000));

        Assert.Null(miss.Node);
        Assert.Null(miss.NodeOption);
        Assert.Null(miss.Mark);
        Assert.Null(miss.MarkLines);
    });

    [Fact]
    public void DescriptorsSerializeWithoutAPropertyGrid() => StaThread.Run(() =>
    {
        using var source = new STNodeEditor();
        source.Nodes.Add(new SerializableNode { Number = 42, Values = ["First", null, "Last"] });
        byte[] data = source.GetCanvasData();
        using var destination = new STNodeEditor();
        destination.LoadAssembly(typeof(SerializableNode).Assembly.Location);

        destination.LoadCanvas(data);

        var restored = Assert.IsType<SerializableNode>(Assert.Single(destination.Nodes));
        Assert.Equal(42, restored.Number);
        Assert.Null(restored.OptionalText);
        Assert.Equal(new[] { "First", "", "Last" }, restored.Values);
    });

    [Fact]
    public void UnboundDescriptorReportsMissingBinding()
    {
        var descriptor = new STNodePropertyDescriptor();
        Assert.Throws<InvalidOperationException>(() => descriptor.Node);
        Assert.Throws<InvalidOperationException>(() => descriptor.PropertyInfo);
        Assert.Throws<InvalidOperationException>(() => descriptor.Control);
    }

    [STNode("Tests")]
    public sealed class SerializableNode : STNode
    {
        [STNodeProperty("Number", "")]
        public int Number { get; set; }
        [STNodeProperty("OptionalText", "")]
        public string? OptionalText { get; set; }
        [STNodeProperty("Values", "")]
        public string?[] Values { get; set; } = [];
    }

    private sealed class InputNode : STNode
    {
        public STNodeOption Output { get; }
        public InputNode()
        {
            Output = new STNodeOption("Output", typeof(string), false);
            OutputOptions.Add(Output);
        }
        public void Hover(STNodeControl control) => m_ctrl_hover = control;
        public void Wheel()
        {
            var e = new MouseEventArgs(MouseButtons.None, 0, 0, 0, 120);
            OnMouseWheel(e);
            OnMouseHWheel(e);
        }
    }

    private sealed class InputEditor : STNodeEditor
    {
        public STNodeOption? ConnectingOption => m_option_down;
        public void MouseDownAt(Point point) =>
            OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0));
    }
}
