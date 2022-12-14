using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using UtilityBelt.Service.Lib.Settings;

namespace UtilityBelt.Lib.Settings.Chat {
    public class OthersCastOption : ISetting {

        [Summary("Others cast buff spell")]
        public readonly Setting<bool> OtherBuffs = new Setting<bool>();

        [Summary("Others cast damage spell")]
        public readonly Setting<bool> OtherCastsDamageSpell = new Setting<bool>();

        [Summary("Others cast regen spell")]
        public readonly Setting<bool> RegenSpell = new Setting<bool>();

        [Summary("Others cast stam regen spell")]
        public readonly Setting<bool> StamRegenSpell = new Setting<bool>();
        

        public OthersCastOption(bool otherBuffs, bool otherCastsDamageSpell, bool regenSpell, bool stamRegenSpell) {
            OtherBuffs.Value = otherBuffs;
            OtherCastsDamageSpell.Value = otherCastsDamageSpell;
            RegenSpell.Value = regenSpell;
            StamRegenSpell.Value = stamRegenSpell;
        }

        new public string ToString() {
            return $"OtherBuffs:{OtherBuffs}, OtherCastsDamageSpell:{OtherCastsDamageSpell}, RegenSpell:{RegenSpell}, StamRegenSpell:{StamRegenSpell}";
        }

        public bool Equals(OthersCastOption obj) {
            return ToString() == obj.ToString();
        }
    }
}