using System;
using uTank2.LootPlugins;
using UtilityBelt.Lib;
using UtilityBelt.Service.Lib.Settings;
using Decal.Adapter;
using Decal.Adapter.Wrappers;
using Decal.Filters;
using System.Collections.Generic;
using System.Globalization;
using BoolValueKey = uTank2.LootPlugins.BoolValueKey;
using DoubleValueKey = Decal.Adapter.Wrappers.DoubleValueKey;
using ObjectClass = Decal.Adapter.Wrappers.ObjectClass;
using StringValueKey = uTank2.LootPlugins.StringValueKey;
using UtilityBelt.Lib.Constants;
using UtilityBelt.Lib.ItemInfoHelper;
using static UtilityBelt.Lib.ItemInfoHelper.MiscCalcs;
using System.Linq;

namespace UtilityBelt.Tools {
    [Name("ItemInfo")]
    public class ItemInfo : ToolBase {
        #region Config

        [Summary("Enable ItemInfo")]
        public readonly Setting<bool> Enabled = new Setting<bool>(false);

        [Summary("Describe items when selected")]
        public readonly Setting<bool> DescribeOnSelect = new Setting<bool>(false);

        [Summary("Describe lootable items found by Looter")]
        public readonly Setting<bool> DescribeOnLoot = new Setting<bool>(true);

        [Summary("Display buffed values")]
        public readonly Setting<bool> ShowMagtoolsBuffedValues = new Setting<bool>(false);

        [Summary("Display damage calculations for missile/melee weps")]
        public readonly Setting<bool> ShowDamage = new Setting<bool>(false);

        [Summary("Display value and burden")]
        public readonly Setting<bool> ShowValueAndBurden = new Setting<bool>(false);

        [Summary("Copy to clipboard")]
        public readonly Setting<bool> AutoClipboard = new Setting<bool>(false);

        [Summary("Show comparison to max retail damage weapons (compares to it's own tier)")]
        public readonly Setting<bool> ShowRetailComparison = new Setting<bool>(false);

        [Summary("Show retail max values next to actual values")]
        public readonly Setting<bool> ShowRetailMax = new Setting<bool>(false);
        #endregion

        private bool scanningItem = false;
        private WorldObject targetItem = null;
        private bool subscribed;

        public ItemInfo(UtilityBeltPlugin ub, string name) : base(ub, name) { }

        public override void Init() {
            base.Init();

            try {
                Enabled.Changed += Enabled_Changed;
                DescribeOnSelect.Changed += DescribeOnSelect_Changed;

                //if (UBHelper.Core.GameState != UBHelper.GameState.In_Game)
                //    UB.Core.CharacterFilter.LoginComplete += CharacterFilter_LoginComplete;
                //else
                    UpdateSubscriptions();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Enabled_Changed(object sender, SettingChangedEventArgs e) {
            try { 
            UpdateSubscriptions();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UpdateSubscriptions() {
            if (Enabled.Value && DescribeOnSelect.Value && !subscribed) {
                subscribed = true;
                UB.Core.ItemSelected += Core_ItemSelected;
            }
            else if (subscribed && Enabled.Value && !DescribeOnSelect.Value) {
                UB.Core.ItemSelected -= Core_ItemSelected;
                subscribed = false;
            }
            else if (subscribed && !Enabled.Value && DescribeOnSelect.Value) {
                UB.Core.ItemSelected -= Core_ItemSelected;
                subscribed = false;
            }
            else if (subscribed && !Enabled.Value && !DescribeOnSelect.Value){
                UB.Core.ItemSelected -= Core_ItemSelected;
                subscribed = false;
            }
        }

        #region Event Handlers
        //private void CharacterFilter_LoginComplete(object sender, EventArgs e) {
        //    UB.Core.CharacterFilter.LoginComplete -= CharacterFilter_LoginComplete;
        //    TryEnable();
        //}

        private void DescribeOnSelect_Changed(object sender, SettingChangedEventArgs e) {
            try { 
            UpdateSubscriptions();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Core_ItemSelected(object sender, ItemSelectedEventArgs e) {
            try { 
            //if (mouseClicked) {
                if (UB.Core.Actions.IsValidObject(e.ItemGuid)) {
                    targetItem = UB.Core.WorldFilter[e.ItemGuid];
                    if (!targetItem.HasIdData) {
                        UB.Assessor.Request(targetItem.Id);
                        UB.Core.RenderFrame += Core_RenderFrame;
                        scanningItem = true;
                    }
                    else {
                    DisplayItem(targetItem.Id);
                        Stop();
                    }
                }
                else {
                    Logger.Debug("not a valid item - ignoring");
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Core_RenderFrame(object sender, EventArgs e) {
            try {
                if (targetItem == null) return;
                if (targetItem.HasIdData) {
                    DisplayItem(targetItem.Id);
                    UB.Core.RenderFrame -= Core_RenderFrame;
                    Stop();
                }
                else {
                    //Logger.WriteToChat("waiting on data");
                }
            }
            catch (Exception ex) {
                Logger.LogException(ex);
            }
        }
        #endregion // Event Handlers

        private void Stop() {
            scanningItem = false;
            targetItem = null;
        }

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
            //sb.Append(isLoot ? $"+({ruleName})" : "-");

            //Colorize loot rule before adding itemId description
            if (string.IsNullOrEmpty(ruleName)) {
                sb.Insert(0, $"<Tell:IIDString:{Util.GetChatId()}:select|{itemInfo.Id}>{Util.GetObjectName(itemInfo.Id)}</Tell >");
            }
            else {
                sb.Insert(0, $"<Tell:IIDString:{Util.GetChatId()}:select|{itemInfo.Id}>{ ruleName + " - " + Util.GetObjectName(itemInfo.Id)}</Tell >");
            }
            //sb.Append(@"<\\Tell>");

            //Add MagTools item description
            sb.Append(new ItemDescriptions(CoreManager.Current.WorldFilter[item])).Append(" ");

            if (!silent)
                Logger.WriteToChat("ItemInfo: " + sb.ToString());
                //MyClasses.VCS_Connector.SendChatTextCategorized("IDs", sb.ToString(), 13, 0);

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
    public class ItemDescriptions {
        private readonly WorldObject wo;
        private readonly MyWorldObject mwo;

        public ItemDescriptions(WorldObject worldObject) {
            wo = worldObject;
            mwo = MyWorldObjectCreator.Create(worldObject);
        }

        public override string ToString() {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            if (wo.Values(LongValueKey.DamageType, 0) > 0 || wo.Values((LongValueKey)353) > 0) {
                sb.Append(" (");
                if (wo.Values(LongValueKey.DamageType, 0) > 0)
                    sb.Append((DamageTypes)wo.Values(LongValueKey.DamageType));
                if (wo.Values((LongValueKey)353) > 0) {
                    if (Dictionaries.MasteryInfo.ContainsKey(wo.Values((LongValueKey)353)))
                        sb.Append(" " + Dictionaries.MasteryInfo[wo.Values((LongValueKey)353)]);
                    else
                        sb.Append(" Unknown mastery " + wo.Values((LongValueKey)353));
                }
                sb.Append(")");
            }

            int set = wo.Values((LongValueKey)265, 0);
            if (set != 0) {
                sb.Append(", ");
                if (Dictionaries.AttributeSetInfo.ContainsKey(set)) {
                    sb.Append(Dictionaries.AttributeSetInfo[set]);
                    if (wo.Values((LongValueKey)319, 0) > 0) {
                        sb.Append(" (Lvl " + wo.Values((LongValueKey)319) + ")");
                    }
                }
                else
                    sb.Append("Unknown set " + set);
            }

            if (wo.ObjectClass == ObjectClass.Clothing && wo.Category == 4 && wo.Values(LongValueKey.EquipableSlots) == 134217728) {
                FileService service = CoreManager.Current.Filter<FileService>();
                if (wo.SpellCount > 0) {
                    string spell = service.SpellTable.GetById(wo.Spell(0)).ToString();
                    sb.Append(", " + spell);
                }
                else {
                    sb.Append(", " + "-200 Damage Chance");
                    //if (wo.Values((LongValueKey)352, 0) == 2) {
                }
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

            if (wo.Values((DoubleValueKey)155, 0) != 0)
                sb.Append(", AC");


            if (wo.Values((DoubleValueKey)136, 0) >= 1)
                sb.Append(", CrushB (" + wo.Values((DoubleValueKey)136, 0) +")");

            if (wo.Values((DoubleValueKey)147, 0) >= 1)
                sb.Append(", BS (" + wo.Values((DoubleValueKey)147, 0) + ")");



            if (wo.Values((LongValueKey)166, 0) >= 1) {
                int slayer = wo.Values((LongValueKey)166);
                if (Dictionaries.Slayers.ContainsKey(slayer))
                    sb.Append(", " + Dictionaries.Slayers[slayer] + " Slayer");
            }

            if (wo.Values((LongValueKey)47, 0) != 0) {
                var attackType = wo.Values((LongValueKey)47);
                if (attackType == 4 && wo.Values((LongValueKey)353, 0) == 11)
                    sb.Append(", Cleaving");
                else if (attackType == 160 || attackType == 166)
                    sb.Append(", Multi-Strike");
                else if (attackType == 486)
                    sb.Append(", Multi-Strike (3)");
            }

            if (wo.Values(LongValueKey.NumberTimesTinkered) > 0)
                sb.Append(", Tinks " + wo.Values(LongValueKey.NumberTimesTinkered));

            if (wo.ObjectClass != ObjectClass.MissileWeapon) {
                if (wo.Values(LongValueKey.MaxDamage) != 0 && wo.Values(DoubleValueKey.Variance) != 0) {
                    sb.Append(", " + (wo.Values(LongValueKey.MaxDamage) - (wo.Values(LongValueKey.MaxDamage) * wo.Values(DoubleValueKey.Variance))).ToString("N2") + "-" + wo.Values(LongValueKey.MaxDamage));
                    if (UtilityBeltPlugin.Instance.ItemInfo.ShowRetailMax)
                        sb.Append("(" + GetMaxProperty(wo, WeaponProperty.MaxDmg).ToString() + ")");
                }
                else if (wo.Values(LongValueKey.MaxDamage) != 0 && wo.Values(DoubleValueKey.Variance) == 0) {
                    sb.Append(", " + wo.Values(LongValueKey.MaxDamage));
                    //sb.Append("(" + mwo.GetBuffedIntValueKey(218103842).ToString() + ")");
                    if (wo.Values(LongValueKey.Workmanship, 0) > 0 && UtilityBeltPlugin.Instance.ItemInfo.ShowRetailMax)
                        sb.Append("(" + GetMaxProperty(wo, WeaponProperty.MaxDmg).ToString() + ")");
                }
            }

            if (wo.Values(DoubleValueKey.Variance) > 0) {
                sb.Append(", " + Math.Round(wo.Values(DoubleValueKey.Variance), 2).ToString() + "v");
                //MyWorldObject.GetMaxProperty(wo, MyWorldObject.WeaponProperty.MaxVar);
                if (UtilityBeltPlugin.Instance.ItemInfo.ShowRetailMax)
                    sb.Append("(" + GetMaxProperty(wo, WeaponProperty.MaxVar).ToString() + ")");
            }

            if (wo.Values(LongValueKey.ElementalDmgBonus, 0) != 0) {
                sb.Append(", +" + wo.Values(LongValueKey.ElementalDmgBonus));
                if (UtilityBeltPlugin.Instance.ItemInfo.ShowRetailMax) 
                    sb.Append("(" + GetMaxProperty(wo, WeaponProperty.MaxElementalDmgBonus).ToString() + ")");
            }

            if (wo.Values(DoubleValueKey.DamageBonus, 1) != 1) {
                sb.Append(", +" + Math.Round(((wo.Values(DoubleValueKey.DamageBonus) - 1) * 100)) + "%");
                if (UtilityBeltPlugin.Instance.ItemInfo.ShowRetailMax)
                    sb.Append("(" + GetMaxProperty(wo, WeaponProperty.MaxDmgMod).ToString() + ")");
            }

            if (wo.Values(DoubleValueKey.ElementalDamageVersusMonsters, 1) != 1) {
                sb.Append(", +" + Math.Round(((wo.Values(DoubleValueKey.ElementalDamageVersusMonsters) - 1) * 100)) + "%");
                if (UtilityBeltPlugin.Instance.ItemInfo.ShowRetailMax)
                    sb.Append("(" + ((GetMaxProperty(wo, WeaponProperty.MaxElementalDmgVsMonsters) - 1) * 100).ToString() + ")");
                sb.Append(" vs. Monsters");
            }

                if (wo.Values(DoubleValueKey.AttackBonus, 1) != 1) {
                sb.Append(", " + Math.Round((mwo.AttackBonus - 1) * 100) + "%a");
                if (mwo.AttackBonus != mwo.BuffedAttackBonus)
                    sb.Append(" (" + Math.Round((mwo.BuffedAttackBonus - 1) * 100) + ")");
            }

            if (wo.Values(DoubleValueKey.MeleeDefenseBonus, 1) != 1) {
                sb.Append(", " + Math.Round(((wo.Values(DoubleValueKey.MeleeDefenseBonus) - 1) * 100)) + "%md");
                if (mwo.MeleeDefenseBonus != mwo.BuffedMeleeDefenseBonus)
                    sb.Append(" (" + Math.Round((mwo.BuffedMeleeDefenseBonus - 1) * 100) + ")");
            }

            if (wo.Values(DoubleValueKey.MagicDBonus, 1) != 1)
                sb.Append(", " + Math.Round(((wo.Values(DoubleValueKey.MagicDBonus) - 1) * 100), 1) + "%mgc.d");

            if (wo.Values(DoubleValueKey.MissileDBonus, 1) != 1)
                sb.Append(", " + Math.Round(((wo.Values(DoubleValueKey.MissileDBonus) - 1) * 100), 1) + "%msl.d");

            if (wo.Values(DoubleValueKey.ManaCBonus) != 0)
                sb.Append(", " + Math.Round((wo.Values(DoubleValueKey.ManaCBonus) * 100)) + "%mc");



            if (UtilityBeltPlugin.Instance.ItemInfo.ShowMagtoolsBuffedValues.Value &&
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
                //}
            }

            if (UtilityBeltPlugin.Instance.ItemInfo.ShowDamage) {

                if (wo.ObjectClass == ObjectClass.MissileWeapon) {
                    sb.Append(", Damage " + Math.Round(mwo.CalcMissileDamage, 2).ToString());
                }

                if (wo.ObjectClass == ObjectClass.MeleeWeapon) {
                    sb.Append(", Damage " + Math.Round(mwo.CalcMeleeDamage, 2).ToString());
                }
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

            if (wo.ObjectClass == ObjectClass.MeleeWeapon) {
                if (UtilityBeltPlugin.Instance.ItemInfo.ShowRetailComparison) {
                    double od = mwo.CalcMeleeDamage - (GetMaxProperty(wo, WeaponProperty.MaxDmg));
                    sb.Append(" (OD +" + Math.Round(od, 2).ToString() + ")");
                }
            }

            if (wo.ObjectClass == ObjectClass.MissileWeapon) {
                if (UtilityBeltPlugin.Instance.ItemInfo.ShowRetailComparison) {
                    double od = mwo.CalcMissileDamage - (GetMaxProperty(wo, WeaponProperty.MaxElementalDmgBonus) + 24 + mwo.MaxArrowDmg);
                    sb.Append(" (OD +" + Math.Round(od, 2).ToString() + ")");
                }
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

            if (UtilityBeltPlugin.Instance.ItemInfo.ShowValueAndBurden.Value
                //Settings.SettingsManager.ItemInfoOnIdent.ShowValueAndBurden.Value
                ) {
                if (wo.Values(LongValueKey.Value) > 0)
                    sb.Append(", Value " + string.Format("{0:n0}", wo.Values(LongValueKey.Value)));

                if (wo.Values(LongValueKey.Burden) > 0)
                    sb.Append(", BU " + wo.Values(LongValueKey.Burden));
            }

            if (wo.ObjectClass != ObjectClass.Player) {
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
            }
            else {
                double dmg = wo.Values((LongValueKey)307, 0);
                double dmgRes = wo.Values((LongValueKey)308, 0);
                double critDmg = wo.Values((LongValueKey)314, 0);
                double critDmgRes = wo.Values((LongValueKey)316, 0);

                if (dmg + dmgRes + critDmg + critDmgRes > 0) {
                    sb.Append(", [");
                    bool first = true;
                    if (dmg > 0) { sb.Append("D " + dmg.ToString()); first = false; }
                    if (dmgRes > 0) { if (!first) sb.Append(", "); sb.Append("DR " + dmgRes.ToString()); first = false; }
                    if (critDmg > 0) { if (!first) sb.Append(", "); sb.Append("CD " + critDmg.ToString()); first = false; }
                    if (critDmgRes > 0) { if (!first) sb.Append(", "); sb.Append("CDR " + critDmgRes.ToString()); first = false; }
                    sb.Append("]");
                }
            }

            if (wo.ObjectClass == ObjectClass.Misc && wo.Name.Contains("Keyring"))
                sb.Append(", Keys: " + wo.Values(LongValueKey.KeysHeld) + ", Uses: " + wo.Values(LongValueKey.UsesRemaining));

            //summons
            if (wo.Values(LongValueKey.IconUnderlay) == 29728) {
                double dps = UtilityBelt.Lib.ItemInfoHelper.MiscCalcs.GetSummonDamage(wo);
                sb.Append(", Approx DPS: " + Math.Round(dps, 2).ToString());
            }

            if (!string.IsNullOrEmpty(wo.Values(Decal.Adapter.Wrappers.StringValueKey.Inscription))) {
                sb.Append(", Inscription: " + wo.Values(Decal.Adapter.Wrappers.StringValueKey.Inscription));
            }

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
                        if (spell == effect.Key && effect.Value.Where(x => x.Key == 28).Count() == 1)
                            armorFromBuffs += effect.Value.Find(x => x.Key == 28).Change;
                    }
                }

                foreach (int spell in Spells) {
                    foreach (var effect in Dictionaries.LongValueKeySpellEffects) {
                        if (spell == effect.Key && effect.Value.Where(x => x.Key == 28).Count() == 1)
                            armorFromBuffs -= effect.Value.Find(x => x.Key == 28).Bonus;
                    }
                }

                return ArmorLevel - armorFromTinks - armorFromBuffs;
            }
        }

        public double MaxArrowDmg {
            get {
                switch (IntValues[353]) {
                    case 8:
                        return 40;
                        break;
                    case 9:
                        return 53;
                        break;
                    case 10:
                        return 42;
                        break;
                    default:
                        return 0;
                }
                return 0;
            }
        }

        public double CalcMissileDamage {
            get {
                WorldObject wo = CoreManager.Current.WorldFilter[Id];
                double dmgMod = DoubleValues[63] * 100 - 100;
                double arrowMax = MaxArrowDmg;
                double numTimesTinkered = wo.Values(LongValueKey.NumberTimesTinkered, 0);
                double remainingTinks = 10;

                if (numTimesTinkered > 0) {
                    remainingTinks = remainingTinks - numTimesTinkered;
                    if (wo.Values(LongValueKey.Imbued, 0) == 0)
                        remainingTinks--; //218103842
                }
                else {
                    remainingTinks--;
                }
                double maxTinkedMissileMod = (GetMaxProperty(wo, WeaponProperty.MaxDmgMod) + 100 + 4 * 9) / 100;
                double buffedDmg = GetBuffedIntValueKey(218103842);
                if (buffedDmg <= 10) buffedDmg += 24;
                //[MissileOD = (1 + (DamageModifier + 36 {9x Mahogany Tinks})/100) * (ElementalDamage + BD + BT + AmmoMaxDamage)/MaxTinkedMissileMod] - (MaxElementalDamage + 24 {BD8}+ArrowMaxDamage]
                return (1 + (dmgMod + (4 * remainingTinks)) / 100) * (ElementalDmgBonus + buffedDmg + arrowMax) / maxTinkedMissileMod;
            }
        }

        public double CalcMeleeDamage {
            get {
                WorldObject wo = CoreManager.Current.WorldFilter[Id];
                double perfectVariance = MiscCalcs.GetMaxProperty(wo, WeaponProperty.MaxVar);
                double varianceTinks = Math.Round(Math.Log(perfectVariance / Variance, 0.8), 2);
                double odValue = GetBuffedIntValueKey(218103842) - varianceTinks;
                return odValue - Variance;
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

        public double BuffedElementalDamageBonus => GetBuffedIntValueKey(204); //204 is proper value, but it already exists in the dictionary so using maxdamage value for lookup

        public double BuffedAttackBonus => GetBuffedDoubleValueKey(167772172, -1);

        public double BuffedMeleeDefenseBonus => GetBuffedDoubleValueKey(29, -1);

        public double BuffedManaCBonus => GetBuffedDoubleValueKey(144, -1);

        public int GetBuffedIntValueKey(int key, int defaultValue = 0) {
            if (!IntValues.ContainsKey(key))
                return defaultValue;

            int value = IntValues[key];

            foreach (int spell in ActiveSpells) {
                if (!Dictionaries.LongValueKeySpellEffects.ContainsKey(spell))
                    continue;
                var matches = Dictionaries.LongValueKeySpellEffects[spell].Where(x => x.Key == key);
                if (matches.Count() == 1)
                    value -= matches.FirstOrDefault().Change;
            }

            foreach (int spell in Spells) {
                if (!Dictionaries.LongValueKeySpellEffects.ContainsKey(spell))
                    continue;
                var matches = Dictionaries.LongValueKeySpellEffects[spell].Where(x => x.Key == key);
                if (matches.Count() == 1)
                    value += matches.FirstOrDefault().Bonus;
            }

            return value;
        }

        public double GetBuffedDoubleValueKey(int key, double defaultValue = 0) {
            if (!DoubleValues.ContainsKey(key))
                return defaultValue;

            double value = DoubleValues[key];

            foreach (int spell in ActiveSpells) {
                if (Dictionaries.DoubleValueKeySpellEffects.ContainsKey(spell) && Dictionaries.DoubleValueKeySpellEffects[spell].Key == key) {
                    if (Math.Abs(Dictionaries.DoubleValueKeySpellEffects[spell].Change - 1) < double.Epsilon) {
                        value /= Dictionaries.DoubleValueKeySpellEffects[spell].Change;
                    }
                    else {
                        value -= Dictionaries.DoubleValueKeySpellEffects[spell].Change;
                    }
                }
            }

            foreach (int spell in Spells) {
                if (Dictionaries.DoubleValueKeySpellEffects.ContainsKey(spell) && Dictionaries.DoubleValueKeySpellEffects[spell].Key == key && Math.Abs(Dictionaries.DoubleValueKeySpellEffects[spell].Bonus - 0) > double.Epsilon) {
                    if (Math.Abs(Dictionaries.DoubleValueKeySpellEffects[spell].Change - 1) < double.Epsilon) {
                        value *= Dictionaries.DoubleValueKeySpellEffects[spell].Bonus;
                    }
                    else {
                    value += Dictionaries.DoubleValueKeySpellEffects[spell].Bonus;
                    }
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
}

#endregion