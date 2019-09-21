using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Decal.Adapter;
using Decal.Adapter.Wrappers;

namespace UtilityBelt.Tools {
    class Counter : IDisposable {


        private bool disposed = false;
        string utlProfile = "";
        private object lootProfile;
        List<WorldObject> idItems = new List<WorldObject>();
        List<WorldObject> matchedWOList = new List<WorldObject>();
        private DateTime lastScanUpdate = DateTime.MinValue;
        private bool isRunning = false;
        private bool scanComplete = false;
        private Dictionary<string, int> matchedRule = new Dictionary<string, int>();

        public Counter() {
            CoreManager.Current.CommandLineText += new EventHandler<ChatParserInterceptEventArgs>(Current_CommandLineText);
        }

        public void Think() {
            try {
                if (DateTime.Now - lastScanUpdate > TimeSpan.FromSeconds(10) && isRunning) {
                    lastScanUpdate = DateTime.Now;
                    matchedWOList = CountItems();
                    Util.WriteToChat("Items remaining to ID: " + idItems.Count());
                    if (idItems.Count == 0 && isRunning) {
                        Util.WriteToChat("Finished IDing Items");
                        scanComplete = true;
                    }
                    if (scanComplete && idItems.Count == 0) {
                        foreach (KeyValuePair<string,int> entry in matchedRule) {
                            
                            Util.WriteToChat("Matched Rule: " + entry.Key + " ---- " + entry.Value);
                            scanComplete = false;
                            isRunning = false;

                        }
                    matchedWOList.Clear();
                    matchedRule.Clear();
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }


        private void Current_CommandLineText(object sender, ChatParserInterceptEventArgs e) {
            try {
                if (e.Text.StartsWith("/ub count ")) {
                    e.Eat = true;
                    if (e.Text.Contains("item ")) {
                        string item = e.Text.Replace("/ub count item", "").Trim();
                        int stackCount = 0;
                        int totalStackCount = 0;
                        foreach (WorldObject wo in CoreManager.Current.WorldFilter.GetInventory()) {
                            if (String.Compare(wo.Name, item, StringComparison.OrdinalIgnoreCase) == 0) {
                                stackCount = wo.Values(LongValueKey.StackCount, 1);
                                totalStackCount += stackCount;
                            }
                            else {
                            }
                        }
                        Util.Think("Item Count: " + item + " - " + totalStackCount.ToString());
                    }
                    else if (e.Text.Contains("profile")) {
                        utlProfile = e.Text.Replace("/ub count profile ", "").Trim();
                        e.Eat = true;

                        Util.WriteToChat(utlProfile);

                        var pluginPath = Path.Combine(Util.GetPluginDirectory(), @"itemgiver");
                        var profilePath = Path.Combine(pluginPath, utlProfile);
                        Util.WriteToChat(profilePath.ToString());

                        if (!File.Exists(profilePath)) {
                            Util.WriteToChat("utl path: " + profilePath);
                            Util.WriteToChat("Profile does not exist: " + utlProfile.ToString());
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
                                Util.WriteToChat("Unable to load VTClassic, something went wrong.");
                                return;
                            }
                        }
                        ((VTClassic.LootCore)lootProfile).LoadProfile(profilePath, false);
                        isRunning = true;
                        lastScanUpdate = DateTime.Now;
                        matchedWOList = CountItems();
                    }
                    else if (e.Text.Contains("player ")) {
                        int playerCount = 0;
                        string rangeString = e.Text.Replace("/ub count player ", "").Trim();
                        int rangeInt = 0;
                        if (Int32.TryParse(rangeString, out rangeInt)) {
                            // success parse
                        }
                        else {
                            Util.WriteToChat("bad player count range: " + rangeString);
                        }

                        foreach (WorldObject wo in CoreManager.Current.WorldFilter.GetLandscape()) {
                            if (wo.Type == 1 && (CoreManager.Current.WorldFilter.Distance(CoreManager.Current.CharacterFilter.Id, wo.Id) * 240) < rangeInt) {
                                Util.WriteToChat("object : " + wo.Name + " id: " + wo.Id + " type: " + wo.Type);
                                playerCount++;
                            }
                        }
                        Util.Think("Player Count: " + " " + playerCount.ToString());
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private List<WorldObject> CountItems() {
            try {
                foreach (WorldObject item in CoreManager.Current.WorldFilter.GetInventory()) {

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
                        CoreManager.Current.Actions.RequestId(item.Id);
                        if (!idItems.Contains(item)) {
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
            catch (Exception ex) { Logger.LogException(ex); }
        return matchedWOList;
        }


        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);

        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    CoreManager.Current.CommandLineText -= new EventHandler<ChatParserInterceptEventArgs>(Current_CommandLineText);
                }
                disposed = true;
            }
        }
    }
}
