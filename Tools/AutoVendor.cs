using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Decal.Adapter;
using Decal.Adapter.Wrappers;
using UtilityBelt.Lib.VendorCache;
using UtilityBelt.Views;
using VirindiViewService.Controls;

namespace UtilityBelt.Tools {
    public class AutoVendor : IDisposable {
        private const int MAX_VENDOR_BUY_COUNT = 5000;
        private const int PYREAL_STACK_SIZE = 25000;
        private DateTime lastThought = DateTime.MinValue;
        private DateTime startedVendoring = DateTime.MinValue;
        private DateTime lastVendorAction = DateTime.MinValue;

        private bool disposed;
        private bool waitingForVendor = true;
        private bool needsVendoring = false;
        private object lootProfile;
        private bool needsToBuy = false;
        private bool needsToSell = false;
        private bool shouldStack = false;
        private int vendorId = 0;
        private string vendorName = "";
        private bool waitingForIds = false;
        private DateTime lastIdSpam = DateTime.MinValue;
        private List<int> itemsToId = new List<int>();

        HudCheckBox UIAutoVendorEnable { get; set; }
        HudCheckBox UIAutoVendorTestMode { get; set; }
        HudCheckBox UIAutoVendorShowMerchantInfo { get; set; }
        HudCheckBox UIAutoVendorThink { get; set; }
        HudCheckBox UIAutoVendorOnlyFromMainPack { get; set; }
        HudHSlider UIAutoVendorSpeed { get; set; }
        HudStaticText UIAutoVendorSpeedText { get; set; }

        public AutoVendor() {
            try {
                Directory.CreateDirectory(Path.Combine(Util.GetPluginDirectory(), "autovendor"));
                Directory.CreateDirectory(Path.Combine(Util.GetCharacterDirectory(), "autovendor"));
                Directory.CreateDirectory(Path.Combine(Util.GetServerDirectory(), "autovendor"));

                UIAutoVendorSpeedText = Globals.MainView.view != null ? (HudStaticText)Globals.MainView.view["AutoVendorSpeedText"] : new HudStaticText();

                UIAutoVendorEnable = Globals.MainView.view != null ? (HudCheckBox)Globals.MainView.view["AutoVendorEnabled"] : new HudCheckBox();
                UIAutoVendorEnable.Change += UIAutoVendorEnable_Change;

                UIAutoVendorTestMode = Globals.MainView.view != null ? (HudCheckBox)Globals.MainView.view["AutoVendorTestMode"] : new HudCheckBox();
                UIAutoVendorTestMode.Change += UIAutoVendorTestMode_Change;

                UIAutoVendorShowMerchantInfo = Globals.MainView.view != null ? (HudCheckBox)Globals.MainView.view["AutoVendorShowMerchantInfo"] : new HudCheckBox();
                UIAutoVendorShowMerchantInfo.Change += UIAutoVendorShowMerchantInfo_Change;

                UIAutoVendorThink = Globals.MainView.view != null ? (HudCheckBox)Globals.MainView.view["AutoVendorThink"] : new HudCheckBox();
                UIAutoVendorThink.Change += UIAutoVendorThink_Change;

                UIAutoVendorOnlyFromMainPack = Globals.MainView.view != null ? (HudCheckBox)Globals.MainView.view["AutoVendorOnlyFromMainPack"] : new HudCheckBox();
                UIAutoVendorOnlyFromMainPack.Change += UIAutoVendorOnlyFromMainPack_Change;

                UIAutoVendorSpeed = Globals.MainView.view != null ? (HudHSlider)Globals.MainView.view["AutoVendorSpeed"] : new HudHSlider();
                UIAutoVendorSpeed.Changed += UIAutoVendorSpeed_Changed;

                Globals.Core.WorldFilter.ApproachVendor += WorldFilter_ApproachVendor;
                Globals.Core.CommandLineText += Current_CommandLineText;
                Globals.Core.WorldFilter.CreateObject += WorldFilter_CreateObject;

                Globals.Settings.AutoSalvage.PropertyChanged += (s, e) => { UpdateUI(); };

                UpdateUI();
            } catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UpdateUI() {
            UIAutoVendorEnable.Checked = Globals.Settings.AutoVendor.Enabled;
            UIAutoVendorSpeedText.Text = Globals.Settings.AutoVendor.Speed.ToString();
            UIAutoVendorTestMode.Checked = Globals.Settings.AutoVendor.TestMode;
            UIAutoVendorShowMerchantInfo.Checked = Globals.Settings.AutoVendor.ShowMerchantInfo;
            UIAutoVendorThink.Checked = Globals.Settings.AutoVendor.Think;
            UIAutoVendorOnlyFromMainPack.Checked = Globals.Settings.AutoVendor.OnlyFromMainPack;
            UIAutoVendorSpeed.Position = (Globals.Settings.AutoVendor.Speed / 100) - 3;
        }

        private void UIAutoVendorEnable_Change(object sender, EventArgs e) {
            Globals.Settings.AutoVendor.Enabled = UIAutoVendorEnable.Checked;
        }

        private void UIAutoVendorTestMode_Change(object sender, EventArgs e) {
            Globals.Settings.AutoVendor.TestMode = UIAutoVendorTestMode.Checked;
        }

        private void UIAutoVendorShowMerchantInfo_Change(object sender, EventArgs e) {
            Globals.Settings.AutoVendor.ShowMerchantInfo = UIAutoVendorShowMerchantInfo.Checked;
        }

        private void UIAutoVendorThink_Change(object sender, EventArgs e) {
            Globals.Settings.AutoVendor.Think = UIAutoVendorThink.Checked;
        }

        private void UIAutoVendorOnlyFromMainPack_Change(object sender, EventArgs e) {
            Globals.Settings.AutoVendor.OnlyFromMainPack = UIAutoVendorOnlyFromMainPack.Checked;
        }

        private void UIAutoVendorSpeed_Changed(int min, int max, int pos) {
            var v = (pos * 100) + 300;
            if (v != Globals.Settings.AutoVendor.Speed) {
                Globals.Settings.AutoVendor.Speed = v;
                UIAutoVendorSpeedText.Text = v.ToString();
            }
        }

        private void WorldFilter_ApproachVendor(object sender, ApproachVendorEventArgs e) {
            try {
                VendorCache.AddVendor(e.Vendor);

                if (!Globals.Core.Actions.IsValidObject(e.MerchantId)) {
                    Stop();
                    return;
                }

                if (vendorId != e.Vendor.MerchantId) {
                    if (Globals.Settings.AutoVendor.ShowMerchantInfo) {
                        Util.WriteToChat(string.Format("{0}[0x{4:X8}]: BuyRate: {1}% SellRate: {2}% MaxValue: {3:n0}",
                            Globals.Core.WorldFilter[e.Vendor.MerchantId].Name, e.Vendor.BuyRate * 100, e.Vendor.SellRate * 100, e.Vendor.MaxValue, e.Vendor.MerchantId));
                    }
                }
                
                if (Globals.Settings.AutoVendor.Enabled == false)
                    return;
                
                if (needsVendoring && vendorId == e.Vendor.MerchantId) return;
                
                if (waitingForVendor)
                    Start(e.Vendor.MerchantId);
            } catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Current_CommandLineText(object sender, ChatParserInterceptEventArgs e) {
            try {
                if (e.Text.StartsWith("/ub autovendor ")) {
                    string path = e.Text.Replace("/ub autovendor ", "").Trim();
                    e.Eat = true;

                    Start(0, path);

                    return;
                }
            } catch (Exception ex) { Logger.LogException(ex); }
        }

        private void WorldFilter_CreateObject(object sender, CreateObjectEventArgs e) {
            try {
                if (!Globals.Settings.AutoVendor.Enabled || !needsVendoring) return;

                // if pyreals are coming in, delay a little until we get them all
                if (e.New.Values(LongValueKey.Type, 0) == 273) {
                    lastThought = DateTime.UtcNow + TimeSpan.FromMilliseconds(800);
                    return;
                }

                if (shouldStack && e.New.Values(LongValueKey.StackMax, 1) > 1) {
                    lastThought = DateTime.UtcNow;
                }
            } catch (Exception ex) { Logger.LogException(ex); }
        }

        private string GetProfilePath(string profileName) {
            var charPath = Path.Combine(Util.GetCharacterDirectory(), "autovendor");
            var mainPath = Path.Combine(Util.GetPluginDirectory(), "autovendor");
            var serverPath = Path.Combine(Util.GetServerDirectory(), "autovendor");

            if (File.Exists(Path.Combine(charPath, profileName))) {
                return Path.Combine(charPath, profileName);
            } else if (File.Exists(Path.Combine(serverPath, profileName))) {
                return Path.Combine(serverPath, profileName);
            } else if (File.Exists(Path.Combine(mainPath, profileName))) {
                return Path.Combine(mainPath, profileName);
            } else if (File.Exists(Path.Combine(charPath, "default.utl"))) {
                return Path.Combine(charPath, "default.utl");
            } else if (File.Exists(Path.Combine(serverPath, "default.utl"))) {
                return Path.Combine(serverPath, "default.utl");
            } else if (File.Exists(Path.Combine(mainPath, "default.utl"))) {
                return Path.Combine(mainPath, "default.utl");
            }

            return Path.Combine(mainPath, profileName);
        }

        public void Start(int merchantId = 0, string useProfilePath = "") {
            if (Util.GetFreeMainPackSpace() < 1) {
                Util.WriteToChat("AutoVendor Fatal - insufficient pack space!");
                Stop(false);
                return;
            }

            waitingForVendor = false;
            var hasLootCore = false;
            if (lootProfile == null) {
                try {
                    lootProfile = new VTClassic.LootCore();
                    hasLootCore = true;
                } catch (Exception ex) { Logger.LogException(ex); }

                if (!hasLootCore) {
                    Util.WriteToChat("Unable to load VTClassic, something went wrong.");
                    return;
                }
            }

            if (merchantId == 0) {
                merchantId = Globals.Core.Actions.VendorId;
            }

            if (merchantId == 0 || Globals.Core.WorldFilter[merchantId] == null) {
                Util.WriteToChat("AutoVendor: no open vendor, cannot start!");
                return;
            }

            if (needsVendoring) {
                Stop(true);
            }

            var merchant = Globals.Core.WorldFilter[merchantId];
            var profilePath = GetProfilePath(string.IsNullOrEmpty(useProfilePath) ? (merchant.Name + ".utl") : useProfilePath);

            vendorId = merchant.Id;
            vendorName = merchant.Name;

            if (!File.Exists(profilePath)) {
                Util.WriteToChat("No vendor profile exists: " + profilePath);
                Stop();
                return;
            }

            VTankControl.Nav_Block(1000, false); // quick block to keep vtank from truckin' off before the profile loads, but short enough to not matter if it errors out and doesn't unlock

            // Load our loot profile
            ((VTClassic.LootCore)lootProfile).LoadProfile(profilePath, false);
            
            itemsToId.Clear();
            var inventory = Globals.Core.WorldFilter.GetInventory();

            // filter inventory beforehand if we are only selling from the main pack
            if (Globals.Settings.AutoVendor.OnlyFromMainPack == true) {
                inventory.SetFilter(new ByContainerFilter(Globals.Core.CharacterFilter.Id));
            }

            // build a list of items to id from our inventory, attempting to be smart about it
            VendorInfo vendor = VendorCache.GetVendor(vendorId);
            foreach (var item in inventory) {
                // will the vendor buy this item?

                if (vendor != null && (vendor.Categories & item.Category) == 0) continue;

                itemsToId.Add(item.Id);
            }

            if (Globals.Assessor.NeedsInventoryData(itemsToId)) {
                Globals.Assessor.RequestAll(itemsToId);
                waitingForIds = true;
                lastIdSpam = DateTime.UtcNow;
            }

            Globals.InventoryManager.Pause();

            needsVendoring = true;
            needsToBuy = false;
            needsToSell = false;
            shouldStack = true;
            startedVendoring = DateTime.UtcNow;

            // ~~disable~~ block vtank autocram/stack because it interferes with vendoring.
            VTankControl.Item_Block(30000, false);
            VTankControl.Nav_Block(30000, Globals.Settings.Plugin.Debug);

            Globals.Core.WorldFilter.CreateObject += WorldFilter_CreateObject;
        }

        public void Stop(bool silent = false) {
            // we delay for 2 seconds in case of server lag, so that we can properly
            // detect getting pyreals from the vendor transaction at the end of the process.
            if (DateTime.UtcNow - lastVendorAction < TimeSpan.FromSeconds(2)) return;


            if (!silent) {
                if (Globals.Settings.AutoVendor.Think == true) {
                    Util.Think("AutoVendor finished: " + vendorName);
                } else {
                    Util.WriteToChat("AutoVendor finished: " + vendorName);
                }
            }

            Globals.InventoryManager.Resume();

            waitingForVendor = true;
            needsVendoring = false;
            needsToBuy = false;
            needsToSell = false;
            vendorName = "";
            vendorId = 0;

            Globals.Core.WorldFilter.CreateObject -= WorldFilter_CreateObject;

            if (lootProfile != null) ((VTClassic.LootCore)lootProfile).UnloadProfile();

            VTankControl.Nav_UnBlock();
            // restore cram/stack settings
            VTankControl.Item_UnBlock();
        }

        public void Think() {
            if (!Globals.Settings.AutoVendor.Enabled)
                return;

            try {
                var thinkInterval = TimeSpan.FromMilliseconds(Globals.Settings.AutoVendor.Speed);

                if (Globals.Settings.AutoVendor.Enabled && DateTime.UtcNow - lastThought >= thinkInterval && DateTime.UtcNow - startedVendoring >= thinkInterval) {
                    lastThought = DateTime.UtcNow;

                    //if autovendor is running, and nav block has less than a second plus thinkInterval remaining, refresh it
                    if (needsVendoring && VTankControl.navBlockedUntil < DateTime.UtcNow + TimeSpan.FromSeconds(1) + thinkInterval) {
                        VTankControl.Item_Block(30000, Globals.Settings.Plugin.Debug);
                        VTankControl.Nav_Block(30000, Globals.Settings.Plugin.Debug);
                    }

                    if (needsVendoring && waitingForIds) {
                        if (Globals.Assessor.NeedsInventoryData(itemsToId)) {
                            if (DateTime.UtcNow - lastIdSpam > TimeSpan.FromSeconds(15)) {
                                lastIdSpam = startedVendoring = DateTime.UtcNow;

                                Logger.Debug(string.Format("AutoVendor waiting to id {0} items, this will take approximately {0} seconds.", Globals.Assessor.GetNeededIdCount(itemsToId)));
                            }

                            // waiting
                            return;
                        } else {
                            waitingForIds = false;
                        }
                    }

                    if (needsVendoring && Globals.Settings.AutoVendor.TestMode == true) {
                        DoTestMode();
                        Stop();
                        return;
                    }


                    if (needsVendoring && shouldStack && Globals.Settings.InventoryManager.AutoStack == true) {
                        if (Globals.InventoryManager.AutoStack() == true) return;
                    }
                    shouldStack = false;

                    List<int> cramExcludeList = GetSellItems().Select((x) => { return x.Id; }).ToList();
                    if (needsVendoring && Globals.Settings.InventoryManager.AutoCram == true) {
                        if (Globals.InventoryManager.AutoCram(cramExcludeList, true) == true) return;
                    }

                    if (needsVendoring == true && HasVendorOpen()) {
                        if (needsToBuy) {
                            needsToBuy = false;
                            shouldStack = true;
                            Globals.Core.Actions.VendorBuyAll();
                            lastVendorAction = DateTime.UtcNow;
                        } else if (needsToSell) {
                            needsToSell = false;
                            shouldStack = false;
                            Globals.Core.Actions.VendorSellAll();
                            lastVendorAction = DateTime.UtcNow;
                        } else {
                            DoVendoring();
                        }
                    }
                    // vendor closed?
                    else if (needsVendoring == true) {
                        Stop();
                    }
                }
            } catch (Exception ex) { Logger.LogException(ex); }
        }

        private void DoVendoring() {
            try {
                List<BuyItem> buyItems = GetBuyItems();
                List<WorldObject> sellItems = GetSellItems();

                if (!HasVendorOpen()) {
                    Logger.Debug("AutoVendor vendor was closed, stopping!");
                    Stop();
                    return;
                }

                Logger.Debug(string.Format("AutoVendor Wants Buy: {0}: ({1}, Sell: {2}: {3} ({4})",
                        buyItems.Count,  buyItems.Count > 0 ? buyItems[0].Item.Name : "null",
                        sellItems.Count, sellItems.Count > 0 ? Util.GetObjectName(sellItems[0].Id) : "null",
                        (sellItems.Count > 0 ? sellItems[0].Values(LongValueKey.StackCount).ToString() : "0")));

                Globals.Core.Actions.VendorClearBuyList();
                Globals.Core.Actions.VendorClearSellList();

                var totalBuyCount = 0;
                var totalBuyPyreals = 0;
                var totalBuySlots = 0;
                var freeSlots = Util.GetFreeMainPackSpace();
                var pyrealCount = Util.PyrealCount();
                StringBuilder buyAdded = new StringBuilder("Autovendor Buy List Added: ");

                foreach (var buyItem in buyItems.OrderBy(o=>o.Item.Value).ToList()) {
                    var vendorPrice = GetVendorSellPrice(buyItem.Item);
                    if (vendorPrice == 0) {
                        Util.WriteToChat(string.Format("Fatal: No vendor price found while adding {0:n}x {1}[0x{2:X8}] Value: {3:n}", buyItem.Amount, buyItem.Item.Name, buyItem.Item.Id, vendorPrice));
                        Stop(false);
                        return;
                    }

                    if ((totalBuyPyreals + GetVendorSellPrice(buyItem.Item)) <= pyrealCount && (freeSlots - totalBuySlots) > 1) {
                        int buyCount = (int)((pyrealCount - totalBuyPyreals) / vendorPrice);
                        if (buyCount > buyItem.Amount) {
                            buyCount = buyItem.Amount;
                        }
                        needsToBuy = true;
                        Globals.Core.Actions.VendorAddBuyList(buyItem.Item.Id, buyCount);
                        totalBuySlots++;
                        totalBuyPyreals += (int)(vendorPrice * buyCount);
                        totalBuyCount++;

                        if (Globals.Settings.Plugin.Debug) {
                            if (totalBuyCount > 1)
                                buyAdded.Append(",");
                            buyAdded.Append(string.Format("{0} {1}", buyCount, buyItem.Item.Name));
                        }
                    } else if (totalBuyCount > 0) {
                        break;
                    }
                }

                if (totalBuyCount > 0) {
                    Logger.Debug(string.Format("{0} - {1}/{2}", buyAdded.ToString(), totalBuyPyreals, pyrealCount));
                    return;
                }

                VendorItem nextBuyItem = null;

                if (buyItems.Count > 0) {
                    nextBuyItem = buyItems[0].Item;
                }

                int totalSellValue = 0;
                int sellItemCount = 0;

                StringBuilder sellAdded = new StringBuilder("Autovendor Sell List Added: ");
                while (sellItemCount < sellItems.Count && sellItemCount < 99) { // GDLE limits transactions to 99 items. (less than 100)
                    var item = sellItems[sellItemCount];
                    var value = GetVendorBuyPrice(item);
                    var stackSize = item.Values(LongValueKey.StackCount, 1);
                    var stackCount = 0;

                    // dont sell notes if we are trying to buy notes...
                    if (((nextBuyItem != null && nextBuyItem.ObjectClass == ObjectClass.TradeNote) || nextBuyItem == null) && item.ObjectClass == ObjectClass.TradeNote) {
                        Logger.Debug(string.Format("AutoVendor bail: buyItem: {0} sellItem: {1}", nextBuyItem == null ? "null" : nextBuyItem.Name, Util.GetObjectName(item.Id)));

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
                                Logger.Debug("AutoVendor Selling single " + wo.Name + " so we can afford to buy: " + nextBuyItem.Name);

                                Globals.Core.Actions.VendorAddSellList(wo.Id);
                                needsToSell = true;
                                return;
                            }
                        }

                        Globals.Core.Actions.SelectItem(item.Id);
                        Globals.Core.Actions.SelectedStackCount = 1;
                        Globals.Core.Actions.MoveItem(item.Id, Globals.Core.CharacterFilter.Id, 0, false);

                        Logger.Debug(string.Format("AutoVendor Splitting {0}. old: {1} new: {2}", Util.GetObjectName(item.Id), item.Values(LongValueKey.StackCount), 1));
                        
                        shouldStack = false;

                        return;
                    }

                    // cant sell the whole stack? split it into what we can sell
                    if (stackSize > 1 && !PyrealsWillFitInMainPack(totalSellValue + (value * stackSize))) {
                        // if we already have items to sell, sell those first
                        if (sellItemCount > 0) break;

                        while (stackCount <= stackSize) {
                            // we include an extra PYREAL_STACK_SIZE because we know we are going to split this item
                            if (!PyrealsWillFitInMainPack(PYREAL_STACK_SIZE + totalSellValue + (value * (stackCount)))) {
                                Globals.Core.Actions.SelectItem(item.Id);
                                Globals.Core.Actions.SelectedStackCount = stackCount > 1 ? stackCount - 1 : 1;
                                Globals.Core.Actions.MoveItem(item.Id, Globals.Core.CharacterFilter.Id, 0, false);

                                Logger.Debug(string.Format("AutoVendor Splitting {0}. old: {1} new: {2}", Util.GetObjectName(item.Id), item.Values(LongValueKey.StackCount), stackCount));

                                shouldStack = false;

                                return;
                            }

                            ++stackCount;
                        }
                    } else {
                        stackCount = item.Values(LongValueKey.StackCount, 1);
                    }

                    if (!PyrealsWillFitInMainPack(totalSellValue + (value * stackCount))) {
                        if (sellItemCount < 1) {
                            Util.WriteToChat("AutoVendor No inventory room to sell... " + Util.GetObjectName(item.Id));
                            Stop();
                            return;
                        }
                        break;
                    }
                    
                    if (Globals.Settings.Plugin.Debug) {
                        if (sellItemCount > 0)
                            sellAdded.Append(", ");
                        sellAdded.Append(Util.GetObjectName(item.Id));
                    }

                    Globals.Core.Actions.VendorAddSellList(item.Id);

                    totalSellValue += value * stackCount;
                    ++sellItemCount;
                }

                if (sellItemCount > 0) {
                    Logger.Debug(sellAdded.ToString()+" - "+totalSellValue);
                    needsToSell = true;
                    return;
                }
                Stop();
            } catch (Exception ex) { Logger.LogException(ex); }
        }

        private float GetVendorSellPrice(VendorItem item) {
            var price = (float)0;
            var vendor = VendorCache.GetVendor(Globals.Core.Actions.VendorId);

            try {
                if (vendorId == 0 || vendor == null) return 0;
                var eachItemValue = (int)(item.Value / item.StackCount);

                price = (float)(eachItemValue * (item.ObjectClass == ObjectClass.TradeNote ? 1.15 : (float)vendor.SellRate));
                //Util.WriteToChat(string.Format("Value of {0}[0x{1:X8}] is {2:n0} (original {3:n0}", item.Name, item.Id, price, eachItemValue));

            } catch (Exception ex) { Logger.LogException(ex); }

            return price;
        }

        private int GetVendorBuyPrice(WorldObject wo) {
            var price = 0;
            var vendor = VendorCache.GetVendor(Globals.Core.Actions.VendorId);

            try {
                if (vendorId == 0 || vendor == null) return 0;

                var eachItemValue = (int)wo.Values(LongValueKey.Value, 0) / wo.Values(LongValueKey.StackCount, 1);

                price = (int)Math.Round((eachItemValue * (wo.ObjectClass == ObjectClass.TradeNote ? 1 : (float)vendor.BuyRate)), 0);
                //Util.WriteToChat(string.Format("Value of {0}[0x{1:X8}] is {2:n0} (original {3:n0}", wo.Name, wo.Id, price, eachItemValue));
            } catch (Exception ex) { Logger.LogException(ex); }

            return price;
        }

        private bool PyrealsWillFitInMainPack(int amount) {
            var myPyreals = Util.PyrealCount();
            var mySlots = Util.GetFreeMainPackSpace();
            var myPartial = myPyreals % PYREAL_STACK_SIZE;
            //Util.WriteToChat(string.Format("PyrealsWillFitInMainPack({0:n0}): {1:n0} slots free, I have {2:n0} pyreals.", amount, mySlots, myPyreals));
            if (myPartial > 0) {
                amount -= (PYREAL_STACK_SIZE - myPartial);      // Free storage of these pyreals in the existing stack!
            }
            mySlots -= (amount / PYREAL_STACK_SIZE);            // take slots for bulk of pyreals
            if ((amount % PYREAL_STACK_SIZE) > 0) {
                mySlots--;                          // take slot for remaining partial
            }
            return (mySlots > 0); // ensures 1 pack slot always remains free.
        }

        private List<BuyItem> GetBuyItems() {
            VendorInfo vendor = VendorCache.GetVendor(Globals.Core.Actions.VendorId);
            List<BuyItem> buyItems = new List<BuyItem>();

            if (vendor == null) return buyItems;


            // keepUpTo rules first, just like mag-tools
            foreach (VendorItem item in vendor.Items.Values) {
                var amount = 0;
                uTank2.LootPlugins.GameItemInfo itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithVendorObjectTemplateID(item.Id);
                if (itemInfo == null)
                    continue;
                uTank2.LootPlugins.LootAction result = ((VTClassic.LootCore)lootProfile).GetLootDecision(itemInfo);
                if (!result.IsKeepUpTo)
                    continue;
                amount = result.Data1 - Util.GetItemCountInInventoryByName(item.Name);

                while (amount > 0) {
                    var thisamount = amount;
                    if (thisamount > MAX_VENDOR_BUY_COUNT) thisamount = MAX_VENDOR_BUY_COUNT;
                    if (thisamount > item.StackMax) thisamount = item.StackMax;

                    buyItems.Add(new BuyItem(item, thisamount));
                    amount -= thisamount;
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

            if (vendor == null || lootProfile == null) return sellObjects;

            foreach (WorldObject wo in Globals.Core.WorldFilter.GetInventory()) {
                if (!ItemIsSafeToGetRidOf(wo)) continue;

                uTank2.LootPlugins.GameItemInfo itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithID(wo.Id);

                if (itemInfo == null) continue;

                uTank2.LootPlugins.LootAction result = ((VTClassic.LootCore)lootProfile).GetLootDecision(itemInfo);

                if (!result.IsSell || !vendor.WillBuyItem(wo))
                    continue;
                
                sellObjects.Add(wo);
            }

            sellObjects.Sort(delegate (WorldObject wo1, WorldObject wo2) {
                // tradenotes last
                if (wo1.ObjectClass == ObjectClass.TradeNote && wo2.ObjectClass != ObjectClass.TradeNote) return 1;
                if (wo1.ObjectClass != ObjectClass.TradeNote && wo2.ObjectClass == ObjectClass.TradeNote) return -1;

                var buyPrice1 = GetVendorBuyPrice(wo1) * wo1.Values(LongValueKey.StackCount, 1);
                var buyPrice2 = GetVendorBuyPrice(wo2) * wo2.Values(LongValueKey.StackCount, 1);

                // cheapest first
                return buyPrice1.CompareTo(buyPrice2);
            });

            return sellObjects;
        }

        private bool ItemIsSafeToGetRidOf(WorldObject wo) {
            if (wo == null) return false;

            // skip 0 value
            if (wo.Values(LongValueKey.Value, 0) <= 0) return false;

            // sellable?
            if (wo.Values(BoolValueKey.CanBeSold, true) == false) return false;

            // bail if we are only selling from main pack and this isnt in there
            if (Globals.Settings.AutoVendor.OnlyFromMainPack == true && wo.Container != Globals.Core.CharacterFilter.Id) {
                return false;
            }

            return Util.IsItemSafeToGetRidOf(wo);
        }

        private void DoTestMode() {
            Util.WriteToChat("Buy Items:");

            foreach (BuyItem bi in GetBuyItems()) {
                uTank2.LootPlugins.GameItemInfo itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithVendorObjectTemplateID(bi.Item.Id);

                if (itemInfo == null) continue;

                uTank2.LootPlugins.LootAction result = ((VTClassic.LootCore)lootProfile).GetLootDecision(itemInfo);

                if (result.IsKeepUpTo || result.IsKeep) {
                    Util.WriteToChat(string.Format("  {0} * {1} - {2}", bi.Item.Name, bi.Amount == 5000 ? "∞" : bi.Amount.ToString(), result.RuleName));
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

        public bool HasVendorOpen() {
            bool hasVendorOpen = false;

            try {
                if (Globals.Core.Actions.VendorId == 0) return false;

                hasVendorOpen = true;
            } catch (Exception ex) { Logger.LogException(ex); }

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
                    Globals.Core.CommandLineText -= Current_CommandLineText;
                    Globals.Core.WorldFilter.CreateObject -= WorldFilter_CreateObject;
                }
                disposed = true;
            }
        }
    }
}
