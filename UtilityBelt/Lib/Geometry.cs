using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib {
    public static class Geometry {
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

        public static float Distance2d(float x1, float y1, float x2, float y2) {
            return (float)Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
        }
    }
}
