using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class NgDecryptTests
{
    // GTACrypto.DecryptNG runs the NG rounds in place; it must stay bit-identical to the per-block reference
    // (DecryptNGBlock) that it replaced, including the untouched trailing partial block.
    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(1000)]
    [InlineData(4099)]
    public void DecryptNGMatchesPerBlockReference(int length)
    {
        var rng = new Random(1234 + length);
        var saved = GTA5Keys.PC_NG_DECRYPT_TABLES;
        try
        {
            var tables = new uint[17][][];
            for (int i = 0; i < 17; i++)
            {
                tables[i] = new uint[16][];
                for (int j = 0; j < 16; j++)
                {
                    tables[i][j] = new uint[256];
                    for (int k = 0; k < 256; k++)
                    {
                        tables[i][j][k] = (uint)rng.NextInt64(0, 1L << 32);
                    }
                }
            }
            GTA5Keys.PC_NG_DECRYPT_TABLES = tables;

            byte[] key = new byte[272];
            rng.NextBytes(key);
            byte[] data = new byte[length];
            rng.NextBytes(data);
            byte[] original = (byte[])data.Clone();

            var keyuints = new uint[key.Length / 4];
            Buffer.BlockCopy(key, 0, keyuints, 0, key.Length);
            var expected = (byte[])data.Clone();
            for (int offset = 0; offset + 16 <= length; offset += 16)
            {
                var block = GTACrypto.DecryptNGBlock(data.AsSpan(offset, 16).ToArray(), keyuints);
                Buffer.BlockCopy(block, 0, expected, offset, 16);
            }

            Assert.Equal(expected, GTACrypto.DecryptNG(data, key));
            Assert.Equal(original, data);
        }
        finally
        {
            GTA5Keys.PC_NG_DECRYPT_TABLES = saved;
        }
    }
}
