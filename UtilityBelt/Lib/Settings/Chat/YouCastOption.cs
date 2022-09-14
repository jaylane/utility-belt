using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using UBService.Lib.Settings;

namespace UtilityBelt.Lib.Settings.Chat {
    public class YouCastOption : ISetting {


        [Summary("You cast messages")]
        public readonly Setting<bool> YouCast = new Setting<bool>();

        [Summary("You fail to affect messages")]
        public readonly Setting<bool> YouFailToAffect = new Setting<bool>();

        [Summary("Your spell fizzled")]
        public readonly Setting<bool> SpellFizzle = new Setting<bool>();

        [Summary("Self buffs")]
        public readonly Setting<bool> SelfBuffs = new Setting<bool>();

        [Summary("Heal self messages")]
        public readonly Setting<bool> HealSelf = new Setting<bool>();

        [Summary("Stamina to mana, stamina to health, etc...")]
        public readonly Setting<bool> GiveAndTake = new Setting<bool>();

        [Summary("Damage spell words for cast")]
        public readonly Setting<bool> YouCastDamageSpell = new Setting<bool>();

        [Summary("Debuff spell words")]
        public readonly Setting<bool> Debuffs = new Setting<bool>();

        public YouCastOption(bool youCast, bool youFailToAffect, bool spellFizzle, bool selfBuffs, bool healSelf, bool giveAndTake, bool youCastDamageSpell, bool debuffs) {
            YouCast.Value = youCast;
            YouFailToAffect.Value = youFailToAffect;
            SpellFizzle.Value = spellFizzle;
            SelfBuffs.Value = selfBuffs;
            HealSelf.Value = healSelf;
            GiveAndTake.Value = giveAndTake;
            YouCastDamageSpell.Value = youCastDamageSpell;
            Debuffs.Value = debuffs;
        }

        new public string ToString() {
            return $"YouCast:{YouCast}, YouFailToAffect:{YouFailToAffect}, SpellFizzle:{SpellFizzle}, SelfBuffs:{SelfBuffs}, HealSelf:{HealSelf}, GiveAndTake:{GiveAndTake}, YouCastDamageSpell:{YouCastDamageSpell}, Debuffs:{Debuffs}";
            //return $"Enabled:{HealKit}";
        }

        public bool Equals(YouCastOption obj) {
            return ToString() == obj.ToString();
        }
    }
}