using Decal.Adapter;
using System;
using Decal.Adapter.Wrappers;
using Decal.Filters;
using UtilityBelt.MagTools.Shared;
using System.Text.RegularExpressions;
using System.IO;
using UtilityBelt.Lib;
using UtilityBelt.Lib.VTNav;
using System.Drawing;
using System.Collections.Generic;

namespace UtilityBelt.Tools {
    class VisualVTankRoutes : IDisposable {
        private bool disposed = false;
        private string currentRoutePath = "";
        private VTNavRoute currentRoute = null;
        private bool doThatSell = false;
        private bool forceUpdate = false;
        private bool needsDraw = false;

        FileSystemWatcher navFileWatcher = null;
        FileSystemWatcher profilesWatcher = null;
        private DateTime lastNavChange = DateTime.MinValue;

        private List<D3DObj> shapes = new List<D3DObj>();

        public VisualVTankRoutes() {
            Globals.Core.CommandLineText += Core_CommandLineText;
            Globals.Core.CharacterFilter.ChangePortalMode += CharacterFilter_ChangePortalMode;

            var server = Globals.Core.CharacterFilter.Server;
            var character = Globals.Core.CharacterFilter.Name;

            profilesWatcher = new FileSystemWatcher();
            profilesWatcher.Path = "C:\\Games\\VirindiPlugins\\VirindiTank\\";
            profilesWatcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite;
            profilesWatcher.Filter = $"{server}_{character}.cdf";
            profilesWatcher.Changed += Profiles_Changed;

            profilesWatcher.EnableRaisingEvents = true;

            DrawCurrentRoute();
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
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void DrawCurrentRoute() {
            var routePath = VTNavRoute.GetLoadedNavigationProfile();

            if (routePath == currentRoutePath && !forceUpdate) return;

            forceUpdate = false;
            ClearCurrentRoute();
            currentRoutePath = routePath;

            if (string.IsNullOrEmpty(currentRoutePath)) return;

            currentRoute = new VTNavRoute(routePath);
            currentRoute.Parse();

            currentRoute.Draw();
            WatchRouteFiles();
        }

        private void WatchRouteFiles() {
            if (navFileWatcher != null) {
                navFileWatcher.EnableRaisingEvents = false;
                navFileWatcher.Dispose();
            }

            navFileWatcher = new FileSystemWatcher();
            navFileWatcher.Path = "C:\\Games\\VirindiPlugins\\VirindiTank\\";
            navFileWatcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite;
            navFileWatcher.Filter = "*.nav";
            navFileWatcher.Changed += Watcher_Changed;

            navFileWatcher.EnableRaisingEvents = true;
        }

        private void Watcher_Changed(object sender, FileSystemEventArgs e) {
            try {
                if (e.FullPath != currentRoutePath || DateTime.UtcNow - lastNavChange < TimeSpan.FromMilliseconds(50)) return;
                lastNavChange = DateTime.UtcNow;
                needsDraw = true;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void ClearCurrentRoute() {
            if (currentRoute == null) return;

            foreach (var p in currentRoute.points) {
                if (p.shapes == null) continue;
                foreach (var shape in p.shapes) {
                    shape.Visible = false;
                    shape.Dispose();
                }
            }

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

                    if (profilesWatcher != null) profilesWatcher.Dispose();
                    if (navFileWatcher != null) navFileWatcher.Dispose();
                }
                disposed = true;
            }
        }
    }
}
