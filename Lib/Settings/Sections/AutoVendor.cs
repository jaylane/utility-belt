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
            get { return (bool)GetSetting(); }
            set { UpdateSetting(value); }
        }

        [Summary("Think to yourself when auto vendor is completed")]
        [DefaultValue(false)]
        public bool Think {
            get { return (bool)GetSetting(); }
            set { UpdateSetting(value); }
        }

        [Summary("Test mode (don't actually sell/buy, just echo to the chat window)")]
        [DefaultValue(false)]
        public bool TestMode {
            get { return (bool)GetSetting(); }
            set { UpdateSetting(value); }
        }

        [Summary("Show merchant info on approach vendor")]
        [DefaultValue(true)]
        public bool ShowMerchantInfo {
            get { return (bool)GetSetting(); }
            set { UpdateSetting(value); }
        }

        [Summary("Only vendor things in your main pack")]
        [DefaultValue(false)]
        public bool OnlyFromMainPack {
            get { return (bool)GetSetting(); }
            set { UpdateSetting(value); }
        }

        [Summary("Delay between vendor actions (in milliseconds)")]
        [DefaultValue(500)]
        public int Speed {
            get { return (int)GetSetting(); }
            set { UpdateSetting(value); }
        }
    }
}
