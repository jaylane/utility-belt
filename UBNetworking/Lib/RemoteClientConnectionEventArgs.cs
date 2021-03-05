using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UBNetworking.Lib {
    public class RemoteClientConnectionEventArgs : EventArgs {
        public enum ConnectionType {
            Connected,
            Disconnected
        }

        public int ClientId { get; set; }

        public ConnectionType ConnectionStatus { get; set; }

        public RemoteClientConnectionEventArgs(int clientId, ConnectionType status) {
            ClientId = clientId;
        }
    }
}
