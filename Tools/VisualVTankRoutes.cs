using Decal.Adapter;
using System;
using Decal.Adapter.Wrappers;
using System.IO;
using UtilityBelt.Lib.VTNav;
using System.Drawing;
using System.Collections.Generic;
using VirindiViewService.Controls;
using Mag.Shared.Settings;
using VirindiViewService;
using UtilityBelt.Views;
using UtilityBelt.Lib;

namespace UtilityBelt.Tools {
    class VisualVTankRoutes : IDisposable {
        private bool disposed = false;
        private string currentRoutePath = "";
        private VTNavRoute currentRoute = null;
        private bool forceUpdate = false;
        public bool needsDraw = false;

        FileSystemWatcher navFileWatcher = null;
        FileSystemWatcher profilesWatcher = null;
        private DateTime lastNavChange = DateTime.MinValue;

        private List<D3DObj> shapes = new List<D3DObj>();

        HudList VisualNavSettingsList { get; set; }
        HudCheckBox VisualNavSaveNoneRoutes { get; set; }

        const int COL_ENABLED = 0;
        const int COL_ICON = 1;
        const int COL_NAME = 2;

        public VisualVTankRoutes() {
            Globals.Core.CommandLineText += Core_CommandLineText;
            Globals.Core.CharacterFilter.ChangePortalMode += CharacterFilter_ChangePortalMode;
            VisualNavSettingsList = Globals.MainView.view != null ? (HudList)Globals.MainView.view["VisualNavSettingsList"] : new HudList();
            VisualNavSettingsList.Click += VisualNavSettingsList_Click;

            VisualNavSaveNoneRoutes = (HudCheckBox)Globals.MainView.view["VisualNavSaveNoneRoutes"];
            VisualNavSaveNoneRoutes.Checked = Globals.Config.VisualNav.SaveNoneRoutes.Value;
            VisualNavSaveNoneRoutes.Change += VisualNavSaveNoneRoutes_Change;

            PopulateSettings();

            var server = Globals.Core.CharacterFilter.Server;
            var character = Globals.Core.CharacterFilter.Name;

            profilesWatcher = new FileSystemWatcher();
            profilesWatcher.Path = Util.GetVTankProfilesDirectory();
            profilesWatcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite;
            profilesWatcher.Filter = $"{server}_{character}.cdf";
            profilesWatcher.Changed += Profiles_Changed;

            profilesWatcher.EnableRaisingEvents = true;

            Globals.Config.VisualNav.ChatTextColor.Changed += ConfigChanged;
            Globals.Config.VisualNav.JumpArrowColor.Changed += ConfigChanged;
            Globals.Config.VisualNav.JumpTextColor.Changed += ConfigChanged;
            Globals.Config.VisualNav.LineColor.Changed += ConfigChanged;
            Globals.Config.VisualNav.LineOffset.Changed += ConfigChanged;
            Globals.Config.VisualNav.OpenVendorColor.Changed += ConfigChanged;
            Globals.Config.VisualNav.PauseColor.Changed += ConfigChanged;
            Globals.Config.VisualNav.PortalColor.Changed += ConfigChanged;
            Globals.Config.VisualNav.RecallColor.Changed += ConfigChanged;
            Globals.Config.VisualNav.ShowChatText.Changed += ConfigChanged;
            Globals.Config.VisualNav.ShowJumpArrow.Changed += ConfigChanged;
            Globals.Config.VisualNav.ShowJumpText.Changed += ConfigChanged;
            Globals.Config.VisualNav.ShowLine.Changed += ConfigChanged;
            Globals.Config.VisualNav.ShowOpenVendor.Changed += ConfigChanged;
            Globals.Config.VisualNav.ShowPause.Changed += ConfigChanged;
            Globals.Config.VisualNav.ShowPortal.Changed += ConfigChanged;
            Globals.Config.VisualNav.ShowRecall.Changed += ConfigChanged;
            Globals.Config.VisualNav.ShowUseNPC.Changed += ConfigChanged;
            Globals.Config.VisualNav.UseNPCColor.Changed += ConfigChanged;
            
            DrawCurrentRoute();

            uTank2.PluginCore.PC.NavRouteChanged += PC_NavRouteChanged;
        }

        private void PC_NavRouteChanged() {
            try {
                if (!Globals.Config.VisualNav.SaveNoneRoutes.Value) return;

                var routePath = VTNavRoute.GetLoadedNavigationProfile();
                var vTank = VTankControl.GetVTankInterface(uTank2.eExternalsPermissionLevel.FullUnderlying);

                if (vTank == null || vTank.NavNumPoints <= 0) return;

                // the route has changed, but we are currently in a [None] route, so we will save it
                // to a new route called " [None].nav" so we can parse and draw it.
                if (string.IsNullOrEmpty(vTank.GetNavProfile())) {
                    Util.DispatchChatToBoxWithPluginIntercept($"/vt nav save {VTNavRoute.NoneNavName}");
                    needsDraw = true;
                }

                // the route has changed, and we are on our custon [None].nav, so we force a redraw
                if (vTank.GetNavProfile().StartsWith(VTNavRoute.NoneNavName)) {
                    needsDraw = true;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void ConfigChanged(object obj) {
            needsDraw = true;
        }

        private void VisualNavSaveNoneRoutes_Change(object sender, EventArgs e) {
            Globals.Config.VisualNav.SaveNoneRoutes.Value = VisualNavSaveNoneRoutes.Checked;
        }

        private void PopulateSettings() {
            int scroll = 0;
            if (Globals.MainView.view.Visible) {
                scroll = VisualNavSettingsList.ScrollPosition;
            }

            VisualNavSettingsList.ClearRows();

            foreach (var setting in Globals.Config.VisualNav.Settings) {
                HudList.HudListRowAccessor row = VisualNavSettingsList.AddRow();

                bool isChecked = Globals.Config.VisualNav.GetFieldValue<Setting<bool>>($"Show{setting}").Value;
                
                ((HudCheckBox)row[COL_ENABLED]).Checked = isChecked;
                ((HudStaticText)row[COL_NAME]).Text = setting;
                ((HudPictureBox)row[COL_ICON]).Image = GetSettingIcon(setting);
            }

            VisualNavSettingsList.ScrollPosition = scroll;
        }

        private ACImage GetSettingIcon(string setting) {
           int color = Globals.Config.VisualNav.GetFieldValue<Setting<int>>($"{setting}Color").Value;

            var bmp = new Bitmap(32, 32);
            using (Graphics gfx = Graphics.FromImage(bmp)) {
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(color))) {
                    gfx.FillRectangle(brush, 0, 0, 32, 32);
                }
            }

            return new ACImage(bmp);
        }

        private void VisualNavSettingsList_Click(object sender, int row, int col) {
            try {
                HudList.HudListRowAccessor clickedRow = VisualNavSettingsList[row];
                var name = ((HudStaticText)clickedRow[COL_NAME]).Text;

                switch (col) {
                    case COL_ENABLED:
                        Globals.Config.VisualNav.GetFieldValue<Setting<bool>>($"Show{name}").Value = ((HudCheckBox)clickedRow[COL_ENABLED]).Checked;
                        break;

                    case COL_ICON:
                        int color = Globals.Config.VisualNav.GetFieldValue<Setting<int>>($"{name}Color").Value;
                        var picker = new ColorPicker(Globals.MainView, name, Color.FromArgb(color));

                        picker.RaiseColorPickerCancelEvent += (s, e) => {
                            picker.Dispose();
                        };

                        picker.RaiseColorPickerSaveEvent += (s,  e) => {
                            Globals.Config.VisualNav.GetFieldValue<Setting<int>>($"{name}Color").Value = e.Color.ToArgb();
                            PopulateSettings();
                            picker.Dispose();
                        };

                        picker.view.VisibleChanged += (s, e) => {
                            if (!picker.view.Visible) {
                                picker.Dispose();
                            }
                        };

                        break;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Profiles_Changed(object sender, FileSystemEventArgs e) {
            try {
                needsDraw = true;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void CharacterFilter_ChangePortalMode(object sender, ChangePortalModeEventArgs e) {
            try {
                if (e.Type == PortalEventType.ExitPortal) {
                    needsDraw = true;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Core_CommandLineText(object sender, ChatParserInterceptEventArgs e) {
            try {
                if (e.Text.StartsWith("/ub testroutes")) {
                    needsDraw = true;
                    e.Eat = true;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void DrawCurrentRoute() {
            var vTank = VTankControl.GetVTankInterface(uTank2.eExternalsPermissionLevel.FullUnderlying);
            var routePath = Path.Combine(Util.GetVTankProfilesDirectory(), vTank.GetNavProfile());
            
            if (routePath == currentRoutePath && !forceUpdate) return;

            forceUpdate = false;
            ClearCurrentRoute();
            currentRoutePath = routePath;

            if (string.IsNullOrEmpty(currentRoutePath) || !File.Exists(currentRoutePath)) return;

            currentRoute = new VTNavRoute(routePath);
            currentRoute.Parse();

            currentRoute.Draw();

            if (navFileWatcher != null) {
                navFileWatcher.EnableRaisingEvents = false;
                navFileWatcher.Dispose();
            }

            if (!vTank.GetNavProfile().StartsWith(VTNavRoute.NoneNavName)) {
                WatchRouteFiles();
            }
        }

        private void WatchRouteFiles() {
            navFileWatcher = new FileSystemWatcher();
            navFileWatcher.Path = Util.GetVTankProfilesDirectory();
            navFileWatcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite;
            navFileWatcher.Filter = "*.nav";
            navFileWatcher.Changed += NavFile_Changed;

            navFileWatcher.EnableRaisingEvents = true;
        }

        private void NavFile_Changed(object sender, FileSystemEventArgs e) {
            try {
                if (e.FullPath != currentRoutePath || DateTime.UtcNow - lastNavChange < TimeSpan.FromMilliseconds(50)) return;
                lastNavChange = DateTime.UtcNow;
                needsDraw = true;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void ClearCurrentRoute() {
            if (currentRoute == null) return;

            currentRoute.Dispose();

            currentRoute = null;
        }

        public void Think() {
            if (needsDraw) {
                needsDraw = false;
                forceUpdate = true;
                DrawCurrentRoute();
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    Globals.Core.CommandLineText -= Core_CommandLineText;
                    Globals.Core.CharacterFilter.ChangePortalMode -= CharacterFilter_ChangePortalMode;
                    uTank2.PluginCore.PC.NavRouteChanged -= PC_NavRouteChanged;

                    if (profilesWatcher != null) profilesWatcher.Dispose();
                    if (navFileWatcher != null) navFileWatcher.Dispose();

                    if (currentRoute != null) currentRoute.Dispose();
                }
                disposed = true;
            }
        }
    }
}
