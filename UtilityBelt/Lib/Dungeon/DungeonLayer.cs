using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VirindiViewService;

namespace UtilityBelt.Lib.Dungeon {
    public class DungeonLayer {
        public List<DungeonCell> Cells = new List<DungeonCell>();
        public int Width { get { return maxX - minX + 10; } }
        public int Height { get { return maxY - minY + 10; } }

        public int roundedZ;
        public int minX = 0;
        public int maxX = 0;
        public int minY = 0;
        public int maxY = 0;
        private Dungeon dungeon;
        private int z;

        public int OffsetX { get { return (dungeon.Width - Width) - (minX - dungeon.minX); } }
        public int OffsetY { get { return minY - dungeon.minY; } }

        public DungeonLayer(Dungeon dungeon, int z) {
            this.dungeon = dungeon;
            this.z = z;
            this.roundedZ = (int)(Math.Floor((z + 3f) / 6f) * 6f);
        }

        internal void AddCell(DungeonCell cell) {
            if (Cells.Count == 0) {
                minX = cell.X;
                maxX = cell.X;
                minY = cell.Y;
                maxY = cell.Y;
            }
            else {
                if (cell.X < minX) minX = cell.X;
                if (cell.X > maxX) maxX = cell.X;
                if (cell.Y < minY) minY = cell.Y;
                if (cell.Y > maxY) maxY = cell.Y;
            }

            Cells.Add(cell);
        }
    }
}
