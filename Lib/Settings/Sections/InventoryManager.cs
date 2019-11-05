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

        [Summary("Think to yourself when ItemGiver Finishes")]
        [DefaultValue(false)]
        public bool IGThink {
            get { return (bool)GetSetting("IGThink"); }
            set { UpdateSetting("IGThink", value); }
        }
        [Summary("Item Failure Count to fail ItemGiver")]
        [DefaultValue(3)]
        public int IGFailure {
            get { return (int)GetSetting("IGFailure"); }
            set { UpdateSetting("IGFailure", value); }
        }
        [Summary("Busy Count to fail ItemGiver give")]
        [DefaultValue(10)]
        public int IGBusyCount {
            get { return (int)GetSetting("IGBusyCount"); }
            set { UpdateSetting("IGBusyCount", value); }
        }
        [Summary("Maximum Range for ItemGiver commands")]
        [DefaultValue(15)]
        public double IGRange {
            get { return (int)GetSetting("IGRange"); }
            set { UpdateSetting("IGRange", value); }
        }
        public InventoryManager(SectionBase parent) : base(parent) {
            Name = "InventoryManager";
        }
    }
}
