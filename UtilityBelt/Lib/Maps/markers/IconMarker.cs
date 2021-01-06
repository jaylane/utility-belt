using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using VirindiViewService;

namespace UtilityBelt.Lib.Maps.Markers {
    public class IconMarker : BaseMarker {
        public DxTexture Texture { get; private set; }

        /// <summary>
        /// Set this to false to handle disposing of the texture yourself
        /// </summary>
        public bool ManageDxTexture { get; set; } = true;

        public static int IconSize = 16;
        internal static int iconDrawCount = 0;

        public IconMarker(double ew, double ns, DxTexture dxTexture) : base(ew, ns) {
            Texture = dxTexture;
            ZOrder = 1;

            MouseOverRect = new Rectangle(-(IconSize/2), -(IconSize/2), IconSize, IconSize);
        }

        public override bool Draw(DxTexture texture, int x, int y, double zoom, bool highlight) {
            if (!base.Draw(texture, x, y, zoom, highlight))
                return false;
            if (Texture == null || Texture.IsDisposed) {
                Dispose();
                return false;
            }

            var iconRect = new Rectangle(x - (IconSize / 2), y - (IconSize / 2), IconSize, IconSize);

            if (highlight) {
                // background
                texture.DrawLine(new PointF(iconRect.X, iconRect.Y + (IconSize / 2)), new PointF(iconRect.X + IconSize, iconRect.Y + (IconSize / 2)), Color.FromArgb(180, 0, 0, 0), IconSize);
                // border
                texture.DrawLine(new PointF(iconRect.X, iconRect.Y), new PointF(iconRect.X + IconSize, iconRect.Y), Color.Orange, 1);
                texture.DrawLine(new PointF(iconRect.X + IconSize, iconRect.Y), new PointF(iconRect.X + IconSize, iconRect.Y + IconSize), Color.Orange, 1);
                texture.DrawLine(new PointF(iconRect.X + IconSize, iconRect.Y + IconSize), new PointF(iconRect.X, iconRect.Y + IconSize), Color.Orange, 1);
                texture.DrawLine(new PointF(iconRect.X, iconRect.Y + IconSize), new PointF(iconRect.X, iconRect.Y), Color.Orange, 1);
            }

            texture.DrawTexture(Texture, iconRect);

            // only add this to the collision detection if we are drawing labels as well
            if (Label != null && zoom >= Label.MinZoomLevel && zoom <= Label.MaxZoomLevel)
                DrawRects.Add(iconRect);

            iconDrawCount++;

            return true;
        }

        public override void Dispose() {
            if (ManageDxTexture && Texture != null && !Texture.IsDisposed)
                Texture.Dispose();
            base.Dispose();
        }

        new internal static void ResetDraw() {
            iconDrawCount = 0;
        }
    }
}
