using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Settings {
    public class PluginMessageDisplay : SectionBase {
        private bool enabled;
        [DefaultValue(true)]
        [Summary("Enabled / Disabled")]
        public bool Enabled {
            get { return enabled; }
            set {
                if (enabled == value) return;
                enabled = value;
                OnPropertyChanged("Enabled");
            }
        }

        private short color;
        [DefaultValue(5)]
        [Summary("Color")]
        public short Color {
            get { return color; }
            set {
                if (color == value) return;
                color = value;
                OnPropertyChanged("Color");
            }
        }

        public PluginMessageDisplay(SectionBase parent, bool show, short color) : base(parent) {
            Enabled = show;
            Color = color;
        }

        new public string ToString() {
            return $"Enabled:{Enabled} Color:{Color}";
        }

        public bool Equals(PluginMessageDisplay obj) {
            return ToString() == obj.ToString();
        }
    }
}
