using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.IO.Enumeration;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace CodeWalker.GameFiles
{
    public class RpfManager
    {
        //for caching and management of RPF file data.

        public string Folder { get; private set; } = string.Empty;
        public string[] ExcludePaths { get; set; } = Array.Empty<string>();
        //extra folders of loose (unpacked) assets to load like a mods dlcpack, eg FiveM map resource folders
        public string[] ExtraFolders { get; set; } = Array.Empty<string>();
        public bool EnableMods { get; set; }
        public Action<string> UpdateStatus { get; private set; } = null!;
        public Action<string> ErrorLog { get; private set; } = null!;

        public List<RpfFile> BaseRpfs { get; private set; } = new();
        public List<RpfFile> ModRpfs { get; private set; } = new();
        public List<RpfFile> DlcRpfs { get; private set; } = new();
        public List<RpfFile> AllRpfs { get; private set; } = new();
        public List<RpfFile> DlcNoModRpfs { get; private set; } = new();
        public List<RpfFile> AllNoModRpfs { get; private set; } = new();
        public Dictionary<string, RpfFile> RpfDict { get; private set; } = new();
        public Dictionary<string, RpfEntry> EntryDict { get; private set; } = new();
        public Dictionary<string, RpfFile> ModRpfDict { get; private set; } = new();
        public Dictionary<string, RpfEntry> ModEntryDict { get; private set; } = new();
        public List<RpfFile> ExtraRpfs { get; private set; } = new(); //loose files found in ExtraFolders
        public int EscrowedFileCount { get; private set; } //loose files skipped for being FXAP (fivem asset escrow)

        public volatile bool IsInited = false;

        /// <summary>
        /// When set, Init(folder) builds the base JenkIndex on a background thread instead of inline. Name lookups
        /// (JenkIndex.GetString/TryGetString) wait until it has finished, so they never see a partial index.
        /// </summary>
        public bool BuildJenkIndexInBackground { get; set; }

        public static bool IsGen9 { get; set; } //not ideal for this to be static, but it's most convenient for ResourceData

        //enhanced (gen9) installs use "onigiri" as the mods folder instead of "mods"
        public static string ModsFolder => IsGen9 ? "onigiri" : "mods";
        public static string ModsFolderPrefix => IsGen9 ? "onigiri\\" : "mods\\";

        public void Init(string folder, bool gen9, Action<string> updateStatus, Action<string> errorLog, bool rootOnly = false, bool buildIndex = true)
        {
            UpdateStatus = updateStatus;
            ErrorLog = errorLog;
            IsGen9 = gen9;

            string replpath = folder + "\\";
            var sopt = rootOnly ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories;
            StartupTimer.Mark("RpfManager.Init: start");
            string[] allfiles = Directory.GetFiles(folder, "*.rpf", sopt);
            StartupTimer.Mark("RpfManager.Init: directory enumeration done (" + allfiles.Length + " rpfs)");

            BaseRpfs = new();
            ModRpfs = new();
            DlcRpfs = new();
            AllRpfs = new();
            DlcNoModRpfs = new();
            AllNoModRpfs = new();
            RpfDict = new();
            EntryDict = new();
            ModRpfDict = new();
            ModEntryDict = new();
            ExtraRpfs = new();
            EscrowedFileCount = 0;

            //Scan all archives in parallel - each ScanStructure opens its own file stream and only
            //touches that file's own state, so this is safe. Results are kept in original order so
            //the dictionary override semantics in AddRpfFile (last-write-wins) are unchanged.
            var scanned = new RpfFile[allfiles.Length];
            int scanDop = Environment.ProcessorCount;
            //CW_SCAN_DOP lets profiling/diagnostics force a specific scan parallelism (e.g. 1 = sequential).
            if (int.TryParse(Environment.GetEnvironmentVariable("CW_SCAN_DOP"), out int dopOverride) && dopOverride > 0)
            {
                scanDop = dopOverride;
            }
            Parallel.For(0, allfiles.Length,
                new ParallelOptions { MaxDegreeOfParallelism = scanDop },
                index =>
                {
                    string rpfpath = allfiles[index];
                    try
                    {
                        RpfFile rf = new(rpfpath, rpfpath.Replace(replpath, ""));

                        if (ExcludePaths != null)
                        {
                            for (int i = 0; i < ExcludePaths.Length; i++)
                            {
                                if (rf.Path.StartsWith(ExcludePaths[i]))
                                {
                                    return; //skip files in exclude paths.
                                }
                            }
                        }

                        rf.ScanStructure(updateStatus, errorLog);

                        if (rf.LastException != null) //incase of corrupted rpf (or renamed NG encrypted RPF)
                        {
                            return;
                        }

                        scanned[index] = rf;
                    }
                    catch (Exception ex)
                    {
                        errorLog(rpfpath + ": " + ex.ToString());
                    }
                });
            StartupTimer.Mark("RpfManager.Init: parallel ScanStructure done");

            //Add to the dictionaries sequentially, in the original discovery order, to preserve
            //deterministic override behaviour between base/mod/dlc archives.
            foreach (RpfFile rf in scanned)
            {
                if (rf == null) continue; //excluded, corrupted, or failed to scan
                AddRpfFile(rf, false, false);
            }
            StartupTimer.Mark("RpfManager.Init: serial AddRpfFile loop done");

            ScanLooseModFiles(folder);
            StartupTimer.Mark("RpfManager.Init: ScanLooseModFiles done");

            ScanExtraFolders(updateStatus);
            StartupTimer.Mark("RpfManager.Init: ScanExtraFolders done");

            if (buildIndex)
            {
                updateStatus("Building jenkindex...");
                if (BuildJenkIndexInBackground)
                {
                    JenkIndex.BuildInBackground(CollectBaseJenkIndexNames);
                    StartupTimer.Mark("RpfManager.Init: BuildBaseJenkIndex started in background");
                }
                else
                {
                    BuildBaseJenkIndex();
                    StartupTimer.Mark("RpfManager.Init: BuildBaseJenkIndex done");
                }
            }

            updateStatus("Scan complete");

            IsInited = true;
        }

        public void Init(List<RpfFile> allRpfs, bool gen9, Action<string>? updateStatus = null, Action<string>? errorLog = null)
        {
            //fast init used by RPF explorer's File cache
            StartupTimer.Mark("RpfManager.Init(list): start");
            UpdateStatus = updateStatus ?? (_ => { });
            ErrorLog = errorLog ?? (_ => { });
            AllRpfs = new(allRpfs);
            IsGen9 = gen9;

            BaseRpfs = new();
            ModRpfs = new();
            DlcRpfs = new();
            DlcNoModRpfs = new();
            AllNoModRpfs = new();
            RpfDict = new();
            EntryDict = new();
            ModRpfDict = new();
            ModEntryDict = new();
            ExtraRpfs = new();
            EscrowedFileCount = 0;
            foreach (var rpf in AllRpfs)
            {
                RpfDict[rpf.Path] = rpf;
                if (rpf.AllEntries == null) continue;
                foreach (var entry in rpf.AllEntries)
                {
                    EntryDict[entry.Path] = entry;
                }
            }

            //The explorer supplies its already-scanned archives, but FiveM resources are loose files
            //and therefore aren't in that list. Include the configured resource folders here as well,
            //so model previews can resolve external YTDs and other companion assets.
            StartupTimer.Mark("RpfManager.Init(list): dict fill done");
            ScanExtraFolders(UpdateStatus);
            StartupTimer.Mark("RpfManager.Init(list): ScanExtraFolders done");

            BuildBaseJenkIndex();
            StartupTimer.Mark("RpfManager.Init(list): BuildBaseJenkIndex done");

            IsInited = true;
        }


        private void AddRpfFile(RpfFile file, bool isdlc, bool ismod)
        {
            isdlc = isdlc || (file.NameLower == "update.rpf") || (file.NameLower.StartsWith("dlc") && file.NameLower.EndsWith(".rpf"));
            ismod = ismod || (file.Path.StartsWith(ModsFolderPrefix, StringComparison.OrdinalIgnoreCase));

            if (file.AllEntries != null)
            {
                AllRpfs.Add(file);
                if (!ismod)
                {
                    AllNoModRpfs.Add(file);
                }
                if (isdlc)
                {
                    DlcRpfs.Add(file);
                    if (!ismod)
                    {
                        DlcNoModRpfs.Add(file);
                    }
                }
                else
                {
                    if (ismod)
                    {
                        ModRpfs.Add(file);
                    }
                    else
                    {
                        BaseRpfs.Add(file);
                    }
                }
                if (ismod)
                {
                    ModRpfDict[file.Path.Substring(ModsFolderPrefix.Length)] = file;
                }

                RpfDict[file.Path] = file;

                foreach (RpfEntry entry in file.AllEntries)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(entry.Name))
                        {
                            if (ismod)
                            {
                                ModEntryDict[entry.Path] = entry;
                                ModEntryDict[entry.Path.Substring(ModsFolderPrefix.Length)] = entry;
                            }
                            else
                            {
                                EntryDict[entry.Path] = entry;
                            }

                            if (entry is RpfFileEntry)
                            {
                                entry.NameHash = JenkHash.GenHash(entry.NameLower);
                                int ind = entry.NameLower.LastIndexOf('.');
                                entry.ShortNameHash = ind > 0 ? JenkHash.GenHash(entry.NameLower.AsSpan(0, ind)) : entry.NameHash;
                                if (entry.ShortNameHash != 0)
                                {
                                    //EntryHashDict[entry.ShortNameHash] = entry;
                                }
                            }

                        }
                    }
                    catch (Exception ex)
                    {
                        file.LastError = ex.ToString();
                        file.LastException = ex;
                        ErrorLog(entry.Path + ": " + ex.ToString());
                    }
                }
            }

            if (file.Children != null)
            {
                foreach (RpfFile cfile in file.Children)
                {
                    AddRpfFile(cfile, isdlc, ismod);
                }
            }
        }


        //The enhanced mods folder (onigiri) is mostly loose, unpacked files rather than replacement rpfs.
        //Map each loose file onto the base game entry path(s) it overrides and register it in ModEntryDict,
        //so the existing EnableMods lookups pick it up like any other mods-folder entry.
        private void ScanLooseModFiles(string folder)
        {
            var modsdir = System.IO.Path.Combine(folder, ModsFolder);
            if (!Directory.Exists(modsdir)) return;

            //loose file name -> (path tail it overrides, entry). Names are rarely unique, hence the list.
            var byName = new Dictionary<string, List<(string tail, RpfFileEntry entry)>>();
            foreach (var f in Directory.GetFiles(modsdir, "*", SearchOption.AllDirectories))
            {
                if (f.EndsWith(".rpf", StringComparison.OrdinalIgnoreCase)) continue; //rpfs come from the main scan
                var rel = f.Substring(modsdir.Length + 1).ToLowerInvariant();
                var tail = rel;
                var sep = rel.IndexOf('\\');
                if (sep > 0)
                {
                    //strip the RAGE device folder (common:/ platform:/ dlcpacks:/) to get the in-rpf path
                    var dev = rel.Substring(0, sep);
                    if (dev == "common" || dev == "platform" || dev == "dlcpacks") tail = rel.Substring(sep + 1);
                }
                var entry = CreateLooseEntry(f, ModsFolderPrefix + rel, out bool escrowed);
                if (escrowed)
                {
                    EscrowedFileCount++;
                }
                if (entry?.File == null) continue;
                if (!byName.TryGetValue(entry.NameLower, out var list)) byName[entry.NameLower] = list = new();
                list.Add((tail, entry));
            }
            if (byName.Count == 0) return;

            foreach (var kvp in EntryDict)
            {
                if (kvp.Value is not RpfFileEntry e) continue;
                if (e.NameLower == null || !byName.TryGetValue(e.NameLower, out var cands)) continue;
                //dlc packs bring their own copies of these files - loose overrides only replace base/update content
                var top = e.File?.GetTopParent();
                if (top == null || top.Path.Contains("dlcpacks")) continue;
                var p = kvp.Key;
                foreach (var (tail, entry) in cands)
                {
                    if (p.Length > tail.Length && p[p.Length - tail.Length - 1] == '\\' && p.EndsWith(tail, StringComparison.Ordinal))
                    {
                        ModEntryDict[p] = entry;
                    }
                }
            }
        }

        //FiveM map resources are trees of loose (unpacked) assets rather than rpfs. Wrap each asset in a
        //LooseRpfFile and register it, so GameFileCache can fold them in like an extra dlcpack that wins
        //over everything else. Paths get a "fivem\" prefix to keep them out of the real game path space.
        private static readonly HashSet<string> ExtraFileExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".ymap", ".ytyp", ".ydr", ".ydd", ".yft", ".ytd", ".ybn", ".ynv", ".ycd", ".yed", ".ypt", ".ymf", ".ymt", ".awc" };

        private static readonly HashSet<string> ExtraExcludedFolders = new(StringComparer.OrdinalIgnoreCase)
        { ".git", ".github", ".githooks", ".claude", "[clothing]", "[peds]" };

        public const string ExtraFolderPrefix = "fivem\\";

        public static IEnumerable<string> EnumerateExtraFolderEntries(string root, bool includeDirectories = false)
        {
            return new FileSystemEnumerable<string>(root,
                (ref FileSystemEntry entry) => entry.ToFullPath(),
                new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0, IgnoreInaccessible = false })
            {
                ShouldIncludePredicate = (ref FileSystemEntry entry) => !entry.IsDirectory ||
                    (includeDirectories && !ExtraExcludedFolders.Contains(entry.FileName.ToString())),
                ShouldRecursePredicate = (ref FileSystemEntry entry) => !ExtraExcludedFolders.Contains(entry.FileName.ToString())
            };
        }

        //Same entries, in the same order, as EnumerateExtraFolderEntries(root, includeDirectories: true), with each
        //entry's kind from the enumeration itself.
        public static IEnumerable<(string path, bool isDirectory)> EnumerateExtraFolderEntryKinds(string root)
        {
            return new FileSystemEnumerable<(string path, bool isDirectory)>(root,
                (ref FileSystemEntry entry) => (entry.ToFullPath(), entry.IsDirectory),
                new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0, IgnoreInaccessible = false })
            {
                ShouldIncludePredicate = (ref FileSystemEntry entry) => !entry.IsDirectory ||
                    !ExtraExcludedFolders.Contains(entry.FileName.ToString()),
                ShouldRecursePredicate = (ref FileSystemEntry entry) => !ExtraExcludedFolders.Contains(entry.FileName.ToString())
            };
        }

        private void ScanExtraFolders(Action<string> updateStatus)
        {
            foreach (var extrafolder in ExtraFolders)
            {
                if (string.IsNullOrWhiteSpace(extrafolder)) continue;
                var root = extrafolder.Trim().TrimEnd('\\', '/');
                if (!Directory.Exists(root)) continue;

                updateStatus?.Invoke("Scanning " + root + "...");

                string[] files;
                try
                {
                    files = EnumerateExtraFolderEntries(root).ToArray();
                }
                catch (Exception ex)
                {
                    ErrorLog(root + ": " + ex.ToString());
                    continue;
                }

                var candidates = new List<string>(files.Length);
                foreach (var f in files)
                {
                    var name = System.IO.Path.GetFileName(f);
                    if (!ExtraFileExtensions.Contains(System.IO.Path.GetExtension(f)) &&
                        !name.EndsWith("gtxd.meta", StringComparison.OrdinalIgnoreCase) &&
                        !name.Equals("vehicles.meta", StringComparison.OrdinalIgnoreCase)) continue;
                    candidates.Add(f);
                }

                //Every candidate needs its 16-byte header read (FXAP escrow check, RSC7 flags). Resource folders hold
                //tens of thousands of files, so open them in parallel; registration below stays sequential in
                //enumeration order, so the entries and their override order are exactly as before.
                var entries = new RpfFileEntry?[candidates.Count];
                int escrowed = 0;
                Parallel.For(0, candidates.Count, i =>
                {
                    var f = candidates[i];
                    entries[i] = CreateLooseEntry(f, ExtraFolderPrefix + f.Substring(root.Length + 1).ToLowerInvariant(), out bool isEscrowed);
                    if (isEscrowed)
                    {
                        Interlocked.Increment(ref escrowed);
                    }
                });
                EscrowedFileCount += escrowed;

                foreach (var entry in entries)
                {
                    if (entry?.File == null) continue;
                    EntryDict[entry.Path] = entry;
                    RpfDict[entry.File.Path] = entry.File;
                    AllRpfs.Add(entry.File); //so the jenk index and the global asset dicts pick them up
                    ExtraRpfs.Add(entry.File);
                }
            }

            if (EscrowedFileCount > 0)
            {
                ErrorLog(EscrowedFileCount + " loose file(s) skipped: FXAP (FiveM asset escrow) content can't be read.");
            }
        }

        //Thread-safe (touches no manager state): ScanExtraFolders calls it in parallel.
        private static RpfFileEntry? CreateLooseEntry(string filepath, string relpath, out bool escrowed)
        {
            escrowed = false;
            try
            {
                var fileName = System.IO.Path.GetFileName(filepath);

                //resource files (ytd/ymt/etc) need a resource entry carrying the RSC7 page flags
                RpfFileEntry entry;
                long length;
                Span<byte> hdr = stackalloc byte[16];
                using (var handle = File.OpenHandle(filepath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    length = RandomAccess.GetLength(handle); //from the open handle - saves a separate path lookup per file
                    var magic = RandomAccess.Read(handle, hdr, 0) == 16 ? BitConverter.ToUInt32(hdr) : 0;
                    if (magic == 0x50415846) //FXAP - fivem asset escrow, the content is encrypted and unreadable
                    {
                        escrowed = true;
                        return null;
                    }
                    if (magic == 0x37435352) //RSC7
                    {
                        entry = new RpfResourceFileEntry
                        {
                            SystemFlags = BitConverter.ToUInt32(hdr.Slice(8)),
                            GraphicsFlags = BitConverter.ToUInt32(hdr.Slice(12)),
                            FileSize = (uint)length,
                        };
                    }
                    else
                    {
                        entry = new RpfBinaryFileEntry { FileSize = (uint)length, FileUncompressedSize = (uint)length };
                    }
                }

                var rpf = new LooseRpfFile(fileName, relpath, length) { FilePath = filepath };
                entry.File = rpf;
                entry.Name = fileName;
                entry.NameLower = entry.Name.ToLowerInvariant();
                entry.Path = relpath;
                entry.NameHash = JenkHash.GenHash(entry.NameLower);
                int ind = entry.NameLower.LastIndexOf('.');
                entry.ShortNameHash = ind > 0 ? JenkHash.GenHash(entry.NameLower.AsSpan(0, ind)) : entry.NameHash;
                rpf.Root = new RpfDirectoryEntry();
                rpf.AllEntries = new List<RpfEntry> { entry };
                return entry;
            }
            catch
            {
                return null; //unreadable/locked file - just skip it
            }
        }


        public RpfFile? FindRpfFile(string path) => FindRpfFile(path, false);


        public RpfFile? FindRpfFile(string path, bool exactPathOnly)
        {
            RpfFile? file = null; //check the dictionary

            if (EnableMods && ModRpfDict.TryGetValue(path, out file))
            {
                return file;
            }

            if (RpfDict.TryGetValue(path, out file))
            {
                return file;
            }

            string lpath = path.ToLowerInvariant(); //try look at names etc
            foreach (RpfFile tfile in AllRpfs)
            {
                if (!exactPathOnly && tfile.NameLower == lpath)
                {
                    return tfile;
                }
                if (tfile.Path == lpath)
                {
                    return tfile;
                }
            }

            return file;
        }


        public RpfEntry? GetEntry(string path)
        {
            return GetEntry(path, true);
        }

        public RpfEntry? GetEntry(string path, bool includeMods)
        {
            RpfEntry? entry;
            string pathl = path.ToLowerInvariant();
            if (includeMods && EnableMods && ModEntryDict.TryGetValue(pathl, out entry))
            {
                return entry;
            }
            EntryDict.TryGetValue(pathl, out entry);
            if (entry == null)
            {
                pathl = pathl.Replace("/", "\\");
                pathl = pathl.Replace("common:", "common.rpf");
                if (includeMods && EnableMods && ModEntryDict.TryGetValue(pathl, out entry))
                {
                    return entry;
                }
                EntryDict.TryGetValue(pathl, out entry);
            }
            return entry;
        }
        public byte[]? GetFileData(string path)
        {
            if (GetEntry(path) is RpfFileEntry entry)
            {
                return entry.File?.ExtractFile(entry);
            }
            return null;
        }
        public string GetFileUTF8Text(string path)
        {
            byte[]? bytes = GetFileData(path);
            return TextUtil.GetUTF8Text(bytes);
        }
        public XmlDocument GetFileXml(string path)
        {
            return GetFileXml(path, true);
        }

        public XmlDocument GetFileXml(string path, bool includeMods)
        {
            XmlDocument doc = new();
            var entry = GetEntry(path, includeMods) as RpfFileEntry;
            string? text = (entry == null) ? null : TextUtil.GetUTF8Text(entry.File?.ExtractFile(entry));
            if (!string.IsNullOrEmpty(text))
            {
                doc.LoadXml(text);
            }
            return doc;
        }

        public T? GetFile<T>(string path) where T : class, PackedFile, new()
        {
            if (GetEntry(path) is not RpfFileEntry entry)
            {
                return null;
            }
            
            byte[]? data = entry.File?.ExtractFile(entry);
            if (data == null)
            {
                return null;
            }
            
            T file = new();
            file.Load(data, entry);
            return file;
        }
        public T? GetFile<T>(RpfEntry e) where T : class, PackedFile, new()
        {
            if (e is not RpfFileEntry entry)
            {
                return null;
            }
            
            byte[]? data = entry.File?.ExtractFile(entry);
            if (data == null)
            {
                return null;
            }
            
            T file = new();
            file.Load(data, entry);
            return file;
        }
        public bool LoadFile<T>(T file, RpfEntry e) where T : class, PackedFile
        {
            if (e is not RpfFileEntry entry)
            {
                return false;
            }
            
            byte[]? data = entry.File?.ExtractFile(entry);
            if (data == null)
            {
                return false;
            }
            
            file.Load(data, entry);
            return true;
        }



        // Async file extraction methods
        public async Task<byte[]?> GetFileDataAsync(string path, CancellationToken cancellationToken = default)
        {
            byte[]? data = null;
            if (GetEntry(path) is RpfFileEntry entry)
            {
                if (entry.File != null)
                    data = await entry.File.ExtractFileAsync(entry, cancellationToken).ConfigureAwait(false);
            }
            return data;
        }

        public async Task<string?> GetFileUTF8TextAsync(string path, CancellationToken cancellationToken = default)
        {
            byte[]? bytes = await GetFileDataAsync(path, cancellationToken).ConfigureAwait(false);
            return TextUtil.GetUTF8Text(bytes);
        }

        public async Task<XmlDocument?> GetFileXmlAsync(string path, CancellationToken cancellationToken = default)
        {
            XmlDocument doc = new();
            string? text = await GetFileUTF8TextAsync(path, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(text))
            {
                doc.LoadXml(text);
            }
            return doc;
        }

        public async Task<T?> GetFileAsync<T>(string path, CancellationToken cancellationToken = default) where T : class, PackedFile, new()
        {
            T? file = null;
            byte[]? data = null;
            RpfFileEntry? entry = null;
            if (GetEntry(path) is RpfFileEntry e)
            {
                entry = e;
                if (entry.File != null)
                    data = await entry.File.ExtractFileAsync(entry, cancellationToken).ConfigureAwait(false);
            }
            if (data != null && entry != null)
            {
                file = new T();
                file.Load(data, entry);
            }
            return file;
        }

        public async Task<T?> GetFileAsync<T>(RpfEntry e, CancellationToken cancellationToken = default) where T : class, PackedFile, new()
        {
            T? file = null;
            byte[]? data = null;
            RpfFileEntry? entry = null;
            if (e is RpfFileEntry ent)
            {
                entry = ent;
                if (entry.File != null)
                    data = await entry.File.ExtractFileAsync(entry, cancellationToken).ConfigureAwait(false);
            }
            if (data != null && entry != null)
            {
                file = new T();
                file.Load(data, entry);
            }
            return file;
        }

        public async Task<bool> LoadFileAsync<T>(T file, RpfEntry e, CancellationToken cancellationToken = default) where T : class, PackedFile
        {
            byte[]? data = null;
            RpfFileEntry? entry = null;
            if (e is RpfFileEntry ent)
            {
                entry = ent;
                if (entry.File != null)
                    data = await entry.File.ExtractFileAsync(entry, cancellationToken).ConfigureAwait(false);
            }
            if (data != null && entry != null)
            {
                file.Load(data, entry);
                return true;
            }
            return false;
        }

        public void BuildBaseJenkIndex()
        {
            JenkIndex.Clear();
            var parts = CollectBaseJenkIndexNames();
            int total = 0;
            foreach (var list in parts)
            {
                total += list.Count;
            }
            JenkIndex.EnsureCapacity(total);
            foreach (var list in parts)
            {
                JenkIndex.EnsureRange(list);
            }
            StartupTimer.Mark("BuildBaseJenkIndex: index filled");
        }

        //The base index's names in the order a sequential build adds them (the first name wins a hash collision):
        //every archive's names in archive order, then "00".."99", then strings.dat/strings.txt.
        //Reads only AllRpfs and the archives, so it can run on any thread.
        private List<IReadOnlyList<KeyValuePair<uint, string>>> CollectBaseJenkIndexNames()
        {
            //strings.dat (millions of names) is read, decompressed and hashed on another thread while the archive
            //entries are processed below; its names are still added last, as before.
            var stringsTask = Task.Run(LoadJenkStringsFile);

            //Each archive's names are hashed in parallel into its own list, then merged in archive order. Merging in
            //order gives the same index as a sequential build (the first name wins a hash collision), every run.
            var rpfs = AllRpfs;
            var perRpf = new List<KeyValuePair<uint, string>>?[rpfs.Count];
            Parallel.For(0, rpfs.Count, rpfIndex =>
                {
                    var file = rpfs[rpfIndex];
                    var localIndex = new List<KeyValuePair<uint, string>>();
                    void Ens(string s)
                    {
                        uint h = JenkHash.GenHash(s);
                        if (h != 0)
                        {
                            localIndex.Add(new KeyValuePair<uint, string>(h, s));
                        }
                    }
                    try
                    {
                        //an archive can hold many name tables (one is known to hold millions of names): extract and
                        //decode them in parallel up front, then add each one's names at its place in the entry order
                        Dictionary<RpfEntry, List<KeyValuePair<uint, string>>?>? nameTables = null;
                        var tableEntries = new List<RpfBinaryFileEntry>();
                        foreach (RpfEntry entry in file.AllEntries)
                        {
                            var nlow = entry.NameLower;
                            if (!string.IsNullOrEmpty(nlow) && nlow.EndsWith(".nametable") && (entry is RpfBinaryFileEntry binfe))
                            {
                                tableEntries.Add(binfe);
                            }
                        }
                        if (tableEntries.Count > 0)
                        {
                            var tableNames = new List<KeyValuePair<uint, string>>?[tableEntries.Count];
                            Parallel.For(0, tableEntries.Count, t =>
                            {
                                byte[]? data = file.ExtractFile(tableEntries[t]); //never throws - returns null on failure
                                if (data != null)
                                {
                                    var names = new List<KeyValuePair<uint, string>>();
                                    AddNameTableNames(data, names);
                                    tableNames[t] = names;
                                }
                            });
                            nameTables = new Dictionary<RpfEntry, List<KeyValuePair<uint, string>>?>(tableEntries.Count);
                            for (int t = 0; t < tableEntries.Count; t++)
                            {
                                nameTables[tableEntries[t]] = tableNames[t];
                            }
                        }

                        Ens(file.Name);
                        foreach (RpfEntry entry in file.AllEntries)
                        {
                            var nlow = entry.NameLower;
                            if (string.IsNullOrEmpty(nlow)) continue;
                            //Ens(entry.Name);
                            //Ens(nlow);
                            int ind = nlow.LastIndexOf('.');
                            if (ind > 0)
                            {
                                Ens(entry.Name.Substring(0, ind));
                                Ens(nlow.Substring(0, ind));
                            }
                            else
                            {
                                Ens(entry.Name);
                                Ens(nlow);
                            }
                            if (nlow.EndsWith(".sps"))
                            {
                                Ens(nlow);//for shader preset filename hashes!
                            }
                            if (nlow.EndsWith(".awc")) //create audio container path hashes...
                            {
                                string[] parts = entry.Path.Split('\\');
                                int pl = parts.Length;
                                if (pl > 2)
                                {
                                    string fn = parts[pl - 1];
                                    string fd = parts[pl - 2];
                                    string hpath = fn.Substring(0, fn.Length - 4);
                                    if (fd.EndsWith(".rpf"))
                                    {
                                        fd = fd.Substring(0, fd.Length - 4);
                                    }
                                    hpath = fd + "/" + hpath;
                                    if (parts[pl - 3] != "sfx")
                                    { }//no hit

                                    Ens(hpath);
                                }
                            }
                            if (nlow.EndsWith(".nametable"))
                            {
                                if ((nameTables != null) && nameTables.TryGetValue(entry, out var names) && (names != null))
                                {
                                    localIndex.AddRange(names);
                                }
                            }
                        }
                    }
                    catch
                    {
                        //failing silently!! not so good really
                    }
                    perRpf[rpfIndex] = localIndex;
                });
            StartupTimer.Mark("BuildBaseJenkIndex: parallel pass done");

            var parts = new List<IReadOnlyList<KeyValuePair<uint, string>>>(perRpf.Length + 2);
            foreach (var list in perRpf)
            {
                if (list != null)
                {
                    parts.Add(list);
                }
            }

            var numbers = new List<KeyValuePair<uint, string>>(100);
            for (int i = 0; i < 100; i++)
            {
                var str = i.ToString("00");
                numbers.Add(new KeyValuePair<uint, string>(JenkHash.GenHash(str), str));
            }
            parts.Add(numbers);

            try
            {
                parts.Add(stringsTask.Result);
            }
            catch (Exception ex)
            {
                //only the strings file is lost - the archive names above are still indexed
                ErrorLog("Error loading the Jenkins hash strings file: " + (ex is AggregateException ae ? ae.GetBaseException() : ex));
            }
            StartupTimer.Mark("BuildBaseJenkIndex: strings file loaded");
            return parts;
        }

        //A .nametable is a list of zero-terminated single-byte names; each one is indexed lower-cased. Some archives
        //carry millions of them, so the table is split into chunks at terminators and decoded in parallel, then
        //appended in table order. Same output as decoding it byte by byte: each byte maps to the char of the same
        //value (Latin-1), invariant lower-casing is per char, empty names and a trailing unterminated name are skipped.
        private static void AddNameTableNames(byte[] data, List<KeyValuePair<uint, string>> output)
        {
            int chunkCount = Math.Max(1, Math.Min(Environment.ProcessorCount * 4, data.Length / (64 * 1024)));
            var bounds = new int[chunkCount + 1];
            for (int c = 1; c < chunkCount; c++)
            {
                int pos = Math.Max(bounds[c - 1], (int)((long)data.Length * c / chunkCount));
                int z = (pos < data.Length) ? Array.IndexOf(data, (byte)0, pos) : -1;
                bounds[c] = (z < 0) ? data.Length : z + 1; //each chunk starts right after a terminator
            }
            bounds[chunkCount] = data.Length;

            var parts = new List<KeyValuePair<uint, string>>[chunkCount];
            Parallel.For(0, chunkCount, c =>
            {
                var part = new List<KeyValuePair<uint, string>>();
                int start = bounds[c];
                int end = bounds[c + 1];
                while (start < end)
                {
                    int z = Array.IndexOf(data, (byte)0, start, end - start);
                    if (z < 0)
                    {
                        break; //no terminator: the old decoder never emitted a trailing unterminated name
                    }
                    int len = z - start;
                    if (len > 0)
                    {
                        string strl = string.Create(len, (data, start), static (dst, st) =>
                        {
                            var src = st.data.AsSpan(st.start, dst.Length);
                            for (int i = 0; i < dst.Length; i++)
                            {
                                dst[i] = char.ToLowerInvariant((char)src[i]);
                            }
                        });
                        uint h = JenkHash.GenHash(strl);
                        if (h != 0)
                        {
                            part.Add(new KeyValuePair<uint, string>(h, strl));
                        }
                    }
                    start = z + 1;
                }
                parts[c] = part;
            });
            foreach (var part in parts)
            {
                output.AddRange(part);
            }
        }

        //strings.dat (gzip) or strings.txt next to the exe, as (hash, name) pairs in file order - blank lines,
        //comment lines and zero hashes dropped, the same filtering JenkIndex.Ensure applied to each line.
        private static List<KeyValuePair<uint, string>> LoadJenkStringsFile()
        {
            var dir = AppContext.BaseDirectory;

            // Try strings.dat (gzip compressed) first, fallback to strings.txt
            var datPath = Path.Combine(dir, "strings.dat");
            var txtPath = Path.Combine(dir, "strings.txt");

            if (File.Exists(datPath))
            {
                try
                {
                    byte[] text;
                    using (var fileStream = File.OpenRead(datPath))
                    using (var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
                    using (var ms = new MemoryStream())
                    {
                        gzipStream.CopyTo(ms);
                        text = ms.ToArray();
                    }
                    var parsed = ParseJenkStringLines(text);
                    if (parsed != null)
                    {
                        return parsed;
                    }
                    //unusual encoding marker: let StreamReader handle it exactly as before
                    using (var reader = new StreamReader(new MemoryStream(text), Encoding.UTF8))
                    {
                        var linesList = new List<string>();
                        string? line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            linesList.Add(line);
                        }
                        return FilterJenkStringLines(linesList);
                    }
                }
                catch
                {
                    // If decompression fails, fall back to strings.txt
                }
            }

            if (File.Exists(txtPath))
            {
                return FilterJenkStringLines(File.ReadAllLines(txtPath));
            }
            return new List<KeyValuePair<uint, string>>();
        }

        private static List<KeyValuePair<uint, string>> FilterJenkStringLines(IEnumerable<string?> lines)
        {
            var result = new List<KeyValuePair<uint, string>>();
            foreach (var line in lines)
            {
                var str = line?.Trim();
                if (string.IsNullOrEmpty(str))
                {
                    continue;
                }
                if (str.StartsWith("//"))
                {
                    continue;
                }
                uint hash = JenkHash.GenHash(str);
                if (hash == 0)
                {
                    continue;
                }
                result.Add(new KeyValuePair<uint, string>(hash, str));
            }
            return result;
        }

        //Splits UTF-8 text into lines like StreamReader.ReadLine (\r, \n and \r\n all end a line; a UTF-8 byte order
        //mark is skipped) and filters/hashes them like FilterJenkStringLines - in parallel chunks, results in line
        //order. Line breaks are ASCII bytes that never occur inside a multi-byte UTF-8 sequence, so decoding line by
        //line gives the same strings as decoding the whole stream. Returns null for UTF-16/32 byte order marks.
        private static List<KeyValuePair<uint, string>>? ParseJenkStringLines(byte[] text)
        {
            int begin = 0;
            if ((text.Length >= 3) && (text[0] == 0xEF) && (text[1] == 0xBB) && (text[2] == 0xBF))
            {
                begin = 3;
            }
            else if ((text.Length >= 2) && (((text[0] == 0xFF) && (text[1] == 0xFE)) || ((text[0] == 0xFE) && (text[1] == 0xFF))))
            {
                return null;
            }

            int chunkCount = Math.Max(1, Math.Min(Environment.ProcessorCount * 4, (text.Length - begin) / (256 * 1024)));
            var bounds = new int[chunkCount + 1];
            bounds[0] = begin;
            for (int c = 1; c < chunkCount; c++)
            {
                int pos = Math.Max(bounds[c - 1], begin + (int)((long)(text.Length - begin) * c / chunkCount));
                int nl = (pos < text.Length) ? Array.IndexOf(text, (byte)'\n', pos) : -1;
                bounds[c] = (nl < 0) ? text.Length : nl + 1; //chunks start right after a \n, so no line is split
            }
            bounds[chunkCount] = text.Length;

            var parts = new List<KeyValuePair<uint, string>>[chunkCount];
            Parallel.For(0, chunkCount, c =>
            {
                var part = new List<KeyValuePair<uint, string>>();
                int start = bounds[c];
                int end = bounds[c + 1];
                while (start < end)
                {
                    int stop = text.AsSpan(start, end - start).IndexOfAny((byte)'\r', (byte)'\n');
                    int lineEnd = (stop < 0) ? end : start + stop;
                    if (lineEnd > start)
                    {
                        var str = Encoding.UTF8.GetString(text, start, lineEnd - start).Trim();
                        if ((str.Length > 0) && !str.StartsWith("//"))
                        {
                            uint hash = JenkHash.GenHash(str);
                            if (hash != 0)
                            {
                                part.Add(new KeyValuePair<uint, string>(hash, str));
                            }
                        }
                    }
                    start = lineEnd + 1; //empty segments (eg between \r and \n) are blank lines, which are skipped
                }
                parts[c] = part;
            });

            int total = 0;
            foreach (var part in parts)
            {
                total += part.Count;
            }
            var result = new List<KeyValuePair<uint, string>>(total);
            foreach (var part in parts)
            {
                result.AddRange(part);
            }
            return result;
        }

    }
}
