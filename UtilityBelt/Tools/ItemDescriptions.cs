using System;
using uTank2.LootPlugins;
using UtilityBelt.Lib;
using UBService.Lib.Settings;
using Decal.Adapter;
using Decal.Adapter.Wrappers;
using Decal.Filters;
using System.Collections.Generic;
using System.Globalization;
using BoolValueKey = uTank2.LootPlugins.BoolValueKey;
using DoubleValueKey = Decal.Adapter.Wrappers.DoubleValueKey;
using ObjectClass = Decal.Adapter.Wrappers.ObjectClass;
using StringValueKey = uTank2.LootPlugins.StringValueKey;

namespace UtilityBelt.Tools {
    [Name("ItemDescriptions")]
    public class ItemDescriptions : ToolBase {
        #region Config
        [Summary("Describe items when selected")]
        public readonly Setting<bool> DescribeOnSelect = new Setting<bool>(true);

        [Summary("Describe lootable items found by Looter")]
        public readonly Setting<bool> DescribeOnLoot = new Setting<bool>(true);

        [Summary("Display buffed values")]
        public readonly Setting<bool> ShowBuffedValues = new Setting<bool>(true);

        [Summary("Display value and burden")]
        public readonly Setting<bool> ShowValueAndBurden = new Setting<bool>(false);

        [Summary("Copy to clipboard")]
        public readonly Setting<bool> AutoClipboard = new Setting<bool>(false);
        #endregion

        #region Commands
        #region /ub desc
        //[Summary("Gives items matching the provided name to a player.")]
        //[Usage("/ub give[p{P|r}] [itemCount] <itemName> to <target>")]
        //[Example("/ub giver Hero.* to Zero Cool", "Gives all items matching the regex \"Hero.*\" to Zero Cool")]
        //[CommandPattern("give", @"^ *((?<Count>\d+)? ?(?<Item>.+?) to (?<Target>.+)|(?<StopCommand>stop|cancel|quit|abort))$", true)]
        //public void DoGive(string command, Match args) { }
        #endregion
        #endregion

        #region Expressions
        //#region getitemcountininventorybyname[string name]
        //[ExpressionMethod("getitemcountininventorybyname")]
        //[ExpressionParameter(0, typeof(string), "name", "Exact itemId name to match")]
        //[ExpressionReturn(typeof(double), "Returns a count of the number of items found. stack size is counted")]
        //[Summary("Counts how many items you have in your inventory exactly matching `name`. Stack sizes are counted")]
        //[Example("getitemcountininventorybyname[Prismatic Taper]", "Returns total count of prismatic tapers in your inventory")]
        //public object Getitemcountininventorybyname(string name) { }
        //#endregion //getitemcountininventorybyname[string namerx]
        #endregion //Expressions

        public ItemDescriptions(UtilityBeltPlugin ub, string name) : base(ub, name) { }

        public override void Init() {
            base.Init();

            try {
                DescribeOnSelect.Changed += DescribeOnSelect_Changed;

                if (UBHelper.Core.GameState != UBHelper.GameState.In_Game)
                    UB.Core.CharacterFilter.LoginComplete += CharacterFilter_LoginComplete;
                else
                    TryEnable();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void TryEnable() {
            if (DescribeOnSelect.Value)
                UB.Core.ItemSelected += Core_ItemSelected;
        }

        #region Event Handlers
        private void CharacterFilter_LoginComplete(object sender, EventArgs e) {
            UB.Core.CharacterFilter.LoginComplete -= CharacterFilter_LoginComplete;
            TryEnable();
        }

        private void DescribeOnSelect_Changed(object sender, SettingChangedEventArgs e) {
            if (DescribeOnSelect.Value)
                UB.Core.ItemSelected += Core_ItemSelected;
            else
                UB.Core.ItemSelected -= Core_ItemSelected;
        }

        private void Core_ItemSelected(object sender, ItemSelectedEventArgs e) {
            DisplayItem(e.ItemGuid);
        }
        #endregion // Event Handlers

        #region Item Display
        internal string DisplayItem(int itemId) {
            //, GameItemInfo itemInfo = null, string ruleName = null) {
            if (!UB.Core.Actions.IsValidObject(itemId)) {
                return null;
            }

            var itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithID(itemId);

            LootAction result = null;
            if (!uTank2.PluginCore.PC.FLootPluginQueryNeedsID(itemId))
                result = uTank2.PluginCore.PC.FLootPluginClassifyImmediate(itemId);
            else
                uTank2.PluginCore.PC.FLootPluginClassifyCallback(itemId, (obj, res, success) => {
                    result = res;
                });

            if (result is null) {
                Logger.Error($"Error getting LootAction for {itemId}");
                return null;
            }

            return DisplayItem(itemId, itemInfo, result.RuleName, !result.IsNoLoot);
        }

        //Based on doProcessItemInfoCallback in MagTools
        internal string DisplayItem(int item, GameItemInfo itemInfo, string ruleName, bool isLoot, bool silent = false) {
            var sb = new System.Text.StringBuilder();
            sb.Append(isLoot ? $"+({ruleName})" : "-");

            //Colorize loot rule before adding itemId description
            sb.Insert(0, $"<Tell:IIDString:221112:{item}@\">");
            sb.Append(@"<\\Tell>");

            //Add MagTools item description
            sb.Append(new ItemInfo(CoreManager.Current.WorldFilter[item])).Append(" ");

            if (!silent) 
                    MyClasses.VCS_Connector.SendChatTextCategorized("IDs", sb.ToString(), 14, 0);

            if (AutoClipboard.Value) {
                try {
                    System.Windows.Forms.Clipboard.SetDataObject(sb.Replace($"<Tell:IIDString:221112:{item}@\">", "").Replace(@"<\\Tell>", "").ToString());
                }
                catch (Exception ex) { Logger.Error(ex.Message); }
            }

            return sb.ToString();
        }
        #endregion

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);

            DescribeOnLoot.Changed -= DescribeOnSelect_Changed;

            if (DescribeOnSelect.Value)
                UB.Core.ItemSelected -= Core_ItemSelected;
        }
    }

    #region Slightly modified MagTools item description classes
    public class ItemInfo {
        private readonly WorldObject wo;
        private readonly MyWorldObject mwo;

        public ItemInfo(WorldObject worldObject) {
            wo = worldObject;
            mwo = MyWorldObjectCreator.Create(worldObject);
        }

        public override string ToString() {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            if (wo.Values(LongValueKey.Material) > 0) {
                if (Dictionaries.MaterialInfo.ContainsKey(wo.Values(LongValueKey.Material)))
                    sb.Append(Dictionaries.MaterialInfo[wo.Values(LongValueKey.Material)] + " ");
                else
                    sb.Append("unknown material " + wo.Values(LongValueKey.Material) + " ");
            }

            sb.Append(wo.Name);

            if (wo.Values((LongValueKey)353) > 0) {
                if (Dictionaries.MasteryInfo.ContainsKey(wo.Values((LongValueKey)353)))
                    sb.Append(" (" + Dictionaries.MasteryInfo[wo.Values((LongValueKey)353)] + ")");
                else
                    sb.Append(" (Unknown mastery " + wo.Values((LongValueKey)353) + ")");
            }

            int set = wo.Values((LongValueKey)265, 0);
            if (set != 0) {
                sb.Append(", ");
                if (Dictionaries.AttributeSetInfo.ContainsKey(set))
                    sb.Append(Dictionaries.AttributeSetInfo[set]);
                else
                    sb.Append("Unknown set " + set);
            }

            if (wo.Values(LongValueKey.ArmorLevel) > 0)
                sb.Append(", AL " + wo.Values(LongValueKey.ArmorLevel));

            if (wo.Values(LongValueKey.Imbued) > 0) {
                sb.Append(",");
                if ((wo.Values(LongValueKey.Imbued) & 1) == 1) sb.Append(" CS");
                if ((wo.Values(LongValueKey.Imbued) & 2) == 2) sb.Append(" CB");
                if ((wo.Values(LongValueKey.Imbued) & 4) == 4) sb.Append(" AR");
                if ((wo.Values(LongValueKey.Imbued) & 8) == 8) sb.Append(" SlashRend");
                if ((wo.Values(LongValueKey.Imbued) & 16) == 16) sb.Append(" PierceRend");
                if ((wo.Values(LongValueKey.Imbued) & 32) == 32) sb.Append(" BludgeRend");
                if ((wo.Values(LongValueKey.Imbued) & 64) == 64) sb.Append(" AcidRend");
                if ((wo.Values(LongValueKey.Imbued) & 128) == 128) sb.Append(" FrostRend");
                if ((wo.Values(LongValueKey.Imbued) & 256) == 256) sb.Append(" LightRend");
                if ((wo.Values(LongValueKey.Imbued) & 512) == 512) sb.Append(" FireRend");
                if ((wo.Values(LongValueKey.Imbued) & 1024) == 1024) sb.Append(" MeleeImbue");
                if ((wo.Values(LongValueKey.Imbued) & 4096) == 4096) sb.Append(" MagicImbue");
                if ((wo.Values(LongValueKey.Imbued) & 8192) == 8192) sb.Append(" Hematited");
                if ((wo.Values(LongValueKey.Imbued) & 536870912) == 536870912) sb.Append(" MagicAbsorb");
            }

            if (wo.Values(LongValueKey.NumberTimesTinkered) > 0)
                sb.Append(", Tinks " + wo.Values(LongValueKey.NumberTimesTinkered));

            if (wo.Values(LongValueKey.MaxDamage) != 0 && wo.Values(DoubleValueKey.Variance) != 0)
                sb.Append(", " + (wo.Values(LongValueKey.MaxDamage) - (wo.Values(LongValueKey.MaxDamage) * wo.Values(DoubleValueKey.Variance))).ToString("N2") + "-" + wo.Values(LongValueKey.MaxDamage));
            else if (wo.Values(LongValueKey.MaxDamage) != 0 && wo.Values(DoubleValueKey.Variance) == 0)
                sb.Append(", " + wo.Values(LongValueKey.MaxDamage));

            if (wo.Values(LongValueKey.ElementalDmgBonus, 0) != 0)
                sb.Append(", +" + wo.Values(LongValueKey.ElementalDmgBonus));

            if (wo.Values(DoubleValueKey.DamageBonus, 1) != 1)
                sb.Append(", +" + Math.Round(((wo.Values(DoubleValueKey.DamageBonus) - 1) * 100)) + "%");

            if (wo.Values(DoubleValueKey.ElementalDamageVersusMonsters, 1) != 1)
                sb.Append(", +" + Math.Round(((wo.Values(DoubleValueKey.ElementalDamageVersusMonsters) - 1) * 100)) + "%vs. Monsters");

            if (wo.Values(DoubleValueKey.AttackBonus, 1) != 1)
                sb.Append(", +" + Math.Round(((wo.Values(DoubleValueKey.AttackBonus) - 1) * 100)) + "%a");

            if (wo.Values(DoubleValueKey.MeleeDefenseBonus, 1) != 1)
                sb.Append(", " + Math.Round(((wo.Values(DoubleValueKey.MeleeDefenseBonus) - 1) * 100)) + "%md");

            if (wo.Values(DoubleValueKey.MagicDBonus, 1) != 1)
                sb.Append(", " + Math.Round(((wo.Values(DoubleValueKey.MagicDBonus) - 1) * 100), 1) + "%mgc.d");

            if (wo.Values(DoubleValueKey.MissileDBonus, 1) != 1)
                sb.Append(", " + Math.Round(((wo.Values(DoubleValueKey.MissileDBonus) - 1) * 100), 1) + "%msl.d");

            if (wo.Values(DoubleValueKey.ManaCBonus) != 0)
                sb.Append(", " + Math.Round((wo.Values(DoubleValueKey.ManaCBonus) * 100)) + "%mc");

            if (UtilityBeltPlugin.Instance.ItemDescriptions.ShowBuffedValues.Value &&
                //Settings.SettingsManager.ItemInfoOnIdent.ShowBuffedValues.Value && 
                (wo.ObjectClass == ObjectClass.MeleeWeapon || wo.ObjectClass == ObjectClass.MissileWeapon || wo.ObjectClass == ObjectClass.WandStaffOrb)) {
                sb.Append(", (");

                // (Damage)
                if (wo.ObjectClass == ObjectClass.MeleeWeapon)
                    sb.Append(mwo.CalcedBuffedTinkedDoT.ToString("N1") + "/" + mwo.GetBuffedIntValueKey((int)LongValueKey.MaxDamage));

                if (wo.ObjectClass == ObjectClass.MissileWeapon)
                    sb.Append(mwo.CalcedBuffedMissileDamage.ToString("N1"));

                if (wo.ObjectClass == ObjectClass.WandStaffOrb)
                    sb.Append(((mwo.GetBuffedDoubleValueKey((int)DoubleValueKey.ElementalDamageVersusMonsters) - 1) * 100).ToString("N1"));

                // (AttackBonus/MeleeDefenseBonus/ManaCBonus)
                sb.Append(" ");

                if (wo.Values(DoubleValueKey.AttackBonus, 1) != 1)
                    sb.Append(Math.Round(((mwo.GetBuffedDoubleValueKey((int)DoubleValueKey.AttackBonus) - 1) * 100)).ToString("N1") + "/");

                if (wo.Values(DoubleValueKey.MeleeDefenseBonus, 1) != 1)
                    sb.Append(Math.Round(((mwo.GetBuffedDoubleValueKey((int)DoubleValueKey.MeleeDefenseBonus) - 1) * 100)).ToString("N1"));

                if (wo.Values(DoubleValueKey.ManaCBonus) != 0)
                    sb.Append("/" + Math.Round(mwo.GetBuffedDoubleValueKey((int)DoubleValueKey.ManaCBonus) * 100));

                sb.Append(")");
            }

            if (wo.SpellCount > 0) {
                FileService service = CoreManager.Current.Filter<FileService>();

                List<int> itemActiveSpells = new List<int>();

                for (int i = 0; i < wo.SpellCount; i++)
                    itemActiveSpells.Add(wo.Spell(i));

                itemActiveSpells.Sort();
                itemActiveSpells.Reverse();

                foreach (int spell in itemActiveSpells) {
                    Spell spellById = service.SpellTable.GetById(spell);

                    // If the item is not loot generated, show all spells
                    if (!wo.LongKeys.Contains((int)LongValueKey.Material))
                        goto ShowSpell;

                    // Always show Minor/Major/Epic Impen
                    if (spellById.Name.Contains("Minor Impenetrability") || spellById.Name.Contains("Major Impenetrability") || spellById.Name.Contains("Epic Impenetrability") || spellById.Name.Contains("Legendary Impenetrability"))
                        goto ShowSpell;

                    // Always show trinket spells
                    if (spellById.Name.Contains("Augmented"))
                        goto ShowSpell;

                    if (wo.Values(LongValueKey.Unenchantable, 0) != 0) {
                        // Show banes and impen on unenchantable equipment
                        if (spellById.Name.Contains(" Bane") || spellById.Name.Contains("Impen") || spellById.Name.StartsWith("Brogard"))
                            goto ShowSpell;
                    }
                    else {
                        // Hide banes and impen on enchantable equipment
                        if (spellById.Name.Contains(" Bane") || spellById.Name.Contains("Impen") || spellById.Name.StartsWith("Brogard"))
                            continue;
                    }

                    if ((spellById.Family >= 152 && spellById.Family <= 158) || spellById.Family == 195 || spellById.Family == 325) {
                        // This is a weapon buff

                        // Lvl 6
                        if (spellById.Difficulty == 250)
                            continue;

                        // Lvl 7
                        if (spellById.Difficulty == 300)
                            goto ShowSpell;

                        // Lvl 8+
                        if (spellById.Difficulty >= 400)
                            goto ShowSpell;

                        continue;
                    }

                    // This is not a weapon buff.

                    // Filter all 1-5 spells
                    if (spellById.Name.EndsWith(" I") || spellById.Name.EndsWith(" II") || spellById.Name.EndsWith(" III") || spellById.Name.EndsWith(" IV") || spellById.Name.EndsWith(" V"))
                        continue;

                    // Filter 6's
                    if (spellById.Name.EndsWith(" VI"))
                        continue;

                    // Filter 7's
                    if (spellById.Difficulty == 300)
                        continue;

                    // Filter 8's
                    if (spellById.Name.Contains("Incantation"))
                        continue;

                    ShowSpell:

                    sb.Append(", " + spellById.Name);
                }
            }

            // Wield Lvl 180
            if (wo.Values(LongValueKey.WieldReqValue) > 0) {
                // I don't quite understand this.
                if (wo.Values(LongValueKey.WieldReqType) == 7 && wo.Values(LongValueKey.WieldReqAttribute) == 1)
                    sb.Append(", Wield Lvl " + wo.Values(LongValueKey.WieldReqValue));
                else {
                    if (Dictionaries.SkillInfo.ContainsKey(wo.Values(LongValueKey.WieldReqAttribute)))
                        sb.Append(", " + Dictionaries.SkillInfo[wo.Values(LongValueKey.WieldReqAttribute)] + " " + wo.Values(LongValueKey.WieldReqValue));
                    else
                        sb.Append(", Unknown skill: " + wo.Values(LongValueKey.WieldReqAttribute) + " " + wo.Values(LongValueKey.WieldReqValue));
                }
            }

            // Summoning Gem
            if (wo.Values((LongValueKey)369) > 0)
                sb.Append(", Lvl " + wo.Values((LongValueKey)369));

            // Melee Defense 300 to Activate
            // If the activation is lower than the wield requirement, don't show it.
            if (wo.Values(LongValueKey.SkillLevelReq) > 0 && (wo.Values(LongValueKey.WieldReqAttribute) != wo.Values(LongValueKey.ActivationReqSkillId) || wo.Values(LongValueKey.WieldReqValue) < wo.Values(LongValueKey.SkillLevelReq))) {
                if (Dictionaries.SkillInfo.ContainsKey(wo.Values(LongValueKey.ActivationReqSkillId)))
                    sb.Append(", " + Dictionaries.SkillInfo[wo.Values(LongValueKey.ActivationReqSkillId)] + " " + wo.Values(LongValueKey.SkillLevelReq) + " to Activate");
                else
                    sb.Append(", Unknown skill: " + wo.Values(LongValueKey.ActivationReqSkillId) + " " + wo.Values(LongValueKey.SkillLevelReq) + " to Activate");
            }

            // Summoning Gem
            if (wo.Values((LongValueKey)366) > 0 && wo.Values((LongValueKey)367) > 0) {
                if (Dictionaries.SkillInfo.ContainsKey(wo.Values((LongValueKey)366)))
                    sb.Append(", " + Dictionaries.SkillInfo[wo.Values((LongValueKey)366)] + " " + wo.Values((LongValueKey)367));
                else
                    sb.Append(", Unknown skill: " + wo.Values((LongValueKey)366) + " " + wo.Values((LongValueKey)367));
            }

            // Summoning Gem
            if (wo.Values((LongValueKey)368) > 0 && wo.Values((LongValueKey)367) > 0) {
                if (Dictionaries.SkillInfo.ContainsKey(wo.Values((LongValueKey)368)))
                    sb.Append(", Spec " + Dictionaries.SkillInfo[wo.Values((LongValueKey)368)] + " " + wo.Values((LongValueKey)367));
                else
                    sb.Append(", Unknown skill spec: " + wo.Values((LongValueKey)368) + " " + wo.Values((LongValueKey)367));
            }

            if (wo.Values(LongValueKey.LoreRequirement) > 0)
                sb.Append(", Diff " + wo.Values(LongValueKey.LoreRequirement));

            if (wo.ObjectClass == ObjectClass.Salvage) {
                if (wo.Values(DoubleValueKey.SalvageWorkmanship) > 0)
                    sb.Append(", Work " + wo.Values(DoubleValueKey.SalvageWorkmanship).ToString("N2"));
            }
            else {
                if (wo.Values(LongValueKey.Workmanship) > 0 && wo.Values(LongValueKey.NumberTimesTinkered) != 10) // Don't show the work if its already 10 tinked.
                    sb.Append(", Craft " + wo.Values(LongValueKey.Workmanship));
            }

            if (wo.ObjectClass == ObjectClass.Armor && wo.Values(LongValueKey.Unenchantable, 0) != 0) {
                sb.Append(", [" +
                    wo.Values(DoubleValueKey.SlashProt).ToString("N1") + "/" +
                    wo.Values(DoubleValueKey.PierceProt).ToString("N1") + "/" +
                    wo.Values(DoubleValueKey.BludgeonProt).ToString("N1") + "/" +
                    wo.Values(DoubleValueKey.ColdProt).ToString("N1") + "/" +
                    wo.Values(DoubleValueKey.FireProt).ToString("N1") + "/" +
                    wo.Values(DoubleValueKey.AcidProt).ToString("N1") + "/" +
                    wo.Values(DoubleValueKey.LightningProt).ToString("N1") + "]");
            }

            if (UtilityBeltPlugin.Instance.ItemDescriptions.ShowValueAndBurden.Value
                //Settings.SettingsManager.ItemInfoOnIdent.ShowValueAndBurden.Value
                ) {
                if (wo.Values(LongValueKey.Value) > 0)
                    sb.Append(", Value " + string.Format("{0:n0}", wo.Values(LongValueKey.Value)));

                if (wo.Values(LongValueKey.Burden) > 0)
                    sb.Append(", BU " + wo.Values(LongValueKey.Burden));
            }

            if (mwo.TotalRating > 0) {
                sb.Append(", [");
                bool first = true;
                if (mwo.DamRating > 0) { sb.Append("D " + mwo.DamRating); first = false; }
                if (mwo.DamResistRating > 0) { if (!first) sb.Append(", "); sb.Append("DR " + mwo.DamResistRating); first = false; }
                if (mwo.CritRating > 0) { if (!first) sb.Append(", "); sb.Append("C " + mwo.CritRating); first = false; }
                if (mwo.CritDamRating > 0) { if (!first) sb.Append(", "); sb.Append("CD " + mwo.CritDamRating); first = false; }
                if (mwo.CritResistRating > 0) { if (!first) sb.Append(", "); sb.Append("CR " + mwo.CritResistRating); first = false; }
                if (mwo.CritDamResistRating > 0) { if (!first) sb.Append(", "); sb.Append("CDR " + mwo.CritDamResistRating); first = false; }
                if (mwo.HealBoostRating > 0) { if (!first) sb.Append(", "); sb.Append("HB " + mwo.HealBoostRating); first = false; }
                if (mwo.VitalityRating > 0) { if (!first) sb.Append(", "); sb.Append("V " + mwo.VitalityRating); first = false; }
                sb.Append("]");
            }

            if (wo.ObjectClass == ObjectClass.Misc && wo.Name.Contains("Keyring"))
                sb.Append(", Keys: " + wo.Values(LongValueKey.KeysHeld) + ", Uses: " + wo.Values(LongValueKey.UsesRemaining));

            return sb.ToString();
        }
    }

    public static class MyWorldObjectCreator {
        public static MyWorldObject Create(WorldObject wo) {
            MyWorldObject mwo = new MyWorldObject();

            Dictionary<int, bool> boolValues = new Dictionary<int, bool>();
            Dictionary<int, double> doubleValues = new Dictionary<int, double>();
            Dictionary<int, int> intValues = new Dictionary<int, int>();
            Dictionary<int, string> stringValues = new Dictionary<int, string>();
            List<int> activeSpells = new List<int>();
            List<int> spells = new List<int>();

            foreach (var key in wo.BoolKeys)
                boolValues.Add(key, wo.Values((Decal.Adapter.Wrappers.BoolValueKey)key));

            foreach (var key in wo.DoubleKeys)
                doubleValues.Add(key, wo.Values((Decal.Adapter.Wrappers.DoubleValueKey)key));

            foreach (var key in wo.LongKeys)
                intValues.Add(key, wo.Values((LongValueKey)key));

            foreach (var key in wo.StringKeys)
                stringValues.Add(key, wo.Values((Decal.Adapter.Wrappers.StringValueKey)key));

            for (int i = 0; i < wo.ActiveSpellCount; i++)
                activeSpells.Add(wo.ActiveSpell(i));

            for (int i = 0; i < wo.SpellCount; i++)
                spells.Add(wo.Spell(i));

            mwo.Init(wo.HasIdData, wo.Id, wo.LastIdTime, (int)wo.ObjectClass, boolValues, doubleValues, intValues, stringValues, activeSpells, spells);

            return mwo;
        }
    }

    public class MyWorldObject {
        public bool HasIdData;
        public int Id;
        public int LastIdTime;
        public int ObjectClass;

        public Dictionary<int, bool> BoolValues = new Dictionary<int, bool>();
        public Dictionary<int, double> DoubleValues = new Dictionary<int, double>();
        public Dictionary<int, int> IntValues = new Dictionary<int, int>();
        public Dictionary<int, string> StringValues = new Dictionary<int, string>();

        public List<int> ActiveSpells = new List<int>();
        public List<int> Spells = new List<int>();

        public void Init(bool hasIdData, int id, int lastIdTime, int objectClass, IDictionary<int, bool> boolValues, IDictionary<int, double> doubleValues, IDictionary<int, int> intValues, IDictionary<int, string> stringValues, IList<int> activeSpells, IList<int> spells) {
            HasIdData = hasIdData;
            Id = id;
            LastIdTime = lastIdTime;
            ObjectClass = objectClass;

            AddTo(boolValues, doubleValues, intValues, stringValues);

            ActiveSpells.Clear();
            foreach (var i in activeSpells)
                ActiveSpells.Add(i);

            Spells.Clear();
            foreach (var i in spells)
                Spells.Add(i);
        }

        public void AddTo(IDictionary<int, bool> boolValues, IDictionary<int, double> doubleValues, IDictionary<int, int> intValues, IDictionary<int, string> stringValues) {
            foreach (var kvp in boolValues) {
                if (boolValues.ContainsKey(kvp.Key))
                    BoolValues[kvp.Key] = kvp.Value;
                else
                    BoolValues.Add(kvp.Key, kvp.Value);
            }

            foreach (var kvp in doubleValues) {
                if (doubleValues.ContainsKey(kvp.Key))
                    DoubleValues[kvp.Key] = kvp.Value;
                else
                    DoubleValues.Add(kvp.Key, kvp.Value);
            }

            foreach (var kvp in intValues) {
                if (intValues.ContainsKey(kvp.Key))
                    IntValues[kvp.Key] = kvp.Value;
                else
                    IntValues.Add(kvp.Key, kvp.Value);
            }

            foreach (var kvp in stringValues) {
                if (stringValues.ContainsKey(kvp.Key))
                    StringValues[kvp.Key] = kvp.Value;
                else
                    StringValues.Add(kvp.Key, kvp.Value);
            }
        }

        public bool Values(BoolValueKey key, bool defaultValue = false) {
            if (BoolValues.ContainsKey((int)key))
                return BoolValues[(int)key];

            return defaultValue;
        }

        public double Values(DoubleValueKey key, double defaultValue = 0) {
            if (DoubleValues.ContainsKey((int)key))
                return DoubleValues[(int)key];

            return defaultValue;
        }

        public int Values(IntValueKey key, int defaultValue = 0) {
            if (IntValues.ContainsKey((int)key))
                return IntValues[(int)key];

            return defaultValue;
        }

        public string Values(StringValueKey key, string defaultValue = null) {
            if (StringValues.ContainsKey((int)key))
                return StringValues[(int)key];

            return defaultValue;
        }

        public string Material { get { if (IntValues.ContainsKey(131)) return Dictionaries.MaterialInfo.ContainsKey(IntValues[131]) ? Dictionaries.MaterialInfo[IntValues[131]] : IntValues[131].ToString(CultureInfo.InvariantCulture); return null; } }

        public string Name => StringValues.ContainsKey(1) ? StringValues[1] : null;

        public string EquipSkill { get { if (IntValues.ContainsKey(218103840)) return Dictionaries.SkillInfo.ContainsKey(IntValues[218103840]) ? Dictionaries.SkillInfo[IntValues[218103840]] : IntValues[218103840].ToString(CultureInfo.InvariantCulture); return null; } }

        public string Mastery { get { if (IntValues.ContainsKey(353)) return Dictionaries.MasteryInfo.ContainsKey(IntValues[353]) ? Dictionaries.MasteryInfo[IntValues[353]] : IntValues[353].ToString(CultureInfo.InvariantCulture); return null; } }

        public string ItemSet { get { if (IntValues.ContainsKey(265)) return Dictionaries.AttributeSetInfo.ContainsKey(IntValues[265]) ? Dictionaries.AttributeSetInfo[IntValues[265]] : IntValues[265].ToString(CultureInfo.InvariantCulture); return null; } }

        public int ArmorLevel => IntValues.ContainsKey(28) ? IntValues[28] : -1;

        public string Imbue {
            get {
                if (!IntValues.ContainsKey(179) || IntValues[179] == 0) return null;

                string retVal = string.Empty;
                if ((IntValues[179] & 1) == 1) retVal += " CS";
                if ((IntValues[179] & 2) == 2) retVal += " CB";
                if ((IntValues[179] & 4) == 4) retVal += " AR";
                if ((IntValues[179] & 8) == 8) retVal += " SlashRend";
                if ((IntValues[179] & 16) == 16) retVal += " PierceRend";
                if ((IntValues[179] & 32) == 32) retVal += " BludgeRend";
                if ((IntValues[179] & 64) == 64) retVal += " AcidRend";
                if ((IntValues[179] & 128) == 128) retVal += " FrostRend";
                if ((IntValues[179] & 256) == 256) retVal += " LightRend";
                if ((IntValues[179] & 512) == 512) retVal += " FireRend";
                if ((IntValues[179] & 1024) == 1024) retVal += " MeleeImbue";
                if ((IntValues[179] & 4096) == 4096) retVal += " MagicImbue";
                if ((IntValues[179] & 8192) == 8192) retVal += " Hematited";
                if ((IntValues[179] & 536870912) == 536870912) retVal += " MagicAbsorb";
                retVal = retVal.Trim();
                return retVal;
            }
        }

        public int Tinks => IntValues.ContainsKey(171) ? IntValues[171] : -1;

        public int MaxDamage => IntValues.ContainsKey(218103842) ? IntValues[218103842] : -1;

        public int ElementalDmgBonus => IntValues.ContainsKey(204) ? IntValues[204] : -1;

        public double Variance => DoubleValues.ContainsKey(167772171) ? DoubleValues[167772171] : -1;

        public double DamageBonus => DoubleValues.ContainsKey(167772174) ? DoubleValues[167772174] : -1;

        public double ElementalDamageVersusMonsters => DoubleValues.ContainsKey(152) ? DoubleValues[152] : -1;

        public double AttackBonus => DoubleValues.ContainsKey(167772172) ? DoubleValues[167772172] : -1;

        public double MeleeDefenseBonus => DoubleValues.ContainsKey(29) ? DoubleValues[29] : -1;

        public double MagicDBonus => DoubleValues.ContainsKey(150) ? DoubleValues[150] : -1;

        public double MissileDBonus => DoubleValues.ContainsKey(149) ? DoubleValues[149] : -1;

        public double ManaCBonus => DoubleValues.ContainsKey(144) ? DoubleValues[144] : -1;

        public int WieldLevel { get { if (IntValues.ContainsKey(160) && IntValues[160] > 0 && IntValues.ContainsKey(158) && IntValues[158] == 7 && IntValues.ContainsKey(159) && IntValues[159] == 1) return IntValues[160]; return -1; } }

        public int SkillLevel { get { if (IntValues.ContainsKey(160) && IntValues[160] > 0 && (!IntValues.ContainsKey(158) || IntValues[158] != 7) && IntValues.ContainsKey(159)) return IntValues[160]; return -1; } }

        public int LoreRequirement => IntValues.ContainsKey(109) ? IntValues[109] : -1;

        public double SalvageWorkmanship => DoubleValues.ContainsKey(167772169) ? DoubleValues[167772169] : -1;

        public int Workmanship => IntValues.ContainsKey(105) ? IntValues[105] : -1;

        public int Value => IntValues.ContainsKey(19) ? IntValues[19] : -1;

        public int Burden => IntValues.ContainsKey(5) ? IntValues[5] : -1;

        public int DamRating => IntValues.ContainsKey(370) ? IntValues[370] : -1;

        public int DamResistRating => IntValues.ContainsKey(371) ? IntValues[371] : -1;

        public int CritRating => IntValues.ContainsKey(372) ? IntValues[372] : -1;

        public int CritResistRating => IntValues.ContainsKey(373) ? IntValues[373] : -1;

        public int CritDamRating => IntValues.ContainsKey(374) ? IntValues[374] : -1;

        public int CritDamResistRating => IntValues.ContainsKey(375) ? IntValues[375] : -1;

        public int HealBoostRating => IntValues.ContainsKey(376) ? IntValues[376] : -1;

        public int VitalityRating => IntValues.ContainsKey(379) ? IntValues[379] : -1;

        /// <summary>
        /// Returns the sum of all the ratings found on this item, or -1 if no ratings exist.
        /// </summary>
        public int TotalRating {
            get {
                if (DamRating == -1 && DamResistRating == -1 && CritRating == -1 && CritResistRating == -1 && CritDamRating == -1 && CritDamResistRating == -1 && HealBoostRating == -1 && VitalityRating == -1)
                    return -1;

                return Math.Max(DamRating, 0) + Math.Max(DamResistRating, 0) + Math.Max(CritRating, 0) + Math.Max(CritResistRating, 0) + Math.Max(CritDamRating, 0) + Math.Max(CritDamResistRating, 0) + Math.Max(HealBoostRating, 0) + Math.Max(VitalityRating, 0);
            }
        }

        /// <summary>
        /// This will take the current AmorLevel of the item, subtract any buffs, subtract tinks as 20 AL each (not including imbue), and add any impen cantrips.
        /// </summary>
        public int CalcedStartingArmorLevel {
            get {
                int armorFromTinks = 0;
                int armorFromBuffs = 0;

                if (Tinks > 0 && ArmorLevel > 0)
                    armorFromTinks = (Imbue != null) ? (Tinks - 1) * 20 : Tinks * 20; // This assumes each tink adds an amor level of 20

                if ((!IntValues.ContainsKey(131) || IntValues[131] == 0) && ArmorLevel > 0) // If this item has no material, its not a loot gen, assume its a quest item and subtract 200 al
                    armorFromTinks = 200;

                foreach (int spell in ActiveSpells) {
                    foreach (var effect in Dictionaries.LongValueKeySpellEffects) {
                        if (spell == effect.Key && effect.Value.Key == 28)
                            armorFromBuffs += effect.Value.Change;
                    }
                }

                foreach (int spell in Spells) {
                    foreach (var effect in Dictionaries.LongValueKeySpellEffects) {
                        if (spell == effect.Key && effect.Value.Key == 28)
                            armorFromBuffs -= effect.Value.Bonus;
                    }
                }

                return ArmorLevel - armorFromTinks - armorFromBuffs;
            }
        }

        /// <summary>
        /// This will take into account Variance, MaxDamage and Tinks of a melee weapon and determine what its optimal 10 tinked DamageOverTime is.
        /// </summary>
        public double CalcedBuffedTinkedDoT {
            get {
                if (!DoubleValues.ContainsKey(167772171) || !IntValues.ContainsKey(218103842))
                    return -1;

                double variance = DoubleValues.ContainsKey(167772171) ? DoubleValues[167772171] : 0;
                int maxDamage = GetBuffedIntValueKey(218103842);

                int numberOfTinksLeft = Math.Max(10 - Math.Max(Tinks, 0), 0);

                if (!IntValues.ContainsKey(179) || IntValues[179] == 0)
                    numberOfTinksLeft--; // Factor in an imbue tink

                // If this is not a loot generated item, it can't be tinked
                if (!IntValues.ContainsKey(131) || IntValues[131] == 0)
                    numberOfTinksLeft = 0;

                for (int i = 1; i <= numberOfTinksLeft; i++) {
                    double ironTinkDoT = CalculateDamageOverTime(maxDamage + 24 + 1, variance);
                    double graniteTinkDoT = CalculateDamageOverTime(maxDamage + 24, variance * .8);

                    if (ironTinkDoT >= graniteTinkDoT)
                        maxDamage++;
                    else
                        variance *= .8;
                }

                return CalculateDamageOverTime(maxDamage + 24, variance);
            }
        }

        /// <summary>
        ///  GetBuffedIntValueKey(LongValueKey.MaxDamage) + (((GetBuffedDoubleValueKey(DoubleValueKey.DamageBonus) - 1) * 100) / 3) + GetBuffedIntValueKey(LongValueKey.ElementalDmgBonus);
        /// </summary>
        public double CalcedBuffedMissileDamage { get { if (!IntValues.ContainsKey(218103842) || !DoubleValues.ContainsKey(167772174) || !IntValues.ContainsKey(204)) return -1; return GetBuffedIntValueKey(218103842) + (((GetBuffedDoubleValueKey(167772174) - 1) * 100) / 3) + GetBuffedIntValueKey(204); } }

        public double BuffedElementalDamageVersusMonsters => GetBuffedDoubleValueKey(152, -1);

        public double BuffedAttackBonus => GetBuffedDoubleValueKey(167772172, -1);

        public double BuffedMeleeDefenseBonus => GetBuffedDoubleValueKey(29, -1);

        public double BuffedManaCBonus => GetBuffedDoubleValueKey(144, -1);

        public int GetBuffedIntValueKey(int key, int defaultValue = 0) {
            if (!IntValues.ContainsKey(key))
                return defaultValue;

            int value = IntValues[key];

            foreach (int spell in ActiveSpells) {
                if (Dictionaries.LongValueKeySpellEffects.ContainsKey(spell) && Dictionaries.LongValueKeySpellEffects[spell].Key == key)
                    value -= Dictionaries.LongValueKeySpellEffects[spell].Change;
            }

            foreach (int spell in Spells) {
                if (Dictionaries.LongValueKeySpellEffects.ContainsKey(spell) && Dictionaries.LongValueKeySpellEffects[spell].Key == key)
                    value += Dictionaries.LongValueKeySpellEffects[spell].Bonus;
            }

            return value;
        }

        public double GetBuffedDoubleValueKey(int key, double defaultValue = 0) {
            if (!DoubleValues.ContainsKey(key))
                return defaultValue;

            double value = DoubleValues[key];

            foreach (int spell in ActiveSpells) {
                if (Dictionaries.DoubleValueKeySpellEffects.ContainsKey(spell) && Dictionaries.DoubleValueKeySpellEffects[spell].Key == key) {
                    if (Math.Abs(Dictionaries.DoubleValueKeySpellEffects[spell].Change - 1) < double.Epsilon)
                        value /= Dictionaries.DoubleValueKeySpellEffects[spell].Change;
                    else
                        value -= Dictionaries.DoubleValueKeySpellEffects[spell].Change;
                }
            }

            foreach (int spell in Spells) {
                if (Dictionaries.DoubleValueKeySpellEffects.ContainsKey(spell) && Dictionaries.DoubleValueKeySpellEffects[spell].Key == key && Math.Abs(Dictionaries.DoubleValueKeySpellEffects[spell].Bonus - 0) > double.Epsilon) {
                    if (Math.Abs(Dictionaries.DoubleValueKeySpellEffects[spell].Change - 1) < double.Epsilon)
                        value *= Dictionaries.DoubleValueKeySpellEffects[spell].Bonus;
                    else
                        value += Dictionaries.DoubleValueKeySpellEffects[spell].Bonus;
                }
            }

            return value;
        }

        /// <summary>
        /// maxDamage * ((1 - critChance) * (2 - variance) / 2 + (critChance * critMultiplier));
        /// </summary>
        /// <param name="maxDamage"></param>
        /// <param name="variance"></param>
        /// <param name="critChance"></param>
        /// <param name="critMultiplier"></param>
        /// <returns></returns>
        public static double CalculateDamageOverTime(int maxDamage, double variance, double critChance = .1, double critMultiplier = 2) {
            return maxDamage * ((1 - critChance) * (2 - variance) / 2 + (critChance * critMultiplier));
        }

        public override string ToString() {
            return Name;
        }
    }

    public static class Dictionaries {
        /// <summary>
        /// Returns a dictionary of skill ids vs names
        /// </summary>
        /// <returns></returns>
        public static readonly Dictionary<int, string> SkillInfo = new Dictionary<int, string>
        {
			// This list was taken from the Alinco source
			{ 0x1, "Axe" },
            { 0x2, "Bow" },
            { 0x3, "Crossbow" },
            { 0x4, "Dagger" },
            { 0x5, "Mace" },
            { 0x6, "Melee Defense" },
            { 0x7, "Missile Defense" },
            { 0x8, "Sling" },
            { 0x9, "Spear" },
            { 0xA, "Staff" },
            { 0xB, "Sword" },
            { 0xC, "Thrown Weapons" },
            { 0xD, "Unarmed Combat" },
            { 0xE, "Arcane Lore" },
            { 0xF, "Magic Defense" },
            { 0x10, "Mana Conversion" },
            { 0x12, "Item Tinkering" },
            { 0x13, "Assess Person" },
            { 0x14, "Deception" },
            { 0x15, "Healing" },
            { 0x16, "Jump" },
            { 0x17, "Lockpick" },
            { 0x18, "Run" },
            { 0x1B, "Assess Creature" },
            { 0x1C, "Weapon Tinkering" },
            { 0x1D, "Armor Tinkering" },
            { 0x1E, "Magic Item Tinkering" },
            { 0x1F, "Creature Enchantment" },
            { 0x20, "Item Enchantment" },
            { 0x21, "Life Magic" },
            { 0x22, "War Magic" },
            { 0x23, "Leadership" },
            { 0x24, "Loyalty" },
            { 0x25, "Fletching" },
            { 0x26, "Alchemy" },
            { 0x27, "Cooking" },
            { 0x28, "Salvaging" },
            { 0x29, "Two Handed Combat" },
            { 0x2A, "Gearcraft"},
            { 0x2B, "Void" },
            { 0x2C, "Heavy Weapons" },
            { 0x2D, "Light Weapons" },
            { 0x2E, "Finesse Weapons" },
            { 0x2F, "Missile Weapons" },
            { 0x30, "Shield" },
            { 0x31, "Dual Wield" },
            { 0x32, "Recklessness" },
            { 0x33, "Sneak Attack" },
            { 0x34, "Dirty Fighting" },
            { 0x35, "Challenge" },
            { 0x36, "Summoning" },
        };

        /// <summary>
        /// Returns a dictionary of mastery ids vs names
        /// </summary>
        /// <returns></returns>
        public static Dictionary<int, string> MasteryInfo = new Dictionary<int, string>
        {
            { 1, "Unarmed Weapon" },
            { 2, "Sword" },
            { 3, "Axe" },
            { 4, "Mace" },
            { 5, "Spear" },
            { 6, "Dagger" },
            { 7, "Staff" },
            { 8, "Bow" },
            { 9, "Crossbow" },
            { 10, "Thrown" },
            { 11, "Two Handed Combat" },
        };

        /// <summary>
        /// Returns a dictionary of attribute set ids vs names
        /// </summary>
        /// <returns></returns>
        public static Dictionary<int, string> AttributeSetInfo = new Dictionary<int, string>
        {
			// This list was taken from Virindi Tank Loot Editor
			// 01
			{ 02, "Test"},
			// 03
			{ 04, "Carraida's Benediction"},
            { 05, "Noble Relic Set" },
            { 06, "Ancient Relic Set" },
            { 07, "Relic Alduressa Set" },
            { 08, "Shou-jen Set" },
            { 09, "Empyrean Rings Set" },
            { 10, "Arm, Mind, Heart Set" },
            { 11, "Coat of the Perfect Light Set" },
            { 12, "Leggings of Perfect Light Set" },
            { 13, "Soldier's Set" },
            { 14, "Adept's Set" },
            { 15, "Archer's Set" },
            { 16, "Defender's Set" },
            { 17, "Tinker's Set" },
            { 18, "Crafter's Set" },
            { 19, "Hearty Set" },
            { 20, "Dexterous Set" },
            { 21, "Wise Set" },
            { 22, "Swift Set" },
            { 23, "Hardenend Set" },
            { 24, "Reinforced Set" },
            { 25, "Interlocking Set" },
            { 26, "Flame Proof Set" },
            { 27, "Acid Proof Set" },
            { 28, "Cold Proof Set" },
            { 29, "Lightning Proof Set" },
            { 30, "Dedication Set" },
            { 31, "Gladiatorial Clothing Set" },
            { 32, "Ceremonial Clothing" },
            { 33, "Protective Clothing" },
            { 34, "Noobie Armor" },
            { 35, "Sigil of Defense" },
            { 36, "Sigil of Destruction" },
            { 37, "Sigil of Fury" },
            { 38, "Sigil of Growth" },
            { 39, "Sigil of Vigor" },
            { 40, "Heroic Protector Set" },
            { 41, "Heroic Destroyer Set" },
            { 42, "Olthoi Armor D Red" },
            { 43, "Olthoi Armor C Rat" },
            { 44, "Olthoi Armor C Red" },
            { 45, "Olthoi Armor D Rat" },
            { 46, "Upgraded Relic Alduressa Set" },
            { 47, "Upgraded Ancient Relic Set" },
            { 48, "Upgraded Noble Relic Set" },
            { 49, "Weave of Alchemy" },
            { 50, "Weave of Arcane Lore" },
            { 51, "Weave of Armor Tinkering" },
            { 52, "Weave of Assess Person" },
            { 53, "Weave of Light Weapons" },
            { 54, "Weave of Missile Weapons" },
            { 55, "Weave of Cooking" },
            { 56, "Weave of Creature Enchantment" },
            { 57, "Weave of Missile Weapons" },
            { 58, "Weave of Finesse" },
            { 59, "Weave of Deception" },
            { 60, "Weave of Fletching" },
            { 61, "Weave of Healing" },
            { 62, "Weave of Item Enchantment" },
            { 63, "Weave of Item Tinkering" },
            { 64, "Weave of Leadership" },
            { 65, "Weave of Life Magic" },
            { 66, "Weave of Loyalty" },
            { 67, "Weave of Light Weapons" },
            { 68, "Weave of Magic Defense" },
            { 69, "Weave of Magic Item Tinkering" },
            { 70, "Weave of Mana Conversion" },
            { 71, "Weave of Melee Defense" },
            { 72, "Weave of Missile Defense" },
            { 73, "Weave of Salvaging" },
            { 74, "Weave of Light Weapons" },
            { 75, "Weave of Light Weapons" },
            { 76, "Weave of Heavy Weapons" },
            { 77, "Weave of Missile Weapons" },
            { 78, "Weave of Two Handed Combat" },
            { 79, "Weave of Light Weapons" },
            { 80, "Weave of Void Magic" },
            { 81, "Weave of War Magic" },
            { 82, "Weave of Weapon Tinkering" },
            { 83, "Weave of Assess Creature " },
            { 84, "Weave of Dirty Fighting" },
            { 85, "Weave of Dual Wield" },
            { 86, "Weave of Recklessness" },
            { 87, "Weave of Shield" },
            { 88, "Weave of Sneak Attack" },
            { 89, "Ninja_New" },
            { 90, "Weave of Summoning" },

            { 91, "Shrouded Soul" },
            { 92, "Darkened Mind" },
            { 93, "Clouded Spirit" },
            { 94, "Minor Stinging Shrouded Soul" },
            { 95, "Minor Sparking Shrouded Soul" },
            { 96, "Minor Smoldering Shrouded Soul" },
            { 97, "Minor Shivering Shrouded Soul" },
            { 98, "Minor Stinging Darkened Mind" },
            { 99, "Minor Sparking Darkened Mind" },

            { 100, "Minor Smoldering Darkened Mind" },
            { 101, "Minor Shivering Darkened Mind" },
            { 102, "Minor Stinging Clouded Spirit" },
            { 103, "Minor Sparking Clouded Spirit" },
            { 104, "Minor Smoldering Clouded Spirit" },
            { 105, "Minor Shivering Clouded Spirit" },
            { 106, "Major Stinging Shrouded Soul" },
            { 107, "Major Sparking Shrouded Soul" },
            { 108, "Major Smoldering Shrouded Soul" },
            { 109, "Major Shivering Shrouded Soul" },

            { 110, "Major Stinging Darkened Mind" },
            { 111, "Major Sparking Darkened Mind" },
            { 112, "Major Smoldering Darkened Mind" },
            { 113, "Major Shivering Darkened Mind" },
            { 114, "Major Stinging Clouded Spirit" },
            { 115, "Major Sparking Clouded Spirit" },
            { 116, "Major Smoldering Clouded Spirit" },
            { 117, "Major Shivering Clouded Spirit" },
            { 118, "Blackfire Stinging Shrouded Soul" },
            { 119, "Blackfire Sparking Shrouded Soul" },

            { 120, "Blackfire Smoldering Shrouded Soul" },
            { 121, "Blackfire Shivering Shrouded Soul" },
            { 122, "Blackfire Stinging Darkened Mind" },
            { 123, "Blackfire Sparking Darkened Mind" },
            { 124, "Blackfire Smoldering Darkened Mind" },
            { 125, "Blackfire Shivering Darkened Mind" },
            { 126, "Blackfire Stinging Clouded Spirit" },
            { 127, "Blackfire Sparking Clouded Spirit" },
            { 128, "Blackfire Smoldering Clouded Spirit" },
            { 129, "Blackfire Shivering Clouded Spirit" },

            { 130, "Shimmering Shadows" },

            { 131, "Brown Society Locket" },
            { 132, "Yellow Society Locket" },
            { 133, "Red Society Band" },
            { 134, "Green Society Band" },
            { 135, "Purple Society Band" },
            { 136, "Blue Society Band" },

            { 137, "Gauntlet Garb" },

            { 138, "UNKNOWN_138" }, // Possibly Paragon Missile Weapons
			{ 139, "UNKNOWN_139" }, // Possibly Paragon Casters
			{ 140, "UNKNOWN_140" }, // Possibly Paragon Melee Weapons
		};

        /// <summary>
        /// Returns a dictionary of material ids vs names
        /// </summary>
        /// <returns></returns>
        public static Dictionary<int, string> MaterialInfo = new Dictionary<int, string>
        {
            { 1, "Ceramic" },
            { 2, "Porcelain" },
			// 3
			{ 4, "Linen" },
            { 5, "Satin" },
            { 6, "Silk" },
            { 7, "Velvet" },
            { 8, "Wool" },
			// 9
			{ 10, "Agate" },
            { 11, "Amber" },
            { 12, "Amethyst" },
            { 13, "Aquamarine" },
            { 14, "Azurite" },
            { 15, "Black Garnet" },
            { 16, "Black Opal" },
            { 17, "Bloodstone" },
            { 18, "Carnelian" },
            { 19, "Citrine" },
            { 20, "Diamond" },
            { 21, "Emerald" },
            { 22, "Fire Opal" },
            { 23, "Green Garnet" },
            { 24, "Green Jade" },
            { 25, "Hematite" },
            { 26, "Imperial Topaz" },
            { 27, "Jet" },
            { 28, "Lapis Lazuli" },
            { 29, "Lavender Jade" },
            { 30, "Malachite" },
            { 31, "Moonstone" },
            { 32, "Onyx" },
            { 33, "Opal" },
            { 34, "Peridot" },
            { 35, "Red Garnet" },
            { 36, "Red Jade" },
            { 37, "Rose Quartz" },
            { 38, "Ruby" },
            { 39, "Sapphire" },
            { 40, "Smokey Quartz" },
            { 41, "Sunstone" },
            { 42, "Tiger Eye" },
            { 43, "Tourmaline" },
            { 44, "Turquoise" },
            { 45, "White Jade" },
            { 46, "White Quartz" },
            { 47, "White Sapphire" },
            { 48, "Yellow Garnet" },
            { 49, "Yellow Topaz" },
            { 50, "Zircon" },
            { 51, "Ivory" },
            { 52, "Leather" },
            { 53, "Armoredillo Hide" },
            { 54, "Gromnie Hide" },
            { 55, "Reed Shark Hide" },
			// 56
			{ 57, "Brass" },
            { 58, "Bronze" },
            { 59, "Copper" },
            { 60, "Gold" },
            { 61, "Iron" },
            { 62, "Pyreal" },
            { 63, "Silver" },
            { 64, "Steel" },
			// 65
			{ 66, "Alabaster" },
            { 67, "Granite" },
            { 68, "Marble" },
            { 69, "Obsidian" },
            { 70, "Sandstone" },
            { 71, "Serpentine" },
            { 73, "Ebony" },
            { 74, "Mahogany" },
            { 75, "Oak" },
            { 76, "Pine" },
            { 77, "Teak" },
        };

        public struct SpellInfo<T> {
            public readonly int Key;
            public readonly T Change;
            public readonly T Bonus;

            public SpellInfo(int key, T change, T bonus = default(T)) {
                Key = key;
                Change = change;
                Bonus = bonus;
            }
        }

        // Taken from Decal.Adapter.Wrappers.LongValueKey
        const int LongValueKey_MaxDamage = 218103842;
        const int LongValueKey_ArmorLevel = 28;

        public static readonly Dictionary<int, SpellInfo<int>> LongValueKeySpellEffects = new Dictionary<int, SpellInfo<int>>()
        {
			// In 2012 they removed these item spells and converted them to auras that are cast on the player, not on the item.
			{ 1616, new SpellInfo<int>(LongValueKey_MaxDamage, 20)}, // Blood Drinker VI
			{ 2096, new SpellInfo<int>(LongValueKey_MaxDamage, 22)}, // Infected Caress
			//{ 5183, new SpellInfo<LongValueKey>(LongValueKey_MaxDamage, 22)}, // Incantation of Blood Drinker Pre Feb-2013
			//{ 4395, new SpellInfo<LongValueKey>(LongValueKey_MaxDamage, 24, 2)}, // Incantation of Blood Drinker, this spell on the item adds 2 more points of damage over a user casted 8 Pre Feb-2013
			{ 5183, new SpellInfo<int>(LongValueKey_MaxDamage, 24)}, // Incantation of Blood Drinker Post Feb-2013
			{ 4395, new SpellInfo<int>(LongValueKey_MaxDamage, 24)}, // Incantation of Blood Drinker Post Feb-2013

			{ 2598, new SpellInfo<int>(LongValueKey_MaxDamage, 2, 2)}, // Minor Blood Thirst
			{ 2586, new SpellInfo<int>(LongValueKey_MaxDamage, 4, 4)}, // Major Blood Thirst
			{ 4661, new SpellInfo<int>(LongValueKey_MaxDamage, 7, 7)}, // Epic Blood Thirst
			{ 6089, new SpellInfo<int>(LongValueKey_MaxDamage, 10, 10)}, // Legendary Blood Thirst

			{ 3688, new SpellInfo<int>(LongValueKey_MaxDamage, 300)}, // Prodigal Blood Drinker


			{ 1486, new SpellInfo<int>(LongValueKey_ArmorLevel, 200)}, // Impenetrability VI
			{ 2108, new SpellInfo<int>(LongValueKey_ArmorLevel, 220)}, // Brogard's Defiance
			{ 4407, new SpellInfo<int>(LongValueKey_ArmorLevel, 240)}, // Incantation of Impenetrability

			{ 2604, new SpellInfo<int>(LongValueKey_ArmorLevel, 20, 20)}, // Minor Impenetrability
			{ 2592, new SpellInfo<int>(LongValueKey_ArmorLevel, 40, 40)}, // Major Impenetrability
			{ 4667, new SpellInfo<int>(LongValueKey_ArmorLevel, 60, 60)}, // Epic Impenetrability
			{ 6095, new SpellInfo<int>(LongValueKey_ArmorLevel, 80, 80)}, // Legendary Impenetrability
		};

        // Taken from Decal.Adapter.Wrappers.DoubleValueKey
        const int DoubleValueKey_ElementalDamageVersusMonsters = 152;
        const int DoubleValueKey_AttackBonus = 167772172;
        const int DoubleValueKey_MeleeDefenseBonus = 29;
        const int DoubleValueKey_ManaCBonus = 144;

        public static readonly Dictionary<int, SpellInfo<double>> DoubleValueKeySpellEffects = new Dictionary<int, SpellInfo<double>>()
        {
			// In 2012 they removed these item spells and converted them to auras that are cast on the player, not on the item.
			{ 3258, new SpellInfo<double>(DoubleValueKey_ElementalDamageVersusMonsters, .06)}, // Spirit Drinker VI
			{ 3259, new SpellInfo<double>(DoubleValueKey_ElementalDamageVersusMonsters, .07)}, // Infected Spirit Caress
			//{ 5182, new SpellInfo<double>(DoubleValueKey_ElementalDamageVersusMonsters, .07)}, // Incantation of Spirit Drinker Pre Feb-2013
			//{ 4414, new SpellInfo<double>(DoubleValueKey_ElementalDamageVersusMonsters, .08, .01)}, // Incantation of Spirit Drinker, this spell on the item adds 1 more % of damage over a user casted 8 Pre Feb-2013
			{ 5182, new SpellInfo<double>(DoubleValueKey_ElementalDamageVersusMonsters, .08)}, // Incantation of Spirit Drinker Post Feb-2013
			{ 4414, new SpellInfo<double>(DoubleValueKey_ElementalDamageVersusMonsters, .08)}, // Incantation of Spirit Drinker, this spell on the item adds 1 more % of damage over a user casted 8 Post Feb-2013

			{ 3251, new SpellInfo<double>(DoubleValueKey_ElementalDamageVersusMonsters, .01, .01)}, // Minor Spirit Thirst
			{ 3250, new SpellInfo<double>(DoubleValueKey_ElementalDamageVersusMonsters, .03, .03)}, // Major Spirit Thirst
			{ 4670, new SpellInfo<double>(DoubleValueKey_ElementalDamageVersusMonsters, .05, .05)}, // Epic Spirit Thirst
			{ 6098, new SpellInfo<double>(DoubleValueKey_ElementalDamageVersusMonsters, .07, .07)}, // Legendary Spirit Thirst

			{ 3735, new SpellInfo<double>(DoubleValueKey_ElementalDamageVersusMonsters, .15)}, // Prodigal Spirit Drinker


			// In 2012 they removed these item spells and converted them to auras that are cast on the player, not on the item.
			{ 1592, new SpellInfo<double>(DoubleValueKey_AttackBonus, .15)}, // Heart Seeker VI
			{ 2106, new SpellInfo<double>(DoubleValueKey_AttackBonus, .17)}, // Elysa's Sight
			{ 4405, new SpellInfo<double>(DoubleValueKey_AttackBonus, .20)}, // Incantation of Heart Seeker

			{ 2603, new SpellInfo<double>(DoubleValueKey_AttackBonus, .03, .03)}, // Minor Heart Thirst
			{ 2591, new SpellInfo<double>(DoubleValueKey_AttackBonus, .05, .05)}, // Major Heart Thirst
			{ 4666, new SpellInfo<double>(DoubleValueKey_AttackBonus, .07, .07)}, // Epic Heart Thirst
			{ 6094, new SpellInfo<double>(DoubleValueKey_AttackBonus, .09, .09)}, // Legendary Heart Thirst


			// In 2012 they removed these item spells and converted them to auras that are cast on the player, not on the item.
			{ 1605, new SpellInfo<double>(DoubleValueKey_MeleeDefenseBonus, .15)}, // Defender VI
			{ 2101, new SpellInfo<double>(DoubleValueKey_MeleeDefenseBonus, .17)}, // Cragstone's Will
			//{ 4400, new SpellInfo<double>(DoubleValueKey_MeleeDefenseBonus, .17)}, // Incantation of Defender Pre Feb-2013
			{ 4400, new SpellInfo<double>(DoubleValueKey_MeleeDefenseBonus, .20)}, // Incantation of Defender Post Feb-2013

			{ 2600, new SpellInfo<double>(DoubleValueKey_MeleeDefenseBonus, .03, .03)}, // Minor Defender
			{ 3985, new SpellInfo<double>(DoubleValueKey_MeleeDefenseBonus, .04, .04)}, // Mukkir Sense
			{ 2588, new SpellInfo<double>(DoubleValueKey_MeleeDefenseBonus, .05, .05)}, // Major Defender
			{ 4663, new SpellInfo<double>(DoubleValueKey_MeleeDefenseBonus, .07, .07)}, // Epic Defender
			{ 6091, new SpellInfo<double>(DoubleValueKey_MeleeDefenseBonus, .09, .09)}, // Legendary Defender

			{ 3699, new SpellInfo<double>(DoubleValueKey_MeleeDefenseBonus, .25)}, // Prodigal Defender


			// In 2012 they removed these item spells and converted them to auras that are cast on the player, not on the item.
			{ 1480, new SpellInfo<double>(DoubleValueKey_ManaCBonus, 1.60)}, // Hermetic Link VI
			{ 2117, new SpellInfo<double>(DoubleValueKey_ManaCBonus, 1.70)}, // Mystic's Blessing
			{ 4418, new SpellInfo<double>(DoubleValueKey_ManaCBonus, 1.80)}, // Incantation of Hermetic Link

			{ 3201, new SpellInfo<double>(DoubleValueKey_ManaCBonus, 1.05, 1.05)}, // Feeble Hermetic Link
			{ 3199, new SpellInfo<double>(DoubleValueKey_ManaCBonus, 1.10, 1.10)}, // Minor Hermetic Link
			{ 3202, new SpellInfo<double>(DoubleValueKey_ManaCBonus, 1.15, 1.15)}, // Moderate Hermetic Link
			{ 3200, new SpellInfo<double>(DoubleValueKey_ManaCBonus, 1.20, 1.20)}, // Major Hermetic Link
			{ 6086, new SpellInfo<double>(DoubleValueKey_ManaCBonus, 1.25, 1.25)}, // Epic Hermetic Link
			{ 6087, new SpellInfo<double>(DoubleValueKey_ManaCBonus, 1.30, 1.30)}, // Legendary Hermetic Link
		};
    }
    #endregion
}
