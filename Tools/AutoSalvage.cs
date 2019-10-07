using Decal.Adapter;
using Decal.Adapter.Wrappers;
using Mag.Shared.Settings;
using System;
using System.Collections.Generic;
using UtilityBelt.Constants;
using VirindiViewService.Controls;

namespace UtilityBelt.Tools {
    class AutoSalvage : IDisposable {
        public const int RETRY_COUNT = 3;
        private const int THINK_INTERVAL_MS = 100;
        private List<int> inventoryItems = new List<int>();
        private List<int> salvageItemIds = new List<int>();
        private List<int> blacklistedIds = new List<int>();
        private bool isRunning = false;
        private bool shouldSalvage = false;
        
        HudCheckBox UIAutoSalvageThink { get; set; }
        HudCheckBox UIAutoSalvageOnlyFromMainPack { get; set; }
        HudButton UIAutoSalvageStart { get; set; }

        private DateTime lastThought = DateTime.MinValue;
        private DateTime lastAction = DateTime.MinValue;
        private bool readyToSalvage = false;
        private bool openedSalvageWindow = false;
        private bool disposed = false;

        private Dictionary<int, bool> lootClassificationCache = new Dictionary<int, bool>();

        public AutoSalvage() {
            Globals.Core.CommandLineText += Current_CommandLineText;
            Globals.Core.WorldFilter.CreateObject += WorldFilter_CreateObject;

            UIAutoSalvageStart = Globals.MainView.view != null ? (HudButton)Globals.MainView.view["AutoSalvageStart"] : new HudButton();
            UIAutoSalvageStart.Hit += UIAutoSalvageStart_Hit;

            UIAutoSalvageThink = Globals.MainView.view != null ? (HudCheckBox)Globals.MainView.view["AutoSalvageThink"] : new HudCheckBox();
            UIAutoSalvageThink.Change += UIAutoSalvageThink_Change;

            UIAutoSalvageOnlyFromMainPack = Globals.MainView.view != null ? (HudCheckBox)Globals.MainView.view["AutoSalvageOnlyFromMainPack"] : new HudCheckBox();
            UIAutoSalvageOnlyFromMainPack.Change += UIAutoSalvageOnlyFromMainPack_Change;

            Globals.Settings.AutoSalvage.PropertyChanged += (s, e) => {
                UpdateUI();
            };

            UpdateUI();
        }

        private void UpdateUI() {
            UIAutoSalvageOnlyFromMainPack.Checked = Globals.Settings.AutoSalvage.OnlyFromMainPack;
            UIAutoSalvageThink.Checked = Globals.Settings.AutoSalvage.Think;
        }

        private void WorldFilter_CreateObject(object sender, CreateObjectEventArgs e) {
            try {
                if (e.New.ObjectClass == ObjectClass.Salvage) {
                    lastThought = DateTime.UtcNow - TimeSpan.FromMilliseconds(THINK_INTERVAL_MS);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UIAutoSalvageStart_Hit(object sender, EventArgs e) {
            try {
                Start();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UIAutoSalvageThink_Change(object sender, EventArgs e) {
            Globals.Settings.AutoSalvage.Think = UIAutoSalvageThink.Checked;
        }

        private void UIAutoSalvageOnlyFromMainPack_Change(object sender, EventArgs e) {
            Globals.Settings.AutoSalvage.OnlyFromMainPack = UIAutoSalvageOnlyFromMainPack.Checked;
        }

        private void Current_CommandLineText(object sender, ChatParserInterceptEventArgs e) {
            try {
                if (e.Text.StartsWith("/autosalvage") || e.Text.StartsWith("/ub autosalvage")) {
                    bool force = e.Text.Contains("force");
                    e.Eat = true;

                    Start(force);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public void Start(bool force = false) {
            if (Globals.AutoVendor.HasVendorOpen() == false) {
                isRunning = true;
                shouldSalvage = force;

                Reset();
                LoadInventory();
            }
            else {
                Stop();
            }
        }

        private void Stop() {
            Reset();
            isRunning = false;

            if (Globals.Settings.AutoSalvage.Think == true) {
                Util.Think("AutoSalvage complete.");
            }
            else {
                Util.WriteToChat("AutoSalvage complete.");
            }
        }

        public void Reset() {
            inventoryItems.Clear();
            salvageItemIds.Clear();
            lootClassificationCache.Clear();
            readyToSalvage = false;
            openedSalvageWindow = false;
        }

        public void LoadInventory() {
            var inventory = Globals.Core.WorldFilter.GetInventory();

            // prefilter inventory if we are only selling from the main pack
            if (Globals.Settings.AutoSalvage.OnlyFromMainPack == true) {
                inventory.SetFilter(new ByContainerFilter(Globals.Core.CharacterFilter.Id));
            }

            foreach (var item in inventory) {
                if (!AllowedToSalvageItem(item)) continue;

                inventoryItems.Add(item.Id);
            }
            
            Globals.Assessor.RequestAll(inventoryItems);
        }

        private bool AllowedToSalvageItem(WorldObject item) {
            if (!Util.IsItemSafeToGetRidOf(item)) return false;

            // dont put in bags of salvage
            if (item.ObjectClass == ObjectClass.Salvage) return false;

            // only things with a material
            if (item.Values(LongValueKey.Material, 0) <= 0) return false;

            // blacklisted items
            if (blacklistedIds.Contains(item.Id)) return false;

            // bail if we are only salvaging from main pack and this isnt in there
            if (Globals.Settings.AutoSalvage.OnlyFromMainPack == true && item.Container != Globals.Core.CharacterFilter.Id) {
                return false;
            }

            return true;
        }

        private bool OpenSalvageWindow() {
            var foundUst = false;

            foreach (var item in Globals.Core.WorldFilter.GetInventory()) {
                if (item != null && item.Name == "Ust") {
                    foundUst = true;
                    Globals.Core.Actions.UseItem(item.Id, 0);
                    break;
                }
            }

            if (!foundUst) {
                Util.WriteToChat("AutoSalvage: No ust in inventory, can't salvage.");
                Stop();
            }

            return foundUst;
        }

        private int GetNextSalvageId() {
            var list = new List<int>(inventoryItems);

            foreach (var id in list) {
                try {
                    var item = Globals.Core.WorldFilter[id];
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

            Globals.Core.Actions.SalvagePanelAdd(id);
            inventoryItems.Remove(id);

            if (Globals.Settings.Debug == true) {
                Util.WriteToChat($"AutoSalvage: Add: {Util.GetObjectName(id)}");
            }

            readyToSalvage = true;
        }

        public void Think() {
            if (!isRunning) return;

            if (DateTime.UtcNow - lastThought >= TimeSpan.FromMilliseconds(THINK_INTERVAL_MS)) {
                lastThought = DateTime.UtcNow;
                
                bool hasAllItemData = !Globals.Assessor.NeedsInventoryData(inventoryItems);

                if (Globals.AutoVendor.HasVendorOpen()) {
                    Util.WriteToChat("AutoSalvage bailing, vendor is open.");
                    Stop();
                    return;
                }

                if (readyToSalvage && shouldSalvage) {
                    readyToSalvage = false;
                    Globals.Core.Actions.SalvagePanelSalvage();
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

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    Globals.Core.CommandLineText -= Current_CommandLineText;
                    Globals.Core.WorldFilter.CreateObject -= WorldFilter_CreateObject;
                }
                disposed = true;
            }
        }
    }
}
