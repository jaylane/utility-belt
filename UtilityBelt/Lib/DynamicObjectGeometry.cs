using ACE.DatLoader;
using ACE.DatLoader.Entity;
using ACE.DatLoader.FileTypes;
using Decal.Adapter;
using Microsoft.DirectX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib {
    public class DynamicObjectGeometry {
        public List<Vector3> Vertices { get; } = new List<Vector3>();
        public List<List<int>> Triangles { get; } = new List<List<int>>();

        private PortalDatDatabase PortalDat { get => UtilityBeltPlugin.Instance.PortalDat; }

        public DynamicObjectGeometry(uint setupId) {
            if (setupId != 0)
                LoadSetup(setupId, new List<Frame>());
        }

        private void LoadSetup(uint cellFileIndex, List<Frame> frames) {
            var setupModel = PortalDat.ReadFromDat<SetupModel>(cellFileIndex);

            foreach (var partId in setupModel.Parts) {
                var gfxObj = PortalDat.ReadFromDat<GfxObj>(partId);
                if (gfxObj.PhysicsPolygons != null && gfxObj.Polygons.Count > 0) {
                    //hasPhysicsPolys = true;
                    break;
                }
            }

            // draw all the child parts
            for (var i = 0; i < setupModel.Parts.Count; i++) {
                // always use PlacementFrames[0] ?
                var tempFrames = frames.ToList(); // clone
                tempFrames.Insert(0, setupModel.PlacementFrames[0].AnimFrame.Frames[i]);
                LoadGfxObj(setupModel.Parts[i], tempFrames);
            }
        }

        private void LoadGfxObj(uint cellFileIndex, List<Frame> frames) {
            var gfxObj = PortalDat.ReadFromDat<GfxObj>(cellFileIndex);
            if (gfxObj == null)
                return;
            foreach (var pkv in gfxObj.Polygons) {
                var vertices = pkv.Value.VertexIds.Select(v => gfxObj.VertexArray.Vertices[(ushort)v].Origin).ToList();
                AddPolygon(vertices, frames);
            }
        }

        public void AddPolygon(List<Vector3> vertices, List<Frame> frames) {
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

                Triangles.Add(tri);
            }
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
    }
}
