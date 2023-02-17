using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Networking.Messages {
    [ProtoContract]
    public class CharacterPositionMessage {
        [ProtoMember(1)]
        public int LandCell { get; set; }

        [ProtoMember(2)]
        public double Heading { get; set; }

        [ProtoMember(3)]
        public double NS { get; set; }

        [ProtoMember(4)]
        public double EW { get; set; }

        [ProtoMember(5)]
        public double Z { get; set; }

        public CharacterPositionMessage() {

        }

        public override bool Equals(object obj) {
            if (obj is null || !(obj is CharacterPositionMessage other))
                return false;
            return LandCell == other.LandCell && Heading == other.Heading && NS == other.NS && EW == other.EW && Z == other.Z;
        }
    }
}
