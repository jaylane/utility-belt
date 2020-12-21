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
using UBLoader.Lib.Settings;
using VirindiViewService.Controls;
using System.IO;
using UtilityBelt.Views;

namespace UtilityBelt.Tools {
    [Name("Profiles")]
    public class Profiles : ToolBase {
        private HudCombo SettingsProfilesCombo;
        private HudButton SettingsProfileCopyTo;
        private HudButton SettingsProfileReset;
        private FileSystemWatcher profilesWatcher;
        private ConfirmationDialog confirmationPopup;
        private TextEditPopup textEditPopup;

        /// <summary>
        /// The default character settings path
        /// </summary>
        public string CharacterSettingsFile { get => Path.Combine(Util.GetCharacterDirectory(), "settings.json"); }
        /// <summary>
        /// Directory where profiles are stored
        /// </summary>
        public string ProfilesDirectory { get => Path.Combine(Util.GetPluginDirectory(), "profiles");  }
        /// <summary>
        /// The file path to the currently loaded settings profile
        /// </summary>
        public string SettingsProfilePath {
            get {
                if (SettingsProfile == "[character]")
                    return CharacterSettingsFile;
                else
                    return Path.Combine(ProfilesDirectory, $"{SettingsProfile}{SettingsProfileExtension}");
            }
        }
        public static readonly string SettingsProfileExtension = ".settings.json";

        #region Config
        [Summary("Settings profile. Choose [character] to use a private copy of settings for this character.")]
        public readonly CharacterState<string> SettingsProfile = new CharacterState<string>("[character]");
        #endregion

        public Profiles(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        public override void Init() {
            base.Init();

            SettingsProfilesCombo = (HudCombo)UB.MainView.view["SettingsProfileList"];
            SettingsProfileCopyTo = (HudButton)UB.MainView.view["SettingsProfileCopyTo"];
            SettingsProfileReset = (HudButton)UB.MainView.view["SettingsProfileReset"];

            SettingsProfilesCombo.Change += SettingsProfileList_Change;
            SettingsProfileCopyTo.Hit += SettingsProfileCopyTo_Hit;
            SettingsProfileReset.Hit += SettingsProfileReset_Hit;

            SettingsProfile.Changed += SettingsProfile_Changed;

            PopulateProfiles();
            SetupFileWatcher();
        }

        private void SettingsProfile_Changed(object sender, SettingChangedEventArgs e) {
            UB.Settings.SettingsPath = SettingsProfilePath;
            PopulateProfiles();
        }

        private void PopulateProfiles() {
            SettingsProfilesCombo.Clear();
            SettingsProfilesCombo.AddItem("[character]", "[character]");
            SettingsProfilesCombo.Current = 0;
            string[] profiles = Directory.GetFiles(ProfilesDirectory, $"*{SettingsProfileExtension}");
            List<string> foundProfileNames = new List<string>();
            foreach (var profile in profiles) {
                var name = Path.GetFileName(profile).Replace(SettingsProfileExtension, "");
                SettingsProfilesCombo.AddItem(name, name);
                if (name == SettingsProfile)
                    SettingsProfilesCombo.Current = SettingsProfilesCombo.Count - 1;
                foundProfileNames.Add(name);
            }
            if (SettingsProfile != "[character]" && !foundProfileNames.Contains(SettingsProfile)) {
                SettingsProfilesCombo.AddItem(SettingsProfile, SettingsProfile);
                SettingsProfilesCombo.Current = SettingsProfilesCombo.Count - 1;
            }
        }

        private void SetupFileWatcher() {
            profilesWatcher = new FileSystemWatcher();
            profilesWatcher.Path = ProfilesDirectory;
            profilesWatcher.Filter = $"*.json";

            profilesWatcher.Created += Profiles_Changed;
            profilesWatcher.Renamed += Profiles_Changed;
            profilesWatcher.Deleted += Profiles_Changed;

            profilesWatcher.EnableRaisingEvents = true;
        }

        private void Profiles_Changed(object sender, FileSystemEventArgs e) {
            PopulateProfiles();
        }

        private void SettingsProfileReset_Hit(object sender, EventArgs e) {
            if (confirmationPopup != null)
                confirmationPopup.Dispose();

            confirmationPopup = new ConfirmationDialog(UB.MainView.view, "Are you sure you want to clear this profile and set it to entirely default values?");
            confirmationPopup.ClickedOK += (s, ce) => {
                foreach (var setting in UB.Settings.GetAll()) {
                    if (setting.SettingType == SettingType.Profile)
                        setting.SetValue(setting.GetDefaultValue());
                }
                Logger.WriteToChat($"Reset profile: {SettingsProfile}");
            };
        }

        private void SettingsProfileCopyTo_Hit(object sender, EventArgs e) {
            if (textEditPopup != null)
                textEditPopup.Dispose();

            textEditPopup = new TextEditPopup(UB.MainView.view, "", $"Copy profile '{SettingsProfile}' to:");
            textEditPopup.ClickedOK += (s, te) => {
                if (string.IsNullOrEmpty(textEditPopup.Value)) {
                    Logger.Error($"Profile name must not be empty");
                    return;
                }

                var newProfilePath = Path.Combine(ProfilesDirectory, $"{textEditPopup.Value}{SettingsProfileExtension}");
                if (File.Exists(newProfilePath)) {
                    Logger.Error($"Profile already exists: {newProfilePath}");
                }
                else if (!File.Exists(SettingsProfilePath)) {
                    Logger.Error($"Existing profile does not exist: {SettingsProfilePath}");
                }
                else {
                    Logger.WriteToChat($"Copying profile '{SettingsProfile}' to new profile: {textEditPopup.Value}");
                    File.Copy(SettingsProfilePath, newProfilePath);
                    SettingsProfile.Value = textEditPopup.Value;
                }
            };
        }

        private void SettingsProfileList_Change(object sender, EventArgs e) {
            SettingsProfile.Value = ((HudStaticText)SettingsProfilesCombo[SettingsProfilesCombo.Current]).Text;
        }

        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    SettingsProfilesCombo.Change -= SettingsProfileList_Change;
                    SettingsProfileCopyTo.Hit -= SettingsProfileCopyTo_Hit;
                    SettingsProfileReset.Hit -= SettingsProfileReset_Hit;
                    SettingsProfile.Changed -= SettingsProfile_Changed;
                    profilesWatcher.Dispose();
                    if (textEditPopup != null) textEditPopup.Dispose();
                    if (confirmationPopup != null) confirmationPopup.Dispose();
                }
                disposedValue = true;
            }
        }
    }
}
