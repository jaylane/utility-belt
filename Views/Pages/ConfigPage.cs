using System;
using System.Globalization;
using Mag.Shared.Settings;
using VirindiViewService.Controls;

namespace UtilityBelt.Views.Pages {
    public class ConfigPage : IDisposable {
        HudCheckBox UIAutoVendorEnable { get; set; }
        HudCheckBox UIAutoVendorTestMode { get; set; }

        public ConfigPage(MainView mainView) {
            try {
                UIAutoVendorEnable = mainView.view != null ? (HudCheckBox)mainView.view["AutoVendorEnable"] : new HudCheckBox();
                UIAutoVendorEnable.Checked = Globals.Config.AutoVendor.Enabled.Value;
                UIAutoVendorEnable.Change += UIAutoVendorEnable_Change;
                Globals.Config.AutoVendor.Enabled.Changed += Config_AutoVendor_Enabled_Changed;

                UIAutoVendorTestMode = mainView.view != null ? (HudCheckBox)mainView.view["AutoVendorTestMode"] : new HudCheckBox();
                UIAutoVendorTestMode.Checked = Globals.Config.AutoVendor.TestMode.Value;
                UIAutoVendorTestMode.Change += UIAutoVendorTestMode_Change;
                Globals.Config.AutoVendor.TestMode.Changed += Config_AutoVendor_TestMode_Changed;
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        private void UIAutoVendorEnable_Change(object sender, EventArgs e) {
            Globals.Config.AutoVendor.Enabled.Value = UIAutoVendorEnable.Checked;
        }

        private void Config_AutoVendor_Enabled_Changed(Setting<bool> obj) {
            UIAutoVendorEnable.Checked = Globals.Config.AutoVendor.Enabled.Value;
        }

        private void UIAutoVendorTestMode_Change(object sender, EventArgs e) {
            Globals.Config.AutoVendor.TestMode.Value = UIAutoVendorTestMode.Checked;
        }

        private void Config_AutoVendor_TestMode_Changed(Setting<bool> obj) {
            UIAutoVendorTestMode.Checked = Globals.Config.AutoVendor.TestMode.Value;
        }

        private bool disposed;

        public void Dispose() {
            try {
                Dispose(true);

                GC.SuppressFinalize(this);
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        protected virtual void Dispose(bool disposing) {
            try {
                if (!disposed) {
                    if (disposing) {
                        if (UIAutoVendorEnable != null) UIAutoVendorEnable.Dispose();
                        if (UIAutoVendorTestMode != null) UIAutoVendorTestMode.Dispose();
                    }

                    disposed = true;
                }
            }
            catch (Exception ex) { Util.LogException(ex); }
        }
    }
}