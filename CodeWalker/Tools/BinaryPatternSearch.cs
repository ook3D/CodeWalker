using System;
using System.Runtime.CompilerServices;

namespace CodeWalker.Tools
{
    internal static class BinaryPatternSearch
    {
        internal static MatchEnumerator FindMatches(ReadOnlySpan<byte> data, ReadOnlySpan<byte> pattern) =>
            new(data, pattern);

        // Search lazily so callers can report results (or stop) without a result list.
        internal ref struct MatchEnumerator
        {
            private readonly ReadOnlySpan<byte> data;
            private readonly ReadOnlySpan<byte> pattern;
            private readonly int[]? skips;
            private int offset;

            internal MatchEnumerator(ReadOnlySpan<byte> data, ReadOnlySpan<byte> pattern)
            {
                this.data = data;
                this.pattern = pattern;
                skips = null;
                // Long needles benefit from Horspool's larger skips. Short needles use
                // the runtime's vectorized span search without a preprocessing table.
                if (pattern.Length >= 128 && data.Length >= pattern.Length)
                {
                    skips = new int[256];
                    skips.AsSpan().Fill(pattern.Length);
                    for (int i = 0; i < pattern.Length - 1; i++)
                        skips[pattern[i]] = pattern.Length - 1 - i;
                }
                offset = 0;
                Current = -1;
            }

            public int Current { get; private set; }
            public MatchEnumerator GetEnumerator() => this;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                if (pattern.IsEmpty || data.Length - offset < pattern.Length) return false;
                if (skips != null) return MoveNextLong(skips);
                // Avoid re-entering IndexOf for every match in dense/repeated input.
                if (data[offset..].StartsWith(pattern))
                {
                    Current = offset;
                    offset += pattern.Length;
                    return true;
                }
                int relative = data[offset..].IndexOf(pattern);
                if (relative < 0)
                {
                    offset = data.Length;
                    return false;
                }
                Current = offset + relative;
                // Preserve the existing non-overlapping match convention.
                offset = Current + pattern.Length;
                return true;
            }
            private bool MoveNextLong(int[] skips)
            {
                int last = data.Length - pattern.Length;
                while (offset <= last)
                {
                    int j = pattern.Length - 1;
                    while (j >= 0 && pattern[j] == data[offset + j]) j--;
                    if (j < 0)
                    {
                        Current = offset;
                        offset += pattern.Length;
                        return true;
                    }
                    int advance = skips[data[offset + pattern.Length - 1]];
                    if (advance > last - offset) break;
                    offset += advance;
                }
                offset = data.Length;
                return false;
            }

        }

        internal static void CopyLowercaseAscii(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            if (destination.Length < source.Length) throw new ArgumentException("Destination is too short.", nameof(destination));
            int i = 0;
            if (System.Numerics.Vector.IsHardwareAccelerated)
            {
                int width = System.Numerics.Vector<byte>.Count;
                var lower = new System.Numerics.Vector<byte>((byte)'A');
                var upper = new System.Numerics.Vector<byte>((byte)'Z');
                var delta = new System.Numerics.Vector<byte>(32);
                for (; i <= source.Length - width; i += width)
                {
                    var bytes = new System.Numerics.Vector<byte>(source.Slice(i, width));
                    var uppercase = System.Numerics.Vector.GreaterThanOrEqual(bytes, lower)
                        & System.Numerics.Vector.LessThanOrEqual(bytes, upper);
                    (bytes + (uppercase & delta)).CopyTo(destination.Slice(i, width));
                }
            }
            for (; i < source.Length; i++)
            {
                byte value = source[i];
                destination[i] = value >= 'A' && value <= 'Z' ? (byte)(value + 32) : value;
            }
        }
    }
}
