using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using VirindiViewService;

namespace UtilityBelt.Lib.Maps.Markers {
    public class LabelMarker : BaseMarker {
        public string LabelText { get; set; }
        public BaseMarker Parent { get; private set; }

        protected Bitmap fontTexture;

        protected static Graphics fontSizeGfx = null;
        protected static Font labelFont = null;
        protected static SolidBrush labelBrush = null;
        protected static Bitmap fontSizeBmp = new Bitmap(1, 1);
        protected static SolidBrush labelBackgroundBrush = new SolidBrush(Color.Black);
        internal static Dictionary<uint, Rectangle> labelDrawPositions = new Dictionary<uint, Rectangle>();
        internal static int labelDrawCount = 0;

        public LabelMarker(string labelText, double ew, double ns) : base(ew, ns) {
            LabelText = labelText;

            if (labelFont == null)
                labelFont = new Font(UtilityBeltPlugin.Instance.LandscapeMapView.view.MainControl.Theme.GetVal<string>("DefaultTextFontFace"), 9f);
            if (labelBrush == null)
                labelBrush = new SolidBrush(Color.White);
            if (fontSizeGfx == null)
                fontSizeGfx = Graphics.FromImage(fontSizeBmp);

            EnsureCachedLabel(LabelText);
        }

        public void SetParent(BaseMarker marker) {
            Parent = marker;
            marker.AttachLabel(this);
        }

        public override bool Draw(DxTexture texture, int x, int y, double zoom, bool highlight) {
            if (!base.Draw(texture, x, y, zoom, highlight))
                return false;

            var potentialLabelRects = GetPotentialLabelRects(x, y);
            var drawRect = new Rectangle();

            if (highlight) {
                drawRect = labelDrawPositions.ContainsKey(Id) ? labelDrawPositions[Id] : potentialLabelRects[0];
                texture.DrawLine(new PointF(drawRect.X, drawRect.Y + (drawRect.Height / 2)), new PointF(drawRect.X + drawRect.Width, drawRect.Y + (drawRect.Height / 2)), Color.FromArgb(180, 0, 0, 0), drawRect.Height);
                texture.DrawLine(new PointF(drawRect.X, drawRect.Y), new PointF(drawRect.X + drawRect.Width, drawRect.Y), Color.Orange, 1);
                texture.DrawLine(new PointF(drawRect.X + drawRect.Width, drawRect.Y), new PointF(drawRect.X + drawRect.Width, drawRect.Y + drawRect.Height), Color.Orange, 1);
                texture.DrawLine(new PointF(drawRect.X + drawRect.Width, drawRect.Y + drawRect.Height), new PointF(drawRect.X, drawRect.Y + drawRect.Height), Color.Orange, 1);
                texture.DrawLine(new PointF(drawRect.X, drawRect.Y + drawRect.Height), new PointF(drawRect.X, drawRect.Y), Color.Orange, 1);
            }
            else {
                foreach (var rect in potentialLabelRects) {
                    bool intersects = false;
                    foreach (var existingRect in DrawRects) {
                        if (rect.IntersectsWith(existingRect)) {
                            intersects = true;
                            break;
                        }
                    }
                    if (!intersects) {
                        DrawRects.Add(rect);
                        drawRect = rect;
                        if (!labelDrawPositions.ContainsKey(Id))
                            labelDrawPositions.Add(Id, rect);
                        break;
                    }
                }
            }

            if (highlight || labelDrawPositions.ContainsKey(Id)) {
                texture.DrawImage(fontTexture, new Rectangle(drawRect.X + 2, drawRect.Y, drawRect.Width, drawRect.Height), Color.Magenta);
            }
            else {
                labelDrawPositions[Id] = potentialLabelRects[0];
            }

            labelDrawCount++;

            return true;
        }

        private List<Rectangle> GetPotentialLabelRects(int x, int y) {
            int w = fontTexture.Width;
            int h = fontTexture.Height;
            x = x - (w / 2);
            y = y - (h / 2) - 16;

            return new List<Rectangle>() {
                    new Rectangle(x, y, w, h),                   // above
                    new Rectangle(x + 4 + (w/2), y + 2, w, h),   // topright
                    new Rectangle(x + 8 + (w/2), y + 15, w, h),  // right
                    new Rectangle(x + 8 + (w/2), y + 26, w, h),  // bottomright
                    new Rectangle(x, y + 28, w, h),              // below
                    new Rectangle(x - 10- (w/2), y + 24, w, h),  // bottomleft
                    new Rectangle(x - 10 - (w/2), y + 15, w, h), // left
                    new Rectangle(x - 6 - (w/2), y + 4, w, h),   // topleft
                };
        }

        private void EnsureCachedLabel(string text) {
            SizeF stringSize = fontSizeGfx.MeasureString(text, labelFont);
            int labelWidth = (int)Math.Ceiling(stringSize.Width);
            int labelHeight = (int)Math.Ceiling(stringSize.Height);
            var label = new Bitmap(labelWidth, labelHeight);
            using (var gfx = Graphics.FromImage(label)) {
                gfx.DrawString(LabelText, labelFont, labelBackgroundBrush, new Point(-1, 0));
                gfx.DrawString(LabelText, labelFont, labelBackgroundBrush, new Point(0, -1));
                gfx.DrawString(LabelText, labelFont, labelBackgroundBrush, new Point(1, 0));
                gfx.DrawString(LabelText, labelFont, labelBackgroundBrush, new Point(0, 1));
                gfx.DrawString(LabelText, labelFont, labelBrush, new Point(0, 0));
            }
            fontTexture = label;
        }

        new internal static void ResetDraw() {
            labelDrawPositions.Clear();
            labelDrawCount = 0;
        }

        public override void Dispose() {
            base.Dispose();
            if (fontTexture != null)
                fontTexture.Dispose();
        }
    }
}
