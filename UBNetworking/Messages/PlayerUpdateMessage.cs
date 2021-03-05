using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UBNetworking.Messages {
    [Serializable]
    public class PlayerUpdateMessage {
        public int CurHealth { get; set; }
        public int CurStam { get; set; }
        public int CurMana { get; set; }
        public int MaxHealth { get; set; }
        public int MaxStam { get; set; }
        public int MaxMana { get; set; }
        public int PlayerId { get; set; }

        public PlayerUpdateMessage() { }
    }
}
