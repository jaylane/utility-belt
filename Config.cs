using Decal.Adapter;
using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Mag.Shared.Settings;

namespace UtilityBelt {
    public class Config : IDisposable {
        private bool disposed;

        public AutoVendorConfig AutoVendor;

        public class AutoVendorConfig : IDisposable {
            public Setting<bool> Enabled;
            public Setting<bool> TestMode;
            private bool disposed = false;

            public AutoVendorConfig() {
                try {
                    Enabled = new Setting<bool>("Config/AutoVendor/Enabled", "Enable AutoVendor", false);
                    TestMode = new Setting<bool>("Config/AutoVendor/TestMode", "Enable AutoVendor TestMode", false);
                }
                catch (Exception e) { Util.LogException(e); }
            }

            public void Dispose() {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing) {
                if (!disposed) {
                    if (disposing) {
                        if (Enabled != null) Enabled.Dispose();
                        if (TestMode != null) TestMode.Dispose();
                    }
                    disposed = true;
                }
            }
        }

        public Config() {
            AutoVendor = new AutoVendorConfig();
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    if (AutoVendor != null) AutoVendor.Dispose();
                }
                disposed = true;
            }
        }
    }
}
