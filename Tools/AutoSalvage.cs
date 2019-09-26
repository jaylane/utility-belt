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

        private List<int> inventoryItems = new List<int>();
        private List<int> salvageItemIds = new List<int>();
        private List<int> blacklistedIds = new List<int>();
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

        private int lastSalvageId = 0;
        private int salvageRetryCount = 0;

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

                if (Globals.Config.AutoSalvage.Think.Value == true)
                {
                    Util.Think("AutoSalvage complete.");
                }
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

            return true;
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

                    if (!AllowedToSalvageItem(item)) continue;
                    if (blacklistedIds.Contains(id)) continue;

                    if (result.SimpleAction == uTank2.eLootAction.Salvage) {
                        salvageIds.Add(id);
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
                if (blacklistedIds.Contains(id)) continue;

                Globals.Core.Actions.SalvagePanelAdd(id);

                if (Globals.Config.AutoSalvage.Debug.Value == true) {
                    Util.WriteToChat(String.Format("AutoSalvage: Add: {0}", Util.GetObjectName(id)));
                }

                if (shouldSalvage) {
                    if (lastSalvageId == id) {
                        salvageRetryCount++;
                    }
                    else {
                        lastSalvageId = id;
                        salvageRetryCount = 1;
                    }

                    if (salvageRetryCount >= RETRY_COUNT) {
                        blacklistedIds.Add(id);
                    }

                    break;
                }
            }

            readyToSalvage = true;
        }

        public void Think() {
            if (DateTime.UtcNow - lastThought > TimeSpan.FromMilliseconds(600)) {
                lastThought = DateTime.UtcNow;

                if (isRunning) {
                    bool hasAllItemData = !Globals.Assessor.NeedsInventoryData(inventoryItems);

                    if (Globals.AutoVendor.HasVendorOpen()) {
                        Util.WriteToChat("AutoSalvage bailing, vendor is open.");
                        Stop();
                        return;
                    }

                    if (readyToSalvage) {
                        readyToSalvage = false;

                        if (shouldSalvage) {
                            Globals.Core.Actions.SalvagePanelSalvage();
                        }
                        else {
                            if ((Globals.Core.CharacterFilter.CharacterOptionFlags & (int)CharOptions2.SalvageMultiple) == 0) {
                                Util.WriteToChat("AutoSalvage: SalvageMultiple config option is turned off, so I can only add one item to the salvage window.");
                            }

                            Stop();
                        }

                        return;
                    }

                    if (isRunning && hasAllItemData) {
                        if (GetSalvageIds().Count == 0) {
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
