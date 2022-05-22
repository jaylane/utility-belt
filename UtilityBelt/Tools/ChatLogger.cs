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
using UtilityBelt.Lib.Settings;
using VirindiViewService.Controls;
using UBLoader.Lib.Settings;

namespace UtilityBelt.Tools {

    [Name("ChatLogger")]
    [Summary("Allows you to log chat messages to a file filtered by type / regular expression.")]
    [FullDescription(@"
The Chat Logger tool is, as its name implies, a way to log messages in your chat window. The logs are driven by a set of user-defined rules that match the message type and a regular expression pattern that matches the text of the message. By default, these messages will be logged to the Chat Log tab in UB, but can also be saved to disk in settings. The logs will be saved at `Documents\Decal Plugins\UtilityBelt\<Server>\<Character>\chat.txt`.

### Chat Log Rules

![](/screenshots/ChatLogSettings.png)

To add a new chat log rule:
1. Create a pattern for messages you'd like to match in the filter field
2. Select the message types you'd like the filter to apply to
3. Click the Add Rule button

### Message Types

Chat logger supports the following message types:

 * ![](/img/chatlog/6119.gif) **Broadcast** - Allegiance MoTD, give messages, craft messages, Mana stone refill, etc.
 * ![](/img/chatlog/1028.gif) **Speech** - Local chat
 * ![](/img/chatlog/1036.gif) **Tell** - Direct tell (whisper) both incoming and outgoing
 * ![](/img/chatlog/2D13.gif) **System** - House maintenance due, Rare discovered
 * ![](/img/chatlog/10BC.gif) **Combat** - Attacks, evades, bleeds
 * ![](/img/chatlog/32CD.gif) **Magic** - Equipment spells, Spell results (resist, heal, stam-to-mana), Spell expiration
 * ![](/img/chatlog/1FA3.gif) **Channel** - Admin/Sentinel channels
 * ![](/img/chatlog/6D9F.gif) **Social** - Patron/Monarch/Vassal/Co-Vassal chat
 * ![](/img/chatlog/1035.gif) **Emote** - Emote text
 * ![](/img/chatlog/73EE.gif) **Advancement** - Level up message, Skill credit message
 * ![](/img/chatlog/1372.gif) **Abuse** - Abuse chat channel
 * ![](/img/chatlog/18D5.gif) **Help** - Help chat channel
 * ![](/img/chatlog/1388.gif) **Appraisal** - 'So - and - so tried and failed to assess you!'
 * ![](/img/chatlog/1374.gif) **Spellcasting** - Spell word messages
 * ![](/img/chatlog/218B.gif) **Allegiance** - Allegiance chat messages
 * ![](/img/chatlog/1436.gif) **Fellowship** - Fellowship chat
 * ![](/img/chatlog/1F88.gif) **World Broadcast** - Global quest messages (Aerlinthe, QQ, etc)
 * ![](/img/chatlog/1382.gif) **Recall** - Recalling home/mansion/hometown messages
 * ![](/img/chatlog/1C72.gif) **Craft** - Tinkering success/failure messages
 * ![](/img/chatlog/26BA.gif) **Salvaging** - Item salvaging messages
 * ![](/img/chatlog/33BF.gif) **General** - General (global) chat messages
 * ![](/img/chatlog/2761.gif) **Trade** - Trade channel chat messages
 * ![](/img/chatlog/2FB9.gif) **LFG** - Looking-For-Group (LFG) chat messages
 * ![](/img/chatlog/624A.gif) **Roleplay** - Roleplay chat messages
 * ![](/img/chatlog/2632.gif) **Admin Tell** - Direct tell from admin
 * ![](/img/chatlog/10E7.gif) **Olthoi** - Olthoi chat channel
 * ![](/img/chatlog/70A0.gif) **Society** - Society chat channel
    ")]
    public class ChatLogger : ToolBase {
        private string filter = null;
        private readonly ObservableCollection<ChatLog> chatLogList = new ObservableCollection<ChatLog>();
        private readonly ObservableCollection<ChatLog> filteredChatLogList = new ObservableCollection<ChatLog>();
        private Timer debounceTimer;
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
        public readonly Setting<bool> SaveToFile = new Setting<bool>(false);

        [Summary("List of message types to log")]
        public readonly Setting<ObservableCollection<ChatLogRule>> Rules = new Setting<ObservableCollection<ChatLogRule>>(new ObservableCollection<ChatLogRule>());

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

        }

        public override void Init() {
            base.Init();
            try {
                UIChatLogFilter = (HudTextBox)UB.MainView.view["ChatLogFilterText"];
                UIChatLogFilter.Change += UIChatLogFilter_Change;

                UIChatLogLogList = (HudList)UB.MainView.view["ChatLogLogList"];
                UIChatLogLogList.Click += UIChatLogLogList_Click;

                UIChatLogClear = (HudButton)UB.MainView.view["ChatLogClear"];
                UIChatLogClear.Hit += UIChatLogClear_Hit;

                UIChatLogSaveToFile = (HudCheckBox)UB.MainView.view["ChatLogSaveToFile"];
                UIChatLogSaveToFile.Change += UIChatLogSaveToFile_Change;

                UIChatLogMessageFilter = (HudTextBox)UB.MainView.view["ChatLogMessageFilter"];

                UIChatLogTypeList = (HudList)UB.MainView.view["ChatLogTypeList"];

                // Initialize chat types
                UIChatLogTypeList.ClearRows();
                int i = 0;
                foreach (ChatMessageType type in Enum.GetValues(typeof(ChatMessageType))) {
                    if (type.ShowInSettings()) {
                        typeTable[i++] = type;

                        var row = UIChatLogTypeList.AddRow();
                        ((HudCheckBox)row[0]).Checked = false;
                        ((HudCheckBox)row[0]).Change += (s, e) => {
                            try {
                                if (((HudCheckBox)row[0]).Checked && !currentSelectedTypes.Contains(type)) {
                                    currentSelectedTypes.Add(type);
                                }
                                else if (currentSelectedTypes.Contains(type)) {
                                    currentSelectedTypes.Remove(type);
                                }
                            }
                            catch (Exception ex) { Logger.LogException(ex); }
                        };
                        ((HudPictureBox)row[1]).Image = type.GetIcon();
                        ((HudStaticText)row[2]).Text = type.GetDescription();
                    }
                }

                UIChatLogRuleAddButton = (HudButton)UB.MainView.view["ChatLogRuleAddButton"];
                UIChatLogRuleAddButton.Hit += UIChatLogRuleAddButton_Hit;

                UIChatLogRuleList = (HudList)UB.MainView.view["ChatLogRuleList"];
                UIChatLogRuleList.Click += UIChatLogRuleList_Click;

                UB.Core.ChatBoxMessage += Core_ChatBoxMessage;

                chatLogList.CollectionChanged += ChatLogList_CollectionChanged;
                filteredChatLogList.CollectionChanged += FilteredChatLogList_CollectionChanged;

                debounceTimer = new Timer(Timer_Tick);

                SaveToFile.Changed += (s, e) => UpdateUI();

                UpdateUI();

                Rules.Value.CollectionChanged += Rules_CollectionChanged;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Rules_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
            UpdateUI();
        }

        private void UIChatLogRuleList_Click(object sender, int row, int col) {
            try {
                if (col == 0) {
                    if (editingRow.HasValue && row == editingRow.Value) {
                        editingRow = null;
                        UIChatLogMessageFilter.Text = "";
                        UIChatLogRuleAddButton.Text = "Add Rule";
                    }

                    Rules.Value.RemoveAt(row);
                }
                else if (col == 1) {
                    Logger.WriteToChat("[" + string.Join(", ", Rules.Value[row].Types.Select(t => t.ToString()).ToArray()) + "]");
                }
                else if (col == 2) {
                    if (editingRow.HasValue)
                        ((HudStaticText)UIChatLogRuleList[editingRow.Value][2]).TextColor = System.Drawing.Color.White;

                    var tRow = UIChatLogRuleList[row];
                    editingRow = row;
                    UIChatLogMessageFilter.Text = Rules.Value[row].MessageFilter;

                    currentSelectedTypes.Clear();
                    for (var i = 0; i < UIChatLogTypeList.RowCount; ++i) {
                        var r = UIChatLogTypeList[i];
                        if (Rules.Value[row].Types.Contains(typeTable[i])) {
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
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UIChatLogRuleAddButton_Hit(object sender, EventArgs e) {
            try {
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
                    Rules.Value[editingRow.Value] = newRule;
                    ((HudStaticText)UIChatLogRuleList[editingRow.Value][2]).TextColor = System.Drawing.Color.White;
                    editingRow = null;
                    UIChatLogRuleAddButton.Text = "Add Rule";
                }
                else {
                    LogDebug($"New rule added: {newRule}");
                    Rules.Value.Add(newRule);
                }

                UIChatLogMessageFilter.Text = "";
                currentSelectedTypes.Clear();
                for (var i = 0; i < UIChatLogTypeList.RowCount; ++i) {
                    var row = UIChatLogTypeList[i];
                    ((HudCheckBox)row[0]).Checked = false;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UIChatLogSaveToFile_Change(object sender, EventArgs e) {
            try {
                SaveToFile.Value = UIChatLogSaveToFile.Checked;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UpdateUI() {
            UIChatLogSaveToFile.Checked = SaveToFile;

            UIChatLogRuleList.ClearRows();
            foreach (var rule in Rules.Value) {
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
            try {
                chatLogList.Clear();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Timer_Tick(object _) {
            Filter = UIChatLogFilter.Text;
        }

        private void UIChatLogLogList_Click(object sender, int row, int col) {
            try {
                var clickedRow = filteredChatLogList[filteredChatLogList.Count - 1 - row];
                var type = clickedRow.Type.GetParent() ?? clickedRow.Type;
                WriteToChat($"{clickedRow.Timestamp.ToLocalTime():g}: [{type.GetDescription()}] {clickedRow.Message}");
            }
            catch (Exception ex) { Logger.LogException(ex); }
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
            try {
                debounceTimer.Change(500, Timeout.Infinite);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Core_ChatBoxMessage(object sender, Decal.Adapter.ChatTextInterceptEventArgs e) {
            try {
                if (Enum.IsDefined(typeof(ChatMessageType), (ChatMessageType)e.Color)) {
                    var type = (ChatMessageType)Enum.ToObject(typeof(ChatMessageType), e.Color);
                    var parentType = type.GetParent();
                    if (Rules.Value.Any(r => r.Types.Contains(parentType ?? type) && Regex.IsMatch(e.Text, r.MessageFilter))) {
                        chatLogList.Add(new ChatLog(type, e.Text.Trim()));
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    Rules.Value.CollectionChanged -= Rules_CollectionChanged;
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
