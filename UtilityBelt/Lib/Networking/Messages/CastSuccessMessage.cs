using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Networking.Messages {
    [Serializable]
    class CastSuccessMessage {
        public int SpellId { get; set; }
        public int Target { get; set; }
        public int Duration { get; set; }

        public CastSuccessMessage() { }

        public CastSuccessMessage(int spellId, int target, int duration) {
            SpellId = spellId;
            Target = target;
            Duration = duration;
        }
    }
}
