using Microsoft.DirectX;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib {
    public static class Geometry {
        public static Quaternion HeadingToQuaternion(float angle) {
            return ToQuaternion((float)Math.PI * -angle / 180.0f, 0, 0);
        }
        public static Quaternion RadiansToQuaternion(float angle) {
            return ToQuaternion(angle, 0, 0);
        }
        public static unsafe double QuaternionToHeading(Quaternion q) {
            // yaw (z-axis rotation)
            return Math.Atan2(2 * (q.W * q.Z + q.X * q.Y), 1 - 2 * (q.Y * q.Y + q.Z * q.Z));
        }

        // https://en.wikipedia.org/wiki/Conversion_between_quaternions_and_Euler_angles
        public static Quaternion ToQuaternion(float yaw, float pitch, float roll) { // yaw (Z), pitch (Y), roll (X)
            // Abbreviations for the various angular functions
            float cy = (float)Math.Cos(yaw * 0.5);
            float sy = (float)Math.Sin(yaw * 0.5);
            float cp = (float)Math.Cos(pitch * 0.5);
            float sp = (float)Math.Sin(pitch * 0.5);
            float cr = (float)Math.Cos(roll * 0.5);
            float sr = (float)Math.Sin(roll * 0.5);

            Quaternion q = new Quaternion();

            q.W = cy * cp * cr + sy * sp * sr;
            q.X = cy * cp * sr - sy * sp * cr;
            q.Y = sy * cp * sr + cy * sp * cr;
            q.Z = sy * cp * cr - cy * sp * sr;

            return q;
        }

        public static uint GetLandblockFromCoordinates(float EW, float NS) {
            NS -= 0.5f;
            EW -= 0.5f;
            NS *= 10.0f;
            EW *= 10.0f;

            uint basex = (uint)(EW + 0x400);
            uint basey = (uint)(NS + 0x400);

            if ((int)(basex) < 0 || (int)(basey) < 0 || basex >= 0x7F8 || basey >= 0x7F8) {
                Console.WriteLine("Out of Bounds");
            }
            byte blockx = (byte)(basex >> 3);
            byte blocky = (byte)(basey >> 3);
            byte cellx = (byte)(basex & 7);
            byte celly = (byte)(basey & 7);

            int block = (blockx << 8) | (blocky);
            int cell = (cellx << 3) | (celly);

            int dwCell = (block << 16) | (cell + 1);

            return (uint)dwCell;
        }

        public static PointF LandblockOffsetFromCoordinates(float ew, float ns) {
            var landblock = GetLandblockFromCoordinates(ew, ns);
            return new PointF(
                    EWToLandblock(landblock, ew),
                    NSToLandblock(landblock, ns)
            );
        }

        public static PointF LandblockOffsetFromCoordinates(uint originLandblock, float ew, float ns) {
            var landblock = GetLandblockFromCoordinates(ew, ns);
            return new PointF(
                    EWToLandblock(landblock, ew) + LandblockXDifference(originLandblock, landblock),
                    NSToLandblock(landblock, ns) + LandblockYDifference(originLandblock, landblock)
            );
        }

        public static int LandblockXDifference(uint originLandblock, uint landblock) {
            var olbx = originLandblock >> 24;
            var lbx = landblock >> 24;

            return (int)(lbx - olbx) * 192;
        }

        public static int LandblockYDifference(uint originLandblock, uint landblock) {
            var olby = originLandblock << 8 >> 24;
            var lby = landblock << 8 >> 24;

            return (int)(lby - olby) * 192;
        }

        public static float NSToLandblock(uint landcell, float ns) {
            uint l = (uint)((landcell & 0x00FF0000) / 0x2000);
            var yOffset = ((ns * 10) - l + 1019.5) * 24;
            return (float)yOffset;
        }

        public static float EWToLandblock(uint landcell, float ew) {
            uint l = (uint)((landcell & 0xFF000000) / 0x200000);
            var yOffset = ((ew * 10) - l + 1019.5) * 24;
            return (float)yOffset;
        }

        public static float LandblockToNS(uint landcell, float yOffset) {
            uint l = (uint)((landcell & 0x00FF0000) / 0x2000);
            var ns = ((yOffset / 24) + l - 1019.5) / 10;
            return (float)ns;
        }

        public static float LandblockToEW(uint landcell, float xOffset) {
            uint l = (uint)((landcell & 0xFF000000) / 0x200000);
            var ew = ((xOffset / 24) + l - 1019.5) / 10;
            return (float)ew;
        }

        public static float Distance2d(float x1, float y1, float x2, float y2) {
            return (float)Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
        }
    }
}
