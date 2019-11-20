using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib {
    public static class Geometry {
        public static uint GetLandblockFromCoordinates(float ew, float ns) {
            float lat2 = (ns * 240.0f + 84.0f) / 192.0f;
            float lng2 = (ew * 240.0f + 84.0f) / 192.0f;

            uint lblat = (uint)(lat2 + 0x7f);
            uint lblng = (uint)(lng2 + 0x7f);

            return (lblng << 8) | lblat;
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

        private static int LandblockXDifference(uint originLandblock, uint landblock) {
            var olbx = originLandblock >> 24;
            var lbx = landblock >> 24;

            return (int)(lbx - olbx) * 192;
        }

        private static int LandblockYDifference(uint originLandblock, uint landblock) {
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

        public static float Distance2d(float x1, float y1, float x2, float y2) {
            return (float)Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
        }
    }
}
