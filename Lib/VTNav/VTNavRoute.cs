using System;
using System.Collections.Generic;
using System.IO;
using UtilityBelt.Lib.VTNav.Waypoints;

namespace UtilityBelt.Lib.VTNav {

    public class VTNavRoute : IDisposable {
        private bool disposed = false;
        public string NavPath;
        public string Header = "uTank2 NAV 1.2";
        public int RecordCount = 0;
        public eNavType NavType = eNavType.Circular;

        public List<VTNPoint> points = new List<VTNPoint>();
        public Dictionary<string, double> offsets = new Dictionary<string, double>();

        public VTNavRoute(string navPath) {
            NavPath = navPath;
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
            using (StreamReader sr = File.OpenText(NavPath)) {
                Header = sr.ReadLine();

                var navTypeLine = sr.ReadLine();
                int navType = 0;
                if (!int.TryParse(navTypeLine, out navType)) {
                    Util.WriteToChat("Could not parse navType from nav file: " + navTypeLine);
                    return false;
                }
                NavType = (eNavType)navType;

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
                            break;

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

        public void Draw() {
            foreach (var point in points) {
                point.Draw();
            }
        }

        public  static string GetLoadedNavigationProfile() {
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
                foreach (var point in points) {
                    point.Dispose();
                }
                disposed = true;
            }
        }
    }
}