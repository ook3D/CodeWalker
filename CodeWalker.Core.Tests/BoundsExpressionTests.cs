using System.Text;
using System.Xml;
using CodeWalker.GameFiles;
using SharpDX;
using Xunit;

namespace CodeWalker.Core.Tests;

public class BoundsExpressionTests
{
    private static XmlElement Parse(string xml)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        return doc.DocumentElement ?? throw new InvalidOperationException();
    }

    [Fact]
    public void EmptyExpressionDictionaryRoundTrips()
    {
        var dictionary = new ExpressionDictionary();
        var data = ResourceBuilder.Build(dictionary, 25);
        var file = new YedFile();
        RpfFile.LoadResourceFile(file, data, 25);
        Assert.Equal(data, file.Save());
    }

    [Fact]
    public void SparseExpressionXmlHasSafeDefaults()
    {
        var expression = new Expression();
        expression.ReadXml(Parse("<Item />"));
        Assert.Equal(string.Empty, expression.Name?.Value);
        Assert.Empty(expression.Streams.data_items);
        Assert.Empty(expression.Tracks.data_items);
        expression.WriteXml(new StringBuilder(), 0);
        var source = new ExpressionInstrBlend.Source();
        source.UpdateValues(1, 0, []);
        Assert.Empty(source.X.Weights);
    }

    [Fact]
    public void InvalidPolygonTypeDoesNotModifyGeometry()
    {
        var geometry = new BoundGeometry();
        Assert.Null(geometry.AddPolygon((BoundPolygonType)255));
        Assert.Empty(geometry.Polygons);
        Assert.Empty(geometry.Vertices);
        Assert.Null(geometry.GetVertexObject(0));
    }

    [Fact]
    public void CompositeXmlPreservesMissingChildSlots()
    {
        var bounds = new BoundComposite();
        bounds.ReadXml(Parse("""
            <Bounds><Children><Item type="None" /><Item type="Sphere">
              <CompositeTransform>1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1</CompositeTransform>
            </Item></Children></Bounds>
            """));
        var children = Assert.IsType<ResourcePointerArray64<Bounds>>(bounds.Children);
        Assert.Equal(2, children.data_items.Length);
        Assert.Null(children.data_items[0]);
        Assert.IsType<BoundSphere>(children.data_items[1]);
    }

    [Fact]
    public void BvhPreservesCompositeCapacityForMissingChildren()
    {
        var items = new List<BVHBuilderItem?>
        {
            null,
            new() { Index = 1, Min = Vector3.Zero, Max = Vector3.One }
        };
        var bvh = BVHBuilder.Build(items, 1);
        Assert.NotNull(bvh);
        Assert.Equal(5, bvh.Nodes.data_items.Length);
        Assert.Equal(1, bvh.Nodes.data_items[0].ItemId);
    }

    [Fact]
    public void SparseDlcChangeSetUsesEmptyCollections()
    {
        var changeSet = new DlcContentChangeSet(Parse("<Item><associatedMap>map</associatedMap><filesToEnable /></Item>"));
        Assert.Empty(changeSet.filesToEnable);
        Assert.Empty(changeSet.filesToDisable);
        Assert.Empty(changeSet.mapChangeSetData);
        Assert.Null(changeSet.executionConditions);
        Assert.Equal("map", changeSet.ToString());
    }
}
