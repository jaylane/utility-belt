using System;
using Decal.Adapter;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using VirindiViewService.Controls;
using System.Data;
using System.Linq;
using System.Threading;

namespace UtilityBelt.Tools {
    class QuestTracker : IDisposable {

        private bool disposed = false;
        private bool shouldEatQuests = false;
        private bool buttonClicked = false;

        HudList UIMyQuestList { get; set; }
        HudButton UIPopulateQuestList { get; set; }
        HudList UIMyKillTaskList { get; set; }
        HudButton PopulateKillTaskList { get; set; }
        HudList UIMyOneTimeList { get; set; }
        HudButton PopulateOneTimeList { get; set; }
        public HudTextBox UIQuestsListFilter { get; set; }
        public HudTextBox UIQuestsKTListFilter { get; set; }
        public HudTextBox UIQuestsOTListFilter { get; set; }
        private System.Windows.Forms.Timer questTimer = new System.Windows.Forms.Timer();
        //private int counter = 1;


        public QuestTracker() {
            questTimer.Tick += new EventHandler(questTimer_Tick);
            questTimer.Interval = 1000;
            questTimer.Start();

            Globals.Core.ChatBoxMessage += new EventHandler<ChatTextInterceptEventArgs>(Current_ChatBoxMessage);

            UIMyQuestList = Globals.MainView.view != null ? (HudList)Globals.MainView.view["myQuestList"] : new HudList();
            UIMyKillTaskList = Globals.MainView.view != null ? (HudList)Globals.MainView.view["myKillTaskList"] : new HudList();
            UIMyOneTimeList = Globals.MainView.view != null ? (HudList)Globals.MainView.view["myOneTimeList"] : new HudList();

            UIPopulateQuestList = Globals.MainView.view != null ? (HudButton)Globals.MainView.view["PopulateQuestList"] : new HudButton();
            UIPopulateQuestList.Hit += PopulateQuestList_Click;

            PopulateKillTaskList = Globals.MainView.view != null ? (HudButton)Globals.MainView.view["PopulateKillTaskList"] : new HudButton();
            PopulateKillTaskList.Hit += PopulateKillTaskList_Click;

            PopulateOneTimeList = Globals.MainView.view != null ? (HudButton)Globals.MainView.view["PopulateOneTimeList"] : new HudButton();
            PopulateOneTimeList.Hit += PopulateOneTimeList_Click;

            UIQuestsListFilter = (HudTextBox)Globals.MainView.view["QuestsListFilter"];
            UIQuestsListFilter.Change += UIQuestsListFilter_Change;

            UIQuestsKTListFilter = (HudTextBox)Globals.MainView.view["QuestsKTListFilter"];
            UIQuestsKTListFilter.Change += UIQuestsKTListFilter_Change;

            UIQuestsOTListFilter = (HudTextBox)Globals.MainView.view["QuestsOTListFilter"];
            UIQuestsOTListFilter.Change += UIQuestsOTListFilter_Change;
            
            Util.LoadQuestLookupXML();
        }

        public string GetFriendlyTimeDifference(TimeSpan difference) {
            string output = "";

            if (difference.TotalDays > 0) output += difference.Days.ToString() + "d ";
            if (difference.TotalHours > 0) output += difference.Hours.ToString() + "h ";
            if (difference.TotalMinutes > 0) output += difference.Minutes.ToString() + "m ";
            if (difference.TotalSeconds > 0) output += difference.Seconds.ToString() + "s ";

            return output.Trim();
        }

        public string GetFriendlyTimeDifference(long difference) {
            return GetFriendlyTimeDifference(TimeSpan.FromSeconds(difference));
        }

        public long todayEpoch() {
            long todayEpoch = (long)((DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds);
            return todayEpoch;
        }

        public long convertToLong (string input) {
            long output = Convert.ToInt64(input.ToString());
            return output;
        }


        //DataTable questDataTable = new DataTable();
        private Dictionary<string, string> questList = new Dictionary<string, string>();
        private Dictionary<string, string> killTaskList = new Dictionary<string, string>();
        private static readonly Regex myQuestRegex = new Regex(@"(?<questKey>\S+) \- (?<solveCount>\d+) solves \((?<completedOn>\d{0,11})\)""?((?<questDescription>.*)"" (?<maxCompletions>.*) (?<repeatTime>\d{0,6}))?.*$");
        private static readonly Regex killTaskRegex = new Regex(@"killtask|killcount|slayerquest|totalgolem.*dead");
        DataTable questDataTable = new DataTable();

        public void Current_ChatBoxMessage(object sender, ChatTextInterceptEventArgs e) {
            try {
                Match match = myQuestRegex.Match(e.Text);
                if (match.Success) {
                    if (questDataTable.Rows.Count == 0) {
                        questDataTable.Columns.Add("questKey");
                        questDataTable.Columns.Add("friendlyName");
                        questDataTable.Columns.Add("solveCount");
                        questDataTable.Columns.Add("questDescription");
                        questDataTable.Columns.Add("maxCompletions");
                        questDataTable.Columns.Add("sortTime");
                        questDataTable.Columns.Add("repeatTime");
                        questDataTable.Columns.Add("questType");
                    }

                    string questKey = match.Groups["questKey"].Value;
                    string friendlyName = Util.GetFriendlyQuestName(questKey);
                    string solveCount = match.Groups["solveCount"].Value;
                    string questDescription = match.Groups["questDescription"].Value;
                    string maxCompletions = match.Groups["maxCompletions"].Value;
                    long availableOnEpoch = 0;
                    string questType = "";
                    long sortTime;
                    
                    bool dupe = false;
                    if (Int32.TryParse(match.Groups["completedOn"].Value, out int completedOn)) {
                        availableOnEpoch = completedOn;
                    }
                    if (Int32.TryParse(match.Groups["repeatTime"].Value, out int repeatTime)) {
                        availableOnEpoch += repeatTime;
                    }

                    Match matchKillTask = killTaskRegex.Match(questKey);
                    if (matchKillTask.Success) {
                        questType = "killTask";
                    }
                    if (string.IsNullOrEmpty(maxCompletions) || maxCompletions == "1"){
                        questType = "oneTimeQuest";
                    }

                    long nowEpoch = todayEpoch();

                    if (nowEpoch > availableOnEpoch) {
                        sortTime = 0;
                    } else {
                        sortTime = availableOnEpoch - nowEpoch;
                    }

                    DataRow newDTRow = questDataTable.NewRow();
                    foreach (DataRow row in questDataTable.Rows) {
                        if (row["questKey"].ToString() == questKey) {
                            dupe = true;
                            row["friendlyName"] = friendlyName;
                            row["solveCount"] = solveCount;
                            row["questDescription"] = questDescription;
                            row["maxCompletions"] = maxCompletions;
                            row["questType"] = questType;
                            row["sortTime"] = sortTime;
                            if (questType != "killTask" && questType != "oneTimeQuest") {
                                newDTRow["repeatTime"] = availableOnEpoch;
                            } else {
                                newDTRow["repeatTime"] = null;
                            }
                        }
                    }

                    if (dupe != true) {
                        newDTRow["questKey"] = questKey;
                        newDTRow["friendlyName"] = friendlyName;
                        newDTRow["solveCount"] = solveCount;
                        newDTRow["questDescription"] = questDescription;
                        newDTRow["maxCompletions"] = maxCompletions;
                        newDTRow["questType"] = questType;
                        newDTRow["sortTime"] = sortTime;
                        if (questType != "killTask" && questType != "oneTimeQuest") {
                            newDTRow["repeatTime"] = availableOnEpoch;
                        } else {
                            newDTRow["repeatTime"] = null;
                        }
                        questDataTable.Rows.Add(newDTRow);
                    }

                    if (shouldEatQuests) {
                        e.Eat = true;
                    }
                }
            } catch (Exception ex) { Logger.LogException(ex); }
        }


        public void PopulateQuests() {
            try {
                buttonClicked = true;
                shouldEatQuests = true;
                UIMyQuestList.ClearRows();
                UIMyKillTaskList.ClearRows();
                UIMyOneTimeList.ClearRows();

                Globals.Core.Actions.InvokeChatParser("/myquests");

                System.Threading.Timer timer = null;
                timer = new System.Threading.Timer((obj) => {
                    try {
                        shouldEatQuests = false;

                        if (questDataTable.Rows.Count >= 1) {
                            DataView dv = questDataTable.DefaultView;
                            dv.Sort = "sortTime DESC, friendlyName ASC";
                            DataTable sortedQuestDataTable = dv.ToTable();

                            foreach (DataRow row in sortedQuestDataTable.Rows) {
                                //string QuestName = Util.GetFriendlyQuestName(row["questKey"].ToString());
                                string questTimerstr;
                                string repeatTimeStr = row["repeatTime"].ToString();
                                if (!string.IsNullOrEmpty(repeatTimeStr)) {
                                    long repeatTime = long.Parse(repeatTimeStr);
                                    long nowEpoch = todayEpoch();

                                    if (nowEpoch > repeatTime) {
                                            questTimerstr = "Ready";
                                    } else {
                                        questTimerstr = GetFriendlyTimeDifference(repeatTime - nowEpoch);
                                    }
                                } else {
                                    questTimerstr = "";
                                }
                                

                                    if (row["questType"].ToString() == "killTask") {
                                    HudList.HudListRowAccessor newKTRow = UIMyKillTaskList.AddRow();
                                    ((HudStaticText)newKTRow[0]).Text = row["friendlyName"].ToString();
                                    ((HudStaticText)newKTRow[1]).TextAlignment = VirindiViewService.WriteTextFormats.Right;
                                    ((HudStaticText)newKTRow[1]).Text = row["solveCount"].ToString();
                                    ((HudStaticText)newKTRow[2]).TextAlignment = VirindiViewService.WriteTextFormats.Right;
                                    ((HudStaticText)newKTRow[2]).Text = row["maxCompletions"].ToString();

                                } else if (row["questType"].ToString() == "oneTimeQuest") {
                                    HudList.HudListRowAccessor newOTRow = UIMyOneTimeList.AddRow();
                                    ((HudStaticText)newOTRow[0]).Text = row["friendlyName"].ToString();
                                } else {
                                    HudList.HudListRowAccessor newQLRow = UIMyQuestList.AddRow();
                                    ((HudStaticText)newQLRow[0]).Text = row["friendlyName"].ToString();
                                    ((HudStaticText)newQLRow[1]).TextAlignment = VirindiViewService.WriteTextFormats.Right;
                                    ((HudStaticText)newQLRow[1]).Text = questTimerstr;
                                    ((HudStaticText)newQLRow[2]).TextAlignment = VirindiViewService.WriteTextFormats.Right;
                                    ((HudStaticText)newQLRow[2]).Text = row["solveCount"].ToString();
                                    ((HudStaticText)newQLRow[3]).Text = row["questKey"].ToString();
                                }
                            }
                        }
                        buttonClicked = false;
                        timer.Dispose();
                    } catch (Exception ex) { Logger.LogException(ex); }

                },
                            null, 500, System.Threading.Timeout.Infinite);
            } catch (Exception ex) { Logger.LogException(ex); }
        }

        public void RedrawQuests() { 
            UIMyQuestList.ClearRows();
            DataView dv = questDataTable.DefaultView;
            dv.Sort = "sortTime DESC, friendlyName ASC";
            DataTable sortedQuestDataTable = dv.ToTable();

            var filterText = UIQuestsListFilter.Text;
            Regex searchRegex = new Regex(filterText, RegexOptions.IgnoreCase);
            foreach (DataRow row in sortedQuestDataTable.Rows) {
                string QuestKey = row["questKey"].ToString();
                string QuestName = Util.GetFriendlyQuestName(row["questKey"].ToString());
                string QuestType = Util.GetFriendlyQuestName(row["questType"].ToString());
                
                if (!string.IsNullOrEmpty(filterText) && ((!searchRegex.IsMatch(QuestName)) && !searchRegex.IsMatch(QuestKey))) {
                    continue;
                }
                string questTimerstr;
                string repeatTimeStr = row["repeatTime"].ToString();
                if (!string.IsNullOrEmpty(repeatTimeStr)) {
                    long repeatTime = long.Parse(repeatTimeStr);
                    long nowEpoch = todayEpoch();
                    if (nowEpoch > repeatTime) {
                        questTimerstr = "Ready";
                    } else {
                        questTimerstr = GetFriendlyTimeDifference(repeatTime - nowEpoch);
                    }
                } else {
                    questTimerstr = "";
                }
                if (QuestType != "killTask" && QuestType != "oneTimeQuest") { 
                    HudList.HudListRowAccessor newQLRow = UIMyQuestList.AddRow();
                    ((HudStaticText)newQLRow[0]).Text = row["friendlyName"].ToString();
                    ((HudStaticText)newQLRow[1]).TextAlignment = VirindiViewService.WriteTextFormats.Right;
                    ((HudStaticText)newQLRow[1]).Text = questTimerstr;
                    ((HudStaticText)newQLRow[2]).TextAlignment = VirindiViewService.WriteTextFormats.Right;
                    ((HudStaticText)newQLRow[2]).Text = row["solveCount"].ToString();
                    ((HudStaticText)newQLRow[3]).Text = row["questKey"].ToString();
                }
            }
        }

        public void RedrawOTQuests() {
            UIMyOneTimeList.ClearRows();
            DataView dv = questDataTable.DefaultView;
            dv.Sort = "friendlyName ASC";
            DataTable sortedQuestDataTable = dv.ToTable();

            var filterText = UIQuestsOTListFilter.Text;
            Regex searchRegex = new Regex(filterText, RegexOptions.IgnoreCase);

            foreach (DataRow row in sortedQuestDataTable.Rows) {
                string QuestKey = row["questKey"].ToString();
                string QuestName = Util.GetFriendlyQuestName(row["questKey"].ToString());
                if (!string.IsNullOrEmpty(filterText) && ((!searchRegex.IsMatch(QuestName)) && !searchRegex.IsMatch(QuestKey))) {
                    continue;
                }

                 if (row["questType"].ToString() == "oneTimeQuest") {
                    HudList.HudListRowAccessor newOTRow = UIMyOneTimeList.AddRow();
                    ((HudStaticText)newOTRow[0]).Text = row["friendlyName"].ToString();
                } 
            }
        }

        public void RedrawKTQuests() {
            UIMyKillTaskList.ClearRows();
            DataView dv = questDataTable.DefaultView;
            dv.Sort = "friendlyName ASC";
            DataTable sortedQuestDataTable = dv.ToTable();

            var filterText = UIQuestsKTListFilter.Text;
            Regex searchRegex = new Regex(filterText, RegexOptions.IgnoreCase);

            foreach (DataRow row in sortedQuestDataTable.Rows) {
                string QuestKey = row["questKey"].ToString();
                string QuestName = Util.GetFriendlyQuestName(row["questKey"].ToString());
                if (!string.IsNullOrEmpty(filterText) && ((!searchRegex.IsMatch(QuestName)) && !searchRegex.IsMatch(QuestKey))) {
                    continue;
                }

                if (row["questType"].ToString() == "killTask") {
                    HudList.HudListRowAccessor newKTRow = UIMyKillTaskList.AddRow();
                    ((HudStaticText)newKTRow[0]).Text = row["friendlyName"].ToString();
                    ((HudStaticText)newKTRow[1]).TextAlignment = VirindiViewService.WriteTextFormats.Right;
                    ((HudStaticText)newKTRow[1]).Text = row["solveCount"].ToString();
                    ((HudStaticText)newKTRow[2]).TextAlignment = VirindiViewService.WriteTextFormats.Right;
                    ((HudStaticText)newKTRow[2]).Text = row["maxCompletions"].ToString();
                }
            }
        }

        private void questTimer_Tick(object sender, EventArgs e) {
            if (Globals.MainView.view.Visible) {
                for (int i = 0; i < UIMyQuestList.RowCount; i++) {
                    // add a new invisible column to quests lists that stores the quest key
                    var questKey = ((HudStaticText)UIMyQuestList[i][3]).Text;
                    var repeatTime = ((HudStaticText)UIMyQuestList[i][1]).Text;
                    var questData = questDataTable.Select(string.Format("questKey = '{0}'", questKey)).First();
                    Regex timeRemainingRegex = new Regex(@"\d+d \d+h \d+m \d+s");
                    Match match = timeRemainingRegex.Match(repeatTime);
                    if (match.Success) {
                        long nowEpoch = todayEpoch();
                        long availableOnEpoch = Convert.ToInt64(questData["repeatTime"].ToString());

                        ((HudStaticText)UIMyQuestList[i][1]).TextAlignment = VirindiViewService.WriteTextFormats.Right;
                        ((HudStaticText)UIMyQuestList[i][1]).Text = GetFriendlyTimeDifference(availableOnEpoch - nowEpoch).ToString();
                    }
                }
            }
        }

        private void UIQuestsListFilter_Change(object sender, EventArgs e) {
            try {
                RedrawQuests();

            } catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UIQuestsKTListFilter_Change(object sender, EventArgs e) {
            try {
                RedrawKTQuests();
            } catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UIQuestsOTListFilter_Change(object sender, EventArgs e) {
            try {
                RedrawOTQuests();
            } catch (Exception ex) { Logger.LogException(ex); }
        }


        private void PopulateQuestList_Click(object sender, EventArgs e) {
            if (buttonClicked == false) {
                PopulateQuests();
            }
        }
        private void PopulateKillTaskList_Click(object sender, EventArgs e) {
            if (buttonClicked == false) {
                PopulateQuests();
            }
        }
        private void PopulateOneTimeList_Click(object sender, EventArgs e) {
            if (buttonClicked == false) {
                PopulateQuests();
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    Globals.Core.ChatBoxMessage -= new EventHandler<ChatTextInterceptEventArgs>(Current_ChatBoxMessage);
                    questTimer.Tick -= new EventHandler(questTimer_Tick);
                }
                disposed = true;
            }
        }
    }
}
