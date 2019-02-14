using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VirindiViewService.Controls;

namespace UtilityBelt.Tools {
    class EmuConfig : IDisposable {
        HudButton UIEmuConfigUseDeception { get; set; }

        private bool disposed = false;

        public EmuConfig() {
            UIEmuConfigUseDeception = Globals.View.view != null ? (HudButton)Globals.View.view["EmuConfigUseDeception"] : new HudButton();
            UIEmuConfigUseDeception.Hit += (s, e) => { ToggleConfig("UseDeception"); };
        }

        private void ToggleConfig(string v) {
            try {
                Util.DispatchChatToBoxWithPluginIntercept(string.Format("/config {0}", v));
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                }
                disposed = true;
            }
        }
    }
}
