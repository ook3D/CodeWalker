using CodeWalker.GameFiles;
using CodeWalker.World;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CodeWalker
{
    /// <summary>
    /// Opt-in correctness fingerprint of everything startup loads. Enabled only when the CW_FINGERPRINT env var
    /// is set to a file path; every call is a no-op otherwise. Each line is "name count hash": hashes are
    /// order-independent (multiset) unless the name ends in "[ordered]", where load order is part of the result.
    /// Used to prove that startup optimisations produce exactly the same loaded state.
    /// </summary>
    public static class StartupFingerprint
    {
        private static readonly string? OutPath = Environment.GetEnvironmentVariable("CW_FINGERPRINT");
        public static bool Enabled => !string.IsNullOrEmpty(OutPath);

        private static readonly object errSync = new();
        private static int errorCount;
        private static ulong errorHash;

        /// <summary>Record an error-log line (count + hash of its first line, so stack-trace line numbers don't matter).</summary>
        public static void NoteError(string? text)
        {
            if (!Enabled || text == null)
            {
                return;
            }
            var first = text;
            int nl = first.IndexOfAny(new[] { '\r', '\n' });
            if (nl >= 0)
            {
                first = first.Substring(0, nl);
            }
            lock (errSync)
            {
                errorCount++;
                errorHash += Mix(Fnv(first));
            }
        }

        public static void Dump(string label, GameFileCache? gfc, Space? space, IDictionary<string, string>? extra = null)
        {
            if (!Enabled)
            {
                return;
            }
            try
            {
                JenkIndex.WaitForBuild();
                var lines = new List<string>();
                lines.Add("## " + label);
                if (gfc != null)
                {
                    AddCache(lines, gfc);
                }
                if (space != null)
                {
                    AddSpace(lines, space);
                }
                //Which string wins a hash collision (case variants, true collisions) depends on parallel merge timing
                //in the existing index build, so the exact strings aren't reproducible run to run: compare the hash
                //set exactly and the strings case-folded.
                AddSet(lines, "JenkIndex.keys", JenkIndex.Index.Keys.Select(k => k.ToString()));
                if (Environment.GetEnvironmentVariable("CW_FINGERPRINT_JENKKEYS") == "1")
                {
                    //full listing, to tell which names differ when the key sets don't match
                    File.WriteAllLines(OutPath + "." + label + ".jenk.txt", JenkIndex.Index.Select(k => k.Key + "\t" + k.Value).OrderBy(x => x, StringComparer.Ordinal));
                }
                AddSet(lines, "JenkIndex.lower", JenkIndex.Index.Select(k => k.Key + ":" + k.Value.ToLowerInvariant()));
                AddSet(lines, "GlobalText", GlobalText.Index.Select(k => k.Key + ":" + k.Value));
                var seen = typeof(LodDiag).GetField("seen", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as HashSet<string>;
                if (seen != null)
                {
                    lock (seen)
                    {
                        AddSet(lines, "LodDiag", seen.ToList());
                    }
                }
                lock (errSync)
                {
                    lines.Add("ErrorLog\t" + errorCount + "\t" + errorHash.ToString("X16"));
                }
                if (extra != null)
                {
                    foreach (var kvp in extra.OrderBy(k => k.Key, StringComparer.Ordinal))
                    {
                        lines.Add(kvp.Key + "\t" + kvp.Value);
                    }
                }
                File.AppendAllText(OutPath!, string.Join(Environment.NewLine, lines) + Environment.NewLine);
            }
            catch (Exception ex)
            {
                try
                {
                    File.AppendAllText(OutPath!, "## FINGERPRINT FAILED: " + ex + Environment.NewLine);
                }
                catch
                {
                }
            }
        }

        public static Dictionary<string, string> DescribeWorld(Timecycle? timecycle, Weather? weather, Clouds? clouds, Water? water, Trains? trains,
            Scenarios? scenarios, PopZones? popzones, Heightmaps? heightmaps, Watermaps? watermaps, AudioZones? audiozones)
        {
            var d = new Dictionary<string, string>();
            if (!Enabled)
            {
                return d;
            }
            string L(string name, IEnumerable<string?> items)
            {
                var tmp = new List<string>();
                AddList(tmp, name, items);
                var parts = tmp[0].Split('\t');
                return parts[1] + "\t" + parts[2];
            }
            d["World.Timecycle.Samples"] = (timecycle?.Samples?.Count ?? -1).ToString();
            d["World.Weather.Types"] = L("w", weather?.WeatherTypes?.Keys.OrderBy(k => k, StringComparer.Ordinal) ?? Enumerable.Empty<string>());
            d["World.Clouds.AnimSettings"] = (clouds?.AnimSettings?.Count ?? -1).ToString();
            d["World.Water.Quads"] = (water?.WaterQuads?.Count ?? -1) + "/" + (water?.CalmingQuads?.Count ?? -1) + "/" + (water?.WaveQuads?.Count ?? -1);
            d["World.Trains.Tracks"] = (trains?.TrainTracks?.Count ?? -1).ToString();
            d["World.Scenarios.Regions"] = L("s", scenarios?.ScenarioRegions?.Select(r => r?.RpfFileEntry?.Path) ?? Enumerable.Empty<string>());
            d["World.PopZones.Groups"] = (popzones?.Groups?.Count ?? -1).ToString();
            d["World.Heightmaps"] = (heightmaps?.HeightmapFiles?.Count ?? -1).ToString();
            d["World.Watermaps"] = (watermaps?.WatermapFiles?.Count ?? -1).ToString();
            d["World.AudioZones"] = (audiozones?.PlacementsDict?.Count ?? -1).ToString();
            d["World.ScenarioTypes"] = Scenarios.ScenarioTypes != null ? "loaded" : "null";
            return d;
        }

        /// <summary>"count	hash" of an unordered collection, for callers outside Core.</summary>
        public static string SetHash(IEnumerable<string?> items)
        {
            var tmp = new List<string>();
            AddSet(tmp, "x", items);
            return tmp[0].Substring(2);
        }

        /// <summary>"count	hash" of an ordered collection, for callers outside Core.</summary>
        public static string ListHash(IEnumerable<string?> items)
        {
            var tmp = new List<string>();
            AddList(tmp, "x", items);
            return tmp[0].Substring(tmp[0].IndexOf('	') + 1);
        }

        private static string EntryDesc(RpfEntry? e)
        {
            if (e == null)
            {
                return "null";
            }
            switch (e)
            {
                case RpfResourceFileEntry r:
                    return "R|" + r.Path + "|" + r.FileSize + "|" + r.SystemFlags + "|" + r.GraphicsFlags + "|" + r.NameHash + "|" + r.ShortNameHash;
                case RpfBinaryFileEntry b:
                    return "B|" + b.Path + "|" + b.FileSize + "|" + b.FileUncompressedSize + "|" + b.NameHash + "|" + b.ShortNameHash;
                default:
                    return "D|" + e.Path;
            }
        }

        private static string RpfDesc(RpfFile? r)
        {
            if (r == null)
            {
                return "null";
            }
            return r.Path + "|" + r.GetType().Name + "|" + r.FileSize + "|" + (r.AllEntries?.Count ?? -1) + "|" + (r.Children?.Count ?? -1);
        }

        private static void AddCache(List<string> lines, GameFileCache gfc)
        {
            var rm = gfc.RpfMan;
            if (rm != null)
            {
                AddList(lines, "Rpf.AllRpfs", rm.AllRpfs.Select(RpfDesc));
                AddList(lines, "Rpf.BaseRpfs", rm.BaseRpfs.Select(RpfDesc));
                AddList(lines, "Rpf.DlcRpfs", rm.DlcRpfs.Select(RpfDesc));
                AddList(lines, "Rpf.ModRpfs", rm.ModRpfs.Select(RpfDesc));
                AddList(lines, "Rpf.ExtraRpfs", rm.ExtraRpfs.Select(RpfDesc));
                AddSet(lines, "Rpf.RpfDict", rm.RpfDict.Select(k => k.Key + "=" + RpfDesc(k.Value)));
                AddSet(lines, "Rpf.ModRpfDict", rm.ModRpfDict.Select(k => k.Key + "=" + RpfDesc(k.Value)));
                AddSet(lines, "Rpf.EntryDict", rm.EntryDict.Select(k => k.Key + "=" + EntryDesc(k.Value)));
                AddSet(lines, "Rpf.ModEntryDict", rm.ModEntryDict.Select(k => k.Key + "=" + EntryDesc(k.Value)));
                lines.Add("Rpf.EscrowedFileCount\t" + rm.EscrowedFileCount);
                //every entry of every archive, including parent links (catches scan differences)
                AddSet(lines, "Rpf.AllEntries", rm.AllRpfs.SelectMany(r => (IEnumerable<RpfEntry>?)r.AllEntries ?? Array.Empty<RpfEntry>())
                    .Select(e => EntryDesc(e) + "<" + (e.Parent?.Path ?? "")));
            }

            AddDict(lines, "YdrDict", gfc.YdrDict);
            AddDict(lines, "YddDict", gfc.YddDict);
            AddDict(lines, "YtdDict", gfc.YtdDict);
            AddDict(lines, "YmapDict", gfc.YmapDict);
            AddDict(lines, "YftDict", gfc.YftDict);
            AddDict(lines, "YbnDict", gfc.YbnDict);
            AddDict(lines, "YcdDict", gfc.YcdDict);
            AddDict(lines, "YedDict", gfc.YedDict);
            AddDict(lines, "YnvDict", gfc.YnvDict);
            AddDict(lines, "Gxt2Dict", gfc.Gxt2Dict);
            AddDict(lines, "AllYmapsDict", gfc.AllYmapsDict);
            AddSet(lines, "YtypDict", gfc.YtypDict.Select(k => k.Key + "=" + (k.Value?.RpfFileEntry?.Path ?? "null") + "|" + (k.Value?.AllArchetypes?.Length ?? -1)));

            var archDict = PrivateField<Dictionary<uint, Archetype>>(gfc, "archetypeDict");
            if (archDict != null)
            {
                AddSet(lines, "ArchetypeDict", archDict.Select(k => k.Key + "=" + (k.Value?.Hash.Hash ?? 0) + "|" + (k.Value?.Ytyp?.RpfFileEntry?.Path ?? "")));
            }
            var texLookup = PrivateField<Dictionary<uint, RpfFileEntry>>(gfc, "textureLookup");
            if (texLookup != null)
            {
                AddSet(lines, "TextureLookup", texLookup.Select(k => k.Key + "=" + k.Value?.Path));
            }
            var texParents = PrivateField<Dictionary<MetaHash, MetaHash>>(gfc, "textureParents");
            if (texParents != null)
            {
                AddSet(lines, "TextureParents", texParents.Select(k => k.Key.Hash + "=" + k.Value.Hash));
            }
            var hdTex = PrivateField<Dictionary<MetaHash, MetaHash>>(gfc, "hdtexturelookup");
            if (hdTex != null)
            {
                AddSet(lines, "HdTextureLookup", hdTex.Select(k => k.Key.Hash + "=" + k.Value.Hash));
            }

            AddList(lines, "DlcNameList", gfc.DlcNameList);
            AddList(lines, "DlcPaths", gfc.DlcPaths);
            AddList(lines, "DlcActiveRpfs", gfc.DlcActiveRpfs.Select(RpfDesc));
            AddList(lines, "DlcCacheFileList", gfc.DlcCacheFileList);
            AddSet(lines, "ActiveMapRpfFiles", gfc.ActiveMapRpfFiles.Select(k => k.Key + "=" + RpfDesc(k.Value)));
            AddSet(lines, "DisabledYmaps", gfc.DisabledYmaps.Select(h => h.ToString()));
            AddList(lines, "AllRpfs(gfc)", gfc.AllRpfs.Select(RpfDesc));
            AddList(lines, "BaseRpfs(gfc)", gfc.BaseRpfs.Select(RpfDesc));
            AddList(lines, "DlcRpfs(gfc)", gfc.DlcRpfs.Select(RpfDesc));
            lines.Add("AllCacheFiles\t" + gfc.AllCacheFiles.Count);
            lines.Add("AllManifests\t" + gfc.AllManifests.Count);
            AddSet(lines, "YmapHierarchyDict", gfc.YmapHierarchyDict.Select(k => k.Key + "=" + NodeDesc(k.Value)));

            AddSet(lines, "VehiclesInitDict", gfc.VehiclesInitDict.Select(k => k.Key.Hash + "=" + k.Value?.modelName + "|" + k.Value?.txdName));
            AddSet(lines, "PedsInitDict", gfc.PedsInitDict.Select(k => k.Key.Hash + "=" + k.Value?.Name));
            AddSet(lines, "PedVariationsDict", gfc.PedVariationsDict.Select(k => k.Key.Hash + "=" + (k.Value?.RpfFileEntry?.Path ?? "")));
            AddNested(lines, "PedDrawableDicts", gfc.PedDrawableDicts);
            AddNested(lines, "PedTextureDicts", gfc.PedTextureDicts);
            AddNested(lines, "PedClothDicts", gfc.PedClothDicts);

            lines.Add("AudioDatRelFiles\t" + gfc.AudioDatRelFiles.Count);
            AddRel(lines, "AudioConfigDict", gfc.AudioConfigDict);
            AddRel(lines, "AudioSpeechDict", gfc.AudioSpeechDict);
            AddRel(lines, "AudioSynthsDict", gfc.AudioSynthsDict);
            AddRel(lines, "AudioMixersDict", gfc.AudioMixersDict);
            AddRel(lines, "AudioCurvesDict", gfc.AudioCurvesDict);
            AddRel(lines, "AudioCategsDict", gfc.AudioCategsDict);
            AddRel(lines, "AudioSoundsDict", gfc.AudioSoundsDict);
            AddRel(lines, "AudioGameDict", gfc.AudioGameDict);
            lines.Add("TimeCycleModsDict\t" + gfc.TimeCycleModsDict.Count);
        }

        private static string NodeDesc(MapDataStoreNode? n)
        {
            if (n == null)
            {
                return "null";
            }
            return n.Name.Hash + "|" + n.ParentName.Hash + "|" + n.ContentFlags + "|" + n.streamingExtentsMin + "|" + n.streamingExtentsMax
                + "|" + n.entitiesExtentsMin + "|" + n.entitiesExtentsMax + "|" + (n.Children?.Length ?? -1);
        }

        private static void AddSpace(List<string> lines, Space space)
        {
            var nodedict = PrivateField<Dictionary<MetaHash, MapDataStoreNode>>(space, "nodedict");
            if (nodedict != null)
            {
                AddSet(lines, "Space.nodedict", nodedict.Select(k => k.Key.Hash + "=" + NodeDesc(k.Value)));
            }
            var boundsdict = PrivateField<IDictionary>(space, "boundsdict");
            if (boundsdict != null)
            {
                var items = new List<string>();
                foreach (DictionaryEntry de in boundsdict)
                {
                    var b = de.Value as BoundsStoreItem;
                    items.Add(de.Key + "=" + (b == null ? "null" : b.Name.Hash + "|" + b.Min + "|" + b.Max + "|" + b.Layer));
                }
                AddSet(lines, "Space.boundsdict", items);
            }
            var used = PrivateField<Dictionary<MetaHash, BoundsStoreItem>>(space, "usedboundsdict");
            if (used != null)
            {
                AddSet(lines, "Space.usedboundsdict", used.Select(k => k.Key.Hash + "=" + k.Value.Min + "|" + k.Value.Max));
            }
            var proxies = PrivateField<IDictionary>(space, "interiorProxies");
            if (proxies != null)
            {
                lines.Add("Space.interiorProxies\t" + proxies.Count);
            }
            var ynds = PrivateField<Dictionary<uint, YndFile>>(space, "AllYnds");
            if (ynds != null)
            {
                AddSet(lines, "Space.AllYnds", ynds.Select(k => k.Key + "=" + (k.Value?.RpfFileEntry?.Path ?? "") + "|" + (k.Value?.Nodes?.Length ?? -1)));
            }

            var mapItems = new List<string>();
            int mapNodes = 0;
            void WalkMap(SpaceMapDataStoreNode? n)
            {
                if (n == null)
                {
                    return;
                }
                mapNodes++;
                if (n.Items != null)
                {
                    foreach (var it in n.Items)
                    {
                        mapItems.Add(n.Depth + ":" + NodeDesc(it));
                    }
                }
                if (n.Children != null)
                {
                    foreach (var c in n.Children)
                    {
                        WalkMap(c);
                    }
                }
            }
            WalkMap(space.MapDataStore?.RootNode);
            lines.Add("Space.MapDataStore.treeNodes\t" + mapNodes);
            AddSet(lines, "Space.MapDataStore.items", mapItems);

            var boundItems = new List<string>();
            int boundNodes = 0;
            void WalkBounds(SpaceBoundsStoreNode? n)
            {
                if (n == null)
                {
                    return;
                }
                boundNodes++;
                if (n.Items != null)
                {
                    foreach (var it in n.Items)
                    {
                        boundItems.Add(n.Depth + ":" + it.Name.Hash + "|" + it.Min + "|" + it.Max);
                    }
                }
                if (n.Children != null)
                {
                    foreach (var c in n.Children)
                    {
                        WalkBounds(c);
                    }
                }
            }
            WalkBounds(space.BoundsStore?.RootNode);
            lines.Add("Space.BoundsStore.treeNodes\t" + boundNodes);
            AddSet(lines, "Space.BoundsStore.items", boundItems);

            int navCells = 0;
            var nav = space.NavGrid;
            if (nav?.Cells != null)
            {
                foreach (var c in nav.Cells)
                {
                    if (c?.YnvEntry != null)
                    {
                        navCells++;
                    }
                }
            }
            lines.Add("Space.NavGrid.ynvCells\t" + navCells);
        }

        private static T? PrivateField<T>(object o, string name) where T : class
        {
            var f = o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            return f?.GetValue(o) as T;
        }

        private static void AddDict(List<string> lines, string name, Dictionary<uint, RpfFileEntry> d)
        {
            AddSet(lines, name, d.Select(k => k.Key + "=" + EntryDesc(k.Value)));
        }

        private static void AddNested(List<string> lines, string name, Dictionary<MetaHash, Dictionary<MetaHash, RpfFileEntry>> d)
        {
            AddSet(lines, name, d.SelectMany(o => o.Value.Select(i => o.Key.Hash + "/" + i.Key.Hash + "=" + i.Value?.Path)));
        }

        private static void AddRel(List<string> lines, string name, Dictionary<MetaHash, RelData> d)
        {
            AddSet(lines, name, d.Select(k => k.Key.Hash + "=" + k.Value?.TypeID + "|" + (k.Value?.Rel?.RpfFileEntry?.Path ?? "")));
        }

        private static void AddSet(List<string> lines, string name, IEnumerable<string?> items)
        {
            int count = 0;
            ulong h = 0;
            foreach (var s in items)
            {
                count++;
                h += Mix(Fnv(s ?? "\0null"));
            }
            lines.Add(name + "\t" + count + "\t" + h.ToString("X16"));
        }

        private static void AddList(List<string> lines, string name, IEnumerable<string?> items)
        {
            int count = 0;
            ulong h = 1469598103934665603UL;
            foreach (var s in items)
            {
                count++;
                h = (h ^ Mix(Fnv(s ?? "\0null"))) * 1099511628211UL;
            }
            lines.Add(name + "[ordered]\t" + count + "\t" + h.ToString("X16"));
        }

        private static ulong Fnv(string s)
        {
            ulong h = 1469598103934665603UL;
            foreach (char c in s)
            {
                h = (h ^ c) * 1099511628211UL;
            }
            return h;
        }

        private static ulong Mix(ulong z)
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
