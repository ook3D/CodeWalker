using CodeWalker.Core.Utils;
using SharpDX;
using Xunit;

namespace CodeWalker.Core.Tests;

public class SimdMathTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(17)]
    [InlineData(100003)]
    public void BatchedOperationsMatchScalarResults(int count)
    {
        var source = Enumerable.Range(0, count).Select(i => new Vector3(i % 7, i % 11 - 5, i % 3)).ToArray();
        var other = source.Select(v => v + Vector3.One).ToArray();
        var destination = new Vector3[count];
        var scalars = new float[count];
        var transform = Matrix.Scaling(2, 3, 4) * Matrix.Translation(5, 6, 7);
        SimdMath.TransformVector3Array(source, destination, ref transform);
        for (int i = 0; i < count; i++) Assert.Equal(Vector3.TransformCoordinate(source[i], transform), destination[i]);
        SimdMath.MultiplyVector3ArrayByScalar(source, destination, 0.5f);
        for (int i = 0; i < count; i++) Assert.Equal(source[i] * 0.5f, destination[i]);
        SimdMath.DotProductArray(source, other, scalars);
        for (int i = 0; i < count; i++) Assert.Equal(Vector3.Dot(source[i], other[i]), scalars[i]);
        var plane = new Plane(2, 3, 4, 5);
        SimdMath.FrustumPlaneDistances(source, ref plane, scalars);
        for (int i = 0; i < count; i++) Assert.Equal(Plane.DotCoordinate(plane, source[i]), scalars[i]);
        var colors = source.Select(v => new Color4(v.X, v.Y, v.Z, 1)).ToArray();
        var colors2 = other.Select(v => new Color4(v.X, v.Y, v.Z, 0)).ToArray();
        var blended = new Color4[count];
        SimdMath.BlendColors(colors, colors2, blended, 0.25f);
        for (int i = 0; i < count; i++) Assert.Equal(colors[i] * 0.75f + colors2[i] * 0.25f, blended[i]);

        // Exact in-place operations must also work across SIMD batches and the scalar tail.
        SimdMath.TransformVector3Array(source, source, ref transform);
        SimdMath.MultiplyVector3ArrayByScalar(source, source, 0.5f);
        for (int i = 0; i < count; i++)
            Assert.Equal(Vector3.TransformCoordinate(other[i] - Vector3.One, transform) * 0.5f, source[i]);
    }
}
