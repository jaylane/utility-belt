using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Networking.Messages {
    [ProtoContract]
    class CastAttemptMessage {
        [ProtoMember(1)]
        public int SpellId { get; set; }

        [ProtoMember(2)]
        public int Target { get; set; }

        [ProtoMember(3)]
        public int Skill { get; set; }

        public CastAttemptMessage() { }

        public CastAttemptMessage(int spellId, int target, int skill) {
            SpellId = spellId;
            Target = target;
            Skill = skill;
        }
    }
}
