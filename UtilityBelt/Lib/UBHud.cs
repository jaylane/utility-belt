using Decal.Adapter;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VirindiViewService;

namespace UtilityBelt.Lib {
    public class UBHud : IDisposable {
        public DxHud Hud { get; private set; }

        public bool IsDraggable { get; set; } = true;
        public bool IsCloseable { get; set; } = true;

        public int X { get; private set; }
        public int Y { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public DxTexture Texture { get => Hud?.Texture; }
        public bool Enabled {
            get => Hud.Enabled;
            set => Hud.Enabled = value;
        }

        public event EventHandler<EventArgs> OnMove;
        public event EventHandler<EventArgs> OnResize;
        public event EventHandler<EventArgs> OnRender;
        public event EventHandler<EventArgs> OnClose;

        private Point lastMousePos;
        private Point dragStartPos;
        private Point dragOffset;
        private bool isDragging;
        private bool isHoldingControl;
        const short WM_MOUSEMOVE = 0x0200;
        const short WM_LBUTTONDOWN = 0x0201;
        const short WM_LBUTTONUP = 0x0202;

        public UBHud(int x, int y, int width, int height) {
            X = x;
            Y = y;
            Width = width;
            Height = height;

            Hud = new DxHud(new Point(X, Y), new Size(Width, Height), 0);
            Hud.Enabled = true;

            CoreManager.Current.WindowMessage += Core_WindowMessage;
        }

        public void Move(int x, int y) {
            if (x == X && y == Y)
                return;

            X = x;
            Y = y;
            Hud.Location = new Point(X, Y);
            OnMove?.Invoke(this, EventArgs.Empty);
        }

        public void Resize(int width, int height) {
            if (width == Width && Height == height)
                return;

            Width = width;
            Height = height;
            Hud.Dispose();
            Hud = new DxHud(new Point(X, Y), new Size(Width, Height), 0);
            OnResize?.Invoke(this, EventArgs.Empty);
        }

        public void DrawShadowText(string text, int x, int y, int width, int height, Color textColor, Color shadowColor, WriteTextFormats format = WriteTextFormats.None) {
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

        public void Render() {
            if (Hud == null || !Hud.Enabled || Hud.Texture == null || Hud.Texture.IsDisposed)
                return;

            OnRender?.Invoke(this, EventArgs.Empty);

            if (!isHoldingControl)
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

        private void Core_WindowMessage(object sender, WindowMessageEventArgs e) {
            var ctrl = (Control.ModifierKeys & Keys.Control) == Keys.Control;
            if (ctrl != isHoldingControl) {
                isHoldingControl = ctrl;
                Render();
            }

            if (!isHoldingControl) {
                if (isDragging) {
                    isDragging = false;
                    Move(X + dragOffset.X, Y + dragOffset.Y);
                    dragOffset = new Point(0, 0);
                }
                return;
            }

            if (e.Msg == WM_MOUSEMOVE || e.Msg == WM_LBUTTONDOWN) {
                var mousePos = new Point(e.LParam);
                if (!isDragging && (mousePos.X < X || mousePos.X > X + Texture.Width || mousePos.Y < Y || mousePos.Y > Y + Texture.Height))
                    return;
            }

            switch (e.Msg) {
                case WM_LBUTTONDOWN:
                    var newMousePos = new Point(e.LParam);
                    // check for clicking close button
                    if (IsCloseable && newMousePos.X > X + Texture.Width - 16 && newMousePos.X < X + Texture.Width && newMousePos.Y > Y && newMousePos.Y < Y + 16) {
                        Enabled = false;
                        OnClose?.Invoke(this, EventArgs.Empty);
                        return;
                    }
                    if (IsDraggable) {
                        isDragging = true;
                        dragStartPos = newMousePos;
                        dragOffset = new Point(0, 0);
                    }
                    break;

                case WM_LBUTTONUP:
                    if (isDragging) {
                        isDragging = false;
                        Move(X + dragOffset.X, Y + dragOffset.Y);
                        dragOffset = new Point(0, 0);
                    }
                    break;

                case WM_MOUSEMOVE:
                    lastMousePos = new Point(e.LParam);
                    if (isDragging) {
                        dragOffset.X = lastMousePos.X - dragStartPos.X;
                        dragOffset.Y = lastMousePos.Y - dragStartPos.Y;
                        Hud.Location = new Point(X + dragOffset.X, Y + dragOffset.Y);
                    }
                    break;
            }
        }

        public void Dispose() {
            CoreManager.Current.WindowMessage -= Core_WindowMessage;
            if (Hud != null)
                Hud.Dispose();
        }
    }
}
