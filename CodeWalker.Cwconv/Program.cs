using CodeWalker.GameFiles;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace CodeWalker.Cwconv;

/// <summary>
/// Headless CodeWalker converter: binary GTA V assets &lt;-&gt; CodeWalker XML, plus RPF listing/extraction.
/// </summary>
static class Program
{
    private const string Usage =
        "cwconv - headless CodeWalker converter (loose files and GTA V RPF archives)\n" +
        "\n" +
        "  cwconv tox <file> [<outdir>]       binary -> CodeWalker XML (<outdir>\\<file>.xml, textures in <outdir>\\<name>\\)\n" +
        "  cwconv frxml <file.xml> [<out>]    CodeWalker XML -> binary (<out> may be a file or a folder)\n" +
        "  cwconv ybnbounds <listfile>        one line per .ybn path in <listfile>: path|minx,miny,minz|maxx,maxy,maxz|type\n" +
        "  cwconv rpfls <pattern> [--game <dir>]\n" +
        "                                     list RPF file entries whose full path matches: <rpf path>|<size>\n" +
        "  cwconv rpfx <pattern> <outdir> [--game <dir>] [--tox] [--flat]\n" +
        "                                     extract matching RPF entries as loose files (resources keep their RSC7 header);\n" +
        "                                     update.rpf / dlc copies win over base copies; --flat = one folder, winners only;\n" +
        "                                     --tox also writes CodeWalker XML next to each file\n" +
        "\n" +
        "<pattern> is a case-insensitive substring of the entry path, '*' matches any run of characters.\n" +
        "--game <dir> or CW_GTA_ROOT=<dir> sets the GTA V install folder.\n" +
        "CW_NAMES_DIR=<folder> registers every file name under <folder> so matching hashes print as names.\n" +
        "Exit codes: 0 ok, 1 conversion failed, 2 bad usage.";

    private static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        if (args.Length < 2)
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        RegisterNames(Environment.GetEnvironmentVariable("CW_NAMES_DIR"));

        try
        {
            switch (args[0].ToLowerInvariant())
            {
                case "tox":
                    return ToXml(args[1], args.Length > 2 ? args[2] : null);
                case "frxml":
                    return FromXml(args[1], args.Length > 2 ? args[2] : null);
                case "ybnbounds":
                    return YbnBounds(args[1]);
                case "rpfls":
                    return RpfList(args);
                case "rpfx":
                    return RpfExtract(args);
                default:
                    Console.Error.WriteLine(Usage);
                    return 2;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message);
            return 1;
        }
    }

    // Registers every file name under the folder in the hash->name index so hashes print as names.
    private static void RegisterNames(string? dir)
    {
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(file).ToLowerInvariant();
            JenkIndex.Ensure(name);
            JenkIndex.Ensure(Path.GetFileNameWithoutExtension(name));
        }
    }

    // Wraps a loose file in a throwaway RPF entry so the Core converters can be used on it.
    private static RpfFileEntry CreateFileEntry(string path, ref byte[] data)
    {
        var name = Path.GetFileName(path);
        RpfFileEntry entry;
        var magic = data.Length > 4 ? BitConverter.ToUInt32(data, 0) : 0;
        if (magic == 0x37435352) // RSC7
        {
            entry = RpfFile.CreateResourceFileEntry(ref data, 0);
            data = ResourceBuilder.Decompress(data);
        }
        else
        {
            var binEntry = new RpfBinaryFileEntry();
            binEntry.FileSize = (uint)data.Length;
            binEntry.FileUncompressedSize = binEntry.FileSize;
            entry = binEntry;
        }
        entry.Name = name;
        entry.NameLower = name.ToLowerInvariant();
        entry.NameHash = JenkHash.GenHash(entry.NameLower);
        entry.ShortNameHash = JenkHash.GenHash(Path.GetFileNameWithoutExtension(entry.NameLower));
        entry.Path = path;
        return entry;
    }

    private static (List<string> positional, string? game, bool tox, bool flat) ParseArgs(string[] args)
    {
        var positional = new List<string>();
        string? game = null;
        var tox = false;
        var flat = false;
        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--game":
                    game = i + 1 < args.Length ? args[++i] : null;
                    break;
                case "--tox":
                    tox = true;
                    break;
                case "--flat":
                    flat = true;
                    break;
                default:
                    positional.Add(args[i]);
                    break;
            }
        }
        return (positional, game, tox, flat);
    }

    private static RpfManager? OpenArchives(string? game)
    {
        game ??= Environment.GetEnvironmentVariable("CW_GTA_ROOT");
        if (string.IsNullOrEmpty(game) || !Directory.Exists(game))
        {
            Console.Error.WriteLine("game folder not found (use --game <dir> or CW_GTA_ROOT): " + game);
            return null;
        }
        game = Path.GetFullPath(game).TrimEnd('\\', '/');

        Console.Error.WriteLine("loading keys from " + game);
        GTA5Keys.LoadFromPath(game, false, null);

        var lastStatus = DateTime.MinValue;
        var manager = new RpfManager();
        manager.Init(game, false,
            status =>
            {
                if (DateTime.UtcNow - lastStatus > TimeSpan.FromSeconds(2))
                {
                    lastStatus = DateTime.UtcNow;
                    Console.Error.WriteLine(status);
                }
            },
            error => Console.Error.WriteLine("warn: " + error),
            false, false);
        Console.Error.WriteLine("scanned " + manager.AllRpfs.Count.ToString() + " archives");
        return manager;
    }

    // '*' matches any run of characters; everything else is a literal, case-insensitive substring.
    private static Regex CompilePattern(string pattern)
    {
        return new Regex(string.Join(".*", pattern.Split('*').Select(Regex.Escape)),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    // Base archives first, then update.rpf copies, then the other dlc copies: later entries win.
    private static List<RpfFile> PriorityOrder(RpfManager manager)
    {
        var dlc = new HashSet<RpfFile>(manager.DlcRpfs);
        int Rank(RpfFile rpf)
        {
            if (!dlc.Contains(rpf))
            {
                return 0;
            }
            return (rpf.Path ?? string.Empty).StartsWith("update\\", StringComparison.OrdinalIgnoreCase) ? 1 : 2;
        }
        return manager.AllNoModRpfs.OrderBy(Rank).ToList();
    }

    private static List<RpfFileEntry> FindEntries(RpfManager manager, string pattern)
    {
        var regex = CompilePattern(pattern);
        var result = new List<RpfFileEntry>();
        foreach (var rpf in PriorityOrder(manager))
        {
            if (rpf.AllEntries == null)
            {
                continue;
            }
            foreach (var entry in rpf.AllEntries)
            {
                if (entry is RpfFileEntry fileEntry && regex.IsMatch(fileEntry.Path))
                {
                    result.Add(fileEntry);
                }
            }
        }
        return result;
    }

    private static int RpfList(string[] args)
    {
        var (positional, game, _, _) = ParseArgs(args);
        if (positional.Count < 1)
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        var manager = OpenArchives(game);
        if (manager == null)
        {
            return 1;
        }

        foreach (var entry in FindEntries(manager, positional[0]))
        {
            Console.WriteLine(entry.Path + "|" + entry.GetFileSize().ToString());
        }
        return 0;
    }

    private static int RpfExtract(string[] args)
    {
        var (positional, game, tox, flat) = ParseArgs(args);
        if (positional.Count < 2)
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        var outDir = Path.GetFullPath(positional[1]);
        var manager = OpenArchives(game);
        if (manager == null)
        {
            return 1;
        }

        var entries = FindEntries(manager, positional[0]);
        var winners = new Dictionary<string, RpfFileEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            winners[entry.Name] = entry;
        }
        foreach (var group in entries.GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            Console.Error.WriteLine("duplicate " + group.Key + ": winner " + winners[group.Key].Path + ", shadowed " +
                string.Join(", ", group.Where(e => e != winners[group.Key]).Select(e => e.Path)));
        }

        Directory.CreateDirectory(outDir);
        var failures = 0;
        foreach (var entry in flat ? winners.Values.ToList() : entries)
        {
            try
            {
                var data = entry.File?.ExtractFile(entry) ?? throw new InvalidDataException("extract returned no data");
                if (entry is RpfResourceFileEntry resourceEntry)
                {
                    data = ResourceBuilder.AddResourceHeader(resourceEntry, ResourceBuilder.Compress(data))!;
                }

                var path = flat ? Path.Combine(outDir, entry.Name) : Path.Combine(outDir, entry.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, data);
                Console.WriteLine(path);

                if (tox && ToXml(path, Path.GetDirectoryName(path)) != 0)
                {
                    failures++;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("failed " + entry.Path + ": " + ex.GetType().Name + ": " + ex.Message);
                failures++;
            }
        }
        return failures > 0 ? 1 : 0;
    }

    private static int ToXml(string file, string? outDir)
    {
        file = Path.GetFullPath(file);
        if (!File.Exists(file))
        {
            Console.Error.WriteLine("file not found: " + file);
            return 1;
        }

        outDir = Path.GetFullPath(outDir ?? Path.GetDirectoryName(file)!);
        Directory.CreateDirectory(outDir);

        var data = File.ReadAllBytes(file);
        var xml = MetaXml.GetXml(CreateFileEntry(file, ref data), data, out var xmlName, outDir);
        if (string.IsNullOrEmpty(xml))
        {
            Console.Error.WriteLine("no XML conversion for: " + Path.GetFileName(file));
            return 1;
        }

        if (string.IsNullOrEmpty(xmlName))
        {
            xmlName = Path.GetFileName(file) + ".xml";
        }

        var xmlPath = Path.Combine(outDir, xmlName);
        File.WriteAllText(xmlPath, xml, new UTF8Encoding(false));
        Console.WriteLine(xmlPath);
        return 0;
    }

    private static int FromXml(string file, string? output)
    {
        file = Path.GetFullPath(file);
        if (!File.Exists(file))
        {
            Console.Error.WriteLine("file not found: " + file);
            return 1;
        }

        var fileName = Path.GetFileName(file);
        var format = XmlMeta.GetXMLFormat(fileName.ToLowerInvariant(), out var trimLength);
        if (format == MetaFormat.XML)
        {
            Console.Error.WriteLine("not a CodeWalker XML file name (expected <name>.<ext>.xml): " + fileName);
            return 1;
        }

        var binName = fileName.Substring(0, fileName.Length - trimLength);
        var dir = Path.GetDirectoryName(file) ?? string.Empty;
        var baseName = Path.Combine(dir, Path.GetFileNameWithoutExtension(binName));

        var doc = new XmlDocument();
        doc.Load(file);
        var data = XmlMeta.GetData(doc, format, baseName);
        if (data == null)
        {
            Console.Error.WriteLine("conversion produced no data: " + fileName);
            return 1;
        }

        string target;
        if (string.IsNullOrEmpty(output))
        {
            target = Path.Combine(dir, binName);
        }
        else if (Directory.Exists(output) || output.EndsWith('\\') || output.EndsWith('/'))
        {
            Directory.CreateDirectory(output);
            target = Path.Combine(output, binName);
        }
        else
        {
            target = output;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target))!);
        }

        File.WriteAllBytes(target, data);
        Console.WriteLine(Path.GetFullPath(target));
        return 0;
    }

    // One line per .ybn path in the list file: path|min|max|type, or path|ERROR message.
    private static int YbnBounds(string listFile)
    {
        if (!File.Exists(listFile))
        {
            Console.Error.WriteLine("list file not found: " + listFile);
            return 2;
        }

        var culture = CultureInfo.InvariantCulture;
        foreach (var line in File.ReadAllLines(listFile))
        {
            var path = line.Trim();
            if (path.Length == 0)
            {
                continue;
            }

            try
            {
                var data = File.ReadAllBytes(path);
                var bounds = RpfFile.GetFile<YbnFile>(CreateFileEntry(path, ref data), data).Bounds
                    ?? throw new InvalidDataException("no bounds");
                Console.WriteLine(string.Format(culture, "{0}|{1:R},{2:R},{3:R}|{4:R},{5:R},{6:R}|{7}",
                    path,
                    bounds.BoxMin.X, bounds.BoxMin.Y, bounds.BoxMin.Z,
                    bounds.BoxMax.X, bounds.BoxMax.Y, bounds.BoxMax.Z,
                    bounds.Type));
            }
            catch (Exception ex)
            {
                Console.WriteLine(path + "|ERROR " + ex.Message.Replace('|', '/').Replace('\n', ' ').Replace('\r', ' '));
            }
        }
        return 0;
    }
}
