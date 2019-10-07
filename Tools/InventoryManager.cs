using Decal.Adapter;
using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VirindiViewService.Controls;

namespace UtilityBelt.Tools {
    public class InventoryManager : IDisposable {
        private const int THINK_INTERVAL = 300;
        private const int ITEM_BLACKLIST_TIMEOUT = 60; // in seconds
        private const int CONTAINER_BLACKLIST_TIMEOUT = 60; // in seconds

        private bool disposed = false;
        private bool isRunning = false;
        private bool isPaused = false;
        private bool isForced = false;
        private DateTime lastThought = DateTime.MinValue;
        private int movingObjectId = 0;
        private int tryCount = 0;
        private Dictionary<int, DateTime> blacklistedItems = new Dictionary<int, DateTime>();
        private Dictionary<int,DateTime> blacklistedContainers = new Dictionary<int, DateTime>();

        HudCheckBox UIInventoryManagerAutoCram { get; set; }
        HudCheckBox UIInventoryManagerAutoStack { get; set; }
        HudButton UIInventoryManagerTest { get; set; }

        // TODO: support AutoPack profiles when cramming
        public InventoryManager() {
            Globals.Core.CommandLineText += Current_CommandLineText;
            Globals.Core.WorldFilter.ChangeObject += WorldFilter_ChangeObject;
            Globals.Core.WorldFilter.CreateObject += WorldFilter_CreateObject;

            UIInventoryManagerTest = (HudButton)Globals.MainView.view["InventoryManagerTest"];
            UIInventoryManagerTest.Hit += UIInventoryManagerTest_Hit;

            UIInventoryManagerAutoCram = (HudCheckBox)Globals.MainView.view["InventoryManagerAutoCram"];
            UIInventoryManagerAutoCram.Change += UIInventoryManagerAutoCram_Change;

            UIInventoryManagerAutoStack = (HudCheckBox)Globals.MainView.view["InventoryManagerAutoStack"];
            UIInventoryManagerAutoStack.Change += UIInventoryManagerAutoStack_Change;

            Globals.Settings.InventoryManager.PropertyChanged += (s, e) => { UpdateUI(); };
        }

        private void UpdateUI() {
            UIInventoryManagerAutoStack.Checked = Globals.Settings.InventoryManager.AutoStack;
            UIInventoryManagerAutoCram.Checked = Globals.Settings.InventoryManager.AutoCram;
        }

        private void UIInventoryManagerTest_Hit(object sender, EventArgs e) {
            Start();
        }

        private void UIInventoryManagerAutoCram_Change(object sender, EventArgs e) {
            Globals.Settings.InventoryManager.AutoCram = UIInventoryManagerAutoCram.Checked;
        }

        private void UIInventoryManagerAutoStack_Change(object sender, EventArgs e) {
            Globals.Settings.InventoryManager.AutoStack = UIInventoryManagerAutoStack.Checked;
        }

        private void Current_CommandLineText(object sender, ChatParserInterceptEventArgs e) {
            try {
                if (e.Text.StartsWith("/ub autoinventory")) {
                    bool force = e.Text.Contains("force");
                    e.Eat = true;

                    Start(force);

                    return;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void WorldFilter_ChangeObject(object sender, ChangeObjectEventArgs e) {
            try {
                if (e.Change != WorldChangeType.StorageChange) return;

                if (movingObjectId == e.Changed.Id) {
                    tryCount = 0;
                    movingObjectId = 0;
                }
                else if (e.Changed.Container == Globals.Core.CharacterFilter.Id && !IsRunning()) {
                    //Start();
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void WorldFilter_CreateObject(object sender, CreateObjectEventArgs e) {
            try {
                // created in main backpack?
                if (e.New.Container == Globals.Core.CharacterFilter.Id && !IsRunning()) {
                    //Start();
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public void Start(bool force=false) {
            isRunning = true;
            isPaused = false;
            isForced = force;
            movingObjectId = 0;
            tryCount = 0;

            Logger.Debug("InventoryManager Started");

            CleanupBlacklists();
        }

        public void Stop() {
            isForced = false;
            isRunning = false;
            movingObjectId = 0;
            tryCount = 0;

            Util.Think("AutoInventory finished.");

            Logger.Debug("InventoryManager Finished");
        }

        public void Pause() {
            Logger.Debug("InventoryManager Paused");
            isPaused = true;
        }

        public void Resume() {
            Logger.Debug("InventoryManager Resumed");
            isPaused = false;
        }

        private void CleanupBlacklists() {
            var containerKeys = blacklistedContainers.Keys.ToArray();
            var itemKeys = blacklistedItems.Keys.ToArray();

            // containers
            foreach (var key in containerKeys) {
                if (blacklistedContainers.ContainsKey(key) && DateTime.UtcNow - blacklistedContainers[key] >= TimeSpan.FromSeconds(CONTAINER_BLACKLIST_TIMEOUT)) {
                    blacklistedContainers.Remove(key);
                }
            }

            // items
            foreach (var key in itemKeys) {
                if (blacklistedItems.ContainsKey(key) && DateTime.UtcNow - blacklistedItems[key] >= TimeSpan.FromSeconds(ITEM_BLACKLIST_TIMEOUT)) {
                    blacklistedItems.Remove(key);
                }
            }
        }

        public bool AutoCram(List<int> excludeList = null, bool excludeMoney=true) {
            Logger.Debug("InventoryManager::AutoCram started");

            foreach (var wo in Globals.Core.WorldFilter.GetInventory()) {
                if (excludeMoney && (wo.Values(LongValueKey.Type, 0) == 273/* pyreals */ || wo.ObjectClass == ObjectClass.TradeNote)) continue;
                if (excludeList != null && excludeList.Contains(wo.Id)) continue;
                if (blacklistedItems.ContainsKey(wo.Id)) continue;

                if (ShouldCramItem(wo) && wo.Values(LongValueKey.Container) == Globals.Core.CharacterFilter.Id) {
                    if (TryCramItem(wo)) return true;
                }
            }

            return false;
        }

        public bool AutoStack(List<int> excludeList = null) {
            Logger.Debug("InventoryManager::AutoStack started");

            foreach (var wo in Globals.Core.WorldFilter.GetInventory()) {
                if (excludeList != null && excludeList.Contains(wo.Id)) continue;

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

                // dont run while vendoring
                if (Globals.Core.Actions.VendorId != 0) return;

                if ((!isRunning || isPaused) && !isForced) return;

                if (Globals.Settings.InventoryManager.AutoCram == true && AutoCram()) return;
                if (Globals.Settings.InventoryManager.AutoStack == true && AutoStack()) return;

                Stop();
            }
        }

        public bool TryCramItem(WorldObject stackThis) {
            // try to cram in side pack
            foreach (var container in Globals.Core.WorldFilter.GetInventory()) {
                int slot = container.Values(LongValueKey.Slot, -1);
                if (container.ObjectClass == ObjectClass.Container && slot >= 0 && !blacklistedContainers.ContainsKey(container.Id)) {
                    int freePackSpace = Util.GetFreePackSpace(container);

                    if (freePackSpace <= 0) continue;

                    Logger.Debug(string.Format("AutoCram: trying to move {0} to {1}({2}) because it has {3} slots open",
                            Util.GetObjectName(stackThis.Id), container.Name, slot, freePackSpace));
                    
                    // blacklist this container
                    if (tryCount > 10) {
                        tryCount = 0;
                        blacklistedContainers.Add(container.Id, DateTime.UtcNow);
                        continue;
                    }

                    movingObjectId = stackThis.Id;
                    tryCount++;

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
                    if (blacklistedContainers.ContainsKey(container.Id)) continue;

                    foreach (var wo in Globals.Core.WorldFilter.GetByContainer(container.Id)) {
                        if (blacklistedItems.ContainsKey(stackThis.Id)) continue;
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
                // blacklist this item
                if (tryCount > 10) {
                    tryCount = 0;
                    if (!blacklistedItems.ContainsKey(stackThis.Id)) {
                        blacklistedItems.Add(stackThis.Id, DateTime.UtcNow);
                    }
                    return false;
                }

                if (woStackCount + stackThisCount <= woStackMax) {
                    Logger.Debug(string.Format("InventoryManager::AutoStack stack {0}({1}) on {2}({3})",
                            Util.GetObjectName(stackThis.Id),
                            stackThisCount,
                            Util.GetObjectName(wo.Id),
                            woStackCount));

                    Globals.Core.Actions.SelectItem(stackThis.Id);
                    Globals.Core.Actions.MoveItem(stackThis.Id, wo.Container, slot, true);
                }
                else if (woStackMax - woStackCount == 0) {
                    return false;
                }
                else {
                    Logger.Debug(string.Format("InventoryManager::AutoStack stack {0}({1}/{2}) on {3}({4})",
                            Util.GetObjectName(stackThis.Id),
                            woStackMax - woStackCount,
                            stackThisCount,
                            Util.GetObjectName(wo.Id),
                            woStackCount));

                    Globals.Core.Actions.SelectItem(stackThis.Id);
                    Globals.Core.Actions.SelectedStackCount = woStackMax - woStackCount;
                    Globals.Core.Actions.MoveItem(stackThis.Id, wo.Container, slot, true);
                }

                tryCount++;
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
