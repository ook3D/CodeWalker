using SharpDX;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace CodeWalker.GameFiles
{

    [TypeConverter(typeof(ExpandableObjectConverter))] public class grmShaderGroup : ResourceSystemBlock
    {
        public override long BlockLength
        {
            get { return 64; }
        }

        // structure data
        public uint VFT { get; set; } = 1080113136;
        public ulong TextureDictionaryPointer { get; set; }
        public ulong ShadersPointer { get; set; }
        public ushort ShadersCount { get; set; }
        public ushort ShadersCapacity { get; set; }
        public ulong ShaderGroupVarsPointer;
        public ushort ShaderGroupVarsCount;
        public ushort ShaderGroupVarsCapacity;
        public ushort ContainerSizeQW { get; set; }
        public bool HasInstancedShader;
        public ulong ShaderDatasPointer;

        // reference data
        public TextureDictionary? TextureDictionary { get; set; }
        public ResourcePointerArray64<grcInstanceData>? Shaders { get; set; }


        public int TotalParameters
        {
            get
            {
                int c = 0;
                if (Shaders?.data_items != null)
                {
                    foreach (var s in Shaders.data_items)
                    {
                        c += s.Count;
                    }
                }
                return c;
            }
        }


        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            // read structure data
            this.VFT = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            this.TextureDictionaryPointer = reader.ReadUInt64();
            this.ShadersPointer = reader.ReadUInt64();
            this.ShadersCount = reader.ReadUInt16();
            this.ShadersCapacity = reader.ReadUInt16();
            _ = reader.ReadUInt32();
            this.ShaderGroupVarsPointer = reader.ReadUInt64();
            this.ShaderGroupVarsCount = reader.ReadUInt16();
            this.ShaderGroupVarsCapacity = reader.ReadUInt16();
            _ = reader.ReadUInt32();
            this.ContainerSizeQW = reader.ReadUInt16();
            this.HasInstancedShader = reader.ReadByte() != 0;
            _ = reader.ReadBytes(5);
            this.ShaderDatasPointer = reader.ReadUInt64();

            // read reference data
            this.TextureDictionary = reader.ReadBlockAt<TextureDictionary>(
                this.TextureDictionaryPointer // offset
            );
            this.Shaders = reader.ReadBlockAt<ResourcePointerArray64<grcInstanceData>>(
                this.ShadersPointer, // offset
                this.ShadersCount
            );

        }
        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            // update structure data
            this.TextureDictionaryPointer = (ulong)(this.TextureDictionary != null ? this.TextureDictionary.FilePosition : 0);
            this.ShadersPointer = (ulong)(this.Shaders != null ? this.Shaders.FilePosition : 0);
            this.ShadersCount = (ushort)(this.Shaders != null ? this.Shaders.Count : 0);
            this.ShadersCapacity = this.ShadersCount;
            // In vanilla files this includes the size of the Shaders array, grcInstanceData blocks and, sometimes,
            // grcInstanceDataEntriesBlock allocations since they are placed contiguously after the ShaderGroup in the file.
            // But CW doesn't always do this so we only include the ShaderGroup size.
            //(ignore for gen9)
            this.ContainerSizeQW = writer.IsGen9 ? (ushort)0 : (ushort)(this.BlockLength / 16);

            // write structure data
            writer.Write(this.VFT);
            writer.Write(1u);
            writer.Write(this.TextureDictionaryPointer);
            writer.Write(this.ShadersPointer);
            writer.Write(this.ShadersCount);
            writer.Write(this.ShadersCapacity);
            writer.Write(0u);
            writer.Write(this.ShaderGroupVarsPointer);
            writer.Write(this.ShaderGroupVarsCount);
            writer.Write(this.ShaderGroupVarsCapacity);
            writer.Write(0u);
            writer.Write(this.ContainerSizeQW);
            writer.Write((byte)(this.HasInstancedShader ? 1 : 0));
            writer.Write(new byte[5]);
            writer.Write(this.ShaderDatasPointer);
        }
        public void WriteXml(StringBuilder sb, int indent, string ddsfolder)
        {
            if (TextureDictionary != null)
            {
                TextureDictionary.WriteXmlNode(TextureDictionary, sb, indent, ddsfolder, "TextureDictionary");
            }
            YdrXml.WriteItemArray(sb, Shaders?.data_items ?? [], indent, "Shaders");
        }
        public void ReadXml(XmlNode node, string ddsfolder)
        {
            var tnode = node.SelectSingleNode("TextureDictionary");
            if (tnode != null)
            {
                TextureDictionary = TextureDictionary.ReadXmlNode(tnode, ddsfolder);
            }
            var shaders = XmlMeta.ReadItemArray<grcInstanceData>(node, "Shaders");
            if (shaders != null)
            {
                Shaders = new ResourcePointerArray64<grcInstanceData>();
                Shaders.data_items = shaders;
            }


            if ((shaders != null) && (TextureDictionary != null))
            {
                foreach (var shader in shaders)
                {
                    var sparams = shader?.EntriesBlock?.Entries;
                    if (sparams != null)
                    {
                        foreach (var sparam in sparams)
                        {
                            if (sparam.Data is TextureBase tex)
                            {
                                var tex2 = TextureDictionary.Lookup(tex.NameHash);
                                if (tex2 != null)
                                {
                                    sparam.Data = tex2;//swap the parameter out for the embedded texture
                                }
                            }
                        }
                    }
                }
            }
        }


        public override IResourceBlock[] GetReferences()
        {
            var list = new List<IResourceBlock>();
            if (TextureDictionary != null) list.Add(TextureDictionary);
            if (Shaders != null) list.Add(Shaders);
            return list.ToArray();
        }
    }

    [TypeConverter(typeof(ExpandableObjectConverter))] public class grcInstanceData : ResourceSystemBlock, IMetaXmlItem
    {
        public override long BlockLength => 48;
        public override long BlockLength_Gen9 => 64;

        // structure data
        public ulong EntriesPointer { get; set; }
        public MetaHash BasisHashCode { get; set; }
        public byte Count { get; set; }
        public byte DrawBucket { get; set; }
        public byte PhysMtlDeprecated { get; set; }
        public byte Flags { get; set; } = 0x80;
        public ushort SpuSize { get; set; }
        public ushort TotalSize { get; set; }
        public MetaHash MaterialHashCode { get; set; }
        public uint DrawBucketMask { get; set; }
        public bool IsInstanced { get; set; }
        public byte UserFlags { get; set; }
        public byte TextureCount { get; set; }
        public uint SortKeyDeprecated { get; set; }

        [Browsable(false)] public MetaHash Name { get => BasisHashCode; set => BasisHashCode = value; }
        [Browsable(false)] public MetaHash FileName { get => MaterialHashCode; set => MaterialHashCode = value; }
        [Browsable(false)] public byte RenderBucket { get => DrawBucket; set => DrawBucket = value; }

        // reference data
        public grcInstanceDataEntriesBlock? EntriesBlock { get; set; }

        // gen9 structure data
        public MetaHash G9_Preset { get; set; } = 0x6D657461;
        public ulong G9_TextureRefsPointer { get; set; }
        public ulong G9_UnknownParamsPointer { get; set; }
        public ulong G9_ParamInfosPointer { get; set; }
        public ShaderParamInfosG9? G9_ParamInfos { get; set; }

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            if (reader.IsGen9)
            {
                BasisHashCode = new MetaHash(reader.ReadUInt32());
                G9_Preset = reader.ReadUInt32();
                EntriesPointer = reader.ReadUInt64();                     // m_parameters
                G9_TextureRefsPointer = reader.ReadUInt64();
                G9_UnknownParamsPointer = reader.ReadUInt64();//something to do with grass_batch (instance data?)
                G9_ParamInfosPointer = reader.ReadUInt64();                // m_parameterData (sgaShaderParamData)
                _ = reader.ReadBytes(16);
                IsInstanced = reader.ReadByte() != 0;
                DrawBucket = reader.ReadByte();
                TotalSize = reader.ReadUInt16();//==EntriesBlock.G9_DataSize
                DrawBucketMask = reader.ReadUInt32();

                G9_ParamInfos = reader.ReadBlockAt<ShaderParamInfosG9>(G9_ParamInfosPointer);
                EntriesBlock = reader.ReadBlockAt<grcInstanceDataEntriesBlock>(EntriesPointer, 0, this);
                MaterialHashCode = JenkHash.GenHash(BasisHashCode.ToCleanString() + ".sps");//TODO: get mapping from G9_Preset to legacy MaterialHashCode

                if (G9_UnknownParamsPointer != 0)
                { }
                switch (G9_Preset)
                {
                    case 0x6D657461:
                        break;
                    default:
                        break;
                }

            }
            else
            {

                // read structure data
                this.EntriesPointer = reader.ReadUInt64();
                this.BasisHashCode = new MetaHash(reader.ReadUInt32());
                _ = reader.ReadUInt32();
                this.Count = reader.ReadByte();
                this.DrawBucket = reader.ReadByte();
                this.PhysMtlDeprecated = reader.ReadByte();
                this.Flags = reader.ReadByte();
                this.SpuSize = reader.ReadUInt16();
                this.TotalSize = reader.ReadUInt16();
                this.MaterialHashCode = new MetaHash(reader.ReadUInt32());
                _ = reader.ReadUInt32();
                this.DrawBucketMask = reader.ReadUInt32();
                this.IsInstanced = reader.ReadByte() != 0;
                this.UserFlags = reader.ReadByte();
                _ = reader.ReadByte();
                this.TextureCount = reader.ReadByte();
                this.SortKeyDeprecated = reader.ReadUInt32();
                _ = reader.ReadUInt32();

                // read reference data
                this.EntriesBlock = reader.ReadBlockAt<grcInstanceDataEntriesBlock>(
                    this.EntriesPointer, // offset
                    this.Count,
                    this
                );

            }
        }
        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            if (writer.IsGen9)
            {
                Count = (byte)(EntriesBlock?.Entries.Length ?? 0);
                TotalSize = (ushort)(EntriesBlock?.G9_DataSize ?? 0);
                EntriesPointer = (ulong)(EntriesBlock?.FilePosition ?? 0);
                G9_ParamInfosPointer = (ulong)(G9_ParamInfos?.FilePosition ?? 0);
                G9_TextureRefsPointer = (EntriesBlock != null && EntriesPointer != 0) ? (EntriesPointer + EntriesBlock.G9_TexturesOffset) : 0;
                G9_UnknownParamsPointer = (EntriesBlock != null && EntriesPointer != 0) ? (EntriesPointer + EntriesBlock.G9_UnknownsOffset) : 0;

                writer.Write((uint)BasisHashCode);
                writer.Write((uint)G9_Preset);
                writer.Write(EntriesPointer);
                writer.Write(G9_TextureRefsPointer);
                writer.Write(G9_UnknownParamsPointer);
                writer.Write(G9_ParamInfosPointer);
                writer.Write(new byte[16]);
                writer.Write((byte)(IsInstanced ? 1 : 0));
                writer.Write(DrawBucket);
                writer.Write(TotalSize);
                writer.Write(DrawBucketMask);

            }
            else
            {
                // update structure data
                this.EntriesPointer = (ulong)(this.EntriesBlock != null ? this.EntriesBlock.FilePosition : 0);
                this.Count = (byte)(this.EntriesBlock?.Entries.Length ?? 0);
                if (this.EntriesBlock != null)
                {
                    this.SpuSize = this.EntriesBlock.SpuSize;
                    this.TotalSize = this.EntriesBlock.TotalSize;
                    this.TextureCount = this.EntriesBlock.TextureCount;
                }

                // write structure data
                writer.Write(this.EntriesPointer);
                writer.Write(this.BasisHashCode.Hash);
                writer.Write(0u);
                writer.Write(this.Count);
                writer.Write(this.DrawBucket);
                writer.Write(this.PhysMtlDeprecated);
                writer.Write(this.Flags);
                writer.Write(this.SpuSize);
                writer.Write(this.TotalSize);
                writer.Write(this.MaterialHashCode.Hash);
                writer.Write(0u);
                writer.Write(this.DrawBucketMask);
                writer.Write((byte)(this.IsInstanced ? 1 : 0));
                writer.Write(this.UserFlags);
                writer.Write((byte)0);
                writer.Write(this.TextureCount);
                writer.Write(this.SortKeyDeprecated);
                writer.Write(0u);
            }
        }
        public void WriteXml(StringBuilder sb, int indent)
        {
            YdrXml.StringTag(sb, indent, "Name", YdrXml.HashString(BasisHashCode));
            YdrXml.StringTag(sb, indent, "FileName", YdrXml.HashString(MaterialHashCode));
            YdrXml.ValueTag(sb, indent, "RenderBucket", DrawBucket.ToString());
            if (EntriesBlock != null)
            {
                YdrXml.OpenTag(sb, indent, "Parameters");
                EntriesBlock.WriteXml(sb, indent + 1);
                YdrXml.CloseTag(sb, indent, "Parameters");
            }
        }
        public void ReadXml(XmlNode node)
        {
            BasisHashCode = XmlMeta.GetHash(Xml.GetChildInnerText(node, "Name"));
            MaterialHashCode = XmlMeta.GetHash(Xml.GetChildInnerText(node, "FileName"));
            DrawBucket = (byte)Xml.GetChildUIntAttribute(node, "RenderBucket", "value");
            DrawBucketMask = ((1u << DrawBucket) | 0xFF00u);
            var pnode = node.SelectSingleNode("Parameters");
            if (pnode != null)
            {
                EntriesBlock = new grcInstanceDataEntriesBlock();
                EntriesBlock.Owner = this;
                EntriesBlock.ReadXml(pnode);

                Count = (byte)EntriesBlock.Entries.Length;
                SpuSize = EntriesBlock.SpuSize;
                TotalSize = EntriesBlock.TotalSize;
                TextureCount = EntriesBlock.TextureCount;
            }
        }


        public void EnsureGen9()
        {
            if (EntriesBlock == null) return;//need this
            //get G9_ParamInfos from GameFileCache.ShadersGen9ConversionData
            //calculate EntriesBlock.G9_DataSize
            //build EntriesBlock.G9_ fields from G9_ParamInfos


            GameFileCache.EnsureShadersGen9ConversionData();
            GameFileCache.ShadersGen9ConversionData.TryGetValue(BasisHashCode, out var dc);

            if (dc == null)
            { }

            var bsizs = dc?.BufferSizes;
            var pinfs = dc?.ParamInfos;
            var tc = 0;
            var uc = 0;
            var sc = 0;
            var bs = 0;
            var multi = 1;
            var bsizsu = new uint[bsizs?.Length ?? 0];
            if (bsizs != null)
            {
                multi = 3;
                for (int i = 0; i < bsizs.Length; i++)
                {
                    var bsiz = bsizs[i];
                    bsizsu[i] = (uint)bsiz;
                    bs += bsiz;
                }
            }
            if (pinfs != null)
            {
                multi = multi << 2;
                foreach (var pinf in pinfs)
                {
                    switch (pinf.Type)
                    {
                        case ShaderParamTypeG9.Texture: tc++; break;
                        case ShaderParamTypeG9.Unknown: uc++; break;
                        case ShaderParamTypeG9.Sampler: sc++; break;
                    }
                }
            }
            var pinfos = new ShaderParamInfosG9();
            pinfos.Params = pinfs ?? [];
            pinfos.NumBuffers = (byte)(bsizs?.Length ?? 0);
            pinfos.NumParams = (byte)(pinfs?.Length ?? 0);
            pinfos.NumTextures = (byte)tc;
            pinfos.NumUnknowns = (byte)uc;
            pinfos.NumSamplers = (byte)sc;
            pinfos.Unknown0 = (byte)0x00;
            pinfos.Unknown1 = (byte)0x01;
            pinfos.BufferCopyMultiplier = (byte)multi;


            var ptrslen = pinfos.NumBuffers * 8 * multi;
            var bufslen = (int)(bs) * multi;
            var texslen = tc * 8 * multi;
            var unkslen = uc * 8 * multi;
            var smpslen = sc;
            var totlen = ptrslen + bufslen + texslen + unkslen + smpslen;
            EntriesBlock.G9_BuffersDataSize = (uint)bufslen;
            EntriesBlock.G9_TexturesOffset = (uint)(ptrslen + bufslen);
            EntriesBlock.G9_UnknownsOffset = (uint)(ptrslen + bufslen + texslen);
            EntriesBlock.G9_DataSize = totlen;
            TotalSize = (ushort)totlen;



            if (G9_ParamInfos != null)
            { }
            G9_ParamInfos = pinfos;
            EntriesBlock.G9_ParamInfos = pinfos;

            if (EntriesBlock.G9_Samplers != null)
            { }
            EntriesBlock.G9_Samplers = dc?.SamplerValues ?? [];

            if (EntriesBlock.G9_BufferSizes != null)
            { }
            EntriesBlock.G9_BufferSizes = bsizsu;


            var parr = EntriesBlock.Entries;
            if (parr != null)
            {
                foreach (var p in parr)
                {
                    if (p.Data is Texture etex)//in case embedded textures are actual texture refs, convert them to TextureBase
                    {
                        var btex = new TextureBase();
                        btex.Name = etex.Name;
                        btex.NameHash = etex.NameHash;
                        btex.G9_Dimension = etex.G9_Dimension;
                        btex.G9_Flags = 0x00260000;
                        //btex.G9_SRV = new ShaderResourceViewG9();
                        //btex.G9_SRV.Dimension = etex.G9_SRV?.Dimension ?? ShaderResourceViewDimensionG9.Texture2D;
                        p.Data = btex;
                    }
                    else if (p.Data is TextureBase btex)
                    {
                        btex.VFT = 0;
                        btex.Unknown_4h = 1;
                        if (btex.G9_Flags == 0) btex.G9_Flags = 0x00260000;
                        //if (btex.G9_SRV == null)//make sure the SRVs for these params exist
                        //{
                        //    btex.G9_SRV = new ShaderResourceViewG9();
                        //    switch (btex.G9_Dimension)
                        //    {
                        //        case TextureDimensionG9.Texture2D: btex.G9_SRV.Dimension = ShaderResourceViewDimensionG9.Texture2D; break;
                        //        case TextureDimensionG9.Texture3D: btex.G9_SRV.Dimension = ShaderResourceViewDimensionG9.Texture3D; break;
                        //        case TextureDimensionG9.TextureCube: btex.G9_SRV.Dimension = ShaderResourceViewDimensionG9.TextureCube; break;
                        //    }
                        //}
                    }
                }
            }


        }


        public override IResourceBlock[] GetReferences()
        {
            var list = new List<IResourceBlock>();
            if (G9_ParamInfos != null) list.Add(G9_ParamInfos);
            if (EntriesBlock != null) list.Add(EntriesBlock);
            return list.ToArray();
        }


        public override string ToString()
        {
            return BasisHashCode.ToString() + " (" + MaterialHashCode.ToString() + ")";
        }

        [TypeConverter(typeof(ExpandableObjectConverter))] public class Entry
        {
            public byte Count { get; set; }
            public byte Register { get; set; }
            public byte SamplerStateSet { get; set; }
            public byte SavedSamplerStateSet { get; set; }
            public ulong DataPointer { get; set; }

            public object? Data { get; set; }

            public void Read(ResourceDataReader reader)
            {
                this.Count = reader.ReadByte();
                this.Register = reader.ReadByte();
                this.SamplerStateSet = reader.ReadByte();
                this.SavedSamplerStateSet = reader.ReadByte();
                _ = reader.ReadUInt32();
                this.DataPointer = reader.ReadUInt64();
            }
            public void Write(ResourceDataWriter writer)
            {
                writer.Write(this.Count);
                writer.Write(this.Register);
                writer.Write(this.SamplerStateSet);
                writer.Write(this.SavedSamplerStateSet);
                writer.Write(0u);
                writer.Write(this.DataPointer);
            }

            public override string ToString()
            {
                return (Data != null) ? (Data.ToString() ?? string.Empty) : (Count.ToString() + ": " + DataPointer.ToString());
            }
        }
    }

    [TypeConverter(typeof(ExpandableObjectConverter))] public class grcInstanceDataEntriesBlock : ResourceSystemBlock
    {
        // Four sub-render threads plus the render thread on the legacy console targets.
        public override long BlockLength => TotalSize * 5L;
        public override long BlockLength_Gen9 => G9_DataSize;

        public ushort SpuSize
        {
            get
            {
                ushort size = (ushort)(Entries.Length * 16);
                foreach (var x in Entries)
                {
                    size += (ushort)(16 * x.Count);
                }
                return size;
            }
        }
        public ushort TotalSize
        {
            get
            {
                var nameHashesSize = (Entries.Length * 4 + 15) & ~15;
                return (ushort)(SpuSize + 32 + nameHashesSize);
            }
        }

        public byte TextureCount
        {
            get
            {
                byte c = 0;
                foreach (var x in Entries)
                {
                    if (x.Count == 0) c++;
                }
                return c;
            }
        }

        public grcInstanceData.Entry[] Entries { get; set; } = [];
        public MetaName[] NameHashes { get; set; } = [];
        public grcInstanceData? Owner { get; set; }

        private ResourceSystemStructBlock<Vector4>?[] FloatDataBlocks = [];


        // gen9 data
        public long G9_DataSize { get; set; }
        public ShaderParamInfosG9? G9_ParamInfos { get; set; }
        public ulong[] G9_BufferPtrs { get; set; } = [];//4x copies of buffers.. buffer data immediately follows pointers array
        public uint[] G9_BufferSizes { get; set; } = [];//sizes of all buffers
        public uint G9_BuffersDataSize { get; set; }
        public uint G9_TexturesOffset { get; set; }
        public uint G9_UnknownsOffset { get; set; }
        public ulong[] G9_TexturePtrs { get; set; } = [];
        public ulong[] G9_UnknownData { get; set; } = [];
        public byte[] G9_Samplers { get; set; } = [];


        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            var entryCount = Convert.ToInt32(parameters[0]);
            Owner = (grcInstanceData)parameters[1];

            if (reader.IsGen9)
            {
                GameFileCache.EnsureShadersGen9ConversionData();
                GameFileCache.ShadersGen9ConversionData.TryGetValue((Owner ?? throw new InvalidOperationException("Shader parameters have no owner.")).BasisHashCode, out var dc);
                var paramap = dc?.ParamsMapGen9ToLegacy;

                G9_ParamInfos = Owner?.G9_ParamInfos ?? throw new InvalidOperationException("Gen9 shader parameter information is missing.");
                var multi = (int)G9_ParamInfos.BufferCopyMultiplier;//12
                var mult = (uint)multi;

                var bcnt = G9_ParamInfos.NumBuffers;
                var spos = reader.Position;
                G9_BufferPtrs = reader.ReadStructs<ulong>(bcnt * mult);//12x copies of buffers...... !!
                G9_BufferSizes = new uint[bcnt];//this might affect load performance slightly, but needed for XML and saving
                for (int i = 0; i < bcnt; i++)
                {
                    G9_BufferSizes[i] = (uint)(G9_BufferPtrs[i + 1] - G9_BufferPtrs[i]);
                }
                var p0 = 0ul;
                var p1 = 0ul;
                if ((G9_BufferPtrs != null) && (G9_BufferPtrs.Length > bcnt))
                {
                    p0 = G9_BufferPtrs[0];
                    p1 = G9_BufferPtrs[bcnt];
                }
                var ptrslen = bcnt * 8 * multi;
                var bufslen = (int)(p1 - p0) * multi;
                var texslen = G9_ParamInfos.NumTextures * 8 * multi;
                var unkslen = G9_ParamInfos.NumUnknowns * 8 * multi;
                var smpslen = G9_ParamInfos.NumSamplers;
                var totlen = ptrslen + bufslen + texslen + unkslen + smpslen;
                G9_BuffersDataSize = (uint)bufslen;
                G9_TexturesOffset = (uint)(ptrslen + bufslen);
                G9_UnknownsOffset = (uint)(ptrslen + bufslen + texslen);
                G9_DataSize = totlen;

                if (Owner.G9_TextureRefsPointer != 0)
                {
                    G9_TexturePtrs = reader.ReadUlongsAt(Owner.G9_TextureRefsPointer, G9_ParamInfos.NumTextures * mult, false) ?? [];
                }
                if (Owner.G9_UnknownParamsPointer != 0)
                {
                    G9_UnknownData = reader.ReadUlongsAt(Owner.G9_UnknownParamsPointer, G9_ParamInfos.NumUnknowns * mult, false) ?? [];
                }
                if (G9_ParamInfos.NumSamplers > 0)
                {
                    G9_Samplers = reader.ReadBytesAt((ulong)(spos + (ptrslen + bufslen + texslen + unkslen)), G9_ParamInfos.NumSamplers, false) ?? [];
                }

                var paras = new List<grcInstanceData.Entry>();
                var hashes = new List<MetaName>();
                foreach (var info in G9_ParamInfos.Params)
                {
                    var hash = info.Name.Hash;
                    if ((paramap != null) && paramap.TryGetValue(hash, out var oldhash))
                    {
                        hash = oldhash;
                    }

                    if (info.Type == ShaderParamTypeG9.Texture)
                    {
                        var p = new grcInstanceData.Entry();
                        p.Count = 0;
                        p.DataPointer = G9_TexturePtrs[info.TextureIndex];
                        p.Data = reader.ReadBlockAt<TextureBase>(p.DataPointer);
                        paras.Add(p);
                        hashes.Add((MetaName)hash);
                        if (p.Data is TextureBase ptex)
                        {
                            if (ptex.G9_SRV != null)
                            { }
                        }
                    }
                    else if (info.Type == ShaderParamTypeG9.CBuffer)
                    {
                        uint fcnt = info.ParamLength / 4u;
                        uint arrsiz = info.ParamLength / 16u;
                        var p = new grcInstanceData.Entry();
                        p.Count = (byte)Math.Max(arrsiz, 1);
                        if ((info.ParamLength) % 4 != 0)
                        { }
                        var cbi = info.CBufferIndex;
                        var baseptr = ((G9_BufferPtrs != null) && (G9_BufferPtrs.Length > cbi)) ? (long)G9_BufferPtrs[cbi] : 0;
                        if (baseptr != 0)
                        {
                            var ptr = baseptr + info.ParamOffset;
                            switch (fcnt)
                            {
                                case 0:
                                    break;
                                case 1: p.Data = new Vector4(reader.ReadStructAt<float>(ptr), 0, 0, 0); break;
                                case 2: p.Data = new Vector4(reader.ReadStructAt<Vector2>(ptr), 0, 0); break;
                                case 3: p.Data = new Vector4(reader.ReadStructAt<Vector3>(ptr), 0); break;
                                case 4: p.Data = reader.ReadStructAt<Vector4>(ptr); break;
                                default:
                                    if (arrsiz == 0)
                                    { }
                                    p.Data = reader.ReadStructsAt<Vector4>((ulong)ptr, arrsiz, false);
                                    break;
                            }
                        }
                        else
                        { }
                        paras.Add(p);
                        hashes.Add((MetaName)hash);
                    }
                    else
                    { }//todo?
                }
                Entries = paras.ToArray();
                NameHashes = hashes.ToArray();
            }
            else
            {

                var paras = new List<grcInstanceData.Entry>();
                for (int i = 0; i < entryCount; i++)
                {
                    var p = new grcInstanceData.Entry();
                    p.Read(reader);
                    paras.Add(p);
                }

                int offset = 0;
                for (int i = 0; i < entryCount; i++)
                {
                    var p = paras[i];

                    // read reference data
                    switch (p.Count)
                    {
                        case 0:
                            offset += 0;
                            p.Data = reader.ReadBlockAt<TextureBase>(p.DataPointer);
                            break;
                        case 1:
                            offset += 16;
                            p.Data = reader.ReadStructAt<Vector4>((long)p.DataPointer);
                            break;
                        default:
                            offset += 16 * p.Count;
                            p.Data = reader.ReadStructsAt<Vector4>(p.DataPointer, p.Count, false);
                            break;
                    }
                }


                reader.Position += offset; //Vector4 data gets embedded here... but why pointers in params also???

                var hashes = new List<MetaName>();
                for (int i = 0; i < entryCount; i++)
                {
                    hashes.Add((MetaName)reader.ReadUInt32());
                }

                Entries = paras.ToArray();
                NameHashes = hashes.ToArray();


            }

        }
        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            if (writer.IsGen9)
            {
                GameFileCache.EnsureShadersGen9ConversionData();
                GameFileCache.ShadersGen9ConversionData.TryGetValue((Owner ?? throw new InvalidOperationException("Shader parameters have no owner.")).BasisHashCode, out var dc);
                var paramap = dc?.ParamsMapLegacyToGen9;

                if (G9_ParamInfos == null) G9_ParamInfos = Owner?.G9_ParamInfos ?? throw new InvalidOperationException("Gen9 shader parameter information is missing.");
                var multi = (int)G9_ParamInfos.BufferCopyMultiplier;
                var mult = (uint)multi;
                var bcnt = G9_ParamInfos.NumBuffers;
                var tcnt = G9_ParamInfos.NumTextures;
                var ucnt = G9_ParamInfos.NumUnknowns;
                var spos = FilePosition;//this position should definitely be assigned by this point
                var bsizes = G9_BufferSizes;
                var boffs = new uint[bsizes.Length];
                var ptrslen = bcnt * 8 * multi;
                var bptrs = new ulong[bcnt * mult];
                var bptr = (ulong)(spos + ptrslen);
                for (int i = 0; i < mult; i++)
                {
                    for (int j = 0; j < bcnt; j++)
                    {
                        bptrs[(i * bcnt) + j] = bptr;
                        bptr += bsizes[j];
                    }
                }
                var boff = 0u;
                for (int i = 0; i < bsizes.Length; i++)
                {
                    boffs[i] = boff;
                    boff += bsizes[i];
                }
                if (G9_BufferPtrs != null)
                { }
                G9_BufferPtrs = bptrs;

                writer.WriteUlongs(bptrs);


                var buf0len = (int)(G9_BuffersDataSize / mult);
                var buf0 = new byte[buf0len];
                var texptrs = new ulong[tcnt * mult];
                if ((Entries != null) && (paramap != null))
                {
                    var exmap = new Dictionary<uint, grcInstanceData.Entry>();
                    var excnt = Math.Min(Entries.Length, NameHashes.Length);
                    for (int i = 0; i < excnt; i++)
                    {
                        var exhash = (uint)NameHashes[i];
                        if (paramap.TryGetValue(exhash, out var g9hash) == false)
                        {
                            g9hash = exhash;
                        }
                        if (g9hash != 0)
                        {
                            exmap[g9hash] = Entries[i];
                        }
                        else
                        { }
                    }
                    void writeStruct<T>(int o, T val) where T : struct
                    {
                        int size = Marshal.SizeOf(typeof(T));
                        IntPtr ptr = Marshal.AllocHGlobal(size);
                        Marshal.StructureToPtr(val, ptr, true);
                        Marshal.Copy(ptr, buf0, o, size);
                        Marshal.FreeHGlobal(ptr);
                    }
                    void writeStructs<T>(int o, T[] val) where T : struct
                    {
                        if (val == null) return;
                        int size = Marshal.SizeOf(typeof(T));
                        foreach (var v in val)
                        {
                            writeStruct(o, v);
                            o += size;
                        }

                    }
                    foreach (var info in G9_ParamInfos.Params)
                    {
                        exmap.TryGetValue(info.Name, out var exparam);
                        if (info.Type == ShaderParamTypeG9.Texture)
                        {
                            var btex = exparam?.Data as TextureBase;
                            texptrs[info.TextureIndex] = (ulong)(btex?.FilePosition ?? 0);
                        }
                        else if (info.Type == ShaderParamTypeG9.CBuffer)
                        {
                            var data = exparam?.Data;
                            uint fcnt = info.ParamLength / 4u;
                            uint arrsiz = info.ParamLength / 16u;
                            var cbi = info.CBufferIndex;
                            var baseoff = ((boffs != null) && (boffs.Length > cbi)) ? boffs[cbi] : 0;
                            var off = (int)(baseoff + info.ParamOffset);
                            var v = (data is Vector4) ? (Vector4)data : Vector4.Zero;
                            switch (fcnt)
                            {
                                case 0: break;
                                case 1: writeStruct(off, v.X); break;
                                case 2: writeStruct(off, new Vector2(v.X, v.Y)); break;
                                case 3: writeStruct(off, new Vector3(v.X, v.Y, v.Z)); break;
                                case 4: writeStruct(off, v); break;
                                default:
                                    if (arrsiz == 0)
                                    { }
                                    writeStructs(off, data as Vector4[] ?? []);
                                    break;
                            }
                        }

                    }
                }
                for (int i = 0; i < mult; i++)
                {
                    writer.Write(buf0);
                }

                if (G9_TexturePtrs != null)
                { }
                G9_TexturePtrs = texptrs;
                if (G9_TexturePtrs != null)
                {
                    writer.WriteUlongs(G9_TexturePtrs);
                }

                if (G9_UnknownData != null)
                { }
                G9_UnknownData = new ulong[ucnt * mult];
                if (G9_UnknownData != null)
                {
                    writer.WriteUlongs(G9_UnknownData);
                }

                if (G9_Samplers != null)
                {
                    writer.Write(G9_Samplers);
                }


            }
            else
            {
                if (NameHashes.Length != Entries.Length)
                {
                    throw new InvalidOperationException("Instance data entries and name hashes must have the same count.");
                }

                // update pointers...
                for (int i = 0; i < Entries.Length; i++)
                {
                    var param = Entries[i];
                    if (param.Count == 0)
                    {
                        param.DataPointer = (ulong)((param.Data as TextureBase)?.FilePosition ?? 0);
                    }
                    else
                    {
                        var block = (i < FloatDataBlocks?.Length) ? FloatDataBlocks[i] : null;
                        if (block != null)
                        {
                            param.DataPointer = (ulong)block.FilePosition;
                        }
                        else
                        {
                            param.DataPointer = 0;//shouldn't happen!
                        }
                    }
                }



                // write parameter infos
                foreach (var f in Entries)
                {
                    f.Write(writer);
                }

                // write vector data
                for (int i = 0; i < Entries.Length; i++)
                {
                    var param = Entries[i];
                    if (param.Count != 0)
                    {
                        var block = (i < FloatDataBlocks?.Length) ? FloatDataBlocks[i] : null;
                        if (block != null)
                        {
                            writer.WriteBlock(block);
                        }
                        else
                        { } //shouldn't happen!
                    }
                }

                // write hashes
                foreach (var h in NameHashes)
                {
                    writer.Write((uint)h);
                }


                var usedSize = SpuSize + NameHashes.Length * 4;
                writer.Write(new byte[BlockLength - usedSize]);

            }
        }
        public void WriteXml(StringBuilder sb, int indent)
        {
            var cind = indent + 1;
            for (int i = 0; i < Entries.Length; i++)
            {
                var param = Entries[i];
                var name = (ShaderParamNames)NameHashes[i];
                var typestr = "";
                if (param.Count == 0) typestr = "Texture";
                else if (param.Count == 1) typestr = "Vector";
                else if (param.Count > 1) typestr = "Array";
                var otstr = "Item name=\"" + name.ToString() + "\" type=\"" + typestr + "\"";

                if (param.Count == 0)
                {
                    if (param.Data is TextureBase tex)
                    {
                        string texName = tex.Name;
                        if (texName != null)
                        {
                            if (texName.StartsWith("pack:/", StringComparison.OrdinalIgnoreCase))
                            {
                                texName = texName.Substring(6);
                            }
                            if (texName.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
                            {
                                texName = texName.Substring(0, texName.Length - 4);
                            }
                        }
                        YdrXml.OpenTag(sb, indent, otstr);
                        YdrXml.StringTag(sb, cind, "Name", YdrXml.XmlEscape(texName));
                        YdrXml.CloseTag(sb, indent, "Item");
                    }
                    else
                    {
                        YdrXml.SelfClosingTag(sb, indent, otstr);
                    }
                }
                else if (param.Count == 1)
                {
                    if (param.Data is Vector4 vec)
                    {
                        YdrXml.SelfClosingTag(sb, indent, otstr + " " + FloatUtil.GetVector4XmlString(vec));
                    }
                    else
                    {
                        YdrXml.SelfClosingTag(sb, indent, otstr);
                    }
                }
                else
                {
                    if (param.Data is Vector4[] arr)
                    {
                        YdrXml.OpenTag(sb, indent, otstr);
                        foreach (var vec in arr)
                        {
                            YdrXml.SelfClosingTag(sb, cind, "Value " + FloatUtil.GetVector4XmlString(vec));
                        }
                        YdrXml.CloseTag(sb, indent, "Item");
                    }
                    else
                    {
                        YdrXml.SelfClosingTag(sb, indent, otstr);
                    }
                }
            }
        }
        public void ReadXml(XmlNode node)
        {
            var plist = new List<grcInstanceData.Entry>();
            var hlist = new List<MetaName>();
            var pnodes = node.SelectNodes("Item")?.Cast<XmlNode>().ToArray() ?? [];
            foreach (XmlNode pnode in pnodes)
            {
                var p = new grcInstanceData.Entry();
                var h = (MetaName)(uint)XmlMeta.GetHash(Xml.GetStringAttribute(pnode, "name")?.ToLowerInvariant());
                var type = Xml.GetStringAttribute(pnode, "type");
                if (type == "Texture")
                {
                    p.Count = 0;
                    if (pnode.SelectSingleNode("Name") != null)
                    {
                        var tex = new TextureBase();
                        tex.ReadXml(pnode, string.Empty);//embedded textures will get replaced in grcInstanceData ReadXML
                        tex.Unknown_32h = 2;
                        p.Data = tex;
                    }
                }
                else if (type == "Vector")
                {
                    p.Count = 1;
                    float fx = Xml.GetFloatAttribute(pnode, "x");
                    float fy = Xml.GetFloatAttribute(pnode, "y");
                    float fz = Xml.GetFloatAttribute(pnode, "z");
                    float fw = Xml.GetFloatAttribute(pnode, "w");
                    p.Data = new Vector4(fx, fy, fz, fw);
                }
                else if (type == "Array")
                {
                    var vecs = new List<Vector4>();
                    var inodes = pnode.SelectNodes("Value")?.Cast<XmlNode>().ToArray() ?? [];
                    foreach (XmlNode inode in inodes)
                    {
                        float fx = Xml.GetFloatAttribute(inode, "x");
                        float fy = Xml.GetFloatAttribute(inode, "y");
                        float fz = Xml.GetFloatAttribute(inode, "z");
                        float fw = Xml.GetFloatAttribute(inode, "w");
                        vecs.Add(new Vector4(fx, fy, fz, fw));
                    }
                    p.Data = vecs.ToArray();
                    p.Count = (byte)vecs.Count;
                }
                plist.Add(p);
                hlist.Add(h);
            }

            Entries = plist.ToArray();
            NameHashes = hlist.ToArray();
            for (int i = 0; i < Entries.Length; i++)
            {
                var param = Entries[i];
                if (param.Count == 0)
                {
                    param.Register = (byte)(i + 2);
                }
            }
            var offset = 160;
            for (int i = Entries.Length - 1; i >= 0; i--)
            {
                var param = Entries[i];
                if (param.Count != 0)
                {
                    param.Register = (byte)offset;
                    offset += param.Count;
                }
            }

        }




        public override IResourceBlock[] GetReferences()
        {
            var list = new List<IResourceBlock>();
            list.AddRange(base.GetReferences());

            foreach (var x in Entries)
            {
                if (x.Count == 0)
                {
                    if (x.Data is TextureBase texture) list.Add(texture);
                }
            }

            return list.ToArray();
        }

        public override Tuple<long, IResourceBlock>[] GetParts()
        {
            var list = new List<Tuple<long, IResourceBlock>>();
            list.AddRange(base.GetParts());

            long offset = Entries.Length * 16;

            var blist = new List<ResourceSystemStructBlock<Vector4>?>();
            foreach (var x in Entries)
            {
                if (x.Count != 0)
                {
                    var vecs = x.Data as Vector4[];
                    if (vecs == null)
                    {
                        vecs = x.Data is Vector4 vector ? [vector] : throw new InvalidOperationException("Shader vector parameter has no vector data.");
                    }
                    var block = new ResourceSystemStructBlock<Vector4>(vecs);
                    list.Add(new Tuple<long, IResourceBlock>(offset, block));
                    blist.Add(block);
                }
                else
                {
                    blist.Add(null);
                }
                offset += 16 * x.Count;
            }
            FloatDataBlocks = blist.ToArray();

            return list.ToArray();
        }
    }

    [TypeConverter(typeof(ExpandableObjectConverter))] public class ShaderParamInfosG9 : ResourceSystemBlock
    {
        public override long BlockLength => 8 + NumParams * 8;

        public byte NumBuffers { get; set; }
        public byte NumTextures { get; set; }
        public byte NumUnknowns { get; set; }
        public byte NumSamplers { get; set; }
        public byte NumParams { get; set; }
        public byte Unknown0 { get; set; }
        public byte Unknown1 { get; set; }
        public byte BufferCopyMultiplier { get; set; } = 0xc;//12  Gen9 constant-buffer copy count
        public ShaderParamInfoG9[] Params { get; set; } = [];

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            NumBuffers = reader.ReadByte();
            NumTextures = reader.ReadByte();
            NumUnknowns = reader.ReadByte();
            NumSamplers = reader.ReadByte();
            NumParams = reader.ReadByte();
            Unknown0 = reader.ReadByte();
            Unknown1 = reader.ReadByte();
            BufferCopyMultiplier = reader.ReadByte();
            Params = reader.ReadStructs<ShaderParamInfoG9>(NumParams);

            if (Unknown0 != 0)
            { }
            if (Unknown1 != 0)
            { }
            if (BufferCopyMultiplier != 0xc)
            { }
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            writer.Write(NumBuffers);
            writer.Write(NumTextures);
            writer.Write(NumUnknowns);
            writer.Write(NumSamplers);
            writer.Write(NumParams);
            writer.Write(Unknown0);
            writer.Write(Unknown1);
            writer.Write(BufferCopyMultiplier);
            writer.WriteStructs(Params);
        }

    }
    [TypeConverter(typeof(ExpandableObjectConverter))] public struct ShaderParamInfoG9
    {
        public MetaHash Name { get; set; }
        public uint Data { get; set; }

        public ShaderParamTypeG9 Type { get => (ShaderParamTypeG9)(Data & 0x3); set { Data = (Data & 0xFFFFFFF8) + (((uint)value) & 0x3); } }
        public byte TextureIndex { get => (byte)((Data >> 2) & 0xFF); set { Data = (Data & 0xFFFFFC03) + (((uint)value & 0xFF) << 2); } }
        public byte SamplerIndex { get => (byte)((Data >> 2) & 0xFF); set { Data = (Data & 0xFFFFFC03) + (((uint)value & 0xFF) << 2); } }
        public byte CBufferIndex { get => (byte)((Data >> 2) & 0x3F); set { Data = (Data & 0xFFFFFF03) + (((uint)value & 0x3F) << 2); } }
        public ushort ParamOffset { get => (ushort)((Data >> 8) & 0xFFF); set { Data = (Data & 0xFFF000FF) + (((uint)value & 0xFFF) << 8); } }
        public ushort ParamLength { get => (ushort)((Data >> 20) & 0xFFF); set { Data = (Data & 0x000FFFFF) + (((uint)value & 0xFFF) << 20); } }

        public override string ToString()
        {
            return $"{Name}: {Type}, {TextureIndex}, {ParamOffset}, {ParamLength}";
        }
    }
    public enum ShaderParamTypeG9 : byte
    {
        Texture = 0,
        Unknown = 1,
        Sampler = 2,
        CBuffer = 3,
    }



    [TypeConverter(typeof(ExpandableObjectConverter))] public class crSkeletonData : ResourceSystemBlock
    {
        public override long BlockLength
        {
            get { return 104; }
        }

        // structure data
        public uint VFT { get; set; } = 1080114336;
        public ulong FirstNodePointer { get; set; }
        public ulong BoneIdTablePointer { get; set; }
        public ushort BoneIdTableSlots { get; set; }
        public ushort BoneIdTableUsed { get; set; }
        public byte BoneIdTableAllowReCompute { get; set; }
        public ulong BonesPointer { get; set; }
        public ulong CumulativeInverseTransformsPointer { get; set; }
        public ulong DefaultTransformsPointer { get; set; }
        public ulong ParentIndicesPointer { get; set; }
        public ulong ChildParentIndicesPointer { get; set; }
        public ulong PropertiesPointer { get; set; }
        public uint Signature { get; set; }
        public uint SignatureNonChiral { get; set; }
        public uint SignatureComprehensive { get; set; }
        public ushort RefCount { get; set; } = 1;
        public ushort NumBones { get; set; }
        public ushort NumChildParents { get; set; }

        // reference data
        public ResourcePointerArray64<atMapEntry>? BoneIdTable { get; set; }
        public crBoneDataArrayBlock? Bones { get; set; }

        public Matrix[] CumulativeInverseTransforms { get; set; } = [];
        public Matrix[] DefaultTransforms { get; set; } = [];
        public short[] ParentIndices { get; set; } = [];
        public ushort[] ChildParentIndices { get; set; } = [];//mapping child->parent indices, first child index, then parent

        private ResourceSystemStructBlock<Matrix>? CumulativeInverseTransformsBlock = null;//for saving only
        private ResourceSystemStructBlock<Matrix>? DefaultTransformsBlock = null;
        private ResourceSystemStructBlock<short>? ParentIndicesBlock = null;
        private ResourceSystemStructBlock<ushort>? ChildParentIndicesBlock = null;


        public Dictionary<ushort, crBoneData> BonesMap { get; set; } = new();//for convienience finding bones by tag
        public crBoneData[] BonesSorted { get; set; } = []; //sometimes bones aren't in parent>child order in the files! (eg player chars)


        public Matrix3_s[] BoneTransforms = []; //for rendering


        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            // read structure data
            this.VFT = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            this.FirstNodePointer = reader.ReadUInt64();
            this.BoneIdTablePointer = reader.ReadUInt64();
            this.BoneIdTableSlots = reader.ReadUInt16();
            this.BoneIdTableUsed = reader.ReadUInt16();
            _ = reader.ReadBytes(3);
            this.BoneIdTableAllowReCompute = reader.ReadByte();
            this.BonesPointer = reader.ReadUInt64();
            this.CumulativeInverseTransformsPointer = reader.ReadUInt64();
            this.DefaultTransformsPointer = reader.ReadUInt64();
            this.ParentIndicesPointer = reader.ReadUInt64();
            this.ChildParentIndicesPointer = reader.ReadUInt64();
            this.PropertiesPointer = reader.ReadUInt64();
            this.Signature = reader.ReadUInt32();
            this.SignatureNonChiral = reader.ReadUInt32();
            this.SignatureComprehensive = reader.ReadUInt32();
            this.RefCount = reader.ReadUInt16();
            this.NumBones = reader.ReadUInt16();
            this.NumChildParents = reader.ReadUInt16();
            _ = reader.ReadBytes(6);

            // read reference data
            this.BoneIdTable = reader.ReadBlockAt<ResourcePointerArray64<atMapEntry>>(this.BoneIdTablePointer, this.BoneIdTableSlots);
            this.Bones = reader.ReadBlockAt<crBoneDataArrayBlock>((this.BonesPointer != 0) ? (BonesPointer - 16) : 0, (uint)this.NumBones);
            this.CumulativeInverseTransforms = reader.ReadStructsAt<Matrix>(this.CumulativeInverseTransformsPointer, this.NumBones) ?? [];
            this.DefaultTransforms = reader.ReadStructsAt<Matrix>(this.DefaultTransformsPointer, this.NumBones) ?? [];
            this.ParentIndices = reader.ReadShortsAt(this.ParentIndicesPointer, this.NumBones) ?? [];
            this.ChildParentIndices = reader.ReadUshortsAt(this.ChildParentIndicesPointer, this.NumChildParents) ?? [];


            AssignBoneParents();
            BuildBonesMap();
        }
        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            // update structure data
            this.BoneIdTablePointer = (ulong)(this.BoneIdTable != null ? this.BoneIdTable.FilePosition : 0);
            this.BoneIdTableSlots = (ushort)(this.BoneIdTable != null ? this.BoneIdTable.Count : 0);
            this.BonesPointer = (ulong)(this.Bones != null ? this.Bones.FilePosition+16 : 0);
            this.CumulativeInverseTransformsPointer = (ulong)(this.CumulativeInverseTransformsBlock != null ? this.CumulativeInverseTransformsBlock.FilePosition : 0);
            this.DefaultTransformsPointer = (ulong)(this.DefaultTransformsBlock != null ? this.DefaultTransformsBlock.FilePosition : 0);
            this.ParentIndicesPointer = (ulong)(this.ParentIndicesBlock != null ? this.ParentIndicesBlock.FilePosition : 0);
            this.ChildParentIndicesPointer = (ulong)(this.ChildParentIndicesBlock != null ? this.ChildParentIndicesBlock.FilePosition : 0);
            this.NumBones = (ushort)(this.Bones?.Items != null ? this.Bones.Items.Length : 0);
            this.NumChildParents = (ushort)(this.ChildParentIndicesBlock != null ? this.ChildParentIndicesBlock.ItemCount : 0);
            this.BoneIdTableUsed = Math.Min(NumBones, BoneIdTableSlots);


            // write structure data
            writer.Write(this.VFT);
            writer.Write(1u);
            writer.Write(this.FirstNodePointer);
            writer.Write(this.BoneIdTablePointer);
            writer.Write(this.BoneIdTableSlots);
            writer.Write(this.BoneIdTableUsed);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write(this.BoneIdTableAllowReCompute);
            writer.Write(this.BonesPointer);
            writer.Write(this.CumulativeInverseTransformsPointer);
            writer.Write(this.DefaultTransformsPointer);
            writer.Write(this.ParentIndicesPointer);
            writer.Write(this.ChildParentIndicesPointer);
            writer.Write(this.PropertiesPointer);
            writer.Write(this.Signature);
            writer.Write(this.SignatureNonChiral);
            writer.Write(this.SignatureComprehensive);
            writer.Write(this.RefCount);
            writer.Write(this.NumBones);
            writer.Write(this.NumChildParents);
            writer.Write(new byte[6]);
        }
        public void WriteXml(StringBuilder sb, int indent)
        {
            YdrXml.ValueTag(sb, indent, "Unknown1C", ((uint)BoneIdTableAllowReCompute << 24).ToString());
            YdrXml.ValueTag(sb, indent, "Unknown50", Signature.ToString());
            YdrXml.ValueTag(sb, indent, "Unknown54", SignatureNonChiral.ToString());
            YdrXml.ValueTag(sb, indent, "Unknown58", SignatureComprehensive.ToString());

            if (Bones?.Items != null)
            {
                YdrXml.WriteItemArray(sb, Bones.Items, indent, "Bones");
            }

        }
        public void ReadXml(XmlNode node)
        {
            BoneIdTableAllowReCompute = (byte)(Xml.GetChildUIntAttribute(node, "Unknown1C", "value") >> 24);
            Signature = Xml.GetChildUIntAttribute(node, "Unknown50", "value");
            SignatureNonChiral = Xml.GetChildUIntAttribute(node, "Unknown54", "value");
            SignatureComprehensive = Xml.GetChildUIntAttribute(node, "Unknown58", "value");

            var bones = XmlMeta.ReadItemArray<crBoneData>(node, "Bones");
            if (bones != null)
            {
                Bones = new crBoneDataArrayBlock();
                Bones.Items = bones;
            }

            BuildIndices();
            BuildBoneIdTable();
            AssignBoneParents();
            BuildTransformations();
            BuildBonesMap();
        }

        public override IResourceBlock[] GetReferences()
        {
            BuildTransformations();

            var list = new List<IResourceBlock>();
            if (BoneIdTable != null) list.Add(BoneIdTable);
            if (Bones != null) list.Add(Bones);
            if (CumulativeInverseTransforms != null)
            {
                CumulativeInverseTransformsBlock = new ResourceSystemStructBlock<Matrix>(CumulativeInverseTransforms);
                list.Add(CumulativeInverseTransformsBlock);
            }
            if (DefaultTransforms != null)
            {
                DefaultTransformsBlock = new ResourceSystemStructBlock<Matrix>(DefaultTransforms);
                list.Add(DefaultTransformsBlock);
            }
            if (ParentIndices != null)
            {
                ParentIndicesBlock = new ResourceSystemStructBlock<short>(ParentIndices);
                list.Add(ParentIndicesBlock);
            }
            if (ChildParentIndices != null)
            {
                ChildParentIndicesBlock = new ResourceSystemStructBlock<ushort>(ChildParentIndices);
                list.Add(ChildParentIndicesBlock);
            }
            return list.ToArray();
        }






        public void AssignBoneParents()
        {
            if ((Bones?.Items != null) && (ParentIndices != null))
            {
                var maxcnt = Math.Min(Bones.Items.Length, ParentIndices.Length);
                for (int i = 0; i < maxcnt; i++)
                {
                    var bone = Bones.Items[i];
                    var pind = ParentIndices[i];
                    if ((pind >= 0) && (pind < Bones.Items.Length))
                    {
                        bone.Parent = Bones.Items[pind];
                    }
                }
            }
        }

        public void BuildBonesMap()
        {
            BonesMap = new Dictionary<ushort, crBoneData>();
            if (Bones?.Items != null)
            {
                var bonesSorted = new List<crBoneData>();
                for (int i = 0; i < Bones.Items.Length; i++)
                {
                    var bone = Bones.Items[i];
                    BonesMap[bone.BoneId] = bone;
                    bonesSorted.Add(bone);

                    bone.UpdateAnimTransform();
                    bone.AbsTransform = bone.AnimTransform;
                    bone.BindTransformInv = (i < CumulativeInverseTransforms.Length) ? CumulativeInverseTransforms[i] : Matrix.Invert(bone.AnimTransform);
                    bone.BindTransformInv.M44 = 1.0f;
                    bone.UpdateSkinTransform();
                    bone.TransformUnk = (i < DefaultTransforms.Length) ? DefaultTransforms[i].Column4 : Vector4.Zero;//still dont know what this is
                }
                bonesSorted.Sort((a, b) => a.Index.CompareTo(b.Index));
                BonesSorted = bonesSorted.ToArray();
            }
        }

        public void BuildIndices()
        {
            var parents = new List<short>();
            var childs = new List<ushort>();
            if (Bones?.Items != null)
            {

                //crazy breadth-wise limited to 4 algorithm for generating the ChildIndices

                var tbones = Bones.Items.ToList();
                var rootbones = tbones.Where(b => (b.ParentIndex < 0)).ToList();
                for (int i = 0; i < tbones.Count; i++)
                {
                    var bone = Bones.Items[i];
                    var pind = bone.ParentIndex;
                    parents.Add(pind);
                }

                List<crBoneData> getChildren(crBoneData b)
                {
                    var r = new List<crBoneData>();
                    if (b == null) return r;
                    for (int i = 0; i < tbones.Count; i++)
                    {
                        var tb = tbones[i];
                        if (tb.ParentIndex == b.Index)
                        {
                            r.Add(tb);
                        }
                    }
                    return r;
                }
                List<crBoneData> getAllChildren(List<crBoneData> bones)
                {
                    var l = new List<crBoneData>();
                    foreach (var b in bones)
                    {
                        var children = getChildren(b);
                        l.AddRange(children);
                    }
                    return l;
                }
                
                var layers = new List<List<crBoneData>>();
                var layer = getAllChildren(rootbones);
                while (layer.Count > 0)
                {
                    var numbones = Math.Min(layer.Count, 4);
                    var inslayer = layer.GetRange(0, numbones);
                    var extlayer = getAllChildren(inslayer);
                    layers.Add(inslayer);
                    layer.RemoveRange(0, numbones);
                    layer.InsertRange(0, extlayer);
                }



                foreach (var l in layers)
                {
                    crBoneData? lastbone = null;
                    foreach (var b in l)
                    {
                        childs.Add((ushort)b.Index);
                        childs.Add((ushort)b.ParentIndex);
                        lastbone = b;
                    }
                    if (lastbone != null)
                    {
                        var npad = 8 - (childs.Count % 8);
                        if (npad < 8)
                        {
                            for (int i = 0; i < npad; i += 2)
                            {
                                childs.Add((ushort)lastbone.Index);
                                childs.Add((ushort)lastbone.ParentIndex);
                            }
                        }
                    }
                }





                //////just testing
                //var numchilds = ChildIndices?.Length ?? 0;
                //int diffstart = -1;
                //int diffend = -1;
                //int ndiff = Math.Abs(numchilds - childs.Count);
                //int maxchilds = Math.Min(numchilds, childs.Count);
                //for (int i = 0; i < maxchilds; i++)
                //{
                //    var oc = ChildIndices[i];
                //    var nc = childs[i];
                //    if (nc != oc)
                //    {
                //        if (diffstart < 0) diffstart = i;
                //        diffend = i;
                //        ndiff++; 
                //    }
                //}
                //if (ndiff > 0)
                //{
                //    var difffrac = ((float)ndiff) / ((float)numchilds);
                //}
                //if (numchilds != childs.Count)
                //{ }




                //var numbones = Bones.Items.Length;
                //var numchilds = ChildIndices?.Length ?? 0;
                //for (int i = 0; i < numchilds; i += 2)
                //{
                //    var bind = ChildIndices[i];
                //    var pind = ChildIndices[i + 1];
                //    if (bind > numbones)
                //    { continue; }//shouldn't happen
                //    var bone = Bones.Items[bind];
                //    if (bone == null)
                //    { continue; }//shouldn't happen
                //    if (pind != bone.ParentIndex)
                //    { }//shouldn't happen?
                //}




            }

            ParentIndices = parents.ToArray();
            ChildParentIndices = childs.ToArray();

        }

        public void BuildBoneIdTable()
        {
            var entries = new List<atMapEntry>();
            if (Bones?.Items != null)
            {
                for (int i = 0; i < Bones.Items.Length; i++)
                {
                    var bone = Bones.Items[i];
                    entries.Add(new atMapEntry
                    {
                        Key = bone.BoneId,
                        Data = i
                    });
                }
            }

            if (entries.Count < 2)
            {
                if (BoneIdTable != null)
                { }
                BoneIdTable = null;
                return;
            }

            var numbuckets = GetNumHashBuckets(entries.Count);

            var buckets = new List<atMapEntry>[numbuckets];
            foreach (var entry in entries)
            {
                var b = entry.Key % numbuckets;
                var bucket = buckets[b];
                if (bucket == null)
                {
                    bucket = new List<atMapEntry>();
                    buckets[b] = bucket;
                }
                bucket.Add(entry);
            }

            var tableEntries = new atMapEntry[buckets.Length];
            for (int bucketIndex = 0; bucketIndex < buckets.Length; bucketIndex++)
            {
                var b = buckets[bucketIndex];
                if (b is { Count: > 0 })
                {
                    b.Reverse();
                    tableEntries[bucketIndex] = b[0];
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


            BoneIdTable = new ResourcePointerArray64<atMapEntry>();
            BoneIdTable.data_items = tableEntries;


        }

        public void BuildTransformations()
        {
            var transforms = new List<Matrix>();
            var transformsinv = new List<Matrix>();
            if (Bones?.Items != null)
            {
                foreach (var bone in Bones.Items)
                {
                    var pos = bone.DefaultTranslation;
                    var ori = bone.DefaultRotation;
                    var sca = bone.DefaultScale;
                    var m = Matrix.AffineTransformation(1.0f, ori, pos);//(local transform)
                    m.ScaleVector *= sca;
                    m.Column4 = bone.TransformUnk;// new Vector4(0, 4, -3, 0);//???

                    var pbone = bone.Parent;
                    while (pbone != null)
                    {
                        pos = pbone.DefaultRotation.Multiply(pos /** pbone.DefaultScale*/) + pbone.DefaultTranslation;
                        ori = pbone.DefaultRotation * ori;
                        pbone = pbone.Parent;
                    }
                    var m2 = Matrix.AffineTransformation(1.0f, ori, pos);//(global transform)
                    var mi = Matrix.Invert(m2);
                    mi.Column4 = Vector4.Zero;

                    transforms.Add(m);
                    transformsinv.Add(mi);
                }
            }

            //if (Transformations != null) //just testing! - all ok
            //{
            //    if (Transformations.Length != transforms.Count)
            //    { }
            //    else
            //    {
            //        for (int i = 0; i < Transformations.Length; i++)
            //        {
            //            if (Transformations[i].Column1 != transforms[i].Column1)
            //            { }
            //            if (Transformations[i].Column2 != transforms[i].Column2)
            //            { }
            //            if (Transformations[i].Column3 != transforms[i].Column3)
            //            { }
            //            if (Transformations[i].Column4 != transforms[i].Column4)
            //            { }
            //        }
            //    }
            //    if (TransformationsInverted.Length != transformsinv.Count)
            //    { }
            //    else
            //    {
            //        for (int i = 0; i < TransformationsInverted.Length; i++)
            //        {
            //            if (TransformationsInverted[i].Column4 != transformsinv[i].Column4)
            //            { }
            //        }
            //    }
            //}

            DefaultTransforms = transforms.ToArray();
            CumulativeInverseTransforms = transformsinv.ToArray();

        }


        public static uint GetNumHashBuckets(int nHashes)
        {
            //todo: refactor with same in Clip.cs?
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



        public void ResetBoneTransforms()
        {
            if (Bones?.Items == null) return;
            foreach (var bone in Bones.Items)
            {
                bone.ResetAnimTransform();
            }
            UpdateBoneTransforms();
        }
        public void UpdateBoneTransforms()
        {
            if (Bones?.Items == null) return;
            if ((BoneTransforms == null) || (BoneTransforms.Length != Bones.Items.Length))
            {
                BoneTransforms = new Matrix3_s[Bones.Items.Length];
            }
            for (int i = 0; i < Bones.Items.Length; i++)
            {
                var bone = Bones.Items[i];
                Matrix b = bone.SkinTransform;
                Matrix3_s bt = new();
                bt.Row1 = b.Column1;
                bt.Row2 = b.Column2;
                bt.Row3 = b.Column3;
                BoneTransforms[i] = bt;
            }
        }






        /// <summary>Uses a complete actor pose while retaining this drawable's bone palette order.</summary>
        public void BindAnimationSkeleton(crSkeletonData actor)
        {
            if (ReferenceEquals(this, actor) || Bones?.Items == null || actor.Bones?.Items == null) return;
            // A component palette can omit ancestors (for example a hand omits the arm).
            // Animation lookup and hierarchy evaluation must both use the complete actor skeleton.
            for (int i = 0; i < Bones.Items.Length; i++)
                if (actor.BonesMap.TryGetValue(Bones.Items[i].BoneId, out var bone))
                    Bones.Items[i] = bone;
            BonesMap = actor.BonesMap;
            BonesSorted = actor.BonesSorted;
        }

        public crSkeletonData Clone()
        {
            var skel = new crSkeletonData();

            skel.FirstNodePointer = FirstNodePointer;
            skel.BoneIdTableSlots = BoneIdTableSlots;
            skel.BoneIdTableUsed = BoneIdTableUsed;
            skel.BoneIdTableAllowReCompute = BoneIdTableAllowReCompute;
            skel.PropertiesPointer = PropertiesPointer;
            skel.Signature = Signature;
            skel.SignatureNonChiral = SignatureNonChiral;
            skel.SignatureComprehensive = SignatureComprehensive;
            skel.RefCount = RefCount;
            skel.NumBones = NumBones;
            skel.NumChildParents = NumChildParents;

            if (BoneIdTable != null)
            {
                skel.BoneIdTable = new ResourcePointerArray64<atMapEntry>();
                if (BoneIdTable.data_items != null)
                {
                    skel.BoneIdTable.data_items = new atMapEntry[BoneIdTable.data_items.Length];
                    for (int i = 0; i < BoneIdTable.data_items.Length; i++)
                    {
                        var obt = BoneIdTable.data_items[i];
                        var nbt = new atMapEntry();
                        skel.BoneIdTable.data_items[i] = nbt;
                        while (obt != null)
                        {
                            nbt.Key = obt.Key;
                            nbt.Data = obt.Data;
                            obt = obt.Next;
                            if (obt != null)
                            {
                                var nxt = new atMapEntry();
                                nbt.Next = nxt;
                                nbt = nxt;
                            }
                        }
                    }
                }
            }
            if (Bones != null)
            {
                skel.Bones = new crBoneDataArrayBlock();
                if (Bones.Items != null)
                {
                    skel.Bones.Items = new crBoneData[Bones.Items.Length];
                    for (int i = 0; i < Bones.Items.Length; i++)
                    {
                        var ob = Bones.Items[i];
                        var nb = new crBoneData();
                        nb.DefaultRotation = ob.DefaultRotation;
                        nb.DefaultTranslation = ob.DefaultTranslation;
                        nb.DefaultScale = ob.DefaultScale;
                        nb.NextIndex = ob.NextIndex;
                        nb.ParentIndex = ob.ParentIndex;
                        nb.Dofs = ob.Dofs;
                        nb.Index = ob.Index;
                        nb.BoneId = ob.BoneId;
                        nb.MirrorIndex = ob.MirrorIndex;
                        nb.Name = ob.Name;
                        nb.AnimRotation = ob.AnimRotation;
                        nb.AnimTranslation = ob.AnimTranslation;
                        nb.AnimScale = ob.AnimScale;
                        nb.AnimTransform = ob.AnimTransform;
                        nb.BindTransformInv = ob.BindTransformInv;
                        nb.SkinTransform = ob.SkinTransform;
                        skel.Bones.Items[i] = nb;
                    }
                }
            }

            skel.CumulativeInverseTransforms = (Matrix[])CumulativeInverseTransforms.Clone();
            skel.DefaultTransforms = (Matrix[])DefaultTransforms.Clone();
            skel.ParentIndices = (short[])ParentIndices.Clone();
            skel.ChildParentIndices = (ushort[])ChildParentIndices.Clone();

            skel.AssignBoneParents();
            skel.BuildBonesMap();

            return skel;
        }



    }

    [TypeConverter(typeof(ExpandableObjectConverter))] public class crBoneDataArrayBlock : ResourceSystemBlock
    {
        public override long BlockLength
        {
            get
            {
                long length = 16;
                if (Items != null)
                {
                    foreach (var b in Items)
                    {
                        length += b.BlockLength;
                    }
                }
                return length;
            }
        }

        public crBoneData[] Items { get; set; } = [];


        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            _ = reader.ReadUInt32();
            _ = reader.ReadBytes(12);

            var count = (uint)parameters[0];
            var items = new crBoneData[count];
            for (uint i = 0; i < count; i++)
            {
                items[i] = reader.ReadRequiredBlock<crBoneData>();
            }
            Items = items;
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            writer.Write((uint)Items.Length);
            writer.Write(new byte[12]);

            foreach (var b in Items)
            {
                b.Write(writer);
            }
        }

        public override Tuple<long, IResourceBlock>[] GetParts()
        {
            var list = new List<Tuple<long, IResourceBlock>>();
            long length = 16;
            if (Items != null)
            {
                foreach (var b in Items)
                {
                    list.Add(new Tuple<long, IResourceBlock>(length, b));
                    length += b.BlockLength;
                }
            }
            return list.ToArray();
        }
    }

    [Flags] public enum crBoneDataDofs : ushort
    {
        NONE = 0,
        ROTATE_X = 1 << 0,
        ROTATE_Y = 1 << 1,
        ROTATE_Z = 1 << 2,
        HAS_ROTATE_LIMITS = 1 << 3,
        TRANSLATE_X = 1 << 4,
        TRANSLATE_Y = 1 << 5,
        TRANSLATE_Z = 1 << 6,
        HAS_TRANSLATE_LIMITS = 1 << 7,
        SCALE_X = 1 << 8,
        SCALE_Y = 1 << 9,
        SCALE_Z = 1 << 10,
        HAS_SCALE_LIMITS = 1 << 11,
        HAS_CHILD = 1 << 12,
        IS_SKINNED = 1 << 13,
        ROTATION = ROTATE_X | ROTATE_Y | ROTATE_Z,
        TRANSLATION = TRANSLATE_X | TRANSLATE_Y | TRANSLATE_Z,
        SCALE = SCALE_X | SCALE_Y | SCALE_Z,
    }


    [TypeConverter(typeof(ExpandableObjectConverter))] public class crBoneData : ResourceSystemBlock, IMetaXmlItem
    {
        public override long BlockLength
        {
            get { return 80; }
        }

        // structure data
        public Quaternion DefaultRotation { get; set; } = Quaternion.Identity;
        public Vector3 DefaultTranslation { get; set; } = Vector3.Zero;
        public Vector3 DefaultScale { get; set; } = Vector3.One;
        public short NextIndex { get; set; } = -1;
        public short ParentIndex { get; set; } = -1;
        public ulong NamePointer { get; set; }
        public crBoneDataDofs Dofs { get; set; }
        public ushort Index { get; set; }
        public ushort BoneId { get; set; }
        public ushort MirrorIndex { get; set; } = ushort.MaxValue;

        // reference data
        public string Name { get; set; } = string.Empty;

        public crBoneData? Parent { get; set; }

        private string_r? NameBlock = null;


        //used by CW for animating skeletons.
        public Quaternion AnimRotation;//relative to parent
        public Vector3 AnimTranslation;//relative to parent
        public Vector3 AnimScale;
        public Matrix AnimTransform;//absolute world transform, animated
        public Matrix BindTransformInv;//inverse of bind pose transform
        public Matrix SkinTransform;//transform to use for skin meshes
        public Matrix AbsTransform;//original absolute transform from loaded file, calculated from bones hierarchy
        public Vector4 TransformUnk { get; set; } //unknown value (column 4) from skeleton's transform array, used for IO purposes

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            // read structure data
            this.DefaultRotation = new Quaternion(reader.ReadVector4());
            this.DefaultTranslation = reader.ReadVector3();
            _ = reader.ReadSingle();
            this.DefaultScale = reader.ReadVector3();
            _ = reader.ReadSingle();
            this.NextIndex = reader.ReadInt16();
            this.ParentIndex = reader.ReadInt16();
            _ = reader.ReadUInt32();
            this.NamePointer = reader.ReadUInt64();
            this.Dofs = (crBoneDataDofs)reader.ReadUInt16();
            this.Index = reader.ReadUInt16();
            this.BoneId = reader.ReadUInt16();
            this.MirrorIndex = reader.ReadUInt16();
            _ = reader.ReadUInt64();

            // read reference data
            this.Name = reader.ReadStringAt(this.NamePointer) ?? string.Empty;

            AnimRotation = DefaultRotation;
            AnimTranslation = DefaultTranslation;
            AnimScale = DefaultScale;

        }
        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            // update structure data
            this.NamePointer = (ulong)(this.NameBlock != null ? this.NameBlock.FilePosition : 0);

            // write structure data
            writer.Write(this.DefaultRotation.ToVector4());
            writer.Write(this.DefaultTranslation);
            writer.Write(0.0f);
            writer.Write(this.DefaultScale);
            writer.Write(1.0f);
            writer.Write(this.NextIndex);
            writer.Write(this.ParentIndex);
            writer.Write(0u);
            writer.Write(this.NamePointer);
            writer.Write((ushort)this.Dofs);
            writer.Write(this.Index);
            writer.Write(this.BoneId);
            writer.Write(this.MirrorIndex);
            writer.Write(0ul);
        }
        public void WriteXml(StringBuilder sb, int indent)
        {
            YdrXml.StringTag(sb, indent, "Name", Name);
            YdrXml.ValueTag(sb, indent, "Tag", BoneId.ToString());
            YdrXml.ValueTag(sb, indent, "Index", Index.ToString());
            YdrXml.ValueTag(sb, indent, "MirrorIndex", MirrorIndex.ToString());
            YdrXml.ValueTag(sb, indent, "ParentIndex", ParentIndex.ToString());
            YdrXml.ValueTag(sb, indent, "SiblingIndex", NextIndex.ToString());
            YdrXml.StringTag(sb, indent, "Flags", Dofs.ToString());
            YdrXml.SelfClosingTag(sb, indent, "Translation " + FloatUtil.GetVector3XmlString(DefaultTranslation));
            YdrXml.SelfClosingTag(sb, indent, "Rotation " + FloatUtil.GetVector4XmlString(DefaultRotation.ToVector4()));
            YdrXml.SelfClosingTag(sb, indent, "Scale " + FloatUtil.GetVector3XmlString(DefaultScale));
            YdrXml.SelfClosingTag(sb, indent, "TransformUnk " + FloatUtil.GetVector4XmlString(TransformUnk));
        }
        public void ReadXml(XmlNode node)
        {
            Name = Xml.GetChildInnerText(node, "Name") ?? string.Empty;
            BoneId = (ushort)Xml.GetChildUIntAttribute(node, "Tag", "value");
            Index = (ushort)Xml.GetChildUIntAttribute(node, "Index", "value");
            MirrorIndex = node.SelectSingleNode("MirrorIndex") != null
                ? (ushort)Xml.GetChildUIntAttribute(node, "MirrorIndex", "value")
                : Index;
            ParentIndex = (short)Xml.GetChildIntAttribute(node, "ParentIndex", "value");
            NextIndex = (short)Xml.GetChildIntAttribute(node, "SiblingIndex", "value");
            Dofs = Xml.GetChildEnumInnerText<crBoneDataDofs>(node, "Flags");
            DefaultTranslation = Xml.GetChildVector3Attributes(node, "Translation");
            DefaultRotation = Xml.GetChildVector4Attributes(node, "Rotation").ToQuaternion();
            DefaultScale = Xml.GetChildVector3Attributes(node, "Scale");
            TransformUnk = Xml.GetChildVector4Attributes(node, "TransformUnk");
        }

        public override IResourceBlock[] GetReferences()
        {
            var list = new List<IResourceBlock>();
            if (Name != null)
            {
                NameBlock = (string_r)Name;
                list.Add(NameBlock);
            }
            return list.ToArray();
        }

        public override string ToString()
        {
            return BoneId.ToString() + ": " + Name;
        }


        public void UpdateAnimTransform()
        {
            AnimTransform = Matrix.AffineTransformation(1.0f, AnimRotation, AnimTranslation);
            AnimTransform.ScaleVector *= AnimScale;
            if (Parent != null)
            {
                AnimTransform = AnimTransform * Parent.AnimTransform;
            }
        }
        public void UpdateSkinTransform()
        {
            SkinTransform = BindTransformInv * AnimTransform;
        }

        public void ResetAnimTransform()
        {
            AnimRotation = DefaultRotation;
            AnimTranslation = DefaultTranslation;
            AnimScale = DefaultScale;
            UpdateAnimTransform();
            UpdateSkinTransform();
        }

        public static uint ElfHash_Uppercased(string str)
        {
            uint hash = 0;
            uint x = 0;
            uint i = 0;

            for (i = 0; i < str.Length; i++)
            {
                var c = ((byte)str[(int)i]);
                if ((byte)(c - 'a') <= 25u) // to uppercase
                    c -= 32;

                hash = (hash << 4) + c;

                if ((x = hash & 0xF0000000) != 0)
                {
                    hash ^= (x >> 24);
                }

                hash &= ~x;
            }

            return hash;
        }
        public static ushort CalculateBoneHash(string boneName)
        {
            return (ushort)(ElfHash_Uppercased(boneName) % 0xFE8F + 0x170);
        }

    }

    [TypeConverter(typeof(ExpandableObjectConverter))] public class crJointData : ResourceSystemBlock
    {
        public override long BlockLength
        {
            get { return 64; }
        }

        // structure data
        public uint VFT { get; set; } = 1080130656;
        public ulong FirstNodePointer { get; set; }
        public ulong RotationLimitsPointer { get; set; }
        public ulong TranslationLimitsPointer { get; set; }
        public ulong ScaleLimitsPointer { get; set; }
        public ulong NamePointer { get; set; }
        public ushort NumRotationLimits { get; set; }
        public ushort NumTranslationLimits { get; set; }
        public ushort NumScaleLimits { get; set; }
        public ushort RefCount { get; set; } = 1;

        // reference data
        public crJointRotationLimit[] RotationLimits { get; set; } = [];
        public crJointTranslationLimit[] TranslationLimits { get; set; } = [];
        public crJointScaleLimit[] ScaleLimits { get; set; } = [];
        public string Name { get; set; } = string.Empty;

        private ResourceSystemStructBlock<crJointRotationLimit>? RotationLimitsBlock = null; //for saving only
        private ResourceSystemStructBlock<crJointTranslationLimit>? TranslationLimitsBlock = null;
        private ResourceSystemStructBlock<crJointScaleLimit>? ScaleLimitsBlock = null;
        private string_r? NameBlock = null;


        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            // read structure data
            this.VFT = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            this.FirstNodePointer = reader.ReadUInt64();
            this.RotationLimitsPointer = reader.ReadUInt64();
            this.TranslationLimitsPointer = reader.ReadUInt64();
            this.ScaleLimitsPointer = reader.ReadUInt64();
            this.NamePointer = reader.ReadUInt64();
            this.NumRotationLimits = reader.ReadUInt16();
            this.NumTranslationLimits = reader.ReadUInt16();
            this.NumScaleLimits = reader.ReadUInt16();
            this.RefCount = reader.ReadUInt16();
            _ = reader.ReadUInt64();

            // read reference data
            this.RotationLimits = reader.ReadStructsAt<crJointRotationLimit>(this.RotationLimitsPointer, this.NumRotationLimits) ?? [];
            this.TranslationLimits = reader.ReadStructsAt<crJointTranslationLimit>(this.TranslationLimitsPointer, this.NumTranslationLimits) ?? [];
            this.ScaleLimits = reader.ReadStructsAt<crJointScaleLimit>(this.ScaleLimitsPointer, this.NumScaleLimits) ?? [];
            this.Name = reader.ReadStringAt(this.NamePointer) ?? string.Empty;
        }
        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            // update structure data
            this.RotationLimitsPointer = (ulong)(this.RotationLimitsBlock != null ? this.RotationLimitsBlock.FilePosition : 0);
            this.TranslationLimitsPointer = (ulong)(this.TranslationLimitsBlock != null ? this.TranslationLimitsBlock.FilePosition : 0);
            this.ScaleLimitsPointer = (ulong)(this.ScaleLimitsBlock != null ? this.ScaleLimitsBlock.FilePosition : 0);
            this.NamePointer = (ulong)(this.NameBlock != null ? this.NameBlock.FilePosition : 0);
            this.NumRotationLimits = (ushort)(this.RotationLimitsBlock != null ? this.RotationLimitsBlock.ItemCount : 0);
            this.NumTranslationLimits = (ushort)(this.TranslationLimitsBlock != null ? this.TranslationLimitsBlock.ItemCount : 0);
            this.NumScaleLimits = (ushort)(this.ScaleLimitsBlock != null ? this.ScaleLimitsBlock.ItemCount : 0);


            // write structure data
            writer.Write(this.VFT);
            writer.Write(1u);
            writer.Write(this.FirstNodePointer);
            writer.Write(this.RotationLimitsPointer);
            writer.Write(this.TranslationLimitsPointer);
            writer.Write(this.ScaleLimitsPointer);
            writer.Write(this.NamePointer);
            writer.Write(this.NumRotationLimits);
            writer.Write(this.NumTranslationLimits);
            writer.Write(this.NumScaleLimits);
            writer.Write(this.RefCount);
            writer.Write(0ul);
        }
        public void WriteXml(StringBuilder sb, int indent)
        {
            if (RotationLimits != null)
            {
                YdrXml.WriteItemArray(sb, RotationLimits, indent, "RotationLimits");
            }
            if (TranslationLimits != null)
            {
                YdrXml.WriteItemArray(sb, TranslationLimits, indent, "TranslationLimits");
            }
            if (ScaleLimits != null)
            {
                YdrXml.WriteItemArray(sb, ScaleLimits, indent, "ScaleLimits");
            }
            YdrXml.StringTag(sb, indent, "Name", Name);
        }
        public void ReadXml(XmlNode node)
        {
            RotationLimits = XmlMeta.ReadItemArray<crJointRotationLimit>(node, "RotationLimits");
            TranslationLimits = XmlMeta.ReadItemArray<crJointTranslationLimit>(node, "TranslationLimits");
            ScaleLimits = XmlMeta.ReadItemArray<crJointScaleLimit>(node, "ScaleLimits");
            Name = Xml.GetChildInnerText(node, "Name") ?? string.Empty;
        }

        public override IResourceBlock[] GetReferences()
        {
            var list = new List<IResourceBlock>();
            if (RotationLimits is { Length: > 0 })
            {
                RotationLimitsBlock = new ResourceSystemStructBlock<crJointRotationLimit>(RotationLimits);
                list.Add(RotationLimitsBlock);
            }
            else RotationLimitsBlock = null;
            if (TranslationLimits is { Length: > 0 })
            {
                TranslationLimitsBlock = new ResourceSystemStructBlock<crJointTranslationLimit>(TranslationLimits);
                list.Add(TranslationLimitsBlock);
            }
            else TranslationLimitsBlock = null;
            if (ScaleLimits is { Length: > 0 })
            {
                ScaleLimitsBlock = new ResourceSystemStructBlock<crJointScaleLimit>(ScaleLimits);
                list.Add(ScaleLimitsBlock);
            }
            else ScaleLimitsBlock = null;
            if (!string.IsNullOrEmpty(Name))
            {
                NameBlock = (string_r)Name;
                list.Add(NameBlock);
            }
            else NameBlock = null;
            return list.ToArray();
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 192)]
    [TypeConverter(typeof(ExpandableObjectConverter))] public struct crJointRotationLimit : IMetaXmlItem
    {
        public enum JointDOFs
        {
            JOINT_1_DOF = 1,
            JOINT_3_DOF = 3,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct JointControlPoint
        {
            public float MaxSwing;
            public float MinTwist;
            public float MaxTwist;

            public JointControlPoint(float maxSwing, float minTwist, float maxTwist)
            {
                MaxSwing = maxSwing;
                MinTwist = minTwist;
                MaxTwist = maxTwist;
            }

            public readonly Vector3 ToVector3() => new(MaxSwing, MinTwist, MaxTwist);
        }

        [FieldOffset(0x08)] public int BoneId;
        [FieldOffset(0x0C)] public int NumControlPoints;
        [FieldOffset(0x10)] public JointDOFs JointDofs;
        [FieldOffset(0x20)] public Quaternion ZeroRotation;
        [FieldOffset(0x30)] public Vector3 ZeroRotationEulers;
        [FieldOffset(0x40)] public Vector3 TwistAxis;
        [FieldOffset(0x50)] public float TwistLimitMin;
        [FieldOffset(0x54)] public float TwistLimitMax;
        [FieldOffset(0x58)] public float SoftLimitScale;
        [FieldOffset(0x5C)] public JointControlPoint ControlPoint0;
        [FieldOffset(0x68)] public JointControlPoint ControlPoint1;
        [FieldOffset(0x74)] public JointControlPoint ControlPoint2;
        [FieldOffset(0x80)] public JointControlPoint ControlPoint3;
        [FieldOffset(0x8C)] public JointControlPoint ControlPoint4;
        [FieldOffset(0x98)] public JointControlPoint ControlPoint5;
        [FieldOffset(0xA4)] public JointControlPoint ControlPoint6;
        [FieldOffset(0xB0)] public JointControlPoint ControlPoint7;
        [FieldOffset(0xBC)] private byte useTwistLimits;
        [FieldOffset(0xBD)] private byte useEulerAngles;
        [FieldOffset(0xBE)] private byte usePerControlTwistLimits;

        public bool UseTwistLimits
        {
            readonly get => useTwistLimits != 0;
            set => useTwistLimits = (byte)(value ? 1 : 0);
        }
        public bool UseEulerAngles
        {
            readonly get => useEulerAngles != 0;
            set => useEulerAngles = (byte)(value ? 1 : 0);
        }
        public bool UsePerControlTwistLimits
        {
            readonly get => usePerControlTwistLimits != 0;
            set => usePerControlTwistLimits = (byte)(value ? 1 : 0);
        }

        public crJointRotationLimit()
        {
            this = default;
            var pi = (float)Math.PI;
            BoneId = -1;
            NumControlPoints = 1;
            JointDofs = JointDOFs.JOINT_3_DOF;
            ZeroRotation = Quaternion.Identity;
            TwistAxis = Vector3.UnitX;
            TwistLimitMin = -pi;
            TwistLimitMax = pi;
            SoftLimitScale = 1.0f;
            var controlPoint = new JointControlPoint(pi, -pi, pi);
            ControlPoint0 = controlPoint;
            ControlPoint1 = controlPoint;
            ControlPoint2 = controlPoint;
            ControlPoint3 = controlPoint;
            ControlPoint4 = controlPoint;
            ControlPoint5 = controlPoint;
            ControlPoint6 = controlPoint;
            ControlPoint7 = controlPoint;
        }

        public void WriteXml(StringBuilder sb, int indent)
        {
            YdrXml.ValueTag(sb, indent, "BoneId", BoneId.ToString());
            YdrXml.SelfClosingTag(sb, indent, "Min " + FloatUtil.GetVector3XmlString(ControlPoint0.ToVector3()));
            YdrXml.SelfClosingTag(sb, indent, "Max " + FloatUtil.GetVector3XmlString(ControlPoint1.ToVector3()));
        }
        public void ReadXml(XmlNode node)
        {
            this = new crJointRotationLimit();
            BoneId = Xml.GetChildIntAttribute(node, "BoneId", "value");
            var upperNode = node.SelectSingleNode("UnknownA");
            if (upperNode != null)
            {
                var upper = Xml.GetChildUIntAttribute(node, "UnknownA", "value");
                BoneId = unchecked((int)((uint)(ushort)BoneId | (upper << 16)));
            }
            UseEulerAngles = true;
            var min = Xml.GetChildVector3Attributes(node, "Min");
            var max = Xml.GetChildVector3Attributes(node, "Max");
            ControlPoint0 = new JointControlPoint(min.X, min.Y, min.Z);
            ControlPoint1 = new JointControlPoint(max.X, max.Y, max.Z);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    [TypeConverter(typeof(ExpandableObjectConverter))] public struct crJointTranslationLimit : IMetaXmlItem
    {
        [FieldOffset(0x08)] public int BoneId;
        [FieldOffset(0x20)] public Vector3 LimitMin;
        [FieldOffset(0x30)] public Vector3 LimitMax;

        public crJointTranslationLimit()
        {
            this = default;
            BoneId = -1;
        }

        public void WriteXml(StringBuilder sb, int indent)
        {
            YdrXml.ValueTag(sb, indent, "BoneId", BoneId.ToString());
            YdrXml.SelfClosingTag(sb, indent, "Min " + FloatUtil.GetVector3XmlString(LimitMin));
            YdrXml.SelfClosingTag(sb, indent, "Max " + FloatUtil.GetVector3XmlString(LimitMax));
        }
        public void ReadXml(XmlNode node)
        {
            this = new crJointTranslationLimit();
            BoneId = Xml.GetChildIntAttribute(node, "BoneId", "value");
            LimitMin = Xml.GetChildVector3Attributes(node, "Min");
            LimitMax = Xml.GetChildVector3Attributes(node, "Max");
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    [TypeConverter(typeof(ExpandableObjectConverter))] public struct crJointScaleLimit : IMetaXmlItem
    {
        [FieldOffset(0x08)] public int BoneId;
        [FieldOffset(0x20)] public Vector3 LimitMin;
        [FieldOffset(0x30)] public Vector3 LimitMax;

        public crJointScaleLimit()
        {
            this = default;
            BoneId = -1;
        }

        public void WriteXml(StringBuilder sb, int indent)
        {
            YdrXml.ValueTag(sb, indent, "BoneId", BoneId.ToString());
            YdrXml.SelfClosingTag(sb, indent, "Min " + FloatUtil.GetVector3XmlString(LimitMin));
            YdrXml.SelfClosingTag(sb, indent, "Max " + FloatUtil.GetVector3XmlString(LimitMax));
        }
        public void ReadXml(XmlNode node)
        {
            this = new crJointScaleLimit();
            BoneId = Xml.GetChildIntAttribute(node, "BoneId", "value");
            LimitMin = Xml.GetChildVector3Attributes(node, "Min");
            LimitMax = Xml.GetChildVector3Attributes(node, "Max");
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    [TypeConverter(typeof(ExpandableObjectConverter))] public struct rmcLod
    {
        [FieldOffset(0x00)] public ulong ModelsPointer;
        [FieldOffset(0x08)] public ushort ModelsCount;
        [FieldOffset(0x0A)] public ushort ModelsCapacity;
    }

    [TypeConverter(typeof(ExpandableObjectConverter))] public class rmcLodContainer : ResourceSystemBlock
    {
        public override long BlockLength
        {
            get
            {
                long len = 0;
                len += ListLength(High, len);
                len += ListLength(Med, len);
                len += ListLength(Low, len);
                len += ListLength(VLow, len);
                return len;
            }
        }

        public rmcDrawable? Owner;

        public grmModel[] High { get; set; } = [];
        public grmModel[] Med { get; set; } = [];
        public grmModel[] Low { get; set; } = [];
        public grmModel[] VLow { get; set; } = [];

        public rmcLod HighLod { get; set; }
        public rmcLod MedLod { get; set; }
        public rmcLod LowLod { get; set; }
        public rmcLod VLowLod { get; set; }

        public ulong[] HighModelPointers { get; set; } = [];
        public ulong[] MedModelPointers { get; set; } = [];
        public ulong[] LowModelPointers { get; set; } = [];
        public ulong[] VLowModelPointers { get; set; } = [];


        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            Owner = parameters[0] as rmcDrawable;
            var highPointer = (Owner?.LodHighPointer ?? 0);
            var medPointer = (Owner?.LodMedPointer ?? 0);
            var lowPointer = (Owner?.LodLowPointer ?? 0);
            var vlowPointer = (Owner?.LodVlowPointer ?? 0);

            if (highPointer != 0)
            {
                HighLod = reader.ReadStructAt<rmcLod>((long)highPointer);
                HighModelPointers = reader.ReadUlongsAt(HighLod.ModelsPointer, HighLod.ModelsCount, false) ?? [];
                High = reader.ReadBlocks<grmModel>(HighModelPointers) ?? [];
            }
            if (medPointer != 0)
            {
                MedLod = reader.ReadStructAt<rmcLod>((long)medPointer);
                MedModelPointers = reader.ReadUlongsAt(MedLod.ModelsPointer, MedLod.ModelsCount, false) ?? [];
                Med = reader.ReadBlocks<grmModel>(MedModelPointers) ?? [];
            }
            if (lowPointer != 0)
            {
                LowLod = reader.ReadStructAt<rmcLod>((long)lowPointer);
                LowModelPointers = reader.ReadUlongsAt(LowLod.ModelsPointer, LowLod.ModelsCount, false) ?? [];
                Low = reader.ReadBlocks<grmModel>(LowModelPointers) ?? [];
            }
            if (vlowPointer != 0)
            {
                VLowLod = reader.ReadStructAt<rmcLod>((long)vlowPointer);
                VLowModelPointers = reader.ReadUlongsAt(VLowLod.ModelsPointer, VLowLod.ModelsCount, false) ?? [];
                VLow = reader.ReadBlocks<grmModel>(VLowModelPointers) ?? [];
            }
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {

            rmcLod makeLod(ref long p, int c)
            {
                p += Pad(p);
                var h = new rmcLod { ModelsPointer = (ulong)(p + 16), ModelsCount = (ushort)c, ModelsCapacity = (ushort)c };
                p += HeaderLength(c);
                return h;
            }
            ulong[] makePointers(ref long p, grmModel[] a)
            {
                var ptrs = new ulong[a.Length];
                for (int i = 0; i < a.Length; i++)
                {
                    p += Pad(p);
                    ptrs[i] = (ulong)p;
                    p += a[i].BlockLength;
                }
                return ptrs;
            }
            void write(rmcLod lod, ulong[] pointers, grmModel[] models)
            {
                writer.WritePadding(16);
                writer.WriteStruct(lod);
                writer.WriteUlongs(pointers);
                for (int i = 0; i < models.Length; i++)
                {
                    writer.WritePadding(16);
                    writer.WriteBlock(models[i]);
                }
            }

            var ptr = writer.Position;
            if (HasModels(High))
            {
                HighLod = makeLod(ref ptr, High.Length);
                HighModelPointers = makePointers(ref ptr, High);
                write(HighLod, HighModelPointers, High);
            }
            if (HasModels(Med))
            {
                MedLod = makeLod(ref ptr, Med.Length);
                MedModelPointers = makePointers(ref ptr, Med);
                write(MedLod, MedModelPointers, Med);
            }
            if (HasModels(Low))
            {
                LowLod = makeLod(ref ptr, Low.Length);
                LowModelPointers = makePointers(ref ptr, Low);
                write(LowLod, LowModelPointers, Low);
            }
            if (HasModels(VLow))
            {
                VLowLod = makeLod(ref ptr, VLow.Length);
                VLowModelPointers = makePointers(ref ptr, VLow);
                write(VLowLod, VLowModelPointers, VLow);
            }

        }


        private long Pad(long o) => ((16 - (o % 16)) % 16);
        private long HeaderLength(int listlength) => 16 + ((listlength) * 8);
        private static bool HasModels(grmModel[]? list) => list is { Length: > 0 };
        private long ListLength(grmModel[]? list, long o)
        {
            if (!HasModels(list)) return 0;
            long l = 0;
            l += HeaderLength(list!.Length);
            foreach (var m in list) l += Pad(l) + m.BlockLength;
            return Pad(o) + l;
        }


        public override Tuple<long, IResourceBlock>[] GetParts()
        {
            var parts = new List<Tuple<long, IResourceBlock>>();
            parts.AddRange(base.GetParts());

            void addParts(ref long p, grmModel[]? a)
            {
                if (!HasModels(a)) return;
                p += Pad(p);
                p += HeaderLength(a!.Length);
                foreach (var m in a)
                {
                    p += Pad(p);
                    parts.Add(new Tuple<long, IResourceBlock>(p, m));
                    p += m.BlockLength;
                }
            }

            var ptr = (long)0;
            addParts(ref ptr, High);
            addParts(ref ptr, Med);
            addParts(ref ptr, Low);
            addParts(ref ptr, VLow);

            return parts.ToArray();
        }


        public long GetHighPointer()
        {
            return GetLodPointer(High);
        }
        public long GetMedPointer()
        {
            return GetLodPointer(Med, High);
        }
        public long GetLowPointer()
        {
            return GetLodPointer(Low, High, Med);
        }
        public long GetVLowPointer()
        {
            return GetLodPointer(VLow, High, Med, Low);
        }
        private long GetLodPointer(grmModel[] lod, params grmModel[][] precedingLods)
        {
            if (!HasModels(lod)) return 0;
            var p = FilePosition;
            foreach (var precedingLod in precedingLods)
            {
                p += ListLength(precedingLod, p);
            }
            return p + Pad(p);
        }
    }

    [Flags] public enum grmModelFlags : byte
    {
        NONE = 0,
        MODEL_RELATIVE = 1,
        RESOURCED = 2,
    }

    [TypeConverter(typeof(ExpandableObjectConverter))] public class grmModel : ResourceSystemBlock, IMetaXmlItem
    {
        public override long BlockLength
        {
            get 
            {
                var off = (long)48;
                var count = Geometries?.Length ?? 0;
                off += (count * 2); //ShaderIndices
                if (count == 1) off += 6;
                else off += ((16 - (off % 16)) % 16);
                off += (count * 8); //Geometries pointers
                off += ((16 - (off % 16)) % 16);
                off += (count + ((count > 1) ? 1 : 0)) * 32; //AABBs
                for (int i = 0; i < count; i++)
                {
                    var geom = (Geometries != null) ? Geometries[i] : null;
                    if (geom != null)
                    {
                        off += ((16 - (off % 16)) % 16);
                        off += geom.BlockLength; //Geometries
                    }
                }
                return off;
            }
        }

        // structure data
        public uint VFT { get; set; } = 1080101528;
        public ulong GeometriesPointer { get; set; }
        public ushort GeometriesCount { get; set; }
        public ushort GeometriesCapacity { get; set; }
        public ulong AABBsPointer { get; set; }
        public ulong ShaderIndexPointer { get; set; }
        public byte MatrixCount { get; set; }
        public grmModelFlags Flags { get; set; }
        public byte Type { get; set; }
        public byte MatrixIndex { get; set; }
        public byte Mask { get; set; } = 0xFF;
        public byte SkinFlagAndTessellatedGeometryCount { get; set; }
        public ushort Count { get; set; }
        public ushort[] ShaderIndices { get; set; } = [];
        public ulong[] GeometryPointers { get; set; } = [];
        public AABB_s[] AABBs { get; set; } = [];
        public grmGeometryQB[] Geometries { get; set; } = [];

        public bool SkinFlag
        {
            get => (SkinFlagAndTessellatedGeometryCount & 1) != 0;
            set => SkinFlagAndTessellatedGeometryCount = (byte)((SkinFlagAndTessellatedGeometryCount & ~1) | (value ? 1 : 0));
        }
        public byte TessellatedGeometryCount
        {
            get => (byte)(SkinFlagAndTessellatedGeometryCount >> 1);
            set => SkinFlagAndTessellatedGeometryCount = (byte)((SkinFlagAndTessellatedGeometryCount & 1) | (value << 1));
        }

        public long MemoryUsage
        {
            get
            {
                long val = 0;
                if (Geometries != null)
                {
                    foreach(var geom in Geometries)
                    {
                        if (geom == null) continue;
                        if (geom.VertexData != null)
                        {
                            val += geom.VertexData.MemoryUsage;
                        }
                        if (geom.IndexBuffer != null)
                        {
                            val += geom.IndexBuffer.MemoryUsage;
                        }
                        if (geom.VertexBuffer != null)
                        {
                            if ((geom.VertexBuffer.LockData != null) && (geom.VertexBuffer.LockData != geom.VertexData))
                            {
                                val += geom.VertexBuffer.LockData.MemoryUsage;
                            }
                            if ((geom.VertexBuffer.VertexData != null) && (geom.VertexBuffer.VertexData != geom.VertexData))
                            {
                                val += geom.VertexBuffer.VertexData.MemoryUsage;
                            }
                        }
                    }
                }
                if (AABBs != null)
                {
                    val += AABBs.Length * 32;
                }
                return val;
            }
        }

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            // read structure data
            this.VFT = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            this.GeometriesPointer = reader.ReadUInt64();
            this.GeometriesCount = reader.ReadUInt16();
            this.GeometriesCapacity = reader.ReadUInt16();
            _ = reader.ReadUInt32();
            this.AABBsPointer = reader.ReadUInt64();
            this.ShaderIndexPointer = reader.ReadUInt64();
            this.MatrixCount = reader.ReadByte();
            this.Flags = (grmModelFlags)reader.ReadByte();
            this.Type = reader.ReadByte();
            this.MatrixIndex = reader.ReadByte();
            this.Mask = reader.ReadByte();
            this.SkinFlagAndTessellatedGeometryCount = reader.ReadByte();
            this.Count = reader.ReadUInt16();

            this.ShaderIndices = reader.ReadUshortsAt(this.ShaderIndexPointer, this.Count, false) ?? [];
            this.GeometryPointers = reader.ReadUlongsAt(this.GeometriesPointer, this.GeometriesCount, false) ?? [];
            this.AABBs = reader.ReadStructsAt<AABB_s>(this.AABBsPointer, (uint)(this.Count > 1 ? this.Count + 1 : this.Count), false) ?? [];
            this.Geometries = reader.ReadBlocks<grmGeometryQB>(this.GeometryPointers) ?? [];

            if (Geometries != null)
            {
                for (int i = 0; i < Geometries.Length; i++)
                {
                    var geom = Geometries[i];
                    if (geom != null)
                    {
                        geom.ShaderID = (i < ShaderIndices.Length) ? ShaderIndices[i] : (ushort)0;
                        geom.AABB = (AABBs.Length > 0) ? ((AABBs.Length > 1) && ((i + 1) < AABBs.Length)) ? AABBs[i + 1] : AABBs[0] : new AABB_s();
                    }
                }
            }
        }
        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            // update structure data
            this.GeometriesCount = (ushort)this.Geometries.Length;
            this.GeometriesCapacity = this.GeometriesCount;
            this.Count = this.GeometriesCount;
            
            long pad(long o) => ((16 - (o % 16)) % 16);
            var off = writer.Position + 48;
            this.ShaderIndexPointer = (ulong)off;
            off += (Count * 2); //ShaderIndices
            if (Count == 1) off += 6;
            else off += pad(off);
            this.GeometriesPointer = (ulong)off;
            off += (GeometriesCount * 8); //Geometries pointers
            off += pad(off);
            this.AABBsPointer = (ulong)off;
            off += AABBs.Length * 32;
            this.GeometryPointers = new ulong[GeometriesCount];
            for (int i = 0; i < GeometriesCount; i++)
            {
                var geom = (Geometries != null) ? Geometries[i] : null;
                if (geom != null)
                {
                    off += pad(off);
                    this.GeometryPointers[i] = (ulong)off;
                    off += geom.BlockLength; //Geometries
                }
            }
            if (Count == 0)
            {
                GeometriesPointer = 0;
                AABBsPointer = 0;
                ShaderIndexPointer = 0;
            }



            // write structure data
            writer.Write(this.VFT);
            writer.Write(1u);
            writer.Write(this.GeometriesPointer);
            writer.Write(this.GeometriesCount);
            writer.Write(this.GeometriesCapacity);
            writer.Write(0u);
            writer.Write(this.AABBsPointer);
            writer.Write(this.ShaderIndexPointer);
            writer.Write(this.MatrixCount);
            writer.Write((byte)this.Flags);
            writer.Write(this.Type);
            writer.Write(this.MatrixIndex);
            writer.Write(this.Mask);
            writer.Write(this.SkinFlagAndTessellatedGeometryCount);
            writer.Write(this.Count);


            for (int i = 0; i < Count; i++)
            {
                writer.Write(ShaderIndices[i]);
            }
            if (Count == 1)
            {
                writer.Write(new byte[6]);
            }
            else
            {
                writer.WritePadding(16);
            }
            for (int i = 0; i < GeometriesCount; i++)
            {
                writer.Write(GeometryPointers[i]);
            }
            writer.WritePadding(16);
            for (int i = 0; i < AABBs.Length; i++)
            {
                writer.WriteStruct(AABBs[i]);
            }
            for (int i = 0; i < GeometriesCount; i++)
            {
                var geom = (Geometries != null) ? Geometries[i] : null;
                if (geom != null)
                {
                    writer.WritePadding(16);
                    writer.WriteBlock(geom);
                }
            }

        }
        public void WriteXml(StringBuilder sb, int indent)
        {
            YdrXml.ValueTag(sb, indent, "RenderMask", Mask.ToString());
            YdrXml.ValueTag(sb, indent, "Flags", SkinFlagAndTessellatedGeometryCount.ToString());
            YdrXml.ValueTag(sb, indent, "HasSkin", ((byte)Flags).ToString());
            YdrXml.ValueTag(sb, indent, "BoneIndex", MatrixIndex.ToString());
            YdrXml.ValueTag(sb, indent, "Unknown1", MatrixCount.ToString());

            if (Geometries != null)
            {
                YdrXml.WriteItemArray(sb, Geometries, indent, "Geometries");
            }

        }
        public void ReadXml(XmlNode node)
        {
            Mask = (byte)Xml.GetChildUIntAttribute(node, "RenderMask", "value");
            SkinFlagAndTessellatedGeometryCount = (byte)Xml.GetChildUIntAttribute(node, "Flags", "value");
            Flags = (grmModelFlags)Xml.GetChildUIntAttribute(node, "HasSkin", "value");
            MatrixIndex = (byte)Xml.GetChildUIntAttribute(node, "BoneIndex", "value");
            MatrixCount = (byte)Xml.GetChildUIntAttribute(node, "Unknown1", "value");

            var aabbs = new List<AABB_s>();
            var shids = new List<ushort>();
            var min = new Vector4(float.MaxValue);
            var max = new Vector4(float.MinValue);
            var geoms = XmlMeta.ReadItemArray<grmGeometryQB>(node, "Geometries");
            if (geoms != null)
            {
                Geometries = geoms;
                foreach (var geom in geoms)
                {
                    aabbs.Add(geom.AABB);
                    shids.Add(geom.ShaderID);
                    min = Vector4.Min(min, geom.AABB.Min);
                    max = Vector4.Max(max, geom.AABB.Max);
                }
                GeometriesCount = GeometriesCapacity = Count = (ushort)geoms.Length;
            }
            if (aabbs.Count > 1)
            {
                var outeraabb = new AABB_s() { Min = min, Max = max };
                aabbs.Insert(0, outeraabb);
            }

            AABBs = aabbs.ToArray();
            ShaderIndices = shids.ToArray();
        }


        public override Tuple<long, IResourceBlock>[] GetParts()
        {
            var parts = new List<Tuple<long, IResourceBlock>>();
            parts.AddRange(base.GetParts());

            var off = (long)48;
            var count = Geometries?.Length ?? 0;
            off += (count * 2); //ShaderIndices
            if (count == 1) off += 6;
            else off += ((16 - (off % 16)) % 16);
            off += (count * 8); //Geometries pointers
            off += ((16 - (off % 16)) % 16);
            off += (count + ((count > 1) ? 1 : 0)) * 32; //AABBs
            for (int i = 0; i < count; i++)
            {
                var geom = (Geometries != null) ? Geometries[i] : null;
                if (geom != null)
                {
                    off += ((16 - (off % 16)) % 16);
                    parts.Add(new Tuple<long, IResourceBlock>(off, geom));
                    off += geom.BlockLength; //Geometries
                }
            }

            return parts.ToArray();
        }

        public override string ToString()
        {
            var gc = Geometries?.Length ?? 0;
            uint totalPolys = 0;
            uint totalVerts = 0;
            if (Geometries != null)
            {
                for (int i = 0; i < Geometries.Length; i++)
                {
                    totalPolys += Geometries[i].PrimitiveCount;
                    totalVerts += Geometries[i].VertexCount;
                }
            }
            return "(" + gc.ToString() + " geometr" + (gc != 1 ? "ies" : "y") + ", " + totalPolys.ToString() + " polys, " + totalVerts.ToString() + " verts)";
        }

    }

    public enum grcDrawMode : byte
    {
        drawPoints,
        drawLines,
        drawLineStrip,
        drawTris,
        drawTriStrip,
        drawTriFan,
        drawQuads,
        drawRects,
        drawTrisAdj,
        drawModesTotal,
    }

    [TypeConverter(typeof(ExpandableObjectConverter))] public class grmGeometryQB : ResourceSystemBlock, IMetaXmlItem
    {
        public override long BlockLength
        {
            get 
            {
                long l = 152;
                if (MatrixPalette != null)
                {
                    if (MatrixPalette.Length > 4) l += 8;
                    l += MatrixPalette.Length * 2;
                }
                return l;
            }
        }

        // structure data
        public uint VFT { get; set; } = 1080133528;
        public ulong VertexDeclarationPointer { get; set; }
        public int Type { get; set; }
        public ulong[] VertexBufferPointers { get; set; } = new ulong[4];
        public ulong[] IndexBufferPointers { get; set; } = new ulong[4];
        public uint IndexCount { get; set; }
        public uint PrimitiveCount { get; set; }
        public ushort VertexCount { get; set; }
        public grcDrawMode PrimitiveType { get; set; } = grcDrawMode.drawTris;
        public byte DoubleBuffered { get; set; }
        public ulong MatrixPalettePointer { get; set; }
        public ushort Stride { get; set; }
        public ushort MatrixCount { get; set; }
        public ulong VertexDataPointer { get; set; }
        public ulong VertexDeclarationOffsetPointer { get; set; }
        public ulong OffsetBufferPointer { get; set; }
        public uint IndexOffset { get; set; }

        // reference data
        public grcVertexBuffer?[] VertexBuffers { get; set; } = new grcVertexBuffer?[4];
        public grcIndexBuffer?[] IndexBuffers { get; set; } = new grcIndexBuffer?[4];
        [Browsable(false)] public grcVertexBuffer? VertexBuffer { get => VertexBuffers[0]; set => VertexBuffers[0] = value; }
        [Browsable(false)] public grcIndexBuffer? IndexBuffer { get => IndexBuffers[0]; set => IndexBuffers[0] = value; }
        public grcVertexBuffer? OffsetBuffer { get; set; }
        public VertexData? VertexData { get; set; }
        public ushort[] MatrixPalette { get; set; } = [];
        public grcInstanceData? Shader { get; set; }//assigned by the owning rmcDrawable, using ShaderID
        public ushort ShaderID { get; set; }//read/written by parent model
        public AABB_s AABB { get; set; }//read/written by parent model


        public bool UpdateRenderableParameters = false; //used by model material editor...


        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            // read structure data
            this.VFT = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            this.VertexDeclarationPointer = reader.ReadUInt64();
            this.Type = reader.ReadInt32();
            _ = reader.ReadUInt32();
            this.VertexBufferPointers = [reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64()];
            this.IndexBufferPointers = [reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64()];
            this.IndexCount = reader.ReadUInt32();
            this.PrimitiveCount = reader.ReadUInt32();
            this.VertexCount = reader.ReadUInt16();
            this.PrimitiveType = (grcDrawMode)reader.ReadByte();
            this.DoubleBuffered = reader.ReadByte();
            _ = reader.ReadUInt32();
            this.MatrixPalettePointer = reader.ReadUInt64();
            this.Stride = reader.ReadUInt16();
            this.MatrixCount = reader.ReadUInt16();
            _ = reader.ReadUInt32();
            this.VertexDataPointer = reader.ReadUInt64();
            this.VertexDeclarationOffsetPointer = reader.ReadUInt64();
            this.OffsetBufferPointer = reader.ReadUInt64();
            this.IndexOffset = reader.ReadUInt32();
            _ = reader.ReadUInt32();

            // read reference data
            this.VertexBuffers = new grcVertexBuffer?[4];
            this.IndexBuffers = new grcIndexBuffer?[4];
            for (int i = 0; i < 4; i++)
            {
                this.VertexBuffers[i] = reader.ReadBlockAt<grcVertexBuffer>(this.VertexBufferPointers[i]);
                this.IndexBuffers[i] = reader.ReadBlockAt<grcIndexBuffer>(this.IndexBufferPointers[i]);
            }
            this.OffsetBuffer = reader.ReadBlockAt<grcVertexBuffer>(this.OffsetBufferPointer);
            this.MatrixPalette = reader.ReadUshortsAt(this.MatrixPalettePointer, this.MatrixCount, false) ?? [];

            if (this.VertexBuffer != null)
            {
                this.VertexData = this.VertexBuffer.VertexData ?? this.VertexBuffer.LockData;

                if (this.VertexCount == 0)
                {
                    this.VertexCount = (ushort)(this.VertexData?.VertexCount ?? 0);
                }
            }
        }
        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            // update structure data
            this.VertexBufferPointers = new ulong[4];
            this.IndexBufferPointers = new ulong[4];
            for (int i = 0; i < 4; i++)
            {
                this.VertexBufferPointers[i] = (ulong)((i < VertexBuffers.Length) ? VertexBuffers[i]?.FilePosition ?? 0 : 0);
                this.IndexBufferPointers[i] = (ulong)((i < IndexBuffers.Length) ? IndexBuffers[i]?.FilePosition ?? 0 : 0);
            }
            this.OffsetBufferPointer = (ulong)(this.OffsetBuffer?.FilePosition ?? 0);
            this.VertexDataPointer = (ulong)(this.VertexData != null ? this.VertexData.FilePosition : 0);
            this.VertexCount = (ushort)(this.VertexData?.VertexCount ?? 0);
            this.Stride = (ushort)(this.VertexBuffer?.Stride ?? 0);
            this.IndexCount = this.IndexBuffer?.IndexCount ?? 0;
            this.PrimitiveCount = (PrimitiveType == grcDrawMode.drawTris) ? this.IndexCount / 3 : this.PrimitiveCount;
            this.MatrixCount = (ushort)(MatrixPalette?.Length ?? 0);
            this.MatrixPalettePointer = (MatrixCount > 0) ? (ulong)(writer.Position + 152 + ((MatrixCount > 4) ? 8 : 0)) : 0;
            

            // write structure data
            writer.Write(this.VFT);
            writer.Write(1u);
            writer.Write(this.VertexDeclarationPointer);
            writer.Write(this.Type);
            writer.Write(0u);
            writer.WriteUlongs(this.VertexBufferPointers);
            writer.WriteUlongs(this.IndexBufferPointers);
            writer.Write(this.IndexCount);
            writer.Write(this.PrimitiveCount);
            writer.Write(this.VertexCount);
            writer.Write((byte)this.PrimitiveType);
            writer.Write(this.DoubleBuffered);
            writer.Write(0u);
            writer.Write(this.MatrixPalettePointer);
            writer.Write(this.Stride);
            writer.Write(this.MatrixCount);
            writer.Write(0u);
            writer.Write(this.VertexDataPointer);
            writer.Write(this.VertexDeclarationOffsetPointer);
            writer.Write(this.OffsetBufferPointer);
            writer.Write(this.IndexOffset);
            writer.Write(0u);

            if (MatrixPalette != null)
            {
                if (MatrixPalette.Length > 4)
                {
                    writer.Write((ulong)0);
                }
                for (int i = 0; i < MatrixPalette.Length; i++)
                {
                    writer.Write(MatrixPalette[i]);
                }
            }

        }
        public void WriteXml(StringBuilder sb, int indent)
        {
            YdrXml.ValueTag(sb, indent, "ShaderIndex", ShaderID.ToString());
            YdrXml.SelfClosingTag(sb, indent, "BoundingBoxMin " + FloatUtil.GetVector4XmlString(AABB.Min));
            YdrXml.SelfClosingTag(sb, indent, "BoundingBoxMax " + FloatUtil.GetVector4XmlString(AABB.Max));
            if (MatrixPalette != null)
            {
                var ids = new StringBuilder();
                foreach (var id in MatrixPalette)
                {
                    if (ids.Length > 0) ids.Append(", ");
                    ids.Append(id.ToString());
                }
                YdrXml.StringTag(sb, indent, "BoneIDs", ids.ToString());
            }
            if (VertexBuffer != null)
            {
                YdrXml.OpenTag(sb, indent, "VertexBuffer");
                VertexBuffer.WriteXml(sb, indent + 1);
                YdrXml.CloseTag(sb, indent, "VertexBuffer");
            }
            if (IndexBuffer != null)
            {
                YdrXml.OpenTag(sb, indent, "IndexBuffer");
                IndexBuffer.WriteXml(sb, indent + 1);
                YdrXml.CloseTag(sb, indent, "IndexBuffer");
            }
        }
        public void ReadXml(XmlNode node)
        {
            ShaderID = (ushort)Xml.GetChildUIntAttribute(node, "ShaderIndex", "value");
            var aabb = new AABB_s();
            aabb.Min = Xml.GetChildVector4Attributes(node, "BoundingBoxMin");
            aabb.Max = Xml.GetChildVector4Attributes(node, "BoundingBoxMax");
            AABB = aabb;
            var bnode = node.SelectSingleNode("BoneIDs");
            if (bnode != null)
            {
                var astr = bnode.InnerText;
                var arr = astr.Split(',');
                var blist = new List<ushort>();
                foreach (var bstr in arr)
                {
                    var tstr = bstr?.Trim();
                    if (string.IsNullOrEmpty(tstr)) continue;
                    if (ushort.TryParse(tstr, out ushort u))
                    {
                        blist.Add(u);
                    }
                }
                MatrixPalette = blist.ToArray();
            }
            var vnode = node.SelectSingleNode("VertexBuffer");
            if (vnode != null)
            {
                VertexBuffer = new grcVertexBuffer();
                VertexBuffer.ReadXml(vnode);
                VertexData = VertexBuffer.VertexData ?? VertexBuffer.LockData;
            }
            var inode = node.SelectSingleNode("IndexBuffer");
            if (inode != null)
            {
                IndexBuffer = new grcIndexBuffer();
                IndexBuffer.ReadXml(inode);
            }
        }

        public override IResourceBlock[] GetReferences()
        {
            var list = new List<IResourceBlock>();
            foreach (var vertexBuffer in VertexBuffers)
            {
                if ((vertexBuffer != null) && !list.Contains(vertexBuffer)) list.Add(vertexBuffer);
            }
            foreach (var indexBuffer in IndexBuffers)
            {
                if ((indexBuffer != null) && !list.Contains(indexBuffer)) list.Add(indexBuffer);
            }
            if ((OffsetBuffer != null) && !list.Contains(OffsetBuffer)) list.Add(OffsetBuffer);
            if (VertexData != null) list.Add(VertexData);
            return list.ToArray();
        }

        public override string ToString()
        {
            return PrimitiveCount.ToString() + " polys, " + VertexCount.ToString() + " verts, " + Shader?.ToString();
        }
    }

    [Flags] public enum grcVertexBufferFlags : byte
    {
        NONE = 0,
        DYNAMIC = 1,
        PREALLOCATED_MEMORY = 2,
        READ_WRITE = 4,
    }

    [TypeConverter(typeof(ExpandableObjectConverter))] public class grcVertexBuffer : ResourceSystemBlock
    {
        public override long BlockLength => 128;
        public override long BlockLength_Gen9 => 64;

        // structure data
        public uint VFT { get; set; } = 1080153080;
        public ushort Stride { get; set; }
        public byte Reserved0 { get; set; }
        public grcVertexBufferFlags Flags { get; set; }
        public ulong LockPointer { get; set; }
        public uint VertexCount { get; set; }
        public ulong VertexDataPointer { get; set; }
        public ulong VertexFormatPointer { get; set; }
        public ulong D3DBufferUnusedPointer { get; set; }

        // gen9 structure data
        public uint G9_BindFlags { get; set; }
        public ulong G9_ShaderResourceViewPointer { get; set; }
        public ShaderResourceViewG9? G9_SRV { get; set; }
        public VertexDeclarationG9? G9_VertexFormat { get; set; }


        // reference data
        public VertexData? LockData { get; set; }
        public VertexData? VertexData { get; set; }
        public grcFvf? VertexFormat { get; set; }


        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            // read structure data
            this.VFT = reader.ReadUInt32();
            _ = reader.ReadUInt32();

            if (reader.IsGen9)
            {
                VertexCount = reader.ReadUInt32();
                Stride = reader.ReadUInt16();
                _ = reader.ReadUInt16();
                G9_BindFlags = reader.ReadUInt32();
                _ = reader.ReadUInt32();
                VertexDataPointer = reader.ReadUInt64();
                _ = reader.ReadUInt64();
                _ = reader.ReadUInt64();
                G9_ShaderResourceViewPointer = reader.ReadUInt64();
                VertexFormatPointer = reader.ReadUInt64();

                G9_SRV = reader.ReadBlockAt<ShaderResourceViewG9>(G9_ShaderResourceViewPointer);
                G9_VertexFormat = reader.ReadBlockAt<VertexDeclarationG9>(VertexFormatPointer);

                var datalen = VertexCount * Stride;
                var vertexBytes = reader.ReadBytesAt(VertexDataPointer, datalen);
                InitVertexDataFromGen9Data(vertexBytes);

            }
            else
            {
                this.Stride = reader.ReadUInt16();
                this.Reserved0 = reader.ReadByte();
                this.Flags = (grcVertexBufferFlags)reader.ReadByte();
                _ = reader.ReadUInt32();
                this.LockPointer = reader.ReadUInt64();
                this.VertexCount = reader.ReadUInt32();
                _ = reader.ReadUInt32();
                this.VertexDataPointer = reader.ReadUInt64();
                _ = reader.ReadUInt64();
                this.VertexFormatPointer = reader.ReadUInt64();
                this.D3DBufferUnusedPointer = reader.ReadUInt64();
                _ = reader.ReadBytes(64);

                // read reference data
                this.VertexFormat = reader.ReadBlockAt<grcFvf>(
                    this.VertexFormatPointer
                );
                if ((LockPointer != 0) || (VertexDataPointer != 0))
                {
                    var vertexFormat = VertexFormat ?? throw new System.IO.InvalidDataException("The vertex declaration is missing.");
                    this.LockData = reader.ReadBlockAt<VertexData>(LockPointer, Stride, VertexCount, vertexFormat);
                    this.VertexData = reader.ReadBlockAt<VertexData>(VertexDataPointer, Stride, VertexCount, vertexFormat);
                }

            }

        }
        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            // update structure data
            var data = this.LockData ?? this.VertexData;
            this.VertexCount = (uint)(data?.VertexCount ?? 0);
            this.LockPointer = (ulong)(this.LockData?.FilePosition ?? 0);
            this.VertexDataPointer = (ulong)(this.VertexData?.FilePosition ?? 0);
            this.VertexFormatPointer = (ulong)(this.VertexFormat?.FilePosition ?? 0);

            // write structure data
            writer.Write(this.VFT);
            writer.Write(1u);

            if (writer.IsGen9)
            {
                G9_ShaderResourceViewPointer = (ulong)(G9_SRV?.FilePosition ?? 0);
                VertexFormatPointer = (ulong)(G9_VertexFormat?.FilePosition ?? 0);
                VertexDataPointer = (ulong)(data?.FilePosition ?? 0);

                if (G9_BindFlags == 0) G9_BindFlags = 0x00580409;
                //G9_BindFlags = //TODO?

                writer.Write(VertexCount);
                writer.Write(Stride);
                writer.Write((ushort)0);
                writer.Write(G9_BindFlags);
                writer.Write(0u);
                writer.Write(VertexDataPointer);
                writer.Write(0ul);
                writer.Write(0ul);
                writer.Write(G9_ShaderResourceViewPointer);
                writer.Write(VertexFormatPointer);

            }
            else
            {
                writer.Write(this.Stride);
                writer.Write(this.Reserved0);
                writer.Write((byte)this.Flags);
                writer.Write(0u);
                writer.Write(this.LockPointer);
                writer.Write(this.VertexCount);
                writer.Write(0u);
                writer.Write(this.VertexDataPointer);
                writer.Write(0ul);
                writer.Write(this.VertexFormatPointer);
                writer.Write(this.D3DBufferUnusedPointer);
                writer.Write(new byte[64]);
            }

        }
        public void WriteXml(StringBuilder sb, int indent)
        {
            var legacyFlags = (ushort)(Reserved0 | ((ushort)Flags << 8));
            YdrXml.ValueTag(sb, indent, "Flags", legacyFlags.ToString());

            if (VertexFormat != null)
            {
                VertexFormat.WriteXml(sb, indent, "Layout");
            }
            var primaryData = LockData ?? VertexData;
            if (primaryData != null)
            {
                YdrXml.OpenTag(sb, indent, "Data");
                primaryData.WriteXml(sb, indent + 1);
                YdrXml.CloseTag(sb, indent, "Data");
            }
            if ((VertexData != null) && (VertexData != primaryData))
            {
                YdrXml.OpenTag(sb, indent, "Data2");
                VertexData.WriteXml(sb, indent + 1);
                YdrXml.CloseTag(sb, indent, "Data2");
            }
        }
        public void ReadXml(XmlNode node)
        {
            var legacyFlags = (ushort)Xml.GetChildUIntAttribute(node, "Flags", "value");
            Reserved0 = (byte)legacyFlags;
            Flags = (grcVertexBufferFlags)(legacyFlags >> 8);

            var inode = node.SelectSingleNode("Layout");
            if (inode != null)
            {
                VertexFormat = new grcFvf();
                VertexFormat.ReadXml(inode);
                Stride = VertexFormat.FvfSize;
            }
            var dnode = node.SelectSingleNode("Data");
            if (dnode != null)
            {
                LockData = new VertexData();
                LockData.ReadXml(dnode, VertexFormat);
                VertexData = LockData;
                VertexCount = (uint)LockData.VertexCount;
            }
            var dnode2 = node.SelectSingleNode("Data2");
            if (dnode2 != null)
            {
                VertexData = new VertexData();
                VertexData.ReadXml(dnode2, VertexFormat);
            }
        }



        public void InitVertexDataFromGen9Data(byte[]? gen9bytes)
        {
            if (gen9bytes == null) return;
            if (G9_VertexFormat == null) return;

            //create the legacy vertex declaration and remap the vertex data

            var g9types = G9_VertexFormat.Types;
            var g9sizes = G9_VertexFormat.Sizes;//these seem to just contain the vertex stride - not sizes but offsets to next item
            var g9offs = G9_VertexFormat.Offsets;
            var vd = G9_VertexFormat.GetLegacyDeclaration();
            var vdtypes = vd.FvfChannelSizes;
            var vtype = (VertexType)vd.Fvf;

            //this really sucks that we have to rebuild the vertex data, but component ordering is different!
            //maybe some layouts still have the same ordering so this could be bypassed, but probably not many.
            var buf = new byte[gen9bytes.Length];
            for (int i = 0; i < g9types.Length; i++)//52
            {
                var t = g9types[i];
                if (t == 0) continue;
                var lci = VertexDeclarationG9.GetLegacyComponentIndex(i, vdtypes);
                if (lci < 0) continue;
                var cssize = (int)g9sizes[i];
                var csoff = (int)g9offs[i];
                var cdoff = vd.GetComponentOffset(lci);
                var cdtype = vd.GetComponentType(lci);
                var cdsize = VertexComponentTypes.GetSizeInBytes(cdtype);
                for (int v = 0; v < VertexCount; v++)
                {
                    var srcoff = csoff + (cssize * v);
                    var dstoff = cdoff + (Stride * v);
                    Buffer.BlockCopy(gen9bytes, srcoff, buf, dstoff, cdsize);
                }
            }

            var data = new VertexData();
            data.Stride = Stride;
            data.VertexCount = (int)VertexCount;
            data.VertexFormat = vd;
            data.VertexType = vtype;
            data.Data = buf;

            LockData = data;
            VertexData = data;
            VertexFormat = vd;

        }
        public byte[]? InitGen9DataFromVertexData()
        {
            if (VertexFormat == null) return null;
            var vertexData = LockData ?? VertexData;
            if (vertexData?.Data == null) return null;

            //create the Gen9 vertex declaration and remap the legacy vertex data

            var vd = VertexFormat;
            var vdtypes = vd.FvfChannelSizes;
            var info = VertexDeclarationG9.FromLegacyDeclaration(vd);
            var g9offs = info.Offsets;
            var g9sizes = info.Sizes;//these seem to just contain the vertex stride - not sizes but offsets to next item
            var g9types = info.Types;

            if (G9_VertexFormat != null)//sanity check with existing layout
            {
                if (info.VertexSize != G9_VertexFormat.VertexSize)
                { }
                if (info.VertexCount != G9_VertexFormat.VertexCount)
                { }
                if (info.ElementCount != G9_VertexFormat.ElementCount)
                { }
                for (int i = 0; i < 52; i++)
                {
                    if (info.Offsets[i] != G9_VertexFormat.Offsets[i])
                    { }
                    if (info.Sizes[i] != G9_VertexFormat.Sizes[i])
                    { }
                    if (info.Types[i] != G9_VertexFormat.Types[i])
                    { }
                }
            }
            G9_VertexFormat = info;

            var legabytes = vertexData.Data;
            var buf = new byte[legabytes.Length];
            for (int i = 0; i < g9types.Length; i++)//52
            {
                var t = g9types[i];
                if (t == 0) continue;
                var lci = VertexDeclarationG9.GetLegacyComponentIndex(i, vdtypes);
                if (lci < 0) continue;
                var cssize = (int)g9sizes[i];
                var csoff = (int)g9offs[i];
                var cdoff = vd.GetComponentOffset(lci);
                var cdtype = vd.GetComponentType(lci);
                var cdsize = VertexComponentTypes.GetSizeInBytes(cdtype);
                for (int v = 0; v < VertexCount; v++)
                {
                    var srcoff = cdoff + (Stride * v);
                    var dstoff = csoff + (cssize * v);
                    Buffer.BlockCopy(legabytes, srcoff, buf, dstoff, cdsize);
                }
            }

            return buf;
        }

        public void EnsureGen9()
        {
            VFT = 1080153080;

            if ((LockData == null) && (VertexData != null))
            {
                LockData = VertexData;
            }
            if (LockData != null)
            {
                LockData.G9_Data = InitGen9DataFromVertexData() ?? [];
            }
            VertexData = LockData;

            if (G9_SRV == null)
            {
                G9_SRV = new ShaderResourceViewG9();
                G9_SRV.Dimension = ShaderResourceViewDimensionG9.Buffer;
            }

        }


        public override IResourceBlock[] GetReferences()
        {
            var list = new List<IResourceBlock>();
            if (LockData != null) list.Add(LockData);
            if ((VertexData != null) && (VertexData != LockData)) list.Add(VertexData);
            if (G9_SRV != null) list.Add(G9_SRV);
            if (G9_VertexFormat != null)
            {
                list.Add(G9_VertexFormat);
            }
            else
            {
                if (VertexFormat != null) list.Add(VertexFormat);
            }
            return list.ToArray();
        }
    }

    [TypeConverter(typeof(ExpandableObjectConverter))] public class VertexData : ResourceSystemBlock
    {
        public override long BlockLength => Data?.Length ?? 0;

        public int Stride { get; set; }
        public int VertexCount { get; set; }
        public grcFvf? VertexFormat { get; set; }
        public VertexType VertexType { get; set; }

        public byte[] Data { get; set; } = [];
        public byte[] G9_Data { get; set; } = [];//only used when saving

        [Browsable(false)] public int VertexStride { get => Stride; set => Stride = value; }
        [Browsable(false)] public grcFvf? Info { get => VertexFormat; set => VertexFormat = value; }
        [Browsable(false)] public byte[] VertexBytes { get => Data; set => Data = value; }
        [Browsable(false)] public byte[] G9_VertexBytes { get => G9_Data; set => G9_Data = value; }

        public long MemoryUsage => (long)VertexCount * Stride;

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            //not used by gen9 reader

            Stride = Convert.ToInt32(parameters[0]);
            VertexCount = Convert.ToInt32(parameters[1]);
            VertexFormat = (grcFvf)parameters[2];
            VertexType = (VertexType)VertexFormat.Fvf;

            Data = reader.ReadBytes(checked(VertexCount * Stride));

            switch (VertexFormat.FvfChannelSizes)
            {
                case VertexDeclarationTypes.GTAV1: //YDR - 0x7755555555996996
                    break;
                case VertexDeclarationTypes.GTAV2:  //YFT - 0x030000000199A006
                    switch (VertexFormat.Fvf)
                    {
                        case 16473: VertexType = VertexType.PCCH2H4; break;  //  PCCH2H4 
                        default:break;
                    }
                    break;
                case VertexDeclarationTypes.GTAV3:  //YFT - 0x0300000001996006  PNCH2H4
                    switch (VertexFormat.Fvf)
                    {
                        case 89: VertexType = VertexType.PNCH2; break;     //  PNCH2
                        default: break;
                    }
                    break;
                default:
                    break;
            }

        }
        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            if (writer.IsGen9)
            {
                if (G9_Data != null) writer.Write(G9_Data);
            }
            else
            {
                if (Data != null) writer.Write(Data);
            }
        }
        public void WriteXml(StringBuilder sb, int indent)
        {
            var flags = VertexFormat?.Fvf ?? 0;
            var row = new StringBuilder();
            for (int v = 0; v < VertexCount; v++)
            {
                row.Clear();
                for (int k = 0; k < 16; k++)
                {
                    if (((flags >> k) & 0x1) == 1)
                    {
                        if (row.Length > 0) row.Append("   ");
                        var str = GetString(v, k, " ");
                        row.Append(str);
                    }
                }
                YdrXml.Indent(sb, indent);
                sb.AppendLine(row.ToString());
            }
        }
        public void ReadXml(XmlNode node, grcFvf? info)
        {
            VertexFormat = info;
            VertexType = (VertexType)(info?.Fvf ?? 0);

            if (VertexFormat != null)
            {
                var flags = VertexFormat.Fvf;
                var vstrs = new List<string[]>();
                var coldelim = new[] { ' ', '\t' };
                var rowdelim = new[] { '\n' };
                var rows = node?.InnerText?.Trim()?.Split(rowdelim, StringSplitOptions.RemoveEmptyEntries);
                if (rows != null)
                {
                    foreach (var row in rows)
                    {
                        var rowt = row.Trim();
                        if (string.IsNullOrEmpty(rowt)) continue;
                        var cols = row.Split(coldelim, StringSplitOptions.RemoveEmptyEntries);
                        vstrs.Add(cols);
                    }
                }
                if (vstrs.Count > 0)
                {
                    AllocateData(vstrs.Count);
                    for (int v = 0; v < vstrs.Count; v++)
                    {
                        var vstr = vstrs[v];
                        var sind = 0;
                        for (int k = 0; k < 16; k++)
                        {
                            if (((flags >> k) & 0x1) == 1)
                            {
                                SetString(v, k, vstr, ref sind);
                            }
                        }
                    }
                }
            }
        }


        public void AllocateData(int vertexCount)
        {
            if (VertexFormat != null)
            {
                var stride = VertexFormat.FvfSize;
                var byteCount = vertexCount * stride;
                Data = new byte[byteCount];
                VertexCount = vertexCount;
                Stride = stride;
            }
        }

        public void SetString(int v, int c, string[] strs, ref int sind)
        {
            if ((Info != null) && (VertexBytes != null) && (strs != null))
            {
                var ind = sind;
                float f(int i) => FloatUtil.Parse(strs[ind + i].Trim());
                byte b(int i) { if (byte.TryParse(strs[ind + i].Trim(), out byte x)) return x; else return 0; }
                var ct = Info.GetComponentType(c);
                var cc = VertexComponentTypes.GetComponentCount(ct);
                if (sind + cc > strs.Length)
                { return; }
                switch (ct)
                {
                    case VertexComponentType.Float: SetFloat(v, c, f(0)); break;
                    case VertexComponentType.Float2: SetVector2(v, c, new Vector2(f(0), f(1))); break;
                    case VertexComponentType.Float3: SetVector3(v, c, new Vector3(f(0), f(1), f(2))); break;
                    case VertexComponentType.Float4: SetVector4(v, c, new Vector4(f(0), f(1), f(2), f(3))); break;
                    case VertexComponentType.RGBA8SNorm: SetRGBA8SNorm(v, c, new Vector4(f(0), f(1), f(2), f(3))); break;
                    case VertexComponentType.Half2: SetHalf2(v, c, new Half2(f(0), f(1))); break;
                    case VertexComponentType.Half4: SetHalf4(v, c, new Half4(f(0), f(1), f(2), f(3))); break;
                    case VertexComponentType.Colour: SetColour(v, c, new Color(b(0), b(1), b(2), b(3))); break;
                    case VertexComponentType.UByte4: SetUByte4(v, c, new Color(b(0), b(1), b(2), b(3))); break;
                    default:
                        break;
                }
                sind += cc;
            }
        }
        public void SetFloat(int v, int c, float val)
        {
            if ((Info != null) && (VertexBytes != null))
            {
                var s = Info.Stride;
                var co = Info.GetComponentOffset(c);
                var o = (v * s) + co;
                var e = o + 4;//sizeof(float)
                if (e <= VertexBytes.Length)
                {
                    var b = BitConverter.GetBytes(val);
                    Buffer.BlockCopy(b, 0, VertexBytes, o, 4);
                }
            }
        }
        public void SetVector2(int v, int c, Vector2 val)
        {
            if ((Info != null) && (VertexBytes != null))
            {
                var s = Info.Stride;
                var co = Info.GetComponentOffset(c);
                var o = (v * s) + co;
                var e = o + 8;//sizeof(Vector2)
                if (e <= VertexBytes.Length)
                {
                    var x = BitConverter.GetBytes(val.X);
                    var y = BitConverter.GetBytes(val.Y);
                    Buffer.BlockCopy(x, 0, VertexBytes, o + 0, 4);
                    Buffer.BlockCopy(y, 0, VertexBytes, o + 4, 4);
                }
            }
        }
        public void SetVector3(int v, int c, Vector3 val)
        {
            if ((Info != null) && (VertexBytes != null))
            {
                var s = Info.Stride;
                var co = Info.GetComponentOffset(c);
                var o = (v * s) + co;
                var e = o + 12;//sizeof(Vector3)
                if (e <= VertexBytes.Length)
                {
                    var x = BitConverter.GetBytes(val.X);
                    var y = BitConverter.GetBytes(val.Y);
                    var z = BitConverter.GetBytes(val.Z);
                    Buffer.BlockCopy(x, 0, VertexBytes, o + 0, 4);
                    Buffer.BlockCopy(y, 0, VertexBytes, o + 4, 4);
                    Buffer.BlockCopy(z, 0, VertexBytes, o + 8, 4);
                }
            }
        }
        public void SetVector4(int v, int c, Vector4 val)
        {
            if ((Info != null) && (VertexBytes != null))
            {
                var s = Info.Stride;
                var co = Info.GetComponentOffset(c);
                var o = (v * s) + co;
                var e = o + 16;//sizeof(Vector4)
                if (e <= VertexBytes.Length)
                {
                    var x = BitConverter.GetBytes(val.X);
                    var y = BitConverter.GetBytes(val.Y);
                    var z = BitConverter.GetBytes(val.Z);
                    var w = BitConverter.GetBytes(val.W);
                    Buffer.BlockCopy(x, 0, VertexBytes, o + 0, 4);
                    Buffer.BlockCopy(y, 0, VertexBytes, o + 4, 4);
                    Buffer.BlockCopy(z, 0, VertexBytes, o + 8, 4);
                    Buffer.BlockCopy(w, 0, VertexBytes, o + 12, 4);
                }
            }
        }
        public void SetRGBA8SNorm(int v, int c, Vector4 val)
        {
            // Equivalent to DXGI_FORMAT_R8G8B8A8_SNORM
            if ((Info != null) && (VertexBytes != null))
            {
                var s = Info.Stride;
                var co = Info.GetComponentOffset(c);
                var o = (v * s) + co;
                var e = o + 4;//sizeof(RGBA8SNorm)
                if (e <= VertexBytes.Length)
                {
                    var x = (byte)Math.Max(-127.0f, Math.Min(val.X * 127.0f, 127.0f));
                    var y = (byte)Math.Max(-127.0f, Math.Min(val.Y * 127.0f, 127.0f));
                    var z = (byte)Math.Max(-127.0f, Math.Min(val.Z * 127.0f, 127.0f));
                    var w = (byte)Math.Max(-127.0f, Math.Min(val.W * 127.0f, 127.0f));
                    var u = x | (y << 8) | (z << 16) | (w << 24);
                    var b = BitConverter.GetBytes(u);
                    Buffer.BlockCopy(b, 0, VertexBytes, o, 4);
                }
            }
        }
        public void SetHalf2(int v, int c, Half2 val)
        {
            if ((Info != null) && (VertexBytes != null))
            {
                var s = Info.Stride;
                var co = Info.GetComponentOffset(c);
                var o = (v * s) + co;
                var e = o + 4;//sizeof(Half2)
                if (e <= VertexBytes.Length)
                {
                    var x = BitConverter.GetBytes(val.X.RawValue);
                    var y = BitConverter.GetBytes(val.Y.RawValue);
                    Buffer.BlockCopy(x, 0, VertexBytes, o + 0, 2);
                    Buffer.BlockCopy(y, 0, VertexBytes, o + 2, 2);
                }
            }
        }
        public void SetHalf4(int v, int c, Half4 val)
        {
            if ((Info != null) && (VertexBytes != null))
            {
                var s = Info.Stride;
                var co = Info.GetComponentOffset(c);
                var o = (v * s) + co;
                var e = o + 8;//sizeof(Half4)
                if (e <= VertexBytes.Length)
                {
                    var x = BitConverter.GetBytes(val.X.RawValue);
                    var y = BitConverter.GetBytes(val.Y.RawValue);
                    var z = BitConverter.GetBytes(val.Z.RawValue);
                    var w = BitConverter.GetBytes(val.W.RawValue);
                    Buffer.BlockCopy(x, 0, VertexBytes, o + 0, 2);
                    Buffer.BlockCopy(y, 0, VertexBytes, o + 2, 2);
                    Buffer.BlockCopy(z, 0, VertexBytes, o + 4, 2);
                    Buffer.BlockCopy(w, 0, VertexBytes, o + 6, 2);
                }
            }
        }
        public void SetColour(int v, int c, Color val)
        {
            if ((Info != null) && (VertexBytes != null))
            {
                var s = Info.Stride;
                var co = Info.GetComponentOffset(c);
                var o = (v * s) + co;
                var e = o + 4;//sizeof(Color)
                if (e <= VertexBytes.Length)
                {
                    var u = val.ToRgba();
                    var b = BitConverter.GetBytes(u);
                    Buffer.BlockCopy(b, 0, VertexBytes, o, 4);
                }
            }
        }
        public void SetUByte4(int v, int c, Color val)
        {
            if ((Info != null) && (VertexBytes != null))
            {
                var s = Info.Stride;
                var co = Info.GetComponentOffset(c);
                var o = (v * s) + co;
                var e = o + 4;//sizeof(UByte4)
                if (e <= VertexBytes.Length)
                {
                    var u = val.ToRgba();
                    var b = BitConverter.GetBytes(u);
                    Buffer.BlockCopy(b, 0, VertexBytes, o, 4);
                }
            }
        }

        public string GetString(int v, int c, string d = ", ")
        {
            if ((Info != null) && (VertexBytes != null))
            {
                var ct = Info.GetComponentType(c);
                switch (ct)
                {
                    case VertexComponentType.Float: return FloatUtil.ToString(GetFloat(v, c));
                    case VertexComponentType.Float2: return FloatUtil.GetVector2String(GetVector2(v, c), d);
                    case VertexComponentType.Float3: return FloatUtil.GetVector3String(GetVector3(v, c), d);
                    case VertexComponentType.Float4: return FloatUtil.GetVector4String(GetVector4(v, c), d);
                    case VertexComponentType.RGBA8SNorm: return FloatUtil.GetVector4String(GetRGBA8SNorm(v, c), d);
                    case VertexComponentType.Half2: return FloatUtil.GetHalf2String(GetHalf2(v, c), d);
                    case VertexComponentType.Half4: return FloatUtil.GetHalf4String(GetHalf4(v, c), d);
                    case VertexComponentType.Colour: return FloatUtil.GetColourString(GetColour(v, c), d);
                    case VertexComponentType.UByte4: return FloatUtil.GetColourString(GetUByte4(v, c), d);
                    default:
                        break;
                }
            }
            return string.Empty;
        }
        public float GetFloat(int v, int c)
        {
            if ((Info != null) && (VertexBytes != null))
            {
                var s = Info.Stride;
                var co = Info.GetComponentOffset(c);
                var o = (v * s) + co;
                var e = o + 4;//sizeof(float)
                if (e <= VertexBytes.Length)
                {
                    var f = BitConverter.ToSingle(VertexBytes, o);
                    return f;
                }
            }
            return 0;
        }
        public Vector2 GetVector2(int v, int c)
        {
            if ((Info != null) && (VertexBytes != null))
            {
                var s = Info.Stride;
                var co = Info.GetComponentOffset(c);
                var o = (v * s) + co;
                var e = o + 8;//sizeof(Vector2)
                if (e <= VertexBytes.Length)
                {
                    var x = BitConverter.ToSingle(VertexBytes, o + 0);
                    var y = BitConverter.ToSingle(VertexBytes, o + 4);
                    return new Vector2(x, y);
                }
            }
            return Vector2.Zero;
        }
        public Vector3 GetVector3(int v, int c)
        {
            if ((Info != null) && (VertexBytes != null))
            {
                var s = Info.Stride;
                var co = Info.GetComponentOffset(c);
                var o = (v * s) + co;
                var e = o + 12;//sizeof(Vector3)
                if (e <= VertexBytes.Length)
                {
                    var x = BitConverter.ToSingle(VertexBytes, o + 0);
                    var y = BitConverter.ToSingle(VertexBytes, o + 4);
                    var z = BitConverter.ToSingle(VertexBytes, o + 8);
                    return new Vector3(x, y, z);
                }
            }
            return Vector3.Zero;
        }
        public Vector4 GetVector4(int v, int c)
        {
            if ((Info != null) && (VertexBytes != null))
            {
                var s = Info.Stride;
                var co = Info.GetComponentOffset(c);
                var o = (v * s) + co;
                var e = o + 16;//sizeof(Vector4)
                if (e <= VertexBytes.Length)
                {
                    var x = BitConverter.ToSingle(VertexBytes, o + 0);
                    var y = BitConverter.ToSingle(VertexBytes, o + 4);
                    var z = BitConverter.ToSingle(VertexBytes, o + 8);
                    var w = BitConverter.ToSingle(VertexBytes, o + 12);
                    return new Vector4(x, y, z, w);
                }
            }
            return Vector4.Zero;
        }
        public Vector4 GetRGBA8SNorm(int v, int c)
        {
            // Equivalent to DXGI_FORMAT_R8G8B8A8_SNORM
            if ((Info != null) && (VertexBytes != null))
            {
                var s = Info.Stride;
                var co = Info.GetComponentOffset(c);
                var o = (v * s) + co;
                var e = o + 4;//sizeof(RGBA8SNorm)
                if (e <= VertexBytes.Length)
                {
                    var xyzw = BitConverter.ToUInt32(VertexBytes, o);
                    var x = (sbyte)(xyzw & 0xFF) / 127.0f;
                    var y = (sbyte)((xyzw >> 8) & 0xFF) / 127.0f;
                    var z = (sbyte)((xyzw >> 16) & 0xFF) / 127.0f;
                    var w = (sbyte)((xyzw >> 24) & 0xFF) / 127.0f;
                    return new Vector4(x, y, z, w);
                }
            }
            return Vector4.Zero;
        }
        public Half2 GetHalf2(int v, int c)
        {
            if ((Info != null) && (VertexBytes != null))
            {
                var s = Info.Stride;
                var co = Info.GetComponentOffset(c);
                var o = (v * s) + co;
                var e = o + 4;//sizeof(Half2)
                if (e <= VertexBytes.Length)
                {
                    var x = BitConverter.ToUInt16(VertexBytes, o + 0);
                    var y = BitConverter.ToUInt16(VertexBytes, o + 2);
                    return new Half2(x, y);
                }
            }
            return new Half2(0, 0);
        }
        public Half4 GetHalf4(int v, int c)
        {
            if ((Info != null) && (VertexBytes != null))
            {
                var s = Info.Stride;
                var co = Info.GetComponentOffset(c);
                var o = (v * s) + co;
                var e = o + 8;//sizeof(Half4)
                if (e <= VertexBytes.Length)
                {
                    var x = BitConverter.ToUInt16(VertexBytes, o + 0);
                    var y = BitConverter.ToUInt16(VertexBytes, o + 2);
                    var z = BitConverter.ToUInt16(VertexBytes, o + 4);
                    var w = BitConverter.ToUInt16(VertexBytes, o + 6);
                    return new Half4(x, y, z, w);
                }
            }
            return new Half4(0, 0, 0, 0);
        }
        public Color GetColour(int v, int c)
        {
            if ((Info != null) && (VertexBytes != null))
            {
                var s = Info.Stride;
                var co = Info.GetComponentOffset(c);
                var o = (v * s) + co;
                var e = o + 4;//sizeof(Color)
                if (e <= VertexBytes.Length)
                {
                    var rgba = BitConverter.ToUInt32(VertexBytes, o);
                    return new Color(rgba);
                }
            }
            return Color.Black;
        }
        public Color GetUByte4(int v, int c)
        {
            //Color is the same as UByte4 really
            if ((Info != null) && (VertexBytes != null))
            {
                var s = Info.Stride;
                var co = Info.GetComponentOffset(c);
                var o = (v * s) + co;
                var e = o + 4;//sizeof(UByte4)
                if (e <= VertexBytes.Length)
                {
                    var rgba = BitConverter.ToUInt32(VertexBytes, o);
                    return new Color(rgba);
                }
            }
            return new Color(0, 0, 0, 0);
        }


        public override string ToString()
        {
            return "Type: " + VertexType.ToString() + ", Count: " + VertexCount.ToString();
        }
    }

    [Flags] public enum grcFvfFlags : byte
    {
        NONE = 0,
        PRE_TRANSFORM = 1,
    }

    [TypeConverter(typeof(ExpandableObjectConverter))] public class grcFvf : ResourceSystemBlock
    {
        public override long BlockLength => 16;

        // structure data
        public uint Fvf { get; set; }
        public byte FvfSize { get; set; }
        public grcFvfFlags Flags { get; set; }
        public byte DynamicOrder { get; set; }
        public byte ChannelCount { get; set; }
        public VertexDeclarationTypes FvfChannelSizes { get; set; }

        public bool IsPreTransform => (Flags & grcFvfFlags.PRE_TRANSFORM) != 0;
        public bool IsDynamicOrder => DynamicOrder != 0;

        [Browsable(false)] public ushort Stride { get => FvfSize; set => FvfSize = checked((byte)value); }
        [Browsable(false)] public byte Unknown_6h { get => DynamicOrder; set => DynamicOrder = value; }
        [Browsable(false)] public byte Count { get => ChannelCount; set => ChannelCount = value; }
        [Browsable(false)] public VertexDeclarationTypes Types { get => FvfChannelSizes; set => FvfChannelSizes = value; }

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            // read structure data
            this.Fvf = reader.ReadUInt32();
            this.FvfSize = reader.ReadByte();
            this.Flags = (grcFvfFlags)reader.ReadByte();
            this.DynamicOrder = reader.ReadByte();
            this.ChannelCount = reader.ReadByte();
            this.FvfChannelSizes = (VertexDeclarationTypes)reader.ReadUInt64();
        }
        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            // write structure data
            writer.Write(this.Fvf);
            writer.Write(this.FvfSize);
            writer.Write((byte)this.Flags);
            writer.Write(this.DynamicOrder);
            writer.Write(this.ChannelCount);
            writer.Write((ulong)this.FvfChannelSizes);
        }
        public void WriteXml(StringBuilder sb, int indent, string name)
        {
            var flagsAttribute = Flags != grcFvfFlags.NONE ? $" flags=\"{(byte)Flags}\"" : string.Empty;
            var dynamicOrderAttribute = DynamicOrder != 0 ? $" dynamicOrder=\"{DynamicOrder}\"" : string.Empty;
            YdrXml.OpenTag(sb, indent, $"{name} type=\"{FvfChannelSizes}\"{flagsAttribute}{dynamicOrderAttribute}");

            for (int k = 0; k < 16; k++)
            {
                if (((Fvf >> k) & 0x1) == 1)
                {
                    var componentSemantic = (VertexSemantics)k;
                    var tag = componentSemantic.ToString();
                    YdrXml.SelfClosingTag(sb, indent + 1, tag);
                }
            }

            YdrXml.CloseTag(sb, indent, name);
        }
        public void ReadXml(XmlNode? node)
        {
            if (node == null) return;

            FvfChannelSizes = Xml.GetEnumValue<VertexDeclarationTypes>(Xml.GetStringAttribute(node, "type"));
            Flags = (grcFvfFlags)Xml.GetUIntAttribute(node, "flags");
            DynamicOrder = (byte)Xml.GetUIntAttribute(node, "dynamicOrder");

            uint f = 0;
            foreach (XmlNode cnode in node.ChildNodes)
            {
                if (cnode is XmlElement celem)
                {
                    var componentSematic = Xml.GetEnumValue<VertexSemantics>(celem.Name);
                    var idx = (int)componentSematic;
                    f = f | (1u << idx);
                }
            }
            Fvf = f;

            UpdateCountAndStride();
        }

        public ulong GetDeclarationId()
        {
            ulong res = 0;
            for(int i=0; i < 16; i++)
            {
                if (((Fvf >> i) & 1) == 1)
                {
                    res |= (ulong)FvfChannelSizes & (0xFuL << (i * 4));
                }
            }
            return res;
        }

        public VertexComponentType GetComponentType(int index)
        {
            //index is the flags bit index
            return (VertexComponentType)(((ulong)FvfChannelSizes >> (index * 4)) & 0x0000000F);
        }

        public int GetComponentOffset(int index)
        {
            //index is the flags bit index
            var offset = 0;
            for (int k = 0; k < index; k++)
            {
                if (((Fvf >> k) & 0x1) == 1)
                {
                    var componentType = GetComponentType(k);
                    offset += VertexComponentTypes.GetSizeInBytes(componentType);
                }
            }
            return offset;
        }

        public void UpdateCountAndStride()
        {
            var cnt = 0;
            var str = 0;
            for (int k = 0; k < 16; k++)
            {
                if (((Fvf >> k) & 0x1) == 1)
                {
                    var componentType = GetComponentType(k);
                    str += VertexComponentTypes.GetSizeInBytes(componentType);
                    cnt++;
                }
            }

            ChannelCount = checked((byte)cnt);
            FvfSize = checked((byte)(IsDynamicOrder ? (str + 15) & ~15 : str));
        }

        public override string ToString()
        {
            return $"{FvfSize}: {ChannelCount}: {Fvf}: {FvfChannelSizes}";
        }
    }

    [TypeConverter(typeof(ExpandableObjectConverter))] public class VertexDeclarationG9 : ResourceSystemBlock
    {
        public override long BlockLength => 320;//316;
        public uint[] Offsets { get; set; } = [];//[52]
        public byte[] Sizes { get; set; } = [];//[52]
        public byte[] Types { get; set; } = [];//[52] //(VertexDeclarationG9ElementFormat)
        public ulong Data { get; set; }

        public bool HasSOA //seems to always be false for GTAV gen9  (but true for RDR2)
        {
            get => (Data & 1) > 0;
        }
        public bool Flag //seems to always be false
        {
            get => ((Data >> 1) & 1) > 0;
        }
        public byte VertexSize
        {
            get => (byte)((Data >> 2) & 0xFF);
            set => Data = (Data & 0xFFFFFC03) + ((value & 0xFFu) << 2);
        }
        public uint VertexCount
        {
            get => (uint)((Data >> 10) & 0x3FFFFF);
            set => Data = (Data & 0x3FF) + ((value & 0x3FFFFF) << 10);
        }
        public uint ElementCount
        {
            get
            {
                if (Types == null) return 0;
                var n = 0u;
                foreach (var t in Types)
                {
                    if (t != 0) n++;
                }
                return n;
            }
        }

        public VertexDeclarationG9ElementFormat[] G9Formats
        {
            get
            {
                if (Types == null) return [];
                var n = ElementCount;
                var a = new VertexDeclarationG9ElementFormat[n];
                var c = 0;
                foreach (var t in Types)
                {
                    if (t == 0) continue;
                    a[c] = (VertexDeclarationG9ElementFormat)t;
                    c++;
                }
                return a;
            }
        }

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            Offsets = reader.ReadStructs<uint>(52);
            Sizes = reader.ReadBytes(52);
            Types = reader.ReadBytes(52);
            Data = reader.ReadUInt64();
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            writer.WriteStructs<uint>(Offsets);
            writer.Write(Sizes);
            writer.Write(Types);
            writer.Write(Data);

        }



        public grcFvf GetLegacyDeclaration(VertexDeclarationTypes vdtypes = VertexDeclarationTypes.GTAV1)
        {
            var vdflags = 0u;
            var g9types = Types;
            var g9sizes = Sizes;//these seem to just contain the vertex stride - not sizes but offsets to next item
            var g9offs = Offsets;
            var g9cnt = ElementCount;
            for (int i = 0; i < g9types.Length; i++)//52
            {
                var t = g9types[i];
                if (t == 0) continue;
                var lci = GetLegacyComponentIndex(i, vdtypes);
                if (lci < 0)
                {
                    //this component type won't work for the given type...
                    //TODO: try a different type! eg GTAV4
                    continue;
                }
                vdflags = BitUtil.SetBit(vdflags, lci);
            }
            var vtype = (VertexType)vdflags;
            switch (vtype)//just testing converted flags
            {
                case VertexType.Default:
                case VertexType.DefaultEx:
                case VertexType.PCTT:
                case VertexType.PNCCT:
                case VertexType.PNCCTTTX:
                case VertexType.PNCTTX:
                case VertexType.PNCCTT:
                case VertexType.PNCTTTX:
                case VertexType.PNCCTTX_2:
                case VertexType.PNCCTX:
                case VertexType.PNCCTTX:
                case VertexType.PBBNCTX:
                case VertexType.PBBNCT:
                case VertexType.PBBNCCTX:
                case VertexType.PBBCCT:
                case VertexType.PBBNCTTX:
                case VertexType.PNC:
                case VertexType.PCT:
                case VertexType.PNCTTTX_2:
                case VertexType.PNCTTTTX:
                case VertexType.PBBNCCT:
                case VertexType.PT:
                case VertexType.PNCCTTTT:
                case VertexType.PNCTTTX_3:
                case VertexType.PCC:
                case (VertexType)113://PCCT: decal_diff_only_um, ch_chint02_floor_mural_01.ydr
                case (VertexType)1://P: farlods.ydd
                case VertexType.PTT:
                case VertexType.PC:
                case VertexType.PBBNCCTTX:
                case VertexType.PBBNCCTT:
                case VertexType.PBBNCTT:
                case VertexType.PBBNCTTT:
                case VertexType.PNCTT:
                case VertexType.PNCTTT:
                case VertexType.PBBNCTTTX:
                    break;
                default:
                    break;
            }

            var vd = new grcFvf();
            vd.FvfChannelSizes = vdtypes;
            vd.Fvf = vdflags;
            vd.UpdateCountAndStride();
            if (vd.ChannelCount != g9cnt)
            { }//just testing converted component count actually matches
            if (vd.FvfSize != VertexSize)
            { }//just testing converted stride actually matches

            return vd;
        }
        public static VertexDeclarationG9 FromLegacyDeclaration(grcFvf vd)
        {
            var vstride = vd.FvfSize;
            var vdtypes = vd.FvfChannelSizes;// VertexDeclarationTypes.GTAV1;
            var vdflags = vd.Fvf;
            var g9offs = new uint[52];
            var g9sizes = new byte[52];//these seem to just contain the vertex stride - not sizes but offsets to next item
            var g9types = new byte[52];

            if (vdtypes != VertexDeclarationTypes.GTAV1)
            {
                //there might be some issues with converting other types
                //for example, if a component doesn't have a (known) equivalent gen9 type
            }

            var offset = 0u;
            for (int i = 0; i < 52; i++)
            {
                g9offs[i] = offset;
                var lci = GetLegacyComponentIndex(i, vdtypes);
                if (lci < 0) continue;//can't be used, unavailable for GTAV1
                if ((vdflags & (1u << lci)) == 0) continue;
                var ctype = (VertexComponentType)(((ulong)vdtypes >> (lci * 4)) & 0xF);
                var csize = VertexComponentTypes.GetSizeInBytes(ctype);
                offset += (uint)csize;
                g9sizes[i] = vstride;
                g9types[i] = (byte)GetGen9ComponentType(lci, vdtypes);
            }
            var info = new VertexDeclarationG9();
            info.Offsets = g9offs;
            info.Sizes = g9sizes;
            info.Types = g9types;
            info.VertexSize = vstride;
            info.VertexCount = 0;//it's actually supposed to be 0

            return info;
        }


        public static VertexComponentType GetLegacyComponentType(VertexDeclarationG9ElementFormat f)
        {
            switch (f)
            {
                case VertexDeclarationG9ElementFormat.R32G32B32_FLOAT: return VertexComponentType.Float3;
                case VertexDeclarationG9ElementFormat.R32G32B32A32_FLOAT: return VertexComponentType.Float4;
                case VertexDeclarationG9ElementFormat.R8G8B8A8_UNORM: return VertexComponentType.Colour;
                case VertexDeclarationG9ElementFormat.R32G32_TYPELESS: return VertexComponentType.Float2;
                case VertexDeclarationG9ElementFormat.R8G8B8A8_UINT: return VertexComponentType.Colour;//for bone inds
                default: return VertexComponentType.Float4;
            }
        }
        public static int GetLegacyComponentIndex(int i, VertexDeclarationTypes vdtypes)
        {
            if (vdtypes != VertexDeclarationTypes.GTAV1)
            { }//TODO: is this ok? are component indices (semantics?) always the same?
            //GTAV1 = 0x7755555555996996, // GTAV - used by most drawables
            switch (i)
            {
                case 0: return 0;//POSITION0
                case 4: return 3;//NORMAL0
                case 8: return 14;//TANGENT0
                case 16: return 1;//BLENDWEIGHTS0
                case 20: return 2;//BLENDINDICES0
                case 24: return 4;//COLOR0
                case 25: return 5;//COLOR1
                case 28: return 6;//TEXCOORD0
                case 29: return 7;//TEXCOORD1
                case 30: return 8;//TEXCOORD2
                case 31: return 9;//TEXCOORD3
                case 32: return 10;//TEXCOORD4
                case 33: return 11;//TEXCOORD5
                default: return -1;
            }
        }
        public static VertexDeclarationG9ElementFormat GetGen9ComponentTypeGTAV1(int lci)
        {
            //(lci=legacy component index)
            //GTAV1 = 0x7755555555996996, // GTAV - used by most drawables
            switch (lci)
            {
                case 0: return VertexDeclarationG9ElementFormat.R32G32B32_FLOAT;
                case 1: return VertexDeclarationG9ElementFormat.R8G8B8A8_UNORM;
                case 2: return VertexDeclarationG9ElementFormat.R8G8B8A8_UINT;
                case 3: return VertexDeclarationG9ElementFormat.R32G32B32_FLOAT;
                case 4:
                case 5: return VertexDeclarationG9ElementFormat.R8G8B8A8_UNORM;
                case 6:
                case 7:
                case 8:
                case 9:
                case 10:
                case 11:
                case 12:
                case 13: return VertexDeclarationG9ElementFormat.R32G32_TYPELESS;
                case 14:
                case 15: return VertexDeclarationG9ElementFormat.R32G32B32A32_FLOAT;
                default: return VertexDeclarationG9ElementFormat.R32G32B32A32_FLOAT;
            }
        }
        public static VertexDeclarationG9ElementFormat GetGen9ComponentType(int lci, VertexDeclarationTypes vdtypes)
        {
            if (vdtypes == VertexDeclarationTypes.GTAV1) return GetGen9ComponentTypeGTAV1(lci);

            switch (lci)
            {
                case 1: return VertexDeclarationG9ElementFormat.R8G8B8A8_UNORM;//boneweights
                case 2: return VertexDeclarationG9ElementFormat.R8G8B8A8_UINT;//boneinds
            }
            var t = (VertexComponentType)((((ulong)vdtypes) >> (lci * 4)) & 0xF);
            switch (t)
            {
                case VertexComponentType.Half2: return VertexDeclarationG9ElementFormat.R16G16_FLOAT;
                case VertexComponentType.Float: return VertexDeclarationG9ElementFormat.NONE;
                case VertexComponentType.Half4: return VertexDeclarationG9ElementFormat.R16G16B16A16_FLOAT;
                case VertexComponentType.FloatUnk: return VertexDeclarationG9ElementFormat.NONE;
                case VertexComponentType.Float2: return VertexDeclarationG9ElementFormat.R32G32_TYPELESS;
                case VertexComponentType.Float3: return VertexDeclarationG9ElementFormat.R32G32B32_FLOAT;
                case VertexComponentType.Float4: return VertexDeclarationG9ElementFormat.R32G32B32A32_FLOAT;
                case VertexComponentType.UByte4: return VertexDeclarationG9ElementFormat.R8G8B8A8_UINT;
                case VertexComponentType.Colour: return VertexDeclarationG9ElementFormat.R8G8B8A8_UNORM;
                case VertexComponentType.RGBA8SNorm: return VertexDeclarationG9ElementFormat.R8G8B8A8_UNORM;//close?!
                default: return VertexDeclarationG9ElementFormat.NONE;
            }

        }


    }
    public enum VertexDeclarationG9ElementFormat : byte
    {
        NONE = 0,
        R32G32B32A32_FLOAT = 2,
        R32G32B32_FLOAT = 6,
        R16G16B16A16_FLOAT = 10,
        R32G32_TYPELESS = 16,
        D3DX_R10G10B10A2 = 24,
        R8G8B8A8_UNORM = 28,
        R8G8B8A8_UINT = 30,
        R16G16_FLOAT = 34,
    }


    [TypeConverter(typeof(ExpandableObjectConverter))] public class grcIndexBuffer : ResourceSystemBlock
    {
        private const uint PreallocatedFlag = 0x01000000;
        private const uint FlagsMask = 0xFF000000;
        private const uint IndexCountMask = 0x00FFFFFF;

        public override long BlockLength => 96;
        public override long BlockLength_Gen9 => 64;

        // structure data
        public uint VFT { get; set; } = 1080152408;
        public uint IndexCountAndFlags { get; set; }
        public ulong IndexDataPointer { get; set; }
        public ulong D3DBufferUnusedPointer { get; set; }

        public uint IndexCount
        {
            get => IndexCountAndFlags & IndexCountMask;
            set
            {
                if (value > IndexCountMask) throw new ArgumentOutOfRangeException(nameof(value));
                IndexCountAndFlags = (IndexCountAndFlags & FlagsMask) | value;
            }
        }
        public bool IsPreallocatedMemory
        {
            get => (IndexCountAndFlags & PreallocatedFlag) != 0;
            set => IndexCountAndFlags = value ? IndexCountAndFlags | PreallocatedFlag : IndexCountAndFlags & ~PreallocatedFlag;
        }
        public long MemoryUsage => (long)IndexCount * sizeof(ushort);

        [Browsable(false)] public uint IndicesCount { get => IndexCount; set => IndexCount = value; }

        // gen9 structure data
        public ushort G9_IndexSize { get; set; } = sizeof(ushort);
        public uint G9_BindFlags { get; set; }
        public ulong G9_ShaderResourceViewPointer { get; set; }
        public ShaderResourceViewG9? G9_SRV { get; set; }

        // reference data
        public ushort[] Indices { get; set; } = [];

        private ResourceSystemStructBlock<ushort>? IndicesBlock; //only used when saving

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            this.VFT = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            this.IndexCountAndFlags = reader.ReadUInt32();

            if (reader.IsGen9)
            {
                G9_IndexSize = reader.ReadUInt16();
                _ = reader.ReadUInt16();
                G9_BindFlags = reader.ReadUInt32();
                _ = reader.ReadUInt32();
                IndexDataPointer = reader.ReadUInt64();
                _ = reader.ReadUInt64();
                _ = reader.ReadUInt64();
                G9_ShaderResourceViewPointer = reader.ReadUInt64();
                _ = reader.ReadUInt64();

                if (G9_IndexSize != sizeof(ushort))
                {
                    throw new System.IO.InvalidDataException($"Unsupported Gen9 index size: {G9_IndexSize} bytes.");
                }
            }
            else
            {
                _ = reader.ReadUInt32();
                this.IndexDataPointer = reader.ReadUInt64();
                this.D3DBufferUnusedPointer = reader.ReadUInt64();
                _ = reader.ReadBytes(64);
            }

            this.Indices = reader.ReadUshortsAt(this.IndexDataPointer, this.IndexCount) ?? [];
            if (reader.IsGen9)
            {
                G9_SRV = reader.ReadBlockAt<ShaderResourceViewG9>(G9_ShaderResourceViewPointer);
            }
        }
        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            this.IndexCount = (uint)(this.IndicesBlock?.ItemCount ?? 0);
            this.IndexDataPointer = (ulong)(this.IndicesBlock?.FilePosition ?? 0);

            writer.Write(this.VFT);
            writer.Write(1u);
            writer.Write(this.IndexCountAndFlags);

            if (writer.IsGen9)
            {
                G9_ShaderResourceViewPointer = (ulong)(G9_SRV?.FilePosition ?? 0);

                if (G9_BindFlags == 0) G9_BindFlags = 0x0058020a;

                writer.Write(G9_IndexSize);
                writer.Write((ushort)0);
                writer.Write(G9_BindFlags);
                writer.Write(0u);
                writer.Write(IndexDataPointer);
                writer.Write(0ul);
                writer.Write(0ul);
                writer.Write(G9_ShaderResourceViewPointer);
                writer.Write(0ul);
            }
            else
            {
                writer.Write(0u);
                writer.Write(this.IndexDataPointer);
                writer.Write(this.D3DBufferUnusedPointer);
                writer.Write(new byte[64]);
            }
        }
        public void WriteXml(StringBuilder sb, int indent)
        {
            if (Indices != null)
            {
                YdrXml.WriteRawArray(sb, Indices, indent, "Data", "", null, 24);
            }
        }
        public void ReadXml(XmlNode node)
        {
            var inode = node.SelectSingleNode("Data");
            if (inode != null)
            {
                Indices = Xml.GetRawUshortArray(node);
                IndexCount = (uint)(Indices?.Length ?? 0);
            }
        }


        public void EnsureGen9()
        {
            VFT = 1080152408;

            if (G9_SRV == null)
            {
                G9_SRV = new ShaderResourceViewG9();
                G9_SRV.Dimension = ShaderResourceViewDimensionG9.Buffer;
            }

        }

        public override IResourceBlock[] GetReferences()
        {
            var list = new List<IResourceBlock>();
            if (Indices != null)
            {
                IndicesBlock = new ResourceSystemStructBlock<ushort>(Indices);
                list.Add(IndicesBlock);
            }
            if (G9_SRV != null)
            {
                list.Add(G9_SRV);
            }
            return list.ToArray();
        }
    }


    public enum LightType : byte
    {
        Point = 1,
        Spot = 2,
        Capsule = 4,
    }

    [TypeConverter(typeof(ExpandableObjectConverter))] public class CLightAttr : ResourceSystemBlock, IMetaXmlItem
    {
        public override long BlockLength
        {
            get { return 168; }
        }

        // structure data
        public ulong VFT { get; set; }
        public Vector3 Position { get; set; }
        public byte ColorR { get; set; }
        public byte ColorG { get; set; }
        public byte ColorB { get; set; }
        public byte Flashiness { get; set; }
        public float Intensity { get; set; }
        public uint Flags { get; set; }
        public short BoneTag { get; set; }
        public LightType Type { get; set; }
        public byte GroupId { get; set; }
        public uint TimeFlags { get; set; }
        public float Falloff { get; set; }
        public float FalloffExponent { get; set; }
        public Vector3 CullingPlaneNormal { get; set; }
        public float CullingPlaneOffset { get; set; }
        public byte ShadowBlur { get; set; }
        public byte ExtraFlags { get; set; }
        public uint ExtraFlagsBank { get; set; }
        public float VolumeIntensity { get; set; }
        public float VolumeSizeScale { get; set; }
        public byte VolumeOuterColorR { get; set; }
        public byte VolumeOuterColorG { get; set; }
        public byte VolumeOuterColorB { get; set; }
        public byte LightHash { get; set; }
        public float VolumeOuterIntensity { get; set; }
        public float CoronaSize { get; set; }
        public float VolumeOuterExponent { get; set; }
        public byte LightFadeDistance { get; set; }
        public byte ShadowFadeDistance { get; set; }
        public byte SpecularFadeDistance { get; set; }
        public byte VolumetricFadeDistance { get; set; }
        public float ShadowNearClip { get; set; }
        public float CoronaIntensity { get; set; }
        public float CoronaZBias { get; set; }
        public Vector3 Direction { get; set; }
        public Vector3 Tangent { get; set; }
        public float ConeInnerAngle { get; set; }
        public float ConeOuterAngle { get; set; }
        public Vector3 Extents { get; set; }
        public MetaHash ProjectedTextureKey { get; set; }

        public bool UpdateRenderable = false; //used by model light form


        public Quaternion Orientation
        {
            get
            {
                Vector3 tx = new();
                Vector3 ty = new();

                switch (Type)
                {
                    case LightType.Point:
                        return Quaternion.Identity;
                    case LightType.Spot:
                    case LightType.Capsule:
                        tx = Vector3.Normalize(Tangent);
                        ty = Vector3.Normalize(Vector3.Cross(Direction, Tangent));
                        break;
                }

                var m = new Matrix();
                m.Row1 = new Vector4(tx, 0);
                m.Row2 = new Vector4(ty, 0);
                m.Row3 = new Vector4(Direction, 0);
                return Quaternion.RotationMatrix(m);
            }
            set
            {
                var inv = Quaternion.Invert(Orientation);
                var delta = value * inv;
                Direction = Vector3.Normalize(delta.Multiply(Direction));
                Tangent = Vector3.Normalize(delta.Multiply(Tangent));
            }
        }


        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            //read structure data
            VFT = reader.ReadUInt64();
            Position = reader.ReadVector3();
            _ = reader.ReadUInt32();
            ColorR = reader.ReadByte();
            ColorG = reader.ReadByte();
            ColorB = reader.ReadByte();
            Flashiness = reader.ReadByte();
            Intensity = reader.ReadSingle();
            Flags = reader.ReadUInt32();
            BoneTag = reader.ReadInt16();
            Type = (LightType)reader.ReadByte();
            GroupId = reader.ReadByte();
            TimeFlags = reader.ReadUInt32();
            Falloff = reader.ReadSingle();
            FalloffExponent = reader.ReadSingle();
            CullingPlaneNormal = reader.ReadVector3();
            CullingPlaneOffset = reader.ReadSingle();
            ShadowBlur = reader.ReadByte();
            ExtraFlags = reader.ReadByte();
            _ = reader.ReadInt16();
            ExtraFlagsBank = reader.ReadUInt32();
            VolumeIntensity = reader.ReadSingle();
            VolumeSizeScale = reader.ReadSingle();
            VolumeOuterColorR = reader.ReadByte();
            VolumeOuterColorG = reader.ReadByte();
            VolumeOuterColorB = reader.ReadByte();
            LightHash = reader.ReadByte();
            VolumeOuterIntensity = reader.ReadSingle();
            CoronaSize = reader.ReadSingle();
            VolumeOuterExponent = reader.ReadSingle();
            LightFadeDistance = reader.ReadByte();
            ShadowFadeDistance = reader.ReadByte();
            SpecularFadeDistance = reader.ReadByte();
            VolumetricFadeDistance = reader.ReadByte();
            ShadowNearClip = reader.ReadSingle();
            CoronaIntensity = reader.ReadSingle();
            CoronaZBias = reader.ReadSingle();
            Direction = reader.ReadVector3();
            Tangent = reader.ReadVector3();
            ConeInnerAngle = reader.ReadSingle();
            ConeOuterAngle = reader.ReadSingle();
            Extents = reader.ReadVector3();
            ProjectedTextureKey = new MetaHash(reader.ReadUInt32());
            _ = reader.ReadUInt32();
        }
        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            //write structure data
            writer.Write(this.VFT);
            writer.Write(this.Position);
            writer.Write(0u);
            writer.Write(this.ColorR);
            writer.Write(this.ColorG);
            writer.Write(this.ColorB);
            writer.Write(this.Flashiness);
            writer.Write(this.Intensity);
            writer.Write(this.Flags);
            writer.Write(this.BoneTag);
            writer.Write((byte)this.Type);
            writer.Write(this.GroupId);
            writer.Write(this.TimeFlags);
            writer.Write(this.Falloff);
            writer.Write(this.FalloffExponent);
            writer.Write(this.CullingPlaneNormal);
            writer.Write(this.CullingPlaneOffset);
            writer.Write(this.ShadowBlur);
            writer.Write(this.ExtraFlags);
            writer.Write((short)0);
            writer.Write(this.ExtraFlagsBank);
            writer.Write(this.VolumeIntensity);
            writer.Write(this.VolumeSizeScale);
            writer.Write(this.VolumeOuterColorR);
            writer.Write(this.VolumeOuterColorG);
            writer.Write(this.VolumeOuterColorB);
            writer.Write(this.LightHash);
            writer.Write(this.VolumeOuterIntensity);
            writer.Write(this.CoronaSize);
            writer.Write(this.VolumeOuterExponent);
            writer.Write(this.LightFadeDistance);
            writer.Write(this.ShadowFadeDistance);
            writer.Write(this.SpecularFadeDistance);
            writer.Write(this.VolumetricFadeDistance);
            writer.Write(this.ShadowNearClip);
            writer.Write(this.CoronaIntensity);
            writer.Write(this.CoronaZBias);
            writer.Write(this.Direction);
            writer.Write(this.Tangent);
            writer.Write(this.ConeInnerAngle);
            writer.Write(this.ConeOuterAngle);
            writer.Write(this.Extents);
            writer.Write(this.ProjectedTextureKey.Hash);
            writer.Write(0u);
        }

        public void WriteXml(StringBuilder sb, int indent)
        {
            YdrXml.SelfClosingTag(sb, indent, "Position " + FloatUtil.GetVector3XmlString(Position));
            YdrXml.SelfClosingTag(sb, indent, $"Colour r=\"{ColorR}\" g=\"{ColorG}\" b=\"{ColorB}\"");
            YdrXml.ValueTag(sb, indent, "Flashiness", Flashiness.ToString());
            YdrXml.ValueTag(sb, indent, "Intensity", FloatUtil.ToString(Intensity));
            YdrXml.ValueTag(sb, indent, "Flags", Flags.ToString());
            YdrXml.ValueTag(sb, indent, "BoneId", unchecked((ushort)BoneTag).ToString());
            YdrXml.StringTag(sb, indent, "Type", Type.ToString());
            YdrXml.ValueTag(sb, indent, "GroupId", GroupId.ToString());
            YdrXml.ValueTag(sb, indent, "TimeFlags", TimeFlags.ToString());
            YdrXml.ValueTag(sb, indent, "Falloff", FloatUtil.ToString(Falloff));
            YdrXml.ValueTag(sb, indent, "FalloffExponent", FloatUtil.ToString(FalloffExponent));
            YdrXml.SelfClosingTag(sb, indent, "CullingPlaneNormal " + FloatUtil.GetVector3XmlString(CullingPlaneNormal));
            YdrXml.ValueTag(sb, indent, "CullingPlaneOffset", FloatUtil.ToString(CullingPlaneOffset));
            YdrXml.ValueTag(sb, indent, "Unknown45", ExtraFlags.ToString());
            YdrXml.ValueTag(sb, indent, "Unknown48", ExtraFlagsBank.ToString());
            YdrXml.ValueTag(sb, indent, "VolumeIntensity", FloatUtil.ToString(VolumeIntensity));
            YdrXml.ValueTag(sb, indent, "VolumeSizeScale", FloatUtil.ToString(VolumeSizeScale));
            YdrXml.SelfClosingTag(sb, indent, $"VolumeOuterColour r=\"{VolumeOuterColorR}\" g=\"{VolumeOuterColorG}\" b=\"{VolumeOuterColorB}\"");
            YdrXml.ValueTag(sb, indent, "LightHash", LightHash.ToString());
            YdrXml.ValueTag(sb, indent, "VolumeOuterIntensity", FloatUtil.ToString(VolumeOuterIntensity));
            YdrXml.ValueTag(sb, indent, "CoronaSize", FloatUtil.ToString(CoronaSize));
            YdrXml.ValueTag(sb, indent, "VolumeOuterExponent", FloatUtil.ToString(VolumeOuterExponent));
            YdrXml.ValueTag(sb, indent, "LightFadeDistance", LightFadeDistance.ToString());
            YdrXml.ValueTag(sb, indent, "ShadowBlur", ShadowBlur.ToString());
            YdrXml.ValueTag(sb, indent, "ShadowFadeDistance", ShadowFadeDistance.ToString());
            YdrXml.ValueTag(sb, indent, "SpecularFadeDistance", SpecularFadeDistance.ToString());
            YdrXml.ValueTag(sb, indent, "VolumetricFadeDistance", VolumetricFadeDistance.ToString());
            YdrXml.ValueTag(sb, indent, "ShadowNearClip", FloatUtil.ToString(ShadowNearClip));
            YdrXml.ValueTag(sb, indent, "CoronaIntensity", FloatUtil.ToString(CoronaIntensity));
            YdrXml.ValueTag(sb, indent, "CoronaZBias", FloatUtil.ToString(CoronaZBias));
            YdrXml.SelfClosingTag(sb, indent, "Direction " + FloatUtil.GetVector3XmlString(Direction));
            YdrXml.SelfClosingTag(sb, indent, "Tangent " + FloatUtil.GetVector3XmlString(Tangent));
            YdrXml.ValueTag(sb, indent, "ConeInnerAngle", FloatUtil.ToString(ConeInnerAngle));
            YdrXml.ValueTag(sb, indent, "ConeOuterAngle", FloatUtil.ToString(ConeOuterAngle));
            YdrXml.SelfClosingTag(sb, indent, "Extent " + FloatUtil.GetVector3XmlString(Extents));
            YdrXml.StringTag(sb, indent, "ProjectedTextureHash", YdrXml.HashString(ProjectedTextureKey));
        }
        public void ReadXml(XmlNode node)
        {
            Position = Xml.GetChildVector3Attributes(node, "Position");
            ColorR = (byte)Xml.GetChildUIntAttribute(node, "Colour", "r");
            ColorG = (byte)Xml.GetChildUIntAttribute(node, "Colour", "g");
            ColorB = (byte)Xml.GetChildUIntAttribute(node, "Colour", "b");
            Flashiness = (byte)Xml.GetChildUIntAttribute(node, "Flashiness", "value");
            Intensity = Xml.GetChildFloatAttribute(node, "Intensity", "value");
            Flags = Xml.GetChildUIntAttribute(node, "Flags", "value");
            BoneTag = unchecked((short)Xml.GetChildUIntAttribute(node, "BoneId", "value"));
            Type = Xml.GetChildEnumInnerText<LightType>(node, "Type");
            GroupId = (byte)Xml.GetChildUIntAttribute(node, "GroupId", "value");
            TimeFlags = Xml.GetChildUIntAttribute(node, "TimeFlags", "value");
            Falloff = Xml.GetChildFloatAttribute(node, "Falloff", "value");
            FalloffExponent = Xml.GetChildFloatAttribute(node, "FalloffExponent", "value");
            CullingPlaneNormal = Xml.GetChildVector3Attributes(node, "CullingPlaneNormal");
            CullingPlaneOffset = Xml.GetChildFloatAttribute(node, "CullingPlaneOffset", "value");
            ExtraFlags = (byte)Xml.GetChildUIntAttribute(node, "Unknown45", "value");
            ExtraFlagsBank = Xml.GetChildUIntAttribute(node, "Unknown48", "value");
            VolumeIntensity = Xml.GetChildFloatAttribute(node, "VolumeIntensity", "value");
            VolumeSizeScale = Xml.GetChildFloatAttribute(node, "VolumeSizeScale", "value");
            VolumeOuterColorR = (byte)Xml.GetChildUIntAttribute(node, "VolumeOuterColour", "r");
            VolumeOuterColorG = (byte)Xml.GetChildUIntAttribute(node, "VolumeOuterColour", "g");
            VolumeOuterColorB = (byte)Xml.GetChildUIntAttribute(node, "VolumeOuterColour", "b");
            LightHash = (byte)Xml.GetChildUIntAttribute(node, "LightHash", "value");
            VolumeOuterIntensity = Xml.GetChildFloatAttribute(node, "VolumeOuterIntensity", "value");
            CoronaSize = Xml.GetChildFloatAttribute(node, "CoronaSize", "value");
            VolumeOuterExponent = Xml.GetChildFloatAttribute(node, "VolumeOuterExponent", "value");
            LightFadeDistance = (byte)Xml.GetChildUIntAttribute(node, "LightFadeDistance", "value");
            ShadowBlur = (byte)Xml.GetChildUIntAttribute(node, "ShadowBlur", "value");
            ShadowFadeDistance = (byte)Xml.GetChildUIntAttribute(node, "ShadowFadeDistance", "value");
            SpecularFadeDistance = (byte)Xml.GetChildUIntAttribute(node, "SpecularFadeDistance", "value");
            VolumetricFadeDistance = (byte)Xml.GetChildUIntAttribute(node, "VolumetricFadeDistance", "value");
            ShadowNearClip = Xml.GetChildFloatAttribute(node, "ShadowNearClip", "value");
            CoronaIntensity = Xml.GetChildFloatAttribute(node, "CoronaIntensity", "value");
            CoronaZBias = Xml.GetChildFloatAttribute(node, "CoronaZBias", "value");
            Direction = Xml.GetChildVector3Attributes(node, "Direction");
            Tangent = Xml.GetChildVector3Attributes(node, "Tangent");
            ConeInnerAngle = Xml.GetChildFloatAttribute(node, "ConeInnerAngle", "value");
            ConeOuterAngle = Xml.GetChildFloatAttribute(node, "ConeOuterAngle", "value");
            Extents = Xml.GetChildVector3Attributes(node, "Extent");
            ProjectedTextureKey = XmlMeta.GetHash(Xml.GetChildInnerText(node, "ProjectedTextureHash"));
        }

    }



    [TypeConverter(typeof(ExpandableObjectConverter))] public abstract class rmcDrawableBase : ResourceFileBase
    {
        public override long BlockLength => 24;

        public ulong ShaderGroupPointer { get; set; }
        public grmShaderGroup? ShaderGroup { get; set; }

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            base.Read(reader, parameters);
            ShaderGroupPointer = reader.ReadUInt64();
            ShaderGroup = reader.ReadBlockAt<grmShaderGroup>(ShaderGroupPointer);
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            base.Write(writer, parameters);
            ShaderGroupPointer = (ulong)(ShaderGroup?.FilePosition ?? 0);
            writer.Write(ShaderGroupPointer);
        }

        public override IResourceBlock[] GetReferences()
        {
            var list = new List<IResourceBlock>(base.GetReferences());
            if (ShaderGroup != null) list.Add(ShaderGroup);
            return list.ToArray();
        }
    }

    [TypeConverter(typeof(ExpandableObjectConverter))] public class rmcDrawable : rmcDrawableBase
    {
        public override long BlockLength
        {
            get { return 176; }
        }

        // structure data
        public ulong SkeletonDataPointer { get; set; }
        public Vector3 CullSphereCenter { get; set; }
        public float CullSphereRadius { get; set; }
        public Vector3 BoundingBoxMin { get; set; }
        public uint BoundingBoxUserData1 { get; set; } = 0x7f800001;
        public Vector3 BoundingBoxMax { get; set; }
        public uint BoundingBoxUserData2 { get; set; } = 0x7f800001;
        public ulong LodHighPointer { get; set; }
        public ulong LodMedPointer { get; set; }
        public ulong LodLowPointer { get; set; }
        public ulong LodVlowPointer { get; set; }
        public float LodThresholdHigh { get; set; }
        public float LodThresholdMed { get; set; }
        public float LodThresholdLow { get; set; }
        public float LodThresholdVlow { get; set; }
        public uint BucketMaskHigh { get; set; }
        public uint BucketMaskMed { get; set; }
        public uint BucketMaskLow { get; set; }
        public uint BucketMaskVlow { get; set; }
        public ulong JointDataPointer { get; set; }
        public ushort HandleIndex { get; set; } // rmcDrawable::m_HandleIndex
        public ushort ContainerSizeQW { get; set; }
        public ulong ContainerPointer { get; set; }
        public ulong DebugNamePointer { get; set; }

        public byte FlagsHigh
        {
            get { return (byte)(BucketMaskHigh & 0xFF); }
            set { BucketMaskHigh = (BucketMaskHigh & 0xFFFFFF00) + (value & 0xFFu); }
        }
        public byte FlagsMed
        {
            get { return (byte)(BucketMaskMed & 0xFF); }
            set { BucketMaskMed = (BucketMaskMed & 0xFFFFFF00) + (value & 0xFFu); }
        }
        public byte FlagsLow
        {
            get { return (byte)(BucketMaskLow & 0xFF); }
            set { BucketMaskLow = (BucketMaskLow & 0xFFFFFF00) + (value & 0xFFu); }
        }
        public byte FlagsVlow
        {
            get { return (byte)(BucketMaskVlow & 0xFF); }
            set { BucketMaskVlow = (BucketMaskVlow & 0xFFFFFF00) + (value & 0xFFu); }
        }
        public byte RenderMaskHigh
        {
            get { return (byte)((BucketMaskHigh >> 8) & 0xFF); }
            set { BucketMaskHigh = (BucketMaskHigh & 0xFFFF00FF) + ((value & 0xFFu) << 8); }
        }
        public byte RenderMaskMed
        {
            get { return (byte)((BucketMaskMed >> 8) & 0xFF); }
            set { BucketMaskMed = (BucketMaskMed & 0xFFFF00FF) + ((value & 0xFFu) << 8); }
        }
        public byte RenderMaskLow
        {
            get { return (byte)((BucketMaskLow >> 8) & 0xFF); }
            set { BucketMaskLow = (BucketMaskLow & 0xFFFF00FF) + ((value & 0xFFu) << 8); }
        }
        public byte RenderMaskVlow
        {
            get { return (byte)((BucketMaskVlow >> 8) & 0xFF); }
            set { BucketMaskVlow = (BucketMaskVlow & 0xFFFF00FF) + ((value & 0xFFu) << 8); }
        }


        // reference data
        public crSkeletonData? SkeletonData { get; set; }
        public crJointData? JointData { get; set; }
        public rmcLodContainer? DrawableModels { get; set; }
        public string DebugName { get; set; } = string.Empty;


        public grmModel[] AllModels { get; set; } = [];
        public Dictionary<ulong, grcFvf> VertexDecls { get; set; } = new();

        public object? Owner { get; set; }

        private string_r? DebugNameBlock;

        public long MemoryUsage
        {
            get
            {
                long val = 0;
                if (AllModels != null)
                {
                    foreach(grmModel m in AllModels)
                    {
                        if (m != null)
                        {
                            val += m.MemoryUsage;
                        }
                    }
                }
                if ((ShaderGroup != null) && (ShaderGroup.TextureDictionary != null))
                {
                    val += ShaderGroup.TextureDictionary.MemoryUsage;
                }
                return val;
            }
        }


        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            base.Read(reader, parameters);

            // read structure data
            this.SkeletonDataPointer = reader.ReadUInt64();
            this.CullSphereCenter = reader.ReadVector3();
            this.CullSphereRadius = reader.ReadSingle();
            this.BoundingBoxMin = reader.ReadVector3();
            this.BoundingBoxUserData1 = reader.ReadUInt32();
            this.BoundingBoxMax = reader.ReadVector3();
            this.BoundingBoxUserData2 = reader.ReadUInt32();
            this.LodHighPointer = reader.ReadUInt64();
            this.LodMedPointer = reader.ReadUInt64();
            this.LodLowPointer = reader.ReadUInt64();
            this.LodVlowPointer = reader.ReadUInt64();
            this.LodThresholdHigh = reader.ReadSingle();
            this.LodThresholdMed = reader.ReadSingle();
            this.LodThresholdLow = reader.ReadSingle();
            this.LodThresholdVlow = reader.ReadSingle();
            this.BucketMaskHigh = reader.ReadUInt32();
            this.BucketMaskMed = reader.ReadUInt32();
            this.BucketMaskLow = reader.ReadUInt32();
            this.BucketMaskVlow = reader.ReadUInt32();
            this.JointDataPointer = reader.ReadUInt64();
            this.HandleIndex = reader.ReadUInt16();
            this.ContainerSizeQW = reader.ReadUInt16();
            _ = reader.ReadUInt32();
            this.ContainerPointer = reader.ReadUInt64();
            this.DebugNamePointer = reader.ReadUInt64();

            // read reference data
            this.SkeletonData = reader.ReadBlockAt<crSkeletonData>(this.SkeletonDataPointer);
            this.JointData = reader.ReadBlockAt<crJointData>(this.JointDataPointer);
            this.DrawableModels = reader.ReadBlockAt<rmcLodContainer>((ContainerPointer == 0) ? LodHighPointer : ContainerPointer, this);
            this.DebugName = reader.ReadStringAt(this.DebugNamePointer) ?? string.Empty;


            BuildAllModels();
            BuildVertexDecls();
            AssignGeometryShaders(ShaderGroup);
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            base.Write(writer, parameters);

            // update structure data
            this.SkeletonDataPointer = (ulong)(this.SkeletonData?.FilePosition ?? 0);
            this.LodHighPointer = (ulong)(DrawableModels?.GetHighPointer() ?? 0);
            this.LodMedPointer = (ulong)(DrawableModels?.GetMedPointer() ?? 0);
            this.LodLowPointer = (ulong)(DrawableModels?.GetLowPointer() ?? 0);
            this.LodVlowPointer = (ulong)(DrawableModels?.GetVLowPointer() ?? 0);
            this.JointDataPointer = (ulong)(this.JointData != null ? this.JointData.FilePosition : 0);
            this.ContainerPointer = (ulong)(DrawableModels?.FilePosition ?? 0);
            this.ContainerSizeQW = (ushort)Math.Ceiling((DrawableModels?.BlockLength ?? 0) / 16.0);
            this.DebugNamePointer = (ulong)(this.DebugNameBlock?.FilePosition ?? 0);

            // write structure data
            writer.Write(this.SkeletonDataPointer);
            writer.Write(this.CullSphereCenter);
            writer.Write(this.CullSphereRadius);
            writer.Write(this.BoundingBoxMin);
            writer.Write(this.BoundingBoxUserData1);
            writer.Write(this.BoundingBoxMax);
            writer.Write(this.BoundingBoxUserData2);
            writer.Write(this.LodHighPointer);
            writer.Write(this.LodMedPointer);
            writer.Write(this.LodLowPointer);
            writer.Write(this.LodVlowPointer);
            writer.Write(this.LodThresholdHigh);
            writer.Write(this.LodThresholdMed);
            writer.Write(this.LodThresholdLow);
            writer.Write(this.LodThresholdVlow);
            writer.Write(this.BucketMaskHigh);
            writer.Write(this.BucketMaskMed);
            writer.Write(this.BucketMaskLow);
            writer.Write(this.BucketMaskVlow);
            writer.Write(this.JointDataPointer);
            writer.Write(this.HandleIndex);
            writer.Write(this.ContainerSizeQW);
            writer.Write(0u);
            writer.Write(this.ContainerPointer);
            writer.Write(this.DebugNamePointer);
        }
        public virtual void WriteXml(StringBuilder sb, int indent, string ddsfolder)
        {
            YdrXml.SelfClosingTag(sb, indent, "BoundingSphereCenter " + FloatUtil.GetVector3XmlString(CullSphereCenter));
            YdrXml.ValueTag(sb, indent, "BoundingSphereRadius", FloatUtil.ToString(CullSphereRadius));
            YdrXml.SelfClosingTag(sb, indent, "BoundingBoxMin " + FloatUtil.GetVector3XmlString(BoundingBoxMin));
            YdrXml.SelfClosingTag(sb, indent, "BoundingBoxMax " + FloatUtil.GetVector3XmlString(BoundingBoxMax));
            YdrXml.ValueTag(sb, indent, "LodDistHigh", FloatUtil.ToString(LodThresholdHigh));
            YdrXml.ValueTag(sb, indent, "LodDistMed", FloatUtil.ToString(LodThresholdMed));
            YdrXml.ValueTag(sb, indent, "LodDistLow", FloatUtil.ToString(LodThresholdLow));
            YdrXml.ValueTag(sb, indent, "LodDistVlow", FloatUtil.ToString(LodThresholdVlow));
            YdrXml.ValueTag(sb, indent, "FlagsHigh", FlagsHigh.ToString());
            YdrXml.ValueTag(sb, indent, "FlagsMed", FlagsMed.ToString());
            YdrXml.ValueTag(sb, indent, "FlagsLow", FlagsLow.ToString());
            YdrXml.ValueTag(sb, indent, "FlagsVlow", FlagsVlow.ToString());
            if (ShaderGroup != null)
            {
                YdrXml.OpenTag(sb, indent, "ShaderGroup");
                ShaderGroup.WriteXml(sb, indent + 1, ddsfolder);
                YdrXml.CloseTag(sb, indent, "ShaderGroup");
            }
            if (SkeletonData != null)
            {
                YdrXml.OpenTag(sb, indent, "Skeleton");
                SkeletonData.WriteXml(sb, indent + 1);
                YdrXml.CloseTag(sb, indent, "Skeleton");
            }
            if (JointData != null)
            {
                YdrXml.OpenTag(sb, indent, "Joints");
                JointData.WriteXml(sb, indent + 1);
                YdrXml.CloseTag(sb, indent, "Joints");
            }
            if (DrawableModels?.High != null)
            {
                YdrXml.WriteItemArray(sb, DrawableModels.High, indent, "DrawableModelsHigh");
            }
            if (DrawableModels?.Med != null)
            {
                YdrXml.WriteItemArray(sb, DrawableModels.Med, indent, "DrawableModelsMedium");
            }
            if (DrawableModels?.Low != null)
            {
                YdrXml.WriteItemArray(sb, DrawableModels.Low, indent, "DrawableModelsLow");
            }
            if (DrawableModels?.VLow != null)
            {
                YdrXml.WriteItemArray(sb, DrawableModels.VLow, indent, "DrawableModelsVeryLow");
            }
        }
        public virtual void ReadXml(XmlNode node, string ddsfolder)
        {
            CullSphereCenter = Xml.GetChildVector3Attributes(node, "BoundingSphereCenter");
            CullSphereRadius = Xml.GetChildFloatAttribute(node, "BoundingSphereRadius", "value");
            BoundingBoxMin = Xml.GetChildVector3Attributes(node, "BoundingBoxMin");
            BoundingBoxMax = Xml.GetChildVector3Attributes(node, "BoundingBoxMax");
            LodThresholdHigh = Xml.GetChildFloatAttribute(node, "LodDistHigh", "value");
            LodThresholdMed = Xml.GetChildFloatAttribute(node, "LodDistMed", "value");
            LodThresholdLow = Xml.GetChildFloatAttribute(node, "LodDistLow", "value");
            LodThresholdVlow = Xml.GetChildFloatAttribute(node, "LodDistVlow", "value");
            FlagsHigh = (byte)Xml.GetChildUIntAttribute(node, "FlagsHigh", "value");
            FlagsMed = (byte)Xml.GetChildUIntAttribute(node, "FlagsMed", "value");
            FlagsLow = (byte)Xml.GetChildUIntAttribute(node, "FlagsLow", "value");
            FlagsVlow = (byte)Xml.GetChildUIntAttribute(node, "FlagsVlow", "value");
            var sgnode = node.SelectSingleNode("ShaderGroup");
            if (sgnode != null)
            {
                ShaderGroup = new grmShaderGroup();
                ShaderGroup.ReadXml(sgnode, ddsfolder);
            }
            var sknode = node.SelectSingleNode("Skeleton");
            if (sknode != null)
            {
                SkeletonData = new crSkeletonData();
                SkeletonData.ReadXml(sknode);
            }
            var jnode = node.SelectSingleNode("Joints");
            if (jnode != null)
            {
                JointData = new crJointData();
                JointData.ReadXml(jnode);
            }
            this.DrawableModels = new rmcLodContainer();
            this.DrawableModels.High = XmlMeta.ReadItemArray<grmModel>(node, "DrawableModelsHigh");
            this.DrawableModels.Med = XmlMeta.ReadItemArray<grmModel>(node, "DrawableModelsMedium");
            this.DrawableModels.Low = XmlMeta.ReadItemArray<grmModel>(node, "DrawableModelsLow");
            this.DrawableModels.VLow = XmlMeta.ReadItemArray<grmModel>(node, "DrawableModelsVeryLow");
            if (DrawableModels.BlockLength == 0)
            {
                DrawableModels = null;
            }

            BuildRenderMasks();
            BuildAllModels();
            BuildVertexDecls();

            FileVFT = 1079456120;
        }

        public override IResourceBlock[] GetReferences()
        {
            var list = new List<IResourceBlock>(base.GetReferences());
            if (SkeletonData != null) list.Add(SkeletonData);
            if (JointData != null) list.Add(JointData);
            if (DrawableModels != null) list.Add(DrawableModels);
            if (!string.IsNullOrEmpty(DebugName))
            {
                DebugNameBlock = (string_r)DebugName;
                list.Add(DebugNameBlock);
            }
            else
            {
                DebugNameBlock = null;
            }
            return list.ToArray();
        }


        public void AssignGeometryShaders(grmShaderGroup? shaderGrp)
        {
            //map the shaders to the geometries
            if (shaderGrp?.Shaders?.data_items != null)
            {
                var shaders = shaderGrp.Shaders.data_items;
                foreach (grmModel model in AllModels)
                {
                    if (model?.Geometries == null) continue;

                    int geomcount = model.Geometries.Length;
                    for (int i = 0; i < geomcount; i++)
                    {
                        var geom = model.Geometries[i];
                        var sid = geom.ShaderID;
                        geom.Shader = (sid < shaders.Length) ? shaders[sid] : null;
                    }
                }
            }
            else
            {
            }

        }



        public void BuildAllModels()
        {
            var allModels = new List<grmModel>();
            if (DrawableModels?.High != null) allModels.AddRange(DrawableModels.High);
            if (DrawableModels?.Med != null) allModels.AddRange(DrawableModels.Med);
            if (DrawableModels?.Low != null) allModels.AddRange(DrawableModels.Low);
            if (DrawableModels?.VLow != null) allModels.AddRange(DrawableModels.VLow);
            AllModels = allModels.ToArray();
        }

        public void BuildVertexDecls()
        {
            var vds = new Dictionary<ulong, grcFvf>();
            foreach (grmModel model in AllModels)
            {
                if (model.Geometries == null) continue;
                foreach (var geom in model.Geometries)
                {
                    var info = geom.VertexBuffer?.VertexFormat;
                    if (info == null) continue;
                    var declid = info.GetDeclarationId();

                    if (!vds.ContainsKey(declid))
                    {
                        vds.Add(declid, info);
                    }
                }
            }
            VertexDecls = new Dictionary<ulong, grcFvf>(vds);
        }


        public void BuildRenderMasks()
        {
            BucketMaskHigh = BuildBucketMask(DrawableModels?.High);
            BucketMaskMed = BuildBucketMask(DrawableModels?.Med);
            BucketMaskLow = BuildBucketMask(DrawableModels?.Low);
            BucketMaskVlow = BuildBucketMask(DrawableModels?.VLow);

        }
        private uint BuildBucketMask(grmModel[]? models)
        {
            var shaders = ShaderGroup?.Shaders?.data_items;
            if ((models == null) || (shaders == null)) return 0;

            uint lodMask = 0;
            foreach (var model in models)
            {
                uint modelMask = 0;
                foreach (var shaderIndex in model.ShaderIndices)
                {
                    if ((shaderIndex < shaders.Length) && (shaders[shaderIndex] != null))
                    {
                        modelMask |= shaders[shaderIndex].DrawBucketMask;
                    }
                }

                if ((modelMask & 0xFF00) == 0xFF00)
                {
                    modelMask = (modelMask & 0xFF) | ((uint)model.Mask << 8);
                }
                lodMask |= modelMask;
            }
            return lodMask;
        }


        public rmcDrawable? ShallowCopy()
        {
            rmcDrawable? r = null;
            if (this is FragDrawable fd)
            {
                var f = new FragDrawable();
                f.FragMatrix = fd.FragMatrix;
                f.FragMatricesIndsCount = fd.FragMatricesIndsCount;
                f.FragMatricesCapacity = fd.FragMatricesCapacity;
                f.FragMatricesCount = fd.FragMatricesCount;
                f.Bound = fd.Bound;
                f.FragMatricesInds = fd.FragMatricesInds;
                f.FragMatrices = fd.FragMatrices;
                f.Name = fd.Name;
                f.OwnerFragment = fd.OwnerFragment;
                f.OwnerFragmentPhys = fd.OwnerFragmentPhys;
                r = f;
            }
            if (this is gtaDrawable dd)
            {
                var d = new gtaDrawable();
                d.Lights = dd.Lights;
                d.TintData = dd.TintData;
                d.PhBound = dd.PhBound;
                r = d;
            }
            if (r != null)
            {
                r.CullSphereCenter = CullSphereCenter;
                r.CullSphereRadius = CullSphereRadius;
                r.BoundingBoxMin = BoundingBoxMin;
                r.BoundingBoxMax = BoundingBoxMax;
                r.LodThresholdHigh = LodThresholdHigh;
                r.LodThresholdMed = LodThresholdMed;
                r.LodThresholdLow = LodThresholdLow;
                r.LodThresholdVlow = LodThresholdVlow;
                r.BucketMaskHigh = BucketMaskHigh;
                r.BucketMaskMed = BucketMaskMed;
                r.BucketMaskLow = BucketMaskLow;
                r.BucketMaskVlow = BucketMaskVlow;
                r.HandleIndex = HandleIndex;
                r.ContainerSizeQW = ContainerSizeQW;
                r.DebugName = DebugName;
                r.ShaderGroup = ShaderGroup;
                r.SkeletonData = SkeletonData?.Clone();
                r.DrawableModels = new rmcLodContainer();
                r.DrawableModels.High = DrawableModels?.High ?? [];
                r.DrawableModels.Med = DrawableModels?.Med ?? [];
                r.DrawableModels.Low = DrawableModels?.Low ?? [];
                r.DrawableModels.VLow = DrawableModels?.VLow ?? [];
                r.JointData = JointData;
                r.AllModels = AllModels;
                r.VertexDecls = VertexDecls;
                r.Owner = Owner;
            }
            return r;
        }



        public void EnsureGen9()
        {
            FileVFT = 1079456120;
            FileUnknown = 1;
            BoundingBoxUserData1 = 0x7f800001;
            BoundingBoxUserData2 = 0x7f800001;

            if (SkeletonData != null)
            {
                SkeletonData.VFT = 1080114336;
            }
            if (JointData != null)
            {
                JointData.VFT = 1080130656;
            }

            if (ShaderGroup != null)
            {
                ShaderGroup.VFT = 1080113136;
                ShaderGroup.TextureDictionary?.EnsureGen9();

                var shaders = ShaderGroup.Shaders?.data_items;
                if (shaders != null)
                {
                    foreach (var shader in shaders)
                    {
                        shader?.EnsureGen9();
                    }
                }
            }

            if (AllModels != null)
            {
                foreach (var model in AllModels)
                {
                    if (model == null) continue;
                    model.VFT = 1080101528;
                    var geoms = model.Geometries;
                    if (geoms == null) continue;
                    foreach (var geom in geoms)
                    {
                        if (geom == null) continue;
                        geom.VFT = 1080133528;
                        geom.VertexBuffer?.EnsureGen9();
                        geom.IndexBuffer?.EnsureGen9();
                    }
                }
            }

            if ((this is gtaDrawable dwbl) && (dwbl.Lights?.data_items != null))
            {
                foreach (var light in dwbl.Lights.data_items)
                {
                    light.VFT = 0;
                }
            }
        }
    }

    [TypeConverter(typeof(ExpandableObjectConverter))] public class gtaDrawable : rmcDrawable
    {
        public override long BlockLength
        {
            get { return 208; }
        }

        // structure data
        public atArray<CLightAttr> Lights { get; set; } = new();
        public ulong TintDataPointer { get; set; }
        public ulong PhBoundPointer { get; set; }

        // reference data
        public ResourceSimpleList64_byte? TintData { get; set; }
        public Bounds? PhBound { get; set; }

        public string? ErrorMessage { get; set; }


#if DEBUG
        public ResourceAnalyzer? Analyzer { get; set; }
#endif


        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            base.Read(reader, parameters);

            // read structure data
            this.Lights = reader.ReadRequiredBlock<atArray<CLightAttr>>();
            this.TintDataPointer = reader.ReadUInt64();
            this.PhBoundPointer = reader.ReadUInt64();

            try
            {

                // read reference data
                this.TintData = reader.ReadBlockAt<ResourceSimpleList64_byte>(this.TintDataPointer);
                this.PhBound = reader.ReadBlockAt<Bounds>(this.PhBoundPointer);
                if (PhBound != null)
                {
                    PhBound.Owner = this;
                }

            }
            catch (Exception ex) 
            {
                ErrorMessage = ex.ToString();
            }

#if DEBUG
            Analyzer = new ResourceAnalyzer(reader);
#endif

        }
        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            base.Write(writer, parameters);

            // update structure data
            this.TintDataPointer = (ulong)(this.TintData?.FilePosition ?? 0);
            this.PhBoundPointer = (ulong)(this.PhBound?.FilePosition ?? 0);

            // write structure data
            writer.WriteBlock(this.Lights);
            writer.Write(this.TintDataPointer);
            writer.Write(this.PhBoundPointer);
        }
        public override void WriteXml(StringBuilder sb, int indent, string ddsfolder)
        {
            YdrXml.StringTag(sb, indent, "Name", YdrXml.XmlEscape(DebugName));
            base.WriteXml(sb, indent, ddsfolder);
            if (PhBound != null)
            {
                Bounds.WriteXmlNode(PhBound, sb, indent);
            }
            if (Lights?.data_items != null)
            {
                YdrXml.WriteItemArray(sb, Lights.data_items, indent, "Lights");
            }
        }
        public override void ReadXml(XmlNode node, string ddsfolder)
        {
            DebugName = Xml.GetChildInnerText(node, "Name") ?? string.Empty;
            base.ReadXml(node, ddsfolder);
            var bnode = node.SelectSingleNode("Bounds");
            if (bnode != null)
            {
                PhBound = Bounds.ReadXmlNode(bnode, this);
            }

            Lights = new atArray<CLightAttr>();
            Lights.data_items = XmlMeta.ReadItemArray<CLightAttr>(node, "Lights");

        }
        public static void WriteXmlNode(gtaDrawable? d, StringBuilder sb, int indent, string ddsfolder, string name = "Drawable")
        {
            if (d == null) return;
            YdrXml.OpenTag(sb, indent, name);
            d.WriteXml(sb, indent + 1, ddsfolder);
            YdrXml.CloseTag(sb, indent, name);
        }
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(node))]
        public static gtaDrawable? ReadXmlNode(XmlNode? node, string ddsfolder)
        {
            if (node == null) return null;
            var d = new gtaDrawable();
            d.ReadXml(node, ddsfolder);
            return d;
        }


        public override IResourceBlock[] GetReferences()
        {
            var list = new List<IResourceBlock>(base.GetReferences());
            if (TintData != null) list.Add(TintData);
            if (PhBound != null) list.Add(PhBound);
            return list.ToArray();
        }
        public override Tuple<long, IResourceBlock>[] GetParts()
        {
            return new Tuple<long, IResourceBlock>[] {
                new Tuple<long, IResourceBlock>(0xB0, Lights),
            };
        }


        public override string ToString()
        {
            return DebugName;
        }
    }

    [TypeConverter(typeof(ExpandableObjectConverter))] public class DrawablePtfx : rmcDrawable
    {
        public override long BlockLength
        {
            get { return 176; }
        }

        public override void WriteXml(StringBuilder sb, int indent, string ddsfolder)
        {
            base.WriteXml(sb, indent, ddsfolder);
        }
        public override void ReadXml(XmlNode node, string ddsfolder)
        {
            base.ReadXml(node, ddsfolder);
        }
        public static void WriteXmlNode(DrawablePtfx? d, StringBuilder sb, int indent, string ddsfolder, string name = "Drawable")
        {
            if (d == null) return;
            YdrXml.OpenTag(sb, indent, name);
            d.WriteXml(sb, indent + 1, ddsfolder);
            YdrXml.CloseTag(sb, indent, name);
        }
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(node))]
        public static DrawablePtfx? ReadXmlNode(XmlNode? node, string ddsfolder)
        {
            if (node == null) return null;
            var d = new DrawablePtfx();
            d.ReadXml(node, ddsfolder);
            return d;
        }

    }

    [TypeConverter(typeof(ExpandableObjectConverter))] public class DrawablePtfxDictionary : ResourceFileBase
    {
        public override long BlockLength
        {
            get { return 64; }
        }

        // structure data
        public ulong Unknown_10h; // 0x0000000000000000
        public ulong Unknown_18h = 1; // 0x0000000000000001
        public ulong HashesPointer { get; set; }
        public ushort HashesCount1 { get; set; }
        public ushort HashesCount2 { get; set; }
        public uint Unknown_2Ch { get; set; }
        public ulong DrawablesPointer { get; set; }
        public ushort DrawablesCount1 { get; set; }
        public ushort DrawablesCount2 { get; set; }
        public uint Unknown_3Ch { get; set; }

        // reference data
        //public ResourceSimpleArray<uint_r> Hashes { get; set; }
        public uint[] Hashes { get; set; } = [];
        public ResourcePointerArray64<DrawablePtfx>? Drawables { get; set; }


        private ResourceSystemStructBlock<uint>? HashesBlock = null;//only used for saving


        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            base.Read(reader, parameters);

            // read structure data
            this.Unknown_10h = reader.ReadUInt64();
            this.Unknown_18h = reader.ReadUInt64();
            this.HashesPointer = reader.ReadUInt64();
            this.HashesCount1 = reader.ReadUInt16();
            this.HashesCount2 = reader.ReadUInt16();
            this.Unknown_2Ch = reader.ReadUInt32();
            this.DrawablesPointer = reader.ReadUInt64();
            this.DrawablesCount1 = reader.ReadUInt16();
            this.DrawablesCount2 = reader.ReadUInt16();
            this.Unknown_3Ch = reader.ReadUInt32();

            // read reference data
            this.Hashes = reader.ReadUintsAt(this.HashesPointer, this.HashesCount1) ?? [];
            this.Drawables = reader.ReadBlockAt<ResourcePointerArray64<DrawablePtfx>>(this.DrawablesPointer, this.DrawablesCount1);

            //if (Unknown_10h != 0)
            //{ }
            //if (Unknown_18h != 1)
            //{ }
            //if (Unknown_2Ch != 0)
            //{ }
            //if (Unknown_3Ch != 0)
            //{ }
        }
        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            base.Write(writer, parameters);

            // update structure data
            this.HashesPointer = (ulong)(this.HashesBlock != null ? this.HashesBlock.FilePosition : 0);
            this.HashesCount1 = (ushort)(this.HashesBlock != null ? this.HashesBlock.ItemCount : 0);
            this.HashesCount2 = (ushort)(this.HashesBlock != null ? this.HashesBlock.ItemCount : 0);
            this.DrawablesPointer = (ulong)(this.Drawables != null ? this.Drawables.FilePosition : 0);
            this.DrawablesCount1 = (ushort)(this.Drawables != null ? this.Drawables.Count : 0);
            this.DrawablesCount2 = (ushort)(this.Drawables != null ? this.Drawables.Count : 0);

            // write structure data
            writer.Write(this.Unknown_10h);
            writer.Write(this.Unknown_18h);
            writer.Write(this.HashesPointer);
            writer.Write(this.HashesCount1);
            writer.Write(this.HashesCount2);
            writer.Write(this.Unknown_2Ch);
            writer.Write(this.DrawablesPointer);
            writer.Write(this.DrawablesCount1);
            writer.Write(this.DrawablesCount2);
            writer.Write(this.Unknown_3Ch);
        }
        public void WriteXml(StringBuilder sb, int indent, string ddsfolder)
        {
            if (Drawables?.data_items != null)
            {
                for (int i=0; i< Drawables.data_items.Length; i++)
                {
                    var d = Drawables.data_items[i];
                    var h = (MetaHash)((i < (Hashes.Length)) ? Hashes[i] : 0);
                    YddXml.OpenTag(sb, indent, "Item");
                    YddXml.StringTag(sb, indent + 1, "Name", YddXml.XmlEscape(h.ToCleanString()));
                    d.WriteXml(sb, indent + 1, ddsfolder);
                    YddXml.CloseTag(sb, indent, "Item");
                }
            }
        }
        public void ReadXml(XmlNode node, string ddsfolder)
        {
            var drawables = new List<DrawablePtfx>();
            var drawablehashes = new List<uint>();

            var inodes = node.SelectNodes("Item")?.Cast<XmlNode>().ToArray() ?? [];
            if (inodes != null)
            {
                foreach (XmlNode inode in inodes)
                {
                    var h = XmlMeta.GetHash(Xml.GetChildInnerText(inode, "Name"));
                    var d = new DrawablePtfx();
                    d.ReadXml(inode, ddsfolder);
                    drawables.Add(d);
                    drawablehashes.Add(h);
                }
            }
            if (drawables.Count > 0)
            {
                Hashes = drawablehashes.ToArray();
                Drawables = new ResourcePointerArray64<DrawablePtfx>();
                Drawables.data_items = drawables.ToArray();
            }
        }
        public static void WriteXmlNode(DrawablePtfxDictionary? d, StringBuilder sb, int indent, string ddsfolder, string name = "DrawableDictionary")
        {
            if (d == null) return;
            YddXml.OpenTag(sb, indent, name);
            d.WriteXml(sb, indent + 1, ddsfolder);
            YddXml.CloseTag(sb, indent, name);
        }
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(node))]
        public static DrawablePtfxDictionary? ReadXmlNode(XmlNode? node, string ddsfolder)
        {
            if (node == null) return null;
            var d = new DrawablePtfxDictionary();
            d.ReadXml(node, ddsfolder);
            return d;
        }

        public override IResourceBlock[] GetReferences()
        {
            var list = new List<IResourceBlock>(base.GetReferences());
            if (Hashes != null)
            {
                HashesBlock = new ResourceSystemStructBlock<uint>(Hashes);
                list.Add(HashesBlock);
            }
            if (Drawables != null) list.Add(Drawables);
            return list.ToArray();
        }
    }

    [TypeConverter(typeof(ExpandableObjectConverter))] public class DrawableDictionary : ResourceFileBase
    {
        // pgDictionary<gtaDrawable>
        public override long BlockLength => 0x40;
        public uint ReferenceCount { get; set; } = 1;
        public ResourceSimpleList64_s<uint> Codes { get; set; } = new();
        public ResourcePointerList64<gtaDrawable> Entries { get; set; } = new();

        [Browsable(false)] public uint[] Hashes
        {
            get => Codes.data_items;
            set => Codes.data_items = value ?? [];
        }
        [Browsable(false)] public ResourcePointerList64<gtaDrawable> Drawables
        {
            get => Entries;
            set => Entries = value ?? new();
        }


        public long MemoryUsage
        {
            get
            {
                long val = 0;
                if (Entries?.data_items != null)
                {
                    foreach(var drawable in Entries.data_items)
                    {
                        if (drawable != null) val += drawable.MemoryUsage;
                    }
                }
                return val;
            }
        }

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            base.Read(reader, parameters);
            _ = reader.ReadUInt64(); // m_Parent is ignored in resources.
            ReferenceCount = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            Codes = reader.ReadRequiredBlock<ResourceSimpleList64_s<uint>>();
            Entries = reader.ReadRequiredBlock<ResourcePointerList64<gtaDrawable>>();
            ValidateCounts();
        }
        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            ValidateCounts();
            base.Write(writer, parameters);
            writer.Write(0ul);
            writer.Write(ReferenceCount);
            writer.Write(0u);
            writer.WriteBlock(Codes);
            writer.WriteBlock(Entries);
        }
        public void WriteXml(StringBuilder sb, int indent, string ddsfolder)
        {
            if (Entries?.data_items != null)
            {
                foreach (var d in Entries.data_items)
                {
                    if (d == null) continue;
                    YddXml.OpenTag(sb, indent, "Item");
                    d.WriteXml(sb, indent + 1, ddsfolder);
                    YddXml.CloseTag(sb, indent, "Item");
                }
            }
        }
        public void ReadXml(XmlNode node, string ddsfolder)
        {
            var items = new List<(uint Code, gtaDrawable Drawable)>();

            var inodes = node.SelectNodes("Item")?.Cast<XmlNode>().ToArray() ?? [];
            if (inodes != null)
            {
                foreach (XmlNode inode in inodes)
                {
                    var d = new gtaDrawable();
                    d.ReadXml(inode, ddsfolder);
                    items.Add((XmlMeta.GetHash(d.DebugName), d));
                }
            }

            items.Sort((a, b) => a.Code.CompareTo(b.Code));
            Codes = new ResourceSimpleList64_s<uint> { data_items = items.Select(x => x.Code).ToArray() };
            Entries = new ResourcePointerList64<gtaDrawable> { data_items = items.Select(x => x.Drawable).ToArray() };
        }
        public static void WriteXmlNode(DrawableDictionary? d, StringBuilder sb, int indent, string ddsfolder, string name = "DrawableDictionary")
        {
            if (d == null) return;
            YddXml.OpenTag(sb, indent, name);
            d.WriteXml(sb, indent + 1, ddsfolder);
            YddXml.CloseTag(sb, indent, name);
        }
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(node))]
        public static DrawableDictionary? ReadXmlNode(XmlNode? node, string ddsfolder)
        {
            if (node == null) return null;
            var d = new DrawableDictionary();
            d.ReadXml(node, ddsfolder);
            return d;
        }

        public override IResourceBlock[] GetReferences()
        {
            return base.GetReferences();
        }
        public override Tuple<long, IResourceBlock>[] GetParts()
        {
            return
            [
                new Tuple<long, IResourceBlock>(0x20, Codes),
                new Tuple<long, IResourceBlock>(0x30, Entries),
            ];
        }

        private void ValidateCounts()
        {
            if ((Codes?.data_items?.Length ?? 0) != (Entries?.data_items?.Length ?? 0))
                throw new InvalidDataException("Drawable dictionary code and entry counts do not match.");
        }
    }


}
