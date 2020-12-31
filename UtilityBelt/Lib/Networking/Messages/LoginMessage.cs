using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Networking.Messages {
    [ProtoContract]
    class LoginMessage {
        [ProtoMember(1)]
        public string Name { get; set; }

        public LoginMessage() { }

        public LoginMessage(string name) {
            Name = name;
        }
    }
}
