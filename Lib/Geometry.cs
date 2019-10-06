using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib {
    public static class Geometry {
        public static int GetLandblockFromCoordinates(float ew, float ns) {
            var xfract = (ew -0.2f - (-101.9f)) / (102f - (-101.9f));
            var yfract = 1f - (ns - 0.2f - (-102f)) / (101.9f - (-102f));
            ushort lbx = (ushort)Math.Floor(xfract * 256f);
            ushort lby = (ushort)(256f - Math.Floor(yfract * 256f));

            // fixy this
            return int.Parse(lbx.ToString("X2") + lby.ToString("X2") + "0000", System.Globalization.NumberStyles.HexNumber);
        }

        public static PointF LandblockOffsetFromCoordinates(float ew, float ns) {
            var landblock = GetLandblockFromCoordinates(ew, ns);
            return new PointF(
                    EWToLandblock(landblock, ew),
                    NSToLandblock(landblock, ns)
            );
        }

        public static PointF LandblockOffsetFromCoordinates(int originLandblock, float ew, float ns) {
            var landblock = GetLandblockFromCoordinates(ew, ns);
            return new PointF(
                    EWToLandblock(landblock, ew) + LandblockXDifference(originLandblock, landblock),
                    NSToLandblock(landblock, ns) + LandblockYDifference(originLandblock, landblock)
            );
        }

        private static int LandblockXDifference(int originLandblock, int landblock) {
            var olbx = originLandblock >> 24;
            var lbx = landblock >> 24;

            return (lbx - olbx) * 192;
        }

        private static int LandblockYDifference(int originLandblock, int landblock) {
            var olby = originLandblock << 8 >> 24;
            var lby = landblock << 8 >> 24;

            return (lby - olby) * 192;
        }

        public static float NSToLandblock(int landcell, float ns) {
            uint l = (uint)((landcell & 0x00FF0000) / 0x2000);
            var yOffset = ((ns * 10) - l + 1019.5) * 24;
            return (float)yOffset;
        }

        public static float EWToLandblock(int landcell, float ew) {
            uint l = (uint)((landcell & 0xFF000000) / 0x200000);
            var yOffset = ((ew * 10) - l + 1019.5) * 24;
            return (float)yOffset;
        }

        public static float Distance2d(float x1, float y1, float x2, float y2) {
            return (float)Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
        }
    }
}
