using Decal.Adapter;
using Decal.Adapter.Wrappers;
using Decal.Filters;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.VTNav.Waypoints {
    class VTNOpenVendor : VTNPoint {
        public string Name = "Vendor";
        public int Id = 0;

        internal double closestDistance = double.MaxValue;

        public VTNOpenVendor(StreamReader reader, VTNavRoute parentRoute, int index) : base(reader, parentRoute, index) {
            Type = eWaypointType.OpenVendor;

            CoreManager.Current.WorldFilter.CreateObject += WorldFilter_CreateObject;
        }

        private void WorldFilter_CreateObject(object sender, CreateObjectEventArgs e) {
            try {
                if (e.New.ObjectClass == ObjectClass.Vendor && e.New.Name == Name) {
                    var c = e.New.Coordinates();
                    var rc = e.New.RawCoordinates();
                    var distance = Util.GetDistance(new Vector3Object(c.EastWest, c.NorthSouth, rc.Z / 240), new Vector3Object(EW, NS, Z));

                    if (distance < closestDistance) {
                        closestDistance = distance;
                        CoreManager.Current.WorldFilter.CreateObject -= WorldFilter_CreateObject;

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

            var idLine = base.sr.ReadLine();
            if (!int.TryParse(idLine, out Id)) {
                Util.WriteToChat("Could not parse VendorID");
                return false;
            }

            Name = base.sr.ReadLine();

            using (var wos = CoreManager.Current.WorldFilter.GetByName(Name)) {
                foreach (var wo in wos) {
                    if (wo.ObjectClass == ObjectClass.Vendor) {
                        var c = wo.Coordinates();
                        var rc = wo.RawCoordinates();
                        var distance = Util.GetDistance(new Vector3Object(c.EastWest, c.NorthSouth, rc.Z / 240), new Vector3Object(EW, NS, Z));

                        if (distance < closestDistance) {
                            closestDistance = distance;

                            NS = wo.Coordinates().NorthSouth;
                            EW = wo.Coordinates().EastWest;
                            Z = wo.RawCoordinates().Z / 240;

                            CoreManager.Current.WorldFilter.CreateObject -= WorldFilter_CreateObject;
                        }
                    }
                }
            }

            return true;
        }

        internal override void Write(StreamWriter file) {
            base.Write(file);
            file.WriteLine(Id);
            file.WriteLine(Name);
        }

        public override void Draw() {
            //base.Draw();
            var rp = GetPreviousPoint();
            var color = Color.FromArgb(UtilityBeltPlugin.Instance.VisualNav.Display.Lines.Color);

            if (closestDistance < double.MaxValue) {
                if (rp != null && UtilityBeltPlugin.Instance.VisualNav.Display.Lines.Enabled) {
                    DrawLineTo(rp, color);
                }

                var height = 1.55f;
                if (CoreManager.Current.Actions.IsValidObject(Id)) {
                    height = CoreManager.Current.Actions.Underlying.ObjectHeight(Id);
                }

                height = height > 0 ? (height) : (1.55f);
                color = Color.FromArgb(UtilityBeltPlugin.Instance.VisualNav.Display.OpenVendor.Color);

                if (UtilityBeltPlugin.Instance.VisualNav.Display.OpenVendor.Enabled) {
                    DrawText("Vendor: " + Name, this, height, color);
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
                    CoreManager.Current.WorldFilter.CreateObject -= WorldFilter_CreateObject;
                }
                base.disposed = true;
            }
        }
    }
}
