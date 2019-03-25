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
        //HudStaticText UIQuest { get; set; }
        //HudStaticText UIrepeatTimeTest { get; set; }
        //HudStaticText UISsolveCountTest { get; set; }
        HudButton UIPopulateQuestList { get; set; }
        HudList UIMyKillTaskList { get; set; }
        //HudStaticText UIQuestName { get; set; }
        //HudStaticText UIMaxCompletionsTest { get; set; }
        HudButton PopulateKillTaskList { get; set; }
        HudList UIMyOneTimeList { get; set; }
        HudButton PopulateOneTimeList { get; set; }
        public HudTextBox UIQuestsListFilter { get; set; }
        public HudTextBox UIQuestsKTListFilter { get; set; }
        public HudTextBox UIQuestsOTListFilter { get; set; }
        public static Dictionary<string, string> questKeyLookup = new Dictionary<string, string>();

        public QuestTracker() {
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

            return output;
        }

        public string GetFriendlyTimeDifference(long difference) {
            return GetFriendlyTimeDifference(TimeSpan.FromSeconds(difference));
        }

        public static string GetFriendlyQuestName(string questKey) {
            if (questKeyLookup.Keys.Contains(questKey)) {
                return questKeyLookup[questKey];
            }
            return questKey;
        }


        public void CreateDataTable() {
            questDataTable.Columns.Add("questKeyTest");
            questDataTable.Columns.Add("solveCountTest");
            //questDataTable.Columns.Add("completedOnTest");
            questDataTable.Columns.Add("questDescriptionTest");
            questDataTable.Columns.Add("maxCompletionsTest");
            questDataTable.Columns.Add("repeatTimeTest");
            questDataTable.Columns.Add("questType");
        }

        //DataTable questDataTable = new DataTable();
        private Dictionary<string, string> questList = new Dictionary<string, string>();
        private Dictionary<string, string> killTaskList = new Dictionary<string, string>();
        private static readonly Regex myQuestRegex = new Regex(@"(?<questKey>\S+) \- (?<solveCount>\d+) solves \((?<completedOn>\d{0,11})\)""?((?<questDescription>.*)"" (?<maxCompletions>.*) (?<repeatTime>\d{0,6}))?.*$");
        //private static readonly Regex myQuestRegex = new Regex(@"(?<questKey>\S+) \- (?<solveCount>\d+) solves? \((?<completedOn>\d{0,11})\)((?<questDescription>.*) -?\d+ (?<repeatTime>\d+))?.*$");
        private static readonly Regex killTaskRegex = new Regex(@"killtask|killcount|slayerquest|totalgolem.*dead");
        DataTable questDataTable = new DataTable();

        public void Current_ChatBoxMessage(object sender, ChatTextInterceptEventArgs e) {
            try {
                Match match = myQuestRegex.Match(e.Text);

                if (match.Success) {

                    if (questDataTable.Rows.Count == 0) {
                        //Util.WriteToChat("does not exit");
                        CreateDataTable();
                    } else {
                        //Util.WriteToChat("exists");
                    }

                    string questKey = match.Groups["questKey"].Value;
                    string solveCount = match.Groups["solveCount"].Value;
                    //string completedOn = match.Groups["completedOn"].Value;
                    string questDescription = match.Groups["questDescription"].Value;
                    string maxCompletions = match.Groups["maxCompletions"].Value;
                    //string repeatTime = match.Groups["repeatTime"].Value;
                    long availableOnEpoch = 0;
                    string questTimerstr = "";
                    string questType = "";
                    long todayEpoch = (long)((DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds);
                    bool dupe = false;
                    if (Int32.TryParse(match.Groups["completedOn"].Value, out int completedOn)) {
                        availableOnEpoch = completedOn;
                    }
                    if (Int32.TryParse(match.Groups["repeatTime"].Value, out int repeatTime)) {
                        availableOnEpoch += repeatTime;
                    }

                    if (todayEpoch > availableOnEpoch) {
                        questTimerstr = "Ready";
                    } else {
                        questTimerstr = GetFriendlyTimeDifference(availableOnEpoch - todayEpoch);
                    }


                    Match matchKillTask = killTaskRegex.Match(questKey);

                    if (matchKillTask.Success) {
                        //Util.WriteToChat("test");
                        questType = "killTask";
                    }
                    if (string.IsNullOrEmpty(maxCompletions) || maxCompletions == "1"){
                        questType = "oneTimeQuest";
                    }


                    DataRow newDTRow = questDataTable.NewRow();
                    foreach (DataRow row in questDataTable.Rows) {
                        if (row["questKeyTest"].ToString() == questKey) {
                            // Util.WriteToChat(questKey + " already exists");
                            dupe = true;
                            // Util.WriteToChat(questKey + ": duplicate");
                            row["solveCountTest"] = solveCount;
                            //newDTRow["completedOnTest"] = completedOn;
                            if (questDescription == "") {
                                row["questDescriptionTest"] = questKey;
                            } else {
                                row["questDescriptionTest"] = questDescription;
                            }

                            row["maxCompletionsTest"] = maxCompletions;
                            row["repeatTimeTest"] = questTimerstr;
                            row["questType"] = questType;
                        }
                    }


                    if (dupe == true) {
                        //Util.WriteToChat(questKey + ": duplicate");
                        //newDTRow["solveCountTest"] = solveCount;
                        ////newDTRow["completedOnTest"] = completedOn;
                        //newDTRow["questDescriptionTest"] = questDescription;
                        //newDTRow["maxCompletionsTest"] = maxCompletions;
                        //newDTRow["repeatTimeTest"] = questTimerstr;
                        //newDTRow["questType"] = questType;
                    } else {

                        //if(row["questKeyTest"])
                        newDTRow["questKeyTest"] = questKey;
                        newDTRow["solveCountTest"] = solveCount;
                        //newDTRow["completedOnTest"] = completedOn;
                        if (questDescription == "") {
                            newDTRow["questDescriptionTest"] = questKey;
                        } else {
                            newDTRow["questDescriptionTest"] = questDescription;
                        }
                        newDTRow["maxCompletionsTest"] = maxCompletions;
                        newDTRow["repeatTimeTest"] = questTimerstr;
                        newDTRow["questType"] = questType;
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


                HudList.HudListRowAccessor newHeaderKTRow = UIMyKillTaskList.AddRow();
                ((HudStaticText)newHeaderKTRow[0]).Text = "QuestName";
                ((HudStaticText)newHeaderKTRow[1]).Text = "solveCount";
                ((HudStaticText)newHeaderKTRow[2]).Text = "maxCompletions";


                HudList.HudListRowAccessor newHeaderOTRow = UIMyOneTimeList.AddRow();
                ((HudStaticText)newHeaderOTRow[0]).Text = "QuestName";

                HudList.HudListRowAccessor newHeaderQLRow = UIMyQuestList.AddRow();
                ((HudStaticText)newHeaderQLRow[0]).Text = "QuestName";
                ((HudStaticText)newHeaderQLRow[1]).Text = "repeatTime";
                ((HudStaticText)newHeaderQLRow[2]).Text = "solveCount";


                System.Threading.Timer timer = null;
                timer = new System.Threading.Timer((obj) => {
                    try {
                        shouldEatQuests = false;

                        if (questDataTable.Rows.Count >= 1) {
                            DataView dv = questDataTable.DefaultView;
                            dv.Sort = "repeatTimeTest ASC, questDescriptionTest ASC";
                            DataTable sortedQuestDataTable = dv.ToTable();


                            foreach (DataRow row in sortedQuestDataTable.Rows) {
                                string QuestName = Util.GetFriendlyQuestName(row["questKeyTest"].ToString());
                                //Util.WriteToChat(row["questKeyTest"].ToString());
                                //Util.WriteToChat(Util.GetFriendlyQuestName(row["questKeyTest"].ToString()));
                                //string QuestName = row["questDescriptionTest"].ToString();

                                if (row["questType"].ToString() == "killTask") {
                                    HudList.HudListRowAccessor newKTRow = UIMyKillTaskList.AddRow();
                                    ((HudStaticText)newKTRow[0]).Text = QuestName;
                                    ((HudStaticText)newKTRow[1]).Text = row["solveCountTest"].ToString();
                                    ((HudStaticText)newKTRow[2]).Text = row["maxCompletionsTest"].ToString();

                                } else if (row["questType"].ToString() == "oneTimeQuest") {
                                    HudList.HudListRowAccessor newOTRow = UIMyOneTimeList.AddRow();
                                    ((HudStaticText)newOTRow[0]).Text = QuestName;
                                } else {
                                    HudList.HudListRowAccessor newQLRow = UIMyQuestList.AddRow();
                                    ((HudStaticText)newQLRow[0]).Text = QuestName;
                                    ((HudStaticText)newQLRow[1]).Text = row["repeatTimeTest"].ToString();
                                    ((HudStaticText)newQLRow[2]).Text = row["solveCountTest"].ToString();
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
            dv.Sort = "repeatTimeTest ASC, questDescriptionTest ASC";
            DataTable sortedQuestDataTable = dv.ToTable();

            var filterText = UIQuestsListFilter.Text;
            Regex searchRegex = new Regex(filterText, RegexOptions.IgnoreCase);

            foreach (DataRow row in sortedQuestDataTable.Rows) {

                string QuestName = Util.GetFriendlyQuestName(row["questKeyTest"].ToString());
                if (!string.IsNullOrEmpty(filterText) && !searchRegex.IsMatch(QuestName)) {
                    continue;
                }

                

                if (row["questType"].ToString() == "killTask") {
                    HudList.HudListRowAccessor newKTRow = UIMyKillTaskList.AddRow();
                    ((HudStaticText)newKTRow[0]).Text = QuestName;
                    ((HudStaticText)newKTRow[1]).Text = row["solveCountTest"].ToString();
                    ((HudStaticText)newKTRow[2]).Text = row["maxCompletionsTest"].ToString();

                } else if (row["questType"].ToString() == "oneTimeQuest") {
                    HudList.HudListRowAccessor newOTRow = UIMyOneTimeList.AddRow();
                    ((HudStaticText)newOTRow[0]).Text = QuestName;
                } else {
                    HudList.HudListRowAccessor newQLRow = UIMyQuestList.AddRow();
                    ((HudStaticText)newQLRow[0]).Text = QuestName;
                    ((HudStaticText)newQLRow[1]).Text = row["repeatTimeTest"].ToString();
                    ((HudStaticText)newQLRow[2]).Text = row["solveCountTest"].ToString();
                }
            }
        }

        public void RedrawOTQuests() {
            UIMyOneTimeList.ClearRows();
            DataView dv = questDataTable.DefaultView;
            dv.Sort = "repeatTimeTest ASC";
            DataTable sortedQuestDataTable = dv.ToTable();

            var filterText = UIQuestsOTListFilter.Text;
            Regex searchRegex = new Regex(filterText, RegexOptions.IgnoreCase);

            foreach (DataRow row in sortedQuestDataTable.Rows) {
                string QuestName = Util.GetFriendlyQuestName(row["questKeyTest"].ToString());
                if (!string.IsNullOrEmpty(filterText) && !searchRegex.IsMatch(QuestName)) {
                    continue;
                }


                if (row["questType"].ToString() == "killTask") {
                    HudList.HudListRowAccessor newKTRow = UIMyKillTaskList.AddRow();
                    ((HudStaticText)newKTRow[0]).Text = QuestName;
                    ((HudStaticText)newKTRow[1]).Text = row["solveCountTest"].ToString();
                    ((HudStaticText)newKTRow[2]).Text = row["maxCompletionsTest"].ToString();

                } else if (row["questType"].ToString() == "oneTimeQuest") {
                    HudList.HudListRowAccessor newOTRow = UIMyOneTimeList.AddRow();
                    ((HudStaticText)newOTRow[0]).Text = QuestName;
                } else {
                    HudList.HudListRowAccessor newQLRow = UIMyQuestList.AddRow();
                    ((HudStaticText)newQLRow[0]).Text = QuestName;
                    ((HudStaticText)newQLRow[1]).Text = row["repeatTimeTest"].ToString();
                    ((HudStaticText)newQLRow[2]).Text = row["solveCountTest"].ToString();
                }
            }
        }

        public void RedrawKTQuests() {
            UIMyKillTaskList.ClearRows();
            DataView dv = questDataTable.DefaultView;
            dv.Sort = "repeatTimeTest ASC";
            DataTable sortedQuestDataTable = dv.ToTable();

            var filterText = UIQuestsKTListFilter.Text;
            Regex searchRegex = new Regex(filterText, RegexOptions.IgnoreCase);

            foreach (DataRow row in sortedQuestDataTable.Rows) {
                string QuestName = Util.GetFriendlyQuestName(row["questKeyTest"].ToString());
                if (!string.IsNullOrEmpty(filterText) && !searchRegex.IsMatch(QuestName)) {
                    continue;
                }


                if (row["questType"].ToString() == "killTask") {
                    HudList.HudListRowAccessor newKTRow = UIMyKillTaskList.AddRow();
                    ((HudStaticText)newKTRow[0]).Text = QuestName;
                    ((HudStaticText)newKTRow[1]).Text = row["solveCountTest"].ToString();
                    ((HudStaticText)newKTRow[2]).Text = row["maxCompletionsTest"].ToString();

                } else if (row["questType"].ToString() == "oneTimeQuest") {
                    HudList.HudListRowAccessor newOTRow = UIMyOneTimeList.AddRow();
                    ((HudStaticText)newOTRow[0]).Text = QuestName;
                } else {
                    HudList.HudListRowAccessor newQLRow = UIMyQuestList.AddRow();
                    ((HudStaticText)newQLRow[0]).Text = QuestName;
                    ((HudStaticText)newQLRow[1]).Text = row["repeatTimeTest"].ToString();
                    ((HudStaticText)newQLRow[2]).Text = row["solveCountTest"].ToString();
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
                }
                disposed = true;
            }
        }
    }
}
