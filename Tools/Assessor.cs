using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Tools {
    public static class Assessor {
        private static DateTime lastThought = DateTime.MinValue;
        public static int itemsNeedingData = 0;

        public static bool NeedsInventoryData() {
            bool needsData = false;
            itemsNeedingData = 0;

            foreach (var wo in Globals.Core.WorldFilter.GetInventory()) {
                if (!wo.HasIdData) {
                    needsData = true;
                    itemsNeedingData++;
                }
            }

            return needsData;
        }

        public static int GetNeededIdCount() {
            bool needsData = false;
            itemsNeedingData = 0;

            foreach (var wo in Globals.Core.WorldFilter.GetInventory()) {
                if (!wo.HasIdData) {
                    needsData = true;
                    itemsNeedingData++;
                }
            }

            return itemsNeedingData;
        }

        public static void RequestAll() {
            itemsNeedingData = 0;

            foreach (var wo in Globals.Core.WorldFilter.GetInventory()) {
                if (!wo.HasIdData) {
                    Globals.Core.Actions.RequestId(wo.Id);

                    itemsNeedingData++;
                }
            }

            if (itemsNeedingData > 0) {
                Util.WriteToChat(String.Format("Requesting id data for {0} inventory items. This will take approximately {0} seconds.", itemsNeedingData));
            }
        }

        public static void Think() {
            if (DateTime.UtcNow - lastThought >= TimeSpan.FromMilliseconds(300)) {
                lastThought = DateTime.UtcNow;

                if (NeedsInventoryData()) {

                }
            }
        }
    }
}
