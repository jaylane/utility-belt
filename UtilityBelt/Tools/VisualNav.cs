﻿using Decal.Adapter;
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
using System.ComponentModel;
using Newtonsoft.Json;
using UBLoader.Lib.Settings;

namespace UtilityBelt.Tools {
    #region VisualNav Display Config
    [Section("VisualNav display options")]
    public class VisualNavDisplayOptions : ISetting {
        public List<string> ValidSettings = new List<string>() {
                "Lines",
                "ChatText",
                "CurrentWaypoint",
                "JumpText",
                "JumpArrow",
                "OpenVendor",
                "Pause",
                "Portal",
                "Recall",
                "UseNPC",
                "FollowArrow"
            };

        //public event EventHandler<SettingChangedEventArgs> Changed;

        [Summary("Point to point lines")]
        public readonly ColorToggleOption Lines = new ColorToggleOption(true, -65281);

        [Summary("Chat commands")]
        public readonly ColorToggleOption ChatText = new ColorToggleOption(true, -1);

        [Summary("Jump commands")]
        public readonly ColorToggleOption JumpText = new ColorToggleOption(true, -1);

        [Summary("Jump heading arrow")]
        public readonly ColorToggleOption JumpArrow = new ColorToggleOption(true, -256);

        [Summary("Open vendor")]
        public readonly ColorToggleOption OpenVendor = new ColorToggleOption(true, -1);

        [Summary("Pause commands")]
        public readonly ColorToggleOption Pause = new ColorToggleOption(true, -1);

        [Summary("Portal commands")]
        public readonly ColorToggleOption Portal = new ColorToggleOption(true, -1);

        [Summary("Recall spells")]
        public readonly ColorToggleOption Recall = new ColorToggleOption(true, -1);

        [Summary("Use NPC commands")]
        public readonly ColorToggleOption UseNPC = new ColorToggleOption(true, -1);

        [Summary("Follow character arrow")]
        public readonly ColorToggleOption FollowArrow = new ColorToggleOption(true, -23296);

        [Summary("Current waypoint ring")]
        public readonly ColorToggleOption CurrentWaypoint = new ColorToggleOption(true, -7722014);

        public VisualNavDisplayOptions() {

        }

        public override object GetDefaultValue() {
            return null;
        }

        public override object GetValue() {
            return this;
        }

        public override void SetValue(object newValue) {
            throw new NotImplementedException();
        }
    }
    #endregion

    [Name("VisualNav")]
    [Summary("Shows VTank nav routes visually, like vi2")]
    [FullDescription(@"
![](https://i.gyazo.com/729b98fa095dee1a70bc991aa62d61bc.mp4)

Shows your currently loaded VTank navigation profile visually.  All waypoint types can be displayed, most of which will just show text above the waypoint.  It actively monitors for when a navigation profile has been changed or reloaded, and will automatically update the route display.  Currently it has no way of knowing where along a route you are, so all waypoints are treated equally.

### Embedded Routes
In order to display embedded nav routes, UB will have to save embedded routes to a file as they are loaded.  Right now all embedded navs are saved to the same file, called '[None].nav'.  If you wish to enable this feature, check the box on the VisualNav tab.

### Customizing

On the VisualNav tab of the main UtilityBelt window you can see the different waypoint types.Unchecking the checkbox next to an item will disable it from being displayed.Clicking on the solid color box next to an item will let you edit its color.Colors can be adjusted by sliding the Alpha, Red, Green, and Blue sliders.

![](https://i.gyazo.com/db99a93bd01b197f1e6be018efd42f8f.mp4)
![](https://i.gyazo.com/2cf96aaddf32badfbd096715474eff45.mp4)
    ")]

    public class VisualNav : ToolBase {
        private string currentRoutePath = "";
        internal VTNavRoute currentRoute = null;
        private bool forceUpdate = false;
        internal bool needsDraw = false;

        FileSystemWatcher navFileWatcher = null;
        FileSystemWatcher profilesWatcher = null;
        private DateTime lastNavChange = DateTime.MinValue;

        private List<D3DObj> shapes = new List<D3DObj>();

        public event EventHandler NavChanged;
        public event EventHandler NavUpdated;

        #region Config
        [Summary("Enabled")]
        [Hotkey("VisualNav", "Toggle VisualNav display")]
        public readonly Setting<bool> Enabled = new Setting<bool>(true);

        [Summary("ScaleCurrentWaypoint")]
        public readonly Setting<bool> ScaleCurrentWaypoint = new Setting<bool>(true);

        [Summary("Line offset from the ground, in meters")]
        public readonly Setting<float> LineOffset = new Setting<float>(0.05f);

        [Summary("Automatically save [None] routes. Enabling this allows embedded routes to be drawn.")]
        public readonly Setting<bool> SaveNoneRoutes = new Setting<bool>(false);

        [Summary("VisualNav display options")]
        public readonly VisualNavDisplayOptions Display = new VisualNavDisplayOptions();
        #endregion

        public VisualNav(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        public override void Init() {
            base.Init();
            UB.Core.CharacterFilter.ChangePortalMode += CharacterFilter_ChangePortalMode;
            UB.Core.RenderFrame += Core_RenderFrame;

            var server = UB.Core.CharacterFilter.Server;
            var character = UB.Core.CharacterFilter.Name;
            if (Directory.Exists(Util.GetVTankProfilesDirectory())) {
                profilesWatcher = new FileSystemWatcher();
                profilesWatcher.Path = Util.GetVTankProfilesDirectory();
                profilesWatcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite;
                profilesWatcher.Filter = $"{server}_{character}.cdf";
                profilesWatcher.Changed += Profiles_Changed;
                profilesWatcher.EnableRaisingEvents = true;
            }
            else {
                LogError($"{Util.GetVTankProfilesDirectory()} does not exist!");
                return;
            }

            if (UBHelper.Core.GameState != UBHelper.GameState.In_Game) {
                UB.Core.CharacterFilter.LoginComplete += CharacterFilter_LoginComplete;

            }
            else {
                needsDraw = true;
            }

            Display.Changed += (s, e) => { needsDraw = true; };

            uTank2.PluginCore.PC.NavRouteChanged += PC_NavRouteChanged;
            uTank2.PluginCore.PC.NavWaypointChanged += PC_NavWaypointChanged;
            UBHelper.VideoPatch.Changed += VideoPatch_Changed;

            Enabled.Changed += (s, e) => {
                forceUpdate = true;
                DrawCurrentRoute();
            };
        }

        private void PC_NavWaypointChanged() {
            try {
                NavUpdated?.Invoke(this, null);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void VideoPatch_Changed() {
            needsDraw = true;
        }

        private void CharacterFilter_LoginComplete(object sender, EventArgs e) {
            try {
                UB.Core.CharacterFilter.LoginComplete -= CharacterFilter_LoginComplete;

                DrawCurrentRoute();
            }
            catch(Exception ex) { Logger.LogException(ex); }
        }

        private void PC_NavRouteChanged() {
            try {
                if (!SaveNoneRoutes || !Enabled) return;

                needsDraw = true;

                var routePath = VTNavRoute.GetLoadedNavigationProfile();

                if (UBHelper.vTank.Instance == null || UBHelper.vTank.Instance.NavNumPoints <= 0) return;

                // the route has changed, but we are currently in a [None] route, so we will save it
                // to a new route called " [None].nav" so we can parse and draw it.
                if (string.IsNullOrEmpty(UBHelper.vTank.Instance?.GetNavProfile())) {
                    Util.DispatchChatToBoxWithPluginIntercept($"/vt nav save {VTNavRoute.NoneNavName}");
                    needsDraw = true;
                }

                // the route has changed, and we are on our custon [None].nav, so we force a redraw
                if (UBHelper.vTank.Instance.GetNavProfile().StartsWith(VTNavRoute.NoneNavName)) {
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

        private void DrawCurrentRoute() {

            if (!Enabled || string.IsNullOrEmpty(UBHelper.vTank.Instance?.GetNavProfile())) {
                ClearCurrentRoute();
                NavChanged?.Invoke(this, new EventArgs());
                return;
            }

            var routePath = Path.Combine(Util.GetVTankProfilesDirectory(), UBHelper.vTank.Instance.GetNavProfile());
            if (routePath == currentRoutePath && !forceUpdate) return;

            forceUpdate = false;
            ClearCurrentRoute();
            currentRoutePath = routePath;

            if (string.IsNullOrEmpty(currentRoutePath) || !File.Exists(currentRoutePath)) return;

            currentRoute = new VTNavRoute(routePath, UB);
            currentRoute.Parse();

            if (UB.OverlayMap.Enabled || (!(UBHelper.VideoPatch.Enabled && !(UBHelper.VideoPatch.bgOnly && UBHelper.Core.isFocused)))) {
                currentRoute.Draw();
            }

            if (navFileWatcher != null) {
                navFileWatcher.EnableRaisingEvents = false;
                navFileWatcher.Dispose();
            }

            if (!UBHelper.vTank.Instance.GetNavProfile().StartsWith(VTNavRoute.NoneNavName)) {
                WatchRouteFiles();
            }

            NavChanged?.Invoke(this, new EventArgs());
        }

        private void WatchRouteFiles() {
            if (!Directory.Exists(Util.GetVTankProfilesDirectory())) {
                LogDebug($"WatchRouteFiles() Error: {Util.GetVTankProfilesDirectory()} does not exist!");
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

        public void Core_RenderFrame(object sender, EventArgs e) {
            try {
                if (needsDraw) {
                    needsDraw = false;
                    forceUpdate = true;
                    DrawCurrentRoute();
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    try {
                        UB.Core.RenderFrame -= Core_RenderFrame;
                        UB.Core.CharacterFilter.ChangePortalMode -= CharacterFilter_ChangePortalMode;
                        uTank2.PluginCore.PC.NavRouteChanged -= PC_NavRouteChanged;
                        uTank2.PluginCore.PC.NavWaypointChanged -= PC_NavWaypointChanged;
                        UBHelper.VideoPatch.Changed -= VideoPatch_Changed;
                    }
                    catch { }

                    if (profilesWatcher != null) profilesWatcher.Dispose();
                    if (navFileWatcher != null) navFileWatcher.Dispose();
                    if (currentRoute != null) currentRoute.Dispose();

                    base.Dispose(disposing);
                }
                disposedValue = true;
            }
        }
    }
}
