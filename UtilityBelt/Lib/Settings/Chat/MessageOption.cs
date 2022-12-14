using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using UtilityBelt.Service.Lib.Settings;

namespace UtilityBelt.Lib.Settings.Chat {
    public class MessageOption : ISetting {

        [Summary("Npc tells")]
        public readonly Setting<bool> NpcTells = new Setting<bool>();

        [Summary("Vendor tells")]
        public readonly Setting<bool> VendorTells = new Setting<bool>();

        public MessageOption(bool npcTells, bool vendorTells) {
            NpcTells.Value = npcTells;
            VendorTells.Value = vendorTells;
        }

        new public string ToString() {
            return $"NpcTells:{NpcTells}, VendorTells:{VendorTells}";
        }

        public bool Equals(MessageOption obj) {
            return ToString() == obj.ToString();
        }
    }
}
