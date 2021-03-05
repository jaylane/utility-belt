using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UBNetworking.Messages {
    [Serializable]
    public class ClientInfoMessage {
        public string CharacterName { get; set; }
        public string WorldName { get; set; }
        public List<string> Tags { get; set; }

        public ClientInfoMessage() { }

        public ClientInfoMessage(string characterName, string worldName, List<string> tags) {
            CharacterName = characterName;
            WorldName = worldName;
            Tags = tags;
        }
    }
}
