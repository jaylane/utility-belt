using System;
using System.Drawing;
using static System.Net.Mime.MediaTypeNames;

namespace UtilityBelt.Lib {
    public partial class UBHud : UBHud.Element, IDisposable {

        /// <summary>
        /// UBHud.Block
        /// Presents 3 text boxes: left, center, right, with a border around them.
        /// BackGroundWidth functions as a progress bar.
        /// </summary>
        public class Block : Button, Element {
            public override int ZIndex { get; set; } = 2;
            public UInt32 TextFaceColor_Left = 0xD0C0C0C0;
            public UInt32 TextFaceColor_Right = 0xD0C0C0C0;
            public UInt32 BarColor = 0xA0704000;
            public int BarWidth = 0;
            public string Text_Left;
            public string Text_Right;
            public object Back_Pocket;
            public Color CurrentFontColor_Left { get => Color.FromArgb((int)(Enabled ? TextFaceColor_Left : MakeDisabled(TextFaceColor_Left))); }
            public Color CurrentFontColor_Right { get => Color.FromArgb((int)(Enabled ? TextFaceColor_Right : MakeDisabled(TextFaceColor_Right))); }
            public Color CurrentBarColor { get => Color.FromArgb((int)(isMouseOver ? MakeOpaque(BarColor) : BarColor)); }
            public override string ToString() => $"UBHud.Block[\"{Text}\",{BBox},Z{ZIndex}]";
            public Block(UBHud _hud, Rectangle _bbox, string _left, string _center, string _right, object _back_pocket, int _backgroundwidth = 0, Event _onClick = null) : base(_hud, _bbox, _center, _onClick, true) {
                Text_Left = _left;
                Text_Right = _right;
                BarWidth = _backgroundwidth;
                Back_Pocket = _back_pocket;
            }
            public override bool MouseUp(Point _pt, Double _hold_time) {
                if (!Visible || !Enabled) return false;
                isMouseDown = false;
                hud.Render();
                if (_hold_time < 0.65) {
                    if (Click()) return true;
                    Logger.WriteToChat($"{Back_Pocket}");
                }
                //Logger.WriteToChat($"({ToString()}) I was released after {_hold_time} seconds, so I'm going to do nothing.");
                return false;
            }
            public override bool MouseDown(Point _pt) {
                if (!Visible || !Enabled) return false;
                //Logger.WriteToChat($"({ToString()}) I'm being held down {(isMouseDown ? "And I'm beind double-penetrated!" : "")}");
                isMouseDown = true;
                hud.Render();
                return true;
            } // change background
            public override void Draw() {
                if (!Visible) return;
                //background
                if (!Transparent) {
                    hud.Texture.Fill(BBox, CurrentBackgroundColor);
                    //bar
                    hud.Texture.Fill(new Rectangle(BBox.X, BBox.Y, BarWidth, BBox.Height), CurrentBarColor);
                    Transparent = true;
                    base.Draw();
                    Transparent = false;
                }
                else {
                    //bar
                    hud.Texture.Fill(new Rectangle(BBox.X, BBox.Y, BarWidth, BBox.Height), CurrentBarColor);
                    base.Draw();
                }
                // more words.
                hud.Texture.BeginText(FontFace, FontSize, 0, false);
                hud.Texture.WriteText(Text_Left, CurrentFontColor_Left, VirindiViewService.WriteTextFormats.VerticalCenter, BBox);
                hud.Texture.WriteText(Text_Right, CurrentFontColor_Right, VirindiViewService.WriteTextFormats.VerticalCenter | VirindiViewService.WriteTextFormats.Right, BBox);
                hud.Texture.EndText();
            }
        }
    }
}