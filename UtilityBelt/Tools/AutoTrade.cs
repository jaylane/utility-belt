using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UtilityBelt.Lib;
using VirindiViewService.Controls;

namespace UtilityBelt.Tools {
    [Name("AutoTrade")]
    public class AutoTrade : ToolBase {
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

        #region Config
        [Summary("AutoTrade Enabled")]
        [DefaultValue(false)]
        public bool Enabled {
            get { return (bool)GetSetting("Enabled"); }
            set { UpdateSetting("Enabled", value); }
        }

        [Summary("Test mode (don't actually add to trade window, just echo to the chat window)")]
        [DefaultValue(false)]
        public bool TestMode {
            get { return (bool)GetSetting("TestMode"); }
            set { UpdateSetting("TestMode", value); }
        }

        [Summary("Think to yourself when auto trade is completed")]
        [DefaultValue(false)]
        public bool Think {
            get { return (bool)GetSetting("Think"); }
            set { UpdateSetting("Think", value); }
        }

        [Summary("Only trade things in your main pack")]
        [DefaultValue(false)]
        public bool OnlyFromMainPack {
            get { return (bool)GetSetting("OnlyFromMainPack"); }
            set { UpdateSetting("OnlyFromMainPack", value); }
        }

        [Summary("Auto accept trade after all items added")]
        [DefaultValue(false)]
        public bool AutoAccept {
            get { return (bool)GetSetting("AutoAccept"); }
            set { UpdateSetting("AutoAccept", value); }
        }

        [Summary("List of characters to auto-accept trade from")]
        [DefaultValue(null)]
        public ObservableCollection<string> AutoAcceptChars { get; set; } = new ObservableCollection<string>();

        private void AutoAcceptChars_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
            OnPropertyChanged(nameof(AutoAcceptChars));
        }
        #endregion

        #region Commands
        #region /ub autotrade
        [Summary("Adds all items matching a VTank loot profile to the trade window.")]
        [Usage("/ub autotrade <lootProfile>")]
        [Example("/ub autotrade", "Adds all items matching CharacterName.utl to the trade window, where CharacterName is the name of the character you currently have a trade open with.")]
        [Example("/ub autotrade mfk.utl", "Adds all items matching mfk.utl to the currently open trade window")]
        [CommandPattern("autotrade", @"^ *(?<LootProfile>.*) *$")]
        public void DoAutoTrade(string command, Match args) {
            Start(traderId, args.Groups["LootProfile"].Value);
        }
        #endregion
        #endregion

        public AutoTrade(UtilityBeltPlugin ub, string name) : base(ub, name) {
            try {
                Directory.CreateDirectory(Path.Combine(Util.GetPluginDirectory(), "autotrade"));
                Directory.CreateDirectory(Path.Combine(Util.GetCharacterDirectory(), "autotrade"));
                Directory.CreateDirectory(Path.Combine(Util.GetServerDirectory(), "autotrade"));

                UB.Core.WorldFilter.EnterTrade += WorldFilter_EnterTrade;
                UB.Core.WorldFilter.EndTrade += WorldFilter_EndTrade;
                UB.Core.WorldFilter.AddTradeItem += WorldFilter_AddTradeItem;
                UB.Core.WorldFilter.FailToAddTradeItem += WorldFilter_FailToAddTradeItem;
                UB.Core.WorldFilter.AcceptTrade += WorldFilter_AcceptTrade;
                UB.Core.RenderFrame += Core_RenderFrame;

                AutoAcceptChars.CollectionChanged += AutoAcceptChars_CollectionChanged;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void WorldFilter_AcceptTrade(object sender, AcceptTradeEventArgs e) {
            if (e.TargetId == UB.Core.CharacterFilter.Id)
                return;

            var target = UB.Core.WorldFilter[e.TargetId];

            if (target == null)
                return;

            Logger.Debug($"Checking {target.Name} against {AutoAcceptChars.Count} accepted chars.");
            if (AutoAcceptChars.Any(c => Regex.IsMatch(target.Name, c, RegexOptions.IgnoreCase))) {
                LogDebug("Accepting trade...");
                UB.Core.Actions.TradeAccept();
                Util.ThinkOrWrite("Trade accepted: " + target.Name, UB.AutoTrade.Think);
            }
        }

        private void WorldFilter_FailToAddTradeItem(object sender, FailToAddTradeItemEventArgs e) {
            if (running)
                pendingAddItems.Remove(e.ItemId);
        }

        private void WorldFilter_AddTradeItem(object sender, AddTradeItemEventArgs e) {
            if (running && e.SideId != 2) {
                LogDebug($"{Util.GetObjectName(e.ItemId)} added to trade window");
                if (pendingAddItems.Remove(e.ItemId)) {
                    addedItems.Add(e.ItemId);
                    bailTimer = DateTime.UtcNow;
                }
            }
        }

        private void WorldFilter_EndTrade(object sender, EndTradeEventArgs e) {
            if (running)
                Stop();
            traderId = 0;
        }

        private void WorldFilter_EnterTrade(object sender, Decal.Adapter.Wrappers.EnterTradeEventArgs e) {
            if (e.TradeeId == UB.Core.CharacterFilter.Id)
                traderId = e.TraderId;
            else if (e.TraderId == UB.Core.CharacterFilter.Id)
                traderId = e.TradeeId;

            if (traderId == 0 || UB.Core.WorldFilter[traderId] == null)
                return;

            if (!UB.AutoTrade.Enabled)
                return;

            traderName = UB.Core.WorldFilter[traderId].Name;

            Start(traderId);
        }

        public void Start(int traderId, string useProfilePath = "") {
            running = true;
            doAccept = false;
            pendingAddItems.Clear();
            keepUpToCounts.Clear();
            addedItems.Clear();
            bailTimer = DateTime.UtcNow;
            lastIdCount = 0;

            if (!UB.Core.Actions.IsValidObject(traderId)) {
                LogError($"You must open a trade with someone first!");
                traderId = 0;
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

            var tradePartner = UB.Core.WorldFilter[traderId];
            var profilePath = GetProfilePath(string.IsNullOrEmpty(useProfilePath) ? (tradePartner.Name + ".utl") : useProfilePath);

            if (!File.Exists(profilePath)) {
                WriteToChat("No auto trade profile exists: " + profilePath);
                Stop(false);
                return;
            }

            VTankControl.Nav_Block(1000, false); // quick block to keep vtank from truckin' off before the profile loads, but short enough to not matter if it errors out and doesn't unlock

            // Load our loot profile
            ((VTClassic.LootCore)lootProfile).LoadProfile(profilePath, false);

            itemsToId.Clear();
            var inventory = UB.Core.WorldFilter.GetInventory();

            // filter inventory beforehand if we are only trading from the main pack
            if (OnlyFromMainPack == true) {
                inventory.SetFilter(new ByContainerFilter(UB.Core.CharacterFilter.Id));
            }

            itemsToId.AddRange(inventory.Select(item => item.Id));

            if (UB.Assessor.NeedsInventoryData(itemsToId)) {
                UB.Assessor.RequestAll(itemsToId);
                waitingForIds = true;
                lastIdSpam = DateTime.UtcNow;
            }

            VTankControl.Item_Block(30000, false);
            VTankControl.Nav_Block(30000, UB.Plugin.Debug);
        }

        public void Core_RenderFrame(object sender, EventArgs e) {
            if (!running || !Enabled)
                return;

            try {
                if (doAccept) {
                    if (pendingAddItems.Count <= 0) {
                        doAccept = false;
                        if (traderId != 0 && AutoAccept) {
                            LogDebug("Accepting trade");
                            UB.Core.Actions.TradeAccept();
                        }

                        Stop();
                    }

                    return;
                }

                if (DateTime.UtcNow - bailTimer > TimeSpan.FromSeconds(10)) {
                    LogDebug("timed out - STOPPING");
                    Stop();
                }

                if (Enabled && VTankControl.navBlockedUntil < DateTime.UtcNow + TimeSpan.FromSeconds(1)) {
                    VTankControl.Item_Block(30000, UB.Plugin.Debug);
                    VTankControl.Nav_Block(30000, UB.Plugin.Debug);
                }

                if (waitingForIds) {
                    if (UB.Assessor.NeedsInventoryData(itemsToId)) {
                        if (DateTime.UtcNow - lastIdSpam > TimeSpan.FromSeconds(15)) {
                            lastIdSpam = DateTime.UtcNow;

                            WriteToChat(string.Format("waiting to id {0} items, this will take approximately {0} seconds.", UB.Assessor.GetNeededIdCount(itemsToId)));
                        }

                        var neededIdCount = UB.Assessor.GetNeededIdCount(itemsToId);
                        if (lastIdCount != neededIdCount) {
                            lastIdCount = neededIdCount;
                            bailTimer = DateTime.UtcNow;
                        }

                        // waiting
                        return;
                    }
                    else
                        waitingForIds = false;
                }

                if (TestMode) {
                    DoTestMode();
                    Stop();
                    return;
                }

                if (UB.Core.Actions.BusyState != 0)
                    return;

                foreach (var item in GetTradeItems()) {
                    // Skip if item already added to trade window
                    if (addedItems.Contains(item.Key))
                        continue;

                    LogDebug($"Trade Item: {Util.GetObjectName(item.Key)}");

                    if (item.Value.IsKeepUpTo) {
                        if (!keepUpToCounts.ContainsKey(item.Value.RuleName))
                            keepUpToCounts.Add(item.Value.RuleName, 0);

                        var stackCount = UB.Core.WorldFilter[item.Key].Values(LongValueKey.StackCount, 1);
                        if (item.Value.Data1 < 0) // keep this many
                        {
                            if (keepUpToCounts[item.Value.RuleName] < -1 * item.Value.Data1) {
                                // add kept item to addedItems list so we don't try to add it in another pass
                                addedItems.Add(item.Key);

                                if (stackCount > -1 * item.Value.Data1 - keepUpToCounts[item.Value.RuleName]) {
                                    TrySplitItem(item.Key, stackCount, stackCount - (-1 * item.Value.Data1 - keepUpToCounts[item.Value.RuleName]));
                                    keepUpToCounts[item.Value.RuleName] = -1 * item.Value.Data1;
                                    return;
                                }
                                else
                                    keepUpToCounts[item.Value.RuleName] += stackCount;
                            }
                            else {
                                AddToTradeWindow(item.Key);
                            }
                        }
                        else // give this many
                        {
                            if (keepUpToCounts[item.Value.RuleName] >= item.Value.Data1)
                                continue;

                            if (stackCount > item.Value.Data1 - keepUpToCounts[item.Value.RuleName]) {
                                TrySplitItem(item.Key, stackCount, item.Value.Data1 - keepUpToCounts[item.Value.RuleName]);
                                keepUpToCounts[item.Value.RuleName] = item.Value.Data1;
                                return;
                            }
                            else {
                                keepUpToCounts[item.Value.RuleName] += stackCount;
                                AddToTradeWindow(item.Key);
                            }
                        }
                    }
                    else if (item.Value.IsKeep) {
                        AddToTradeWindow(item.Key);
                    }
                }

                if (AutoAccept)
                    doAccept = true;
                else
                    Stop();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Stop(bool profileLoaded = true) {
            try {
                if (profileLoaded) {
                    Util.ThinkOrWrite("AutoTrade finished: " + traderName, Think);
                }

                if (lootProfile != null) ((VTClassic.LootCore)lootProfile).UnloadProfile();

                VTankControl.Nav_UnBlock();
                // restore cram/stack settings
                VTankControl.Item_UnBlock();
            }
            catch (Exception ex) { Logger.LogException(ex); }
            finally {
                running = false;
                doAccept = false;
                keepUpToCounts.Clear();
                pendingAddItems.Clear();
                addedItems.Clear();
            }
        }

        private void DoTestMode() {
            try {
                var items = new StringBuilder();
                items.AppendLine("Trade Items:");

                foreach (var item in GetTradeItems()) {
                    items.AppendLine($"  {Util.GetObjectName(item.Key)} - {item.Value.RuleName}");
                }

                WriteToChat(items.ToString().Replace("\r", ""));
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private IEnumerable<KeyValuePair<int, uTank2.LootPlugins.LootAction>> GetTradeItems() {
            foreach (var item in itemsToId.OrderBy(i => UB.Core.WorldFilter[i].Values(LongValueKey.StackCount))) {
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

        private bool ItemIsSafeToGetRidOf(int item) {
            var wo = UB.Core.WorldFilter[item];
            if (wo == null)
                return false;
            if (wo.Values(LongValueKey.Attuned, 0) > 0)
                return false;

            return Util.IsItemSafeToGetRidOf(item);
        }

        private void AddToTradeWindow(int itemId) {
            LogDebug($"Adding to trade window: {Util.GetObjectName(itemId)}");
            pendingAddItems.Add(itemId);
            UB.Core.Actions.TradeAdd(itemId);
        }

        private void TrySplitItem(int item, int stackCount, int splitCount) {
            EventHandler<CreateObjectEventArgs> splitHandler = null;
            splitHandler = (sender, e) => {
                if (e.New.Container == UB.Core.CharacterFilter.Id &&
                    e.New.Name == UB.Core.WorldFilter[item].Name &&
                    e.New.Type == UB.Core.WorldFilter[item].Type &&
                    e.New.Values(LongValueKey.StackCount, 1) == splitCount) {
                    LogDebug($"Adding {splitCount} of {Util.GetObjectName(e.New.Id)} to trade window");
                    pendingAddItems.Add(e.New.Id);
                    UB.Core.Actions.TradeAdd(e.New.Id);
                    UB.Core.WorldFilter.CreateObject -= splitHandler;
                }
            };

            UB.Core.Actions.SelectItem(item);
            UB.Core.Actions.SelectedStackCount = splitCount;
            UB.Core.WorldFilter.CreateObject += splitHandler;
            UB.Core.Actions.MoveItem(item, UB.Core.CharacterFilter.Id, 0, false);

            LogDebug($"Splitting {Util.GetObjectName(item)}. old: {stackCount} new: {splitCount}");
        }

        private string GetProfilePath(string profileName) {
            var charPath = Path.Combine(Util.GetCharacterDirectory(), "autotrade");
            var mainPath = Path.Combine(Util.GetPluginDirectory(), "autotrade");
            var serverPath = Path.Combine(Util.GetServerDirectory(), "autotrade");

            if (File.Exists(Path.Combine(charPath, profileName))) {
                return Path.Combine(charPath, profileName);
            }
            else if (File.Exists(Path.Combine(serverPath, profileName))) {
                return Path.Combine(serverPath, profileName);
            }
            else if (File.Exists(Path.Combine(mainPath, profileName))) {
                return Path.Combine(mainPath, profileName);
            }
            else if (File.Exists(Path.Combine(charPath, "default.utl"))) {
                return Path.Combine(charPath, "default.utl");
            }
            else if (File.Exists(Path.Combine(serverPath, "default.utl"))) {
                return Path.Combine(serverPath, "default.utl");
            }
            else if (File.Exists(Path.Combine(mainPath, "default.utl"))) {
                return Path.Combine(mainPath, "default.utl");
            }

            return Path.Combine(mainPath, profileName);
        }

        protected override void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    UB.Core.WorldFilter.EnterTrade -= WorldFilter_EnterTrade;
                    UB.Core.WorldFilter.EndTrade -= WorldFilter_EndTrade;
                    UB.Core.WorldFilter.AddTradeItem -= WorldFilter_AddTradeItem;
                    UB.Core.WorldFilter.FailToAddTradeItem -= WorldFilter_FailToAddTradeItem;
                    UB.Core.WorldFilter.AcceptTrade -= WorldFilter_AcceptTrade;
                    UB.Core.RenderFrame -= Core_RenderFrame;

                    base.Dispose(disposing);
                }
                disposed = true;
            }
        }

    }
}
