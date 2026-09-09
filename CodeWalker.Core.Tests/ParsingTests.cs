using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using SharpDX;
using Xunit;

namespace CodeWalker.Core.Tests;

public class ParsingTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(16)]
    [InlineData(3)]
    public void RawByteArraysMatchPreviousParser(int fromBase)
    {
        var whitespace = new string(Enumerable.Range(0, 65536).Select(i => (char)i).Where(char.IsWhiteSpace).ToArray());
        var cases = new List<string>
        {
            "", whitespace, "00" + whitespace + "01", "0xFF +0x01", "0Xff", "+1", "-0", "-1",
            "100", "256", "000000FF", "FFFFFFFFFFFFFFFFFFFFFFFF", "0x", "+", "-", "GG", "1\u200b2", "F\0"
        };
        cases.AddRange(Enumerable.Range(0, 256).Select(i => i.ToString("X2", CultureInfo.InvariantCulture)));
        cases.AddRange(Enumerable.Range(0, 256).Select(i => i.ToString("x", CultureInfo.InvariantCulture)));
        if (fromBase != 3)
        {
            cases.Add(string.Join(whitespace, Enumerable.Range(0, 256).Select(i => Convert.ToString(i, fromBase))));
        }
        foreach (var text in cases)
        {
            byte[]? expected = null;
            var error = Record.Exception(() => expected = Regex.Split(text, @"[\s\r\n\t]")
                .Where(s => s.Length != 0).Select(s => Convert.ToByte(s, fromBase)).ToArray());
            if (error == null)
            {
                Assert.Equal(expected, Xml.GetRawByteArray(Node(text), fromBase));
            }
            else
            {
                var actual = Record.Exception(() => Xml.GetRawByteArray(Node(text), fromBase));
                Assert.NotNull(actual);
                Assert.Equal(error.GetType(), actual.GetType());
                Assert.Equal(error.Message, actual.Message);
            }
        }
        Assert.Empty(Xml.GetRawByteArray(null, fromBase));
        Assert.Null(Xml.GetChildRawByteArrayNullable(Node(""), "Missing", fromBase));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("ar-SA")]
    public void RawNumericArraysMatchPreviousParser(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var whitespace = new string(Enumerable.Range(0, 65536).Select(i => (char)i).Where(char.IsWhiteSpace).ToArray());
            foreach (var text in new[] { "", whitespace, "1 2 -3 65536 4294967296 invalid", "1.5 -2e3 NaN Infinity 1,234.5", "12" + whitespace + "34\u200b56" })
            {
                var node = Node(text);
                var tokens = Regex.Split(text, @"[\s\r\n\t]").Where(s => s.Length != 0).ToArray();
                Assert.Equal(tokens.Select(s => { ushort.TryParse(s, out var v); return v; }), Xml.GetRawUshortArray(node));
                Assert.Equal(tokens.Select(s => { uint.TryParse(s, out var v); return v; }), Xml.GetRawUintArray(node));
                Assert.Equal(tokens.Select(s => { int.TryParse(s, out var v); return v; }), Xml.GetRawIntArray(node));
                Assert.Equal(tokens.Select(s => { float.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v); return v; }), Xml.GetRawFloatArray(node));
            }
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void RawVectorsKeepEmptyComponentAndIncompleteRowBehavior(int dimensions)
    {
        const string text = "\r\n1,, 2,3,4,5\r\n1\n  \ninvalid,2,3,4\n,,,\n5,6\n-1.5,2e3,NaN,Infinity";
        var expected = text.Split('\n').Select(line => line.Split(',').Select(s => s.Trim()).Where(s => s.Length != 0)
            .Select(FloatUtil.Parse).ToArray()).Where(values => values.Length >= dimensions).ToArray();
        var node = Node(text);
        if (dimensions == 2) Assert.Equal(expected.Select(v => new Vector2(v[0], v[1])), Xml.GetRawVector2Array(node));
        if (dimensions == 3) Assert.Equal(expected.Select(v => new Vector3(v[0], v[1], v[2])), Xml.GetRawVector3Array(node));
        if (dimensions == 4) Assert.Equal(expected.Select(v => new Vector4(v[0], v[1], v[2], v[3])), Xml.GetRawVector4Array(node));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1,,3,4,5")]
    [InlineData(" 1.5 , -2e3 , invalid , 4")]
    [InlineData("NaN,Infinity,-Infinity")]
    public void VectorStringsKeepPositionalEmptyComponents(string text)
    {
        var expected = text.Split(',').Select(s => { float.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var f); return f; }).Concat(new float[4]).ToArray();
        Assert.Equal(new Vector2(expected[0], expected[1]), FloatUtil.ParseVector2String(text));
        Assert.Equal(new Vector3(expected[0], expected[1], expected[2]), FloatUtil.ParseVector3String(text));
        Assert.Equal(new Vector4(expected[0], expected[1], expected[2], expected[3]), FloatUtil.ParseVector4String(text));
    }

    [Fact]
    public void NullNumericInputKeepsFallbacks()
    {
        Assert.Empty(Xml.GetRawIntArray(null!));
        Assert.Empty(Xml.GetRawFloatArray(null!));
        Assert.Empty(Xml.GetRawVector3Array(null!));
        Assert.False(FloatUtil.TryParse((string)null!, out var value));
        Assert.Equal(0, value);
    }

    private static XmlElement Node(string text)
    {
        var document = new XmlDocument();
        var node = document.CreateElement("Item");
        node.InnerText = text;
        return node;
    }
}
