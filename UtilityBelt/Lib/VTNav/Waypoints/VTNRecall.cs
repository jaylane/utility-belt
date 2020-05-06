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
    class VTNRecall : VTNPoint {
        public int RecallSpellId = 0;

        public VTNRecall(StreamReader reader, VTNavRoute parentRoute, int index) : base(reader, parentRoute, index) {
            Type = eWaypointType.Recall;
        }

        new public bool Parse() {
            if (!base.Parse()) return false;

            var recallId = base.sr.ReadLine();

            if (!int.TryParse(recallId, out RecallSpellId)) {
                Logger.Error("Could not parse recall spell id: " + recallId);
                return false;
            }

            return true;
        }

        internal override void Write(StreamWriter file) {
            base.Write(file);
            file.WriteLine(RecallSpellId);
        }

        public override void Draw() {
            FileService service = CoreManager.Current.Filter<FileService>();
            var spell = service.SpellTable.GetById(RecallSpellId);

            VTNPoint rp = GetPreviousPoint();
            var color = Color.FromArgb(UtilityBeltPlugin.Instance.VisualNav.Display.Recall.Color);
            VTNPoint point = rp == null ? this : rp;

            if (UtilityBeltPlugin.Instance.VisualNav.Display.Recall.Enabled) {
                DrawText(spell.Name, point, 0.25f, color);
                DrawIcon(spell.IconId, 0.35f, point);
            }
        }
    }
}
