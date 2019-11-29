using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UtilityBelt.Lib;

namespace UtilityBelt.Tools {
    [Name("EquipmentManager")]
    public class EquipmentManager : ToolBase {
        private static readonly ObjectClass[] ValidEquippableObjectClasses = new[] {
                        ObjectClass.Armor,
                        ObjectClass.Clothing,
                        ObjectClass.Gem, // Aetheria
                        ObjectClass.Jewelry,
                        ObjectClass.MeleeWeapon,
                        ObjectClass.MissileWeapon,
                        ObjectClass.WandStaffOrb
                    };

        private enum RunningState
        {
            Idle,
            Dequipping,
            Equipping,
            Creating,
            Test
        }

        private RunningState state = RunningState.Idle;
        private string currentProfileName = null;
        private object lootProfile = null;
        private DateTime bailTimer = DateTime.MinValue;
        private int currentEquipAttempts = 0;
        private readonly Stopwatch timer = new Stopwatch();
        private readonly List<WorldObject> equippableItems = new List<WorldObject>();
        private int pendingIdCount = 0;
        private DateTime lastIdSpam = DateTime.MinValue;
        private Queue<int> itemList = new Queue<int>();
        private int lastEquippingItem = 0;

        #region Config
        [Summary("Think to yourself when done equipping items")]
        [DefaultValue(false)]
        public bool Think {
            get => (bool)GetSetting("Think");
            set => UpdateSetting("Think", value);
        }
        #endregion

        #region Commands
        #region /ub equip
        [Summary("Commands to manage your equipment.")]
        [Usage("/ub equip {list | load <lootProfile> | test <lootProfile> | create <lootProfile>}")]
        [Example("/ub equip load profile.utl", "Equips all items matching profile.utl.")]
        [Example("/ub equip list", "Lists available equipment profiles.")]
        [Example("/ub equip test profile.utl", "Test equipping profile.utl")]
        [CommandPattern("equip", @"^ *(?<Verb>(load|list|test|create)) *(?<Profile>.*) *$")]
        public void DoEquip(string command, Match args) {
                    var verb = args.Groups["Verb"].Value;
                    var profileName = args.Groups["Profile"].Value;

                    switch (verb) {
                        case "create":
                            Start(profileName, RunningState.Creating);
                            break;
                        case "load":
                            Start(profileName, RunningState.Dequipping);
                            break;

                        case "list":
                            WriteToChat("Equip Profiles");
                            foreach (var p in GetProfiles(profileName)) {
                                Util.WriteToChat($" * {Path.GetFileName(p)} ({Path.GetDirectoryName(p)})");
                            }
                            break;

                        case "test":
                            Start(profileName, RunningState.Test);
                            break;

                        default:
                            LogError($"Unknown verb: {verb}");
                            break;
                    }
        }

        #endregion
        #endregion

        public EquipmentManager(UtilityBeltPlugin ub, string name) : base(ub, name) {
            try {
                Directory.CreateDirectory(Path.Combine(Util.GetPluginDirectory(), "equip"));
                Directory.CreateDirectory(Path.Combine(Util.GetCharacterDirectory(), "equip"));
                Directory.CreateDirectory(Path.Combine(Util.GetServerDirectory(), "equip"));

                // TODO: be selective about when to subscribe to RenderFrame
                UB.Core.RenderFrame += Core_RenderFrame;
                UB.Core.WorldFilter.ChangeObject += WorldFilter_ChangeObject;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private bool IsValidItem(int item, out WorldObject wo) {
            wo = null;

            if (item == 0 || !UB.Core.Actions.IsValidObject(item)) {
                return false;
            }

            wo = UB.Core.WorldFilter[item];
            if (wo == null || wo.Values(LongValueKey.EquipableSlots, 0) == 0) {
                return false;
            }

            if (wo.Container != UB.Core.CharacterFilter.Id) {
                if (!UB.Core.Actions.IsValidObject(wo.Container)) {
                    return false;
                }

                var container = UB.Core.WorldFilter[wo.Container];
                if (container == null || container.Container != UB.Core.CharacterFilter.Id) {
                    return false;
                }
            }

            return true;
        }

        private void WorldFilter_ChangeObject(object sender, ChangeObjectEventArgs e) {
            try {
                if (state != RunningState.Equipping)
                    return;
                if (e.Change != WorldChangeType.StorageChange)
                    return;

                //LogDebug($"Change object fired - {Util.GetObjectName(e.Changed.Id)}");
                if (itemList.Count > 0 && e.Changed.Id == itemList.Peek() && e.Changed.Values(LongValueKey.Slot, -1) == -1) {
                    LogDebug("Removing item from queue");
                    itemList.Dequeue();
                    bailTimer = DateTime.UtcNow;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void CreateEquipProfile(string profileName = "") {
            try {
                if (string.IsNullOrEmpty(profileName))
                    profileName = UB.Core.CharacterFilter.Name + ".utl";

                var path = Path.Combine(Path.Combine(Util.GetCharacterDirectory(), "equip"), profileName);
                var items = GetEquippedItems();

                LogDebug($"Creating loot profile for {items.Count} items at {path}");
                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(fs, System.Text.Encoding.UTF8, 65535)) {
                    LogDebug("Writing file header");
                    writer.WriteLine("UTL");
                    writer.WriteLine("1"); // Version
                    writer.WriteLine(items.Count);

                    foreach (var equippedItem in items) {
                        LogDebug($"Writing loot rule for {equippedItem.Name}");
                        writer.WriteLine(Util.GetObjectName(equippedItem.Id));
                        writer.WriteLine(); // Custom Expression

                        var material = equippedItem.Values(LongValueKey.Material, 0);

                        // big line
                        writer.Write("0;1;12;12;12;12");
                        if (material != 0)
                            writer.Write(";12");
                        writer.WriteLine();

                        WriteKeyValue(writer, equippedItem, LongValueKey.EquipableSlots);
                        WriteKeyValue(writer, equippedItem, LongValueKey.Type);
                        WriteKeyValue(writer, equippedItem, LongValueKey.Icon);
                        WriteKeyValue(writer, equippedItem, LongValueKey.Value);
                        if (material != 0)
                            WriteKeyValue(writer, equippedItem, LongValueKey.Material);
                    }

                    LogDebug("Writing SalvageCombine Block");
                    writer.WriteLine("SalvageCombine");
                    var sb = new StringBuilder();
                    sb.AppendLine("1"); // version
                    sb.AppendLine(); // Default Combine String
                    sb.AppendLine("0"); // Combine Strings Count
                    sb.AppendLine("0"); // Combine Values Up To Count

                    writer.WriteLine(sb.Length);
                    writer.Write(sb.ToString());

                    writer.Flush();
                }

                Util.WriteToChat($"Profile created at {path}");
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private static void WriteKeyValue(StreamWriter writer, WorldObject equippedItem, LongValueKey key) {
            var sb = new StringBuilder();
            sb.Append(equippedItem.Values(key, 0)).AppendLine().Append((int)key).AppendLine();
            writer.WriteLine(sb.Length);
            writer.Write(sb.ToString());
        }

        private List<WorldObject> GetEquippedItems() {
            var list = new List<WorldObject>();
            foreach (var item in UB.Core.WorldFilter.GetInventory())
            {
                if (item.Values(LongValueKey.Slot, -1) == -1)
                {
                    list.Add(item);
                }
            }
            return list;
        }

        private IEnumerable<string> GetProfiles(string profileName = "") {
            var charPath = Path.Combine(Util.GetCharacterDirectory(), "equip");
            var mainPath = Path.Combine(Util.GetPluginDirectory(), "equip");
            var serverPath = Path.Combine(Util.GetServerDirectory(), "equip");

            var charFiles = Directory.GetFiles(charPath, string.IsNullOrEmpty(profileName) ? "*.utl" : profileName);
            var serverFiles = Directory.GetFiles(serverPath, string.IsNullOrEmpty(profileName) ? "*.utl" : profileName);
            var mainFiles = Directory.GetFiles(mainPath, string.IsNullOrEmpty(profileName) ? "*.utl" : profileName);

            return charFiles.Concat(serverFiles).Concat(mainFiles);
        }

        private void Start(string profileName, RunningState startState) {
            try {
                LogDebug($"Executing equip load command with profile '{profileName}'");
                state = startState;
                currentProfileName = profileName;
                bailTimer = DateTime.UtcNow;
                itemList = null;

                timer.Reset();
                timer.Start();

                if (state != RunningState.Creating) {
                    var hasLootCore = false;
                    if (lootProfile == null) {
                        try {
                            lootProfile = new VTClassic.LootCore();
                            hasLootCore = true;
                        }
                        catch (Exception ex) { Logger.LogException(ex); }

                        if (!hasLootCore) {
                            Util.WriteToChat("Unable to load VTClassic, something went wrong.");
                            return;
                        }
                    }

                    var profilePath = GetProfilePath(string.IsNullOrEmpty(profileName) ? (UB.Core.CharacterFilter.Name + ".utl") : profileName);

                    if (!File.Exists(profilePath)) {
                        LogDebug("No equip profile exists: " + profilePath);
                        Stop();
                        return;
                    }

                    // Load our loot profile
                    ((VTClassic.LootCore)lootProfile).LoadProfile(profilePath, false);
                }

                equippableItems.AddRange(GetEquippableItems());
                LogDebug($"Found {equippableItems.Count} equippable items");
                if (UB.Assessor.NeedsInventoryData(equippableItems.Select(i => i.Id))) {
                    pendingIdCount = UB.Assessor.GetNeededIdCount(equippableItems.Select(i => i.Id));
                    UB.Assessor.RequestAll(equippableItems.Select(i => i.Id));
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private string GetProfilePath(string profileName) {
            var charPath = Path.Combine(Util.GetCharacterDirectory(), "equip");
            var mainPath = Path.Combine(Util.GetPluginDirectory(), "equip");
            var serverPath = Path.Combine(Util.GetServerDirectory(), "equip");

            if (File.Exists(Path.Combine(charPath, profileName))) {
                return Path.Combine(charPath, profileName);
            }
            else if (File.Exists(Path.Combine(serverPath, profileName))) {
                return Path.Combine(serverPath, profileName);
            }
            else if (File.Exists(Path.Combine(mainPath, profileName))) {
                return Path.Combine(mainPath, profileName);
            }
            else if (File.Exists(Path.Combine(charPath, "default.utl"))) {
                return Path.Combine(charPath, "default.utl");
            }
            else if (File.Exists(Path.Combine(serverPath, "default.utl"))) {
                return Path.Combine(serverPath, "default.utl");
            }
            else if (File.Exists(Path.Combine(mainPath, "default.utl"))) {
                return Path.Combine(mainPath, "default.utl");
            }
            return Path.Combine(mainPath, profileName);
        }

        private void Stop() {
            try {
                if (state == RunningState.Equipping || state == RunningState.Dequipping) {
                    Util.ThinkOrWrite($"Equipment Manager: Finished equipping items in {timer.Elapsed.TotalSeconds}s", Think);
                }

                if (state != RunningState.Creating && lootProfile != null)
                    ((VTClassic.LootCore)lootProfile).UnloadProfile();
                if (state == RunningState.Equipping || state == RunningState.Dequipping) {
                    VTankControl.Nav_UnBlock();
                    VTankControl.Item_UnBlock();
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
            finally {
                state = RunningState.Idle;
                itemList = null;
                currentEquipAttempts = 0;
                equippableItems.Clear();
                lastEquippingItem = 0;
                currentProfileName = null;
            }
        }

        public void Core_RenderFrame(object sender, EventArgs e) {
            if (state == RunningState.Idle)
                return;

            if (state == RunningState.Dequipping || state == RunningState.Equipping)
                CheckVTankNavBlock();

            if (HasPendingIdItems())
                return;

            if (state == RunningState.Creating) {
                CreateEquipProfile(currentProfileName);
                Stop();
                return;
            }

            if (itemList == null) {
                itemList = new Queue<int>();
                foreach (var id in CheckUtl())
                    itemList.Enqueue(id);

                LogDebug($"Found {itemList.Count} items to equip");
            }

            if (state == RunningState.Test) {
                Util.WriteToChat("Will attempt to equip the following items in order:");
                while (itemList.Count > 0) {
                    var item = itemList.Dequeue();
                    Util.WriteToChat($" * {Util.GetObjectName(item)} <{item}>");
                }

                Stop();
                return;
            }

            if (UB.Core.Actions.BusyState == 0) {
                if (state == RunningState.Dequipping) {
                    if (TryDequipItem())
                        return;

                    LogDebug("Finished dequipping items");
                    state = RunningState.Equipping;
                }

                if (itemList.Count == 0) {
                    LogDebug("Item queue empty - stopping");
                    Stop();
                    return;
                }

                if (!TryGetItemToEquip(out var wo))
                    return;

                if (lastEquippingItem != itemList.Peek()) {
                    lastEquippingItem = itemList.Peek();
                    currentEquipAttempts = 0;
                }
                else if (++currentEquipAttempts > 15) // Stop trying to make fetch happen
                {
                    LogDebug($"Too many equip attempts ({Util.GetObjectName(itemList.Peek())}) - SKIPPING");
                    currentEquipAttempts = 0;
                    itemList.Dequeue();
                    return;
                }

                EquipItem(wo);
            }

            if (DateTime.UtcNow - bailTimer > TimeSpan.FromSeconds(10)) {
                WriteToChat($"bail, timeout expired");
                Stop();
            }
        }

        private void EquipItem(WorldObject wo) {
            try {
                LogDebug($"Attempting to equip item - {Util.GetObjectName(wo.Id)}");
                UB.Core.Actions.UseItem(wo.Id, 0);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void CheckVTankNavBlock() {
            try {
                if (VTankControl.navBlockedUntil < DateTime.UtcNow + TimeSpan.FromSeconds(1)) { //if equip is running, and nav block has less than a second remaining, refresh it
                    VTankControl.Nav_Block(30000, UB.Plugin.Debug);
                    VTankControl.Item_Block(30000, false);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private bool TryDequipItem() {
            try {
                foreach (var item in UB.Core.WorldFilter.GetInventory()) {
                    // skip items in profile since they are going to be equipped
                    if (itemList.Contains(item.Id))
                        continue;

                    if (item.Values(LongValueKey.Slot, -1) == -1) {
                        LogDebug($"Dequipping item - {Util.GetObjectName(item.Id)}");
                        UB.Core.Actions.MoveItem(item.Id, UB.Core.CharacterFilter.Id);
                        return true;
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return false;
        }

        private bool TryGetItemToEquip(out WorldObject wo) {
            wo = null;
            try {
                while (wo == null || wo.Values(LongValueKey.Slot, -1) == -1) {
                    if (!IsValidItem(itemList.Peek(), out wo) || wo == null) {
                        LogError($"Could not find item with id: {wo.Id} - SKIPPING");
                        itemList.Dequeue();
                        return false;
                    }
                    else if (wo.Values(LongValueKey.Slot, -1) == -1) {
                        LogDebug($"Item already equipped: {wo.Id} - SKIPPING");
                        itemList.Dequeue();
                        if (itemList.Count == 0) {
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return true;
        }

        private IEnumerable<int> CheckUtl() {
            List<int> ids = new List<int>();
            try {
                foreach (var item in equippableItems) {
                    LogDebug($"Assessing {Util.GetObjectName(item.Id)}...");
                    uTank2.LootPlugins.GameItemInfo itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithID(item.Id);
                    if (itemInfo == null) continue;
                    uTank2.LootPlugins.LootAction result = ((VTClassic.LootCore)lootProfile).GetLootDecision(itemInfo);
                    if (result.IsKeep) {
                        LogDebug($"Matches rule: {result.RuleName}");
                        ids.Add(item.Id);
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return ids;
        }

        private IEnumerable<WorldObject> GetEquippableItems() {

            foreach (var item in UB.Core.WorldFilter.GetInventory()) {
                if (!ValidEquippableObjectClasses.Contains(item.ObjectClass)) {
                    continue;
                }
                if (item.Values(LongValueKey.EquipableSlots, 0) == 0)
                    continue;

                yield return item;
            }
        }

        private bool HasPendingIdItems() {
            try {
                if (pendingIdCount > 0) {
                    var idCount = UB.Assessor.GetNeededIdCount(equippableItems.Select(i => i.Id));
                    if (idCount != pendingIdCount) {
                        pendingIdCount = idCount;
                        bailTimer = DateTime.UtcNow;
                    }

                    if (idCount > 0) {
                        if (DateTime.UtcNow - lastIdSpam > TimeSpan.FromSeconds(15)) {
                            WriteToChat(string.Format("waiting to id {0} items, this will take approximately {0} seconds.", idCount));
                            lastIdSpam = DateTime.UtcNow;
                        }

                        return true;
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return false;
        }

        #region IDisposable Support
        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    UB.Core.RenderFrame -= Core_RenderFrame;
                    UB.Core.WorldFilter.ChangeObject -= WorldFilter_ChangeObject;
                    base.Dispose(disposing);
                }

                disposedValue = true;
            }
        }
        #endregion
    }
}
