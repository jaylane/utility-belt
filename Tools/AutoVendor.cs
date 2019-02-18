using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Decal.Adapter.Wrappers;
using Mag.Shared.Settings;
using UtilityBelt.Views;
using VirindiViewService.Controls;

namespace UtilityBelt.Tools {
    public struct BuyItem {
        public VendorItem Item;
        public int Amount;

        public BuyItem(VendorItem item, int amount) {
            Item = item;
            Amount = amount;
        }
    }

    public class VendorItem {
        public ObjectClass ObjectClass = ObjectClass.Unknown;
        public int Id = 0;
        public int Value = 0;
        public string Name = "Unknown";
        public int StackMax = 0;
        public int StackCount = 0;

        public VendorItem(WorldObject item) {
            Id = item.Id;
            Value = item.Values(LongValueKey.Value, 0);
            Name = item.Name;
            StackMax = item.Values(LongValueKey.StackMax, 1);
            StackCount = item.Values(LongValueKey.StackCount, 1);
            ObjectClass = item.ObjectClass;
        }
    }

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
    }

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


    class AutoVendor : IDisposable {
        private const int MAX_VENDOR_BUY_COUNT = 5000;
        private const double PYREAL_STACK_SIZE = 25000.0;
        private DateTime firstThought = DateTime.MinValue;
        private DateTime lastThought = DateTime.MinValue;
        private DateTime startedVendoring = DateTime.MinValue;
        
        private bool disposed;
        private int AutoVendorTimeout = 60; // in seconds

        private bool needsVendoring = false;
        private object lootProfile;
        private bool needsToBuy = false;
        private bool needsToSell = false;
        private int stackItem = 0;
        private bool shouldStack = false;
        private int vendorId = 0;
        private string vendorName = "";
        private bool waitingForIds = false;
        private DateTime lastIdSpam = DateTime.MinValue;
        private bool needsToUse = false;

        HudCheckBox UIAutoVendorEnable { get; set; }
        HudCheckBox UIAutoVendorTestMode { get; set; }
        HudCheckBox UIAutoVendorDebug { get; set; }
        HudCheckBox UIAutoVendorShowMerchantInfo { get; set; }
        HudCheckBox UIAutoVendorThink { get; set; }
        HudHSlider UIAutoVendorSpeed { get; set; }
        HudStaticText UIAutoVendorSpeedText { get; set; }

        public AutoVendor() {
            try {
                Directory.CreateDirectory(Util.GetPluginDirectory() + @"autovendor\");
                Directory.CreateDirectory(Util.GetCharacterDirectory() + @"autovendor\");

                UIAutoVendorSpeedText = Globals.View.view != null ? (HudStaticText)Globals.View.view["AutoVendorSpeedText"] : new HudStaticText();
                UIAutoVendorSpeedText.Text = Globals.Config.AutoVendor.Speed.Value.ToString();

                UIAutoVendorEnable = Globals.View.view != null ? (HudCheckBox)Globals.View.view["AutoVendorEnabled"] : new HudCheckBox();
                UIAutoVendorEnable.Checked = Globals.Config.AutoVendor.Enabled.Value;
                UIAutoVendorEnable.Change += UIAutoVendorEnable_Change;
                Globals.Config.AutoVendor.Enabled.Changed += Config_AutoVendor_Enabled_Changed;

                UIAutoVendorTestMode = Globals.View.view != null ? (HudCheckBox)Globals.View.view["AutoVendorTestMode"] : new HudCheckBox();
                UIAutoVendorTestMode.Checked = Globals.Config.AutoVendor.TestMode.Value;
                UIAutoVendorTestMode.Change += UIAutoVendorTestMode_Change;
                Globals.Config.AutoVendor.TestMode.Changed += Config_AutoVendor_TestMode_Changed;

                UIAutoVendorDebug = Globals.View.view != null ? (HudCheckBox)Globals.View.view["AutoVendorDebug"] : new HudCheckBox();
                UIAutoVendorDebug.Checked = Globals.Config.AutoVendor.Debug.Value;
                UIAutoVendorDebug.Change += UIAutoVendorDebug_Change;
                Globals.Config.AutoVendor.Debug.Changed += Config_AutoVendor_Debug_Changed;

                UIAutoVendorShowMerchantInfo = Globals.View.view != null ? (HudCheckBox)Globals.View.view["AutoVendorShowMerchantInfo"] : new HudCheckBox();
                UIAutoVendorShowMerchantInfo.Checked = Globals.Config.AutoVendor.ShowMerchantInfo.Value;
                UIAutoVendorShowMerchantInfo.Change += UIAutoVendorShowMerchantInfo_Change;
                Globals.Config.AutoVendor.ShowMerchantInfo.Changed += Config_AutoVendor_ShowMerchantInfo_Changed;

                UIAutoVendorThink = Globals.View.view != null ? (HudCheckBox)Globals.View.view["AutoVendorThink"] : new HudCheckBox();
                UIAutoVendorThink.Checked = Globals.Config.AutoVendor.Think.Value;
                UIAutoVendorThink.Change += UIAutoVendorThink_Change;
                Globals.Config.AutoVendor.Think.Changed += Config_AutoVendor_Think_Changed;

                UIAutoVendorSpeed = Globals.View.view != null ? (HudHSlider)Globals.View.view["AutoVendorSpeed"] : new HudHSlider();
                UIAutoVendorSpeed.Position = (Globals.Config.AutoVendor.Speed.Value / 100) - 3;
                UIAutoVendorSpeed.Changed += UIAutoVendorSpeed_Changed;
                Globals.Config.AutoVendor.Speed.Changed += Config_AutoVendor_Speed_Changed;

                if (lootProfile == null) {
                    lootProfile = new VTClassic.LootCore();
                }

                Globals.Core.WorldFilter.ApproachVendor += WorldFilter_ApproachVendor;
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        private void UIAutoVendorEnable_Change(object sender, EventArgs e) {
            Globals.Config.AutoVendor.Enabled.Value = UIAutoVendorEnable.Checked;
        }

        private void Config_AutoVendor_Enabled_Changed(Setting<bool> obj) {
            UIAutoVendorEnable.Checked = Globals.Config.AutoVendor.Enabled.Value;
        }

        private void UIAutoVendorTestMode_Change(object sender, EventArgs e) {
            Globals.Config.AutoVendor.TestMode.Value = UIAutoVendorTestMode.Checked;
        }

        private void Config_AutoVendor_TestMode_Changed(Setting<bool> obj) {
            UIAutoVendorTestMode.Checked = Globals.Config.AutoVendor.TestMode.Value;
        }

        private void UIAutoVendorDebug_Change(object sender, EventArgs e) {
            Globals.Config.AutoVendor.Debug.Value = UIAutoVendorDebug.Checked;
        }

        private void Config_AutoVendor_Debug_Changed(Setting<bool> obj) {
            UIAutoVendorDebug.Checked = Globals.Config.AutoVendor.Debug.Value;
        }

        private void UIAutoVendorShowMerchantInfo_Change(object sender, EventArgs e) {
            Globals.Config.AutoVendor.ShowMerchantInfo.Value = UIAutoVendorShowMerchantInfo.Checked;
        }

        private void Config_AutoVendor_ShowMerchantInfo_Changed(Setting<bool> obj) {
            UIAutoVendorShowMerchantInfo.Checked = Globals.Config.AutoVendor.ShowMerchantInfo.Value;
        }

        private void UIAutoVendorThink_Change(object sender, EventArgs e) {
            Globals.Config.AutoVendor.Think.Value = UIAutoVendorThink.Checked;
        }

        private void Config_AutoVendor_Think_Changed(Setting<bool> obj) {
            UIAutoVendorThink.Checked = Globals.Config.AutoVendor.Think.Value;
        }

        private void UIAutoVendorSpeed_Changed(int min, int max, int pos) {
            var v = (pos * 100) + 300;
            if (v != Globals.Config.AutoVendor.Speed.Value) {
                Globals.Config.AutoVendor.Speed.Value = v;
                UIAutoVendorSpeedText.Text = v.ToString();
            }
        }

        private void Config_AutoVendor_Speed_Changed(Setting<int> obj) {
            //UIAutoVendorSpeed.Position = (Globals.Config.AutoVendor.Speed.Value / 100) - 300;
        }

        private void WorldFilter_ApproachVendor(object sender, ApproachVendorEventArgs e) {
            try {
                if (Globals.Config.AutoVendor.Enabled.Value == false) {
                    return;
                }

                if (Globals.Core.Actions.IsValidObject(e.MerchantId) == false) {
                    Stop();
                    return;
                }

                if (needsVendoring && vendorId == e.Vendor.MerchantId) return;

                VendorCache.AddVendor(e.Vendor);

                var merchant = Globals.Core.WorldFilter[e.MerchantId];
                var profilePath = GetMerchantProfilePath(merchant);

                vendorId = merchant.Id;
                vendorName = merchant.Name;

                if (!File.Exists(profilePath)) {
                    Util.WriteToChat("No vendor profile exists: " + profilePath);
                    return;
                }

                if (Globals.Config.AutoVendor.ShowMerchantInfo.Value == true) {
                    Util.WriteToChat(merchant.Name);
                    Util.WriteToChat(string.Format("BuyRate: {0}% SellRate: {1}% MaxValue: {2:n0}", e.Vendor.BuyRate*100, e.Vendor.SellRate*100, e.Vendor.MaxValue));
                }

                // Load our loot profile
                ((VTClassic.LootCore)lootProfile).LoadProfile(profilePath, false);

                /*
                if (Assessor.NeedsInventoryData()) {
                    Assessor.RequestAll();
                    waitingForIds = true;
                    lastIdSpam = DateTime.UtcNow;
                }
                */

                Globals.InventoryManager.Pause();
                
                needsVendoring = true;
                needsToBuy = false;
                needsToSell = false;
                shouldStack = true;
                startedVendoring = DateTime.UtcNow;

                Globals.Core.WorldFilter.CreateObject += WorldFilter_CreateObject;
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        private void WorldFilter_CreateObject(object sender, CreateObjectEventArgs e) {
            try {
                if (!Globals.Config.AutoVendor.Enabled.Value || !needsVendoring) return;
                
                //if (shouldStack && e.New.Values(LongValueKey.StackMax, 1) > 1) {
                //    lastThought = DateTime.UtcNow;
                //}
            }
            catch (Exception ex) { Util.LogException(ex); }
        }
        
        private string GetMerchantProfilePath(WorldObject merchant) {
            // TODO: support more than utl?
            if (File.Exists(Util.GetCharacterDirectory() + @"autovendor\" + merchant.Name + ".utl")) {
                return Util.GetCharacterDirectory() + @"autovendor\" + merchant.Name + ".utl";
            }
            else if (File.Exists(Util.GetPluginDirectory() + @"autovendor\" + merchant.Name + ".utl")) {
                return Util.GetPluginDirectory() + @"autovendor\" + merchant.Name + ".utl";
            }
            else if (File.Exists(Util.GetCharacterDirectory() + @"autovendor\default.utl")) {
                return Util.GetCharacterDirectory() + @"autovendor\default.utl";
            }
            else if (File.Exists(Util.GetPluginDirectory() + @"autovendor\default.utl")) {
                return Util.GetPluginDirectory() + @"autovendor\default.utl";
            }

            return Util.GetPluginDirectory() + @"autovendor\" + merchant.Name + ".utl";
        }

        public void Stop() {
            if (Globals.Config.AutoVendor.Think.Value == true) {
                Util.Think("AutoVendor finished: " + vendorName);
            }
            else {
                Util.WriteToChat("AutoVendor finished: " + vendorName);
            }

            Globals.InventoryManager.Resume();

            needsVendoring = false;
            needsToBuy = false;
            needsToSell = false;
            vendorName = "";
            vendorId = 0;

            Globals.Core.WorldFilter.CreateObject += WorldFilter_CreateObject;

            if (lootProfile != null) ((VTClassic.LootCore)lootProfile).UnloadProfile();
        }

        public void Think() {
            try {
                var thinkInterval = TimeSpan.FromMilliseconds(Globals.Config.AutoVendor.Speed.Value);

                if (Globals.Config.AutoVendor.Enabled.Value == false) return;

                if (DateTime.UtcNow - lastThought >= thinkInterval && DateTime.UtcNow - startedVendoring >= thinkInterval) {
                    lastThought = DateTime.UtcNow;
                    
                    if (needsVendoring && waitingForIds) {
                        if (Assessor.NeedsInventoryData()) {
                            if (DateTime.UtcNow - lastIdSpam > TimeSpan.FromSeconds(10)) {
                                lastIdSpam = DateTime.UtcNow;
                                startedVendoring = DateTime.UtcNow;

                                if (Globals.Config.AutoVendor.Debug.Value == true) {
                                    Util.WriteToChat(string.Format("AutoVendor Waiting to id {0} items, this will take approximately {0} seconds.", Assessor.GetNeededIdCount()));
                                }
                            }

                            // waiting
                            return;
                        }
                        else {
                            waitingForIds = false;
                        }
                    }

                    if (needsVendoring && Globals.Config.AutoVendor.TestMode.Value == true) {
                        DoTestMode();
                        Stop();
                        return;
                    }


                    if (needsVendoring && shouldStack && Globals.Config.InventoryManager.AutoStack.Value == true) {
                        if (Globals.InventoryManager.AutoStack() == true) return;
                    }
                    shouldStack = false;

                    List<int> cramExcludeList = GetSellItems().Select((x) => { return x.Id; }).ToList();
                    if (needsVendoring && Globals.Config.InventoryManager.AutoCram.Value == true) {
                        if (Globals.InventoryManager.AutoCram(cramExcludeList, true) == true) return;
                    }

                    //if (needsToUse) {
                    //    if (Globals.Core.Actions.VendorId != 0) {
                    //        Globals.Core.Actions.UseItem(Globals.Core.Actions.VendorId, 0);
                    //    }

                    //    needsToUse = false;
                    //    return;
                    //}

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
                            needsToUse = true;
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
                List<BuyItem> buyItems = GetBuyItems();
                List<WorldObject> sellItems = GetSellItems();

                if (!HasVendorOpen()) {
                    if (Globals.Config.AutoVendor.Debug.Value == true) {
                        Util.WriteToChat("AutoVendor vendor was closed, stopping!");
                    }
                    Stop();
                    return;
                }

                if (Globals.Config.AutoVendor.Debug.Value == true) {
                    Util.WriteToChat("AutoVendor:DoVendoring");
                }

                if (Globals.Config.AutoVendor.Debug.Value == true) {
                    Util.WriteToChat(string.Format("AutoVendor Wants Buy: {0}", buyItems.Count > 0 ? buyItems[0].Item.Name : "null"));
                    Util.WriteToChat(string.Format("AutoVendor Wants Sell: {0}", sellItems.Count > 0 ? Util.GetObjectName(sellItems[0].Id) : "null"));
                }

                Globals.Core.Actions.VendorClearBuyList();
                Globals.Core.Actions.VendorClearSellList();

                var totalBuyCount = 0;
                var totalBuyPyreals = 0;
                foreach (var buyItem in buyItems) {
                    if (totalBuyPyreals + GetVendorSellPrice(buyItem.Item) <= Util.PyrealCount()) {
                        int buyCount = 1;

                        // TODO check stack size of incoming item to make sure we have enough space...

                        while (buyCount < buyItem.Amount && totalBuyPyreals + (GetVendorSellPrice(buyItem.Item) * (buyCount + 1)) <= Util.PyrealCount()) {
                            ++buyCount;
                        }

                        Globals.Core.Actions.VendorAddBuyList(buyItem.Item.Id, buyCount);
                        totalBuyPyreals += GetVendorSellPrice(buyItem.Item) * buyCount;
                        totalBuyCount++;

                        if (Globals.Config.AutoVendor.Debug.Value == true) {
                            Util.WriteToChat(string.Format("AutoVendor Buying {0} {1} - {2}/{3}", buyCount, buyItem.Item.Name, totalBuyPyreals, Util.PyrealCount()));
                        }
                    }
                    else if (totalBuyCount > 0) {
                        needsToBuy = true;
                        return;
                    }
                }

                if (totalBuyCount > 0) {
                    needsToBuy = true;
                    return;
                }

                VendorItem nextBuyItem = null;

                if (buyItems.Count > 0) {
                    nextBuyItem = buyItems[0].Item;
                }

                int totalSellValue = 0;
                int sellItemCount = 0;

                while (sellItemCount < sellItems.Count && sellItemCount < Util.GetFreeMainPackSpace()) {
                    var item = sellItems[sellItemCount];
                    var value = GetVendorBuyPrice(item);
                    var stackSize = item.Values(LongValueKey.StackCount, 1);
                    var stackCount = 0;

                    // dont sell notes if we are trying to buy notes...
                    if (((nextBuyItem != null && nextBuyItem.ObjectClass == ObjectClass.TradeNote) || nextBuyItem == null) && item.ObjectClass == ObjectClass.TradeNote) {

                        if (Globals.Config.AutoVendor.Debug.Value == true) {
                            Util.WriteToChat(string.Format("AutoVendor bail: buyItem: {0} sellItem: {1}", nextBuyItem == null ? "null" : nextBuyItem.Name, Util.GetObjectName(item.Id)));
                        }
                        break;
                    }

                    // if we are selling notes to buy something, sell the minimum amount
                    if (nextBuyItem != null && item.ObjectClass == ObjectClass.TradeNote) {
                        if (!PyrealsWillFitInMainPack(GetVendorBuyPrice(item))) {
                            Util.WriteToChat("AutoVendor No inventory room to sell... " + Util.GetObjectName(item.Id));
                            Stop();
                            return;
                        }

                        // see if we already have a single stack of this item
                        foreach (var wo in Globals.Core.WorldFilter.GetInventory()) {
                            if (wo.Name == item.Name && wo.Values(LongValueKey.StackCount, 0) == 1) {
                                if (Globals.Config.AutoVendor.Debug.Value == true) {
                                    Util.WriteToChat("AutoVendor Selling single " + wo.Name + " so we can afford to buy: " + nextBuyItem.Name);
                                }
                                Globals.Core.Actions.VendorAddSellList(wo.Id);
                                needsToSell = true;
                                return;
                            }
                        }

                        Globals.Core.Actions.SelectItem(item.Id);
                        Globals.Core.Actions.SelectedStackCount = 1;
                        Globals.Core.Actions.MoveItem(item.Id, Globals.Core.CharacterFilter.Id, 0, false);

                        if (Globals.Config.AutoVendor.Debug.Value == true) {
                            Util.WriteToChat(string.Format("AutoVendor Splitting {0}. old: {1} new: {2}", Util.GetObjectName(item.Id), item.Values(LongValueKey.StackCount), 1));
                        }

                        shouldStack = false;

                        return;
                    }

                    if (item.Values(LongValueKey.StackMax, 0) > 1 && item.Values(LongValueKey.StackCount, 1) > 1) {
                        while (stackCount <= stackSize) {
                            if (!PyrealsWillFitInMainPack(totalSellValue + (value * stackCount))) {
                                if (sellItemCount > 0) {
                                    break;
                                }

                                if (item.Values(LongValueKey.StackCount, 1) > 1) {
                                    Globals.Core.Actions.SelectItem(item.Id);
                                    Globals.Core.Actions.SelectedStackCount = stackCount;
                                    Globals.Core.Actions.MoveItem(item.Id, Globals.Core.CharacterFilter.Id, 0, false);
                                    if (Globals.Config.AutoVendor.Debug.Value == true) {
                                        Util.WriteToChat(string.Format("AutoVendor Splitting {0}. old: {1} new: {2}", Util.GetObjectName(item.Id), item.Values(LongValueKey.StackCount), stackCount));
                                    }

                                    shouldStack = false;

                                    return;
                                }
                            }

                            ++stackCount;
                        }
                    }
                    else {
                        stackCount = 1;
                    }

                    if (!PyrealsWillFitInMainPack(totalSellValue + (value * stackCount))) {
                        break;
                    }

                    if (Globals.Config.AutoVendor.Debug.Value == true) {
                        Util.WriteToChat(string.Format("AutoVendor Adding {0} to sell list", Util.GetObjectName(item.Id)));
                    }

                    Globals.Core.Actions.VendorAddSellList(item.Id);

                    totalSellValue += value * stackCount;
                    ++sellItemCount;
                }

                if (sellItemCount > 0) {
                    needsToSell = true;
                    return;
                }

                Stop();
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        private int GetVendorSellPrice(VendorItem item) {
            var price = 0;
            var vendor = VendorCache.GetVendor(Globals.Core.Actions.VendorId);

            try {
                if (vendorId == 0 || vendor == null) return 0;

                // notes are always 1.15 when buying
                if (item.ObjectClass == ObjectClass.TradeNote) vendor.SellRate = 1.15;

                price = (int)Math.Ceiling((item.Value / item.StackCount) * vendor.SellRate);
            }
            catch (Exception ex) { Util.LogException(ex); }

            return price;
        }

        private int GetVendorBuyPrice(WorldObject wo) {
            var price = 0;
            var vendor = VendorCache.GetVendor(Globals.Core.Actions.VendorId);

            try {
                if (vendorId == 0 || vendor == null) return 0;

                // notes are always 1 when selling
                if (wo.ObjectClass == ObjectClass.TradeNote) vendor.BuyRate = 1;

                price = (int)Math.Floor((wo.Values(LongValueKey.Value, 0) / wo.Values(LongValueKey.StackCount, 1)) * vendor.BuyRate);
            }
            catch (Exception ex) { Util.LogException(ex); }

            return price;
        }

        private bool PyrealsWillFitInMainPack(int amount) {
            int packSlotsNeeded = (int)Math.Ceiling(amount / PYREAL_STACK_SIZE);

            return Util.GetFreeMainPackSpace() > packSlotsNeeded;
        }

        private List<BuyItem> GetBuyItems() {
            VendorInfo vendor = VendorCache.GetVendor(Globals.Core.Actions.VendorId);
            List<BuyItem> buyItems = new List<BuyItem>();

            if (vendor == null) return buyItems;


            // keepUpTo rules first, just like mag-tools
            foreach (VendorItem item in vendor.Items.Values) {
                var amount = 0;

                uTank2.LootPlugins.GameItemInfo itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithVendorObjectTemplateID(item.Id);

                if (itemInfo == null) continue;

                uTank2.LootPlugins.LootAction result = ((VTClassic.LootCore)lootProfile).GetLootDecision(itemInfo);

                if (!result.IsKeepUpTo) continue;

                amount = result.Data1 - Util.GetItemCountInInventoryByName(item.Name);

                if (amount > item.StackMax) {
                    amount = item.StackMax;
                }

                if (amount > MAX_VENDOR_BUY_COUNT) amount = MAX_VENDOR_BUY_COUNT;

                if (amount > 0) {
                    if (Globals.Config.AutoVendor.Debug.Value == true) {
                        Util.WriteToChat("AutoVendor: Buy " + item.Name);
                    }

                    buyItems.Add(new BuyItem(item, amount));
                }
            }

            // keep rules next
            foreach (VendorItem item in vendor.Items.Values) {
                uTank2.LootPlugins.GameItemInfo itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithVendorObjectTemplateID(item.Id);

                if (itemInfo == null) continue;

                uTank2.LootPlugins.LootAction result = ((VTClassic.LootCore)lootProfile).GetLootDecision(itemInfo);

                if (!result.IsKeep) continue;
                
                buyItems.Add(new BuyItem(item, MAX_VENDOR_BUY_COUNT));
            }

            return buyItems;
        }

        private List<WorldObject> GetSellItems() {
            List<WorldObject> sellObjects = new List<WorldObject>();
            var vendor = VendorCache.GetVendor(Globals.Core.Actions.VendorId);

            if (vendor == null) return sellObjects;

            foreach (WorldObject wo in Globals.Core.WorldFilter.GetInventory()) {
                if (!Util.ItemIsSafeToGetRidOf(wo) || !ItemIsSafeToGetRidOf(wo)) continue;

                if (wo.Values(LongValueKey.Value, 0) <= 0) continue;

                uTank2.LootPlugins.GameItemInfo itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithID(wo.Id);

                if (itemInfo == null) continue;

                uTank2.LootPlugins.LootAction result = ((VTClassic.LootCore)lootProfile).GetLootDecision(itemInfo);

                if (!result.IsSell)
                    continue;
                
                // too expensive for this vendor
                if (vendor.MaxValue < wo.Values(LongValueKey.Value, 0) && wo.ObjectClass != ObjectClass.TradeNote) continue;
                
                // will vendor buy this item?
                if (wo.ObjectClass != ObjectClass.TradeNote && (vendor.Categories & wo.Category) == 0) {
                    continue;
                }

                sellObjects.Add(wo);
            }

            sellObjects.Sort(delegate (WorldObject wo1, WorldObject wo2) {
                // tradenotes last
                if (wo1.ObjectClass == ObjectClass.TradeNote && wo2.ObjectClass != ObjectClass.TradeNote) return 1;
                if (wo1.ObjectClass != ObjectClass.TradeNote && wo2.ObjectClass == ObjectClass.TradeNote) return -1;

                // then cheapest first
                if (wo1.Values(LongValueKey.Value, 0) > wo2.Values(LongValueKey.Value, 0)) return 1;
                if (wo1.Values(LongValueKey.Value, 0) < wo2.Values(LongValueKey.Value, 0)) return -1;

                // then smallest stack size
                if (wo1.Values(LongValueKey.StackCount, 1) > wo2.Values(LongValueKey.StackCount, 1)) return 1;
                if (wo1.Values(LongValueKey.StackCount, 1) < wo2.Values(LongValueKey.StackCount, 1)) return -1;

                return 0;
            });

            return sellObjects;
        }

        private bool ItemIsSafeToGetRidOf(WorldObject wo) {
            if (wo == null) return false;

            // dont sell items with descriptions (quest items)
            // peas have descriptions...
            //if (wo.Values(StringValueKey.FullDescription, "").Length > 1) return false;

            // can be sold?
            //if (wo.Values(BoolValueKey.CanBeSold, false) == false) return false;

            // no attuned
            if (wo.Values(LongValueKey.Attuned, 0) > 1) return false;

            return true;
        }

        private void DoTestMode() {
            Util.WriteToChat("Buy Items:");

            foreach (BuyItem bi in GetBuyItems()) {
                uTank2.LootPlugins.GameItemInfo itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithVendorObjectTemplateID(bi.Item.Id);

                if (itemInfo == null) continue;

                uTank2.LootPlugins.LootAction result = ((VTClassic.LootCore)lootProfile).GetLootDecision(itemInfo);

                if (result.IsKeepUpTo || result.IsKeep) {
                    Util.WriteToChat(string.Format("  {0} * {1} - {2}", bi.Item.Name, bi.Amount==5000 ? "∞" : bi.Amount.ToString(), result.RuleName));
                }
            }

            Util.WriteToChat("Sell Items:");

            foreach (WorldObject wo in GetSellItems()) {
                uTank2.LootPlugins.GameItemInfo itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithID(wo.Id);

                if (itemInfo == null) continue;
                
                uTank2.LootPlugins.LootAction result = ((VTClassic.LootCore)lootProfile).GetLootDecision(itemInfo);

                if (result.IsSell) {
                    Util.WriteToChat(string.Format("  {0} - {1}", Util.GetObjectName(wo.Id), result.RuleName));
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
