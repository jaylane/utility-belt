using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Settings.Sections {
    [Section("AutoTinker")]
    public class AutoTinker : SectionBase {
        [Summary("Minimum percentage required to perform tinker")]
        [DefaultValue(99.5f)]
        public float MinPercentage {
            get { return (float)GetSetting("MinPercentage"); }
            set { UpdateSetting("MinPercentage", value); }
        }

        public AutoTinker(SectionBase parent) : base(parent) {
            Name = "AutoTinker";
        }
    }
}
