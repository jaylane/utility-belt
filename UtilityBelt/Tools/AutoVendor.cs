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
using UtilityBelt.Lib.VendorCache;
using UtilityBelt.Views;
using VirindiViewService.Controls;

namespace UtilityBelt.Tools {
    [Name("AutoVendor")]
    [Summary("Automatically buys/sells items at vendors based on loot profiles.")]
    [FullDescription(@"
<span style='color:red'>I **highly** suggest enabling test mode before actually running, this can **sell all of your stuff** so please don't blame me.</span>

Kind of like [mag-tools auto buy/sell](https://github.com/Mag-nus/Mag-Plugins/wiki/Mag%E2%80%90Tools-Misc#vendor-auto-buysell-on-open-trade). It's less forgiving, and can sell just about anything. It will load the first profile it finds when you open a vendor in this order:

```
Documents\Decal Plugins\UtilityBelt\<server>\<char>\autovendor\Vendor Name.utl
Documents\Decal Plugins\UtilityBelt\autovendor\Vendor Name.utl
Documents\Decal Plugins\UtilityBelt\<server>\<char>\default.utl
Documents\Decal Plugins\UtilityBelt\autovendor\default.utl
```

### How to use

* Create a virindi tank loot profile with some keep rules, here is an example for [Aun Amanaualuan the Elder Shaman](uploads/7e2943b4e4924a30cc0a876251e3bee5/Aun_Amanaualuan_the_Elder_Shaman.utl). (replace underscores with spaces after downloading)
* Drop the profile in `%USERPROFILE%\Documents\Decal Plugins\UtilityBelt\autovendor\`
* Make sure UB AutoVendor option is enabled in the decal plugin window
* I **highly** suggest enabling test mode before actually running, this can **sell all of your stuff** so please don't blame me.
* Next time you open Aun Amanaualuan the Elder Shaman, ub will auto buy components and sell peas.
* If a profile does not exist, nothing will be bought/sold.

### Info

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

### Example Profiles
* [Tunlok Weapons Master.utl](/utl/Tunlok Weapons Master.utl)
    - (Sells all salvage except Granite)
* [Aun Amanaualuan the Elder Shaman.utl](/utl/Aun Amanaualuan the Elder Shaman.utl)
    - Buys components /portal gems. Sells peas.
* [Thimrin Woodsetter.utl](/utl/Thimrin Woodsetter.utl)
    - Buys supplies based on character's current skill level (healing kits, cooking pot, rations)
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
        private ActionQueue.Item splitQueue;
        private bool shouldAutoStack = false;
        private bool shouldAutoCram = false;
        private bool showMerchantInfoCooldown = false;
        private List<int> itemsToId = new List<int>();

        private int expectedPyreals = 0;
        private readonly Dictionary<int, int> myPyreals = new Dictionary<int, int>();
        private readonly Dictionary<int, int> pendingBuy = new Dictionary<int, int>();
        private readonly List<int> pendingSell = new List<int>();

        internal readonly Dictionary<int, string> ShopItemListTypes = new Dictionary<int, string> { { 0x00000001, "Weapons" }, { 0x00000002, "Armor" }, { 0x00000004, "Clothing" }, { 0x00000008, "Jewelry" }, { 0x00000010, "Miscellaneous" }, { 0x00000020, "Food" }, { 0x00000080, "Miscellaneous" }, { 0x00000100, "Weapons" }, { 0x00000200, "Containers" }, { 0x00000400, "Miscellaneous" }, { 0x00000800, "Gems" }, { 0x00001000, "Spell Components" }, { 0x00002000, "Books, Paper" }, { 0x00004000, "Keys, Tools" }, { 0x00008000, "Magic Items" }, { 0x00040000, "Trade Notes" }, { 0x00080000, "Mana Stones" }, { 0x00100000, "Services" }, { 0x00400000, "Cooking Items" }, { 0x00800000, "Alchemical Items" }, { 0x01000000, "Fletching Items" }, { 0x04000000, "Alchemical Items" }, { 0x08000000, "Fletching Items" }, { 0x20000000, "Keys, Tools" } };

        #region Config
        [Summary("Enabled")]
        [DefaultValue(true)]
        [Hotkey("AutoVendor", "Toggle AutoVendor functionality")]
        public bool Enabled {
            get { return (bool)GetSetting("Enabled"); }
            set { UpdateSetting("Enabled", value); }
        }

        [Summary("EnableBuying")]
        [DefaultValue(true)]
        public bool EnableBuying {
            get { return (bool)GetSetting("EnableBuying"); }
            set { UpdateSetting("EnableBuying", value); }
        }

        [Summary("EnableSelling")]
        [DefaultValue(true)]
        public bool EnableSelling {
            get { return (bool)GetSetting("EnableSelling"); }
            set { UpdateSetting("EnableSelling", value); }
        }

        [Summary("Think to yourself when auto vendor is completed")]
        [DefaultValue(false)]
        public bool Think {
            get { return (bool)GetSetting("Think"); }
            set { UpdateSetting("Think", value); }
        }

        [Summary("Test mode (don't actually sell/buy, just echo to the chat window)")]
        [DefaultValue(false)]
        [Hotkey("AutoVendorTestMode", "Toggle AutoVendor TestMode functionality")]
        public bool TestMode {
            get { return (bool)GetSetting("TestMode"); }
            set { UpdateSetting("TestMode", value); }
        }

        [Summary("Show merchant info on approach vendor")]
        [DefaultValue(true)]
        public bool ShowMerchantInfo {
            get { return (bool)GetSetting("ShowMerchantInfo"); }
            set { UpdateSetting("ShowMerchantInfo", value); }
        }

        [Summary("Only vendor things in your main pack")]
        [DefaultValue(false)]
        public bool OnlyFromMainPack {
            get { return (bool)GetSetting("OnlyFromMainPack"); }
            set { UpdateSetting("OnlyFromMainPack", value); }
        }

        [Summary("Attempts to open vendor on /ub vendor open[p]")]
        [DefaultValue(4)]
        public int Tries {
            get { return (int)GetSetting("Tries"); }
            set { UpdateSetting("Tries", value); }
        }

        [Summary("Tine between open vendor attempts (in milliseconds)")]
        [DefaultValue(5000)]
        public int TriesTime {
            get { return (int)GetSetting("TriesTime"); }
            set { UpdateSetting("TriesTime", value); }
        }
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
        [Usage("/ub vendor {open[p] <vendorname,vendorid,vendorhex> | buyall | sellall | clearbuy | clearsell | opencancel}")]
        [Example("/ub vendor open Tunlok Weapons Master", "Opens vendor with name \"Tunlok Weapons Master\"")]
        [Example("/ub vendor opencancel", "Quietly cancels the last /ub vendor open* command")]
        [CommandPattern("vendor", @"^ *(?<params>(openp? ?.+|buy(all)?|sell(all)?|clearbuy|clearsell|opencancel)) *$")]
        public void DoVendor(string _, Match args) {
            UB_vendor(args.Groups["params"].Value);
        }
        private DateTime vendorTimestamp = DateTime.MinValue;
        private int vendorOpening = 0;
        private static WorldObject vendor = null;

        public void UB_vendor(string parameters) {
            char[] stringSplit = { ' ' };
            string[] parameter = parameters.Split(stringSplit, 2);
            if (parameter.Length == 0) {
                Logger.Error("Usage: /ub vendor {open[p] [vendorname,vendorid,vendorhex],opencancel,buyall,sellall,clearbuy,clearsell}");
                return;
            }

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
            }
        }
        private void UB_vendor_open(string vendorname, bool partial) {
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
            try {
                Directory.CreateDirectory(Path.Combine(Util.GetPluginDirectory(), "autovendor"));
                Directory.CreateDirectory(Path.Combine(Util.GetCharacterDirectory(), "autovendor"));
                Directory.CreateDirectory(Path.Combine(Util.GetServerDirectory(), "autovendor"));

                UB.Core.WorldFilter.ApproachVendor += WorldFilter_ApproachVendor;
            } catch (Exception ex) { Logger.LogException(ex); }
        }

        public override void Init() {
            base.Init();

            if (UB.Core.CharacterFilter.LoginStatus != 3)
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
                        UB.Core.RenderFrame += Core_RenderFramePyrealStackCheck;
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Core_RenderFramePyrealStackCheck(object sender, EventArgs e) {
            try {
                CheckPyrealMaxStack();
                if (hasUpdatedPyrealStackSize) {
                    UB.Core.RenderFrame -= Core_RenderFramePyrealStackCheck;
                }
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
            if (expectedPyreals < 50 && pendingSell.Count() == 0 && pendingBuy.Count() == 0) {
                lastEvent = DateTime.MinValue;
                shouldAutoStack = UB.InventoryManager.AutoStack;
                shouldAutoCram = UB.InventoryManager.AutoCram;
            }
            else
                lastEvent = DateTime.UtcNow;
            bailTimer = DateTime.UtcNow;
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
            var hasLootCore = false;
            if (lootProfile == null) {
                try {
                    lootProfile = new VTClassic.LootCore();
                    hasLootCore = true;
                } catch (Exception ex) { Logger.LogException(ex); }

                if (!hasLootCore) {
                    LogError("Unable to load VTClassic, something went wrong.");
                    if (Enabled) {
                        WriteToChat("Disabled");
                        Enabled = false;
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
            lootProfile.LoadProfile(profilePath, false);

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
            vendorId = 0;

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
                if (DateTime.UtcNow - bailTimer > TimeSpan.FromSeconds(60)) {
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
                    if (UB.InventoryManager.AutoStack && UBHelper.InventoryManager.AutoStack()) {
                        UBHelper.ActionQueue.InventoryEvent += ActionQueue_InventoryEvent;
                        waitingForAutoStackCram = true;
                        return;
                    }
                }
                if (shouldAutoCram) {
                    shouldAutoCram = false;
                    if (UB.InventoryManager.AutoCram && UBHelper.InventoryManager.AutoCram()) {
                        UBHelper.ActionQueue.InventoryEvent += ActionQueue_InventoryEvent;
                        waitingForAutoStackCram = true;
                        return;
                    }
                }

                if (DateTime.UtcNow - lastEvent >= TimeSpan.FromMilliseconds(15000)) {
                    if (lastEvent != DateTime.MinValue) // minvalue was not set, so it expired naturally:
                        Logger.Debug($"Event Timeout. Pyreals: {expectedPyreals:n0}, Sell List: {pendingSell.Count():n0}, Buy List: {pendingBuy.Count():n0}");
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

        private void ActionQueue_InventoryEvent(object sender, EventArgs e) {
            try {
                waitingForAutoStackCram = false;
                UBHelper.ActionQueue.InventoryEvent -= ActionQueue_InventoryEvent;
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

                    if (stackSize < 0) {
                        stackSize = (new UBHelper.Weenie(item.Id)).StackCount;
                        if (stackSize == 0)
                            stackSize = 1;
                    }

                    // dont sell notes if we are trying to buy notes...
                    if (((nextBuyItem != null && nextBuyItem.ObjectClass == ObjectClass.TradeNote) || nextBuyItem == null) && item.ObjectClass == ObjectClass.TradeNote) {
                        Logger.Debug($"AutoVendor bail: buyItem: {(nextBuyItem == null ? "null" : nextBuyItem.Name)} sellItem: {Util.GetObjectName(item.Id)}");
                        break;
                    }

                    // if we are selling notes to buy something, sell the minimum amount
                    if (nextBuyItem != null && item.ObjectClass == ObjectClass.TradeNote) {
                        if (sellItemCount > 0) break; // if we already have items to sell, sell those first
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
                                    nestedBreak = true;
                                    break;
                                }
                            }
                        }
                        if (nestedBreak)
                            break;
                        DoSplit(item, 1);
                        return;
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

        private void DoSplit(WorldObject item, int newStackSize) {
            if (splitQueue != null) {
                LogDebug("Attempted to split item while another split was in progress");
                return;
            }
            splitQueue = new UBHelper.ActionQueue.Item();
            var weenie = new UBHelper.Weenie(item.Id);
            UBHelper.ActionQueue.InventoryEvent += ActionQueue_InventoryEvent_DoSplit;
            weenie.Split(UB.Core.CharacterFilter.Id, 0, newStackSize);
            splitQueue.Queue(weenie.Id);
            waitingForSplit = true;
            LogDebug($"DoSplit Splitting {Util.GetObjectName(item.Id)}:{item.Id:X8}. old: {item.Values(LongValueKey.StackCount)} new: {newStackSize}");
        }

        private void ActionQueue_InventoryEvent_DoSplit(object sender, EventArgs e) {
            try {
                if (sender.Equals(splitQueue)) {
                    splitQueue = null;
                    UBHelper.ActionQueue.InventoryEvent -= ActionQueue_InventoryEvent_DoSplit;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
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

            VendorInfo vendor = VendorCache.GetVendor(UBHelper.Vendor.Id);
            List<BuyItem> buyItems = new List<BuyItem>();

            if (vendor == null) {
                if (!TestMode)
                    Logger.Error("Vendor is null");
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
            // TODO: string builder and write to chat once
            Logger.WriteToChat("AutoVendor TEST MODE");
            Logger.WriteToChat("Buy Items:");
            foreach (BuyItem bi in GetBuyItems()) {
                uTank2.LootPlugins.GameItemInfo itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithVendorObjectTemplateID(bi.Item.Id);
                if (itemInfo == null) continue;
                uTank2.LootPlugins.LootAction result = lootProfile.GetLootDecision(itemInfo);
                if (result.IsKeepUpTo || result.IsKeep) {
                    string mc = (ShopItemListTypes.ContainsKey(bi.Item.Category) ? ShopItemListTypes[bi.Item.Category] : $"Unknown Category 0x{bi.Item.Category:X8}");
                    Logger.WriteToChat($"  {mc} -> {bi.Item.Name} * {(bi.Amount == int.MaxValue ? "∞" : bi.Amount.ToString())} - {result.RuleName}");
                }
            }
            pendingBuy.Clear();

            Logger.WriteToChat("Sell Items:");
            List<WorldObject> sellObjects = GetSellItems();
            sellObjects.Sort(delegate (WorldObject wo1, WorldObject wo2) { return Util.GetOverallSlot(wo1) - Util.GetOverallSlot(wo2); });
            foreach (WorldObject wo in sellObjects) {
                uTank2.LootPlugins.GameItemInfo itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithID(wo.Id);
                if (itemInfo == null) continue;
                uTank2.LootPlugins.LootAction result = lootProfile.GetLootDecision(itemInfo);
                if (result.IsSell) {
                    Logger.WriteToChat($"  {Util.GetItemLocation(wo.Id)}: <Tell:IIDString:{Util.GetChatId()}:select|{wo.Id}>{Util.GetObjectName(wo.Id)}</Tell> - {result.RuleName}");
                }
            }
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
                    UB.Core.RenderFrame -= Core_RenderFramePyrealStackCheck;
                    Decal.Adapter.CoreManager.Current.WorldFilter.ApproachVendor -= WorldFilter_ApproachVendor;
                    Decal.Adapter.CoreManager.Current.RenderFrame -= Core_RenderFrame;
                    Decal.Adapter.CoreManager.Current.EchoFilter.ServerDispatch -= EchoFilter_ServerDispatch;
                    Decal.Adapter.CoreManager.Current.EchoFilter.ClientDispatch -= EchoFilter_ClientDispatch;
                    if (lootProfile != null) lootProfile.UnloadProfile();
                    base.Dispose(disposing);
                }
                disposed = true;
            }
        }
    }
}
