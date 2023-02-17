using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Networking.Messages {
    [ProtoContract]
    public class TrackedItemStatus {
        [ProtoMember(1)]
        public int Icon { get; set; }

        [ProtoMember(2)]
        public string Name { get; set; } = "";

        [ProtoMember(3)]
        public int Count { get; set; }

        public TrackedItemStatus() {

        }

        public override string ToString() {
            return Name;
        }

        public override bool Equals(object obj) {
            if (obj is null || !(obj is TrackedItemStatus s)) {
                return false;
            }
            return Icon == s.Icon && Name == s.Name && Count == s.Count;
        }
    }
}
