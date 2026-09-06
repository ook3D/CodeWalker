using System.Collections.Specialized;

namespace CodeWalker.Properties;

public sealed partial class Settings
{
    // Stock lighting can be used while retaining modded geometry and textures.
    public bool UseOriginalLighting
    {
        get => SettingsManager.GetBool(nameof(UseOriginalLighting), true);
        set => SettingsManager.SetBool(nameof(UseOriginalLighting), value);
    }

    private static Settings? _default;
    
    public static Settings Default
    {
        get
        {
            _default ??= new Settings();
            return _default;
        }
    }

    // Folders
    public string Key
    {
        get => SettingsManager.GetString(nameof(Key));
        set => SettingsManager.SetString(nameof(Key), value);
    }

    public string GTAFolder
    {
        get => SettingsManager.GetString(nameof(GTAFolder), @"C:\Program Files (x86)\Steam\SteamApps\common\Grand Theft Auto V");
        set => SettingsManager.SetString(nameof(GTAFolder), value);
    }

    public string GTAFolderLegacy
    {
        get => SettingsManager.GetString(nameof(GTAFolderLegacy));
        set => SettingsManager.SetString(nameof(GTAFolderLegacy), value);
    }

    public string GTAFolderEnhanced
    {
        get => SettingsManager.GetString(nameof(GTAFolderEnhanced));
        set => SettingsManager.SetString(nameof(GTAFolderEnhanced), value);
    }

    //semicolon-separated folders of unpacked FiveM map resources, loaded like a mods dlcpack
    public string FiveMResourceFolders
    {
        get => SettingsManager.GetString(nameof(FiveMResourceFolders));
        set => SettingsManager.SetString(nameof(FiveMResourceFolders), value);
    }

    public string CompiledScriptFolder
    {
        get => SettingsManager.GetString(nameof(CompiledScriptFolder));
        set => SettingsManager.SetString(nameof(CompiledScriptFolder), value);
    }

    public string DecompiledScriptFolder
    {
        get => SettingsManager.GetString(nameof(DecompiledScriptFolder));
        set => SettingsManager.SetString(nameof(DecompiledScriptFolder), value);
    }

    public string GTAExeDumpFile
    {
        get => SettingsManager.GetString(nameof(GTAExeDumpFile));
        set => SettingsManager.SetString(nameof(GTAExeDumpFile), value);
    }

    public string ExtractedTexturesFolder
    {
        get => SettingsManager.GetString(nameof(ExtractedTexturesFolder));
        set => SettingsManager.SetString(nameof(ExtractedTexturesFolder), value);
    }

    public string ExtractedRawFilesFolder
    {
        get => SettingsManager.GetString(nameof(ExtractedRawFilesFolder));
        set => SettingsManager.SetString(nameof(ExtractedRawFilesFolder), value);
    }

    public string ExtractedShadersFolder
    {
        get => SettingsManager.GetString(nameof(ExtractedShadersFolder));
        set => SettingsManager.SetString(nameof(ExtractedShadersFolder), value);
    }

    // Display Settings
    public bool FullScreen
    {
        get => SettingsManager.GetBool(nameof(FullScreen), false);
        set => SettingsManager.SetBool(nameof(FullScreen), value);
    }

    public bool Wireframe
    {
        get => SettingsManager.GetBool(nameof(Wireframe), false);
        set => SettingsManager.SetBool(nameof(Wireframe), value);
    }

    public bool Skydome
    {
        get => SettingsManager.GetBool(nameof(Skydome), true);
        set => SettingsManager.SetBool(nameof(Skydome), value);
    }

    public bool ShowTimedEntities
    {
        get => SettingsManager.GetBool(nameof(ShowTimedEntities), true);
        set => SettingsManager.SetBool(nameof(ShowTimedEntities), value);
    }

    public bool ShowCollisionMeshes
    {
        get => SettingsManager.GetBool(nameof(ShowCollisionMeshes), false);
        set => SettingsManager.SetBool(nameof(ShowCollisionMeshes), value);
    }

    public int CollisionMeshRange
    {
        get => SettingsManager.GetInt(nameof(CollisionMeshRange), 4);
        set => SettingsManager.SetInt(nameof(CollisionMeshRange), value);
    }

    public bool DynamicLOD
    {
        get => SettingsManager.GetBool(nameof(DynamicLOD), true);
        set => SettingsManager.SetBool(nameof(DynamicLOD), value);
    }

    public int DetailDist
    {
        get => SettingsManager.GetInt(nameof(DetailDist), 5);
        set => SettingsManager.SetInt(nameof(DetailDist), value);
    }

    public string MarkerStyle
    {
        get => SettingsManager.GetString(nameof(MarkerStyle), "Glokon Marker");
        set => SettingsManager.SetString(nameof(MarkerStyle), value);
    }

    public string LocatorStyle
    {
        get => SettingsManager.GetString(nameof(LocatorStyle), "Glokon Debug");
        set => SettingsManager.SetString(nameof(LocatorStyle), value);
    }

    public bool MarkerDepthClip
    {
        get => SettingsManager.GetBool(nameof(MarkerDepthClip), false);
        set => SettingsManager.SetBool(nameof(MarkerDepthClip), value);
    }

    public string BoundsStyle
    {
        get => SettingsManager.GetString(nameof(BoundsStyle), "None");
        set => SettingsManager.SetString(nameof(BoundsStyle), value);
    }

    public bool BoundsDepthClip
    {
        get => SettingsManager.GetBool(nameof(BoundsDepthClip), true);
        set => SettingsManager.SetBool(nameof(BoundsDepthClip), value);
    }

    public int BoundsRange
    {
        get => SettingsManager.GetInt(nameof(BoundsRange), 100);
        set => SettingsManager.SetInt(nameof(BoundsRange), value);
    }

    public bool ShowErrorConsole
    {
        get => SettingsManager.GetBool(nameof(ShowErrorConsole), false);
        set => SettingsManager.SetBool(nameof(ShowErrorConsole), value);
    }

    public bool Shadows
    {
        get => SettingsManager.GetBool(nameof(Shadows), true);
        set => SettingsManager.SetBool(nameof(Shadows), value);
    }

    public int ShadowCascades
    {
        get => SettingsManager.GetInt(nameof(ShadowCascades), 6);
        set => SettingsManager.SetInt(nameof(ShadowCascades), value);
    }

    public bool Grass
    {
        get => SettingsManager.GetBool(nameof(Grass), true);
        set => SettingsManager.SetBool(nameof(Grass), value);
    }

    public bool ShowStatusBar
    {
        get => SettingsManager.GetBool(nameof(ShowStatusBar), true);
        set => SettingsManager.SetBool(nameof(ShowStatusBar), value);
    }

    public bool WaitForChildren
    {
        get => SettingsManager.GetBool(nameof(WaitForChildren), true);
        set => SettingsManager.SetBool(nameof(WaitForChildren), value);
    }

    // Cache Settings
    public long CacheSize
    {
        get => SettingsManager.GetLong(nameof(CacheSize), 2147483648);
        set => SettingsManager.SetLong(nameof(CacheSize), value);
    }

    // What to load at startup. All of this is retained for the whole session, so turning a piece off is a
    // straight memory saving at the cost of the feature. Measured on a full install + FiveM maps:
    // audio ~130MB, peds+vehicles ~25MB (managed heap, before anything streams).
    public bool LoadAudioData //the .rel audio dats, used by the audio explorer and project window
    {
        get => SettingsManager.GetBool(nameof(LoadAudioData), true);
        set => SettingsManager.SetBool(nameof(LoadAudioData), value);
    }

    public bool LoadPedData
    {
        get => SettingsManager.GetBool(nameof(LoadPedData), true);
        set => SettingsManager.SetBool(nameof(LoadPedData), value);
    }

    public bool LoadVehicleData
    {
        get => SettingsManager.GetBool(nameof(LoadVehicleData), true);
        set => SettingsManager.SetBool(nameof(LoadVehicleData), value);
    }

    public double CacheTime
    {
        get => SettingsManager.GetDouble(nameof(CacheTime), 10.0);
        set => SettingsManager.SetDouble(nameof(CacheTime), value);
    }

    public long GPUGeometryCacheSize
    {
        get => SettingsManager.GetLong(nameof(GPUGeometryCacheSize), 536870912);
        set => SettingsManager.SetLong(nameof(GPUGeometryCacheSize), value);
    }

    public long GPUTextureCacheSize
    {
        get => SettingsManager.GetLong(nameof(GPUTextureCacheSize), 1073741824);
        set => SettingsManager.SetLong(nameof(GPUTextureCacheSize), value);
    }

    public long GPUBoundCompCacheSize
    {
        get => SettingsManager.GetLong(nameof(GPUBoundCompCacheSize), 134217728);
        set => SettingsManager.SetLong(nameof(GPUBoundCompCacheSize), value);
    }

    public double GPUCacheTime
    {
        get => SettingsManager.GetDouble(nameof(GPUCacheTime), 1.0);
        set => SettingsManager.SetDouble(nameof(GPUCacheTime), value);
    }

    public double GPUCacheFlushTime
    {
        get => SettingsManager.GetDouble(nameof(GPUCacheFlushTime), 0.1);
        set => SettingsManager.SetDouble(nameof(GPUCacheFlushTime), value);
    }

    // Camera Settings
    public float CameraSmoothing
    {
        get => SettingsManager.GetFloat(nameof(CameraSmoothing), 10f);
        set => SettingsManager.SetFloat(nameof(CameraSmoothing), value);
    }

    public float CameraSensitivity
    {
        get => SettingsManager.GetFloat(nameof(CameraSensitivity), 0.005f);
        set => SettingsManager.SetFloat(nameof(CameraSensitivity), value);
    }

    public float CameraFieldOfView
    {
        get => SettingsManager.GetFloat(nameof(CameraFieldOfView), 1f);
        set => SettingsManager.SetFloat(nameof(CameraFieldOfView), value);
    }

    // Render Settings
    public string RenderMode
    {
        get => SettingsManager.GetString(nameof(RenderMode), "Default");
        set => SettingsManager.SetString(nameof(RenderMode), value);
    }

    public string RenderTextureSampler
    {
        get => SettingsManager.GetString(nameof(RenderTextureSampler), "DiffuseSampler");
        set => SettingsManager.SetString(nameof(RenderTextureSampler), value);
    }

    public string RenderTextureSamplerCoord
    {
        get => SettingsManager.GetString(nameof(RenderTextureSamplerCoord), "Texture coord 1");
        set => SettingsManager.SetString(nameof(RenderTextureSamplerCoord), value);
    }

    public string ExcludeFolders
    {
        get => SettingsManager.GetString(nameof(ExcludeFolders), "Installers;_CommonRedist");
        set => SettingsManager.SetString(nameof(ExcludeFolders), value);
    }

    public bool AnisotropicFiltering
    {
        get => SettingsManager.GetBool(nameof(AnisotropicFiltering), true);
        set => SettingsManager.SetBool(nameof(AnisotropicFiltering), value);
    }

    public bool HDR
    {
        get => SettingsManager.GetBool(nameof(HDR), true);
        set => SettingsManager.SetBool(nameof(HDR), value);
    }

    public bool WindowMaximized
    {
        get => SettingsManager.GetBool(nameof(WindowMaximized), false);
        set => SettingsManager.SetBool(nameof(WindowMaximized), value);
    }

    public bool EnableMods
    {
        get => SettingsManager.GetBool(nameof(EnableMods), false);
        set => SettingsManager.SetBool(nameof(EnableMods), value);
    }

    public string DLC
    {
        get => SettingsManager.GetString(nameof(DLC));
        set => SettingsManager.SetString(nameof(DLC), value);
    }

    // Input Settings
    public bool XInputLThumbInvert
    {
        get => SettingsManager.GetBool(nameof(XInputLThumbInvert), true);
        set => SettingsManager.SetBool(nameof(XInputLThumbInvert), value);
    }

    public bool XInputRThumbInvert
    {
        get => SettingsManager.GetBool(nameof(XInputRThumbInvert), false);
        set => SettingsManager.SetBool(nameof(XInputRThumbInvert), value);
    }

    public float XInputLThumbSensitivity
    {
        get => SettingsManager.GetFloat(nameof(XInputLThumbSensitivity), 2f);
        set => SettingsManager.SetFloat(nameof(XInputLThumbSensitivity), value);
    }

    public float XInputRThumbSensitivity
    {
        get => SettingsManager.GetFloat(nameof(XInputRThumbSensitivity), 2f);
        set => SettingsManager.SetFloat(nameof(XInputRThumbSensitivity), value);
    }

    public float XInputZoomSpeed
    {
        get => SettingsManager.GetFloat(nameof(XInputZoomSpeed), 2f);
        set => SettingsManager.SetFloat(nameof(XInputZoomSpeed), value);
    }

    public float XInputMoveSpeed
    {
        get => SettingsManager.GetFloat(nameof(XInputMoveSpeed), 15f);
        set => SettingsManager.SetFloat(nameof(XInputMoveSpeed), value);
    }

    public bool MouseInvert
    {
        get => SettingsManager.GetBool(nameof(MouseInvert), false);
        set => SettingsManager.SetBool(nameof(MouseInvert), value);
    }

    public bool RememberGTAFolder
    {
        get => SettingsManager.GetBool(nameof(RememberGTAFolder), true);
        set => SettingsManager.SetBool(nameof(RememberGTAFolder), value);
    }

    // Theme Settings
    public string ProjectWindowTheme
    {
        get => SettingsManager.GetString(nameof(ProjectWindowTheme), "Blue");
        set => SettingsManager.SetString(nameof(ProjectWindowTheme), value);
    }

    public string ExplorerWindowTheme
    {
        get => SettingsManager.GetString(nameof(ExplorerWindowTheme), "Windows");
        set => SettingsManager.SetString(nameof(ExplorerWindowTheme), value);
    }

    public bool Deferred
    {
        get => SettingsManager.GetBool(nameof(Deferred), true);
        set => SettingsManager.SetBool(nameof(Deferred), value);
    }

    // Editor Settings
    public float SnapRotationDegrees
    {
        get => SettingsManager.GetFloat(nameof(SnapRotationDegrees), 5f);
        set => SettingsManager.SetFloat(nameof(SnapRotationDegrees), value);
    }

    public float SnapGridSize
    {
        get => SettingsManager.GetFloat(nameof(SnapGridSize), 10f);
        set => SettingsManager.SetFloat(nameof(SnapGridSize), value);
    }

    public bool JumpListInitialised
    {
        get => SettingsManager.GetBool(nameof(JumpListInitialised), false);
        set => SettingsManager.SetBool(nameof(JumpListInitialised), value);
    }

    // RPF Explorer Settings
    public string RPFExplorerSelectedFolder
    {
        get => SettingsManager.GetString(nameof(RPFExplorerSelectedFolder));
        set => SettingsManager.SetString(nameof(RPFExplorerSelectedFolder), value);
    }

    public string RPFExplorerExtraFolders
    {
        get => SettingsManager.GetString(nameof(RPFExplorerExtraFolders));
        set => SettingsManager.SetString(nameof(RPFExplorerExtraFolders), value);
    }

    public bool RPFExplorerStartInEditMode
    {
        get => SettingsManager.GetBool(nameof(RPFExplorerStartInEditMode), false);
        set => SettingsManager.SetBool(nameof(RPFExplorerStartInEditMode), value);
    }

    public string RPFExplorerStartFolder
    {
        get => SettingsManager.GetString(nameof(RPFExplorerStartFolder));
        set => SettingsManager.SetString(nameof(RPFExplorerStartFolder), value);
    }

    // World Settings
    public int TimeOfDay
    {
        get => SettingsManager.GetInt(nameof(TimeOfDay), 720);
        set => SettingsManager.SetInt(nameof(TimeOfDay), value);
    }

    public bool LODLights
    {
        get => SettingsManager.GetBool(nameof(LODLights), true);
        set => SettingsManager.SetBool(nameof(LODLights), value);
    }

    public string Region
    {
        get => SettingsManager.GetString(nameof(Region), "Global");
        set => SettingsManager.SetString(nameof(Region), value);
    }

    public string Clouds
    {
        get => SettingsManager.GetString(nameof(Clouds), "contrails");
        set => SettingsManager.SetString(nameof(Clouds), value);
    }

    public string Weather
    {
        get => SettingsManager.GetString(nameof(Weather), "EXTRASUNNY");
        set => SettingsManager.SetString(nameof(Weather), value);
    }

    public bool NaturalAmbientLight
    {
        get => SettingsManager.GetBool(nameof(NaturalAmbientLight), true);
        set => SettingsManager.SetBool(nameof(NaturalAmbientLight), value);
    }

    public bool ArtificialAmbientLight
    {
        get => SettingsManager.GetBool(nameof(ArtificialAmbientLight), true);
        set => SettingsManager.SetBool(nameof(ArtificialAmbientLight), value);
    }

    // Key Bindings
    public StringCollection KeyBindings
    {
        get
        {
            var bindings = SettingsManager.GetStringCollection(nameof(KeyBindings));
            if (bindings.Count == 0)
            {
                // Default key bindings
                bindings.Add("Move Forwards: W");
                bindings.Add("Move Backwards: S");
                bindings.Add("Move Left: A");
                bindings.Add("Move Right: D");
                bindings.Add("Move Up: R");
                bindings.Add("Move Down: F");
                bindings.Add("Move Slower / Zoom In: Z");
                bindings.Add("Move Faster / Zoom Out: X");
                bindings.Add("Toggle Mouse Select: C");
                bindings.Add("Toggle Toolbar: T");
                bindings.Add("Exit Edit Mode: Q");
                bindings.Add("Edit Position: W");
                bindings.Add("Edit Rotation: E");
                bindings.Add("Edit Scale: R");
            }
            return bindings;
        }
        set => SettingsManager.SetStringCollection(nameof(KeyBindings), value);
    }

    // Color Picker Settings
    public string ColourPickerCustomColours
    {
        get => SettingsManager.GetString(nameof(ColourPickerCustomColours));
        set => SettingsManager.SetString(nameof(ColourPickerCustomColours), value);
    }

    public string ColourPickerRecentColours
    {
        get => SettingsManager.GetString(nameof(ColourPickerRecentColours));
        set => SettingsManager.SetString(nameof(ColourPickerRecentColours), value);
    }

    // Position Settings
    public string StartPosition
    {
        get => SettingsManager.GetString(nameof(StartPosition), "0, 0, 100");
        set => SettingsManager.SetString(nameof(StartPosition), value);
    }

    public bool SavePosition
    {
        get => SettingsManager.GetBool(nameof(SavePosition), true);
        set => SettingsManager.SetBool(nameof(SavePosition), value);
    }

    public string StartRotation
    {
        get => SettingsManager.GetString(nameof(StartRotation), "0, 0, 0");
        set => SettingsManager.SetString(nameof(StartRotation), value);
    }

    public string StartCameraOrientation
    {
        get => SettingsManager.GetString(nameof(StartCameraOrientation), "");
        set => SettingsManager.SetString(nameof(StartCameraOrientation), value);
    }

    public bool SaveTimeOfDay
    {
        get => SettingsManager.GetBool(nameof(SaveTimeOfDay), true);
        set => SettingsManager.SetBool(nameof(SaveTimeOfDay), value);
    }

    public bool GTAGen9
    {
        get => SettingsManager.GetBool(nameof(GTAGen9), false);
        set => SettingsManager.SetBool(nameof(GTAGen9), value);
    }

    public void Save()
    {
        SettingsManager.Save();
    }

    public void Reset()
    {
        SettingsManager.Reset();
    }
}
