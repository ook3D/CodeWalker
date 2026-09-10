using CodeWalker.Core.GameFiles.FileTypes.Builders;
using CodeWalker.GameFiles;
using CodeWalker.World;
using SharpDX;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace CodeWalker.Project.Panels
{
    public partial class GenerateNavMeshPanel : ProjectPanel
    {
        public ProjectForm ProjectForm { get; set; }
        public ProjectFile? CurrentProjectFile { get; set; }

        private BackgroundWorker backgroundWorker;
        private CancellationTokenSource? cancellationTokenSource;
        private bool isGenerating = false;

        public GenerateNavMeshPanel(ProjectForm projectForm)
        {
            ProjectForm = projectForm;
            InitializeComponent();
            Tag = "GenerateNavMeshPanel";

            if (ProjectForm.WorldForm == null)
            {
                //could happen in some other startup mode - world form is required for this..
                GenerateButton.Enabled = false;
                UpdateStatus("Unable to generate - World View not available!");
            }

            // Initialize background worker
            backgroundWorker = new BackgroundWorker();
            backgroundWorker.WorkerReportsProgress = true;
            backgroundWorker.WorkerSupportsCancellation = true;
            backgroundWorker.DoWork += BackgroundWorker_DoWork;
            backgroundWorker.ProgressChanged += BackgroundWorker_ProgressChanged;
            backgroundWorker.RunWorkerCompleted += BackgroundWorker_RunWorkerCompleted;
        }

        public void SetProject(ProjectFile project)
        {
            CurrentProjectFile = project;
        }

        private void GenerateButton_Click(object sender, EventArgs e)
        {
            if (isGenerating)
            {
                MessageBox.Show("Generation is already in progress.");
                return;
            }

            var space = ProjectForm.WorldForm?.Space;
            if (space == null)
            {
                MessageBox.Show("Unable to generate - World View not available!");
                return;
            }

            var gameFileCache = ProjectForm.WorldForm?.GameFileCache;
            if (gameFileCache == null)
            {
                MessageBox.Show("Unable to generate - Game file cache not available!");
                return;
            }

            // Validate inputs
            if (!ValidateInputs(out Vector2 min, out Vector2 max, out string errorMessage))
            {
                MessageBox.Show(errorMessage, "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Prepare generation parameters (defaults match R* fwNavGenConfig)
            var genParams = new NavGenParams
            {
                SamplingDensity = (float)SamplingDensityNumeric.Value,
                TriangulationMaxHeightDiff = (float)HeightThresholdNumeric.Value,
                MaxAngleForWalkable = (float)MaxSlopeAngleNumeric.Value
            };

            // Prepare generation context
            var context = new GenerationContext
            {
                Space = space,
                GameFileCache = gameFileCache,
                Min = min,
                Max = max,
                GenParams = genParams,
                ProjectFolder = GetProjectFolder()
            };

            // Start generation
            isGenerating = true;
            GenerateButton.Enabled = false;
            CancelButton.Enabled = true;
            ProgressBar.Value = 0;
            UpdateStatus("Starting generation...");

            cancellationTokenSource = new CancellationTokenSource();
            backgroundWorker.RunWorkerAsync(context);
        }

        private bool ValidateInputs(out Vector2 min, out Vector2 max, out string errorMessage)
        {
            min = Vector2.Zero;
            max = Vector2.Zero;
            errorMessage = string.Empty;

            // Parse min/max coordinates
            try
            {
                min = FloatUtil.ParseVector2String(MinTextBox.Text);
                max = FloatUtil.ParseVector2String(MaxTextBox.Text);
            }
            catch
            {
                errorMessage = "Invalid coordinate format. Please use format: X, Y";
                return false;
            }

            // Check if area is valid
            if (min == max)
            {
                errorMessage = "No valid area was specified!\nMake sure Min and Max form a box around the area you want to generate the nav meshes for.";
                return false;
            }

            // Check if coordinates are in valid range
            if ((min.X < -6000) || (min.Y < -6000) || (max.X > 9000) || (max.Y > 9000))
            {
                if (MessageBox.Show("Warning: min/max goes outside the possible navmesh area - valid range is from -6000 to 9000 (X and Y).\nDo you want to continue anyway?", 
                    "Warning - specified area out of range", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                {
                    errorMessage = "Generation cancelled by user.";
                    return false;
                }
            }

            // Validate parameter ranges
            if (SamplingDensityNumeric.Value < 0.1m || SamplingDensityNumeric.Value > 5m)
            {
                errorMessage = "Sampling density must be between 0.1 and 5.0 meters.";
                return false;
            }

            if (HeightThresholdNumeric.Value < 0.1m || HeightThresholdNumeric.Value > 10m)
            {
                errorMessage = "Height threshold must be between 0.1 and 10.0 meters.";
                return false;
            }

            if (MaxSlopeAngleNumeric.Value < 10m || MaxSlopeAngleNumeric.Value > 90m)
            {
                errorMessage = "Max slope angle must be between 10 and 90 degrees.";
                return false;
            }

            return true;
        }

        private string GetProjectFolder()
        {
            string? projectFolder = Path.GetDirectoryName(CurrentProjectFile?.Filepath);
            if (string.IsNullOrEmpty(projectFolder))
            {
                projectFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
            return projectFolder;
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            if (isGenerating && cancellationTokenSource != null)
            {
                UpdateStatus("Cancelling generation...");
                CancelButton.Enabled = false;
                cancellationTokenSource.Cancel();
            }
        }

        private void BackgroundWorker_DoWork(object? sender, DoWorkEventArgs e)
        {
            var context = e.Argument as GenerationContext ?? throw new InvalidOperationException("Missing generation context.");
            var worker = sender as BackgroundWorker ?? throw new InvalidOperationException("Missing generation worker.");
            var token = cancellationTokenSource?.Token ?? throw new InvalidOperationException("Generation has not started.");

            try
            {
                // Normalize min/max so min < max on both axes
                var cmin = context.Min;
                var cmax = context.Max;
                if (cmin.X > cmax.X) { float t = cmin.X; cmin.X = cmax.X; cmax.X = t; }
                if (cmin.Y > cmax.Y) { float t = cmin.Y; cmin.Y = cmax.Y; cmax.Y = t; }

                // Snap to full navmesh grid cell boundaries (R* always generates complete sectors)
                // Each grid cell is 150m, starting at WorldMin (-6000, -6000)
                float cellSize = context.GenParams.NavGridCellSize;
                float worldMinX = context.GenParams.WorldMinX;
                float worldMinY = context.GenParams.WorldMinY;
                cmin.X = worldMinX + (float)Math.Floor((cmin.X - worldMinX) / cellSize) * cellSize;
                cmin.Y = worldMinY + (float)Math.Floor((cmin.Y - worldMinY) / cellSize) * cellSize;
                cmax.X = worldMinX + (float)Math.Ceiling((cmax.X - worldMinX) / cellSize) * cellSize;
                cmax.Y = worldMinY + (float)Math.Ceiling((cmax.Y - worldMinY) / cellSize) * cellSize;

                context.Min = cmin;
                context.Max = cmax;
                worker.ReportProgress(-1, $"Snapped to grid cells: [{cmin.X:F0}, {cmin.Y:F0}] to [{cmax.X:F0}, {cmax.Y:F0}]");

                // Create YnvBuilder instance
                var builder = new YnvBuilder();
                builder.SetGenerationParams(context.GenParams);

                // Initialize log file
                string logPath = Path.Combine(context.ProjectFolder, $"NavMeshGen_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                builder.InitializeLogFile(logPath);
                worker.ReportProgress(-1, $"Log file: {logPath}");

                // Phase 1: Load collision geometry (10%)
                if (token.IsCancellationRequested) { e.Cancel = true; return; }
                worker.ReportProgress(5, "Loading collision geometry...");
                
                bool loadSuccess = false;
                try
                {
                    loadSuccess = builder.LoadCollisionGeometry(
                        context.GameFileCache,
                        context.Space.BoundsStore,
                        context.Min,
                        context.Max,
                        (status) => worker.ReportProgress(-1, status)
                    );
                }
                catch (Exception ex)
                {
                    e.Result = new GenerationResult 
                    { 
                        Success = false, 
                        Message = $"Error loading collision geometry: {ex.Message}\n\nPlease check that:\n" +
                                  "- The specified area contains valid collision files\n" +
                                  "- The game files are properly loaded\n" +
                                  "- There is enough memory available"
                    };
                    return;
                }

                if (!loadSuccess)
                {
                    e.Result = new GenerationResult 
                    { 
                        Success = false, 
                        Message = "Failed to load collision geometry.\n\n" +
                                  "This can happen if:\n" +
                                  "- No YBN files exist in the specified area\n" +
                                  "- YBN files failed to load (check status messages)\n" +
                                  "- The octree could not be built from the collision data"
                    };
                    return;
                }

                worker.ReportProgress(10, "Collision geometry loaded");

                // Phase 2: Perform height sampling (20%)
                if (token.IsCancellationRequested) { e.Cancel = true; return; }
                worker.ReportProgress(15, "Performing height sampling...");
                
                int sampleCount = 0;
                try
                {
                    // Get Z bounds from loaded collision (R* exporter cutoffs: -500 to 1000)
                    var bmin = new Vector3(context.Min, context.GenParams.ExporterMinZCutoff);
                    var bmax = new Vector3(context.Max, context.GenParams.ExporterMaxZCutoff);
                    var boundslist = context.Space.BoundsStore.GetItems(ref bmin, ref bmax);
                    
                    foreach (var boundsitem in boundslist)
                    {
                        var ybn = context.GameFileCache.GetYbn(boundsitem.Name);
                        if (ybn?.Loaded == true && ybn.Bounds != null)
                        {
                            bmin.Z = Math.Min(bmin.Z, ybn.Bounds.BoxMin.Z);
                            bmax.Z = Math.Max(bmax.Z, ybn.Bounds.BoxMax.Z);
                        }
                    }

                    sampleCount = builder.PerformHeightSampling(
                        context.Min,
                        context.Max,
                        bmin.Z,
                        bmax.Z,
                        (status) => worker.ReportProgress(-1, status)
                    );
                }
                catch (Exception ex)
                {
                    e.Result = new GenerationResult 
                    { 
                        Success = false, 
                        Message = $"Error during height sampling: {ex.Message}\n\n" +
                                  "This may indicate:\n" +
                                  "- Invalid sampling density parameter\n" +
                                  "- Memory allocation failure\n" +
                                  "- Corrupt collision geometry"
                    };
                    return;
                }

                if (sampleCount == 0)
                {
                    e.Result = new GenerationResult 
                    { 
                        Success = false, 
                        Message = "No height samples created.\n\n" +
                                  "This can happen if:\n" +
                                  "- The collision geometry has no surfaces in the specified area\n" +
                                  "- The sampling density is too low\n" +
                                  "- The Z bounds are incorrect"
                    };
                    return;
                }

                worker.ReportProgress(20, $"Height sampling complete: {sampleCount} samples");

                // Phase 3: Perform triangulation (35%)
                if (token.IsCancellationRequested) { e.Cancel = true; return; }
                worker.ReportProgress(25, "Performing triangulation...");
                
                int triangleCount = 0;
                try
                {
                    triangleCount = builder.PerformTriangulation((status) => worker.ReportProgress(-1, status));
                }
                catch (Exception ex)
                {
                    e.Result = new GenerationResult 
                    { 
                        Success = false, 
                        Message = $"Error during triangulation: {ex.Message}\n\n" +
                                  "This may indicate:\n" +
                                  "- Invalid height threshold parameter\n" +
                                  "- Degenerate geometry in the collision data\n" +
                                  "- Memory allocation failure"
                    };
                    return;
                }
                
                if (triangleCount == 0)
                {
                    e.Result = new GenerationResult 
                    { 
                        Success = false, 
                        Message = "No triangles created during triangulation.\n\n" +
                                  "This can happen if:\n" +
                                  "- Height samples are too far apart\n" +
                                  "- Height threshold is too restrictive\n" +
                                  "- Line-of-sight tests are failing due to obstacles"
                    };
                    return;
                }

                worker.ReportProgress(35, $"Triangulation complete: {triangleCount} triangles");

                // Phase 4: Perform edge collapse optimization (50%)
                if (token.IsCancellationRequested) { e.Cancel = true; return; }
                worker.ReportProgress(40, "Performing edge collapse optimization...");
                
                int edgesCollapsed = 0;
                try
                {
                    edgesCollapsed = builder.PerformEdgeCollapseOptimization((status) => worker.ReportProgress(-1, status));
                }
                catch (Exception ex)
                {
                    // Edge collapse optimization is optional - log warning but continue
                    worker.ReportProgress(-1, $"Warning: Edge collapse optimization failed: {ex.Message}");
                    worker.ReportProgress(-1, "Continuing with unoptimized mesh...");
                }
                
                worker.ReportProgress(50, $"Optimization complete: {edgesCollapsed} edges collapsed");

                // Phase 5: Perform polygon merging (65%)
                if (token.IsCancellationRequested) { e.Cancel = true; return; }
                worker.ReportProgress(55, "Performing polygon merging...");
                
                List<NavSurfacePoly>? polygons = null;
                try
                {
                    polygons = builder.PerformPolygonMerging((status) => worker.ReportProgress(-1, status));
                }
                catch (Exception ex)
                {
                    e.Result = new GenerationResult 
                    { 
                        Success = false, 
                        Message = $"Error during polygon merging: {ex.Message}\n\n" +
                                  "This may indicate:\n" +
                                  "- Invalid convexity or coplanarity parameters\n" +
                                  "- Degenerate triangles in the mesh\n" +
                                  "- Memory allocation failure"
                    };
                    return;
                }
                
                if (polygons == null || polygons.Count == 0)
                {
                    e.Result = new GenerationResult 
                    { 
                        Success = false, 
                        Message = "No polygons created during merging.\n\n" +
                                  "This can happen if:\n" +
                                  "- All triangles were removed as degenerate\n" +
                                  "- Merging parameters are too restrictive\n" +
                                  "- The triangulated mesh is invalid"
                    };
                    return;
                }

                worker.ReportProgress(65, $"Polygon merging complete: {polygons.Count} polygons");

                // Phase 6: Remove colinear edges (70%)
                if (token.IsCancellationRequested) { e.Cancel = true; return; }
                worker.ReportProgress(68, "Removing colinear edges...");
                
                int verticesRemoved = 0;
                try
                {
                    verticesRemoved = builder.RemoveColinearEdges((status) => worker.ReportProgress(-1, status));
                }
                catch (Exception ex)
                {
                    // Colinear edge removal is optional - log warning but continue
                    worker.ReportProgress(-1, $"Warning: Colinear edge removal failed: {ex.Message}");
                    worker.ReportProgress(-1, "Continuing with unoptimized polygons...");
                }
                
                worker.ReportProgress(70, $"Colinear edge removal complete: {verticesRemoved} vertices removed");

                // Phase 7: Split polygons into grid cells (80%)
                if (token.IsCancellationRequested) { e.Cancel = true; return; }
                worker.ReportProgress(75, "Splitting polygons into grid cells...");
                
                List<NavSurfacePoly>? splitPolygons = null;
                try
                {
                    splitPolygons = builder.SplitPolygonsIntoGridCells(polygons, (status) => worker.ReportProgress(-1, status));
                }
                catch (Exception ex)
                {
                    e.Result = new GenerationResult 
                    { 
                        Success = false, 
                        Message = $"Error during grid cell splitting: {ex.Message}\n\n" +
                                  "This may indicate:\n" +
                                  "- Invalid grid cell size parameter\n" +
                                  "- Polygon splitting algorithm failure\n" +
                                  "- Memory allocation failure"
                    };
                    return;
                }
                
                if (splitPolygons == null || splitPolygons.Count == 0)
                {
                    e.Result = new GenerationResult 
                    { 
                        Success = false, 
                        Message = "No polygons after grid cell splitting.\n\n" +
                                  "This can happen if:\n" +
                                  "- All polygons were removed during splitting\n" +
                                  "- The grid cell size is too small\n" +
                                  "- The polygon splitting algorithm failed"
                    };
                    return;
                }

                worker.ReportProgress(80, $"Grid cell splitting complete: {splitPolygons.Count} polygons");

                // Phase 8: Convert to YNV files (90%)
                if (token.IsCancellationRequested) { e.Cancel = true; return; }
                worker.ReportProgress(85, "Converting to YNV files...");
                
                List<YnvFile>? ynvFiles = null;
                try
                {
                    ynvFiles = builder.ConvertToYnvFiles(splitPolygons, (status) => worker.ReportProgress(-1, status));
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("exceeds maximum vertex count"))
                {
                    // Specific error for vertex count overflow
                    e.Result = new GenerationResult 
                    { 
                        Success = false, 
                        Message = $"Vertex count overflow: {ex.Message}\n\n" +
                                  "To fix this:\n" +
                                  "- Reduce the sampling density (increase the value)\n" +
                                  "- Increase the grid cell size\n" +
                                  "- Enable more aggressive polygon merging\n" +
                                  "- Process a smaller area"
                    };
                    return;
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("invalid adjacency"))
                {
                    // Specific error for adjacency validation failure
                    e.Result = new GenerationResult 
                    { 
                        Success = false, 
                        Message = $"Polygon adjacency validation failed: {ex.Message}\n\n" +
                                  "This indicates a bug in the navmesh generation algorithm.\n" +
                                  "Please report this issue with the generation parameters used."
                    };
                    return;
                }
                catch (Exception ex)
                {
                    e.Result = new GenerationResult 
                    { 
                        Success = false, 
                        Message = $"Error converting to YNV files: {ex.Message}\n\n" +
                                  "This may indicate:\n" +
                                  "- Polygon vertex count overflow\n" +
                                  "- Invalid polygon adjacency\n" +
                                  "- YNV file format error"
                    };
                    return;
                }
                
                if (ynvFiles == null || ynvFiles.Count == 0)
                {
                    e.Result = new GenerationResult 
                    { 
                        Success = false, 
                        Message = "No YNV files created.\n\n" +
                                  "This can happen if:\n" +
                                  "- All polygons were removed during validation\n" +
                                  "- The grid cell assignment failed\n" +
                                  "- The YNV file creation algorithm failed"
                    };
                    return;
                }

                worker.ReportProgress(85, $"YNV file generation complete: {ynvFiles.Count} files");

                // Phase 9: Stitch to existing adjacent navmeshes (90%)
                if (token.IsCancellationRequested) { e.Cancel = true; return; }
                worker.ReportProgress(88, "Stitching to existing adjacent navmeshes...");

                try
                {
                    int stitchedEdges = builder.StitchToExistingNavMeshes(
                        context.GameFileCache,
                        (status) => worker.ReportProgress(-1, status)
                    );
                    worker.ReportProgress(90, $"Stitching complete: {stitchedEdges} boundary edges connected");
                }
                catch (Exception ex)
                {
                    // Stitching is optional - log warning but continue
                    worker.ReportProgress(-1, $"Warning: Boundary stitching failed: {ex.Message}");
                    worker.ReportProgress(-1, "Continuing without stitching - boundary edges will be disconnected");
                }

                // Phase 10: Save YNV files (100%)
                if (token.IsCancellationRequested) { e.Cancel = true; return; }
                worker.ReportProgress(95, "Saving YNV files...");
                
                string navmeshFolder = Path.Combine(context.ProjectFolder, "navmeshes");
                
                try
                {
                    Directory.CreateDirectory(navmeshFolder);
                }
                catch (Exception ex)
                {
                    e.Result = new GenerationResult 
                    { 
                        Success = false, 
                        Message = $"Error creating output directory: {ex.Message}\n\n" +
                                  $"Path: {navmeshFolder}\n\n" +
                                  "Please check:\n" +
                                  "- You have write permissions to the project folder\n" +
                                  "- The path is valid\n" +
                                  "- There is enough disk space"
                    };
                    return;
                }

                int savedCount = 0;
                int failedCount = 0;
                var failedFiles = new List<string>();
                
                foreach (var ynv in ynvFiles)
                {
                    if (token.IsCancellationRequested) { e.Cancel = true; return; }
                    
                    try
                    {
                        byte[] data = ynv.Save();
                        string filename = Path.Combine(navmeshFolder, ynv.Name + ".ynv");
                        File.WriteAllBytes(filename, data);
                        savedCount++;
                    }
                    catch (Exception ex)
                    {
                        worker.ReportProgress(-1, $"Error saving {ynv.Name}: {ex.Message}");
                        failedFiles.Add(ynv.Name);
                        failedCount++;
                    }
                }

                worker.ReportProgress(100, "Generation complete!");
                
                // Close log file
                builder.CloseLogFile();

                string resultMessage = $"Successfully generated {savedCount} YNV files";
                if (failedCount > 0)
                {
                    resultMessage += $"\n\nWarning: {failedCount} files failed to save:\n" + 
                                    string.Join("\n", failedFiles.Take(10));
                    if (failedFiles.Count > 10)
                    {
                        resultMessage += $"\n... and {failedFiles.Count - 10} more";
                    }
                }

                e.Result = new GenerationResult
                {
                    Success = savedCount > 0,
                    Message = resultMessage,
                    Summary = $"{sampleCount} samples → {triangleCount} triangles → {polygons.Count} polygons → {ynvFiles.Count} YNV files",
                    OutputFolder = navmeshFolder
                };
            }
            catch (OutOfMemoryException ex)
            {
                e.Result = new GenerationResult 
                { 
                    Success = false, 
                    Message = $"Out of memory error: {ex.Message}\n\n" +
                              "The navmesh generation requires too much memory.\n\n" +
                              "To fix this:\n" +
                              "- Process a smaller area\n" +
                              "- Increase the sampling density (use larger value)\n" +
                              "- Close other applications to free up memory\n" +
                              "- Restart CodeWalker to clear memory"
                };
            }
            catch (Exception ex)
            {
                e.Result = new GenerationResult 
                { 
                    Success = false, 
                    Message = $"Unexpected error during generation: {ex.Message}\n\n" +
                              $"Error type: {ex.GetType().Name}\n\n" +
                              "Please report this error if it persists.\n\n" +
                              "Check the log file for detailed information."
                };
            }
            finally
            {
                // Ensure log file is closed even if there's an error
                try
                {
                    var builder = e.Argument as YnvBuilder;
                    builder?.CloseLogFile();
                }
                catch { }
            }
        }

        private void BackgroundWorker_ProgressChanged(object? sender, ProgressChangedEventArgs e)
        {
            if (e.ProgressPercentage >= 0)
            {
                ProgressBar.Value = Math.Min(e.ProgressPercentage, 100);
            }

            if (e.UserState is string status)
            {
                UpdateStatus(status);
            }
        }

        private void BackgroundWorker_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
        {
            isGenerating = false;
            GenerateButton.Enabled = true;
            CancelButton.Enabled = false;

            if (e.Cancelled)
            {
                ProgressBar.Value = 0;
                UpdateStatus("Generation cancelled by user");
                MessageBox.Show("Navmesh generation was cancelled.", "Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (e.Error != null)
            {
                ProgressBar.Value = 0;
                UpdateStatus($"Error: {e.Error.Message}");
                MessageBox.Show($"An error occurred during generation:\n{e.Error.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else if (e.Result is GenerationResult result)
            {
                if (result.Success)
                {
                    ProgressBar.Value = 100;
                    UpdateStatus(result.Message);
                    MessageBox.Show($"{result.Message}\n\n{result.Summary}\n\nFiles saved to: {result.OutputFolder}", 
                        "Generation Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    ProgressBar.Value = 0;
                    UpdateStatus(result.Message);
                    MessageBox.Show($"Generation failed:\n{result.Message}", "Generation Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;
        }

        private void UpdateStatus(string text)
        {
            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => { UpdateStatus(text); }));
                }
                else
                {
                    StatusLabel.Text = text;
                }
            }
            catch { }
        }

        /// <summary>
        /// Context for background generation
        /// </summary>
        private class GenerationContext
        {
            public required Space Space { get; set; }
            public required GameFileCache GameFileCache { get; set; }
            public Vector2 Min { get; set; }
            public Vector2 Max { get; set; }
            public required NavGenParams GenParams { get; set; }
            public required string ProjectFolder { get; set; }
        }

        /// <summary>
        /// Result of generation process
        /// </summary>
        private class GenerationResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public string Summary { get; set; } = string.Empty;
            public string OutputFolder { get; set; } = string.Empty;
        }
    }
}
