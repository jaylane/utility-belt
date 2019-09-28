using Decal.Adapter.Wrappers;
using Decal.Filters;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.DungeonMaps {
    public class LandBlock {
        public const int CELL_SIZE = 10;
        public Color TRANSPARENT_COLOR = Color.White;
        public int LandBlockId;
        private List<int> checkedCells = new List<int>();
        private List<string> filledCoords = new List<string>();
        public Dictionary<int, List<DungeonCell>> zLayers = new Dictionary<int, List<DungeonCell>>();
        private Dictionary<int, DungeonCell> allCells = new Dictionary<int, DungeonCell>();
        public static List<int> portalIds = new List<int>();
        public Dictionary<int, List<Portal>> zPortals = new Dictionary<int, List<Portal>>();
        private bool? isDungeon;
        private float minX = 0;
        private float maxX = 0;
        private float minY = 0;
        private float maxY = 0;
        public int dungeonWidth = 0;
        public int dungeonHeight = 0;

        public Dictionary<int, Bitmap> bitmapLayers = new Dictionary<int, Bitmap>();

        public LandBlock(int landCell) {

            LandBlockId = landCell >> 16 << 16;

            LoadCells();

            dungeonWidth = (int)(Math.Abs(maxX) + Math.Abs(minX) + CELL_SIZE);
            dungeonHeight = (int)(Math.Abs(maxY) + Math.Abs(minY) + CELL_SIZE);
        }

        internal void LoadCells() {
            try {
                int cellCount;
                FileService service = Globals.Core.Filter<FileService>();

                try {
                    cellCount = BitConverter.ToInt32(service.GetCellFile(65534 + LandBlockId), 4);
                }
                catch (Exception ex) {
                    return;
                }

                for (uint index = 0; (long)index < (long)cellCount; ++index) {
                    int num = ((int)index + LandBlockId + 256);
                    var cell = new DungeonCell(num);
                    if (cell.CellId != 0 && !this.filledCoords.Contains(cell.GetCoords())) {
                        this.filledCoords.Add(cell.GetCoords());
                        int roundedZ = (int)Math.Round(cell.Z);
                        if (!zLayers.ContainsKey(roundedZ)) {
                            zLayers.Add(roundedZ, new List<DungeonCell>());
                        }

                        if (cell.X < minX) minX = cell.X;
                        if (cell.X > maxX) maxX = cell.X;
                        if (cell.Y < minY) minY = cell.Y;
                        if (cell.Y > maxY) maxY = cell.Y;

                        allCells.Add(num, cell);

                        zLayers[roundedZ].Add(cell);
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public bool IsDungeon() {
            if ((uint)(Globals.Core.Actions.Landcell << 16 >> 16) < 0x0100) {
                return false;
            }

            if (isDungeon.HasValue) {
                return isDungeon.Value;
            }

            bool _hasCells = false;
            bool _hasOutdoorCells = false;

            if (zLayers.Count > 0) {
                foreach (var zKey in zLayers.Keys) {
                    foreach (var cell in zLayers[zKey]) {
                        // When this value is >= 0x0100 you are inside (either in a building or in a dungeon).
                        if ((uint)(cell.CellId << 16 >> 16) < 0x0100) {
                            _hasOutdoorCells = true;
                            break;
                        }
                        else {
                            _hasCells = true;
                        }
                    }

                    if (_hasOutdoorCells) break;
                }
            }

            isDungeon = _hasCells && !_hasOutdoorCells;

            return isDungeon.Value;
        }

        public List<DungeonCell> GetCurrentCells() {
            var cells = new List<DungeonCell>();
            foreach (var zKey in zLayers.Keys) {
                foreach (var cell in zLayers[zKey]) {
                    if (-Math.Round(cell.X / 10) == Math.Round(Globals.Core.Actions.LocationX / 10) && Math.Round(cell.Y / 10) == Math.Round(Globals.Core.Actions.LocationY / 10)) {
                        cells.Add(cell);
                    }
                }
            }

            return cells;
        }

        public void AddPortal(WorldObject portalObject) {
            if (portalIds.Contains(portalObject.Id)) return;

            var portal = new Portal(portalObject);
            var portalZ = (int)Math.Floor(portal.Z / 6) * 6;

            if (!zPortals.Keys.Contains(portalZ)) {
                zPortals.Add(portalZ, new List<Portal>());
            }

            portalIds.Add(portal.Id);
            zPortals[portalZ].Add(portal);
        }
    }
}
