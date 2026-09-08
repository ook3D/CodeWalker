using System;
using System.Collections.Generic;
using CodeWalker.GameFiles;
using SharpDX;

namespace CodeWalker.Rendering
{
    // Loads common:/data/effects/ptxclipregions.dat - the per-texture sprite-sheet frame UV rects.
    // Without this, atlas frames can only be guessed (square grid), which is wrong for most sheets.
    public static class ParticleClipRegions
    {
        public class ClipRegion
        {
            public int Cols;
            public int Rows;
            public Vector4[] Frames = []; //each = (uMin, vMin, uMax, vMax) ready for ParticleInstance.UVRect
        }

        static Dictionary<uint, ClipRegion>? regions;
        static bool attempted;

        public static void EnsureLoaded(GameFileCache? gfc)
        {
            if (attempted) return;
            attempted = true;
            regions = new Dictionary<uint, ClipRegion>();
            try
            {
                var rpfman = gfc?.RpfMan;
                if (rpfman == null) return;
                string txt = rpfman.GetFileUTF8Text("common:/data/effects/ptxclipregions.dat");
                if (string.IsNullOrEmpty(txt)) return;
                Parse(txt, regions);
            }
            catch { }
        }

        internal static void Parse(string txt, Dictionary<uint, ClipRegion> destination)
        {
            ReadOnlySpan<char> text = txt.AsSpan();
            var tokens = text.SplitAny(" \t\r\n");
            Span<Range> fields = stackalloc Range[4];
            if (!TryReadFields(ref tokens, text, fields[..2])) return;
            int numTextures = ParseInt(text[fields[0]]);
            for (int t = 0; t < numTextures && TryReadFields(ref tokens, text, fields[..3]); t++)
            {
                var name = text[fields[0]];
                int cols = ParseInt(text[fields[1]]);
                int rows = ParseInt(text[fields[2]]);
                int frames = Math.Max(0, cols * rows);
                var cr = new ClipRegion { Cols = cols, Rows = rows, Frames = new Vector4[frames] };
                for (int f = 0; f < frames && TryReadFields(ref tokens, text, fields); f++)
                {
                    cr.Frames[f] = new Vector4(ParseF(text[fields[0]]), ParseF(text[fields[2]]),
                        ParseF(text[fields[1]]), ParseF(text[fields[3]]));
                }
                destination[JenkHash.GenHashLowerInvariant(name)] = cr;
            }
        }

        // Commit only a complete group, preserving the old parser's truncated-input behavior.
        private static bool TryReadFields(ref MemoryExtensions.SpanSplitEnumerator<char> tokens,
            ReadOnlySpan<char> text, scoped Span<Range> fields)
        {
            var next = tokens;
            int count = 0;
            while (next.MoveNext())
            {
                if (text[next.Current].IsEmpty) continue;
                fields[count++] = next.Current;
                if (count == fields.Length)
                {
                    tokens = next;
                    return true;
                }
            }
            return false;
        }

        public static ClipRegion? Get(uint texHash)
        {
            if ((regions != null) && (texHash != 0) && regions.TryGetValue(texHash, out var c)) return c;
            return null;
        }

        static int ParseInt(ReadOnlySpan<char> s) { return int.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int v) ? v : 0; }
        static float ParseF(ReadOnlySpan<char> s) { return float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 0f; }
    }


}
