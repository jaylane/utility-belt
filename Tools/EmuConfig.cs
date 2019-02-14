using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VirindiViewService.Controls;

namespace UtilityBelt.Tools {
    class EmuConfig : IDisposable {

        // this will hold a reference to our ui button for UseDeception
        HudButton UIEmuConfigUseDeception { get; set; }

        private bool disposed = false;

        public EmuConfig() {
            // Find the button in the view, or create if it doesnt exist (which is bad and shouldnt happen)
            UIEmuConfigUseDeception = Globals.View.view != null ? (HudButton)Globals.View.view["EmuConfigUseDeception"] : new HudButton();
            // listen to Hit (click) event on the button
            UIEmuConfigUseDeception.Hit += (s, e) => { ToggleConfig("UseDeception"); };
        }

        // sends a /config command to toggle a setting
        private void ToggleConfig(string setting) {
            try {
                Util.DispatchChatToBoxWithPluginIntercept(string.Format("/config {0}", setting));
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
