using Decal.Adapter.Wrappers;
using Decal.Filters;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.VTNav.Waypoints {
    class VTNPortal : VTNPoint {
        public string Name = "Portal";
        public int Id = 0;
        public ObjectClass ObjectClass = ObjectClass.Unknown;

        public double PortalNS = 0;
        public double PortalEW = 0;
        public double PortalZ = 0;

        internal double closestDistance = double.MaxValue;

        public VTNPortal(StreamReader reader, VTNavRoute parentRoute, int index) : base(reader, parentRoute, index) {
            Type = eWaypointType.Portal;

            Globals.Core.WorldFilter.CreateObject += WorldFilter_CreateObject;
        }

        private void WorldFilter_CreateObject(object sender, CreateObjectEventArgs e) {
            try {
                if (e.New.ObjectClass == ObjectClass && e.New.Name == Name) {
                    var c = e.New.Coordinates();
                    var rc = e.New.RawCoordinates();
                    var distance = Util.GetDistance(new Vector3Object(c.EastWest, c.NorthSouth, rc.Z/240), new Vector3Object(PortalEW, PortalNS, PortalZ));

                    if (distance < closestDistance) {
                        closestDistance = distance;
                        Id = e.New.Id;
                        Globals.Core.WorldFilter.CreateObject -= WorldFilter_CreateObject;

                        foreach (var shape in shapes) {
                            try {
                                try { shape.Visible = false; } catch { }

                                shape.Dispose();
                            }
                            catch { }
                        }
                        shapes.Clear();

                        NS = e.New.Coordinates().NorthSouth;
                        EW = e.New.Coordinates().EastWest;
                        Z = e.New.RawCoordinates().Z / 240;

                        Draw();
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        new public bool Parse() {
            if (!base.Parse()) return false;
            Name = base.sr.ReadLine();

            var objectClassLine = base.sr.ReadLine(); // 14 ObjectClass?
            var objectClassInt = 0;
            if (!int.TryParse(objectClassLine, out objectClassInt)) {
                Util.WriteToChat("Could not parse ObjectClass");
                return false;
            }
            ObjectClass = (ObjectClass)objectClassInt;

            base.sr.ReadLine(); // true ?

            // these are portal exit coordinates
            if (!double.TryParse(sr.ReadLine(), out PortalNS)) {
                Util.WriteToChat("Could not parse PortalNS");
                return false;
            }

            if (!double.TryParse(sr.ReadLine(), out PortalEW)) {
                Util.WriteToChat("Could not parse PortalEW");
                return false;
            }

            if (!double.TryParse(sr.ReadLine(), out PortalZ)) {
                Util.WriteToChat("Could not parse PortalZ");
                return false;
            }

            NS = PortalNS;
            EW = PortalEW;
            Z = PortalZ;

            var wos = Globals.Core.WorldFilter.GetByName(Name);
            foreach (var wo in wos) {
                if (wo.ObjectClass == ObjectClass) {
                    var c = wo.Coordinates();
                    var rc = wo.RawCoordinates();
                    var distance = Util.GetDistance(new Vector3Object(c.EastWest, c.NorthSouth, rc.Z / 240), new Vector3Object(PortalEW, PortalNS, PortalZ));

                    if (distance < closestDistance) {
                        closestDistance = distance;
                        NS = wo.Coordinates().NorthSouth;
                        EW = wo.Coordinates().EastWest;
                        Z = wo.RawCoordinates().Z / 240;
                        Id = wo.Id;
                        
                        Globals.Core.WorldFilter.CreateObject -= WorldFilter_CreateObject;
                    }
                }
            }

            return true;
        }

        public override void Draw() {
            var rp = GetPreviousPoint();
            var color = Color.FromArgb(Globals.Config.VisualNav.LineColor.Value);

            if (closestDistance < double.MaxValue) {
                if (rp != null && Globals.Config.VisualNav.ShowLine.Value) {
                    DrawLineTo(rp, color);
                }

                color = Color.FromArgb(Globals.Config.VisualNav.PortalColor.Value);

                var height = 1.55f;
                if (Globals.Core.Actions.IsValidObject(Id)) {
                    height = Globals.Core.Actions.Underlying.ObjectHeight(Id);
                }
                height = height > 0 ? height : 1.55f;

                if (Globals.Config.VisualNav.ShowPortal.Value) {
                    DrawText("Use: " + Name, this, height, color);
                }
            }
        }

        protected override void Dispose(bool disposing) {
            if (!base.disposed) {
                if (disposing) {
                    foreach (var shape in shapes) {
                        try { shape.Visible = false; } catch { }
                        shape.Dispose();
                    }
                    Globals.Core.WorldFilter.CreateObject -= WorldFilter_CreateObject;
                }
                base.disposed = true;
            }
        }
    }
}
