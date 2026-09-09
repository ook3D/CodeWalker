using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace CodeWalker.GameFiles
{
    public class VehiclesFile : GameFile, PackedFile
    {


        public string ResidentTxd { get; set; } = string.Empty;
        public List<VehicleInitData> InitDatas { get; set; } = new();
        public Dictionary<string, string> TxdRelationships { get; set; } = new();




        public VehiclesFile() : base(null, GameFileType.Vehicles)
        {
        }
        public VehiclesFile(RpfFileEntry entry) : base(entry, GameFileType.Vehicles)
        {
        }



        public void Load(byte[] data, RpfFileEntry entry)
        {
            RpfFileEntry = entry;
            Name = entry.Name;
            FilePath = Name;


            if (entry.NameLower.EndsWith(".meta"))
            {
                string xml = TextUtil.GetUTF8Text(data);

                XmlDocument xmldoc = new();
                xmldoc.LoadXml(xml);


                ResidentTxd = Xml.GetChildInnerText(xmldoc.SelectSingleNode("CVehicleModelInfo__InitDataList"), "residentTxd") ?? string.Empty;

                LoadInitDatas(xmldoc);

                LoadTxdRelationships(xmldoc);

                Loaded = true;
            }
        }


        private void LoadInitDatas(XmlDocument xmldoc)
        {
            var items = xmldoc.SelectNodes("CVehicleModelInfo__InitDataList/InitDatas/Item | CVehicleModelInfo__InitDataList/InitDatas/item")?.Cast<XmlNode>().ToArray() ?? [];

            InitDatas = new List<VehicleInitData>();
            for (int i = 0; i < items.Length; i++)
            {
                var node = items[i];
                VehicleInitData d = new();
                d.Load(node);
                InitDatas.Add(d);
            }
        }

        private void LoadTxdRelationships(XmlDocument xmldoc)
        {
            var items = xmldoc.SelectNodes("CVehicleModelInfo__InitDataList/txdRelationships/Item | CVehicleModelInfo__InitDataList/txdRelationships/item")?.Cast<XmlNode>().ToArray() ?? [];

            TxdRelationships = new Dictionary<string, string>();
            for (int i = 0; i < items.Length; i++)
            {
                var parentstr = Xml.GetChildInnerText(items[i], "parent");
                var childstr = Xml.GetChildInnerText(items[i], "child");

                if ((!string.IsNullOrEmpty(parentstr)) && (!string.IsNullOrEmpty(childstr)))
                {
                    if (!TxdRelationships.ContainsKey(childstr))
                    {
                        TxdRelationships.Add(childstr, parentstr);
                    }
                    else
                    { }
                }
            }
        }


    }


    public class VehicleInitData
    {
        
        public string modelName { get; set; } = string.Empty;                   //<modelName>impaler3</modelName>
        public string txdName { get; set; } = string.Empty;                     //<txdName>impaler3</txdName>
        public string handlingId { get; set; } = string.Empty;                  //<handlingId>IMPALER3</handlingId>
        public string gameName { get; set; } = string.Empty;                    //<gameName>IMPALER3</gameName>
        public string vehicleMakeName { get; set; } = string.Empty;             //<vehicleMakeName>DECLASSE</vehicleMakeName>
        public string expressionDictName { get; set; } = string.Empty;          //<expressionDictName>null</expressionDictName>
        public string expressionName { get; set; } = string.Empty;              //<expressionName>null</expressionName>
        public string animConvRoofDictName { get; set; } = string.Empty;        //<animConvRoofDictName>null</animConvRoofDictName>
        public string animConvRoofName { get; set; } = string.Empty;            //<animConvRoofName>null</animConvRoofName>
        public string animConvRoofWindowsAffected { get; set; } = string.Empty; //<animConvRoofWindowsAffected />
        public string ptfxAssetName { get; set; } = string.Empty;               //<ptfxAssetName>weap_xs_vehicle_weapons</ptfxAssetName>
        public string audioNameHash { get; set; } = string.Empty;               //<audioNameHash />
        public string layout { get; set; } = string.Empty;                      //<layout>LAYOUT_STD_ARENA_1HONLY</layout>
        public string coverBoundOffsets { get; set; } = string.Empty;           //<coverBoundOffsets>IMPALER_COVER_OFFSET_INFO</coverBoundOffsets>
        public string explosionInfo { get; set; } = string.Empty;               //<explosionInfo>EXPLOSION_INFO_DEFAULT</explosionInfo>
        public string scenarioLayout { get; set; } = string.Empty;              //<scenarioLayout />
        public string cameraName { get; set; } = string.Empty;                  //<cameraName>FOLLOW_CHEETAH_CAMERA</cameraName>
        public string aimCameraName { get; set; } = string.Empty;               //<aimCameraName>DEFAULT_THIRD_PERSON_VEHICLE_AIM_CAMERA</aimCameraName>
        public string bonnetCameraName { get; set; } = string.Empty;            //<bonnetCameraName>VEHICLE_BONNET_CAMERA_STANDARD_LONG_DEVIANT</bonnetCameraName>
        public string povCameraName { get; set; } = string.Empty;               //<povCameraName>REDUCED_NEAR_CLIP_POV_CAMERA</povCameraName>
        public Vector3 FirstPersonDriveByIKOffset { get; set; }                     //<FirstPersonDriveByIKOffset x="0.020000" y="-0.065000" z="-0.050000" />
        public Vector3 FirstPersonDriveByUnarmedIKOffset { get; set; }              //<FirstPersonDriveByUnarmedIKOffset x="0.000000" y="-0.100000" z="0.000000" />
        public Vector3 FirstPersonProjectileDriveByIKOffset { get; set; }           //<FirstPersonProjectileDriveByIKOffset x="0.000000" y="-0.130000" z="-0.050000" />
        public Vector3 FirstPersonProjectileDriveByPassengerIKOffset { get; set; }  //<FirstPersonProjectileDriveByPassengerIKOffset x="0.000000" y="-0.100000" z="0.000000" />
        public Vector3 FirstPersonDriveByRightPassengerIKOffset { get; set; }       //<FirstPersonDriveByRightPassengerIKOffset x="-0.020000" y="-0.065000" z="-0.050000" />
        public Vector3 FirstPersonDriveByRightPassengerUnarmedIKOffset { get; set; }//<FirstPersonDriveByRightPassengerUnarmedIKOffset x="0.000000" y="-0.100000" z="0.000000" />
        public Vector3 FirstPersonMobilePhoneOffset { get; set; }                   //<FirstPersonMobilePhoneOffset x="0.146000" y="0.220000" z="0.510000" />
        public Vector3 FirstPersonPassengerMobilePhoneOffset { get; set; }          //<FirstPersonPassengerMobilePhoneOffset x="0.234000" y="0.169000" z="0.395000" />
        public Vector3 PovCameraOffset { get; set; }                                //<PovCameraOffset x="0.000000" y="-0.195000" z="0.640000" />
        public Vector3 PovCameraVerticalAdjustmentForRollCage { get; set; }         //<PovCameraVerticalAdjustmentForRollCage value="0.000000" />
        public Vector3 PovPassengerCameraOffset { get; set; }                       //<PovPassengerCameraOffset x="0.000000" y="0.000000" z="0.000000" />
        public Vector3 PovRearPassengerCameraOffset { get; set; }                   //<PovRearPassengerCameraOffset x="0.000000" y="0.000000" z="0.000000" />
        public string vfxInfoName { get; set; } = string.Empty;                         //<vfxInfoName>VFXVEHICLEINFO_CAR_GENERIC</vfxInfoName>
        public bool shouldUseCinematicViewMode { get; set; }            //<shouldUseCinematicViewMode value="true" />
        public bool shouldCameraTransitionOnClimbUpDown { get; set; }   //<shouldCameraTransitionOnClimbUpDown value="false" />
        public bool shouldCameraIgnoreExiting { get; set; }             //<shouldCameraIgnoreExiting value="false" />
        public bool AllowPretendOccupants { get; set; }                 //<AllowPretendOccupants value="true" />
        public bool AllowJoyriding { get; set; }                        //<AllowJoyriding value="true" />
        public bool AllowSundayDriving { get; set; }                    //<AllowSundayDriving value="true" />
        public bool AllowBodyColorMapping { get; set; }                 //<AllowBodyColorMapping value="true" />
        public float wheelScale { get; set; }                           //<wheelScale value="0.202300" />
        public float wheelScaleRear { get; set; }                       //<wheelScaleRear value="0.0.201800" />
        public float dirtLevelMin { get; set; }                         //<dirtLevelMin value="0.000000" />
        public float dirtLevelMax { get; set; }                         //<dirtLevelMax value="0.450000" />
        public float envEffScaleMin { get; set; }                       //<envEffScaleMin value="0.000000" />
        public float envEffScaleMax { get; set; }                       //<envEffScaleMax value="1.000000" />
        public float envEffScaleMin2 { get; set; }                      //<envEffScaleMin2 value="0.000000" />
        public float envEffScaleMax2 { get; set; }                      //<envEffScaleMax2 value="1.000000" />
        public float damageMapScale { get; set; }                       //<damageMapScale value="0.000000" />
        public float damageOffsetScale { get; set; }                    //<damageOffsetScale value="0.100000" />
        public Color4 diffuseTint { get; set; }                         //<diffuseTint value="0x00FFFFFF" />
        public float steerWheelMult { get; set; }                       //<steerWheelMult value="0.700000" />
        public float HDTextureDist { get; set; }                        //<HDTextureDist value="5.000000" />
        public float[] lodDistances { get; set; } = [];                       //<lodDistances content="float_array">//  10.000000//  25.000000//  60.000000//  120.000000//  500.000000//  500.000000//</lodDistances>
        public float minSeatHeight { get; set; }                        //<minSeatHeight value="0.844" />
        public float identicalModelSpawnDistance { get; set; }          //<identicalModelSpawnDistance value="20" />
        public int maxNumOfSameColor { get; set; }                      //<maxNumOfSameColor value="1" />
        public float defaultBodyHealth { get; set; }                    //<defaultBodyHealth value="1000.000000" />
        public float pretendOccupantsScale { get; set; }                //<pretendOccupantsScale value="1.000000" />
        public float visibleSpawnDistScale { get; set; }                //<visibleSpawnDistScale value="1.000000" />
        public float trackerPathWidth { get; set; }                     //<trackerPathWidth value="2.000000" />
        public float weaponForceMult { get; set; }                      //<weaponForceMult value="1.000000" />
        public float frequency { get; set; }                            //<frequency value="30" />
        public string swankness { get; set; } = string.Empty;                           //<swankness>SWANKNESS_4</swankness>
        public int maxNum { get; set; }                                 //<maxNum value="10" />
        public string[] flags { get; set; } = [];                             //<flags>FLAG_RECESSED_HEADLIGHT_CORONAS FLAG_EXTRAS_STRONG FLAG_AVERAGE_CAR FLAG_HAS_INTERIOR_EXTRAS FLAG_CAN_HAVE_NEONS FLAG_HAS_JUMP_MOD FLAG_HAS_NITROUS_MOD FLAG_HAS_RAMMING_SCOOP_MOD FLAG_USE_AIRCRAFT_STYLE_WEAPON_TARGETING FLAG_HAS_SIDE_SHUNT FLAG_HAS_WEAPON_SPIKE_MODS FLAG_HAS_SUPERCHARGER FLAG_INCREASE_CAMBER_WITH_SUSPENSION_MOD FLAG_DISABLE_DEFORMATION</flags>
        public string type { get; set; } = string.Empty;                                //<type>VEHICLE_TYPE_CAR</type>
        public string plateType { get; set; } = string.Empty;                           //<plateType>VPT_FRONT_AND_BACK_PLATES</plateType>
        public string dashboardType { get; set; } = string.Empty;                       //<dashboardType>VDT_DUKES</dashboardType>
        public string vehicleClass { get; set; } = string.Empty;                        //<vehicleClass>VC_MUSCLE</vehicleClass>
        public string wheelType { get; set; } = string.Empty;                           //<wheelType>VWT_MUSCLE</wheelType>
        public string[] trailers { get; set; } = [];                          //<trailers />
        public string[] additionalTrailers { get; set; } = [];                //<additionalTrailers />
        public VehicleDriver[] drivers { get; set; } = [];                    //<drivers />
        public string[] extraIncludes { get; set; } = [];                     //<extraIncludes />
        public string[] doorsWithCollisionWhenClosed { get; set; } = [];      //<doorsWithCollisionWhenClosed />
        public string[] driveableDoors { get; set; } = [];                    //<driveableDoors />
        public bool bumpersNeedToCollideWithMap { get; set; }           //<bumpersNeedToCollideWithMap value="false" />
        public bool needsRopeTexture { get; set; }                      //<needsRopeTexture value="false" />
        public string[] requiredExtras { get; set; } = [];                    //<requiredExtras>EXTRA_1 EXTRA_2 EXTRA_3</requiredExtras>
        public string[] rewards { get; set; } = [];                           //<rewards />
        public string[] cinematicPartCamera { get; set; } = [];               //<cinematicPartCamera>//  <Item>WHEEL_FRONT_RIGHT_CAMERA</Item>//  <Item>WHEEL_FRONT_LEFT_CAMERA</Item>//  <Item>WHEEL_REAR_RIGHT_CAMERA</Item>//  <Item>WHEEL_REAR_LEFT_CAMERA</Item>//</cinematicPartCamera>
        public string NmBraceOverrideSet { get; set; } = string.Empty;                  //<NmBraceOverrideSet />
        public Vector3 buoyancySphereOffset { get; set; }               //<buoyancySphereOffset x="0.000000" y="0.000000" z="0.000000" />
        public float buoyancySphereSizeScale { get; set; }              //<buoyancySphereSizeScale value="1.000000" />
        public VehicleOverrideRagdollThreshold? pOverrideRagdollThreshold { get; set; }  //<pOverrideRagdollThreshold type="NULL" />
        public string[] firstPersonDrivebyData { get; set; } = [];            //<firstPersonDrivebyData>//  <Item>STD_IMPALER2_FRONT_LEFT</Item>//  <Item>STD_IMPALER2_FRONT_RIGHT</Item>//</firstPersonDrivebyData>


        public void Load(XmlNode node)
        {
            modelName = Xml.GetChildInnerText(node, "modelName") ?? string.Empty;
            txdName = Xml.GetChildInnerText(node, "txdName") ?? string.Empty;
            handlingId = Xml.GetChildInnerText(node, "handlingId") ?? string.Empty;
            gameName = Xml.GetChildInnerText(node, "gameName") ?? string.Empty;
            vehicleMakeName = Xml.GetChildInnerText(node, "vehicleMakeName") ?? string.Empty;
            expressionDictName = Xml.GetChildInnerText(node, "expressionDictName") ?? string.Empty;
            expressionName = Xml.GetChildInnerText(node, "expressionName") ?? string.Empty;
            animConvRoofDictName = Xml.GetChildInnerText(node, "animConvRoofDictName") ?? string.Empty;
            animConvRoofName = Xml.GetChildInnerText(node, "animConvRoofName") ?? string.Empty;
            animConvRoofWindowsAffected = Xml.GetChildInnerText(node, "animConvRoofWindowsAffected") ?? string.Empty;//?
            ptfxAssetName = Xml.GetChildInnerText(node, "ptfxAssetName") ?? string.Empty;
            audioNameHash = Xml.GetChildInnerText(node, "audioNameHash") ?? string.Empty;
            layout = Xml.GetChildInnerText(node, "layout") ?? string.Empty;
            coverBoundOffsets = Xml.GetChildInnerText(node, "coverBoundOffsets") ?? string.Empty;
            explosionInfo = Xml.GetChildInnerText(node, "explosionInfo") ?? string.Empty;
            scenarioLayout = Xml.GetChildInnerText(node, "scenarioLayout") ?? string.Empty;
            cameraName = Xml.GetChildInnerText(node, "cameraName") ?? string.Empty;
            aimCameraName = Xml.GetChildInnerText(node, "aimCameraName") ?? string.Empty;
            bonnetCameraName = Xml.GetChildInnerText(node, "bonnetCameraName") ?? string.Empty;
            povCameraName = Xml.GetChildInnerText(node, "povCameraName") ?? string.Empty;
            FirstPersonDriveByIKOffset = Xml.GetChildVector3Attributes(node, "FirstPersonDriveByIKOffset");
            FirstPersonDriveByUnarmedIKOffset = Xml.GetChildVector3Attributes(node, "FirstPersonDriveByUnarmedIKOffset");
            FirstPersonProjectileDriveByIKOffset = Xml.GetChildVector3Attributes(node, "FirstPersonProjectileDriveByIKOffset");
            FirstPersonProjectileDriveByPassengerIKOffset = Xml.GetChildVector3Attributes(node, "FirstPersonProjectileDriveByPassengerIKOffset");
            FirstPersonDriveByRightPassengerIKOffset = Xml.GetChildVector3Attributes(node, "FirstPersonDriveByRightPassengerIKOffset");
            FirstPersonDriveByRightPassengerUnarmedIKOffset = Xml.GetChildVector3Attributes(node, "FirstPersonDriveByRightPassengerUnarmedIKOffset");
            FirstPersonMobilePhoneOffset = Xml.GetChildVector3Attributes(node, "FirstPersonMobilePhoneOffset");
            FirstPersonPassengerMobilePhoneOffset = Xml.GetChildVector3Attributes(node, "FirstPersonPassengerMobilePhoneOffset");
            PovCameraOffset = Xml.GetChildVector3Attributes(node, "PovCameraOffset");
            PovCameraVerticalAdjustmentForRollCage = Xml.GetChildVector3Attributes(node, "PovCameraVerticalAdjustmentForRollCage");
            PovPassengerCameraOffset = Xml.GetChildVector3Attributes(node, "PovPassengerCameraOffset");
            PovRearPassengerCameraOffset = Xml.GetChildVector3Attributes(node, "PovRearPassengerCameraOffset");
            vfxInfoName = Xml.GetChildInnerText(node, "vfxInfoName") ?? string.Empty;
            shouldUseCinematicViewMode = Xml.GetChildBoolAttribute(node, "shouldUseCinematicViewMode", "value");
            shouldCameraTransitionOnClimbUpDown = Xml.GetChildBoolAttribute(node, "shouldCameraTransitionOnClimbUpDown", "value");
            shouldCameraIgnoreExiting = Xml.GetChildBoolAttribute(node, "shouldCameraIgnoreExiting", "value");
            AllowPretendOccupants = Xml.GetChildBoolAttribute(node, "AllowPretendOccupants", "value");
            AllowJoyriding = Xml.GetChildBoolAttribute(node, "AllowJoyriding", "value");
            AllowSundayDriving = Xml.GetChildBoolAttribute(node, "AllowSundayDriving", "value");
            AllowBodyColorMapping = Xml.GetChildBoolAttribute(node, "AllowBodyColorMapping", "value");
            wheelScale = Xml.GetChildFloatAttribute(node, "wheelScale", "value");
            wheelScaleRear = Xml.GetChildFloatAttribute(node, "wheelScaleRear", "value");
            dirtLevelMin = Xml.GetChildFloatAttribute(node, "dirtLevelMin", "value");
            dirtLevelMax = Xml.GetChildFloatAttribute(node, "dirtLevelMax", "value");
            envEffScaleMin = Xml.GetChildFloatAttribute(node, "envEffScaleMin", "value");
            envEffScaleMax = Xml.GetChildFloatAttribute(node, "envEffScaleMax", "value");
            envEffScaleMin2 = Xml.GetChildFloatAttribute(node, "envEffScaleMin2", "value");
            envEffScaleMax2 = Xml.GetChildFloatAttribute(node, "envEffScaleMax2", "value");
            damageMapScale = Xml.GetChildFloatAttribute(node, "damageMapScale", "value");
            damageOffsetScale = Xml.GetChildFloatAttribute(node, "damageOffsetScale", "value");
            diffuseTint = new Color4(Convert.ToUInt32((Xml.GetChildStringAttribute(node, "diffuseTint", "value") ?? "0").Replace("0x", ""), 16));
            steerWheelMult = Xml.GetChildFloatAttribute(node, "steerWheelMult", "value");
            HDTextureDist = Xml.GetChildFloatAttribute(node, "HDTextureDist", "value");
            lodDistances = GetFloatArray(node, "lodDistances", '\n');
            minSeatHeight = Xml.GetChildFloatAttribute(node, "minSeatHeight", "value");
            identicalModelSpawnDistance = Xml.GetChildFloatAttribute(node, "identicalModelSpawnDistance", "value");
            maxNumOfSameColor = Xml.GetChildIntAttribute(node, "maxNumOfSameColor", "value");
            defaultBodyHealth = Xml.GetChildFloatAttribute(node, "defaultBodyHealth", "value");
            pretendOccupantsScale = Xml.GetChildFloatAttribute(node, "pretendOccupantsScale", "value");
            visibleSpawnDistScale = Xml.GetChildFloatAttribute(node, "visibleSpawnDistScale", "value");
            trackerPathWidth = Xml.GetChildFloatAttribute(node, "trackerPathWidth", "value");
            weaponForceMult = Xml.GetChildFloatAttribute(node, "weaponForceMult", "value");
            frequency = Xml.GetChildFloatAttribute(node, "frequency", "value");
            swankness = Xml.GetChildInnerText(node, "swankness") ?? string.Empty;
            maxNum = Xml.GetChildIntAttribute(node, "maxNum", "value");
            flags = GetStringArray(node, "flags", ' ');
            type = Xml.GetChildInnerText(node, "type") ?? string.Empty;
            plateType = Xml.GetChildInnerText(node, "plateType") ?? string.Empty;
            dashboardType = Xml.GetChildInnerText(node, "dashboardType") ?? string.Empty;
            vehicleClass = Xml.GetChildInnerText(node, "vehicleClass") ?? string.Empty;
            wheelType = Xml.GetChildInnerText(node, "wheelType") ?? string.Empty;
            trailers = GetStringItemArray(node, "trailers");
            additionalTrailers = GetStringItemArray(node, "additionalTrailers");
            var dnode = node.SelectSingleNode("drivers");
            if (dnode != null)
            {
                var items = dnode.SelectNodes("Item")?.Cast<XmlNode>().ToArray() ?? [];
                if (items.Length > 0)
                {
                    drivers = new VehicleDriver[items.Length];
                    for (int i = 0; i < items.Length; i++)
                    {
                        var item = items[i];
                        var driver = new VehicleDriver();
                        driver.driverName = Xml.GetChildInnerText(item, "driverName") ?? string.Empty;
                        driver.npcName = Xml.GetChildInnerText(item, "npcName") ?? string.Empty;
                        drivers[i] = driver;
                    }
                }
            }
            extraIncludes = GetStringItemArray(node, "extraIncludes");
            doorsWithCollisionWhenClosed = GetStringItemArray(node, "doorsWithCollisionWhenClosed");
            driveableDoors = GetStringItemArray(node, "driveableDoors");
            bumpersNeedToCollideWithMap = Xml.GetChildBoolAttribute(node, "bumpersNeedToCollideWithMap", "value");
            needsRopeTexture = Xml.GetChildBoolAttribute(node, "needsRopeTexture", "value");
            requiredExtras = GetStringArray(node, "requiredExtras", ' ');
            rewards = GetStringItemArray(node, "rewards");
            cinematicPartCamera = GetStringItemArray(node, "cinematicPartCamera");
            NmBraceOverrideSet = Xml.GetChildInnerText(node, "NmBraceOverrideSet") ?? string.Empty;
            buoyancySphereOffset = Xml.GetChildVector3Attributes(node, "buoyancySphereOffset");
            buoyancySphereSizeScale = Xml.GetChildFloatAttribute(node, "buoyancySphereSizeScale", "value");
            var tnode = node.SelectSingleNode("pOverrideRagdollThreshold");
            if (tnode != null)
            {
                var ttype = tnode.Attributes?["type"]?.Value;
                switch (ttype)
                {
                    case "NULL": break;
                    case "CVehicleModelInfo__CVehicleOverrideRagdollThreshold":
                        pOverrideRagdollThreshold = new VehicleOverrideRagdollThreshold();
                        pOverrideRagdollThreshold.MinComponent = Xml.GetChildIntAttribute(tnode, "MinComponent", "value");
                        pOverrideRagdollThreshold.MaxComponent = Xml.GetChildIntAttribute(tnode, "MaxComponent", "value");
                        pOverrideRagdollThreshold.ThresholdMult = Xml.GetChildFloatAttribute(tnode, "ThresholdMult", "value");
                        break;
                    default:
                        break;
                }
            }
            firstPersonDrivebyData = GetStringItemArray(node, "firstPersonDrivebyData");
        }

        private string[] GetStringItemArray(XmlNode node, string childName)
        {
            var cnode = node.SelectSingleNode(childName);
            if (cnode == null) return [];
            var items = cnode.SelectNodes("Item");
            if (items == null) return [];
            List<string> getStringArrayList = new();
            foreach (XmlNode inode in items)
            {
                var istr = inode.InnerText;
                if (!string.IsNullOrEmpty(istr))
                {
                    getStringArrayList.Add(istr);
                }
            }
            if (getStringArrayList.Count == 0) return [];
            return getStringArrayList.ToArray();
        }
        private string[] GetStringArray(XmlNode node, string childName, char delimiter)
        {
            var text = Xml.GetChildInnerText(node, childName).AsSpan();
            if (text.IsEmpty) return [];
            List<string> getStringArrayList = new();
            foreach (var range in text.Split(delimiter))
            {
                var ldt = text[range].Trim();
                if (!ldt.IsEmpty)
                {
                    getStringArrayList.Add(ldt.ToString());
                }
            }
            if (getStringArrayList.Count == 0) return [];
            return getStringArrayList.ToArray();
        }
        private float[] GetFloatArray(XmlNode node, string childName, char delimiter)
        {
            var text = Xml.GetChildInnerText(node, childName).AsSpan();
            if (text.IsEmpty) return [];
            List<float> getFloatArrayList = new();
            foreach (var range in text.Split(delimiter))
            {
                var ldt = text[range].Trim();
                if (!ldt.IsEmpty)
                {
                    float f;
                    if (FloatUtil.TryParse(ldt, out f))
                    {
                        getFloatArrayList.Add(f);
                    }
                }
            }
            if (getFloatArrayList.Count == 0) return [];
            return getFloatArrayList.ToArray();
        }



        public override string ToString()
        {
            return modelName;
        }
    }

    public class VehicleOverrideRagdollThreshold
    {
        public int MinComponent { get; set; }
        public int MaxComponent { get; set; }
        public float ThresholdMult { get; set; }

        public override string ToString()
        {
            return MinComponent.ToString() + ", " + MaxComponent.ToString() + ", " + ThresholdMult.ToString();
        }
    }
    public class VehicleDriver
    {
        public string driverName { get; set; } = string.Empty;
        public string npcName { get; set; } = string.Empty;

        public override string ToString()
        {
            return driverName + ", " + npcName;
        }
    }

}
