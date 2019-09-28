using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Decal.Adapter.Wrappers;

namespace UtilityBelt.Tools {
    public class Assessor : IDisposable {
        private bool disposed = false;
        private static DateTime lastThought = DateTime.MinValue;
        private static DateTime lastIdentRecieved = DateTime.UtcNow;
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
            ObjectClass.Plant,
            ObjectClass.Lockpick,
            ObjectClass.ManaStone,
            ObjectClass.HealingKit,
            ObjectClass.Ust,
            ObjectClass.Book,
            ObjectClass.CraftedAlchemy,
            ObjectClass.CraftedCooking,
            ObjectClass.CraftedFletching,
            ObjectClass.Misc,
            ObjectClass.Key
        };

        public Assessor() : base() {
            Globals.Core.WorldFilter.ChangeObject += WorldFilter_ChangeObject;
        }

        private void WorldFilter_ChangeObject(object sender, ChangeObjectEventArgs e) {
            try {
                if (e.Change == WorldChangeType.IdentReceived) {
                    lastIdentRecieved = DateTime.UtcNow;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public bool NeedsInventoryData() {
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

        public bool NeedsInventoryData(List<int> items) {
            bool needsData = false;
            itemsNeedingData = 0;

            foreach (var id in items) {
                var wo = Globals.Core.WorldFilter[id];
                if (wo != null && !wo.HasIdData && ItemNeedsIdData(wo)) {
                    needsData = true;
                    itemsNeedingData++;
                }
            }

            return needsData;
        }

        private bool ItemNeedsIdData(WorldObject wo) {
            if (SkippableObjectClasses.Contains(wo.ObjectClass)) return false;

            return true;
        }

        public int GetNeededIdCount() {
            itemsNeedingData = 0;

            foreach (var wo in Globals.Core.WorldFilter.GetInventory()) {
                if (!wo.HasIdData && ItemNeedsIdData(wo)) {
                    itemsNeedingData++;
                }
            }

            return itemsNeedingData;
        }

        public int GetNeededIdCount(List<int> items) {
            itemsNeedingData = 0;
            foreach (var id in items) {
                var wo = Globals.Core.WorldFilter[id];
                if (wo != null && !wo.HasIdData && ItemNeedsIdData(wo)) {
                    itemsNeedingData++;
                }
            }

            return itemsNeedingData;
        }

        internal void RequestAll(List<int> items) {
            itemsNeedingData = 0;

            foreach (var id in items) {
                var wo = Globals.Core.WorldFilter[id];

                if (wo != null && !wo.HasIdData && ItemNeedsIdData(wo)) {
                    Globals.Core.Actions.RequestId(wo.Id);

                    itemsNeedingData++;
                }
            }

            if (itemsNeedingData > 0) {
                Util.WriteToChat(String.Format("Requesting id data for {0} inventory items. This will take approximately {0} seconds.", itemsNeedingData));
            }
        }

        public void RequestAll() {
            var ids = new List<int>();

            foreach (var wo in Globals.Core.WorldFilter.GetInventory()) {
                ids.Add(wo.Id);
            }

            RequestAll(ids);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    Globals.Core.WorldFilter.ChangeObject -= WorldFilter_ChangeObject;
                }
                disposed = true;
            }
        }
    }
}
