using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace CodeWalker.Numerics
{
    /// <summary>
    /// Bit-exact C# port of RAGE's <c>Vec::Vector_4V</c> SSE engine
    /// (rage/base/src/vectormath, win32pc core). Every op maps 1:1 to the same
    /// SSE instruction the game compiles, so results match RAGE to the bit -
    /// which SharpDX does not (it uses scalar math + 1/sqrt, diverging by ~1 ULP
    /// and occasionally flipping the x10-truncated LOD-light hash).
    ///
    /// Vector_4V == __m128 == Vector128&lt;float&gt;. Lane order is x,y,z,w.
    /// Requires SSE/SSE2 (baseline on every x64 CPU); guarded so a non-x86 JIT
    /// throws loudly rather than silently returning wrong bits.
    /// </summary>
    public static class Vec
    {
        // _MM_SHUFFLE(d,c,b,a): result lane i takes source lane (bits[2i..2i+1]).
        private const byte SHUF_YXWZ = 0xB1; // [y,x,w,z]  RAGE V4Permute<Y,X,W,Z>
        private const byte SHUF_ZWXY = 0x4E; // [z,w,x,y]  RAGE V4Permute<Z,W,X,Y>
        private const byte SHUF_YZXW = 0xC9; // [y,z,x,w]
        private const byte SHUF_ZXYW = 0xD2; // [z,x,y,w]
        private const byte SPLAT_X = 0x00, SPLAT_Y = 0x55, SPLAT_Z = 0xAA, SPLAT_W = 0xFF;

        // Constants (RAGE V4VConstant equivalents).
        private static readonly Vector128<float> SignMask = Vector128.Create(0x80000000u).AsSingle();
        private static readonly Vector128<float> MaskX = Vector128.Create(0xFFFFFFFFu, 0, 0, 0).AsSingle();
        private static readonly Vector128<float> MaskZW = Vector128.Create(0u, 0, 0xFFFFFFFFu, 0xFFFFFFFFu).AsSingle();
        private static readonly Vector128<float> One = Vector128.Create(1f);
        private static readonly Vector128<float> Half = Vector128.Create(0.5f);

        // ponytail: SSE2 is baseline on all x64. Sse.* throw PlatformNotSupportedException
        // themselves on a non-x86 JIT, so no explicit guard needed.

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> Set(float x, float y, float z, float w) => Vector128.Create(x, y, z, w);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetX(Vector128<float> v) => v.ToScalar();

        // --- basic arithmetic (V4Add / V4Subtract / V4Scale) ---
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> Add(Vector128<float> a, Vector128<float> b) => Sse.Add(a, b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> Subtract(Vector128<float> a, Vector128<float> b) => Sse.Subtract(a, b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> Scale(Vector128<float> a, Vector128<float> b) => Sse.Multiply(a, b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> Divide(Vector128<float> a, Vector128<float> b) => Sse.Divide(a, b);

        // V4AddScaled(a,b,c) = a + b*c ; V4SubtractScaled(a,b,c) = a - b*c
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> AddScaled(Vector128<float> a, Vector128<float> b, Vector128<float> c) => Sse.Add(a, Sse.Multiply(b, c));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> SubtractScaled(Vector128<float> a, Vector128<float> b, Vector128<float> c) => Sse.Subtract(a, Sse.Multiply(b, c));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> Negate(Vector128<float> v) => Sse.Subtract(Vector128<float>.Zero, v); // V4Negate: 0 - v

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> Abs(Vector128<float> v) => Sse.AndNot(SignMask, v); // V4Abs: clear sign bits

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> Min(Vector128<float> a, Vector128<float> b) => Sse.Min(a, b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> Max(Vector128<float> a, Vector128<float> b) => Sse.Max(a, b);

        // --- permutes / splats / merges ---
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> SplatX(Vector128<float> v) => Sse.Shuffle(v, v, SPLAT_X);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> SplatY(Vector128<float> v) => Sse.Shuffle(v, v, SPLAT_Y);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> SplatZ(Vector128<float> v) => Sse.Shuffle(v, v, SPLAT_Z);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> SplatW(Vector128<float> v) => Sse.Shuffle(v, v, SPLAT_W);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> MergeXY(Vector128<float> a, Vector128<float> b) => Sse.UnpackLow(a, b);  // [ax,bx,ay,by]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> MergeZW(Vector128<float> a, Vector128<float> b) => Sse.UnpackHigh(a, b); // [az,bz,aw,bw]

        // V4SelectFT(control, f, t): lane = (control bit set) ? t : f
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> SelectFT(Vector128<float> control, Vector128<float> f, Vector128<float> t)
            => Sse.Or(Sse.AndNot(control, f), Sse.And(control, t));

        // --- dot products (RAGE shuffle-add tree; NOT dpps, for cross-platform determinism) ---
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> Dot4V(Vector128<float> a, Vector128<float> b)
        {
            var m = Sse.Multiply(a, b);
            var t = Sse.Add(m, Sse.Shuffle(m, m, SHUF_YXWZ));
            return Sse.Add(t, Sse.Shuffle(t, t, SHUF_ZWXY));
        }

        // V3DotV: mul then Add(Add(splatX, splatY), splatZ)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> Dot3V(Vector128<float> a, Vector128<float> b)
        {
            var m = Sse.Multiply(a, b);
            return Sse.Add(Sse.Add(SplatX(m), SplatY(m)), SplatZ(m));
        }

        public static float Dot3(Vector128<float> a, Vector128<float> b) => GetX(Dot3V(a, b));
        public static float Dot4(Vector128<float> a, Vector128<float> b) => GetX(Dot4V(a, b));

        // --- sqrt / invsqrt (the ULP-critical RAGE sequences) ---
        // V4Sqrt on Intel is the direct sqrtps intrinsic.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> Sqrt(Vector128<float> a) => Sse.Sqrt(a);

        // V4InvSqrt = rsqrtps estimate + exactly ONE Newton-Raphson refine:
        //   y1 = y0 + 0.5*y0*(1 - x*y0*y0)  ==  AddScaled(y0, y0, SubtractScaled(half, half, x*(y0*y0)))
        public static Vector128<float> InvSqrt(Vector128<float> x)
        {
            var y0 = Sse.ReciprocalSqrt(x); // _mm_rsqrt_ps
            var y0y0 = Sse.Multiply(y0, y0);
            var inner = SubtractScaled(Half, Half, Sse.Multiply(x, y0y0));
            return AddScaled(y0, y0, inner);
        }

        // --- magnitude / normalize ---
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> Mag3SquaredV(Vector128<float> v) => Dot3V(v, v);

        // V3Normalize = v * V3InvMag(v), InvMag = InvSqrt(MagSquared)
        public static Vector128<float> Normalize3(Vector128<float> v) => Sse.Multiply(v, InvSqrt(Dot3V(v, v)));
        public static Vector128<float> Normalize4(Vector128<float> v) => Sse.Multiply(v, InvSqrt(Dot4V(v, v)));

        public static float Mag3(Vector128<float> v) => GetX(Sqrt(Dot3V(v, v)));

        /// <summary>
        /// RAGE Transform_Imp34 (PC branch): transform a point by a 3x4 matrix given
        /// as basis columns c0,c1,c2 and translation c3. Accumulation order is verbatim:
        ///   r = c3 + x*c0 ; r += y*c1 ; r += z*c2
        /// </summary>
        public static Vector128<float> Transform34(Vector128<float> c0, Vector128<float> c1, Vector128<float> c2, Vector128<float> c3, Vector128<float> point)
        {
            var r = AddScaled(c3, SplatX(point), c0);
            r = AddScaled(r, SplatY(point), c1);
            r = AddScaled(r, SplatZ(point), c2);
            return r;
        }

        /// <summary>3x3 rotate only (no translation): x*c0 + y*c1 + z*c2 (used for extents/AABB half-size).</summary>
        public static Vector128<float> Transform33(Vector128<float> c0, Vector128<float> c1, Vector128<float> c2, Vector128<float> v)
        {
            var r = Sse.Multiply(SplatX(v), c0);
            r = AddScaled(r, SplatY(v), c1);
            r = AddScaled(r, SplatZ(v), c2);
            return r;
        }

        /// <summary>
        /// RAGE Mat33VFromQuatV: build rotation basis columns from a quaternion (x,y,z,w).
        /// Verbatim select/permute sequence so the resulting matrix matches the game bit-for-bit.
        /// </summary>
        public static void Mat33FromQuat(Vector128<float> q, out Vector128<float> col0, out Vector128<float> col1, out Vector128<float> col2)
        {
            var xyzwSq = Sse.Add(q, q);               // 2*q
            var wwww = SplatW(q);
            var yzxw = Sse.Shuffle(q, q, SHUF_YZXW);
            var zxyw = Sse.Shuffle(q, q, SHUF_ZXYW);
            var yzxwSq = Sse.Shuffle(xyzwSq, xyzwSq, SHUF_YZXW);
            var zxywSq = Sse.Shuffle(xyzwSq, xyzwSq, SHUF_ZXYW);

            var t0 = Sse.Multiply(yzxwSq, wwww);
            var t1 = SubtractScaled(One, yzxwSq, yzxw);
            var t2 = Sse.Multiply(yzxw, xyzwSq);
            t0 = AddScaled(t0, zxyw, xyzwSq);
            t1 = SubtractScaled(t1, zxywSq, zxyw);
            t2 = SubtractScaled(t2, wwww, zxywSq);
            var t3 = SelectFT(MaskX, t0, t1);
            var t4 = SelectFT(MaskX, t1, t2);
            var t5 = SelectFT(MaskX, t2, t0);

            col0 = SelectFT(MaskZW, t3, t2);
            col1 = SelectFT(MaskZW, t4, t0);
            col2 = SelectFT(MaskZW, t5, t1);
        }
    }
}
