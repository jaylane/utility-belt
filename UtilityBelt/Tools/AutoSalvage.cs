using Decal.Adapter;
using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;
using UtilityBelt.Constants;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Settings;
using VirindiViewService.Controls;
using UtilityBelt.Service.Lib.Settings;
using UtilityBelt.Lib.Expressions;

namespace UtilityBelt.Tools {
    [Name("AutoSalvage")]
    [Summary("Salvages items in your inventory based on the loot profile currently loaded in VTank")]
    [FullDescription(@"
> [!CAUTION]
> Use at your own risk! This tool can salvage things automatically. I'm not responsible if you salvage your super dope untinkered armor that was in your inventory.

This plugin will attempt to salvage all items in your inventory that match loot rule salvage in your currently loaded VTank loot profile.  This is helpful for cleaning up after vtank misses salvaging items.  It needs to ID all items in your inventory so it may take a minute to run.  It avoids salvaging equipped, tinkered, and imbued. Anything else that matches a salvage loot rule is fair game, <span style='color: red'>**you have been warned**</span>.

Using `/ub autosalvage` without the force parameter will add all matching items to your salvage window. Specifying the force option with `/ub autosalvage force` will salvage one item at a time until all matching items are salvaged.  VTank will combine them depending on your loot profile rules.
    ")]

    public class AutoSalvage : ToolBase {

        private const int THINK_INTERVAL_MS = 100;
        private List<int> inventoryItems = new List<int>();
        private List<int> salvageItemIds = new List<int>();
        private bool isRunning = false;
        private bool shouldSalvage = false;

        private DateTime lastThought = DateTime.MinValue;
        private bool readyToSalvage = false;
        private bool openedSalvageWindow = false;

        private bool vtankUseItemLocked = false;

        private Dictionary<int, bool> lootClassificationCache = new Dictionary<int, bool>();

        public event EventHandler AutoSalvageFinished;

        #region Settings
        [Summary("Think to yourself when auto salvage is completed")]
        public readonly Setting<bool> Think = new Setting<bool>(false);

        [Summary("Only salvage things in your main pack")]
        public readonly Setting<bool> OnlyFromMainPack = new Setting<bool>(false);
        #endregion

        #region commands
        [Summary("Salvages items in your inventory the match a Salvage type rule in your currently loaded VTank loot profile. You must end the command with \"force\" in order for it to actually salvage anything.")]
        [Usage("/ub autosalvage [force]")]
        [Example("/ub autosalvage", "Adds all matching items to your salvage window")]
        [Example("/ub autosalvage force", "Adds all matching items to your salvage window AND clicks salvage")]
        [CommandPattern("autosalvage", @"^(?<Force>force)?$")]
        public void DoAutoSalvage(string command, Match args) {
            Start(!string.IsNullOrEmpty(args.Groups["Force"].Value));
        }
        #endregion

        #region Expressions
        #region ustadd[wobject obj]
        [ExpressionMethod("ustadd")]
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "obj", "World object to add to the ust panel")]
        [ExpressionReturn(typeof(double), "Returns 1")]
        [Summary("Adds an item to the ust panel")]
        [Example("ustadd[wobjectgetselection[]]", "Adds the currently selected item to your ust panel")]
        public object UstAdd(ExpressionWorldObject wobject) {
            UB.Core.Actions.SalvagePanelAdd(wobject.Id);
            return 1;
        }
        #endregion //ustadd[wobject obj]
        #region ustopen[]
        [ExpressionMethod("ustopen")]
        [ExpressionReturn(typeof(double), "Returns 1 on succes, 0 on failure")]
        [Summary("Opens the ust panel")]
        [Example("ustopen[]", "Opens the ust panel")]
        public object UstOpen() {
            bool foundUst = false;
            using (var inv = UB.Core.WorldFilter.GetInventory()) {
                foreach (var item in inv) {
                    if (item != null && item.Name == "Ust") {
                        foundUst = true;
                        UB.Core.Actions.UseItem(item.Id, 0);
                        break;
                    }
                }
            }
            return foundUst ? 1 : 0;
        }
        #endregion //ustopen[]
        #region ustsalvage[]
        [ExpressionMethod("ustsalvage")]
        [ExpressionReturn(typeof(double), "Returns 1")]
        [Summary("Salvages the items in the ust panel")]
        [Example("ustsalvage[]", "Salvages all the items in the ust panel")]
        public object UstSalvage() {
            UB.Core.Actions.SalvagePanelSalvage();
            return 1;
        }
        #endregion //ustsalvage[]
        #endregion

        public AutoSalvage(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        private void WorldFilter_CreateObject(object sender, CreateObjectEventArgs e) {
            try {
                if (e.New.ObjectClass == ObjectClass.Salvage) {
                    lastThought = DateTime.UtcNow - TimeSpan.FromMilliseconds(THINK_INTERVAL_MS);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }


        public void Start(bool force = false, List<int> trySalvageList = null) {
            //if (UB.AutoVendor?.HasVendorOpen() == true) {
            if (UB.Core.Actions.OpenedContainer != 0) {
                Stop();
                return;
            }
            else if (isRunning) {
                LogError("Already running");
                return;
            }

            isRunning = true;
            shouldSalvage = force;

            UB.Core.RenderFrame += Core_RenderFrame;
            UB.Core.WorldFilter.CreateObject += WorldFilter_CreateObject;

            Reset();
            if (trySalvageList == null) {
                LoadInventory();
            }
            else {
                inventoryItems = trySalvageList;
                //foreach (int i in inventoryItems) {
                //    Logger.WriteToChat(i.ToString());
                //}
                UB.Assessor.RequestAll(inventoryItems);
            }
        }

        public void StartSingleItem(int item, bool force = false) {
            //if (UB.AutoVendor?.HasVendorOpen() == true) {
            if (UB.Core.Actions.OpenedContainer != 0) {
                Stop();
                return;
            }
            else if (isRunning) {
                LogError("Already running");
                return;
            }

            isRunning = true;
            shouldSalvage = force;

            UB.Core.RenderFrame += Core_RenderFrame;
            UB.Core.WorldFilter.CreateObject += WorldFilter_CreateObject;

            Reset();
            //LoadInventory();
            if (AllowedToSalvageItem(UB.Core.WorldFilter[item])) {
                inventoryItems.Add(item);
            }
            else {
                Stop();
            }
        }

        private void Stop() {
            if (isRunning == false) return;
            isRunning = false;

            UB.Core.WorldFilter.CreateObject -= WorldFilter_CreateObject;
            UB.Core.RenderFrame -= Core_RenderFrame;

            Reset();

            if (Think == true) ChatThink("complete.");
            else WriteToChat("complete.");
            if (vtankUseItemLocked) {
                UBHelper.vTank.Decision_UnLock(uTank2.ActionLockType.Salvage);
                vtankUseItemLocked = false;
            }
            AutoSalvageFinished?.Invoke(this, EventArgs.Empty);
        }

        public void Reset() {
            inventoryItems.Clear();
            salvageItemIds.Clear();
            lootClassificationCache.Clear();
            readyToSalvage = false;
            openedSalvageWindow = false;
        }

        public void LoadInventory() {
            using (var inventory = UB.Core.WorldFilter.GetInventory()) {

                // prefilter inventory if we are only selling from the main pack
                if (OnlyFromMainPack == true) {
                    inventory.SetFilter(new ByContainerFilter(UB.Core.CharacterFilter.Id));
                }

                foreach (var item in inventory) {
                    if (!AllowedToSalvageItem(item)) continue;

                    inventoryItems.Add(item.Id);
                }
            }

            UB.Assessor.RequestAll(inventoryItems);
        }

        private bool AllowedToSalvageItem(WorldObject item) {
            if (!Util.IsItemSafeToGetRidOf(item)) return false;

            // dont put in bags of salvage
            if (item.ObjectClass == ObjectClass.Salvage) return false;

            // only things with a material
            if (item.Values(LongValueKey.Material, 0) <= 0) return false;

            // bail if we are only salvaging from main pack and this isnt in there
            if (OnlyFromMainPack == true && item.Container != UB.Core.CharacterFilter.Id) {
                return false;
            }

            return true;
        }

        public bool OpenSalvageWindow() {
            var foundUst = false;

            using (var inv = UB.Core.WorldFilter.GetInventory()) {
                foreach (var item in inv) {
                    if (item != null && item.Name == "Ust") {
                        foundUst = true;
                        UB.Core.Actions.UseItem(item.Id, 0);
                        break;
                    }
                }
            }

            if (!foundUst) {
                LogError("No ust in inventory, can't salvage.");
                Stop();
            }

            return foundUst;
        }

        private int GetNextSalvageId() {
            var list = new List<int>(inventoryItems);

            foreach (var id in list) {
                try {
                    var item = UB.Core.WorldFilter[id];
                    if (!AllowedToSalvageItem(item)) continue;

                    if (lootClassificationCache.ContainsKey(id)) {
                        inventoryItems.Remove(id);
                        if (lootClassificationCache[id] == true) {
                            return id;
                        }

                        continue;
                    }
                    else {
                        var result = uTank2.PluginCore.PC.FLootPluginClassifyImmediate(id);

                        lootClassificationCache.Add(id, result.SimpleAction == uTank2.eLootAction.Salvage);

                        if (result.SimpleAction == uTank2.eLootAction.Salvage) {
                            return id;
                        }
                    }
                }
                catch (Exception ex) { Logger.LogException(ex); }
            }
            
            return 0;
        }

        private void AddSalvageToWindow() {
            var id = GetNextSalvageId();

            if (id == 0) {
                Stop();
                return;
            }
            if (UB.Core.Actions.BusyState != 0) {
                return;
            }
            UB.Core.Actions.SalvagePanelAdd(id);
            inventoryItems.Remove(id);

            LogDebug($"Adding {Util.GetObjectName(id)}");

            readyToSalvage = true;
        }

        public void Core_RenderFrame(object sender, EventArgs e) {
            try {
                if (DateTime.UtcNow - lastThought >= TimeSpan.FromMilliseconds(THINK_INTERVAL_MS)) {
                    lastThought = DateTime.UtcNow;

                    bool hasAllItemData = !UB.Assessor.NeedsInventoryData(inventoryItems);

                    //if (UB.AutoVendor.HasVendorOpen()) {

                    if (UB.Core.Actions.OpenedContainer != 0) {
                        WriteToChat("bailing, vendor is open.");
                        Stop();
                        return;
                    }

                    if (readyToSalvage && shouldSalvage) {
                        UB.Core.Actions.SalvagePanelSalvage();
                        readyToSalvage = false;
                        lastThought = DateTime.UtcNow + TimeSpan.FromMilliseconds(800);
                        return;
                    }

                    if (isRunning && hasAllItemData) {
                        if (!vtankUseItemLocked) {
                            UBHelper.vTank.Decision_Lock(uTank2.ActionLockType.Salvage, TimeSpan.FromMilliseconds(10000));
                            vtankUseItemLocked = true;
                        }
                        if (openedSalvageWindow) {
                            AddSalvageToWindow();
                            return;
                        }
                        else {
                            if (!OpenSalvageWindow()) {
                                Logger.WriteToChat("not open salvage window");
                                Stop();
                                return;
                            }

                            openedSalvageWindow = true;
                        }
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
    }
}
