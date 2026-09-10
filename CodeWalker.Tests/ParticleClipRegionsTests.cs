using CodeWalker.GameFiles;
using CodeWalker.Rendering;
using SharpDX;
using Xunit;
using System.Globalization;

namespace CodeWalker.Tests;

public class ParticleClipRegionsTests
{
    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("1 1 TEX 1 1 0 1 0 1")]
    [InlineData("1 1 TEX 1 1 invalid 1 NaN Infinity")]
    [InlineData("2 2 TEX 1 1 0 1 0 1 tex 1 1 2 3 4 5")]
    [InlineData("2 2 É漢 1 1 0 1 0 1 İI 1 1 2 3 4 5")]
    [InlineData("2 2 TEX 2 1 0 1 0 1 9 1 1")]
    [InlineData("1 0 TEX -1 1")]
    [InlineData("1 1 TEX 1 1 0 1 0")]
    [InlineData("1 1 TEX 1 1 0\u00a01 0 1")]
    public void SpanParserMatchesPreviousFormatBehavior(string text) => Compare(text);

    [Fact]
    public void AllTruncationPointsAndWhitespaceLayoutsMatch()
    {
        var tokens = "2 3 A 1 2 0 1 0 0.5 0 1 0.5 1 B 1 1 0 1 0 1".Split(' ');
        foreach (var separator in new[] { " ", "\t", "\r\n", " \t \n" })
        for (int count = 0; count <= tokens.Length; count++)
            Compare(separator + string.Join(separator, tokens.Take(count)) + separator);
    }

    private static void Compare(string text)
    {
        var actual = new Dictionary<uint, ParticleClipRegions.ClipRegion>();
        ParticleClipRegions.Parse(text, actual);
        var expected = ReferenceParse(text);
        Assert.Equal(expected.Keys.Order(), actual.Keys.Order());
        foreach (var (hash, region) in expected)
        {
            Assert.Equal(region.Cols, actual[hash].Cols);
            Assert.Equal(region.Rows, actual[hash].Rows);
            Assert.Equal(region.Frames, actual[hash].Frames);
        }
    }

    private static Dictionary<uint, ParticleClipRegions.ClipRegion> ReferenceParse(string text)
    {
        var result = new Dictionary<uint, ParticleClipRegions.ClipRegion>();
        var tokens = text.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        int i = 0;
        if (tokens.Length < 2) return result;
        int textures = Int(tokens[i++]);
        i++;
        for (int t = 0; t < textures && i + 2 < tokens.Length; t++)
        {
            string name = tokens[i++];
            int cols = Int(tokens[i++]), rows = Int(tokens[i++]);
            var frames = new Vector4[Math.Max(0, cols * rows)];
            for (int f = 0; f < frames.Length && i + 3 < tokens.Length; f++)
            {
                float uMin = Float(tokens[i++]), uMax = Float(tokens[i++]);
                float vMin = Float(tokens[i++]), vMax = Float(tokens[i++]);
                frames[f] = new Vector4(uMin, vMin, uMax, vMax);
            }
            result[JenkHash.GenHash(name.ToLowerInvariant())] = new() { Cols = cols, Rows = rows, Frames = frames };
        }
        return result;
    }

    private static int Int(string text) => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
    private static float Float(string text) => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;
}
