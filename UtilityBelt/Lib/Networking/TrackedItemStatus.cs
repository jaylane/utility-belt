using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Networking {
    [Serializable()]
    public class TrackedItemStatus {
        public int Icon { get; set; }
        public string Name { get; set; } = "";
        public int Count { get; set; }

        public TrackedItemStatus() {
        
        }

        public override string ToString() {
            return Name;
        }
    }
}
