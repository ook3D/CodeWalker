using System.Globalization;
using System.Xml;
using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class YbnColourParsingTests
{
    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("ar-SA")]
    public void MaterialColoursMatchPreviousParser(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            foreach (var text in new[]
            {
                "", " \r\n\t ", "1", ",,,", "1,2", "1,2,3", "1,2,3,4,5",
                "1,,2, ,3,4", "invalid,256,-1,255", "\u20031\u2003,\t2,3,4\r\n5,6",
                "1\n1,2\n\n,,,\n3,4,5,6", "1 2,3", "+1,-0,255,0"
            })
            {
                var doc = new XmlDocument();
                var node = doc.CreateElement("Colours");
                node.InnerText = text;
                var expected = new List<(byte, byte, byte, byte)>();
                foreach (var line in text.Split('\n'))
                {
                    var components = line.Trim().Split(',').Select(s => s.Trim()).Where(s => s.Length != 0)
                        .Select(s => { byte.TryParse(s, out var value); return value; }).ToArray();
                    if (components.Length < 2) continue;
                    expected.Add((components[0], components[1], components.Length > 2 ? components[2] : (byte)0,
                        components.Length > 3 ? components[3] : (byte)0));
                }
                var actual = XmlYbn.GetRawBoundMaterialColourArray(node);
                if (expected.Count == 0) Assert.Null(actual);
                else
                {
                    Assert.NotNull(actual);
                    Assert.Equal(expected, actual.Select(c => (c.R, c.G, c.B, c.A)));
                }
            }
            Assert.Null(XmlYbn.GetRawBoundMaterialColourArray(null));
            var empty = new XmlDocument();
            empty.LoadXml("<Root/>");
            Assert.Null(XmlYbn.GetChildRawBoundMaterialColourArray(empty.DocumentElement!, "Missing"));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
