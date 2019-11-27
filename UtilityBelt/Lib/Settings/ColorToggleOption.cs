using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Settings {
    public class ColorToggleOption : SectionBase {
        private bool enabled;
        [DefaultValue(true)]
        public bool Enabled {
            get { return enabled; }
            set {
                if (enabled == value) return;
                enabled = value;
                OnPropertyChanged("Enabled");
            }
        }
        
        private int color;
        [DefaultValue(-1)]
        public int Color {
            get { return color; }
            set {
                if (color == value) return;
                color = value;
                OnPropertyChanged("Color");
            }
        }

        [JsonIgnore]
        public int DefaultColor { get; set; } = System.Drawing.Color.White.ToArgb();

        public ColorToggleOption(SectionBase parent, bool show, int color) : base(parent) {
            Enabled = show;
            Color = color;
            DefaultColor = color;
        }

        new public string ToString() {
            return $"Enabled:{Enabled} Color:{Color} DefaultColor:{Color}";
        }

        public bool Equals(MarkerToggleOption obj) {
            return ToString() == obj.ToString();
        }
    }
}
