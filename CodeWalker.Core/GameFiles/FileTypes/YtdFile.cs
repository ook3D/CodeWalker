using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace CodeWalker.GameFiles
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class YtdFile : GameFile, PackedFile
    {
        public TextureDictionary? TextureDict { get; set; }


        public YtdFile() : base(null, GameFileType.Ytd)
        {
        }
        public YtdFile(RpfFileEntry entry) : base(entry, GameFileType.Ytd)
        {
        }


        public void Load(byte[] data)
        {
            //direct load from a raw, compressed ytd file

            RpfFile.LoadResourceFile(this, data, (uint)GetVersion(RpfManager.IsGen9));

            Loaded = true;
        }

        public async Task LoadAsync(byte[] data, IProgress<float>? progress = null, CancellationToken cancellationToken = default)
        {
            //direct load from a raw, compressed ytd file - async version

            await Task.Run(() =>
            {
                progress?.Report(0.5f);
                RpfFile.LoadResourceFile(this, data, (uint)GetVersion(RpfManager.IsGen9));
                progress?.Report(1.0f);
            }, cancellationToken).ConfigureAwait(false);

            Loaded = true;
        }
        public void Load(byte[] data, RpfFileEntry entry)
        {
            Name = entry.Name;
            RpfFileEntry = entry;


            RpfResourceFileEntry? resentry = entry as RpfResourceFileEntry;
            if (resentry == null)
            {
                throw new Exception("File entry wasn't a resource! (is it binary data?)");
            }

            ResourceDataReader rd = new(resentry, data);
            
            if (rd.IsGen9)
            {
                switch (resentry.Version)
                {
                    case 5:
                        break;
                    case 13:
                        rd.IsGen9 = false;
                        break;
                    default:
                        break;
                }
            }


            TextureDict = rd.ReadRequiredBlock<TextureDictionary>();

            //MemoryUsage = 0; //uses decompressed file size now..
            //if (TextureDict != null)
            //{
            //    MemoryUsage += TextureDict.MemoryUsage;
            //}

            //var analyzer = new ResourceAnalyzer(rd);

        }

        public async Task LoadAsync(byte[] data, RpfFileEntry entry, IProgress<float>? progress = null, CancellationToken cancellationToken = default)
        {
            Name = entry.Name;
            RpfFileEntry = entry;

            RpfResourceFileEntry? resentry = entry as RpfResourceFileEntry;
            if (resentry == null)
            {
                throw new Exception("File entry wasn't a resource! (is it binary data?)");
            }

            await Task.Run(() =>
            {
                progress?.Report(0.3f);
                
                ResourceDataReader rd = new(resentry, data);
                
                if (rd.IsGen9)
                {
                    switch (resentry.Version)
                    {
                        case 5:
                            break;
                        case 13:
                            rd.IsGen9 = false;
                            break;
                        default:
                            break;
                    }
                }

                progress?.Report(0.7f);
                TextureDict = rd.ReadRequiredBlock<TextureDictionary>();
                progress?.Report(1.0f);
            }, cancellationToken).ConfigureAwait(false);
        }


        public byte[] Save()
        {
            var gen9 = RpfManager.IsGen9;
            if (gen9)
            {
                TextureDict?.EnsureGen9();
            }

            byte[] data = ResourceBuilder.Build(TextureDict ?? throw new InvalidOperationException("No resource loaded."), GetVersion(gen9), true, gen9);

            return data;
        }

        public int GetVersion(bool gen9)
        {
            return gen9 ? 5 : 13;
        }

    }




    public class YtdXml : MetaXmlBase
    {

        public static string GetXml(YtdFile ytd, string outputFolder = "")
        {
            StringBuilder sb = new();
            sb.AppendLine(XmlHeader);

            if (ytd?.TextureDict != null)
            {
                TextureDictionary.WriteXmlNode(ytd.TextureDict, sb, 0, outputFolder);
            }

            return sb.ToString();
        }

    }

    public class XmlYtd
    {

        public static YtdFile GetYtd(string xml, string inputFolder = "")
        {
            XmlDocument doc = new();
            doc.LoadXml(xml);
            return GetYtd(doc, inputFolder);
        }

        public static YtdFile GetYtd(XmlDocument doc, string inputFolder = "")
        {
            YtdFile r = new();

            var ddsfolder = inputFolder;

            var node = doc.DocumentElement;
            if (node != null)
            {
                r.TextureDict = TextureDictionary.ReadXmlNode(node, ddsfolder);
            }

            r.Name = Path.GetFileName(inputFolder);

            return r;
        }

    }



}
