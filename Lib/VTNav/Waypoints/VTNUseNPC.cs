using Decal.Adapter.Wrappers;
using Decal.Filters;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.VTNav.Waypoints {
    class VTNUseNPC : VTNPoint {
        public string Name = "Vendor";
        public int Id = 0;
        public ObjectClass ObjectClass = ObjectClass.Npc;

        public double NpcNS = 0;
        public double NpcEW = 0;
        public double NpcZ = 0;

        internal double closestDistance = double.MaxValue;

        public VTNUseNPC(StreamReader reader, VTNavRoute parentRoute, int index) : base(reader, parentRoute, index) {
            Type = eWaypointType.UseNPC;

            Globals.Core.WorldFilter.CreateObject += WorldFilter_CreateObject;
        }

        private void WorldFilter_CreateObject(object sender, CreateObjectEventArgs e) {
            try {
                if (e.New.ObjectClass == ObjectClass && e.New.Name == Name) {
                    var c = e.New.Coordinates();
                    var rc = e.New.RawCoordinates();
                    var distance = Util.GetDistance(new Vector3Object(c.EastWest, c.NorthSouth, rc.Z / 240), new Vector3Object(NpcEW, NpcNS, NpcZ));

                    if (distance < closestDistance) {
                        closestDistance = distance;

                        Globals.Core.WorldFilter.CreateObject -= WorldFilter_CreateObject;

                        foreach (var shape in shapes) {
                            try {
                                try { shape.Visible = false; } catch (Exception ex) { }
                                shape.Dispose();
                            }
                            catch (Exception ex) { }
                        }
                        shapes.Clear();

                        NS = e.New.Coordinates().NorthSouth;
                        EW = e.New.Coordinates().EastWest;
                        Z = e.New.RawCoordinates().Z / 240;

                        Id = e.New.Id;

                        Draw();
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        new public bool Parse() {
            if (!base.Parse()) return false;

            Name = base.sr.ReadLine();
            var objectClass = 0;
            if (!int.TryParse(sr.ReadLine(), out objectClass)) {
                Util.WriteToChat($"Could not parse objectClass");
                return false;
            }
            ObjectClass = (ObjectClass)objectClass;

            base.sr.ReadLine(); // true?

            if (!double.TryParse(sr.ReadLine(), out NpcEW)) {
                Util.WriteToChat($"Could not parse NpcEW");
                return false;
            }
            if (!double.TryParse(sr.ReadLine(), out NpcNS)) {
                Util.WriteToChat($"Could not parse NpcNS");
                return false;
            }
            if (!double.TryParse(sr.ReadLine(), out NpcZ)) {
                Util.WriteToChat($"Could not parse NpcZ");
                return false;
            }

            var wos = Globals.Core.WorldFilter.GetByName(Name);
            WorldObject closestWO = null;

            foreach (var wo in wos) {
                if (wo.ObjectClass == ObjectClass) {
                    var c = wo.Coordinates();
                    var rc = wo.RawCoordinates();
                    var distance = Util.GetDistance(new Vector3Object(c.EastWest, c.NorthSouth, rc.Z / 240), new Vector3Object(NpcEW, NpcNS, NpcZ));

                    if (distance < closestDistance) {
                        closestDistance = distance;
                        closestWO = wo;
                    }
                }
            }

            if (closestWO != null) {
                NS = closestWO.Coordinates().NorthSouth;
                EW = closestWO.Coordinates().EastWest;
                Z = closestWO.RawCoordinates().Z / 240;
                Id = closestWO.Id;
                
                Globals.Core.WorldFilter.CreateObject -= WorldFilter_CreateObject;
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

                var height = 1.55f;
                if (Globals.Core.Actions.IsValidObject(Id)) {
                    height = Globals.Core.Actions.Underlying.ObjectHeight(Id);
                }
                height = height > 0 ? (height) : (1.55f);

                color = Color.FromArgb(Globals.Config.VisualNav.UseNPCColor.Value);

                if (Globals.Config.VisualNav.ShowUseNPC.Value) {
                    DrawText("Talk: " + Name, this, height, color);
                }
            }
        }

        protected override void Dispose(bool disposing) {
            if (!base.disposed) {
                if (disposing) {
                    foreach (var shape in shapes) {
                        try { shape.Visible = false; } catch (Exception ex) { }
                        shape.Dispose();
                    }
                    Globals.Core.WorldFilter.CreateObject -= WorldFilter_CreateObject;
                }
                base.disposed = true;
            }
        }
    }
}
