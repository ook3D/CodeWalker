using System.Globalization;
using System.Text;
using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class CacheFileDateTests
{
    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("ar-SA")]
    public void DateFieldsMatchPreviousParser(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            foreach (var line in new[]
            {
                "", " ", "  ", "1", "1 2", "1 2 3", "1 2 3 ignored fields",
                " 1 2 3", "1  3", "1 2  3", "1\t2 3", "\t1 2 3\r",
                "4294967295 9223372036854775807 4294967295",
                "4294967296 9223372036854775808 4294967296",
                "+1 -9223372036854775808 +3", "-1 invalid -3", "1\u00a0 2 3",
                "invalid 2 3", "1 2 invalid"
            })
            {
                var parts = line.Split(' ');
                uint hash = 0, id = 0;
                long timestamp = 0;
                if (parts.Length > 0) uint.TryParse(parts[0], out hash);
                if (parts.Length > 1) long.TryParse(parts[1], out timestamp);
                if (parts.Length > 2) uint.TryParse(parts[2], out id);
                var actual = new CacheFileDate(line);
                Assert.Equal(hash, actual.FileName.Hash);
                Assert.Equal(timestamp, actual.TimeStamp);
                Assert.Equal(id, actual.FileID);
            }
            Assert.Throws<NullReferenceException>(() => new CacheFileDate(null!));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void CacheLoadPreservesDateRecords()
    {
        var header = new byte[100];
        Encoding.ASCII.GetBytes("[VERSION]test\r\n").CopyTo(header, 0);
        var body = Encoding.ASCII.GetBytes("<fileDates>\n2740459947 130680580712018938 8944\n42 123\n1  3\n</fileDates>\n");
        var cache = new CacheDatFile();
        cache.Load([.. header, .. body], null!);
        Assert.Equal("test", cache.Version);
        Assert.Equal(new[] { (2740459947u, 130680580712018938L, 8944u), (42u, 123L, 0u), (1u, 0L, 3u) },
            cache.FileDates.Select(d => (d.FileName.Hash, d.TimeStamp, d.FileID)));
        Assert.Empty(cache.AllMapNodes);
        Assert.Empty(cache.AllBoundsStoreItems);
    }
}
