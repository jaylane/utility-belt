using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Decal.Adapter;
using Decal.Adapter.Wrappers;
using System.Text.RegularExpressions;
using UtilityBelt.Lib;

namespace UtilityBelt.Tools {
    [Name("Counter")]
    [Summary("Counter is used to count items based on text or utl profiles as well as players in range.")]
    [FullDescription(@"
Counter is used to count items based on text or utl profiles as well as players in range.
    ")]
    public class Counter : ToolBase {
        string utlProfile = "";
        private object lootProfile;
        List<WorldObject> idItems = new List<WorldObject>();
        List<WorldObject> matchedWOList = new List<WorldObject>();
        private DateTime lastScanUpdate = DateTime.MinValue;
        private bool isRunning = false;
        private bool scanComplete = false;
        private Dictionary<string, int> matchedRule = new Dictionary<string, int>();
        private Dictionary<string, int> itemList = new Dictionary<string, int>();

        #region Commands
        #region /ub count
        [Summary("Count items in your inventory based on a name or profile.")]
        [Usage("/ub count {item <name> | profile <lootProfile> | player <range>} [debug] [think]")]
        [Example("/ub count Prismatic Taper", "Counts the total number of Prismatic Tapers in your inventory.")]
        [Example("/ub count recomp.utl", "Counts the number of items matching recomp.utl in your inventory, thinking to yourself when finished")]
        [CommandPattern("count", @"^ *(?<Command>(item|profile|player)) (?<Name>.*?) ?(?<Options>(debug|think|\s)*)$")]
        public void DoCount(string command, Match args) {
            itemList.Clear();

            if (args.Groups["Command"].Value.ToLower() == "item") {
                string item = args.Groups["Name"].Value;

                Regex searchRegex = new Regex(item, RegexOptions.IgnoreCase);
                LogDebug("Regex String: " + searchRegex);
                int stackCount = 0;
                int totalCount = 0;
                using (var inv = CoreManager.Current.WorldFilter.GetInventory()) {
                    foreach (WorldObject wo in inv) {
                        string itemName = Util.GetObjectName(wo.Id);
                        if (searchRegex.IsMatch(itemName)) {
                            LogDebug("Matched Item: " + itemName);
                            stackCount = wo.Values(LongValueKey.StackCount, 1);
                            if (!itemList.ContainsKey(itemName)) {
                                itemList[itemName] = stackCount;
                                totalCount += stackCount;
                                LogDebug("Count In Progress: " + itemName + " - " + stackCount);
                            }
                            else if (itemList[itemName] > 0) {
                                itemList[itemName] += stackCount;
                                totalCount += stackCount;
                                LogDebug("Count In Progress: " + itemName + " - " + stackCount);
                            }
                            else {
                                continue;
                            }
                        }
                        else {
                            //Util.WriteToChat("no match for " + item);
                        }
                    }
                }

                if (itemList.Count == 0) {
                    ChatThink("Item Count: " + item + " - 0");
                }
                else {
                    foreach (KeyValuePair<string, int> entry in itemList) {
                        ChatThink("Item Count: " + entry.Key + " - " + entry.Value.ToString());
                    }
                }

                ChatThink("Total Item Count: " + totalCount.ToString());
            }
            else if (args.Groups["Command"].Value.ToLower() == "profile") {
                utlProfile = args.Groups["Name"].Value.Trim();

                Logger.WriteToChat(utlProfile);

                var pluginPath = Path.Combine(Util.GetPluginDirectory(), @"itemgiver");
                var profilePath = Path.Combine(pluginPath, utlProfile);

                if (!File.Exists(profilePath)) {
                    WriteToChat("utl path: " + profilePath);
                    WriteToChat("Profile does not exist: " + utlProfile.ToString());
                    return;
                }

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
                ((VTClassic.LootCore)lootProfile).LoadProfile(profilePath, false);
                isRunning = true;
                lastScanUpdate = DateTime.UtcNow;
                matchedWOList = CountItems();
            }
            else if (args.Groups["Command"].Value.ToLower() == "player") {
                int playerCount = 0;
                string rangeString = args.Groups["Name"].Value;

                if (!Int32.TryParse(rangeString, out int rangeInt)) {
                    LogError("bad player count range: " + rangeString);
                    return;
                }

                using (var landscape = CoreManager.Current.WorldFilter.GetLandscape()) {
                    foreach (WorldObject wo in landscape) {
                        if (wo.Type == 1 && (CoreManager.Current.WorldFilter.Distance(CoreManager.Current.CharacterFilter.Id, wo.Id) * 240) < rangeInt) {
                            LogDebug("object : " + wo.Name + " id: " + wo.Id + " type: " + wo.Type);
                            playerCount++;
                        }
                    }
                }
                ChatThink("Player Count: " + playerCount.ToString());
            }

        }
        #endregion
        #endregion

        public Counter(UtilityBeltPlugin ub, string name) : base(ub, name) {
            UB.Core.RenderFrame += Core_RenderFrame;
        }

        public void Core_RenderFrame(object sender, EventArgs e) {
            try {
                if (!isRunning)
                    return;

                if (DateTime.UtcNow - lastScanUpdate > TimeSpan.FromSeconds(10)) {
                    lastScanUpdate = DateTime.UtcNow;
                    matchedWOList = CountItems();
                    WriteToChat("Items remaining to ID: " + idItems.Count());
                }
                if (idItems.Count == 0) {
                    WriteToChat("Finished IDing Items");
                    scanComplete = true;
                }
                if (scanComplete && idItems.Count == 0) {
                    var totalCount = 0;
                    foreach (var item in matchedWOList) {
                        var stackCount = item.Values(LongValueKey.StackCount, 1);
                        if (!itemList.ContainsKey(item.Name)) {
                            itemList[item.Name] = stackCount;
                            totalCount += stackCount;
                        }
                        else if (itemList[item.Name] > 0) {
                            itemList[item.Name] += stackCount;
                            totalCount += stackCount;
                        }
                        else {
                            continue;
                        }
                    }

                    if (itemList.Count == 0) {
                        ChatThink("Item Count: " + utlProfile + " - 0");
                    }
                    else {
                        foreach (KeyValuePair<string, int> entry in itemList) {
                            ChatThink("Item Count: " + entry.Key + " - " + entry.Value.ToString());
                        }
                    }

                    ChatThink("Total Item Count: " + totalCount.ToString());

                    matchedWOList.Clear();
                    matchedRule.Clear();
                    isRunning = false;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private List<WorldObject> CountItems() {
            try {
                using (var inv = CoreManager.Current.WorldFilter.GetInventory()) {
                    foreach (WorldObject item in inv) {
                        // If the item is equipped or wielded, don't process it.
                        if (item.Values(LongValueKey.EquippedSlots, 0) > 0 || item.Values(LongValueKey.Slot, -1) == -1)
                            continue;
                        uTank2.LootPlugins.GameItemInfo itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithID(item.Id);

                        if (itemInfo == null) {
                            // This happens all the time for aetheria that has been converted
                            continue;
                        }

                        if (!((VTClassic.LootCore)lootProfile).DoesPotentialItemNeedID(itemInfo) && idItems.Contains(item)) {
                            idItems.Remove(item);
                        }

                        if (((VTClassic.LootCore)lootProfile).DoesPotentialItemNeedID(itemInfo)) {
                            if (UB.Assessor.Queue(item.Id) && !idItems.Contains(item)) {
                                idItems.Add(item);
                            }
                            continue;
                        }

                        if (matchedWOList.Contains(item)) {
                            continue;
                        }

                        uTank2.LootPlugins.LootAction result = ((VTClassic.LootCore)lootProfile).GetLootDecision(itemInfo);
                        if (!result.IsKeep && !result.IsKeepUpTo) {
                            continue;
                        }

                        //Util.WriteToChat("matched keep up to...");
                        if (!matchedRule.ContainsKey(result.RuleName)) {
                            matchedRule[result.RuleName] = 1;
                            //Util.WriteToChat("Rule: " + result.RuleName + " matched " + result.Data1 + " times");
                        }
                        else if (matchedRule[result.RuleName] > 0) {
                            matchedRule[result.RuleName]++;
                            //Util.WriteToChat("Rule: " + result.RuleName + " Count: " + matchedRule[result.RuleName]);
                        }
                        else {
                            continue;
                        }
                        matchedWOList.Add(item);
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
            return matchedWOList;
        }

        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    UB.Core.RenderFrame -= Core_RenderFrame;
                    base.Dispose(disposing);
                }
                disposedValue = true;
            }
        }
    }
}
