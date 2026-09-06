using System.Security.Cryptography;
using System.Text;
using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class CryptoAndStreamTests
{
    [Fact]
    public void Aes256MatchesNistEcbVector()
    {
        byte[] key = Convert.FromHexString("603deb1015ca71be2b73aef0857d77811f352c073b6108d72d9810a30914dff4");
        byte[] plaintext = Convert.FromHexString("6bc1bee22e409f96e93d7e117393172a");
        byte[] ciphertext = Convert.FromHexString("f3eed1bdb5d2a03c064b5a7e3db181f8");

        Assert.Equal(ciphertext, GTACrypto.EncryptAESData(plaintext, key));
        Assert.Equal(plaintext, GTACrypto.DecryptAESData(ciphertext, key));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(7, 1)]
    [InlineData(32, 0)]
    [InlineData(32, 1)]
    [InlineData(39, 3)]
    public void AesPreservesTrailingBytesAndDoesNotMutateInput(int length, int rounds)
    {
        byte[] key = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        byte[] plaintext = Enumerable.Range(0, length).Select(i => (byte)(i * 7)).ToArray();
        byte[] original = (byte[])plaintext.Clone();

        byte[] encrypted = GTACrypto.EncryptAESData(plaintext, key, rounds);

        Assert.Equal(original, plaintext);
        Assert.NotSame(plaintext, encrypted);
        Assert.Equal(plaintext.AsSpan(length - length % 16).ToArray(), encrypted.AsSpan(length - length % 16).ToArray());
        Assert.Equal(plaintext, GTACrypto.DecryptAESData(encrypted, key, rounds));
    }

    [Fact]
    public void BinaryFbxDetectionHandlesPartialReadsAndResetsPosition()
    {
        byte[] header = Encoding.ASCII.GetBytes("Kaydara FBX Binary  \0\x1a\0");
        using var stream = new ShortReadStream(header);

        Assert.True(FbxBinary.IsBinary(stream));
        Assert.Equal(0, stream.Position);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(22)]
    public void TruncatedFbxHeaderIsNotBinary(int length)
    {
        byte[] header = Encoding.ASCII.GetBytes("Kaydara FBX Binary  \0\x1a\0");
        using var stream = new ShortReadStream(header[..length]);

        Assert.False(FbxBinary.IsBinary(stream));
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void HashSearchReadsCompleteCandidatesAndSkipsTruncatedTail()
    {
        // The scanner processes one-megabyte blocks at eight-byte alignments.
        byte[] data = new byte[1024 * 1024];
        byte[] key = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
        key.CopyTo(data, data.Length - key.Length);
        using var stream = new ShortReadStream(data);

        byte[] result = HashSearch.SearchHash(stream, SHA1.HashData(key));

        Assert.Equal(key, result);
    }

    [Fact]
    public void FbxVertexEqualityHandlesAnUninitializedPeer()
    {
        var empty = new FbxVertex();
        var populated = new FbxVertex { Bytes = [1, 2, 3] };

        Assert.False(populated.Equals(empty));
        Assert.False(empty.Equals(populated));
        Assert.False(populated.Equals((FbxVertex?)null));
        Assert.True(populated.Equals(new FbxVertex { Bytes = [1, 2, 3] }));
    }

    private sealed class ShortReadStream(byte[] data) : MemoryStream(data)
    {
        public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, 7));
        public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(buffer.Length, 7)]);
    }
}
