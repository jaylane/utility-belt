using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Networking.Messages {
    [ProtoContract]
    class CastSuccessMessage {
        [ProtoMember(1)]
        public int SpellId { get; set; }

        [ProtoMember(2)]
        public int Target { get; set; }

        [ProtoMember(3)]
        public int Duration { get; set; }

        public CastSuccessMessage() { }

        public CastSuccessMessage(int spellId, int target, int duration) {
            SpellId = spellId;
            Target = target;
            Duration = duration;
        }
    }
}
