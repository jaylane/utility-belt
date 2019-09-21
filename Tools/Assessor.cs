using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Decal.Adapter.Wrappers;

namespace UtilityBelt.Tools {
    public static class Assessor {
        private static DateTime lastThought = DateTime.MinValue;
        public static int itemsNeedingData = 0;

        private static List<ObjectClass> SkippableObjectClasses = new List<ObjectClass>() {
            ObjectClass.Money,
            ObjectClass.TradeNote,
            ObjectClass.Salvage,
            ObjectClass.Scroll,
            ObjectClass.SpellComponent,
            ObjectClass.Container,
            ObjectClass.Foci,
            ObjectClass.Food,
            ObjectClass.Plant
        };

        public static bool NeedsInventoryData() {
            bool needsData = false;
            itemsNeedingData = 0;

            foreach (var wo in Globals.Core.WorldFilter.GetInventory()) {
                if (!wo.HasIdData && ItemNeedsIdData(wo)) {
                    needsData = true;
                    itemsNeedingData++;
                }
            }

            return needsData;
        }

        private static bool ItemNeedsIdData(WorldObject wo) {
            if (SkippableObjectClasses.Contains(wo.ObjectClass)) return false;

            return true;
        }

        public static int GetNeededIdCount() {
            itemsNeedingData = 0;

            foreach (var wo in Globals.Core.WorldFilter.GetInventory()) {
                if (!wo.HasIdData) {
                    itemsNeedingData++;
                }
            }

            return itemsNeedingData;
        }

        public static void RequestAll() {
            itemsNeedingData = 0;

            foreach (var wo in Globals.Core.WorldFilter.GetInventory()) {
                if (!wo.HasIdData && ItemNeedsIdData(wo)) {
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
