using CodeWalker.GameFiles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace CodeWalker.World
{
    public class TimecycleMods
    {

        public Dictionary<uint, TimecycleMod> Dict = new();



        public void Init(GameFileCache gameFileCache, Action<string> updateStatus)
        {
            Dict.Clear();

            var rpfman = gameFileCache.RpfMan
                ?? throw new InvalidOperationException("The game file cache must have an RPF manager before loading timecycle modifiers.");

            LoadXml(rpfman.GetFileXml("common.rpf\\data\\timecycle\\timecycle_mods_1.xml"));
            LoadXml(rpfman.GetFileXml("common.rpf\\data\\timecycle\\timecycle_mods_2.xml"));
            LoadXml(rpfman.GetFileXml("common.rpf\\data\\timecycle\\timecycle_mods_3.xml"));
            LoadXml(rpfman.GetFileXml("common.rpf\\data\\timecycle\\timecycle_mods_4.xml"));

            LoadXml(rpfman.GetFileXml("update\\update.rpf\\common\\data\\timecycle\\timecycle_mods_1.xml"));
            LoadXml(rpfman.GetFileXml("update\\update.rpf\\common\\data\\timecycle\\timecycle_mods_2.xml"));//doesn't exist, but try anyway
            LoadXml(rpfman.GetFileXml("update\\update.rpf\\common\\data\\timecycle\\timecycle_mods_3.xml"));
            LoadXml(rpfman.GetFileXml("update\\update.rpf\\common\\data\\timecycle\\timecycle_mods_4.xml"));

            if (gameFileCache.EnableDlc)
            {
                foreach (var dlcrpf in gameFileCache.DlcActiveRpfs)
                {
                    foreach (var file in dlcrpf.AllEntries)
                    {
                        if (file.NameLower.EndsWith(".xml") && file.NameLower.StartsWith("timecycle_mods_"))
                        {
                            LoadXml(rpfman.GetFileXml(file.Path));
                        }
                    }
                }
            }


            gameFileCache.TimeCycleModsDict = Dict;
        }

        private void LoadXml(XmlDocument? doc)
        {
            var root = doc?.DocumentElement;
            if (root == null)
            { return; }

            float version = Xml.GetFloatAttribute(root, "version");

            var modnodes = root.SelectNodes("modifier");
            if (modnodes == null) return;
            foreach (XmlNode modnode in modnodes)
            {
                if (!(modnode is XmlElement)) continue; 
                TimecycleMod mod = new();
                mod.Init(modnode);
                Dict[mod.nameHash] = mod;
            }

        }


    }


    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class TimecycleMod
    {
        public string name { get; set; } = string.Empty;
        public uint nameHash { get; set; }
        public int numMods { get; set; }
        public int userFlags { get; set; }

        public TimecycleModValue[] Values { get; set; } = [];
        public Dictionary<string, TimecycleModValue> Dict { get; set; } = new();

        public void Init(XmlNode node)
        {
            Dict = new Dictionary<string, TimecycleModValue>();

            name = Xml.GetStringAttribute(node, "name")
                ?? throw new XmlException("A timecycle modifier must have a name attribute.");
            numMods = Xml.GetIntAttribute(node, "numMods");
            userFlags = Xml.GetIntAttribute(node, "userFlags");

            string namel = name.ToLowerInvariant();
            JenkIndex.Ensure(namel);
            nameHash = JenkHash.GenHash(namel);

            List<TimecycleModValue> vals = new();
            foreach (XmlNode valnode in node.ChildNodes)
            {
                if (!(valnode is XmlElement)) continue;

                TimecycleModValue val = new();
                val.Init(valnode);

                vals.Add(val);
                Dict[val.name] = val;
            }
            Values = vals.ToArray();

        }

        public override string ToString()
        {
            return name + " (" + numMods.ToString() + " mods, userFlags: " + userFlags.ToString() + ")";
        }
    }

    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class TimecycleModValue
    {
        public string name { get; set; } = string.Empty;
        public float value1 { get; set; }
        public float value2 { get; set; }

        public void Init(XmlNode node)
        {
            name = node.Name;

            string valstr = node.InnerText;
            string[] valstrs = valstr.Split(' ');
            if (valstrs.Length == 2)
            {
                value1 = FloatUtil.Parse(valstrs[0]);
                value2 = FloatUtil.Parse(valstrs[1]);
            }
            else
            { }
        }

        public override string ToString()
        {
            return name + ": " + FloatUtil.ToString(value1) + ", " + FloatUtil.ToString(value2);
        }
    }

}
