using System;
using System.IO;
using System.Collections.Generic;
using TC = System.ComponentModel.TypeConverterAttribute;
using EXP = System.ComponentModel.ExpandableObjectConverter;
using System.Linq;
using System.Text;
using System.Xml;

namespace CodeWalker.GameFiles
{
    [TC(typeof(EXP))] public class MrfFile : GameFile, PackedFile
    {
        public const uint ExpectedMagic = 0x45566F4D; // 'MoVE'

        public byte[] RawFileData { get; set; } = [];
        public uint Magic { get; set; } = ExpectedMagic;
        public int VersionMajor { get; set; } = 2;
        public int VersionMinor { get; set; }
        public int VersionPatch { get; set; }
        public int VersionRevision { get; set; }
        public int DefinitionLength { get; set; }
        public int StringTableLength { get; set; }
        public int ExternalReferenceCount { get; set; }
        public uint RequestCount { get; set; }
        public uint FlagCount { get; set; }

        public MrfExternalReference[] ExternalReferences { get; set; } = [];
        public MrfMoveNetworkBit[] Requests { get; set; } = [];
        public MrfMoveNetworkBit[] Flags { get; set; } = [];
        public byte[] StringTable { get; set; } = [];

        public MrfNode[] AllNodes { get; set; } = [];
        public MrfNodeStateBase? RootState { get; set; }

        // DOT graphs to visualize the move network
        public string DebugTreeGraph { get; set; } = string.Empty;
        public string DebugStateGraph { get; set; } = string.Empty;

        public MrfFile() : base(null, GameFileType.Mrf)
        {
        }

        public MrfFile(RpfFileEntry entry) : base(entry, GameFileType.Mrf)
        {
            RpfFileEntry = entry;
        }

        public void Load(byte[] data, RpfFileEntry entry)
        {
            RawFileData = data;
            if (entry != null)
            {
                RpfFileEntry = entry;
                Name = entry.Name;
            }

            using (MemoryStream ms = new(data))
            {
                DataReader r = new(ms, Endianess.LittleEndian);

                Read(r);
            };
        }

        public byte[] Save()
        {
            MemoryStream s = new();
            DataWriter w = new(s);
            NoOpDataWriter nw = new();

            ExternalReferences ??= [];
            Requests ??= [];
            Flags ??= [];
            StringTable ??= [];
            foreach (var reference in ExternalReferences)
            {
                reference.Length = checked((uint)reference.Data.Length);
            }
            ExternalReferenceCount = ExternalReferences.Length;
            RequestCount = checked((uint)Requests.Length);
            FlagCount = checked((uint)Flags.Length);
            StringTableLength = StringTable.Length;

            Write(nw, updateOffsets: true); // first pass to calculate relative offsets
            var externalReferencesLength = ExternalReferences.Sum(reference => 4L + reference.Length);
            DefinitionLength = checked((int)(nw.Length - 32 - externalReferencesLength - StringTableLength));
            Write(w, updateOffsets: false); // now write the MRF

            var buf = new byte[s.Length];
            s.Position = 0;
            s.Read(buf, 0, buf.Length);
            RawFileData = buf;
            return buf;
        }

        private void Write(DataWriter w, bool updateOffsets)
        {
            if (Magic != ExpectedMagic || VersionMajor != 2 || VersionMinor != 0 || VersionRevision != 0)
                throw new InvalidOperationException("Failed to write MRF: incompatible MoVE header.");

            w.Write(Magic);
            w.Write(VersionMajor);
            w.Write(VersionMinor);
            w.Write(VersionPatch);
            w.Write(VersionRevision);
            w.Write(DefinitionLength);
            w.Write(StringTableLength);

            w.Write(ExternalReferenceCount);
            if (ExternalReferenceCount > 0)
            {
                foreach (var entry in ExternalReferences)
                {
                    w.Write(entry.Length);
                    w.Write(entry.Data);
                }
            }

            w.Write(RequestCount);
            if (RequestCount > 0)
            {
                foreach (var entry in Requests)
                {
                    w.Write(entry.Name);
                    w.Write(entry.BitPosition);
                }
            }

            w.Write(FlagCount);
            if (FlagCount > 0)
            {
                foreach (var entry in Flags)
                {
                    w.Write(entry.Name);
                    w.Write(entry.BitPosition);
                }
            }

            if (AllNodes != null)
            {
                foreach (var node in AllNodes)
                {
                    if (updateOffsets) node.FileOffset = (int)w.Position;
                    node.Write(w);
                    if (updateOffsets) node.FileDataSize = (int)(w.Position - node.FileOffset);
                }

                if (updateOffsets)
                {
                    foreach (var node in AllNodes)
                    {
                        node.UpdateRelativeOffsets();
                    }
                }
            }

            w.Write(StringTable);
        }

        private void Read(DataReader r)
        {
            Magic = r.ReadUInt32(); // Should be 'MoVE'
            VersionMajor = r.ReadInt32();
            VersionMinor = r.ReadInt32();
            VersionPatch = r.ReadInt32();
            VersionRevision = r.ReadInt32();
            DefinitionLength = r.ReadInt32();
            StringTableLength = r.ReadInt32();

            if (Magic != ExpectedMagic || VersionMajor != 2 || VersionMinor != 0 || VersionRevision != 0)
                throw new InvalidDataException("Failed to read MRF: incompatible MoVE header.");
            if (DefinitionLength < 8 || StringTableLength < 0)
                throw new InvalidDataException("Failed to read MRF: invalid data length.");

            ExternalReferenceCount = r.ReadInt32();
            if (ExternalReferenceCount < 0 || ExternalReferenceCount > (r.Length - r.Position) / 4)
                throw new InvalidDataException("Failed to read MRF: invalid external-reference count.");
            ExternalReferences = new MrfExternalReference[ExternalReferenceCount];
            for (int i = 0; i < ExternalReferenceCount; i++)
                ExternalReferences[i] = new MrfExternalReference(r);

            var definitionStart = r.Position;
            var definitionEnd = checked(definitionStart + DefinitionLength);
            if (definitionEnd > r.Length - StringTableLength)
                throw new InvalidDataException("Failed to read MRF: definition extends beyond the file.");

            RequestCount = r.ReadUInt32();
            if (RequestCount > (definitionEnd - r.Position) / 8)
                throw new InvalidDataException("Failed to read MRF: invalid request count.");
            Requests = new MrfMoveNetworkBit[RequestCount];
            for (int i = 0; i < RequestCount; i++)
                Requests[i] = new MrfMoveNetworkBit(r);

            FlagCount = r.ReadUInt32();
            if (FlagCount > (definitionEnd - r.Position) / 8)
                throw new InvalidDataException("Failed to read MRF: invalid flag count.");
            Flags = new MrfMoveNetworkBit[FlagCount];
            for (int i = 0; i < FlagCount; i++)
                Flags[i] = new MrfMoveNetworkBit(r);

            var nodes = new List<MrfNode>();

            while (r.Position < definitionEnd)
            {
                var index = nodes.Count;

                var node = ReadNode(r);
                if (node == null || r.Position > definitionEnd)
                    throw new InvalidDataException("Failed to read MRF: invalid node data.");

                node.FileIndex = index;
                nodes.Add(node);
            }

            AllNodes = nodes.ToArray();

            StringTable = r.ReadBytes(StringTableLength);

            RootState = AllNodes.Length > 0 ? (MrfNodeStateBase)AllNodes[0] : null; // the first node is always a state or state machine node (not inlined state machine)
            ResolveRelativeOffsets();

            DebugTreeGraph = DumpTreeGraph();
            DebugStateGraph = DumpStateGraph();

            if (r.Position != r.Length)
                throw new InvalidDataException($"Failed to read MRF ({r.Position} / {r.Length}).");
        }

        private MrfNode? ReadNode(DataReader r)
        {
            var startPos = r.Position;
            var nodeType = (MrfNodeType)r.ReadUInt16();
            r.Position = startPos;

            if (nodeType <= MrfNodeType.None || nodeType >= MrfNodeType.Max)
            {
                if (r.Position != r.Length)//should only be at EOF
                { }
                return null;
            }

            var node = CreateNode(nodeType);
            node.FileOffset = (int)startPos;
            node.Read(r);
            node.FileDataSize = (int)(r.Position - node.FileOffset);

            return node;
        }

        public static MrfNode CreateNode(MrfNodeType infoType)
        {
            switch (infoType)
            {
                case MrfNodeType.StateMachine:
                    return new MrfNodeStateMachine();
                case MrfNodeType.Tail:
                    return new MrfNodeTail();
                case MrfNodeType.InlinedStateMachine:
                    return new MrfNodeInlinedStateMachine();
                case MrfNodeType.Animation:
                    return new MrfNodeAnimation();
                case MrfNodeType.Blend:
                    return new MrfNodeBlend();
                case MrfNodeType.AddSubtract:
                    return new MrfNodeAddSubtract();
                case MrfNodeType.Filter:
                    return new MrfNodeFilter();
                case MrfNodeType.Mirror:
                    return new MrfNodeMirror();
                case MrfNodeType.Frame:
                    return new MrfNodeFrame();
                case MrfNodeType.Ik:
                    return new MrfNodeIk();
                case MrfNodeType.BlendN:
                    return new MrfNodeBlendN();
                case MrfNodeType.Clip:
                    return new MrfNodeClip();
                case MrfNodeType.Pm:
                    return new MrfNodePm();
                case MrfNodeType.Extrapolate:
                    return new MrfNodeExtrapolate();
                case MrfNodeType.Expression:
                    return new MrfNodeExpression();
                case MrfNodeType.Capture:
                    return new MrfNodeCapture();
                case MrfNodeType.Proxy:
                    return new MrfNodeProxy();
                case MrfNodeType.AddN:
                    return new MrfNodeAddN();
                case MrfNodeType.Identity:
                    return new MrfNodeIdentity();
                case MrfNodeType.Merge:
                    return new MrfNodeMerge();
                case MrfNodeType.Pose:
                    return new MrfNodePose();
                case MrfNodeType.MergeN:
                    return new MrfNodeMergeN();
                case MrfNodeType.State:
                    return new MrfNodeState();
                case MrfNodeType.Invalid:
                    return new MrfNodeInvalid();
                case MrfNodeType.JointLimit:
                    return new MrfNodeJointLimit();
                case MrfNodeType.SubNetwork:
                    return new MrfNodeSubNetwork();
                case MrfNodeType.Reference:
                    return new MrfNodeReference();
            }

            throw new Exception($"A handler for ({infoType}) mrf node type is not valid");
        }

        private void ResolveRelativeOffsets()
        {
            foreach (var n in AllNodes)
            {
                n.ResolveRelativeOffsets(this);
            }
        }

        public void WriteXml(StringBuilder sb, int indent)
        {
            MrfXml.WriteItemArray(sb, Requests.Where(t => !t.IsEndMarker).ToArray(), indent, "MoveNetworkTriggers");
            MrfXml.WriteItemArray(sb, Flags.Where(t => !t.IsEndMarker).ToArray(), indent, "MoveNetworkFlags");
            MrfXml.WriteNode(sb, indent, "RootState", RootState);
            MrfXml.WriteItemArray(sb, ExternalReferences, indent, "Unk1");
            MrfXml.WriteRawArray(sb, StringTable, indent, "UnkBytes", "", MrfXml.FormatHexByte, 16);
        }
        public void ReadXml(XmlNode node)
        {
            var triggers = XmlMeta.ReadItemArray<MrfMoveNetworkBit>(node, "MoveNetworkTriggers");
            var flags = XmlMeta.ReadItemArray<MrfMoveNetworkBit>(node, "MoveNetworkFlags");
            Requests = SortMoveNetworkBitsArray(triggers);
            Flags = SortMoveNetworkBitsArray(flags);
            RootState = (MrfNodeStateBase?)XmlMrf.ReadChildNode(node, "RootState");
            ExternalReferences = XmlMeta.ReadItemArrayNullable<MrfExternalReference>(node, "Unk1") ?? [];
            StringTable = Xml.GetChildRawByteArrayNullable(node, "UnkBytes") ?? [];
            RequestCount = (uint)Requests.Length;
            FlagCount = (uint)Flags.Length;
            ExternalReferenceCount = ExternalReferences.Length;
            StringTableLength = StringTable.Length;

            AllNodes = BuildNodesArray(RootState);

            // At this point the TargetStates of most transitions have been resolved by MrfNodeStateBase.ResolveXmlTargetStatesInTransitions
            // but there is one transition in onfoothuman.mrf with a target state not in the parent StateMachine (not really sure why,
            // it points to a state in a sibling tree), so ResolveXmlTargetStatesInTransitions can't find it.
            // The source state is node hash_76D78558 and the transition target state is hash_1836C818.
            // Iterate all transitions and try to resolve them again if target state is still null to solve this edge case.
            var stateNodes = AllNodes.OfType<MrfNodeStateBase>();
            foreach (var state in stateNodes)
            {
                if (state.Transitions == null) continue;
                
                foreach (var t in state.Transitions)
                {
                    if (t.TargetState != null) continue;

                    t.TargetState = stateNodes.FirstOrDefault(n => n.ID == t.XmlTargetStateName);
                }
            }
            
            DebugTreeGraph = DumpTreeGraph();
            DebugStateGraph = DumpStateGraph();
        }

        private static MrfNode[] BuildNodesArray(MrfNodeStateBase? root)
        {
            var nodes = new List<MrfNode>();
            AddRecursive(root);
            return nodes.ToArray();

            void AddRecursive(MrfNode? node)
            {
                if (node == null) return;
                nodes.Add(node);

                IEnumerable<MrfNode>? children = null;
                if (node is MrfNodeStateMachine sm)
                {
                    children = sm.States.Select(s => s.State).OfType<MrfNode>();
                }
                else if (node is MrfNodeInlinedStateMachine ism)
                {
                    children = ism.States.Select(s => s.State).OfType<MrfNode>();
                }
                else if (node is MrfNodeState ns)
                {
                    children = ns.GetChildren(excludeTailNodes: false);
                }

                if (children != null)
                {
                    foreach (var c in children.OrderBy(s => s is MrfNodeTail ? ushort.MaxValue : s.Index)) // NodeTail is placed after other nodes, their index is ignored
                    {
                        AddRecursive(c);
                    }
                }
            }
        }

        public MrfNode? FindNodeAtFileOffset(int fileOffset)
        {
            foreach (var n in AllNodes)
            {
                if (n.FileOffset == fileOffset) return n;
            }

            return null;
        }

        /// <summary>
        /// Finds the first clip which can be previewed below a network node.
        /// Parameter-driven branches cannot be evaluated without a live move-network context,
        /// so the traversal follows the serialized input order.
        /// </summary>
        public MrfNodeClip? FindPreviewClip(MrfNode? node)
        {
            var visited = new HashSet<MrfNode>();
            return FindPreviewClip(node, visited);
        }

        private static MrfNodeClip? FindPreviewClip(MrfNode? node, HashSet<MrfNode> visited)
        {
            if (node == null || !visited.Add(node)) return null;
            if (node is MrfNodeClip clip && clip.ClipType == MrfValueType.Literal) return clip;

            if (node is MrfNodeStateBase state)
            {
                var result = FindPreviewClip(state.InitialNode, visited);
                if (result != null) return result;
            }
            if (node is MrfNodeWithChildBase child)
                return FindPreviewClip(child.Input, visited);
            if (node is MrfNodePairBase pair)
                return FindPreviewClip(pair.Input0, visited) ?? FindPreviewClip(pair.Input1, visited);
            if (node is MrfNodeNBase many)
            {
                foreach (var input in many.Children)
                {
                    var result = FindPreviewClip(input, visited);
                    if (result != null) return result;
                }
            }

            return null;
        }

        public MrfMoveNetworkBit? FindMoveNetworkTriggerForBit(int bitPosition)
        {
            return FindMoveNetworkBitByBitPosition(Requests, bitPosition);
        }
        public MrfMoveNetworkBit? FindMoveNetworkFlagForBit(int bitPosition)
        {
            return FindMoveNetworkBitByBitPosition(Flags, bitPosition);
        }
        public static MrfMoveNetworkBit? FindMoveNetworkBitByBitPosition(MrfMoveNetworkBit[]? bits, int bitPosition)
        {
            if (bits == null)
            {
                return null;
            }

            foreach (var flag in bits)
            {
                if (!flag.IsEndMarker && flag.BitPosition == bitPosition) return flag;
            }

            return null;
        }

        // Request and flag getters by name for reference of how the arrays should be sorted in buckets
        public MrfMoveNetworkBit? FindMoveNetworkTriggerByName(MetaHash name)
        {
            return FindMoveNetworkBitByName(Requests, name);
        }
        public MrfMoveNetworkBit? FindMoveNetworkFlagByName(MetaHash name)
        {
            return FindMoveNetworkBitByName(Flags, name);
        }
        public static MrfMoveNetworkBit? FindMoveNetworkBitByName(MrfMoveNetworkBit[]? bits, MetaHash name)
        {
            if (bits is not { Length: > 0 })
            {
                return null;
            }

            for (int i = (int)(name.Hash % bits.Length); ; i = (i + 1) % bits.Length)
            {
                var b = bits[i];
                if (b.IsEndMarker) break;
                if (b.Name == name) return b;
            }

            return null;
        }

        public static MrfMoveNetworkBit[] SortMoveNetworkBitsArray(MrfMoveNetworkBit[]? bits)
        {
            if (bits is not { Length: > 0 })
            {
                return [];
            }

            var bitsSorted = new MrfMoveNetworkBit[bits.Length + 1]; // +1 for the end marker

            bits = bits.OrderBy(b => b.BitPosition).ToArray();
            for (int i = 0; i < bits.Length; i++)
            {
                var sortedIdx = bits[i].Name % bitsSorted.Length;
                while (bitsSorted[sortedIdx].Name != 0)
                {
                    sortedIdx = (sortedIdx + 1) % bitsSorted.Length;
                }
                bitsSorted[sortedIdx] = bits[i];
            }

            // place the end marker in the only empty slot left
            for (int i = 0; i < bitsSorted.Length; i++)
            {
                if (bitsSorted[i].Name == 0)
                {
                    bitsSorted[i] = MrfMoveNetworkBit.EndMarker;
                    break;
                }
            }

            return bitsSorted;
        }

        /// <summary>
        /// Dump a DOT graph with the whole node hierarchy as a tree.
        /// </summary>
        public string DumpTreeGraph()
        {
            using (var w = new StringWriter())
            {
                w.WriteLine($@"digraph ""{Name}"" {{");
                w.WriteLine($@"    label=""{Name}""");
                w.WriteLine($@"    labelloc=""t""");
                w.WriteLine($@"    concentrate=true");
                w.WriteLine($@"    rankdir=""LR""");
                w.WriteLine($@"    graph[fontname = ""Consolas""];");
                w.WriteLine($@"    edge[fontname = ""Consolas""];");
                w.WriteLine($@"    node[fontname = ""Consolas""];");
                w.WriteLine();

                w.WriteLine("    root [label=\"root\"];");

                // nodes
                foreach (var n in AllNodes)
                {
                    var id = n.FileOffset;
                    var label = $"{n.Type} '{n.ID}'";
                    w.WriteLine("    n{0} [label=\"{1}\"];", id, label);
                }
                w.WriteLine();

                // edges
                if (RootState != null) w.WriteLine("    n{0} -> root [color = black]", RootState.FileOffset);
                foreach (var n in AllNodes)
                {
                    MrfStateTransition[]? transitions = null;
                    MrfNode? initial = null;
                    if (n is MrfNodeStateBase sb)
                    {
                        initial = sb.InitialNode;
                        transitions = sb.Transitions;
                    }


                    if (n is MrfNodeInlinedStateMachine im)
                    {
                        if (im.FallbackNode != null) w.WriteLine("    n{1} -> n{0} [color = black, xlabel=\"fallback\"]", n.FileOffset, im.FallbackNode.FileOffset);
                    }

                    if (n is MrfNodeWithChildBase f)
                    {
                        if (f.Input != null) w.WriteLine("    n{1} -> n{0} [color = black, xlabel=\"input\"]", n.FileOffset, f.Input.FileOffset);
                    }

                    if (n is MrfNodePairBase p)
                    {
                        if (p.Input0 != null) w.WriteLine("    n{1} -> n{0} [color = black, xlabel=\"#0\"]", n.FileOffset, p.Input0.FileOffset);
                        if (p.Input1 != null) w.WriteLine("    n{1} -> n{0} [color = black, xlabel=\"#1\"]", n.FileOffset, p.Input1.FileOffset);
                    }

                    if (n is MrfNodeNBase nn && nn.Children != null)
                    {
                        for (int i = 0; i < nn.Children.Length; i++)
                        {
                            w.WriteLine("    n{1} -> n{0} [color = black, xlabel=\"#{2}\"]", n.FileOffset, nn.Children[i].FileOffset, i);
                        }
                    }

                    if (transitions != null)
                    {
                        foreach (var transition in transitions)
                        {
                            var conditions = transition.Conditions == null ? "[]" : "[" + string.Join(" & ", transition.Conditions.Select(c => c.ToExpressionString(this))) + "]";
                            var target = transition.TargetState;
                            if (target != null) w.WriteLine("    n{1} -> n{0} [color = black, xlabel=\"T {2}\"]", n.FileOffset, target.FileOffset, conditions);
                        }
                    }

                    if (initial != null)
                    {
                        w.WriteLine("    n{1} -> n{0} [color = black, xlabel=\"init\"]", n.FileOffset, initial.FileOffset);
                    }
                }

                // footer
                w.WriteLine("}");

                return w.ToString();
            }
        }

        /// <summary>
        /// Dump a DOT graph of the state machines where nodes are placed inside their corresponding state.
        /// </summary>
        public string DumpStateGraph()
        {
            using (var w = new StringWriter())
            {
                w.WriteLine($@"digraph ""{Name}"" {{");
                w.WriteLine($@"    label=""{Name}""");
                w.WriteLine($@"    labelloc=""t""");
                w.WriteLine($@"    concentrate=true");
                w.WriteLine($@"    compound=true");
                w.WriteLine($@"    rankdir=""LR""");
                w.WriteLine($@"    graph[fontname = ""Consolas""];");
                w.WriteLine($@"    edge[fontname = ""Consolas""];");
                w.WriteLine($@"    node[fontname = ""Consolas""];");
                w.WriteLine();

                DumpNode(RootState, w, null);

                w.WriteLine("    root [label=\"root\",shape=\"diamond\"];");
                if (RootState != null) w.WriteLine("    root -> S{0} [color = black][lhead=\"clusterS{0}\"]", RootState.FileOffset);

                // footer
                w.WriteLine("}");

                return w.ToString();
            }
        }

        private void DumpStateMachineSubGraph(MrfNodeStateMachine sm, TextWriter w)
        {
            // header
            w.WriteLine($@"subgraph ""clusterS{sm.FileOffset}"" {{");
            w.WriteLine($@"    label=""State Machine '{sm.ID}'""");
            w.WriteLine($@"    labelloc=""t""");
            w.WriteLine($@"    concentrate=true");
            w.WriteLine($@"    rankdir=""RL""");
            w.WriteLine($@"    S{sm.FileOffset}[shape=""none""][style=""invis""][label=""""]"); // hidden node to be able to connect subgraphs
            w.WriteLine();

            if (sm.States != null)
            {
                foreach (var state in sm.States)
                {
                    var stateNode = state.State;
                    if (stateNode is MrfNodeState ns) DumpStateSubGraph(ns, w);
                    if (stateNode is MrfNodeStateMachine nsm) DumpStateMachineSubGraph(nsm, w);
                    if (stateNode is MrfNodeInlinedStateMachine ism) DumpInlinedStateMachineSubGraph(ism, w);
                }

                foreach (var state in sm.States)
                {
                    DumpStateTransitionsGraph(state.State, w);
                }
            }

            w.WriteLine("    startS{0} [label=\"start\",shape=\"diamond\"];", sm.FileOffset);
            if (sm.InitialNode != null) w.WriteLine("    startS{0} -> S{1} [color = black][lhead=\"clusterS{1}\"]", sm.FileOffset, sm.InitialNode.FileOffset);

            // footer
            w.WriteLine("}");
        }

        private void DumpInlinedStateMachineSubGraph(MrfNodeInlinedStateMachine sm, TextWriter w)
        {
            // header
            w.WriteLine($@"subgraph ""clusterS{sm.FileOffset}"" {{");
            w.WriteLine($@"    label=""Inlined State Machine '{sm.ID}'""");
            w.WriteLine($@"    labelloc=""t""");
            w.WriteLine($@"    concentrate=true");
            w.WriteLine($@"    rankdir=""RL""");
            w.WriteLine($@"    S{sm.FileOffset}[shape=""none""][style=""invis""][label=""""]"); // hidden node to be able to connect subgraphs
            w.WriteLine();

            if (sm.States != null)
            {
                foreach (var state in sm.States)
                {
                    var stateNode = state.State;
                    if (stateNode is MrfNodeState ns) DumpStateSubGraph(ns, w);
                    if (stateNode is MrfNodeStateMachine nsm) DumpStateMachineSubGraph(nsm, w);
                    if (stateNode is MrfNodeInlinedStateMachine ism) DumpInlinedStateMachineSubGraph(ism, w);
                }

                foreach (var state in sm.States)
                {
                    DumpStateTransitionsGraph(state.State, w);
                }
            }

            w.WriteLine("    startS{0} [label=\"start\",shape=\"diamond\"];", sm.FileOffset);
            if (sm.InitialNode != null) w.WriteLine("    startS{0} -> S{1} [color = black][lhead=\"clusterS{1}\"]", sm.FileOffset, sm.InitialNode.FileOffset);

            if (sm.FallbackNode != null)
            {
                var fn = sm.FallbackNode;
                DumpNode(fn, w, null);

                w.WriteLine("    fallbackS{0} [label=\"fallback\",shape=\"diamond\"];", sm.FileOffset);
                if (fn is MrfNodeStateBase) w.WriteLine("    fallbackS{0} -> S{1} [color = black][lhead=\"clusterS{1}\"]", sm.FileOffset, fn.FileOffset);
                else w.WriteLine("    fallbackS{0} -> n{1} [color = black]", sm.FileOffset, fn.FileOffset);
            }

            // footer
            w.WriteLine("}");
        }

        private void DumpStateTransitionsGraph(MrfNodeStateBase? from, TextWriter w)
        {
            if (from == null) return;
            var transitions = from.Transitions;
            if (transitions != null)
            {
                int i = 0;
                foreach (var transition in transitions)
                {
                    var conditions = transition.Conditions == null ? "[]" : "[" + string.Join(" & ", transition.Conditions.Select(c => c.ToExpressionString(this))) + "]";
                    var target = transition.TargetState;
                    if (target != null) w.WriteLine("    S{0} -> S{1} [color = black, xlabel=\"T#{2} {3}\"][ltail=\"clusterS{0}\"][lhead=\"clusterS{1}\"]", from.FileOffset, target.FileOffset, i, conditions);
                    i++;
                }
            }
        }

        private void DumpStateSubGraph(MrfNodeStateBase state, TextWriter w)
        {
            if (state.InitialNode == null)
            {
                return;
            }

            // header
            w.WriteLine($@"subgraph clusterS{state.FileOffset} {{");
            w.WriteLine($@"    label=""State '{state.ID}'""");
            w.WriteLine($@"    labelloc=""t""");
            w.WriteLine($@"    concentrate=true");
            w.WriteLine($@"    rankdir=""LR""");
            w.WriteLine($@"    S{state.FileOffset}[shape=""none""][style=""invis""][label=""""]"); // hidden node to be able to connect subgraphs
            w.WriteLine();

            var initial = state.InitialNode;
            DumpNode(initial, w, null);

            w.WriteLine("    outputS{0} [label=\"output\",shape=\"point\"];", state.FileOffset);
            if (initial is MrfNodeStateBase) w.WriteLine("    S{0} -> outputS{1} [color = black][ltail=\"clusterS{0}\"]", initial.FileOffset, state.FileOffset);
            else w.WriteLine("    n{0} -> outputS{1} [color = black]", initial.FileOffset, state.FileOffset);

            // footer
            w.WriteLine("}");
        }

        private void DumpNodeGraph(MrfNode n, TextWriter w, HashSet<MrfNode> visitedNodes)
        {
            if (!visitedNodes.Add(n))
            {
                return;
            }

            var label = $"{n.Type} '{n.ID}'";
            if (n is MrfNodeSubNetwork sub)
            {
                label += $"\\n'{sub.SubNetworkParameterName}'";
            }

            w.WriteLine("    n{0} [label=\"{1}\"];", n.FileOffset, label);

            void writeConnection(MrfNode? target, string connectionLabel)
            {
                if (target == null) return;
                if (target is MrfNodeStateBase) w.WriteLine("    S{1} -> n{0} [color = black, xlabel=\"{2}\"][ltail=\"clusterS{1}\"]", n.FileOffset, target.FileOffset, connectionLabel);
                else w.WriteLine("    n{1} -> n{0} [color = black, xlabel=\"{2}\"]", n.FileOffset, target.FileOffset, connectionLabel);
            }

            if (n is MrfNodeInlinedStateMachine im)
            {
                DumpNode(im.FallbackNode, w, visitedNodes);
                writeConnection(im.FallbackNode, "fallback");
            }

            if (n is MrfNodeWithChildBase f)
            {
                DumpNode(f.Input, w, visitedNodes);
                writeConnection(f.Input, "");
            }

            if (n is MrfNodePairBase p)
            {
                DumpNode(p.Input0, w, visitedNodes);
                DumpNode(p.Input1, w, visitedNodes);
                writeConnection(p.Input0, "#0");
                writeConnection(p.Input1, "#1");
            }

            if (n is MrfNodeNBase nn && nn.Children != null)
            {
                for (int i = 0; i < nn.Children.Length; i++)
                {
                    DumpNode(nn.Children[i], w, visitedNodes);
                    writeConnection(nn.Children[i], $"#{i}");
                }
            }
        }

        private void DumpNode(MrfNode? n, TextWriter w, HashSet<MrfNode>? visitedNodes)
        {
            if (n == null) return;
            if (n is MrfNodeState ns) DumpStateSubGraph(ns, w);
            else if (n is MrfNodeStateMachine nsm) DumpStateMachineSubGraph(nsm, w);
            else if (n is MrfNodeInlinedStateMachine ism) DumpInlinedStateMachineSubGraph(ism, w);
            else DumpNodeGraph(n, w, visitedNodes ?? new HashSet<MrfNode>());
        }

        /// <summary>
        /// Writer used to calculate where the nodes will be placed, so the relative offsets can be calculated before using the real writer.
        /// </summary>
        private class NoOpDataWriter : DataWriter
        {
            private long length;
            private long position;

            public override long Length => length;
            public override long Position { get => position; set => position = value; }

            public NoOpDataWriter() : base(Stream.Null, Endianess.LittleEndian)
            {
            }

            protected override void WriteToStream(ReadOnlySpan<byte> value, bool ignoreEndianess = false)
            {
                position += value.Length;
                length = Math.Max(length, position);
            }
        }
    }



    // Unused node indexes by GTAV: 11, 12, 14, 16
    // Exist in GTAV but not used in MRFs: 4, 8, 10, 17, 21, 22, 28, 29, 31, 32
    public enum MrfNodeType : ushort
    {
        None = 0,
        StateMachine = 1,
        Tail = 2,
        InlinedStateMachine = 3,
        Animation = 4,
        Blend = 5,
        AddSubtract = 6,
        Filter = 7,
        Mirror = 8,
        Frame = 9,
        Ik = 10,
        BlendN = 13,
        Clip = 15,
        Pm = 17,
        Extrapolate = 18,
        Expression = 19,
        Capture = 20,
        Proxy = 21,
        AddN = 22,
        Identity = 23,
        Merge = 24,
        Pose = 25,
        MergeN = 26,
        State = 27,
        Invalid = 28,
        JointLimit = 29,
        SubNetwork = 30,
        Reference = 31,
        Max = 32
    }

    public enum MrfNodeParameterId : ushort // node parameter IDs are specific to the node type
    {
        // StateMachine
        // none

        // Tail
        // none

        // InlinedStateMachine
        // none

        // Animation
        Animation_Animation = 0, // rage::crAnimation
        Animation_Phase = 1,     // float
        Animation_Rate = 2,      // float
        Animation_Delta = 3,     // float
        Animation_Looped = 4,    // bool
        Animation_Absolute = 5,  // bool

        // Blend
        Blend_Filter = 0, // rage::crFrameFilter
        Blend_Weight = 1, // float

        // AddSubtract
        AddSubtract_Filter = 0, // rage::crFrameFilter
        AddSubtract_Weight = 1, // float

        // Filter
        Filter_Filter = 0, // rage::crFrameFilter

        // Mirror
        Mirror_Filter = 0, // rage::crFrameFilter

        // Frame
        Frame_Frame = 0, // rage::crFrame
        Frame_Owner = 1, // exporter-only parameter

        // Ik
        // none

        // BlendN
        BlendN_FilterN = 0,      // rage::crFrameFilterN (no direct getter/setter)
        BlendN_Filter = 1,       // rage::crFrameFilter
        BlendN_Weight = 2,       // float (extra arg is the child index)
        BlendN_InputFilter = 3,  // rage::crFrameFilter (extra arg is the child index)

        // Clip
        Clip_Clip = 0,   // rage::crClip
        Clip_Phase = 1,  // float
        Clip_Rate = 2,   // float
        Clip_Delta = 3,  // float
        Clip_Looped = 4, // bool
        Clip_Property = 5, // float (getter only; extra arg is the property name)

        // Pm
        Pm_ParameterizedMotion = 0, // rage::crpmMotion
        Pm_Phase = 1,               // float
        Pm_Delta = 2,               // float
        Pm_Rate = 3,                // float
        Pm_ParameterValue = 4,      // float (extra arg is the parameter index)

        // Extrapolate
        // The exporter defines all six IDs; the runtime getter table only exposes a mismatched subset.
        Extrapolate_Damping = 0,       // float
        Extrapolate_LastFrame = 1,     // rage::crFrame
        Extrapolate_OwnLastFrame = 2,  // bool
        Extrapolate_DeltaFrame = 3,    // rage::crFrame
        Extrapolate_OwnDeltaFrame = 4, // bool
        Extrapolate_DeltaTime = 5,     // float

        // Expression
        Expression_Expressions = 0, // rage::crExpressions
        Expression_Weight = 1,     // float
        Expression_Variable = 2,   // float (extra arg is the variable name)

        // Capture
        Capture_Frame = 0, // rage::crFrame

        // Proxy
        Proxy_Node = 0, // rage::crmtNode (the type resolver returns rage::crmtObserver but the getter/setter expect a rage::crmtNode, R* bug?)

        // AddN
        AddN_FilterN = 0,      // rage::crFrameFilterN (no direct getter/setter)
        AddN_Filter = 1,       // rage::crFrameFilter
        AddN_Weight = 2,       // float (extra arg is the child index)
        AddN_InputFilter = 3,  // rage::crFrameFilter (extra arg is the child index)

        // Identity
        // none

        // Merge
        Merge_Filter = 0, // rage::crFrameFilter

        // Pose
        Pose_IsNormalized = 0, // bool (getter hardcoded to true, setter does nothing)

        // MergeN
        MergeN_FilterN = 0,      // rage::crFrameFilterN (no direct getter/setter)
        MergeN_Filter = 1,       // rage::crFrameFilter
        MergeN_Weight = 2,       // float (extra arg is the child index)
        MergeN_InputFilter = 3,  // rage::crFrameFilter (extra arg is the child index)

        // State
        // none

        // Invalid
        // none

        // JointLimit
        JointLimit_Filter = 0, // rage::crFrameFilter (only setter exists)

        // SubNetwork
        // none

        // Reference
        // none
    }

    public enum MrfNodeEventId : ushort // node event IDs are specific to the node type
    {
        // StateMachine
        // none

        // Tail
        // none

        // InlinedStateMachine
        // none

        // Animation
        Animation_AnimationLooped = 0,
        Animation_AnimationEnded = 1,

        // Blend
        // none

        // AddSubtract
        // none

        // Filter
        // none

        // Mirror
        // none

        // Frame
        // none

        // Ik
        // none

        // BlendN
        // none

        // Clip
        Clip_ClipLooped = 0,
        Clip_ClipEnded = 1,
        Clip_ClipTagEnter = 2,
        Clip_ClipTagExit = 3,
        Clip_ClipTagUpdate = 4,

        // Pm
        Pm_PmLooped = 0,
        Pm_PmEnded = 1,

        // Extrapolate
        // none

        // Expression
        // none

        // Capture
        // none

        // Proxy
        // none

        // AddN
        // none

        // Identity
        // none

        // Merge

        // Pose
        // none

        // MergeN
        // none

        // State
        // none

        // Invalid
        // none

        // JointLimit
        // none

        // SubNetwork
        // none

        // Reference
        // none
    }

#region mrf node abstractions

    [TC(typeof(EXP))] public abstract class MrfNode : IMetaXmlItem
    {
        public MrfNodeType Type { get; set; }
        public ushort Index { get; set; } // index in the parent state's children array
        public MetaHash ID { get; set; } // unique ID generated from the node name

        public int FileIndex { get; set; } //index in the file
        public int FileOffset { get; set; } //offset in the file
        public int FileDataSize { get; set; } //number of bytes read from the file (this node only)

        protected MrfNode(MrfNodeType type)
        {
            Type = type;
        }

        public virtual void Read(DataReader r)
        {
            Type = (MrfNodeType)r.ReadUInt16();
            Index = r.ReadUInt16();
            ID = r.ReadUInt32();
        }

        public virtual void Write(DataWriter w)
        {
            w.Write((ushort)Type);
            w.Write(Index);
            w.Write(ID);
        }

        public virtual void ReadXml(XmlNode node)
        {
            ID = XmlMeta.GetHash(Xml.GetChildInnerText(node, "Name"));
            Index = (ushort)Xml.GetChildUIntAttribute(node, "NodeIndex");
        }

        public virtual void WriteXml(StringBuilder sb, int indent)
        {
            MrfXml.StringTag(sb, indent, "Name", MrfXml.HashString(ID));
            MrfXml.ValueTag(sb, indent, "NodeIndex", Index.ToString());
        }

        public override string ToString()
        {
            return Type + " - " + Index + " - " + ID;
        }

        public virtual void ResolveRelativeOffsets(MrfFile mrf)
        {
        }

        public virtual void UpdateRelativeOffsets()
        {
        }
    }

    [TC(typeof(EXP))] public abstract class MrfNodeWithFlagsBase : MrfNode
    {
        public uint Flags { get; set; }

        protected MrfNodeWithFlagsBase(MrfNodeType type) : base(type) { }

        public override void Read(DataReader r)
        {
            base.Read(r);
            Flags = r.ReadUInt32();
        }

        public override void Write(DataWriter w)
        {
            base.Write(w);
            w.Write(Flags);
        }

        protected uint GetFlagsSubset(int bitOffset, uint mask)
        {
            return (Flags >> bitOffset) & mask;
        }

        protected void SetFlagsSubset(int bitOffset, uint mask, uint value)
        {
            Flags = (Flags & ~(mask << bitOffset)) | ((value & mask) << bitOffset);
        }

        public override string ToString()
        {
            return base.ToString() + " - " + Flags.ToString("X8");
        }
    }

    [TC(typeof(EXP))] public abstract class MrfNodeStateBase : MrfNode
    {
        public int InitialOffset { get; set; } // offset from the start of this field
        public int InitialFileOffset { get; set; }
        public uint DeferBlockUpdate { get; set; } // logical boolean stored as u32 for alignment
        public bool OnEnterEnabled { get; set; }
        public bool OnExitEnabled { get; set; }
        public byte ChildCount { get; set; } // states for state machines; non-tail children for states
        public byte TransitionCount { get; set; }
        public MetaHash OnEnterID { get; set; } // inserted as true when the network enters this node
        public MetaHash OnExitID { get; set; } // inserted as true when the network leaves this node
        public int TransitionsOffset { get; set; } // offset from the start of this field
        public int TransitionsFileOffset { get; set; }

        public MrfNode? InitialNode { get; set; } // for Node(Inlined)StateMachine this is a NodeStateBase, for NodeState it can be any node
        public MrfStateTransition[] Transitions { get; set; } = [];

        protected MrfNodeStateBase(MrfNodeType type) : base(type) { }

        public override void Read(DataReader r)
        {
            base.Read(r);
            InitialOffset = r.ReadInt32();
            InitialFileOffset = (int)(r.Position + InitialOffset - 4);
            DeferBlockUpdate = r.ReadUInt32();
            OnEnterEnabled = r.ReadByte() != 0;
            OnExitEnabled = r.ReadByte() != 0;
            ChildCount = r.ReadByte();
            TransitionCount = r.ReadByte();
            OnEnterID = r.ReadUInt32();
            OnExitID = r.ReadUInt32();
            TransitionsOffset = r.ReadInt32();
            TransitionsFileOffset = (int)(r.Position + TransitionsOffset - 4);
        }

        public override void Write(DataWriter w)
        {
            base.Write(w);
            w.Write(InitialOffset);
            w.Write(DeferBlockUpdate);
            w.Write((byte)(OnEnterEnabled ? 1 : 0));
            w.Write((byte)(OnExitEnabled ? 1 : 0));
            w.Write(ChildCount);
            w.Write(TransitionCount);
            w.Write(OnEnterID);
            w.Write(OnExitID);
            w.Write(TransitionsOffset);
        }

        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            DeferBlockUpdate = Xml.GetChildUIntAttribute(node, "DeferBlockUpdate");
            if (node.SelectSingleNode("DeferBlockUpdate") == null)
                DeferBlockUpdate = Xml.GetChildUIntAttribute(node, "StateUnk3");
            OnEnterID = XmlMeta.GetHash(Xml.GetChildInnerText(node, "EntryParameterName"));
            OnExitID = XmlMeta.GetHash(Xml.GetChildInnerText(node, "ExitParameterName"));
            OnEnterEnabled = OnEnterID != 0;
            OnExitEnabled = OnExitID != 0;
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            MrfXml.ValueTag(sb, indent, "DeferBlockUpdate", DeferBlockUpdate.ToString());
            if (OnEnterEnabled) MrfXml.StringTag(sb, indent, "EntryParameterName", MrfXml.HashString(OnEnterID));
            if (OnExitEnabled) MrfXml.StringTag(sb, indent, "ExitParameterName", MrfXml.HashString(OnExitID));
        }

        public override void ResolveRelativeOffsets(MrfFile mrf)
        {
            base.ResolveRelativeOffsets(mrf);

            var initNode = mrf.FindNodeAtFileOffset(InitialFileOffset);
            if (initNode == null)
                throw new InvalidDataException($"Movement initial node at file offset {InitialFileOffset} could not be resolved.");

            if ((this is MrfNodeStateMachine || this is MrfNodeInlinedStateMachine) && !(initNode is MrfNodeStateBase))
                throw new InvalidDataException("Movement state-machine initial nodes must also be state nodes.");

            InitialNode = initNode;
        }

        public override void UpdateRelativeOffsets()
        {
            base.UpdateRelativeOffsets();

            InitialFileOffset = (InitialNode ?? throw new InvalidDataException("A movement node reference is unresolved.")).FileOffset;
            InitialOffset = InitialFileOffset - (FileOffset + 0x8);
        }

        protected void ResolveNodeOffsetsInTransitions(MrfStateTransition[]? transitions, MrfFile mrf)
        {
            if (transitions == null)
            {
                return;
            }

            foreach (var t in transitions)
            {
                var node = mrf.FindNodeAtFileOffset(t.TargetStateFileOffset);
                if (node == null)
                    throw new InvalidDataException($"Movement transition target at file offset {t.TargetStateFileOffset} could not be resolved.");

                if (!(node is MrfNodeStateBase))
                    throw new InvalidDataException("Movement transition targets must be state nodes.");

                t.TargetState = (MrfNodeStateBase?)node;
            }
        }

        protected void ResolveNodeOffsetsInStates(MrfStateRef[]? states, MrfFile mrf)
        {
            if (states == null)
            {
                return;
            }

            foreach (var s in states)
            {
                var node = mrf.FindNodeAtFileOffset(s.StateFileOffset);
                if (node == null)
                    throw new InvalidDataException($"Movement state at file offset {s.StateFileOffset} could not be resolved.");

                if (!(node is MrfNodeStateBase))
                    throw new InvalidDataException("Movement state references must target state nodes.");

                s.State = (MrfNodeStateBase?)node;
            }
        }

        protected int UpdateNodeOffsetsInTransitions(MrfStateTransition[]? transitions, int transitionsArrayOffset, bool offsetSetToZeroIfNoTransitions)
        {
            int offset = transitionsArrayOffset;
            TransitionsFileOffset = offset;
            TransitionsOffset = TransitionsFileOffset - (FileOffset + 0x1C);
            if (transitions != null)
            {
                foreach (var transition in transitions)
                {
                    transition.TargetStateFileOffset = (transition.TargetState ?? throw new InvalidDataException("A movement node reference is unresolved.")).FileOffset;
                    transition.TargetStateOffset = transition.TargetStateFileOffset - (offset + 0x14);
                    transition.CalculateSize();
                    offset += (int)transition.Size;
                }
            }
            else if (offsetSetToZeroIfNoTransitions)
            {
                // Special case for some MrfNodeStateMachines with no transitions and MrfNodeInlinedStateMachines.
                // Unlike MrfNodeState, when these don't have transtions the TransititionsOffset is 0.
                // So we set it to 0 too to be able to compare Save() result byte by byte
                TransitionsOffset = 0;
                TransitionsFileOffset = FileOffset + 0x1C; // and keep FileOffset consistent with what Read() does
            }
            return offset;
        }

        protected int UpdateNodeOffsetsInStates(MrfStateRef[] states, int statesArrayOffset)
        {
            int offset = statesArrayOffset;
            if (states != null)
            {
                foreach (var state in states)
                {
                    state.StateFileOffset = (state.State ?? throw new InvalidDataException("A movement node reference is unresolved.")).FileOffset;
                    state.StateOffset = state.StateFileOffset - (offset + 4);
                    offset += 8; // sizeof(MrfStructStateMachineStateRef)
                }
            }
            return offset;
        }

        protected void ResolveXmlTargetStatesInTransitions(MrfStateRef[]? states)
        {
            if (states == null)
            {
                return;
            }

            foreach (var state in states)
            {
                if (state.State?.Transitions == null) continue;

                foreach (var t in state.State.Transitions)
                {
                    t.TargetState = states.FirstOrDefault(s => s.StateName == t.XmlTargetStateName)?.State;
                    if (t.TargetState == null)
                    { } // only 1 hit in onfoothuman.mrf, solved at the end of MrfFile.ReadXml
                }
            }
        }

        public override string ToString()
        {
            return base.ToString()
                + " - Init:" + InitialOffset.ToString()
                + " - CC:" + ChildCount.ToString()
                + " - TC:" + TransitionCount.ToString()
                + " - DeferBlockUpdate:" + DeferBlockUpdate.ToString()
                + " - OnEnter(" + OnEnterEnabled.ToString() + "):" + OnEnterID.ToString()
                + " - OnExit(" + OnExitEnabled.ToString() + "):" + OnExitID.ToString()
                + " - TO:" + TransitionsOffset.ToString();
        }
    }

    [TC(typeof(EXP))] public abstract class MrfNodePairBase : MrfNodeWithFlagsBase
    {
        // rage::mvNodePairDef stores these as m_Inputs[2]. mvNodeMergeDef uses the same serialized prefix.
        public int Input0Offset { get; set; }
        public int Input0FileOffset { get; set; }
        public int Input1Offset { get; set; }
        public int Input1FileOffset { get; set; }

        public MrfNode? Input0 { get; set; }
        public MrfNode? Input1 { get; set; }

        protected MrfNodePairBase(MrfNodeType type) : base(type) { }

        public override void Read(DataReader r)
        {
            base.Read(r);

            Input0Offset = r.ReadInt32();
            Input0FileOffset = checked((int)(r.Position + Input0Offset - 4));
            Input1Offset = r.ReadInt32();
            Input1FileOffset = checked((int)(r.Position + Input1Offset - 4));
        }

        public override void Write(DataWriter w)
        {
            base.Write(w);

            w.Write(Input0Offset);
            w.Write(Input1Offset);
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            MrfXml.WriteNode(sb, indent, "Child0", Input0);
            MrfXml.WriteNode(sb, indent, "Child1", Input1);
        }

        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            Input0 = XmlMrf.ReadChildNode(node, "Child0");
            Input1 = XmlMrf.ReadChildNode(node, "Child1");
        }

        public override void ResolveRelativeOffsets(MrfFile mrf)
        {
            base.ResolveRelativeOffsets(mrf);

            var input0 = mrf.FindNodeAtFileOffset(Input0FileOffset);
            var input1 = mrf.FindNodeAtFileOffset(Input1FileOffset);

            Input0 = input0;
            Input1 = input1;
        }

        public override void UpdateRelativeOffsets()
        {
            base.UpdateRelativeOffsets();

            Input0FileOffset = (Input0 ?? throw new InvalidDataException("Movement node input 0 is unresolved.")).FileOffset;
            Input0Offset = Input0FileOffset - (FileOffset + 0xC);
            Input1FileOffset = (Input1 ?? throw new InvalidDataException("Movement node input 1 is unresolved.")).FileOffset;
            Input1Offset = Input1FileOffset - (FileOffset + 0x10);
        }
    }

    public enum MrfSynchronizerType
    {
        Phase = 0, // attaches a rage::mvSynchronizerPhase instance to the node
        Tag = 1,   // attaches a rage::mvSynchronizerTag instance and serializes its tag bit field
        None = 2,
    }

    [Flags] public enum MrfSynchronizerTagFlags : uint
    {
        LeftFootHeel = 0x20,  // adds rage::mvSynchronizerTag::sm_LeftFootHeel to a rage::mvSynchronizerTag instance attached to the node
        RightFootHeel = 0x40, // same but with rage::mvSynchronizerTag::sm_RightFootHeel
    }

    public enum MrfValueType
    {
        None = 0,
        Literal = 1,   // specific value (in case of expressions, clips or filters, a dictionary/name hash pair)
        Parameter = 2, // lookup value in the network parameters
    }

    public enum MrfInfluenceOverride
    {
        None = 0, // influence affected by weight (at least in NodeBlend case)
        Zero = 1, // influence = 0.0
        One  = 2, // influence = 1.0
    }

    [TC(typeof(EXP))] public abstract class MrfNodePairWeightedBase : MrfNodePairBase
    {
        // Optional data serialized after the fixed rage::mvNodePairDef header.
        public MrfSynchronizerTagFlags SynchronizerTag { get; set; }
        public float Weight { get; set; }
        public MetaHash WeightParameterName { get; set; }
        public MetaHash FilterDictionaryName { get; set; }
        public MetaHash FilterName { get; set; }
        public MetaHash FilterParameterName { get; set; }

        public MrfValueType WeightType
        {
            get => (MrfValueType)GetFlagsSubset(0, 3);
            set => SetFlagsSubset(0, 3, (uint)value);
        }
        public MrfValueType FilterType
        {
            get => (MrfValueType)GetFlagsSubset(2, 3);
            set => SetFlagsSubset(2, 3, (uint)value);
        }
        public bool Transitional
        {
            get => GetFlagsSubset(6, 1) != 0;
            set => SetFlagsSubset(6, 1, value ? 1 : 0u);
        }
        public bool Source0Immutable
        {
            get => GetFlagsSubset(7, 1) != 0;
            set => SetFlagsSubset(7, 1, value ? 1u : 0u);
        }
        public bool Source1Immutable
        {
            get => GetFlagsSubset(8, 1) != 0;
            set => SetFlagsSubset(8, 1, value ? 1u : 0u);
        }
        public MrfInfluenceOverride Source0InfluenceOverride
        {
            get => (MrfInfluenceOverride)GetFlagsSubset(12, 3);
            set => SetFlagsSubset(12, 3, (uint)value);
        }
        public MrfInfluenceOverride Source1InfluenceOverride
        {
            get => (MrfInfluenceOverride)GetFlagsSubset(14, 3);
            set => SetFlagsSubset(14, 3, (uint)value);
        }
        public MrfSynchronizerType SynchronizerType
        {
            get => (MrfSynchronizerType)GetFlagsSubset(19, 3);
            set => SetFlagsSubset(19, 3, (uint)value);
        }
        public bool MergeBlend
        {
            get => GetFlagsSubset(31, 1) != 0;
            set => SetFlagsSubset(31, 1, value ? 1 : 0u);
        }

        protected MrfNodePairWeightedBase(MrfNodeType type) : base(type) { }

        public override void Read(DataReader r)
        {
            base.Read(r);
            ValidateFlags();

            if (SynchronizerType == MrfSynchronizerType.Tag)
                SynchronizerTag = (MrfSynchronizerTagFlags)r.ReadUInt32();

            switch (WeightType)
            {
                case MrfValueType.Literal:
                    Weight = r.ReadSingle();
                    break;
                case MrfValueType.Parameter:
                    WeightParameterName = r.ReadUInt32();
                    break;
            }

            switch (FilterType)
            {
                case MrfValueType.Literal:
                    FilterDictionaryName = r.ReadUInt32();
                    FilterName = r.ReadUInt32();
                    break;
                case MrfValueType.Parameter:
                    FilterParameterName = r.ReadUInt32();
                    break;
            }
        }

        public override void Write(DataWriter w)
        {
            ValidateFlags();
            base.Write(w);

            if (SynchronizerType == MrfSynchronizerType.Tag)
                w.Write((uint)SynchronizerTag);

            switch (WeightType)
            {
                case MrfValueType.Literal:
                    w.Write(Weight);
                    break;
                case MrfValueType.Parameter:
                    w.Write(WeightParameterName);
                    break;
            }

            switch (FilterType)
            {
                case MrfValueType.Literal:
                    w.Write(FilterDictionaryName);
                    w.Write(FilterName);
                    break;
                case MrfValueType.Parameter:
                    w.Write(FilterParameterName);
                    break;
            }
        }

        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);

            Source0InfluenceOverride = Xml.GetChildEnumInnerText<MrfInfluenceOverride>(node, "Child0InfluenceOverride");
            Source1InfluenceOverride = Xml.GetChildEnumInnerText<MrfInfluenceOverride>(node, "Child1InfluenceOverride");
            (WeightType, Weight, WeightParameterName) = XmlMrf.GetChildParameterizedFloat(node, "Weight");
            (FilterType, FilterDictionaryName, FilterName, FilterParameterName) = XmlMrf.GetChildParameterizedAsset(node, "FrameFilter");
            SynchronizerType = Xml.GetChildEnumInnerText<MrfSynchronizerType>(node, "SynchronizerType");
            if (SynchronizerType == MrfSynchronizerType.Tag)
            {
                SynchronizerTag = Xml.GetChildEnumInnerText<MrfSynchronizerTagFlags>(node, "SynchronizerTagFlags");
            }
            MergeBlend = Xml.GetChildBoolAttribute(node, "MergeBlend");
            Transitional = Xml.GetChildBoolAttribute(node, "Transitional") || Xml.GetChildBoolAttribute(node, "UnkFlag6");
            var legacyImmutable = Xml.GetChildUIntAttribute(node, "UnkFlag7");
            Source0Immutable = Xml.GetChildBoolAttribute(node, "Source0Immutable") || (legacyImmutable & 1) != 0;
            Source1Immutable = Xml.GetChildBoolAttribute(node, "Source1Immutable") || (legacyImmutable & 2) != 0;
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);

            MrfXml.StringTag(sb, indent, "Child0InfluenceOverride", Source0InfluenceOverride.ToString());
            MrfXml.StringTag(sb, indent, "Child1InfluenceOverride", Source1InfluenceOverride.ToString());
            MrfXml.ParameterizedFloatTag(sb, indent, "Weight", WeightType, Weight, WeightParameterName);
            MrfXml.ParameterizedAssetTag(sb, indent, "FrameFilter", FilterType, FilterDictionaryName, FilterName, FilterParameterName);
            MrfXml.StringTag(sb, indent, "SynchronizerType", SynchronizerType.ToString());
            if (SynchronizerType == MrfSynchronizerType.Tag)
            {
                MrfXml.StringTag(sb, indent, "SynchronizerTagFlags", SynchronizerTag.ToString());
            }
            MrfXml.ValueTag(sb, indent, "MergeBlend", MergeBlend.ToString());
            MrfXml.ValueTag(sb, indent, "Transitional", Transitional.ToString());
            MrfXml.ValueTag(sb, indent, "Source0Immutable", Source0Immutable.ToString());
            MrfXml.ValueTag(sb, indent, "Source1Immutable", Source1Immutable.ToString());
        }

        private void ValidateFlags()
        {
            if (WeightType > MrfValueType.Parameter || FilterType > MrfValueType.Parameter || SynchronizerType > MrfSynchronizerType.None)
                throw new InvalidDataException("Movement pair node flags contain an invalid value type or synchronizer.");
        }
    }

    [TC(typeof(EXP))] public abstract class MrfNodeWithChildBase : MrfNodeWithFlagsBase
    {
        public int InputOffset { get; set; }
        public int InputFileOffset { get; set; }

        public MrfNode? Input { get; set; }

        protected MrfNodeWithChildBase(MrfNodeType type) : base(type) { }

        public override void Read(DataReader r)
        {
            base.Read(r);

            InputOffset = r.ReadInt32();
            InputFileOffset = checked((int)(r.Position + InputOffset - 4));
        }

        public override void Write(DataWriter w)
        {
            base.Write(w);

            w.Write(InputOffset);
        }

        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            Input = XmlMrf.ReadChildNode(node, "Child");
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            MrfXml.WriteNode(sb, indent, "Child", Input);
        }

        public override void ResolveRelativeOffsets(MrfFile mrf)
        {
            base.ResolveRelativeOffsets(mrf);

            var node = mrf.FindNodeAtFileOffset(InputFileOffset);
            if (node == null)
                throw new InvalidDataException($"Movement child node at file offset {InputFileOffset} could not be resolved.");

            Input = node;
        }

        public override void UpdateRelativeOffsets()
        {
            base.UpdateRelativeOffsets();

            InputFileOffset = (Input ?? throw new InvalidDataException("A movement node input is unresolved.")).FileOffset;
            InputOffset = InputFileOffset - (FileOffset + 0xC);
        }
    }

    [TC(typeof(EXP))] public abstract class MrfNodeWithChildAndFilterBase : MrfNodeWithChildBase
    {
        public MetaHash FilterDictionaryName { get; set; }
        public MetaHash FilterName { get; set; }
        public MetaHash FilterParameterName { get; set; }

        public MrfValueType FilterType
        {
            get => (MrfValueType)GetFlagsSubset(0, 3);
            set => SetFlagsSubset(0, 3, (uint)value);
        }

        protected MrfNodeWithChildAndFilterBase(MrfNodeType type) : base(type) { }

        public override void Read(DataReader r)
        {
            base.Read(r);
            ValidateFilterType();

            switch (FilterType)
            {
                case MrfValueType.Literal:
                    FilterDictionaryName = r.ReadUInt32();
                    FilterName = r.ReadUInt32();
                    break;
                case MrfValueType.Parameter:
                    FilterParameterName = r.ReadUInt32();
                    break;
            }
        }

        public override void Write(DataWriter w)
        {
            ValidateFilterType();
            base.Write(w);

            switch (FilterType)
            {
                case MrfValueType.Literal:
                    w.Write(FilterDictionaryName);
                    w.Write(FilterName);
                    break;
                case MrfValueType.Parameter:
                    w.Write(FilterParameterName);
                    break;
            }
        }

        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            (FilterType, FilterDictionaryName, FilterName, FilterParameterName) = XmlMrf.GetChildParameterizedAsset(node, "FrameFilter");
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            MrfXml.ParameterizedAssetTag(sb, indent, "FrameFilter", FilterType, FilterDictionaryName, FilterName, FilterParameterName);
        }

        private void ValidateFilterType()
        {
            if (FilterType > MrfValueType.Parameter)
                throw new InvalidDataException("Movement node flags contain an invalid filter type.");
        }
    }

    [TC(typeof(EXP))] public abstract class MrfNodeNBase : MrfNodeWithFlagsBase
    {
        public MrfSynchronizerTagFlags SynchronizerTag { get; set; }
        public float[] FilterNValues { get; set; } = [];
        public MetaHash FilterNParameterName { get; set; }
        public MetaHash FilterDictionaryName { get; set; }
        public MetaHash FilterName { get; set; }
        public MetaHash FilterParameterName { get; set; }
        public int[] InputOffsets { get; set; } = [];
        public int[] InputFileOffsets { get; set; } = [];
        public MrfNodeNChildData[] ChildrenData { get; set; } = [];
        public uint[] InputFlags { get; set; } = []; // four 8-bit source flag sets per word

        public MrfNode[] Children { get; set; } = [];

        public MrfValueType FilterNType
        {
            get => (MrfValueType)GetFlagsSubset(0, 3);
            set => SetFlagsSubset(0, 3, (uint)value);
        }
        public MrfValueType FilterType
        {
            get => (MrfValueType)GetFlagsSubset(2, 3);
            set => SetFlagsSubset(2, 3, (uint)value);
        }
        public bool ZeroDestination
        {
            get => GetFlagsSubset(4, 1) != 0;
            set => SetFlagsSubset(4, 1, value ? 1 : 0u);
        }
        public bool Transitional
        {
            get => GetFlagsSubset(6, 1) != 0;
            set => SetFlagsSubset(6, 1, value ? 1u : 0u);
        }
        public MrfSynchronizerType SynchronizerType
        {
            get => (MrfSynchronizerType)GetFlagsSubset(19, 3);
            set => SetFlagsSubset(19, 3, (uint)value);
        }
        public uint SourceCount
        {
            get => GetFlagsSubset(26, 0x3F);
            set => SetFlagsSubset(26, 0x3F, value);
        }
        
        public byte GetChildFlags(int index)
        {
            int blockIndex = 8 * index / 32;
            int bitOffset = 8 * index % 32;
            uint block = InputFlags[blockIndex];
            return (byte)((block >> bitOffset) & 0xFF);
        }
        public void SetChildFlags(int index, byte flags)
        {
            int blockIndex = 8 * index / 32;
            int bitOffset = 8 * index % 32;
            uint block = InputFlags[blockIndex];
            block = (block & ~(0xFFu << bitOffset)) | ((uint)flags << bitOffset);
            InputFlags[blockIndex] = block;
        }
        public bool GetChildImmutable(int index)
        {
            if ((uint)index >= 8) return false;
            return GetFlagsSubset(index + 7, 1) != 0;
        }
        public void SetChildImmutable(int index, bool value)
        {
            if ((uint)index >= 8) throw new ArgumentOutOfRangeException(nameof(index));
            SetFlagsSubset(index + 7, 1, value ? 1u : 0u);
        }
        public MrfValueType GetChildWeightType(int index)
        {
            return (MrfValueType)(GetChildFlags(index) & 3);
        }
        public void SetChildWeightType(int index, MrfValueType type)
        {
            var flags = GetChildFlags(index);
            flags = (byte)(flags & ~3u | ((uint)type & 3u));
            SetChildFlags(index, flags);
        }
        public MrfValueType GetChildFilterType(int index)
        {
            return (MrfValueType)((GetChildFlags(index) >> 4) & 3);
        }
        public void SetChildFilterType(int index, MrfValueType type)
        {
            var flags = GetChildFlags(index);
            flags = (byte)(flags & ~(3u << 4) | (((uint)type & 3u) << 4));
            SetChildFlags(index, flags);
        }

        protected MrfNodeNBase(MrfNodeType type) : base(type) { }

        public override void Read(DataReader r)
        {
            base.Read(r);
            ValidateFlags();

            if (SynchronizerType == MrfSynchronizerType.Tag)
                SynchronizerTag = (MrfSynchronizerTagFlags)r.ReadUInt32();

            switch (FilterNType)
            {
                case MrfValueType.Literal:
                    FilterNValues = new float[19];
                    for (int i = 0; i < FilterNValues.Length; i++)
                        FilterNValues[i] = r.ReadSingle();
                    break;
                case MrfValueType.Parameter:
                    FilterNParameterName = r.ReadUInt32();
                    break;
            }

            switch (FilterType)
            {
                case MrfValueType.Literal:
                    FilterDictionaryName = r.ReadUInt32();
                    FilterName = r.ReadUInt32();
                    break;
                case MrfValueType.Parameter:
                    FilterParameterName = r.ReadUInt32();
                    break;
            }

            var childrenCount = SourceCount;
            if (childrenCount > 0)
            {
                InputOffsets = new int[childrenCount];
                InputFileOffsets = new int[childrenCount];

                for (int i = 0; i < childrenCount; i++)
                {
                    InputOffsets[i] = r.ReadInt32();
                    InputFileOffsets[i] = checked((int)(r.Position + InputOffsets[i] - 4));
                }
            }

            var inputFlagsWordCount = GetInputFlagsWordCount(childrenCount);

            if (inputFlagsWordCount > 0)
            {
                InputFlags = new uint[inputFlagsWordCount];

                for (int i = 0; i < inputFlagsWordCount; i++)
                    InputFlags[i] = r.ReadUInt32();
            }

            if (SourceCount == 0)
                return;

            ChildrenData = new MrfNodeNChildData[childrenCount];

            for (int i = 0; i < childrenCount; i++)
            {
                var item = new MrfNodeNChildData();
                if (GetChildWeightType(i) > MrfValueType.Parameter || GetChildFilterType(i) > MrfValueType.Parameter)
                    throw new InvalidDataException($"Movement N-way child {i} contains an invalid value type.");

                switch (GetChildWeightType(i))
                {
                    case MrfValueType.Literal:
                        item.Weight = r.ReadSingle();
                        break;
                    case MrfValueType.Parameter:
                        item.WeightParameterName = r.ReadUInt32();
                        break;
                }

                switch (GetChildFilterType(i))
                {
                    case MrfValueType.Literal:
                        item.FilterDictionaryName = r.ReadUInt32();
                        item.FilterName = r.ReadUInt32();
                        break;
                    case MrfValueType.Parameter:
                        item.FilterParameterName = r.ReadUInt32();
                        break;
                }

                ChildrenData[i] = item;
            }
        }

        public override void Write(DataWriter w)
        {
            PrepareForWrite();
            ValidateFlags();
            base.Write(w);

            if (SynchronizerType == MrfSynchronizerType.Tag)
                w.Write((uint)SynchronizerTag);

            switch (FilterNType)
            {
                case MrfValueType.Literal:
                    if (FilterNValues.Length != 19)
                        throw new InvalidDataException("A literal movement FilterN must contain 19 values.");
                    foreach (var value in FilterNValues)
                        w.Write(value);
                    break;
                case MrfValueType.Parameter:
                    w.Write(FilterNParameterName);
                    break;
            }

            switch (FilterType)
            {
                case MrfValueType.Literal:
                    w.Write(FilterDictionaryName);
                    w.Write(FilterName);
                    break;
                case MrfValueType.Parameter:
                    w.Write(FilterParameterName);
                    break;
            }

            var childrenCount = SourceCount;
            for (int i = 0; i < childrenCount; i++)
                w.Write(InputOffsets[i]);

            var inputFlagsWordCount = GetInputFlagsWordCount(childrenCount);

            for (int i = 0; i < inputFlagsWordCount; i++)
                w.Write(InputFlags[i]);

            if (childrenCount == 0)
                return;

            for (int i = 0; i < childrenCount; i++)
            {
                var item = ChildrenData[i];

                switch (GetChildWeightType(i))
                {
                    case MrfValueType.Literal:
                        w.Write(item.Weight);
                        break;
                    case MrfValueType.Parameter:
                        w.Write(item.WeightParameterName);
                        break;
                }

                switch (GetChildFilterType(i))
                {
                    case MrfValueType.Literal:
                        w.Write(item.FilterDictionaryName);
                        w.Write(item.FilterName);
                        break;
                    case MrfValueType.Parameter:
                        w.Write(item.FilterParameterName);
                        break;
                }
            }
        }

        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);

            FilterNType = Xml.GetChildEnumInnerText<MrfValueType>(node, "FilterNType");
            if (FilterNType == MrfValueType.Literal)
                FilterNValues = Xml.GetChildRawFloatArray(node, "FilterNValues");
            else if (FilterNType == MrfValueType.Parameter)
                FilterNParameterName = XmlMeta.GetHash(Xml.GetChildInnerText(node, "FilterNParameterName"));
            (FilterType, FilterDictionaryName, FilterName, FilterParameterName) = XmlMrf.GetChildParameterizedAsset(node, "FrameFilter");
            SynchronizerType = Xml.GetChildEnumInnerText<MrfSynchronizerType>(node, "SynchronizerType");
            if (SynchronizerType == MrfSynchronizerType.Tag)
            {
                SynchronizerTag = Xml.GetChildEnumInnerText<MrfSynchronizerTagFlags>(node, "SynchronizerTagFlags");
            }
            ZeroDestination = Xml.GetChildBoolAttribute(node, "ZeroDestination");
            Transitional = Xml.GetChildBoolAttribute(node, "Transitional");
            Children = [];
            ChildrenData = [];
            InputFlags = [];
            InputOffsets = [];
            SourceCount = 0;
            var statesNode = node.SelectSingleNode("Children");
            if (statesNode != null)
            {
                var inodes = statesNode.SelectNodes("Item");
                if (inodes?.Count > 0)
                {
                    SourceCount = checked((uint)inodes.Count);
                    Children = new MrfNode[SourceCount];
                    ChildrenData = new MrfNodeNChildData[SourceCount];
                    InputFlags = new uint[GetInputFlagsWordCount(SourceCount)];
                    InputOffsets = new int[SourceCount];
                    int i = 0;
                    foreach (XmlNode inode in inodes)
                    {
                        var weight = XmlMrf.GetChildParameterizedFloat(inode, "Weight");
                        var filter = XmlMrf.GetChildParameterizedAsset(inode, "FrameFilter");

                        ChildrenData[i].Weight = weight.Value;
                        ChildrenData[i].WeightParameterName = weight.ParameterName;
                        SetChildWeightType(i, weight.Type);
                        ChildrenData[i].FilterDictionaryName = filter.DictionaryName;
                        ChildrenData[i].FilterName = filter.AssetName;
                        ChildrenData[i].FilterParameterName = filter.ParameterName;
                        SetChildFilterType(i, filter.Type);
                        SetChildImmutable(i, Xml.GetChildBoolAttribute(inode, "Immutable"));
                        Children[i] = XmlMrf.ReadChildNode(inode, "Node") ?? throw new InvalidDataException("Movement XML contains an invalid child element.");
                        i++;
                    }
                }
            }
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);

            MrfXml.StringTag(sb, indent, "FilterNType", FilterNType.ToString());
            if (FilterNType == MrfValueType.Literal)
                MrfXml.WriteRawArray(sb, FilterNValues, indent, "FilterNValues", "", FloatUtil.ToString, 19);
            else if (FilterNType == MrfValueType.Parameter)
                MrfXml.StringTag(sb, indent, "FilterNParameterName", MrfXml.HashString(FilterNParameterName));
            MrfXml.ParameterizedAssetTag(sb, indent, "FrameFilter", FilterType, FilterDictionaryName, FilterName, FilterParameterName);
            MrfXml.StringTag(sb, indent, "SynchronizerType", SynchronizerType.ToString());
            if (SynchronizerType == MrfSynchronizerType.Tag)
            {
                MrfXml.StringTag(sb, indent, "SynchronizerTagFlags", SynchronizerTag.ToString());
            }
            MrfXml.ValueTag(sb, indent, "ZeroDestination", ZeroDestination.ToString());
            MrfXml.ValueTag(sb, indent, "Transitional", Transitional.ToString());
            int cindent = indent + 1;
            int cindent2 = cindent + 1;
            int childIndex = 0;
            MrfXml.OpenTag(sb, indent, "Children");
            foreach (var child in Children)
            {
                var childData = ChildrenData[childIndex];
                MrfXml.OpenTag(sb, cindent, "Item");
                MrfXml.ParameterizedFloatTag(sb, cindent2, "Weight", GetChildWeightType(childIndex), childData.Weight, childData.WeightParameterName);
                MrfXml.ParameterizedAssetTag(sb, cindent2, "FrameFilter", GetChildFilterType(childIndex), childData.FilterDictionaryName, childData.FilterName, childData.FilterParameterName);
                MrfXml.ValueTag(sb, cindent2, "Immutable", GetChildImmutable(childIndex).ToString());
                MrfXml.WriteNode(sb, cindent2, "Node", child);
                MrfXml.CloseTag(sb, cindent, "Item");
                childIndex++;
            }
            MrfXml.CloseTag(sb, indent, "Children");
        }

        public override void ResolveRelativeOffsets(MrfFile mrf)
        {
            base.ResolveRelativeOffsets(mrf);

            if (InputFileOffsets != null)
            {
                Children = new MrfNode[InputFileOffsets.Length];
                for (int i = 0; i < InputFileOffsets.Length; i++)
                {
                    var node = mrf.FindNodeAtFileOffset(InputFileOffsets[i]);
                    if (node == null)
                    { } // no hits

                    Children[i] = node ?? throw new InvalidDataException("A movement child node could not be resolved.");
                }
            }
        }

        public override void UpdateRelativeOffsets()
        {
            base.UpdateRelativeOffsets();

            SourceCount = checked((uint)(Children?.Length ?? 0));
            var offset = FileOffset + 0xC/*sizeof(MrfNodeWithFlagsBase)*/;
            offset += SynchronizerType == MrfSynchronizerType.Tag ? 4 : 0;
            offset += FilterNType == MrfValueType.Literal ? 76 : 0;
            offset += FilterNType == MrfValueType.Parameter ? 4 : 0;
            offset += FilterType == MrfValueType.Literal ? 8 : 0;
            offset += FilterType == MrfValueType.Parameter ? 4 : 0;
            
            if (Children != null)
            {
                InputOffsets = new int[Children.Length];
                InputFileOffsets = new int[Children.Length];
                for (int i = 0; i < Children.Length; i++)
                {
                    var node = Children[i];
                    InputFileOffsets[i] = node.FileOffset;
                    InputOffsets[i] = node.FileOffset - offset;
                    offset += 4;
                }
            }
        }

        private static int GetInputFlagsWordCount(uint sourceCount) => checked((int)(sourceCount / 4 + 1));

        private void PrepareForWrite()
        {
            Children ??= [];
            SourceCount = checked((uint)Children.Length);
            if (SourceCount > 8)
                throw new InvalidDataException("Movement N-way nodes support at most 8 children.");
            if (ChildrenData?.Length != Children.Length)
                throw new InvalidDataException("Movement N-way child data count does not match its child count.");
            if (InputOffsets?.Length != Children.Length)
                InputOffsets = new int[Children.Length];
            int flagsCount = GetInputFlagsWordCount(SourceCount);
            if (InputFlags?.Length != flagsCount)
            {
                var flags = InputFlags ?? [];
                Array.Resize(ref flags, flagsCount);
                InputFlags = flags;
            }
        }

        private void ValidateFlags()
        {
            if (FilterNType > MrfValueType.Parameter || FilterType > MrfValueType.Parameter || SynchronizerType > MrfSynchronizerType.None || SourceCount > 8)
                throw new InvalidDataException("Movement N-way node flags contain an invalid type, synchronizer, or source count.");
        }
    }
    
    [TC(typeof(EXP))] public struct MrfNodeNChildData
    {
        public float Weight { get; set; }
        public MetaHash WeightParameterName { get; set; }
        public MetaHash FilterDictionaryName { get; set; }
        public MetaHash FilterName { get; set; }
        public MetaHash FilterParameterName { get; set; }

        public override string ToString()
        {
            return $"{FloatUtil.ToString(Weight)} - {WeightParameterName} - {FilterDictionaryName} - {FilterName} - {FilterParameterName}";
        }
    }



#endregion

#region mrf node structs

    [TC(typeof(EXP))] public class MrfExternalReference : IMetaXmlItem
    {
        public uint Length { get; set; }
        public byte[] Data { get; set; } = [];

        public MrfExternalReference()
        {
        }

        public MrfExternalReference(DataReader r)
        {
            Length = r.ReadUInt32();
            if (Length > r.Length - r.Position)
                throw new InvalidDataException("Failed to read MRF: invalid external-reference length.");
            Data = r.ReadBytes(checked((int)Length));
        }

        public void Write(DataWriter w)
        {
            Length = checked((uint)Data.Length);
            w.Write(Length);
            w.Write(Data);
        }

        public void ReadXml(XmlNode node)
        {
            Data = Xml.GetChildRawByteArrayNullable(node, "Bytes") ?? [];
            Length = checked((uint)Data.Length);
        }

        public void WriteXml(StringBuilder sb, int indent)
        {
            MrfXml.WriteRawArray(sb, Data, indent, "Bytes", "", MrfXml.FormatHexByte, 16);
        }

        public override string ToString()
        {
            return Length + " bytes";
        }
    }

    /// <summary>
    /// If used as <see cref="MrfFile.Requests"/>:
    /// Parameter that can be triggered by the game to control transitions.
    /// Only active for 1 tick.
    /// The native `REQUEST_TASK_MOVE_NETWORK_STATE_TRANSITION` uses these triggers but appends "request" to the passed string,
    /// e.g. `REQUEST_TASK_MOVE_NETWORK_STATE_TRANSITION(ped, "running")` will trigger "runningrequest".
    /// <para>
    /// If used as <see cref="MrfFile.Flags"/>:
    /// Parameter that can be toggled by the game to control transitions.
    /// Can be enabled with fwClipSet.moveNetworkFlags too (seems like only if the game uses it as a MrfClipContainerType.VariableClipSet).
    /// </para>
    /// </summary>
    [TC(typeof(EXP))] public struct MrfMoveNetworkBit : IMetaXmlItem
    {
        public static MrfMoveNetworkBit EndMarker => new MrfMoveNetworkBit { Name = 0xFFFFFFFF, BitPosition = 0 };

        public MetaHash Name { get; set; }
        public int BitPosition { get; set; }

        public bool IsEndMarker => Name == 0xFFFFFFFF;

        public MrfMoveNetworkBit(DataReader r)
        {
            Name = r.ReadUInt32();
            BitPosition = r.ReadInt32();
        }

        public void Write(DataWriter w)
        {
            w.Write(Name);
            w.Write(BitPosition);
        }

        public override string ToString()
        {
            return IsEndMarker ? "--- end marker ---" : $"{Name} - {BitPosition}";
        }

        public void WriteXml(StringBuilder sb, int indent)
        {
            MrfXml.StringTag(sb, indent, "Name", MrfXml.HashString(Name));
            MrfXml.ValueTag(sb, indent, "BitPosition", BitPosition.ToString());
        }

        public void ReadXml(XmlNode node)
        {
            Name = XmlMeta.GetHash(Xml.GetChildInnerText(node, "Name"));
            BitPosition = Xml.GetChildIntAttribute(node, "BitPosition");
        }
    }

    public enum MrfWeightModifierType
    {
        EaseInOut = 0,
        EaseOut = 1,
        EaseIn = 2,
        Linear = 3,
        Step = 4,

        SlowInSlowOut = EaseInOut,
        SlowOut = EaseOut,
        SlowIn = EaseIn,
        None = Linear,
    }

    [TC(typeof(EXP))] public class MrfStateTransition : IMetaXmlItem
    {
        // rage::mvTransitionDef

        public uint Flags { get; set; }
        public MrfSynchronizerTagFlags SynchronizerTag { get; set; }
        public float Duration { get; set; } // time in seconds it takes for the transition to blend between the source and target states
        public MetaHash DurationParameterName { get; set; }
        public MetaHash TransitionWeightParameterName { get; set; }
        public int TargetStateOffset { get; set; } // offset from the start of this field
        public int TargetStateFileOffset { get; set; }
        public MetaHash FilterDictionaryName { get; set; }
        public MetaHash FilterName { get; set; }
        public MrfCondition[] Conditions { get; set; } = [];

        public MrfNodeStateBase? TargetState { get; set; }

        // flags getters and setters
        public bool HasTransitionWeightParameter
        {
            get => GetFlagsSubset(1, 1) != 0;
            set => SetFlagsSubset(1, 1, value ? 1 : 0u);
        }
        public bool BlockUpdateAfterTransition
        {
            get => GetFlagsSubset(2, 1) != 0;
            set => SetFlagsSubset(2, 1, value ? 1 : 0u);
        }
        public bool DurationFromParameter
        {
            get => GetFlagsSubset(3, 1) != 0;
            set => SetFlagsSubset(3, 1, value ? 1 : 0u);
        }
        public uint Size
        {
            get => GetFlagsSubset(4, 0x3FFF); 
            set => SetFlagsSubset(4, 0x3FFF, value);
        }
        public bool Transitional
        {
            get => GetFlagsSubset(18, 1) != 0;
            set => SetFlagsSubset(18, 1, value ? 1 : 0u);
        }
        public bool Immutable
        {
            get => GetFlagsSubset(19, 1) != 0;
            set => SetFlagsSubset(19, 1, value ? 1 : 0u);
        }
        public uint ConditionCount
        {
            get => GetFlagsSubset(20, 0xF);
            set => SetFlagsSubset(20, 0xF, value);
        }
        public MrfWeightModifierType Modifier
        {
            get => (MrfWeightModifierType)GetFlagsSubset(24, 7);
            set => SetFlagsSubset(24, 7, (uint)value);
        }
        public MrfSynchronizerType SynchronizerType
        {
            get => (MrfSynchronizerType)GetFlagsSubset(28, 3);
            set => SetFlagsSubset(28, 3, (uint)value);
        }
        public bool ReEvaluate
        {
            get => GetFlagsSubset(27, 1) != 0;
            set => SetFlagsSubset(27, 1, value ? 1 : 0u);
        }
        public bool HasFilter
        {
            get => GetFlagsSubset(30, 1) != 0;
            set => SetFlagsSubset(30, 1, value ? 1 : 0u);
        }
        public bool MergeBlend
        {
            get => GetFlagsSubset(31, 1) != 0;
            set => SetFlagsSubset(31, 1, value ? 1 : 0u);
        }

        [System.ComponentModel.Browsable(false)]
        public MetaHash XmlTargetStateName { get; set; } // for XML loading

        public MrfStateTransition()
        {
        }

        public MrfStateTransition(DataReader r)
        {
            var startReadPosition = r.Position;

            Flags = r.ReadUInt32();
            SynchronizerTag = (MrfSynchronizerTagFlags)r.ReadUInt32();
            Duration = r.ReadSingle();
            DurationParameterName = r.ReadUInt32();
            TransitionWeightParameterName = r.ReadUInt32();
            TargetStateOffset = r.ReadInt32();
            TargetStateFileOffset = checked((int)(r.Position + TargetStateOffset - 4));

            ValidateFlags();

            if (ConditionCount > 0)
            {
                Conditions = new MrfCondition[ConditionCount];
                for (int i = 0; i < ConditionCount; i++)
                {
                    var startPos = r.Position;
                    var conditionType = (MrfConditionType)r.ReadUInt16();
                    r.Position = startPos;
                    
                    MrfCondition cond;
                    switch (conditionType)
                    {
                        case MrfConditionType.ParameterInsideRange:    cond = new MrfConditionParameterInsideRange(r); break;
                        case MrfConditionType.ParameterOutsideRange:   cond = new MrfConditionParameterOutsideRange(r); break;
                        case MrfConditionType.MoveNetworkTrigger:      cond = new MrfConditionMoveNetworkTrigger(r); break;
                        case MrfConditionType.MoveNetworkFlag:         cond = new MrfConditionMoveNetworkFlag(r); break;
                        case MrfConditionType.EventOccurred:           cond = new MrfConditionEventOccurred(r); break;
                        case MrfConditionType.ParameterGreaterThan:    cond = new MrfConditionParameterGreaterThan(r); break;
                        case MrfConditionType.ParameterGreaterOrEqual: cond = new MrfConditionParameterGreaterOrEqual(r); break;
                        case MrfConditionType.ParameterLessThan:       cond = new MrfConditionParameterLessThan(r); break;
                        case MrfConditionType.ParameterLessOrEqual:    cond = new MrfConditionParameterLessOrEqual(r); break;
                        case MrfConditionType.TimeGreaterThan:         cond = new MrfConditionTimeGreaterThan(r); break;
                        case MrfConditionType.TimeLessThan:            cond = new MrfConditionTimeLessThan(r); break;
                        case MrfConditionType.BoolParameterExists:     cond = new MrfConditionBoolParameterExists(r); break;
                        case MrfConditionType.BoolParameterEquals:     cond = new MrfConditionBoolParameterEquals(r); break;
                        default: throw new Exception($"Unknown condition type ({conditionType})");
                    }

                    Conditions[i] = cond;
                }
            }

            if (HasFilter)
            {
                FilterDictionaryName = r.ReadUInt32();
                FilterName = r.ReadUInt32();
            }
            else
            {
                FilterDictionaryName = 0;
                FilterName = 0;
            }

            if ((r.Position - startReadPosition) != Size)
                throw new InvalidDataException($"Movement transition size is {Size}, but {r.Position - startReadPosition} bytes were read.");
        }

        public void Write(DataWriter w)
        {
            int conditionCount = Conditions?.Length ?? 0;
            if (conditionCount > 15)
                throw new InvalidDataException("Movement transitions support at most 15 conditions.");
            ConditionCount = (uint)conditionCount;
            CalculateSize();
            ValidateFlags();

            w.Write(Flags);
            w.Write((uint)SynchronizerTag);
            w.Write(Duration);
            w.Write(DurationParameterName);
            w.Write(TransitionWeightParameterName);
            w.Write(TargetStateOffset);

            if (Conditions != null)
                for (int i = 0; i < Conditions.Length; i++)
                    Conditions[i].Write(w);

            if (HasFilter)
            {
                w.Write(FilterDictionaryName);
                w.Write(FilterName);
            }
        }

        public void ReadXml(XmlNode node)
        {
            XmlTargetStateName = XmlMrf.ReadChildNodeRef(node, "TargetState");
            Duration = Xml.GetChildFloatAttribute(node, "Duration");
            DurationParameterName = XmlMeta.GetHash(Xml.GetChildInnerText(node, "DurationParameterName"));
            TransitionWeightParameterName = XmlMeta.GetHash(Xml.GetChildInnerText(node, "TransitionWeightParameterName"));
            if (TransitionWeightParameterName == 0)
                TransitionWeightParameterName = XmlMeta.GetHash(Xml.GetChildInnerText(node, "ProgressParameterName"));
            DurationFromParameter = DurationParameterName != 0;
            HasTransitionWeightParameter = TransitionWeightParameterName != 0;
            Modifier = node.SelectSingleNode("Modifier") != null
                ? Xml.GetChildEnumInnerText<MrfWeightModifierType>(node, "Modifier")
                : Xml.GetChildEnumInnerText<MrfWeightModifierType>(node, "BlendModifier");

            SynchronizerType = Xml.GetChildEnumInnerText<MrfSynchronizerType>(node, "SynchronizerType");
            if (SynchronizerType == MrfSynchronizerType.Tag)
            {
                SynchronizerTag = Xml.GetChildEnumInnerText<MrfSynchronizerTagFlags>(node, "SynchronizerTag");
                if (SynchronizerTag == 0)
                    SynchronizerTag = Xml.GetChildEnumInnerText<MrfSynchronizerTagFlags>(node, "SynchronizerTagFlags");
            }
            else
            {
                SynchronizerTag = (MrfSynchronizerTagFlags)0xFFFFFFFF;
            }
            
            var filter = XmlMrf.GetChildParameterizedAsset(node, "FrameFilter");
            if (filter.Type == MrfValueType.Literal)
            {
                HasFilter = true;
                FilterDictionaryName = filter.DictionaryName;
                FilterName = filter.AssetName;
            }
            else
            {
                HasFilter = false;
                FilterDictionaryName = 0;
                FilterName = 0;
            }
            
            BlockUpdateAfterTransition = Xml.GetChildBoolAttribute(node, "BlockUpdateAfterTransition") || Xml.GetChildBoolAttribute(node, "UnkFlag2_DetachUpdateObservers");
            Transitional = Xml.GetChildBoolAttribute(node, "Transitional") || Xml.GetChildBoolAttribute(node, "UnkFlag18");
            Immutable = Xml.GetChildBoolAttribute(node, "Immutable") || Xml.GetChildBoolAttribute(node, "UnkFlag19");
            ReEvaluate = Xml.GetChildBoolAttribute(node, "ReEvaluate");
            MergeBlend = Xml.GetChildBoolAttribute(node, "MergeBlend");

            Conditions = [];
            var conditionsNode = node.SelectSingleNode("Conditions");
            if (conditionsNode != null)
            {
                var inodes = conditionsNode.SelectNodes("Item");
                if (inodes?.Count > 0)
                {
                    Conditions = new MrfCondition[inodes.Count];
                    int i = 0;
                    foreach (XmlNode inode in inodes)
                    {
                        Conditions[i] = XmlMrf.ReadCondition(inode) ?? throw new InvalidDataException("Movement XML contains an invalid child element.");
                        i++;
                    }
                }
            }

            CalculateSize();
        }

        public void WriteXml(StringBuilder sb, int indent)
        {
            //MrfXml.ValueTag(sb, indent, "Flags", Flags.ToString());
            MrfXml.WriteNodeRef(sb, indent, "TargetState", TargetState);
            MrfXml.ValueTag(sb, indent, "Duration", FloatUtil.ToString(Duration));
            if (DurationFromParameter) MrfXml.StringTag(sb, indent, "DurationParameterName", MrfXml.HashString(DurationParameterName));
            if (HasTransitionWeightParameter) MrfXml.StringTag(sb, indent, "TransitionWeightParameterName", MrfXml.HashString(TransitionWeightParameterName));
            MrfXml.StringTag(sb, indent, "Modifier", Modifier.ToString());

            MrfXml.StringTag(sb, indent, "SynchronizerType", SynchronizerType.ToString());
            if (SynchronizerType == MrfSynchronizerType.Tag)
            {
                MrfXml.StringTag(sb, indent, "SynchronizerTag", SynchronizerTag.ToString());
            }

            if (HasFilter)
            {
                MrfXml.ParameterizedAssetTag(sb, indent, "FrameFilter", MrfValueType.Literal, FilterDictionaryName, FilterName, 0);
            }
            else
            {
                MrfXml.SelfClosingTag(sb, indent, "FrameFilter");
            }

            MrfXml.ValueTag(sb, indent, "BlockUpdateAfterTransition", BlockUpdateAfterTransition.ToString());
            MrfXml.ValueTag(sb, indent, "Transitional", Transitional.ToString());
            MrfXml.ValueTag(sb, indent, "Immutable", Immutable.ToString());
            MrfXml.ValueTag(sb, indent, "ReEvaluate", ReEvaluate.ToString());
            MrfXml.ValueTag(sb, indent, "MergeBlend", MergeBlend.ToString());
            
            if (Conditions != null)
            {
                int cindent = indent + 1;
                MrfXml.OpenTag(sb, indent, "Conditions");
                foreach (var c in Conditions)
                {
                    MrfXml.WriteCondition(sb, cindent, "Item", c);
                }
                MrfXml.CloseTag(sb, indent, "Conditions");
            }
            else
            {
                MrfXml.SelfClosingTag(sb, indent, "Conditions");
            }
        }

        public uint GetFlagsSubset(int bitOffset, uint mask)
        {
            return (Flags >> bitOffset) & mask;
        }

        public void SetFlagsSubset(int bitOffset, uint mask, uint value)
        {
            Flags = (Flags & ~(mask << bitOffset)) | ((value & mask) << bitOffset);
        }

        public void CalculateSize()
        {
            uint dataSize = 0x18;
            if (Conditions != null)
            {
                dataSize += (uint)Conditions.Sum(c => c.DataSize);
            }

            if (HasFilter)
            {
                dataSize += 8;
            }

            Size = dataSize;
        }

        private void ValidateFlags()
        {
            if ((uint)Modifier > (uint)MrfWeightModifierType.Step)
                throw new InvalidDataException($"Unknown movement transition modifier ({Modifier}).");
            if ((uint)SynchronizerType > (uint)MrfSynchronizerType.None)
                throw new InvalidDataException($"Unknown movement transition synchronizer ({SynchronizerType}).");
            if (Duration < 0)
                throw new InvalidDataException("Movement transition duration cannot be negative.");
        }

        public override string ToString()
        {
            return $"{TargetState?.ID.ToString() ?? TargetStateFileOffset.ToString()} - {FloatUtil.ToString(Duration)} - {Conditions?.Length ?? 0} conditions";
        }
    }

    public enum MrfConditionType : ushort
    {
        InRange = 0,
        OutOfRange = 1,
        OnRequest = 2,
        OnFlag = 3,
        AtEvent = 4,
        GreaterThan = 5,
        GreaterThanEqual = 6,
        LessThan = 7,
        LessThanEqual = 8,
        LifetimeGreaterThan = 9,
        LifetimeLessThan = 10,
        OnMoveEventTag = 11,
        BoolEquals = 12,

        ParameterInsideRange = InRange,
        ParameterOutsideRange = OutOfRange,
        MoveNetworkTrigger = OnRequest,
        MoveNetworkFlag = OnFlag,
        EventOccurred = AtEvent,
        ParameterGreaterThan = GreaterThan,
        ParameterGreaterOrEqual = GreaterThanEqual,
        ParameterLessThan = LessThan,
        ParameterLessOrEqual = LessThanEqual,
        TimeGreaterThan = LifetimeGreaterThan,
        TimeLessThan = LifetimeLessThan,
        BoolParameterExists = OnMoveEventTag,
        BoolParameterEquals = BoolEquals,
    }

    [TC(typeof(EXP))] public abstract class MrfCondition : IMetaXmlItem
    {
        // rage::mvConditionDef

        public MrfConditionType Type { get; set; }
        public ushort Set { get; set; }

        public abstract uint DataSize { get; }

        protected MrfCondition(MrfConditionType type)
        {
            Type = type;
        }

        protected MrfCondition(DataReader r)
        {
            Type = (MrfConditionType)r.ReadUInt16();
            Set = r.ReadUInt16();
        }

        public virtual void Write(DataWriter w)
        {
            w.Write((ushort)Type);
            w.Write(Set);
        }

        public virtual void WriteXml(StringBuilder sb, int indent)
        {
            if (Set != 0) MrfXml.ValueTag(sb, indent, "Set", Set.ToString());
        }

        public virtual void ReadXml(XmlNode node)
        {
            Set = (ushort)Xml.GetChildUIntAttribute(node, "Set");
        }

        public override string ToString()
        {
            return Type.ToString();
        }

        /// <summary>
        /// Returns the condition as a C-like expression. Mainly to include it in the debug DOT graphs.
        /// </summary>
        public abstract string ToExpressionString(MrfFile mrf);

        public static MrfCondition CreateCondition(MrfConditionType conditionType)
        {
            switch (conditionType)
            {
                case MrfConditionType.ParameterInsideRange:    return new MrfConditionParameterInsideRange();
                case MrfConditionType.ParameterOutsideRange:   return new MrfConditionParameterOutsideRange();
                case MrfConditionType.MoveNetworkTrigger:      return new MrfConditionMoveNetworkTrigger();
                case MrfConditionType.MoveNetworkFlag:         return new MrfConditionMoveNetworkFlag();
                case MrfConditionType.EventOccurred:           return new MrfConditionEventOccurred();
                case MrfConditionType.ParameterGreaterThan:    return new MrfConditionParameterGreaterThan();
                case MrfConditionType.ParameterGreaterOrEqual: return new MrfConditionParameterGreaterOrEqual();
                case MrfConditionType.ParameterLessThan:       return new MrfConditionParameterLessThan();
                case MrfConditionType.ParameterLessOrEqual:    return new MrfConditionParameterLessOrEqual();
                case MrfConditionType.TimeGreaterThan:         return new MrfConditionTimeGreaterThan();
                case MrfConditionType.TimeLessThan:            return new MrfConditionTimeLessThan();
                case MrfConditionType.BoolParameterExists:     return new MrfConditionBoolParameterExists();
                case MrfConditionType.BoolParameterEquals:     return new MrfConditionBoolParameterEquals();
                default: throw new Exception($"Unknown condition type ({conditionType})");
            }
        }
    }
    [TC(typeof(EXP))] public abstract class MrfConditionWithParameterAndRangeBase : MrfCondition
    {
        public override uint DataSize => 16;
        public MetaHash ParameterName { get; set; }
        public float MaxValue { get; set; }
        public float MinValue { get; set; }

        protected MrfConditionWithParameterAndRangeBase(MrfConditionType type) : base(type) { }
        protected MrfConditionWithParameterAndRangeBase(DataReader r) : base(r)
        {
            ParameterName = r.ReadUInt32();
            MaxValue = r.ReadSingle();
            MinValue = r.ReadSingle();
        }

        public override void Write(DataWriter w)
        {
            base.Write(w);
            w.Write(ParameterName);
            w.Write(MaxValue);
            w.Write(MinValue);
        }

        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            ParameterName = XmlMeta.GetHash(Xml.GetChildInnerText(node, "ParameterName"));
            MinValue = Xml.GetChildFloatAttribute(node, "Min");
            MaxValue = Xml.GetChildFloatAttribute(node, "Max");
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            MrfXml.StringTag(sb, indent, "ParameterName", MrfXml.HashString(ParameterName));
            MrfXml.ValueTag(sb, indent, "Min", FloatUtil.ToString(MinValue));
            MrfXml.ValueTag(sb, indent, "Max", FloatUtil.ToString(MaxValue));
        }

        public override string ToString()
        {
            return base.ToString() + $" - {{ {nameof(ParameterName)} = {ParameterName}, {nameof(MaxValue)} = {FloatUtil.ToString(MaxValue)}, {nameof(MinValue)} = {FloatUtil.ToString(MinValue)} }}";
        }
    }
    [TC(typeof(EXP))] public abstract class MrfConditionWithParameterAndValueBase : MrfCondition
    {
        public override uint DataSize => 12;
        public MetaHash ParameterName { get; set; }
        public float Value { get; set; }

        protected MrfConditionWithParameterAndValueBase(MrfConditionType type) : base(type) { }
        protected MrfConditionWithParameterAndValueBase(DataReader r) : base(r)
        {
            ParameterName = r.ReadUInt32();
            Value = r.ReadSingle();
        }

        public override void Write(DataWriter w)
        {
            base.Write(w);
            w.Write(ParameterName);
            w.Write(Value);
        }

        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            ParameterName = XmlMeta.GetHash(Xml.GetChildInnerText(node, "ParameterName"));
            Value = Xml.GetChildFloatAttribute(node, "Value");
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            MrfXml.StringTag(sb, indent, "ParameterName", MrfXml.HashString(ParameterName));
            MrfXml.ValueTag(sb, indent, "Value", FloatUtil.ToString(Value));
        }

        public override string ToString()
        {
            return base.ToString() + $" - {{ {nameof(ParameterName)} = {ParameterName}, {nameof(Value)} = {FloatUtil.ToString(Value)} }}";
        }
    }
    [TC(typeof(EXP))] public abstract class MrfConditionWithValueBase : MrfCondition
    {
        public override uint DataSize => 8;
        public float Value { get; set; }

        protected MrfConditionWithValueBase(MrfConditionType type) : base(type) { }
        protected MrfConditionWithValueBase(DataReader r) : base(r)
        {
            Value = r.ReadSingle();
        }

        public override void Write(DataWriter w)
        {
            base.Write(w);
            w.Write(Value);
        }

        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            Value = Xml.GetChildFloatAttribute(node, "Value");
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            MrfXml.ValueTag(sb, indent, "Value", FloatUtil.ToString(Value));
        }

        public override string ToString()
        {
            return base.ToString() + $" - {{ {nameof(Value)} = {FloatUtil.ToString(Value)} }}";
        }
    }
    [TC(typeof(EXP))] public abstract class MrfConditionWithParameterAndBoolValueBase : MrfCondition
    {
        public override uint DataSize => 12;
        public MetaHash ParameterName { get; set; }
        public bool Value { get; set; }

        protected MrfConditionWithParameterAndBoolValueBase(MrfConditionType type) : base(type) { }
        protected MrfConditionWithParameterAndBoolValueBase(DataReader r) : base(r)
        {
            ParameterName = r.ReadUInt32();
            Value = r.ReadUInt32() != 0;
        }

        public override void Write(DataWriter w)
        {
            base.Write(w);
            w.Write(ParameterName);
            w.Write(Value ? 1 : 0u);
        }

        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            ParameterName = XmlMeta.GetHash(Xml.GetChildInnerText(node, "ParameterName"));
            Value = Xml.GetChildBoolAttribute(node, "Value");
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            MrfXml.StringTag(sb, indent, "ParameterName", MrfXml.HashString(ParameterName));
            MrfXml.ValueTag(sb, indent, "Value", Value.ToString());
        }

        public override string ToString()
        {
            return base.ToString() + $" - {{ {nameof(ParameterName)} = {ParameterName}, {nameof(Value)} = {Value} }}";
        }
    }
    [TC(typeof(EXP))] public abstract class MrfConditionBitTestBase : MrfCondition
    {
        public override uint DataSize => 12;
        public int BitPosition { get; set; }
        public bool Invert { get; set; }

        protected MrfConditionBitTestBase(MrfConditionType type) : base(type) { }
        protected MrfConditionBitTestBase(DataReader r) : base(r)
        {
            BitPosition = r.ReadInt32();
            Invert = r.ReadUInt32() != 0;
        }

        public override void Write(DataWriter w)
        {
            base.Write(w);
            w.Write(BitPosition);
            w.Write(Invert ? 1u : 0u);
        }

        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            BitPosition = Xml.GetChildIntAttribute(node, "BitPosition");
            Invert = Xml.GetChildBoolAttribute(node, "Invert");
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            MrfXml.ValueTag(sb, indent, "BitPosition", BitPosition.ToString());
            MrfXml.ValueTag(sb, indent, "Invert", Invert.ToString());
        }

        public string FindBitName(MrfFile mrf)
        {
            MetaHash? bitNameHash = null;
            if (mrf != null)
            {
                bitNameHash = Type == MrfConditionType.MoveNetworkTrigger ?
                                    mrf.FindMoveNetworkTriggerForBit(BitPosition)?.Name :
                                    mrf.FindMoveNetworkFlagForBit(BitPosition)?.Name;
            }
            return bitNameHash.HasValue ? $"'{bitNameHash.Value}'" : BitPosition.ToString();
        }

        public override string ToString()
        {
            return base.ToString() + $" - {{ {nameof(BitPosition)} = {BitPosition}, {nameof(Invert)} = {Invert} }}";
        }
    }

    [TC(typeof(EXP))] public class MrfConditionParameterInsideRange : MrfConditionWithParameterAndRangeBase
    {
        public MrfConditionParameterInsideRange() : base(MrfConditionType.ParameterInsideRange) { }
        public MrfConditionParameterInsideRange(DataReader r) : base(r) { }

        public override string ToExpressionString(MrfFile mrf)
        {
            return $"{FloatUtil.ToString(MinValue)} < '{ParameterName}' < {FloatUtil.ToString(MaxValue)}";
        }
    }
    [TC(typeof(EXP))] public class MrfConditionParameterOutsideRange : MrfConditionWithParameterAndRangeBase
    {
        public MrfConditionParameterOutsideRange() : base(MrfConditionType.ParameterOutsideRange) { }
        public MrfConditionParameterOutsideRange(DataReader r) : base(r) { }

        public override string ToExpressionString(MrfFile mrf)
        {
            return $"'{ParameterName}' < {FloatUtil.ToString(MinValue)} < {FloatUtil.ToString(MaxValue)} < '{ParameterName}'";
        }
    }
    [TC(typeof(EXP))] public class MrfConditionMoveNetworkTrigger : MrfConditionBitTestBase
    {
        public MrfConditionMoveNetworkTrigger() : base(MrfConditionType.MoveNetworkTrigger) { }
        public MrfConditionMoveNetworkTrigger(DataReader r) : base(r) { }

        public override string ToExpressionString(MrfFile mrf)
        {
            return (Invert ? "!" : "") + $"trigger({FindBitName(mrf)})";
        }
    }
    [TC(typeof(EXP))] public class MrfConditionMoveNetworkFlag : MrfConditionBitTestBase
    {
        public MrfConditionMoveNetworkFlag() : base(MrfConditionType.MoveNetworkFlag) { }
        public MrfConditionMoveNetworkFlag(DataReader r) : base(r) { }

        public override string ToExpressionString(MrfFile mrf)
        {
            return (Invert ? "!" : "") + $"flag({FindBitName(mrf)})";
        }
    }
    [TC(typeof(EXP))] public class MrfConditionParameterGreaterThan : MrfConditionWithParameterAndValueBase
    {
        public MrfConditionParameterGreaterThan() : base(MrfConditionType.ParameterGreaterThan) { }
        public MrfConditionParameterGreaterThan(DataReader r) : base(r) { }

        public override string ToExpressionString(MrfFile mrf)
        {
            return $"'{ParameterName}' > {FloatUtil.ToString(Value)}";
        }
    }
    [TC(typeof(EXP))] public class MrfConditionParameterGreaterOrEqual : MrfConditionWithParameterAndValueBase
    {
        public MrfConditionParameterGreaterOrEqual() : base(MrfConditionType.ParameterGreaterOrEqual) { }
        public MrfConditionParameterGreaterOrEqual(DataReader r) : base(r) { }

        public override string ToExpressionString(MrfFile mrf)
        {
            return $"'{ParameterName}' >= {FloatUtil.ToString(Value)}";
        }
    }
    [TC(typeof(EXP))] public class MrfConditionParameterLessThan : MrfConditionWithParameterAndValueBase
    {
        public MrfConditionParameterLessThan() : base(MrfConditionType.ParameterLessThan) { }
        public MrfConditionParameterLessThan(DataReader r) : base(r) { }

        public override string ToExpressionString(MrfFile mrf)
        {
            return $"'{ParameterName}' < {FloatUtil.ToString(Value)}";
        }
    }
    [TC(typeof(EXP))] public class MrfConditionParameterLessOrEqual : MrfConditionWithParameterAndValueBase
    {
        public MrfConditionParameterLessOrEqual() : base(MrfConditionType.ParameterLessOrEqual) { }
        public MrfConditionParameterLessOrEqual(DataReader r) : base(r) { }

        public override string ToExpressionString(MrfFile mrf)
        {
            return $"'{ParameterName}' <= {FloatUtil.ToString(Value)}";
        }
    }
    [TC(typeof(EXP))] public class MrfConditionTimeGreaterThan : MrfConditionWithValueBase
    {
        public MrfConditionTimeGreaterThan() : base(MrfConditionType.TimeGreaterThan) { }
        public MrfConditionTimeGreaterThan(DataReader r) : base(r) { }

        public override string ToExpressionString(MrfFile mrf)
        {
            return $"Time > {FloatUtil.ToString(Value)}";
        }
    }
    [TC(typeof(EXP))] public class MrfConditionTimeLessThan : MrfConditionWithValueBase
    {
        public MrfConditionTimeLessThan() : base(MrfConditionType.TimeLessThan) { }
        public MrfConditionTimeLessThan(DataReader r) : base(r) { }

        public override string ToExpressionString(MrfFile mrf)
        {
            return $"Time < {FloatUtil.ToString(Value)}";
        }
    }
    [TC(typeof(EXP))] public class MrfConditionEventOccurred : MrfConditionWithParameterAndBoolValueBase
    {
        public bool Invert { get => Value; set => Value = value; }

        public MrfConditionEventOccurred() : base(MrfConditionType.EventOccurred) { }
        public MrfConditionEventOccurred(DataReader r) : base(r) { }

        public override string ToExpressionString(MrfFile mrf)
        {
            return (Invert ? "!" : "") + $"event('{ParameterName}')";
        }
    }
    [TC(typeof(EXP))] public class MrfConditionBoolParameterExists : MrfConditionWithParameterAndBoolValueBase
    {
        public bool Invert { get => Value; set => Value = value; }

        public MrfConditionBoolParameterExists() : base(MrfConditionType.BoolParameterExists) { }
        public MrfConditionBoolParameterExists(DataReader r) : base(r) { }

        public override string ToExpressionString(MrfFile mrf)
        {
            return (Invert ? "!" : "") + $"exists('{ParameterName}')";
        }
    }
    [TC(typeof(EXP))] public class MrfConditionBoolParameterEquals : MrfConditionWithParameterAndBoolValueBase
    {
        public MrfConditionBoolParameterEquals() : base(MrfConditionType.BoolParameterEquals) { }
        public MrfConditionBoolParameterEquals(DataReader r) : base(r) { }

        public override string ToExpressionString(MrfFile mrf)
        {
            return $"'{ParameterName}' == {Value}";
        }
    }

    /// <summary>
    /// Before the target node updates, sets the target node parameter to the source network parameter value.
    /// </summary>
    [TC(typeof(EXP))] public class MrfStateInputParameter : IMetaXmlItem
    {
        // rage::mvNodeStateDef::InputParameter

        public MetaHash SourceParameterName { get; set; }
        public ushort TargetNodeIndex { get; set; }
        public /*MrfNodeParameterId*/ushort TargetNodeParameterId { get; set; }
        public uint TargetParameterIndex { get; set; }

        public MrfStateInputParameter() { }
        public MrfStateInputParameter(DataReader r)
        {
            SourceParameterName = r.ReadUInt32();
            TargetNodeIndex = r.ReadUInt16();
            TargetNodeParameterId = r.ReadUInt16();
            TargetParameterIndex = r.ReadUInt32();
        }

        public void Write(DataWriter w)
        {
            w.Write(SourceParameterName);
            w.Write(TargetNodeIndex);
            w.Write(TargetNodeParameterId);
            w.Write(TargetParameterIndex);
        }

        public void ReadXml(XmlNode node)
        {
            SourceParameterName = XmlMeta.GetHash(Xml.GetChildInnerText(node, "SourceParameterName"));
            TargetNodeIndex = (ushort)Xml.GetChildUIntAttribute(node, "TargetNodeIndex");
            TargetNodeParameterId = (ushort)Xml.GetChildUIntAttribute(node, "TargetNodeParameterId");
            TargetParameterIndex = Xml.GetChildUIntAttribute(node, "TargetParameterIndex");
            if (node.SelectSingleNode("TargetParameterIndex") == null)
                TargetParameterIndex = Xml.GetChildUIntAttribute(node, "TargetNodeParameterExtraArg");
        }

        public void WriteXml(StringBuilder sb, int indent)
        {
            MrfXml.StringTag(sb, indent, "SourceParameterName", MrfXml.HashString(SourceParameterName));
            MrfXml.ValueTag(sb, indent, "TargetNodeIndex", TargetNodeIndex.ToString());
            MrfXml.ValueTag(sb, indent, "TargetNodeParameterId", TargetNodeParameterId.ToString());
            MrfXml.ValueTag(sb, indent, "TargetParameterIndex", TargetParameterIndex.ToString());
        }

        public override string ToString()
        {
            return SourceParameterName.ToString() + " - " + TargetNodeIndex.ToString() + " - " + TargetNodeParameterId.ToString() + " - " + TargetParameterIndex.ToString();
        }
    }

    /// <summary>
    /// Sets a network bool parameter named <see cref="ParameterName"/> to true when the event occurs on the specified node.
    /// </summary>
    [TC(typeof(EXP))] public class MrfStateEvent : IMetaXmlItem
    {
        // rage::mvNodeStateDef::Event

        public ushort NodeIndex { get; set; }
        public /*MrfNodeEventId*/ushort NodeEventId { get; set; }
        public MetaHash ParameterName { get; set; }

        public MrfStateEvent() { }
        public MrfStateEvent(DataReader r)
        {
            NodeIndex = r.ReadUInt16();
            NodeEventId = r.ReadUInt16();
            ParameterName = r.ReadUInt32();
        }

        public void Write(DataWriter w)
        {
            w.Write(NodeIndex);
            w.Write(NodeEventId);
            w.Write(ParameterName);
        }

        public void ReadXml(XmlNode node)
        {
            NodeIndex = (ushort)Xml.GetChildUIntAttribute(node, "NodeIndex");
            NodeEventId = (ushort)Xml.GetChildUIntAttribute(node, "NodeEventId");
            ParameterName = XmlMeta.GetHash(Xml.GetChildInnerText(node, "ParameterName"));
        }

        public void WriteXml(StringBuilder sb, int indent)
        {
            MrfXml.ValueTag(sb, indent, "NodeIndex", NodeIndex.ToString());
            MrfXml.ValueTag(sb, indent, "NodeEventId", NodeEventId.ToString());
            MrfXml.StringTag(sb, indent, "ParameterName", MrfXml.HashString(ParameterName));
        }

        public override string ToString()
        {
            return NodeIndex.ToString() + " - " + NodeEventId.ToString() + " - " + ParameterName.ToString();
        }
    }

    /// <summary>
    /// After the source node updates, sets the target network parameter to the source node parameter value.
    /// </summary>
    [TC(typeof(EXP))] public class MrfStateOutputParameter : IMetaXmlItem
    {
        // rage::mvNodeStateDef::OutputParameter

        public MetaHash TargetParameterName { get; set; }
        public ushort SourceNodeIndex { get; set; }
        public /*MrfNodeParameterId*/ushort SourceNodeParameterId { get; set; } // if 0xFFFF, it stores the node itself, so it can be used by NodeProxy
        public uint SourceParameterIndex { get; set; }

        public MrfStateOutputParameter() { }
        public MrfStateOutputParameter(DataReader r)
        {
            TargetParameterName = r.ReadUInt32();
            SourceNodeIndex = r.ReadUInt16();
            SourceNodeParameterId = r.ReadUInt16();
            SourceParameterIndex = r.ReadUInt32();
        }

        public void Write(DataWriter w)
        {
            w.Write(TargetParameterName);
            w.Write(SourceNodeIndex);
            w.Write(SourceNodeParameterId);
            w.Write(SourceParameterIndex);
        }

        public void ReadXml(XmlNode node)
        {
            TargetParameterName = XmlMeta.GetHash(Xml.GetChildInnerText(node, "TargetParameterName"));
            SourceNodeIndex = (ushort)Xml.GetChildUIntAttribute(node, "SourceNodeIndex");
            SourceNodeParameterId = (ushort)Xml.GetChildUIntAttribute(node, "SourceNodeParameterId");
            SourceParameterIndex = Xml.GetChildUIntAttribute(node, "SourceParameterIndex");
            if (node.SelectSingleNode("SourceParameterIndex") == null)
                SourceParameterIndex = Xml.GetChildUIntAttribute(node, "SourceNodeParameterExtraArg");
        }

        public void WriteXml(StringBuilder sb, int indent)
        {
            MrfXml.StringTag(sb, indent, "TargetParameterName", MrfXml.HashString(TargetParameterName));
            MrfXml.ValueTag(sb, indent, "SourceNodeIndex", SourceNodeIndex.ToString());
            MrfXml.ValueTag(sb, indent, "SourceNodeParameterId", SourceNodeParameterId.ToString());
            MrfXml.ValueTag(sb, indent, "SourceParameterIndex", SourceParameterIndex.ToString());
        }

        public override string ToString()
        {
            return TargetParameterName.ToString() + " - " + SourceNodeIndex.ToString() + " - " + SourceNodeParameterId.ToString() + " - " + SourceParameterIndex.ToString();
        }
    }

    [TC(typeof(EXP))] public class MrfStateRef
    {
        public MetaHash StateName { get; set; }
        public int StateOffset { get; set; } // offset from the start of this field
        public int StateFileOffset { get; set; }

        public MrfNodeStateBase? State { get; set; }

        public MrfStateRef()
        {
        }

        public MrfStateRef(DataReader r)
        {
            StateName = r.ReadUInt32();
            StateOffset = r.ReadInt32();
            StateFileOffset = checked((int)(r.Position + StateOffset - 4));
        }

        public void Write(DataWriter w)
        {
            w.Write(StateName);
            w.Write(StateOffset);
        }

        public override string ToString()
        {
            return StateName.ToString();
        }
    }

    public enum MrfOperatorType : uint
    {
        End = 0,
        PushValue = 1,
        FindAndPushValue = 2,
        Add = 3,            // adds the two values at the top of the stack, not used in vanilla MRFs
        Multiply = 4,       // multiplies the two values at the top of the stack
        FCurve = 5,

        Finish = End,
        PushLiteral = PushValue,
        PushParameter = FindAndPushValue,
        Remap = FCurve,
    }

    [TC(typeof(EXP))] public abstract class MrfStateOperator : IMetaXmlItem
    {
        // rage::mvNodeStateDef::Operator

        public MrfOperatorType Type { get; set; }      //0, 2, 4, 5

        public MrfStateOperator(MrfOperatorType type)
        {
            Type = type;
        }
        public MrfStateOperator(DataReader r)
        {
            Type = (MrfOperatorType)r.ReadUInt32();
        }

        public virtual void Write(DataWriter w)
        {
            w.Write((uint)Type);
        }

        public virtual void ReadXml(XmlNode node) { }
        public virtual void WriteXml(StringBuilder sb, int indent) { }

        public override string ToString()
        {
            return Type.ToString();
        }

        public static MrfStateOperator CreateOperator(MrfOperatorType type)
        {
            switch (type)
            {
                case MrfOperatorType.Finish:        return new MrfStateOperatorEnd();
                case MrfOperatorType.PushLiteral:   return new MrfStateOperatorPushValue();
                case MrfOperatorType.PushParameter: return new MrfStateOperatorFindAndPushValue();
                case MrfOperatorType.Add:           return new MrfStateOperatorAdd();
                case MrfOperatorType.Multiply:      return new MrfStateOperatorMultiply();
                case MrfOperatorType.Remap:         return new MrfStateOperatorFCurve();
                default: throw new Exception($"Unknown operator type ({type})");
            }
        }
    }

    [TC(typeof(EXP))] public class MrfStateOperatorEnd : MrfStateOperator
    {
        public MrfStateOperatorEnd() : base(MrfOperatorType.End) { }
        public MrfStateOperatorEnd(DataReader r) : base(r)
        {
            r.Position += sizeof(uint);
        }

        public override void Write(DataWriter w)
        {
            base.Write(w);
            w.Write(0u);
        }
    }

    [TC(typeof(EXP))] public class MrfStateOperatorPushValue : MrfStateOperator
    {
        public float Value { get; set; }

        public MrfStateOperatorPushValue() : base(MrfOperatorType.PushValue) { }
        public MrfStateOperatorPushValue(DataReader r) : base(r)
        {
            Value = r.ReadSingle();
        }

        public override void Write(DataWriter w)
        {
            base.Write(w);
            w.Write(Value);
        }

        public override void ReadXml(XmlNode node)
        {
            Value = Xml.GetChildFloatAttribute(node, "Value");
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            MrfXml.ValueTag(sb, indent, "Value", FloatUtil.ToString(Value));
        }

        public override string ToString()
        {
            return Type + " " + FloatUtil.ToString(Value);
        }
    }

    [TC(typeof(EXP))] public class MrfStateOperatorFindAndPushValue : MrfStateOperator
    {
        public MetaHash ParameterName { get; set; }

        public MrfStateOperatorFindAndPushValue() : base(MrfOperatorType.FindAndPushValue) { }
        public MrfStateOperatorFindAndPushValue(DataReader r) : base(r)
        {
            ParameterName = r.ReadUInt32();
        }

        public override void Write(DataWriter w)
        {
            base.Write(w);
            w.Write(ParameterName);
        }

        public override void ReadXml(XmlNode node)
        {
            ParameterName = XmlMeta.GetHash(Xml.GetChildInnerText(node, "ParameterName"));
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            MrfXml.StringTag(sb, indent, "ParameterName", MrfXml.HashString(ParameterName));
        }

        public override string ToString()
        {
            return Type + " '" + ParameterName + "'";
        }
    }

    [TC(typeof(EXP))] public class MrfStateOperatorAdd : MrfStateOperator
    {
        public MrfStateOperatorAdd() : base(MrfOperatorType.Add) { }
        public MrfStateOperatorAdd(DataReader r) : base(r)
        {
            r.Position += sizeof(uint);
        }

        public override void Write(DataWriter w)
        {
            base.Write(w);
            w.Write(0u);
        }
    }

    [TC(typeof(EXP))] public class MrfStateOperatorMultiply : MrfStateOperator
    {
        public MrfStateOperatorMultiply() : base(MrfOperatorType.Multiply) { }
        public MrfStateOperatorMultiply(DataReader r) : base(r)
        {
            r.Position += sizeof(uint);
        }

        public override void Write(DataWriter w)
        {
            base.Write(w);
            w.Write(0u);
        }
    }

    [TC(typeof(EXP))] public class MrfStateOperatorFCurve : MrfStateOperator
    {
        public int DataOffset { get; set; } = 4;
        public float ClampMinimum { get; set; }
        public float ClampMaximum { get; set; }
        public uint KeyframeCount { get; set; }
        public int KeyframesOffset { get; set; } = 4;

        public MrfStateOperatorFCurveKeyframe[] Keyframes { get; set; } = [];

        public MrfStateOperatorFCurve() : base(MrfOperatorType.FCurve) { }
        public MrfStateOperatorFCurve(DataReader r) : base(r)
        {
            DataOffset = r.ReadInt32();
            if (DataOffset != sizeof(int))
                throw new InvalidDataException($"Movement FCurve data offset must be 4, but was {DataOffset}.");
            ClampMinimum = r.ReadSingle();
            ClampMaximum = r.ReadSingle();
            KeyframeCount = r.ReadUInt32();
            KeyframesOffset = r.ReadInt32();
            if (KeyframesOffset != sizeof(int))
                throw new InvalidDataException($"Movement FCurve keyframe offset must be 4, but was {KeyframesOffset}.");
            if (KeyframeCount > int.MaxValue)
                throw new InvalidDataException("Movement FCurve has too many keyframes.");
            Keyframes = new MrfStateOperatorFCurveKeyframe[KeyframeCount];
            for (int i = 0; i < KeyframeCount; i++)
            {
                Keyframes[i] = new MrfStateOperatorFCurveKeyframe(r);
            }
        }

        public override void Write(DataWriter w)
        {
            base.Write(w);

            Keyframes ??= [];
            KeyframeCount = checked((uint)Keyframes.Length);
            DataOffset = sizeof(int);
            KeyframesOffset = sizeof(int);

            w.Write(DataOffset);
            w.Write(ClampMinimum);
            w.Write(ClampMaximum);
            w.Write(KeyframeCount);
            w.Write(KeyframesOffset);

            foreach (var item in Keyframes)
                item.Write(w);
        }

        public override void ReadXml(XmlNode node)
        {
            ClampMinimum = Xml.GetChildFloatAttribute(node, "ClampMinimum");
            ClampMaximum = Xml.GetChildFloatAttribute(node, "ClampMaximum");
            if (node.SelectSingleNode("ClampMinimum") == null) ClampMinimum = Xml.GetChildFloatAttribute(node, "Min");
            if (node.SelectSingleNode("ClampMaximum") == null) ClampMaximum = Xml.GetChildFloatAttribute(node, "Max");
            Keyframes = XmlMeta.ReadItemArray<MrfStateOperatorFCurveKeyframe>(node, "Keyframes");
            if (Keyframes.Length == 0) Keyframes = XmlMeta.ReadItemArray<MrfStateOperatorFCurveKeyframe>(node, "Ranges");
            KeyframeCount = checked((uint)Keyframes.Length);
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            MrfXml.ValueTag(sb, indent, "ClampMinimum", FloatUtil.ToString(ClampMinimum));
            MrfXml.ValueTag(sb, indent, "ClampMaximum", FloatUtil.ToString(ClampMaximum));
            MrfXml.WriteItemArray(sb, Keyframes, indent, "Keyframes");
        }

        public override string ToString()
        {
            return Type + " (" + FloatUtil.ToString(ClampMinimum) + ".." + FloatUtil.ToString(ClampMaximum) + ") -> [" + string.Join(",", Keyframes.AsEnumerable()) + "]";
        }
    }

    [TC(typeof(EXP))] public class MrfStateOperatorFCurveKeyframe : IMetaXmlItem
    {
        public uint Type { get; set; }
        public float Key { get; set; }
        public float ConstantM { get; set; }
        public float ConstantB { get; set; }

        public MrfStateOperatorFCurveKeyframe() { }
        public MrfStateOperatorFCurveKeyframe(DataReader r)
        {
            Type = r.ReadUInt32();
            Key = r.ReadSingle();
            ConstantM = r.ReadSingle();
            ConstantB = r.ReadSingle();
        }

        public void Write(DataWriter w)
        {
            w.Write(Type);
            w.Write(Key);
            w.Write(ConstantM);
            w.Write(ConstantB);
        }

        public void WriteXml(StringBuilder sb, int indent)
        {
            MrfXml.ValueTag(sb, indent, "Type", Type.ToString());
            MrfXml.ValueTag(sb, indent, "Key", FloatUtil.ToString(Key));
            MrfXml.ValueTag(sb, indent, "ConstantM", FloatUtil.ToString(ConstantM));
            MrfXml.ValueTag(sb, indent, "ConstantB", FloatUtil.ToString(ConstantB));
        }

        public void ReadXml(XmlNode node)
        {
            Type = Xml.GetChildUIntAttribute(node, "Type");
            Key = Xml.GetChildFloatAttribute(node, "Key");
            ConstantM = Xml.GetChildFloatAttribute(node, "ConstantM");
            ConstantB = Xml.GetChildFloatAttribute(node, "ConstantB");
            if (node.SelectSingleNode("Key") == null) Key = Xml.GetChildFloatAttribute(node, "Percent");
            if (node.SelectSingleNode("ConstantM") == null) ConstantM = Xml.GetChildFloatAttribute(node, "Length");
            if (node.SelectSingleNode("ConstantB") == null) ConstantB = Xml.GetChildFloatAttribute(node, "Min");
        }

        public override string ToString()
        {
            return $"{FloatUtil.ToString(Key)}: {FloatUtil.ToString(ConstantM)}x + {FloatUtil.ToString(ConstantB)}";
        }
    }

    /// <summary>
    /// Before the node updates, calculates the specified operations and stores the value in a node parameter.
    /// </summary>
    [TC(typeof(EXP))] public class MrfStateOperation : IMetaXmlItem
    {
        // rage::mvNodeStateDef::Operation

        public ushort NodeIndex { get; set; }
        public /*MrfNodeParameterId*/ushort NodeParameterId { get; set; }
        public ushort Size { get; set; }
        public ushort ParameterIndex { get; set; }
        public MrfStateOperator[] Operators { get; set; } = [];

        public MrfStateOperation() { }
        public MrfStateOperation(DataReader r)
        {
            NodeIndex = r.ReadUInt16();
            NodeParameterId = r.ReadUInt16();
            Size = r.ReadUInt16();
            ParameterIndex = r.ReadUInt16();

            if (Size == 0 || (Size & 7) != 0)
                throw new InvalidDataException($"Movement state operation size must be a non-zero multiple of 8, but was {Size}.");

            var operators = new List<MrfStateOperator>();

            int packetCount = Size / 8;
            for (int packetIndex = 0; packetIndex < packetCount; packetIndex++)
            {
                var startPos = r.Position;
                var opType = (MrfOperatorType)r.ReadUInt32();
                r.Position = startPos;

                MrfStateOperator op;
                switch (opType)
                {
                    case MrfOperatorType.Finish:        op = new MrfStateOperatorEnd(r); break;
                    case MrfOperatorType.PushLiteral:   op = new MrfStateOperatorPushValue(r); break;
                    case MrfOperatorType.PushParameter: op = new MrfStateOperatorFindAndPushValue(r); break;
                    case MrfOperatorType.Add:           op = new MrfStateOperatorAdd(r); break;
                    case MrfOperatorType.Multiply:      op = new MrfStateOperatorMultiply(r); break;
                    case MrfOperatorType.Remap:         op = new MrfStateOperatorFCurve(r); break;
                    default: throw new Exception($"Unknown operator type ({opType})");
                }

                operators.Add(op);
                if (opType == MrfOperatorType.Finish)
                {
                    break;
                }
            }

            Operators = operators.ToArray();

            if (Size != Operators.Length * 8)
                throw new InvalidDataException($"Movement state operation size is {Size}, but contains {Operators.Length * 8} bytes of operators.");
            if (Operators[^1].Type != MrfOperatorType.End)
                throw new InvalidDataException("Movement state operation does not end with an End operator.");
        }

        public void Write(DataWriter w)
        {
            Operators ??= [];
            if (Operators.Length == 0 || Operators[^1].Type != MrfOperatorType.End)
                throw new InvalidDataException("Movement state operations must end with an End operator.");
            Size = checked((ushort)(Operators.Length * 8));
            w.Write(NodeIndex);
            w.Write(NodeParameterId);
            w.Write(Size);
            w.Write(ParameterIndex);

            foreach (var op in Operators)
                op.Write(w);
        }

        public void ReadXml(XmlNode node)
        {
            NodeIndex = (ushort)Xml.GetChildUIntAttribute(node, "NodeIndex");
            NodeParameterId = (ushort)Xml.GetChildUIntAttribute(node, "NodeParameterId");
            ParameterIndex = (ushort)Xml.GetChildUIntAttribute(node, "ParameterIndex");
            if (node.SelectSingleNode("ParameterIndex") == null)
                ParameterIndex = (ushort)Xml.GetChildUIntAttribute(node, "NodeParameterExtraArg");
            Operators = [];
            var operatorsNode = node.SelectSingleNode("Operators");
            if (operatorsNode != null)
            {
                var inodes = operatorsNode.SelectNodes("Item");
                if (inodes?.Count > 0)
                {
                    Operators = new MrfStateOperator[inodes.Count];
                    int i = 0;
                    foreach (XmlNode inode in inodes)
                    {
                        Operators[i] = XmlMrf.ReadOperator(inode) ?? throw new InvalidDataException("Movement XML contains an invalid child element.");
                        i++;
                    }
                }
            }
            Size = checked((ushort)((Operators?.Length ?? 0) * 8));
        }

        public void WriteXml(StringBuilder sb, int indent)
        {
            MrfXml.ValueTag(sb, indent, "NodeIndex", NodeIndex.ToString());
            MrfXml.ValueTag(sb, indent, "NodeParameterId", NodeParameterId.ToString());
            MrfXml.ValueTag(sb, indent, "ParameterIndex", ParameterIndex.ToString());
            int cindent = indent + 1;
            MrfXml.OpenTag(sb, indent, "Operators");
            foreach (var op in Operators)
            {
                MrfXml.WriteOperator(sb, cindent, "Item", op);
            }
            MrfXml.CloseTag(sb, indent, "Operators");
        }

        public override string ToString()
        {
            return NodeIndex.ToString() + " - " + NodeParameterId.ToString() + " - " + Size.ToString() + " - " + ParameterIndex.ToString() + " - " +
                (Operators?.Length ?? 0).ToString() + " operators";
        }
    }
    
#endregion

#region mrf node classes
    
    [TC(typeof(EXP))] public class MrfNodeStateMachine : MrfNodeStateBase
    {
        // rage__mvNodeStateMachineClass (1)

        public MrfStateRef[] States { get; set; } = [];

        public MrfNodeStateMachine() : base(MrfNodeType.StateMachine) { }

        public override void Read(DataReader r)
        {
            base.Read(r);


            if (ChildCount > 0)
            {
                States = new MrfStateRef[ChildCount];
                for (int i = 0; i < ChildCount; i++)
                    States[i] = new MrfStateRef(r);

            }

            if (TransitionCount > 0)
            {
                if (r.Position != TransitionsFileOffset)
                    throw new InvalidDataException($"Movement transition table begins at {TransitionsFileOffset}, but the reader is at {r.Position}.");

                Transitions = new MrfStateTransition[TransitionCount];
                for (int i = 0; i < TransitionCount; i++)
                    Transitions[i] = new MrfStateTransition(r);
            }
        }

        public override void Write(DataWriter w)
        {
            ChildCount = checked((byte)(States?.Length ?? 0));
            TransitionCount = checked((byte)(Transitions?.Length ?? 0));

            base.Write(w);

            if (States != null)
                foreach (var state in States)
                    state.Write(w);

            if (Transitions != null)
                foreach(var transition in Transitions)
                    transition.Write(w);
        }

        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);

            States = [];
            var statesNode = node.SelectSingleNode("States");
            if (statesNode != null)
            {
                var inodes = statesNode.SelectNodes("Item");
                if (inodes?.Count > 0)
                {
                    States = new MrfStateRef[inodes.Count];
                    int i = 0;
                    foreach (XmlNode inode in inodes)
                    {
                        var s = new MrfStateRef();
                        s.State = XmlMrf.ReadNode(inode) as MrfNodeStateBase ?? throw new InvalidDataException("A movement state element is invalid.");
                        s.StateName = s.State.ID;
                        States[i] = s;
                        i++;
                    }
                }
            }
            ChildCount = checked((byte)(States?.Length ?? 0));
            var initialStateName = XmlMrf.ReadChildNodeRef(node, "InitialState");
            InitialNode = States?.FirstOrDefault(s => s.StateName == initialStateName)?.State;
            Transitions = XmlMeta.ReadItemArray<MrfStateTransition>(node, "Transitions");
            TransitionCount = checked((byte)(Transitions?.Length ?? 0));

            ResolveXmlTargetStatesInTransitions(States);
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);

            MrfXml.WriteNodeRef(sb, indent, "InitialState", InitialNode);
            int cindent = indent + 1;
            MrfXml.OpenTag(sb, indent, "States");
            foreach (var s in States)
            {
                MrfXml.WriteNode(sb, cindent, "Item", s.State);
            }
            MrfXml.CloseTag(sb, indent, "States");
            MrfXml.WriteItemArray(sb, Transitions, indent, "Transitions");
        }

        public override void ResolveRelativeOffsets(MrfFile mrf)
        {
            base.ResolveRelativeOffsets(mrf);

            ResolveNodeOffsetsInTransitions(Transitions, mrf);
            ResolveNodeOffsetsInStates(States, mrf);
        }

        public override void UpdateRelativeOffsets()
        {
            base.UpdateRelativeOffsets();

            var offset = (int)(FileOffset + 0x20/*sizeof(MrfNodeStateBase)*/);
            offset = UpdateNodeOffsetsInStates(States, offset);

            offset = UpdateNodeOffsetsInTransitions(Transitions, offset,
                        offsetSetToZeroIfNoTransitions: TransitionsOffset == 0); // MrfNodeStateMachine doesn't seem consistent on whether TransitionsOffset
                                                                                 // should be 0 if there are no transitions, so if it's already zero don't change it
        }
    }

    [TC(typeof(EXP))] public class MrfNodeTail : MrfNode
    {
        // rage__mvNodeTail (2)

        public MrfNodeTail() : base(MrfNodeType.Tail) { }
    }

    [TC(typeof(EXP))] public class MrfNodeInlinedStateMachine : MrfNodeStateBase
    {
        // rage__mvNodeInlinedStateMachine (3)

        public int FallbackNodeOffset { get; set; }
        public int FallbackNodeFileOffset { get; set; }
        public MrfStateRef[] States { get; set; } = [];

        public MrfNode? FallbackNode { get; set; } // node used when a NodeTail is reached (maybe in some other cases too?). This node is considered a child
                                                  // of the parent NodeState, so FallbackNode and its children (including their index) should be
                                                  // included in the parent NodeState.ChildCount, not in this NodeInlinedStateMachine.ChildCount

        public MrfNodeInlinedStateMachine() : base(MrfNodeType.InlinedStateMachine) { }

        public override void Read(DataReader r)
        {
            base.Read(r);

            FallbackNodeOffset = r.ReadInt32();
            FallbackNodeFileOffset = checked((int)(r.Position + FallbackNodeOffset - 4));

            if (ChildCount > 0)
            {
                States = new MrfStateRef[ChildCount];
                for (int i = 0; i < ChildCount; i++)
                    States[i] = new MrfStateRef(r);
            }

            if (TransitionCount > 0)
            {
                if (r.Position != TransitionsFileOffset)
                    throw new InvalidDataException($"Movement transition table begins at {TransitionsFileOffset}, but the reader is at {r.Position}.");
                Transitions = new MrfStateTransition[TransitionCount];
                for (int i = 0; i < TransitionCount; i++)
                    Transitions[i] = new MrfStateTransition(r);
            }
        }

        public override void Write(DataWriter w)
        {
            ChildCount = checked((byte)(States?.Length ?? 0));
            TransitionCount = checked((byte)(Transitions?.Length ?? 0));

            base.Write(w);

            w.Write(FallbackNodeOffset);

            if (States != null)
                foreach (var item in States)
                    item.Write(w);

            if (Transitions != null)
                foreach (var transition in Transitions)
                    transition.Write(w);
        }

        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);

            States = [];
            var statesNode = node.SelectSingleNode("States");
            if (statesNode != null)
            {
                var inodes = statesNode.SelectNodes("Item");
                if (inodes?.Count > 0)
                {
                    States = new MrfStateRef[inodes.Count];
                    int i = 0;
                    foreach (XmlNode inode in inodes)
                    {
                        var s = new MrfStateRef();
                        s.State = XmlMrf.ReadNode(inode) as MrfNodeStateBase ?? throw new InvalidDataException("A movement state element is invalid.");
                        s.StateName = s.State.ID;
                        States[i] = s;
                        i++;
                    }
                }
            }
            ChildCount = checked((byte)(States?.Length ?? 0));
            var initialStateName = XmlMrf.ReadChildNodeRef(node, "InitialState");
            InitialNode = States?.FirstOrDefault(s => s.StateName == initialStateName)?.State;
            FallbackNode = XmlMrf.ReadChildNode(node, "FallbackNode");
            Transitions = XmlMeta.ReadItemArray<MrfStateTransition>(node, "Transitions");
            TransitionCount = checked((byte)Transitions.Length);

            ResolveXmlTargetStatesInTransitions(States);
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);

            MrfXml.WriteNodeRef(sb, indent, "InitialState", InitialNode);
            int cindent = indent + 1;
            MrfXml.OpenTag(sb, indent, "States");
            foreach (var s in States)
            {
                MrfXml.WriteNode(sb, cindent, "Item", s.State);
            }
            MrfXml.CloseTag(sb, indent, "States");
            MrfXml.WriteNode(sb, indent, "FallbackNode", FallbackNode);
            MrfXml.WriteItemArray(sb, Transitions, indent, "Transitions");
        }

        public override void ResolveRelativeOffsets(MrfFile mrf)
        {
            base.ResolveRelativeOffsets(mrf);

            var fallbackNode = mrf.FindNodeAtFileOffset(FallbackNodeFileOffset);
            if (fallbackNode == null)
                throw new InvalidDataException($"Movement fallback node at file offset {FallbackNodeFileOffset} could not be resolved.");

            FallbackNode = fallbackNode;

            ResolveNodeOffsetsInStates(States, mrf);
            ResolveNodeOffsetsInTransitions(Transitions, mrf);
        }

        public override void UpdateRelativeOffsets()
        {
            base.UpdateRelativeOffsets();

            var offset = FileOffset + 0x20/*sizeof(MrfNodeStateBase)*/;

            FallbackNodeFileOffset = (FallbackNode ?? throw new InvalidDataException("A movement node reference is unresolved.")).FileOffset;
            FallbackNodeOffset = FallbackNodeFileOffset - offset;

            offset += 4;
            offset = UpdateNodeOffsetsInStates(States, offset);

            offset = UpdateNodeOffsetsInTransitions(Transitions, offset, offsetSetToZeroIfNoTransitions: true);
        }

        public override string ToString()
        {
            return base.ToString() + " - " + FallbackNodeOffset.ToString();
        }
    }

    [TC(typeof(EXP))] public class MrfNodeAnimation : MrfNodeWithFlagsBase
    {
        // rage__mvNode* (4) not used in final game
        // Probably not worth researching further. Seems like the introduction of NodeClip (and rage::crClip), made this node obsolete.
        // Even the function pointer used to lookup the rage::crAnimation when AnimationType==Literal is null, so the only way to get animations is through a parameter.

        public uint AnimationFilenameLength { get; set; }
        public byte[] AnimationFilenameData { get; set; } = [];
        public string AnimationFilename
        {
            get => Encoding.UTF8.GetString(AnimationFilenameData).TrimEnd('\0');
            set => AnimationFilenameData = EncodeFilename(value);
        }
        public MetaHash AnimationParameterName { get; set; }
        public float Phase { get; set; }
        public MetaHash PhaseParameterName { get; set; }
        public float Rate { get; set; }
        public MetaHash RateParameterName { get; set; }
        public float Delta { get; set; }
        public MetaHash DeltaParameterName { get; set; }
        public bool Looped { get; set; }
        public MetaHash LoopedParameterName { get; set; }
        public bool Absolute { get; set; }
        public MetaHash AbsoluteParameterName { get; set; }

        // flags getters and setters
        public MrfValueType AnimationType
        {
            get => (MrfValueType)GetFlagsSubset(0, 3);
            set => SetFlagsSubset(0, 3, (uint)value);
        }
        public MrfValueType PhaseType { get => (MrfValueType)GetFlagsSubset(2, 3); set => SetFlagsSubset(2, 3, (uint)value); }
        public MrfValueType RateType { get => (MrfValueType)GetFlagsSubset(4, 3); set => SetFlagsSubset(4, 3, (uint)value); }
        public MrfValueType DeltaType { get => (MrfValueType)GetFlagsSubset(6, 3); set => SetFlagsSubset(6, 3, (uint)value); }
        public MrfValueType LoopedType { get => (MrfValueType)GetFlagsSubset(8, 3); set => SetFlagsSubset(8, 3, (uint)value); }
        public MrfValueType AbsoluteType { get => (MrfValueType)GetFlagsSubset(10, 3); set => SetFlagsSubset(10, 3, (uint)value); }

        public MrfNodeAnimation() : base(MrfNodeType.Animation) { }

        public override void Read(DataReader r)
        {
            base.Read(r);

            switch (AnimationType)
            {
                case MrfValueType.Literal:
                    {
                        AnimationFilenameLength = r.ReadUInt32();
                        AnimationFilenameData = r.ReadBytes(checked((int)AnimationFilenameLength));
                        break;
                    }
                case MrfValueType.Parameter:
                    AnimationParameterName = r.ReadUInt32();
                    break;
            }

            (Phase, PhaseParameterName) = ReadFloat(r, PhaseType);
            (Rate, RateParameterName) = ReadFloat(r, RateType);
            (Delta, DeltaParameterName) = ReadFloat(r, DeltaType);
            (Looped, LoopedParameterName) = ReadBool(r, LoopedType);
            (Absolute, AbsoluteParameterName) = ReadBool(r, AbsoluteType);
        }

        public override void Write(DataWriter w)
        {
            base.Write(w);

            switch (AnimationType)
            {
                case MrfValueType.Literal:
                    {
                        AnimationFilenameData ??= [];
                        AnimationFilenameLength = checked((uint)AnimationFilenameData.Length);
                        w.Write(AnimationFilenameLength);
                        w.Write(AnimationFilenameData);
                        break;
                    }
                case MrfValueType.Parameter:
                    w.Write(AnimationParameterName);
                    break;
            }

            WriteFloat(w, PhaseType, Phase, PhaseParameterName);
            WriteFloat(w, RateType, Rate, RateParameterName);
            WriteFloat(w, DeltaType, Delta, DeltaParameterName);
            WriteBool(w, LoopedType, Looped, LoopedParameterName);
            WriteBool(w, AbsoluteType, Absolute, AbsoluteParameterName);
        }

        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            var animationNode = node.SelectSingleNode("Animation");
            if (animationNode?.Attributes?["filename"] != null)
            {
                AnimationType = MrfValueType.Literal;
                AnimationFilename = Xml.GetStringAttribute(animationNode, "filename") ?? string.Empty;
            }
            else if (animationNode?.Attributes?["parameter"] != null)
            {
                AnimationType = MrfValueType.Parameter;
                AnimationParameterName = XmlMeta.GetHash(Xml.GetStringAttribute(animationNode, "parameter"));
            }
            else AnimationType = MrfValueType.None;
            (PhaseType, Phase, PhaseParameterName) = XmlMrf.GetChildParameterizedFloat(node, "Phase");
            (RateType, Rate, RateParameterName) = XmlMrf.GetChildParameterizedFloat(node, "Rate");
            (DeltaType, Delta, DeltaParameterName) = XmlMrf.GetChildParameterizedFloat(node, "Delta");
            (LoopedType, Looped, LoopedParameterName) = XmlMrf.GetChildParameterizedBool(node, "Looped");
            (AbsoluteType, Absolute, AbsoluteParameterName) = XmlMrf.GetChildParameterizedBool(node, "Absolute");
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            MrfXml.ParameterizedFilenameTag(sb, indent, "Animation", AnimationType, AnimationFilename, AnimationParameterName);
            MrfXml.ParameterizedFloatTag(sb, indent, "Phase", PhaseType, Phase, PhaseParameterName);
            MrfXml.ParameterizedFloatTag(sb, indent, "Rate", RateType, Rate, RateParameterName);
            MrfXml.ParameterizedFloatTag(sb, indent, "Delta", DeltaType, Delta, DeltaParameterName);
            MrfXml.ParameterizedBoolTag(sb, indent, "Looped", LoopedType, Looped, LoopedParameterName);
            MrfXml.ParameterizedBoolTag(sb, indent, "Absolute", AbsoluteType, Absolute, AbsoluteParameterName);
        }

        private static (float Value, MetaHash ParameterName) ReadFloat(DataReader r, MrfValueType type) =>
            type == MrfValueType.Literal ? (r.ReadSingle(), 0) : type == MrfValueType.Parameter ? (0, r.ReadUInt32()) : (0, 0);
        private static (bool Value, MetaHash ParameterName) ReadBool(DataReader r, MrfValueType type) =>
            type == MrfValueType.Literal ? (r.ReadUInt32() != 0, 0) : type == MrfValueType.Parameter ? (false, r.ReadUInt32()) : (false, 0);
        private static void WriteFloat(DataWriter w, MrfValueType type, float value, MetaHash parameterName)
        {
            if (type == MrfValueType.Literal) w.Write(value);
            else if (type == MrfValueType.Parameter) w.Write(parameterName);
        }
        private static void WriteBool(DataWriter w, MrfValueType type, bool value, MetaHash parameterName)
        {
            if (type == MrfValueType.Literal) w.Write(value ? 0x01000000u : 0u);
            else if (type == MrfValueType.Parameter) w.Write(parameterName);
        }
        internal static byte[] EncodeFilename(string value)
        {
            byte[] text = Encoding.UTF8.GetBytes(value ?? string.Empty);
            int length = checked((text.Length + 5) & ~3);
            byte[] data = new byte[length];
            text.CopyTo(data, 0);
            return data;
        }
    }

    [TC(typeof(EXP))] public class MrfNodeBlend : MrfNodePairWeightedBase
    {
        // rage__mvNodeBlend (5)

        public MrfNodeBlend() : base(MrfNodeType.Blend) { }
    }

    [TC(typeof(EXP))] public class MrfNodeAddSubtract : MrfNodePairWeightedBase
    {
        // rage__mvNodeAddSubtract (6)

        public MrfNodeAddSubtract() : base(MrfNodeType.AddSubtract) { }
    }

    [TC(typeof(EXP))] public class MrfNodeFilter : MrfNodeWithChildAndFilterBase
    {
        // rage__mvNodeFilter (7)

        public MrfNodeFilter() : base(MrfNodeType.Filter) { }
    }

    [TC(typeof(EXP))] public class MrfNodeMirror : MrfNodeWithChildAndFilterBase
    {
        // rage__mvNodeMirror (8)

        public MrfNodeMirror() : base(MrfNodeType.Mirror) { }
    }

    [TC(typeof(EXP))] public class MrfNodeFrame : MrfNodeWithFlagsBase
    {
        // rage__mvNodeFrame (9)

        public MetaHash FrameParameterName { get; set; }
        public bool Owner { get; set; }
        public MetaHash OwnerParameterName { get; set; }

        // flags getters and setters
        public MrfValueType FrameType // only Parameter type is supported
        {
            get => (MrfValueType)GetFlagsSubset(0, 3);
            set => SetFlagsSubset(0, 3, (uint)value);
        }
        public MrfValueType OwnerType
        {
            get => (MrfValueType)GetFlagsSubset(4, 3);
            set => SetFlagsSubset(4, 3, (uint)value);
        }

        public MrfNodeFrame() : base(MrfNodeType.Frame) { }

        public override void Read(DataReader r)
        {
            base.Read(r);

            if (FrameType != MrfValueType.None)
                FrameParameterName = r.ReadUInt32();

            switch (OwnerType)
            {
                case MrfValueType.Literal: Owner = r.ReadUInt32() != 0; break;
                case MrfValueType.Parameter: OwnerParameterName = r.ReadUInt32(); break;
            }
        }

        public override void Write(DataWriter w)
        {
            base.Write(w);

            if (FrameType != MrfValueType.None)
                w.Write(FrameParameterName);

            switch (OwnerType)
            {
                case MrfValueType.Literal: w.Write(Owner ? 0x01000000u : 0u); break;
                case MrfValueType.Parameter: w.Write(OwnerParameterName); break;
            }
        }

        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);

            (FrameType, _, _, FrameParameterName) = XmlMrf.GetChildParameterizedAsset(node, "Frame");
            (OwnerType, Owner, OwnerParameterName) = XmlMrf.GetChildParameterizedBool(node, "Owner");
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);

            MrfXml.ParameterizedAssetTag(sb, indent, "Frame", FrameType, 0, 0, FrameParameterName);
            MrfXml.ParameterizedBoolTag(sb, indent, "Owner", OwnerType, Owner, OwnerParameterName);
        }
    }

    [TC(typeof(EXP))] public class MrfNodeIk : MrfNode
    {
        // rage__mvNodeIk (10)

        public MrfNodeIk() : base(MrfNodeType.Ik) { }
    }

    [TC(typeof(EXP))] public class MrfNodeBlendN : MrfNodeNBase
    {
        // rage__mvNodeBlendN (13)

        public MrfNodeBlendN() : base(MrfNodeType.BlendN) { }
    }

    public enum MrfClipContainerType : uint
    {
        VariableClipSet = 0,
        ClipDictionary = 1,
        AbsoluteClipSet = 2,
        LocalFile = 3,

        ClipSet = AbsoluteClipSet,
        Unk3 = LocalFile,
    }

    [TC(typeof(EXP))] public class MrfNodeClip : MrfNodeWithFlagsBase
    {
        // rage__mvNodeClip (15)

        public MetaHash ClipParameterName { get; set; }
        public MrfClipContainerType ClipContainerType { get; set; }
        public MetaHash ClipContainerName { get; set; }
        public MetaHash ClipName { get; set; }
        public float Phase { get; set; }
        public MetaHash PhaseParameterName { get; set; }
        public float Rate { get; set; }
        public MetaHash RateParameterName { get; set; }
        public float Delta { get; set; }
        public MetaHash DeltaParameterName { get; set; }
        public bool Looped { get; set; }
        public MetaHash LoopedParameterName { get; set; }

        // flags getters and setters
        public MrfValueType ClipType
        {
            get => (MrfValueType)GetFlagsSubset(0, 3);
            set => SetFlagsSubset(0, 3, (uint)value);
        }
        public MrfValueType PhaseType
        {
            get => (MrfValueType)GetFlagsSubset(2, 3);
            set => SetFlagsSubset(2, 3, (uint)value);
        }
        public MrfValueType RateType
        {
            get => (MrfValueType)GetFlagsSubset(4, 3);
            set => SetFlagsSubset(4, 3, (uint)value);
        }
        public MrfValueType DeltaType
        {
            get => (MrfValueType)GetFlagsSubset(6, 3);
            set => SetFlagsSubset(6, 3, (uint)value);
        }
        public MrfValueType LoopedType
        {
            get => (MrfValueType)GetFlagsSubset(8, 3);
            set => SetFlagsSubset(8, 3, (uint)value);
        }
        public bool IsZeroWeight
        {
            get => GetFlagsSubset(10, 1) != 0;
            set => SetFlagsSubset(10, 1, value ? 1u : 0u);
        }

        public MrfNodeClip() : base(MrfNodeType.Clip) { }

        public override void Read(DataReader r)
        {
            base.Read(r);

            switch (ClipType)
            {
                case MrfValueType.Literal:
                    {
                        ClipContainerType = (MrfClipContainerType)r.ReadUInt32();
                        if (ClipContainerType == MrfClipContainerType.LocalFile)
                            ClipName = r.ReadUInt32();
                        else
                        {
                            ClipContainerName = r.ReadUInt32();
                            ClipName = r.ReadUInt32();
                        }
                        break;
                    }
                case MrfValueType.Parameter:
                    ClipParameterName = r.ReadUInt32();
                    break;
            }

            switch (PhaseType)
            {
                case MrfValueType.Literal:
                    Phase = r.ReadSingle();
                    break;
                case MrfValueType.Parameter:
                    PhaseParameterName = r.ReadUInt32();
                    break;
            }

            switch (RateType)
            {
                case MrfValueType.Literal:
                    Rate = r.ReadSingle();
                    break;
                case MrfValueType.Parameter:
                    RateParameterName = r.ReadUInt32();
                    break;
            }

            switch (DeltaType)
            {
                case MrfValueType.Literal:
                    Delta = r.ReadSingle();
                    break;
                case MrfValueType.Parameter:
                    DeltaParameterName = r.ReadUInt32();
                    break;
            }

            switch (LoopedType)
            {
                case MrfValueType.Literal:
                    Looped = r.ReadUInt32() != 0;
                    break;
                case MrfValueType.Parameter:
                    LoopedParameterName = r.ReadUInt32();
                    break;
            }
        }

        public override void Write(DataWriter w)
        {
            base.Write(w);

            switch (ClipType)
            {
                case MrfValueType.Literal:
                    {
                        w.Write((uint)ClipContainerType);
                        if (ClipContainerType == MrfClipContainerType.LocalFile)
                            w.Write(ClipName);
                        else
                        {
                            w.Write(ClipContainerName);
                            w.Write(ClipName);
                        }

                        break;
                    }
                case MrfValueType.Parameter:
                    w.Write(ClipParameterName);
                    break;
            }

            switch (PhaseType)
            {
                case MrfValueType.Literal:
                    w.Write(Phase);
                    break;
                case MrfValueType.Parameter:
                    w.Write(PhaseParameterName);
                    break;
            }

            switch (RateType)
            {
                case MrfValueType.Literal:
                    w.Write(Rate);
                    break;
                case MrfValueType.Parameter:
                    w.Write(RateParameterName);
                    break;
            }

            switch (DeltaType)
            {
                case MrfValueType.Literal:
                    w.Write(Delta);
                    break;
                case MrfValueType.Parameter:
                    w.Write(DeltaParameterName);
                    break;
            }

            switch (LoopedType)
            {
                case MrfValueType.Literal:
                    w.Write(Looped ? 0x01000000 : 0u); // bool originally stored as a big-endian uint, game just checks != 0. Here we do it to output the same bytes as the input
                    break;
                case MrfValueType.Parameter:
                    w.Write(LoopedParameterName);
                    break;
            }
        }

        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);

            (ClipType, ClipContainerType, ClipContainerName, ClipName, ClipParameterName) = XmlMrf.GetChildParameterizedClip(node, "Clip");
            (PhaseType, Phase, PhaseParameterName) = XmlMrf.GetChildParameterizedFloat(node, "Phase");
            (RateType, Rate, RateParameterName) = XmlMrf.GetChildParameterizedFloat(node, "Rate");
            (DeltaType, Delta, DeltaParameterName) = XmlMrf.GetChildParameterizedFloat(node, "Delta");
            (LoopedType, Looped, LoopedParameterName) = XmlMrf.GetChildParameterizedBool(node, "Looped");
            IsZeroWeight = Xml.GetChildBoolAttribute(node, "IsZeroWeight") || Xml.GetChildUIntAttribute(node, "UnkFlag10") != 0;
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);

            MrfXml.ParameterizedClipTag(sb, indent, "Clip", ClipType, ClipContainerType, ClipContainerName, ClipName, ClipParameterName);
            MrfXml.ParameterizedFloatTag(sb, indent, "Phase", PhaseType, Phase, PhaseParameterName);
            MrfXml.ParameterizedFloatTag(sb, indent, "Rate", RateType, Rate, RateParameterName);
            MrfXml.ParameterizedFloatTag(sb, indent, "Delta", DeltaType, Delta, DeltaParameterName);
            MrfXml.ParameterizedBoolTag(sb, indent, "Looped", LoopedType, Looped, LoopedParameterName);
            MrfXml.ValueTag(sb, indent, "IsZeroWeight", IsZeroWeight.ToString());
        }
    }

    [TC(typeof(EXP))] public class MrfNodePm : MrfNodeWithFlagsBase
    {
        // rage__mvNode* (17) not used in final game
        // The backing node is rage::crmtNodePm
        // Pm = Parameterized Motion
        // In RDR3 renamed to rage::mvNodeMotion/rage::crmtNodeMotion?

        // Seems similar to NodeClip but for rage::crpmMotion/.#pm files (added in RDR3, WIP in GTA5/MP3?)
        // In GTA5 the function pointer used to lookup the rage::crpmMotion is null

        public uint MotionFilenameLength { get; set; }
        public byte[] MotionFilenameData { get; set; } = [];
        public string MotionFilename
        {
            get => Encoding.UTF8.GetString(MotionFilenameData).TrimEnd('\0');
            set => MotionFilenameData = MrfNodeAnimation.EncodeFilename(value);
        }
        public MetaHash MotionParameterName { get; set; }
        public float Rate { get; set; }
        public MetaHash RateParameterName { get; set; }
        public float Phase { get; set; }
        public MetaHash PhaseParameterName { get; set; }
        public float Delta { get; set; }
        public MetaHash DeltaParameterName { get; set; }
        public MrfNodePmParameter[] Parameters { get; set; } = [];

        // flags getters and setters
        public MrfValueType MotionType
        {
            // Literal not supported, the function pointer used to lookup the motion is null
            get => (MrfValueType)GetFlagsSubset(0, 3);
            set => SetFlagsSubset(0, 3, (uint)value);
        }
        public MrfValueType RateType { get => (MrfValueType)GetFlagsSubset(2, 3); set => SetFlagsSubset(2, 3, (uint)value); }
        public MrfValueType PhaseType { get => (MrfValueType)GetFlagsSubset(4, 3); set => SetFlagsSubset(4, 3, (uint)value); }
        public MrfValueType DeltaType { get => (MrfValueType)GetFlagsSubset(6, 3); set => SetFlagsSubset(6, 3, (uint)value); }
        public uint ParameterCount { get => GetFlagsSubset(10, 0xF); set => SetFlagsSubset(10, 0xF, value); }
        public MrfValueType GetParameterType(int index)
        {
            if ((uint)index >= 6) throw new ArgumentOutOfRangeException(nameof(index));
            return (MrfValueType)GetFlagsSubset(19 + index * 2, 3);
        }
        public void SetParameterType(int index, MrfValueType type)
        {
            if ((uint)index >= 6) throw new ArgumentOutOfRangeException(nameof(index));
            SetFlagsSubset(19 + index * 2, 3, (uint)type);
        }

        public MrfNodePm() : base(MrfNodeType.Pm) { }

        public override void Read(DataReader r)
        {
            base.Read(r);

            switch (MotionType)
            {
                case MrfValueType.Literal:
                    {
                        MotionFilenameLength = r.ReadUInt32();
                        MotionFilenameData = r.ReadBytes(checked((int)MotionFilenameLength));
                        break;
                    }
                case MrfValueType.Parameter:
                    MotionParameterName = r.ReadUInt32();
                    break;
            }

            (Rate, RateParameterName) = ReadFloat(r, RateType);
            (Phase, PhaseParameterName) = ReadFloat(r, PhaseType);
            (Delta, DeltaParameterName) = ReadFloat(r, DeltaType);
            if (ParameterCount > 6)
                throw new InvalidDataException($"Parameterized-motion node has {ParameterCount} parameters; the native flag layout supports 6.");
            Parameters = new MrfNodePmParameter[ParameterCount];
            for (int i = 0; i < Parameters.Length; i++)
            {
                var value = ReadFloat(r, GetParameterType(i));
                Parameters[i] = new MrfNodePmParameter(value.Value, value.ParameterName);
            }
        }

        public override void Write(DataWriter w)
        {
            Parameters ??= [];
            if (Parameters.Length > 6)
                throw new InvalidDataException("Parameterized-motion nodes support at most 6 parameters.");
            ParameterCount = checked((uint)Parameters.Length);
            base.Write(w);

            switch (MotionType)
            {
                case MrfValueType.Literal:
                    {
                        MotionFilenameData ??= [];
                        MotionFilenameLength = checked((uint)MotionFilenameData.Length);
                        w.Write(MotionFilenameLength);
                        w.Write(MotionFilenameData);
                        break;
                    }
                case MrfValueType.Parameter:
                    w.Write(MotionParameterName);
                    break;
            }

            WriteFloat(w, RateType, Rate, RateParameterName);
            WriteFloat(w, PhaseType, Phase, PhaseParameterName);
            WriteFloat(w, DeltaType, Delta, DeltaParameterName);
            for (int i = 0; i < Parameters.Length; i++)
                WriteFloat(w, GetParameterType(i), Parameters[i].Value, Parameters[i].ParameterName);
        }

        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            var motionNode = node.SelectSingleNode("Motion");
            if (motionNode?.Attributes?["filename"] != null)
            {
                MotionType = MrfValueType.Literal;
                MotionFilename = Xml.GetStringAttribute(motionNode, "filename") ?? string.Empty;
            }
            else if (motionNode?.Attributes?["parameter"] != null)
            {
                MotionType = MrfValueType.Parameter;
                MotionParameterName = XmlMeta.GetHash(Xml.GetStringAttribute(motionNode, "parameter"));
            }
            else MotionType = MrfValueType.None;
            (RateType, Rate, RateParameterName) = XmlMrf.GetChildParameterizedFloat(node, "Rate");
            (PhaseType, Phase, PhaseParameterName) = XmlMrf.GetChildParameterizedFloat(node, "Phase");
            (DeltaType, Delta, DeltaParameterName) = XmlMrf.GetChildParameterizedFloat(node, "Delta");
            var items = node.SelectSingleNode("Parameters")?.SelectNodes("Item");
            if ((items?.Count ?? 0) > 6)
                throw new InvalidDataException("Parameterized-motion nodes support at most 6 parameters.");
            Parameters = new MrfNodePmParameter[items?.Count ?? 0];
            for (int i = 0; i < Parameters.Length; i++)
            {
                var parameter = XmlMrf.GetChildParameterizedFloat(items![i]!, "Value");
                SetParameterType(i, parameter.Type);
                Parameters[i] = new MrfNodePmParameter(parameter.Value, parameter.ParameterName);
            }
            ParameterCount = checked((uint)Parameters.Length);
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            MrfXml.ParameterizedFilenameTag(sb, indent, "Motion", MotionType, MotionFilename, MotionParameterName);
            MrfXml.ParameterizedFloatTag(sb, indent, "Rate", RateType, Rate, RateParameterName);
            MrfXml.ParameterizedFloatTag(sb, indent, "Phase", PhaseType, Phase, PhaseParameterName);
            MrfXml.ParameterizedFloatTag(sb, indent, "Delta", DeltaType, Delta, DeltaParameterName);
            MrfXml.OpenTag(sb, indent, "Parameters");
            for (int i = 0; i < Parameters.Length; i++)
            {
                MrfXml.OpenTag(sb, indent + 1, "Item");
                MrfXml.ParameterizedFloatTag(sb, indent + 2, "Value", GetParameterType(i), Parameters[i].Value, Parameters[i].ParameterName);
                MrfXml.CloseTag(sb, indent + 1, "Item");
            }
            MrfXml.CloseTag(sb, indent, "Parameters");
        }

        private static (float Value, MetaHash ParameterName) ReadFloat(DataReader r, MrfValueType type) =>
            type == MrfValueType.Literal ? (r.ReadSingle(), 0) : type == MrfValueType.Parameter ? (0, r.ReadUInt32()) : (0, 0);
        private static void WriteFloat(DataWriter w, MrfValueType type, float value, MetaHash parameterName)
        {
            if (type == MrfValueType.Literal) w.Write(value);
            else if (type == MrfValueType.Parameter) w.Write(parameterName);
        }
    }

    [TC(typeof(EXP))] public struct MrfNodePmParameter
    {
        public float Value { get; set; }
        public MetaHash ParameterName { get; set; }

        public MrfNodePmParameter(float value, MetaHash parameterName)
        {
            Value = value;
            ParameterName = parameterName;
        }
    }

    [TC(typeof(EXP))] public class MrfNodeExtrapolate : MrfNodeWithChildBase
    {
        // rage__mvNodeExtrapolate (18)

        public float Damping { get; set; }
        public MetaHash DampingParameterName { get; set; }

        // flags getters and setters
        public MrfValueType DampingType
        {
            get => (MrfValueType)GetFlagsSubset(0, 3);
            set => SetFlagsSubset(0, 3, (uint)value);
        }

        public MrfNodeExtrapolate() : base(MrfNodeType.Extrapolate) { }

        public override void Read(DataReader r)
        {
            base.Read(r);

            switch (DampingType)
            {
                case MrfValueType.Literal:
                    Damping = r.ReadSingle();
                    break;
                case MrfValueType.Parameter:
                    DampingParameterName = r.ReadUInt32();
                    break;
            }
        }

        public override void Write(DataWriter w)
        {
            base.Write(w);

            switch (DampingType)
            {
                case MrfValueType.Literal:
                    w.Write(Damping);
                    break;
                case MrfValueType.Parameter:
                    w.Write(DampingParameterName);
                    break;
            }
        }

        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);

            (DampingType, Damping, DampingParameterName) = XmlMrf.GetChildParameterizedFloat(node, "Damping");
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);

            MrfXml.ParameterizedFloatTag(sb, indent, "Damping", DampingType, Damping, DampingParameterName);
        }
    }

    [TC(typeof(EXP))] public class MrfNodeExpression : MrfNodeWithChildBase
    {
        // rage__mvNodeExpression (19)

        public MetaHash ExpressionDictionaryName { get; set; }
        public MetaHash ExpressionName { get; set; }
        public MetaHash ExpressionParameterName { get; set; }
        public float Weight { get; set; }
        public MetaHash WeightParameterName { get; set; }
        public MrfNodeExpressionVariable[] Variables { get; set; } = [];

        // flags getters and setters
        public MrfValueType ExpressionType
        {
            get => (MrfValueType)GetFlagsSubset(0, 3);
            set => SetFlagsSubset(0, 3, (uint)value);
        }
        public MrfValueType WeightType
        {
            get => (MrfValueType)GetFlagsSubset(2, 3);
            set => SetFlagsSubset(2, 3, (uint)value);
        }
        public uint VariableFlags
        {
            get => GetFlagsSubset(4, 0xFFFFFF);
            set => SetFlagsSubset(4, 0xFFFFFF, value);
        }
        public uint VariableCount
        {
            get => GetFlagsSubset(28, 0xF);
            set => SetFlagsSubset(28, 0xF, value);
        }

        // VariableFlags accessors by index
        public MrfValueType GetVariableType(int index)
        {
            if ((uint)index >= 12) throw new ArgumentOutOfRangeException(nameof(index));
            return (MrfValueType)GetFlagsSubset(4 + 2 * index, 3);
        }
        public void SetVariableType(int index, MrfValueType type)
        {
            if ((uint)index >= 12) throw new ArgumentOutOfRangeException(nameof(index));
            SetFlagsSubset(4 + 2 * index, 3, (uint)type);
        }

        public MrfNodeExpression() : base(MrfNodeType.Expression) { }

        public override void Read(DataReader r)
        {
            base.Read(r);

            switch (ExpressionType)
            {
                case MrfValueType.Literal:
                    ExpressionDictionaryName = r.ReadUInt32();
                    ExpressionName = r.ReadUInt32();
                    break;
                case MrfValueType.Parameter:
                    ExpressionParameterName = r.ReadUInt32();
                    break;
            }

            switch (WeightType)
            {
                case MrfValueType.Literal:
                    Weight = r.ReadSingle();
                    break;
                case MrfValueType.Parameter:
                    WeightParameterName = r.ReadUInt32();
                    break;
            }

            var varCount = VariableCount;
            if (varCount > 12)
                throw new InvalidDataException("Movement expression nodes support at most 12 variables.");

            if (varCount == 0)
                return;

            Variables = new MrfNodeExpressionVariable[varCount];

            for (int i = 0; i < varCount; i++)
            {
                var type = GetVariableType(i);
                var name = r.ReadUInt32();
                float value = 0.0f;
                uint valueParameterName = 0;

                switch (type)
                {
                    case MrfValueType.Literal:
                        value = r.ReadSingle();
                        break;
                    case MrfValueType.Parameter:
                        valueParameterName = r.ReadUInt32();
                        break;
                }

                Variables[i] = new MrfNodeExpressionVariable(name, value, valueParameterName);
            }
        }

        public override void Write(DataWriter w)
        {
            Variables ??= [];
            if (Variables.Length > 12)
                throw new InvalidDataException("Movement expression nodes support at most 12 variables.");
            VariableCount = checked((uint)Variables.Length);
            base.Write(w);

            switch (ExpressionType)
            {
                case MrfValueType.Literal:
                    w.Write(ExpressionDictionaryName);
                    w.Write(ExpressionName);
                    break;
                case MrfValueType.Parameter:
                    w.Write(ExpressionParameterName);
                    break;
            }

            switch (WeightType)
            {
                case MrfValueType.Literal:
                    w.Write(Weight);
                    break;
                case MrfValueType.Parameter:
                    w.Write(WeightParameterName);
                    break;
            }

            var varCount = VariableCount;

            if (varCount == 0)
                return;

            for (int i = 0; i < varCount; i++)
            {
                var type = GetVariableType(i);
                if (type > MrfValueType.Parameter)
                    throw new InvalidDataException($"Movement expression variable {i} contains an invalid value type.");
                var variable = Variables[i];
                w.Write(variable.Name);

                switch (type)
                {
                    case MrfValueType.Literal:
                        w.Write(variable.Value);
                        break;
                    case MrfValueType.Parameter:
                        w.Write(variable.ValueParameterName);
                        break;
                }
            }
        }

        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);

            (WeightType, Weight, WeightParameterName) = XmlMrf.GetChildParameterizedFloat(node, "Weight");
            (ExpressionType, ExpressionDictionaryName, ExpressionName, ExpressionParameterName) = XmlMrf.GetChildParameterizedAsset(node, "Expression");

            Variables = [];
            VariableFlags = 0;
            VariableCount = 0;
            var variablesNode = node.SelectSingleNode("Variables");
            if (variablesNode != null)
            {
                var inodes = variablesNode.SelectNodes("Item");
                if (inodes?.Count > 0)
                {
                    if (inodes.Count > 12)
                        throw new InvalidDataException("Movement expression nodes support at most 12 variables.");
                    VariableCount = (uint)inodes.Count;
                    Variables = new MrfNodeExpressionVariable[VariableCount];
                    int i = 0;
                    foreach (XmlNode inode in inodes)
                    {
                        var name = XmlMeta.GetHash(Xml.GetChildInnerText(inode, "Name"));
                        var value = XmlMrf.GetChildParameterizedFloat(inode, "Value");
                        Variables[i] = new MrfNodeExpressionVariable(name, value.Value, value.ParameterName);
                        SetVariableType(i, value.Type);
                        i++;
                    }
                }
            }
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);

            MrfXml.ParameterizedFloatTag(sb, indent, "Weight", WeightType, Weight, WeightParameterName);
            MrfXml.ParameterizedAssetTag(sb, indent, "Expression", ExpressionType, ExpressionDictionaryName, ExpressionName, ExpressionParameterName);
            if (Variables != null)
            {
                int cindent = indent + 1;
                int cindent2 = cindent + 1;
                int varIndex = 0;
                MrfXml.OpenTag(sb, indent, "Variables");
                foreach (var v in Variables)
                {
                    MrfXml.OpenTag(sb, cindent, "Item");
                    MrfXml.StringTag(sb, cindent2, "Name", MrfXml.HashString(v.Name));
                    MrfXml.ParameterizedFloatTag(sb, cindent2, "Value", GetVariableType(varIndex), v.Value, v.ValueParameterName);
                    MrfXml.CloseTag(sb, cindent, "Item");
                    varIndex++;
                }
                MrfXml.CloseTag(sb, indent, "Variables");
            }
            else
            {
                MrfXml.SelfClosingTag(sb, indent, "Variables");
            }
        }
    }

    [TC(typeof(EXP))] public struct MrfNodeExpressionVariable
    {
        public MetaHash Name { get; set; }
        public float Value { get; set; } // used if type == Literal
        public MetaHash ValueParameterName { get; set; } // used if type == Parameter

        public MrfNodeExpressionVariable(MetaHash name, float value, MetaHash valueParameterName)
        {
            Name = name;
            Value = value;
            ValueParameterName = valueParameterName;
        }
        
        public override string ToString()
        {
            return Name.ToString() + " - " + FloatUtil.ToString(Value) + " | " + ValueParameterName.ToString();
        }
    }

    [TC(typeof(EXP))] public class MrfNodeCapture : MrfNodeWithChildBase
    {
        // rage::mvNodeCaptureDef (20)

        public MetaHash FrameParameterName { get; set; }
        public bool Owner { get; set; }
        public MetaHash OwnerParameterName { get; set; }

        // flags getters and setters
        public MrfValueType FrameType // only Parameter type is supported
        {
            get => (MrfValueType)GetFlagsSubset(0, 3);
            set => SetFlagsSubset(0, 3, (uint)value);
        }
        public MrfValueType OwnerType
        {
            get => (MrfValueType)GetFlagsSubset(4, 3);
            set => SetFlagsSubset(4, 3, (uint)value);
        }

        public MrfNodeCapture() : base(MrfNodeType.Capture) { }

        public override void Read(DataReader r)
        {
            base.Read(r);

            if (FrameType != MrfValueType.None)
                FrameParameterName = r.ReadUInt32();

            switch (OwnerType)
            {
                case MrfValueType.Literal: Owner = r.ReadUInt32() != 0; break;
                case MrfValueType.Parameter: OwnerParameterName = r.ReadUInt32(); break;
            }
        }

        public override void Write(DataWriter w)
        {
            base.Write(w);

            if (FrameType != MrfValueType.None)
                w.Write(FrameParameterName);

            switch (OwnerType)
            {
                case MrfValueType.Literal: w.Write(Owner ? 0x01000000u : 0u); break;
                case MrfValueType.Parameter: w.Write(OwnerParameterName); break;
            }
        }

        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);

            (FrameType, _, _, FrameParameterName) = XmlMrf.GetChildParameterizedAsset(node, "Frame");
            (OwnerType, Owner, OwnerParameterName) = XmlMrf.GetChildParameterizedBool(node, "Owner");
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);

            MrfXml.ParameterizedAssetTag(sb, indent, "Frame", FrameType, 0, 0, FrameParameterName);
            MrfXml.ParameterizedBoolTag(sb, indent, "Owner", OwnerType, Owner, OwnerParameterName);
        }
    }

    [TC(typeof(EXP))] public class MrfNodeProxy : MrfNode
    {
        // rage__mvNodeProxy (21)

        public MetaHash NodeParameterName { get; set; } // lookups a rage::crmtObserver parameter, then gets the observed node

        public MrfNodeProxy() : base(MrfNodeType.Proxy) { }

        public override void Read(DataReader r)
        {
            base.Read(r);
            NodeParameterName = r.ReadUInt32();
        }

        public override void Write(DataWriter w)
        {
            base.Write(w);
            w.Write(NodeParameterName);
        }

        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);

            NodeParameterName = XmlMeta.GetHash(Xml.GetChildInnerText(node, "NodeParameterName"));
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);

            MrfXml.StringTag(sb, indent, "NodeParameterName", MrfXml.HashString(NodeParameterName));
        }

        public override string ToString()
        {
            return base.ToString() + " - " + NodeParameterName.ToString();
        }
    }

    [TC(typeof(EXP))] public class MrfNodeAddN : MrfNodeNBase
    {
        // rage__mvNodeAddN (22)

        public MrfNodeAddN() : base(MrfNodeType.AddN) { }
    }

    [TC(typeof(EXP))] public class MrfNodeIdentity : MrfNode
    {
        // rage__mvNodeIdentity (23)

        public MrfNodeIdentity() : base(MrfNodeType.Identity) { }
    }

    [TC(typeof(EXP))] public class MrfNodeMerge : MrfNodePairBase
    {
        // rage::mvNodeMergeDef (24)

        public MrfSynchronizerTagFlags SynchronizerTag { get; set; }
        public MetaHash FilterDictionaryName { get; set; }
        public MetaHash FilterName { get; set; }
        public MetaHash FilterParameterName { get; set; }

        // flags getters and setters
        public MrfValueType FilterType
        {
            get => (MrfValueType)GetFlagsSubset(0, 3);
            set => SetFlagsSubset(0, 3, (uint)value);
        }
        public MrfInfluenceOverride Source0InfluenceOverride
        {
            get => (MrfInfluenceOverride)GetFlagsSubset(2, 3);
            set => SetFlagsSubset(2, 3, (uint)value);
        }
        public MrfInfluenceOverride Source1InfluenceOverride
        {
            get => (MrfInfluenceOverride)GetFlagsSubset(4, 3);
            set => SetFlagsSubset(4, 3, (uint)value);
        }
        public bool Transitional
        {
            get => GetFlagsSubset(6, 1) != 0;
            set => SetFlagsSubset(6, 1, value ? 1 : 0u);
        }
        public bool Source0Immutable
        {
            get => GetFlagsSubset(7, 1) != 0;
            set => SetFlagsSubset(7, 1, value ? 1u : 0u);
        }
        public bool Source1Immutable
        {
            get => GetFlagsSubset(8, 1) != 0;
            set => SetFlagsSubset(8, 1, value ? 1u : 0u);
        }
        public MrfSynchronizerType SynchronizerType
        {
            get => (MrfSynchronizerType)GetFlagsSubset(19, 3);
            set => SetFlagsSubset(19, 3, (uint)value);
        }
        public MrfNodeMerge() : base(MrfNodeType.Merge) { }

        public override void Read(DataReader r)
        {
            base.Read(r);

            if (SynchronizerType == MrfSynchronizerType.Tag)
                SynchronizerTag = (MrfSynchronizerTagFlags)r.ReadUInt32();

            switch (FilterType)
            {
                case MrfValueType.Literal:
                    FilterDictionaryName = r.ReadUInt32();
                    FilterName = r.ReadUInt32();
                    break;
                case MrfValueType.Parameter:
                    FilterParameterName = r.ReadUInt32();
                    break;
            }
        }

        public override void Write(DataWriter w)
        {
            base.Write(w);

            if (SynchronizerType == MrfSynchronizerType.Tag)
                w.Write((uint)SynchronizerTag);

            switch (FilterType)
            {
                case MrfValueType.Literal:
                    w.Write(FilterDictionaryName);
                    w.Write(FilterName);
                    break;
                case MrfValueType.Parameter:
                    w.Write(FilterParameterName);
                    break;
            }
        }

        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);

            Source0InfluenceOverride = Xml.GetChildEnumInnerText<MrfInfluenceOverride>(node, "Child0InfluenceOverride");
            Source1InfluenceOverride = Xml.GetChildEnumInnerText<MrfInfluenceOverride>(node, "Child1InfluenceOverride");
            (FilterType, FilterDictionaryName, FilterName, FilterParameterName) = XmlMrf.GetChildParameterizedAsset(node, "FrameFilter");
            SynchronizerType = Xml.GetChildEnumInnerText<MrfSynchronizerType>(node, "SynchronizerType");
            if (SynchronizerType == MrfSynchronizerType.Tag)
            {
                SynchronizerTag = Xml.GetChildEnumInnerText<MrfSynchronizerTagFlags>(node, "SynchronizerTagFlags");
            }
            Transitional = Xml.GetChildBoolAttribute(node, "Transitional") || Xml.GetChildBoolAttribute(node, "UnkFlag6");
            uint legacyImmutable = Xml.GetChildUIntAttribute(node, "UnkFlag7");
            Source0Immutable = Xml.GetChildBoolAttribute(node, "Source0Immutable") || (legacyImmutable & 1) != 0;
            Source1Immutable = Xml.GetChildBoolAttribute(node, "Source1Immutable") || (legacyImmutable & 2) != 0;
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);

            MrfXml.StringTag(sb, indent, "Child0InfluenceOverride", Source0InfluenceOverride.ToString());
            MrfXml.StringTag(sb, indent, "Child1InfluenceOverride", Source1InfluenceOverride.ToString());
            MrfXml.ParameterizedAssetTag(sb, indent, "FrameFilter", FilterType, FilterDictionaryName, FilterName, FilterParameterName);
            MrfXml.StringTag(sb, indent, "SynchronizerType", SynchronizerType.ToString());
            if (SynchronizerType == MrfSynchronizerType.Tag)
            {
                MrfXml.StringTag(sb, indent, "SynchronizerTagFlags", SynchronizerTag.ToString());
            }
            MrfXml.ValueTag(sb, indent, "Transitional", Transitional.ToString());
            MrfXml.ValueTag(sb, indent, "Source0Immutable", Source0Immutable.ToString());
            MrfXml.ValueTag(sb, indent, "Source1Immutable", Source1Immutable.ToString());
        }
    }

    [TC(typeof(EXP))] public class MrfNodePose : MrfNodeWithFlagsBase
    {
        // rage__mvNodePose (25)

        public bool Normalize { get; set; } = true;
        public MetaHash NormalizeParameterName { get; set; }

        // flags getters and setters
        public MrfValueType NormalizeType
        {
            get => (MrfValueType)GetFlagsSubset(0, 3);
            set => SetFlagsSubset(0, 3, (uint)value);
        }

        public MrfNodePose() : base(MrfNodeType.Pose) { }

        public override void Read(DataReader r)
        {
            base.Read(r);

            switch (NormalizeType)
            {
                case MrfValueType.Literal: Normalize = r.ReadUInt32() != 0; break;
                case MrfValueType.Parameter: NormalizeParameterName = r.ReadUInt32(); break;
            }
        }

        public override void Write(DataWriter w)
        {
            base.Write(w);

            switch (NormalizeType)
            {
                case MrfValueType.Literal: w.Write(Normalize ? 0x01000000u : 0u); break;
                case MrfValueType.Parameter: w.Write(NormalizeParameterName); break;
            }
        }

        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            (NormalizeType, Normalize, NormalizeParameterName) = XmlMrf.GetChildParameterizedBool(node, "Normalize");
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            MrfXml.ParameterizedBoolTag(sb, indent, "Normalize", NormalizeType, Normalize, NormalizeParameterName);
        }
    }

    [TC(typeof(EXP))] public class MrfNodeMergeN : MrfNodeNBase
    {
        // rage__mvNodeMergeN (26)

        public MrfNodeMergeN() : base(MrfNodeType.MergeN) { }
    }

    [TC(typeof(EXP))] public class MrfNodeState : MrfNodeStateBase
    {
        // rage__mvNodeState (27)

        public int InputParametersOffset { get; set; }
        public int InputParametersFileOffset { get; set; }
        public uint InputParameterCount { get; set; }
        public int EventsOffset { get; set; }
        public int EventsFileOffset { get; set; }
        public uint EventCount { get; set; }
        public int OutputParametersOffset { get; set; }
        public int OutputParametersFileOffset { get; set; }
        public uint OutputParameterCount { get; set; }
        public int OperationsOffset { get; set; }
        public int OperationsFileOffset { get; set; }
        public uint OperationCount { get; set; }

        public MrfStateInputParameter[] InputParameters { get; set; } = [];
        public MrfStateEvent[] Events { get; set; } = [];
        public MrfStateOutputParameter[] OutputParameters { get; set; } = [];
        public MrfStateOperation[] Operations { get; set; } = [];

        public MrfNodeState() : base(MrfNodeType.State) { }

        public override void Read(DataReader r)
        {
            base.Read(r);

            InputParametersOffset = r.ReadInt32();
            InputParametersFileOffset = checked((int)(r.Position + InputParametersOffset - 4));
            InputParameterCount = r.ReadUInt32();
            EventsOffset = r.ReadInt32();
            EventsFileOffset = checked((int)(r.Position + EventsOffset - 4));
            EventCount = r.ReadUInt32();
            OutputParametersOffset = r.ReadInt32();
            OutputParametersFileOffset = checked((int)(r.Position + OutputParametersOffset - 4));
            OutputParameterCount = r.ReadUInt32();
            OperationsOffset = r.ReadInt32();
            OperationsFileOffset = checked((int)(r.Position + OperationsOffset - 4));
            OperationCount = r.ReadUInt32();


            if (TransitionCount > 0)
            {
                if (r.Position != TransitionsFileOffset)
                    throw new InvalidDataException($"Movement transition table begins at {TransitionsFileOffset}, but the reader is at {r.Position}.");

                Transitions = new MrfStateTransition[TransitionCount];
                for (int i = 0; i < TransitionCount; i++)
                    Transitions[i] = new MrfStateTransition(r);
            }

            if (InputParameterCount > 0)
            {
                if (r.Position != InputParametersFileOffset)
                    throw new InvalidDataException($"Movement input table begins at {InputParametersFileOffset}, but the reader is at {r.Position}.");

                InputParameters = new MrfStateInputParameter[InputParameterCount];
                for (int i = 0; i < InputParameterCount; i++)
                    InputParameters[i] = new MrfStateInputParameter(r);
            }

            if (EventCount > 0)
            {
                if (r.Position != EventsFileOffset)
                    throw new InvalidDataException($"Movement event table begins at {EventsFileOffset}, but the reader is at {r.Position}.");

                Events = new MrfStateEvent[EventCount];
                for (int i = 0; i < EventCount; i++)
                    Events[i] = new MrfStateEvent(r);
            }

            if (OutputParameterCount > 0)
            {
                if (r.Position != OutputParametersFileOffset)
                    throw new InvalidDataException($"Movement output table begins at {OutputParametersFileOffset}, but the reader is at {r.Position}.");

                OutputParameters = new MrfStateOutputParameter[OutputParameterCount];
                for (int i = 0; i < OutputParameterCount; i++)
                    OutputParameters[i] = new MrfStateOutputParameter(r);
            }

            if (OperationCount > 0)
            {
                if (r.Position != OperationsFileOffset)
                    throw new InvalidDataException($"Movement operation table begins at {OperationsFileOffset}, but the reader is at {r.Position}.");

                Operations = new MrfStateOperation[OperationCount];
                for (int i = 0; i < OperationCount; i++)
                    Operations[i] = new MrfStateOperation(r);
            }
        }

        public override void Write(DataWriter w)
        {
            TransitionCount = checked((byte)(Transitions?.Length ?? 0));
            InputParameterCount = checked((uint)(InputParameters?.Length ?? 0));
            EventCount = checked((uint)(Events?.Length ?? 0));
            OutputParameterCount = checked((uint)(OutputParameters?.Length ?? 0));
            OperationCount = checked((uint)(Operations?.Length ?? 0));

            base.Write(w);

            w.Write(InputParametersOffset);
            w.Write(InputParameterCount);
            w.Write(EventsOffset);
            w.Write(EventCount);
            w.Write(OutputParametersOffset);
            w.Write(OutputParameterCount);
            w.Write(OperationsOffset);
            w.Write(OperationCount);

            if (Transitions != null)
                foreach (var transition in Transitions)
                    transition.Write(w);

            if (InputParameters != null)
                foreach (var item in InputParameters)
                    item.Write(w);

            if (Events != null)
                foreach (var item in Events)
                    item.Write(w);

            if (OutputParameters != null)
                foreach (var item in OutputParameters)
                    item.Write(w);

            if (Operations != null)
                foreach (var item in Operations)
                    item.Write(w);
        }

        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            
            InitialNode = XmlMrf.ReadChildNode(node, "InitialNode");
            Transitions = XmlMeta.ReadItemArray<MrfStateTransition>(node, "Transitions");
            InputParameters = XmlMeta.ReadItemArray<MrfStateInputParameter>(node, "InputParameters");
            OutputParameters = XmlMeta.ReadItemArray<MrfStateOutputParameter>(node, "OutputParameters");
            Events = XmlMeta.ReadItemArray<MrfStateEvent>(node, "Events");
            Operations = XmlMeta.ReadItemArray<MrfStateOperation>(node, "Operations");
            TransitionCount = checked((byte)(Transitions?.Length ?? 0));
            InputParameterCount = checked((uint)(InputParameters?.Length ?? 0));
            OutputParameterCount = checked((uint)(OutputParameters?.Length ?? 0));
            EventCount = checked((uint)(Events?.Length ?? 0));
            OperationCount = checked((uint)(Operations?.Length ?? 0));
            ChildCount = checked((byte)GetChildren(excludeTailNodes: true).Count);
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            MrfXml.WriteNode(sb, indent, "InitialNode", InitialNode);
            MrfXml.WriteItemArray(sb, Transitions, indent, "Transitions");
            MrfXml.WriteItemArray(sb, InputParameters, indent, "InputParameters");
            MrfXml.WriteItemArray(sb, OutputParameters, indent, "OutputParameters");
            MrfXml.WriteItemArray(sb, Events, indent, "Events");
            MrfXml.WriteItemArray(sb, Operations, indent, "Operations");
        }

        public override void ResolveRelativeOffsets(MrfFile mrf)
        {
            base.ResolveRelativeOffsets(mrf);

            ResolveNodeOffsetsInTransitions(Transitions, mrf);
        }

        public override void UpdateRelativeOffsets()
        {
            base.UpdateRelativeOffsets();

            var offset = FileOffset + 0x20/*sizeof(MrfNodeStateBase)*/ + 8*4/*all offsets/counts*/;

            offset = UpdateNodeOffsetsInTransitions(Transitions, offset, offsetSetToZeroIfNoTransitions: false);

            InputParametersFileOffset = offset;
            InputParametersOffset = InputParametersFileOffset - (FileOffset + 0x20 + 0);
            offset += (int)InputParameterCount * 0xC;

            EventsFileOffset = offset;
            EventsOffset = EventsFileOffset - (FileOffset + 0x20 + 8);
            offset += (int)EventCount * 0x8;

            OutputParametersFileOffset = offset;
            OutputParametersOffset = OutputParametersFileOffset - (FileOffset + 0x20 + 0x10);
            offset += (int)OutputParameterCount * 0xC;

            OperationsFileOffset = offset;
            OperationsOffset = OperationsFileOffset - (FileOffset + 0x20 + 0x18);
        }

        public List<MrfNode> GetChildren(bool excludeTailNodes)
        {
            var result = new List<MrfNode>();
            if (InitialNode == null) return result;

            var q = new Queue<MrfNode>();
            q.Enqueue(InitialNode);
            while (q.Count > 0)
            {
                var n = q.Dequeue();
                if (!excludeTailNodes || !(n is MrfNodeTail))
                {
                    result.Add(n);
                }

                if (n is MrfNodeWithChildBase nc)
                {
                    if (nc.Input != null) q.Enqueue(nc.Input);
                }
                else if (n is MrfNodePairBase np)
                {
                    if (np.Input0 != null) q.Enqueue(np.Input0);
                    if (np.Input1 != null) q.Enqueue(np.Input1);
                }
                else if (n is MrfNodeNBase nn)
                {
                    foreach (var c in nn.Children)
                    {
                        q.Enqueue(c);
                    }
                }
                else if (n is MrfNodeInlinedStateMachine ism)
                {
                    if (ism.FallbackNode != null) q.Enqueue(ism.FallbackNode);
                }
            }

            return result;
        }
    }

    [TC(typeof(EXP))] public class MrfNodeInvalid : MrfNode
    {
        // rage__mvNodeInvalid (28)

        public MrfNodeInvalid() : base(MrfNodeType.Invalid) { }
    }

    [TC(typeof(EXP))] public class MrfNodeJointLimit : MrfNodeWithChildAndFilterBase
    {
        // rage__mvNodeJointLimit (29)

        public MrfNodeJointLimit() : base(MrfNodeType.JointLimit) { }
    }

    [TC(typeof(EXP))] public class MrfNodeSubNetwork : MrfNode
    {
        // rage__mvNodeSubNetworkClass (30)

        public MetaHash SubNetworkParameterName { get; set; } // parameter of type rage::mvSubNetwork to lookup

        public MrfNodeSubNetwork() : base(MrfNodeType.SubNetwork) { }

        public override void Read(DataReader r)
        {
            base.Read(r);
            SubNetworkParameterName = r.ReadUInt32();
        }

        public override void Write(DataWriter w)
        {
            base.Write(w);
            w.Write(SubNetworkParameterName);
        }

        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);
            SubNetworkParameterName = XmlMeta.GetHash(Xml.GetChildInnerText(node, "SubNetworkParameterName"));
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);
            MrfXml.StringTag(sb, indent, "SubNetworkParameterName", MrfXml.HashString(SubNetworkParameterName));
        }
    }

    [TC(typeof(EXP))] public class MrfNodeReference : MrfNode
    {
        // rage__mvNodeReference (31)

        // Unused in the final game but from testing, seems to work fine initially but when it finishes it crashes calling a pure virtual function rage::crmtNode::GetNodeTypeInfo
        // Maybe some kind of double-free/use-after-free bug, not sure if a R* bug or an issue with the generated MRF file.

        public MetaHash NetworkId { get; set; }
        public int SourceParameterDataOffset { get; set; }
        public int SourceParameterDataFileOffset { get; set; }
        public uint SourceParameterCount { get; set; }
        public MrfNodeReferenceParameterSourceData[] SourceParameters { get; set; } = [];
        public uint ParameterCount { get; set; }
        public MrfNodeReferenceNamePair[] Parameters { get; set; } = [];
        public uint FlagCount { get; set; }
        public MrfNodeReferenceNamePair[] Flags { get; set; } = [];
        public uint RequestCount { get; set; }
        public MrfNodeReferenceNamePair[] Requests { get; set; } = [];

        public MrfNodeReference() : base(MrfNodeType.Reference) { }

        public override void Read(DataReader r)
        {
            base.Read(r);

            NetworkId = r.ReadUInt32();
            SourceParameterDataOffset = r.ReadInt32();
            SourceParameterDataFileOffset = checked((int)(r.Position + SourceParameterDataOffset - 4));
            SourceParameterCount = r.ReadUInt32();
            ParameterCount = r.ReadUInt32();
            FlagCount = r.ReadUInt32();
            RequestCount = r.ReadUInt32();

            if (ParameterCount > 0)
            {
                Parameters = new MrfNodeReferenceNamePair[ParameterCount];

                for (int i = 0; i < ParameterCount; i++)
                {
                    var name = r.ReadUInt32();
                    var newName = r.ReadUInt32();
                    Parameters[i] = new MrfNodeReferenceNamePair(name, newName);
                }
            }

            if (FlagCount > 0)
            {
                Flags = new MrfNodeReferenceNamePair[FlagCount];

                for (int i = 0; i < FlagCount; i++)
                {
                    var name = r.ReadUInt32();
                    var newName = r.ReadUInt32();
                    Flags[i] = new MrfNodeReferenceNamePair(name, newName);
                }
            }

            if (RequestCount > 0)
            {
                Requests = new MrfNodeReferenceNamePair[RequestCount];

                for (int i = 0; i < RequestCount; i++)
                {
                    var name = r.ReadUInt32();
                    var newName = r.ReadUInt32();
                    Requests[i] = new MrfNodeReferenceNamePair(name, newName);
                }
            }

            if (SourceParameterCount > 0)
            {
                if (r.Position != SourceParameterDataFileOffset)
                    throw new InvalidDataException($"Movement reference parameter-source table begins at {SourceParameterDataFileOffset}, but the reader is at {r.Position}.");

                SourceParameters = new MrfNodeReferenceParameterSourceData[SourceParameterCount];

                for (int i = 0; i < SourceParameterCount; i++)
                {
                    var type = r.ReadUInt32();
                    var name = r.ReadUInt32();
                    var data = r.ReadInt32();
                    SourceParameters[i] = new MrfNodeReferenceParameterSourceData((MrfSignalType)type, name, data);
                }
            }
        }

        public override void Write(DataWriter w)
        {
            base.Write(w);

            SourceParameters ??= [];
            Parameters ??= [];
            Flags ??= [];
            Requests ??= [];
            SourceParameterCount = checked((uint)SourceParameters.Length);
            ParameterCount = checked((uint)Parameters.Length);
            FlagCount = checked((uint)Flags.Length);
            RequestCount = checked((uint)Requests.Length);

            w.Write(NetworkId);
            w.Write(SourceParameterDataOffset);
            w.Write(SourceParameterCount);
            w.Write(ParameterCount);
            w.Write(FlagCount);
            w.Write(RequestCount);

            if (ParameterCount > 0)
            {
                foreach (var entry in Parameters)
                {
                    w.Write(entry.Name);
                    w.Write(entry.NewName);
                }
            }

            if (FlagCount > 0)
            {
                foreach (var entry in Flags)
                {
                    w.Write(entry.Name);
                    w.Write(entry.NewName);
                }
            }

            if (RequestCount > 0)
            {
                foreach (var entry in Requests)
                {
                    w.Write(entry.Name);
                    w.Write(entry.NewName);
                }
            }

            if (SourceParameterCount > 0)
            {
                foreach (var entry in SourceParameters)
                {
                    w.Write((uint)entry.Type);
                    w.Write(entry.Key);
                    w.Write(entry.Payload);
                }
            }
        }

        public override void ReadXml(XmlNode node)
        {
            base.ReadXml(node);

            NetworkId = XmlMeta.GetHash(Xml.GetChildInnerText(node, "NetworkId"));
            if (NetworkId == 0) NetworkId = XmlMeta.GetHash(Xml.GetChildInnerText(node, "MoveNetworkName"));
            Parameters = XmlMeta.ReadItemArray<MrfNodeReferenceNamePair>(node, "Parameters");
            if (Parameters.Length == 0) Parameters = XmlMeta.ReadItemArray<MrfNodeReferenceNamePair>(node, "ImportedParameters");
            Flags = XmlMeta.ReadItemArray<MrfNodeReferenceNamePair>(node, "Flags");
            if (Flags.Length == 0) Flags = XmlMeta.ReadItemArray<MrfNodeReferenceNamePair>(node, "MoveNetworkFlags");
            Requests = XmlMeta.ReadItemArray<MrfNodeReferenceNamePair>(node, "Requests");
            if (Requests.Length == 0) Requests = XmlMeta.ReadItemArray<MrfNodeReferenceNamePair>(node, "MoveNetworkTriggers");
            SourceParameters = XmlMeta.ReadItemArray<MrfNodeReferenceParameterSourceData>(node, "SourceParameters");
            if (SourceParameters.Length == 0) SourceParameters = XmlMeta.ReadItemArray<MrfNodeReferenceParameterSourceData>(node, "InitialParameters");
            ParameterCount = checked((uint)Parameters.Length);
            FlagCount = checked((uint)Flags.Length);
            RequestCount = checked((uint)Requests.Length);
            SourceParameterCount = checked((uint)SourceParameters.Length);
        }

        public override void WriteXml(StringBuilder sb, int indent)
        {
            base.WriteXml(sb, indent);

            MrfXml.StringTag(sb, indent, "NetworkId", MrfXml.HashString(NetworkId));
            MrfXml.WriteItemArray(sb, Parameters, indent, "Parameters");
            MrfXml.WriteItemArray(sb, Flags, indent, "Flags");
            MrfXml.WriteItemArray(sb, Requests, indent, "Requests");
            MrfXml.WriteItemArray(sb, SourceParameters, indent, "SourceParameters");
        }

        public override void UpdateRelativeOffsets()
        {
            base.UpdateRelativeOffsets();

            var offset = FileOffset + 0x20;
            offset += checked((int)ParameterCount) * 8;
            offset += checked((int)FlagCount) * 8;
            offset += checked((int)RequestCount) * 8;
            SourceParameterDataFileOffset = offset;
            SourceParameterDataOffset = SourceParameterDataFileOffset - (FileOffset + 0xC);
        }
    }

    [TC(typeof(EXP))] public struct MrfNodeReferenceNamePair : IMetaXmlItem
    {
        public MetaHash Name { get; set; } // name in the parent network
        public MetaHash NewName { get; set; } // name in the new network

        public MrfNodeReferenceNamePair(MetaHash name, MetaHash newName)
        {
            Name = name;
            NewName = newName;
        }

        public void ReadXml(XmlNode node)
        {
            Name = XmlMeta.GetHash(Xml.GetChildInnerText(node, "Name"));
            NewName = XmlMeta.GetHash(Xml.GetChildInnerText(node, "NewName"));
        }

        public void WriteXml(StringBuilder sb, int indent)
        {
            MrfXml.StringTag(sb, indent, "Name", MrfXml.HashString(Name));
            MrfXml.StringTag(sb, indent, "NewName", MrfXml.HashString(NewName));
        }

        public override string ToString()
        {
            return $"{Name} - {NewName}";
        }
    }

    public enum MrfSignalType : uint
    {
        Animation = 0,
        Clip = 1,
        Expression = 2,
        Filter = 3,
        FilterN = 4,
        Frame = 5,
        ParameterizedMotion = 6,
        Real = 7,
        Boolean = 8,
        Data = 9,
        Request = 10,
        Flag = 11,
        Network = 12,
    }

    [TC(typeof(EXP))] public struct MrfNodeReferenceParameterSourceData : IMetaXmlItem
    {
        public MrfSignalType Type { get; set; }
        public MetaHash Key { get; set; }
        public int Payload { get; set; }
        public float RealValue
        {
            get => BitConverter.Int32BitsToSingle(Payload);
            set => Payload = BitConverter.SingleToInt32Bits(value);
        }

        public MrfNodeReferenceParameterSourceData(MrfSignalType type, MetaHash key, int payload)
        {
            Type = type;
            Key = key;
            Payload = payload;
        }

        public void ReadXml(XmlNode node)
        {
            var typeNode = node.SelectSingleNode("Type");
            Type = !string.IsNullOrWhiteSpace(typeNode?.InnerText)
                ? Xml.GetEnumValue<MrfSignalType>(typeNode.InnerText)
                : (MrfSignalType)Xml.GetChildUIntAttribute(node, "Type");
            Key = XmlMeta.GetHash(Xml.GetChildInnerText(node, "Key"));
            if (Key == 0) Key = XmlMeta.GetHash(Xml.GetChildInnerText(node, "Name"));
            if (Type == MrfSignalType.Real && node.SelectSingleNode("RealValue") != null)
                RealValue = Xml.GetChildFloatAttribute(node, "RealValue");
            else
            {
                Payload = Xml.GetChildIntAttribute(node, "Payload");
                if (node.SelectSingleNode("Payload") == null) Payload = Xml.GetChildIntAttribute(node, "Data");
            }
        }

        public void WriteXml(StringBuilder sb, int indent)
        {
            MrfXml.StringTag(sb, indent, "Type", Type.ToString());
            MrfXml.StringTag(sb, indent, "Key", MrfXml.HashString(Key));
            if (Type == MrfSignalType.Real)
                MrfXml.ValueTag(sb, indent, "RealValue", FloatUtil.ToString(RealValue));
            else
                MrfXml.ValueTag(sb, indent, "Payload", Payload.ToString());
        }

        public override string ToString()
        {
            return $"{Type} - {Key} - {(Type == MrfSignalType.Real ? FloatUtil.ToString(RealValue) : Payload)}";
        }
    }

#endregion



    public class MrfXml : MetaXmlBase
    {
        public static string GetXml(MrfFile mrf)
        {
            StringBuilder sb = new();
            sb.AppendLine(XmlHeader);

            if (mrf != null)
            {
                var name = "MoveNetwork";

                OpenTag(sb, 0, name);

                mrf.WriteXml(sb, 1);

                CloseTag(sb, 0, name);
            }

            return sb.ToString();
        }

        public static void WriteNode(StringBuilder sb, int indent, string name, MrfNode? node)
        {
            if (node == null) return;
            OpenTag(sb, indent, name + " type=\"" + node.Type + "\"");
            node.WriteXml(sb, indent + 1);
            CloseTag(sb, indent, name);
        }

        public static void WriteNodeRef(StringBuilder sb, int indent, string name, MrfNode? node)
        {
            if (node == null) return;
            Indent(sb, indent);
            sb.Append("<");
            sb.Append(name);
            sb.Append(" ref=\"");
            sb.Append(HashString(node.ID));
            sb.Append("\" />");
            sb.AppendLine();
        }
        public static void WriteCondition(StringBuilder sb, int indent, string name, MrfCondition condition)
        {
            OpenTag(sb, indent, name + " type=\"" + condition.Type + "\"");
            condition.WriteXml(sb, indent + 1);
            CloseTag(sb, indent, name);
        }
        public static void WriteOperator(StringBuilder sb, int indent, string name, MrfStateOperator op)
        {
            OpenTag(sb, indent, name + " type=\"" + op.Type + "\"");
            op.WriteXml(sb, indent + 1);
            CloseTag(sb, indent, name);
        }

        public static void ParameterizedFloatTag(StringBuilder sb, int indent, string name, MrfValueType type, float value, MetaHash parameter)
        {
            switch (type)
            {
                case MrfValueType.None: SelfClosingTag(sb, indent, name); break;
                case MrfValueType.Literal: ValueTag(sb, indent, name, FloatUtil.ToString(value), "value"); break;
                case MrfValueType.Parameter: ValueTag(sb, indent, name, HashString(parameter), "parameter"); break;
            }
        }

        public static void ParameterizedBoolTag(StringBuilder sb, int indent, string name, MrfValueType type, bool value, MetaHash parameter)
        {
            switch (type)
            {
                case MrfValueType.None: SelfClosingTag(sb, indent, name); break;
                case MrfValueType.Literal: ValueTag(sb, indent, name, value.ToString(), "value"); break;
                case MrfValueType.Parameter: ValueTag(sb, indent, name, HashString(parameter), "parameter"); break;
            }
        }

        public static void ParameterizedFilenameTag(StringBuilder sb, int indent, string name, MrfValueType type, string filename, MetaHash parameter)
        {
            switch (type)
            {
                case MrfValueType.None: SelfClosingTag(sb, indent, name); break;
                case MrfValueType.Literal: ValueTag(sb, indent, name, filename, "filename"); break;
                case MrfValueType.Parameter: ValueTag(sb, indent, name, HashString(parameter), "parameter"); break;
            }
        }

        public static void ParameterizedAssetTag(StringBuilder sb, int indent, string name, MrfValueType type, MetaHash dictionaryName, MetaHash assetName, MetaHash parameter)
        {
            switch (type)
            {
                case MrfValueType.None: SelfClosingTag(sb, indent, name); break;
                case MrfValueType.Literal:
                    OpenTag(sb, indent, name);
                    StringTag(sb, indent + 1, "DictionaryName", HashString(dictionaryName));
                    StringTag(sb, indent + 1, "Name", HashString(assetName));
                    CloseTag(sb, indent, name);
                    break;
                case MrfValueType.Parameter: ValueTag(sb, indent, name, HashString(parameter), "parameter"); break;
            }
        }

        public static void ParameterizedClipTag(StringBuilder sb, int indent, string name, MrfValueType type, MrfClipContainerType containerType, MetaHash containerName, MetaHash clipName, MetaHash parameter)
        {
            switch (type)
            {
                case MrfValueType.None: SelfClosingTag(sb, indent, name); break;
                case MrfValueType.Literal:
                    OpenTag(sb, indent, name);
                    StringTag(sb, indent + 1, "ContainerType", containerType.ToString());
                    if (containerType != MrfClipContainerType.LocalFile)
                        StringTag(sb, indent + 1, "ContainerName", HashString(containerName));
                    StringTag(sb, indent + 1, "Name", HashString(clipName));
                    CloseTag(sb, indent, name);
                    break;
                case MrfValueType.Parameter: ValueTag(sb, indent, name, HashString(parameter), "parameter"); break;
            }
        }
    }

    public class XmlMrf
    {
        public static MrfFile GetMrf(string xml)
        {
            XmlDocument doc = new();
            doc.LoadXml(xml);
            return GetMrf(doc);
        }

        public static MrfFile GetMrf(XmlDocument doc)
        {
            MrfFile mrf = new();
            mrf.ReadXml(doc.DocumentElement ?? throw new InvalidDataException("The movement XML has no root element."));
            return mrf;
        }

        public static MrfNode? ReadChildNode(XmlNode node, string name)
        {
            return ReadNode(node.SelectSingleNode(name));
        }
        public static MrfNode? ReadNode(XmlNode? node)
        {
            if (node != null && Enum.TryParse<MrfNodeType>(Xml.GetStringAttribute(node, "type"), out var type))
            {
                var n = MrfFile.CreateNode(type);
                n.ReadXml(node);
                return n;
            }

            return null;
        }
        public static MetaHash ReadChildNodeRef(XmlNode node, string name)
        {
            return ReadNodeRef(node.SelectSingleNode(name));
        }
        public static MetaHash ReadNodeRef(XmlNode? node)
        {
            var name = XmlMeta.GetHash(Xml.GetStringAttribute(node, "ref"));
            return name;
        }
        public static MrfCondition? ReadCondition(XmlNode? node)
        {
            if (node != null && Enum.TryParse<MrfConditionType>(Xml.GetStringAttribute(node, "type"), out var type))
            {
                var n = MrfCondition.CreateCondition(type);
                n.ReadXml(node);
                return n;
            }

            return null;
        }
        public static MrfStateOperator? ReadOperator(XmlNode? node)
        {
            if (node != null && Enum.TryParse<MrfOperatorType>(Xml.GetStringAttribute(node, "type"), out var type))
            {
                var op = MrfStateOperator.CreateOperator(type);
                op.ReadXml(node);
                return op;
            }

            return null;
        }

        public static (MrfValueType Type, float Value, MetaHash ParameterName) GetChildParameterizedFloat(XmlNode node, string name)
        {
            var type = MrfValueType.None;
            var value = 0.0f;
            var parameter = default(MetaHash);

            var childNode = node.SelectSingleNode(name);
            if (childNode?.Attributes?["value"] != null)
            {
                type = MrfValueType.Literal;
                value = Xml.GetFloatAttribute(childNode, "value");
            }
            else if (childNode?.Attributes?["parameter"] != null)
            {
                type = MrfValueType.Parameter;
                parameter = XmlMeta.GetHash(Xml.GetStringAttribute(childNode, "parameter"));
            }

            return (type, value, parameter);
        }

        public static (MrfValueType Type, bool Value, MetaHash ParameterName) GetChildParameterizedBool(XmlNode node, string name)
        {
            var type = MrfValueType.None;
            var value = false;
            var parameter = default(MetaHash);

            var childNode = node.SelectSingleNode(name);
            if (childNode?.Attributes?["value"] != null)
            {
                type = MrfValueType.Literal;
                value = Xml.GetBoolAttribute(childNode, "value");
            }
            else if (childNode?.Attributes?["parameter"] != null)
            {
                type = MrfValueType.Parameter;
                parameter = XmlMeta.GetHash(Xml.GetStringAttribute(childNode, "parameter"));
            }

            return (type, value, parameter);
        }

        public static (MrfValueType Type, MetaHash DictionaryName, MetaHash AssetName, MetaHash ParameterName) GetChildParameterizedAsset(XmlNode node, string name)
        {
            var type = MrfValueType.None;
            var dictionaryName = default(MetaHash);
            var assetName = default(MetaHash);
            var parameter = default(MetaHash);

            var childNode = node.SelectSingleNode(name);
            var dictionaryNode = childNode?.SelectSingleNode("DictionaryName");
            var nameNode = childNode?.SelectSingleNode("Name");
            if (dictionaryNode != null && nameNode != null)
            {
                type = MrfValueType.Literal;
                dictionaryName = XmlMeta.GetHash(dictionaryNode.InnerText);
                assetName = XmlMeta.GetHash(nameNode.InnerText);
            }
            else if (childNode?.Attributes?["parameter"] != null)
            {
                type = MrfValueType.Parameter;
                parameter = XmlMeta.GetHash(Xml.GetStringAttribute(childNode, "parameter"));
            }

            return (type, dictionaryName, assetName, parameter);
        }

        public static (MrfValueType Type, MrfClipContainerType ContainerType, MetaHash ContainerName, MetaHash ClipName, MetaHash ParameterName) GetChildParameterizedClip(XmlNode node, string name)
        {
            var type = MrfValueType.None;
            var containerType = default(MrfClipContainerType);
            var containerName = default(MetaHash);
            var assetName = default(MetaHash);
            var parameter = default(MetaHash);

            var childNode = node.SelectSingleNode(name);
            var containerTypeNode = childNode?.SelectSingleNode("ContainerType");
            var containerNode = childNode?.SelectSingleNode("ContainerName");
            var nameNode = childNode?.SelectSingleNode("Name");
            if (containerTypeNode != null && nameNode != null)
            {
                type = MrfValueType.Literal;
                containerType = Xml.GetEnumValue<MrfClipContainerType>(containerTypeNode.InnerText);
                if (containerType != MrfClipContainerType.LocalFile && containerNode == null)
                    throw new InvalidDataException("A non-local movement clip requires a container name.");
                containerName = XmlMeta.GetHash(containerNode?.InnerText);
                assetName = XmlMeta.GetHash(nameNode.InnerText);
            }
            else if (childNode?.Attributes?["parameter"] != null)
            {
                type = MrfValueType.Parameter;
                parameter = XmlMeta.GetHash(Xml.GetStringAttribute(childNode, "parameter"));
            }

            return (type, containerType, containerName, assetName, parameter);
        }
    }
}
