using CodeWalker.GameFiles;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace CodeWalker.World
{
    public class Space
    {

        public LinkedList<Entity> TemporaryEntities = new();
        public LinkedList<Entity> PersistentEntities = new();
        public List<Entity> EnabledEntities = new(); //built each frame

        private GameFileCache GameFileCache = null;

        public SpaceMapDataStore MapDataStore;
        public SpaceBoundsStore BoundsStore;

        private Dictionary<MetaHash, MetaHash> interiorLookup = new();
        private Dictionary<MetaHash, YmfInterior> interiorManifest = new();
        private Dictionary<SpaceBoundsKey, CInteriorProxy> interiorProxies = new();
        private Dictionary<MetaHash, YmfMapDataGroup> dataGroupDict = new();
        private Dictionary<MetaHash, MapDataStoreNode> nodedict = new();
        private Dictionary<SpaceBoundsKey, BoundsStoreItem> boundsdict = new();
        private Dictionary<MetaHash, BoundsStoreItem> usedboundsdict = new();

        private Dictionary<MetaHash, uint> ymaptimes = new();
        private Dictionary<MetaHash, MetaHash[]> ymapweathertypes = new();

        public bool Inited = false;


        public SpaceNodeGrid NodeGrid;
        private Dictionary<uint, YndFile> AllYnds = new();

        public SpaceNavGrid NavGrid;

        public List<SpaceEntityCollision> Collisions = new();
        private bool[] CollisionLayers = new[] { true, false, false };

        private int CurrentHour;
        private MetaHash CurrentWeather;


        public void Init(GameFileCache gameFileCache, Action<string> updateStatus)
        {
            GameFileCache = gameFileCache;


            updateStatus("Scanning manifests...");

            InitManifestData();


            updateStatus("Scanning caches...");

            InitCacheData();


            updateStatus("Building map data store...");

            InitMapDataStore();


            updateStatus("Building bounds store...");

            InitBoundsStore();


            updateStatus("Loading paths...");

            InitNodeGrid();


            updateStatus("Loading nav meshes...");

            InitNavGrid();


            Inited = true;
            updateStatus("World initialised.");
        }


        private void InitManifestData()
        {
            interiorLookup.Clear();
            interiorManifest.Clear();
            ymaptimes.Clear();
            ymapweathertypes.Clear();
            dataGroupDict.Clear();

            var manifests = GameFileCache.AllManifests;
            if (manifests == null) return;

            // Process manifests in parallel for better performance
            Lock lockObj = new();
            Parallel.ForEach(manifests, manifest =>
            {
                if (manifest == null) return;

                // Local collections to minimize lock contention
                var localInteriorLookup = new Dictionary<MetaHash, MetaHash>();
                var localInteriorManifest = new Dictionary<MetaHash, YmfInterior>();
                var localYmapTimes = new Dictionary<MetaHash, uint>();
                var localYmapWeatherTypes = new Dictionary<MetaHash, MetaHash[]>();
                var localDataGroupDict = new Dictionary<MetaHash, YmfMapDataGroup>();

                // Build interior lookup - maps child->parent interior bounds
                if (manifest.Interiors != null)
                {
                    foreach (var interior in manifest.Interiors)
                    {
                        if (interior?.Interior == null) continue;

                        var intname = interior.Interior.Name;
                        localInteriorManifest[intname] = interior;

                        if (interior.Bounds != null)
                        {
                            foreach (var intbound in interior.Bounds)
                            {
                                localInteriorLookup[intbound] = intname;
                            }
                        }
                    }
                }

                // Process dynamic "togglable" ymaps
                if (manifest.MapDataGroups != null)
                {
                    foreach (var mapgroup in manifest.MapDataGroups)
                    {
                        if (mapgroup == null) continue;

                        if (mapgroup.HoursOnOff != 0)
                        {
                            localYmapTimes[mapgroup.Name] = mapgroup.HoursOnOff;
                        }
                        if (mapgroup.WeatherTypes != null)
                        {
                            localYmapWeatherTypes[mapgroup.Name] = mapgroup.WeatherTypes;
                        }

                        // Always add/update - let the last one win
                        localDataGroupDict[mapgroup.DataGroup.Name] = mapgroup;
                    }
                }

                // Merge local results with minimal locking
                lock (lockObj)
                {
                    foreach (var kvp in localInteriorLookup) interiorLookup[kvp.Key] = kvp.Value;
                    foreach (var kvp in localInteriorManifest) interiorManifest[kvp.Key] = kvp.Value;
                    foreach (var kvp in localYmapTimes) ymaptimes[kvp.Key] = kvp.Value;
                    foreach (var kvp in localYmapWeatherTypes) ymapweathertypes[kvp.Key] = kvp.Value;
                    foreach (var kvp in localDataGroupDict) dataGroupDict[kvp.Key] = kvp.Value;
                }
            });
        }
        private void InitCacheData()
        {
            var caches = GameFileCache.AllCacheFiles;
            if (caches == null || caches.Count == 0) return;

            // Pre-calculate totals for optimal dictionary sizing
            int totalNodes = 0, totalInteriorProxies = 0, totalBoundsItems = 0, totalFileDates = 0;
            foreach (var c in caches)
            {
                if (c == null) continue;
                totalNodes += c.AllMapNodes?.Length ?? 0;
                totalInteriorProxies += c.AllCInteriorProxies?.Length ?? 0;
                totalBoundsItems += c.AllBoundsStoreItems?.Length ?? 0;
                totalFileDates += c.FileDates?.Length ?? 0;
            }

            // Pre-size dictionaries with 25% extra capacity to minimize resizing
            int capNodes = Math.Max(16, totalNodes + (totalNodes >> 2));
            int capBounds = Math.Max(16, totalBoundsItems + (totalBoundsItems >> 2));
            int capUsed = capBounds;
            int capInteriors = Math.Max(16, totalInteriorProxies + (totalInteriorProxies >> 2));
            int capDates1 = Math.Max(16, totalFileDates + (totalFileDates >> 2));

            nodedict = new Dictionary<MetaHash, MapDataStoreNode>(capNodes);
            boundsdict = new Dictionary<SpaceBoundsKey, BoundsStoreItem>(capBounds);
            usedboundsdict = new Dictionary<MetaHash, BoundsStoreItem>(capUsed);
            interiorProxies = new Dictionary<SpaceBoundsKey, CInteriorProxy>(capInteriors);

            var intlist = new List<BoundsStoreItem>(Math.Max(16, totalBoundsItems >> 3));
            var filedates = new Dictionary<MetaHash, CacheFileDate>(capDates1);
            var filedates2 = new Dictionary<uint, CacheFileDate>(capDates1);

            // Cache frequently accessed dictionaries to avoid repeated property access
            var ymapDict = GameFileCache.YmapDict;
            var ybnDict = GameFileCache.YbnDict;

            // Process caches in parallel for better performance
            Lock lockObj = new();
            Parallel.ForEach(caches, cache =>
            {
                if (cache == null) return;

                // Local collections to minimize lock contention
                var localNodeDict = new Dictionary<MetaHash, MapDataStoreNode>();
                var localInteriorProxies = new Dictionary<SpaceBoundsKey, CInteriorProxy>();
                var localBoundsDict = new Dictionary<SpaceBoundsKey, BoundsStoreItem>();
                var localUsedBoundsDict = new Dictionary<MetaHash, BoundsStoreItem>();
                List<BoundsStoreItem> localIntList = [];
                var localFileDates = new Dictionary<MetaHash, CacheFileDate>();
                var localFileDates2 = new Dictionary<uint, CacheFileDate>();

                // Process file dates
                var dates = cache.FileDates;
                if (dates != null)
                {
                    foreach (var fd in dates)
                    {
                        if (fd == null) continue;

                        // Use indexer for better performance than TryGetValue + conditional assignment
                        if (!localFileDates.TryGetValue(fd.FileName, out var existing1) || fd.TimeStamp >= existing1.TimeStamp)
                            localFileDates[fd.FileName] = fd;

                        if (!localFileDates2.TryGetValue(fd.FileID, out var existing2) || fd.TimeStamp >= existing2.TimeStamp)
                            localFileDates2[fd.FileID] = fd;
                    }
                }

                // Process map nodes
                var nodes = cache.AllMapNodes;
                if (nodes != null)
                {
                    foreach (var node in nodes)
                    {
                        if (node != null && ymapDict.ContainsKey(node.Name))
                        {
                            localNodeDict[node.Name] = node;
                        }
                    }
                }

                // Process interior proxies
                var proxies = cache.AllCInteriorProxies;
                if (proxies != null)
                {
                    foreach (var prx in proxies)
                    {
                        if (prx != null)
                        {
                            var key = new SpaceBoundsKey(prx.Name, prx.Position);
                            localInteriorProxies[key] = prx;
                        }
                    }
                }

                // Process bounds
                var bounds = cache.AllBoundsStoreItems;
                if (bounds != null)
                {
                    foreach (var item in bounds)
                    {
                        if (item == null || !ybnDict.ContainsKey(item.Name)) continue;

                        if (interiorLookup.TryGetValue(item.Name, out var inthash))
                        {
                            localIntList.Add(item);
                        }
                        else
                        {
                            var key = new SpaceBoundsKey(item.Name, item.Min);
                            localBoundsDict[key] = item;
                        }

                        localUsedBoundsDict[item.Name] = item;
                    }
                }

                // Merge local results into global dictionaries with minimal locking
                lock (lockObj)
                {
                    foreach (var kvp in localNodeDict) nodedict[kvp.Key] = kvp.Value;
                    foreach (var kvp in localInteriorProxies) interiorProxies[kvp.Key] = kvp.Value;
                    foreach (var kvp in localBoundsDict) boundsdict[kvp.Key] = kvp.Value;
                    foreach (var kvp in localUsedBoundsDict) usedboundsdict[kvp.Key] = kvp.Value;
                    foreach (var item in localIntList) intlist.Add(item);
                    
                    foreach (var kvp in localFileDates)
                    {
                        if (!filedates.TryGetValue(kvp.Key, out var existing) || kvp.Value.TimeStamp >= existing.TimeStamp)
                            filedates[kvp.Key] = kvp.Value;
                    }
                    
                    foreach (var kvp in localFileDates2)
                    {
                        if (!filedates2.TryGetValue(kvp.Key, out var existing) || kvp.Value.TimeStamp >= existing.TimeStamp)
                            filedates2[kvp.Key] = kvp.Value;
                    }
                }
            });

            // Process uncached ymap/ybn files (mostly mods) - this part remains sequential due to file I/O
            var maprpfs = GameFileCache.ActiveMapRpfFiles;
            if (maprpfs != null)
            {
                // Pre-filter entries to reduce string comparisons
                List<(RpfEntry entry, bool isYmap, bool isExtra)> uncachedEntries = [];

                var extrarpfs = GameFileCache.RpfMan?.ExtraRpfs;
                var extraset = extrarpfs is { Count: > 0 } ? new HashSet<RpfFile>(extrarpfs) : null;
                var extraymaps = new Dictionary<uint, RpfEntry>(); //to spot the same ymap shipped by two resources

                foreach (var maprpf in maprpfs.Values)
                {
                    var entries = maprpf?.AllEntries;
                    if (entries == null) continue;
                    var isextra = extraset?.Contains(maprpf) == true;

                    foreach (var entry in entries)
                    {
                        if (entry?.NameLower == null) continue;

                        if (entry.NameLower.EndsWith(".ymap", StringComparison.Ordinal))
                        {
                            var h = new MetaHash(entry.ShortNameHash);
                            if (!nodedict.ContainsKey(h))
                            {
                                uncachedEntries.Add((entry, true, isextra));
                            }
                            else if (isextra)
                            {
                                var node = nodedict[h];

                                //same rule as the uncached branch below, but these have a cache .dat node so the
                                //parent name is already known - no need to load the ymap to find out.
                                if ((node.ParentName.Hash != 0) && GameFileCache.DisabledYmaps.Contains(entry.ShortNameHash)
                                    && !GameFileCache.YmapDict.ContainsKey(node.ParentName))
                                {
                                    LodDiag.Report(entry.Path + ": skipped - the active DLC replaced this map variant and its LOD parent '" + node.ParentName.ToString() + "' isn't loaded.");
                                    nodedict.Remove(h);
                                    continue;
                                }

                                //the cached hierarchy (children, parent) still comes from the game's cache .dat,
                                //so this file's entity list has to match the ymap it replaced. Only worth
                                //reporting for ymaps that are actually part of a hierarchy - occlusion, grass
                                //and other standalone ymaps get replaced all the time and can't break anything.
                                if ((node.Children?.Length > 0) || (node.ParentName.Hash != 0))
                                {
                                    LodDiag.Report(entry.Path + " replaces a cached map node (" + (node.Children?.Length ?? 0).ToString() + " LOD children, parent '" + node.ParentName.ToString() + "') - its entity order must match the original or LOD links will be wrong.");
                                }
                            }

                            if (isextra && !extraymaps.TryAdd(entry.ShortNameHash, entry))
                            {
                                //two resources shipping the same ymap name - only one of them can win, and
                                //which one it is depends on folder scan order
                                var winner = GameFileCache.YmapDict.TryGetValue(h, out var wy) ? wy?.Path : null;
                                LodDiag.Report(entry.GetShortNameLower() + ".ymap is provided by more than one resource: " + extraymaps[entry.ShortNameHash].Path + " and " + entry.Path + (winner != null ? " (loaded: " + winner + ")" : ""));
                            }
                        }
                        else if (entry.NameLower.EndsWith(".ybn", StringComparison.Ordinal))
                        {
                            var ehash = new MetaHash(entry.ShortNameHash);
                            if (!usedboundsdict.ContainsKey(ehash) && !interiorLookup.ContainsKey(ehash))
                            {
                                uncachedEntries.Add((entry, false, isextra));
                            }
                        }
                    }
                }

                // Process uncached entries
                foreach (var (entry, isYmap, isExtra) in uncachedEntries)
                {
                    try
                    {
                        if (isYmap)
                        {
                            var ymap = GameFileCache.RpfMan.GetFile<YmapFile>(entry);
                            if (ymap != null)
                            {
                                //A DLC map changeset swapped this ymap's variant out (eg the vanilla id2_* set
                                //under mpheist) and its LOD parent went with it, but a mods/extra folder still
                                //provides this one. Registering it would drop orphaned geometry into the middle
                                //of the replacement variant's chain. Only skip when the parent really is gone -
                                //a resource that ships a whole self-consistent set (eg the vw_lodlights pair)
                                //supplies its own parent and stays.
                                var pymap = ymap._CMapData.parent;
                                if ((pymap != 0) && GameFileCache.DisabledYmaps.Contains(entry.ShortNameHash)
                                    && !GameFileCache.YmapDict.ContainsKey(pymap))
                                {
                                    LodDiag.Report(entry.Path + ": skipped - the active DLC replaced this map variant and its LOD parent '" + pymap.ToString() + "' isn't loaded.");
                                    continue;
                                }

                                //Key the node by the file name, like the game's map data store (and every
                                //GetYmap lookup) does. Ymaps whose internal CMapData.name doesn't match
                                //their file name - common in FiveM resources built from vanilla ymaps -
                                //would otherwise replace the cached node of that name, wiping out its
                                //child list and breaking the LOD hierarchy chain for the real ymap.
                                var dsn = new MapDataStoreNode(ymap) { Name = entry.ShortNameHash };
                                if (dsn.Name != 0)
                                    nodedict[dsn.Name] = dsn;

                                //only worth reporting for mod/resource ymaps - the game ships plenty of vanilla
                                //ymaps whose internal name doesn't match the file, and those work fine
                                if (isExtra && (ymap._CMapData.name != entry.ShortNameHash))
                                {
                                    LodDiag.Report(entry.Path + ": CMapData.name is '" + ymap._CMapData.name.ToString() + "' but the file is named '" + entry.GetShortNameLower() + "'. Anything referencing it as a LOD parent uses the file name, so the internal name must match.");
                                }
                                if ((ymap._CMapData.parent != 0) && !GameFileCache.YmapDict.ContainsKey(ymap._CMapData.parent))
                                {
                                    LodDiag.Report(entry.Path + ": LOD parent '" + ymap._CMapData.parent.ToString() + "' doesn't exist - its LOD chain stops here.");
                                }
                            }
                        }
                        else
                        {
                            var ybn = GameFileCache.RpfMan.GetFile<YbnFile>(entry);
                            if (ybn?.Bounds != null)
                            {
                                var ehash = new MetaHash(entry.ShortNameHash);
                                var item = new BoundsStoreItem(ybn.Bounds) { Name = ehash };
                                var key = new SpaceBoundsKey(ehash, item.Min);
                                boundsdict[key] = item;
                                usedboundsdict[ehash] = item;
                            }
                        }
                    }
                    catch
                    {
                        // Silently continue on file loading errors to maintain robustness
                    }
                }
            }
        }

        private void InitMapDataStore()
        {

            MapDataStore = new SpaceMapDataStore();

            MapDataStore.Init(nodedict.Values.ToList());

        }

        private void InitBoundsStore()
        {

            BoundsStore = new SpaceBoundsStore();

            BoundsStore.Init(boundsdict.Values.ToList());

        }

        private void InitNodeGrid()
        {

            NodeGrid = new SpaceNodeGrid();
            AllYnds.Clear();

            var rpfman = GameFileCache.RpfMan;
            Dictionary<uint, RpfFileEntry> yndentries = new();
            foreach (var rpffile in GameFileCache.BaseRpfs) //load nodes from base rpfs
            {
                AddRpfYnds(rpffile, yndentries);
            }
            if (GameFileCache.EnableDlc)
            {
                var updrpf = rpfman.FindRpfFile("update\\update.rpf"); //load nodes from patch area...
                if (updrpf != null)
                {
                    foreach (var rpffile in updrpf.Children)
                    {
                        AddRpfYnds(rpffile, yndentries);
                    }
                }
                foreach (var dlcrpf in GameFileCache.DlcActiveRpfs) //load nodes from current dlc rpfs
                {
                    if (dlcrpf.Path.StartsWith("x64")) continue; //don't override update.rpf YNDs with x64 ones! *hack
                    foreach (var rpffile in dlcrpf.Children)
                    {
                        AddRpfYnds(rpffile, yndentries);
                    }
                }
            }


            Vector3 corner = new(-8192, -8192, -2048);
            Vector3 cellsize = new(512, 512, 4096);

            for (int x = 0; x < NodeGrid.CellCountX; x++)
            {
                for (int y = 0; y < NodeGrid.CellCountY; y++)
                {
                    var cell = NodeGrid.Cells[x, y];
                    string fname = "nodes" + cell.ID + ".ynd";
                    uint fnhash = JenkHash.GenHash(fname);
                    RpfFileEntry? fentry = null;
                    if (yndentries.TryGetValue(fnhash, out fentry))
                    {
                        cell.Ynd = rpfman.GetFile<YndFile>(fentry);
                        cell.Ynd.BBMin = corner + (cellsize * new Vector3(x, y, 0));
                        cell.Ynd.BBMax = cell.Ynd.BBMin + cellsize;
                        cell.Ynd.CellX = x;
                        cell.Ynd.CellY = y;
                        cell.Ynd.Loaded = true;

                        AllYnds[fnhash] = cell.Ynd;


                        #region node flags test

                        //if (cell.Ynd == null) continue;
                        //if (cell.Ynd.NodeDictionary == null) continue;
                        //if (cell.Ynd.NodeDictionary.Nodes == null) continue;
                        //var na = cell.Ynd.NodeDictionary.Nodes;

                        //for (int i = 0; i < na.Length; i++)
                        //{
                        //    var node = na[i];

                        //    int nodetype = node.Unk25Type & 7;
                        //    int linkcount = node.Unk25Type >> 3;
                        //    int nxtlink = node.LinkID + linkcount;
                        //    if (i < na.Length - 1)
                        //    {
                        //        var nxtnode = na[i + 1];
                        //        if (nxtnode.LinkID != nxtlink)
                        //        { }
                        //    }
                        //    else
                        //    {
                        //        if (nxtlink != cell.Ynd.NodeDictionary.LinksCount)
                        //        { }
                        //    }

                        //    switch (node.Flags0)
                        //    {
                        //        case 0:
                        //        case 1:
                        //        case 2:
                        //        case 8:
                        //        case 10:
                        //        case 32:
                        //        case 34:
                        //        case 35:
                        //        case 40:
                        //        case 42:
                        //        case 66:
                        //        case 98:
                        //        case 129:
                        //        case 130:
                        //        case 162:
                        //        case 194:
                        //        case 226:
                        //            break;
                        //        default:
                        //            break;
                        //    }
                        //    switch (node.Flags1)
                        //    {
                        //        case 0:
                        //        case 1:
                        //        case 2:
                        //        case 3:
                        //        case 4:
                        //        case 16:
                        //        case 80:
                        //        case 112:
                        //        case 120:
                        //        case 121:
                        //        case 122:
                        //        case 128:
                        //        case 129:
                        //        case 136:
                        //        case 144:
                        //        case 152:
                        //        case 160:
                        //            break;
                        //        default:
                        //            break;
                        //    }

                        //}
                        #endregion

                    }
                }
            }

            //join the dots....
            //StringBuilder sb = new();
            List<EditorVertex> tverts = new();
            List<YndLink> tlinks = new();
            List<YndLink> nlinks = new();
            foreach (var ynd in AllYnds.Values)
            {
                BuildYndData(ynd, tverts, tlinks, nlinks);

                //sb.Append(ynd.nodestr);
            }

            //string str = sb.ToString();
        }

        public void PatchYndFile(YndFile ynd)
        {
            //ideally we should be able to revert to the vanilla ynd's after closing the project window,
            //but codewalker can always just be restarted, so who cares really
            NodeGrid.UpdateYnd(ynd);
        }

        private void AddRpfYnds(RpfFile rpffile, Dictionary<uint, RpfFileEntry> yndentries)
        {
            if (rpffile?.AllEntries == null) return;
            
            foreach (var entry in rpffile.AllEntries)
            {
                if (entry is RpfFileEntry fentry && entry.NameLower.EndsWith(".ynd", StringComparison.Ordinal))
                {
                    yndentries[entry.NameHash] = fentry;
                }
            }
        }

        public void BuildYndLinks(YndFile ynd, List<YndLink>? tlinks = null, List<YndLink>? nlinks = null)
        {
            var ynodes = ynd.Nodes;
            var nodes = ynd.NodeDictionary?.Nodes;
            var links = ynd.NodeDictionary?.Links;
            if ((ynodes == null) || (nodes == null) || (links == null)) return;

            int nodecount = ynodes.Length;


            //build the links arrays.
            tlinks ??= [];
            nlinks ??= [];
            tlinks.Clear();
            for (int i = 0; i < nodecount; i++)
            {
                nlinks.Clear();
                var node = ynodes[i];

                var linkid = node.LinkID;
                for (int l = 0; l < node.LinkCount; l++)
                {
                    var llid = linkid + l;
                    if (llid >= links.Length) continue;
                    var link = links[llid];
                    YndNode tnode;
                    if (link.AreaID == node.AreaID)
                    {
                        if (link.NodeID >= ynodes.Length)
                        { continue; }
                        tnode = ynodes[link.NodeID];
                    }
                    else
                    {
                        tnode = NodeGrid.GetYndNode(link.AreaID, link.NodeID);
                        if (tnode == null)
                        { continue; }
                        if ((Math.Abs(tnode.Ynd.CellX - ynd.CellX) > 1) || (Math.Abs(tnode.Ynd.CellY - ynd.CellY) > 1))
                        { /*continue;*/ } //non-adjacent cell? seems to be the carrier problem...
                    }

                    YndLink yl = new();
                    yl.Init(ynd, node, tnode, link);
                    tlinks.Add(yl);
                    nlinks.Add(yl);
                }
                node.Links = nlinks.ToArray();
            }
            ynd.Links = tlinks.ToArray();

        }
        public void BuildYndVerts(YndFile ynd, YndNode[] selectedNodes, List<EditorVertex>? tverts = null)
        {
            var laneColour = (uint)new Color4(0f, 0f, 1f, 1f).ToRgba();
            var ynodes = ynd.Nodes;
            if (ynodes == null) return;

            int nodecount = ynodes.Length;

            // Pre-calculate capacity for better performance
            int estimatedVertCount = 0;
            for (int i = 0; i < nodecount; i++)
            {
                var node = ynodes[i];
                if (node.Links != null)
                {
                    estimatedVertCount += node.Links.Length * 10; // Rough estimate
                }
            }

            tverts ??= new List<EditorVertex>(estimatedVertCount);
            tverts.Clear();
            if (estimatedVertCount > 0) tverts.Capacity = Math.Max(tverts.Capacity, estimatedVertCount);

            // Cache commonly used values
            const float arrowSize = 0.5f;
            const float negArrowSize = -0.5f;
            var unitZ = Vector3.UnitZ;

            for (int i = 0; i < nodecount; i++)
            {
                var node = ynodes[i];
                if (node.Links == null) continue;

                var nvert = new EditorVertex
                {
                    Position = node.Position,
                    Colour = (uint)node.Colour.ToRgba()
                };

                var links = node.Links;
                for (int l = 0; l < links.Length; l++)
                {
                    var yl = links[l];
                    var tnode = yl.Node2;
                    if (tnode == null) continue; // Invalid links

                    // Cache calculations
                    var laneDir = yl.GetDirection();
                    var laneDirCross = Vector3.Cross(laneDir, unitZ);
                    var laneWidth = yl.GetLaneWidth();
                    var laneHalfWidth = laneWidth * 0.5f;
                    var isTwoWay = yl.IsTwoWay();
                    var offset = isTwoWay
                        ? yl.LaneOffset * laneWidth - laneHalfWidth
                        : yl.LaneOffset - yl.LaneCountForward * laneWidth * 0.5f + laneHalfWidth;

                    var iOffset = isTwoWay ? 1 : 0;
                    var laneCountForward = yl.LaneCountForward;

                    var tvert = new EditorVertex
                    {
                        Position = tnode.Position,
                        Colour = (uint)tnode.Colour.ToRgba()
                    };

                    tverts.Add(nvert);
                    tverts.Add(tvert);

                    // Add lane display - batch vertex creation
                    var laneEndIndex = laneCountForward + iOffset;
                    for (int j = iOffset; j < laneEndIndex; j++)
                    {
                        var vertOffset = laneDirCross * (offset + laneWidth * j);
                        vertOffset.Z = 0.1f;

                        var lvert1 = new EditorVertex
                        {
                            Position = nvert.Position + vertOffset,
                            Colour = laneColour
                        };

                        var lvert2 = new EditorVertex
                        {
                            Position = tvert.Position + vertOffset,
                            Colour = laneColour
                        };

                        tverts.Add(lvert1);
                        tverts.Add(lvert2);

                        // Arrow - optimized vertex creation
                        var apos = lvert1.Position + laneDir * (yl.Distance * 0.5f);
                        var arrowVert1 = new EditorVertex { Position = apos, Colour = laneColour };
                        var arrowVert2 = new EditorVertex { Position = apos + laneDir * negArrowSize + laneDirCross * arrowSize, Colour = laneColour };
                        var arrowVert3 = new EditorVertex { Position = apos, Colour = laneColour };
                        var arrowVert4 = new EditorVertex { Position = apos + laneDir * negArrowSize + laneDirCross * negArrowSize, Colour = laneColour };

                        tverts.Add(arrowVert1);
                        tverts.Add(arrowVert2);
                        tverts.Add(arrowVert3);
                        tverts.Add(arrowVert4);
                    }
                }
            }
            ynd.LinkedVerts = tverts.ToArray();
            ynd.UpdateTriangleVertices(selectedNodes);
        }
        public void BuildYndJuncs(YndFile ynd)
        {
            //attach the junctions to the nodes.
            var yjuncs = ynd.Junctions;
            if (yjuncs != null)
            {
                var junccount = yjuncs.Length;
                for (int i = 0; i < junccount; i++)
                {
                    var junc = yjuncs[i];
                    var cell = NodeGrid.GetCell(junc.RefData.AreaID);
                    if ((cell == null) || (cell.Ynd == null) || (cell.Ynd.Nodes == null))
                    { continue; }

                    var jynd = cell.Ynd;
                    if (cell.Ynd != ynd) //junc in different ynd..? no hits here, except ynds in project..
                    {
                        if (cell.Ynd.AreaID == ynd.AreaID)
                        {
                            jynd = ynd;
                        }
                        else
                        { }
                    }

                    if (junc.RefData.NodeID >= jynd.Nodes.Length)
                    { continue; }

                    var jnode = jynd.Nodes[junc.RefData.NodeID];
                    jnode.Junction = junc;
                    jnode.HasJunction = true;
                }
            }

        }
        public void BuildYndData(YndFile ynd, List<EditorVertex>? tverts = null, List<YndLink>? tlinks = null, List<YndLink>? nlinks = null)
        {

            BuildYndLinks(ynd, tlinks, nlinks);

            BuildYndJuncs(ynd);

            BuildYndVerts(ynd, null, tverts);

        }

        public HashSet<YndFile> GetYndFilesThatDependOnYndFile(YndFile file)
        {
            HashSet<YndFile> result = new();
            int targetAreaID = file.AreaID; // Cache to avoid repeated property access

            foreach (var ynd in AllYnds.Values)
            {
                foreach (var link in ynd.Links)
                {
                    if (link.Node2.AreaID == targetAreaID)
                    {
                        result.Add(ynd);
                        break; // No need to check more links for this YndFile
                    }
                }
            }

            return result;
        }

        public void MoveYndArea(YndFile ynd, int desiredX, int desiredY)
        {
            var xDir = Math.Min(1, Math.Max(-1, desiredX - ynd.CellX));
            var yDir = Math.Min(1, Math.Max(-1, desiredY - ynd.CellY));
            var x = desiredX;
            var y = desiredY;

            if (xDir != 0)
            {
                while (x >= 0 && x <= 31)
                {
                    if (NodeGrid.Cells[x, y].Ynd == null)
                    {
                        break;
                    }
                    x += xDir;
                }
            }
            if (yDir != 0)
            {
                while (y >= 0 && y <= 31)
                {
                    if (NodeGrid.Cells[x, y].Ynd == null)
                    {
                        break;
                    }
                    y += yDir;
                }
            }

            var dx = x - ynd.CellX;
            var dy = y - ynd.CellY;
            var areaId = y * 32 + x;
            var areaIdorig = ynd.AreaID;
            var changed = ynd.AreaID != areaId;
            ynd.CellX = x;
            ynd.CellY = y;
            ynd.AreaID = areaId;
            ynd.Name = $"nodes{areaId}";
            if (changed)
            {
                var nodes = ynd.Nodes;
                if (nodes != null)
                {
                    for (int i = 0; i < nodes.Length; i++)
                    {
                        var node = nodes[i];
                        node.SetPosition(node.Position + new Vector3(512 * dx, 512 * dy, 0));
                        if (node.AreaID == areaIdorig)
                        {
                            node.AreaID = (ushort)areaId;
                        }
                    }
                }
                var links = ynd.Links;
                if (links != null)
                {
                    for (int i = 0; i < links.Length; i++)
                    {
                        var link = links[i];
                        if (link._RawData.AreaID == areaIdorig)
                        {
                            link._RawData.AreaID = (ushort)areaId;
                        }
                    }
                }
                var juncs = ynd.Junctions;
                if (juncs != null)
                {
                    for (int i = 0; i < juncs.Length; i++)
                    {
                        var junc = juncs[i];
                        junc.PositionX += (short)(512 * dx);
                        junc.PositionY += (short)(512 * dy);
                    }
                }
                ynd.UpdateAllNodePositions();
                ynd.UpdateBoundingBox();
                ynd.UpdateTriangleVertices(null);
                ynd.BuildStructs();
            }
            NodeGrid.UpdateYnd(ynd);
        }

        public void RecalculateAllYndIndices()
        {
            foreach (var yndFile in AllYnds.Values)
            {
                yndFile.RecalculateNodeIndices();
            }
        }


        private void InitNavGrid()
        {
            NavGrid = new SpaceNavGrid();

            var rpfman = GameFileCache.RpfMan;
            Dictionary<uint, RpfFileEntry> ynventries = new();
            foreach (var rpffile in GameFileCache.BaseRpfs) //load navmeshes from base rpfs
            {
                AddRpfYnvs(rpffile, ynventries);
            }
            if (GameFileCache.EnableDlc)
            {
                var updrpf = rpfman.FindRpfFile("update\\update.rpf"); //load navmeshes from patch area...
                if (updrpf != null)
                {
                    foreach (var rpffile in updrpf.Children)
                    {
                        AddRpfYnvs(rpffile, ynventries);
                    }
                }
                foreach (var dlcrpf in GameFileCache.DlcActiveRpfs) //load navmeshes from current dlc rpfs
                {
                    foreach (var rpffile in dlcrpf.Children)
                    {
                        AddRpfYnvs(rpffile, ynventries);
                    }
                }
            }


            for (int x = 0; x < NavGrid.CellCountX; x++)
            {
                for (int y = 0; y < NavGrid.CellCountY; y++)
                {
                    var cell = NavGrid.Cells[x, y];
                    string fname = "navmesh[" + cell.FileX.ToString() + "][" + cell.FileY.ToString() + "].ynv";
                    uint fnhash = JenkHash.GenHash(fname);
                    RpfFileEntry? fentry = null;
                    if (ynventries.TryGetValue(fnhash, out fentry))
                    {
                        cell.YnvEntry = fentry as RpfResourceFileEntry;
                        //cell.Ynv = rpfman.GetFile<YnvFile>(fentry);
                    }
                }
            }

        }

        private void AddRpfYnvs(RpfFile rpffile, Dictionary<uint, RpfFileEntry> ynventries)
        {
            if (rpffile?.AllEntries == null) return;
            
            foreach (var entry in rpffile.AllEntries)
            {
                if (entry is RpfFileEntry fentry && entry.NameLower.EndsWith(".ynv", StringComparison.Ordinal))
                {
                    ynventries[entry.NameHash] = fentry;
                }
            }
        }



        public void Update(float elapsed)
        {
            if (!Inited) return;
            if (BoundsStore == null) return;

            if (elapsed > 0.1f) elapsed = 0.1f;


            Collisions.Clear();


            EnabledEntities.Clear();
            foreach (var e in PersistentEntities)
            {
                if (e.Enabled) EnabledEntities.Add(e);
            }
            foreach (var e in TemporaryEntities)
            {
                if (e.Enabled) EnabledEntities.Add(e);
            }



            float gravamt = -9.8f;
            Vector3 dvgrav = new(0, 0, gravamt * elapsed); //gravity acceleration vector
            dvgrav += (0.5f * dvgrav * elapsed); //v = ut+0.5at^2 !
            float minvel = 0.5f; // stop bouncing when slow...

            foreach (var e in EnabledEntities)
            {
                if (!e.Enabled) continue;

                e.Velocity += dvgrav; //apply gravity
                e.Momentum = e.Velocity * e.Mass;
                e.Age += elapsed;

                e.PreUpdate(elapsed);

                if (e.EnableCollisions)
                {
                    var coll = FindFirstCollision(e, elapsed);

                    if (coll.Hit)
                    {
                        Collisions.Add(coll);

                        float argvel = Math.Abs((e.Velocity - dvgrav).Length());

                        if (e.WasColliding && (argvel < minvel))
                        {
                            e.Velocity = Vector3.Zero;
                            e.Momentum = Vector3.Zero;
                        }
                        else
                        {
                            e.Position = coll.PrePos; //move to the last known position before collision

                            //bounce...
                            int maxbounce = 5;
                            int curbounce = 0;
                            float trem = 1.0f - coll.PreT;
                            while (trem > 0)
                            {
                                float vl = e.Velocity.Length();
                                float erem = elapsed * trem;
                                float drem = vl * erem;
                                Vector3 hitn = coll.SphereHit.Normal;
                                Vector3 bdir = Vector3.Reflect(coll.HitVelDir, hitn);
                                Vector3 newvel = bdir * (vl * 0.5f); //restitution/bouncyness
                                e.Velocity = newvel;

                                coll = FindFirstCollision(e, erem);

                                if (!coll.Hit)
                                {
                                    e.Position = coll.HitPos;//no hit, all done
                                    break;
                                }

                                Collisions.Add(coll);

                                e.Position = coll.PrePos;

                                trem = Math.Max(trem * (1.0f - coll.PreT), 0);

                                curbounce++;
                                if (curbounce >= maxbounce)
                                {
                                    e.Position = coll.HitPos;
                                    break;
                                }


                                //if ((coll.PreT <= 0))// || (coll.SphereHit.Normal == hitn))
                                //{
                                //    //ae.Velocity = Vector3.Zero; //same collision twice? abort?
                                //    break;
                                //}
                            }

                            e.Momentum = e.Velocity * e.Mass;
                        }
                        e.WasColliding = true;
                    }
                    else
                    {
                        e.Position = coll.HitPos; //hit pos is the end pos if no hit
                        e.WasColliding = false;
                    }
                }

                if (e.EntityDef != null)
                {
                    e.EntityDef.Position = e.Position;
                }


                if ((e.Lifetime > 0.0f) && (e.Age > e.Lifetime))
                {
                    TemporaryEntities.Remove(e);
                }

            }


        }


        public SpaceEntityCollision FindFirstCollision(Entity e, float elapsed)
        {
            SpaceEntityCollision r = new();
            r.Entity = e;

            Vector3 pos = e.Position;
            Vector3 sphpos = pos + e.Center;
            Vector3 disp = e.Velocity * elapsed;
            float absdisp = disp.Length();

            r.HitVelDir = Vector3.Normalize(disp);
            r.HitPos = pos + disp;
            r.HitVel = e.Velocity;
            r.HitT = 1.0f;
            r.PreT = 0.0f;
            r.PrePos = pos;

            BoundingSphere sph = new(r.HitPos + e.Center, e.Radius);

            r.SphereHit = SphereIntersect(sph, CollisionLayers);

            if (!r.SphereHit.Hit)
            {
                if (absdisp > e.Radius) //fast-moving... do a ray test to make sure it's not tunnelling
                {
                    Ray rayt = new(sphpos, r.HitVelDir);
                    float rayl = absdisp + e.Radius * 4.0f; //include some extra incase of glancing hit
                    var rayhit = RayIntersect(rayt, rayl);
                    if (rayhit.Hit) //looks like it is tunnelling... need to find the sphere hit point
                    {
                        sph.Center = rayhit.Position - (r.HitVelDir*Math.Min(e.Radius*0.5f, rayhit.HitDist));
                        float hitd = rayhit.HitDist;
                        r.HitT = hitd / absdisp;
                        if (r.HitT > 1.0f)
                        {
                            r.HitT = 1.0f;
                            sph.Center = r.HitPos + e.Center; //this really shouldn't happen... but just in case of glancing hit..
                        }

                        r.SphereHit = SphereIntersect(sph, CollisionLayers); //this really should be a hit!
                    }
                }
            }
            
            if (r.SphereHit.Hit)
            {
                int maxiter = 6;//(would be better to iterate until error within tolerance..)
                int curiter = 0;
                float curt = r.HitT * 0.5f;
                float step = curt * 0.5f;
                float minstep = 0.05f;
                while (curiter < maxiter) //iterate to find a closer hit time... improve this!
                {
                    sph.Center = sphpos + disp * curt;
                    var tcollres = SphereIntersect(sph, CollisionLayers);
                    if (tcollres.Hit)
                    {
                        r.HitT = curt;
                        r.HitPos = sph.Center - e.Center;
                        r.SphereHit = tcollres; //only use the best hit (ignore misses)
                        r.HitNumber = curiter;
                    }
                    else
                    {
                        r.PreT = curt;
                        r.PrePos = sph.Center - e.Center;
                    }
                    curiter++;
                    if (curiter < maxiter)
                    {
                        curt += step * (tcollres.Hit ? -1.0f : 1.0f);
                        step *= 0.5f;
                    }
                    if (absdisp * step < minstep)
                    {
                        break;
                    }
                }
            }

            r.Hit = r.SphereHit.Hit;

            return r;
        }


        public void AddTemporaryEntity(Entity e)
        {
            e.Space = this;
            while (TemporaryEntities.Count > 100)
            {
                TemporaryEntities.RemoveFirst();//don't be too laggy
            }
            TemporaryEntities.AddLast(e);
        }

        public void AddPersistentEntity(Entity e)
        {
            e.Space = this;
            PersistentEntities.AddLast(e);
        }

        public void RemovePersistentEntity(Entity e)
        {
            PersistentEntities.Remove(e);
        }



        private bool IsYmapAvailable(uint ymaphash, int hour, MetaHash weather)
        {
            MetaHash ymapname = new(ymaphash);
            uint ymaptime;
            MetaHash[]? weathers;
            if ((hour >= 0) && (hour <= 23))
            {
                if (ymaptimes.TryGetValue(ymapname, out ymaptime))
                {
                    uint mask = 1u << hour;
                    if ((ymaptime & mask) == 0) return false;
                }
            }
            if (weather.Hash != 0)
            {
                if (ymapweathertypes.TryGetValue(ymapname, out weathers))
                {
                    for (int i = 0; i < weathers.Length; i++)
                    {
                        if (weathers[i] == weather) return true;
                    }
                    return false;
                }
            }
            return true;
        }

        public void GetVisibleYmaps(Camera cam, int hour, MetaHash weather, Dictionary<MetaHash, YmapFile> ymaps)
        {
            if (!Inited || MapDataStore == null) return;
            
            CurrentHour = hour;
            CurrentWeather = weather;
            var items = MapDataStore.GetItems(ref cam.Position);

            // Create a snapshot to avoid collection modified exception from concurrent access
            var itemsSnapshot = items.ToArray();

            // Pre-filter items and batch process for better performance
            var validItems = new List<MapDataStoreNode>(itemsSnapshot.Length);
            foreach (var item in itemsSnapshot)
            {
                if (item != null && item.Name > 0 && !ymaps.ContainsKey(item.Name))
                {
                    validItems.Add(item);
                }
            }

            // Process valid items
            foreach (var item in validItems)
            {
                var hash = item.Name;
                var processedHashes = new HashSet<MetaHash>(); // Prevent infinite loops
                
                var ymap = GameFileCache.GetYmap(hash);
                while (ymap != null && ymap.Loaded && !processedHashes.Contains(hash))
                {
                    processedHashes.Add(hash);
                    
                    if (!IsYmapAvailable(hash, hour, weather)) break;
                    if (ymaps.ContainsKey(hash)) break;
                    
                    ymaps[hash] = ymap;
                    var child = ymap;
                    hash = ymap._CMapData.parent;

                    if (hash == 0) break;
                    ymap = GameFileCache.GetYmap(hash);
                    if (ymap == null)
                    {
                        //the chain stops here, so nothing in the child ymap will ever get LOD-linked upwards
                        LodDiag.Report(child.Name + ": LOD parent '" + hash.ToString() + "' not found - chain broken.");
                    }
                }
            }
        }


        public void GetVisibleBounds(Camera cam, int gridrange, bool[] layers, List<BoundsStoreItem> boundslist)
        {
            if (!Inited) return;

            if (BoundsStore == null) return;
            float dist = 50.0f * gridrange;
            var pos = cam.Position;
            var min = pos - dist;
            var max = pos + dist;
            var items = BoundsStore.GetItems(ref min, ref max, layers);
            boundslist.AddRange(items);
        }


        public void GetVisibleYnds(Camera cam, List<YndFile> ynds)
        {
            if (!Inited) return;
            if (NodeGrid == null) return;

            //int x = 9;
            //int y = 15; //== nodes489.ynd

            //ynds.Add(NodeGrid.Cells[x, y].Ynd);

            ynds.AddRange(AllYnds.Values);

        }


        public void GetVisibleYnvs(Camera cam, int gridrange, List<YnvFile> ynvs)
        {
            if (!Inited || NavGrid == null) return;

            ynvs.Clear();

            var pos = NavGrid.GetCellPos(cam.Position);
            
            // Clamp bounds once for better performance
            int minx = Math.Max(pos.X - gridrange, 0);
            int maxx = Math.Min(pos.X + gridrange, NavGrid.CellCountX - 1);
            int miny = Math.Max(pos.Y - gridrange, 0);
            int maxy = Math.Min(pos.Y + gridrange, NavGrid.CellCountY - 1);

            // Pre-size the list for better performance
            int estimatedCount = (maxx - minx + 1) * (maxy - miny + 1);
            if (ynvs.Capacity < estimatedCount)
            {
                ynvs.Capacity = estimatedCount;
            }

            // Cache cells array reference
            var cells = NavGrid.Cells;
            
            for (int x = minx; x <= maxx; x++)
            {
                for (int y = miny; y <= maxy; y++)
                {
                    var cell = cells[x, y];
                    if (cell?.YnvEntry != null)
                    {
                        var hash = cell.YnvEntry.ShortNameHash;
                        if (hash > 0)
                        {
                            var ynv = GameFileCache.GetYnv(hash);
                            if (ynv?.Loaded == true)
                            {
                                ynvs.Add(ynv);
                            }
                        }
                    }
                }
            }
        }


        public SpaceRayIntersectResult RayIntersect(Ray ray, float maxdist = float.MaxValue, bool[]? layers = null, bool testDrawableCollisions = true)
        {
            var res = new SpaceRayIntersectResult();
            if (GameFileCache == null) return res;
            bool testcomplete = true;
            res.HitDist = maxdist;
            var box = new BoundingBox();
            float boxhitdisttest;

            if ((BoundsStore == null) || (MapDataStore == null)) return res;

            var boundslist = BoundsStore.GetItems(ref ray, layers);

            for (int i = 0; i < boundslist.Count; i++)
            {
                var bound = boundslist[i];
                box.Minimum = bound.Min;
                box.Maximum = bound.Max;
                if (ray.Intersects(ref box, out boxhitdisttest))
                {
                    if (boxhitdisttest > res.HitDist)
                    { continue; } //already a closer hit

                    YbnFile ybn = GameFileCache.GetYbn(bound.Name);
                    if (ybn == null)
                    { continue; } //ybn not found?
                    if (!ybn.Loaded)
                    { testcomplete = false; continue; } //ybn not loaded yet...

                    var b = ybn.Bounds;
                    if (b == null)
                    { continue; }

                    var bhit = b.RayIntersect(ref ray, res.HitDist);
                    if (bhit.Hit)
                    {
                        bhit.HitYbn = ybn;
                    }
                    res.TryUpdate(ref bhit);
                }
            }

            if (!testDrawableCollisions)
            {
                if (res.Hit)
                {
                    res.Position = ray.Position + ray.Direction * res.HitDist;
                }
                res.TestComplete = testcomplete;
                return res;
            }

            var mapdatalist = MapDataStore.GetItems(ref ray);

            for (int i = 0; i < mapdatalist.Count; i++)
            {
                var mapdata = mapdatalist[i];
                if (mapdata == null)
                {
                    continue;
                }
                if ((mapdata.ContentFlags & 1) == 0)
                { continue; } //only test HD ymaps

                box.Minimum = mapdata.entitiesExtentsMin;
                box.Maximum = mapdata.entitiesExtentsMax;
                if (ray.Intersects(ref box, out boxhitdisttest))
                {
                    if (boxhitdisttest > res.HitDist)
                    { continue; } //already a closer hit

                    var hash = mapdata.Name;
                    var ymap = (hash > 0) ? GameFileCache.GetYmap(hash) : null;
                    if ((ymap != null) && (ymap.Loaded) && (ymap.AllEntities != null))
                    {
                        if (!IsYmapAvailable(hash, CurrentHour, CurrentWeather))
                        { continue; }

                        for (int e = 0; e < ymap.AllEntities.Length; e++)
                        {
                            var ent = ymap.AllEntities[e];

                            if (!EntityCollisionsEnabled(ent))
                            { continue; }

                            box.Minimum = ent.BBMin;
                            box.Maximum = ent.BBMax;
                            if (ray.Intersects(ref box, out boxhitdisttest))
                            {
                                if (boxhitdisttest > res.HitDist)
                                { continue; } //already a closer hit

                                if (ent.IsMlo)
                                {
                                    var ihit = RayIntersectInterior(ref ray, ent, res.HitDist);
                                    res.TryUpdate(ref ihit);
                                }
                                else
                                {
                                    var ehit = RayIntersectEntity(ref ray, ent, res.HitDist);
                                    res.TryUpdate(ref ehit);
                                }
                            }
                        }
                    }
                    else if ((ymap != null) && (!ymap.Loaded))
                    {
                        testcomplete = false;
                    }
                }
            }




            if (res.Hit)
            {
                res.Position = ray.Position + ray.Direction * res.HitDist;
            }

            res.TestComplete = testcomplete;

            return res;
        }
        public SpaceRayIntersectResult RayIntersectEntity(ref Ray ray, YmapEntityDef ent, float maxdist = float.MaxValue)
        {
            var res = new SpaceRayIntersectResult();
            res.HitDist = maxdist;

            var drawable = GameFileCache.TryGetDrawable(ent.Archetype);
            if (drawable != null)
            {
                var eori = ent.Orientation;
                var eorinv = Quaternion.Invert(ent.Orientation);
                var eray = new Ray();
                eray.Position = eorinv.Multiply(ray.Position - ent.Position);
                eray.Direction = eorinv.Multiply(ray.Direction);

                if ((drawable is Drawable sdrawable) && (sdrawable.Bound != null))
                {
                    var dhit = sdrawable.Bound.RayIntersect(ref eray, res.HitDist);
                    if (dhit.Hit)
                    {
                        dhit.Position = eori.Multiply(dhit.Position) + ent.Position;
                        dhit.Normal = eori.Multiply(dhit.Normal);
                    }
                    res.TryUpdate(ref dhit);
                }
                else if (drawable is FragDrawable fdrawable)
                {
                    if (fdrawable.Bound != null)
                    {
                        var fhit = fdrawable.Bound.RayIntersect(ref eray, res.HitDist);
                        if (fhit.Hit)
                        {
                            fhit.Position = eori.Multiply(fhit.Position) + ent.Position;
                            fhit.Normal = eori.Multiply(fhit.Normal);
                        }
                        res.TryUpdate(ref fhit);
                    }
                    var fbound = fdrawable.OwnerFragment?.PhysicsLODGroup?.PhysicsLOD1?.Bound;
                    if (fbound != null)
                    {
                        var fhit = fbound.RayIntersect(ref eray, res.HitDist);//TODO: these probably have extra transforms..!
                        if (fhit.Hit)
                        {
                            fhit.Position = eori.Multiply(fhit.Position) + ent.Position;
                            fhit.Normal = eori.Multiply(fhit.Normal);
                        }
                        res.TryUpdate(ref fhit);
                    }
                }
            }
            if (res.Hit)
            {
                res.HitEntity = ent;
            }

            return res;
        }
        public SpaceRayIntersectResult RayIntersectInterior(ref Ray ray, YmapEntityDef mlo, float maxdist = float.MaxValue)
        {
            var res = new SpaceRayIntersectResult();
            res.HitDist = maxdist;

            if (mlo.Archetype == null)
            { return res; }

            var iori = mlo.Orientation;
            var iorinv = Quaternion.Invert(mlo.Orientation);
            var iray = new Ray();
            iray.Position = iorinv.Multiply(ray.Position - mlo.Position);
            iray.Direction = iorinv.Multiply(ray.Direction);

            var hash = mlo.Archetype.Hash;
            var ybn = GameFileCache.GetYbn(hash);
            if ((ybn != null) && (ybn.Loaded))
            {
                var ihit = ybn.Bounds.RayIntersect(ref iray, res.HitDist);
                if (ihit.Hit)
                {
                    ihit.HitYbn = ybn;
                    ihit.HitEntity = mlo;
                    ihit.Position = iori.Multiply(ihit.Position) + mlo.Position;
                    ihit.Normal = iori.Multiply(ihit.Normal);
                }
                res.TryUpdate(ref ihit);
            }

            var mlodat = mlo.MloInstance;
            if (mlodat == null)
            { return res; }

            var box = new BoundingBox();
            float boxhitdisttest;

            if (mlodat.Entities != null)
            {
                for (int j = 0; j < mlodat.Entities.Length; j++) //should really improve this by using rooms!
                {
                    var intent = mlodat.Entities[j];
                    if (intent.Archetype == null) continue; //missing archetype...

                    if (!EntityCollisionsEnabled(intent))
                    { continue; }

                    box.Minimum = intent.BBMin;
                    box.Maximum = intent.BBMax;
                    if (ray.Intersects(ref box, out boxhitdisttest))
                    {
                        if (boxhitdisttest > res.HitDist)
                        { continue; } //already a closer hit

                        var ehit = RayIntersectEntity(ref ray, intent, res.HitDist);
                        res.TryUpdate(ref ehit);
                    }
                }
            }
            if (mlodat.EntitySets != null)
            {
                for (int e = 0; e < mlodat.EntitySets.Length; e++)
                {
                    var entityset = mlodat.EntitySets[e];
                    if (!entityset.Visible) continue;
                    var entities = entityset.Entities;
                    if (entities == null) continue;
                    for (int i = 0; i < entities.Count; i++) //should really improve this by using rooms!
                    {
                        var intent = entities[i];
                        if (intent.Archetype == null) continue; //missing archetype...

                        if (!EntityCollisionsEnabled(intent))
                        { continue; }

                        box.Minimum = intent.BBMin;
                        box.Maximum = intent.BBMax;
                        if (ray.Intersects(ref box, out boxhitdisttest))
                        {
                            if (boxhitdisttest > res.HitDist)
                            { continue; } //already a closer hit

                            var ehit = RayIntersectEntity(ref ray, intent, res.HitDist);
                            res.TryUpdate(ref ehit);
                        }
                    }
                }
            }

            return res;
        }

        public SpaceSphereIntersectResult SphereIntersect(BoundingSphere sph, bool[]? layers = null)
        {
            var res = new SpaceSphereIntersectResult();
            if (GameFileCache == null) return res;
            bool testcomplete = true;
            Vector3 sphmin = sph.Center - sph.Radius;
            Vector3 sphmax = sph.Center + sph.Radius;
            var box = new BoundingBox();

            if ((BoundsStore == null) || (MapDataStore == null)) return res;

            var boundslist = BoundsStore.GetItems(ref sphmin, ref sphmax, layers);
            var mapdatalist = MapDataStore.GetItems(ref sphmin, ref sphmax);

            for (int i = 0; i < boundslist.Count; i++)
            {
                var bound = boundslist[i];
                box.Minimum = bound.Min;
                box.Maximum = bound.Max;
                if (sph.Intersects(ref box))
                {
                    YbnFile ybn = GameFileCache.GetYbn(bound.Name);
                    if (ybn == null)
                    { continue; } //ybn not found?
                    if (!ybn.Loaded)
                    { testcomplete = false; continue; } //ybn not loaded yet...

                    var b = ybn.Bounds;
                    if (b == null)
                    { continue; }

                    var bhit = b.SphereIntersect(ref sph);
                    res.TryUpdate(ref bhit);
                }
            }

            for (int i = 0; i < mapdatalist.Count; i++)
            {
                var mapdata = mapdatalist[i];
                if ((mapdata.ContentFlags & 1) == 0)
                { continue; } //only test HD ymaps

                box.Minimum = mapdata.entitiesExtentsMin;
                box.Maximum = mapdata.entitiesExtentsMax;
                if (sph.Intersects(ref box))
                {
                    var hash = mapdata.Name;
                    var ymap = (hash > 0) ? GameFileCache.GetYmap(hash) : null;
                    if ((ymap != null) && (ymap.Loaded) && (ymap.AllEntities != null))
                    {
                        if (!IsYmapAvailable(hash, CurrentHour, CurrentWeather))
                        { continue; }

                        for (int e = 0; e < ymap.AllEntities.Length; e++)
                        {
                            var ent = ymap.AllEntities[e];

                            if (!EntityCollisionsEnabled(ent))
                            { continue; }

                            box.Minimum = ent.BBMin;
                            box.Maximum = ent.BBMax;
                            if (sph.Intersects(ref box))
                            {
                                if (ent.IsMlo)
                                {
                                    var ihit = SphereIntersectInterior(ref sph, ent);
                                    res.TryUpdate(ref ihit);
                                }
                                else
                                {
                                    var ehit = SphereIntersectEntity(ref sph, ent);
                                    res.TryUpdate(ref ehit);
                                }
                            }
                        }
                    }
                    else if ((ymap != null) && (!ymap.Loaded))
                    {
                        testcomplete = false;
                    }
                }
            }


            //if (hit)
            //{
            //    hitpos = ray.Position + ray.Direction * itemhitdist;
            //}

            res.TestComplete = testcomplete;

            return res;
        }
        public SpaceSphereIntersectResult SphereIntersectEntity(ref BoundingSphere sph, YmapEntityDef ent)
        {
            var res = new SpaceSphereIntersectResult();

            var drawable = GameFileCache.TryGetDrawable(ent.Archetype);
            if (drawable != null)
            {
                var eori = ent.Orientation;
                var eorinv = Quaternion.Invert(ent.Orientation);
                var esph = sph;
                esph.Center = eorinv.Multiply(sph.Center - ent.Position);

                if ((drawable is Drawable sdrawable) && (sdrawable.Bound != null))
                {
                    var dhit = sdrawable.Bound.SphereIntersect(ref esph);
                    if (dhit.Hit)
                    {
                        dhit.Position = eori.Multiply(dhit.Position) + ent.Position;
                        dhit.Normal = eori.Multiply(dhit.Normal);
                    }
                    res.TryUpdate(ref dhit);
                }
                else if (drawable is FragDrawable fdrawable)
                {
                    if (fdrawable.Bound != null)
                    {
                        var fhit = fdrawable.Bound.SphereIntersect(ref esph);
                        if (fhit.Hit)
                        {
                            fhit.Position = eori.Multiply(fhit.Position) + ent.Position;
                            fhit.Normal = eori.Multiply(fhit.Normal);
                        }
                        res.TryUpdate(ref fhit);
                    }
                    var fbound = fdrawable.OwnerFragment?.PhysicsLODGroup?.PhysicsLOD1?.Bound;
                    if (fbound != null)
                    {
                        var fhit = fbound.SphereIntersect(ref esph);//TODO: these probably have extra transforms..!
                        if (fhit.Hit)
                        {
                            fhit.Position = eori.Multiply(fhit.Position) + ent.Position;
                            fhit.Normal = eori.Multiply(fhit.Normal);
                        }
                        res.TryUpdate(ref fhit);
                    }
                }
            }

            return res;
        }
        public SpaceSphereIntersectResult SphereIntersectInterior(ref BoundingSphere sph, YmapEntityDef mlo)
        {
            var res = new SpaceSphereIntersectResult();

            if (mlo.Archetype == null)
            { return res; }

            var iori = mlo.Orientation;
            var iorinv = Quaternion.Invert(mlo.Orientation);
            var isph = sph;
            isph.Center = iorinv.Multiply(sph.Center - mlo.Position);

            var hash = mlo.Archetype.Hash;
            var ybn = GameFileCache.GetYbn(hash);
            if ((ybn != null) && (ybn.Loaded))
            {
                var ihit = ybn.Bounds.SphereIntersect(ref isph);
                if (ihit.Hit)
                {
                    ihit.Position = iori.Multiply(ihit.Position) + mlo.Position;
                    ihit.Normal = iori.Multiply(ihit.Normal);
                }
                res.TryUpdate(ref ihit);
            }

            var mlodat = mlo.MloInstance;
            if (mlodat == null)
            { return res; }

            var box = new BoundingBox();

            if (mlodat.Entities != null)
            {
                for (int j = 0; j < mlodat.Entities.Length; j++) //should really improve this by using rooms!
                {
                    var intent = mlodat.Entities[j];
                    if (intent.Archetype == null) continue; //missing archetype...

                    if (!EntityCollisionsEnabled(intent))
                    { continue; }

                    box.Minimum = intent.BBMin;
                    box.Maximum = intent.BBMax;
                    if (sph.Intersects(ref box))
                    {
                        var ehit = SphereIntersectEntity(ref sph, intent);
                        res.TryUpdate(ref ehit);
                    }
                }
            }
            if (mlodat.EntitySets != null)
            {
                for (int e = 0; e < mlodat.EntitySets.Length; e++)
                {
                    var entityset = mlodat.EntitySets[e];
                    if (!entityset.Visible) continue;
                    var entities = entityset.Entities;
                    if (entities == null) continue;
                    for (int i = 0; i < entities.Count; i++) //should really improve this by using rooms!
                    {
                        var intent = entities[i];
                        if (intent.Archetype == null) continue; //missing archetype...

                        if (!EntityCollisionsEnabled(intent))
                        { continue; }

                        box.Minimum = intent.BBMin;
                        box.Maximum = intent.BBMax;
                        if (sph.Intersects(ref box))
                        {
                            var ehit = SphereIntersectEntity(ref sph, intent);
                            res.TryUpdate(ref ehit);
                        }
                    }
                }
            }

            return res;
        }

        private bool EntityCollisionsEnabled(YmapEntityDef? ent)
        {
            if (ent == null)
            { return false; } //entity slot can be null while ymaps are being mutated (e.g. grass-batch painting)

            if ((ent._CEntityDef.lodLevel != rage__eLodType.LODTYPES_DEPTH_ORPHANHD) && (ent._CEntityDef.lodLevel != rage__eLodType.LODTYPES_DEPTH_HD))
            { return false; } //only test HD entities

            if ((ent._CEntityDef.flags & 4) > 0)
            { return false; } //embedded collisions disabled

            return true;
        }

    }



    public struct SpaceBoundsKey
    {
        public MetaHash Name { get; set; }
        public Vector3 Position { get; set; }
        public SpaceBoundsKey(MetaHash name, Vector3 position)
        {
            Name = name;
            Position = position;
        }
    }


    public class SpaceMapDataStore
    {
        public SpaceMapDataStoreNode RootNode;
        public int SplitThreshold = 10;

        public void Init(List<MapDataStoreNode> rootnodes)
        {
            RootNode = new SpaceMapDataStoreNode();
            RootNode.Owner = this;
            foreach (var item in rootnodes)
            {
                RootNode.Add(item);
            }
            RootNode.TrySplit(SplitThreshold);
        }

        public List<MapDataStoreNode> GetItems(ref Vector3 p) //get items at a point, using the streaming extents
        {
            var items = new List<MapDataStoreNode>();
            if (RootNode != null)
            {
                RootNode.GetItems(ref p, items);
            }
            return items;
        }
        public List<MapDataStoreNode> GetItems(ref Vector3 min, ref Vector3 max) //get items intersecting a box, using the entities extents
        {
            var items = new List<MapDataStoreNode>();
            if (RootNode != null)
            {
                RootNode.GetItems(ref min, ref max, items);
            }
            return items;
        }
        public List<MapDataStoreNode> GetItems(ref Ray ray) //get items intersecting a ray, using the entities extents
        {
            var items = new List<MapDataStoreNode>();
            if (RootNode != null)
            {
                RootNode.GetItems(ref ray, items);
            }
            return items;
        }
    }
    public class SpaceMapDataStoreNode
    {
        public SpaceMapDataStore Owner = null;
        public SpaceMapDataStoreNode[] Children = null;
        public List<MapDataStoreNode> Items = null;
        public Vector3 BBMin = new(float.MaxValue);
        public Vector3 BBMax = new(float.MinValue);
        public int Depth = 0;

        public void Add(MapDataStoreNode item)
        {
            Items ??= [];
            BBMin = Vector3.Min(BBMin, item.streamingExtentsMin);
            BBMax = Vector3.Max(BBMax, item.streamingExtentsMax);
            Items.Add(item);
        }

        public void TrySplit(int threshold)
        {
            if ((Items == null) || (Items.Count <= threshold))
            { return; }

            Children = new SpaceMapDataStoreNode[4];

            List<MapDataStoreNode> newItems = [];

            var ncen = (BBMax + BBMin) * 0.5f;
            var next = (BBMax - BBMin) * 0.5f;
            var nsiz = Math.Max(next.X, next.Y);
            var nsizh = nsiz * 0.5f;

            foreach (var item in Items)
            {
                var imin = item.streamingExtentsMin;
                var imax = item.streamingExtentsMax;
                var icen = (imax + imin) * 0.5f;
                var iext = (imax - imin) * 0.5f;
                var isiz = Math.Max(iext.X, iext.Y);

                if (isiz >= nsizh)
                {
                    newItems.Add(item);
                }
                else
                {
                    var cind = ((icen.X > ncen.X) ? 1 : 0) + ((icen.Y > ncen.Y) ? 2 : 0);
                    var c = Children[cind];
                    if (c == null)
                    {
                        c = new SpaceMapDataStoreNode();
                        c.Owner = Owner;
                        c.Depth = Depth + 1;
                        Children[cind] = c;
                    }
                    c.Add(item);
                }
            }

            for (int i = 0; i < 4; i++)
            {
                var c = Children[i];
                if (c != null)
                {
                    c.TrySplit(threshold);
                }
            }

            Items = newItems;
        }

        public void GetItems(ref Vector3 p, List<MapDataStoreNode> items) //get items at a point, using the streaming extents
        {
            if ((p.X >= BBMin.X) && (p.X <= BBMax.X) && (p.Y >= BBMin.Y) && (p.Y <= BBMax.Y))
            {
                if (Items != null)
                {
                    for (int i = 0; i < Items.Count; i++)
                    {
                        var item = Items[i];
                        var imin = item.streamingExtentsMin;
                        var imax = item.streamingExtentsMax;
                        if ((p.X >= imin.X) && (p.X <= imax.X) && (p.Y >= imin.Y) && (p.Y <= imax.Y))
                        {
                            items.Add(item);
                        }
                    }
                }
                if (Children != null)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        var c = Children[i];
                        if (c != null)
                        {
                            c.GetItems(ref p, items);
                        }
                    }
                }
            }
        }
        public void GetItems(ref Vector3 min, ref Vector3 max, List<MapDataStoreNode> items) //get items intersecting a box, using the entities extents
        {
            if ((max.X >= BBMin.X) && (min.X <= BBMax.X) && (max.Y >= BBMin.Y) && (min.Y <= BBMax.Y))
            {
                if (Items != null)
                {
                    for (int i = 0; i < Items.Count; i++)
                    {
                        var item = Items[i];
                        var imin = item.entitiesExtentsMin;
                        var imax = item.entitiesExtentsMax;
                        if ((max.X >= imin.X) && (min.X <= imax.X) && (max.Y >= imin.Y) && (min.Y <= imax.Y))
                        {
                            items.Add(item);
                        }
                    }
                }
                if (Children != null)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        var c = Children[i];
                        if (c != null)
                        {
                            c.GetItems(ref min, ref max, items);
                        }
                    }
                }
            }
        }
        public void GetItems(ref Ray ray, List<MapDataStoreNode> items) //get items intersecting a ray, using the entities extents
        {
            var bb = new BoundingBox(BBMin, BBMax);
            if (ray.Intersects(ref bb))
            {
                if (Items != null)
                {
                    for (int i = 0; i < Items.Count; i++)
                    {
                        var item = Items[i];
                        bb.Minimum = item.entitiesExtentsMin;
                        bb.Maximum = item.entitiesExtentsMax;
                        if (ray.Intersects(ref bb))
                        {
                            items.Add(item);
                        }
                    }
                }
                if (Children != null)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        var c = Children[i];
                        if (c != null)
                        {
                            c.GetItems(ref ray, items);
                        }
                    }
                }
            }
        }
    }


    public class SpaceBoundsStore
    {
        public SpaceBoundsStoreNode RootNode;
        public int SplitThreshold = 10;

        public void Init(List<BoundsStoreItem> items)
        {
            RootNode = new SpaceBoundsStoreNode();
            RootNode.Owner = this;
            foreach (var item in items)
            {
                RootNode.Add(item);
            }
            RootNode.TrySplit(SplitThreshold);
        }

        public List<BoundsStoreItem> GetItems(ref Vector3 min, ref Vector3 max, bool[]? layers = null)
        {
            var items = new List<BoundsStoreItem>();
            if (RootNode != null)
            {
                RootNode.GetItems(ref min, ref max, items, layers);
            }
            return items;
        }
        public List<BoundsStoreItem> GetItems(ref Ray ray, bool[]? layers = null)
        {
            var items = new List<BoundsStoreItem>();
            if (RootNode != null)
            {
                RootNode.GetItems(ref ray, items, layers);
            }
            return items;
        }
    }
    public class SpaceBoundsStoreNode
    {
        public SpaceBoundsStore Owner = null;
        public SpaceBoundsStoreNode[] Children = null;
        public List<BoundsStoreItem> Items = null;
        public Vector3 BBMin = new(float.MaxValue);
        public Vector3 BBMax = new(float.MinValue);
        public int Depth = 0;

        public void Add(BoundsStoreItem item)
        {
            Items ??= [];
            BBMin = Vector3.Min(BBMin, item.Min);
            BBMax = Vector3.Max(BBMax, item.Max);
            Items.Add(item);
        }

        public void TrySplit(int threshold)
        {
            if ((Items == null) || (Items.Count <= threshold))
            { return; }

            Children = new SpaceBoundsStoreNode[4];

            List<BoundsStoreItem> newItems = [];

            var ncen = (BBMax + BBMin) * 0.5f;
            var next = (BBMax - BBMin) * 0.5f;
            var nsiz = Math.Max(next.X, next.Y);
            var nsizh = nsiz * 0.5f;

            foreach (var item in Items)
            {
                var imin = item.Min;
                var imax = item.Max;
                var icen = (imax + imin) * 0.5f;
                var iext = (imax - imin) * 0.5f;
                var isiz = Math.Max(iext.X, iext.Y);

                if (isiz >= nsizh)
                {
                    newItems.Add(item);
                }
                else
                {
                    var cind = ((icen.X > ncen.X) ? 1 : 0) + ((icen.Y > ncen.Y) ? 2 : 0);
                    var c = Children[cind];
                    if (c == null)
                    {
                        c = new SpaceBoundsStoreNode();
                        c.Owner = Owner;
                        c.Depth = Depth + 1;
                        Children[cind] = c;
                    }
                    c.Add(item);
                }
            }

            for (int i = 0; i < 4; i++)
            {
                var c = Children[i];
                if (c != null)
                {
                    c.TrySplit(threshold);
                }
            }

            Items = newItems;
        }

        public void GetItems(ref Vector3 min, ref Vector3 max, List<BoundsStoreItem> items, bool[]? layers = null)
        {
            if ((max.X >= BBMin.X) && (min.X <= BBMax.X) && (max.Y >= BBMin.Y) && (min.Y <= BBMax.Y))
            {
                if (Items != null)
                {
                    for (int i = 0; i < Items.Count; i++)
                    {
                        var item = Items[i];

                        if ((layers != null) && (item.Layer < 3) && (!layers[item.Layer]))
                        { continue; }

                        if ((max.X >= item.Min.X) && (min.X <= item.Max.X) && (max.Y >= item.Min.Y) && (min.Y <= item.Max.Y))
                        {
                            items.Add(item);
                        }
                    }
                }
                if (Children != null)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        var c = Children[i];
                        if (c != null)
                        {
                            c.GetItems(ref min, ref max, items, layers);
                        }
                    }
                }
            }
        }
        public void GetItems(ref Ray ray, List<BoundsStoreItem> items, bool[]? layers = null)
        {
            var box = new BoundingBox(BBMin, BBMax);
            if (ray.Intersects(ref box))
            {
                if (Items != null)
                {
                    for (int i = 0; i < Items.Count; i++)
                    {
                        var item = Items[i];

                        if ((layers != null) && (item.Layer < 3) && (!layers[item.Layer]))
                        { continue; }

                        box = new BoundingBox(item.Min, item.Max);
                        if (ray.Intersects(box))
                        {
                            items.Add(item);
                        }
                    }
                }
                if (Children != null)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        var c = Children[i];
                        if (c != null)
                        {
                            c.GetItems(ref ray, items, layers);
                        }
                    }
                }
            }
        }
    }


    public class SpaceNodeGrid
    {
        //node grid for V paths
        public SpaceNodeGridCell[,] Cells { get; set; }
        public float CellSize = 512.0f;
        public float CellSizeInv; //inverse of the cell size.
        public int CellCountX = 32;
        public int CellCountY = 32;
        public float CornerX = -8192.0f;
        public float CornerY = -8192.0f;

        public SpaceNodeGrid()
        {
            CellSizeInv = 1.0f / CellSize;

            Cells = new SpaceNodeGridCell[CellCountX, CellCountY];

            for (int x = 0; x < CellCountX; x++)
            {
                for (int y = 0; y < CellCountY; y++)
                {
                    Cells[x, y] = new SpaceNodeGridCell(x, y);
                }
            }
        }

        public SpaceNodeGridCell GetCell(int id)
        {
            int x = id % CellCountX;
            int y = id / CellCountX;
            if ((x >= 0) && (x < CellCountX) && (y >= 0) && (y < CellCountY))
            {
                return Cells[x, y];
            }
            return null;
        }

        public SpaceNodeGridCell GetCellForPosition(Vector3 position)
        {
            var x = (int)((position.X - CornerX) / CellSize);
            var y = (int)((position.Y - CornerY) / CellSize);

            if ((x >= 0) && (x < CellCountX) && (y >= 0) && (y < CellCountY))
            {
                return Cells[x, y];
            }

            return null;
        }


        public YndNode GetYndNode(ushort areaid, ushort nodeid)
        {
            var cell = GetCell(areaid);
            if ((cell == null) || (cell.Ynd == null) || (cell.Ynd.Nodes == null))
            { return null; }
            if (nodeid >= cell.Ynd.Nodes.Length)
            { return null; }
            return cell.Ynd.Nodes[nodeid];
        }

        public void UpdateYnd(YndFile ynd)
        {
            // Cache dimensions for better performance
            int lengthX = Cells.GetLength(0);
            int lengthY = Cells.GetLength(1);
            
            // Clear existing references to this ynd
            for (int xx = 0; xx < lengthX; xx++)
            {
                for (int yy = 0; yy < lengthY; yy++)
                {
                    if (Cells[xx, yy].Ynd == ynd)
                    {
                        Cells[xx, yy].Ynd = null;
                    }
                }
            }

            // Set new position - add bounds checking for safety
            var x = ynd.CellX;
            var y = ynd.CellY;
            if (x >= 0 && x < lengthX && y >= 0 && y < lengthY)
            {
                Cells[x, y].Ynd = ynd;
            }
        }
    }
    public class SpaceNodeGridCell
    {
        public int X;
        public int Y;
        public int ID;

        public YndFile Ynd;

        public SpaceNodeGridCell(int x, int y)
        {
            X = x;
            Y = y;
            ID = y * 32 + x;
        }

    }


    public class SpaceNavGrid
    {
        //grid for V navmeshes
        public SpaceNavGridCell[,] Cells { get; set; }
        public float CellSize = 150.0f;
        public float CellSizeInv; //inverse of the cell size.
        public int CellCountX = 100;
        public int CellCountY = 100;
        public float CornerX = -6000.0f;//max = -6000+(100*150) = 9000
        public float CornerY = -6000.0f;

        public SpaceNavGrid()
        {
            CellSizeInv = 1.0f / CellSize;

            Cells = new SpaceNavGridCell[CellCountX, CellCountY];

            for (int x = 0; x < CellCountX; x++)
            {
                for (int y = 0; y < CellCountY; y++)
                {
                    Cells[x, y] = new SpaceNavGridCell(x, y);
                }
            }
        }

        public SpaceNavGridCell GetCell(int id)
        {
            int x = id % CellCountX;
            int y = id / CellCountX;
            if ((x >= 0) && (x < CellCountX) && (y >= 0) && (y < CellCountY))
            {
                return Cells[x, y];
            }
            return null;
        }


        public Vector3 GetCellRel(Vector3 p)//float value in cell coords
        {
            return (p - new Vector3(CornerX, CornerY, 0)) * CellSizeInv;
        }
        public Vector2I GetCellPos(Vector3 p)
        {
            Vector3 ind = (p - new Vector3(CornerX, CornerY, 0)) * CellSizeInv;
            int x = (int)ind.X;
            int y = (int)ind.Y;
            x = (x < 0) ? 0 : (x >= CellCountX) ? CellCountX-1 : x;
            y = (y < 0) ? 0 : (y >= CellCountY) ? CellCountY-1 : y;
            return new Vector2I(x, y);
        }
        public SpaceNavGridCell GetCell(Vector2I g)
        {
            var cell = Cells[g.X, g.Y];
            if (cell == null)
            {
                //cell = new SpaceNavGridCell(g.X, g.Y);
                //Cells[g.X, g.Y] = cell;
            }
            return cell;
        }
        public SpaceNavGridCell GetCell(Vector3 p)
        {
            return GetCell(GetCellPos(p));
        }


        public Vector3 GetCellMin(SpaceNavGridCell cell)
        {
            Vector3 c = new(cell.X, cell.Y, 0);
            return new Vector3(CornerX, CornerY, 0) + (c * CellSize);
        }
        public Vector3 GetCellMax(SpaceNavGridCell cell)
        {
            return GetCellMin(cell) + new Vector3(CellSize, CellSize, 0.0f);
        }

    }
    public class SpaceNavGridCell
    {
        public int X;
        public int Y;
        public int ID;
        public int FileX;
        public int FileY;

        public RpfResourceFileEntry YnvEntry;
        public YnvFile Ynv;

        public SpaceNavGridCell(int x, int y)
        {
            X = x;
            Y = y;
            ID = y * 100 + x;
            FileX = x * 3;
            FileY = y * 3;
        }

    }



    public struct SpaceRayIntersectResult
    {
        public bool Hit;
        public float HitDist;
        public BoundVertexRef HitVertex;
        public BoundPolygon HitPolygon;
        public Bounds HitBounds;
        public YbnFile HitYbn;
        public YmapEntityDef HitEntity;
        public Vector3 Position;
        public Vector3 Normal;
        public int TestedNodeCount;
        public int TestedPolyCount;
        public bool TestComplete;
        public BoundMaterial_s Material;

        public void TryUpdate(ref SpaceRayIntersectResult r)
        {
            if (r.Hit)
            {
                Hit = true;
                HitDist = r.HitDist;
                HitVertex = r.HitVertex;
                HitPolygon = r.HitPolygon;
                HitBounds = r.HitBounds;
                HitYbn = r.HitYbn;
                HitEntity = r.HitEntity;
                Material = r.Material;
                Position = r.Position;
                Normal = r.Normal;
            }
            TestedNodeCount += r.TestedNodeCount;
            TestedPolyCount += r.TestedPolyCount;
        }
    }
    public struct SpaceSphereIntersectResult
    {
        public bool Hit;
        public float HitDist;
        public BoundPolygon HitPolygon;
        public Vector3 Position;
        public Vector3 Normal;
        public int TestedNodeCount;
        public int TestedPolyCount;
        public bool TestComplete;

        public void TryUpdate(ref SpaceSphereIntersectResult r)
        {
            if (r.Hit)
            {
                Hit = true;
                HitPolygon = r.HitPolygon;
                Normal = r.Normal;
            }
            TestedPolyCount += r.TestedPolyCount;
            TestedNodeCount += r.TestedNodeCount;
        }
    }

    public struct SpaceEntityCollision
    {
        public Entity Entity; //the entity owning this collision
        public Entity Entity2; //second entity, if this is a collision between two entities
        public SpaceSphereIntersectResult SphereHit; //details of the sphere intersection point
        public Vector3 PrePos; //last known position before hit
        public float PreT; //last known T before hit
        public float HitT; //fraction of the frame (0-1)
        public Vector3 HitPos; //position of the sphere center at hit point
        public Quaternion HitRot; //rotation of the entity at hit point
        public Vector3 HitVel; //velocity of the entity for this hit
        public Vector3 HitAngVel; //angular velocity of the entity for this hit
        public int HitNumber; //count of previous iterations
        public bool Hit;
        public Vector3 HitVelDir;
    }

}
