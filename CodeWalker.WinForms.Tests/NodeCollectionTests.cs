using System.Collections;
using ST.Library.UI.NodeEditor;
using Xunit;

namespace CodeWalker.WinForms.Tests;

public class NodeCollectionTests
{
    [Theory]
    [InlineData("nodes")]
    [InlineData("options")]
    [InlineData("controls")]
    public void IListQueriesHandleNullAndIncompatibleValues(string kind) => StaThread.Run(() =>
    {
        using var editor = new STNodeEditor();
        var (collection, createItem) = CreateCollection(editor, kind);
        collection.Add(createItem());

        foreach (object? value in new object?[] { null, "wrong type" })
        {
            Assert.False(collection.Contains(value));
            Assert.Equal(-1, collection.IndexOf(value));
            collection.Remove(value);
        }
        Assert.Single(collection);
        Assert.Throws<ArgumentNullException>(() => collection.Add(null));
        Assert.Throws<ArgumentNullException>(() => collection.Insert(0, null));
    });

    [Theory]
    [InlineData("nodes")]
    [InlineData("options")]
    [InlineData("controls")]
    public void RemovingLastItemAllowsItToBeAddedAgain(string kind) => StaThread.Run(() =>
    {
        using var editor = new STNodeEditor();
        var (collection, createItem) = CreateCollection(editor, kind);
        var first = createItem();
        var last = createItem();
        collection.Add(first);
        collection.Add(last);

        collection.RemoveAt(1);

        Assert.False(collection.Contains(last));
        Assert.Equal(-1, collection.IndexOf(last));
        Assert.Equal(1, collection.Add(last));
        Assert.Equal(2, collection.Count);
        Assert.Same(last, collection[1]);
    });

    private static (IList Collection, Func<object> CreateItem) CreateCollection(STNodeEditor editor, string kind)
    {
        var node = new TestNode();
        return kind switch
        {
            "nodes" => (editor.Nodes, () => new TestNode()),
            "options" => (node.Options, () => new STNodeOption("Option", typeof(string), false)),
            "controls" => (node.NodeControls, () => new STNodeControl()),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private sealed class TestNode : STNode
    {
        public STNodeOptionCollection Options => InputOptions;
        public STNodeControlCollection NodeControls => Controls;
    }
}
