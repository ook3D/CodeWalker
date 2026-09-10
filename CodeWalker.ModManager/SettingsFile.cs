using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CodeWalker.ModManager
{
    public class SettingsFile
    {
        private Properties.Settings Settings => Properties.Settings.Default;
        
        public string GameFolder { get; set; } = string.Empty;
        public string GameFolderLegacy
        {
            get => Settings.GameFolderLegacy;
            set => Settings.GameFolderLegacy = value;
        }
        public string GameFolderEnhanced
        {
            get => Settings.GameFolderEnhanced;
            set => Settings.GameFolderEnhanced = value;
        }
        public bool IsGen9
        {
            get => Settings.IsGen9;
            set => Settings.IsGen9 = value;
        }
        public string AESKey
        {
            get => Settings.AESKey ?? string.Empty;
            set => Settings.AESKey = value;
        }

        public string GameName => GameFolderOk ? IsGen9 ? "GTAV (Enhanced)" : "GTAV (Legacy)" : "(None selected)";
        public string GameTitle => IsGen9 ? "GTAV Enhanced" : "GTAV Legacy";
        public string GameExeName => IsGen9 ? "gta5_enhanced.exe" : "gta5.exe";
        public string GameExePath => $"{GameFolder}\\{GameExeName}";
        public string GameModCache => IsGen9 ? "GTAVEnhanced" : "GTAVLegacy";
        public bool GameFolderOk
        {
            get
            {
                if (Directory.Exists(GameFolder) == false) return false;
                if (File.Exists(GameExePath) == false) return false;
                return true;
            }
        }

        public SettingsFile()
        {
            try
            {
                Load();
            }
            catch (Exception ex)
            {
                // Log error if needed
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            }
        }

        public void Load()
        {
            // Reload settings from App.config
            Settings.Reload();
            
            // Set GameFolder to the appropriate path based on current mode
            GameFolder = IsGen9 ? GameFolderEnhanced : GameFolderLegacy;
            
            // Ensure we have non-null values
            if (string.IsNullOrEmpty(GameFolder))
            {
                GameFolder = string.Empty;
            }
        }

        public void Save()
        {
            // Save settings to App.config
            Settings.Save();
        }

        public void Reset()
        {
            GameFolder = string.Empty;
            GameFolderLegacy = string.Empty;
            GameFolderEnhanced = string.Empty;
            IsGen9 = false;
            AESKey = string.Empty;
            Save();
        }
    }
}
