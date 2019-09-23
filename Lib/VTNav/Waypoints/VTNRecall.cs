using Decal.Adapter.Wrappers;
using Decal.Filters;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.VTNav.Waypoints {
    class VTNRecall : VTNPoint {
        public int RecallSpellId = 0;

        public VTNRecall(StreamReader reader, VTNavRoute parentRoute, int index) : base(reader, parentRoute, index) {
            Type = eWaypointType.Recall;
        }

        new public bool Parse() {
            if (!base.Parse()) return false;

            var recallId = base.sr.ReadLine();

            if (!int.TryParse(recallId, out RecallSpellId)) {
                Util.WriteToChat("Could not parse recall spell id: " + recallId);
                return false;
            }

            return true;
        }

        public override void Draw() {
            //base.Draw();

            FileService service = Globals.Core.Filter<FileService>();
            var spell = service.SpellTable.GetById(RecallSpellId);

            VTNPoint rp = GetPreviousPoint();
            var color = Color.FromArgb(221, 221, 221, 221);
            VTNPoint point = rp != null ? rp : this;

            DrawText(spell.Name, point, 0.25f, color);

            DrawIcon(spell.IconId, 0.35f, point);
        }
    }
}
