using Decal.Adapter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.DirectX;
using System.Runtime.InteropServices;

namespace UtilityBelt.Lib {
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct CameraFrame {
        public IntPtr vtable;
        public uint landblock;

        // quat
        public float qw, qx, qy, qz;

        // matrix
        public float m11, m12, m13;
        public float m21, m22, m23;
        public float m31, m32, m33;

        // vec
        public float x, y, z;

        public unsafe static CameraFrame Get(int obj) {
            CameraFrame* f = (CameraFrame*)obj;
            return *f;
        }

        public override string ToString() {
            return $"landblock: 0x{landblock:X8}\nqw: {qw}, qx: {qx}, qy: {qy}, qz: {qz}\nm11: {m11}, m12: {m12}, m13: {m13}\nm21: {m21}, m22: {m22}, m23: {m23}\nm31: {m31}, m32: {m32}, m33: {m33}\nx: {x}, y: {y}, z: {z}";
        }
    }

    public static class Camera {
        public static CameraFrame GetFrame() {
            return CameraFrame.Get(CoreManager.Current.Actions.Underlying.SmartboxPtr() + 0x08);
        }

        public static Matrix GetD3DViewTransform() {
            var cameraFrame = GetFrame();

            // build a new temporary matrix from cameraFrame, just to get our translation
            // (swapping y/z, which converts from ac coords to d3d coords system)
            var m = new Matrix() {
                M11 = cameraFrame.m11,
                M12 = cameraFrame.m13,
                M13 = cameraFrame.m12,
                M21 = cameraFrame.m21,
                M22 = cameraFrame.m23,
                M23 = cameraFrame.m22,
                M31 = cameraFrame.m31,
                M32 = cameraFrame.m33,
                M33 = cameraFrame.m32,
                M41 = cameraFrame.x,
                M42 = cameraFrame.z,
                M43 = cameraFrame.y,
                M44 = 1
            };

            // if we invert the matrix above, we get a good M41,M42,M43 for translation
            m.Invert();

            // (convert from row major to column major)
            var viewTransform = new Matrix() {
                M11 = cameraFrame.m11,
                M12 = cameraFrame.m31,
                M13 = cameraFrame.m21,
                M14 = 0,
                M21 = cameraFrame.m13,
                M22 = cameraFrame.m33,
                M23 = cameraFrame.m23,
                M24 = 0,
                M31 = cameraFrame.m12,
                M32 = cameraFrame.m32,
                M33 = cameraFrame.m22,
                M34 = 0,
                M41 = m.M41,
                M42 = m.M43,
                M43 = m.M42,
                M44 = 1
            };

            return viewTransform;
        }
    }
}
