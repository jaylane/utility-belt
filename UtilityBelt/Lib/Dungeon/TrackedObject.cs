using Decal.Adapter;
using Decal.Adapter.Wrappers;
using Microsoft.DirectX;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using UtilityBelt.Lib.Models;
using UtilityBelt.Tools;
using VirindiViewService;

namespace UtilityBelt.Lib.Dungeon {
    public struct LegendEntry {
        public string Name;
        public int Wcid;
        public Color Color;
        public int Icon;

        public LegendEntry(int wcid, string name, Color color, int icon) {
            Wcid = wcid;
            Name = name;
            Color = color;
            Icon = icon;
        }
    }

    public class TrackedObject : IDisposable {
        public bool IsDisposed = false;
        public string Name = "";
        public int PrevZLayer = 0;
        public int ZLayer = 0;
        public int Id = 0;
        public int Wcid = 0;
        public int Type = 0;
        public Vector3 Position = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        public int Icon = 0;
        public ObjectClass ObjectClass;
        public bool Static = true;
        public bool ShouldDraw = true;
        public bool GotPosition = false;
        public bool IsLSD = false;
        private static List<Color> iconTints = new List<Color>() {
            Color.Magenta,
            Color.LimeGreen,
            Color.Yellow,
            Color.LightPink,
        };

        private static int nextIconIndex = 0;
        public const double UPDATE_DISTANCE = 25;

        public static Dictionary<int, List<TrackedObject>> ByZLayer = new Dictionary<int, List<TrackedObject>>();
        public static Dictionary<int, LegendEntry> Legend = new Dictionary<int, LegendEntry>();
        public static List<int> LegendIcons = new List<int>();
        public static Dictionary<int, int> LinkIndicators = new Dictionary<int, int>();
        private static int currentLink = 0;

        public TrackedObject(int id) {
            Id = id;

            var wo = CoreManager.Current.WorldFilter[id];
            Type = wo.Values(LongValueKey.Type, 0);
            Name = wo.Name;
            if (wo.ObjectClass == ObjectClass.Portal || wo.ObjectClass == ObjectClass.Npc) {
                Name = Name.Replace("Portal to ", "").Replace(" Portal", "");
            }

            ObjectClass = wo.ObjectClass;
            Icon = 0x06000000 + wo.Icon;
            Static = !(wo.ObjectClass == ObjectClass.Player || wo.ObjectClass == ObjectClass.Monster);
        }

        public TrackedObject(WeenieSpawn spawn, Landblock lb) {
            Id = spawn.Id;
            Name = spawn.Weenie.StringValue(1, Id.ToString("X8"));
            Icon = spawn.Weenie.DIDValue(8, 0);
            Type = spawn.Weenie.Type;
            Static = true;
            Position = new Vector3(spawn.Position.Frame.Origin.X, spawn.Position.Frame.Origin.Y, spawn.Position.Frame.Origin.Z);
            ObjectClass = spawn.Weenie.GetObjectClass();
            GotPosition = true;
            IsLSD = true;
            Wcid = spawn.Wcid;
            var newZ = (int)(Math.Floor((Position.Z + 3) / 6) * 6);
            if (!ByZLayer.ContainsKey(newZ)) ByZLayer.Add(newZ, new List<TrackedObject>());
            if (!ByZLayer[newZ].Contains(this)) ByZLayer[newZ].Add(this);

            if (ObjectClass == ObjectClass.Portal || ObjectClass == ObjectClass.Npc) {
                Name = Name.Replace("Portal to ", "").Replace(" Portal", "");
            }

            if (ObjectClass == ObjectClass.Monster && !Legend.ContainsKey(Wcid)) {
                Color color;
                if (LegendIcons.Contains(Icon)) {
                    color = iconTints[nextIconIndex++ % iconTints.Count];
                }
                else {
                    color = Color.White;
                    LegendIcons.Add(Icon);
                }
                Legend.Add(Wcid, new LegendEntry(Wcid, Name, color, Icon));
            }

            // locked doors / chests
            // TODO: show key name used to open it, if applicable
            if (spawn.Weenie.BoolValue((int)BoolValueKey.Locked, false) == true) {
                var diff = spawn.Weenie.IntValue(38, 0);
                if (diff > 0)
                    Name += $"(L:{diff})";
            }

            // links (currently only showing links with doors)
            var links = lb.Links.Where(l => l.Source == spawn.Id || l.Target == spawn.Id);
            var indicator = -1;
            foreach (var link in links) {
                var source = lb.Weenies.Find(w => w.Id == link.Source);
                var target = lb.Weenies.Find(w => w.Id == link.Target);
                if (source.Weenie.GetObjectClass() != ObjectClass.Door && target.Weenie.GetObjectClass() != ObjectClass.Door)
                    continue;

                if (LinkIndicators.ContainsKey(link.Source))
                    indicator = LinkIndicators[link.Source];
                else if (LinkIndicators.ContainsKey(link.Target))
                    indicator = LinkIndicators[link.Target];
                else {
                    indicator = currentLink++;
                    LinkIndicators.Add(spawn.Id, indicator);
                }
            }

            if (indicator >= 0)
                Name += $" [{(char)(65 + indicator)}]";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>true if this requires a redraw</returns>
        public unsafe bool Update(bool force) {
            if (Static && GotPosition) return false;

            if (!ShouldDraw || !CoreManager.Current.Actions.IsValidObject(Id) || ObjectClass == ObjectClass.Door) {
                Remove();
                return true;
            }

            var p = CoreManager.Current.Actions.Underlying.GetPhysicsObjectPtr(Id);
            var newPosition = new Vector3(*(float*)(p + 0x84), *(float*)(p + 0x88), *(float*)(p + 0x8C));

            if (Util.GetDistance(Position, newPosition) > UPDATE_DISTANCE) {
                var oldZ = (int)(Math.Floor((Position.Z + 3) / 6) * 6);
                var newZ = (int)(Math.Floor((newPosition.Z + 3) / 6) * 6);

                if (oldZ != newZ && ByZLayer != null) {
                    if (ByZLayer.ContainsKey(oldZ) && ByZLayer[oldZ].Contains(this)) ByZLayer[oldZ].Remove(this);
                    if (!ByZLayer.ContainsKey(newZ)) ByZLayer.Add(newZ, new List<TrackedObject>());
                    if (!ByZLayer[newZ].Contains(this)) ByZLayer[newZ].Add(this);
                    PrevZLayer = ZLayer;
                    ZLayer = newZ;
                }
                else {
                    PrevZLayer = ZLayer;
                }

                Position = newPosition;
                GotPosition = true;

                return true;
            }

            return false;
        }

        private void Remove() {
            Dispose();
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing) {
            IsDisposed = true;

            if (!disposedValue) {
                if (disposing) {
                    foreach (var kv in TrackedObject.ByZLayer) {
                        kv.Value.Remove(this);
                    }
                }
                disposedValue = true;
            }
        }
        public void Dispose() {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }

        internal static void Clear() {
            Legend.Clear();
            LegendIcons.Clear();
            LinkIndicators.Clear();
            foreach (var kv in TrackedObject.ByZLayer) {
                foreach (var obj in kv.Value) {
                    obj.Dispose();
                }
            }
            ByZLayer.Clear();
        }
        #endregion
    }
}
