using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Settings.Sections {
    [Section("VTank")]
    public class VTank : SectionBase {
        [Summary("VitalSharing")]
        [DefaultValue(true)]
        public bool VitalSharing {
            get { return (bool)GetSetting("VitalSharing"); }
            set { UpdateSetting("VitalSharing", value); }
        }

        public VTank(SectionBase parent) : base(parent) {
            Name = "VTank";
        }
    }
}
