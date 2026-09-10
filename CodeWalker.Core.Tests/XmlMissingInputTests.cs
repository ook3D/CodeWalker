using System.Xml;
using SharpDX;
using Xunit;

namespace CodeWalker.Core.Tests;

public class XmlMissingInputTests
{
    [Fact]
    public void MissingNodesProduceDefaultNumericValues()
    {
        Assert.False(Xml.GetBoolAttribute(null, "value"));
        Assert.Equal(0, Xml.GetIntAttribute(null, "value"));
        Assert.Equal(0u, Xml.GetUIntAttribute(null, "value"));
        Assert.Equal(0ul, Xml.GetULongAttribute(null, "value"));
        Assert.Equal(0f, Xml.GetFloatAttribute(null, "value"));
        Assert.Equal(Vector3.Zero, Xml.GetChildVector3Attributes(null, "Position"));
        Assert.Equal(Matrix.Identity, Xml.GetChildMatrix(null, "Transform"));
    }

    [Fact]
    public void NodesWithoutAttributesProduceDefaultNumericValues()
    {
        var document = new XmlDocument();
        var text = document.CreateTextNode("text");
        Assert.False(Xml.GetBoolAttribute(text, "value"));
        Assert.Equal(0, Xml.GetIntAttribute(text, "value"));
        Assert.Equal(0u, Xml.GetUIntAttribute(text, "value"));
        Assert.Equal(0ul, Xml.GetULongAttribute(text, "value"));
        Assert.Equal(0f, Xml.GetFloatAttribute(text, "value"));

        document.LoadXml("<root>text</root>");
        Assert.Equal(0f, Xml.GetChildFloatAttribute(document.DocumentElement, "text()"));
    }

    [Fact]
    public void MissingParentProducesEmptyArrays()
    {
        Assert.Empty(Xml.GetChildRawByteArray(null, "Items"));
        Assert.Empty(Xml.GetChildRawUshortArray(null, "Items"));
        Assert.Empty(Xml.GetChildRawUintArray(null, "Items"));
        Assert.Empty(Xml.GetChildRawIntArray(null, "Items"));
        Assert.Empty(Xml.GetChildRawFloatArray(null, "Items"));
        Assert.Empty(Xml.GetChildRawVector2Array(null, "Items"));
        Assert.Empty(Xml.GetChildRawVector3Array(null, "Items"));
        Assert.Empty(Xml.GetChildRawVector4Array(null, "Items"));
    }
}
