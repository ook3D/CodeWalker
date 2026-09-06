using CodeWalker.GameFiles;
using CodeWalker.World;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CodeWalker;

namespace CodeWalker.Core.GameFiles.FileTypes.Builders
{
    public class YnvBuilder
    {
        /*
         * 
         * YnvBuilder by dexyfex
         * 
         * This class allows for conversion of navmesh data in a generic format into .ynv files.
         * The usage is to call AddPoly() with an array of vertex positions for each polygon.
         * Polygons should be wound in an anticlockwise direction.
         * The returned YnvPoly object needs to have its Edges array set by the importer.
         * YnvPoly.Edges is an array of YnvEdge, with one edge for each vertex in the poly. 
         * The first edge should join the first and second vertices, and the last edge should
         * join the last and first vertices.
         * The YnvEdge Poly1 and Poly2 both need to be set to the same value, which is the 
         * corresponding YnvPoly object that was returned by AddPoly.
         * Flags values on the polygons and edges also need to be set by the importer.
         * 
         * Once the polygons and edges have all been added, the Build() method should be called,
         * which will return a list of YnvFile objects. Call the Save() method on each of those
         * to get the byte array for the .ynv file. The correct filename is given by the
         * YnvFile.Name property.
         * Note that the .ynv building process will split polygons that cross .ynv area borders,
         * and assign all the new polygons into the correct .ynvs.
         * 
         */



        public List<YnvPoly> PolyList = new();
        public string VehicleName = string.Empty;
        private SpaceNavGrid NavGrid = null;
        private List<YnvFile> YnvFiles = null;
        
        // Navmesh generation fields
        private NavOctree collisionOctree = null;
        private List<NavGenTri> collisionTriangles = new();
        private CPlacedNodeMultiMap heightSampleGrid = null;
        private NavGenParams genParams = new();
        private List<NavSurfaceTri> surfaceTriangles = new();
        private List<NavSurfacePoly> surfacePolygons = new();
        
        // Logging
        private System.IO.StreamWriter logWriter = null;
        private string logFilePath = null;

        public YnvPoly AddPoly(Vector3[]? verts)
        {
            if ((verts == null) || (verts.Length < 3))
            { return null; }

            YnvPoly poly = new();
            poly.AreaID = 0x3FFF;
            poly.Index = PolyList.Count;
            poly.Vertices = verts;

            PolyList.Add(poly);

            return poly;
        }






        public List<YnvFile> Build(bool forVehicle)
        {
            NavGrid = new SpaceNavGrid();
            YnvFiles = new List<YnvFile>();

            if (forVehicle) //for vehicle YNV, only need a single ynv, no splitting
            {
                AddVehiclePolys(PolyList);

                FinalizeYnvs(YnvFiles, true);
            }
            else //for static world ynv, need to split polys and generate a set of ynvs.
            {
                // split polys going over nav grid borders, first by X then by Y
                var splitpolysX = SplitPolys(PolyList, true);
                var splitpolysY = SplitPolys(splitpolysX, false);

                // assign polys into their new ynvs
                AddPolysIntoGrid(splitpolysY);


                // fix up generated ynvs
                FinalizeYnvs(YnvFiles, false);

            }

            return YnvFiles;
        }





        private List<YnvPoly> SplitPolys(List<YnvPoly> polys, bool xaxis)
        {
            var newpolys = new List<YnvPoly>();

            var verts1 = new List<Vector3>();
            var verts2 = new List<Vector3>();
            var edges1 = new List<YnvEdge>();
            var edges2 = new List<YnvEdge>();

            var polysplits = new Dictionary<YnvPoly, YnvPolySplit>();

            foreach (var poly in polys)  //split along borders
            {
                var verts = poly.Vertices;
                if (verts == null)
                { continue; }//ignore empty polys..
                if (verts.Length < 3)
                { continue; }//not enough verts for a triangle!

                Vector2I gprev = NavGrid.GetCellPos(verts[0]);
                int split1 = 0;
                int split2 = 0;
                for (int i = 1; i < verts.Length; i++)
                {
                    Vector2I g = NavGrid.GetCellPos(verts[i]);
                    int g1 = xaxis ? g.X : g.Y;
                    int g2 = xaxis ? gprev.X : gprev.Y;
                    if (g1 != g2) //this poly is crossing a border
                    {
                        if (split1 == 0) { split1 = i; }
                        else { split2 = i; break; }
                    }
                    gprev = g;
                }
                if (split1 > 0)
                {
                    var split2beg = (split2 > 0) ? split2 - 1 : verts.Length - 1;
                    var split2end = split2beg + 1;
                    var sv11 = verts[split1 - 1];
                    var sv12 = verts[split1];
                    var sv21 = verts[split2beg];
                    var sv22 = verts[split2];
                    var sp1 = GetSplitPos(sv11, sv12, xaxis);
                    var sp2 = GetSplitPos(sv21, sv22, xaxis);

                    //if ((sp1 == sp2) || (sp1 == sv11) || (sp1 == sv12) || (sp2 == sv21) || (sp2 == sv22))
                    if (!IsValidSplit(sp1, sp2, sv11, sv12, sv21, sv22))
                    {
                        //split did nothing, just leave this poly alone
                        newpolys.Add(poly);
                    }
                    else
                    {
                        //split it!
                        var poly1 = new YnvPoly();
                        var poly2 = new YnvPoly();
                        poly1.RawData = poly.RawData;
                        poly2.RawData = poly.RawData;
                        verts1.Clear();
                        verts2.Clear();

                        for (int i = 0; i < split1; i++) verts1.Add(verts[i]);
                        verts1.Add(sp1);
                        verts1.Add(sp2);
                        for (int i = split2end; i < verts.Length; i++) verts1.Add(verts[i]);

                        verts2.Add(sp1);
                        for (int i = split1; i < split2end; i++) verts2.Add(verts[i]);
                        verts2.Add(sp2);

                        poly1.Vertices = verts1.ToArray();
                        poly2.Vertices = verts2.ToArray();


                        //save this information for the edge splitting pass
                        var polysplit = new YnvPolySplit();
                        polysplit.Orig = poly;
                        polysplit.New1 = poly1;
                        polysplit.New2 = poly2;
                        polysplit.Split1 = split1;
                        polysplit.Split2 = split2end;
                        polysplits[poly] = polysplit;


                        newpolys.Add(poly1);
                        newpolys.Add(poly2);
                    }
                }
                else
                {
                    //no need to split
                    newpolys.Add(poly);
                }
            }


            foreach (var polysplit in polysplits.Values) //build new edges for split polys
            {
                //the two edges that were split each need to be turned into two new edges (1 for each poly).
                //also, the split itself needs to be added as a new edge to the original poly.

                var poly = polysplit.Orig;
                var poly1 = polysplit.New1;
                var poly2 = polysplit.New2;
                var edges = poly.Edges;
                var verts = poly.Vertices;
                var ec = edges?.Length ?? 0;
                if (ec <= 0)
                { continue; }//shouldnt happen - no edges?
                if (ec != poly.Vertices?.Length)
                { continue; }//shouldnt happen

                var split1beg = polysplit.Split1 - 1;
                var split1end = polysplit.Split1;
                var split2beg = polysplit.Split2 - 1;
                var split2end = polysplit.Split2;

                edges1.Clear();
                edges2.Clear();

                var se1 = edges[split1beg]; //the two original edges that got split 
                var se2 = edges[split2beg];
                var sp1 = TryGetSplit(polysplits, se1.Poly1);//could use Poly2, but they should be the same..
                var sp2 = TryGetSplit(polysplits, se2.Poly1);
                var sv1a = verts[split1beg];
                var sv2a = verts[split2beg];
                var sp1a = sp1?.GetNearest(sv1a);
                var sp1b = sp1?.GetOther(sp1a);
                var sp2b = sp2?.GetNearest(sv2a);
                var sp2a = sp2?.GetOther(sp2b);
                var edge1a = new YnvEdge(se1, sp1a);
                var edge1b = new YnvEdge(se1, sp1b);
                var edge2a = new YnvEdge(se2, sp2a);
                var edge2b = new YnvEdge(se2, sp2b);
                var splita = new YnvEdge(se1, poly2);
                var splitb = new YnvEdge(se1, poly1);

                for (int i = 0; i < split1beg; i++) edges1.Add(edges[i]);//untouched edges
                edges1.Add(edge1a);
                edges1.Add(splita);
                edges1.Add(edge2a);
                for (int i = split2end; i < ec; i++) edges1.Add(edges[i]);//untouched edges

                edges2.Add(edge1b);
                for (int i = split1end; i < split2beg; i++) edges2.Add(edges[i]);//untouched edges
                edges2.Add(edge2b);
                edges2.Add(splitb);


                poly1.Edges = edges1.ToArray();
                poly2.Edges = edges2.ToArray();

                if (poly1.Edges.Length != poly1.Vertices.Length)
                { }//debug
                if (poly2.Edges.Length != poly2.Vertices.Length)
                { }//debug

            }

            foreach (var poly in newpolys) //fix any untouched edges that joined to split polys
            {
                if (poly.Edges?.Length != poly.Vertices?.Length)
                { continue; }//shouldnt happen (no edges?)
                for (int i = 0; i < poly.Edges.Length; i++)
                {
                    var edge = poly.Edges[i];
                    var vert = poly.Vertices[i];
                    if (edge == null)
                    { continue; }//shouldnt happen
                    if (edge.Poly1 != edge.Poly2)
                    { continue; }//shouldnt happen?
                    if (edge.Poly1 == null)
                    { continue; }//probably this edge joins to nothing


                    YnvPolySplit? polysplit;
                    if (polysplits.TryGetValue(edge.Poly1, out polysplit))
                    {
                        var newpoly = polysplit.GetNearest(vert);
                        if (newpoly == null)
                        { }//debug
                        edge.Poly1 = newpoly;
                        edge.Poly2 = newpoly;
                    }

                }
            }


            return newpolys;
        }

        private Vector3 GetSplitPos(Vector3 a, Vector3 b, bool xaxis)
        {
            Vector3 ca = NavGrid.GetCellRel(a);
            Vector3 cb = NavGrid.GetCellRel(b);
            float fa = xaxis ? ca.X : ca.Y;
            float fb = xaxis ? cb.X : cb.Y;
            float f = 0;
            if (fb > fa)
            {
                float ib = (float)Math.Floor(fb);
                f = (ib - fa) / (fb - fa);
            }
            else
            {
                float ia = (float)Math.Floor(fa);
                f = (fa - ia) / (fa - fb);
            }
            if (f < 0.0f)
            { }//debug
            if (f > 1.0f)
            { }//debug
            return a + (b - a) * Math.Min(Math.Max(f, 0.0f), 1.0f);
        }

        private bool IsValidSplit(Vector3 s1, Vector3 s2, Vector3 v1a, Vector3 v1b, Vector3 v2a, Vector3 v2b)
        {
            if (XYEqual(s1, s2)) return false;
            if (XYEqual(s1, v1a)) return false;
            if (XYEqual(s1, v1b)) return false;
            if (XYEqual(s2, v2a)) return false;
            if (XYEqual(s2, v2b)) return false;
            return true;
        }

        private bool XYEqual(Vector3 v1, Vector3 v2)
        {
            return ((v1.X == v2.X) && (v1.Y == v2.Y));
        }

        private class YnvPolySplit
        {
            public YnvPoly Orig;
            public YnvPoly New1;
            public YnvPoly New2;
            public int Split1;
            public int Split2;
            public YnvPoly GetNearest(Vector3 v)
            {
                if (New1?.Vertices == null) return New2;
                if (New2?.Vertices == null) return New1;
                float len1 = float.MaxValue;
                float len2 = float.MaxValue;
                for (int i = 0; i < New1.Vertices.Length; i++)
                {
                    len1 = Math.Min(len1, (v - New1.Vertices[i]).LengthSquared());
                }
                if (len1 == 0.0f) return New1;
                for (int i = 0; i < New2.Vertices.Length; i++)
                {
                    len2 = Math.Min(len2, (v - New2.Vertices[i]).LengthSquared());
                }
                if (len2 == 0.0f) return New2;
                return (len1 <= len2) ? New1 : New2;
            }
            public YnvPoly GetOther(YnvPoly p)
            {
                if (p == New1) return New2;
                return New1;
            }
        }
        private YnvPolySplit TryGetSplit(Dictionary<YnvPoly, YnvPolySplit> polysplits, YnvPoly? poly)
        {
            if (poly == null) return null;
            YnvPolySplit? r = null;
            polysplits.TryGetValue(poly, out r);
            return r;
        }



        private void AddPolysIntoGrid(List<YnvPoly> polys)
        {
            foreach (var poly in polys)
            {
                poly.CalculatePosition();
                var pos = poly.Position;
                var cell = NavGrid.GetCell(pos);

                var ynv = cell.Ynv;
                if (ynv == null)
                {
                    ynv = new YnvFile();
                    ynv.Name = "navmesh[" + cell.FileX.ToString() + "][" + cell.FileY.ToString() + "]";
                    ynv.Nav = new NavMesh();
                    ynv.Nav.SetDefaults(false);
                    ynv.Nav.AABBSize = new Vector3(NavGrid.CellSize, NavGrid.CellSize, 0.0f);
                    ynv.Nav.SectorTree = new NavMeshSector();
                    // R* sets initial Z bounds from exporter cutoffs: min=-500, max=1000
                    // These get refined in FinalizeYnvs to match actual content
                    var cellMin = NavGrid.GetCellMin(cell);
                    var cellMax = NavGrid.GetCellMax(cell);
                    ynv.Nav.SectorTree.AABBMin = new Vector4(cellMin.X, cellMin.Y, genParams.ExporterMinZCutoff, 0.0f);
                    ynv.Nav.SectorTree.AABBMax = new Vector4(cellMax.X, cellMax.Y, genParams.ExporterMaxZCutoff, 0.0f);
                    ynv.AreaID = cell.X + cell.Y * 100;
                    ynv.Polys = new List<YnvPoly>();
                    ynv.HasChanged = true;//mark it for the project window
                    ynv.RpfFileEntry = new RpfResourceFileEntry();
                    ynv.RpfFileEntry.Name = ynv.Name + ".ynv";
                    ynv.RpfFileEntry.Path = string.Empty;
                    cell.Ynv = ynv;
                    YnvFiles.Add(ynv);
                }

                poly.AreaID = (ushort)ynv.AreaID;
                poly.Index = ynv.Polys.Count;
                poly.Ynv = ynv;
                ynv.Polys.Add(poly);

            }
        }

        private void AddVehiclePolys(List<YnvPoly> polys)
        {
            var bbmin = new Vector3(float.MaxValue);
            var bbmax = new Vector3(float.MinValue);
            foreach (var poly in polys)
            {
                poly.CalculatePosition();
                var pos = poly.Position;
                var verts = poly.Vertices;
                if (verts != null)
                {
                    foreach (var vert in verts)
                    {
                        bbmin = Vector3.Min(bbmin, vert);
                        bbmax = Vector3.Max(bbmax, vert);
                    }
                }
            }
            var bbsize = bbmax - bbmin;

            var ynv = new YnvFile();
            ynv.Name = VehicleName;
            ynv.Nav = new NavMesh();
            ynv.Nav.SetDefaults(true);
            ynv.Nav.AABBSize = new Vector3(bbsize.X, bbsize.Y, 0.0f);
            ynv.Nav.SectorTree = new NavMeshSector();
            ynv.Nav.SectorTree.AABBMin = new Vector4(bbmin, 0.0f);
            ynv.Nav.SectorTree.AABBMax = new Vector4(bbmax, 0.0f);
            ynv.AreaID = 10000;
            ynv.Polys = new List<YnvPoly>();
            ynv.HasChanged = true;//mark it for the project window
            ynv.RpfFileEntry = new RpfResourceFileEntry();
            ynv.RpfFileEntry.Name = ynv.Name + ".ynv";
            ynv.RpfFileEntry.Path = string.Empty;
            YnvFiles.Add(ynv);

            foreach (var poly in polys)
            {
                poly.AreaID = (ushort)ynv.AreaID;
                poly.Index = ynv.Polys.Count;
                poly.Ynv = ynv;
                ynv.Polys.Add(poly);
            }
        }


        private void FinalizeYnvs(List<YnvFile> ynvs, bool vehicle)
        {
            const int MaxVerticesPerYnv = 65535; // Maximum vertices per YNV file
            const int MaxVerticesPerPoly = 8; // Maximum vertices per polygon

            foreach (var ynv in ynvs)
            {
                int totalVertexCount = 0;
                var polygonsToRemove = new List<YnvPoly>();

                foreach (var poly in ynv.Polys)
                {
                    if (poly.Vertices != null && poly.Vertices.Length > MaxVerticesPerPoly)
                    {
                        // Polygon has too many vertices, mark for removal
                        polygonsToRemove.Add(poly);
                        continue;
                    }

                    if (poly.Vertices != null)
                    {
                        totalVertexCount += poly.Vertices.Length;
                    }
                }

                // Remove polygons with too many vertices
                foreach (var poly in polygonsToRemove)
                {
                    ynv.Polys.Remove(poly);
                }

                // Adaptive vertex reduction if count exceeds limit
                if (totalVertexCount > MaxVerticesPerYnv)
                {
                    int excessVertices = totalVertexCount - MaxVerticesPerYnv;
                    float reductionPercentage = (float)excessVertices / totalVertexCount * 100f;

                    throw new InvalidOperationException(
                        $"YNV file {ynv.Name} exceeds maximum vertex count: {totalVertexCount} > {MaxVerticesPerYnv} " +
                        $"({excessVertices} excess vertices, {reductionPercentage:F1}% over limit).\n\n" +
                        "To fix this:\n" +
                        $"- Increase edge collapse optimization (current MaxQuadricErrorMetric: {genParams.MaxQuadricErrorMetric:F3})\n" +
                        $"  Suggested: {genParams.MaxQuadricErrorMetric * 2.0f:F3} or higher\n" +
                        "- Enable more aggressive polygon merging\n" +
                        "- Reduce sampling density (increase from current: " + genParams.SamplingDensity + "m)\n" +
                        "- Process a smaller area\n" +
                        "- Split generation into multiple smaller YNV files");
                }

                //find zmin and zmax and update AABBSize and SectorTree root
                float zmin = float.MaxValue;
                float zmax = float.MinValue;
                foreach (var poly in ynv.Polys)
                {
                    foreach (var vert in poly.Vertices)
                    {
                        zmin = Math.Min(zmin, vert.Z);
                        zmax = Math.Max(zmax, vert.Z);
                    }
                }
                var yn = ynv.Nav;
                var ys = yn.SectorTree;
                yn.AABBSize = new Vector3(yn.AABBSize.X, yn.AABBSize.Y, zmax - zmin);
                ys.AABBMin = new Vector4(ys.AABBMin.X, ys.AABBMin.Y, zmin, 0.0f);
                ys.AABBMax = new Vector4(ys.AABBMax.X, ys.AABBMax.Y, zmax, 0.0f);

                ynv.UpdateContentFlags(vehicle);

                foreach (var poly in ynv.Polys)
                {
                    if (poly.Edges != null && poly.Vertices != null)
                    {
                        // Check that edge count matches vertex count
                        if (poly.Edges.Length != poly.Vertices.Length)
                        {
                            throw new InvalidOperationException(
                                $"Polygon in YNV {ynv.Name} has mismatched edge/vertex count: " +
                                $"{poly.Edges.Length} edges vs {poly.Vertices.Length} vertices");
                        }

                        // Validate each edge has valid adjacency information
                        for (int i = 0; i < poly.Edges.Length; i++)
                        {
                            var edge = poly.Edges[i];
                            if (edge == null)
                            {
                                throw new InvalidOperationException(
                                    $"Polygon in YNV {ynv.Name} has null edge at index {i}");
                            }

                            // Check that edge references are consistent
                            if (edge.Poly1 != edge.Poly2 && edge.Poly1 != null && edge.Poly2 != null)
                            {
                                // Edge connects two different polygons - this shouldnt happen in finalized YNV
                                // unless its a cross-boundary edge
                                if (edge.Poly1.AreaID == edge.Poly2.AreaID)
                                {
                                    throw new InvalidOperationException(
                                        $"Polygon in YNV {ynv.Name} has invalid adjacency: " +
                                        "edge connects two polygons in same area but Poly1 != Poly2");
                                }
                            }
                        }
                    }
                }

                //fix up flags on edges that cross ynv borders
                foreach (var poly in ynv.Polys)
                {
                    bool border = false;
                    if (poly.Edges == null)
                    { continue; }
                    foreach (var edge in poly.Edges)
                    {
                        if (edge.Poly1 != null)
                        {
                            if (edge.Poly1.AreaID != poly.AreaID)
                            {
                                edge._RawData._Poly1.Unk2 = 0;//crash without this
                                edge._RawData._Poly2.Unk2 = 0;//crash without this
                                edge._RawData._Poly2.Unk3 = 4;////// edge._RawData._Poly2.Unk3 | 4;
                                border = true;

                                ////DEBUG dont join edges
                                //edge.Poly1 = null;
                                //edge.Poly2 = null;
                                //edge.AreaID1 = 0x3FFF;
                                //edge.AreaID2 = 0x3FFF;
                                //edge._RawData._Poly1.PolyID = 0x3FFF;
                                //edge._RawData._Poly2.PolyID = 0x3FFF;
                                //edge._RawData._Poly1.Unk2 = 1;
                                //edge._RawData._Poly2.Unk2 = 1;
                                //edge._RawData._Poly1.Unk3 = 0;
                                //edge._RawData._Poly2.Unk3 = 0;

                            }
                        }
                    }
                    poly.B19_IsCellEdge = border;
                }


            }

        }

        public bool LoadCollisionGeometry(GameFileCache gameFileCache, SpaceBoundsStore boundsStore, Vector2 min, Vector2 max, Action<string>? statusCallback = null)
        {
            Log("", statusCallback);
            Log("PHASE 1: LOADING COLLISION GEOMETRY", statusCallback);
            Log("=".PadRight(80, '='), statusCallback);
            
            if (gameFileCache == null || boundsStore == null)
            {
                Log("Error: GameFileCache or BoundsStore is null", statusCallback);
                return false;
            }

            collisionTriangles.Clear();

            // Ensure min is actually less than max (swap if needed)
            // This can happen if the UI passes coordinates in the wrong order
            if (min.X > max.X)
            {
                float temp = min.X;
                min.X = max.X;
                max.X = temp;
                Log($"WARNING: Swapped min.X and max.X (were reversed)", statusCallback);
            }
            if (min.Y > max.Y)
            {
                float temp = min.Y;
                min.Y = max.Y;
                max.Y = temp;
                Log($"WARNING: Swapped min.Y and max.Y (were reversed)", statusCallback);
            }

            Log($"Query bounds: X=[{min.X:F1}, {max.X:F1}], Y=[{min.Y:F1}, {max.Y:F1}]", statusCallback);

            // R* exporter Z cutoffs: min=-500, max=1000
            var bmin = new Vector3(min, genParams.ExporterMinZCutoff);
            var bmax = new Vector3(max, genParams.ExporterMaxZCutoff);
            var boundslist = boundsStore.GetItems(ref bmin, ref bmax);

            if (boundslist == null || boundslist.Count == 0)
            {
                statusCallback?.Invoke("Error: No collision geometry found in specified area");
                return false;
            }

            int loadedCount = 0;
            int failedCount = 0;
            int timeoutCount = 0;
            int triangleCount = 0;
            var failedFiles = new List<string>();

            // Load YBN files and extract triangles
            statusCallback?.Invoke($"Found {boundslist.Count} YBN files in area");
            
            foreach (var boundsitem in boundslist)
            {
                try
                {
                    string ybnName = boundsitem.Name.ToString();
                    statusCallback?.Invoke($"Processing YBN: {ybnName} at [{boundsitem.Min.X:F1}, {boundsitem.Min.Y:F1}, {boundsitem.Min.Z:F1}] to [{boundsitem.Max.X:F1}, {boundsitem.Max.Y:F1}, {boundsitem.Max.Z:F1}]");
                    
                    YbnFile ybn = gameFileCache.GetYbn(boundsitem.Name);
                    if (ybn == null)
                    {
                        statusCallback?.Invoke($"Warning: Could not load YBN {ybnName}");
                        failedFiles.Add(ybnName);
                        failedCount++;
                        continue;
                    }

                    if (!ybn.Loaded)
                    {
                        statusCallback?.Invoke($"Loading YBN: {boundsitem.Name}...");
                        int waitCount = 0;
                        const int maxWaitCount = 500; // 10 second timeout (500 * 20ms)
                        
                        while (!ybn.Loaded && waitCount < maxWaitCount)
                        {
                            System.Threading.Thread.Sleep(20);
                            waitCount++;
                            ybn = gameFileCache.GetYbn(boundsitem.Name); // Try to queue it again
                        }

                        if (!ybn.Loaded)
                        {
                            statusCallback?.Invoke($"Timeout: YBN {boundsitem.Name} failed to load within 10 seconds");
                            failedFiles.Add(ybnName);
                            timeoutCount++;
                            continue;
                        }
                    }

                    // Extract triangles from bounds
                    if (ybn.Loaded && ybn.Bounds != null)
                    {
                        try
                        {
                            // The bounds vertices are in local space relative to BoxCenter
                            // We need to apply the bounds transform (rotation/scale) and then translate to BoxCenter
                            var boxCenter = ybn.Bounds.BoxCenter;
                            var worldTransform = ybn.Bounds.Transform * Matrix.Translation(boxCenter);
                            
                            var tris = ExtractTrianglesFromBounds(ybn.Bounds, worldTransform);
                            
                            if (tris.Count > 0)
                            {
                                var firstTri = tris[0];
                                Log($"  First tri after transform: [{firstTri.Vertices[0].X:F1}, {firstTri.Vertices[0].Y:F1}, {firstTri.Vertices[0].Z:F1}]");
                            }
                            
                            collisionTriangles.AddRange(tris);
                            triangleCount += tris.Count;
                            loadedCount++;
                            
                            Log($"  -> Extracted {tris.Count} triangles from {ybnName}", statusCallback);

                            // Update Z bounds using world-space bounds
                            bmin.Z = Math.Min(bmin.Z, boundsitem.Min.Z);
                            bmax.Z = Math.Max(bmax.Z, boundsitem.Max.Z);
                        }
                        catch (Exception ex)
                        {
                            Log($"Error extracting triangles from {ybnName}: {ex.Message}", statusCallback);
                            failedFiles.Add(ybnName);
                            failedCount++;
                        }
                    }
                    else if (!ybn.Loaded)
                    {
                        statusCallback?.Invoke($"  -> YBN {ybnName} not loaded yet");
                    }
                    else if (ybn.Bounds == null)
                    {
                        statusCallback?.Invoke($"  -> YBN {ybnName} has no bounds");
                    }
                }
                catch (Exception ex)
                {
                    string ybnName = boundsitem.Name.ToString();
                    statusCallback?.Invoke($"Error loading YBN {ybnName}: {ex.Message}");
                    failedFiles.Add(ybnName);
                    failedCount++;
                }
            }

            // Report loading statistics
            Log("", statusCallback);
            Log($"SUMMARY: Loaded {loadedCount} YBN files with {triangleCount} triangles", statusCallback);
            if (failedCount > 0)
            {
                Log($"WARNING: {failedCount} YBN files failed to load", statusCallback);
            }
            if (timeoutCount > 0)
            {
                Log($"WARNING: {timeoutCount} YBN files timed out", statusCallback);
            }

            if (collisionTriangles.Count == 0)
            {
                statusCallback?.Invoke("Error: No collision triangles extracted from any YBN files");
                if (failedFiles.Count > 0)
                {
                    statusCallback?.Invoke($"Failed files: {string.Join(", ", failedFiles.Take(10))}");
                }
                return false;
            }

            // Build octree from collision triangles
            try
            {
                Log($"Building octree with bounds: [{bmin.X:F1}, {bmin.Y:F1}, {bmin.Z:F1}] to [{bmax.X:F1}, {bmax.Y:F1}, {bmax.Z:F1}]", statusCallback);
                BuildOctree(bmin, bmax);
                
                if (collisionOctree == null)
                {
                    Log("Error: Octree construction failed", statusCallback);
                    return false;
                }
                
                Log($"Octree built successfully with {collisionTriangles.Count} triangles", statusCallback);
                
                // Test a single ray cast to verify octree is working
                var testOrigin = new Vector3((bmin.X + bmax.X) * 0.5f, (bmin.Y + bmax.Y) * 0.5f, bmax.Z + 10.0f);
                var testDir = new Vector3(0, 0, -1);
                Log($"Test ray: origin=[{testOrigin.X:F1}, {testOrigin.Y:F1}, {testOrigin.Z:F1}], dir=[{testDir.X}, {testDir.Y}, {testDir.Z}], maxDist=1000", statusCallback);
                
                var testResult = collisionOctree.RayIntersect(testOrigin, testDir, 1000.0f);
                Log($"Test ray result: Hit={testResult.Hit}, Distance={testResult.Distance:F2}", statusCallback);
                
                if (!testResult.Hit)
                {
                    Log("WARNING: Test ray cast missed! This indicates the octree may not contain geometry in the expected location.", statusCallback);
                    
                    // Try to find where the geometry actually is
                    if (collisionTriangles.Count > 0)
                    {
                        var firstTri = collisionTriangles[0];
                        Log($"First triangle vertices: [{firstTri.Vertices[0].X:F1}, {firstTri.Vertices[0].Y:F1}, {firstTri.Vertices[0].Z:F1}], " +
                            $"[{firstTri.Vertices[1].X:F1}, {firstTri.Vertices[1].Y:F1}, {firstTri.Vertices[1].Z:F1}], " +
                            $"[{firstTri.Vertices[2].X:F1}, {firstTri.Vertices[2].Y:F1}, {firstTri.Vertices[2].Z:F1}]", statusCallback);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error building octree: {ex.Message}", statusCallback);
                return false;
            }

            return true;
        }

        private List<NavGenTri> ExtractTrianglesFromBounds(Bounds bounds, Matrix? worldTransform = null)
        {
            var triangles = new List<NavGenTri>();

            if (bounds == null) return triangles;

            // Use the provided world transform, or just the bounds transform if none provided
            var transform = worldTransform ?? bounds.Transform;

            // Handle different bound types
            if (bounds is BoundGeometry geom)
            {
                ExtractTrianglesFromGeometry(geom, triangles, transform);
            }
            else if (bounds is BoundBVH bvh)
            {
                ExtractTrianglesFromGeometry(bvh, triangles, transform);
            }
            else if (bounds is BoundComposite composite)
            {
                // Recursively extract from child bounds with accumulated transform
                if (composite.Children?.data_items != null)
                {
                    foreach (var child in composite.Children.data_items)
                    {
                        if (child != null)
                        {
                            var childTris = ExtractTrianglesFromBounds(child, worldTransform);
                            triangles.AddRange(childTris);
                        }
                    }
                }
            }

            return triangles;
        }

        private void ExtractTrianglesFromGeometry(BoundGeometry geom, List<NavGenTri> triangles, Matrix transform)
        {
            if (geom.Polygons == null) return;

            foreach (var poly in geom.Polygons)
            {
                if (poly == null) continue;

                // Only process triangles for now
                if (poly is BoundPolygonTriangle triPoly)
                {
                    var tri = new NavGenTri();
                    
                    // Use the polygons Vertex properties which already return world-space positions
                    tri.Vertices[0] = triPoly.Vertex1;
                    tri.Vertices[1] = triPoly.Vertex2;
                    tri.Vertices[2] = triPoly.Vertex3;

                    // Calculate normal
                    var edge1 = tri.Vertices[1] - tri.Vertices[0];
                    var edge2 = tri.Vertices[2] - tri.Vertices[0];
                    tri.Normal = Vector3.Normalize(Vector3.Cross(edge1, edge2));

                    // Get material information
                    if (geom.PolygonMaterialIndices != null && triPoly.Index < geom.PolygonMaterialIndices.Length)
                    {
                        byte matIndex = geom.PolygonMaterialIndices[triPoly.Index];
                        if (geom.Materials != null && matIndex < geom.Materials.Length)
                        {
                            var material = geom.Materials[matIndex];

                            tri.ProceduralId = material.ProceduralId;
                            tri.RoomId = material.RoomId;
                            tri.PedDensity = material.PedDensity;
                            tri.MaterialFlags = material.Flags;

                            // Map material type based on material properties and flags
                            tri.Material = MaterialType.Default;

                            // Check FLAGS first (higher priority)
                            if ((material.Flags & EBoundMaterialFlags.FLAG_STAIRS) != 0)
                            {
                                tri.Material = MaterialType.Stairs;
                            }
                            // Check for water material (material type 4 is typically water)
                            else if (material.Type == 4)
                            {
                                tri.Material = MaterialType.Water;
                                tri.IsWater = true;

                                // Check for shallow water flag
                                if (material.Type.Index == 12) // Specific shallow water material
                                {
                                    tri.IsShallowWater = true;
                                }
                            }
                            // Check for pavement/concrete materials (types 1, 2, 3 are typically hard surfaces)
                            else if (material.Type == 1 || material.Type == 2 || material.Type == 3)
                            {
                                tri.Material = MaterialType.Pavement;
                            }
                            // Check for stairs based on material type (material type 5 is often used for stairs/steps)
                            else if (material.Type == 5)
                            {
                                tri.Material = MaterialType.Stairs;
                            }

                            if (material.Type.Index >= 7 && material.Type.Index <= 10) // Road material types
                            {
                                tri.IsRoad = true;
                            }

                            // IsTrainTracks: Check if this is a train track surface
                            if (material.Type.Index == 55 || material.Type.Index == 56) // Metal track materials
                            {
                                tri.IsTrainTracks = true;
                            }

                            // IsInterior: Extract from RoomId (non-zero RoomId indicates interior)
                            tri.IsInterior = (material.RoomId > 0);
                        }
                    }

                    triangles.Add(tri);
                }
            }
        }

        private void BuildOctree(Vector3 min, Vector3 max)
        {
            if (collisionTriangles.Count == 0)
            {
                collisionOctree = null;
                return;
            }

            collisionOctree = new NavOctree();
            collisionOctree.Build(collisionTriangles, min, max);
        }

        public NavOctree.RayIntersectResult RayIntersect(Vector3 origin, Vector3 direction, float maxDistance = float.MaxValue)
        {
            if (collisionOctree == null)
            {
                return new NavOctree.RayIntersectResult { Hit = false };
            }

            return collisionOctree.RayIntersect(origin, direction, maxDistance);
        }

        public List<NavGenTri> GetTrianglesInBounds(Vector3 min, Vector3 max)
        {
            if (collisionOctree == null)
            {
                return new List<NavGenTri>();
            }

            return collisionOctree.GetTrianglesInBounds(min, max);
        }

        public void SetGenerationParams(NavGenParams parameters)
        {
            genParams = parameters ?? new NavGenParams();
        }

        public void InitializeLogFile(string? logPath = null)
        {
            try
            {
                if (string.IsNullOrEmpty(logPath))
                {
                    logPath = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "CodeWalker",
                        $"NavMeshGen_{DateTime.Now:yyyyMMdd_HHmmss}.log"
                    );
                }

                logFilePath = logPath;
                var directory = System.IO.Path.GetDirectoryName(logPath);
                if (!System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                logWriter = new System.IO.StreamWriter(logPath, false);
                logWriter.AutoFlush = true;
                
                Log($"NavMesh Generation Log - {DateTime.Now}");
                Log($"Log file: {logPath}");
                Log("=".PadRight(80, '='));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize log file: {ex.Message}");
            }
        }

        public void CloseLogFile()
        {
            if (logWriter != null)
            {
                Log("=".PadRight(80, '='));
                Log($"Log completed at {DateTime.Now}");
                logWriter.Close();
                logWriter = null;
            }
        }

        private void Log(string message, Action<string>? statusCallback = null)
        {
            if (logWriter != null)
            {
                logWriter.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
            }

            System.Diagnostics.Debug.WriteLine(message);
            statusCallback?.Invoke(message);
        }

        public int PerformHeightSampling(Vector2 min, Vector2 max, float minZ, float maxZ, Action<string>? statusCallback = null)
        {
            Log("", statusCallback);
            Log("PHASE 2: HEIGHT SAMPLING", statusCallback);
            Log("=".PadRight(80, '='), statusCallback);

            if (collisionOctree == null)
            {
                Log("Error: Collision octree not built. Call LoadCollisionGeometry first.", statusCallback);
                return 0;
            }

            // Ensure min < max on all axes
            if (min.X > max.X) { float t = min.X; min.X = max.X; max.X = t; }
            if (min.Y > max.Y) { float t = min.Y; min.Y = max.Y; max.Y = t; }
            if (minZ > maxZ) { float t = minZ; minZ = maxZ; maxZ = t; }

            // Calculate grid dimensions based on sampling density
            float samplingDensity = genParams.SamplingDensity;
            int gridWidth = (int)Math.Ceiling((max.X - min.X) / samplingDensity) + 1;
            int gridHeight = (int)Math.Ceiling((max.Y - min.Y) / samplingDensity) + 1;

            Log($"Creating sampling grid: {gridWidth} x {gridHeight} = {gridWidth * gridHeight} samples", statusCallback);
            Log($"Bounds: X=[{min.X:F1}, {max.X:F1}], Y=[{min.Y:F1}, {max.Y:F1}], Z=[{minZ:F1}, {maxZ:F1}]", statusCallback);
            Log($"Collision triangles available: {collisionTriangles.Count}", statusCallback);
            Log($"Sampling density: {samplingDensity:F2} meters", statusCallback);

            // Initialize the spatial grid for storing height samples
            var gridMin = new Vector3(min.X, min.Y, minZ);
            var gridMax = new Vector3(max.X, max.Y, maxZ);
            heightSampleGrid = new CPlacedNodeMultiMap();
            heightSampleGrid.Initialize(gridMin, gridMax, samplingDensity);

            int totalSamples = 0;
            int processedCells = 0;
            int totalCells = gridWidth * gridHeight;
            int totalRayCasts = 0;
            int totalHits = 0;

            // Cast rays downward from each grid position
            for (int gx = 0; gx < gridWidth; gx++)
            {
                for (int gy = 0; gy < gridHeight; gy++)
                {
                    // Calculate world position for this grid cell
                    float worldX = min.X + gx * samplingDensity;
                    float worldY = min.Y + gy * samplingDensity;

                    if (genParams.JitterSamples)
                    {
                        // Use deterministic random based on grid position for reproducibility
                        var random = new Random(gx * 10007 + gy * 10009); // Prime numbers for good distribution
                        float jitterX = ((float)random.NextDouble() - 0.5f) * samplingDensity * genParams.JitterAmount;
                        float jitterY = ((float)random.NextDouble() - 0.5f) * samplingDensity * genParams.JitterAmount;
                        worldX += jitterX;
                        worldY += jitterY;
                    }

                    // Start ray from top of the area
                    var rayOrigin = new Vector3(worldX, worldY, maxZ + 10.0f);
                    var rayDirection = new Vector3(0, 0, -1); // Downward (normalized)

                    // Cast ray and collect all intersections at different Z levels
                    float currentZ = maxZ + 10.0f;
                    int samplesAtThisXY = 0;
                    float lastSampleZ = float.MaxValue;

                    while (currentZ > minZ && samplesAtThisXY < 15) // Limit to 15 samples per XY to avoid infinite loops
                    {
                        rayOrigin.Z = currentZ;
                        float rayDistance = currentZ - minZ + 10.0f; // Add extra distance to ensure we reach minZ
                        
                        totalRayCasts++;
                        var result = RayIntersect(rayOrigin, rayDirection, rayDistance);

                        if (result.Hit && result.Triangle != null)
                        {
                            totalHits++;
                            
                            // Filter out surfaces that are too close to previous sample (likely same surface)
                            if (lastSampleZ != float.MaxValue && Math.Abs(result.Position.Z - lastSampleZ) < 0.1f)
                            {
                                currentZ = result.Position.Z - genParams.MinZDistBetweenSamples;
                                continue;
                            }
                            
                            // Filter out steep surfaces (likely walls, not walkable ground)
                            var upVector = new Vector3(0, 0, 1);
                            float normalDot = Vector3.Dot(result.Triangle.Normal, upVector);
                            float surfaceAngle = (float)Math.Acos(Math.Clamp(normalDot, -1f, 1f)) * 180f / (float)Math.PI;
                            
                            // Skip surfaces steeper than 60 degrees (likely walls)
                            if (surfaceAngle > 60.0f)
                            {
                                currentZ = result.Position.Z - genParams.MinZDistBetweenSamples;
                                continue;
                            }
                            
                            // Check if we already have a sample at this Z level (within tolerance)
                            var existingNode = heightSampleGrid.GetNode(result.Position, genParams.MinZDistBetweenSamples, samplingDensity * 0.1f);

                            if (existingNode == null)
                            {
                                // Create new height sample node - PROPAGATE ALL FLAGS from collision triangle
                                var node = new NavGenNode
                                {
                                    BasePosition = result.Position,
                                    CollisionTriangle = result.Triangle,
                                    Material = result.Triangle.Material,
                                    IsWater = result.Triangle.IsWater,
                                    Flags = 0,

                                    // Propagate extended flags from collision material
                                    ProceduralId = result.Triangle.ProceduralId,
                                    RoomId = result.Triangle.RoomId,
                                    PedDensity = result.Triangle.PedDensity,
                                    MaterialFlags = result.Triangle.MaterialFlags,
                                    IsInterior = result.Triangle.IsInterior,
                                    IsRoad = result.Triangle.IsRoad,
                                    IsTrainTracks = result.Triangle.IsTrainTracks
                                };

                                // Set flags based on material and surface properties
                                if (result.Triangle.IsWater)
                                {
                                    node.Flags |= 0x01; // Water flag
                                }

                                // Set flags for pavement
                                if (result.Triangle.Material == MaterialType.Pavement)
                                {
                                    node.Flags |= 0x02; // Pavement flag
                                }

                                // Set flags for stairs
                                if (result.Triangle.Material == MaterialType.Stairs)
                                {
                                    node.Flags |= 0x04; // Stairs flag
                                }

                                // Add to spatial grid
                                heightSampleGrid.AddNode(node);
                                totalSamples++;
                                samplesAtThisXY++;
                                lastSampleZ = result.Position.Z;
                            }

                            // Move ray down for next intersection (step by minimum Z distance)
                            currentZ = result.Position.Z - genParams.MinZDistBetweenSamples;
                        }
                        else
                        {
                            break;
                        }
                    }

                    processedCells++;

                    if (processedCells % 1000 == 0)
                    {
                        float progress = (float)processedCells / totalCells * 100f;
                        float hitRate = totalRayCasts > 0 ? (totalHits * 100.0f / totalRayCasts) : 0;
                        statusCallback?.Invoke($"Height sampling: {progress:F1}% ({totalSamples} samples, {totalHits}/{totalRayCasts} hits = {hitRate:F1}%)");
                    }
                    
                    // Log first few cells for debugging
                    if (processedCells <= 5)
                    {
                        statusCallback?.Invoke($"  Cell [{gx},{gy}] at ({worldX:F1}, {worldY:F1}): {samplesAtThisXY} samples found");
                    }
                }
            }

            statusCallback?.Invoke($"Height sampling complete: {totalSamples} samples created from {gridWidth}x{gridHeight} grid");
            statusCallback?.Invoke($"Ray casting stats: {totalRayCasts} casts, {totalHits} hits ({(totalRayCasts > 0 ? (totalHits * 100.0f / totalRayCasts) : 0):F1}% hit rate)");

            return totalSamples;
        }

        public CPlacedNodeMultiMap GetHeightSampleGrid()
        {
            return heightSampleGrid;
        }

        public List<NavSurfaceTri> GetSurfaceTriangles()
        {
            return surfaceTriangles;
        }

        public List<NavSurfacePoly> GetSurfacePolygons()
        {
            return surfacePolygons;
        }

        public List<NavSurfacePoly> ConvertTrianglesToPolygons(Action<string>? statusCallback = null)
        {
            if (surfaceTriangles == null || surfaceTriangles.Count == 0)
            {
                statusCallback?.Invoke("Error: No surface triangles available. Call PerformTriangulation first.");
                return new List<NavSurfacePoly>();
            }

            statusCallback?.Invoke($"Converting {surfaceTriangles.Count} triangles to polygons (no merging)...");

            var polygons = new List<NavSurfacePoly>();

            foreach (var tri in surfaceTriangles)
            {
                if (tri.IsRemoved) continue;

                // Create a polygon from this triangle - PRESERVE ALL FLAGS
                var poly = new NavSurfacePoly
                {
                    Vertices = new List<NavGenNode>(tri.Nodes),
                    Material = tri.Material,
                    PolyFlags = tri.PolyFlags,
                    IsWater = tri.IsWater,
                    IsTooSteep = tri.IsTooSteep,

                    // Preserve extended flags from triangle
                    ProceduralId = tri.ProceduralId,
                    RoomId = tri.RoomId,
                    PedDensity = tri.PedDensity,
                    MaterialFlags = tri.MaterialFlags,
                    IsInterior = tri.IsInterior,
                    IsRoad = tri.IsRoad,
                    IsTrainTracks = tri.IsTrainTracks,
                    IsFlatGround = tri.IsFlatGround,
                    IsShallowWater = tri.IsShallowWater
                };

                polygons.Add(poly);
            }

            surfacePolygons = polygons;
            statusCallback?.Invoke($"Created {polygons.Count} polygons from triangles (no merging applied)");

            return polygons;
        }

        public int PerformTriangulation(Action<string>? statusCallback = null)
        {
            if (heightSampleGrid == null)
            {
                statusCallback?.Invoke("Error: Height sample grid not created. Call PerformHeightSampling first.");
                return 0;
            }

            statusCallback?.Invoke("Starting triangulation...");

            var triangles = new List<NavSurfaceTri>();
            int processedNodes = 0;
            int totalNodes = 0;

            // Count total nodes for progress reporting
            for (int gx = 0; gx < heightSampleGrid.GridWidth; gx++)
            {
                for (int gy = 0; gy < heightSampleGrid.GridHeight; gy++)
                {
                    totalNodes += heightSampleGrid.GetNodesAt(gx, gy).Count;
                }
            }

            statusCallback?.Invoke($"Triangulating {totalNodes} height samples...");

            for (int gx = 0; gx < heightSampleGrid.GridWidth; gx++)
            {
                for (int gy = 0; gy < heightSampleGrid.GridHeight; gy++)
                {
                    var nodes = heightSampleGrid.GetNodesAt(gx, gy);

                    foreach (var node in nodes)
                    {
                        if (node.IsRemoved) continue;

                        TryCreateTriangle(node, 1, 0, 1, 1, triangles);
                        TryCreateTriangle(node, 1, 1, 0, 1, triangles);

                        processedNodes++;

                        if (processedNodes % 1000 == 0)
                        {
                            float progress = (float)processedNodes / totalNodes * 100f;
                            statusCallback?.Invoke($"Triangulation: {progress:F1}% ({triangles.Count} triangles created)");
                        }
                    }
                }
            }

            // Establish adjacency relationships between triangles
            statusCallback?.Invoke("Establishing triangle adjacency...");
            EstablishTriangleAdjacency(triangles);

            // Mark steep triangles
            statusCallback?.Invoke("Marking steep triangles...");
            MarkSteepTriangles(triangles);

            // Store triangles for later phases
            surfaceTriangles = triangles;

            statusCallback?.Invoke($"Triangulation complete: {triangles.Count} triangles created");

            return triangles.Count;
        }

        private void TryCreateTriangle(NavGenNode node, int dx1, int dy1, int dx2, int dy2, List<NavSurfaceTri> triangles)
        {
            // Find adjacent nodes
            var adj1 = heightSampleGrid.GetAdjacentNode(node, dx1, dy1, genParams.TriangulationMaxHeightDiff);
            var adj2 = heightSampleGrid.GetAdjacentNode(node, dx2, dy2, genParams.TriangulationMaxHeightDiff);

            if (adj1 == null || adj2 == null)
                return;

            if (adj1.IsRemoved || adj2.IsRemoved)
                return;

            // Check height differences
            float heightDiff1 = Math.Abs(adj1.BasePosition.Z - node.BasePosition.Z);
            float heightDiff2 = Math.Abs(adj2.BasePosition.Z - node.BasePosition.Z);
            float heightDiff12 = Math.Abs(adj2.BasePosition.Z - adj1.BasePosition.Z);

            if (heightDiff1 > genParams.TriangulationMaxHeightDiff ||
                heightDiff2 > genParams.TriangulationMaxHeightDiff ||
                heightDiff12 > genParams.TriangulationMaxHeightDiff)
            {
                return;
            }

            // Perform line-of-sight tests to ensure no obstacles between samples
            if (!LineOfSightTest(node.BasePosition, adj1.BasePosition))
                return;

            if (!LineOfSightTest(node.BasePosition, adj2.BasePosition))
                return;

            if (!LineOfSightTest(adj1.BasePosition, adj2.BasePosition))
                return;

            if (!IsLineFreeOfSuddenHeightChanges(node.BasePosition, adj1.BasePosition, out float heightChange1))
                return;

            if (!IsLineFreeOfSuddenHeightChanges(node.BasePosition, adj2.BasePosition, out float heightChange2))
                return;

            if (!IsLineFreeOfSuddenHeightChanges(adj1.BasePosition, adj2.BasePosition, out float heightChange12))
                return;

            // Create the triangle
            var triangle = new NavSurfaceTri
            {
                Nodes = new NavGenNode[] { node, adj1, adj2 }
            };

            triangle.CalculateNormal();
            if (genParams.TestClearHeight > 0.01f)
            {
                if (!HasClearHeightAboveTriangle(triangle.Nodes, genParams.TestClearHeight))
                {
                    // Not enough clearance, skip this triangle
                    return;
                }
            }

            float area = triangle.CalculateArea();
            if (area < genParams.MinTriangleArea)
            {
                // Triangle is degenerate (too small), skip it
                return;
            }

            // Set material from nodes (use most common material)
            // Priority: Water > Stairs > Pavement > Slope > Default
            if (node.IsWater || adj1.IsWater || adj2.IsWater)
            {
                triangle.Material = MaterialType.Water;
                triangle.IsWater = true;

                // Check if any source collision triangle was shallow water
                if (node.CollisionTriangle?.IsShallowWater == true ||
                    adj1.CollisionTriangle?.IsShallowWater == true ||
                    adj2.CollisionTriangle?.IsShallowWater == true)
                {
                    triangle.IsShallowWater = true;
                }
            }
            else if (node.Material == MaterialType.Stairs || adj1.Material == MaterialType.Stairs || adj2.Material == MaterialType.Stairs)
            {
                triangle.Material = MaterialType.Stairs;
            }
            else if (node.Material == MaterialType.Pavement || adj1.Material == MaterialType.Pavement || adj2.Material == MaterialType.Pavement)
            {
                triangle.Material = MaterialType.Pavement;
            }
            else if (node.Material == MaterialType.Slope || adj1.Material == MaterialType.Slope || adj2.Material == MaterialType.Slope)
            {
                triangle.Material = MaterialType.Slope;
            }
            else
            {
                triangle.Material = MaterialType.Default;
            }

            // Use priority node (same as material priority: water > stairs > pavement > slope > default)
            NavGenNode priorityNode = node;
            if (node.IsWater || (adj1.IsWater && !node.Material.Equals(MaterialType.Water)) || (adj2.IsWater && !node.Material.Equals(MaterialType.Water)))
            {
                priorityNode = node.IsWater ? node : (adj1.IsWater ? adj1 : adj2);
            }
            else if (node.Material == MaterialType.Stairs || adj1.Material == MaterialType.Stairs || adj2.Material == MaterialType.Stairs)
            {
                priorityNode = (node.Material == MaterialType.Stairs) ? node : ((adj1.Material == MaterialType.Stairs) ? adj1 : adj2);
            }
            else if (node.Material == MaterialType.Pavement || adj1.Material == MaterialType.Pavement || adj2.Material == MaterialType.Pavement)
            {
                priorityNode = (node.Material == MaterialType.Pavement) ? node : ((adj1.Material == MaterialType.Pavement) ? adj1 : adj2);
            }

            triangle.ProceduralId = priorityNode.ProceduralId;
            triangle.RoomId = priorityNode.RoomId;
            triangle.PedDensity = priorityNode.PedDensity;
            triangle.MaterialFlags = priorityNode.MaterialFlags;
            triangle.IsInterior = priorityNode.IsInterior || adj1.IsInterior || adj2.IsInterior; // Any interior node marks triangle as interior
            triangle.IsRoad = priorityNode.IsRoad;
            triangle.IsTrainTracks = priorityNode.IsTrainTracks;

            // Determine IsFlatGround based on slope
            var upVector = new Vector3(0, 0, 1);
            float normalDot = Vector3.Dot(triangle.Normal, upVector);
            float surfaceAngle = (float)Math.Acos(Math.Clamp(normalDot, -1f, 1f)) * 180f / (float)Math.PI;
            triangle.IsFlatGround = (surfaceAngle < 10.0f); // Less than 10 degrees is considered flat

            // Add triangle to nodes surrounding triangles list
            node.SurroundingTriangles.Add(triangle);
            adj1.SurroundingTriangles.Add(triangle);
            adj2.SurroundingTriangles.Add(triangle);

            triangles.Add(triangle);
        }

        private bool LineOfSightTest(Vector3 from, Vector3 to)
        {
            // Calculate direction and distance
            var direction = to - from;
            var distance = direction.Length();

            if (distance < 0.01f)
                return true; // Too close, consider it valid

            direction.Normalize();

            // For sloped surfaces, we need to be more lenient
            // Calculate the expected slope between the two points
            float heightDiff = Math.Abs(to.Z - from.Z);
            float horizontalDist = new Vector2(to.X - from.X, to.Y - from.Y).Length();
            float slopeAngle = horizontalDist > 0.01f ? (float)Math.Atan(heightDiff / horizontalDist) * 180f / (float)Math.PI : 0f;

            // If this is a steep slope, use a more lenient test
            bool isSteepSlope = slopeAngle > 20.0f;

            // Cast ray from slightly above the from position
            var testOrigin = from + new Vector3(0, 0, genParams.HeightAboveNodeBase);
            var testDirection = to - testOrigin;
            var testDistance = testDirection.Length();
            testDirection.Normalize();

            // Check if ray hits any geometry between the two points
            var result = RayIntersect(testOrigin, testDirection, testDistance);

            if (!result.Hit)
                return true; // No obstacle

            // Calculate the expected Z at the hit point along the line from->to
            float t = 0f;
            if (Math.Abs(to.X - from.X) > 0.001f)
            {
                t = (result.Position.X - from.X) / (to.X - from.X);
            }
            else if (Math.Abs(to.Y - from.Y) > 0.001f)
            {
                t = (result.Position.Y - from.Y) / (to.Y - from.Y);
            }
            else
            {
                // Vertical connection, use Z
                t = Math.Abs(to.Z - from.Z) > 0.001f ? (result.Position.Z - from.Z) / (to.Z - from.Z) : 0.5f;
            }

            t = Math.Clamp(t, 0f, 1f);
            var expectedZ = from.Z + (to.Z - from.Z) * t;
            var actualHeightDiff = Math.Abs(result.Position.Z - expectedZ);

            // Use a more lenient threshold for steep slopes
            float threshold = isSteepSlope ? genParams.MaxHeightChangeUnderEdge * 2.0f : genParams.MaxHeightChangeUnderEdge;

            // Also check if the hit is very close to either endpoint (likely the surface were sampling)
            float distToFrom = (result.Position - from).Length();
            float distToTo = (result.Position - to).Length();
            bool nearEndpoint = distToFrom < 0.2f || distToTo < 0.2f;

            if (nearEndpoint)
            {
                // Hit is near an endpoint, likely the surface itself - allow it
                return true;
            }

            return actualHeightDiff <= threshold;
        }

        private bool HasClearHeightAboveTriangle(NavGenNode[]? nodes, float clearHeight)
        {
            if (nodes == null || nodes.Length < 3)
                return false;

            // Test clearance at triangle centroid
            var centroid = (nodes[0].BasePosition + nodes[1].BasePosition + nodes[2].BasePosition) / 3f;

            // Cast ray upward from centroid to check for obstacles
            var rayOrigin = centroid + new Vector3(0, 0, 0.1f); // Start slightly above surface
            var rayDir = new Vector3(0, 0, 1); // Upward
            var result = RayIntersect(rayOrigin, rayDir, clearHeight);

            // If ray hits something within the clearance height, triangle is blocked
            if (result.Hit && result.Distance < clearHeight)
            {
                return false; // Obstacle above (ceiling, overhang, etc.)
            }

            return true; // Clear height confirmed
        }

        private bool IsLineFreeOfSuddenHeightChanges(Vector3 from, Vector3 to, out float maxHeightChange)
        {
            maxHeightChange = 0f;

            float horizontalDist = new Vector2(to.X - from.X, to.Y - from.Y).Length();

            if (horizontalDist < 0.01f)
            {
                // Vertical edge, no horizontal stepping needed
                return true;
            }

            // Step along edge with fine granularity
            float stepSize = 0.25f;
            int numSteps = (int)Math.Ceiling(horizontalDist / stepSize);

            if (numSteps < 2)
                numSteps = 2; // At minimum check start and end

            float previousZ = from.Z;
            bool firstSample = true;

            for (int step = 0; step <= numSteps; step++)
            {
                float t = (float)step / numSteps;
                Vector3 testPos = Vector3.Lerp(from, to, t);

                // Cast ray down from above to find ground height at this position
                var rayOrigin = new Vector3(testPos.X, testPos.Y, testPos.Z + 2.0f);
                var rayDir = new Vector3(0, 0, -1);
                var result = RayIntersect(rayOrigin, rayDir, 10.0f);

                if (result.Hit)
                {
                    if (!firstSample)
                    {
                        // Check height difference from previous sample
                        float heightChange = Math.Abs(result.Position.Z - previousZ);
                        maxHeightChange = Math.Max(maxHeightChange, heightChange);

                        // If height change exceeds threshold, this edge crosses a low wall/obstacle
                        if (heightChange > genParams.MaxHeightChangeUnderEdge)
                        {
                            return false; // Sudden drop/rise detected
                        }
                    }

                    previousZ = result.Position.Z;
                    firstSample = false;
                }
            }

            return true;
        }

        private void EstablishTriangleAdjacency(List<NavSurfaceTri> triangles)
        {
            // Build edge dictionary for fast lookups
            var edgeToTriangles = new Dictionary<(NavGenNode, NavGenNode), List<NavSurfaceTri>>();

            foreach (var tri in triangles)
            {
                if (tri.IsRemoved) continue;

                // Add all three edges
                for (int i = 0; i < 3; i++)
                {
                    var n1 = tri.Nodes[i];
                    var n2 = tri.Nodes[(i + 1) % 3];

                    // Create edge key (order independent)
                    var edgeKey = n1.GetHashCode() < n2.GetHashCode() ? (n1, n2) : (n2, n1);

                    if (!edgeToTriangles.ContainsKey(edgeKey))
                    {
                        edgeToTriangles[edgeKey] = new List<NavSurfaceTri>();
                    }

                    edgeToTriangles[edgeKey].Add(tri);
                }
            }

            // Set adjacency for each triangle
            foreach (var tri in triangles)
            {
                if (tri.IsRemoved) continue;

                tri.AdjacentTris = new NavSurfaceTri[3];

                for (int i = 0; i < 3; i++)
                {
                    var n1 = tri.Nodes[i];
                    var n2 = tri.Nodes[(i + 1) % 3];

                    var edgeKey = n1.GetHashCode() < n2.GetHashCode() ? (n1, n2) : (n2, n1);

                    if (edgeToTriangles.TryGetValue(edgeKey, out var edgeTriangles))
                    {
                        // Find the other triangle sharing this edge
                        foreach (var otherTri in edgeTriangles)
                        {
                            if (otherTri != tri && !otherTri.IsRemoved)
                            {
                                tri.AdjacentTris[i] = otherTri;
                                break;
                            }
                        }
                    }
                }
            }
        }

        private void MarkSteepTriangles(List<NavSurfaceTri> triangles)
        {
            var upVector = new Vector3(0, 0, 1);
            float maxAngleRad = genParams.MaxAngleForWalkable * (float)Math.PI / 180f;
            float steepAngleRad = genParams.AngleForTooSteep * (float)Math.PI / 180f;

            foreach (var tri in triangles)
            {
                if (tri.IsRemoved) continue;

                // Calculate angle between triangle normal and up vector
                float dotProduct = Vector3.Dot(tri.Normal, upVector);
                float angle = (float)Math.Acos(Math.Clamp(dotProduct, -1f, 1f));

                // Mark as too steep if angle exceeds threshold
                if (angle > steepAngleRad)
                {
                    tri.IsTooSteep = true;
                }
                // Mark as slope if angle is between walkable and too steep thresholds
                else if (angle > maxAngleRad)
                {
                    // This is a slope - walkable but steep
                    // Only set material to Slope if its not already a special material (water, stairs, etc.)
                    if (tri.Material == MaterialType.Default || tri.Material == MaterialType.Pavement)
                    {
                        tri.Material = MaterialType.Slope;
                    }
                }
            }
        }

        public int PerformEdgeCollapseOptimization(Action<string>? statusCallback = null)
        {
            return PerformEdgeCollapseOptimization(surfaceTriangles, statusCallback);
        }
        public int PerformAdaptiveOptimization(int targetVertexCount, Action<string>? statusCallback = null)
        {
            if (surfaceTriangles == null || surfaceTriangles.Count == 0)
            {
                statusCallback?.Invoke("Error: No triangles to optimize");
                return 0;
            }

            // Calculate current vertex count estimate
            int currentVertexCount = EstimateVertexCount(surfaceTriangles);
            statusCallback?.Invoke($"Current estimated vertex count: {currentVertexCount}");

            if (currentVertexCount <= targetVertexCount)
            {
                statusCallback?.Invoke($"Vertex count already within target ({targetVertexCount}), performing standard optimization");
                return PerformEdgeCollapseOptimization(surfaceTriangles, statusCallback);
            }

            // Calculate how much we need to reduce
            float reductionRatio = (float)targetVertexCount / currentVertexCount;
            statusCallback?.Invoke($"Need to reduce vertices by {(1 - reductionRatio) * 100:F1}%");

            // Iteratively increase error threshold until we reach target
            float originalErrorMetric = genParams.MaxQuadricErrorMetric;
            float currentErrorMetric = originalErrorMetric;
            int iterations = 0;
            const int maxIterations = 5;

            while (currentVertexCount > targetVertexCount && iterations < maxIterations)
            {
                iterations++;

                // Increase error threshold progressively
                currentErrorMetric *= 1.5f;
                genParams.MaxQuadricErrorMetric = currentErrorMetric;

                statusCallback?.Invoke($"Iteration {iterations}: Trying with MaxQuadricErrorMetric = {currentErrorMetric:F3}");

                // Perform optimization with new threshold
                int collapsed = PerformEdgeCollapseOptimization(surfaceTriangles, statusCallback);

                // Recalculate vertex count
                currentVertexCount = EstimateVertexCount(surfaceTriangles);
                statusCallback?.Invoke($"  Result: {currentVertexCount} vertices ({collapsed} edges collapsed)");

                if (currentVertexCount <= targetVertexCount)
                {
                    statusCallback?.Invoke($"Target reached! Final vertex count: {currentVertexCount}");
                    break;
                }
            }

            // Restore original parameter
            genParams.MaxQuadricErrorMetric = originalErrorMetric;

            if (currentVertexCount > targetVertexCount)
            {
                statusCallback?.Invoke($"WARNING: Could not reach target vertex count after {iterations} iterations");
                statusCallback?.Invoke($"Final count: {currentVertexCount}, Target: {targetVertexCount}");
            }

            return currentVertexCount;
        }
        private int EstimateVertexCount(List<NavSurfaceTri>? triangles)
        {
            if (triangles == null) return 0;

            // Count unique vertices (rough estimate)
            var uniqueNodes = new HashSet<NavGenNode>();
            foreach (var tri in triangles)
            {
                if (tri.IsRemoved || tri.Nodes == null) continue;
                foreach (var node in tri.Nodes)
                {
                    if (node != null)
                        uniqueNodes.Add(node);
                }
            }
            return uniqueNodes.Count;
        }

        public int PerformEdgeCollapseOptimization(List<NavSurfaceTri> triangles, Action<string>? statusCallback = null)
        {
            if (triangles == null || triangles.Count == 0)
            {
                statusCallback?.Invoke("Error: No triangles to optimize");
                return 0;
            }

            statusCallback?.Invoke("Starting edge collapse optimization...");

            // 1) Create NavTriEdge objects for all triangle edges
            statusCallback?.Invoke("Creating edge list...");
            var edgeList = CreateEdgeList(triangles);
            statusCallback?.Invoke($"Created {edgeList.Count} edges");

            // 2) Calculate quadric error metric cost for each edge
            statusCallback?.Invoke("Calculating edge costs...");
            foreach (var edge in edgeList)
            {
                CalculateEdgeCost(edge);
            }

            // 3) Create priority queue sorted by edge cost
            var edgeQueue = new SortedSet<NavTriEdge>(new EdgeCostComparer());
            foreach (var edge in edgeList)
            {
                if (!edge.IsRemoved)
                {
                    edgeQueue.Add(edge);
                }
            }

            statusCallback?.Invoke($"Priority queue initialized with {edgeQueue.Count} edges");

            int collapsedCount = 0;
            int processedCount = 0;
            int totalEdges = edgeQueue.Count;

            // 4) Collapse edges in order of increasing cost
            while (edgeQueue.Count > 0)
            {
                // Get lowest cost edge
                var edge = edgeQueue.Min;
                edgeQueue.Remove(edge);

                if (edge.IsRemoved)
                    continue;

                processedCount++;

                // Check if cost exceeds threshold
                if (edge.CostNode1ToNode2 > genParams.MaxQuadricErrorMetric)
                {
                    // All remaining edges have higher cost, stop optimization
                    break;
                }

                // 5) Validate collapse doesnt create invalid geometry
                if (!ValidateEdgeCollapse(edge))
                {
                    continue;
                }

                // Perform the collapse
                if (CollapseEdge(edge))
                {
                    collapsedCount++;

                    // 6) Update surrounding edge costs
                    var affectedEdges = GetAffectedEdges(edge.Node1);
                    foreach (var affectedEdge in affectedEdges)
                    {
                        if (!affectedEdge.IsRemoved)
                        {
                            // Remove from queue, recalculate cost, re-add
                            edgeQueue.Remove(affectedEdge);
                            CalculateEdgeCost(affectedEdge);
                            edgeQueue.Add(affectedEdge);
                        }
                    }
                }

                if (processedCount % 100 == 0)
                {
                    float progress = (float)processedCount / totalEdges * 100f;
                    statusCallback?.Invoke($"Edge collapse: {progress:F1}% ({collapsedCount} edges collapsed)");
                }
            }

            statusCallback?.Invoke($"Edge collapse complete: {collapsedCount} edges collapsed from {totalEdges} total edges");

            return collapsedCount;
        }

        private List<NavTriEdge> CreateEdgeList(List<NavSurfaceTri> triangles)
        {
            var edgeDict = new Dictionary<(NavGenNode, NavGenNode), NavTriEdge>();

            foreach (var tri in triangles)
            {
                if (tri.IsRemoved || tri.Nodes == null || tri.Nodes.Length < 3)
                    continue;

                for (int i = 0; i < 3; i++)
                {
                    var node1 = tri.Nodes[i];
                    var node2 = tri.Nodes[(i + 1) % 3];

                    if (node1 == null || node2 == null || node1.IsRemoved || node2.IsRemoved)
                        continue;

                    // Create edge key (order independent)
                    var edgeKey = node1.GetHashCode() < node2.GetHashCode() 
                        ? (node1, node2) 
                        : (node2, node1);

                    if (!edgeDict.ContainsKey(edgeKey))
                    {
                        var edge = new NavTriEdge
                        {
                            Node1 = edgeKey.Item1,
                            Node2 = edgeKey.Item2,
                            Tri1 = tri,
                            Tri2 = null
                        };

                        edgeDict[edgeKey] = edge;

                        // Add edge to nodes surrounding edges
                        node1.SurroundingEdges.Add(edge);
                        node2.SurroundingEdges.Add(edge);
                    }
                    else
                    {
                        // This is the second triangle sharing this edge
                        var edge = edgeDict[edgeKey];
                        edge.Tri2 = tri;
                    }
                }
            }

            return edgeDict.Values.ToList();
        }

        private void CalculateEdgeCost(NavTriEdge edge)
        {
            if (edge == null || edge.Node1 == null || edge.Node2 == null)
            {
                edge.CostNode1ToNode2 = float.MaxValue;
                edge.CostNode2ToNode1 = float.MaxValue;
                return;
            }


            // Calculate cost for collapsing Node2 onto Node1
            edge.CostNode1ToNode2 = CalculateQuadricError(edge.Node2, edge.Node1.BasePosition, edge);

            // Calculate cost for collapsing Node1 onto Node2
            edge.CostNode2ToNode1 = CalculateQuadricError(edge.Node1, edge.Node2.BasePosition, edge);
        }

        private float CalculateQuadricError(NavGenNode? nodeToMove, Vector3 targetPosition, NavTriEdge edge)
        {
            if (nodeToMove == null || nodeToMove.SurroundingTriangles == null)
                return float.MaxValue;

            float totalError = 0f;
            int validTriangles = 0;

            // For each triangle surrounding the node being moved
            foreach (var tri in nodeToMove.SurroundingTriangles)
            {
                if (tri == null || tri.IsRemoved)
                    continue;

                // Skip the triangles that will be removed by this edge collapse
                if (tri == edge.Tri1 || tri == edge.Tri2)
                    continue;

                float distance = Vector3.Dot(tri.Normal, targetPosition) - tri.PlaneDistance;

                // Sum squared distances (Quadric Error Metric)
                totalError += distance * distance;
                validTriangles++;
            }

            if (validTriangles == 0)
                return float.MaxValue;

            // Apply additional constraints from original implementation

            // Penalize boundary edges more heavily (edges with only one triangle)
            if (edge.Tri1 == null || edge.Tri2 == null)
            {
                totalError *= 5.0f;
            }

            // Penalize nodes with too many surrounding triangles to preserve topology
            int triangleCount = nodeToMove.SurroundingTriangles.Count;
            if (triangleCount > genParams.MaxTrianglesSurroundingNode)
            {
                totalError *= 10.0f;
            }

            // Additional penalty for surface type mismatches to preserve material boundaries
            // This prevents pavement from merging with non-pavement, etc.
            if (edge.Tri1 != null && edge.Tri2 != null)
            {
                if (edge.Tri1.Material != edge.Tri2.Material)
                {
                    // Never collapse edges between different materials
                    return float.MaxValue;
                }

                if (edge.Tri1.IsWater != edge.Tri2.IsWater)
                {
                    // Never collapse edges between water and non-water
                    return float.MaxValue;
                }

                if (edge.Tri1.IsTooSteep != edge.Tri2.IsTooSteep)
                {
                    // Never collapse edges between steep and non-steep surfaces
                    return float.MaxValue;
                }
            }

            return totalError;
        }

        private bool ValidateEdgeCollapse(NavTriEdge? edge)
        {
            if (edge == null || edge.IsRemoved)
                return false;

            if (edge.Node1 == null || edge.Node2 == null)
                return false;

            if (edge.Node1.IsRemoved || edge.Node2.IsRemoved)
                return false;

            // Dont collapse if either node has too many triangles
            if (edge.Node1.SurroundingTriangles.Count > genParams.MaxTrianglesSurroundingNode ||
                edge.Node2.SurroundingTriangles.Count > genParams.MaxTrianglesSurroundingNode)
                return false;

            // Check that collapse wont flip normals of surrounding triangles
            var targetPos = edge.Node1.BasePosition; // Collapse to Node1

            foreach (var tri in edge.Node2.SurroundingTriangles)
            {
                if (tri.IsRemoved)
                    continue;

                // Skip the two triangles that will be removed
                if (tri == edge.Tri1 || tri == edge.Tri2)
                    continue;

                // Calculate what the normal would be after collapse
                var newNormal = CalculateTriangleNormalAfterCollapse(tri, edge.Node2, targetPos);

                // Check if normal flipped
                float dotProduct = Vector3.Dot(tri.Normal, newNormal);
                if (dotProduct < 0.0f)
                {
                    return false; // Normal would flip
                }

                // Check if triangle would become degenerate
                float newArea = CalculateTriangleAreaAfterCollapse(tri, edge.Node2, targetPos);
                if (newArea < genParams.MinTriangleArea)
                {
                    return false; // Triangle would be too small
                }
            }

            return true;
        }

        private Vector3 CalculateTriangleNormalAfterCollapse(NavSurfaceTri tri, NavGenNode oldNode, Vector3 newPos)
        {
            if (tri.Nodes == null || tri.Nodes.Length < 3)
                return Vector3.Zero;

            var v0 = tri.Nodes[0] == oldNode ? newPos : tri.Nodes[0].BasePosition;
            var v1 = tri.Nodes[1] == oldNode ? newPos : tri.Nodes[1].BasePosition;
            var v2 = tri.Nodes[2] == oldNode ? newPos : tri.Nodes[2].BasePosition;

            var edge1 = v1 - v0;
            var edge2 = v2 - v0;

            var normal = Vector3.Cross(edge1, edge2);
            if (normal.LengthSquared() > 0)
            {
                normal.Normalize();
            }

            return normal;
        }

        private float CalculateTriangleAreaAfterCollapse(NavSurfaceTri tri, NavGenNode oldNode, Vector3 newPos)
        {
            if (tri.Nodes == null || tri.Nodes.Length < 3)
                return 0f;

            var v0 = tri.Nodes[0] == oldNode ? newPos : tri.Nodes[0].BasePosition;
            var v1 = tri.Nodes[1] == oldNode ? newPos : tri.Nodes[1].BasePosition;
            var v2 = tri.Nodes[2] == oldNode ? newPos : tri.Nodes[2].BasePosition;

            var edge1 = v1 - v0;
            var edge2 = v2 - v0;

            return Vector3.Cross(edge1, edge2).Length() * 0.5f;
        }

        private bool CollapseEdge(NavTriEdge? edge)
        {
            if (edge == null || edge.IsRemoved)
                return false;

            var node1 = edge.Node1;
            var node2 = edge.Node2;

            if (node1 == null || node2 == null || node1.IsRemoved || node2.IsRemoved)
                return false;

            // Mark the two triangles sharing this edge as removed
            if (edge.Tri1 != null)
            {
                edge.Tri1.IsRemoved = true;
                RemoveTriangleFromNodes(edge.Tri1);
            }

            if (edge.Tri2 != null)
            {
                edge.Tri2.IsRemoved = true;
                RemoveTriangleFromNodes(edge.Tri2);
            }

            // Update all triangles that reference Node2 to reference Node1 instead
            foreach (var tri in node2.SurroundingTriangles.ToList())
            {
                if (tri.IsRemoved)
                    continue;

                // Skip the triangles we just removed
                if (tri == edge.Tri1 || tri == edge.Tri2)
                    continue;

                // Replace Node2 with Node1 in the triangle
                for (int i = 0; i < tri.Nodes.Length; i++)
                {
                    if (tri.Nodes[i] == node2)
                    {
                        tri.Nodes[i] = node1;
                        node1.SurroundingTriangles.Add(tri);
                    }
                }
                tri.CalculateNormal();
            }

            // Mark all edges connected to Node2 as removed
            foreach (var e in node2.SurroundingEdges)
            {
                e.IsRemoved = true;
            }

            edge.IsRemoved = true;
            node2.IsRemoved = true;

            return true;
        }

        private void RemoveTriangleFromNodes(NavSurfaceTri tri)
        {
            if (tri.Nodes == null)
                return;

            foreach (var node in tri.Nodes)
            {
                if (node != null)
                {
                    node.SurroundingTriangles.Remove(tri);
                }
            }
        }

        private List<NavTriEdge> GetAffectedEdges(NavGenNode node)
        {
            var affectedEdges = new List<NavTriEdge>();

            if (node == null || node.IsRemoved)
                return affectedEdges;

            // Get all edges from surrounding triangles
            foreach (var tri in node.SurroundingTriangles)
            {
                if (tri.IsRemoved || tri.Nodes == null)
                    continue;

                for (int i = 0; i < tri.Nodes.Length; i++)
                {
                    var n1 = tri.Nodes[i];
                    var n2 = tri.Nodes[(i + 1) % tri.Nodes.Length];

                    if (n1 == null || n2 == null)
                        continue;

                    // Find edge in nodes surrounding edges
                    foreach (var edge in n1.SurroundingEdges)
                    {
                        if (edge.IsRemoved)
                            continue;

                        if ((edge.Node1 == n1 && edge.Node2 == n2) ||
                            (edge.Node1 == n2 && edge.Node2 == n1))
                        {
                            if (!affectedEdges.Contains(edge))
                            {
                                affectedEdges.Add(edge);
                            }
                        }
                    }
                }
            }

            return affectedEdges;
        }

        private class EdgeCostComparer : IComparer<NavTriEdge>
        {
            public int Compare(NavTriEdge? x, NavTriEdge? y)
            {
                if (x == null && y == null) return 0;
                if (x == null) return -1;
                if (y == null) return 1;

                int costCompare = x.CostNode1ToNode2.CompareTo(y.CostNode1ToNode2);
                if (costCompare != 0)
                    return costCompare;

                // If costs are equal, use hash code to maintain uniqueness in SortedSet
                return x.GetHashCode().CompareTo(y.GetHashCode());
            }
        }

        public List<NavSurfacePoly> PerformPolygonMerging(Action<string>? statusCallback = null)
        {
            return PerformPolygonMerging(surfaceTriangles, statusCallback);
        }

        public int DecimatePolygons(int targetVertexCount, Action<string>? statusCallback = null)
        {
            if (surfacePolygons == null || surfacePolygons.Count == 0)
            {
                statusCallback?.Invoke("Error: No polygons to decimate");
                return 0;
            }

            int currentVertexCount = surfacePolygons.Sum(p => p.Vertices?.Count ?? 0);
            statusCallback?.Invoke($"Current vertex count: {currentVertexCount}, Target: {targetVertexCount}");

            if (currentVertexCount <= targetVertexCount)
            {
                statusCallback?.Invoke("Already within target vertex count");
                return 0;
            }

            // Sort polygons by area (smallest first)
            var sortedPolys = surfacePolygons
                .Where(p => !p.IsRemoved)
                .OrderBy(p => p.CalculateArea())
                .ToList();

            int removedCount = 0;
            int verticesRemoved = 0;

            // Remove smallest polygons until we reach target
            foreach (var poly in sortedPolys)
            {
                if (currentVertexCount <= targetVertexCount)
                    break;

                int polyVertexCount = poly.Vertices?.Count ?? 0;
                poly.IsRemoved = true;
                removedCount++;
                verticesRemoved += polyVertexCount;
                currentVertexCount -= polyVertexCount;

                if (removedCount % 100 == 0)
                {
                    statusCallback?.Invoke($"Removed {removedCount} polygons, {verticesRemoved} vertices. Current: {currentVertexCount}");
                }
            }

            // Remove from list
            surfacePolygons.RemoveAll(p => p.IsRemoved);

            statusCallback?.Invoke($"Decimation complete: Removed {removedCount} polygons ({verticesRemoved} vertices)");
            statusCallback?.Invoke($"Final vertex count: {currentVertexCount}");

            return removedCount;
        }

        public List<NavSurfacePoly> PerformPolygonMerging(List<NavSurfaceTri> triangles, Action<string>? statusCallback = null)
        {
            if (triangles == null || triangles.Count == 0)
            {
                statusCallback?.Invoke("Error: No triangles to merge");
                return new List<NavSurfacePoly>();
            }

            statusCallback?.Invoke("Starting polygon merging...");

            // 1) Build edge dictionary for fast adjacency lookups
            statusCallback?.Invoke("Building edge dictionary...");
            var edgeDict = BuildEdgeDictionary(triangles);
            statusCallback?.Invoke($"Edge dictionary built with {edgeDict.Count} edges");

            // 2) Convert triangles to initial polygons and build adjacency
            var polygons = new List<NavSurfacePoly>();
            var triangleToPolygon = new Dictionary<NavSurfaceTri, NavSurfacePoly>();
            var edgeToPolygon = new Dictionary<(NavGenNode, NavGenNode), NavSurfacePoly>();

            foreach (var tri in triangles)
            {
                if (tri.IsRemoved) continue;

                var poly = new NavSurfacePoly
                {
                    Vertices = new List<NavGenNode> { tri.Nodes[0], tri.Nodes[1], tri.Nodes[2] },
                    Material = tri.Material,
                    IsWater = tri.IsWater,
                    IsTooSteep = tri.IsTooSteep,
                    PolyFlags = tri.PolyFlags,

                    // Preserve ALL extended flags from triangle
                    ProceduralId = tri.ProceduralId,
                    RoomId = tri.RoomId,
                    PedDensity = tri.PedDensity,
                    MaterialFlags = tri.MaterialFlags,
                    IsInterior = tri.IsInterior,
                    IsRoad = tri.IsRoad,
                    IsTrainTracks = tri.IsTrainTracks,
                    IsFlatGround = tri.IsFlatGround,
                    IsShallowWater = tri.IsShallowWater
                };

                poly.CalculateNormalAndPlane();
                polygons.Add(poly);
                triangleToPolygon[tri] = poly;
                
                // Build edge-to-polygon mapping for fast adjacency lookup
                for (int i = 0; i < 3; i++)
                {
                    var v1 = tri.Nodes[i];
                    var v2 = tri.Nodes[(i + 1) % 3];
                    var edgeKey = v1.GetHashCode() < v2.GetHashCode() ? (v1, v2) : (v2, v1);
                    
                    // Store the polygon for this edge (will be overwritten if shared)
                    edgeToPolygon[edgeKey] = poly;
                }
            }

            Log($"Created {polygons.Count} initial polygons from triangles", statusCallback);

            // 3) Sort polygons by area (merge smaller polygons first)
            polygons.Sort((a, b) => a.CalculateArea().CompareTo(b.CalculateArea()));

            // 4) Attempt to merge adjacent polygons using fast edge lookup
            int mergeCount = 0;
            int processedCount = 0;
            int totalPolygons = polygons.Count;
            int maxMergeAttempts = 10; // Increased from 3 to allow more aggressive merging

            Log($"Starting merge loop with {totalPolygons} polygons", statusCallback);
            
            // Reset debug counters
            debugMergeAttempts = 0;
            debugMaterialFail = 0;
            debugWaterFail = 0;
            debugVertexCountFail = 0;
            debugCoplanarFail = 0;
            debugConvexFail = 0;

            for (int i = 0; i < polygons.Count; i++)
            {
                var poly = polygons[i];
                if (poly.IsRemoved) continue;

                processedCount++;

                // Try to merge with each adjacent polygon (limited attempts)
                int mergeAttempts = 0;
                bool merged = true;
                while (merged && poly.Vertices.Count < genParams.MaxPolygonVertices && mergeAttempts < maxMergeAttempts)
                {
                    merged = false;
                    mergeAttempts++;

                    // Find adjacent polygons by checking edges using fast lookup
                    for (int edgeIdx = 0; edgeIdx < poly.Vertices.Count; edgeIdx++)
                    {
                        var v1 = poly.Vertices[edgeIdx];
                        var v2 = poly.Vertices[(edgeIdx + 1) % poly.Vertices.Count];
                        var edgeKey = v1.GetHashCode() < v2.GetHashCode() ? (v1, v2) : (v2, v1);

                        // Fast lookup of adjacent polygon
                        NavSurfacePoly? adjacentPoly = null;
                        if (edgeToPolygon.TryGetValue(edgeKey, out var edgePoly))
                        {
                            if (edgePoly != poly && !edgePoly.IsRemoved)
                            {
                                adjacentPoly = edgePoly;
                            }
                        }

                        if (adjacentPoly != null)
                        {
                            debugMergeAttempts++;
                            
                            // Test if merge is valid with detailed logging
                            bool canMerge = true;

                            // This preserves material boundaries (pavement, roads, etc.)
                            if (poly.Material != adjacentPoly.Material)
                            {
                                debugMaterialFail++;
                                canMerge = false;
                            }
                            else if (poly.IsWater != adjacentPoly.IsWater)
                            {
                                debugWaterFail++;
                                canMerge = false;
                            }
                            else if (poly.IsTooSteep != adjacentPoly.IsTooSteep)
                            {
                                // Never merge steep surfaces with non-steep surfaces
                                canMerge = false;
                            }
                            else
                            {
                                int combinedVertexCount = poly.Vertices.Count + adjacentPoly.Vertices.Count - 2;
                                if (combinedVertexCount > genParams.MaxPolygonVertices)
                                {
                                    debugVertexCountFail++;
                                    canMerge = false;
                                }
                                else if (!TestCoplanarity(poly, adjacentPoly))
                                {
                                    debugCoplanarFail++;
                                    canMerge = false;
                                }
                                else if (!TestConvexityAfterMerge(poly, adjacentPoly, v1, v2))
                                {
                                    debugConvexFail++;
                                    canMerge = false;
                                }
                            }
                            
                            if (canMerge)
                            {
                                MergePolygons(poly, adjacentPoly, v1, v2);
                                adjacentPoly.IsRemoved = true;
                                mergeCount++;
                                merged = true;
                                
                                // Update edge-to-polygon mapping for the merged polygons edges
                                for (int j = 0; j < poly.Vertices.Count; j++)
                                {
                                    var ev1 = poly.Vertices[j];
                                    var ev2 = poly.Vertices[(j + 1) % poly.Vertices.Count];
                                    var ek = ev1.GetHashCode() < ev2.GetHashCode() ? (ev1, ev2) : (ev2, ev1);
                                    edgeToPolygon[ek] = poly;
                                }
                                
                                break; // Start over with new polygon shape
                            }
                        }
                    }
                }
                
                if (processedCount == 100 && mergeCount == 0)
                {
                    Log($"DEBUG: After 100 polygons, 0 merges. Attempts: {debugMergeAttempts}, Material: {debugMaterialFail}, Water: {debugWaterFail}, Vertex: {debugVertexCountFail}, Coplanar: {debugCoplanarFail}, Convex: {debugConvexFail}", statusCallback);
                }

                if (processedCount % 1000 == 0)
                {
                    float progress = (float)processedCount / totalPolygons * 100f;
                    statusCallback?.Invoke($"Polygon merging: {progress:F1}% ({mergeCount} merges performed)");
                }
            }
            
            Log($"Merge loop complete: processed {processedCount} polygons, performed {mergeCount} merges", statusCallback);
            Log($"Merge statistics:", statusCallback);
            Log($"  Total merge attempts: {debugMergeAttempts}", statusCallback);
            Log($"  Failed - Material mismatch: {debugMaterialFail}", statusCallback);
            Log($"  Failed - Water mismatch: {debugWaterFail}", statusCallback);
            Log($"  Failed - Vertex count: {debugVertexCountFail}", statusCallback);
            Log($"  Failed - Coplanarity: {debugCoplanarFail}", statusCallback);
            Log($"  Failed - Convexity: {debugConvexFail}", statusCallback);

            // Step 5: Remove merged polygons and collect final list
            var finalPolygons = polygons.Where(p => !p.IsRemoved).ToList();

            // Store polygons for later phases
            surfacePolygons = finalPolygons;

            statusCallback?.Invoke($"Polygon merging complete: {mergeCount} merges performed, {finalPolygons.Count} final polygons");

            return finalPolygons;
        }

        private EdgeDictionary BuildEdgeDictionary(List<NavSurfaceTri> triangles)
        {
            var edgeDict = new EdgeDictionary();

            foreach (var tri in triangles)
            {
                if (tri.IsRemoved || tri.Nodes == null || tri.Nodes.Length < 3)
                    continue;

                for (int i = 0; i < 3; i++)
                {
                    var v1 = tri.Nodes[i].BasePosition;
                    var v2 = tri.Nodes[(i + 1) % 3].BasePosition;

                    var existingEdge = edgeDict.TryGetEdge(v1, v2);
                    if (existingEdge == null)
                    {
                        var edge = new NavTriEdge
                        {
                            Node1 = tri.Nodes[i],
                            Node2 = tri.Nodes[(i + 1) % 3],
                            Tri1 = tri
                        };
                        edgeDict.AddEdge(v1, v2, edge);
                    }
                    else
                    {
                        existingEdge.Tri2 = tri;
                    }
                }
            }

            return edgeDict;
        }

        private NavSurfacePoly FindAdjacentPolygon(NavSurfacePoly poly, NavGenNode v1, NavGenNode v2, 
            List<NavSurfacePoly> polygons, EdgeDictionary edgeDict)
        {
            // Look up edge in dictionary
            var edge = edgeDict.TryGetEdge(v1.BasePosition, v2.BasePosition);
            if (edge == null)
                return null;

            // Check triangles sharing this edge
            var triangles = new[] { edge.Tri1, edge.Tri2 };

            foreach (var tri in triangles)
            {
                if (tri == null || tri.IsRemoved)
                    continue;

                // Find which polygon this triangle belongs to
                foreach (var otherPoly in polygons)
                {
                    if (otherPoly == poly || otherPoly.IsRemoved)
                        continue;

                    // Check if this polygon contains the triangles vertices
                    if (PolygonContainsTriangle(otherPoly, tri))
                    {
                        return otherPoly;
                    }
                }
            }

            return null;
        }

        private bool PolygonContainsTriangle(NavSurfacePoly poly, NavSurfaceTri tri)
        {
            if (tri.Nodes == null || tri.Nodes.Length < 3)
                return false;

            int matchCount = 0;
            foreach (var triNode in tri.Nodes)
            {
                foreach (var polyNode in poly.Vertices)
                {
                    if (polyNode == triNode)
                    {
                        matchCount++;
                        break;
                    }
                }
            }

            return matchCount >= 2; // At least 2 vertices match (shared edge)
        }

        private bool CanMergePolygons(NavSurfacePoly poly1, NavSurfacePoly poly2, NavGenNode sharedV1, NavGenNode sharedV2)
        {
            if (poly1.Material != poly2.Material)
                return false;

            if (poly1.IsWater != poly2.IsWater)
                return false;

            int combinedVertexCount = poly1.Vertices.Count + poly2.Vertices.Count - 2; // Subtract shared edge vertices
            if (combinedVertexCount > genParams.MaxPolygonVertices)
                return false;

            if (!TestCoplanarity(poly1, poly2))
                return false;

            if (!TestConvexityAfterMerge(poly1, poly2, sharedV1, sharedV2))
                return false;

            return true;
        }
        
        private static int debugMergeAttempts = 0;
        private static int debugMaterialFail = 0;
        private static int debugWaterFail = 0;
        private static int debugVertexCountFail = 0;
        private static int debugCoplanarFail = 0;
        private static int debugConvexFail = 0;

        private bool TestCoplanarity(NavSurfacePoly poly1, NavSurfacePoly poly2)
        {
            // Test all vertices of poly2 against poly1s plane
            foreach (var vertex in poly2.Vertices)
            {
                float distance = Math.Abs(Vector3.Dot(poly1.Normal, vertex.BasePosition) - poly1.PlaneDistance);
                if (distance > genParams.CoplanarPlaneTestEps)
                {
                    return false;
                }
            }

            // Test all vertices of poly1 against poly2s plane
            foreach (var vertex in poly1.Vertices)
            {
                float distance = Math.Abs(Vector3.Dot(poly2.Normal, vertex.BasePosition) - poly2.PlaneDistance);
                if (distance > genParams.CoplanarPlaneTestEps)
                {
                    return false;
                }
            }

            return true;
        }

        private bool TestConvexityAfterMerge(NavSurfacePoly poly1, NavSurfacePoly poly2, NavGenNode sharedV1, NavGenNode sharedV2)
        {
            // Create merged vertex list
            var mergedVertices = CreateMergedVertexList(poly1, poly2, sharedV1, sharedV2);

            if (mergedVertices == null || mergedVertices.Count < 3)
                return false;

            // Test convexity in 2D (XY plane) using cross product sign
            // For a convex polygon, all cross products should have the same sign
            bool? isClockwise = null;

            for (int i = 0; i < mergedVertices.Count; i++)
            {
                var v0 = mergedVertices[i].BasePosition;
                var v1 = mergedVertices[(i + 1) % mergedVertices.Count].BasePosition;
                var v2 = mergedVertices[(i + 2) % mergedVertices.Count].BasePosition;

                // Calculate cross product in 2D (Z component only)
                var edge1 = new Vector2(v1.X - v0.X, v1.Y - v0.Y);
                var edge2 = new Vector2(v2.X - v1.X, v2.Y - v1.Y);
                float crossZ = edge1.X * edge2.Y - edge1.Y * edge2.X;

                // Skip near-zero cross products (colinear edges)
                if (Math.Abs(crossZ) < 0.001f)
                    continue;

                bool currentClockwise = crossZ < 0;

                if (!isClockwise.HasValue)
                {
                    isClockwise = currentClockwise;
                }
                else if (isClockwise.Value != currentClockwise)
                {
                    // Cross product sign changed - polygon is not convex
                    return false;
                }
            }

            return true;
        }

        private List<NavGenNode> CreateMergedVertexList(NavSurfacePoly poly1, NavSurfacePoly poly2, 
            NavGenNode sharedV1, NavGenNode sharedV2)
        {
            var mergedVertices = new List<NavGenNode>();

            // Find indices of shared vertices in both polygons
            int poly1_v1_idx = poly1.Vertices.IndexOf(sharedV1);
            int poly1_v2_idx = poly1.Vertices.IndexOf(sharedV2);
            int poly2_v1_idx = poly2.Vertices.IndexOf(sharedV1);
            int poly2_v2_idx = poly2.Vertices.IndexOf(sharedV2);

            if (poly1_v1_idx < 0 || poly1_v2_idx < 0 || poly2_v1_idx < 0 || poly2_v2_idx < 0)
                return null; // Shared vertices not found

            // Add vertices from poly1, skipping the shared edge
            int startIdx = poly1_v2_idx;
            int endIdx = poly1_v1_idx;
            int count = 0;

            for (int i = startIdx; count < poly1.Vertices.Count; i = (i + 1) % poly1.Vertices.Count, count++)
            {
                if (i == poly1_v1_idx)
                    break;

                mergedVertices.Add(poly1.Vertices[i]);
            }

            // Add vertices from poly2, skipping the shared edge
            startIdx = poly2_v1_idx;
            endIdx = poly2_v2_idx;
            count = 0;

            for (int i = startIdx; count < poly2.Vertices.Count; i = (i + 1) % poly2.Vertices.Count, count++)
            {
                if (i == poly2_v2_idx)
                    break;

                mergedVertices.Add(poly2.Vertices[i]);
            }

            return mergedVertices;
        }

        private void MergePolygons(NavSurfacePoly poly1, NavSurfacePoly poly2, NavGenNode sharedV1, NavGenNode sharedV2)
        {
            // Create merged vertex list
            var mergedVertices = CreateMergedVertexList(poly1, poly2, sharedV1, sharedV2);

            if (mergedVertices == null)
                return;

            poly1.Vertices = mergedVertices;
            poly1.CalculateNormalAndPlane();
        }

        public int RemoveColinearEdges(Action<string>? statusCallback = null)
        {
            return RemoveColinearEdges(surfacePolygons, statusCallback);
        }

        public int RemoveColinearEdges(List<NavSurfacePoly> polygons, Action<string>? statusCallback = null)
        {
            if (polygons == null || polygons.Count == 0)
            {
                statusCallback?.Invoke("Error: No polygons to process");
                return 0;
            }

            statusCallback?.Invoke("Starting colinear edge removal...");

            int totalVerticesRemoved = 0;
            int processedCount = 0;
            int totalPolygons = polygons.Count;

            // Build adjacency information to identify shared vertices
            var sharedVertices = BuildSharedVertexSet(polygons);

            foreach (var poly in polygons)
            {
                if (poly.IsRemoved || poly.Vertices == null || poly.Vertices.Count < 3)
                    continue;

                processedCount++;

                // Remove colinear vertices from this polygon
                int removedCount = RemoveColinearVerticesFromPolygon(poly, sharedVertices);
                totalVerticesRemoved += removedCount;

                if (processedCount % 100 == 0)
                {
                    float progress = (float)processedCount / totalPolygons * 100f;
                    statusCallback?.Invoke($"Colinear edge removal: {progress:F1}% ({totalVerticesRemoved} vertices removed)");
                }
            }

            statusCallback?.Invoke($"Colinear edge removal complete: {totalVerticesRemoved} vertices removed from {processedCount} polygons");

            return totalVerticesRemoved;
        }

        private HashSet<NavGenNode> BuildSharedVertexSet(List<NavSurfacePoly> polygons)
        {
            var vertexCounts = new Dictionary<NavGenNode, int>();

            // Count how many polygons each vertex appears in
            foreach (var poly in polygons)
            {
                if (poly.IsRemoved || poly.Vertices == null)
                    continue;

                foreach (var vertex in poly.Vertices)
                {
                    if (vertex == null)
                        continue;

                    if (!vertexCounts.ContainsKey(vertex))
                    {
                        vertexCounts[vertex] = 0;
                    }
                    vertexCounts[vertex]++;
                }
            }

            // Build set of shared vertices (appear in more than one polygon)
            var sharedVertices = new HashSet<NavGenNode>();
            foreach (var kvp in vertexCounts)
            {
                if (kvp.Value > 1)
                {
                    sharedVertices.Add(kvp.Key);
                }
            }

            return sharedVertices;
        }

        private int RemoveColinearVerticesFromPolygon(NavSurfacePoly poly, HashSet<NavGenNode> sharedVertices)
        {
            if (poly.Vertices == null || poly.Vertices.Count < 3)
                return 0;

            int removedCount = 0;
            bool removedAny = true;

            // Keep iterating until no more vertices can be removed
            while (removedAny && poly.Vertices.Count > 3)
            {
                removedAny = false;
                var newVertices = new List<NavGenNode>();

                for (int i = 0; i < poly.Vertices.Count; i++)
                {
                    var prevVertex = poly.Vertices[(i - 1 + poly.Vertices.Count) % poly.Vertices.Count];
                    var currentVertex = poly.Vertices[i];
                    var nextVertex = poly.Vertices[(i + 1) % poly.Vertices.Count];

                    // Check if current vertex is shared with other polygons
                    // If so, we cannot remove it as its on a shared edge
                    if (sharedVertices.Contains(currentVertex))
                    {
                        newVertices.Add(currentVertex);
                        continue;
                    }

                    // Calculate vectors for the two edges
                    var edge1 = currentVertex.BasePosition - prevVertex.BasePosition;
                    var edge2 = nextVertex.BasePosition - currentVertex.BasePosition;

                    // Normalize the edges
                    float edge1Length = edge1.Length();
                    float edge2Length = edge2.Length();

                    if (edge1Length < 1e-6f || edge2Length < 1e-6f)
                    {
                        // Degenerate edge, keep the vertex
                        newVertices.Add(currentVertex);
                        continue;
                    }

                    edge1 /= edge1Length;
                    edge2 /= edge2Length;

                    float dotProduct = Vector3.Dot(edge1, edge2);

                    // If dot product is close to 1.0, the edges are colinear (angle close to 0 degrees)
                    // If dot product is close to -1.0, the edges are opposite (angle close to 180 degrees)
                    // We want to remove vertices where the angle is close to 180 degrees (straight line)
                    const float colinearThreshold = 0.9998f; // Corresponds to about 1 degree tolerance

                    if (dotProduct > colinearThreshold)
                    {
                        removedCount++;
                        removedAny = true;
                    }
                    else
                    {
                        newVertices.Add(currentVertex);
                    }
                }

                if (removedAny && newVertices.Count >= 3)
                {
                    poly.Vertices = newVertices;
                }
                else if (newVertices.Count < 3)
                {
                    break;
                }
            }

            if (removedCount > 0)
            {
                poly.CalculateNormalAndPlane();
            }

            return removedCount;
        }

        public int FillJaggedEdges(Action<string>? statusCallback = null)
        {
            return FillJaggedEdges(surfaceTriangles, statusCallback);
        }

        public int FillJaggedEdges(List<NavSurfaceTri> triangles, Action<string>? statusCallback = null)
        {
            if (triangles == null || triangles.Count == 0)
            {
                statusCallback?.Invoke("Error: No triangles to process");
                return 0;
            }

            statusCallback?.Invoke("Starting jagged edge filling...");

            // 1) Identify boundary edges (edges with only one adjacent triangle)
            statusCallback?.Invoke("Identifying boundary edges...");
            var boundaryEdges = IdentifyBoundaryEdges(triangles);
            statusCallback?.Invoke($"Found {boundaryEdges.Count} boundary edges");

            if (boundaryEdges.Count == 0)
            {
                statusCallback?.Invoke("No boundary edges found, skipping gap filling");
                return 0;
            }

            // 2) Find gaps between non-adjacent boundary edges
            statusCallback?.Invoke("Finding gaps between boundary edges...");
            var gaps = FindGapsBetweenBoundaryEdges(boundaryEdges);
            statusCallback?.Invoke($"Found {gaps.Count} potential gaps to fill");

            if (gaps.Count == 0)
            {
                statusCallback?.Invoke("No gaps found, skipping gap filling");
                return 0;
            }

            // 3) Attempt to fill each gap with triangles
            statusCallback?.Invoke("Filling gaps with triangles...");
            int trianglesCreated = 0;
            int processedGaps = 0;

            foreach (var gap in gaps)
            {
                processedGaps++;

                // Try to create a triangle to fill this gap
                var newTriangle = TryCreateGapFillingTriangle(gap, triangles);

                if (newTriangle != null)
                {
                    // Validate the new triangle doesnt intersect existing geometry
                    if (ValidateGapFillingTriangle(newTriangle, triangles))
                    {
                        triangles.Add(newTriangle);
                        trianglesCreated++;

                        // Establish adjacency with surrounding triangles
                        EstablishAdjacencyForNewTriangle(newTriangle, triangles);
                    }
                }

                if (processedGaps % 10 == 0)
                {
                    float progress = (float)processedGaps / gaps.Count * 100f;
                    statusCallback?.Invoke($"Gap filling: {progress:F1}% ({trianglesCreated} triangles created)");
                }
            }

            statusCallback?.Invoke($"Jagged edge filling complete: {trianglesCreated} triangles created from {gaps.Count} gaps");

            return trianglesCreated;
        }

        private class BoundaryEdge
        {
            public NavGenNode Node1 { get; set; }
            public NavGenNode Node2 { get; set; }
            public NavSurfaceTri Triangle { get; set; }
            public Vector3 Midpoint { get; set; }
            public float Length { get; set; }

            public BoundaryEdge(NavGenNode n1, NavGenNode n2, NavSurfaceTri tri)
            {
                Node1 = n1;
                Node2 = n2;
                Triangle = tri;
                Midpoint = (n1.BasePosition + n2.BasePosition) * 0.5f;
                Length = (n2.BasePosition - n1.BasePosition).Length();
            }
        }

        private class EdgeGap
        {
            public BoundaryEdge Edge1 { get; set; }
            public BoundaryEdge Edge2 { get; set; }
            public float Distance { get; set; }
            public NavGenNode SharedNode1 { get; set; } // Node from Edge1 closest to Edge2
            public NavGenNode SharedNode2 { get; set; } // Node from Edge2 closest to Edge1
            public NavGenNode ThirdNode { get; set; }   // Third node to form triangle
        }

        private List<BoundaryEdge> IdentifyBoundaryEdges(List<NavSurfaceTri> triangles)
        {
            var boundaryEdges = new List<BoundaryEdge>();
            var edgeCount = new Dictionary<(NavGenNode, NavGenNode), (NavSurfaceTri, int)>();

            foreach (var tri in triangles)
            {
                if (tri.IsRemoved || tri.Nodes == null || tri.Nodes.Length < 3)
                    continue;

                for (int i = 0; i < 3; i++)
                {
                    var n1 = tri.Nodes[i];
                    var n2 = tri.Nodes[(i + 1) % 3];

                    if (n1 == null || n2 == null || n1.IsRemoved || n2.IsRemoved)
                        continue;

                    // Create edge key
                    var edgeKey = n1.GetHashCode() < n2.GetHashCode() ? (n1, n2) : (n2, n1);

                    if (edgeCount.ContainsKey(edgeKey))
                    {
                        // This edge is shared by multiple triangles, not a boundary edge
                        var existing = edgeCount[edgeKey];
                        edgeCount[edgeKey] = (existing.Item1, existing.Item2 + 1);
                    }
                    else
                    {
                        // First time seeing this edge
                        edgeCount[edgeKey] = (tri, 1);
                    }
                }
            }

            // Collect edges that appear only once (boundary edges)
            foreach (var kvp in edgeCount)
            {
                if (kvp.Value.Item2 == 1)
                {
                    var edge = new BoundaryEdge(kvp.Key.Item1, kvp.Key.Item2, kvp.Value.Item1);
                    boundaryEdges.Add(edge);
                }
            }

            return boundaryEdges;
        }

        private List<EdgeGap> FindGapsBetweenBoundaryEdges(List<BoundaryEdge> boundaryEdges)
        {
            var gaps = new List<EdgeGap>();

            // Maximum distance to consider for gap filling
            float maxGapDistance = genParams.MaxTriangleSideLength * 2.0f;

            // For each boundary edge, look for nearby boundary edges that could form a gap
            for (int i = 0; i < boundaryEdges.Count; i++)
            {
                var edge1 = boundaryEdges[i];

                for (int j = i + 1; j < boundaryEdges.Count; j++)
                {
                    var edge2 = boundaryEdges[j];

                    // Check if edges share a node (adjacent edges dont form a gap)
                    if (edge1.Node1 == edge2.Node1 || edge1.Node1 == edge2.Node2 ||
                        edge1.Node2 == edge2.Node1 || edge1.Node2 == edge2.Node2)
                    {
                        continue;
                    }

                    // Calculate distance between edge midpoints
                    float distance = (edge2.Midpoint - edge1.Midpoint).Length();

                    if (distance > maxGapDistance)
                        continue;

                    // Find the closest nodes between the two edges
                    float minDist = float.MaxValue;
                    NavGenNode? closestNode1 = null;
                    NavGenNode? closestNode2 = null;

                    foreach (var n1 in new[] { edge1.Node1, edge1.Node2 })
                    {
                        foreach (var n2 in new[] { edge2.Node1, edge2.Node2 })
                        {
                            float dist = (n2.BasePosition - n1.BasePosition).Length();
                            if (dist < minDist)
                            {
                                minDist = dist;
                                closestNode1 = n1;
                                closestNode2 = n2;
                            }
                        }
                    }

                    // Check if the distance is reasonable for gap filling
                    if (minDist > genParams.MaxTriangleSideLength)
                        continue;

                    // Find the third node to form a triangle
                    // Try both remaining nodes from each edge
                    NavGenNode? thirdNode = null;
                    float bestThirdNodeDist = float.MaxValue;

                    foreach (var n in new[] { edge1.Node1, edge1.Node2, edge2.Node1, edge2.Node2 })
                    {
                        if (n == closestNode1 || n == closestNode2)
                            continue;

                        float dist1 = (n.BasePosition - closestNode1.BasePosition).Length();
                        float dist2 = (n.BasePosition - closestNode2.BasePosition).Length();

                        // Check if this node forms a reasonable triangle
                        if (dist1 <= genParams.MaxTriangleSideLength && 
                            dist2 <= genParams.MaxTriangleSideLength)
                        {
                            float avgDist = (dist1 + dist2) * 0.5f;
                            if (avgDist < bestThirdNodeDist)
                            {
                                bestThirdNodeDist = avgDist;
                                thirdNode = n;
                            }
                        }
                    }

                    if (thirdNode != null)
                    {
                        var gap = new EdgeGap
                        {
                            Edge1 = edge1,
                            Edge2 = edge2,
                            Distance = minDist,
                            SharedNode1 = closestNode1,
                            SharedNode2 = closestNode2,
                            ThirdNode = thirdNode
                        };

                        gaps.Add(gap);
                    }
                }
            }

            // Sort gaps by distance (fill smaller gaps first)
            gaps.Sort((a, b) => a.Distance.CompareTo(b.Distance));

            return gaps;
        }

        private NavSurfaceTri TryCreateGapFillingTriangle(EdgeGap gap, List<NavSurfaceTri> existingTriangles)
        {
            if (gap.SharedNode1 == null || gap.SharedNode2 == null || gap.ThirdNode == null)
                return null;

            // Check if these three nodes already form a triangle
            foreach (var tri in existingTriangles)
            {
                if (tri.IsRemoved || tri.Nodes == null)
                    continue;

                int matchCount = 0;
                foreach (var node in tri.Nodes)
                {
                    if (node == gap.SharedNode1 || node == gap.SharedNode2 || node == gap.ThirdNode)
                        matchCount++;
                }

                if (matchCount == 3)
                {
                    // Triangle already exists
                    return null;
                }
            }

            var newTriangle = new NavSurfaceTri
            {
                Nodes = new NavGenNode[] { gap.SharedNode1, gap.SharedNode2, gap.ThirdNode }
            };

            newTriangle.CalculateNormal();

            float area = newTriangle.CalculateArea();
            if (area < genParams.MinTriangleArea || area > genParams.MaxTriangleArea)
            {
                return null;
            }

            // Set material and flags from surrounding triangles
            var srcTri = gap.Edge1.Triangle;
            newTriangle.Material = srcTri.Material;
            newTriangle.IsWater = srcTri.IsWater || gap.Edge2.Triangle.IsWater;
            newTriangle.IsShallowWater = srcTri.IsShallowWater || gap.Edge2.Triangle.IsShallowWater;
            newTriangle.ProceduralId = srcTri.ProceduralId;
            newTriangle.RoomId = srcTri.RoomId;
            newTriangle.PedDensity = srcTri.PedDensity;
            newTriangle.MaterialFlags = srcTri.MaterialFlags;
            newTriangle.IsInterior = srcTri.IsInterior;
            newTriangle.IsRoad = srcTri.IsRoad;
            newTriangle.IsTrainTracks = srcTri.IsTrainTracks;

            // Determine IsFlatGround based on slope
            var upVec = new Vector3(0, 0, 1);
            float flatDot = Vector3.Dot(newTriangle.Normal, upVec);
            float flatAngle = (float)Math.Acos(Math.Clamp(flatDot, -1f, 1f)) * 180f / (float)Math.PI;
            newTriangle.IsFlatGround = (flatAngle < 10.0f);

            // Check if triangle is too steep
            var upVector = new Vector3(0, 0, 1);
            float dotProduct = Vector3.Dot(newTriangle.Normal, upVector);
            float angle = (float)Math.Acos(Math.Clamp(dotProduct, -1f, 1f));
            float steepAngleRad = genParams.AngleForTooSteep * (float)Math.PI / 180f;

            if (angle > steepAngleRad)
            {
                newTriangle.IsTooSteep = true;
            }

            // Add triangle to nodes surrounding triangles
            gap.SharedNode1.SurroundingTriangles.Add(newTriangle);
            gap.SharedNode2.SurroundingTriangles.Add(newTriangle);
            gap.ThirdNode.SurroundingTriangles.Add(newTriangle);

            return newTriangle;
        }

        private bool ValidateGapFillingTriangle(NavSurfaceTri newTriangle, List<NavSurfaceTri> existingTriangles)
        {
            if (newTriangle == null || newTriangle.Nodes == null || newTriangle.Nodes.Length < 3)
                return false;

            // Check that the triangle doesnt have degenerate geometry
            var v0 = newTriangle.Nodes[0].BasePosition;
            var v1 = newTriangle.Nodes[1].BasePosition;
            var v2 = newTriangle.Nodes[2].BasePosition;

            // Check for colinear vertices
            var edge1 = v1 - v0;
            var edge2 = v2 - v0;
            var cross = Vector3.Cross(edge1, edge2);

            if (cross.LengthSquared() < 1e-6f)
            {
                return false; // Degenerate triangle
            }

            // Check that the triangle doesnt intersect with existing triangles
            // Well do a simple check: ensure the triangles center doesnt fall inside any existing triangle
            // and that edges dont cross existing triangle edges
            var center = (v0 + v1 + v2) / 3.0f;

            foreach (var existingTri in existingTriangles)
            {
                if (existingTri.IsRemoved || existingTri.Nodes == null || existingTri.Nodes.Length < 3)
                    continue;

                // Skip if triangles share nodes (theyre adjacent, not intersecting)
                int sharedNodes = 0;
                foreach (var newNode in newTriangle.Nodes)
                {
                    foreach (var existingNode in existingTri.Nodes)
                    {
                        if (newNode == existingNode)
                        {
                            sharedNodes++;
                            break;
                        }
                    }
                }

                if (sharedNodes >= 2)
                {
                    // Triangles share an edge, this is OK
                    continue;
                }

                // Check if triangles are on very different Z levels (no intersection possible)
                var existingCenter = (existingTri.Nodes[0].BasePosition + 
                                     existingTri.Nodes[1].BasePosition + 
                                     existingTri.Nodes[2].BasePosition) / 3.0f;

                float zDiff = Math.Abs(center.Z - existingCenter.Z);
                if (zDiff > genParams.TriangulationMaxHeightDiff * 2.0f)
                {
                    // Triangles are far apart vertically, no intersection
                    continue;
                }

                // Check if the new triangles center is inside the existing triangle
                if (PointInTriangle2D(center, existingTri.Nodes[0].BasePosition, 
                                     existingTri.Nodes[1].BasePosition, 
                                     existingTri.Nodes[2].BasePosition))
                {
                    // Check Z distance to see if they actually overlap
                    float expectedZ = CalculateZOnTriangle(center, existingTri);
                    if (Math.Abs(center.Z - expectedZ) < 0.5f)
                    {
                        return false; // Triangles overlap
                    }
                }
            }

            return true;
        }

        private bool PointInTriangle2D(Vector3 point, Vector3 v0, Vector3 v1, Vector3 v2)
        {
            // Use barycentric coordinates
            float denom = ((v1.Y - v2.Y) * (v0.X - v2.X) + (v2.X - v1.X) * (v0.Y - v2.Y));

            if (Math.Abs(denom) < 1e-6f)
                return false; // Degenerate triangle

            float a = ((v1.Y - v2.Y) * (point.X - v2.X) + (v2.X - v1.X) * (point.Y - v2.Y)) / denom;
            float b = ((v2.Y - v0.Y) * (point.X - v2.X) + (v0.X - v2.X) * (point.Y - v2.Y)) / denom;
            float c = 1.0f - a - b;

            return a >= 0 && a <= 1 && b >= 0 && b <= 1 && c >= 0 && c <= 1;
        }

        private float CalculateZOnTriangle(Vector3 point, NavSurfaceTri triangle)
        {
            if (triangle.Nodes == null || triangle.Nodes.Length < 3)
                return 0f;

            // Use the plane equation: dot(normal, point) = planeDistance
            // Solve for Z: normal.Z * Z = planeDistance - normal.X * point.X - normal.Y * point.Y
            if (Math.Abs(triangle.Normal.Z) < 1e-6f)
                return triangle.Nodes[0].BasePosition.Z; // Vertical triangle, return any Z

            float z = (triangle.PlaneDistance - triangle.Normal.X * point.X - triangle.Normal.Y * point.Y) / triangle.Normal.Z;
            return z;
        }

        private void EstablishAdjacencyForNewTriangle(NavSurfaceTri newTriangle, List<NavSurfaceTri> allTriangles)
        {
            if (newTriangle == null || newTriangle.Nodes == null || newTriangle.Nodes.Length < 3)
                return;

            newTriangle.AdjacentTris = new NavSurfaceTri[3];

            // For each edge of the new triangle, find adjacent triangles
            for (int i = 0; i < 3; i++)
            {
                var n1 = newTriangle.Nodes[i];
                var n2 = newTriangle.Nodes[(i + 1) % 3];

                // Search for triangles that share this edge
                foreach (var tri in allTriangles)
                {
                    if (tri == newTriangle || tri.IsRemoved || tri.Nodes == null || tri.Nodes.Length < 3)
                        continue;

                    // Check if this triangle shares the edge (n1, n2)
                    bool hasN1 = false;
                    bool hasN2 = false;

                    foreach (var node in tri.Nodes)
                    {
                        if (node == n1) hasN1 = true;
                        if (node == n2) hasN2 = true;
                    }

                    if (hasN1 && hasN2)
                    {
                        // Found adjacent triangle
                        newTriangle.AdjacentTris[i] = tri;

                        // Update the adjacent triangles adjacency to point back to the new triangle
                        if (tri.AdjacentTris != null)
                        {
                            for (int j = 0; j < tri.AdjacentTris.Length; j++)
                            {
                                var adjN1 = tri.Nodes[j];
                                var adjN2 = tri.Nodes[(j + 1) % tri.Nodes.Length];

                                if ((adjN1 == n1 && adjN2 == n2) || (adjN1 == n2 && adjN2 == n1))
                                {
                                    tri.AdjacentTris[j] = newTriangle;
                                    break;
                                }
                            }
                        }

                        break;
                    }
                }
            }
        }

        public int SmoothBoundaryEdges(Action<string>? statusCallback = null)
        {
            return SmoothBoundaryEdges(surfaceTriangles, statusCallback);
        }

        public int SmoothBoundaryEdges(List<NavSurfaceTri> triangles, Action<string>? statusCallback = null)
        {
            if (triangles == null || triangles.Count == 0)
            {
                statusCallback?.Invoke("Error: No triangles to process");
                return 0;
            }

            statusCallback?.Invoke("Starting boundary edge smoothing...");

            // 1) Identify boundary edges (edges with only one adjacent triangle)
            statusCallback?.Invoke("Identifying boundary edges...");
            var boundaryEdges = IdentifyBoundaryEdges(triangles);
            statusCallback?.Invoke($"Found {boundaryEdges.Count} boundary edges");

            if (boundaryEdges.Count == 0)
            {
                statusCallback?.Invoke("No boundary edges found, skipping smoothing");
                return 0;
            }

            // 2) Identify vertices with aliasing artifacts (jagged patterns)
            statusCallback?.Invoke("Identifying vertices with aliasing artifacts...");
            var verticesToSmooth = IdentifyAliasingVertices(boundaryEdges);
            statusCallback?.Invoke($"Found {verticesToSmooth.Count} vertices with aliasing artifacts");

            if (verticesToSmooth.Count == 0)
            {
                statusCallback?.Invoke("No aliasing artifacts found, skipping smoothing");
                return 0;
            }

            // 3) Calculate optimal positions for each vertex
            statusCallback?.Invoke("Calculating optimal vertex positions...");
            var vertexMoves = CalculateOptimalVertexPositions(verticesToSmooth, boundaryEdges);
            statusCallback?.Invoke($"Calculated {vertexMoves.Count} vertex moves");

            // 4) Validate and apply moves
            statusCallback?.Invoke("Validating and applying vertex moves...");
            int movedCount = 0;
            int processedCount = 0;

            foreach (var move in vertexMoves)
            {
                processedCount++;

                // Validate that moving this vertex wont create invalid triangles
                if (ValidateVertexMove(move.Key, move.Value, triangles))
                {
                    move.Key.BasePosition = move.Value;
                    movedCount++;

                    // Recalculate normals for affected triangles
                    foreach (var tri in move.Key.SurroundingTriangles)
                    {
                        if (!tri.IsRemoved)
                        {
                            tri.CalculateNormal();
                        }
                    }
                }

                if (processedCount % 10 == 0)
                {
                    float progress = (float)processedCount / vertexMoves.Count * 100f;
                    statusCallback?.Invoke($"Smoothing: {progress:F1}% ({movedCount} vertices moved)");
                }
            }

            // 5) Re-establish adjacency relationships (in case they changed)
            if (movedCount > 0)
            {
                statusCallback?.Invoke("Re-establishing triangle adjacency...");
                EstablishTriangleAdjacency(triangles);
            }

            statusCallback?.Invoke($"Boundary edge smoothing complete: {movedCount} vertices moved from {verticesToSmooth.Count} candidates");

            return movedCount;
        }

        private HashSet<NavGenNode> IdentifyAliasingVertices(List<BoundaryEdge> boundaryEdges)
        {
            var aliasingVertices = new HashSet<NavGenNode>();

            // Build a graph of boundary vertices and their connections
            var vertexConnections = new Dictionary<NavGenNode, List<NavGenNode>>();

            foreach (var edge in boundaryEdges)
            {
                if (!vertexConnections.ContainsKey(edge.Node1))
                {
                    vertexConnections[edge.Node1] = new List<NavGenNode>();
                }
                if (!vertexConnections.ContainsKey(edge.Node2))
                {
                    vertexConnections[edge.Node2] = new List<NavGenNode>();
                }

                vertexConnections[edge.Node1].Add(edge.Node2);
                vertexConnections[edge.Node2].Add(edge.Node1);
            }

            // For each boundary vertex, check if its part of a jagged pattern
            foreach (var kvp in vertexConnections)
            {
                var vertex = kvp.Key;
                var connections = kvp.Value;

                // A vertex is potentially aliased if it has exactly 2 boundary connections
                // (its in the middle of a boundary chain, not a corner or endpoint)
                if (connections.Count != 2)
                    continue;

                var prev = connections[0];
                var next = connections[1];

                // Calculate vectors to previous and next vertices
                var toPrev = prev.BasePosition - vertex.BasePosition;
                var toNext = next.BasePosition - vertex.BasePosition;

                float prevLength = toPrev.Length();
                float nextLength = toNext.Length();

                if (prevLength < 1e-6f || nextLength < 1e-6f)
                    continue;

                toPrev /= prevLength;
                toNext /= nextLength;

                // Calculate the angle between the two edges
                float dotProduct = Vector3.Dot(toPrev, toNext);
                float angle = (float)Math.Acos(Math.Clamp(dotProduct, -1f, 1f));

                // If the angle is close to 180 degrees (straight line), this vertex is colinear and doesnt need smoothing
                const float straightLineThreshold = 170.0f * (float)Math.PI / 180f; // 170 degrees

                if (angle > straightLineThreshold)
                    continue;

                // If the angle is very sharp (< 90 degrees), this might be an intentional corner
                const float sharpCornerThreshold = 90.0f * (float)Math.PI / 180f; // 90 degrees

                if (angle < sharpCornerThreshold)
                    continue;

                // Check if this vertex creates a "staircase" pattern
                // This happens when the edges alternate between primarily X and Y directions
                bool isPrevPrimarilyX = Math.Abs(toPrev.X) > Math.Abs(toPrev.Y);
                bool isNextPrimarilyX = Math.Abs(toNext.X) > Math.Abs(toNext.Y);

                // If one edge is primarily X and the other is primarily Y, this is a staircase pattern
                if (isPrevPrimarilyX != isNextPrimarilyX)
                {
                    aliasingVertices.Add(vertex);
                }
                else
                {
                    // Also check for small deviations from a straight line
                    // Calculate the distance from the vertex to the line between prev and next
                    var prevToNext = next.BasePosition - prev.BasePosition;
                    var prevToVertex = vertex.BasePosition - prev.BasePosition;

                    // Project prevToVertex onto prevToNext
                    float t = Vector3.Dot(prevToVertex, prevToNext) / prevToNext.LengthSquared();
                    t = Math.Clamp(t, 0f, 1f);

                    var closestPointOnLine = prev.BasePosition + prevToNext * t;
                    float distanceToLine = (vertex.BasePosition - closestPointOnLine).Length();

                    // If the vertex is close to the line but not on it, its an aliasing artifact
                    const float aliasingThreshold = 0.5f; // meters

                    if (distanceToLine > 0.1f && distanceToLine < aliasingThreshold)
                    {
                        aliasingVertices.Add(vertex);
                    }
                }
            }

            return aliasingVertices;
        }

        private Dictionary<NavGenNode, Vector3> CalculateOptimalVertexPositions(HashSet<NavGenNode> verticesToSmooth, List<BoundaryEdge> boundaryEdges)
        {
            var vertexMoves = new Dictionary<NavGenNode, Vector3>();

            // Build vertex connection graph
            var vertexConnections = new Dictionary<NavGenNode, List<NavGenNode>>();

            foreach (var edge in boundaryEdges)
            {
                if (!vertexConnections.ContainsKey(edge.Node1))
                {
                    vertexConnections[edge.Node1] = new List<NavGenNode>();
                }
                if (!vertexConnections.ContainsKey(edge.Node2))
                {
                    vertexConnections[edge.Node2] = new List<NavGenNode>();
                }

                vertexConnections[edge.Node1].Add(edge.Node2);
                vertexConnections[edge.Node2].Add(edge.Node1);
            }

            // For each vertex to smooth, calculate the optimal position
            foreach (var vertex in verticesToSmooth)
            {
                if (!vertexConnections.TryGetValue(vertex, out var connections))
                    continue;

                if (connections.Count != 2)
                    continue;

                var prev = connections[0];
                var next = connections[1];

                // Calculate the optimal position as the point on the line between prev and next
                // that is closest to the current vertex position
                var prevToNext = next.BasePosition - prev.BasePosition;
                var prevToVertex = vertex.BasePosition - prev.BasePosition;

                // Project prevToVertex onto prevToNext
                float t = Vector3.Dot(prevToVertex, prevToNext) / prevToNext.LengthSquared();
                t = Math.Clamp(t, 0f, 1f);

                var optimalPosition = prev.BasePosition + prevToNext * t;

                // Limit the maximum move distance to avoid drastic changes
                const float maxMoveDistance = 1.0f; // meters

                var moveVector = optimalPosition - vertex.BasePosition;
                float moveDistance = moveVector.Length();

                if (moveDistance > maxMoveDistance)
                {
                    moveVector = moveVector / moveDistance * maxMoveDistance;
                    optimalPosition = vertex.BasePosition + moveVector;
                }

                // Only move if the distance is significant
                const float minMoveDistance = 0.05f; // meters

                if (moveDistance >= minMoveDistance)
                {
                    vertexMoves[vertex] = optimalPosition;
                }
            }

            return vertexMoves;
        }

        private bool ValidateVertexMove(NavGenNode? vertex, Vector3 newPosition, List<NavSurfaceTri> allTriangles)
        {
            if (vertex == null || vertex.SurroundingTriangles == null)
                return false;

            // Check all triangles that use this vertex
            foreach (var tri in vertex.SurroundingTriangles)
            {
                if (tri.IsRemoved || tri.Nodes == null || tri.Nodes.Length < 3)
                    continue;

                // Calculate what the triangle would look like after the move
                var v0 = tri.Nodes[0] == vertex ? newPosition : tri.Nodes[0].BasePosition;
                var v1 = tri.Nodes[1] == vertex ? newPosition : tri.Nodes[1].BasePosition;
                var v2 = tri.Nodes[2] == vertex ? newPosition : tri.Nodes[2].BasePosition;

                // 1) Triangle must not become degenerate (zero area)
                var edge1 = v1 - v0;
                var edge2 = v2 - v0;
                var cross = Vector3.Cross(edge1, edge2);
                float newArea = cross.Length() * 0.5f;

                if (newArea < genParams.MinTriangleArea)
                {
                    return false; // Triangle would become too small
                }

                // 2) Triangle normal must not flip
                var newNormal = cross;
                if (newNormal.LengthSquared() > 0)
                {
                    newNormal.Normalize();
                }

                float dotProduct = Vector3.Dot(tri.Normal, newNormal);
                if (dotProduct < 0.0f)
                {
                    return false; // Normal would flip
                }

                // 3) Triangle must not become too elongated
                float edge1Length = edge1.Length();
                float edge2Length = edge2.Length();
                var edge3 = v2 - v1;
                float edge3Length = edge3.Length();

                float maxEdgeLength = Math.Max(Math.Max(edge1Length, edge2Length), edge3Length);
                float minEdgeLength = Math.Min(Math.Min(edge1Length, edge2Length), edge3Length);

                // Aspect ratio check, longest edge should not be more than 10x the shortest
                if (maxEdgeLength > minEdgeLength * 10.0f)
                {
                    return false; // Triangle would become too elongated
                }

                // 4) Edge lengths must be within acceptable range
                if (maxEdgeLength > genParams.MaxTriangleSideLength)
                {
                    return false; // Edge would be too long
                }

                if (minEdgeLength < genParams.MinTriangleSideLength)
                {
                    return false; // Edge would be too short
                }

                // 5) Triangle angles must be reasonable
                var angle1 = CalculateAngle(v0, v1, v2);
                var angle2 = CalculateAngle(v1, v2, v0);
                var angle3 = CalculateAngle(v2, v0, v1);

                float minAngleDegrees = genParams.MinTriangleAngle;
                float minAngleRad = minAngleDegrees * (float)Math.PI / 180f;

                if (angle1 < minAngleRad || angle2 < minAngleRad || angle3 < minAngleRad)
                {
                    return false; // Triangle would have too sharp an angle
                }
            }

            // Check that the vertex doesnt move into another triangle
            foreach (var tri in allTriangles)
            {
                if (tri.IsRemoved || tri.Nodes == null || tri.Nodes.Length < 3)
                    continue;

                // Skip triangles that use this vertex
                bool usesVertex = false;
                foreach (var node in tri.Nodes)
                {
                    if (node == vertex)
                    {
                        usesVertex = true;
                        break;
                    }
                }

                if (usesVertex)
                    continue;

                // Check if the new position would be inside this triangle
                if (PointInTriangle2D(newPosition, tri.Nodes[0].BasePosition, tri.Nodes[1].BasePosition, tri.Nodes[2].BasePosition))
                {
                    // Check Z distance to see if they actually overlap
                    float expectedZ = CalculateZOnTriangle(newPosition, tri);
                    if (Math.Abs(newPosition.Z - expectedZ) < 0.5f)
                    {
                        return false; // Vertex would move into another triangle
                    }
                }
            }

            return true;
        }

        private float CalculateAngle(Vector3 v0, Vector3 v1, Vector3 v2)
        {
            var edge1 = v0 - v1;
            var edge2 = v2 - v1;

            float edge1Length = edge1.Length();
            float edge2Length = edge2.Length();

            if (edge1Length < 1e-6f || edge2Length < 1e-6f)
                return 0f;

            edge1 /= edge1Length;
            edge2 /= edge2Length;

            float dotProduct = Vector3.Dot(edge1, edge2);
            return (float)Math.Acos(Math.Clamp(dotProduct, -1f, 1f));
        }

        public int RemoveSmallPatches(Action<string>? statusCallback = null)
        {
            return RemoveSmallPatches(surfacePolygons, statusCallback);
        }

        public int RemoveSmallPatches(List<NavSurfacePoly> polygons, Action<string>? statusCallback = null)
        {
            if (polygons == null || polygons.Count == 0)
            {
                statusCallback?.Invoke("Error: No polygons to process");
                return 0;
            }

            statusCallback?.Invoke("Starting small patch removal...");

            // 1) Build adjacency information for polygons
            statusCallback?.Invoke("Building polygon adjacency information...");
            BuildPolygonAdjacency(polygons);

            // 2) Find connected regions using flood fill
            statusCallback?.Invoke("Finding connected regions...");
            var regions = FindConnectedRegions(polygons);
            statusCallback?.Invoke($"Found {regions.Count} connected regions");

            if (regions.Count == 0)
            {
                statusCallback?.Invoke("No regions found, skipping patch removal");
                return 0;
            }

            // 3) Calculate total area of each region
            statusCallback?.Invoke("Calculating region areas...");
            var regionAreas = new Dictionary<int, float>();
            foreach (var kvp in regions)
            {
                int regionId = kvp.Key;
                var regionPolygons = kvp.Value;

                float totalArea = 0f;
                foreach (var poly in regionPolygons)
                {
                    totalArea += poly.CalculateArea();
                }

                regionAreas[regionId] = totalArea;
            }

            // 4) Determine minimum area threshold
            // Use a reasonable default, 1 square meter
            float minAreaThreshold = 1.0f;

            // 5) Remove regions below threshold
            statusCallback?.Invoke($"Removing regions below {minAreaThreshold:F2} square meters...");
            int removedCount = 0;
            int processedRegions = 0;

            foreach (var kvp in regions)
            {
                int regionId = kvp.Key;
                var regionPolygons = kvp.Value;
                float regionArea = regionAreas[regionId];

                processedRegions++;

                if (regionArea < minAreaThreshold)
                {
                    // Mark all polygons in this region as removed
                    foreach (var poly in regionPolygons)
                    {
                        poly.IsRemoved = true;
                        removedCount++;
                    }

                    statusCallback?.Invoke($"Removed region {regionId} with area {regionArea:F2} m² ({regionPolygons.Count} polygons)");
                }

                if (processedRegions % 10 == 0)
                {
                    float progress = (float)processedRegions / regions.Count * 100f;
                    statusCallback?.Invoke($"Patch removal: {progress:F1}% ({removedCount} polygons removed)");
                }
            }

            // 6) Update adjacencies after removal
            if (removedCount > 0)
            {
                statusCallback?.Invoke("Updating adjacencies after removal...");
                UpdatePolygonAdjacenciesAfterRemoval(polygons);
            }

            statusCallback?.Invoke($"Small patch removal complete: {removedCount} polygons removed from {regions.Count} regions");

            return removedCount;
        }

        private void BuildPolygonAdjacency(List<NavSurfacePoly> polygons)
        {
            var edgeToPolygons = new Dictionary<(NavGenNode, NavGenNode), List<NavSurfacePoly>>();

            foreach (var poly in polygons)
            {
                if (poly.IsRemoved || poly.Vertices == null || poly.Vertices.Count < 3)
                    continue;

                // Initialize adjacency list if needed
                if (poly.AdjacentPolys == null)
                {
                    poly.AdjacentPolys = new List<NavSurfacePoly>();
                }
                else
                {
                    poly.AdjacentPolys.Clear();
                }

                // Add all edges of this polygon to the mapping
                for (int i = 0; i < poly.Vertices.Count; i++)
                {
                    var v1 = poly.Vertices[i];
                    var v2 = poly.Vertices[(i + 1) % poly.Vertices.Count];

                    // Create edge key (order independent)
                    var edgeKey = v1.GetHashCode() < v2.GetHashCode() ? (v1, v2) : (v2, v1);

                    if (!edgeToPolygons.ContainsKey(edgeKey))
                    {
                        edgeToPolygons[edgeKey] = new List<NavSurfacePoly>();
                    }

                    edgeToPolygons[edgeKey].Add(poly);
                }
            }

            // Build adjacency lists for each polygon
            foreach (var poly in polygons)
            {
                if (poly.IsRemoved || poly.Vertices == null || poly.Vertices.Count < 3)
                    continue;

                // Find adjacent polygons by checking shared edges
                var adjacentPolygons = new HashSet<NavSurfacePoly>();

                for (int i = 0; i < poly.Vertices.Count; i++)
                {
                    var v1 = poly.Vertices[i];
                    var v2 = poly.Vertices[(i + 1) % poly.Vertices.Count];

                    var edgeKey = v1.GetHashCode() < v2.GetHashCode() ? (v1, v2) : (v2, v1);

                    if (edgeToPolygons.TryGetValue(edgeKey, out var edgePolygons))
                    {
                        foreach (var adjacentPoly in edgePolygons)
                        {
                            if (adjacentPoly != poly && !adjacentPoly.IsRemoved)
                            {
                                adjacentPolygons.Add(adjacentPoly);
                            }
                        }
                    }
                }

                poly.AdjacentPolys = adjacentPolygons.ToList();
            }
        }

        private Dictionary<int, List<NavSurfacePoly>> FindConnectedRegions(List<NavSurfacePoly> polygons)
        {
            var regions = new Dictionary<int, List<NavSurfacePoly>>();
            var visited = new HashSet<NavSurfacePoly>();
            int currentRegionId = 0;

            foreach (var poly in polygons)
            {
                if (poly.IsRemoved || visited.Contains(poly))
                    continue;

                // Start a new region with flood fill from this polygon
                var regionPolygons = new List<NavSurfacePoly>();
                FloodFillRegion(poly, visited, regionPolygons);

                if (regionPolygons.Count > 0)
                {
                    regions[currentRegionId] = regionPolygons;
                    currentRegionId++;
                }
            }

            return regions;
        }

        private void FloodFillRegion(NavSurfacePoly startPoly, HashSet<NavSurfacePoly> visited, List<NavSurfacePoly> regionPolygons)
        {
            // Use iterative flood fill with a queue to avoid stack overflow
            var queue = new Queue<NavSurfacePoly>();
            queue.Enqueue(startPoly);
            visited.Add(startPoly);

            while (queue.Count > 0)
            {
                var currentPoly = queue.Dequeue();
                regionPolygons.Add(currentPoly);

                // Add all unvisited adjacent polygons to the queue
                if (currentPoly.AdjacentPolys != null)
                {
                    foreach (var adjacentPoly in currentPoly.AdjacentPolys)
                    {
                        if (adjacentPoly == null || adjacentPoly.IsRemoved || visited.Contains(adjacentPoly))
                            continue;

                        visited.Add(adjacentPoly);
                        queue.Enqueue(adjacentPoly);
                    }
                }
            }
        }

        private void UpdatePolygonAdjacenciesAfterRemoval(List<NavSurfacePoly> polygons)
        {
            foreach (var poly in polygons)
            {
                if (poly.IsRemoved || poly.AdjacentPolys == null)
                    continue;

                // Remove any adjacent polygons that have been marked as removed
                poly.AdjacentPolys.RemoveAll(adj => adj == null || adj.IsRemoved);
            }
        }

        public List<NavSurfacePoly> SplitPolygonsIntoGridCells(Action<string>? statusCallback = null)
        {
            return SplitPolygonsIntoGridCells(surfacePolygons, statusCallback);
        }

        public List<NavSurfacePoly> SplitPolygonsIntoGridCells(List<NavSurfacePoly> polygons, Action<string>? statusCallback = null)
        {
            if (polygons == null || polygons.Count == 0)
            {
                statusCallback?.Invoke("Error: No polygons to split");
                return new List<NavSurfacePoly>();
            }

            if (NavGrid == null)
            {
                NavGrid = new SpaceNavGrid();
                // Apply R* grid parameters: WorldMin=(-6000,-6000), CellSize=150 (SectorWidth * SectorsPerNavMesh)
                NavGrid.CellSize = genParams.NavGridCellSize;
                NavGrid.CellSizeInv = 1.0f / NavGrid.CellSize;
                NavGrid.CornerX = genParams.WorldMinX;
                NavGrid.CornerY = genParams.WorldMinY;
                // Recalculate cell counts based on world extent (15000 / 150 = 100)
                float worldExtent = 15000.0f; // -6000 to 9000
                NavGrid.CellCountX = Math.Max(1, (int)(worldExtent / NavGrid.CellSize));
                NavGrid.CellCountY = Math.Max(1, (int)(worldExtent / NavGrid.CellSize));
                // Reinitialize cells array with correct dimensions
                NavGrid.Cells = new SpaceNavGridCell[NavGrid.CellCountX, NavGrid.CellCountY];
                for (int x = 0; x < NavGrid.CellCountX; x++)
                {
                    for (int y = 0; y < NavGrid.CellCountY; y++)
                    {
                        NavGrid.Cells[x, y] = new SpaceNavGridCell(x, y);
                    }
                }
            }

            statusCallback?.Invoke("Starting grid cell splitting...");

            // 1) Split along X axis boundaries
            statusCallback?.Invoke("Splitting polygons along X boundaries...");
            var splitPolysX = SplitSurfacePolygons(polygons, true);
            statusCallback?.Invoke($"X-axis split complete: {splitPolysX.Count} polygons");

            // 2) Split along Y axis boundaries
            statusCallback?.Invoke("Splitting polygons along Y boundaries...");
            var splitPolysY = SplitSurfacePolygons(splitPolysX, false);
            statusCallback?.Invoke($"Y-axis split complete: {splitPolysY.Count} polygons");

            // Store the split polygons
            surfacePolygons = splitPolysY;

            statusCallback?.Invoke($"Grid cell splitting complete: {polygons.Count} polygons split into {splitPolysY.Count} polygons");

            return splitPolysY;
        }

        private List<NavSurfacePoly> SplitSurfacePolygons(List<NavSurfacePoly> polygons, bool xaxis)
        {
            var newPolygons = new List<NavSurfacePoly>();

            foreach (var poly in polygons)
            {
                if (poly.IsRemoved || poly.Vertices == null || poly.Vertices.Count < 3)
                    continue;

                // Check if polygon crosses a grid boundary
                Vector2I firstCellPos = NavGrid.GetCellPos(poly.Vertices[0].BasePosition);
                int split1 = 0;
                int split2 = 0;

                for (int i = 1; i < poly.Vertices.Count; i++)
                {
                    Vector2I cellPos = NavGrid.GetCellPos(poly.Vertices[i].BasePosition);
                    int coord1 = xaxis ? cellPos.X : cellPos.Y;
                    int coord2 = xaxis ? firstCellPos.X : firstCellPos.Y;

                    if (coord1 != coord2) // Polygon crosses a boundary
                    {
                        if (split1 == 0)
                        {
                            split1 = i;
                        }
                        else
                        {
                            split2 = i;
                            break;
                        }
                    }

                    firstCellPos = cellPos;
                }

                if (split1 > 0)
                {
                    // Polygon crosses at least one boundary, need to split it
                    var split2beg = (split2 > 0) ? split2 - 1 : poly.Vertices.Count - 1;
                    var split2end = split2beg + 1;

                    var sv11 = poly.Vertices[split1 - 1].BasePosition;
                    var sv12 = poly.Vertices[split1].BasePosition;
                    var sv21 = poly.Vertices[split2beg].BasePosition;
                    var sv22 = poly.Vertices[split2 % poly.Vertices.Count].BasePosition;

                    var sp1 = GetSplitPos(sv11, sv12, xaxis);
                    var sp2 = GetSplitPos(sv21, sv22, xaxis);

                    // Validate split
                    if (!IsValidSplit(sp1, sp2, sv11, sv12, sv21, sv22))
                    {
                        // Split did nothing, keep polygon as is
                        newPolygons.Add(poly);
                    }
                    else
                    {
                        // Create two new polygons from the split, preserving all flags
                        var poly1 = new NavSurfacePoly
                        {
                            Material = poly.Material,
                            IsWater = poly.IsWater,
                            IsTooSteep = poly.IsTooSteep,
                            PolyFlags = poly.PolyFlags,
                            ProceduralId = poly.ProceduralId,
                            RoomId = poly.RoomId,
                            PedDensity = poly.PedDensity,
                            MaterialFlags = poly.MaterialFlags,
                            IsInterior = poly.IsInterior,
                            IsRoad = poly.IsRoad,
                            IsTrainTracks = poly.IsTrainTracks,
                            IsFlatGround = poly.IsFlatGround,
                            IsShallowWater = poly.IsShallowWater,
                            Vertices = new List<NavGenNode>()
                        };

                        var poly2 = new NavSurfacePoly
                        {
                            Material = poly.Material,
                            IsWater = poly.IsWater,
                            IsTooSteep = poly.IsTooSteep,
                            PolyFlags = poly.PolyFlags,
                            ProceduralId = poly.ProceduralId,
                            RoomId = poly.RoomId,
                            PedDensity = poly.PedDensity,
                            MaterialFlags = poly.MaterialFlags,
                            IsInterior = poly.IsInterior,
                            IsRoad = poly.IsRoad,
                            IsTrainTracks = poly.IsTrainTracks,
                            IsFlatGround = poly.IsFlatGround,
                            IsShallowWater = poly.IsShallowWater,
                            Vertices = new List<NavGenNode>()
                        };

                        // Create new nodes for the split points
                        var splitNode1 = new NavGenNode
                        {
                            BasePosition = sp1,
                            Material = poly.Material,
                            IsWater = poly.IsWater
                        };

                        var splitNode2 = new NavGenNode
                        {
                            BasePosition = sp2,
                            Material = poly.Material,
                            IsWater = poly.IsWater
                        };

                        // Build vertex list for poly1
                        for (int i = 0; i < split1; i++)
                        {
                            poly1.Vertices.Add(poly.Vertices[i]);
                        }
                        poly1.Vertices.Add(splitNode1);
                        poly1.Vertices.Add(splitNode2);
                        for (int i = split2end; i < poly.Vertices.Count; i++)
                        {
                            poly1.Vertices.Add(poly.Vertices[i]);
                        }

                        // Build vertex list for poly2
                        poly2.Vertices.Add(splitNode1);
                        for (int i = split1; i < split2end; i++)
                        {
                            poly2.Vertices.Add(poly.Vertices[i]);
                        }
                        poly2.Vertices.Add(splitNode2);

                        // Recalculate normals and planes
                        poly1.CalculateNormalAndPlane();
                        poly2.CalculateNormalAndPlane();

                        newPolygons.Add(poly1);
                        newPolygons.Add(poly2);
                    }
                }
                else
                {
                    // Polygon doesnt cross any boundaries, keep as is
                    newPolygons.Add(poly);
                }
            }

            return newPolygons;
        }

        public List<YnvFile> ConvertToYnvFiles(Action<string>? statusCallback = null)
        {
            return ConvertToYnvFiles(surfacePolygons, statusCallback);
        }

        public List<YnvFile> ConvertToYnvFiles(List<NavSurfacePoly> polygons, Action<string>? statusCallback = null)
        {
            if (polygons == null || polygons.Count == 0)
            {
                statusCallback?.Invoke("Error: No polygons to convert");
                return new List<YnvFile>();
            }

            if (NavGrid == null)
            {
                NavGrid = new SpaceNavGrid();
                NavGrid.CellSize = genParams.NavGridCellSize;
                NavGrid.CellSizeInv = 1.0f / NavGrid.CellSize;
                NavGrid.CornerX = genParams.WorldMinX;
                NavGrid.CornerY = genParams.WorldMinY;
                float worldExtent = 15000.0f;
                NavGrid.CellCountX = Math.Max(1, (int)(worldExtent / NavGrid.CellSize));
                NavGrid.CellCountY = Math.Max(1, (int)(worldExtent / NavGrid.CellSize));
                NavGrid.Cells = new SpaceNavGridCell[NavGrid.CellCountX, NavGrid.CellCountY];
                for (int x = 0; x < NavGrid.CellCountX; x++)
                {
                    for (int y = 0; y < NavGrid.CellCountY; y++)
                    {
                        NavGrid.Cells[x, y] = new SpaceNavGridCell(x, y);
                    }
                }
            }

            YnvFiles = new List<YnvFile>();

            statusCallback?.Invoke("Converting polygons to YNV format...");

            int convertedCount = 0;

            foreach (var poly in polygons)
            {
                if (poly.IsRemoved || poly.Vertices == null || poly.Vertices.Count < 3)
                    continue;

                // Convert to YnvPoly
                var ynvPoly = poly.ToYnvPoly();
                if (ynvPoly == null)
                    continue;

                // Calculate position and determine grid cell
                ynvPoly.CalculatePosition();
                var pos = ynvPoly.Position;
                var cell = NavGrid.GetCell(pos);

                // Get or create YNV file for this cell
                var ynv = cell.Ynv;
                if (ynv == null)
                {
                    ynv = new YnvFile();
                    ynv.Name = "navmesh[" + cell.FileX.ToString() + "][" + cell.FileY.ToString() + "]";
                    ynv.Nav = new NavMesh();
                    ynv.Nav.SetDefaults(false);
                    ynv.Nav.AABBSize = new Vector3(NavGrid.CellSize, NavGrid.CellSize, 0.0f);
                    ynv.Nav.SectorTree = new NavMeshSector();
                    // R* sets initial Z bounds from exporter cutoffs: min=-500, max=1000
                    var cellMin = NavGrid.GetCellMin(cell);
                    var cellMax = NavGrid.GetCellMax(cell);
                    ynv.Nav.SectorTree.AABBMin = new Vector4(cellMin.X, cellMin.Y, genParams.ExporterMinZCutoff, 0.0f);
                    ynv.Nav.SectorTree.AABBMax = new Vector4(cellMax.X, cellMax.Y, genParams.ExporterMaxZCutoff, 0.0f);
                    ynv.AreaID = cell.X + cell.Y * 100;
                    ynv.Polys = new List<YnvPoly>();
                    ynv.HasChanged = true;
                    ynv.RpfFileEntry = new RpfResourceFileEntry();
                    ynv.RpfFileEntry.Name = ynv.Name + ".ynv";
                    ynv.RpfFileEntry.Path = string.Empty;
                    cell.Ynv = ynv;
                    YnvFiles.Add(ynv);
                }

                // Set polygon properties
                ynvPoly.AreaID = (ushort)ynv.AreaID;
                ynvPoly.Index = ynv.Polys.Count;
                ynvPoly.Ynv = ynv;
                ynv.Polys.Add(ynvPoly);

                convertedCount++;
            }

            statusCallback?.Invoke($"Converted {convertedCount} polygons into {YnvFiles.Count} YNV files");

            // Link adjacent polygon edges (internal + cross-cell)
            LinkEdges(statusCallback);

            // Finalize YNV files
            FinalizeYnvs(YnvFiles, false);

            return YnvFiles;
        }

        // ==========================================
        // Edge linking methods
        // ==========================================

        /// <summary>
        /// Quantize a vertex position to a string key for edge matching.
        /// Uses 0.01 precision to handle floating point imprecision from polygon splitting.
        /// </summary>
        private static string VKey(Vector3 v)
        {
            return $"{Math.Round(v.X * 100)},{Math.Round(v.Y * 100)},{Math.Round(v.Z * 100)}";
        }

        private static string EKey(Vector3 v1, Vector3 v2)
        {
            return VKey(v1) + "|" + VKey(v2);
        }

        /// <summary>
        /// Links all polygon edges: internal (within each YNV) and cross-cell (between generated YNVs).
        /// Must be called after ConvertToYnvFiles populates YnvFiles but before FinalizeYnvs.
        /// </summary>
        private void LinkEdges(Action<string>? statusCallback = null)
        {
            statusCallback?.Invoke("Linking polygon edges...");

            // Phase 1: Link internal edges within each YNV
            int totalInternal = 0;
            foreach (var ynv in YnvFiles)
            {
                totalInternal += LinkInternalEdges(ynv);
            }
            statusCallback?.Invoke($"Linked {totalInternal} internal edge pairs across {YnvFiles.Count} YNV files");

            // Phase 2: Link cross-cell edges between generated YNVs
            int totalCrossCell = LinkCrossCellEdges();
            statusCallback?.Invoke($"Linked {totalCrossCell} cross-cell edge pairs");
        }

        /// <summary>
        /// Links edges between polygons within a single YNV file.
        /// Two polygons share an edge if they have two consecutive vertices at the same positions
        /// but in reverse winding order.
        /// </summary>
        private int LinkInternalEdges(YnvFile ynv)
        {
            if (ynv.Polys == null) return 0;

            int linked = 0;
            var edgeMap = new Dictionary<string, (YnvPoly poly, int edgeIdx)>();

            foreach (var poly in ynv.Polys)
            {
                if (poly.Vertices == null || poly.Edges == null) continue;
                if (poly.Edges.Length != poly.Vertices.Length) continue;

                for (int i = 0; i < poly.Vertices.Length; i++)
                {
                    var v1 = poly.Vertices[i];
                    var v2 = poly.Vertices[(i + 1) % poly.Vertices.Length];

                    // Look for reverse edge (another polygon sharing this edge in opposite winding)
                    var reverseKey = EKey(v2, v1);
                    if (edgeMap.TryGetValue(reverseKey, out var match))
                    {
                        // Link both edges to each other's polygon
                        poly.Edges[i].Poly1 = match.poly;
                        poly.Edges[i].Poly2 = match.poly;
                        poly.Edges[i].AreaID1 = match.poly.AreaID;
                        poly.Edges[i].AreaID2 = match.poly.AreaID;

                        match.poly.Edges[match.edgeIdx].Poly1 = poly;
                        match.poly.Edges[match.edgeIdx].Poly2 = poly;
                        match.poly.Edges[match.edgeIdx].AreaID1 = poly.AreaID;
                        match.poly.Edges[match.edgeIdx].AreaID2 = poly.AreaID;

                        edgeMap.Remove(reverseKey);
                        linked++;
                    }
                    else
                    {
                        var forwardKey = EKey(v1, v2);
                        edgeMap.TryAdd(forwardKey, (poly, i));
                    }
                }
            }

            return linked;
        }

        /// <summary>
        /// Links edges between polygons in adjacent generated YNV files (different AreaIDs).
        /// Only processes each cell pair once (East and North neighbors).
        /// </summary>
        private int LinkCrossCellEdges()
        {
            if (YnvFiles == null || YnvFiles.Count < 2) return 0;

            int linked = 0;

            // Build dictionary of generated YNVs by AreaID
            var ynvByArea = new Dictionary<int, YnvFile>();
            foreach (var ynv in YnvFiles)
            {
                ynvByArea[ynv.AreaID] = ynv;
            }

            // For each generated YNV, check East (+1) and North (+100) neighbors to avoid duplicates
            var processed = new HashSet<(int, int)>();

            foreach (var ynv in YnvFiles)
            {
                int areaID = ynv.AreaID;

                foreach (int offset in new[] { 1, 100 })
                {
                    int neighborAreaID = areaID + offset;

                    if (!ynvByArea.TryGetValue(neighborAreaID, out var neighborYnv))
                        continue;

                    var pair = (Math.Min(areaID, neighborAreaID), Math.Max(areaID, neighborAreaID));
                    if (!processed.Add(pair)) continue;

                    linked += LinkBoundaryEdgesBetweenYnvs(ynv, neighborYnv);
                }
            }

            return linked;
        }

        /// <summary>
        /// Links shared boundary edges between two YNV files using vertex position matching.
        /// </summary>
        private int LinkBoundaryEdgesBetweenYnvs(YnvFile ynv1, YnvFile ynv2)
        {
            if (ynv1.Polys == null || ynv2.Polys == null) return 0;

            int linked = 0;

            // Build edge map from ynv2's unlinked edges
            var edgeMap = new Dictionary<string, (YnvPoly poly, int edgeIdx)>();

            foreach (var poly in ynv2.Polys)
            {
                if (poly.Vertices == null || poly.Edges == null) continue;
                if (poly.Edges.Length != poly.Vertices.Length) continue;

                for (int i = 0; i < poly.Vertices.Length; i++)
                {
                    // Only consider edges that are still self-referencing (unlinked)
                    if (poly.Edges[i].Poly1 != null && poly.Edges[i].Poly1 != poly)
                        continue;

                    var v1 = poly.Vertices[i];
                    var v2 = poly.Vertices[(i + 1) % poly.Vertices.Length];
                    var key = EKey(v1, v2);
                    edgeMap.TryAdd(key, (poly, i));
                }
            }

            // Match against ynv1's unlinked edges
            foreach (var poly in ynv1.Polys)
            {
                if (poly.Vertices == null || poly.Edges == null) continue;
                if (poly.Edges.Length != poly.Vertices.Length) continue;

                for (int i = 0; i < poly.Vertices.Length; i++)
                {
                    if (poly.Edges[i].Poly1 != null && poly.Edges[i].Poly1 != poly)
                        continue;

                    var v1 = poly.Vertices[i];
                    var v2 = poly.Vertices[(i + 1) % poly.Vertices.Length];
                    var reverseKey = EKey(v2, v1);

                    if (edgeMap.TryGetValue(reverseKey, out var match))
                    {
                        // Cross-cell link
                        poly.Edges[i].Poly1 = match.poly;
                        poly.Edges[i].Poly2 = match.poly;
                        poly.Edges[i].AreaID1 = match.poly.AreaID;
                        poly.Edges[i].AreaID2 = match.poly.AreaID;

                        match.poly.Edges[match.edgeIdx].Poly1 = poly;
                        match.poly.Edges[match.edgeIdx].Poly2 = poly;
                        match.poly.Edges[match.edgeIdx].AreaID1 = poly.AreaID;
                        match.poly.Edges[match.edgeIdx].AreaID2 = poly.AreaID;

                        edgeMap.Remove(reverseKey);
                        linked++;
                    }
                }
            }

            return linked;
        }

        /// <summary>
        /// Stitches generated navmeshes to existing adjacent navmeshes (R* DLC-style join).
        /// For boundary edges that face outside the generation area, loads the existing adjacent
        /// YNV and sets up cross-boundary edge references so pathfinding works across the seam.
        /// </summary>
        public int StitchToExistingNavMeshes(GameFileCache gameFileCache, Action<string>? statusCallback = null)
        {
            if (YnvFiles == null || YnvFiles.Count == 0 || NavGrid == null)
                return 0;

            statusCallback?.Invoke("Starting boundary stitching to existing navmeshes...");

            // Build set of generated AreaIDs for quick lookup
            var generatedAreaIDs = new HashSet<int>();
            foreach (var ynv in YnvFiles)
            {
                generatedAreaIDs.Add(ynv.AreaID);
            }

            int totalStitched = 0;
            // R* uses 0.125 for max dist from edge, 0.5 for segment distance - we use 0.5 for vertex tolerance
            float boundaryTolerance = 0.5f;

            foreach (var ynv in YnvFiles)
            {
                int areaID = ynv.AreaID;
                int cellX = areaID % 100;
                int cellY = areaID / 100;

                // Check 4 cardinal neighbors
                int[] neighborOffsets = { -1, 1, -100, 100 };  // West, East, South, North
                foreach (int offset in neighborOffsets)
                {
                    int neighborAreaID = areaID + offset;
                    int neighborX = neighborAreaID % 100;
                    int neighborY = neighborAreaID / 100;

                    // Skip if out of grid bounds
                    if (neighborX < 0 || neighborX >= 100 || neighborY < 0 || neighborY >= 100)
                        continue;

                    // Skip if neighbor was also generated (already linked by LinkCrossCellEdges)
                    if (generatedAreaIDs.Contains(neighborAreaID))
                        continue;

                    // Try to load existing adjacent YNV from game files
                    YnvFile? existingYnv = null;
                    try
                    {
                        existingYnv = gameFileCache.GetYnv((uint)neighborAreaID);
                        if (existingYnv != null && !existingYnv.Loaded)
                        {
                            int waitCount = 0;
                            while (!existingYnv.Loaded && waitCount < 200)
                            {
                                System.Threading.Thread.Sleep(10);
                                waitCount++;
                            }
                        }
                    }
                    catch { }

                    if (existingYnv?.Nav == null || existingYnv.Polys == null || existingYnv.Polys.Count == 0)
                    {
                        statusCallback?.Invoke($"  No existing YNV found for neighbor AreaID {neighborAreaID}");
                        continue;
                    }

                    // Determine which boundary we share
                    var cellMin = NavGrid.GetCellMin(NavGrid.Cells[cellX, cellY]);
                    var cellMax = NavGrid.GetCellMax(NavGrid.Cells[cellX, cellY]);

                    float boundaryCoord;
                    bool isXBoundary; // true = shared boundary is a vertical line (X=const), false = horizontal (Y=const)
                    if (offset == -1) { boundaryCoord = cellMin.X; isXBoundary = true; }       // West
                    else if (offset == 1) { boundaryCoord = cellMax.X; isXBoundary = true; }    // East
                    else if (offset == -100) { boundaryCoord = cellMin.Y; isXBoundary = false; } // South
                    else { boundaryCoord = cellMax.Y; isXBoundary = false; }                     // North

                    int stitchedCount = StitchBoundaryEdges(ynv, existingYnv, boundaryCoord, isXBoundary, boundaryTolerance);
                    totalStitched += stitchedCount;

                    statusCallback?.Invoke($"  {(offset == -1 ? "West" : offset == 1 ? "East" : offset == -100 ? "South" : "North")} " +
                        $"neighbor AreaID {neighborAreaID}: stitched {stitchedCount} edges");
                }
            }

            statusCallback?.Invoke($"Boundary stitching complete: {totalStitched} edges connected to existing navmeshes");
            return totalStitched;
        }

        /// <summary>
        /// Stitches boundary edges between a new YNV and an existing adjacent YNV along a shared boundary.
        /// For each unlinked edge on the boundary of the new YNV, finds the best overlapping polygon
        /// edge in the existing YNV and creates a cross-boundary reference.
        /// </summary>
        private int StitchBoundaryEdges(YnvFile newYnv, YnvFile existingYnv, float boundaryCoord, bool isXBoundary, float tolerance)
        {
            int stitchedCount = 0;

            // Pre-build list of existing boundary edges for faster matching
            var existingBoundaryEdges = new List<(YnvPoly poly, int edgeIdx, Vector3 v1, Vector3 v2, float rangeMin, float rangeMax)>();

            foreach (var existPoly in existingYnv.Polys)
            {
                if (existPoly.Vertices == null || existPoly.Vertices.Length < 3)
                    continue;

                for (int ei = 0; ei < existPoly.Vertices.Length; ei++)
                {
                    var ev1 = existPoly.Vertices[ei];
                    var ev2 = existPoly.Vertices[(ei + 1) % existPoly.Vertices.Length];

                    // Check if this edge is on the boundary
                    float ec1 = isXBoundary ? ev1.X : ev1.Y;
                    float ec2 = isXBoundary ? ev2.X : ev2.Y;

                    if (Math.Abs(ec1 - boundaryCoord) > tolerance || Math.Abs(ec2 - boundaryCoord) > tolerance)
                        continue;

                    // Calculate range along the boundary axis
                    float rangeMin, rangeMax;
                    if (isXBoundary)
                    {
                        rangeMin = Math.Min(ev1.Y, ev2.Y);
                        rangeMax = Math.Max(ev1.Y, ev2.Y);
                    }
                    else
                    {
                        rangeMin = Math.Min(ev1.X, ev2.X);
                        rangeMax = Math.Max(ev1.X, ev2.X);
                    }

                    existingBoundaryEdges.Add((existPoly, ei, ev1, ev2, rangeMin, rangeMax));
                }
            }

            if (existingBoundaryEdges.Count == 0)
                return 0;

            // Match our boundary edges to existing boundary edges
            foreach (var poly in newYnv.Polys)
            {
                if (poly.Vertices == null || poly.Edges == null)
                    continue;
                if (poly.Edges.Length != poly.Vertices.Length)
                    continue;

                for (int edgeIdx = 0; edgeIdx < poly.Edges.Length; edgeIdx++)
                {
                    var edge = poly.Edges[edgeIdx];
                    if (edge == null) continue;

                    // Only process unlinked edges (self-referencing = no neighbor found yet)
                    if (edge.Poly1 != null && edge.Poly1 != poly)
                        continue;

                    // Get edge vertices
                    var v1 = poly.Vertices[edgeIdx];
                    var v2 = poly.Vertices[(edgeIdx + 1) % poly.Vertices.Length];

                    // Check if this edge lies on the shared boundary
                    float coord1 = isXBoundary ? v1.X : v1.Y;
                    float coord2 = isXBoundary ? v2.X : v2.Y;

                    if (Math.Abs(coord1 - boundaryCoord) > tolerance || Math.Abs(coord2 - boundaryCoord) > tolerance)
                        continue;

                    // Get the range along the boundary (the non-boundary axis)
                    float edgeMin, edgeMax;
                    if (isXBoundary)
                    {
                        edgeMin = Math.Min(v1.Y, v2.Y);
                        edgeMax = Math.Max(v1.Y, v2.Y);
                    }
                    else
                    {
                        edgeMin = Math.Min(v1.X, v2.X);
                        edgeMax = Math.Max(v1.X, v2.X);
                    }

                    float edgeMidZ = (v1.Z + v2.Z) * 0.5f;

                    // Find the best matching existing boundary edge
                    YnvPoly? bestMatch = null;
                    float bestOverlap = 0;

                    foreach (var (existPoly, ei, ev1, ev2, existMin, existMax) in existingBoundaryEdges)
                    {
                        // Check Z proximity
                        float existMidZ = (ev1.Z + ev2.Z) * 0.5f;
                        if (Math.Abs(edgeMidZ - existMidZ) > 2.0f)
                            continue;

                        // Calculate overlap along the boundary
                        float overlapMin = Math.Max(edgeMin, existMin);
                        float overlapMax = Math.Min(edgeMax, existMax);
                        float overlap = overlapMax - overlapMin;

                        if (overlap > bestOverlap)
                        {
                            bestOverlap = overlap;
                            bestMatch = existPoly;
                        }
                    }

                    if (bestMatch != null && bestOverlap > 0.01f)
                    {
                        // Set up cross-boundary edge reference
                        edge.Poly1 = bestMatch;
                        edge.Poly2 = bestMatch;
                        edge.AreaID1 = bestMatch.AreaID;
                        edge.AreaID2 = bestMatch.AreaID;
                        edge._RawData._Poly1.Unk2 = 0;
                        edge._RawData._Poly2.Unk2 = 0;
                        edge._RawData._Poly2.Unk3 = 4;
                        poly.B19_IsCellEdge = true;
                        stitchedCount++;
                    }
                }
            }

            return stitchedCount;
        }

    }

    // Core data structures for navmesh generation
    public class NavGenNode
    {
        public Vector3 BasePosition { get; set; }
        public List<NavSurfaceTri> SurroundingTriangles { get; set; } = new();
        public List<NavTriEdge> SurroundingEdges { get; set; } = new();
        public NavGenTri CollisionTriangle { get; set; }
        public MaterialType Material { get; set; }
        public bool IsWater { get; set; }
        public bool IsRemoved { get; set; }
        public int Flags { get; set; }

        // Extended flags from collision material
        public byte ProceduralId { get; set; }
        public byte RoomId { get; set; }
        public byte PedDensity { get; set; }
        public EBoundMaterialFlags MaterialFlags { get; set; }
        public bool IsInterior { get; set; }
        public bool IsRoad { get; set; }
        public bool IsTrainTracks { get; set; }
    }

    public class NavGenTri
    {
        public Vector3[] Vertices { get; set; } = new Vector3[3];
        public Vector3 Normal { get; set; }
        public MaterialType Material { get; set; }
        public bool IsWater { get; set; }

        // Extended flags from BoundMaterial_s
        public byte ProceduralId { get; set; }
        public byte RoomId { get; set; }
        public byte PedDensity { get; set; }
        public EBoundMaterialFlags MaterialFlags { get; set; }

        // Derived flags from material analysis
        public bool IsInterior { get; set; }
        public bool IsRoad { get; set; }
        public bool IsTrainTracks { get; set; }
        public bool IsShallowWater { get; set; }
    }

    public enum MaterialType
    {
        Default = 0,
        Pavement = 1,
        Water = 2,
        Stairs = 3,
        Slope = 4
    }

    public class NavSurfaceTri
    {
        public NavGenNode[] Nodes { get; set; } = new NavGenNode[3];
        public NavSurfaceTri[] AdjacentTris { get; set; } = new NavSurfaceTri[3];
        public Vector3 Normal { get; set; }
        public float PlaneDistance { get; set; }
        public MaterialType Material { get; set; }
        public ushort PolyFlags { get; set; }
        public bool IsWater { get; set; }
        public bool IsTooSteep { get; set; }
        public bool IsRemoved { get; set; }

        // Extended flags (propagated from collision material)
        public byte ProceduralId { get; set; }
        public byte RoomId { get; set; }
        public byte PedDensity { get; set; }
        public EBoundMaterialFlags MaterialFlags { get; set; }
        public bool IsInterior { get; set; }
        public bool IsRoad { get; set; }
        public bool IsTrainTracks { get; set; }
        public bool IsFlatGround { get; set; }
        public bool IsShallowWater { get; set; }

        public void CalculateNormal()
        {
            if (Nodes == null || Nodes.Length < 3) return;

            var v0 = Nodes[0].BasePosition;
            var v1 = Nodes[1].BasePosition;
            var v2 = Nodes[2].BasePosition;

            var edge1 = v1 - v0;
            var edge2 = v2 - v0;

            Normal = Vector3.Cross(edge1, edge2);
            Normal.Normalize();

            PlaneDistance = Vector3.Dot(Normal, v0);
        }

        public float CalculateArea()
        {
            if (Nodes == null || Nodes.Length < 3) return 0f;

            var v0 = Nodes[0].BasePosition;
            var v1 = Nodes[1].BasePosition;
            var v2 = Nodes[2].BasePosition;

            var edge1 = v1 - v0;
            var edge2 = v2 - v0;

            return Vector3.Cross(edge1, edge2).Length() * 0.5f;
        }
    }

    public class NavTriEdge
    {
        public NavGenNode Node1 { get; set; }
        public NavGenNode Node2 { get; set; }
        public NavSurfaceTri Tri1 { get; set; }
        public NavSurfaceTri Tri2 { get; set; }
        public float CostNode1ToNode2 { get; set; }
        public float CostNode2ToNode1 { get; set; }
        public bool IsRemoved { get; set; }

        public float Length()
        {
            if (Node1 == null || Node2 == null) return 0f;
            return (Node2.BasePosition - Node1.BasePosition).Length();
        }
    }

    public class NavSurfacePoly
    {
        public List<NavGenNode> Vertices { get; set; } = new();
        public List<NavSurfacePoly> AdjacentPolys { get; set; } = new();
        public Vector3 Normal { get; set; }
        public float PlaneDistance { get; set; }
        public MaterialType Material { get; set; }
        public ushort PolyFlags { get; set; }
        public bool IsWater { get; set; }
        public bool IsTooSteep { get; set; }
        public bool IsRemoved { get; set; }

        // Extended flags (preserved through merging)
        public byte ProceduralId { get; set; }
        public byte RoomId { get; set; }
        public byte PedDensity { get; set; }
        public EBoundMaterialFlags MaterialFlags { get; set; }
        public bool IsInterior { get; set; }
        public bool IsRoad { get; set; }
        public bool IsTrainTracks { get; set; }
        public bool IsFlatGround { get; set; }
        public bool IsShallowWater { get; set; }
        public bool HasPathNode { get; set; }

        public void CalculateNormalAndPlane()
        {
            if (Vertices == null || Vertices.Count < 3) return;

            // Use first three vertices to calculate normal
            var v0 = Vertices[0].BasePosition;
            var v1 = Vertices[1].BasePosition;
            var v2 = Vertices[2].BasePosition;

            var edge1 = v1 - v0;
            var edge2 = v2 - v0;

            Normal = Vector3.Cross(edge1, edge2);
            if (Normal.LengthSquared() > 0)
            {
                Normal.Normalize();
            }

            PlaneDistance = Vector3.Dot(Normal, v0);
        }

        public float CalculateArea()
        {
            if (Vertices == null || Vertices.Count < 3) return 0f;

            // Use triangulation to calculate area
            float totalArea = 0f;
            var v0 = Vertices[0].BasePosition;

            for (int i = 1; i < Vertices.Count - 1; i++)
            {
                var v1 = Vertices[i].BasePosition;
                var v2 = Vertices[i + 1].BasePosition;

                var edge1 = v1 - v0;
                var edge2 = v2 - v0;

                totalArea += Vector3.Cross(edge1, edge2).Length() * 0.5f;
            }

            return totalArea;
        }

        public YnvPoly ToYnvPoly()
        {
            if (Vertices == null || Vertices.Count < 3)
                return null;

            var positions = Vertices.Select(v => v.BasePosition).ToArray();
            var ynvPoly = new YnvPoly
            {
                Vertices = positions,
                AreaID = 0x3FFF // Will be set later based on grid position
            };

            // === PolyFlags0 (bits 0-7) ===
            // R* mapping: NSB_FLAG_PAVEMENT → OrFlags(NAVMESHPOLY_IS_PAVEMENT) = B02
            if (Material == MaterialType.Pavement || Material == MaterialType.Stairs)
            {
                ynvPoly.B02_IsFootpath = true;
            }
            // B03_IsUnderground = R*'s NAVMESHPOLY_IN_SHELTER (shelter from rain/weather)
            // R* determines this via raycast analysis in NavGen_Analyse.cpp (SetIsSheltered),
            // NOT from IsInterior. We don't have shelter detection, so only set for
            // actual underground areas (RoomId > 0 AND not at surface level).
            // For now, leave unset - shelter analysis would require upward raycasting.

            // R* mapping: NSB_FLAG_TOOSTEEP → OrFlags(NAVMESHPOLY_TOO_STEEP_TO_WALK_ON) = B06
            // R* uses: DotProduct(upVector, normal) < cos(44°) OR dot < 0
            if (IsTooSteep)
            {
                ynvPoly.B06_SteepSlope = true;
            }
            // R* mapping: NSB_FLAG_WATER → OrFlags(NAVMESHPOLY_IS_WATER) = B07
            if (IsWater)
            {
                ynvPoly.B07_IsWater = true;
            }

            // === PolyFlags1 (bits 8-24) ===
            // B08-B11: Underground level flags - these indicate underground depth levels,
            // not generic interior status. Leave unset unless we can determine actual depth.

            // B13_HasPathNode - Indicate if this polygon should have path nodes
            if (HasPathNode)
            {
                ynvPoly.B13_HasPathNode = true;
            }

            // R* mapping: NSB_FLAG_INTERIOR → SetIsInterior() = B14
            if (IsInterior)
            {
                ynvPoly.B14_IsInterior = true;
            }

            // B17_IsFlatGround - Set for near-horizontal surfaces (< 10 degrees from horizontal)
            if (IsFlatGround)
            {
                ynvPoly.B17_IsFlatGround = true;
            }

            // B18_IsRoad - Set from collision material IsThisSurfaceTypeRoad
            if (IsRoad)
            {
                ynvPoly.B18_IsRoad = true;
            }

            // B19_IsCellEdge - set during edge linking / navmesh stitching

            // B20_IsTrainTrack - Set from collision material IsThisSurfaceTypeTrainTracks
            if (IsTrainTracks)
            {
                ynvPoly.B20_IsTrainTrack = true;
            }

            // B21_IsShallowWater - water that can be walked through
            if (IsWater && IsShallowWater)
            {
                ynvPoly.B21_IsShallowWater = true;
            }

            // === PolyFlags2 (bits 25-32): Slope direction flags ===
            // Set slope direction based on the horizontal projection of the surface normal.
            // Only set for surfaces with significant slope (steep or slope material).
            if (IsTooSteep || Material == MaterialType.Slope)
            {
                var horizontalNormal = new Vector3(Normal.X, Normal.Y, 0);
                if (horizontalNormal.LengthSquared() > 0.01f)
                {
                    horizontalNormal.Normalize();

                    // Calculate angle from north (0, 1, 0) - the direction the slope faces
                    float angle = (float)Math.Atan2(horizontalNormal.X, horizontalNormal.Y) * 180f / (float)Math.PI;
                    if (angle < 0) angle += 360f;

                    // Map to 8 compass directions (45-degree sectors)
                    if (angle >= 337.5f || angle < 22.5f)
                        ynvPoly.B29_SlopeNorth = true;
                    else if (angle >= 22.5f && angle < 67.5f)
                        ynvPoly.B28_SlopeNorthEast = true;
                    else if (angle >= 67.5f && angle < 112.5f)
                        ynvPoly.B27_SlopeEast = true;
                    else if (angle >= 112.5f && angle < 157.5f)
                        ynvPoly.B26_SlopeSouthEast = true;
                    else if (angle >= 157.5f && angle < 202.5f)
                        ynvPoly.B25_SlopeSouth = true;
                    else if (angle >= 202.5f && angle < 247.5f)
                        ynvPoly.B32_SlopeSouthWest = true;
                    else if (angle >= 247.5f && angle < 292.5f)
                        ynvPoly.B31_SlopeWest = true;
                    else if (angle >= 292.5f && angle < 337.5f)
                        ynvPoly.B30_SlopeNorthWest = true;
                }
            }

            // Initialize edges array (will be populated later if needed)
            ynvPoly.Edges = new YnvEdge[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                ynvPoly.Edges[i] = new YnvEdge();
                ynvPoly.Edges[i].Poly1 = ynvPoly;
                ynvPoly.Edges[i].Poly2 = ynvPoly;
                ynvPoly.Edges[i].AreaID1 = 0x3FFF;
                ynvPoly.Edges[i].AreaID2 = 0x3FFF;
            }

            return ynvPoly;
        }
    }

    public class NavOctree
    {
        private class OctreeNode
        {
            public Vector3 Min { get; set; }
            public Vector3 Max { get; set; }
            public List<NavGenTri> Triangles { get; set; }
            public OctreeNode[] Children { get; set; }
            public bool IsLeaf => Children == null;
        }

        private OctreeNode root;
        private int maxTrianglesPerLeaf = 10;
        private int maxDepth = 8;

        public void Build(List<NavGenTri> collisionTriangles, Vector3 min, Vector3 max)
        {
            root = new OctreeNode
            {
                Min = min,
                Max = max,
                Triangles = new List<NavGenTri>()
            };

            foreach (var tri in collisionTriangles)
            {
                InsertTriangle(root, tri, 0);
            }
        }

        private void InsertTriangle(OctreeNode node, NavGenTri triangle, int depth)
        {
            if (node.IsLeaf)
            {
                node.Triangles.Add(triangle);

                // Subdivide if we have too many triangles and havent reached max depth
                if (node.Triangles.Count > maxTrianglesPerLeaf && depth < maxDepth)
                {
                    Subdivide(node, depth);
                }
            }
            else
            {
                // Insert into appropriate children
                for (int i = 0; i < 8; i++)
                {
                    if (TriangleIntersectsBox(triangle, node.Children[i].Min, node.Children[i].Max))
                    {
                        InsertTriangle(node.Children[i], triangle, depth + 1);
                    }
                }
            }
        }

        private void Subdivide(OctreeNode node, int depth)
        {
            var center = (node.Min + node.Max) * 0.5f;
            node.Children = new OctreeNode[8];

            for (int i = 0; i < 8; i++)
            {
                var min = new Vector3(
                    (i & 1) == 0 ? node.Min.X : center.X,
                    (i & 2) == 0 ? node.Min.Y : center.Y,
                    (i & 4) == 0 ? node.Min.Z : center.Z
                );
                var max = new Vector3(
                    (i & 1) == 0 ? center.X : node.Max.X,
                    (i & 2) == 0 ? center.Y : node.Max.Y,
                    (i & 4) == 0 ? center.Z : node.Max.Z
                );

                node.Children[i] = new OctreeNode
                {
                    Min = min,
                    Max = max,
                    Triangles = new List<NavGenTri>()
                };
            }

            // Redistribute triangles to children
            foreach (var tri in node.Triangles)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (TriangleIntersectsBox(tri, node.Children[i].Min, node.Children[i].Max))
                    {
                        InsertTriangle(node.Children[i], tri, depth + 1);
                    }
                }
            }

            node.Triangles.Clear();
        }

        private bool TriangleIntersectsBox(NavGenTri triangle, Vector3 boxMin, Vector3 boxMax)
        {
            // Simple AABB test - check if any vertex is inside the box or if triangle intersects box
            foreach (var vertex in triangle.Vertices)
            {
                if (vertex.X >= boxMin.X && vertex.X <= boxMax.X &&
                    vertex.Y >= boxMin.Y && vertex.Y <= boxMax.Y &&
                    vertex.Z >= boxMin.Z && vertex.Z <= boxMax.Z)
                {
                    return true;
                }
            }

            // Check if triangle bounding box intersects node bounding box
            var triMin = new Vector3(
                Math.Min(Math.Min(triangle.Vertices[0].X, triangle.Vertices[1].X), triangle.Vertices[2].X),
                Math.Min(Math.Min(triangle.Vertices[0].Y, triangle.Vertices[1].Y), triangle.Vertices[2].Y),
                Math.Min(Math.Min(triangle.Vertices[0].Z, triangle.Vertices[1].Z), triangle.Vertices[2].Z)
            );
            var triMax = new Vector3(
                Math.Max(Math.Max(triangle.Vertices[0].X, triangle.Vertices[1].X), triangle.Vertices[2].X),
                Math.Max(Math.Max(triangle.Vertices[0].Y, triangle.Vertices[1].Y), triangle.Vertices[2].Y),
                Math.Max(Math.Max(triangle.Vertices[0].Z, triangle.Vertices[1].Z), triangle.Vertices[2].Z)
            );

            return !(triMax.X < boxMin.X || triMin.X > boxMax.X ||
                     triMax.Y < boxMin.Y || triMin.Y > boxMax.Y ||
                     triMax.Z < boxMin.Z || triMin.Z > boxMax.Z);
        }

        public class RayIntersectResult
        {
            public bool Hit { get; set; }
            public Vector3 Position { get; set; }
            public NavGenTri Triangle { get; set; }
            public float Distance { get; set; }
        }

        public RayIntersectResult RayIntersect(Vector3 origin, Vector3 direction, float maxDistance)
        {
            var result = new RayIntersectResult { Hit = false, Distance = float.MaxValue };

            if (root == null) return result;

            RayIntersectNode(root, origin, direction, maxDistance, result);

            return result;
        }

        private void RayIntersectNode(OctreeNode node, Vector3 origin, Vector3 direction, float maxDistance, RayIntersectResult result)
        {
            // Test if ray intersects this nodes bounding box
            if (!RayBoxIntersect(origin, direction, node.Min, node.Max, maxDistance))
                return;

            if (node.IsLeaf)
            {
                // Test all triangles in this leaf
                foreach (var tri in node.Triangles)
                {
                    var hit = RayTriangleIntersect(origin, direction, tri, out float distance);
                    if (hit && distance < result.Distance && distance <= maxDistance)
                    {
                        result.Hit = true;
                        result.Distance = distance;
                        result.Position = origin + direction * distance;
                        result.Triangle = tri;
                    }
                }
            }
            else
            {
                // Recursively test children
                foreach (var child in node.Children)
                {
                    RayIntersectNode(child, origin, direction, maxDistance, result);
                }
            }
        }

        private bool RayBoxIntersect(Vector3 origin, Vector3 direction, Vector3 boxMin, Vector3 boxMax, float maxDistance)
        {
            float tmin = 0.0f;
            float tmax = maxDistance;

            for (int i = 0; i < 3; i++)
            {
                float o = i == 0 ? origin.X : (i == 1 ? origin.Y : origin.Z);
                float d = i == 0 ? direction.X : (i == 1 ? direction.Y : direction.Z);
                float bmin = i == 0 ? boxMin.X : (i == 1 ? boxMin.Y : boxMin.Z);
                float bmax = i == 0 ? boxMax.X : (i == 1 ? boxMax.Y : boxMax.Z);

                if (Math.Abs(d) < 1e-6f)
                {
                    if (o < bmin || o > bmax)
                        return false;
                }
                else
                {
                    float t1 = (bmin - o) / d;
                    float t2 = (bmax - o) / d;

                    if (t1 > t2)
                    {
                        float temp = t1;
                        t1 = t2;
                        t2 = temp;
                    }

                    tmin = Math.Max(tmin, t1);
                    tmax = Math.Min(tmax, t2);

                    if (tmin > tmax)
                        return false;
                }
            }

            return true;
        }

        private bool RayTriangleIntersect(Vector3 origin, Vector3 direction, NavGenTri triangle, out float distance)
        {
            distance = 0f;

            const float EPSILON = 1e-6f;

            var v0 = triangle.Vertices[0];
            var v1 = triangle.Vertices[1];
            var v2 = triangle.Vertices[2];

            var edge1 = v1 - v0;
            var edge2 = v2 - v0;

            var h = Vector3.Cross(direction, edge2);
            var a = Vector3.Dot(edge1, h);

            if (a > -EPSILON && a < EPSILON)
                return false; // Ray is parallel to triangle

            var f = 1.0f / a;
            var s = origin - v0;
            var u = f * Vector3.Dot(s, h);

            if (u < 0.0f || u > 1.0f)
                return false;

            var q = Vector3.Cross(s, edge1);
            var v = f * Vector3.Dot(direction, q);

            if (v < 0.0f || u + v > 1.0f)
                return false;

            distance = f * Vector3.Dot(edge2, q);

            return distance > EPSILON;
        }

        public List<NavGenTri> GetTrianglesInBounds(Vector3 min, Vector3 max)
        {
            var result = new List<NavGenTri>();
            if (root != null)
            {
                GetTrianglesInBoundsRecursive(root, min, max, result);
            }
            return result;
        }

        private void GetTrianglesInBoundsRecursive(OctreeNode node, Vector3 min, Vector3 max, List<NavGenTri> result)
        {
            // Check if node intersects query bounds
            if (node.Max.X < min.X || node.Min.X > max.X ||
                node.Max.Y < min.Y || node.Min.Y > max.Y ||
                node.Max.Z < min.Z || node.Min.Z > max.Z)
                return;

            if (node.IsLeaf)
            {
                result.AddRange(node.Triangles);
            }
            else
            {
                foreach (var child in node.Children)
                {
                    GetTrianglesInBoundsRecursive(child, min, max, result);
                }
            }
        }
    }

    public class CPlacedNodeMultiMap
    {
        private class NodeList
        {
            public List<NavGenNode> Nodes { get; set; } = new();
        }

        private NodeList[,] nodeGrid;
        private Vector3 gridMin;
        private Vector3 gridMax;
        private float cellSize;
        private int gridWidth;
        private int gridHeight;

        public int GridWidth => gridWidth;

        public int GridHeight => gridHeight;

        public void Initialize(Vector3 min, Vector3 max, float cellSize)
        {
            this.gridMin = min;
            this.gridMax = max;
            this.cellSize = cellSize;

            gridWidth = (int)Math.Ceiling((max.X - min.X) / cellSize);
            gridHeight = (int)Math.Ceiling((max.Y - min.Y) / cellSize);

            nodeGrid = new NodeList[gridWidth, gridHeight];

            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    nodeGrid[x, y] = new NodeList();
                }
            }
        }

        public void AddNode(NavGenNode node)
        {
            var gridPos = WorldToGrid(node.BasePosition);
            if (IsValidGridPos(gridPos))
            {
                nodeGrid[gridPos.X, gridPos.Y].Nodes.Add(node);
            }
        }

        public NavGenNode GetNode(Vector3 position, float zInterval, float xyEpsilon)
        {
            var gridPos = WorldToGrid(position);
            if (!IsValidGridPos(gridPos))
                return null;

            var nodes = nodeGrid[gridPos.X, gridPos.Y].Nodes;

            foreach (var node in nodes)
            {
                var dx = Math.Abs(node.BasePosition.X - position.X);
                var dy = Math.Abs(node.BasePosition.Y - position.Y);
                var dz = Math.Abs(node.BasePosition.Z - position.Z);

                if (dx <= xyEpsilon && dy <= xyEpsilon && dz <= zInterval)
                {
                    return node;
                }
            }

            return null;
        }

        public NavGenNode GetHighestNodeBelow(Vector3 position, float maxZ)
        {
            var gridPos = WorldToGrid(position);
            if (!IsValidGridPos(gridPos))
                return null;

            var nodes = nodeGrid[gridPos.X, gridPos.Y].Nodes;
            NavGenNode? highest = null;
            float highestZ = float.MinValue;

            foreach (var node in nodes)
            {
                if (node.BasePosition.Z <= maxZ && node.BasePosition.Z > highestZ)
                {
                    highest = node;
                    highestZ = node.BasePosition.Z;
                }
            }

            return highest;
        }

        public List<NavGenNode> GetNodesAt(int gridX, int gridY)
        {
            if (gridX >= 0 && gridX < gridWidth && gridY >= 0 && gridY < gridHeight)
            {
                return nodeGrid[gridX, gridY].Nodes;
            }
            return new List<NavGenNode>();
        }

        public NavGenNode GetAdjacentNode(NavGenNode node, int dx, int dy, float maxHeightDiff)
        {
            var gridPos = WorldToGrid(node.BasePosition);
            var adjGridPos = new Vector2I(gridPos.X + dx, gridPos.Y + dy);

            if (!IsValidGridPos(adjGridPos))
                return null;

            var nodes = nodeGrid[adjGridPos.X, adjGridPos.Y].Nodes;
            NavGenNode? closest = null;
            float closestDist = float.MaxValue;

            foreach (var adjNode in nodes)
            {
                var heightDiff = Math.Abs(adjNode.BasePosition.Z - node.BasePosition.Z);
                if (heightDiff <= maxHeightDiff && heightDiff < closestDist)
                {
                    closest = adjNode;
                    closestDist = heightDiff;
                }
            }

            return closest;
        }

        private Vector2I WorldToGrid(Vector3 worldPos)
        {
            int x = (int)((worldPos.X - gridMin.X) / cellSize);
            int y = (int)((worldPos.Y - gridMin.Y) / cellSize);
            return new Vector2I(x, y);
        }

        private bool IsValidGridPos(Vector2I gridPos)
        {
            return gridPos.X >= 0 && gridPos.X < gridWidth &&
                   gridPos.Y >= 0 && gridPos.Y < gridHeight;
        }
    }

    public class EdgeDictionary
    {
        private struct EdgeKey : IEquatable<EdgeKey>
        {
            public Vector3 V1 { get; }
            public Vector3 V2 { get; }

            public EdgeKey(Vector3 v1, Vector3 v2)
            {
                // Always store with smaller vertex first for consistent hashing
                if (CompareVectors(v1, v2) <= 0)
                {
                    V1 = v1;
                    V2 = v2;
                }
                else
                {
                    V1 = v2;
                    V2 = v1;
                }
            }

            private static int CompareVectors(Vector3 a, Vector3 b)
            {
                if (a.X != b.X) return a.X.CompareTo(b.X);
                if (a.Y != b.Y) return a.Y.CompareTo(b.Y);
                return a.Z.CompareTo(b.Z);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + V1.GetHashCode();
                    hash = hash * 31 + V2.GetHashCode();
                    return hash;
                }
            }

            public bool Equals(EdgeKey other)
            {
                return V1.Equals(other.V1) && V2.Equals(other.V2);
            }

            public override bool Equals(object? obj)
            {
                return obj is EdgeKey other && Equals(other);
            }
        }

        private Dictionary<EdgeKey, NavTriEdge> edges = new();

        public NavTriEdge TryGetEdge(Vector3 v1, Vector3 v2)
        {
            var key = new EdgeKey(v1, v2);
            edges.TryGetValue(key, out var edge);
            return edge;
        }

        public void AddEdge(Vector3 v1, Vector3 v2, NavTriEdge edge)
        {
            var key = new EdgeKey(v1, v2);
            edges[key] = edge;
        }

        public void RemoveEdge(Vector3 v1, Vector3 v2)
        {
            var key = new EdgeKey(v1, v2);
            edges.Remove(key);
        }

        public IEnumerable<NavTriEdge> GetAllEdges()
        {
            return edges.Values;
        }

        public void Clear()
        {
            edges.Clear();
        }

        public int Count => edges.Count;
    }

    public class NavGenParams
    {
        // Sampling (R* ref: fwNavGenConfig, NavMeshMaker.h)
        public float SamplingDensity { get; set; } = 1.0f; // R* m_SampleResolutionDefault = 1.0f
        public float MinZDistBetweenSamples { get; set; } = 2.0f; // R* m_MinimumVerticalDistanceBetweenNodes = 2.0f
        public bool JitterSamples { get; set; } = false;
        public float JitterAmount { get; set; } = 0.3f;

        // Triangulation (R* ref: fwNavGenConfig)
        public float TriangulationMaxHeightDiff { get; set; } = 2.0f;
        public float HeightAboveNodeBase { get; set; } = 1.0f; // Height above surface for sampling ray origin
        public float MaxHeightChangeUnderEdge { get; set; } = 0.4f; // R* m_MaxHeightChangeUnderTriangleEdge = 0.4f
        public float TestClearHeight { get; set; } = 1.8f; // R* m_TestClearHeightInTriangulation = 1.8f

        // Slope Detection (R* ref: fwNavGenConfig)
        public float MaxAngleForWalkable { get; set; } = 44.0f; // R* m_AngleForNavMeshPolyToBeTooSteep = 44.0f
        public float AngleForTooSteep { get; set; } = 60.0f; // R* m_MaxAngleForPolyToBeInNavMesh = 60.0f

        // Optimization
        public float MaxQuadricErrorMetric { get; set; } = 0.25f;
        public int MaxTrianglesSurroundingNode { get; set; } = 16;
        public float MinTriangleArea { get; set; } = 0.1f;
        public float MaxTriangleArea { get; set; } = 50.0f;
        public float MinTriangleSideLength { get; set; } = 0.3f;
        public float MaxTriangleSideLength { get; set; } = 15.0f;
        public float MinTriangleAngle { get; set; } = 15.0f; // degrees

        // Merging
        public float CoplanarPlaneTestEps { get; set; } = 0.25f; // Coplanarity tolerance for polygon merging
        public float ConvexPlaneTestEps { get; set; } = 0.01f; // Convexity tolerance for polygon merging
        public int MaxPolygonVertices { get; set; } = 8;

        // Grid (R* ref: CPathServerExtents)
        // R* uses: WorldMin=(-6000,-6000), SectorWidth=50, SectorsPerNavMesh=3, NavMeshSize=150
        // Grid: 100x100 navmeshes, total world = 15000x15000 (-6000 to 9000)
        public float NavGridCellSize { get; set; } = 150.0f; // meters per YNV file (SectorWidth * SectorsPerNavMesh)
        public float WorldMinX { get; set; } = -6000.0f; // R* m_WorldMinX
        public float WorldMinY { get; set; } = -6000.0f; // R* m_WorldMinY
        public int NumSectorsPerNavMesh { get; set; } = 3; // R* m_iNumSectorsPerNavMesh
        public float ExporterMinZCutoff { get; set; } = -500.0f; // R* m_ExporterMinZCutoff
        public float ExporterMaxZCutoff { get; set; } = 1000.0f; // R* m_ExporterMaxZCutoff
        public float PedRadius { get; set; } = 0.35f; // R* m_PedRadius
    }
}
