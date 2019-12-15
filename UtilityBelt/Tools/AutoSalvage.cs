using Decal.Adapter;
using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;
using UtilityBelt.Constants;
using UtilityBelt.Lib;
using VirindiViewService.Controls;

namespace UtilityBelt.Tools {
    [Name("AutoSalvage")]
    [Summary("Salvages items in your inventory based on the loot profile currently loaded in VTank")]
    [FullDescription(@"
<span style='color: red'>
**Use at your own risk, I'm not responsible if you salvage your super dope untinkered armor that was in your inventory.**
</span>

This plugin will attempt to salvage all items in your inventory that match loot rule salvage in your currently loaded VTank loot profile.  This is helpful for cleaning up after vtank misses salvaging items.  It needs to ID all items in your inventory so it may take a minute to run.  It avoids salvaging equipped, tinkered, and imbued. Anything else that matches a salvage loot rule is fair game, <span style='color: red'>**you have been warned**</span>.
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

        private Dictionary<int, bool> lootClassificationCache = new Dictionary<int, bool>();

        #region Settings
        [Summary("Think to yourself when auto salvage is completed")]
        [DefaultValue(false)]
        public bool Think {
            get { return (bool)GetSetting("Think"); }
            set { UpdateSetting("Think", value); }
        }

        [Summary("Only salvage things in your main pack")]
        [DefaultValue(false)]
        public bool OnlyFromMainPack {
            get { return (bool)GetSetting("OnlyFromMainPack"); }
            set { UpdateSetting("OnlyFromMainPack", value); }
        }
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

        public void Start(bool force = false) {
            if (UB.AutoVendor?.HasVendorOpen() == true) {
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
            LoadInventory();
        }

        private void Stop() {
            if (isRunning == false) return;
            isRunning = false;

            UB.Core.WorldFilter.CreateObject -= WorldFilter_CreateObject;
            UB.Core.RenderFrame -= Core_RenderFrame;

            Reset();

            if (Think == true) ChatThink("complete.");
            else WriteToChat("complete.");
        }

        public void Reset() {
            inventoryItems.Clear();
            salvageItemIds.Clear();
            lootClassificationCache.Clear();
            readyToSalvage = false;
            openedSalvageWindow = false;
        }

        public void LoadInventory() {
            var inventory = UB.Core.WorldFilter.GetInventory();

            // prefilter inventory if we are only selling from the main pack
            if (OnlyFromMainPack == true) {
                inventory.SetFilter(new ByContainerFilter(UB.Core.CharacterFilter.Id));
            }

            foreach (var item in inventory) {
                if (!AllowedToSalvageItem(item)) continue;

                inventoryItems.Add(item.Id);
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

        private bool OpenSalvageWindow() {
            var foundUst = false;

            foreach (var item in UB.Core.WorldFilter.GetInventory()) {
                if (item != null && item.Name == "Ust") {
                    foundUst = true;
                    UB.Core.Actions.UseItem(item.Id, 0);
                    break;
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

                    if (UB.AutoVendor.HasVendorOpen()) {
                        WriteToChat("bailing, vendor is open.");
                        Stop();
                        return;
                    }

                    if (readyToSalvage && shouldSalvage) {
                        readyToSalvage = false;
                        UB.Core.Actions.SalvagePanelSalvage();
                        lastThought = DateTime.UtcNow + TimeSpan.FromMilliseconds(800);
                        return;
                    }

                    if (isRunning && hasAllItemData) {
                        if (openedSalvageWindow) {
                            AddSalvageToWindow();
                            return;
                        }
                        else {
                            if (!OpenSalvageWindow()) {
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
