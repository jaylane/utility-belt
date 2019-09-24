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
                Util.WriteToChat("Could not parse pause: " + pauseText);
                return false;
            }

            return true;
        }

        public override void Draw() {
            var rp = GetPreviousPoint();
            var color = Color.FromArgb(Globals.Config.VisualNav.PauseColor.Value);
            rp = rp == null ? GetNextPoint() : rp;
            rp = rp == null ? this : rp;

            if (Globals.Config.VisualNav.ShowPause.Value) {
                DrawText($"Pause for {Pause / 1000} seconds", rp, 0, color);
            }
        }
    }
}
