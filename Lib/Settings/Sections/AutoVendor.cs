using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Settings.Sections {
    [Section("AutoVendor")]
    public class AutoVendor : SectionBase {
        [Summary("Enabled")]
        [DefaultValue(true)]
        public bool Enabled {
            get { return (bool)GetSetting("Enabled"); }
            set { UpdateSetting("Enabled", value); }
        }

        [Summary("Think to yourself when auto vendor is completed")]
        [DefaultValue(false)]
        public bool Think {
            get { return (bool)GetSetting("Think"); }
            set { UpdateSetting("Think", value); }
        }

        [Summary("Test mode (don't actually sell/buy, just echo to the chat window)")]
        [DefaultValue(false)]
        public bool TestMode {
            get { return (bool)GetSetting("TestMode"); }
            set { UpdateSetting("TestMode", value); }
        }

        [Summary("Show merchant info on approach vendor")]
        [DefaultValue(true)]
        public bool ShowMerchantInfo {
            get { return (bool)GetSetting("ShowMerchantInfo"); }
            set { UpdateSetting("ShowMerchantInfo", value); }
        }

        [Summary("Only vendor things in your main pack")]
        [DefaultValue(false)]
        public bool OnlyFromMainPack {
            get { return (bool)GetSetting("OnlyFromMainPack"); }
            set { UpdateSetting("OnlyFromMainPack", value); }
        }

        [Summary("Delay between vendor actions (in milliseconds)")]
        [DefaultValue(500)]
        public int Speed {
            get { return (int)GetSetting("Speed"); }
            set { UpdateSetting("Speed", value); }
        }

        [Summary("Attempts to open vendor on /ub vendor open[p]")]
        [DefaultValue(4)]
        public int Tries {
            get { return (int)GetSetting("Tries"); }
            set { UpdateSetting("Tries", value); }
        }

        [Summary("Tine between open vendor attempts (in milliseconds)")]
        [DefaultValue(5000)]
        public int TriesTime {
            get { return (int)GetSetting("TriesTime"); }
            set { UpdateSetting("TriesTime", value); }
        }

        public AutoVendor(SectionBase parent) : base(parent) {
            Name = "AutoVendor";
        }
    }
}
