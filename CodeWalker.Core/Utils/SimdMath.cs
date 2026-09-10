using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CodeWalker.Core.Utils;

/// <summary>
/// SIMD-optimized mathematical operations for improved performance.
/// Uses System.Numerics.Vector and hardware intrinsics where available.
/// </summary>
public static class SimdMath
{
    /// <summary>
    /// Transforms an array of Vector3 positions by a matrix using SIMD operations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void TransformVector3Array(ReadOnlySpan<SharpDX.Vector3> source, Span<SharpDX.Vector3> destination, ref SharpDX.Matrix transform)
    {
        if (source.Length != destination.Length)
            throw new ArgumentException("Source and destination spans must have the same length");

        int i = 0;
        int simdLength = source.Length - (source.Length % System.Numerics.Vector<float>.Count);

        // Process vectors in SIMD batches
        if (System.Numerics.Vector.IsHardwareAccelerated && simdLength >= System.Numerics.Vector<float>.Count)
        {
            var m11 = new System.Numerics.Vector<float>(transform.M11);
            var m12 = new System.Numerics.Vector<float>(transform.M12);
            var m13 = new System.Numerics.Vector<float>(transform.M13);
            var m21 = new System.Numerics.Vector<float>(transform.M21);
            var m22 = new System.Numerics.Vector<float>(transform.M22);
            var m23 = new System.Numerics.Vector<float>(transform.M23);
            var m31 = new System.Numerics.Vector<float>(transform.M31);
            var m32 = new System.Numerics.Vector<float>(transform.M32);
            var m33 = new System.Numerics.Vector<float>(transform.M33);
            var m41 = new System.Numerics.Vector<float>(transform.M41);
            var m42 = new System.Numerics.Vector<float>(transform.M42);
            var m43 = new System.Numerics.Vector<float>(transform.M43);

            // Reuse scratch buffers; stackalloc inside the loop grows with input size.
            Span<float> xValues = stackalloc float[System.Numerics.Vector<float>.Count];
            Span<float> yValues = stackalloc float[System.Numerics.Vector<float>.Count];
            Span<float> zValues = stackalloc float[System.Numerics.Vector<float>.Count];
            Span<float> rxValues = stackalloc float[System.Numerics.Vector<float>.Count];
            Span<float> ryValues = stackalloc float[System.Numerics.Vector<float>.Count];
            Span<float> rzValues = stackalloc float[System.Numerics.Vector<float>.Count];

            for (; i < simdLength; i += System.Numerics.Vector<float>.Count)
            {
                // Load X, Y, Z components
                for (int j = 0; j < System.Numerics.Vector<float>.Count && (i + j) < source.Length; j++)
                {
                    xValues[j] = source[i + j].X;
                    yValues[j] = source[i + j].Y;
                    zValues[j] = source[i + j].Z;
                }

                var vx = new System.Numerics.Vector<float>(xValues);
                var vy = new System.Numerics.Vector<float>(yValues);
                var vz = new System.Numerics.Vector<float>(zValues);

                // Transform: result = M * v + translation
                var rx = m11 * vx + m21 * vy + m31 * vz + m41;
                var ry = m12 * vx + m22 * vy + m32 * vz + m42;
                var rz = m13 * vx + m23 * vy + m33 * vz + m43;

                // Store results
                rx.CopyTo(rxValues);
                ry.CopyTo(ryValues);
                rz.CopyTo(rzValues);

                for (int j = 0; j < System.Numerics.Vector<float>.Count && (i + j) < destination.Length; j++)
                {
                    destination[i + j] = new SharpDX.Vector3(rxValues[j], ryValues[j], rzValues[j]);
                }
            }
        }

        // Process remaining elements
        for (; i < source.Length; i++)
        {
            var v = source[i];
            destination[i] = new SharpDX.Vector3(
                transform.M11 * v.X + transform.M21 * v.Y + transform.M31 * v.Z + transform.M41,
                transform.M12 * v.X + transform.M22 * v.Y + transform.M32 * v.Z + transform.M42,
                transform.M13 * v.X + transform.M23 * v.Y + transform.M33 * v.Z + transform.M43
            );
        }
    }

    /// <summary>
    /// Multiplies an array of Vector3 by a scalar using SIMD operations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MultiplyVector3ArrayByScalar(ReadOnlySpan<SharpDX.Vector3> source, Span<SharpDX.Vector3> destination, float scalar)
    {
        if (source.Length != destination.Length)
            throw new ArgumentException("Source and destination spans must have the same length");

        // SharpDX.Vector3 stores three consecutive floats. Scalar multiplication does
        // not need to gather/scatter components into separate SIMD scratch buffers.
        var values = MemoryMarshal.Cast<SharpDX.Vector3, float>(source);
        var output = MemoryMarshal.Cast<SharpDX.Vector3, float>(destination);
        int i = 0;
        if (System.Numerics.Vector.IsHardwareAccelerated)
        {
            int width = System.Numerics.Vector<float>.Count;
            var multiplier = new System.Numerics.Vector<float>(scalar);
            for (; i <= values.Length - width; i += width)
            {
                var batch = new System.Numerics.Vector<float>(values.Slice(i, width));
                (batch * multiplier).CopyTo(output.Slice(i, width));
            }
        }
        for (; i < values.Length; i++) output[i] = values[i] * scalar;
    }

    /// <summary>
    /// Computes dot products for arrays of vectors using SIMD operations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DotProductArray(ReadOnlySpan<SharpDX.Vector3> a, ReadOnlySpan<SharpDX.Vector3> b, Span<float> results)
    {
        if (a.Length != b.Length || a.Length != results.Length)
            throw new ArgumentException("All spans must have the same length");

        int i = 0;
        int simdLength = a.Length - (a.Length % System.Numerics.Vector<float>.Count);

        if (System.Numerics.Vector.IsHardwareAccelerated && simdLength >= System.Numerics.Vector<float>.Count)
        {
            // Reuse scratch buffers; stackalloc inside the loop grows with input size.
            Span<float> ax = stackalloc float[System.Numerics.Vector<float>.Count];
            Span<float> ay = stackalloc float[System.Numerics.Vector<float>.Count];
            Span<float> az = stackalloc float[System.Numerics.Vector<float>.Count];
            Span<float> bx = stackalloc float[System.Numerics.Vector<float>.Count];
            Span<float> by = stackalloc float[System.Numerics.Vector<float>.Count];
            Span<float> bz = stackalloc float[System.Numerics.Vector<float>.Count];
            Span<float> dotValues = stackalloc float[System.Numerics.Vector<float>.Count];

            for (; i < simdLength; i += System.Numerics.Vector<float>.Count)
            {
                for (int j = 0; j < System.Numerics.Vector<float>.Count && (i + j) < a.Length; j++)
                {
                    ax[j] = a[i + j].X;
                    ay[j] = a[i + j].Y;
                    az[j] = a[i + j].Z;
                    bx[j] = b[i + j].X;
                    by[j] = b[i + j].Y;
                    bz[j] = b[i + j].Z;
                }

                var vax = new System.Numerics.Vector<float>(ax);
                var vay = new System.Numerics.Vector<float>(ay);
                var vaz = new System.Numerics.Vector<float>(az);
                var vbx = new System.Numerics.Vector<float>(bx);
                var vby = new System.Numerics.Vector<float>(by);
                var vbz = new System.Numerics.Vector<float>(bz);

                var dot = vax * vbx + vay * vby + vaz * vbz;

                dot.CopyTo(dotValues);

                for (int j = 0; j < System.Numerics.Vector<float>.Count && (i + j) < results.Length; j++)
                {
                    results[i + j] = dotValues[j];
                }
            }
        }

        for (; i < a.Length; i++)
        {
            results[i] = SharpDX.Vector3.Dot(a[i], b[i]);
        }
    }

    /// <summary>
    /// Optimized frustum plane distance calculations using SIMD.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FrustumPlaneDistances(ReadOnlySpan<SharpDX.Vector3> points, ref SharpDX.Plane plane, Span<float> distances)
    {
        if (points.Length != distances.Length)
            throw new ArgumentException("Points and distances spans must have the same length");

        int i = 0;
        int simdLength = points.Length - (points.Length % System.Numerics.Vector<float>.Count);

        if (System.Numerics.Vector.IsHardwareAccelerated && simdLength >= System.Numerics.Vector<float>.Count)
        {
            var nx = new System.Numerics.Vector<float>(plane.Normal.X);
            var ny = new System.Numerics.Vector<float>(plane.Normal.Y);
            var nz = new System.Numerics.Vector<float>(plane.Normal.Z);
            var d = new System.Numerics.Vector<float>(plane.D);

            // Reuse scratch buffers; stackalloc inside the loop grows with input size.
            Span<float> px = stackalloc float[System.Numerics.Vector<float>.Count];
            Span<float> py = stackalloc float[System.Numerics.Vector<float>.Count];
            Span<float> pz = stackalloc float[System.Numerics.Vector<float>.Count];
            Span<float> distValues = stackalloc float[System.Numerics.Vector<float>.Count];

            for (; i < simdLength; i += System.Numerics.Vector<float>.Count)
            {
                for (int j = 0; j < System.Numerics.Vector<float>.Count && (i + j) < points.Length; j++)
                {
                    px[j] = points[i + j].X;
                    py[j] = points[i + j].Y;
                    pz[j] = points[i + j].Z;
                }

                var vpx = new System.Numerics.Vector<float>(px);
                var vpy = new System.Numerics.Vector<float>(py);
                var vpz = new System.Numerics.Vector<float>(pz);

                var dist = vpx * nx + vpy * ny + vpz * nz + d;

                dist.CopyTo(distValues);

                for (int j = 0; j < System.Numerics.Vector<float>.Count && (i + j) < distances.Length; j++)
                {
                    distances[i + j] = distValues[j];
                }
            }
        }

        for (; i < points.Length; i++)
        {
            distances[i] = SharpDX.Plane.DotCoordinate(plane, points[i]);
        }
    }

    /// <summary>
    /// Optimized bounding box containment test using SIMD for multiple boxes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AABBContainsPoint(ReadOnlySpan<SharpDX.Vector3> points, ref SharpDX.Vector3 min, ref SharpDX.Vector3 max, Span<bool> results)
    {
        if (points.Length != results.Length)
            throw new ArgumentException("Points and results spans must have the same length");

        for (int i = 0; i < points.Length; i++)
        {
            var p = points[i];
            results[i] = p.X >= min.X && p.X <= max.X &&
                        p.Y >= min.Y && p.Y <= max.Y &&
                        p.Z >= min.Z && p.Z <= max.Z;
        }
    }

    /// <summary>
    /// Optimized vector normalization for arrays using SIMD.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NormalizeVector3Array(ReadOnlySpan<SharpDX.Vector3> source, Span<SharpDX.Vector3> destination)
    {
        if (source.Length != destination.Length)
            throw new ArgumentException("Source and destination spans must have the same length");

        for (int i = 0; i < source.Length; i++)
        {
            var v = source[i];
            var lengthSq = v.X * v.X + v.Y * v.Y + v.Z * v.Z;
            if (lengthSq > 0)
            {
                var invLength = 1.0f / MathF.Sqrt(lengthSq);
                destination[i] = new SharpDX.Vector3(v.X * invLength, v.Y * invLength, v.Z * invLength);
            }
            else
            {
                destination[i] = SharpDX.Vector3.Zero;
            }
        }
    }

    /// <summary>
    /// Optimized color blending using SIMD operations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void BlendColors(ReadOnlySpan<SharpDX.Color4> colors1, ReadOnlySpan<SharpDX.Color4> colors2, Span<SharpDX.Color4> results, float blend)
    {
        if (colors1.Length != colors2.Length || colors1.Length != results.Length)
            throw new ArgumentException("All spans must have the same length");

        int i = 0;
        int simdLength = colors1.Length - (colors1.Length % System.Numerics.Vector<float>.Count);

        if (System.Numerics.Vector.IsHardwareAccelerated && simdLength >= System.Numerics.Vector<float>.Count)
        {
            var blendVec = new System.Numerics.Vector<float>(blend);
            var oneMinusBlend = new System.Numerics.Vector<float>(1.0f - blend);

            // Reuse scratch buffers; stackalloc inside the loop grows with input size.
            Span<float> r1 = stackalloc float[System.Numerics.Vector<float>.Count];
            Span<float> g1 = stackalloc float[System.Numerics.Vector<float>.Count];
            Span<float> b1 = stackalloc float[System.Numerics.Vector<float>.Count];
            Span<float> a1 = stackalloc float[System.Numerics.Vector<float>.Count];
            Span<float> r2 = stackalloc float[System.Numerics.Vector<float>.Count];
            Span<float> g2 = stackalloc float[System.Numerics.Vector<float>.Count];
            Span<float> b2 = stackalloc float[System.Numerics.Vector<float>.Count];
            Span<float> a2 = stackalloc float[System.Numerics.Vector<float>.Count];
            Span<float> rrValues = stackalloc float[System.Numerics.Vector<float>.Count];
            Span<float> rgValues = stackalloc float[System.Numerics.Vector<float>.Count];
            Span<float> rbValues = stackalloc float[System.Numerics.Vector<float>.Count];
            Span<float> raValues = stackalloc float[System.Numerics.Vector<float>.Count];

            for (; i < simdLength; i += System.Numerics.Vector<float>.Count)
            {
                for (int j = 0; j < System.Numerics.Vector<float>.Count && (i + j) < colors1.Length; j++)
                {
                    r1[j] = colors1[i + j].Red;
                    g1[j] = colors1[i + j].Green;
                    b1[j] = colors1[i + j].Blue;
                    a1[j] = colors1[i + j].Alpha;
                    r2[j] = colors2[i + j].Red;
                    g2[j] = colors2[i + j].Green;
                    b2[j] = colors2[i + j].Blue;
                    a2[j] = colors2[i + j].Alpha;
                }

                var vr1 = new System.Numerics.Vector<float>(r1);
                var vg1 = new System.Numerics.Vector<float>(g1);
                var vb1 = new System.Numerics.Vector<float>(b1);
                var va1 = new System.Numerics.Vector<float>(a1);
                var vr2 = new System.Numerics.Vector<float>(r2);
                var vg2 = new System.Numerics.Vector<float>(g2);
                var vb2 = new System.Numerics.Vector<float>(b2);
                var va2 = new System.Numerics.Vector<float>(a2);

                var rr = vr1 * oneMinusBlend + vr2 * blendVec;
                var rg = vg1 * oneMinusBlend + vg2 * blendVec;
                var rb = vb1 * oneMinusBlend + vb2 * blendVec;
                var ra = va1 * oneMinusBlend + va2 * blendVec;

                rr.CopyTo(rrValues);
                rg.CopyTo(rgValues);
                rb.CopyTo(rbValues);
                ra.CopyTo(raValues);

                for (int j = 0; j < System.Numerics.Vector<float>.Count && (i + j) < results.Length; j++)
                {
                    results[i + j] = new SharpDX.Color4(rrValues[j], rgValues[j], rbValues[j], raValues[j]);
                }
            }
        }

        for (; i < colors1.Length; i++)
        {
            var c1 = colors1[i];
            var c2 = colors2[i];
            results[i] = new SharpDX.Color4(
                c1.Red * (1.0f - blend) + c2.Red * blend,
                c1.Green * (1.0f - blend) + c2.Green * blend,
                c1.Blue * (1.0f - blend) + c2.Blue * blend,
                c1.Alpha * (1.0f - blend) + c2.Alpha * blend
            );
        }
    }
}
