using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;
using System.Linq;
using SharpDX;
using SharpDX.XInput;
using Device = SharpDX.Direct3D11.Device;
using DeviceContext = SharpDX.Direct3D11.DeviceContext;
using CodeWalker.World;
using CodeWalker.Project;
using CodeWalker.Rendering;
using CodeWalker.GameFiles;
using CodeWalker.Properties;
using CodeWalker.Tools;

namespace CodeWalker
{
    public partial class WorldForm : Form, DXForm
    {
        public Form Form { get { return this; } } //for DXForm/DXManager use

        public Renderer Renderer = null;
        public Lock RenderSyncRoot { get { return Renderer.RenderSyncRoot; } }

        volatile bool formopen = false;
        volatile bool running = false;
        volatile bool pauserendering = false;
        volatile bool initialised = false;

        Stopwatch frametimer = new();
        Space space = new();
        Camera camera;
        Timecycle timecycle;
        Weather weather;
        Clouds clouds;
        Water water = new();
        Trains trains = new();
        Scenarios scenarios = new();
        PopZones popzones = new();
        Heightmaps heightmaps = new();
        Watermaps watermaps = new();
        AudioZones audiozones = new();

        public Space Space { get { return space; } }

        bool MouseLButtonDown = false;
        bool MouseRButtonDown = false;
        int MouseX;
        int MouseY;
        System.Drawing.Point MouseDownPoint;
        System.Drawing.Point MouseLastPoint;

        bool BoxSelectActive = false;
        bool BoxSelectPending = false;
        System.Drawing.Point BoxSelectStart;
        System.Drawing.Point BoxSelectEnd;
        const int BoxSelectThreshold = 5;

        bool rendermaps = false;
        bool renderworld = false;
        int startupviewmode = 0; //0=world, 1=ymap, 2=model
        string modelname = "dt1_tc_dufo_core";//"dt1_11_fount_decal";//"v_22_overlays";//
        string[] ymaplist;

        Vector3 prevworldpos = FloatUtil.ParseVector3String(Settings.Default.StartPosition);


        public GameFileCache GameFileCache { get { return gameFileCache; } }
        GameFileCache gameFileCache = GameFileCacheFactory.Create();


        WorldControlMode ControlMode = WorldControlMode.Free;

        Lock MouseControlSyncRoot = new();
        int MouseControlX = 0;
        int MouseControlY = 0;
        int MouseControlWheel = 0;
        MouseButtons MouseControlButtons = MouseButtons.None;
        MouseButtons MouseControlButtonsPrev = MouseButtons.None;
        bool MouseInvert = Settings.Default.MouseInvert;

        bool ControlFireToggle = false;


        int ControlBrushTimer = 0;
        bool ControlBrushEnabled;
        //float ControlBrushRadius;

        Entity camEntity = new();
        PedEntity pedEntity = new();


        bool iseditmode = false;


        List<MapIcon> Icons;
        MapIcon MarkerIcon = null;
        MapIcon LocatorIcon = null;
        MapMarker LocatorMarker = null;
        MapMarker GrabbedMarker = null;
        MapMarker SelectedMarker = null;
        MapMarker MousedMarker = null;
        List<MapMarker> Markers = new();
        List<MapMarker> SortedMarkers = new();
        List<MapMarker> MarkerBatch = new();
        bool RenderLocator = false;
        Lock markersyncroot = new();
        Lock markersortedsyncroot = new();



        



        bool rendercollisionmeshes = Settings.Default.ShowCollisionMeshes;
        List<BoundsStoreItem> collisionitems = new();
        List<YbnFile> collisionybns = new();
        Dictionary<YmapEntityDef, YbnFile> collisioninteriors = new();
        int collisionmeshrange = Settings.Default.CollisionMeshRange;
        bool[] collisionmeshlayers = { true, true, true };

        Dictionary<MetaHash, YmapFile> renderworldVisibleYmapDict = new();

        bool worldymaptimefilter = true;
        bool worldymapweatherfilter = true;
        bool hidenorthyankton = false;
        bool hidecayoperico = false;
        static readonly HashSet<string> cayoPericoFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "h4_aa_guns",
            "h4_aa_guns_1",
            "h4_aa_guns_2",
            "h4_aa_guns_3",
            "h4_aa_guns_4",
            "h4_aa_guns_5",
            "h4_aa_guns_lod",
            "h4_airstrip_hanger",
            "h4_beach",
            "h4_beach_1",
            "h4_beach_10",
            "h4_beach_11",
            "h4_beach_12",
            "h4_beach_13",
            "h4_beach_14",
            "h4_beach_15",
            "h4_beach_16",
            "h4_beach_17",
            "h4_beach_18",
            "h4_beach_19",
            "h4_beach_2",
            "h4_beach_20",
            "h4_beach_21",
            "h4_beach_22",
            "h4_beach_3",
            "h4_beach_4",
            "h4_beach_5",
            "h4_beach_6",
            "h4_beach_7",
            "h4_beach_8",
            "h4_beach_9",
            "h4_beach_bar_props",
            "h4_beach_lod",
            "h4_beach_party",
            "h4_beach_party_1",
            "h4_beach_party_10",
            "h4_beach_party_2",
            "h4_beach_party_3",
            "h4_beach_party_4",
            "h4_beach_party_5",
            "h4_beach_party_6",
            "h4_beach_party_7",
            "h4_beach_party_8",
            "h4_beach_party_9",
            "h4_beach_party_lod",
            "h4_beach_props",
            "h4_beach_props_1",
            "h4_beach_props_10",
            "h4_beach_props_11",
            "h4_beach_props_2",
            "h4_beach_props_3",
            "h4_beach_props_4",
            "h4_beach_props_5",
            "h4_beach_props_6",
            "h4_beach_props_7",
            "h4_beach_props_8",
            "h4_beach_props_9",
            "h4_beach_props_lod",
            "h4_beach_props_party",
            "h4_beach_props_slod",
            "h4_beach_slod",
            "h4_boatblockers",
            "h4_boatblockers_1",
            "h4_boatblockers_2",
            "h4_boatblockers_3",
            "h4_boatblockers_4",
            "h4_islandairstrip",
            "h4_islandairstrip_1",
            "h4_islandairstrip_10",
            "h4_islandairstrip_11",
            "h4_islandairstrip_12",
            "h4_islandairstrip_13",
            "h4_islandairstrip_14",
            "h4_islandairstrip_15",
            "h4_islandairstrip_16",
            "h4_islandairstrip_17",
            "h4_islandairstrip_18",
            "h4_islandairstrip_19",
            "h4_islandairstrip_2",
            "h4_islandairstrip_20",
            "h4_islandairstrip_21",
            "h4_islandairstrip_22",
            "h4_islandairstrip_23",
            "h4_islandairstrip_24",
            "h4_islandairstrip_25",
            "h4_islandairstrip_26",
            "h4_islandairstrip_27",
            "h4_islandairstrip_28",
            "h4_islandairstrip_29",
            "h4_islandairstrip_3",
            "h4_islandairstrip_30",
            "h4_islandairstrip_31",
            "h4_islandairstrip_32",
            "h4_islandairstrip_33",
            "h4_islandairstrip_34",
            "h4_islandairstrip_35",
            "h4_islandairstrip_36",
            "h4_islandairstrip_4",
            "h4_islandairstrip_5",
            "h4_islandairstrip_6",
            "h4_islandairstrip_7",
            "h4_islandairstrip_8",
            "h4_islandairstrip_9",
            "h4_islandairstrip_doorsclosed",
            "h4_islandairstrip_doorsclosed_1",
            "h4_islandairstrip_doorsclosed_lod",
            "h4_islandairstrip_doorsopen",
            "h4_islandairstrip_doorsopen_1",
            "h4_islandairstrip_doorsopen_lod",
            "h4_islandairstrip_hangar_props",
            "h4_islandairstrip_hangar_props_1",
            "h4_islandairstrip_hangar_props_2",
            "h4_islandairstrip_hangar_props_lod",
            "h4_islandairstrip_hangar_props_slod",
            "h4_islandairstrip_lod",
            "h4_islandairstrip_props",
            "h4_islandairstrip_propsb",
            "h4_islandairstrip_propsb_1",
            "h4_islandairstrip_propsb_2",
            "h4_islandairstrip_propsb_lod",
            "h4_islandairstrip_propsb_slod",
            "h4_islandairstrip_props_1",
            "h4_islandairstrip_props_2",
            "h4_islandairstrip_props_3",
            "h4_islandairstrip_props_lod",
            "h4_islandairstrip_props_slod",
            "h4_islandairstrip_slod",
            "h4_islandx",
            "h4_islandxcanal_props",
            "h4_islandxcanal_props_1",
            "h4_islandxcanal_props_10",
            "h4_islandxcanal_props_11",
            "h4_islandxcanal_props_12",
            "h4_islandxcanal_props_13",
            "h4_islandxcanal_props_2",
            "h4_islandxcanal_props_3",
            "h4_islandxcanal_props_4",
            "h4_islandxcanal_props_5",
            "h4_islandxcanal_props_6",
            "h4_islandxcanal_props_7",
            "h4_islandxcanal_props_8",
            "h4_islandxcanal_props_9",
            "h4_islandxcanal_props_lod",
            "h4_islandxcanal_props_slod",
            "h4_islandxdock",
            "h4_islandxdock_1",
            "h4_islandxdock_10",
            "h4_islandxdock_11",
            "h4_islandxdock_12",
            "h4_islandxdock_13",
            "h4_islandxdock_14",
            "h4_islandxdock_15",
            "h4_islandxdock_16",
            "h4_islandxdock_17",
            "h4_islandxdock_18",
            "h4_islandxdock_19",
            "h4_islandxdock_2",
            "h4_islandxdock_20",
            "h4_islandxdock_21",
            "h4_islandxdock_22",
            "h4_islandxdock_23",
            "h4_islandxdock_24",
            "h4_islandxdock_25",
            "h4_islandxdock_26",
            "h4_islandxdock_27",
            "h4_islandxdock_28",
            "h4_islandxdock_29",
            "h4_islandxdock_3",
            "h4_islandxdock_4",
            "h4_islandxdock_5",
            "h4_islandxdock_6",
            "h4_islandxdock_7",
            "h4_islandxdock_8",
            "h4_islandxdock_9",
            "h4_islandxdock_lod",
            "h4_islandxdock_props",
            "h4_islandxdock_props_1",
            "h4_islandxdock_props_2",
            "h4_islandxdock_props_2",
            "h4_islandxdock_props_2_1",
            "h4_islandxdock_props_2_10",
            "h4_islandxdock_props_2_11",
            "h4_islandxdock_props_2_12",
            "h4_islandxdock_props_2_13",
            "h4_islandxdock_props_2_14",
            "h4_islandxdock_props_2_2",
            "h4_islandxdock_props_2_3",
            "h4_islandxdock_props_2_4",
            "h4_islandxdock_props_2_5",
            "h4_islandxdock_props_2_6",
            "h4_islandxdock_props_2_7",
            "h4_islandxdock_props_2_8",
            "h4_islandxdock_props_2_9",
            "h4_islandxdock_props_2_lod",
            "h4_islandxdock_props_2_slod",
            "h4_islandxdock_props_lod",
            "h4_islandxdock_props_slod",
            "h4_islandxdock_slod",
            "h4_islandxdock_water_hatch",
            "h4_islandxtower",
            "h4_islandxtower_1",
            "h4_islandxtower_10",
            "h4_islandxtower_11",
            "h4_islandxtower_12",
            "h4_islandxtower_13",
            "h4_islandxtower_14",
            "h4_islandxtower_15",
            "h4_islandxtower_16",
            "h4_islandxtower_17",
            "h4_islandxtower_18",
            "h4_islandxtower_19",
            "h4_islandxtower_2",
            "h4_islandxtower_20",
            "h4_islandxtower_21",
            "h4_islandxtower_22",
            "h4_islandxtower_23",
            "h4_islandxtower_24",
            "h4_islandxtower_25",
            "h4_islandxtower_26",
            "h4_islandxtower_27",
            "h4_islandxtower_28",
            "h4_islandxtower_29",
            "h4_islandxtower_3",
            "h4_islandxtower_30",
            "h4_islandxtower_31",
            "h4_islandxtower_32",
            "h4_islandxtower_33",
            "h4_islandxtower_34",
            "h4_islandxtower_35",
            "h4_islandxtower_36",
            "h4_islandxtower_37",
            "h4_islandxtower_38",
            "h4_islandxtower_39",
            "h4_islandxtower_4",
            "h4_islandxtower_40",
            "h4_islandxtower_41",
            "h4_islandxtower_42",
            "h4_islandxtower_43",
            "h4_islandxtower_44",
            "h4_islandxtower_45",
            "h4_islandxtower_46",
            "h4_islandxtower_47",
            "h4_islandxtower_48",
            "h4_islandxtower_49",
            "h4_islandxtower_5",
            "h4_islandxtower_50",
            "h4_islandxtower_51",
            "h4_islandxtower_52",
            "h4_islandxtower_53",
            "h4_islandxtower_54",
            "h4_islandxtower_55",
            "h4_islandxtower_56",
            "h4_islandxtower_57",
            "h4_islandxtower_58",
            "h4_islandxtower_59",
            "h4_islandxtower_6",
            "h4_islandxtower_60",
            "h4_islandxtower_61",
            "h4_islandxtower_62",
            "h4_islandxtower_63",
            "h4_islandxtower_64",
            "h4_islandxtower_65",
            "h4_islandxtower_66",
            "h4_islandxtower_67",
            "h4_islandxtower_68",
            "h4_islandxtower_69",
            "h4_islandxtower_7",
            "h4_islandxtower_70",
            "h4_islandxtower_71",
            "h4_islandxtower_72",
            "h4_islandxtower_73",
            "h4_islandxtower_74",
            "h4_islandxtower_75",
            "h4_islandxtower_76",
            "h4_islandxtower_77",
            "h4_islandxtower_78",
            "h4_islandxtower_79",
            "h4_islandxtower_8",
            "h4_islandxtower_80",
            "h4_islandxtower_81",
            "h4_islandxtower_82",
            "h4_islandxtower_83",
            "h4_islandxtower_9",
            "h4_islandxtower_lod",
            "h4_islandxtower_slod",
            "h4_islandxtower_veg",
            "h4_islandxtower_veg_1",
            "h4_islandxtower_veg_2",
            "h4_islandxtower_veg_3",
            "h4_islandxtower_veg_4",
            "h4_islandxtower_veg_5",
            "h4_islandxtower_veg_6",
            "h4_islandxtower_veg_lod",
            "h4_islandxtower_veg_slod",
            "h4_islandx_1",
            "h4_islandx_2",
            "h4_islandx_barrack_hatch",
            "h4_islandx_barrack_props",
            "h4_islandx_barrack_props_1",
            "h4_islandx_barrack_props_10",
            "h4_islandx_barrack_props_11",
            "h4_islandx_barrack_props_12",
            "h4_islandx_barrack_props_13",
            "h4_islandx_barrack_props_14",
            "h4_islandx_barrack_props_15",
            "h4_islandx_barrack_props_16",
            "h4_islandx_barrack_props_2",
            "h4_islandx_barrack_props_3",
            "h4_islandx_barrack_props_4",
            "h4_islandx_barrack_props_5",
            "h4_islandx_barrack_props_6",
            "h4_islandx_barrack_props_7",
            "h4_islandx_barrack_props_8",
            "h4_islandx_barrack_props_9",
            "h4_islandx_barrack_props_lod",
            "h4_islandx_barrack_props_slod",
            "h4_islandx_checkpoint",
            "h4_islandx_checkpoint_1",
            "h4_islandx_checkpoint_2",
            "h4_islandx_checkpoint_3",
            "h4_islandx_checkpoint_4",
            "h4_islandx_checkpoint_5",
            "h4_islandx_checkpoint_6",
            "h4_islandx_checkpoint_7",
            "h4_islandx_checkpoint_lod",
            "h4_islandx_checkpoint_props",
            "h4_islandx_checkpoint_props_1",
            "h4_islandx_checkpoint_props_10",
            "h4_islandx_checkpoint_props_11",
            "h4_islandx_checkpoint_props_12",
            "h4_islandx_checkpoint_props_13",
            "h4_islandx_checkpoint_props_14",
            "h4_islandx_checkpoint_props_2",
            "h4_islandx_checkpoint_props_3",
            "h4_islandx_checkpoint_props_4",
            "h4_islandx_checkpoint_props_5",
            "h4_islandx_checkpoint_props_6",
            "h4_islandx_checkpoint_props_7",
            "h4_islandx_checkpoint_props_8",
            "h4_islandx_checkpoint_props_9",
            "h4_islandx_checkpoint_props_lod",
            "h4_islandx_checkpoint_props_slod",
            "h4_islandx_disc_strandedshark",
            "h4_islandx_disc_strandedshark_1",
            "h4_islandx_disc_strandedshark_lod",
            "h4_islandx_disc_strandedwhale",
            "h4_islandx_disc_strandedwhale_1",
            "h4_islandx_disc_strandedwhale_lod",
            "h4_islandx_maindock",
            "h4_islandx_maindock_1",
            "h4_islandx_maindock_10",
            "h4_islandx_maindock_11",
            "h4_islandx_maindock_12",
            "h4_islandx_maindock_13",
            "h4_islandx_maindock_14",
            "h4_islandx_maindock_15",
            "h4_islandx_maindock_16",
            "h4_islandx_maindock_17",
            "h4_islandx_maindock_18",
            "h4_islandx_maindock_19",
            "h4_islandx_maindock_2",
            "h4_islandx_maindock_20",
            "h4_islandx_maindock_21",
            "h4_islandx_maindock_22",
            "h4_islandx_maindock_23",
            "h4_islandx_maindock_24",
            "h4_islandx_maindock_25",
            "h4_islandx_maindock_26",
            "h4_islandx_maindock_27",
            "h4_islandx_maindock_28",
            "h4_islandx_maindock_29",
            "h4_islandx_maindock_3",
            "h4_islandx_maindock_30",
            "h4_islandx_maindock_31",
            "h4_islandx_maindock_32",
            "h4_islandx_maindock_4",
            "h4_islandx_maindock_5",
            "h4_islandx_maindock_6",
            "h4_islandx_maindock_7",
            "h4_islandx_maindock_8",
            "h4_islandx_maindock_9",
            "h4_islandx_maindock_lod",
            "h4_islandx_maindock_props",
            "h4_islandx_maindock_props_1",
            "h4_islandx_maindock_props_2",
            "h4_islandx_maindock_props_2",
            "h4_islandx_maindock_props_2_1",
            "h4_islandx_maindock_props_2_10",
            "h4_islandx_maindock_props_2_11",
            "h4_islandx_maindock_props_2_12",
            "h4_islandx_maindock_props_2_2",
            "h4_islandx_maindock_props_2_3",
            "h4_islandx_maindock_props_2_4",
            "h4_islandx_maindock_props_2_5",
            "h4_islandx_maindock_props_2_6",
            "h4_islandx_maindock_props_2_7",
            "h4_islandx_maindock_props_2_8",
            "h4_islandx_maindock_props_2_9",
            "h4_islandx_maindock_props_2_lod",
            "h4_islandx_maindock_props_2_slod",
            "h4_islandx_maindock_props_3",
            "h4_islandx_maindock_props_4",
            "h4_islandx_maindock_props_5",
            "h4_islandx_maindock_props_lod",
            "h4_islandx_maindock_props_slod",
            "h4_islandx_maindock_slod",
            "h4_islandx_mansion",
            "h4_islandx_mansion_1",
            "h4_islandx_mansion_10",
            "h4_islandx_mansion_11",
            "h4_islandx_mansion_12",
            "h4_islandx_mansion_13",
            "h4_islandx_mansion_14",
            "h4_islandx_mansion_15",
            "h4_islandx_mansion_16",
            "h4_islandx_mansion_17",
            "h4_islandx_mansion_18",
            "h4_islandx_mansion_19",
            "h4_islandx_mansion_2",
            "h4_islandx_mansion_20",
            "h4_islandx_mansion_21",
            "h4_islandx_mansion_22",
            "h4_islandx_mansion_23",
            "h4_islandx_mansion_24",
            "h4_islandx_mansion_25",
            "h4_islandx_mansion_26",
            "h4_islandx_mansion_27",
            "h4_islandx_mansion_28",
            "h4_islandx_mansion_29",
            "h4_islandx_mansion_3",
            "h4_islandx_mansion_30",
            "h4_islandx_mansion_31",
            "h4_islandx_mansion_32",
            "h4_islandx_mansion_33",
            "h4_islandx_mansion_34",
            "h4_islandx_mansion_35",
            "h4_islandx_mansion_36",
            "h4_islandx_mansion_37",
            "h4_islandx_mansion_38",
            "h4_islandx_mansion_39",
            "h4_islandx_mansion_4",
            "h4_islandx_mansion_40",
            "h4_islandx_mansion_41",
            "h4_islandx_mansion_42",
            "h4_islandx_mansion_43",
            "h4_islandx_mansion_44",
            "h4_islandx_mansion_45",
            "h4_islandx_mansion_46",
            "h4_islandx_mansion_47",
            "h4_islandx_mansion_48",
            "h4_islandx_mansion_49",
            "h4_islandx_mansion_5",
            "h4_islandx_mansion_50",
            "h4_islandx_mansion_51",
            "h4_islandx_mansion_52",
            "h4_islandx_mansion_53",
            "h4_islandx_mansion_54",
            "h4_islandx_mansion_55",
            "h4_islandx_mansion_56",
            "h4_islandx_mansion_57",
            "h4_islandx_mansion_58",
            "h4_islandx_mansion_59",
            "h4_islandx_mansion_6",
            "h4_islandx_mansion_60",
            "h4_islandx_mansion_61",
            "h4_islandx_mansion_62",
            "h4_islandx_mansion_63",
            "h4_islandx_mansion_64",
            "h4_islandx_mansion_65",
            "h4_islandx_mansion_66",
            "h4_islandx_mansion_67",
            "h4_islandx_mansion_68",
            "h4_islandx_mansion_69",
            "h4_islandx_mansion_7",
            "h4_islandx_mansion_70",
            "h4_islandx_mansion_71",
            "h4_islandx_mansion_8",
            "h4_islandx_mansion_9",
            "h4_islandx_mansion_b",
            "h4_islandx_mansion_b_1",
            "h4_islandx_mansion_b_10",
            "h4_islandx_mansion_b_11",
            "h4_islandx_mansion_b_12",
            "h4_islandx_mansion_b_13",
            "h4_islandx_mansion_b_14",
            "h4_islandx_mansion_b_15",
            "h4_islandx_mansion_b_16",
            "h4_islandx_mansion_b_17",
            "h4_islandx_mansion_b_18",
            "h4_islandx_mansion_b_19",
            "h4_islandx_mansion_b_2",
            "h4_islandx_mansion_b_20",
            "h4_islandx_mansion_b_21",
            "h4_islandx_mansion_b_22",
            "h4_islandx_mansion_b_23",
            "h4_islandx_mansion_b_24",
            "h4_islandx_mansion_b_25",
            "h4_islandx_mansion_b_26",
            "h4_islandx_mansion_b_27",
            "h4_islandx_mansion_b_28",
            "h4_islandx_mansion_b_29",
            "h4_islandx_mansion_b_3",
            "h4_islandx_mansion_b_30",
            "h4_islandx_mansion_b_31",
            "h4_islandx_mansion_b_32",
            "h4_islandx_mansion_b_33",
            "h4_islandx_mansion_b_34",
            "h4_islandx_mansion_b_35",
            "h4_islandx_mansion_b_36",
            "h4_islandx_mansion_b_37",
            "h4_islandx_mansion_b_38",
            "h4_islandx_mansion_b_39",
            "h4_islandx_mansion_b_4",
            "h4_islandx_mansion_b_40",
            "h4_islandx_mansion_b_41",
            "h4_islandx_mansion_b_42",
            "h4_islandx_mansion_b_43",
            "h4_islandx_mansion_b_44",
            "h4_islandx_mansion_b_45",
            "h4_islandx_mansion_b_46",
            "h4_islandx_mansion_b_47",
            "h4_islandx_mansion_b_48",
            "h4_islandx_mansion_b_49",
            "h4_islandx_mansion_b_5",
            "h4_islandx_mansion_b_50",
            "h4_islandx_mansion_b_51",
            "h4_islandx_mansion_b_52",
            "h4_islandx_mansion_b_53",
            "h4_islandx_mansion_b_54",
            "h4_islandx_mansion_b_55",
            "h4_islandx_mansion_b_6",
            "h4_islandx_mansion_b_7",
            "h4_islandx_mansion_b_8",
            "h4_islandx_mansion_b_9",
            "h4_islandx_mansion_b_lod",
            "h4_islandx_mansion_b_side_fence",
            "h4_islandx_mansion_b_side_fence_1",
            "h4_islandx_mansion_b_side_fence_2",
            "h4_islandx_mansion_b_slod",
            "h4_islandx_mansion_entrance_fence",
            "h4_islandx_mansion_entrance_fence_1",
            "h4_islandx_mansion_entrance_fence_2",
            "h4_islandx_mansion_guardfence",
            "h4_islandx_mansion_guardfence_1",
            "h4_islandx_mansion_guardfence_2",
            "h4_islandx_mansion_lights",
            "h4_islandx_mansion_lights_1",
            "h4_islandx_mansion_lockup_01",
            "h4_islandx_mansion_lockup_01_lod",
            "h4_islandx_mansion_lockup_02",
            "h4_islandx_mansion_lockup_02_lod",
            "h4_islandx_mansion_lockup_03",
            "h4_islandx_mansion_lockup_03_lod",
            "h4_islandx_mansion_lod",
            "h4_islandx_mansion_office",
            "h4_islandx_mansion_office_lod",
            "h4_islandx_mansion_props",
            "h4_islandx_mansion_props_1",
            "h4_islandx_mansion_props_10",
            "h4_islandx_mansion_props_11",
            "h4_islandx_mansion_props_12",
            "h4_islandx_mansion_props_13",
            "h4_islandx_mansion_props_14",
            "h4_islandx_mansion_props_15",
            "h4_islandx_mansion_props_16",
            "h4_islandx_mansion_props_2",
            "h4_islandx_mansion_props_3",
            "h4_islandx_mansion_props_4",
            "h4_islandx_mansion_props_5",
            "h4_islandx_mansion_props_6",
            "h4_islandx_mansion_props_7",
            "h4_islandx_mansion_props_8",
            "h4_islandx_mansion_props_9",
            "h4_islandx_mansion_props_lod",
            "h4_islandx_mansion_props_slod",
            "h4_islandx_mansion_slod",
            "h4_islandx_mansion_vault",
            "h4_islandx_mansion_vault_lod",
            "h4_islandx_placement_01",
            "h4_islandx_placement_01_1",
            "h4_islandx_placement_01_10",
            "h4_islandx_placement_01_11",
            "h4_islandx_placement_01_12",
            "h4_islandx_placement_01_13",
            "h4_islandx_placement_01_14",
            "h4_islandx_placement_01_15",
            "h4_islandx_placement_01_16",
            "h4_islandx_placement_01_17",
            "h4_islandx_placement_01_18",
            "h4_islandx_placement_01_19",
            "h4_islandx_placement_01_2",
            "h4_islandx_placement_01_20",
            "h4_islandx_placement_01_21",
            "h4_islandx_placement_01_22",
            "h4_islandx_placement_01_23",
            "h4_islandx_placement_01_24",
            "h4_islandx_placement_01_25",
            "h4_islandx_placement_01_26",
            "h4_islandx_placement_01_27",
            "h4_islandx_placement_01_28",
            "h4_islandx_placement_01_29",
            "h4_islandx_placement_01_3",
            "h4_islandx_placement_01_30",
            "h4_islandx_placement_01_31",
            "h4_islandx_placement_01_32",
            "h4_islandx_placement_01_33",
            "h4_islandx_placement_01_34",
            "h4_islandx_placement_01_35",
            "h4_islandx_placement_01_36",
            "h4_islandx_placement_01_37",
            "h4_islandx_placement_01_38",
            "h4_islandx_placement_01_39",
            "h4_islandx_placement_01_4",
            "h4_islandx_placement_01_40",
            "h4_islandx_placement_01_41",
            "h4_islandx_placement_01_42",
            "h4_islandx_placement_01_43",
            "h4_islandx_placement_01_44",
            "h4_islandx_placement_01_45",
            "h4_islandx_placement_01_46",
            "h4_islandx_placement_01_47",
            "h4_islandx_placement_01_48",
            "h4_islandx_placement_01_49",
            "h4_islandx_placement_01_5",
            "h4_islandx_placement_01_50",
            "h4_islandx_placement_01_51",
            "h4_islandx_placement_01_52",
            "h4_islandx_placement_01_53",
            "h4_islandx_placement_01_54",
            "h4_islandx_placement_01_55",
            "h4_islandx_placement_01_6",
            "h4_islandx_placement_01_7",
            "h4_islandx_placement_01_8",
            "h4_islandx_placement_01_9",
            "h4_islandx_placement_02",
            "h4_islandx_placement_02_1",
            "h4_islandx_placement_02_10",
            "h4_islandx_placement_02_11",
            "h4_islandx_placement_02_12",
            "h4_islandx_placement_02_13",
            "h4_islandx_placement_02_14",
            "h4_islandx_placement_02_15",
            "h4_islandx_placement_02_16",
            "h4_islandx_placement_02_17",
            "h4_islandx_placement_02_18",
            "h4_islandx_placement_02_19",
            "h4_islandx_placement_02_2",
            "h4_islandx_placement_02_20",
            "h4_islandx_placement_02_21",
            "h4_islandx_placement_02_22",
            "h4_islandx_placement_02_23",
            "h4_islandx_placement_02_24",
            "h4_islandx_placement_02_25",
            "h4_islandx_placement_02_26",
            "h4_islandx_placement_02_27",
            "h4_islandx_placement_02_28",
            "h4_islandx_placement_02_29",
            "h4_islandx_placement_02_3",
            "h4_islandx_placement_02_30",
            "h4_islandx_placement_02_31",
            "h4_islandx_placement_02_32",
            "h4_islandx_placement_02_33",
            "h4_islandx_placement_02_34",
            "h4_islandx_placement_02_35",
            "h4_islandx_placement_02_36",
            "h4_islandx_placement_02_37",
            "h4_islandx_placement_02_38",
            "h4_islandx_placement_02_39",
            "h4_islandx_placement_02_4",
            "h4_islandx_placement_02_40",
            "h4_islandx_placement_02_41",
            "h4_islandx_placement_02_42",
            "h4_islandx_placement_02_43",
            "h4_islandx_placement_02_44",
            "h4_islandx_placement_02_45",
            "h4_islandx_placement_02_46",
            "h4_islandx_placement_02_47",
            "h4_islandx_placement_02_48",
            "h4_islandx_placement_02_49",
            "h4_islandx_placement_02_5",
            "h4_islandx_placement_02_50",
            "h4_islandx_placement_02_51",
            "h4_islandx_placement_02_6",
            "h4_islandx_placement_02_7",
            "h4_islandx_placement_02_8",
            "h4_islandx_placement_02_9",
            "h4_islandx_placement_03",
            "h4_islandx_placement_03_1",
            "h4_islandx_placement_03_10",
            "h4_islandx_placement_03_11",
            "h4_islandx_placement_03_12",
            "h4_islandx_placement_03_13",
            "h4_islandx_placement_03_14",
            "h4_islandx_placement_03_15",
            "h4_islandx_placement_03_16",
            "h4_islandx_placement_03_17",
            "h4_islandx_placement_03_18",
            "h4_islandx_placement_03_19",
            "h4_islandx_placement_03_2",
            "h4_islandx_placement_03_20",
            "h4_islandx_placement_03_21",
            "h4_islandx_placement_03_22",
            "h4_islandx_placement_03_23",
            "h4_islandx_placement_03_24",
            "h4_islandx_placement_03_25",
            "h4_islandx_placement_03_26",
            "h4_islandx_placement_03_27",
            "h4_islandx_placement_03_28",
            "h4_islandx_placement_03_29",
            "h4_islandx_placement_03_3",
            "h4_islandx_placement_03_30",
            "h4_islandx_placement_03_31",
            "h4_islandx_placement_03_32",
            "h4_islandx_placement_03_33",
            "h4_islandx_placement_03_34",
            "h4_islandx_placement_03_35",
            "h4_islandx_placement_03_36",
            "h4_islandx_placement_03_37",
            "h4_islandx_placement_03_38",
            "h4_islandx_placement_03_39",
            "h4_islandx_placement_03_4",
            "h4_islandx_placement_03_40",
            "h4_islandx_placement_03_41",
            "h4_islandx_placement_03_42",
            "h4_islandx_placement_03_43",
            "h4_islandx_placement_03_44",
            "h4_islandx_placement_03_45",
            "h4_islandx_placement_03_46",
            "h4_islandx_placement_03_47",
            "h4_islandx_placement_03_48",
            "h4_islandx_placement_03_49",
            "h4_islandx_placement_03_5",
            "h4_islandx_placement_03_6",
            "h4_islandx_placement_03_7",
            "h4_islandx_placement_03_8",
            "h4_islandx_placement_03_9",
            "h4_islandx_placement_04",
            "h4_islandx_placement_04_1",
            "h4_islandx_placement_04_10",
            "h4_islandx_placement_04_11",
            "h4_islandx_placement_04_12",
            "h4_islandx_placement_04_13",
            "h4_islandx_placement_04_14",
            "h4_islandx_placement_04_15",
            "h4_islandx_placement_04_16",
            "h4_islandx_placement_04_17",
            "h4_islandx_placement_04_18",
            "h4_islandx_placement_04_19",
            "h4_islandx_placement_04_2",
            "h4_islandx_placement_04_20",
            "h4_islandx_placement_04_21",
            "h4_islandx_placement_04_22",
            "h4_islandx_placement_04_23",
            "h4_islandx_placement_04_24",
            "h4_islandx_placement_04_25",
            "h4_islandx_placement_04_26",
            "h4_islandx_placement_04_27",
            "h4_islandx_placement_04_28",
            "h4_islandx_placement_04_29",
            "h4_islandx_placement_04_3",
            "h4_islandx_placement_04_30",
            "h4_islandx_placement_04_31",
            "h4_islandx_placement_04_32",
            "h4_islandx_placement_04_33",
            "h4_islandx_placement_04_34",
            "h4_islandx_placement_04_35",
            "h4_islandx_placement_04_36",
            "h4_islandx_placement_04_37",
            "h4_islandx_placement_04_38",
            "h4_islandx_placement_04_39",
            "h4_islandx_placement_04_4",
            "h4_islandx_placement_04_40",
            "h4_islandx_placement_04_41",
            "h4_islandx_placement_04_42",
            "h4_islandx_placement_04_43",
            "h4_islandx_placement_04_44",
            "h4_islandx_placement_04_5",
            "h4_islandx_placement_04_6",
            "h4_islandx_placement_04_7",
            "h4_islandx_placement_04_8",
            "h4_islandx_placement_04_9",
            "h4_islandx_placement_05",
            "h4_islandx_placement_05_1",
            "h4_islandx_placement_05_10",
            "h4_islandx_placement_05_11",
            "h4_islandx_placement_05_12",
            "h4_islandx_placement_05_13",
            "h4_islandx_placement_05_14",
            "h4_islandx_placement_05_15",
            "h4_islandx_placement_05_16",
            "h4_islandx_placement_05_17",
            "h4_islandx_placement_05_18",
            "h4_islandx_placement_05_19",
            "h4_islandx_placement_05_2",
            "h4_islandx_placement_05_20",
            "h4_islandx_placement_05_21",
            "h4_islandx_placement_05_22",
            "h4_islandx_placement_05_23",
            "h4_islandx_placement_05_24",
            "h4_islandx_placement_05_25",
            "h4_islandx_placement_05_26",
            "h4_islandx_placement_05_27",
            "h4_islandx_placement_05_28",
            "h4_islandx_placement_05_29",
            "h4_islandx_placement_05_3",
            "h4_islandx_placement_05_30",
            "h4_islandx_placement_05_31",
            "h4_islandx_placement_05_32",
            "h4_islandx_placement_05_33",
            "h4_islandx_placement_05_34",
            "h4_islandx_placement_05_35",
            "h4_islandx_placement_05_36",
            "h4_islandx_placement_05_37",
            "h4_islandx_placement_05_38",
            "h4_islandx_placement_05_39",
            "h4_islandx_placement_05_4",
            "h4_islandx_placement_05_40",
            "h4_islandx_placement_05_41",
            "h4_islandx_placement_05_42",
            "h4_islandx_placement_05_43",
            "h4_islandx_placement_05_44",
            "h4_islandx_placement_05_45",
            "h4_islandx_placement_05_46",
            "h4_islandx_placement_05_47",
            "h4_islandx_placement_05_48",
            "h4_islandx_placement_05_49",
            "h4_islandx_placement_05_5",
            "h4_islandx_placement_05_50",
            "h4_islandx_placement_05_6",
            "h4_islandx_placement_05_7",
            "h4_islandx_placement_05_8",
            "h4_islandx_placement_05_9",
            "h4_islandx_placement_06",
            "h4_islandx_placement_06_1",
            "h4_islandx_placement_06_10",
            "h4_islandx_placement_06_11",
            "h4_islandx_placement_06_12",
            "h4_islandx_placement_06_13",
            "h4_islandx_placement_06_14",
            "h4_islandx_placement_06_15",
            "h4_islandx_placement_06_16",
            "h4_islandx_placement_06_17",
            "h4_islandx_placement_06_18",
            "h4_islandx_placement_06_19",
            "h4_islandx_placement_06_2",
            "h4_islandx_placement_06_20",
            "h4_islandx_placement_06_21",
            "h4_islandx_placement_06_22",
            "h4_islandx_placement_06_23",
            "h4_islandx_placement_06_24",
            "h4_islandx_placement_06_25",
            "h4_islandx_placement_06_26",
            "h4_islandx_placement_06_27",
            "h4_islandx_placement_06_28",
            "h4_islandx_placement_06_29",
            "h4_islandx_placement_06_3",
            "h4_islandx_placement_06_30",
            "h4_islandx_placement_06_31",
            "h4_islandx_placement_06_32",
            "h4_islandx_placement_06_33",
            "h4_islandx_placement_06_34",
            "h4_islandx_placement_06_35",
            "h4_islandx_placement_06_36",
            "h4_islandx_placement_06_37",
            "h4_islandx_placement_06_4",
            "h4_islandx_placement_06_5",
            "h4_islandx_placement_06_6",
            "h4_islandx_placement_06_7",
            "h4_islandx_placement_06_8",
            "h4_islandx_placement_06_9",
            "h4_islandx_placement_07",
            "h4_islandx_placement_07_1",
            "h4_islandx_placement_07_10",
            "h4_islandx_placement_07_11",
            "h4_islandx_placement_07_12",
            "h4_islandx_placement_07_13",
            "h4_islandx_placement_07_14",
            "h4_islandx_placement_07_15",
            "h4_islandx_placement_07_16",
            "h4_islandx_placement_07_17",
            "h4_islandx_placement_07_18",
            "h4_islandx_placement_07_19",
            "h4_islandx_placement_07_2",
            "h4_islandx_placement_07_20",
            "h4_islandx_placement_07_21",
            "h4_islandx_placement_07_22",
            "h4_islandx_placement_07_23",
            "h4_islandx_placement_07_24",
            "h4_islandx_placement_07_25",
            "h4_islandx_placement_07_26",
            "h4_islandx_placement_07_27",
            "h4_islandx_placement_07_28",
            "h4_islandx_placement_07_29",
            "h4_islandx_placement_07_3",
            "h4_islandx_placement_07_30",
            "h4_islandx_placement_07_31",
            "h4_islandx_placement_07_32",
            "h4_islandx_placement_07_33",
            "h4_islandx_placement_07_34",
            "h4_islandx_placement_07_35",
            "h4_islandx_placement_07_36",
            "h4_islandx_placement_07_37",
            "h4_islandx_placement_07_38",
            "h4_islandx_placement_07_4",
            "h4_islandx_placement_07_5",
            "h4_islandx_placement_07_6",
            "h4_islandx_placement_07_7",
            "h4_islandx_placement_07_8",
            "h4_islandx_placement_07_9",
            "h4_islandx_placement_08",
            "h4_islandx_placement_08_1",
            "h4_islandx_placement_08_10",
            "h4_islandx_placement_08_11",
            "h4_islandx_placement_08_12",
            "h4_islandx_placement_08_13",
            "h4_islandx_placement_08_14",
            "h4_islandx_placement_08_15",
            "h4_islandx_placement_08_16",
            "h4_islandx_placement_08_17",
            "h4_islandx_placement_08_18",
            "h4_islandx_placement_08_19",
            "h4_islandx_placement_08_2",
            "h4_islandx_placement_08_20",
            "h4_islandx_placement_08_21",
            "h4_islandx_placement_08_22",
            "h4_islandx_placement_08_23",
            "h4_islandx_placement_08_24",
            "h4_islandx_placement_08_25",
            "h4_islandx_placement_08_26",
            "h4_islandx_placement_08_27",
            "h4_islandx_placement_08_28",
            "h4_islandx_placement_08_29",
            "h4_islandx_placement_08_3",
            "h4_islandx_placement_08_30",
            "h4_islandx_placement_08_31",
            "h4_islandx_placement_08_32",
            "h4_islandx_placement_08_33",
            "h4_islandx_placement_08_34",
            "h4_islandx_placement_08_35",
            "h4_islandx_placement_08_36",
            "h4_islandx_placement_08_4",
            "h4_islandx_placement_08_5",
            "h4_islandx_placement_08_6",
            "h4_islandx_placement_08_7",
            "h4_islandx_placement_08_8",
            "h4_islandx_placement_08_9",
            "h4_islandx_placement_09",
            "h4_islandx_placement_09_1",
            "h4_islandx_placement_09_10",
            "h4_islandx_placement_09_11",
            "h4_islandx_placement_09_12",
            "h4_islandx_placement_09_13",
            "h4_islandx_placement_09_14",
            "h4_islandx_placement_09_15",
            "h4_islandx_placement_09_16",
            "h4_islandx_placement_09_17",
            "h4_islandx_placement_09_18",
            "h4_islandx_placement_09_19",
            "h4_islandx_placement_09_2",
            "h4_islandx_placement_09_20",
            "h4_islandx_placement_09_21",
            "h4_islandx_placement_09_22",
            "h4_islandx_placement_09_23",
            "h4_islandx_placement_09_24",
            "h4_islandx_placement_09_25",
            "h4_islandx_placement_09_26",
            "h4_islandx_placement_09_27",
            "h4_islandx_placement_09_28",
            "h4_islandx_placement_09_29",
            "h4_islandx_placement_09_3",
            "h4_islandx_placement_09_30",
            "h4_islandx_placement_09_4",
            "h4_islandx_placement_09_5",
            "h4_islandx_placement_09_6",
            "h4_islandx_placement_09_7",
            "h4_islandx_placement_09_8",
            "h4_islandx_placement_09_9",
            "h4_islandx_placement_10",
            "h4_islandx_placement_10_1",
            "h4_islandx_placement_10_10",
            "h4_islandx_placement_10_11",
            "h4_islandx_placement_10_12",
            "h4_islandx_placement_10_13",
            "h4_islandx_placement_10_14",
            "h4_islandx_placement_10_15",
            "h4_islandx_placement_10_16",
            "h4_islandx_placement_10_17",
            "h4_islandx_placement_10_18",
            "h4_islandx_placement_10_19",
            "h4_islandx_placement_10_2",
            "h4_islandx_placement_10_20",
            "h4_islandx_placement_10_21",
            "h4_islandx_placement_10_22",
            "h4_islandx_placement_10_23",
            "h4_islandx_placement_10_24",
            "h4_islandx_placement_10_25",
            "h4_islandx_placement_10_26",
            "h4_islandx_placement_10_27",
            "h4_islandx_placement_10_28",
            "h4_islandx_placement_10_29",
            "h4_islandx_placement_10_3",
            "h4_islandx_placement_10_30",
            "h4_islandx_placement_10_31",
            "h4_islandx_placement_10_32",
            "h4_islandx_placement_10_33",
            "h4_islandx_placement_10_34",
            "h4_islandx_placement_10_35",
            "h4_islandx_placement_10_36",
            "h4_islandx_placement_10_37",
            "h4_islandx_placement_10_38",
            "h4_islandx_placement_10_39",
            "h4_islandx_placement_10_4",
            "h4_islandx_placement_10_40",
            "h4_islandx_placement_10_41",
            "h4_islandx_placement_10_42",
            "h4_islandx_placement_10_43",
            "h4_islandx_placement_10_44",
            "h4_islandx_placement_10_45",
            "h4_islandx_placement_10_46",
            "h4_islandx_placement_10_47",
            "h4_islandx_placement_10_48",
            "h4_islandx_placement_10_49",
            "h4_islandx_placement_10_5",
            "h4_islandx_placement_10_50",
            "h4_islandx_placement_10_51",
            "h4_islandx_placement_10_52",
            "h4_islandx_placement_10_53",
            "h4_islandx_placement_10_54",
            "h4_islandx_placement_10_6",
            "h4_islandx_placement_10_7",
            "h4_islandx_placement_10_8",
            "h4_islandx_placement_10_9",
            "h4_islandx_props",
            "h4_islandx_props_1",
            "h4_islandx_props_10",
            "h4_islandx_props_11",
            "h4_islandx_props_12",
            "h4_islandx_props_13",
            "h4_islandx_props_14",
            "h4_islandx_props_15",
            "h4_islandx_props_16",
            "h4_islandx_props_17",
            "h4_islandx_props_18",
            "h4_islandx_props_19",
            "h4_islandx_props_2",
            "h4_islandx_props_20",
            "h4_islandx_props_21",
            "h4_islandx_props_22",
            "h4_islandx_props_23",
            "h4_islandx_props_24",
            "h4_islandx_props_25",
            "h4_islandx_props_26",
            "h4_islandx_props_27",
            "h4_islandx_props_28",
            "h4_islandx_props_29",
            "h4_islandx_props_3",
            "h4_islandx_props_30",
            "h4_islandx_props_31",
            "h4_islandx_props_32",
            "h4_islandx_props_33",
            "h4_islandx_props_34",
            "h4_islandx_props_35",
            "h4_islandx_props_36",
            "h4_islandx_props_37",
            "h4_islandx_props_38",
            "h4_islandx_props_39",
            "h4_islandx_props_4",
            "h4_islandx_props_40",
            "h4_islandx_props_41",
            "h4_islandx_props_42",
            "h4_islandx_props_43",
            "h4_islandx_props_44",
            "h4_islandx_props_45",
            "h4_islandx_props_46",
            "h4_islandx_props_47",
            "h4_islandx_props_48",
            "h4_islandx_props_49",
            "h4_islandx_props_5",
            "h4_islandx_props_50",
            "h4_islandx_props_51",
            "h4_islandx_props_52",
            "h4_islandx_props_53",
            "h4_islandx_props_54",
            "h4_islandx_props_55",
            "h4_islandx_props_56",
            "h4_islandx_props_57",
            "h4_islandx_props_58",
            "h4_islandx_props_59",
            "h4_islandx_props_6",
            "h4_islandx_props_60",
            "h4_islandx_props_61",
            "h4_islandx_props_62",
            "h4_islandx_props_63",
            "h4_islandx_props_64",
            "h4_islandx_props_65",
            "h4_islandx_props_66",
            "h4_islandx_props_67",
            "h4_islandx_props_68",
            "h4_islandx_props_69",
            "h4_islandx_props_7",
            "h4_islandx_props_70",
            "h4_islandx_props_71",
            "h4_islandx_props_72",
            "h4_islandx_props_73",
            "h4_islandx_props_8",
            "h4_islandx_props_9",
            "h4_islandx_props_lod",
            "h4_islandx_sea_mines",
            "h4_islandx_terrain_01",
            "h4_islandx_terrain_01_1",
            "h4_islandx_terrain_01_10",
            "h4_islandx_terrain_01_11",
            "h4_islandx_terrain_01_12",
            "h4_islandx_terrain_01_13",
            "h4_islandx_terrain_01_14",
            "h4_islandx_terrain_01_15",
            "h4_islandx_terrain_01_16",
            "h4_islandx_terrain_01_17",
            "h4_islandx_terrain_01_18",
            "h4_islandx_terrain_01_19",
            "h4_islandx_terrain_01_2",
            "h4_islandx_terrain_01_20",
            "h4_islandx_terrain_01_21",
            "h4_islandx_terrain_01_22",
            "h4_islandx_terrain_01_23",
            "h4_islandx_terrain_01_24",
            "h4_islandx_terrain_01_25",
            "h4_islandx_terrain_01_26",
            "h4_islandx_terrain_01_27",
            "h4_islandx_terrain_01_28",
            "h4_islandx_terrain_01_29",
            "h4_islandx_terrain_01_3",
            "h4_islandx_terrain_01_30",
            "h4_islandx_terrain_01_31",
            "h4_islandx_terrain_01_32",
            "h4_islandx_terrain_01_33",
            "h4_islandx_terrain_01_34",
            "h4_islandx_terrain_01_35",
            "h4_islandx_terrain_01_4",
            "h4_islandx_terrain_01_5",
            "h4_islandx_terrain_01_6",
            "h4_islandx_terrain_01_7",
            "h4_islandx_terrain_01_8",
            "h4_islandx_terrain_01_9",
            "h4_islandx_terrain_01_lod",
            "h4_islandx_terrain_01_slod",
            "h4_islandx_terrain_02",
            "h4_islandx_terrain_02_1",
            "h4_islandx_terrain_02_10",
            "h4_islandx_terrain_02_100",
            "h4_islandx_terrain_02_101",
            "h4_islandx_terrain_02_102",
            "h4_islandx_terrain_02_103",
            "h4_islandx_terrain_02_104",
            "h4_islandx_terrain_02_105",
            "h4_islandx_terrain_02_106",
            "h4_islandx_terrain_02_107",
            "h4_islandx_terrain_02_108",
            "h4_islandx_terrain_02_109",
            "h4_islandx_terrain_02_11",
            "h4_islandx_terrain_02_110",
            "h4_islandx_terrain_02_111",
            "h4_islandx_terrain_02_112",
            "h4_islandx_terrain_02_113",
            "h4_islandx_terrain_02_114",
            "h4_islandx_terrain_02_115",
            "h4_islandx_terrain_02_116",
            "h4_islandx_terrain_02_117",
            "h4_islandx_terrain_02_118",
            "h4_islandx_terrain_02_119",
            "h4_islandx_terrain_02_12",
            "h4_islandx_terrain_02_120",
            "h4_islandx_terrain_02_121",
            "h4_islandx_terrain_02_122",
            "h4_islandx_terrain_02_123",
            "h4_islandx_terrain_02_124",
            "h4_islandx_terrain_02_13",
            "h4_islandx_terrain_02_14",
            "h4_islandx_terrain_02_15",
            "h4_islandx_terrain_02_16",
            "h4_islandx_terrain_02_17",
            "h4_islandx_terrain_02_18",
            "h4_islandx_terrain_02_19",
            "h4_islandx_terrain_02_2",
            "h4_islandx_terrain_02_20",
            "h4_islandx_terrain_02_21",
            "h4_islandx_terrain_02_22",
            "h4_islandx_terrain_02_23",
            "h4_islandx_terrain_02_24",
            "h4_islandx_terrain_02_25",
            "h4_islandx_terrain_02_26",
            "h4_islandx_terrain_02_27",
            "h4_islandx_terrain_02_28",
            "h4_islandx_terrain_02_29",
            "h4_islandx_terrain_02_3",
            "h4_islandx_terrain_02_30",
            "h4_islandx_terrain_02_31",
            "h4_islandx_terrain_02_32",
            "h4_islandx_terrain_02_33",
            "h4_islandx_terrain_02_34",
            "h4_islandx_terrain_02_35",
            "h4_islandx_terrain_02_36",
            "h4_islandx_terrain_02_37",
            "h4_islandx_terrain_02_38",
            "h4_islandx_terrain_02_39",
            "h4_islandx_terrain_02_4",
            "h4_islandx_terrain_02_40",
            "h4_islandx_terrain_02_41",
            "h4_islandx_terrain_02_42",
            "h4_islandx_terrain_02_43",
            "h4_islandx_terrain_02_44",
            "h4_islandx_terrain_02_45",
            "h4_islandx_terrain_02_46",
            "h4_islandx_terrain_02_47",
            "h4_islandx_terrain_02_48",
            "h4_islandx_terrain_02_49",
            "h4_islandx_terrain_02_5",
            "h4_islandx_terrain_02_50",
            "h4_islandx_terrain_02_51",
            "h4_islandx_terrain_02_52",
            "h4_islandx_terrain_02_53",
            "h4_islandx_terrain_02_54",
            "h4_islandx_terrain_02_55",
            "h4_islandx_terrain_02_56",
            "h4_islandx_terrain_02_57",
            "h4_islandx_terrain_02_58",
            "h4_islandx_terrain_02_59",
            "h4_islandx_terrain_02_6",
            "h4_islandx_terrain_02_60",
            "h4_islandx_terrain_02_61",
            "h4_islandx_terrain_02_62",
            "h4_islandx_terrain_02_63",
            "h4_islandx_terrain_02_64",
            "h4_islandx_terrain_02_65",
            "h4_islandx_terrain_02_66",
            "h4_islandx_terrain_02_67",
            "h4_islandx_terrain_02_68",
            "h4_islandx_terrain_02_69",
            "h4_islandx_terrain_02_7",
            "h4_islandx_terrain_02_70",
            "h4_islandx_terrain_02_71",
            "h4_islandx_terrain_02_72",
            "h4_islandx_terrain_02_73",
            "h4_islandx_terrain_02_74",
            "h4_islandx_terrain_02_75",
            "h4_islandx_terrain_02_76",
            "h4_islandx_terrain_02_77",
            "h4_islandx_terrain_02_78",
            "h4_islandx_terrain_02_79",
            "h4_islandx_terrain_02_8",
            "h4_islandx_terrain_02_80",
            "h4_islandx_terrain_02_81",
            "h4_islandx_terrain_02_82",
            "h4_islandx_terrain_02_83",
            "h4_islandx_terrain_02_84",
            "h4_islandx_terrain_02_85",
            "h4_islandx_terrain_02_86",
            "h4_islandx_terrain_02_87",
            "h4_islandx_terrain_02_88",
            "h4_islandx_terrain_02_89",
            "h4_islandx_terrain_02_9",
            "h4_islandx_terrain_02_90",
            "h4_islandx_terrain_02_91",
            "h4_islandx_terrain_02_92",
            "h4_islandx_terrain_02_93",
            "h4_islandx_terrain_02_94",
            "h4_islandx_terrain_02_95",
            "h4_islandx_terrain_02_96",
            "h4_islandx_terrain_02_97",
            "h4_islandx_terrain_02_98",
            "h4_islandx_terrain_02_99",
            "h4_islandx_terrain_02_lod",
            "h4_islandx_terrain_02_slod",
            "h4_islandx_terrain_03",
            "h4_islandx_terrain_03_1",
            "h4_islandx_terrain_03_10",
            "h4_islandx_terrain_03_11",
            "h4_islandx_terrain_03_12",
            "h4_islandx_terrain_03_13",
            "h4_islandx_terrain_03_14",
            "h4_islandx_terrain_03_15",
            "h4_islandx_terrain_03_16",
            "h4_islandx_terrain_03_17",
            "h4_islandx_terrain_03_18",
            "h4_islandx_terrain_03_19",
            "h4_islandx_terrain_03_2",
            "h4_islandx_terrain_03_20",
            "h4_islandx_terrain_03_21",
            "h4_islandx_terrain_03_22",
            "h4_islandx_terrain_03_23",
            "h4_islandx_terrain_03_24",
            "h4_islandx_terrain_03_25",
            "h4_islandx_terrain_03_26",
            "h4_islandx_terrain_03_27",
            "h4_islandx_terrain_03_28",
            "h4_islandx_terrain_03_29",
            "h4_islandx_terrain_03_3",
            "h4_islandx_terrain_03_30",
            "h4_islandx_terrain_03_31",
            "h4_islandx_terrain_03_32",
            "h4_islandx_terrain_03_33",
            "h4_islandx_terrain_03_34",
            "h4_islandx_terrain_03_35",
            "h4_islandx_terrain_03_36",
            "h4_islandx_terrain_03_37",
            "h4_islandx_terrain_03_38",
            "h4_islandx_terrain_03_39",
            "h4_islandx_terrain_03_4",
            "h4_islandx_terrain_03_40",
            "h4_islandx_terrain_03_41",
            "h4_islandx_terrain_03_42",
            "h4_islandx_terrain_03_43",
            "h4_islandx_terrain_03_44",
            "h4_islandx_terrain_03_45",
            "h4_islandx_terrain_03_46",
            "h4_islandx_terrain_03_47",
            "h4_islandx_terrain_03_48",
            "h4_islandx_terrain_03_49",
            "h4_islandx_terrain_03_5",
            "h4_islandx_terrain_03_50",
            "h4_islandx_terrain_03_51",
            "h4_islandx_terrain_03_6",
            "h4_islandx_terrain_03_7",
            "h4_islandx_terrain_03_8",
            "h4_islandx_terrain_03_9",
            "h4_islandx_terrain_03_lod",
            "h4_islandx_terrain_04",
            "h4_islandx_terrain_04_1",
            "h4_islandx_terrain_04_10",
            "h4_islandx_terrain_04_11",
            "h4_islandx_terrain_04_12",
            "h4_islandx_terrain_04_13",
            "h4_islandx_terrain_04_14",
            "h4_islandx_terrain_04_15",
            "h4_islandx_terrain_04_16",
            "h4_islandx_terrain_04_17",
            "h4_islandx_terrain_04_18",
            "h4_islandx_terrain_04_19",
            "h4_islandx_terrain_04_2",
            "h4_islandx_terrain_04_20",
            "h4_islandx_terrain_04_21",
            "h4_islandx_terrain_04_22",
            "h4_islandx_terrain_04_23",
            "h4_islandx_terrain_04_24",
            "h4_islandx_terrain_04_25",
            "h4_islandx_terrain_04_26",
            "h4_islandx_terrain_04_27",
            "h4_islandx_terrain_04_28",
            "h4_islandx_terrain_04_29",
            "h4_islandx_terrain_04_3",
            "h4_islandx_terrain_04_30",
            "h4_islandx_terrain_04_31",
            "h4_islandx_terrain_04_32",
            "h4_islandx_terrain_04_33",
            "h4_islandx_terrain_04_34",
            "h4_islandx_terrain_04_35",
            "h4_islandx_terrain_04_36",
            "h4_islandx_terrain_04_37",
            "h4_islandx_terrain_04_38",
            "h4_islandx_terrain_04_39",
            "h4_islandx_terrain_04_4",
            "h4_islandx_terrain_04_40",
            "h4_islandx_terrain_04_41",
            "h4_islandx_terrain_04_42",
            "h4_islandx_terrain_04_43",
            "h4_islandx_terrain_04_44",
            "h4_islandx_terrain_04_45",
            "h4_islandx_terrain_04_46",
            "h4_islandx_terrain_04_47",
            "h4_islandx_terrain_04_48",
            "h4_islandx_terrain_04_49",
            "h4_islandx_terrain_04_5",
            "h4_islandx_terrain_04_50",
            "h4_islandx_terrain_04_51",
            "h4_islandx_terrain_04_52",
            "h4_islandx_terrain_04_53",
            "h4_islandx_terrain_04_54",
            "h4_islandx_terrain_04_55",
            "h4_islandx_terrain_04_56",
            "h4_islandx_terrain_04_57",
            "h4_islandx_terrain_04_58",
            "h4_islandx_terrain_04_59",
            "h4_islandx_terrain_04_6",
            "h4_islandx_terrain_04_60",
            "h4_islandx_terrain_04_61",
            "h4_islandx_terrain_04_62",
            "h4_islandx_terrain_04_63",
            "h4_islandx_terrain_04_64",
            "h4_islandx_terrain_04_65",
            "h4_islandx_terrain_04_66",
            "h4_islandx_terrain_04_67",
            "h4_islandx_terrain_04_68",
            "h4_islandx_terrain_04_69",
            "h4_islandx_terrain_04_7",
            "h4_islandx_terrain_04_70",
            "h4_islandx_terrain_04_71",
            "h4_islandx_terrain_04_72",
            "h4_islandx_terrain_04_73",
            "h4_islandx_terrain_04_74",
            "h4_islandx_terrain_04_75",
            "h4_islandx_terrain_04_76",
            "h4_islandx_terrain_04_77",
            "h4_islandx_terrain_04_78",
            "h4_islandx_terrain_04_79",
            "h4_islandx_terrain_04_8",
            "h4_islandx_terrain_04_80",
            "h4_islandx_terrain_04_81",
            "h4_islandx_terrain_04_82",
            "h4_islandx_terrain_04_83",
            "h4_islandx_terrain_04_84",
            "h4_islandx_terrain_04_85",
            "h4_islandx_terrain_04_86",
            "h4_islandx_terrain_04_87",
            "h4_islandx_terrain_04_88",
            "h4_islandx_terrain_04_89",
            "h4_islandx_terrain_04_9",
            "h4_islandx_terrain_04_90",
            "h4_islandx_terrain_04_91",
            "h4_islandx_terrain_04_92",
            "h4_islandx_terrain_04_93",
            "h4_islandx_terrain_04_94",
            "h4_islandx_terrain_04_95",
            "h4_islandx_terrain_04_96",
            "h4_islandx_terrain_04_97",
            "h4_islandx_terrain_04_98",
            "h4_islandx_terrain_04_lod",
            "h4_islandx_terrain_04_slod",
            "h4_islandx_terrain_05",
            "h4_islandx_terrain_05_1",
            "h4_islandx_terrain_05_10",
            "h4_islandx_terrain_05_11",
            "h4_islandx_terrain_05_12",
            "h4_islandx_terrain_05_13",
            "h4_islandx_terrain_05_14",
            "h4_islandx_terrain_05_15",
            "h4_islandx_terrain_05_16",
            "h4_islandx_terrain_05_17",
            "h4_islandx_terrain_05_18",
            "h4_islandx_terrain_05_19",
            "h4_islandx_terrain_05_2",
            "h4_islandx_terrain_05_20",
            "h4_islandx_terrain_05_21",
            "h4_islandx_terrain_05_22",
            "h4_islandx_terrain_05_23",
            "h4_islandx_terrain_05_24",
            "h4_islandx_terrain_05_25",
            "h4_islandx_terrain_05_26",
            "h4_islandx_terrain_05_27",
            "h4_islandx_terrain_05_28",
            "h4_islandx_terrain_05_29",
            "h4_islandx_terrain_05_3",
            "h4_islandx_terrain_05_30",
            "h4_islandx_terrain_05_31",
            "h4_islandx_terrain_05_32",
            "h4_islandx_terrain_05_33",
            "h4_islandx_terrain_05_34",
            "h4_islandx_terrain_05_35",
            "h4_islandx_terrain_05_36",
            "h4_islandx_terrain_05_37",
            "h4_islandx_terrain_05_38",
            "h4_islandx_terrain_05_39",
            "h4_islandx_terrain_05_4",
            "h4_islandx_terrain_05_40",
            "h4_islandx_terrain_05_41",
            "h4_islandx_terrain_05_42",
            "h4_islandx_terrain_05_43",
            "h4_islandx_terrain_05_44",
            "h4_islandx_terrain_05_45",
            "h4_islandx_terrain_05_46",
            "h4_islandx_terrain_05_47",
            "h4_islandx_terrain_05_48",
            "h4_islandx_terrain_05_49",
            "h4_islandx_terrain_05_5",
            "h4_islandx_terrain_05_50",
            "h4_islandx_terrain_05_51",
            "h4_islandx_terrain_05_52",
            "h4_islandx_terrain_05_53",
            "h4_islandx_terrain_05_54",
            "h4_islandx_terrain_05_6",
            "h4_islandx_terrain_05_7",
            "h4_islandx_terrain_05_8",
            "h4_islandx_terrain_05_9",
            "h4_islandx_terrain_05_lod",
            "h4_islandx_terrain_05_slod",
            "h4_islandx_terrain_06",
            "h4_islandx_terrain_06_1",
            "h4_islandx_terrain_06_10",
            "h4_islandx_terrain_06_11",
            "h4_islandx_terrain_06_12",
            "h4_islandx_terrain_06_13",
            "h4_islandx_terrain_06_14",
            "h4_islandx_terrain_06_2",
            "h4_islandx_terrain_06_3",
            "h4_islandx_terrain_06_4",
            "h4_islandx_terrain_06_5",
            "h4_islandx_terrain_06_6",
            "h4_islandx_terrain_06_7",
            "h4_islandx_terrain_06_8",
            "h4_islandx_terrain_06_9",
            "h4_islandx_terrain_06_lod",
            "h4_islandx_terrain_06_slod",
            "h4_islandx_terrain_props_05_a",
            "h4_islandx_terrain_props_05_a_lod",
            "h4_islandx_terrain_props_05_b",
            "h4_islandx_terrain_props_05_b_lod",
            "h4_islandx_terrain_props_05_c",
            "h4_islandx_terrain_props_05_c_lod",
            "h4_islandx_terrain_props_05_d",
            "h4_islandx_terrain_props_05_d_1",
            "h4_islandx_terrain_props_05_d_2",
            "h4_islandx_terrain_props_05_d_lod",
            "h4_islandx_terrain_props_05_d_slod",
            "h4_islandx_terrain_props_05_e",
            "h4_islandx_terrain_props_05_e_1",
            "h4_islandx_terrain_props_05_e_2",
            "h4_islandx_terrain_props_05_e_3",
            "h4_islandx_terrain_props_05_e_4",
            "h4_islandx_terrain_props_05_e_5",
            "h4_islandx_terrain_props_05_e_lod",
            "h4_islandx_terrain_props_05_e_slod",
            "h4_islandx_terrain_props_05_f",
            "h4_islandx_terrain_props_05_f_1",
            "h4_islandx_terrain_props_05_f_10",
            "h4_islandx_terrain_props_05_f_11",
            "h4_islandx_terrain_props_05_f_2",
            "h4_islandx_terrain_props_05_f_3",
            "h4_islandx_terrain_props_05_f_4",
            "h4_islandx_terrain_props_05_f_5",
            "h4_islandx_terrain_props_05_f_6",
            "h4_islandx_terrain_props_05_f_7",
            "h4_islandx_terrain_props_05_f_8",
            "h4_islandx_terrain_props_05_f_9",
            "h4_islandx_terrain_props_05_f_lod",
            "h4_islandx_terrain_props_05_f_slod",
            "h4_islandx_terrain_props_06_a",
            "h4_islandx_terrain_props_06_a_1",
            "h4_islandx_terrain_props_06_a_2",
            "h4_islandx_terrain_props_06_a_3",
            "h4_islandx_terrain_props_06_a_4",
            "h4_islandx_terrain_props_06_a_lod",
            "h4_islandx_terrain_props_06_a_slod",
            "h4_islandx_terrain_props_06_b",
            "h4_islandx_terrain_props_06_b_1",
            "h4_islandx_terrain_props_06_b_2",
            "h4_islandx_terrain_props_06_b_lod",
            "h4_islandx_terrain_props_06_b_slod",
            "h4_islandx_terrain_props_06_c",
            "h4_islandx_terrain_props_06_c_1",
            "h4_islandx_terrain_props_06_c_2",
            "h4_islandx_terrain_props_06_c_lod",
            "h4_islandx_terrain_props_06_c_slod",
            "h4_island_padlock_props",
            "h4_mansion_gate_broken",
            "h4_mansion_gate_closed",
            "h4_mansion_remains_cage",
            "h4_mansion_remains_cage_1",
            "h4_mph4_airstrip",
            "h4_mph4_airstrip_interior_0_airstrip_hanger",
            "h4_mph4_beach",
            "h4_mph4_beach_0",
            "h4_mph4_dock",
            "h4_mph4_island",
            "h4_mph4_island_0",
            "h4_mph4_island_lod",
            "h4_mph4_island_long_0",
            "h4_mph4_island_ne_placement",
            "h4_mph4_island_nw_placement",
            "h4_mph4_island_placement",
            "h4_mph4_island_se_placement",
            "h4_mph4_island_strm_0",
            "h4_mph4_island_sw_placement",
            "h4_mph4_mansion",
            "h4_mph4_mansion_b",
            "h4_mph4_mansion_b_strm_0",
            "h4_mph4_mansion_strm_0",
            "h4_mph4_terrain_01",
            "h4_mph4_terrain_01_0",
            "h4_mph4_terrain_01_grass_0",
            "h4_mph4_terrain_01_grass_1",
            "h4_mph4_terrain_01_long_0",
            "h4_mph4_terrain_02",
            "h4_mph4_terrain_02_grass_0",
            "h4_mph4_terrain_02_grass_1",
            "h4_mph4_terrain_02_grass_2",
            "h4_mph4_terrain_02_grass_3",
            "h4_mph4_terrain_03",
            "h4_mph4_terrain_04",
            "h4_mph4_terrain_04_grass_0",
            "h4_mph4_terrain_04_grass_1",
            "h4_mph4_terrain_05",
            "h4_mph4_terrain_05_grass_0",
            "h4_mph4_terrain_06",
            "h4_mph4_terrain_06_grass_0",
            "h4_mph4_terrain_06_strm_0",
            "h4_mph4_terrain_lod",
            "h4_mph4_terrain_occ_00",
            "h4_mph4_terrain_occ_01",
            "h4_mph4_terrain_occ_02",
            "h4_mph4_terrain_occ_03",
            "h4_mph4_terrain_occ_04",
            "h4_mph4_terrain_occ_05",
            "h4_mph4_terrain_occ_06",
            "h4_mph4_terrain_occ_07",
            "h4_mph4_terrain_occ_08",
            "h4_mph4_terrain_occ_09",
            "h4_mph4_wtowers",
            "h4_ne_ipl_00",
            "h4_ne_ipl_00_1",
            "h4_ne_ipl_00_2",
            "h4_ne_ipl_00_3",
            "h4_ne_ipl_00_4",
            "h4_ne_ipl_00_5",
            "h4_ne_ipl_00_6",
            "h4_ne_ipl_00_7",
            "h4_ne_ipl_00_lod",
            "h4_ne_ipl_00_slod",
            "h4_ne_ipl_01",
            "h4_ne_ipl_01_1",
            "h4_ne_ipl_01_10",
            "h4_ne_ipl_01_11",
            "h4_ne_ipl_01_12",
            "h4_ne_ipl_01_13",
            "h4_ne_ipl_01_14",
            "h4_ne_ipl_01_15",
            "h4_ne_ipl_01_16",
            "h4_ne_ipl_01_17",
            "h4_ne_ipl_01_18",
            "h4_ne_ipl_01_2",
            "h4_ne_ipl_01_3",
            "h4_ne_ipl_01_4",
            "h4_ne_ipl_01_5",
            "h4_ne_ipl_01_6",
            "h4_ne_ipl_01_7",
            "h4_ne_ipl_01_8",
            "h4_ne_ipl_01_9",
            "h4_ne_ipl_01_lod",
            "h4_ne_ipl_01_slod",
            "h4_ne_ipl_02",
            "h4_ne_ipl_02_1",
            "h4_ne_ipl_02_10",
            "h4_ne_ipl_02_11",
            "h4_ne_ipl_02_12",
            "h4_ne_ipl_02_13",
            "h4_ne_ipl_02_14",
            "h4_ne_ipl_02_2",
            "h4_ne_ipl_02_3",
            "h4_ne_ipl_02_4",
            "h4_ne_ipl_02_5",
            "h4_ne_ipl_02_6",
            "h4_ne_ipl_02_7",
            "h4_ne_ipl_02_8",
            "h4_ne_ipl_02_9",
            "h4_ne_ipl_02_lod",
            "h4_ne_ipl_02_slod",
            "h4_ne_ipl_03",
            "h4_ne_ipl_03_1",
            "h4_ne_ipl_03_10",
            "h4_ne_ipl_03_2",
            "h4_ne_ipl_03_3",
            "h4_ne_ipl_03_4",
            "h4_ne_ipl_03_5",
            "h4_ne_ipl_03_6",
            "h4_ne_ipl_03_7",
            "h4_ne_ipl_03_8",
            "h4_ne_ipl_03_9",
            "h4_ne_ipl_03_lod",
            "h4_ne_ipl_03_slod",
            "h4_ne_ipl_04",
            "h4_ne_ipl_04_1",
            "h4_ne_ipl_04_10",
            "h4_ne_ipl_04_11",
            "h4_ne_ipl_04_12",
            "h4_ne_ipl_04_13",
            "h4_ne_ipl_04_14",
            "h4_ne_ipl_04_15",
            "h4_ne_ipl_04_2",
            "h4_ne_ipl_04_3",
            "h4_ne_ipl_04_4",
            "h4_ne_ipl_04_5",
            "h4_ne_ipl_04_6",
            "h4_ne_ipl_04_7",
            "h4_ne_ipl_04_8",
            "h4_ne_ipl_04_9",
            "h4_ne_ipl_04_lod",
            "h4_ne_ipl_04_slod",
            "h4_ne_ipl_05",
            "h4_ne_ipl_05_1",
            "h4_ne_ipl_05_10",
            "h4_ne_ipl_05_11",
            "h4_ne_ipl_05_2",
            "h4_ne_ipl_05_3",
            "h4_ne_ipl_05_4",
            "h4_ne_ipl_05_5",
            "h4_ne_ipl_05_6",
            "h4_ne_ipl_05_7",
            "h4_ne_ipl_05_8",
            "h4_ne_ipl_05_9",
            "h4_ne_ipl_05_lod",
            "h4_ne_ipl_05_slod",
            "h4_ne_ipl_06",
            "h4_ne_ipl_06_1",
            "h4_ne_ipl_06_10",
            "h4_ne_ipl_06_11",
            "h4_ne_ipl_06_12",
            "h4_ne_ipl_06_2",
            "h4_ne_ipl_06_3",
            "h4_ne_ipl_06_4",
            "h4_ne_ipl_06_5",
            "h4_ne_ipl_06_6",
            "h4_ne_ipl_06_7",
            "h4_ne_ipl_06_8",
            "h4_ne_ipl_06_9",
            "h4_ne_ipl_06_lod",
            "h4_ne_ipl_06_slod",
            "h4_ne_ipl_07",
            "h4_ne_ipl_07_1",
            "h4_ne_ipl_07_10",
            "h4_ne_ipl_07_11",
            "h4_ne_ipl_07_12",
            "h4_ne_ipl_07_13",
            "h4_ne_ipl_07_14",
            "h4_ne_ipl_07_15",
            "h4_ne_ipl_07_16",
            "h4_ne_ipl_07_17",
            "h4_ne_ipl_07_2",
            "h4_ne_ipl_07_3",
            "h4_ne_ipl_07_4",
            "h4_ne_ipl_07_5",
            "h4_ne_ipl_07_6",
            "h4_ne_ipl_07_7",
            "h4_ne_ipl_07_8",
            "h4_ne_ipl_07_9",
            "h4_ne_ipl_07_lod",
            "h4_ne_ipl_07_slod",
            "h4_ne_ipl_08",
            "h4_ne_ipl_08_1",
            "h4_ne_ipl_08_10",
            "h4_ne_ipl_08_11",
            "h4_ne_ipl_08_12",
            "h4_ne_ipl_08_13",
            "h4_ne_ipl_08_14",
            "h4_ne_ipl_08_15",
            "h4_ne_ipl_08_16",
            "h4_ne_ipl_08_17",
            "h4_ne_ipl_08_2",
            "h4_ne_ipl_08_3",
            "h4_ne_ipl_08_4",
            "h4_ne_ipl_08_5",
            "h4_ne_ipl_08_6",
            "h4_ne_ipl_08_7",
            "h4_ne_ipl_08_8",
            "h4_ne_ipl_08_9",
            "h4_ne_ipl_08_lod",
            "h4_ne_ipl_08_slod",
            "h4_ne_ipl_09",
            "h4_ne_ipl_09_1",
            "h4_ne_ipl_09_2",
            "h4_ne_ipl_09_3",
            "h4_ne_ipl_09_lod",
            "h4_ne_ipl_09_slod",
            "h4_nw_ipl_00",
            "h4_nw_ipl_00_1",
            "h4_nw_ipl_00_2",
            "h4_nw_ipl_00_3",
            "h4_nw_ipl_00_4",
            "h4_nw_ipl_00_lod",
            "h4_nw_ipl_00_slod",
            "h4_nw_ipl_01",
            "h4_nw_ipl_01_1",
            "h4_nw_ipl_01_10",
            "h4_nw_ipl_01_11",
            "h4_nw_ipl_01_2",
            "h4_nw_ipl_01_3",
            "h4_nw_ipl_01_4",
            "h4_nw_ipl_01_5",
            "h4_nw_ipl_01_6",
            "h4_nw_ipl_01_7",
            "h4_nw_ipl_01_8",
            "h4_nw_ipl_01_9",
            "h4_nw_ipl_01_lod",
            "h4_nw_ipl_01_slod",
            "h4_nw_ipl_02",
            "h4_nw_ipl_02_1",
            "h4_nw_ipl_02_2",
            "h4_nw_ipl_02_lod",
            "h4_nw_ipl_02_slod",
            "h4_nw_ipl_03",
            "h4_nw_ipl_03_1",
            "h4_nw_ipl_03_2",
            "h4_nw_ipl_03_3",
            "h4_nw_ipl_03_4",
            "h4_nw_ipl_03_5",
            "h4_nw_ipl_03_6",
            "h4_nw_ipl_03_7",
            "h4_nw_ipl_03_8",
            "h4_nw_ipl_03_lod",
            "h4_nw_ipl_03_slod",
            "h4_nw_ipl_04",
            "h4_nw_ipl_04_1",
            "h4_nw_ipl_04_10",
            "h4_nw_ipl_04_2",
            "h4_nw_ipl_04_3",
            "h4_nw_ipl_04_4",
            "h4_nw_ipl_04_5",
            "h4_nw_ipl_04_6",
            "h4_nw_ipl_04_7",
            "h4_nw_ipl_04_8",
            "h4_nw_ipl_04_9",
            "h4_nw_ipl_04_lod",
            "h4_nw_ipl_04_slod",
            "h4_nw_ipl_05",
            "h4_nw_ipl_05_1",
            "h4_nw_ipl_05_10",
            "h4_nw_ipl_05_11",
            "h4_nw_ipl_05_12",
            "h4_nw_ipl_05_13",
            "h4_nw_ipl_05_14",
            "h4_nw_ipl_05_15",
            "h4_nw_ipl_05_16",
            "h4_nw_ipl_05_17",
            "h4_nw_ipl_05_18",
            "h4_nw_ipl_05_19",
            "h4_nw_ipl_05_2",
            "h4_nw_ipl_05_20",
            "h4_nw_ipl_05_3",
            "h4_nw_ipl_05_4",
            "h4_nw_ipl_05_5",
            "h4_nw_ipl_05_6",
            "h4_nw_ipl_05_7",
            "h4_nw_ipl_05_8",
            "h4_nw_ipl_05_9",
            "h4_nw_ipl_05_lod",
            "h4_nw_ipl_05_slod",
            "h4_nw_ipl_06",
            "h4_nw_ipl_06_1",
            "h4_nw_ipl_06_10",
            "h4_nw_ipl_06_11",
            "h4_nw_ipl_06_12",
            "h4_nw_ipl_06_13",
            "h4_nw_ipl_06_2",
            "h4_nw_ipl_06_3",
            "h4_nw_ipl_06_4",
            "h4_nw_ipl_06_5",
            "h4_nw_ipl_06_6",
            "h4_nw_ipl_06_7",
            "h4_nw_ipl_06_8",
            "h4_nw_ipl_06_9",
            "h4_nw_ipl_06_lod",
            "h4_nw_ipl_06_slod",
            "h4_nw_ipl_07",
            "h4_nw_ipl_07_1",
            "h4_nw_ipl_07_2",
            "h4_nw_ipl_07_3",
            "h4_nw_ipl_07_4",
            "h4_nw_ipl_07_lod",
            "h4_nw_ipl_07_slod",
            "h4_nw_ipl_08",
            "h4_nw_ipl_08_1",
            "h4_nw_ipl_08_10",
            "h4_nw_ipl_08_11",
            "h4_nw_ipl_08_2",
            "h4_nw_ipl_08_3",
            "h4_nw_ipl_08_4",
            "h4_nw_ipl_08_5",
            "h4_nw_ipl_08_6",
            "h4_nw_ipl_08_7",
            "h4_nw_ipl_08_8",
            "h4_nw_ipl_08_9",
            "h4_nw_ipl_08_lod",
            "h4_nw_ipl_08_slod",
            "h4_nw_ipl_09",
            "h4_nw_ipl_09_1",
            "h4_nw_ipl_09_10",
            "h4_nw_ipl_09_2",
            "h4_nw_ipl_09_3",
            "h4_nw_ipl_09_4",
            "h4_nw_ipl_09_5",
            "h4_nw_ipl_09_6",
            "h4_nw_ipl_09_7",
            "h4_nw_ipl_09_8",
            "h4_nw_ipl_09_9",
            "h4_nw_ipl_09_lod",
            "h4_nw_ipl_09_slod",
            "h4_se_ipl_00",
            "h4_se_ipl_00_1",
            "h4_se_ipl_00_2",
            "h4_se_ipl_00_3",
            "h4_se_ipl_00_4",
            "h4_se_ipl_00_5",
            "h4_se_ipl_00_6",
            "h4_se_ipl_00_7",
            "h4_se_ipl_00_8",
            "h4_se_ipl_00_lod",
            "h4_se_ipl_00_slod",
            "h4_se_ipl_01",
            "h4_se_ipl_01_1",
            "h4_se_ipl_01_10",
            "h4_se_ipl_01_11",
            "h4_se_ipl_01_12",
            "h4_se_ipl_01_13",
            "h4_se_ipl_01_14",
            "h4_se_ipl_01_15",
            "h4_se_ipl_01_16",
            "h4_se_ipl_01_17",
            "h4_se_ipl_01_2",
            "h4_se_ipl_01_3",
            "h4_se_ipl_01_4",
            "h4_se_ipl_01_5",
            "h4_se_ipl_01_6",
            "h4_se_ipl_01_7",
            "h4_se_ipl_01_8",
            "h4_se_ipl_01_9",
            "h4_se_ipl_01_lod",
            "h4_se_ipl_01_slod",
            "h4_se_ipl_02",
            "h4_se_ipl_02_1",
            "h4_se_ipl_02_2",
            "h4_se_ipl_02_3",
            "h4_se_ipl_02_4",
            "h4_se_ipl_02_5",
            "h4_se_ipl_02_lod",
            "h4_se_ipl_02_slod",
            "h4_se_ipl_03",
            "h4_se_ipl_03_1",
            "h4_se_ipl_03_2",
            "h4_se_ipl_03_3",
            "h4_se_ipl_03_4",
            "h4_se_ipl_03_5",
            "h4_se_ipl_03_6",
            "h4_se_ipl_03_7",
            "h4_se_ipl_03_8",
            "h4_se_ipl_03_9",
            "h4_se_ipl_03_lod",
            "h4_se_ipl_03_slod",
            "h4_se_ipl_04",
            "h4_se_ipl_04_1",
            "h4_se_ipl_04_2",
            "h4_se_ipl_04_3",
            "h4_se_ipl_04_4",
            "h4_se_ipl_04_5",
            "h4_se_ipl_04_lod",
            "h4_se_ipl_04_slod",
            "h4_se_ipl_05",
            "h4_se_ipl_05_1",
            "h4_se_ipl_05_10",
            "h4_se_ipl_05_11",
            "h4_se_ipl_05_12",
            "h4_se_ipl_05_13",
            "h4_se_ipl_05_14",
            "h4_se_ipl_05_2",
            "h4_se_ipl_05_3",
            "h4_se_ipl_05_4",
            "h4_se_ipl_05_5",
            "h4_se_ipl_05_6",
            "h4_se_ipl_05_7",
            "h4_se_ipl_05_8",
            "h4_se_ipl_05_9",
            "h4_se_ipl_05_lod",
            "h4_se_ipl_05_slod",
            "h4_se_ipl_06",
            "h4_se_ipl_06_1",
            "h4_se_ipl_06_10",
            "h4_se_ipl_06_11",
            "h4_se_ipl_06_12",
            "h4_se_ipl_06_13",
            "h4_se_ipl_06_14",
            "h4_se_ipl_06_15",
            "h4_se_ipl_06_2",
            "h4_se_ipl_06_3",
            "h4_se_ipl_06_4",
            "h4_se_ipl_06_5",
            "h4_se_ipl_06_6",
            "h4_se_ipl_06_7",
            "h4_se_ipl_06_8",
            "h4_se_ipl_06_9",
            "h4_se_ipl_06_lod",
            "h4_se_ipl_06_slod",
            "h4_se_ipl_07",
            "h4_se_ipl_07_1",
            "h4_se_ipl_07_10",
            "h4_se_ipl_07_11",
            "h4_se_ipl_07_12",
            "h4_se_ipl_07_2",
            "h4_se_ipl_07_3",
            "h4_se_ipl_07_4",
            "h4_se_ipl_07_5",
            "h4_se_ipl_07_6",
            "h4_se_ipl_07_7",
            "h4_se_ipl_07_8",
            "h4_se_ipl_07_9",
            "h4_se_ipl_07_lod",
            "h4_se_ipl_07_slod",
            "h4_se_ipl_08",
            "h4_se_ipl_08_1",
            "h4_se_ipl_08_10",
            "h4_se_ipl_08_11",
            "h4_se_ipl_08_12",
            "h4_se_ipl_08_13",
            "h4_se_ipl_08_14",
            "h4_se_ipl_08_15",
            "h4_se_ipl_08_16",
            "h4_se_ipl_08_2",
            "h4_se_ipl_08_3",
            "h4_se_ipl_08_4",
            "h4_se_ipl_08_5",
            "h4_se_ipl_08_6",
            "h4_se_ipl_08_7",
            "h4_se_ipl_08_8",
            "h4_se_ipl_08_9",
            "h4_se_ipl_08_lod",
            "h4_se_ipl_08_slod",
            "h4_se_ipl_09",
            "h4_se_ipl_09_1",
            "h4_se_ipl_09_10",
            "h4_se_ipl_09_11",
            "h4_se_ipl_09_12",
            "h4_se_ipl_09_13",
            "h4_se_ipl_09_2",
            "h4_se_ipl_09_3",
            "h4_se_ipl_09_4",
            "h4_se_ipl_09_5",
            "h4_se_ipl_09_6",
            "h4_se_ipl_09_7",
            "h4_se_ipl_09_8",
            "h4_se_ipl_09_9",
            "h4_se_ipl_09_lod",
            "h4_se_ipl_09_slod",
            "h4_sw_ipl_00",
            "h4_sw_ipl_00_1",
            "h4_sw_ipl_00_10",
            "h4_sw_ipl_00_2",
            "h4_sw_ipl_00_3",
            "h4_sw_ipl_00_4",
            "h4_sw_ipl_00_5",
            "h4_sw_ipl_00_6",
            "h4_sw_ipl_00_7",
            "h4_sw_ipl_00_8",
            "h4_sw_ipl_00_9",
            "h4_sw_ipl_00_lod",
            "h4_sw_ipl_00_slod",
            "h4_sw_ipl_01",
            "h4_sw_ipl_01_1",
            "h4_sw_ipl_01_2",
            "h4_sw_ipl_01_3",
            "h4_sw_ipl_01_4",
            "h4_sw_ipl_01_5",
            "h4_sw_ipl_01_lod",
            "h4_sw_ipl_01_slod",
            "h4_sw_ipl_02",
            "h4_sw_ipl_02_1",
            "h4_sw_ipl_02_10",
            "h4_sw_ipl_02_11",
            "h4_sw_ipl_02_2",
            "h4_sw_ipl_02_3",
            "h4_sw_ipl_02_4",
            "h4_sw_ipl_02_5",
            "h4_sw_ipl_02_6",
            "h4_sw_ipl_02_7",
            "h4_sw_ipl_02_8",
            "h4_sw_ipl_02_9",
            "h4_sw_ipl_02_lod",
            "h4_sw_ipl_02_slod",
            "h4_sw_ipl_03",
            "h4_sw_ipl_03_1",
            "h4_sw_ipl_03_2",
            "h4_sw_ipl_03_3",
            "h4_sw_ipl_03_4",
            "h4_sw_ipl_03_5",
            "h4_sw_ipl_03_6",
            "h4_sw_ipl_03_7",
            "h4_sw_ipl_03_8",
            "h4_sw_ipl_03_lod",
            "h4_sw_ipl_03_slod",
            "h4_sw_ipl_04",
            "h4_sw_ipl_04_1",
            "h4_sw_ipl_04_2",
            "h4_sw_ipl_04_3",
            "h4_sw_ipl_04_4",
            "h4_sw_ipl_04_5",
            "h4_sw_ipl_04_6",
            "h4_sw_ipl_04_7",
            "h4_sw_ipl_04_lod",
            "h4_sw_ipl_04_slod",
            "h4_sw_ipl_05",
            "h4_sw_ipl_05_1",
            "h4_sw_ipl_05_2",
            "h4_sw_ipl_05_3",
            "h4_sw_ipl_05_4",
            "h4_sw_ipl_05_5",
            "h4_sw_ipl_05_6",
            "h4_sw_ipl_05_7",
            "h4_sw_ipl_05_8",
            "h4_sw_ipl_05_lod",
            "h4_sw_ipl_05_slod",
            "h4_sw_ipl_06",
            "h4_sw_ipl_06_1",
            "h4_sw_ipl_06_2",
            "h4_sw_ipl_06_3",
            "h4_sw_ipl_06_4",
            "h4_sw_ipl_06_5",
            "h4_sw_ipl_06_6",
            "h4_sw_ipl_06_7",
            "h4_sw_ipl_06_lod",
            "h4_sw_ipl_06_slod",
            "h4_sw_ipl_07",
            "h4_sw_ipl_07_1",
            "h4_sw_ipl_07_2",
            "h4_sw_ipl_07_3",
            "h4_sw_ipl_07_4",
            "h4_sw_ipl_07_5",
            "h4_sw_ipl_07_lod",
            "h4_sw_ipl_07_slod",
            "h4_sw_ipl_08",
            "h4_sw_ipl_08_1",
            "h4_sw_ipl_08_2",
            "h4_sw_ipl_08_3",
            "h4_sw_ipl_08_4",
            "h4_sw_ipl_08_lod",
            "h4_sw_ipl_08_slod",
            "h4_sw_ipl_09",
            "h4_sw_ipl_09_1",
            "h4_sw_ipl_09_10",
            "h4_sw_ipl_09_11",
            "h4_sw_ipl_09_2",
            "h4_sw_ipl_09_3",
            "h4_sw_ipl_09_4",
            "h4_sw_ipl_09_5",
            "h4_sw_ipl_09_6",
            "h4_sw_ipl_09_7",
            "h4_sw_ipl_09_8",
            "h4_sw_ipl_09_9",
            "h4_sw_ipl_09_lod",
            "h4_sw_ipl_09_slod",
            "h4_underwater_gate_closed",
            "h4_underwater_gate_closed_1",
            "hi@h4_airstrip_hanger",
            "ma@h4_mph4_terrain_03_0",
            "ma@h4_mph4_terrain_03_1",
            "ma@h4_mph4_terrain_03_10",
            "ma@h4_mph4_terrain_03_11",
            "ma@h4_mph4_terrain_03_12",
            "ma@h4_mph4_terrain_03_13",
            "ma@h4_mph4_terrain_03_14",
            "ma@h4_mph4_terrain_03_15",
            "ma@h4_mph4_terrain_03_16",
            "ma@h4_mph4_terrain_03_17",
            "ma@h4_mph4_terrain_03_18",
            "ma@h4_mph4_terrain_03_19",
            "ma@h4_mph4_terrain_03_2",
            "ma@h4_mph4_terrain_03_20",
            "ma@h4_mph4_terrain_03_21",
            "ma@h4_mph4_terrain_03_22",
            "ma@h4_mph4_terrain_03_23",
            "ma@h4_mph4_terrain_03_24",
            "ma@h4_mph4_terrain_03_25",
            "ma@h4_mph4_terrain_03_26",
            "ma@h4_mph4_terrain_03_27",
            "ma@h4_mph4_terrain_03_28",
            "ma@h4_mph4_terrain_03_29",
            "ma@h4_mph4_terrain_03_3",
            "ma@h4_mph4_terrain_03_30",
            "ma@h4_mph4_terrain_03_31",
            "ma@h4_mph4_terrain_03_32",
            "ma@h4_mph4_terrain_03_33",
            "ma@h4_mph4_terrain_03_34",
            "ma@h4_mph4_terrain_03_35",
            "ma@h4_mph4_terrain_03_4",
            "ma@h4_mph4_terrain_03_5",
            "ma@h4_mph4_terrain_03_6",
            "ma@h4_mph4_terrain_03_7",
            "ma@h4_mph4_terrain_03_8",
            "ma@h4_mph4_terrain_03_9",
        };

        // Helper method to check if a file name is a Cayo Perico file
        static bool IsCayoPericoFile(string? name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            // Check exact match in the HashSet
            if (cayoPericoFiles.Contains(name)) return true;

            // Check if name contains any of the Cayo Perico file names
            foreach (var cpFile in cayoPericoFiles)
            {
                if (name.Contains(cpFile)) return true;
            }

            return false;
        }

        bool renderpathbounds = true;
        bool renderpaths = false;
        List<YndFile> renderpathynds = new();

        bool renderwaterquads = true;

        bool rendertraintracks = false;
        List<TrainTrack> rendertraintracklist = new();

        bool rendernavmeshes = false;
        List<YnvFile> rendernavmeshynvs = new();

        bool renderscenariobounds = false;
        bool renderscenarios = false;
        List<YmtFile> renderscenariolist = new();

        bool renderpopzones = false;
        bool renderheightmaps = false;
        bool renderwatermaps = false;

        bool renderaudiozones = false;
        bool renderaudioouterbounds = true;
        List<RelFile> renderaudfilelist = new();
        List<AudioPlacement> renderaudplacementslist = new();

        bool MapViewEnabled = false;
        int MapViewDragX = 0;
        int MapViewDragY = 0;


        bool MouseSelectEnabled = false;
        bool ShowSelectionBounds = true;
        bool SelectByGeometry = true; //select by geometry for more precise selection 
        MapSelection CurMouseHit = new();
        MapSelection LastMouseHit = new();
        MapSelection PrevMouseHit = new();

        bool MouseRayCollisionEnabled = true;
        bool MouseRayCollisionVisible = false;
        SpaceRayIntersectResult MouseRayCollision = new();

        string SelectionModeStr = "Entity";
        MapSelectionMode SelectionMode = MapSelectionMode.Entity;
        MapSelection SelectedItem;
        MapSelection CopiedItem;
        WorldInfoForm InfoForm = null;
        public MapSelection CurrentMapSelection { get { return SelectedItem; } }


        TransformWidget Widget = new();
        TransformWidget GrabbedWidget = null;
        bool ShowWidget = true;


        ProjectForm ProjectForm = null;

        Stack<UndoStep> UndoSteps = new();
        Stack<UndoStep> RedoSteps = new();
        Vector3 UndoStartPosition;
        Quaternion UndoStartRotation;
        Vector3 UndoStartScale;

        WorldSnapMode SnapMode = WorldSnapMode.None;
        WorldSnapMode SnapModePrev = WorldSnapMode.Ground;//also the default snap mode
        float SnapGridSize = 1.0f;


        public bool EditEntityPivot { get; set; } = false;

        SettingsForm SettingsForm = null;

        WorldSearchForm SearchForm = null;

        CutsceneForm CutsceneForm = null;

        InputManager Input = new();



        bool toolspanelexpanded = false;
        int toolspanellastwidth;
        bool toolsPanelResizing = false;
        int toolsPanelResizeStartX = 0;
        int toolsPanelResizeStartLeft = 0;
        int toolsPanelResizeStartRight = 0;

        bool initedOk = false;


        public WorldForm()
        {
            InitializeComponent();

            var originalLighting = new CheckBox
            {
                Text = "Original GTA lighting (restart)",
                AutoSize = true,
                Checked = Settings.Default.UseOriginalLighting,
                Location = new System.Drawing.Point(10,
                    OptionsLightingTabPage.Controls.Cast<Control>().Max(control => control.Bottom) + 12)
            };
            originalLighting.CheckedChanged += (_, _) =>
            {
                Settings.Default.UseOriginalLighting = originalLighting.Checked;
                Settings.Default.Save();
            };
            OptionsLightingTabPage.AutoScroll = true;
            OptionsLightingTabPage.Controls.Add(originalLighting);

            Renderer = new Renderer(this, gameFileCache);
            camera = Renderer.camera;
            timecycle = Renderer.timecycle;
            weather = Renderer.weather;
            clouds = Renderer.clouds;

            CurMouseHit.WorldForm = this;
            LastMouseHit.WorldForm = this;
            PrevMouseHit.WorldForm = this;

            initedOk = Renderer.Init();

            GTAFolder.UpdateEnhancedFormTitle(this);
        }


        private void Init()
        {
            //called from WorldForm_Load

            if (!initedOk)
            {
                Close();
                return;
            }


            MouseWheel += WorldForm_MouseWheel;

            if (!GTAFolder.UpdateGTAFolder(true))
            {
                Close();
                return;
            }

            Widget.Position = new Vector3(1.0f, 10.0f, 100.0f);
            Widget.Rotation = Quaternion.Identity;
            Widget.Scale = Vector3.One;
            Widget.Visible = false;
            Widget.OnPositionChange += Widget_OnPositionChange;
            Widget.OnRotationChange += Widget_OnRotationChange;
            Widget.OnScaleChange += Widget_OnScaleChange;

            ymaplist = YmapsTextBox.Text.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            ViewModeComboBox.SelectedIndex = startupviewmode;
            BoundsStyleComboBox.SelectedIndex = 0; //LoadSettings will handle this

            SelectionModeComboBox.SelectedIndex = 0; //Entity mode
            ShowSelectedExtensionTab(false);

            toolspanellastwidth = ToolsPanel.Width * 2; //default expanded size


            Icons = new List<MapIcon>();
            AddIcon("Google Marker", "icon_google_marker_64x64.png", 64, 64, 11.0f, 40.0f, 1.0f);
            AddIcon("Glokon Marker", "icon_glokon_normal_32x32.png", 32, 32, 11.0f, 32.0f, 1.0f);
            AddIcon("Glokon Debug", "icon_glokon_debug_32x32.png", 32, 32, 11.5f, 32.0f, 1.0f);
            MarkerIcon = Icons[1];
            LocatorIcon = Icons[2];
            foreach (MapIcon icon in Icons)
            {
                MarkerStyleComboBox.Items.Add(icon);
                LocatorStyleComboBox.Items.Add(icon);
            }
            MarkerStyleComboBox.SelectedItem = MarkerIcon; //LoadSettings will handle this
            LocatorStyleComboBox.SelectedItem = LocatorIcon;
            LocatorMarker = new MapMarker();
            LocatorMarker.Icon = LocatorIcon;
            LocatorMarker.IsMovable = true;
            //AddDefaultMarkers(); //some POI to start with

            ShaderParamNames[] texsamplers = RenderableGeometry.GetTextureSamplerList();
            foreach (var texsampler in texsamplers)
            {
                TextureSamplerComboBox.Items.Add(texsampler);
            }
            //TextureSamplerComboBox.SelectedIndex = 0; //LoadSettings will handle this
            //RenderModeComboBox.SelectedIndex = 0; //Default

            WorldMaxLodComboBox.SelectedIndex = 0;//should this be a setting?

            WeatherComboBox.SelectedIndex = 0;//show "<Loading...>" until weather types are loaded

            CameraModeComboBox.SelectedIndex = 0; //"Perspective"

            DlcLevelComboBox.SelectedIndex = 0; //show "<Loading...>" until DLC list is loaded

            UpdateToolbarShortcutsText();


            Input.Init();


            Renderer.Start();
        }



        private MapIcon AddIcon(string name, string filename, int texw, int texh, float centerx, float centery, float scale)
        {
            string filepath = PathUtil.GetFilePath("icons\\" + filename);
            try
            {
                MapIcon mi = new(name, filepath, texw, texh, centerx, centery, scale);
                Icons.Add(mi);
                return mi;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load map icon " + filepath + " for " + name + "!\n\n" + ex.ToString());
            }
            return null;
        }




        public void InitScene(Device device)
        {
            int width = ClientSize.Width;
            int height = ClientSize.Height;

            try
            {
                Renderer.DeviceCreated(device, width, height);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading shaders!\n" + ex.ToString());
                return;
            }

            if (Icons != null)
            {
                foreach (MapIcon icon in Icons)
                {
                    icon.LoadTexture(device, LogError);
                }
            }

            camera.FollowEntity = camEntity;
            camEntity.Position = (startupviewmode!=2) ? prevworldpos : Vector3.Zero;
            camEntity.Orientation = Quaternion.LookAtLH(Vector3.Zero, Vector3.Up, Vector3.ForwardLH);

            if (startupviewmode != 2)
            {
                var rotation = FloatUtil.ParseVector3String(Settings.Default.StartRotation);
                if (float.IsFinite(rotation.X) && float.IsFinite(rotation.Y) && float.IsFinite(rotation.Z))
                {
                    rotation.Y = Math.Clamp(rotation.Y, -1.55f, 1.55f);
                    camera.CurrentRotation = rotation;
                    camera.TargetRotation = rotation;
                }
                var savedOrientation = FloatUtil.ParseVector4String(Settings.Default.StartCameraOrientation);
                var orientation = new Quaternion(savedOrientation.X, savedOrientation.Y, savedOrientation.Z, savedOrientation.W);
                if (float.IsFinite(orientation.LengthSquared()) && orientation.LengthSquared() > 1e-6f)
                {
                    camEntity.Orientation = Quaternion.Normalize(orientation);
                }
                camEntity.OrientationInv = Quaternion.Invert(camEntity.Orientation);
            }

            space.AddPersistentEntity(pedEntity);


            LoadSettings();


            formopen = true;
            new Thread(new ThreadStart(ContentThread)).Start();

            frametimer.Start();
        }
        public void CleanupScene()
        {
            formopen = false;

            Renderer.DeviceDestroyed();

            if (Icons != null)
            {
                foreach (MapIcon icon in Icons)
                {
                    icon.UnloadTexture();
                }
            }

            int count = 0;
            while (running && (count < 5000)) //wait for the content thread to exit gracefully
            {
                Thread.Sleep(1);
                count++;
            }
        }
        public void BuffersResized(int w, int h)
        {
            Renderer.BuffersResized(w, h);
        }
        public void RenderScene(DeviceContext context)
        {
            float elapsed = (float)frametimer.Elapsed.TotalSeconds;
            frametimer.Restart();

            if (pauserendering) return;

            GameFileCache.BeginFrame();

            var renderLock = Renderer.RenderSyncRoot;
            if (!renderLock.TryEnter(50))
            { return; } //couldn't get a lock, try again next time
            try
            {
                // cache frequently accessed properties
                bool isMouseSelectEnabled = MouseSelectEnabled;

                UpdateControlInputs(elapsed);

                space.Update(elapsed);

                if (CutsceneForm != null)
                {
                    CutsceneForm.UpdateAnimation(elapsed);
                }

                Renderer.Update(elapsed, MouseLastPoint.X, MouseLastPoint.Y);



                UpdateWidgets();

                BeginMouseHitTest();




                Renderer.BeginRender(context);

                Renderer.RenderSkyAndClouds();

                Renderer.SelectedDrawable = SelectedItem.Drawable;

                // Set the selected node position to exclude its cube from rendering
                Renderer.shaders.SelectedScenarioNodePosition = SelectedItem.ScenarioNode?.Position
                    ?? SelectedItem.PathNode?.Position;

                if (renderworld)
                {
                    RenderWorld();
                }
                else if (rendermaps)
                {
                    RenderYmaps();
                }
                else
                {
                    RenderSingleItem();
                }

                UpdateMouseHits();

                RenderSelection();

                RenderMoused();

                Renderer.RenderQueued();

                Renderer.RenderBounds(SelectionMode);

                Renderer.RenderSelectionGeometry(SelectionMode);

                Renderer.RenderFinalPass();

                RenderEntityOutlines();

                RenderMarkers();

                RenderWidgets();

                Renderer.EndRender();
            }
            finally
            {
                renderLock.Exit();
            }

            UpdateMarkerSelectionPanelInvoke();
        }
        public bool ConfirmQuit()
        {
            if ((ProjectForm != null) && (ProjectForm.CurrentProjectFile != null))
            {
                if (MessageBox.Show("Are you sure you want to quit CodeWalker?", "Confirm quit", MessageBoxButtons.YesNo) != DialogResult.Yes)
                {
                    return false;
                }
            }
            return true;
        }


        private void UpdateTimeOfDayLabel()
        {
            int v = TimeOfDayTrackBar.Value;
            float fh = v / 60.0f;
            int ih = (int)fh;
            int im = v - (ih * 60);
            if (ih == 24) ih = 0;
            TimeOfDayLabel.Text = string.Format("{0:00}:{1:00}", ih, im);
        }

        private void UpdateControlInputs(float elapsed)
        {
            if (elapsed > 0.1f) elapsed = 0.1f;

            // cache settings
            var s = Settings.Default;
            float moveSpeed = 50.0f;


            Input.Update();

            if (Input.xbenable)
            {
                if (Input.ControllerButtonJustPressed(GamepadButtonFlags.Start))
                {
                    SetControlMode(ControlMode == WorldControlMode.Free ? WorldControlMode.Ped : WorldControlMode.Free);
                }
            }


            if (ControlMode == WorldControlMode.Free || ControlBrushEnabled)
            {
                if (Input.ShiftPressed)
                {
                    moveSpeed *= 5.0f;
                }
                if (Input.CtrlPressed)
                {
                    moveSpeed *= 0.2f;
                }

                Vector3 movevec = Input.KeyboardMoveVec(MapViewEnabled);

                if (Input.xbenable)
                {
                    movevec.X += Input.xblx;
                    if (MapViewEnabled) movevec.Y += Input.xbly;
                    else movevec.Z -= Input.xbly;
                    moveSpeed *= (1.0f + (Math.Min(Math.Max(Input.xblt, 0.0f), 1.0f) * 15.0f)); //boost with left trigger
                    if (Input.ControllerButtonPressed(GamepadButtonFlags.A | GamepadButtonFlags.RightShoulder | GamepadButtonFlags.LeftShoulder))
                    {
                        moveSpeed *= 5.0f;
                    }
                }


                if (MapViewEnabled)
                {
                    movevec *= elapsed * moveSpeed * Math.Min(camera.OrthographicTargetSize * 0.01f, 50.0f);

                    float mapviewscale = 1.0f / camera.Height;
                    float fdx = MapViewDragX * mapviewscale;
                    float fdy = MapViewDragY * mapviewscale;
                    movevec.X -= fdx * camera.OrthographicSize;
                    movevec.Y += fdy * camera.OrthographicSize;

                }
                else
                {
                    //normal movement
                    movevec *= elapsed * moveSpeed * Math.Min(camera.TargetDistance, 20.0f);
                }


                Vector3 movewvec = camera.ViewInvQuaternion.Multiply(movevec);
                camEntity.Position += movewvec;

                MapViewDragX = 0;
                MapViewDragY = 0;




                if (Input.xbenable)
                {
                    camera.ControllerRotate(Input.xbrx, Input.xbry, elapsed);

                    float zoom = 0.0f;
                    float zoomspd = s.XInputZoomSpeed;
                    float zoomamt = zoomspd * elapsed;
                    if (Input.ControllerButtonPressed(GamepadButtonFlags.DPadUp)) zoom += zoomamt;
                    if (Input.ControllerButtonPressed(GamepadButtonFlags.DPadDown)) zoom -= zoomamt;
                    if (MapViewEnabled) zoom -= zoomamt * Input.xbry;

                    camera.ControllerZoom(zoom);

                    bool fire = (Input.xbtrigs.Y > 0);
                    if (fire && !ControlFireToggle)
                    {
                        SpawnTestEntity(true);
                    }
                    ControlFireToggle = fire;

                }

            }
            else
            {
                //"play" mode

                int mcx, mcy, mcw;
                MouseButtons mcb, mcbp;
                bool mlb = false, mrb = false;
                bool mlbjustpressed = false, mrbjustpressed = false;
                lock (MouseControlSyncRoot)
                {
                    mcx = MouseControlX;
                    mcy = MouseControlY;
                    mcw = MouseControlWheel;
                    mcb = MouseControlButtons;
                    mcbp = MouseControlButtonsPrev;
                    mlb = ((mcb & MouseButtons.Left) > 0);
                    mrb = ((mcb & MouseButtons.Right) > 0);
                    mlbjustpressed = mlb && ((mcbp & MouseButtons.Left) == 0);
                    mrbjustpressed = mrb && ((mcbp & MouseButtons.Right) == 0);
                    MouseControlX = 0;
                    MouseControlY = 0;
                    MouseControlWheel = 0;
                    MouseControlButtonsPrev = MouseControlButtons;
                    //MouseControlButtons = MouseButtons.None;
                }


                camera.MouseRotate(mcx, mcy);

                if (Input.xbenable)
                {
                    camera.ControllerRotate(Input.xbrx, Input.xbry, elapsed);
                }



                Vector2 movecontrol = new(Input.xbmainaxes.X, Input.xbmainaxes.Y); //(L stick)
                if (Input.kbmovelft) movecontrol.X -= 1.0f;
                if (Input.kbmovergt) movecontrol.X += 1.0f;
                if (Input.kbmovefwd) movecontrol.Y += 1.0f;
                if (Input.kbmovebck) movecontrol.Y -= 1.0f;
                movecontrol.X = Math.Min(movecontrol.X, 1.0f);
                movecontrol.X = Math.Max(movecontrol.X, -1.0f);
                movecontrol.Y = Math.Min(movecontrol.Y, 1.0f);
                movecontrol.Y = Math.Max(movecontrol.Y, -1.0f);

                Vector3 fwd = camera.ViewDirection;
                Vector3 fwdxy = Vector3.Normalize(new Vector3(fwd.X, fwd.Y, 0));
                Vector3 lftxy = Vector3.Normalize(Vector3.Cross(fwd, Vector3.UnitZ));
                Vector3 move = lftxy * movecontrol.X + fwdxy * movecontrol.Y;
                Vector2 movexy = new(move.X, move.Y);

                movexy *= (1.0f + (Math.Min(Math.Max(Input.xblt, 0.0f), 1.0f) * 15.0f)); //boost with left trigger

                pedEntity.ControlMovement = movexy;
                pedEntity.ControlJump = Input.kbjump || Input.ControllerButtonPressed(GamepadButtonFlags.X);
                pedEntity.ControlBoost = Input.ShiftPressed || Input.ControllerButtonPressed(GamepadButtonFlags.A | GamepadButtonFlags.RightShoulder | GamepadButtonFlags.LeftShoulder);


                //Vector3 pedfwd = pedEntity.Orientation.Multiply(Vector3.UnitZ);






                bool fire = mlb || (Input.xbtrigs.Y > 0);
                if (fire && !ControlFireToggle)
                {
                    SpawnTestEntity(true);
                }
                ControlFireToggle = fire;


            }


        }




        private void RenderWorld()
        {
            //start point for world view mode rendering.
            //also used for the water, paths, collisions, nav mesh, and the project window items.

            renderworldVisibleYmapDict.Clear();


            int hour = worldymaptimefilter ? (int)Renderer.timeofday : -1;
            MetaHash weathertype = worldymapweatherfilter ? ((weather.CurrentWeatherType != null) ? weather.CurrentWeatherType.NameHash : new MetaHash(0)) : new MetaHash(0);

            IEnumerable<Entity>? spaceEnts = null;

            if (renderworld)
            {
                space.GetVisibleYmaps(camera, hour, weathertype, renderworldVisibleYmapDict);

                spaceEnts = space.TemporaryEntities;
            }

            if (ProjectForm != null)
            {
                ProjectForm.GetVisibleYmaps(camera, renderworldVisibleYmapDict);
            }

            if (CutsceneForm != null)
            {
                CutsceneForm.GetVisibleYmaps(camera, renderworldVisibleYmapDict);
            }

            // Filter out ymaps based on location hiding settings
            if (hidenorthyankton || hidecayoperico)
            {
                var toRemove = new List<MetaHash>();
                foreach (var kvp in renderworldVisibleYmapDict)
                {
                    var ymap = kvp.Value;
                    if (ymap?.Name != null)
                    {
                        var name = ymap.Name.ToLowerInvariant();
                        if (hidenorthyankton && name.Contains("prologue"))
                        {
                            toRemove.Add(kvp.Key);
                        }
                        else if (hidecayoperico && IsCayoPericoFile(name))
                        {
                            toRemove.Add(kvp.Key);
                        }
                    }
                }
                foreach (var key in toRemove)
                {
                    renderworldVisibleYmapDict.Remove(key);
                }
            }

            Renderer.RenderWorld(renderworldVisibleYmapDict, spaceEnts);


            foreach (var ymap in Renderer.VisibleYmaps)
            {
                UpdateMouseHits(ymap);
            }



            if (renderwaterquads || (SelectionMode == MapSelectionMode.WaterQuad))
            {
                RenderWorldWaterQuads();
            }
            if (SelectionMode == MapSelectionMode.WaveQuad)
            {
                RenderWorldWaterWaveQuads();
            }
            if (SelectionMode == MapSelectionMode.CalmingQuad)
            {
                RenderWorldWaterCalmingQuads();
            }
            if (rendercollisionmeshes || (SelectionMode == MapSelectionMode.Collision))
            {
                RenderWorldCollisionMeshes();
            }
            if (renderpaths || (SelectionMode == MapSelectionMode.Path))
            {
                RenderWorldPaths();
            }
            if (rendertraintracks || (SelectionMode == MapSelectionMode.TrainTrack))
            {
                RenderWorldTrainTracks();
            }
            if (rendernavmeshes || (SelectionMode == MapSelectionMode.NavMesh))
            {
                RenderWorldNavMeshes();
            }
            if (renderscenarios || (SelectionMode == MapSelectionMode.Scenario))
            {
                RenderWorldScenarios();
            }
            if (renderpopzones || (SelectionMode == MapSelectionMode.PopZone))
            {
                RenderWorldPopZones();
            }
            if (renderheightmaps || (SelectionMode == MapSelectionMode.Heightmap))
            {
                RenderWorldHeightmaps();
            }
            if (renderwatermaps || (SelectionMode == MapSelectionMode.Watermap))
            {
                RenderWorldWatermaps();
            }
            if (renderaudiozones || (SelectionMode == MapSelectionMode.Audio))
            {
                RenderWorldAudioZones();
            }

        }

        private void RenderWorldCollisionMeshes()
        {
            //enqueue collision meshes for rendering - from the world grid

            collisionitems.Clear();
            space.GetVisibleBounds(camera, collisionmeshrange, collisionmeshlayers, collisionitems);

            collisionybns.Clear();
            foreach (var item in collisionitems)
            {
                YbnFile ybn = gameFileCache.GetYbn(item.Name);
                if ((ybn != null) && (ybn.Loaded))
                {
                    collisionybns.Add(ybn);
                }
            }

            collisioninteriors.Clear();
            foreach (var mlo in Renderer.VisibleMlos)
            {
                if (mlo.Archetype == null) return;
                var hash = mlo.Archetype.Hash;
                YbnFile ybn = gameFileCache.GetYbn(hash);
                if ((ybn != null) && (ybn.Loaded))
                {
                    collisioninteriors[mlo] = ybn;
                }
            }


            if (ProjectForm != null)
            {
                ProjectForm.GetVisibleYbns(camera, collisionybns, collisioninteriors);
            }

            // Filter out ybns based on location hiding settings
            if (hidenorthyankton || hidecayoperico)
            {
                collisionybns.RemoveAll(ybn =>
                {
                    if (ybn?.Name != null)
                    {
                        var name = ybn.Name.ToLowerInvariant();
                        if (hidenorthyankton && name.Contains("prologue"))
                        {
                            return true;
                        }
                        if (hidecayoperico && IsCayoPericoFile(name))
                        {
                            return true;
                        }
                    }
                    return false;
                });
            }

            foreach (var ybn in collisionybns)
            {
                if ((ybn != null) && (ybn.Loaded))
                {
                    Renderer.RenderCollisionMesh(ybn.Bounds, null);
                }
            }

            foreach (var kvp in collisioninteriors)
            {
                if ((kvp.Value != null) && (kvp.Value.Loaded))
                {
                    Renderer.RenderCollisionMesh(kvp.Value.Bounds, kvp.Key);
                }
            }

        }

        private void RenderWorldWaterQuads()
        {
            var quads = RenderWorldBaseWaterQuads(water.WaterQuads, MapSelectionMode.WaterQuad);
            Renderer.RenderWaterQuads(quads);
        }

        private void RenderWorldWaterCalmingQuads() => RenderWorldBaseWaterQuads(water.CalmingQuads, MapSelectionMode.CalmingQuad);

        private void RenderWorldWaterWaveQuads() => RenderWorldBaseWaterQuads(water.WaveQuads, MapSelectionMode.WaveQuad);

        private List<T> RenderWorldBaseWaterQuads<T>(IEnumerable<T> quads, MapSelectionMode requiredMode) where T : BaseWaterQuad
        {
            List<T> renderwaterquadlist = water.GetVisibleQuads<T>(camera, quads);

            ProjectForm?.GetVisibleWaterQuads<T>(camera, renderwaterquadlist);

            if(SelectionMode == requiredMode) UpdateMouseHits(renderwaterquadlist);

            return renderwaterquadlist;
        }

        private void RenderWorldPaths()
        {
            renderpathynds.Clear();

            space.GetVisibleYnds(camera, renderpathynds);

            if (ProjectForm != null)
            {
                ProjectForm.GetVisibleYnds(camera, renderpathynds);
            }

            Renderer.RenderPaths(renderpathynds);

            UpdateMouseHits(renderpathynds);
        }

        private void RenderWorldTrainTracks()
        {
            if (!trains.Inited) return;

            rendertraintracklist.Clear();
            rendertraintracklist.AddRange(trains.TrainTracks);

            if (ProjectForm != null)
            {
                ProjectForm.GetVisibleTrainTracks(camera, rendertraintracklist);
            }

            Renderer.RenderTrainTracks(rendertraintracklist);

            UpdateMouseHits(rendertraintracklist);
        }

        private void RenderWorldNavMeshes()
        {

            rendernavmeshynvs.Clear();
            space.GetVisibleYnvs(camera, collisionmeshrange, rendernavmeshynvs);

            if (ProjectForm != null)
            {
                ProjectForm.GetVisibleYnvs(camera, rendernavmeshynvs);
            }

            Renderer.RenderNavMeshes(rendernavmeshynvs);

            UpdateMouseHits(rendernavmeshynvs);


        }

        private void RenderWorldScenarios()
        {
            if (!scenarios.Inited) return;

            renderscenariolist.Clear();
            renderscenariolist.AddRange(scenarios.ScenarioRegions);

            if (ProjectForm != null)
            {
                ProjectForm.GetVisibleScenarios(camera, renderscenariolist);
            }

            Renderer.RenderScenarios(renderscenariolist);

            UpdateMouseHits(renderscenariolist);
        }

        private void RenderWorldPopZones()
        {
            if (!popzones.Inited) return;

            //renderpopzonelist.Clear();
            //renderpopzonelist.AddRange(popzones.Groups.Values);

            if (ProjectForm != null)
            {
                //ProjectForm.GetVisiblePopZones(camera, renderpopzonelist);
            }

            Renderer.RenderPopZones(popzones);
        }

        private void RenderWorldHeightmaps()
        {
            if (!heightmaps.Inited) return;

            //renderheightmaplist.Clear();
            //renderheightmaplist.AddRange(heightmaps.Heightmaps);

            if (ProjectForm != null)
            {
                //ProjectForm.GetVisibleHeightmaps(camera, renderheightmaplist);
            }

            Renderer.RenderBasePath(heightmaps);
        }

        private void RenderWorldWatermaps()
        {
            if (!watermaps.Inited) return;

            //renderwatermaplist.Clear();
            //renderwatermaplist.AddRange(watermaps.Watermaps);

            if (ProjectForm != null)
            {
                //ProjectForm.GetVisibleWatermaps(camera, renderwatermaplist);
            }

            Renderer.RenderBasePath(watermaps);
        }

        private void RenderWorldAudioZones()
        {
            if (!audiozones.Inited) return;

            renderaudfilelist.Clear();
            renderaudfilelist.AddRange(GameFileCache.AudioDatRelFiles);

            if (ProjectForm != null)
            {
                ProjectForm.GetVisibleAudioFiles(camera, renderaudfilelist);
            }

            renderaudplacementslist.Clear();
            audiozones.GetPlacements(renderaudfilelist, renderaudplacementslist);



            BoundingBox bbox = new();
            BoundingSphere bsph = new();
            Ray mray = new();
            mray.Position = camera.MouseRay.Position + camera.Position;
            mray.Direction = camera.MouseRay.Direction;
            float hitdist = float.MaxValue;

            MapBox lastHitOuterBox = new();
            MapSphere lastHitOuterSphere = new();
            MapBox mb = new();
            MapSphere ms = new();

            for (int i = 0; i < renderaudplacementslist.Count; i++)
            {
                var placement = renderaudplacementslist[i];
                switch (placement.Shape)
                {
                    case Dat151ZoneShape.Box:
                    case Dat151ZoneShape.Line:

                        mb.CamRelPos = placement.InnerPos - camera.Position;
                        mb.BBMin = placement.InnerMin;
                        mb.BBMax = placement.InnerMax;
                        mb.Orientation = placement.InnerOri;
                        mb.Scale = Vector3.One;
                        Renderer.HilightBoxes.Add(mb);

                        if (renderaudioouterbounds)
                        {
                            mb.CamRelPos = placement.OuterPos - camera.Position;
                            mb.BBMin = placement.OuterMin;
                            mb.BBMax = placement.OuterMax;
                            mb.Orientation = placement.OuterOri;
                            mb.Scale = Vector3.One;
                            Renderer.BoundingBoxes.Add(mb);
                        }

                        Vector3 hbcamrel = (placement.Position - camera.Position);
                        Ray mraytrn = new();
                        mraytrn.Position = placement.OrientationInv.Multiply(camera.MouseRay.Position - hbcamrel);
                        mraytrn.Direction = placement.OrientationInv.Multiply(mray.Direction);
                        bbox.Minimum = placement.HitboxMin;
                        bbox.Maximum = placement.HitboxMax;
                        if (mraytrn.Intersects(ref bbox, out hitdist) && (hitdist < CurMouseHit.HitDist) && (hitdist > 0))
                        {
                            CurMouseHit.Audio = placement;
                            CurMouseHit.HitDist = hitdist;
                            CurMouseHit.CamRel = hbcamrel;
                            CurMouseHit.AABB = bbox;
                            lastHitOuterBox = mb; //highlight the outer box
                        }
                        break;
                    case Dat151ZoneShape.Sphere:

                        if ((placement.InnerPos != Vector3.Zero) && (placement.OuterPos != Vector3.Zero))
                        {
                            ms.CamRelPos = placement.InnerPos - camera.Position;
                            ms.Radius = placement.InnerRadius;
                            Renderer.HilightSpheres.Add(ms);

                            if (renderaudioouterbounds)
                            {
                                ms.CamRelPos = placement.OuterPos - camera.Position;
                                ms.Radius = placement.OuterRadius;
                                Renderer.BoundingSpheres.Add(ms);
                            }

                            bsph.Center = placement.Position;
                            bsph.Radius = placement.HitSphereRad;
                            if (mray.Intersects(ref bsph, out hitdist) && (hitdist < CurMouseHit.HitDist) && (hitdist > 0))
                            {
                                CurMouseHit.Audio = placement;
                                CurMouseHit.HitDist = hitdist;
                                CurMouseHit.CamRel = placement.Position - camera.Position;
                                CurMouseHit.AABB = new BoundingBox(); //no box here
                                CurMouseHit.BSphere = bsph;
                                lastHitOuterSphere = ms; //highlight the outer sphere
                            }
                        }
                        else
                        { }


                        break;
                    default:
                        break;//shouldn't get here
                }
            }

            if (CurMouseHit.Audio != null)
            {
                //hilight the outer bounds of moused item
                switch (CurMouseHit.Audio.Shape)
                {
                    case Dat151ZoneShape.Box:
                    case Dat151ZoneShape.Line:
                        Renderer.HilightBoxes.Add(lastHitOuterBox);
                        break;
                    case Dat151ZoneShape.Sphere:
                        Renderer.HilightSpheres.Add(lastHitOuterSphere);
                        break;
                }
            }


        }



        private void RenderSingleItem()
        {
            //start point for model view mode rendering

            uint hash = 0;// JenkHash.GenHash(modelname);
            if (!uint.TryParse(modelname, out hash)) //try use a hash directly
            {
                hash = JenkHash.GenHash(modelname);
            }
            Archetype arche = gameFileCache.GetArchetype(hash);

            Archetype? selarch = null;
            DrawableBase? seldrwbl = null;
            YmapEntityDef? selent = null;

            if (arche != null)
            {
                Renderer.RenderArchetype(arche, null);

                selarch = arche;
            }
            else
            {
                YmapFile ymap = gameFileCache.GetYmap(hash);
                if (ymap != null)
                {
                    Renderer.RenderYmap(ymap);
                }
                else
                {
                    //not a ymap... see if it's a ydr or yft
                    YdrFile ydr = gameFileCache.GetYdr(hash);
                    if (ydr != null)
                    {
                        if (ydr.Loaded)
                        {
                            Renderer.RenderDrawable(ydr.Drawable, null, null, hash);

                            seldrwbl = ydr.Drawable;
                        }
                    }
                    else
                    {
                        YftFile yft = gameFileCache.GetYft(hash);
                        if (yft != null)
                        {
                            if (yft.Loaded)
                            {
                                if (yft.Fragment != null)
                                {
                                    var f = yft.Fragment;

                                    Renderer.RenderFragment(null, null, f, hash);

                                    seldrwbl = f.Drawable;
                                }
                            }
                        }
                        else
                        {
                            //TODO: collision bounds single model...
                            //YbnFile ybn = gameFileCache.GetYbn(hash);
                        }
                    }

                }
            }

            if ((selarch != null) && (seldrwbl == null))
            {
                seldrwbl = gameFileCache.TryGetDrawable(selarch);
            }

            //select this item for viewing by the UI...
            if ((SelectedItem.Archetype != selarch) || (SelectedItem.Drawable != seldrwbl) || (SelectedItem.EntityDef != selent))
            {
                SelectedItem.Clear();
                SelectedItem.Archetype = selarch;
                SelectedItem.Drawable = seldrwbl;
                SelectedItem.EntityDef = selent;
                UpdateSelectionUI(false);
            }

        }


        private void RenderYmaps()
        {
            //start point for ymap view mode rendering

            foreach (string lod in ymaplist)
            {
                uint hash = JenkHash.GenHash(lod);
                YmapFile ymap = gameFileCache.GetYmap(hash); //explicitly named ymaps, active or not
                Renderer.RenderYmap(ymap);

                UpdateMouseHits(ymap);
            }
        }






        private void RenderMoused()
        {
            //immediately render the bounding box of the currently moused entity.

            if (!MouseSelectEnabled)
            { return; }

            PrevMouseHit = LastMouseHit;
            LastMouseHit = CurMouseHit;

            bool change = LastMouseHit.CheckForChanges(PrevMouseHit); //(LastMouseHit.EntityDef != PrevMouseHit.EntityDef);
            if (SelectByGeometry)
            {
                change = change || (LastMouseHit.Geometry != PrevMouseHit.Geometry);
            }

            if (change)
            {
                string text = LastMouseHit.GetFullNameString(string.Empty);
                UpdateMousedLabel(text);
            }

            if(!CurMouseHit.HasHit)
            { return; }


            BoundsShaderMode mode = BoundsShaderMode.Box;
            float bsphrad = CurMouseHit.BSphere.Radius;
            Vector3 bbmin = CurMouseHit.AABB.Minimum;
            Vector3 bbmax = CurMouseHit.AABB.Maximum;
            Vector3 camrel = CurMouseHit.CamRel;
            Vector3 scale = Vector3.One;
            Quaternion ori = Quaternion.Identity;
            bool ext = (CurMouseHit.ArchetypeExtension != null) || (CurMouseHit.EntityExtension != null) || (CurMouseHit.CollisionBounds != null);
            if (CurMouseHit.EntityDef != null)
            {
                scale = ext ? Vector3.One : CurMouseHit.EntityDef.Scale;
                ori = CurMouseHit.EntityDef.Orientation;
            }
            if (CurMouseHit.Archetype != null)
            {
                bbmin = CurMouseHit.Archetype.BBMin;
                bbmax = CurMouseHit.Archetype.BBMax;
            }
            if ((CurMouseHit.Geometry != null) || ext)
            {
                bbmin = CurMouseHit.AABB.Minimum; //override archetype AABB..
                bbmax = CurMouseHit.AABB.Maximum;
            }
            if (CurMouseHit.CarGenerator != null)
            {
                ori = CurMouseHit.CarGenerator.Orientation;
            }
            if (CurMouseHit.BoxOccluder != null)
            {
                ori = CurMouseHit.BoxOccluder.Orientation;
            }
            if (CurMouseHit.OccludeModelTri != null)
            {
                var otri = CurMouseHit.OccludeModelTri;
                Renderer.RenderSelectionTriangleOutline(otri.Corner1, otri.Corner2, otri.Corner3, 0xFFFFFFFF);
                return;
            }
            if (CurMouseHit.MloEntityDef != null)
            {
                scale = Vector3.One;
            }
            if (CurMouseHit.WaterQuad != null)
            {
            }
            if (CurMouseHit.ScenarioNode != null)
            {
                var sp = CurMouseHit.ScenarioNode.MyPoint ?? CurMouseHit.ScenarioNode.ClusterMyPoint;
                if (sp != null) //orientate the moused box for the correct scenario point direction...
                {
                    ori = sp.Orientation;
                }
            }
            if (CurMouseHit.NavPoint != null)
            {
                ori = CurMouseHit.NavPoint.Orientation;
            }
            if (CurMouseHit.NavPortal != null)
            {
                ori = CurMouseHit.NavPortal.Orientation;
            }
            if (CurMouseHit.NavPoly != null)
            {
                Renderer.RenderSelectionNavPolyOutline(CurMouseHit.NavPoly, 0xFFFFFFFF);
                return;
            }
            if (CurMouseHit.Audio != null)
            {
                ori = CurMouseHit.Audio.Orientation;
                if (CurMouseHit.Audio.Shape == Dat151ZoneShape.Sphere)
                {
                    mode = BoundsShaderMode.Sphere;
                }
            }
            if (CurMouseHit.CollisionVertex != null)
            {
                var vpos = CurMouseHit.CollisionVertex.Position;
                var crpos = camrel + ori.Multiply(vpos);
                var vertexSize = 0.1f;
                Renderer.RenderSelectionCircle(vpos, vertexSize, 0xFFFFFFFF);
            }
            if (CurMouseHit.CollisionPoly != null)
            {
                Renderer.RenderSelectionCollisionPolyOutline(CurMouseHit.CollisionPoly, 0xFFFFFFFF, CurMouseHit.EntityDef);
            }
            if (CurMouseHit.CollisionBounds != null)
            {
                ori = ori * CurMouseHit.BBOrientation;
            }


            // Skip bounding box for entities with drawables - outline shader handles them
            if (CurMouseHit.Drawable != null && CurMouseHit.EntityDef != null)
                return;

            Renderer.RenderMouseHit(mode, ref camrel, ref bbmin, ref bbmax, ref scale, ref ori, bsphrad);
        }

        private void RenderSelection()
        {
            if (SelectedItem.MultipleSelectionItems != null)
            {
                for (int i = 0; i < SelectedItem.MultipleSelectionItems.Length; i++)
                {
                    var item = SelectedItem.MultipleSelectionItems[i];
                    RenderSelection(ref item);
                }
            }
            else
            {
                RenderSelection(ref SelectedItem);
            }
        }
        private void RenderEntityOutlines()
        {
            // Render outline around hovered entity
            if (CurMouseHit.HasHit && CurMouseHit.EntityDef != null && CurMouseHit.Drawable != null)
            {
                var renderable = Renderer.RenderableCache?.GetRenderable(CurMouseHit.Drawable);
                if (renderable != null && renderable.IsLoaded)
                {
                    var scale = CurMouseHit.EntityDef.Scale;
                    var ori = CurMouseHit.EntityDef.Orientation;
                    var camrel = CurMouseHit.CamRel;
                    var colour = new SharpDX.Vector4(1.0f, 1.0f, 1.0f, 0.8f); // white outline for hover
                    Renderer.RenderEntityOutline(renderable, camrel, ori, scale, colour, 3);
                }
            }

            // Render outline around selected entity
            if (SelectedItem.EntityDef != null && SelectedItem.Drawable != null)
            {
                var renderable = Renderer.RenderableCache?.GetRenderable(SelectedItem.Drawable);
                if (renderable != null && renderable.IsLoaded)
                {
                    var scale = SelectedItem.EntityDef.Scale;
                    var ori = SelectedItem.EntityDef.Orientation;
                    var camrel = SelectedItem.CamRel;
                    // Update camrel for current camera position
                    camrel = SelectedItem.EntityDef.Position - camera.Position;
                    var colour = new SharpDX.Vector4(0.0f, 1.0f, 0.5f, 0.9f); // green outline for selection
                    Renderer.RenderEntityOutline(renderable, camrel, ori, scale, colour, 4);
                }
            }

            // Render outlines for multiple selection
            if (SelectedItem.MultipleSelectionItems != null)
            {
                foreach (var item in SelectedItem.MultipleSelectionItems)
                {
                    if (item.EntityDef != null && item.Drawable != null)
                    {
                        var renderable = Renderer.RenderableCache?.GetRenderable(item.Drawable);
                        if (renderable != null && renderable.IsLoaded)
                        {
                            var scale = item.EntityDef.Scale;
                            var ori = item.EntityDef.Orientation;
                            var camrel = item.EntityDef.Position - camera.Position;
                            var colour = new SharpDX.Vector4(0.0f, 1.0f, 0.5f, 0.9f);
                            Renderer.RenderEntityOutline(renderable, camrel, ori, scale, colour, 4);
                        }
                    }
                }
            }
        }

        private void RenderSelection(ref MapSelection selectionItem)
        {
            //immediately render the bounding box of the current selection. also, arrows.

            const uint cred = 0xFF0000FF;
            const uint cgrn = 0xFF00FF00;
            const uint cblu = 0xFFFF0000;
            const uint caqu = 0xFFFFFF00;
            //const uint cyel = 0xFF00FFFF;

            if (ControlBrushEnabled && MouseRayCollision.Hit)
            {
                var arup = MouseRayCollision.Normal.GetPerpVec();
                Renderer.RenderBrushRadiusOutline(MouseRayCollision.Position, MouseRayCollision.Normal, arup, ProjectForm.GetInstanceBrushRadius(), cgrn);
            }
            if (MouseRayCollisionVisible && MouseRayCollision.Hit)
            {
                var arup = MouseRayCollision.Normal.GetPerpVec();
                Renderer.RenderSelectionArrowOutline(MouseRayCollision.Position, MouseRayCollision.Normal, arup, Quaternion.Identity, 1.0f, 0.05f, cgrn);
            }

            if (!ShowSelectionBounds)
            { return; }

            if (!selectionItem.HasValue)
            { return; }



            BoundsShaderMode mode = BoundsShaderMode.Box;
            float bsphrad = selectionItem.BSphere.Radius;
            Vector3 bbmin = selectionItem.AABB.Minimum;
            Vector3 bbmax = selectionItem.AABB.Maximum;
            Vector3 camrel = -camera.Position;
            Vector3 scale = Vector3.One;
            Quaternion ori = Quaternion.Identity;


            var arch = selectionItem.Archetype;
            var ent = selectionItem.EntityDef;
            if (selectionItem.Archetype != null)
            {
                bbmin = selectionItem.Archetype.BBMin;
                bbmax = selectionItem.Archetype.BBMax;
            }
            if (selectionItem.EntityDef != null)
            {
                camrel = ent.Position - camera.Position;
                scale = ent.Scale;
                ori = ent.Orientation;

                if (EditEntityPivot)
                {
                    Renderer.RenderSelectionEntityPivot(ent);
                }
            }
            if (selectionItem.CarGenerator != null)
            {
                var cg = selectionItem.CarGenerator;
                camrel = cg.Position - camera.Position;
                ori = cg.Orientation;
                bbmin = cg.BBMin;
                bbmax = cg.BBMax;
                float arrowlen = cg._CCarGen.perpendicularLength;
                float arrowrad = arrowlen * 0.066f;
                Renderer.RenderSelectionArrowOutline(cg.Position, Vector3.UnitX, Vector3.UnitY, ori, arrowlen, arrowrad, cgrn);

                if (!Renderer.rendercars)//only render selected car if not already rendering cars..
                {
                    Quaternion cgtrn = Quaternion.RotationAxis(Vector3.UnitZ, (float)Math.PI * -0.5f); //car fragments currently need to be rotated 90 deg right...
                    Quaternion cgori = Quaternion.Multiply(ori, cgtrn);

                    Renderer.RenderCar(cg.Position, cgori, cg._CCarGen.carModel, cg._CCarGen.popGroup);
                }
            }
            if (selectionItem.WaveQuad != null)
            {
                var quad = selectionItem.WaveQuad;
                Vector3 quadArrowPos = new(quad.minX + (quad.maxX - quad.minX) * 0.5f, quad.minY + (quad.maxY - quad.minY) * 0.5f, 5);
                Quaternion waveOri = quad.WaveOrientation;
                float arrowlen = quad.Amplitude * 50;
                float arrowrad = arrowlen * 0.066f;
                Renderer.RenderSelectionArrowOutline(quadArrowPos, Vector3.UnitX, Vector3.UnitY, waveOri, arrowlen, arrowrad, cgrn);
            }
            if (selectionItem.LodLight != null)
            {
                Renderer.RenderSelectionLodLight(selectionItem.LodLight);

                if (selectionItem.LodLight.LodLights != null)
                {
                    bbmin = selectionItem.LodLight.LodLights.BBMin;
                    bbmax = selectionItem.LodLight.LodLights.BBMax;
                }

            }
            if (selectionItem.PathNode != null)
            {
                camrel = selectionItem.PathNode.Position - camera.Position;

                // Render sultan vehicle model at the selected path node, oriented toward the first linked node
                Quaternion carOri = Quaternion.Identity;
                var links = selectionItem.PathNode.Links;
                if (links != null && links.Length > 0 && links[0].Node2 != null)
                {
                    var dir = links[0].Node2.Position - selectionItem.PathNode.Position;
                    float heading = (float)Math.Atan2(-dir.X, dir.Y);
                    carOri = Quaternion.RotationAxis(Vector3.UnitZ, heading);
                }
                var carPos = selectionItem.PathNode.Position + new Vector3(0, 0, 0.5f);
                Renderer.RenderCar(carPos, carOri, JenkHash.GenHash("sultan"), 0);
            }
            if (selectionItem.TrainTrackNode != null)
            {
                camrel = selectionItem.TrainTrackNode.Position - camera.Position;
            }
            if (selectionItem.ScenarioNode != null)
            {
                camrel = selectionItem.ScenarioNode.Position - camera.Position;

                var sn = selectionItem.ScenarioNode;

                //render direction arrow for ScenarioPoint
                ori = sn.Orientation;
                float arrowlen = 2.0f;
                float arrowrad = 0.25f;
                Renderer.RenderSelectionArrowOutline(sn.Position, Vector3.UnitY, Vector3.UnitZ, ori, arrowlen, arrowrad, cgrn);

                MCScenarioPoint vpoint = sn.MyPoint ?? sn.ClusterMyPoint;
                if ((vpoint != null) && (vpoint?.Type?.IsVehicle ?? false))
                {
                    var vhash = vpoint.ModelSet?.NameHash ?? 493038497;//"none"
                    if ((vhash == 0) || (vhash == 493038497))
                    {
                        vhash = vpoint.Type?.VehicleModelSetHash ?? 0;
                    }
                    if ((vhash == 0) && (sn.ChainingNode?.Chain?.Edges != null) && (sn.ChainingNode.Chain.Edges.Length > 0))
                    {
                        var fedge = sn.ChainingNode.Chain.Edges[0]; //for chain nodes, show the first node's model...
                        var fnode = fedge?.NodeFrom?.ScenarioNode;
                        if (fnode != null)
                        {
                            vpoint = fnode.MyPoint ?? fnode.ClusterMyPoint;
                            vhash = vpoint.ModelSet?.NameHash ?? 493038497;//"none"
                            if ((vhash == 0) || (vhash == 493038497))
                            {
                                vhash = vpoint.Type?.VehicleModelSetHash ?? 0;
                            }
                        }
                    }

                    Renderer.RenderCar(sn.Position, sn.Orientation, 0, vhash, true);
                }
                else
                {
                    // Render ped model for non-vehicle scenarios
                    Renderer.RenderScenarioNode(sn);
                }

            }
            if (selectionItem.ScenarioEdge != null)
            {
                //render scenario edge arrow
                var se = selectionItem.ScenarioEdge;
                var sn1 = se.NodeFrom;
                var sn2 = se.NodeTo;
                if ((sn1 != null) && (sn2 != null))
                {
                    var dirp = sn2.Position - sn1.Position;
                    float dl = dirp.Length();
                    Vector3 dir = dirp * (1.0f / dl);
                    Vector3 dup = Vector3.UnitZ;
                    var aori = Quaternion.Invert(Quaternion.RotationLookAtRH(dir, dup));
                    float arrowrad = 0.25f;
                    float arrowlen = Math.Max(dl - arrowrad*5.0f, 0);
                    Renderer.RenderSelectionArrowOutline(sn1.Position, -Vector3.UnitZ, Vector3.UnitY, aori, arrowlen, arrowrad, cblu);
                }
            }
            if (selectionItem.MloEntityDef != null)
            {
                bbmin = selectionItem.AABB.Minimum;
                bbmax = selectionItem.AABB.Maximum;
                var mlo = selectionItem.MloEntityDef;
                var mlop = mlo.Position;
                var mloa = mlo.Archetype as MloArchetype;
                if (mloa != null)
                {
                    VertexTypePC p1 = new();
                    VertexTypePC p2 = new();
                    if (mloa.portals != null)
                    {
                        for (int ip = 0; ip < mloa.portals.Length; ip++)
                        {
                            var portal = mloa.portals[ip];
                            if (portal.Corners == null) continue;
                            p2.Colour = caqu;
                            if ((portal._Data.flags & 4) > 0)
                            {
                                p2.Colour = cblu;
                            }
                            var pcl = portal.Corners.Length;
                            for (int ic = 0; ic < pcl; ic++)
                            {
                                var icn = ic + 1; if (icn >= pcl) icn = 0;
                                p1.Colour = (ic == 0) ? cred : p2.Colour;//highlight index 0 and winding direction
                                p1.Position = mlop + mlo.Orientation.Multiply(portal.Corners[ic].XYZ());
                                p2.Position = mlop + mlo.Orientation.Multiply(portal.Corners[icn].XYZ());
                                Renderer.SelectionLineVerts.Add(p1);
                                Renderer.SelectionLineVerts.Add(p2);
                            }
                        }
                    }
                    if (mloa.rooms != null)
                    {
                        MapBox wbox = new();
                        wbox.Scale = Vector3.One;
                        for (int ir = 0; ir < mloa.rooms.Length; ir++)
                        {
                            var room = mloa.rooms[ir];

                            wbox.CamRelPos = mlop - camera.Position;
                            wbox.BBMin = room._Data.bbMin;// + offset;
                            wbox.BBMax = room._Data.bbMax;// + offset;
                            wbox.Orientation = mlo.Orientation;
                            if ((ir == 0) || (room.RoomName == "limbo"))
                            {
                                bbmin = room._Data.bbMin;
                                bbmax = room._Data.bbMax;
                                //////Renderer.BoundingBoxes.Add(wbox);
                            }
                            else
                            {
                                wbox.BBMin = room.BBMin_CW; //hack method to use CW calculated room AABBs, 
                                wbox.BBMax = room.BBMax_CW; //R* ones are right size, but wrong position??
                                Renderer.WhiteBoxes.Add(wbox);
                            }
                        }
                    }
                }
            }
            if (selectionItem.MloRoomDef != null)
            {
                camrel += ori.Multiply(selectionItem.BBOffset);
                ori = ori * selectionItem.BBOrientation;
                bbmin = selectionItem.MloRoomDef._Data.bbMin;
                bbmax = selectionItem.MloRoomDef._Data.bbMax;   
            }
            if ((selectionItem.ArchetypeExtension != null) || (selectionItem.EntityExtension != null) || (selectionItem.CollisionBounds != null))
            {
                bbmin = selectionItem.AABB.Minimum;
                bbmax = selectionItem.AABB.Maximum;
                scale = Vector3.One;
            }
            if (selectionItem.GrassBatch != null)
            {
                bbmin = selectionItem.GrassBatch.AABBMin;
                bbmax = selectionItem.GrassBatch.AABBMax;
                scale = Vector3.One;
            }
            if (selectionItem.BoxOccluder != null)
            {
                var bo = selectionItem.BoxOccluder;
                camrel = bo.Position - camera.Position;
                ori = bo.Orientation;
                bbmin = bo.BBMin;
                bbmax = bo.BBMax;
            }
            if (selectionItem.OccludeModelTri != null)
            {
                var ot = selectionItem.OccludeModelTri;
                var om = ot.Model;
                bbmin = om._OccludeModel.bmin;
                bbmax = om._OccludeModel.bmax;
                Renderer.RenderSelectionTriangleOutline(ot.Corner1, ot.Corner2, ot.Corner3, cgrn);
            }
            if (selectionItem.NavPoly != null)
            {
                Renderer.RenderSelectionNavPoly(selectionItem.NavPoly);
                Renderer.RenderSelectionNavPolyOutline(selectionItem.NavPoly, cgrn);
                return;//don't render a selection box for nav poly
            }
            if (selectionItem.NavPoint != null)
            {
                var navp = selectionItem.NavPoint;
                camrel = navp.Position - camera.Position;

                //render direction arrow for NavPoint
                ori = navp.Orientation;
                float arrowlen = 2.0f;
                float arrowrad = 0.25f;
                Renderer.RenderSelectionArrowOutline(navp.Position, -Vector3.UnitY, Vector3.UnitZ, ori, arrowlen, arrowrad, cgrn);
            }
            if (selectionItem.NavPortal != null)
            {
                var navp = selectionItem.NavPortal;
                camrel = navp.Position - camera.Position;

                //render direction arrow for NavPortal
                ori = navp.Orientation;
                float arrowlen = 2.0f;
                float arrowrad = 0.25f;
                Renderer.RenderSelectionArrowOutline(navp.Position, Vector3.UnitY, Vector3.UnitZ, ori, arrowlen, arrowrad, cgrn);
            }
            if (selectionItem.Audio != null)
            {
                var au = selectionItem.Audio;
                camrel = au.Position - camera.Position;
                ori = au.Orientation;
                bbmin = au.HitboxMin;
                bbmax = au.HitboxMax;

                if (selectionItem.Audio.Shape == Dat151ZoneShape.Sphere)
                {
                    mode = BoundsShaderMode.Sphere;
                    MapSphere wsph = new();
                    wsph.CamRelPos = au.OuterPos - camera.Position;
                    wsph.Radius = au.OuterRadius;
                    Renderer.WhiteSpheres.Add(wsph);
                }
                else
                {
                    MapBox wbox = new();
                    wbox.CamRelPos = au.OuterPos - camera.Position;
                    wbox.BBMin = au.OuterMin;
                    wbox.BBMax = au.OuterMax;
                    wbox.Orientation = au.OuterOri;
                    wbox.Scale = scale;
                    Renderer.WhiteBoxes.Add(wbox);
                }
            }
            if (selectionItem.CollisionVertex != null)
            {
                var vpos = selectionItem.CollisionVertex.Position;
                var crpos = camrel + ori.Multiply(vpos);
                var vertexSize = 0.1f;
                Renderer.RenderSelectionCircle(vpos, vertexSize, cgrn);
            }
            else if (selectionItem.CollisionPoly != null)
            {
                Renderer.RenderSelectionCollisionPolyOutline(selectionItem.CollisionPoly, cgrn, selectionItem.EntityDef);
            }
            if (selectionItem.CollisionBounds != null)
            {
                camrel += ori.Multiply(selectionItem.BBOffset);
                ori = ori * selectionItem.BBOrientation;
            }

            // Skip bounding box/sphere for entities with drawables - outline shader handles them
            if (selectionItem.Drawable != null && selectionItem.EntityDef != null)
                return;

            if (mode == BoundsShaderMode.Box)
            {
                MapBox box = new();
                box.CamRelPos = camrel;
                box.BBMin = bbmin;
                box.BBMax = bbmax;
                box.Orientation = ori;
                box.Scale = scale;
                Renderer.SelectionBoxes.Add(box);
            }
            else if (mode == BoundsShaderMode.Sphere)
            {
                MapSphere sph = new();
                sph.CamRelPos = camrel;
                sph.Radius = bsphrad;
                Renderer.SelectionSpheres.Add(sph);
            }

        }



        private void RenderMarkers()
        {
            //immediately render all the current markers.

            lock (markersyncroot) //should only cause delays if markers moved/updated
            {
                foreach (var marker in Markers)
                {
                    marker.CamRelPos = marker.WorldPos - camera.Position;
                    marker.Distance = marker.CamRelPos.Length();
                    marker.ScreenPos = camera.ViewProjMatrix.MultiplyW(marker.CamRelPos);
                }

                lock (markersortedsyncroot) //stop collisions with mouse testing
                {
                    SortedMarkers.Clear();
                    SortedMarkers.AddRange(Markers);
                    if (RenderLocator)
                    {
                        LocatorMarker.CamRelPos = LocatorMarker.WorldPos - camera.Position;
                        LocatorMarker.Distance = LocatorMarker.CamRelPos.Length();
                        LocatorMarker.ScreenPos = camera.ViewProjMatrix.MultiplyW(LocatorMarker.CamRelPos);
                        SortedMarkers.Add(LocatorMarker);
                    }
                    SortedMarkers.Sort((m1, m2) => m2.Distance.CompareTo(m1.Distance));
                }

                MarkerBatch.Clear();
                MarkerBatch.AddRange(SortedMarkers);
            }

            Renderer.RenderMarkers(MarkerBatch);
        }


        private void RenderWidgets()
        {
            if (!ShowWidget) return;

            Renderer.RenderTransformWidget(Widget);
        }
        private void UpdateWidgets()
        {
            if (!ShowWidget) return;

            Widget.Update(camera);
        }



        private Vector3 GetGroundPoint(Vector3 p)
        {
            float uplimit = 3.0f;
            float downlimit = 20.0f;
            Ray ray = new(p, new Vector3(0, 0, -1.0f));
            ray.Position.Z += 0.1f;
            SpaceRayIntersectResult hit = space.RayIntersect(ray, downlimit);
            if (hit.Hit)
            {
                return hit.Position;
            }
            ray.Position.Z += uplimit;
            hit = space.RayIntersect(ray, downlimit);
            if (hit.Hit)
            {
                return hit.Position;
            }
            return p;
        }
        private Vector3 SnapPosition(Vector3 p)
        {
            Vector3 gpos = (p / SnapGridSize).Round() * SnapGridSize;
            switch (SnapMode)
            {
                case WorldSnapMode.Grid:
                    p = gpos;
                    break;
                case WorldSnapMode.Ground:
                    p = GetGroundPoint(p);
                    break;
                case WorldSnapMode.Hybrid:
                    p = GetGroundPoint(gpos);
                    break;
            }
            return p;
        }


        private void Widget_OnPositionChange(Vector3 newpos, Vector3 oldpos)
        {
            //called during UpdateWidgets()

            newpos = SnapPosition(newpos);

            if (newpos == oldpos) return;

            SelectedItem.SetPosition(newpos, EditEntityPivot);

            SelectedItem.UpdateGraphics(this);

            if (ProjectForm != null)
            {
                ProjectForm.OnWorldSelectionModified(SelectedItem);
            }
        }
        private void Widget_OnRotationChange(Quaternion newrot, Quaternion oldrot)
        {
            //called during UpdateWidgets()
            if (newrot == oldrot) return;

            SelectedItem.SetRotation(newrot, EditEntityPivot);

            SelectedItem.UpdateGraphics(this);

            if (ProjectForm != null)
            {
                ProjectForm.OnWorldSelectionModified(SelectedItem);
            }
        }
        private void Widget_OnScaleChange(Vector3 newscale, Vector3 oldscale)
        {
            //called during UpdateWidgets()
            if (newscale == oldscale) return;

            SelectedItem.SetScale(newscale, EditEntityPivot);

            SelectedItem.UpdateGraphics(this);

            if (ProjectForm != null)
            {
                ProjectForm.OnWorldSelectionModified(SelectedItem);
            }
        }

        public void SetWidgetPosition(Vector3 pos, bool enableUndo = false)
        {
            if (enableUndo)
            {
                SetWidgetMode("Position");
                MarkUndoStart(Widget);
            }

            Widget.Position = pos;

            if (enableUndo)
            {
                MarkUndoEnd(Widget);
            }
        }
        public void SetWidgetRotation(Quaternion q, bool enableUndo = false)
        {
            if (enableUndo)
            {
                SetWidgetMode("Rotation");
                MarkUndoStart(Widget);
            }

            Widget.Rotation = q;

            if (enableUndo)
            {
                MarkUndoEnd(Widget);
            }
        }
        public void SetWidgetScale(Vector3 s, bool enableUndo = false)
        {
            if (enableUndo)
            {
                SetWidgetMode("Scale");
                MarkUndoStart(Widget);
            }

            Widget.Scale = s;

            if (enableUndo)
            {
                MarkUndoEnd(Widget);
            }
        }

        public void ChangeMultiPosition(MapSelection[] items, Vector3 pos, bool editPivot = false)
        {
            if (items == SelectedItem.MultipleSelectionItems)
            {
                SelectedItem.SetPosition(pos, editPivot);
                SetWidgetPosition(pos, true);
            }
        }
        public void ChangeMultiRotation(MapSelection[] items, Quaternion rot, bool editPivot = false)
        {
            if (items == SelectedItem.MultipleSelectionItems)
            {
                SelectedItem.SetRotation(rot, editPivot);
                SetWidgetRotation(rot, true);
            }
        }
        public void ChangeMultiScale(MapSelection[] items, Vector3 scale, bool editPivot = false)
        {
            if (items == SelectedItem.MultipleSelectionItems)
            {
                SelectedItem.SetScale(scale, editPivot);
                SetWidgetScale(scale, true);
            }
        }

        public void UpdatePathYndGraphics(YndFile? ynd, bool fullupdate)
        {
            if (ynd == null)
            {
                return;
            }

            var selection = SelectedItem.PathNode != null
                ? new[] { SelectedItem.PathNode }
                : null;

            if (fullupdate)
            {
                ynd.UpdateAllNodePositions();
                ynd.BuildBVH();

                space.BuildYndData(ynd);
            }
            else
            {
                ynd.UpdateAllNodePositions();
                space.BuildYndVerts(ynd, selection);
            }
            //lock (Renderer.RenderSyncRoot)
            {
                Renderer.Invalidate(ynd);
            }
        }
        public void UpdatePathNodeGraphics(YndNode? pathnode, bool fullupdate)
        {
            if (pathnode == null) return;
            pathnode.Ynd.UpdateBvhForNode(pathnode);
            UpdatePathYndGraphics(pathnode.Ynd, fullupdate);
        }
        public YndNode GetPathNodeFromSpace(ushort areaid, ushort nodeid)
        {
            return space.NodeGrid.GetYndNode(areaid, nodeid);
        }

        public void UpdateCollisionBoundsGraphics(Bounds b)
        {
            lock (Renderer.RenderSyncRoot)
            {
                if (b is BoundBVH bvh)
                {
                    bvh.BuildBVH();
                }
                else if (b is BoundComposite bc)
                {
                    bc.BuildBVH();
                }

                var ib = b;
                while (ib.Parent != null)
                {
                    ib = ib.Parent;
                }

                Renderer.Invalidate(ib);
            }
        }

        public void UpdateNavYnvGraphics(YnvFile ynv, bool fullupdate)
        {
            ynv.UpdateAllNodePositions();
            ynv.UpdateTriangleVertices();
            ynv.BuildBVH();

            //lock (Renderer.RenderSyncRoot)
            {
                Renderer.Invalidate(ynv);
            }
        }
        public void UpdateNavPolyGraphics(YnvPoly? poly, bool fullupdate)
        {
            if (poly == null) return;
            //poly.Ynv.UpdateBvhForPoly(poly);//TODO!
            UpdateNavYnvGraphics(poly.Ynv, fullupdate);
        }
        public void UpdateNavPointGraphics(YnvPoint? point, bool fullupdate)
        {
            if (point == null) return;
            //poly.Ynv.UpdateBvhForPoint(point);//TODO!
            UpdateNavYnvGraphics(point.Ynv, fullupdate);
        }
        public void UpdateNavPortalGraphics(YnvPortal? portal, bool fullupdate)
        {
            if (portal == null) return;
            //poly.Ynv.UpdateBvhForPortal(portal);//TODO!
            UpdateNavYnvGraphics(portal.Ynv, fullupdate);
        }

        public void UpdateTrainTrackGraphics(TrainTrack tt, bool fullupdate)
        {
            tt.BuildVertices();
            tt.BuildBVH();
            //if (fullupdate)
            //{
            //    //space.BuildYndData(ynd);
            //}
            //else
            //{
            //    //space.BuildYndVerts(ynd);
            //}
            //lock (Renderer.RenderSyncRoot)
            {
                Renderer.Invalidate(tt);
            }
        }
        public void UpdateTrainTrackNodeGraphics(TrainTrackNode? node, bool fullupdate)
        {
            if (node == null) return;
            node.Track.UpdateBvhForNode(node);
            UpdateTrainTrackGraphics(node.Track, fullupdate);
        }

        public void UpdateScenarioGraphics(YmtFile ymt, bool fullupdate)
        {
            var scenario = ymt.ScenarioRegion;
            if (scenario == null) return;

            scenario.BuildBVH();

            scenario.BuildVertices();

            //lock (Renderer.RenderSyncRoot)
            {
                Renderer.Invalidate(scenario);
            }
        }

        public void UpdateLodLightGraphics(YmapLODLight lodlight)
        {

            lodlight.LodLights?.BuildBVH();

            //lock (Renderer.RenderSyncRoot)
            {
                Renderer.Invalidate(lodlight);
            }
        }
        public void UpdateBoxOccluderGraphics(YmapBoxOccluder box)
        {
            //lock (Renderer.RenderSyncRoot)
            {
                Renderer.Invalidate(box);
            }
        }
        public void UpdateOccludeModelGraphics(YmapOccludeModel model)
        {
            model.BuildBVH();
            model.BuildVertices();

            //lock (Renderer.RenderSyncRoot)
            {
                Renderer.Invalidate(model);
            }
        }
        public void UpdateGrassBatchGraphics(YmapGrassInstanceBatch grassBatch)
        {
            //lock (Renderer.RenderSyncRoot)
            {
                Renderer.Invalidate(grassBatch);
            }
        }

        public void UpdateAudioPlacementGraphics(RelFile rel)
        {
            audiozones.PlacementsDict.Remove(rel); //should cause a rebuild to add/remove items
        }
        public AudioPlacement GetAudioPlacement(RelFile rel, Dat151RelData reldata)
        {
            var placement = audiozones.FindPlacement(rel, reldata);
            if (placement == null)
            {
                if (reldata is Dat151AmbientZone az) placement = new AudioPlacement(rel, az);
                if (reldata is Dat151AmbientRule ar) placement = new AudioPlacement(rel, ar);
                if (reldata is Dat151StaticEmitter se) placement = new AudioPlacement(rel, se);
            }
            return placement;
        }


        public void SetCameraTransform(Vector3 pos, Quaternion rot)
        {
            camera.FollowEntity.Position = pos;
            camera.FollowEntity.Orientation = rot;
            camera.FollowEntity.OrientationInv = Quaternion.Invert(rot);
            camera.TargetRotation = Vector3.Zero;
            camera.TargetDistance = 0.01f;
        }
        public void SetCameraClipPlanes(float znear, float zfar)
        {
            //sets the camera clip planes to the specified values, for use in eg cutscenes
            camera.ZNear = znear;
            camera.ZFar = zfar;
            camera.UpdateProj = true;
        }
        public void ResetCameraClipPlanes()
        {
            //resets the camera clip planes to the values in the UI controls.
            camera.ZNear = (float)NearClipUpDown.Value;
            camera.ZFar = (float)FarClipUpDown.Value;
            camera.UpdateProj = true;
        }

        public Vector3 GetCameraPosition()
        {
            //currently used by ProjectForm when creating entities
            lock (Renderer.RenderSyncRoot)
            {
                return camera.Position;
            }
        }
        public Vector3 GetCameraViewDir()
        {
            //currently used by ProjectForm when creating entities
            lock (Renderer.RenderSyncRoot)
            {
                return camera.ViewDirection;
            }
        }

        public void SetCameraSensitivity(float sensitivity, float smoothing)
        {
            camera.Sensitivity = sensitivity;
            camera.Smoothness = smoothing;
        }
        public void SetMouseInverted(bool invert)
        {
            MouseInvert = invert;
        }

        public void SetKeyBindings(KeyBindings kb)
        {
            Input.keyBindings = kb.Copy();
            UpdateToolbarShortcutsText();
        }
        private void UpdateToolbarShortcutsText()
        {
            var kb = Input.keyBindings;
            ToolbarSelectButton.ToolTipText = string.Format("Select objects / Exit edit mode ({0}, {1})", kb.ToggleMouseSelect, kb.ExitEditMode);
            ToolbarMoveButton.ToolTipText = string.Format("Move ({0})", kb.EditPosition);
            ToolbarRotateButton.ToolTipText = string.Format("Rotate ({0})", kb.EditRotation);
            ToolbarScaleButton.ToolTipText = string.Format("Scale ({0})", kb.EditScale);
            ShowToolbarCheckBox.Text = string.Format("Show Toolbar ({0})", kb.ToggleToolbar);
        }


        private MapBox GetExtensionBox(Vector3 camrel, MetaWrapper ext)
        {
            MapBox b = new();
            Vector3 pos = Vector3.Zero;
            float size = 0.5f;
            if (ext is MCExtensionDefLightEffect)
            {
                var le = ext as MCExtensionDefLightEffect;
                pos = le.Data.offsetPosition;
            }
            else if (ext is MCExtensionDefSpawnPointOverride)
            {
                var spo = ext as MCExtensionDefSpawnPointOverride;
                pos = spo.Data.offsetPosition;
                size = spo.Data.Radius;
            }
            else if (ext is MCExtensionDefDoor)
            {
                var door = ext as MCExtensionDefDoor;
                pos = door.Data.offsetPosition;
            }
            else if (ext is Mrage__phVerletClothCustomBounds)
            {
                var cb = ext as Mrage__phVerletClothCustomBounds;
                if ((cb.CollisionData != null) && (cb.CollisionData.Length > 0))
                {
                    pos = cb.CollisionData[0].Data.Position;
                }
            }
            else if (ext is MCExtensionDefParticleEffect)
            {
                var pe = ext as MCExtensionDefParticleEffect;
                pos = pe.Data.offsetPosition;
            }
            else if (ext is MCExtensionDefAudioCollisionSettings)
            {
                var acs = ext as MCExtensionDefAudioCollisionSettings;
                pos = acs.Data.offsetPosition;
            }
            else if (ext is MCExtensionDefAudioEmitter)
            {
                var ae = ext as MCExtensionDefAudioEmitter;
                pos = ae.Data.offsetPosition;
            }
            else if (ext is MCExtensionDefSpawnPoint)
            {
                var sp = ext as MCExtensionDefSpawnPoint;
                pos = sp.Data.offsetPosition;
            }
            else if (ext is MCExtensionDefExplosionEffect)
            {
                var ee = ext as MCExtensionDefExplosionEffect;
                pos = ee.Data.offsetPosition;
            }
            else if (ext is MCExtensionDefLadder)
            {
                var ld = ext as MCExtensionDefLadder;
                pos = ld.Data.offsetPosition;
            }
            else if (ext is MCExtensionDefBuoyancy)
            {
                var bu = ext as MCExtensionDefBuoyancy;
                pos = bu.Data.offsetPosition;
            }
            else if (ext is MCExtensionDefExpression)
            {
                var exp = ext as MCExtensionDefExpression;
                pos = exp.Data.offsetPosition;
            }
            else if (ext is MCExtensionDefLightShaft)
            {
                var ls = ext as MCExtensionDefLightShaft;
                pos = ls.Data.offsetPosition;
            }
            else if (ext is MCExtensionDefWindDisturbance)
            {
                var wd = ext as MCExtensionDefWindDisturbance;
                pos = wd.Data.offsetPosition;
            }
            else if (ext is MCExtensionDefProcObject)
            {
                var po = ext as MCExtensionDefProcObject;
                pos = po.Data.offsetPosition;
            }


            b.BBMin = pos - size;
            b.BBMax = pos + size;
            b.CamRelPos = camrel;

            return b;
        }


        private void SpawnTestEntity(bool cameraCenter = false)
        {
            if (!space.Inited) return;

            Vector3 dir = (cameraCenter ? camera.ViewDirection : camera.MouseRay.Direction);
            Vector3 ofs = (cameraCenter ? Vector3.Zero : camera.MouseRay.Position);
            Vector3 pos = ofs + camera.Position + (dir * 1.5f);
            Vector3 vel = dir * 50.0f; //m/s

            var hash = JenkHash.GenHash("prop_alien_egg_01");
            var arch = GameFileCache.GetArchetype(hash);

            if (arch == null) return;

            CEntityDef cent = new();
            cent.archetypeName = hash;
            cent.rotation = new Vector4(0, 0, 0, 1);
            cent.scaleXY = 1.0f;
            cent.scaleZ = 1.0f;
            cent.flags = 1572872;
            cent.parentIndex = -1;
            cent.lodDist = 200.0f;
            cent.lodLevel = rage__eLodType.LODTYPES_DEPTH_ORPHANHD;
            cent.priorityLevel = rage__ePriorityLevel.PRI_REQUIRED;
            cent.ambientOcclusionMultiplier = 255;
            cent.artificialAmbientOcclusion = 255;
            cent.position = pos;

            YmapEntityDef? ent = new(null, 0, ref cent);

            ent.SetArchetype(arch);


            Entity e = new();
            e.Position = pos;
            e.Velocity = vel;
            e.Mass = 10.0f;
            e.Momentum = vel * e.Mass;
            e.EntityDef = ent;
            e.Radius = arch.BSRadius * 0.7f;
            e.EnableCollisions = true;
            e.Enabled = true;
            e.Lifetime = 20.0f;

            lock (Renderer.RenderSyncRoot)
            {
                space.AddTemporaryEntity(e);
            }
        }



        public void SetControlMode(WorldControlMode mode)
        {
            if (InvokeRequired)
            {
                try
                {
                    Invoke(new Action(() => { SetControlMode(mode); }));
                }
                catch
                { }
                return;
            }

            if (mode == ControlMode) return;

            bool wasfree = (ControlMode == WorldControlMode.Free || ControlBrushEnabled);
            bool isfree = (mode == WorldControlMode.Free || ControlBrushEnabled);

            if (isfree && !wasfree)
            {
                camEntity.Position = pedEntity.Position;

                pedEntity.Enabled = false;

                Renderer.timerunning = false;

                camera.SetFollowEntity(camEntity);
                camera.TargetDistance = 1.0f; //default?
                camera.Smoothness = Settings.Default.CameraSmoothing;

                Cursor.Show();
            }
            else if (!isfree && wasfree)
            {
                pedEntity.Position = camEntity.Position;
                pedEntity.Velocity = Vector3.Zero;
                pedEntity.Enabled = true;

                Renderer.timerunning = true;

                camera.SetFollowEntity(pedEntity.CameraEntity);
                camera.TargetDistance = 0.01f; //1cm
                camera.Smoothness = 20.0f;

                //center the mouse in the window
                System.Drawing.Point centerp = new System.Drawing.Point(ClientSize.Width / 2, ClientSize.Height / 2);
                MouseLastPoint = centerp;
                MouseX = centerp.X;
                MouseY = centerp.Y;
                Cursor.Position = PointToScreen(centerp);
                Cursor.Hide();
            }




            ControlMode = mode;

        }






        private void BeginMouseHitTest()
        {
            // reset variables for beginning the mouse hit test
            CurMouseHit.Clear();

            // cache input state
            bool ctrlPressed = Input.CtrlPressed;
            bool canPaintInstances = ProjectForm?.CanPaintInstances() ?? false;
         
            if (ctrlPressed && canPaintInstances)   // get whether or not we can brush from the project form.
            {
                ControlBrushEnabled = true;
                MouseRayCollisionVisible = false;
                MouseRayCollision = GetSpaceMouseRay();
            }
            else
            {
                ControlBrushEnabled = false;
                if (ctrlPressed && MouseRayCollisionEnabled)
                {
                    MouseRayCollisionVisible = true;
                    MouseRayCollision = GetSpaceMouseRay();
                }
                else
                {
                    MouseRayCollisionVisible = false;
                }
            }

            // cache selection mode
            var selectionMode = SelectionMode;
            bool mouseSelectEnabled = MouseSelectEnabled;

            Renderer.RenderedDrawablesListEnable =
                ((selectionMode == MapSelectionMode.Entity) && mouseSelectEnabled) ||
                (selectionMode == MapSelectionMode.EntityExtension) ||
                (selectionMode == MapSelectionMode.ArchetypeExtension);

            Renderer.RenderedBoundCompsListEnable = (selectionMode == MapSelectionMode.Collision);
        }
        
        private SpaceRayIntersectResult _cachedMouseRay;
        private Vector3 _lastMouseRayPosition;
        private Vector3 _lastMouseRayDirection;
        private Vector3 _lastCameraPosition;
        private bool _lastDrawableCollisionEnabled;

        public SpaceRayIntersectResult GetSpaceMouseRay()
        {
            if (!space.Inited || space.BoundsStore == null)
            {
                return new SpaceRayIntersectResult();
            }

            // check if we can use cached result
            var currentMouseRayPos = camera.MouseRay.Position;
            var currentMouseRayDir = camera.MouseRay.Direction;
            var currentCameraPos = camera.Position;
            var drawableCollisionEnabled = Renderer.rendercollisionmeshlayerdrawable;

            if (_cachedMouseRay.Hit &&
                _lastMouseRayPosition == currentMouseRayPos &&
                _lastMouseRayDirection == currentMouseRayDir &&
                _lastCameraPosition == currentCameraPos &&
                _lastDrawableCollisionEnabled == drawableCollisionEnabled)
            {
                return _cachedMouseRay;
            }

            // calculate new ray intersection
            Ray mray = new();
            mray.Position = currentMouseRayPos + currentCameraPos;
            mray.Direction = currentMouseRayDir;

            _cachedMouseRay = space.RayIntersect(mray, float.MaxValue, collisionmeshlayers, drawableCollisionEnabled);
            _lastMouseRayPosition = currentMouseRayPos;
            _lastMouseRayDirection = currentMouseRayDir;
            _lastCameraPosition = currentCameraPos;
            _lastDrawableCollisionEnabled = drawableCollisionEnabled;

            return _cachedMouseRay;
        }

        public SpaceRayIntersectResult Raycast(Ray ray)
        {
            return space.RayIntersect(ray, float.MaxValue, collisionmeshlayers, Renderer.rendercollisionmeshlayerdrawable);
        }

        private void UpdateMouseHits()
        {
            UpdateMouseHitsFromRenderer();
            UpdateMouseHitsFromSpace();
            UpdateMouseHitsFromProject();
        }
        private void UpdateMouseHitsFromRenderer()
        {
            if (!MouseSelectEnabled) return;
            
            var renderedDrawables = Renderer.RenderedDrawables;
            if (renderedDrawables == null || renderedDrawables.Count == 0) return;
            
            // pre calculate camera position
            var cameraPos = camera.Position;
            
            // only sort entities, not all drawables
            var entitiesWithDrawables = new List<(RenderedDrawable rd, float distSq)>();
            var drawablesWithoutEntities = new List<RenderedDrawable>();
            
            foreach (var rd in renderedDrawables)
            {
                if (rd.Entity != null)
                {
                    var distSq = (rd.Entity.Position - cameraPos).LengthSquared();
                    entitiesWithDrawables.Add((rd, distSq));
                }
                else
                {
                    drawablesWithoutEntities.Add(rd);
                }
            }
            
            // sort only entities by distance
            entitiesWithDrawables.Sort((a, b) => a.distSq.CompareTo(b.distSq));
            
            // process sorted entities first
            foreach (var (rd, _) in entitiesWithDrawables)
            {
                UpdateMouseHits(rd.Drawable, rd.Archetype, rd.Entity);
                if (CurMouseHit.HasHit) break;
            }
            
            // process drawables without entities only if no hit found
            if (!CurMouseHit.HasHit)
            {
                foreach (var rd in drawablesWithoutEntities)
                {
                    UpdateMouseHits(rd.Drawable, rd.Archetype, rd.Entity);
                    if (CurMouseHit.HasHit) break;
                }
            }
        }
        private void UpdateMouseHitsFromSpace()
        {
            if (SelectionMode == MapSelectionMode.Collision)
            {
                MouseRayCollision = GetSpaceMouseRay();

                if (MouseRayCollision.Hit)
                {
                    CurMouseHit.UpdateCollisionFromRayHit(ref MouseRayCollision, camera);
                }
            }
        }
        private void UpdateMouseHitsFromProject()
        {
            if (ProjectForm == null) return;

            if (SelectionMode == MapSelectionMode.Collision)
            {

                ProjectForm.GetMouseCollision(camera, ref CurMouseHit);

            }
        }
        private float GetGeometryTriangleIntersection(DrawableGeometry geom, Ray ray, Vector3 scale, Matrix? modelTransform = null)
        {
            // this method attempts to find the closest triangle intersection
            // returns the hit distance, or -1 if no hit
            try
            {
                var vb = geom.VertexBuffer;
                var ib = geom.IndexBuffer;

                if ((vb?.Data1?.VertexBytes == null) || (ib?.Indices == null)) return -1;

                // get vertex stride and position offset
                int stride = vb.VertexStride;
                if (stride <= 0 || stride < 12) return -1; // need at least 12 bytes for position

                var vertices = vb.Data1.VertexBytes;
                var indices = ib.Indices;

                // early bounds check
                if (vertices.Length < stride * 3 || indices.Length < 3) return -1;

                bool hasTransform = modelTransform.HasValue;
                Matrix mtx = modelTransform.GetValueOrDefault(Matrix.Identity);

                float closestHit = float.MaxValue;
                bool hasHit = false;

                int maxTriangles = indices.Length / 3;

                // process triangles
                for (int triIndex = 0; triIndex < maxTriangles; triIndex++)
                {
                    int baseIndex = triIndex * 3;
                    if (baseIndex + 2 >= indices.Length) break;

                    int i1 = indices[baseIndex];
                    int i2 = indices[baseIndex + 1];
                    int i3 = indices[baseIndex + 2];

                    // bounds check
                    int maxVertexIndex = Math.Max(Math.Max(i1, i2), i3);
                    if (maxVertexIndex * stride + 12 > vertices.Length) continue;

                    // extract vertex positions
                    int offset1 = i1 * stride;
                    int offset2 = i2 * stride;
                    int offset3 = i3 * stride;

                    Vector3 v1 = new Vector3(
                        BitConverter.ToSingle(vertices, offset1),
                        BitConverter.ToSingle(vertices, offset1 + 4),
                        BitConverter.ToSingle(vertices, offset1 + 8));

                    Vector3 v2 = new Vector3(
                        BitConverter.ToSingle(vertices, offset2),
                        BitConverter.ToSingle(vertices, offset2 + 4),
                        BitConverter.ToSingle(vertices, offset2 + 8));

                    Vector3 v3 = new Vector3(
                        BitConverter.ToSingle(vertices, offset3),
                        BitConverter.ToSingle(vertices, offset3 + 4),
                        BitConverter.ToSingle(vertices, offset3 + 8));

                    // apply bone/fragment model transform if present
                    if (hasTransform)
                    {
                        v1 = Vector3.TransformCoordinate(v1, mtx);
                        v2 = Vector3.TransformCoordinate(v2, mtx);
                        v3 = Vector3.TransformCoordinate(v3, mtx);
                    }

                    // apply entity scale
                    v1 *= scale;
                    v2 *= scale;
                    v3 *= scale;

                    // ray triangle intersection test
                    float hitDist;
                    if (ray.Intersects(ref v1, ref v2, ref v3, out hitDist))
                    {
                        if (hitDist > 0 && hitDist < closestHit)
                        {
                            closestHit = hitDist;
                            hasHit = true;

                            // Early exit if we found a very close hit
                            if (hitDist < 0.1f) break;
                        }
                    }
                }

                return hasHit ? closestHit : -1;
            }
            catch
            {
                // if triangle intersection fails, return -1 to fall back to bounding box
                return -1;
            }
        }

        private float GetCableLineIntersection(DrawableGeometry geom, Ray ray, Vector3 scale, Matrix? modelTransform = null, float cableRadius = 0.05f)
        {
            // Ray-line segment proximity test for cable geometries (LineList topology)
            // Returns the ray hit distance if the ray passes within cableRadius of any line segment, or -1
            try
            {
                var vb = geom.VertexBuffer;
                var ib = geom.IndexBuffer;

                if ((vb?.Data1?.VertexBytes == null) || (ib?.Indices == null)) return -1;

                int stride = vb.VertexStride;
                if (stride <= 0 || stride < 12) return -1;

                var vertices = vb.Data1.VertexBytes;
                var indices = ib.Indices;

                if (vertices.Length < stride * 2 || indices.Length < 2) return -1;

                bool hasTransform = modelTransform.HasValue;
                Matrix mtx = modelTransform.GetValueOrDefault(Matrix.Identity);

                float closestHit = float.MaxValue;
                bool hasHit = false;
                float radiusSq = cableRadius * cableRadius;

                int lineCount = indices.Length / 2;

                for (int li = 0; li < lineCount; li++)
                {
                    int idx0 = indices[li * 2];
                    int idx1 = indices[li * 2 + 1];

                    int maxIdx = Math.Max(idx0, idx1);
                    if (maxIdx * stride + 12 > vertices.Length) continue;

                    int off0 = idx0 * stride;
                    int off1 = idx1 * stride;

                    Vector3 p0 = new Vector3(
                        BitConverter.ToSingle(vertices, off0),
                        BitConverter.ToSingle(vertices, off0 + 4),
                        BitConverter.ToSingle(vertices, off0 + 8));
                    Vector3 p1 = new Vector3(
                        BitConverter.ToSingle(vertices, off1),
                        BitConverter.ToSingle(vertices, off1 + 4),
                        BitConverter.ToSingle(vertices, off1 + 8));

                    if (hasTransform)
                    {
                        p0 = Vector3.TransformCoordinate(p0, mtx);
                        p1 = Vector3.TransformCoordinate(p1, mtx);
                    }

                    p0 *= scale;
                    p1 *= scale;

                    // Compute closest approach between ray and line segment
                    Vector3 u = ray.Direction;
                    Vector3 v = p1 - p0;
                    Vector3 w = ray.Position - p0;

                    float a = Vector3.Dot(u, u);
                    float b = Vector3.Dot(u, v);
                    float c = Vector3.Dot(v, v);
                    float d = Vector3.Dot(u, w);
                    float e = Vector3.Dot(v, w);
                    float denom = a * c - b * b;

                    float sc, tc;
                    if (denom < 1e-8f)
                    {
                        sc = 0;
                        tc = (b > c) ? d / b : e / c;
                    }
                    else
                    {
                        sc = (b * e - c * d) / denom;
                        tc = (a * e - b * d) / denom;
                    }

                    // Clamp tc to [0,1] (segment bounds)
                    tc = Math.Max(0, Math.Min(1, tc));
                    // Recompute sc for clamped tc
                    sc = (b * tc - d) / a;

                    if (sc <= 0) continue; // Behind ray origin

                    Vector3 closestOnRay = ray.Position + u * sc;
                    Vector3 closestOnSeg = p0 + v * tc;
                    float distSq = (closestOnRay - closestOnSeg).LengthSquared();

                    if (distSq < radiusSq && sc < closestHit)
                    {
                        closestHit = sc;
                        hasHit = true;
                    }
                }

                return hasHit ? closestHit : -1;
            }
            catch
            {
                return -1;
            }
        }

        private void UpdateMouseHits(DrawableBase drawable, Archetype arche, YmapEntityDef entity)
        {
            //if ((SelectionMode == MapSelectionMode.Entity) && !MouseSelectEnabled) return; //performance improvement when not selecting entities...
            //test the selected entity/archetype for mouse hit.
            //first test the bounding sphere for mouse hit..
            Quaternion orinv;
            Ray mraytrn;
            float hitdist = 0.0f;
            int geometryIndex = 0;
            DrawableGeometry? geometry = null;
            BoundingBox geometryAABB = new();
            BoundingSphere bsph = new();
            BoundingBox bbox = new();
            BoundingBox gbbox = new();
            Quaternion orientation = Quaternion.Identity;
            Vector3 scale = Vector3.One;
            Vector3 camrel = -camera.Position;
            if (entity != null)
            {
                orientation = entity.Orientation;
                scale = entity.Scale;
                camrel += entity.Position;
            }
            if (arche != null)
            {
                bsph.Center = camrel + orientation.Multiply(arche.BSCenter);//could use entity.BSCenter
                bsph.Radius = arche.BSRadius;
                bbox.Minimum = arche.BBMin * scale;
                bbox.Maximum = arche.BBMax * scale;
            }
            else
            {
                bsph.Center = camrel + drawable.BoundingCenter;
                bsph.Radius = drawable.BoundingSphereRadius;
                bbox.Minimum = drawable.BoundingBoxMin * scale;
                bbox.Maximum = drawable.BoundingBoxMax * scale;
            }
            bool mousespherehit = camera.MouseRay.Intersects(ref bsph);



            if ((SelectionMode == MapSelectionMode.EntityExtension) || (SelectionMode == MapSelectionMode.ArchetypeExtension))
            {
                //transform the mouse ray into the entity space.
                orinv = Quaternion.Invert(orientation);
                mraytrn = new Ray();
                mraytrn.Position = orinv.Multiply(camera.MouseRay.Position-camrel);
                mraytrn.Direction = orinv.Multiply(camera.MouseRay.Direction);

                if (SelectionMode == MapSelectionMode.EntityExtension)
                {
                    if ((entity != null) && (entity.Extensions != null))
                    {
                        for (int i = 0; i < entity.Extensions.Length; i++)
                        {
                            var extension = entity.Extensions[i];
                            MapBox mb = GetExtensionBox(camrel, extension);
                            mb.Orientation = orientation;
                            mb.Scale = Vector3.One;// scale;
                            mb.BBMin *= scale;
                            mb.BBMax *= scale;
                            Renderer.BoundingBoxes.Add(mb);

                            bbox.Minimum = mb.BBMin; //TODO: refactor this!
                            bbox.Maximum = mb.BBMax;
                            if (mraytrn.Intersects(ref bbox, out hitdist) && (hitdist < CurMouseHit.HitDist) && (hitdist > 0))
                            {
                                CurMouseHit.EntityDef = entity;
                                CurMouseHit.Archetype = arche;
                                CurMouseHit.EntityExtension = extension;
                                CurMouseHit.HitDist = hitdist;
                                CurMouseHit.CamRel = mb.CamRelPos;
                                CurMouseHit.AABB = bbox;
                            }
                        }
                    }
                    return; //only test extensions when in select extension mode...
                }
                if (SelectionMode == MapSelectionMode.ArchetypeExtension)
                {
                    if ((arche != null) && (arche.Extensions != null))
                    {
                        for (int i = 0; i < arche.Extensions.Length; i++)
                        {
                            var extension = arche.Extensions[i];
                            MapBox mb = GetExtensionBox(camrel, extension);
                            mb.Orientation = orientation;
                            mb.Scale = Vector3.One;// scale;
                            mb.BBMin *= scale;
                            mb.BBMax *= scale;
                            Renderer.BoundingBoxes.Add(mb);

                            bbox.Minimum = mb.BBMin; //TODO: refactor this!
                            bbox.Maximum = mb.BBMax;
                            if (mraytrn.Intersects(ref bbox, out hitdist) && (hitdist < CurMouseHit.HitDist) && (hitdist > 0))
                            {
                                CurMouseHit.EntityDef = entity;
                                CurMouseHit.Archetype = arche;
                                CurMouseHit.ArchetypeExtension = extension;
                                CurMouseHit.HitDist = hitdist;
                                CurMouseHit.CamRel = mb.CamRelPos;
                                CurMouseHit.AABB = bbox;
                            }
                        }
                    }
                    return; //only test extensions when in select extension mode...
                }

            }




            if (!mousespherehit)
            { return; } //no sphere hit, so no entity hit.



            bool usegeomboxes = SelectByGeometry;
            var dmodels = drawable.DrawableModels?.High;
            if (dmodels == null)
            { usegeomboxes = false; }
            if (usegeomboxes)
            {
                for (int i = 0; i < dmodels.Length; i++)
                {
                    var m = dmodels[i];
                    if ((m.BoundsData == null) || (m.Geometries == null))
                    { usegeomboxes = false; break; }
                }
            }



            //transform the mouse ray into the entity space.
            orinv = Quaternion.Invert(orientation);
            mraytrn = new Ray();
            mraytrn.Position = orinv.Multiply(camera.MouseRay.Position-camrel);
            mraytrn.Direction = orinv.Multiply(camera.MouseRay.Direction);
            hitdist = 0.0f;


            if (usegeomboxes)
            {
                //geometry-based selection with triangle intersection
                float ghitdist = float.MaxValue;
                DrawableGeometry? bestGeometry = null;
                BoundingBox bestAABB = new();
                int bestGeomIndex = 0;

                // Get renderable to access per-model bone/fragment transforms
                Rendering.Renderable? rndbl = Renderer.RenderableCache?.GetRenderable(drawable);

                for (int i = 0; i < dmodels.Length; i++)
                {
                    var m = dmodels[i];
                    if ((m.Geometries == null) || (m.BoundsData == null)) continue;

                    // Get the corresponding RenderableModel's transform if available
                    Matrix? modelTransform = null;
                    if (rndbl?.HDModels != null && i < rndbl.HDModels.Length)
                    {
                        var rm = rndbl.HDModels[i];
                        if (rm != null && rm.UseTransform)
                        {
                            modelTransform = rm.Transform;
                        }
                    }

                    // BoundsData may have a leading model-level box (boffset=1) or one box per geometry (boffset=0).
                    // Map geometry j -> BoundsData[j + boffset]; test every geometry box independently (no early break).
                    int geomcount = m.Geometries.Length;
                    int boffset = (m.BoundsData.Length > geomcount) ? 1 : 0;
                    for (int j = 0; j < geomcount; j++)
                    {
                        int bidx = j + boffset;
                        if (bidx >= m.BoundsData.Length) break;
                        var gbox = m.BoundsData[bidx];
                        gbbox.Minimum = gbox.Min.XYZ();
                        gbbox.Maximum = gbox.Max.XYZ();

                        // Transform bounds by model transform for proper bounding box test
                        if (modelTransform.HasValue)
                        {
                            var tmin = Vector3.TransformCoordinate(gbbox.Minimum, modelTransform.Value);
                            var tmax = Vector3.TransformCoordinate(gbbox.Maximum, modelTransform.Value);
                            bbox.Minimum = Vector3.Min(tmin, tmax) * scale;
                            bbox.Maximum = Vector3.Max(tmin, tmax) * scale;
                        }
                        else
                        {
                            bbox.Minimum = gbbox.Minimum * scale;
                            bbox.Maximum = gbbox.Maximum * scale;
                        }

                        // skip geometries whose box isn't under the cursor
                        if (!mraytrn.Intersects(ref bbox, out hitdist)) continue;

                        var geom = m.Geometries[j];
                        bool isTreesLod = (geom?.Shader?.FileName == 4113118754); // trees_lod2.sps - vertices are billboard roots, shader generates geometry
                        if (!isTreesLod && geom?.VertexBuffer?.Data1?.VertexBytes != null && geom?.IndexBuffer?.Indices != null)
                        {
                            // Use cable line intersection for cable.sps, triangle intersection for everything else
                            bool isCable = (geom.Shader?.FileName == 3854885487); // cable.sps
                            float triangleHitDist = isCable
                                ? GetCableLineIntersection(geom, mraytrn, scale, modelTransform)
                                : GetGeometryTriangleIntersection(geom, mraytrn, scale, modelTransform);
                            if (triangleHitDist > 0 && triangleHitDist < ghitdist)
                            {
                                ghitdist = triangleHitDist;
                                bestGeometry = geom;
                                bestAABB = gbbox;
                                bestGeomIndex = j;
                            }
                        }
                        else if (hitdist > 0.0f && hitdist < ghitdist)
                        {
                            // Fallback to bounding box if no vertex data available
                            ghitdist = hitdist;
                            bestGeometry = geom;
                            bestAABB = gbbox;
                            bestGeomIndex = j;
                        }
                    }
                }
                
                if (bestGeometry == null)
                {
                    return; // No geometry hit
                }
                
                geometry = bestGeometry;
                geometryAABB = bestAABB;
                geometryIndex = bestGeomIndex;
                hitdist = ghitdist;
            }
            else
            {
                //archetype/drawable bounding boxes version
                bool outerhit = false;
                if (mraytrn.Intersects(ref bbox, out hitdist)) //test primary box
                {
                    bool firsthit = (CurMouseHit.EntityDef == null);
                    if (firsthit || (hitdist > 0.0f)) //ignore when inside the box..
                    {
                        bool nearer = (hitdist < CurMouseHit.HitDist);  //closer than the last..
                        if (nearer)
                        {
                            outerhit = true; //closer always wins - select the thing in front
                        }
                        else if (Math.Abs(hitdist - CurMouseHit.HitDist) < 0.1f) //near-equal depth: tie-break by size
                        {
                            bool radsm = true;
                            if ((CurMouseHit.Archetype != null) && (arche != null)) //compare hit archetype sizes...
                            {
                                radsm = (arche.BSRadius <= CurMouseHit.Archetype.BSRadius); //prefer selecting smaller things
                            }
                            outerhit = radsm;
                        }
                    }
                }
                if (!outerhit)
                { return; } //no hit.
            }




            // Only update if this is a better hit (closer or more precise)
            bool isBetterHit = false;
            if (hitdist > 0.0f)
            {
                if (CurMouseHit.HitDist <= 0 || hitdist < CurMouseHit.HitDist)
                {
                    isBetterHit = true;
                }
                else if (Math.Abs(hitdist - CurMouseHit.HitDist) < 0.1f) // Similar distance
                {
                    // Prefer geometry-based hits over bounding box hits
                    if (usegeomboxes && geometry != null && CurMouseHit.Geometry == null)
                    {
                        isBetterHit = true;
                    }
                }
            }
            
            if (isBetterHit)
            {
                CurMouseHit.HitDist = hitdist;
                CurMouseHit.EntityDef = entity;
                CurMouseHit.Archetype = arche;
                CurMouseHit.Drawable = drawable;
                CurMouseHit.Geometry = geometry;
                CurMouseHit.AABB = geometryAABB;
                CurMouseHit.GeometryIndex = geometryIndex;
                CurMouseHit.CamRel = camrel;
            }




            //go through geometries...? need to use skeleton?
            //if (drawable.DrawableModelsHigh == null)
            //{ return; }
            //if (drawable.DrawableModelsHigh.data_items == null)
            //{ return; }
            //for (int i = 0; i < drawable.DrawableModelsHigh.data_items.Length; i++)
            //{
            //    var model = drawable.DrawableModelsHigh.data_items[i];
            //    if ((model.Geometries == null) || (model.Geometries.data_items == null))
            //    { continue; }
            //    if ((model.Unknown_18h_Data == null))
            //    { continue; }
            //    int boffset = 0;
            //    if ((model.Unknown_18h_Data.Length > model.Geometries.data_items.Length))
            //    { boffset = 1; }
            //    for (int j = 0; j < model.Geometries.data_items.Length; j++)
            //    {
            //        var geom = model.Geometries.data_items[j];
            //        var gbox = model.Unknown_18h_Data[j + boffset];
            //        bbox.Minimum = gbox.AABB_Max.XYZ();
            //        bbox.Maximum = gbox.AABB_Min.XYZ();
            //        if (mraytrn.Intersects(ref bbox, out hitdist)) //test geom box
            //        {
            //            bool firsthit = (mousehit.EntityDef == null);
            //            if (firsthit || (hitdist > 0.0f)) //ignore when inside the box..
            //            {
            //                bool nearer = (hitdist < mousehit.HitDist);  //closer than the last..
            //                if (nearer)
            //                {
            //                    mousehit.HitDist = (hitdist > 0.0f) ? hitdist : mousehit.HitDist;
            //                    mousehit.EntityDef = entity;
            //                    mousehit.Archetype = arche;
            //                    mousehit.Drawable = drawable;
            //                    mousehit.CamRel = camrel;
            //                }
            //            }
            //        }
            //    }
            //}


            //Bounds b = null;
            //var dd = drawable as Drawable;
            //if (dd != null)
            //{
            //    b = dd.Bound;
            //}
            //else
            //{
            //    var fd = drawable as FragDrawable;
            //    if (fd != null)
            //    {
            //        b = fd.Bound;
            //    }
            //}
            //if (b == null)
            //{ return; }
            //else
            //{ }


        }
        private void UpdateMouseHits(YmapFile ymap)
        {
            //find mouse hits for things like MLOs, time cycle mods, grass batches, and car generators in ymaps.

            BoundingBox bbox = new();
            Ray mray = new();
            mray.Position = camera.MouseRay.Position + camera.Position;
            mray.Direction = camera.MouseRay.Direction;
            float hitdist = float.MaxValue;

            float dmax = Renderer.renderboundsmaxdist;

            if ((SelectionMode == MapSelectionMode.TimeCycleModifier) && (ymap.TimeCycleModifiers != null))
            {
                for (int i = 0; i < ymap.TimeCycleModifiers.Length; i++)
                {
                    var tcm = ymap.TimeCycleModifiers[i];
                    if ((((tcm.BBMin + tcm.BBMax) * 0.5f) - camera.Position).Length() > dmax) continue;

                    MapBox mb = new();
                    mb.CamRelPos = -camera.Position;
                    mb.BBMin = tcm.BBMin;
                    mb.BBMax = tcm.BBMax;
                    mb.Orientation = Quaternion.Identity;
                    mb.Scale = Vector3.One;
                    Renderer.BoundingBoxes.Add(mb);

                    bbox.Minimum = mb.BBMin;
                    bbox.Maximum = mb.BBMax;
                    if (mray.Intersects(ref bbox, out hitdist) && (hitdist < CurMouseHit.HitDist) && (hitdist > 0))
                    {
                        CurMouseHit.TimeCycleModifier = tcm;
                        CurMouseHit.HitDist = hitdist;
                        CurMouseHit.CamRel = mb.CamRelPos;
                        CurMouseHit.AABB = bbox;
                    }
                }
            }
            if ((SelectionMode == MapSelectionMode.CarGenerator) && (ymap.CarGenerators != null))
            {
                for (int i = 0; i < ymap.CarGenerators.Length; i++)
                {
                    var cg = ymap.CarGenerators[i];
                    MapBox mb = new();
                    mb.CamRelPos = cg.Position - camera.Position;
                    mb.BBMin = cg.BBMin;
                    mb.BBMax = cg.BBMax;
                    mb.Orientation = cg.Orientation;
                    mb.Scale = Vector3.One;
                    Renderer.BoundingBoxes.Add(mb);

                    Quaternion orinv = Quaternion.Invert(cg.Orientation);
                    Ray mraytrn = new();
                    mraytrn.Position = orinv.Multiply(camera.MouseRay.Position - mb.CamRelPos);
                    mraytrn.Direction = orinv.Multiply(mray.Direction);
                    bbox.Minimum = mb.BBMin;
                    bbox.Maximum = mb.BBMax;
                    if (mraytrn.Intersects(ref bbox, out hitdist) && (hitdist < CurMouseHit.HitDist) && (hitdist > 0))
                    {
                        CurMouseHit.CarGenerator = cg;
                        CurMouseHit.HitDist = hitdist;
                        CurMouseHit.CamRel = mb.CamRelPos;
                        CurMouseHit.AABB = bbox;
                    }
                }
                if (SelectedItem.CarGenerator != null)
                {
                }

            }
            if ((SelectionMode == MapSelectionMode.MloInstance) && (ymap.MloEntities != null))
            {
                for (int i = 0; i < ymap.MloEntities.Length; i++)
                {
                    var ent = ymap.MloEntities[i];
                    if (SelectedItem.MloEntityDef == ent) continue;
                    MapBox mb = new();
                    mb.CamRelPos = ent.Position - camera.Position;
                    mb.BBMin = /*ent?.BBMin ??*/ new Vector3(-1.5f);
                    mb.BBMax = /*ent?.BBMax ??*/ new Vector3(1.5f);
                    mb.Orientation = ent?.Orientation ?? Quaternion.Identity;
                    mb.Scale = /*ent?.Scale ??*/ Vector3.One;
                    Renderer.BoundingBoxes.Add(mb);

                    Quaternion orinv = Quaternion.Invert(mb.Orientation);
                    Ray mraytrn = new();
                    mraytrn.Position = orinv.Multiply(camera.MouseRay.Position - mb.CamRelPos);
                    mraytrn.Direction = orinv.Multiply(mray.Direction);
                    bbox.Minimum = mb.BBMin;
                    bbox.Maximum = mb.BBMax;
                    if (mraytrn.Intersects(ref bbox, out hitdist) && (hitdist < CurMouseHit.HitDist) && (hitdist > 0))
                    {
                        CurMouseHit.MloEntityDef = ent;
                        CurMouseHit.EntityDef = ent;
                        CurMouseHit.HitDist = hitdist;
                        CurMouseHit.CamRel = mb.CamRelPos;
                        CurMouseHit.AABB = new BoundingBox(mb.BBMin, mb.BBMax);
                    }
                }
            }
            if ((SelectionMode == MapSelectionMode.Grass) && (ymap.GrassInstanceBatches != null))
            {
                for (int i = 0; i < ymap.GrassInstanceBatches.Length; i++)
                {
                    var gb = ymap.GrassInstanceBatches[i];
                    if ((gb.Position - camera.Position).Length() > dmax) continue;

                    MapBox mb = new();
                    mb.CamRelPos = -camera.Position;
                    mb.BBMin = gb.AABBMin;
                    mb.BBMax = gb.AABBMax;
                    mb.Orientation = Quaternion.Identity;
                    mb.Scale = Vector3.One;
                    Renderer.BoundingBoxes.Add(mb);

                    bbox.Minimum = mb.BBMin;
                    bbox.Maximum = mb.BBMax;
                    if (mray.Intersects(ref bbox, out hitdist) && (hitdist < CurMouseHit.HitDist) && (hitdist > 0))
                    {
                        CurMouseHit.GrassBatch = gb;
                        CurMouseHit.HitDist = hitdist;
                        CurMouseHit.CamRel = mb.CamRelPos;
                        CurMouseHit.AABB = bbox;
                    }
                }
            }
            if ((SelectionMode == MapSelectionMode.LodLights) && (ymap.LODLights != null))
            {
                var ll = ymap.LODLights;
                if ((((ll.BBMin + ll.BBMax) * 0.5f) - camera.Position).Length() <= dmax)
                {

                    MapBox mb = new();
                    mb.CamRelPos = -camera.Position;
                    mb.BBMin = ll.BBMin;
                    mb.BBMax = ll.BBMax;
                    mb.Orientation = Quaternion.Identity;
                    mb.Scale = Vector3.One;
                    Renderer.BoundingBoxes.Add(mb);

                    if (ll.LodLights != null)
                    {
                        for (int i = 0; i < ll.LodLights.Length; i++)
                        {
                            var light = ll.LodLights[i];
                            if ((light.Position - camera.Position).Length() > dmax) continue;
                            MapBox lmb = new();
                            lmb.CamRelPos = light.Position - camera.Position;
                            lmb.BBMin = new Vector3(-0.5f);
                            lmb.BBMax = new Vector3(0.5f);
                            lmb.Orientation = Quaternion.Identity;
                            lmb.Scale = Vector3.One;
                            Renderer.BoundingBoxes.Add(lmb);
                        }
                    }

                    if (ll.BVH != null)
                    {
                        UpdateMouseHits(ll.BVH, ref mray);
                    }
                }
            }
            if ((SelectionMode == MapSelectionMode.LodLights) && (ymap.DistantLODLights != null))
            {
                var dll = ymap.DistantLODLights;
                if ((((dll.BBMin + dll.BBMax) * 0.5f) - camera.Position).Length() <= dmax)
                {
                    MapBox mb = new();
                    mb.CamRelPos = -camera.Position;
                    mb.BBMin = dll.BBMin;
                    mb.BBMax = dll.BBMax;
                    mb.Orientation = Quaternion.Identity;
                    mb.Scale = Vector3.One;
                    Renderer.BoundingBoxes.Add(mb);
                }
            }
            if ((SelectionMode == MapSelectionMode.Occlusion) && (ymap.BoxOccluders != null))
            {
                for (int i = 0; i < ymap.BoxOccluders.Length; i++)
                {
                    var bo = ymap.BoxOccluders[i];
                    if ((bo.Position - camera.Position).Length() > dmax) continue;

                    Renderer.RenderBasePath(bo);

                    MapBox mb = new();
                    mb.CamRelPos = bo.Position - camera.Position;
                    mb.BBMin = bo.BBMin;
                    mb.BBMax = bo.BBMax;
                    mb.Orientation = bo.Orientation;
                    mb.Scale = Vector3.One;
                    //Renderer.BoundingBoxes.Add(mb);

                    Quaternion orinv = Quaternion.Invert(bo.Orientation);
                    Ray mraytrn = new();
                    mraytrn.Position = orinv.Multiply(camera.MouseRay.Position - mb.CamRelPos);
                    mraytrn.Direction = orinv.Multiply(mray.Direction);
                    bbox.Minimum = mb.BBMin;
                    bbox.Maximum = mb.BBMax;
                    if (mraytrn.Intersects(ref bbox, out float hd) && (hd < CurMouseHit.HitDist) && (hd > 0))
                    {
                        hitdist = hd;
                        CurMouseHit.BoxOccluder = bo;
                        CurMouseHit.OccludeModelTri = null;
                        CurMouseHit.HitDist = hitdist;
                        CurMouseHit.CamRel = mb.CamRelPos;
                        CurMouseHit.AABB = bbox;
                    }
                }
            }
            if ((SelectionMode == MapSelectionMode.Occlusion) && (ymap.OccludeModels != null))
            {
                for (int i = 0; i < ymap.OccludeModels.Length; i++)
                {
                    var om = ymap.OccludeModels[i];

                    Renderer.RenderBasePath(om);

                    var hittri = om.RayIntersect(ref mray, ref hitdist);
                    if ((hittri != null) && (hitdist < CurMouseHit.HitDist))
                    {
                        CurMouseHit.BoxOccluder = null;
                        CurMouseHit.OccludeModelTri = hittri;
                        CurMouseHit.HitDist = hitdist;
                        CurMouseHit.CamRel = -camera.Position;
                        CurMouseHit.AABB = hittri.Box;
                    }

                }
            }

        }
        private void UpdateMouseHits<T>(List<T> waterquads) where T : BaseWaterQuad
        {
            BoundingBox bbox = new();
            Ray mray = new();
            mray.Position = camera.MouseRay.Position + camera.Position;
            mray.Direction = camera.MouseRay.Direction;
            float hitdist;


            foreach (T quad in waterquads)
            {
                MapBox mb = new();
                mb.CamRelPos = -camera.Position;
                mb.BBMin = new Vector3(quad.minX, quad.minY, quad.z ?? 0);
                mb.BBMax = new Vector3(quad.maxX, quad.maxY, quad.z ?? 0);
                mb.Orientation = Quaternion.Identity;
                mb.Scale = Vector3.One;
                Renderer.BoundingBoxes.Add(mb);

                bbox.Minimum = mb.BBMin;
                bbox.Maximum = mb.BBMax;

                if(mray.Intersects(ref bbox, out hitdist) && hitdist > 0 && hitdist <= CurMouseHit.HitDist)
                {
                    float curSize = CurMouseHit.AABB.Size.X * CurMouseHit.AABB.Size.Y;
                    float newSize = bbox.Size.X * bbox.Size.Y;
                    if ((curSize == 0) || (newSize < curSize))
                    {
                        CurMouseHit.HitDist = hitdist;
                        CurMouseHit.CamRel = mb.CamRelPos;
                        CurMouseHit.AABB = bbox;

                        CurMouseHit.WaterQuad = quad as WaterQuad;
                        CurMouseHit.WaveQuad = quad as WaterWaveQuad;
                        CurMouseHit.CalmingQuad = quad as WaterCalmingQuad;
                    }
                }
            }
        }
        private void UpdateMouseHits(List<YnvFile> ynvs)
        {
            if (SelectionMode != MapSelectionMode.NavMesh) return;

            Ray mray = new();
            mray.Position = camera.MouseRay.Position + camera.Position;
            mray.Direction = camera.MouseRay.Direction;

            foreach (var ynv in ynvs)
            {
                if (renderpathbounds)
                {
                    if (ynv.Nav == null) continue;
                    if (ynv.Nav.SectorTree == null) continue;

                    MapBox mb = new();
                    mb.CamRelPos = -camera.Position;
                    mb.BBMin = ynv.Nav.SectorTree.AABBMin.XYZ();
                    mb.BBMax = ynv.Nav.SectorTree.AABBMax.XYZ();
                    mb.Orientation = Quaternion.Identity;
                    mb.Scale = Vector3.One;
                    Renderer.BoundingBoxes.Add(mb);
                }

                if (ynv.BVH != null)
                {
                    UpdateMouseHits(ynv.BVH, ref mray);
                }
                //if ((CurMouseHit.NavPoint != null) || (CurMouseHit.NavPortal != null)) continue;
                if ((ynv.Nav != null) && (ynv.Vertices != null) && (ynv.Indices != null) && (ynv.Polys != null))
                {
                    UpdateMouseHits(ynv, ynv.Nav.SectorTree, ynv.Nav.SectorTree, ref mray);
                }
            }

        }
        private void UpdateMouseHits(YnvFile ynv, NavMeshSector? navsector, NavMeshSector rootsec, ref Ray mray)
        {
            if (navsector == null) return;

            float hitdist = float.MaxValue;

            BoundingBox bbox = new();
            bbox.Minimum = navsector.AABBMin.XYZ();
            bbox.Maximum = navsector.AABBMax.XYZ();

            if (rootsec != null) //apparently the Z values are incorrect :(
            {
                bbox.Minimum.Z = rootsec.AABBMin.Z;
                bbox.Maximum.Z = rootsec.AABBMax.Z;
            }

            float fhd;
            if (mray.Intersects(ref bbox, out fhd)) //ray intersects this node... check children for hits!
            {
                ////test vis
                //MapBox mb = new();
                //mb.CamRelPos = -camera.Position;
                //mb.BBMin = bbox.Minimum;
                //mb.BBMax = bbox.Maximum;
                //mb.Orientation = Quaternion.Identity;
                //mb.Scale = Vector3.One;
                //BoundingBoxes.Add(mb);


                if (navsector.SubTree1 != null)
                {
                    UpdateMouseHits(ynv, navsector.SubTree1, rootsec, ref mray);
                }
                if (navsector.SubTree2 != null)
                {
                    UpdateMouseHits(ynv, navsector.SubTree2, rootsec, ref mray);
                }
                if (navsector.SubTree3 != null)
                {
                    UpdateMouseHits(ynv, navsector.SubTree3, rootsec, ref mray);
                }
                if (navsector.SubTree4 != null)
                {
                    UpdateMouseHits(ynv, navsector.SubTree4, rootsec, ref mray);
                }
                if ((navsector.Data != null) && (navsector.Data.PolyIDs != null))
                {
                    BoundingBox cbox = new();
                    cbox.Minimum = bbox.Minimum - camera.Position;
                    cbox.Maximum = bbox.Maximum - camera.Position;

                    var polys = ynv.Polys;
                    var polyids = navsector.Data.PolyIDs;
                    for (int i = 0; i < polyids.Length; i++)
                    {
                        var polyid = polyids[i];
                        if (polyid >= polys.Count)
                        { continue; }

                        var poly = polys[polyid];
                        var ic = poly._RawData.IndexCount;
                        var startid = poly._RawData.IndexID;
                        var endid = startid + ic;
                        if (startid >= ynv.Indices.Count)
                        { continue; }
                        if (endid > ynv.Indices.Count)
                        { continue; }

                        var vc = ynv.Vertices.Count;
                        var startind = ynv.Indices[startid];
                        if (startind >= vc)
                        { continue; }

                        Vector3 p0 = ynv.Vertices[startind];

                        //test triangles for the poly.
                        int tricount = ic - 2;
                        for (int t = 0; t < tricount; t++)
                        {
                            int tid = startid + t;
                            int ind1 = ynv.Indices[tid + 1];
                            int ind2 = ynv.Indices[tid + 2];
                            if ((ind1 >= vc) || (ind2 >= vc))
                            { continue; }

                            Vector3 p1 = ynv.Vertices[ind1];
                            Vector3 p2 = ynv.Vertices[ind2];

                            if (mray.Intersects(ref p0, ref p1, ref p2, out hitdist) && (hitdist < CurMouseHit.HitDist) && (hitdist > 0))
                            {
                                var cellaabb = poly._RawData.CellAABB;
                                CurMouseHit.NavPoly = poly;
                                CurMouseHit.NavPoint = null;
                                CurMouseHit.NavPortal = null;
                                CurMouseHit.HitDist = hitdist;
                                CurMouseHit.AABB = new BoundingBox(cellaabb.Min, cellaabb.Max);
                                break;//no need to test further tris in this poly
                            }
                        }
                    }
                }
            }
        }
        private void UpdateMouseHits(List<YndFile> ynds)
        {
            if (SelectionMode != MapSelectionMode.Path) return;

            Ray mray = new();
            mray.Position = camera.MouseRay.Position + camera.Position;
            mray.Direction = camera.MouseRay.Direction;

            foreach (var ynd in ynds)
            {
                if (renderpathbounds)
                {
                    float minz = (ynd.BVH != null) ? ynd.BVH.Box.Minimum.Z : 0.0f;
                    float maxz = (ynd.BVH != null) ? ynd.BVH.Box.Maximum.Z : 0.0f;
                    MapBox mb = new();
                    mb.CamRelPos = -camera.Position;
                    mb.BBMin = new Vector3(ynd.BBMin.X, ynd.BBMin.Y, minz);
                    mb.BBMax = new Vector3(ynd.BBMax.X, ynd.BBMax.Y, maxz);
                    mb.Orientation = Quaternion.Identity;
                    mb.Scale = Vector3.One;
                    Renderer.BoundingBoxes.Add(mb);
                }

                if (ynd.BVH != null)
                {
                    UpdateMouseHits(ynd.BVH, ref mray);
                }
            }


            if (SelectedItem.PathNode != null)
            {
                float linkrad = 0.25f;

                var n = SelectedItem.PathNode;
                if (n.Links != null)
                {
                    foreach (var ln in n.Links)
                    {
                        if (ln.Node2 == null) continue;//invalid links can hit here...
                        Vector3 dv = n.Position - ln.Node2.Position;
                        float dl = dv.Length();
                        Vector3 dir = dv * (1.0f / dl);
                        Vector3 dup = Vector3.UnitZ;
                        MapBox mb = new();

                        int lanestot = ln.LaneCountForward + ln.LaneCountBackward;
                        float lanewidth = ln.GetLaneWidth();
                        float inner = ln.LaneOffset * lanewidth;// 0.0f;
                        float outer = inner + Math.Max(lanewidth * ln.LaneCountForward, 0.5f);
                        float totwidth = lanestot * lanewidth;
                        float halfwidth = totwidth * 0.5f;
                        if (ln.LaneCountBackward == 0)
                        {
                            inner -= halfwidth;
                            outer -= halfwidth;
                        }
                        if (ln.LaneCountForward == 0)
                        {
                            inner += halfwidth;
                            outer += halfwidth;
                        }


                        mb.CamRelPos = n.Position - camera.Position;
                        mb.BBMin = new Vector3(-linkrad - outer, -linkrad, 0.0f);
                        mb.BBMax = new Vector3(linkrad - inner, linkrad, dl);
                        mb.Orientation = Quaternion.Invert(Quaternion.RotationLookAtRH(dir, dup));
                        mb.Scale = Vector3.One;
                        if (ln == SelectedItem.PathLink)
                        {
                            Renderer.HilightBoxes.Add(mb);
                        }
                        else
                        {
                            Renderer.BoundingBoxes.Add(mb);
                        }
                    }
                }
            }

        }
        private void UpdateMouseHits(List<TrainTrack> tracks)
        {
            if (SelectionMode != MapSelectionMode.TrainTrack) return;

            Ray mray = new();
            mray.Position = camera.MouseRay.Position + camera.Position;
            mray.Direction = camera.MouseRay.Direction;

            foreach (var track in tracks)
            {
                if (renderpathbounds)
                {
                    //MapBox mb = new();
                    //mb.CamRelPos = -camera.Position;
                    //mb.BBMin = track.BVH?.Box.Minimum ?? Vector3.Zero;
                    //mb.BBMax = track.BVH?.Box.Maximum ?? Vector3.Zero;
                    //mb.Orientation = Quaternion.Identity;
                    //mb.Scale = Vector3.One;
                    //BoundingBoxes.Add(mb);
                }

                if (track.BVH != null)
                {
                    UpdateMouseHits(track.BVH, ref mray);
                }
            }


            if (SelectedItem.TrainTrackNode != null)
            {
                float linkrad = 0.25f;
                var n = SelectedItem.TrainTrackNode;
                if (n.Links != null)
                {
                    foreach (var ln in n.Links)
                    {
                        if (ln == null) continue;
                        Vector3 dv = n.Position - ln.Position;
                        float dl = dv.Length();
                        Vector3 dir = dv * (1.0f / dl);
                        Vector3 dup = Vector3.UnitZ;
                        MapBox mb = new();
                        mb.CamRelPos = n.Position - camera.Position;
                        mb.BBMin = new Vector3(-linkrad, -linkrad, 0.0f);
                        mb.BBMax = new Vector3(linkrad, linkrad, dl);
                        mb.Orientation = Quaternion.Invert(Quaternion.RotationLookAtRH(dir, dup));
                        mb.Scale = Vector3.One;
                        Renderer.BoundingBoxes.Add(mb);
                    }
                }
            }

        }
        private void UpdateMouseHits(List<YmtFile> scenarios)
        {
            if (SelectionMode != MapSelectionMode.Scenario) return;

            Ray mray = new();
            mray.Position = camera.MouseRay.Position + camera.Position;
            mray.Direction = camera.MouseRay.Direction;

            foreach (var scenario in scenarios)
            {
                var sr = scenario.ScenarioRegion;
                if (sr == null) continue;

                if (renderscenariobounds)
                {
                    MapBox mb = new();
                    mb.CamRelPos = -camera.Position;
                    mb.BBMin = sr?.BVH?.Box.Minimum ?? Vector3.Zero;
                    mb.BBMax = sr?.BVH?.Box.Maximum ?? Vector3.Zero;
                    mb.Orientation = Quaternion.Identity;
                    mb.Scale = Vector3.One;
                    Renderer.BoundingBoxes.Add(mb);
                }

                if (sr.BVH != null)
                {
                    UpdateMouseHits(sr.BVH, ref mray);
                }
            }


            if (SelectedItem.ScenarioNode != null) //move this stuff to renderselection..?
            {
                var n = SelectedItem.ScenarioNode;
                var nc = n.ChainingNode?.Chain;
                var ncl = n.Cluster;


                //float linkrad = 0.25f;
                //if (n.Links != null)
                //{
                //    foreach (var ln in n.Links)
                //    {
                //        if (ln == null) continue;
                //        Vector3 dv = n.Position - ln.Position;
                //        float dl = dv.Length();
                //        Vector3 dir = dv * (1.0f / dl);
                //        Vector3 dup = Vector3.UnitZ;
                //        MapBox mb = new();
                //        mb.CamRelPos = n.Position - camera.Position;
                //        mb.BBMin = new Vector3(-linkrad, -linkrad, 0.0f);
                //        mb.BBMax = new Vector3(linkrad, linkrad, dl);
                //        mb.Orientation = Quaternion.Invert(Quaternion.RotationLookAtRH(dir, dup));
                //        mb.Scale = Vector3.One;
                //        BoundingBoxes.Add(mb);
                //    }
                //}

                var sr = SelectedItem.ScenarioNode.Ymt.ScenarioRegion;
                //if (renderscenariobounds)
                {
                    MapBox mb = new();
                    mb.CamRelPos = -camera.Position;
                    mb.BBMin = sr?.BVH?.Box.Minimum ?? Vector3.Zero;
                    mb.BBMax = sr?.BVH?.Box.Maximum ?? Vector3.Zero;
                    mb.Orientation = Quaternion.Identity;
                    mb.Scale = Vector3.One;
                    if (renderscenariobounds)
                    {
                        Renderer.HilightBoxes.Add(mb);
                    }
                    else
                    {
                        Renderer.BoundingBoxes.Add(mb);
                    }
                }


                if (ncl != null)
                {

                    //hilight the cluster itself
                    MapBox mb = new();
                    mb.Scale = Vector3.One;
                    mb.BBMin = new Vector3(-0.5f);
                    mb.BBMax = new Vector3(0.5f);
                    mb.CamRelPos = ncl.Position - camera.Position;
                    mb.Orientation = Quaternion.Identity;
                    Renderer.HilightBoxes.Add(mb);


                    //show boxes for points in the cluster
                    if ((ncl.Points != null) && (ncl.Points.MyPoints != null))
                    {
                        foreach (var clpoint in ncl.Points.MyPoints)
                        {
                            if (clpoint == n.ClusterMyPoint) continue; //don't highlight the selected node...
                            mb = new MapBox();
                            mb.Scale = Vector3.One;
                            mb.BBMin = new Vector3(-0.5f);
                            mb.BBMax = new Vector3(0.5f);
                            mb.CamRelPos = clpoint.Position - camera.Position;
                            mb.Orientation = clpoint.Orientation;
                            Renderer.BoundingBoxes.Add(mb);
                        }
                    }
                }



            }




        }
        private void UpdateMouseHits(PathBVHNode pathbvhnode, ref Ray mray)
        {
            float nrad = 0.5f;
            float hitdist = float.MaxValue;

            BoundingSphere bsph = new();
            bsph.Radius = nrad;

            BoundingBox bbox = new();
            bbox.Minimum = pathbvhnode.Box.Minimum - nrad;
            bbox.Maximum = pathbvhnode.Box.Maximum + nrad;

            BoundingBox nbox = new();
            nbox.Minimum = new Vector3(-nrad);
            nbox.Maximum = new Vector3(nrad);

            float fhd;
            if (mray.Intersects(ref bbox, out fhd)) //ray intersects this node... check children for hits!
            {
                if ((pathbvhnode.Node1 != null) && (pathbvhnode.Node2 != null)) //node is split. recurse
                {
                    UpdateMouseHits(pathbvhnode.Node1, ref mray);
                    UpdateMouseHits(pathbvhnode.Node2, ref mray);
                }
                else if (pathbvhnode.Nodes != null) //leaf node. test contaned pathnodes
                {
                    foreach (var n in pathbvhnode.Nodes)
                    {
                        bsph.Center = n.Position;
                        if (mray.Intersects(ref bsph, out hitdist) && (hitdist < CurMouseHit.HitDist) && (hitdist > 0))
                        {
                            CurMouseHit.PathNode = n as YndNode;
                            CurMouseHit.TrainTrackNode = n as TrainTrackNode;
                            CurMouseHit.ScenarioNode = n as ScenarioNode;
                            CurMouseHit.LodLight = n as YmapLODLight;
                            CurMouseHit.NavPoint = n as YnvPoint;
                            CurMouseHit.NavPortal = n as YnvPortal;
                            CurMouseHit.NavPoly = null;
                            CurMouseHit.HitDist = hitdist;
                            CurMouseHit.CamRel = (n.Position - camera.Position);
                            CurMouseHit.AABB = nbox;
                        }
                    }
                }
            }
        }

        public void SelectObject(object obj, object? parent = null, bool addSelection = false)
        {
            if (obj == null)
            {
                SelectItem(null, addSelection);
                return;
            }
            if (obj is object[] arr)
            {
                SelectItem(null, addSelection);
                foreach (var mobj in arr)
                {
                    SelectObject(mobj, null, true);
                }
                if (!addSelection)
                {
                    UpdateSelectionUI(true);
                }
                if ((ProjectForm != null) && !addSelection)
                {
                    ProjectForm.OnWorldSelectionChanged(SelectedItem);
                }
            }
            else
            {
                var ms = MapSelection.FromProjectObject(this, obj, parent);
                if (!ms.HasValue)
                {
                    SelectItem(null, addSelection);
                }
                else
                {
                    SelectItem(ms, addSelection);
                }
            }
        }
        public void SelectItem(MapSelection? mhit = null, bool addSelection = false, bool manualSelection = false, bool notifyProject = true)
        {
            var mhitv = mhit.HasValue ? mhit.Value : new MapSelection();
            if (mhit != null)
            {
                if ((mhitv.Archetype == null) && (mhitv.EntityDef != null))
                {
                    mhitv.Archetype = mhitv.EntityDef.Archetype; //use the entity archetype if no archetype given
                }
                if (mhitv.GrassBatch != null)
                {
                    mhitv.Archetype = mhitv.GrassBatch.Archetype;
                }
            }
            if ((mhitv.Archetype != null) && (mhitv.Drawable == null))
            {
                mhitv.Drawable = gameFileCache.TryGetDrawable(mhitv.Archetype); //no drawable given.. try to get it from the cache.. if it's not there, drawable info won't display...
            }

            var oldnode = SelectedItem.PathNode;
            bool change = false;
            if (mhit != null)
            {
                change = SelectedItem.CheckForChanges(mhitv); 
            }
            else
            {
                change = SelectedItem.CheckForChanges();
            }

            if (addSelection)
            {
                var items = new List<MapSelection>();
                if (SelectedItem.MultipleSelectionItems != null)
                {
                    items.AddRange(SelectedItem.MultipleSelectionItems);

                    if (mhitv.HasValue) //incoming selection isn't empty...
                    {
                        //search the list for a match, remove it if already there, otherwise add it.
                        bool found = false;
                        foreach (var item in items)
                        {
                            if (!item.CheckForChanges(mhitv))
                            {
                                items.Remove(item);
                                found = true;
                                break;
                            }
                        }
                        if (found)
                        {
                            if (items.Count == 1)
                            {
                                mhitv = items[0];
                                items.Clear();
                            }
                            else if (items.Count <= 0)
                            {
                                mhitv.Clear();
                                items.Clear();//this shouldn't really happen..
                            }
                            mhitv.SetMultipleSelectionItems(items.ToArray());
                        }
                        else
                        {
                            mhitv.SetMultipleSelectionItems(null);
                            items.Add(mhitv);
                        }
                        change = true;
                    }
                    else //empty incoming value... do nothing?
                    {
                        return;
                    }
                }
                else //current selection is single item, or empty
                {
                    if (change) //incoming selection item is different from the current one
                    {
                        if (mhitv.HasValue) //incoming selection isn't empty, add it to the list
                        {
                            if (SelectedItem.HasValue) //add the existing item to the selection list, if it's not empty
                            {
                                mhitv.SetMultipleSelectionItems(null);
                                SelectedItem.SetMultipleSelectionItems(null);
                                items.Add(SelectedItem);
                                items.Add(mhitv);
                                SelectedItem.SetMultipleSelectionItems(items.ToArray());
                            }
                        }
                        else //empty incoming value... do nothing?
                        {
                            return;
                        }
                    }
                    else //same thing was selected a 2nd time, just clear the selection.
                    {
                        SelectedItem.Clear();
                        mhit = null; //dont's wants to selects it agains!
                        change = true;
                    }
                }

                if (items.Count > 1)
                {
                    //iterate the selected items, and calculate the selection position
                    mhitv.Clear();
                    mhitv.SetMultipleSelectionItems(items.ToArray());
                }
            }
            else
            {
                if (SelectedItem.MultipleSelectionItems != null)
                {
                    change = true;
                    SelectedItem.Clear();
                }
            }

            if (!change)
            {
                if (mhit.HasValue)
                {
                    //make sure the path link gets changed (sub-selection!)
                    //lock (Renderer.RenderSyncRoot)
                    {
                        SelectedItem.PathLink = mhitv.PathLink;
                        SelectedItem.ScenarioEdge = mhitv.ScenarioEdge;
                    }
                }
                return;
            }

            lock (Renderer.RenderSyncRoot) //drawflags is used when rendering.. need that lock
            {
                if (mhit.HasValue)
                {
                    SelectedItem = mhitv;
                }
                else
                {
                    SelectedItem.Clear();
                }

                if (change)
                {
                    if (!addSelection)
                    {
                        UpdateSelectionUI(true);
                    }

                    Widget.Visible = SelectedItem.CanShowWidget;
                    if (Widget.Visible)
                    {
                        Widget.Position = SelectedItem.WidgetPosition;
                        Widget.Rotation = SelectedItem.WidgetRotation;
                        Widget.RotationWidget.EnableAxes = SelectedItem.WidgetRotationAxes;
                        Widget.ScaleWidget.LockXY = SelectedItem.WidgetScaleLockXY;
                        Widget.Scale = SelectedItem.WidgetScale;
                    }
                }
            }
            if (notifyProject && change && (ProjectForm != null) && (!addSelection || manualSelection))
            {
                ProjectForm.OnWorldSelectionChanged(SelectedItem);
            }

            var newnode = SelectedItem.PathNode;
            if (newnode != oldnode)//this is to allow junction heightmaps to be displayed when selecting a junction node
            {
                UpdatePathYndGraphics(oldnode?.Ynd, false);
                UpdatePathYndGraphics(newnode?.Ynd, false);
            }

            if (change)
            {
                // If an item has been selected the user is likely to use a keybind. We need focus!
                //Focus();//DISABLED THIS due to causing problems with using arrows to select in project window!
            }
        }
        public void SelectMulti(MapSelection[] items, bool addSelection = false, bool notifyProject = true)
        {
            SelectItem(null, addSelection, false, notifyProject);
            if (items != null)
            {
                foreach (var item in items)
                {
                    SelectItem(item, true, false, notifyProject);
                }
                if (!addSelection)
                {
                    UpdateSelectionUI(true);
                }
                if (notifyProject && (ProjectForm != null) && !addSelection)
                {
                    ProjectForm.OnWorldSelectionChanged(SelectedItem);
                }
            }
        }
        private void SelectMousedItem()
        {
            //when clicked, select the currently moused item and update the selection info UI

            if (!MouseSelectEnabled)
            { return; }

            SelectItem(LastMouseHit, Input.CtrlPressed, true);
        }
        private void PerformBoxSelect()
        {
            float minSX = Math.Min(BoxSelectStart.X, BoxSelectEnd.X);
            float maxSX = Math.Max(BoxSelectStart.X, BoxSelectEnd.X);
            float minSY = Math.Min(BoxSelectStart.Y, BoxSelectEnd.Y);
            float maxSY = Math.Max(BoxSelectStart.Y, BoxSelectEnd.Y);

            var items = new List<MapSelection>();
            RenderedDrawable[]? drawableSnapshot = null;

            try
            {
                var list = Renderer.RenderedDrawables;
                if (list != null)
                {
                    int count = list.Count;
                    drawableSnapshot = new RenderedDrawable[count];
                    for (int i = 0; i < count && i < list.Count; i++)
                    {
                        drawableSnapshot[i] = list[i];
                    }
                }
            }
            catch { }

            if (drawableSnapshot != null)
            {
                var viewDir = camera.ViewDirection;
                var camPos = camera.Position;
                var vpMatrix = camera.ViewProjMatrix;
                float camW = camera.Width;
                float camH = camera.Height;

                foreach (var rd in drawableSnapshot)
                {
                    if (rd.Entity == null) continue;

                    var camrel = rd.Entity.Position - camPos;

                    // Must be in front of camera
                    if (Vector3.Dot(camrel, viewDir) <= 0) continue;

                    // Project to screen pixel coordinates
                    var ndc = vpMatrix.MultiplyW(camrel);
                    float sx = (ndc.X * 0.5f + 0.5f) * camW;
                    float sy = (-ndc.Y * 0.5f + 0.5f) * camH;

                    if (sx >= minSX && sx <= maxSX && sy >= minSY && sy <= maxSY)
                    {
                        var item = new MapSelection();
                        item.EntityDef = rd.Entity;
                        item.Archetype = rd.Archetype;
                        item.Drawable = rd.Drawable;
                        item.HitDist = camrel.Length();
                        item.CamRel = camrel;
                        if (rd.Archetype != null)
                        {
                            item.AABB = new BoundingBox(rd.Archetype.BBMin * rd.Entity.Scale, rd.Archetype.BBMax * rd.Entity.Scale);
                            item.BSphere = new BoundingSphere(Vector3.Zero, rd.Archetype.BSRadius);
                        }
                        items.Add(item);
                    }
                }
            }

            if (SelectionMode == MapSelectionMode.Scenario)
            {
                var scenarioList = new List<YmtFile>();
                if (scenarios.Inited)
                {
                    scenarioList.AddRange(scenarios.ScenarioRegions);
                }
                if (ProjectForm != null)
                {
                    ProjectForm.GetVisibleScenarios(camera, scenarioList);
                }

                var viewDir = camera.ViewDirection;
                var camPos = camera.Position;
                var vpMatrix = camera.ViewProjMatrix;
                float camW = camera.Width;
                float camH = camera.Height;

                foreach (var scenario in scenarioList)
                {
                    var sr = scenario.ScenarioRegion;
                    if (sr?.BVH != null)
                    {
                        BoxSelectScenarioBVH(sr.BVH, ref viewDir, ref camPos, ref vpMatrix, camW, camH, minSX, maxSX, minSY, maxSY, items);
                    }
                }
            }

            if (items.Count > 0)
            {
                bool addToSelection = Input.CtrlPressed;
                SelectMulti(items.ToArray(), addToSelection);
            }
            else if (!Input.CtrlPressed)
            {
                SelectItem(null); // clear selection if nothing was in the box
            }
        }
        private void BoxSelectScenarioBVH(PathBVHNode bvhnode, ref Vector3 viewDir, ref Vector3 camPos, ref Matrix vpMatrix, float camW, float camH, float minSX, float maxSX, float minSY, float maxSY, List<MapSelection> items)
        {
            // Quick reject: check if the BVH node's bounding box is entirely behind the camera or off-screen
            if ((bvhnode.Node1 != null) && (bvhnode.Node2 != null))
            {
                BoxSelectScenarioBVH(bvhnode.Node1, ref viewDir, ref camPos, ref vpMatrix, camW, camH, minSX, maxSX, minSY, maxSY, items);
                BoxSelectScenarioBVH(bvhnode.Node2, ref viewDir, ref camPos, ref vpMatrix, camW, camH, minSX, maxSX, minSY, maxSY, items);
            }
            else if (bvhnode.Nodes != null)
            {
                BoundingBox nbox = new();
                nbox.Minimum = new Vector3(-0.5f);
                nbox.Maximum = new Vector3(0.5f);

                foreach (var n in bvhnode.Nodes)
                {
                    var sn = n as ScenarioNode;
                    if (sn == null) continue;

                    var camrel = sn.Position - camPos;
                    if (Vector3.Dot(camrel, viewDir) <= 0) continue;

                    var ndc = vpMatrix.MultiplyW(camrel);
                    float sx = (ndc.X * 0.5f + 0.5f) * camW;
                    float sy = (-ndc.Y * 0.5f + 0.5f) * camH;

                    if (sx >= minSX && sx <= maxSX && sy >= minSY && sy <= maxSY)
                    {
                        var item = new MapSelection();
                        item.ScenarioNode = sn;
                        item.HitDist = camrel.Length();
                        item.CamRel = camrel;
                        item.AABB = nbox;
                        items.Add(item);
                    }
                }
            }
        }
        private void UpdateSelectionUI(bool wait)
        {
            try
            {
                if (IsDisposed || IsHandleCreated == false) return;
                if (InvokeRequired)
                {
                    if (wait)
                    {
                        Invoke(new Action(() => { UpdateSelectionUI(wait); }));
                    }
                    else
                    {
                        BeginInvoke(new Action(() => { UpdateSelectionUI(wait); }));
                    }
                }
                else
                {
                    SetSelectionUI(SelectedItem);

                    if (InfoForm != null)
                    {
                        InfoForm.SetSelection(SelectedItem);
                    }
                }
            }
            catch (ObjectDisposedException) { }
            catch (Win32Exception) { }
            catch (InvalidOperationException) { }
        }
        private void SetSelectionUI(MapSelection item)
        {
            SelectionNameTextBox.Text = item.GetNameString("Nothing selected");
            //SelEntityPropertyGrid.SelectedObject = item.EntityDef;
            SelArchetypePropertyGrid.SelectedObject = item.Archetype;
            SelDrawablePropertyGrid.SelectedObject = item.Drawable;

            Renderer.SelectionModelDrawFlags.Clear();
            Renderer.SelectionGeometryDrawFlags.Clear();
            SelDrawableModelsTreeView.Nodes.Clear();
            SelDrawableTexturesTreeView.Nodes.Clear();
            if (item.Drawable != null)
            {
                AddSelectionDrawableModelsTreeNodes(item.Drawable.DrawableModels?.High, "High Detail", true);
                AddSelectionDrawableModelsTreeNodes(item.Drawable.DrawableModels?.Med, "Medium Detail", false);
                AddSelectionDrawableModelsTreeNodes(item.Drawable.DrawableModels?.Low, "Low Detail", false);
                AddSelectionDrawableModelsTreeNodes(item.Drawable.DrawableModels?.VLow, "Very Low Detail", false);
                //AddSelectionDrawableModelsTreeNodes(item.Drawable.DrawableModels?.Extra, "X Detail", false);
            }


            YmapFile? ymap = null;
            YnvFile? ynv = null;
            YndFile? ynd = null;
            TrainTrack? traintr = null;
            YmtFile? scenario = null;
            RelFile? audiofile = null;
            ToolbarCopyButton.Enabled = false;
            ToolbarDeleteItemButton.Enabled = false;
            ToolbarDeleteItemButton.Text = "Delete";
            ToolbarAddItemButton.ToolTipText = "Add";
            ToolbarAddItemButton.Enabled = false;
            ToolbarPasteButton.Enabled = CopiedItem.CanCopyPaste;


            if (item.MultipleSelectionItems != null)
            {
                SelectionEntityTabPage.Text = "Multiple items";
                SelEntityPropertyGrid.SelectedObject = item.MultipleSelectionItems;
                ToolbarCopyButton.Enabled = item.CanCopyPaste;
                ToolbarDeleteItemButton.Enabled = true;
                ToolbarDeleteItemButton.Text = "Delete multiple items";
            }
            else if (item.TimeCycleModifier != null)
            {
                SelectionEntityTabPage.Text = "TCMod";
                SelEntityPropertyGrid.SelectedObject = item.TimeCycleModifier;
            }
            else if (item.CarGenerator != null)
            {
                SelectionEntityTabPage.Text = "CarGen";
                SelEntityPropertyGrid.SelectedObject = item.CarGenerator;
                ymap = item.CarGenerator.Ymap;
                ToolbarCopyButton.Enabled = true;
                ToolbarDeleteItemButton.Enabled = true;
                ToolbarDeleteItemButton.Text = "Delete car generator";
            }
            else if (item.LodLight != null)
            {
                SelectionEntityTabPage.Text = "LodLight";
                SelEntityPropertyGrid.SelectedObject = item.LodLight;
                ymap = item.LodLight.Ymap;
                ToolbarCopyButton.Enabled = true;
                ToolbarDeleteItemButton.Enabled = true;
                ToolbarDeleteItemButton.Text = "Delete LOD light";
            }
            else if (item.GrassBatch != null)
            {
                SelectionEntityTabPage.Text = "Grass";
                SelEntityPropertyGrid.SelectedObject = item.GrassBatch;
            }
            else if (item.BoxOccluder != null)
            {
                SelectionEntityTabPage.Text = "BoxOccluder";
                SelEntityPropertyGrid.SelectedObject = item.BoxOccluder;
                ymap = item.BoxOccluder.Ymap;
                ToolbarCopyButton.Enabled = true;
                ToolbarDeleteItemButton.Enabled = true;
                ToolbarDeleteItemButton.Text = "Delete Box Occluder";
            }
            else if (item.OccludeModelTri != null)
            {
                SelectionEntityTabPage.Text = "OccludeTriangle";
                SelEntityPropertyGrid.SelectedObject = item.OccludeModelTri;
                ymap = item.OccludeModelTri.Ymap;
                ToolbarCopyButton.Enabled = true;
                ToolbarDeleteItemButton.Enabled = true;
                ToolbarDeleteItemButton.Text = "Delete Occlude Model Triangle";
            }
            else if (item.WaterQuad != null)
            {
                SelectionEntityTabPage.Text = "WaterQuad";
                SelEntityPropertyGrid.SelectedObject = item.WaterQuad;
            }
            else if (item.CalmingQuad != null)
            {
                SelectionEntityTabPage.Text = "CalmingQuad";
                SelEntityPropertyGrid.SelectedObject = item.CalmingQuad;
            }
            else if (item.WaveQuad != null)
            {
                SelectionEntityTabPage.Text = "WaveQuad";
                SelEntityPropertyGrid.SelectedObject = item.WaveQuad;
            }
            else if (item.PathNode != null)
            {
                SelectionEntityTabPage.Text = "PathNode";
                SelEntityPropertyGrid.SelectedObject = item.PathNode;
                ynd = item.PathNode.Ynd;
                ToolbarCopyButton.Enabled = true;
                ToolbarDeleteItemButton.Enabled = true;
                ToolbarDeleteItemButton.Text = "Delete path node";
            }
            else if (item.NavPoly != null)
            {
                SelectionEntityTabPage.Text = "NavPoly";
                SelEntityPropertyGrid.SelectedObject = item.NavPoly;
                ynv = item.NavPoly.Ynv;
                //ToolbarCopyButton.Enabled = true;
                //ToolbarDeleteItemButton.Enabled = true;
                //ToolbarDeleteItemButton.Text = "Delete nav poly";
            }
            else if (item.NavPoint != null)
            {
                SelectionEntityTabPage.Text = "NavPoint";
                SelEntityPropertyGrid.SelectedObject = item.NavPoint;
                ynv = item.NavPoint.Ynv;
                //ToolbarCopyButton.Enabled = true;
                //ToolbarDeleteItemButton.Enabled = true;
                //ToolbarDeleteItemButton.Text = "Delete nav point";
            }
            else if (item.NavPortal != null)
            {
                SelectionEntityTabPage.Text = "NavPortal";
                SelEntityPropertyGrid.SelectedObject = item.NavPortal;
                ynv = item.NavPortal.Ynv;
                //ToolbarCopyButton.Enabled = true;
                //ToolbarDeleteItemButton.Enabled = true;
                //ToolbarDeleteItemButton.Text = "Delete nav portal";
            }
            else if (item.TrainTrackNode != null)
            {
                SelectionEntityTabPage.Text = "TrainNode";
                SelEntityPropertyGrid.SelectedObject = item.TrainTrackNode;
                traintr = item.TrainTrackNode.Track;
                ToolbarCopyButton.Enabled = true;
                ToolbarDeleteItemButton.Enabled = true;
                ToolbarDeleteItemButton.Text = "Delete train track node";
            }
            else if (item.ScenarioNode != null)
            {
                SelectionEntityTabPage.Text = item.ScenarioNode.ShortTypeName;
                SelEntityPropertyGrid.SelectedObject = item.ScenarioNode;
                scenario = item.ScenarioNode.Ymt;
                ToolbarCopyButton.Enabled = true;
                ToolbarDeleteItemButton.Enabled = true;
                ToolbarDeleteItemButton.Text = "Delete scenario point";
            }
            else if (item.Audio != null)
            {
                SelectionEntityTabPage.Text = item.Audio.ShortTypeName;
                SelEntityPropertyGrid.SelectedObject = item.Audio;
                audiofile = item.Audio.RelFile;
            }
            else
            {
                SelectionEntityTabPage.Text = "Entity";
                SelEntityPropertyGrid.SelectedObject = item.EntityDef;
                if (item.EntityDef != null)
                {
                    ymap = item.EntityDef?.Ymap;
                    ToolbarCopyButton.Enabled = true;
                    ToolbarDeleteItemButton.Enabled = true;
                    ToolbarDeleteItemButton.Text = "Delete entity";
                }
            }


            if (item.EntityExtension != null)
            {
                SelExtensionPropertyGrid.SelectedObject = item.EntityExtension;
                ShowSelectedExtensionTab(true);
            }
            else if (item.ArchetypeExtension != null)
            {
                SelExtensionPropertyGrid.SelectedObject = item.ArchetypeExtension;
                ShowSelectedExtensionTab(true);
            }
            else if (item.CollisionVertex != null)
            {
                SelExtensionPropertyGrid.SelectedObject = item.CollisionVertex;
                ShowSelectedExtensionTab(true, "Coll");
                ToolbarDeleteItemButton.Enabled = true;
                ToolbarDeleteItemButton.Text = "Delete collision vertex";
            }
            else if (item.CollisionPoly != null)
            {
                SelExtensionPropertyGrid.SelectedObject = item.CollisionPoly;
                ShowSelectedExtensionTab(true, "Coll");
                ToolbarDeleteItemButton.Enabled = true;
                ToolbarDeleteItemButton.Text = "Delete collision poly";
            }
            else if (item.CollisionBounds != null)
            {
                SelExtensionPropertyGrid.SelectedObject = item.CollisionBounds;
                ShowSelectedExtensionTab(true, "Coll");
                ToolbarDeleteItemButton.Enabled = true;
                ToolbarDeleteItemButton.Text = "Delete collision bounds";
            }
            else
            {
                SelExtensionPropertyGrid.SelectedObject = null;
                ShowSelectedExtensionTab(false);
            }



            if (ymap != null)
            {
                EnableYmapUI(true, ymap.Name);
            }
            if (ynd != null)
            {
                EnableYndUI(true, ynd.Name);
            }
            if (ynv != null)
            {
                EnableYnvUI(true, ynv.Name);
            }
            if (traintr != null)
            {
                EnableTrainsUI(true, traintr.Name);
            }
            if (scenario != null)
            {
                EnableScenarioUI(true, scenario.Name);
            }
            if (audiofile != null)
            {
                EnableAudioUI(true, audiofile.Name);
            }

        }
        private void ShowSelectedExtensionTab(bool show, string text = "Ext")
        {
            SelectionExtensionTabPage.Text = text;
            if (show)
            {
                if (!SelectionTabControl.TabPages.Contains(SelectionExtensionTabPage))
                {
                    SelectionTabControl.TabPages.Add(SelectionExtensionTabPage);
                    SelectionTabControl.SelectedTab = SelectionExtensionTabPage;
                }
            }
            else
            {
                if (SelectionTabControl.TabPages.Contains(SelectionExtensionTabPage))
                {
                    SelectionTabControl.TabPages.Remove(SelectionExtensionTabPage);
                }
            }
        }
        private void AddSelectionDrawableModelsTreeNodes(DrawableModel[]? models, string prefix, bool check)
        {
            if (models == null) return;

            for (int mi = 0; mi < models.Length; mi++)
            {
                var model = models[mi];
                string mprefix = prefix + " " + (mi + 1).ToString();
                var mnode = SelDrawableModelsTreeView.Nodes.Add(mprefix + " " + model.ToString());
                mnode.Tag = model;
                mnode.Checked = check;

                var tmnode = SelDrawableTexturesTreeView.Nodes.Add(mprefix + " " + model.ToString());
                tmnode.Tag = model;

                if (!check)
                {
                    Renderer.SelectionModelDrawFlags[model] = false;
                }

                if (model.Geometries == null) continue;

                foreach (var geom in model.Geometries)
                {
                    var gname = geom.ToString();
                    var gnode = mnode.Nodes.Add(gname);
                    gnode.Tag = geom;
                    gnode.Checked = true;// check;

                    var tgnode = tmnode.Nodes.Add(gname);
                    tgnode.Tag = geom;

                    if ((geom.Shader != null) && (geom.Shader.ParametersList != null) && (geom.Shader.ParametersList.Hashes != null))
                    {
                        var pl = geom.Shader.ParametersList;
                        var h = pl.Hashes;
                        var p = pl.Parameters;
                        for (int ip = 0; ip < h.Length; ip++)
                        {
                            var hash = pl.Hashes[ip];
                            var parm = pl.Parameters[ip];
                            var tex = parm.Data as TextureBase;
                            if (tex != null)
                            {
                                var t = tex as Texture;
                                var tstr = tex.Name.Trim();
                                if (t != null)
                                {
                                    tstr = string.Format("{0} ({1}x{2}, embedded)", tex.Name, t.Width, t.Height);
                                }
                                var tnode = tgnode.Nodes.Add(hash.ToString().Trim() + ": " + tstr);
                                tnode.Tag = tex;
                            }
                        }
                        tgnode.Expand();
                    }
                    
                }

                mnode.Expand();
                tmnode.Expand();
            }
        }
        private void UpdateSelectionDrawFlags(TreeNode node)
        {
            //update the selection draw flags depending on tag and checked/unchecked
            var model = node.Tag as DrawableModel;
            var geom = node.Tag as DrawableGeometry;
            bool rem = node.Checked;

            Renderer.UpdateSelectionDrawFlags(model, geom, rem);
        }
        public void SyncSelDrawableModelsTreeNode(TreeNode node)
        {
            //called by the info form when a selection treeview node is checked/unchecked.
            foreach (TreeNode mnode in SelDrawableModelsTreeView.Nodes)
            {
                if (mnode.Tag == node.Tag)
                {
                    if (mnode.Checked != node.Checked)
                    {
                        mnode.Checked = node.Checked;
                    }
                }
                foreach (TreeNode gnode in mnode.Nodes)
                {
                    if (gnode.Tag == node.Tag)
                    {
                        if (gnode.Checked != node.Checked)
                        {
                            gnode.Checked = node.Checked;
                        }
                    }
                }
            }
        }


        private void ShowInfoForm()
        {
            if (InfoForm == null)
            {
                InfoForm = new WorldInfoForm(this);
                InfoForm.SetSelection(SelectedItem);
                InfoForm.SetSelectionMode(SelectionModeStr, MouseSelectEnabled);
                InfoForm.Show(this);
            }
            else
            {
                if (InfoForm.WindowState == FormWindowState.Minimized)
                {
                    InfoForm.WindowState = FormWindowState.Normal;
                }
                InfoForm.Focus();
            }
            ToolbarInfoWindowButton.Checked = true;
        }
        public void OnInfoFormSelectionModeChanged(string mode, bool enableSelect)
        {
            //called by the WorldInfoForm
            SetSelectionMode(mode);
            SetMouseSelect(enableSelect);
        }
        public void OnInfoFormClosed()
        {
            //called by the WorldInfoForm when it's closed.
            InfoForm = null;
            ToolbarInfoWindowButton.Checked = false;
        }

        private void ShowProjectForm()
        {
            if (ProjectForm == null)
            {
                ProjectForm = new ProjectForm(this);
                ProjectForm.Show(this);
                ProjectForm.OnWorldSelectionChanged(SelectedItem); // so that the project form isn't stuck on the welcome window.
            }
            else
            {
                if (ProjectForm.WindowState == FormWindowState.Minimized)
                {
                    ProjectForm.WindowState = FormWindowState.Normal;
                }
                ProjectForm.Focus();
            }
            ToolbarProjectWindowButton.Checked = true;
        }
        public void OnProjectFormClosed()
        {
            ProjectForm = null;
            ToolbarProjectWindowButton.Checked = false;
        }

        private void ShowSearchForm()
        {
            if (SearchForm == null)
            {
                SearchForm = new WorldSearchForm(this);
                SearchForm.Show(this);
            }
            else
            {
                if (SearchForm.WindowState == FormWindowState.Minimized)
                {
                    SearchForm.WindowState = FormWindowState.Normal;
                }
                SearchForm.Focus();
            }
            //ToolbarSearchWindowButton.Checked = true;
        }
        public void OnSearchFormClosed()
        {
            SearchForm = null;
            //ToolbarSearchWindowButton.Checked = false;
        }

        private void ShowCutsceneForm()
        {
            if (CutsceneForm == null)
            {
                CutsceneForm = new CutsceneForm(this);
                CutsceneForm.Show(this);
            }
            else
            {
                if (CutsceneForm.WindowState == FormWindowState.Minimized)
                {
                    CutsceneForm.WindowState = FormWindowState.Normal;
                }
                CutsceneForm.Focus();
            }
            //ToolbarCutsceneWindowButton.Checked = true;
        }
        public void OnCutsceneFormClosed()
        {
            CutsceneForm = null;
            //ToolbarCutsceneWindowButton.Checked = false;
        }

        public void ShowModel(string name)
        {
            ViewModeComboBox.Text = "Model view";
            ModelComboBox.Text = name;
            modelname = name;
        }
        public void GoToEntity(YmapEntityDef? entity)
        {
            if (entity == null) return;
            ViewModeComboBox.Text = "World view";
            GoToPosition(entity.Position);
            SelectObject(entity);
        }


        private void LoadWorld()
        {

#if !DEBUG
            try
            {
#endif
                UpdateStatus("Loading timecycles...");
                timecycle.UseModdedData = !Settings.Default.UseOriginalLighting;
                timecycle.Init(gameFileCache, UpdateStatus);
                timecycle.SetTime(Renderer.timeofday);
#if !DEBUG
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading timecycles: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            try
            {
#endif
                UpdateStatus("Loading materials...");
                BoundsMaterialTypes.Init(gameFileCache);
#if !DEBUG
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading materials: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            try
            {
#endif
                UpdateStatus("Loading weather...");
                weather.Init(gameFileCache, UpdateStatus, timecycle);
                UpdateWeatherTypesComboBox(weather);
#if !DEBUG
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading weather files, ensure you do not have FiveMods installed or any Redux mod. Game may require reinstall.: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            try
            {
#endif
                UpdateStatus("Loading clouds...");
                clouds.Init(gameFileCache, UpdateStatus, weather);
                UpdateCloudTypesComboBox(clouds);
#if !DEBUG
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading clouds: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            try
            {
#endif
                UpdateStatus("Loading water...");
                water.Init(gameFileCache, UpdateStatus);
#if !DEBUG
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading water: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            try
            {
#endif
                UpdateStatus("Loading trains...");
                trains.Init(gameFileCache, UpdateStatus);
#if !DEBUG
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading trains: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            try
            {
#endif
                UpdateStatus("Loading scenarios...");
                scenarios.Init(gameFileCache, UpdateStatus, timecycle);
#if !DEBUG
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading scenarios: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            try
            {
#endif
                UpdateStatus("Loading popzones...");
                popzones.Init(gameFileCache, UpdateStatus);
#if !DEBUG
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading popzones: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            try
            {
#endif
                UpdateStatus("Loading heightmaps...");
                heightmaps.Init(gameFileCache, UpdateStatus);
#if !DEBUG
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading heightmaps: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            try
            {
#endif
                UpdateStatus("Loading watermaps...");
                watermaps.Init(gameFileCache, UpdateStatus);
#if !DEBUG
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading watermaps: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            try
            {
#endif
                UpdateStatus("Loading audio zones...");
                audiozones.Init(gameFileCache, UpdateStatus);
#if !DEBUG
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading audio zones: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            try
            {
#endif
                UpdateStatus("Loading world...");
                space.Init(gameFileCache, UpdateStatus);
#if !DEBUG
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading world: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
#endif
            UpdateStatus("World loaded");

        }



        private void SetDlcLevel(string dlc, bool enable)
        {
            if (!initialised) return;
            Cursor = Cursors.WaitCursor;
            Task.Run(() =>
            {
                try
                {
                    lock (Renderer.RenderSyncRoot)
                    {
                        if (gameFileCache.SetDlcLevel(dlc, enable))
                        {
                            LoadWorld();
                        }
                    }
                    Invoke(new Action(()=> {
                        Cursor = Cursors.Default;
                    }));
                }
                catch (Exception ex)
                {
                    try { Invoke(new Action(() => { Cursor = Cursors.Default; MessageBox.Show($"Error setting DLC level: {ex.Message}"); })); }
                    catch (ObjectDisposedException) { }
                    catch (Win32Exception) { }
                    catch (InvalidOperationException) { }
                }
            });
        }

        private void SetModsEnabled(bool enable)
        {
            if (!initialised) return;
            Cursor = Cursors.WaitCursor;
            Task.Run(() =>
            {
                try
                {
                    lock (Renderer.RenderSyncRoot)
                    {
                        if (gameFileCache.SetModsEnabled(enable))
                        {
                            UpdateDlcListComboBox(gameFileCache.DlcNameList);

                            LoadWorld();
                        }
                    }
                    Invoke(new Action(() => {
                        Cursor = Cursors.Default;
                    }));
                }
                catch (Exception ex)
                {
                    try { Invoke(new Action(() => { Cursor = Cursors.Default; MessageBox.Show($"Error setting mods enabled: {ex.Message}"); })); }
                    catch (ObjectDisposedException) { }
                    catch (Win32Exception) { }
                    catch (InvalidOperationException) { }
                }
            });
        }


        private void ContentThread()
        {
            //main content loading thread.
            running = true;

            UpdateStatus("Scanning...");

            try
            {
                GTA5Keys.LoadFromPath(GTAFolder.CurrentGTAFolder, GTAFolder.IsGen9, Settings.Default.Key);

                //save the key for later if it's not saved already. not really ideal to have this in this thread
                if (string.IsNullOrEmpty(Settings.Default.Key) && (GTA5Keys.PC_AES_KEY != null))
                {
                    Settings.Default.Key = Convert.ToBase64String(GTA5Keys.PC_AES_KEY);
                    Settings.Default.Save();
                }
            }
            catch
            {
                MessageBox.Show("Keys not found! This shouldn't happen, GTA5.exe outdated? CodeWalker outdated?");
                Close();
                return;
            }

            gameFileCache.Init(UpdateStatus, LogError);

            UpdateDlcListComboBox(gameFileCache.DlcNameList);

            EnableCacheDependentUI();


#if !DEBUG
            try
            {
#endif
                LoadWorld();
#if !DEBUG
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load world: {ex.Message}");
                Close();
                return;
            }
#endif



            initialised = true;

            EnableDLCModsUI();


            Task.Run(() => {
                while (formopen && !IsDisposed) //renderer content loop
                {
#if !DEBUG
                    try
                    {
#endif
                        bool rcItemsPending = Renderer.ContentThreadProc();
                        if (!rcItemsPending)
                        {
                            Thread.Sleep(1); //sleep if there's nothing to do
                        }
#if !DEBUG
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Renderer Failed: {ex.Message}");
                        Close();
                        return;
                    }
#endif
                }
            });

            while (formopen && !IsDisposed) //main asset loop
            {
#if !DEBUG
                try
                {
#endif
                    bool fcItemsPending = gameFileCache.ContentThreadProc();
                    if (!fcItemsPending)
                    {
                        Thread.Sleep(1); //sleep if there's nothing to do
                    }
#if !DEBUG
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"GameFileCache Failed: {ex.Message}");
                    Close();
                    return;
                }
#endif
            }

            gameFileCache.Clear();

            running = false;
        }



        private volatile string pendingStatusText;
        private int statusUpdatePending; //0 = no marshal in flight, 1 = one queued

        private void UpdateStatus(string text)
        {
            try
            {
                if (IsDisposed || IsHandleCreated == false) return;
                if (InvokeRequired)
                {
                    //Coalesce status updates: during loading this is called per-entry from worker
                    //threads (hundreds of thousands of times). Queuing a BeginInvoke for each one
                    //floods the UI message pump. Instead keep at most one marshal in flight and let
                    //it pick up the most recent text, so we never drop the final value.
                    pendingStatusText = text;
                    if (Interlocked.Exchange(ref statusUpdatePending, 1) == 0)
                    {
                        BeginInvoke(new Action(() =>
                        {
                            Interlocked.Exchange(ref statusUpdatePending, 0);
                            if (IsDisposed) return;
                            StatusLabel.Text = pendingStatusText;
                        }));
                    }
                }
                else
                {
                    StatusLabel.Text = text;
                }
            }
            catch (ObjectDisposedException) { }
            catch (Win32Exception) { }
            catch (InvalidOperationException) { }
        }
        private void UpdateMousedLabel(string text)
        {
            try
            {
                if (IsDisposed || IsHandleCreated == false) return;
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => { UpdateMousedLabel(text); }));
                }
                else
                {
                    MousedLabel.Text = text;
                }
            }
            catch (ObjectDisposedException) { }
            catch (Win32Exception) { }
            catch (InvalidOperationException) { }
        }
        private void UpdateWeatherTypesComboBox(Weather weather)
        {
            try
            {
                if (IsDisposed || IsHandleCreated == false) return;
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => { UpdateWeatherTypesComboBox(weather); }));
                }
                else
                {

                    //MessageBox.Show("sky_hdr: " + weather.GetDynamicValue("sky_hdr").ToString() + "\n" +
                    //                "Timecycle index: " + weather.Timecycle.CurrentSampleIndex + "\n" +
                    //                "Timecycle blend: " + weather.Timecycle.CurrentSampleBlend + "\n");

                    WeatherComboBox.Items.Clear();
                    foreach (string wt in weather.WeatherTypes.Keys)
                    {
                        WeatherComboBox.Items.Add(wt);
                    }
                    WeatherComboBox.SelectedIndex = Math.Max(WeatherComboBox.FindString(Settings.Default.Weather), 0);
                    WeatherRegionComboBox.SelectedIndex = Math.Max(WeatherRegionComboBox.FindString(Settings.Default.Region), 0);
                }
            }
            catch (ObjectDisposedException) { }
            catch (Win32Exception) { }
            catch (InvalidOperationException) { }
        }
        private void UpdateCloudTypesComboBox(Clouds clouds)
        {
            try
            {
                if (IsDisposed || IsHandleCreated == false) return;
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => { UpdateCloudTypesComboBox(clouds); }));
                }
                else
                {
                    CloudsComboBox.Items.Clear();
                    foreach (var frag in clouds.HatManager.CloudHatFrags)
                    {
                        CloudsComboBox.Items.Add(frag.Name);
                    }
                    CloudsComboBox.SelectedIndex = Math.Max(CloudsComboBox.FindString(Renderer.individualcloudfrag), 0);


                    CloudParamComboBox.Items.Clear();
                    foreach (var setting in clouds.AnimSettings.Values)
                    {
                        CloudParamComboBox.Items.Add(setting);
                    }
                    CloudParamComboBox.SelectedIndex = 0;
                }
            }
            catch (ObjectDisposedException) { }
            catch (Win32Exception) { }
            catch (InvalidOperationException) { }
        }
        private void UpdateDlcListComboBox(List<string> dlcnames)
        {
            try
            {
                if (IsDisposed || IsHandleCreated == false) return;
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => { UpdateDlcListComboBox(dlcnames); }));
                }
                else
                {
                    DlcLevelComboBox.Items.Clear();
                    foreach (var dlcname in dlcnames)
                    {
                        DlcLevelComboBox.Items.Add(dlcname);
                    }
                    if (string.IsNullOrEmpty(gameFileCache.SelectedDlc))
                    {
                        DlcLevelComboBox.SelectedIndex = dlcnames.Count - 1;
                    }
                    else
                    {
                        int idx = DlcLevelComboBox.FindString(gameFileCache.SelectedDlc);
                        DlcLevelComboBox.SelectedIndex = (idx > 0) ? idx : (dlcnames.Count - 1);
                    }
                }
            }
            catch (ObjectDisposedException) { }
            catch (Win32Exception) { }
            catch (InvalidOperationException) { }
        }

        private void LogError(string text)
        {
            try
            {
                if (IsDisposed || IsHandleCreated == false) return;
                if (InvokeRequired)
                {
                    Invoke(new Action(() => { LogError(text); }));
                }
                else
                {
                    ConsoleTextBox.AppendText(text + "\r\n");
                    //MessageBox.Show(text);
                }
            }
            catch (ObjectDisposedException) { }
            catch (Win32Exception) { }
            catch (InvalidOperationException) { }
        }




        private void UpdateMarkerSelectionPanelInvoke()
        {
            try
            {
                if (IsDisposed || IsHandleCreated == false) return;
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => { UpdateMarkerSelectionPanel(); }));
                }
                else
                {
                    UpdateMarkerSelectionPanel();
                }
            }
            catch (ObjectDisposedException) { }
            catch (Win32Exception) { }
            catch (InvalidOperationException) { }
        }
        private void UpdateMarkerSelectionPanel()
        {
            if (!SelectedMarkerPanel.Visible) return;
            if (SelectedMarker == null)
            {
                SelectedMarkerPanel.Visible = false;
                return;
            }

            int ox = -90; //screen offset from actual marker world pos
            int oy = -76;

            float spx = ((SelectedMarker.ScreenPos.X * 0.5f) + 0.5f) * camera.Width;
            float spy = ((SelectedMarker.ScreenPos.Y * -0.5f) + 0.5f) * camera.Height;

            int px = (int)Math.Round(spx, MidpointRounding.AwayFromZero) + ox;
            int py = (int)Math.Round(spy, MidpointRounding.AwayFromZero) + oy;

            int sx = SelectedMarkerPanel.Width;
            int sy = SelectedMarkerPanel.Height;

            SelectedMarkerPanel.SetBounds(px, py, sx, sy);
        }
        private void ShowMarkerSelectionInfo(MapMarker marker)
        {
            SelectedMarkerNameTextBox.Text = SelectedMarker.Name;
            SelectedMarkerPositionTextBox.Text = SelectedMarker.Get3DWorldPosString();
            UpdateMarkerSelectionPanel();
            SelectedMarkerPanel.Visible = true;
        }
        private void HideMarkerSelectionInfo()
        {
            SelectedMarkerPanel.Visible = false;
        }

        private MapMarker FindMousedMarker()
        {
            if (!MouseSelectEnabled) return null;
            
            if (!markersortedsyncroot.TryEnter(1)) // dont wait long for lock
                return null;
            
            try
            {
                float mx = MouseLastPoint.X;
                float my = MouseLastPoint.Y;
                
                // cache camera dimensions
                float cameraWidth = camera.Width;
                float cameraHeight = camera.Height;

                if (ShowLocatorCheckBox.Checked && LocatorMarker != null)
                {
                    if (IsMarkerUnderPointOptimized(LocatorMarker, mx, my, cameraWidth, cameraHeight))
                    {
                        return LocatorMarker;
                    }
                }

                // search backwards through the render markers
                var markers = SortedMarkers;
                for (int i = markers.Count - 1; i >= 0; i--)
                {
                    MapMarker m = markers[i];
                    if (IsMarkerUnderPointOptimized(m, mx, my, cameraWidth, cameraHeight))
                    {
                        return m;
                    }
                }
            }
            finally
            {
                markersortedsyncroot.Exit();
            }
            
            return null;
        }
        
        private bool IsMarkerUnderPointOptimized(MapMarker marker, float x, float y, float cameraWidth, float cameraHeight)
        {
            if (marker.ScreenPos.Z <= 0.0f) return false; // behind the camera...
            
            float screenX = ((marker.ScreenPos.X * 0.5f) + 0.5f) * cameraWidth;
            float screenY = ((marker.ScreenPos.Y * -0.5f) + 0.5f) * cameraHeight;
            
            float dx = x - screenX;
            float dy = y - screenY;
            float mcx = marker.Icon.Center.X;
            float mcy = marker.Icon.Center.Y;
            
            return (dx >= -mcx && dx <= mcx) && (dy <= 0.0f && dy >= -mcy);
        }
        private bool IsMarkerUnderPoint(MapMarker marker, float x, float y)
        {
            return IsMarkerUnderPointOptimized(marker, x, y, camera.Width, camera.Height);
        }

        private void GoToMarker(MapMarker m)
        {
            //////adjust the target to account for the main panel...
            ////Vector3 view = m.TexturePos;
            ////view.X += ((float)(MainPanel.Width + 4) * 0.5f) / CurrentZoom;
            ////TargetViewCenter = view;

            camera.FollowEntity.Position = m.WorldPos;

        }
        public void GoToPosition(Vector3 p)
        {
            camera.FollowEntity.Position = p;
        }
        public void GoToPosition(Vector3 p, Vector3 bound)
        {
            camera.FollowEntity.Position = p;
            var bl = bound.Length();
            camera.TargetDistance = bl > 1f ? bl : 1f;
        }

        public MapMarker AddMarker(Vector3 pos, string name, bool addtotxtbox = false)
        {
            string str = pos.X.ToString() + ", " + pos.Y.ToString() + ", " + pos.Z.ToString();
            if (!string.IsNullOrEmpty(name))
            {
                str += ", " + name;
            }
            if (addtotxtbox)
            {
                StringBuilder sb = new();
                sb.Append(MultiFindTextBox.Text);
                if ((sb.Length > 0) && (!MultiFindTextBox.Text.EndsWith("\n")))
                {
                    sb.AppendLine();
                }
                sb.AppendLine(str);
                MultiFindTextBox.Text = sb.ToString();
            }

            return AddMarker(str);
        }
        private MapMarker AddMarker(string markerstr)
        {
            lock (markersyncroot)
            {
                MapMarker m = new();
                m.Parse(markerstr.Trim());
                m.Icon = MarkerIcon;

                Markers.Add(m);

                //ListViewItem lvi = new(new string[] { m.Name, m.WorldPos.X.ToString(), m.WorldPos.Y.ToString(), m.WorldPos.Z.ToString() });
                //lvi.Tag = m;
                //MarkersListView.Items.Add(lvi);

                return m;
            }
        }
        private void AddDefaultMarkers()
        {
            StringBuilder sb = new();
            //sb.AppendLine("1972.606, 3817.044, 0.0, Trevor Bed");
            //sb.AppendLine("94.5723, -1290.082, 0.0, Strip Club Bed");
            //sb.AppendLine("-1151.746, -1518.136, 0.0, Trevor City Bed");
            //sb.AppendLine("-1154.11, -2715.203, 0.0, Flight School");
            //sb.AppendLine("-1370.625, 56.1227, 52.82404, Golf");
            //sb.AppendLine("-1109.213, 4914.744, 0.0, Altruist Cult");
            //sb.AppendLine("-1633.087, 4736.784, 0.0, Deal Gone Wrong");
            sb.AppendLine("-2052, 3237, 1449.036, Zancudo UFO");
            sb.AppendLine("2490, 3777, 2400, Hippy UFO");
            sb.AppendLine("2577.396, 3301.573, 52.52076, Sand glyph");
            sb.AppendLine("-804.8452, 176.4936, 75.40561, bh1_48_michaels");
            sb.AppendLine("-5.757423, 529.674, 171.1747, ch2_05c_b1");
            sb.AppendLine("1971.208, 3818.237, 33.46632, cs4_10_trailer003b");
            sb.AppendLine("760.4618, 7392.803, -126.0774, cs1_09_sea_ufo");
            sb.AppendLine("501.4398, 5603.96, 795.9738, cs1_10_redeye");
            sb.AppendLine("51.3909, 5957.7568, 209.614, cs1_10_clue_moon02");
            sb.AppendLine("400.7087, 5714.5645, 605.0978, cs1_10_clue_rain01");
            sb.AppendLine("703.442, 6329.8936, 76.4973, cs1_10_clue_rain02");
            sb.AppendLine("228.7844, 5370.585, 577.2613, cs1_10_clue_moon01");
            sb.AppendLine("366.4871, 5518.0742, 704.3185, cs1_10_clue_mountain01");
            sb.AppendLine("41.64376, -779.9391, 832.4024, hw1_22_shipint");
            sb.AppendLine("-1255.392, 6795.764, -181.9927, cs1_08_sea_base");
            sb.AppendLine("4285.036, 2967.639, -184.1908, cs5_1_sea_hatch");
            sb.AppendLine("3041.498, 5584.321, 196.4748, cs2_08_generic02");
            sb.AppendLine("3406.483, 5498.655, 23.50577, cs2_08_generic01a");
            sb.AppendLine("1507.081, 6565.075, 8.681923, cs1_09_props_elec_spider1");
            sb.AppendLine("455.7852, 5586.104, 779.4382, cs1_10_elec_spider_spline052b");
            sb.AppendLine("3861.661, -4959.252, 91.49448, plg_01_nico_new");
            sb.AppendLine("-1689.308, 2174.457, 107.2592, ch1_09b_vinesleaf_28");
            sb.AppendLine("440.8488, 5810.079, 563.4703, Cock face");
            sb.AppendLine("-3955.667, -4675.212, -1274.563, Interesting...");
            sb.AppendLine("4512.627, 2623.241, 2500, Interesting...");
            sb.AppendLine("228.6058, -992.0537, -100, v_garagel");

            MultiFindTextBox.Text = sb.ToString();
            string[] lines = MultiFindTextBox.Text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                AddMarker(line);
            }

        }






        private void LoadSettings()
        {
            var s = Settings.Default;
            WindowState = s.WindowMaximized ? FormWindowState.Maximized : WindowState;
            FullScreenCheckBox.Checked = s.FullScreen;
            WireframeCheckBox.Checked = s.Wireframe;
            DeferredShadingCheckBox.Checked = s.Deferred;
            HDRRenderingCheckBox.Checked = s.HDR;
            ShadowsCheckBox.Checked = s.Shadows;
            SkydomeCheckBox.Checked = s.Skydome;
            GrassCheckBox.Checked = s.Grass;
            TimedEntitiesCheckBox.Checked = s.ShowTimedEntities;
            CollisionMeshesCheckBox.Checked = s.ShowCollisionMeshes;
            CollisionMeshRangeTrackBar.Value = s.CollisionMeshRange;
            DynamicLODCheckBox.Checked = s.DynamicLOD;
            DetailTrackBar.Value = s.DetailDist;
            WaitForChildrenCheckBox.Checked = s.WaitForChildren;
            RenderModeComboBox.SelectedIndex = Math.Max(RenderModeComboBox.FindString(s.RenderMode), 0);
            TextureSamplerComboBox.SelectedIndex = Math.Max(TextureSamplerComboBox.FindString(s.RenderTextureSampler), 0);
            TextureCoordsComboBox.SelectedIndex = Math.Max(TextureCoordsComboBox.FindString(s.RenderTextureSamplerCoord), 0);
            MarkerStyleComboBox.SelectedIndex = Math.Max(MarkerStyleComboBox.FindString(s.MarkerStyle), 0);
            LocatorStyleComboBox.SelectedIndex = Math.Max(LocatorStyleComboBox.FindString(s.LocatorStyle), 0);
            MarkerDepthClipCheckBox.Checked = s.MarkerDepthClip;
            AnisotropicFilteringCheckBox.Checked = s.AnisotropicFiltering;
            BoundsStyleComboBox.SelectedIndex = Math.Max(BoundsStyleComboBox.FindString(s.BoundsStyle), 0);
            BoundsDepthClipCheckBox.Checked = s.BoundsDepthClip;
            BoundsRangeTrackBar.Value = s.BoundsRange;
            ErrorConsoleCheckBox.Checked = s.ShowErrorConsole;
            StatusBarCheckBox.Checked = s.ShowStatusBar;
            SnapGridSizeUpDown.Value = (decimal)s.SnapGridSize;
            SetRotationSnapping(s.SnapRotationDegrees);
            TimeOfDayTrackBar.Value = s.TimeOfDay;
            LODLightsCheckBox.Checked = s.LODLights;
            WeatherComboBox.SelectedIndex = Math.Max(WeatherComboBox.FindString(s.Weather), 0);
            WeatherRegionComboBox.SelectedIndex = Math.Max(WeatherRegionComboBox.FindString(s.Region), 0);
            Renderer.individualcloudfrag = s.Clouds;
            NaturalAmbientLightCheckBox.Checked = s.NaturalAmbientLight;
            ArtificialAmbientLightCheckBox.Checked = s.ArtificialAmbientLight;
            SavePositionCheckBox.Checked = s.SavePosition;
            SaveTimeOfDayCheckBox.Checked = s.SaveTimeOfDay;
            
            SetTimeOfDay(s.TimeOfDay);
            Renderer.SetWeatherType(s.Weather);
            

            EnableModsCheckBox.Checked = s.EnableMods;
            DlcLevelComboBox.Text = s.DLC;
            gameFileCache.SelectedDlc = s.DLC;
            EnableDlcCheckBox.Checked = !string.IsNullOrEmpty(s.DLC);
        }
        private void SaveSettings()
        {
            var s = Settings.Default;
            s.WindowMaximized = (WindowState == FormWindowState.Maximized);
            s.FullScreen = FullScreenCheckBox.Checked;
            s.Wireframe = WireframeCheckBox.Checked;
            s.Deferred = DeferredShadingCheckBox.Checked;
            s.HDR = HDRRenderingCheckBox.Checked;
            s.Shadows = ShadowsCheckBox.Checked;
            s.Skydome = SkydomeCheckBox.Checked;
            s.Grass = GrassCheckBox.Checked;
            s.ShowTimedEntities = TimedEntitiesCheckBox.Checked;
            s.ShowCollisionMeshes = CollisionMeshesCheckBox.Checked;
            s.CollisionMeshRange = CollisionMeshRangeTrackBar.Value;
            s.DynamicLOD = DynamicLODCheckBox.Checked;
            s.DetailDist = DetailTrackBar.Value;
            s.WaitForChildren = WaitForChildrenCheckBox.Checked;
            s.RenderMode = RenderModeComboBox.Text;
            s.RenderTextureSampler = TextureSamplerComboBox.Text;
            s.RenderTextureSamplerCoord = TextureCoordsComboBox.Text;
            s.MarkerStyle = MarkerStyleComboBox.Text;
            s.LocatorStyle = LocatorStyleComboBox.Text;
            s.MarkerDepthClip = MarkerDepthClipCheckBox.Checked;
            s.AnisotropicFiltering = AnisotropicFilteringCheckBox.Checked;
            s.BoundsStyle = BoundsStyleComboBox.Text;
            s.BoundsDepthClip = BoundsDepthClipCheckBox.Checked;
            s.BoundsRange = BoundsRangeTrackBar.Value;
            s.ShowErrorConsole = ErrorConsoleCheckBox.Checked;
            s.ShowStatusBar = StatusBarCheckBox.Checked;
            s.SnapRotationDegrees = (float)SnapAngleUpDown.Value;
            s.SnapGridSize = (float)SnapGridSizeUpDown.Value;
            s.LODLights = LODLightsCheckBox.Checked;
            s.NaturalAmbientLight = NaturalAmbientLightCheckBox.Checked;
            s.ArtificialAmbientLight = ArtificialAmbientLightCheckBox.Checked;
            s.SavePosition = SavePositionCheckBox.Checked;
            s.SaveTimeOfDay = SaveTimeOfDayCheckBox.Checked;
            if (s.SavePosition)
            {
                s.StartPosition = FloatUtil.GetVector3String(camEntity?.Position ?? camera.Position);
                s.StartRotation = FloatUtil.GetVector3String(camera.CurrentRotation);
                // Orbit rotation is relative to the followed entity, including
                // orientations set by camera bookmarks and the Go To command.
                var orientation = camera.FollowEntity?.Orientation ?? Quaternion.Identity;
                s.StartCameraOrientation = FloatUtil.GetVector4String(new Vector4(
                    orientation.X, orientation.Y, orientation.Z, orientation.W));
            }
            if (s.SaveTimeOfDay)
            {
                s.TimeOfDay = TimeOfDayTrackBar.Value;
                s.Weather = WeatherComboBox.Text;
                s.Region = WeatherRegionComboBox.Text;
                s.Clouds = CloudsComboBox.Text;
            }

            //additional settings from gamefilecache...
            s.EnableMods = gameFileCache.EnableMods;
            s.DLC = gameFileCache.EnableDlc ? gameFileCache.SelectedDlc : "";

            s.Save();
        }
        private void ResetSettings()
        {
            if (MessageBox.Show("Are you sure you want to reset all settings to their default values?", "Reset All Settings", MessageBoxButtons.YesNo) != DialogResult.Yes) return;

            Settings.Default.Reset();
            LoadSettings();

            if (camEntity != null)
            {
                camEntity.Position = FloatUtil.ParseVector3String(Settings.Default.StartPosition);
                camEntity.Orientation = Quaternion.LookAtLH(Vector3.Zero, Vector3.Up, Vector3.ForwardLH);
                camera.CurrentRotation = Vector3.Zero;
                camera.TargetRotation = Vector3.Zero;
            }

            MessageBox.Show("All settings have been reset to their default values. Please restart CodeWalker.");
        }

        private void ShowSettingsForm(string tab = "")
        {
            if (SettingsForm == null)
            {
                SettingsForm = new SettingsForm(this);
                SettingsForm.Show(this);
            }
            else
            {
                if (SettingsForm.WindowState == FormWindowState.Minimized)
                {
                    SettingsForm.WindowState = FormWindowState.Normal;
                }
                SettingsForm.Focus();
            }
            if (!string.IsNullOrEmpty(tab))
            {
                SettingsForm.SelectTab(tab);
            }
        }
        public void OnSettingsFormClosed()
        {
            //called by the SettingsForm when it's closed.
            SettingsForm = null;
        }




        private void MarkUndoStart(Widget w)
        {
            if (!SelectedItem.CanMarkUndo()) return;
            if (Widget is TransformWidget)
            {
                UndoStartPosition = Widget.Position;
                UndoStartRotation = Widget.Rotation;
                UndoStartScale = Widget.Scale;
            }
        }
        private void MarkUndoEnd(Widget w)
        {
            if (!SelectedItem.CanMarkUndo()) return;
            TransformWidget tw = Widget as TransformWidget;
            UndoStep? s = null;
            if (tw != null)
            {
                s = SelectedItem.CreateUndoStep(tw.Mode, UndoStartPosition, UndoStartRotation, UndoStartScale, this, EditEntityPivot);
            }
            if (s != null)
            {
                RedoSteps.Clear();
                UndoSteps.Push(s);
                UpdateUndoUI();
            }
        }
        private void Undo()
        {
            if (UndoSteps.Count == 0) return;
            var s = UndoSteps.Pop();
            RedoSteps.Push(s);

            s.Undo(this, ref SelectedItem);

            if (ProjectForm != null)
            {
                ProjectForm.OnWorldSelectionModified(SelectedItem);
            }

            UpdateUndoUI();
        }
        private void Redo()
        {
            if (RedoSteps.Count == 0) return;
            var s = RedoSteps.Pop();
            UndoSteps.Push(s);

            s.Redo(this, ref SelectedItem);

            if (ProjectForm != null)
            {
                ProjectForm.OnWorldSelectionModified(SelectedItem);
            }

            UpdateUndoUI();
        }
        private void UpdateUndoUI()
        {
            ToolbarUndoButton.DropDownItems.Clear();
            ToolbarRedoButton.DropDownItems.Clear();
            int i = 0;
            foreach (var step in UndoSteps)
            {
                var button = ToolbarUndoButton.DropDownItems.Add(step.ToString());
                button.Tag = step;
                button.Click += ToolbarUndoListButton_Click;
                i++;
                if (i >= 10) break;
            }
            i = 0;
            foreach (var step in RedoSteps)
            {
                var button = ToolbarRedoButton.DropDownItems.Add(step.ToString());
                button.Tag = step;
                button.Click += ToolbarRedoListButton_Click;
                i++;
                if (i >= 10) break;
            }
            ToolbarUndoButton.Enabled = (UndoSteps.Count > 0);
            ToolbarRedoButton.Enabled = (RedoSteps.Count > 0);
        }



        private void EnableCacheDependentUI()
        {
            try
            {
                if (IsDisposed || IsHandleCreated == false) return;
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => { EnableCacheDependentUI(); }));
                }
                else
                {
                    ToolbarNewButton.Enabled = true;
                    ToolbarOpenButton.Enabled = true;
                    ToolbarProjectWindowButton.Enabled = true;
                    ToolsMenuProjectWindow.Enabled = true;
                    ToolsMenuCutsceneViewer.Enabled = true;
                    ToolsMenuAudioExplorer.Enabled = true;
                    ToolsMenuBinarySearch.Enabled = true;
                    ToolsMenuJenkInd.Enabled = true;
                }
            }
            catch (ObjectDisposedException) { }
            catch (Win32Exception) { }
            catch (InvalidOperationException) { }
        }
        private void EnableDLCModsUI()
        {
            try
            {
                if (IsDisposed || IsHandleCreated == false) return;
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => { EnableDLCModsUI(); }));
                }
                else
                {
                    EnableDlcCheckBox.Enabled = true;
                    EnableModsCheckBox.Enabled = true;
                    HideNorthYanktonCheckBox.Enabled = true;
                    HideCayoPericoCheckBox.Enabled = true;
                    DlcLevelComboBox.Enabled = true;
                }
            }
            catch (ObjectDisposedException) { }
            catch (Win32Exception) { }
            catch (InvalidOperationException) { }
        }


        public void SetCurrentSaveItem(string filename)
        {
            bool enable = !string.IsNullOrEmpty(filename);
            ToolbarSaveButton.ToolTipText = enable ? ("Save " + filename) : "Save";
            ToolbarSaveButton.Enabled = enable;
            ToolbarSaveAllButton.Enabled = enable;
        }
        public void EnableYmapUI(bool enable, string filename)
        {
            string type = "entity";
            switch (SelectionMode)
            {
                case MapSelectionMode.CarGenerator: type = "car generator"; break;
            }

            ToolbarAddItemButton.ToolTipText = "Add " + type + (enable ? (" to " + filename) : "");
            ToolbarAddItemButton.Enabled = enable;
        }
        public void EnableYbnUI(bool enable, string filename)
        {

            if (enable) //only do something if a ybn is selected - EnableYmapUI will handle the no selection case.. 
            {
                //ToolbarAddItemButton.ToolTipText = "Add " + type + (enable ? (" to " + filename) : "");
                //ToolbarAddItemButton.Enabled = enable;
            }
        }
        public void EnableYndUI(bool enable, string filename)
        {
            string type = "node";
            switch (SelectionMode)
            {
                case MapSelectionMode.Path: type = "node"; break;
            }

            if (enable) //only do something if a ynd is selected - EnableYmapUI will handle the no selection case.. 
            {
                ToolbarAddItemButton.ToolTipText = "Add " + type + (enable ? (" to " + filename) : "");
                ToolbarAddItemButton.Enabled = enable;
            }
        }
        public void EnableYnvUI(bool enable, string filename)
        {
            string type = "polygon";
            switch (SelectionMode)
            {
                case MapSelectionMode.NavMesh: type = "polygon"; break;
            }

            if (enable) //only do something if a ynv is selected - EnableYmapUI will handle the no selection case.. 
            {
                ToolbarAddItemButton.ToolTipText = "Add " + type + (enable ? (" to " + filename) : "");
                ToolbarAddItemButton.Enabled = enable;
            }
        }
        public void EnableTrainsUI(bool enable, string filename)
        {
            string type = "node";
            switch (SelectionMode)
            {
                case MapSelectionMode.TrainTrack: type = "node"; break;
            }

            if (enable) //only do something if a track is selected - EnableYmapUI will handle the no selection case.. 
            {
                ToolbarAddItemButton.ToolTipText = "Add " + type + (enable ? (" to " + filename) : "");
                ToolbarAddItemButton.Enabled = enable;
            }
        }
        public void EnableScenarioUI(bool enable, string filename)
        {
            string type = "scenario point";
            switch (SelectionMode)
            {
                case MapSelectionMode.Scenario: type = "scenario point"; break;
            }

            if (enable) //only do something if a scenario is selected - EnableYmapUI will handle the no selection case.. 
            {
                ToolbarAddItemButton.ToolTipText = "Add " + type + (enable ? (" to " + filename) : "");
                ToolbarAddItemButton.Enabled = enable;
            }
        }
        public void EnableAudioUI(bool enable, string filename) //TODO
        {

        }


        private void New()
        {
            ShowProjectForm();

            if (ProjectForm.IsProjectLoaded)
            {
                ProjectForm.NewYmap();
            }
            else
            {
                ProjectForm.NewProject();
            }
        }
        private void NewProject()
        {
            ShowProjectForm();
            ProjectForm.NewProject();
        }
        private void NewYmap()
        {
            ShowProjectForm();
            ProjectForm.NewYmap();
        }
        private void NewYtyp()
        {
            ShowProjectForm();
            ProjectForm.NewYtyp();
        }
        private void NewYbn()
        {
            ShowProjectForm();
            ProjectForm.NewYbn();
        }
        private void NewYnd()
        {
            ShowProjectForm();
            ProjectForm.NewYnd();
        }
        private void NewTrainTrack()
        {
            ShowProjectForm();
            ProjectForm.NewTrainTrack();
        }
        private void NewScenario()
        {
            ShowProjectForm();
            ProjectForm.NewScenario();
        }
        private void Open()
        {
            ShowProjectForm();

            if (ProjectForm.IsProjectLoaded)
            {
                ProjectForm.OpenFiles();
            }
            else
            {
                ProjectForm.OpenProject();
            }
        }
        private void OpenProject()
        {
            ShowProjectForm();
            ProjectForm.OpenProject();
        }
        private void OpenFiles()
        {
            ShowProjectForm();
            ProjectForm.OpenFiles();
        }
        private void OpenFolder()
        {
            ShowProjectForm();
            ProjectForm.OpenFolder();
        }
        private void Save()
        {
            if (ProjectForm == null) return;
            ProjectForm.Save();
        }
        private void SaveAll()
        {
            if (ProjectForm == null) return;
            ProjectForm.SaveAll();
        }


        private void AddItem()
        {
            if (ProjectForm == null) return;
            switch (SelectionMode)
            {
                case MapSelectionMode.Entity: ProjectForm.NewEntity(); break;
                case MapSelectionMode.CarGenerator: ProjectForm.NewCarGen(); break;
                case MapSelectionMode.Path: ProjectForm.NewPathNode(); break;
                case MapSelectionMode.NavMesh: ProjectForm.NewNavPoly(); break; //.NewNavPoint/.NewNavPortal//how to add points/portals? project window
                case MapSelectionMode.TrainTrack: ProjectForm.NewTrainNode(); break;
                case MapSelectionMode.Scenario: ProjectForm.NewScenarioNode(); break; //how to add different node types? project window
                case MapSelectionMode.Audio: ProjectForm.NewAudioAmbientZone(); break; //.NewAudioEmitter // how to add emitters as well? project window
            }
        }
        private void CopyItem()
        {
            CopiedItem = SelectedItem;
            ToolbarPasteButton.Enabled = CopiedItem.CanCopyPaste && (ProjectForm != null); //ToolbarAddItemButton.Enabled;
        }
        private void PasteItem()
        {
            if ((ProjectForm != null) && CopiedItem.CanCopyPaste)
            {
                SelectObject(ProjectForm.NewObject(CopiedItem, (CopiedItem.MultipleSelectionItems != null)));
            }
        }
        private void CloneItem()
        {
            if ((ProjectForm != null) && SelectedItem.CanCopyPaste)
            {
                SelectObject(ProjectForm.NewObject(SelectedItem, true));
            }
        }
        private void DeleteItem()
        {
            if (ProjectForm != null)
            {
                ProjectForm.DeleteObject(SelectedItem);
                SelectItem(null);
            }
            else
            {
                DeleteItem(SelectedItem);
                SelectItem(null);
            }
        }
        private void DeleteItem(MapSelection item)
        {
            if (item.MultipleSelectionItems != null)
            {
                for (int i = 0; i < item.MultipleSelectionItems.Length; i++)
                {
                    DeleteItem(item.MultipleSelectionItems[i]);
                }
            }
            else if (item.CollisionVertex != null) DeleteCollisionVertex(item.CollisionVertex);
            else if (item.CollisionPoly != null) DeleteCollisionPoly(item.CollisionPoly);
            else if (item.CollisionBounds != null) DeleteCollisionBounds(item.CollisionBounds);
            else if (item.EntityDef != null) DeleteEntity(item.EntityDef);
            else if (item.CarGenerator != null) DeleteCarGen(item.CarGenerator);
            else if (item.LodLight != null) DeleteLodLight(item.LodLight);
            else if (item.BoxOccluder != null) DeleteBoxOccluder(item.BoxOccluder);
            else if (item.OccludeModelTri != null) DeleteOccludeModelTriangle(item.OccludeModelTri);
            else if (item.PathNode != null) DeletePathNode(item.PathNode);
            else if (item.NavPoly != null) DeleteNavPoly(item.NavPoly);
            else if (item.NavPoint != null) DeleteNavPoint(item.NavPoint);
            else if (item.NavPortal != null) DeleteNavPortal(item.NavPortal);
            else if (item.TrainTrackNode != null) DeleteTrainNode(item.TrainTrackNode);
            else if (item.ScenarioNode != null) DeleteScenarioNode(item.ScenarioNode);
            else if (item.Audio?.AmbientZone != null) DeleteAudioAmbientZone(item.Audio);
            else if (item.Audio?.AmbientRule != null) DeleteAudioAmbientRule(item.Audio);
            else if (item.Audio?.StaticEmitter != null) DeleteAudioStaticEmitter(item.Audio);
        }
        private void DeleteEntity(YmapEntityDef? ent)
        {
            if (ent == null) return;

            //project not open, or entity not selected there, just remove the entity from the ymap/mlo...
            var ymap = ent.Ymap;
            var instance = ent.MloParent?.MloInstance;
            if (ymap == null)
            {
                if (instance != null)
                {
                    try
                    {
                        if (!instance.DeleteEntity(ent))
                        {
                            SelectItem(null);
                        }
                    }
                    catch (Exception e) // various failures can happen here.
                    {
                        MessageBox.Show("Unable to remove entity..." + Environment.NewLine + e.Message);
                    }
                }
            }
            else if (!ymap.RemoveEntity(ent))
            {
                MessageBox.Show("Unable to remove entity.");
            }
            else
            {
                SelectItem(null);
            }
        }
        private void DeleteCarGen(YmapCarGen? cargen)
        {
            if (cargen == null) return;

            //project not open, or cargen not selected there, just remove the cargen from the ymap...
            var ymap = cargen.Ymap;
            if (!ymap.RemoveCarGen(cargen))
            {
                MessageBox.Show("Unable to remove car generator.");
            }
            else
            {
                SelectItem(null);
            }
        }
        private void DeleteLodLight(YmapLODLight? lodlight)
        {
            if (lodlight == null) return;

            //project not open, or lodlight not selected there, just remove the lodlight from the ymap...
            var ymap = lodlight.Ymap;
            if (!ymap.RemoveLodLight(lodlight))
            {
                MessageBox.Show("Unable to remove LOD light.");
            }
            else
            {
                SelectItem(null);
            }
        }
        private void DeleteBoxOccluder(YmapBoxOccluder? box)
        {
            if (box == null) return;

            //project not open, or box not selected there, just remove the box from the ymap...
            var ymap = box.Ymap;
            if (!ymap.RemoveBoxOccluder(box))
            {
                MessageBox.Show("Unable to remove box occluder.");
            }
            else
            {
                SelectItem(null);
            }
        }
        private void DeleteOccludeModelTriangle(YmapOccludeModelTriangle? tri)
        {
            if (tri == null) return;

            //project not open, or tri not selected there, just remove the tri from the ymap...
            var ymap = tri.Ymap;
            if (!ymap.RemoveOccludeModelTriangle(tri))
            {
                MessageBox.Show("Unable to remove occlude model triangle.");
            }
            else
            {
                UpdateOccludeModelGraphics(tri.Model);
                SelectItem(null);
            }
        }
        private void DeletePathNode(YndNode? pathnode)
        {
            if (pathnode == null) return;
            if (pathnode.Ynd == null) return;

            //project not open, or node not selected there, just remove the node from the ynd...
            var ynd = pathnode.Ynd;
            if (!ynd.RemoveYndNode(Space, pathnode, true, out var affectedFiles))
            {
                MessageBox.Show("Unable to remove path node.");
            }
            else
            {
                UpdatePathNodeGraphics(pathnode, false);
                ProjectForm?.AddYndToProject(ynd);

                foreach (var affectedFile in affectedFiles)
                {
                    UpdatePathYndGraphics(affectedFile, false);
                    ProjectForm?.AddYndToProject(affectedFile);
                    affectedFile.HasChanged = true;
                }


                SelectItem(null);
            }
        }
        private void DeleteNavPoly(YnvPoly? navpoly)
        {
            if (navpoly == null) return;

            //project not open, or nav poly not selected there, just remove the poly from the ynv...
            var ynv = navpoly.Ynv;
            if (!ynv.RemovePoly(navpoly))
            {
                MessageBox.Show("Unable to remove nav poly. NavMesh editing TODO!");
            }
            else
            {
                UpdateNavPolyGraphics(navpoly, false);
                SelectItem(null);
            }
        }
        private void DeleteNavPoint(YnvPoint? navpoint)
        {
            if (navpoint == null) return;

            //project not open, or nav point not selected there, just remove the point from the ynv...
            var ynv = navpoint.Ynv;
            if (!ynv.RemovePoint(navpoint))
            {
                MessageBox.Show("Unable to remove nav point. NavMesh editing TODO!");
            }
            else
            {
                UpdateNavPointGraphics(navpoint, false);
                SelectItem(null);
            }
        }
        private void DeleteNavPortal(YnvPortal? navportal)
        {
            if (navportal == null) return;

            //project not open, or nav portal not selected there, just remove the portal from the ynv...
            var ynv = navportal.Ynv;
            if (!ynv.RemovePortal(navportal))
            {
                MessageBox.Show("Unable to remove nav portal. NavMesh editing TODO!");
            }
            else
            {
                UpdateNavPortalGraphics(navportal, false);
                SelectItem(null);
            }
        }
        private void DeleteTrainNode(TrainTrackNode? trainnode)
        {
            if (trainnode == null) return;

            //project not open, or train node not selected there, just remove the node from the train track...
            var track = trainnode.Track;
            if (!track.RemoveNode(trainnode))
            {
                MessageBox.Show("Unable to remove train track node.");
            }
            else
            {
                UpdateTrainTrackNodeGraphics(trainnode, false);
                SelectItem(null);
            }
        }
        private void DeleteScenarioNode(ScenarioNode? scenariopt)
        {
            if (scenariopt == null) return;

            //project not open, or scenario point not selected there, just remove the point from the region...
            var region = scenariopt.Region.Ymt.ScenarioRegion;
            if (!region.RemoveNode(scenariopt))
            {
                MessageBox.Show("Unable to remove scenario point.");
            }
            else
            {
                UpdateScenarioGraphics(scenariopt.Ymt, false);
                SelectItem(null);
            }
        }
        private void DeleteAudioAmbientZone(AudioPlacement? audio)
        {
            if (audio == null) return;

            //project not open, or zone not selected there, just remove the zone from the rel...
            var rel = audio.RelFile;
            if (!rel.RemoveRelData(audio.AmbientZone))
            {
                MessageBox.Show("Unable to remove audio ambient zone.");
            }
            else
            {
                SelectItem(null);
            }
        }
        private void DeleteAudioAmbientRule(AudioPlacement? audio)
        {
            if (audio == null) return;

            //project not open, or rule not selected there, just remove the rule from the rel...
            var rel = audio.RelFile;
            if (!rel.RemoveRelData(audio.AmbientRule))
            {
                MessageBox.Show("Unable to remove audio ambient rule.");
            }
            else
            {
                SelectItem(null);
            }
        }
        private void DeleteAudioStaticEmitter(AudioPlacement? audio)
        {
            if (audio == null) return;

            //project not open, or emitter not selected there, just remove the emitter from the rel...
            var rel = audio.RelFile;
            if (!rel.RemoveRelData(audio.StaticEmitter))
            {
                MessageBox.Show("Unable to remove audio static emitter.");
            }
            else
            {
                SelectItem(null);
            }
        }
        private void DeleteCollisionVertex(BoundVertex? vertex)
        {
            if (vertex == null) return;

            //project not open, or vertex not selected there, just remove the vertex from the geometry...
            var bgeom = vertex.Owner;
            if ((bgeom == null) || (!bgeom.DeleteVertex(vertex.Index)))
            {
                MessageBox.Show("Unable to remove vertex.");
            }
            else
            {
                UpdateCollisionBoundsGraphics(bgeom);
                SelectItem(null);
            }
        }
        private void DeleteCollisionPoly(BoundPolygon? poly)
        {
            if (poly == null) return;

            //project not open, or polygon not selected there, just remove the vertex from the geometry...
            var bgeom = poly.Owner;
            if ((bgeom == null) || (!bgeom.DeletePolygon(poly)))
            {
                MessageBox.Show("Unable to remove polygon.");
            }
            else
            {
                UpdateCollisionBoundsGraphics(bgeom);
                SelectItem(null);
            }
        }
        private void DeleteCollisionBounds(Bounds? bounds)
        {
            if (bounds == null) return;

            var parent = bounds.Parent;
            if (parent != null)
            {
                parent.DeleteChild(bounds);
                UpdateCollisionBoundsGraphics(parent);
            }
            else
            {
                var ybn = bounds.GetRootYbn();
                ybn.RemoveBounds(bounds);
            }

            SelectItem(null);
        }


        private void SetMouseSelect(bool enable)
        {
            MouseSelectEnabled = enable;
            MouseSelectCheckBox.Checked = enable;
            ToolbarSelectButton.Checked = enable;

            if (InfoForm != null)
            {
                InfoForm.SetSelectionMode(SelectionModeStr, MouseSelectEnabled);
            }
        }

        private void SetWidgetMode(string mode)
        {
            ToolbarMoveButton.Checked = false;
            ToolbarRotateButton.Checked = false;
            ToolbarScaleButton.Checked = false;

            lock (Renderer.RenderSyncRoot)
            {
                switch (mode)
                {
                    case "Default":
                        Widget.Mode = WidgetMode.Default;
                        iseditmode = false;
                        break;
                    case "Position":
                        Widget.Mode = WidgetMode.Position;
                        iseditmode = true;
                        ToolbarMoveButton.Checked = true;
                        break;
                    case "Rotation":
                        Widget.Mode = WidgetMode.Rotation;
                        iseditmode = true;
                        ToolbarRotateButton.Checked = true;
                        break;
                    case "Scale":
                        Widget.Mode = WidgetMode.Scale;
                        iseditmode = true;
                        ToolbarScaleButton.Checked = true;
                        break;
                }
            }
        }

        private void SetWidgetSpace(string space)
        {
            foreach (var child in ToolbarTransformSpaceButton.DropDownItems)
            {
                var childi = child as ToolStripMenuItem;
                if (childi != null)
                {
                    childi.Checked = false;
                }
            }

            lock (Renderer.RenderSyncRoot)
            {
                switch (space)
                {
                    case "World space":
                        Widget.ObjectSpace = false;
                        ToolbarTransformSpaceButton.Image = ToolbarWorldSpaceButton.Image;
                        ToolbarWorldSpaceButton.Checked = true;
                        break;
                    case "Object space":
                        Widget.ObjectSpace = true;
                        ToolbarTransformSpaceButton.Image = ToolbarObjectSpaceButton.Image;
                        ToolbarObjectSpaceButton.Checked = true;
                        break;
                }
            }
        }

        private void ToggleWidgetSpace()
        {
            SetWidgetSpace(Widget.ObjectSpace ? "World space" : "Object space");
        }



        private void SetFullscreen(bool fullscreen)
        {
            lock (Renderer.RenderSyncRoot)
            {
                if (fullscreen)
                {
                    FormBorderStyle = FormBorderStyle.None;
                    WindowState = FormWindowState.Maximized;
                }
                else
                {
                    WindowState = FormWindowState.Normal;
                    FormBorderStyle = FormBorderStyle.Sizable;
                }
            }
        }

        private void SetBoundsMode(string modestr)
        {
            BoundsShaderMode mode = BoundsShaderMode.None;
            switch (modestr)
            {
                case "Boxes":
                    mode = BoundsShaderMode.Box;
                    break;
                case "Spheres":
                    mode = BoundsShaderMode.Sphere;
                    break;
            }
            Renderer.boundsmode = mode;
        }



        private void SetSelectionMode(string modestr)
        {

            foreach (var child in ToolbarSelectButton.DropDownItems)
            {
                var childi = child as ToolStripMenuItem;
                if (childi != null)
                {
                    childi.Checked = false;
                }
            }

            MapSelectionMode mode = MapSelectionMode.Entity;
            switch (modestr)
            {
                default:
                case "Entity":
                    mode = MapSelectionMode.Entity;
                    ToolbarSelectEntityButton.Checked = true;
                    break;
                case "Entity Extension":
                    mode = MapSelectionMode.EntityExtension;
                    ToolbarSelectEntityExtensionButton.Checked = true;
                    break;
                case "Archetype Extension":
                    mode = MapSelectionMode.ArchetypeExtension;
                    ToolbarSelectArchetypeExtensionButton.Checked = true;
                    break;
                case "Time Cycle Modifier":
                    mode = MapSelectionMode.TimeCycleModifier;
                    ToolbarSelectTimeCycleModifierButton.Checked = true;
                    break;
                case "Car Generator":
                    mode = MapSelectionMode.CarGenerator;
                    ToolbarSelectCarGeneratorButton.Checked = true;
                    break;
                case "Grass":
                    mode = MapSelectionMode.Grass;
                    ToolbarSelectGrassButton.Checked = true;
                    break;
                case "Water Quad":
                    mode = MapSelectionMode.WaterQuad;
                    ToolbarSelectWaterQuadButton.Checked = true;
                    break;
                case "Water Calming Quad":
                    mode = MapSelectionMode.CalmingQuad;
                    ToolbarSelectCalmingQuadButton.Checked = true;
                    break;
                case "Water Wave Quad":
                    mode = MapSelectionMode.WaveQuad;
                    ToolbarSelectWaveQuadButton.Checked = true;
                    break;
                case "Collision":
                    mode = MapSelectionMode.Collision;
                    ToolbarSelectCollisionButton.Checked = true;
                    break;
                case "Nav Mesh":
                    mode = MapSelectionMode.NavMesh;
                    ToolbarSelectNavMeshButton.Checked = true;
                    break;
                case "Path":
                    mode = MapSelectionMode.Path;
                    ToolbarSelectPathButton.Checked = true;
                    break;
                case "Train Track":
                    mode = MapSelectionMode.TrainTrack;
                    ToolbarSelectTrainTrackButton.Checked = true;
                    break;
                case "Lod Lights":
                    mode = MapSelectionMode.LodLights;
                    ToolbarSelectLodLightsButton.Checked = true;
                    break;
                case "Mlo Instance":
                    mode = MapSelectionMode.MloInstance;
                    ToolbarSelectMloInstanceButton.Checked = true;
                    break;
                case "Scenario":
                    mode = MapSelectionMode.Scenario;
                    ToolbarSelectScenarioButton.Checked = true;
                    break;
                case "Audio":
                    mode = MapSelectionMode.Audio;
                    ToolbarSelectAudioButton.Checked = true;
                    break;
                case "Occlusion":
                    mode = MapSelectionMode.Occlusion;
                    ToolbarSelectOcclusionButton.Checked = true;
                    break;

            }
            SelectionMode = mode;
            SelectionModeStr = modestr;
            Renderer.SelectionMode = mode;

            if (SelectionModeComboBox.Text != modestr)
            {
                SelectionModeComboBox.Text = modestr;
            }

            if (InfoForm != null)
            {
                InfoForm.SetSelectionMode(modestr, MouseSelectEnabled);
            }
        }


        private void SetSnapMode(WorldSnapMode mode)
        {
            foreach (var child in ToolbarSnapButton.DropDownItems)
            {
                var childi = child as ToolStripMenuItem;
                if (childi != null)
                {
                    childi.Checked = false;
                }
            }

            ToolbarSnapButton.Checked = (mode != WorldSnapMode.None);

            ToolStripMenuItem? selItem = null;

            switch (mode)
            {
                case WorldSnapMode.Ground:
                    selItem = ToolbarSnapToGroundButton;
                    break;
                case WorldSnapMode.Grid:
                    selItem = ToolbarSnapToGridButton;
                    break;
                case WorldSnapMode.Hybrid:
                    selItem = ToolbarSnapToGroundGridButton;
                    break;
            }

            if (selItem != null)
            {
                selItem.Checked = true;
                ToolbarSnapButton.Image = selItem.Image;
                ToolbarSnapButton.Text = selItem.Text;
                ToolbarSnapButton.ToolTipText = selItem.ToolTipText;
            }

            if (mode != WorldSnapMode.None)
            {
                SnapModePrev = mode;
            }
            SnapMode = mode;
        }

        private void SetRotationSnapping(float degrees)
        {
            Widget.SnapAngleDegrees = degrees;

            foreach (var child in ToolbarRotationSnappingButton.DropDownItems)
            {
                var childi = child as ToolStripMenuItem;
                if (childi != null)
                {
                    childi.Checked = false;
                }
            }

            var selItem = ToolbarRotationSnappingCustomButton;

            switch (degrees)
            {
                case 0.0f:
                    selItem = ToolbarRotationSnappingOffButton;
                    break;
                case 1.0f:
                    selItem = ToolbarRotationSnapping1Button;
                    break;
                case 2.0f:
                    selItem = ToolbarRotationSnapping2Button;
                    break;
                case 5.0f:
                    selItem = ToolbarRotationSnapping5Button;
                    break;
                case 10.0f:
                    selItem = ToolbarRotationSnapping10Button;
                    break;
                case 45.0f:
                    selItem = ToolbarRotationSnapping45Button;
                    break;
                case 90.0f:
                    selItem = ToolbarRotationSnapping90Button;
                    break;
            }

            if (selItem != null)
            {
                selItem.Checked = true;
            }

            var cval = (float)SnapAngleUpDown.Value;
            if (cval != degrees)
            {
                SnapAngleUpDown.Value = (decimal)degrees;
            }

        }


        private void SetCameraMode(string modestr)
        {
            foreach (var child in ToolbarCameraModeButton.DropDownItems)
            {
                var childi = child as ToolStripMenuItem;
                if (childi != null)
                {
                    childi.Checked = false;
                }
            }


            Renderer.SetCameraMode(modestr);

            switch (modestr)
            {
                case "Perspective":
                    MapViewEnabled = false;
                    ToolbarCameraModeButton.Image = ToolbarCameraPerspectiveButton.Image;
                    ToolbarCameraPerspectiveButton.Checked = true;
                    break;
                case "Orthographic":
                    MapViewEnabled = false;
                    ToolbarCameraModeButton.Image = ToolbarCameraOrthographicButton.Image;
                    ToolbarCameraOrthographicButton.Checked = true;
                    break;
                case "2D Map":
                    MapViewEnabled = true;
                    ToolbarCameraModeButton.Image = ToolbarCameraMapViewButton.Image;
                    ToolbarCameraMapViewButton.Checked = true;
                    break;
            }

            FieldOfViewTrackBar.Enabled = !MapViewEnabled;
            MapViewDetailTrackBar.Enabled = MapViewEnabled;


            if (CameraModeComboBox.Text != modestr)
            {
                CameraModeComboBox.Text = modestr;
            }


        }

        private void ToggleCameraMode()
        {
            SetCameraMode(MapViewEnabled ? "Perspective" : "2D Map");
        }


        private void ToggleToolbar()
        {
            ToolbarPanel.Visible = !ToolbarPanel.Visible;
            ShowToolbarCheckBox.Checked = ToolbarPanel.Visible;
        }




        public void ShowSubtitle(string text, float duration)
        {
            if (IsDisposed || IsHandleCreated == false) return;
            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(() => { ShowSubtitle(text, duration); }));
                }
                catch (ObjectDisposedException) { }
                catch (Win32Exception) { }
                catch (InvalidOperationException) { }
                return;
            }

            SubtitleLabel.Text = text;
            SubtitleLabel.Visible = true;
            SubtitleTimer.Interval = (int)(duration * 1000.0f);
            SubtitleTimer.Enabled = true;

        }




        private void SetTimeOfDay(int minute)
        {
            float hour = minute / 60.0f;
            UpdateTimeOfDayLabel();
            lock (Renderer.RenderSyncRoot)
            {
                Renderer.SetTimeOfDay(hour);
            }
        }




        private void TryCreateNodeLink()//TODO: move this to project window
        {
            if (SelectionMode != MapSelectionMode.Path)
            {
                return;
            }

            var selection = SelectedItem.MultipleSelectionItems;
            if (selection?.Length != 2)
            {
                MessageBox.Show("Please select 2 nodes to perform this action",
                    "Join Failed.",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var n1 = selection[0].PathNode;
            var n2 = selection[1].PathNode;
            if (n1 != null && n2 != null)
            {
                var link = n1.AddLink(n2);
                if (link == null)
                {
                    MessageBox.Show("Failed to join nodes. The nodes are likely too far away!", "Join Failed.", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var copy = n1.Links.FirstOrDefault();

                link.SetForwardLanesBidirectionally(copy?.LaneCountBackward ?? 1);
                link.SetBackwardLanesBidirectionally(copy?.LaneCountForward ?? 1);
                UpdatePathYndGraphics(n1.Ynd, false);
                UpdatePathYndGraphics(n2.Ynd, false);
            }
        }

        private void TryCreateNodeShortcut()//TODO: move this to project window
        {
            if (SelectionMode != MapSelectionMode.Path)
            {
                return;
            }

            var selection = SelectedItem.MultipleSelectionItems;
            if (selection?.Length != 2)
            {
                MessageBox.Show("Please select 2 nodes to perform this action",
                    "Join Failed.",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var n1 = selection[0].PathNode;
            var n2 = selection[1].PathNode;
            if (n1 != null && n2 != null)
            {
                var link = n1.AddLink(n2);
                if (link == null)
                {
                    MessageBox.Show("Failed to join nodes. The nodes are likely too far away!", "Join Failed.", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                link.SetForwardLanesBidirectionally(1);
                link.Shortcut = true;

                if (n2.TryGetLinkForNode(n1, out var backLink))
                {
                    backLink.Shortcut = true;
                }

                UpdatePathYndGraphics(n1.Ynd, false);
                UpdatePathYndGraphics(n2.Ynd, false);
            }
        }




        private void StatsUpdateTimer_Tick(object sender, EventArgs e)
        {

            StatsLabel.Text = Renderer.GetStatusText();

            if (Renderer.timerunning)
            {
                float fv = Renderer.timeofday * 60.0f;
                TimeOfDayTrackBar.Value = (int)fv;
                UpdateTimeOfDayLabel();
            }

            CameraPositionTextBox.Text = FloatUtil.GetVector3StringFormat(camera.Position, "0.##");
        }

        private void WorldForm_Load(object sender, EventArgs e)
        {
            Init();
        }

        private void WorldForm_FormClosing(object sender, FormClosingEventArgs e)
        {
        }

        private void WorldForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            SaveSettings();
        }

        private void WorldForm_MouseDown(object sender, MouseEventArgs e)
        {
            switch (e.Button)
            {
                case MouseButtons.Left: MouseLButtonDown = true; break;
                case MouseButtons.Right: MouseRButtonDown = true; break;
            }

            if (!ToolsPanelShowButton.Focused)
            {
                ToolsPanelShowButton.Focus(); //make sure no textboxes etc are focused!
            }

            MouseDownPoint = e.Location;
            MouseLastPoint = MouseDownPoint;
            Input.CtrlPressed = (ModifierKeys & Keys.Control) > 0;
            Input.ShiftPressed = (ModifierKeys & Keys.Shift) > 0;

            if (ControlMode == WorldControlMode.Free && !ControlBrushEnabled)
            {
                if (MouseLButtonDown)
                {
                    if (MousedMarker != null)
                    {
                        if (MousedMarker.IsMovable)
                        {
                            GrabbedMarker = MousedMarker;
                        }
                        else
                        {
                            SelectedMarker = MousedMarker;
                            ShowMarkerSelectionInfo(SelectedMarker);
                        }
                        if (GrabbedWidget != null)
                        {
                            GrabbedWidget.IsDragging = false;
                            GrabbedWidget = null;
                        }
                    }
                    else
                    {
                        if (ShowWidget && Widget.IsUnderMouse && !Input.kbmoving)
                        {
                            GrabbedWidget = Widget;
                            GrabbedWidget.IsDragging = true;
                            if (Input.ShiftPressed)
                            {
                                var ms = CurrentMapSelection.MultipleSelectionItems;
                                if (ms?.Length > 0 && ms[0].PathNode != null)
                                {
                                    MessageBox.Show("You cannot clone multiple path nodes at once", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    GrabbedWidget.IsDragging = false;
                                    GrabbedWidget = null;
                                } else
                                {
                                    CloneItem();
                                }
                            }
                            MarkUndoStart(GrabbedWidget);
                        }
                        else
                        {
                            if (GrabbedWidget != null)
                            {
                                GrabbedWidget.IsDragging = false;
                                GrabbedWidget = null;
                            }

                            if (Input.CtrlPressed)
                            {
                                SpawnTestEntity();
                            }

                        }
                        GrabbedMarker = null;
                    }
                }

                if (MouseRButtonDown)
                {
                    SelectMousedItem();
                }
            }
            else
            {
                lock (MouseControlSyncRoot)
                {
                    MouseControlButtons |= e.Button;
                }
            }

            MouseX = e.X; //to stop jumps happening on mousedown, sometimes the last MouseMove event was somewhere else... (eg after clicked a menu)
            MouseY = e.Y;
        }

        private void WorldForm_MouseUp(object sender, MouseEventArgs e)
        {
            switch (e.Button)
            {
                case MouseButtons.Left: MouseLButtonDown = false; break;
                case MouseButtons.Right: MouseRButtonDown = false; break;
            }

            Input.CtrlPressed = (ModifierKeys & Keys.Control) > 0;
            Input.ShiftPressed = (ModifierKeys & Keys.Shift) > 0;

            lock (MouseControlSyncRoot)
            {
                MouseControlButtons &= ~e.Button;
            }

            if (e.Button == MouseButtons.Left)
            {
                if (BoxSelectActive)
                {
                    BoxSelectEnd = e.Location;
                    PerformBoxSelect();
                    BoxSelectActive = false;
                    BoxSelectPending = false;
                    ControlBrushTimer = 0;
                    return;
                }
                BoxSelectActive = false;
                BoxSelectPending = false;

                GrabbedMarker = null;
                if (GrabbedWidget != null)
                {
                    MarkUndoEnd(GrabbedWidget);
                    GrabbedWidget.IsDragging = false;
                    GrabbedWidget.Position = SelectedItem.WidgetPosition;//in case of any snapping, make sure widget is in correct position at the end
                    GrabbedWidget = null;
                }
                if ((e.Location == MouseDownPoint) && (MousedMarker == null))
                {
                    //was clicked. but not on a marker... deselect and hide the panel
                    SelectedMarker = null;
                    HideMarkerSelectionInfo();
                }
                ControlBrushTimer = 0;
            }

        }

        private void WorldForm_MouseMove(object sender, MouseEventArgs e)
        {
            int dx = e.X - MouseX;
            int dy = e.Y - MouseY;

            Input.CtrlPressed = (ModifierKeys & Keys.Control) > 0;
            Input.ShiftPressed = (ModifierKeys & Keys.Shift) > 0;

            if (MouseInvert)
            {
                dy = -dy;
            }

            bool altPressed = (ModifierKeys & Keys.Alt) > 0;

            if (ControlMode == WorldControlMode.Free && !ControlBrushEnabled)
            {
                if (MouseLButtonDown && altPressed && GrabbedWidget == null && GrabbedMarker == null)
                {
                    // Alt+Left drag: box selection (suppresses camera rotation)
                    if (!BoxSelectPending && !BoxSelectActive)
                    {
                        // First time detecting Alt during this drag - initialize
                        BoxSelectPending = true;
                        BoxSelectStart = MouseDownPoint;
                        BoxSelectEnd = e.Location;
                    }
                    else
                    {
                        BoxSelectEnd = e.Location;
                    }
                    if (BoxSelectPending)
                    {
                        int adx = Math.Abs(e.Location.X - BoxSelectStart.X);
                        int ady = Math.Abs(e.Location.Y - BoxSelectStart.Y);
                        if (adx > BoxSelectThreshold || ady > BoxSelectThreshold)
                        {
                            BoxSelectActive = true;
                            BoxSelectPending = false;
                        }
                    }
                }
                else if (MouseLButtonDown)
                {
                    if (BoxSelectActive || BoxSelectPending)
                    {
                        // Alt was released during drag - cancel box select
                        BoxSelectActive = false;
                        BoxSelectPending = false;
                    }
                    RotateCam(dx, dy);
                }
                if (MouseRButtonDown)
                {
                    if (Renderer.controllightdir)
                    {
                        Renderer.lightdirx += (dx * camera.Sensitivity);
                        Renderer.lightdiry += (dy * camera.Sensitivity);
                    }
                    else if (Renderer.controltimeofday)
                    {
                        float tod = Renderer.timeofday;
                        tod += (dx - dy) / 30.0f;
                        while (tod >= 24.0f) tod -= 24.0f;
                        while (tod < 0.0f) tod += 24.0f;
                        timecycle.SetTime(tod);
                        Renderer.timeofday = tod;

                        float fv = tod * 60.0f;
                        TimeOfDayTrackBar.Value = (int)fv;
                        UpdateTimeOfDayLabel();
                    }
                }

                UpdateMousePosition(e);

            }
            else if (ControlBrushEnabled)
            {
                if (MouseRButtonDown)
                {
                    RotateCam(dx, dy);
                }

                UpdateMousePosition(e);

                ControlBrushTimer++;
                if (ControlBrushTimer > (Input.ShiftPressed ? 5 : 10))
                {
                    //lock (Renderer.RenderSyncRoot)
                    {
                        if (ProjectForm != null && MouseLButtonDown)
                        {
                            ProjectForm.PaintGrass(MouseRayCollision, Input);
                        }
                        ControlBrushTimer = 0;
                    }
                }
            }
            else
            {
                lock (MouseControlSyncRoot)
                {
                    MouseControlX += dx;
                    MouseControlY += dy;
                    //MouseControlButtons = e.Button;
                }
                var newpos = PointToScreen(MouseLastPoint);
                if (Cursor.Position != newpos)
                {
                    Cursor.Position = newpos;
                    return;
                }
            }



            MousedMarker = FindMousedMarker();

            if (Cursor != Cursors.WaitCursor)
            {
                if (MousedMarker != null)
                {
                    if (MousedMarker.IsMovable)
                    {
                        Cursor = Cursors.SizeAll;
                    }
                    else
                    {
                        Cursor = Cursors.Hand;
                    }
                }
                else
                {
                    Cursor = Cursors.Default;
                }
            }
        }

        private void UpdateMousePosition(MouseEventArgs e)
        {
            MouseX = e.X;
            MouseY = e.Y;
            MouseLastPoint = e.Location;
        }

        private void RotateCam(int dx, int dy)
        {
            if (GrabbedMarker == null)
            {
                if (GrabbedWidget == null)
                {
                    if (MapViewEnabled == false)
                    {
                        camera.MouseRotate(dx, dy);
                    }
                    else
                    {
                        //need to move the camera entity XY with mouse in mapview mode...
                        MapViewDragX += dx;
                        MapViewDragY += dy;
                    }
                }
                else
                {
                    //grabbed widget will move itself in Update() when IsDragging==true
                }
            }
            else
            {
                //move the grabbed marker...
                //float uptx = (CurrentMap != null) ? CurrentMap.UnitsPerTexelX : 1.0f;
                //float upty = (CurrentMap != null) ? CurrentMap.UnitsPerTexelY : 1.0f;
                //Vector3 wpos = GrabbedMarker.WorldPos;
                //wpos.X += dx * uptx;
                //wpos.Y += dy * upty;
                //GrabbedMarker.WorldPos = wpos;
                //UpdateMarkerTexturePos(GrabbedMarker);
                //if (GrabbedMarker == LocatorMarker)
                //{
                //    LocateTextBox.Text = LocatorMarker.ToString();
                //    WorldCoordTextBox.Text = LocatorMarker.Get2DWorldPosString();
                //    TextureCoordTextBox.Text = LocatorMarker.Get2DTexturePosString();
                //}
            }
        }

        private void WorldForm_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (e.Delta != 0)
            {
                if (ControlMode == WorldControlMode.Free || ControlBrushEnabled)
                {
                    camera.MouseZoom(e.Delta);
                }
                else
                {
                    lock (MouseControlSyncRoot)
                    {
                        MouseControlWheel += e.Delta;
                    }
                }
            }

        }

        private void WorldForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (ActiveControl is TextBox)
            {
                var tb = ActiveControl as TextBox;
                if (!tb.ReadOnly) return; //don't move the camera when typing!
            }
            if (ActiveControl is ComboBox)
            {
                var cb = ActiveControl as ComboBox;
                if (cb.DropDownStyle != ComboBoxStyle.DropDownList) return; //nontypable combobox
            }

            bool enablemove = (!iseditmode) || (MouseLButtonDown && (GrabbedMarker == null) && (GrabbedWidget == null));

            Input.KeyDown(e, enablemove);

            var k = e.KeyCode;
            var kb = Input.keyBindings;
            bool ctrl = Input.CtrlPressed;
            bool shift = Input.ShiftPressed;


            if (!ctrl)
            {
                if (k == kb.MoveSlowerZoomIn)
                {
                    camera.MouseZoom(1);
                }
                if (k == kb.MoveFasterZoomOut)
                {
                    camera.MouseZoom(-1);
                }
            }


            if (!Input.kbmoving && !Widget.IsDragging) //don't trigger further actions if camera moving or widget dragging 
            {
                if (!ctrl)
                {
                    //switch widget modes and spaces.
                    if ((k == kb.ExitEditMode))
                    {
                        if (Widget.Mode == WidgetMode.Default) ToggleWidgetSpace();
                        else SetWidgetMode("Default");
                    }
                    if ((k == kb.EditPosition))// && !enablemove)
                    {
                        if (Widget.Mode == WidgetMode.Position) ToggleWidgetSpace();
                        else SetWidgetMode("Position");
                    }
                    if ((k == kb.EditRotation))// && !enablemove)
                    {
                        if (Widget.Mode == WidgetMode.Rotation) ToggleWidgetSpace();
                        else SetWidgetMode("Rotation");
                    }
                    if ((k == kb.EditScale))// && !enablemove)
                    {
                        if (Widget.Mode == WidgetMode.Scale) ToggleWidgetSpace();
                        else SetWidgetMode("Scale");
                    }
                    if (k == kb.ToggleMouseSelect)
                    {
                        SetMouseSelect(!MouseSelectEnabled);
                    }
                    if (k == kb.ToggleToolbar)
                    {
                        ToggleToolbar();
                    }
                    if (k == kb.FirstPerson)
                    {
                        SetControlMode((ControlMode == WorldControlMode.Free) ? WorldControlMode.Ped : WorldControlMode.Free);
                    }
                    if (k == Keys.Delete)
                    {
                        DeleteItem();
                    }
                    if (SelectionMode == MapSelectionMode.Path)
                    {
                        if (k == Keys.J)
                        {
                            TryCreateNodeLink();
                        }
                        if (k == Keys.K)
                        {
                            TryCreateNodeShortcut();
                        }
                    }
                }
                else
                {
                    switch (k)
                    {
                        case Keys.N:
                            New();
                            break;
                        case Keys.O:
                            Open();
                            break;
                        case Keys.S:
                            if (shift) SaveAll();
                            else Save();
                            break;
                        case Keys.Z:
                            Undo();
                            break;
                        case Keys.Y:
                            Redo();
                            break;
                        case Keys.C:
                            CopyItem();
                            break;
                        case Keys.V:
                            PasteItem();
                            break;
                        case Keys.U:
                            ToolsPanelShowButton.Visible = !ToolsPanelShowButton.Visible;
                            break;
                    }
                }
            }

            if (k == Keys.Escape) //temporary? panic get cursor back when in first person mode
            {
                if (ControlMode != WorldControlMode.Free) SetControlMode(WorldControlMode.Free);
            }

            if (ControlMode != WorldControlMode.Free || ControlBrushEnabled)
            {
                e.Handled = true;
            }
        }

        private void WorldForm_KeyUp(object sender, KeyEventArgs e)
        {
            Input.KeyUp(e);

            if (ActiveControl is TextBox)
            {
                var tb = ActiveControl as TextBox;
                if (!tb.ReadOnly) return; //don't move the camera when typing!
            }
            if (ActiveControl is ComboBox)
            {
                var cb = ActiveControl as ComboBox;
                if (cb.DropDownStyle != ComboBoxStyle.DropDownList) return; //non-typable combobox
            }

            if (ControlMode != WorldControlMode.Free)
            {
                e.Handled = true;
            }
        }

        private void WorldForm_Deactivate(object sender, EventArgs e)
        {
            //try not to lock keyboard movement if the form loses focus.
            Input.KeyboardStop();
        }

        private void ViewModeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool prevmodel = !(rendermaps || renderworld);
            string? mode = (string?)ViewModeComboBox.SelectedItem;
            switch (mode)
            {
                case "World view":
                    rendermaps = false;
                    renderworld = true;
                    ViewTabControl.SelectedTab = ViewWorldTabPage;
                    break;
                case "Ymap view":
                    rendermaps = true;
                    renderworld = false;
                    ViewTabControl.SelectedTab = ViewYmapsTabPage;
                    break;
                case "Model view":
                    rendermaps = false;
                    renderworld = false;
                    ViewTabControl.SelectedTab = ViewModelTabPage;
                    if (SelectionNameTextBox.Text != "" && SelectionNameTextBox.Text != "Nothing selected")
                    {
                        modelname = SelectionNameTextBox.Text;
                    }
                    break;
            }

            if ((camera == null) || (camera.FollowEntity == null)) return;
            if (rendermaps || renderworld)
            {
                if (prevmodel) //only change location if the last mode was model mode
                {
                    camera.FollowEntity.Position = prevworldpos;
                }
            }
            else
            {
                prevworldpos = camera.FollowEntity.Position;
                camera.FollowEntity.Position = new Vector3(0.0f, 0.0f, 0.0f);
            }
        }

        private void ModelComboBox_TextUpdate(object sender, EventArgs e)
        {
            modelname = ModelComboBox.Text;
        }

        private void ModelComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            modelname = ModelComboBox.Text;
        }

        private void YmapsTextBox_TextChanged(object sender, EventArgs e)
        {
            ymaplist = YmapsTextBox.Text.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        }

        private void ToolsPanelHideButton_Click(object sender, EventArgs e)
        {
            ToolsPanel.Visible = false;
            ToolsPanelShowButton.Focus();
        }

        private void ToolsPanelShowButton_Click(object sender, EventArgs e)
        {
            ToolsPanel.Visible = true;
            ToolsPanelHideButton.Focus();
        }

        private void WireframeCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Renderer.shaders.wireframe = WireframeCheckBox.Checked;
        }

        private void GrassCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Renderer.rendergrass = GrassCheckBox.Checked;
        }

        private void CarGeneratorsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Renderer.rendercars = CarGeneratorsCheckBox.Checked;
        }

        private void TimedEntitiesCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Renderer.rendertimedents = TimedEntitiesCheckBox.Checked;
        }

        private void TimedEntitiesAlwaysOnCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Renderer.rendertimedentsalways = TimedEntitiesAlwaysOnCheckBox.Checked;
        }

        private void InteriorsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Renderer.renderinteriors = InteriorsCheckBox.Checked;
        }

        private void WaterQuadsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            renderwaterquads = WaterQuadsCheckBox.Checked;
        }

        private void ProxiesCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Renderer.renderproxies = ProxiesCheckBox.Checked;
        }

        private void HDTexturesCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Renderer.renderhdtextures = HDTexturesCheckBox.Checked;
        }

        private void NearClipUpDown_ValueChanged(object sender, EventArgs e)
        {
            camera.ZNear = (float)NearClipUpDown.Value;
            camera.UpdateProj = true;
        }

        private void FarClipUpDown_ValueChanged(object sender, EventArgs e)
        {
            camera.ZFar = (float)FarClipUpDown.Value;
            camera.UpdateProj = true;
        }

        private void PathsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            renderpaths = PathsCheckBox.Checked;
        }

        private void PathBoundsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            renderpathbounds = PathBoundsCheckBox.Checked;
        }

        private void TrainPathsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            rendertraintracks = TrainPathsCheckBox.Checked;
        }

        private void NavMeshesCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            rendernavmeshes = NavMeshesCheckBox.Checked;
        }

        private void PathsDepthClipCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Renderer.shaders.PathsDepthClip = PathsDepthClipCheckBox.Checked;
        }

        private void ErrorConsoleCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            ConsolePanel.Visible = ErrorConsoleCheckBox.Checked;
        }

        private void DynamicLODCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Renderer.usedynamiclod = DynamicLODCheckBox.Checked;
            ShowYmapChildrenCheckBox.Enabled = !Renderer.usedynamiclod;
        }

        private void DetailTrackBar_Scroll(object sender, EventArgs e)
        {
            Renderer.lodthreshold = 50.0f / (0.1f + (float)DetailTrackBar.Value);
        }

        private void WaitForChildrenCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Renderer.waitforchildrentoload = WaitForChildrenCheckBox.Checked;
        }

        private void ReloadShadersButton_Click(object sender, EventArgs e)
        {
            //### NO LONGER USED
            if (Renderer.Device == null) return; //can't do this with no device

            Cursor = Cursors.WaitCursor;
            pauserendering = true;

            lock (Renderer.RenderSyncRoot)
            {
                try
                {
                    Renderer.ReloadShaders();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading shaders!\n" + ex.ToString());
                    return;
                }
            }

            pauserendering = false;
            Cursor = Cursors.Default;
        }

        private void MarkerStyleComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            MapIcon? icon = MarkerStyleComboBox.SelectedItem as MapIcon;
            if (icon != MarkerIcon)
            {
                MarkerIcon = icon;
                foreach (MapMarker m in Markers)
                {
                    m.Icon = icon;
                }
            }
        }

        private void LocatorStyleComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            MapIcon? icon = LocatorStyleComboBox.SelectedItem as MapIcon;
            if (icon != LocatorIcon)
            {
                LocatorIcon = icon;
                LocatorMarker.Icon = icon;
            }
        }

        private void ShowLocatorCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            RenderLocator = ShowLocatorCheckBox.Checked;
        }

        private void LocateTextBox_TextChanged(object sender, EventArgs e)
        {
            if (GrabbedMarker == LocatorMarker) return; //don't try to update the marker if it's being dragged
            if (LocatorMarker == null) return; //this shouldn't happen, but anyway

            LocatorMarker.Parse(LocateTextBox.Text);

            //UpdateMarkerTexturePos(LocatorMarker);
        }

        private void GoToButton_Click(object sender, EventArgs e)
        {
            GoToMarker(LocatorMarker);
        }

        private void AddMarkersButton_Click(object sender, EventArgs e)
        {
            string[] lines = MultiFindTextBox.Text.Split('\n');
            foreach (string line in lines)
            {
                AddMarker(line);
            }
        }

        private void ClearMarkersButton_Click(object sender, EventArgs e)
        {
            MultiFindTextBox.Text = string.Empty;
            Markers.Clear();
        }

        private void ResetMarkersButton_Click(object sender, EventArgs e)
        {
            Markers.Clear();
            AddDefaultMarkers();
        }

        private void AddCurrentPositonMarkerButton_Click(object sender, EventArgs e)
        {
            AddMarker(camera.Position, "Marker", true);
        }

        private void AddSelectionMarkerButton_Click(object sender, EventArgs e)
        {
            if (SelectedItem.EntityDef == null)
            { return; }

            Vector3 pos = SelectedItem.EntityDef.Position;
            string name = SelectedItem.EntityDef.CEntityDef.archetypeName.ToString();
            var marker = AddMarker(pos, name, true);
            SelectedMarker = marker;
            ShowMarkerSelectionInfo(SelectedMarker);
        }

        private void MarkerDepthClipCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Renderer.markerdepthclip = MarkerDepthClipCheckBox.Checked;
        }

        private void ShadowsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            lock (Renderer.RenderSyncRoot)
            {
                Renderer.shaders.shadows = ShadowsCheckBox.Checked;
            }
        }

        private void SkydomeCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Renderer.renderskydome = SkydomeCheckBox.Checked;
        }

        private void BoundsStyleComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            var val = BoundsStyleComboBox.SelectedItem;
            var strval = val as string;
            SetBoundsMode(strval);
        }

        private void BoundsDepthClipCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Renderer.renderboundsclip = BoundsDepthClipCheckBox.Checked;
        }

        private void BoundsRangeTrackBar_Scroll(object sender, EventArgs e)
        {
            float fv = BoundsRangeTrackBar.Value;
            Renderer.renderboundsmaxdist = fv * fv;
        }

        private void MouseSelectCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            SetMouseSelect(MouseSelectCheckBox.Checked);
        }

        private void SelectionBoundsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            ShowSelectionBounds = SelectionBoundsCheckBox.Checked;
        }

        private void PopZonesCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            renderpopzones = PopZonesCheckBox.Checked;
        }

        private void SkeletonsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Renderer.renderskeletons = SkeletonsCheckBox.Checked;
        }

        private void AudioOuterBoundsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            renderaudioouterbounds = AudioOuterBoundsCheckBox.Checked;
        }

        private void ToolsPanelExpandButton_Click(object sender, EventArgs e)
        {
            toolspanelexpanded = !toolspanelexpanded;

            int oldwidth = ToolsPanel.Width;
            if (toolspanelexpanded)
            {
                ToolsPanelExpandButton.Text = ">>";
            }
            else
            {
                ToolsPanelExpandButton.Text = "<<";
            }
            ToolsPanel.Width = toolspanellastwidth; //or extended width
            ToolsPanel.Left -= (toolspanellastwidth - oldwidth);
            toolspanellastwidth = oldwidth;
        }

        private void ToolsDragPanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                toolsPanelResizing = true;
                toolsPanelResizeStartX = e.X + ToolsPanel.Left;
                toolsPanelResizeStartLeft = ToolsPanel.Left;
                toolsPanelResizeStartRight = ToolsPanel.Right;
            }
        }

        private void ToolsDragPanel_MouseUp(object sender, MouseEventArgs e)
        {
            toolsPanelResizing = false;
        }

        private void ToolsDragPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (toolsPanelResizing)
            {
                int rx = e.X + ToolsPanel.Left;
                int dx = rx - toolsPanelResizeStartX;
                ToolsPanel.Left = toolsPanelResizeStartLeft + dx;
                ToolsPanel.Width = toolsPanelResizeStartRight - toolsPanelResizeStartLeft - dx;
            }
        }

        private void FullScreenCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            SetFullscreen(FullScreenCheckBox.Checked);
        }

        private void ControlSettingsButton_Click(object sender, EventArgs e)
        {
            ShowSettingsForm("Controls");
        }

        private void AdvancedSettingsButton_Click(object sender, EventArgs e)
        {
            ShowSettingsForm("Advanced");
        }

        private void ResetSettingsButton_Click(object sender, EventArgs e)
        {
            ResetSettings();
        }

        private void SaveSettingsButton_Click(object sender, EventArgs e)
        {
            SaveSettings();
        }

        private void AboutButton_Click(object sender, EventArgs e)
        {
            AboutForm f = new();
            f.Show(this);
        }

        private void ToolsButton_Click(object sender, EventArgs e)
        {
            ToolsMenu.Show(ToolsButton, 0, ToolsButton.Height);
        }

        private void ToolsMenuConfigureGame_Click(object sender, EventArgs e)
        {
            var result = GTAFolder.UpdateGTAFolder(false, false);
            if (result)
            {
                MessageBox.Show("CodeWalker will now restart.");
                Application.Restart();
                Environment.Exit(0);
            }
        }

        private void ToolsMenuRPFBrowser_Click(object sender, EventArgs e)
        {
            BrowseForm f = new();
            f.Show(this);
        }

        private void ToolsMenuRPFExplorer_Click(object sender, EventArgs e)
        {
            ExploreForm f = new();
            f.Show(this);
        }

        private void ToolsMenuSelectionInfo_Click(object sender, EventArgs e)
        {
            ShowInfoForm();
        }

        private void ToolsMenuProjectWindow_Click(object sender, EventArgs e)
        {
            ShowProjectForm();
        }

        private void ToolsMenuCutsceneViewer_Click(object sender, EventArgs e)
        {
            ShowCutsceneForm();
        }

        private void ToolsMenuAudioExplorer_Click(object sender, EventArgs e)
        {
            AudioExplorerForm f = new(gameFileCache);
            f.Show(this);
        }

        private void ToolsMenuWorldSearch_Click(object sender, EventArgs e)
        {
            ShowSearchForm();
        }

        private void ToolsMenuBinarySearch_Click(object sender, EventArgs e)
        {
            BinarySearchForm f = new(gameFileCache);
            f.Show(this);
        }

        private void ToolsMenuJenkGen_Click(object sender, EventArgs e)
        {
            JenkGenForm f = new();
            f.Show(this);
        }

        private void ToolsMenuJenkInd_Click(object sender, EventArgs e)
        {
            JenkIndForm f = new(gameFileCache);
            f.Show(this);
        }

        private void ToolsMenuExtractScripts_Click(object sender, EventArgs e)
        {
            ExtractScriptsForm f = new();
            f.Show(this);
        }

        private void ToolsMenuExtractTextures_Click(object sender, EventArgs e)
        {
            ExtractTexForm f = new();
            f.Show(this);
        }

        private void ToolsMenuExtractRawFiles_Click(object sender, EventArgs e)
        {
            ExtractRawForm f = new();
            f.Show(this);
        }

        private void ToolsMenuExtractShaders_Click(object sender, EventArgs e)
        {
            ExtractShadersForm f = new();
            f.Show(this);
        }

        private void ToolsMenuOptions_Click(object sender, EventArgs e)
        {
            ShowSettingsForm();
        }

        private void StatusBarCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            StatusStrip.Visible = StatusBarCheckBox.Checked;
        }

        private void RenderModeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            TextureSamplerComboBox.Enabled = false;
            TextureCoordsComboBox.Enabled = false;
            switch (RenderModeComboBox.Text)
            {
                default:
                case "Default":
                    Renderer.shaders.RenderMode = WorldRenderMode.Default;
                    break;
                case "Single texture":
                    Renderer.shaders.RenderMode = WorldRenderMode.SingleTexture;
                    TextureSamplerComboBox.Enabled = true;
                    TextureCoordsComboBox.Enabled = true;
                    break;
                case "Vertex normals":
                    Renderer.shaders.RenderMode = WorldRenderMode.VertexNormals;
                    break;
                case "Vertex tangents":
                    Renderer.shaders.RenderMode = WorldRenderMode.VertexTangents;
                    break;
                case "Vertex colour 1":
                    Renderer.shaders.RenderMode = WorldRenderMode.VertexColour;
                    Renderer.shaders.RenderVertexColourIndex = 1;
                    break;
                case "Vertex colour 2":
                    Renderer.shaders.RenderMode = WorldRenderMode.VertexColour;
                    Renderer.shaders.RenderVertexColourIndex = 2;
                    break;
                case "Vertex colour 3":
                    Renderer.shaders.RenderMode = WorldRenderMode.VertexColour;
                    Renderer.shaders.RenderVertexColourIndex = 3;
                    break;
                case "Texture coord 1":
                    Renderer.shaders.RenderMode = WorldRenderMode.TextureCoord;
                    Renderer.shaders.RenderTextureCoordIndex = 1;
                    break;
                case "Texture coord 2":
                    Renderer.shaders.RenderMode = WorldRenderMode.TextureCoord;
                    Renderer.shaders.RenderTextureCoordIndex = 2;
                    break;
                case "Texture coord 3":
                    Renderer.shaders.RenderMode = WorldRenderMode.TextureCoord;
                    Renderer.shaders.RenderTextureCoordIndex = 3;
                    break;
            }
        }

        private void TextureSamplerComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (TextureSamplerComboBox.SelectedItem is ShaderParamNames)
            {
                Renderer.shaders.RenderTextureSampler = (ShaderParamNames)TextureSamplerComboBox.SelectedItem;
            }
        }

        private void TextureCoordsComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (TextureCoordsComboBox.Text)
            {
                default:
                case "Texture coord 1":
                    Renderer.shaders.RenderTextureSamplerCoord = 1;
                    break;
                case "Texture coord 2":
                    Renderer.shaders.RenderTextureSamplerCoord = 2;
                    break;
                case "Texture coord 3":
                    Renderer.shaders.RenderTextureSamplerCoord = 3;
                    break;
            }

        }

        private void CollisionMeshesCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            rendercollisionmeshes = CollisionMeshesCheckBox.Checked;
            Renderer.rendercollisionmeshes = rendercollisionmeshes;
        }

        private void CollisionMeshRangeTrackBar_Scroll(object sender, EventArgs e)
        {
            collisionmeshrange = CollisionMeshRangeTrackBar.Value;
        }

        private void CollisionMeshLayer0CheckBox_CheckedChanged(object sender, EventArgs e)
        {
            collisionmeshlayers[0] = CollisionMeshLayer0CheckBox.Checked;
        }

        private void CollisionMeshLayer1CheckBox_CheckedChanged(object sender, EventArgs e)
        {
            collisionmeshlayers[1] = CollisionMeshLayer1CheckBox.Checked;
        }

        private void CollisionMeshLayer2CheckBox_CheckedChanged(object sender, EventArgs e)
        {
            collisionmeshlayers[2] = CollisionMeshLayer2CheckBox.Checked;
        }

        private void CollisionMeshLayerDrawableCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Renderer.rendercollisionmeshlayerdrawable = CollisionMeshLayerDrawableCheckBox.Checked;
        }

        private void ControlLightDirectionCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Renderer.controllightdir = ControlLightDirectionCheckBox.Checked;
            if (Renderer.controllightdir)
            {
                ControlTimeOfDayCheckBox.Checked = false;
            }
        }

        private void ControlTimeOfDayCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Renderer.controltimeofday = ControlTimeOfDayCheckBox.Checked;
            if (Renderer.controltimeofday)
            {
                ControlLightDirectionCheckBox.Checked = false;
            }
        }

        private void ShowYmapChildrenCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Renderer.renderchildents = ShowYmapChildrenCheckBox.Checked;
        }

        private void DeferredShadingCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            lock (Renderer.RenderSyncRoot)
            {
                Renderer.shaders.deferred = DeferredShadingCheckBox.Checked;
            }
        }

        private void HDRRenderingCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            lock (Renderer.RenderSyncRoot)
            {
                Renderer.shaders.hdr = HDRRenderingCheckBox.Checked;
            }
        }

        private void AnisotropicFilteringCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Renderer.shaders.AnisotropicFiltering = AnisotropicFilteringCheckBox.Checked;
        }

        private void WorldMaxLodComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (WorldMaxLodComboBox.Text)
            {
                default:
                case "ORPHANHD":
                    Renderer.renderworldMaxLOD = rage__eLodType.LODTYPES_DEPTH_ORPHANHD;
                    break;
                case "HD":
                    Renderer.renderworldMaxLOD = rage__eLodType.LODTYPES_DEPTH_HD;
                    break;
                case "LOD":
                    Renderer.renderworldMaxLOD = rage__eLodType.LODTYPES_DEPTH_LOD;
                    break;
                case "SLOD1":
                    Renderer.renderworldMaxLOD = rage__eLodType.LODTYPES_DEPTH_SLOD1;
                    break;
                case "SLOD2":
                    Renderer.renderworldMaxLOD = rage__eLodType.LODTYPES_DEPTH_SLOD2;
                    break;
                case "SLOD3":
                    Renderer.renderworldMaxLOD = rage__eLodType.LODTYPES_DEPTH_SLOD3;
                    break;
                case "SLOD4":
                    Renderer.renderworldMaxLOD = rage__eLodType.LODTYPES_DEPTH_SLOD4;
                    break;
            }
        }

        private void WorldLodDistTrackBar_Scroll(object sender, EventArgs e)
        {
            float loddist = ((float)WorldLodDistTrackBar.Value) * 0.1f;
            Renderer.renderworldLodDistMult = loddist;
            WorldLodDistLabel.Text = loddist.ToString("0.0");
        }

        private void WorldDetailDistTrackBar_Scroll(object sender, EventArgs e)
        {
            float detdist = ((float)WorldDetailDistTrackBar.Value) * 0.1f;
            Renderer.renderworldDetailDistMult = detdist;
            WorldDetailDistLabel.Text = detdist.ToString("0.0");
        }

        private void WorldScriptedYmapsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Renderer.ShowScriptedYmaps = WorldScriptedYmapsCheckBox.Checked;
        }

        private void WorldYmapTimeFilterCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            worldymaptimefilter = WorldYmapTimeFilterCheckBox.Checked;
        }

        private void WorldYmapWeatherFilterCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            worldymapweatherfilter = WorldYmapWeatherFilterCheckBox.Checked;
        }

        private void EnableModsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (!initialised) return;
            if (ProjectForm != null)
            {
                MessageBox.Show("Please close the Project Window before enabling or disabling mods.");
                return;
            }

            SetModsEnabled(EnableModsCheckBox.Checked);
        }

        private void HideNorthYanktonCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (!initialised) return;
            hidenorthyankton = HideNorthYanktonCheckBox.Checked;
        }

        private void HideCayoPericoCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (!initialised) return;
            hidecayoperico = HideCayoPericoCheckBox.Checked;
        }

        private void EnableDlcCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (!initialised) return;
            if (ProjectForm != null)
            {
                MessageBox.Show("Please close the Project Window before enabling or disabling DLC.");
                return;
            }

            SetDlcLevel(DlcLevelComboBox.Text, EnableDlcCheckBox.Checked);
        }

        private void DlcLevelComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!initialised) return;
            if (ProjectForm != null)
            {
                MessageBox.Show("Please close the Project Window before changing the DLC level.");
                return;
            }

            SetDlcLevel(DlcLevelComboBox.Text, EnableDlcCheckBox.Checked);
        }

        private void TimeOfDayTrackBar_Scroll(object sender, EventArgs e)
        {
            SetTimeOfDay(TimeOfDayTrackBar.Value);
        }

        private void WeatherComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            Renderer.SetWeatherType(WeatherComboBox.Text);
        }

        private void WeatherRegionComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            weather.Region = WeatherRegionComboBox.Text;
        }

        private void CloudsComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            //if (!rendersyncroot.TryEnter(50))
            //{ return; } //couldn't get a lock...
            Renderer.individualcloudfrag = CloudsComboBox.Text;
            //rendersyncroot.Exit();
        }

        private void HDLightsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Renderer.renderlights = HDLightsCheckBox.Checked;
        }

        private void LODLightsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Renderer.renderdistlodlights = LODLightsCheckBox.Checked;
            Renderer.renderlodlights = LODLightsCheckBox.Checked;
        }

        private void NaturalAmbientLightCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Renderer.rendernaturalambientlight = NaturalAmbientLightCheckBox.Checked;
        }

        private void ArtificialAmbientLightCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Renderer.renderartificialambientlight = ArtificialAmbientLightCheckBox.Checked;
        }

        private void TimeStartStopButton_Click(object sender, EventArgs e)
        {
            Renderer.timerunning = !Renderer.timerunning;
            TimeStartStopButton.Text = Renderer.timerunning ? "Stop" : "Start";
        }

        private void TimeSpeedTrackBar_Scroll(object sender, EventArgs e)
        {
            float tv = TimeSpeedTrackBar.Value * 0.01f;
            //when tv=0,   speed=0 min/sec
            //when tv=0.5, speed=0.5 min/sec
            //when tv=1,  speed=128 min/sec

            Renderer.timespeed = 128.0f * tv * tv * tv * tv * tv * tv * tv * tv;

            TimeSpeedLabel.Text = Renderer.timespeed.ToString("0.###") + " min/sec";
        }

        private void CameraModeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetCameraMode(CameraModeComboBox.Text);
        }

        private void MapViewDetailTrackBar_Scroll(object sender, EventArgs e)
        {
            float det = ((float)MapViewDetailTrackBar.Value) * 0.1f;
            MapViewDetailLabel.Text = det.ToString("0.0#");
            lock (Renderer.RenderSyncRoot)
            {
                Renderer.MapViewDetail = det;
            }
        }

        private void FieldOfViewTrackBar_Scroll(object sender, EventArgs e)
        {
            float fov = FieldOfViewTrackBar.Value * 0.01f;
            FieldOfViewLabel.Text = fov.ToString("0.0#");
            lock (Renderer.RenderSyncRoot)
            {
                camera.FieldOfView = fov;
                camera.UpdateProj = true;
            }
        }

        private void CloudParamComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            CloudAnimSetting? setting = CloudParamComboBox.SelectedItem as CloudAnimSetting;
            if (setting != null)
            {
                float rng = setting.MaxValue - setting.MinValue;
                float cval = (setting.CurrentValue - setting.MinValue) / rng;
                int ival = (int)(cval * 200.0f);
                ival = Math.Min(Math.Max(ival, 0), 200);
                CloudParamTrackBar.Value = ival;
            }
        }

        private void CloudParamTrackBar_Scroll(object sender, EventArgs e)
        {
            CloudAnimSetting? setting = CloudParamComboBox.SelectedItem as CloudAnimSetting;
            if (setting != null)
            {
                float rng = setting.MaxValue - setting.MinValue;
                float fval = CloudParamTrackBar.Value / 200.0f;
                float cval = (fval * rng) + setting.MinValue;
                setting.CurrentValue = cval;
            }
        }

        private void SelDrawableModelsTreeView_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (e.Node != null)
            {
                UpdateSelectionDrawFlags(e.Node);

                if (InfoForm != null)
                {
                    InfoForm.SyncSelDrawableModelsTreeNode(e.Node);
                }
            }
        }

        private void SelDrawableModelsTreeView_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node != null)
            {
                e.Node.Checked = !e.Node.Checked;
                //UpdateSelectionDrawFlags(e.Node);
            }
        }

        private void SelDrawableModelsTreeView_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = true; //stops annoying ding sound...
        }

        private void SelectionWidgetCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            ShowWidget = SelectionWidgetCheckBox.Checked;
        }

        private void ShowToolbarCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            ToolbarPanel.Visible = ShowToolbarCheckBox.Checked;
        }

        private void ToolbarNewButton_ButtonClick(object sender, EventArgs e)
        {
            New();
        }

        private void ToolbarNewProjectButton_Click(object sender, EventArgs e)
        {
            NewProject();
        }

        private void ToolbarNewYmapButton_Click(object sender, EventArgs e)
        {
            NewYmap();
        }

        private void ToolbarNewYtypButton_Click(object sender, EventArgs e)
        {
            NewYtyp();
        }

        private void ToolbarNewYbnButton_Click(object sender, EventArgs e)
        {
            NewYbn();
        }

        private void ToolbarNewYndButton_Click(object sender, EventArgs e)
        {
            NewYnd();
        }

        private void ToolbarNewTrainsButton_Click(object sender, EventArgs e)
        {
            NewTrainTrack();
        }

        private void ToolbarNewScenarioButton_Click(object sender, EventArgs e)
        {
            NewScenario();
        }

        private void ToolbarOpenButton_ButtonClick(object sender, EventArgs e)
        {
            Open();
        }

        private void ToolbarOpenProjectButton_Click(object sender, EventArgs e)
        {
            OpenProject();
        }

        private void ToolbarOpenFilesButton_Click(object sender, EventArgs e)
        {
            OpenFiles();
        }

        private void ToolbarOpenFolderButton_Click(object sender, EventArgs e)
        {
            OpenFolder();
        }

        private void ToolbarSaveButton_Click(object sender, EventArgs e)
        {
            Save();
        }

        private void ToolbarSaveAllButton_Click(object sender, EventArgs e)
        {
            SaveAll();
        }

        private void ToolbarSelectButton_ButtonClick(object sender, EventArgs e)
        {
            SetMouseSelect(!ToolbarSelectButton.Checked);
            SetWidgetMode("Default");
        }

        private void ToolbarSelectEntityButton_Click(object sender, EventArgs e)
        {
            SetSelectionMode("Entity");
            SetMouseSelect(true);
        }

        private void ToolbarSelectEntityExtensionButton_Click(object sender, EventArgs e)
        {
            SetSelectionMode("Entity Extension");
            SetMouseSelect(true);
        }

        private void ToolbarSelectArchetypeExtensionButton_Click(object sender, EventArgs e)
        {
            SetSelectionMode("Archetype Extension");
            SetMouseSelect(true);
        }

        private void ToolbarSelectTimeCycleModifierButton_Click(object sender, EventArgs e)
        {
            SetSelectionMode("Time Cycle Modifier");
            SetMouseSelect(true);
        }

        private void ToolbarSelectCarGeneratorButton_Click(object sender, EventArgs e)
        {
            SetSelectionMode("Car Generator");
            SetMouseSelect(true);
        }

        private void ToolbarSelectGrassButton_Click(object sender, EventArgs e)
        {
            SetSelectionMode("Grass");
            SetMouseSelect(true);
        }

        private void ToolbarSelectWaterQuadButton_Click(object sender, EventArgs e)
        {
            SetSelectionMode("Water Quad");
            SetMouseSelect(true);
        }

        private void ToolbarSelectCalmingQuadButton_Click(object sender, EventArgs e)
        {
            SetSelectionMode("Water Calming Quad");
            SetMouseSelect(true);
        }

        private void ToolbarSelectWaveQuadButton_Click(object sender, EventArgs e)
        {
            SetSelectionMode("Water Wave Quad");
            SetMouseSelect(true);
        }

        private void ToolbarSelectCollisionButton_Click(object sender, EventArgs e)
        {
            SetSelectionMode("Collision");
            SetMouseSelect(true);
        }

        private void ToolbarSelectNavMeshButton_Click(object sender, EventArgs e)
        {
            SetSelectionMode("Nav Mesh");
            SetMouseSelect(true);
        }

        private void ToolbarSelectPathButton_Click(object sender, EventArgs e)
        {
            SetSelectionMode("Path");
            SetMouseSelect(true);
        }

        private void ToolbarSelectTrainTrackButton_Click(object sender, EventArgs e)
        {
            SetSelectionMode("Train Track");
            SetMouseSelect(true);
        }

        private void ToolbarSelectLodLightsButton_Click(object sender, EventArgs e)
        {
            SetSelectionMode("Lod Lights");
            SetMouseSelect(true);
        }

        private void ToolbarSelectMloInstanceButton_Click(object sender, EventArgs e)
        {
            SetSelectionMode("Mlo Instance");
            SetMouseSelect(true);
        }

        private void ToolbarSelectScenarioButton_Click(object sender, EventArgs e)
        {
            SetSelectionMode("Scenario");
            SetMouseSelect(true);
        }

        private void ToolbarSelectAudioButton_Click(object sender, EventArgs e)
        {
            SetSelectionMode("Audio");
            SetMouseSelect(true);
        }

        private void ToolbarSelectOcclusionButton_Click(object sender, EventArgs e)
        {
            SetSelectionMode("Occlusion");
            SetMouseSelect(true);
        }

        private void ToolbarMoveButton_Click(object sender, EventArgs e)
        {
            SetWidgetMode(ToolbarMoveButton.Checked ? "Default" : "Position");
        }

        private void ToolbarRotateButton_Click(object sender, EventArgs e)
        {
            SetWidgetMode(ToolbarRotateButton.Checked ? "Default" : "Rotation");
        }

        private void ToolbarScaleButton_Click(object sender, EventArgs e)
        {
            SetWidgetMode(ToolbarScaleButton.Checked ? "Default" : "Scale");
        }

        private void ToolbarTransformSpaceButton_ButtonClick(object sender, EventArgs e)
        {
            SetWidgetSpace(Widget.ObjectSpace ? "World space" : "Object space");
        }

        private void ToolbarObjectSpaceButton_Click(object sender, EventArgs e)
        {
            SetWidgetSpace("Object space");
        }

        private void ToolbarWorldSpaceButton_Click(object sender, EventArgs e)
        {
            SetWidgetSpace("World space");
        }

        private void ToolbarSnapButton_ButtonClick(object sender, EventArgs e)
        {
            if (SnapMode == WorldSnapMode.None)
            {
                SetSnapMode(SnapModePrev);
            }
            else
            {
                SetSnapMode(WorldSnapMode.None);
            }
        }

        private void ToolbarSnapToGroundButton_Click(object sender, EventArgs e)
        {
            SetSnapMode(WorldSnapMode.Ground);
        }

        private void ToolbarSnapToGridButton_Click(object sender, EventArgs e)
        {
            SetSnapMode(WorldSnapMode.Grid);
        }

        private void ToolbarSnapToGroundGridButton_Click(object sender, EventArgs e)
        {
            SetSnapMode(WorldSnapMode.Hybrid);
        }

        private void ToolbarRotationSnappingOffButton_Click(object sender, EventArgs e)
        {
            SetRotationSnapping(0);
        }

        private void ToolbarRotationSnapping1Button_Click(object sender, EventArgs e)
        {
            SetRotationSnapping(1);
        }

        private void ToolbarRotationSnapping2Button_Click(object sender, EventArgs e)
        {
            SetRotationSnapping(2);
        }

        private void ToolbarRotationSnapping5Button_Click(object sender, EventArgs e)
        {
            SetRotationSnapping(5);
        }

        private void ToolbarRotationSnapping10Button_Click(object sender, EventArgs e)
        {
            SetRotationSnapping(10);
        }

        private void ToolbarRotationSnapping45Button_Click(object sender, EventArgs e)
        {
            SetRotationSnapping(45);
        }

        private void ToolbarRotationSnapping90Button_Click(object sender, EventArgs e)
        {
            SetRotationSnapping(90);
        }

        private void ToolbarRotationSnappingCustomButton_Click(object sender, EventArgs e)
        {
            ToolsPanel.Visible = true;
            ToolsTabControl.SelectedTab = OptionsTabPage;
            OptionsTabControl.SelectedTab = OptionsHelpersTabPage;
            SnapAngleUpDown.Focus();
        }

        private void ToolbarSnapGridSizeButton_Click(object sender, EventArgs e)
        {
            ToolsPanel.Visible = true;
            ToolsTabControl.SelectedTab = OptionsTabPage;
            OptionsTabControl.SelectedTab = OptionsHelpersTabPage;
            SnapGridSizeUpDown.Focus();
        }

        private void ToolbarUndoButton_ButtonClick(object sender, EventArgs e)
        {
            Undo();
        }

        private void ToolbarUndoListButton_Click(object? sender, EventArgs e)
        {
            var tsi = sender as ToolStripItem;
            if (tsi == null) return;
            var step = tsi.Tag as UndoStep;
            if (step == null) return;
            if (UndoSteps.Count == 0) return;
            var cstep = UndoSteps.Peek();
            while (cstep != null)
            {
                Undo();
                if (cstep == step) break;
                if (UndoSteps.Count == 0) break;
                cstep = UndoSteps.Peek();
            }
        }

        private void ToolbarRedoButton_ButtonClick(object sender, EventArgs e)
        {
            Redo();
        }

        private void ToolbarRedoListButton_Click(object? sender, EventArgs e)
        {
            var tsi = sender as ToolStripItem;
            if (tsi == null) return;
            var step = tsi.Tag as UndoStep;
            if (step == null) return;
            if (RedoSteps.Count == 0) return;
            var cstep = RedoSteps.Peek();
            while (cstep != null)
            {
                Redo();
                if (cstep == step) break;
                if (RedoSteps.Count == 0) break;
                cstep = RedoSteps.Peek();
            }
        }

        private void ToolbarInfoWindowButton_Click(object sender, EventArgs e)
        {
            ShowInfoForm();
        }

        private void ToolbarProjectWindowButton_Click(object sender, EventArgs e)
        {
            ShowProjectForm();
        }

        private void ToolbarAddItemButton_Click(object sender, EventArgs e)
        {
            AddItem();
        }

        private void ToolbarDeleteItemButton_Click(object sender, EventArgs e)
        {
            DeleteItem();
        }

        private void ToolbarCopyButton_Click(object sender, EventArgs e)
        {
            CopyItem();
        }

        private void ToolbarPasteButton_Click(object sender, EventArgs e)
        {
            PasteItem();
        }

        private void ToolbarCameraModeButton_ButtonClick(object sender, EventArgs e)
        {
            ToggleCameraMode();
        }

        private void ToolbarCameraPerspectiveButton_Click(object sender, EventArgs e)
        {
            SetCameraMode("Perspective");
        }

        private void ToolbarCameraMapViewButton_Click(object sender, EventArgs e)
        {
            SetCameraMode("2D Map");
        }

        private void ToolbarCameraOrthographicButton_Click(object sender, EventArgs e)
        {
            SetCameraMode("Orthographic");
        }

        private void SelectionModeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetSelectionMode(SelectionModeComboBox.Text);
        }

        private void SelectionModeComboBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = true;
        }

        private void ViewModeComboBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = true;
        }

        private void WorldMaxLodComboBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = true;
        }

        private void DlcLevelComboBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = true;
        }

        private void CameraModeComboBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = true;
        }

        private void RenderModeComboBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = true;
        }

        private void TextureSamplerComboBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = true;
        }

        private void TextureCoordsComboBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = true;
        }

        private void MarkerStyleComboBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = true;
        }

        private void LocatorStyleComboBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = true;
        }

        private void BoundsStyleComboBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = true;
        }

        private void WeatherComboBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = true;
        }

        private void WeatherRegionComboBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = true;
        }

        private void CloudsComboBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = true;
        }

        private void CloudParamComboBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = true;
        }

        private void SnapGridSizeUpDown_ValueChanged(object sender, EventArgs e)
        {
            SnapGridSize = (float)SnapGridSizeUpDown.Value;
        }

        private void SnapAngleUpDown_ValueChanged(object sender, EventArgs e)
        {
            if (Widget != null)
            {
                SetRotationSnapping((float)SnapAngleUpDown.Value);
            }
        }

        private void RenderEntitiesCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Renderer.renderentities = RenderEntitiesCheckBox.Checked;
        }

        private void StatsLabel_DoubleClick(object sender, EventArgs e)
        {
            var statsForm = new StatisticsForm(this);
            statsForm.Show(this);
        }

        private void SubtitleLabel_SizeChanged(object sender, EventArgs e)
        {
            SubtitleLabel.Left = (ClientSize.Width - SubtitleLabel.Size.Width) / 2; //keep subtitle label centered
        }

        private void SubtitleTimer_Tick(object sender, EventArgs e)
        {
            SubtitleTimer.Enabled = false;
            SubtitleLabel.Visible = false;
        }
    }

    public enum WorldControlMode
    {
        Free = 0,
        Ped = 1,
        Car = 2,
        Heli = 3,
        Plane = 4,
        Jetpack = 10,
    }

    public enum WorldSnapMode
    {
        None = 0,
        Grid = 1,
        Ground = 2,
        Hybrid = 3,
    }

}
