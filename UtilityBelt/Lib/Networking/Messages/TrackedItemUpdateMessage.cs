using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Networking.Messages {
    [Serializable()]
    public class TrackedItemUpdateMessage {
        public List<TrackedItemStatus> TrackedItems { get; set; }

        public TrackedItemUpdateMessage() {
        
        }
    }
}
