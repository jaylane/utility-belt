using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using UtilityBelt.Service.Lib.Settings;

namespace UtilityBelt.Lib.Settings {
    public class MarkerToggleOption : ISetting {
        [Summary("Enabled")]
        public readonly Setting<bool> Enabled = new Setting<bool>();

        [Summary("Show Label")]
        public readonly Setting<bool> ShowLabel = new Setting<bool>();

        [Summary("Use Icon")]
        public readonly Setting<bool> UseIcon = new Setting<bool>();

        [Summary("Marker Color")]
        public readonly Setting<int> Color = new Setting<int>();

        [Summary("Size")]
        public readonly Setting<int> Size = new Setting<int>();

        public MarkerToggleOption(bool enabled, bool useIcon, bool showLabel, int color, int size) : base() {
            Enabled.Value = enabled;
            ShowLabel.Value = showLabel;
            UseIcon.Value = useIcon;
            Color.Value = color;
            Size.Value = size;
        }

        new public string ToString() {
            return $"Enabled:{Enabled} UseIcon:{UseIcon} ShowLabel:{ShowLabel} Color:{Color} DefaultColor:{Color} Size:{Size}";
        }

        public bool Equals(MarkerToggleOption obj) {
            return ToString() == obj.ToString();
        }
    }
}
