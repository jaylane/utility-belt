using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using Decal.Adapter;

namespace UtilityBelt.Lib.Salvage {
    class TinkerCalc {
        public TinkerCalc() {
        }

        double successChance;

        public List<float> tinkDifficulty = new List<float>()
        {
            // attempt #
            1.0f,   // 1
            1.1f,   // 2
            1.3f,   // 3
            1.6f,   // 4
            2.0f,   // 5
            2.5f,   // 6
            3.0f,   // 7
            3.5f,   // 8
            4.0f,   // 9
            4.5f    // 10
        };

        public double GetSkillChance(int skill, int difficulty, float factor = 0.03f) {
            var chance = 1.0 - (1.0 / (1.0 + Math.Exp(factor * (skill - difficulty))));

            return Math.Min(1.0, Math.Max(0.0, chance));
        }

        public double DoCalc(int salvageID, WorldObject targetItem, int tinkeredCount) {
            try {
                var targetSalvage = CoreManager.Current.WorldFilter[salvageID];
                TinkerType tinkerType = new TinkerType();
                var salvageMod = TinkerType.GetMaterialMod(targetSalvage.Values(LongValueKey.Material));
                var salvageWorkmanship = targetSalvage.Values(DoubleValueKey.SalvageWorkmanship);
                var itemWorkmanship = targetItem.Values(LongValueKey.Workmanship);
                var attemptMod = tinkDifficulty[tinkeredCount];
                int tinkerSkill = TinkerType.GetTinkerType(targetSalvage.Values(LongValueKey.Material));
                int skill = tinkerType.GetRequiredTinkSkill(tinkerSkill);
            
                var workmanshipMod = 1.0f;
                if (salvageWorkmanship >= itemWorkmanship) {
                    workmanshipMod = 2.0f;
                }
                var difficulty = (int)Math.Floor(((salvageMod * 5.0f) + (itemWorkmanship * salvageMod * 2.0f) - (salvageWorkmanship * workmanshipMod * salvageMod / 5.0f)) * attemptMod);
                successChance = GetSkillChance(skill, difficulty);

                if(TinkerType.SalvageType(targetSalvage.Values(LongValueKey.Material)) == 2){
                    successChance /= 3.0f;
                    if (CoreManager.Current.CharacterFilter.GetCharProperty((int)Augmentations.CharmedSmith) == 1) {
                        successChance += 0.05f;
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
            return successChance;
        }
    }
    class FakeItem {
        public int tinkeredCount;
        public int id;
        public string name;
        public double successPercent;
        public double workmanship;
    }
}

