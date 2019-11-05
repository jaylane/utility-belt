using System.Collections.ObjectModel;
using System.ComponentModel;

namespace UtilityBelt.Lib.Settings.Sections
{
    [Section("AutoTrade")]
    public class AutoTrade : SectionBase
    {
        [Summary("AutoTrade Enabled")]
        [DefaultValue(false)]
        public bool Enabled
        {
            get { return (bool)GetSetting("Enabled"); }
            set { UpdateSetting("Enabled", value); }
        }

        [Summary("Test mode (don't actually add to trade window, just echo to the chat window)")]
        [DefaultValue(false)]
        public bool TestMode
        {
            get { return (bool)GetSetting("TestMode"); }
            set { UpdateSetting("TestMode", value); }
        }

        [Summary("Think to yourself when auto trade is completed")]
        [DefaultValue(false)]
        public bool Think
        {
            get { return (bool)GetSetting("Think"); }
            set { UpdateSetting("Think", value); }
        }

        [Summary("Only trade things in your main pack")]
        [DefaultValue(false)]
        public bool OnlyFromMainPack
        {
            get { return (bool)GetSetting("OnlyFromMainPack"); }
            set { UpdateSetting("OnlyFromMainPack", value); }
        }

        [Summary("Auto accept trade after all items added")]
        [DefaultValue(false)]
        public bool AutoAccept
        {
            get { return (bool)GetSetting("AutoAccept"); }
            set { UpdateSetting("AutoAccept", value); }
        }

        [Summary("List of characters to auto-accept trade from")]
        [DefaultValue(null)]
        public ObservableCollection<string> AutoAcceptChars { get; set; } = new ObservableCollection<string>();

        public AutoTrade(SectionBase parent) : base(parent)
        {
            Name = "AutoTrade";
            AutoAcceptChars.CollectionChanged += AutoAcceptChars_CollectionChanged;
        }

        private void AutoAcceptChars_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(AutoAcceptChars));
        }
    }
}
