using Decal.Adapter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib {
    public class DoorWatcher : IDisposable {
        Dictionary<int, bool> doorStates = new Dictionary<int, bool>();

        public DoorWatcher() {
            Globals.Core.EchoFilter.ServerDispatch += EchoFilter_ServerDispatch;
        }

        public bool GetOpenStatus(int id) {
            if (doorStates.ContainsKey(id)) {
                return doorStates[id];
            }

            if (!Globals.Core.Actions.IsValidObject(id)) return false;

            var wo = Globals.Core.WorldFilter[id];

            if (wo == null) return false;

            // we didn't get a SetObjectMovement packet for this door yet so
            // use the open prop from object creation
            return wo.Values(Decal.Adapter.Wrappers.BoolValueKey.Open);
        }

        private void EchoFilter_ServerDispatch(object sender, NetworkMessageEventArgs e) {
            try {
                if (e.Message.Type == 0xF74C) { // SetObjectMovement
                    var id = e.Message.Value<int>("object");

                    // make sure the client is aware of this object
                    if (!Globals.Core.Actions.IsValidObject(id)) return;

                    var wo = Globals.Core.WorldFilter[id];

                    if (wo == null) return;
                    if (wo.ObjectClass != Decal.Adapter.Wrappers.ObjectClass.Door) return;

                    var isOpen = e.Message.Value<int>("sequence") % 2 != 0;

                    if (doorStates.ContainsKey(id)) {
                        doorStates[id] = isOpen;
                    }
                    else {
                        doorStates.Add(id, isOpen);
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
                    Globals.Core.EchoFilter.ServerDispatch -= EchoFilter_ServerDispatch;
                }

                disposedValue = true;
            }
        }
        void IDisposable.Dispose() {
            Dispose(true);
        }
        #endregion
    }
}
