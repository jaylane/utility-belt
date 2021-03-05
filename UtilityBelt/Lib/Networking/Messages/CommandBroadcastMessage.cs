using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Networking.Messages {
    [Serializable]
    class CommandBroadcastMessage {
        public string Command { get; set; }
        public int Delay { get; set; }

        public CommandBroadcastMessage() { }

        public CommandBroadcastMessage(string command, int delay=0) {
            Command = command;
            Delay = delay;
        }
    }
}
