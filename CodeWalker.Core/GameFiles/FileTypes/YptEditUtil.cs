using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace CodeWalker.GameFiles
{
    // Structural-editing helpers for .ypt particle files, shared by the particle editor UI.
    // New/duplicated structure is built via the same WriteXml/ReadXml round-trip the XML importer uses, so the
    // counts/capacities/ManualReferenceOverride flags that ResourceBuilder.Build needs are set up correctly.
    public static class YptEditUtil
    {
        // ---- deep clone via XML round-trip ----

        private static XmlNode WriteToItemNode(Action<StringBuilder> writeXml)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<Item>");
            writeXml(sb);
            sb.AppendLine("</Item>");
            var doc = new XmlDocument();
            doc.LoadXml(sb.ToString());
            return doc.DocumentElement;
        }

        public static ParticleEffectRule CloneEffect(ParticleEffectRule src)
        {
            var node = WriteToItemNode(sb => src.WriteXml(sb, 1));
            var clone = new ParticleEffectRule();
            clone.ReadXml(node);

            // re-link the cloned event emitters to the same (shared) emitter/particle rules as the source
            var se = src.EventEmitters?.data_items;
            var ce = clone.EventEmitters?.data_items;
            if ((se != null) && (ce != null))
            {
                for (int i = 0; i < Math.Min(se.Length, ce.Length); i++)
                {
                    if ((se[i] != null) && (ce[i] != null))
                    {
                        ce[i].EmitterRule = se[i].EmitterRule;
                        ce[i].ParticleRule = se[i].ParticleRule;
                    }
                }
            }
            return clone;
        }

        public static ParticleEventEmitter CloneEmitter(ParticleEventEmitter src)
        {
            var node = WriteToItemNode(sb => src.WriteXml(sb, 1));
            var clone = new ParticleEventEmitter();
            clone.ReadXml(node);
            clone.EmitterRule = src.EmitterRule;
            clone.ParticleRule = src.ParticleRule;
            return clone;
        }


        // ---- effect dictionary ----

        public static string UniqueEffectName(YptFile ypt, string baseName)
        {
            if (string.IsNullOrEmpty(baseName)) baseName = "new_effect";
            var dict = ypt?.EffectDict;
            var name = baseName;
            int n = 1;
            while ((dict != null) && dict.ContainsKey(JenkHash.GenHash(name)))
            {
                name = baseName + "_" + n.ToString();
                n++;
            }
            return name;
        }

        public static ParticleEffectRule NewEffectFromTemplate(YptFile ypt, ParticleEffectRule template, string baseName)
        {
            var clone = CloneEffect(template);
            var name = UniqueEffectName(ypt, baseName ?? ((template.Name?.Value ?? "effect") + "_copy"));
            clone.Name = (string_r)name;
            clone.NameHash = JenkHash.GenHash(name);
            JenkIndex.Ensure(name);
            AddEffect(ypt, clone);
            return clone;
        }

        public static void AddEffect(YptFile ypt, ParticleEffectRule effect)
        {
            var dict = ypt.PtfxList.EffectRuleDictionary;
            var rules = (dict.EffectRules?.data_items ?? Array.Empty<ParticleEffectRule>()).ToList();
            rules.Add(effect);
            rules.Sort((a, b) => a.NameHash.Hash.CompareTo(b.NameHash.Hash));
            WriteEffectDict(dict, rules);
        }

        public static void RemoveEffect(YptFile ypt, ParticleEffectRule effect)
        {
            var dict = ypt.PtfxList.EffectRuleDictionary;
            var rules = (dict.EffectRules?.data_items ?? Array.Empty<ParticleEffectRule>()).ToList();
            rules.Remove(effect);
            WriteEffectDict(dict, rules);
        }

        private static void WriteEffectDict(ParticleEffectRuleDictionary dict, List<ParticleEffectRule> rules)
        {
            dict.EffectRules = new ResourcePointerList64<ParticleEffectRule>();
            dict.EffectRules.data_items = rules.ToArray();
            dict.EffectRuleNameHashes = new ResourceSimpleList64_s<MetaHash>();
            dict.EffectRuleNameHashes.data_items = rules.Select(r => r.NameHash).ToArray();
        }


        // ---- emitters (within an effect) ----

        public static ParticleEventEmitter AddEmitter(ParticleEffectRule eff)
        {
            var items = eff.EventEmitters?.data_items;
            int count = Math.Min(eff.EventEmittersCount, items?.Length ?? 0);
            ParticleEventEmitter? src = null;
            for (int i = 0; i < count; i++) { if (items[i] != null) { src = items[i]; break; } }
            if (src == null) return null; //nothing to clone (blank emitters not supported yet)
            if (eff.EventEmittersCount >= 32) return null;

            var clone = CloneEmitter(src);
            var list = new List<ParticleEventEmitter>();
            for (int i = 0; i < count; i++) { if (items[i] != null) list.Add(items[i]); }
            list.Add(clone);
            for (int i = 0; i < list.Count; i++) list[i].Index = (uint)i;
            while (list.Count < 32) list.Add(null);
            eff.EventEmitters.data_items = list.ToArray();
            eff.EventEmittersCount = (ushort)list.Count(x => x != null);
            return clone;
        }

        public static void RemoveEmitter(ParticleEffectRule eff, ParticleEventEmitter em)
        {
            var items = eff.EventEmitters?.data_items;
            if (items == null) return;
            var list = new List<ParticleEventEmitter>();
            int count = Math.Min(eff.EventEmittersCount, items.Length);
            for (int i = 0; i < count; i++) { if ((items[i] != null) && (items[i] != em)) list.Add(items[i]); }
            for (int i = 0; i < list.Count; i++) list[i].Index = (uint)i;
            while (list.Count < 32) list.Add(null);
            eff.EventEmitters.data_items = list.ToArray();
            eff.EventEmittersCount = (ushort)list.Count(x => x != null);
        }


        // ---- behaviours (within a particle rule) ----

        public static ParticleBehaviour AddBehaviour(ParticleRule prule, ParticleBehaviourType type)
        {
            var beh = ParticleBehaviour.Create(type);
            if (beh == null) return null;
            beh.Type = type; // Create() builds the subclass but doesn't set Type (normally set by Read/ReadXml on load)
            beh.CreateKeyframeProps();
            AppendBehaviour(prule.AllBehaviours, beh);
            if (type == ParticleBehaviourType.Sprite) AppendBehaviour(prule.DrawBehaviours, beh);
            else AppendBehaviour(prule.UpdateBehaviours, beh);
            return beh;
        }

        public static void RemoveBehaviour(ParticleRule prule, ParticleBehaviour beh)
        {
            RemoveBehaviourFrom(prule.AllBehaviours, beh);
            RemoveBehaviourFrom(prule.InitBehaviours, beh);
            RemoveBehaviourFrom(prule.UpdateBehaviours, beh);
            RemoveBehaviourFrom(prule.UpdateFinalizeBehaviours, beh);
            RemoveBehaviourFrom(prule.DrawBehaviours, beh);
        }

        private static void AppendBehaviour(ResourcePointerList64<ParticleBehaviour>? list, ParticleBehaviour beh)
        {
            if (list == null) return;
            var items = (list.data_items ?? Array.Empty<ParticleBehaviour>()).Where(x => x != null).ToList();
            items.Add(beh);
            list.data_items = items.ToArray();
            list.EntriesCount = (ushort)items.Count;
            list.ManualCountOverride = true;
        }

        private static void RemoveBehaviourFrom(ResourcePointerList64<ParticleBehaviour> list, ParticleBehaviour beh)
        {
            var items = list?.data_items;
            if (items == null) return;
            var keep = items.Where(x => (x != null) && (x != beh)).ToList();
            if (keep.Count == items.Count(x => x != null)) return;
            list.data_items = keep.ToArray();
            list.EntriesCount = (ushort)keep.Count;
            list.ManualCountOverride = true;
        }
    }
}
