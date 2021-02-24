using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Networking.Messages {
    [ProtoContract]
    class ClientInfoMessage {
        [ProtoMember(1)]
        public int Id { get; set; }

        [ProtoMember(2)]
        public string CharacterName { get; set; }

        [ProtoMember(3)]
        public string WorldName { get; set; }

        [ProtoMember(4)]
        public List<string> Tags { get; set; }

        [ProtoMember(5)]
        public bool Disconnected { get; set; }

        public ClientInfoMessage() { }

        public ClientInfoMessage(int id, string characterName, string worldName, List<string> tags, bool disconnected) {
            Id = id;
            CharacterName = characterName;
            WorldName = worldName;
            Tags = tags;
            Disconnected = disconnected;
        }
    }
}
