using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Decal.Adapter;
using Decal.Adapter.Wrappers;
using Mag.Shared.Settings;
using UtilityBelt.Lib.VendorCache;
using UtilityBelt.Views;
using VirindiViewService.Controls;

namespace UtilityBelt.Tools {
    public class AutoVendor : IDisposable {
        private const int MAX_VENDOR_BUY_COUNT = 5000;
        private const double PYREAL_STACK_SIZE = 25000.0;
        private DateTime firstThought = DateTime.MinValue;
        private DateTime lastThought = DateTime.MinValue;
        private DateTime startedVendoring = DateTime.MinValue;
        private DateTime lastVendorAction = DateTime.MinValue;

        private bool disposed;
        private bool waitingForVendor = true;
        private int lastVendorOpened = 0;
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
        HudCheckBox UIAutoVendorDebug { get; set; }
        HudCheckBox UIAutoVendorShowMerchantInfo { get; set; }
        HudCheckBox UIAutoVendorThink { get; set; }
        HudCheckBox UIAutoVendorOnlyFromMainPack { get; set; }
        HudHSlider UIAutoVendorSpeed { get; set; }
        HudStaticText UIAutoVendorSpeedText { get; set; }

        public AutoVendor() {
            try {
                Directory.CreateDirectory(Path.Combine(Util.GetPluginDirectory(), "autovendor"));
                Directory.CreateDirectory(Path.Combine(Util.GetCharacterDirectory(), @"autovendor"));
                Directory.CreateDirectory(Path.Combine(Util.GetServerDirectory(), @"autovendor"));

                UIAutoVendorSpeedText = Globals.MainView.view != null ? (HudStaticText)Globals.MainView.view["AutoVendorSpeedText"] : new HudStaticText();
                UIAutoVendorSpeedText.Text = Globals.Config.AutoVendor.Speed.Value.ToString();

                UIAutoVendorEnable = Globals.MainView.view != null ? (HudCheckBox)Globals.MainView.view["AutoVendorEnabled"] : new HudCheckBox();
                UIAutoVendorEnable.Checked = Globals.Config.AutoVendor.Enabled.Value;
                UIAutoVendorEnable.Change += UIAutoVendorEnable_Change;
                Globals.Config.AutoVendor.Enabled.Changed += Config_AutoVendor_Enabled_Changed;

                UIAutoVendorTestMode = Globals.MainView.view != null ? (HudCheckBox)Globals.MainView.view["AutoVendorTestMode"] : new HudCheckBox();
                UIAutoVendorTestMode.Checked = Globals.Config.AutoVendor.TestMode.Value;
                UIAutoVendorTestMode.Change += UIAutoVendorTestMode_Change;
                Globals.Config.AutoVendor.TestMode.Changed += Config_AutoVendor_TestMode_Changed;

                UIAutoVendorDebug = Globals.MainView.view != null ? (HudCheckBox)Globals.MainView.view["AutoVendorDebug"] : new HudCheckBox();
                UIAutoVendorDebug.Checked = Globals.Config.AutoVendor.Debug.Value;
                UIAutoVendorDebug.Change += UIAutoVendorDebug_Change;
                Globals.Config.AutoVendor.Debug.Changed += Config_AutoVendor_Debug_Changed;

                UIAutoVendorShowMerchantInfo = Globals.MainView.view != null ? (HudCheckBox)Globals.MainView.view["AutoVendorShowMerchantInfo"] : new HudCheckBox();
                UIAutoVendorShowMerchantInfo.Checked = Globals.Config.AutoVendor.ShowMerchantInfo.Value;
                UIAutoVendorShowMerchantInfo.Change += UIAutoVendorShowMerchantInfo_Change;
                Globals.Config.AutoVendor.ShowMerchantInfo.Changed += Config_AutoVendor_ShowMerchantInfo_Changed;

                UIAutoVendorThink = Globals.MainView.view != null ? (HudCheckBox)Globals.MainView.view["AutoVendorThink"] : new HudCheckBox();
                UIAutoVendorThink.Checked = Globals.Config.AutoVendor.Think.Value;
                UIAutoVendorThink.Change += UIAutoVendorThink_Change;
                Globals.Config.AutoVendor.Think.Changed += Config_AutoVendor_Think_Changed;

                UIAutoVendorOnlyFromMainPack = Globals.MainView.view != null ? (HudCheckBox)Globals.MainView.view["AutoVendorOnlyFromMainPack"] : new HudCheckBox();
                UIAutoVendorOnlyFromMainPack.Checked = Globals.Config.AutoVendor.OnlyFromMainPack.Value;
                UIAutoVendorOnlyFromMainPack.Change += UIAutoVendorOnlyFromMainPack_Change;
                Globals.Config.AutoVendor.OnlyFromMainPack.Changed += OnlyFromMainPack_Changed;

                UIAutoVendorSpeed = Globals.MainView.view != null ? (HudHSlider)Globals.MainView.view["AutoVendorSpeed"] : new HudHSlider();
                UIAutoVendorSpeed.Position = (Globals.Config.AutoVendor.Speed.Value / 100) - 3;
                UIAutoVendorSpeed.Changed += UIAutoVendorSpeed_Changed;
                Globals.Config.AutoVendor.Speed.Changed += Config_AutoVendor_Speed_Changed;

                Globals.Core.WorldFilter.ApproachVendor += WorldFilter_ApproachVendor;
                Globals.Core.CommandLineText += Current_CommandLineText;
                Globals.Core.WorldFilter.CreateObject += WorldFilter_CreateObject;
            } catch (Exception ex) { Logger.LogException(ex); }
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

        private void UIAutoVendorOnlyFromMainPack_Change(object sender, EventArgs e) {
            Globals.Config.AutoVendor.OnlyFromMainPack.Value = UIAutoVendorOnlyFromMainPack.Checked;
        }

        private void OnlyFromMainPack_Changed(Setting<bool> obj) {
            UIAutoVendorOnlyFromMainPack.Checked = Globals.Config.AutoVendor.OnlyFromMainPack.Value;
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
                VendorCache.AddVendor(e.Vendor);

                if (Globals.Config.AutoVendor.ShowMerchantInfo.Value && e.Vendor.MerchantId != lastVendorOpened) {
                    Util.WriteToChat(string.Format("{0}[0x{4:X8}]: BuyRate: {1}% SellRate: {2}% MaxValue: {3:n0}",
                        Globals.Core.WorldFilter[e.Vendor.MerchantId].Name, e.Vendor.BuyRate * 100, e.Vendor.SellRate * 100, e.Vendor.MaxValue, e.Vendor.MerchantId));
                    lastVendorOpened = e.Vendor.MerchantId;
                }

                if (Globals.Config.AutoVendor.Enabled.Value == false)
                    return;

                if (Globals.Core.Actions.IsValidObject(e.MerchantId) == false) {
                    Stop();
                    return;
                }

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
                if (!Globals.Config.AutoVendor.Enabled.Value || !needsVendoring) return;

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
            if (Globals.Config.AutoVendor.OnlyFromMainPack.Value == true) {
                inventory.SetFilter(new ByContainerFilter(Globals.Core.CharacterFilter.Id));
            }

            // build a list of items to id from our inventory, attempting to be smart about it
            foreach (var item in inventory) {
                // will the vendor buy this item?
                VendorInfo vendor = VendorCache.GetVendor(vendorId);
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
            VTankControl.Item_Block(30000, Globals.Config.AutoVendor.Debug.Value);
            VTankControl.Nav_Block(30000, Globals.Config.AutoVendor.Debug.Value);

            Globals.Core.WorldFilter.CreateObject += WorldFilter_CreateObject;
        }

        public void Stop(bool silent = false) {
            // we delay for 2 seconds in case of server lag, so that we can properly
            // detect getting pyreals from the vendor transaction at the end of the process.
            if (DateTime.UtcNow - lastVendorAction < TimeSpan.FromSeconds(2)) return;


            if (!silent) {
                if (Globals.Config.AutoVendor.Think.Value == true) {
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
            try {
                var thinkInterval = TimeSpan.FromMilliseconds(Globals.Config.AutoVendor.Speed.Value);

                if (Globals.Config.AutoVendor.Enabled.Value && DateTime.UtcNow - lastThought >= thinkInterval && DateTime.UtcNow - startedVendoring >= thinkInterval) {
                    lastThought = DateTime.UtcNow;

                    //if autovendor is running, and nav block has less than a second plus thinkInterval remaining, refresh it
                    if (needsVendoring && VTankControl.navBlockedUntil < DateTime.UtcNow + TimeSpan.FromSeconds(1) + thinkInterval) {
                        VTankControl.Item_Block(30000, Globals.Config.AutoVendor.Debug.Value);
                        VTankControl.Nav_Block(30000, Globals.Config.AutoVendor.Debug.Value);
                    }

                    if (needsVendoring && waitingForIds) {
                        if (Globals.Assessor.NeedsInventoryData(itemsToId)) {
                            if (DateTime.UtcNow - lastIdSpam > TimeSpan.FromSeconds(15)) {
                                lastIdSpam = DateTime.UtcNow;
                                startedVendoring = DateTime.UtcNow;

                                if (Globals.Config.AutoVendor.Debug.Value == true) {
                                    Util.WriteToChat(string.Format("AutoVendor waiting to id {0} items, this will take approximately {0} seconds.", Globals.Assessor.GetNeededIdCount(itemsToId)));
                                }
                            }

                            // waiting
                            return;
                        } else {
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
                    if (Globals.Config.AutoVendor.Debug.Value == true) {
                        Util.WriteToChat("AutoVendor vendor was closed, stopping!");
                    }
                    Stop();
                    return;
                }

                if (Globals.Config.AutoVendor.Debug.Value == true) {
                    Util.WriteToChat(string.Format("AutoVendor Wants Buy: {0}", buyItems.Count > 0 ? buyItems[0].Item.Name : "null"));
                    Util.WriteToChat(string.Format("AutoVendor Wants Sell: {0} ({1})", sellItems.Count > 0 ? Util.GetObjectName(sellItems[0].Id) : "null", (sellItems.Count > 0 ? sellItems[0].Values(LongValueKey.StackCount).ToString() : "0")));
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
                    } else if (totalBuyCount > 0) {
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

                    // cant sell the whole stack? split it into what we can sell
                    if (stackSize > 1 && !PyrealsWillFitInMainPack(totalSellValue + (value * stackSize))) {
                        // if we already have items to sell, sell those first
                        if (sellItemCount > 0) break;

                        while (stackCount <= stackSize) {
                            // we include an extra PYREAL_STACK_SIZE because we know we are going to split this item
                            if (!PyrealsWillFitInMainPack((int)PYREAL_STACK_SIZE + totalSellValue + (value * (stackCount)))) {
                                Globals.Core.Actions.SelectItem(item.Id);
                                Globals.Core.Actions.SelectedStackCount = stackCount > 1 ? stackCount - 1 : 1;
                                Globals.Core.Actions.MoveItem(item.Id, Globals.Core.CharacterFilter.Id, 0, false);

                                if (Globals.Config.AutoVendor.Debug.Value == true) {
                                    Util.WriteToChat(string.Format("AutoVendor Splitting {0}. old: {1} new: {2}", Util.GetObjectName(item.Id), item.Values(LongValueKey.StackCount), stackCount));
                                }

                                shouldStack = false;

                                return;
                            }

                            ++stackCount;
                        }
                    } else {
                        stackCount = item.Values(LongValueKey.StackCount, 1);
                    }

                    if (!PyrealsWillFitInMainPack(totalSellValue + (value * stackCount))) {
                        Util.WriteToChat(string.Format("break to sell 2: {0} - {1}", sellItemCount, stackCount));
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
            } catch (Exception ex) { Logger.LogException(ex); }
        }

        private int GetVendorSellPrice(VendorItem item) {
            var price = 0;
            var vendor = VendorCache.GetVendor(Globals.Core.Actions.VendorId);

            try {
                if (vendorId == 0 || vendor == null) return 0;
                var sellRate = vendor.SellRate;

                // notes are always 1.15 when buying
                if (item.ObjectClass == ObjectClass.TradeNote) sellRate = 1.15;

                price = (int)Math.Ceiling((item.Value / item.StackCount) * sellRate);
            } catch (Exception ex) { Logger.LogException(ex); }

            return price;
        }

        private int GetVendorBuyPrice(WorldObject wo) {
            var price = 0;
            var vendor = VendorCache.GetVendor(Globals.Core.Actions.VendorId);

            try {
                if (vendorId == 0 || vendor == null) return 0;
                var buyRate = vendor.BuyRate;

                // notes are always 1 when selling
                if (wo.ObjectClass == ObjectClass.TradeNote) buyRate = 1;

                price = (int)Math.Floor((wo.Values(LongValueKey.Value, 0) / wo.Values(LongValueKey.StackCount, 1)) * buyRate);
            } catch (Exception ex) { Logger.LogException(ex); }

            return price;
        }

        private bool PyrealsWillFitInMainPack(int amount) {
            int packSlotsNeeded = 1 + (int)Math.Ceiling(amount / PYREAL_STACK_SIZE); // always leave 1 slot free. Fixes issue with main pack filling, and unable to buy a Trade Note.

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

                // then cheapest first
                if (GetVendorBuyPrice(wo2) > GetVendorBuyPrice(wo2)) return 1;
                if (GetVendorBuyPrice(wo1) < GetVendorBuyPrice(wo2)) return -1;

                // then smallest stack size
                if (wo1.Values(LongValueKey.StackCount, 1) > wo2.Values(LongValueKey.StackCount, 1)) return 1;
                if (wo1.Values(LongValueKey.StackCount, 1) < wo2.Values(LongValueKey.StackCount, 1)) return -1;

                return 0;
            });

            return sellObjects;
        }

        private bool ItemIsSafeToGetRidOf(WorldObject wo) {
            if (wo == null) return false;

            // skip 0 value
            if (wo.Values(LongValueKey.Value, 0) <= 0) return false;

            // bail if we are only selling from main pack and this isnt in there
            if (Globals.Config.AutoVendor.OnlyFromMainPack.Value == true && wo.Container != Globals.Core.CharacterFilter.Id) {
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
