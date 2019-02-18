using Decal.Adapter;
using Decal.Adapter.Wrappers;
using Mag.Shared.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VirindiViewService.Controls;

namespace UtilityBelt.Tools {
    public class InventoryManager : IDisposable {
        private const int THINK_INTERVAL = 300;

        private bool disposed = false;
        private bool isRunning = false;
        private bool isPaused = false;
        private bool isForced = false;
        private DateTime lastThought = DateTime.MinValue;
        private int movingObjectId = 0;

        HudCheckBox UIInventoryManagerAutoCram { get; set; }
        HudCheckBox UIInventoryManagerAutoStack { get; set; }
        HudCheckBox UIInventoryManagerDebug { get; set; }
        HudButton UIInventoryManagerTest { get; set; }

        // TODO: support AutoPack profiles when cramming
        public InventoryManager() {
            Globals.Core.CommandLineText += Current_CommandLineText;
            Globals.Core.WorldFilter.ChangeObject += WorldFilter_ChangeObject;
            Globals.Core.WorldFilter.CreateObject += WorldFilter_CreateObject;

            UIInventoryManagerTest = Globals.View.view != null ? (HudButton)Globals.View.view["InventoryManagerTest"] : new HudButton();
            UIInventoryManagerTest.Hit += UIInventoryManagerTest_Hit;

            UIInventoryManagerDebug = Globals.View.view != null ? (HudCheckBox)Globals.View.view["InventoryManagerDebug"] : new HudCheckBox();
            UIInventoryManagerDebug.Checked = Globals.Config.InventoryManager.Debug.Value;
            UIInventoryManagerDebug.Change += UIInventoryManagerDebug_Change;
            Globals.Config.InventoryManager.Debug.Changed += Config_InventoryManager_Debug_Changed;

            UIInventoryManagerAutoCram = Globals.View.view != null ? (HudCheckBox)Globals.View.view["InventoryManagerAutoCram"] : new HudCheckBox();
            UIInventoryManagerAutoCram.Checked = Globals.Config.InventoryManager.AutoCram.Value;
            UIInventoryManagerAutoCram.Change += UIInventoryManagerAutoCram_Change;
            Globals.Config.InventoryManager.AutoCram.Changed += Config_InventoryManager_AutoCram_Changed;

            UIInventoryManagerAutoStack = Globals.View.view != null ? (HudCheckBox)Globals.View.view["InventoryManagerAutoStack"] : new HudCheckBox();
            UIInventoryManagerAutoStack.Checked = Globals.Config.InventoryManager.AutoStack.Value;
            UIInventoryManagerAutoStack.Change += UIInventoryManagerAutoStack_Change;
            Globals.Config.InventoryManager.AutoStack.Changed += Config_InventoryManager_AutoStack_Changed;

            if (Globals.Config.InventoryManager.AutoCram.Value || Globals.Config.InventoryManager.AutoStack.Value) {
                Start();
            }
        }

        private void UIInventoryManagerTest_Hit(object sender, EventArgs e) {
            Start();
        }

        private void UIInventoryManagerDebug_Change(object sender, EventArgs e) {
            Globals.Config.InventoryManager.Debug.Value = UIInventoryManagerDebug.Checked;
        }

        private void Config_InventoryManager_Debug_Changed(Setting<bool> obj) {
            UIInventoryManagerDebug.Checked = Globals.Config.InventoryManager.Debug.Value;
        }

        private void UIInventoryManagerAutoCram_Change(object sender, EventArgs e) {
            Globals.Config.InventoryManager.AutoCram.Value = UIInventoryManagerAutoCram.Checked;
        }

        private void Config_InventoryManager_AutoCram_Changed(Setting<bool> obj) {
            UIInventoryManagerAutoCram.Checked = Globals.Config.InventoryManager.AutoCram.Value;
        }

        private void UIInventoryManagerAutoStack_Change(object sender, EventArgs e) {
            Globals.Config.InventoryManager.AutoStack.Value = UIInventoryManagerAutoStack.Checked;
        }

        private void Config_InventoryManager_AutoStack_Changed(Setting<bool> obj) {
            UIInventoryManagerAutoStack.Checked = Globals.Config.InventoryManager.AutoStack.Value;
        }

        private void Current_CommandLineText(object sender, ChatParserInterceptEventArgs e) {
            try {
                /*
                if (e.Text.StartsWith("/ub autocram")) {
                    bool force = e.Text.Contains("force");
                    e.Eat = true;

                    Start(force);

                    return;
                }
                */
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        private void WorldFilter_ChangeObject(object sender, ChangeObjectEventArgs e) {
            try {
                if (e.Change != WorldChangeType.StorageChange) return;

                if (movingObjectId == e.Changed.Id) {
                    movingObjectId = 0;
                }
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        private void WorldFilter_CreateObject(object sender, CreateObjectEventArgs e) {
            try {
                // created in main backpack?
                if (e.New.Container == Globals.Core.CharacterFilter.Id && !IsRunning()) {
                    Start();
                }
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        public void Start(bool force=false) {
            isRunning = true;
            isPaused = false;
            isForced = force;
            movingObjectId = 0;
            if (Globals.Config.InventoryManager.Debug.Value == true) {
                Util.WriteToChat("InventoryManager Started");
            }
        }

        public void Stop() {
            isForced = false;
            isRunning = false;
            movingObjectId = 0;
            if (Globals.Config.InventoryManager.Debug.Value == true) {
                Util.WriteToChat("InventoryManager Finished");
            }
        }

        public void Pause() {
            isPaused = true;
        }

        public void Resume() {
            isPaused = false;
        }

        public bool AutoCram() {
            if (Globals.Config.InventoryManager.Debug.Value == true) {
                Util.WriteToChat("InventoryManager::AutoCram started");
            }
            foreach (var wo in Globals.Core.WorldFilter.GetInventory()) {
                if (ShouldCramItem(wo) && wo.Values(LongValueKey.Container) == Globals.Core.CharacterFilter.Id) {
                    if (TryCramItem(wo)) return true;
                }
            }

            return false;
        }

        public bool AutoStack() {
            if (Globals.Config.InventoryManager.Debug.Value == true) {
                Util.WriteToChat("InventoryManager::AutoStack started");
            }
            foreach (var wo in Globals.Core.WorldFilter.GetInventory()) {
                if (wo != null && wo.Values(LongValueKey.StackMax, 1) > 1) {
                    if (TryStackItem(wo)) return true;
                }
            }

            return false;
        }

        public bool IsRunning() {
            return isRunning;
        }

        internal static bool ShouldCramItem(WorldObject wo) {
            if (wo == null) return false;

            // skip packs
            if (wo.ObjectClass == ObjectClass.Container) return false;

            // skip foci
            if (wo.ObjectClass == ObjectClass.Foci) return false;

            // skip equipped
            if (wo.Values(LongValueKey.EquippedSlots, 0) > 0) return false;

            // skip wielded
            if (wo.Values(LongValueKey.Slot, -1) == -1) return false;

            return true;
        }

        public void Think(bool force=false) {
            if (force || DateTime.UtcNow - lastThought > TimeSpan.FromMilliseconds(THINK_INTERVAL)) {
                lastThought = DateTime.UtcNow;

                // dont run automatically while vendoring
                if (Globals.Core.Actions.VendorId != 0) return;

                if ((!isRunning || isPaused) && !isForced) return;

                if (Globals.Config.InventoryManager.AutoCram.Value == true && AutoCram()) return;
                if (Globals.Config.InventoryManager.AutoStack.Value == true && AutoStack()) return;

                Stop();
            }
        }

        public bool TryCramItem(WorldObject stackThis) {
            // try to cram in side pack
            foreach (var container in Globals.Core.WorldFilter.GetInventory()) {
                int slot = container.Values(LongValueKey.Slot, -1);
                if (container.ObjectClass == ObjectClass.Container && slot >= 0) {
                    int freePackSpace = Util.GetFreePackSpace(container);

                    if (freePackSpace <= 0) continue;

                    if (Globals.Config.InventoryManager.Debug.Value == true) {
                        Util.WriteToChat(string.Format("AutoCram: trying to move {0} to {1}({2}) because it has {3} slots open",
                            Util.GetObjectName(stackThis.Id), container.Name, slot, freePackSpace));
                    }

                    movingObjectId = stackThis.Id;
                    Globals.Core.Actions.MoveItem(stackThis.Id, container.Id, slot, false);
                    return true;
                }
            }

            return false;
        }

        public bool TryStackItem(WorldObject stackThis) {
            int stackThisSize = stackThis.Values(LongValueKey.StackCount, 1);

            // try to stack in side pack
            foreach (var container in Globals.Core.WorldFilter.GetInventory()) {
                if (container.ObjectClass == ObjectClass.Container && container.Values(LongValueKey.Slot, -1) >= 0) {
                    foreach (var wo in Globals.Core.WorldFilter.GetByContainer(container.Id)) {
                        if (TryStackItemTo(wo, stackThis, container.Values(LongValueKey.Slot))) return true;
                    }
                }
            }

            // try to stack in main pack
            foreach (var wo in Globals.Core.WorldFilter.GetInventory()) {
                if (TryStackItemTo(wo, stackThis, 0)) return true;
            }

            return false;
        }

        public bool TryStackItemTo(WorldObject wo, WorldObject stackThis, int slot=0) {
            int woStackCount = wo.Values(LongValueKey.StackCount, 1);
            int woStackMax = wo.Values(LongValueKey.StackMax, 1);
            int stackThisCount = stackThis.Values(LongValueKey.StackCount, 1);

            // not stackable?
            if (woStackMax <= 1 || stackThis.Values(LongValueKey.StackMax, 1) <= 1) return false;

            if (wo.Name == stackThis.Name && wo.Id != stackThis.Id && stackThisCount < woStackMax) {
                if (woStackCount + stackThisCount <= woStackMax) {
                    if (Globals.Config.InventoryManager.Debug.Value == true) {
                        Util.WriteToChat(string.Format("InventoryManager::AutoStack stack {0}({1}) on {2}({3})",
                            Util.GetObjectName(stackThis.Id),
                            stackThisCount,
                            Util.GetObjectName(wo.Id),
                            woStackCount));
                    }
                    Globals.Core.Actions.SelectItem(stackThis.Id);
                    Globals.Core.Actions.MoveItem(stackThis.Id, wo.Container, slot, true);
                }
                else if (woStackMax - woStackCount == 0) {
                    return false;
                }
                else {
                    if (Globals.Config.InventoryManager.Debug.Value == true) {
                        Util.WriteToChat(string.Format("InventoryManager::AutoStack stack {0}({1}/{2}) on {3}({4})",
                            Util.GetObjectName(stackThis.Id),
                            woStackMax - woStackCount,
                            stackThisCount,
                            Util.GetObjectName(wo.Id),
                            woStackCount));
                    }
                    Globals.Core.Actions.SelectItem(stackThis.Id);
                    Globals.Core.Actions.SelectedStackCount = woStackMax - woStackCount;
                    Globals.Core.Actions.MoveItem(stackThis.Id, wo.Container, slot, true);
                }

                movingObjectId = stackThis.Id;
                return true;
            }

            return false;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    Globals.Core.CommandLineText -= Current_CommandLineText;
                    Globals.Core.WorldFilter.ChangeObject -= WorldFilter_ChangeObject;
                    Globals.Core.WorldFilter.CreateObject -= WorldFilter_CreateObject;
                }
                disposed = true;
            }
        }
    }
}
