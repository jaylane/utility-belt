using Decal.Adapter.Wrappers;
using Decal.Interop.Core;
using Decal.Interop.Render;
using System.Drawing;
using System.Runtime.InteropServices;

namespace UBLoader {
    internal static class VersionWatermark {
        public static string text;
        public static int color = Color.FromArgb(0xA0, 0xFF, 0xFF, 0xFF).ToArgb();
        private static tagRECT hudregion = new tagRECT() { left = 2, top = 2, right = 402, bottom = 402 };
        private static RenderService render = null;
        public static HUDView hud = null;
        public static void Display(NetServiceHost Host, string VersionString) {
            text = VersionString;
            if (render == null) {
                render = (RenderService)Host.Decal.GetObject("services\\DecalRender.RenderService");
                render.DeviceLost += render_DeviceLost;
            }
            render_DeviceLost();
        }
        public static void Destroy() {
            if (hud != null) {
                render.RemoveHUD(hud);
                Marshal.ReleaseComObject(hud);
                hud = null;
            }
            if (render != null) {
                render.DeviceLost -= render_DeviceLost;
                render = null;
            }
        }

        private static void render_DeviceLost() {
            try {
                if (hud != null) {
                    render.RemoveHUD(hud);
                    Marshal.ReleaseComObject(hud);
                }
                hud = render.CreateHUD(ref hudregion);
                Draw();
                hud.Enabled = true;
            }
            catch { }
        }

        public static void Draw() {
            hud.BeginRender(false);
            hud.BeginText("Terminal", 10, 0, false);
            hud.WriteText(ref hudregion, color, 0, text);
            hud.EndText();
            hud.EndRender();
        }
    }
}
