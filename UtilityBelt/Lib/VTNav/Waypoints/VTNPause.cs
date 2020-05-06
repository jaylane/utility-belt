using Decal.Adapter.Wrappers;
using Decal.Filters;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.VTNav.Waypoints {
    class VTNPause : VTNPoint {
        public int Pause = 0;

        public VTNPause(StreamReader reader, VTNavRoute parentRoute, int index) : base(reader, parentRoute, index) {
            Type = eWaypointType.Pause;
        }

        new public bool Parse() {
            if (!base.Parse()) return false;

            var pauseText = base.sr.ReadLine();

            if (!int.TryParse(pauseText, out Pause)) {
                Logger.Error("Could not parse pause: " + pauseText);
                return false;
            }

            return true;
        }

        internal override void Write(StreamWriter file) {
            base.Write(file);
            file.WriteLine(Pause);
        }

        public override void Draw() {
            var rp = GetPreviousPoint();
            var color = Color.FromArgb(UtilityBeltPlugin.Instance.VisualNav.Display.Pause.Color);
            rp = rp == null ? GetNextPoint() : rp;
            rp = rp == null ? this : rp;

            if (UtilityBeltPlugin.Instance.VisualNav.Display.Pause.Enabled) {
                DrawText($"Pause for {Pause / 1000} seconds", rp, 0, color);
            }
        }
    }
}
