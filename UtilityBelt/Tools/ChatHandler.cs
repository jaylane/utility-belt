using System;
using Decal.Adapter;
using Decal.Adapter.Wrappers;
using Decal.Filters;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Chat;
using UtilityBelt.Service.Lib.Settings;


namespace UtilityBelt.Tools {
    public class MyBaseEventArgs : EventArgs {
        public ChatTextInterceptEventArgs DecalChatEvent { get; set; } // this is the original e, from decal chat handler
        public NetworkMessageEventArgs DecalNetworkEvent { get; set; } // this is the original e, from decal network event
        public ChatType ChatMessageType { get; set; }

        public MyBaseEventArgs(ref ChatTextInterceptEventArgs e, ChatType chatMessageType) {
            DecalChatEvent = e;
            ChatMessageType = chatMessageType;
        }

        public MyBaseEventArgs(ref NetworkMessageEventArgs e, ChatType chatMessageType) {
            DecalNetworkEvent = e;
            ChatMessageType = chatMessageType;
        }
    }

    [Name("ChatHandler")]
    public class ChatHandler : ToolBase {
        public string deathMessage = "";
        private List<string> deathMessages = new List<string>();
        public string filterme = "";
        private Match match;
        public event EventHandler<MyBaseEventArgs> ChatBoxMessage;
        private bool subscribedChat = false;
        private bool subscribedEcho = false;
        bool chatFilterEnabled = UtilityBeltPlugin.Instance.ChatFilter.Enabled;
        private string npcText = "";
        private string vendorText = "";
        private Dictionary<string, ISetting> filterSettingsCache = new Dictionary<string, ISetting>();


        public string attackVerb = "";
        public string spell = "";
        string[] damages = new string[] { "hit", "mangle", "slash", "cut", "scratch", "gore", "impale", "stab", "nick", "crush", "smash", "bash", "graze", "incinerate", "burn", "scorch", "singe", "freeze", "frost", "chill", "numb", "dissolve", "corrode", "sear", "blister", "blast", "jolt", "shock", "spark", "eradicate", "wither", "twist", "scar", "deplete", "siphon", "exhaust", "drain" };
        string[] debuffs = new string[] { "Cruath", "Equin", "Yanoi"     };
        string[] buffspells = new string[] { "Malar", "Helkas", "Boquar", "Puish" };
        string[] damagespells = new string[] { "Volae", "Jevak", "Zojak", "Tugak", "Slavu", "Feazh", "Quavosh" };
        string[] regens = new string[] { "Malar Zhapaj", "Malar Zhavik", "Puish" };

        //Attacks/Damage on you
        Regex DamageSpellOnYouRegex = new Regex(@"^(?!You ).*(?<attackVerb>.*) you for (?<damage>\d+) points with (?<spell>.*)\.$", RegexOptions.Compiled);
        Regex DamageMeleeOnYouRegex = new Regex(@"^(?!You ).* your (?<location>.*) for (?<damage>\d+) points? of ?(?<damageType>.*) damage\!.*$", RegexOptions.Compiled);
        Regex YouEvadedRegex = new Regex(@"^You evaded (?<mob>[\s\S]+)\!$", RegexOptions.Compiled);
        Regex YouResistSpellRegex = new Regex(@"^You resist.*$", RegexOptions.Compiled);
        Regex FailsToAffectYouRegex = new Regex(@"^.* fails to affect you.*$", RegexOptions.Compiled);
        Regex OthersCastRegex = new Regex(@"^(?!You ).*casts? .* you.*$", RegexOptions.Compiled);
        Regex PeriodicDamageRegex = new Regex(@"^You receive (?<damage>\d+) points of periodic.*damage.*$", RegexOptions.Compiled);

        //Your attacks on target
        Regex DamageOnTargetRegex = new Regex(@"^((Sneak Attack!|Recklessness!|Critical (H|h)it!|\s+)+)?You (?<attackVerb>\S+) (?<targetMob>[\S\s]+?) for (?<damage>\d+) points? (with|of) (?<spell>.*)(\.|\!)$", RegexOptions.Compiled);
        Regex YourSpellResistedRegex = new Regex(@"^.* resists your spell.*$", RegexOptions.Compiled);
        Regex MissileAttackMissedRegex = new Regex(@"^Your missile attack hit the environment\.$", RegexOptions.Compiled);

        //Misc
        Regex BurnedCompsRegex = new Regex(@"^The spell consumed the following components\:.*$", RegexOptions.Compiled);
        Regex DirtyFightingRegex = new Regex(@"^Dirty Fighting! .* delivers a .* to (?<targetMob>.*)\!$", RegexOptions.Compiled);
        Regex AetheriaSurgeRegex = new Regex(@"^Aetheria surges on.*$", RegexOptions.Compiled);
        Regex SpellExpiredRegex = new Regex(@".* has expired\.$", RegexOptions.Compiled);
        Regex CloakRegex = new Regex(@"^The cloak of.*$", RegexOptions.Compiled);
        Regex SalvageRegex = new Regex(@"^You obtain \d+ .* using your knowledge of Salvaging\.$", RegexOptions.Compiled);

        //You cast spells
        Regex YouSayRegex = new Regex(@"^You say, ""(?<spell>.*)""$", RegexOptions.Compiled);
        Regex YouCastRegex = new Regex(@"^You cast.*$", RegexOptions.Compiled);
        Regex YouFailToAffectRegex = new Regex(@"^You failed to affect.*$", RegexOptions.Compiled);
        Regex SpellFizzleRegex = new Regex(@"^Your spell fizzled.$", RegexOptions.Compiled);

        //Others cast spell
        Regex OthersSayRegex = new Regex(@"^(?<name>.*) says, ""(?<spell>.*)""$", RegexOptions.Compiled);

        //Recharge
        Regex GiveAndTakeRegex = new Regex(@"^You cast (?<spell>.*) on yourself and lose (?<givePoints>\d+) points of (?<give>.*) and also gain (?<takePoints>\d+) points of (?<take>.*)$", RegexOptions.Compiled); //This represents s2m, s2h, etc.
        Regex HealKitRegex = new Regex(@"^You heal yourself for (?<points>\d+) Health points. Your (?<kit>.*) has \d+ uses left.$", RegexOptions.Compiled);
        Regex PeriodicHealingRegex = new Regex(@"You receive (?<points>\d+) points of periodic healing.$", RegexOptions.Compiled);
        Regex HealedByOtherRegex = new Regex(@"(?<charName>.*) casts (?<spell>.*) and restores (?<points>\d+) points of your (?<gain>.*).$", RegexOptions.Compiled);
        Regex ConsumableRegex = new Regex(@"The (?<consumable>.*) restores (?<points>\d+) points of your (?<gain>.*).$", RegexOptions.Compiled);
        Regex HealSelfRegex = new Regex(@"^You cast (?<spell>.*) and restore (?<points>\d+) points of your (?<gain>.*).$", RegexOptions.Compiled);
        
        public ChatHandler(UtilityBeltPlugin ub, string name) : base(ub, name) {
            UtilityBeltPlugin.Instance.ChatFilter.Changed += ChatFilter_Changed;
        }


        public override void Init() {
            base.Init();
            bool chatFilterEnabled = UtilityBeltPlugin.Instance.ChatFilter.Enabled;
            if (!chatFilterEnabled) {
                Update_ChatSubscription(false);
                Update_EchoSubscription(false);
            }
            else {
                UpdateSubscriptions();
            }
        }

        private void ChatFilter_Changed(object sender, SettingChangedEventArgs e) {
            chatFilterEnabled = UtilityBeltPlugin.Instance.ChatFilter.Enabled;
            
            if (!chatFilterEnabled) {
                Update_ChatSubscription(false);
                Update_EchoSubscription(false);
            }
            else {
                UpdateSubscriptions();
            }
        }

        private bool chatSettingIsDefault() {
            int enabledFilterCount = 0;
            foreach (ISetting setting in UtilityBeltPlugin.Instance.ChatFilter.GetChildren()) {
                if (!setting.IsDefault) {
                    enabledFilterCount++;
                }
            }
            if (enabledFilterCount <= 0) {
                return true;
            }
            return false;
        }

        private bool echoSettingIsDefault() {
            int enabledFilterCount = 0;
            foreach (ISetting setting in UtilityBeltPlugin.Instance.ChatFilter.GetChildren()) {
                if (setting.Name == "Message" && !setting.IsDefault) {
                    enabledFilterCount++;
                }
                else {
                    foreach (ISetting s in setting.GetChildren()) {
                        if (s.Name == "DeathMessages") {
                            if (!s.IsDefault) {
                                enabledFilterCount++;
                            }
                        }
                    }
                }
            }
            if (enabledFilterCount <= 0) {
                return true;
            }
            return false;
        }


        private void Update_ChatSubscription(bool enabled) {
            if (subscribedChat && enabled) return;
            else if (!subscribedChat && !enabled) return;
            else if (!subscribedChat && enabled) {
                subscribedChat = true;
                UB.Core.ChatBoxMessage += Current_ChatBoxMessage;
            }
            else if (subscribedChat && !enabled) {
                subscribedChat = false;
                UB.Core.ChatBoxMessage -= Current_ChatBoxMessage;
            }
        }

        private void Update_EchoSubscription(bool enabled) {
            if (subscribedEcho && enabled) return;
            else if (!subscribedEcho && !enabled) return;
            else if (!subscribedEcho && enabled) {
                subscribedEcho = true;
                UB.Core.EchoFilter.ServerDispatch += EchoFilter_ServerDispatch;
            }
            else if (subscribedEcho && !enabled) {
                subscribedEcho = false;
                UB.Core.EchoFilter.ServerDispatch -= EchoFilter_ServerDispatch;
            }
        }

        private void UpdateSubscriptions() {
            if (chatSettingIsDefault()) {
                Update_ChatSubscription(false);
            }
            else if (!chatSettingIsDefault()) {
                Update_ChatSubscription(true);
            }
            if (echoSettingIsDefault()) {
                Update_EchoSubscription(false);
            }
            else if (!echoSettingIsDefault()) {
                Update_EchoSubscription(true);
            }
        }
        
        private void EchoFilter_ServerDispatch(object sender, Decal.Adapter.NetworkMessageEventArgs e) {
            try {
                switch (e.Message.Type) {
                    case 0xF7B0:
                        switch (e.Message.Value<int>("event")) {
                            case 0x02BD: // Communication_HearSpeech
                                npcText = "";
                                WorldObject msgSender = null;
                                msgSender = CoreManager.Current.WorldFilter[e.Message.Value<int>("sender")];
                                if (msgSender == null) {
                                    return;
                                }
                                if (msgSender.ObjectClass == ObjectClass.Npc)
                                    npcText = e.Message.Value<string>("text");
                                else if (msgSender.ObjectClass == ObjectClass.Vendor)
                                    vendorText = e.Message.Value<string>("text");
                                break;
                            case 0x01AD: // Combat_HandleAttackerNotificationEvent
                                deathMessage = e.Message.Value<string>("text").Trim();
                                if (!deathMessages.Contains(deathMessage)) {
                                    deathMessages.Add(deathMessage);
                                }
                                break;
                        }
                        break;
                }
            }
            catch (Exception ex) {
                Logger.LogException(ex);
            }
        }
               
        public void Current_ChatBoxMessage(object sender, ChatTextInterceptEventArgs e) {
            try {
                GetChatType(e);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public void GetChatType(ChatTextInterceptEventArgs e) {
            ChatType chatType = ChatType.Unknown;
            string damage = "";
            int d;
            string message = e.Text;
            if (DamageMeleeOnYouRegex.IsMatch(message)) {
                chatType = ChatType.DamageMeleeOnYou;
                match = DamageMeleeOnYouRegex.Match(e.Text.Trim());
                damage = match.Groups["damage"].Value;
                int.TryParse(damage, out d);
                chatType = ChatType.DamageMeleeOnYou;
            }
            else if (PeriodicDamageRegex.IsMatch(message)) {
                chatType = ChatType.PeriodicDamage;
                match = PeriodicDamageRegex.Match(e.Text.Trim());
                damage = match.Groups["damage"].Value;
                int.TryParse(damage, out d);
                chatType = ChatType.PeriodicDamage;
            }
            else if (DamageOnTargetRegex.IsMatch(message)) {
                match = DamageOnTargetRegex.Match(message.Trim());
                attackVerb = match.Groups["attackVerb"].Value;
                if (damages.Contains(attackVerb)) {
                    chatType = ChatType.DamageOnTarget;
                }
            }
            else if (YouSayRegex.IsMatch(message)) { //selfbuff
                match = YouSayRegex.Match(message.Trim());
                spell = match.Groups["spell"].Value;
                foreach (string buffspell in buffspells) {
                    if (spell.Contains(buffspell)) {
                        chatType = ChatType.SelfBuffs;
                    }
                }
                foreach (string debuff in debuffs) { //debuff
                    if (spell.Contains(debuff)) {
                        chatType = ChatType.Debuff;
                    }
                }
                foreach (string damagespell in damagespells) { //warspells
                    if (spell.Contains(damagespell)) {
                        chatType = ChatType.YouCastDamageSpell;
                    }
                }
            }
            else if (OthersSayRegex.IsMatch(message)) {
                match = OthersSayRegex.Match(message.Trim());
                spell = match.Groups["spell"].Value;
                foreach (string buffspell in buffspells) { //buffother
                    if (spell.Contains(buffspell)) {
                        foreach (string regen in regens) {
                            if (spell.Contains(regen)) {
                                chatType = ChatType.RegenSpell;
                            }
                            else {
                                chatType = ChatType.OtherBuffs;
                            }
                        }
                    }
                }
                foreach (string damagespell in damagespells) { //otherdamage
                    if (spell.Contains(damagespell)) {
                        chatType = ChatType.OtherCastsDamageSpell;
                    }
                }
            }
            else if (DamageSpellOnYouRegex.IsMatch(message)) {
                chatType = ChatType.DamageSpellOnYou;
                match = DamageSpellOnYouRegex.Match(e.Text.Trim());
                damage = match.Groups["damage"].Value;
                int.TryParse(damage, out d);
                chatType = ChatType.DamageSpellOnYou;
            }
            else if (HealKitRegex.IsMatch(message)) {
                chatType = ChatType.HealKit;
            }
            else if (YouEvadedRegex.IsMatch(message)) {
                chatType = ChatType.YouEvaded;
            }
            else if (BurnedCompsRegex.IsMatch(message)) {
                chatType = ChatType.BurnedComps;
            }
            else if (YouResistSpellRegex.IsMatch(message)) {
                chatType = ChatType.YouResistSpell;
            }
            else if (GiveAndTakeRegex.IsMatch(message)) {
                chatType = ChatType.GiveAndTake;
            }
            else if (ConsumableRegex.IsMatch(message)) {
                chatType = ChatType.Consumable;
            }
            else if (HealedByOtherRegex.IsMatch(message)) {
                chatType = ChatType.HealedByOther;
            }
            else if (HealSelfRegex.IsMatch(message)) {
                chatType = ChatType.HealSelf;
            }
            else if (YouCastRegex.IsMatch(message)) {
                chatType = ChatType.YouCast;
            }
            else if (OthersCastRegex.IsMatch(message)) {
                chatType = ChatType.OthersCast;
            }
            else if (YourSpellResistedRegex.IsMatch(message)) {
                chatType = ChatType.YourSpellResisted;
            }
            else if (MissileAttackMissedRegex.IsMatch(message)) {
                chatType = ChatType.MissileAttackMissed;
            }
            else if (YouFailToAffectRegex.IsMatch(message)) {
                chatType = ChatType.YouFailToAffect;
            }
            else if (FailsToAffectYouRegex.IsMatch(message)) {
                chatType = ChatType.FailsToAffectYou;
            }
            else if (SpellFizzleRegex.IsMatch(message)) {
                chatType = ChatType.SpellFizzle;
            }
            else if (DirtyFightingRegex.IsMatch(message)) {
                chatType = ChatType.DirtyFighting;
            }
            else if (AetheriaSurgeRegex.IsMatch(message)) {
                chatType = ChatType.AetheriaSurge;
            }
            else if (SpellExpiredRegex.IsMatch(message)) {
                chatType = ChatType.SpellExpired;
            }
            else if (CloakRegex.IsMatch(message)) {
                chatType = ChatType.Cloak;
            }
            else if (SalvageRegex.IsMatch(message)) {
                chatType = ChatType.Salvage;
            }
            else if (PeriodicHealingRegex.IsMatch(message)) {
                chatType = ChatType.PeriodicHealing;
            }
            else if (deathMessages.Count != 0 && deathMessages.Contains(message.Trim())) {
                chatType = ChatType.DeathMessages;
            }
            else if (!string.IsNullOrEmpty(npcText) && e.Text.Contains(npcText)) {
                npcText = "";
                chatType = ChatType.NpcTells;
            }
            else if (!string.IsNullOrEmpty(vendorText) && e.Text.Contains(vendorText)) {
                vendorText = "";
                chatType = ChatType.VendorTells;
            }
            if (chatType != ChatType.Unknown) {
                ChatBoxMessage?.Invoke(this, new MyBaseEventArgs(ref e, chatType));
            }

            chatType = ChatType.Unknown;
        }

        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {

                    UB.Core.ChatBoxMessage -= Current_ChatBoxMessage;
                    UB.Core.EchoFilter.ServerDispatch -= EchoFilter_ServerDispatch;
                    base.Dispose(disposing);
                }
                disposedValue = true;
            }
        }
    }
}
