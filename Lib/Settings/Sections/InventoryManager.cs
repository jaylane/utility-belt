using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Settings.Sections {
    [Section("InventoryManager")]
    public class InventoryManager : SectionBase {
        [Summary("Automatically cram items into side packs")]
        [DefaultValue(false)]
        public bool AutoCram {
            get { return (bool)GetSetting("AutoCram"); }
            set { UpdateSetting("AutoCram", value); }
        }

        [Summary("Automatically combine stacked items")]
        [DefaultValue(false)]
        public bool AutoStack {
            get { return (bool)GetSetting("AutoStack"); }
            set { UpdateSetting("AutoStack", value); }
        }

        public InventoryManager(SectionBase parent) : base(parent) {
            Name = "InventoryManager";
        }
    }
}
