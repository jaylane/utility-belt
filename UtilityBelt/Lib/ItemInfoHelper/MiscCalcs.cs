using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;

namespace UtilityBelt.Lib.ItemInfoHelper {
    class MiscCalcs
    {
        #region maxproperty
        public enum WeaponProperty { MaxDmg = 0x0001, MaxVar = 0x0002, MaxDmgMod = 0x0004, MaxElementalDmgBonus = 0x0008, MaxElementalDmgVsMonsters = 0x0016 }

        public static double GetMaxProperty(WorldObject worldObject, WeaponProperty weaponProperty) {
            double maxProp = 0;
            DataTable weaponMods = BestValuesDatatable.GetTable();
            double multiStrike = 0;
            if (worldObject.Values((LongValueKey)47, 0) == 160 || worldObject.Values((LongValueKey)47, 0) == 166 || worldObject.Values((LongValueKey)47, 0) == 486 ||
                (worldObject.Values((LongValueKey)47, 0) == 4 && worldObject.Values((LongValueKey)353) == 11)) {
                multiStrike = 1;
            }
            double skill = 0;
            if (worldObject.Values(LongValueKey.EquipSkill, 0) != 0)
                skill = worldObject.Values(LongValueKey.EquipSkill);
            else skill = worldObject.Values(LongValueKey.WieldReqAttribute);

            DataRow[] result = weaponMods.Select("Skill = " + skill + " AND Mastery = " + worldObject.Values((LongValueKey)353, 0)
                + " AND WieldReq = " + worldObject.Values(LongValueKey.WieldReqValue, 0) + " AND MultiStrike = " + multiStrike);
            if (result.Length > 0)
                maxProp = (double)result[0][$"{weaponProperty}"];
            return maxProp;
        }
        #endregion

        #region petdps
        private static Dictionary<int, float> petDPS = new Dictionary<int, float>{
            { 50, 50 },
            { 80, 80 },
            { 100, 100 },
            { 125, 125 },
            { 150, 150 },
            { 180, 225 },
            { 200, 750 },
        };

        public static double GetSummonDamage(WorldObject wo) {

            int level = 200;
            string match = Regex.Match(wo.Name, @".*\((?<level>\d+)\)").Groups["level"].ToString();
            if (!string.IsNullOrEmpty(match)) _ = int.TryParse(match, out level);

            float baseDPS = 0;
            if (petDPS.ContainsKey(level)) {
                baseDPS = petDPS[level];
            }

            float dmgMod = wo.Values((LongValueKey)370, 0);
            Logger.WriteToChat("dmgMod: " + dmgMod.ToString());
            float critMod = wo.Values((LongValueKey)372, 0);
            Logger.WriteToChat("critMod: " + critMod.ToString());
            float critDmgMod = wo.Values((LongValueKey)374, 0);
            Logger.WriteToChat("critDmgMod: " + critDmgMod.ToString());

            float avgD = baseDPS * (1 + dmgMod / 100);
            Logger.WriteToChat("1) Pet dmg: " + avgD);

            float avgCritDmgFreq = (float).1 + critMod / 100;
            Logger.WriteToChat("2) avgCritDmgFreq: " + avgCritDmgFreq);

            float avgCD = avgD * 2 * (1 + critDmgMod / 100);
            Logger.WriteToChat("3) avgCD: " + avgCD);

            float avgDmgFreq = 1 - avgCritDmgFreq;
            Logger.WriteToChat("avgDmgFreq: " + avgDmgFreq);


            float weightedDPS = avgD * avgDmgFreq + avgCD * avgCritDmgFreq;

            return weightedDPS;

        }
        #endregion
    }
}
