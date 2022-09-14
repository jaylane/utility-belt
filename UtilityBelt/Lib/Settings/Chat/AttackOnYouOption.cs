using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using UBService.Lib.Settings;

namespace UtilityBelt.Lib.Settings.Chat {
    public class AttackOnYouOption : ISetting {

        [Summary("A spell has been cast on you")]
        public readonly Setting<bool> OthersCast = new Setting<bool>();

        [Summary("Sepll damage has been taken")]
        public readonly Setting<bool> DamageSpellOnYou = new Setting<bool>();

        [Summary("Melee/missile damage has been taken")]
        public readonly Setting<bool> DamageMeleeOnYou = new Setting<bool>();

        [Summary("You evaded an attack")]
        public readonly Setting<bool> YouEvaded = new Setting<bool>();

        [Summary("You resisted a spell")]
        public readonly Setting<bool> YouResistSpell = new Setting<bool>();

        [Summary("Fails to affect you messages")]
        public readonly Setting<bool> FailsToAffectYou = new Setting<bool>();

        [Summary("Periodic damage on you")]
        public readonly Setting<bool> PeriodicDamage = new Setting<bool>();

        [Summary("Debuff spell has been cast on you")]
        public readonly Setting<bool> DebuffOnYou = new Setting<bool>();

        public AttackOnYouOption(bool othersCast, bool damageSpellOnYou, bool damageMeleeOnYou, bool youEvaded, bool youResistSpell,
            bool failsToAffectYou, bool periodicDamage, bool debuffOnYou) {
            OthersCast.Value = othersCast;
            DamageSpellOnYou.Value = damageSpellOnYou;
            DamageMeleeOnYou.Value = damageMeleeOnYou;
            YouEvaded.Value = youEvaded;
            YouResistSpell.Value = youResistSpell;
            FailsToAffectYou.Value = failsToAffectYou;
            PeriodicDamage.Value = periodicDamage;
            DebuffOnYou.Value = debuffOnYou;
        }

        new public string ToString() {
            return $"OthersCast:{OthersCast}, DamageSpellOnYou:{DamageSpellOnYou}, DamageMeleeOnYou:{DamageMeleeOnYou}, YouEvaded:{YouEvaded}, YouResistSpell:{YouResistSpell},FailsToAffectYou:{FailsToAffectYou},DebuffOnYou:{DebuffOnYou},PeriodicDamage:{PeriodicDamage}";
            //return $"Enabled:{HealKit}";
        }

        public bool Equals(AttackOnYouOption obj) {
            return ToString() == obj.ToString();
        }
    }
}