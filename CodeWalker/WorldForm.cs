using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CodeWalker.GameFiles;
using CodeWalker.Project;
using CodeWalker.Properties;
using CodeWalker.Rendering;
using CodeWalker.Tools;
using CodeWalker.World;
using SharpDX;
using SharpDX.XInput;
using Device = SharpDX.Direct3D11.Device;
using DeviceContext = SharpDX.Direct3D11.DeviceContext;
using Point = System.Drawing.Point;

namespace CodeWalker;

public partial class WorldForm : Form, DXForm
{
    private readonly AudioZones audiozones = new();
    //float ControlBrushRadius;

    private readonly Entity camEntity = new();
    private readonly Camera camera;
    private readonly Clouds clouds;
    private readonly Dictionary<YmapEntityDef, YbnFile> collisioninteriors = new();
    private readonly List<BoundsStoreItem> collisionitems = [];
    private readonly bool[] collisionmeshlayers = [true, true, true];
    private readonly List<YbnFile> collisionybns = [];

    private readonly Stopwatch frametimer = new();
    private readonly Heightmaps heightmaps = new();

    private readonly bool initedOk;

    private readonly InputManager Input = new();
    private readonly List<MapMarker> MarkerBatch = [];
    private readonly List<MapMarker> Markers = [];
    private readonly object markersortedsyncroot = new();
    private readonly object markersyncroot = new();

    private readonly object MouseControlSyncRoot = new();

    private readonly bool MouseRayCollisionEnabled = true;
    private readonly PedEntity pedEntity = new();
    private readonly PopZones popzones = new();
    private readonly Stack<UndoStep> RedoSteps = new();
    private readonly List<RelFile> renderaudfilelist = [];
    private readonly bool renderaudiozones = false;
    private readonly List<AudioPlacement> renderaudplacementslist = [];
    private readonly bool renderheightmaps = false;
    private readonly List<YnvFile> rendernavmeshynvs = [];
    private readonly List<YndFile> renderpathynds = [];

    private readonly bool renderscenariobounds = false;
    private readonly List<YmtFile> renderscenariolist = [];
    private readonly bool renderscenarios = false;
    private readonly List<TrainTrack> rendertraintracklist = [];
    private readonly bool renderwatermaps = false;

    private readonly Dictionary<MetaHash, YmapFile> renderworldVisibleYmapDict = new();
    private readonly Scenarios scenarios = new();
    private readonly bool SelectByGeometry = false; //select by geometry needs more work 
    private readonly List<MapMarker> SortedMarkers = [];
    private readonly int startupviewmode = 0; //0=world, 1=ymap, 2=model
    private readonly Timecycle timecycle;
    private readonly Trains trains = new();

    private readonly Stack<UndoStep> UndoSteps = new();
    private readonly Water water = new();
    private readonly Watermaps watermaps = new();
    private readonly Weather weather;


    private readonly TransformWidget Widget = new();
    private int collisionmeshrange = Settings.Default.CollisionMeshRange;
    private bool ControlBrushEnabled;


    private int ControlBrushTimer;

    private bool ControlFireToggle;


    private WorldControlMode ControlMode = WorldControlMode.Free;
    private MapSelection CopiedItem;
    private MapSelection CurMouseHit;

    private CutsceneForm CutsceneForm;

    private volatile bool formopen;
    private MapMarker GrabbedMarker;
    private TransformWidget GrabbedWidget;


    private List<MapIcon> Icons;
    private WorldInfoForm InfoForm;
    private volatile bool initialised;


    private bool iseditmode;
    private MapSelection LastMouseHit;
    private MapIcon LocatorIcon;
    private MapMarker LocatorMarker;
    private int MapViewDragX;
    private int MapViewDragY;

    private bool MapViewEnabled;
    private MapIcon MarkerIcon;
    private string modelname = "dt1_tc_dufo_core"; //"dt1_11_fount_decal";//"v_22_overlays";//
    private MouseButtons MouseControlButtons = MouseButtons.None;
    private MouseButtons MouseControlButtonsPrev = MouseButtons.None;
    private int MouseControlX;
    private int MouseControlY;
    private MapMarker MousedMarker;
    private Point MouseDownPoint;
    private bool MouseInvert = Settings.Default.MouseInvert;
    private Point MouseLastPoint;

    private bool MouseLButtonDown;
    private SpaceRayIntersectResult MouseRayCollision;
    private bool MouseRayCollisionVisible;
    private bool MouseRButtonDown;


    private bool MouseSelectEnabled;
    private int MouseX;
    private int MouseY;
    private volatile bool pauserendering;
    private MapSelection PrevMouseHit;

    private Vector3 prevworldpos = FloatUtil.ParseVector3String(Settings.Default.StartPosition);


    private ProjectForm ProjectForm;
    private bool renderaudioouterbounds = true;


    private bool rendercollisionmeshes = Settings.Default.ShowCollisionMeshes;

    public Renderer Renderer;
    private bool RenderLocator;

    private bool rendermaps;

    private bool rendernavmeshes;

    private bool renderpathbounds = true;
    private bool renderpaths;

    private bool renderpopzones;

    private bool rendertraintracks;

    private bool renderwaterquads = true;
    private bool renderworld;
    private volatile bool running;

    private WorldSearchForm SearchForm;
    private MapSelection SelectedItem;
    private MapMarker SelectedMarker;
    private MapSelectionMode SelectionMode = MapSelectionMode.Entity;

    private string SelectionModeStr = "Entity";

    private SettingsForm SettingsForm;
    private bool ShowSelectionBounds = true;
    private bool ShowWidget = true;
    private float SnapGridSize = 1.0f;

    private WorldSnapMode SnapMode = WorldSnapMode.None;
    private WorldSnapMode SnapModePrev = WorldSnapMode.Ground; //also the default snap mode


    private bool toolspanelexpanded;
    private int toolspanellastwidth;
    private int toolsPanelResizeStartLeft;
    private int toolsPanelResizeStartRight;
    private int toolsPanelResizeStartX;
    private bool toolsPanelResizing;
    private Vector3 UndoStartPosition;
    private Quaternion UndoStartRotation;
    private Vector3 UndoStartScale;

    private bool worldymaptimefilter = true;
    private bool worldymapweatherfilter = true;
    private string[] ymaplist;


    public WorldForm()
    {
        InitializeComponent();

        Renderer = new Renderer(this, GameFileCache);
        camera = Renderer.camera;
        timecycle = Renderer.timecycle;
        weather = Renderer.weather;
        clouds = Renderer.clouds;

        CurMouseHit.WorldForm = this;
        LastMouseHit.WorldForm = this;
        PrevMouseHit.WorldForm = this;

        initedOk = Renderer.Init();
    }

    public object RenderSyncRoot => Renderer.RenderSyncRoot;

    public Space Space { get; } = new();


    public GameFileCache GameFileCache { get; } = GameFileCacheFactory.Create();
    public MapSelection CurrentMapSelection => SelectedItem;


    public bool EditEntityPivot { get; set; } = false;
    public Form Form => this; //for DXForm/DXManager use


    public void InitScene(Device device)
    {
        var width = ClientSize.Width;
        var height = ClientSize.Height;

        try
        {
            Renderer.DeviceCreated(device, width, height);
        }
        catch (Exception ex)
        {
            MessageBox.Show("""
                            Error loading shaders!

                            """ + ex);
            return;
        }

        if (Icons != null)
            foreach (var icon in Icons)
                icon.LoadTexture(device, LogError);

        camera.FollowEntity = camEntity;
        camEntity.Position = startupviewmode != 2 ? prevworldpos : Vector3.Zero;
        camEntity.Orientation = Quaternion.LookAtLH(Vector3.Zero, Vector3.Up, Vector3.ForwardLH);

        Space.AddPersistentEntity(pedEntity);


        LoadSettings();


        formopen = true;
        new Thread(ContentThread).Start();

        frametimer.Start();
    }

    public void CleanupScene()
    {
        formopen = false;

        Renderer.DeviceDestroyed();

        if (Icons != null)
            foreach (var icon in Icons)
                icon.UnloadTexture();

        var count = 0;
        while (running && count < 5000) //wait for the content thread to exit gracefully
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
        var elapsed = (float)frametimer.Elapsed.TotalSeconds;
        frametimer.Restart();

        if (pauserendering) return;

        GameFileCache.BeginFrame();

        if (!Monitor.TryEnter(Renderer.RenderSyncRoot, 50)) return; //couldn't get a lock, try again next time

        UpdateControlInputs(elapsed);

        Space.Update(elapsed);

        CutsceneForm?.UpdateAnimation(elapsed);

        Renderer.Update(elapsed, MouseLastPoint.X, MouseLastPoint.Y);


        UpdateWidgets();

        BeginMouseHitTest();


        Renderer.BeginRender(context);

        Renderer.RenderSkyAndClouds();

        Renderer.SelectedDrawable = SelectedItem.Drawable;

        if (renderworld)
            RenderWorld();
        else if (rendermaps)
            RenderYmaps();
        else
            RenderSingleItem();

        UpdateMouseHits();

        RenderSelection();

        RenderMoused();

        Renderer.RenderQueued();

        Renderer.RenderBounds(SelectionMode);

        Renderer.RenderSelectionGeometry(SelectionMode);

        Renderer.RenderFinalPass();

        RenderMarkers();

        RenderWidgets();

        Renderer.EndRender();

        Monitor.Exit(Renderer.RenderSyncRoot);

        UpdateMarkerSelectionPanelInvoke();
    }

    public bool ConfirmQuit()
    {
        if (ProjectForm is not { CurrentProjectFile: not null }) return true;
        return MessageBox.Show(@"Are you sure you want to quit CodeWalker?", @"Confirm quit",
                   MessageBoxButtons.YesNo) ==
               DialogResult.Yes;
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
        foreach (var icon in Icons)
        {
            MarkerStyleComboBox.Items.Add(icon);
            LocatorStyleComboBox.Items.Add(icon);
        }

        MarkerStyleComboBox.SelectedItem = MarkerIcon; //LoadSettings will handle this
        LocatorStyleComboBox.SelectedItem = LocatorIcon;
        LocatorMarker = new MapMarker
        {
            Icon = LocatorIcon,
            IsMovable = true
        };
        //AddDefaultMarkers(); //some POI to start with

        var texsamplers = RenderableGeometry.GetTextureSamplerList();
        foreach (var texsampler in texsamplers) TextureSamplerComboBox.Items.Add(texsampler);

        WorldMaxLodComboBox.SelectedIndex = 0; //should this be a setting?

        WeatherComboBox.SelectedIndex = 0; //show "<Loading...>" until weather types are loaded

        CameraModeComboBox.SelectedIndex = 0; //"Perspective"

        DlcLevelComboBox.SelectedIndex = 0; //show "<Loading...>" until DLC list is loaded

        UpdateToolbarShortcutsText();


        Input.Init();


        Renderer.Start();
    }


    private MapIcon AddIcon(string name, string filename, int texw, int texh, float centerx, float centery, float scale)
    {
        var filepath = PathUtil.GetFilePath("icons\\" + filename);
        try
        {
            var mi = new MapIcon(name, filepath, texw, texh, centerx, centery, scale);
            Icons.Add(mi);
            return mi;
        }
        catch (Exception ex)
        {
            MessageBox.Show(@"Could not load map icon " + filepath + @" for " + name + """
                !


                """ + ex);
        }

        return null;
    }


    private void UpdateTimeOfDayLabel()
    {
        var v = TimeOfDayTrackBar.Value;
        var fh = v / 60.0f;
        var ih = (int)fh;
        var im = v - ih * 60;
        if (ih == 24) ih = 0;
        TimeOfDayLabel.Text = $@"{ih:00}:{im:00}";
    }

    private void UpdateControlInputs(float elapsed)
    {
        if (elapsed > 0.1f) elapsed = 0.1f;

        var s = Settings.Default;

        var moveSpeed = 50.0f;


        Input.Update();

        if (Input.xbenable)
            if (Input.ControllerButtonJustPressed(GamepadButtonFlags.Start))
                SetControlMode(ControlMode == WorldControlMode.Free ? WorldControlMode.Ped : WorldControlMode.Free);


        if (ControlMode == WorldControlMode.Free || ControlBrushEnabled)
        {
            if (Input.ShiftPressed) moveSpeed *= 5.0f;
            if (Input.CtrlPressed) moveSpeed *= 0.2f;

            var movevec = Input.KeyboardMoveVec(MapViewEnabled);

            if (Input.xbenable)
            {
                movevec.X += Input.xblx;
                if (MapViewEnabled) movevec.Y += Input.xbly;
                else movevec.Z -= Input.xbly;
                moveSpeed *= 1.0f + Math.Min(Math.Max(Input.xblt, 0.0f), 1.0f) * 15.0f; //boost with left trigger
                if (Input.ControllerButtonPressed(GamepadButtonFlags.A | GamepadButtonFlags.RightShoulder |
                                                  GamepadButtonFlags.LeftShoulder)) moveSpeed *= 5.0f;
            }


            if (MapViewEnabled)
            {
                movevec *= elapsed * moveSpeed * Math.Min(camera.OrthographicTargetSize * 0.01f, 50.0f);

                var mapviewscale = 1.0f / camera.Height;
                var fdx = MapViewDragX * mapviewscale;
                var fdy = MapViewDragY * mapviewscale;
                movevec.X -= fdx * camera.OrthographicSize;
                movevec.Y += fdy * camera.OrthographicSize;
            }
            else
            {
                //normal movement
                movevec *= elapsed * moveSpeed * Math.Min(camera.TargetDistance, 20.0f);
            }


            var movewvec = camera.ViewInvQuaternion.Multiply(movevec);
            camEntity.Position += movewvec;

            MapViewDragX = 0;
            MapViewDragY = 0;


            if (!Input.xbenable) return;
            camera.ControllerRotate(Input.xbrx, Input.xbry, elapsed);

            var zoom = 0.0f;
            var zoomspd = s.XInputZoomSpeed;
            var zoomamt = zoomspd * elapsed;
            if (Input.ControllerButtonPressed(GamepadButtonFlags.DPadUp)) zoom += zoomamt;
            if (Input.ControllerButtonPressed(GamepadButtonFlags.DPadDown)) zoom -= zoomamt;
            if (MapViewEnabled) zoom -= zoomamt * Input.xbry;

            camera.ControllerZoom(zoom);

            var fire = Input.xbtrigs.Y > 0;
            if (fire && !ControlFireToggle) SpawnTestEntity(true);
            ControlFireToggle = fire;
        }
        else
        {
            //"play" mode

            int mcx, mcy;
            bool mlb;
            lock (MouseControlSyncRoot)
            {
                mcx = MouseControlX;
                mcy = MouseControlY;
                var mcb = MouseControlButtons;
                var mcbp = MouseControlButtonsPrev;
                mlb = (mcb & MouseButtons.Left) > 0;
                MouseControlX = 0;
                MouseControlY = 0;
                MouseControlButtonsPrev = MouseControlButtons;
                //MouseControlButtons = MouseButtons.None;
            }


            camera.MouseRotate(mcx, mcy);

            if (Input.xbenable) camera.ControllerRotate(Input.xbrx, Input.xbry, elapsed);


            var movecontrol = new Vector2(Input.xbmainaxes.X, Input.xbmainaxes.Y); //(L stick)
            if (Input.kbmovelft) movecontrol.X -= 1.0f;
            if (Input.kbmovergt) movecontrol.X += 1.0f;
            if (Input.kbmovefwd) movecontrol.Y += 1.0f;
            if (Input.kbmovebck) movecontrol.Y -= 1.0f;
            movecontrol.X = Math.Min(movecontrol.X, 1.0f);
            movecontrol.X = Math.Max(movecontrol.X, -1.0f);
            movecontrol.Y = Math.Min(movecontrol.Y, 1.0f);
            movecontrol.Y = Math.Max(movecontrol.Y, -1.0f);

            var fwd = camera.ViewDirection;
            var fwdxy = Vector3.Normalize(new Vector3(fwd.X, fwd.Y, 0));
            var lftxy = Vector3.Normalize(Vector3.Cross(fwd, Vector3.UnitZ));
            var move = lftxy * movecontrol.X + fwdxy * movecontrol.Y;
            var movexy = new Vector2(move.X, move.Y);

            movexy *= 1.0f + Math.Min(Math.Max(Input.xblt, 0.0f), 1.0f) * 15.0f; //boost with left trigger

            pedEntity.ControlMovement = movexy;
            pedEntity.ControlJump = Input.kbjump || Input.ControllerButtonPressed(GamepadButtonFlags.X);
            pedEntity.ControlBoost = Input.ShiftPressed || Input.ControllerButtonPressed(GamepadButtonFlags.A |
                GamepadButtonFlags.RightShoulder | GamepadButtonFlags.LeftShoulder);
            var fire = mlb || Input.xbtrigs.Y > 0;
            if (fire && !ControlFireToggle) SpawnTestEntity(true);
            ControlFireToggle = fire;
        }
    }


    private void RenderWorld()
    {
        //start point for world view mode rendering.
        //also used for the water, paths, collisions, nav mesh, and the project window items.

        renderworldVisibleYmapDict.Clear();


        var hour = worldymaptimefilter ? (int)Renderer.timeofday : -1;
        var weathertype = worldymapweatherfilter
            ? weather.CurrentWeatherType?.NameHash ?? new MetaHash(0)
            : new MetaHash(0);

        IEnumerable<Entity> spaceEnts = null;

        if (renderworld)
        {
            Space.GetVisibleYmaps(camera, hour, weathertype, renderworldVisibleYmapDict);

            spaceEnts = Space.TemporaryEntities;
        }

        ProjectForm?.GetVisibleYmaps(camera, renderworldVisibleYmapDict);

        CutsceneForm?.GetVisibleYmaps(camera, renderworldVisibleYmapDict);

        Renderer.RenderWorld(renderworldVisibleYmapDict, spaceEnts);


        foreach (var ymap in Renderer.VisibleYmaps) UpdateMouseHits(ymap);


        if (renderwaterquads || SelectionMode == MapSelectionMode.WaterQuad) RenderWorldWaterQuads();
        if (SelectionMode == MapSelectionMode.WaveQuad) RenderWorldWaterWaveQuads();
        if (SelectionMode == MapSelectionMode.CalmingQuad) RenderWorldWaterCalmingQuads();
        if (rendercollisionmeshes || SelectionMode == MapSelectionMode.Collision) RenderWorldCollisionMeshes();
        if (renderpaths || SelectionMode == MapSelectionMode.Path) RenderWorldPaths();
        if (rendertraintracks || SelectionMode == MapSelectionMode.TrainTrack) RenderWorldTrainTracks();
        if (rendernavmeshes || SelectionMode == MapSelectionMode.NavMesh) RenderWorldNavMeshes();
        if (renderscenarios || SelectionMode == MapSelectionMode.Scenario) RenderWorldScenarios();
        if (renderpopzones || SelectionMode == MapSelectionMode.PopZone) RenderWorldPopZones();
        if (renderheightmaps || SelectionMode == MapSelectionMode.Heightmap) RenderWorldHeightmaps();
        if (renderwatermaps || SelectionMode == MapSelectionMode.Watermap) RenderWorldWatermaps();
        if (renderaudiozones || SelectionMode == MapSelectionMode.Audio) RenderWorldAudioZones();
    }

    private void RenderWorldCollisionMeshes()
    {
        //enqueue collision meshes for rendering - from the world grid

        collisionitems.Clear();
        Space.GetVisibleBounds(camera, collisionmeshrange, collisionmeshlayers, collisionitems);

        collisionybns.Clear();
        foreach (var ybn in collisionitems.Select(item => GameFileCache.GetYbn(item.Name))
                     .Where(ybn => ybn is { Loaded: true })) collisionybns.Add(ybn);

        collisioninteriors.Clear();
        foreach (var mlo in Renderer.VisibleMlos)
        {
            if (mlo.Archetype == null) return;
            var hash = mlo.Archetype.Hash;
            var ybn = GameFileCache.GetYbn(hash);
            if (ybn is { Loaded: true }) collisioninteriors[mlo] = ybn;
        }


        ProjectForm?.GetVisibleYbns(camera, collisionybns, collisioninteriors);

        foreach (var ybn in collisionybns.Where(ybn => ybn is { Loaded: true }))
            Renderer.RenderCollisionMesh(ybn.Bounds, null);

        foreach (var kvp in collisioninteriors.Where(kvp => kvp.Value is { Loaded: true }))
            Renderer.RenderCollisionMesh(kvp.Value.Bounds, kvp.Key);
    }

    private void RenderWorldWaterQuads()
    {
        var quads = RenderWorldBaseWaterQuads(water.WaterQuads, MapSelectionMode.WaterQuad);
        Renderer.RenderWaterQuads(quads);
    }

    private void RenderWorldWaterCalmingQuads()
    {
        RenderWorldBaseWaterQuads(water.CalmingQuads, MapSelectionMode.CalmingQuad);
    }

    private void RenderWorldWaterWaveQuads()
    {
        RenderWorldBaseWaterQuads(water.WaveQuads, MapSelectionMode.WaveQuad);
    }

    private List<T> RenderWorldBaseWaterQuads<T>(IEnumerable<T> quads, MapSelectionMode requiredMode)
        where T : BaseWaterQuad
    {
        var renderwaterquadlist = water.GetVisibleQuads(camera, quads);

        ProjectForm?.GetVisibleWaterQuads(camera, renderwaterquadlist);

        if (SelectionMode == requiredMode) UpdateMouseHits(renderwaterquadlist);

        return renderwaterquadlist;
    }

    private void RenderWorldPaths()
    {
        renderpathynds.Clear();

        Space.GetVisibleYnds(camera, renderpathynds);

        ProjectForm?.GetVisibleYnds(camera, renderpathynds);

        Renderer.RenderPaths(renderpathynds);

        UpdateMouseHits(renderpathynds);
    }

    private void RenderWorldTrainTracks()
    {
        if (!trains.Inited) return;

        rendertraintracklist.Clear();
        rendertraintracklist.AddRange(trains.TrainTracks);

        ProjectForm?.GetVisibleTrainTracks(camera, rendertraintracklist);

        Renderer.RenderTrainTracks(rendertraintracklist);

        UpdateMouseHits(rendertraintracklist);
    }

    private void RenderWorldNavMeshes()
    {
        rendernavmeshynvs.Clear();
        Space.GetVisibleYnvs(camera, collisionmeshrange, rendernavmeshynvs);

        ProjectForm?.GetVisibleYnvs(camera, rendernavmeshynvs);

        Renderer.RenderNavMeshes(rendernavmeshynvs);

        UpdateMouseHits(rendernavmeshynvs);
    }

    private void RenderWorldScenarios()
    {
        if (!scenarios.Inited) return;

        renderscenariolist.Clear();
        renderscenariolist.AddRange(scenarios.ScenarioRegions);

        ProjectForm?.GetVisibleScenarios(camera, renderscenariolist);

        Renderer.RenderScenarios(renderscenariolist);

        UpdateMouseHits(renderscenariolist);
    }

    private void RenderWorldPopZones()
    {
        if (!popzones.Inited) return;

        Renderer.RenderPopZones(popzones);
    }

    private void RenderWorldHeightmaps()
    {
        if (!heightmaps.Inited) return;


        Renderer.RenderBasePath(heightmaps);
    }

    private void RenderWorldWatermaps()
    {
        if (!watermaps.Inited) return;


        Renderer.RenderBasePath(watermaps);
    }

    private void RenderWorldAudioZones()
    {
        if (!audiozones.Inited) return;

        renderaudfilelist.Clear();
        renderaudfilelist.AddRange(GameFileCache.AudioDatRelFiles);

        ProjectForm?.GetVisibleAudioFiles(camera, renderaudfilelist);

        renderaudplacementslist.Clear();
        audiozones.GetPlacements(renderaudfilelist, renderaudplacementslist);


        var bbox = new BoundingBox();
        var bsph = new BoundingSphere();
        var mray = new Ray
        {
            Position = camera.MouseRay.Position + camera.Position,
            Direction = camera.MouseRay.Direction
        };

        var lastHitOuterBox = new MapBox();
        var lastHitOuterSphere = new MapSphere();
        var mb = new MapBox();
        var ms = new MapSphere();

        foreach (var placement in renderaudplacementslist)
        {
            float hitdist;
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

                    var hbcamrel = placement.Position - camera.Position;
                    var mraytrn = new Ray
                    {
                        Position = placement.OrientationInv.Multiply(camera.MouseRay.Position - hbcamrel),
                        Direction = placement.OrientationInv.Multiply(mray.Direction)
                    };
                    bbox.Minimum = placement.HitboxMin;
                    bbox.Maximum = placement.HitboxMax;
                    if (mraytrn.Intersects(ref bbox, out hitdist) && hitdist < CurMouseHit.HitDist && hitdist > 0)
                    {
                        CurMouseHit.Audio = placement;
                        CurMouseHit.HitDist = hitdist;
                        CurMouseHit.CamRel = hbcamrel;
                        CurMouseHit.AABB = bbox;
                        lastHitOuterBox = mb; //highlight the outer box
                    }

                    break;
                case Dat151ZoneShape.Sphere:

                    if (placement.InnerPos != Vector3.Zero && placement.OuterPos != Vector3.Zero)
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
                        if (mray.Intersects(ref bsph, out hitdist) && hitdist < CurMouseHit.HitDist && hitdist > 0)
                        {
                            CurMouseHit.Audio = placement;
                            CurMouseHit.HitDist = hitdist;
                            CurMouseHit.CamRel = placement.Position - camera.Position;
                            CurMouseHit.AABB = new BoundingBox(); //no box here
                            CurMouseHit.BSphere = bsph;
                            lastHitOuterSphere = ms; //highlight the outer sphere
                        }
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        if (CurMouseHit.Audio == null) return;
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
            default:
                throw new ArgumentOutOfRangeException();
        }
    }


    private void RenderSingleItem()
    {
        //start point for model view mode rendering

        if (!uint.TryParse(modelname, out var hash)) //try use a hash directly
            hash = JenkHash.GenHash(modelname);
        var arche = GameFileCache.GetArchetype(hash);

        Archetype selarch = null;
        DrawableBase seldrwbl = null;
        YmapEntityDef selent = null;

        if (arche != null)
        {
            Renderer.RenderArchetype(arche, null);

            selarch = arche;
        }
        else
        {
            var ymap = GameFileCache.GetYmap(hash);
            if (ymap != null)
            {
                Renderer.RenderYmap(ymap);
            }
            else
            {
                //not a ymap... see if it's a ydr or yft
                var ydr = GameFileCache.GetYdr(hash);
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
                    var yft = GameFileCache.GetYft(hash);
                    if (yft != null)
                        if (yft.Loaded)
                            if (yft.Fragment != null)
                            {
                                var f = yft.Fragment;

                                Renderer.RenderFragment(null, null, f, hash);

                                seldrwbl = f.Drawable;
                            }
                    //TODO: collision bounds single model...
                    //YbnFile ybn = gameFileCache.GetYbn(hash);
                }
            }
        }

        if (selarch != null) seldrwbl = GameFileCache.TryGetDrawable(selarch);

        //select this item for viewing by the UI...
        if (SelectedItem.Archetype == selarch && SelectedItem.Drawable == seldrwbl &&
            SelectedItem.EntityDef == selent) return;
        SelectedItem.Clear();
        SelectedItem.Archetype = selarch;
        SelectedItem.Drawable = seldrwbl;
        SelectedItem.EntityDef = null;
        UpdateSelectionUI(false);
    }


    private void RenderYmaps()
    {
        foreach (var lod in ymaplist)
        {
            var hash = JenkHash.GenHash(lod);
            var ymap = GameFileCache.GetYmap(hash);
            Renderer.RenderYmap(ymap);

            UpdateMouseHits(ymap);
        }
    }


    private void RenderMoused()
    {
        //immediately render the bounding box of the currently moused entity.

        if (!MouseSelectEnabled) return;

        PrevMouseHit = LastMouseHit;
        LastMouseHit = CurMouseHit;

        var change = LastMouseHit.CheckForChanges(PrevMouseHit); //(LastMouseHit.EntityDef != PrevMouseHit.EntityDef);
        if (SelectByGeometry) change = change || LastMouseHit.Geometry != PrevMouseHit.Geometry;

        if (change)
        {
            var text = LastMouseHit.GetFullNameString(string.Empty);
            UpdateMousedLabel(text);
        }

        if (!CurMouseHit.HasHit) return;


        var mode = BoundsShaderMode.Box;
        var bsphrad = CurMouseHit.BSphere.Radius;
        var bbmin = CurMouseHit.AABB.Minimum;
        var bbmax = CurMouseHit.AABB.Maximum;
        var camrel = CurMouseHit.CamRel;
        var scale = Vector3.One;
        var ori = Quaternion.Identity;
        var ext = CurMouseHit.ArchetypeExtension != null || CurMouseHit.EntityExtension != null ||
                  CurMouseHit.CollisionBounds != null;
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

        if (CurMouseHit.Geometry != null || ext)
        {
            bbmin = CurMouseHit.AABB.Minimum; //override archetype AABB..
            bbmax = CurMouseHit.AABB.Maximum;
        }

        if (CurMouseHit.CarGenerator != null) ori = CurMouseHit.CarGenerator.Orientation;
        if (CurMouseHit.BoxOccluder != null) ori = CurMouseHit.BoxOccluder.Orientation;
        if (CurMouseHit.OccludeModelTri != null)
        {
            var otri = CurMouseHit.OccludeModelTri;
            Renderer.RenderSelectionTriangleOutline(otri.Corner1, otri.Corner2, otri.Corner3, 0xFFFFFFFF);
            return;
        }

        if (CurMouseHit.MloEntityDef != null) scale = Vector3.One;
        if (CurMouseHit.WaterQuad != null)
        {
        }

        if (CurMouseHit.ScenarioNode != null)
        {
            var sp = CurMouseHit.ScenarioNode.MyPoint ?? CurMouseHit.ScenarioNode.ClusterMyPoint;
            if (sp != null) //orientate the moused box for the correct scenario point direction...
                ori = sp.Orientation;
        }

        if (CurMouseHit.NavPoint != null) ori = CurMouseHit.NavPoint.Orientation;
        if (CurMouseHit.NavPortal != null) ori = CurMouseHit.NavPortal.Orientation;
        if (CurMouseHit.NavPoly != null)
        {
            Renderer.RenderSelectionNavPolyOutline(CurMouseHit.NavPoly, 0xFFFFFFFF);
            return;
        }

        if (CurMouseHit.Audio != null)
        {
            ori = CurMouseHit.Audio.Orientation;
            if (CurMouseHit.Audio.Shape == Dat151ZoneShape.Sphere) mode = BoundsShaderMode.Sphere;
        }

        if (CurMouseHit.CollisionVertex != null)
        {
            var vpos = CurMouseHit.CollisionVertex.Position;
            var crpos = camrel + ori.Multiply(vpos);
            var vertexSize = 0.1f;
            Renderer.RenderSelectionCircle(vpos, vertexSize, 0xFFFFFFFF);
        }

        if (CurMouseHit.CollisionPoly != null)
            Renderer.RenderSelectionCollisionPolyOutline(CurMouseHit.CollisionPoly, 0xFFFFFFFF, CurMouseHit.EntityDef);
        if (CurMouseHit.CollisionBounds != null) ori = ori * CurMouseHit.BBOrientation;


        Renderer.RenderMouseHit(mode, ref camrel, ref bbmin, ref bbmax, ref scale, ref ori, bsphrad);
    }

    private void RenderSelection()
    {
        if (SelectedItem.MultipleSelectionItems != null)
            foreach (var t in SelectedItem.MultipleSelectionItems)
            {
                var item = t;
                RenderSelection(ref item);
            }
        else
            RenderSelection(ref SelectedItem);
    }

    private void RenderSelection(ref MapSelection selectionItem)
    {
        //immediately render the bounding box of the current selection. also, arrows.

        const uint cred = 0xFF0000FF;
        const uint cgrn = 0xFF00FF00;
        const uint cblu = 0xFFFF0000;
        const uint caqu = 0xFFFFFF00;

        if (ControlBrushEnabled && MouseRayCollision.Hit)
        {
            var arup = MouseRayCollision.Normal.GetPerpVec();
            Renderer.RenderBrushRadiusOutline(MouseRayCollision.Position, MouseRayCollision.Normal, arup,
                ProjectForm.GetInstanceBrushRadius(), cgrn);
        }

        if (MouseRayCollisionVisible && MouseRayCollision.Hit)
        {
            var arup = MouseRayCollision.Normal.GetPerpVec();
            Renderer.RenderSelectionArrowOutline(MouseRayCollision.Position, MouseRayCollision.Normal, arup,
                Quaternion.Identity, 1.0f, 0.05f, cgrn);
        }

        if (!ShowSelectionBounds) return;

        if (!selectionItem.HasValue) return;


        var mode = BoundsShaderMode.Box;
        var bsphrad = selectionItem.BSphere.Radius;
        var bbmin = selectionItem.AABB.Minimum;
        var bbmax = selectionItem.AABB.Maximum;
        var camrel = -camera.Position;
        var scale = Vector3.One;
        var ori = Quaternion.Identity;


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

            if (EditEntityPivot) Renderer.RenderSelectionEntityPivot(ent);
        }

        if (selectionItem.CarGenerator != null)
        {
            var cg = selectionItem.CarGenerator;
            camrel = cg.Position - camera.Position;
            ori = cg.Orientation;
            bbmin = cg.BBMin;
            bbmax = cg.BBMax;
            var arrowlen = cg._CCarGen.perpendicularLength;
            var arrowrad = arrowlen * 0.066f;
            Renderer.RenderSelectionArrowOutline(cg.Position, Vector3.UnitX, Vector3.UnitY, ori, arrowlen, arrowrad,
                cgrn);

            if (!Renderer.rendercars) //only render selected car if not already rendering cars..
            {
                var cgtrn = Quaternion.RotationAxis(Vector3.UnitZ,
                    (float)Math.PI * -0.5f); //car fragments currently need to be rotated 90 deg right...
                var cgori = Quaternion.Multiply(ori, cgtrn);

                Renderer.RenderCar(cg.Position, cgori, cg._CCarGen.carModel, cg._CCarGen.popGroup);
            }
        }

        if (selectionItem.WaveQuad != null)
        {
            var quad = selectionItem.WaveQuad;
            var quadArrowPos = new Vector3(quad.minX + (quad.maxX - quad.minX) * 0.5f,
                quad.minY + (quad.maxY - quad.minY) * 0.5f, 5);
            var waveOri = quad.WaveOrientation;
            var arrowlen = quad.Amplitude * 50;
            var arrowrad = arrowlen * 0.066f;
            Renderer.RenderSelectionArrowOutline(quadArrowPos, Vector3.UnitX, Vector3.UnitY, waveOri, arrowlen,
                arrowrad, cgrn);
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

        if (selectionItem.PathNode != null) camrel = selectionItem.PathNode.Position - camera.Position;
        if (selectionItem.TrainTrackNode != null) camrel = selectionItem.TrainTrackNode.Position - camera.Position;
        if (selectionItem.ScenarioNode != null)
        {
            camrel = selectionItem.ScenarioNode.Position - camera.Position;

            var sn = selectionItem.ScenarioNode;

            //render direction arrow for ScenarioPoint
            ori = sn.Orientation;
            var arrowlen = 2.0f;
            var arrowrad = 0.25f;
            Renderer.RenderSelectionArrowOutline(sn.Position, Vector3.UnitY, Vector3.UnitZ, ori, arrowlen, arrowrad,
                cgrn);

            var vpoint = sn.MyPoint ?? sn.ClusterMyPoint;
            if (vpoint != null && (vpoint?.Type?.IsVehicle ?? false))
            {
                var vhash = vpoint.ModelSet?.NameHash ?? 493038497; //"none"
                if (vhash == 0 || vhash == 493038497) vhash = vpoint.Type?.VehicleModelSetHash ?? 0;
                if (vhash == 0 && sn.ChainingNode?.Chain?.Edges is { Length: > 0 })
                {
                    var fedge = sn.ChainingNode.Chain.Edges[0]; //for chain nodes, show the first node's model...
                    var fnode = fedge?.NodeFrom?.ScenarioNode;
                    if (fnode != null)
                    {
                        vpoint = fnode.MyPoint ?? fnode.ClusterMyPoint;
                        vhash = vpoint.ModelSet?.NameHash ?? 493038497; //"none"
                        if (vhash == 0 || vhash == 493038497) vhash = vpoint.Type?.VehicleModelSetHash ?? 0;
                    }
                }

                Renderer.RenderCar(sn.Position, sn.Orientation, 0, vhash, true);
            }
        }

        if (selectionItem.ScenarioEdge != null)
        {
            //render scenario edge arrow
            var se = selectionItem.ScenarioEdge;
            var sn1 = se.NodeFrom;
            var sn2 = se.NodeTo;
            if (sn1 != null && sn2 != null)
            {
                var dirp = sn2.Position - sn1.Position;
                var dl = dirp.Length();
                var dir = dirp * (1.0f / dl);
                var dup = Vector3.UnitZ;
                var aori = Quaternion.Invert(Quaternion.RotationLookAtRH(dir, dup));
                var arrowrad = 0.25f;
                var arrowlen = Math.Max(dl - arrowrad * 5.0f, 0);
                Renderer.RenderSelectionArrowOutline(sn1.Position, -Vector3.UnitZ, Vector3.UnitY, aori, arrowlen,
                    arrowrad, cblu);
            }
        }

        if (selectionItem.MloEntityDef != null)
        {
            bbmin = selectionItem.AABB.Minimum;
            bbmax = selectionItem.AABB.Maximum;
            var mlo = selectionItem.MloEntityDef;
            var mlop = mlo.Position;
            if (mlo.Archetype is MloArchetype mloa)
            {
                var p1 = new VertexTypePC();
                var p2 = new VertexTypePC();
                if (mloa.portals != null)
                    foreach (var portal in mloa.portals)
                    {
                        if (portal.Corners == null) continue;
                        p2.Colour = caqu;
                        if ((portal._Data.flags & 4) > 0) p2.Colour = cblu;
                        var pcl = portal.Corners.Length;
                        for (var ic = 0; ic < pcl; ic++)
                        {
                            var icn = ic + 1;
                            if (icn >= pcl) icn = 0;
                            p1.Colour = ic == 0 ? cred : p2.Colour; //highlight index 0 and winding direction
                            p1.Position = mlop + mlo.Orientation.Multiply(portal.Corners[ic].XYZ());
                            p2.Position = mlop + mlo.Orientation.Multiply(portal.Corners[icn].XYZ());
                            Renderer.SelectionLineVerts.Add(p1);
                            Renderer.SelectionLineVerts.Add(p2);
                        }
                    }

                if (mloa.rooms != null)
                {
                    var wbox = new MapBox
                    {
                        Scale = Vector3.One
                    };
                    for (var ir = 0; ir < mloa.rooms.Length; ir++)
                    {
                        var room = mloa.rooms[ir];

                        wbox.CamRelPos = mlop - camera.Position;
                        wbox.BBMin = room._Data.bbMin; // + offset;
                        wbox.BBMax = room._Data.bbMax; // + offset;
                        wbox.Orientation = mlo.Orientation;
                        if (ir == 0 || room.RoomName == "limbo")
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
        }

        if (selectionItem.ArchetypeExtension != null || selectionItem.EntityExtension != null ||
            selectionItem.CollisionBounds != null)
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
            return; //don't render a selection box for nav poly
        }

        if (selectionItem.NavPoint != null)
        {
            var navp = selectionItem.NavPoint;
            camrel = navp.Position - camera.Position;

            //render direction arrow for NavPoint
            ori = navp.Orientation;
            var arrowlen = 2.0f;
            var arrowrad = 0.25f;
            Renderer.RenderSelectionArrowOutline(navp.Position, -Vector3.UnitY, Vector3.UnitZ, ori, arrowlen, arrowrad,
                cgrn);
        }

        if (selectionItem.NavPortal != null)
        {
            var navp = selectionItem.NavPortal;
            camrel = navp.Position - camera.Position;

            //render direction arrow for NavPortal
            ori = navp.Orientation;
            var arrowlen = 2.0f;
            var arrowrad = 0.25f;
            Renderer.RenderSelectionArrowOutline(navp.Position, Vector3.UnitY, Vector3.UnitZ, ori, arrowlen, arrowrad,
                cgrn);
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
                var wsph = new MapSphere
                {
                    CamRelPos = au.OuterPos - camera.Position,
                    Radius = au.OuterRadius
                };
                Renderer.WhiteSpheres.Add(wsph);
            }
            else
            {
                var wbox = new MapBox
                {
                    CamRelPos = au.OuterPos - camera.Position,
                    BBMin = au.OuterMin,
                    BBMax = au.OuterMax,
                    Orientation = au.OuterOri,
                    Scale = scale
                };
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

        switch (mode)
        {
            case BoundsShaderMode.Box:
            {
                var box = new MapBox
                {
                    CamRelPos = camrel,
                    BBMin = bbmin,
                    BBMax = bbmax,
                    Orientation = ori,
                    Scale = scale
                };
                Renderer.SelectionBoxes.Add(box);
                break;
            }
            case BoundsShaderMode.Sphere:
            {
                var sph = new MapSphere
                {
                    CamRelPos = camrel,
                    Radius = bsphrad
                };
                Renderer.SelectionSpheres.Add(sph);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
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
        var uplimit = 3.0f;
        var downlimit = 20.0f;
        var ray = new Ray(p, new Vector3(0, 0, -1.0f));
        ray.Position.Z += 0.1f;
        var hit = Space.RayIntersect(ray, downlimit);
        if (hit.Hit) return hit.Position;
        ray.Position.Z += uplimit;
        hit = Space.RayIntersect(ray, downlimit);
        return hit.Hit ? hit.Position : p;
    }

    private Vector3 SnapPosition(Vector3 p)
    {
        var gpos = (p / SnapGridSize).Round() * SnapGridSize;
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

        ProjectForm?.OnWorldSelectionModified(SelectedItem);
    }

    private void Widget_OnRotationChange(Quaternion newrot, Quaternion oldrot)
    {
        //called during UpdateWidgets()
        if (newrot == oldrot) return;

        SelectedItem.SetRotation(newrot, EditEntityPivot);

        SelectedItem.UpdateGraphics(this);

        if (ProjectForm != null) ProjectForm.OnWorldSelectionModified(SelectedItem);
    }

    private void Widget_OnScaleChange(Vector3 newscale, Vector3 oldscale)
    {
        //called during UpdateWidgets()
        if (newscale == oldscale) return;

        SelectedItem.SetScale(newscale, EditEntityPivot);

        SelectedItem.UpdateGraphics(this);

        if (ProjectForm != null) ProjectForm.OnWorldSelectionModified(SelectedItem);
    }

    public void SetWidgetPosition(Vector3 pos, bool enableUndo = false)
    {
        if (enableUndo)
        {
            SetWidgetMode("Position");
            MarkUndoStart(Widget);
        }

        Widget.Position = pos;

        if (enableUndo) MarkUndoEnd(Widget);
    }

    public void SetWidgetRotation(Quaternion q, bool enableUndo = false)
    {
        if (enableUndo)
        {
            SetWidgetMode("Rotation");
            MarkUndoStart(Widget);
        }

        Widget.Rotation = q;

        if (enableUndo) MarkUndoEnd(Widget);
    }

    public void SetWidgetScale(Vector3 s, bool enableUndo = false)
    {
        if (enableUndo)
        {
            SetWidgetMode("Scale");
            MarkUndoStart(Widget);
        }

        Widget.Scale = s;

        if (enableUndo) MarkUndoEnd(Widget);
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

    public void UpdatePathYndGraphics(YndFile ynd, bool fullupdate)
    {
        if (ynd == null) return;

        var selection = SelectedItem.PathNode != null
            ? new[] { SelectedItem.PathNode }
            : null;

        if (fullupdate)
        {
            ynd.UpdateAllNodePositions();
            ynd.BuildBVH();

            Space.BuildYndData(ynd);
        }
        else
        {
            ynd.UpdateAllNodePositions();
            Space.BuildYndVerts(ynd, selection);
        }

        //lock (Renderer.RenderSyncRoot)
        {
            Renderer.Invalidate(ynd);
        }
    }

    public void UpdatePathNodeGraphics(YndNode pathnode, bool fullupdate)
    {
        if (pathnode == null) return;
        pathnode.Ynd.UpdateBvhForNode(pathnode);
        UpdatePathYndGraphics(pathnode.Ynd, fullupdate);
    }

    public YndNode GetPathNodeFromSpace(ushort areaid, ushort nodeid)
    {
        return Space.NodeGrid.GetYndNode(areaid, nodeid);
    }

    public void UpdateCollisionBoundsGraphics(Bounds b)
    {
        lock (Renderer.RenderSyncRoot)
        {
            if (b is BoundBVH bvh)
                bvh.BuildBVH();
            else if (b is BoundComposite bc) bc.BuildBVH();

            var ib = b;
            while (ib.Parent != null) ib = ib.Parent;

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

    public void UpdateNavPolyGraphics(YnvPoly poly, bool fullupdate)
    {
        if (poly == null) return;
        //poly.Ynv.UpdateBvhForPoly(poly);//TODO!
        UpdateNavYnvGraphics(poly.Ynv, fullupdate);
    }

    public void UpdateNavPointGraphics(YnvPoint point, bool fullupdate)
    {
        if (point == null) return;
        //poly.Ynv.UpdateBvhForPoint(point);//TODO!
        UpdateNavYnvGraphics(point.Ynv, fullupdate);
    }

    public void UpdateNavPortalGraphics(YnvPortal portal, bool fullupdate)
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

    public void UpdateTrainTrackNodeGraphics(TrainTrackNode node, bool fullupdate)
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
        ToolbarSelectButton.ToolTipText =
            $@"Select objects / Exit edit mode ({kb.ToggleMouseSelect}, {kb.ExitEditMode})";
        ToolbarMoveButton.ToolTipText = $@"Move ({kb.EditPosition})";
        ToolbarRotateButton.ToolTipText = $@"Rotate ({kb.EditRotation})";
        ToolbarScaleButton.ToolTipText = $@"Scale ({kb.EditScale})";
        ShowToolbarCheckBox.Text = $@"Show Toolbar ({kb.ToggleToolbar})";
    }


    private MapBox GetExtensionBox(Vector3 camrel, MetaWrapper ext)
    {
        var b = new MapBox();
        var pos = Vector3.Zero;
        var size = 0.5f;
        switch (ext)
        {
            case MCExtensionDefLightEffect le:
                pos = le.Data.offsetPosition;
                break;
            case MCExtensionDefSpawnPointOverride spo:
                pos = spo.Data.offsetPosition;
                size = spo.Data.Radius;
                break;
            case MCExtensionDefDoor door:
                pos = door.Data.offsetPosition;
                break;
            case Mrage__phVerletClothCustomBounds cb:
            {
                if (cb.CollisionData is { Length: > 0 }) pos = cb.CollisionData[0].Data.Position;
                break;
            }
            case MCExtensionDefParticleEffect pe:
                pos = pe.Data.offsetPosition;
                break;
            case MCExtensionDefAudioCollisionSettings acs:
                pos = acs.Data.offsetPosition;
                break;
            case MCExtensionDefAudioEmitter ae:
                pos = ae.Data.offsetPosition;
                break;
            case MCExtensionDefSpawnPoint point:
            {
                pos = point.Data.offsetPosition;
                break;
            }
            case MCExtensionDefExplosionEffect effect:
            {
                pos = effect.Data.offsetPosition;
                break;
            }
            case MCExtensionDefLadder ladder:
            {
                pos = ladder.Data.offsetPosition;
                break;
            }
            case MCExtensionDefBuoyancy buoyancy:
            {
                pos = buoyancy.Data.offsetPosition;
                break;
            }
            case MCExtensionDefExpression expression:
            {
                pos = expression.Data.offsetPosition;
                break;
            }
            case MCExtensionDefLightShaft shaft:
            {
                pos = shaft.Data.offsetPosition;
                break;
            }
            case MCExtensionDefWindDisturbance disturbance:
            {
                pos = disturbance.Data.offsetPosition;
                break;
            }
            case MCExtensionDefProcObject procObject:
            {
                pos = procObject.Data.offsetPosition;
                break;
            }
        }


        b.BBMin = pos - size;
        b.BBMax = pos + size;
        b.CamRelPos = camrel;

        return b;
    }


    private void SpawnTestEntity(bool cameraCenter = false)
    {
        if (!Space.Inited) return;

        var dir = cameraCenter ? camera.ViewDirection : camera.MouseRay.Direction;
        var ofs = cameraCenter ? Vector3.Zero : camera.MouseRay.Position;
        var pos = ofs + camera.Position + dir * 1.5f;
        var vel = dir * 50.0f; //m/s

        var hash = JenkHash.GenHash("prop_alien_egg_01");
        var arch = GameFileCache.GetArchetype(hash);

        if (arch == null) return;

        var cent = new CEntityDef
        {
            archetypeName = hash,
            rotation = new Vector4(0, 0, 0, 1),
            scaleXY = 1.0f,
            scaleZ = 1.0f,
            flags = 1572872,
            parentIndex = -1,
            lodDist = 200.0f,
            lodLevel = rage__eLodType.LODTYPES_DEPTH_ORPHANHD,
            priorityLevel = rage__ePriorityLevel.PRI_REQUIRED,
            ambientOcclusionMultiplier = 255,
            artificialAmbientOcclusion = 255,
            position = pos
        };

        var ent = new YmapEntityDef(null, 0, ref cent);

        ent.SetArchetype(arch);


        var e = new Entity
        {
            Position = pos,
            Velocity = vel,
            Mass = 10.0f
        };
        e.Momentum = vel * e.Mass;
        e.EntityDef = ent;
        e.Radius = arch.BSRadius * 0.7f;
        e.EnableCollisions = true;
        e.Enabled = true;
        e.Lifetime = 20.0f;

        lock (Renderer.RenderSyncRoot)
        {
            Space.AddTemporaryEntity(e);
        }
    }


    public void SetControlMode(WorldControlMode mode)
    {
        if (InvokeRequired)
        {
            try
            {
                Invoke(() => { SetControlMode(mode); });
            }
            catch
            {
                // ignored
            }

            return;
        }

        if (mode == ControlMode) return;

        var wasfree = ControlMode == WorldControlMode.Free || ControlBrushEnabled;
        var isfree = mode == WorldControlMode.Free || ControlBrushEnabled;

        switch (isfree)
        {
            case true when !wasfree:
                camEntity.Position = pedEntity.Position;

                pedEntity.Enabled = false;

                Renderer.timerunning = false;

                camera.SetFollowEntity(camEntity);
                camera.TargetDistance = 1.0f; //default?
                camera.Smoothness = Settings.Default.CameraSmoothing;

                Cursor.Show();
                break;
            case false when wasfree:
            {
                pedEntity.Position = camEntity.Position;
                pedEntity.Velocity = Vector3.Zero;
                pedEntity.Enabled = true;

                Renderer.timerunning = true;

                camera.SetFollowEntity(pedEntity.CameraEntity);
                camera.TargetDistance = 0.01f; //1cm
                camera.Smoothness = 20.0f;

                //center the mouse in the window
                var centerp = new Point(ClientSize.Width / 2, ClientSize.Height / 2);
                MouseLastPoint = centerp;
                MouseX = centerp.X;
                MouseY = centerp.Y;
                Cursor.Position = PointToScreen(centerp);
                Cursor.Hide();
                break;
            }
        }


        ControlMode = mode;
    }


    private void BeginMouseHitTest()
    {
        //reset variables for beginning the mouse hit test
        CurMouseHit.Clear();


        if (Input.CtrlPressed && ProjectForm != null &&
            ProjectForm.CanPaintInstances()) // Get whether or not we can brush from the project form.
        {
            ControlBrushEnabled = true;
            MouseRayCollisionVisible = false;
            MouseRayCollision = GetSpaceMouseRay();
        }
        else
        {
            ControlBrushEnabled = false;
            if (Input.CtrlPressed && MouseRayCollisionEnabled)
            {
                MouseRayCollisionVisible = true;
                MouseRayCollision = GetSpaceMouseRay();
            }
            else
            {
                MouseRayCollisionVisible = false;
            }
        }


        Renderer.RenderedDrawablesListEnable =
            (SelectionMode == MapSelectionMode.Entity && MouseSelectEnabled) ||
            SelectionMode == MapSelectionMode.EntityExtension ||
            SelectionMode == MapSelectionMode.ArchetypeExtension;

        Renderer.RenderedBoundCompsListEnable = SelectionMode == MapSelectionMode.Collision;
    }

    public SpaceRayIntersectResult GetSpaceMouseRay()
    {
        var ret = new SpaceRayIntersectResult();
        if (!Space.Inited || Space.BoundsStore == null) return ret;
        var mray = new Ray
        {
            Position = camera.MouseRay.Position + camera.Position,
            Direction = camera.MouseRay.Direction
        };
        return Space.RayIntersect(mray, float.MaxValue, collisionmeshlayers);
    }

    public SpaceRayIntersectResult Raycast(Ray ray)
    {
        return Space.RayIntersect(ray, float.MaxValue, collisionmeshlayers);
    }

    private void UpdateMouseHits()
    {
        UpdateMouseHitsFromRenderer();
        UpdateMouseHitsFromSpace();
        UpdateMouseHitsFromProject();
    }

    private void UpdateMouseHitsFromRenderer()
    {
        foreach (var rd in Renderer.RenderedDrawables) UpdateMouseHits(rd.Drawable, rd.Archetype, rd.Entity);
    }

    private void UpdateMouseHitsFromSpace()
    {
        if (SelectionMode != MapSelectionMode.Collision) return;
        MouseRayCollision = GetSpaceMouseRay();

        if (MouseRayCollision.Hit) CurMouseHit.UpdateCollisionFromRayHit(ref MouseRayCollision, camera);
    }

    private void UpdateMouseHitsFromProject()
    {
        if (ProjectForm == null) return;

        if (SelectionMode == MapSelectionMode.Collision) ProjectForm.GetMouseCollision(camera, ref CurMouseHit);
    }

    private void UpdateMouseHits(DrawableBase drawable, Archetype arche, YmapEntityDef entity)
    {
        //first test the bounding sphere for mouse hit..
        Quaternion orinv;
        Ray mraytrn;
        var hitdist = 0.0f;
        var geometryIndex = 0;
        DrawableGeometry geometry = null;
        var geometryAABB = new BoundingBox();
        var bsph = new BoundingSphere();
        var bbox = new BoundingBox();
        var gbbox = new BoundingBox();
        var orientation = Quaternion.Identity;
        var scale = Vector3.One;
        var camrel = -camera.Position;
        if (entity != null)
        {
            orientation = entity.Orientation;
            scale = entity.Scale;
            camrel += entity.Position;
        }

        if (arche != null)
        {
            bsph.Center = camrel + orientation.Multiply(arche.BSCenter); //could use entity.BSCenter
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

        var mousespherehit = camera.MouseRay.Intersects(ref bsph);


        if (SelectionMode is MapSelectionMode.EntityExtension or MapSelectionMode.ArchetypeExtension)
        {
            //transform the mouse ray into the entity space.
            orinv = Quaternion.Invert(orientation);
            mraytrn = new Ray
            {
                Position = orinv.Multiply(camera.MouseRay.Position - camrel),
                Direction = orinv.Multiply(camera.MouseRay.Direction)
            };

            switch (SelectionMode)
            {
                case MapSelectionMode.EntityExtension when entity?.Extensions == null:
                    return;
                case MapSelectionMode.EntityExtension:
                {
                    foreach (var extension in entity.Extensions)
                    {
                        var mb = GetExtensionBox(camrel, extension);
                        mb.Orientation = orientation;
                        mb.Scale = Vector3.One; // scale;
                        mb.BBMin *= scale;
                        mb.BBMax *= scale;
                        Renderer.BoundingBoxes.Add(mb);

                        bbox.Minimum = mb.BBMin; //TODO: refactor this!
                        bbox.Maximum = mb.BBMax;
                        if (!mraytrn.Intersects(ref bbox, out hitdist) || !(hitdist < CurMouseHit.HitDist) ||
                            !(hitdist > 0)) continue;
                        CurMouseHit.EntityDef = entity;
                        CurMouseHit.Archetype = arche;
                        CurMouseHit.EntityExtension = extension;
                        CurMouseHit.HitDist = hitdist;
                        CurMouseHit.CamRel = mb.CamRelPos;
                        CurMouseHit.AABB = bbox;
                    }

                    return; //only test extensions when in select extension mode...
                }
                case MapSelectionMode.ArchetypeExtension:
                {
                    if (arche is not { Extensions: not null }) return;
                    foreach (var extension in arche.Extensions)
                    {
                        var mb = GetExtensionBox(camrel, extension);
                        mb.Orientation = orientation;
                        mb.Scale = Vector3.One; // scale;
                        mb.BBMin *= scale;
                        mb.BBMax *= scale;
                        Renderer.BoundingBoxes.Add(mb);

                        bbox.Minimum = mb.BBMin; //TODO: refactor this!
                        bbox.Maximum = mb.BBMax;
                        if (!mraytrn.Intersects(ref bbox, out hitdist) || !(hitdist < CurMouseHit.HitDist) ||
                            !(hitdist > 0)) continue;
                        CurMouseHit.EntityDef = entity;
                        CurMouseHit.Archetype = arche;
                        CurMouseHit.ArchetypeExtension = extension;
                        CurMouseHit.HitDist = hitdist;
                        CurMouseHit.CamRel = mb.CamRelPos;
                        CurMouseHit.AABB = bbox;
                    }

                    return; //only test extensions when in select extension mode...
                }
            }
        }


        if (!mousespherehit) return; //no sphere hit, so no entity hit.


        var usegeomboxes = SelectByGeometry;
        var dmodels = drawable.DrawableModels?.High;
        if (dmodels == null) usegeomboxes = false;
        if (usegeomboxes)
            if (dmodels.Any(m => m.BoundsData == null))
                usegeomboxes = false;


        //transform the mouse ray into the entity space.
        orinv = Quaternion.Invert(orientation);
        mraytrn = new Ray
        {
            Position = orinv.Multiply(camera.MouseRay.Position - camrel),
            Direction = orinv.Multiply(camera.MouseRay.Direction)
        };
        hitdist = 0.0f;


        if (usegeomboxes)
        {
            //geometry bounding boxes version
            var ghitdist = float.MaxValue;
            foreach (var m in dmodels)
            {
                var gbbcount = m.BoundsData.Length;
                for (var j = 0; j < gbbcount; j++) //first box seems to be whole model
                {
                    var gbox = m.BoundsData[j];
                    gbbox.Minimum = gbox.Min.XYZ();
                    gbbox.Maximum = gbox.Max.XYZ();
                    bbox.Minimum = gbbox.Minimum * scale;
                    bbox.Maximum = gbbox.Maximum * scale;
                    var usehit = false;
                    if (mraytrn.Intersects(ref bbox, out hitdist))
                    {
                        if (j == 0 && gbbcount > 1) continue; //ignore a model hit
                        //bool firsthit = (mousehit.EntityDef == null);
                        if (hitdist > 0.0f) //firsthit || //ignore when inside the box
                        {
                            var nearer = hitdist < CurMouseHit.HitDist && hitdist < ghitdist;
                            var radsm = true;
                            if (CurMouseHit.Geometry != null)
                            {
                                var b1 = (gbbox.Maximum - gbbox.Minimum) * scale;
                                var b2 = (CurMouseHit.AABB.Maximum - CurMouseHit.AABB.Minimum) * scale;
                                var r1 = b1.Length() * 0.5f;
                                var r2 = b2.Length() * 0.5f;
                                radsm = r1 < r2; // * 0.5f));
                            }

                            if ((nearer && radsm) || radsm) usehit = true;
                        }
                    }
                    else if (j == 0) //no hit on model box
                    {
                        break; //don't try this model's geometries
                    }

                    if (!usehit) continue;
                    var gind = j > 0 ? j - 1 : 0;
                    ghitdist = hitdist;
                    geometry = m.Geometries[gind];
                    geometryAABB = gbbox;
                    geometryIndex = gind;
                }
            }

            if (geometry == null) return; //no geometry hit.
            hitdist = ghitdist;
        }
        else
        {
            //archetype/drawable bounding boxes version
            var outerhit = false;
            if (mraytrn.Intersects(ref bbox, out hitdist)) //test primary box
            {
                var firsthit = CurMouseHit.EntityDef == null;
                if (firsthit || hitdist > 0.0f) //ignore when inside the box..
                {
                    var nearer = hitdist < CurMouseHit.HitDist; //closer than the last..
                    var radsm = true;
                    if (CurMouseHit.Archetype != null && arche != null) //compare hit archetype sizes...
                    {
                        //var b1 = (arche.BBMax - arche.BBMin) * scale;
                        //var b2 = (mousehit.Archetype.BBMax - mousehit.Archetype.BBMin) * scale;
                        var r1 = arche.BSRadius;
                        var r2 = CurMouseHit.Archetype.BSRadius;
                        radsm = r1 <= r2; // * 0.5f)); //prefer selecting smaller things
                    }

                    if ((nearer && radsm) || radsm) outerhit = true;
                }
            }

            if (!outerhit) return; //no hit.
        }


        CurMouseHit.HitDist = hitdist > 0.0f ? hitdist : CurMouseHit.HitDist;
        CurMouseHit.EntityDef = entity;
        CurMouseHit.Archetype = arche;
        CurMouseHit.Drawable = drawable;
        CurMouseHit.Geometry = geometry;
        CurMouseHit.AABB = geometryAABB;
        CurMouseHit.GeometryIndex = geometryIndex;
        CurMouseHit.CamRel = camrel;
    }

    private void UpdateMouseHits(YmapFile ymap)
    {
        //find mouse hits for things like MLOs, time cycle mods, grass batches, and car generators in ymaps.

        var bbox = new BoundingBox();
        var mray = new Ray
        {
            Position = camera.MouseRay.Position + camera.Position,
            Direction = camera.MouseRay.Direction
        };
        var hitdist = float.MaxValue;

        var dmax = Renderer.renderboundsmaxdist;

        switch (SelectionMode)
        {
            case MapSelectionMode.TimeCycleModifier when ymap.TimeCycleModifiers != null:
            {
                foreach (var tcm in ymap.TimeCycleModifiers)
                {
                    if (((tcm.BBMin + tcm.BBMax) * 0.5f - camera.Position).Length() > dmax) continue;

                    var mb = new MapBox
                    {
                        CamRelPos = -camera.Position,
                        BBMin = tcm.BBMin,
                        BBMax = tcm.BBMax,
                        Orientation = Quaternion.Identity,
                        Scale = Vector3.One
                    };
                    Renderer.BoundingBoxes.Add(mb);

                    bbox.Minimum = mb.BBMin;
                    bbox.Maximum = mb.BBMax;
                    if (!mray.Intersects(ref bbox, out hitdist) || !(hitdist < CurMouseHit.HitDist) ||
                        !(hitdist > 0)) continue;
                    CurMouseHit.TimeCycleModifier = tcm;
                    CurMouseHit.HitDist = hitdist;
                    CurMouseHit.CamRel = mb.CamRelPos;
                    CurMouseHit.AABB = bbox;
                }

                break;
            }
            case MapSelectionMode.CarGenerator when ymap.CarGenerators != null:
            {
                foreach (var cg in ymap.CarGenerators)
                {
                    var mb = new MapBox
                    {
                        CamRelPos = cg.Position - camera.Position,
                        BBMin = cg.BBMin,
                        BBMax = cg.BBMax,
                        Orientation = cg.Orientation,
                        Scale = Vector3.One
                    };
                    Renderer.BoundingBoxes.Add(mb);

                    var orinv = Quaternion.Invert(cg.Orientation);
                    var mraytrn = new Ray
                    {
                        Position = orinv.Multiply(camera.MouseRay.Position - mb.CamRelPos),
                        Direction = orinv.Multiply(mray.Direction)
                    };
                    bbox.Minimum = mb.BBMin;
                    bbox.Maximum = mb.BBMax;
                    if (mraytrn.Intersects(ref bbox, out hitdist) && hitdist < CurMouseHit.HitDist && hitdist > 0)
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

                break;
            }
            case MapSelectionMode.MloInstance when ymap.MloEntities != null:
            {
                foreach (var ent in ymap.MloEntities)
                {
                    if (SelectedItem.MloEntityDef == ent) continue;
                    var mb = new MapBox
                    {
                        CamRelPos = ent.Position - camera.Position,
                        BBMin = new Vector3(-1.5f),
                        BBMax = new Vector3(1.5f),
                        Orientation = ent?.Orientation ?? Quaternion.Identity,
                        Scale = Vector3.One
                    };
                    Renderer.BoundingBoxes.Add(mb);

                    var orinv = Quaternion.Invert(mb.Orientation);
                    var mraytrn = new Ray
                    {
                        Position = orinv.Multiply(camera.MouseRay.Position - mb.CamRelPos),
                        Direction = orinv.Multiply(mray.Direction)
                    };
                    bbox.Minimum = mb.BBMin;
                    bbox.Maximum = mb.BBMax;
                    if (!mraytrn.Intersects(ref bbox, out hitdist) || !(hitdist < CurMouseHit.HitDist) ||
                        !(hitdist > 0)) continue;
                    CurMouseHit.MloEntityDef = ent;
                    CurMouseHit.EntityDef = ent;
                    CurMouseHit.HitDist = hitdist;
                    CurMouseHit.CamRel = mb.CamRelPos;
                    CurMouseHit.AABB = new BoundingBox(mb.BBMin, mb.BBMax);
                }

                break;
            }
            case MapSelectionMode.Grass when ymap.GrassInstanceBatches != null:
            {
                foreach (var gb in ymap.GrassInstanceBatches)
                {
                    if ((gb.Position - camera.Position).Length() > dmax) continue;

                    var mb = new MapBox
                    {
                        CamRelPos = -camera.Position,
                        BBMin = gb.AABBMin,
                        BBMax = gb.AABBMax,
                        Orientation = Quaternion.Identity,
                        Scale = Vector3.One
                    };
                    Renderer.BoundingBoxes.Add(mb);

                    bbox.Minimum = mb.BBMin;
                    bbox.Maximum = mb.BBMax;
                    if (!mray.Intersects(ref bbox, out hitdist) || !(hitdist < CurMouseHit.HitDist) ||
                        !(hitdist > 0)) continue;
                    CurMouseHit.GrassBatch = gb;
                    CurMouseHit.HitDist = hitdist;
                    CurMouseHit.CamRel = mb.CamRelPos;
                    CurMouseHit.AABB = bbox;
                }

                break;
            }
            case MapSelectionMode.LodLights when ymap.LODLights != null:
            {
                var ll = ymap.LODLights;
                if (((ll.BBMin + ll.BBMax) * 0.5f - camera.Position).Length() <= dmax)
                {
                    var mb = new MapBox
                    {
                        CamRelPos = -camera.Position,
                        BBMin = ll.BBMin,
                        BBMax = ll.BBMax,
                        Orientation = Quaternion.Identity,
                        Scale = Vector3.One
                    };
                    Renderer.BoundingBoxes.Add(mb);

                    if (ll.BVH != null) UpdateMouseHits(ll.BVH, ref mray);
                }

                break;
            }
            case MapSelectionMode.None:
                break;
            case MapSelectionMode.Entity:
                break;
            case MapSelectionMode.EntityExtension:
                break;
            case MapSelectionMode.ArchetypeExtension:
                break;
            case MapSelectionMode.WaterQuad:
                break;
            case MapSelectionMode.Collision:
                break;
            case MapSelectionMode.NavMesh:
                break;
            case MapSelectionMode.Path:
                break;
            case MapSelectionMode.TrainTrack:
                break;
            case MapSelectionMode.Scenario:
                break;
            case MapSelectionMode.PopZone:
                break;
            case MapSelectionMode.Heightmap:
                break;
            case MapSelectionMode.Watermap:
                break;
            case MapSelectionMode.Audio:
                break;
            case MapSelectionMode.Occlusion:
                break;
            case MapSelectionMode.CalmingQuad:
                break;
            case MapSelectionMode.WaveQuad:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        switch (SelectionMode)
        {
            case MapSelectionMode.LodLights when ymap.DistantLODLights != null:
            {
                var dll = ymap.DistantLODLights;
                if (((dll.BBMin + dll.BBMax) * 0.5f - camera.Position).Length() <= dmax)
                {
                    var mb = new MapBox
                    {
                        CamRelPos = -camera.Position,
                        BBMin = dll.BBMin,
                        BBMax = dll.BBMax,
                        Orientation = Quaternion.Identity,
                        Scale = Vector3.One
                    };
                    Renderer.BoundingBoxes.Add(mb);
                }

                break;
            }
            case MapSelectionMode.Occlusion when ymap.BoxOccluders != null:
            {
                foreach (var bo in ymap.BoxOccluders)
                {
                    if ((bo.Position - camera.Position).Length() > dmax) continue;

                    Renderer.RenderBasePath(bo);

                    var mb = new MapBox
                    {
                        CamRelPos = bo.Position - camera.Position,
                        BBMin = bo.BBMin,
                        BBMax = bo.BBMax,
                        Orientation = bo.Orientation,
                        Scale = Vector3.One
                    };

                    //Renderer.BoundingBoxes.Add(mb);
                    var orinv = Quaternion.Invert(bo.Orientation);
                    var mraytrn = new Ray
                    {
                        Position = orinv.Multiply(camera.MouseRay.Position - mb.CamRelPos),
                        Direction = orinv.Multiply(mray.Direction)
                    };
                    bbox.Minimum = mb.BBMin;
                    bbox.Maximum = mb.BBMax;
                    if (!mraytrn.Intersects(ref bbox, out float hd) || !(hd < CurMouseHit.HitDist) ||
                        !(hd > 0)) continue;
                    hitdist = hd;
                    CurMouseHit.BoxOccluder = bo;
                    CurMouseHit.OccludeModelTri = null;
                    CurMouseHit.HitDist = hitdist;
                    CurMouseHit.CamRel = mb.CamRelPos;
                    CurMouseHit.AABB = bbox;
                }

                break;
            }
            case MapSelectionMode.None:
                break;
            case MapSelectionMode.Entity:
                break;
            case MapSelectionMode.EntityExtension:
                break;
            case MapSelectionMode.ArchetypeExtension:
                break;
            case MapSelectionMode.TimeCycleModifier:
                break;
            case MapSelectionMode.CarGenerator:
                break;
            case MapSelectionMode.Grass:
                break;
            case MapSelectionMode.WaterQuad:
                break;
            case MapSelectionMode.Collision:
                break;
            case MapSelectionMode.NavMesh:
                break;
            case MapSelectionMode.Path:
                break;
            case MapSelectionMode.TrainTrack:
                break;
            case MapSelectionMode.MloInstance:
                break;
            case MapSelectionMode.Scenario:
                break;
            case MapSelectionMode.PopZone:
                break;
            case MapSelectionMode.Heightmap:
                break;
            case MapSelectionMode.Watermap:
                break;
            case MapSelectionMode.Audio:
                break;
            case MapSelectionMode.CalmingQuad:
                break;
            case MapSelectionMode.WaveQuad:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (SelectionMode != MapSelectionMode.Occlusion || ymap.OccludeModels == null) return;
        foreach (var om in ymap.OccludeModels)
        {
            Renderer.RenderBasePath(om);

            var hittri = om.RayIntersect(ref mray, ref hitdist);
            if (hittri == null || !(hitdist < CurMouseHit.HitDist)) continue;
            CurMouseHit.BoxOccluder = null;
            CurMouseHit.OccludeModelTri = hittri;
            CurMouseHit.HitDist = hitdist;
            CurMouseHit.CamRel = -camera.Position;
            CurMouseHit.AABB = hittri.Box;
        }
    }

    private void UpdateMouseHits<T>(List<T> waterquads) where T : BaseWaterQuad
    {
        var bbox = new BoundingBox();
        var mray = new Ray
        {
            Position = camera.MouseRay.Position + camera.Position,
            Direction = camera.MouseRay.Direction
        };


        foreach (var quad in waterquads)
        {
            var mb = new MapBox
            {
                CamRelPos = -camera.Position,
                BBMin = new Vector3(quad.minX, quad.minY, quad.z ?? 0),
                BBMax = new Vector3(quad.maxX, quad.maxY, quad.z ?? 0),
                Orientation = Quaternion.Identity,
                Scale = Vector3.One
            };
            Renderer.BoundingBoxes.Add(mb);

            bbox.Minimum = mb.BBMin;
            bbox.Maximum = mb.BBMax;

            if (!mray.Intersects(ref bbox, out float hitdist) || !(hitdist > 0) ||
                !(hitdist <= CurMouseHit.HitDist)) continue;
            var curSize = CurMouseHit.AABB.Size.X * CurMouseHit.AABB.Size.Y;
            var newSize = bbox.Size.X * bbox.Size.Y;
            if (curSize != 0 && !(newSize < curSize)) continue;
            CurMouseHit.HitDist = hitdist;
            CurMouseHit.CamRel = mb.CamRelPos;
            CurMouseHit.AABB = bbox;

            CurMouseHit.WaterQuad = quad as WaterQuad;
            CurMouseHit.WaveQuad = quad as WaterWaveQuad;
            CurMouseHit.CalmingQuad = quad as WaterCalmingQuad;
        }
    }

    private void UpdateMouseHits(List<YnvFile> ynvs)
    {
        if (SelectionMode != MapSelectionMode.NavMesh) return;

        var mray = new Ray
        {
            Position = camera.MouseRay.Position + camera.Position,
            Direction = camera.MouseRay.Direction
        };

        foreach (var ynv in ynvs)
        {
            if (renderpathbounds)
            {
                if (ynv.Nav?.SectorTree == null) continue;

                var mb = new MapBox
                {
                    CamRelPos = -camera.Position,
                    BBMin = ynv.Nav.SectorTree.AABBMin.XYZ(),
                    BBMax = ynv.Nav.SectorTree.AABBMax.XYZ(),
                    Orientation = Quaternion.Identity,
                    Scale = Vector3.One
                };
                Renderer.BoundingBoxes.Add(mb);
            }

            if (ynv.BVH != null) UpdateMouseHits(ynv.BVH, ref mray);
            //if ((CurMouseHit.NavPoint != null) || (CurMouseHit.NavPortal != null)) continue;
            if (ynv.Nav != null && ynv.Vertices != null && ynv.Indices != null && ynv.Polys != null)
                UpdateMouseHits(ynv, ynv.Nav.SectorTree, ynv.Nav.SectorTree, ref mray);
        }
    }

    private void UpdateMouseHits(YnvFile ynv, NavMeshSector navsector, NavMeshSector rootsec, ref Ray mray)
    {
        if (navsector == null) return;

        var bbox = new BoundingBox();
        bbox.Minimum = navsector.AABBMin.XYZ();
        bbox.Maximum = navsector.AABBMax.XYZ();

        if (rootsec != null) //apparently the Z values are incorrect :(
        {
            bbox.Minimum.Z = rootsec.AABBMin.Z;
            bbox.Maximum.Z = rootsec.AABBMax.Z;
        }

        float fhd;
        if (!mray.Intersects(ref bbox, out fhd)) return; //ray intersects this node... check children for hits!
        if (navsector.SubTree1 != null) UpdateMouseHits(ynv, navsector.SubTree1, rootsec, ref mray);
        if (navsector.SubTree2 != null) UpdateMouseHits(ynv, navsector.SubTree2, rootsec, ref mray);
        if (navsector.SubTree3 != null) UpdateMouseHits(ynv, navsector.SubTree3, rootsec, ref mray);
        if (navsector.SubTree4 != null) UpdateMouseHits(ynv, navsector.SubTree4, rootsec, ref mray);
        if (navsector.Data is not { PolyIDs: not null }) return;
        var cbox = new BoundingBox
        {
            Minimum = bbox.Minimum - camera.Position,
            Maximum = bbox.Maximum - camera.Position
        };

        var polys = ynv.Polys;
        var polyids = navsector.Data.PolyIDs;
        foreach (var polyid in polyids)
        {
            if (polyid >= polys.Count) continue;

            var poly = polys[polyid];
            var ic = poly._RawData.IndexCount;
            var startid = poly._RawData.IndexID;
            var endid = startid + ic;
            if (startid >= ynv.Indices.Count) continue;
            if (endid > ynv.Indices.Count) continue;

            var vc = ynv.Vertices.Count;
            var startind = ynv.Indices[startid];
            if (startind >= vc) continue;

            var p0 = ynv.Vertices[startind];

            //test triangles for the poly.
            var tricount = ic - 2;
            for (var t = 0; t < tricount; t++)
            {
                var tid = startid + t;
                int ind1 = ynv.Indices[tid + 1];
                int ind2 = ynv.Indices[tid + 2];
                if (ind1 >= vc || ind2 >= vc) continue;

                var p1 = ynv.Vertices[ind1];
                var p2 = ynv.Vertices[ind2];

                if (!mray.Intersects(ref p0, ref p1, ref p2, out float hitdist) || !(hitdist < CurMouseHit.HitDist) ||
                    !(hitdist > 0)) continue;
                var cellaabb = poly._RawData.CellAABB;
                CurMouseHit.NavPoly = poly;
                CurMouseHit.NavPoint = null;
                CurMouseHit.NavPortal = null;
                CurMouseHit.HitDist = hitdist;
                CurMouseHit.AABB = new BoundingBox(cellaabb.Min, cellaabb.Max);
                break; //no need to test further tris in this poly
            }
        }
    }

    private void UpdateMouseHits(List<YndFile> ynds)
    {
        if (SelectionMode != MapSelectionMode.Path) return;

        var mray = new Ray
        {
            Position = camera.MouseRay.Position + camera.Position,
            Direction = camera.MouseRay.Direction
        };

        foreach (var ynd in ynds)
        {
            if (renderpathbounds)
            {
                var minz = ynd.BVH != null ? ynd.BVH.Box.Minimum.Z : 0.0f;
                var maxz = ynd.BVH != null ? ynd.BVH.Box.Maximum.Z : 0.0f;
                var mb = new MapBox
                {
                    CamRelPos = -camera.Position,
                    BBMin = new Vector3(ynd.BBMin.X, ynd.BBMin.Y, minz),
                    BBMax = new Vector3(ynd.BBMax.X, ynd.BBMax.Y, maxz),
                    Orientation = Quaternion.Identity,
                    Scale = Vector3.One
                };
                Renderer.BoundingBoxes.Add(mb);
            }

            if (ynd.BVH != null) UpdateMouseHits(ynd.BVH, ref mray);
        }


        if (SelectedItem.PathNode == null) return;
        {
            var linkrad = 0.25f;

            var n = SelectedItem.PathNode;
            if (n.Links == null) return;
            foreach (var ln in n.Links)
            {
                if (ln.Node2 == null) continue; //invalid links can hit here...
                var dv = n.Position - ln.Node2.Position;
                var dl = dv.Length();
                var dir = dv * (1.0f / dl);
                var dup = Vector3.UnitZ;
                var mb = new MapBox();

                var lanestot = ln.LaneCountForward + ln.LaneCountBackward;
                var lanewidth = ln.GetLaneWidth();
                var inner = ln.LaneOffset * lanewidth; // 0.0f;
                var outer = inner + Math.Max(lanewidth * ln.LaneCountForward, 0.5f);
                var totwidth = lanestot * lanewidth;
                var halfwidth = totwidth * 0.5f;
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
                    Renderer.HilightBoxes.Add(mb);
                else
                    Renderer.BoundingBoxes.Add(mb);
            }
        }
    }

    private void UpdateMouseHits(List<TrainTrack> tracks)
    {
        if (SelectionMode != MapSelectionMode.TrainTrack) return;

        var mray = new Ray
        {
            Position = camera.MouseRay.Position + camera.Position,
            Direction = camera.MouseRay.Direction
        };

        foreach (var track in tracks.Where(track => track.BVH != null)) UpdateMouseHits(track.BVH, ref mray);


        if (SelectedItem.TrainTrackNode == null) return;
        var linkrad = 0.25f;
        var n = SelectedItem.TrainTrackNode;
        if (n.Links == null) return;
        foreach (var ln in n.Links)
        {
            if (ln == null) continue;
            var dv = n.Position - ln.Position;
            var dl = dv.Length();
            var dir = dv * (1.0f / dl);
            var dup = Vector3.UnitZ;
            var mb = new MapBox
            {
                CamRelPos = n.Position - camera.Position,
                BBMin = new Vector3(-linkrad, -linkrad, 0.0f),
                BBMax = new Vector3(linkrad, linkrad, dl),
                Orientation = Quaternion.Invert(Quaternion.RotationLookAtRH(dir, dup)),
                Scale = Vector3.One
            };
            Renderer.BoundingBoxes.Add(mb);
        }
    }

    private void UpdateMouseHits(List<YmtFile> scenarios)
    {
        if (SelectionMode != MapSelectionMode.Scenario) return;

        var mray = new Ray
        {
            Position = camera.MouseRay.Position + camera.Position,
            Direction = camera.MouseRay.Direction
        };

        foreach (var sr in scenarios.Select(scenario => scenario.ScenarioRegion).Where(sr => sr != null))
        {
            if (renderscenariobounds)
            {
                var mb = new MapBox
                {
                    CamRelPos = -camera.Position,
                    BBMin = sr?.BVH?.Box.Minimum ?? Vector3.Zero,
                    BBMax = sr?.BVH?.Box.Maximum ?? Vector3.Zero,
                    Orientation = Quaternion.Identity,
                    Scale = Vector3.One
                };
                Renderer.BoundingBoxes.Add(mb);
            }

            if (sr.BVH != null) UpdateMouseHits(sr.BVH, ref mray);
        }


        if (SelectedItem.ScenarioNode == null) return; //move this stuff to renderselection..?
        {
            var n = SelectedItem.ScenarioNode;
            var nc = n.ChainingNode?.Chain;
            var ncl = n.Cluster;

            var sr = SelectedItem.ScenarioNode.Ymt.ScenarioRegion;
            {
                var mb = new MapBox
                {
                    CamRelPos = -camera.Position,
                    BBMin = sr?.BVH?.Box.Minimum ?? Vector3.Zero,
                    BBMax = sr?.BVH?.Box.Maximum ?? Vector3.Zero,
                    Orientation = Quaternion.Identity,
                    Scale = Vector3.One
                };
                if (renderscenariobounds)
                    Renderer.HilightBoxes.Add(mb);
                else
                    Renderer.BoundingBoxes.Add(mb);
            }


            if (ncl == null) return;
            {
                //hilight the cluster itself
                var mb = new MapBox
                {
                    Scale = Vector3.One,
                    BBMin = new Vector3(-0.5f),
                    BBMax = new Vector3(0.5f),
                    CamRelPos = ncl.Position - camera.Position,
                    Orientation = Quaternion.Identity
                };
                Renderer.HilightBoxes.Add(mb);


                //show boxes for points in the cluster
                if (ncl.Points is not { MyPoints: not null }) return;
                foreach (var clpoint in ncl.Points.MyPoints)
                {
                    if (clpoint == n.ClusterMyPoint) continue; //don't highlight the selected node...
                    mb = new MapBox
                    {
                        Scale = Vector3.One,
                        BBMin = new Vector3(-0.5f),
                        BBMax = new Vector3(0.5f),
                        CamRelPos = clpoint.Position - camera.Position,
                        Orientation = clpoint.Orientation
                    };
                    Renderer.BoundingBoxes.Add(mb);
                }
            }
        }
    }

    private void UpdateMouseHits(PathBVHNode pathbvhnode, ref Ray mray)
    {
        while (true)
        {
            var nrad = 0.5f;

            var bsph = new BoundingSphere { Radius = nrad };

            var bbox = new BoundingBox
                { Minimum = pathbvhnode.Box.Minimum - nrad, Maximum = pathbvhnode.Box.Maximum + nrad };

            var nbox = new BoundingBox
            {
                Minimum = new Vector3(-nrad),
                Maximum = new Vector3(nrad)
            };

            float fhd;
            if (mray.Intersects(ref bbox, out fhd)) //ray intersects this node... check children for hits!
            {
                if (pathbvhnode.Node1 != null && pathbvhnode.Node2 != null) //node is split. recurse
                {
                    UpdateMouseHits(pathbvhnode.Node1, ref mray);
                    pathbvhnode = pathbvhnode.Node2;
                    continue;
                }

                if (pathbvhnode.Nodes != null) //leaf node. test contaned pathnodes
                    foreach (var n in pathbvhnode.Nodes)
                    {
                        bsph.Center = n.Position;
                        if (!mray.Intersects(ref bsph, out float hitdist) || !(hitdist < CurMouseHit.HitDist) ||
                            !(hitdist > 0)) continue;
                        CurMouseHit.PathNode = n as YndNode;
                        CurMouseHit.TrainTrackNode = n as TrainTrackNode;
                        CurMouseHit.ScenarioNode = n as ScenarioNode;
                        CurMouseHit.LodLight = n as YmapLODLight;
                        CurMouseHit.NavPoint = n as YnvPoint;
                        CurMouseHit.NavPortal = n as YnvPortal;
                        CurMouseHit.NavPoly = null;
                        CurMouseHit.HitDist = hitdist;
                        CurMouseHit.CamRel = n.Position - camera.Position;
                        CurMouseHit.AABB = nbox;
                    }
            }

            break;
        }
    }

    public void SelectObject(object obj, object parent = null, bool addSelection = false)
    {
        switch (obj)
        {
            case null:
                SelectItem(null, addSelection);
                return;
            case object[] arr:
            {
                SelectItem(null, addSelection);
                foreach (var mobj in arr) SelectObject(mobj, null, true);
                if (!addSelection) UpdateSelectionUI(true);
                if (ProjectForm != null && !addSelection) ProjectForm.OnWorldSelectionChanged(SelectedItem);
                break;
            }
            default:
            {
                var ms = MapSelection.FromProjectObject(this, obj, parent);
                if (!ms.HasValue)
                    SelectItem(null, addSelection);
                else
                    SelectItem(ms, addSelection);
                break;
            }
        }
    }

    public void SelectItem(MapSelection? mhit = null, bool addSelection = false, bool manualSelection = false,
        bool notifyProject = true)
    {
        var mhitv = mhit ?? new MapSelection();
        if (mhit != null)
        {
            if (mhitv.Archetype == null && mhitv.EntityDef != null)
                mhitv.Archetype = mhitv.EntityDef.Archetype; //use the entity archetype if no archetype given
            if (mhitv.GrassBatch != null) mhitv.Archetype = mhitv.GrassBatch.Archetype;
        }

        if (mhitv is { Archetype: not null, Drawable: null })
            mhitv.Drawable =
                GameFileCache.TryGetDrawable(mhitv
                    .Archetype); //no drawable given.. try to get it from the cache.. if it's not there, drawable info won't display...

        var oldnode = SelectedItem.PathNode;
        var change = false;
        change = mhit != null ? SelectedItem.CheckForChanges(mhitv) : SelectedItem.CheckForChanges();

        if (addSelection)
        {
            var items = new List<MapSelection>();
            if (SelectedItem.MultipleSelectionItems != null)
            {
                items.AddRange(SelectedItem.MultipleSelectionItems);

                if (mhitv.HasValue) //incoming selection isn't empty...
                {
                    //search the list for a match, remove it if already there, otherwise add it.
                    var found = false;
                    foreach (var item in items.Where(item => !item.CheckForChanges(mhitv)))
                    {
                        items.Remove(item);
                        found = true;
                        break;
                    }

                    if (found)
                    {
                        switch (items.Count)
                        {
                            case 1:
                                mhitv = items[0];
                                items.Clear();
                                break;
                            case <= 0:
                                mhitv.Clear();
                                items.Clear(); //this shouldn't really happen..
                                break;
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
            if (!mhit.HasValue) return;
            //make sure the path link gets changed (sub-selection!)
            //lock (Renderer.RenderSyncRoot)
            SelectedItem.PathLink = mhitv.PathLink;
            SelectedItem.ScenarioEdge = mhitv.ScenarioEdge;

            return;
        }

        lock (Renderer.RenderSyncRoot) //drawflags is used when rendering.. need that lock
        {
            if (mhit.HasValue)
                SelectedItem = mhitv;
            else
                SelectedItem.Clear();

            if (!addSelection) UpdateSelectionUI(true);

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

        if (notifyProject && ProjectForm != null && (!addSelection || manualSelection))
            ProjectForm.OnWorldSelectionChanged(SelectedItem);

        var newnode = SelectedItem.PathNode;
        if (newnode != oldnode) //this is to allow junction heightmaps to be displayed when selecting a junction node
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
            foreach (var item in items) SelectItem(item, true, false, notifyProject);
            if (!addSelection) UpdateSelectionUI(true);
            if (notifyProject && ProjectForm != null && !addSelection)
                ProjectForm.OnWorldSelectionChanged(SelectedItem);
        }
    }

    private void SelectMousedItem()
    {
        //when clicked, select the currently moused item and update the selection info UI

        if (!MouseSelectEnabled) return;

        SelectItem(LastMouseHit, Input.CtrlPressed, true);
    }

    private void UpdateSelectionUI(bool wait)
    {
        try
        {
            if (InvokeRequired)
            {
                if (wait)
                    Invoke(() => { UpdateSelectionUI(wait); });
                else
                    BeginInvoke(() => { UpdateSelectionUI(wait); });
            }
            else
            {
                SetSelectionUI(SelectedItem);

                if (InfoForm != null) InfoForm.SetSelection(SelectedItem);
            }
        }
        catch
        {
        }
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


        YmapFile ymap = null;
        YnvFile ynv = null;
        YndFile ynd = null;
        TrainTrack traintr = null;
        YmtFile scenario = null;
        RelFile audiofile = null;
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


        if (ymap != null) EnableYmapUI(true, ymap.Name);
        if (ynd != null) EnableYndUI(true, ynd.Name);
        if (ynv != null) EnableYnvUI(true, ynv.Name);
        if (traintr != null) EnableTrainsUI(true, traintr.Name);
        if (scenario != null) EnableScenarioUI(true, scenario.Name);
        if (audiofile != null) EnableAudioUI(true, audiofile.Name);
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
                SelectionTabControl.TabPages.Remove(SelectionExtensionTabPage);
        }
    }

    private void AddSelectionDrawableModelsTreeNodes(DrawableModel[] models, string prefix, bool check)
    {
        if (models == null) return;

        for (var mi = 0; mi < models.Length; mi++)
        {
            var model = models[mi];
            var mprefix = prefix + " " + (mi + 1);
            var mnode = SelDrawableModelsTreeView.Nodes.Add(mprefix + " " + model);
            mnode.Tag = model;
            mnode.Checked = check;

            var tmnode = SelDrawableTexturesTreeView.Nodes.Add(mprefix + " " + model);
            tmnode.Tag = model;

            if (!check) Renderer.SelectionModelDrawFlags[model] = false;

            if (model.Geometries == null) continue;

            foreach (var geom in model.Geometries)
            {
                var gname = geom.ToString();
                var gnode = mnode.Nodes.Add(gname);
                gnode.Tag = geom;
                gnode.Checked = true; // check;

                var tgnode = tmnode.Nodes.Add(gname);
                tgnode.Tag = geom;

                if (geom.Shader != null && geom.Shader.ParametersList != null &&
                    geom.Shader.ParametersList.Hashes != null)
                {
                    var pl = geom.Shader.ParametersList;
                    var h = pl.Hashes;
                    var p = pl.Parameters;
                    for (var ip = 0; ip < h.Length; ip++)
                    {
                        var hash = pl.Hashes[ip];
                        var parm = pl.Parameters[ip];
                        var tex = parm.Data as TextureBase;
                        if (tex != null)
                        {
                            var t = tex as Texture;
                            var tstr = tex.Name.Trim();
                            if (t != null) tstr = string.Format("{0} ({1}x{2}, embedded)", tex.Name, t.Width, t.Height);
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
        var rem = node.Checked;

        Renderer.UpdateSelectionDrawFlags(model, geom, rem);
    }

    public void SyncSelDrawableModelsTreeNode(TreeNode node)
    {
        //called by the info form when a selection treeview node is checked/unchecked.
        foreach (TreeNode mnode in SelDrawableModelsTreeView.Nodes)
        {
            if (mnode.Tag == node.Tag)
                if (mnode.Checked != node.Checked)
                    mnode.Checked = node.Checked;
            foreach (TreeNode gnode in mnode.Nodes)
                if (gnode.Tag == node.Tag)
                    if (gnode.Checked != node.Checked)
                        gnode.Checked = node.Checked;
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
            if (InfoForm.WindowState == FormWindowState.Minimized) InfoForm.WindowState = FormWindowState.Normal;
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
            ProjectForm.OnWorldSelectionChanged(
                SelectedItem); // so that the project form isn't stuck on the welcome window.
        }
        else
        {
            if (ProjectForm.WindowState == FormWindowState.Minimized) ProjectForm.WindowState = FormWindowState.Normal;
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
            if (SearchForm.WindowState == FormWindowState.Minimized) SearchForm.WindowState = FormWindowState.Normal;
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
                CutsceneForm.WindowState = FormWindowState.Normal;
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

    public void GoToEntity(YmapEntityDef entity)
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
            timecycle.Init(GameFileCache, UpdateStatus);
            timecycle.SetTime(Renderer.timeofday);
#if !DEBUG
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading timecycles: {ex.Message}", "Error", MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        try
        {
#endif
            UpdateStatus("Loading materials...");
            BoundsMaterialTypes.Init(GameFileCache);
#if !DEBUG
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading materials: {ex.Message}", "Error", MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        try
        {
#endif
            UpdateStatus("Loading weather...");
            weather.Init(GameFileCache, UpdateStatus, timecycle);
            UpdateWeatherTypesComboBox(weather);
#if !DEBUG
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error loading weather files, ensure you do not have FiveMods installed or any Redux mod. Game may require reinstall.: {ex.Message}",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        try
        {
#endif
            UpdateStatus("Loading clouds...");
            clouds.Init(GameFileCache, UpdateStatus, weather);
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
            water.Init(GameFileCache, UpdateStatus);
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
            trains.Init(GameFileCache, UpdateStatus);
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
            scenarios.Init(GameFileCache, UpdateStatus, timecycle);
#if !DEBUG
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading scenarios: {ex.Message}", "Error", MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        try
        {
#endif
            UpdateStatus("Loading popzones...");
            popzones.Init(GameFileCache, UpdateStatus);
#if !DEBUG
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading popzones: {ex.Message}", "Error", MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        try
        {
#endif
            UpdateStatus("Loading heightmaps...");
            heightmaps.Init(GameFileCache, UpdateStatus);
#if !DEBUG
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading heightmaps: {ex.Message}", "Error", MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        try
        {
#endif
            UpdateStatus("Loading watermaps...");
            watermaps.Init(GameFileCache, UpdateStatus);
#if !DEBUG
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading watermaps: {ex.Message}", "Error", MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        try
        {
#endif
            UpdateStatus("Loading audio zones...");
            audiozones.Init(GameFileCache, UpdateStatus);
#if !DEBUG
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading audio zones: {ex.Message}", "Error", MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        try
        {
#endif
            UpdateStatus("Loading world...");
            Space.Init(GameFileCache, UpdateStatus);
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
            lock (Renderer.RenderSyncRoot)
            {
                if (GameFileCache.SetDlcLevel(dlc, enable)) LoadWorld();
            }

            Invoke(() => { Cursor = Cursors.Default; });
        });
    }

    private void SetModsEnabled(bool enable)
    {
        if (!initialised) return;
        Cursor = Cursors.WaitCursor;
        Task.Run(() =>
        {
            lock (Renderer.RenderSyncRoot)
            {
                if (GameFileCache.SetModsEnabled(enable))
                {
                    UpdateDlcListComboBox(GameFileCache.DlcNameList);

                    LoadWorld();
                }
            }

            Invoke(() => { Cursor = Cursors.Default; });
        });
    }


    private void ContentThread()
    {
        //main content loading thread.
        running = true;

        UpdateStatus("Scanning...");

        try
        {
            GTA5Keys.LoadFromPath(GTAFolder.CurrentGTAFolder, Settings.Default.Key);

            //save the key for later if it's not saved already. not really ideal to have this in this thread
            if (string.IsNullOrEmpty(Settings.Default.Key) && GTA5Keys.PC_AES_KEY != null)
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

        GameFileCache.Init(UpdateStatus, LogError);

        UpdateDlcListComboBox(GameFileCache.DlcNameList);

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


        Task.Run(() =>
        {
            while (formopen && !IsDisposed) //renderer content loop
            {
#if !DEBUG
                try
                {
#endif
                    var rcItemsPending = Renderer.ContentThreadProc();
                    if (!rcItemsPending) Thread.Sleep(1); //sleep if there's nothing to do
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
                var fcItemsPending = GameFileCache.ContentThreadProc();
                if (!fcItemsPending) Thread.Sleep(1); //sleep if there's nothing to do
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

        GameFileCache.Clear();

        running = false;
    }


    private void UpdateStatus(string text)
    {
        try
        {
            if (InvokeRequired)
                BeginInvoke(() => { UpdateStatus(text); });
            else
                StatusLabel.Text = text;
        }
        catch
        {
        }
    }

    private void UpdateMousedLabel(string text)
    {
        try
        {
            if (InvokeRequired)
                BeginInvoke(() => { UpdateMousedLabel(text); });
            else
                MousedLabel.Text = text;
        }
        catch
        {
        }
    }

    private void UpdateWeatherTypesComboBox(Weather weather)
    {
        try
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => { UpdateWeatherTypesComboBox(weather); });
            }
            else
            {
                //MessageBox.Show("sky_hdr: " + weather.GetDynamicValue("sky_hdr").ToString() + "\n" +
                //                "Timecycle index: " + weather.Timecycle.CurrentSampleIndex + "\n" +
                //                "Timecycle blend: " + weather.Timecycle.CurrentSampleBlend + "\n");

                WeatherComboBox.Items.Clear();
                foreach (var wt in weather.WeatherTypes.Keys) WeatherComboBox.Items.Add(wt);
                WeatherComboBox.SelectedIndex = Math.Max(WeatherComboBox.FindString(Settings.Default.Weather), 0);
                WeatherRegionComboBox.SelectedIndex =
                    Math.Max(WeatherRegionComboBox.FindString(Settings.Default.Region), 0);
            }
        }
        catch
        {
        }
    }

    private void UpdateCloudTypesComboBox(Clouds clouds)
    {
        try
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => { UpdateCloudTypesComboBox(clouds); });
            }
            else
            {
                CloudsComboBox.Items.Clear();
                foreach (var frag in clouds.HatManager.CloudHatFrags) CloudsComboBox.Items.Add(frag.Name);
                CloudsComboBox.SelectedIndex = Math.Max(CloudsComboBox.FindString(Renderer.individualcloudfrag), 0);


                CloudParamComboBox.Items.Clear();
                foreach (var setting in clouds.AnimSettings.Values) CloudParamComboBox.Items.Add(setting);
                CloudParamComboBox.SelectedIndex = 0;
            }
        }
        catch
        {
        }
    }

    private void UpdateDlcListComboBox(List<string> dlcnames)
    {
        try
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => { UpdateDlcListComboBox(dlcnames); });
            }
            else
            {
                DlcLevelComboBox.Items.Clear();
                foreach (var dlcname in dlcnames) DlcLevelComboBox.Items.Add(dlcname);
                if (string.IsNullOrEmpty(GameFileCache.SelectedDlc))
                {
                    DlcLevelComboBox.SelectedIndex = dlcnames.Count - 1;
                }
                else
                {
                    var idx = DlcLevelComboBox.FindString(GameFileCache.SelectedDlc);
                    DlcLevelComboBox.SelectedIndex = idx > 0 ? idx : dlcnames.Count - 1;
                }
            }
        }
        catch
        {
        }
    }

    private void LogError(string text)
    {
        try
        {
            if (InvokeRequired)
                Invoke(() => { LogError(text); });
            else
                ConsoleTextBox.AppendText(text + "\r\n");
            //MessageBox.Show(text);
        }
        catch
        {
        }
    }


    private void UpdateMarkerSelectionPanelInvoke()
    {
        try
        {
            if (InvokeRequired)
                BeginInvoke(() => { UpdateMarkerSelectionPanel(); });
            else
                UpdateMarkerSelectionPanel();
        }
        catch
        {
        }
    }

    private void UpdateMarkerSelectionPanel()
    {
        if (!SelectedMarkerPanel.Visible) return;
        if (SelectedMarker == null)
        {
            SelectedMarkerPanel.Visible = false;
            return;
        }

        var ox = -90; //screen offset from actual marker world pos
        var oy = -76;

        var spx = (SelectedMarker.ScreenPos.X * 0.5f + 0.5f) * camera.Width;
        var spy = (SelectedMarker.ScreenPos.Y * -0.5f + 0.5f) * camera.Height;

        var px = (int)Math.Round(spx, MidpointRounding.AwayFromZero) + ox;
        var py = (int)Math.Round(spy, MidpointRounding.AwayFromZero) + oy;

        var sx = SelectedMarkerPanel.Width;
        var sy = SelectedMarkerPanel.Height;

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
        lock (markersortedsyncroot)
        {
            float mx = MouseLastPoint.X;
            float my = MouseLastPoint.Y;

            if (ShowLocatorCheckBox.Checked)
                if (IsMarkerUnderPoint(LocatorMarker, mx, my))
                    return LocatorMarker;

            //search backwards through the render markers (front to back)
            for (var i = SortedMarkers.Count - 1; i >= 0; i--)
            {
                var m = SortedMarkers[i];
                if (IsMarkerUnderPoint(m, mx, my)) return m;
            }
        }

        return null;
    }

    private bool IsMarkerUnderPoint(MapMarker marker, float x, float y)
    {
        if (marker.ScreenPos.Z <= 0.0f) return false; //behind the camera...
        var dx = x - (marker.ScreenPos.X * 0.5f + 0.5f) * camera.Width;
        var dy = y - (marker.ScreenPos.Y * -0.5f + 0.5f) * camera.Height;
        var mcx = marker.Icon.Center.X;
        var mcy = marker.Icon.Center.Y;
        var bx = dx >= -mcx && dx <= mcx;
        var by = dy <= 0.0f && dy >= -mcy;
        return bx && by;
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
        var str = pos.X + ", " + pos.Y + ", " + pos.Z;
        if (!string.IsNullOrEmpty(name)) str += ", " + name;
        if (addtotxtbox)
        {
            var sb = new StringBuilder();
            sb.Append(MultiFindTextBox.Text);
            if (sb.Length > 0 && !MultiFindTextBox.Text.EndsWith("\n")) sb.AppendLine();
            sb.AppendLine(str);
            MultiFindTextBox.Text = sb.ToString();
        }

        return AddMarker(str);
    }

    private MapMarker AddMarker(string markerstr)
    {
        lock (markersyncroot)
        {
            var m = new MapMarker();
            m.Parse(markerstr.Trim());
            m.Icon = MarkerIcon;

            Markers.Add(m);

            //ListViewItem lvi = new ListViewItem(new string[] { m.Name, m.WorldPos.X.ToString(), m.WorldPos.Y.ToString(), m.WorldPos.Z.ToString() });
            //lvi.Tag = m;
            //MarkersListView.Items.Add(lvi);

            return m;
        }
    }

    private void AddDefaultMarkers()
    {
        var sb = new StringBuilder();
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
        var lines = MultiFindTextBox.Text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines) AddMarker(line);
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
        TextureCoordsComboBox.SelectedIndex =
            Math.Max(TextureCoordsComboBox.FindString(s.RenderTextureSamplerCoord), 0);
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
        GameFileCache.SelectedDlc = s.DLC;
        EnableDlcCheckBox.Checked = !string.IsNullOrEmpty(s.DLC);
    }

    private void SaveSettings()
    {
        var s = Settings.Default;
        s.WindowMaximized = WindowState == FormWindowState.Maximized;
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
        if (s.SavePosition) s.StartPosition = FloatUtil.GetVector3String(camEntity?.Position ?? camera.Position);
        if (s.SaveTimeOfDay)
        {
            s.TimeOfDay = TimeOfDayTrackBar.Value;
            s.Weather = WeatherComboBox.Text;
            s.Region = WeatherRegionComboBox.Text;
            s.Clouds = CloudsComboBox.Text;
        }

        //additional settings from gamefilecache...
        s.EnableMods = GameFileCache.EnableMods;
        s.DLC = GameFileCache.EnableDlc ? GameFileCache.SelectedDlc : "";

        s.Save();
    }

    private void ResetSettings()
    {
        if (MessageBox.Show("Are you sure you want to reset all settings to their default values?",
                "Reset All Settings", MessageBoxButtons.YesNo) != DialogResult.Yes) return;

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
                SettingsForm.WindowState = FormWindowState.Normal;
            SettingsForm.Focus();
        }

        if (!string.IsNullOrEmpty(tab)) SettingsForm.SelectTab(tab);
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
        var tw = Widget;
        UndoStep s = null;
        if (tw != null)
            s = SelectedItem.CreateUndoStep(tw.Mode, UndoStartPosition, UndoStartRotation, UndoStartScale, this,
                EditEntityPivot);
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

        if (ProjectForm != null) ProjectForm.OnWorldSelectionModified(SelectedItem);

        UpdateUndoUI();
    }

    private void Redo()
    {
        if (RedoSteps.Count == 0) return;
        var s = RedoSteps.Pop();
        UndoSteps.Push(s);

        s.Redo(this, ref SelectedItem);

        if (ProjectForm != null) ProjectForm.OnWorldSelectionModified(SelectedItem);

        UpdateUndoUI();
    }

    private void UpdateUndoUI()
    {
        ToolbarUndoButton.DropDownItems.Clear();
        ToolbarRedoButton.DropDownItems.Clear();
        var i = 0;
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

        ToolbarUndoButton.Enabled = UndoSteps.Count > 0;
        ToolbarRedoButton.Enabled = RedoSteps.Count > 0;
    }


    private void EnableCacheDependentUI()
    {
        try
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => { EnableCacheDependentUI(); });
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
        catch
        {
        }
    }

    private void EnableDLCModsUI()
    {
        try
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => { EnableDLCModsUI(); });
            }
            else
            {
                EnableDlcCheckBox.Enabled = true;
                EnableModsCheckBox.Enabled = true;
                DlcLevelComboBox.Enabled = true;
            }
        }
        catch
        {
        }
    }


    public void SetCurrentSaveItem(string filename)
    {
        var enable = !string.IsNullOrEmpty(filename);
        ToolbarSaveButton.ToolTipText = enable ? "Save " + filename : "Save";
        ToolbarSaveButton.Enabled = enable;
        ToolbarSaveAllButton.Enabled = enable;
    }

    public void EnableYmapUI(bool enable, string filename)
    {
        var type = "entity";
        switch (SelectionMode)
        {
            case MapSelectionMode.CarGenerator:
                type = "car generator";
                break;
        }

        ToolbarAddItemButton.ToolTipText = "Add " + type + (enable ? " to " + filename : "");
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
        var type = "node";
        switch (SelectionMode)
        {
            case MapSelectionMode.Path:
                type = "node";
                break;
        }

        if (enable) //only do something if a ynd is selected - EnableYmapUI will handle the no selection case.. 
        {
            ToolbarAddItemButton.ToolTipText = "Add " + type + (enable ? " to " + filename : "");
            ToolbarAddItemButton.Enabled = enable;
        }
    }

    public void EnableYnvUI(bool enable, string filename)
    {
        var type = "polygon";
        switch (SelectionMode)
        {
            case MapSelectionMode.NavMesh:
                type = "polygon";
                break;
        }

        if (enable) //only do something if a ynv is selected - EnableYmapUI will handle the no selection case.. 
        {
            ToolbarAddItemButton.ToolTipText = "Add " + type + (enable ? " to " + filename : "");
            ToolbarAddItemButton.Enabled = enable;
        }
    }

    public void EnableTrainsUI(bool enable, string filename)
    {
        var type = "node";
        switch (SelectionMode)
        {
            case MapSelectionMode.TrainTrack:
                type = "node";
                break;
        }

        if (enable) //only do something if a track is selected - EnableYmapUI will handle the no selection case.. 
        {
            ToolbarAddItemButton.ToolTipText = "Add " + type + (enable ? " to " + filename : "");
            ToolbarAddItemButton.Enabled = enable;
        }
    }

    public void EnableScenarioUI(bool enable, string filename)
    {
        var type = "scenario point";
        switch (SelectionMode)
        {
            case MapSelectionMode.Scenario:
                type = "scenario point";
                break;
        }

        if (enable) //only do something if a scenario is selected - EnableYmapUI will handle the no selection case.. 
        {
            ToolbarAddItemButton.ToolTipText = "Add " + type + (enable ? " to " + filename : "");
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
            ProjectForm.NewYmap();
        else
            ProjectForm.NewProject();
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
            ProjectForm.OpenFiles();
        else
            ProjectForm.OpenProject();
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
            case MapSelectionMode.Entity:
                ProjectForm.NewEntity();
                break;
            case MapSelectionMode.CarGenerator:
                ProjectForm.NewCarGen();
                break;
            case MapSelectionMode.Path:
                ProjectForm.NewPathNode();
                break;
            case MapSelectionMode.NavMesh:
                ProjectForm.NewNavPoly();
                break; //.NewNavPoint/.NewNavPortal//how to add points/portals? project window
            case MapSelectionMode.TrainTrack:
                ProjectForm.NewTrainNode();
                break;
            case MapSelectionMode.Scenario:
                ProjectForm.NewScenarioNode();
                break; //how to add different node types? project window
            case MapSelectionMode.Audio:
                ProjectForm.NewAudioAmbientZone();
                break; //.NewAudioEmitter // how to add emitters as well? project window
        }
    }

    private void CopyItem()
    {
        CopiedItem = SelectedItem;
        ToolbarPasteButton.Enabled = CopiedItem.CanCopyPaste && ProjectForm != null; //ToolbarAddItemButton.Enabled;
    }

    private void PasteItem()
    {
        if (ProjectForm != null && CopiedItem.CanCopyPaste)
            SelectObject(ProjectForm.NewObject(CopiedItem, CopiedItem.MultipleSelectionItems != null));
    }

    private void CloneItem()
    {
        if (ProjectForm != null && SelectedItem.CanCopyPaste) SelectObject(ProjectForm.NewObject(SelectedItem, true));
    }

    private void DeleteItem()
    {
        if (ProjectForm != null)
        {
            ProjectForm.DeleteObject(SelectedItem);
            SelectItem();
        }
        else
        {
            DeleteItem(SelectedItem);
            SelectItem();
        }
    }

    private void DeleteItem(MapSelection item)
    {
        if (item.MultipleSelectionItems != null)
            for (var i = 0; i < item.MultipleSelectionItems.Length; i++)
                DeleteItem(item.MultipleSelectionItems[i]);
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

    private void DeleteEntity(YmapEntityDef ent)
    {
        if (ent == null) return;

        //project not open, or entity not selected there, just remove the entity from the ymap/mlo...
        var ymap = ent.Ymap;
        var instance = ent.MloParent?.MloInstance;
        if (ymap == null)
        {
            if (instance != null)
                try
                {
                    if (!instance.DeleteEntity(ent)) SelectItem();
                }
                catch (Exception e) // various failures can happen here.
                {
                    MessageBox.Show("Unable to remove entity..." + Environment.NewLine + e.Message);
                }
        }
        else if (!ymap.RemoveEntity(ent))
        {
            MessageBox.Show("Unable to remove entity.");
        }
        else
        {
            SelectItem();
        }
    }

    private void DeleteCarGen(YmapCarGen cargen)
    {
        if (cargen == null) return;

        //project not open, or cargen not selected there, just remove the cargen from the ymap...
        var ymap = cargen.Ymap;
        if (!ymap.RemoveCarGen(cargen))
            MessageBox.Show("Unable to remove car generator.");
        else
            SelectItem();
    }

    private void DeleteLodLight(YmapLODLight lodlight)
    {
        if (lodlight == null) return;

        //project not open, or lodlight not selected there, just remove the lodlight from the ymap...
        var ymap = lodlight.Ymap;
        if (!ymap.RemoveLodLight(lodlight))
            MessageBox.Show("Unable to remove LOD light.");
        else
            SelectItem();
    }

    private void DeleteBoxOccluder(YmapBoxOccluder box)
    {
        if (box == null) return;

        //project not open, or box not selected there, just remove the box from the ymap...
        var ymap = box.Ymap;
        if (!ymap.RemoveBoxOccluder(box))
            MessageBox.Show("Unable to remove box occluder.");
        else
            SelectItem();
    }

    private void DeleteOccludeModelTriangle(YmapOccludeModelTriangle tri)
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
            SelectItem();
        }
    }

    private void DeletePathNode(YndNode pathnode)
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


            SelectItem();
        }
    }

    private void DeleteNavPoly(YnvPoly navpoly)
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
            SelectItem();
        }
    }

    private void DeleteNavPoint(YnvPoint navpoint)
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
            SelectItem();
        }
    }

    private void DeleteNavPortal(YnvPortal navportal)
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
            SelectItem();
        }
    }

    private void DeleteTrainNode(TrainTrackNode trainnode)
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
            SelectItem();
        }
    }

    private void DeleteScenarioNode(ScenarioNode scenariopt)
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
            SelectItem();
        }
    }

    private void DeleteAudioAmbientZone(AudioPlacement audio)
    {
        if (audio == null) return;

        //project not open, or zone not selected there, just remove the zone from the rel...
        var rel = audio.RelFile;
        if (!rel.RemoveRelData(audio.AmbientZone))
            MessageBox.Show("Unable to remove audio ambient zone.");
        else
            SelectItem();
    }

    private void DeleteAudioAmbientRule(AudioPlacement audio)
    {
        if (audio == null) return;

        //project not open, or rule not selected there, just remove the rule from the rel...
        var rel = audio.RelFile;
        if (!rel.RemoveRelData(audio.AmbientRule))
            MessageBox.Show("Unable to remove audio ambient rule.");
        else
            SelectItem();
    }

    private void DeleteAudioStaticEmitter(AudioPlacement audio)
    {
        if (audio == null) return;

        //project not open, or emitter not selected there, just remove the emitter from the rel...
        var rel = audio.RelFile;
        if (!rel.RemoveRelData(audio.StaticEmitter))
            MessageBox.Show("Unable to remove audio static emitter.");
        else
            SelectItem();
    }

    private void DeleteCollisionVertex(BoundVertex vertex)
    {
        if (vertex == null) return;

        //project not open, or vertex not selected there, just remove the vertex from the geometry...
        var bgeom = vertex.Owner;
        if (bgeom == null || !bgeom.DeleteVertex(vertex.Index))
        {
            MessageBox.Show("Unable to remove vertex.");
        }
        else
        {
            UpdateCollisionBoundsGraphics(bgeom);
            SelectItem();
        }
    }

    private void DeleteCollisionPoly(BoundPolygon poly)
    {
        if (poly == null) return;

        //project not open, or polygon not selected there, just remove the vertex from the geometry...
        var bgeom = poly.Owner;
        if (bgeom == null || !bgeom.DeletePolygon(poly))
        {
            MessageBox.Show("Unable to remove polygon.");
        }
        else
        {
            UpdateCollisionBoundsGraphics(bgeom);
            SelectItem();
        }
    }

    private void DeleteCollisionBounds(Bounds bounds)
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

        SelectItem();
    }


    private void SetMouseSelect(bool enable)
    {
        MouseSelectEnabled = enable;
        MouseSelectCheckBox.Checked = enable;
        ToolbarSelectButton.Checked = enable;

        if (InfoForm != null) InfoForm.SetSelectionMode(SelectionModeStr, MouseSelectEnabled);
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
            if (childi != null) childi.Checked = false;
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

    private void SetBoundsMode(string modestr)
    {
        var mode = BoundsShaderMode.None;
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
            if (childi != null) childi.Checked = false;
        }

        var mode = MapSelectionMode.Entity;
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

        if (SelectionModeComboBox.Text != modestr) SelectionModeComboBox.Text = modestr;

        if (InfoForm != null) InfoForm.SetSelectionMode(modestr, MouseSelectEnabled);
    }


    private void SetSnapMode(WorldSnapMode mode)
    {
        foreach (var child in ToolbarSnapButton.DropDownItems)
        {
            var childi = child as ToolStripMenuItem;
            if (childi != null) childi.Checked = false;
        }

        ToolbarSnapButton.Checked = mode != WorldSnapMode.None;

        ToolStripMenuItem selItem = null;

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

        if (mode != WorldSnapMode.None) SnapModePrev = mode;
        SnapMode = mode;
    }

    private void SetRotationSnapping(float degrees)
    {
        Widget.SnapAngleDegrees = degrees;

        foreach (var child in ToolbarRotationSnappingButton.DropDownItems)
        {
            var childi = child as ToolStripMenuItem;
            if (childi != null) childi.Checked = false;
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

        if (selItem != null) selItem.Checked = true;

        var cval = (float)SnapAngleUpDown.Value;
        if (cval != degrees) SnapAngleUpDown.Value = (decimal)degrees;
    }


    private void SetCameraMode(string modestr)
    {
        foreach (var child in ToolbarCameraModeButton.DropDownItems)
        {
            var childi = child as ToolStripMenuItem;
            if (childi != null) childi.Checked = false;
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


        if (CameraModeComboBox.Text != modestr) CameraModeComboBox.Text = modestr;
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
        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(() => { ShowSubtitle(text, duration); });
            }
            catch
            {
            }

            return;
        }

        SubtitleLabel.Text = text;
        SubtitleLabel.Visible = true;
        SubtitleTimer.Interval = (int)(duration * 1000.0f);
        SubtitleTimer.Enabled = true;
    }


    private void SetTimeOfDay(int minute)
    {
        var hour = minute / 60.0f;
        UpdateTimeOfDayLabel();
        lock (Renderer.RenderSyncRoot)
        {
            Renderer.SetTimeOfDay(hour);
        }
    }


    private void TryCreateNodeLink() //TODO: move this to project window
    {
        if (SelectionMode != MapSelectionMode.Path) return;

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
                MessageBox.Show("Failed to join nodes. The nodes are likely too far away!", "Join Failed.",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var copy = n1.Links.FirstOrDefault();

            link.SetForwardLanesBidirectionally(copy?.LaneCountBackward ?? 1);
            link.SetBackwardLanesBidirectionally(copy?.LaneCountForward ?? 1);
            UpdatePathYndGraphics(n1.Ynd, false);
            UpdatePathYndGraphics(n2.Ynd, false);
        }
    }

    private void TryCreateNodeShortcut() //TODO: move this to project window
    {
        if (SelectionMode != MapSelectionMode.Path) return;

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
                MessageBox.Show("Failed to join nodes. The nodes are likely too far away!", "Join Failed.",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            link.SetForwardLanesBidirectionally(1);
            link.Shortcut = true;

            if (n2.TryGetLinkForNode(n1, out var backLink)) backLink.Shortcut = true;

            UpdatePathYndGraphics(n1.Ynd, false);
            UpdatePathYndGraphics(n2.Ynd, false);
        }
    }


    private void StatsUpdateTimer_Tick(object sender, EventArgs e)
    {
        StatsLabel.Text = Renderer.GetStatusText();

        if (Renderer.timerunning)
        {
            var fv = Renderer.timeofday * 60.0f;
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
            case MouseButtons.Left:
                MouseLButtonDown = true;
                break;
            case MouseButtons.Right:
                MouseRButtonDown = true;
                break;
        }

        if (!ToolsPanelShowButton.Focused) ToolsPanelShowButton.Focus(); //make sure no textboxes etc are focused!

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
                                MessageBox.Show("You cannot clone multiple path nodes at once", "Error",
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                                GrabbedWidget.IsDragging = false;
                                GrabbedWidget = null;
                            }
                            else
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

                        if (Input.CtrlPressed) SpawnTestEntity();
                    }

                    GrabbedMarker = null;
                }
            }

            if (MouseRButtonDown) SelectMousedItem();
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
            case MouseButtons.Left:
                MouseLButtonDown = false;
                break;
            case MouseButtons.Right:
                MouseRButtonDown = false;
                break;
        }

        Input.CtrlPressed = (ModifierKeys & Keys.Control) > 0;
        Input.ShiftPressed = (ModifierKeys & Keys.Shift) > 0;

        lock (MouseControlSyncRoot)
        {
            MouseControlButtons &= ~e.Button;
        }


        if (e.Button == MouseButtons.Left)
        {
            GrabbedMarker = null;
            if (GrabbedWidget != null)
            {
                MarkUndoEnd(GrabbedWidget);
                GrabbedWidget.IsDragging = false;
                GrabbedWidget.Position =
                    SelectedItem
                        .WidgetPosition; //in case of any snapping, make sure widget is in correct position at the end
                GrabbedWidget = null;
            }

            if (e.Location == MouseDownPoint && MousedMarker == null)
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
        var dx = e.X - MouseX;
        var dy = e.Y - MouseY;

        Input.CtrlPressed = (ModifierKeys & Keys.Control) > 0;
        Input.ShiftPressed = (ModifierKeys & Keys.Shift) > 0;

        if (MouseInvert) dy = -dy;

        if (ControlMode == WorldControlMode.Free && !ControlBrushEnabled)
        {
            if (MouseLButtonDown) RotateCam(dx, dy);
            if (MouseRButtonDown)
            {
                if (Renderer.controllightdir)
                {
                    Renderer.lightdirx += dx * camera.Sensitivity;
                    Renderer.lightdiry += dy * camera.Sensitivity;
                }
                else if (Renderer.controltimeofday)
                {
                    var tod = Renderer.timeofday;
                    tod += (dx - dy) / 30.0f;
                    while (tod >= 24.0f) tod -= 24.0f;
                    while (tod < 0.0f) tod += 24.0f;
                    timecycle.SetTime(tod);
                    Renderer.timeofday = tod;

                    var fv = tod * 60.0f;
                    TimeOfDayTrackBar.Value = (int)fv;
                    UpdateTimeOfDayLabel();
                }
            }

            UpdateMousePosition(e);
        }
        else if (ControlBrushEnabled)
        {
            if (MouseRButtonDown) RotateCam(dx, dy);

            UpdateMousePosition(e);

            ControlBrushTimer++;
            if (ControlBrushTimer > (Input.ShiftPressed ? 5 : 10))
                //lock (Renderer.RenderSyncRoot)
            {
                if (ProjectForm != null && MouseLButtonDown) ProjectForm.PaintGrass(MouseRayCollision, Input);
                ControlBrushTimer = 0;
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
                    Cursor = Cursors.SizeAll;
                else
                    Cursor = Cursors.Hand;
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
        //grabbed widget will move itself in Update() when IsDragging==true
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

    private void WorldForm_MouseWheel(object sender, MouseEventArgs e)
    {
        if (e.Delta != 0)
        {
            if (ControlMode == WorldControlMode.Free || ControlBrushEnabled)
                camera.MouseZoom(e.Delta);
            else
                lock (MouseControlSyncRoot)
                {
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

        var enablemove = !iseditmode || (MouseLButtonDown && GrabbedMarker == null && GrabbedWidget == null);

        Input.KeyDown(e, enablemove);

        var k = e.KeyCode;
        var kb = Input.keyBindings;
        var ctrl = Input.CtrlPressed;
        var shift = Input.ShiftPressed;


        if (!ctrl)
        {
            if (k == kb.MoveSlowerZoomIn) camera.MouseZoom(1);
            if (k == kb.MoveFasterZoomOut) camera.MouseZoom(-1);
        }


        if (!Input.kbmoving && !Widget.IsDragging) //don't trigger further actions if camera moving or widget dragging 
        {
            if (!ctrl)
            {
                //switch widget modes and spaces.
                if (k == kb.ExitEditMode)
                {
                    if (Widget.Mode == WidgetMode.Default) ToggleWidgetSpace();
                    else SetWidgetMode("Default");
                }

                if (k == kb.EditPosition) // && !enablemove)
                {
                    if (Widget.Mode == WidgetMode.Position) ToggleWidgetSpace();
                    else SetWidgetMode("Position");
                }

                if (k == kb.EditRotation) // && !enablemove)
                {
                    if (Widget.Mode == WidgetMode.Rotation) ToggleWidgetSpace();
                    else SetWidgetMode("Rotation");
                }

                if (k == kb.EditScale) // && !enablemove)
                {
                    if (Widget.Mode == WidgetMode.Scale) ToggleWidgetSpace();
                    else SetWidgetMode("Scale");
                }

                if (k == kb.ToggleMouseSelect) SetMouseSelect(!MouseSelectEnabled);
                if (k == kb.ToggleToolbar) ToggleToolbar();
                if (k == kb.FirstPerson)
                    SetControlMode(ControlMode == WorldControlMode.Free ? WorldControlMode.Ped : WorldControlMode.Free);
                if (k == Keys.Delete) DeleteItem();
                if (SelectionMode == MapSelectionMode.Path)
                {
                    if (k == Keys.J) TryCreateNodeLink();
                    if (k == Keys.K) TryCreateNodeShortcut();
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
            if (ControlMode != WorldControlMode.Free)
                SetControlMode(WorldControlMode.Free);

        if (ControlMode != WorldControlMode.Free || ControlBrushEnabled) e.Handled = true;
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

        if (ControlMode != WorldControlMode.Free) e.Handled = true;
    }

    private void WorldForm_Deactivate(object sender, EventArgs e)
    {
        //try not to lock keyboard movement if the form loses focus.
        Input.KeyboardStop();
    }

    private void ViewModeComboBox_SelectedIndexChanged(object sender, EventArgs e)
    {
        var prevmodel = !(rendermaps || renderworld);
        var mode = (string)ViewModeComboBox.SelectedItem;
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
                    modelname = SelectionNameTextBox.Text;
                break;
        }

        if (camera == null || camera.FollowEntity == null) return;
        if (rendermaps || renderworld)
        {
            if (prevmodel) //only change location if the last mode was model mode
                camera.FollowEntity.Position = prevworldpos;
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
        Renderer.lodthreshold = 50.0f / (0.1f + DetailTrackBar.Value);
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
                MessageBox.Show("Error loading shaders!\n" + ex);
                return;
            }
        }

        pauserendering = false;
        Cursor = Cursors.Default;
    }

    private void MarkerStyleComboBox_SelectedIndexChanged(object sender, EventArgs e)
    {
        var icon = MarkerStyleComboBox.SelectedItem as MapIcon;
        if (icon != MarkerIcon)
        {
            MarkerIcon = icon;
            foreach (var m in Markers) m.Icon = icon;
        }
    }

    private void LocatorStyleComboBox_SelectedIndexChanged(object sender, EventArgs e)
    {
        var icon = LocatorStyleComboBox.SelectedItem as MapIcon;
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
        var lines = MultiFindTextBox.Text.Split('\n');
        foreach (var line in lines) AddMarker(line);
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
        if (SelectedItem.EntityDef == null) return;

        var pos = SelectedItem.EntityDef.Position;
        var name = SelectedItem.EntityDef.CEntityDef.archetypeName.ToString();
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

        var oldwidth = ToolsPanel.Width;
        if (toolspanelexpanded)
            ToolsPanelExpandButton.Text = ">>";
        else
            ToolsPanelExpandButton.Text = "<<";
        ToolsPanel.Width = toolspanellastwidth; //or extended width
        ToolsPanel.Left -= toolspanellastwidth - oldwidth;
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
            var rx = e.X + ToolsPanel.Left;
            var dx = rx - toolsPanelResizeStartX;
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
        var f = new AboutForm();
        f.Show(this);
    }

    private void ToolsButton_Click(object sender, EventArgs e)
    {
        ToolsMenu.Show(ToolsButton, 0, ToolsButton.Height);
    }

    private void ToolsMenuRPFBrowser_Click(object sender, EventArgs e)
    {
        var f = new BrowseForm();
        f.Show(this);
    }

    private void ToolsMenuRPFExplorer_Click(object sender, EventArgs e)
    {
        var f = new ExploreForm();
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
        var f = new AudioExplorerForm(GameFileCache);
        f.Show(this);
    }

    private void ToolsMenuWorldSearch_Click(object sender, EventArgs e)
    {
        ShowSearchForm();
    }

    private void ToolsMenuBinarySearch_Click(object sender, EventArgs e)
    {
        var f = new BinarySearchForm(GameFileCache);
        f.Show(this);
    }

    private void ToolsMenuJenkGen_Click(object sender, EventArgs e)
    {
        var f = new JenkGenForm();
        f.Show(this);
    }

    private void ToolsMenuJenkInd_Click(object sender, EventArgs e)
    {
        var f = new JenkIndForm(GameFileCache);
        f.Show(this);
    }

    private void ToolsMenuExtractScripts_Click(object sender, EventArgs e)
    {
        var f = new ExtractScriptsForm();
        f.Show(this);
    }

    private void ToolsMenuExtractTextures_Click(object sender, EventArgs e)
    {
        var f = new ExtractTexForm();
        f.Show(this);
    }

    private void ToolsMenuExtractRawFiles_Click(object sender, EventArgs e)
    {
        var f = new ExtractRawForm();
        f.Show(this);
    }

    private void ToolsMenuExtractShaders_Click(object sender, EventArgs e)
    {
        var f = new ExtractShadersForm();
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
            Renderer.shaders.RenderTextureSampler = (ShaderParamNames)TextureSamplerComboBox.SelectedItem;
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
        if (Renderer.controllightdir) ControlTimeOfDayCheckBox.Checked = false;
    }

    private void ControlTimeOfDayCheckBox_CheckedChanged(object sender, EventArgs e)
    {
        Renderer.controltimeofday = ControlTimeOfDayCheckBox.Checked;
        if (Renderer.controltimeofday) ControlLightDirectionCheckBox.Checked = false;
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
        var loddist = WorldLodDistTrackBar.Value * 0.1f;
        Renderer.renderworldLodDistMult = loddist;
        WorldLodDistLabel.Text = loddist.ToString("0.0");
    }

    private void WorldDetailDistTrackBar_Scroll(object sender, EventArgs e)
    {
        var detdist = WorldDetailDistTrackBar.Value * 0.1f;
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
        //if (!Monitor.TryEnter(rendersyncroot, 50))
        //{ return; } //couldn't get a lock...
        Renderer.individualcloudfrag = CloudsComboBox.Text;
        //Monitor.Exit(rendersyncroot);
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
        var tv = TimeSpeedTrackBar.Value * 0.01f;
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
        var det = MapViewDetailTrackBar.Value * 0.1f;
        MapViewDetailLabel.Text = det.ToString("0.0#");
        lock (Renderer.RenderSyncRoot)
        {
            Renderer.MapViewDetail = det;
        }
    }

    private void FieldOfViewTrackBar_Scroll(object sender, EventArgs e)
    {
        var fov = FieldOfViewTrackBar.Value * 0.01f;
        FieldOfViewLabel.Text = fov.ToString("0.0#");
        lock (Renderer.RenderSyncRoot)
        {
            camera.FieldOfView = fov;
            camera.UpdateProj = true;
        }
    }

    private void CloudParamComboBox_SelectedIndexChanged(object sender, EventArgs e)
    {
        var setting = CloudParamComboBox.SelectedItem as CloudAnimSetting;
        if (setting != null)
        {
            var rng = setting.MaxValue - setting.MinValue;
            var cval = (setting.CurrentValue - setting.MinValue) / rng;
            var ival = (int)(cval * 200.0f);
            ival = Math.Min(Math.Max(ival, 0), 200);
            CloudParamTrackBar.Value = ival;
        }
    }

    private void CloudParamTrackBar_Scroll(object sender, EventArgs e)
    {
        var setting = CloudParamComboBox.SelectedItem as CloudAnimSetting;
        if (setting != null)
        {
            var rng = setting.MaxValue - setting.MinValue;
            var fval = CloudParamTrackBar.Value / 200.0f;
            var cval = fval * rng + setting.MinValue;
            setting.CurrentValue = cval;
        }
    }

    private void SelDrawableModelsTreeView_AfterCheck(object sender, TreeViewEventArgs e)
    {
        if (e.Node != null)
        {
            UpdateSelectionDrawFlags(e.Node);

            if (InfoForm != null) InfoForm.SyncSelDrawableModelsTreeNode(e.Node);
        }
    }

    private void SelDrawableModelsTreeView_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Node != null) e.Node.Checked = !e.Node.Checked;
        //UpdateSelectionDrawFlags(e.Node);
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
            SetSnapMode(SnapModePrev);
        else
            SetSnapMode(WorldSnapMode.None);
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

    private void ToolbarUndoListButton_Click(object sender, EventArgs e)
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

    private void ToolbarRedoListButton_Click(object sender, EventArgs e)
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
        if (Widget != null) SetRotationSnapping((float)SnapAngleUpDown.Value);
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
    Jetpack = 10
}

public enum WorldSnapMode
{
    None = 0,
    Grid = 1,
    Ground = 2,
    Hybrid = 3
}