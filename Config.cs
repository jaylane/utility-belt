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
            public Setting<bool> ShowMerchantInfo;
            public Setting<bool> Think;
            public Setting<bool> Debug;
            public Setting<int> Speed;
            public Setting<int> MaxSellCount;

            private bool disposed = false;

            public AutoVendorConfig() {
                try {
                    Enabled = new Setting<bool>("Config/AutoVendor/Enabled", "Enable AutoVendor", false);
                    TestMode = new Setting<bool>("Config/AutoVendor/TestMode", "Enable TestMode", false);
                    ShowMerchantInfo = new Setting<bool>("Config/AutoVendor/ShowMerchantInfo", "Show merchant info on approach", true);
                    Think = new Setting<bool>("Config/AutoVendor/Think", "Think to yourself when finished", true);
                    Debug = new Setting<bool>("Config/AutoVendor/Debug", "Show debug messages", false);
                    Speed = new Setting<int>("Config/AutoVendor/Speed", "Delay between autovendor actions", 500);
                    MaxSellCount = new Setting<int>("Config/AutoVendor/MaxSellCount", "Maximum number of items to sell at once", 5);
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
                        if (ShowMerchantInfo != null) ShowMerchantInfo.Dispose();
                        if (Think != null) Think.Dispose();
                        if (Speed != null) Speed.Dispose();
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
