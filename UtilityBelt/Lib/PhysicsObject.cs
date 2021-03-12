using Decal.Adapter;
using Decal.Adapter.Wrappers;
using Microsoft.DirectX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace UtilityBelt.Lib {
    public static class PhysicsObject {
        public static unsafe Vector3 GetPosition(int id) {
            if (CoreManager.Current.Actions.IsValidObject(id)) {
                var p = CoreManager.Current.Actions.Underlying.GetPhysicsObjectPtr(id);
                return new Vector3(*(float*)(p + 0x84), *(float*)(p + 0x88), *(float*)(p + 0x8C));
            }

            return new Vector3();
        }

        public static unsafe double GetDistance(int id) {
            if (CoreManager.Current.Actions.IsValidObject(id)) {
                var pos = PhysicsObject.GetPosition(id);
                var landcell = PhysicsObject.GetLandcell(id);
                var coords = new Coordinates(Geometry.LandblockToEW((uint)landcell, pos.X), Geometry.LandblockToNS((uint)landcell, pos.Y), pos.Z);
                return coords.DistanceTo(Coordinates.Me);
            }

            return double.MaxValue;
        }

        public static unsafe int GetLandcell(int id) {
            if (CoreManager.Current.Actions.IsValidObject(id)) {
                var p = CoreManager.Current.Actions.Underlying.GetPhysicsObjectPtr(id);
                return *(int*)(p + 0x4C);
            }

            return 0;
        }

        internal static unsafe Quaternion GetRot(int id) {
            if (CoreManager.Current.Actions.IsValidObject(id)) {
                var p = CoreManager.Current.Actions.Underlying.GetPhysicsObjectPtr(id);
                return new Quaternion(*(float*)(p + 0x50), *(float*)(p + 0x54), *(float*)(p + 0x58), *(float*)(p + 0x5C));
            }

            return new Quaternion(0,0,0,0);
        }
    }
}
