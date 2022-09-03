using System;
using System.Drawing;

namespace UtilityBelt.Lib {
    public partial class UBHud : UBHud.Element, IDisposable {

        /// <summary>
        /// UBHud.Button
        /// 
        /// </summary>
        public class Button : Label {
            public override int ZIndex { get; set; } = 15;
            public UInt32 BackgroundColor = 0x7F000000; // aRGB
            public UInt32 BorderColor = 0xD0FFA0A0;
            public UInt32 MouseDownColor = 0xA0E0A030;
            public float BorderWidth = 1;
            public bool Transparent = false;
            public virtual Color CurrentBackgroundColor { get => Color.FromArgb((int)(isMouseOver ? isMouseDown ? MouseDownColor : MakeOpaque(BackgroundColor) : BackgroundColor)); }
            public override string ToString() => $"UBHud.Button[\"{Text}\",{BBox},Z{ZIndex}]";
            public Button(UBHud _hud, Rectangle _bbox, string _text, Event _onClick, bool _enabled) : base(_hud,_bbox,_text, _onClick, _enabled) {
            //Logger.WriteToChat($"({ToString()})A Wild Button has appeared! ");
        }

            public override void Draw() {
                if (!Visible) return;
                //background
                if (!Transparent) hud.Texture.Fill(BBox, CurrentBackgroundColor);
                //border
                hud.Texture.DrawLine(new Point(BBox.X, BBox.Y), new Point(BBox.X + BBox.Width - 1, BBox.Y), Color.FromArgb((int)BorderColor), BorderWidth);
                hud.Texture.DrawLine(new Point(BBox.X + BBox.Width - 1, BBox.Y), new Point(BBox.X + BBox.Width - 1, BBox.Y + BBox.Height - 1), Color.FromArgb((int)BorderColor), BorderWidth);
                hud.Texture.DrawLine(new Point(BBox.X + BBox.Width - 1, BBox.Y + BBox.Height - 1), new Point(BBox.X, BBox.Y + BBox.Height - 1), Color.FromArgb((int)BorderColor), BorderWidth);
                hud.Texture.DrawLine(new Point(BBox.X, BBox.Y + BBox.Height - 1), new Point(BBox.X, BBox.Y), Color.FromArgb((int)BorderColor), BorderWidth);
                // the word.

                base.Draw();
            }
        }

    }
}
