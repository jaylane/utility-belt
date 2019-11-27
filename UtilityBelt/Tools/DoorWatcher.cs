using Decal.Adapter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UtilityBelt.Lib;

namespace UtilityBelt.Tools {
    [Name("DoorWatcher")]
    public class DoorWatcher : ToolBase {
        Dictionary<int, bool> doorStates = new Dictionary<int, bool>();

        public DoorWatcher(UtilityBeltPlugin ub, string name) : base(ub, name) {
            UB.Core.EchoFilter.ServerDispatch += EchoFilter_ServerDispatch;
        }

        public bool GetOpenStatus(int id) {
            if (doorStates.ContainsKey(id)) {
                return doorStates[id];
            }

            if (!UB.Core.Actions.IsValidObject(id)) return false;

            var wo = UB.Core.WorldFilter[id];

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
                    if (!UB.Core.Actions.IsValidObject(id)) return;

                    var wo = UB.Core.WorldFilter[id];

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
        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    UB.Core.EchoFilter.ServerDispatch -= EchoFilter_ServerDispatch;
                    base.Dispose(disposing);
                }

                disposedValue = true;
            }
        }
        #endregion
    }
}
