using System;
using Decal.Adapter;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using VirindiViewService.Controls;
using System.Data;
using System.Linq;
using System.Xml.Schema;
using System.Xml;
using System.IO;
using System.Windows.Forms;
using UtilityBelt.Lib.Quests;

namespace UtilityBelt.Tools {
    class QuestTracker : IDisposable {
        DateTime lastMyQuestsRequest = DateTime.MinValue;

        HudTextBox UIQuestsListFilter { get; set; }
        HudTabView UIQuestListNotebook { get; set; }
        HudButton UIQuestListRefresh { get; set; }

        HudList UITimedQuestList { get; set; }
        HudList UIKillTaskQuestList { get; set; }
        HudList UIOnceQuestList { get; set; }

        Timer questRedrawTimer = new Timer();
        Dictionary<string, QuestFlag> questFlags = new Dictionary<string, QuestFlag>();

        public QuestTracker() {
            UIQuestsListFilter = (HudTextBox)Globals.MainView.view["QuestsListFilter"];
            UIQuestListNotebook = (HudTabView)Globals.MainView.view["QuestListNotebook"];
            UIQuestListRefresh = (HudButton)Globals.MainView.view["QuestListRefresh"];

            UITimedQuestList = (HudList)Globals.MainView.view["TimedQuestList"];
            UIKillTaskQuestList = (HudList)Globals.MainView.view["KillTaskQuestList"];
            UIOnceQuestList = (HudList)Globals.MainView.view["OnceQuestList"];

            UIQuestsListFilter.Change += (s, e) => { DrawQuestLists(); };

            UIQuestListRefresh.Hit += (s, e) => { GetMyQuestsList(); };

            UITimedQuestList.Click += (s, r, c) => { HandleRowClicked(UITimedQuestList, r, c); };
            UIKillTaskQuestList.Click += (s, r, c) => { HandleRowClicked(UIKillTaskQuestList, r, c); };
            UIOnceQuestList.Click += (s, r, c) => { HandleRowClicked(UIOnceQuestList, r, c); };

            Globals.MainView.view.VisibleChanged += MainView_VisibleChanged;
            questRedrawTimer.Tick += QuestRedrawTimer_Tick;
            questRedrawTimer.Interval = 1000;

            Globals.Core.ChatBoxMessage += Current_ChatBoxMessage;
            Globals.Core.CommandLineText += Core_CommandLineText;
            
            GetMyQuestsList();
        }

        private void Core_CommandLineText(object sender, ChatParserInterceptEventArgs e) {
            try {
                if (e.Text.StartsWith("/ub quests check ")) {
                    e.Eat = true;
                    var searchText = e.Text.Replace("/ub quests check ", "");
                    var searchRe = new Regex(searchText, RegexOptions.IgnoreCase);
                    var thoughts = 0;

                    var keys = new List<string>(questFlags.Keys);
                    keys.AddRange(new List<string>(QuestFlag.FriendlyNamesLookup.Keys));
                    keys = new List<string>(keys.Distinct());

                    foreach (var key in keys) {
                        if (searchRe.IsMatch(key)) {
                            ThinkQuestFlag(key);
                            thoughts++;

                            if (thoughts >= 5) {
                                Util.WriteToChat("Quests output has been truncated to the first 5 matching quest flags.");
                                return;
                            }
                        }
                    }

                    if (thoughts == 0) {
                        Util.Think($"Quest: {searchText} has not been completed");
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void QuestRedrawTimer_Tick(object sender, EventArgs e) {
            try {
                if (Globals.MainView.view.Visible) {
                    DrawQuestLists();
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void MainView_VisibleChanged(object sender, EventArgs e) {
            try {
                if (Globals.MainView.view.Visible) {
                    questRedrawTimer.Start();
                }
                else {
                    questRedrawTimer.Stop();
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public void Current_ChatBoxMessage(object sender, ChatTextInterceptEventArgs e) {
            try {
                if (ShouldEatQuests()) {
                    e.Eat = true;
                }

                if (QuestFlag.MyQuestRegex.IsMatch(e.Text)) {
                    var questFlag = QuestFlag.FromMyQuestsLine(e.Text);

                    if (questFlag != null) {
                        UpdateQuestFlag(questFlag);
                    }
                }
            } catch (Exception ex) { Logger.LogException(ex); }
        }

        private void HandleRowClicked(HudList uiList, int row, int col) {
            try {
                var key = ((HudStaticText)uiList[row][0]).Text;

                Util.WriteToChat($"Quest Key: {key}");
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

        private bool ShouldEatQuests() {
            // assuming we get a response back in <3 seconds
            return DateTime.UtcNow - lastMyQuestsRequest < TimeSpan.FromSeconds(3);
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

        private void GetMyQuestsList() {
            try {
                lastMyQuestsRequest = DateTime.UtcNow;
                Globals.Core.Actions.InvokeChatParser("/myquests");
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private bool disposed = false;
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    Globals.Core.ChatBoxMessage -= Current_ChatBoxMessage;
                    Globals.Core.CommandLineText -= Core_CommandLineText;

                    if (questRedrawTimer != null) {
                        questRedrawTimer.Stop();
                        questRedrawTimer.Dispose();
                    }
                }
                disposed = true;
            }
        }
    }
}
