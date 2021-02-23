using Decal.Adapter.Wrappers;
using Decal.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib {
    public static class Spells {
        private static FileService fs = null;
        private static SpellTable SpellTable {
            get {
                if (fs != null)
                    return fs.SpellTable;
                fs = UtilityBeltPlugin.Instance.Core.Filter<FileService>();
                return fs.SpellTable;
            }
        }

        /// <summary>
        /// Checks if spellId is known by the player
        /// </summary>
        /// <param name="spellId">spellId to check</param>
        /// <returns></returns>
        public static bool IsKnown(int spellId) {
            return UtilityBeltPlugin.Instance.Core.CharacterFilter.IsSpellKnown(spellId);
        }

        /// <summary>
        /// Checks if the player has the required skill to cast spellId. Difficulty is modified by the 
        /// SpellDiffExcessThreshold-Hunt vtank setting.
        /// </summary>
        /// <param name="spellId">spell id to check</param>
        /// <returns></returns>
        public static bool HasSkillHunt(int spellId) {
            var spell = SpellTable.GetById(spellId);
            var minSkillRequired = spell.Difficulty + (int)UBHelper.vTank.Instance.GetSetting("SpellDiffExcessThreshold-Hunt");
            var effectiveSkill = GetEffectiveSkillForSpell(spell);

            //Util.WriteToChat($"School: {spell.School} Buffed: {effectiveSkill} minSkillRequired: {minSkillRequired}");

            return effectiveSkill >= minSkillRequired;
        }

        /// <summary>
        /// Checks if the player has the required skill to cast spellId. Difficulty is modified by the 
        /// SpellDiffExcessThreshold-Buff vtank setting.
        /// </summary>
        /// <param name="spellId">spell id to check</param>
        /// <returns></returns>
        public static bool HasSkillBuff(int spellId) {
            var spell = SpellTable.GetById(spellId);
            var minSkillRequired = spell.Difficulty + (int)UBHelper.vTank.Instance.GetSetting("SpellDiffExcessThreshold-Buff");
            var effectiveSkill = GetEffectiveSkillForSpell(spell);

            //Util.WriteToChat($"School: {spell.School} Buffed: {effectiveSkill} minSkillRequired: {minSkillRequired}");

            return effectiveSkill >= minSkillRequired;
        }
        public static Spell GetSpell(int spellId) {
            return SpellTable.GetById(spellId);
        }

        /// <summary>
        /// Checks if player has scarabs required for a spell (currently does not check tapers)
        /// </summary>
        /// <param name="spellId">id of the spell to check</param>
        /// <returns></returns>
        public static bool HasComponents(int spellId) {
            var spell = SpellTable.GetById(spellId);
            var neededComps = new Dictionary<string, int>();
            for (var i=0; i < spell.ComponentIDs.Length; i++) {
                var id = spell.ComponentIDs[i];
                var component = fs.ComponentTable.GetById(id);

                if (!component.Name.Contains("Scarab"))
                    continue;

                //Util.WriteToChat($"Component: {component.Name} burnRate:{component.BurnRate} gestureSpeed:{component.GestureSpeed} word:{component.Word}");

                if (!neededComps.ContainsKey(component.Name))
                    neededComps.Add(component.Name, 0);

                neededComps[component.Name]++;
            }

            foreach (var kv in neededComps) {
                if (Inventory.CountByName(kv.Key) < kv.Value)
                    return false;
            }

            return true;
        }

        public static string GetName(int spellId) {
            var spell = SpellTable.GetById(spellId);
            return spell == null ? $"UnknownSpell:{spellId}" : spell.Name;
        }

        public static int GetEffectiveSkillForSpell(int spellId) {
            return GetEffectiveSkillForSpell(SpellTable.GetById(spellId));
        }

        public static int GetEffectiveSkillForSpell(Spell spell) {
            var buffedSkill = 0;
            var cf = UtilityBeltPlugin.Instance.Core.CharacterFilter;

            switch (spell.School.ToString()) {
                case "Item Enchantment":
                    buffedSkill = cf.EffectiveSkill[CharFilterSkillType.ItemEnchantment];
                    break;
                case "War Magic":
                    buffedSkill = cf.EffectiveSkill[CharFilterSkillType.WarMagic];
                    break;
                case "Creature Enchantment":
                    buffedSkill = cf.EffectiveSkill[CharFilterSkillType.CreatureEnchantment];
                    break;
                case "Void Magic":
                    buffedSkill = cf.EffectiveSkill[CharFilterSkillType.VoidMagic];
                    break;
                case "Life Magic":
                    buffedSkill = cf.EffectiveSkill[CharFilterSkillType.LifeMagic];
                    break;
            }

            if (cf.GetCharProperty(326/*Jack of All Trades*/) == 1)
                buffedSkill += 5;
            if (cf.GetCharProperty((int)Augmentations.MasterFiveFoldPath) == 1)
                buffedSkill += 10;

            return buffedSkill;
        }

        internal static int GetSpellDuration(int spellId) {
            return (int)SpellTable.GetById(spellId).Duration;
        }
    }
}
