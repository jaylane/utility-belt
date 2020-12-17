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

        private HudList SettingsList;
        private HudFixedLayout SettingEditLayout;
        private int selectedIndex = -1;
        private SettingsForm currentForm = null;
        private HudStaticText SummaryText = null;
        private HudFixedLayout FormLayout = null;
        private HudButton CheckForUpdate;
        internal HudButton ExportPCap;
        private ACImage icon;
        private const int descriptionHeight = 40;

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
                    timer.Stop();
                    UB.Plugin.WindowPositionX.Value = view.Location.X;
                    UB.Plugin.WindowPositionY.Value = view.Location.Y;
                };

                view.Moved += (s, e) => {
                    if (timer.Enabled) timer.Stop();
                    timer.Start();
                };

                UB.Plugin.WindowPositionX.Changed += WindowPosition_Changed;
                UB.Plugin.WindowPositionY.Changed += WindowPosition_Changed;

                SettingsList = (HudList)view["SettingsList"];
                SettingEditLayout = (HudFixedLayout)view["SettingsForm"];
                CheckForUpdate = (HudButton)view["CheckForUpdate"];
                ExportPCap = (HudButton)view["ExportPCap"];

                SettingsList.Click += SettingsList_Click;
                CheckForUpdate.Hit += CheckForUpdate_Hit;
                ExportPCap.Hit += ExportPCap_Hit;
                UB.Settings.Changed += Settings_Changed;
                UBLoader.FilterCore.Settings.Changed += FilterSettings_Changed;
                UB.Plugin.PCap.Changed += PCap_Changed;

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
                                Logger.WriteToChat($"{kv.Value} = {setting.Setting.DisplayValue()}");
                            }
                        }
                        catch (Exception ex) { Logger.LogException(ex); }
                    };
                }

                ExportPCap.Visible = UB.Plugin.PCap;

                PopulateSettings();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void PCap_Changed(object sender, SettingChangedEventArgs e) {
            ExportPCap.Visible = UB.Plugin.PCap;
        }

        private void WindowPosition_Changed(object sender, SettingChangedEventArgs e) {
            view.Location = new Point(UB.Plugin.WindowPositionX, UB.Plugin.WindowPositionY);
        }

        private void Settings_Changed(object sender, EventArgs e) {
            UpdateSettingsList(((ISetting)sender).GetName(), (ISetting)sender);
        }

        private void FilterSettings_Changed(object sender, EventArgs e) {
            UpdateSettingsList("Global." + ((ISetting)sender).GetName(), (ISetting)sender);
        }

        private void UpdateSettingsList(string name, ISetting sender) {
            for (var i = 0; i < SettingsList.RowCount; i++) {
                if (((HudStaticText)SettingsList[i][0]).Text == name) {
                    ((HudStaticText)SettingsList[i][1]).Text = ((ISetting)sender).DisplayValue();
                }
            }
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

        private void SettingsList_Click(object sender, int rowIndex, int colIndex) {
            try {
                var row = ((HudList.HudListRowAccessor)SettingsList[rowIndex]);
                var prop = GetSettingPropFromText(((HudStaticText)row[0]).Text);

                if (selectedIndex >= 0 && SettingsList.RowCount > selectedIndex) {
                    ((HudStaticText)((HudList.HudListRowAccessor)SettingsList[selectedIndex])[0]).TextColor = view.Theme.GetColor("ListText");
                }

                ((HudStaticText)row[0]).TextColor = Color.Red;
                selectedIndex = rowIndex;

                DrawSetting(prop.Setting);

                if (colIndex == 1 && prop.Setting.GetValue().GetType() == typeof(bool)) {
                    prop.Setting.SetValue(!(bool)prop.Setting.GetValue());
                    DrawSetting(prop.Setting);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private OptionResult GetSettingPropFromText(string setting) {
            if (setting.StartsWith("Global."))
                return UBLoader.FilterCore.Settings.Get(setting.Substring(7));
            else
                return UB.Settings.Get(setting);
        }

        private void DrawSetting(ISetting setting) {
            if (currentForm != null) {
                currentForm.Dispose();
                currentForm = null;
            }
            if (SummaryText == null) {
                SummaryText = new HudStaticText();

                SettingEditLayout.AddControl(SummaryText, new Rectangle(5, 5, 390, descriptionHeight));
            }
            if (FormLayout == null) {
                FormLayout = new HudFixedLayout();
                SettingEditLayout.AddControl(FormLayout, new Rectangle(5, descriptionHeight, 390, 25));
            }

            SummaryText.TextAlignment = WriteTextFormats.WordBreak;

            var summaryAttr = setting.FieldInfo.GetCustomAttributes(typeof(SummaryAttribute), true);
            if (summaryAttr.Length == 1) {
                SummaryText.Text = ((SummaryAttribute)summaryAttr[0]).Summary;
            }
            else {
                SummaryText.Text = setting.FieldInfo.Name;
            }

            var type = setting.GetValue().GetType().ToString().Replace("System.","");
            if (setting.GetValue() is ObservableCollection<string>) {
                type = "List";
            }
            else if (setting.GetValue() is ObservableDictionary<string, string>) {
                type = "Dictionary";
            }
            currentForm = new SettingsForm(setting, FormLayout, setting.GetValue().GetType());
            SummaryText.Text += " (" + type + ")";

            currentForm.Changed += (s, e) => {
                setting.SetValue(currentForm.Value);
            };
        }

        private void PopulateSettings(bool clear = false) {
            var settings = UB.Settings.GetAll();
            var globalSettings = UBLoader.FilterCore.Settings.GetAll();

            if (clear)
                SettingsList.ClearRows();

            foreach (var setting in globalSettings) {
                var row = SettingsList.AddRow();
                ((HudStaticText)row[0]).Text = $"Global.{setting.GetName()}";
                ((HudStaticText)row[1]).Text = setting.DisplayValue();
                ((HudStaticText)row[1]).TextAlignment = WriteTextFormats.Right;
            }

            foreach (var setting in settings) {
                var row = SettingsList.AddRow();
                ((HudStaticText)row[0]).Text = setting.GetName();
                ((HudStaticText)row[1]).Text = setting.DisplayValue();
                ((HudStaticText)row[1]).TextAlignment = WriteTextFormats.Right;
            }
        }

        internal override ACImage GetIcon() {
            if (icon != null)
                return icon;
            icon = GetIcon("UtilityBelt.Resources.icons.utilitybelt.png");
            return icon;
        }
        ~MainView() {
            if (icon != null) icon.Dispose();
            UB.Plugin.PCap.Changed -= PCap_Changed;
            UB.Plugin.WindowPositionX.Changed -= WindowPosition_Changed;
            UB.Plugin.WindowPositionY.Changed -= WindowPosition_Changed;
            UB.Settings.Changed -= Settings_Changed;
            UBLoader.FilterCore.Settings.Changed -= FilterSettings_Changed;
        }
    }
}
