using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Decal.Adapter;
using Decal.Adapter.Wrappers;
using UBHelper;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Settings;
using UtilityBelt.Lib.VendorCache;
using UtilityBelt.Views;
using VirindiViewService.Controls;
using UBLoader.Lib.Settings;
using Hellosam.Net.Collections;
using UBLoader.Lib;

namespace UtilityBelt.Tools {
    [Name("AutoXp")]
    [Summary("Automatically spends experience based on a policy.")]
    [FullDescription(@"
### Overview

This plugin will attempt to spend experience based on a weighted importance of a skill set in the Policy.
The higher the number the more experience you'd be willing to spend on the skill (e.g., War - 10, Endurance - 1 would level War next if it cost less than 10x Endurance).

The plugin can spend either batches of experience up to the max chunk size or just enough to get to the next level.
If the plugin is already spending experience it will stop if you use a command to spend experience.

You can tell the plugin to stop a specified number of levels before the max, but it will always halt at least 1 before the max.
    ")]
    public class AutoXp : ToolBase {
        private DateTime lastXpSpend = DateTime.MinValue;
        private bool disposed;
        private bool isRunning = false;
        private List<KeyValuePair<XpTarget, int>> flatPlan { get; set; }
        private int planIndex { get; set; } = 0;

        #region Config
//        [Summary("Spend experience when it is gained")]
//        public readonly Setting<bool> EnableXpChange = new Setting<bool>(true);

        [Summary("Levels before max level to stop attempting to level a target.")]
        public readonly Setting<int> StopBeforeMax = new Setting<int>(10);

        [Summary("Time between attempts to spend XP (in milliseconds)")]
        public readonly Setting<int> TriesTime = new Setting<int>(300);

        [Summary("Max amount of experience to attempt to spend at once with batch spending.")]
        public readonly Setting<int> MaxXpChunk = new Setting<int>(1000000000);

        [Summary("Weighted policy that determines ratios of xp spent on targets.")]
        public readonly Setting<ObservableDictionary<XpTarget, double>> Policy = new Setting<ObservableDictionary<XpTarget, double>>(
            new ObservableDictionary<XpTarget, double>() {
                    //Attributes - Default to neutral
                    { XpTarget.Strength, 1},
                    { XpTarget.Endurance, 1},
                    { XpTarget.Coordination, 1},
                    { XpTarget.Quickness, 1},
                    { XpTarget.Focus, 1},
                    { XpTarget.Self, 1},
                    //Vitals
                    { XpTarget.Health, 1.4},
                    { XpTarget.Stamina, .1},
                    { XpTarget.Mana, .1},
                    //Skills -- Default to low secondary, high primary
                    { XpTarget.Alchemy, 0},
                    { XpTarget.ArcaneLore, .1},
                    { XpTarget.ArmorTinkering, 0},
                    { XpTarget.AssessCreature, 0},
                    { XpTarget.AssessPerson, 0},
                    { XpTarget.Cooking, 0},
                    { XpTarget.CreatureEnchantment, .2},
                    { XpTarget.Deception, .1},
                    { XpTarget.DirtyFighting, .1},
                    { XpTarget.DualWield, .1},
                    { XpTarget.FinesseWeapons, 10},
                    { XpTarget.Fletching, .1},
                    { XpTarget.Healing, .1},
                    { XpTarget.HeavyWeapons, 10},
                    { XpTarget.ItemEnchantment, .2},
                    { XpTarget.ItemTinkering, 0},
                    { XpTarget.Jump, .02},
                    { XpTarget.Leadership, .1},
                    { XpTarget.LifeMagic, 1},
                    { XpTarget.LightWeapons, 10},
                    { XpTarget.Lockpick, 0},
                    { XpTarget.Loyalty, .1},
                    { XpTarget.MagicDefense, .1},
                    { XpTarget.MagicItemTinkering, 0},
                    { XpTarget.ManaConversion, .1},
                    { XpTarget.MeleeDefense, 5},
                    { XpTarget.MissileDefense, 5},
                    { XpTarget.MissileWeapons, 10},
                    { XpTarget.Recklessness, .1},
                    { XpTarget.Run, .1},
                    { XpTarget.Salvaging, .1},
                    { XpTarget.Shield, 0},
                    { XpTarget.SneakAttack, .1},
                    { XpTarget.Summoning, 1},
                    { XpTarget.TwoHandedCombat, 10},
                    { XpTarget.VoidMagic, 10},
                    { XpTarget.WarMagic, 10},
                    { XpTarget.WeaponTinkering, 0}
                });
        #endregion

        #region Commands
        #region /ub xp <level|slow|test>
        [Summary("Automatically spend experience according to a policy.")]
        [Usage("/ub xp [level|test|slow]")]
        [Example("/ub xp", "Displays the weights of the current xp policy.")]
        [Example("/ub xp test", "Displays the way current experience would be spent with the current policy")]
        [Example("/ub xp level", "Begins (or halts) quickly spending experience with up to MaxXpChunk.")]
        [Example("/ub xp slow", "Begins (or halts) spending experience one level at a time.")]
        [CommandPattern("xp", @"^ *(?<Verb>.*)$")]
        public void DoAutoXp(string _, Match args) {
            switch (args.Groups["Verb"].Value) {
                case "level":
                    SpendExperience(true);
                    break;
                case "slow":
                    SpendExperience(false);
                    break;
                case "test":
                    PrintExperiencePlan();
                    break;
                default:
                    PrintPolicy();
                    break;
            }
        }
        #endregion
        #endregion

        public AutoXp(UtilityBeltPlugin ub, string name) : base(ub, name) {
        }

        private void Core_RenderFrame_SpendExperience(object sender, EventArgs e) {
            //Check if plan finished
            if (flatPlan.Count <= planIndex) {
                Halt();
            }
            //Check if enough time has passed
            else if ((DateTime.Now - lastXpSpend).TotalMilliseconds < TriesTime) {
                //Logger.WriteToChat($"{(DateTime.Now - lastXpSpend).TotalMilliseconds}/{TriesTime} ms passed");
            }
            //Otherwise level the next thing
            else {
                lastXpSpend = DateTime.Now;
                var target = flatPlan[planIndex].Key;
                var exp = flatPlan[planIndex].Value;
                //Logger.WriteToChat($"Adding {exp} to {target}");
                target.AddExp(exp);
                planIndex++;
            }
        }

        private void Halt() {
            Logger.WriteToChat($"Finished leveling {planIndex} targets.");
            isRunning = false;
            flatPlan = null;
            UB.Core.RenderFrame -= Core_RenderFrame_SpendExperience;
        }

        /// <summary>
        /// Creates a plan to spend all unassigned experience
        /// </summary>
        /// <returns></returns>
        private Dictionary<XpTarget, List<int>> GetPlan() {
            return GetPlan(CoreManager.Current.CharacterFilter.UnassignedXP);
        }

        //Plan to spend a set amount of XP, up to some max steps in the plan
        private Dictionary<XpTarget, List<int>> GetPlan(long expToSpend, int maxSteps = 9999) {
            var plan = new Dictionary<XpTarget, List<int>>();
            var weightedCosts = new Dictionary<XpTarget, double>();

            //Find what exp targets are candidates to be leveled
            foreach (var key in Policy.Value.Keys) {
                //Skip invalid weights
                if (!Policy.Value.TryGetValue(key, out double value) || value <= 0)
                    continue;

                try {
                    var cost = key.CostToLevel(StopBeforeMax) ?? -1;
                    //Logger.WriteToChat($"{t} - {cost}");
                    //Continue if no known cost to level
                    if (cost <= 0) {
                        continue;
                    }

                    //Otherwise consider it for spending exp on
                    plan.Add(key, new List<int>());

                    //Figure out initial weighted cost of exp target
                    weightedCosts.Add(key, cost / Policy.Value[key]);
                }
                catch (Exception e) {
                    Logger.LogException(e);
                }
            }

            //Break if nothing left to level
            if (plan.Count == 0)
                return plan;

            for (var i = 0; i < maxSteps; i++) {
                //Get the most efficient thing to spend exp on as determined by weighted cost
                var nextTarget = weightedCosts.OrderBy(t => t.Value).First().Key;

                //Find cost of leveling that skill after the steps previously taken in the plan
                var timesLeveled = plan[nextTarget].Count;
                var cost = nextTarget.CostToLevel(StopBeforeMax, timesLeveled) ?? -1;

                //Halt if there is insufficient exp or no more levels
                if (expToSpend < cost || cost == -1) {
                    break;
                }

                //Add to plan
                plan[nextTarget].Add(cost);
                //Simulate use of that exp
                expToSpend -= cost;

                //Update weighted cost
                var nextCost = nextTarget.CostToLevel(StopBeforeMax, timesLeveled + 1) ?? -1;
                //TODO: Improve logic here.  If there's no next level, set weight cost to max value
                if (nextCost == -1) {
                    weightedCosts[nextTarget] = double.PositiveInfinity;
                }
                else {
                    var newWeightedCost = nextCost / Policy.Value[nextTarget];
                    weightedCosts[nextTarget] = newWeightedCost;
                }
            }

            return plan;
        }

        /// <summary>
        /// Creates and initiates a plan to spend experience, or halts an already running plan.
        /// </summary>
        /// <param name="batchLevels">Spends up to MAX_XP_CHUNK to level multiple times at once</param>
        private void SpendExperience(bool batchLevels = false) {
            SpendExperience(CoreManager.Current.CharacterFilter.UnassignedXP, batchLevels);
        }
        private void SpendExperience(long expToSpend, bool batchLevels = false) {
            //Halt if already spending experience?  
            if (isRunning) {
                Logger.WriteToChat("Stopping AutoXp.");
                Halt();
                return;
            }

            //Get plan for leveling
            flatPlan = new List<KeyValuePair<XpTarget, int>>();
            foreach (var steps in GetPlan(expToSpend)) {
                if (batchLevels) {
                    long totalXp = 0;
                    foreach (var step in steps.Value) {
                        totalXp += step;
                    }
                    while (totalXp > MaxXpChunk) {
                        flatPlan.Add(new KeyValuePair<XpTarget, int>(steps.Key, MaxXpChunk));
                        totalXp -= MaxXpChunk;
                    }
                    if (totalXp > 0) {
                        flatPlan.Add(new KeyValuePair<XpTarget, int>(steps.Key, (int)totalXp));
                    }
                }
                else {
                    for (var i = 0; i < steps.Value.Count; i++) {
                        flatPlan.Add(new KeyValuePair<XpTarget, int>(steps.Key, steps.Value[i]));
                    }
                }
            }

            //Check for nothing left to level
            if (flatPlan.Count == 0) {
                return;
            }

            //Sort by cost?
            flatPlan = flatPlan.OrderBy(t => t.Value).ToList();

            //Start at beginning of plan
            planIndex = 0;

            Logger.WriteToChat($"Spending on a plan consisting of {flatPlan.Count} steps with {expToSpend} available exp.");

            //Start leveling
            isRunning = true;
            UB.Core.RenderFrame += Core_RenderFrame_SpendExperience;
        }

        /// <summary>
        /// Prints out what would be leveled using all unassigned xp
        /// </summary>
        private void PrintExperiencePlan() {
            PrintExperiencePlan(CoreManager.Current.CharacterFilter.UnassignedXP);
        }
        private void PrintExperiencePlan(long expToSpend) {
            var plan = GetPlan(expToSpend);

            Logger.WriteToChat($"Experience plan for {expToSpend} exp:");
            foreach (var t in plan) {
                var steps = t.Value.Count;
                var description = new StringBuilder($"{t.Key.ToString()} ({steps}): ");

                for (var i = 0; i < t.Value.Count; i++) {
                    description.Append($"{t.Value[i]}\t");
                }

                Logger.WriteToChat(description.ToString());
            }
        }
        
        private void PrintPolicy() {
            Logger.WriteToChat("Current experience policy weights:");
            foreach (var key in Policy.Value.Keys) {
                //Ignore unused in the policy
                if (Policy.Value.TryGetValue(key, out double value) && value > 0)
                    Logger.WriteToChat(key + ": " + value);
            }
        }

        private void CharacterFilter_LoginComplete(object sender, EventArgs e) {
            try {
                //Todo: login checks if experience spent when gained
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public override void Init() {
            base.Init();

            //if (UBHelper.Core.GameState != UBHelper.GameState.In_Game)
            //    UB.Core.CharacterFilter.LoginComplete += CharacterFilter_LoginComplete;
        }

        protected override void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {

                    base.Dispose(disposing);
                }
                disposed = true;
            }
        }
    }

    
}
