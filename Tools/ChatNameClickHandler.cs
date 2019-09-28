using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Tools {
    class ChatNameClickHandler : IDisposable {
        public ChatNameClickHandler() {
            Globals.Core.ChatNameClicked += Core_ChatNameClicked;
        }

        private void Core_ChatNameClicked(object sender, Decal.Adapter.ChatClickInterceptEventArgs e) {
            try {
                if (e.Id == Util.GetChatId()) {
                    e.Eat = true;
                    var parts = e.Text.Split('|');
                    var command = parts[0];
                    var args = parts[1];

                    switch (command) {
                        case "openurl":
                            System.Diagnostics.Process.Start(args);
                            break;
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    Globals.Core.ChatNameClicked -= Core_ChatNameClicked;
                }
                disposedValue = true;
            }
        }

        public void Dispose() {
            Dispose(true);
        }
        #endregion
    }
}
