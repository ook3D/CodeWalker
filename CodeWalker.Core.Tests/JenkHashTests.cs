using System.Text;
using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class JenkHashTests
{
    [Theory]
    [InlineData("")]
    [InlineData("adder")]
    [InlineData("A\0Bé漢😀")]
    [InlineData("\ud800abc\udfff")]
    public void SpanAndEncodedHashesPreserveAlgorithms(string text)
    {
        Assert.Equal(ReferenceHash(text.Select(c => (byte)c).ToArray()), JenkHash.GenHash(text));
        Assert.Equal(JenkHash.GenHash(text), JenkHash.GenHash((text + ".ydr").AsSpan(0, text.Length)));
        foreach (int length in new[] { 1, 256, 257, 4096 })
        {
            var input = string.Concat(Enumerable.Repeat(text, length));
            foreach (var encoding in new[] { JenkHashInputEncoding.ASCII, JenkHashInputEncoding.UTF8, (JenkHashInputEncoding)42 })
            {
                var bytes = (encoding == JenkHashInputEncoding.ASCII ? Encoding.ASCII : Encoding.UTF8).GetBytes(input);
                Assert.Equal(ReferenceHash(bytes), JenkHash.GenHash(input, encoding));
                Assert.Equal(ReferenceHash(bytes), JenkHash.GenHash(bytes.AsSpan()));
            }
        }
    }

    [Fact]
    public void StringNullStillHashesToZero() => Assert.Equal(0u, JenkHash.GenHash((string)null!));

    private static uint ReferenceHash(byte[] bytes)
    {
        uint hash = 0;
        foreach (byte b in bytes)
        {
            hash += b;
            hash += hash << 10;
            hash ^= hash >> 6;
        }
        hash += hash << 3;
        hash ^= hash >> 11;
        hash += hash << 15;
        return hash;
    }
}
