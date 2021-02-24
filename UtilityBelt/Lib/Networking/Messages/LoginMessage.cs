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

        [ProtoMember(2)]
        public string WorldName { get; set; }

        [ProtoMember(3)]
        public List<string> Tags { get; set; }

        public LoginMessage() { }

        public LoginMessage(string name, string worldName, List<string> tags) {
            Name = name;
            WorldName = worldName;
            Tags = tags;
        }
    }
}
