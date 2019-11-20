using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace UtilityBelt.Lib.Quests {
    class QuestFlag : IComparable {
        public static readonly Regex MyQuestRegex = new Regex(@"(?<key>\S+) \- (?<solves>\d+) solves \((?<completedOn>\d{0,11})\)""?((?<description>.*)"" (?<maxSolves>.*) (?<repeatTime>\d{0,6}))?.*$");
        public static readonly Regex KillTaskRegex = new Regex(@"killtask|killcount|slayerquest|totalgolem.*dead");

        public static Dictionary<string, string> FriendlyNamesLookup = new Dictionary<string, string>();

        public string Key = "";
        public string Description = "";
        public int Solves = 0;
        public int MaxSolves = 1;
        public DateTime CompletedOn = DateTime.MinValue;
        public TimeSpan RepeatTime = TimeSpan.FromSeconds(0);
        
        public string Name {
            get {
                if (FriendlyNamesLookup.Keys.Contains(Key)) {
                    return FriendlyNamesLookup[Key];
                }

                return Key;
            }
        }

        public QuestFlagType FlagType {
            get {
                if (KillTaskRegex.IsMatch(Key)) {
                    return QuestFlagType.KillTask;
                }
                else if (MaxSolves == 1) {
                    return QuestFlagType.Once;
                }
                else {
                    return QuestFlagType.Timed;
                }
            }
        }

        static QuestFlag() {
            LoadQuestLookupXML();
        }

        public static QuestFlag FromMyQuestsLine(string line) {
            try {
                var questFlag = new QuestFlag();
                Match match = MyQuestRegex.Match(line);

                if (match.Success) {
                    questFlag.Key = match.Groups["key"].Value;
                    questFlag.Description = match.Groups["description"].Value;

                    int.TryParse(match.Groups["solves"].Value, out questFlag.Solves);
                    int.TryParse(match.Groups["maxSolves"].Value, out questFlag.MaxSolves);

                    double completedOn = 0;
                    if (double.TryParse(match.Groups["completedOn"].Value, out completedOn)) {
                        questFlag.CompletedOn = Util.UnixTimeStampToDateTime(completedOn);

                        double repeatTime = 0;
                        if (double.TryParse(match.Groups["repeatTime"].Value, out repeatTime)) {
                            questFlag.RepeatTime = TimeSpan.FromSeconds(repeatTime);
                        }
                    }

                    return questFlag;
                }
                else {
                    Util.WriteToChat("Unable to parse myquests line: " + line);
                    return null;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return null;
        }
        
        public static void LoadQuestLookupXML() {
            try {
                string filePath = Path.Combine(Util.GetResourcesDirectory(), "quests.xml");
                Stream fileStream = null;
                if (File.Exists(filePath)) {
                    fileStream = new FileStream(filePath, FileMode.Open);
                }
                else {
                    fileStream = typeof(QuestFlag).Assembly.GetManifestResourceStream($"UtilityBelt.Resources.quests.xml");
                }

                using (XmlReader reader = XmlReader.Create(fileStream)) {
                    while (reader.Read()) {
                        if (reader.IsStartElement() && reader.Name != "root") {
                            var questTag = reader.Name.ToLower();
                            var questDisplay = reader.ReadElementContentAsString();

                            if (!string.IsNullOrEmpty(questDisplay) && !FriendlyNamesLookup.ContainsKey(questTag)) {
                                FriendlyNamesLookup.Add(questTag, questDisplay);
                            }
                        }
                    }
                }

                fileStream.Dispose();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public int CompareTo(object obj) {
            if (obj == null) return -1;

            QuestFlag otherQuestFlag = obj as QuestFlag;

            if (otherQuestFlag != null) {
                if (otherQuestFlag.IsReady() && !IsReady()) {
                    return -1;
                }
                else if (IsReady() && !otherQuestFlag.IsReady()) {
                    return 1;
                }
                else if (!IsReady() && !otherQuestFlag.IsReady()) {
                    var otherTime = (otherQuestFlag.CompletedOn + otherQuestFlag.RepeatTime) - DateTime.UtcNow;
                    var thisTime = (CompletedOn + RepeatTime) - DateTime.UtcNow;
                    return thisTime.CompareTo(otherTime);
                }
                else {
                    return Name.ToLower().CompareTo(otherQuestFlag.Name.ToLower());
                }
            }

            return -1;
        }

        internal bool IsReady() {
            var difference = (CompletedOn + RepeatTime) - DateTime.UtcNow;

            if (difference.TotalSeconds > 0) {
                return false;
            }
            else {
                return FlagType == QuestFlagType.Once ? false : true;
            }
        }

        internal string NextAvailable() {
            var difference = (CompletedOn + RepeatTime) - DateTime.UtcNow;

            if (difference.TotalSeconds > 0) {
                return Util.GetFriendlyTimeDifference(difference);
            }
            else {
                return "ready";
            }
        }
    }
}
