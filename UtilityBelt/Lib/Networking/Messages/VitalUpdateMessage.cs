using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UtilityBelt.Networking;
using UtilityBelt.Service;

namespace UtilityBelt.Lib.Networking.Messages {
    [ProtoContract]
    public class VitalUpdateMessage {
        [ProtoMember(2)]
        public int CurrentHealth { get; set; }

        [ProtoMember(3)]
        public int CurrentMana { get; set; }

        [ProtoMember(4)]
        public int CurrentStamina { get; set; }

        [ProtoMember(5)]
        public int MaxHealth { get; set; }

        [ProtoMember(6)]
        public int MaxMana { get; set; }

        [ProtoMember(7)]
        public int MaxStamina { get; set; }

        public VitalUpdateMessage() { }

        public override bool Equals(object obj) {
            if (obj is null || !(obj is VitalUpdateMessage m)) { return false; }
            return CurrentHealth == m.CurrentHealth && CurrentMana == m.CurrentMana && CurrentStamina == m.CurrentStamina && MaxHealth == m.MaxHealth && MaxMana == m.MaxMana && MaxStamina == m.MaxStamina ;
        }
    }
}
