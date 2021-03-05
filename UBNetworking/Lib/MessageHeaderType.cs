using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UBNetworking.Lib {
    public enum MessageHeaderType : int {
        KeepAlive = 1,
        Serialized = 2,
        RemoteClientConnected = 3,
        RemoteClientDisconnected = 4,
        ClientInit = 5,
    }
}
