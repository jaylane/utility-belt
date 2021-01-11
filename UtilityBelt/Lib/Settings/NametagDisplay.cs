using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using UBLoader.Lib.Settings;

namespace UtilityBelt.Lib.Settings {
    public class NametagDisplay : ISetting {
        [Summary("Enabled / Disabled")]
        public readonly Setting<bool> Enabled = new Setting<bool>();

        [Summary("Tag Color (main text)")]
        public readonly Setting<int> TagColor = new Setting<int>();

        [Summary("Tag Size (main text)")]
        public readonly Setting<float> TagSize = new Setting<float>();

        [Summary("Ticker Color (sub text)")]
        public readonly Setting<int> TickerColor = new Setting<int>();

        [Summary("Ticker Size (sub text)")]
        public readonly Setting<float> TickerSize = new Setting<float>();


        public NametagDisplay(bool enabled, int tagColor, float tagSize, int tickerColor, float tickerSize) {
            Enabled.Value = enabled;
            TagColor.Value = tagColor;
            TagSize.Value = tagSize;
            TickerColor.Value = tickerColor;
            TickerSize.Value = tickerSize;
        }

        new public string ToString() {
            return $"Enabled:{Enabled} TagColor:{TagColor} TagSize:{TagSize} TickerColor:{TickerColor} TickerSize:{TickerSize}";
        }
    }
}
