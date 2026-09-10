using System.Globalization;
using System.Xml;
using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class AwcGrainParsingTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" \r\n\t ")]
    [InlineData("1 2.5 3 4\n5 6.5 7 8")]
    [InlineData("\n 1 2.5 3 4\r\n\r\n5 6.5 7 8 \n")]
    [InlineData("1 2 3\ninvalid\n1 NaN 2 3\n1 Infinity 2 3\n1 -0 2 3")]
    [InlineData("1\t2 3 4\n1 2 3 4 trailing\n1\u00a02 3 4")]
    public void XmlGrainRowsMatchPreviousLoader(string text)
    {
        var doc = new XmlDocument();
        doc.LoadXml("<Item><GranularGrains/></Item>");
        doc.DocumentElement!["GranularGrains"]!.InnerText = text;
        var chunk = new AwcGranularGrainsChunk(new AwcChunkInfo());
        chunk.ReadXml(doc.DocumentElement);
        var rows = text.Trim().Split('\n');
        Assert.Equal(rows.Length, chunk.GranularGrains.Length);
        for (int i = 0; i < rows.Length; i++)
        {
            var tokens = rows[i].Trim().Split([" "], StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).Where(s => s.Length != 0).ToArray();
            uint u = 0;
            float f = 0;
            ushort s1 = 0, s2 = 0;
            if (tokens.Length >= 4)
            {
                uint.TryParse(tokens[0], out u);
                FloatUtil.TryParse(tokens[1], out f);
                ushort.TryParse(tokens[2], out s1);
                ushort.TryParse(tokens[3], out s2);
            }
            var actual = chunk.GranularGrains[i];
            Assert.Equal(u, actual.UnkUint1);
            Assert.Equal(BitConverter.SingleToInt32Bits(f), BitConverter.SingleToInt32Bits(actual.UnkFloat1));
            Assert.Equal(s1, actual.UnkUshort1);
            Assert.Equal(s2, actual.UnkUshort2);
        }
    }

    [Fact]
    public void MissingGrainElementPreservesExistingRecords()
    {
        var doc = new XmlDocument();
        doc.LoadXml("<Item/>");
        var chunk = new AwcGranularGrainsChunk(new AwcChunkInfo());
        var grains = new[] { new AwcGranularGrainsChunk.GranularGrain { UnkUint1 = 123 } };
        chunk.GranularGrains = grains;
        chunk.ReadXml(doc.DocumentElement!);
        Assert.Same(grains, chunk.GranularGrains);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("ar-SA")]
    public void GrainRecordsMatchPreviousParser(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            foreach (var line in new[]
            {
                "", " \t\r\n ", "1", "1 2 3", "1 2.5 3 4", "1 2.5 3 4 ignored extra",
                " 1  \t 2.5 \r 3 4 ", "1\t2 3 4", "\u20031\u2003 2.5 3 4",
                "4294967295 -2e3 65535 0", "4294967296 invalid 65536 -1",
                "invalid NaN invalid invalid", "1 Infinity 2 3", "1 -0 2 3", "1 2,5 3 4"
            })
            {
                var actual = new AwcGranularGrainsChunk.GranularGrain
                {
                    UnkUint1 = 77, UnkFloat1 = 88, UnkUshort1 = 99, UnkUshort2 = 111
                };
                uint expectedUint = 77;
                float expectedFloat = 88;
                ushort expectedShort1 = 99, expectedShort2 = 111;
                var fields = line.Split([" "], StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim()).Where(s => s.Length != 0).ToArray();
                if (fields.Length >= 4)
                {
                    uint.TryParse(fields[0], out expectedUint);
                    FloatUtil.TryParse(fields[1], out expectedFloat);
                    ushort.TryParse(fields[2], out expectedShort1);
                    ushort.TryParse(fields[3], out expectedShort2);
                }
                actual.ReadLine(line);
                Assert.Equal(expectedUint, actual.UnkUint1);
                Assert.Equal(BitConverter.SingleToInt32Bits(expectedFloat), BitConverter.SingleToInt32Bits(actual.UnkFloat1));
                Assert.Equal(expectedShort1, actual.UnkUshort1);
                Assert.Equal(expectedShort2, actual.UnkUshort2);
            }
            Assert.Throws<NullReferenceException>(() => new AwcGranularGrainsChunk.GranularGrain().ReadLine(null!));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
