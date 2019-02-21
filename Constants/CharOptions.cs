using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Constants {
    public enum CharOptions : uint {
        Undef = 0x0,
        AutoRepeatAttack = 0x2,
        IgnoreAllegianceRequests = 0x4,
        IgnoreFellowshipRequests = 0x8,
        AllowGive = 0x40,
        ViewCombatTarget = 0x80,
        ShowTooltips = 0x100,
        UseDeception = 0x200,
        ToggleRun = 0x400,
        StayInChatMode = 0x800,
        AdvancedCombatUI = 0x1000,
        AutoTarget = 0x2000,
        VividTargetingIndicator = 0x8000,
        DisableMostWeatherEffects = 0x10000,
        IgnoreTradeRequests = 0x20000,
        FellowshipShareXP = 0x40000,
        AcceptLootPermits = 0x80000,
        FellowshipShareLoot = 0x100000,
        SideBySideVitals = 0x200000,
        CoordinatesOnRadar = 0x400000,
        SpellDuration = 0x800000,
        DisableHouseRestrictionEffects = 0x2000000,
        DragItemOnPlayerOpensSecureTrade = 0x4000000,
        DisplayAllegianceLogonNotifications = 0x8000000,
        UseChargeAttack = 0x10000000,
        AutoAcceptFellowRequest = 0x20000000,
        HearAllegianceChat = 0x40000000,
        UseCraftSuccessDialog = 0x80000000,
        Default = 0x50C4A54A,
        FORCE_32_BIT = 0x7FFFFFFF,
    }
}
//Array values = Enum.GetValues(typeof(myEnum));
//
//foreach(MyEnum val in values ) {
//   Console.WriteLine(String.Format("{0}: {1}", Enum.GetName(typeof(MyEnum), val), val));
//}