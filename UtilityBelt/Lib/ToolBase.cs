using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using UtilityBelt.Lib.Settings;

namespace UtilityBelt.Lib {
    public class ToolBase : SectionBase {
        protected Dictionary<string, object> propValues = new Dictionary<string, object>();
        protected UtilityBeltPlugin UB;

        public ToolBase(UtilityBeltPlugin ub, string name) : base(null) {
            UB = ub;
            Name = name;
        }

        protected void LogDebug(string message) {
            Logger.Debug(Name + ": " + message);
        }

        protected void LogError(string message) {
            Logger.Error(Name + ": " + message);
        }

        protected void ChatThink(string message) {
            Util.Think(Name + ": " + message);
        }

        protected void WriteToChat(string message, int color=5) {
            Util.WriteToChat(Name + ": " + message, color);
        }

        #region IDisposable Support
        protected bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    // TODO: dispose managed state (managed objects).
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
