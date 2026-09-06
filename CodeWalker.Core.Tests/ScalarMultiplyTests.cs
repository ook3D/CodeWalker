using CodeWalker.Core.Utils;
using SharpDX;
using Xunit;

namespace CodeWalker.Core.Tests;

public class ScalarMultiplyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(17)]
    public void PackedMultiplyMatchesScalarBitsForSlicesAndInPlace(int count)
    {
        float[] values = [0f, -0f, float.Epsilon, -float.Epsilon, float.MaxValue, float.PositiveInfinity, float.NegativeInfinity, float.NaN, 1.25f];
        foreach (float multiplier in new[] { 0f, -0f, 0.5f, -2f, float.PositiveInfinity })
        {
            var source = Enumerable.Range(0, count + 2).Select(i => new Vector3(values[i % values.Length], values[(i + 1) % values.Length], values[(i + 2) % values.Length])).ToArray();
            var output = Enumerable.Repeat(new Vector3(42), count + 2).ToArray();
            var expected = source.Skip(1).Take(count).Select(v => v * multiplier).ToArray();
            SimdMath.MultiplyVector3ArrayByScalar(source.AsSpan(1, count), output.AsSpan(1, count), multiplier);
            Assert.Equal(new Vector3(42), output[0]);
            Assert.Equal(new Vector3(42), output[^1]);
            SimdMath.MultiplyVector3ArrayByScalar(source.AsSpan(1, count), source.AsSpan(1, count), multiplier);
            for (int i = 0; i < count; i++)
            {
                Check(expected[i], output[i + 1]);
                Check(expected[i], source[i + 1]);
            }
        }
    }

    private static void Check(Vector3 expected, Vector3 actual)
    {
        for (int i = 0; i < 3; i++)
        {
            if (float.IsNaN(expected[i])) Assert.True(float.IsNaN(actual[i]));
            else Assert.Equal(BitConverter.SingleToInt32Bits(expected[i]), BitConverter.SingleToInt32Bits(actual[i]));
        }
    }
}
