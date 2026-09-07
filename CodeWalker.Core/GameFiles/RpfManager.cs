using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
            string[] allfiles = Directory.GetFiles(folder, "*.rpf", sopt);

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

            //Add to the dictionaries sequentially, in the original discovery order, to preserve
            //deterministic override behaviour between base/mod/dlc archives.
            foreach (RpfFile rf in scanned)
            {
                if (rf == null) continue; //excluded, corrupted, or failed to scan
                AddRpfFile(rf, false, false);
            }

            ScanLooseModFiles(folder);

            ScanExtraFolders(updateStatus);

            if (buildIndex)
            {
                updateStatus("Building jenkindex...");
                BuildBaseJenkIndex();
            }

            updateStatus("Scan complete");

            IsInited = true;
        }

        public void Init(List<RpfFile> allRpfs, bool gen9)
        {
            //fast init used by RPF explorer's File cache
            AllRpfs = allRpfs;
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
            foreach (var rpf in allRpfs)
            {
                RpfDict[rpf.Path] = rpf;
                if (rpf.AllEntries == null) continue;
                foreach (var entry in rpf.AllEntries)
                {
                    EntryDict[entry.Path] = entry;
                }
            }

            BuildBaseJenkIndex();

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
                var entry = CreateLooseEntry(f, ModsFolderPrefix + rel);
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

        public const string ExtraFolderPrefix = "fivem\\";

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
                    files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
                }
                catch (Exception ex)
                {
                    ErrorLog(root + ": " + ex.ToString());
                    continue;
                }

                foreach (var f in files)
                {
                    if (!ExtraFileExtensions.Contains(System.IO.Path.GetExtension(f))) continue;
                    var entry = CreateLooseEntry(f, ExtraFolderPrefix + f.Substring(root.Length + 1).ToLowerInvariant());
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

        private RpfFileEntry? CreateLooseEntry(string filepath, string relpath)
        {
            try
            {
                var fi = new FileInfo(filepath);
                var rpf = new LooseRpfFile(fi.Name, relpath, fi.Length) { FilePath = filepath };

                //resource files (ytd/ymt/etc) need a resource entry carrying the RSC7 page flags
                RpfFileEntry entry;
                Span<byte> hdr = stackalloc byte[16];
                using (var fs = fi.OpenRead())
                {
                    var magic = fs.Read(hdr) == 16 ? BitConverter.ToUInt32(hdr) : 0;
                    if (magic == 0x50415846) //FXAP - fivem asset escrow, the content is encrypted and unreadable
                    {
                        EscrowedFileCount++;
                        return null;
                    }
                    if (magic == 0x37435352) //RSC7
                    {
                        entry = new RpfResourceFileEntry
                        {
                            SystemFlags = BitConverter.ToUInt32(hdr.Slice(8)),
                            GraphicsFlags = BitConverter.ToUInt32(hdr.Slice(12)),
                            FileSize = (uint)fi.Length,
                        };
                    }
                    else
                    {
                        entry = new RpfBinaryFileEntry { FileSize = (uint)fi.Length, FileUncompressedSize = (uint)fi.Length };
                    }
                }

                entry.File = rpf;
                entry.Name = fi.Name;
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

            //Build the index in parallel: each RPF is hashed into a thread-local dictionary (no shared
            //lock on the hot path), then bulk-merged into the global JenkIndex once per partition. The
            //string-building and hashing - the expensive part - all run concurrently.
            Parallel.ForEach(AllRpfs,
                () => new Dictionary<uint, string>(),
                (file, state, localIndex) =>
                {
                    void Ens(string s)
                    {
                        uint h = JenkHash.GenHash(s);
                        if (h != 0) localIndex.TryAdd(h, s);
                    }
                    var sb = new StringBuilder();
                    try
                    {
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
                                RpfBinaryFileEntry? binfe = entry as RpfBinaryFileEntry;
                                if (binfe != null)
                                {
                                    byte[]? data = file.ExtractFile(binfe);
                                    if (data != null)
                                    {
                                        sb.Clear();
                                        for (int i = 0; i < data.Length; i++)
                                        {
                                            byte c = data[i];
                                            if (c == 0)
                                            {
                                                string str = sb.ToString();
                                                if (!string.IsNullOrEmpty(str))
                                                {
                                                    string strl = str.ToLowerInvariant();
                                                    //Ens(str);
                                                    Ens(strl);
                                                }
                                                sb.Clear();
                                            }
                                            else
                                            {
                                                sb.Append((char)c);
                                            }
                                        }
                                    }
                                }
                                else
                                { }
                            }
                        }
                    }
                    catch
                    {
                        //failing silently!! not so good really
                    }
                    return localIndex;
                },
                localIndex => JenkIndex.EnsureRange(localIndex));

            for (int i = 0; i < 100; i++)
            {
                JenkIndex.Ensure(i.ToString("00"));
            }


            var path = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var dir = Path.GetDirectoryName(path) ?? AppContext.BaseDirectory;
            
            // Try strings.dat (gzip compressed) first, fallback to strings.txt
            var datPath = Path.Combine(dir, "strings.dat");
            var txtPath = Path.Combine(dir, "strings.txt");
            
            string[]? lines = null;
            
            if (File.Exists(datPath))
            {
                try
                {
                    using (var fileStream = File.OpenRead(datPath))
                    using (var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
                    using (var reader = new StreamReader(gzipStream, Encoding.UTF8))
                    {
                        var linesList = new List<string>();
                        string? line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            linesList.Add(line);
                        }
                        lines = linesList.ToArray();
                    }
                }
                catch
                {
                    // If decompression fails, fall back to strings.txt
                    lines = null;
                }
            }
            
            if (lines == null && File.Exists(txtPath))
            {
                lines = File.ReadAllLines(txtPath);
            }
            
            if (lines != null)
            {
                foreach (var line in lines)
                {
                    var str = line?.Trim();
                    if (string.IsNullOrEmpty(str)) continue;
                    if (str.StartsWith("//")) continue;
                    JenkIndex.Ensure(str);
                }
            }

        }

    }
}
