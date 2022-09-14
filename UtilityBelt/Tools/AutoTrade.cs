using Decal.Adapter.Wrappers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Settings;
using UBService.Lib.Settings;

namespace UtilityBelt.Tools {
    [Name("AutoTrade")]
    [Summary("Provides commands for automatic muling via the trade window")]
    [FullDescription(@"
This plugin will add items to a trade window that match keep or keep # rules in a VTank loot profile (.utl file). This can be used to automatically mule items during a meta as an alternative to giving items directly. Much like other tools in UB, it requires all items to be assessed to determine whether they can/should be added to the trade window. Other features include automatically accepting the trade after items have been added, and auto-accepting a trade from characters whose name matches a certain pattern. The tool can be launched either from the command line or automatically if a profile exists that matches your trade partner's name.

When enabled, AutoTrade will attempt to load a profile in one the following locations (stopping when it finds the first match):

* Documents\\Decal Plugins\\UtilityBelt\\autotrade\\&lt;Server&gt;\\&lt;Character&gt;\\&lt;Trade Partner Name&gt;.utl
* Documents\\Decal Plugins\\UtilityBelt\\autotrade\\&lt;Server&gt;\\&lt;Trade Partner Name&gt;.utl
* Documents\\Decal Plugins\\UtilityBelt\\autotrade\\&lt;Trade Partner Name&gt;.utl
* Documents\\Decal Plugins\\UtilityBelt\\autotrade\\&lt;Server&gt;\\&lt;Character&gt;\default.utl
* Documents\\Decal Plugins\\UtilityBelt\\autotrade\\&lt;Server&gt;\\default.utl
* Documents\\Decal Plugins\\UtilityBelt\\autotrade\\default.utl

### Keep/Keep # Rules

AutoTrade supports keep and keep # actions in VTank rules. These actions have the following meanings:

 * **Keep** - Add all items to trade window that match the rule
 * **Keep # (# > 0)** - Add up to (#) items to trade window that match the rule (may split stacks if applicable)
 * **Keep # (# < 0)** - Add all items minus (#) to trade window that match the rule (may split stacks if applicable)

###  Example VTank Profiles

 * [Shen-Sort I.utl](/utl/Shen-Sort I.utl) - Add trophies/epics/weapons to trade window for sort mule
 * [Shen-Steel I.utl](/utl/Shen-Steel I.utl) - Add full bags of Salvaged Steel to trade window

### Auto-Accept List

AutoTrade supports a list of patterns you want to auto-accept any incoming trades from. So, when your trade partner accepts a trade and matches at least one of the patterns in your list, your character will automatically accept the trade. You can add patterns to your character's list, a shared server list, or a global (all servers) list. The patterns can be any .NET regular expression.

**Note**: Formerly, the auto-accept list was stored in the character-specific settings.json file. This will continue to be supported for backwards-compatibility purposes, but the `/ub autotrade autoaccept add/remove` commands will not modify this list. You can use the settings panel to add/remove characters from this list.
    ")]
    public class AutoTrade : ToolBase {
        private static readonly string AutoAcceptListFileName = "autoAcceptList.json";
        private bool disposed = false;
        private object lootProfile = null;
        private bool waitingForIds = false;
        private DateTime lastIdSpam = DateTime.MinValue;
        private List<int> itemsToId = new List<int>();
        private int traderId = 0;
        private string traderName = null;
        private bool running = false;
        private bool doAccept = false;
        private Dictionary<string, int> keepUpToCounts = new Dictionary<string, int>();
        private readonly List<int> pendingAddItems = new List<int>();
        private readonly List<int> addedItems = new List<int>();
        private DateTime bailTimer = DateTime.MinValue;

        #region Settings
        [Summary("Enable AutoTrade when Trade Window is Opened")]
        [Hotkey("AutoTrade", "Toggle AutoTrade functionality")]
        public readonly Setting<bool> Enabled = new Setting<bool>(false);

        [Summary("Test mode (don't actually add to trade window, just echo to the chat window)")]
        public readonly Setting<bool> TestMode = new Setting<bool>(false);

        [Summary("Think to yourself when auto trade is completed")]
        public readonly Setting<bool> Think = new Setting<bool>(false);

        [Summary("Only trade things in your main pack")]
        public readonly Setting<bool> OnlyFromMainPack = new Setting<bool>(false);

        [Summary("Auto accept trade after all items added")]
        public readonly Setting<bool> AutoAccept = new Setting<bool>(false);

        [Summary("List of characters to auto-accept trade from")]
        public readonly Setting<ObservableCollection<string>> AutoAcceptChars = new Setting<ObservableCollection<string>>(new ObservableCollection<string>());

        private void AutoAcceptChars_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
            try {
                //OnPropertyChanged(nameof(AutoAcceptChars));
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        #endregion

        #region Commands
        #region /ub autotrade
        [Summary("Adds all items matching a VTank loot profile to the trade window.")]
        [Usage("/ub autotrade { <lootProfile> | autoaccept { add[gs] <namePattern> | remove[gs] <namePattern> | list } }")]
        [Example("/ub autotrade", "Adds all items matching CharacterName.utl to the trade window, where CharacterName is the name of the character you currently have a trade open with.")]
        [Example("/ub autotrade mfk.utl", "Adds all items matching mfk.utl to the currently open trade window")]
        [Example("/ub autotrade autoaccept add Shen-.*", "Adds any char matching the pattern Shen-.* to the current character's auto-accept list")]
        [Example("/ub autotrade autoaccept removes Sunnuj", "Removes Sunnuj from the auto-accept list for all of your characters on the current server")]
        [Example("/ub autotrade autoaccept addg Yonneh", "Adds Yonneh to the auto-accept list for all of your characters on any server")]
        [Example("/ub autotrade autoaccept list", "Lists all auto accept name patterns")]
        [CommandPattern("autotrade", @"^\s*autoaccept\s+(?<Verb>(?:add|remove|list)[gs]?)\s*(?<CharPattern>.*)|(?<LootProfile>.*\.utl)$")]
        public void DoAutoTrade(string command, Match args) {
            if (!string.IsNullOrEmpty(args.Groups["LootProfile"].Value)) {
                LogDebug($"Starting autotrade with profile: {args.Groups["LootProfile"].Value}");
                Start(traderId, args.Groups["LootProfile"].Value);
            }
            else if (args.Groups["Verb"].Value == "list") {
                var list = GetAutoAcceptChars();
                Logger.WriteToChat("Auto Accept List:");
                int i = 0;
                foreach (var aac in list) {
                    Logger.WriteToChat($" [{++i}] {aac}");
                }
            }
            else if (!string.IsNullOrEmpty(args.Groups["Verb"].Value) &&
                    !string.IsNullOrEmpty(args.Groups["CharPattern"].Value)) {
                ChangeAutoAcceptCharList(args.Groups["Verb"].Value, args.Groups["CharPattern"].Value);
            }
        }
        #endregion
        #endregion

        public AutoTrade(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        public override void Init() {
            base.Init();
            try {
                Directory.CreateDirectory(Path.Combine(Util.GetPluginDirectory(), "autotrade"));
                Directory.CreateDirectory(Path.Combine(Util.GetCharacterDirectory(), "autotrade"));
                Directory.CreateDirectory(Path.Combine(Util.GetServerDirectory(), "autotrade"));

                UB.Core.WorldFilter.EnterTrade += WorldFilter_EnterTrade;
                UB.Core.WorldFilter.EndTrade += WorldFilter_EndTrade;
                UB.Core.WorldFilter.AddTradeItem += WorldFilter_AddTradeItem;
                UB.Core.WorldFilter.FailToAddTradeItem += WorldFilter_FailToAddTradeItem;
                UB.Core.WorldFilter.AcceptTrade += WorldFilter_AcceptTrade;

                AutoAcceptChars.Value.CollectionChanged += AutoAcceptChars_CollectionChanged;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void ChangeAutoAcceptCharList(string verb, string charPattern) {
            string path = null;
            switch (verb.LastOrDefault()) {
                case 'g':
                    path = Path.Combine(Path.Combine(Util.GetPluginDirectory(), "autotrade"), AutoAcceptListFileName);
                    break;
                case 's':
                    path = Path.Combine(Path.Combine(Util.GetServerDirectory(), "autotrade"), AutoAcceptListFileName);
                    break;
                default:
                    path = Path.Combine(Path.Combine(Util.GetCharacterDirectory(), "autotrade"), AutoAcceptListFileName);
                    break;
            }

            var autoAcceptList = new List<string>(ReadAutoAcceptCharFile(path));

            if (verb.StartsWith("add") && !autoAcceptList.Contains(charPattern)) {
                try {
                    Regex.Match("", charPattern);
                }
                catch {
                    Logger.Error("Error: Invalid regex");
                    return;
                }

                autoAcceptList.Add(charPattern);
            }
            else if (verb.StartsWith("remove") && autoAcceptList.Contains(charPattern)) {
                autoAcceptList.Remove(charPattern);
            }

            if (autoAcceptList.Count <= 0)
                File.Delete(path);
            else
                UBLoader.Lib.File.TryWrite(path, JsonConvert.SerializeObject(autoAcceptList), false);
        }

        private void WorldFilter_AcceptTrade(object sender, AcceptTradeEventArgs e) {
            try {
                if (e.TargetId == UB.Core.CharacterFilter.Id)
                    return;

                var target = UB.Core.WorldFilter[e.TargetId];

                if (target == null)
                    return;

                var autoAcceptCharList = GetAutoAcceptChars();
                Logger.Debug($"Checking {target.Name} against {autoAcceptCharList.Count()} accepted chars.");
                if (autoAcceptCharList.Any(c => Regex.IsMatch(target.Name, c, RegexOptions.IgnoreCase))) {
                    LogDebug("Accepting trade...");
                    UB.Core.Actions.TradeAccept();
                    Util.ThinkOrWrite("Trade accepted: " + target.Name, UB.AutoTrade.Think);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void WorldFilter_FailToAddTradeItem(object sender, FailToAddTradeItemEventArgs e) {
            try {
                if (running)
                    pendingAddItems.Remove(e.ItemId);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void WorldFilter_AddTradeItem(object sender, AddTradeItemEventArgs e) {
            try {
                if (running && e.SideId != 2) {
                    LogDebug($"{Util.GetObjectName(e.ItemId)} added to trade window");
                    if (pendingAddItems.Remove(e.ItemId)) {
                        addedItems.Add(e.ItemId);
                        bailTimer = DateTime.UtcNow;
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void WorldFilter_EndTrade(object sender, EndTradeEventArgs e) {
            try {
                if (running)
                    Stop();
                traderId = 0;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void WorldFilter_EnterTrade(object sender, Decal.Adapter.Wrappers.EnterTradeEventArgs e) {
            try {
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
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public void Start(int traderId, string useProfilePath = "") {
            if (running) {
                LogError("Already running.");
                return;
            }

            doAccept = false;
            pendingAddItems.Clear();
            keepUpToCounts.Clear();
            addedItems.Clear();
            bailTimer = DateTime.UtcNow;

            if (!UB.Core.Actions.IsValidObject(traderId)) {
                LogError($"You must open a trade with someone first!");
                traderId = 0;
                return;
            }

            LogDebug("Loading LootCore");
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
            UBHelper.vTank.Decision_Lock(uTank2.ActionLockType.Navigation, TimeSpan.FromMilliseconds(1000));

            // Load our loot profile
            LogDebug($"Loading loot profile at {profilePath}");
            try {
                ((VTClassic.LootCore)lootProfile).LoadProfile(profilePath, false);
            }
            catch (Exception) {
                LogError("Unable to load loot profile. Ensure that no profile is loaded in Virindi Item Tool.");
                Stop();
                return;
            }

            running = true;
            UB.Core.RenderFrame += Core_RenderFrame;

            itemsToId.Clear();
            using (var inventory = UB.Core.WorldFilter.GetInventory()) {
                // filter inventory beforehand if we are only trading from the main pack
                if (OnlyFromMainPack == true) {
                    inventory.SetFilter(new ByContainerFilter(UB.Core.CharacterFilter.Id));
                }

                itemsToId.AddRange(inventory.Select(item => item.Id));
            }

            if (UB.Assessor.NeedsInventoryData(itemsToId)) {
                new Assessor.Job(UB.Assessor, ref itemsToId, (_) => { bailTimer = DateTime.UtcNow; }, () => { waitingForIds = false; }, false);
                waitingForIds = true;
                lastIdSpam = DateTime.UtcNow;
            }
            UBHelper.vTank.Decision_Lock(uTank2.ActionLockType.Navigation, TimeSpan.FromMilliseconds(30000));
            UBHelper.vTank.Decision_Lock(uTank2.ActionLockType.ItemUse, TimeSpan.FromMilliseconds(30000));
        }

        private IEnumerable<string> GetAutoAcceptChars() {
            var globalChars = ReadAutoAcceptCharFile(Path.Combine(Path.Combine(Util.GetPluginDirectory(), "autotrade"), AutoAcceptListFileName));
            var serverChars = ReadAutoAcceptCharFile(Path.Combine(Path.Combine(Util.GetServerDirectory(), "autotrade"), AutoAcceptListFileName));
            var localChars = ReadAutoAcceptCharFile(Path.Combine(Path.Combine(Util.GetCharacterDirectory(), "autotrade"), AutoAcceptListFileName));

            return globalChars.Union(serverChars).Union(localChars).Union(AutoAcceptChars.Value);
        }

        private IEnumerable<string> ReadAutoAcceptCharFile(string file) {
            var list = new List<string>();
            if (File.Exists(file)) {
                var json = File.ReadAllText(file);
                list.AddRange(JsonConvert.DeserializeObject<IEnumerable<string>>(json));
            }
            return list.AsReadOnly();
        }

        //FIXME - unregister when not in use
        public void Core_RenderFrame(object sender, EventArgs e) {
            if (!running) {
                UB.Core.RenderFrame -= Core_RenderFrame;
                return;
            }

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

                if (Enabled && UBHelper.vTank.locks[uTank2.ActionLockType.Navigation] < DateTime.UtcNow + TimeSpan.FromSeconds(1)) {
                    UBHelper.vTank.Decision_Lock(uTank2.ActionLockType.Navigation, TimeSpan.FromMilliseconds(30000));
                    UBHelper.vTank.Decision_Lock(uTank2.ActionLockType.ItemUse, TimeSpan.FromMilliseconds(30000));
                }

                if (waitingForIds)
                    return;

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

                UBHelper.vTank.Decision_UnLock(uTank2.ActionLockType.Navigation);
                UBHelper.vTank.Decision_UnLock(uTank2.ActionLockType.ItemUse);
            }
            catch (Exception ex) { Logger.LogException(ex); }
            finally {
                running = false;
                doAccept = false;
                keepUpToCounts.Clear();
                pendingAddItems.Clear();
                addedItems.Clear();
                UB.Core.RenderFrame -= Core_RenderFrame;
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
                try {
                    if (e.New.Container == UB.Core.CharacterFilter.Id &&
                        e.New.Name == UB.Core.WorldFilter[item].Name &&
                        e.New.Type == UB.Core.WorldFilter[item].Type &&
                        e.New.Values(LongValueKey.StackCount, 1) == splitCount) {
                        LogDebug($"Adding {splitCount} of {Util.GetObjectName(e.New.Id)} to trade window");
                        pendingAddItems.Add(e.New.Id);
                        UB.Core.Actions.TradeAdd(e.New.Id);
                        UB.Core.WorldFilter.CreateObject -= splitHandler;
                    }
                }
                catch (Exception ex) { Logger.LogException(ex); }
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
