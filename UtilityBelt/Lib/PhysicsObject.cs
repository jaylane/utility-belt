using Decal.Adapter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace UtilityBelt.Lib {
    [StructLayout(LayoutKind.Sequential)]
    public class PhysicsObject {
        public int VTable;
        public int Landblock;

        [StructLayout(LayoutKind.Sequential)]
        public struct QuaternionT { public float W, X, Y, Z; }
        public QuaternionT Quaternion;

        [StructLayout(LayoutKind.Sequential)]
        public struct HeadingT { public float X, Y, Z; }
        public HeadingT Heading;

        [StructLayout(LayoutKind.Sequential)]
        public struct MatrixT { public float m0, m1, m2, m3, m4, m5; }
        public MatrixT Matrix;

        [StructLayout(LayoutKind.Sequential)]
        public struct PositionT { public float X, Y, Z; }
        public PositionT Position;

        public static PhysicsObject FromId(int id) {
            try {
                unsafe {
                    PhysicsObject obj = new PhysicsObject();

                    IntPtr ptr = CoreManager.Current.Actions.PhysicsObject(id);
                    IntPtr offsetPtr = new IntPtr((int)ptr + 0x48);

                    obj = (PhysicsObject)Marshal.PtrToStructure(offsetPtr, obj.GetType());

                    return obj;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return null;
        }
    }
}
