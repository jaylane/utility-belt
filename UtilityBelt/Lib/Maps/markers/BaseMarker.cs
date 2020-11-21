using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using VirindiViewService;

namespace UtilityBelt.Lib.Maps.Markers {
    public class BaseMarker : IDisposable {
        private static uint id = 0;

        public bool IsDisposed { get; private set; }
        public uint Id { get; private set; }
        public double NS { get; set; }
        public double EW { get; set; }
        public double MinZoomLevel { get; set; } = 0.0;
        public double MaxZoomLevel { get; set; } = 1.0;
        public double ZOrder { get; set; } = 1;
        public LabelMarker Label { get; protected set; }
        public Rectangle MouseOverRect { get; protected set; }
        internal static List<Rectangle> DrawRects { get; private set; } = new List<Rectangle>();

        public BaseMarker(double ew, double ns) {
            Id = ++id;
            NS = ns;
            EW = ew;
        }

        internal void AttachLabel(LabelMarker label) {
            Label = label;
        }

        public virtual bool Draw(DxTexture texture, int x, int y, double zoom, bool highlight) {
            var coordsVisible = UtilityBeltPlugin.Instance.LandscapeMaps.CoordinatesVisible(EW, NS);
            var correctZoomLevel = zoom >= MinZoomLevel && zoom <= MaxZoomLevel;
            return highlight || (coordsVisible && correctZoomLevel);
        }

        internal static void ResetDraw() {
            DrawRects.Clear();
        }

        public virtual void Dispose() {
            IsDisposed = true;
            UtilityBeltPlugin.Instance.LandscapeMaps.Redraw();
        }
    }
}
