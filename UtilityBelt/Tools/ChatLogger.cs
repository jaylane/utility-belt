using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using UtilityBelt.Lib;
using UtilityBelt.Lib.ChatLog;
using UtilityBelt.Lib.Constants;
using VirindiViewService.Controls;

namespace UtilityBelt.Tools {

    public class ChatLogRule {
        [JsonProperty(ItemConverterType = typeof(StringEnumConverter))]
        public IEnumerable<ChatMessageType> Types { get; set; }

        public string MessageFilter { get; set; }

        public override string ToString() {
            return JsonConvert.SerializeObject(this);
        }
    }

    [Name("ChatLogger")]
    public class ChatLogger : ToolBase {
        private string filter = null;
        private readonly ObservableCollection<ChatLog> chatLogList = new ObservableCollection<ChatLog>();
        private readonly ObservableCollection<ChatLog> filteredChatLogList = new ObservableCollection<ChatLog>();
        private readonly Timer debounceTimer;
        private readonly ChatLogWriter writer = new ChatLogWriter();
        private readonly Dictionary<int, ChatMessageType> typeTable = new Dictionary<int, ChatMessageType>();
        private readonly List<ChatMessageType> currentSelectedTypes = new List<ChatMessageType>();
        private int? editingRow = null;

        HudTextBox UIChatLogFilter;
        HudList UIChatLogLogList;
        HudButton UIChatLogClear;
        HudCheckBox UIChatLogSaveToFile;
        HudTextBox UIChatLogMessageFilter;
        HudList UIChatLogTypeList;
        HudButton UIChatLogRuleAddButton;
        HudList UIChatLogRuleList;

        #region Config
        [Summary("Save chat log to disk")]
        [DefaultValue(false)]
        public bool SaveToFile {
            get { return (bool)GetSetting("SaveToFile"); }
            set { UpdateSetting("SaveToFile", value); }
        }

        //[Summary("List of message types to log")]
        public ObservableCollection<ChatLogRule> Rules { get; set; } = new ObservableCollection<ChatLogRule>();

        private void Rules_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
            base.OnPropertyChanged(nameof(Rules));
        }
        #endregion

        internal string Filter {
            get => filter;
            set {
                if (filter != value) {
                    filter = value;

                    try {
                        // Test if regex is valid
                        if (!string.IsNullOrEmpty(Filter))
                            Regex.Match("", Filter);

                        UIChatLogLogList.ClearRows();
                        filteredChatLogList.Clear();
                        foreach (var message in chatLogList.Where(IsMatch)) {
                            filteredChatLogList.Add(message);
                        }
                    }
                    catch (ArgumentException) { } // Regex is invalid
                }
            }
        }

        public ChatLogger(UtilityBeltPlugin ub, string name) : base(ub, name) {
            try {
                UIChatLogFilter = UB.MainView.view != null ? (HudTextBox)UB.MainView.view["ChatLogFilterText"] : new HudTextBox();
                UIChatLogFilter.Change += UIChatLogFilter_Change;

                UIChatLogLogList = UB.MainView.view != null ? (HudList)UB.MainView.view["ChatLogLogList"] : new HudList();
                UIChatLogLogList.Click += UIChatLogLogList_Click;

                UIChatLogClear = UB.MainView.view != null ? (HudButton)UB.MainView.view["ChatLogClear"] : new HudButton();
                UIChatLogClear.Hit += UIChatLogClear_Hit;

                UIChatLogSaveToFile = UB.MainView.view != null ? (HudCheckBox)UB.MainView.view["ChatLogSaveToFile"] : new HudCheckBox();
                UIChatLogSaveToFile.Change += UIChatLogSaveToFile_Change;

                UIChatLogMessageFilter = UB.MainView.view != null ? (HudTextBox)UB.MainView.view["ChatLogMessageFilter"] : new HudTextBox();

                UIChatLogTypeList = UB.MainView.view != null ? (HudList)UB.MainView.view["ChatLogTypeList"] : new HudList();

                // Initialize chat types
                UIChatLogTypeList.ClearRows();
                int i = 0;
                foreach (ChatMessageType type in Enum.GetValues(typeof(ChatMessageType))) {
                    if (type.ShowInSettings()) {
                        typeTable[i++] = type;

                        var row = UIChatLogTypeList.AddRow();
                        ((HudCheckBox)row[0]).Checked = false;
                        ((HudCheckBox)row[0]).Change += (s, e) => {
                            if (((HudCheckBox)row[0]).Checked && !currentSelectedTypes.Contains(type)) {
                                currentSelectedTypes.Add(type);
                            }
                            else if (currentSelectedTypes.Contains(type)) {
                                currentSelectedTypes.Remove(type);
                            }
                        };
                        ((HudPictureBox)row[1]).Image = type.GetIcon();
                        ((HudStaticText)row[2]).Text = type.GetDescription();
                    }
                }

                UIChatLogRuleAddButton = UB.MainView.view != null ? (HudButton)UB.MainView.view["ChatLogRuleAddButton"] : new HudButton();
                UIChatLogRuleAddButton.Hit += UIChatLogRuleAddButton_Hit;

                UIChatLogRuleList = UB.MainView.view != null ? (HudList)UB.MainView.view["ChatLogRuleList"] : new HudList();
                UIChatLogRuleList.Click += UIChatLogRuleList_Click;

                UB.Core.ChatBoxMessage += Core_ChatBoxMessage;

                chatLogList.CollectionChanged += ChatLogList_CollectionChanged;
                filteredChatLogList.CollectionChanged += FilteredChatLogList_CollectionChanged;

                debounceTimer = new Timer(Timer_Tick);

                PropertyChanged += (s, e) => UpdateUI();
                Rules.CollectionChanged += Rules_CollectionChanged;

                UpdateUI();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UIChatLogRuleList_Click(object sender, int row, int col) {
            if (col == 0) {
                if (editingRow.HasValue && row == editingRow.Value) {
                    editingRow = null;
                    UIChatLogMessageFilter.Text = "";
                    UIChatLogRuleAddButton.Text = "Add Rule";
                }

                Rules.RemoveAt(row);
            }
            else if (col == 1) {
                Util.WriteToChat("[" + string.Join(", ", Rules[row].Types.Select(t => t.ToString()).ToArray()) + "]");
            }
            else if (col == 2) {
                if (editingRow.HasValue)
                    ((HudStaticText)UIChatLogRuleList[editingRow.Value][2]).TextColor = System.Drawing.Color.White;

                var tRow = UIChatLogRuleList[row];
                editingRow = row;
                UIChatLogMessageFilter.Text = Rules[row].MessageFilter;

                currentSelectedTypes.Clear();
                for (var i = 0; i < UIChatLogTypeList.RowCount; ++i) {
                    var r = UIChatLogTypeList[i];
                    if (Rules[row].Types.Contains(typeTable[i])) {
                        currentSelectedTypes.Add(typeTable[i]);
                        ((HudCheckBox)r[0]).Checked = true;
                    }
                    else {
                        ((HudCheckBox)r[0]).Checked = false;
                    }
                }

                ((HudStaticText)tRow[2]).TextColor = System.Drawing.Color.Red;
                UIChatLogRuleAddButton.Text = "Save Changes";
            }
        }

        private void UIChatLogRuleAddButton_Hit(object sender, EventArgs e) {
            if (currentSelectedTypes.Count <= 0) {
                WriteToChat("Please select one or more message types to filter.");
                return;
            }

            var filter = UIChatLogMessageFilter.Text;

            var isValid = true;
            if (!string.IsNullOrEmpty(filter)) {
                try {
                    Regex.Match("", filter);
                }
                catch (ArgumentException) {
                    isValid = false;
                }
            }

            if (!isValid) {
                LogError("Invalid filter regex.");
                return;
            }

            var newRule = new ChatLogRule() {
                MessageFilter = filter,
                Types = currentSelectedTypes.ToArray()
            };
            if (editingRow.HasValue) {
                Rules[editingRow.Value] = newRule;
                ((HudStaticText)UIChatLogRuleList[editingRow.Value][2]).TextColor = System.Drawing.Color.White;
                editingRow = null;
                UIChatLogRuleAddButton.Text = "Add Rule";
            }
            else {
                LogDebug($"New rule added: {newRule}");
                Rules.Add(newRule);
            }

            UIChatLogMessageFilter.Text = "";
            currentSelectedTypes.Clear();
            for (var i = 0; i < UIChatLogTypeList.RowCount; ++i) {
                var row = UIChatLogTypeList[i];
                ((HudCheckBox)row[0]).Checked = false;
            }
        }

        private void UIChatLogSaveToFile_Change(object sender, EventArgs e) {
            SaveToFile = UIChatLogSaveToFile.Checked;
        }

        private void UpdateUI() {
            UIChatLogSaveToFile.Checked = SaveToFile;

            UIChatLogRuleList.ClearRows();
            foreach (var rule in Rules) {
                var row = UIChatLogRuleList.AddRow();
                ((HudPictureBox)row[0]).Image = 0x60011F8;
                if (rule.Types.Count() == 1)
                    ((HudPictureBox)row[1]).Image = rule.Types.Single().GetIcon();
                else
                    ((HudPictureBox)row[1]).Image = 0x6006AE2;
                ((HudStaticText)row[2]).Text = rule.MessageFilter;
            }

            writer.Enabled = SaveToFile;
        }

        private void UIChatLogClear_Hit(object sender, EventArgs e) {
            chatLogList.Clear();
        }

        private void Timer_Tick(object _) {
            Filter = UIChatLogFilter.Text;
        }

        private void UIChatLogLogList_Click(object sender, int row, int col) {
            var clickedRow = filteredChatLogList[filteredChatLogList.Count - 1 - row];
            var type = clickedRow.Type.GetParent() ?? clickedRow.Type;
            WriteToChat($"{clickedRow.Timestamp.ToLocalTime():g}: [{type.GetDescription()}] {clickedRow.Message}");
        }

        private void ChatLogList_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset) {
                filteredChatLogList.Clear();
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add) {
                foreach (ChatLog item in e.NewItems) {
                    if (SaveToFile) {
                        writer.AddLog(item);
                    }

                    try {
                        if (IsMatch(item)) {
                            filteredChatLogList.Add(item);
                        }
                    }
                    catch (ArgumentException) { } // Regex is invalid
                }
            }
        }

        private bool IsMatch(ChatLog logItem) {
            if (string.IsNullOrEmpty(Filter))
                return true;

            try {
                if (Regex.IsMatch(logItem.Message, Filter))
                    return true;
            }
            catch (ArgumentException) { }

            return false;
        }

        private void FilteredChatLogList_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset) {
                UIChatLogLogList.ClearRows();
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add) {
                foreach (ChatLog item in e.NewItems) {
                    AddRowToChatLogTable(item);
                }
            }
        }

        private void AddRowToChatLogTable(ChatLog message) {
            var row = UIChatLogLogList.InsertRow(0);
            ((HudStaticText)row[0]).Text = message.Timestamp.ToLocalTime().ToString("t");
            ((HudPictureBox)row[1]).Image = (message.Type.GetParent() ?? message.Type).GetIcon();

            var msg = message.Message;
            var m = Regex.Match(msg, @"^(\[[^]]*\]|Your patron|Your vassal|Your follower)?\s*<Tell:IIDString:[^>]*>([^<]*)<\\Tell> (.*)$");
            if (m != null && m.Success) {
                msg = string.Join(" ", m.Groups.Cast<Group>().Skip(1).Select(c => c.Value).ToArray());
            }
            ((HudStaticText)row[2]).Text = msg;
            ((HudStaticText)row[2]).TextColor = message.Type.GetChatColor();
        }

        private void UIChatLogFilter_Change(object sender, EventArgs e) {
            debounceTimer.Change(500, Timeout.Infinite);
        }

        private void Core_ChatBoxMessage(object sender, Decal.Adapter.ChatTextInterceptEventArgs e) {
            if (Enum.IsDefined(typeof(ChatMessageType), (ChatMessageType)e.Color)) {
                var type = (ChatMessageType)Enum.ToObject(typeof(ChatMessageType), e.Color);
                var parentType = type.GetParent();
                if (Rules.Any(r => r.Types.Contains(parentType ?? type) && Regex.IsMatch(e.Text, r.MessageFilter))) {
                    chatLogList.Add(new ChatLog(type, e.Text.Trim()));
                }
            }
        }

        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    UB.Core.ChatBoxMessage -= Core_ChatBoxMessage;
                    chatLogList.CollectionChanged -= ChatLogList_CollectionChanged;
                    filteredChatLogList.CollectionChanged -= FilteredChatLogList_CollectionChanged;
                    writer.Enabled = false;
                    base.Dispose(disposing);
                }
                disposedValue = true;
            }
        }
    }
}
