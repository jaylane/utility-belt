using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Settings {
    public class TrackedItem {
        public int Icon { get; set; }
        public string Name { get; set; } = "";

        public TrackedItem() {

        }

        public override string ToString() {
            return Name;
        }
    }
}
