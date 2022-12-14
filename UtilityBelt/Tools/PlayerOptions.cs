using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Settings;
using UtilityBelt.Service.Lib.Settings;
using VirindiViewService.Controls;
using System.IO;
using UtilityBelt.Views;
using System.Drawing;

namespace UtilityBelt.Tools {
    [Name("PlayerOptions")]
    public class PlayerOptions : ToolBase {
        internal HudCombo CharacterOptionsProfilesCombo;
        //private HudButton CharacterOptionsProfileCopyTo;
        //private HudButton CharacterOptionsProfileReset;
        //private HudButton CharacterOptionsProfileImport;

        /// <summary>
        /// The file path to the currently loaded settings profile
        /// </summary>
        public string CharacterOptionsProfilePath {
            get {
                if (Profile == "[character]")
                    return Path.Combine(Util.GetCharacterDirectory(), CharacterOptionsProfileExtension);
                else
                    return Path.Combine(Util.GetProfilesDirectory(), $"{Profile}.{CharacterOptionsProfileExtension}");
            }
        }

        public static readonly string CharacterOptionsProfileExtension = "playeroptions.json";

        #region Config
        [Summary("Character options profile. Choose [character] to use a private copy of settings for this character.")]
        public readonly CharacterState<string> Profile = new CharacterState<string>("[character]");

        public class PlayerOptionsSetting : ISetting {
            public class UserInterfaceBehaviorSettings : ISetting {
                [Summary("Keep combat targets in view")]
                public readonly Setting<bool?> ViewCombatTarget = new Setting<bool?>(new Nullable<bool>());

                [Summary("Salvage multiple materials at once")]
                public readonly Setting<bool?> SalvageMultiple = new Setting<bool?>(new Nullable<bool>());

                [Summary("Use main pack as default for picking up items")]
                public readonly Setting<bool?> MainPackPreferred = new Setting<bool?>(new Nullable<bool>());

                [Summary("Use mouse turning")]
                public readonly Setting<bool?> UseMouseTurning = new Setting<bool?>(new Nullable<bool>());

                [Summary("Lock UI")]
                public readonly Setting<bool?> LockUI = new Setting<bool?>(new Nullable<bool>());
            }

            public class UserInterfaceDisplaySettings : ISetting {
                [Summary("Vivid target indicator")]
                public readonly Setting<bool?> VividTargetingIndicator = new Setting<bool?>(new Nullable<bool>());

                [Summary("Display 3D tooltips")]
                public readonly Setting<bool?> ShowTooltips = new Setting<bool?>(new Nullable<bool>());

                [Summary("Show coordinates by the radar")]
                public readonly Setting<bool?> CoordinatesOnRadar = new Setting<bool?>(new Nullable<bool>());

                [Summary("Side by side vitals")]
                public readonly Setting<bool?> SideBySideVitals = new Setting<bool?>(new Nullable<bool>());

                [Summary("Display spell durations")]
                public readonly Setting<bool?> SpellDuration = new Setting<bool?>(new Nullable<bool>());

                [Summary("Disable most weather effects")]
                public readonly Setting<bool?> DisableMostWeatherEffects = new Setting<bool?>(new Nullable<bool>());

                [Summary("Disable distance fog")]
                public readonly Setting<bool?> DisableDistanceFog = new Setting<bool?>(new Nullable<bool>());

                [Summary("Always daylight outdoors")]
                public readonly Setting<bool?> PersistentAtDay = new Setting<bool?>(new Nullable<bool>());

                [Summary("Disable house restriction effects")]
                public readonly Setting<bool?> DisableHouseRestrictionEffects = new Setting<bool?>(new Nullable<bool>());

                [Summary("Use crafting chance of success dialog")]
                public readonly Setting<bool?> UseCraftSuccessDialog = new Setting<bool?>(new Nullable<bool>());

                [Summary("Confirm use of rare gems")]
                public readonly Setting<bool?> ConfirmVolatileRareUse = new Setting<bool?>(new Nullable<bool>());

                [Summary("Display timestamps")]
                public readonly Setting<bool?> DisplayTimeStamps = new Setting<bool?>(new Nullable<bool>());

                [Summary("Filter language")]
                public readonly Setting<bool?> FilterLanguage = new Setting<bool?>(new Nullable<bool>());

                [Summary("Show your healm or head gear")]
                public readonly Setting<bool?> ShowHelm = new Setting<bool?>(new Nullable<bool>());

                [Summary("Show your cloak")]
                public readonly Setting<bool?> ShowCloak = new Setting<bool?>(new Nullable<bool>());
            }

            public class GroupingSettings : ISetting {
                [Summary("Ignore allegiance requests")]
                public readonly Setting<bool?> IgnoreAllegianceRequests = new Setting<bool?>(new Nullable<bool>());

                [Summary("Ignore fellowship requests")]
                public readonly Setting<bool?> IgnoreFellowshipRequests = new Setting<bool?>(new Nullable<bool>());

                [Summary("Show allegiance logins")]
                public readonly Setting<bool?> DisplayAllegianceLogonNotifications = new Setting<bool?>(new Nullable<bool>());

                [Summary("Share fellowship experience and luminance")]
                public readonly Setting<bool?> FellowshipShareXP = new Setting<bool?>(new Nullable<bool>());

                [Summary("Share fellowship loot")]
                public readonly Setting<bool?> FellowshipShareLoot = new Setting<bool?>(new Nullable<bool>());

                [Summary("Automatically accept fellowship requests")]
                public readonly Setting<bool?> FellowshipAutoAcceptRequests = new Setting<bool?>(new Nullable<bool>());
            }

            public class OtherPlayersSettings : ISetting {
                [Summary("Accept corpse looting permissions")]
                public readonly Setting<bool?> AcceptLootPermits = new Setting<bool?>(new Nullable<bool>());

                [Summary("Attempt to deceive other players")]
                public readonly Setting<bool?> UseDeception = new Setting<bool?>(new Nullable<bool>());

                [Summary("Let other players give you items")]
                public readonly Setting<bool?> AllowGive = new Setting<bool?>(new Nullable<bool>());

                [Summary("Ignore trade requests")]
                public readonly Setting<bool?> IgnoreTradeRequests = new Setting<bool?>(new Nullable<bool>());

                [Summary("Drag item to player opens trade")]
                public readonly Setting<bool?> DragItemOnPlayerOpensSecureTrade = new Setting<bool?>(new Nullable<bool>());

                [Summary("Allow others to see your date of birth")]
                public readonly Setting<bool?> DisplayDateOfBirth = new Setting<bool?>(new Nullable<bool>());

                [Summary("Allow others to see your age")]
                public readonly Setting<bool?> DisplayAge = new Setting<bool?>(new Nullable<bool>());

                [Summary("Allow others to see your chess rank")]
                public readonly Setting<bool?> DisplayChessRank = new Setting<bool?>(new Nullable<bool>());

                [Summary("Allow others to see your fishing skill")]
                public readonly Setting<bool?> DisplayFishingSkill = new Setting<bool?>(new Nullable<bool>());

                [Summary("Allow others to see your number of deaths")]
                public readonly Setting<bool?> DisplayNumberDeaths = new Setting<bool?>(new Nullable<bool>());

                [Summary("Allow others to see your number of titles")]
                public readonly Setting<bool?> DisplayNumberCharacterTitles = new Setting<bool?>(new Nullable<bool>());
            }

            public class CharacterBehaviorSettings : ISetting {
                [Summary("Run as default movement")]
                public readonly Setting<bool?> ToggleRun = new Setting<bool?>(new Nullable<bool>());

                [Summary("Advanced combat interface")]
                public readonly Setting<bool?> AdvancedCombatUI = new Setting<bool?>(new Nullable<bool>());

                [Summary("Auto target")]
                public readonly Setting<bool?> AutoTarget = new Setting<bool?>(new Nullable<bool>());

                [Summary("Automatically repeat attacks")]
                public readonly Setting<bool?> AutoRepeatAttack = new Setting<bool?>(new Nullable<bool>());

                [Summary("Use charge attack")]
                public readonly Setting<bool?> UseChargeAttack = new Setting<bool?>(new Nullable<bool>());

                [Summary("Lead missile targets")]
                public readonly Setting<bool?> LeadMissileTargets = new Setting<bool?>(new Nullable<bool>());

                [Summary("Use fast missiles")]
                public readonly Setting<bool?> UseFastMissiles = new Setting<bool?>(new Nullable<bool>());
            }

            public class ChatSettings : ISetting {
                [Summary("Stay in chat mode after sending a message")]
                public readonly Setting<bool?> StayInChatMode = new Setting<bool?>(new Nullable<bool>());

                [Summary("Listen to Allegiance chat")]
                public readonly Setting<bool?> HearAllegianceChat = new Setting<bool?>(new Nullable<bool>());

                [Summary("Listen to General chat")]
                public readonly Setting<bool?> HearGeneralChat = new Setting<bool?>(new Nullable<bool>());

                [Summary("Listen to Trade chat")]
                public readonly Setting<bool?> HearTradeChat = new Setting<bool?>(new Nullable<bool>());

                [Summary("Listen to LFG chat")]
                public readonly Setting<bool?> HearLFGChat = new Setting<bool?>(new Nullable<bool>());

                [Summary("Listen to Roleplay chat")]
                public readonly Setting<bool?> HearRoleplayChat = new Setting<bool?>(new Nullable<bool>());

                [Summary("Listen to Society chat")]
                public readonly Setting<bool?> HearSocietyChat = new Setting<bool?>(new Nullable<bool>());

                [Summary("Appear offline")]
                public readonly Setting<bool?> AppearOffline = new Setting<bool?>(new Nullable<bool>());
            }

            [Summary("User Interface Behavior")]
            public readonly UserInterfaceBehaviorSettings UserInterfaceBehavior = new UserInterfaceBehaviorSettings();

            [Summary("User Interface Display")]
            public readonly UserInterfaceDisplaySettings UserInterfaceDisplay = new UserInterfaceDisplaySettings();

            [Summary("Grouping / Fellowships")]
            public readonly GroupingSettings Grouping = new GroupingSettings();

            [Summary("Other Players")]
            public readonly OtherPlayersSettings OtherPlayers = new OtherPlayersSettings();

            [Summary("Character Behavior")]
            public readonly CharacterBehaviorSettings CharacterBehavior = new CharacterBehaviorSettings();

            [Summary("Chat")]
            public readonly ChatSettings Chat = new ChatSettings();

            public PlayerOptionsSetting() {
                SettingType = SettingType.CharacterSettings;
            }
        }

        public readonly PlayerOptionsSetting PlayerOption = new PlayerOptionsSetting();
        #endregion

        #region Commands
        #region /ub playeroption (list|<option> {on | true | off | false})"
        [Summary("Turns on/off acclient player options.")]
        [Usage("/ub playeroption (list|<option> {on | true | off | false})")]
        [Example("/ub playeroption AutoRepeatAttack on", "Enables the AutoRepeatAttack player option.")]
        [CommandPattern("playeroption", @"^(?<params>.+( on| off| true| false)?)$")]
        public void DoPlayerOption(string _, Match args) {
            UB_playeroption(args.Groups["params"].Value);
        }
        public void UB_playeroption(string parameters) {
            string[] p = parameters.Split(' ');
            if (parameters.Equals("list")) {
                Logger.WriteToChat($"Valid values are: {string.Join(", ", Enum.GetNames(typeof(UBHelper.Player.PlayerOption)))}");
                return;
            }
            if (p.Length != 2) {
                Logger.Error($"Usage: /ub playeroption <option> <on/true|off/false>");
                return;
            }
            int option;
            try {
                option = (int)Enum.Parse(typeof(UBHelper.Player.PlayerOption), p[0], true);
            }
            catch {
                Logger.Error($"Invalid option. Valid values are: {string.Join(", ", Enum.GetNames(typeof(UBHelper.Player.PlayerOption)))}");
                return;
            }
            bool value = false;
            string inval = p[1].ToLower();
            if (inval.Equals("on") || inval.Equals("true"))
                value = true;

            UBHelper.Player.SetOption((UBHelper.Player.PlayerOption)option, value);
            Logger.WriteToChat($"Setting {(UBHelper.Player.PlayerOption)option} = {value}");
        }
        #endregion
        #endregion Commands

        public PlayerOptions(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        public override void Init() {
            base.Init();
            /*
            CharacterOptionsProfilesCombo = (HudCombo)UB.MainView.view["CharacterOptionsProfilesCombo"];
            CharacterOptionsProfileCopyTo = (HudButton)UB.MainView.view["CharacterOptionsProfileCopyTo"];
            CharacterOptionsProfileReset = (HudButton)UB.MainView.view["CharacterOptionsProfileReset"];
            CharacterOptionsProfileImport = (HudButton)UB.MainView.view["CharacterOptionsProfileImport"];

            CharacterOptionsProfilesCombo.Change += CharacterOptionsProfilesCombo_Change;
            CharacterOptionsProfileCopyTo.Hit += CharacterOptionsProfileCopyTo_Hit;
            CharacterOptionsProfileReset.Hit += CharacterOptionsProfileReset_Hit;
            CharacterOptionsProfileImport.Hit += CharacterOptionsProfileImport_Hit;

            Profile.Changed += SelectedCharacterOptionsProfile_Changed;
            UB.PlayerOptionsSettings.Changed += PlayerOptionsSettings_Changed;

            UB.MainView.PopulateProfiles(CharacterOptionsProfileExtension, CharacterOptionsProfilesCombo, Profile);
            RestoreOptions();
            */
        }

        private void PlayerOptionsSettings_Changed(object sender, SettingChangedEventArgs e) {
            RestoreOption(e.Setting);
        }

        //private void RestoreOptions() {
        //    foreach (var categoryField in PlayerOption.GetType().GetFields(Settings.BindingFlags)) {
        //        var category = categoryField.GetValue(PlayerOption);
        //        foreach (var optionField in categoryField.FieldType.GetFields(Settings.BindingFlags)) {
        //            RestoreOption((ISetting)optionField.GetValue(category));
        //        }
        //    }
        //    UBHelper.Player.SaveOptions();
        //}

        private void RestoreOption(ISetting setting) {
            try {
                if (setting.IsDefault)
                    return;
                var t = (UBHelper.Player.PlayerOption)Enum.Parse(typeof(UBHelper.Player.PlayerOption), setting.Name);
                //UBHelper.Player.SetOption(t, (bool)setting.GetValue(), false);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void SelectedCharacterOptionsProfile_Changed(object sender, SettingChangedEventArgs e) {
            UB.PlayerOptionsSettings.SettingsPath = CharacterOptionsProfilePath;
            UB.MainView.PopulateProfiles(CharacterOptionsProfileExtension, CharacterOptionsProfilesCombo, Profile);
        }

        private void CharacterOptionsProfileReset_Hit(object sender, EventArgs e) {
            UB.MainView.ResetProfile(SettingType.CharacterSettings, UB.PlayerOptionsSettings, Profile);
        }

        private void CharacterOptionsProfileCopyTo_Hit(object sender, EventArgs e) {
            UB.MainView.CopyProfile(Profile, CharacterOptionsProfilePath, (v) => {
                return Path.Combine(Util.GetProfilesDirectory(), $"{v}.{CharacterOptionsProfileExtension}");
            });
        }

        private void CharacterOptionsProfilesCombo_Change(object sender, EventArgs e) {
            Profile.Value = ((HudStaticText)CharacterOptionsProfilesCombo[CharacterOptionsProfilesCombo.Current]).Text;
        }

        private void CharacterOptionsProfileImport_Hit(object sender, EventArgs e) {
            UB.MainView.ImportProfile(() => {
                foreach (int i in Enum.GetValues(typeof(UBHelper.Player.PlayerOption))) {
                    var optionName = ((UBHelper.Player.PlayerOption)i).ToString();
                    var found = false;
                    foreach (var categoryField in PlayerOption.GetType().GetFields(Settings.BindingFlags)) {
                        var category = categoryField.GetValue(PlayerOption);
                        foreach (var optionField in categoryField.FieldType.GetFields(Settings.BindingFlags)) {
                            var option = (ISetting)optionField.GetValue(category);
                            if (option.Name == optionName) {
                                option.SetValue(UBHelper.Player.GetOption((UBHelper.Player.PlayerOption)i));
                                found = true;
                                break;
                            }
                        }
                        if (found)
                            break;
                    }
                }
                Logger.WriteToChat($"Imported current PlayerOptions into profile '{Profile}'");
                return true;
            });
        }

        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    /*
                    CharacterOptionsProfilesCombo.Change -= CharacterOptionsProfilesCombo_Change;
                    CharacterOptionsProfileCopyTo.Hit -= CharacterOptionsProfileCopyTo_Hit;
                    CharacterOptionsProfileReset.Hit -= CharacterOptionsProfileReset_Hit;
                    Profile.Changed -= SelectedCharacterOptionsProfile_Changed;
                    UB.PlayerOptionsSettings.Changed -= PlayerOptionsSettings_Changed;
                    */
                }
                disposedValue = true;
            }
        }
    }
}
