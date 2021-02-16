using Decal.Adapter;
using Decal.Adapter.Wrappers;
using Decal.Filters;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.VTNav.Waypoints {
    class VTNJump : VTNPoint {
        public int Heading = 0;
        public double Milliseconds = 0;
        public bool ShiftJump = false;

        public VTNJump(StreamReader reader, VTNavRoute parentRoute, int index) : base(reader, parentRoute, index) {
            Type = eWaypointType.Jump;
        }

        new public bool Parse() {
            if (!base.Parse()) return false;

            var facingLine = base.sr.ReadLine();
            if (!int.TryParse(facingLine, out Heading)) {
                Logger.Error("Could not parse Facing: " + facingLine);
                return false;
            }

            var shiftLine = base.sr.ReadLine();
            if (shiftLine.Trim().ToLower() == "true") {
                ShiftJump = true;
            }

            var millisecondsLine = base.sr.ReadLine();
            if (!double.TryParse(millisecondsLine, out Milliseconds)) {
                Logger.Error("Could not parse Milliseconds: " + millisecondsLine);
                return false;
            }

            return true;
        }

        internal override void Write(StreamWriter file) {
            base.Write(file);
            file.WriteLine(Heading);
            file.WriteLine(ShiftJump);
            file.WriteLine(Milliseconds);
        }

        public override void Draw() {
            var rp = GetPreviousPoint();
            var textColor = Color.FromArgb(UtilityBeltPlugin.Instance.VisualNav.Display.JumpText.Color);
            var arrowColor = Color.FromArgb(UtilityBeltPlugin.Instance.VisualNav.Display.JumpArrow.Color);
            var tp = rp == null ? GetNextPoint() : rp;
            tp = tp == null ? this : tp;

            if (UtilityBeltPlugin.Instance.VisualNav.Display.JumpText.Enabled) {
                var obj = CoreManager.Current.D3DService.MarkCoordsWithShape((float)tp.NS, (float)tp.EW, (float)(tp.Z * 240) + (float)route.GetZOffset(tp.NS, tp.EW), D3DShape.HorizontalArrow, arrowColor.ToArgb());
                float dist = 1f;
                float a = (float)((360 - (Heading - 90)) * Math.PI / 180f);
                var ns = tp.NS + (Math.Sin(a) * dist);
                var ew = tp.EW + (Math.Cos(a) * dist);
                obj.OrientToCoords((float)ns, (float)ew, (float)tp.Z * 240, false);

                shapes.Add(obj);
            }

            if (UtilityBeltPlugin.Instance.VisualNav.Display.JumpArrow.Enabled) {
                DrawText($"{(ShiftJump ? "Shift" : "")} Jump {Math.Round(Milliseconds / 10, 0)}%", tp, 0, textColor);
            }
        }
    }
}
