using System.Text;
using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class PlatformPathTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(257)]
    [InlineData(10000)]
    public void PlatformPathsMatchExistingCharacterAndTokenRules(int padding)
    {
        foreach (var suffix in new[] { "", "%PLATFORM%\\FILE", "platform:/X64", "PLATFORM:/file", "%platform%", "%PLATFORM%platform:", "%%PLATFORM%platform:platform:", "platform", "%PLATFORM", "É漢\U00010400İI" })
        {
            var input = new string('a', padding) + suffix;
            var expected = ReferencePlatformPath(input);
            Assert.Equal(expected, GameFileCache.GetDlcPlatformPath(input));
            if (expected == input) Assert.Same(input, GameFileCache.GetDlcPlatformPath(input));
        }
        Assert.Null(GameFileCache.GetDlcPlatformPath(null));
    }

    private static string ReferencePlatformPath(string input)
    {
        var output = new StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            if (input.AsSpan(i).StartsWith("%PLATFORM%", StringComparison.Ordinal))
            {
                output.Append("x64"); i += 9;
            }
            else if (input.AsSpan(i).StartsWith("platform:", StringComparison.Ordinal))
            {
                output.Append("x64"); i += 8;
            }
            else output.Append(input[i] == '\\' ? '/' : char.ToLowerInvariant(input[i]));
        }
        return output.ToString();
    }
}
