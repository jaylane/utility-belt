using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using UtilityBelt.Lib.ChatLog;

namespace UtilityBelt.Lib.Settings.Sections
{
    public class ChatLogger : SectionBase
    {
        [Summary("Save chat log to disk")]
        [DefaultValue(false)]
        public bool SaveToFile
        {
            get { return (bool)GetSetting("SaveToFile"); }
            set { UpdateSetting("SaveToFile", value); }
        }

        //[Summary("List of message types to log")]
        public ObservableCollection<ChatLogRule> Rules { get; set; } = new ObservableCollection<ChatLogRule>();

        public ChatLogger(SectionBase parent) : base(parent)
        {
            Name = "ChatLogger";
            Rules.CollectionChanged += Rules_CollectionChanged;
        }

        private void Rules_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            base.OnPropertyChanged(nameof(Rules));
        }
    }

    public class ChatLogRule
    {
        [JsonProperty(ItemConverterType = typeof(StringEnumConverter))]
        public IEnumerable<ChatMessageType> Types { get; set; }

        public string MessageFilter { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
