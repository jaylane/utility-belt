using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
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

                view = new VirindiViewService.HudView(properties, controls);
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        public ACImage GetIcon() {
            ACImage acImage = null;

            try {
                using (Stream manifestResourceStream = typeof(MainView).Assembly.GetManifestResourceStream("UtilityBelt.icons.utilitybelt.png")) {
                    if (manifestResourceStream != null) {
                        using (Bitmap bitmap = new Bitmap(manifestResourceStream))
                            acImage = new ACImage(bitmap);
                    }
                }
            }
            catch (Exception ex) { Util.LogException(ex); }
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
