using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Timers;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Settings;
using VirindiViewService;
using VirindiViewService.Controls;
using VirindiViewService.XMLParsers;
using UBLoader.Lib.Settings;
using Hellosam.Net.Collections;

namespace UtilityBelt.Views {
    public class MainView : BaseView {
        private Timer timer;
        private FileSystemWatcher profilesWatcher;
        private ConfirmationDialog confirmationPopup;
        private TextEditPopup textEditPopup;

        private HudCombo SettingsProfilesCombo;
        private HudButton SettingsProfileCopyTo;
        private HudButton SettingsProfileReset;

        private HudButton CheckForUpdate;
        internal HudButton ExportPCap;
        private ACImage icon;
        private SettingsView settingsView = null;
        private HudButton SettingsButton;

        private readonly Dictionary<string, string> buttons = new Dictionary<string, string>() {
                    { "AutoVendorEnable", "AutoVendor.Enabled" },
                    { "AutoVendorTestMode", "AutoVendor.TestMode" },
                    { "AutoTradeEnable", "AutoTrade.Enabled" },
                    { "AutoTradeTestMode", "AutoTrade.TestMode" },
                    { "DungeonMapsEnabled", "DungeonMaps.Enabled" },
                    { "NameTagsEnabled", "Nametags.Enabled" },
                    { "VideoPatchEnabled", "Plugin.VideoPatch" },
                    { "VideoPatchFocusEnabled", "Plugin.VideoPatchFocus" },
                    { "VisualNavEnabled", "VisualNav.Enabled" },
                    { "VitalSharingEnabled", "VTank.VitalSharing" },
                    { "ArrowEnabled", "Arrow.Enabled" },
                    { "DerethTimeEnabled", "DerethTime.Enabled" },
                    { "LandscapeMapsEnabled", "LandscapeMaps.Enabled" },
                    { "Debug", "Plugin.Debug" }
                };

        public MainView(UtilityBeltPlugin ub) : base(ub) {
            CreateFromXMLResource("UtilityBelt.Views.MainView.xml");
        }

        public void Init() {
            try {
                view.Location = new Point(
                    UB.Plugin.WindowPositionX,
                    UB.Plugin.WindowPositionY
                );

                timer = new Timer(2000);

                timer.Elapsed += (s, e) => {
                    UB.Plugin.WindowPositionX.Value = view.Location.X;
                    UB.Plugin.WindowPositionY.Value = view.Location.Y;
                    timer.Stop();
                };

                view.Moved += (s, e) => {
                    if (timer.Enabled) timer.Stop();
                    timer.Start();
                };

                UB.Plugin.WindowPositionX.Changed += WindowPosition_Changed;
                UB.Plugin.WindowPositionY.Changed += WindowPosition_Changed;

                CheckForUpdate = (HudButton)view["CheckForUpdate"];
                ExportPCap = (HudButton)view["ExportPCap"];
                SettingsButton = (HudButton)view["Settings"];
                SettingsProfilesCombo = (HudCombo)view["SettingsProfilesCombo"];
                SettingsProfileCopyTo = (HudButton)view["SettingsProfileCopyTo"];
                SettingsProfileReset = (HudButton)view["SettingsProfileReset"];

                SettingsProfilesCombo.Change += SettingsProfilesCombo_Change;
                SettingsProfileCopyTo.Hit += SettingsProfileCopyTo_Hit;
                SettingsProfileReset.Hit += SettingsProfileReset_Hit;

                CheckForUpdate.Hit += CheckForUpdate_Hit;
                ExportPCap.Hit += ExportPCap_Hit;
                SettingsButton.Hit += SettingsButton_Hit;
                UB.Plugin.PCap.Changed += PCap_Changed;
                UB.Plugin.SettingsProfile.Changed += SettingsProfile_Changed;

                foreach (var kv in buttons) {
                    UpdateButton(kv);
                    var hudButton = (HudButton)view[kv.Key];
                    var setting = GetSettingPropFromText(kv.Value);

                    if (setting == null) {
                        Logger.WriteToChat($"Setting was null: {kv.Value}");
                        continue;
                    }

                    setting.Setting.Changed += (s, e) => {
                        try {
                            UpdateButton(kv);
                        }
                        catch (Exception ex) { Logger.LogException(ex); }
                    };

                    hudButton.Hit += (s, e) => {
                        try {
                            setting.Setting.SetValue(!(bool)setting.Setting.GetValue());
                            if (!UB.Plugin.Debug) {
                                Logger.WriteToChat(setting.Setting.FullDisplayValue());
                            }
                        }
                        catch (Exception ex) { Logger.LogException(ex); }
                    };
                }

                ExportPCap.Visible = UB.Plugin.PCap;
                PopulateProfiles(Tools.Plugin.SettingsProfileExtension, SettingsProfilesCombo, UB.Plugin.SettingsProfile);
                SetupFileWatcher();
            }
            catch (Exception ex) { Logger.LogException(ex); }

        }

        private void SettingsButton_Hit(object sender, EventArgs e) {
            if (settingsView == null) {
                settingsView = new SettingsView(UB);
                settingsView.Init();
                settingsView.view.VisibleChanged += (s, ee) => {
                    settingsView.view.ShowInBar = settingsView.view.Visible;
                };
            }
            settingsView.view.Visible = !settingsView.view.Visible;
        }

        private void PCap_Changed(object sender, SettingChangedEventArgs e) {
            ExportPCap.Visible = UB.Plugin.PCap;
        }

        private void SettingsProfile_Changed(object sender, SettingChangedEventArgs e) {
            UB.Settings.SettingsPath = UB.Plugin.SettingsProfilePath;
            PopulateProfiles(Tools.Plugin.SettingsProfileExtension, SettingsProfilesCombo, UB.Plugin.SettingsProfile);
        }

        private void WindowPosition_Changed(object sender, SettingChangedEventArgs e) {
            if (!timer.Enabled)
                view.Location = new Point(UB.Plugin.WindowPositionX, UB.Plugin.WindowPositionY);
        }

        private void CheckForUpdate_Hit(object sender, EventArgs e) {
            try {
                UpdateChecker.CheckForUpdate();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void ExportPCap_Hit(object sender, EventArgs e) {
            try {
                string filename = $"{Util.GetPluginDirectory()}\\pkt_{DateTime.UtcNow:yyyy-M-d}_{(int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds}_log.pcap";
                UBHelper.PCap.Print(filename);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UpdateButton(KeyValuePair<string, string> kv) {
            try {
                var hudButton = (HudButton)view[kv.Key];
                if (hudButton == null)
                    return;

                hudButton.OverlayImageRectangle = new Rectangle(3, 4, 16, 16);
                if ((bool)GetSettingPropFromText(kv.Value).Setting.GetValue()) {
                    hudButton.OverlayImage = 0x060069A1;
                }
                else {
                    hudButton.OverlayImage = 0x060011F8;
                }
            }
            catch (Exception ex) {
                Logger.LogException(ex.ToString());
            }
        }

        private OptionResult GetSettingPropFromText(string setting) {
            if (setting.StartsWith("Global."))
                return UBLoader.FilterCore.Settings.Get(setting);
            else if (UB.Settings.Exists(setting))
                return UB.Settings.Get(setting);
            else
                return UB.State.Get(setting);
        }

        internal void PopulateProfiles(string profileExtension, HudCombo profilesCombo, string selected) {
            profilesCombo.Clear();
            profilesCombo.AddItem("[character]", "[character]");
            profilesCombo.Current = 0;
            string[] profiles = Directory.GetFiles(Util.GetProfilesDirectory(), $"*.{profileExtension}");
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
            profilesWatcher.Path = Util.GetProfilesDirectory();
            profilesWatcher.Filter = $"*.json";

            profilesWatcher.Created += ProfilesWatcher_Changed;
            profilesWatcher.Renamed += ProfilesWatcher_Changed;
            profilesWatcher.Deleted += ProfilesWatcher_Changed;

            profilesWatcher.EnableRaisingEvents = true;
        }

        private void ProfilesWatcher_Changed(object sender, FileSystemEventArgs e) {
            PopulateProfiles(Tools.Plugin.SettingsProfileExtension, SettingsProfilesCombo, UB.Plugin.SettingsProfile);
            PopulateProfiles(Tools.Client.ClientUIProfileExtension, UB.Client.ClientUIProfilesCombo, UB.Client.UIProfile);
        }

        private void SettingsProfileReset_Hit(object sender, EventArgs e) {
            ResetProfile(SettingType.Profile, UB.Settings, UB.Plugin.SettingsProfile);
        }


        private void SettingsProfileCopyTo_Hit(object sender, EventArgs e) {
            CopyProfile(UB.Plugin.SettingsProfile, UB.Plugin.SettingsProfilePath, (v) => {
                return Path.Combine(Util.GetProfilesDirectory(), $"{v}.{Tools.Plugin.SettingsProfileExtension}");
            });
        }

        private void SettingsProfilesCombo_Change(object sender, EventArgs e) {
            UB.Plugin.SettingsProfile.Value = ((HudStaticText)SettingsProfilesCombo[SettingsProfilesCombo.Current]).Text;
        }

        internal void CopyProfile(ISetting profileSetting, string profilePath, Func<string, string> calcNewProfilePath) {
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
                else if (!File.Exists(profilePath)) {
                    Logger.Error($"Existing profile does not exist: {profilePath}");
                }
                else {
                    Logger.WriteToChat($"Copying profile '{profileSetting.GetValue()}' to new profile: {textEditPopup.Value}");
                    File.Copy(profilePath, newProfilePath);
                    profileSetting.SetValue(textEditPopup.Value);
                }
            };
        }

        internal void ResetProfile(SettingType settingType, Settings settings, string profileName) {
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

        internal void ImportProfile(Func<bool> ok) {
            if (confirmationPopup != null)
                confirmationPopup.Dispose();

            confirmationPopup = new ConfirmationDialog(UB.MainView.view, "Are you sure you want to clear this profile and import fresh values?");
            confirmationPopup.ClickedOK += (s, ce) => {
                ok();
            };
        }

        internal override ACImage GetIcon() {
            if (icon != null)
                return icon;
            icon = GetIcon("UtilityBelt.Resources.icons.utilitybelt.png");
            return icon;
        }

        new public void Dispose() {
            base.Dispose();
            if (icon != null) icon.Dispose();
            UB.Plugin.PCap.Changed -= PCap_Changed;
            UB.Plugin.WindowPositionX.Changed -= WindowPosition_Changed;
            UB.Plugin.WindowPositionY.Changed -= WindowPosition_Changed;
            if (settingsView != null) settingsView.Dispose();
            if (profilesWatcher != null) profilesWatcher.Dispose();
            if (textEditPopup != null) textEditPopup.Dispose();
            if (confirmationPopup != null) confirmationPopup.Dispose();
        }
    }
}
