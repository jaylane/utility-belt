using Decal.Adapter;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace UtilityBelt.Lib {
    class UBHudManager : IDisposable {
        List<UBHud> Huds = new List<UBHud>();

        private bool isHandlingEvents = false;
        private Point lastMousePos;
        private Point dragStartPos;
        private Point dragOffset;
        private bool isDragging;
        public bool IsHoldingControl { get; private set; }

        UBHud activeHud = null;

        const short WM_MOUSEMOVE = 0x0200;
        const short WM_LBUTTONDOWN = 0x0201;
        const short WM_LBUTTONUP = 0x0202;

        public UBHud CreateHud(int x, int y, int width, int height) {
            var hud = new UBHud(x, y, width, height, this);
            Huds.Add(hud);
            Huds.Sort((h1, h2) => {
                return h1.ZPriority.CompareTo(h2.ZPriority);
            });
            EnsureEventHandlers();
            return hud;
        }

        public void AddHud(UBHud hud) {
            if (!Huds.Contains(hud))
                Huds.Add(hud);
        }

        public void RemoveHud(UBHud hud) {
            if (Huds.Contains(hud)) {
                Huds.Remove(hud);
                hud.Dispose();
            }
        }

        private void EnsureEventHandlers() {
            if (isHandlingEvents)
                return;

            CoreManager.Current.WindowMessage += Core_WindowMessage;
        }

        private void Core_WindowMessage(object sender, WindowMessageEventArgs e) {
            var isCtrl = (Control.ModifierKeys & Keys.Control) == Keys.Control;

            if (isCtrl != IsHoldingControl) {
                IsHoldingControl = isCtrl;
                foreach (var hud in Huds)
                    hud.Render();
            }

            if (!IsHoldingControl) {
                if (isDragging) {
                    isDragging = false;
                    activeHud.Move(activeHud.X + dragOffset.X, activeHud.Y + dragOffset.Y);
                    dragOffset = new Point(0, 0);
                }
                return;
            }

            if (e.Msg == WM_MOUSEMOVE || e.Msg == WM_LBUTTONDOWN || e.Msg == WM_LBUTTONUP) {
                var mousePos = new Point(e.LParam);

                if (!isDragging) {
                    var foundHud = false;
                    foreach (var hud in Huds) {
                        if ((mousePos.X > hud.X && mousePos.X < hud.X + hud.Width && mousePos.Y > hud.Y && mousePos.Y < hud.Y + hud.Height)) {
                            activeHud = hud;
                            foundHud = true;
                            break;
                        }
                    }
                    if (!foundHud)
                        activeHud = null;
                }
            }
            else
                return;

            if (activeHud == null)
                return;

            switch (e.Msg) {
                case WM_LBUTTONDOWN:
                    var newMousePos = new Point(e.LParam);
                    // check for clicking close button
                    if (IsHoldingControl && activeHud.IsCloseable && newMousePos.X > activeHud.X + activeHud.Width - 16 && newMousePos.X < activeHud.X + activeHud.Width && newMousePos.Y > activeHud.Y && newMousePos.Y < activeHud.Y + 16) {
                        activeHud.Close();
                        return;
                    }
                    if (activeHud.IsDraggable) {
                        isDragging = true;
                        dragStartPos = newMousePos;
                        dragOffset = new Point(0, 0);
                    }
                    break;

                case WM_LBUTTONUP:
                    if (isDragging) {
                        isDragging = false;
                        activeHud.Move(activeHud.X + dragOffset.X, activeHud.Y + dragOffset.Y);
                        dragOffset = new Point(0, 0);
                    }
                    break;

                case WM_MOUSEMOVE:
                    lastMousePos = new Point(e.LParam);
                    if (isDragging) {
                        dragOffset.X = lastMousePos.X - dragStartPos.X;
                        dragOffset.Y = lastMousePos.Y - dragStartPos.Y;
                        activeHud.Hud.Location = new Point(activeHud.X + dragOffset.X, activeHud.Y + dragOffset.Y);
                    }
                    break;
            }
        }

        public void Dispose() {
            if (isHandlingEvents) {
                CoreManager.Current.WindowMessage -= Core_WindowMessage;
                isHandlingEvents = false;
            }

            foreach (var hud in Huds)
                hud.Dispose();

            Huds.Clear();
        }
    }
}
