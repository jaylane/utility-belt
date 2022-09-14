using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using UBService.Lib.Settings;

namespace UtilityBelt.Lib.Settings.Chat {
    public class MiscOption : ISetting {

        [Summary("Burned components messages")]
        public readonly Setting<bool> BurnedComps = new Setting<bool>();

        [Summary("Aetheria surge")]
        public readonly Setting<bool> AetheriaSurge = new Setting<bool>();

        [Summary("Cloak casts")]
        public readonly Setting<bool> Cloak = new Setting<bool>();

        [Summary("Dirty fighting attack")]
        public readonly Setting<bool> DirtyFighting = new Setting<bool>();

        [Summary("You obtain x salvage")]
        public readonly Setting<bool> Salvage = new Setting<bool>();

        [Summary("Spell has expired")]
        public readonly Setting<bool> SpellExpired = new Setting<bool>();

        public MiscOption(bool burnedComps, bool aetheriaSurge, bool cloak, bool dirtyFighting, bool salvage, bool spellExpired) {
            BurnedComps.Value = burnedComps;
            AetheriaSurge.Value = aetheriaSurge;
            Cloak.Value = cloak;
            DirtyFighting.Value = dirtyFighting;
            Salvage.Value = salvage;
            SpellExpired.Value = spellExpired;
        }

        new public string ToString() {
            return $"BurnedComps:{BurnedComps}, AetheriaSurge:{AetheriaSurge}, Cloak:{Cloak}, DirtyFighting:{DirtyFighting}, Salvage:{Salvage}, SpellExpired:{SpellExpired}";
            //return $"Enabled:{HealKit}";
        }

        public bool Equals(MiscOption obj) {
            return ToString() == obj.ToString();
        }
    }
}