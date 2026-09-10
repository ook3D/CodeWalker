using System.Globalization;
using System.Threading.Tasks;
using System.Xml;
using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class VehicleInitDataTests
{
    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("ar-SA")]
    public void DelimitedArraysKeepPreviousParsingBehavior(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            foreach (var text in new[] { "", " \r\n\t ", " A  B\tC D\nE ", "\u2003A\u2003 B", "1.5\ninvalid\n-2e3\nNaN\nInfinity", "1 2\n3,5\n0\n-0", "1\r2\n3" })
            {
                var doc = new XmlDocument();
                doc.LoadXml("<Item><flags/><requiredExtras/><lodDistances/></Item>");
                foreach (XmlNode child in doc.DocumentElement!.ChildNodes) child.InnerText = text;
                var expectedStrings = text.Split(' ').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
                var expectedFloats = new List<float>();
                foreach (var token in text.Split('\n'))
                {
                    var trimmed = token.Trim();
                    if (trimmed.Length > 0 && FloatUtil.TryParse(trimmed, out var value)) expectedFloats.Add(value);
                }
                var vehicle = new VehicleInitData();
                vehicle.Load(doc.DocumentElement);
                Assert.Equal(expectedStrings, vehicle.flags);
                Assert.Equal(expectedStrings, vehicle.requiredExtras);
                Assert.Equal(expectedFloats.Select(BitConverter.SingleToInt32Bits), vehicle.lodDistances.Select(BitConverter.SingleToInt32Bits));
            }
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void ItemArraysPreserveWhitespaceAndOrderWhileSkippingEmptyItems()
    {
        var doc = new XmlDocument();
        doc.LoadXml("<Item><trailers><Item>  trailer  </Item><Item/><Item> </Item><Item>trailer</Item><Item>trailer</Item></trailers></Item>");
        // Set whitespace explicitly so XML loading does not discard it.
        doc.DocumentElement!["trailers"]!.ChildNodes[2]!.InnerText = " ";
        var vehicle = new VehicleInitData();
        vehicle.Load(doc.DocumentElement);
        Assert.Equal(new[] { "  trailer  ", " ", "trailer", "trailer" }, vehicle.trailers);
        Assert.Empty(vehicle.flags);
        Assert.Empty(vehicle.requiredExtras);
        Assert.Empty(vehicle.lodDistances);
        Assert.Empty(vehicle.rewards);
    }

    [Fact]
    public void ParallelLoadsKeepVehicleArraysIsolated()
    {
        Parallel.For(0, 2000, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
        {
            var document = new XmlDocument();
            document.LoadXml($"""
                <Item>
                    <lodDistances> {i}
                invalid
                {i + 1}
                </lodDistances>
                    <flags> FLAG_{i}  COMMON </flags>
                    <trailers><Item>trailer_{i}</Item><Item></Item><Item>shared</Item></trailers>
                </Item>
                """);
            var vehicle = new VehicleInitData();

            vehicle.Load(document.DocumentElement!);

            Assert.Equal(new float[] { i, i + 1 }, vehicle.lodDistances);
            Assert.Equal(new[] { $"FLAG_{i}", "COMMON" }, vehicle.flags);
            Assert.Equal(new[] { $"trailer_{i}", "shared" }, vehicle.trailers);
        });
    }
}
