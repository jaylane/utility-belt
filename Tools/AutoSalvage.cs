using Decal.Adapter;
using Decal.Adapter.Wrappers;
using Mag.Shared.Settings;
using System;
using System.Collections.Generic;
using VirindiViewService.Controls;

namespace UtilityBelt.Tools {
    class AutoSalvage : IDisposable {
        private List<int> inventoryItems = new List<int>();
        private List<int> salvageItemIds = new List<int>();
        private bool isRunning = false;
        private bool shouldSalvage = false;

        HudCheckBox UIAutoSalvageDebug { get; set; }
        HudCheckBox UIAutoSalvageThink { get; set; }
        HudButton UIAutoSalvageStart { get; set; }

        private DateTime lastThought = DateTime.MinValue;
        private DateTime lastAction = DateTime.MinValue;
        private bool readyToSalvage = false;
        private bool openedSalvageWindow = false;
        private bool disposed = false;

        public AutoSalvage() {
            Globals.Core.CommandLineText += Current_CommandLineText;

            UIAutoSalvageStart = Globals.MainView.view != null ? (HudButton)Globals.MainView.view["AutoSalvageStart"] : new HudButton();
            UIAutoSalvageStart.Hit += UIAutoSalvageStart_Hit;

            UIAutoSalvageDebug = Globals.MainView.view != null ? (HudCheckBox)Globals.MainView.view["AutoSalvageDebug"] : new HudCheckBox();
            UIAutoSalvageDebug.Checked = Globals.Config.AutoSalvage.Debug.Value;
            UIAutoSalvageDebug.Change += UIAutoSalvageDebug_Change;
            Globals.Config.AutoSalvage.Debug.Changed += Config_AutoSalvage_Debug_Changed;

            UIAutoSalvageThink = Globals.MainView.view != null ? (HudCheckBox)Globals.MainView.view["AutoSalvageThink"] : new HudCheckBox();
            UIAutoSalvageThink.Checked = Globals.Config.AutoSalvage.Think.Value;
            UIAutoSalvageThink.Change += UIAutoSalvageThink_Change;
            Globals.Config.AutoSalvage.Think.Changed += Config_AutoSalvage_Think_Changed;
        }

        private void UIAutoSalvageStart_Hit(object sender, EventArgs e) {
            try {
                Start();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UIAutoSalvageDebug_Change(object sender, EventArgs e) {
            Globals.Config.AutoSalvage.Debug.Value = UIAutoSalvageDebug.Checked;
        }

        private void Config_AutoSalvage_Debug_Changed(Setting<bool> obj) {
            UIAutoSalvageDebug.Checked = Globals.Config.AutoSalvage.Debug.Value;
        }

        private void UIAutoSalvageThink_Change(object sender, EventArgs e) {
            Globals.Config.AutoSalvage.Think.Value = UIAutoSalvageThink.Checked;
        }

        private void Config_AutoSalvage_Think_Changed(Setting<bool> obj) {
            UIAutoSalvageThink.Checked = Globals.Config.AutoSalvage.Think.Value;
        }

        private void Current_CommandLineText(object sender, ChatParserInterceptEventArgs e) {
            try {
                if (e.Text.StartsWith("/autosalvage") || e.Text.StartsWith("/ub autosalvage")) {
                    bool force = e.Text.Contains("force");
                    e.Eat = true;

                    Start(force);

                    return;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public void Start(bool force = false) {
            if (Globals.AutoVendor.HasVendorOpen() == false) {
                isRunning = true;
                openedSalvageWindow = false;
                shouldSalvage = force;

                Reset();
                LoadInventory();
            }
            else {
                Util.WriteToChat("AutoSalvage bailing, vendor is open.");
            }
        }

        private void Stop() {
            Reset();
            isRunning = false;

            if (Globals.Config.AutoSalvage.Think.Value == true) {
                Util.Think("AutoSalvage complete.");
            }
            else {
                Util.WriteToChat("AutoSalvage complete.");
            }
        }

        public void Reset() {
            inventoryItems.Clear();
            salvageItemIds.Clear();
            readyToSalvage = false;
        }

        public void LoadInventory() {
            var inventory = Globals.Core.WorldFilter.GetInventory();
            int requestedIdsCount = 0;

            foreach (var item in inventory) {
                inventoryItems.Add(item.Id);

                if (NeedsID(item.Id)) {
                    requestedIdsCount++;
                    Globals.Core.Actions.RequestId(item.Id);
                }
            }

            if (requestedIdsCount > 0) {
                Util.WriteToChat(String.Format("AutoSalvage: Requesting id data for {0}/{1} inventory items. This will take approximately {2} seconds.", requestedIdsCount, inventoryItems.Count, requestedIdsCount));
            }
        }

        private bool NeedsID(int id) {
            return uTank2.PluginCore.PC.FLootPluginQueryNeedsID(id);
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
            }

            return foundUst;
        }

        private List<int> GetSalvageIds() {
            var salvageIds = new List<int>();
            foreach (var id in inventoryItems) {
                try {
                    var result = uTank2.PluginCore.PC.FLootPluginClassifyImmediate(id);

                    var item = Globals.Core.WorldFilter[id];

                    if (item == null) continue;

                    if (!Util.ItemIsSafeToGetRidOf(item)) continue;

                    // If the item is equipped or wielded, don't process it.
                    if (item.Values(LongValueKey.EquippedSlots, 0) > 0 || item.Values(LongValueKey.Slot, -1) == -1)
                        continue;

                    // If the item is tinkered don't process it
                    if (item.Values(LongValueKey.NumberTimesTinkered, 0) > 1) continue;

                    // If the item is imbued don't process it
                    if (item.Values(LongValueKey.Imbued, 0) > 1) continue;

                    // dont put in bags of salvage
                    if (item.Name.StartsWith("Salvage")) continue;

                    if (result.SimpleAction == uTank2.eLootAction.Salvage) {
                        salvageIds.Add(id);

                        if (Globals.Config.AutoSalvage.Debug.Value == true) {
                            Util.WriteToChat(String.Format("AutoSalvage: Add: {0}", item.Name));
                        }
                    }
                }
                catch (Exception ex) { Logger.LogException(ex); }
            }

            return salvageIds;
        }

        private void AddSalvageToWindow() {
            var salvageIds = GetSalvageIds();

            Util.WriteToChat(String.Format("AutoSalvage: Found {0} items to salvage.", salvageIds.Count));
                
            // TODO: do multiple passes taking workmanship and loot rules into account
            foreach (var id in salvageIds) {
                Globals.Core.Actions.SalvagePanelAdd(id);
            }

            readyToSalvage = true;
        }

        public void Think() {
            if (DateTime.UtcNow - lastThought > TimeSpan.FromMilliseconds(1000)) {
                lastThought = DateTime.UtcNow;

                if (isRunning) {
                    bool hasAllItemData = true;

                    if (Globals.AutoVendor.HasVendorOpen() == false) {
                        Util.WriteToChat("AutoSalvage bailing, vendor is open.");
                        Stop();
                        return;
                    }

                    foreach (var id in inventoryItems) {
                        if (NeedsID(id)) {
                            hasAllItemData = false;
                            break;
                        }
                    }

                    if (readyToSalvage) {
                        readyToSalvage = false;
                        isRunning = false;

                        if (shouldSalvage) {
                            Globals.Core.Actions.SalvagePanelSalvage();
                        }

                        return;
                    }

                    if (isRunning && hasAllItemData) {
                        if (GetSalvageIds().Count == 0) {
                            if (Globals.Config.AutoSalvage.Debug.Value) {
                                Util.WriteToChat("AutoSalvage: No salvageable items found.");
                            }
                            Stop();
                            return;
                        }

                        if (openedSalvageWindow) {
                            AddSalvageToWindow();
                            return;
                        }

                        if (OpenSalvageWindow()) {
                            openedSalvageWindow = true;
                        }
                        else {
                            Stop();
                        }
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
                }
                disposed = true;
            }
        }
    }
}
