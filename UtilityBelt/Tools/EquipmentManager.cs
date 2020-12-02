using Decal.Adapter.Wrappers;
using Decal.Filters;
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
    [Summary("Provides commands for managing equipped items.")]
    [FullDescription(@"
Equipment Manager allows you to create and execute equipment profiles based on VTank loot profiles. As an example, you could have your main suit of armor in a loot profile an issue a command to equip it. Alternatively, if you like to switch to your robe and mask when sitting at your allegiance mansion, you could issue a command to dequip your current items and equip that.

When invoked, Equipment Manager will attempt to load a VTank loot profile in one of the following locations:

 * Documents\Decal Plugins\UtilityBelt\equip\<Server>\<Character>\<Trade Partner Name>.utl
 * Documents\Decal Plugins\UtilityBelt\equip\<Server>\<Trade Partner Name>.utl
 * Documents\Decal Plugins\UtilityBelt\equip\<Trade Partner Name>.utl
 * Documents\Decal Plugins\UtilityBelt\equip\<Server>\<Character>\default.utl
 * Documents\Decal Plugins\UtilityBelt\equip\<Server>\default.utl
 * Documents\Decal Plugins\UtilityBelt\equip\default.utl

### Example VTank Profiles

 * [Cosmic Jester.utl](/utl/Cosmic Jester.utl) - Matches a suit of armor
 * [Plaguefang.utl](/utl/Plaguefang.utl) - Matches a PF robe and Rynthid Energy Shield
 * [Set A.utl](/utl/Set A.utl) - Matches all items that are equippable and inscribed with 'Set A'
    ")]
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

        #region SpellInfo
        struct SpellInfo<T> {
            public readonly T Key;
            public readonly double Change;
            public readonly double Bonus;

            public SpellInfo(T key, double change)
                : this(key, change, 0) {
            }

            public SpellInfo(T key, double change, double bonus) {
                Key = key;
                Change = change;
                Bonus = bonus;
            }
        }
        #endregion

        Dictionary<int, SpellInfo<LongValueKey>> LongValueKeySpellEffects = new Dictionary<int, SpellInfo<LongValueKey>>();
        Dictionary<int, SpellInfo<DoubleValueKey>> DoubleValueKeySpellEffects = new Dictionary<int, SpellInfo<DoubleValueKey>>();

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
        [Usage("/ub equip {list | load [lootProfile] | test [lootProfile] | create [lootProfile]}")]
        [Example("/ub equip load profile.utl", "Equips all items matching profile.utl.")]
        [Example("/ub equip list", "Lists available equipment profiles.")]
        [Example("/ub equip test profile.utl", "Test equipping profile.utl")]
        [CommandPattern("equip", @"^\s*(?<Verb>load|list|test|create)(?:\s+(?<Profile>.*?))?\s*$")]
        public void DoEquip(string command, Match args) {
            var verb = args.Groups["Verb"].Value;
            var profileName = args.Groups["Profile"].Value;

            if (verb != "list" && !string.IsNullOrEmpty(profileName) && string.IsNullOrEmpty(Path.GetExtension(profileName)))
                profileName = profileName + ".utl";

            switch (verb) {
                case "create":
                    Start(profileName, RunningState.Creating);
                    break;
                case "load":
                    Start(profileName, RunningState.Dequipping);
                    break;

                case "list":
                    Logger.WriteToChat("Equip Profiles:");
                    foreach (var p in GetProfiles(profileName)) {
                        Logger.WriteToChat($" * {Path.GetFileName(p)} ({Path.GetDirectoryName(p)})");
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
        #region /ub calcdamage
        [Summary("Calculates the buffed damage of the currently selected item. Only cantrip buffs are included in the calculation.")]
        [Usage("/ub calcdamage")]
        [Example("/ub calcdamage", "calcdamage")]
        [CommandPattern("calcdamage", @".*")]
        public void UB_CalcDamage(string command, Match args) {
            try {
                var wo = UB.Core.WorldFilter[UB.Core.Actions.CurrentSelection];

                if (wo == null) {
                    Logger.Error($"Nothing selected");
                    return;
                }

                if (!wo.HasIdData) {
                    Logger.Error($"{Util.GetObjectName(wo.Id)} does not have id data, please examine it first.");
                    return;
                }

                switch (wo.ObjectClass) {
                    case ObjectClass.MissileWeapon:
                        CalculatePotentialMissileWeaponDamage(wo, false);
                        return;

                    default:
                        Logger.Error($"Calc Damage: {wo.ObjectClass} is not currently supported");
                        break;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); Logger.Error(ex.ToString()); }
        }

        private double CalculatePotentialMissileWeaponDamage(WorldObject wo, bool silent=true) {
            var maxDamage = wo.Values(LongValueKey.MaxDamage, 0);
            var damageBonus = Math.Round(wo.Values(DoubleValueKey.DamageBonus), 2);
            var elementalBonus = wo.Values(LongValueKey.ElementalDmgBonus);
            var maxDamageSpellBonus = GetSpellBonuses(wo, LongValueKey.MaxDamage, 0, silent);
            var numberTimesTinkered = wo.Values(LongValueKey.NumberTimesTinkered, 0);
            var isImbued = wo.Values(LongValueKey.Imbued, 0) > 0;
            var tinkersAvailable = 10 - Math.Min(10, numberTimesTinkered + (isImbued ? 0 : 1));
            var dmg = (maxDamage + maxDamageSpellBonus + elementalBonus) * (damageBonus + (tinkersAvailable * 0.04)- 1);

            if (!silent) {
                if (tinkersAvailable > 0 && !silent)
                    Logger.WriteToChat($"{tinkersAvailable} mahogany salvage adds {(tinkersAvailable * 0.04)} to DamageModifier");

                Logger.WriteToChat($"Formula: (DamageBonus + ElementalBonus) * DamageModifier");
                Logger.WriteToChat($"Calculated Formula: ({maxDamage}(+{maxDamageSpellBonus} from cantrips) + {elementalBonus}) * {damageBonus - 1}(+{(tinkersAvailable * 0.04)} from {tinkersAvailable} tinkers)");
                Logger.WriteToChat($"Calculated (after tinks): {dmg}");
            }

            return dmg;
        }
        #endregion
        #endregion

        public EquipmentManager(UtilityBeltPlugin ub, string name) : base(ub, name) {
            try {
                Directory.CreateDirectory(Path.Combine(Util.GetPluginDirectory(), "equip"));
                Directory.CreateDirectory(Path.Combine(Util.GetCharacterDirectory(), "equip"));
                Directory.CreateDirectory(Path.Combine(Util.GetServerDirectory(), "equip"));


                // from http://www.virindi.net/repos/virindi_public/trunk/VirindiTankLootPlugins/VTClassic/VTClassic/ComputedItemInfo.cs
                LongValueKeySpellEffects[2598] = new SpellInfo<LongValueKey>(LongValueKey.MaxDamage, 2, 2); // Minor Blood Thirst
                LongValueKeySpellEffects[2586] = new SpellInfo<LongValueKey>(LongValueKey.MaxDamage, 4, 4); // Major Blood Thirst
                LongValueKeySpellEffects[4661] = new SpellInfo<LongValueKey>(LongValueKey.MaxDamage, 7, 7); // Epic Blood Thirst
                LongValueKeySpellEffects[6089] = new SpellInfo<LongValueKey>(LongValueKey.MaxDamage, 10, 10); // Legendary Blood Thirst
                LongValueKeySpellEffects[3688] = new SpellInfo<LongValueKey>(LongValueKey.MaxDamage, 300); // Prodigal Blood Drinker
                LongValueKeySpellEffects[2604] = new SpellInfo<LongValueKey>(LongValueKey.ArmorLevel, 20, 20); // Minor Impenetrability
                LongValueKeySpellEffects[2592] = new SpellInfo<LongValueKey>(LongValueKey.ArmorLevel, 40, 40); // Major Impenetrability
                LongValueKeySpellEffects[4667] = new SpellInfo<LongValueKey>(LongValueKey.ArmorLevel, 60, 60); // Epic Impenetrability
                LongValueKeySpellEffects[6095] = new SpellInfo<LongValueKey>(LongValueKey.ArmorLevel, 80, 80); // Legendary Impenetrability
                
                DoubleValueKeySpellEffects[3251] = new SpellInfo<DoubleValueKey>(DoubleValueKey.ElementalDamageVersusMonsters, .01, .01); // Minor Spirit Thirst
                DoubleValueKeySpellEffects[3250] = new SpellInfo<DoubleValueKey>(DoubleValueKey.ElementalDamageVersusMonsters, .03, .03); // Major Spirit Thirst
                DoubleValueKeySpellEffects[4670] = new SpellInfo<DoubleValueKey>(DoubleValueKey.ElementalDamageVersusMonsters, .05, .05); // Epic Spirit Thirst
                DoubleValueKeySpellEffects[6098] = new SpellInfo<DoubleValueKey>(DoubleValueKey.ElementalDamageVersusMonsters, .07, .07); // Legendary Spirit Thirst

                DoubleValueKeySpellEffects[3735] = new SpellInfo<DoubleValueKey>(DoubleValueKey.ElementalDamageVersusMonsters, .15); // Prodigal Spirit Drinker
                
                DoubleValueKeySpellEffects[2603] = new SpellInfo<DoubleValueKey>(DoubleValueKey.AttackBonus, .03, .03); // Minor Heart Thirst
                DoubleValueKeySpellEffects[2591] = new SpellInfo<DoubleValueKey>(DoubleValueKey.AttackBonus, .05, .05); // Major Heart Thirst
                DoubleValueKeySpellEffects[4666] = new SpellInfo<DoubleValueKey>(DoubleValueKey.AttackBonus, .07, .07); // Epic Heart Thirst
                DoubleValueKeySpellEffects[6094] = new SpellInfo<DoubleValueKey>(DoubleValueKey.AttackBonus, .09, .09); // Legendary Heart Thirst
                
                DoubleValueKeySpellEffects[2600] = new SpellInfo<DoubleValueKey>(DoubleValueKey.MeleeDefenseBonus, .03, .03); // Minor Defender
                DoubleValueKeySpellEffects[3985] = new SpellInfo<DoubleValueKey>(DoubleValueKey.MeleeDefenseBonus, .04, .04); // Mukkir Sense
                DoubleValueKeySpellEffects[2588] = new SpellInfo<DoubleValueKey>(DoubleValueKey.MeleeDefenseBonus, .05, .05); // Major Defender
                DoubleValueKeySpellEffects[4663] = new SpellInfo<DoubleValueKey>(DoubleValueKey.MeleeDefenseBonus, .07, .07); // Epic Defender
                DoubleValueKeySpellEffects[6091] = new SpellInfo<DoubleValueKey>(DoubleValueKey.MeleeDefenseBonus, .09, .09); // Legendary Defender

                DoubleValueKeySpellEffects[3699] = new SpellInfo<DoubleValueKey>(DoubleValueKey.MeleeDefenseBonus, .25); // Prodigal Defender
                
                DoubleValueKeySpellEffects[3201] = new SpellInfo<DoubleValueKey>(DoubleValueKey.ManaCBonus, 1.05, 1.05); // Feeble Hermetic Link
                DoubleValueKeySpellEffects[3199] = new SpellInfo<DoubleValueKey>(DoubleValueKey.ManaCBonus, 1.10, 1.10); // Minor Hermetic Link
                DoubleValueKeySpellEffects[3202] = new SpellInfo<DoubleValueKey>(DoubleValueKey.ManaCBonus, 1.15, 1.15); // Moderate Hermetic Link
                DoubleValueKeySpellEffects[3200] = new SpellInfo<DoubleValueKey>(DoubleValueKey.ManaCBonus, 1.20, 1.20); // Major Hermetic Link
                DoubleValueKeySpellEffects[6086] = new SpellInfo<DoubleValueKey>(DoubleValueKey.ManaCBonus, 1.25, 1.25); // Epic Hermetic Link
                DoubleValueKeySpellEffects[6087] = new SpellInfo<DoubleValueKey>(DoubleValueKey.ManaCBonus, 1.30, 1.30); // Legendary Hermetic Link
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

        public int GetSpellBonuses(WorldObject wo, LongValueKey key, int defaultValue=0, bool silent=true) {
            int bonus = 0;
            FileService service = UB.Core.Filter<FileService>();
            for (int i = 0; i < wo.SpellCount; i++) {
                var spell = service.SpellTable.GetById(wo.Spell(i));
                if (LongValueKeySpellEffects.ContainsKey(wo.Spell(i)) && LongValueKeySpellEffects[wo.Spell(i)].Key == key && LongValueKeySpellEffects[wo.Spell(i)].Bonus != 0) {
                    bonus += (int)LongValueKeySpellEffects[wo.Spell(i)].Bonus;
                    if (!silent)
                        Logger.WriteToChat($"Spell {spell.Name} buffs {key} by {(int)LongValueKeySpellEffects[wo.Spell(i)].Bonus}");
                }
            }

            return bonus;
        }

        public int GetSpellBonuses(WorldObject wo, DoubleValueKey key, int defaultValue=0, bool silent=true) {
            int bonus = 0;
            FileService service = UB.Core.Filter<FileService>();
            for (int i = 0; i < wo.SpellCount; i++) {
                var spell = service.SpellTable.GetById(wo.Spell(i));
                if (DoubleValueKeySpellEffects.ContainsKey(wo.Spell(i)) && DoubleValueKeySpellEffects[wo.Spell(i)].Key == key && DoubleValueKeySpellEffects[wo.Spell(i)].Bonus != 0) {
                    bonus += (int)DoubleValueKeySpellEffects[wo.Spell(i)].Bonus;
                    if (!silent) {
                        var mod = wo.ObjectClass == ObjectClass.MissileWeapon ? "DamageBonus" : key.ToString();
                        Logger.WriteToChat($"Spell {spell.Name} buffs {mod} by {(int)DoubleValueKeySpellEffects[wo.Spell(i)].Bonus}");
                    }
                }
            }

            return bonus;
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

                Logger.WriteToChat($"Profile created at {path}");
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
            using (var inv = UB.Core.WorldFilter.GetInventory()) {
                foreach (var item in inv) {
                    if (item.Values(LongValueKey.Slot, -1) == -1) {
                        list.Add(item);
                    }
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
                            LogError("Unable to load VTClassic, something went wrong.");
                            return;
                        }
                    }

                    if (string.IsNullOrEmpty(profileName))
                        profileName = UB.Core.CharacterFilter.Name + ".utl";
                    else if (string.IsNullOrEmpty(Path.GetExtension(profileName)))
                        profileName += ".utl";

                    var profilePath = GetProfilePath(profileName);

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

                UB.Core.RenderFrame += Core_RenderFrame;
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
                    UBHelper.vTank.Decision_UnLock(UBHelper.vTank.ActionLockType.Navigation);
                    UBHelper.vTank.Decision_UnLock(UBHelper.vTank.ActionLockType.ItemUse);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
            finally {
                UB.Core.WorldFilter.ChangeObject -= WorldFilter_ChangeObject;
                UB.Core.RenderFrame -= Core_RenderFrame;
                state = RunningState.Idle;
                itemList = null;
                currentEquipAttempts = 0;
                equippableItems.Clear();
                lastEquippingItem = 0;
                currentProfileName = null;
            }
        }

        public void Core_RenderFrame(object sender, EventArgs e) {
            try {
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
                    Logger.WriteToChat("Will attempt to equip the following items in order:");
                    while (itemList.Count > 0) {
                        var item = itemList.Dequeue();
                        Logger.WriteToChat($" * {Util.GetObjectName(item)} <{item}>");
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
                        UB.Core.WorldFilter.ChangeObject += WorldFilter_ChangeObject;
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
            catch (Exception ex) { Logger.LogException(ex); }
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
                if (UBHelper.vTank.locks[UBHelper.vTank.ActionLockType.Navigation] < DateTime.UtcNow + TimeSpan.FromSeconds(1)) { //if equip is running, and nav block has less than a second remaining, refresh it
                    UBHelper.vTank.Decision_Lock(UBHelper.vTank.ActionLockType.Navigation, TimeSpan.FromMilliseconds(30000));
                    UBHelper.vTank.Decision_Lock(UBHelper.vTank.ActionLockType.ItemUse, TimeSpan.FromMilliseconds(30000));
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private bool TryDequipItem() {
            try {
                using (var inv = UB.Core.WorldFilter.GetInventory()) {
                    foreach (var item in inv) {
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
            using (var inv = UB.Core.WorldFilter.GetInventory()) {
                foreach (var item in inv) {
                    if (!ValidEquippableObjectClasses.Contains(item.ObjectClass)) {
                        continue;
                    }
                    if (item.Values(LongValueKey.EquipableSlots, 0) == 0)
                        continue;

                    yield return item;
                }
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
                    if (state != RunningState.Idle)
                        Stop();
                    base.Dispose(disposing);
                }

                disposedValue = true;
            }
        }
        #endregion
    }
}
