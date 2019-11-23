using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Constants {
    public enum DamageTypes : uint {
        Normal = 0x00,
        Slashing = 0x01,
        Piercing = 0x02,
        SlashPierce = 0x03,
        Bludgeoning = 0x04,
        Cold = 0x08,
        Fire = 0x10,
        Acid = 0x20,
        Electric = 0x40,
        Nether = 0x400
    }
}
