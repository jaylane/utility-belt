using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.VTNav.Waypoints {
    public class VTNPoint {
        public double NS = 0.0;
        public double EW = 0.0;
        public double Z = 0.0;
        public List<D3DObj> shapes = new List<D3DObj>();

        public VTNPoint Previous = null;

        internal StreamReader sr;
        internal int index = 0;
        internal VTNavRoute route;
        internal bool disposed = false;

        internal eWaypointType Type = eWaypointType.Point;

        public VTNPoint(StreamReader reader, VTNavRoute parentRoute, int index) {
            sr = reader;
            route = parentRoute;
            this.index = index;
        }

        public VTNPoint GetPreviousPoint() {
            for (var i = index - 1; i >= 0; i--) {
                var t = route.points[i].Type;
                if (t == eWaypointType.Point) {
                    return route.points[i];
                }
                else if (t == eWaypointType.OpenVendor) {
                    var ov = (VTNOpenVendor)route.points[i];
                    if (ov.closestDistance < Double.MaxValue) {
                        return route.points[i];
                    }
                }
                else if (t == eWaypointType.UseNPC) {
                    var ov = (VTNUseNPC)route.points[i];
                    if (ov.closestDistance < Double.MaxValue) {
                        return route.points[i];
                    }
                }
                else if (t == eWaypointType.Portal || t == eWaypointType.Portal2) {
                    break;
                }
            }

            return null;
        }

        public VTNPoint GetNextPoint() {
            for (var i = index + 1; i < route.points.Count - 1; i++) {
                if (route.points[i].Type == eWaypointType.Point) return route.points[i];
            }

            return null;
        }

        public bool Parse() {
            if (!double.TryParse(sr.ReadLine(), out EW)) return false;
            if (!double.TryParse(sr.ReadLine(), out NS)) return false;
            if (!double.TryParse(sr.ReadLine(), out Z)) return false;

            // i dont know what this value is, always 0?
            sr.ReadLine();

            return true;
        }

        public double DistanceTo(VTNPoint rp) {
            return Math.Abs(Math.Sqrt(Math.Pow(rp.NS - NS, 2) + Math.Pow(rp.EW - EW, 2) + Math.Pow(rp.Z - Z, 2))) * 240;
        }

        public void DrawLineTo(VTNPoint rp, Color color) {
            if (rp == null) return;

            var d = DistanceTo(rp);

            var obj = Globals.Core.D3DService.NewD3DObj();
            obj.Visible = false;
            obj.Color = color.ToArgb();
            obj.SetShape(D3DShape.Cube);
            obj.Anchor((float)(NS + rp.NS) / 2, (float)(EW + rp.EW) / 2, (float)((Z + rp.Z) * 120) + 0.05f);
            obj.OrientToCoords((float)NS, (float)EW, (float)(Z * 240) + 0.05f, true);
            obj.ScaleX = 0.25f;
            obj.ScaleZ = 0.25f;
            obj.ScaleY = (float)d;
            obj.Visible = true;

            shapes.Add(obj);
        }

        public void DrawText(string text, VTNPoint rp, float height, Color color) {
            if (rp == null) return;

            var textObj = Globals.Core.D3DService.MarkCoordsWith3DText(0, 0, 0, text, "arial", 0);
            textObj.Visible = false;
            textObj.Color = color.ToArgb();

            textObj.Anchor((float)rp.NS, (float)rp.EW, (float)(rp.Z * 240) + height + (float)route.GetZOffset(rp.NS, rp.EW));
            route.AddOffset(rp.NS, rp.EW, 0.25f);

            textObj.Scale(0.2f);
            textObj.OrientToCamera(true);
            textObj.Visible = true;

            shapes.Add(textObj);
        }

        public void DrawIcon(int iconId, float height, VTNPoint point) {
            if (point == null) return;

            var icon = Globals.Core.D3DService.MarkCoordsWithIcon((float)point.NS, (float)point.EW, (float)(point.Z * 240) + height + (float)route.GetZOffset(point.NS, point.EW), iconId);
            route.AddOffset(point.NS, point.EW, 0.5);

            icon.OrientToCamera(true);
            icon.Visible = true;
            icon.Scale(0.5f);

            shapes.Add(icon);
        }

        public virtual void Draw() {
            var rp = Previous;

            // we dont want to draw lines to the previous point if it was a recall or portal or jump
            if (rp == null || rp.Type == eWaypointType.Recall || rp.Type == eWaypointType.Portal || rp.Type == eWaypointType.Portal2 || rp.Type == eWaypointType.Jump) {
                return;
            }

            rp = GetPreviousPoint();

            var color = Color.FromArgb(150, 221, 32, 97);
            DrawLineTo(rp, color);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    foreach (var shape in shapes) {
                        try { shape.Visible = false; } catch (Exception ex) { }
                        shape.Dispose();
                    }
                }
                disposed = true;
            }
        }
    }
}
