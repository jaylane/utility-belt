using Decal.Adapter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UtilityBelt.Lib;

namespace UtilityBelt.Tools {
    [Name("ChatNameClickHandler")]
    public class ChatNameClickHandler : ToolBase {
        public ChatNameClickHandler(UtilityBeltPlugin ub, string name) : base(ub, name) {
            CoreManager.Current.ChatNameClicked += Core_ChatNameClicked;
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
                        case "select":
                            int.TryParse(args, out int id);
                            if (id != 0 && CoreManager.Current.WorldFilter[id] != null) {
                                CoreManager.Current.Actions.SelectItem(id);
                            }
                            break;

                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        #region IDisposable Support
        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    CoreManager.Current.ChatNameClicked -= Core_ChatNameClicked;
                    base.Dispose(disposing);
                }
                disposedValue = true;
            }
        }
        #endregion
    }
}
