using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Networking.Messages {
    [ProtoContract]
    class PlayerUpdateMessage {
        [ProtoMember(1)]
        public int PlayerID { get; set; }

        [ProtoMember(2)]
        public string PlayerName { get; set; }

        [ProtoMember(3)]
        public string Server { get; set; }

        [ProtoMember(4)]
        public int CurHealth { get; set; }

        [ProtoMember(5)]
        public int CurStam { get; set; }

        [ProtoMember(6)]
        public int CurMana { get; set; }

        [ProtoMember(7)]
        public int MaxHealth { get; set; }

        [ProtoMember(8)]
        public int MaxStam { get; set; }

        [ProtoMember(9)]
        public int MaxMana { get; set; }
    }
}
