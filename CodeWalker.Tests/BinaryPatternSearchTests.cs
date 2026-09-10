using CodeWalker.Tools;
using Xunit;

namespace CodeWalker.Tests;

public class BinaryPatternSearchTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("abc", "")]
    [InlineData("", "a")]
    [InlineData("aaa", "aa")]
    [InlineData("aaaa", "aa")]
    [InlineData("abcabc", "abc")]
    [InlineData("abc", "abcd")]
    [InlineData("abcabc", "c")]
    [InlineData("abc", "z")]
    public void KeepsNonOverlappingOffsets(string text, string pattern)
    {
        byte[] data = System.Text.Encoding.UTF8.GetBytes(text);
        byte[] needle = System.Text.Encoding.UTF8.GetBytes(pattern);
        Assert.Equal(ReferenceMatches(data, needle), Matches(data, needle));
    }

    [Fact]
    public void RandomBinaryInputsMatchReferenceIncludingSlices()
    {
        var random = new Random(42);
        for (int test = 0; test < 400; test++)
        {
            var data = new byte[random.Next(1, 2048)];
            random.NextBytes(data);
            int length = random.Next(0, Math.Min(data.Length, 256));
            var needle = data.AsSpan(random.Next(0, data.Length - length + 1), length).ToArray();
            Assert.Equal(ReferenceMatches(data, needle), Matches(data, needle));
            Assert.Equal(ReferenceMatches(data[1..], needle), Matches(data.AsSpan(1), needle));
        }
    }

    [Fact]
    public void DenseBinaryMatchesAndEarlyStopWork()
    {
        var data = new byte[65537];
        Assert.Equal(ReferenceMatches(data, [0, 0, 0]), Matches(data, [0, 0, 0]));
        var iterator = BinaryPatternSearch.FindMatches(data, [0]);
        Assert.True(iterator.MoveNext());
        Assert.Equal(0, iterator.Current);
        // Discarding the enumerator needs no disposal and must not scan the remainder.
    }

    [Fact]
    public void LowercasePreservesAllOtherByteValuesAndUnusedBufferTail()
    {
        var data = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        var original = data.ToArray();
        var output = Enumerable.Repeat((byte)0xee, 300).ToArray();
        BinaryPatternSearch.CopyLowercaseAscii(data, output);
        Assert.Equal(original.Select(b => b >= 65 && b <= 90 ? (byte)(b + 32) : b), output[..256]);
        Assert.All(output[256..], b => Assert.Equal(0xee, b));
        Assert.Equal(original, data);
        BinaryPatternSearch.CopyLowercaseAscii(data, data);
        Assert.Equal(output[..256], data);
        Assert.Throws<ArgumentException>(() => BinaryPatternSearch.CopyLowercaseAscii(data, new byte[255]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(257)]
    [InlineData(1000)]
    public void LowercaseHandlesVectorBoundariesAndSlices(int length)
    {
        var data = Enumerable.Range(0, length + 2).Select(i => (byte)i).ToArray();
        var expected = data.Skip(1).Take(length).Select(b => b >= 65 && b <= 90 ? (byte)(b + 32) : b).ToArray();
        var output = new byte[length];
        BinaryPatternSearch.CopyLowercaseAscii(data.AsSpan(1, length), output);
        Assert.Equal(expected, output);
    }

    [Theory]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(129)]
    [InlineData(4096)]
    public void LongAndShortStrategiesKeepDenseMatchOffsets(int length)
    {
        var data = new byte[length * 3 + 7];
        var needle = new byte[length];
        Assert.Equal(ReferenceMatches(data, needle), Matches(data, needle));
        needle[^1] = 1;
        Assert.Empty(Matches(data, needle));
    }

    private static List<int> Matches(ReadOnlySpan<byte> data, ReadOnlySpan<byte> needle)
    {
        var result = new List<int>();
        foreach (int match in BinaryPatternSearch.FindMatches(data, needle)) result.Add(match);
        return result;
    }

    private static List<int> ReferenceMatches(byte[] data, byte[] needle)
    {
        var result = new List<int>();
        if (needle.Length == 0) return result;
        for (int offset = 0; offset <= data.Length - needle.Length;)
        {
            int i = 0;
            while (i < needle.Length && data[offset + i] == needle[i]) i++;
            if (i == needle.Length) { result.Add(offset); offset += needle.Length; }
            else offset++;
        }
        return result;
    }
}
