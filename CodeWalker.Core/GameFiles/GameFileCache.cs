using SharpDX;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace CodeWalker.GameFiles
{
    public class GameFileCache
    {
        public RpfManager? RpfMan;
        private RpfManager ArchiveManager => RpfMan ?? throw new InvalidOperationException("The archive manager has not been initialized.");
        private Action<string>? UpdateStatus;
        internal Action<string>? ErrorLog;
        public int MaxItemsPerLoop = 8; //files loaded per content loop - they load in parallel, so this is also the batch width
        public int MaxQueueLength = 512; //pending distinct file requests. Stale ones are skipped on dequeue, so a deep queue costs nothing

        private ConcurrentQueue<GameFile> requestQueue = new();
        private int requestQueueCount; //tracked separately - ConcurrentQueue.Count is a segment walk, and the enqueue check is per-Get*

        ////dynamic cache
        private Cache<GameFileCacheKey, GameFile> mainCache;
        public volatile bool IsInited = false;

        private volatile bool archetypesLoaded = false;
        private Dictionary<uint, Archetype> archetypeDict = new();
        private Dictionary<uint, RpfFileEntry> textureLookup = new();
        private Dictionary<MetaHash, MetaHash> textureParents = new();
        private Dictionary<MetaHash, MetaHash> hdtexturelookup = new();

        private object updateSyncRoot = new object();
        private object requestSyncRoot = new object();
        private object textureSyncRoot = new object(); //for the texture lookup.


        private Dictionary<GameFileCacheKey, GameFile> projectFiles = new(); //for cache files loaded in project window: ydr,ydd,ytd,yft
        private Dictionary<uint, Archetype> projectArchetypes = new(); //used to override archetypes in world view with project ones




        //static indexes
        public Dictionary<uint, RpfFileEntry> YdrDict { get; private set; } = new();
        public Dictionary<uint, RpfFileEntry> YddDict { get; private set; } = new();
        public Dictionary<uint, RpfFileEntry> YtdDict { get; private set; } = new();
        public Dictionary<uint, RpfFileEntry> YmapDict { get; private set; } = new();
        public Dictionary<uint, RpfFileEntry> YftDict { get; private set; } = new();
        public Dictionary<uint, RpfFileEntry> YbnDict { get; private set; } = new();
        public Dictionary<uint, RpfFileEntry> YcdDict { get; private set; } = new();
        public Dictionary<uint, RpfFileEntry> YedDict { get; private set; } = new();
        public Dictionary<uint, RpfFileEntry> YnvDict { get; private set; } = new();
        public Dictionary<uint, RpfFileEntry> Gxt2Dict { get; private set; } = new();


        public Dictionary<uint, RpfFileEntry> AllYmapsDict { get; private set; } = new();


        //static cached data loaded at init
        public Dictionary<uint, YtypFile> YtypDict { get; set; } = new();

        public List<CacheDatFile> AllCacheFiles { get; set; } = new();
        public Dictionary<uint, MapDataStoreNode> YmapHierarchyDict { get; set; } = new();

        public List<YmfFile> AllManifests { get; set; } = new();


        public bool EnableDlc { get; set; } = false;//true;//
        public bool EnableMods { get; set; } = false;

        public List<string> DlcPaths { get; set; } = new();
        public List<RpfFile> DlcActiveRpfs { get; set; } = new();
        public List<DlcSetupFile> DlcSetupFiles { get; set; } = new();
        public List<DlcExtraFolderMountFile> DlcExtraFolderMounts { get; set; } = new();
        public Dictionary<string, string> DlcPatchedPaths { get; set; } = new();
        public List<string> DlcCacheFileList { get; set; } = new();
        public List<string> DlcNameList { get; set; } = new();
        public string SelectedDlc { get; set; } = string.Empty;

        public Dictionary<string, RpfFile> ActiveMapRpfFiles { get; set; } = new();
        //ymaps belonging to a map variant the active DLC changesets replaced (eg vanilla id2_* under mpheist)
        public HashSet<uint> DisabledYmaps { get; } = new();

        public Dictionary<uint, World.TimecycleMod> TimeCycleModsDict = new();

        public Dictionary<MetaHash, VehicleInitData> VehiclesInitDict { get; set; } = new();
        public Dictionary<MetaHash, CPedModelInfo__InitData> PedsInitDict { get; set; } = new();
        public Dictionary<MetaHash, PedFile> PedVariationsDict { get; set; } = new();
        public Dictionary<MetaHash, Dictionary<MetaHash, RpfFileEntry>> PedDrawableDicts { get; set; } = new();
        public Dictionary<MetaHash, Dictionary<MetaHash, RpfFileEntry>> PedTextureDicts { get; set; } = new();
        public Dictionary<MetaHash, Dictionary<MetaHash, RpfFileEntry>> PedClothDicts { get; set; } = new();


        public List<RelFile> AudioDatRelFiles = new();
        public Dictionary<MetaHash, RelData> AudioConfigDict = new();
        public Dictionary<MetaHash, RelData> AudioSpeechDict = new();
        public Dictionary<MetaHash, RelData> AudioSynthsDict = new();
        public Dictionary<MetaHash, RelData> AudioMixersDict = new();
        public Dictionary<MetaHash, RelData> AudioCurvesDict = new();
        public Dictionary<MetaHash, RelData> AudioCategsDict = new();
        public Dictionary<MetaHash, RelData> AudioSoundsDict = new();
        public Dictionary<MetaHash, RelData> AudioGameDict = new();



        public List<RpfFile> BaseRpfs { get; private set; } = new();
        public List<RpfFile> AllRpfs { get; private set; } = new();
        public List<RpfFile> DlcRpfs { get; private set; } = new();

        //semicolon-separated folders of loose map assets (FiveM map resources) to load like a mods dlcpack
        public string ExtraFolders = string.Empty;

        public bool DoFullStringIndex = false;
        public bool LoadArchetypes = true;
        public bool LoadVehicles = true;
        public bool LoadPeds = true;
        public bool LoadAudio = true;
        private bool PreloadedMode = false;

        private bool GTAGen9;
        private string GTAFolder = string.Empty;
        private string ExcludeFolders = string.Empty;



        public int QueueLength
        {
            get
            {
                return Interlocked.CompareExchange(ref requestQueueCount, 0, 0);
            }
        }
        public int ItemCount
        {
            get
            {
                return mainCache.Count;
            }
        }
        public long MemoryUsage
        {
            get
            {
                return mainCache.CurrentMemoryUsage;
            }
        }



        public GameFileCache(long size, double cacheTime, string folder, bool gen9, string dlc, bool mods, string excludeFolders)
        {
            mainCache = new Cache<GameFileCacheKey, GameFile>(size, cacheTime);//2GB is good as default
            SelectedDlc = dlc;
            EnableDlc = !string.IsNullOrEmpty(SelectedDlc);
            EnableMods = mods;
            GTAGen9 = gen9;
            GTAFolder = folder;
            ExcludeFolders = excludeFolders;
        }


        public void Clear()
        {
            IsInited = false;

            mainCache.Clear();

            textureLookup.Clear();

            GameFile? queueclear;
            while (requestQueue.TryDequeue(out queueclear))
            { queueclear.LoadQueued = false; } //empty the old queue out...
            Interlocked.Exchange(ref requestQueueCount, 0);
        }

        public void Init(Action<string> updateStatus, Action<string> errorLog)
        {
            UpdateStatus = updateStatus;
            ErrorLog = errorLog;
            LodDiag.Log = errorLog;
            LodDiag.Reset();

            if (IsInited && RpfMan != null)
            {
                if (UpdateStatus != null) UpdateStatus?.Invoke("Already initialized.");
                return;
            }

            Clear();

            if (RpfMan == null)
            {
                var exclude = GetExcludePaths();
                RpfMan = new RpfManager
                {
                    ExcludePaths = exclude,
                    //distinct: the same folder listed twice would be scanned twice for no gain
                    ExtraFolders = (ExtraFolders ?? string.Empty).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                                   .Select(f => f.Trim()).Where(f => f.Length > 0)
                                   .Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                    EnableMods = EnableMods,
                };

                ArchiveManager.Init(GTAFolder, GTAGen9, UpdateStatus, ErrorLog);

                IProgress<string>? statusProgress = null;
                if (UpdateStatus != null)
                {
                    statusProgress = new Progress<string>(UpdateStatus);
                }

                if (statusProgress != null)
                {
                    InitGlobalAsync(statusProgress, CancellationToken.None).GetAwaiter().GetResult();
                }
                else
                {
                    InitGlobalAsync(null, CancellationToken.None).GetAwaiter().GetResult();
                }

                if (statusProgress != null)
                {
                    InitDlcAsync(statusProgress, CancellationToken.None).GetAwaiter().GetResult();
                }
                else
                {
                    InitDlcAsync(null, CancellationToken.None).GetAwaiter().GetResult();
                }
                //RE_Testing();
            }

            if (UpdateStatus != null) UpdateStatus?.Invoke("Scan complete");
            IsInited = true;
        }

        public void RE_Testing()
        {
            //RE test area!
            TestAudioRels();
            TestAudioYmts();
            TestAudioAwcs();
            TestMetas();
            TestPsos();
            TestRbfs();
            TestCuts();
            TestYlds();
            TestYeds();
            TestYcds();
            TestYtds();
            TestYbns();
            TestYdrs();
            TestYdds();
            TestYfts();
            TestYpts();
            TestYnvs();
            TestYvrs();
            TestYwrs();
            TestYmaps();
            TestYpdbs();
            TestYfds();
            TestMrfs();
            TestFxcs();
            TestPlacements();
            TestDrawables();
            TestCacheFiles();
            TestHeightmaps();
            TestWatermaps();
            GetShadersXml();
            GetShadersLegacyConversionXml();
            GetShadersGen9ConversionXml();
            GetArchetypeTimesList();
            string typestr = PsoTypes.GetTypesString();
        }
        public async Task InitAsync(IProgress<string>? status, IProgress<string>? errors, List<RpfFile> allRpfs, CancellationToken ct = default)
        {
            UpdateStatus = status != null ? new Action<string>(status.Report) : null;
            ErrorLog = errors != null ? new Action<string>(errors.Report) : null;

            Clear();

            PreloadedMode = true;
            EnableDlc = true;
            EnableMods = false;

            RpfMan = new RpfManager();
            ArchiveManager.Init(allRpfs, GTAGen9);

            AllRpfs = [.. allRpfs];
            BaseRpfs = AllRpfs;
            DlcRpfs = [];

            await PhaseAsync(status, ct, "Building global dictionaries...", InitGlobalDicts);
            await PhaseAsync(status, ct, "Loading manifests...", InitManifestDicts);
            await PhaseAsync(status, ct, "Loading global texture list...", InitGtxds);
            await PhaseAsync(status, ct, "Loading archetypes...", InitArchetypeDicts);
            await PhaseAsync(status, ct, "Loading strings...", InitStringDicts);
            await PhaseAsync(status, ct, "Loading audio...", InitAudio);

            IsInited = true;
        }
        public void Init(Action<string> updateStatus, Action<string> errorLog, List<RpfFile> allRpfs)
        {
            var status = updateStatus is null ? null : new Progress<string>(updateStatus);
            var errors = errorLog is null ? null : new Progress<string>(errorLog);

            InitAsync(status, errors, allRpfs, CancellationToken.None).GetAwaiter().GetResult();
        }

        private async Task InitGlobalAsync(IProgress<string>? status, CancellationToken ct)
        {
            BaseRpfs = GetModdedRpfList(ArchiveManager.BaseRpfs);
            AllRpfs = GetModdedRpfList(ArchiveManager.AllRpfs);
            DlcRpfs = GetModdedRpfList(ArchiveManager.DlcRpfs);

            await PhaseAsync(status, ct, "Building global dictionaries...", InitGlobalDicts);
        }

        private async Task InitDlcAsync(IProgress<string>? status, CancellationToken ct)
        {
            await PhaseAsync(status, ct, "Building DLC List...", InitDlcList);
            await PhaseAsync(status, ct, "Building active RPF dictionary...", InitActiveMapRpfFiles);
            await PhaseAsync(status, ct, "Building map dictionaries...", InitMapDicts);
            await PhaseAsync(status, ct, "Loading manifests...", InitManifestDicts);
            await PhaseAsync(status, ct, "Loading global texture list...", InitGtxds);
            await PhaseAsync(status, ct, "Loading cache...", InitMapCaches);
            await PhaseAsync(status, ct, "Loading archetypes...", InitArchetypeDicts);
            await PhaseAsync(status, ct, "Loading strings...", InitStringDicts);
            await PhaseAsync(status, ct, "Loading vehicles...", InitVehicles);
            await PhaseAsync(status, ct, "Loading peds...", InitPeds);
            await PhaseAsync(status, ct, "Loading audio...", InitAudio);
        }
        private static async Task PhaseAsync(IProgress<string>? status, CancellationToken ct, string message, Action phase)
        {
            status?.Report(message);
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            phase();
        }

        private void InitDlcList()
        {
            DlcPaths.Clear();

            const string dlcListPath = @"update\update.rpf\common\data\dlclist.xml";
            var dlcListXml = ArchiveManager.GetFileXml(dlcListPath);

            if ((dlcListXml == null) || (dlcListXml.DocumentElement == null))
            {
                ErrorLog?.Invoke($"InitDlcList: Couldn't load {dlcListPath}.");
                return;
            }

            foreach (XmlNode pathsNode in dlcListXml.DocumentElement)
            {
                foreach (XmlNode itemNode in pathsNode.ChildNodes)
                {
                    if (itemNode.NodeType != XmlNodeType.Element) continue;
                    var normalized = itemNode.InnerText.ToLowerInvariant().Replace('\\', '/').Replace("platform:", "x64");
                    DlcPaths.Add(normalized);
                }
            }

            // build DLC lookup dictionaries
            Dictionary<string, RpfFile> dlcByVirtualPath = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, RpfFile> dlcByName = new(StringComparer.OrdinalIgnoreCase);

            foreach (var dlcRpf in DlcRpfs)
            {
                if (dlcRpf == null) continue;
                if (dlcRpf.NameLower == "dlc.rpf")
                {
                    var vpath = GetDlcRpfVirtualPath(dlcRpf.Path);
                    var name = GetDlcNameFromPath(dlcRpf.Path);

                    dlcByVirtualPath[vpath] = dlcRpf;
                    dlcByName[name] = dlcRpf;
                }
            }

            // build map from update.rpf
            DlcPatchedPaths.Clear();

            const string updateRpfPath = @"update\update.rpf";
            var updateRpf = ArchiveManager.FindRpfFile(updateRpfPath);

            if (updateRpf == null)
            {
                ErrorLog?.Invoke("InitDlcList: update.rpf not found!");
            }
            else
            {
                try
                {
                    var updSetupDoc = ArchiveManager.GetFileXml(updateRpfPath + @"\setup2.xml");
                    var updSetupFile = new DlcSetupFile();
                    updSetupFile.Load(updSetupDoc);

                    var updContentDoc = ArchiveManager.GetFileXml(updateRpfPath + @"\" + updSetupFile.datFile);
                    var updContentFile = new DlcContentFile();
                    updContentFile.Load(updContentDoc);

                    updSetupFile.DlcFile = updateRpf;
                    updSetupFile.ContentFile = updContentFile;
                    updContentFile.DlcFile = updateRpf;

                    updSetupFile.deviceName = "update";
                    updContentFile.LoadDicts(updSetupFile, ArchiveManager, this);

                    var extraTU = updContentFile.ExtraTitleUpdates;
                    if (extraTU != null)
                    {
                        foreach (var tuMount in extraTU.Mounts)
                        {
                            var lpath = tuMount.path.ToLowerInvariant();
                            var relPath = lpath.Replace('/', '\\').Replace(@"update:\", "");

                            var dlcName = GetDlcNameFromPath(relPath);
                            RpfFile? dlcFile;
                            if (!dlcByName.TryGetValue(dlcName, out dlcFile) || dlcFile == null) continue;

                            var dlcPathPrefix = dlcFile.Path + "\\";
                            var files = updateRpf.GetFiles(relPath, true);
                            foreach (var file in files)
                            {
                                if (file == null) continue;
                                var srcFull = file.Path;

                                var mapped = srcFull.Replace(updateRpfPath, "update:").Replace('\\', '/').Replace(lpath, dlcPathPrefix).Replace('/', '\\');
                                if (mapped.StartsWith(RpfManager.ModsFolderPrefix, StringComparison.OrdinalIgnoreCase))
                                {
                                    mapped = mapped.Substring(RpfManager.ModsFolderPrefix.Length);
                                }
                                DlcPatchedPaths[mapped] = srcFull;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ErrorLog?.Invoke($"InitDlcList: Error processing {updateRpfPath}: {ex}");
                }
            }

            // load each DLCs setup/content (with patched lookups) and gather extra mounts
            DlcSetupFiles.Clear();
            DlcExtraFolderMounts.Clear();

            foreach (var vpath in DlcPaths)
            {
                RpfFile? dlcFile;
                if (!dlcByVirtualPath.TryGetValue(vpath, out dlcFile) || dlcFile == null) continue;

                try
                {
                    var setupPath = GetDlcPatchedPath(dlcFile.Path + @"\setup2.xml");
                    var setupDoc = ArchiveManager.GetFileXml(setupPath);
                    var setupFile = new DlcSetupFile();
                    setupFile.Load(setupDoc);

                    var contentPath = GetDlcPatchedPath(dlcFile.Path + @"\" + setupFile.datFile);
                    var contentDoc = ArchiveManager.GetFileXml(contentPath);
                    var contentFile = new DlcContentFile();
                    contentFile.Load(contentDoc);

                    setupFile.DlcFile = dlcFile;
                    setupFile.ContentFile = contentFile;
                    contentFile.DlcFile = dlcFile;

                    contentFile.LoadDicts(setupFile, ArchiveManager, this);

                    foreach (var extra in contentFile.ExtraMounts.Values)
                    {
                        DlcExtraFolderMounts.Add(extra);
                    }

                    DlcSetupFiles.Add(setupFile);
                }
                catch (Exception ex)
                {
                    ErrorLog?.Invoke($"InitDlcList: Error processing DLC '{vpath}': {ex}");
                }
            }

            // Loop alternative: sort in-place instead of creating new list
            DlcSetupFiles.Sort((a, b) => a.order.CompareTo(b.order));
            DlcNameList.Clear();

            foreach (var sfile in DlcSetupFiles)
            {
                if (sfile == null || sfile.DlcFile == null) continue;
                DlcNameList.Add(GetDlcNameFromPath(sfile.DlcFile.Path));
            }

            if (DlcNameList.Count > 0 && string.IsNullOrEmpty(SelectedDlc))
            {
                SelectedDlc = DlcNameList[DlcNameList.Count - 1];
            }
        }

        private void InitImagesMetas()
        {
            //currently not used..

            ////parse images.meta
            //string imagesmetapath = "common.rpf\\data\\levels\\gta5\\images.meta";
            //if (EnableDlc)
            //{
            //    imagesmetapath = "update\\update.rpf\\common\\data\\levels\\gta5\\images.meta";
            //}
            //var imagesmetaxml = ArchiveManager.GetFileXml(imagesmetapath);
            //var imagesnodes = imagesmetaxml.DocumentElement.ChildNodes;
            //List<DlcContentDataFile> imagedatafilelist = new();
            //Dictionary<string, DlcContentDataFile> imagedatafiles = new();
            //foreach (XmlNode node in imagesnodes)
            //{
            //    DlcContentDataFile datafile = new(node);
            //    string fname = datafile.filename.ToLower();
            //    fname = fname.Replace('\\', '/');
            //    imagedatafiles[fname] = datafile;
            //    imagedatafilelist.Add(datafile);
            //}


            //filter ActiveMapFiles based on images.meta?

            //DlcContentDataFile imagesdata;
            //if (imagedatafiles.TryGetValue(path, out imagesdata))
            //{
            //    ActiveMapRpfFiles[path] = baserpf;
            //}
        }

        private void InitActiveMapRpfFiles()
        {
            ActiveMapRpfFiles.Clear();
            DisabledYmaps.Clear();

            string? NormalizeSlash(string? s) { return s == null ? null : s.Replace('\\', '/'); }

            // base RPFs
            foreach (var baseRpf in BaseRpfs)
            {
                if (baseRpf == null) continue;

                var normPath = NormalizeSlash(baseRpf.Path);
                if (string.IsNullOrEmpty(normPath)) continue;
                if (normPath == "common.rpf")
                {
                    ActiveMapRpfFiles["common"] = baseRpf;
                    continue;
                }

                var slashIdx = normPath.IndexOf('/');
                if (slashIdx > 0 && slashIdx < normPath.Length)
                {
                    var key = "x64" + normPath.Substring(slashIdx);
                    ActiveMapRpfFiles[key] = baseRpf;
                }
                else
                {
                    ActiveMapRpfFiles[normPath] = baseRpf;
                }
            }

            if (!EnableDlc)
            {
                AddExtraActiveMapRpfFiles();
                return;
            }
            // include update.rpf so files not present in child RPFs can be used
            foreach (var rpf in DlcRpfs)
            {
                if (rpf != null && rpf.NameLower == "update.rpf")
                {
                    var upPath = NormalizeSlash(rpf.Path);
                    if (string.IsNullOrEmpty(upPath)) continue;
                    ActiveMapRpfFiles[upPath] = rpf;
                    break;
                }
            }

            DlcActiveRpfs.Clear();
            DlcCacheFileList.Clear();

            Dictionary<string, List<string>> overlays = new();

            // DLCs in order
            foreach (var setupFile in DlcSetupFiles)
            {
                if (setupFile == null || setupFile.DlcFile == null) continue;

                var contentFile = setupFile.ContentFile;
                var dlcFile = setupFile.DlcFile;

                // dlc.rpf
                DlcActiveRpfs.Add(dlcFile);

                // subpack dlcs, dlc1.rpf, dlc2.rpf etc.
                if (setupFile.subPackCount > 0)
                {
                    for (var i = 1; i <= setupFile.subPackCount; i++)
                    {
                        var subpackPath = dlcFile.Path.Replace("\\dlc.rpf", "\\dlc" + i.ToString() + ".rpf");
                        var subpack = ArchiveManager.FindRpfFile(subpackPath);
                        if (subpack == null) continue;

                        DlcActiveRpfs.Add(subpack);

                        setupFile.DlcSubpacks ??= [];
                        setupFile.DlcSubpacks.Add(subpack);
                    }
                }

                // temporary hack to stop this dlc breaking everything
                var dlcName = GetDlcNameFromPath(dlcFile.Path);
                if (dlcName == "patchday27ng" && SelectedDlc != dlcName)
                {
                    continue;
                }

                // base RPF data files listed in content.xml
                if (contentFile != null && contentFile.RpfDataFiles != null)
                {
                    foreach (var kvp in contentFile.RpfDataFiles)
                    {
                        var logicalKey = kvp.Key;
                        var unmounted = GetDlcUnmountedPath(kvp.Value.filename);
                        var physical = GetDlcRpfPhysicalPath(unmounted, setupFile);

                        AddDlcOverlayRpf(logicalKey, unmounted, setupFile, overlays);
                        AddDlcActiveMapRpfFile(logicalKey, physical, setupFile);
                    }
                }

                // content changesets, cache loader & file enables / disables
                if (contentFile != null && contentFile.contentChangeSets != null)
                {
                    foreach (var changeSet in contentFile.contentChangeSets)
                    {
                        if (changeSet == null) continue;

                        // cache loader
                        if (changeSet.useCacheLoader)
                        {
                            var cacheHash = JenkHash.GenHashLowerInvariant(changeSet.changeSetName);
                            var cacheFileName = dlcName + "_" + cacheHash.ToString() + "_cache_y.dat";
                            var cachePath = dlcFile.Path + "\\x64\\data\\cacheloaderdata_dlc\\" + cacheFileName;
                            var patchedPath = GetDlcPatchedPath(cachePath);
                            DlcCacheFileList.Add(patchedPath);
                        }

                        // filesToEnable
                        if (changeSet.filesToEnable != null)
                        {
                            foreach (var file in changeSet.filesToEnable)
                            {
                                if (string.IsNullOrEmpty(file)) continue;

                                var dfn = GetDlcPlatformPath(file).ToLowerInvariant();

                                DlcExtraFolderMountFile? extraMount;
                                if (contentFile.ExtraMounts != null && contentFile.ExtraMounts.TryGetValue(dfn, out extraMount))
                                { }
                                else
                                {
                                    DlcContentDataFile? rpfDataFile;
                                    if (contentFile.RpfDataFiles != null &&
                                        contentFile.RpfDataFiles.TryGetValue(dfn, out rpfDataFile))
                                    {
                                        var physical = GetDlcRpfPhysicalPath(rpfDataFile.filename, setupFile);

                                        AddDlcOverlayRpf(dfn, rpfDataFile.filename, setupFile, overlays);
                                        AddDlcActiveMapRpfFile(dfn, physical, setupFile);
                                    }
                                    else
                                    {
                                        if (dfn.EndsWith(".rpf", StringComparison.OrdinalIgnoreCase))
                                        { }
                                    }
                                }
                            }
                        }

                        // mapChangeSetData
                        if (changeSet.mapChangeSetData != null)
                        {
                            foreach (var mapCs in changeSet.mapChangeSetData)
                            {
                                if (mapCs == null) continue;

                                // filesToInvalidate
                                if (mapCs.filesToInvalidate != null)
                                {
                                    foreach (var file in mapCs.filesToInvalidate)
                                    {
                                        if (string.IsNullOrEmpty(file)) continue;

                                        var mounted = GetDlcMountedPath(file);
                                        var platform = GetDlcPlatformPath(mounted);

                                        if (platform.EndsWith(".rpf", StringComparison.OrdinalIgnoreCase))
                                        {
                                            RemoveDlcActiveMapRpfFile(platform, overlays);
                                        }
                                        else
                                        { }
                                    }
                                }

                                // filesToEnable
                                if (mapCs.filesToEnable != null)
                                {
                                    foreach (var file in mapCs.filesToEnable)
                                    {
                                        if (string.IsNullOrEmpty(file)) continue;

                                        var platform = GetDlcPlatformPath(file);
                                        var unmounted = GetDlcUnmountedPath(platform);
                                        var physical = GetDlcRpfPhysicalPath(unmounted, setupFile);

                                        if (!string.Equals(platform, unmounted, StringComparison.Ordinal))
                                        { }

                                        AddDlcOverlayRpf(platform, unmounted, setupFile, overlays);
                                        AddDlcActiveMapRpfFile(platform, physical, setupFile);
                                    }
                                }
                            }
                        }
                    }
                }
                // stop after selected DLC
                if (dlcName == SelectedDlc)
                {
                    break;
                }
            }

            AddExtraActiveMapRpfFiles();

            ReportResurrectedYmaps();
        }

        //A map changeset invalidates an rpf by its unprefixed platform path, but DLC packs mount their copies
        //under "dlc_<name>:/..." keys - so a patchday copy of the same rpf survives the invalidate and puts the
        //superseded variant back. Name them, with the key that kept them alive.
        private void ReportResurrectedYmaps()
        {
            if (DisabledYmaps.Count == 0) return;
            var found = new List<string>();
            foreach (var kvp in ActiveMapRpfFiles)
            {
                var entries = kvp.Value?.AllEntries;
                if (entries == null) continue;
                foreach (var entry in entries)
                {
                    if (entry?.NameLower == null) continue;
                    if (!entry.NameLower.EndsWith(".ymap", StringComparison.Ordinal)) continue;
                    if (entry is not RpfFileEntry fe) continue;
                    if (!DisabledYmaps.Contains(fe.ShortNameHash)) continue;
                    found.Add(entry.NameLower + " <- '" + kvp.Key + "'");
                }
            }
            if (found.Count > 0)
            {
                found.Sort(StringComparer.OrdinalIgnoreCase);
                LodDiag.Report(found.Count + " ymap(s) the active DLC invalidated are still mounted:\n    "
                    + string.Join("\n    ", found));
            }
        }

        //Added last so their ymaps/ybns win over the game's, and so Space picks them up as uncached map files.
        private void AddExtraActiveMapRpfFiles()
        {
            if (RpfMan?.ExtraRpfs == null) return;
            foreach (var rpf in ArchiveManager.ExtraRpfs)
            {
                ActiveMapRpfFiles[rpf.Path] = rpf;
            }
        }


        private void AddDlcActiveMapRpfFile(string vpath, string phpath, DlcSetupFile setupfile)
        {
            vpath = vpath.ToLowerInvariant();
            phpath = phpath.ToLowerInvariant();
            if (phpath.EndsWith(".rpf"))
            {
                RpfFile? rpffile = ArchiveManager.FindRpfFile(phpath);
                if (rpffile != null)
                {
                    ActiveMapRpfFiles[vpath] = rpffile;
                    SetYmapsDisabled(rpffile, false); //re-enabled by a later changeset
                }
                else
                { }
            }
            else
            { } //how to handle individual files? eg interiorProxies.meta
        }
        private void AddDlcOverlayRpf(string path, string umpath, DlcSetupFile setupfile, Dictionary<string, List<string>> overlays)
        {
            string opath = GetDlcOverlayPath(umpath, setupfile);
            if (opath == path) return;
            if (!overlays.TryGetValue(opath, out var overlayList))
            {
                overlayList = [];
                overlays[opath] = overlayList;
            }
            overlayList.Add(path);
        }
        private void RemoveDlcActiveMapRpfFile(string vpath, Dictionary<string, List<string>> overlays)
        {
            List<string>? overlayList;
            if (overlays.TryGetValue(vpath, out overlayList))
            {
                foreach (string overlayPath in overlayList)
                {
                    if (ActiveMapRpfFiles.TryGetValue(overlayPath, out var orpf)) SetYmapsDisabled(orpf, true);
                    ActiveMapRpfFiles.Remove(overlayPath);
                }
                overlays.Remove(vpath);
            }

            //Invalidation names an rpf by its unprefixed platform path, but a DLC pack mounts its own copy of
            //the same file under a "dlc_<name>:/..." key. The game drops every mount of the file, so match on
            //the path after the device prefix too - otherwise eg patchday2ng's indust_02_metadata.rpf survives
            //mpheist's invalidate and puts the vanilla id2 variant back alongside the hei_ one.
            vpath = vpath.ToLowerInvariant();
            foreach (var key in ActiveMapRpfFiles.Keys.Where(k => StripDlcDevice(k) == vpath).ToList())
            {
                if (ActiveMapRpfFiles.TryGetValue(key, out var rpf)) SetYmapsDisabled(rpf, true);
                ActiveMapRpfFiles.Remove(key);
            }
        }

        private static string StripDlcDevice(string key)
        {
            var i = key.IndexOf(':');
            return i < 0 ? key : key.Substring(i + 1).TrimStart('/', '\\');
        }

        //A DLC map changeset invalidating an rpf disables the ymaps in it - eg mpheist swaps the whole
        //id2 set for its hei_ variants. Loose extra folders bypass the rpf mounting entirely, so without
        //this they'd resurrect single ymaps of the superseded variant whose LOD parents are no longer loaded.
        private void SetYmapsDisabled(RpfFile rpf, bool disabled)
        {
            var entries = rpf?.AllEntries;
            if (entries == null) return;
            foreach (var entry in entries)
            {
                if (entry?.NameLower == null) continue;
                if (!entry.NameLower.EndsWith(".ymap", StringComparison.Ordinal)) continue;
                if (entry is not RpfFileEntry fe) continue;
                if (disabled) DisabledYmaps.Add(fe.ShortNameHash);
                else DisabledYmaps.Remove(fe.ShortNameHash);
            }
        }
        private string GetDlcRpfPhysicalPath(string path, DlcSetupFile setupfile)
        {
            string devname = setupfile.deviceName.ToLowerInvariant();
            string fpath = GetDlcPlatformPath(path);
            string kpath = fpath;
            string dlcpath = (setupfile.DlcFile ?? throw new InvalidOperationException("DLC archive is not loaded.")).Path;
            
            // Optimize string replacements using Span
            fpath = ReplaceDeviceAndX64Paths(fpath, devname, dlcpath);
            
            if (setupfile.DlcSubpacks != null)
            {
                if (ArchiveManager.FindRpfFile(fpath) == null)
                {
                    foreach (var subpack in setupfile.DlcSubpacks)
                    {
                        dlcpath = subpack.Path;
                        var tpath = ReplaceDeviceAndX64Paths(kpath, devname, dlcpath);
                        if (ArchiveManager.FindRpfFile(tpath) != null)
                        {
                            return GetDlcPatchedPath(tpath);
                        }
                    }
                }
            }
            return GetDlcPatchedPath(fpath);
        }

        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull("source")]
        private string? ReplaceDeviceAndX64Paths(string? source, string devname, string dlcpath)
        {
            if (string.IsNullOrEmpty(source)) return source;

            // Use StringBuilder for multiple replacements
            var sb = new StringBuilder(source.Length + dlcpath.Length);
            ReadOnlySpan<char> sourceSpan = source.AsSpan();
            
            string devnameColon = devname + ":";
            string x64Replacement = dlcpath + "\\x64";
            
            int pos = 0;
            while (pos < sourceSpan.Length)
            {
                // Check for devname: pattern
                if (pos + devnameColon.Length <= sourceSpan.Length && 
                    sourceSpan.Slice(pos, devnameColon.Length).SequenceEqual(devnameColon.AsSpan()))
                {
                    sb.Append(dlcpath);
                    pos += devnameColon.Length;
                    continue;
                }
                
                // Check for x64: pattern
                if (pos + 4 <= sourceSpan.Length && 
                    sourceSpan.Slice(pos, 4).SequenceEqual("x64:".AsSpan()))
                {
                    sb.Append(x64Replacement);
                    pos += 4;
                    continue;
                }
                
                // Replace forward slash with backslash
                char c = sourceSpan[pos];
                sb.Append(c == '/' ? '\\' : c);
                pos++;
            }
            
            return sb.ToString();
        }
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull("path")]
        private string? GetDlcOverlayPath(string? path, DlcSetupFile setupfile)
        {
            if (string.IsNullOrEmpty(path)) return path;

            string devname = setupfile.deviceName.ToLowerInvariant();
            
            // Use Span for efficient string processing
            Span<char> buffer = stackalloc char[path.Length * 2];
            ReadOnlySpan<char> source = path.AsSpan();
            int length = 0;
            
            for (int i = 0; i < source.Length; i++)
            {
                // Check for "%PLATFORM%" pattern
                if (i + 10 <= source.Length && source.Slice(i, 10).SequenceEqual("%PLATFORM%".AsSpan()))
                {
                    buffer[length++] = 'x';
                    buffer[length++] = '6';
                    buffer[length++] = '4';
                    i += 9;
                    continue;
                }
                
                char c = source[i];
                // Replace backslash with forward slash and convert to lowercase
                buffer[length++] = c == '\\' ? '/' : char.ToLowerInvariant(c);
            }
            
            ReadOnlySpan<char> processed = buffer.Slice(0, length);
            
            // Remove devname:/ prefix if present
            string devnamePrefix = devname + ":/";
            if (processed.StartsWith(devnamePrefix.AsSpan()))
            {
                processed = processed.Slice(devnamePrefix.Length);
            }
            
            return processed.ToString();
        }
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull("path")]
        private string? GetDlcRpfVirtualPath(string? path)
        {
            if (string.IsNullOrEmpty(path)) return path;

            // Use Span to avoid allocations
            Span<char> buffer = stackalloc char[path.Length];
            ReadOnlySpan<char> source = path.AsSpan();
            
            // Replace backslashes with forward slashes
            int length = 0;
            for (int i = 0; i < source.Length; i++)
            {
                buffer[length++] = source[i] == '\\' ? '/' : source[i];
            }
            
            ReadOnlySpan<char> processed = buffer.Slice(0, length);
            
            // Remove "mods/" (or "onigiri/") prefix if present
            var modsSlash = RpfManager.ModsFolder + "/";
            if (processed.StartsWith(modsSlash.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                processed = processed.Slice(modsSlash.Length);
            }
            
            // Trim off "dlc.rpf" suffix if present
            if (processed.Length > 7)
            {
                processed = processed.Slice(0, processed.Length - 7);
            }
            
            // Handle x64 prefix
            if (processed.StartsWith("x64".AsSpan()))
            {
                int slashIndex = processed.IndexOf('/');
                if (slashIndex > 0 && slashIndex < processed.Length)
                {
                    // Build "x64" + substring after first slash
                    return string.Concat("x64".AsSpan(), processed.Slice(slashIndex));
                }
            }
            // Replace "update/x64/dlcpacks" with "dlcpacks:"
            else if (processed.StartsWith("update/x64/dlcpacks".AsSpan()))
            {
                return string.Concat("dlcpacks:".AsSpan(), processed.Slice(19));
            }
            // the enhanced mods folder holds its packs directly under "dlcpacks" (the RAGE device name)
            else if (processed.StartsWith("dlcpacks/".AsSpan()))
            {
                return string.Concat("dlcpacks:".AsSpan(), processed.Slice(8));
            }

            return processed.ToString();
        }
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull("path")]
        private string? GetDlcNameFromPath(string? path)
        {
            if (string.IsNullOrEmpty(path)) return path;

            // Use Span to avoid allocating substrings
            ReadOnlySpan<char> pathSpan = path.AsSpan();
            
            // Find the last two backslashes
            int lastBackslash = pathSpan.LastIndexOf('\\');
            if (lastBackslash > 0)
            {
                int secondLastBackslash = pathSpan.Slice(0, lastBackslash).LastIndexOf('\\');
                if (secondLastBackslash >= 0)
                {
                    // Extract the DLC name between the two backslashes
                    ReadOnlySpan<char> dlcName = pathSpan.Slice(secondLastBackslash + 1, lastBackslash - secondLastBackslash - 1);
                    return dlcName.ToString().ToLowerInvariant();
                }
            }
            return path.ToLowerInvariant();
        }
        private static readonly SearchValues<string> DlcPlatformTokens =
            SearchValues.Create(["%PLATFORM%", "platform:"], StringComparison.Ordinal);

        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull("path")]
        public static string? GetDlcPlatformPath(string? path)
        {
            if (string.IsNullOrEmpty(path)) return path;

            // Replacements only shorten the path. Return the actual rented buffer.
            char[]? rented = null;
            Span<char> buffer = path.Length <= 256
                ? stackalloc char[path.Length]
                : (rented = ArrayPool<char>.Shared.Rent(path.Length));
            try
            {
                ReadOnlySpan<char> remaining = path.AsSpan();
                int length = 0;
                // Short paths benefit from direct checks; use multi-pattern search for larger input.
                if (remaining.Length < 64)
                {
                    for (int i = 0; i < remaining.Length; i++)
                    {
                        char c = remaining[i];
                        if (c == '%' && remaining[i..].StartsWith("%PLATFORM%", StringComparison.Ordinal))
                        {
                            "x64".AsSpan().CopyTo(buffer[length..]);
                            length += 3;
                            i += 9;
                        }
                        else if (c == 'p' && remaining[i..].StartsWith("platform:", StringComparison.Ordinal))
                        {
                            "x64".AsSpan().CopyTo(buffer[length..]);
                            length += 3;
                            i += 8;
                        }
                        else buffer[length++] = c == '\\' ? '/' : char.ToLowerInvariant(c);
                    }
                    remaining = default;
                }
                while (!remaining.IsEmpty)
                {
                    int tokenIndex = remaining.IndexOfAny(DlcPlatformTokens);
                    var literal = tokenIndex < 0 ? remaining : remaining[..tokenIndex];
                    foreach (char c in literal)
                    {
                        // Keep the original UTF-16 character casing semantics.
                        buffer[length++] = c == '\\' ? '/' : char.ToLowerInvariant(c);
                    }
                    if (tokenIndex < 0) break;

                    "x64".AsSpan().CopyTo(buffer[length..]);
                    length += 3;
                    int tokenLength = remaining[tokenIndex] == '%' ? 10 : 9;
                    remaining = remaining[(tokenIndex + tokenLength)..];
                }
                var normalized = buffer[..length];
                return normalized.SequenceEqual(path.AsSpan()) ? path : new string(normalized);
            }
            finally
            {
                if (rented != null) ArrayPool<char>.Shared.Return(rented);
            }
        }
        private string GetDlcMountedPath(string path)
        {
            foreach (var efm in DlcExtraFolderMounts)
            {
                foreach (var fm in efm.FolderMounts)
                {
                    if ((fm.platform == null) || (fm.platform == "x64"))
                    {
                        if (path.StartsWith(fm.path))
                        {
                            path = path.Replace(fm.path, fm.mountAs);
                        }
                    }
                }
            }
            return path;
        }
        private string GetDlcUnmountedPath(string path)
        {
            foreach (var efm in DlcExtraFolderMounts)
            {
                foreach (var fm in efm.FolderMounts)
                {
                    if ((fm.platform == null) || (fm.platform == "x64"))
                    {
                        if (path.StartsWith(fm.mountAs))
                        {
                            path = path.Replace(fm.mountAs, fm.path);
                        }
                    }
                }
            }
            return path;
        }
        public string GetDlcPatchedPath(string path)
        {
            string? p;
            if (DlcPatchedPaths.TryGetValue(path, out p))
            {
                return p;
            }
            return path;
        }

        private List<RpfFile> GetModdedRpfList(List<RpfFile> list)
        {
            if (list is not { Count: > 0 }) return [];
            List<RpfFile> result = new(list.Count);
            var modDict = ArchiveManager.ModRpfDict;
            var baseDict = ArchiveManager.RpfDict;
            ReadOnlySpan<char> modsPrefix = RpfManager.ModsFolder.AsSpan();
            int ModsPrefixLen = modsPrefix.Length;

            if (!EnableMods)
            {
                // exclude anything under mods
                foreach (var file in list)
                {
                    if (file == null || string.IsNullOrEmpty(file.Path)) continue;

                    // Use Span for prefix check
                    if (!file.Path.AsSpan().StartsWith(modsPrefix, StringComparison.OrdinalIgnoreCase))
                        result.Add(file);
                }
                return result;
            }
            // EnableMods
            foreach (var file in list)
            {
                if (file == null || string.IsNullOrEmpty(file.Path)) continue;
                var path = file.Path;

                // Use CollectionsMarshal for zero-allocation lookup
                ref var modOverride = ref CollectionsMarshal.GetValueRefOrNullRef(modDict, path);
                if (!Unsafe.IsNullRef(ref modOverride) && modOverride != null)
                {
                    result.Add(modOverride);
                    continue;
                }

                // if entry is from mods, only keep it if it doesnt override a base path
                ReadOnlySpan<char> pathSpan = path.AsSpan();
                if (pathSpan.StartsWith(modsPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    if (path.Length > ModsPrefixLen)
                    {
                        // Use Span to avoid Substring allocation
                        var basePath = path.Substring(ModsPrefixLen);
                        var overridesBase = baseDict != null && baseDict.ContainsKey(basePath);

                        if (!overridesBase)
                        {
                            result.Add(file);
                        }
                    }
                    else
                    {
                        result.Add(file);
                    }
                }
                else
                {
                    result.Add(file);
                }
            }
            return result;
        }


        private List<RpfFile> GetAssetPriorityOrderedRpfs()
        {
            // Asset override priority: base rpfs first, then update/dlc rpfs, so patched and
            // dlc/mod-pack assets deterministically win duplicated names. The raw AllRpfs scan
            // order is alphabetical, which would put base x64 rpfs after dlcpacks and make base
            // assets override dlc ones. Within the dlc partition: official update packs first,
            // then custom packs (e.g. NVE's folder - the game's dlclist appends those last so
            // they override official content), then mods-folder copies last.
            // (Mods substitution is already applied via GetModdedRpfList.)
            // Extra folders (FiveM map resources) go last of all, so they override everything.
            if (AllRpfs is not { Count: > 0 }) return AllRpfs ?? [];
            if (DlcRpfs is not { Count: > 0 }) return AllRpfs; //extras are already at the end of AllRpfs
            var dlcset = new HashSet<RpfFile>(DlcRpfs);
            var extraset = RpfMan?.ExtraRpfs is { Count: > 0 } ? new HashSet<RpfFile>(ArchiveManager.ExtraRpfs) : null;
            int DlcRank(RpfFile r)
            {
                var p = r.Path ?? "";
                if (p.StartsWith(RpfManager.ModsFolderPrefix, StringComparison.OrdinalIgnoreCase)) return 2;
                if (p.StartsWith("update\\", StringComparison.OrdinalIgnoreCase)) return 0;
                return 1; //custom dlc pack folders (NVE etc.)
            }
            var ordered = new List<RpfFile>(AllRpfs.Count);
            foreach (var r in AllRpfs) if (!dlcset.Contains(r) && extraset?.Contains(r) != true) ordered.Add(r);
            for (int rank = 0; rank <= 2; rank++)
            {
                foreach (var r in AllRpfs) if (dlcset.Contains(r) && DlcRank(r) == rank) ordered.Add(r);
            }
            if (extraset != null) foreach (var r in AllRpfs) if (extraset.Contains(r)) ordered.Add(r);
            return ordered;
        }

        private void InitGlobalDicts()
        {
            // Pre-size dictionaries based on estimated file counts for better performance
            YdrDict = new(8192);
            YddDict = new(2048);
            YtdDict = new(4096);
            YftDict = new(1024);
            YcdDict = new(512);
            YedDict = new(256);

            if (AllRpfs is not { Count: > 0 }) return;

            // Build per-rpf local dictionaries in parallel, then merge them in priority order -
            // later rpfs (update/dlc patches) must overwrite earlier ones so the correct asset
            // version wins, same as the game's load order. A completion-order merge is a race
            // that nondeterministically picks stale base-game assets over patched ones.
            var orderedRpfs = GetAssetPriorityOrderedRpfs();
            var localDicts = new Dictionary<uint, RpfFileEntry>[orderedRpfs.Count][];
            Parallel.For(0, orderedRpfs.Count, i =>
            {
                var rpf = orderedRpfs[i];
                if (rpf?.AllEntries == null) return;

                var local = new Dictionary<uint, RpfFileEntry>[6];
                for (int d = 0; d < 6; d++) local[d] = new();

                foreach (var entry in rpf.AllEntries)
                {
                    if (!(entry is RpfFileEntry fentry)) continue;

                    var nameLower = entry.NameLower;
                    if (string.IsNullOrEmpty(nameLower)) continue;

                    // Use Span to check extension without allocation
                    ReadOnlySpan<char> nameSpan = nameLower.AsSpan();

                    if (nameSpan.EndsWith(".ydr".AsSpan()))
                    {
                        local[0][entry.ShortNameHash] = fentry;
                    }
                    else if (nameSpan.EndsWith(".ydd".AsSpan()))
                    {
                        local[1][entry.ShortNameHash] = fentry;
                    }
                    else if (nameSpan.EndsWith(".ytd".AsSpan()))
                    {
                        local[2][entry.ShortNameHash] = fentry;
                    }
                    else if (nameSpan.EndsWith(".yft".AsSpan()))
                    {
                        local[3][entry.ShortNameHash] = fentry;
                    }
                    else if (nameSpan.EndsWith(".ycd".AsSpan()))
                    {
                        local[4][entry.ShortNameHash] = fentry;
                    }
                    else if (nameSpan.EndsWith(".yed".AsSpan()))
                    {
                        local[5][entry.ShortNameHash] = fentry;
                    }
                }

                localDicts[i] = local;
            });

            var globals = new[] { YdrDict, YddDict, YtdDict, YftDict, YcdDict, YedDict };
            foreach (var local in localDicts)
            {
                if (local == null) continue;
                for (int d = 0; d < 6; d++)
                {
                    foreach (var kvp in local[d]) globals[d][kvp.Key] = kvp.Value;
                }
            }
        }

        private void InitMapDicts()
        {
            // Pre-size dictionaries for better performance
            YmapDict = new(2048);
            YbnDict = new(1024);
            YnvDict = new(512);

            // Merge in rpf load order (not parallel completion order) so later rpfs
            // (update/dlc patches) deterministically overwrite earlier ones.
            if (ActiveMapRpfFiles is { Count: > 0 })
            {
                //ActiveMapRpfFiles has entries removed during the DLC pass, so its enumeration order isn't
                //insertion order - put the extras (FiveM resources) at the end explicitly so they win.
                var extras = RpfMan?.ExtraRpfs;
                var mapRpfs = ActiveMapRpfFiles.Values.ToList();
                int extrastart = mapRpfs.Count;
                if (extras is { Count: > 0 })
                {
                    var extraset = new HashSet<RpfFile>(extras);
                    mapRpfs.RemoveAll(extraset.Contains);
                    extrastart = mapRpfs.Count;
                    mapRpfs.AddRange(extras);
                }
                var localDicts = new Dictionary<uint, RpfFileEntry>[mapRpfs.Count][];
                Parallel.For(0, mapRpfs.Count, i =>
                {
                    var rpf = mapRpfs[i];
                    if (rpf?.AllEntries == null) return;

                    var local = new Dictionary<uint, RpfFileEntry>[3];
                    for (int d = 0; d < 3; d++) local[d] = new();

                    foreach (var entry in rpf.AllEntries)
                    {
                        if (!(entry is RpfFileEntry fentry)) continue;

                        var nameLower = entry.NameLower;
                        if (string.IsNullOrEmpty(nameLower)) continue;

                        // Use Span to check extension without allocation
                        ReadOnlySpan<char> nameSpan = nameLower.AsSpan();

                        if (nameSpan.EndsWith(".ymap".AsSpan()))
                        {
                            local[0][entry.ShortNameHash] = fentry;
                        }
                        else if (nameSpan.EndsWith(".ybn".AsSpan()))
                        {
                            local[1][entry.ShortNameHash] = fentry;
                        }
                        else if (nameSpan.EndsWith(".ynv".AsSpan()))
                        {
                            local[2][entry.ShortNameHash] = fentry;
                        }
                    }

                    localDicts[i] = local;
                });

                var globals = new[] { YmapDict, YbnDict, YnvDict };
                var shadowed = new List<string>();
                for (int i = 0; i < localDicts.Length; i++)
                {
                    var local = localDicts[i];
                    if (local == null) continue;
                    var isextra = i >= extrastart;
                    for (int d = 0; d < 3; d++)
                    {
                        foreach (var kvp in local[d])
                        {
                            //an extra ymap replacing a game one has to match its entity order, or the LOD
                            //parent/child indices in the rest of the chain will point at the wrong entities
                            if (isextra && (d == 0) && globals[0].ContainsKey(kvp.Key)) shadowed.Add(kvp.Value.Name);
                            globals[d][kvp.Key] = kvp.Value;
                        }
                    }
                }
                if (shadowed.Count > 0)
                {
                    ErrorLog?.Invoke(shadowed.Count + " game ymap(s) replaced by extra folder copies - LOD links will break if the entity order doesn't match: " + string.Join(", ", shadowed.Take(20)));
                }
            }

            AllYmapsDict = new Dictionary<uint, RpfFileEntry>(4096);
            if (AllRpfs != null && AllRpfs.Count > 0)
            {
                var orderedRpfs = GetAssetPriorityOrderedRpfs();
                var localYmaps = new Dictionary<uint, RpfFileEntry>[orderedRpfs.Count];
                Parallel.For(0, orderedRpfs.Count, i =>
                {
                    var rpf = orderedRpfs[i];
                    if (rpf?.AllEntries == null) return;

                    var local = new Dictionary<uint, RpfFileEntry>();

                    foreach (var entry in rpf.AllEntries)
                    {
                        if (!(entry is RpfFileEntry fentry)) continue;

                        var nameLower = entry.NameLower;
                        if (string.IsNullOrEmpty(nameLower)) continue;

                        // Optimize extension check for .ymap files
                        if (nameLower.EndsWith(".ymap", StringComparison.Ordinal))
                        {
                            local[entry.ShortNameHash] = fentry;
                        }
                    }

                    localYmaps[i] = local;
                });

                foreach (var local in localYmaps)
                {
                    if (local == null) continue;
                    foreach (var kvp in local) AllYmapsDict[kvp.Key] = kvp.Value;
                }
            }
        }

        private void InitManifestDicts()
        {
            AllManifests = [];
            hdtexturelookup = new();
            IEnumerable<RpfFile> rpfs = PreloadedMode ? AllRpfs : (IEnumerable<RpfFile>)ActiveMapRpfFiles.Values;
            foreach (RpfFile file in rpfs)
            {
                if (file.AllEntries == null) continue;
                foreach (RpfEntry entry in file.AllEntries)
                {
                    if (entry.Name.EndsWith(".ymf"))
                    {
                        try
                        {
                            UpdateStatus?.Invoke(entry.Path);
                            YmfFile? ymffile = ArchiveManager.GetFile<YmfFile>(entry);
                            if (ymffile != null)
                            {
                                AllManifests.Add(ymffile);
                                if (ymffile.HDTxdAssetBindings != null)
                                {
                                    for (int i = 0; i < ymffile.HDTxdAssetBindings.Length; i++)
                                    {
                                        var b = ymffile.HDTxdAssetBindings[i];
                                        var targetasset = JenkHash.GenHash(b.targetAsset.ToString().ToLowerInvariant());
                                        var hdtxd = JenkHash.GenHash(b.HDTxd.ToString().ToLowerInvariant());
                                        hdtexturelookup[targetasset] = hdtxd;
                                    }
                                }

                            }
                        }
                        catch (Exception ex)
                        {
                            string errstr = entry.Path + "\n" + ex.ToString();
                            ErrorLog?.Invoke(errstr);
                        }
                    }

                }

            }
        }

        private void InitGtxds()
        {

            var parentTxds = new Dictionary<MetaHash, MetaHash>();

            IEnumerable<RpfFile> rpfs = PreloadedMode ? AllRpfs : (IEnumerable<RpfFile>)ActiveMapRpfFiles.Values;

            var addTxdRelationships = new Action<Dictionary<string, string>>((from) =>
            {
                foreach (var kvp in from)
                {
                    uint chash = JenkHash.GenHashLowerInvariant(kvp.Key);
                    uint phash = JenkHash.GenHashLowerInvariant(kvp.Value);
                    parentTxds.TryAdd(chash, phash);
                }
            });

            //Collect gtxd/vehicles.meta entries in discovery order, parse them in parallel (the parse is
            //the cost), then merge their relationships sequentially so the TryAdd first-wins ordering is
            //preserved exactly.
            List<RpfEntry> txdEntries = [];
            void CollectTxdEntries(IEnumerable<RpfFile> from)
            {
                if (from == null) return;
                foreach (RpfFile file in from)
                {
                    if (file.AllEntries == null) continue;
                    foreach (RpfEntry entry in file.AllEntries)
                    {
                        var nl = entry.NameLower;
                        if (nl == "gtxd.ymt" || nl == "gtxd.meta" || nl == "mph4_gtxd.ymt" || nl == "vehicles.meta")
                        {
                            txdEntries.Add(entry);
                        }
                    }
                }
            }
            CollectTxdEntries(rpfs);
            if (EnableDlc)
            {
                CollectTxdEntries(DlcActiveRpfs);
            }

            var txdRels = new Dictionary<string, string>?[txdEntries.Count];
            Parallel.For(0, txdEntries.Count, i =>
            {
                var entry = txdEntries[i];
                try
                {
                    if (entry.NameLower == "vehicles.meta")
                    {
                        txdRels[i] = ArchiveManager.GetFile<VehiclesFile>(entry)?.TxdRelationships;
                    }
                    else
                    {
                        txdRels[i] = ArchiveManager.GetFile<GtxdFile>(entry)?.TxdRelationships;
                    }
                }
                catch (Exception ex)
                {
                    ErrorLog?.Invoke(entry.Path + "\n" + ex.ToString());
                }
            });

            foreach (var rels in txdRels)
            {
                if (rels != null) addTxdRelationships(rels);
            }


            textureParents = parentTxds;




            //ensure resident global texture dicts:
            if (GetYtdEntry(JenkHash.GenHash("mapdetail")) is { } entry1)
            {
                YtdFile ytd1 = new(entry1);
                LoadFile(ytd1);
                AddTextureLookups(ytd1);
            }

            if (GetYtdEntry(JenkHash.GenHash("vehshare")) is { } entry2)
            {
                YtdFile ytd2 = new(entry2);
                LoadFile(ytd2);
                AddTextureLookups(ytd2);
            }



        }

        private void InitMapCaches()
        {
            AllCacheFiles = [];
            YmapHierarchyDict = new();


            CacheDatFile? loadCacheFile(string path, bool finalAttempt)
            {
                try
                {
                    var cache = ArchiveManager.GetFile<CacheDatFile>(path);
                    if (cache != null)
                    {
                        AllCacheFiles.Add(cache);
                        foreach (var node in cache.AllMapNodes)
                        {
                            if (YmapDict.ContainsKey(node.Name))
                            {
                                YmapHierarchyDict[node.Name] = node;
                            }
                        }
                    }
                    else if (finalAttempt)
                    {
                        ErrorLog?.Invoke(path + ": main cachefile not loaded! Possibly an unsupported GTAV installation version.");
                    }
                    return cache;
                }
                catch (Exception ex)
                {
                    ErrorLog?.Invoke(path + ": " + ex.ToString());
                }
                return null;
            }

            CacheDatFile? maincache = null;
            if (EnableDlc)
            {
                maincache = loadCacheFile("update\\update.rpf\\common\\data\\gta5_cache_y.dat", false);
                if (maincache == null)
                {
                    maincache = loadCacheFile("update\\update2.rpf\\common\\data\\gta5_cache_y.dat", true);
                }
            }
            else
            {
                maincache = loadCacheFile("common.rpf\\data\\gta5_cache_y.dat", true);
            }

            if (EnableDlc)
            {
                foreach (string dlccachefile in DlcCacheFileList)
                {
                    loadCacheFile(dlccachefile, false);
                }
            }
        }

        private void InitArchetypeDicts()
        {
            YtypDict = new Dictionary<uint, YtypFile>(512);
            archetypesLoaded = false;
            archetypeDict.Clear();

            if (!LoadArchetypes) return;
            List<RpfFile> rpfs;
            if (EnableDlc)
            {
                rpfs = GetAssetPriorityOrderedRpfs();
            }
            else
            {
                rpfs = new List<RpfFile>(BaseRpfs);
                if (RpfMan?.ExtraRpfs != null) rpfs.AddRange(ArchiveManager.ExtraRpfs);
            }

            // Collect all .ytyp entries first to avoid repeated file system access
            List<RpfEntry> ytypEntries = [];
            foreach (RpfFile file in rpfs)
            {
                if (file.AllEntries == null) continue;
                if (!EnableDlc && file.Path.StartsWith("update")) continue;

                foreach (RpfEntry entry in file.AllEntries)
                {
                    if (entry.NameLower.EndsWith(".ytyp", StringComparison.Ordinal))
                    {
                        ytypEntries.Add(entry);
                    }
                }
            }

            // Parse .ytyp files in parallel, but populate the dictionaries in rpf load order -
            // later rpfs (update/dlc/mods) must overwrite earlier ones so duplicated archetypes
            // resolve to the correct patched version, not a nondeterministic race winner.
            var ytypFiles = new YtypFile?[ytypEntries.Count];
            var exceptions = new ConcurrentBag<Exception>();

            Parallel.For(0, ytypEntries.Count, i =>
            {
                try
                {
                    ytypFiles[i] = ArchiveManager.GetFile<YtypFile>(ytypEntries[i]);
                }
                catch (Exception ex)
                {
                    exceptions.Add(new Exception($"{ytypEntries[i].Path}: {ex.Message}", ex));
                }
            });

            for (int i = 0; i < ytypEntries.Count; i++)
            {
                var ytypfile = ytypFiles[i];
                if (ytypfile?.Meta == null) continue;

                UpdateStatus?.Invoke(ytypEntries[i].Path);

                YtypDict[ytypfile.NameHash] = ytypfile;

                if (ytypfile.AllArchetypes?.Length > 0)
                {
                    foreach (var arch in ytypfile.AllArchetypes)
                    {
                        uint hash = arch.Hash;
                        if (hash != 0)
                        {
                            archetypeDict[hash] = arch;
                        }
                    }
                }
                else
                {
                    ErrorLog?.Invoke(ytypEntries[i].Path + ": no archetypes found");
                }
            }

            // Report any exceptions that occurred during parallel processing
            foreach (var ex in exceptions)
            {
                ErrorLog?.Invoke(ex.Message);
            }

            archetypesLoaded = true;
        }

        private void AddYtypToDictionary(RpfEntry entry)
        {
            UpdateStatus?.Invoke(entry.Path);
            YtypFile? ytypfile = ArchiveManager.GetFile<YtypFile>(entry);
            if (ytypfile == null)
            {
                throw new Exception("Couldn't load ytyp file."); //couldn't load the file for some reason... shouldn't happen..
            }
            if (ytypfile.Meta == null)
            {
                throw new Exception("ytyp file was not in meta format.");
            }
            YtypDict[ytypfile.NameHash] = ytypfile; //override ytyp and continue anyway, could be unique archetypes in here still...

            if ((ytypfile.AllArchetypes == null) || (ytypfile.AllArchetypes.Length == 0))
            {
                ErrorLog?.Invoke(entry.Path + ": no archetypes found");
            }
            else
            {
                foreach (var arch in ytypfile.AllArchetypes)
                {
                    uint hash = arch.Hash;
                    if (hash == 0) continue;
                    archetypeDict[hash] = arch;
                }
            }
        }

        public void InitStringDicts()
        {
            string langstr = "american_rel"; //todo: make this variable?
            string langstr2 = "americandlc.rpf";
            string langstr3 = "american.rpf";

            Gxt2Dict = new(256);
            List<Gxt2File> gxt2files = [];

            // Collect relevant entries first to reduce iterations
            List<RpfEntry> relevantEntries = [];
            foreach (var rpf in AllRpfs)
            {
                if (rpf?.AllEntries == null) continue;
                
                foreach (var entry in rpf.AllEntries)
                {
                    if (entry is RpfFileEntry fentry)
                    {
                        var nameLower = entry.NameLower;
                        if (nameLower.EndsWith(".gxt2", StringComparison.Ordinal))
                        {
                            var p = entry.Path;
                            if (p.Contains(langstr) || p.Contains(langstr2) || p.Contains(langstr3))
                            {
                                Gxt2Dict[entry.ShortNameHash] = fentry;
                                if (DoFullStringIndex)
                                {
                                    relevantEntries.Add(entry);
                                }
                            }
                        }
                        else if (DoFullStringIndex && nameLower.EndsWith("statssetup.xml", StringComparison.Ordinal))
                        {
                            relevantEntries.Add(entry);
                        }
                    }
                }
            }

            if (!DoFullStringIndex)
            {
                string globalgxt2path = "x64b.rpf\\data\\lang\\" + langstr + ".rpf\\global.gxt2";
                var globalgxt2 = ArchiveManager.GetFile<Gxt2File>(globalgxt2path);
                if (globalgxt2?.TextEntries != null)
                {
                    foreach (var e in globalgxt2.TextEntries)
                    {
                        GlobalText.Ensure(e.Text, e.Hash);
                    }
                }
                return;
            }

            // Process entries in parallel for better performance
            var lockObj = new object();
            Parallel.ForEach(relevantEntries, entry =>
            {
                try
                {
                    if (entry.NameLower.EndsWith(".gxt2", StringComparison.Ordinal))
                    {
                        var gxt2 = ArchiveManager.GetFile<Gxt2File>(entry);
                        if (gxt2?.TextEntries != null)
                        {
                            lock (lockObj)
                            {
                                foreach (var e in gxt2.TextEntries)
                                {
                                    GlobalText.Ensure(e.Text, e.Hash);
                                }
                                gxt2files.Add(gxt2);
                            }
                        }
                    }
                    else if (entry.NameLower.EndsWith("statssetup.xml", StringComparison.Ordinal))
                    {
                        var xml = ArchiveManager.GetFileXml(entry.Path);
                        if (xml != null)
                        {
                            var statnodes = xml.SelectNodes("StatsSetup/stats/stat")?.Cast<XmlNode>().ToArray() ?? [];
                            if (statnodes != null)
                            {
                                lock (lockObj)
                                {
                                    foreach (XmlNode statnode in statnodes)
                                    {
                                        if (statnode == null) continue;
                                        
                                        var statname = Xml.GetStringAttribute(statnode, "Name");
                                        if (string.IsNullOrEmpty(statname)) continue;

                                        var statnamel = statname.ToLowerInvariant();
                                        StatsNames.Ensure(statname);
                                        StatsNames.Ensure(statnamel);
                                        StatsNames.Ensure("sp_" + statnamel);
                                        StatsNames.Ensure("mp0_" + statnamel);
                                        StatsNames.Ensure("mp1_" + statnamel);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ErrorLog?.Invoke($"Error processing {entry.Path}: {ex.Message}");
                }
            });

            GlobalText.FullIndexBuilt = true;
            StatsNames.FullIndexBuilt = true;
        }


        //Neos7
        //Involved files(at least for rendering purpose)
        //Vehicles.meta
        //Carcols.meta
        //Carvariations.meta
        //Vehiclelayouts.meta
        //The other metas shouldn't be important for rendering
        //Then the global carcols.ymt is required too
        //As it contains the general shared tuning options
        //Carcols for modkits and lights kits definitions
        //Carvariations links such modkits and lights kits to each vehicle plus defines colours combinations of spawned vehicles
        //Vehiclelayouts mostly to handle ped interactions with the vehicle
        public void InitVehicles()
        {
            if (!LoadVehicles) return;
            IEnumerable<RpfFile> rpfs = PreloadedMode? (IEnumerable<RpfFile>)AllRpfs : (IEnumerable<RpfFile>)ActiveMapRpfFiles.Values;

            Dictionary<MetaHash, VehicleInitData> allVehicles = new();

            //Only vehicles.meta feeds VehiclesInitDict (the sole stored output). carcols/carmodcols/
            //carvariations/vehiclelayouts were previously parsed into locals that were never stored and
            //GetFile<T> has no cache side-effect, so that work was discarded - it's been removed.
            //
            //Collect the vehicles.meta entries first, parse them in parallel (decompress + meta parse is
            //the expensive part), then merge into the dictionary sequentially in discovery order so the
            //last-write-wins override behaviour is preserved exactly.
            List<RpfEntry> vehicleEntries = [];
            void CollectVehicleEntries(IEnumerable<RpfFile> from)
            {
                if (from == null) return;
                foreach (var file in from)
                {
                    if (file == null || file.AllEntries == null) continue;
                    foreach (var entry in file.AllEntries)
                    {
                        if (entry?.NameLower == "vehicles.meta") vehicleEntries.Add(entry);
                    }
                }
            }
            CollectVehicleEntries(rpfs);
            if (EnableDlc)
            {
                CollectVehicleEntries(DlcActiveRpfs);
            }

            var vehicleFiles = new VehiclesFile?[vehicleEntries.Count];
            Parallel.For(0, vehicleEntries.Count, i =>
            {
                try { vehicleFiles[i] = ArchiveManager.GetFile<VehiclesFile>(vehicleEntries[i]); }
                catch (Exception ex) { ErrorLog?.Invoke(vehicleEntries[i].Path + ": " + ex.Message); }
            });

            foreach (var vf in vehicleFiles)
            {
                if (vf?.InitDatas == null) continue;
                foreach (var initData in vf.InitDatas)
                {
                    if (initData == null || string.IsNullOrEmpty(initData.modelName)) continue;
                    var hash = JenkHash.GenHashLowerInvariant(initData.modelName);
                    allVehicles[hash] = initData;
                }
            }
            VehiclesInitDict = allVehicles;
        }


        public void InitPeds()
        {
            if (!LoadPeds) return;
            IEnumerable<RpfFile> rpfs = PreloadedMode ? (IEnumerable<RpfFile>)AllRpfs : (IEnumerable<RpfFile>)ActiveMapRpfFiles.Values;

            List<RpfFile> dlcRpfs = [];
            if (EnableDlc && DlcActiveRpfs != null)
            {
                foreach (var rpf in DlcActiveRpfs)
                {
                    if (rpf == null) continue;
                    dlcRpfs.Add(rpf);

                    if (rpf.Children == null) continue;
                    foreach (var child in rpf.Children)
                    {
                        if (child != null) dlcRpfs.Add(child);
                    }
                }
            }
            Dictionary<MetaHash, CPedModelInfo__InitData> allPeds = new();
            List<PedsFile> allPedsFiles = [];
            Dictionary<MetaHash, PedFile> allPedYmts = new();
            Dictionary<MetaHash, Dictionary<MetaHash, RpfFileEntry>> allPedDrwDicts = new();
            Dictionary<MetaHash, Dictionary<MetaHash, RpfFileEntry>> allPedTexDicts = new();
            Dictionary<MetaHash, Dictionary<MetaHash, RpfFileEntry>> allPedClothDicts = new();


            Dictionary<MetaHash, RpfFileEntry> EnsureDict(Dictionary<MetaHash, Dictionary<MetaHash, RpfFileEntry>> coll,MetaHash key)
            {
                if (!coll.TryGetValue(key, out var dict))
                {
                    dict = new();
                    coll[key] = dict;
                }
                return dict;
            }

            // collect drawable/texture/cloth dicts for a ped
            void AddPedDicts(string shortNameLower, MetaHash pedHash, RpfDirectoryEntry? dir)
            {
                if (dir == null) return;

                // .yld
                var files = dir.Files;
                if (files != null)
                {
                    foreach (var f in files)
                    {
                        if (f == null || f.NameLower == null) continue;
                        if (f.NameLower == shortNameLower + ".yld")
                        {
                            var dict = EnsureDict(allPedClothDicts, pedHash);
                            dict[f.ShortNameHash] = f;
                        }
                    }
                }
                if (dir.Directories != null)
                {
                    RpfDirectoryEntry? childDir = null;
                    foreach (var c in dir.Directories)
                    {
                        if (c != null && c.NameLower == shortNameLower)
                        {
                            childDir = c;
                            break;
                        }
                    }

                    files = childDir != null ? childDir.Files : null;
                    if (files != null)
                    {
                        foreach (var f in files)
                        {
                            if (f == null || f.NameLower == null) continue;
                            if (f.NameLower.EndsWith(".ydd", StringComparison.Ordinal))
                            {
                                var dict = EnsureDict(allPedDrwDicts, pedHash);
                                dict[f.ShortNameHash] = f;
                            }
                            else if (f.NameLower.EndsWith(".ytd", StringComparison.Ordinal))
                            {
                                var dict = EnsureDict(allPedTexDicts, pedHash);
                                dict[f.ShortNameHash] = f;
                            }
                            else if (f.NameLower.EndsWith(".yld", StringComparison.Ordinal))
                            {
                                var dict = EnsureDict(allPedClothDicts, pedHash);
                                dict[f.ShortNameHash] = f;
                            }
                        }
                    }
                }
            }

            // peds.ymt / peds.meta - collect entries in order, parse in parallel, merge sequentially.
            List<RpfEntry> pedsFileEntries = [];
            void CollectPedsFiles(IEnumerable<RpfFile> from)
            {
                if (from == null) return;
                foreach (var file in from)
                {
                    if (file == null || file.AllEntries == null) continue;
                    foreach (var entry in file.AllEntries)
                    {
                        if (entry == null) continue;
                        var nameLower = entry.NameLower;
                        if (nameLower == "peds.ymt" || nameLower == "peds.meta") pedsFileEntries.Add(entry);
                    }
                }
            }
            CollectPedsFiles(rpfs);
            if (dlcRpfs.Count > 0) CollectPedsFiles(dlcRpfs);

            var pedsFiles = new PedsFile?[pedsFileEntries.Count];
            Parallel.For(0, pedsFileEntries.Count, i =>
            {
                try { pedsFiles[i] = ArchiveManager.GetFile<PedsFile>(pedsFileEntries[i]); }
                catch (Exception ex) { ErrorLog?.Invoke(pedsFileEntries[i].Path + ": " + ex.Message); }
            });

            foreach (var pf in pedsFiles)
            {
                if (pf == null) continue;
                var list = pf.InitDataList != null ? pf.InitDataList.InitDatas : null;
                if (list != null)
                {
                    foreach (var init in list)
                    {
                        if (init == null || string.IsNullOrEmpty(init.Name)) continue;
                        var hash = JenkHash.GenHashLowerInvariant(init.Name);
                        allPeds[hash] = init;
                    }
                }
                allPedsFiles.Add(pf);
            }

            // individual *.ymt ped files - collect those referenced by allPeds, parse in parallel,
            // then assemble drawable/texture/cloth dicts sequentially (the dict assembly is cheap; the
            // PedFile parse is the cost).
            List<RpfEntry> pedYmtEntries = [];
            void CollectPedFiles(IEnumerable<RpfFile> from)
            {
                if (from == null) return;
                foreach (var file in from)
                {
                    if (file == null || file.AllEntries == null) continue;
                    foreach (var entry in file.AllEntries)
                    {
                        if (entry == null) continue;
                        var nameLower = entry.NameLower;
                        if (string.IsNullOrEmpty(nameLower)) continue;
                        if (!nameLower.EndsWith(".ymt", StringComparison.Ordinal)) continue;
                        var shortLower = entry.GetShortNameLower();
                        if (string.IsNullOrEmpty(shortLower)) continue;
                        if (!allPeds.ContainsKey(JenkHash.GenHash(shortLower))) continue;
                        pedYmtEntries.Add(entry);
                    }
                }
            }
            CollectPedFiles(rpfs);
            if (dlcRpfs.Count > 0) CollectPedFiles(dlcRpfs);

            var pedFiles = new PedFile?[pedYmtEntries.Count];
            Parallel.For(0, pedYmtEntries.Count, i =>
            {
                try { pedFiles[i] = ArchiveManager.GetFile<PedFile>(pedYmtEntries[i]); }
                catch (Exception ex) { ErrorLog?.Invoke(pedYmtEntries[i].Path + ": " + ex.Message); }
            });

            for (int i = 0; i < pedYmtEntries.Count; i++)
            {
                var pf = pedFiles[i];
                if (pf == null) continue;
                var entry = pedYmtEntries[i];
                var shortLower = entry.GetShortNameLower();
                var pedHash = JenkHash.GenHash(shortLower);
                allPedYmts[pedHash] = pf;
                AddPedDicts(shortLower, pedHash, entry.Parent);
            }

            PedsInitDict = allPeds;
            PedVariationsDict = allPedYmts;
            PedDrawableDicts = allPedDrwDicts;
            PedTextureDicts = allPedTexDicts;
            PedClothDicts = allPedClothDicts;
        }


        public void InitAudio()
        {
            if (!LoadAudio) return;

            Dictionary<uint, RpfFileEntry> datrelentries = new();
            void addRpfDatRelEntries(RpfFile rpffile)
            {
                if (rpffile.AllEntries == null) return;
                foreach (var entry in rpffile.AllEntries)
                {
                    if (entry is RpfFileEntry fentry && entry.NameLower.EndsWith(".rel"))
                    {
                        datrelentries[entry.NameHash] = fentry;
                    }
                }
            }

            var audrpf = ArchiveManager.FindRpfFile("x64\\audio\\audio_rel.rpf");
            if (audrpf != null)
            {
                addRpfDatRelEntries(audrpf);
            }

            if (EnableDlc)
            {
                var updrpf = ArchiveManager.FindRpfFile("update\\update.rpf");
                if (updrpf != null)
                {
                    addRpfDatRelEntries(updrpf);
                }
                foreach (var dlcrpf in DlcActiveRpfs) //load from current dlc rpfs
                {
                    addRpfDatRelEntries(dlcrpf);
                }
                if (DlcActiveRpfs.Count == 0) //when activated from RPF explorer... DLCs aren't initialised fully
                {
                    foreach (var rpf in AllRpfs) //this is a bit of a hack - DLC orders won't be correct so likely will select wrong versions of things
                    {
                        if (rpf.NameLower.StartsWith("dlc"))
                        {
                            addRpfDatRelEntries(rpf);
                        }
                    }
                }
            }


            List<RelFile> audioDatRelFiles = [];
            Dictionary<MetaHash, RelData> audioConfigDict = new();
            Dictionary<MetaHash, RelData> audioSpeechDict = new();
            Dictionary<MetaHash, RelData> audioSynthsDict = new();
            Dictionary<MetaHash, RelData> audioMixersDict = new();
            Dictionary<MetaHash, RelData> audioCurvesDict = new();
            Dictionary<MetaHash, RelData> audioCategsDict = new();
            Dictionary<MetaHash, RelData> audioSoundsDict = new();
            Dictionary<MetaHash, RelData> audioGameDict = new();



            //Parse the .rel audio files in parallel (decompress + parse is the cost), then merge into
            //the per-type dictionaries sequentially in the original order to preserve last-write-wins.
            var relEntryList = datrelentries.Values.ToList();
            var relFiles = new RelFile?[relEntryList.Count];
            Parallel.For(0, relEntryList.Count, i =>
            {
                try { relFiles[i] = ArchiveManager.GetFile<RelFile>(relEntryList[i]); }
                catch (Exception ex) { ErrorLog?.Invoke(relEntryList[i].Path + ": " + ex.Message); }
            });

            foreach (var relfile in relFiles)
            {
                if (relfile == null) continue;

                audioDatRelFiles.Add(relfile);

                var d = audioGameDict;
                var t = relfile.RelType;
                switch (t)
                {
                    case RelDatFileType.Dat4:
                        d = relfile.IsAudioConfig ? audioConfigDict : audioSpeechDict;
                        break;
                    case RelDatFileType.Dat10ModularSynth:
                        d = audioSynthsDict;
                        break;
                    case RelDatFileType.Dat15DynamicMixer:
                        d = audioMixersDict;
                        break;
                    case RelDatFileType.Dat16Curves:
                        d = audioCurvesDict;
                        break;
                    case RelDatFileType.Dat22Categories:
                        d = audioCategsDict;
                        break;
                    case RelDatFileType.Dat54DataEntries:
                        d = audioSoundsDict;
                        break;
                    case RelDatFileType.Dat149:
                    case RelDatFileType.Dat150:
                    case RelDatFileType.Dat151:
                    default:
                        d = audioGameDict;
                        break;
                }

                foreach (var reldata in relfile.RelDatas)
                {
                    if (reldata.NameHash == 0) continue;
                    //if (d.TryGetValue(reldata.NameHash, out var exdata) && (exdata.TypeID != reldata.TypeID))
                    //{ }//sanity check
                    d[reldata.NameHash] = reldata;
                }

            }




            AudioDatRelFiles = audioDatRelFiles;
            AudioConfigDict = audioConfigDict;
            AudioSpeechDict = audioSpeechDict;
            AudioSynthsDict = audioSynthsDict;
            AudioMixersDict = audioMixersDict;
            AudioCurvesDict = audioCurvesDict;
            AudioCategsDict = audioCategsDict;
            AudioSoundsDict = audioSoundsDict;
            AudioGameDict = audioGameDict;

        }





        public bool SetDlcLevel(string dlc, bool enable)
        {
            bool dlcchange = (dlc != SelectedDlc);
            bool enablechange = (enable != EnableDlc);
            bool change = (dlcchange && enable) || enablechange;

            if (change)
            {
                lock (updateSyncRoot)
                {
                    //lock (textureSyncRoot)
                    {
                        SelectedDlc = dlc;
                        EnableDlc = enable;

                        //mainCache.Clear();
                        ClearCachedMaps();

                        InitDlcAsync(UpdateStatus is null ? null : new Progress<string>(UpdateStatus), CancellationToken.None).GetAwaiter().GetResult();
                    }
                }
            }

            return change;
        }

        public bool SetModsEnabled(bool enable)
        {
            bool change = (enable != EnableMods);

            if (change)
            {
                lock (updateSyncRoot)
                {
                    //lock (textureSyncRoot)
                    {
                        EnableMods = enable;
                        ArchiveManager.EnableMods = enable;

                        mainCache.Clear();

                        InitGlobalAsync(UpdateStatus is null ? null : new Progress<string>(UpdateStatus), CancellationToken.None).GetAwaiter().GetResult();
                        InitDlcAsync(UpdateStatus is null ? null : new Progress<string>(UpdateStatus), CancellationToken.None).GetAwaiter().GetResult();
                    }
                }
            }

            return change;
        }


        private void ClearCachedMaps()
        {
            if (AllYmapsDict != null)
            {
                foreach (var ymap in AllYmapsDict.Values)
                {
                    GameFileCacheKey k = new(ymap.ShortNameHash, GameFileType.Ymap);
                    mainCache.Remove(k);
                }
            }
        }




        public void AddProjectFile(GameFile? f)
        {
            if (f == null) return;
            if (f.RpfFileEntry == null) return;
            if (f.RpfFileEntry.ShortNameHash == 0)
            {
                f.RpfFileEntry.ShortNameHash = JenkHash.GenHash(f.RpfFileEntry.GetShortNameLower());
            }
            var key = new GameFileCacheKey(f.RpfFileEntry.ShortNameHash, f.Type);
            lock (requestSyncRoot)
            {
                projectFiles[key] = f;
            }
        }
        public void RemoveProjectFile(GameFile? f)
        {
            if (f == null) return;
            if (f.RpfFileEntry == null) return;
            if (f.RpfFileEntry.ShortNameHash == 0) return;
            var key = new GameFileCacheKey(f.RpfFileEntry.ShortNameHash, f.Type);
            lock (requestSyncRoot)
            {
                projectFiles.Remove(key);
            }
        }
        public void ClearProjectFiles()
        {
            lock (requestSyncRoot)
            {
                projectFiles.Clear();
            }
        }

        public void AddProjectArchetype(Archetype a)
        {
            if (a == null || a.Hash == 0) return;
            lock (requestSyncRoot)
            {
                projectArchetypes[a.Hash] = a;
            }
        }
        public void RemoveProjectArchetype(Archetype a)
        {
            if (a == null || a.Hash == 0) return;
            Archetype? tarch = null;
            lock (requestSyncRoot)
            {
                projectArchetypes.TryGetValue(a.Hash, out tarch);
                if (tarch == a)
                {
                    projectArchetypes.Remove(a.Hash);
                }
            }
        }
        public void ClearProjectArchetypes()
        {
            lock (requestSyncRoot)
            {
                projectArchetypes.Clear();
            }
        }

        public void TryLoadEnqueue(GameFile gf)
        {
            //LoadQueued is cleared as soon as the content thread dequeues, so this dedupes the queue without
            //blocking retries. Without it every frame re-enqueues the same handful of files, and the queue
            //cap then drops everything else that frame - which is why loading used to trickle in.
            //requestQueueCount rather than requestQueue.Count: this runs for every Get* call on the render
            //thread, and ConcurrentQueue.Count walks the segment list.
            if ((!gf.Loaded) && (!gf.LoadQueued) && (Interlocked.CompareExchange(ref requestQueueCount, 0, 0) < MaxQueueLength))
            {
                gf.LoadQueued = true; //set before enqueueing, or the content thread's clear can land first and strand it
                gf.LastUseTime = mainCache.CurrentTime;
                Interlocked.Increment(ref requestQueueCount);
                requestQueue.Enqueue(gf);
            }
        }


        public Archetype? GetArchetype(uint hash)
        {
            if (!archetypesLoaded) return null;
            
            // Use CollectionsMarshal for zero-allocation lookups
            ref var projectArch = ref CollectionsMarshal.GetValueRefOrNullRef(projectArchetypes, hash);
            if (!Unsafe.IsNullRef(ref projectArch))
            {
                return projectArch;
            }
            
            ref var arch = ref CollectionsMarshal.GetValueRefOrNullRef(archetypeDict, hash);
            return !Unsafe.IsNullRef(ref arch) ? arch : null;
        }
        public MapDataStoreNode? GetMapNode(uint hash)
        {
            if (!IsInited) return null;
            MapDataStoreNode? node = null;
            YmapHierarchyDict.TryGetValue(hash, out node);
            return node;
        }

        public YdrFile? GetYdr(uint hash)
        {
            if (!IsInited) return null;
            lock (requestSyncRoot)
            {
                var key = new GameFileCacheKey(hash, GameFileType.Ydr);
                if (projectFiles.TryGetValue(key, out GameFile? pgf))
                {
                    return pgf as YdrFile;
                }
                var ydr = mainCache.TryGet(key) as YdrFile;
                if (ydr == null)
                {
                    var e = GetYdrEntry(hash);
                    if (e != null)
                    {
                        ydr = new YdrFile(e);
                        if (mainCache.TryAdd(key, ydr))
                        {
                            TryLoadEnqueue(ydr);
                        }
                        else
                        {
                            ydr.LoadQueued = false;
                            //ErrorLog?.Invoke("Out of cache space - couldn't load drawable: " + JenkIndex.GetString(hash)); //too spammy...
                        }
                    }
                    else
                    {
                        //ErrorLog?.Invoke("Drawable not found: " + JenkIndex.GetString(hash)); //too spammy...
                    }
                }
                else if (!ydr.Loaded)
                {
                    TryLoadEnqueue(ydr);
                }
                return ydr;
            }
        }
        public YddFile? GetYdd(uint hash)
        {
            if (!IsInited) return null;
            lock (requestSyncRoot)
            {
                var key = new GameFileCacheKey(hash, GameFileType.Ydd);
                if (projectFiles.TryGetValue(key, out GameFile? pgf))
                {
                    return pgf as YddFile;
                }
                var ydd = mainCache.TryGet(key) as YddFile;
                if (ydd == null)
                {
                    var e = GetYddEntry(hash);
                    if (e != null)
                    {
                        ydd = new YddFile(e);
                        if (mainCache.TryAdd(key, ydd))
                        {
                            TryLoadEnqueue(ydd);
                        }
                        else
                        {
                            ydd.LoadQueued = false;
                            //ErrorLog?.Invoke("Out of cache space - couldn't load drawable dictionary: " + JenkIndex.GetString(hash)); //too spammy...
                        }
                    }
                    else
                    {
                        //ErrorLog?.Invoke("Drawable dictionary not found: " + JenkIndex.GetString(hash)); //too spammy...
                    }
                }
                else if (!ydd.Loaded)
                {
                    TryLoadEnqueue(ydd);
                }
                return ydd;
            }
        }
        public YtdFile? GetYtd(uint hash)
        {
            if (!IsInited) return null;
            lock (requestSyncRoot)
            {
                var key = new GameFileCacheKey(hash, GameFileType.Ytd);
                if (projectFiles.TryGetValue(key, out GameFile? pgf))
                {
                    return pgf as YtdFile;
                }
                var ytd = mainCache.TryGet(key) as YtdFile;
                if (ytd == null)
                {
                    var e = GetYtdEntry(hash);
                    if (e != null)
                    {
                        ytd = new YtdFile(e);
                        if (mainCache.TryAdd(key, ytd))
                        {
                            TryLoadEnqueue(ytd);
                        }
                        else
                        {
                            ytd.LoadQueued = false;
                            //ErrorLog?.Invoke("Out of cache space - couldn't load texture dictionary: " + JenkIndex.GetString(hash)); //too spammy...
                        }
                    }
                    else
                    {
                        //ErrorLog?.Invoke("Texture dictionary not found: " + JenkIndex.GetString(hash)); //too spammy...
                    }
                }
                else if (!ytd.Loaded)
                {
                    TryLoadEnqueue(ytd);
                }
                return ytd;
            }
        }
        public YmapFile? GetYmap(uint hash)
        {
            if (!IsInited) return null;
            lock (requestSyncRoot)
            {
                var key = new GameFileCacheKey(hash, GameFileType.Ymap);
                var ymap = mainCache.TryGet(key) as YmapFile;
                if (ymap == null)
                {
                    //Active map rpfs only. GetYmapEntry's AllYmapsDict fallback searches every rpf in the
                    //install, so a LOD parent/child link would resurrect a ymap the DLC changesets dropped -
                    //eg loading vanilla id2_21_c from patchday1ng, which then pulls in patchday2ng's id2_lod
                    //alongside mpheist's hei_id2_lod. ProjectForm still uses GetYmapEntry to open any ymap.
                    YmapDict.TryGetValue(hash, out var e);
                    if (e != null)
                    {
                        ymap = new YmapFile(e);
                        if (mainCache.TryAdd(key, ymap))
                        {
                            TryLoadEnqueue(ymap);
                        }
                        else
                        {
                            ymap.LoadQueued = false;
                            //ErrorLog?.Invoke("Out of cache space - couldn't load ymap: " + JenkIndex.GetString(hash));
                        }
                    }
                    else
                    {
                        //ErrorLog?.Invoke("Ymap not found: " + JenkIndex.GetString(hash)); //too spammy...
                    }
                }
                else if (!ymap.Loaded)
                {
                    TryLoadEnqueue(ymap);
                }
                return ymap;
            }
        }
        public YftFile? GetYft(uint hash)
        {
            if (!IsInited) return null;
            lock (requestSyncRoot)
            {
                var key = new GameFileCacheKey(hash, GameFileType.Yft);
                var yft = mainCache.TryGet(key) as YftFile;
                if (projectFiles.TryGetValue(key, out GameFile? pgf))
                {
                    return pgf as YftFile;
                }
                if (yft == null)
                {
                    var e = GetYftEntry(hash);
                    if (e != null)
                    {
                        yft = new YftFile(e);
                        if (mainCache.TryAdd(key, yft))
                        {
                            TryLoadEnqueue(yft);
                        }
                        else
                        {
                            yft.LoadQueued = false;
                            //ErrorLog?.Invoke("Out of cache space - couldn't load yft: " + JenkIndex.GetString(hash)); //too spammy...
                        }
                    }
                    else
                    {
                        //ErrorLog?.Invoke("Yft not found: " + JenkIndex.GetString(hash)); //too spammy...
                    }
                }
                else if (!yft.Loaded)
                {
                    TryLoadEnqueue(yft);
                }
                return yft;
            }
        }
        public YbnFile? GetYbn(uint hash)
        {
            if (!IsInited) return null;
            lock (requestSyncRoot)
            {
                var key = new GameFileCacheKey(hash, GameFileType.Ybn);
                YbnFile? ybn = mainCache.TryGet(key) as YbnFile;
                if (ybn == null)
                {
                    var e = GetYbnEntry(hash);
                    if (e != null)
                    {
                        ybn = new YbnFile(e);
                        if (mainCache.TryAdd(key, ybn))
                        {
                            TryLoadEnqueue(ybn);
                        }
                        else
                        {
                            ybn.LoadQueued = false;
                            //ErrorLog?.Invoke("Out of cache space - couldn't load ybn: " + JenkIndex.GetString(hash)); //too spammy...
                        }
                    }
                    else
                    {
                        //ErrorLog?.Invoke("Ybn not found: " + JenkIndex.GetString(hash)); //too spammy...
                    }
                }
                else if (!ybn.Loaded)
                {
                    TryLoadEnqueue(ybn);
                }
                return ybn;
            }
        }
        public YcdFile? GetYcd(uint hash)
        {
            if (!IsInited) return null;
            lock (requestSyncRoot)
            {
                var key = new GameFileCacheKey(hash, GameFileType.Ycd);
                YcdFile? ycd = mainCache.TryGet(key) as YcdFile;
                if (ycd == null)
                {
                    var e = GetYcdEntry(hash);
                    if (e != null)
                    {
                        ycd = new YcdFile(e);
                        if (mainCache.TryAdd(key, ycd))
                        {
                            TryLoadEnqueue(ycd);
                        }
                        else
                        {
                            ycd.LoadQueued = false;
                            //ErrorLog?.Invoke("Out of cache space - couldn't load ycd: " + JenkIndex.GetString(hash)); //too spammy...
                        }
                    }
                    else
                    {
                        //ErrorLog?.Invoke("Ycd not found: " + JenkIndex.GetString(hash)); //too spammy...
                    }
                }
                else if (!ycd.Loaded)
                {
                    TryLoadEnqueue(ycd);
                }
                return ycd;
            }
        }
        public YedFile? GetYed(uint hash)
        {
            if (!IsInited) return null;
            lock (requestSyncRoot)
            {
                var key = new GameFileCacheKey(hash, GameFileType.Yed);
                YedFile? yed = mainCache.TryGet(key) as YedFile;
                if (yed == null)
                {
                    var e = GetYedEntry(hash);
                    if (e != null)
                    {
                        yed = new YedFile(e);
                        if (mainCache.TryAdd(key, yed))
                        {
                            TryLoadEnqueue(yed);
                        }
                        else
                        {
                            yed.LoadQueued = false;
                            //ErrorLog?.Invoke("Out of cache space - couldn't load yed: " + JenkIndex.GetString(hash)); //too spammy...
                        }
                    }
                    else
                    {
                        //ErrorLog?.Invoke("Yed not found: " + JenkIndex.GetString(hash)); //too spammy...
                    }
                }
                else if (!yed.Loaded)
                {
                    TryLoadEnqueue(yed);
                }
                return yed;
            }
        }
        public YnvFile? GetYnv(uint hash)
        {
            if (!IsInited) return null;
            lock (requestSyncRoot)
            {
                var key = new GameFileCacheKey(hash, GameFileType.Ynv);
                YnvFile? ynv = mainCache.TryGet(key) as YnvFile;
                if (ynv == null)
                {
                    var e = GetYnvEntry(hash);
                    if (e != null)
                    {
                        ynv = new YnvFile(e);
                        if (mainCache.TryAdd(key, ynv))
                        {
                            TryLoadEnqueue(ynv);
                        }
                        else
                        {
                            ynv.LoadQueued = false;
                            //ErrorLog?.Invoke("Out of cache space - couldn't load ycd: " + JenkIndex.GetString(hash)); //too spammy...
                        }
                    }
                    else
                    {
                        //ErrorLog?.Invoke("Ycd not found: " + JenkIndex.GetString(hash)); //too spammy...
                    }
                }
                else if (!ynv.Loaded)
                {
                    TryLoadEnqueue(ynv);
                }
                return ynv;
            }
        }


        public RpfFileEntry? GetYdrEntry(uint hash)
        {
            // Use CollectionsMarshal for zero-allocation dictionary lookup
            ref var entry = ref CollectionsMarshal.GetValueRefOrNullRef(YdrDict, hash);
            return !Unsafe.IsNullRef(ref entry) ? entry : null;
        }
        public RpfFileEntry? GetYddEntry(uint hash)
        {
            ref var entry = ref CollectionsMarshal.GetValueRefOrNullRef(YddDict, hash);
            return !Unsafe.IsNullRef(ref entry) ? entry : null;
        }
        public RpfFileEntry? GetYtdEntry(uint hash)
        {
            ref var entry = ref CollectionsMarshal.GetValueRefOrNullRef(YtdDict, hash);
            return !Unsafe.IsNullRef(ref entry) ? entry : null;
        }
        public RpfFileEntry? GetYmapEntry(uint hash)
        {
            ref var entry = ref CollectionsMarshal.GetValueRefOrNullRef(YmapDict, hash);
            if (!Unsafe.IsNullRef(ref entry))
            {
                return entry;
            }
            
            ref var allEntry = ref CollectionsMarshal.GetValueRefOrNullRef(AllYmapsDict, hash);
            return !Unsafe.IsNullRef(ref allEntry) ? allEntry : null;
        }
        public RpfFileEntry? GetYftEntry(uint hash)
        {
            ref var entry = ref CollectionsMarshal.GetValueRefOrNullRef(YftDict, hash);
            return !Unsafe.IsNullRef(ref entry) ? entry : null;
        }
        public RpfFileEntry? GetYbnEntry(uint hash)
        {
            ref var entry = ref CollectionsMarshal.GetValueRefOrNullRef(YbnDict, hash);
            return !Unsafe.IsNullRef(ref entry) ? entry : null;
        }
        public RpfFileEntry? GetYcdEntry(uint hash)
        {
            ref var entry = ref CollectionsMarshal.GetValueRefOrNullRef(YcdDict, hash);
            return !Unsafe.IsNullRef(ref entry) ? entry : null;
        }
        public RpfFileEntry? GetYedEntry(uint hash)
        {
            ref var entry = ref CollectionsMarshal.GetValueRefOrNullRef(YedDict, hash);
            return !Unsafe.IsNullRef(ref entry) ? entry : null;
        }
        public RpfFileEntry? GetYnvEntry(uint hash)
        {
            ref var entry = ref CollectionsMarshal.GetValueRefOrNullRef(YnvDict, hash);
            return !Unsafe.IsNullRef(ref entry) ? entry : null;
        }



        public bool LoadFile<T>(T? file) where T : GameFile, PackedFile
        {
            if (file == null) return false;
            RpfFileEntry? entry = file.RpfFileEntry;
            if (entry != null)
            {
                return ArchiveManager.LoadFile(file, entry);
            }
            return false;
        }

        public async Task<bool> LoadFileAsync<T>(T? file, CancellationToken cancellationToken = default) where T : GameFile, PackedFile
        {
            if (file == null) return false;
            RpfFileEntry? entry = file.RpfFileEntry;
            if (entry != null)
            {
                return await ArchiveManager.LoadFileAsync(file, entry, cancellationToken).ConfigureAwait(false);
            }
            return false;
        }


        public T GetFileUncached<T>(RpfFileEntry e) where T : GameFile, new()
        {
            var f = new T();
            f.RpfFileEntry = e;
            TryLoadEnqueue(f);
            return f;
        }


        public void BeginFrame()
        {
            lock (requestSyncRoot)
            {
                mainCache.BeginFrame();
            }
        }


        private readonly List<GameFile> contentBatch = new();
        private readonly ParallelOptions contentParallelOptions = new()
        {
            MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount / 2),
        };

        public bool ContentThreadProc()
        {
            lock (updateSyncRoot)
            {
                contentBatch.Clear();
                var now = mainCache.CurrentTime;
                while ((contentBatch.Count < MaxItemsPerLoop) && requestQueue.TryDequeue(out GameFile? req))
                {
                    Interlocked.Decrement(ref requestQueueCount);
                    req.LoadQueued = false; //cleared here so a failed or skipped load can be requested again

                    if (req.Loaded)
                        continue; //it's already loaded... (somehow)

                    if ((now - req.LastUseTime).TotalSeconds > 0.5)
                        continue; //hasn't been requested lately..! ignore, will try again later if necessary

                    contentBatch.Add(req);
                }

                //the expensive part is per-file and independent (open stream, decrypt, inflate, parse), and
                //ExtractFile opens its own stream each call - so run the batch across cores instead of one at a
                //time. Bounded: the render thread needs cores too, and saturating them to stream faster is a
                //bad trade when the frame is what the user is looking at.
                if (contentBatch.Count > 1)
                {
                    Parallel.ForEach(contentBatch, contentParallelOptions, LoadContentFile);
                }
                else if (contentBatch.Count == 1)
                {
                    LoadContentFile(contentBatch[0]);
                }

                foreach (var req in contentBatch)
                {
                    //ymap archetype hookup reads the shared archetype/ytyp caches, so keep it off the parallel path
                    if (req.Loaded && (req is YmapFile y)) y.InitYmapEntityArchetypes(this);

                    UpdateStatus?.Invoke((req.Loaded ? "Loaded " : "Error loading ") + req.ToString());
                    if (!req.Loaded) ErrorLog?.Invoke("Error loading " + req.ToString());
                }
            }

            //whether or not we need another content thread loop
            return contentBatch.Count >= MaxItemsPerLoop;
        }

        private void LoadContentFile(GameFile req)
        {
#if !DEBUG
            try
            {
#endif
            switch (req.Type)
            {
                case GameFileType.Ydr:
                    req.Loaded = LoadFile(req as YdrFile);
                    break;
                case GameFileType.Ydd:
                    req.Loaded = LoadFile(req as YddFile);
                    break;
                case GameFileType.Ytd:
                    req.Loaded = LoadFile(req as YtdFile);
                    break;
                case GameFileType.Ymap:
                    req.Loaded = LoadFile(req as YmapFile);
                    break;
                case GameFileType.Yft:
                    req.Loaded = LoadFile(req as YftFile);
                    break;
                case GameFileType.Ybn:
                    req.Loaded = LoadFile(req as YbnFile);
                    break;
                case GameFileType.Ycd:
                    req.Loaded = LoadFile(req as YcdFile);
                    break;
                case GameFileType.Yed:
                    req.Loaded = LoadFile(req as YedFile);
                    break;
                case GameFileType.Ynv:
                    req.Loaded = LoadFile(req as YnvFile);
                    break;
                case GameFileType.Yld:
                    req.Loaded = LoadFile(req as YldFile);
                    break;
                default:
                    break;
            }
#if !DEBUG
            }
            catch (Exception ex)
            {
                ErrorLog?.Invoke($"Failed to load file {req.Name}: {ex.Message}");
                //TODO: try to stop subsequent attempts to load this!
            }
#endif
        }






        private void AddTextureLookups(YtdFile ytd)
        {
            if (ytd?.TextureDict?.TextureNameHashes?.data_items == null || ytd.RpfFileEntry == null) return;

            lock (textureSyncRoot)
            {
                foreach (uint hash in ytd.TextureDict.TextureNameHashes.data_items)
                {
                    textureLookup[hash] = ytd.RpfFileEntry;
                }

            }
        }
        public YtdFile? TryGetTextureDictForTexture(uint hash)
        {
            lock (textureSyncRoot)
            {
                RpfFileEntry? e;
                if (textureLookup.TryGetValue(hash, out e))
                {
                    return GetYtd(e.ShortNameHash);
                }

            }
            return null;
        }
        public YtdFile? TryGetParentYtd(uint hash)
        {
            // Use CollectionsMarshal for zero-allocation lookup
            ref var phash = ref CollectionsMarshal.GetValueRefOrNullRef(textureParents, hash);
            if (!Unsafe.IsNullRef(ref phash))
            {
                return GetYtd(phash);
            }
            return null;
        }
        public uint TryGetParentYtdHash(uint hash)
        {
            ref var phash = ref CollectionsMarshal.GetValueRefOrNullRef(textureParents, hash);
            return !Unsafe.IsNullRef(ref phash) ? phash : 0;
        }
        public uint TryGetHDTextureHash(uint txdhash)
        {
            if (hdtexturelookup == null) return txdhash;
            
            ref var hdhash = ref CollectionsMarshal.GetValueRefOrNullRef(hdtexturelookup, txdhash);
            return !Unsafe.IsNullRef(ref hdhash) ? hdhash : txdhash;
        }

        public Texture? TryFindTextureInParent(uint texhash, uint txdhash)
        {
            Texture? tex = null;

            var ytd = TryGetParentYtd(txdhash);
            while ((ytd != null) && (tex == null))
            {
                if (ytd.Loaded && (ytd.TextureDict != null))
                {
                    tex = ytd.TextureDict.Lookup(texhash);
                }
                if (tex == null)
                {
                    ytd = TryGetParentYtd(ytd.Key.Hash);
                }
            }

            return tex;
        }








        public DrawableBase? TryGetDrawable(Archetype? arche)
        {
            if (arche == null) return null;
            uint drawhash = arche.Hash;
            DrawableBase? drawable = null;
            if ((arche.DrawableDict != 0))// && (arche.DrawableDict != arche.Hash))
            {
                //try get drawable from ydd...
                YddFile? ydd = GetYdd(arche.DrawableDict);
                if (ydd != null)
                {
                    if (ydd.Loaded && (ydd.Dict != null))
                    {
                        Drawable? d;
                        ydd.Dict.TryGetValue(drawhash, out d); //can't out to base class?
                        drawable = d;
                        if (drawable == null)
                        {
                            return null; //drawable wasn't in dict!!
                        }
                    }
                    else
                    {
                        return null; //ydd not loaded yet, or has no dict
                    }
                }
                else
                {
                    //return null; //couldn't find drawable dict... quit now?
                }
            }
            if (drawable == null)
            {
                //try get drawable from ydr.
                YdrFile? ydr = GetYdr(drawhash);
                if (ydr != null)
                {
                    if (ydr.Loaded)
                    {
                        drawable = ydr.Drawable;
                    }
                }
                else
                {
                    YftFile? yft = GetYft(drawhash);
                    if (yft != null)
                    {
                        if (yft.Loaded)
                        {
                            if (yft.Fragment != null)
                            {
                                drawable = yft.Fragment.Drawable;
                            }
                        }
                    }
                }
            }

            return drawable;
        }

        public async Task<(DrawableBase? drawable, bool waitingForLoad)> TryGetDrawableAsync(Archetype? arche)
        {
            if (arche == null) return (null, false);

            uint drawhash = arche.Hash;

            // Run Ydd, Ydr, and Yft parts in parallel - each returns its own result
            var yddTask = Task.Run(() => TryGetDrawableFromYdd(arche, drawhash));
            var ydrTask = Task.Run(() => TryGetDrawableFromYdr(drawhash));
            var yftTask = Task.Run(() => TryGetDrawableFromYft(drawhash));

            // Wait for all tasks to complete
            await Task.WhenAll(yddTask, ydrTask, yftTask);

            // Check results in priority order
            var yddResult = await yddTask;
            if (yddResult != null) return (yddResult, false);

            var ydrResult = await ydrTask;
            if (ydrResult != null) return (ydrResult, false);

            var yftResult = await yftTask;
            if (yftResult != null) return (yftResult, false);

            return (null, true);
        }

        // Helper functions for Ydd, Ydr, and Yft - return results instead of using ref

        private DrawableBase? TryGetDrawableFromYdd(Archetype arche, uint drawhash)
        {
            if (arche.DrawableDict != 0)
            {
                YddFile? ydd = GetYdd(arche.DrawableDict);
                if (ydd != null && ydd.Loaded && ydd.Dict != null && ydd.Dict.TryGetValue(drawhash, out Drawable? d))
                {
                    return d;
                }
            }
            return null;
        }

        private DrawableBase? TryGetDrawableFromYdr(uint drawhash)
        {
            YdrFile? ydr = GetYdr(drawhash);
            if (ydr != null && ydr.Loaded)
            {
                return ydr.Drawable;
            }
            return null;
        }

        private DrawableBase? TryGetDrawableFromYft(uint drawhash)
        {
            YftFile? yft = GetYft(drawhash);
            if (yft != null && yft.Loaded)
            {
                return yft.Fragment?.Drawable;
            }
            return null;
        }










        private string[] GetExcludePaths()
        {
            if (string.IsNullOrEmpty(ExcludeFolders))
                return [];

            string[] exclpaths = ExcludeFolders.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (exclpaths.Length == 0)
                return [];

            // Use Array.ConvertAll for better performance than manual loop
            return Array.ConvertAll(exclpaths, path => path.ToLowerInvariant());
        }







        public void TestAudioRels()
        {
            UpdateStatus?.Invoke("Testing Audio REL files");


            bool savetest = true;
            bool xmltest = true;
            bool asmtest = true;

            foreach (RpfFile rpf in ArchiveManager.AllRpfs)
            {
                foreach (RpfEntry entry in rpf.AllEntries)
                {
                    var rfe = entry as RpfFileEntry;
                    var rbfe = rfe as RpfBinaryFileEntry;
                    if ((rfe == null) || (rbfe == null)) continue;

                    if (rfe.NameLower.EndsWith(".rel"))
                    {
                        UpdateStatus?.Invoke(entry.Path);

                        RelFile rel = new(rfe);
                        ArchiveManager.LoadFile(rel, rfe);



                        byte[] data;

                        if (savetest)
                        {

                            data = rel.Save();
                            if (data != null)
                            {
                                if (data.Length != rbfe.FileUncompressedSize)
                                { }
                                else if (data.Length != rel.RawFileData.Length)
                                { }
                                else
                                {
                                    for (int i = 0; i < data.Length; i++) //raw file test
                                        if (data[i] != rel.RawFileData[i])
                                        { break; }
                                }


                                RelFile rel2 = new();
                                rel2.Load(data, rfe);//roundtrip test

                                if (rel2.IndexCount != rel.IndexCount)
                                { }
                                if (rel2.RelDatas == null)
                                { }

                            }
                            else
                            { }

                        }

                        if (xmltest)
                        {
                            var relxml = RelXml.GetXml(rel); //XML test...
                            var rel3 = XmlRel.GetRel(relxml);
                            if (rel3 != null)
                            {
                                if (rel3.RelDatasSorted?.Length != rel.RelDatasSorted?.Length)
                                { } //check nothing went missing...


                                data = rel3.Save(); //full roundtrip!
                                if (data != null)
                                {
                                    var rel4 = new RelFile();
                                    rel4.Load(data, rfe); //insanity check

                                    if (data.Length != rbfe.FileUncompressedSize)
                                    { }
                                    else if (data.Length != rel.RawFileData.Length)
                                    { }
                                    else
                                    {
                                        for (int i = 0; i < data.Length; i++) //raw file test
                                            if (data[i] != rel.RawFileData[i])
                                            { break; }
                                    }

                                    var relxml2 = RelXml.GetXml(rel4); //full insanity
                                    if (relxml2.Length != relxml.Length)
                                    { }
                                    if (relxml2 != relxml)
                                    { }

                                }
                                else
                                { }
                            }
                            else
                            { }

                        }

                        if (asmtest && rel != null)
                        {
                            if (rel.RelType == RelDatFileType.Dat10ModularSynth)
                            {
                                foreach (var d in rel.RelDatasSorted ?? [])
                                {
                                    if (d is Dat10Synth synth)
                                    {
                                        synth.TestDisassembly();
                                    }
                                }
                            }
                        }

                    }

                }

            }



            var hashmap = RelFile.HashesMap;
            if (hashmap.Count > 0)
            { }


            var sb2 = new StringBuilder();
            foreach (var kvp in hashmap)
            {
                string itemtype = kvp.Key.ItemType.ToString();
                if (kvp.Key.FileType == RelDatFileType.Dat151)
                {
                    itemtype = ((Dat151RelType)kvp.Key.ItemType).ToString();
                }
                else if (kvp.Key.FileType == RelDatFileType.Dat54DataEntries)
                {
                    itemtype = ((Dat54SoundType)kvp.Key.ItemType).ToString();
                }
                else
                {
                    itemtype = kvp.Key.FileType.ToString() + ".Unk" + kvp.Key.ItemType.ToString();
                }
                if (kvp.Key.IsContainer)
                {
                    itemtype += " (container)";
                }

                //if (kvp.Key.FileType == RelDatFileType.Dat151)
                {
                    sb2.Append(itemtype);
                    sb2.Append("     ");
                    foreach (var val in kvp.Value)
                    {
                        sb2.Append(val.ToString());
                        sb2.Append("   ");
                    }

                    sb2.AppendLine();
                }

            }

            var hashmapstr = sb2.ToString();
            if (!string.IsNullOrEmpty(hashmapstr))
            { }

        }
        public void TestAudioYmts()
        {

            StringBuilder sb = new();

            Dictionary<uint, int> allids = new();

            foreach (RpfFile file in AllRpfs)
            {
                foreach (RpfEntry entry in file.AllEntries)
                {
                    try
                    {
                        var n = entry.NameLower;
                        if (n.EndsWith(".ymt"))
                        {
                            UpdateStatus?.Invoke(entry.Path);
                            //YmtFile? ymtfile = ArchiveManager.GetFile<YmtFile>(entry);
                            //if ((ymtfile != null))
                            //{
                            //}

                            var sn = entry.GetShortName();
                            uint un;
                            if (uint.TryParse(sn, out un))
                            {
                                if (allids.TryGetValue(un, out int count))
                                {
                                    allids[un] = count + 1;
                                }
                                else
                                {
                                    allids[un] = 1;
                                    //ushort s1 = (ushort)(un & 0x1FFFu);
                                    //ushort s2 = (ushort)((un >> 13) & 0x1FFFu);
                                    uint s1 = un % 80000;
                                    uint s2 = (un / 80000);
                                    float f1 = s1 / 5000.0f;
                                    float f2 = s2 / 5000.0f;
                                    sb.AppendFormat("{0}, {1}, 0, {2}\r\n", f1, f2, sn);
                                }
                            }


                        }
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus?.Invoke("Error! " + ex.ToString());
                    }
                }
            }

            // Loop alternative: create list directly from keys and sort
            var skeys = new List<uint>(allids.Keys);
            skeys.Sort();

            List<string> hkeys = [];
            foreach (var skey in skeys)
            {
                FlagsUint fu = new(skey);
                //hkeys.Add(skey.ToString("X"));
                hkeys.Add(fu.Bin);
            }

            string nstr = string.Join("\r\n", hkeys);
            string pstr = sb.ToString();
            if (pstr.Length > 0)
            { }


        }
        public void TestAudioAwcs()
        {

            StringBuilder sb = new();

            Dictionary<uint, int> allids = new();

            foreach (RpfFile file in AllRpfs)
            {
                foreach (RpfEntry entry in file.AllEntries)
                {
                    //try
                    //{
                    var n = entry.NameLower;
                    if (n.EndsWith(".awc"))
                    {
                        UpdateStatus?.Invoke(entry.Path);
                        var awcfile = ArchiveManager.GetFile<AwcFile>(entry);
                        if (awcfile != null)
                        { }
                    }
                    //}
                    //catch (Exception ex)
                    //{
                    //    UpdateStatus?.Invoke("Error! " + ex.ToString());
                    //}
                }
            }
        }
        public void TestMetas()
        {
            //find all RSC meta files and generate the MetaTypes init code

            MetaTypes.Clear();
            foreach (RpfFile file in AllRpfs)
            {
                foreach (RpfEntry entry in file.AllEntries)
                {
                    //try
                    {
                        var n = entry.NameLower;
                        //if (n.EndsWith(".ymap"))
                        //{
                        //    UpdateStatus?.Invoke(entry.Path);
                        //    YmapFile? ymapfile = ArchiveManager.GetFile<YmapFile>(entry);
                        //    if ((ymapfile != null) && (ymapfile.Meta != null))
                        //    {
                        //        MetaTypes.EnsureMetaTypes(ymapfile.Meta);
                        //    }
                        //}
                        //else if (n.EndsWith(".ytyp"))
                        //{
                        //    UpdateStatus?.Invoke(entry.Path);
                        //    YtypFile? ytypfile = ArchiveManager.GetFile<YtypFile>(entry);
                        //    if ((ytypfile != null) && (ytypfile.Meta != null))
                        //    {
                        //        MetaTypes.EnsureMetaTypes(ytypfile.Meta);
                        //    }
                        //}
                        //else if (n.EndsWith(".ymt"))
                        //{
                        //    UpdateStatus?.Invoke(entry.Path);
                        //    YmtFile? ymtfile = ArchiveManager.GetFile<YmtFile>(entry);
                        //    if ((ymtfile != null) && (ymtfile.Meta != null))
                        //    {
                        //        MetaTypes.EnsureMetaTypes(ymtfile.Meta);
                        //    }
                        //}


                        if (n.EndsWith(".ymap") || n.EndsWith(".ytyp") || n.EndsWith(".ymt"))
                        {
                            var rfe = entry as RpfResourceFileEntry;
                            if (rfe == null) continue;

                            UpdateStatus?.Invoke(entry.Path);

                            var data = rfe.File?.ExtractFile(rfe);
                            if (data == null) continue;
                            ResourceDataReader rd = new(rfe, data);
                            var meta = rd.ReadRequiredBlock<Meta>();
                            var xml = MetaXml.GetXml(meta);
                            var xdoc = new XmlDocument();
                            xdoc.LoadXml(xml);
                            var meta2 = XmlMeta.GetMeta(xdoc);
                            var xml2 = MetaXml.GetXml(meta2);

                            if (xml.Length != xml2.Length)
                            { }
                            if ((xml != xml2) && (!n.EndsWith("srl.ymt") && !n.StartsWith("des_")))
                            { }

                        }


                    }
                    //catch (Exception ex)
                    //{
                    //    UpdateStatus?.Invoke("Error! " + ex.ToString());
                    //}
                }
            }

            string str = MetaTypes.GetTypesInitString();

        }
        public void TestPsos()
        {
            //find all PSO meta files and generate the PsoTypes init code
            PsoTypes.Clear();

            var exceptions = new List<Exception>();
            var allpsos = new List<string>();
            var diffpsos = new List<string>();

            foreach (RpfFile file in AllRpfs)
            {
                foreach (RpfEntry entry in file.AllEntries)
                {
                    //try
                    {
                        var n = entry.NameLower;
                        if (!(n.EndsWith(".pso") ||
                              n.EndsWith(".ymt") ||
                              n.EndsWith(".ymf") ||
                              n.EndsWith(".ymap") ||
                              n.EndsWith(".ytyp") ||
                              n.EndsWith(".cut")))
                            continue; //PSO files seem to only have these extensions

                        if (entry is not RpfFileEntry fentry) continue;
                        var data = entry.File?.ExtractFile(fentry);
                        if (data != null)
                        {
                            using (MemoryStream ms = new(data))
                            {
                                if (PsoFile.IsPSO(ms))
                                {
                                    UpdateStatus?.Invoke(entry.Path);

                                    var pso = new PsoFile();
                                    pso.Load(ms);

                                    allpsos.Add(fentry.Path);

                                    PsoTypes.EnsurePsoTypes(pso);

                                    var xml = PsoXml.GetXml(pso);
                                    if (!string.IsNullOrEmpty(xml))
                                    { }

                                    var xdoc = new XmlDocument();
                                    xdoc.LoadXml(xml);
                                    var pso2 = XmlPso.GetPso(xdoc);
                                    var pso2b = pso2.Save();

                                    var pso3 = new PsoFile();
                                    pso3.Load(pso2b);
                                    var xml3 = PsoXml.GetXml(pso3);

                                    if (xml.Length != xml3.Length)
                                    { }
                                    if (xml != xml3)
                                    {
                                        diffpsos.Add(fentry.Path);
                                    }


                                    //if (entry.NameLower == "clip_sets.ymt")
                                    //{ }
                                    //if (entry.NameLower == "vfxinteriorinfo.ymt")
                                    //{ }
                                    //if (entry.NameLower == "vfxvehicleinfo.ymt")
                                    //{ }
                                    //if (entry.NameLower == "vfxpedinfo.ymt")
                                    //{ }
                                    //if (entry.NameLower == "vfxregioninfo.ymt")
                                    //{ }
                                    //if (entry.NameLower == "vfxweaponinfo.ymt")
                                    //{ }
                                    //if (entry.NameLower == "physicstasks.ymt")
                                    //{ }

                                }
                            }
                        }
                    }
                    //catch (Exception ex)
                    //{
                    //    UpdateStatus?.Invoke("Error! " + ex.ToString());
                    //    exceptions.Add(ex);
                    //}
                }
            }

            string allpsopaths = string.Join("\r\n", allpsos);
            string diffpsopaths = string.Join("\r\n", diffpsos);

            string str = PsoTypes.GetTypesInitString();
            if (!string.IsNullOrEmpty(str))
            {
            }
        }
        public void TestRbfs()
        {
            var exceptions = new List<Exception>();
            var allrbfs = new List<string>();
            var diffrbfs = new List<string>();

            foreach (RpfFile file in AllRpfs)
            {
                foreach (RpfEntry entry in file.AllEntries)
                {
                    var n = entry.NameLower;
                    if (!(n.EndsWith(".ymt") ||
                          n.EndsWith(".ymf") ||
                          n.EndsWith(".ymap") ||
                          n.EndsWith(".ytyp") ||
                          n.EndsWith(".cut")))
                        continue; //PSO files seem to only have these extensions

                    if (entry is not RpfFileEntry fentry) continue;
                    var data = entry.File?.ExtractFile(fentry);
                    if (data != null)
                    {
                        using (MemoryStream ms = new(data))
                        {
                            if (RbfFile.IsRBF(ms))
                            {
                                UpdateStatus?.Invoke(entry.Path);

                                var rbf = new RbfFile();
                                rbf.Load(ms);

                                allrbfs.Add(fentry.Path);

                                var xml = RbfXml.GetXml(rbf);
                                if (!string.IsNullOrEmpty(xml))
                                { }

                                var xdoc = new XmlDocument();
                                xdoc.LoadXml(xml);
                                var rbf2 = XmlRbf.GetRbf(xdoc);
                                var rbf2b = rbf2.Save();

                                var rbf3 = new RbfFile();
                                rbf3.Load(rbf2b);
                                var xml3 = RbfXml.GetXml(rbf3);

                                if (xml.Length != xml3.Length)
                                { }
                                if (xml != xml3)
                                {
                                    diffrbfs.Add(fentry.Path);
                                }

                                if (data.Length != rbf2b.Length)
                                {
                                    //File.WriteAllBytes("C:\\GitHub\\CodeWalkerResearch\\RBF\\" + fentry.Name + ".dat0", data);
                                    //File.WriteAllBytes("C:\\GitHub\\CodeWalkerResearch\\RBF\\" + fentry.Name + ".dat1", rbf2b);
                                }
                                else
                                {
                                    for (int i = 0; i < data.Length; i++)
                                    {
                                        if (data[i] != rbf2b[i])
                                        {
                                            diffrbfs.Add(fentry.Path);
                                            break;
                                        }
                                    }
                                }

                            }
                        }
                    }

                }
            }

            string allrbfpaths = string.Join("\r\n", allrbfs);
            string diffrbfpaths = string.Join("\r\n", diffrbfs);

        }
        public void TestCuts()
        {

            var exceptions = new List<Exception>();

            foreach (RpfFile file in AllRpfs)
            {
                foreach (RpfEntry entry in file.AllEntries)
                {
                    //try
                    {
                        var rfe = entry as RpfFileEntry;
                        if (rfe == null) continue;

                        if (rfe.NameLower.EndsWith(".cut"))
                        {
                            UpdateStatus?.Invoke(entry.Path);

                            CutFile cut = new(rfe);
                            ArchiveManager.LoadFile(cut, rfe);

                            //PsoTypes.EnsurePsoTypes(cut.Pso);
                        }
                    }
                    //catch (Exception ex)
                    //{
                    //    UpdateStatus?.Invoke("Error! " + ex.ToString());
                    //    exceptions.Add(ex);
                    //}
                }
            }

            string str = PsoTypes.GetTypesInitString();
            if (!string.IsNullOrEmpty(str))
            {
            }
        }
        public void TestYlds()
        {

            var exceptions = new List<Exception>();

            foreach (RpfFile file in AllRpfs)
            {
                foreach (RpfEntry entry in file.AllEntries)
                {
                    //try
                    {
                        var rfe = entry as RpfFileEntry;
                        if (rfe == null) continue;

                        if (rfe.NameLower.EndsWith(".yld"))
                        {
                            UpdateStatus?.Invoke(entry.Path);

                            YldFile yld = new(rfe);
                            ArchiveManager.LoadFile(yld, rfe);

                        }
                    }
                    //catch (Exception ex)
                    //{
                    //    UpdateStatus?.Invoke("Error! " + ex.ToString());
                    //    exceptions.Add(ex);
                    //}
                }
            }

            if (exceptions.Count > 0)
            { }
        }
        public void TestYeds()
        {
            bool xmltest = true;
            var exceptions = new List<Exception>();

            foreach (RpfFile file in AllRpfs)
            {
                foreach (RpfEntry entry in file.AllEntries)
                {
                    //try
                    {
                        var rfe = entry as RpfFileEntry;
                        if (rfe == null) continue;

                        if (rfe.NameLower.EndsWith(".yed"))
                        {
                            UpdateStatus?.Invoke(entry.Path);

                            YedFile yed = new(rfe);
                            ArchiveManager.LoadFile(yed, rfe);

                            if (xmltest)
                            {
                                var xml = YedXml.GetXml(yed);
                                var yed2 = XmlYed.GetYed(xml);
                                var data2 = yed2.Save();
                                var yed3 = new YedFile();
                                RpfFile.LoadResourceFile(yed3, data2, 25);//full roundtrip
                                var xml2 = YedXml.GetXml(yed3);
                                if (xml != xml2)
                                { }//no hitting
                            }

                        }
                    }
                    //catch (Exception ex)
                    //{
                    //    UpdateStatus?.Invoke("Error! " + ex.ToString());
                    //    exceptions.Add(ex);
                    //}
                }
            }

            if (exceptions.Count > 0)
            { }
        }
        public void TestYcds()
        {
            bool savetest = false;
            var errorfiles = new List<YcdFile>();
            var errorentries = new List<RpfEntry>();
            foreach (RpfFile file in AllRpfs)
            {
                foreach (RpfEntry entry in file.AllEntries)
                {
                    //try
                    //{
                    if (entry.NameLower.EndsWith(".ycd"))
                    {
                        UpdateStatus?.Invoke(entry.Path);
                        YcdFile? ycd1 = ArchiveManager.GetFile<YcdFile>(entry);
                        if (ycd1 == null)
                        {
                            errorentries.Add(entry);
                        }
                        else if (ycd1?.LoadException != null)
                        {
                            errorfiles.Add(ycd1);//these ones have file corruption issues and won't load as resource...
                        }
                        else if (savetest)
                        {
                            if (ycd1?.ClipDictionary == null)
                            { continue; }

                            //var data1 = ycd1.Save();

                            var xml = YcdXml.GetXml(ycd1);
                            var ycdX = XmlYcd.GetYcd(xml);
                            var data = ycdX.Save();
                            var ycd2 = new YcdFile();
                            RpfFile.LoadResourceFile(ycd2, data, 46);//full roundtrip

                            {
                                if (ycd2 == null)
                                { continue; }
                                if (ycd2.ClipDictionary == null)
                                { continue; }

                                var c1 = ycd1.ClipDictionary.Clips?.data_items;
                                var c2 = ycd2.ClipDictionary.Clips?.data_items;
                                if ((c1 == null) || (c2 == null))
                                { continue; }
                                if (c1.Length != c2.Length)
                                { continue; }

                                var a1 = ycd1.ClipDictionary.Animations?.Animations?.data_items;
                                var a2 = ycd2.ClipDictionary.Animations?.Animations?.data_items;
                                if ((a1 == null) || (a2 == null))
                                { continue; }
                                if (a1.Length != a2.Length)
                                { continue; }

                                var m1 = ycd1.AnimMap;
                                var m2 = ycd2.AnimMap;
                                if ((m1 == null) || (m2 == null))
                                { continue; }
                                if (m1.Count != m2.Count)
                                { continue; }
                                foreach (var kvp1 in m1)
                                {
                                    var an1 = kvp1.Value;
                                    var an2 = an1;
                                    if (!m2.TryGetValue(kvp1.Key, out an2))
                                    { continue; }

                                    var sa1 = an1?.Animation?.Sequences?.data_items;
                                    var sa2 = an2?.Animation?.Sequences?.data_items;
                                    if ((sa1 == null) || (sa2 == null))
                                    { continue; }
                                    if (sa1.Length != sa2.Length)
                                    { continue; }
                                    for (int s = 0; s < sa1.Length; s++)
                                    {
                                        var s1 = sa1[s];
                                        var s2 = sa2[s];
                                        if ((s1?.Sequences == null) || (s2?.Sequences == null))
                                        { continue; }

                                        if (s1.NumFrames != s2.NumFrames)
                                        { }
                                        if (s1.ChunkSize != s2.ChunkSize)
                                        { }
                                        if (s1.FrameOffset != s2.FrameOffset)
                                        { }
                                        if (s1.DataLength != s2.DataLength)
                                        { }
                                        else
                                        {
                                            //for (int b = 0; b < s1.DataLength; b++)
                                            //{
                                            //    var b1 = s1.Data[b];
                                            //    var b2 = s2.Data[b];
                                            //    if (b1 != b2)
                                            //    { }
                                            //}
                                        }

                                        for (int ss = 0; ss < s1.Sequences.Length; ss++)
                                        {
                                            var ss1 = s1.Sequences[ss];
                                            var ss2 = s2.Sequences[ss];
                                            if ((ss1?.Channels == null) || (ss2?.Channels == null))
                                            { continue; }
                                            if (ss1.Channels.Length != ss2.Channels.Length)
                                            { continue; }


                                            for (int c = 0; c < ss1.Channels.Length; c++)
                                            {
                                                var sc1 = ss1.Channels[c];
                                                var sc2 = ss2.Channels[c];
                                                if ((sc1 == null) || (sc2 == null))
                                                { continue; }
                                                if (sc1.Type == AnimChannelType.LinearFloat)
                                                { continue; }
                                                if (sc1.Type != sc2.Type)
                                                { continue; }
                                                if (sc1.Index != sc2.Index)
                                                { continue; }
                                                if (sc1.Type == AnimChannelType.StaticQuaternion)
                                                {
                                                    var acsq1 = sc1 as AnimChannelStaticQuaternion;
                                                    var acsq2 = sc2 as AnimChannelStaticQuaternion;
                                                    if (acsq1 == null || acsq2 == null) continue;
                                                    var vdiff = acsq1.Value - acsq2.Value;
                                                    var len = vdiff.Length();
                                                    var v1len = Math.Max(acsq1.Value.Length(), 1);
                                                    if (len > 1e-2f * v1len)
                                                    { continue; }
                                                }
                                                else if (sc1.Type == AnimChannelType.StaticVector3)
                                                {
                                                    var acsv1 = sc1 as AnimChannelStaticVector3;
                                                    var acsv2 = sc2 as AnimChannelStaticVector3;
                                                    if (acsv1 == null || acsv2 == null) continue;
                                                    var vdiff = acsv1.Value - acsv2.Value;
                                                    var len = vdiff.Length();
                                                    var v1len = Math.Max(acsv1.Value.Length(), 1);
                                                    if (len > 1e-2f * v1len)
                                                    { continue; }
                                                }
                                                else if (sc1.Type == AnimChannelType.StaticFloat)
                                                {
                                                    var acsf1 = sc1 as AnimChannelStaticFloat;
                                                    var acsf2 = sc2 as AnimChannelStaticFloat;
                                                    if (acsf1 == null || acsf2 == null) continue;
                                                    var vdiff = Math.Abs(acsf1.Value - acsf2.Value);
                                                    var v1len = Math.Max(Math.Abs(acsf1.Value), 1);
                                                    if (vdiff > 1e-2f * v1len)
                                                    { continue; }
                                                }
                                                else if (sc1.Type == AnimChannelType.RawFloat)
                                                {
                                                    var acrf1 = sc1 as AnimChannelRawFloat;
                                                    var acrf2 = sc2 as AnimChannelRawFloat;
                                                    if (acrf1 == null || acrf2 == null) continue;
                                                    for (int v = 0; v < acrf1.Values.Length; v++)
                                                    {
                                                        var v1 = acrf1.Values[v];
                                                        var v2 = acrf2.Values[v];
                                                        var vdiff = Math.Abs(v1 - v2);
                                                        var v1len = Math.Max(Math.Abs(v1), 1);
                                                        if (vdiff > 1e-2f * v1len)
                                                        { break; }
                                                    }
                                                }
                                                else if (sc1.Type == AnimChannelType.QuantizeFloat)
                                                {
                                                    var acqf1 = sc1 as AnimChannelQuantizeFloat;
                                                    var acqf2 = sc2 as AnimChannelQuantizeFloat;
                                                    if (acqf1 == null || acqf2 == null) continue;
                                                    if (acqf1.ValueBits != acqf2.ValueBits)
                                                    { continue; }
                                                    if (Math.Abs(acqf1.Offset - acqf2.Offset) > (0.001f * Math.Abs(acqf1.Offset)))
                                                    { continue; }
                                                    if (Math.Abs(acqf1.Quantum - acqf2.Quantum) > 0.00001f)
                                                    { continue; }
                                                    for (int v = 0; v < acqf1.Values.Length; v++)
                                                    {
                                                        var v1 = acqf1.Values[v];
                                                        var v2 = acqf2.Values[v];
                                                        var vdiff = Math.Abs(v1 - v2);
                                                        var v1len = Math.Max(Math.Abs(v1), 1);
                                                        if (vdiff > 1e-2f * v1len)
                                                        { break; }
                                                    }
                                                }
                                                else if (sc1.Type == AnimChannelType.IndirectQuantizeFloat)
                                                {
                                                    var aciqf1 = sc1 as AnimChannelIndirectQuantizeFloat;
                                                    var aciqf2 = sc2 as AnimChannelIndirectQuantizeFloat;
                                                    if (aciqf1 == null || aciqf2 == null) continue;
                                                    if (aciqf1.FrameBits != aciqf2.FrameBits)
                                                    { continue; }
                                                    if (aciqf1.ValueBits != aciqf2.ValueBits)
                                                    { continue; }
                                                    if (Math.Abs(aciqf1.Offset - aciqf2.Offset) > (0.001f * Math.Abs(aciqf1.Offset)))
                                                    { continue; }
                                                    if (Math.Abs(aciqf1.Quantum - aciqf2.Quantum) > 0.00001f)
                                                    { continue; }
                                                    for (int f = 0; f < aciqf1.Frames.Length; f++)
                                                    {
                                                        if (aciqf1.Frames[f] != aciqf2.Frames[f])
                                                        { break; }
                                                    }
                                                    for (int v = 0; v < aciqf1.Values.Length; v++)
                                                    {
                                                        var v1 = aciqf1.Values[v];
                                                        var v2 = aciqf2.Values[v];
                                                        var vdiff = Math.Abs(v1 - v2);
                                                        var v1len = Math.Max(Math.Abs(v1), 1);
                                                        if (vdiff > 1e-2f * v1len)
                                                        { break; }
                                                    }
                                                }
                                                else if ((sc1.Type == AnimChannelType.CachedQuaternion1) || (sc1.Type == AnimChannelType.CachedQuaternion2))
                                                {
                                                    var acrf1 = sc1 as AnimChannelCachedQuaternion;
                                                    var acrf2 = sc2 as AnimChannelCachedQuaternion;
                                                    if (acrf1 == null || acrf2 == null) continue;
                                                    if (acrf1.QuatIndex != acrf2.QuatIndex)
                                                    { continue; }
                                                }




                                            }


                                            //for (int f = 0; f < s1.NumFrames; f++)
                                            //{
                                            //    var v1 = ss1.EvaluateVector(f);
                                            //    var v2 = ss2.EvaluateVector(f);
                                            //    var vdiff = v1 - v2;
                                            //    var len = vdiff.Length();
                                            //    var v1len = Math.Max(v1.Length(), 1);
                                            //    if (len > 1e-2f*v1len)
                                            //    { }
                                            //}
                                        }


                                    }


                                }


                            }

                        }
                    }
                    //if (entry.NameLower.EndsWith(".awc")) //awcs can also contain clip dicts..
                    //{
                    //    UpdateStatus?.Invoke(entry.Path);
                    //    AwcFile? awcfile = ArchiveManager.GetFile<AwcFile>(entry);
                    //    if ((awcfile != null))
                    //    { }
                    //}
                    //}
                    //catch (Exception ex)
                    //{
                    //    UpdateStatus?.Invoke("Error! " + ex.ToString());
                    //}
                }
            }

            if (errorfiles.Count > 0)
            { }

        }
        public void TestYtds()
        {
            bool ddstest = false;
            bool savetest = false;
            var errorfiles = new List<RpfEntry>();
            foreach (RpfFile file in AllRpfs)
            {
                foreach (RpfEntry entry in file.AllEntries)
                {
                    //try
                    {
                        if (entry.NameLower.EndsWith(".ytd"))
                        {
                            UpdateStatus?.Invoke(entry.Path);
                            YtdFile? ytdfile = null;
                            try
                            {
                                ytdfile = ArchiveManager.GetFile<YtdFile>(entry);
                            }
                            catch (Exception ex)
                            {
                                UpdateStatus?.Invoke("Error! " + ex.ToString());
                                errorfiles.Add(entry);
                            }
                            if (ddstest && (ytdfile != null) && (ytdfile.TextureDict != null))
                            {
                                foreach (var tex in ytdfile.TextureDict.Textures.data_items)
                                {
                                    var dds = Utils.DDSIO.GetDDSFile(tex);
                                    var tex2 = Utils.DDSIO.GetTexture(dds) ?? throw new InvalidDataException("DDS roundtrip produced an invalid header.");
                                    if (!tex.Name.StartsWith("script_rt"))
                                    {
                                        if (tex.Data?.FullData?.Length != tex2.Data?.FullData?.Length)
                                        { }
                                        if (tex.Stride != tex2.Stride)
                                        { }
                                    }
                                    if ((tex.Format != tex2.Format) || (tex.Width != tex2.Width) || (tex.Height != tex2.Height) || (tex.Depth != tex2.Depth) || (tex.Levels != tex2.Levels))
                                    { }
                                }
                            }
                            if (savetest && (ytdfile != null) && (ytdfile.TextureDict != null))
                            {
                                if (entry is not RpfFileEntry fentry) continue;
                                if (fentry == null)
                                { continue; } //shouldn't happen

                                var bytes = ytdfile.Save();

                                string origlen = TextUtil.GetBytesReadable(fentry.FileSize);
                                string bytelen = TextUtil.GetBytesReadable(bytes.Length);

                                if (ytdfile.TextureDict.Textures?.Count == 0)
                                { }


                                var ytd2 = new YtdFile();
                                //ytd2.Load(bytes, fentry);
                                RpfFile.LoadResourceFile(ytd2, bytes, 13);

                                if (ytd2.TextureDict == null)
                                { continue; }
                                if (ytd2.TextureDict.Textures?.Count != ytdfile.TextureDict.Textures?.Count)
                                { continue; }

                                if (ytdfile?.TextureDict?.Textures == null || ytd2?.TextureDict?.Textures == null) continue;
                                for (int i = 0; i < Math.Min(ytdfile.TextureDict.Textures.Count, ytd2.TextureDict.Textures.Count); i++)
                                {
                                    var tx1 = ytdfile.TextureDict.Textures[i];
                                    var tx2 = ytd2.TextureDict.Textures[i];
                                    var td1 = tx1.Data;
                                    var td2 = tx2.Data;
                                    if (td1 == null || td2 == null) continue;
                                    if (td1.FullData.Length != td2.FullData.Length)
                                    { continue; }

                                    for (int j = 0; j < td1.FullData.Length; j++)
                                    {
                                        if (td1.FullData[j] != td2.FullData[j])
                                        { break; }
                                    }

                                }

                            }
                        }
                    }
                    //catch (Exception ex)
                    //{
                    //    UpdateStatus?.Invoke("Error! " + ex.ToString());
                    //}
                }
            }
            if (errorfiles.Count > 0)
            { }
        }
        public void TestYbns()
        {
            bool xmltest = false;
            bool savetest = false;
            bool reloadtest = false;
            var errorfiles = new List<RpfEntry>();
            foreach (RpfFile file in AllRpfs)
            {
                foreach (RpfEntry entry in file.AllEntries)
                {
                    //try
                    {
                        if (entry.NameLower.EndsWith(".ybn"))
                        {
                            UpdateStatus?.Invoke(entry.Path);
                            YbnFile? ybn = null;
                            try
                            {
                                ybn = ArchiveManager.GetFile<YbnFile>(entry);
                            }
                            catch (Exception ex)
                            {
                                UpdateStatus?.Invoke("Error! " + ex.ToString());
                                errorfiles.Add(entry);
                            }
                            if (xmltest && (ybn != null) && (ybn.Bounds != null))
                            {
                                var xml = YbnXml.GetXml(ybn);
                                var ybn2 = XmlYbn.GetYbn(xml);
                                var xml2 = YbnXml.GetXml(ybn2);
                                if (xml.Length != xml2.Length)
                                { }
                            }
                            if (savetest && (ybn != null) && (ybn.Bounds != null))
                            {
                                if (entry is not RpfFileEntry fentry) continue;
                                if (fentry == null)
                                { continue; } //shouldn't happen

                                var bytes = ybn.Save();

                                if (!reloadtest)
                                { continue; }

                                string origlen = TextUtil.GetBytesReadable(fentry.FileSize);
                                string bytelen = TextUtil.GetBytesReadable(bytes.Length);


                                var ybn2 = new YbnFile();
                                RpfFile.LoadResourceFile(ybn2, bytes, 43);

                                if (ybn2.Bounds == null)
                                { continue; }
                                if (ybn2.Bounds.Type != ybn.Bounds.Type)
                                { continue; }

                                //quick check of roundtrip
                                switch (ybn2.Bounds.Type)
                                {
                                    case BoundsType.Sphere:
                                        {
                                            var a = ybn.Bounds as BoundSphere;
                                            var b = ybn2.Bounds as BoundSphere;
                                            if (a == null || b == null) continue;
                                            if (b == null)
                                            { continue; }
                                            break;
                                        }
                                    case BoundsType.Capsule:
                                        {
                                            var a = ybn.Bounds as BoundCapsule;
                                            var b = ybn2.Bounds as BoundCapsule;
                                            if (a == null || b == null) continue;
                                            if (b == null)
                                            { continue; }
                                            break;
                                        }
                                    case BoundsType.Box:
                                        {
                                            var a = ybn.Bounds as BoundBox;
                                            var b = ybn2.Bounds as BoundBox;
                                            if (a == null || b == null) continue;
                                            if (b == null)
                                            { continue; }
                                            break;
                                        }
                                    case BoundsType.Geometry:
                                        {
                                            var a = ybn.Bounds as BoundGeometry;
                                            var b = ybn2.Bounds as BoundGeometry;
                                            if (a == null || b == null) continue;
                                            if (b == null)
                                            { continue; }
                                            if (a.Polygons == null || b.Polygons == null || a.Polygons.Length != b.Polygons.Length)
                                            { continue; }
                                            for (int i = 0; i < a.Polygons.Length; i++)
                                            {
                                                var pa = a.Polygons[i];
                                                var pb = b.Polygons[i];
                                                if (pa.Type != pb.Type)
                                                { }
                                            }
                                            break;
                                        }
                                    case BoundsType.GeometryBVH:
                                        {
                                            var a = ybn.Bounds as BoundBVH;
                                            var b = ybn2.Bounds as BoundBVH;
                                            if (a == null || b == null) continue;
                                            if (b == null)
                                            { continue; }
                                            if (a.BVH?.Nodes?.data_items?.Length != b.BVH?.Nodes?.data_items?.Length)
                                            { }
                                            if (a.Polygons == null || b.Polygons == null || a.Polygons.Length != b.Polygons.Length)
                                            { continue; }
                                            for (int i = 0; i < a.Polygons.Length; i++)
                                            {
                                                var pa = a.Polygons[i];
                                                var pb = b.Polygons[i];
                                                if (pa.Type != pb.Type)
                                                { }
                                            }
                                            break;
                                        }
                                    case BoundsType.Composite:
                                        {
                                            var a = ybn.Bounds as BoundComposite;
                                            var b = ybn2.Bounds as BoundComposite;
                                            if (a == null || b == null) continue;
                                            if (b == null)
                                            { continue; }
                                            if (a.Children?.data_items?.Length != b.Children?.data_items?.Length)
                                            { }
                                            break;
                                        }
                                    case BoundsType.Disc:
                                        {
                                            var a = ybn.Bounds as BoundDisc;
                                            var b = ybn2.Bounds as BoundDisc;
                                            if (a == null || b == null) continue;
                                            if (b == null)
                                            { continue; }
                                            break;
                                        }
                                    case BoundsType.Cylinder:
                                        {
                                            var a = ybn.Bounds as BoundCylinder;
                                            var b = ybn2.Bounds as BoundCylinder;
                                            if (a == null || b == null) continue;
                                            if (b == null)
                                            { continue; }
                                            break;
                                        }
                                    case BoundsType.Cloth:
                                        {
                                            var a = ybn.Bounds as BoundCloth;
                                            var b = ybn2.Bounds as BoundCloth;
                                            if (a == null || b == null) continue;
                                            if (b == null)
                                            { continue; }
                                            break;
                                        }
                                    default: //return null; // throw new Exception("Unknown bound type");
                                        break;
                                }



                            }
                        }
                    }
                    //catch (Exception ex)
                    //{
                    //    UpdateStatus?.Invoke("Error! " + ex.ToString());
                    //}
                }
            }
            if (errorfiles.Count > 0)
            { }
        }
        public void TestYdrs()
        {
            bool savetest = true;
            bool boundsonly = false;
            var errorfiles = new List<RpfEntry>();
            foreach (RpfFile file in AllRpfs)
            {
                foreach (RpfEntry entry in file.AllEntries)
                {
                    //try
                    {
                        if (entry.NameLower.EndsWith(".ydr"))
                        {
                            UpdateStatus?.Invoke(entry.Path);
                            YdrFile? ydr = null;
                            try
                            {
                                ydr = ArchiveManager.GetFile<YdrFile>(entry);
                            }
                            catch (Exception ex)
                            {
                                UpdateStatus?.Invoke("Error! " + ex.ToString());
                                errorfiles.Add(entry);
                            }
                            if (savetest && (ydr != null) && (ydr.Drawable != null))
                            {
                                if (entry is not RpfFileEntry fentry) continue;
                                if (fentry == null)
                                { continue; } //shouldn't happen

                                if (boundsonly && (ydr.Drawable.Bound == null))
                                { continue; }

                                var bytes = ydr.Save();

                                string origlen = TextUtil.GetBytesReadable(fentry.FileSize);
                                string bytelen = TextUtil.GetBytesReadable(bytes.Length);

                                var ydr2 = new YdrFile();
                                RpfFile.LoadResourceFile(ydr2, bytes, (uint)ydr.GetVersion(RpfManager.IsGen9));

                                if (ydr2.Drawable == null)
                                { continue; }
                                if (ydr2.Drawable.AllModels?.Length != ydr.Drawable.AllModels?.Length)
                                { continue; }

                            }
                        }
                    }
                    //catch (Exception ex)
                    //{
                    //    UpdateStatus?.Invoke("Error! " + ex.ToString());
                    //}
                }
            }
            if (errorfiles.Count != 13)
            { }
        }
        public void TestYdds()
        {
            bool savetest = false;
            var errorfiles = new List<RpfEntry>();
            foreach (RpfFile file in AllRpfs)
            {
                foreach (RpfEntry entry in file.AllEntries)
                {
                    //try
                    {
                        if (entry.NameLower.EndsWith(".ydd"))
                        {
                            UpdateStatus?.Invoke(entry.Path);
                            YddFile? ydd = null;
                            try
                            {
                                ydd = ArchiveManager.GetFile<YddFile>(entry);
                            }
                            catch (Exception ex)
                            {
                                UpdateStatus?.Invoke("Error! " + ex.ToString());
                                errorfiles.Add(entry);
                            }
                            if (savetest && (ydd != null) && (ydd.DrawableDict != null))
                            {
                                if (entry is not RpfFileEntry fentry) continue;
                                if (fentry == null)
                                { continue; } //shouldn't happen

                                var bytes = ydd.Save();

                                string origlen = TextUtil.GetBytesReadable(fentry.FileSize);
                                string bytelen = TextUtil.GetBytesReadable(bytes.Length);


                                var ydd2 = new YddFile();
                                RpfFile.LoadResourceFile(ydd2, bytes, 165);

                                if (ydd2.DrawableDict == null)
                                { continue; }
                                if (ydd2.DrawableDict.Drawables?.Count != ydd.DrawableDict.Drawables?.Count)
                                { continue; }

                            }
                            if (ydd?.DrawableDict?.Hashes != null)
                            {
                                uint h = 0;
                                foreach (uint th in ydd.DrawableDict.Hashes)
                                {
                                    if (th <= h)
                                    { } //should never happen
                                    h = th;
                                }
                            }
                        }
                    }
                    //catch (Exception ex)
                    //{
                    //    UpdateStatus?.Invoke("Error! " + ex.ToString());
                    //}
                }
            }
            if (errorfiles.Count > 0)
            { }
        }
        public void TestYfts()
        {
            bool xmltest = false;
            bool savetest = false;
            bool glasstest = false;
            var errorfiles = new List<RpfEntry>();
            var sb = new StringBuilder();
            var flagdict = new Dictionary<uint, int>();
            foreach (RpfFile file in AllRpfs)
            {
                foreach (RpfEntry entry in file.AllEntries)
                {
                    //try
                    {
                        if (entry.NameLower.EndsWith(".yft"))
                        {
                            UpdateStatus?.Invoke(entry.Path);
                            YftFile? yft = null;
                            try
                            {
                                yft = ArchiveManager.GetFile<YftFile>(entry);
                            }
                            catch (Exception ex)
                            {
                                UpdateStatus?.Invoke("Error! " + ex.ToString());
                                errorfiles.Add(entry);
                            }
                            if (xmltest && (yft != null) && (yft.Fragment != null))
                            {
                                var xml = YftXml.GetXml(yft);
                                var yft2 = XmlYft.GetYft(xml);//can't do full roundtrip here due to embedded textures
                                var xml2 = YftXml.GetXml(yft2);
                                if (xml != xml2)
                                { }
                            }
                            if (savetest && (yft != null) && (yft.Fragment != null))
                            {
                                if (entry is not RpfFileEntry fentry) continue;
                                if (fentry == null)
                                { continue; } //shouldn't happen

                                var bytes = yft.Save();


                                string origlen = TextUtil.GetBytesReadable(fentry.FileSize);
                                string bytelen = TextUtil.GetBytesReadable(bytes.Length);

                                var yft2 = new YftFile();
                                RpfFile.LoadResourceFile(yft2, bytes, 162);

                                if (yft2.Fragment == null)
                                { continue; }
                                if (yft2.Fragment.Drawable?.AllModels?.Length != yft.Fragment.Drawable?.AllModels?.Length)
                                { continue; }

                            }

                            if (glasstest && (yft?.Fragment?.GlassWindows?.data_items != null))
                            {
                                var lastf = -1;
                                for (int i = 0; i < yft.Fragment.GlassWindows.data_items.Length; i++)
                                {
                                    var w = yft.Fragment.GlassWindows.data_items[i];
                                    if (w.Flags == lastf) continue;
                                    lastf = w.Flags;
                                    flagdict.TryGetValue(w.Flags, out int n);
                                    if (n < 10)
                                    {
                                        flagdict[w.Flags] = n + 1;
                                        sb.AppendLine(entry.Path + " Window " + i.ToString() + ": Flags " + w.Flags.ToString() + ", Low:" + w.FlagsLo.ToString() + ", High:" + w.FlagsHi.ToString());
                                    }
                                }
                            }

                        }
                    }
                    //catch (Exception ex)
                    //{
                    //    UpdateStatus?.Invoke("Error! " + ex.ToString());
                    //}
                }
            }
            var teststr = sb.ToString();

            if (errorfiles.Count > 0)
            { }
        }
        public void TestYpts()
        {
            var savetest = false;
            var errorfiles = new List<RpfEntry>();
            foreach (RpfFile file in AllRpfs)
            {
                foreach (RpfEntry entry in file.AllEntries)
                {
                    //try
                    {
                        if (entry.NameLower.EndsWith(".ypt"))
                        {
                            UpdateStatus?.Invoke(entry.Path);
                            YptFile? ypt = null;
                            try
                            {
                                ypt = ArchiveManager.GetFile<YptFile>(entry);
                            }
                            catch (Exception ex)
                            {
                                UpdateStatus?.Invoke("Error! " + ex.ToString());
                                errorfiles.Add(entry);
                            }
                            if (savetest && (ypt != null) && (ypt.PtfxList != null))
                            {
                                if (entry is not RpfFileEntry fentry) continue;
                                if (fentry == null)
                                { continue; } //shouldn't happen

                                var bytes = ypt.Save();


                                string origlen = TextUtil.GetBytesReadable(fentry.FileSize);
                                string bytelen = TextUtil.GetBytesReadable(bytes.Length);

                                var ypt2 = new YptFile();
                                RpfFile.LoadResourceFile(ypt2, bytes, 68);

                                if (ypt2.PtfxList == null)
                                { continue; }
                                if (ypt2.PtfxList.Name?.Value != ypt.PtfxList.Name?.Value)
                                { continue; }

                            }
                        }
                    }
                    //catch (Exception ex)
                    //{
                    //    UpdateStatus?.Invoke("Error! " + ex.ToString());
                    //}
                }
            }
            if (errorfiles.Count > 0)
            { }
        }
        public void TestYnvs()
        {
            bool xmltest = true;
            var savetest = false;
            var errorfiles = new List<RpfEntry>();
            foreach (RpfFile file in AllRpfs)
            {
                foreach (RpfEntry entry in file.AllEntries)
                {
                    //try
                    {
                        if (entry.NameLower.EndsWith(".ynv"))
                        {
                            UpdateStatus?.Invoke(entry.Path);
                            YnvFile? ynv = null;
                            try
                            {
                                ynv = ArchiveManager.GetFile<YnvFile>(entry);
                            }
                            catch (Exception ex)
                            {
                                UpdateStatus?.Invoke("Error! " + ex.ToString());
                                errorfiles.Add(entry);
                            }
                            if (xmltest && (ynv != null) && (ynv.Nav != null))
                            {
                                var xml = YnvXml.GetXml(ynv);
                                if (xml == null) continue;
                                var ynv2 = XmlYnv.GetYnv(xml);
                                if (ynv2 == null) continue;
                                var ynv2b = ynv2.Save();
                                if (ynv2b == null) continue;
                                var ynv3 = new YnvFile();
                                RpfFile.LoadResourceFile(ynv3, ynv2b, 2);
                                var xml3 = YnvXml.GetXml(ynv3);
                                if (xml.Length != xml3.Length)
                                { }
                                var xmllines = xml.Split('\n');
                                var xml3lines = xml3.Split('\n');
                                if (xmllines.Length != xml3lines.Length)
                                { }
                            }
                            if (savetest && (ynv != null) && (ynv.Nav != null))
                            {
                                if (entry is not RpfFileEntry fentry) continue;
                                if (fentry == null)
                                { continue; } //shouldn't happen

                                var bytes = ynv.Save();

                                string origlen = TextUtil.GetBytesReadable(fentry.FileSize);
                                string bytelen = TextUtil.GetBytesReadable(bytes.Length);

                                var ynv2 = new YnvFile();
                                RpfFile.LoadResourceFile(ynv2, bytes, 2);

                                if (ynv2.Nav == null)
                                { continue; }

                            }
                        }
                    }
                    //catch (Exception ex)
                    //{
                    //    UpdateStatus?.Invoke("Error! " + ex.ToString());
                    //}
                }
            }
            if (errorfiles.Count > 0)
            { }
        }
        public void TestYvrs()
        {

            var exceptions = new List<Exception>();

            foreach (RpfFile file in AllRpfs)
            {
                foreach (RpfEntry entry in file.AllEntries)
                {
                    //try
                    {
                        var rfe = entry as RpfFileEntry;
                        if (rfe == null) continue;

                        if (rfe.NameLower.EndsWith(".yvr"))
                        {
                            if (rfe.NameLower == "agencyprep001.yvr") continue; //this file seems corrupted

                            UpdateStatus?.Invoke(entry.Path);

                            YvrFile yvr = new(rfe);
                            ArchiveManager.LoadFile(yvr, rfe);

                            var xml = YvrXml.GetXml(yvr);
                            var yvr2 = XmlYvr.GetYvr(xml);
                            var data2 = yvr2.Save();
                            var yvr3 = new YvrFile();
                            RpfFile.LoadResourceFile(yvr3, data2, 1);//full roundtrip
                            var xml2 = YvrXml.GetXml(yvr3);
                            if (xml != xml2)
                            { }

                        }
                    }
                    //catch (Exception ex)
                    //{
                    //    UpdateStatus?.Invoke("Error! " + ex.ToString());
                    //    exceptions.Add(ex);
                    //}
                }
            }

            if (exceptions.Count > 0)
            { }
        }
        public void TestYwrs()
        {

            var exceptions = new List<Exception>();

            foreach (RpfFile file in AllRpfs)
            {
                foreach (RpfEntry entry in file.AllEntries)
                {
                    //try
                    {
                        var rfe = entry as RpfFileEntry;
                        if (rfe == null) continue;

                        if (rfe.NameLower.EndsWith(".ywr"))
                        {
                            UpdateStatus?.Invoke(entry.Path);

                            YwrFile ywr = new(rfe);
                            ArchiveManager.LoadFile(ywr, rfe);

                            var xml = YwrXml.GetXml(ywr);
                            var ywr2 = XmlYwr.GetYwr(xml);
                            var data2 = ywr2.Save();
                            var ywr3 = new YwrFile();
                            RpfFile.LoadResourceFile(ywr3, data2, 1);//full roundtrip
                            var xml2 = YwrXml.GetXml(ywr3);
                            if (xml != xml2)
                            { }

                        }
                    }
                    //catch (Exception ex)
                    //{
                    //    UpdateStatus?.Invoke("Error! " + ex.ToString());
                    //    exceptions.Add(ex);
                    //}
                }
            }

            if (exceptions.Count > 0)
            { }
        }
        public void TestYmaps()
        {
            foreach (RpfFile file in AllRpfs)
            {
                foreach (RpfEntry entry in file.AllEntries)
                {
                    try
                    {
                        if (entry.NameLower.EndsWith(".ymap"))
                        {
                            UpdateStatus?.Invoke(entry.Path);
                            YmapFile? ymapfile = ArchiveManager.GetFile<YmapFile>(entry);
                            if ((ymapfile != null))// && (ymapfile.Meta != null))
                            { }
                        }
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus?.Invoke("Error! " + ex.ToString());
                    }
                }
            }
        }
        public void TestYpdbs()
        {
            foreach (RpfFile file in AllRpfs)
            {
                foreach (RpfEntry entry in file.AllEntries)
                {
                    var rfe = entry as RpfFileEntry;
                    if (rfe == null) continue;

                    try
                    {
                        if (rfe.NameLower.EndsWith(".ypdb"))
                        {
                            UpdateStatus?.Invoke(entry.Path);
                            YpdbFile? ypdb = ArchiveManager.GetFile<YpdbFile>(entry);
                            if (ypdb != null)
                            {
                                var odata = entry.File?.ExtractFile((RpfFileEntry)entry);
                                if (odata == null) continue;
                                //var ndata = ypdb.Save();

                                var xml = YpdbXml.GetXml(ypdb);
                                var ypdb2 = XmlYpdb.GetYpdb(xml);
                                var ndata = ypdb2.Save();

                                if (ndata.Length == odata.Length)
                                {
                                    for (int i = 0; i < ndata.Length; i++)
                                    {
                                        if (ndata[i] != odata[i])
                                        { break; }
                                    }
                                }
                                else
                                { }
                            }
                            else
                            { }
                        }
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus?.Invoke("Error! " + ex.ToString());
                    }

                }
            }
        }
        public void TestYfds()
        {
            foreach (RpfFile file in AllRpfs)
            {
                foreach (RpfEntry entry in file.AllEntries)
                {
                    var rfe = entry as RpfFileEntry;
                    if (rfe == null) continue;

                    try
                    {
                        if (rfe.NameLower.EndsWith(".yfd"))
                        {
                            UpdateStatus?.Invoke(entry.Path);
                            YfdFile? yfd = ArchiveManager.GetFile<YfdFile>(entry);
                            if (yfd != null)
                            {
                                if (yfd.FrameFilterDictionary != null)
                                {
                                    // check that all signatures can be re-calculated
                                    foreach (var f in yfd.FrameFilterDictionary.Filters.data_items)
                                    {
                                        if (f.Signature != f.CalculateSignature())
                                        { }
                                    }
                                }

                                var xml = YfdXml.GetXml(yfd);
                                var yfd2 = XmlYfd.GetYfd(xml);
                                var data2 = yfd2.Save();
                                var yfd3 = new YfdFile();
                                RpfFile.LoadResourceFile(yfd3, data2, 4);//full roundtrip
                                var xml2 = YfdXml.GetXml(yfd3);
                                if (xml != xml2)
                                { }
                            }
                            else
                            { }
                        }
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus?.Invoke("Error! " + ex.ToString());
                    }

                }
            }
        }
        public void TestMrfs()
        {
            foreach (RpfFile file in AllRpfs)
            {
                foreach (RpfEntry entry in file.AllEntries)
                {
                    try
                    {
                        if (entry.NameLower.EndsWith(".mrf"))
                        {
                            UpdateStatus?.Invoke(entry.Path);
                            MrfFile? mrffile = ArchiveManager.GetFile<MrfFile>(entry);
                            if (mrffile != null)
                            {
                                var odata = entry.File?.ExtractFile((RpfFileEntry)entry);
                                if (odata == null) continue;
                                var ndata = mrffile.Save();
                                if (ndata.Length == odata.Length)
                                {
                                    for (int i = 0; i < ndata.Length; i++)
                                    {
                                        if (ndata[i] != odata[i])
                                        { break; }
                                    }
                                }
                                else
                                { }

                                var xml = MrfXml.GetXml(mrffile);
                                var mrf2 = XmlMrf.GetMrf(xml);
                                var ndata2 = mrf2.Save();
                                if (ndata2.Length == odata.Length)
                                {
                                    for (int i = 0; i < ndata2.Length; i++)
                                    {
                                        if (ndata2[i] != odata[i] && !mrfDiffCanBeIgnored(i, mrffile))
                                        { break; }
                                    }
                                }
                                else
                                { }

                                bool mrfDiffCanBeIgnored(int fileOffset, MrfFile originalMrf)
                                {
                                    foreach (var n in originalMrf.AllNodes)
                                    {
                                        if (n is MrfNodeStateBase state)
                                        {
                                            // If TransitionCount is 0, the TransitionsOffset value can be ignored.
                                            // TransitionsOffset in original MRFs isn't always set to 0 in this case,
                                            // XML-imported MRFs always set it to 0
                                            if (state.TransitionCount == 0 && fileOffset == (state.FileOffset + 0x1C))
                                            {
                                                return true;
                                            }
                                        }
                                    }

                                    return false;
                                }
                            }
                            else
                            { }
                        }
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus?.Invoke("Error! " + ex.ToString());
                    }
                }
            }

            // create and save a custom MRF
            {
                // Usage example:
                //  RequestAnimDict("move_m@alien")
                //  TaskMoveNetworkByName(PlayerPedId(), "mymrf", 0.0, true, 0, 0)
                //  SetTaskMoveNetworkSignalFloat(PlayerPedId(), "sprintrate", 2.0)
                var mymrf = new MrfFile();
                var clip1 = new MrfNodeClip
                {
                    NodeIndex = 0,
                    Name = JenkHash.GenHash("clip1"),
                    ClipType = MrfValueType.Literal,
                    ClipContainerType = MrfClipContainerType.ClipDictionary,
                    ClipContainerName = JenkHash.GenHash("move_m@alien"),
                    ClipName = JenkHash.GenHash("alien_run"),
                    LoopedType = MrfValueType.Literal,
                    Looped = true,
                };
                var clip2 = new MrfNodeClip
                {
                    NodeIndex = 0,
                    Name = JenkHash.GenHash("clip2"),
                    ClipType = MrfValueType.Literal,
                    ClipContainerType = MrfClipContainerType.ClipDictionary,
                    ClipContainerName = JenkHash.GenHash("move_m@alien"),
                    ClipName = JenkHash.GenHash("alien_sprint"),
                    LoopedType = MrfValueType.Literal,
                    Looped = true,
                    RateType = MrfValueType.Parameter,
                    RateParameterName = JenkHash.GenHash("sprintrate"),
                };
                var clipstate1 = new MrfNodeState
                {
                    NodeIndex = 0,
                    Name = JenkHash.GenHash("clipstate1"),
                    InitialNode = clip1,
                    Transitions = new[]
                    {
                        new MrfStateTransition
                        {
                            Duration = 2.5f,
                            HasDurationParameter = false,
                            //TargetState = clipstate2,
                            Conditions = new[]
                            {
                                new MrfConditionTimeGreaterThan { Value = 4.0f },
                            },
                        }
                    },
                };
                var clipstate2 = new MrfNodeState
                {
                    NodeIndex = 1,
                    Name = JenkHash.GenHash("clipstate2"),
                    InitialNode = clip2,
                    Transitions = new[]
                    {
                        new MrfStateTransition
                        {
                            Duration = 2.5f,
                            HasDurationParameter = false,
                            //TargetState = clipstate1,
                            Conditions = new[]
                            {
                                new MrfConditionTimeGreaterThan { Value = 4.0f },
                            },
                }
                    },
                };
                clipstate1.Transitions[0].TargetState = clipstate2;
                clipstate2.Transitions[0].TargetState = clipstate1;
                var rootsm = new MrfNodeStateMachine
                {
                    NodeIndex = 0,
                    Name = JenkHash.GenHash("statemachine"),
                    States = new[]
                    {
                        new MrfStateRef { StateName = clipstate1.Name, State = clipstate1 },
                        new MrfStateRef { StateName = clipstate2.Name, State = clipstate2 },
                    },
                    InitialNode = clipstate1,
                };
                mymrf.AllNodes = new MrfNode[]
                {
                    rootsm,
                    clipstate1,
                    clip1,
                    clipstate2,
                    clip2,
                };
                mymrf.RootState = rootsm;

                var mymrfData = mymrf.Save();
                //File.WriteAllBytes("mymrf.mrf", mymrfData);
                //File.WriteAllText("mymrf.dot", mymrf.DumpStateGraph());
            }
        }
        public void TestFxcs()
        {
            foreach (RpfFile file in AllRpfs)
            {
                foreach (RpfEntry entry in file.AllEntries)
                {
                    try
                    {
                        if (entry.NameLower.EndsWith(".fxc"))
                        {
                            UpdateStatus?.Invoke(entry.Path);
                            var fxcfile = ArchiveManager.GetFile<FxcFile>(entry);
                            if (fxcfile != null)
                            {
                                var odata = entry.File?.ExtractFile((RpfFileEntry)entry);
                                if (odata == null) continue;
                                var ndata = fxcfile.Save();
                                if (ndata.Length == odata.Length)
                                {
                                    for (int i = 0; i < ndata.Length; i++)
                                    {
                                        if (ndata[i] != odata[i])
                                        { break; }
                                    }
                                }
                                else
                                { }

                                var xml1 = FxcXml.GetXml(fxcfile);//won't output bytecodes with no output folder
                                var fxc1 = XmlFxc.GetFxc(xml1);
                                var xml2 = FxcXml.GetXml(fxc1);
                                if (xml1 != xml2)
                                { }


                                for (int i = 0; i < fxcfile.Shaders.Length; i++)
                                {
                                    if (fxc1.Shaders[i].Name != fxcfile.Shaders[i].Name)
                                    { }
                                    fxc1.Shaders[i].ByteCode = fxcfile.Shaders[i].ByteCode;
                                }

                                var xdata = fxc1.Save();
                                if (xdata.Length == odata.Length)
                                {
                                    for (int i = 0; i < xdata.Length; i++)
                                    {
                                        if (xdata[i] != odata[i])
                                        { break; }
                                    }
                                }
                                else
                                { }


                            }
                            else
                            { }
                        }
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus?.Invoke("Error! " + ex.ToString());
                    }
                }
            }
        }
        public void TestPlacements()
        {
            //int totplacements = 0;
            //int tottimedplacements = 0;
            //int totaudioplacements = 0;
            //StringBuilder sbtest = new();
            //StringBuilder sbterr = new();
            //sbtest.AppendLine("X, Y, Z, name, assetName, drawableDictionary, textureDictionary, ymap");
            //foreach (RpfFile file in ArchiveManager.AllRpfs)
            //{
            //    foreach (RpfEntry entry in file.AllEntries)
            //    {
            //        try
            //        {
            //            if (entry.NameLower.EndsWith(".ymap"))
            //            {
            //                UpdateStatus?.Invoke(entry.Path);
            //                YmapFile? ymapfile = ArchiveManager.GetFile<YmapFile>(entry);
            //                if ((ymapfile != null))// && (ymapfile.Meta != null))
            //                {
            //                    //if (ymapfile.CMapData.parent == 0) //root ymap output
            //                    //{
            //                    //    sbtest.AppendLine(JenkIndex.GetString(ymapfile.CMapData.name) + ": " + entry.Path);
            //                    //}
            //                    if (ymapfile.CEntityDefs != null)
            //                    {
            //                        for (int n = 0; n < ymapfile.CEntityDefs.Length; n++)
            //                        {
            //                            //find ytyp...
            //                            var entdef = ymapfile.CEntityDefs[n];
            //                            var pos = entdef.position;
            //                            bool istimed = false;
            //                            Tuple<YtypFile, int> archetyp;
            //                            if (!BaseArchetypes.TryGetValue(entdef.archetypeName, out archetyp))
            //                            {
            //                                sbterr.AppendLine("Couldn't find ytyp for " + entdef.ToString());
            //                            }
            //                            else
            //                            {
            //                                int ymapbasecount = (archetyp.Item1.CBaseArchetypeDefs != null) ? archetyp.Item1.CBaseArchetypeDefs.Length : 0;
            //                                int baseoffset = archetyp.Item2 - ymapbasecount;
            //                                if (baseoffset >= 0)
            //                                {
            //                                    if ((archetyp.Item1.CTimeArchetypeDefs == null) || (baseoffset > archetyp.Item1.CTimeArchetypeDefs.Length))
            //                                    {
            //                                        sbterr.AppendLine("Couldn't lookup CTimeArchetypeDef... " + archetyp.ToString());
            //                                        continue;
            //                                    }

            //                                    istimed = true;

            //                                    //it's a CTimeArchetypeDef...
            //                                    CTimeArchetypeDef ctad = archetyp.Item1.CTimeArchetypeDefs[baseoffset];

            //                                    //if (ctad.ToString().Contains("spider"))
            //                                    //{
            //                                    //}
            //                                    //sbtest.AppendFormat("{0}, {1}, {2}, {3}, {4}", pos.X, pos.Y, pos.Z, ctad.ToString(), entry.Name);
            //                                    //sbtest.AppendLine();

            //                                    tottimedplacements++;
            //                                }
            //                                totplacements++;
            //                            }

            //                            Tuple<YtypFile, int> audiotyp;
            //                            if (AudioArchetypes.TryGetValue(entdef.archetypeName, out audiotyp))
            //                            {
            //                                if (istimed)
            //                                {
            //                                }
            //                                if (!BaseArchetypes.TryGetValue(entdef.archetypeName, out archetyp))
            //                                {
            //                                    sbterr.AppendLine("Couldn't find ytyp for " + entdef.ToString());
            //                                }
            //                                if (audiotyp.Item1 != archetyp.Item1)
            //                                {
            //                                }

            //                                CBaseArchetypeDef cbad = archetyp.Item1.CBaseArchetypeDefs[archetyp.Item2];
            //                                CExtensionDefAudioEmitter emitr = audiotyp.Item1.AudioEmitters[audiotyp.Item2];

            //                                if (emitr.name != cbad.name)
            //                                {
            //                                }

            //                                string hashtest = JenkIndex.GetString(emitr.effectHash);

            //                                sbtest.AppendFormat("{0}, {1}, {2}, {3}, {4}, {5}", pos.X, pos.Y, pos.Z, cbad.ToString(), entry.Name, hashtest);
            //                                sbtest.AppendLine();

            //                                totaudioplacements++;
            //                            }

            //                        }
            //                    }

            //                    //if (ymapfile.TimeCycleModifiers != null)
            //                    //{
            //                    //    for (int n = 0; n < ymapfile.TimeCycleModifiers.Length; n++)
            //                    //    {
            //                    //        var tcmod = ymapfile.TimeCycleModifiers[n];
            //                    //        Tuple<YtypFile, int> archetyp;
            //                    //        if (BaseArchetypes.TryGetValue(tcmod.name, out archetyp))
            //                    //        {
            //                    //        }
            //                    //        else
            //                    //        {
            //                    //        }
            //                    //    }
            //                    //}
            //                }
            //            }
            //        }
            //        catch (Exception ex)
            //        {
            //            sbterr.AppendLine(entry.Path + ": " + ex.ToString());
            //        }
            //    }
            //}

            //UpdateStatus?.Invoke("Ymap scan finished.");

            //sbtest.AppendLine();
            //sbtest.AppendLine(totplacements.ToString() + " total CEntityDef placements parsed");
            //sbtest.AppendLine(tottimedplacements.ToString() + " total CTimeArchetypeDef placements");
            //sbtest.AppendLine(totaudioplacements.ToString() + " total CExtensionDefAudioEmitter placements");

            //string teststr = sbtest.ToString();
            //string testerr = sbterr.ToString();

            //return;
        }
        public void TestDrawables()
        {


            DateTime starttime = DateTime.Now;

            bool doydr = true;
            bool doydd = true;
            bool doyft = true;

            List<string> errs = new();
            Dictionary<ulong, VertexDeclaration> vdecls = new();
            Dictionary<ulong, int> vdecluse = new();
            int drawablecount = 0;
            foreach (RpfFile file in AllRpfs)
            {
                foreach (RpfEntry entry in file.AllEntries)
                {
                    try
                    {
                        if (doydr && entry.NameLower.EndsWith(".ydr"))
                        {
                            UpdateStatus?.Invoke(entry.Path);
                            YdrFile? ydr = ArchiveManager.GetFile<YdrFile>(entry);

                            if (ydr == null)
                            {
                                errs.Add(entry.Path + ": Couldn't read file");
                                continue;
                            }
                            if (ydr.Drawable == null)
                            {
                                errs.Add(entry.Path + ": Couldn't read drawable data");
                                continue;
                            }
                            drawablecount++;
                            foreach (var kvp in ydr.Drawable.VertexDecls)
                            {
                                if (vdecls.TryAdd(kvp.Key, kvp.Value))
                                {
                                    vdecluse.Add(kvp.Key, 1);
                                }
                                else
                                {
                                    vdecluse[kvp.Key]++;
                                }
                            }
                        }
                        else if (doydd & entry.NameLower.EndsWith(".ydd"))
                        {
                            UpdateStatus?.Invoke(entry.Path);
                            YddFile? ydd = ArchiveManager.GetFile<YddFile>(entry);

                            if (ydd == null)
                            {
                                errs.Add(entry.Path + ": Couldn't read file");
                                continue;
                            }
                            if (ydd.Dict == null)
                            {
                                errs.Add(entry.Path + ": Couldn't read drawable dictionary data");
                                continue;
                            }
                            foreach (var drawable in ydd.Dict.Values)
                            {
                                drawablecount++;
                                foreach (var kvp in drawable.VertexDecls)
                                {
                                    if (vdecls.TryAdd(kvp.Key, kvp.Value))
                                    {
                                        vdecluse.Add(kvp.Key, 1);
                                    }
                                    else
                                    {
                                        vdecluse[kvp.Key]++;
                                    }
                                }
                            }
                        }
                        else if (doyft && entry.NameLower.EndsWith(".yft"))
                        {
                            UpdateStatus?.Invoke(entry.Path);
                            YftFile? yft = ArchiveManager.GetFile<YftFile>(entry);

                            if (yft == null)
                            {
                                errs.Add(entry.Path + ": Couldn't read file");
                                continue;
                            }
                            if (yft.Fragment == null)
                            {
                                errs.Add(entry.Path + ": Couldn't read fragment data");
                                continue;
                            }
                            if (yft.Fragment.Drawable != null)
                            {
                                drawablecount++;
                                foreach (var kvp in yft.Fragment.Drawable.VertexDecls)
                                {
                                    if (vdecls.TryAdd(kvp.Key, kvp.Value))
                                    {
                                        vdecluse.Add(kvp.Key, 1);
                                    }
                                    else
                                    {
                                        vdecluse[kvp.Key]++;
                                    }
                                }
                            }
                            if ((yft.Fragment.Cloths != null) && (yft.Fragment.Cloths.data_items != null))
                            {
                                foreach (var cloth in yft.Fragment.Cloths.data_items)
                                {
                                    drawablecount++;
                                    foreach (var kvp in cloth.Drawable?.VertexDecls ?? [])
                                    {
                                        if (vdecls.TryAdd(kvp.Key, kvp.Value))
                                        {
                                            vdecluse.Add(kvp.Key, 1);
                                        }
                                        else
                                        {
                                            vdecluse[kvp.Key]++;
                                        }
                                    }
                                }
                            }
                            if ((yft.Fragment.DrawableArray != null) && (yft.Fragment.DrawableArray.data_items != null))
                            {
                                foreach (var drawable in yft.Fragment.DrawableArray.data_items)
                                {
                                    drawablecount++;
                                    foreach (var kvp in drawable.VertexDecls)
                                    {
                                        if (vdecls.TryAdd(kvp.Key, kvp.Value))
                                        {
                                            vdecluse.Add(kvp.Key, 1);
                                        }
                                        else
                                        {
                                            vdecluse[kvp.Key]++;
                                        }
                                    }
                                }
                            }

                        }

                    }
                    catch (Exception ex)
                    {
                        errs.Add(entry.Path + ": " + ex.ToString());
                    }
                }
            }


            string errstr = string.Join("\r\n", errs);



            //build vertex types code string
            errs.Clear();
            StringBuilder sbverts = new();
            foreach (var kvp in vdecls)
            {
                var vd = kvp.Value;
                int usage = vdecluse[kvp.Key];
                sbverts.AppendFormat("public struct VertexType{0} //id: {1}, stride: {2}, flags: {3}, types: {4}, refs: {5}", vd.Flags, kvp.Key, vd.Stride, vd.Flags, vd.Types, usage);
                sbverts.AppendLine();
                sbverts.AppendLine("{");
                uint compid = 1;
                for (int i = 0; i < 16; i++)
                {
                    if (((vd.Flags >> i) & 1) == 1)
                    {
                        string typestr = "Unknown";
                        uint type = (uint)(((ulong)vd.Types >> (4 * i)) & 0xF);
                        switch (type)
                        {
                            case 0: typestr = "ushort"; break;// Data[i] = new ushort[1 * count]; break;
                            case 1: typestr = "ushort2"; break;// Data[i] = new ushort[2 * count]; break;
                            case 2: typestr = "ushort3"; break;// Data[i] = new ushort[3 * count]; break;
                            case 3: typestr = "ushort4"; break;// Data[i] = new ushort[4 * count]; break;
                            case 4: typestr = "float"; break;// Data[i] = new float[1 * count]; break;
                            case 5: typestr = "Vector2"; break;// Data[i] = new float[2 * count]; break;
                            case 6: typestr = "Vector3"; break;// Data[i] = new float[3 * count]; break;
                            case 7: typestr = "Vector4"; break;// Data[i] = new float[4 * count]; break;
                            case 8: typestr = "uint"; break;// Data[i] = new uint[count]; break;
                            case 9: typestr = "uint"; break;// Data[i] = new uint[count]; break;
                            case 10: typestr = "uint"; break;// Data[i] = new uint[count]; break;
                            default:
                                break;
                        }
                        sbverts.AppendLine("   public " + typestr + " Component" + compid.ToString() + ";");
                        compid++;
                    }

                }
                sbverts.AppendLine("}");
                sbverts.AppendLine();
            }

            string vertstr = sbverts.ToString();
            string verrstr = string.Join("\r\n", errs);

            UpdateStatus?.Invoke((DateTime.Now - starttime).ToString() + " elapsed, " + drawablecount.ToString() + " drawables, " + errs.Count.ToString() + " errors.");

        }
        public void TestCacheFiles()
        {
            foreach (RpfFile file in AllRpfs)
            {
                foreach (RpfEntry entry in file.AllEntries)
                {
                    try
                    {
                        if (entry.NameLower.EndsWith("cache_y.dat"))// || entry.NameLower.EndsWith("cache_y_bank.dat"))
                        {
                            UpdateStatus?.Invoke(entry.Path);
                            var cdfile = ArchiveManager.GetFile<CacheDatFile>(entry);
                            if (cdfile != null)
                            {
                                var odata = entry.File?.ExtractFile((RpfFileEntry)entry);
                                if (odata == null) continue;
                                //var ndata = cdfile.Save();

                                var xml = CacheDatXml.GetXml(cdfile);
                                var cdf2 = XmlCacheDat.GetCacheDat(xml);
                                var ndata = cdf2.Save();

                                if (ndata.Length == odata.Length)
                                {
                                    for (int i = 0; i < ndata.Length; i++)
                                    {
                                        if (ndata[i] != odata[i])
                                        { break; }
                                    }
                                }
                                else
                                { }
                            }
                            else
                            { }
                        }
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus?.Invoke("Error! " + ex.ToString());
                    }
                }
            }
        }
        public void TestHeightmaps()
        {
            var errorfiles = new List<RpfEntry>();
            foreach (RpfFile file in AllRpfs)
            {
                foreach (RpfEntry entry in file.AllEntries)
                {
                    if (entry.NameLower.EndsWith(".dat") && entry.NameLower.StartsWith("heightmap"))
                    {
                        UpdateStatus?.Invoke(entry.Path);
                        HeightmapFile? hmf = null;
                        hmf = ArchiveManager.GetFile<HeightmapFile>(entry);
                        if (hmf == null) continue;
                        var d1 = hmf.RawFileData;
                        //var d2 = hmf.Save();
                        var xml = HmapXml.GetXml(hmf);
                        var hmf2 = XmlHmap.GetHeightmap(xml);
                        var d2 = hmf2.Save();

                        if (d1.Length == d2.Length)
                        {
                            for (int i = 0; i < d1.Length; i++)
                            {
                                if (d1[i] != d2[i])
                                { }
                            }
                        }
                        else
                        { }

                    }
                }
            }
            if (errorfiles.Count > 0)
            { }
        }
        public void TestWatermaps()
        {
            var errorfiles = new List<RpfEntry>();
            foreach (RpfFile file in AllRpfs)
            {
                foreach (RpfEntry entry in file.AllEntries)
                {
                    if (entry.NameLower.EndsWith(".dat") && entry.NameLower.StartsWith("waterheight"))
                    {
                        UpdateStatus?.Invoke(entry.Path);
                        WatermapFile? wmf = null;
                        wmf = ArchiveManager.GetFile<WatermapFile>(entry);
                        //var d1 = wmf.RawFileData;
                        //var d2 = wmf.Save();
                        //var xml = WatermapXml.GetXml(wmf);
                        //var wmf2 = XmlWatermap.GetWatermap(xml);
                        //var d2 = wmf2.Save();

                        //if (d1.Length == d2.Length)
                        //{
                        //    for (int i = 0; i < d1.Length; i++)
                        //    {
                        //        if (d1[i] != d2[i])
                        //        { }
                        //    }
                        //}
                        //else
                        //{ }

                    }
                }
            }
            if (errorfiles.Count > 0)
            { }
        }
        public void GetShadersXml()
        {
            bool doydr = true;
            bool doydd = true;
            bool doyft = true;
            bool doypt = true;

            var data = new Dictionary<MetaHash, ShaderXmlDataCollection>();

            void collectDrawable(DrawableBase? d)
            {
                if (d?.AllModels == null) return;
                foreach (var model in d.AllModels)
                {
                    if (model?.Geometries == null) continue;
                    foreach (var geom in model.Geometries)
                    {
                        if (geom == null) continue;
                        var s = geom.Shader;
                        if (s == null) continue;
                        ShaderXmlDataCollection? dc = null;
                        if (!data.TryGetValue(s.Name, out dc))
                        {
                            dc = new ShaderXmlDataCollection();
                            dc.Name = s.Name;
                            data.Add(s.Name, dc);
                        }
                        dc.AddShaderUse(s, geom);
                    }
                }
            }



            foreach (RpfFile file in AllRpfs)
            {
                foreach (RpfEntry entry in file.AllEntries)
                {
                    try
                    {
                        if (doydr && entry.NameLower.EndsWith(".ydr"))
                        {
                            UpdateStatus?.Invoke(entry.Path);
                            YdrFile? ydr = ArchiveManager.GetFile<YdrFile>(entry);

                            if (ydr == null) { continue; }
                            if (ydr.Drawable == null) { continue; }
                            collectDrawable(ydr.Drawable);
                        }
                        else if (doydd & entry.NameLower.EndsWith(".ydd"))
                        {
                            UpdateStatus?.Invoke(entry.Path);
                            YddFile? ydd = ArchiveManager.GetFile<YddFile>(entry);

                            if (ydd == null) { continue; }
                            if (ydd.Dict == null) { continue; }
                            foreach (var drawable in ydd.Dict.Values)
                            {
                                collectDrawable(drawable);
                            }
                        }
                        else if (doyft && entry.NameLower.EndsWith(".yft"))
                        {
                            UpdateStatus?.Invoke(entry.Path);
                            YftFile? yft = ArchiveManager.GetFile<YftFile>(entry);

                            if (yft == null) { continue; }
                            if (yft.Fragment == null) { continue; }
                            if (yft.Fragment.Drawable != null)
                            {
                                collectDrawable(yft.Fragment.Drawable);
                            }
                            if ((yft.Fragment.Cloths != null) && (yft.Fragment.Cloths.data_items != null))
                            {
                                foreach (var cloth in yft.Fragment.Cloths.data_items)
                                {
                                    collectDrawable(cloth.Drawable);
                                }
                            }
                            if ((yft.Fragment.DrawableArray != null) && (yft.Fragment.DrawableArray.data_items != null))
                            {
                                foreach (var drawable in yft.Fragment.DrawableArray.data_items)
                                {
                                    collectDrawable(drawable);
                                }
                            }
                        }
                        else if (doypt && entry.NameLower.EndsWith(".ypt"))
                        {
                            UpdateStatus?.Invoke(entry.Path);
                            YptFile? ypt = ArchiveManager.GetFile<YptFile>(entry);

                            if (ypt == null) { continue; }
                            if (ypt.DrawableDict == null) { continue; }
                            foreach (var drawable in ypt.DrawableDict.Values)
                            {
                                collectDrawable(drawable);
                            }
                        }
                    }
                    catch //(Exception ex)
                    { }
                }
            }




            // Loop alternative: create list directly from values and sort
            var shaders = new List<ShaderXmlDataCollection>(data.Values);
            shaders.Sort((a, b) => b.GeomCount.CompareTo(a.GeomCount));


            StringBuilder sb = new();

            sb.AppendLine(MetaXml.XmlHeader);
            MetaXml.OpenTag(sb, 0, "Shaders");
            foreach (var s in shaders)
            {
                MetaXml.OpenTag(sb, 1, "Item");
                MetaXml.StringTag(sb, 2, "Name", MetaXml.HashString(s.Name));
                MetaXml.WriteHashItemArray(sb, s.GetSortedList(s.FileNames).ToArray(), 2, "FileName");
                MetaXml.WriteRawArray(sb, s.GetSortedList(s.RenderBuckets).ToArray(), 2, "RenderBucket", "");
                MetaXml.OpenTag(sb, 2, "Layout");
                var layouts = s.GetSortedList(s.VertexLayouts);
                foreach (var l in layouts)
                {
                    var vd = new VertexDeclaration();
                    vd.Types = l.Types;
                    vd.Flags = l.Flags;
                    vd.WriteXml(sb, 3, "Item");
                }
                MetaXml.CloseTag(sb, 2, "Layout");
                MetaXml.OpenTag(sb, 2, "Parameters");
                var texparams = s.GetSortedList(s.TexParams);
                var valparams = s.ValParams;
                var arrparams = s.ArrParams;
                foreach (var tp in texparams)
                {
                    MetaXml.SelfClosingTag(sb, 3, $"Item name=\"{(ShaderParamNames)tp}\" type=\"Texture\"");
                }
                foreach (var vp in valparams)
                {
                    var svp = s.GetSortedList(vp.Value);
                    var defval = svp.FirstOrDefault();
                    MetaXml.SelfClosingTag(sb, 3, $"Item name=\"{(ShaderParamNames)vp.Key}\" type=\"Vector\" " + FloatUtil.GetVector4XmlString(defval));
                }
                foreach (var ap in arrparams)
                {
                    var defval = ap.Value.FirstOrDefault();
                    MetaXml.OpenTag(sb, 3, $"Item name=\"{(ShaderParamNames)ap.Key}\" type=\"Array\"");
                    if (defval != null)
                    {
                        foreach (var vec in defval)
                        {
                            MetaXml.SelfClosingTag(sb, 4, "Value " + FloatUtil.GetVector4XmlString(vec));
                        }
                    }
                    MetaXml.CloseTag(sb, 3, "Item");
                }
                MetaXml.CloseTag(sb, 2, "Parameters");
                MetaXml.CloseTag(sb, 1, "Item");
            }
            MetaXml.CloseTag(sb, 0, "Shaders");

            var xml = sb.ToString();

            File.WriteAllText("C:\\Shaders.xml", xml);


        }
        public void GetShadersLegacyConversionXml()
        {
            //on legacy game files, iterate fxc files and generate mapping of old>new param names
            //for use by legacy/gen9 conversions
            //gen9 param names seem to use the other name as speficied in the fxc files...


            var dict = new Dictionary<string, Dictionary<string, string>>();//shadername:(legacyparam:gen9param)

            foreach (RpfFile file in AllRpfs)
            {
                foreach (RpfEntry entry in file.AllEntries)
                {
                    if (entry.NameLower.EndsWith(".fxc"))
                    {
                        UpdateStatus?.Invoke(entry.Path);
                        var fxcfile = ArchiveManager.GetFile<FxcFile>(entry);
                        if (fxcfile != null)
                        {
                            var sname = entry.GetShortNameLower();
                            dict.TryGetValue(sname, out var pdict);
                            if (pdict == null)
                            {
                                pdict = new Dictionary<string, string>();
                                dict[sname] = pdict;
                            }

                            var paras = fxcfile.Variables2;
                            if (paras != null)
                            {
                                foreach (var para in paras)
                                {
                                    if (para == null) continue;
                                    pdict[para.Name1] = para.Name2;
                                }
                            }

                        }
                    }
                }
            }

            // Loop alternative: create list directly from keys and sort
            var shadernames = new List<string>(dict.Keys);
            shadernames.Sort();

            var sb = new StringBuilder();
            sb.AppendLine(MetaXml.XmlHeader);
            MetaXml.OpenTag(sb, 0, "ShadersLegacyConversion");
            foreach (var shadername in shadernames)
            {
                MetaXml.OpenTag(sb, 1, "Item");
                MetaXml.StringTag(sb, 2, "Name", shadername);
                MetaXml.StringTag(sb, 2, "FileName", shadername.ToLowerInvariant() + ".sps");
                MetaXml.OpenTag(sb, 2, "Parameters");
                dict.TryGetValue(shadername, out var pdict);
                if (pdict != null)
                {
                    foreach (var kvp in pdict)
                    {
                        MetaXml.SelfClosingTag(sb, 3, $"Item name=\"{kvp.Key}\" gen9=\"{kvp.Value}\"");
                    }
                }
                MetaXml.CloseTag(sb, 2, "Parameters");
                MetaXml.CloseTag(sb, 1, "Item");
            }
            MetaXml.CloseTag(sb, 0, "ShadersLegacyConversion");

            var xml = sb.ToString();

            File.WriteAllText("C:\\ShadersLegacyConversion.xml", xml);


        }
        public void GetShadersGen9ConversionXml()
        {
            //on gen9 game files, iterate drawables and find param offsets and types
            //use the ShadersLegacyConversion.xml to generate ShadersGen9Conversion.xml,
            //filtering to required shaders only and including the offsets and types.
            //this should allow for rebuilding the gen9 params buffers from legacy ones.


            bool doydr = true;
            bool doydd = true;
            bool doyft = true;
            bool doypt = true;

            var data = new Dictionary<MetaHash, ShaderGen9XmlDataCollection>();

            void updateDC(ShaderGen9XmlDataCollection dc, ShaderParamInfoG9[] infos, ShaderFX s)
            {
                var pi = s.G9_ParamInfos ?? throw new InvalidOperationException("Gen9 shader parameter information is missing.");
                var pb = s.ParametersList ?? throw new InvalidOperationException("Gen9 shader parameters are missing.");
                var bc = pi.NumBuffers;
                var bsizs = pb.G9_BufferSizes;

                var blens = new int[bc];
                for (int i = 0; i < bc; i++)
                {
                    blens[i] = (int)bsizs[i];
                }
                dc.BufferSizes = blens;
                dc.ParamInfos = infos;
                dc.SamplerValues = pb.G9_Samplers;

            }
            void collectDrawable(DrawableBase? d)
            {
                if (d?.AllModels == null) return;
                foreach (var model in d.AllModels)
                {
                    if (model?.Geometries == null) continue;
                    foreach (var geom in model.Geometries)
                    {
                        if (geom == null) continue;
                        var s = geom.Shader;
                        if (s == null) continue;
                        if (s.G9_ParamInfos == null || s.ParametersList == null) continue;
                        data.TryGetValue(s.Name, out var dc);
                        if (dc == null)
                        {
                            dc = new ShaderGen9XmlDataCollection();
                            dc.Name = s.Name;
                            updateDC(dc, s.G9_ParamInfos.Params, s);
                            data[s.Name] = dc;
                        }
                        else
                        {
                            var pi = s.G9_ParamInfos;
                            var pb = s.ParametersList;
                            var bc = pi.NumBuffers;
                            var bsizs = pb.G9_BufferSizes;
                            var changed = false;//sometimes params don't all match... ugh
                            if (dc.BufferSizes.Length != bc)
                            { changed = true; }
                            for (int i = 0; i < bc; i++)
                            {
                                if (dc.BufferSizes[i] != bsizs[i])
                                { /*changed = true;*/ break; }
                            }
                            if (dc.ParamInfos.Length < pi.Params.Length)
                            { changed = true; }//just take whichever has the most params.. maybe not 100% correct since really we want the latest ones
                            if (changed)
                            {
                                updateDC(dc, pi.Params, s);
                            }
                        }

                    }
                }
            }

            foreach (RpfFile file in AllRpfs)
            {
                foreach (RpfEntry entry in file.AllEntries)
                {
                    try
                    {
                        if (doydr && entry.NameLower.EndsWith(".ydr"))
                        {
                            if (entry is RpfResourceFileEntry re)
                            {
                                if (re.Version != 159) continue;
                            }

                            UpdateStatus?.Invoke(entry.Path);
                            YdrFile? ydr = ArchiveManager.GetFile<YdrFile>(entry);

                            if (ydr == null) { continue; }
                            if (ydr.Drawable == null) { continue; }
                            collectDrawable(ydr.Drawable);
                        }
                        else if (doydd & entry.NameLower.EndsWith(".ydd"))
                        {
                            UpdateStatus?.Invoke(entry.Path);
                            YddFile? ydd = ArchiveManager.GetFile<YddFile>(entry);

                            if (ydd == null) { continue; }
                            if (ydd.Dict == null) { continue; }
                            foreach (var drawable in ydd.Dict.Values)
                            {
                                collectDrawable(drawable);
                            }
                        }
                        else if (doyft && entry.NameLower.EndsWith(".yft"))
                        {
                            UpdateStatus?.Invoke(entry.Path);
                            YftFile? yft = ArchiveManager.GetFile<YftFile>(entry);

                            if (yft == null) { continue; }
                            if (yft.Fragment == null) { continue; }
                            if (yft.Fragment.Drawable != null)
                            {
                                collectDrawable(yft.Fragment.Drawable);
                            }
                            if ((yft.Fragment.Cloths != null) && (yft.Fragment.Cloths.data_items != null))
                            {
                                foreach (var cloth in yft.Fragment.Cloths.data_items)
                                {
                                    collectDrawable(cloth.Drawable);
                                }
                            }
                            if ((yft.Fragment.DrawableArray != null) && (yft.Fragment.DrawableArray.data_items != null))
                            {
                                foreach (var drawable in yft.Fragment.DrawableArray.data_items)
                                {
                                    collectDrawable(drawable);
                                }
                            }
                        }
                        else if (doypt && entry.NameLower.EndsWith(".ypt"))
                        {
                            UpdateStatus?.Invoke(entry.Path);
                            YptFile? ypt = ArchiveManager.GetFile<YptFile>(entry);

                            if (ypt == null) { continue; }
                            if (ypt.DrawableDict == null) { continue; }
                            foreach (var drawable in ypt.DrawableDict.Values)
                            {
                                collectDrawable(drawable);
                            }
                        }
                    }
                    catch //(Exception ex)
                    { }
                }
            }





            var legxml = File.ReadAllText("C:\\ShadersLegacyConversion.xml");
            var xdoc = new XmlDocument();
            xdoc.LoadXml(legxml);
            var shaders = xdoc.SelectNodes("ShadersLegacyConversion/Item")?.Cast<XmlNode>().ToArray() ?? [];
            var shadernodes = new Dictionary<MetaHash, XmlNode>();
            var shadernames = new List<string>();
            foreach (XmlNode shader in shaders)
            {
                var name = (Xml.GetChildInnerText(shader, "Name") ?? string.Empty).ToLowerInvariant();
                var hash = new MetaHash(JenkHash.GenHash(name));
                if (data.ContainsKey(hash) == false) continue;
                shadernodes[hash] = shader;
                shadernames.Add(name);
            }
            shadernames.Sort();

            if (shadernames.Count != data.Count)
            { }//this shouldn't happen - something was missing?


            StringBuilder sb = new();
            sb.AppendLine(MetaXml.XmlHeader);
            MetaXml.OpenTag(sb, 0, "ShadersGen9Conversion");
            foreach (var name in shadernames)
            {
                var hash = new MetaHash(JenkHash.GenHash(name));
                data.TryGetValue(hash, out var cd);
                shadernodes.TryGetValue(hash, out var sn);
                if (cd == null) continue;//shouldn't happen
                if (sn == null) continue;//shouldn't happen

                var pdict = new Dictionary<MetaHash, (string, string)>();//gen9hash:(gen9str:legacystr)
                var pnodes = sn.SelectNodes("Parameters/Item")?.Cast<XmlNode>().ToArray() ?? [];
                foreach (XmlNode p in pnodes)
                {
                    var old = Xml.GetStringAttribute(p, "name");
                    var gen9 = Xml.GetStringAttribute(p, "gen9") ?? string.Empty;
                    var phash = new MetaHash(JenkHash.GenHashLowerInvariant(gen9));
                    pdict[phash] = (gen9, old ?? string.Empty);
                }

                MetaXml.OpenTag(sb, 1, "Item");
                MetaXml.StringTag(sb, 2, "Name", name);
                MetaXml.StringTag(sb, 2, "FileName", Xml.GetChildInnerText(sn, "FileName"));
                MetaXml.StringTag(sb, 2, "BufferSizes", string.Join(" ", cd.BufferSizes));
                MetaXml.OpenTag(sb, 2, "Parameters");
                foreach (var p in cd.ParamInfos)
                {
                    var istr = $"Item type=\"{p.Type}\"";
                    if (pdict.TryGetValue(p.Name, out var pdv))
                    {
                        istr += $" name=\"{pdv.Item1}\" old=\"{pdv.Item2}\"";
                    }
                    else
                    {
                        istr += $" name=\"{MetaXml.HashString(p.Name)}\"";
                    }
                    switch (p.Type)
                    {
                        case ShaderParamTypeG9.Texture: istr += $" index=\"{p.TextureIndex}\""; break;
                        case ShaderParamTypeG9.Unknown: istr += $" index=\"{p.SamplerIndex}\""; break;
                        case ShaderParamTypeG9.Sampler: istr += $" index=\"{p.SamplerIndex}\" sampler=\"{cd.SamplerValues[p.SamplerIndex]}\""; break;
                        case ShaderParamTypeG9.CBuffer: istr += $" buffer=\"{p.CBufferIndex}\" length=\"{p.ParamLength}\" offset=\"{p.ParamOffset}\""; break;
                    }
                    MetaXml.SelfClosingTag(sb, 3, istr);
                }
                MetaXml.CloseTag(sb, 2, "Parameters");
                MetaXml.CloseTag(sb, 1, "Item");
            }
            MetaXml.CloseTag(sb, 0, "ShadersGen9Conversion");
            var xml = sb.ToString();

            File.WriteAllText("C:\\ShadersGen9Conversion.xml", xml);

        }
        public void GetArchetypeTimesList()
        {

            StringBuilder sb = new();
            sb.AppendLine("Name,AssetName,12am,1am,2am,3am,4am,5am,6am,7am,8am,9am,10am,11am,12pm,1pm,2pm,3pm,4pm,5pm,6pm,7pm,8pm,9pm,10pm,11pm,+12am,+1am,+2am,+3am,+4am,+5am,+6am,+7am");
            foreach (var ytyp in YtypDict.Values)
            {
                foreach (var arch in ytyp.AllArchetypes)
                {
                    if (arch.Type == MetaName.CTimeArchetypeDef)
                    {
                        if (arch is not TimeArchetype ta) continue;
                        var t = ta.TimeFlags;
                        sb.Append(arch.Name);
                        sb.Append(",");
                        sb.Append(arch.AssetName);
                        sb.Append(",");
                        for (int i = 0; i < 32; i++)
                        {
                            bool v = ((t >> i) & 1) == 1;
                            sb.Append(v ? "1" : "0");
                            sb.Append(",");
                        }
                        sb.AppendLine();
                    }
                }
            }

            var csv = sb.ToString();



        }



        public static Dictionary<MetaHash, ShaderGen9XmlDataCollection>? ShadersGen9ConversionData;
        [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(ShadersGen9ConversionData))]
        public static void EnsureShadersGen9ConversionData()
        {
            if (ShadersGen9ConversionData != null) return;

            var path = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var dir = Path.GetDirectoryName(path) ?? AppContext.BaseDirectory;
            var fpath = Path.Combine(dir, "ShadersGen9Conversion.xml");
            if (File.Exists(fpath) == false) throw new Exception("Unable to load ShadersGen9Conversion.xml");//where's the XML file huh?
            var gen9xml = File.ReadAllText(fpath);
            var xdoc = new XmlDocument();
            xdoc.LoadXml(gen9xml);
            var shaders = xdoc.SelectNodes("ShadersGen9Conversion/Item")?.Cast<XmlNode>().ToArray() ?? [];
            var dict = new Dictionary<MetaHash, ShaderGen9XmlDataCollection>();
            var infos = new List<ShaderParamInfoG9>();
            var svdict = new Dictionary<byte, byte>();
            foreach (XmlNode shader in shaders)
            {
                infos.Clear();
                svdict.Clear();

                var name = (Xml.GetChildInnerText(shader, "Name") ?? string.Empty).ToLowerInvariant();
                var hash = new MetaHash(JenkHash.GenHash(name));
                var dc = new ShaderGen9XmlDataCollection();
                dc.Name = hash;
                dc.BufferSizes = Xml.GetChildRawIntArray(shader, "BufferSizes");
                dc.ParamsMapLegacyToGen9 = new Dictionary<MetaHash, MetaHash>();
                dc.ParamsMapGen9ToLegacy = new Dictionary<MetaHash, MetaHash>();

                var pnodes = shader.SelectNodes("Parameters/Item")?.Cast<XmlNode>().ToArray() ?? [];
                foreach (XmlNode p in pnodes)
                {
                    var ptype = Xml.GetStringAttribute(p, "type");
                    var pname = Xml.GetStringAttribute(p, "name")?.ToLowerInvariant() ?? string.Empty;
                    var pnameold = Xml.GetStringAttribute(p, "old")?.ToLowerInvariant();
                    var phash = JenkHash.GenHash(pname);
                    var phashold = JenkHash.GenHash(pnameold ?? string.Empty);
                    if (phash != 0)
                    {
                        if (pname.StartsWith("hash_"))
                        {
                            phash = (MetaHash)Convert.ToUInt32(pname.Substring(5), 16);
                        }
                        else
                        {
                            JenkIndex.Ensure(pname);
                        }
                    }
                    Enum.TryParse<ShaderParamTypeG9>(ptype, out var pt);
                    var ps = new ShaderParamInfoG9();
                    ps.Name = phash;
                    ps.Type = pt;
                    switch (pt)
                    {
                        case ShaderParamTypeG9.Texture: 
                            ps.TextureIndex = (byte)Xml.GetIntAttribute(p, "index"); 
                            break;
                        case ShaderParamTypeG9.Unknown: 
                            ps.SamplerIndex = (byte)Xml.GetIntAttribute(p, "index"); 
                            break;
                        case ShaderParamTypeG9.Sampler: 
                            ps.SamplerIndex = (byte)Xml.GetIntAttribute(p, "index"); 
                            svdict[ps.SamplerIndex] = (byte)Xml.GetIntAttribute(p, "sampler"); 
                            break;
                        case ShaderParamTypeG9.CBuffer: 
                            ps.CBufferIndex = (byte)Xml.GetIntAttribute(p, "buffer"); 
                            ps.ParamLength = (ushort)Xml.GetUIntAttribute(p, "length"); 
                            ps.ParamOffset = (ushort)Xml.GetUIntAttribute(p, "offset");
                            break;
                    }
                    infos.Add(ps);

                    if ((phash != 0) && (phashold != 0))
                    {
                        dc.ParamsMapLegacyToGen9[phashold] = phash;
                        dc.ParamsMapGen9ToLegacy[phash] = phashold;
                    }

                }
                dc.ParamInfos = infos.ToArray();

                var scnt = 0;
                foreach (var kvp in svdict) if (kvp.Key >= scnt) scnt = kvp.Key + 1;
                var svals = new byte[scnt];
                foreach (var kvp in svdict) svals[kvp.Key] = kvp.Value;
                dc.SamplerValues = svals;

                dict[hash] = dc;
            }

            ShadersGen9ConversionData = dict;

        }
        public class ShaderGen9XmlDataCollection
        {
            public MetaHash Name;
            public int[] BufferSizes = [];
            public byte[] SamplerValues = [];
            public ShaderParamInfoG9[] ParamInfos = [];
            public Dictionary<MetaHash, MetaHash> ParamsMapGen9ToLegacy = new();
            public Dictionary<MetaHash, MetaHash> ParamsMapLegacyToGen9 = new();
        }
        private class ShaderXmlDataCollection
        {
            public MetaHash Name { get; set; }
            public Dictionary<MetaHash, int> FileNames { get; set; } = new Dictionary<MetaHash, int>();
            public Dictionary<byte, int> RenderBuckets { get; set; } = new Dictionary<byte, int>();
            public Dictionary<ShaderXmlVertexLayout, int> VertexLayouts { get; set; } = new Dictionary<ShaderXmlVertexLayout, int>();
            public Dictionary<MetaName, int> TexParams { get; set; } = new Dictionary<MetaName, int>();
            public Dictionary<MetaName, Dictionary<Vector4, int>> ValParams { get; set; } = new Dictionary<MetaName, Dictionary<Vector4, int>>();
            public Dictionary<MetaName, List<Vector4[]>> ArrParams { get; set; } = new Dictionary<MetaName, List<Vector4[]>>();
            public int GeomCount { get; set; } = 0;


            public void AddShaderUse(ShaderFX s, DrawableGeometry g)
            {
                GeomCount++;

                AddItem(s.FileName, FileNames);
                AddItem(s.RenderBucket, RenderBuckets);

                var info = g.VertexBuffer?.Info;
                if (info != null)
                {
                    AddItem(new ShaderXmlVertexLayout() { Flags = info.Flags, Types = info.Types }, VertexLayouts);
                }

                if (s.ParametersList?.Parameters == null) return;
                if (s.ParametersList?.Hashes == null) return;

                for (int i = 0; i < s.ParametersList.Count; i++)
                {
                    var h = s.ParametersList.Hashes[i];
                    var p = s.ParametersList.Parameters[i];

                    if (p.DataType == 0)//texture
                    {
                        AddItem(h, TexParams);
                    }
                    else if (p.DataType == 1)//vector
                    {
                        var vp = GetItem(h, ValParams);
                        if (p.Data is Vector4 vec)
                        {
                            AddItem(vec, vp);
                        }
                    }
                    else if (p.DataType > 1)//array
                    {
                        var ap = GetItem(h, ArrParams);
                        if (p.Data is Vector4[] arr)
                        {
                            bool found = false;
                            foreach (var exarr in ap)
                            {
                                if (exarr.Length != arr.Length) continue;
                                bool match = true;
                                for (int j = 0; j < exarr.Length; j++)
                                {
                                    if (exarr[j] != arr[j])
                                    {
                                        match = false;
                                        break;
                                    }
                                }
                                if (match)
                                {
                                    found = true;
                                    break;
                                }
                            }
                            if (!found)
                            {
                                ap.Add(arr);
                            }
                        }
                    }
                }

            }
            public void AddItem<T>(T t, Dictionary<T, int> d) where T : notnull
            {
                if (d.TryGetValue(t, out int count))
                {
                    d[t] = count + 1;
                }
                else
                {
                    d[t] = 1;
                }
            }
            public U GetItem<T, U>(T t, Dictionary<T, U> d) where T : notnull where U:new()
            {
                U? r = default(U);
                if (!d.TryGetValue(t, out r))
                {
                    r = new U();
                    d[t] = r;
                }
                return r;
            }
            public List<T> GetSortedList<T>(Dictionary<T, int> d) where T : notnull
            {
                // Consolidated: sort in-place and extract keys in single pass
                var result = new List<T>(d.Count);
                var kvps = new List<KeyValuePair<T, int>>(d);
                kvps.Sort((a, b) => b.Value.CompareTo(a.Value));
                foreach (var kvp in kvps)
                {
                    result.Add(kvp.Key);
                }
                return result;
            }
        }
        private struct ShaderXmlVertexLayout
        {
            public VertexDeclarationTypes Types { get; set; }
            public uint Flags { get; set; }
            public VertexType VertexType { get { return (VertexType)Flags; } }
            public override string ToString()
            {
                return Types.ToString() + ", " + VertexType.ToString();
            }
        }
    }


}
