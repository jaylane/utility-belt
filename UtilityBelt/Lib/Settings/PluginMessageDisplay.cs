using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using UBLoader.Lib.Settings;

namespace UtilityBelt.Lib.Settings {
    public class PluginMessageDisplay : ISetting {
        [Summary("Enabled / Disabled")]
        public readonly Setting<bool> Enabled = new Setting<bool>();

        [Summary("Color")]
        public readonly Setting<Constants.ChatMessageType> Color = new Setting<Constants.ChatMessageType>();

        public PluginMessageDisplay(bool show, Constants.ChatMessageType color) : base() {
            Enabled.Value = show;
            Color.Value = color;
        }

        new public string ToString() {
            return $"Enabled:{Enabled} Color:{Color}";
        }
    }
}
