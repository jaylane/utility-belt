using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using VirindiViewService;
using VirindiViewService.XMLParsers;

namespace UtilityBelt.Views {
    public class BaseView : IDisposable {
        #region Imports
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width { get { return Right - Left; } }
            public int Height { get { return Bottom - Top; } }
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);
        #endregion

        public VirindiViewService.HudView view;

        private ViewProperties properties;
        private ControlGroup controls;
        protected UtilityBeltPlugin UB;
        public BaseView(UtilityBeltPlugin ub) {
            UB = ub;
        }

        protected void CreateFromXMLResource(string resourcePath) {
            new Decal3XMLParser().ParseFromResource(resourcePath, out properties, out controls);

            properties.Icon = GetIcon("UtilityBelt.Resources.icons.utilitybelt.png");
            properties.Title = string.Format("UtilityBelt - v{0}", Util.GetVersion());

            view = new VirindiViewService.HudView(properties, controls);

            view.VisibleChanged += View_VisibleChanged;
        }

        private void View_VisibleChanged(object sender, EventArgs e) {
            try {
                // keep the plugin window within the game window

                RECT rect = new RECT();
                GetWindowRect(UB.Core.Decal.Hwnd, ref rect);

                if (view.Location.X + view.Width > rect.Width) {
                    view.Location = new Point(rect.Width - view.Width, view.Location.Y);
                }
                else if (view.Location.X < 0) {
                    view.Location = new Point(20, view.Location.Y);
                }

                if (view.Location.Y + view.Height > rect.Height) {
                    view.Location = new Point(view.Location.X, rect.Height - view.Height);
                }
                else if (view.Location.Y < 0) {
                    view.Location = new Point(view.Location.X, 20);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        internal virtual ACImage GetIcon() {
            return null;
        }

        protected ACImage GetIcon(string resourcePath) {
            ACImage acImage = null;

            try {
                using (Stream manifestResourceStream = typeof(MainView).Assembly.GetManifestResourceStream(resourcePath)) {
                   using (Bitmap bitmap = new Bitmap(manifestResourceStream))
                       acImage = new ACImage(bitmap);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
            return acImage;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    if (view != null) {
                        view.VisibleChanged -= View_VisibleChanged;
                        view.Dispose();
                    }
                    view = null;
                }
                disposedValue = true;
            }
        }

        public void Dispose() {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

    }
}
