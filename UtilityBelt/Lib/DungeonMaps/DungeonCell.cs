using Decal.Adapter;
using Decal.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.DungeonMaps {
    public class DungeonCell {
        public int CellId;
        public ushort EnvironmentId;
        public float X;
        public float Y;
        public float Z;
        public int R = 0;

        public DungeonCell(int landCell) {
            FileService service = CoreManager.Current.Filter<FileService>();
            byte[] cellFile = service.GetCellFile(landCell);

            try {
                if (cellFile == null) {
                    CellId = 0;
                    return;
                }

                CellId = landCell;
                EnvironmentId = BitConverter.ToUInt16(cellFile, 16 + (int)cellFile[12] * 2);
                X = BitConverter.ToSingle(cellFile, 20 + (int)cellFile[12] * 2) * -1;
                Y = BitConverter.ToSingle(cellFile, 24 + (int)cellFile[12] * 2);
                Z = BitConverter.ToSingle(cellFile, 28 + (int)cellFile[12] * 2);
                var rot = BitConverter.ToSingle(cellFile, 32 + (int)cellFile[12] * 2);

                if (X % 10 != 0) {
                    CellId = 0;
                    return;
                }

                if (rot == 1) {
                    R = 180;
                }
                else if (rot < -0.70 && rot > -0.8) {
                    R = 90;
                }
                else if (rot > 0.70 && rot < 0.8) {
                    R = 270;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public string GetCoords() {
            return string.Format("{0},{1},{2}", X, Y, Z);
        }
    }
}
