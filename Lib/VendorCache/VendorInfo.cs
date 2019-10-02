using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.VendorCache {
    public class VendorInfo {
        public int Id = 0;
        public double BuyRate = 0;
        public double SellRate = 0;
        public int MaxValue = 0;
        public int Categories = 0;
        public Dictionary<int, VendorItem> Items = new Dictionary<int, VendorItem>();

        public VendorInfo(Vendor vendor) {
            Id = vendor.MerchantId;
            BuyRate = vendor.BuyRate;
            SellRate = vendor.SellRate;
            MaxValue = vendor.MaxValue;
            Categories = vendor.Categories;

            foreach (var wo in vendor) {
                Items.Add(wo.Id, new VendorItem(wo));
            }
        }

        public bool WillBuyItem(WorldObject wo) {
            // all vendors buy tradenotes, right?
            if (wo.ObjectClass == ObjectClass.TradeNote) return true;

            // will vendor buy this item?
            if (wo.ObjectClass != ObjectClass.TradeNote && (Categories & wo.Category) == 0) {
                return false;
            }

            // too expensive for this vendor
            if (MaxValue < (wo.Values(LongValueKey.Value, 0) / wo.Values(LongValueKey.StackCount, 1))) {
                return false;
            }

            return true;
        }
    }
}
