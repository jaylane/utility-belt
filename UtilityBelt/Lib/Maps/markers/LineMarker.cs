using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using VirindiViewService;

namespace UtilityBelt.Lib.Maps.Markers {
    public class LineMarker : BaseMarker {
        public double NS2 { get; set; }
        public double EW2 { get; set; }
        public Color Color { get; set; }
        public int Thickness { get; set; }

        public LineMarker(double ew1, double ns1, double ew2, double ns2, Color color, int thickness=1) : base(ew1, ns1) {
            ZOrder = 2;
            Color = color;
            Thickness = thickness;
            EW2 = ew2;
            NS2 = ns2;
        }

        public override bool Draw(DxTexture texture, int x, int y, double zoom, bool highlight) {
            //if (!base.Draw(texture, x, y, zoom, highlight))
            //    return false;

            var startPos = UtilityBeltPlugin.Instance.LandscapeMaps.CoordinatesToHud(EW, NS);
            var endPos = UtilityBeltPlugin.Instance.LandscapeMaps.CoordinatesToHud(EW2, NS2);

            texture.DrawLine(startPos, endPos, Color, Thickness);
            return true;
        }

        public override void Dispose() {
            base.Dispose();
        }

        new internal static void ResetDraw() {
        }
    }
}
