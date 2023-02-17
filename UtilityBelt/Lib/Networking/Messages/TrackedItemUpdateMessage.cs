using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Networking.Messages {
    [ProtoContract]
    public class TrackedItemUpdateMessage {
        [ProtoMember(1)]
        public List<TrackedItemStatus> TrackedItems { get; set; }

        public TrackedItemUpdateMessage() {
        
        }

        public override bool Equals(object obj) {
            if (obj is null || !(obj is TrackedItemUpdateMessage inst)) {
                return false;
            }
            return TrackedItems.Select(i => inst.TrackedItems.Where((t) => t.Equals(i)).Any()).All(c => c);
        }
    }
}
