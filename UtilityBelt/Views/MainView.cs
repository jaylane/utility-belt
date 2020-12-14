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

namespace UtilityBelt.Views {
    public class MainView : BaseView {

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

                view.Moved += (s, e) => {
                    UB.Plugin.WindowPositionX.Value = view.Location.X;
                    UB.Plugin.WindowPositionY.Value = view.Location.Y;
                };

                SettingsList = (HudList)view["SettingsList"];
                SettingEditLayout = (HudFixedLayout)view["SettingsForm"];
                CheckForUpdate = (HudButton)view["CheckForUpdate"];
                ExportPCap = (HudButton)view["ExportPCap"];

                SettingsList.Click += SettingsList_Click;
                CheckForUpdate.Hit += CheckForUpdate_Hit;
                ExportPCap.Hit += ExportPCap_Hit;
                UB.Settings.Changed += Settings_Changed;

                foreach (var kv in buttons) {
                    UpdateButton(kv);
                    var hudButton = (HudButton)view[kv.Key];
                    var setting = UB.Settings.Get(kv.Value);

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
                                Logger.WriteToChat($"{kv.Value} = {UB.Settings.DisplayValue(kv.Value)}");
                            }
                        }
                        catch (Exception ex) { Logger.LogException(ex); }
                    };
                }

                if (!UB.Plugin.PCap) ExportPCap.Visible = false;

                PopulateSettings(UB, "");
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Settings_Changed(object sender, EventArgs e) {
            var name = ((ISetting)sender).GetName();
            for(var i = 0; i < SettingsList.RowCount; i++) {
                if (((HudStaticText)SettingsList[i][0]).Text == name) {
                    ((HudStaticText)SettingsList[i][1]).Text = UB.Settings.DisplayValue(name);
                }
            }

            ExportPCap.Visible = UB.Plugin.PCap;
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
                if ((bool)UB.Settings.Get(kv.Value).Setting.GetValue()) {
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
                var prop = UB.Settings.Get(((HudStaticText)row[0]).Text);

                if (selectedIndex >= 0 && SettingsList.RowCount > selectedIndex) {
                    ((HudStaticText)((HudList.HudListRowAccessor)SettingsList[selectedIndex])[0]).TextColor = view.Theme.GetColor("ListText");
                }

                ((HudStaticText)row[0]).TextColor = Color.Red;
                selectedIndex = rowIndex;

                DrawSetting(((HudStaticText)row[0]).Text);

                if (colIndex == 1 && prop.Setting.GetValue().GetType() == typeof(bool)) {
                    prop.Setting.SetValue(!(bool)prop.Setting.GetValue());
                    DrawSetting(((HudStaticText)row[0]).Text);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void DrawSetting(string setting) {
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
            
            var prop = UB.Settings.Get(setting);
            SummaryText.TextAlignment = WriteTextFormats.WordBreak;

            var summaryAttr = prop.FieldInfo.GetCustomAttributes(typeof(SummaryAttribute), true);
            if (summaryAttr.Length == 1) {
                SummaryText.Text = ((SummaryAttribute)summaryAttr[0]).Summary;
            }
            else {
                SummaryText.Text = prop.FieldInfo.Name;
            }

            SummaryText.Text += " (" + prop.Setting.GetValue().GetType() + ")";

            currentForm = new SettingsForm(setting, FormLayout);
            currentForm.Changed += (s, e) => {
                prop.Setting.SetValue(currentForm.Value);
            };
        }

        private void PopulateSettings(object obj, string history, bool clear = false) {
            var settings = UB.Settings.GetAll();

            if (clear)
                SettingsList.ClearRows();

            foreach (var setting in settings) {
                var row = SettingsList.AddRow();
                ((HudStaticText)row[0]).Text = setting.GetName();
                ((HudStaticText)row[1]).Text = UB.Settings.DisplayValue(setting.GetName());
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
            UB.Settings.Changed -= Settings_Changed;
        }
    }
}
