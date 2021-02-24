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

        [ProtoMember(4)]
        public List<string> Tags { get; set; }

        public CommandBroadcastMessage() { }

        public CommandBroadcastMessage(string command, int delayMsBetweenClients=0, List<string> tags=null) {
            Command = command;
            DelayMsBetweenClients = delayMsBetweenClients;
            Tags = tags;
        }
    }
}
