using Decal.Adapter;
using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.Linq;
using VirindiViewService.Controls;
using System.IO;
using System.Text.RegularExpressions;
using VTClassic;
using uTank2.LootPlugins;

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
        private Dictionary<int, DateTime> blacklistedContainers = new Dictionary<int, DateTime>();

        private static readonly List<int> giveObjects = new List<int>(), idItems = new List<int>();
        private static DateTime lastIdSpam = DateTime.MinValue, bailTimer = DateTime.MinValue, startGive, reloadLootProfileTS = DateTime.MinValue;
        private static bool igRunning = false, givePartialItem, isRegex = false, reloadLootProfile = false;
        private static int currentItem, retryCount, destinationId, failedItems, totalFailures, maxGive, itemsGiven, lastIdCount, pendingGiveCount, currentSplitItem;
        private LootCore lootProfile = null;
        private static string targetPlayer = "", utlProfile = "", profilePath = "";
        private static readonly Dictionary<string, int> givenItemsCount = new Dictionary<string, int>();
        private static readonly Queue<KeyValuePair<int, int>> splitQueue = new Queue<KeyValuePair<int, int>>();

        private static FileSystemWatcher profilesWatcher = null;

        // TODO: support AutoPack profiles when cramming
        public InventoryManager() {
            profilePath = Path.Combine(Util.GetPluginDirectory(), "itemgiver");
            Directory.CreateDirectory(profilePath);

            Globals.Core.CommandLineText += Current_CommandLineText;
            Globals.Core.WorldFilter.ChangeObject += WorldFilter_ChangeObject;
            Globals.Core.WorldFilter.CreateObject += WorldFilter_CreateObject;

            if (Globals.Settings.InventoryManager.WatchLootProfile) WatchLootProfile_Changed(true);
        }

        private static readonly Regex giveRegex = new Regex(@"^\/ub give(?<flags>[pPr]*) ?(?<giveCount>\d+)? (?<itemName>.+) to (?<targetPlayer>.+)");
        private static readonly Regex igRegex = new Regex(@"^\/ub ig(?<partial>p)? ?(?<utlProfile>.+) to (?<targetPlayer>.+)");
        private void Current_CommandLineText(object sender, ChatParserInterceptEventArgs e) {
            try {
                if (e.Text.StartsWith("/ub autoinventory")) {
                    bool force = e.Text.Contains("force");
                    e.Eat = true;

                    Start(force);

                    return;
                }

                if (!e.Text.StartsWith("/ub ig") && !e.Text.StartsWith("/ub give"))
                    return;
                e.Eat = true;
                Match giveMatch = giveRegex.Match(e.Text);
                Match igMatch = igRegex.Match(e.Text);

                if (giveMatch.Success) {
                    StartGive(giveMatch);
                } else if (igMatch.Success) {
                    StartIG(igMatch);
                } else if (e.Text.EndsWith(" stop") || e.Text.EndsWith(" abort") || e.Text.EndsWith(" quit")) {
                    IGStop();
                    return;
                } else if (e.Text.StartsWith("/ub ig")) {
                    Util.WriteToChat("Usage: /ub ig stop\n" +
                                     "       /ub ig[p] <profile[.utl]> to <character|selected>\n" +
                                     "       p: use partial name match for character");
                } else if (e.Text.StartsWith("/ub give")) {
                    Util.WriteToChat("Usage: /ub give stop\n" +
                                     "       /ub give[[Pr]p] [count] <itemName> to <character|selected>\n" +
                                     "       P: use partial name match for itemName\n" +
                                     "       r: use regex inplace of itemName\n" +
                                     "       p: use partial name match for character\n" +
                                     "       count: if omitted, or less than 1, gives all items that match");
                }

            } catch (Exception ex) { Logger.LogException(ex); }
        }
        #region Loot Profile Watcher

        // Handle settings changes while running
        public static void WatchLootProfile_Changed(bool enabled) {
            if (VTankControl.vTankInstance == null && enabled) {
                Util.WriteToChat("Error accessing VTank");
                Globals.Settings.InventoryManager.WatchLootProfile = false;
                return;
            }
            string profilePath = Util.GetVTankProfilesDirectory();
            if (!Directory.Exists(profilePath) && enabled) {
                Logger.Debug($"WatchLootProfile_Changed(true) Error: {profilePath} does not exist!");
                Globals.Settings.InventoryManager.WatchLootProfile = false;
                return;
            }
            if (profilesWatcher != null)
                profilesWatcher.Dispose();
            if (enabled) {
                string loadedProfile = VTankControl.vTankInstance.GetLootProfile();
                profilesWatcher = new FileSystemWatcher();
                profilesWatcher.NotifyFilter = NotifyFilters.LastWrite;
                profilesWatcher.Changed += LootProfile_Changed;
                profilesWatcher.Filter = loadedProfile;
                profilesWatcher.Path = profilePath;
                profilesWatcher.EnableRaisingEvents = true;
                uTank2.PluginCore.PC.LootProfileChanged += PC_LootProfileChanged;
                Logger.Debug($"FileSystemWatcher enabled on Path={profilePath},Filter={loadedProfile}");
            } else {
                uTank2.PluginCore.PC.LootProfileChanged -= PC_LootProfileChanged;
            }
        }

        private static void PC_LootProfileChanged() {
            string loadedProfile = VTankControl.vTankInstance.GetLootProfile();
            profilesWatcher.Filter = loadedProfile;
        }

        private static void LootProfile_Changed(object sender, FileSystemEventArgs e) {
            reloadLootProfile = true;
            reloadLootProfileTS = DateTime.UtcNow;
        }
        #endregion
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
            if (!isPaused && (Globals.Settings.InventoryManager.AutoCram || Globals.Settings.InventoryManager.AutoStack))
                Logger.Debug("InventoryManager Paused");
            isPaused = true;
        }

        public void Resume() {
            if (isPaused && (Globals.Settings.InventoryManager.AutoCram || Globals.Settings.InventoryManager.AutoStack))
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
                if (excludeMoney && (wo.Values(LongValueKey.Type, 0) == 273/* pyreals */ || wo.ObjectClass == Decal.Adapter.Wrappers.ObjectClass.TradeNote)) continue;
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
            if (wo.ObjectClass == Decal.Adapter.Wrappers.ObjectClass.Container) return false;

            // skip foci
            if (wo.ObjectClass == Decal.Adapter.Wrappers.ObjectClass.Foci) return false;

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
            if (reloadLootProfile && DateTime.UtcNow - reloadLootProfileTS > TimeSpan.FromSeconds(2)) {
                reloadLootProfile = false;
                VTankControl.vTankInstance.LoadLootProfile(VTankControl.vTankInstance.GetLootProfile());
            }
            if (!igRunning)
                return;
            try {
                if (VTankControl.navBlockedUntil < DateTime.UtcNow + TimeSpan.FromSeconds(1)) { //if itemgiver is running, and nav block has less than a second remaining, refresh it
                    VTankControl.Nav_Block(30000, Globals.Settings.Plugin.Debug);
                    VTankControl.Item_Block(30000, false);
                }

                if (idItems.Count > 0 && DateTime.UtcNow - lastIdSpam > TimeSpan.FromSeconds(10)) {
                    lastIdSpam = DateTime.UtcNow;
                    var thisIdCount = idItems.Count;
                    Util.WriteToChat(string.Format("ItemGiver waiting to id {0} items, this will take approximately {0} seconds.", thisIdCount));
                    if (lastIdCount != thisIdCount) { // if count has changed, reset bail timer
                        lastIdCount = thisIdCount;
                        bailTimer = DateTime.UtcNow;
                    }
                }

                if (Globals.Core.Actions.BusyState == 0) {
                    if (Globals.Core.WorldFilter[destinationId] == null) {
                        Logger.Debug($"ItemGiver {targetPlayer} vanished!");
                        IGStop();
                        return;
                    }
                    if (idItems.Count > 0)
                        GetIGItems();

                    itemsGiven += giveObjects.RemoveAll(x => (Globals.Core.WorldFilter[x] == null) || (Globals.Core.WorldFilter[x].Container == -1));

                    if (splitQueue.Count > 0 && TrySplitStack())
                        return;

                    foreach (int item in giveObjects) {
                        if (item != currentItem) {
                            retryCount = 0;
                            bailTimer = DateTime.UtcNow;
                        }
                        currentItem = item;

                        retryCount++;
                        totalFailures++;
                        Globals.Core.Actions.GiveItem(item, destinationId);
                        if (retryCount > Globals.Settings.InventoryManager.IGBusyCount) {
                            giveObjects.Remove(item);
                            Logger.Debug($"unable to give {Util.GetObjectName(item)}");
                            failedItems++;
                        }

                        if (failedItems > Globals.Settings.InventoryManager.IGFailure) IGStop();

                        return;
                    }

                    if (giveObjects.Count == 0 && idItems.Count == 0 && splitQueue.Count == 0) {
                        IGStop();
                    }
                }
                if (DateTime.UtcNow - bailTimer > TimeSpan.FromSeconds(10)) {
                    Util.WriteToChat("ItemGiver bail, Timeout expired");
                    IGStop();
                }
            } catch (Exception ex) { Logger.LogException(ex); }




        }

        public bool TryCramItem(WorldObject stackThis) {
            // try to cram in side pack
            foreach (var container in Globals.Core.WorldFilter.GetInventory()) {
                int slot = container.Values(LongValueKey.Slot, -1);
                if (container.ObjectClass == Decal.Adapter.Wrappers.ObjectClass.Container && slot >= 0 && !blacklistedContainers.ContainsKey(container.Id)) {
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
                if (container.ObjectClass == Decal.Adapter.Wrappers.ObjectClass.Container && container.Values(LongValueKey.Slot, -1) >= 0) {
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
        private void StartGive(Match giveMatch) {
            if (igRunning) {
                Util.WriteToChat("ItemGiver is already running.  Please wait until it completes or use /ub give stop to quit previous session");
                return;
            }
            VTankControl.Nav_Block(1000, false); // quick block to keep vtank from truckin' off before the profile loads, but short enough to not matter if it errors out and doesn't unlock
            targetPlayer = giveMatch.Groups["targetPlayer"].Value;
            var destination = Globals.Misc.FindName(targetPlayer, (giveMatch.Groups["flags"].Value.Contains("p") ? true : false), new Decal.Adapter.Wrappers.ObjectClass[] { Decal.Adapter.Wrappers.ObjectClass.Player, Decal.Adapter.Wrappers.ObjectClass.Npc });

            if (destination == null) {
                Util.WriteToChat($"ItemGiver: player {targetPlayer} not found");
                return;
            }
            destinationId = destination.Id;

            if (destinationId == Globals.Core.CharacterFilter.Id) {
                Util.WriteToChat("ItemGiver: You can't give to yourself");
                return;
            }

            var playerDistance = (float)Globals.Core.WorldFilter.Distance(Globals.Core.CharacterFilter.Id, destinationId) * 240;
            if (playerDistance > Globals.Settings.InventoryManager.IGRange) {
                Util.WriteToChat($"ItemGiver: {targetPlayer} is {playerDistance:n2} meters away");
                return;
            }
            isRegex = (giveMatch.Groups["flags"].Value.Contains("r") ? true : false);
            givePartialItem = (giveMatch.Groups["flags"].Value.Contains("P") ? true : false);
            int.TryParse(giveMatch.Groups["giveCount"].Value, out maxGive);
            if (maxGive < 1)
                maxGive = int.MaxValue;

            if (isRegex)
                utlProfile = giveMatch.Groups["itemName"].Value; //NOT a profile name. just re-purposing this.
            else
                utlProfile = giveMatch.Groups["itemName"].Value.ToLower(); //NOT a profile name. just re-purposing this.


            Logger.Debug($"ItemGiver GIVE {(maxGive == int.MaxValue ? "∞" : maxGive.ToString())} {(givePartialItem ? "(partial)" : "")}{utlProfile} to {Globals.Core.WorldFilter[destinationId].Name}");
            VTankControl.Nav_Block(30000, Globals.Settings.Plugin.Debug);
            VTankControl.Item_Block(30000, false);
            GetGiveItems();

            lastIdCount = int.MaxValue;
            startGive = bailTimer = DateTime.UtcNow;
            igRunning = true;


        }
        private void StartIG(Match igMatch) {
            if (igRunning) {
                Util.WriteToChat("ItemGiver is already running.  Please wait until it completes or use /ub ig stop to quit previous session");
                return;
            }
            VTankControl.Nav_Block(1000, false); // quick block to keep vtank from truckin' off before the profile loads, but short enough to not matter if it errors out and doesn't unlock
            targetPlayer = igMatch.Groups["targetPlayer"].Value;

            var destination = Globals.Misc.FindName(targetPlayer, (igMatch.Groups["partial"].Value.Equals("p") ? true : false), new Decal.Adapter.Wrappers.ObjectClass[] { Decal.Adapter.Wrappers.ObjectClass.Player, Decal.Adapter.Wrappers.ObjectClass.Npc });

            if (destination == null) {
                Util.WriteToChat($"ItemGiver: player {targetPlayer} not found");
                return;
            }
            destinationId = destination.Id;

            if (destinationId == Globals.Core.CharacterFilter.Id) {
                Util.WriteToChat("You can't give to yourself");
                return;
            }

            var playerDistance = Globals.Core.WorldFilter.Distance(Globals.Core.CharacterFilter.Id, destinationId) * 240;
            if (playerDistance > Globals.Settings.InventoryManager.IGRange) {
                Util.WriteToChat($"ItemGiver {targetPlayer} is {playerDistance:n2} meters away");
                return;
            }

            utlProfile = igMatch.Groups["utlProfile"].Value;

            if (!File.Exists(Path.Combine(profilePath, utlProfile))) {
                if (File.Exists(Path.Combine(profilePath, utlProfile + ".utl"))) {
                    utlProfile += ".utl";
                } else {
                    Util.WriteToChat($"Profile does not exist: {utlProfile} ({profilePath})");
                    return;
                }
            }

            if (lootProfile == null) {
                var hasLootCore = false;
                try {
                    lootProfile = new LootCore();
                    hasLootCore = true;
                } catch (Exception ex) { Logger.LogException(ex); }

                if (!hasLootCore) {
                    Util.WriteToChat("Unable to load VTClassic, something went wrong.");
                    return;
                }
            }

            lootProfile.LoadProfile(Path.Combine(profilePath, utlProfile), false);

            VTankControl.Nav_Block(30000, Globals.Settings.Plugin.Debug);
            VTankControl.Item_Block(30000, false);
            lastIdCount = int.MaxValue;
            GetIGItems();

            startGive = bailTimer = DateTime.UtcNow;
            igRunning = true;
        }

        private void IGStop() {
            if (!igRunning) {
                Util.WriteToChat("ItemGiver is not running.");
                return;
            }

            Util.ThinkOrWrite($"ItemGiver finished: {utlProfile} to {targetPlayer}. took {Util.GetFriendlyTimeDifference(DateTime.UtcNow - startGive)} to give {itemsGiven} item(s). {totalFailures - itemsGiven}", Globals.Settings.InventoryManager.IGThink);
            VTankControl.Nav_UnBlock();
            VTankControl.Item_UnBlock();

            itemsGiven = totalFailures = failedItems = pendingGiveCount = 0;
            igRunning = isRegex = false;
            lootProfile = null;
            givenItemsCount.Clear();
            giveObjects.Clear();
            idItems.Clear();
            splitQueue.Clear();
        }

        private void GetGiveItems() {
            try {
                Regex itemre = new Regex(utlProfile);
                foreach (WorldObject item in Globals.Core.WorldFilter.GetInventory()) {

                    if (pendingGiveCount >= maxGive)
                    {
                        Logger.Debug($"Max give ({maxGive}) reached, breaking");
                        break;
                    }

                    // Util.WriteToChat($"Processing 0x{item.Id:X8} {item.Name}");

                    if (item.Values(LongValueKey.EquippedSlots, 0) > 0 || item.Values(LongValueKey.Slot, -1) == -1) // If the item is equipped or wielded, don't process it.
                        continue;

                    if (item.Container == -1) // Silly GDLE.... sigh.
                        continue;
                    if (isRegex) {
                        if (!itemre.IsMatch(Util.GetObjectName(item.Id)))
                            continue;
                    } else if (givePartialItem) {
                        if (!Util.GetObjectName(item.Id).ToLower().Contains(utlProfile))
                            continue;
                    } else if (!Util.GetObjectName(item.Id).ToLower().Equals(utlProfile))
                        continue;

                    if (Globals.Settings.InventoryManager.TreatStackAsSingleItem)
                    {
                        // Util.WriteToChat($"       Adding {item.Name}");
                        giveObjects.Add(item.Id);
                        pendingGiveCount++;
                    }
                    else
                    {
                        var stackCount = item.Values(LongValueKey.StackCount, 1);
                        if (stackCount > maxGive - pendingGiveCount)
                        {
                            splitQueue.Enqueue(new KeyValuePair<int, int>(item.Id, maxGive - pendingGiveCount));
                            pendingGiveCount = maxGive;
                        }
                        else
                        {
                            Logger.Debug($"Giving {stackCount} * {item.Name}");
                            giveObjects.Add(item.Id);
                            pendingGiveCount += stackCount;
                        }
                    }
                }
            } catch (Exception ex) { Logger.LogException(ex); }
        }

        private void GetIGItems() {
            try {
                foreach (WorldObject item in Globals.Core.WorldFilter.GetInventory()) {
                    if (giveObjects.Contains(item.Id)) // already in the list
                        continue;

                    if (item.Values(LongValueKey.EquippedSlots, 0) > 0 || item.Values(LongValueKey.Slot, -1) == -1) // If the item is equipped or wielded, don't process it.
                        continue;

                    if (item.Container == -1) // Silly GDLE.... sigh.
                        continue;

                    GameItemInfo itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithID(item.Id);
                    if (itemInfo == null) // This happens all the time for aetheria that has been converted
                        continue;

                    if (!item.HasIdData && lootProfile.DoesPotentialItemNeedID(itemInfo)) {
                        if (!idItems.Contains(item.Id)) {
                            Globals.Assessor.Queue(item.Id);
                            idItems.Add(item.Id);
                        }
                        continue;
                    }

                    if (!item.HasIdData) {
                        if (lootProfile.DoesPotentialItemNeedID(itemInfo)) {
                            if (!idItems.Contains(item.Id)) {
                                Globals.Assessor.Queue(item.Id);
                                idItems.Add(item.Id);
                            }
                            continue;
                        }
                    } else if (idItems.Contains(item.Id))
                        idItems.Remove(item.Id);


                    LootAction result = lootProfile.GetLootDecision(itemInfo);
                    if (!result.IsKeep && !result.IsKeepUpTo) {
                        continue;
                    }

                    if (result.IsKeepUpTo) {
                        if (!givenItemsCount.ContainsKey(result.RuleName)) {
                            givenItemsCount.Add(result.RuleName, 0);
                        }

                        var stackCount = Globals.Settings.InventoryManager.TreatStackAsSingleItem ? 1 : Globals.Core.WorldFilter[item.Id].Values(LongValueKey.StackCount, 1);
                        if (result.Data1 < 0) // Keep this many
                        {
                            // Keep matches until we have kept the KeepUpTo #
                            if (givenItemsCount[result.RuleName] < Math.Abs(result.Data1))
                            {
                                Logger.Debug($"Need to keep: {Math.Abs(result.Data1) - givenItemsCount[result.RuleName]}");
                                if (!Globals.Settings.InventoryManager.TreatStackAsSingleItem &&
                                    stackCount > Math.Abs(result.Data1) - givenItemsCount[result.RuleName])
                                {
                                    int splitCount = stackCount - (Math.Abs(result.Data1) - givenItemsCount[result.RuleName]);
                                    splitQueue.Enqueue(new KeyValuePair<int, int>(item.Id, splitCount));
                                    givenItemsCount[result.RuleName] += Math.Abs(result.Data1) - givenItemsCount[result.RuleName];
                                }
                                else
                                {
                                    Logger.Debug($"Keeping: {Util.GetObjectName(item.Id)} ({stackCount})");
                                    givenItemsCount[result.RuleName] += stackCount;
                                }

                                continue;
                            }
                        }
                        else // Give this many
                        {
                            // Keep if already given KeepUpTo #
                            if (givenItemsCount[result.RuleName] >= result.Data1)
                            {
                                continue;
                            }

                            if (!Globals.Settings.InventoryManager.TreatStackAsSingleItem && stackCount > result.Data1 - givenItemsCount[result.RuleName])
                            {
                                int neededCount = result.Data1 - givenItemsCount[result.RuleName];
                                splitQueue.Enqueue(new KeyValuePair<int, int>(item.Id, neededCount));
                                givenItemsCount[result.RuleName] += neededCount;

                                continue;
                            }
                            else
                                givenItemsCount[result.RuleName] += stackCount;
                        }
                    }

                    giveObjects.Add(item.Id);
                }
                idItems.RemoveAll(x => (Globals.Core.WorldFilter[x] == null) || (Globals.Core.WorldFilter[x].Container == -1)); // Remove items from IDQueue that no longer exist
            } catch (Exception ex) { Logger.LogException(ex); }
        }

        public bool TrySplitStack()
        {
            var item = splitQueue.Peek();
            if (item.Key == currentSplitItem)
                return false;
            var wo = Globals.Core.WorldFilter[item.Key];
            if (wo != null)
            {
                var splitCount = item.Value;
                var stackCount = wo.Values(LongValueKey.StackCount, 1);

                EventHandler<CreateObjectEventArgs> splitHandler = null;
                splitHandler = (sender, e) =>
                {
                    if (igRunning &&
                        e.New.Name == wo.Name &&
                        e.New.Type == wo.Type &&
                        e.New.Values(LongValueKey.StackCount, 1) == splitCount)
                    {
                        Logger.Debug($"Adding to give list: {Util.GetObjectName(e.New.Id)}");
                        giveObjects.Add(e.New.Id);
                        splitQueue.Dequeue();
                    }
                    Globals.Core.WorldFilter.CreateObject -= splitHandler;
                };

                currentSplitItem = item.Key;
                Globals.Core.Actions.SelectItem(wo.Id);
                Globals.Core.Actions.SelectedStackCount = splitCount;
                Globals.Core.WorldFilter.CreateObject += splitHandler;
                Globals.Core.Actions.MoveItem(wo.Id, Globals.Core.CharacterFilter.Id, 0, false);

                Logger.Debug(string.Format("ItemGiver Splitting {0}. old: {1} new: {2}",
                    Util.GetObjectName(wo.Id),
                    stackCount,
                    splitCount));

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
                    if (profilesWatcher != null) {
                        profilesWatcher.Dispose();
                        uTank2.PluginCore.PC.LootProfileChanged -= PC_LootProfileChanged;
                    }
                }
                disposed = true;
            }
        }
    }
}
