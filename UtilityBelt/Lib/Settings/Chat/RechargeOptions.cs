using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using UtilityBelt.Service.Lib.Settings;

namespace UtilityBelt.Lib.Settings.Chat {
    public class RechargeOption : ISetting {

        [Summary("Heal yourself with a healing kit")]
        public readonly Setting<bool> HealKit = new Setting<bool>();

        [Summary("Periodic healing messages")]
        public readonly Setting<bool> PeriodicHealing = new Setting<bool>();

        [Summary("Enabled / Disabled")]
        public readonly Setting<bool> Consumable = new Setting<bool>();

        public RechargeOption(bool healKit, bool periodicHealing, bool consumable) {
            HealKit.Value = healKit;
            PeriodicHealing.Value = periodicHealing;
            Consumable.Value = consumable;
        }

        new public string ToString() {
            return $"HealKit:{HealKit}, PeriodicHealing:{PeriodicHealing}, Consumable:{Consumable}";
            //return $"Enabled:{HealKit}";
        }

        public bool Equals(RechargeOption obj) {
            return ToString() == obj.ToString();
        }
    }
}
