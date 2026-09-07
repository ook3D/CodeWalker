using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace CodeWalker.GameFiles
{
    public class DlcSetupFile
    {
        public string deviceName { get; set; } = string.Empty;
        public string datFile { get; set; } = string.Empty;
        public string nameHash { get; set; } = string.Empty;
        public List<DlcSetupContentChangesetGroup> contentChangeSetGroups { get; set; } = [];
        public string type { get; set; } = string.Empty;
        public string timeStamp { get; set; } = string.Empty;
        public int order { get; set; }
        public int minorOrder { get; set; }
        public int subPackCount { get; set; }
        public bool isLevelPack { get; set; }

        public RpfFile? DlcFile { get; set; } //used by GameFileCache
        public List<RpfFile> DlcSubpacks { get; set; } = []; //used by GameFileCache
        public DlcContentFile? ContentFile { get; set; }

        public void Load(XmlDocument doc)
        {

            var root = doc.DocumentElement ?? throw new XmlException("The DLC setup XML is missing its root element.");
            deviceName = Xml.GetChildInnerText(root, "deviceName") ?? string.Empty;
            datFile = Xml.GetChildInnerText(root, "datFile") ?? string.Empty;
            nameHash = Xml.GetChildInnerText(root, "nameHash") ?? string.Empty;
            type = Xml.GetChildInnerText(root, "type") ?? string.Empty;
            timeStamp = Xml.GetChildInnerText(root, "timeStamp") ?? string.Empty;
            order = Xml.GetIntAttribute(root.SelectSingleNode("order"), "value");
            minorOrder = Xml.GetIntAttribute(root.SelectSingleNode("minorOrder"), "value");
            subPackCount = Xml.GetIntAttribute(root.SelectSingleNode("subPackCount"), "value");
            isLevelPack = Xml.GetBoolAttribute(root.SelectSingleNode("isLevelPack"), "value");

            contentChangeSetGroups = new List<DlcSetupContentChangesetGroup>();
            var groups = root.SelectNodes("contentChangeSetGroups/Item");
            foreach (XmlNode node in groups?.Cast<XmlNode>() ?? Enumerable.Empty<XmlNode>())
            {
                var group = new DlcSetupContentChangesetGroup();
                group.Load(node);
                contentChangeSetGroups.Add(group);
            }

            if (root.ChildNodes.Count > 15)
            { }
        }

        public override string ToString()
        {
            return deviceName + ", " + datFile + ", " + nameHash + ", " + type + ", " + order.ToString() + ", " + ((contentChangeSetGroups != null) ? contentChangeSetGroups.Count.ToString() : "0") + " groups, " + timeStamp;
        }
    }

    public class DlcSetupContentChangesetGroup
    {
        public string NameHash { get; set; } = string.Empty;
        public List<string> ContentChangeSets { get; set; } = [];

        public void Load(XmlNode node)
        {
            if (node.ChildNodes.Count != 2)
            { }
            NameHash = Xml.GetChildInnerText(node, "NameHash") ?? string.Empty;
            ContentChangeSets = new List<string>();
            var changesets = node.SelectNodes("ContentChangeSets/Item");
            foreach (XmlNode changeset in changesets?.Cast<XmlNode>() ?? Enumerable.Empty<XmlNode>())
            {
                ContentChangeSets.Add(changeset.InnerText);
            }
        }

        public override string ToString()
        {
            return NameHash + " (" + ((ContentChangeSets != null) ? ContentChangeSets.Count.ToString() : "0") + " changesets)";
        }
    }
}
