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
using System.Drawing;

namespace UtilityBelt.Tools {
    [Name("Profiles")]
    public class Profiles : ToolBase {
        private FileSystemWatcher profilesWatcher;
        private ConfirmationDialog confirmationPopup;
        private TextEditPopup textEditPopup;

        private HudCombo SettingsProfilesCombo;
        private HudButton SettingsProfileCopyTo;
        private HudButton SettingsProfileReset;

        private HudCombo ClientUIProfilesCombo;
        private HudButton ClientUIProfileCopyTo;
        private HudButton ClientUIProfileReset;
        private HudButton ClientUIProfileImport;

        /// <summary>
        /// Directory where profiles are stored
        /// </summary>
        public string ProfilesDirectory { get => Path.Combine(Util.GetPluginDirectory(), "profiles"); }

        /// <summary>
        /// The default character settings path
        /// </summary>
        public string CharacterSettingsFile { get => Path.Combine(Util.GetCharacterDirectory(), SettingsProfileExtension); }

        /// <summary>
        /// The default character client ui path
        /// </summary>
        public string CharacterClientUIFile { get => Path.Combine(Util.GetCharacterDirectory(), ClientUIProfileExtension); }

        /// <summary>
        /// The file path to the currently loaded settings profile
        /// </summary>
        public string SettingsProfilePath {
            get {
                if (SelectedSettingsProfile == "[character]")
                    return CharacterSettingsFile;
                else
                    return Path.Combine(ProfilesDirectory, $"{SelectedSettingsProfile}.{SettingsProfileExtension}");
            }
        }

        /// <summary>
        /// The file path to the currently loaded settings profile
        /// </summary>
        public string ClientUIProfilePath {
            get {
                if (SelectedClientUIProfile == "[character]")
                    return CharacterClientUIFile;
                else
                    return Path.Combine(ProfilesDirectory, $"{SelectedClientUIProfile}.{ClientUIProfileExtension}");
            }
        }

        public static readonly string SettingsProfileExtension = "settings.json";
        public static readonly string ClientUIProfileExtension = "clientui.json";

        #region Config
        [Summary("Settings profile. Choose [character] to use a private copy of settings for this character.")]
        public readonly CharacterState<string> SelectedSettingsProfile = new CharacterState<string>("[character]");

        [Summary("Client UI profile. Choose [character] to use a private copy of settings for this character.")]
        public readonly CharacterState<string> SelectedClientUIProfile = new CharacterState<string>("[character]");

        [Summary("Client UI data for the current profile")]
        public readonly ClientUIProfile ClientUI = new ClientUIProfile();
        #endregion

        public Profiles(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        public override void Init() {
            base.Init();

            SettingsProfilesCombo = (HudCombo)UB.MainView.view["SettingsProfilesCombo"];
            SettingsProfileCopyTo = (HudButton)UB.MainView.view["SettingsProfileCopyTo"];
            SettingsProfileReset = (HudButton)UB.MainView.view["SettingsProfileReset"];

            ClientUIProfilesCombo = (HudCombo)UB.MainView.view["ClientUIProfilesCombo"];
            ClientUIProfileCopyTo = (HudButton)UB.MainView.view["ClientUIProfileCopyTo"];
            ClientUIProfileReset = (HudButton)UB.MainView.view["ClientUIProfileReset"];
            ClientUIProfileImport = (HudButton)UB.MainView.view["ClientUIProfileImport"];

            SettingsProfilesCombo.Change += SettingsProfilesCombo_Change;
            SettingsProfileCopyTo.Hit += SettingsProfileCopyTo_Hit;
            SettingsProfileReset.Hit += SettingsProfileReset_Hit;

            ClientUIProfilesCombo.Change += ClientUIProfilesCombo_Change;
            ClientUIProfileCopyTo.Hit += ClientUIProfileCopyTo_Hit;
            ClientUIProfileReset.Hit += ClientUIProfileReset_Hit;
            ClientUIProfileImport.Hit += ClientUIProfileImport_Hit;

            SelectedSettingsProfile.Changed += SettingsProfile_Changed;
            SelectedClientUIProfile.Changed += SelectedClientUIProfile_Changed;

            UB.ClientUISettings.Changed += ClientUI_Changed;

            PopulateProfiles();
            SetupFileWatcher();
            RestoreClientUI();
        }

        private void SettingsProfile_Changed(object sender, SettingChangedEventArgs e) {
            UB.Settings.SettingsPath = SettingsProfilePath;
            PopulateProfiles(SettingsProfileExtension, SettingsProfilesCombo, SelectedSettingsProfile);
        }

        private void SelectedClientUIProfile_Changed(object sender, SettingChangedEventArgs e) {
            UB.ClientUISettings.SettingsPath = ClientUIProfilePath;
            PopulateProfiles(ClientUIProfileExtension, ClientUIProfilesCombo, SelectedClientUIProfile);
        }

        private void ClientUI_Changed(object sender, SettingChangedEventArgs e) {
            if (e.Setting.Parent is ClientUIProfile.UIElementVector v) {
                RestoreClientUIElement(v);
            }
        }

        private void RestoreClientUI() {
            foreach (var element in ClientUI.GetChildren()) {
                if (element is ClientUIProfile.UIElementVector vector) {
                    RestoreClientUIElement(vector);
                }
            }
        }

        private void RestoreClientUIElement(ClientUIProfile.UIElementVector vector) {
            if (!vector.IsDefault) {
                var t = UBHelper.Core.GetElementPosition(vector.UIElement);
                var x = vector.X.IsDefault ? t.X : vector.X;
                var y = vector.Y.IsDefault ? t.Y : vector.Y;
                var width = vector.Width.IsDefault ? t.Width : vector.Width;
                var height = vector.Height.IsDefault ? t.Height : vector.Height;
                UBHelper.Core.MoveElement(vector.UIElement, new Rectangle(x, y, width, height));
            }
        }

        private void PopulateProfiles() {
            PopulateProfiles(SettingsProfileExtension, SettingsProfilesCombo, SelectedSettingsProfile);
            PopulateProfiles(ClientUIProfileExtension, ClientUIProfilesCombo, SelectedClientUIProfile);
        }

        private void PopulateProfiles(string profileExtension, HudCombo profilesCombo, string selected) {
            profilesCombo.Clear();
            profilesCombo.AddItem("[character]", "[character]");
            profilesCombo.Current = 0;
            string[] profiles = Directory.GetFiles(ProfilesDirectory, $"*.{profileExtension}");
            List<string> foundProfileNames = new List<string>();
            foreach (var profile in profiles) {
                var name = Path.GetFileName(profile).Replace("." + profileExtension, "");
                profilesCombo.AddItem(name, name);
                if (name == selected)
                    profilesCombo.Current = profilesCombo.Count - 1;
                foundProfileNames.Add(name);
            }
            if (selected != "[character]" && !foundProfileNames.Contains(selected)) {
                profilesCombo.AddItem(selected, selected);
                profilesCombo.Current = profilesCombo.Count - 1;
            }
        }

        private void SetupFileWatcher() {
            profilesWatcher = new FileSystemWatcher();
            profilesWatcher.Path = ProfilesDirectory;
            profilesWatcher.Filter = $"*.json";

            profilesWatcher.Created += ProfilesWatcher_Changed;
            profilesWatcher.Renamed += ProfilesWatcher_Changed;
            profilesWatcher.Deleted += ProfilesWatcher_Changed;

            profilesWatcher.EnableRaisingEvents = true;
        }

        private void ProfilesWatcher_Changed(object sender, FileSystemEventArgs e) {
            PopulateProfiles();
        }

        private void SettingsProfileReset_Hit(object sender, EventArgs e) {
            ResetProfile(SettingType.Profile, UB.Settings, SelectedSettingsProfile);
        }

        private void ClientUIProfileReset_Hit(object sender, EventArgs e) {
            ResetProfile(SettingType.ClientUI, UB.ClientUISettings, SelectedClientUIProfile);
        }

        private void SettingsProfileCopyTo_Hit(object sender, EventArgs e) {
            CopyProfile(SelectedSettingsProfile, SettingsProfilePath, (v) => {
                return Path.Combine(ProfilesDirectory, $"{v}.{SettingsProfileExtension}");
            });
        }

        private void ClientUIProfileCopyTo_Hit(object sender, EventArgs e) {
            CopyProfile(SelectedClientUIProfile, ClientUIProfilePath, (v) => {
                return Path.Combine(ProfilesDirectory, $"{v}.{ClientUIProfileExtension}");
            });
        }

        private void SettingsProfilesCombo_Change(object sender, EventArgs e) {
            SelectedSettingsProfile.Value = ((HudStaticText)SettingsProfilesCombo[SettingsProfilesCombo.Current]).Text;
        }

        private void ClientUIProfilesCombo_Change(object sender, EventArgs e) {
            SelectedClientUIProfile.Value = ((HudStaticText)ClientUIProfilesCombo[ClientUIProfilesCombo.Current]).Text;
        }

        private void ClientUIProfileImport_Hit(object sender, EventArgs e) {
            foreach (int i in Enum.GetValues(typeof(UBHelper.UIElement))) {
                System.Drawing.Rectangle t = UBHelper.Core.GetElementPosition((UBHelper.UIElement)i);
                var field = ClientUI.GetType().GetField(((UBHelper.UIElement)i).ToString(), Settings.BindingFlags);
                if (field != null) {
                    ((ISetting)field.FieldType.GetField("X", Settings.BindingFlags).GetValue(field.GetValue(ClientUI))).SetValue(t.X);
                    ((ISetting)field.FieldType.GetField("Y", Settings.BindingFlags).GetValue(field.GetValue(ClientUI))).SetValue(t.Y);
                    ((ISetting)field.FieldType.GetField("Width", Settings.BindingFlags).GetValue(field.GetValue(ClientUI))).SetValue(t.Width);
                    ((ISetting)field.FieldType.GetField("Height", Settings.BindingFlags).GetValue(field.GetValue(ClientUI))).SetValue(t.Height);
                }
            }
            Logger.WriteToChat($"Imported current ClientUI into profile '{SelectedClientUIProfile}'");
        }

        private void CopyProfile(ISetting profileSetting, string profilePath, Func<string, string> calcNewProfilePath) {
            if (textEditPopup != null)
                textEditPopup.Dispose();

            textEditPopup = new TextEditPopup(UB.MainView.view, "", $"Copy profile '{profileSetting.GetValue()}' to:");
            textEditPopup.ClickedOK += (s, te) => {
                if (string.IsNullOrEmpty(textEditPopup.Value)) {
                    Logger.Error($"Profile name must not be empty");
                    return;
                }

                var newProfilePath = calcNewProfilePath(textEditPopup.Value);
                if (File.Exists(newProfilePath)) {
                    Logger.Error($"Profile already exists: {newProfilePath}");
                }
                else if (!File.Exists(SettingsProfilePath)) {
                    Logger.Error($"Existing profile does not exist: {profilePath}");
                }
                else {
                    Logger.WriteToChat($"Copying profile '{profileSetting.GetValue()}' to new profile: {textEditPopup.Value}");
                    File.Copy(profilePath, newProfilePath);
                    profileSetting.SetValue(textEditPopup.Value);
                }
            };
        }

        private void ResetProfile(SettingType settingType, Settings settings, string profileName) {
            if (confirmationPopup != null)
                confirmationPopup.Dispose();

            confirmationPopup = new ConfirmationDialog(UB.MainView.view, "Are you sure you want to clear this profile and set it to entirely default values?");
            confirmationPopup.ClickedOK += (s, ce) => {
                foreach (var setting in settings.GetAll()) {
                    if (setting.SettingType == settingType)
                        setting.SetValue(setting.GetDefaultValue());
                }
                Logger.WriteToChat($"Reset profile to defaults: {profileName}");
            };
        }

        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    SettingsProfilesCombo.Change -= SettingsProfilesCombo_Change;
                    SettingsProfileCopyTo.Hit -= SettingsProfileCopyTo_Hit;
                    SettingsProfileReset.Hit -= SettingsProfileReset_Hit;
                    ClientUIProfilesCombo.Change -= ClientUIProfilesCombo_Change;
                    ClientUIProfileCopyTo.Hit -= ClientUIProfileCopyTo_Hit;
                    ClientUIProfileReset.Hit -= ClientUIProfileReset_Hit;
                    SelectedSettingsProfile.Changed -= SettingsProfile_Changed;
                    SelectedClientUIProfile.Changed -= SelectedClientUIProfile_Changed;
                    ClientUI.Changed -= ClientUI_Changed;
                    if (profilesWatcher != null) profilesWatcher.Dispose();
                    if (textEditPopup != null) textEditPopup.Dispose();
                    if (confirmationPopup != null) confirmationPopup.Dispose();
                }
                disposedValue = true;
            }
        }
    }
}
