using System;
using System.Collections.Generic;

namespace CodeWalker.GameFiles
{
    //LOD hierarchy diagnostics. Each distinct message is only reported once, so these calls can sit
    //on hot paths (per-frame ymap linking) without spamming the log.
    public static class LodDiag
    {
        public static Action<string>? Log; //set by GameFileCache.Init, so messages land in the normal error log

        private static readonly HashSet<string> seen = new();

        public static void Report(string msg)
        {
            var log = Log;
            if (log == null) return;
            lock (seen)
            {
                if (!seen.Add(msg)) return;
            }
            log("[LOD] " + msg);
        }

        public static void Reset()
        {
            lock (seen) { seen.Clear(); }
        }
    }
}
