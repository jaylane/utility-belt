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
        private DateTime lastEvent = DateTime.MinValue;
        private DateTime vendorOpened = DateTime.MinValue;
        private DateTime bailTimer = DateTime.MinValue;

        private bool disposed;
        private bool isRunning = false;
        private object lootProfile;
        private bool needsToBuy = false;
        private bool needsToSell = false;
        private int vendorId = 0;
        private string vendorName = "";
        private float vendorSellRate = 1;
        private float vendorBuyRate = 1;
        private bool waitingForIds = false;
        private DateTime lastIdSpam = DateTime.MinValue;
        private int lastIdCount;
        private readonly List<int> itemsToId = new List<int>();

        private int expectedPyreals = 0;
        private readonly Dictionary<int, int> myPyreals = new Dictionary<int, int>();
        private readonly Dictionary<int, int> pendingBuy = new Dictionary<int, int>();
        private readonly List<int> pendingSell = new List<int>();

        HudCheckBox UIAutoVendorEnable { get; set; }
        HudCheckBox UIAutoVendorTestMode { get; set; }
        HudCheckBox UIAutoVendorShowMerchantInfo { get; set; }
        HudCheckBox UIAutoVendorThink { get; set; }
        HudCheckBox UIAutoVendorOnlyFromMainPack { get; set; }
        HudHSlider UIAutoVendorTries { get; set; }
        HudStaticText UIAutoVendorTriesText { get; set; }

        public AutoVendor() {
            try {
                Directory.CreateDirectory(Path.Combine(Util.GetPluginDirectory(), "autovendor"));
                Directory.CreateDirectory(Path.Combine(Util.GetCharacterDirectory(), "autovendor"));
                Directory.CreateDirectory(Path.Combine(Util.GetServerDirectory(), "autovendor"));

                UIAutoVendorTriesText = Globals.MainView.view != null ? (HudStaticText)Globals.MainView.view["AutoVendorTriesText"] : new HudStaticText();

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

                UIAutoVendorTries = Globals.MainView.view != null ? (HudHSlider)Globals.MainView.view["AutoVendorTries"] : new HudHSlider();
                UIAutoVendorTries.Changed += UIAutoVendorTries_Changed;

                Globals.Core.WorldFilter.ApproachVendor += WorldFilter_ApproachVendor;
                Globals.Core.CommandLineText += Current_CommandLineText;

                Globals.Settings.AutoSalvage.PropertyChanged += (s, e) => { UpdateUI(); };

                UpdateUI();
            } catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UpdateUI() {
            UIAutoVendorEnable.Checked = Globals.Settings.AutoVendor.Enabled;
            UIAutoVendorTestMode.Checked = Globals.Settings.AutoVendor.TestMode;
            UIAutoVendorShowMerchantInfo.Checked = Globals.Settings.AutoVendor.ShowMerchantInfo;
            UIAutoVendorThink.Checked = Globals.Settings.AutoVendor.Think;
            UIAutoVendorOnlyFromMainPack.Checked = Globals.Settings.AutoVendor.OnlyFromMainPack;
            UIAutoVendorTries.Position = Globals.Settings.AutoVendor.Tries - 1;
            UIAutoVendorTriesText.Text = Globals.Settings.AutoVendor.Tries.ToString();
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

        private void UIAutoVendorTries_Changed(int min, int max, int pos) {
            pos++;
            if (pos != Globals.Settings.AutoVendor.Tries) {
                Globals.Settings.AutoVendor.Tries = pos;
                UIAutoVendorTriesText.Text = pos.ToString();
            }
        }

        private void Reset_pendingBuy() {
            if (pendingBuy.Count > 0) {
                Logger.Debug($"ERR: pendingBuy reset while it still contained {pendingBuy.Count:n0} items!");
                foreach (KeyValuePair<int, int> fff in pendingBuy) {
                    Logger.Debug($"    Type 0x{fff.Key:X8} x{fff.Value:n0}");
                }
            }
            pendingBuy.Clear();
        }
        private void Reset_pendingSell() {
            if (pendingSell.Count > 0) {
                Logger.Debug($"ERR: pendingSell reset while it still contained {pendingSell.Count:n0} items!");
                foreach (int i in pendingSell) {
                    Logger.Debug($"    Item 0x{i:X8} {(Globals.Core.WorldFilter[i] == null?"Error":Globals.Core.WorldFilter[i].Name)}");
                }
            }
            pendingSell.Clear();
        }
        private void Reset_myPyreals() {
            myPyreals.Clear();
            foreach (WorldObject wo in Globals.Core.WorldFilter.GetInventory()) {
                if (wo.Type == 273) {
                    myPyreals.Add(wo.Id, wo.Values(LongValueKey.StackCount, 1));
                }
            }
        }
        private void CheckDone() {
            if (Math.Abs(expectedPyreals) < 50 && pendingSell.Count() == 0 && pendingBuy.Count() == 0)
                lastEvent = DateTime.MinValue;
            else
                lastEvent = DateTime.UtcNow;
            bailTimer = DateTime.Now;
        }
        private void EchoFilter_ClientDispatch(object sender, NetworkMessageEventArgs e) {
            if (!isRunning) return;
            if (e.Message.Type == 0xF7B1 && (int)e.Message["action"] == 0x005F) {
                //Logger.Debug("Server has Buy");
                Reset_myPyreals();
                lastEvent = DateTime.UtcNow;
            }
            if (e.Message.Type == 0xF7B1 && (int)e.Message["action"] == 0x0060) {
                //Logger.Debug("Server has Sell");
                Reset_myPyreals();
                lastEvent = DateTime.UtcNow;
            }
        }
        private void EchoFilter_ServerDispatch(object sender, NetworkMessageEventArgs e) {
            if (!isRunning) return;
            try {
                if (e.Message.Type == 0xF7B0 && (int)e.Message["event"] == 0x0022 && (int)e.Message["container"] != Globals.Core.CharacterFilter.Id) {  // ACE Item Remove Handling
                    if (myPyreals.ContainsKey((int)e.Message["item"])) {
                        if (Math.Abs(expectedPyreals) > 50) {
                            expectedPyreals += myPyreals[(int)e.Message["item"]];
                            CheckDone();
                        }
                    } else if (pendingSell.Contains((int)e.Message["item"])) {
                        pendingSell.Remove((int)e.Message["item"]);
                        CheckDone();
                    }
                }
                if (e.Message.Type == 0x0024) { // GDLE Item Remove Handling
                    if (myPyreals.ContainsKey((int)e.Message["object"])) {
                        if (Math.Abs(expectedPyreals) > 50) { //fix for GDLE taking out too many pyreals (MR #517)
                            expectedPyreals += myPyreals[(int)e.Message["object"]];
                            CheckDone();
                        }
                    } else if (pendingSell.Contains((int)e.Message["object"])) {
                        pendingSell.Remove((int)e.Message["object"]);
                        CheckDone();
                    }
                }
                if (e.Message.Type == 0x0197 && Globals.Core.WorldFilter[(int)e.Message["item"]] != null && Globals.Core.WorldFilter[(int)e.Message["item"]].Type == 273) { // GDLE Stack Size Handling
                    var newStackSize = (int)e.Message["count"];
                    myPyreals.TryGetValue((int)e.Message["item"], out int oldStackSize);
                    var stackChange = newStackSize - oldStackSize;
                    if (Math.Abs(expectedPyreals) > 50) { //fix for GDLE taking out too many pyreals (MR #517)
                        expectedPyreals -= stackChange;
                        CheckDone();
                    }
                }
                if (e.Message.Type == 0x02DA && (int)e.Message["key"] == 2 && (int)e.Message["value"] == Globals.Core.CharacterFilter.Id && Globals.Core.WorldFilter[(int)e.Message["object"]] != null) { // GDLE Item Add Handling
                    var wo = Globals.Core.WorldFilter[(int)e.Message["object"]];
                    if (wo.Type == 273) { // Pyreal
                        if (Math.Abs(expectedPyreals) > 50) {
                            expectedPyreals -= wo.Values(LongValueKey.StackCount, 1);
                            CheckDone();
                        }
                    } else if (pendingBuy.TryGetValue(wo.Type, out int pbq)) {
                        pbq -= wo.Values(LongValueKey.StackCount, 1);
                        if (pbq <= 0) {
                            pendingBuy.Remove(wo.Type);
                            CheckDone();
                        } else {
                            pendingBuy[wo.Type] = pbq;
                        }
                    }
                }
                if (e.Message.Type == 0xF745 && Globals.Core.WorldFilter[(int)e.Message["object"]] != null && Globals.Core.WorldFilter[(int)e.Message["object"]].Container == Globals.Core.CharacterFilter.Id) { // ACE Item Add Handling
                    var wo = Globals.Core.WorldFilter[(int)e.Message["object"]];
                    if (wo.Type == 273) { // Pyreal
                        if (Math.Abs(expectedPyreals) > 50) {
                            expectedPyreals -= wo.Values(LongValueKey.StackCount, 1);
                            CheckDone();
                        }
                    } else if (pendingBuy.TryGetValue(wo.Type, out int pbq)) {
                        pbq -= wo.Values(LongValueKey.StackCount, 1);
                        if (pbq <= 0) {
                            pendingBuy.Remove(wo.Type);
                            CheckDone();
                        } else {
                            pendingBuy[wo.Type] = pbq;
                        }
                    }
                }
            } catch { }
        }
        private void WorldFilter_ApproachVendor(object sender, ApproachVendorEventArgs e) {
            try {
                if (isRunning) return;

                VendorCache.AddVendor(e.Vendor);

                if (Globals.Core.WorldFilter[e.MerchantId] == null) {
                    return;
                }

                if (vendorId != e.Vendor.MerchantId) {
                    vendorId = Globals.Core.WorldFilter[e.MerchantId].Id;
                    vendorName = Globals.Core.WorldFilter[e.MerchantId].Name;
                    vendorSellRate = e.Vendor.SellRate;
                    vendorBuyRate = e.Vendor.BuyRate;
                    vendorOpened = DateTime.UtcNow;
                    var vendorInfo = $"{Globals.Core.WorldFilter[e.Vendor.MerchantId].Name}[0x{e.Vendor.MerchantId:X8}]: BuyRate: {e.Vendor.BuyRate * 100:n0}% SellRate: {e.Vendor.SellRate * 100:n0}% MaxValue: {e.Vendor.MaxValue:n0}";
                    if (Globals.Settings.AutoVendor.ShowMerchantInfo)
                        Util.WriteToChat(vendorInfo);
                    else
                        Logger.Debug(vendorInfo);
                }

                if (Globals.Settings.AutoVendor.Enabled == false)
                    return;

                if (!isRunning)
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
                Util.ThinkOrWrite("AutoVendor Fatal - insufficient pack space", Globals.Settings.AutoVendor.Think);
                Stop();
                return;
            }

            var hasLootCore = false;
            if (lootProfile == null) {
                try {
                    lootProfile = new VTClassic.LootCore();
                    hasLootCore = true;
                } catch (Exception ex) { Logger.LogException(ex); }

                if (!hasLootCore) {
                    Util.WriteToChat("Unable to load VTClassic, something went wrong.");
                    if (Globals.Settings.AutoVendor.Enabled) {
                        Util.WriteToChat("AutoVendor Disabled");
                        Globals.Settings.AutoVendor.Enabled = false;
                    }
                    return;
                }
            }

            if (merchantId == 0)
                merchantId = vendorId;

            if (merchantId == 0 || Globals.Core.WorldFilter[merchantId] == null) {
                Util.ThinkOrWrite("AutoVendor Fatal - no vendor, cannot start", Globals.Settings.AutoVendor.Think);
                return;
            }

            if (isRunning)
                Stop(true);

            var profilePath = GetProfilePath(string.IsNullOrEmpty(useProfilePath) ? (Globals.Core.WorldFilter[merchantId].Name + ".utl") : useProfilePath);

            if (!File.Exists(profilePath)) {
                Logger.Debug("No vendor profile exists: " + profilePath);
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

            bailTimer = DateTime.Now;
            isRunning = true;
            needsToBuy = needsToSell = false;
            lastThought = DateTime.UtcNow;
            lastIdCount = int.MaxValue;
            VTankControl.Item_Block(30000, false);
            VTankControl.Nav_Block(30000, Globals.Settings.Plugin.Debug);
            Globals.Core.EchoFilter.ServerDispatch += EchoFilter_ServerDispatch;
            Globals.Core.EchoFilter.ClientDispatch += EchoFilter_ClientDispatch;
        }

        public void Stop(bool silent = false) {
            if (!isRunning)
                return;
            if (!silent)
                Util.ThinkOrWrite("AutoVendor finished: " + vendorName, Globals.Settings.AutoVendor.Think);

            Globals.InventoryManager.Resume();
            isRunning = needsToBuy = needsToSell = false;
            vendorId = 0;

            Globals.Core.EchoFilter.ServerDispatch -= EchoFilter_ServerDispatch;
            Globals.Core.EchoFilter.ClientDispatch -= EchoFilter_ClientDispatch;
            if (lootProfile != null) ((VTClassic.LootCore)lootProfile).UnloadProfile();
            VTankControl.Nav_UnBlock();
            VTankControl.Item_UnBlock();
        }

        public void Think() {
            //if Merchant info is displayed, does not need vendoring, vendorId is set, and it's been over 5 minutes since the vendor was opened; reset vendorId.
            if (Globals.Settings.AutoVendor.ShowMerchantInfo && !isRunning && vendorId > 0 && DateTime.UtcNow - vendorOpened > TimeSpan.FromMinutes(5)) {
                vendorId = 0;
                vendorOpened = DateTime.MinValue;
            }
            if (!isRunning)
                return;

            try {
                if (Globals.Core.Actions.BusyState == 0 && DateTime.UtcNow - lastThought >= TimeSpan.FromMilliseconds(250)) {
                    lastThought = DateTime.UtcNow;

                    //if autovendor is running, and nav block has less than a second plus thinkInterval remaining, refresh it
                    if (VTankControl.navBlockedUntil < DateTime.UtcNow + TimeSpan.FromMilliseconds(1250)) {
                        VTankControl.Item_Block(30000, false);
                        VTankControl.Nav_Block(30000, Globals.Settings.Plugin.Debug);
                    }

                    if (waitingForIds) {
                        if (Globals.Assessor.NeedsInventoryData(itemsToId)) {
                            if (DateTime.UtcNow - lastIdSpam > TimeSpan.FromSeconds(15)) {
                                lastIdSpam = DateTime.UtcNow;
                                var thisIdCount = Globals.Assessor.GetNeededIdCount(itemsToId);
                                Logger.Debug(string.Format("AutoVendor waiting to id {0} items, this will take approximately {0} seconds.", thisIdCount));
                                if (lastIdCount != thisIdCount) { // if count has changed, reset bail timer
                                    lastIdCount = thisIdCount;
                                    bailTimer = DateTime.UtcNow;
                                }
                            }
                            return;
                        } else
                            waitingForIds = false;
                    }

                    if (Globals.Settings.AutoVendor.TestMode) {
                        DoTestMode();
                        Stop();
                        return;
                    }

                    if (DateTime.UtcNow - lastEvent >= TimeSpan.FromMilliseconds(15000)) {
                        if (lastEvent != DateTime.MinValue) // minvalue was not set, so it expired naturally:
                            Logger.Debug($"Event Timeout. Pyreals: {expectedPyreals:n0}, Sell List: {pendingSell.Count():n0}, Buy List: {pendingBuy.Count():n0}");
                        if (HasVendorOpen()) {
                            if (needsToBuy) {
                                needsToBuy = false;
                                Globals.Core.Actions.VendorBuyAll();
                            } else if (needsToSell) {
                                needsToSell = false;
                                Globals.Core.Actions.VendorSellAll();
                            } else {
                                DoVendoring();
                            }
                        }
                        // vendor closed?
                        else
                            Stop();
                    }
                }
                if (DateTime.Now - bailTimer > TimeSpan.FromSeconds(60)) {
                    Util.WriteToChat("AutoVendor bail, Timeout expired");
                    Stop();
                }
            } catch (Exception ex) { Logger.LogException(ex); }
        }

        private void DoVendoring() {
            try {
                List<BuyItem> buyItems = GetBuyItems();
                List<WorldObject> sellItems = GetSellItems();

                Globals.Core.Actions.VendorClearBuyList();
                Reset_pendingBuy();
                Globals.Core.Actions.VendorClearSellList();
                Reset_pendingSell();
                expectedPyreals = 0;

                var totalBuyCount = 0;
                var totalBuyPyreals = 0;
                var totalBuySlots = 0;
                var freeSlots = Util.GetFreeMainPackSpace();
                var pyrealCount = Util.PyrealCount();
                StringBuilder buyAdded = new StringBuilder("Autovendor Buy List: ");
                foreach (var buyItem in buyItems.OrderBy(o => o.Item.Value).ToList()) {
                    var vendorPrice = GetVendorSellPrice(buyItem.Item);
                    if (vendorPrice == 0) {
                        Util.ThinkOrWrite($"AutoVendor Fatal - No vendor price found while adding {buyItem.Amount:n0}x {buyItem.Item.Name}[0x{buyItem.Item.Id:X8}] Value: {vendorPrice:n}", Globals.Settings.AutoVendor.Think);
                        Stop();
                        return;
                    }

                    if ((totalBuyPyreals + vendorPrice) <= pyrealCount && (freeSlots - totalBuySlots) > 1) {
                        int buyCount = (int)((pyrealCount - totalBuyPyreals) / vendorPrice);
                        if (buyCount > buyItem.Amount)
                            buyCount = buyItem.Amount;
                        if (buyCount > MAX_VENDOR_BUY_COUNT)
                            buyCount = MAX_VENDOR_BUY_COUNT;
                        if (freeSlots < (int)Math.Ceiling((float)buyCount / buyItem.Item.StackMax) + totalBuySlots) {
                            buyCount = buyItem.Item.StackMax * ((freeSlots - 2) - totalBuySlots);
                            // Logger.Debug(string.Format("AutoVendor Limiting buy of {0} to {1:n0} due to pack limited pack slots {2:n0}", buyItem.Item.Name, buyCount, (freeSlots - totalBuySlots)));
                        }
                        if (buyCount == 0)
                            break;

                        pendingBuy[buyItem.Item.Type] = buyCount;
                        Globals.Core.Actions.VendorAddBuyList(buyItem.Item.Id, buyCount);
                        totalBuySlots += (int)Math.Ceiling((float)buyCount / buyItem.Item.StackMax);
                        totalBuyPyreals += (int)Math.Ceiling((vendorPrice * buyCount - 0.1));
                        totalBuyCount++;

                        if (totalBuyCount > 1)
                            buyAdded.Append(",");
                        buyAdded.Append($"{buyCount} {buyItem.Item.Name}");

                        if (buyItem.Amount > buyCount)
                            break;
                    } else if (totalBuyCount > 0)
                        break;
                }

                if (totalBuyCount > 0) {
                    Logger.Debug($"{buyAdded.ToString()} - {totalBuyPyreals}/{pyrealCount}");
                    needsToBuy = true;
                    expectedPyreals = -totalBuyPyreals;
                    return;
                }

                VendorItem nextBuyItem = null;

                if (buyItems.Count > 0)
                    nextBuyItem = buyItems[0].Item;

                int totalSellValue = 0;
                int sellItemCount = 0;

                StringBuilder sellAdded = new StringBuilder("Autovendor Sell List: ");
                while (sellItemCount < sellItems.Count && sellItemCount < 99) { // GDLE limits transactions to 99 items. (less than 100)
                    var item = sellItems[sellItemCount];
                    var value = GetVendorBuyPrice(item);
                    var stackSize = item.Values(LongValueKey.StackCount, 1);
                    bool nestedBreak = false;

                    // dont sell notes if we are trying to buy notes...
                    if (((nextBuyItem != null && nextBuyItem.ObjectClass == ObjectClass.TradeNote) || nextBuyItem == null) && item.ObjectClass == ObjectClass.TradeNote) {
                        Logger.Debug($"AutoVendor bail: buyItem: {(nextBuyItem == null ? "null" : nextBuyItem.Name)} sellItem: {Util.GetObjectName(item.Id)}");
                        break;
                    }

                    // if we are selling notes to buy something, sell the minimum amount
                    if (nextBuyItem != null && item.ObjectClass == ObjectClass.TradeNote) {
                        if (sellItemCount > 0) break; // if we already have items to sell, sell those first
                        if (!PyrealsWillFitInMainPack(value)) {
                            Util.ThinkOrWrite($"AutoVendor Fatal - No inventory room to sell {Util.GetObjectName(item.Id)}", Globals.Settings.AutoVendor.Think);
                            Stop();
                            return;
                        }

                        // see if we already have a single stack of this item
                        foreach (var wo in Globals.Core.WorldFilter.GetInventory()) {
                            if (wo.Name == item.Name && wo.Values(LongValueKey.StackCount, 0) == 1) {
                                Logger.Debug($"AutoVendor Selling single {Util.GetObjectName(wo.Id)} so we can afford to buy: " + nextBuyItem.Name);
                                needsToSell = true;
                                if (sellItemCount > 0)
                                    sellAdded.Append(", ");
                                sellAdded.Append(Util.GetObjectName(wo.Id));
                                pendingSell.Add(wo.Id);
                                Globals.Core.Actions.VendorAddSellList(wo.Id);
                                totalSellValue += (int)(value);
                                sellItemCount++;
                                nestedBreak = true;
                                break;
                            }
                        }
                        if (nestedBreak) break;
                        Globals.Core.Actions.SelectItem(item.Id);
                        Globals.Core.Actions.SelectedStackCount = 1;
                        Globals.Core.Actions.MoveItem(item.Id, Globals.Core.CharacterFilter.Id, 0, false);
                        Logger.Debug($"AutoVendor Splitting {Util.GetObjectName(item.Id)}. old: {item.Values(LongValueKey.StackCount)} new: 1");
                        return;
                    }

                    // cant sell the whole stack? split it into what we can sell
                    if (!PyrealsWillFitInMainPack(totalSellValue + (int)(value * stackSize + 0.1))) {
                        if (sellItemCount < 1) {
                            // the whole stack won't fit, and there is nothing else in the sell list.
                            if (item.Values(LongValueKey.StackCount, 1) > 1) { // good news- this is a stack. try lobbing one off, and running again!
                                Globals.Core.Actions.SelectItem(item.Id);
                                Globals.Core.Actions.SelectedStackCount = 1;
                                Globals.Core.Actions.MoveItem(item.Id, Globals.Core.CharacterFilter.Id, 0, false);
                                Logger.Debug($"AutoVendor Splitting {Util.GetObjectName(item.Id)}. old: {item.Values(LongValueKey.StackCount)} new: 1");
                                return;
                            }
                            Util.ThinkOrWrite($"AutoVendor Fatal - No inventory room to sell {Util.GetObjectName(item.Id)}", Globals.Settings.AutoVendor.Think);
                            Stop();
                            return;
                        }
                        break;
                    }

                    if (sellItemCount > 0)
                        sellAdded.Append(", ");
                    sellAdded.Append(Util.GetObjectName(item.Id));

                    pendingSell.Add(item.Id);
                    Globals.Core.Actions.VendorAddSellList(item.Id);

                    totalSellValue += (int)(value * stackSize);
                    ++sellItemCount;
                }

                if (sellItemCount > 0) {
                    Logger.Debug(sellAdded.ToString() + " - " + totalSellValue);
                    expectedPyreals = totalSellValue;
                    needsToSell = true;
                    return;
                }
                Stop();
            } catch (Exception ex) { Logger.LogException(ex); }
        }

        private float GetVendorSellPrice(VendorItem item) {
            return (float)((int)(item.Value / item.StackCount) * (item.ObjectClass == ObjectClass.TradeNote ? 1.15f : vendorSellRate));
        }

        private float GetVendorBuyPrice(WorldObject wo) {
            return (float)((int)(wo.Values(LongValueKey.Value, 0) / wo.Values(LongValueKey.StackCount, 1)) * (wo.ObjectClass == ObjectClass.TradeNote ? 1f : vendorBuyRate));
        }

        private bool PyrealsWillFitInMainPack(float amount) { // fixed for ACE's bad pyreal management.
            return ((Util.GetFreeMainPackSpace() - Math.Ceiling(amount / PYREAL_STACK_SIZE)) > 0); // ensures 1 pack slot always remains free.
        }

        private List<BuyItem> GetBuyItems() {
            VendorInfo vendor = VendorCache.GetVendor(vendorId);
            List<BuyItem> buyItems = new List<BuyItem>();

            if (vendor == null) return buyItems;
            foreach (VendorItem item in vendor.Items.Values) {
                uTank2.LootPlugins.GameItemInfo itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithVendorObjectTemplateID(item.Id);
                if (itemInfo == null)
                    continue;
                uTank2.LootPlugins.LootAction result = ((VTClassic.LootCore)lootProfile).GetLootDecision(itemInfo);
                if (!result.IsKeepUpTo)
                    continue;
                if (result.Data1 > Util.GetItemCountInInventoryByName(item.Name))
                    buyItems.Add(new BuyItem(item, result.Data1 - Util.GetItemCountInInventoryByName(item.Name)));
            }

            foreach (VendorItem item in vendor.Items.Values) {
                uTank2.LootPlugins.GameItemInfo itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithVendorObjectTemplateID(item.Id);
                if (itemInfo == null) continue;
                uTank2.LootPlugins.LootAction result = ((VTClassic.LootCore)lootProfile).GetLootDecision(itemInfo);
                if (!result.IsKeep) continue;
                buyItems.Add(new BuyItem(item, int.MaxValue));
            }
            return buyItems;
        }

        private List<WorldObject> GetSellItems() {
            List<WorldObject> sellObjects = new List<WorldObject>();
            var vendor = VendorCache.GetVendor(vendorId);

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
            if (wo.Values(LongValueKey.Value, 0) <= 0) return false; // skip 0 value
            if (wo.Values(BoolValueKey.CanBeSold, true) == false) return false; // sellable?
            if (Globals.Settings.AutoVendor.OnlyFromMainPack == true && wo.Container != Globals.Core.CharacterFilter.Id)
                return false; // bail if we are only selling from main pack and this isnt in there
            return Util.IsItemSafeToGetRidOf(wo);
        }

        private void DoTestMode() {
            Util.WriteToChat("Buy Items:");
            foreach (BuyItem bi in GetBuyItems()) {
                uTank2.LootPlugins.GameItemInfo itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithVendorObjectTemplateID(bi.Item.Id);
                if (itemInfo == null) continue;
                uTank2.LootPlugins.LootAction result = ((VTClassic.LootCore)lootProfile).GetLootDecision(itemInfo);
                if (result.IsKeepUpTo || result.IsKeep) {
                    Util.WriteToChat($"  {bi.Item.Name} * {(bi.Amount == int.MaxValue ? "∞" : bi.Amount.ToString())} - {result.RuleName}");
                }
            }

            Util.WriteToChat("Sell Items:");
            foreach (WorldObject wo in GetSellItems()) {
                uTank2.LootPlugins.GameItemInfo itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithID(wo.Id);
                if (itemInfo == null) continue;
                uTank2.LootPlugins.LootAction result = ((VTClassic.LootCore)lootProfile).GetLootDecision(itemInfo);
                if (result.IsSell) {
                    Util.WriteToChat($"  {Util.GetObjectName(wo.Id)} - {result.RuleName}");
                }
            }
        }

        public bool HasVendorOpen() {
            try {
                if (Globals.Core.Actions.VendorId != 0) return true;
            } catch (Exception ex) { Logger.LogException(ex); }
            return false;
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
                    if (isRunning) {
                        Globals.Core.EchoFilter.ServerDispatch -= EchoFilter_ServerDispatch;
                        Globals.Core.EchoFilter.ClientDispatch -= EchoFilter_ClientDispatch;
                    }
                }
                disposed = true;
            }
        }
    }
}
