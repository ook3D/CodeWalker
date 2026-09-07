using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX;
using System.IO;
using System.Xml;

using TC = System.ComponentModel.TypeConverterAttribute;
using EXP = System.ComponentModel.ExpandableObjectConverter;


namespace CodeWalker.GameFiles
{
    [TC(typeof(EXP))] public class PedsFile : GameFile, PackedFile
    {
        public PsoFile? Pso { get; set; }
        public string Xml { get; set; } = string.Empty;

        public CPedModelInfo__InitDataList? InitDataList { get; set; }

        public PedsFile() : base(null, GameFileType.Peds)
        { }
        public PedsFile(RpfFileEntry entry) : base(entry, GameFileType.Peds)
        { }

        public void Load(byte[] data, RpfFileEntry entry)
        {
            RpfFileEntry = entry;
            Name = entry.Name;
            FilePath = Name;



            //can be PSO .ymt or XML .meta
            using MemoryStream ms = new(data);
            if (PsoFile.IsPSO(ms))
            {
                Pso = new PsoFile();
                Pso.Load(data);
                Xml = PsoXml.GetXml(Pso); //yep let's just convert that to XML :P
            }
            else
            {
                Xml = TextUtil.GetUTF8Text(data);
            }

            XmlDocument xdoc = new();
            if (!string.IsNullOrEmpty(Xml))
            {
                try
                {
                    xdoc.LoadXml(Xml);
                }
                catch (Exception ex)
                {
                    var msg = ex.Message;
                }
            }
            else
            { }


            if (xdoc.DocumentElement != null)
            {
                InitDataList = new CPedModelInfo__InitDataList(xdoc.DocumentElement);
            }




            Loaded = true;
        }
    }



    [TC(typeof(EXP))] public class CPedModelInfo__InitDataList
    {
        public string residentTxd { get; set; }
        public string[] residentAnims { get; set; } = [];
        public CPedModelInfo__InitData[] InitDatas { get; set; } = [];
        public CTxdRelationship[] txdRelationships { get; set; } = [];
        public CMultiTxdRelationship[] multiTxdRelationships { get; set; } = [];

        public CPedModelInfo__InitDataList(XmlNode node)
        {
            XmlNode[] items;

            residentTxd = Xml.GetChildInnerText(node, "residentTxd") ?? string.Empty;

            items = node.SelectSingleNode("residentAnims")?.SelectNodes("Item")?.Cast<XmlNode>().ToArray() ?? [];
            if (items.Length > 0)
            {
                residentAnims = new string[items.Length];
                for (int i = 0; i < items.Length; i++)
                {
                    residentAnims[i] = items[i].InnerText;
                }
            }
            items = node.SelectSingleNode("InitDatas")?.SelectNodes("Item")?.Cast<XmlNode>().ToArray() ?? [];
            if (items.Length > 0)
            {
                InitDatas = new CPedModelInfo__InitData[items.Length];
                for (int i = 0; i < items.Length; i++)
                {
                    InitDatas[i] = new CPedModelInfo__InitData(items[i]);
                }
            }
            items = node.SelectSingleNode("txdRelationships")?.SelectNodes("Item")?.Cast<XmlNode>().ToArray() ?? [];
            if (items.Length > 0)
            {
                txdRelationships = new CTxdRelationship[items.Length];
                for (int i = 0; i < items.Length; i++)
                {
                    txdRelationships[i] = new CTxdRelationship(items[i]);
                }
            }
            items = node.SelectSingleNode("multiTxdRelationships")?.SelectNodes("Item")?.Cast<XmlNode>().ToArray() ?? [];
            if (items.Length > 0)
            {
                multiTxdRelationships = new CMultiTxdRelationship[items.Length];
                for (int i = 0; i < items.Length; i++)
                {
                    multiTxdRelationships[i] = new CMultiTxdRelationship(items[i]);
                }
            }

        }
    }

    [TC(typeof(EXP))] public class CPedModelInfo__InitData
    {
        public string Name { get; set; }
        public string PropsName { get; set; }
        public string ClipDictionaryName { get; set; }
        public string BlendShapeFileName { get; set; }
        public string ExpressionSetName { get; set; }
        public string ExpressionDictionaryName { get; set; }
        public string ExpressionName { get; set; }
        public string Pedtype { get; set; }
        public string MovementClipSet { get; set; }
        public string[] MovementClipSets { get; set; } = [];
        public string StrafeClipSet { get; set; }
        public string MovementToStrafeClipSet { get; set; }
        public string InjuredStrafeClipSet { get; set; }
        public string FullBodyDamageClipSet { get; set; }
        public string AdditiveDamageClipSet { get; set; }
        public string DefaultGestureClipSet { get; set; }
        public string FacialClipsetGroupName { get; set; }
        public string DefaultVisemeClipSet { get; set; }
        public string SidestepClipSet { get; set; }
        public string PoseMatcherName { get; set; }
        public string PoseMatcherProneName { get; set; }
        public string GetupSetHash { get; set; }
        public string CreatureMetadataName { get; set; }
        public string DecisionMakerName { get; set; }
        public string MotionTaskDataSetName { get; set; }
        public string DefaultTaskDataSetName { get; set; }
        public string PedCapsuleName { get; set; }
        public string PedLayoutName { get; set; }
        public string PedComponentSetName { get; set; }
        public string PedComponentClothName { get; set; }
        public string PedIKSettingsName { get; set; }
        public string TaskDataName { get; set; }
        public bool IsStreamedGfx { get; set; }
        public bool AmbulanceShouldRespondTo { get; set; }
        public bool CanRideBikeWithNoHelmet { get; set; }
        public bool CanSpawnInCar { get; set; }
        public bool IsHeadBlendPed { get; set; }
        public bool bOnlyBulkyItemVariations { get; set; }
        public string RelationshipGroup { get; set; }
        public string NavCapabilitiesName { get; set; }
        public string PerceptionInfo { get; set; }
        public string DefaultBrawlingStyle { get; set; }
        public string DefaultUnarmedWeapon { get; set; }
        public string Personality { get; set; }
        public string CombatInfo { get; set; }
        public string VfxInfoName { get; set; }
        public string AmbientClipsForFlee { get; set; }
        public string Radio1 { get; set; } // MetaName.ePedRadioGenre
        public string Radio2 { get; set; } // MetaName.ePedRadioGenre
        public float FUpOffset { get; set; }
        public float RUpOffset { get; set; }
        public float FFrontOffset { get; set; }
        public float RFrontOffset { get; set; }
        public float MinActivationImpulse { get; set; }
        public float Stubble { get; set; }
        public float HDDist { get; set; }
        public float TargetingThreatModifier { get; set; }
        public float KilledPerceptionRangeModifer { get; set; }
        public string Sexiness { get; set; } // MetaTypeName.ARRAYINFO MetaName.eSexinessFlags
        public byte Age { get; set; }
        public byte MaxPassengersInCar { get; set; }
        public string ExternallyDrivenDOFs { get; set; } // MetaTypeName.ARRAYINFO MetaName.eExternallyDrivenDOFs
        public string PedVoiceGroup { get; set; }
        public string AnimalAudioObject { get; set; }
        public string AbilityType { get; set; } // MetaName.SpecialAbilityType
        public string ThermalBehaviour { get; set; } // MetaName.ThermalBehaviour
        public string SuperlodType { get; set; } // MetaName.eSuperlodType
        public string ScenarioPopStreamingSlot { get; set; } // MetaName.eScenarioPopStreamingSlot
        public string DefaultSpawningPreference { get; set; } // MetaName.DefaultSpawnPreference
        public float DefaultRemoveRangeMultiplier { get; set; }
        public bool AllowCloseSpawning { get; set; }


        public CPedModelInfo__InitData(XmlNode node)
        {
            Name = Xml.GetChildInnerText(node, "Name") ?? string.Empty;
            PropsName = Xml.GetChildInnerText(node, "PropsName") ?? string.Empty;
            ClipDictionaryName = Xml.GetChildInnerText(node, "ClipDictionaryName") ?? string.Empty;
            BlendShapeFileName = Xml.GetChildInnerText(node, "BlendShapeFileName") ?? string.Empty;
            ExpressionSetName = Xml.GetChildInnerText(node, "ExpressionSetName") ?? string.Empty;
            ExpressionDictionaryName = Xml.GetChildInnerText(node, "ExpressionDictionaryName") ?? string.Empty;
            ExpressionName = Xml.GetChildInnerText(node, "ExpressionName") ?? string.Empty;
            Pedtype = Xml.GetChildInnerText(node, "Pedtype") ?? string.Empty;
            MovementClipSet = Xml.GetChildInnerText(node, "MovementClipSet") ?? string.Empty;
            var items = node.SelectSingleNode("MovementClipSets")?.SelectNodes("Item")?.Cast<XmlNode>().ToArray() ?? [];
            if (items.Length > 0)
            {
                MovementClipSets = new string[items.Length];
                for (int i = 0; i < items.Length; i++)
                {
                    MovementClipSets[i] = items[i].InnerText;
                }
            }
            StrafeClipSet = Xml.GetChildInnerText(node, "StrafeClipSet") ?? string.Empty;
            MovementToStrafeClipSet = Xml.GetChildInnerText(node, "MovementToStrafeClipSet") ?? string.Empty;
            InjuredStrafeClipSet = Xml.GetChildInnerText(node, "InjuredStrafeClipSet") ?? string.Empty;
            FullBodyDamageClipSet = Xml.GetChildInnerText(node, "FullBodyDamageClipSet") ?? string.Empty;
            AdditiveDamageClipSet = Xml.GetChildInnerText(node, "AdditiveDamageClipSet") ?? string.Empty;
            DefaultGestureClipSet = Xml.GetChildInnerText(node, "DefaultGestureClipSet") ?? string.Empty;
            FacialClipsetGroupName = Xml.GetChildInnerText(node, "FacialClipsetGroupName") ?? string.Empty;
            DefaultVisemeClipSet = Xml.GetChildInnerText(node, "DefaultVisemeClipSet") ?? string.Empty;
            SidestepClipSet = Xml.GetChildInnerText(node, "SidestepClipSet") ?? string.Empty;
            PoseMatcherName = Xml.GetChildInnerText(node, "PoseMatcherName") ?? string.Empty;
            PoseMatcherProneName = Xml.GetChildInnerText(node, "PoseMatcherProneName") ?? string.Empty;
            GetupSetHash = Xml.GetChildInnerText(node, "GetupSetHash") ?? string.Empty;
            CreatureMetadataName = Xml.GetChildInnerText(node, "CreatureMetadataName") ?? string.Empty;
            DecisionMakerName = Xml.GetChildInnerText(node, "DecisionMakerName") ?? string.Empty;
            MotionTaskDataSetName = Xml.GetChildInnerText(node, "MotionTaskDataSetName") ?? string.Empty;
            DefaultTaskDataSetName = Xml.GetChildInnerText(node, "DefaultTaskDataSetName") ?? string.Empty;
            PedCapsuleName = Xml.GetChildInnerText(node, "PedCapsuleName") ?? string.Empty;
            PedLayoutName = Xml.GetChildInnerText(node, "PedLayoutName") ?? string.Empty;
            PedComponentSetName = Xml.GetChildInnerText(node, "PedComponentSetName") ?? string.Empty;
            PedComponentClothName = Xml.GetChildInnerText(node, "PedComponentClothName") ?? string.Empty;
            PedIKSettingsName = Xml.GetChildInnerText(node, "PedIKSettingsName") ?? string.Empty;
            TaskDataName = Xml.GetChildInnerText(node, "TaskDataName") ?? string.Empty;
            IsStreamedGfx = Xml.GetChildBoolAttribute(node, "IsStreamedGfx", "value");
            AmbulanceShouldRespondTo = Xml.GetChildBoolAttribute(node, "AmbulanceShouldRespondTo", "value");
            CanRideBikeWithNoHelmet = Xml.GetChildBoolAttribute(node, "CanRideBikeWithNoHelmet", "value");
            CanSpawnInCar = Xml.GetChildBoolAttribute(node, "CanSpawnInCar", "value");
            IsHeadBlendPed = Xml.GetChildBoolAttribute(node, "IsHeadBlendPed", "value");
            bOnlyBulkyItemVariations = Xml.GetChildBoolAttribute(node, "bOnlyBulkyItemVariations", "value");
            RelationshipGroup = Xml.GetChildInnerText(node, "RelationshipGroup") ?? string.Empty;
            NavCapabilitiesName = Xml.GetChildInnerText(node, "NavCapabilitiesName") ?? string.Empty;
            PerceptionInfo = Xml.GetChildInnerText(node, "PerceptionInfo") ?? string.Empty;
            DefaultBrawlingStyle = Xml.GetChildInnerText(node, "DefaultBrawlingStyle") ?? string.Empty;
            DefaultUnarmedWeapon = Xml.GetChildInnerText(node, "DefaultUnarmedWeapon") ?? string.Empty;
            Personality = Xml.GetChildInnerText(node, "Personality") ?? string.Empty;
            CombatInfo = Xml.GetChildInnerText(node, "CombatInfo") ?? string.Empty;
            VfxInfoName = Xml.GetChildInnerText(node, "VfxInfoName") ?? string.Empty;
            AmbientClipsForFlee = Xml.GetChildInnerText(node, "AmbientClipsForFlee") ?? string.Empty;
            Radio1 = Xml.GetChildInnerText(node, "Radio1") ?? string.Empty; // MetaName.ePedRadioGenre
            Radio2 = Xml.GetChildInnerText(node, "Radio2") ?? string.Empty; // MetaName.ePedRadioGenre
            FUpOffset = Xml.GetChildFloatAttribute(node, "FUpOffset", "value");
            RUpOffset = Xml.GetChildFloatAttribute(node, "RUpOffset", "value");
            FFrontOffset = Xml.GetChildFloatAttribute(node, "FFrontOffset", "value");
            RFrontOffset = Xml.GetChildFloatAttribute(node, "RFrontOffset", "value");
            MinActivationImpulse = Xml.GetChildFloatAttribute(node, "MinActivationImpulse", "value");
            Stubble = Xml.GetChildFloatAttribute(node, "Stubble", "value");
            HDDist = Xml.GetChildFloatAttribute(node, "HDDist", "value");
            TargetingThreatModifier = Xml.GetChildFloatAttribute(node, "TargetingThreatModifier", "value");
            KilledPerceptionRangeModifer = Xml.GetChildFloatAttribute(node, "KilledPerceptionRangeModifer", "value");
            Sexiness = Xml.GetChildInnerText(node, "Sexiness") ?? string.Empty; // MetaTypeName.ARRAYINFO MetaName.eSexinessFlags
            Age = (byte)Xml.GetChildUIntAttribute(node, "Age", "value");
            MaxPassengersInCar = (byte)Xml.GetChildUIntAttribute(node, "MaxPassengersInCar", "value");
            ExternallyDrivenDOFs = Xml.GetChildInnerText(node, "ExternallyDrivenDOFs") ?? string.Empty; // MetaTypeName.ARRAYINFO MetaName.eExternallyDrivenDOFs
            PedVoiceGroup = Xml.GetChildInnerText(node, "PedVoiceGroup") ?? string.Empty;
            AnimalAudioObject = Xml.GetChildInnerText(node, "AnimalAudioObject") ?? string.Empty;
            AbilityType = Xml.GetChildInnerText(node, "AbilityType") ?? string.Empty; // MetaName.SpecialAbilityType
            ThermalBehaviour = Xml.GetChildInnerText(node, "ThermalBehaviour") ?? string.Empty; // MetaName.ThermalBehaviour
            SuperlodType = Xml.GetChildInnerText(node, "SuperlodType") ?? string.Empty; // MetaName.eSuperlodType
            ScenarioPopStreamingSlot = Xml.GetChildInnerText(node, "ScenarioPopStreamingSlot") ?? string.Empty; // MetaName.eScenarioPopStreamingSlot
            DefaultSpawningPreference = Xml.GetChildInnerText(node, "DefaultSpawningPreference") ?? string.Empty; // MetaName.DefaultSpawnPreference
            DefaultRemoveRangeMultiplier = Xml.GetChildFloatAttribute(node, "DefaultRemoveRangeMultiplier", "value");
            AllowCloseSpawning = Xml.GetChildBoolAttribute(node, "AllowCloseSpawning", "value");
        }


        public override string ToString()
        {
            return Name;
        }
    }

    [TC(typeof(EXP))] public class CTxdRelationship
    {
        public string parent { get; set; }
        public string child { get; set; }

        public CTxdRelationship(XmlNode node)
        {
            parent = Xml.GetChildInnerText(node, "parent") ?? string.Empty;
            child = Xml.GetChildInnerText(node, "child") ?? string.Empty;
        }

        public override string ToString()
        {
            return parent + ": " + child;
        }
    }

    [TC(typeof(EXP))] public class CMultiTxdRelationship
    {
        public string parent { get; set; }
        public string[] children { get; set; } = [];

        public CMultiTxdRelationship(XmlNode node)
        {
            parent = Xml.GetChildInnerText(node, "parent") ?? string.Empty;
            var items = node.SelectSingleNode("children")?.SelectNodes("Item")?.Cast<XmlNode>().ToArray() ?? [];
            if (items.Length > 0)
            {
                children = new string[items.Length];
                for (int i = 0; i < items.Length; i++)
                {
                    children[i] = items[i].InnerText;
                }
            }
        }

        public override string ToString()
        {
            return parent + ": " + (children?.Length ?? 0).ToString() + " children";
        }
    }






}


