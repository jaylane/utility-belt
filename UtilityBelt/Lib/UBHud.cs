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
        internal UBHudManager HudManager { get; }
        public DxTexture Texture { get => Hud?.Texture; }
        public bool Enabled {
            get => Hud.Enabled;
            set => Hud.Enabled = value;
        }
        public int ZPriority { get => Hud.ZPriority; }

        public event EventHandler<EventArgs> OnMove;
        public event EventHandler<EventArgs> OnResize;
        public event EventHandler<EventArgs> OnRender;
        public event EventHandler<EventArgs> OnClose;
        public event EventHandler<EventArgs> OnReMake;


        /// <summary>
        /// Use HudManager.Create instead
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        internal UBHud(int x, int y, int width, int height, UBHudManager manager) {
            X = x;
            Y = y;
            Width = width;
            Height = height;

            HudManager = manager;

            Hud = new DxHud(new Point(X, Y), new Size(Width, Height), 0);
            Hud.Enabled = true;

            manager.AddHud(this);
        }

        public void Move(int x, int y) {
            if (x == X && y == Y)
                return;

            X = x;
            Y = y;
            Hud.Location = new Point(X, Y);
            OnMove?.Invoke(this, EventArgs.Empty);
            Render();
        }

        public void ReMake() {
            if (Hud != null) {
                Hud.Enabled = false;
                Hud.Dispose();
                Hud = null;
            }

            Hud = new DxHud(new Point(X, Y), new Size(Width, Height), 0);
            Hud.Enabled = true;
            OnReMake?.Invoke(this, EventArgs.Empty);
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

        public void Close() {
            Enabled = false;
            OnClose?.Invoke(this, EventArgs.Empty);
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

            if (!HudManager.IsHoldingControl)
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

        public void Dispose() {
            HudManager.RemoveHud(this);
            if (Hud != null)
                Hud.Dispose();
        }
    }
}
