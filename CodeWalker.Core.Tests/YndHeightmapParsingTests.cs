using System.Globalization;
using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class YndHeightmapParsingTests
{
    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("ar-SA")]
    public void HeightmapEditsMatchPreviousParser(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            foreach (var width in new[] { 0, 1, 4 })
            foreach (var text in new[]
            {
                "", "\n\n", " \n\t\n\r", "1 2 3 4 5\n6", "\n1\n\n2\n",
                "1  2 invalid 256\n-1 +3 255", "1\t2 3\n1\u00a02 4",
                "\u20031 2\u2003\r\n3 4", "1\n2\n3\n4\n5"
            })
            {
                var map = Create(width);
                var expected = map.Rows.Select(r => r.Values.ToArray()).ToArray();
                var rows = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                for (int y = 0; y < rows.Length; y++)
                {
                    var columns = rows[y].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var values = columns.Select(s => { byte.TryParse(s, out var v); return v; }).ToArray();
                    if (y >= expected.Length) continue;
                    expected[y] = new byte[width];
                    Buffer.BlockCopy(values, 0, expected[y], 0, Math.Min(values.Length, width));
                }
                map.SetData(text);
                Assert.Equal(expected.SelectMany(r => r), map.GetBytes());
                Assert.Equal(width, map.CountX);
                Assert.Equal(3, map.CountY);
                Assert.All(map.Rows, r => Assert.Equal(width, r.Values.Length));
            }
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void MissingRowsAreIgnoredAndNullInputStillFails()
    {
        var map = Create(4);
        map.Rows = null!;
        map.SetData("1 2 3\n4 5");
        Assert.Null(map.Rows);
        Assert.Throws<NullReferenceException>(() => map.SetData(null!));
        map.Rows = [];
        map.SetData("1 2 3");
        Assert.Empty(map.Rows);
    }

    private static YndJunctionHeightmap Create(int width) => new([], new YndJunction())
    {
        CountX = width,
        CountY = 3,
        Rows = Enumerable.Range(0, 3).Select(i => new YndJunctionHeightmapRow(Enumerable.Repeat((byte)(70 + i), width).ToArray())).ToArray()
    };
}
