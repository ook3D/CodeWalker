using CodeWalker.GameFiles;
using SharpDX;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Globalization;
using System.Windows.Forms;
using System.Xml.Linq;

namespace CodeWalker.Project.Panels
{
    public partial class GenerateLODLightsPanel : ProjectPanel
    {
        public ProjectForm ProjectForm { get; set; }
        public ProjectFile? CurrentProjectFile { get; set; }

        // R* stock is 48.0f, raised so NVE's high-intensity lights survive the u8 pack.
        // The game unpacks with its own baked 48.0f - DisableEditorWatermark patches it to
        // match, and LODLightManager.h in the R* tool has to carry the same value.
        // Change it and every LOD light has to be regenerated.
        const float MAX_LODLIGHT_INTENSITY = 400.0f;
        const float MAX_LODLIGHT_CONE_ANGLE = 180.0f;
        const float MAX_LODLIGHT_CAPSULE_EXTENT = 140.0f;
        const float MAX_LODLIGHT_CORONA_INTENSITY = 32.0f;
        const uint LIGHTFLAG_DONT_USE_IN_CUTSCENE = (1u << 2);
        const uint LIGHTFLAG_CORONA_ONLY = (1u << 15);
        const uint LIGHTFLAG_FAR_LOD_LIGHT = (1u << 22);
        const uint LIGHTFLAG_FORCE_MEDIUM_LOD_LIGHT = (1u << 28);
        const uint LIGHTFLAG_CORONA_ONLY_LOD_LIGHT = (1u << 29);
        const int LIGHT_CATEGORY_SMALL = 0;
        const int LIGHT_CATEGORY_MEDIUM = 1;
        const int LIGHT_CATEGORY_LARGE = 2;

        const int MAX_LIGHTS_PER_CELL = 800;
        const int MIN_LIGHT_COUNT_TO_CONSOLIDATE = 100;

        // R* LightExtractionTool .imap output dir - source of authoritative in-game LOD light
        // hashes (DistLODLights_*.imap positions + LODLights_*.imap hashes, paired by index).
        const string ROCKSTAR_IMAP_DIR = @"X:\gta5\exported_LODLights";

        static readonly string[] CategoryLabels = { "small", "medium", "large" };

        // R* LightExtractionTool visibility ranges (expansion 200 + 250/750/2500)
        // used for cell subdivision limits and imap streaming extents
        static readonly float[] LodVisRadius = { 450.0f, 950.0f, 2700.0f };
        static readonly float[] DistVisRadius = { 450.0f, 3000.0f, 2700.0f };


        public GenerateLODLightsPanel(ProjectForm projectForm)
        {
            ProjectForm = projectForm ?? throw new ArgumentNullException(nameof(projectForm));
            InitializeComponent();

            if (ProjectForm.WorldForm == null)
            {
                GenerateButton.Enabled = false;
                UpdateStatus("Unable to generate - World View not available!");
            }
        }


        public void SetProject(ProjectFile? project)
        {
            CurrentProjectFile = project;
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select output directory for LOD light ymaps";
                if (!string.IsNullOrEmpty(OutputPathTextBox.Text) && Directory.Exists(OutputPathTextBox.Text))
                {
                    dialog.SelectedPath = OutputPathTextBox.Text;
                }
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    OutputPathTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void GenerateComplete()
        {
            try
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() => { GenerateComplete(); }));
                }
                else
                {
                    GenerateButton.Enabled = true;
                }
            }
            catch { }
        }


        private void UpdateStatus(string text)
        {
            try
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() => { UpdateStatus(text); }));
                }
                else
                {
                    StatusLabel.Text = text;
                }
            }
            catch { }
        }

        private static byte PackU8(float val, float range)
        {
            // R* LL_PACK_U8 truncates (no rounding); clamp added to avoid overflow on out-of-range source data
            return (byte)Math.Max(Math.Min((int)(val * (255.0f / range)), 255), 0);
        }

        // mirrors Lights::CalculateLightCategory (lights.cpp) - uses the light type
        // as stored after any capsule->point conversion, same as the R* tool
        private static int GetLightCategory(uint flags, uint lightType, float falloff, float intensity, float capsuleExtent)
        {
            if ((flags & LIGHTFLAG_FAR_LOD_LIGHT) != 0)
            {
                return LIGHT_CATEGORY_LARGE;
            }

            float length = falloff;
            if (lightType == (uint)LightType.Capsule)
            {
                length = 2.0f * falloff + capsuleExtent;
            }

            if ((flags & LIGHTFLAG_FORCE_MEDIUM_LOD_LIGHT) != 0 || (length >= 10.0f && intensity >= 1.0f))
            {
                return LIGHT_CATEGORY_MEDIUM;
            }

            return LIGHT_CATEGORY_SMALL;
        }


        private void GenerateButton_Click(object sender, EventArgs e)
        {
            var gameFileCache = ProjectForm?.WorldForm?.GameFileCache;
            if (gameFileCache == null) return;
            var project = ProjectForm?.CurrentProjectFile;
            if (project == null)
            {
                UpdateStatus("Unable to generate - no project is open!");
                return;
            }

            var outputDir = OutputPathTextBox.Text;
            if (string.IsNullOrEmpty(outputDir) || !Directory.Exists(outputDir))
            {
                MessageBox.Show("Please select a valid output directory.");
                return;
            }

            bool fullMap = FullMapCheckBox.Checked;

            if (!fullMap && (ProjectForm.CurrentProjectFile?.YmapFiles == null || ProjectForm.CurrentProjectFile.YmapFiles.Count == 0))
            {
                MessageBox.Show("No project ymaps loaded. Check 'Regenerate full map' to use all game ymaps instead.");
                return;
            }

            GenerateButton.Enabled = false;

            List<YmapFile> projectYmaps = fullMap ? [] : project.YmapFiles;

            var pname = NameTextBox.Text;
            var distRange = (float)DistRangeUpDown.Value;

            Task.Run(async () =>
            {
                if (fullMap)
                {
                    projectYmaps = LoadAllGameYmaps(gameFileCache);
                }

                var lightsSmall = new List<Light>();
                var lightsMedium = new List<Light>();
                var lightsLarge = new List<Light>();

                // Collect all entities and deduplicate archetypes for batch loading
                var allEntities = new List<(YmapEntityDef ent, string entName)>();
                var uniqueArchetypes = new HashSet<uint>();

                // Drop co-located duplicate placements (same archetype at the same position in two
                // ymaps) - the game renders one, so a second LOD light would link to nothing.
                var entityByKey = new HashSet<(uint, long, long, long)>();
                foreach (var ymap in projectYmaps)
                {
                    if (ymap?.AllEntities == null) continue;
                    foreach (var ent in ymap.AllEntities)
                    {
                        if (ent.Archetype == null) continue;
                        var entName = ent.Archetype.Name?.ToString() ?? "";
                        if (entName.Contains("prop_dock_bouy")) continue;

                        var p = ent.Position;
                        var key = ((uint)ent.Archetype.Hash,
                                   (long)MathF.Round(p.X * 1000f),
                                   (long)MathF.Round(p.Y * 1000f),
                                   (long)MathF.Round(p.Z * 1000f));
                        if (!entityByKey.Add(key)) continue; // drop the duplicate placement
                        allEntities.Add((ent, entName));
                        uniqueArchetypes.Add(ent.Archetype.Hash);
                    }
                }

                // Pre-request all unique drawables so they start loading in parallel
                UpdateStatus($"Requesting {uniqueArchetypes.Count} unique drawables...");
                var drawableCache = new Dictionary<uint, DrawableBase>();
                var pendingArchetypes = new HashSet<uint>();

                foreach (var (ent, _) in allEntities)
                {
                    if (ent.Archetype == null) continue;
                    var hash = ent.Archetype.Hash;
                    if (drawableCache.ContainsKey(hash) || pendingArchetypes.Contains(hash)) continue;

                    var (dwbl, waiting) = await gameFileCache.TryGetDrawableAsync(ent.Archetype);
                    if (dwbl != null)
                    {
                        drawableCache[hash] = dwbl;
                    }
                    else if (waiting)
                    {
                        pendingArchetypes.Add(hash);
                    }
                }

                // Wait for all pending drawables to finish loading (10s timeout per drawable)
                if (pendingArchetypes.Count > 0)
                {
                    UpdateStatus($"Waiting for {pendingArchetypes.Count} drawables to load...");
                    var archetypeLookup = new Dictionary<uint, Archetype>();
                    foreach (var (ent, _) in allEntities)
                    {
                        if (ent.Archetype == null) continue;
                    var hash = ent.Archetype.Hash;
                        if (pendingArchetypes.Contains(hash) && !archetypeLookup.ContainsKey(hash))
                        {
                            archetypeLookup[hash] = ent.Archetype;
                        }
                    }

                    // ponytail: the loader queue is serial and only holds ~10 items, so per-model timers
                    // start ticking long before a model is even attempted. Time out on lack of *progress*
                    // instead - if anything resolved, the rest just haven't been reached yet.
                    var stallTimer = System.Diagnostics.Stopwatch.StartNew();
                    while (pendingArchetypes.Count > 0)
                    {
                        await Task.Delay(3);
                        var resolved = new List<uint>();
                        foreach (var hash in pendingArchetypes)
                        {
                            var (dwbl, waiting) = await gameFileCache.TryGetDrawableAsync(archetypeLookup[hash]);
                            if (dwbl != null)
                            {
                                drawableCache[hash] = dwbl;
                                resolved.Add(hash);
                            }
                            else if (!waiting)
                            {
                                resolved.Add(hash);
                            }
                        }
                        if (resolved.Count > 0)
                        {
                            stallTimer.Restart();
                            foreach (var hash in resolved)
                            {
                                pendingArchetypes.Remove(hash);
                            }
                        }
                        else if (stallTimer.Elapsed.TotalMinutes >= 1.0)
                        {
                            UpdateStatus($"No load progress for 1 minute, skipping {pendingArchetypes.Count} stuck drawables...");
                            break;
                        }
                        if (pendingArchetypes.Count > 0)
                        {
                            UpdateStatus($"Waiting for {pendingArchetypes.Count} drawables to load...");
                        }
                    }
                }

                // Process all entities using cached drawables
                UpdateStatus($"Processing {allEntities.Count} entities...");
                var usedHashes = new HashSet<uint>(); // R* tool skips duplicate entities and hash clashes

                foreach (var (ent, entName) in allEntities)
                {
                    if (ent.Archetype == null || !drawableCache.TryGetValue(ent.Archetype.Hash, out var dwbl)) continue;

                    // R* B*951557: don't generate LOD lights for priority-stripped entities.
                    // fixed = entity/archetype FLAG_IS_FIXED, fixed-for-nav, or WillGenerateBuilding()
                    // (static non-object non-animated archetypes - i.e. most map buildings)
                    if (ent._CEntityDef.priorityLevel > rage__ePriorityLevel.PRI_REQUIRED)
                    {
                        uint af = ent.Archetype._BaseArchetypeDef.flags;
                        bool isFixed = ((ent._CEntityDef.flags & (1u << 5)) != 0) ||
                                       ((af & ((1u << 5) | (1u << 27))) != 0) ||
                                       (((af & ((1u << 17) | (1u << 9))) == 0) && (ent.Archetype._BaseArchetypeDef.clipDictionary == 0));
                        if (!isFixed) continue;
                    }

                    ent.EnsureLights(dwbl);
                    var elights = ent.Lights;
                    if (elights == null) continue;

                    // R* tool only tags "streetlight"/"street_light"; nylamp/nytraf kept for IV map props
                    bool isStreetLight = entName.Contains("streetlight") || entName.Contains("street_light") || entName.Contains("nylamp") || entName.Contains("nytraf");

                    // lightId = archetype extension count + light index (matches EnsureLights + the ASI sweep)
                    int exts = ent.Archetype?.Extensions?.Length ?? 0;

                    for (int li = 0; li < elights.Length; li++)
                    {
                        var elight = elights[li];
                        var la = elight.Attributes;

                        // duplicate entity / hash clash - R* tool skips both (same hash inputs -> same hash)
                        if (!usedHashes.Add(elight.Hash)) continue;

                        // B*1786337: exclude lights with a light fade distance
                        if (la == null || la.LightFadeDistance > 0) continue;

                        uint flags = la.Flags;
                        bool isCoronaOnly = (flags & (LIGHTFLAG_CORONA_ONLY | LIGHTFLAG_CORONA_ONLY_LOD_LIGHT)) != 0;

                        uint type = (uint)la.Type;
                        float capsuleExtent = la.Extent.X;

                        // B*2043802: capsule whose extent packs to 0 becomes an omni
                        if (type == (uint)LightType.Capsule)
                        {
                            float minCapsuleExtent = MAX_LODLIGHT_CAPSULE_EXTENT / 255.0f;
                            if (capsuleExtent < minCapsuleExtent)
                            {
                                type = (uint)LightType.Point;
                            }
                        }
                        if (capsuleExtent > MAX_LODLIGHT_CAPSULE_EXTENT)
                        {
                            capsuleExtent = MAX_LODLIGHT_CAPSULE_EXTENT - 1.0f; //R* CheckPackedLightData clamp
                        }

                        uint r = la.ColorR;
                        uint g = la.ColorG;
                        uint b = la.ColorB;
                        uint packedIntensity = PackU8(la.Intensity, MAX_LODLIGHT_INTENSITY);
                        uint colour = (packedIntensity << 24) + (r << 16) + (g << 8) + b;

                        byte inner = PackU8(la.ConeInnerAngle, MAX_LODLIGHT_CONE_ANGLE);
                        byte outer;
                        if (type == (uint)LightType.Capsule)
                        {
                            outer = PackU8(capsuleExtent, MAX_LODLIGHT_CAPSULE_EXTENT);
                        }
                        else
                        {
                            outer = PackU8(la.ConeOuterAngle, MAX_LODLIGHT_CONE_ANGLE);
                        }

                        byte packedCorona = (la.CoronaSize < 0.05f) ? (byte)0 : PackU8(la.CoronaIntensity, MAX_LODLIGHT_CORONA_INTENSITY);

                        uint timeAndState = la.TimeFlags & 0x00FFFFFFu;
                        if (isStreetLight)
                        {
                            timeAndState |= (1u << 24);
                        }
                        if (isCoronaOnly)
                        {
                            timeAndState |= (1u << 25);
                        }
                        timeAndState |= (type << 26);
                        if ((flags & LIGHTFLAG_DONT_USE_IN_CUTSCENE) != 0)
                        {
                            timeAndState |= (1u << 31);
                        }

                        var light = new Light();
                        light.position = new MetaVECTOR3(elight.Position);
                        light.colour = colour;
                        light.direction = new MetaVECTOR3(elight.Direction);
                        light.falloff = la.Falloff;
                        light.falloffExponent = la.FalloffExponent;
                        light.timeAndStateFlags = timeAndState;
                        light.hash = elight.Hash; //EnsureLights computes the game's GetAABB-based hash
                        light.coneInnerAngle = inner;
                        light.coneOuterAngleOrCapExt = outer;
                        light.coronaIntensity = packedCorona;
                        light.isStreetLight = isStreetLight;

                        int category = GetLightCategory(flags, type, la.Falloff, la.Intensity, capsuleExtent);
                        switch (category)
                        {
                            case LIGHT_CATEGORY_LARGE:
                                lightsLarge.Add(light);
                                break;
                            case LIGHT_CATEGORY_MEDIUM:
                                lightsMedium.Add(light);
                                break;
                            default:
                                lightsSmall.Add(light);
                                break;
                        }
                    }
                }

                int totalLights = lightsSmall.Count + lightsMedium.Count + lightsLarge.Count;

                if (totalLights == 0)
                {
                    MessageBox.Show("No lights found in project!");
                    GenerateComplete();
                    return;
                }

                UpdateStatus($"Collected {totalLights} lights (S:{lightsSmall.Count} M:{lightsMedium.Count} L:{lightsLarge.Count}). Borrowing from vanilla LOD lights...");

                // R*-hash borrow (authoritative): the R* LightExtractionTool runs inside the game
                // engine, so its GenerateLODLightHash uses the real runtime AABB of the actual
                // (NVE-modified) models - the exact hash the game links against. That beats CW's
                // static AABB hash and the vanilla borrow (which copies UNMODDED prop hashes, wrong
                // for modded props). Runs first, locks each match so the vanilla borrow below won't
                // touch it; lights the R* set doesn't cover fall through to the vanilla borrow.
                int rborrowed = BorrowRockstarHashes(ROCKSTAR_IMAP_DIR, new[] { lightsSmall, lightsMedium, lightsLarge });
                if (rborrowed > 0)
                    UpdateStatus($"Borrowed {rborrowed} authoritative hashes from R* LightExtractionTool output ({ROCKSTAR_IMAP_DIR}).");

                // Vanilla-hash borrow: a fallback for when CW can't reproduce the runtime AABB (it
                // reuses a nearby vanilla LOD light's hash).
                int borrowed = BorrowVanillaHashes(gameFileCache, new[] { lightsSmall, lightsMedium, lightsLarge });
                UpdateStatus($"Borrowed {borrowed} hashes from vanilla LOD lights. Chopping into grid cells...");

                var categoryLights = new[] { lightsSmall, lightsMedium, lightsLarge };
                var allYmaps = new List<YmapFile>();

                for (int cat = 0; cat < 3; cat++)
                {
                    var lights = categoryLights[cat];
                    if (lights.Count == 0) continue;

                    var cells = ChopLightsIntoGrid(lights, LodVisRadius[cat]);

                    UpdateStatus($"Building {cells.Count} ymap pairs for {CategoryLabels[cat]} category...");

                    for (int ci = 0; ci < cells.Count; ci++)
                    {
                        var cell = cells[ci];

                        cell.Lights.Sort((a, b) =>
                        {
                            if (a.isStreetLight != b.isStreetLight) return b.isStreetLight.CompareTo(a.isStreetLight);
                            return a.hash.CompareTo(b.hash);
                        });

                        var (lodymap, distymap) = BuildYmapPair(cell.Lights, pname, cat, ci, distRange);
                        allYmaps.Add(lodymap);
                        allYmaps.Add(distymap);
                    }
                }

                UpdateStatus($"Saving {allYmaps.Count} ymaps to {outputDir}...");

                foreach (var ymap in allYmaps)
                {
                    var data = ymap.Save();
                    if (data != null)
                    {
                        var entry = ymap.RpfFileEntry ?? throw new InvalidOperationException("Generated light map has no archive entry.");
                        var filePath = Path.Combine(outputDir, entry.Name);
                        File.WriteAllBytes(filePath, data);
                    }
                }

                int totalYmaps = allYmaps.Count / 2;
                UpdateStatus($"Process complete. {totalLights} lights (S:{lightsSmall.Count} M:{lightsMedium.Count} L:{lightsLarge.Count}) - {borrowed} borrowed, in {totalYmaps} ymap pairs saved to {outputDir}");
                GenerateComplete();
            });
        }


        private List<YmapFile> LoadAllGameYmaps(GameFileCache gameFileCache)
        {
            var ymaps = new List<YmapFile>();

            // The active ymap dict misses the imap-group ymaps (_strm/_critical/_long files in the
            // *_metadata.rpf containers). Merge those in from the all-rpfs dict. Exclude DLC map
            // content (\dlcpacks\ + hei_/lr_ from update.rpf): R*'s LOD tool ran on base-game data
            // only, and DLC ships its own distant-lod-lights, so regenerating them makes wrong-hash
            // lights that never link.
            static bool IsDlc(RpfFileEntry fe)
            {
                var p = fe.Path ?? "";
                if (p.IndexOf("\\dlcpacks\\", StringComparison.OrdinalIgnoreCase) >= 0
                    || p.StartsWith("update\\x64\\dlcpacks", StringComparison.OrdinalIgnoreCase)) return true;
                var n = fe.GetShortNameLower();
                return n.StartsWith("hei_", StringComparison.Ordinal) || n.StartsWith("lr_", StringComparison.Ordinal);
            }

            var entryDict = new Dictionary<uint, RpfFileEntry>(gameFileCache.YmapDict.Count);
            foreach (var kvp in gameFileCache.YmapDict)
            {
                if (IsDlc(kvp.Value)) continue;
                entryDict[kvp.Key] = kvp.Value;
            }
            foreach (var kvp in gameFileCache.AllYmapsDict)
            {
                if (entryDict.ContainsKey(kvp.Key) || IsDlc(kvp.Value)) continue;
                entryDict[kvp.Key] = kvp.Value;
            }

            var entries = entryDict.Values.ToList();
            for (int i = 0; i < entries.Count; i++)
            {
                if ((i % 200) == 0) UpdateStatus($"Loading game ymaps... {i}/{entries.Count}");
                try
                {
                    // ponytail: load direct from RPF, bypassing the async cache - no eviction pressure, no wait loops
                    var ymap = gameFileCache.RpfMan.GetFile<YmapFile>(entries[i]);
                    if (ymap?.AllEntities == null || ymap.AllEntities.Length == 0) continue;
                    if ((ymap._CMapData.flags & 1) != 0) continue; //skip scripted ymaps - only loaded on demand by scripts
                    if ((ymap._CMapData.contentFlags & 1) == 0) continue; //R* tool: only ymaps with HD entities
                    if ((ymap._CMapData.contentFlags & 8) != 0) continue; //R* tool: skip interiors (MLO)
                    ymap.InitYmapEntityArchetypes(gameFileCache);
                    ymaps.Add(ymap);
                }
                catch { }
            }

            return ymaps;
        }


        // Borrow authoritative hashes from the R* LightExtractionTool .imap output. Harvests
        // (position -> hash) from each DistLODLights_*.imap (positions) + its LODLights_*.imap
        // (hashes), paired by array index, then greedily assigns each generated light the hash of
        // the nearest R* light within 1m (each R* hash lent once). Matched lights are hashLocked so
        // the subsequent vanilla borrow leaves them alone. Mirrors BorrowVanillaHashes' matching.
        private int BorrowRockstarHashes(string dir, List<Light>[] categoryLights)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return 0;

            var rPos = new List<Vector3>();
            var rHash = new List<uint>();
            foreach (var distPath in Directory.GetFiles(dir, "DistLODLights_*.imap"))
            {
                var lodPath = Path.Combine(dir, Path.GetFileName(distPath).Replace("DistLODLights_", "LODLights_"));
                if (!File.Exists(lodPath)) continue;
                try
                {
                    var posEl = XDocument.Load(distPath).Root?.Element("DistantLODLightsSOA")?.Element("position");
                    var hashEl = XDocument.Load(lodPath).Root?.Element("LODLightsSOA")?.Element("hash");
                    if (posEl == null || hashEl == null) continue;
                    var items = posEl.Elements("Item").ToList();
                    var hashes = (hashEl.Value ?? "").Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    int n = Math.Min(items.Count, hashes.Length);
                    for (int i = 0; i < n; i++)
                    {
                        if (!uint.TryParse(hashes[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var h)) continue;
                        float x = float.Parse(items[i].Element("x").Attribute("value").Value, CultureInfo.InvariantCulture);
                        float y = float.Parse(items[i].Element("y").Attribute("value").Value, CultureInfo.InvariantCulture);
                        float z = float.Parse(items[i].Element("z").Attribute("value").Value, CultureInfo.InvariantCulture);
                        rPos.Add(new Vector3(x, y, z));
                        rHash.Add(h);
                    }
                }
                catch { }
            }
            if (rHash.Count == 0) return 0;

            (int, int, int) CellKey(Vector3 p) => ((int)Math.Floor(p.X / 2.0f), (int)Math.Floor(p.Y / 2.0f), (int)Math.Floor(p.Z / 2.0f));
            var grid = new Dictionary<(int, int, int), List<int>>();
            for (int i = 0; i < rPos.Count; i++)
            {
                var k = CellKey(rPos[i]);
                if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<int>();
                l.Add(i);
            }

            const float MAX_BORROW_DIST = 1.0f;
            var pairs = new List<(float dist, Light light, int vi)>();
            foreach (var lights in categoryLights)
            {
                foreach (var light in lights)
                {
                    var p = new Vector3(light.position.x, light.position.y, light.position.z);
                    float bestDist = MAX_BORROW_DIST;
                    int best = -1;
                    var k = CellKey(p);
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dz = -1; dz <= 1; dz++)
                            {
                                if (!grid.TryGetValue((k.Item1 + dx, k.Item2 + dy, k.Item3 + dz), out var l)) continue;
                                foreach (var vi in l)
                                {
                                    var d = (rPos[vi] - p).Length();
                                    if (d < bestDist) { bestDist = d; best = vi; }
                                }
                            }
                    if (best >= 0) pairs.Add((bestDist, light, best));
                }
            }
            pairs.Sort((a, b) => a.dist.CompareTo(b.dist));
            var claimed = new HashSet<int>();
            int borrowed = 0;
            foreach (var (dist, light, vi) in pairs)
            {
                if (!claimed.Add(vi)) continue;
                light.hash = rHash[vi];
                light.hashLocked = true; // authoritative - stop the vanilla borrow from overwriting
                borrowed++;
            }
            return borrowed;
        }

        private int BorrowVanillaHashes(GameFileCache gameFileCache, List<Light>[] categoryLights)
        {
            // harvest (position, hash) from the BASE GAME lodlights ymap pairs (unprefixed
            // "lodlights_*.ymap"/"distlodlights_*.ymap") - these carry the hashes the runtime
            // actually links against. Keep every copy per name and use the first that actually
            // has lights, preferring base-game paths - mod packs (e.g. NVE) override these
            // names with EMPTY ymaps to disable the vanilla LOD lights.
            var lodByName = new Dictionary<string, List<RpfFileEntry>>(StringComparer.OrdinalIgnoreCase);
            var distByName = new Dictionary<string, List<RpfFileEntry>>(StringComparer.OrdinalIgnoreCase);
            void AddEntry(Dictionary<string, List<RpfFileEntry>> dict, string name, RpfFileEntry fe)
            {
                if (!dict.TryGetValue(name, out var l)) dict[name] = l = new List<RpfFileEntry>();
                l.Add(fe);
            }
            foreach (var rpf in gameFileCache.RpfMan.AllRpfs)
            {
                if (rpf?.AllEntries == null) continue;
                foreach (var entry in rpf.AllEntries)
                {
                    if (!(entry is RpfFileEntry fe)) continue;
                    var n = fe.NameLower;
                    if (n == null || !n.EndsWith(".ymap")) continue;
                    if (n.StartsWith("distlodlights_")) AddEntry(distByName, n, fe);
                    else if (n.StartsWith("lodlights_")) AddEntry(lodByName, n, fe);
                }
            }

            int PathRank(RpfFileEntry fe) //base game first, dlcpack/mods copies last
            {
                var p = fe.Path ?? "";
                return (p.StartsWith("mods\\", StringComparison.OrdinalIgnoreCase) ? 2 : 0)
                     + (p.IndexOf("dlcpacks", StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 0);
            }
            YmapFile LoadFirstWithContent(List<RpfFileEntry> cands, bool wantLod)
            {
                cands.Sort((a, b) => PathRank(a).CompareTo(PathRank(b)));
                foreach (var fe in cands)
                {
                    try
                    {
                        var ymap = gameFileCache.RpfMan.GetFile<YmapFile>(fe);
                        if (wantLod ? ((ymap?.LODLights?.hash?.Length ?? 0) > 0) : ((ymap?.DistantLODLights?.positions?.Length ?? 0) > 0))
                        {
                            return ymap;
                        }
                    }
                    catch { }
                }
                return null;
            }

            var vanPos = new List<Vector3>();
            var vanHash = new List<uint>();
            foreach (var kvp in lodByName)
            {
                if (!distByName.TryGetValue(kvp.Key.Replace("lodlights_", "distlodlights_"), out var dcands)) continue;
                var lod = LoadFirstWithContent(kvp.Value, true);
                var dist = LoadFirstWithContent(dcands, false);
                var ll = lod?.LODLights;
                var dl = dist?.DistantLODLights;
                if (ll?.hash == null || dl?.positions == null) continue;
                int cnt = Math.Min(ll.hash.Length, dl.positions.Length);
                for (int i = 0; i < cnt; i++)
                {
                    vanPos.Add(dl.positions[i].ToVector3());
                    vanHash.Add(ll.hash[i]);
                }
            }
            if (vanHash.Count == 0) return 0;

            // spatial hash over vanilla lights, 2m cells
            (int, int, int) CellKey(Vector3 p) => ((int)Math.Floor(p.X / 2.0f), (int)Math.Floor(p.Y / 2.0f), (int)Math.Floor(p.Z / 2.0f));
            var grid = new Dictionary<(int, int, int), List<int>>();
            for (int i = 0; i < vanPos.Count; i++)
            {
                var k = CellKey(vanPos[i]);
                if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<int>();
                l.Add(i);
            }

            // pair each generated light with its nearest vanilla light within 1m,
            // assigned greedily by distance so each vanilla hash is lent only once
            const float MAX_BORROW_DIST = 1.0f;
            var pairs = new List<(float dist, Light light, int vi)>();
            foreach (var lights in categoryLights)
            {
                foreach (var light in lights)
                {
                    if (light.hashLocked) continue; // already has the authoritative in-game hash
                    var p = new Vector3(light.position.x, light.position.y, light.position.z);
                    float bestDist = MAX_BORROW_DIST;
                    int best = -1;
                    var k = CellKey(p);
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dz = -1; dz <= 1; dz++)
                            {
                                if (!grid.TryGetValue((k.Item1 + dx, k.Item2 + dy, k.Item3 + dz), out var l)) continue;
                                foreach (var vi in l)
                                {
                                    var d = (vanPos[vi] - p).Length();
                                    if (d < bestDist) { bestDist = d; best = vi; }
                                }
                            }
                        }
                    }
                    if (best >= 0) pairs.Add((bestDist, light, best));
                }
            }
            pairs.Sort((a, b) => a.dist.CompareTo(b.dist));
            var claimed = new HashSet<int>();
            int borrowed = 0;
            foreach (var (dist, light, vi) in pairs)
            {
                if (!claimed.Add(vi)) continue;
                if (light.hash != vanHash[vi])
                {
                    light.hash = vanHash[vi];
                    borrowed++;
                }
            }
            return borrowed;
        }


        #region Grid Chopping

        private class GridCell
        {
            public float StartX;
            public float StartY;
            public float Width;
            public float Height;
            public List<Light> Lights = new List<Light>();
        }

        private static List<GridCell> ChopLightsIntoGrid(List<Light> lights, float lodVisRadius)
        {
            if (lights.Count == 0) return new List<GridCell>();

            // R* tool starts with one world-sized cell tightened to the light extents
            var cell = new GridCell();
            cell.Lights.AddRange(lights);
            UpdateCellExtentsFromLights(cell);

            var cells = new List<GridCell> { cell };

            SubdivideOverpopulatedCells(cells, lodVisRadius);
            ConsolidateSparseCells(cells);
            MakeCellsSquareIsh(cells);

            cells.RemoveAll(c => c.Lights.Count == 0);

            return cells;
        }

        private static void UpdateCellExtentsFromLights(GridCell cell)
        {
            if (cell.Lights.Count == 0) return;
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = -float.MaxValue, maxY = -float.MaxValue;
            foreach (var light in cell.Lights)
            {
                float x = light.position.x;
                float y = light.position.y;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
            cell.StartX = minX;
            cell.StartY = minY;
            cell.Width = maxX - minX;
            cell.Height = maxY - minY;
        }

        private static void SubdivideOverpopulatedCells(List<GridCell> cells, float lodVisRadius)
        {
            // Iterate including newly added cells.
            // R* tool only splits cells wider than the category's LOD visibility radius (width only, not height)
            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (cell.Lights.Count > MAX_LIGHTS_PER_CELL && cell.Width > lodVisRadius)
                {
                    var newCells = DivideCellVH(cell);
                    cells.AddRange(newCells);
                }
            }
            cells.RemoveAll(c => c.Lights.Count == 0);
        }

        private static List<GridCell> DivideCellVH(GridCell cell)
        {
            float halfW = cell.Width / 2.0f;
            float halfH = cell.Height / 2.0f;

            var tl = new GridCell { StartX = cell.StartX, StartY = cell.StartY, Width = halfW, Height = halfH };
            var bl = new GridCell { StartX = cell.StartX, StartY = cell.StartY + halfH, Width = halfW, Height = halfH };
            var tr = new GridCell { StartX = cell.StartX + halfW, StartY = cell.StartY, Width = halfW, Height = halfH };
            var br = new GridCell { StartX = cell.StartX + halfW, StartY = cell.StartY + halfH, Width = halfW, Height = halfH };

            // R* pushes TL,BL,TR,BR and (due to a copy-paste bug) never tightens BR's
            // extents - replicated so zone layouts match the original tool's output
            var newCells = new List<GridCell> { tl, bl, tr, br };
            MoveLightsToClosestCell(cell, newCells);
            UpdateCellExtentsFromLights(tl);
            UpdateCellExtentsFromLights(bl);
            UpdateCellExtentsFromLights(tr);
            return newCells;
        }

        private static List<GridCell> DivideCellVByCount(GridCell cell, int count)
        {
            float newWidth = cell.Width / count;
            var newCells = new List<GridCell>();
            for (int i = 0; i < count; i++)
            {
                newCells.Add(new GridCell
                {
                    StartX = cell.StartX + newWidth * i,
                    StartY = cell.StartY,
                    Width = newWidth,
                    Height = cell.Height
                });
            }
            MoveLightsToClosestCell(cell, newCells);
            foreach (var nc in newCells) UpdateCellExtentsFromLights(nc);
            return newCells;
        }

        private static List<GridCell> DivideCellHByCount(GridCell cell, int count)
        {
            float newHeight = cell.Height / count;
            var newCells = new List<GridCell>();
            for (int i = 0; i < count; i++)
            {
                newCells.Add(new GridCell
                {
                    StartX = cell.StartX,
                    StartY = cell.StartY + newHeight * i,
                    Width = cell.Width,
                    Height = newHeight
                });
            }
            MoveLightsToClosestCell(cell, newCells);
            foreach (var nc in newCells) UpdateCellExtentsFromLights(nc);
            return newCells;
        }

        private static void MoveLightsToClosestCell(GridCell source, List<GridCell> targets)
        {
            foreach (var light in source.Lights)
            {
                float x = light.position.x;
                float y = light.position.y;
                float bestDist = float.MaxValue;
                GridCell bestCell = targets[0];
                foreach (var target in targets)
                {
                    float dist = DistToRectSq(x, y, target);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestCell = target;
                    }
                }
                bestCell.Lights.Add(light);
            }
            source.Lights.Clear();
        }

        private static float DistToRectSq(float x, float y, GridCell cell)
        {
            float dx = 0, dy = 0;
            float minX = cell.StartX;
            float maxX = cell.StartX + cell.Width;
            float minY = cell.StartY;
            float maxY = cell.StartY + cell.Height;
            if (x < minX) dx = x - minX;
            else if (x > maxX) dx = x - maxX;
            if (y < minY) dy = y - minY;
            else if (y > maxY) dy = y - maxY;
            return dx * dx + dy * dy;
        }

        private static void ConsolidateSparseCells(List<GridCell> cells)
        {
            int nCells = cells.Count;
            for (int i = 0; i < nCells; i++)
            {
                var cell = cells[i];
                int nLights = cell.Lights.Count;
                if (nLights > 0 && nLights < MIN_LIGHT_COUNT_TO_CONSOLIDATE)
                {
                    var otherCells = new List<GridCell>();
                    for (int k = 0; k < nCells; k++)
                    {
                        if (k != i && cells[k].Lights.Count > MIN_LIGHT_COUNT_TO_CONSOLIDATE)
                        {
                            otherCells.Add(cells[k]);
                        }
                    }
                    if (otherCells.Count > 0)
                    {
                        // R* moves lights one at a time (in reverse), growing the target
                        // cell's extents after each move so later picks see the new extents
                        for (int j = cell.Lights.Count - 1; j >= 0; j--)
                        {
                            var light = cell.Lights[j];
                            float x = light.position.x;
                            float y = light.position.y;
                            float bestDist = float.MaxValue;
                            GridCell bestCell = otherCells[0];
                            foreach (var other in otherCells)
                            {
                                float dist = DistToRectSq(x, y, other);
                                if (dist < bestDist)
                                {
                                    bestDist = dist;
                                    bestCell = other;
                                }
                            }
                            bestCell.Lights.Add(light);
                            UpdateCellExtentsFromLights(bestCell);
                        }
                        cell.Lights.Clear();
                    }
                }
            }
            cells.RemoveAll(c => c.Lights.Count == 0);
        }

        private static void MakeCellsSquareIsh(List<GridCell> cells)
        {
            int nCells = cells.Count;
            for (int i = 0; i < nCells; i++)
            {
                var cell = cells[i];
                if (cell.Lights.Count == 0 || cell.Height <= 0 || cell.Width <= 0) continue;

                float whRatio = cell.Width / cell.Height;
                if (whRatio > 2.0f)
                {
                    var newCells = DivideCellVByCount(cell, (int)whRatio);
                    cells.AddRange(newCells);
                }
                else if (whRatio < 0.5f)
                {
                    var newCells = DivideCellHByCount(cell, (int)(1.0f / whRatio));
                    cells.AddRange(newCells);
                }
            }
            cells.RemoveAll(c => c.Lights.Count == 0);
        }

        #endregion


        private (YmapFile lodymap, YmapFile distymap) BuildYmapPair(List<Light> lights, string pname, int category, int cellIndex, float distRange)
        {
            var position = new List<MetaVECTOR3>();
            var colour = new List<uint>();
            var direction = new List<MetaVECTOR3>();
            var falloff = new List<float>();
            var falloffExponent = new List<float>();
            var timeAndStateFlags = new List<uint>();
            var hash = new List<uint>();
            var coneInnerAngle = new List<byte>();
            var coneOuterAngleOrCapExt = new List<byte>();
            var coronaIntensity = new List<byte>();
            ushort numStreetLights = 0;

            foreach (var light in lights)
            {
                position.Add(light.position);
                colour.Add(light.colour);
                direction.Add(light.direction);
                falloff.Add(light.falloff);
                falloffExponent.Add(light.falloffExponent);
                timeAndStateFlags.Add(light.timeAndStateFlags);
                hash.Add(light.hash);
                coneInnerAngle.Add(light.coneInnerAngle);
                coneOuterAngleOrCapExt.Add(light.coneOuterAngleOrCapExt);
                coronaIntensity.Add(light.coronaIntensity);
                if (light.isStreetLight) numStreetLights++;
            }

            string catLabel = CategoryLabels[category];

            var lodymap = new YmapFile();
            var distymap = new YmapFile();
            var ll = new YmapLODLights();
            var dl = new YmapDistantLODLights();
            var cdl = new CDistantLODLight();
            distymap.DistantLODLights = dl;
            lodymap.LODLights = ll;
            lodymap.Parent = distymap;
            cdl.category = (ushort)category;
            cdl.numStreetLights = numStreetLights;
            dl.CDistantLODLight = cdl;
            dl.positions = position.ToArray();
            dl.colours = colour.ToArray();
            dl.Ymap = distymap;
            dl.CalcBB();
            ll.direction = direction.ToArray();
            ll.falloff = falloff.ToArray();
            ll.falloffExponent = falloffExponent.ToArray();
            ll.timeAndStateFlags = timeAndStateFlags.ToArray();
            ll.hash = hash.ToArray();
            ll.coneInnerAngle = coneInnerAngle.ToArray();
            ll.coneOuterAngleOrCapExt = coneOuterAngleOrCapExt.ToArray();
            ll.coronaIntensity = coronaIntensity.ToArray();
            ll.Ymap = lodymap;
            ll.BuildLodLights(dl);
            ll.CalcBB();
            ll.BuildBVH();

            lodymap.CalcFlags();
            lodymap.CalcExtents();
            distymap.CalcFlags();
            distymap.CalcExtents();

            // R* CalcIMapExtents: streaming extents = light positions expanded by the category
            // visibility radius (CalcExtents hardcodes the medium radii, so override per category).
            // Physical extents approximated as position +/- falloff (R* uses the light entity AABBs).
            var posMin = new Vector3(float.MaxValue);
            var posMax = new Vector3(float.MinValue);
            var physMin = new Vector3(float.MaxValue);
            var physMax = new Vector3(float.MinValue);
            foreach (var light in lights)
            {
                var p = new Vector3(light.position.x, light.position.y, light.position.z);
                posMin = Vector3.Min(posMin, p);
                posMax = Vector3.Max(posMax, p);
                physMin = Vector3.Min(physMin, p - light.falloff);
                physMax = Vector3.Max(physMax, p + light.falloff);
            }
            float lodR = LodVisRadius[category];
            // distant lights render until their ymap streams out, so a larger radius keeps them
            // visible further than vanilla's ranges. small dist lights never render (the game
            // skips category 0 as "just noise") so only medium/large get the extension
            float distR = DistVisRadius[category];
            if (category != LIGHT_CATEGORY_SMALL)
            {
                distR = Math.Max(distR, distRange);
            }
            lodymap._CMapData.entitiesExtentsMin = physMin;
            lodymap._CMapData.entitiesExtentsMax = physMax;
            lodymap._CMapData.streamingExtentsMin = posMin - lodR;
            lodymap._CMapData.streamingExtentsMax = posMax + lodR;
            distymap._CMapData.entitiesExtentsMin = physMin;
            distymap._CMapData.entitiesExtentsMax = physMax;
            distymap._CMapData.streamingExtentsMin = posMin - distR;
            distymap._CMapData.streamingExtentsMax = posMax + distR;

            var lodname = $"{pname}_lodlights_{catLabel}{cellIndex:D3}";
            var distname = $"{pname}_distlodlights_{catLabel}{cellIndex:D3}";
            lodymap.Name = lodname;
            lodymap._CMapData.name = JenkHash.GenHash(lodname);
            lodymap.RpfFileEntry = new RpfResourceFileEntry();
            lodymap.RpfFileEntry.Name = lodname + ".ymap";
            lodymap.RpfFileEntry.NameLower = lodname + ".ymap";
            distymap.Name = distname;
            distymap._CMapData.name = JenkHash.GenHash(distname);
            distymap.RpfFileEntry = new RpfResourceFileEntry();
            distymap.RpfFileEntry.Name = distname + ".ymap";
            distymap.RpfFileEntry.NameLower = distname + ".ymap";

            lodymap._CMapData.parent = distymap._CMapData.name;
            lodymap.Loaded = true;
            distymap.Loaded = true;

            return (lodymap, distymap);
        }

        public class Light
        {
            public MetaVECTOR3 position { get; set; }
            public uint colour { get; set; }
            public MetaVECTOR3 direction { get; set; }
            public float falloff { get; set; }
            public float falloffExponent { get; set; }
            public uint timeAndStateFlags { get; set; }
            public uint hash { get; set; }
            public byte coneInnerAngle { get; set; }
            public byte coneOuterAngleOrCapExt { get; set; }
            public byte coronaIntensity { get; set; }
            public bool isStreetLight { get; set; }
            public bool hashLocked { get; set; } // hash came from the in-game sweep - authoritative, don't overwrite
        }
    }
}
