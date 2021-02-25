using Antlr4.Runtime;
using Decal.Adapter;
using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Expressions;
using UtilityBelt.Lib.VTNav;

namespace UtilityBelt.Lib.Chat {
    public enum ChatType {
        AetheriaSurge,
        BuffOnTarget,
        Consumable,
        SelfBuffs,
        OtherBuffs,
        BurnedComps,
        Cloak,
        DamageMeleeOnYou,
        DamageOnTarget,
        DamageSpellOnYou,
        YouCastDamageSpell,
        OtherCastsDamageSpell,
        Death,
        DebuffOnTarget,
        DebuffOnYou,
        DirtyFighting,
        FailsToAffectYou,
        GiveAndTake,
        HealKit,
        HealSelf,
        HealedByOther,
        SpellFizzle,
        SpellExpired,
        MissileAttackMissed,
        OthersCast,
        PeriodicHealing,
        PeriodicDamage,
        Philtre,
        RegenSpell,
        Salvage,
        StamRegenSpell,
        FieldRation,
        TestTag,
        YouCast,
        YouEvaded,
        YouFailToAffect,
        YouResistSpell,
        YourSpellResisted,
        Debuff,
        NpcTells,
        VendorTells,
        Unknown
    }
}

