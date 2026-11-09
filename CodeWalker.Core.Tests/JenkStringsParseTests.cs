using System.Reflection;
using System.Text;
using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class JenkStringsParseTests
{
    // RpfManager parses strings.dat in parallel chunks. The result must match the original StreamReader.ReadLine
    // loop (Trim, skip blank and "//" lines, skip zero hashes), in line order.
    private static List<KeyValuePair<uint, string>> Reference(byte[] text)
    {
        var result = new List<KeyValuePair<uint, string>>();
        using var reader = new StreamReader(new MemoryStream(text), Encoding.UTF8);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var str = line.Trim();
            if (string.IsNullOrEmpty(str)) continue;
            if (str.StartsWith("//")) continue;
            uint hash = JenkHash.GenHash(str);
            if (hash == 0) continue;
            result.Add(new KeyValuePair<uint, string>(hash, str));
        }
        return result;
    }

    private static List<KeyValuePair<uint, string>>? Actual(byte[] text)
    {
        var method = typeof(RpfManager).GetMethod("ParseJenkStringLines", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (List<KeyValuePair<uint, string>>?)method.Invoke(null, new object[] { text });
    }

    [Theory]
    [InlineData(10, 1)]
    [InlineData(5_000, 2)]
    [InlineData(400_000, 3)]
    public void ParallelParseMatchesStreamReader(int lineCount, int seed)
    {
        var rng = new Random(seed);
        var pieces = new[] { "prop_tree_01", "  padded name ", "//comment", "", " ", "Ünïcödé_名前", "a/b/c", "x\ty", " nbsp " };
        var breaks = new[] { "\r\n", "\n", "\r", "\r\n\r\n" };
        var sb = new StringBuilder();
        if (seed == 2)
        {
            sb.Append('﻿');
        }
        for (int i = 0; i < lineCount; i++)
        {
            sb.Append(pieces[rng.Next(pieces.Length)]);
            sb.Append(rng.Next(1000));
            sb.Append(breaks[rng.Next(breaks.Length)]);
        }
        sb.Append("last_line_without_break");
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        if (seed == 3)
        {
            bytes[bytes.Length / 2] = 0xC3; //an invalid / truncated multi-byte sequence somewhere
        }
        Assert.Equal(Reference(bytes), Actual(bytes));
    }

    [Fact]
    public void Utf16ByteOrderMarkIsLeftToStreamReader()
    {
        Assert.Null(Actual(new byte[] { 0xFF, 0xFE, (byte)'a', 0 }));
    }
}
