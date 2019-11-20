using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using VirindiViewService.Controls;

namespace UtilityBelt.Tools
{
    public class AutoTrade : IDisposable
    {
        private bool disposed = false;
        private object lootProfile = null;
        private bool waitingForIds = false;
        private DateTime lastIdSpam = DateTime.MinValue;
        private readonly List<int> itemsToId = new List<int>();
        private int traderId = 0;
        private string traderName = null;
        private bool running = false;
        private bool doAccept = false;
        private Dictionary<string, int> keepUpToCounts = new Dictionary<string, int>();
        private readonly List<int> pendingAddItems = new List<int>();
        private readonly List<int> addedItems = new List<int>();
        private int lastIdCount = 0;
        private DateTime bailTimer = DateTime.MinValue;

        public AutoTrade()
        {
            try
            {
                Directory.CreateDirectory(Path.Combine(Util.GetPluginDirectory(), "autotrade"));
                Directory.CreateDirectory(Path.Combine(Util.GetCharacterDirectory(), "autotrade"));
                Directory.CreateDirectory(Path.Combine(Util.GetServerDirectory(), "autotrade"));

                Globals.Core.WorldFilter.EnterTrade += WorldFilter_EnterTrade;
                Globals.Core.WorldFilter.EndTrade += WorldFilter_EndTrade;
                Globals.Core.WorldFilter.AddTradeItem += WorldFilter_AddTradeItem;
                Globals.Core.WorldFilter.FailToAddTradeItem += WorldFilter_FailToAddTradeItem;
                Globals.Core.WorldFilter.AcceptTrade += WorldFilter_AcceptTrade;
                Globals.Core.CommandLineText += Core_CommandLineText;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void WorldFilter_AcceptTrade(object sender, AcceptTradeEventArgs e)
        {
            if (e.TargetId == Globals.Core.CharacterFilter.Id)
                return;

            var target = Globals.Core.WorldFilter[e.TargetId];

            if (target == null)
                return;

            Logger.Debug($"Checking {target.Name} against {Globals.Settings.AutoTrade.AutoAcceptChars.Count} accepted chars.");
            if (Globals.Settings.AutoTrade.AutoAcceptChars.Any(c => Regex.IsMatch(target.Name, c, RegexOptions.IgnoreCase)))
            {
                Logger.Debug("Accepting trade...");
                Globals.Core.Actions.TradeAccept();
                Util.ThinkOrWrite("Trade accepted: " + target.Name, Globals.Settings.AutoTrade.Think);
            }
        }

        private void WorldFilter_FailToAddTradeItem(object sender, FailToAddTradeItemEventArgs e)
        {
            if (running)
                pendingAddItems.Remove(e.ItemId);
        }

        private void WorldFilter_AddTradeItem(object sender, AddTradeItemEventArgs e)
        {
            if (running && e.SideId != 2)
            {
                Logger.Debug($"{Util.GetObjectName(e.ItemId)} added to trade window");
                if (pendingAddItems.Remove(e.ItemId))
                {
                    addedItems.Add(e.ItemId);
                    bailTimer = DateTime.UtcNow;
                }
            }
        }

        private void WorldFilter_EndTrade(object sender, EndTradeEventArgs e)
        {
            if (running)
                Stop();
            traderId = 0;
        }

        private void Core_CommandLineText(object sender, Decal.Adapter.ChatParserInterceptEventArgs e)
        {
            try
            {
                var match = Regex.Match(e.Text, @"^/ub autotrade(?:\s+(?<param>.*))?$");
                if (match != null && match.Success)
                {
                    var path = match.Groups["param"].Value.Trim();
                    e.Eat = true;

                    Start(traderId, path);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void WorldFilter_EnterTrade(object sender, Decal.Adapter.Wrappers.EnterTradeEventArgs e)
        {
            if (e.TradeeId == Globals.Core.CharacterFilter.Id)
                traderId = e.TraderId;
            else if (e.TraderId == Globals.Core.CharacterFilter.Id)
                traderId = e.TradeeId;

            if (traderId == 0 || Globals.Core.WorldFilter[traderId] == null)
                return;

            if (!Globals.Settings.AutoTrade.Enabled)
                return;

            traderName = Globals.Core.WorldFilter[traderId].Name;

            Start(traderId);
        }

        public void Start(int traderId, string useProfilePath = "")
        {
            running = true;
            doAccept = false;
            pendingAddItems.Clear();
            keepUpToCounts.Clear();
            addedItems.Clear();
            bailTimer = DateTime.UtcNow;
            lastIdCount = 0;

            var hasLootCore = false;
            if (lootProfile == null)
            {
                try
                {
                    lootProfile = new VTClassic.LootCore();
                    hasLootCore = true;
                }
                catch (Exception ex) { Logger.LogException(ex); }

                if (!hasLootCore)
                {
                    Util.WriteToChat("Unable to load VTClassic, something went wrong.");
                    return;
                }
            }

            var tradePartner = Globals.Core.WorldFilter[traderId];
            var profilePath = GetProfilePath(string.IsNullOrEmpty(useProfilePath) ? (tradePartner.Name + ".utl") : useProfilePath);

            if (!File.Exists(profilePath))
            {
                Util.WriteToChat("No auto trade profile exists: " + profilePath);
                Stop(false);
                return;
            }

            VTankControl.Nav_Block(1000, false); // quick block to keep vtank from truckin' off before the profile loads, but short enough to not matter if it errors out and doesn't unlock

            // Load our loot profile
            ((VTClassic.LootCore)lootProfile).LoadProfile(profilePath, false);

            itemsToId.Clear();
            var inventory = Globals.Core.WorldFilter.GetInventory();

            // filter inventory beforehand if we are only trading from the main pack
            if (Globals.Settings.AutoTrade.OnlyFromMainPack == true)
            {
                inventory.SetFilter(new ByContainerFilter(Globals.Core.CharacterFilter.Id));
            }

            itemsToId.AddRange(inventory.Select(item => item.Id));

            if (Globals.Assessor.NeedsInventoryData(itemsToId))
            {
                Globals.Assessor.RequestAll(itemsToId);
                waitingForIds = true;
                lastIdSpam = DateTime.UtcNow;
            }

            VTankControl.Item_Block(30000, false);
            VTankControl.Nav_Block(30000, Globals.Settings.Plugin.Debug);
        }

        public void Think()
        {
            if (!running)
                return;

            try
            {
                if (doAccept)
                {
                    if (pendingAddItems.Count <= 0)
                    {
                        doAccept = false;
                        if (traderId != 0 && Globals.Settings.AutoTrade.AutoAccept)
                        {
                            Logger.Debug("Accepting trade");
                            Globals.Core.Actions.TradeAccept();
                        }

                        Stop();
                    }

                    return;
                }

                if (DateTime.UtcNow - bailTimer > TimeSpan.FromSeconds(10))
                {
                    Util.WriteToChat("AutoTrade timed out - STOPPING");
                    Stop();
                }

                if (Globals.Settings.AutoTrade.Enabled && VTankControl.navBlockedUntil < DateTime.UtcNow + TimeSpan.FromSeconds(1))
                {
                    VTankControl.Item_Block(30000, Globals.Settings.Plugin.Debug);
                    VTankControl.Nav_Block(30000, Globals.Settings.Plugin.Debug);
                }

                if (waitingForIds)
                {
                    if (Globals.Assessor.NeedsInventoryData(itemsToId))
                    {
                        if (DateTime.UtcNow - lastIdSpam > TimeSpan.FromSeconds(15))
                        {
                            lastIdSpam = DateTime.UtcNow;

                            Logger.Debug(string.Format("AutoTrade waiting to id {0} items, this will take approximately {0} seconds.", Globals.Assessor.GetNeededIdCount(itemsToId)));
                        }

                        var neededIdCount = Globals.Assessor.GetNeededIdCount(itemsToId);
                        if (lastIdCount != neededIdCount)
                        {
                            lastIdCount = neededIdCount;
                            bailTimer = DateTime.UtcNow;
                        }

                        // waiting
                        return;
                    }
                    else
                        waitingForIds = false;
                }

                if (Globals.Settings.AutoTrade.TestMode)
                {
                    DoTestMode();
                    Stop();
                    return;
                }

                if (Globals.Core.Actions.BusyState != 0)
                    return;

                foreach (var item in GetTradeItems())
                {
                    // Skip if item already added to trade window
                    if (addedItems.Contains(item.Key))
                        continue;

                    Logger.Debug($"Trade Item: {Util.GetObjectName(item.Key)}");

                    if (item.Value.IsKeepUpTo)
                    {
                        if (!keepUpToCounts.ContainsKey(item.Value.RuleName))
                            keepUpToCounts.Add(item.Value.RuleName, 0);

                        var stackCount = Globals.Core.WorldFilter[item.Key].Values(LongValueKey.StackCount, 1);
                        if (item.Value.Data1 < 0) // keep this many
                        {
                            if (keepUpToCounts[item.Value.RuleName] < -1 * item.Value.Data1)
                            {
                                // add kept item to addedItems list so we don't try to add it in another pass
                                addedItems.Add(item.Key);

                                if (stackCount > -1 * item.Value.Data1 - keepUpToCounts[item.Value.RuleName])
                                {
                                    TrySplitItem(item.Key, stackCount, stackCount - (-1 * item.Value.Data1 - keepUpToCounts[item.Value.RuleName]));
                                    keepUpToCounts[item.Value.RuleName] = -1 * item.Value.Data1;
                                    return;
                                }
                                else
                                    keepUpToCounts[item.Value.RuleName] += stackCount;
                            }
                            else
                            {
                                AddToTradeWindow(item.Key);
                            }
                        }
                        else // give this many
                        {
                            if (keepUpToCounts[item.Value.RuleName] >= item.Value.Data1)
                                continue;

                            if (stackCount > item.Value.Data1 - keepUpToCounts[item.Value.RuleName])
                            {
                                TrySplitItem(item.Key, stackCount, item.Value.Data1 - keepUpToCounts[item.Value.RuleName]);
                                keepUpToCounts[item.Value.RuleName] = item.Value.Data1;
                                return;
                            }
                            else
                            {
                                keepUpToCounts[item.Value.RuleName] += stackCount;
                                AddToTradeWindow(item.Key);
                            }
                        }
                    }
                    else if (item.Value.IsKeep)
                    {
                        AddToTradeWindow(item.Key);
                    }
                }

                if (Globals.Settings.AutoTrade.AutoAccept)
                    doAccept = true;
                else
                    Stop();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Stop(bool profileLoaded = true)
        {
            try
            {
                if (profileLoaded)
                {
                    Util.ThinkOrWrite("AutoTrade finished: " + traderName, Globals.Settings.AutoTrade.Think);
                }

                if (lootProfile != null) ((VTClassic.LootCore)lootProfile).UnloadProfile();

                VTankControl.Nav_UnBlock();
                // restore cram/stack settings
                VTankControl.Item_UnBlock();
            }
            catch (Exception ex) { Logger.LogException(ex); }
            finally
            {
                running = false;
                doAccept = false;
                keepUpToCounts.Clear();
                pendingAddItems.Clear();
                addedItems.Clear();
            }
        }

        private void DoTestMode()
        {
            try
            {
                Util.WriteToChat("Trade Items:");

                foreach (var item in  GetTradeItems())
                {
                    Util.WriteToChat($"  {Util.GetObjectName(item.Key)} - {item.Value.RuleName}");
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private IEnumerable<KeyValuePair<int, uTank2.LootPlugins.LootAction>> GetTradeItems()
        {
            foreach (var item in itemsToId.OrderBy(i => Globals.Core.WorldFilter[i].Values(LongValueKey.StackCount)))
            {
                if (!ItemIsSafeToGetRidOf(item))
                    continue;

                var itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithID(item);
                if (itemInfo == null)
                    continue;

                var result = ((VTClassic.LootCore)lootProfile).GetLootDecision(itemInfo);

                if (result.IsKeep || result.IsKeepUpTo)
                    yield return new KeyValuePair<int, uTank2.LootPlugins.LootAction>(item, result);
            }
        }

        private bool ItemIsSafeToGetRidOf(int item)
        {
            var wo = Globals.Core.WorldFilter[item];
            if (wo == null)
                return false;
            if (wo.Values(LongValueKey.Attuned, 0) > 0)
                return false;

            return Util.IsItemSafeToGetRidOf(item);
        }

        private void AddToTradeWindow(int itemId)
        {
            Logger.Debug($"Adding to trade window: {Util.GetObjectName(itemId)}");
            pendingAddItems.Add(itemId);
            Globals.Core.Actions.TradeAdd(itemId);
        }

        private void TrySplitItem(int item, int stackCount, int splitCount)
        {
            EventHandler<CreateObjectEventArgs> splitHandler = null;
            splitHandler = (sender, e) =>
            {
                if (e.New.Container == Globals.Core.CharacterFilter.Id &&
                    e.New.Name == Globals.Core.WorldFilter[item].Name &&
                    e.New.Type == Globals.Core.WorldFilter[item].Type &&
                    e.New.Values(LongValueKey.StackCount, 1) == splitCount)
                {
                    Logger.Debug($"Adding {splitCount} of {Util.GetObjectName(e.New.Id)} to trade window");
                    pendingAddItems.Add(e.New.Id);
                    Globals.Core.Actions.TradeAdd(e.New.Id);
                    Globals.Core.WorldFilter.CreateObject -= splitHandler;
                }
            };

            Globals.Core.Actions.SelectItem(item);
            Globals.Core.Actions.SelectedStackCount = splitCount;
            Globals.Core.WorldFilter.CreateObject += splitHandler;
            Globals.Core.Actions.MoveItem(item, Globals.Core.CharacterFilter.Id, 0, false);

            Logger.Debug($"AutoTrade Splitting {Util.GetObjectName(item)}. old: {stackCount} new: {splitCount}");
        }

        private string GetProfilePath(string profileName)
        {
            var charPath = Path.Combine(Util.GetCharacterDirectory(), "autotrade");
            var mainPath = Path.Combine(Util.GetPluginDirectory(), "autotrade");
            var serverPath = Path.Combine(Util.GetServerDirectory(), "autotrade");

            if (File.Exists(Path.Combine(charPath, profileName)))
            {
                return Path.Combine(charPath, profileName);
            }
            else if (File.Exists(Path.Combine(serverPath, profileName)))
            {
                return Path.Combine(serverPath, profileName);
            }
            else if (File.Exists(Path.Combine(mainPath, profileName)))
            {
                return Path.Combine(mainPath, profileName);
            }
            else if (File.Exists(Path.Combine(charPath, "default.utl")))
            {
                return Path.Combine(charPath, "default.utl");
            }
            else if (File.Exists(Path.Combine(serverPath, "default.utl")))
            {
                return Path.Combine(serverPath, "default.utl");
            }
            else if (File.Exists(Path.Combine(mainPath, "default.utl")))
            {
                return Path.Combine(mainPath, "default.utl");
            }

            return Path.Combine(mainPath, profileName);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    Globals.Core.WorldFilter.EnterTrade -= WorldFilter_EnterTrade;
                    Globals.Core.WorldFilter.EndTrade -= WorldFilter_EndTrade;
                    Globals.Core.WorldFilter.AddTradeItem -= WorldFilter_AddTradeItem;
                    Globals.Core.WorldFilter.FailToAddTradeItem -= WorldFilter_FailToAddTradeItem;
                    Globals.Core.WorldFilter.AcceptTrade -= WorldFilter_AcceptTrade;
                    Globals.Core.CommandLineText -= Core_CommandLineText;
                }
                disposed = true;
            }
        }

    }
}
