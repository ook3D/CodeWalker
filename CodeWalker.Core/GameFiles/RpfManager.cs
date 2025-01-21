using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace CodeWalker.GameFiles
{
    public class RpfManager
    {
        //for caching and management of RPF file data.

        public string Folder { get; private set; }
        public string[] ExcludePaths { get; set; }
        public bool EnableMods { get; set; }
        public bool BuildExtendedJenkIndex { get; set; } = true;
        public Action<string> UpdateStatus { get; private set; }
        public Action<string> ErrorLog { get; private set; }

        public List<RpfFile> BaseRpfs { get; private set; }
        public List<RpfFile> ModRpfs { get; private set; }
        public List<RpfFile> DlcRpfs { get; private set; }
        public List<RpfFile> AllRpfs { get; private set; }
        public List<RpfFile> DlcNoModRpfs { get; private set; }
        public List<RpfFile> AllNoModRpfs { get; private set; }
        public Dictionary<string, RpfFile> RpfDict { get; private set; }
        public Dictionary<string, RpfEntry> EntryDict { get; private set; }
        public Dictionary<string, RpfFile> ModRpfDict { get; private set; }
        public Dictionary<string, RpfEntry> ModEntryDict { get; private set; }

        public volatile bool IsInited = false;

        public void Init(string folder, Action<string> updateStatus, Action<string> errorLog, bool rootOnly = false, bool buildIndex = true)
        {
            UpdateStatus = updateStatus;
            ErrorLog = errorLog;

            string replpath = folder + Path.DirectorySeparatorChar;
            var searchOption = rootOnly ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories;

            var allFiles = Directory.EnumerateFiles(folder, "*.rpf", searchOption);

            BaseRpfs = new List<RpfFile>();
            ModRpfs = new List<RpfFile>();
            DlcRpfs = new List<RpfFile>();
            AllRpfs = new List<RpfFile>();
            DlcNoModRpfs = new List<RpfFile>();
            AllNoModRpfs = new List<RpfFile>();
            RpfDict = new Dictionary<string, RpfFile>();
            EntryDict = new Dictionary<string, RpfEntry>();
            ModRpfDict = new Dictionary<string, RpfFile>();
            ModEntryDict = new Dictionary<string, RpfEntry>();

            Parallel.ForEach(allFiles, rpfPath =>
            {
                try
                {
                    var relativePath = rpfPath.Replace(replpath, "");
                    var rpfFile = new RpfFile(rpfPath, relativePath);

                    // Skip files in excluded paths
                    if (ExcludePaths?.Any(excluded => rpfFile.Path.StartsWith(excluded, StringComparison.OrdinalIgnoreCase)) == true)
                    {
                        return;
                    }

                    rpfFile.ScanStructure(updateStatus, errorLog);

                    if (rpfFile.LastException == null) //   incase of corrupted rpf (or renamed NG encrypted RPF)
                    {
                        lock (AllRpfs) // Ensure thread safety for shared collections
                        {
                            AddRpfFile(rpfFile, false, false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    errorLog($"{rpfPath}: {ex}");
                }
            });

            if (buildIndex)
            {
                updateStatus("Building jenkindex...");
                BuildBaseJenkIndex();
            }

            updateStatus("Scan complete");
            IsInited = true;
        }

        public void Init(List<RpfFile> allRpfs)
        {
            //fast init used by RPF explorer's File cache
            AllRpfs = allRpfs;

            BaseRpfs = new List<RpfFile>();
            ModRpfs = new List<RpfFile>();
            DlcRpfs = new List<RpfFile>();
            DlcNoModRpfs = new List<RpfFile>();
            AllNoModRpfs = new List<RpfFile>();
            RpfDict = new Dictionary<string, RpfFile>();
            EntryDict = new Dictionary<string, RpfEntry>();
            ModRpfDict = new Dictionary<string, RpfFile>();
            ModEntryDict = new Dictionary<string, RpfEntry>();
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
            ismod = ismod || (file.Path.StartsWith("mods\\"));

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
                    ModRpfDict[file.Path.Substring(5)] = file;
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
                                ModEntryDict[entry.Path.Substring(5)] = entry;
                            }
                            else
                            {
                                EntryDict[entry.Path] = entry;
                            }

                            if (entry is RpfFileEntry)
                            {
                                RpfFileEntry fentry = entry as RpfFileEntry;
                                entry.NameHash = JenkHash.GenHash(entry.NameLower);
                                int ind = entry.NameLower.LastIndexOf('.');
                                entry.ShortNameHash = (ind > 0) ? JenkHash.GenHash(entry.NameLower.Substring(0, ind)) : entry.NameHash;
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


        public RpfFile FindRpfFile(string path) => FindRpfFile(path, false);


        public RpfFile FindRpfFile(string path, bool exactPathOnly)
        {
            RpfFile file = null; //check the dictionary

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


        public RpfEntry GetEntry(string path)
        {
            RpfEntry entry;
            string pathl = path.ToLowerInvariant();
            if (EnableMods && ModEntryDict.TryGetValue(pathl, out entry))
            {
                return entry;
            }
            EntryDict.TryGetValue(pathl, out entry);
            if (entry == null)
            {
                pathl = pathl.Replace("/", "\\");
                pathl = pathl.Replace("common:", "common.rpf");
                if (EnableMods && ModEntryDict.TryGetValue(pathl, out entry))
                {
                    return entry;
                }
                EntryDict.TryGetValue(pathl, out entry);
            }
            return entry;
        }
        public byte[] GetFileData(string path)
        {
            byte[] data = null;
            RpfFileEntry entry = GetEntry(path) as RpfFileEntry;
            if (entry != null)
            {
                data = entry.File.ExtractFile(entry);
            }
            return data;
        }
        public string GetFileUTF8Text(string path)
        {
            byte[] bytes = GetFileData(path);
            return TextUtil.GetUTF8Text(bytes);
        }
        public XmlDocument GetFileXml(string path)
        {
            XmlDocument doc = new XmlDocument();
            string text = GetFileUTF8Text(path);
            if (!string.IsNullOrEmpty(text))
            {
                doc.LoadXml(text);
            }
            return doc;
        }

        public T GetFile<T>(string path) where T : class, PackedFile, new()
        {
            T file = null;
            byte[] data = null;
            RpfFileEntry entry = GetEntry(path) as RpfFileEntry;
            if (entry != null)
            {
                data = entry.File.ExtractFile(entry);
            }
            if (data != null)
            {
                file = new T();
                file.Load(data, entry);
            }
            return file;
        }
        public T GetFile<T>(RpfEntry e) where T : class, PackedFile, new()
        {
            T file = null;
            byte[] data = null;
            RpfFileEntry entry = e as RpfFileEntry;
            if (entry != null)
            {
                data = entry.File.ExtractFile(entry);
            }
            if (data != null)
            {
                file = new T();
                file.Load(data, entry);
            }
            return file;
        }
        public bool LoadFile<T>(T file, RpfEntry e) where T : class, PackedFile
        {
            byte[] data = null;
            RpfFileEntry entry = e as RpfFileEntry;
            if (entry != null)
            {
                data = entry.File.ExtractFile(entry);
            }
            if (data != null)
            {
                file.Load(data, entry);
                return true;
            }
            return false;
        }



        public void BuildBaseJenkIndex()
        {
            JenkIndex.Clear();
            HashSet<string> uniqueEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // Prevent duplicates
            StringBuilder sb = new StringBuilder();

            foreach (RpfFile file in AllRpfs)
            {
                try
                {
                    uniqueEntries.Add(file.Name);

                    foreach (RpfEntry entry in file.AllEntries)
                    {
                        string nlow = entry.NameLower;
                        if (string.IsNullOrEmpty(nlow)) continue;

                        ProcessEntryName(entry.Name, nlow, uniqueEntries);

                        if (BuildExtendedJenkIndex)
                        {
                            ProcessExtendedEntries(entry, nlow, file, sb, uniqueEntries);
                        }
                    }
                }
                catch
                {
                    // Log the error here for better debugging instead of silently failing
                }
            }

            // Add numbers 00 to 99
            for (int i = 0; i < 100; i++)
            {
                uniqueEntries.Add(i.ToString("00"));
            }

            // Commit all unique entries to JenkIndex
            foreach (var entry in uniqueEntries)
            {
                JenkIndex.Ensure(entry);
            }
        }

        private void ProcessEntryName(string name, string nlow, HashSet<string> uniqueEntries)
        {
            int ind = nlow.LastIndexOf('.');
            if (ind > 0)
            {
                uniqueEntries.Add(name.Substring(0, ind));
                uniqueEntries.Add(nlow.Substring(0, ind));
            }
            else
            {
                uniqueEntries.Add(name);
                uniqueEntries.Add(nlow);
            }
        }

        private void ProcessExtendedEntries(RpfEntry entry, string nlow, RpfFile file, StringBuilder sb, HashSet<string> uniqueEntries)
        {
            // Cache the length of `nlow` to avoid recalculating it multiple times
            int nlowLength = nlow.Length;

            if (nlow.EndsWith(".ydr", StringComparison.OrdinalIgnoreCase))
            {
                string baseName = nlow.Substring(0, nlowLength - 4);
                uniqueEntries.Add($"{baseName}_lod");
                uniqueEntries.Add($"{baseName}_loda");
                uniqueEntries.Add($"{baseName}_lodb");
            }
            else if (nlow.EndsWith(".ydd", StringComparison.OrdinalIgnoreCase))
            {
                ProcessYddEntries(nlow, uniqueEntries);
            }
            else if (nlow.EndsWith(".sps", StringComparison.OrdinalIgnoreCase))
            {
                uniqueEntries.Add(nlow);
            }
            else if (nlow.EndsWith(".awc", StringComparison.OrdinalIgnoreCase))
            {
                ProcessAwcEntries(entry, uniqueEntries);
            }
            else if (nlow.EndsWith(".nametable", StringComparison.OrdinalIgnoreCase) && entry is RpfBinaryFileEntry binEntry)
            {
                ProcessNametable(file, binEntry, sb, uniqueEntries);
            }
        }

        private void ProcessYddEntries(string nlow, HashSet<string> uniqueEntries)
        {
            if (nlow.EndsWith("_children.ydd"))
            {
                string strn = nlow.Substring(0, nlow.Length - 13);
                uniqueEntries.Add(strn);
                uniqueEntries.Add(strn + "_lod");
                uniqueEntries.Add(strn + "_loda");
                uniqueEntries.Add(strn + "_lodb");
            }

            int idx = nlow.LastIndexOf('_');
            if (idx > 0)
            {
                string str1 = nlow.Substring(0, idx);
                int idx2 = str1.LastIndexOf('_');
                if (idx2 > 0)
                {
                    string str2 = str1.Substring(0, idx2);
                    uniqueEntries.Add(str2 + "_lod");

                    for (int i = 1; i <= 100; i++)
                    {
                        string str3 = $"{str2}_{i:D2}_lod";
                        uniqueEntries.Add(str3);
                    }
                }
            }
        }

        private void ProcessAwcEntries(RpfEntry entry, HashSet<string> uniqueEntries)
        {
            string[] parts = entry.Path.Split('\\');
            if (parts.Length > 2)
            {
                string fn = parts[parts.Length - 1];
                string fd = parts[parts.Length - 2];
                string hpath = fn.Substring(0, fn.Length - 4);

                if (fd.EndsWith(".rpf"))
                {
                    fd = fd.Substring(0, fd.Length - 4);
                }
                hpath = $"{fd}/{hpath}";
                uniqueEntries.Add(hpath);
            }
        }

        private void ProcessNametable(RpfFile file, RpfBinaryFileEntry binfe, StringBuilder sb, HashSet<string> uniqueEntries)
        {
            if (binfe == null) return;

            byte[] data = file.ExtractFile(binfe);
            if (data == null) return;

            sb.Clear();
            foreach (byte c in data)
            {
                if (c == 0)
                {
                    string str = sb.ToString();
                    if (!string.IsNullOrEmpty(str))
                    {
                        uniqueEntries.Add(str.ToLowerInvariant());
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
}
