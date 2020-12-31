using System;
using Decal.Adapter;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using VirindiViewService.Controls;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using UtilityBelt.Lib.Quests;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Settings;
using UBLoader.Lib.Settings;

namespace UtilityBelt.Tools {
    [Name("QuestTracker")]
    [Summary("UI / commands for checking your quest flag timers.")]
    [FullDescription(@"
    TODO
    ")]
    public class QuestTracker : ToolBase {
        DateTime lastHeartbeat = DateTime.MinValue;

        public bool ShouldEat { get; private set; }

        HudTextBox UIQuestsListFilter;
        HudTabView UIQuestListNotebook;
        HudButton UIQuestListRefresh;

        HudList UITimedQuestList;
        HudList UIKillTaskQuestList;
        HudList UIOnceQuestList;

        Timer questRedrawTimer = new Timer();
        Dictionary<string, QuestFlag> questFlags = new Dictionary<string, QuestFlag>();

        #region Config
        [Summary("Loads Quests from server on login")]
        public readonly Setting<bool> AutoRequest = new Setting<bool>(true);
        #endregion

        #region Commands
        #region /ub quests
        [Summary("Checks quest flags, and thinks to yourself with the status.  To find a quest flag, open quest tracker and click on something to print the name to the chatbox. Note: If you recently completed a quest, you need to run `/myquests` first.")]
        [Usage("/ub quests check <questFlag>")]
        [Example("/ub quests check blankaug", "Think to yourself with the status of all quest flags matching blankaug")]
        [CommandPattern("quests", @"^ *(?<Verb>(check|parse)) +(?<QuestFlag>.+)$")]
        public void DoQuestsFlagCheck(string command, Match args) {
            var verb = args.Groups["Verb"].Value.Trim().ToLower();
            var text = args.Groups["QuestFlag"].Value.Trim().ToLower();

            switch (verb) {
                case "check":
                    var searchRe = new Regex(text, RegexOptions.IgnoreCase);
                    var thoughts = 0;

                    var keys = new List<string>(questFlags.Keys);
                    keys.AddRange(new List<string>(QuestFlag.FriendlyNamesLookup.Keys));
                    keys = new List<string>(keys.Distinct());
                    var foundExact = false;
                    foreach (var key in keys) {
                        if (searchRe.IsMatch(key)) {
                            ThinkQuestFlag(key);
                            thoughts++;

                            if (key.ToLower().Equals(text))
                                foundExact = true;

                            if (thoughts >= 5) {
                                Logger.WriteToChat("Quests output has been truncated to the first 5 matching quest flags.");
                                break;
                            }
                        }
                    }

                    if (thoughts == 0 || !foundExact) {
                        Util.Think($"Quest: {text} has not been completed");
                    }
                    break;

                case "parse":
                    if (QuestFlag.MyQuestRegex.IsMatch(text)) {
                        var questFlag = QuestFlag.FromMyQuestsLine(text);

                        if (questFlag != null) {
                            UpdateQuestFlag(questFlag);
                        }

                        Logger.WriteToChat($"Quest {questFlag.Name} repeat:{questFlag.RepeatTime} available:{questFlag.NextAvailable()} solves:{questFlag.Solves}/{questFlag.MaxSolves}");
                    }
                    else {
                        LogError($"Could not parse MyQuestLine");
                    }
                    break;
            }
        }
        #endregion
        #region /ub quests
        [Summary("Refreshes your quest flags with /myquests, and hides the output.")]
        [Usage("/ub myquests")]
        [Example("/ub myquests", "Refreshes your quest flags with /myquests, and hides the output")]
        [CommandPattern("myquests", @"")]
        public void DoMyQuests(string command, Match args) {
            WriteToChat($"Refreshing quests");
            GetMyQuestsList(1, true, true);
        }
        #endregion
        //regaliamaskuber - 1 solves (1522807913)"Player has collected the mask from a Virinidi Profatrix." -1 72000
        #endregion

        #region Expressions
        #region testquestflag[string questflag]
        [ExpressionMethod("testquestflag")]
        [ExpressionParameter(0, typeof(string), "questflag", "quest flag to check")]
        [ExpressionReturn(typeof(double), "Returns 1 if you have ever completed this quest flag, 0 otherwise")]
        [Summary("Checks if a quest flag has ever been completed. You must manually refresh `/myquests` after completing a quest, for these values to be up to date.  UB will call `/myquests` once on login automatically.")]
        [Example("testquestflag[spokenbobo]", "Checks if your character is flagged for bobo")]
        public object Testquestflag(string questflag) {
            return questFlags.ContainsKey(questflag);
        }
        #endregion //testquestflag[string questflag]
        #region getqueststatus[string questflag]
        [ExpressionMethod("getqueststatus")]
        [ExpressionParameter(0, typeof(string), "questflag", "quest flag to check")]
        [ExpressionReturn(typeof(double), "Returns status of a quest flag. 0 = Not Ready, 1 = Ready")]
        [Summary("Checks if a quest flag has a timer on it. If there is a timer it returns 0, if it's ready it returns 0. You must manually refresh `/myquests` after completing a quest, for these values to be up to date.  UB will call `/myquests` once on login automatically.")]
        [Example("getqueststatus[blankaug]", "Checks if your character is ready to run the blankaug quest again.")]
        public object Getqueststatus(string questflag) {
            if (questFlags.ContainsKey(questflag)) {
                return questFlags[questflag].IsReady();
            }

            // quests with no flag stored are ready
            return true;
        }
        #endregion //getqueststatus[string questflag]
        #region getquestktprogress[string questflag]
        [ExpressionMethod("getquestktprogress")]
        [ExpressionParameter(0, typeof(string), "questflag", "quest flag to check")]
        [ExpressionReturn(typeof(double), "Returns a progress count")]
        [Summary("Checks progress of a killtask quest. You must manually refresh `/myquests` after completing a quest, for these values to be up to date.  UB will call `/myquests` once on login automatically.")]
        [Example("getquestktprogress[totalgolemmagmaexarchdead]", "Checks how many exarch kills you currently have")]
        public object Getquestktprogress(string questflag) {
            if (questFlags.ContainsKey(questflag)) {
                return questFlags[questflag].Solves;
            }

            return 0;
        }
        #endregion //getquestktprogress[string questflag]
        #region getquestktrequired[string questflag]
        [ExpressionMethod("getquestktrequired")]
        [ExpressionParameter(0, typeof(string), "questflag", "quest flag to check")]
        [ExpressionReturn(typeof(double), "Returns a required count")]
        [Summary("Checks total required solves of a killtask quest.  If you have not started the killtask, this will return 0. You must manually refresh `/myquests` after completing a quest, for these values to be up to date.  UB will call `/myquests` once on login automatically.")]
        [Example("getquestktrequired[totalgolemmagmaexarchdead]", "Checks required total exarch kills for this kill task")]
        public object Getquestktrequired(string questflag) {
            if (questFlags.ContainsKey(questflag)) {
                return questFlags[questflag].MaxSolves;
            }

            return 0;
        }
        #endregion //getquestktrequired[string questflag]
        #region isrefreshingquests[]
        [ExpressionMethod("isrefreshingquests")]
        [ExpressionReturn(typeof(double), "Returns 1 if quests are currently being fetched and processed, 0 otherwise")]
        [Summary("Checks if quests are currently being fetched or processed")]
        [Example("isrefreshingquests[]", "Returns 1 if quests are currently being fetched and processed")]
        public object Isrefreshingquests() {
            return GettingQuests;
        }
        #endregion //isrefreshingquests[]
        #endregion

        public QuestTracker(UtilityBeltPlugin ub, string name) : base(ub, name) {
        }

        public override void Init() {
            base.Init();

            UIQuestsListFilter = (HudTextBox)UB.MainView.view["QuestsListFilter"];
            UIQuestListNotebook = (HudTabView)UB.MainView.view["QuestListNotebook"];
            UIQuestListRefresh = (HudButton)UB.MainView.view["QuestListRefresh"];

            UITimedQuestList = (HudList)UB.MainView.view["TimedQuestList"];
            UIKillTaskQuestList = (HudList)UB.MainView.view["KillTaskQuestList"];
            UIOnceQuestList = (HudList)UB.MainView.view["OnceQuestList"];

            UIQuestsListFilter.Change += (s, e) => { DrawQuestLists(); };

            UIQuestListRefresh.Hit += (s, e) => {
                UITimedQuestList.ClearRows();
                UIKillTaskQuestList.ClearRows();
                UIOnceQuestList.ClearRows();
                GetMyQuestsList(1);
            };

            UITimedQuestList.Click += (s, r, c) => { HandleRowClicked(UITimedQuestList, r, c); };
            UIKillTaskQuestList.Click += (s, r, c) => { HandleRowClicked(UIKillTaskQuestList, r, c); };
            UIOnceQuestList.Click += (s, r, c) => { HandleRowClicked(UIOnceQuestList, r, c); };

            UB.MainView.view.VisibleChanged += MainView_VisibleChanged;
            questRedrawTimer.Tick += QuestRedrawTimer_Tick;
            questRedrawTimer.Interval = 1000;

            UB.Core.CommandLineText += Core_CommandLineText;
            UB.Core.ChatBoxMessage += Current_ChatBoxMessage;
            if (AutoRequest) {
                if (UBHelper.Core.GameState != UBHelper.GameState.In_Game)
                    UB.Core.CharacterFilter.LoginComplete += CharacterFilter_LoginComplete;
                else
                    GetMyQuestsList(3);
            } else UIQuestListRefresh.Text = "Load";
        }

        private void Core_CommandLineText(object sender, ChatParserInterceptEventArgs e) {
            try {
                if (e.Text == "/myquests" || e.Text == "@myquests") {
                    GetMyQuestsList(1, false, false);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void CharacterFilter_LoginComplete(object sender, EventArgs e) {
            try {
                UB.Core.CharacterFilter.LoginComplete -= CharacterFilter_LoginComplete;
                GetMyQuestsList(3);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        private bool GettingQuests = false;
        private bool GotFirstQuest = false;
        private sbyte GetQuestTries = 0;
        private void GetMyQuestsList(sbyte tries = 1, bool doCommand=true, bool shouldEat=true) {
            if (GettingQuests) {
                if (doCommand) LogError("GetMyQuestsList called while it was already running");
                return;
            }
            questFlags.Clear();
            GetQuestTries = tries;
            GettingQuests = true;
            GotFirstQuest = false;
            ShouldEat = shouldEat;
            UB.Core.RenderFrame += Core_RenderFrame;
            lastHeartbeat = DateTime.UtcNow;
            UIQuestListRefresh.Text = "Refresh";
            UIQuestListRefresh.Visible = false;
            if (doCommand) RealGetMyQuestList();
        }
        private void RealGetMyQuestList() {
            if (GetQuestTries < 1) {
                LogError("GetMyQuestList failed too many times, retiring");
                GettingQuests = false;
                UB.Core.RenderFrame -= Core_RenderFrame;
                UIQuestListRefresh.Visible = true;
                ShouldEat = false;
                return;
            }
            try {
                UB.Core.Actions.InvokeChatParser("/myquests");
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public void Current_ChatBoxMessage(object sender, ChatTextInterceptEventArgs e) {
            try {
                if (e.Text.Equals("Quest list is empty.\n") || e.Text.Equals("The command \"myquests\" is not currently enabled on this server.\n")) {
                    GettingQuests = false;
                    UB.Core.RenderFrame -= Core_RenderFrame;
                    UIQuestListRefresh.Visible = true;
                    ShouldEat = false;
                    return;
                }

                if (QuestFlag.MyQuestRegex.IsMatch(e.Text)) {
                    e.Eat = ShouldEat;
                    GotFirstQuest = true;
                    var questFlag = QuestFlag.FromMyQuestsLine(e.Text);

                    if (questFlag != null) {
                        UpdateQuestFlag(questFlag);
                    }
                    lastHeartbeat = DateTime.UtcNow;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Core_RenderFrame(object sender, EventArgs e) {
            if (!GettingQuests) {
                UB.Core.RenderFrame -= Core_RenderFrame;
                return;
            }
            if (GotFirstQuest) {
                if (DateTime.UtcNow - lastHeartbeat > TimeSpan.FromSeconds(1)) {
                    GettingQuests = false;
                    UB.Core.RenderFrame -= Core_RenderFrame;
                    UIQuestListRefresh.Visible = true;
                    ShouldEat = false;

                    UpdateQuestDB();
                }
            } else {
                if (DateTime.UtcNow - lastHeartbeat > TimeSpan.FromSeconds(15)) {
                    LogError("Timeout (15s) getting quests");
                    GetQuestTries--;
                    lastHeartbeat = DateTime.UtcNow;
                    RealGetMyQuestList();
                }
            }
        }

        private void UpdateQuestDB() {
            var flags = new List<Lib.Models.QuestFlag>();
            foreach (var kv in this.questFlags) {
                var questFlag = kv.Value;
                var qf = new Lib.Models.QuestFlag() {
                    Key = questFlag.Key,
                    MaxSolves = questFlag.MaxSolves,
                    CompletedOn = questFlag.CompletedOn,
                    Description = questFlag.Description,
                    RepeatTime = (int)questFlag.RepeatTime.TotalSeconds,
                    Solves = questFlag.Solves,
                    Character = UB.Core.CharacterFilter.Name,
                    Server = UB.Core.CharacterFilter.Server
                };
                flags.Add(qf);
            }

            UB.Database.QuestFlags.Delete(
                LiteDB.Query.And(
                    LiteDB.Query.EQ("Character", UB.Core.CharacterFilter.Name),
                    LiteDB.Query.EQ("Server", UB.Core.CharacterFilter.Server)
                )
            );

            UB.Database.QuestFlags.InsertBulk(flags);
        }

        private void QuestRedrawTimer_Tick(object sender, EventArgs e) {
            try {
                if (UB.MainView.view.Visible) {
                    DrawQuestLists();
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void MainView_VisibleChanged(object sender, EventArgs e) {
            try {
                if (UB.MainView.view.Visible) {
                    questRedrawTimer.Start();
                }
                else {
                    questRedrawTimer.Stop();
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void HandleRowClicked(HudList uiList, int row, int col) {
            try {
                var key = ((HudStaticText)uiList[row][0]).Text;

                Logger.WriteToChat($"Quest Key: {key}");
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void ThinkQuestFlag(string key) {
            if (!questFlags.ContainsKey(key)) {
                Util.Think($"Quest: {key} has not been completed");
                return;
            }

            var questFlag = questFlags[key];

            switch (questFlag.FlagType) {
                case QuestFlagType.Once:
                    Util.Think($"Quest: {questFlag.Key} is completed");
                    return;

                case QuestFlagType.KillTask:
                    if (questFlag.Solves >= questFlag.MaxSolves) {
                        Util.Think($"Quest: {questFlag.Key} is completed");
                    }
                    else {
                        Util.Think($"Quest: {questFlag.Key} is at {questFlag.Solves} of {questFlag.MaxSolves}");
                    }
                    return;

                case QuestFlagType.Timed:
                    if (questFlag.IsReady()) {
                        Util.Think($"Quest: {questFlag.Key} is ready");
                    }
                    else {
                        Util.Think($"Quest: {questFlag.Key} is not ready ({questFlag.NextAvailable()})");
                    }
                    return;
            }
        }

        private void DrawQuestLists() {
            var flags = questFlags.Values.ToList();

            // it would be nice to just update in place
            var timedScrollPosition = UITimedQuestList.ScrollPosition;
            var killTakScrollPosition = UIKillTaskQuestList.ScrollPosition;
            var onceScrollPosition = UIOnceQuestList.ScrollPosition;

            UITimedQuestList.ClearRows();
            UIKillTaskQuestList.ClearRows();
            UIOnceQuestList.ClearRows();

            flags.Sort((p1, p2) => p1.CompareTo(p2));

            foreach (var questFlag in flags) {
                UpdateQuestFlag(questFlag);
            }

            UITimedQuestList.ScrollPosition = timedScrollPosition;
            UIKillTaskQuestList.ScrollPosition = killTakScrollPosition;
            UIOnceQuestList.ScrollPosition = onceScrollPosition;
        }

        private void UpdateQuestFlag(QuestFlag questFlag) {
            try {
                HudList UIList = GetUIListForFlagType(questFlag.FlagType);
                Regex filter = GetFilter();

                if (questFlags.ContainsKey(questFlag.Key)) {
                    questFlags[questFlag.Key] = questFlag;
                }
                else {
                    questFlags.Add(questFlag.Key, questFlag);
                }

                if (filter.IsMatch(questFlag.Key) || filter.IsMatch(questFlag.Name)) {
                    InsertQuestFlagRow(UIList, questFlag);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private Regex GetFilter() {
            Regex filter = null;

            try {
                if (!string.IsNullOrEmpty(UIQuestsListFilter.Text)) {
                    filter = new Regex(UIQuestsListFilter.Text, RegexOptions.IgnoreCase);
                }
            }
            finally {
                if (filter == null) {
                    filter = new Regex(".*");
                }
            }

            return filter;
        }

        private void InsertQuestFlagRow(HudList UIList, QuestFlag questFlag) {
            HudList.HudListRowAccessor row = UIList.AddRow();

            ((HudStaticText)row[0]).Text = questFlag.Key;
            ((HudStaticText)row[1]).Text = questFlag.Name;

            ((HudStaticText)row[2]).TextAlignment = VirindiViewService.WriteTextFormats.Right;
            ((HudStaticText)row[3]).TextAlignment = VirindiViewService.WriteTextFormats.Right;

            if (questFlag.FlagType == QuestFlagType.Timed) {
                ((HudStaticText)row[2]).Text = questFlag.NextAvailable();
                ((HudStaticText)row[3]).Text = questFlag.Solves.ToString();
            }
            else if (questFlag.FlagType == QuestFlagType.KillTask) {
                ((HudStaticText)row[2]).Text = questFlag.Solves.ToString();
                ((HudStaticText)row[3]).Text = questFlag.MaxSolves.ToString();
            }
        }

        private HudList GetUIListForFlagType(QuestFlagType flagType) {
            switch (flagType) {
                case QuestFlagType.Timed:
                    return UITimedQuestList;
                case QuestFlagType.Once:
                    return UIOnceQuestList;
                case QuestFlagType.KillTask:
                    return UIKillTaskQuestList;
            }

            return null;
        }

        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    UB.Core.CharacterFilter.LoginComplete -= CharacterFilter_LoginComplete;
                    UB.Core.ChatBoxMessage -= Current_ChatBoxMessage;
                    UB.Core.RenderFrame -= Core_RenderFrame;
                    UB.Core.CommandLineText -= Core_CommandLineText;
                    if (questRedrawTimer != null) {
                        questRedrawTimer.Stop();
                        questRedrawTimer.Dispose();
                    }

                    base.Dispose(disposing);
                }
                disposedValue = true;
            }
        }
    }
}
