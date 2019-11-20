using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace UtilityBelt.Tools
{
    public class EquipmentManager : IDisposable
    {
        private bool running = false;
        private object lootProfile = null;
        private DateTime bailTimer = DateTime.MinValue;
        private bool dequippingState = false;
        private int currentEquipAttempts = 0;
        private readonly Stopwatch timer = new Stopwatch();
        private readonly List<WorldObject> equippableItems = new List<WorldObject>();
        private int pendingIdCount = 0;
        private DateTime lastIdSpam = DateTime.MinValue;
        private Queue<int> itemList = new Queue<int>();
        private int lastEquippingItem = 0;
        private bool isTestMode = false;

        public EquipmentManager()
        {
            try
            {
                Directory.CreateDirectory(Path.Combine(Util.GetPluginDirectory(), "equip"));
                Directory.CreateDirectory(Path.Combine(Util.GetCharacterDirectory(), "equip"));
                Directory.CreateDirectory(Path.Combine(Util.GetServerDirectory(), "equip"));

                Globals.Core.CommandLineText += Core_CommandLineText;
                Globals.Core.WorldFilter.ChangeObject += WorldFilter_ChangeObject;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private bool IsValidItem(int item, out WorldObject wo)
        {
            wo = null;

            if (item == 0 || !Globals.Core.Actions.IsValidObject(item))
            {
                return false;
            }

            wo = Globals.Core.WorldFilter[item];
            if (wo == null || wo.Values(LongValueKey.EquipableSlots, 0) == 0)
            {
                return false;
            }

            if (wo.Container != Globals.Core.CharacterFilter.Id)
            {
                if (!Globals.Core.Actions.IsValidObject(wo.Container))
                {
                    return false;
                }

                var container = Globals.Core.WorldFilter[wo.Container];
                if (container == null || container.Container != Globals.Core.CharacterFilter.Id)
                {
                    return false;
                }
            }

            return true;
        }

        private void WorldFilter_ChangeObject(object sender, ChangeObjectEventArgs e)
        {
            try
            {
                if (!running)
                    return;
                if (e.Change != WorldChangeType.StorageChange)
                    return;
                if (dequippingState)
                    return;

                //Logger.Debug($"Change object fired - {Util.GetObjectName(e.Changed.Id)}");
                if (e.Changed.Id == itemList.Peek() && e.Changed.Values(LongValueKey.Slot, -1) == -1)
                {
                    Logger.Debug("Equipment Manager: Removing item from queue");
                    itemList.Dequeue();
                    bailTimer = DateTime.UtcNow;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private static readonly Regex cliRe = new Regex(@"^/ub\s+equip\s+(?<verb>load|list|test|help)(?:\s+(?<profileName>.*))?");
        private void Core_CommandLineText(object sender, Decal.Adapter.ChatParserInterceptEventArgs e)
        {
            try
            {
                var match = cliRe.Match(e.Text);
                if (match != null && match.Success)
                {
                    var verb = match.Groups["verb"].Value.Trim();
                    var profileName = match.Groups["profileName"].Value;

                    if (verb != "help" && verb != "list" && string.IsNullOrEmpty(profileName))
                    {
                        Util.WriteToChat("Profile name required");
                        return;
                    }

                    switch (verb)
                    {
                        case "load":
                            Start(profileName);
                            break;

                        case "list":
                            Util.WriteToChat("Equip Profiles");
                            foreach (var p in GetProfiles(profileName))
                            {
                                Util.WriteToChat($" * {Path.GetFileName(p)} ({Path.GetDirectoryName(p)})");
                            }
                            break;

                        case "test": // TODO
                            Start(profileName, true);
                            break;

                        case "help": // TODO
                            Util.WriteToChat("Usage: /ub equip { list | load | test | help } [profile.utl]");
                            break;

                        default:
                            Util.WriteToChat($"Unknown verb: {verb}");
                            break;
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private IEnumerable<string> GetProfiles(string profileName = "")
        {
            var charPath = Path.Combine(Util.GetCharacterDirectory(), "equip");
            var mainPath = Path.Combine(Util.GetPluginDirectory(), "equip");
            var serverPath = Path.Combine(Util.GetServerDirectory(), "equip");

            var charFiles = Directory.GetFiles(charPath, string.IsNullOrEmpty(profileName) ? "*.utl" : profileName);
            var serverFiles = Directory.GetFiles(serverPath, string.IsNullOrEmpty(profileName) ? "*.utl" : profileName);
            var mainFiles = Directory.GetFiles(mainPath, string.IsNullOrEmpty(profileName) ? "*.utl" : profileName);

            return charFiles.Concat(serverFiles).Concat(mainFiles);
        }

        private void Start(string profileName = "", bool testMode = false)
        {
            try
            {
                Logger.Debug($"Executing equip load command with profile '{profileName}'");
                running = true;
                bailTimer = DateTime.UtcNow;
                dequippingState = true;
                itemList = null;
                isTestMode = testMode;

                timer.Reset();
                timer.Start();

                var hasLootCore = false;
                if (lootProfile == null)
                {
                    try
                    {
                        lootProfile = new VTClassic.LootCore();
                        hasLootCore = true;
                    }
                    catch (Exception ex) { Logger.LogException(ex); }

                    if (!hasLootCore)
                    {
                        Util.WriteToChat("Unable to load VTClassic, something went wrong.");
                        return;
                    }
                }

                var profilePath = GetProfilePath(string.IsNullOrEmpty(profileName) ? (Globals.Core.CharacterFilter.Name + ".utl") : profileName);

                if (!File.Exists(profilePath))
                {
                    Logger.Debug("No equip profile exists: " + profilePath);
                    Stop();
                    return;
                }

                // Load our loot profile
                ((VTClassic.LootCore)lootProfile).LoadProfile(profilePath, false);

                equippableItems.AddRange(GetEquippableItems());
                Logger.Debug($"Found {equippableItems.Count} equippable items");
                if (Globals.Assessor.NeedsInventoryData(equippableItems.Select(i => i.Id)))
                {
                    pendingIdCount = Globals.Assessor.GetNeededIdCount(equippableItems.Select(i => i.Id));
                    Globals.Assessor.RequestAll(equippableItems.Select(i => i.Id));
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private string GetProfilePath(string profileName)
        {
            var charPath = Path.Combine(Util.GetCharacterDirectory(), "equip");
            var mainPath = Path.Combine(Util.GetPluginDirectory(), "equip");
            var serverPath = Path.Combine(Util.GetServerDirectory(), "equip");

            if (File.Exists(Path.Combine(charPath, profileName)))
            {
                return Path.Combine(charPath, profileName);
            }
            else if (File.Exists(Path.Combine(serverPath, profileName)))
            {
                return Path.Combine(serverPath, profileName);
            }
            else if (File.Exists(Path.Combine(mainPath, profileName)))
            {
                return Path.Combine(mainPath, profileName);
            }
            else if (File.Exists(Path.Combine(charPath, "default.utl")))
            {
                return Path.Combine(charPath, "default.utl");
            }
            else if (File.Exists(Path.Combine(serverPath, "default.utl")))
            {
                return Path.Combine(serverPath, "default.utl");
            }
            else if (File.Exists(Path.Combine(mainPath, "default.utl")))
            {
                return Path.Combine(mainPath, "default.utl");
            }
            return Path.Combine(mainPath, profileName);
        }

        private void Stop()
        {
            try
            {
                Util.ThinkOrWrite($"Equipment Manager: Finished equipping items in {timer.Elapsed.TotalSeconds}s",
                    Globals.Settings.EquipmentManager.Think);

                if (lootProfile != null) ((VTClassic.LootCore)lootProfile).UnloadProfile();
                VTankControl.Nav_UnBlock();
                VTankControl.Item_UnBlock();
            }
            catch (Exception ex) { Logger.LogException(ex); }
            finally
            {
                running = false;
                itemList = null;
                currentEquipAttempts = 0;
                equippableItems.Clear();
                lastEquippingItem = 0;
            }
        }

        public void Think()
        {
            try
            {
                if (!running)
                    return;

                if (VTankControl.navBlockedUntil < DateTime.UtcNow + TimeSpan.FromSeconds(1))
                { //if equip is running, and nav block has less than a second remaining, refresh it
                    VTankControl.Nav_Block(30000, Globals.Settings.Plugin.Debug);
                    VTankControl.Item_Block(30000, false);
                }

                if (Globals.Core.Actions.BusyState == 0)
                {
                    if (pendingIdCount > 0)
                    {
                        var idCount = Globals.Assessor.GetNeededIdCount(equippableItems.Select(i => i.Id));
                        if (idCount != pendingIdCount)
                        {
                            pendingIdCount = idCount;
                            bailTimer = DateTime.UtcNow;
                        }

                        if (idCount > 0)
                        {
                            if (DateTime.UtcNow - lastIdSpam > TimeSpan.FromSeconds(15))
                            {
                                Util.WriteToChat(string.Format("Equip Mgr waiting to id {0} items, this will take approximately {0} seconds.", idCount));
                                lastIdSpam = DateTime.UtcNow;
                            }

                            return;
                        }
                    }

                    if (itemList == null)
                    {
                        itemList = new Queue<int>();
                        foreach (var item in equippableItems)
                        {
                            Logger.Debug($"Assessing {Util.GetObjectName(item.Id)}...");
                            uTank2.LootPlugins.GameItemInfo itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithID(item.Id);
                            if (itemInfo == null) continue;
                            uTank2.LootPlugins.LootAction result = ((VTClassic.LootCore)lootProfile).GetLootDecision(itemInfo);
                            if (result.IsKeep)
                            {
                                Logger.Debug($"Matches rule: {result.RuleName}");
                                itemList.Enqueue(item.Id);
                            }
                        }

                        Logger.Debug($"Found {itemList.Count} items to equip");
                    }

                    if (itemList.Count == 0)
                    {
                        Logger.Debug("Item queue empty - stopping");
                        Stop();
                        return;
                    }

                    if (isTestMode)
                    {
                        Util.WriteToChat("Will attempt to equip the following items in order:");
                        while (itemList.Count > 0)
                        {
                            var item = itemList.Dequeue();
                            Util.WriteToChat($" * {Util.GetObjectName(item)} <{item}>");
                        }

                        Stop();
                        return;
                    }

                    if (dequippingState)
                    {
                        foreach (var item in Globals.Core.WorldFilter.GetInventory())
                        {
                            // skip items in profile since they are going to be equipped
                            if (itemList.Contains(item.Id))
                                continue;

                            if (item.Values(LongValueKey.Slot, -1) == -1)
                            {
                                Logger.Debug($"Dequipping item - {Util.GetObjectName(item.Id)}");
                                Globals.Core.Actions.MoveItem(item.Id, Globals.Core.CharacterFilter.Id);
                                return;
                            }
                        }

                        Logger.Debug("Finished dequipping items");
                        dequippingState = false;
                    }

                    WorldObject wo = null;
                    while (wo == null || wo.Values(LongValueKey.Slot, -1) == -1)
                    {
                        if (!IsValidItem(itemList.Peek(), out wo) || wo == null)
                        {
                            Util.WriteToChat($"Could not find item with id: {wo.Id} - SKIPPING");
                            itemList.Dequeue();
                            return;
                        }
                        else if (wo.Values(LongValueKey.Slot, -1) == -1)
                        {
                            Logger.Debug($"Item already equipped: {wo.Id} - SKIPPING");
                            itemList.Dequeue();
                            if (itemList.Count == 0)
                            {
                                return;
                            }
                        }
                    }

                    if (lastEquippingItem != itemList.Peek())
                    {
                        lastEquippingItem = itemList.Peek();
                        currentEquipAttempts = 0;
                    }
                    else if (++currentEquipAttempts > 15) // Stop trying to make fetch happen
                    {
                        Logger.Debug($"Too many equip attempts ({Util.GetObjectName(itemList.Peek())}) - SKIPPING");
                        currentEquipAttempts = 0;
                        itemList.Dequeue();
                        return;
                    }

                    Logger.Debug($"Attempting to equip item - {Util.GetObjectName(wo.Id)}");
                    Globals.Core.Actions.UseItem(wo.Id, 0);
                }

                if (DateTime.UtcNow - bailTimer > TimeSpan.FromSeconds(10))
                {
                    Util.WriteToChat($"Equipment Manager bail, timeout expired");
                    Stop();
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private IEnumerable<WorldObject> GetEquippableItems()
        {
            foreach (var item in Globals.Core.WorldFilter.GetInventory())
            {
                if (!new[]
                    {
                        ObjectClass.Armor,
                        ObjectClass.Clothing,
                        ObjectClass.Gem, // Aetheria
                        ObjectClass.Jewelry,
                        ObjectClass.MeleeWeapon,
                        ObjectClass.MissileWeapon,
                        ObjectClass.WandStaffOrb
                    }.Contains(item.ObjectClass))
                {
                    continue;
                }
                if (item.Values(LongValueKey.EquipableSlots, 0) == 0)
                    continue;

                yield return item;
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Globals.Core.CommandLineText -= Core_CommandLineText;
                    Globals.Core.WorldFilter.ChangeObject -= WorldFilter_ChangeObject;
                }

                disposedValue = true;
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
