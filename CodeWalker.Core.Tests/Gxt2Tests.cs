using System.Globalization;
using System.Text;
using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class Gxt2Tests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\n \r\ninvalid\n0x00000002 = second\r\n0x00000001 = first\n")]
    [InlineData("0xFFFFFFFF = café 模型 😀\n0x00000000 = \n0x00000001 = first\n0x00000001 = duplicate")]
    [InlineData("\u20030x00000005 = spaces\u2003\nxx00000006---accepted\n0xGGGGGGGG = invalid")]
    public void TextImportMatchesPreviousParser(string? text)
    {
        var expected = new List<Gxt2Entry>();
        foreach (var line in text?.Split('\n', StringSplitOptions.RemoveEmptyEntries) ?? [])
        {
            var trimmed = line.Trim();
            if (trimmed.Length < 13) continue;
            if (uint.TryParse(trimmed.Substring(2, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hash))
                expected.Add(new Gxt2Entry { Hash = hash, Text = trimmed.Length > 13 ? trimmed.Substring(13) : "" });
        }
        expected.Sort((a, b) => a.Hash.CompareTo(b.Hash));
        var actual = Gxt2File.FromText(text);
        Assert.Equal((uint)expected.Count, actual.EntryCount);
        Assert.Equal(expected.Select(e => (e.Hash, e.Text)), actual.TextEntries.Select(e => (e.Hash, e.Text)));
        Assert.Equal(string.Concat(expected.Select(e => "0x" + e.Hash.ToString("X").PadLeft(8, '0') + " = " + e.Text + Environment.NewLine)), actual.ToText());
    }

    [Fact]
    public void SaveMatchesPreviousFormatAndOffsets()
    {
        var file = new Gxt2File
        {
            TextEntries = [
                new() { Hash = uint.MaxValue, Text = "café 模型 😀" },
                new() { Hash = 1, Text = "" },
                new() { Hash = 1, Text = "embedded\0null" },
                new() { Hash = 2, Text = "unpaired\ud800" },
                new() { Hash = 3, Text = null! }
            ]
        };
        var expected = PreviousSave(file.TextEntries);
        Assert.Equal(expected, file.Save());
        Assert.Equal(expected, file.Save()); // Saving again must not accumulate offsets.
        uint offset = 16 + (uint)file.TextEntries.Length * 8;
        foreach (var entry in file.TextEntries)
        {
            Assert.Equal(offset, entry.Offset);
            offset += (uint)Encoding.UTF8.GetByteCount(entry.Text + "\0");
        }
        Assert.Equal(PreviousSave([]), new Gxt2File().Save());
        Assert.Equal(PreviousSave([]), new Gxt2File { TextEntries = null! }.Save());
    }

    [Fact]
    public void SavedUnicodeTextLoadsAgain()
    {
        var original = Gxt2File.FromText("0x00000001 = café 模型 😀\n0x00000002 = plain");
        var loaded = new Gxt2File();
        loaded.Load(original.Save(), null);
        Assert.Equal(original.ToText(), loaded.ToText());
    }

    private static byte[] PreviousSave(Gxt2Entry[] entries)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        uint offset = 16 + (uint)entries.Length * 8;
        var strings = entries.Select(e => Encoding.UTF8.GetBytes(e.Text + "\0")).ToArray();
        writer.Write(1196971058);
        writer.Write((uint)entries.Length);
        for (int i = 0; i < entries.Length; i++)
        {
            writer.Write(entries[i].Hash);
            writer.Write(offset);
            offset += (uint)strings[i].Length;
        }
        writer.Write(1196971058);
        writer.Write(offset);
        foreach (var bytes in strings) writer.Write(bytes);
        writer.Flush();
        return stream.ToArray();
    }
}
