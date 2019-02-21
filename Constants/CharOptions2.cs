using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Constants {
    public enum CharOptions2 : uint {
        Undef = 0x0,
        PersistentAtDay = 0x1,
        DisplayDateOfBirth = 0x2,
        DisplayChessRank = 0x4,
        DisplayFishingSkill = 0x8,
        DisplayNumberDeaths = 0x10,
        DisplayAge = 0x20,
        TimeStamp = 0x40,
        SalvageMultiple = 0x80,
        HearGeneralChat = 0x100,
        HearTradeChat = 0x200,
        HearLFGChat = 0x400,
        HearRoleplayChat = 0x800,
        AppearOffline = 0x1000,
        DisplayNumberCharacterTitles = 0x2000,
        MainPackPreferred = 0x4000,
        LeadMissileTargets = 0x8000,
        UseFastMissiles = 0x10000,
        FilterLanguage = 0x20000,
        ConfirmVolatileRareUse = 0x40000,
        HearSocietyChat = 0x80000,
        ShowHelm = 0x100000,
        DisableDistanceFog = 0x200000,
        UseMouseTurning = 0x400000,
        ShowCloak = 0x800000,
        LockUI = 0x1000000,
        Default = 0x948700,
        FORCE_32_BIT = 0x7FFFFFFF,
    }
}
