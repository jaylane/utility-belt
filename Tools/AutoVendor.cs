using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Decal.Adapter.Wrappers;

namespace UtilityBelt.Tools {
    class AutoVendor : IDisposable {
        private const int MAX_VENDOR_BUY_COUNT = 5000;
        private const double PYREAL_STACK_SIZE = 25000.0;
        private const int MAX_SELL_COUNT = 5;
        private DateTime firstThought = DateTime.MinValue;
        private DateTime lastThought = DateTime.MinValue;
        private DateTime startedVendoring = DateTime.MinValue;

        private readonly TimeSpan thinkInterval = TimeSpan.FromMilliseconds(300);
        private bool disposed;
        private int AutoVendorTimeout = 60; // in seconds

        private bool needsVendoring = false;
        private object lootProfile;
        private bool needsToBuy = false;
        private bool needsToSell = false;
        private int stackItem = 0;
        private bool shouldStack = false;

        public AutoVendor() {
            try {
                Directory.CreateDirectory(Util.GetPluginDirectory() + @"autovendor\");

                if (lootProfile == null) {
                    lootProfile = new VTClassic.LootCore();
                }

                Globals.Core.WorldFilter.ApproachVendor += WorldFilter_ApproachVendor;
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        private void WorldFilter_ApproachVendor(object sender, ApproachVendorEventArgs e) {
            try {
                Util.WriteToChat("ApproachVendor: " + e.MerchantId);

                if (!Globals.Config.AutoVendor.Enabled.Value || !Globals.Core.Actions.IsValidObject(e.MerchantId)) {
                    Stop();
                    return;
                }

                if (needsVendoring) return;

                var merchant = Globals.Core.WorldFilter[e.MerchantId];
                var profilePath = GetMerchantProfilePath(merchant);

                if (!File.Exists(profilePath)) {
                    Util.WriteToChat("No vendor profile exists: " + profilePath);
                    return;
                }

                Util.WriteToChat(string.Format("rates buy: {0} sell: {1}", e.Vendor.BuyRate, e.Vendor.SellRate));

                // Load our loot profile
                ((VTClassic.LootCore)lootProfile).LoadProfile(profilePath, false);

                if (Globals.Config.AutoVendor.TestMode.Value == true) {
                    DoTestMode();
                }
                else {
                    needsVendoring = true;
                    needsToBuy = false;
                    needsToSell = false;
                    startedVendoring = DateTime.UtcNow;

                    Globals.Core.WorldFilter.CreateObject += WorldFilter_CreateObject;
                }
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        private void WorldFilter_CreateObject(object sender, CreateObjectEventArgs e) {
            try {
                if (shouldStack && e.New.Values(LongValueKey.StackMax, 1) > 1) {
                    // TODO: multipass stacking
                    stackItem = e.New.Id;

                    lastThought = DateTime.UtcNow;
                }
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        private string GetMerchantProfilePath(WorldObject merchant) {
            // TODO: support more than utl?
            return Util.GetPluginDirectory() + @"autovendor\" + merchant.Name + ".utl";
        }

        public void Stop() {
            Util.WriteToChat("AutoVendor:Stop");

            needsVendoring = false;
            needsToBuy = false;
            needsToSell = false;

            Globals.Core.WorldFilter.CreateObject += WorldFilter_CreateObject;

            if (lootProfile != null) ((VTClassic.LootCore)lootProfile).UnloadProfile();
        }

        public void Think() {
            try {
                if (Globals.Config.AutoVendor.Enabled.Value == false) return;

                if (DateTime.UtcNow - lastThought >= thinkInterval && DateTime.UtcNow - startedVendoring >= thinkInterval) {
                    lastThought = DateTime.UtcNow;

                    if (stackItem != 0) {
                        Util.StackItem(Globals.Core.WorldFilter[stackItem]);
                        stackItem = 0;
                        return;
                    }

                    if (needsVendoring == true && HasVendorOpen()) {
                        if (DateTime.UtcNow - startedVendoring > TimeSpan.FromSeconds(AutoVendorTimeout)) {
                            Util.WriteToChat(string.Format("AutoVendor timed out after {0} seconds.", AutoVendorTimeout));
                            Stop();
                            return;
                        }

                        if (needsToBuy) {
                            needsToBuy = false;
                            shouldStack = true;
                            Globals.Core.Actions.VendorBuyAll();
                        }
                        else if (needsToSell) {
                            needsToSell = false;
                            shouldStack = false;
                            Globals.Core.Actions.VendorSellAll();
                        }
                        else {
                            DoVendoring();
                        }
                    }
                    // vendor closed?
                    else if (needsVendoring == true) {
                        Stop();
                    }
                }
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        private void DoVendoring() {
            try {
                int amount;
                WorldObject nextBuyItem = GetNextBuyItem(out amount);
                List<WorldObject> sellItems = GetSellItems();

                Util.WriteToChat("AutoVendor:DoVendoring");

                if (!HasVendorOpen()) {
                    Stop();
                    return;
                }

                Globals.Core.Actions.VendorClearBuyList();
                Globals.Core.Actions.VendorClearSellList();

                if (nextBuyItem != null && CanBuy(nextBuyItem)) {
                    int buyCount = 1;

                    // TODO check stack size of incoming item to make sure we have enough space...

                    while (buyCount < amount && GetVendorSellPrice(nextBuyItem) * (buyCount + 1) <= Util.PyrealCount()) {
                        ++buyCount;
                    }

                    Util.WriteToChat(string.Format("Buying {0} {1}", buyCount, nextBuyItem.Name));

                    Globals.Core.Actions.VendorAddBuyList(nextBuyItem.Id, buyCount);
                    needsToBuy = true;
                    return;
                }

                int totalSellValue = 0;
                int sellItemCount = 0;

                while (sellItemCount < MAX_SELL_COUNT && sellItemCount < sellItems.Count) {
                    var item = sellItems[sellItemCount];
                    var value = GetVendorBuyPrice(item);
                    var stackSize = item.Values(LongValueKey.StackCount, 1);
                    var stackCount = 0;

                    // dont sell notes if we are trying to buy notes...
                    if (((nextBuyItem != null && nextBuyItem.ObjectClass == ObjectClass.TradeNote) || nextBuyItem == null) && item.ObjectClass == ObjectClass.TradeNote) {
                        break;
                    }

                    // if we are selling notes to buy something, sell the minimum amount...
                    if (nextBuyItem != null && !CanBuy(nextBuyItem) && item.ObjectClass == ObjectClass.TradeNote) {
                        if (!PyrealsWillFitInMainPack(GetVendorBuyPrice(item))) {
                            Util.WriteToChat("No inventory room to sell... " + item.Name);
                            Stop();
                            return;
                        }

                        foreach (var wo in Globals.Core.WorldFilter.GetInventory()) {
                            if (wo.Name == item.Name && wo.Values(LongValueKey.StackCount, 0) == 1) {
                                Util.WriteToChat("Selling single " + wo.Name + " so we can afford to buy: " + nextBuyItem.Name);
                                Globals.Core.Actions.VendorAddSellList(wo.Id);
                                needsToSell = true;
                                return;
                            }
                        }

                        Globals.Core.Actions.SelectItem(item.Id);
                        Globals.Core.Actions.SelectedStackCount = 1;
                        Globals.Core.Actions.MoveItem(item.Id, Globals.Core.CharacterFilter.Id, 0, false);

                        Util.WriteToChat(string.Format("Splitting {0}. old: {1} new: {2}", item.Name, item.Values(LongValueKey.StackCount), 1));

                        shouldStack = false;

                        return;
                    }

                    if (item.Values(LongValueKey.StackMax, 0) > 1) {
                        while (stackCount <= stackSize) {
                            if (!PyrealsWillFitInMainPack(totalSellValue + (value * stackCount))) {
                                if (item.Values(LongValueKey.StackCount, 1) > 1) {
                                    Globals.Core.Actions.SelectItem(item.Id);
                                    Globals.Core.Actions.SelectedStackCount = stackCount;
                                    Globals.Core.Actions.MoveItem(item.Id, Globals.Core.CharacterFilter.Id, 0, false);
                                    Util.WriteToChat(string.Format("Splitting {0}. old: {1} new: {2}", item.Name, item.Values(LongValueKey.StackCount), stackCount));

                                    shouldStack = false;

                                    return;
                                }
                            }

                            ++stackCount;
                        }
                    }
                    else {
                        if (!PyrealsWillFitInMainPack(totalSellValue + value)) {
                            needsToSell = true;
                            return;
                        }
                    }

                    Util.WriteToChat(string.Format("Adding {0} to sell list", item.Name));
                    Globals.Core.Actions.VendorAddSellList(item.Id);

                    totalSellValue += value * stackCount;
                    ++sellItemCount;
                }

                if (sellItemCount > 0) {
                    needsToSell = true;
                    return;
                }

                Util.Think("AutoVendor finished.");
                Util.WriteToChat("AutoVendor finished.");
                Stop();
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        private int GetVendorSellPrice(WorldObject wo) {
            var price = 0;

            using (Vendor vendor = Globals.Core.WorldFilter.OpenVendor) {

                if (vendor == null) return 0;

                price = (int)Math.Ceiling((wo.Values(LongValueKey.Value, 0) / wo.Values(LongValueKey.StackCount, 1)) * vendor.SellRate);

            }
            return price;
        }

        private int GetVendorBuyPrice(WorldObject wo) {
            var price = 0;

            using (Vendor vendor = Globals.Core.WorldFilter.OpenVendor) {

                if (vendor == null) return 0;

                var buyRate = vendor.BuyRate;

                // tradenotes are always sold to vendor at full price?
                if (wo.ObjectClass == ObjectClass.TradeNote) buyRate = 1;

                price = (int)Math.Floor((wo.Values(LongValueKey.Value, 0) / wo.Values(LongValueKey.StackCount, 1)) * buyRate);

            }

            return price;
        }

        private bool CanBuy(WorldObject nextBuyItem) {
            if (nextBuyItem == null) return false;

            return Util.PyrealCount() >= GetVendorSellPrice(nextBuyItem);
        }

        private bool PyrealsWillFitInMainPack(int amount) {
            int packSlotsNeeded = (int)Math.Ceiling(amount / PYREAL_STACK_SIZE);

            Util.WriteToChat(string.Format("space needed: {0} have: {1} (amount:{2})", packSlotsNeeded, Util.GetFreeMainPackSpace(), amount));

            return Util.GetFreeMainPackSpace() >= packSlotsNeeded;
        }

        private WorldObject GetNextBuyItem(out int amount) {
            using (Vendor openVendor = Globals.Core.WorldFilter.OpenVendor) {

                amount = 0;

                if (openVendor == null || openVendor.MerchantId == 0) {
                    Util.WriteToChat("Bad Merchant");
                    return null;
                }

                // keepUpTo rules first, just like mag-tools
                foreach (WorldObject wo in openVendor) {
                    uTank2.LootPlugins.GameItemInfo itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithVendorObjectTemplateID(wo.Id);

                    if (itemInfo == null) continue;

                    uTank2.LootPlugins.LootAction result = ((VTClassic.LootCore)lootProfile).GetLootDecision(itemInfo);

                    if (!result.IsKeepUpTo) continue;

                    amount = result.Data1 - Util.GetItemCountInInventoryByName(wo.Name);

                    if (amount > wo.Values(LongValueKey.StackMax, 1)) {
                        amount = wo.Values(LongValueKey.StackMax, 1);
                    }

                    if (amount > MAX_VENDOR_BUY_COUNT) amount = MAX_VENDOR_BUY_COUNT;

                    if (amount > 0) {
                        return wo;
                    }
                }

                // keep rules next
                foreach (WorldObject wo in openVendor) {
                    uTank2.LootPlugins.GameItemInfo itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithVendorObjectTemplateID(wo.Id);

                    if (itemInfo == null) continue;

                    uTank2.LootPlugins.LootAction result = ((VTClassic.LootCore)lootProfile).GetLootDecision(itemInfo);

                    if (!result.IsKeep) continue;

                    amount = MAX_VENDOR_BUY_COUNT;

                    return wo;
                }
            }

            return null;
        }
        private List<WorldObject> GetSellItems() {
            List<WorldObject> sellObjects = new List<WorldObject>();
            using (Vendor vendor = Globals.Core.WorldFilter.OpenVendor) {

                foreach (WorldObject wo in Globals.Core.WorldFilter.GetInventory()) {
                    if (!Util.ItemIsSafeToGetRidOf(wo)) continue;

                    if (wo.Values(LongValueKey.Value, 0) <= 0) continue;

                    uTank2.LootPlugins.GameItemInfo itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithID(wo.Id);

                    if (itemInfo == null) continue;

                    uTank2.LootPlugins.LootAction result = ((VTClassic.LootCore)lootProfile).GetLootDecision(itemInfo);

                    if (!result.IsSell)
                        continue;

                    sellObjects.Add(wo);
                }

                sellObjects.Sort(
                    delegate (WorldObject wo1, WorldObject wo2) {
                    // tradenotes last
                    if (wo1.ObjectClass == ObjectClass.TradeNote && wo2.ObjectClass != ObjectClass.TradeNote) return 1;

                    // then cheapest first
                    if (wo1.Values(LongValueKey.Value, 0) > wo2.Values(LongValueKey.Value, 0)) return 1;
                        if (wo1.Values(LongValueKey.Value, 0) < wo2.Values(LongValueKey.Value, 0)) return -1;

                    // then smallest stack size
                    if (wo1.Values(LongValueKey.StackCount, 1) > wo2.Values(LongValueKey.StackCount, 1)) return 1;
                        if (wo1.Values(LongValueKey.StackCount, 1) < wo2.Values(LongValueKey.StackCount, 1)) return -1;

                        return 0;
                    }
                );
            }
            return sellObjects;
        }

        private void DoTestMode() {
            Util.WriteToChat("Buy Items:");

            using (Vendor openVendor = Globals.Core.WorldFilter.OpenVendor) {
                foreach (WorldObject vendorObj in openVendor) {
                    uTank2.LootPlugins.GameItemInfo itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithVendorObjectTemplateID(vendorObj.Id);

                    if (itemInfo == null) continue;
                    
                    uTank2.LootPlugins.LootAction result = ((VTClassic.LootCore)lootProfile).GetLootDecision(itemInfo);

                    if (result.IsKeepUpTo && result.Data1 - Util.GetItemCountInInventoryByName(vendorObj.Name) > 0) {
                        Util.WriteToChat(string.Format("  {0} * {1}", vendorObj.Name, result.Data1 - Util.GetItemCountInInventoryByName(vendorObj.Name)));
                    }
                    else if (result.IsKeep) {
                        Util.WriteToChat("  " + vendorObj.Name);
                    }
                }
            }

            Util.WriteToChat("Sell Items:");

            foreach (WorldObject wo in GetSellItems()) {
                uTank2.LootPlugins.GameItemInfo itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithID(wo.Id);

                if (itemInfo == null) continue;
                
                uTank2.LootPlugins.LootAction result = ((VTClassic.LootCore)lootProfile).GetLootDecision(itemInfo);

                if (result.IsSell) {
                    Util.WriteToChat("  " + wo.Name);
                }
            }
        }

        private bool HasVendorOpen() {
            bool hasVendorOpen = false;

            try {
                if (Globals.Core.Actions.VendorId == 0) return false;

                hasVendorOpen = true;
            }
            catch (Exception ex) { Util.LogException(ex); }

            return hasVendorOpen;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    Globals.Core.WorldFilter.ApproachVendor -= WorldFilter_ApproachVendor;
                }
                disposed = true;
            }
        }
    }
}
