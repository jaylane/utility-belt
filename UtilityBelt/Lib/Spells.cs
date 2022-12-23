using AcClient;
using ACE.DatLoader.Entity;
using ACE.DatLoader.FileTypes;
using Decal.Adapter.Wrappers;
using Decal.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using UtilityBelt.Common.Enums;
using Spell = Decal.Filters.Spell;
using SpellComponentBase = ACE.DatLoader.Entity.SpellComponentBase;
using SpellTable = Decal.Filters.SpellTable;

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
        private static ComponentTable ComponentTable {
            get {
                if (fs != null)
                    return fs.ComponentTable;
                fs = UtilityBeltPlugin.Instance.Core.Filter<FileService>();
                return fs.ComponentTable;
            }
        }
        private static ACE.DatLoader.FileTypes.SpellTable DatSpellTable {
            get => UtilityBeltPlugin.Instance.PortalDat.SpellTable;
        }
        private static ACE.DatLoader.FileTypes.SpellComponentsTable DatComponentTable {
            get => UtilityBeltPlugin.Instance.PortalDat.SpellComponentsTable;
        }
        //Table to find IDs of a Spellbase.  Should be unique?
        private static Dictionary<SpellBase, uint> _spellToId = null;
        private static Dictionary<SpellBase, uint> IdTable {
            get {
                if (_spellToId is null)
                    _spellToId = DatSpellTable.Spells.ToDictionary(x => x.Value, x => x.Key);
                return _spellToId;
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
        public static unsafe bool IsKnown(uint spellId) => IsKnown((int)spellId);
        //Todo: Bug yonneh about exception
        //CObjectMaint.s_pcInstance->GetWeenieObject(*CPhysicsPart.player_iid)->m_pQualities->a0.IsSpellKnown(spellId) == 1;

        /// <summary>
        /// Checks if the player has the required skill to cast spellId. Difficulty is modified by the 
        /// SpellDiffExcessThreshold-Hunt vtank setting.
        /// </summary>
        /// <param name="spellId">spell id to check</param>
        /// <returns></returns>
        public static bool HasSkillHunt(int spellId) {
            var spell = GetSpell(spellId);
            var minSkillRequired = spell.Difficulty + (int)UBHelper.vTank.Instance.GetSetting("SpellDiffExcessThreshold-Hunt");
            var effectiveSkill = GetEffectiveSkillForSpell(spell);

            //Util.WriteToChat($"School: {spell.School} Buffed: {effectiveSkill} minSkillRequired: {minSkillRequired}");

            return effectiveSkill >= minSkillRequired;
        }
        public static bool HasSkillHunt(uint spellId) {
            var spell = GetSpell(spellId);
            return spell.GetEffectiveSkillForSpell() >= spell.Power + (int)UBHelper.vTank.Instance.GetSetting("SpellDiffExcessThreshold-Hunt");
        }

        /// <summary>
        /// Checks if the player has the required skill to cast spellId. Difficulty is modified by the  
        /// SpellDiffExcessThreshold-Buff vtank setting.
        /// </summary>
        /// <param name="spellId">spell id to check</param>
        /// <returns></returns>
        public static bool HasSkillBuff(int spellId) {
            var spell = GetSpell(spellId);
            var minSkillRequired = spell.Difficulty + (int)UBHelper.vTank.Instance.GetSetting("SpellDiffExcessThreshold-Buff");
            var effectiveSkill = GetEffectiveSkillForSpell(spell);

            //Util.WriteToChat($"School: {spell.School} Buffed: {effectiveSkill} minSkillRequired: {minSkillRequired}");

            return effectiveSkill >= minSkillRequired;
        }
        public static bool HasSkillBuff(uint spellId) {
            var spell = GetSpell(spellId);
            return spell.GetEffectiveSkillForSpell() >= spell.Power + (int)UBHelper.vTank.Instance.GetSetting("SpellDiffExcessThreshold-Buff");
        }

        public static bool HasSkill(uint spellId, int difficultyModifier = 0) {
            var spell = GetSpell(spellId);
            return spell.Power + difficultyModifier < spell.GetEffectiveSkillForSpell();
        }

        public static Spell GetSpell(int spellId) {
            return SpellTable.GetById(spellId);
        }
        public static SpellBase GetSpell(uint spellId) => DatSpellTable.Spells.TryGetValue(spellId, out var spell) ? spell : null;

        /// <summary>
        /// Checks if player has scarabs required for a spell (currently does not check tapers)
        /// </summary>
        /// <param name="spellId">id of the spell to check</param>
        /// <returns></returns>
        public static bool HasComponents(int spellId) {
            var spell = GetSpell(spellId);
            var neededComps = new Dictionary<string, int>();
            for (var i = 0; i < spell.ComponentIDs.Length; i++) {
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
        public static bool HasComponents(uint spellId) {
            var spell = GetSpell(spellId);
            var neededComps = new Dictionary<string, int>();

            for (var i = 0; i < spell.Formula.Count; i++) {
                var id = spell.Formula[i];
                var component = GetComponent(id);


                //What's wrong with checking other comps..?
                //if (!component.Name.Contains("Scarab"))
                if (component.Type != (uint)SpellComponentsTable.Type.Scarab)
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
            var spell = GetSpell(spellId);
            return spell == null ? $"UnknownSpell:{spellId}" : spell.Name;
        }
        public static string GetName(uint spellId) => DatSpellTable.Spells.TryGetValue(spellId, out SpellBase spell)
            ? spell.Name : $"UnknownSpell:{spellId}";

        public static int GetEffectiveSkillForSpell(int spellId) {
            return GetEffectiveSkillForSpell(GetSpell(spellId));
        }
        public static int GetEffectiveSkillForSpell(uint spellId) => GetEffectiveSkillForSpell(GetSpell(spellId));

        public static int GetSpellDuration(int spellId) {
            return (int)GetSpell(spellId).Duration;
        }
        public static int GetSpellDuration(uint spellId) => DatSpellTable.Spells.TryGetValue(spellId, out var spell) ?
            (int)spell.Duration : 0;

        public static string GetComponentName(int componentId) {
            return ComponentTable.GetById(componentId).Name;
        }
        public static string GetComponentName(uint componentId) =>
            DatComponentTable.SpellComponents.TryGetValue(componentId, out var component) ? component.Name : null;

        public static Component GetComponent(int componentId) {
            return ComponentTable.GetById(componentId);
        }
        public static SpellComponentBase GetComponent(uint componentId) =>
            DatComponentTable.SpellComponents.TryGetValue(componentId, out var component) ? component : null;

        public static int GetEffectiveSkillForSpell(this Spell spell) {
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
        public static int GetEffectiveSkillForSpell(this SpellBase spell) {
            var cf = UtilityBeltPlugin.Instance.Core.CharacterFilter;
            var buffedSkill = spell.School switch {
                MagicSchool.CreatureEnchantment => cf.EffectiveSkill[CharFilterSkillType.CreatureEnchantment],
                MagicSchool.ItemEnchantment => cf.EffectiveSkill[CharFilterSkillType.ItemEnchantment],
                MagicSchool.LifeMagic => cf.EffectiveSkill[CharFilterSkillType.LifeMagic],
                MagicSchool.VoidMagic => cf.EffectiveSkill[CharFilterSkillType.VoidMagic],
                MagicSchool.WarMagic => cf.EffectiveSkill[CharFilterSkillType.WarMagic],
                _ => throw new NotImplementedException(),
            };

            if (cf.GetCharProperty(326/*Jack of All Trades*/) == 1)
                buffedSkill += 5;
            if (cf.GetCharProperty((int)Augmentations.MasterFiveFoldPath) == 1)
                buffedSkill += 10;

            return buffedSkill;
        }

        static Dictionary<uint, List<uint>> _spellGroups;
        /// <summary>
        /// Get a list of spell IDs similar to the given one sorted by level
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static List<uint> GetComparableSpells(uint id) {
            if (_spellGroups is not null && _spellGroups.ContainsKey(id))
                return _spellGroups[id];

            _spellGroups = new Dictionary<uint, List<uint>>();
            //Create spell groupings
            //Logger.WriteToChat("Creating spell groups...");

            var groups = DatSpellTable.Spells
                .OrderBy(s => GetSpell(s.Key).GetLevel())                    //Sort by level?
                .GroupBy(x => new {
                    x.Value.Category,                           //Group by category
                    MaskedFlags = x.Value.GetComparedFlags(),
                    x.Value.MetaSpellType,                      //Healing Mastery vs Heal Self
                    x.Value.NonComponentTargetType,             //Blood Drinker vs Spirit Drinker
                    x.Value.School,                             //Nether Blast same category as Flame Blast
                    //Unique group if Portal<Sending|Recall|Summon|Link> and FellowPortalSending
                    IsPortal = (x.Value.MetaSpellType.ToString().Contains("Portal") ? x.Key : 0)
                });

            foreach (var g in groups) {
                var group = g.Select(x => x.Key).ToList();
                foreach (var s in g) {
                    _spellGroups.Add(s.Key, group);
                }
            }

            #region Group Dump
            ////Testing groups
            //var watch = new System.Diagnostics.Stopwatch();
            //watch.Start();
            //var sb = new StringBuilder();
            //int gNum = 1;
            //foreach (var g in groups) {
            //    sb.AppendLine($"Group {gNum++} ({g.Count()}):");
            //    foreach (var s in g) {
            //        sb.AppendLine($"  {s.Key}\t{s.Value.Name}\t{s.Value.GetComparedFlags():X8}\t");
            //    }
            //}
            //watch.Stop();
            //sb.Insert(0, $"{watch.ElapsedMilliseconds} ms\r\n\r\n");
            //System.Windows.Forms.Clipboard.SetText(sb.ToString());
            #endregion Group Dump

            return GetComparableSpells(id);
        }

        public static bool TryGetBestCastable(uint source, out uint id, bool ignoreSkillBuff = true, bool ignoreSkillHunt = true, bool ignoreComps = true, bool ignoreKnown = true) {
            id = GetComparableSpells(source)
                .Where(s =>
                (ignoreComps || HasComponents(s)) &&
                (ignoreSkillBuff || HasSkillBuff(s)) &&
                (ignoreSkillHunt || HasSkillHunt(s)) &&
                (ignoreKnown || IsKnown(s)))
                .LastOrDefault();

            return id > 0;
        }
        public static bool TryGetBestCastable(uint source, out uint id, int difficultyModifier = 0, bool ignoreComps = true) {
            id = GetComparableSpells(source)
                .Where(s =>  IsKnown(s) && HasSkill(s, difficultyModifier) && (ignoreComps || HasComponents(s)))
                .LastOrDefault();
            return id > 0;
        }

        public static Scarab GetScarab(this Spell spell) => Enum.IsDefined(typeof(Scarab), spell.ComponentIDs[0]) ? (Scarab)spell.ComponentIDs[0] : Scarab.Unknown;
        public static int GetLevel(this Spell spell) => ScarabLevel.TryGetValue(spell.GetScarab(), out int level) ? level : 0;
        public static int GetPowerLevel(this Spell spell) => ScarabPower.TryGetValue(spell.GetScarab(), out int level) ? level : 0;

        public static uint GetId(this SpellBase spell) => IdTable.TryGetValue(spell, out var id) ? id : 0;
        public static Scarab GetScarab(this SpellBase spell) => Enum.IsDefined(typeof(Scarab), (int)spell.Formula[0]) ? (Scarab)(int)spell.Formula[0] : Scarab.Unknown;
        public static int GetLevel(this SpellBase spell) => ScarabLevel.TryGetValue(spell.GetScarab(), out int level) ? level : 0;
        public static int GetPowerLevel(this SpellBase spell) => ScarabPower.TryGetValue(spell.GetScarab(), out int level) ? level : 0;

        #region Enums and Constants
        //ACE enum as uint for use with bitmask
        public enum SpellFlags : uint {
            Resistable = 0x1, PKSensitive = 0x2, Beneficial = 0x4, SelfTargeted = 0x8,
            Reversed = 0x10, NotIndoor = 0x20, NotOutdoor = 0x40, NotResearchable = 0x80,
            Projectile = 0x100, CreatureSpell = 0x200, ExcludedFromItemDescriptions = 0x400, IgnoresManaConversion = 0x800,
            NonTrackingProjectile = 0x1000, FellowshipSpell = 0x2000, FastCast = 0x4000, IndoorLongRange = 0x8000,
            DamageOverTime = 0x10000, UNKNOWN = 0x20000
        }
        const uint BIT_MASK = (uint)~(
            SpellFlags.NotResearchable |    //Ignore fastcast/researchable for 7-8s
            SpellFlags.FastCast |
            SpellFlags.PKSensitive);        //Strength Self 1-6 diff PKSensitive than 7-8
        static uint GetComparedFlags(this SpellBase spell) => spell.Bitfield & BIT_MASK;

        //Enum matches component ID
        public enum Scarab {
            Lead = 1, Iron = 2, Copper = 3, Silver = 4, Gold = 5, Pyreal = 6, Diamond = 110, Platinum = 112, Mana = 193, Dark = 192, Unknown
        }
        //Using ACE's implementation of level and power level from SpellFormula.cs
        //OptimShi's site disagrees with things (all Diamonds are Lvl 3? http://ac.yotesfan.com/spells/spell/1839)
        private static readonly Dictionary<Scarab, int> ScarabLevel = new Dictionary<Scarab, int>()
        {
            { Scarab.Lead,     1 },
            { Scarab.Iron,     2 },
            { Scarab.Copper,   3 },
            { Scarab.Silver,   4 },
            { Scarab.Gold,     5 },
            { Scarab.Pyreal,   6 },
            { Scarab.Diamond,  6 },
            { Scarab.Platinum, 7 },
            { Scarab.Dark,     7 },
            { Scarab.Mana,     8 }
        };
        private static readonly Dictionary<Scarab, int> ScarabPower = new Dictionary<Scarab, int>()
                {
            { Scarab.Lead,     1 },
            { Scarab.Iron,     2 },
            { Scarab.Copper,   3 },
            { Scarab.Silver,   4 },
            { Scarab.Gold,     5 },
            { Scarab.Pyreal,   6 },
            { Scarab.Diamond,  7 },
            { Scarab.Platinum, 8 },
            { Scarab.Dark,     9 },
            { Scarab.Mana,    10 }
        };
        #endregion
    }
}
