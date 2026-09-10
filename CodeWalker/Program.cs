using CodeWalker.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Shell;

namespace CodeWalker;

static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {

            bool menumode = false;
            bool explorermode = false;
            bool projectmode = false;
            bool vehiclesmode = false;
            bool pedsmode = false;
            if (args is { Length: > 0 })
            {
                foreach (string arg in args)
                {
                    switch (arg.ToLowerInvariant())
                    {
                        case "menu":
                            menumode = true;
                            break;
                        case "explorer":
                            explorermode = true;
                            break;
                        case "project":
                            projectmode = true;
                            break;
                        case "vehicles":
                            vehiclesmode = true;
                            break;
                        case "peds":
                            pedsmode = true;
                            break;
                    }
                }
            }

            EnsureJumpList();

            //Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);


            // Always check the GTA folder first thing
            if (!GTAFolder.UpdateGTAFolder(Properties.Settings.Default.RememberGTAFolder))
            {
                MessageBox.Show("Could not load CodeWalker because no valid GTA 5 folder was selected. CodeWalker will now exit.", "GTA 5 Folder Not Found", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return;
            }
#if !DEBUG
            try
            {
#endif
                if (menumode)
                {
                    Application.Run(new MenuForm());
                }
                else if (explorermode)
                {
                    Application.Run(new ExploreForm());
                }
                else if (projectmode)
                {
                    Application.Run(new Project.ProjectForm());
                }
                else if (vehiclesmode)
                {
                    Application.Run(new VehicleForm());
                }
                else if (pedsmode)
                {
                    Application.Run(new PedsForm());
                }
                else
                {
                    Application.Run(new WorldForm());
                }
#if !DEBUG
            }
            catch (Exception ex)
            {
                MessageBox.Show("An unexpected error was encountered!\n" + ex.ToString());
                //this can happen if folder wasn't chosen, or in some other catastrophic error. meh.
            }
#endif
        }


        static void EnsureJumpList()
        {
            if (Settings.Default.JumpListInitialised) return;

            try
            {
                var cwpath = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).Location;
                var cwdir = Path.GetDirectoryName(cwpath) ?? AppContext.BaseDirectory;

                JumpTask jtWorld = new()
                {
                    ApplicationPath = cwpath,
                    IconResourcePath = cwpath,
                    WorkingDirectory = cwdir,
                    Arguments = "",
                    Title = "World View",
                    Description = "Display the GTAV World",
                    CustomCategory = "Launch Options"
                };

                JumpTask jtExplorer = new()
                {
                    ApplicationPath = cwpath,
                    IconResourcePath = Path.Combine(cwdir, "CodeWalker RPF Explorer.exe"),
                    WorkingDirectory = cwdir,
                    Arguments = "explorer",
                    Title = "RPF Explorer",
                    Description = "Open RPF Explorer",
                    CustomCategory = "Launch Options"
                };

                JumpTask jtVehicles = new()
                {
                    ApplicationPath = cwpath,
                    IconResourcePath = Path.Combine(cwdir, "CodeWalker Vehicle Viewer.exe"),
                    WorkingDirectory = cwdir,
                    Arguments = "vehicles",
                    Title = "Vehicle Viewer",
                    Description = "Open Vehicle Viewer",
                    CustomCategory = "Launch Options"
                };

                JumpTask jtPeds = new()
                {
                    ApplicationPath = cwpath,
                    IconResourcePath = Path.Combine(cwdir, "CodeWalker Ped Viewer.exe"),
                    WorkingDirectory = cwdir,
                    Arguments = "peds",
                    Title = "Ped Viewer",
                    Description = "Open Ped Viewer",
                    CustomCategory = "Launch Options"
                };

                JumpList jumpList = new();

                jumpList.JumpItems.Add(jtWorld);
                jumpList.JumpItems.Add(jtExplorer);
                jumpList.JumpItems.Add(jtVehicles);
                jumpList.JumpItems.Add(jtPeds);

                jumpList.Apply();

                Settings.Default.JumpListInitialised = true;
                Settings.Default.Save();
            }
            catch
            { }
        }
    }
