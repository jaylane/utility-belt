using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.VendorCache {
    public struct BuyItem {
        public VendorItem Item;
        public int Amount;

        public BuyItem(VendorItem item, int amount) {
            Item = item;
            Amount = amount;
        }
    }
}
