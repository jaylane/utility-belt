using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UBNetworking.Messages {
    [Serializable]
    public class LoginMessage {
        public string Name { get; set; }
        public string WorldName { get; set; }
        public List<string> Tags { get; set; }

        public LoginMessage() { }

        public LoginMessage(string name, string worldName, List<string> tags) {
            Name = name;
            WorldName = worldName;
            Tags = tags;
        }
    }
}
