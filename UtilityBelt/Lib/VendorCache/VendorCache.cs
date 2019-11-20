using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.VendorCache {
    public static class VendorCache {
        public static Dictionary<int, VendorInfo> Vendors = new Dictionary<int, VendorInfo>();

        public static void AddVendor(Vendor vendor) {
            if (Vendors.ContainsKey(vendor.MerchantId)) {
                Vendors[vendor.MerchantId] = new VendorInfo(vendor);
            }
            else {
                Vendors.Add(vendor.MerchantId, new VendorInfo(vendor));
            }
        }

        public static VendorInfo GetVendor(int vendorId) {
            if (Vendors.ContainsKey(vendorId)) {
                return Vendors[vendorId];
            }

            return null;
        }
    }
}
