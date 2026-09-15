using CodeWalker.GameFiles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Globalization;
using SharpDX;

namespace CodeWalker.World
{
    // Mirrors CWeaponInfo::AttachPoints and CWeaponComponentInfo in the game metadata.
    public sealed class WeaponCatalog
    {
        public Dictionary<string, WeaponDefinition> Weapons { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, WeaponComponentDefinition> Components { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, HashSet<string>> AnimationSets { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, WeaponTint[]> TintSets { get; } = new(StringComparer.OrdinalIgnoreCase);

        public static WeaponCatalog Load(GameFileCache cache, Action<string> log)
        {
            var catalog = new WeaponCatalog();
            var archives = cache.ActiveMapRpfFiles.Count == 0 ? cache.AllRpfs : cache.ActiveMapRpfFiles.Values.ToList();
            if (cache.EnableDlc) archives = archives.Concat(cache.DlcActiveRpfs).Distinct().ToList();
            foreach (var archive in archives)
            {
                foreach (var entry in archive.AllEntries.OfType<RpfFileEntry>())
                {
                    if (!entry.NameLower.StartsWith("weapon", StringComparison.Ordinal) || !entry.NameLower.EndsWith(".meta", StringComparison.Ordinal)) continue;
                    try
                    {
                        catalog.AddXml(TextUtil.GetUTF8Text(archive.ExtractFile(entry)));
                    }
                    catch (Exception ex)
                    {
                        log(entry.Path + ": " + ex.Message);
                    }
                }
            }
            return catalog;
        }

        public void AddXml(string xml)
        {
            var doc = XDocument.Parse(xml);
            foreach (var item in doc.Descendants("TintSpecValues").Elements("Item"))
            {
                var name = Value(item, "Name");
                if (name.Length == 0) continue;
                TintSets[name] = (item.Element("Tints")?.Elements("Item") ?? []).Select(x => new WeaponTint
                {
                    SpecularIntensity = (float?)x.Element("SpecIntMult")?.Attribute("value") ?? 1,
                    SpecularFalloff = (float?)x.Element("SpecFalloffMult")?.Attribute("value") ?? 100,
                    SpecularFresnel = (float?)x.Element("SpecFresnel")?.Attribute("value") ?? 0.97f,
                    SecondaryFalloff = (float?)x.Element("Spec2Factor")?.Attribute("value") ?? 40,
                    SecondaryIntensity = (float?)x.Element("Spec2ColorInt")?.Attribute("value") ?? 4.7f,
                    SecondaryColour = ReadTintColour(Value(x, "Spec2Color"))
                }).ToArray();
            }
            foreach (var item in doc.Descendants("Item"))
            {
                var type = (string?)item.Attribute("type") ?? "";
                var name = Value(item, "Name");
                var model = Value(item, "Model");
                if (type == "CWeaponInfo" && name.Length > 0 && model.Length > 0 && model != "NULL")
                {
                    var definition = new WeaponDefinition { Name = name, Model = model, TintSet = (string?)item.Element("TintSpecValues")?.Attribute("ref") ?? "" };
                    foreach (var point in item.Element("AttachPoints")?.Elements("Item") ?? [])
                    {
                        foreach (var component in point.Element("Components")?.Elements("Item") ?? [])
                        {
                            var componentName = Value(component, "Name");
                            if (componentName.Length == 0) continue;
                            definition.Attachments.Add(new WeaponAttachment
                            {
                                Name = componentName,
                                Bone = Value(point, "AttachBone"),
                                Default = string.Equals(Value(component, "Default"), "true", StringComparison.OrdinalIgnoreCase)
                            });
                        }
                    }
                    Weapons[name] = definition;
                }
                else if (type.StartsWith("CWeaponComponent", StringComparison.Ordinal) && name.Length > 0)
                {
                    var component = new WeaponComponentDefinition
                    {
                        Name = name, Model = model, Bone = Value(item, "AttachBone"),
                        Variant = type == "CWeaponComponentVariantModelInfo",
                        TintOverride = int.TryParse(Value(item, "TintIndexOverride"), out var tint) ? tint : -1,
                        ApplyTint = string.Equals(Value(item, "ApplyWeaponTint"), "true", StringComparison.OrdinalIgnoreCase)
                    };
                    foreach (var extra in item.Element("ExtraComponents")?.Elements("Item") ?? [])
                        component.ExtraModels[Value(extra, "ComponentName")] = Value(extra, "ComponentModel");
                    Components[name] = component;
                }

                // weaponanimations.meta contains maps keyed by weapon, nested within animation sets.
                var key = (string?)item.Attribute("key") ?? name;
                if (!key.StartsWith("WEAPON_", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var field in item.Elements().Where(x => x.Name.LocalName.Contains("WeaponClipSet", StringComparison.Ordinal)))
                {
                    var set = field.Value.Trim();
                    if (set.Length == 0 || set == "NULL") continue;
                    if (!AnimationSets.TryGetValue(key, out var sets)) AnimationSets[key] = sets = new(StringComparer.OrdinalIgnoreCase);
                    sets.Add(set);
                }
            }
        }

        private static Vector3 ReadTintColour(string value)
        {
            bool hex = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
            if (!ulong.TryParse(hex ? value[2..] : value, hex ? NumberStyles.HexNumber : NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var colour)) return Vector3.One;
            return new Vector3((colour >> 16) & 255, (colour >> 8) & 255, colour & 255) / 255f;
        }

        private static string Value(XElement element, string name)
        {
            var child = element.Element(name);
            return ((string?)child?.Attribute("value") ?? child?.Value ?? "").Trim();
        }
    }

    public sealed class WeaponDefinition
    {
        public string Name { get; set; } = "";
        public string Model { get; set; } = "";
        public string TintSet { get; set; } = "";
        public List<WeaponAttachment> Attachments { get; } = [];
        public override string ToString() => Name;
    }

    public sealed class WeaponAttachment
    {
        public string Name { get; set; } = "";
        public string Bone { get; set; } = "";
        public bool Default { get; set; }
        public override string ToString() => Name;
    }

    public sealed class WeaponComponentDefinition
    {
        public string Name { get; set; } = "";
        public string Model { get; set; } = "";
        public string Bone { get; set; } = "";
        public bool ApplyTint { get; set; }
        public bool Variant { get; set; }
        public int TintOverride { get; set; } = -1;
        public Dictionary<string, string> ExtraModels { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class WeaponTint
    {
        public float SpecularIntensity { get; set; }
        public float SpecularFalloff { get; set; }
        public float SpecularFresnel { get; set; }
        public float SecondaryFalloff { get; set; }
        public float SecondaryIntensity { get; set; }
        public Vector3 SecondaryColour { get; set; }
    }
}
