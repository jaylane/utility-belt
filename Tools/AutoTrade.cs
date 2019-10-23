using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private int pendingAddCount = 0;
        private Dictionary<string, int> keepUpToCounts = new Dictionary<string, int>();

        HudCheckBox UIAutoTradeEnable { get; set; }
        HudCheckBox UIAutoTradeTestMode { get; set; }
        HudCheckBox UIAutoTradeThink { get; set; }
        HudCheckBox UIAutoTradeOnlyFromMainPack { get; set; }
        HudCheckBox UIAutoTradeAutoAccept { get; set; }

        public AutoTrade()
        {
            try
            {
                Directory.CreateDirectory(Path.Combine(Util.GetPluginDirectory(), "autotrade"));
                Directory.CreateDirectory(Path.Combine(Util.GetCharacterDirectory(), "autotrade"));
                Directory.CreateDirectory(Path.Combine(Util.GetServerDirectory(), "autotrade"));

                UIAutoTradeEnable = Globals.MainView.view != null ? (HudCheckBox)Globals.MainView.view["AutoTradeEnabled"] : new HudCheckBox();
                UIAutoTradeEnable.Change += UIAutoTradeEnable_Change;

                UIAutoTradeTestMode = Globals.MainView.view != null ? (HudCheckBox)Globals.MainView.view["AutoTradeTestMode"] : new HudCheckBox();
                UIAutoTradeTestMode.Change += UIAutoTradeTestMode_Change;

                UIAutoTradeThink = Globals.MainView.view != null ? (HudCheckBox)Globals.MainView.view["AutoTradeThink"] : new HudCheckBox();
                UIAutoTradeThink.Change += UIAutoTradeThink_Change;

                UIAutoTradeOnlyFromMainPack = Globals.MainView.view != null ? (HudCheckBox)Globals.MainView.view["AutoTradeOnlyFromMainPack"] : new HudCheckBox();
                UIAutoTradeOnlyFromMainPack.Change += UIAutoTradeOnlyFromMainPack_Change;

                UIAutoTradeAutoAccept = Globals.MainView.view != null ? (HudCheckBox)Globals.MainView.view["AutoTradeAutoAccept"] : new HudCheckBox();
                UIAutoTradeAutoAccept.Change += UIAutoTradeAutoAccept_Change;

                Globals.Core.WorldFilter.EnterTrade += WorldFilter_EnterTrade;
                Globals.Core.WorldFilter.EndTrade += WorldFilter_EndTrade;
                Globals.Core.WorldFilter.AddTradeItem += WorldFilter_AddTradeItem;
                Globals.Core.WorldFilter.FailToAddTradeItem += WorldFilter_FailToAddTradeItem;
                Globals.Core.CommandLineText += Core_CommandLineText;

                Globals.Settings.AutoSalvage.PropertyChanged += (s, e) => UpdateUI();

                UpdateUI();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void WorldFilter_FailToAddTradeItem(object sender, FailToAddTradeItemEventArgs e)
        {
            if (running)
                pendingAddCount--;
        }

        private void WorldFilter_AddTradeItem(object sender, AddTradeItemEventArgs e)
        {
            if (running && e.SideId != 2)
                pendingAddCount--;
        }

        private void WorldFilter_EndTrade(object sender, EndTradeEventArgs e)
        {
            if (running)
                Stop();
            traderId = 0;
        }

        private void UIAutoTradeAutoAccept_Change(object sender, EventArgs e)
        {
            Globals.Settings.AutoTrade.AutoAccept = UIAutoTradeAutoAccept.Checked;
        }

        private void UIAutoTradeOnlyFromMainPack_Change(object sender, EventArgs e)
        {
            Globals.Settings.AutoTrade.OnlyFromMainPack = UIAutoTradeOnlyFromMainPack.Checked;
        }

        private void UIAutoTradeThink_Change(object sender, EventArgs e)
        {
            Globals.Settings.AutoTrade.Think = UIAutoTradeThink.Checked;
        }

        private void UIAutoTradeTestMode_Change(object sender, EventArgs e)
        {
            Globals.Settings.AutoTrade.TestMode = UIAutoTradeTestMode.Checked;
        }

        private void UIAutoTradeEnable_Change(object sender, EventArgs e)
        {
            Globals.Settings.AutoTrade.Enabled = UIAutoTradeEnable.Checked;
        }

        private void UpdateUI()
        {
            UIAutoTradeEnable.Checked = Globals.Settings.AutoTrade.Enabled;
            UIAutoTradeTestMode.Checked = Globals.Settings.AutoTrade.TestMode;
            UIAutoTradeThink.Checked = Globals.Settings.AutoTrade.Think;
            UIAutoTradeOnlyFromMainPack.Checked = Globals.Settings.AutoTrade.OnlyFromMainPack;
            UIAutoTradeAutoAccept.Checked = Globals.Settings.AutoTrade.AutoAccept;
        }

        private void Core_CommandLineText(object sender, Decal.Adapter.ChatParserInterceptEventArgs e)
        {
            try
            {
                if (e.Text.StartsWith("/ub autotrade "))
                {
                    var path = e.Text.Replace("/ub autotrade ", "").Trim();
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
            pendingAddCount = 0;
            keepUpToCounts.Clear();

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
            if (!Globals.Settings.AutoTrade.Enabled)
                return;
            if (!running)
                return;

            try
            {
                if (doAccept)
                {
                    if (pendingAddCount <= 0)
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

                AddTradeItems();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Stop(bool profileLoaded = true)
        {
            if (profileLoaded)
            {
                if (Globals.Settings.AutoTrade.Think == true)
                {
                    Util.Think("AutoTrade finished: " + traderName);
                }
                else
                {
                    Util.WriteToChat("AutoTrade finished: " + traderName);
                }
            }

            if (lootProfile != null) ((VTClassic.LootCore)lootProfile).UnloadProfile();

            VTankControl.Nav_UnBlock();
            // restore cram/stack settings
            VTankControl.Item_UnBlock();

            running = false;
            keepUpToCounts.Clear();
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

        private void AddTradeItems()
        {
            try
            {
                foreach (var item in GetTradeItems())
                {
                    Logger.Debug($"Trade Item: {Util.GetObjectName(item.Key)}");

                    if (item.Value.IsKeepUpTo)
                    {
                        if (!keepUpToCounts.ContainsKey(item.Value.RuleName))
                            keepUpToCounts.Add(item.Value.RuleName, 0);

                        var stackCount = Globals.Core.WorldFilter[item.Key].Values(LongValueKey.StackCount, 1);
                        if (item.Value.Data1 < 0)
                        {
                            if (keepUpToCounts[item.Value.RuleName] < Math.Abs(item.Value.Data1))
                            {
                                Logger.Debug($"Need to keep: {Math.Abs(item.Value.Data1) - keepUpToCounts[item.Value.RuleName]}");
                                if (stackCount > Math.Abs(item.Value.Data1) - keepUpToCounts[item.Value.RuleName])
                                {
                                    int splitCount = stackCount - (Math.Abs(item.Value.Data1) - keepUpToCounts[item.Value.RuleName]);
                                    EventHandler<CreateObjectEventArgs> splitHandler = null;
                                    splitHandler = (sender, e) =>
                                    {
                                        if (e.New.Name == Globals.Core.WorldFilter[item.Key].Name &&
                                            e.New.Values(LongValueKey.StackCount, 1) == splitCount)
                                        {
                                            Logger.Debug($"Adding to trade window: {Util.GetObjectName(e.New.Id)}");
                                            Globals.Core.Actions.TradeAdd(e.New.Id);
                                            pendingAddCount++;
                                            Globals.Core.WorldFilter.CreateObject -= splitHandler;
                                        }
                                    };

                                    Globals.Core.Actions.SelectItem(item.Key);
                                    Globals.Core.Actions.SelectedStackCount = splitCount;
                                    Globals.Core.WorldFilter.CreateObject += splitHandler;
                                    keepUpToCounts[item.Value.RuleName] += Math.Abs(item.Value.Data1) - keepUpToCounts[item.Value.RuleName];
                                    Globals.Core.Actions.MoveItem(item.Key, Globals.Core.CharacterFilter.Id, 0, false);

                                    Logger.Debug(string.Format("AutoTrade Splitting {0}. old: {1} new: {2}", Util.GetObjectName(item.Key),
                                        stackCount,
                                        splitCount));
                                }
                                else
                                {
                                    Logger.Debug($"Keeping: {Util.GetObjectName(item.Key)} ({stackCount})");
                                    keepUpToCounts[item.Value.RuleName] += stackCount;
                                }

                                continue;
                            }
                        }
                        else
                        {
                            if (keepUpToCounts[item.Value.RuleName] >= item.Value.Data1)
                            {
                                continue;
                            }

                            if (stackCount > item.Value.Data1 - keepUpToCounts[item.Value.RuleName])
                            {
                                int neededCount = item.Value.Data1 - keepUpToCounts[item.Value.RuleName];
                                EventHandler<CreateObjectEventArgs> splitHandler = null;
                                splitHandler = (sender, e) =>
                                {
                                    if (e.New.Name == Globals.Core.WorldFilter[item.Key].Name &&
                                        e.New.Values(LongValueKey.StackCount, 1) == neededCount)
                                    {
                                        Globals.Core.Actions.TradeAdd(e.New.Id);
                                        pendingAddCount++;
                                        Globals.Core.WorldFilter.CreateObject -= splitHandler;
                                    }
                                };

                                Globals.Core.Actions.SelectItem(item.Key);
                                Globals.Core.Actions.SelectedStackCount = neededCount;
                                Globals.Core.WorldFilter.CreateObject += splitHandler;
                                keepUpToCounts[item.Value.RuleName] += neededCount;
                                Globals.Core.Actions.MoveItem(item.Key, Globals.Core.CharacterFilter.Id, 0, false);

                                Logger.Debug(string.Format("AutoTrade Splitting {0}. old: {1} new: {2}", Util.GetObjectName(item.Key),
                                    stackCount,
                                    item.Value.Data1 - keepUpToCounts[item.Value.RuleName]));

                                continue;
                            }
                        }

                        keepUpToCounts[item.Value.RuleName] += stackCount;
                    }

                    Logger.Debug($"Adding to trade: {Util.GetObjectName(item.Key)}");
                    Globals.Core.Actions.TradeAdd(item.Key);
                    pendingAddCount++;
                }

                if (Globals.Settings.AutoTrade.AutoAccept)
                {
                    doAccept = true;
                    return;
                }

                Stop();
            }
            catch (Exception ex) { Logger.LogException(ex); }
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
                    Globals.Core.CommandLineText -= Core_CommandLineText;
                }
                disposed = true;
            }
        }

    }
}
