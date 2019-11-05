using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using UtilityBelt.Lib.VTNav.Waypoints;
using UtilityBelt.Tools;

namespace UtilityBelt.Lib.VTNav {

    public class VTNavRoute : IDisposable {
        private bool disposed = false;
        public string NavPath;
        public string Header = "uTank2 NAV 1.2";
        public int RecordCount = 0;
        public eNavType NavType = eNavType.Circular;

        public int TargetId = 0;
        public string TargetName = "";

        public static string NoneNavName = " [None]";

        public List<VTNPoint> points = new List<VTNPoint>();
        public Dictionary<string, double> offsets = new Dictionary<string, double>();
        public List<D3DObj> shapes = new List<D3DObj>();
        private D3DObj currentNavShape = null;

        public VTNavRoute(string navPath) {
            NavPath = navPath;

            uTank2.PluginCore.PC.NavWaypointChanged += PC_NavWaypointChanged;
            Globals.Settings.VisualNav.Display.CurrentWaypoint.PropertyChanged += (s, e) => { UpdateCurrentWaypoint(); };
        }

        public void AddOffset(double ns, double ew, double offset) {
            var key = $"{ns},{ew}";

            if (offsets.ContainsKey(key)) {
                offsets[key] += offset;
            }
            else {
                offsets.Add(key, GetZOffset(ns, ew) + offset);
            }
        }

        public double GetZOffset(double ns, double ew) {
            var key = $"{ns},{ew}";

            if (offsets.ContainsKey(key)) {
                return offsets[key];
            }
            else {
                return 0.25;
            }
        }

        public bool Parse() {
            try {
                using (StreamReader sr = File.OpenText(NavPath)) {
                    Header = sr.ReadLine();

                    var navTypeLine = sr.ReadLine();
                    int navType = 0;
                    if (!int.TryParse(navTypeLine, out navType)) {
                        Util.WriteToChat("Could not parse navType from nav file: " + navTypeLine);
                        return false;
                    }
                    NavType = (eNavType)navType;

                    if (NavType == eNavType.Target) {
                        if (sr.EndOfStream) {
                            Util.WriteToChat("Follow nav is empty");
                            return true;
                        }

                        TargetName = sr.ReadLine();
                        var targetId = sr.ReadLine();
                        if (!int.TryParse(targetId, out TargetId)) {
                            Util.WriteToChat("Could not parse target id: " + targetId);
                            return false;
                        }

                        Globals.Core.WorldFilter.CreateObject += WorldFilter_CreateObject;
                        Globals.Core.WorldFilter.ReleaseObject += WorldFilter_ReleaseObject;

                        return true;
                    }

                    var recordCount = sr.ReadLine();
                    if (!int.TryParse(recordCount, out RecordCount)) {
                        Util.WriteToChat("Could not read record count from nav file: " + recordCount);
                        return false;
                    }

                    int x = 0;
                    VTNPoint previous = null;
                    while (!sr.EndOfStream && points.Count < RecordCount) {
                        int recordType = 0;
                        var recordTypeLine = sr.ReadLine();

                        if (!int.TryParse(recordTypeLine, out recordType)) {
                            Util.WriteToChat($"Unable to parse recordType: {recordTypeLine}");
                            return false;
                        }

                        VTNPoint point = null;

                        switch ((eWaypointType)recordType) {
                            case eWaypointType.ChatCommand:
                                point = new VTNChat(sr, this, x);
                                ((VTNChat)point).Parse();
                                break;

                            case eWaypointType.Checkpoint:
                                point = new VTNPoint(sr, this, x);
                                ((VTNPoint)point).Parse();
                                break;

                            case eWaypointType.Jump:
                                point = new VTNJump(sr, this, x);
                                ((VTNJump)point).Parse();
                                break;

                            case eWaypointType.OpenVendor:
                                point = new VTNOpenVendor(sr, this, x);
                                ((VTNOpenVendor)point).Parse();
                                break;

                            case eWaypointType.Other: // no clue here...
                                throw new System.Exception("eWaypointType.Other");

                            case eWaypointType.Pause:
                                point = new VTNPause(sr, this, x);
                                ((VTNPause)point).Parse();
                                break;

                            case eWaypointType.Point:
                                point = new VTNPoint(sr, this, x);
                                ((VTNPoint)point).Parse();
                                break;

                            case eWaypointType.Portal:
                                point = new VTNPortal(sr, this, x);
                                ((VTNPortal)point).Parse();
                                break;

                            case eWaypointType.Portal2:
                                point = new VTNPortal(sr, this, x);
                                ((VTNPortal)point).Parse();
                                break;

                            case eWaypointType.Recall:
                                point = new VTNRecall(sr, this, x);
                                ((VTNRecall)point).Parse();
                                break;

                            case eWaypointType.UseNPC:
                                point = new VTNUseNPC(sr, this, x);
                                ((VTNUseNPC)point).Parse();
                                break;
                        }

                        if (point != null) {
                            point.Previous = previous;
                            points.Add(point);
                            previous = point;
                            x++;
                        }

                    }

                    return true;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return false;
        }

        private void WorldFilter_ReleaseObject(object sender, ReleaseObjectEventArgs e) {
            try {
                if (NavType != eNavType.Target) {
                    Globals.Core.WorldFilter.ReleaseObject -= WorldFilter_ReleaseObject;
                    return;
                }

                if (e.Released.Id == TargetId) {
                    Globals.Core.WorldFilter.ReleaseObject -= WorldFilter_ReleaseObject;
                    Globals.Core.WorldFilter.CreateObject += WorldFilter_CreateObject;
                    foreach (var shape in shapes) {
                        try {
                            shape.Visible = false;
                        }
                        finally {
                            shape.Dispose();
                        }
                    }
                    shapes.Clear();
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void WorldFilter_CreateObject(object sender, CreateObjectEventArgs e) {
            try {
                if (NavType != eNavType.Target) {
                    Globals.Core.WorldFilter.CreateObject -= WorldFilter_CreateObject;
                    return;
                }

                if (e.New.Id == TargetId) {
                    Globals.Core.WorldFilter.CreateObject -= WorldFilter_CreateObject;
                    Draw();
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void PC_NavWaypointChanged() {
            try {
                if (NavType == eNavType.Once && points.Count > VTankControl.vTankInstance.NavNumPoints) {
                    var pointsDeleted = 0;
                    while (points.Count > 0 && points.Count > VTankControl.vTankInstance.NavNumPoints) {
                        points[0].Dispose();
                        points.RemoveAt(0);
                        pointsDeleted++;
                    }

                    if (pointsDeleted > 0) {
                        for (var i = 0; i < points.Count; i++) {
                            points[i].index = i;
                        }
                    }
                }

                UpdateCurrentWaypoint();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UpdateCurrentWaypoint() {
            var routeFinished = VTankControl.vTankInstance.NavCurrent > VTankControl.vTankInstance.NavNumPoints - 1;
            var isWaypointRoute = NavType != eNavType.Target;
            var isEnabled = Globals.Settings.VisualNav.Display.CurrentWaypoint.Enabled;

            if (isEnabled && isWaypointRoute && !routeFinished) {
                var current = VTankControl.vTankInstance.NavGetPoint(VTankControl.vTankInstance.NavCurrent);

                if (currentNavShape == null) {
                    currentNavShape = Globals.Core.D3DService.MarkCoordsWithShape(0f, 0f, 0f, D3DShape.Ring, Color.Red.ToArgb());
                }

                // this is dumb, i cant get it to convert straight to a float
                var navCloseStopRangeStr = VTankControl.vTankInstance.GetSetting("NavCloseStopRange").ToString();

                if (float.TryParse(navCloseStopRangeStr, out float navCloseStopRange)) {
                    currentNavShape.Visible = true;
                    currentNavShape.ScaleX = (float)navCloseStopRange * 240f;
                    currentNavShape.ScaleY = (float)navCloseStopRange * 240f;
                    currentNavShape.Anchor((float)current.loc.y, (float)current.loc.x, (float)(current.loc.z * 240f) + Globals.Settings.VisualNav.LineOffset);
                    currentNavShape.Color = Globals.Settings.VisualNav.Display.CurrentWaypoint.Color;
                    return;
                }
            }

            if (currentNavShape != null) {
                currentNavShape.Visible = false;
            }
        }

        public void Draw() {
            if (NavType == eNavType.Target && Globals.Settings.VisualNav.Display.FollowArrow.Enabled) {
                if (TargetId != 0 && TargetId != Globals.Core.CharacterFilter.Id) {
                    var wo = Globals.Core.WorldFilter[TargetId];

                    if (wo != null) {
                        var color = Globals.Settings.VisualNav.Display.FollowArrow.Color;
                        var shape = Globals.Core.D3DService.PointToObject(TargetId, color);
                        shape.Scale(0.6f);
                        shapes.Add(shape);
                    }
                }
            }
            else {
                for (var i=0; i < points.Count; i++) {
                    points[i].Draw();
                }
            }

            UpdateCurrentWaypoint();
        }

        public static bool IsPretendNoneNav() {
            var server = Globals.Core.CharacterFilter.Server;
            var character = Globals.Core.CharacterFilter.Name;
            var path = Path.Combine(Util.GetVTankProfilesDirectory(), $"{server}_{character}.cdf");

            var contents = File.ReadAllLines(path);

            if (contents.Length >= 4) {
                var navFile = contents[3].Trim();
                return navFile.StartsWith(NoneNavName);
            }

            return false;
        }

        public static string GetLoadedNavigationProfile() {
            var server = Globals.Core.CharacterFilter.Server;
            var character = Globals.Core.CharacterFilter.Name;
            var path = Path.Combine(Util.GetVTankProfilesDirectory(), $"{server}_{character}.cdf");

            var contents = File.ReadAllLines(path);

            if (contents.Length >= 4) {
                var navFile = contents[3].Trim();
                var navPath = Path.Combine(Util.GetVTankProfilesDirectory(), navFile);

                if (navFile.Length <= 0) return null;

                return navPath;
            }

            return null;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                Globals.Core.WorldFilter.CreateObject -= WorldFilter_CreateObject;
                Globals.Core.WorldFilter.ReleaseObject -= WorldFilter_ReleaseObject;
                uTank2.PluginCore.PC.NavWaypointChanged -= PC_NavWaypointChanged;

                foreach (var shape in shapes) {
                    try {
                        shape.Visible = false;
                    }
                    finally {
                        try {
                            shape.Dispose();
                        }
                        catch { }
                    }
                }

                shapes.Clear();

                if (currentNavShape != null) {
                    try {
                        currentNavShape.Visible = false;
                        currentNavShape.Dispose();
                    }
                    catch { }
                }

                foreach (var point in points) {
                    point.Dispose();
                }
                disposed = true;
            }
        }

        internal List<VTNPoint> GetAllNavPoints() {
            throw new NotImplementedException();
        }
    }
}