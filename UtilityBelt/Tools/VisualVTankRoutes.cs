using Decal.Adapter;
using System;
using Decal.Adapter.Wrappers;
using System.IO;
using UtilityBelt.Lib.VTNav;
using System.Drawing;
using System.Collections.Generic;
using VirindiViewService.Controls;
using VirindiViewService;
using UtilityBelt.Views;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Settings;

namespace UtilityBelt.Tools {
    public class VisualVTankRoutes : IDisposable {
        private bool disposed = false;
        private string currentRoutePath = "";
        public VTNavRoute currentRoute = null;
        private bool forceUpdate = false;
        public bool needsDraw = false;

        FileSystemWatcher navFileWatcher = null;
        FileSystemWatcher profilesWatcher = null;
        private DateTime lastNavChange = DateTime.MinValue;

        private List<D3DObj> shapes = new List<D3DObj>();

        const int COL_ENABLED = 0;
        const int COL_ICON = 1;
        const int COL_NAME = 2;

        public VisualVTankRoutes() {
            Globals.Core.CommandLineText += Core_CommandLineText;
            Globals.Core.CharacterFilter.ChangePortalMode += CharacterFilter_ChangePortalMode;

            var server = Globals.Core.CharacterFilter.Server;
            var character = Globals.Core.CharacterFilter.Name;
            if (Directory.Exists(Util.GetVTankProfilesDirectory())) {
                profilesWatcher = new FileSystemWatcher();
                profilesWatcher.Path = Util.GetVTankProfilesDirectory();
                profilesWatcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite;
                profilesWatcher.Filter = $"{server}_{character}.cdf";
                profilesWatcher.Changed += Profiles_Changed;
                profilesWatcher.EnableRaisingEvents = true;
            } else {
                Logger.Debug($"VisualVTankRoutes() Error: {Util.GetVTankProfilesDirectory()} does not exist!");
                return;
            }

            Globals.Settings.VisualNav.Display.PropertyChanged += (s, e) => { needsDraw = true; };
            DrawCurrentRoute();

            uTank2.PluginCore.PC.NavRouteChanged += PC_NavRouteChanged;

            Globals.Settings.VisualNav.PropertyChanged += (s, e) => {
                if (e.PropertyName == "Enabled") {
                    forceUpdate = true;
                    DrawCurrentRoute();
                }
            };
        }

        private void PC_NavRouteChanged() {
            try {
                if (!Globals.Settings.VisualNav.SaveNoneRoutes || !Globals.Settings.VisualNav.Enabled) return;

                needsDraw = true;

                var routePath = VTNavRoute.GetLoadedNavigationProfile();
                var vTank = VTankControl.vTankInstance;

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

        private ACImage GetSettingIcon(ColorToggleOption option) {
            var bmp = new Bitmap(32, 32);
            using (Graphics gfx = Graphics.FromImage(bmp)) {
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(option.Color))) {
                    gfx.FillRectangle(brush, 0, 0, 32, 32);
                }
            }

            return new ACImage(bmp);
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
            if (!Globals.Settings.VisualNav.Enabled || Globals.Settings.Plugin.VideoPatch) {
                ClearCurrentRoute();
                return;
            }

            var vTank = VTankControl.vTankInstance;
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
            if (!Directory.Exists(Util.GetVTankProfilesDirectory())) {
                Logger.Debug($"WatchRouteFiles() Error: {Util.GetVTankProfilesDirectory()} does not exist!");
                return;
            }
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
                    try {
                        Globals.Core.CommandLineText -= Core_CommandLineText;
                        Globals.Core.CharacterFilter.ChangePortalMode -= CharacterFilter_ChangePortalMode;
                        uTank2.PluginCore.PC.NavRouteChanged -= PC_NavRouteChanged;
                    }
                    catch { }

                    if (profilesWatcher != null) profilesWatcher.Dispose();
                    if (navFileWatcher != null) navFileWatcher.Dispose();
                    if (currentRoute != null) currentRoute.Dispose();
                }
                disposed = true;
            }
        }
    }
}
