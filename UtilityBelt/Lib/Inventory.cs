using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib {
    public static class Inventory {
        #region public static int CountByName(string name)
        public static int CountByName(string name) {
            var count = 0;
            var inventory = new List<int>();

            UBHelper.InventoryManager.GetInventory(ref inventory, UBHelper.InventoryManager.GetInventoryType.Everything);
            foreach (var item in inventory) {
                var weenie = new UBHelper.Weenie(item);
                if (weenie.Name.ToLower().Equals(name.ToLower()))
                    count += weenie.StackCount;
            }

            return count;
        }
        #endregion
        #region public static bool AutoCram()
        /// <summary>
        /// Crams all backpack items into sidepacks
        /// </summary>
        /// <returns>true if it did something</returns>
        public static bool AutoCram(Lib.ActionQueue.JobCallback jobCallback) {
            bool didSomething = false;
            List<int> itemsToCram = new List<int>();
            UBHelper.InventoryManager.GetInventory(ref itemsToCram, UBHelper.InventoryManager.GetInventoryType.MainPack);

            List<int> packs = new List<int>();
            UBHelper.InventoryManager.GetInventory(ref packs, UBHelper.InventoryManager.GetInventoryType.Containers);
            List<UBHelper.Weenie> cramPacks = new List<UBHelper.Weenie>();
            foreach (int i in packs) cramPacks.Add(new UBHelper.Weenie(i));
            ActionQueue.Item q = new ActionQueue.Item(jobCallback);
            foreach (int i in itemsToCram) {
                UBHelper.Weenie w = new UBHelper.Weenie(i);
                UBHelper.Weenie cramPack = null;
                foreach (UBHelper.Weenie j in cramPacks) {
                    if (j.FreeSpace > 0) {
                        cramPack = j;
                        j.ItemsContained++;
                        break;
                    }
                }
                if (cramPack == null) break;
                q.Queue(w.Id);
                w.MoveTo(cramPack.Id, 0);
                didSomething = true;
            }
            if (!didSomething) q.Dispose();
            return didSomething;
        }
        #endregion
        #region public static bool AutoStack()
        /// <summary>
        /// Stacks all items in your backpack and sidepacks into as few stacks as possible
        /// </summary>
        /// <returns>true if it did something</returns>
        public static bool AutoStack(Lib.ActionQueue.JobCallback jobCallback) {
            //generate list of inventory types
            Dictionary<int, List<UBHelper.Weenie>> invTypes = new Dictionary<int, List<UBHelper.Weenie>>();
            List<int> inv = new List<int>();
            UBHelper.InventoryManager.GetInventory(ref inv, UBHelper.InventoryManager.GetInventoryType.AllItems);
            foreach (int weenie in inv) {
                UBHelper.Weenie w = new UBHelper.Weenie(weenie);
                if (
                    ((w.Bools & UBHelper.Weenie.Bitfield.BF_REQUIRES_PACKSLOT) != 0) || // skip containers
                    (w.WielderID != 0) || // skip wielded
                    (w.Location != 0) || // skip equipped
                    (w.StackMax == w.StackCount) || // skip non-stackable and full stacks
                    (w.TradeState) || // item is in trade
                    (w.SellState) || // item is in vendor
                    (UBHelper.Core.prevRequestObjectID == weenie) // another inventory request is already in progress on this item
                    ) continue;
                if (!invTypes.ContainsKey(w.WCID)) invTypes.Add(w.WCID, new List<UBHelper.Weenie>());
                invTypes[w.WCID].Add(w);
            }
            bool didSomething = false;
            ActionQueue.Item q = new ActionQueue.Item(jobCallback);
            foreach (KeyValuePair<int, List<UBHelper.Weenie>> i in invTypes) {
                while (i.Value.Count > 1) {
                    UBHelper.Weenie target = i.Value[0];
                    UBHelper.Weenie source = i.Value[1];
                    int stackMax = target.StackMax;
                    int targetStackCount = i.Value[0].StackCount;
                    int sourceStackCount = i.Value[1].StackCount;
                    int amountToMerge = sourceStackCount;
                    if (amountToMerge == (stackMax - targetStackCount)) { // exactly enough room, remove both
                        i.Value.RemoveAt(1);
                        i.Value.RemoveAt(0);
                    }
                    else if (amountToMerge > (stackMax - targetStackCount)) { // not enough room for whole stack, remove target, dec source
                        amountToMerge = stackMax - targetStackCount;
                        i.Value.RemoveAt(0);
                        i.Value[0].StackCount -= amountToMerge;
                    }
                    else { //deplete source, inc target
                        i.Value.RemoveAt(1);
                        i.Value[0].StackCount += amountToMerge;
                    }
                    didSomething = true;
                    q.Queue(source.Id);
                    source.StackTo(target, amountToMerge);
                }
            }
            if (!didSomething) q.Dispose();
            return didSomething;
        }
        #endregion
        #region public static bool UnStack(int splitItem = 0, int splitCount = 102)
        /// <summary>
        /// Unstacks splitItem or the currently selected item in your inventory if splitItem is 0
        /// </summary>
        /// <param name="splitItem">the item id to split, if 0 it will use currently selected item</param>
        /// <param name="splitCount">how many splits to do</param>
        /// <returns>true if it did anything</returns>
        public static bool UnStack(Lib.ActionQueue.JobCallback jobCallback, int splitItem = 0, int splitCount = 102) {
            Random rnd = new Random();
            if (splitItem == 0) splitItem = UBHelper.Core.selectedID;
            if (splitItem == 0) {
                Logger.WriteToChat("Unstack: no object specified or selected!");
                return false;
            }

            UBHelper.Weenie w = new UBHelper.Weenie(splitItem);
            if (w.TradeState) {
                Logger.WriteToChat("Unstack: Item is being traded!");
                return false;
            }
            if (w.StackCount == 0) {
                Logger.WriteToChat("Unstack: item not stackable!");
                return false;
            }


            int freeslots = new UBHelper.Weenie(UBHelper.Core.playerID).FreeSpace;
            if (freeslots < splitCount) {
                Logger.WriteToChat($"Limiting split to {freeslots}");
                splitCount = freeslots;
            }
            int containerID = UBHelper.Core.playerID;
            int amountLeft = w.StackCount;
            bool didSomething = false;
            ActionQueue.Item q = new ActionQueue.Item(jobCallback);
            for (int i = 0; i < splitCount; i++) {
                int amountToSplit = rnd.Next(1, 2);
                if (amountToSplit > amountLeft) break;
                amountLeft -= amountToSplit;
                didSomething = true;
                q.Queue(w.Id);
                w.Split(containerID, 0, amountToSplit);
            }
            if (!didSomething) q.Dispose();
            return didSomething;
        }
        #endregion
    }
}
