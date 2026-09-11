using SharpDX;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

/*
    Copyright(c) 2016 Neodymium

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in
    all copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
    THE SOFTWARE.
*/


namespace CodeWalker.GameFiles
{


    [TypeConverter(typeof(ExpandableObjectConverter))] public class ClipDictionary : ResourceFileBase
    {
        public override long BlockLength
        {
            get { return 64; }
        }

        // structure data
        public uint ReferenceCount { get; set; }
        public ulong AnimationsPointer { get; set; }
        public bool OwnsAnimationDictionary { get; set; } = true;
        public bool UsesBaseNameKeys { get; set; } = true;
        public ulong ClipsPointer { get; set; }
        public ushort ClipsMapCapacity { get; set; }
        public ushort ClipsMapEntries { get; set; }
        public uint ClipsMapFlags { get; set; } = 0x01000000;

        // reference data
        public AnimationMap? Animations { get; set; }
        public ResourcePointerArray64<ClipMapEntry>? Clips { get; set; }

        //data used by CW for loading/saving
        public Dictionary<MetaHash, ClipMapEntry> ClipMap { get; set; } = new();
        public Dictionary<MetaHash, AnimationMapEntry> AnimMap { get; set; } = new();


        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            base.Read(reader, parameters);

            // read structure data
            this.ReferenceCount = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            this.AnimationsPointer = reader.ReadUInt64();
            this.OwnsAnimationDictionary = reader.ReadByte() != 0;
            this.UsesBaseNameKeys = reader.ReadByte() != 0;
            _ = reader.ReadUInt16();
            _ = reader.ReadUInt32();
            this.ClipsPointer = reader.ReadUInt64();
            this.ClipsMapCapacity = reader.ReadUInt16();
            this.ClipsMapEntries = reader.ReadUInt16();
            this.ClipsMapFlags = reader.ReadUInt32();
            _ = reader.ReadUInt64();

            // read reference data
            this.Animations = reader.ReadBlockAt<AnimationMap>(
                this.AnimationsPointer // offset
            );
            this.Clips = reader.ReadBlockAt<ResourcePointerArray64<ClipMapEntry>>(
                this.ClipsPointer, // offset
                this.ClipsMapCapacity
            );

            BuildMaps();
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            base.Write(writer, parameters);

            // update structure data
            this.AnimationsPointer = (ulong)(this.Animations != null ? this.Animations.FilePosition : 0);
            this.ClipsPointer = (ulong)(this.Clips != null ? this.Clips.FilePosition : 0);
            this.ClipsMapCapacity = (ushort)((Clips != null) ? Clips.Count : 0);
            this.ClipsMapEntries = (ushort)((ClipMap != null) ? ClipMap.Count : 0);


            // write structure data
            writer.Write(this.ReferenceCount);
            writer.Write(0u);
            writer.Write(this.AnimationsPointer);
            writer.Write((byte)(this.OwnsAnimationDictionary ? 1 : 0));
            writer.Write((byte)(this.UsesBaseNameKeys ? 1 : 0));
            writer.Write((ushort)0);
            writer.Write(0u);
            writer.Write(this.ClipsPointer);
            writer.Write(this.ClipsMapCapacity);
            writer.Write(this.ClipsMapEntries);
            writer.Write(this.ClipsMapFlags);
            writer.Write(0ul);
        }

        public override IResourceBlock[] GetReferences()
        {
            var list = new List<IResourceBlock>(base.GetReferences());
            if (Animations != null)
            {
                list.Add(Animations);
                Animations.AnimationsMapEntries = (ushort)(AnimMap?.Count ?? 0);
            }
            if (Clips != null) list.Add(Clips);
            return list.ToArray();
        }



        public void BuildMaps()
        {
            ClipMap = new Dictionary<MetaHash, ClipMapEntry>();
            AnimMap = new Dictionary<MetaHash, AnimationMapEntry>();

            if ((Clips != null) && (Clips.data_items != null))
            {
                foreach (var cme in Clips.data_items)
                {
                    if (cme != null)
                    {
                        ClipMap[cme.Hash] = cme;
                        var nxt = cme.Next;
                        while (nxt != null)
                        {
                            ClipMap[nxt.Hash] = nxt;
                            nxt = nxt.Next;
                        }
                    }
                }
            }
            if ((Animations != null) && (Animations.Animations != null) && (Animations.Animations.data_items != null))
            {
                foreach (var ame in Animations.Animations.data_items)
                {
                    if (ame != null)
                    {
                        AnimMap[ame.Hash] = ame;
                        var nxt = ame.NextEntry;
                        while (nxt != null)
                        {
                            AnimMap[nxt.Hash] = nxt;
                            nxt = nxt.NextEntry;
                        }
                    }
                }
            }

            foreach (var cme in ClipMap.Values)
            {
                var clip = cme.Clip;
                if (clip == null) continue;

                var name = clip.ShortName; //just to make sure ShortName is generated and in JenkIndex...

                //if (name.EndsWith("_uv_0")) //hash for these entries match string with this removed, +1
                //{
                //}
                //if (name.EndsWith("_uv_1")) //same as above, but +2
                //{
                //}

            }
            //foreach (var ame in AnimMap.Values)
            //{
            //    var anim = ame.Animation;
            //    if (anim == null) continue;
            //}
        }

        public void UpdateUsageCounts()
        {

            var usages = new Dictionary<MetaHash, uint>();

            void addUsage(MetaHash h)
            {
                uint u = 0;
                usages.TryGetValue(h, out u);
                u++;
                usages[h] = u;
            }

            if ((Animations != null) && (Animations.Animations != null) && (Animations.Animations.data_items != null))
            {
                foreach (var ame in Animations.Animations.data_items)
                {
                    if (ame != null)
                    {
                        addUsage(ame.Hash);
                        var nxt = ame.NextEntry;
                        while (nxt != null)
                        {
                            addUsage(nxt.Hash);
                            nxt = nxt.NextEntry;
                        }
                    }
                }
            }

            foreach (var cme in ClipMap.Values)
            {
                var ca = cme.Clip as ClipAnimation;
                var cal = cme.Clip as ClipAnimationList;
                if (ca?.Animation != null)
                {
                    addUsage(ca.Animation.Hash);
                }
                if (cal?.Animations != null)
                {
                    foreach (var cae in cal.Animations)
                    {
                        if (cae?.Animation != null)
                        {
                            addUsage(cae.Animation.Hash);
                        }
                    }
                }
            }




            foreach (var ame in AnimMap.Values)
            {
                if (ame.Animation != null)
                {
                    uint u = 0;
                    if (usages.TryGetValue(ame.Animation.Hash, out u))
                    {
                        if (ame.Animation.UsageCount != u)
                        { }
                        ame.Animation.UsageCount = u;
                    }
                    else
                    { }
                }
            }


        }


        public void WriteXml(StringBuilder sb, int indent)
        {
            var clips = new List<ClipBase>();
            if (ClipMap != null)
            {
                foreach (var cme in ClipMap.Values)
                {
                    if (cme?.Clip == null) continue;
                    clips.Add(cme.Clip);
                }
            }
            var anims = new List<Animation>();
            if (AnimMap != null)
            {
                foreach (var ame in AnimMap.Values)
                {
                    if (ame?.Animation == null) continue;
                    anims.Add(ame.Animation);
                }
            }

            YcdXml.WriteItemArray(sb, clips.ToArray(), indent, "Clips");
            YcdXml.WriteItemArray(sb, anims.ToArray(), indent, "Animations");

        }
        public void ReadXml(XmlNode node)
        {

            var clipList = new List<ClipMapEntry>();
            var clipsNode = node.SelectSingleNode("Clips");
            if (clipsNode != null)
            {
                var inodes = clipsNode.SelectNodes("Item");
                if (inodes?.Count > 0)
                {
                    foreach (XmlNode inode in inodes)
                    {
                        var type = Xml.GetEnumValue<ClipType>(Xml.GetChildStringAttribute(inode, "Type", "value"));
                        var c = ClipBase.ConstructClip(type);
                        c.ReadXml(inode);

                        var cme = new ClipMapEntry();
                        cme.Hash = c.Hash;
                        cme.Clip = c;
                        clipList.Add(cme);
                    }
                }
            }

            var animDict = new Dictionary<MetaHash, Animation>();
            var animList = new List<AnimationMapEntry>();
            var anims = XmlMeta.ReadItemArrayNullable<Animation>(node, "Animations") ?? [];
            if (anims != null)
            {
                foreach (var anim in anims)
                {
                    animDict[anim.Hash] = anim;

                    var ame = new AnimationMapEntry();
                    ame.Hash = anim.Hash;
                    ame.Animation = anim;
                    animList.Add(ame);
                }
            }

            foreach (var cme in clipList)
            {
                var cb = cme?.Clip;
                var clipanim = cb as ClipAnimation;
                if (clipanim != null)
                {
                    animDict.TryGetValue(clipanim.AnimationHash, out Animation? a);
                    clipanim.Animation = a;
                }
                var clipanimlist = cb as ClipAnimationList;
                if (clipanimlist?.Animations?.Data != null)
                {
                    foreach (var cae in clipanimlist.Animations.Data)
                    {
                        animDict.TryGetValue(cae.AnimationHash, out Animation? a);
                        cae.Animation = a;
                    }
                }
            }

            CreateAnimationsMap(animList.ToArray());
            CreateClipsMap(clipList.ToArray());



            BuildMaps();
            UpdateUsageCounts();
        }




        public void CreateClipsMap(ClipMapEntry[] clips)
        {
            var numClipBuckets = GetNumHashBuckets(clips?.Length ?? 0);
            var clipBuckets = new List<ClipMapEntry>[numClipBuckets];
            if (clips != null)
            {
                foreach (var cme in clips)
                {
                    var b = cme.Hash % numClipBuckets;
                    var bucket = clipBuckets[b];
                    if (bucket == null)
                    {
                        bucket = new List<ClipMapEntry>();
                        clipBuckets[b] = bucket;
                    }
                    bucket.Add(cme);
                }
            }

            var newClips = new ClipMapEntry[clipBuckets.Length];
            for (int bucketIndex = 0; bucketIndex < clipBuckets.Length; bucketIndex++)
            {
                var b = clipBuckets[bucketIndex];
                if (b is { Count: > 0 })
                {
                    newClips[bucketIndex] = b[0];
                    var p = b[0];
                    for (int i = 1; i < b.Count; i++)
                    {
                        var c = b[i];
                        c.Next = null;
                        p.Next = c;
                        p = c;
                    }
                }
            }

            Clips = new ResourcePointerArray64<ClipMapEntry>();
            Clips.data_items = newClips.ToArray();

        }
        public void CreateAnimationsMap(AnimationMapEntry[] anims)
        {
            var numAnimBuckets = GetNumHashBuckets(anims?.Length ?? 0);
            var animBuckets = new List<AnimationMapEntry>[numAnimBuckets];
            if (anims != null)
            {
                foreach (var ame in anims)
                {
                    var b = ame.Hash % numAnimBuckets;
                    var bucket = animBuckets[b];
                    if (bucket == null)
                    {
                        bucket = new List<AnimationMapEntry>();
                        animBuckets[b] = bucket;
                    }
                    bucket.Add(ame);
                }
            }

            var newAnims = new AnimationMapEntry[animBuckets.Length];
            for (int bucketIndex = 0; bucketIndex < animBuckets.Length; bucketIndex++)
            {
                var b = animBuckets[bucketIndex];
                if (b is { Count: > 0 })
                {
                    newAnims[bucketIndex] = b[0];
                    var p = b[0];
                    for (int i = 1; i < b.Count; i++)
                    {
                        var c = b[i];
                        c.NextEntry = null;
                        p.NextEntry = c;
                        p = c;
                    }
                }
            }

            Animations = new AnimationMap();
            Animations.Animations = new ResourcePointerArray64<AnimationMapEntry>();
            Animations.Animations.data_items = newAnims.ToArray();



        }

        public static uint GetNumHashBuckets(int nHashes)
        {
            if (nHashes < 11) return 11;
            else if (nHashes < 29) return 29;
            else if (nHashes < 59) return 59;
            else if (nHashes < 107) return 107;
            else if (nHashes < 191) return 191;
            else if (nHashes < 331) return 331;
            else if (nHashes < 563) return 563;
            else if (nHashes < 953) return 953;
            else if (nHashes < 1609) return 1609;
            else if (nHashes < 2729) return 2729;
            else if (nHashes < 4621) return 4621;
            else if (nHashes < 7841) return 7841;
            else if (nHashes < 13297) return 13297;
            else if (nHashes < 22571) return 22571;
            else if (nHashes < 38351) return 38351;
            else if (nHashes < 65167) return 65167;
            else /*if (nHashes < 65521)*/ return 65521;
            //return ((uint)nHashes / 4) * 4 + 3;
        }

    }


    [TypeConverter(typeof(ExpandableObjectConverter))] public class AnimationMap : ResourceSystemBlock
    {
        public override long BlockLength
        {
            get { return 48; }
        }

        // structure data
        public uint VFT { get; set; }
        public uint BaseReferenceCount { get; set; } = 1;
        public ulong PagesInfoPointer { get; set; }
        public uint ReferenceCount { get; set; }
        public ulong AnimationsPointer { get; set; }
        public ushort AnimationsMapCapacity { get; set; }
        public ushort AnimationsMapEntries { get; set; }
        public uint AnimationsMapFlags { get; set; } = 0x01000000;
        public bool UsesBaseNameKeys { get; set; } = true;

        // reference data
        public ResourcePointerArray64<AnimationMapEntry>? Animations { get; set; }

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            // read structure data
            this.VFT = reader.ReadUInt32();
            this.BaseReferenceCount = reader.ReadUInt32();
            this.PagesInfoPointer = reader.ReadUInt64();
            this.ReferenceCount = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            this.AnimationsPointer = reader.ReadUInt64();
            this.AnimationsMapCapacity = reader.ReadUInt16();
            this.AnimationsMapEntries = reader.ReadUInt16();
            this.AnimationsMapFlags = reader.ReadUInt32();
            this.UsesBaseNameKeys = reader.ReadByte() != 0;
            _ = reader.ReadBytes(7);

            // read reference data
            this.Animations = reader.ReadBlockAt<ResourcePointerArray64<AnimationMapEntry>>(
                this.AnimationsPointer, // offset
                this.AnimationsMapCapacity
            );
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            // update structure data
            this.AnimationsPointer = (ulong)(this.Animations != null ? this.Animations.FilePosition : 0);
            this.AnimationsMapCapacity = (ushort)(this.Animations != null ? this.Animations.Count : 0);
            //this.AnimationsMapEntries //this is already set by ClipDictionary

            // write structure data
            writer.Write(this.VFT);
            writer.Write(this.BaseReferenceCount);
            writer.Write(this.PagesInfoPointer);
            writer.Write(this.ReferenceCount);
            writer.Write(0u);
            writer.Write(this.AnimationsPointer);
            writer.Write(this.AnimationsMapCapacity);
            writer.Write(this.AnimationsMapEntries);
            writer.Write(this.AnimationsMapFlags);
            writer.Write((byte)(this.UsesBaseNameKeys ? 1 : 0));
            writer.Write(new byte[7]);
        }

        public override IResourceBlock[] GetReferences()
        {
            var list = new List<IResourceBlock>();
            if (Animations != null) list.Add(Animations);
            return list.ToArray();
        }
    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class AnimationMapEntry : ResourceSystemBlock
    {
        public override long BlockLength
        {
            get { return 32; }
        }

        // structure data
        public MetaHash Hash { get; set; }
        public ulong AnimationPtr { get; set; }
        public ulong NextEntryPtr { get; set; }

        // reference data
        public Animation? Animation { get; set; }
        public AnimationMapEntry? NextEntry { get; set; }

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            // read structure data
            this.Hash = new MetaHash(reader.ReadUInt32());
            _ = reader.ReadUInt32();
            this.AnimationPtr = reader.ReadUInt64();
            this.NextEntryPtr = reader.ReadUInt64();
            _ = reader.ReadUInt64();

            // read reference data
            this.Animation = reader.ReadBlockAt<Animation>(
                this.AnimationPtr // offset
            );
            this.NextEntry = reader.ReadBlockAt<AnimationMapEntry>(
                this.NextEntryPtr // offset
            );

            if (Animation != null)
            {
                if (Animation.Hash != 0)
                { }
                Animation.Hash = Hash;
            }
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            // update structure data
            this.AnimationPtr = (ulong)(this.Animation != null ? this.Animation.FilePosition : 0);
            this.NextEntryPtr = (ulong)(this.NextEntry != null ? this.NextEntry.FilePosition : 0);

            // write structure data
            writer.Write(this.Hash);
            writer.Write(0u);
            writer.Write(this.AnimationPtr);
            writer.Write(this.NextEntryPtr);
            writer.Write(0ul);
        }

        public override IResourceBlock[] GetReferences()
        {
            var list = new List<IResourceBlock>();
            if (Animation != null) list.Add(Animation);
            if (NextEntry != null) list.Add(NextEntry);
            return list.ToArray();
        }

        public override string ToString()
        {
            return Hash.ToString();
        }

    }
    [Flags]
    public enum AnimationFlags : ushort
    {
        None = 0,
        Looped = 1 << 0,
        Raw = 1 << 3,
        MoverTracks = 1 << 4,
        Packed = 1 << 8,
        Compact = 1 << 10,
    }

    [TypeConverter(typeof(ExpandableObjectConverter))] public class Animation : ResourceSystemBlock, IMetaXmlItem
    {
        public override long BlockLength
        {
            get { return 96; }
        }

        // structure data
        public uint VFT { get; set; }
        public uint ReferenceCount { get; set; } = 1;
        public AnimationFlags Flags { get; set; } = AnimationFlags.Packed;
        public ushort ProjectFlags { get; set; }
        public ushort Frames { get; set; }
        public ushort FramesPerChunk { get; set; }
        public ushort SequenceFrameLimit { get => FramesPerChunk; set => FramesPerChunk = value; }
        public float Duration { get; set; }
        public MetaHash Signature { get; set; }
        public ulong NamePointer { get; set; }
        public uint MaxBlockSize { get; set; }
        public uint UsageCount { get; set; }
        public ResourcePointerList64<Sequence> Sequences { get; set; } = new();
        public ResourceSimpleList64_s<AnimationBoneId> BoneIds { get; set; } = new();

        public YcdFile? Ycd { get; set; }

        public MetaHash Hash { get; set; } //updated by CW, for use when reading/writing files
        public string Name { get; set; } = string.Empty;
        private string_r? NameBlock;

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            // read structure data
            this.VFT = reader.ReadUInt32();
            this.ReferenceCount = reader.ReadUInt32();
            _ = reader.ReadUInt64();
            this.Flags = (AnimationFlags)reader.ReadUInt16();
            this.ProjectFlags = reader.ReadUInt16();
            this.Frames = reader.ReadUInt16(); //221   17      151     201     frames
            this.FramesPerChunk = reader.ReadUInt16();
            this.Duration = reader.ReadSingle(); //7.34  0.53    5.0     6.66    duration
            this.Signature = reader.ReadUInt32();
            this.NamePointer = reader.ReadUInt64();
            _ = reader.ReadBytes(16); // m_Tracks is stripped from runtime resources.
            this.MaxBlockSize = reader.ReadUInt32();
            this.UsageCount = reader.ReadUInt32(); //2     2       2       2      
            this.Sequences = reader.ReadRequiredBlock<ResourcePointerList64<Sequence>>();
            this.BoneIds = reader.ReadRequiredBlock<ResourceSimpleList64_s<AnimationBoneId>>();
            this.Name = reader.ReadStringAt(this.NamePointer) ?? string.Empty;

            AssignSequenceBoneIds();
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            //BuildSequencesData();
            this.NamePointer = (ulong)(NameBlock?.FilePosition ?? 0);

            // write structure data
            writer.Write(this.VFT);
            writer.Write(this.ReferenceCount);
            writer.Write(0ul);
            writer.Write((ushort)this.Flags);
            writer.Write(this.ProjectFlags);
            writer.Write(this.Frames);
            writer.Write(this.FramesPerChunk);
            writer.Write(this.Duration);
            writer.Write(this.Signature);
            writer.Write(this.NamePointer);
            writer.Write(new byte[16]);
            writer.Write(this.MaxBlockSize);
            writer.Write(this.UsageCount);
            writer.WriteBlock(this.Sequences);
            writer.WriteBlock(this.BoneIds);
        }

        public override Tuple<long, IResourceBlock>[] GetParts()
        {
            BuildSequencesData();

            return new Tuple<long, IResourceBlock>[] {
                new Tuple<long, IResourceBlock>(0x40, Sequences),
                new Tuple<long, IResourceBlock>(0x50, BoneIds)
            };
        }

        public override IResourceBlock[] GetReferences()
        {
            NameBlock = string.IsNullOrEmpty(Name) ? null : (string_r)Name;
            return NameBlock == null ? [] : [NameBlock];
        }


        public void AssignSequenceBoneIds()
        {
            if (Sequences?.data_items != null)
            {
                foreach (var seq in Sequences.data_items)
                {
                    for (int i = 0; i < seq?.Sequences?.Length; i++)
                    {
                        if (i < BoneIds?.data_items?.Length)
                        {
                            seq.Sequences[i].BoneId = BoneIds.data_items[i];
                        }
                    }
                }
            }
        }

        public void CalculateMaxSeqBlockLength()
        {
            if (Sequences?.data_items != null)
            {
                uint maxSize = 0;
                foreach (var seq in Sequences.data_items)
                {
                    maxSize = Math.Max(maxSize, (uint)seq.BlockLength);
                }
                MaxBlockSize = maxSize;
            }
        }

        public void BuildSequencesData()
        {
            AssignSequenceBoneIds();

            if (Sequences?.data_items != null)
            {
                foreach (var seq in Sequences.data_items)
                {
                    seq.BuildData();
                }
            }

            CalculateMaxSeqBlockLength();
        }


        public void WriteXml(StringBuilder sb, int indent)
        {
            YcdXml.StringTag(sb, indent, "Hash", YcdXml.HashString(Hash));
            if (!string.IsNullOrEmpty(Name)) YcdXml.StringTag(sb, indent, "Name", MetaXml.XmlEscape(Name));
            YcdXml.ValueTag(sb, indent, "Flags", ((ushort)Flags).ToString());
            YcdXml.ValueTag(sb, indent, "FrameCount", Frames.ToString());
            YcdXml.ValueTag(sb, indent, "SequenceFrameLimit", FramesPerChunk.ToString());
            YcdXml.ValueTag(sb, indent, "Duration", FloatUtil.ToString(Duration));
            YcdXml.StringTag(sb, indent, "Signature", YcdXml.HashString(Signature));
            YcdXml.WriteItemArray(sb, BoneIds.data_items, indent, "BoneIds");
            YcdXml.WriteItemArray(sb, Sequences.data_items, indent, "Sequences");
        }
        public void ReadXml(XmlNode node)
        {
            Hash = XmlMeta.GetHash(Xml.GetChildInnerText(node, "Hash"));
            Name = Xml.GetChildInnerText(node, "Name") ?? string.Empty;
            var flagsNode = node.SelectSingleNode("Flags");
            Flags = (AnimationFlags)(flagsNode != null
                ? Xml.GetUIntAttribute(flagsNode, "value")
                : Xml.GetChildUIntAttribute(node, "Unknown10", "value") | 0x100u);
            Frames = (ushort)Xml.GetChildUIntAttribute(node, "FrameCount", "value");
            FramesPerChunk = (ushort)Xml.GetChildUIntAttribute(node, "SequenceFrameLimit", "value");
            Duration = Xml.GetChildFloatAttribute(node, "Duration", "value");
            var signatureText = Xml.GetChildInnerText(node, "Signature");
            if (string.IsNullOrEmpty(signatureText)) signatureText = Xml.GetChildInnerText(node, "Unknown1C");
            Signature = XmlMeta.GetHash(signatureText);

            BoneIds = new ResourceSimpleList64_s<AnimationBoneId>();
            BoneIds.data_items = XmlMeta.ReadItemArray<AnimationBoneId>(node, "BoneIds");

            Sequences = new ResourcePointerList64<Sequence>();
            Sequences.data_items = XmlMeta.ReadItemArrayNullable<Sequence>(node, "Sequences") ?? [];

            AssignSequenceBoneIds();
        }



        public struct FramePosition
        {
            public int Frame0;
            public int Frame1;
            public float Alpha0;
            public float Alpha1;
        }
        public FramePosition GetFramePosition(float t)
        {
            FramePosition p = new();
            if (Frames == 0 || Duration <= 0.0f) return p;
            var nframes = Math.Max(Frames - 1, 1);

            var curPos = Math.Clamp(t / Duration, 0.0f, 1.0f) * nframes;
            p.Frame0 = Math.Min((int)curPos, Frames - 1);
            p.Frame1 = Math.Min(p.Frame0 + 1, Frames - 1);
            p.Alpha1 = (float)(curPos - Math.Floor(curPos));
            p.Alpha0 = 1.0f - p.Alpha1;

            return p;
        }
        public Vector4 EvaluateVector4(FramePosition frame, int boneIndex, bool interpolate)
        {
            if (FramesPerChunk == 0) return Vector4.Zero;
            var v0 = GetAnimSequence(frame.Frame0, boneIndex, out var f0)?.EvaluateVector(f0) ?? Vector4.Zero;
            var v1 = GetAnimSequence(frame.Frame1, boneIndex, out var f1)?.EvaluateVector(f1) ?? v0;
            var v = interpolate ? (v0 * frame.Alpha0) + (v1 * frame.Alpha1) : v0;
            return v;
        }
        public Quaternion EvaluateQuaternion(FramePosition frame, int boneIndex, bool interpolate)
        {
            if (FramesPerChunk == 0) return Quaternion.Identity;
            var q0 = GetAnimSequence(frame.Frame0, boneIndex, out var f0)?.EvaluateQuaternion(f0) ?? Quaternion.Identity;
            var q1 = GetAnimSequence(frame.Frame1, boneIndex, out var f1)?.EvaluateQuaternion(f1) ?? q0;
            var q = interpolate ? QuaternionExtension.FastLerp(q0, q1, frame.Alpha1) : q0;
            return q;
        }

        private AnimSequence? GetAnimSequence(int frame, int boneIndex, out int localFrame)
        {
            localFrame = frame % FramesPerChunk;
            var sequenceIndex = frame / FramesPerChunk;
            var blocks = Sequences?.data_items;
            if ((uint)sequenceIndex >= (uint)(blocks?.Length ?? 0)) return null;
            var sequences = blocks![sequenceIndex]?.Sequences;
            return (uint)boneIndex < (uint)(sequences?.Length ?? 0) ? sequences![boneIndex] : null;
        }

        public int FindBoneIndex(ushort boneTag, byte track)
        {
            if (BoneIds?.data_items != null)
            {
                for (int i = 0; i < BoneIds.data_items.Length; i++)
                {
                    var b = BoneIds.data_items[i];
                    if ((b.BoneId == boneTag) && (b.Track == track)) return i;
                }
            }
            return -1;
        }

    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public struct AnimationBoneId : IMetaXmlItem
    {
        public ushort BoneId { get; set; }
        public byte Type { get; set; }
        public byte Track { get; set; }

        public override string ToString()
        {
            return BoneId.ToString() + ": " + Type.ToString() + ", " + Track.ToString();
        }

        public void WriteXml(StringBuilder sb, int indent)
        {
            YcdXml.ValueTag(sb, indent, "BoneId", BoneId.ToString());
            YcdXml.ValueTag(sb, indent, "Track", Track.ToString());
            YcdXml.ValueTag(sb, indent, "Type", Type.ToString());
        }
        public void ReadXml(XmlNode node)
        {
            BoneId = (ushort)Xml.GetChildUIntAttribute(node, "BoneId", "value");
            Track = (byte)Xml.GetChildUIntAttribute(node, "Track", "value");
            var typeNode = node.SelectSingleNode("Type");
            Type = (byte)(typeNode != null
                ? Xml.GetUIntAttribute(typeNode, "value")
                : Xml.GetChildUIntAttribute(node, "Unk0", "value"));
        }
    }

    public enum AnimChannelType : int
    {
        StaticQuaternion = 0,
        StaticVector3 = 1,
        StaticFloat = 2,
        RawFloat = 3,
        QuantizeFloat = 4,
        IndirectQuantizeFloat = 5,
        LinearFloat = 6,
        ReconstructQuaternion = 7,
        NormalizeQuaternion = 8,
        CachedQuaternion1 = ReconstructQuaternion,
        CachedQuaternion2 = NormalizeQuaternion,
    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public abstract class AnimChannel : IMetaXmlItem
    {
        public AnimChannelType Type { get; set; }
        public int Sequence { get; set; }
        public int Index { get; set; }

        public int DataOffset { get; set; }
        public int FrameOffset { get; set; }

        public abstract void Read(AnimChannelDataReader reader);
        public virtual void Write(AnimChannelDataWriter writer)
        { }
        public virtual void ReadFrame(AnimChannelDataReader reader)
        { }
        public virtual void WriteFrame(AnimChannelDataWriter writer)
        { }

        public virtual int GetReferenceIndex()
        { return Index; }
        public virtual int GetFrameBits()
        { return 0; }

        public virtual float EvaluateFloat(int frame) => 0.0f;

        public void Associate(int sequence, int index)
        {
            Sequence = sequence;
            Index = index;
        }

        public virtual void WriteXml(StringBuilder sb, int indent)
        {
            YcdXml.ValueTag(sb, indent, "Type", Type.ToString());
            //YcdXml.ValueTag(sb, indent, "Sequence", Sequence.ToString());
            //YcdXml.ValueTag(sb, indent, "Index", Index.ToString());
        }
        public virtual void ReadXml(XmlNode node)
        {
            //not necessary to read Type as it's already read and set in constructor
            //Type = Xml.GetEnumValue<AnimChannelType>(Xml.GetChildStringAttribute(node, "Type", "value"));
            //Sequence = Xml.GetChildIntAttribute(node, "Sequence", "value");
            //Index = Xml.GetChildIntAttribute(node, "Index", "value");
        }


        public static AnimChannel ConstructChannel(AnimChannelType type)
        {
            switch (type)
            {
                case AnimChannelType.StaticQuaternion:
                    return new AnimChannelStaticQuaternion();
                case AnimChannelType.StaticVector3:
                    return new AnimChannelStaticVector3();
                case AnimChannelType.StaticFloat:
                    return new AnimChannelStaticFloat();
                case AnimChannelType.RawFloat:
                    return new AnimChannelRawFloat();
                case AnimChannelType.QuantizeFloat:
                    return new AnimChannelQuantizeFloat();
                case AnimChannelType.IndirectQuantizeFloat:
                    return new AnimChannelIndirectQuantizeFloat();
                case AnimChannelType.LinearFloat:
                    return new AnimChannelLinearFloat();
                case AnimChannelType.ReconstructQuaternion:
                    return new AnimChannelCachedQuaternion(AnimChannelType.ReconstructQuaternion);
                case AnimChannelType.NormalizeQuaternion:
                    return new AnimChannelCachedQuaternion(AnimChannelType.NormalizeQuaternion);
                default:
                    throw new InvalidDataException($"Unsupported animation channel type: {type}.");
            }

        }

        public override string ToString()
        {
            return Sequence.ToString() + ": " + Index.ToString() + ": " + Type.ToString() + "   DataOffset: " + DataOffset.ToString() + "   FrameOffset: " + FrameOffset.ToString();
        }
    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class AnimChannelStaticFloat : AnimChannel
    {
        public float Value { get; set; }

        public AnimChannelStaticFloat()
        {
            Type = AnimChannelType.StaticFloat;
        }

        public override void Read(AnimChannelDataReader reader)
        {
            Value = reader.ReadSingle();
        }
        public override void Write(AnimChannelDataWriter writer)
        {
            writer.Write(Value);
        }

        public override float EvaluateFloat(int frame)
        {
            return Value;
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            YcdXml.ValueTag(sb, indent, "Value", FloatUtil.ToString(Value));
        }
        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            Value = Xml.GetChildFloatAttribute(node, "Value", "value");
        }
    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class AnimChannelStaticVector3 : AnimChannel
    {
        public Vector3 Value { get; set; }

        public AnimChannelStaticVector3()
        {
            Type = AnimChannelType.StaticVector3;
        }

        public override void Read(AnimChannelDataReader reader)
        {
            Value = reader.ReadVector3();
        }
        public override void Write(AnimChannelDataWriter writer)
        {
            writer.Write(Value);
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            YcdXml.SelfClosingTag(sb, indent, "Value " + FloatUtil.GetVector3XmlString(Value));
        }
        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            Value = Xml.GetChildVector3Attributes(node, "Value");
        }
    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class AnimChannelStaticQuaternion : AnimChannel
    {
        public Quaternion Value { get; set; }

        public AnimChannelStaticQuaternion()
        {
            Type = AnimChannelType.StaticQuaternion;
        }

        public override void Read(AnimChannelDataReader reader)
        {
            var vec = reader.ReadVector3();

            Value = new Quaternion(
                vec,
                (float)Math.Sqrt(Math.Max(1.0f - vec.LengthSquared(), 0.0))
            );
        }
        public override void Write(AnimChannelDataWriter writer)
        {
            writer.Write(Value.ToVector4().XYZ());//heh
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            YcdXml.SelfClosingTag(sb, indent, "Value " + FloatUtil.GetVector4XmlString(Value.ToVector4()));
        }
        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            Value = new Quaternion(Xml.GetChildVector4Attributes(node, "Value"));
        }
    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class AnimChannelIndirectQuantizeFloat : AnimChannel
    {
        public int FrameBits { get; set; }
        public int ValueBits { get; set; }
        public int NumInts { get; set; }
        public float Quantum { get; set; }
        public float Offset { get; set; }
        public float[] Values { get; set; } = [];
        public uint[] ValueList { get; set; } = [];
        public uint[] Frames { get; set; } = [];


        public AnimChannelIndirectQuantizeFloat()
        {
            Type = AnimChannelType.IndirectQuantizeFloat;
        }

        public override void Read(AnimChannelDataReader reader)
        {
            FrameBits = reader.ReadInt32();
            ValueBits = reader.ReadInt32();
            NumInts = reader.ReadInt32();
            Quantum = reader.ReadSingle();
            Offset = reader.ReadSingle();

            Frames = new uint[reader.NumFrames];

            if (ValueBits <= 0 || ValueBits > 32) throw new InvalidDataException($"Invalid indirect value width {ValueBits}.");
            if (FrameBits <= 0 || FrameBits > 31) throw new InvalidDataException($"Invalid indirect index width {FrameBits}.");
            var packedValueCapacity = (NumInts * 32) / ValueBits;
            var indexCapacity = 1 << FrameBits;
            var numValues = Math.Min(packedValueCapacity, indexCapacity);
            Values = new float[numValues];
            ValueList = new uint[numValues];
            reader.BitPosition = reader.Position * 8;
            for (int i = 0; i < numValues; i++)
            {
                uint bits = reader.ReadBits(ValueBits);
                Values[i] = (bits * Quantum) + Offset;
                ValueList[i] = bits;
            }
            reader.Position += NumInts * 4;
        }
        public override void Write(AnimChannelDataWriter writer)
        {
            if (Values.Length == 0) throw new InvalidDataException("An indirect channel must contain at least one value.");
            if (Frames.Length != writer.NumFrames) throw new InvalidDataException("Indirect channel index count must match the block frame count.");
            if (Frames.Any(index => index >= Values.Length)) throw new InvalidDataException("Indirect channel contains an out-of-range value index.");

            var frameBits = Math.Max(writer.BitCount((uint)(Values.Length - 1)), 2);
            //if ((frameBits != FrameBits)&&(ValueList!=null))
            //{ } // ######### DEBUG TEST
            FrameBits = frameBits;

            var valueCount = Values.Length;
            var valueList = new uint[valueCount];
            for (int i = 0; i < valueCount; i++)
            {
                var bits = GetQuanta(Values[i]);
                valueList[i] = bits;

                //if (ValueList != null) // ######### DEBUG TEST
                //{
                //    var testbits = ValueList[i];
                //    if (bits != testbits)
                //    { }
                //}
            }
            var valueBits = Math.Max(writer.BitCount(valueList), 3);
            //if ((valueBits != ValueBits)&&(ValueList!=null))
            //{ }// ######### DEBUG TEST
            ValueBits = valueBits;

            writer.ResetBitstream();
            for (int i = 0; i < valueCount; i++)
            {
                var u = valueList[i];
                writer.WriteBits(u, ValueBits);
            }

            NumInts = writer.Bitstream.Count;


            writer.Write(FrameBits);
            writer.Write(ValueBits);
            writer.Write(NumInts);
            writer.Write(Quantum);
            writer.Write(Offset);
            writer.WriteBitstream();
        }

        public override void ReadFrame(AnimChannelDataReader reader)
        {
            Frames[reader.Frame] = reader.ReadFrameBits(FrameBits);
        }
        public override void WriteFrame(AnimChannelDataWriter writer)
        {
            writer.WriteFrameBits(Frames[writer.Frame], FrameBits);
        }

        public override int GetFrameBits()
        {
            return FrameBits;
        }


        private uint GetQuanta(float v)
        {
            if (Quantum == 0.0f) return 0;
            var q = (v - Offset) / Quantum;
            return (uint)(q + 0.5f);
            //return (uint)Math.Round(q, 0);//any better way?
        }


        public override float EvaluateFloat(int frame)
        {
            if (Frames?.Length > 0) return Values[Frames[frame % Frames.Length]];
            return Offset;
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            YcdXml.ValueTag(sb, indent, "Quantum", FloatUtil.ToString(Quantum));
            YcdXml.ValueTag(sb, indent, "Offset", FloatUtil.ToString(Offset));
            YcdXml.WriteRawArray(sb, Values, indent, "Values", "", FloatUtil.ToString, 10);// (Values.Length) + 1);
            YcdXml.WriteRawArray(sb, Frames, indent, "Frames", "", null, 10);// (Frames?.Length ?? 0) + 1);
        }
        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            Quantum = Xml.GetChildFloatAttribute(node, "Quantum", "value");
            Offset = Xml.GetChildFloatAttribute(node, "Offset", "value");
            Values = Xml.GetChildRawFloatArray(node, "Values");
            Frames = Xml.GetChildRawUintArray(node, "Frames");
        }
    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class AnimChannelQuantizeFloat : AnimChannel
    {
        public int ValueBits { get; set; }
        public float Quantum { get; set; }
        public float Offset { get; set; }
        public float[] Values { get; set; } = [];
        public uint[] ValueList { get; set; } = [];

        public AnimChannelQuantizeFloat()
        {
            Type = AnimChannelType.QuantizeFloat;
        }

        public override void Read(AnimChannelDataReader reader)
        {
            ValueBits = reader.ReadInt32();
            Quantum = reader.ReadSingle();
            Offset = reader.ReadSingle();
            Values = new float[reader.NumFrames];
            ValueList = new uint[reader.NumFrames];

            if (ValueBits < 1)
            { }
        }
        public override void Write(AnimChannelDataWriter writer)
        {
            var valueCount = Values.Length;
            var valueList = new uint[valueCount];
            for (int i = 0; i < valueCount; i++)
            {
                var bits =  GetQuanta(Values[i]);
                valueList[i] = bits;
            }
            var valueBits = Math.Max(writer.BitCount(valueList), 1);
            //if ((valueBits != ValueBits)&&(ValueList!=null))
            //{ } // ######### DEBUG TEST
            ValueBits = valueBits;

            writer.Write(ValueBits);
            writer.Write(Quantum);
            writer.Write(Offset);
        }

        public override void ReadFrame(AnimChannelDataReader reader)
        {
            uint bits = reader.ReadFrameBits(ValueBits);
            float val = (bits * Quantum) + Offset;
            Values[reader.Frame] = val;
            ValueList[reader.Frame] = bits;
        }
        public override void WriteFrame(AnimChannelDataWriter writer)
        {
            uint bits = GetQuanta(Values[writer.Frame]);
            writer.WriteFrameBits(bits, ValueBits);
        }

        public override int GetFrameBits()
        {
            return ValueBits;
        }


        private uint GetQuanta(float v)
        {
            if (Quantum == 0.0f) return 0;
            var q = (v - Offset) / Quantum;
            return (uint)(q + 0.5f);
            //return (uint)Math.Round(q, 0);//any better way?
        }


        public override float EvaluateFloat(int frame)
        {
            if (Values?.Length > 0) return Values[frame%Values.Length];
            return Offset;
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            YcdXml.ValueTag(sb, indent, "Quantum", FloatUtil.ToString(Quantum));
            YcdXml.ValueTag(sb, indent, "Offset", FloatUtil.ToString(Offset));
            YcdXml.WriteRawArray(sb, Values, indent, "Values", "", FloatUtil.ToString, 10);// (Values.Length) + 1);
        }
        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            Quantum = Xml.GetChildFloatAttribute(node, "Quantum", "value");
            Offset = Xml.GetChildFloatAttribute(node, "Offset", "value");
            Values = Xml.GetChildRawFloatArray(node, "Values");
        }
    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class AnimChannelLinearFloat : AnimChannel
    {
        private int NumInts { get; set; }
        private int Counts { get; set; }
        public float Quantum { get; set; }
        public float Offset { get; set; }

        private int Bit { get; set; }    //chunks start bit
        private int Count1 { get; set; } //number of offset bits for each chunk 
        private int Count2 { get; set; } //number of value bits for each chunk
        private int Count3 { get; set; } //number of delta bits for each frame

        public float[] Values { get; set; } = [];
        public int[] ValueList { get; set; } = [];


        public AnimChannelLinearFloat()
        {
            Type = AnimChannelType.LinearFloat;
        }

        public override void Read(AnimChannelDataReader reader)
        {
            var channelStart = reader.Position;
            NumInts = reader.ReadInt32();
            Counts = reader.ReadInt32();
            Quantum = reader.ReadSingle();
            Offset = reader.ReadSingle();

            Bit = (reader.Position * 8);    //chunks start bit
            Count1 = Counts & 0xFF;         //number of offset bits for each chunk 
            Count2 = (Counts >> 8) & 0xFF;  //number of value bits for each chunk
            Count3 = (Counts >> 16) & 0xFF; //number of delta bits for each frame

            var streamLength = (reader.Data?.Length ?? 0) * 8;
            var numFrames = reader.NumFrames;
            var chunkSize = reader.ChunkSize;//64 or 255(-1?)
            var numChunks = (ushort)((chunkSize + numFrames - 1) / chunkSize);
            var deltaOffset = Bit + (numChunks * (Count1 + Count2));//base offset to delta bits

            reader.BitPosition = Bit;
            var chunkOffsets = new int[numChunks];
            var chunkValues = new int[numChunks];
            var frameValues = new float[numFrames];
            var frameBits = new int[numFrames];
            for (int i = 0; i < numChunks; i++)
            {
                chunkOffsets[i] = (Count1 > 0) ? (int)reader.ReadBits(Count1) : 0;
            }
            for (int i = 0; i < numChunks; i++)
            {
                chunkValues[i] = (Count2 > 0) ? (int)reader.ReadBits(Count2) : 0;
            }
            for (int i = 0; i < numChunks; i++)
            {
                var doffs = chunkOffsets[i] + deltaOffset;//bit offset for chunk deltas
                var value = chunkValues[i];//chunk start frame value
                var cframe = (i * chunkSize);//chunk start frame
                ////if ((reader.BitPosition != doffs))
                ////{ }
                reader.BitPosition = doffs;
                var inc = 0;
                for (int j = 0; j < chunkSize; j++)
                {
                    int frame = cframe + j;
                    if (frame >= numFrames) break;

                    frameValues[frame] = (value * Quantum) + Offset;
                    frameBits[frame] = value;

                    if ((frame + 1) >= numFrames || (j + 1) >= chunkSize) break;

                    var delta = (Count3 != 0) ? (int)reader.ReadBits(Count3) : 0;
                    var so = reader.BitPosition;
                    var maxso = streamLength;//Math.Min(so + 32 - Count3, streamLength); // 
                    uint b = 0;
                    while (b == 0)  // scan for a '1' bit
                    {
                        b = reader.ReadBits(1);
                        if (reader.BitPosition >= maxso)
                        { break; } //trying to read more than 32 bits, or end of data... don't get into an infinite loop..!
                    }
                    delta |= ((reader.BitPosition - so - 1) << Count3); //add the found bit onto the delta. (position-so-1) is index of found bit 
                    if (delta != 0)
                    {
                        var sign = reader.ReadBits(1);
                        if (sign == 1)
                        {
                            delta = -delta;
                        }
                    }
                    inc += delta;
                    value += inc;
                }
            }
            Values = frameValues;
            ValueList = frameBits;


            reader.Position = checked(channelStart + (NumInts * 4));
        }
        public override void Write(AnimChannelDataWriter writer)
        {
            var numFrames = writer.NumFrames;
            byte chunkSize = 64; //seems to always be 64 for this
            var numChunks = (ushort)((numFrames + chunkSize - 1) / chunkSize);
            if (writer.ChunkSize != chunkSize)
            { writer.ChunkSize = chunkSize; }

            var valueCount = Values.Length;
            var valueList = new int[valueCount];
            for (int i = 0; i < valueCount; i++)
            {
                var bits = GetQuanta(Values[i]);
                valueList[i] = bits;
            }


            var chunkOffsets = new uint[numChunks];
            var chunkValues = new uint[numChunks];
            var chunkDeltas = new int[numChunks][];
            var allDeltas = new List<int>(numFrames);
            for (int i = 0; i < numChunks; i++)
            {
                var cframe = (i * chunkSize);//chunk start frame
                var cvalue = (cframe < valueCount) ? valueList[cframe] : valueList[0];
                var cdeltas = new int[chunkSize];
                var cinc = 0;
                chunkValues[i] = (uint)cvalue;
                chunkDeltas[i] = cdeltas;
                for (int j = 1; j < chunkSize; j++)
                {
                    int frame = cframe + j;
                    if (frame >= numFrames) break;
                    var value = valueList[frame];
                    var inc = value - cvalue;
                    var delta = inc - cinc;
                    cinc = inc;
                    cvalue = value;
                    cdeltas[j] = delta;
                    allDeltas.Add(delta);
                }
            }
            Count3 = FindBestRiceDivisor(allDeltas);
            uint coffset = 0;
            for (int i = 0; i < numChunks; i++)
            {
                chunkOffsets[i] = coffset;
                var cdeltas = chunkDeltas[i];
                for (int j = 1; j < chunkSize; j++)
                {
                    if ((i * chunkSize) + j >= numFrames) break;
                    var delta = cdeltas[j];
                    var magnitude = GetMagnitude(delta);
                    coffset += (uint)Count3 + (magnitude >> Count3) + (delta != 0 ? 2u : 1u);
                }
            }
            Count1 = writer.BitCount(chunkOffsets); //number of offset bits for each chunk
            Count2 = writer.BitCount(chunkValues); //number of value bits for each chunk



            writer.ResetBitstream();
            if (Count1 > 0) ////write chunk delta offsets
            {
                for (int i = 0; i < numChunks; i++)
                {
                    writer.WriteBits(chunkOffsets[i], Count1);
                }
            }
            if (Count2 > 0) ////write chunk start values
            {
                for (int i = 0; i < numChunks; i++)
                {
                    writer.WriteBits(chunkValues[i], Count2);
                }
            }
            for (int i = 0; i < numChunks; i++) ////write chunk frame deltas
            {
                var cdeltas = chunkDeltas[i];
                for (int j = 1; j < chunkSize; j++)
                {
                    if ((i * chunkSize) + j >= numFrames) break;
                    var delta = cdeltas[j];
                    var deltaa = GetMagnitude(delta);
                    var remainderMask = Count3 == 0 ? 0u : (1u << Count3) - 1u;
                    writer.WriteBits(deltaa & remainderMask, Count3);
                    var quotient = (int)(deltaa >> Count3);
                    writer.WriteBits(0, quotient);
                    writer.WriteBits(1, 1);//"stop" bit
                    if (delta != 0)
                    {
                        writer.WriteBits(delta < 0 ? 1u : 0u, 1);//sign bit
                    }
                }
            }


            Counts = Count1 & 0xFF;
            Counts += (Count2 & 0xFF) << 8;
            Counts += (Count3 & 0xFF) << 16;
            NumInts = 4 + writer.Bitstream.Count;

            writer.Write(NumInts);
            writer.Write(Counts);
            writer.Write(Quantum);
            writer.Write(Offset);
            writer.WriteBitstream();
        }


        public override float EvaluateFloat(int frame)
        {
            if (Values?.Length > 0) return Values[frame % Values.Length];
            return Offset;
        }


        private int GetQuanta(float v)
        {
            if (Quantum == 0.0f) return 0;
            var q = (v - Offset) / Quantum;
            return (int)(q + 0.5f);
            //return (uint)Math.Round(Math.Max(q, 0));//any better way?
        }

        private static int FindBestRiceDivisor(IReadOnlyList<int> values)
        {
            var bestDivisor = -1;
            long bestSize = long.MaxValue;
            for (var divisor = 0; divisor <= 15; divisor++)
            {
                long size = 0;
                var valid = true;
                foreach (var value in values)
                {
                    var magnitude = GetMagnitude(value);
                    var quotient = magnitude >> divisor;
                    if (quotient > 30)
                    {
                        valid = false;
                        break;
                    }
                    size += divisor + quotient + 1 + (value != 0 ? 1 : 0);
                }
                if (valid && size < bestSize)
                {
                    bestSize = size;
                    bestDivisor = divisor;
                }
            }
            if (bestDivisor < 0)
            {
                throw new InvalidDataException("Linear channel deltas cannot be represented by the native Rice encoding.");
            }
            return bestDivisor;
        }

        private static uint GetMagnitude(int value)
        {
            return (uint)(value < 0 ? -(long)value : value);
        }


        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            YcdXml.ValueTag(sb, indent, "Quantum", FloatUtil.ToString(Quantum));
            YcdXml.ValueTag(sb, indent, "Offset", FloatUtil.ToString(Offset));
            YcdXml.WriteRawArray(sb, Values, indent, "Values", "", FloatUtil.ToString, 10);// (Values.Length) + 1);
        }
        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            Quantum = Xml.GetChildFloatAttribute(node, "Quantum", "value");
            Offset = Xml.GetChildFloatAttribute(node, "Offset", "value");
            Values = Xml.GetChildRawFloatArray(node, "Values");
        }
    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class AnimChannelRawFloat : AnimChannel
    {
        public float[] Values { get; set; } = [];

        public AnimChannelRawFloat()
        {
            Type = AnimChannelType.RawFloat;
        }

        public override void Read(AnimChannelDataReader reader)
        {
            Values = new float[reader.NumFrames];
        }
        public override void Write(AnimChannelDataWriter writer)
        {
            //nothing to do here
        }

        public override void ReadFrame(AnimChannelDataReader reader)
        {
            uint bits = reader.ReadFrameBits(32);
            float v = MetaTypes.ConvertData<float>(MetaTypes.ConvertToBytes(bits));
            Values[reader.Frame] = v;
        }
        public override void WriteFrame(AnimChannelDataWriter writer)
        {
            float v = Values[writer.Frame];
            var b = BitConverter.GetBytes(v);
            var bits = BitConverter.ToUInt32(b, 0);
            writer.WriteFrameBits(bits, 32);
        }

        public override int GetFrameBits()
        {
            return 32;
        }

        public override float EvaluateFloat(int frame)
        {
            if (Values?.Length > 0) return Values[frame % Values.Length];
            return base.EvaluateFloat(frame);
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            YcdXml.WriteRawArray(sb, Values, indent, "Values", "", FloatUtil.ToString, 10);// (Values.Length) + 1);
        }
        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            Values = Xml.GetChildRawFloatArray(node, "Values");
        }
    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class AnimChannelCachedQuaternion : AnimChannel
    {
        private AnimChannelDataReader? blockStream;

        private float[]? valueCache;

        public float[] Values
        {
            get
            {
                if (valueCache != null)
                {
                    return valueCache;
                }

                if (blockStream == null || Type != AnimChannelType.ReconstructQuaternion) return [];
                valueCache = new float[blockStream.NumFrames];

                var channels = new AnimChannel[3];
                var ch = 0;

                for (int i = 0; i < 4; i++)
                {
                    if (i != 3)
                    {
                        channels[ch] = blockStream.Sequences[Sequence].Channels[i];
                        ch++;
                    }
                }

                for (int i = 0; i < valueCache.Length; i++)
                {
                    var vec = new Vector3(
                        channels[0].EvaluateFloat(i),
                        channels[1].EvaluateFloat(i),
                        channels[2].EvaluateFloat(i)
                    );

                    valueCache[i] = (float)Math.Sqrt(Math.Max(1.0f - vec.LengthSquared(), 0.0));
                }

                return valueCache;
            }
        }

        public int QuatIndex { get; set; }

        public AnimChannelCachedQuaternion(AnimChannelType type)
        {
            Type = type;
        }

        public override void Read(AnimChannelDataReader reader)
        {
            this.blockStream = reader;
        }

        public override int GetReferenceIndex()
        {
            return QuatIndex;
        }

        public override float EvaluateFloat(int frame)
        {
            if (Values?.Length > 0) return Values[frame % Values.Length];
            return 0.0f;
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            YcdXml.ValueTag(sb, indent, "QuatIndex", QuatIndex.ToString());
            //data is already written in other channels...
        }
        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            QuatIndex = Xml.GetChildIntAttribute(node, "QuatIndex", "value");
            //data was already read in other channels...
        }
    }
    public class AnimChannelDataReader
    {
        public byte[] Data { get; set; } = [];
        public ushort NumFrames { get; set; }
        public byte ChunkSize { get; set; } //stride of channel frame items (in frames)
        public int Position { get; set; } //current byte that the main data reader is on
        public int Frame { get; set; } //current frame that the reader is on
        public uint FrameOffset { get; set; } //offset to frame data items / bytes
        public ushort FrameLength { get; set; } //stride of frame data item
        public int ChannelListOffset { get; set; }//offset to channel counts
        public int ChannelDataOffset { get; set; }//offset to channel data/info ushorts
        public int ChannelFrameOffset { get; set; }//offset to channel current frame data (in bits!)
        public AnimSequence[] Sequences { get; set; } = [];//used by AnimChannelCachedQuaternion when accessing values (when evaluating)
        public int BitPosition { get; set; } //for use with ReadBits()

        public AnimChannelDataReader(byte[] data, ushort numFrames, byte chunkSize, uint frameOffset, ushort frameLength)
        {
            Data = data;
            NumFrames = numFrames;
            ChunkSize = chunkSize;
            Position = 0;
            Frame = 0;
            FrameOffset = frameOffset;
            FrameLength = frameLength;
            ChannelListOffset = (int)FrameOffset + (FrameLength * NumFrames);
            ChannelDataOffset = ChannelListOffset + (9 * 2);
            ChannelFrameOffset = 0;
        }

        public int ReadInt32()
        {
            int i = BitConverter.ToInt32(Data, Position);
            Position += 4;
            return i;
        }
        public float ReadSingle()
        {
            float f = BitConverter.ToSingle(Data, Position);
            Position += 4;
            return f;
        }
        public Vector3 ReadVector3()
        {
            var v = new Vector3();
            v.X = BitConverter.ToSingle(Data, Position);
            v.Y = BitConverter.ToSingle(Data, Position + 4);
            v.Z = BitConverter.ToSingle(Data, Position + 8);
            Position += 12;
            return v;
        }

        public uint GetBits(int startBit, int length)
        {
            //dexyfex version that won't read too many bytes - probably won't perform as well
            if (startBit < 0)
            { return 0; } //something must have went wrong reading other data... happening in  fos_ep_1_p6-35.ycd
            int startByte = startBit / 8;
            int bitOffset = startBit % 8;
            uint result = 0;
            int shift = -bitOffset;
            int curByte = startByte;
            int bitsRemaining = length;
            while (bitsRemaining > 0)
            {
                var b = (curByte < Data.Length) ? (uint)Data[curByte++] : 0;
                var sb = (shift < 0) ? (b >> -shift) : (b << shift);
                var bm = ((1u << Math.Min(bitsRemaining, 8)) - 1u) << (Math.Max(shift, 0));
                var mb = (sb & bm);
                result += mb;
                bitsRemaining -= (8 + Math.Min(shift, 0));
                shift += 8;
            }
            return result;
        }

        public uint ReadBits(int length)
        {
            uint bits = GetBits(BitPosition, length);
            BitPosition += length;
            return bits;
        }


        public ushort ReadChannelCount()
        {
            ushort channelCount = BitConverter.ToUInt16(Data, ChannelListOffset);
            ChannelListOffset += 2;
            return channelCount;
        }
        public ushort ReadChannelDataBits()
        {
            ushort channelDataBit = BitConverter.ToUInt16(Data, ChannelDataOffset);
            ChannelDataOffset += 2;
            return channelDataBit;
        }
        public byte[] ReadChannelDataBytes(int n)
        {
            var r = new byte[n];
            Buffer.BlockCopy(Data, ChannelDataOffset, r, 0, n);
            ChannelDataOffset += n;
            return r;
        }

        public void AlignChannelDataOffset(int channelCount)
        {
            int remainder = channelCount % 4;
            if (remainder > 0)
            {
                int addamt = (4 - remainder) * 2;
                ChannelDataOffset += addamt;
            }
        }


        public void BeginFrame(int f)
        {
            Frame = f;
            ChannelFrameOffset = (int)((FrameOffset + (FrameLength * f)) * 8);
        }
        public uint ReadFrameBits(int n)
        {
            uint b = GetBits(ChannelFrameOffset, n);
            ChannelFrameOffset += n;
            return b;
        }

    }
    public class AnimChannelDataWriter
    {
        public int ChannelListOffset { get; set; }//offset to channel counts
        public int ChannelItemOffset { get; set; }//offset to channel data/info ushorts
        public int ChannelFrameOffset { get; set; }//offset to channel current frame data (in bits!)
        public int Position { get; set; } //current byte that the main data reader is on
        public int Frame { get; set; } //current frame that the reader is on
        public ushort NumFrames { get; set; }
        public byte ChunkSize { get; set; } //stride of channel frame items - starts at 0 and will be set to 64 if need be
        public ushort FrameLength { get; set; } = 0; //stride of frame data item, calculated when ending frames

        MemoryStream ChannelListStream = new();
        MemoryStream ChannelItemStream = new();
        MemoryStream MainStream = new();
        BinaryWriter ChannelListWriter;
        BinaryWriter ChannelItemWriter;
        BinaryWriter MainWriter;
        public List<uint> ChannelFrameStream { get; private set; } = new List<uint>(); //frame bits stream.
        public List<uint[]> ChannelFrames { get; private set; } = new List<uint[]>();//bitstreams for each frame

        public List<uint> Bitstream { get; private set; } = new List<uint>();
        public int BitstreamPos { get; set; } = 0;

        public AnimChannelDataWriter(ushort numFrames)
        {
            Position = 0;
            Frame = 0;
            NumFrames = numFrames;
            ChunkSize = 0; //default 0 value means chunks not used
            ChannelListWriter = new BinaryWriter(ChannelListStream);
            ChannelItemWriter = new BinaryWriter(ChannelItemStream);
            MainWriter = new BinaryWriter(MainStream);
        }

        public void WriteChannelListData(ushort c)
        {
            ChannelListWriter.Write(c);
            ChannelListOffset += 2;
        }
        public void WriteChannelItemData(ushort c)
        {
            ChannelItemWriter.Write(c);
            ChannelItemOffset += 2;
        }
        public void AlignChannelItemData(int channelCount, int sequenceCount)
        {
            int remainder = channelCount % 4;
            if (remainder > 0)
            {
                ushort writeval = (ushort)(sequenceCount << 2);
                int addamt = (4 - remainder);
                for (int i = 0; i < addamt; i++)
                {
                    WriteChannelItemData(writeval);
                }
            }
        }
        public void WriteChannelItemDataBytes(byte[] data)
        {
            if (data?.Length > 0)
            {
                ChannelItemWriter.Write(data);
                ChannelItemOffset += data.Length;
            }
        }


        public void Write(int i)
        {
            MainWriter.Write(i);
            Position += 4;
        }
        public void Write(float f)
        {
            MainWriter.Write(f);
            Position += 4;
        }
        public void Write(Vector3 v)
        {
            MainWriter.Write(v.X);
            MainWriter.Write(v.Y);
            MainWriter.Write(v.Z);
            Position += 12;
        }


        public void BeginFrame(int f)
        {
            Frame = f;
            ChannelFrameStream.Clear();
            ChannelFrameOffset = 0;
        }
        public void WriteFrameBits(uint bits, int n)
        {
            WriteToBitstream(ChannelFrameStream, ChannelFrameOffset, bits, n);
            ChannelFrameOffset += n;
        }
        public void EndFrame()
        {
            FrameLength = Math.Max(FrameLength, (ushort)(ChannelFrameStream.Count * 4));
            ChannelFrames.Add(ChannelFrameStream.ToArray());
            ChannelFrameStream.Clear();
        }

        public int BitCount(uint bits)//could be static, but not for convenience
        {
            int bc = 0;
            for (int i = 0; i < 32; i++)
            {
                uint mask = 1u << i;
                if ((bits & mask) > 0) bc = (i + 1);
            }
            return bc;
        }
        public int BitCount(uint[] values)
        {
            uint maxValue = 0;
            for (int i = 0; i < values?.Length; i++)
            {
                maxValue = Math.Max(maxValue, values[i]);
            }
            return BitCount(maxValue);
        }


        public void ResetBitstream()
        {
            Bitstream.Clear();
            BitstreamPos = 0;
        }
        public void WriteBits(uint bits, int n)//write n bits to the bitstream.
        {
            WriteToBitstream(Bitstream, BitstreamPos, bits, n);
            BitstreamPos += n;
        }
        public void WriteBitstream()//write the contents of the bitstream (as uints) to the main writer
        {
            for (int i = 0; i < Bitstream.Count; i++)
            {
                MainWriter.Write(Bitstream[i]);
            }
            Position += (Bitstream.Count * 4);
        }


        private void WriteToBitstream(List<uint>? stream, int offset, uint bits, int n)
        {
            if (stream == null) return;
            if ((uint)n > 32u) throw new ArgumentOutOfRangeException(nameof(n));
            if (n == 0) return;

            uint mask = n == 32 ? uint.MaxValue : ((1u << n) - 1u);
            uint masked = bits & mask;
            if (bits != masked) throw new InvalidDataException($"Value {bits} does not fit in {n} bits.");

            int soffset = offset % 32;
            int sindex = offset / 32;
            while (sindex >= stream.Count) stream.Add(0); //pad beginning of the stream
            uint sval = stream[sindex];
            uint sbits = masked << soffset;
            stream[sindex] = sval | sbits;

            int endbit = (soffset + n) - 32;
            if (endbit > 0)
            {
                int eindex = sindex + 1;
                while (eindex >= stream.Count) stream.Add(0);//pad end of stream
                int eoffset = 32 - soffset;
                uint eval = stream[eindex];
                uint ebits = masked >> eoffset;
                stream[eindex] = eval | ebits;
            }

        }


        public byte[] GetStreamData(MemoryStream ms)
        {
            var length = (int)ms.Length;
            var data = new byte[length];
            ms.Flush();
            ms.Position = 0;
            ms.Read(data, 0, length);
            return data;
        }
        public byte[] GetChannelListDataBytes()
        {
            return GetStreamData(ChannelListStream);
        }
        public byte[] GetChannelItemDataBytes()
        {
            return GetStreamData(ChannelItemStream);
        }
        public byte[] GetMainDataBytes()
        {
            return GetStreamData(MainStream);
        }
        public byte[] GetFrameDataBytes()
        {
            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);
            var frameUintCount = FrameLength / 4;
            for (int i = 0; i < ChannelFrames.Count; i++)
            {
                var frameData = ChannelFrames[i];
                for (int f = 0; f < frameUintCount; f++)
                {
                    bw.Write((f < frameData.Length) ? frameData[f] : 0);
                }
            }
            return GetStreamData(ms);
        }

    }

    [TypeConverter(typeof(ExpandableObjectConverter))] public class AnimSequence : IMetaXmlItem
    {
        public AnimChannel[] Channels { get; set; } = [];
        public bool IsType7Quat { get; set; }
        public bool NormalizeQuaternion { get; set; }

        public AnimationBoneId BoneId { get; set; }//for convenience

        public Quaternion EvaluateQuaternionType7(int frame)
        {
            if (!IsType7Quat)
            {
                return new Quaternion(
                    Channels[0].EvaluateFloat(frame),
                    Channels[1].EvaluateFloat(frame),
                    Channels[2].EvaluateFloat(frame),
                    Channels[3].EvaluateFloat(frame)
                );
            }

            var t7 = Channels[3] as AnimChannelCachedQuaternion;//type 1
            if (t7 == null) t7 = Channels[4] as AnimChannelCachedQuaternion;//type 2

            var x = Channels[0].EvaluateFloat(frame);
            var y = Channels[1].EvaluateFloat(frame);
            var z = Channels[2].EvaluateFloat(frame);
            var normalized = (t7 ?? throw new InvalidDataException("Quaternion sequence has no cached quaternion channel.")).EvaluateFloat(frame);

            switch (t7.QuatIndex)
            {
                case 0:
                    return new Quaternion(normalized, x, y, z);
                case 1:
                    return new Quaternion(x, normalized, y, z);
                case 2:
                    return new Quaternion(x, y, normalized, z);
                case 3:
                    return new Quaternion(x, y, z, normalized);
                default:
                    return Quaternion.Identity;
            }
        }

        public Quaternion EvaluateQuaternion(int frame)
        {
            if (IsType7Quat) return EvaluateQuaternionType7(frame);
            var value = EvaluateVectorComponents(frame).ToQuaternion();
            return NormalizeQuaternion ? Quaternion.Normalize(value) : value;
        }

        public Vector4 EvaluateVector(int frame)
        {
            if (Channels == null) return Vector4.Zero;
            if (IsType7Quat) return Quaternion.Normalize(EvaluateQuaternionType7(frame)).ToVector4();//normalization shouldn't be necessary, but saves explosions in case of incorrectness
            var value = EvaluateVectorComponents(frame);
            return NormalizeQuaternion ? Quaternion.Normalize(value.ToQuaternion()).ToVector4() : value;
        }

        private Vector4 EvaluateVectorComponents(int frame)
        {
            var v = Vector4.Zero;
            int c = 0;
            for (int i = 0; i < Channels.Length; i++)
            {
                if (c >= 4) break;
                var channel = Channels[i];
                if (channel == null) continue;
                var sv3c = channel as AnimChannelStaticVector3;
                var ssqc = channel as AnimChannelStaticQuaternion;
                if (sv3c != null)
                {
                    for (int n = 0; n < 3; n++)
                    {
                        if ((c + n) >= 4) break;
                        v[c + n] = sv3c.Value[n];
                    }
                    c += 3;
                }
                else if (ssqc != null)
                {
                    for (int n = 0; n < 4; n++)
                    {
                        if ((c + n) >= 4) break;
                        v[c + n] = ssqc.Value[n];
                    }
                    c += 4;
                }
                else
                {
                    v[c] = channel.EvaluateFloat(frame);
                    c++;
                }
            }
            return v;
        }


        public void WriteXml(StringBuilder sb, int indent)
        {
            YcdXml.WriteItemArray(sb, Channels, indent, "Channels");
        }
        public void ReadXml(XmlNode node)
        {
            var chansNode = node.SelectSingleNode("Channels");
            if (chansNode != null)
            {
                var inodes = chansNode.SelectNodes("Item");
                if (inodes?.Count > 0)
                {
                    var clist = new List<AnimChannel>();
                    foreach (XmlNode inode in inodes)
                    {
                        var type = Xml.GetEnumValue<AnimChannelType>(Xml.GetChildStringAttribute(inode, "Type", "value"));
                        var c = AnimChannel.ConstructChannel(type);
                        c.ReadXml(inode);
                        clist.Add(c);
                    }
                    Channels = clist.ToArray();
                    IsType7Quat = Channels.Any(c => c?.Type == AnimChannelType.ReconstructQuaternion);
                    NormalizeQuaternion = Channels.Any(c => c?.Type == AnimChannelType.NormalizeQuaternion);
                }
            }

        }

        public override string ToString()
        {
            return "AnimSequence: " + (Channels?.Length??0).ToString() + " channels";
        }
    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class SequenceRootChannelRef : IMetaXmlItem
    {
        public byte[] Bytes { get; set; } = [];
        public byte ChannelType { get { return Bytes[0]; } set { Bytes[0] = value; } }
        public byte ChannelIndex { get { return Bytes[1]; } set { Bytes[1] = value; } }
        public ushort DataIntOffset
        {
            get { return (ushort)(Bytes[2] + (Bytes[3] << 8)); }
            set
            {
                Bytes[2] = (byte)(value & 0xFF);
                Bytes[3] = (byte)((value >> 8) & 0xFF);
            }
        }
        public ushort FrameBitOffset
        {
            get { return (ushort)(Bytes[4] + (Bytes[5] << 8)); }
            set
            {
                Bytes[4] = (byte)(value & 0xFF);
                Bytes[5] = (byte)((value >> 8) & 0xFF);
            }
        }


        public SequenceRootChannelRef()
        {
            Bytes = new byte[6];
        }
        public SequenceRootChannelRef(AnimChannelType type, int channelIndex)
        {
            Bytes = new byte[6];
            ChannelType = (byte)type;
            ChannelIndex = (byte)channelIndex;
        }
        public SequenceRootChannelRef(byte[] bytes)
        {
            Bytes = bytes;
        }
        public override string ToString()
        {
            if (Bytes?.Length >= 6)
            {
                return ChannelType.ToString() + ", " + ChannelIndex.ToString() + ", " + DataIntOffset.ToString() + ", " + FrameBitOffset.ToString();
            }
            return "(empty)";
        }

        public void WriteXml(StringBuilder sb, int indent)
        {
            YcdXml.WriteRawArray(sb, Bytes, indent, "Bytes", "", YcdXml.FormatHexByte, 6);
        }
        public void ReadXml(XmlNode node)
        {
            Bytes = Xml.GetChildRawByteArray(node, "Bytes");
        }
    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class Sequence : ResourceSystemBlock, IMetaXmlItem
    {
        public override long BlockLength
        {
            get { return 32 + (Data?.Length ?? 0); }
        }

        // structure data
        public MetaHash Hash { get; set; }
        public uint DataLength { get; set; }
        public uint CompactSize { get; set; }
        public uint ConstantSize { get; set; }
        public uint ChannelOffset { get; set; }
        public ushort SlopSize { get; set; }
        public ushort NumFrames { get; set; } // count of frame data items
        public ushort FrameSize { get; set; }
        public ushort IndirectSize { get; set; }
        public ushort QuantizeSize { get; set; }
        public byte SegmentSize { get; set; }
        public byte ChunkSize { get => SegmentSize; set => SegmentSize = value; }
        public uint FrameOffset { get => ConstantSize; set => ConstantSize = value; }
        public byte MoverChannelCounts { get; set; }
        public byte[] Data { get; set; } = [];



        // parsed data
        public AnimSequence[] Sequences { get; set; } = [];

        public SequenceRootChannelRef[] RootPositionRefs { get; set; } = [];
        public SequenceRootChannelRef[] RootRotationRefs { get; set; } = [];
        public int RootPositionRefCount
        {
            get { return (MoverChannelCounts >> 4) & 0xF; }
            set
            {
                var rrc = MoverChannelCounts & 0xF;
                MoverChannelCounts = (byte)(rrc + ((value & 0xF) << 4));
            }
        }
        public int RootRotationRefCount
        {
            get { return MoverChannelCounts & 0xF; }
            set
            {
                var rpc = (MoverChannelCounts >> 4) & 0xF;
                MoverChannelCounts = (byte)(rpc + (value & 0xF));
            }
        }


        class AnimChannelListItem
        {
            public int Sequence;
            public int Index;
            public AnimChannel Channel;
            public AnimChannelListItem(int seq, int ind, AnimChannel channel)
            {
                Sequence = seq;
                Index = ind;
                Channel = channel;
            }
        }


        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            // read structure data
            this.Hash = reader.ReadUInt32();
            this.DataLength = reader.ReadUInt32();              //282        142        1206       358
            this.CompactSize = reader.ReadUInt32();
            this.ConstantSize = reader.ReadUInt32();
            this.ChannelOffset = reader.ReadUInt32();
            this.SlopSize = reader.ReadUInt16();
            this.NumFrames = reader.ReadUInt16();               //221 (DD)   17 (11)    151 (97)   201
            this.FrameSize = reader.ReadUInt16();
            this.IndirectSize = reader.ReadUInt16();
            this.QuantizeSize = reader.ReadUInt16();
            this.SegmentSize = reader.ReadByte();
            this.MoverChannelCounts = reader.ReadByte();

            this.Data = reader.ReadBytes((int)DataLength);

            ParseData();

        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            //BuildData should be called before this

            // write structure data
            writer.Write(this.Hash);
            writer.Write(this.DataLength);
            writer.Write(this.CompactSize);
            writer.Write(this.ConstantSize);
            writer.Write(this.ChannelOffset);
            writer.Write(this.SlopSize);
            writer.Write(this.NumFrames);
            writer.Write(this.FrameSize);
            writer.Write(this.IndirectSize);
            writer.Write(this.QuantizeSize);
            writer.Write(this.SegmentSize);
            writer.Write(this.MoverChannelCounts);
            writer.Write(this.Data);
        }

        public override string ToString()
        {
            return Hash.ToString() + ": " + DataLength.ToString();
        }



        public void ParseData()
        {
            if (Data.Length < 18) throw new InvalidDataException("Animation block is too short to contain channel counts.");
            var reader = new AnimChannelDataReader(Data, NumFrames, SegmentSize, ConstantSize, FrameSize);
            var channelList = new List<AnimChannelListItem>();
            var channelLists = new AnimChannel[9][];
            var frameOffset = 0;
            for (int i = 0; i < 9; i++)//iterate through anim channel types
            {
                var ctype = (AnimChannelType)i;
                int channelCount = reader.ReadChannelCount();
                var channels = new AnimChannel[channelCount];
                for (int c = 0; c < channelCount; c++) //construct and read channels
                {
                    var channel = AnimChannel.ConstructChannel(ctype);
                    var channelDataBit = reader.ReadChannelDataBits();
                    if (channel != null)//read channel sequences and indexes
                    {
                        channel.DataOffset = reader.Position / 4;
                        channel.Read(reader);
                        channels[c] = channel;
                        var sequence = channelDataBit >> 2;
                        var index = channelDataBit & 3;
                        if (channel is AnimChannelCachedQuaternion t7)
                        {
                            t7.QuatIndex = index;
                            index = (channel.Type == AnimChannelType.ReconstructQuaternion) ? 3 : 4;
                        }
                        channel.Associate(sequence, index);
                        channelList.Add(new AnimChannelListItem(sequence, index, channel));
                        channel.FrameOffset = frameOffset;
                        frameOffset += channel.GetFrameBits();
                    }
                }
                reader.AlignChannelDataOffset(channelCount);
                channelLists[i] = channels;
            }

            for (int f = 0; f < NumFrames; f++)//read channel frame data
            {
                reader.BeginFrame(f);
                for (int i = 0; i < 9; i++)
                {
                    var channels = channelLists[i];
                    for (int c = 0; c < channels.Length; c++)
                    {
                        var channel = channels[c];
                        channel?.ReadFrame(reader);
                    }
                }
            }

            Sequences = channelList.Count == 0 ? [] : new AnimSequence[channelList.Max(a => a.Sequence) + 1];
            for (int i = 0; i < Sequences.Length; i++) //assign channels to sequences according to read indices
            {
                Sequences[i] = new AnimSequence();

                var thisSeq = channelList.Where(a => a.Sequence == i);
                if (thisSeq.Count() == 0)
                { continue; }

                Sequences[i].Channels = new AnimChannel[thisSeq.Max(a => a.Index) + 1];

                for (int j = 0; j < Sequences[i].Channels.Length; j++)
                {
                    var channel = thisSeq.FirstOrDefault(a => a.Index == j)?.Channel;
                    if (channel == null) continue;
                    Sequences[i].Channels[j] = channel;

                    if (Sequences[i].Channels[j].Type == AnimChannelType.ReconstructQuaternion)
                    {
                        Sequences[i].IsType7Quat = true;
                    }
                    else if (Sequences[i].Channels[j].Type == AnimChannelType.NormalizeQuaternion)
                    {
                        Sequences[i].NormalizeQuaternion = true;
                    }
                }
            }

            reader.Sequences = Sequences;



            int numPosRefs = RootPositionRefCount;
            int numRotRefs = RootRotationRefCount;
            if (numPosRefs > 0)
            {
                RootPositionRefs = new SequenceRootChannelRef[numPosRefs];
                for (int i = 0; i < numPosRefs; i++)
                {
                    var pref = new SequenceRootChannelRef(reader.ReadChannelDataBytes(6));
                    RootPositionRefs[i] = pref;
                }
            }
            if (numRotRefs > 0)
            {
                RootRotationRefs = new SequenceRootChannelRef[numRotRefs];
                for (int i = 0; i < numRotRefs; i++)
                {
                    var rref = new SequenceRootChannelRef(reader.ReadChannelDataBytes(6));
                    RootRotationRefs[i] = rref;
                }
            }
            if (reader.ChannelDataOffset != Data.Length)
            {
                var brem = Data.Length - reader.ChannelDataOffset;
            }



        }


        public void BuildData()
        {
            // convert parsed sequences into Data byte array............

            if (Sequences == null) return;//this shouldn't happen...

            var writer = new AnimChannelDataWriter(NumFrames);

            var channelLists = new List<AnimChannel>[9];

            for (int s = 0; s < Sequences.Length; s++)
            {
                var seq = Sequences[s];
                if (seq?.Channels == null) continue;
                for (int c = 0; c < seq.Channels.Length; c++)
                {
                    var chan = seq.Channels[c];
                    if (chan == null) continue;
                    int typeid = (int)chan.Type;
                    if ((typeid < 0) || (typeid >= 9))
                    { continue; }
                    var chanList = channelLists[typeid];
                    if (chanList == null)
                    {
                        chanList = new List<AnimChannel>();
                        channelLists[typeid] = chanList;
                    }
                    if (chan is AnimChannelCachedQuaternion accq)
                    {
                        chan.Index = accq.QuatIndex;//seems to have QuatIndex stored in there (for channelDataBit below)
                    }
                    chanList.Add(chan);
                }
            }

            for (int i = 0; i < 9; i++)
            {
                var channelList = channelLists[i] ?? [];
                var channelCount = (ushort)(channelList.Count);
                writer.WriteChannelListData(channelCount);
                for (int c = 0; c < channelCount; c++)
                {
                    var channel = channelList[c];
                    var channelDataBit = (ushort)((channel?.Index ?? 0) + ((channel?.Sequence ?? 0) << 2));
                    writer.WriteChannelItemData(channelDataBit);
                    if (channel != null)
                    {
                        channel.DataOffset = writer.Position / 4;
                        channel?.Write(writer);
                    }
                }
                writer.AlignChannelItemData(channelCount, Sequences.Length);
            }

            for (int f = 0; f < NumFrames; f++)//write channel frame data
            {
                writer.BeginFrame(f);
                for (int i = 0; i < 9; i++)
                {
                    var channelList = channelLists[i] ?? [];
                    var channelCount = (ushort)(channelList.Count);
                    for (int c = 0; c < channelCount; c++)
                    {
                        var channel = channelList[c];
                        channel?.WriteFrame(writer);
                    }
                }
                writer.EndFrame();
            }



            var frameOffset = 0;
            for (int i = 0; i < channelLists.Length; i++)
            {
                var chanList = channelLists[i];
                if (chanList == null) continue;
                for (int c = 0; c < chanList.Count; c++)
                {
                    var chan = chanList[c];
                    if (chan == null) continue;
                    chan.FrameOffset = frameOffset;
                    frameOffset += chan.GetFrameBits();
                }
            }



            UpdateRootMotionRefs();
            if (RootPositionRefs != null)
            {
                for (int i = 0; i < RootPositionRefs.Length; i++)
                {
                    writer.WriteChannelItemDataBytes(RootPositionRefs[i].Bytes);
                }
            }
            if (RootRotationRefs != null)
            {
                for (int i = 0; i < RootRotationRefs.Length; i++)
                {
                    writer.WriteChannelItemDataBytes(RootRotationRefs[i].Bytes);
                }
            }



            var mainData = writer.GetMainDataBytes();
            var frameData = writer.GetFrameDataBytes();
            var channelListData = writer.GetChannelListDataBytes();
            var channelItemData = writer.GetChannelItemDataBytes();

            var dataLen = mainData.Length + frameData.Length + channelListData.Length + channelItemData.Length;
            var data = new byte[dataLen];
            var curpos = 0;
            Buffer.BlockCopy(mainData, 0, data, 0, mainData.Length); curpos += mainData.Length;
            Buffer.BlockCopy(frameData, 0, data, curpos, frameData.Length); curpos += frameData.Length;
            Buffer.BlockCopy(channelListData, 0, data, curpos, channelListData.Length); curpos += channelListData.Length;
            Buffer.BlockCopy(channelItemData, 0, data, curpos, channelItemData.Length);



            Data = data;
            DataLength = (uint)data.Length;
            CompactSize = 0;
            ConstantSize = (uint)mainData.Length;
            FrameSize = writer.FrameLength;
            SegmentSize = (writer.ChunkSize > 0) ? writer.ChunkSize : (byte)255;
            QuantizeSize = GetQuantizeFloatValueBits();
            IndirectSize = GetIndirectQuantizeFloatNumInts();
            MoverChannelCounts = (byte)((((uint)(RootPositionRefs?.Length??0))<<4) | ((uint)(RootRotationRefs?.Length ?? 0)));
            ChannelOffset = (uint)(BlockLength - ((RootPositionRefCount + RootRotationRefCount) * 6));
        }


        public void AssociateSequenceChannels()//assigns Sequence and Index to all channels
        {
            if (Sequences == null) return;//this shouldn't happen...
            for (int s = 0; s < Sequences.Length; s++)
            {
                var seq = Sequences[s];
                if (seq?.Channels == null) continue;
                for (int c = 0; c < seq.Channels.Length; c++)
                {
                    var chan = seq.Channels[c];
                    chan?.Associate(s, c);
                }
            }
        }


        public ushort GetQuantizeFloatValueBits()
        {
            int b = 0;
            foreach (var seq in Sequences)
            {
                foreach (var chan in seq.Channels)
                {
                    if (chan.Type == AnimChannelType.QuantizeFloat)
                    {
                        var acqf = (AnimChannelQuantizeFloat)chan;
                        b += acqf.ValueBits;
                    }
                }
            }
            return (ushort)b;
        }
        public ushort GetIndirectQuantizeFloatNumInts()
        {
            int b = 0;
            foreach (var seq in Sequences)
            {
                foreach (var chan in seq.Channels)
                {
                    if (chan.Type == AnimChannelType.IndirectQuantizeFloat)
                    {
                        var acif = (AnimChannelIndirectQuantizeFloat)chan;
                        b += acif.NumInts+5;
                    }
                }
            }
            return (ushort)b;
        }

        public void UpdateRootMotionRefs()
        {
            // OFFSETS -  [ChannelType], [Index], [DataIntOffset 0xFFFF], [FrameBitOffset 0xFFFF]

            var newPosRefs = new List<SequenceRootChannelRef>();
            var newRotRefs = new List<SequenceRootChannelRef>();

            for (int i = 0; i < Sequences.Length; i++)
            {
                var seq = Sequences[i];
                if (seq == null) continue;
                if (seq.BoneId.Track == 5) //root position
                {
                    for (var c = 0; c < seq.Channels?.Length; c++)
                    {
                        var chan = seq.Channels[c];
                        var newPosRef = new SequenceRootChannelRef(chan.Type, chan.GetReferenceIndex());
                        newPosRef.DataIntOffset = (ushort)chan.DataOffset;
                        newPosRef.FrameBitOffset = (ushort)chan.FrameOffset;
                        newPosRefs.Add(newPosRef);
                    }
                }
                if (seq.BoneId.Track == 6) //root rotation
                {
                    for (var c = 0; c < seq.Channels?.Length; c++)
                    {
                        var chan = seq.Channels[c];
                        var newRotRef = new SequenceRootChannelRef(chan.Type, chan.GetReferenceIndex());
                        newRotRef.DataIntOffset = (ushort)chan.DataOffset;
                        newRotRef.FrameBitOffset = (ushort)chan.FrameOffset;
                        newRotRefs.Add(newRotRef);
                    }
                }
            }


            int compare(SequenceRootChannelRef a, SequenceRootChannelRef b)
            {
                var v1 = a.ChannelType.CompareTo(b.ChannelType);
                if (v1 != 0) return v1;
                var v2 = a.ChannelIndex.CompareTo(b.ChannelIndex);
                if (v2 != 0) return v2;
                return 0;
            }
            newPosRefs.Sort((a, b) => { return compare(a, b); });
            newRotRefs.Sort((a, b) => { return compare(a, b); });


            RootPositionRefs = newPosRefs.ToArray();
            RootRotationRefs = newRotRefs.ToArray();


        }




        public void WriteXml(StringBuilder sb, int indent)
        {
            YcdXml.StringTag(sb, indent, "Hash", YcdXml.HashString(Hash));
            YcdXml.ValueTag(sb, indent, "FrameCount", NumFrames.ToString());
            YcdXml.WriteItemArray(sb, Sequences, indent, "SequenceData");
        }
        public void ReadXml(XmlNode node)
        {
            Hash = XmlMeta.GetHash(Xml.GetChildInnerText(node, "Hash"));
            NumFrames = (ushort)Xml.GetChildUIntAttribute(node, "FrameCount", "value");
            Sequences = XmlMeta.ReadItemArray<AnimSequence>(node, "SequenceData");

            AssociateSequenceChannels();
        }

    }


    [TypeConverter(typeof(ExpandableObjectConverter))] public class ClipMapEntry : ResourceSystemBlock
    {
        public override long BlockLength
        {
            get { return 32; }
        }

        // structure data
        public MetaHash Hash { get; set; }
        public ulong ClipPointer { get; set; }
        public ulong NextPointer { get; set; }

        // reference data
        public ClipBase? Clip { get; set; }
        public ClipMapEntry? Next { get; set; }

        public bool EnableRootMotion { get; set; } = false; //used by CW to toggle whether or not to include root motion when playing animations
        public bool OverridePlayTime { get; set; } = false; //used by CW to manually override the animation playback time
        public float PlayTime { get; set; } = 0.0f;


        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            // read structure data
            this.Hash = new MetaHash(reader.ReadUInt32());
            _ = reader.ReadUInt32();
            this.ClipPointer = reader.ReadUInt64();
            this.NextPointer = reader.ReadUInt64();
            _ = reader.ReadUInt64();

            // read reference data
            this.Clip = reader.ReadBlockAt<ClipBase>(
                this.ClipPointer // offset
            );
            this.Next = reader.ReadBlockAt<ClipMapEntry>(
                this.NextPointer // offset
            );


            if (Clip != null)
            {
                Clip.Hash = Hash;
            }
            else
            { }
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            // update structure data
            this.ClipPointer = (ulong)(this.Clip != null ? this.Clip.FilePosition : 0);
            this.NextPointer = (ulong)(this.Next != null ? this.Next.FilePosition : 0);

            // write structure data
            writer.Write(this.Hash);
            writer.Write(0u);
            writer.Write(this.ClipPointer);
            writer.Write(this.NextPointer);
            writer.Write(0ul);
        }

        public override IResourceBlock[] GetReferences()
        {
            var list = new List<IResourceBlock>();
            if (Clip != null) list.Add(Clip);
            if (Next != null) list.Add(Next);
            return list.ToArray();
        }

        public override string ToString()
        {
            return Clip?.Name ?? Hash.ToString();
        }

    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class ClipBase : ResourceSystemBlock, IResourceXXSystemBlock, IMetaXmlItem
    {
        public override long BlockLength
        {
            get { return 112; }
        }

        // structure data
        public uint VFT { get; set; }
        public uint BaseReferenceCount { get; set; } = 1;
        public ClipType Type { get; set; } // 1, 2
        public ulong NamePointer { get; set; }
        public ushort NameLength { get; set; } // short, name length
        public ushort NameCapacity { get; set; } // short, name length +1
        public ulong DictionaryPointer { get; set; } = 0x50000000;
        public ClipFlags Flags { get; set; }
        public ulong TagsPointer { get; set; }
        public ulong PropertiesPointer { get; set; }
        public uint ReferenceCount { get; set; } = 1;

        // reference data
        public string Name { get; set; } = string.Empty;
        public ClipTagList? Tags { get; set; }
        public ClipPropertyMap? Properties { get; set; }

        private string_r? NameBlock;

        public YcdFile? Ycd { get; set; }
        public string ShortName
        {
            get
            {
                if (!string.IsNullOrEmpty(_ShortName)) return _ShortName;
                if (string.IsNullOrEmpty(Name)) return string.Empty;

                string name = Name.Replace('\\', '/');
                var slidx = name.LastIndexOf('/');
                if ((slidx >= 0) && (slidx < name.Length - 1))
                {
                    name = name.Substring(slidx + 1);
                }
                var didx = name.IndexOf('.');
                if ((didx > 0) && (didx < name.Length))
                {
                    name = name.Substring(0, didx);
                }
                _ShortName = name.ToLowerInvariant();
                JenkIndex.Ensure(_ShortName);
                return _ShortName;
            }
        }
        private string? _ShortName;

        public MetaHash Hash { get; set; } //used by CW when reading/writing


        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            // read structure data
            this.VFT = reader.ReadUInt32();
            this.BaseReferenceCount = reader.ReadUInt32();
            _ = reader.ReadUInt64();
            this.Type = (ClipType)reader.ReadUInt32();
            _ = reader.ReadUInt32();
            this.NamePointer = reader.ReadUInt64();
            this.NameLength = reader.ReadUInt16();
            this.NameCapacity = reader.ReadUInt16();
            _ = reader.ReadUInt32();
            this.DictionaryPointer = reader.ReadUInt64();
            this.Flags = (ClipFlags)reader.ReadByte();
            _ = reader.ReadBytes(7);
            this.TagsPointer = reader.ReadUInt64();
            this.PropertiesPointer = reader.ReadUInt64();
            this.ReferenceCount = reader.ReadUInt32();
            _ = reader.ReadUInt32();


            this.Name = reader.ReadStringAt(this.NamePointer) ?? string.Empty;
            this.Tags = reader.ReadBlockAt<ClipTagList>(
                this.TagsPointer // offset
            );
            this.Properties = reader.ReadBlockAt<ClipPropertyMap>(
                this.PropertiesPointer // offset
            );

        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            // update structure data
            this.NamePointer = (ulong)(this.NameBlock != null ? this.NameBlock.FilePosition : 0);
            this.NameLength = (ushort)(Name?.Length ?? 0);
            this.NameCapacity = string.IsNullOrEmpty(Name) ? (ushort)0 : checked((ushort)(Name.Length + 1));
            this.TagsPointer = (ulong)(this.Tags != null ? this.Tags.FilePosition : 0);
            this.PropertiesPointer = (ulong)(this.Properties != null ? this.Properties.FilePosition : 0);


            // write structure data
            writer.Write(this.VFT);
            writer.Write(this.BaseReferenceCount);
            writer.Write(0ul);
            writer.Write((uint)this.Type);
            writer.Write(0u);
            writer.Write(this.NamePointer);
            writer.Write(this.NameLength);
            writer.Write(this.NameCapacity);
            writer.Write(0u);
            writer.Write(this.DictionaryPointer);
            writer.Write((byte)this.Flags);
            writer.Write(new byte[7]);
            writer.Write(this.TagsPointer);
            writer.Write(this.PropertiesPointer);
            writer.Write(this.ReferenceCount);
            writer.Write(0u);
        }

        public override IResourceBlock[] GetReferences()
        {
            var list = new List<IResourceBlock>();
            if (!string.IsNullOrEmpty(Name))
            {
                NameBlock = (string_r)Name;
                list.Add(NameBlock);
            }
            else NameBlock = null;
            if (Tags != null) list.Add(Tags);
            if (Properties != null) list.Add(Properties);
            return list.ToArray();
        }

        public IResourceSystemBlock GetType(ResourceDataReader reader, params object[] parameters)
        {
            reader.Position += 16;
            var type = reader.ReadByte();
            reader.Position -= 17;

            return ConstructClip((ClipType)type);
        }


        public static ClipBase ConstructClip(ClipType type)
        {
            switch (type)
            {
                case ClipType.Animation: return new ClipAnimation();
                case ClipType.AnimationList: return new ClipAnimationList();
                case ClipType.AnimationExpression: return new ClipAnimationExpression();
                default: throw new InvalidDataException($"Unsupported clip type: {type}.");
            }
        }

        public virtual float GetDuration() => 0.0f;

        public virtual void ForEachAnimation(double currentTime, Action<Animation, float> callback)
        {
        }

        protected float GetClipTime(double currentTime, float duration)
        {
            if (duration <= 0.0f || !double.IsFinite(currentTime)) return 0.0f;
            if ((Flags & ClipFlags.Looped) == 0)
                return (float)Math.Clamp(currentTime, 0.0, duration);

            double time = currentTime % duration;
            if (time < 0.0) time += duration;
            return (float)time;
        }


        public override string ToString()
        {
            return Name;
        }


        public virtual void WriteXml(StringBuilder sb, int indent)
        {
            YcdXml.StringTag(sb, indent, "Hash", MetaXml.XmlEscape(YcdXml.HashString(Hash)));
            YcdXml.StringTag(sb, indent, "Name", MetaXml.XmlEscape(Name));
            YcdXml.ValueTag(sb, indent, "Type", Type.ToString());
            YcdXml.ValueTag(sb, indent, "Flags", ((byte)Flags).ToString());
            YcdXml.WriteItemArray(sb, Tags?.Tags?.data_items ?? [], indent, "Tags");
            YcdXml.WriteItemArray(sb, Properties?.AllProperties ?? [], indent, "Properties");
        }
        public virtual void ReadXml(XmlNode node)
        {
            Hash = XmlMeta.GetHash(Xml.GetChildInnerText(node, "Hash"));
            Name = Xml.GetChildInnerText(node, "Name") ?? string.Empty;
            var flagsNode = node.SelectSingleNode("Flags");
            Flags = (ClipFlags)(flagsNode != null
                ? Xml.GetUIntAttribute(flagsNode, "value")
                : Xml.GetChildUIntAttribute(node, "Unknown30", "value"));

            var tags = XmlMeta.ReadItemArrayNullable<ClipTag>(node, "Tags") ?? [];
            Tags = new ClipTagList();
            if (tags != null)
            {
                Tags.Tags = new ResourcePointerArray64<ClipTag>();
                Tags.Tags.data_items = tags;
                Tags.BuildAllTags();
                Tags.AssignTagOwners();
            }

            var props = XmlMeta.ReadItemArrayNullable<ClipProperty>(node, "Properties") ?? [];
            Properties = new ClipPropertyMap();
            Properties.CreatePropertyMap(props);
        }
    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class ClipAnimation : ClipBase
    {
        public override long BlockLength
        {
            get { return 112; }
        }

        // structure data
        public ulong AnimationPointer { get; set; }
        public float StartTime { get; set; } //start time
        public float EndTime { get; set; } //end time
        public float Rate { get; set; } //1.0  rate..?

        // reference data
        public Animation? Animation { get; set; }
        public MetaHash AnimationHash { get; set; } //used when reading XML.

        public ClipAnimation()
        {
            Type = ClipType.Animation;
        }

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            base.Read(reader, parameters);
            this.AnimationPointer = reader.ReadUInt64();
            this.StartTime = reader.ReadSingle();
            this.EndTime = reader.ReadSingle();
            this.Rate = reader.ReadSingle();
            _ = reader.ReadBytes(GetType() == typeof(ClipAnimation) ? 12 : 4);

            this.Animation = reader.ReadBlockAt<Animation>(
                this.AnimationPointer // offset
            );
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            base.Write(writer, parameters);

            this.AnimationPointer = (ulong)(this.Animation != null ? this.Animation.FilePosition : 0);

            writer.Write(this.AnimationPointer);
            writer.Write(this.StartTime);
            writer.Write(this.EndTime);
            writer.Write(this.Rate);
            writer.Write(new byte[GetType() == typeof(ClipAnimation) ? 12 : 4]);
        }

        public override IResourceBlock[] GetReferences()
        {
            var list = new List<IResourceBlock>();
            list.AddRange(base.GetReferences());
            if (Animation != null) list.Add(Animation);
            return list.ToArray();
        }

        public override float GetDuration()
        {
            float duration = EndTime - StartTime;
            return duration > 0.0f && Rate > 0.0f ? duration / Rate : 0.0f;
        }

        public float GetClipTime(double currentTime) => base.GetClipTime(currentTime, GetDuration());

        public float GetPlaybackTime(double currentTime)
        {
            if (EndTime <= StartTime || Rate <= 0.0f) return StartTime;
            return Math.Clamp(StartTime + (GetClipTime(currentTime) * Rate), StartTime, EndTime);
        }

        public override void ForEachAnimation(double currentTime, Action<Animation, float> callback)
        {
            if (Animation != null) callback(Animation, GetPlaybackTime(currentTime));
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            YcdXml.StringTag(sb, indent, "AnimationHash", YcdXml.HashString(Animation?.Hash ?? 0));
            YcdXml.ValueTag(sb, indent, "StartTime", FloatUtil.ToString(StartTime));
            YcdXml.ValueTag(sb, indent, "EndTime", FloatUtil.ToString(EndTime));
            YcdXml.ValueTag(sb, indent, "Rate", FloatUtil.ToString(Rate));
        }
        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            AnimationHash = XmlMeta.GetHash(Xml.GetChildInnerText(node, "AnimationHash"));
            StartTime = Xml.GetChildFloatAttribute(node, "StartTime", "value");
            EndTime = Xml.GetChildFloatAttribute(node, "EndTime", "value");
            Rate = Xml.GetChildFloatAttribute(node, "Rate", "value");
        }
    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class ClipAnimationExpression : ClipAnimation
    {
        public override long BlockLength => 112;

        public ulong ExpressionsPointer { get; set; }
        public Expression? Expressions { get; set; }

        public ClipAnimationExpression()
        {
            Type = ClipType.AnimationExpression;
        }

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            base.Read(reader, parameters);
            ExpressionsPointer = reader.ReadUInt64();
            Expressions = reader.ReadBlockAt<Expression>(ExpressionsPointer);
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            base.Write(writer, parameters);
            ExpressionsPointer = (ulong)(Expressions?.FilePosition ?? 0);
            writer.Write(ExpressionsPointer);
        }

        public override IResourceBlock[] GetReferences()
        {
            var references = new List<IResourceBlock>(base.GetReferences());
            if (Expressions != null) references.Add(Expressions);
            return references.ToArray();
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            if (Expressions != null)
            {
                YcdXml.OpenTag(sb, indent, "Expressions");
                Expressions.WriteXml(sb, indent + 1);
                YcdXml.CloseTag(sb, indent, "Expressions");
            }
        }

        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            var expressionsNode = node.SelectSingleNode("Expressions");
            if (expressionsNode != null)
            {
                Expressions = new Expression();
                Expressions.ReadXml(expressionsNode);
            }
        }
    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class ClipAnimationList : ClipBase
    {
        public override long BlockLength
        {
            get { return 112; }
        }

        // structure data
        public ulong AnimationsPointer { get; set; }
        public ushort AnimationsCount1 { get; set; }
        public ushort AnimationsCount2 { get; set; }
        public float Duration { get; set; }
        public bool Parallel { get; set; }

        // reference data
        public ResourceSimpleArray<ClipAnimationsEntry>? Animations { get; set; }


        public ClipAnimationList()
        {
            Type = ClipType.AnimationList;
        }

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            base.Read(reader, parameters);
            this.AnimationsPointer = reader.ReadUInt64();
            this.AnimationsCount1 = reader.ReadUInt16();
            this.AnimationsCount2 = reader.ReadUInt16();
            _ = reader.ReadUInt32();
            this.Duration = reader.ReadSingle();
            this.Parallel = reader.ReadByte() != 0;
            _ = reader.ReadBytes(11);

            this.Animations = reader.ReadBlockAt<ResourceSimpleArray<ClipAnimationsEntry>>(
                this.AnimationsPointer, // offset
                this.AnimationsCount1
            );
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            base.Write(writer, parameters);

            this.AnimationsPointer = (ulong)(this.Animations != null ? this.Animations.FilePosition : 0);
            this.AnimationsCount1 = (ushort)(this.Animations != null ? this.Animations.Count : 0);
            this.AnimationsCount2 = this.AnimationsCount1;

            writer.Write(this.AnimationsPointer);
            writer.Write(this.AnimationsCount1);
            writer.Write(this.AnimationsCount2);
            writer.Write(0u);
            writer.Write(this.Duration);
            writer.Write((byte)(this.Parallel ? 1 : 0));
            writer.Write(new byte[11]);
        }

        public override IResourceBlock[] GetReferences()
        {
            var list = new List<IResourceBlock>();
            list.AddRange(base.GetReferences());
            if (Animations != null) list.Add(Animations);
            return list.ToArray();
        }


        public override float GetDuration()
        {
            if (Duration > 0.0f) return Duration;
            var animations = Animations?.Data;
            if (animations == null || animations.Count == 0) return 0.0f;
            return Parallel ? animations.Max(a => a?.GetDuration() ?? 0.0f) : animations.Sum(a => a?.GetDuration() ?? 0.0f);
        }

        public float GetPlaybackTime(double currentTime) => GetClipTime(currentTime, GetDuration());

        public override void ForEachAnimation(double currentTime, Action<Animation, float> callback)
        {
            var animations = Animations?.Data;
            if (animations == null || animations.Count == 0) return;

            float time = GetPlaybackTime(currentTime);
            if (Parallel)
            {
                foreach (var entry in animations)
                    if (entry?.Animation != null) callback(entry.Animation, entry.GetPlaybackTime(time));
                return;
            }

            for (int i = 0; i < animations.Count; i++)
            {
                var entry = animations[i];
                float duration = entry?.GetDuration() ?? 0.0f;
                bool last = i == animations.Count - 1;
                if (entry?.Animation != null && (time < duration || last))
                {
                    callback(entry.Animation, entry.GetPlaybackTime(time));
                    return;
                }
                time -= duration;
            }
        }



        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            YcdXml.ValueTag(sb, indent, "Duration", FloatUtil.ToString(Duration));
            YcdXml.ValueTag(sb, indent, "Parallel", Parallel.ToString().ToLowerInvariant());
            YcdXml.WriteItemArray(sb, Animations?.Data.ToArray() ?? [], indent, "Animations");
        }
        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            Duration = Xml.GetChildFloatAttribute(node, "Duration", "value");
            Parallel = Xml.GetChildBoolAttribute(node, "Parallel", "value");

            Animations = new ResourceSimpleArray<ClipAnimationsEntry>();
            Animations.Data = new List<ClipAnimationsEntry>();
            var anims = XmlMeta.ReadItemArrayNullable<ClipAnimationsEntry>(node, "Animations") ?? [];
            if (anims != null) Animations.Data.AddRange(anims);
        }
    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class ClipAnimationsEntry : ResourceSystemBlock, IMetaXmlItem
    {
        public override long BlockLength
        {
            get { return 24; }
        }

        // structure data
        public float StartTime { get; set; }
        public float EndTime { get; set; }
        public float Rate { get; set; }
        public ulong AnimationPointer { get; set; }

        // reference data
        public Animation? Animation { get; set; }
        public MetaHash AnimationHash { get; set; } //used when reading XML.

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            // read structure data
            this.StartTime = reader.ReadSingle();
            this.EndTime = reader.ReadSingle();
            this.Rate = reader.ReadSingle();
            _ = reader.ReadUInt32();
            this.AnimationPointer = reader.ReadUInt64();

            // read reference data
            this.Animation = reader.ReadBlockAt<Animation>(
                this.AnimationPointer // offset
            );
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            // update structure data
            this.AnimationPointer = (ulong)(this.Animation != null ? this.Animation.FilePosition : 0);

            // write structure data
            writer.Write(this.StartTime);
            writer.Write(this.EndTime);
            writer.Write(this.Rate);
            writer.Write(0u);
            writer.Write(this.AnimationPointer);
        }

        public override IResourceBlock[] GetReferences()
        {
            var list = new List<IResourceBlock>();
            if (Animation != null) list.Add(Animation);
            return list.ToArray();
        }


        public float GetDuration()
        {
            float duration = EndTime - StartTime;
            return duration > 0.0f && Rate > 0.0f ? duration / Rate : 0.0f;
        }

        public float GetPlaybackTime(double currentTime) => EndTime > StartTime && Rate > 0.0f
            ? Math.Clamp(StartTime + ((float)currentTime * Rate), StartTime, EndTime)
            : StartTime;


        public void WriteXml(StringBuilder sb, int indent)
        {
            YcdXml.StringTag(sb, indent, "AnimationHash", YcdXml.HashString(Animation?.Hash ?? 0));
            YcdXml.ValueTag(sb, indent, "StartTime", FloatUtil.ToString(StartTime));
            YcdXml.ValueTag(sb, indent, "EndTime", FloatUtil.ToString(EndTime));
            YcdXml.ValueTag(sb, indent, "Rate", FloatUtil.ToString(Rate));
        }
        public void ReadXml(XmlNode node)
        {
            AnimationHash = XmlMeta.GetHash(Xml.GetChildInnerText(node, "AnimationHash"));
            StartTime = Xml.GetChildFloatAttribute(node, "StartTime", "value");
            EndTime = Xml.GetChildFloatAttribute(node, "EndTime", "value");
            Rate = Xml.GetChildFloatAttribute(node, "Rate", "value");
        }
    }
    public enum ClipType : uint
    {
        Animation = 1,
        AnimationList = 2,
        AnimationExpression = 3,
    }

    [Flags]
    public enum ClipFlags : byte
    {
        None = 0,
        Looped = 1 << 0,
        Resource = 1 << 1,
    }


    [TypeConverter(typeof(ExpandableObjectConverter))] public class ClipPropertyMap : ResourceSystemBlock
    {
        public override long BlockLength
        {
            get { return 16; }
        }

        // structure data
        public ulong PropertyEntriesPointer { get; set; }
        public ushort PropertyEntriesCapacity { get; set; }
        public ushort PropertyEntriesCount { get; set; }
        public uint MapFlags { get; set; } = 0x01000000;

        // reference data
        public ResourcePointerArray64<ClipPropertyMapEntry>? Properties { get; set; }

        public ClipProperty[] AllProperties { get; set; } = [];
        public Dictionary<MetaHash, ClipProperty> PropertyMap { get; set; } = new();


        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            // read structure data
            this.PropertyEntriesPointer = reader.ReadUInt64();
            this.PropertyEntriesCapacity = reader.ReadUInt16();
            this.PropertyEntriesCount = reader.ReadUInt16();
            this.MapFlags = reader.ReadUInt32();

            // read reference data
            this.Properties = reader.ReadBlockAt<ResourcePointerArray64<ClipPropertyMapEntry>>(
                this.PropertyEntriesPointer, // offset
                this.PropertyEntriesCapacity
            );

            BuildPropertyMap();
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            // update structure data
            this.PropertyEntriesPointer = (ulong)(this.Properties != null ? this.Properties.FilePosition : 0);
            this.PropertyEntriesCapacity = (ushort)(this.Properties != null ? this.Properties.Count : 0);
            this.PropertyEntriesCount = (ushort)(this.AllProperties?.Length ?? 0);

            // write structure data
            writer.Write(this.PropertyEntriesPointer);
            writer.Write(this.PropertyEntriesCapacity);
            writer.Write(this.PropertyEntriesCount);
            writer.Write(this.MapFlags);
        }

        public override IResourceBlock[] GetReferences()
        {
            var list = new List<IResourceBlock>();
            if (Properties != null) list.Add(Properties);
            return list.ToArray();
        }

        public override string ToString()
        {
            return "Count: " + (AllProperties?.Length ?? 0).ToString();
        }


        public void BuildPropertyMap()
        {
            AllProperties = [];
            PropertyMap = new Dictionary<MetaHash, ClipProperty>();
            if (Properties?.data_items != null)
            {
                List<ClipProperty> pl = new();
                foreach (var pme in Properties.data_items)
                {
                    ClipPropertyMapEntry? cpme = pme;
                    while (cpme?.Data != null)
                    {
                        pl.Add(cpme.Data);
                        cpme = cpme.Next;
                    }
                }
                AllProperties = pl.ToArray();

                foreach (var cp in AllProperties)
                {
                    PropertyMap[cp.NameHash] = cp;
                }
            }
        }


        public void CreatePropertyMap(ClipProperty[] properties)
        {
            var numBuckets = ClipDictionary.GetNumHashBuckets(properties?.Length ?? 0);
            var buckets = new List<ClipPropertyMapEntry>[numBuckets];
            if (properties != null)
            {
                foreach (var prop in properties)
                {
                    var b = prop.NameHash % numBuckets;
                    var bucket = buckets[b];
                    if (bucket == null)
                    {
                        bucket = new List<ClipPropertyMapEntry>();
                        buckets[b] = bucket;
                    }
                    var pme = new ClipPropertyMapEntry();
                    pme.PropertyNameHash = prop.NameHash;
                    pme.Data = prop;
                    bucket.Add(pme);
                }
            }

            var newProperties = new ClipPropertyMapEntry[buckets.Length];
            for (int bucketIndex = 0; bucketIndex < buckets.Length; bucketIndex++)
            {
                var b = buckets[bucketIndex];
                if (b is { Count: > 0 })
                {
                    newProperties[bucketIndex] = b[0];
                    var p = b[0];
                    for (int i = 1; i < b.Count; i++)
                    {
                        var c = b[i];
                        c.Next = null;
                        p.Next = c;
                        p = c;
                    }
                }
            }

            Properties = new ResourcePointerArray64<ClipPropertyMapEntry>();
            Properties.data_items = newProperties.ToArray();

            AllProperties = properties ?? [];
        }

    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class ClipPropertyMapEntry : ResourceSystemBlock
    {
        public override long BlockLength
        {
            get { return 32; }
        }

        // structure data
        public MetaHash PropertyNameHash { get; set; }
        public ulong DataPointer { get; set; }
        public ulong NextPointer { get; set; }

        // reference data
        public ClipProperty? Data { get; set; }
        public ClipPropertyMapEntry? Next { get; set; }

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            // read structure data
            this.PropertyNameHash = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            this.DataPointer = reader.ReadUInt64();
            this.NextPointer = reader.ReadUInt64();
            _ = reader.ReadUInt64();

            // read reference data
            this.Data = reader.ReadBlockAt<ClipProperty>(
                this.DataPointer // offset
            );
            this.Next = reader.ReadBlockAt<ClipPropertyMapEntry>(
                this.NextPointer // offset
            );
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            // update structure data
            this.DataPointer = (ulong)(this.Data != null ? this.Data.FilePosition : 0);
            this.NextPointer = (ulong)(this.Next != null ? this.Next.FilePosition : 0);

            // write structure data
            writer.Write(this.PropertyNameHash);
            writer.Write(0u);
            writer.Write(this.DataPointer);
            writer.Write(this.NextPointer);
            writer.Write(0ul);
        }

        public override IResourceBlock[] GetReferences()
        {
            var list = new List<IResourceBlock>();
            if (Data != null) list.Add(Data);
            if (Next != null) list.Add(Next);
            return list.ToArray();
        }

    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class ClipProperty : ResourceSystemBlock, IMetaXmlItem
    {
        public override long BlockLength
        {
            get { return 64; }
        }

        // structure data
        public uint VFT { get; set; }
        public uint BaseReferenceCount { get; set; } = 1;
        public ulong NamePointer { get; set; }
        public MetaHash NameHash { get; set; }
        public ulong AttributesPointer { get; set; }
        public ushort AttributesCount { get; set; }
        public ushort AttributesCapacity { get; set; }
        public ulong PropertiesPointer { get; set; }
        public MetaHash Signature { get; set; }

        // reference data
        public ResourcePointerArray64<ClipPropertyAttribute>? Attributes { get; set; }
        public ClipPropertyMap? Properties { get; set; }
        public string Name { get; set; } = string.Empty;
        private string_r? NameBlock;

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            // read structure data
            this.VFT = reader.ReadUInt32();
            this.BaseReferenceCount = reader.ReadUInt32();
            _ = reader.ReadUInt64();
            this.NamePointer = reader.ReadUInt64();
            this.NameHash = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            this.AttributesPointer = reader.ReadUInt64();
            this.AttributesCount = reader.ReadUInt16();
            this.AttributesCapacity = reader.ReadUInt16();
            _ = reader.ReadUInt32();
            this.PropertiesPointer = reader.ReadUInt64();
            this.Signature = reader.ReadUInt32();
            _ = reader.ReadUInt32();

            // read reference data
            this.Attributes = reader.ReadBlockAt<ResourcePointerArray64<ClipPropertyAttribute>>(
                this.AttributesPointer, // offset
                this.AttributesCount
            );
            this.Properties = reader.ReadBlockAt<ClipPropertyMap>(this.PropertiesPointer);
            this.Name = reader.ReadStringAt(this.NamePointer) ?? string.Empty;
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            // update structure data
            this.AttributesPointer = (ulong)(this.Attributes != null ? this.Attributes.FilePosition : 0);
            this.AttributesCount = (ushort)(this.Attributes != null ? this.Attributes.Count : 0);
            this.AttributesCapacity = this.AttributesCount;
            this.NamePointer = (ulong)(this.NameBlock?.FilePosition ?? 0);
            this.PropertiesPointer = (ulong)(this.Properties?.FilePosition ?? 0);
            if (this.NameHash == 0 && !string.IsNullOrEmpty(this.Name)) this.NameHash = JenkHash.GenHash(this.Name);

            // write structure data
            writer.Write(this.VFT);
            writer.Write(this.BaseReferenceCount);
            writer.Write(0ul);
            writer.Write(this.NamePointer);
            writer.Write(this.NameHash);
            writer.Write(0u);
            writer.Write(this.AttributesPointer);
            writer.Write(this.AttributesCount);
            writer.Write(this.AttributesCapacity);
            writer.Write(0u);
            writer.Write(this.PropertiesPointer);
            writer.Write(this.Signature);
            writer.Write(0u);
        }

        public override IResourceBlock[] GetReferences()
        {
            var list = new List<IResourceBlock>();
            if (Attributes != null) list.Add(Attributes);
            if (Properties != null) list.Add(Properties);
            NameBlock = string.IsNullOrEmpty(Name) ? null : (string_r)Name;
            if (NameBlock != null) list.Add(NameBlock);
            return list.ToArray();
        }

        public override string ToString()
        {
            StringBuilder sb = new();
            if ((Attributes != null) && (Attributes.data_items != null))
            {
                foreach (var item in Attributes.data_items)
                {
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(item.ToString());
                }
            }
            return NameHash.ToString() + ": " + Signature.ToString() + ": " + sb.ToString();
        }


        public virtual void WriteXml(StringBuilder sb, int indent)
        {
            YcdXml.StringTag(sb, indent, "NameHash", YcdXml.HashString(NameHash));
            if (!string.IsNullOrEmpty(Name)) YcdXml.StringTag(sb, indent, "Name", MetaXml.XmlEscape(Name));
            YcdXml.StringTag(sb, indent, "Signature", YcdXml.HashString(Signature));
            YcdXml.WriteItemArray(sb, Attributes?.data_items ?? [], indent, "Attributes");
            YcdXml.WriteItemArray(sb, Properties?.AllProperties ?? [], indent, "Properties");
        }
        public virtual void ReadXml(XmlNode node)
        {
            NameHash = XmlMeta.GetHash(Xml.GetChildInnerText(node, "NameHash"));
            Name = Xml.GetChildInnerText(node, "Name") ?? string.Empty;
            if (NameHash == 0 && !string.IsNullOrEmpty(Name)) NameHash = JenkHash.GenHash(Name);
            var signatureText = Xml.GetChildInnerText(node, "Signature");
            if (string.IsNullOrEmpty(signatureText)) signatureText = Xml.GetChildInnerText(node, "UnkHash");
            Signature = XmlMeta.GetHash(signatureText);

            var attrsNode = node.SelectSingleNode("Attributes");
            if (attrsNode != null)
            {
                var inodes = attrsNode.SelectNodes("Item");
                if (inodes?.Count > 0)
                {
                    var alist = new List<ClipPropertyAttribute>();
                    foreach (XmlNode inode in inodes)
                    {
                        var item = new ClipPropertyAttribute();
                        item.ReadXml(inode);
                        var v = ClipPropertyAttribute.ConstructItem(item.Type);
                        v.ReadXml(inode);//slightly wasteful but meh
                        alist.Add(v);
                    }
                    Attributes = new ResourcePointerArray64<ClipPropertyAttribute>();
                    Attributes.data_items = alist.ToArray();
                }
            }

            if (node.SelectSingleNode("Properties") != null)
            {
                var properties = XmlMeta.ReadItemArrayNullable<ClipProperty>(node, "Properties") ?? [];
                Properties = new ClipPropertyMap();
                Properties.CreatePropertyMap(properties);
            }
        }
    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class ClipPropertyAttribute : ResourceSystemBlock, IResourceXXSystemBlock, IMetaXmlItem
    {
        public override long BlockLength
        {
            get { return 32; }
        }

        public uint VFT { get; set; }
        public uint BaseReferenceCount { get; set; } = 1;
        public ClipPropertyAttributeType Type { get; set; }
        public ulong NamePointer { get; set; }
        public MetaHash NameHash { get; set; }
        public string Name { get; set; } = string.Empty;
        private string_r? NameBlock;

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            this.VFT = reader.ReadUInt32();
            this.BaseReferenceCount = reader.ReadUInt32();
            this.Type = (ClipPropertyAttributeType)reader.ReadByte();
            _ = reader.ReadBytes(7);
            this.NamePointer = reader.ReadUInt64();
            this.NameHash = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            this.Name = reader.ReadStringAt(this.NamePointer) ?? string.Empty;
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            this.NamePointer = (ulong)(this.NameBlock?.FilePosition ?? 0);
            if (this.NameHash == 0 && !string.IsNullOrEmpty(this.Name)) this.NameHash = JenkHash.GenHash(this.Name);
            writer.Write(this.VFT);
            writer.Write(this.BaseReferenceCount);
            writer.Write((byte)this.Type);
            writer.Write(new byte[7]);
            writer.Write(this.NamePointer);
            writer.Write(this.NameHash);
            writer.Write(0u);
        }

        public override IResourceBlock[] GetReferences()
        {
            NameBlock = string.IsNullOrEmpty(Name) ? null : (string_r)Name;
            return NameBlock == null ? [] : [NameBlock];
        }

        public IResourceSystemBlock GetType(ResourceDataReader reader, params object[] parameters)
        {
            reader.Position += 8;
            var type = (ClipPropertyAttributeType)reader.ReadByte();
            reader.Position -= 9;
            return ConstructItem(type);
        }

        public static ClipPropertyAttribute ConstructItem(ClipPropertyAttributeType type)
        {
            switch (type)
            {
                case ClipPropertyAttributeType.Float: return new ClipPropertyAttributeFloat();
                case ClipPropertyAttributeType.Int: return new ClipPropertyAttributeInt();
                case ClipPropertyAttributeType.Bool: return new ClipPropertyAttributeBool();
                case ClipPropertyAttributeType.String: return new ClipPropertyAttributeString();
                case ClipPropertyAttributeType.BitSet: return new ClipPropertyAttributeBitSet();
                case ClipPropertyAttributeType.Vector3: return new ClipPropertyAttributeVector3();
                case ClipPropertyAttributeType.Vector4: return new ClipPropertyAttributeVector4();
                case ClipPropertyAttributeType.Quaternion: return new ClipPropertyAttributeQuaternion();
                case ClipPropertyAttributeType.Matrix34: return new ClipPropertyAttributeMatrix34();
                case ClipPropertyAttributeType.Situation: return new ClipPropertyAttributeSituation();
                case ClipPropertyAttributeType.Data: return new ClipPropertyAttributeData();
                case ClipPropertyAttributeType.HashString: return new ClipPropertyAttributeHashString();
                default: throw new InvalidDataException($"Unsupported clip property attribute type: {type}.");
            }
        }

        public virtual void WriteXml(StringBuilder sb, int indent)
        {
            YcdXml.StringTag(sb, indent, "NameHash", YcdXml.HashString(NameHash));
            if (!string.IsNullOrEmpty(Name)) YcdXml.StringTag(sb, indent, "Name", MetaXml.XmlEscape(Name));
            YcdXml.ValueTag(sb, indent, "Type", Type.ToString());
        }
        public virtual void ReadXml(XmlNode node)
        {
            NameHash = XmlMeta.GetHash(Xml.GetChildInnerText(node, "NameHash"));
            Name = Xml.GetChildInnerText(node, "Name") ?? string.Empty;
            if (NameHash == 0 && !string.IsNullOrEmpty(Name)) NameHash = JenkHash.GenHash(Name);
            Type = Xml.GetEnumValue<ClipPropertyAttributeType>(Xml.GetChildStringAttribute(node, "Type", "value"));
        }
    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class ClipPropertyAttributeFloat : ClipPropertyAttribute
    {
        public override long BlockLength
        {
            get { return 48; }
        }

        public float Value { get; set; }

        public ClipPropertyAttributeFloat() => Type = ClipPropertyAttributeType.Float;

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            base.Read(reader, parameters);

            // read structure data
            this.Value = reader.ReadSingle();
            _ = reader.ReadBytes(12);
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            base.Write(writer, parameters);

            // write structure data
            writer.Write(this.Value);
            writer.Write(new byte[12]);
        }

        public override string ToString()
        {
            return "Float:" + FloatUtil.ToString(Value);
        }


        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            YcdXml.ValueTag(sb, indent, "Value", FloatUtil.ToString(Value));
        }
        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            Value = Xml.GetChildFloatAttribute(node, "Value", "value");
        }
    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class ClipPropertyAttributeInt : ClipPropertyAttribute
    {
        public override long BlockLength
        {
            get { return 48; }
        }

        public int Value { get; set; }

        public ClipPropertyAttributeInt() => Type = ClipPropertyAttributeType.Int;

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            base.Read(reader, parameters);

            // read structure data
            this.Value = reader.ReadInt32();
            _ = reader.ReadBytes(12);
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            base.Write(writer, parameters);

            // write structure data
            writer.Write(this.Value);
            writer.Write(new byte[12]);
        }

        public override string ToString()
        {
            return "Int:" + Value.ToString();
        }


        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            YcdXml.ValueTag(sb, indent, "Value", Value.ToString());
        }
        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            Value = Xml.GetChildIntAttribute(node, "Value", "value");
        }
    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class ClipPropertyAttributeBool : ClipPropertyAttribute
    {
        public override long BlockLength
        {
            get { return 48; }
        }

        public bool Value { get; set; }

        public ClipPropertyAttributeBool() => Type = ClipPropertyAttributeType.Bool;

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            base.Read(reader, parameters);

            // read structure data
            this.Value = reader.ReadByte() != 0;
            _ = reader.ReadBytes(15);
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            base.Write(writer, parameters);

            // write structure data
            writer.Write((byte)(this.Value ? 1 : 0));
            writer.Write(new byte[15]);
        }

        public override string ToString()
        {
            return "Bool:" + Value.ToString();
        }


        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            YcdXml.ValueTag(sb, indent, "Value", Value.ToString().ToLowerInvariant());
        }
        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            Value = Xml.GetChildBoolAttribute(node, "Value", "value");
        }
    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class ClipPropertyAttributeString : ClipPropertyAttribute
    {
        public override long BlockLength
        {
            get { return 48; }
        }

        public ulong ValuePointer { get; set; }
        public ushort ValueLength { get; set; }
        public ushort ValueCapacity { get; set; }

        public string Value = string.Empty;
        private string_r? ValueBlock;

        public ClipPropertyAttributeString() => Type = ClipPropertyAttributeType.String;

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            base.Read(reader, parameters);

            // read structure data
            this.ValuePointer = reader.ReadUInt64();
            this.ValueLength = reader.ReadUInt16();
            this.ValueCapacity = reader.ReadUInt16();
            _ = reader.ReadUInt32();

            //// read reference data
            Value = reader.ReadStringAt(ValuePointer) ?? string.Empty;
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            base.Write(writer, parameters);

            // update structure data
            this.ValuePointer = (ulong)(this.ValueBlock != null ? this.ValueBlock.FilePosition : 0);
            this.ValueLength = (ushort)(Value?.Length ?? 0);
            this.ValueCapacity = string.IsNullOrEmpty(Value) ? (ushort)0 : checked((ushort)(Value.Length + 1));

            // write structure data
            writer.Write(this.ValuePointer);
            writer.Write(this.ValueLength);
            writer.Write(this.ValueCapacity);
            writer.Write(0u);
        }

        public override IResourceBlock[] GetReferences()
        {
            var list = new List<IResourceBlock>(base.GetReferences());
            if (!string.IsNullOrEmpty(Value))
            {
                ValueBlock = (string_r)Value;
                list.Add(ValueBlock);
            }
            else ValueBlock = null;
            return list.ToArray();
        }

        public override string ToString()
        {
            return "String:" + Value;
        }


        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            YcdXml.StringTag(sb, indent, "Value", MetaXml.XmlEscape(Value));
        }
        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            Value = Xml.GetChildInnerText(node, "Value") ?? string.Empty;
            ValueLength = (ushort)(Value?.Length??0);
            ValueCapacity = ValueLength;
        }
    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class ClipPropertyAttributeVector3 : ClipPropertyAttribute
    {
        public override long BlockLength
        {
            get { return 48; }
        }

        public Vector3 Value { get; set; }

        public ClipPropertyAttributeVector3() => Type = ClipPropertyAttributeType.Vector3;

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            base.Read(reader, parameters);

            // read structure data
            Value = reader.ReadVector3();
            _ = reader.ReadSingle();
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            base.Write(writer, parameters);

            // write structure data          
            writer.Write(this.Value);
            writer.Write(0.0f);
        }

        public override string ToString()
        {
            return "Vector3:" + FloatUtil.GetVector3String(Value);
        }


        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            YcdXml.SelfClosingTag(sb, indent, "Value " + FloatUtil.GetVector3XmlString(Value));
        }
        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            Value = Xml.GetChildVector3Attributes(node, "Value");
        }
    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class ClipPropertyAttributeVector4 : ClipPropertyAttribute
    {
        public override long BlockLength
        {
            get { return 48; }
        }

        public Vector4 Value { get; set; }

        public ClipPropertyAttributeVector4() => Type = ClipPropertyAttributeType.Vector4;

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            base.Read(reader, parameters);

            // read structure data
            this.Value = reader.ReadVector4();
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            base.Write(writer, parameters);

            // write structure data
            writer.Write(this.Value);
        }

        public override string ToString()
        {
            return "Vector4:" + FloatUtil.GetVector4String(Value);
        }


        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            YcdXml.SelfClosingTag(sb, indent, "Value " + FloatUtil.GetVector4XmlString(Value));
        }
        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            Value = Xml.GetChildVector4Attributes(node, "Value");
        }
    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class ClipPropertyAttributeBitSet : ClipPropertyAttribute
    {
        public override long BlockLength => 48;

        public atBitSet Value { get; set; } = new();

        public ClipPropertyAttributeBitSet()
        {
            Type = ClipPropertyAttributeType.BitSet;
        }

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            base.Read(reader, parameters);
            Value = reader.ReadRequiredBlock<atBitSet>();
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            base.Write(writer, parameters);
            writer.WriteBlock(Value);
        }

        public override Tuple<long, IResourceBlock>[] GetParts() => [new(0x20, Value)];

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            YcdXml.ValueTag(sb, indent, "BitCount", Value.BitCount.ToString());
            YcdXml.WriteRawArray(sb, Value.Words, indent, "Words", "", null, 8);
        }

        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            Value = new atBitSet
            {
                BitCount = (ushort)Xml.GetChildUIntAttribute(node, "BitCount", "value"),
                Words = Xml.GetChildRawUintArray(node, "Words")
            };
        }

        public override string ToString() => "BitSet:" + Value;
    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class ClipPropertyAttributeQuaternion : ClipPropertyAttribute
    {
        public override long BlockLength => 48;

        public Quaternion Value { get; set; } = Quaternion.Identity;

        public ClipPropertyAttributeQuaternion()
        {
            Type = ClipPropertyAttributeType.Quaternion;
        }

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            base.Read(reader, parameters);
            Value = new Quaternion(reader.ReadVector4());
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            base.Write(writer, parameters);
            writer.Write(Value.ToVector4());
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            YcdXml.SelfClosingTag(sb, indent, "Value " + FloatUtil.GetVector4XmlString(Value.ToVector4()));
        }

        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            Value = new Quaternion(Xml.GetChildVector4Attributes(node, "Value"));
        }

        public override string ToString() => "Quaternion:" + FloatUtil.GetVector4String(Value.ToVector4());
    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class ClipPropertyAttributeMatrix34 : ClipPropertyAttribute
    {
        public override long BlockLength => 96;

        public Vector4 Column0 { get; set; }
        public Vector4 Column1 { get; set; }
        public Vector4 Column2 { get; set; }
        public Vector4 Column3 { get; set; }

        public ClipPropertyAttributeMatrix34()
        {
            Type = ClipPropertyAttributeType.Matrix34;
        }

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            base.Read(reader, parameters);
            Column0 = reader.ReadVector4();
            Column1 = reader.ReadVector4();
            Column2 = reader.ReadVector4();
            Column3 = reader.ReadVector4();
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            base.Write(writer, parameters);
            writer.Write(Column0);
            writer.Write(Column1);
            writer.Write(Column2);
            writer.Write(Column3);
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            YcdXml.SelfClosingTag(sb, indent, "Column0 " + FloatUtil.GetVector4XmlString(Column0));
            YcdXml.SelfClosingTag(sb, indent, "Column1 " + FloatUtil.GetVector4XmlString(Column1));
            YcdXml.SelfClosingTag(sb, indent, "Column2 " + FloatUtil.GetVector4XmlString(Column2));
            YcdXml.SelfClosingTag(sb, indent, "Column3 " + FloatUtil.GetVector4XmlString(Column3));
        }

        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            Column0 = Xml.GetChildVector4Attributes(node, "Column0");
            Column1 = Xml.GetChildVector4Attributes(node, "Column1");
            Column2 = Xml.GetChildVector4Attributes(node, "Column2");
            Column3 = Xml.GetChildVector4Attributes(node, "Column3");
        }

        public override string ToString() => "Matrix34";
    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class ClipPropertyAttributeSituation : ClipPropertyAttribute
    {
        public override long BlockLength => 64;

        public Quaternion Rotation { get; set; } = Quaternion.Identity;
        public Vector4 Position { get; set; }

        public ClipPropertyAttributeSituation()
        {
            Type = ClipPropertyAttributeType.Situation;
        }

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            base.Read(reader, parameters);
            Rotation = new Quaternion(reader.ReadVector4());
            Position = reader.ReadVector4();
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            base.Write(writer, parameters);
            writer.Write(Rotation.ToVector4());
            writer.Write(Position);
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            YcdXml.SelfClosingTag(sb, indent, "Rotation " + FloatUtil.GetVector4XmlString(Rotation.ToVector4()));
            YcdXml.SelfClosingTag(sb, indent, "Position " + FloatUtil.GetVector4XmlString(Position));
        }

        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            Rotation = new Quaternion(Xml.GetChildVector4Attributes(node, "Rotation"));
            Position = Xml.GetChildVector4Attributes(node, "Position");
        }

        public override string ToString() => "Situation";
    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class ClipPropertyAttributeData : ClipPropertyAttribute
    {
        public override long BlockLength => 48;

        public ResourceSimpleList64_byte Value { get; set; } = new();

        public ClipPropertyAttributeData()
        {
            Type = ClipPropertyAttributeType.Data;
        }

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            base.Read(reader, parameters);
            Value = reader.ReadRequiredBlock<ResourceSimpleList64_byte>();
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            base.Write(writer, parameters);
            writer.WriteBlock(Value);
        }

        public override Tuple<long, IResourceBlock>[] GetParts() => [new(0x20, Value)];

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            YcdXml.WriteRawArray(sb, Value.data_items, indent, "Value", "", YcdXml.FormatHexByte, 16);
        }

        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            Value = new ResourceSimpleList64_byte { data_items = Xml.GetChildRawByteArray(node, "Value") };
        }

        public override string ToString() => $"Data:{Value.data_items.Length} bytes";
    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class ClipPropertyAttributeHashString : ClipPropertyAttribute
    {
        public override long BlockLength => 0x30;

        public MetaHash Value { get; set; }

        public ClipPropertyAttributeHashString() => Type = ClipPropertyAttributeType.HashString;

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            base.Read(reader, parameters);

            // read structure data
            this.Value = reader.ReadUInt32();
            _ = reader.ReadBytes(12);
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            base.Write(writer, parameters);

            // write structure data
            writer.Write(this.Value);
            writer.Write(new byte[12]);
        }

        public override string ToString()
        {
            return "Hash:" + Value.ToString();
        }


        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            YcdXml.StringTag(sb, indent, "Value", YcdXml.HashString(Value));
        }
        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            Value = XmlMeta.GetHash(Xml.GetChildInnerText(node, "Value"));
        }
    }
    public enum ClipPropertyAttributeType : byte
    {
        None = 0,
        Float = 1,
        Int = 2,
        Bool = 3,
        String = 4,
        BitSet = 5,
        Vector3 = 6,
        Vector4 = 7,
        Quaternion = 8,
        Matrix34 = 9,
        Situation = 10,
        Data = 11,
        HashString = 12,
    }


    [TypeConverter(typeof(ExpandableObjectConverter))] public class ClipTagList : ResourceSystemBlock
    {
        public override long BlockLength
        {
            get { return 32; }
        }

        // structure data
        public ulong TagsPointer { get; set; }
        public ushort TagCount1 { get; set; }
        public ushort TagCount2 { get; set; }
        public bool HasBlockTags { get; set; }

        // reference data
        public ResourcePointerArray64<ClipTag>? Tags { get; set; }

        public ClipTag[] AllTags { get; set; } = [];


        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            // read structure data
            this.TagsPointer = reader.ReadUInt64();
            this.TagCount1 = reader.ReadUInt16();
            this.TagCount2 = reader.ReadUInt16();
            _ = reader.ReadUInt32();
            this.HasBlockTags = reader.ReadByte() != 0;
            _ = reader.ReadBytes(15);

            // read reference data
            this.Tags = reader.ReadBlockAt<ResourcePointerArray64<ClipTag>>(
                this.TagsPointer, // offset
                this.TagCount1
            );

            BuildAllTags();

            if (TagCount1 != TagCount2)
            { }
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            // update structure data
            this.TagsPointer = (ulong)(this.Tags != null ? this.Tags.FilePosition : 0);
            this.TagCount1 = (ushort)(this.Tags != null ? this.Tags.Count : 0);
            this.TagCount2 = this.TagCount1;

            BuildAllTags(); //just in case? updates HasBlockTag

            // write structure data
            writer.Write(this.TagsPointer);
            writer.Write(this.TagCount1);
            writer.Write(this.TagCount2);
            writer.Write(0u);
            writer.Write((byte)(this.HasBlockTags ? 1 : 0));
            writer.Write(new byte[15]);
        }

        public override IResourceBlock[] GetReferences()
        {
            var list = new List<IResourceBlock>();
            if (Tags != null) list.Add(Tags);
            return list.ToArray();
        }

        public override string ToString()
        {
            return "Count: " + (AllTags?.Length ?? 0).ToString();
        }

        public void BuildAllTags()
        {
            AllTags = [];
            if ((Tags != null) && (Tags.data_items != null))
            {
                List<ClipTag> tl = new();
                foreach (var te in Tags.data_items)
                {
                    if (te != null) tl.Add(te);
                }
                AllTags = tl.ToArray();
            }


            bool hasBlock = false;
            if (AllTags != null)
            {
                foreach (var tag in AllTags)
                {
                    if (tag.NameHash == (uint)MetaName.block)
                    { hasBlock = true; break; }
                }
            }
            HasBlockTags = hasBlock;

        }

        public void AssignTagOwners()
        {
            if (Tags?.data_items == null) return;
            foreach (var tag in Tags.data_items)
            {
                tag.Tags = this;
            }
        }
    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public class ClipTag : ClipProperty
    {
        public override long BlockLength
        {
            get { return 80; }
        }

        public float StartPhase { get; set; }
        public float EndPhase { get; set; }
        public ulong TagsPointer { get; set; }

        // reference data
        public ClipTagList? Tags { get; set; }


        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            base.Read(reader, parameters);

            // read structure data
            this.StartPhase = reader.ReadSingle();
            this.EndPhase = reader.ReadSingle();
            this.TagsPointer = reader.ReadUInt64();

            // read reference data
            this.Tags = reader.ReadBlockAt<ClipTagList>(
                this.TagsPointer // offset
            );
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            base.Write(writer, parameters);

            // update structure data
            this.TagsPointer = (ulong)(this.Tags != null ? this.Tags.FilePosition : 0);

            // write structure data         
            writer.Write(this.StartPhase);
            writer.Write(this.EndPhase);
            writer.Write(this.TagsPointer);
        }

        public override IResourceBlock[] GetReferences()
        {
            var list = new List<IResourceBlock>(base.GetReferences());
            if (Tags != null) list.Add(Tags);
            return list.ToArray();
        }

        public override string ToString()
        {
            return base.ToString() + ": " + StartPhase.ToString() + ", " + EndPhase.ToString();
        }


        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            YcdXml.ValueTag(sb, indent, "StartPhase", FloatUtil.ToString(StartPhase));
            YcdXml.ValueTag(sb, indent, "EndPhase", FloatUtil.ToString(EndPhase));
        }
        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            StartPhase = Xml.GetChildFloatAttribute(node, "StartPhase", "value");
            EndPhase = Xml.GetChildFloatAttribute(node, "EndPhase", "value");
        }
    }


}
