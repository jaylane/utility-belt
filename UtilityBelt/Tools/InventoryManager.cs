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
using System.Diagnostics;
using UtilityBelt.Lib;
using System.ComponentModel;

namespace UtilityBelt.Tools {
    [Name("InventoryManager")]
    public class InventoryManager : ToolBase {
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

        private static readonly Dictionary<int, int> giveObjects = new Dictionary<int, int>();
        private static readonly List<int> idItems = new List<int>();
        private static DateTime lastIdSpam = DateTime.MinValue, bailTimer = DateTime.MinValue, reloadLootProfileTS = DateTime.MinValue;
        private static bool igRunning = false, givePartialItem, isRegex = false, reloadLootProfile = false;
        private static int currentItem, retryCount, destinationId, failedItems, totalFailures, maxGive, itemsGiven, lastIdCount, pendingGiveCount;
        private LootCore lootProfile = null;
        private static string targetPlayer = "", utlProfile = "", profilePath = "";
        private static readonly Dictionary<string, int> givenItemsCount = new Dictionary<string, int>();
        private Stopwatch giveTimer;

        private static FileSystemWatcher profilesWatcher = null;

        #region Config
        [Summary("Automatically cram items into side packs")]
        [DefaultValue(false)]
        public bool AutoCram {
            get { return (bool)GetSetting("AutoCram"); }
            set { UpdateSetting("AutoCram", value); }
        }

        [Summary("Automatically combine stacked items")]
        [DefaultValue(false)]
        public bool AutoStack {
            get { return (bool)GetSetting("AutoStack"); }
            set { UpdateSetting("AutoStack", value); }
        }

        [Summary("Think to yourself when ItemGiver Finishes")]
        [DefaultValue(false)]
        public bool IGThink {
            get { return (bool)GetSetting("IGThink"); }
            set { UpdateSetting("IGThink", value); }
        }
        [Summary("Item Failure Count to fail ItemGiver")]
        [DefaultValue(3)]
        public int IGFailure {
            get { return (int)GetSetting("IGFailure"); }
            set { UpdateSetting("IGFailure", value); }
        }
        [Summary("Busy Count to fail ItemGiver give")]
        [DefaultValue(10)]
        public int IGBusyCount {
            get { return (int)GetSetting("IGBusyCount"); }
            set { UpdateSetting("IGBusyCount", value); }
        }
        [Summary("Maximum Range for ItemGiver commands")]
        [DefaultValue(15f)]
        public float IGRange {
            get { return (float)GetSetting("IGRange"); }
            set { UpdateSetting("IGRange", value); }
        }
        [Summary("Treat stacks as single item")]
        [DefaultValue(true)]
        public bool TreatStackAsSingleItem {
            get { return (bool)GetSetting("TreatStackAsSingleItem"); }
            set { UpdateSetting("TreatStackAsSingleItem", value); }
        }
        [Summary("Watch VTank Loot Profile for changes, and reload")]
        [DefaultValue(false)]
        public bool WatchLootProfile {
            get { return (bool)GetSetting("WatchLootProfile"); }
            set {
                UpdateSetting("WatchLootProfile", value);
                WatchLootProfile_Changed(value);
            }
        }
        #endregion

        #region Commands
        #region /ub give
        [Summary("Gives items matching the provided name to a player.")]
        [Usage("/ub give[p{P|r}] [itemCount] <itemName> to <target>")]
        [Example("/ub givep 10 Prismatic to Zero Cool", "Gives 10 items partially matching the name \"Prismatic\" to Zero Cool")]
        [Example("/ub giveP 10 Prismatic Tapers to Zero", "Gives 10 Prismatic Tapers to a character with a name partially matching \"Zero\"")]
        [Example("/ub give Hero Token to Zero Cool", "Gives all Hero Tokens to Zero Cool")]
        [Example("/ub giver Hero.* to Zero Cool", "Gives all items matching the regex \"Hero.*\" to Zero Cool")]
        [CommandPattern("give", @"^ *((?<Count>\d+)? ?(?<Item>.+) to (?<Target>.+)|(?<StopCommand>stop|cancel|quit|abort))$", true)]
        public void DoGive(string command, Match args) {
            if (!string.IsNullOrEmpty(args.Groups["StopCommand"].Value)) {
                IGStop();
            }
            else {
                StartGive(command, args);
            }
        }
        #endregion

        #region /ub ig
        [Summary("Gives items matching the provided loot profile to a player.")]
        [Usage("/ub ig[p] <lootProfile> to <target>")]
        [Example("/ub ig muledItems.utl to Zero Cool", "Gives all items matching Keep rules in muledItems.utl to Zero Cool")]
        [Example("/ub igp muledItems.utl to Zero", "Gives all items matching Keep rules in muledItems.utl to a character partially matching the name Zero")]
        [CommandPattern("ig", @"^ *(?<utlProfile>.+) to (?<Target>.+)|(?<StopCommand>(cancel|stop|abort|quit))$")]
        public void DoItemGiver(string command, Match args) {
            if (!string.IsNullOrEmpty(args.Groups["StopCommand"].Value)) {
                IGStop();
            }
            else {
                StartIG(command, args);
            }
        }
        #endregion
        #endregion

        // TODO: support AutoPack profiles when cramming
        public InventoryManager(UtilityBeltPlugin ub, string name) : base(ub, name) {
            profilePath = Path.Combine(Util.GetPluginDirectory(), "itemgiver");
            Directory.CreateDirectory(profilePath);

            UB.Core.WorldFilter.ChangeObject += WorldFilter_ChangeObject;
            UB.Core.WorldFilter.CreateObject += WorldFilter_CreateObject;
            UB.Core.RenderFrame += Core_RenderFrame;

            if (WatchLootProfile) WatchLootProfile_Changed(true);
        }

        #region Loot Profile Watcher

        // Handle settings changes while running
        public void WatchLootProfile_Changed(bool enabled) {
            if (VTankControl.vTankInstance == null && enabled) {
                LogError("Error accessing VTank");
                WatchLootProfile = false;
                return;
            }
            string profilePath = Util.GetVTankProfilesDirectory();
            if (!Directory.Exists(profilePath) && enabled) {
                LogError($"WatchLootProfile_Changed(true) Error: {profilePath} does not exist!");
                WatchLootProfile = false;
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
                LogDebug($"FileSystemWatcher enabled on Path={profilePath},Filter={loadedProfile}");
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
            try
            {
                if (igRunning && e.Changed.Id == currentItem && (e.Change == WorldChangeType.SizeChange || e.Changed.Container == -1))
                {
                    giveObjects.Remove(e.Changed.Id);
                }

                if (e.Change != WorldChangeType.StorageChange) return;

                if (movingObjectId == e.Changed.Id) {
                    tryCount = 0;
                    movingObjectId = 0;
                }
                else if (e.Changed.Container == UB.Core.CharacterFilter.Id && !IsRunning()) {
                    //Start();
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void WorldFilter_CreateObject(object sender, CreateObjectEventArgs e) {
            try {
                // created in main backpack?
                if (e.New.Container == UB.Core.CharacterFilter.Id && !IsRunning()) {
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

            LogDebug("Started");

            CleanupBlacklists();
        }

        public void Stop() {
            isForced = false;
            isRunning = false;
            movingObjectId = 0;
            tryCount = 0;

            ChatThink("Finished.");
            LogDebug("Finished");
        }

        public void Pause() {
            if (!isPaused && (AutoCram || AutoStack))
                LogDebug("Paused");
            isPaused = true;
        }

        public void Resume() {
            if (isPaused && (AutoCram || AutoStack))
                LogDebug("Resumed");
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

        public bool DoAutoCram(List<int> excludeList = null, bool excludeMoney=true) {
            LogDebug("AutoCram started");

            foreach (var wo in UB.Core.WorldFilter.GetInventory()) {
                if (excludeMoney && (wo.Values(LongValueKey.Type, 0) == 273/* pyreals */ || wo.ObjectClass == Decal.Adapter.Wrappers.ObjectClass.TradeNote)) continue;
                if (excludeList != null && excludeList.Contains(wo.Id)) continue;
                if (blacklistedItems.ContainsKey(wo.Id)) continue;

                if (ShouldCramItem(wo) && wo.Values(LongValueKey.Container) == UB.Core.CharacterFilter.Id) {
                    if (TryCramItem(wo)) return true;
                }
            }

            return false;
        }

        public bool DoAutoStack(List<int> excludeList = null) {
            LogDebug("AutoStack started");

            foreach (var wo in UB.Core.WorldFilter.GetInventory()) {
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

        public void Core_RenderFrame(object sender, EventArgs e) {
            if (DateTime.UtcNow - lastThought > TimeSpan.FromMilliseconds(THINK_INTERVAL)) {
                lastThought = DateTime.UtcNow;

                // dont run while vendoring
                if (UB.Core.Actions.VendorId != 0) return;

                if ((!isRunning || isPaused) && !isForced) return;

                if (AutoCram == true && DoAutoCram()) return;
                if (AutoStack == true && DoAutoStack()) return;

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
                    VTankControl.Nav_Block(30000, UB.Plugin.Debug);
                    VTankControl.Item_Block(30000, false);
                }

                if (idItems.Count > 0 && DateTime.UtcNow - lastIdSpam > TimeSpan.FromSeconds(10)) {
                    lastIdSpam = DateTime.UtcNow;
                    var thisIdCount = idItems.Count;
                    WriteToChat(string.Format("ItemGiver waiting to id {0} items, this will take approximately {0} seconds.", thisIdCount));
                    if (lastIdCount != thisIdCount) { // if count has changed, reset bail timer
                        lastIdCount = thisIdCount;
                        bailTimer = DateTime.UtcNow;
                    }
                }

                if (UB.Core.Actions.BusyState == 0) {
                    if (UB.Core.WorldFilter[destinationId] == null) {
                        LogDebug($"ItemGiver {targetPlayer} vanished!");
                        IGStop();
                        return;
                    }

                    if (idItems.Count > 0)
                        GetIGItems();

                    var invalidItems = giveObjects.Where(x => (UB.Core.WorldFilter[x.Key] == null) || (UB.Core.WorldFilter[x.Key].Container == -1)).ToArray();
                    foreach (var item in invalidItems)
                    {
                        itemsGiven++;
                        giveObjects.Remove(item.Key);
                    }

                    foreach (var item in giveObjects) {
                        if (item.Key != currentItem) {
                            retryCount = 0;
                            bailTimer = DateTime.UtcNow;
                        }
                        currentItem = item.Key;

                        retryCount++;
                        totalFailures++;
                        Logger.Debug($"Attempting to give {Util.GetObjectName(item.Key)} <{item.Key}> * {item.Value}");
                        if (item.Value > 0)
                        {
                            UB.Core.Actions.SelectItem(item.Key);
                            UB.Core.Actions.SelectedStackCount = item.Value;
                        }
                        UB.Core.Actions.GiveItem(item.Key, destinationId);
                        if (retryCount > IGBusyCount) {
                            giveObjects.Remove(item.Key);
                            LogDebug($"unable to give {Util.GetObjectName(item.Key)}");
                            failedItems++;
                        }

                        if (failedItems > IGFailure) IGStop();

                        return;
                    }

                    if (giveObjects.Count == 0 && idItems.Count == 0) {
                        IGStop();
                    }
                }
                if (DateTime.UtcNow - bailTimer > TimeSpan.FromSeconds(10)) {
                    LogError("ItemGiver bail, Timeout expired");
                    IGStop();
                }
            } catch (Exception ex) { Logger.LogException(ex); }
        }

        public bool TryCramItem(WorldObject stackThis) {
            // try to cram in side pack
            foreach (var container in UB.Core.WorldFilter.GetInventory()) {
                int slot = container.Values(LongValueKey.Slot, -1);
                if (container.ObjectClass == Decal.Adapter.Wrappers.ObjectClass.Container && slot >= 0 && !blacklistedContainers.ContainsKey(container.Id)) {
                    int freePackSpace = Util.GetFreePackSpace(container);

                    if (freePackSpace <= 0) continue;

                    LogDebug(string.Format("AutoCram: trying to move {0} to {1}({2}) because it has {3} slots open",
                            Util.GetObjectName(stackThis.Id), container.Name, slot, freePackSpace));
                    
                    // blacklist this container
                    if (tryCount > 10) {
                        tryCount = 0;
                        blacklistedContainers.Add(container.Id, DateTime.UtcNow);
                        continue;
                    }

                    movingObjectId = stackThis.Id;
                    tryCount++;

                    UB.Core.Actions.MoveItem(stackThis.Id, container.Id, slot, false);
                    return true;
                }
            }

            return false;
        }

        public bool TryStackItem(WorldObject stackThis) {
            int stackThisSize = stackThis.Values(LongValueKey.StackCount, 1);

            // try to stack in side pack
            foreach (var container in UB.Core.WorldFilter.GetInventory()) {
                if (container.ObjectClass == Decal.Adapter.Wrappers.ObjectClass.Container && container.Values(LongValueKey.Slot, -1) >= 0) {
                    if (blacklistedContainers.ContainsKey(container.Id)) continue;

                    foreach (var wo in UB.Core.WorldFilter.GetByContainer(container.Id)) {
                        if (blacklistedItems.ContainsKey(stackThis.Id)) continue;
                        if (TryStackItemTo(wo, stackThis, container.Values(LongValueKey.Slot))) return true;
                    }
                }
            }

            // try to stack in main pack
            foreach (var wo in UB.Core.WorldFilter.GetInventory()) {
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
                    LogDebug(string.Format("AutoStack stack {0}({1}) on {2}({3})",
                            Util.GetObjectName(stackThis.Id),
                            stackThisCount,
                            Util.GetObjectName(wo.Id),
                            woStackCount));

                    UB.Core.Actions.SelectItem(stackThis.Id);
                    UB.Core.Actions.MoveItem(stackThis.Id, wo.Container, slot, true);
                }
                else if (woStackMax - woStackCount == 0) {
                    return false;
                }
                else {
                    LogDebug(string.Format("AutoStack stack {0}({1}/{2}) on {3}({4})",
                            Util.GetObjectName(stackThis.Id),
                            woStackMax - woStackCount,
                            stackThisCount,
                            Util.GetObjectName(wo.Id),
                            woStackCount));

                    UB.Core.Actions.SelectItem(stackThis.Id);
                    UB.Core.Actions.SelectedStackCount = woStackMax - woStackCount;
                    UB.Core.Actions.MoveItem(stackThis.Id, wo.Container, slot, true);
                }

                tryCount++;
                movingObjectId = stackThis.Id;
                return true;
            }

            return false;
        }
        private void StartGive(string command, Match giveMatch) {
            if (igRunning) {
                LogError("Already running.  Please wait until it completes or use /ub give stop to quit previous session");
                return;
            }
            var flags = command.Replace("give", "");
            VTankControl.Nav_Block(1000, false); // quick block to keep vtank from truckin' off before the profile loads, but short enough to not matter if it errors out and doesn't unlock
            targetPlayer = giveMatch.Groups["Target"].Value;
            var destination = UB.Plugin.FindName(targetPlayer, flags.Contains("p"), new Decal.Adapter.Wrappers.ObjectClass[] { Decal.Adapter.Wrappers.ObjectClass.Player, Decal.Adapter.Wrappers.ObjectClass.Npc });

            if (destination == null) {
                LogError($"player {targetPlayer} not found");
                return;
            }
            destinationId = destination.Id;

            if (destinationId == UB.Core.CharacterFilter.Id) {
                LogError("You can't give to yourself");
                return;
            }

            var playerDistance = (float)UB.Core.WorldFilter.Distance(UB.Core.CharacterFilter.Id, destinationId) * 240;
            if (playerDistance > IGRange) {
                LogError($"{targetPlayer} is {playerDistance:n2} meters away, IGRange is set to {IGRange}. bailing.");
                return;
            }
            isRegex = flags.Contains("r");
            givePartialItem = flags.Contains("P");
            int.TryParse(giveMatch.Groups["Count"].Value, out maxGive);
            if (maxGive < 1)
                maxGive = int.MaxValue;

            if (isRegex)
                utlProfile = giveMatch.Groups["Item"].Value; //NOT a profile name. just re-purposing this.
            else
                utlProfile = giveMatch.Groups["Item"].Value.ToLower(); //NOT a profile name. just re-purposing this.


            LogDebug($"ItemGiver GIVE {(maxGive == int.MaxValue ? "∞" : maxGive.ToString())} {(givePartialItem ? "(partial)" : "")}{utlProfile} to {UB.Core.WorldFilter[destinationId].Name}");
            VTankControl.Nav_Block(30000, UB.Plugin.Debug);
            VTankControl.Item_Block(30000, false);
            GetGiveItems();

            lastIdCount = int.MaxValue;
            bailTimer = DateTime.UtcNow;
            giveTimer = Stopwatch.StartNew();
            igRunning = true;
        }
        private void StartIG(string command, Match igMatch) {
            if (igRunning) {
                LogError("Already running.  Please wait until it completes or use /ub ig stop to quit previous session");
                return;
            }
            var flags = command.Replace("ig", "");
            VTankControl.Nav_Block(1000, false); // quick block to keep vtank from truckin' off before the profile loads, but short enough to not matter if it errors out and doesn't unlock
            targetPlayer = igMatch.Groups["Target"].Value;

            var destination = UB.Plugin.FindName(targetPlayer, flags.Equals("p"), new Decal.Adapter.Wrappers.ObjectClass[] { Decal.Adapter.Wrappers.ObjectClass.Player, Decal.Adapter.Wrappers.ObjectClass.Npc });

            if (destination == null) {
                LogError($"player {targetPlayer} not found");
                return;
            }
            destinationId = destination.Id;

            if (destinationId == UB.Core.CharacterFilter.Id) {
                LogError("You can't give to yourself");
                return;
            }

            var playerDistance = UB.Core.WorldFilter.Distance(UB.Core.CharacterFilter.Id, destinationId) * 240;
            if (playerDistance > IGRange) {
                LogError($"ItemGiver {targetPlayer} is {playerDistance:n2} meters away. IGRange is set to {IGRange}");
                return;
            }

            utlProfile = igMatch.Groups["utlProfile"].Value;

            if (!File.Exists(Path.Combine(profilePath, utlProfile))) {
                if (File.Exists(Path.Combine(profilePath, utlProfile + ".utl"))) {
                    utlProfile += ".utl";
                } else {
                    LogError($"ItemGiver Profile does not exist: {utlProfile} ({profilePath})");
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
                    LogError("ItemGiver unable to load VTClassic, something went wrong.");
                    return;
                }
            }

            lootProfile.LoadProfile(Path.Combine(profilePath, utlProfile), false);

            VTankControl.Nav_Block(30000, UB.Plugin.Debug);
            VTankControl.Item_Block(30000, false);
            lastIdCount = int.MaxValue;
            GetIGItems();

            bailTimer = DateTime.UtcNow;
            giveTimer = Stopwatch.StartNew();
            igRunning = true;
        }

        private void IGStop() {
            if (!igRunning) {
                Util.WriteToChat("ItemGiver is not running.");
                return;
            }

            Util.ThinkOrWrite($"ItemGiver finished: {utlProfile} to {targetPlayer}. took {Util.GetFriendlyTimeDifference(giveTimer.Elapsed)} to give {itemsGiven} item(s). {totalFailures - itemsGiven}", IGThink);
            VTankControl.Nav_UnBlock();
            VTankControl.Item_UnBlock();

            itemsGiven = totalFailures = failedItems = pendingGiveCount = 0;
            igRunning = isRegex = false;
            lootProfile = null;
            givenItemsCount.Clear();
            giveObjects.Clear();
            idItems.Clear();
        }

        private void GetGiveItems() {
            try {
                Regex itemre = new Regex(utlProfile);
                foreach (WorldObject item in UB.Core.WorldFilter.GetInventory()) {

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

                    if (TreatStackAsSingleItem) {
                        // Util.WriteToChat($"       Adding {item.Name}");
                        giveObjects.Add(item.Id, 0);
                        pendingGiveCount++;
                    }
                    else {
                        var stackCount = item.Values(LongValueKey.StackCount, 1);
                        if (stackCount > maxGive - pendingGiveCount) {
                            giveObjects.Add(item.Id, maxGive - pendingGiveCount);
                            pendingGiveCount = maxGive;
                        }
                        else {
                            LogDebug($"Giving {stackCount} * {item.Name}");
                            giveObjects.Add(item.Id, 0);
                            pendingGiveCount += stackCount;
                        }
                    }
                }
            } catch (Exception ex) { Logger.LogException(ex); }
        }

        private void GetIGItems() {
            try {
                foreach (WorldObject item in UB.Core.WorldFilter.GetInventory()) {
                    if (giveObjects.ContainsKey(item.Id)) // already in the list
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
                            UB.Assessor.Queue(item.Id);
                            idItems.Add(item.Id);
                        }
                        continue;
                    }

                    if (!item.HasIdData) {
                        if (lootProfile.DoesPotentialItemNeedID(itemInfo)) {
                            if (!idItems.Contains(item.Id)) {
                                UB.Assessor.Queue(item.Id);
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

                        var stackCount = TreatStackAsSingleItem ? 1 : UB.Core.WorldFilter[item.Id].Values(LongValueKey.StackCount, 1);
                        if (result.Data1 < 0) { // Keep this many
                            // Keep matches until we have kept the KeepUpTo #
                            if (givenItemsCount[result.RuleName] < Math.Abs(result.Data1)) {
                                LogDebug($"Need to keep: {Math.Abs(result.Data1) - givenItemsCount[result.RuleName]}");
                                if (!TreatStackAsSingleItem && stackCount > Math.Abs(result.Data1) - givenItemsCount[result.RuleName]) {
                                    int splitCount = stackCount - (Math.Abs(result.Data1) - givenItemsCount[result.RuleName]);
                                    giveObjects.Add(item.Id, splitCount);
                                    givenItemsCount[result.RuleName] += Math.Abs(result.Data1) - givenItemsCount[result.RuleName];
                                }
                                else {
                                    LogDebug($"Keeping: {Util.GetObjectName(item.Id)} ({stackCount})");
                                    givenItemsCount[result.RuleName] += stackCount;
                                }

                                continue;
                            }
                        }
                        else { // Give this many
                            // Keep if already given KeepUpTo #
                            if (givenItemsCount[result.RuleName] >= result.Data1) {
                                continue;
                            }

                            if (!TreatStackAsSingleItem && stackCount > result.Data1 - givenItemsCount[result.RuleName]) {
                                int neededCount = result.Data1 - givenItemsCount[result.RuleName];
                                giveObjects.Add(item.Id, neededCount);
                                givenItemsCount[result.RuleName] += neededCount;

                                continue;
                            }
                            else
                                givenItemsCount[result.RuleName] += stackCount;
                        }
                    }

                    giveObjects.Add(item.Id, 0);
                }
                idItems.RemoveAll(x => (UB.Core.WorldFilter[x] == null) || (UB.Core.WorldFilter[x].Container == -1)); // Remove items from IDQueue that no longer exist
            } catch (Exception ex) { Logger.LogException(ex); }
        }
        protected override void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    if (UB.Core != null) {
                        if (UB.Core.WorldFilter != null) {
                            UB.Core.WorldFilter.ChangeObject -= WorldFilter_ChangeObject;
                            UB.Core.WorldFilter.CreateObject -= WorldFilter_CreateObject;
                        }
                    }
                    if (profilesWatcher != null) {
                        profilesWatcher.Dispose();
                        if (uTank2.PluginCore.PC != null) {
                            uTank2.PluginCore.PC.LootProfileChanged -= PC_LootProfileChanged;
                        }
                    }

                    base.Dispose(disposing);
                }
                disposed = true;
            }
        }
    }
}
