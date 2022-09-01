using System;
using System.Drawing;

namespace UtilityBelt.Lib {
    public partial class UBHud : UBHud.Element, IDisposable {
        /// <summary>
        /// UBHud.Label
        /// </summary>
        public class Label : Element {
            public UBHud hud { get; private set; }
            public virtual int ZIndex { get; set; } = 1;
            public virtual Rectangle BBox { get; set; } = Rectangle.Empty;
            public UInt32 FontColor = 0xD0FFFFFF; // aRGB
            public VirindiViewService.WriteTextFormats Alignment;
            public string FontFace = "Arial";
            public int FontSize = 10;
            public string Text;
            public bool Visible = true;
            public bool Enabled = true;
            public override string ToString() => $"UBHud.Label[\"{Text}\",{BBox},Z{ZIndex}]";
            internal bool isMouseDown = false;
            internal bool isMouseOver = false;

            public Color CurrentFontColor { get => Color.FromArgb((int)(Enabled ? FontColor : MakeDisabled(FontColor))); }
            public Label(UBHud _hud, Rectangle _bbox, string _text, Event _onClick, bool _enabled = true, VirindiViewService.WriteTextFormats _alignment = VirindiViewService.WriteTextFormats.VerticalCenter | VirindiViewService.WriteTextFormats.Center) {
                hud = _hud;
                BBox = _bbox;
                Text = _text;
                Enabled = _enabled;
                Alignment = _alignment;
                if (_bbox.Height <= 16)
                    FontSize = _bbox.Height - 7;
                if (_onClick != null) OnClick += _onClick;
                hud.RegisterElement(this);
            }
            public virtual bool MouseUp(Point _pt, Double _hold_time) {
                if (!Visible || !Enabled) return false;
                isMouseDown = false;
                hud.Render();
                if (_hold_time < 0.65)
                    return Click();
                //Logger.WriteToChat($"({ToString()}) I was released after {_hold_time} seconds, so I'm going to do nothing.");
                return false;
            }
            public virtual bool MouseDown(Point _pt) {
                if (!Visible || !Enabled) return false;
                //Logger.WriteToChat($"({ToString()}) I'm being held down {(isMouseDown ? "And I'm beind double-penetrated!" : "")}");
                isMouseDown = true;
                hud.Render();
                return true;
            } // change background
            public virtual bool MouseFocus() {
                if (!Visible || !Enabled) return false;
                //Logger.WriteToChat($"({ToString()}) the mouse has entered me {(isMouseOver ? "And I'm beind double-penetrated!" : "")}");
                isMouseOver = true;
                hud.Render();
                // change background
                return true;
            }
            public virtual bool MouseBlur() {
                if (!Visible || !Enabled) return false;
                //Logger.WriteToChat($"({ToString()}) the mouse has left me");
                isMouseOver = false;
                hud.Render();
                // change background
                return true;
            }
            public virtual void Draw() {
                hud.Texture.BeginText(FontFace, FontSize, 0, false);
                hud.Texture.WriteText(Text, CurrentFontColor, Alignment, BBox);
                hud.Texture.EndText();
            }
            public virtual bool IsMouseOver(Point _pt) => new Rectangle(hud.BBox.X + BBox.X, hud.BBox.Y + BBox.Y, BBox.Width, BBox.Height).Contains(_pt);

            public event Event OnClick;
            protected virtual bool Click() {
                if (OnClick != null) {
                    OnClick.Invoke();
                    return true;
                }
                return false;
            }
        }
    }
}
