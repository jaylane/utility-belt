using MetaViewWrappers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using UtilityBelt.Lib;
using VirindiViewService;
using VirindiViewService.XMLParsers;

namespace UtilityBelt.Views {
    public class MapView : IDisposable {
        public readonly VirindiViewService.HudView view;

        private ViewProperties properties;
        private ControlGroup controls;

        private bool disposed;

        public MapView() {
            try {
                new Decal3XMLParser().ParseFromResource("UtilityBelt.Views.MapView.xml", out properties, out controls);
                properties.Icon = GetIcon();

                view = new VirindiViewService.HudView(properties, controls);

                view.UserResizeable = true;

                view.Location = new Point(
                    Globals.Config.DungeonMaps.MapWindowX.Value,
                    Globals.Config.DungeonMaps.MapWindowY.Value
                );
                view.Width = Globals.Config.DungeonMaps.MapWindowWidth.Value;
                view.Height = Globals.Config.DungeonMaps.MapWindowHeight.Value;

                var timer = new Timer();
                timer.Interval = 2000; // save the window position 2 seconds after it has stopped moving
                timer.Tick += (s, e) => {
                    timer.Stop();
                    Globals.Config.DungeonMaps.MapWindowX.Value = view.Location.X;
                    Globals.Config.DungeonMaps.MapWindowY.Value = view.Location.Y;
                    Globals.Config.DungeonMaps.MapWindowWidth.Value = view.Width;
                    Globals.Config.DungeonMaps.MapWindowHeight.Value = view.Height;
                };

                view.Moved += (s, e) => {
                    if (timer.Enabled) timer.Stop();
                    timer.Start();
                };

                view.Resize += (s, e) => {
                    if (timer.Enabled) timer.Stop();
                    timer.Start();
                };
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public ACImage GetIcon() {
            ACImage acImage = null;

            try {
                using (Stream manifestResourceStream = typeof(MainView).Assembly.GetManifestResourceStream("UtilityBelt.Resources.icons.dungeonmaps.png")) {
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
