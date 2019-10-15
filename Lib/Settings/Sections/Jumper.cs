using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Settings.Sections {
    [Section("Jumper")]
    public class Jumper : SectionBase {
        [Summary("PauseNav")]
        [DefaultValue(true)]
        public bool PauseNav {
            get { return (bool)GetSetting("PauseNav"); }
            set { UpdateSetting("PauseNav", value); }
        }

        [Summary("ThinkComplete")]
        [DefaultValue(false)]
        public bool ThinkComplete {
            get { return (bool)GetSetting("ThinkComplete"); }
            set { UpdateSetting("ThinkComplete", value); }
        }

        [Summary("ThinkFail")]
        [DefaultValue(false)]
        public bool ThinkFail {
            get { return (bool)GetSetting("ThinkFail"); }
            set { UpdateSetting("ThinkFail", value); }
        }

        [Summary("Attempts")]
        [DefaultValue(3)]
        public int Attempts {
            get { return (int)GetSetting("Attempts"); }
            set { UpdateSetting("Attempts", value); }
        }

        public Jumper(SectionBase parent) : base(parent) {
            Name = "Jumper";
        }
    }
}
