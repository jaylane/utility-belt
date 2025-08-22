using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Decal.Adapter;
using Decal.Adapter.Wrappers;
using UBHelper;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Settings;
using UtilityBelt.Lib.VendorCache;
using UtilityBelt.Views;
using VirindiViewService.Controls;
using UtilityBelt.Service.Lib.Settings;

namespace UtilityBelt.Tools {
    [Name("AutoVendor")]
    [Summary("Automatically buys/sells items at vendors based on loot profiles.")]
    [FullDescription(@"
> [!CAUTION]
> It is **highly** suggested to enable test mode with `/ub opt set AutoVendor.TestMode true` before actually running, this can **sell all of your stuff**.

Kind of like [mag-tools auto buy/sell](https://github.com/Mag-nus/Mag-Plugins/wiki/Mag%E2%80%90Tools-Misc#vendor-auto-buysell-on-open-trade). It's less forgiving, and can sell just about anything. It will load the first profile it finds when you open a vendor in this order:

```
Documents\Decal Plugins\UtilityBelt\<server>\<char>\autovendor\Vendor Name.utl
Documents\Decal Plugins\UtilityBelt\autovendor\Vendor Name.utl
Documents\Decal Plugins\UtilityBelt\<server>\<char>\default.utl
Documents\Decal Plugins\UtilityBelt\autovendor\default.utl
```

#### How to use

* Create a virindi tank loot profile with some keep rules, here is an example for [Aun Amanaualuan the Elder Shaman](/utl/Aun Amanaualuan the Elder Shaman.utl). (replace underscores with spaces after downloading)
* Drop the profile in `%USERPROFILE%\Documents\Decal Plugins\UtilityBelt\autovendor\`
* Make sure UB AutoVendor option is enabled in the decal plugin window
* I **highly** suggest enabling test mode before actually running, this can **sell all of your stuff** so please don't blame me.
* Next time you open Aun Amanaualuan the Elder Shaman, ub will auto buy components and sell peas.
* If a profile does not exist, nothing will be bought/sold.

#### Info

* Supported rule actions: Keep, Keep #, and Sell
  - Keep (buy as many as this item as it can afford.  wont sell notes to buy other notes).
  - Keep # (buy # of this item)
  - Sell (sell all of these)
* Red loot rules are supported (the ones that need id data)
* Allows default and character override profiles, see directory info at the top of this page
* Can show vendor sell / buy rates when approaching vendor, see config option on AutoVendor tab.
* Will buy things even if you don't have trade notes (assuming you have enough pyreals).
* Can optionally think to yourself when finished, for better meta integration
* Things that it *wont* sell:
  - Currently equipped items
  - Currently wielded items
  - Inscribed items
  - Retained items
  - Tinkered/Imbued items
  - Items with zero value

#### Example Profiles

| Vendor UTL | Description |
|------------|-------------|
| [Tunlok Weapons Master.utl](../../utl/Tunlok%20Weapons%20Master.utl) | Sells all salvage except Granite |
| [Aun Amanaualuan the Elder Shaman.utl](../../utl/Aun%20Amanaualuan%20the%20Elder%20Shaman.utl) | Buys components /portal gems. Sells peas. |
| [Thimrin Woodsetter.utl](../../utl/Thimrin%20Woodsetter.utl) | Buys supplies based on character's current skill level (healing kits, cooking pot, rations) |

    ")]
    public class AutoVendor : ToolBase {
        private const int MAX_VENDOR_BUY_COUNT = 5000;
        // this defaults to 25000, but will be updated with the value read from pyreals.MaxStackSize as available
        // to support custom content servers
        private int PYREAL_STACK_SIZE = 25000;
        private bool hasUpdatedPyrealStackSize = false;
        private bool isWaitingForPyrealStackSize = false;
        private DateTime lastEvent = DateTime.MinValue;
        private DateTime vendorOpened = DateTime.MinValue;
        private DateTime bailTimer = DateTime.MinValue;

        private bool disposed;
        private bool isRunning = false;
        private VTClassic.LootCore lootProfile;
        private bool needsToBuy = false;
        private bool needsToSell = false;
        private int vendorId = 0;
        private string vendorName = "";
        private float vendorSellRate = 1;
        private float vendorBuyRate = 1;
        private Weenie.ITEM_TYPE vendorCategories;
        private bool waitingForAutoStackCram = false;
        private bool waitingForSplit = false;
        private DateTime lastSplit;
        private ActionQueue.Item splitQueue;
        private bool shouldAutoStack = false;
        private bool shouldAutoCram = false;
        private bool showMerchantInfoCooldown = false;
        private List<int> itemsToId = new List<int>();

        // Sell batching state tracking
        private DateTime lastSellTransactionTime = DateTime.MinValue;
        private bool waitingForSellDelay = false;
        private List<WorldObject> remainingSellItems = new List<WorldObject>();
        private bool processingSellBatches = false;
        private bool needsDelayAfterSell = false; // Flag to set delay after sell completes
        
        // Dynamic timeout calculation for batching
        private DateTime dynamicBailTimeout = DateTime.MinValue;
        private TimeSpan lastDelayStartTime = TimeSpan.Zero;
        private int totalBatchCount = 0;
        private int currentBatchNumber = 0;

        private int expectedPyreals = 0;
        private readonly Dictionary<int, int> myPyreals = new Dictionary<int, int>();
        private readonly Dictionary<int, int> pendingBuy = new Dictionary<int, int>();
        private readonly List<int> pendingSell = new List<int>();

        internal readonly Dictionary<int, string> ShopItemListTypes = new Dictionary<int, string> { { 0x00000001, "Weapons" }, { 0x00000002, "Armor" }, { 0x00000004, "Clothing" }, { 0x00000008, "Jewelry" }, { 0x00000010, "Miscellaneous" }, { 0x00000020, "Food" }, { 0x00000080, "Miscellaneous" }, { 0x00000100, "Weapons" }, { 0x00000200, "Containers" }, { 0x00000400, "Miscellaneous" }, { 0x00000800, "Gems" }, { 0x00001000, "Spell Components" }, { 0x00002000, "Books, Paper" }, { 0x00004000, "Keys, Tools" }, { 0x00008000, "Magic Items" }, { 0x00040000, "Trade Notes" }, { 0x00080000, "Mana Stones" }, { 0x00100000, "Services" }, { 0x00400000, "Cooking Items" }, { 0x00800000, "Alchemical Items" }, { 0x01000000, "Fletching Items" }, { 0x04000000, "Alchemical Items" }, { 0x08000000, "Fletching Items" }, { 0x20000000, "Keys, Tools" } };

        #region Config
        [Summary("Enabled")]
        [Hotkey("AutoVendor", "Toggle AutoVendor functionality")]
        public readonly Setting<bool> Enabled = new Setting<bool>(true);

        [Summary("EnableBuying")]
        public readonly Setting<bool> EnableBuying = new Setting<bool>(true);

        [Summary("EnableSelling")]
        public readonly Setting<bool> EnableSelling = new Setting<bool>(true);

        [Summary("Test mode (don't actually sell/buy, just echo to the chat window)")]
        [Hotkey("AutoVendorTestMode", "Toggle AutoVendor TestMode functionality")]
        public readonly Setting<bool> TestMode = new Setting<bool>(false);

        [Summary("Think to yourself when auto vendor is completed")]
        public readonly Setting<bool> Think = new Setting<bool>(false);

        [Summary("Show merchant info on approach vendor")]
        public readonly Setting<bool> ShowMerchantInfo = new Setting<bool>(true);

        [Summary("Only vendor things in your main pack")]
        public readonly Setting<bool> OnlyFromMainPack = new Setting<bool>(false);

        [Summary("Attempts to open vendor on /ub vendor open[p]")]
        public readonly Setting<int> Tries = new Setting<int>(4);

        [Summary("Tine between open vendor attempts (in milliseconds)")]
        public readonly Setting<int> TriesTime = new Setting<int>(5000);

        [Summary("Maximum items to sell per transaction")]
        public readonly Setting<int> MaxItemsPerTransaction = new Setting<int>(99);

        [Summary("Timeout between sale transactions in seconds")]
        public readonly Setting<int> TimeoutBetweenTransactionsSeconds = new Setting<int>(0);
        #endregion

        #region Commands
        #region /ub autovendor <lootProfile>
        [Summary("Auto buy/sell from vendors.")]
        [Usage("/ub autovendor <cancel|lootProfile>")]
        [Example("/ub autovendor", "Loads VendorName.utl and starts the AutoVendor process.")]
        [Example("/ub autovendor cancel", "Cancels the current autovendor session.")]
        [Example("/ub autovendor recomp.utl", "Loads recomp.utl and starts the AutoVendor process.")]
        [CommandPattern("autovendor", @"^ *(?<LootProfile>.*)$")]
        public void DoAutoVendor(string _, Match args) {
            switch (args.Groups["LootProfile"].Value) {
                case "cancel":
                case "stop":
                case "quit":
                    Stop();
                    break;
                default:
                    Start(0, args.Groups["LootProfile"].Value);
                    break;
            }
        }
        #endregion
        #region /ub vendor {open[p] [vendorname,vendorid,vendorhex],opencancel,buyall,sellall,clearbuy,clearsell}
        [Summary("Vendor commands, with build in VTank pausing.")]
        [Usage("/ub vendor {open[p] <vendorname,vendorid,vendorhex> | buyall | sellall | clearbuy | clearsell | opencancel | addbuy[p] <item> | addsell[p] <item>}")]
        [Example("/ub vendor open Tunlok Weapons Master", "Opens vendor with name \"Tunlok Weapons Master\"")]
        [Example("/ub vendor opencancel", "Quietly cancels the last /ub vendor open* command")]
        [Example("/ub vendor addbuy 10 Mana Scarab", "Adds 10 Mana Scarabs to the buy list. use /ub vendor buyall to actually buy")]
        [CommandPattern("vendor", @"^ *(?<params>(openp? ?.+|buy(all)?|sell(all)?|clearbuy|clearsell|opencancel|addsellp? .+|addbuyp? .+)) *$")]
        public void DoVendor(string _, Match args) {
            UB_vendor(args.Groups["params"].Value);
        }
        private DateTime vendorTimestamp = DateTime.MinValue;
        private int vendorOpening = 0;
        private static WorldObject vendor = null;
        private bool hasLootCore;
        private int sellListSplitItemId;
        private int sellListItemsNeeded;
        private int lastSellListItemCount;

        public void UB_vendor(string parameters) {
            char[] stringSplit = { ' ' };
            string[] parameter = parameters.Split(stringSplit, 2);
            if (parameter.Length == 0) {
                Logger.Error("Usage: /ub vendor {open[p] [vendorname,vendorid,vendorhex],opencancel,buyall,sellall,clearbuy,clearsell,addsell[p] [count] itemname,addbuy[p] [count] itemname}");
                return;
            }
            var partial = parameter[0].EndsWith("p");
            VendorInfo cachedVendor = VendorCache.GetVendor(vendorId);

            switch (parameter[0]) {
                case "buy":
                case "buyall":
                    CoreManager.Current.Actions.VendorBuyAll();
                    break;

                case "sell":
                case "sellall":
                    CoreManager.Current.Actions.VendorSellAll();
                    break;

                case "clearbuy":
                    CoreManager.Current.Actions.VendorClearBuyList();
                    break;

                case "clearsell":
                    CoreManager.Current.Actions.VendorClearSellList();
                    break;

                case "open":
                    if (parameter.Length != 2)
                        UB_vendor_open("", true);
                    else
                        UB_vendor_open(parameter[1], false);
                    break;

                case "openp":
                    if (parameter.Length != 2)
                        UB_vendor_open("", true);
                    else
                        UB_vendor_open(parameter[1], true);
                    break;
                case "opencancel":
                    UB.Core.Actions.FaceHeading(UB.Core.Actions.Heading - 1, true);
                    vendor = null;
                    vendorOpening = 0;
                    UBHelper.vTank.Decision_UnLock(uTank2.ActionLockType.Navigation);
                    break;
                case "addbuy":
                case "addbuyp":
                    var buyItemName = string.Join(" ", parameter.Skip(1).ToArray());
                    var buyParts = parameter[1].Split(' ');
                    if (int.TryParse(buyParts[0], out int parsedCount)) {
                        buyItemName = string.Join(" ", buyParts.Skip(1).ToArray());
                    }
                    else {
                        parsedCount = 1;
                    }

                    Logger.WriteToChat($"Name: {buyItemName} count: {parsedCount} param:{parameter[1]}");

                    if (UBHelper.Vendor.Id == 0 || cachedVendor == null) {
                        Logger.Error("addbuy: No vendor open");
                        return;
                    }

                    foreach (var item in cachedVendor.Items) {
                        if (partial && item.Value.Name.ToLower().Contains(buyItemName.ToLower())) {
                            AddItemToBuyList(item.Value, parsedCount);
                            return;
                        }
                        else if (!partial && item.Value.Name.ToLower().Equals(buyItemName.ToLower())) {
                            AddItemToBuyList(item.Value, parsedCount);
                            return;
                        }
                    }
                    Logger.Error($"addbuy: Unable to find item {(partial ? "partially " : "")}named '{buyItemName}' in vendor sell list");
                    break;
                case "addsell":
                case "addsellp":
                    var sellItemName = string.Join(" ", parameter.Skip(1).ToArray());
                    var sellParts = parameter[1].Split(' ');
                    if (int.TryParse(sellParts[0], out int parsedSellCount)) {
                        sellItemName = string.Join(" ", sellParts.Skip(1).ToArray());
                    }
                    else {
                        parsedSellCount = 1;
                    }

                    if (UBHelper.Vendor.Id == 0 || cachedVendor == null) {
                        Logger.Error("addsell: No vendor open");
                        return;
                    }
                    var inv = new List<int>();
                    var foundItems = new List<Weenie>();
                    UBHelper.InventoryManager.GetInventory(ref inv, UBHelper.InventoryManager.GetInventoryType.AllItems);
                    foreach (var id in inv) {
                        var weenie = new Weenie(id);
                        var wo = UB.Core.WorldFilter[id];
                        if (weenie == null || wo == null)
                            continue;
                        var name = weenie.GetName(NameType.NAME_SINGULAR).ToLower();
                        if (partial && name.Contains(sellItemName.ToLower()) && ItemIsSafeToGetRidOf(wo)) {
                            foundItems.Add(weenie);
                        }
                        else if (!partial && name.Equals(sellItemName.ToLower()) && ItemIsSafeToGetRidOf(wo)) {
                            foundItems.Add(weenie);
                        }
                    }

                    if (foundItems.Count > 0)
                        AddItemToSellList(foundItems, parsedSellCount);
                    else
                        Logger.Error($"addsell: Unable to find item {(partial ? "partially " : "")}named '{sellItemName}' in inventory");
                    break;
            }
        }

        private void AddItemToSellList(List<Weenie> weenies, int count) {
            // see if we can fulfil count with current stacks..
            lastSellListItemCount = count;
            var itemsNeeded = count;
            var sellList = new List<int>();
            Weenie oversized = null;
            foreach (var weenie in weenies) {
                var stackCount = weenie.StackCount == 0 ? 1 : weenie.StackCount;
                if (stackCount <= itemsNeeded) {
                    UB.Core.Actions.VendorAddSellList(weenie.Id);
                    sellList.Add(weenie.Id);
                    itemsNeeded -= stackCount;
                }
                else if (oversized == null || oversized.StackCount > stackCount)
                    oversized = weenie;
                if (itemsNeeded == 0)
                    break;
            }

            if (itemsNeeded == 0) {
                Logger.WriteToChat($"Added item to sell list: {weenies.First().GetName(NameType.NAME_SINGULAR)} * {count}");
            }
            else if (itemsNeeded > 0 && oversized == null) {
                Logger.WriteToChat($"Added item to sell list: {weenies.First().GetName(NameType.NAME_SINGULAR)} * {count}, but was missing {itemsNeeded} items 22");
            }
            else {
                // we need items and have an item with at least that many
                UBHelper.vTank.Decision_Lock(uTank2.ActionLockType.ItemUse, TimeSpan.FromMilliseconds(1000));
                DoSplit(UB.Core.WorldFilter[oversized.Id], itemsNeeded);
                sellListSplitItemId = oversized.Id;
                sellListItemsNeeded = itemsNeeded;
                UB.Core.EchoFilter.ServerDispatch += EchoFilter_ServerDispatch;
                UB.Core.RenderFrame += Core_RenderFrame_SellListSplit;
            }
        }

        private void Core_RenderFrame_SellListSplit(object sender, EventArgs e) {
            if (!waitingForSplit) {
                var splitWeenie = new UBHelper.Weenie(sellListSplitItemId);
                var inv = new List<int>();
                bool foundItem = false;
                UBHelper.InventoryManager.GetInventory(ref inv, UBHelper.InventoryManager.GetInventoryType.AllItems);
                foreach (var id in inv) {
                    var weenie = new Weenie(id);
                    if (weenie.GetName(NameType.NAME_SINGULAR).ToLower() == splitWeenie.GetName(NameType.NAME_SINGULAR).ToLower() && sellListItemsNeeded == weenie.StackCount) {
                        foundItem = true;
                        UB.Core.Actions.VendorAddSellList(weenie.Id);
                        Logger.WriteToChat($"Added item to sell list: {weenie.GetName(NameType.NAME_SINGULAR)} * {lastSellListItemCount}");
                        break;
                    }
                }

                if (!foundItem)
                    Logger.WriteToChat($"Added item to sell list: {splitWeenie.GetName(NameType.NAME_SINGULAR)} * {lastSellListItemCount - sellListItemsNeeded}, but was missing {sellListItemsNeeded} items");
                UB.Core.EchoFilter.ServerDispatch -= EchoFilter_ServerDispatch;
                UB.Core.RenderFrame -= Core_RenderFrame_SellListSplit;
            }
        }

        private void AddItemToBuyList(VendorItem item, int count) {
            UB.Core.Actions.VendorAddBuyList(item.Id, count);
            Logger.WriteToChat($"Added item to buy list: {item.Name} * {count}");
        }

        public void UB_vendor_open(string vendorname, bool partial) {
            vendor = Util.FindName(vendorname, partial, new ObjectClass[] { ObjectClass.Vendor });
            if (vendor != null) {
                OpenVendor();
                return;
            }
            Util.ThinkOrWrite("AutoVendor failed to open vendor", UB.AutoVendor.Think);
        }
        private void OpenVendor() {
            UBHelper.vTank.Decision_Lock(uTank2.ActionLockType.Navigation, TimeSpan.FromMilliseconds(500 + UB.AutoVendor.TriesTime));
            vendorOpening = 1;
            UB.Core.RenderFrame += Core_RenderFrame_OpenVendor;
            vendorTimestamp = DateTime.UtcNow - TimeSpan.FromMilliseconds(UB.AutoVendor.TriesTime - 250); // fudge timestamp so next think hits in 500ms
            UB.Core.Actions.SetAutorun(false);
            LogDebug("Attempting to open vendor " + vendor.Name);

        }
        public void Core_RenderFrame_OpenVendor(object sender, EventArgs e) {
            try {
                if (DateTime.UtcNow - vendorTimestamp > TimeSpan.FromMilliseconds(UB.AutoVendor.TriesTime)) {

                    if (vendorOpening <= UB.AutoVendor.Tries) {
                        if (vendorOpening > 1)
                            Logger.Debug("Vendor Open Timed out, trying again");

                        UBHelper.vTank.Decision_Lock(uTank2.ActionLockType.Navigation, TimeSpan.FromMilliseconds(500 + UB.AutoVendor.TriesTime));
                        vendorOpening++;
                        vendorTimestamp = DateTime.UtcNow;
                        CoreManager.Current.Actions.UseItem(vendor.Id, 0);
                    }
                    else {
                        UB.Core.Actions.FaceHeading(UB.Core.Actions.Heading - 1, true); // Cancel the previous useitem call (don't ask)
                        Util.ThinkOrWrite("AutoVendor failed to open vendor", UB.AutoVendor.Think);
                        vendor = null;
                        vendorOpening = 0;
                        UB.Core.RenderFrame -= Core_RenderFrame_OpenVendor;
                        UBHelper.vTank.Decision_UnLock(uTank2.ActionLockType.Navigation);
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        #endregion
        #endregion

        public AutoVendor(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        public override void Init() {
            base.Init();
            try {
                Directory.CreateDirectory(Path.Combine(Util.GetPluginDirectory(), "autovendor"));
                Directory.CreateDirectory(Path.Combine(Util.GetCharacterDirectory(), "autovendor"));
                Directory.CreateDirectory(Path.Combine(Util.GetServerDirectory(), "autovendor"));

                UB.Core.WorldFilter.ApproachVendor += WorldFilter_ApproachVendor;
            }
            catch (Exception ex) { Logger.LogException(ex); }

            if (UBHelper.Core.GameState != UBHelper.GameState.In_Game)
                UB.Core.CharacterFilter.LoginComplete += CharacterFilter_LoginComplete;
            else
                CheckPyrealMaxStack();
        }

        private void CharacterFilter_LoginComplete(object sender, EventArgs e) {
            try {
                CheckPyrealMaxStack();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void CheckPyrealMaxStack() {
            if (hasUpdatedPyrealStackSize) {
                isWaitingForPyrealStackSize = false;
                UB.Core.WorldFilter.CreateObject -= WorldFilter_CreateObject;
                return;
            }

            var inv = new List<int>();
            UBHelper.InventoryManager.GetInventory(ref inv, UBHelper.InventoryManager.GetInventoryType.AllItems);
            foreach (var item in inv) {
                var weenie = new UBHelper.Weenie(item);
                if (weenie.WCID == 273) { // pyreals
                    PYREAL_STACK_SIZE = weenie.StackMax;
                    hasUpdatedPyrealStackSize = true;
                    break;
                }
            }

            if (!hasUpdatedPyrealStackSize && !isWaitingForPyrealStackSize) {
                isWaitingForPyrealStackSize = true;
                UB.Core.WorldFilter.CreateObject += WorldFilter_CreateObject;
            }
        }

        private void WorldFilter_CreateObject(object sender, CreateObjectEventArgs e) {
            try {
                if (e.New.Values(LongValueKey.Type, 0) == 273/* pyreals */) {
                    CheckPyrealMaxStack();
                    UB.Core.WorldFilter.CreateObject -= WorldFilter_CreateObject;

                    if (!hasUpdatedPyrealStackSize) {
                        UBHelper.Core.RadarUpdate += Core_RadarUpdatePyrealStackCheck;
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Core_RadarUpdatePyrealStackCheck(double uptime) {
            try {
                CheckPyrealMaxStack();
                if (hasUpdatedPyrealStackSize)
                    UBHelper.Core.RadarUpdate -= Core_RadarUpdatePyrealStackCheck;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Reset_pendingBuy() {
            if (pendingBuy.Count > 0) {
                LogError($"pendingBuy reset while it still contained {pendingBuy.Count:n0} items!");
                foreach (KeyValuePair<int, int> fff in pendingBuy) {
                    LogDebug($"    Type 0x{fff.Key:X8} x{fff.Value:n0}");
                }
                List<int> inv = new List<int>();
                waitingForAutoStackCram = true;
                UBHelper.InventoryManager.GetInventory(ref inv, UBHelper.InventoryManager.GetInventoryType.AllItems, UBHelper.Weenie.INVENTORY_LOC.ALL_LOC);
                new Assessor.Job(UB.Assessor, ref inv, (_) => { bailTimer = DateTime.UtcNow; }, () => { waitingForAutoStackCram = false; }, false);
            }
            pendingBuy.Clear();
        }
        private void Reset_pendingSell() {
            if (pendingSell.Count > 0) {
                LogError($"pendingSell reset while it still contained {pendingSell.Count:n0} items!");


                foreach (int i in pendingSell) {
                    LogDebug($"    Item 0x{i:X8} {(UB.Core.WorldFilter[i] == null?"Error": UB.Core.WorldFilter[i].Name)}");
                }
                List<int> inv = new List<int>();
                waitingForAutoStackCram = true;
                UBHelper.InventoryManager.GetInventory(ref inv, UBHelper.InventoryManager.GetInventoryType.AllItems, UBHelper.Weenie.INVENTORY_LOC.ALL_LOC);
                new Assessor.Job(UB.Assessor, ref inv, (_) => { bailTimer = DateTime.UtcNow; }, () => { waitingForAutoStackCram = false; }, false);
            }
            pendingSell.Clear();
        }
        private void Reset_myPyreals() {
            myPyreals.Clear();
            List<int> inv = new List<int>();
            UBHelper.InventoryManager.GetInventory(ref inv, UBHelper.InventoryManager.GetInventoryType.AllItems);
            foreach (int i in inv) {
                UBHelper.Weenie w = new UBHelper.Weenie(i);
                if (w.WCID == 273)
                    myPyreals.Add(i, w.StackCount);
            }
        }
        private void CheckDone() {
            //Logger.WriteToChat($"CheckDone {((double)((DateTime.UtcNow.ToFileTimeUtc() - 116444736000000000) / 10) / 1000000):n6} expectedPyreals:{expectedPyreals} pendingSell({pendingSell.Count}):{string.Join(",",pendingSell.Select(x=>x.ToString("X8")).ToArray())} pendingBuy({pendingBuy.Count}):{string.Join(",", pendingBuy.Select(x=>x.Key.ToString("X8")+"*"+x.Value).ToArray())}");
            
            // Check if we need to set up a delay after sell completion
            if (needsDelayAfterSell && expectedPyreals < 50 && pendingSell.Count() == 0 && pendingBuy.Count() == 0) {
                needsDelayAfterSell = false;
                lastSellTransactionTime = DateTime.UtcNow;
                waitingForSellDelay = true;
                LogDebug($"Sell transaction complete, starting {TimeoutBetweenTransactionsSeconds} second delay before next batch");
                lastEvent = DateTime.UtcNow;
                RefreshBailTimer();
                return;
            }
            
            if (expectedPyreals < 50 && pendingSell.Count() == 0 && pendingBuy.Count() == 0) {
                lastEvent = DateTime.MinValue;
                shouldAutoStack = UB.InventoryManager.AutoStack;
                shouldAutoCram = UB.InventoryManager.AutoCram;
            }
            else
                lastEvent = DateTime.UtcNow;
            RefreshBailTimer();
        }

        private void RefreshBailTimer() {
            bailTimer = DateTime.UtcNow;
            // Set timeout for current batch only, not all remaining batches
            if (processingSellBatches && remainingSellItems.Count > 0) {
                dynamicBailTimeout = DateTime.UtcNow.AddSeconds(TimeoutBetweenTransactionsSeconds + 20); // Current batch timeout + 30s buffer
                LogDebug($"Dynamic timeout set for {TimeoutBetweenTransactionsSeconds + 20} seconds (single batch timeout)");
            } else {
                dynamicBailTimeout = DateTime.UtcNow.AddSeconds(120); // Standard timeout when not batching
            }
        }

        private bool IsTimedOut() {
            // Use dynamic timeout if we're in batch processing mode
            if (processingSellBatches && dynamicBailTimeout != DateTime.MinValue) {
                return DateTime.UtcNow > dynamicBailTimeout;
            }
            // Otherwise use standard 60-second timeout
            return DateTime.UtcNow - bailTimer > TimeSpan.FromSeconds(60);
        }
        private void EchoFilter_ClientDispatch(object sender, NetworkMessageEventArgs e) {
            try {
                // 0x005F: Vendor_Buy
                if (e.Message.Type == 0xF7B1 && (int)e.Message["action"] == 0x005F) {
                    //Logger.Debug("Server has Buy");
                    Reset_myPyreals();
                    lastEvent = DateTime.UtcNow;
                }
                // 0x0060: Vendor_Sell
                if (e.Message.Type == 0xF7B1 && (int)e.Message["action"] == 0x0060) {
                    //Logger.Debug("Server has Sell");
                    Reset_myPyreals();
                    lastEvent = DateTime.UtcNow;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        private void EchoFilter_ServerDispatch(object sender, NetworkMessageEventArgs e) {
            try {
                if (e.Message.Type == 0xF7B0 && (int)e.Message["event"] == 0x0022 && (int)e.Message["container"] != UB.Core.CharacterFilter.Id) {  // ACE Item Remove Handling
                    if (myPyreals.ContainsKey((int)e.Message["item"])) {
                        if (Math.Abs(expectedPyreals) > 50) {
                            //Logger.WriteToChat($"expectedPyreals 0x0022 += {myPyreals[(int)e.Message["item"]]}");
                            expectedPyreals += myPyreals[(int)e.Message["item"]];
                            //Logger.WriteToChat($"expectedPyreals = {expectedPyreals}");
                            CheckDone();
                        }
                    } else if (pendingSell.Contains((int)e.Message["item"])) {
                        pendingSell.Remove((int)e.Message["item"]);
                        CheckDone();
                    }
                }
                if (e.Message.Type == 0x0024) { // GDLE Item Remove Handling
                    if (pendingSell.Contains((int)e.Message["object"])) {
                        pendingSell.Remove((int)e.Message["object"]);
                        CheckDone();
                    }
                }

                if (e.Message.Type == 0x0197 && UB.Core.WorldFilter[(int)e.Message["item"]] != null && UB.Core.WorldFilter[(int)e.Message["item"]].Type == 273) { // GDLE Stack Size Handling
                    var newStackSize = (int)e.Message["count"];
                    myPyreals.TryGetValue((int)e.Message["item"], out int oldStackSize);
                    var stackChange = newStackSize - oldStackSize;
                    if (Math.Abs(expectedPyreals) > 50) { //fix for GDLE taking out too many pyreals (MR #517)
                        //Logger.WriteToChat($"expectedPyreals 0x0197 -= {stackChange}");
                        expectedPyreals -= stackChange;
                        //Logger.WriteToChat($"expectedPyreals = {expectedPyreals}");
                        CheckDone();
                    }
                }

                if (e.Message.Type == 0x02DA && (int)e.Message["key"] == 2 && (int)e.Message["value"] == UB.Core.CharacterFilter.Id && UB.Core.WorldFilter[(int)e.Message["object"]] != null) { // GDLE Item Add Handling
                    var wo = UB.Core.WorldFilter[(int)e.Message["object"]];
                    var stackCount = (new UBHelper.Weenie(wo.Id)).StackCount;
                    if (stackCount == 0)
                        stackCount = wo.Values(LongValueKey.StackCount, 1);
                    if (wo.Type == 273) { // Pyreal
                        if (Math.Abs(expectedPyreals) > 50) {
                            //Logger.WriteToChat($"expectedPyreals 0x02DA -= {stackCount}");
                            expectedPyreals -= stackCount;
                            //Logger.WriteToChat($"expectedPyreals = {expectedPyreals}");
                            CheckDone();
                        }
                    } else if (pendingBuy.TryGetValue(wo.Type, out int pbq)) {
                        pbq -= stackCount;
                        if (pbq <= 0) {
                            pendingBuy.Remove(wo.Type);
                            CheckDone();
                        } else {
                            pendingBuy[wo.Type] = pbq;
                        }
                    }
                }
                if (e.Message.Type == 0xF745) {
                    //Logger.WriteToChat($"0x745 exist {UB.Core.WorldFilter[(int)e.Message["object"]]} {UB.Core.WorldFilter[(int)e.Message["object"]].Container} {UB.Core.WorldFilter[(int)e.Message["object"]].Container == UB.Core.CharacterFilter.Id}");
                    if (((int)e.Message.Struct("game")["flags1"] & 0x00004000) != 0) {
                        var container = (int)e.Message.Struct("game")["container"];
                        //Logger.WriteToChat($"Found container: {container}");
                    }
                }
                if (e.Message.Type == 0xF745 && UB.Core.WorldFilter[(int)e.Message["object"]] != null && UB.Core.WorldFilter[(int)e.Message["object"]].Container == UB.Core.CharacterFilter.Id) { // ACE Item Add Handling
                    var wo = UB.Core.WorldFilter[(int)e.Message["object"]];
                    var obj = e.Message.Struct("game");
                    var stackCount = (new UBHelper.Weenie(wo.Id)).StackCount;
                    if (stackCount == 0)
                        stackCount = 1;
                    // handle stacks larger than short.max
                    if (((int)obj["flags1"] & 0x00001000) != 0) {
                        stackCount = (short)obj["stack"];
                        if (stackCount < 0)
                            stackCount += 65536;
                    }
                    if (wo.Type == 273) { // Pyreal
                        if (Math.Abs(expectedPyreals) > 50) {
                            //Logger.WriteToChat($"expectedPyreals 0xF745 -= {stackCount}");
                            expectedPyreals -= stackCount;
                            //Logger.WriteToChat($"expectedPyreals = {expectedPyreals}");
                            CheckDone();
                        }
                    } else if (pendingBuy.TryGetValue(wo.Type, out int pbq)) {
                        //Logger.WriteToChat($"pbq stackCount = {stackCount} ... pqb now is {pbq - stackCount}");
                        pbq -= stackCount;
                        if (pbq <= 0) {
                            pendingBuy.Remove(wo.Type);
                            CheckDone();
                        } else {
                            pendingBuy[wo.Type] = pbq;
                        }
                    } else if (waitingForSplit) {
                        waitingForSplit = false;
                        lastSplit = DateTime.UtcNow;
                    }
                }
            } catch (Exception ex) { Logger.Debug(ex.ToString()); }
        }
        private void WorldFilter_ApproachVendor(object sender, ApproachVendorEventArgs e) {
            try {
                if (vendorOpening > 0 && e.Vendor.MerchantId == vendor.Id) {
                    LogDebug("vendor " + vendor.Name + " opened successfully");
                    vendor = null;
                    vendorOpening = 0;
                    UB.Core.RenderFrame -= Core_RenderFrame_OpenVendor;
                    // VTankControl.Nav_UnBlock(); Let it bleed over into AutoVendor; odds are there's a reason this vendor was opened, and letting vtank run off prolly isn't it.
                }

                VendorCache.AddVendor(e.Vendor);

                if (isRunning || UB.Core.WorldFilter[e.MerchantId] == null)
                    return;

                if (vendorId != e.Vendor.MerchantId) {
                    vendorId = UB.Core.WorldFilter[e.MerchantId].Id;
                    vendorName = UB.Core.WorldFilter[e.MerchantId].Name;
                    vendorSellRate = e.Vendor.SellRate;
                    vendorBuyRate = e.Vendor.BuyRate;
                    vendorCategories = (UBHelper.Weenie.ITEM_TYPE)e.Vendor.Categories;
                    vendorOpened = DateTime.UtcNow;
                    var vendorInfo = $"{UB.Core.WorldFilter[e.Vendor.MerchantId].Name}[0x{e.Vendor.MerchantId:X8}]: BuyRate: {e.Vendor.BuyRate * 100:n0}% SellRate: {e.Vendor.SellRate * 100:n0}% MaxValue: {e.Vendor.MaxValue:n0} Buy: {(EnableBuying&&Enabled?"Enabled":"Disabled")} Sell: {(EnableSelling&&Enabled?"Enabled":"Disabled")}";
                    if (!showMerchantInfoCooldown && ShowMerchantInfo) {
                        showMerchantInfoCooldown = true;
                        UB.Core.RenderFrame += Core_RenderFrame_VendorSpam;
                        Logger.WriteToChat(vendorInfo);
                    }
                    else
                        LogDebug(vendorInfo);
                }

                if (Enabled == false)
                    return;

                Start(e.Vendor.MerchantId);
            } catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UBHelper_VendorClosed(object sender, EventArgs e) {
            try {
                UBHelper.Vendor.VendorClosed -= UBHelper_VendorClosed;
                if (isRunning)
                    Stop();
            }
            catch (Exception ex) { Logger.LogException(ex); }
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
            if (lootProfile == null) {
                try {
                    lootProfile = new VTClassic.LootCore();
                    hasLootCore = true;
                } catch (Exception ex) { Logger.LogException(ex); }

                if (!hasLootCore) {
                    LogError("Unable to load VTClassic, something went wrong.");
                    if (Enabled) {
                        WriteToChat("Disabled");
                        Enabled.Value = false;
                    }
                    return;
                }
            }

            if (merchantId == 0)
                merchantId = vendorId;

            if (!string.IsNullOrEmpty(useProfilePath) && merchantId == 0)
                merchantId = UBHelper.Vendor.Id;

            if (merchantId == 0 || UB.Core.WorldFilter[merchantId] == null) {
                Util.ThinkOrWrite("AutoVendor Fatal - no vendor, cannot start", Think);
                return;
            }

            if (isRunning)
                Stop(true);

            var profilePath = GetProfilePath(string.IsNullOrEmpty(useProfilePath) ? (UB.Core.WorldFilter[merchantId].Name + ".utl") : useProfilePath);

            if (!File.Exists(profilePath)) {
                LogError("No vendor profile exists: " + profilePath);
                Stop();
                return;
            }

            UBHelper.vTank.Decision_Lock(uTank2.ActionLockType.Navigation, TimeSpan.FromMilliseconds(1000));

            // Load our loot profile
            try {
                lootProfile.LoadProfile(profilePath, false);
            }
            catch (Exception) {
                LogError("Unable to load loot profile. Ensure that no profile is loaded in Virindi Item Tool.");
                Stop();
                return;
            }

            itemsToId.Clear();
            List<int> inventory = new List<int>();
            // filter inventory beforehand if we are only selling from the main pack
            if (OnlyFromMainPack) {
                UBHelper.InventoryManager.GetInventory(ref inventory, UBHelper.InventoryManager.GetInventoryType.MainPack);
            } else {
                UBHelper.InventoryManager.GetInventory(ref inventory, UBHelper.InventoryManager.GetInventoryType.AllItems);
            }

            // build a list of items to id from our inventory, attempting to be smart about it
            VendorInfo vendor = VendorCache.GetVendor(vendorId);
            foreach (int item in inventory) {
                UBHelper.Weenie w = new UBHelper.Weenie(item);
                WorldObject wo = UB.Core.WorldFilter[item];
                if (w == null || wo == null || wo.HasIdData) continue;
                if (vendor != null && (vendorCategories & w.Type) == 0) continue;
                itemsToId.Add(item);
            }
            bailTimer = DateTime.UtcNow;
            UBHelper.vTank.Decision_Lock(uTank2.ActionLockType.Navigation, TimeSpan.FromMilliseconds(30000));
            UBHelper.vTank.Decision_Lock(uTank2.ActionLockType.ItemUse, TimeSpan.FromMilliseconds(30000));
            UBHelper.Vendor.VendorClosed += UBHelper_VendorClosed;
            waitingForAutoStackCram = false;
            needsToBuy = needsToSell = false;
            
            // Reset sell batching state
            lastSellTransactionTime = DateTime.MinValue;
            waitingForSellDelay = false;
            remainingSellItems.Clear();
            processingSellBatches = false;
            needsDelayAfterSell = false;
            UB.Core.EchoFilter.ServerDispatch += EchoFilter_ServerDispatch;
            UB.Core.EchoFilter.ClientDispatch += EchoFilter_ClientDispatch;
            UB.Core.RenderFrame += Core_RenderFrame;
            new Assessor.Job(UB.Assessor, ref itemsToId, (_) => { bailTimer = DateTime.UtcNow; }, () => { isRunning = true; });
            shouldAutoCram = UB.InventoryManager.AutoCram;
            shouldAutoStack = UB.InventoryManager.AutoStack;
        }

        public void Stop(bool silent = false) {
            if (!silent)
                Util.ThinkOrWrite("AutoVendor finished: " + vendorName, Think);

            //UB.InventoryManager.Resume();
            isRunning = needsToBuy = needsToSell = false;

            pendingBuy.Clear();
            pendingSell.Clear();

            UB.Core.RenderFrame -= Core_RenderFrame;
            UBHelper.Vendor.VendorClosed -= UBHelper_VendorClosed;
            UB.Core.EchoFilter.ServerDispatch -= EchoFilter_ServerDispatch;
            UB.Core.EchoFilter.ClientDispatch -= EchoFilter_ClientDispatch;
            if (lootProfile != null) (lootProfile).UnloadProfile();
            UBHelper.vTank.Decision_UnLock(uTank2.ActionLockType.Navigation);
            UBHelper.vTank.Decision_UnLock(uTank2.ActionLockType.ItemUse);
        }

        public void Core_RenderFrame_VendorSpam(object sender, EventArgs e) {
            try {
                //if Merchant info is displayed, does not need vendoring, vendorId is set, and it's been over 5 minutes since the vendor was opened; reset vendorId.
                if (DateTime.UtcNow - vendorOpened > TimeSpan.FromSeconds(15)) {
                    showMerchantInfoCooldown = false;
                    UB.Core.RenderFrame -= Core_RenderFrame_VendorSpam;
                    if (!Enabled)
                        vendorId = 0;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        public void Core_RenderFrame(object sender, EventArgs e) {
            try {
                if (IsTimedOut()) {
                    WriteToChat("bail, Timeout expired");
                    Stop();
                }

                if (!Enabled) {
                    Stop();
                    return;
                }

                if (!isRunning)
                    return;

                //if autovendor is running, and nav block has less than a second plus thinkInterval remaining, refresh it
                if (UBHelper.vTank.locks[uTank2.ActionLockType.Navigation] < DateTime.UtcNow + TimeSpan.FromMilliseconds(1250)) {
                    UBHelper.vTank.Decision_Lock(uTank2.ActionLockType.Navigation, TimeSpan.FromMilliseconds(30000));
                    UBHelper.vTank.Decision_Lock(uTank2.ActionLockType.ItemUse, TimeSpan.FromMilliseconds(30000));
                }

                if (TestMode) {
                    DoTestMode();
                    Stop();
                    return;
                }

                if (waitingForAutoStackCram || waitingForSplit || UB.Core.Actions.BusyState != 0)
                    return;

                if (shouldAutoStack) {
                    shouldAutoStack = false;
                    if (UB.InventoryManager.AutoStack && Lib.Inventory.AutoStack(didSomething => { if (didSomething) waitingForAutoStackCram = false; })) {
                        waitingForAutoStackCram = true;
                        return;
                    }
                }
                if (shouldAutoCram) {
                    shouldAutoCram = false;
                    if (UB.InventoryManager.AutoCram && Lib.Inventory.AutoCram(didSomething => { if (didSomething) waitingForAutoStackCram = false; })) {
                        waitingForAutoStackCram = true;
                        return;
                    }
                }

                // Use dynamic timeout for lastEvent - longer during batch delays
                var eventTimeoutMs = waitingForSellDelay ? (TimeoutBetweenTransactionsSeconds * 1000 + 15000) : 15000;
                
                if (DateTime.UtcNow - lastEvent >= TimeSpan.FromMilliseconds(eventTimeoutMs)) {
                    if (lastEvent != DateTime.MinValue) // minvalue was not set, so it expired naturally:
                        Logger.Debug($"Event Timeout. Pyreals: {expectedPyreals:n0}, Sell List: {pendingSell.Count():n0}, Buy List: {pendingBuy.Count():n0}");
                    
                    // Check if we're waiting for sell delay between batches
                    if (waitingForSellDelay) {
                        if (DateTime.UtcNow - lastSellTransactionTime < TimeSpan.FromSeconds(TimeoutBetweenTransactionsSeconds)) {
                            return; // Still waiting for delay - no need to refresh timers constantly
                        }
                        waitingForSellDelay = false;
                        LogDebug($"Sell delay complete, continuing with next batch");
                    }
                    
                    if (HasVendorOpen()) { // not needed any more, but leaving it in for good measure.
                        if (needsToBuy) {
                            needsToBuy = false;
                            UB.Core.Actions.VendorBuyAll();
                            //Logger.Debug("VendorBuyAll");
                            CheckDone();
                        }
                        else if (needsToSell) {
                            needsToSell = false;
                            UB.Core.Actions.VendorSellAll();
                            //Logger.Debug("VendorSellAll");
                            CheckDone();
                        }
                        else {
                            DoVendoring();
                        }
                    }
                    // vendor closed?
                    else if (DateTime.UtcNow - bailTimer > TimeSpan.FromMilliseconds(500)) {
                        LogDebug("Stop because no vendor");
                        Stop();
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void DoVendoring() {
            try {
                List<BuyItem> buyItems = GetBuyItems();
                List<WorldObject> sellItems = GetSellItems();

                UB.Core.Actions.VendorClearBuyList();
                Reset_pendingBuy();
                UB.Core.Actions.VendorClearSellList();
                Reset_pendingSell();
                expectedPyreals = 0;

                var totalBuyCount = 0;
                var totalBuyPyreals = 0;
                var totalBuySlots = 0;
                var freeSlots = (new Weenie(UB.Core.CharacterFilter.Id)).FreeSpace;
                var pyrealCount = Util.PyrealCount();
                StringBuilder buyAdded = new StringBuilder("Autovendor Buy List: ");
                foreach (var buyItem in buyItems.OrderBy(o => o.Item.Value).ToList()) {
                    var vendorPrice = GetVendorSellPrice(buyItem.Item);
                    if (vendorPrice == 0) {
                        Util.ThinkOrWrite($"AutoVendor Fatal - No vendor price found while adding {buyItem.Amount:n0}x {buyItem.Item.Name}[0x{buyItem.Item.Id:X8}] Value: {vendorPrice:n}", Think);
                        Stop();
                        return;
                    }
                    if ((totalBuyPyreals + vendorPrice) <= pyrealCount && (freeSlots - totalBuySlots) > (buyItem.Item.ObjectClass == ObjectClass.TradeNote ? 0 : 1)) {
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
                        UB.Core.Actions.VendorAddBuyList(buyItem.Item.Id, buyCount);
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
                    LogDebug($"{buyAdded.ToString()} - {totalBuyPyreals}/{pyrealCount}");
                    needsToBuy = true;
                    expectedPyreals = -totalBuyPyreals;
                    return;
                }

                // Handle sell batching
                VendorItem nextBuyItem = null;
                if (buyItems.Count > 0)
                    nextBuyItem = buyItems[0].Item;

                // If we don't have remaining sell items, populate the list
                if (remainingSellItems.Count == 0 && !processingSellBatches) {
                    remainingSellItems = new List<WorldObject>(sellItems);
                    processingSellBatches = remainingSellItems.Count > 0;
                    
                    if (processingSellBatches) {
                        // Refresh bailTimer and set dynamic timeout when starting batch processing
                        RefreshBailTimer();
                        LogDebug($"Starting sell batching with {remainingSellItems.Count} total items, max {MaxItemsPerTransaction} per batch");
                    }
                }

                // Process sell batches
                if (processingSellBatches && remainingSellItems.Count > 0) {
                    int totalSellValue = 0;
                    int sellItemCount = 0;
                    int batchLimit = Math.Min(MaxItemsPerTransaction, Math.Min(remainingSellItems.Count, 99)); // GDLE limits transactions to 99 items
                    
                    StringBuilder sellAdded = new StringBuilder("Autovendor Sell List (Batch): ");
                    var itemsToProcess = remainingSellItems.Take(batchLimit).ToList();
                    
                    foreach (var item in itemsToProcess) {
                        var value = GetVendorBuyPrice(item);
                        var stackSize = item.Values(LongValueKey.StackCount, 1);
                        bool shouldSkip = false;

                        if (stackSize < 0) {
                            stackSize = (new UBHelper.Weenie(item.Id)).StackCount;
                            if (stackSize == 0)
                                stackSize = 1;
                        }

                        // dont sell notes if we are trying to buy notes...
                        if (((nextBuyItem != null && nextBuyItem.ObjectClass == ObjectClass.TradeNote) || nextBuyItem == null) && item.ObjectClass == ObjectClass.TradeNote) {
                            Logger.Debug($"AutoVendor bail: buyItem: {(nextBuyItem == null ? "null" : nextBuyItem.Name)} sellItem: {Util.GetObjectName(item.Id)}");
                            shouldSkip = true;
                        }

                        // if we are selling notes to buy something, sell the minimum amount
                        if (!shouldSkip && nextBuyItem != null && item.ObjectClass == ObjectClass.TradeNote) {
                            if (sellItemCount > 0) {
                                shouldSkip = true; // if we already have items to sell, sell those first
                            } else {
                                if (!PyrealsWillFitInMainPack(value)) {
                                    Util.ThinkOrWrite($"AutoVendor Fatal - No inventory room to sell {Util.GetObjectName(item.Id)}", UB.AutoVendor.Think);
                                    Stop();
                                    return;
                                }

                                // see if we already have a single stack of this item
                                using (var inventoryNotes = UB.Core.WorldFilter.GetInventory()) {
                                    inventoryNotes.SetFilter(new ByObjectClassFilter(ObjectClass.TradeNote));
                                    foreach (var wo in inventoryNotes) {
                                        if (wo.Name == item.Name && wo.Values(LongValueKey.StackCount, 0) == 1) {
                                            Logger.Debug($"AutoVendor Selling single {Util.GetObjectName(wo.Id)} so we can afford to buy: " + nextBuyItem.Name);
                                            needsToSell = true;
                                            if (sellItemCount > 0)
                                                sellAdded.Append(", ");
                                            sellAdded.Append(Util.GetObjectName(wo.Id));
                                            pendingSell.Add(wo.Id);
                                            UB.Core.Actions.VendorAddSellList(wo.Id);
                                            totalSellValue += (int)(value);
                                            sellItemCount++;
                                            remainingSellItems.Remove(item);
                                            break;
                                        }
                                    }
                                }
                                if (sellItemCount > 0) break;
                                DoSplit(item, 1);
                                return;
                            }
                        }

                        if (shouldSkip) {
                            remainingSellItems.Remove(item);
                            continue;
                        }

                        // cant sell the whole stack? split it into what we can sell
                        if (!PyrealsWillFitInMainPack(totalSellValue + (int)(value * stackSize))) {
                            if (sellItemCount < 1) {
                                stackSize = (int)(((new Weenie(UB.Core.CharacterFilter.Id)).FreeSpace - 2) * PYREAL_STACK_SIZE / value);
                                if (stackSize > 0) {
                                    DoSplit(item, stackSize);
                                    return;
                                }
                                Util.ThinkOrWrite($"AutoVendor Fatal - No inventory room to sell {Util.GetObjectName(item.Id)}", UB.AutoVendor.Think);
                                Stop();
                                return;
                            }
                            break;
                        }

                        if (sellItemCount > 0)
                            sellAdded.Append(", ");
                        sellAdded.Append(Util.GetObjectName(item.Id));

                        pendingSell.Add(item.Id);
                        UB.Core.Actions.SelectItem(item.Id);
                        UB.Core.Actions.VendorAddSellList(item.Id);
                        totalSellValue += (int)(value * stackSize);
                        sellItemCount++;
                    }

                    // Remove processed items from remaining list
                    foreach (var item in itemsToProcess.Take(sellItemCount)) {
                        remainingSellItems.Remove(item);
                    }

                    if (sellItemCount > 0) {
                        var batchInfo = remainingSellItems.Count > 0 ? $" (Batch {Math.Ceiling((double)(sellItems.Count - remainingSellItems.Count) / MaxItemsPerTransaction)} of {Math.Ceiling((double)sellItems.Count / MaxItemsPerTransaction)})" : "";
                        LogDebug($"{sellAdded.ToString()}{batchInfo} - {totalSellValue}");
                        expectedPyreals = totalSellValue;
                        needsToSell = true;
                        
                        // Flag to set up delay AFTER the sell completes, not before
                        if (remainingSellItems.Count > 0) {
                            needsDelayAfterSell = true;
                            LogDebug($"Batch ready to sell, {remainingSellItems.Count} items remaining for next batch.");
                        } else {
                            processingSellBatches = false;
                            needsDelayAfterSell = false;
                            LogDebug("Final batch ready to sell.");
                        }
                        return;
                    }
                }

                // No more items to sell
                processingSellBatches = false;
                remainingSellItems.Clear();
                Stop();
            } catch (Exception ex) { Logger.LogException(ex); }
        }

        private void DoSplit(WorldObject item, int newStackSize) {
            if (splitQueue != null) {
                LogDebug("Attempted to split item while another split was in progress");
                return;
            }
            splitQueue = new Lib.ActionQueue.Item(_ => { splitQueue = null; });
            var weenie = new UBHelper.Weenie(item.Id);
            weenie.Split(UB.Core.CharacterFilter.Id, 0, newStackSize);
            splitQueue.Queue(weenie.Id);
            waitingForSplit = true;
            lastSplit = DateTime.UtcNow;
            LogDebug($"DoSplit Splitting {Util.GetObjectName(item.Id)}:{item.Id:X8}. old: {item.Values(LongValueKey.StackCount)} new: {newStackSize}");
        }


        private float GetVendorSellPrice(VendorItem item) {
            return (float)((int)(item.Value / item.StackCount) * (item.ObjectClass == ObjectClass.TradeNote ? 1.15f : vendorSellRate));
        }

        private float GetVendorBuyPrice(WorldObject wo) {
            return (float)((int)(wo.Values(LongValueKey.Value, 0) / wo.Values(LongValueKey.StackCount, 1)) * (wo.ObjectClass == ObjectClass.TradeNote ? 1f : vendorBuyRate));
        }

        private bool PyrealsWillFitInMainPack(float amount) { // fixed for ACE's bad pyreal management.
            return ((new Weenie(UB.Core.CharacterFilter.Id).FreeSpace - Math.Ceiling(amount / PYREAL_STACK_SIZE)) > 0); // ensures 1 pack slot always remains free.
        }

        private List<BuyItem> GetBuyItems() {
            bool buyingContainers = false;
            if (!EnableBuying)
                return new List<BuyItem>();

            VendorInfo vendor = VendorCache.GetVendor(vendorId);
            List<BuyItem> buyItems = new List<BuyItem>();

            if (vendor == null) {
                if (!TestMode)
                    Logger.Error($"Vendor is null: {vendorId:X8}");
                return buyItems;
            }

            foreach (VendorItem item in vendor.Items.Values) {
                uTank2.LootPlugins.GameItemInfo itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithVendorObjectTemplateID(item.Id);
                if (itemInfo == null)
                    continue;
                uTank2.LootPlugins.LootAction result = lootProfile.GetLootDecision(itemInfo);
                if (!result.IsKeepUpTo)
                    continue;
                var countObjClass = Util.GetItemCountInInventoryByObjectClass(item.ObjectClass);
                var countByName = Util.GetItemCountInInventoryByName(item.Name);
                if (!buyingContainers && item.ObjectClass == ObjectClass.Container && result.Data1 > countObjClass) {
                    buyingContainers = true;
                    buyItems.Add(new BuyItem(item, result.Data1 - countObjClass));
                }
                else if (item.ObjectClass != ObjectClass.Container && result.Data1 > countByName) {
                    buyItems.Add(new BuyItem(item, result.Data1 - countByName));
                }
            }

            foreach (VendorItem item in vendor.Items.Values) {
                uTank2.LootPlugins.GameItemInfo itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithVendorObjectTemplateID(item.Id);
                if (itemInfo == null) continue;
                uTank2.LootPlugins.LootAction result = lootProfile.GetLootDecision(itemInfo);
                if (!result.IsKeep) continue;
                buyItems.Add(new BuyItem(item, int.MaxValue));
            }

            buyItems.Sort(delegate (BuyItem a, BuyItem b) {
                // tradenotes last
                if (a.Item.ObjectClass == ObjectClass.TradeNote && b.Item.ObjectClass != ObjectClass.TradeNote) return 1;
                if (a.Item.ObjectClass != ObjectClass.TradeNote && b.Item.ObjectClass == ObjectClass.TradeNote) return -1;

                // cheapest first
                var buyPrice1 = GetVendorSellPrice(a.Item);
                var buyPrice2 = GetVendorSellPrice(b.Item);

                return buyPrice1.CompareTo(buyPrice2);
            });

            return buyItems;
        }

        private List<WorldObject> GetSellItems() {
            if (!EnableSelling)
                return new List<WorldObject>();

            List<WorldObject> sellObjects = new List<WorldObject>();
            var vendor = VendorCache.GetVendor(vendorId);

            if (vendor == null || lootProfile == null) return sellObjects;

            using (var inv = UB.Core.WorldFilter.GetInventory()) {
                foreach (WorldObject wo in inv) {
                    if (!ItemIsSafeToGetRidOf(wo)) continue;
                    uTank2.LootPlugins.GameItemInfo itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithID(wo.Id);
                    if (itemInfo == null) continue;
                    uTank2.LootPlugins.LootAction result = lootProfile.GetLootDecision(itemInfo);
                    if (!result.IsSell || !vendor.WillBuyItem(wo))
                        continue;
                    sellObjects.Add(wo);
                }
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
            if (wo.Values(LongValueKey.IconUnderlay, 0) == 23308) return false; // rares

            if (OnlyFromMainPack == true && wo.Container != UB.Core.CharacterFilter.Id)
                return false; // bail if we are only selling from main pack and this isnt in there

            return Util.IsItemSafeToGetRidOf(wo);
        }

        private void DoTestMode() {
            StringBuilder test_mode = new StringBuilder();
            Logger.WriteToChat("AutoVendor TEST MODE");
            test_mode.Append("Buy Items:\n");
            foreach (BuyItem bi in GetBuyItems()) {
                uTank2.LootPlugins.GameItemInfo itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithVendorObjectTemplateID(bi.Item.Id);
                if (itemInfo == null) continue;
                uTank2.LootPlugins.LootAction result = lootProfile.GetLootDecision(itemInfo);
                if (result.IsKeepUpTo || result.IsKeep) {
                    string mc = (ShopItemListTypes.ContainsKey(bi.Item.Category) ? ShopItemListTypes[bi.Item.Category] : $"Unknown Category 0x{bi.Item.Category:X8}");
                    test_mode.Append($"  {mc} -> {bi.Item.Name} * {(bi.Amount == int.MaxValue ? "∞" : bi.Amount.ToString())} - {result.RuleName}\n");
                }
            }
            if (test_mode.Length == 11) test_mode.Append("  (Nothing)");
            Logger.WriteToChat(test_mode.ToString());
            test_mode = new StringBuilder();
            pendingBuy.Clear();

            test_mode.Append("Sell Items:\n");
            List<WorldObject> sellObjects = GetSellItems();
            sellObjects.Sort(delegate (WorldObject wo1, WorldObject wo2) { return Util.GetOverallSlot(wo1) - Util.GetOverallSlot(wo2); });
            
            if (sellObjects.Count > MaxItemsPerTransaction) {
                test_mode.Append($"  NOTE: {sellObjects.Count} items will be processed in {Math.Ceiling((double)sellObjects.Count / MaxItemsPerTransaction)} batches of {MaxItemsPerTransaction} items each, with {TimeoutBetweenTransactionsSeconds} second delays between batches.\n");
            }
            
            foreach (WorldObject wo in sellObjects) {
                uTank2.LootPlugins.GameItemInfo itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithID(wo.Id);
                if (itemInfo == null) continue;
                uTank2.LootPlugins.LootAction result = lootProfile.GetLootDecision(itemInfo);
                if (result.IsSell) {
                    test_mode.Append($"  {Util.GetItemLocation(wo.Id)}: <Tell:IIDString:{Util.GetChatId()}:select|{wo.Id}>{Util.GetObjectName(wo.Id)}</Tell> - {result.RuleName}\n");
                }
            }
            if (test_mode.Length == 12) test_mode.Append("  (Nothing)");
            Logger.WriteToChat(test_mode.ToString());
            pendingSell.Clear();
            expectedPyreals = 0;
            lastEvent = DateTime.MinValue;
        }
        public bool HasVendorOpen() {
            return (UBHelper.Vendor.Id != 0);
        }

        protected override void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    UBHelper.Vendor.VendorClosed -= UBHelper_VendorClosed;
                    UB.Core.WorldFilter.CreateObject -= WorldFilter_CreateObject;
                    UBHelper.Core.RadarUpdate -= Core_RadarUpdatePyrealStackCheck;
                    Decal.Adapter.CoreManager.Current.WorldFilter.ApproachVendor -= WorldFilter_ApproachVendor;
                    Decal.Adapter.CoreManager.Current.RenderFrame -= Core_RenderFrame;
                    Decal.Adapter.CoreManager.Current.EchoFilter.ServerDispatch -= EchoFilter_ServerDispatch;
                    Decal.Adapter.CoreManager.Current.EchoFilter.ClientDispatch -= EchoFilter_ClientDispatch;
                    try {
                        if (hasLootCore && lootProfile != null) lootProfile.UnloadProfile();
                    }
                    catch { }
                    base.Dispose(disposing);
                }
                disposed = true;
            }
        }
    }
}
