using Decal.Adapter;
using Decal.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Dungeon {
    public class DungeonCell {
        public int Landcell;
        public ushort EnvironmentId;
        public int X;
        public int Y;
        public int Z;
        public float R = 0;
        public float Rot = 0;

        public DungeonCell(int landcell) {
            byte[] cellFile = Util.FileService.GetCellFile(landcell);

            try {
                if (cellFile == null) {
                    Landcell = 0;
                    return;
                }

                Landcell = landcell;
                EnvironmentId = BitConverter.ToUInt16(cellFile, 16 + (int)cellFile[12] * 2);
                X = (int)Math.Round(BitConverter.ToSingle(cellFile, 20 + (int)cellFile[12] * 2));
                Y = (int)Math.Round(BitConverter.ToSingle(cellFile, 24 + (int)cellFile[12] * 2));
                Z = (int)Math.Round(BitConverter.ToSingle(cellFile, 28 + (int)cellFile[12] * 2));
                var rot = BitConverter.ToSingle(cellFile, 32 + (int)cellFile[12] * 2);

                Rot = rot;

                if (X % 10 != 0) {
                    Landcell = 0;
                    return;
                }
                
                if (rot == 1) {
                    R = (float)Math.PI;
                }
                else if (rot < -0.70 && rot > -0.8) {
                    R = (float)(Math.PI/2);
                }
                else if (rot > 0.70 && rot < 0.8) {
                    R = (float)-(Math.PI/2);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public string GetCoords() {
            return string.Format("{0},{1},{2}", X, Y, Z);
        }
    }
}
