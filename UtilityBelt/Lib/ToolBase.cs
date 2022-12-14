using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using UtilityBelt.Service.Lib.Settings;

namespace UtilityBelt.Lib {
    public class ToolBase : ISetting {
        protected Dictionary<string, object> propValues = new Dictionary<string, object>();
        protected UtilityBeltPlugin UB;

        public ToolBase(UtilityBeltPlugin ub, string name) {
            UB = ub;
            FullName = name;
        }

        public virtual void Init() {
        
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

        protected void WriteToChat(string message, Logger.LogMessageType logMessageType=Logger.LogMessageType.Generic) {
            Logger.WriteToChat(Name + ": " + message, logMessageType);
        }

        internal virtual void RenderUI() {
        
        }

        #region IDisposable Support
        protected bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {

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
