using SharpDX;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TC = System.ComponentModel.TypeConverterAttribute;
using EXP = System.ComponentModel.ExpandableObjectConverter;
using System.Xml;

/*
    Copyright(c) 2017 Neodymium
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


//ruthlessly stolen


namespace CodeWalker.GameFiles
{

    [Flags]
    public enum ExpressionFlags : ushort
    {
        None = 0,
        Optimized = 1 << 0,
        Packed = 1 << 1,
    }

    [TC(typeof(EXP))] public class ExpressionDictionary : ResourceFileBase
    {
        // pgDictionary<crExpressions> : pgDictionaryBase
        public override long BlockLength => 0x40;
        public uint ReferenceCount { get; set; } = 1;
        public ResourceSimpleList64_s<MetaHash> ExpressionNameHashes { get; set; } = new();
        public ResourcePointerList64<Expression> Expressions { get; set; } = new();

        public Dictionary<MetaHash, Expression> ExprMap { get; set; } = new();


        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            base.Read(reader, parameters);
            _ = reader.ReadUInt64(); // pgDictionary::m_Parent is ignored in resources.
            ReferenceCount = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            ExpressionNameHashes = reader.ReadRequiredBlock<ResourceSimpleList64_s<MetaHash>>();
            Expressions = reader.ReadRequiredBlock<ResourcePointerList64<Expression>>();
            BuildMap();
        }
        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            base.Write(writer, parameters);
            writer.Write(0ul);
            writer.Write(ReferenceCount);
            writer.Write(0u);
            writer.WriteBlock(ExpressionNameHashes);
            writer.WriteBlock(Expressions);
        }
        public void WriteXml(StringBuilder sb, int indent)
        {
            if (Expressions?.data_items != null)
            {
                foreach (var e in Expressions.data_items)
                {
                    YedXml.OpenTag(sb, indent, "Item");
                    e.WriteXml(sb, indent + 1);
                    YedXml.CloseTag(sb, indent, "Item");
                }
            }
        }
        public void ReadXml(XmlNode node)
        {
            var expressions = new List<Expression>();
            var expressionhashes = new List<MetaHash>();

            var inodes = node.SelectNodes("Item");
            if (inodes != null)
            {
                foreach (XmlNode inode in inodes)
                {
                    var e = new Expression();
                    e.ReadXml(inode);
                    expressions.Add(e);
                }

                //expressions in the file should be sorted by hash
                expressions.Sort((a, b) => a.NameHash.Hash.CompareTo(b.NameHash.Hash));
                foreach (var e in expressions)
                {
                    expressionhashes.Add(e.NameHash);
                }
            }

            ExpressionNameHashes = new ResourceSimpleList64_s<MetaHash>();
            ExpressionNameHashes.data_items = expressionhashes.ToArray();
            Expressions = new ResourcePointerList64<Expression>();
            Expressions.data_items = expressions.ToArray();
            
            BuildMap();
        }
        public static void WriteXmlNode(ExpressionDictionary? d, StringBuilder sb, int indent, string name = "ExpressionDictionary")
        {
            if (d == null) return;
            if ((d.Expressions?.data_items == null) || (d.Expressions.data_items.Length == 0))
            {
                YedXml.SelfClosingTag(sb, indent, name);
            }
            else
            {
                YedXml.OpenTag(sb, indent, name);
                d.WriteXml(sb, indent + 1);
                YedXml.CloseTag(sb, indent, name);
            }
        }
        [return: NotNullIfNotNull(nameof(node))]
        public static ExpressionDictionary? ReadXmlNode(XmlNode? node)
        {
            if (node == null) return null;
            var ed = new ExpressionDictionary();
            ed.ReadXml(node);
            return ed;
        }


        public override IResourceBlock[] GetReferences()
        {
            var list = new List<IResourceBlock>(base.GetReferences());
            return list.ToArray();
        }
        public override Tuple<long, IResourceBlock>[] GetParts()
        {
            return new Tuple<long, IResourceBlock>[] {
                new Tuple<long, IResourceBlock>(0x20, ExpressionNameHashes),
                new Tuple<long, IResourceBlock>(0x30, Expressions)
            };
        }


        public void BuildMap()
        {
            ExprMap = new Dictionary<MetaHash, Expression>();

            if ((Expressions?.data_items != null) && (ExpressionNameHashes?.data_items != null))
            {
                var exprs = Expressions.data_items;
                var names = ExpressionNameHashes.data_items;

                for (int i = 0; i < exprs.Length; i++)
                {
                    var expr = exprs[i];
                    if (expr == null) continue;
                    var name = (i < names.Length) ? names[i] : (MetaHash)JenkHash.GenHash(expr.GetShortName() ?? "");
                    expr.NameHash = name;
                    ExprMap[name] = expr;
                }
            }

        }

    }



    [TC(typeof(EXP))] public class Expression : ResourceSystemBlock
    {
        // crExpressions : pgBase
        public override long BlockLength => 0x90;
        public uint VFT { get; set; }
        public uint BaseReferenceCount { get; set; } = 1;
        public ResourcePointerList64<ExpressionStream> Streams { get; set; } = new();
        public ResourceSimpleList64_s<ExpressionTrack> Tracks { get; set; } = new(); // bone tags / animation tracks
        public ResourceSimpleList64<ExpressionMotionDescriptionBlock> Motions { get; set; } = new();
        public ResourceSimpleList64_s<MetaHash> Variables { get; set; } = new();
        public ulong NamePointer { get; set; }
        public ushort NameLength { get; set; } // name len
        public ushort NameCapacity { get; set; } // name len+1
        public uint ReferenceCount { get; set; } = 1;
        public uint Signature { get; set; }
        public uint MaxStreamSize { get; set; } // max length of any item in Streams
        public ExpressionFlags Flags { get; set; } = ExpressionFlags.Packed;

        public string_r? Name { get; set; }
        public MetaHash NameHash { get; set; }

        public Dictionary<ExpressionTrack, ExpressionTrack> BoneTracksDict { get; set; } = new();


        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            VFT = reader.ReadUInt32();
            BaseReferenceCount = reader.ReadUInt32();
            _ = reader.ReadBytes(24); // pgBase data and the unpacked m_Expressions array.
            Streams = reader.ReadRequiredBlock<ResourcePointerList64<ExpressionStream>>();
            Tracks = reader.ReadRequiredBlock<ResourceSimpleList64_s<ExpressionTrack>>();
            Motions = reader.ReadRequiredBlock<ResourceSimpleList64<ExpressionMotionDescriptionBlock>>();
            Variables = reader.ReadRequiredBlock<ResourceSimpleList64_s<MetaHash>>();
            NamePointer = reader.ReadUInt64();
            NameLength = reader.ReadUInt16();
            NameCapacity = reader.ReadUInt16();
            _ = reader.ReadUInt32();
            ReferenceCount = reader.ReadUInt32();
            Signature = reader.ReadUInt32();
            MaxStreamSize = reader.ReadUInt32();
            Flags = (ExpressionFlags)reader.ReadUInt16();
            _ = reader.ReadBytes(18);

            Name = reader.ReadBlockAt<string_r>(NamePointer);

            JenkIndex.Ensure(GetShortName());


            BuildBoneTracksDict();



            #region testing

            //long tlen = 0;
            //if (Streams?.data_items != null) foreach (var item in Streams.data_items) tlen = Math.Max(tlen, item.BlockLength);
            //if (MaxStreamSize != tlen)
            //{ }//no hit

            #endregion
        }
        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            NamePointer = (ulong)(Name != null ? Name.FilePosition : 0);
            NameLength = checked((ushort)(Name?.Value.Length ?? 0));
            NameCapacity = NameLength == 0 ? (ushort)0 : checked((ushort)(NameLength + 1));

            writer.Write(VFT);
            writer.Write(BaseReferenceCount);
            writer.Write(new byte[24]);
            writer.WriteBlock(Streams);
            writer.WriteBlock(Tracks);
            writer.WriteBlock(Motions);
            writer.WriteBlock(Variables);
            writer.Write(NamePointer);
            writer.Write(NameLength);
            writer.Write(NameCapacity);
            writer.Write(0u);
            writer.Write(ReferenceCount);
            writer.Write(Signature);
            writer.Write(MaxStreamSize);
            writer.Write((ushort)Flags);
            writer.Write(new byte[18]);
        }
        public void WriteXml(StringBuilder sb, int indent)
        {
            YedXml.StringTag(sb, indent, "Name", Name?.Value ?? "");
            YedXml.ValueTag(sb, indent, "Signature", Signature.ToString());
            YedXml.ValueTag(sb, indent, "Flags", ((ushort)Flags).ToString());

            if ((Tracks.data_items.Length) > 0)
            {
                YedXml.WriteItemArray(sb, Tracks.data_items, indent, "Tracks");
            }

            if ((Streams.data_items.Length) > 0)
            {
                YedXml.WriteItemArray(sb, Streams.data_items, indent, "Streams");
            }

        }
        public void ReadXml(XmlNode node)
        {
            Name = new string_r();
            Name.Value = Xml.GetChildInnerText(node, "Name") ?? string.Empty;
            NameLength = (ushort)Name.Value.Length;
            NameCapacity = (ushort)(NameLength + 1);
            NameHash = JenkHash.GenHash(GetShortName());
            Signature = Xml.GetChildUIntAttribute(node, "Signature");
            var flagsNode = node.SelectSingleNode("Flags");
            Flags = (ExpressionFlags)(flagsNode != null
                ? Xml.GetUIntAttribute(flagsNode, "value")
                : Xml.GetChildUIntAttribute(node, "Unk7C"));

            Tracks = new ResourceSimpleList64_s<ExpressionTrack>();
            Tracks.data_items = XmlMeta.ReadItemArray<ExpressionTrack>(node, "Tracks");

            Streams = new ResourcePointerList64<ExpressionStream>();
            Streams.data_items = XmlMeta.ReadItemArray<ExpressionStream>(node, "Streams");

            BuildBoneTracksDict();
            BuildMotionsList();
            UpdateVariables();
            UpdateStreamBuffers();
        }

        public override IResourceBlock[] GetReferences()
        {
            var list = new List<IResourceBlock>();
            if (Name != null) list.Add(Name);
            return list.ToArray();
        }
        public override Tuple<long, IResourceBlock>[] GetParts()
        {
            PrepareForWrite();
            return new Tuple<long, IResourceBlock>[] {
                new Tuple<long, IResourceBlock>(0x20, Streams),
                new Tuple<long, IResourceBlock>(0x30, Tracks),
                new Tuple<long, IResourceBlock>(0x40, Motions),
                new Tuple<long, IResourceBlock>(0x50, Variables)
            };
        }

        private void PrepareForWrite()
        {
            BuildMotionsList();
            UpdateVariables();
            Signature = CalculateSignature();
            UpdateStreamBuffers();
            Flags |= ExpressionFlags.Packed;
        }


        public void BuildBoneTracksDict()
        {
            BoneTracksDict = new Dictionary<ExpressionTrack, ExpressionTrack>();

            if (Tracks?.data_items == null) return;

            var mapto = new ExpressionTrack();
            for(int i=0; i< Tracks.data_items.Length;i++)
            {
                var bt = Tracks.data_items[i];
                if ((bt.Flags & 128) == 0)
                {
                    mapto = bt;
                }
                else if (bt.BoneId != 0)
                {
                    var key = bt;
                    key.Flags &= 0x7F;
                    BoneTracksDict[key] = mapto;
                }
            }

        }
        public void BuildMotionsList()
        {
            var motions = new List<ExpressionMotionDescriptionBlock>();
            if (Streams?.data_items != null)
            {
                foreach (var stream in Streams.data_items)
                {
                    foreach (var node in stream.Instructions)
                    {
                        if (node is ExpressionInstrMotion instr)
                        {
                            motions.Add(new ExpressionMotionDescriptionBlock
                            {
                                Motion = instr.MotionDescription.Clone()
                            });
                        }
                    }
                }
            }
            Motions = new ResourceSimpleList64<ExpressionMotionDescriptionBlock>();
            Motions.data_items = motions.ToArray();
        }
        public void UpdateVariables()
        {
            var dict = new Dictionary<MetaHash, uint>();
            if (Streams?.data_items != null)
            {
                foreach (var stream in Streams.data_items)
                {
                    foreach (var instr in stream.Instructions)
                    {
                        if (instr is ExpressionInstrVariable instrVar)
                        {
                            dict[instrVar.Variable] = 0;
                        }
                    }
                }
            }
            var list = dict.Keys.ToList();
            list.Sort((a, b) => a.Hash.CompareTo(b.Hash));
            for (int i = 0; i < list.Count; i++)
            {
                dict[list[i]] = (uint)i;
            }
            if (Streams?.data_items != null)
            {
                foreach (var stream in Streams.data_items)
                {
                    foreach (var item in stream.Instructions)
                    {
                        if (item is ExpressionInstrVariable s3)
                        {
                            var index = dict[s3.Variable];
                            s3.VariableIndex = index;
                        }
                    }
                }
            }

            Variables = new ResourceSimpleList64_s<MetaHash>();
            Variables.data_items = list.ToArray();

        }
        public uint CalculateSignature()
        {
            uint signature = 0;
            var tracks = Tracks?.data_items ?? [];

            void AddTrack(ushort id, byte track)
            {
                signature = FrameFilterBase.Crc32Hash(BitConverter.GetBytes(((uint)track << 16) | id), signature);
            }
            void AddAccelerator(uint index)
            {
                if (index >= tracks.Length)
                    throw new InvalidDataException($"Expression accelerator index {index} is outside the track table.");
                var track = tracks[index];
                AddTrack(track.BoneId, track.Track);
            }

            foreach (var stream in Streams?.data_items ?? [])
            {
                foreach (var instruction in stream?.Instructions ?? [])
                {
                    if (instruction is ExpressionInstrBone frame)
                    {
                        AddTrack(frame.Id, frame.Track);
                    }
                    else if (instruction is ExpressionInstrBlend linear)
                    {
                        foreach (var source in linear.SourceInfos) AddAccelerator(source.AcceleratorIndex);
                    }
                    else if (instruction is ExpressionInstrMotion motion)
                    {
                        AddAccelerator(motion.AccumulatedRotationIndex);
                        AddAccelerator(motion.AccumulatedTranslationIndex);
                    }
                }
            }
            return signature;
        }
        public void UpdateStreamBuffers()
        {
            if (Streams?.data_items != null)
            {
                foreach (var item in Streams.data_items)
                {
                    item?.WriteInstructions();
                }
                UpdateJumpInstructions();
                MaxStreamSize = 0;
                foreach (var item in Streams.data_items)
                {
                    if (item == null) continue;
                    item.WriteInstructions();
                    MaxStreamSize = Math.Max(MaxStreamSize, checked((uint)item.BlockLength));
                }
            }
            else MaxStreamSize = 0;
        }
        public void UpdateJumpInstructions()
        {
            if (Streams?.data_items != null)
            {
                foreach (var stream in Streams.data_items)
                {
                    if (stream?.Instructions == null) continue;
                    foreach (var node in stream.Instructions)
                    {
                        if (node is ExpressionInstrJump jump)
                        {
                            var targetIndex = checked(jump.Index + 1 + (int)jump.OperationOffset);
                            if ((uint)targetIndex >= (uint)stream.Instructions.Length)
                                throw new InvalidDataException($"Expression branch at {jump.Index} targets operation {targetIndex} outside the stream.");
                            var target = stream.Instructions[targetIndex];
                            jump.AlignedParameterOffset = checked((uint)(target.Offset1 - jump.Offset1));
                            jump.ParameterOffset = checked((uint)(target.Offset2 - jump.Offset2 - ExpressionInstrJump.ParameterSize));
                        }
                    }
                }
            }
        }


        public string GetShortName()
        {
            return Path.GetFileNameWithoutExtension(Name?.Value ?? "").ToLowerInvariant();
        }


        public override string ToString()
        {
            return Name?.ToString() ?? base.ToString() ?? string.Empty;
        }
    }



    [TC(typeof(EXP))] public class ExpressionStream : ResourceSystemBlock, IMetaXmlItem
    {
        public override long BlockLength => 16 + AlignedParameters.Length + Parameters.Length + Operations.Length;
        public MetaHash DataHash { get; set; }
        public uint AlignedParametersSize { get; private set; }
        public uint ParametersSize { get; private set; }
        public ushort OperationCount { get; private set; }
        public ushort MaxStackDepth { get; set; }
        public byte[] AlignedParameters { get; private set; } = [];
        public byte[] Parameters { get; private set; } = [];
        public byte[] Operations { get; private set; } = [];


        public ExpressionInstrBase[] Instructions { get; set; } = [];



        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            DataHash = reader.ReadUInt32();
            AlignedParametersSize = reader.ReadUInt32();
            ParametersSize = reader.ReadUInt32();
            OperationCount = reader.ReadUInt16();
            MaxStackDepth = reader.ReadUInt16();
            AlignedParameters = reader.ReadBytes(checked((int)AlignedParametersSize));
            Parameters = reader.ReadBytes(checked((int)ParametersSize));
            Operations = reader.ReadBytes(OperationCount);
            ReadInstructions();
        }
        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            //WriteInstructions();//should already be done by Expression.UpdateStreamBuffers
            writer.Write(DataHash);
            writer.Write(AlignedParametersSize);
            writer.Write(ParametersSize);
            writer.Write(OperationCount);
            writer.Write(MaxStackDepth);
            writer.Write(AlignedParameters);
            writer.Write(Parameters);
            writer.Write(Operations);
        }
        public void WriteXml(StringBuilder sb, int indent)
        {
            YedXml.StringTag(sb, indent, "Hash", YedXml.HashString(DataHash));
            YedXml.ValueTag(sb, indent, "MaxStackDepth", MaxStackDepth.ToString());

            YedXml.OpenTag(sb, indent, "Instructions");
            var cind = indent + 1;
            var cind2 = cind + 1;
            foreach (var item in Instructions)
            {
                if (item is ExpressionInstrEmpty)
                {
                    YedXml.SelfClosingTag(sb, cind, "Item type=\"" + item.Type + "\"");
                }
                else
                {
                    YedXml.OpenTag(sb, cind, "Item type=\"" + item.Type + "\"");
                    item.WriteXml(sb, cind2);
                    YedXml.CloseTag(sb, cind, "Item");
                }
            }
            YedXml.CloseTag(sb, indent, "Instructions");


        }
        public void ReadXml(XmlNode node)
        {
            var hashText = Xml.GetChildInnerText(node, "Hash");
            if (string.IsNullOrEmpty(hashText)) hashText = Xml.GetChildInnerText(node, "Name");
            DataHash = XmlMeta.GetHash(hashText);
            var depthNode = node.SelectSingleNode("MaxStackDepth");
            MaxStackDepth = (ushort)(depthNode != null
                ? Xml.GetUIntAttribute(depthNode, "value")
                : Xml.GetChildUIntAttribute(node, "Depth", "value"));

            var items = new List<ExpressionInstrBase>();
            var instnode = node.SelectSingleNode("Instructions");
            if (instnode != null)
            {
                var inodes = instnode.SelectNodes("Item");
                if (inodes?.Count > 0)
                {
                    foreach (XmlNode inode in inodes)
                    {
                        if (Enum.TryParse<ExpressionInstrType>(Xml.GetStringAttribute(inode, "type"), out var type))
                        {
                            var item = CreateInstruction(type);
                            item.Type = type;
                            item.ReadXml(inode);
                            items.Add(item);
                        }
                        else throw new InvalidDataException($"Unknown expression instruction '{Xml.GetStringAttribute(inode, "type")}'.");
                    }
                }
            }
            Instructions = items.ToArray();
        }

        public void ReadInstructions()
        {
            var insts = new ExpressionInstrBase[Operations.Length];
            var s1 = new MemoryStream(AlignedParameters);
            var s2 = new MemoryStream(Parameters);
            var r1 = new DataReader(s1);
            var r2 = new DataReader(s2);

            for (int i = 0; i < insts.Length; i++)
            {
                var type = (ExpressionInstrType)Operations[i];
                var instr = CreateInstruction(type);
                instr.Type = type;
                instr.Index = i;
                instr.Offset1 = (int)r1.Position;
                instr.Offset2 = (int)r2.Position;
                instr.Read(r1, r2);
                insts[i] = instr;
            }

            if (r1.Position != r1.Length || r2.Position != r2.Length)
                throw new InvalidDataException("Expression parameter buffers were not consumed exactly.");

            Instructions = insts;
        }

        public void WriteInstructions()
        {
            var s1 = new MemoryStream();
            var s2 = new MemoryStream();
            var s3 = new MemoryStream();
            var w1 = new DataWriter(s1);
            var w2 = new DataWriter(s2);
            var w3 = new DataWriter(s3);

            foreach (var instr in Instructions)
            {
                instr.Offset1 = (int)w1.Position;
                instr.Offset2 = (int)w2.Position;
                instr.Index = (int)w3.Position;
                w3.Write((byte)instr.Type);
                instr.Write(w1, w2);
            }

            AlignedParameters = s1.ToArray();
            Parameters = s2.ToArray();
            Operations = s3.ToArray();
            AlignedParametersSize = checked((uint)AlignedParameters.Length);
            ParametersSize = checked((uint)Parameters.Length);
            OperationCount = checked((ushort)Operations.Length);
            var hashData = new byte[AlignedParameters.Length + Parameters.Length + Operations.Length];
            Buffer.BlockCopy(AlignedParameters, 0, hashData, 0, AlignedParameters.Length);
            Buffer.BlockCopy(Parameters, 0, hashData, AlignedParameters.Length, Parameters.Length);
            Buffer.BlockCopy(Operations, 0, hashData, AlignedParameters.Length + Parameters.Length, Operations.Length);
            DataHash = JenkHash.GenHash(hashData);
        }


        public static ExpressionInstrBase CreateInstruction(ExpressionInstrType type)
        {
            switch (type)
            {
                case ExpressionInstrType.Halt:
                case ExpressionInstrType.Pop:
                case ExpressionInstrType.Push:
                case ExpressionInstrType.Zero:
                case ExpressionInstrType.One:
                case ExpressionInstrType.Deprecated0:
                case ExpressionInstrType.Deprecated1:
                case ExpressionInstrType.Abs:
                case ExpressionInstrType.Negate:
                case ExpressionInstrType.Invert:
                case ExpressionInstrType.Sqrt:
                case ExpressionInstrType.Log:
                case ExpressionInstrType.Exp:
                case ExpressionInstrType.Cos:
                case ExpressionInstrType.Sin:
                case ExpressionInstrType.Tan:
                case ExpressionInstrType.ArcCos:
                case ExpressionInstrType.ArcSin:
                case ExpressionInstrType.ArcTan:
                case ExpressionInstrType.QuatInvert:
                case ExpressionInstrType.Square:
                case ExpressionInstrType.DegToRad:
                case ExpressionInstrType.RadToDeg:
                case ExpressionInstrType.Clamp01:
                case ExpressionInstrType.FromEuler:
                case ExpressionInstrType.ToEuler:
                case ExpressionInstrType.Add:
                case ExpressionInstrType.Subtract:
                case ExpressionInstrType.Multiply:
                case ExpressionInstrType.Min:
                case ExpressionInstrType.Max:
                case ExpressionInstrType.QuatMultiply:
                case ExpressionInstrType.QuatScale:
                case ExpressionInstrType.GreaterThan:
                case ExpressionInstrType.LessThan:
                case ExpressionInstrType.GreaterThanEqual:
                case ExpressionInstrType.LessThanEqual:
                case ExpressionInstrType.Clamp:
                case ExpressionInstrType.Lerp:
                case ExpressionInstrType.MultiplyAdd:
                case ExpressionInstrType.QuatLerp:
                case ExpressionInstrType.ToVec:
                case ExpressionInstrType.Time:
                case ExpressionInstrType.Transform:
                case ExpressionInstrType.Xor:
                case ExpressionInstrType.DeltaTime:
                case ExpressionInstrType.QuatIdentity:
                case ExpressionInstrType.Equal:
                case ExpressionInstrType.NotEqual:
                case ExpressionInstrType.CosH:
                case ExpressionInstrType.SinH:
                case ExpressionInstrType.TanH:
                case ExpressionInstrType.Exponent:
                    return new ExpressionInstrEmpty();

                case ExpressionInstrType.LinearVec:
                case ExpressionInstrType.LinearQuat:
                    return new ExpressionInstrBlend();

                case ExpressionInstrType.Get:
                case ExpressionInstrType.GetComp:
                case ExpressionInstrType.GetRelative:
                case ExpressionInstrType.GetCompRelative:
                case ExpressionInstrType.ObjectGet:
                case ExpressionInstrType.Valid:
                case ExpressionInstrType.ObjectConvertFrom:
                case ExpressionInstrType.ObjectConvertTo:
                case ExpressionInstrType.Set:
                case ExpressionInstrType.SetComp:
                case ExpressionInstrType.SetRelative:
                case ExpressionInstrType.SetCompRelative:
                case ExpressionInstrType.ObjectSet:
                    return new ExpressionInstrBone();

                case ExpressionInstrType.GetVariable:
                case ExpressionInstrType.SetVariable:
                    return new ExpressionInstrVariable();

                case ExpressionInstrType.Branch:
                case ExpressionInstrType.BranchZero:
                case ExpressionInstrType.BranchNotZero:
                    return new ExpressionInstrJump();

                case ExpressionInstrType.ConstantFloat: return new ExpressionInstrFloat();
                case ExpressionInstrType.Constant: return new ExpressionInstrVector();
                case ExpressionInstrType.Motion: return new ExpressionInstrMotion();
                case ExpressionInstrType.Curve: return new ExpressionInstrCurve();
                case ExpressionInstrType.LookAt: return new ExpressionInstrLookAt();

                default: throw new InvalidDataException($"Unknown expression instruction type 0x{(byte)type:X2}.");
            }
        }


        public override string ToString()
        {
            return DataHash + " (" + (Instructions?.Length??0) + " instructions)";
        }

    }


    public enum ExpressionInstrType : byte
    {
        Halt = 0,
        Pop = 0x01,
        Push = 0x02,
        Zero = 0x03,
        One = 0x04,
        ConstantFloat = 0x05,
        Get = 0x06,
        GetComp = 0x07,
        GetRelative = 0x08,
        GetCompRelative = 0x09,
        ObjectGet = 0x0A,
        Constant = 0x0B,
        Deprecated0 = 0x0C,
        Deprecated1 = 0x0D,
        Motion = 0x0E,
        Abs = 0x0F,
        Negate = 0x10,
        Invert = 0x11,
        Sqrt = 0x12,
        Log = 0x13,
        Exp = 0x14,
        Cos = 0x15,
        Sin = 0x16,
        Tan = 0x17,
        ArcCos = 0x18,
        ArcSin = 0x19,
        ArcTan = 0x1A,
        QuatInvert = 0x1B,
        Square = 0x1C,
        DegToRad = 0x1D,
        RadToDeg = 0x1E,
        Clamp01 = 0x1F,
        Valid = 0x20,
        FromEuler = 0x21,
        ToEuler = 0x22,
        ObjectConvertFrom = 0x23,
        ObjectConvertTo = 0x24,
        Curve = 0x25,
        Set = 0x26,
        SetComp = 0x27,
        SetRelative = 0x28,
        SetCompRelative = 0x29,
        ObjectSet = 0x2A,
        Branch = 0x2B,
        BranchZero = 0x2C,
        BranchNotZero = 0x2D,
        Add = 0x2E,
        Subtract = 0x2F,
        Multiply = 0x30,
        Min = 0x31,
        Max = 0x32,
        QuatMultiply = 0x33,
        QuatScale = 0x34,
        GreaterThan = 0x35,
        LessThan = 0x36,
        GreaterThanEqual = 0x37,
        LessThanEqual = 0x38,
        Clamp = 0x39,
        Lerp = 0x3A,
        MultiplyAdd = 0x3B,
        QuatLerp = 0x3C,
        ToVec = 0x3D,
        LookAt = 0x3E,
        Time = 0x3F,
        Transform = 0x40,
        Xor = 0x41,
        GetVariable = 0x42,
        SetVariable = 0x43,
        LinearVec = 0x44,
        LinearQuat = 0x45,
        DeltaTime = 0x46,
        QuatIdentity = 0x47,
        Equal = 0x48,
        NotEqual = 0x49,
        CosH = 0x4A,
        SinH = 0x4B,
        TanH = 0x4C,
        Exponent = 0x4D,

        End = Halt,
        Dup = Push,
        Push0 = Zero,
        Push1 = One,
        PushFloat = ConstantFloat,
        TrackGet = Get,
        TrackGetComp = GetComp,
        TrackGetOffset = GetRelative,
        TrackGetOffsetComp = GetCompRelative,
        TrackGetBoneTransform = ObjectGet,
        PushVector = Constant,
        DefineSpring = Motion,
        VectorAbs = Abs,
        VectorNeg = Negate,
        VectorRcp = Invert,
        VectorSqrt = Sqrt,
        VectorNeg3 = QuatInvert,
        VectorSquare = Square,
        VectorDeg2Rad = DegToRad,
        VectorRad2Deg = RadToDeg,
        VectorSaturate = Clamp01,
        TrackValid = Valid,
        Unk23 = ObjectConvertFrom,
        TrackSet = Set,
        TrackSetComp = SetComp,
        TrackSetOffset = SetRelative,
        TrackSetOffsetComp = SetCompRelative,
        TrackSetBoneTransform = ObjectSet,
        Jump = Branch,
        JumpIfTrue = BranchZero,
        JumpIfFalse = BranchNotZero,
        VectorAdd = Add,
        VectorSub = Subtract,
        VectorMul = Multiply,
        VectorMin = Min,
        VectorMax = Max,
        QuatMul = QuatMultiply,
        VectorGreaterThan = GreaterThan,
        VectorLessThan = LessThan,
        VectorGreaterEqual = GreaterThanEqual,
        VectorLessEqual = LessThanEqual,
        VectorClamp = Clamp,
        VectorLerp = Lerp,
        VectorMad = MultiplyAdd,
        QuatSlerp = QuatLerp,
        ToVector = ToVec,
        PushTime = Time,
        VectorTransform = Transform,
        BlendVector = LinearVec,
        BlendQuaternion = LinearQuat,
        PushDeltaTime = DeltaTime,
        VectorEqual = Equal,
        VectorNotEqual = NotEqual,
    }


    [TC(typeof(EXP))] public abstract class ExpressionInstrBase
    {
        public ExpressionInstrType Type { get; set; }
        public int Offset1 { get; set; }
        public int Offset2 { get; set; }
        public int Index { get; set; }

        public virtual void Read(DataReader r1, DataReader r2)
        { }
        public virtual void Write(DataWriter w1, DataWriter w2)
        { }
        public virtual void WriteXml(StringBuilder sb, int indent)
        { }
        public virtual void ReadXml(XmlNode node)
        { }

        public override string ToString()
        {
            return Type.ToString();
        }
    }
    [TC(typeof(EXP))] public class ExpressionInstrEmpty : ExpressionInstrBase
    { }
    [TC(typeof(EXP))] public class ExpressionInstrBlend : ExpressionInstrBase
    {
        public struct SourceInfo
        {
            public ushort AcceleratorIndex;
            public ushort ComponentOffset;
            public ushort TrackIndex { readonly get => AcceleratorIndex; set => AcceleratorIndex = value; }
            public override string ToString()
            {
                return $"{AcceleratorIndex} : {ComponentOffset}";
            }
        }
        [TC(typeof(EXP))] public class SourceComponent : IMetaXmlItem
        {
            public float[] Weights { get; set; } = [];
            public float[] Offsets { get; set; } = [];
            public float[] Thresholds { get; set; } = [];

            public SourceComponent() { }
            public SourceComponent(uint numSourceWeights)
            {
                Weights = new float[numSourceWeights];
                Offsets = new float[numSourceWeights];
                Thresholds = new float[numSourceWeights - 1];
            }

            public void WriteXml(StringBuilder sb, int indent)
            {
                YedXml.WriteRawArray(sb, Weights, indent, "Weights", "", FloatUtil.ToString, 32);
                YedXml.WriteRawArray(sb, Offsets, indent, "Offsets", "", FloatUtil.ToString, 32);
                YedXml.WriteRawArray(sb, Thresholds, indent, "Thresholds", "", FloatUtil.ToString, 32);
            }
            public void ReadXml(XmlNode? node)
            {
                if (node == null) return;
                Weights = Xml.GetChildRawFloatArray(node, "Weights");
                Offsets = Xml.GetChildRawFloatArray(node, "Offsets");
                Thresholds = Xml.GetChildRawFloatArray(node, "Thresholds");
            }
        }
        [TC(typeof(EXP))] public class Source : IMetaXmlItem
        {
            public SourceInfo Info { get; set; }
            public SourceComponent X { get; set; } = new();
            public SourceComponent Y { get; set; } = new();
            public SourceComponent Z { get; set; } = new();

            public Source()
            { }
            public Source(SourceInfo info, uint numSourceWeights, int index, Vector4[] values)
            {
                Info = info;
                X = new SourceComponent(numSourceWeights);
                Y = new SourceComponent(numSourceWeights);
                Z = new SourceComponent(numSourceWeights);

                var j = index / 4;
                var k = index % 4;
                var v = j * (6 + 9 * (int)(numSourceWeights - 1));
                X.Weights[0] = values[v + 0][k];
                Y.Weights[0] = values[v + 1][k];
                Z.Weights[0] = values[v + 2][k];
                X.Offsets[0] = values[v + 3][k];
                Y.Offsets[0] = values[v + 4][k];
                Z.Offsets[0] = values[v + 5][k];
                for (int n = 1; n < numSourceWeights; n++)
                {
                    var m = n - 1;
                    var b = v + 6 + (9 * m);
                    X.Thresholds[m] = values[b + 0][k];
                    Y.Thresholds[m] = values[b + 1][k];
                    Z.Thresholds[m] = values[b + 2][k];
                    X.Weights[n] = values[b + 3][k];
                    Y.Weights[n] = values[b + 4][k];
                    Z.Weights[n] = values[b + 5][k];
                    X.Offsets[n] = values[b + 6][k];
                    Y.Offsets[n] = values[b + 7][k];
                    Z.Offsets[n] = values[b + 8][k];
                }
            }

            public void WriteXml(StringBuilder sb, int indent)
            {
                YedXml.ValueTag(sb, indent, "AcceleratorIndex", Info.AcceleratorIndex.ToString());
                YedXml.ValueTag(sb, indent, "ComponentIndex", (Info.ComponentOffset / 4).ToString());
                YedXml.OpenTag(sb, indent, "X");
                X.WriteXml(sb, indent + 1);
                YedXml.CloseTag(sb, indent, "X");
                YedXml.OpenTag(sb, indent, "Y");
                Y.WriteXml(sb, indent + 1);
                YedXml.CloseTag(sb, indent, "Y");
                YedXml.OpenTag(sb, indent, "Z");
                Z.WriteXml(sb, indent + 1);
                YedXml.CloseTag(sb, indent, "Z");
            }
            public void ReadXml(XmlNode node)
            {
                var info = new SourceInfo();
                var acceleratorNode = node.SelectSingleNode("AcceleratorIndex");
                info.AcceleratorIndex = (ushort)(acceleratorNode != null
                    ? Xml.GetUIntAttribute(acceleratorNode, "value")
                    : Xml.GetChildUIntAttribute(node, "TrackIndex", "value"));
                info.ComponentOffset = (ushort)(Xml.GetChildUIntAttribute(node, "ComponentIndex", "value") * 4);
                Info = info;
                X = new SourceComponent();
                X.ReadXml(node.SelectSingleNode("X"));
                Y = new SourceComponent();
                Y.ReadXml(node.SelectSingleNode("Y"));
                Z = new SourceComponent();
                Z.ReadXml(node.SelectSingleNode("Z"));
            }

            public void UpdateValues(uint numSourceWeights, int index, Vector4[] values)
            {
                if (numSourceWeights == 0) return;
                if (X == null) return;
                if (Y == null) return;
                if (Z == null) return;
                if (X.Weights.Length < numSourceWeights) return;
                if (Y.Weights.Length < numSourceWeights) return;
                if (Z.Weights.Length < numSourceWeights) return;
                if (X.Offsets.Length < numSourceWeights) return;
                if (Y.Offsets.Length < numSourceWeights) return;
                if (Z.Offsets.Length < numSourceWeights) return;
                if (X.Thresholds.Length < (numSourceWeights - 1)) return;
                if (Y.Thresholds.Length < (numSourceWeights - 1)) return;
                if (Z.Thresholds.Length < (numSourceWeights - 1)) return;
                var j = index / 4;
                var k = index % 4;
                var v = j * (6 + 9 * (int)(numSourceWeights - 1));
                values[v + 0][k] = X.Weights[0];
                values[v + 1][k] = Y.Weights[0];
                values[v + 2][k] = Z.Weights[0];
                values[v + 3][k] = X.Offsets[0];
                values[v + 4][k] = Y.Offsets[0];
                values[v + 5][k] = Z.Offsets[0];
                for (int n = 1; n < numSourceWeights; n++)
                {
                    var m = n - 1;
                    var b = v + 6 + (9 * m);
                    values[b + 0][k] = X.Thresholds[m];
                    values[b + 1][k] = Y.Thresholds[m];
                    values[b + 2][k] = Z.Thresholds[m];
                    values[b + 3][k] = X.Weights[n];
                    values[b + 4][k] = Y.Weights[n];
                    values[b + 5][k] = Z.Weights[n];
                    values[b + 6][k] = X.Offsets[n];
                    values[b + 7][k] = Y.Offsets[n];
                    values[b + 8][k] = Z.Offsets[n];
                }
            }

            public override string ToString()
            {
                return $"AcceleratorIndex {Info.AcceleratorIndex}, ComponentIndex {Info.ComponentOffset / 4} (offset {Info.ComponentOffset})";
            }
        }

        public uint Size { get; private set; }
        public uint NumSources { get; private set; }
        public uint IntervalsPerSource { get; set; } = 1;
        public uint SourceCount { get => NumSources; set => NumSources = value; }
        public uint NumSourceWeights { get => IntervalsPerSource; set => IntervalsPerSource = value; }
        public SourceInfo[] SourceInfos { get; set; } = [];
        public Vector4[] Values { get; set; } = [];

        public uint RequiredValueCount => ((NumSources + 3) / 4) * (6 + ((IntervalsPerSource - 1) * 9));

        public override void Read(DataReader r1, DataReader r2)
        {
            Size = r1.ReadUInt32();
            NumSources = r1.ReadUInt32();
            IntervalsPerSource = r1.ReadUInt32();
            _ = r1.ReadUInt32();
            if (IntervalsPerSource == 0)
                throw new InvalidDataException("Invalid linear expression operation dimensions.");

            SourceInfos = new SourceInfo[NumSources];
            for (int i = 0; i < NumSources; i++)
            {
                var s = new SourceInfo();
                s.AcceleratorIndex = r1.ReadUInt16();
                s.ComponentOffset = r1.ReadUInt16();
                SourceInfos[i] = s;
            }
            Values = new Vector4[RequiredValueCount];
            for (int i = 0; i < Values.Length; i++)
            {
                Values[i] = r1.ReadVector4();
            }
            var expectedSize = checked(16u + NumSources * 4u + RequiredValueCount * 16u);
            if (Size != expectedSize)
                throw new InvalidDataException($"Invalid linear expression operation size {Size}; expected {expectedSize}.");
        }
        public override void Write(DataWriter w1, DataWriter w2)
        {
            NumSources = checked((uint)SourceInfos.Length);
            IntervalsPerSource = Math.Max(IntervalsPerSource, 1);
            if (Values.Length != RequiredValueCount)
                throw new InvalidDataException("Linear expression operation requires an exact value table.");
            Size = checked(16u + NumSources * 4u + RequiredValueCount * 16u);

            w1.Write(Size);
            w1.Write(NumSources);
            w1.Write(IntervalsPerSource);
            w1.Write(0u);

            for (int i = 0; i < NumSources; i++)
            {
                var si = SourceInfos[i];
                w1.Write(si.AcceleratorIndex);
                w1.Write(si.ComponentOffset);
            }
            for (int i = 0; i < Values.Length; i++)
            {
                w1.Write(Values[i]);
            }
        }
        public override void WriteXml(StringBuilder sb, int indent)
        {
            // organize data into more human-readable layout
            // the file layout is optimized for vectorized operations
            var sources = new Source[NumSources];
            for (int i = 0; i < NumSources; i++)
            {
                sources[i] = new Source(SourceInfos[i], IntervalsPerSource, i, Values);
            }

            YedXml.ValueTag(sb, indent, "IntervalsPerSource", IntervalsPerSource.ToString());
            YedXml.WriteItemArray(sb, sources, indent, "Sources");
        }
        public override void ReadXml(XmlNode node)
        {
            var intervalsNode = node.SelectSingleNode("IntervalsPerSource");
            IntervalsPerSource = Math.Max(intervalsNode != null
                ? Xml.GetUIntAttribute(intervalsNode, "value")
                : Xml.GetChildUIntAttribute(node, "NumSourceWeights"), 1);
            var sources = XmlMeta.ReadItemArray<Source>(node, "Sources");
            NumSources = checked((uint)sources.Length);
            SourceInfos = new SourceInfo[NumSources];
            Values = new Vector4[RequiredValueCount];
            for (int i = 0; i < NumSources; i++)
            {
                var s = sources[i];
                SourceInfos[i] = s.Info;
                s.UpdateValues(IntervalsPerSource, i, Values);
            }
        }

        public override string ToString()
        {
            return base.ToString() + "  -  " + NumSources + ", " + IntervalsPerSource;
        }
    }
    [TC(typeof(EXP))] public class ExpressionInstrBone : ExpressionInstrBase
    {
        public ushort AcceleratorIndex { get; set; }
        public ushort Id { get; set; }
        public byte Track { get; set; }
        public byte Format { get; set; }
        public byte Component { get; set; }
        public bool ForceDefault { get; set; }
        public ushort TrackIndex { get => AcceleratorIndex; set => AcceleratorIndex = value; }
        public ushort BoneId { get => Id; set => Id = value; }
        public byte ComponentIndex { get => Component; set => Component = value; }
        public bool UseDefaults { get => ForceDefault; set => ForceDefault = value; }

        public override void Read(DataReader r1, DataReader r2)
        {
            AcceleratorIndex = r2.ReadUInt16();
            Id = r2.ReadUInt16();
            Track = r2.ReadByte();
            Format = r2.ReadByte();
            Component = r2.ReadByte();
            ForceDefault = r2.ReadByte() != 0;
        }
        public override void Write(DataWriter w1, DataWriter w2)
        {
            w2.Write(AcceleratorIndex);
            w2.Write(Id);
            w2.Write(Track);
            w2.Write(Format);
            w2.Write(Component);
            w2.Write(ForceDefault ? (byte)1 : (byte)0);
        }
        public override void WriteXml(StringBuilder sb, int indent)
        {
            YedXml.ValueTag(sb, indent, "AcceleratorIndex", AcceleratorIndex.ToString());
            YedXml.ValueTag(sb, indent, "Id", Id.ToString());
            YedXml.ValueTag(sb, indent, "Track", Track.ToString());
            YedXml.ValueTag(sb, indent, "Format", Format.ToString());
            YedXml.ValueTag(sb, indent, "Component", Component.ToString());
            YedXml.ValueTag(sb, indent, "ForceDefault", ForceDefault.ToString());
        }
        public override void ReadXml(XmlNode node)
        {
            AcceleratorIndex = (ushort)(node.SelectSingleNode("AcceleratorIndex") != null
                ? Xml.GetChildUIntAttribute(node, "AcceleratorIndex", "value")
                : Xml.GetChildUIntAttribute(node, "TrackIndex", "value"));
            Id = (ushort)(node.SelectSingleNode("Id") != null
                ? Xml.GetChildUIntAttribute(node, "Id", "value")
                : Xml.GetChildUIntAttribute(node, "BoneId", "value"));
            Track = (byte)Xml.GetChildUIntAttribute(node, "Track", "value");
            Format = (byte)Xml.GetChildUIntAttribute(node, "Format", "value");
            Component = (byte)(node.SelectSingleNode("Component") != null
                ? Xml.GetChildUIntAttribute(node, "Component", "value")
                : Xml.GetChildUIntAttribute(node, "ComponentIndex", "value"));
            ForceDefault = node.SelectSingleNode("ForceDefault") != null
                ? Xml.GetChildBoolAttribute(node, "ForceDefault", "value")
                : Xml.GetChildBoolAttribute(node, "UseDefaults", "value");
        }

        public override string ToString()
        {
            return base.ToString() + "  -  AcceleratorIndex:" + AcceleratorIndex + ", Id:" + Id + ", Track: " + Track + ", Format: " + Format + ", Component: " + Component + ", ForceDefault: " + ForceDefault;
        }
    }
    [TC(typeof(EXP))] public class ExpressionInstrVariable : ExpressionInstrBase
    {
        public MetaHash Variable { get; set; }
        public uint VariableIndex { get; set; } //index of the hash in the Expression.Variables array (autoupdated - don't need in XML)

        public override void Read(DataReader r1, DataReader r2)
        {
            Variable = r2.ReadUInt32();
            VariableIndex = r2.ReadUInt32();
        }
        public override void Write(DataWriter w1, DataWriter w2)
        {
            w2.Write(Variable);
            w2.Write(VariableIndex);
        }
        public override void WriteXml(StringBuilder sb, int indent)
        {
            YedXml.StringTag(sb, indent, "Variable", YedXml.HashString(Variable));
        }
        public override void ReadXml(XmlNode node)
        {
            Variable = XmlMeta.GetHash(Xml.GetChildInnerText(node, "Variable"));
        }

        public override string ToString()
        {
            return base.ToString() + "  -  Variable:" + Variable + "  [" + VariableIndex + "]";
        }
    }
    [TC(typeof(EXP))] public class ExpressionInstrCurve : ExpressionInstrBase
    {
        [TC(typeof(EXP))] public class Key : IMetaXmlItem
        {
            public float Input { get; set; }
            public float Output { get; set; }

            public void WriteXml(StringBuilder sb, int indent)
            {
                YedXml.ValueTag(sb, indent, "Input", FloatUtil.ToString(Input));
                YedXml.ValueTag(sb, indent, "Output", FloatUtil.ToString(Output));
            }
            public void ReadXml(XmlNode node)
            {
                Input = Xml.GetChildFloatAttribute(node, "Input");
                Output = Xml.GetChildFloatAttribute(node, "Output");
            }
        }

        public Key[] Keys { get; set; } = [];

        public override void Read(DataReader r1, DataReader r2)
        {
            var size = r2.ReadUInt32();
            var count = r2.ReadUInt32();
            if (size != checked(8u + count * 8u) || count > int.MaxValue)
                throw new InvalidDataException($"Invalid expression curve payload ({size} bytes, {count} keys).");
            Keys = new Key[count];
            for (var i = 0; i < Keys.Length; i++)
            {
                Keys[i] = new Key { Input = r2.ReadSingle(), Output = r2.ReadSingle() };
            }
        }
        public override void Write(DataWriter w1, DataWriter w2)
        {
            Keys ??= [];
            w2.Write(checked(8u + (uint)Keys.Length * 8u));
            w2.Write(checked((uint)Keys.Length));
            foreach (var key in Keys)
            {
                w2.Write(key.Input);
                w2.Write(key.Output);
            }
        }
        public override void WriteXml(StringBuilder sb, int indent)
        {
            YedXml.WriteItemArray(sb, Keys, indent, "Keys");
        }
        public override void ReadXml(XmlNode node)
        {
            Keys = XmlMeta.ReadItemArray<Key>(node, "Keys");
        }
    }
    [TC(typeof(EXP))] public class ExpressionInstrJump : ExpressionInstrBase
    {
        public const int ParameterSize = 12;
        public uint AlignedParameterOffset { get; set; }
        public uint ParameterOffset { get; set; }
        public uint OperationOffset { get; set; }
        public uint Data3Offset { get => OperationOffset; set => OperationOffset = value; }

        public override void Read(DataReader r1, DataReader r2)
        {
            AlignedParameterOffset = r2.ReadUInt32();
            ParameterOffset = r2.ReadUInt32();
            OperationOffset = r2.ReadUInt32();
        }
        public override void Write(DataWriter w1, DataWriter w2)
        {
            w2.Write(AlignedParameterOffset);
            w2.Write(ParameterOffset);
            w2.Write(OperationOffset);
        }
        public override void WriteXml(StringBuilder sb, int indent)
        {
            YedXml.ValueTag(sb, indent, "OperationOffset", OperationOffset.ToString());
        }
        public override void ReadXml(XmlNode node)
        {
            var operationNode = node.SelectSingleNode("OperationOffset");
            OperationOffset = operationNode != null
                ? Xml.GetUIntAttribute(operationNode, "value")
                : Xml.GetChildUIntAttribute(node, "InstructionOffset");
        }

        public override string ToString()
        {
            return base.ToString() + "  -  " + AlignedParameterOffset + ", " + ParameterOffset + ", " + OperationOffset;
        }
    }
    [TC(typeof(EXP))] public class ExpressionInstrFloat : ExpressionInstrBase
    {
        public float Value { get; set; }

        public override void Read(DataReader r1, DataReader r2)
        {
            Value = r2.ReadSingle();
        }
        public override void Write(DataWriter w1, DataWriter w2)
        {
            w2.Write(Value);
        }
        public override void WriteXml(StringBuilder sb, int indent)
        {
            YedXml.ValueTag(sb, indent, "Value", FloatUtil.ToString(Value));
        }
        public override void ReadXml(XmlNode node)
        {
            Value = Xml.GetChildFloatAttribute(node, "Value");
        }

        public override string ToString()
        {
            return base.ToString() + "  -  " + Value.ToString();
        }
    }
    [TC(typeof(EXP))] public class ExpressionInstrVector : ExpressionInstrBase
    {
        public Vector4 Value { get; set; }

        public override void Read(DataReader r1, DataReader r2)
        {
            Value = r1.ReadVector4();
        }
        public override void Write(DataWriter w1, DataWriter w2)
        {
            w1.Write(Value);
        }
        public override void WriteXml(StringBuilder sb, int indent)
        {
            YedXml.SelfClosingTag(sb, indent, "Value " + FloatUtil.GetVector4XmlString(Value));
        }
        public override void ReadXml(XmlNode node)
        {
            Value = Xml.GetChildVector4Attributes(node, "Value");
        }

        public override string ToString()
        {
            return base.ToString() + "  -  " + Value;
        }
    }
    [TC(typeof(EXP))] public class ExpressionInstrMotion : ExpressionInstrBase
    {
        public ExpressionMotionDescription MotionDescription { get; set; } = new();
        public uint AccumulatedRotationIndex { get; set; }
        public uint AccumulatedTranslationIndex { get; set; }

        public override void Read(DataReader r1, DataReader r2)
        {
            MotionDescription = new ExpressionMotionDescription();
            MotionDescription.Read(r1);
            AccumulatedRotationIndex = r1.ReadUInt32();
            AccumulatedTranslationIndex = r1.ReadUInt32();
            _ = r1.ReadBytes(8);
        }
        public override void Write(DataWriter w1, DataWriter w2)
        {
            MotionDescription ??= new ExpressionMotionDescription();
            MotionDescription.Write(w1);
            w1.Write(AccumulatedRotationIndex);
            w1.Write(AccumulatedTranslationIndex);
            w1.Write(new byte[8]);
        }
        public override void WriteXml(StringBuilder sb, int indent)
        {
            MotionDescription ??= new ExpressionMotionDescription();
            MotionDescription.WriteXml(sb, indent);
            YedXml.ValueTag(sb, indent, "AccumulatedRotationIndex", AccumulatedRotationIndex.ToString());
            YedXml.ValueTag(sb, indent, "AccumulatedTranslationIndex", AccumulatedTranslationIndex.ToString());
        }
        public override void ReadXml(XmlNode node)
        {
            MotionDescription = new ExpressionMotionDescription();
            MotionDescription.ReadXml(node);
            var rotationNode = node.SelectSingleNode("AccumulatedRotationIndex");
            var translationNode = node.SelectSingleNode("AccumulatedTranslationIndex");
            AccumulatedRotationIndex = rotationNode != null
                ? Xml.GetUIntAttribute(rotationNode, "value")
                : Xml.GetChildUIntAttribute(node, "BoneTrackRot", "value");
            AccumulatedTranslationIndex = translationNode != null
                ? Xml.GetUIntAttribute(translationNode, "value")
                : Xml.GetChildUIntAttribute(node, "BoneTrackPos", "value");
        }

        public override string ToString()
        {
            return base.ToString() + "   -   " + AccumulatedRotationIndex + ", " + AccumulatedTranslationIndex;
        }
    }
    [TC(typeof(EXP))] public class ExpressionInstrSpring : ExpressionInstrMotion
    { }
    [TC(typeof(EXP))] public class ExpressionInstrLookAt : ExpressionInstrBase
    {
        public enum Axis : uint
        {
            PositiveX = 0, // ( 1.0,  0.0,  0.0)
            PositiveY = 1, // ( 0.0,  1.0,  0.0)
            PositiveZ = 2, // ( 0.0,  0.0,  1.0)
            NegativeX = 3, // (-1.0,  0.0,  0.0)
            NegativeY = 4, // ( 0.0, -1.0,  0.0)
            NegativeZ = 5, // ( 0.0,  0.0, -1.0)
        }
        
        public Vector4 Offset { get; set; }
        public Axis LookAtAxis { get; set; } // 0, 1, 2
        public Axis UpAxis { get; set; } // 0, 2
        public Axis Origin { get; set; } // 0, 2
        public override void Read(DataReader r1, DataReader r2)
        {
            Offset = r1.ReadVector4();
            LookAtAxis = (Axis)r1.ReadUInt32();
            UpAxis = (Axis)r1.ReadUInt32();
            Origin = (Axis)r1.ReadUInt32();
            _ = r1.ReadUInt32();
        }
        public override void Write(DataWriter w1, DataWriter w2)
        {
            w1.Write(Offset);
            w1.Write((uint)LookAtAxis);
            w1.Write((uint)UpAxis);
            w1.Write((uint)Origin);
            w1.Write(0u);
        }
        public override void WriteXml(StringBuilder sb, int indent)
        {
            YedXml.SelfClosingTag(sb, indent, "Offset " + FloatUtil.GetVector4XmlString(Offset));
            YedXml.StringTag(sb, indent, "LookAtAxis", LookAtAxis.ToString());
            YedXml.StringTag(sb, indent, "UpAxis", UpAxis.ToString());
            YedXml.StringTag(sb, indent, "Origin", Origin.ToString());
        }
        public override void ReadXml(XmlNode node)
        {
            Offset = Xml.GetChildVector4Attributes(node, "Offset");
            LookAtAxis = Xml.GetChildEnumInnerText<Axis>(node, "LookAtAxis");
            UpAxis = Xml.GetChildEnumInnerText<Axis>(node, "UpAxis");
            Origin = Xml.GetChildEnumInnerText<Axis>(node, "Origin");
        }

        public override string ToString()
        {
            return base.ToString() + "  -  " + Offset + "   -   " + LookAtAxis + ", " + UpAxis + ", " + Origin;
        }
    }




    [TC(typeof(EXP))] public class ExpressionMotionDescriptionBlock : ResourceSystemBlock
    {
        public override long BlockLength => 0xA0;
        public ExpressionMotionDescription Motion { get; set; } = new();

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            Motion = new ExpressionMotionDescription();
            Motion.Read(reader);
        }
        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            (Motion ??= new ExpressionMotionDescription()).Write(writer);
        }
        public override string ToString() => Motion?.ToString() ?? base.ToString() ?? string.Empty;
    }

    [TC(typeof(EXP))] public class ExpressionMotionDescription
    {
        public Vector3 LinearStrength { get; set; }
        public Vector3 LinearDamping { get; set; }
        public Vector3 LinearMinConstraint { get; set; }
        public Vector3 LinearMaxConstraint { get; set; }
        public Vector3 AngularStrength { get; set; }
        public Vector3 AngularDamping { get; set; }
        public Vector3 AngularMinConstraint { get; set; }
        public Vector3 AngularMaxConstraint { get; set; }
        public Vector3 Direction { get; set; }
        public Vector3 Gravity { get; set; }
        public ushort BoneId { get; set; }

        public void Read(DataReader r)
        {
            LinearStrength = ReadPaddedVector(r);
            LinearDamping = ReadPaddedVector(r);
            LinearMinConstraint = ReadPaddedVector(r);
            LinearMaxConstraint = ReadPaddedVector(r);
            AngularStrength = ReadPaddedVector(r);
            AngularDamping = ReadPaddedVector(r);
            AngularMinConstraint = ReadPaddedVector(r);
            AngularMaxConstraint = ReadPaddedVector(r);
            Direction = ReadPaddedVector(r);
            Gravity = r.ReadVector3();
            BoneId = r.ReadUInt16();
            _ = r.ReadUInt16();
        }
        public void Write(DataWriter w)
        {
            WritePaddedVector(w, LinearStrength);
            WritePaddedVector(w, LinearDamping);
            WritePaddedVector(w, LinearMinConstraint);
            WritePaddedVector(w, LinearMaxConstraint);
            WritePaddedVector(w, AngularStrength);
            WritePaddedVector(w, AngularDamping);
            WritePaddedVector(w, AngularMinConstraint);
            WritePaddedVector(w, AngularMaxConstraint);
            WritePaddedVector(w, Direction);
            w.Write(Gravity);
            w.Write(BoneId);
            w.Write((ushort)0);
        }
        public void WriteXml(StringBuilder sb, int indent)
        {
            WriteVectorXml(sb, indent, "LinearStrength", LinearStrength);
            WriteVectorXml(sb, indent, "LinearDamping", LinearDamping);
            WriteVectorXml(sb, indent, "LinearMinConstraint", LinearMinConstraint);
            WriteVectorXml(sb, indent, "LinearMaxConstraint", LinearMaxConstraint);
            WriteVectorXml(sb, indent, "AngularStrength", AngularStrength);
            WriteVectorXml(sb, indent, "AngularDamping", AngularDamping);
            WriteVectorXml(sb, indent, "AngularMinConstraint", AngularMinConstraint);
            WriteVectorXml(sb, indent, "AngularMaxConstraint", AngularMaxConstraint);
            WriteVectorXml(sb, indent, "Direction", Direction);
            WriteVectorXml(sb, indent, "Gravity", Gravity);
            YedXml.ValueTag(sb, indent, "BoneId", BoneId.ToString());
        }
        public void ReadXml(XmlNode node)
        {
            LinearStrength = ReadVectorXml(node, "LinearStrength", "Vector01");
            LinearDamping = ReadVectorXml(node, "LinearDamping", "Vector02");
            LinearMinConstraint = ReadVectorXml(node, "LinearMinConstraint", "Vector03");
            LinearMaxConstraint = ReadVectorXml(node, "LinearMaxConstraint", "Vector04");
            AngularStrength = ReadVectorXml(node, "AngularStrength", "Vector05");
            AngularDamping = ReadVectorXml(node, "AngularDamping", "Vector06");
            AngularMinConstraint = ReadVectorXml(node, "AngularMinConstraint", "Vector07");
            AngularMaxConstraint = ReadVectorXml(node, "AngularMaxConstraint", "Vector08");
            Direction = ReadVectorXml(node, "Direction", "Vector09");
            Gravity = Xml.GetChildVector3Attributes(node, "Gravity");
            BoneId = (ushort)(node.SelectSingleNode("BoneId") != null
                ? Xml.GetChildUIntAttribute(node, "BoneId", "value")
                : Xml.GetChildUIntAttribute(node, "BoneTag", "value"));
        }
        public bool Compare(ExpressionMotionDescription other)
        {
            return other != null && LinearStrength == other.LinearStrength && LinearDamping == other.LinearDamping
                && LinearMinConstraint == other.LinearMinConstraint && LinearMaxConstraint == other.LinearMaxConstraint
                && AngularStrength == other.AngularStrength && AngularDamping == other.AngularDamping
                && AngularMinConstraint == other.AngularMinConstraint && AngularMaxConstraint == other.AngularMaxConstraint
                && Direction == other.Direction && Gravity == other.Gravity && BoneId == other.BoneId;
        }
        public ExpressionMotionDescription Clone() => (ExpressionMotionDescription)MemberwiseClone();
        public override string ToString() => BoneId.ToString();

        private static Vector3 ReadPaddedVector(DataReader r)
        {
            var value = r.ReadVector3();
            _ = r.ReadUInt32();
            return value;
        }
        private static void WritePaddedVector(DataWriter w, Vector3 value)
        {
            w.Write(value);
            w.Write(0u);
        }
        private static void WriteVectorXml(StringBuilder sb, int indent, string name, Vector3 value)
        {
            YedXml.SelfClosingTag(sb, indent, name + " " + FloatUtil.GetVector3XmlString(value));
        }
        private static Vector3 ReadVectorXml(XmlNode node, string name, string legacyName)
        {
            return Xml.GetChildVector3Attributes(node, node.SelectSingleNode(name) != null ? name : legacyName);
        }
    }


    public enum ExpressionTrackFormat : byte
    {
        Vector3 = 0,
        Quaternion = 1,
        Float = 2,
    }

    [TC(typeof(EXP))] public struct ExpressionTrack : IMetaXmlItem
    {
        public ushort BoneId { get; set; }
        public byte Track { get; set; }
        public byte Flags { get; set; }

        public ExpressionTrackFormat Format => (ExpressionTrackFormat)(Flags & 0x7F);
        public bool IsInput => (Flags & 0x80) != 0;
        public bool UnkFlag => IsInput;

        public void WriteXml(StringBuilder sb, int indent)
        {
            YedXml.ValueTag(sb, indent, "BoneId", BoneId.ToString());
            YedXml.ValueTag(sb, indent, "Track", Track.ToString());
            YedXml.StringTag(sb, indent, "Format", Format.ToString());
            YedXml.ValueTag(sb, indent, "IsInput", IsInput.ToString());
        }
        public void ReadXml(XmlNode node)
        {
            BoneId = (ushort)Xml.GetChildUIntAttribute(node, "BoneId");
            Track = (byte)Xml.GetChildUIntAttribute(node, "Track");
            var formatText = Xml.GetChildInnerText(node, "Format");
            var format = Enum.TryParse<ExpressionTrackFormat>(formatText, out var parsedFormat)
                ? (byte)parsedFormat
                : (byte)Xml.GetChildUIntAttribute(node, "Format");
            var isInput = node.SelectSingleNode("IsInput") != null
                ? Xml.GetChildBoolAttribute(node, "IsInput")
                : Xml.GetChildBoolAttribute(node, "UnkFlag");
            Flags = (byte)((format & 0x7F) | (isInput ? 0x80 : 0));
        }

        public override string ToString()
        {
            return BoneId + ", " + Track + ", " + Format + ", " + IsInput;
        }
    }


}
