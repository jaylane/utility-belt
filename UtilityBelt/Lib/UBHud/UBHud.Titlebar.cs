using AcClient;
using System;
using System.Drawing;
using System.IO;
using UtilityBelt.Views;
using VirindiViewService;
using static UtilityBelt.Tools.Player;

namespace UtilityBelt.Lib {
    public partial class UBHud : UBHud.Element, IDisposable {

        /// <summary>
        /// UBHud.Button
        /// 
        /// </summary>
        public class Titlebar : Element {
            public UBHud hud { get; private set; }
            public virtual int ZIndex { get; set; } = 10;
            public UInt32 BackgroundColor = 0x7F000000; // aRGB
            public UInt32 BorderColor = 0xD0FFA0A0;
            public UInt32 MouseDownColor = 0xA0E0A030;
            public virtual Rectangle BBox { get; set; } = Rectangle.Empty;
            public UInt32 FontColor = 0xD0FFFFFF; // aRGB
            public VirindiViewService.WriteTextFormats Alignment = VirindiViewService.WriteTextFormats.VerticalCenter;

            public Rectangle IconPos;
            public Rectangle TitlePos;

            public bool ShowPageButton = false;
            public UBHud.Button prevBTN;
            public UBHud.Button nextBTN;
            public UBHud.Button closeBTN;
            public UBHud.Label IndexLabel;
            public UBHud.Image IconImage;
            public string FontFace = "Arial";
            public int FontSize = 10;
            public string Text;
            public float BorderWidth = 2;
            public bool Transparent = false;
            public bool Visible = true;
            public bool Enabled = true;
            internal bool isMouseDown = false;
            internal bool isMouseOver = false;

            public override string ToString() => $"UBHud.Titlebar[\"{Text}\",{BBox},Z{ZIndex}]";
            public Color CurrentFontColor { get => Color.FromArgb((int)(Enabled ? FontColor : MakeDisabled(FontColor))); }
            public Color CurrentBackgroundColor { get => Color.FromArgb((int)(isMouseOver ? isMouseDown ? MouseDownColor : MakeOpaque(BackgroundColor) : BackgroundColor)); }

            public Titlebar(UBHud _hud, Rectangle _bbox, string _text, Event _onClick, bool _showPageButton) {
                hud = _hud;
                BBox = _bbox;
                Text = _text;
                if (_bbox.Height <= 16)
                    FontSize = _bbox.Height - 7;
                if (_onClick != null) OnClick += _onClick;

                BorderColor = 0x90D64B0F;
                Transparent = true;
                BorderWidth = 2;
                ShowPageButton = _showPageButton;
                if (_showPageButton) {
                    prevBTN = new UBHud.Button(hud, new Rectangle(BBox.X + BBox.Width - BBox.Height + 2 - 155, 1, 55, BBox.Height - 2), "<PREV", PrevClick, true);
                    IndexLabel = new UBHud.Label(hud, new Rectangle(BBox.X + BBox.Width - BBox.Height + 2 - 100, 1, 45, BBox.Height - 2), $"...", null);
                    nextBTN = new UBHud.Button(hud, new Rectangle(BBox.X + BBox.Width - BBox.Height + 2 - 55, 1, 55, BBox.Height - 2), "NEXT>", NextClick, true);
                    closeBTN = new UBHud.Button(hud, new Rectangle(BBox.X + BBox.Width - BBox.Height + 2, 1, BBox.Height - 2, BBox.Height - 2), "X", hud.Close, true);
                }

                IconImage = new UBHud.Image(hud, new Rectangle(BBox.X, BBox.Y, BBox.Height, BBox.Height), null, hud.GetIcon("UtilityBelt.Resources.icons.utilitybelt.png"));
                TitlePos = new Rectangle(BBox.Height, BBox.Y, BBox.Width - BBox.Height, BBox.Height); //todo this should end contidional, instead of going under prev/next buttons
                hud.RegisterElement(this);
            }
            public bool MouseUp(Point _pt, Double _hold_time) => false;
            public bool MouseDown(Point _pt) => false;
            public virtual bool MouseFocus() => false;
            public virtual bool MouseBlur() => false;
            public void Draw() {
                if (!Visible) return;
                //background
                if (!Transparent) hud.Texture.Fill(BBox, CurrentBackgroundColor);
                //border
                hud.Texture.DrawLine(new Point(BBox.X, BBox.Y), new Point(BBox.X + BBox.Width - 1, BBox.Y), Color.FromArgb((int)BorderColor), BorderWidth);
                hud.Texture.DrawLine(new Point(BBox.X + BBox.Width - 1, BBox.Y), new Point(BBox.X + BBox.Width - 1, BBox.Y + BBox.Height - 1), Color.FromArgb((int)BorderColor), BorderWidth);
                hud.Texture.DrawLine(new Point(BBox.X + BBox.Width - 1, BBox.Y + BBox.Height - 1), new Point(BBox.X, BBox.Y + BBox.Height - 1), Color.FromArgb((int)BorderColor), BorderWidth);
                hud.Texture.DrawLine(new Point(BBox.X, BBox.Y + BBox.Height - 1), new Point(BBox.X, BBox.Y), Color.FromArgb((int)BorderColor), BorderWidth);

                // label
                hud.Texture.BeginText(FontFace, FontSize, 1, false);
                hud.Texture.WriteText(Text, CurrentFontColor, Alignment, TitlePos);
                hud.Texture.EndText();
            }
            public virtual bool IsMouseOver(Point _pt) => false;
            public event Event OnClick;
            protected virtual bool Click() {
                if (OnClick != null) {
                    OnClick.Invoke();
                    return true;
                }
                return false;
            }
            public event Event OnPrevClick;
            private void PrevClick() => OnPrevClick?.Invoke();
            public event Event OnNextClick;
            private void NextClick() => OnNextClick?.Invoke();
        }

    }
}
