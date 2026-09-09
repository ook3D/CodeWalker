using System.Globalization;
using System.Xml;
using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class CarVariationParsingTests
{
    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("ar-SA")]
    public void NumericVariationsMatchPreviousParser(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            foreach (var text in new[] { "", "\r\n \t", "0 1 2 255", "-1 256 invalid +3", "1\r2 3", "1\u00a02 3", "\u20031\u2003 2", "0\n1\t2\r\n3" })
            {
                var doc = new XmlDocument();
                doc.LoadXml("<Item><indices/><liveries/></Item>");
                doc.DocumentElement!["indices"]!.InnerText = text;
                doc.DocumentElement["liveries"]!.InnerText = text;
                var expected = new List<byte>();
                foreach (var token in text.Split(['\n', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries))
                {
                    if (byte.TryParse(token.Trim(), out var value)) expected.Add(value);
                }
                var actual = new CVehicleModelInfoVariation_2575850962(doc.DocumentElement);
                Assert.Equal(expected, actual.indices);
                Assert.Equal(expected.Select(b => b > 0), actual.liveries);
            }
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void LiveryItemsTakePrecedenceOverNumericText()
    {
        var doc = new XmlDocument();
        doc.LoadXml("<Item><liveries>1 1<Item value=\"true\"/><Item value=\"False\"/><Item value=\"1\"/><Item/><Item value=\"TRUE\"/></liveries></Item>");
        var actual = new CVehicleModelInfoVariation_2575850962(doc.DocumentElement!);
        Assert.Empty(actual.indices);
        Assert.Equal(new[] { true, false, false, false, true }, actual.liveries);
    }

    [Theory]
    [InlineData("<Item/>")]
    [InlineData("<Item><indices/><liveries/></Item>")]
    public void MissingAndEmptyCollectionsRemainEmpty(string xml)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        var actual = new CVehicleModelInfoVariation_2575850962(doc.DocumentElement!);
        Assert.Empty(actual.indices);
        Assert.Empty(actual.liveries);
    }
}
