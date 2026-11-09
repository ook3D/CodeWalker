using System.Reflection;
using System.Text;
using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class NameTableDecodeTests
{
    // RpfManager decodes .nametable files (zero-terminated single-byte names) in parallel chunks for the jenk
    // index. It must produce exactly what the original byte-by-byte StringBuilder decoder produced, in order.
    private static List<KeyValuePair<uint, string>> Reference(byte[] data)
    {
        var result = new List<KeyValuePair<uint, string>>();
        var sb = new StringBuilder();
        for (int i = 0; i < data.Length; i++)
        {
            byte c = data[i];
            if (c == 0)
            {
                string str = sb.ToString();
                if (!string.IsNullOrEmpty(str))
                {
                    string strl = str.ToLowerInvariant();
                    uint h = JenkHash.GenHash(strl);
                    if (h != 0)
                    {
                        result.Add(new KeyValuePair<uint, string>(h, strl));
                    }
                }
                sb.Clear();
            }
            else
            {
                sb.Append((char)c);
            }
        }
        return result;
    }

    private static List<KeyValuePair<uint, string>> Actual(byte[] data)
    {
        var method = typeof(RpfManager).GetMethod("AddNameTableNames", BindingFlags.NonPublic | BindingFlags.Static)!;
        var output = new List<KeyValuePair<uint, string>>();
        method.Invoke(null, new object[] { data, output });
        return output;
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(100, 2)]
    [InlineData(70_000, 3)]
    [InlineData(3_000_000, 4)]
    public void ParallelDecodeMatchesByteByByteDecoder(int length, int seed)
    {
        var rng = new Random(seed);
        var data = new byte[length];
        for (int i = 0; i < length; i++)
        {
            int r = rng.Next(100);
            //mostly letters, some upper case and Latin-1 bytes, frequent terminators incl. runs of them
            data[i] = r < 12 ? (byte)0 : r < 60 ? (byte)('a' + rng.Next(26)) : r < 85 ? (byte)('A' + rng.Next(26)) : (byte)rng.Next(1, 256);
        }
        Assert.Equal(Reference(data), Actual(data));
    }

    [Fact]
    public void EdgeCasesMatch()
    {
        foreach (var text in new[] { "\0\0abc\0", "ABC", "abc\0DEF", "\0", "x\0\0\0Y\0ÀÞÿ\0tail" })
        {
            var data = Encoding.Latin1.GetBytes(text);
            Assert.Equal(Reference(data), Actual(data));
        }
    }
}
