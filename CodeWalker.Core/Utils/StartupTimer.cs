using System;
using System.Diagnostics;
using System.IO;
using System.Runtime;

namespace CodeWalker
{
    /// <summary>
    /// Opt-in startup timing. Enabled only when the CW_STARTUP_LOG env var is set to a file path.
    /// When disabled every call is a cheap no-op.
    /// </summary>
    public static class StartupTimer
    {
        private static readonly string LogPath = Environment.GetEnvironmentVariable("CW_STARTUP_LOG");
        private static readonly bool Enabled = !string.IsNullOrEmpty(LogPath);
        private static readonly object Sync = new object();
        private static bool started;
        private static long processStartTick; //TickCount64 value corresponding to process start
        private static long lastTick;

        public static void Mark(string label)
        {
            if (!Enabled)
            {
                return;
            }

            try
            {
                lock (Sync)
                {
                    long now = Environment.TickCount64;
                    if (!started)
                    {
                        started = true;
                        double sinceStart = (DateTime.Now - Process.GetCurrentProcess().StartTime).TotalMilliseconds;
                        processStartTick = now - (long)sinceStart;
                        lastTick = processStartTick;
#if DEBUG
                        string build = "Debug";
#else
                        string build = "Release";
#endif
                        File.AppendAllText(LogPath, "# ServerGC=" + GCSettings.IsServerGC + " LatencyMode=" + GCSettings.LatencyMode + " Build=" + build + " DebuggerAttached=" + Debugger.IsAttached + Environment.NewLine);
                    }

                    long total = now - processStartTick;
                    long delta = now - lastTick;
                    lastTick = now;
                    double allocMb = GC.GetTotalAllocatedBytes(false) / (1024.0 * 1024.0);
                    string line = label + "\t" + total + "\t" + delta + "\t" + allocMb.ToString("F1") + "\t"
                        + GC.CollectionCount(0) + "/" + GC.CollectionCount(1) + "/" + GC.CollectionCount(2) + Environment.NewLine;
                    File.AppendAllText(LogPath, line); //opens, writes and closes each call, so every line is flushed
                }
            }
            catch
            {
            }
        }
    }
}
