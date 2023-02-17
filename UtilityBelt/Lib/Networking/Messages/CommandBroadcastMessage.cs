using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UtilityBelt.Networking;
using UtilityBelt.Networking.Lib.Packets;

namespace UtilityBelt.Lib.Networking.Messages {
    [ProtoContract]
    class CommandBroadcastRequest {
        [ProtoMember(1)]
        public string Command { get; set; }

        [ProtoMember(2)]
        public int Delay { get; set; }

        public CommandBroadcastRequest() : base() { }

        public CommandBroadcastRequest(string command, int delay = 0) {
            Command = command;
            Delay = delay;
        }
    }

    [ProtoContract]
    class CommandBroadcastResponse {
        [ProtoMember(1)]
        public string Command { get; set; }

        [ProtoMember(2)]
        public int Delay { get; set; }

        [ProtoMember(3)]
        public bool Success { get; set; }

        public CommandBroadcastResponse() : base() { }

        public CommandBroadcastResponse(bool success, string command, int delay) {
            Success = success;
            Command = command;
            Delay = delay;
        }
    }
}
