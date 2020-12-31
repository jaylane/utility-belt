using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Networking.Messages {
    [ProtoContract]
    class CommandBroadcastMessage {
        [ProtoMember(1)]
        public string Command { get; set; }

        [ProtoMember(2)]
        public int DelayMsBetweenClients { get; set; }

        [ProtoMember(3)]
        public int ClientIndex { get; set; }

        public CommandBroadcastMessage() { }

        public CommandBroadcastMessage(string command, int delayMsBetweenClients=0) {
            Command = command;
            DelayMsBetweenClients = delayMsBetweenClients;
        }
    }
}
