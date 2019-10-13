using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Settings {
    public class MarkerToggleOption : SectionBase {
        private bool enabled;
        public bool Enabled {
            get { return enabled; }
            set {
                if (enabled == value) return;
                enabled = value;
                OnPropertyChanged("Enabled");
            }
        }

        private bool showLabel;
        public bool ShowLabel {
            get { return showLabel; }
            set {
                if (showLabel == value) return;
                showLabel = value;
                OnPropertyChanged("ShowLabel");
            }
        }

        private bool useIcon;
        public bool UseIcon {
            get { return useIcon; }
            set {
                if (useIcon == value) return;
                useIcon = value;
                OnPropertyChanged("UseIcon");
            }
        }

        private int color;
        public int Color {
            get { return color; }
            set {
                if (color == value) return;
                color = value;
                OnPropertyChanged("Color");
            }
        }

        private int size;
        public int Size {
            get { return size; }
            set {
                if (size == value) return;
                size = value;
                OnPropertyChanged("Size");
            }
        }

        [JsonIgnore]
        public int DefaultColor { get; set; } = System.Drawing.Color.White.ToArgb();

        public MarkerToggleOption(SectionBase parent, bool enabled, bool useIcon, bool showLabel, int color, int size) : base(parent) {
            Enabled = enabled;
            ShowLabel = showLabel;
            UseIcon = useIcon;
            Color = color;
            DefaultColor = color;
            Size = size;
        }

        new public string ToString() {
            return $"Enabled:{Enabled} UseIcon:{UseIcon} ShowLabel:{ShowLabel} Color:{Color} DefaultColor:{Color} Size:{Size}";
        }

        public MarkerToggleOption Clone() {
            var n = new MarkerToggleOption(parent, enabled, useIcon, showLabel, color, size);
            n.Name = Name;
            return n;
        }

        internal void RestoreFrom(MarkerToggleOption originalOptions) {
            Name = originalOptions.Name;
            parent = originalOptions.parent;
            Enabled = originalOptions.Enabled;
            UseIcon = originalOptions.UseIcon;
            ShowLabel = originalOptions.ShowLabel;
            Color = originalOptions.Color;
            Size = originalOptions.Size;
        }
        public bool Equals(MarkerToggleOption obj) {
            return ToString() == obj.ToString();
        }
    }
}
