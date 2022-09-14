using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using UBService.Lib.Settings;

namespace UtilityBelt.Lib.Settings.Chat {
    public class AttackOnTargetOption : ISetting {
        [Summary("You do damage on your target")]
        public readonly Setting<bool> DamageOnTarget = new Setting<bool>();

        [Summary("Your spell was resisted")]
        public readonly Setting<bool> YourSpellResisted = new Setting<bool>();

        [Summary("Your missile attack missed")]
        public readonly Setting<bool> MissileAttackMissed = new Setting<bool>();

        [Summary("You cast a debuff on target")]
        public readonly Setting<bool> DebuffOnTarget = new Setting<bool>();

        [Summary("Messages from when you kill a creature")]
        public readonly Setting<bool> DeathMessages = new Setting<bool>();

        public AttackOnTargetOption(bool damageOnTarget, bool yourSpellResisted, bool missileAttackMissed,
                bool debuffOnTarget, bool deathMessages) {
            DamageOnTarget.Value = damageOnTarget;
            YourSpellResisted.Value = yourSpellResisted;
            MissileAttackMissed.Value = missileAttackMissed;
            DebuffOnTarget.Value = debuffOnTarget;
            DeathMessages.Value = deathMessages;
        }

        new public string ToString() {
            return $"DamageOnTarget:{DamageOnTarget}, YourSpellResisted:{YourSpellResisted}, MissileAttackMissed:{MissileAttackMissed}, DebuffOnTarget:{DebuffOnTarget}, DeathMessages:{DeathMessages}";
            //return $"Enabled:{HealKit}";
        }

        public bool Equals(AttackOnTargetOption obj) {
            return ToString() == obj.ToString();
        }
    }
}