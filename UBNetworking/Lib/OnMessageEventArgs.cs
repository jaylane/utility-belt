using System;

namespace UBNetworking.Lib {
    public class OnMessageEventArgs : EventArgs {
        public MessageHeader Header { get; }
        public byte[] Body { get; }

        public OnMessageEventArgs(MessageHeader header, byte[] body) {
            Header = header;
            Body = body;
        }
    }
}