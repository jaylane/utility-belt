using Decal.Adapter;
using Decal.Adapter.Wrappers;
using Microsoft.DirectX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VirindiViewService;

namespace UtilityBelt.Lib.Dungeon {
    class TrackedObject : IDisposable {
        public bool IsDisposed = false;
        public string Name = "";
        public int PrevZLayer = 0;
        public int ZLayer = 0;
        public int Id = 0;
        public Vector3 Position = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        public int Icon = 0;
        public ObjectClass ObjectClass;
        public bool Static = true;
        public bool ShouldDraw = true;
        public bool GotPosition = false;

        public const double UPDATE_DISTANCE = 25;

        public static Dictionary<int, List<TrackedObject>> ByZLayer = new Dictionary<int, List<TrackedObject>>();

        public TrackedObject(int id) {
            Id = id;

            var wo = CoreManager.Current.WorldFilter[id];
            Name = wo.Name;

            if (wo.ObjectClass == ObjectClass.Portal || wo.ObjectClass == ObjectClass.Npc) {
                Name = Name.Replace("Portal to ", "").Replace(" Portal", "");
            }

            ObjectClass = wo.ObjectClass;
            Icon = wo.Icon;
            Static = !(wo.ObjectClass == ObjectClass.Player || wo.ObjectClass == ObjectClass.Monster);
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

                if (oldZ != newZ) {
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
                    var z = (int)Math.Floor((Position.Z + 1) / 6) * 6;
                    if (ByZLayer.ContainsKey(z)) ByZLayer[z].Remove(this);
                }
                disposedValue = true;
            }
        }
        public void Dispose() {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}
