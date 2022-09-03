using System;
using System.Drawing;
using System.IO;
using UtilityBelt.Views;

namespace UtilityBelt.Lib {
    public partial class UBHud : UBHud.Element, IDisposable {

        /// <summary>
        /// UBHud.Button
        /// 
        /// </summary>
        public class Image : Label {
            public override int ZIndex { get; set; } = 2;
            public Bitmap BMP;
            public override string ToString() => $"UBHud.Image[{BBox},Z{ZIndex}]";
            public Image(UBHud _hud, Rectangle _bbox, Event _onClick, Bitmap _bmp) : base(_hud, _bbox, "", _onClick, true) {
                BMP = _bmp;
                //Logger.WriteToChat($"({ToString()})A Wild Image has appeared! ");
            }

            public override void Draw() {
                if (!Visible) return;
                hud.Texture.DrawImage(BMP, BBox, Color.Empty);
                //base.Draw();
            }
        }
    }
}
