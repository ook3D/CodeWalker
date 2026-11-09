using CodeWalker.GameFiles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

// Headless startup profiler for CodeWalker's game-file loading.
// Measures the RPF scan (sequential vs parallel) and each GameFileCache init phase,
// with wall-clock time and managed allocations per phase.

static class Profiler
{
    static string Folder;
    static bool Gen9;

    static int Main(string[] args)
    {
        if (args.Length > 1 && args[0] == "--world")
        {
            return RunWorld(args);
        }

        Folder = args.FirstOrDefault(a => Directory.Exists(a))
                 ?? @"C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V";
        Folder = Folder.TrimEnd('\\');
        Gen9 = File.Exists(Path.Combine(Folder, "gta5_enhanced.exe")) &&
               !File.Exists(Path.Combine(Folder, "gta5.exe"));

        Console.WriteLine($"GTA folder : {Folder}");
        Console.WriteLine($"Gen9       : {Gen9}");
        Console.WriteLine($"CPU cores  : {Environment.ProcessorCount}");
        Console.WriteLine($"Server GC  : {System.Runtime.GCSettings.IsServerGC}");
        Console.WriteLine();

        if (!File.Exists(Path.Combine(Folder, Gen9 ? "gta5_enhanced.exe" : "gta5.exe")))
        {
            Console.WriteLine("ERROR: game exe not found in folder. Pass the GTA V folder as an argument.");
            return 1;
        }

        var keySw = Stopwatch.StartNew();
        GTA5Keys.LoadFromPath(Folder, Gen9, null);
        keySw.Stop();
        Console.WriteLine($"Key load   : {keySw.ElapsedMilliseconds} ms");
        Console.WriteLine();

        // 1) Isolated RPF scan: sequential vs parallel.
        MeasureScan(dop: 1, label: "Scan (sequential, DOP=1)");
        MeasureScan(dop: Environment.ProcessorCount, label: $"Scan (parallel, DOP={Environment.ProcessorCount})");

        // 2) Full GameFileCache.Init phase breakdown (parallel scan).
        Environment.SetEnvironmentVariable("CW_SCAN_DOP", null);
        MeasureFullInit();

        return 0;
    }

    // Headless replica of WorldForm's ContentThread + LoadWorld for a user configuration, used to
    // iterate on startup changes and to dump the CW_FINGERPRINT correctness fingerprint.
    // Usage: --world <gtafolder> [--dlc <name>] [--mods] [--extra <folders;...>]
    static int RunWorld(string[] args)
    {
        string GetArg(string name)
        {
            int i = Array.IndexOf(args, name);
            return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : null;
        }
        var folder = args[1].TrimEnd('\\');
        var gen9 = File.Exists(Path.Combine(folder, "gta5_enhanced.exe")) &&
                   !File.Exists(Path.Combine(folder, "gta5.exe")); //same detection as Main, which returns before computing Gen9 for --world
        var dlc = GetArg("--dlc") ?? "";
        var mods = args.Contains("--mods");
        var extra = GetArg("--extra") ?? "";
        var headers = new HashSet<string>
        {
            "Building jenkindex...", "Building global dictionaries...", "Building DLC List...", "Building active RPF dictionary...",
            "Building map dictionaries...", "Loading manifests...", "Loading global texture list...", "Loading cache...",
            "Loading archetypes...", "Loading strings...", "Loading vehicles...", "Loading peds...", "Loading audio...", "Scan complete"
        };
        Action<string> status = s =>
        {
            if (s != null && headers.Contains(s))
            {
                CodeWalker.StartupTimer.Mark("status: " + s);
            }
        };
        Action<string> error = e => CodeWalker.StartupFingerprint.NoteError(e);

        CodeWalker.StartupTimer.Mark("Main start");
        GTA5Keys.LoadFromPath(folder, gen9, null);
        CodeWalker.StartupTimer.Mark("keys loaded");
        var gfc = new GameFileCache(2147483648, 10.0, folder, gen9, dlc, mods, "Installers;_CommonRedist")
        {
            ExtraFolders = extra,
            EnableFiveMResources = true,
            LoadAudio = true,
            LoadPeds = true,
            LoadVehicles = true,
        };
        gfc.DeferNonEssentialData = !args.Contains("--nodefer");
        gfc.Init(status, error);
        CodeWalker.StartupTimer.Mark("gameFileCache.Init done");

        var timecycle = new CodeWalker.World.Timecycle();
        var weather = new CodeWalker.World.Weather();
        var clouds = new CodeWalker.World.Clouds();
        var water = new CodeWalker.World.Water();
        var trains = new CodeWalker.World.Trains();
        var scenarios = new CodeWalker.World.Scenarios();
        var popzones = new CodeWalker.World.PopZones();
        var heightmaps = new CodeWalker.World.Heightmaps();
        var watermaps = new CodeWalker.World.Watermaps();
        var audiozones = new CodeWalker.World.AudioZones();
        var space = new CodeWalker.World.Space();
        Action<string> ns = _ => { };

        timecycle.UseModdedData = false;
        timecycle.Init(gfc, ns);
        CodeWalker.StartupTimer.Mark("LoadWorld: timecycles done");
        BoundsMaterialTypes.Init(gfc);
        weather.Init(gfc, ns, timecycle);
        clouds.Init(gfc, ns, weather);
        water.Init(gfc, ns);
        trains.Init(gfc, ns);
        CodeWalker.StartupTimer.Mark("LoadWorld: trains done");
        popzones.Init(gfc, ns);
        heightmaps.Init(gfc, ns);
        watermaps.Init(gfc, ns);
        space.Init(gfc, ns);
        CodeWalker.StartupTimer.Mark("WORLD_USABLE");
        // same order as WorldForm.LoadDeferredWorld
        gfc.LoadDeferredData();
        CodeWalker.StartupTimer.Mark("Deferred: game file cache data done");
        audiozones.Init(gfc, ns);
        CodeWalker.StartupTimer.Mark("LoadWorld: audiozones done");
        scenarios.Init(gfc, ns, timecycle);
        CodeWalker.StartupTimer.Mark("LoadWorld: scenarios done");
        JenkIndex.WaitForBuild();
        CodeWalker.StartupTimer.Mark("INITIALISED");

        CodeWalker.StartupFingerprint.Dump("world", gfc, space,
            CodeWalker.StartupFingerprint.DescribeWorld(timecycle, weather, clouds, water, trains, scenarios, popzones, heightmaps, watermaps, audiozones));
        CodeWalker.StartupTimer.Mark("fingerprint done");
        GC.Collect();
        Console.WriteLine("Managed heap after load: " + (GC.GetTotalMemory(true) / (1024 * 1024)) + " MB, working set " + (Environment.WorkingSet / (1024 * 1024)) + " MB");
        return 0;
    }

    static void MeasureScan(int dop, string label)
    {
        Environment.SetEnvironmentVariable("CW_SCAN_DOP", dop.ToString());
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long alloc0 = GC.GetTotalAllocatedBytes(true);
        var sw = Stopwatch.StartNew();

        var man = new RpfManager { EnableMods = false };
        man.Init(Folder, Gen9, _ => { }, _ => { });

        sw.Stop();
        long alloc1 = GC.GetTotalAllocatedBytes(true);

        int rpfCount = man.AllRpfs.Count;
        int entryCount = man.EntryDict.Count;
        Console.WriteLine($"{label,-34} {sw.ElapsedMilliseconds,7} ms   " +
                          $"rpfs={rpfCount,5}  entries={entryCount,7}  alloc={(alloc1 - alloc0) / (1024 * 1024),6} MB");
    }

    static void MeasureFullInit()
    {
        Console.WriteLine();
        Console.WriteLine("=== Full GameFileCache.Init phase breakdown (DOP=parallel) ===");

        var marks = new List<(double t, long alloc, string msg)>();
        var phaseHeaders = new HashSet<string>(StringComparer.Ordinal)
        {
            "Building jenkindex...", "Building global dictionaries...", "Building DLC List...",
            "Building active RPF dictionary...", "Building map dictionaries...", "Loading manifests...",
            "Loading global texture list...", "Loading cache...", "Loading archetypes...",
            "Loading strings...", "Loading vehicles...", "Loading peds...", "Loading audio...",
            "Scan complete",
        };

        var sw = Stopwatch.StartNew();
        object lk = new();
        int totalMsgs = 0;
        string lastHeader = null;

        Action<string> status = m =>
        {
            System.Threading.Interlocked.Increment(ref totalMsgs);
            if (phaseHeaders.Contains(m))
            {
                lock (lk)
                {
                    // "Scan complete" appears twice (RpfManager + GameFileCache); keep both.
                    if (m == lastHeader && m != "Scan complete") return;
                    lastHeader = m;
                    marks.Add((sw.Elapsed.TotalMilliseconds, GC.GetTotalAllocatedBytes(false), m));
                }
            }
        };

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Match a typical World-view load: DLC on, mods off, everything loaded.
        var gfc = new GameFileCache(2147483648, 10.0, Folder, Gen9, "", false, "")
        {
            EnableDlc = true,
            LoadArchetypes = true,
            LoadVehicles = true,
            LoadPeds = true,
            LoadAudio = true,
        };

        long allocStart = GC.GetTotalAllocatedBytes(true);
        marks.Add((0, allocStart, "<start>"));
        gfc.Init(status, _ => { });
        sw.Stop();
        long allocEnd = GC.GetTotalAllocatedBytes(true);
        marks.Add((sw.Elapsed.TotalMilliseconds, allocEnd, "<end>"));

        Console.WriteLine();
        Console.WriteLine($"{"phase",-38}{"start ms",10}{"dur ms",10}{"alloc MB",12}");
        Console.WriteLine(new string('-', 70));
        for (int i = 1; i < marks.Count; i++)
        {
            var prev = marks[i - 1];
            var cur = marks[i];
            double dur = cur.t - prev.t;
            double allocMb = (cur.alloc - prev.alloc) / (1024.0 * 1024.0);
            Console.WriteLine($"{prev.msg,-38}{prev.t,10:F0}{dur,10:F0}{allocMb,12:F1}");
        }
        Console.WriteLine(new string('-', 70));
        Console.WriteLine($"{"TOTAL",-38}{0,10}{sw.Elapsed.TotalMilliseconds,10:F0}" +
                          $"{(allocEnd - allocStart) / (1024.0 * 1024.0),12:F1}");
        Console.WriteLine();
        Console.WriteLine($"Total status callbacks fired during init: {totalMsgs:N0}");
        Console.WriteLine($"GC: gen0={GC.CollectionCount(0)} gen1={GC.CollectionCount(1)} gen2={GC.CollectionCount(2)}");
    }
}
