using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using UtilityBelt.Lib;
using VirindiViewService;
using VirindiViewService.XMLParsers;

namespace UtilityBelt.Views {
    public class MainView : IDisposable {
        public readonly VirindiViewService.HudView view;

        private ViewProperties properties;
        private ControlGroup controls;

        private bool disposed;

        public MainView() {
            try {
                new Decal3XMLParser().ParseFromResource("UtilityBelt.Views.MainView.xml", out properties, out controls);

                properties.Icon = GetIcon();
                properties.Title = string.Format("{0} - v{1}", Globals.PluginName, Util.GetVersion());

                view = new VirindiViewService.HudView(properties, controls);

                view.Location = new Point(
                    Globals.Settings.Main.WindowPositionX,
                    Globals.Settings.Main.WindowPositionY
                );

                var timer = new Timer();
                timer.Interval = 2000; // save the window position 2 seconds after it has stopped moving
                timer.Tick += (s, e) => {
                    timer.Stop();
                    Globals.Settings.Main.WindowPositionX = view.Location.X;
                    Globals.Settings.Main.WindowPositionY = view.Location.Y;
                };

                view.Moved += (s, e) => {
                    if (timer.Enabled) timer.Stop();
                    timer.Start();
                };
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public ACImage GetIcon() {
            ACImage acImage = null;

            try {
                using (Stream manifestResourceStream = typeof(MainView).Assembly.GetManifestResourceStream("UtilityBelt.Resources.icons.utilitybelt.png")) {
                    if (manifestResourceStream != null) {
                        using (Bitmap bitmap = new Bitmap(manifestResourceStream))
                            acImage = new ACImage(bitmap);
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
            return acImage;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    if (view != null) view.Dispose();
                }
                disposed = true;
            }
        }
    }
}
