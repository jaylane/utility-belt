using Decal.Adapter.Wrappers;
using Decal.Filters;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.VTNav.Waypoints {
    class VTNChat : VTNPoint {
        public string Message = "";

        public VTNChat(StreamReader reader, VTNavRoute parentRoute, int index) : base(reader, parentRoute, index) {
            Type = eWaypointType.Pause;
        }

        new public bool Parse() {
            if (!base.Parse()) return false;

            Message = base.sr.ReadLine();

            return true;
        }

        public override void Draw() {
            var rp = GetPreviousPoint();
            rp = rp == null ? GetNextPoint() : this;

            var color = Color.FromArgb(255, 255, 255, 255);

            DrawText($"Chat: {Message}", rp, 0, color);
        }
    }
}
