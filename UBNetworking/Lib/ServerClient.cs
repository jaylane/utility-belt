using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using UBNetworking.Messages;

namespace UBNetworking.Lib {
    public class ServerClient : ClientBase {
        public ServerClient(int clientId, string connectionId, TcpClient client, Action<string> log, Action<Action> runOnMainThread) : base(clientId, connectionId, log, runOnMainThread, null) {
            SetClient(client);
            IsRemote = true;
        }

        protected override void Dispose(bool isDisposing) {
            base.Dispose(isDisposing);
        }
    }
}
