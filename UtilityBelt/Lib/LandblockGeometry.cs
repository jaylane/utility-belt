﻿using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using Decal.Adapter;
using Decal.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using UBCommon.Messages.Types;
using Frame = ACE.DatLoader.Entity.Frame;

namespace UtilityBelt.Lib {
    public class LandblockGeometry {
        public enum GeometryType {
            Unknown,
            Floor,
            StaticObject
        }

        public List<Vector3> Vertices { get; } = new List<Vector3>();
        public Dictionary<int, GeometryType> VerticesTypes = new Dictionary<int, GeometryType>();
        public List<List<int>> WalkableTriangles { get; } = new List<List<int>>();
        public List<List<int>> StaticTriangles { get; } = new List<List<int>>();
        public List<List<int>> OtherTriangles { get; } = new List<List<int>>();
        public uint Landblock { get; }
        private List<byte> Height { get; } = new List<byte>();
        private List<ushort> Terrain { get; } = new List<ushort>();


        private CellDatDatabase CellDat { get => UtilityBeltPlugin.Instance.CellDat; }
        private PortalDatDatabase PortalDat { get => UtilityBeltPlugin.Instance.PortalDat; }

        public LandblockGeometry(uint landblock) {
            Landblock = landblock;

            if (Landblock != 0)
                LoadLandblock();
        }

        private void LoadLandblock() {
            try {
                WalkableTriangles.Clear();
                OtherTriangles.Clear();
                StaticTriangles.Clear();
                Vertices.Clear();
                VerticesTypes.Clear();

                var lbInfoId = (uint)(0xFFFE + (Landblock & 0xFFFF0000));
                var lbDataId = (uint)(0xFFFF + (Landblock & 0xFFFF0000));
                Logger.Debug($"Attempting to load landblock: 0x{lbInfoId:X8}");

                var landblockInfo = CellDat.ReadFromDat<LandblockInfo>(lbInfoId);
                var landblockData = CellDat.ReadFromDat<CellLandblock>(lbDataId);
                var frames = new List<Frame>();

                for (var i = 0; i < landblockInfo.NumCells; i++) {
                    EnvCell cell = CellDat.ReadFromDat<EnvCell>((uint)((landblockInfo.Id >> 16 << 16) + 0x00000100 + i));

                    var tempFrames = frames.ToList();
                    tempFrames.Add(cell.Position);

                    LoadEnvironment(cell.EnvironmentId, tempFrames, cell.CellStructure);

                    if (cell.StaticObjects != null && cell.StaticObjects.Count > 0) {
                        foreach (var staticObject in cell.StaticObjects) {
                            var sFrames = new List<Frame>();
                            sFrames.Add(staticObject.Frame);
                            LoadSetupOrGfxObj(staticObject.Id, sFrames, GeometryType.StaticObject);
                        }
                    }
                }

                if (landblockInfo.Objects != null && landblockInfo.Objects.Count > 0) {
                    for (var i = 0; i < landblockInfo.Objects.Count; i++) {
                        var obj = landblockInfo.Objects[i];
                        var sFrames = frames.ToList();
                        sFrames.Add(obj.Frame);
                        LoadSetupOrGfxObj(obj.Id, sFrames, GeometryType.StaticObject);
                    }
                }

                if (landblockInfo.Buildings != null && landblockInfo.Buildings.Count > 0) {
                    for (var i = 0; i < landblockInfo.Buildings.Count; i++) {
                        var building = landblockInfo.Buildings[i];
                        var sFrames = frames.ToList();
                        sFrames.Add(building.Frame);
                        LoadSetupOrGfxObj(building.ModelId, sFrames, GeometryType.StaticObject);
                    }
                }

                if (IsDungeon())
                    return;

                if (landblockData.Height != null && landblockData.Height.Count > 0) {
                    for (int i = 0; i < 81; i++) {
                        Height.Add(landblockData.Height[i]);
                        Terrain.Add(landblockData.Terrain[i]);
                    }

                    for (uint tileX = 0; tileX < 8; tileX++) {
                        for (uint tileY = 0; tileY < 8; tileY++) {
                            uint v1 = tileX * 9 + tileY;
                            uint v2 = tileX * 9 + tileY + 1;
                            uint v3 = (tileX + 1) * 9 + tileY;
                            uint v4 = (tileX + 1) * 9 + tileY + 1;

                            var p1 = new Vector3();
                            p1.X = tileX * 24;
                            p1.Y = tileY * 24;
                            p1.Z = landblockData.Height[(int)v1] * 2;

                            var p2 = new Vector3();
                            p2.X = tileX * 24;
                            p2.Y = (tileY + 1) * 24;
                            p2.Z = landblockData.Height[(int)v2] * 2;

                            var p3 = new Vector3();
                            p3.X = (tileX + 1) * 24;
                            p3.Y = tileY * 24;
                            p3.Z = landblockData.Height[(int)v3] * 2;

                            //Program.Log($"{landblockData.Height.Count} -- {tileX}, {tileY} -- {p1.X:N3}, {p1.Y:N3}, {p1.Z:N3} - {p2.X:N3}, {p2.Y:N3}, {p2.Z:N3} - {p3.X:N3}, {p3.Y:N3}, {p3.Z:N3}");

                            AddPolygon(new List<Vector3>() { p3, p2, p1 }, frames, GeometryType.Floor);
                            var p4 = new Vector3();
                            p4.X = (tileX + 1) * 24;
                            p4.Y = (tileY + 1) * 24;
                            p4.Z = landblockData.Height[(int)v4] * 2;
                            AddPolygon(new List<Vector3>() { p4, p2, p3 }, frames, GeometryType.Floor);
                        }
                    }
                }


            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private bool IsDungeon() {
            if ((CoreManager.Current.Actions.Landcell & 0x0000FFFF) < 0x0100) {
                return false;
            }

            int dungeonId = (int)(CoreManager.Current.Actions.Landcell & 0xFFFF0000);
            bool isDungeon;


            FileService service = CoreManager.Current.Filter<FileService>();
            byte[] dungeonBlock = service.GetCellFile((int)CoreManager.Current.Actions.Landcell);

            if (dungeonBlock == null || dungeonBlock.Length < 5) {
                // This shouldn't happen...
                isDungeon = true;
                if (dungeonBlock == null) {
                    Logger.Debug("Null cell file for landblock: " + CoreManager.Current.Actions.Landcell.ToString("X8"));
                }
                else {
                    Logger.Debug("Cell file is only " + dungeonBlock.Length
                        + " bytes long for landblock: " + CoreManager.Current.Actions.Landcell.ToString("X8"));
                }
            }
            else {
                // Check whether it's a surface dwelling or a dungeon
                isDungeon = (dungeonBlock[4] & 0x01) == 0;
            }
            return isDungeon;
        }

        private void LoadEnvironment(uint cellFileIndex, List<Frame> frames, int envCellIndex = -1) {
            var environment = PortalDat.ReadFromDat<ACE.DatLoader.FileTypes.Environment>(cellFileIndex);
            if (environment.Id == (uint)0x0D0000CA || environment.Id == (uint)0x0D00016D)
                return;

            if (envCellIndex >= 0) {
                var polys = environment.Cells[(uint)envCellIndex].PhysicsPolygons;
                foreach (var poly in polys.Values) {
                    AddPolygon(poly.VertexIds.Select(v => environment.Cells[(uint)envCellIndex].VertexArray.Vertices[(ushort)v].Origin).ToList(), frames, GeometryType.Unknown);
                }
            }
            else {
                foreach (var cell in environment.Cells.Values) {
                    foreach (var poly in cell.PhysicsPolygons.Values) {
                        AddPolygon(poly.VertexIds.Select(v => cell.VertexArray.Vertices[(ushort)v].Origin).ToList(), frames, GeometryType.Unknown);
                    }
                }
            }
        }

        private void LoadSetupOrGfxObj(uint id, List<Frame> sFrames, GeometryType gType) {
            if ((id & 0x02000000) != 0) {
                LoadSetup(id, sFrames, gType);
            }
            else {
                LoadGfxObj(id, sFrames, gType);
            }
        }

        private void LoadGfxObj(uint cellFileIndex, List<Frame> frames, GeometryType gType) {
            var gfxObj = PortalDat.ReadFromDat<GfxObj>(cellFileIndex);
            if (gfxObj == null)
                return;
            foreach (var pkv in gfxObj.PhysicsPolygons) {
                var vertices = pkv.Value.VertexIds.Select(v => gfxObj.VertexArray.Vertices[(ushort)v].Origin).ToList();
                AddPolygon(vertices, frames, gType);
            }
        }

        private void LoadSetup(uint cellFileIndex, List<Frame> frames, GeometryType gType) {
            var setupModel = PortalDat.ReadFromDat<SetupModel>(cellFileIndex);
            bool hasPhysicsPolys = false;

            // physics draw priority is child part physics polys, then cylspheres, then spheres?
            foreach (var partId in setupModel.Parts) {
                var gfxObj = PortalDat.ReadFromDat<GfxObj>(partId);
                if (gfxObj.PhysicsPolygons != null && gfxObj.PhysicsPolygons.Count > 0) {
                    hasPhysicsPolys = true;
                    break;
                }
            }

            if (!hasPhysicsPolys && setupModel.CylSpheres != null && setupModel.CylSpheres.Count > 0) {
                foreach (var cSphere in setupModel.CylSpheres) {
                    List<List<Vector3>> polys = GetCylSpherePolygons(cSphere.Origin, cSphere.Height, cSphere.Radius, false);
                    foreach (var poly in polys) {
                        AddPolygon(poly, frames, GeometryType.StaticObject);
                    }
                }
            }

            if (!hasPhysicsPolys && setupModel.Spheres != null && setupModel.Spheres.Count > 0) {
                foreach (var sphere in setupModel.Spheres) {
                    List<List<Vector3>> polys = GetSpherePolygons(sphere.Origin, sphere.Radius);
                    foreach (var poly in polys) {
                        AddPolygon(poly, frames, GeometryType.StaticObject);
                    }
                }
            }

            // draw all the child parts
            for (var i = 0; i < setupModel.Parts.Count; i++) {
                // always use PlacementFrames[0] ?
                var tempFrames = frames.ToList(); // clone
                tempFrames.Insert(0, setupModel.PlacementFrames.Last().Value.AnimFrame.Frames.Last());
                LoadGfxObj(setupModel.Parts[i], tempFrames, gType);
            }
        }

        public void AddPolygon(List<Vector3> vertices, List<Frame> frames, GeometryType gType) {
            var poly = new List<Vector3>();
            for (var i = 0; i < vertices.Count; i++) {
                var vertice = vertices[i];
                foreach (var frame in frames) {
                    vertice = Transform(vertice, frame.Orientation);
                    vertice += frame.Origin;
                }

                vertice = new Vector3() {
                    X = vertice.X,
                    Y = vertice.Y,
                    Z = vertice.Z
                };

                poly.Add(vertice);
            }

            // split to triangles
            for (int i = 2; i < poly.Count; i++) {
                var tri = new List<int>();

                Vertices.Add(poly[i]);
                tri.Add(Vertices.Count - 1);

                Vertices.Add(poly[i - 1]);
                tri.Add(Vertices.Count - 1);

                Vertices.Add(poly[0]);
                tri.Add(Vertices.Count - 1);

                if (CanWalkTri(tri, gType)) {
                    gType = GeometryType.Floor;
                }

                VerticesTypes.Add(Vertices.Count - 1, gType);
                VerticesTypes.Add(Vertices.Count - 2, gType);
                VerticesTypes.Add(Vertices.Count - 3, gType);

                switch (gType) {
                    case GeometryType.Floor:
                        WalkableTriangles.Add(tri);
                        break;
                    case GeometryType.StaticObject:
                        StaticTriangles.Add(tri);
                        break;
                    default:
                        OtherTriangles.Add(tri);
                        break;
                }
            }
        }

        private Vector3 CalculateTriSurfaceNormal(Vector3 a, Vector3 b, Vector3 c) {
            Vector3 normal = new Vector3();
            Vector3 u = new Vector3();
            Vector3 v = new Vector3();

            u.X = b.X - a.X;
            u.Y = b.Y - a.Y;
            u.Z = b.Z - a.Z;

            v.X = c.X - a.X;
            v.Y = c.Y - a.Y;
            v.Z = c.Z - a.Z;

            normal.X = u.Y * v.Z - u.Z * v.Y;
            normal.Y = u.Z * v.X - u.X * v.Z;
            normal.Z = u.X * v.Y - u.Y * v.X;

            normal.Normalize();

            return normal;
        }

        private bool CanWalkTri(List<int> tri, GeometryType gType) {
            var floorZ = -0.66417414618662751f;
            var triNormal = CalculateTriSurfaceNormal(Vertices[tri[0]], Vertices[tri[1]], Vertices[tri[2]]);
            return triNormal.Z <= floorZ;
        }

        public static Vector3 Transform(Vector3 value, Quaternion rotation) {
            float x2 = rotation.X + rotation.X;
            float y2 = rotation.Y + rotation.Y;
            float z2 = rotation.Z + rotation.Z;

            float wx2 = rotation.W * x2;
            float wy2 = rotation.W * y2;
            float wz2 = rotation.W * z2;
            float xx2 = rotation.X * x2;
            float xy2 = rotation.X * y2;
            float xz2 = rotation.X * z2;
            float yy2 = rotation.Y * y2;
            float yz2 = rotation.Y * z2;
            float zz2 = rotation.Z * z2;

            return new Vector3(
                value.X * (1.0f - yy2 - zz2) + value.Y * (xy2 - wz2) + value.Z * (xz2 + wy2),
                value.X * (xy2 + wz2) + value.Y * (1.0f - xx2 - zz2) + value.Z * (yz2 - wx2),
                value.X * (xz2 - wy2) + value.Y * (yz2 + wx2) + value.Z * (1.0f - xx2 - yy2));
        }

        public static List<List<Vector3>> GetSpherePolygons(Vector3 origin, float radius) {
            var results = new List<List<Vector3>>();
            var vectors = new List<Vector3>();
            var indices = new List<int>();

            Icosahedron(vectors, indices);

            for (var i = 0; i < 3; i++)
                Subdivide(vectors, indices, true);

            /// normalize vectors to "inflate" the icosahedron into a sphere.
            for (var i = 0; i < indices.Count / 3; i++) {
                var testIndicies = new List<int>();
                for (var x = 2; x >= 0; x--) {
                    var ii = (i * 3) + x;
                    vectors[indices[ii]] = Vector3.Normalize(vectors[indices[ii]]) * radius;
                    testIndicies.Add(indices[ii]);
                }

                // adjust height.. 
                var h = new Vector3(0, 0, radius);
                results.Add(new List<Vector3>() { vectors[testIndicies[0]] + h, vectors[testIndicies[1]] + h, vectors[testIndicies[2]] + h });
            }

            return results;
        }

        public static List<List<Vector3>> GetCylSpherePolygons(Vector3 origin, float height, float radius, bool coneTop = false) {
            var num_sides = 12;
            var axis = new Vector3(0, 0, height);
            var results = new List<List<Vector3>>();
            // Get two vectors perpendicular to the axis.
            Vector3 v1;
            if ((axis.Z < -0.01) || (axis.Z > 0.01))
                v1 = new Vector3(axis.Z, axis.Z, -axis.X - axis.Y);
            else
                v1 = new Vector3(-axis.Y - axis.Z, axis.X, axis.X);
            Vector3 v2 = Vector3.Cross(v1, axis);

            // Make the vectors have length radius.
            v1 *= (radius / v1.Length());
            v2 *= (radius / v2.Length());

            var Positions = new List<Vector3>();

            // Make the top end cap.
            // Make the end point.
            int pt0 = Positions.Count; // Index of end_point.
            Positions.Add(origin);

            // Make the top points.
            double theta = 0;
            double dtheta = 2 * Math.PI / num_sides;
            for (int i = 0; i < num_sides; i++) {
                Positions.Add(origin +
                    v1 * (float)Math.Cos(theta) +
                    v2 * (float)Math.Sin(theta));
                theta += dtheta;
            }

            // Make the top triangles.
            int pt1 = Positions.Count - 1; // Index of last point.
            int pt2 = pt0 + 1;                  // Index of first point.
            for (int i = 0; i < num_sides; i++) {
                results.Add(new List<Vector3>() { Positions[pt0], Positions[pt1], Positions[pt2] });
                pt1 = pt2++;
            }

            // Make the bottom end cap.
            // Make the end point.
            pt0 = Positions.Count; // Index of end_point2.
            Vector3 end_point2 = origin + axis;
            Positions.Add(end_point2);

            // Make the bottom points.
            theta = 0;
            for (int i = 0; i < num_sides; i++) {
                Positions.Add(end_point2 +
                    v1 * (float)Math.Cos(theta) +
                    v2 * (float)Math.Sin(theta));
                theta += dtheta;
            }

            // Make the bottom triangles.
            theta = 0;
            pt1 = Positions.Count - 1; // Index of last point.
            pt2 = pt0 + 1;                  // Index of first point.
            var p0 = Positions[num_sides + 1];
            if (coneTop && axis.Z > 20)
                p0 += new Vector3(0, 0, 20);

            for (int i = 0; i < num_sides; i++) {
                results.Add(new List<Vector3>() { p0, Positions[pt2], Positions[pt1] });
                pt1 = pt2++;
            }

            // Make the sides.
            // Add the points to the mesh.
            int first_side_point = Positions.Count;
            theta = 0;
            for (int i = 0; i < num_sides; i++) {
                Vector3 p1 = origin +
                    v1 * (float)Math.Cos(theta) +
                    v2 * (float)Math.Sin(theta);
                Positions.Add(p1);
                Vector3 p2 = p1 + axis;
                Positions.Add(p2);
                theta += dtheta;
            }

            // Make the side triangles.
            pt1 = Positions.Count - 2;
            pt2 = pt1 + 1;
            int pt3 = first_side_point;
            int pt4 = pt3 + 1;
            for (int i = 0; i < num_sides; i++) {
                results.Add(new List<Vector3>() { Positions[pt1], Positions[pt2], Positions[pt4] });
                results.Add(new List<Vector3>() { Positions[pt1], Positions[pt4], Positions[pt3] });

                pt1 = pt3;
                pt3 += 2;
                pt2 = pt4;
                pt4 += 2;
            }

            return results;
        }



        private static int GetMidpointIndex(Dictionary<string, int> midpointIndices, List<Vector3> vertices, int i0, int i1) {

            var edgeKey = string.Format("{0}_{1}", Math.Min(i0, i1), Math.Max(i0, i1));

            var midpointIndex = -1;

            if (!midpointIndices.TryGetValue(edgeKey, out midpointIndex)) {
                var v0 = vertices[i0];
                var v1 = vertices[i1];

                var midpoint = (v0 + v1);
                midpoint *= 0.5f;

                if (vertices.Contains(midpoint))
                    midpointIndex = vertices.IndexOf(midpoint);
                else {
                    midpointIndex = vertices.Count;
                    vertices.Add(midpoint);
                    midpointIndices.Add(edgeKey, midpointIndex);
                }
            }


            return midpointIndex;

        }

        /// <remarks>
        ///      i0
        ///     /  \
        ///    m02-m01
        ///   /  \ /  \
        /// i2---m12---i1
        /// </remarks>
        /// <param name="vectors"></param>
        /// <param name="indices"></param>
        public static void Subdivide(List<Vector3> vectors, List<int> indices, bool removeSourceTriangles) {
            var midpointIndices = new Dictionary<string, int>();

            var newIndices = new List<int>(indices.Count * 4);

            if (!removeSourceTriangles)
                newIndices.AddRange(indices);

            for (var i = 0; i < indices.Count - 2; i += 3) {
                var i0 = indices[i];
                var i1 = indices[i + 1];
                var i2 = indices[i + 2];

                var m01 = GetMidpointIndex(midpointIndices, vectors, i0, i1);
                var m12 = GetMidpointIndex(midpointIndices, vectors, i1, i2);
                var m02 = GetMidpointIndex(midpointIndices, vectors, i2, i0);

                newIndices.AddRange(
                    new[] {
                    i0,m01,m02
                    ,
                    i1,m12,m01
                    ,
                    i2,m02,m12
                    ,
                    m02,m01,m12
                    }
                    );

            }

            indices.Clear();
            indices.AddRange(newIndices);
        }

        /// <summary>
        /// create a regular icosahedron (20-sided polyhedron)
        /// </summary>
        /// <param name="primitiveType"></param>
        /// <param name="size"></param>
        /// <param name="vertices"></param>
        /// <param name="indices"></param>
        /// <remarks>
        /// You can create this programmatically instead of using the given vertex 
        /// and index list, but it's kind of a pain and rather pointless beyond a 
        /// learning exercise.
        /// </remarks>

        /// note: icosahedron definition may have come from the OpenGL red book. I don't recall where I found it. 
        public static void Icosahedron(List<Vector3> vertices, List<int> indices, float scale = 1f) {

            indices.AddRange(
                new int[]
                {
                0,4,1,
                0,9,4,
                9,5,4,
                4,5,8,
                4,8,1,
                8,10,1,
                8,3,10,
                5,3,8,
                5,2,3,
                2,7,3,
                7,10,3,
                7,6,10,
                7,11,6,
                11,0,6,
                0,1,6,
                6,1,10,
                9,0,11,
                9,11,2,
                9,2,5,
                7,2,11
                }
                .Select(i => i + vertices.Count)
            );

            var X = 0.525731112119133606f;
            var Z = 0.850650808352039932f;

            vertices.AddRange(
                new[]
                {
                new Vector3(-X, 0f, Z),
                new Vector3(X, 0f, Z),
                new Vector3(-X, 0f, -Z),
                new Vector3(X, 0f, -Z),
                new Vector3(0f, Z, X),
                new Vector3(0f, Z, -X),
                new Vector3(0f, -Z, X),
                new Vector3(0f, -Z, -X),
                new Vector3(Z, X, 0f),
                new Vector3(-Z, X, 0f),
                new Vector3(Z, -X, 0f),
                new Vector3(-Z, -X, 0f)
                }
            );
        }

    }
}
