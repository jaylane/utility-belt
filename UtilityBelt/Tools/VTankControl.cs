using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using uTank2;
using UtilityBelt.Lib;
using UtilityBelt.Lib.VTNav;
using static uTank2.PluginCore;

namespace UtilityBelt.Tools {
    [Name("VTank")]
    public class VTankControl : ToolBase {
        #region Config
        [Summary("VitalSharing")]
        [DefaultValue(true)]
        [Hotkey("VitalSharing", "Toggle VitalSharing functionality")]
        public bool VitalSharing {
            get { return (bool)GetSetting("VitalSharing"); }
            set { UpdateSetting("VitalSharing", value); }
        }

        [Summary("Detect and fix vtank nav portal loops")]
        [DefaultValue(false)]
        public bool FixPortalLoops {
            get { return (bool)GetSetting("FixPortalLoops"); }
            set { UpdateSetting("FixPortalLoops", value); }
        }

        [Summary("Number of portal loops to the same location to trigger portal loop fix")]
        [DefaultValue(3)]
        public int PortalLoopCount {
            get { return (int)GetSetting("PortalLoopCount"); }
            set { UpdateSetting("PortalLoopCount", value); }
        }
        #endregion

        #region Commands
        [Summary("Translates a VTank nav route from one landblock to another. Add force flag to overwrite the output nav. **NOTE**: This will translate **ALL** points, even if some are in a dungeon and some are not, it doesn't care.")]
        [Usage("/ub translateroute <startLandblock> <routeToLoad> <endLandblock> <routeToSaveAs> [force]")]
        [Example("/ub translateroute 0x00640371 eo-east.nav 0x002B0371 eo-main.nav", "Translates eo-east.nav to landblock 0x002B0371(eo main) and saves it as eo-main.nav if the file doesn't exist")]
        [Example("/ub translateroute 0x00640371 eo-east.nav 0x002B0371 eo-main.nav force", "Translates eo-east.nav to landblock 0x002B0371(eo main) and saves it as eo-main.nav, overwriting if the file exists")]
        [CommandPattern("translateroute", @"^ *(?<StartLandblock>[0-9A-Fx]+) +(?<RouteToLoad>.+\.(nav)) +(?<EndLandblock>[0-9A-Fx]+) +(?<RouteToSaveAs>.+\.(nav)) *(?<Force>force)?$")]
        public void TranslateRoute(string command, Match args) {
            try {
                LogDebug($"Translating route: RouteToLoad:{args.Groups["RouteToLoad"].Value} StartLandblock:{args.Groups["StartLandblock"].Value} EndLandblock:{args.Groups["EndLandblock"].Value} RouteToSaveAs:{args.Groups["RouteToSaveAs"].Value} Force:{!string.IsNullOrEmpty(args.Groups["Force"].Value)}");

                if (!uint.TryParse(args.Groups["StartLandblock"].Value.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out uint startLandblock)) {
                    LogError($"Could not parse hex value from StartLandblock: {args.Groups["StartLandblock"].Value}");
                    return;
                }

                if (!uint.TryParse(args.Groups["EndLandblock"].Value.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out uint endLandblock)) {
                    LogError($"Could not parse hex value from EndLandblock: {args.Groups["EndLandblock"].Value}");
                    return;
                }

                var loadPath = Path.Combine(Util.GetVTankProfilesDirectory(), args.Groups["RouteToLoad"].Value);
                if (!File.Exists(loadPath)) {
                    LogError($"Could not find route to load: {loadPath}");
                    return;
                }

                var savePath = Path.Combine(Util.GetVTankProfilesDirectory(), args.Groups["RouteToSaveAs"].Value);
                if (string.IsNullOrEmpty(args.Groups["Force"].Value) && File.Exists(savePath)) {
                    LogError($"Output path already exists! Run with force flag to overwrite: {savePath}");
                    return;
                }

                var route = new Lib.VTNav.VTNavRoute(loadPath, UB);
                if (!route.Parse()) {
                    LogError($"Unable to parse route");
                    return;
                }
                var allPoints = route.points.Where((p) => (p.Type == Lib.VTNav.eWaypointType.Point)).ToArray();
                if (allPoints.Length <= 0) {
                    LogError($"Unable to translate route, no nav points found! Type:{route.NavType}");
                    return;
                }

                var ewOffset = Geometry.LandblockXDifference(startLandblock, endLandblock) / 240f;
                var nsOffset = Geometry.LandblockYDifference(startLandblock, endLandblock) / 240f;

                foreach (var point in route.points) {
                    point.EW += ewOffset;
                    point.NS += nsOffset;
                }

                using (StreamWriter file = new StreamWriter(savePath)) {
                    route.Write(file);
                    file.Flush();
                }

                LogDebug($"Translated {route.RecordCount} records from {startLandblock:X8} to {endLandblock:X8} by adding offsets NS:{nsOffset} EW:{ewOffset}\nSaved to file: {savePath}");
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        #endregion

        private bool isFixingPortalLoops = false;
        private int portalExitCount = 0;
        private int lastPortalExitLandcell = 0;

        public VTankControl(UtilityBeltPlugin ub, string name) : base(ub, name) {
            if (UB.Core.CharacterFilter.LoginStatus != 0) Enable();
            else UB.Core.CharacterFilter.LoginComplete += CharacterFilter_LoginComplete;

            PropertyChanged += VTankControl_PropertyChanged;

            if (FixPortalLoops) {
                UB.Core.CharacterFilter.ChangePortalMode += CharacterFilter_ChangePortalMode;
                isFixingPortalLoops = true;
            }
        }

        private void VTankControl_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName.Equals("FixPortalLoops")) {
                if (FixPortalLoops && !isFixingPortalLoops) {
                    UB.Core.CharacterFilter.ChangePortalMode += CharacterFilter_ChangePortalMode;
                    isFixingPortalLoops = true;
                }
                else if (!FixPortalLoops && isFixingPortalLoops) {
                    UB.Core.CharacterFilter.ChangePortalMode -= CharacterFilter_ChangePortalMode;
                    isFixingPortalLoops = false;
                }
            }
        }

        private void CharacterFilter_ChangePortalMode(object sender, Decal.Adapter.Wrappers.ChangePortalModeEventArgs e) {
            try {
                if (e.Type != Decal.Adapter.Wrappers.PortalEventType.ExitPortal)
                    return;

                if (lastPortalExitLandcell == UB.Core.Actions.Landcell) {
                    portalExitCount++;
                }
                else {
                    portalExitCount = 1;
                    lastPortalExitLandcell = UB.Core.Actions.Landcell;
                }
                
                if (portalExitCount >= PortalLoopCount) {
                    DoPortalLoopFix();
                    return;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void CharacterFilter_LoginComplete(object sender, EventArgs e) {
            try {
                UB.Core.CharacterFilter.LoginComplete -= CharacterFilter_LoginComplete;
                Enable();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public void Enable() {
            UBHelper.vTank.Enable();
            UB.Core.CharacterFilter.Logoff += CharacterFilter_Logoff;
        }

        private void CharacterFilter_Logoff(object sender, Decal.Adapter.Wrappers.LogoffEventArgs e) {
            try {
                if (e.Type == Decal.Adapter.Wrappers.LogoffEventType.Authorized)
                    UBHelper.vTank.Disable();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void DoPortalLoopFix() {
            Util.WriteToChat($"Nav: {UBHelper.vTank.Instance.NavCurrent}");
            Util.DispatchChatToBoxWithPluginIntercept($"/vt nav save {VTNavRoute.NoneNavName}");
            UBHelper.vTank.Instance.NavDeletePoint(0);
        }

        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    UB.Core.CharacterFilter.LoginComplete -= CharacterFilter_LoginComplete;
                    UB.Core.CharacterFilter.Logoff -= CharacterFilter_Logoff;
                    
                    if (isFixingPortalLoops)
                        UB.Core.CharacterFilter.ChangePortalMode -= CharacterFilter_ChangePortalMode;

                    base.Dispose(disposing);
                }
                disposedValue = true;
            }
        }
    }
}
