using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using UBLoader.Lib.Settings;

namespace UtilityBelt.Lib.Settings {
    public class ColorToggleOption : ISetting {
        [Summary("Enabled / Disabled")]
        public readonly Setting<bool> Enabled = new Setting<bool>();

        [Summary("Color")]
        public readonly Setting<int> Color = new Setting<int>();

        public int DefaultColor { get; set; } = System.Drawing.Color.White.ToArgb();

        public ColorToggleOption(bool enabled, int defaultColor) : base() {
            Enabled.Value = enabled;
            DefaultColor = defaultColor;
            Color.Value = defaultColor;
        }

        new public string ToString() {
            return $"Enabled:{Enabled} Color:{Color}";
        }

        public bool Equals(ColorToggleOption obj) {
            return ToString() == obj.ToString();
        }
    }
}
