using Decal.Adapter;
using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using UtilityBelt.Views;
using VirindiViewService;
using WriteTextFormats = VirindiViewService.WriteTextFormats;

namespace UtilityBelt.Lib {
    public partial class UBHud : UBHud.Element, IDisposable {
        public DxHud Hud { get; private set; }

        public int ZIndex { get; set; } = -1;
        public UBHud hud => this;
        public bool IsDraggable { get; set; } = true;
        public bool IsCloseable { get; set; } = true;
        public Rectangle BBox { get; private set; }

        internal UBHudManager HudManager { get; }
        public DxTexture Texture { get => (Hud?.Texture); }
        public bool Enabled {
            get => Hud.Enabled;
            set => Hud.Enabled = value;
        }
        public int ZPriority { get => Hud.ZPriority; }


        Rectangle Element.BBox => throw new NotImplementedException();

        public event Event OnMove;
        public event Event OnResize;
        public event Event OnRender;
        public event Event OnClose;
        public event Event OnReMake;
        public event EventHandler<WindowMessageEventArgs> OnWindowMessage;
        public event Event OnClick;
        public event Key OnKey;

        public UInt32 BackgroundColor = 0xA0000000;
        public bool Transparent = true;

        /// <summary>
        /// Element standard delegate
        /// </summary>
        public delegate void Event();
        public delegate void Key(WinKeys _key, bool isDown, double holdTime);

        protected virtual bool Click() {
            if (OnClick != null) {
                OnClick.Invoke();
                return true;
            }
            return false;
        }

        internal void Event_Key(WinKeys _key, bool isDown, double holdTime) {
            if (OnKey != null) OnKey.Invoke(_key, isDown, holdTime);
        }
        public void RegisterElement(Element _element) {
            Elements.Add(_element);
            Elements.Sort((h1, h2) => {
                return h2.ZIndex.CompareTo(h1.ZIndex);
            });
        }
        public void UnRegisterElement(Element _element) {
            if (!Elements.Contains(_element)) {
                Logger.WriteToChat($"Logic Error- UBHud.UnRegisterElement({_element})");
                return;
            }
            Elements.Remove(_element);
        }
        public List<Element> Elements = new List<Element>();

        /// <summary>
        /// Use HudManager.Create instead
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        internal UBHud(int x, int y, int width, int height, UBHudManager manager) {
            try {
                BBox = new Rectangle(x, y, width, height);

                HudManager = manager;

                Hud = new DxHud(BBox.Location, BBox.Size, 0) {
                    Enabled = true
                };

            }
            catch (Exception ex) { Logger.LogException(ex); }
        }


        public bool MouseUp(Point _pt, Double _hold_time) => false;
        public bool MouseDown(Point _pt) => false;


        private bool isMouseOver = false;
        public bool MouseFocus() {
            if (!Enabled) return false;
            //Logger.WriteToChat($"(UBHud) the mouse has entered me {(isMouseOver ? "And I'm beind double-penetrated!" : "")}");
            isMouseOver = true;
            hud.Render();
            // change background
            return true;
        }
        public bool MouseBlur() {
            if (!Enabled) return false;
            //Logger.WriteToChat($"(UBHud) the mouse has left me");
            isMouseOver = false;
            hud.Render();
            // change background
            return true;
        }

        public void Draw() { }

        public bool IsCloseClick(Point _mousePos) => IsCloseable && new Rectangle(BBox.Width - 16, 0, 16, 16).Contains(TranslateMouseToHud(_mousePos));
        public bool IsMouseOver(Point _mousePos) => BBox.Contains(_mousePos);

        public Point TranslateMouseToHud(Point _mousePos) => _mousePos - new Size(BBox.Location);

        public void Move(int x, int y) => Move(new Point(x, y));


        public void Move(Point _pt) {
            if (_pt == BBox.Location) return;
            BBox = new Rectangle(_pt, BBox.Size);
            Hud.Location = BBox.Location;
            if (OnMove != null) { OnMove.Invoke(); }
            Render();
        }

        public void ReMake() {
            try {
                if (Hud != null) {
                    Hud.Enabled = false;
                    Hud.Dispose();
                    Hud = null;
                }

                Hud = new DxHud(BBox.Location, BBox.Size, 0) {
                    Enabled = true
                };
                if (OnReMake != null) OnReMake.Invoke();

            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public void Resize(Size _sz) {
            if (_sz == BBox.Size) return;
            BBox = new Rectangle(BBox.Location, _sz);
            var enabled = Hud.Enabled;
            Hud.Dispose();
            Hud = new DxHud(BBox.Location, BBox.Size, 0) {
                Enabled = enabled
            };
            if (OnResize != null) OnResize.Invoke();
            Render();
        }
        public void Resize(int width, int height) => Resize(new Size(width, height));

        internal void HandleWindowMessage(WindowMessageEventArgs e) {
            if (OnWindowMessage != null) OnWindowMessage.Invoke(this, e);
        }

        public void Close() {
            Enabled = false;
            if (OnClose != null) OnClose.Invoke();
        }

        public void DrawShadowText(string text, int x, int y, int width, int height, Color textColor, Color shadowColor, WriteTextFormats format = WriteTextFormats.None) {
            try {
                // WriteText with shadow doesn't seem to work... so...
                Texture.WriteText(text, shadowColor, format, new Rectangle(x - 1, y - 1, width, height));
                Texture.WriteText(text, shadowColor, format, new Rectangle(x + 1, y - 1, width, height));
                Texture.WriteText(text, shadowColor, format, new Rectangle(x - 1, y + 1, width, height));
                Texture.WriteText(text, shadowColor, format, new Rectangle(x + 1, y + 1, width, height));
                Texture.WriteText(text, shadowColor, format, new Rectangle(x - 1, y, width, height));
                Texture.WriteText(text, shadowColor, format, new Rectangle(x + 1, y, width, height));
                Texture.WriteText(text, shadowColor, format, new Rectangle(x, y + 1, width, height));
                Texture.WriteText(text, shadowColor, format, new Rectangle(x, y - 1, width, height));

                Texture.WriteText(text, textColor, format, new Rectangle(x, y, width, height));

            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public void Render() {
            if (!Enabled)
                return;

            // this can happen sometimes, not exactly sure when, but i can force it by
            // resetting my gpu drivers with ctrl+shift+win+b
            if (Hud.Texture.IsDisposed) {
                ReMake();
                return;
            }
            hud.Texture.Clear();
            if (!Transparent) hud.Texture.Fill(new Rectangle(new Point(0, 0), BBox.Size), Color.FromArgb((int)BackgroundColor));

            if (OnRender != null) OnRender.Invoke();
            try {
                if (hud == null || hud.Texture == null || hud.Texture.IsDisposed)
                    return;
                hud.Texture.BeginRender();
                foreach (var element in hud.Elements) element.Draw();
            }
            catch (Exception ex) { Logger.LogException(ex); }
            finally {
                hud.Texture.EndRender();
            }

            if (!HudManager.IsHoldingControl || !isMouseOver)
                return;

            try {
                Texture.BeginRender();

                // border highlight
                Texture.DrawLine(new PointF(0, 0), new PointF(Texture.Width - 1, 0), Color.Yellow, 1);
                Texture.DrawLine(new PointF(Texture.Width - 1, 0), new PointF(Texture.Width - 1, Texture.Height - 1), Color.Yellow, 1);
                Texture.DrawLine(new PointF(Texture.Width - 1, Texture.Height - 1), new PointF(0, Texture.Height - 1), Color.Yellow, 1);
                Texture.DrawLine(new PointF(0, Texture.Height - 1), new PointF(0, 0), Color.Yellow, 1);

                // close icon
                if (IsCloseable)
                    Texture.DrawPortalImage(0x060011F8, new Rectangle(Texture.Width - 16, 0, 16, 16));
            }
            catch (Exception ex) { Logger.LogException(ex); }
            finally {
                Texture.EndRender();
            }
        }

        private static UInt32 MakeOpaque(UInt32 _in) {
            return (_in & 0x00FFFFFF) + 0xFF000000;
        }
        private static UInt32 MakeAlmostOpaque(UInt32 _in) {
            return (_in & 0x00FFFFFF) + 0xD0000000;
        }
        private static UInt32 MakeDisabled(UInt32 _in) {
            return (_in & 0x00FFFFFF) + 0x7F000000;
        }

        public Bitmap GetIcon(string resourcePath) {
            Bitmap ubImg = null;
            try {
                using Stream manifestResourceStream = typeof(MainView).Assembly.GetManifestResourceStream(resourcePath);
                ubImg = new Bitmap(manifestResourceStream);
            }
            catch (Exception ex) { Logger.LogException(ex); }
            return ubImg;
        }
        #region IDisposable Support
        protected bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    HudManager.DestroyHud(this);
                    Elements = null;
                    if (Hud != null)
                        Hud.Dispose();


                }
                disposedValue = true;
            }
        }


        public void Dispose() {
            Dispose(true);
        }
        #endregion

    }
}
