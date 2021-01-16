using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UtilityBelt.Lib.Actions;

namespace UtilityBelt.Lib.GameEvents {
    public class UBGameEvent {
        public BaseGameEvent.GameEventType GameEventType { get; set; }
        public BaseAction Action { get; set; }

        public UBGameEvent() {

        }

        public UBGameEvent(BaseGameEvent.GameEventType gameEventType, BaseAction action) {
            GameEventType = gameEventType;
            Action = action;
        }
    }
}
