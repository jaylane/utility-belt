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

        public AutoSalvageConfig AutoSalvage;
        public AutoVendorConfig AutoVendor;
        public InventoryManagerConfig InventoryManager;

        public class InventoryManagerConfig : IDisposable {
            public Setting<bool> AutoCram;
            public Setting<bool> AutoStack;
            public Setting<bool> Debug;

            private bool disposed = false;

            public InventoryManagerConfig() {
                try {
                    AutoCram = new Setting<bool>("Config/InventoryManager/AutoCram", "Automatically cram items into side packs", false);
                    AutoStack = new Setting<bool>("Config/InventoryManager/AutoStack", "Automatically combine stacked items", false);
                    Debug = new Setting<bool>("Config/InventoryManager/Debug", "Show debug messages", false);
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
                        if (AutoCram != null) Debug.Dispose();
                        if (AutoStack != null) Debug.Dispose();
                        if (Debug != null) Debug.Dispose();
                    }
                    disposed = true;
                }
            }
        }

        public class AutoSalvageConfig : IDisposable {
            public Setting<bool> Think;
            public Setting<bool> Debug;

            private bool disposed = false;

            public AutoSalvageConfig() {
                try {
                    Think = new Setting<bool>("Config/AutoSalvage/Think", "Think to yourself when finished", true);
                    Debug = new Setting<bool>("Config/AutoSalvage/Debug", "Show debug messages", false);
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
                        if (Think != null) Think.Dispose();
                        if (Debug != null) Debug.Dispose();
                    }
                    disposed = true;
                }
            }
        }

        public class AutoVendorConfig : IDisposable {
            public Setting<bool> Enabled;
            public Setting<bool> TestMode;
            public Setting<bool> ShowMerchantInfo;
            public Setting<bool> Think;
            public Setting<bool> Debug;
            public Setting<int> Speed;

            private bool disposed = false;

            public AutoVendorConfig() {
                try {
                    Enabled = new Setting<bool>("Config/AutoVendor/Enabled", "Enable AutoVendor", false);
                    TestMode = new Setting<bool>("Config/AutoVendor/TestMode", "Enable TestMode", false);
                    ShowMerchantInfo = new Setting<bool>("Config/AutoVendor/ShowMerchantInfo", "Show merchant info on approach", true);
                    Think = new Setting<bool>("Config/AutoVendor/Think", "Think to yourself when finished", true);
                    Debug = new Setting<bool>("Config/AutoVendor/Debug", "Show debug messages", false);
                    Speed = new Setting<int>("Config/AutoVendor/Speed", "Delay between autovendor actions", 500);
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
                        if (Debug != null) Debug.Dispose();
                    }
                    disposed = true;
                }
            }
        }

        public Config() {
            AutoSalvage = new AutoSalvageConfig();
            AutoVendor = new AutoVendorConfig();
            InventoryManager = new InventoryManagerConfig();
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    if (AutoSalvage != null) AutoSalvage.Dispose();
                    if (AutoVendor != null) AutoVendor.Dispose();
                    if (InventoryManager != null) InventoryManager.Dispose();
                }
                disposed = true;
            }
        }
    }
}
