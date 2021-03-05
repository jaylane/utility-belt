using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Networking.Messages {
    [Serializable]
    class CastAttemptMessage {
        public int SpellId { get; set; }
        public int Target { get; set; }
        public int Skill { get; set; }

        public CastAttemptMessage() { }

        public CastAttemptMessage(int spellId, int target, int skill) {
            SpellId = spellId;
            Target = target;
            Skill = skill;
        }
    }
}
