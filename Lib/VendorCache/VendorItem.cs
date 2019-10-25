using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.VendorCache {
    public class VendorItem {
        public ObjectClass ObjectClass = ObjectClass.Unknown;
        public int Id = 0;
        public int Type = 0;
        public int Value = 0;
        public string Name = "Unknown";
        public int StackMax = 0;
        public int StackCount = 0;

        public VendorItem(WorldObject item) {
            Id = item.Id;
            Type = item.Type;
            Value = item.Values(LongValueKey.Value, 0);
            Name = item.Name;
            StackMax = item.Values(LongValueKey.StackMax, 1);
            StackCount = item.Values(LongValueKey.StackCount, 1);
            ObjectClass = item.ObjectClass;
        }
    }
}
