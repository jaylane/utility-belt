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
        Dictionary<double, int> DifficultyTable = new Dictionary<double, int>();

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

        public void BuildDifficultyTable() {

            TinkerType tinkerType = new TinkerType();
            int tinkerSkill = TinkerType.GetTinkerType(67);
            int skill = tinkerType.GetRequiredTinkSkill(tinkerSkill);

            for (int i = -500; i <= 500; i++) {
                int delta = i;
                double chance = 1 - (1 / (1 + Math.Exp(.03 * i)));
                DifficultyTable.Add(chance, delta);
            }
        }

        public int GetDifficulty(double chance, int material) {
            TinkerType tinkerType = new TinkerType();
            int tinkerSkill = TinkerType.GetTinkerType(material);
            int skill = tinkerType.GetRequiredTinkSkill(tinkerSkill);
            if (DifficultyTable.Count <= 0) {
                BuildDifficultyTable();
            }
            int delta = 0;
            int diff = 0;
            for (int i = 0; i <= DifficultyTable.Count() - 1; i++) {
                //Logger.WriteToChat(i.ToString());
                double currentChance = DifficultyTable.Keys.ElementAt(i);
                //Logger.WriteToChat(chance.ToString());
                //Logger.WriteToChat(currentChance.ToString());
                if (currentChance > chance) {
                    delta = DifficultyTable.Values.ElementAt(i);
                    diff = skill - delta;
                    //Logger.WriteToChat("difficulty: " + diff.ToString());
                    break;
                }
            }
            return diff;
        }

        public void GetRequiredSalvage(double difficulty, int salvageMaterial, WorldObject item, float attemptMod) {
            double itemWorkmanship = item.Values(DoubleValueKey.SalvageWorkmanship);
            var salvageMod = TinkerType.GetMaterialMod(salvageMaterial);


            // ((s * 5) + (w * s * 2) - (x * 1 * s / 5)) * a = d, solve for x
            // x = -(5 d)/(a s) + 10 w + 25 and a s!=0
            var toolWorkmanship_lower = 5.0f * (-difficulty / (attemptMod * salvageMod)) + 10.0f * itemWorkmanship + 25.0f;

            // workmanshipMod == 2:

            // ((s * 5) + (w * s * 2) - (x * 2 * s / 5)) * a = d, solve for x
            // x = 5/2 (-d/(a s) + 2 w + 5) and a s!=0
            var toolWorkmanship_higher = 2.5f * (-difficulty / (attemptMod * salvageMod) + 2.0f * itemWorkmanship + 5.0f);

            if (toolWorkmanship_higher >= itemWorkmanship) {
                Logger.WriteToChat(Util.FileService.MaterialTable.GetById(salvageMaterial).Name +  " requires wk: " + toolWorkmanship_higher);
            }
            else {
                Logger.WriteToChat(Util.FileService.MaterialTable.GetById(salvageMaterial).Name + " requires wk: " + toolWorkmanship_lower);

            }
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
                //Logger.WriteToChat("difficulty: " + difficulty.ToString());
                successChance = GetSkillChance(skill, difficulty);
                //Logger.WriteToChat("successChance: " + successChance);
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
}

