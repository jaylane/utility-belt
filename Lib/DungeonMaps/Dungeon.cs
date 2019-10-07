using Decal.Adapter.Wrappers;
using Decal.Filters;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using UtilityBelt.Lib.VTNav.Waypoints;

namespace UtilityBelt.Lib.DungeonMaps {
    public class Dungeon {
        private const int PLAYER_SIZE = 4;
        private SolidBrush playerBrush = new SolidBrush(Color.Red);
        private SolidBrush portalBrush = new SolidBrush(Color.Purple);
        private Pen navPen = new Pen(new SolidBrush(Color.Magenta), 1);

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
        public List<uint> visitedTiles = new List<uint>();
        private List<string> drawNavLines = new List<string>();

        public Dungeon(int landCell) {
            LandBlockId = landCell >> 16 << 16;

            Util.WriteToChat(LandBlockId.ToString("X8"));

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
                catch {
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

        public int drawCount = 0;

        public Bitmap Draw(float centerX, float centerY, float scale, int rotation, Rectangle r) {
            Bitmap drawBitmap;
            drawBitmap = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppPArgb);
            drawBitmap.MakeTransparent();

            var drawGfx = Graphics.FromImage(drawBitmap);

            float mapDrawOffsetX = centerX;
            float mapDrawOffsetY = centerY;
            int offsetX = (int)(drawBitmap.Width / 2);
            int offsetY = (int)(drawBitmap.Height / 2);
            
            drawGfx.SmoothingMode = SmoothingMode.None;
            drawGfx.InterpolationMode = InterpolationMode.Low;
            drawGfx.CompositingQuality = CompositingQuality.HighSpeed;
            //drawGfx.CompositingMode = CompositingMode.SourceCopy;
            drawGfx.Clear(Color.Transparent);
            GraphicsState gs = drawGfx.Save();

            drawGfx.TranslateTransform(offsetX, offsetY);
            drawGfx.RotateTransform(rotation);
            drawGfx.ScaleTransform(scale, scale);
            drawGfx.TranslateTransform(mapDrawOffsetX, mapDrawOffsetY);
            var portals = Globals.Core.WorldFilter.GetByObjectClass(ObjectClass.Portal);

            ColorMatrix matrix = null;
            ColorMatrix visitedMatrix = new ColorMatrix(new float[][] {
                            new float[] {.3f, .3f, .3f, 0, 0},
                            new float[] {.59f, .59f, .59f, 0, 0},
                            new float[] {.11f, .11f, .11f, 0, 0},
                            new float[] {0, 0, 0, 1, 0},
                            new float[] {0, 0, 0, 0, 1}
                        });

            drawNavLines.Clear();

            ImageAttributes attributes = new ImageAttributes();

            drawCount = 0;
            foreach (var zLayer in zLayers.Keys) {
                // floors more than one level above your char are not drawn
                if (Globals.Core.Actions.LocationZ - zLayer < -10) {
                    continue;
                }

                foreach (var cell in zLayers[zLayer]) {
                    // make sure this cell is in view on the map window
                    if (!CellIsInView(cell, r, centerX, centerY, scale)) continue;

                    var rotated = TileCache.Get(cell.EnvironmentId);
                    var x = (int)Math.Round(cell.X);
                    var y = (int)Math.Round(cell.Y);
                    if (rotated == null) continue;

                    GraphicsState gs1 = drawGfx.Save();
                    drawGfx.TranslateTransform(x, y);
                    drawGfx.RotateTransform(cell.R);
                    var isVisited = false;

                    //visited cells
                    if (Globals.Settings.DungeonMaps.ShowVisitedTiles && visitedTiles.Contains((uint)(cell.CellId << 16 >> 16))) {
                        isVisited = true;
                    }
                    // floors directly above your character
                    if (Globals.Core.Actions.LocationZ - cell.Z < -3) {
                        float b = 1.0F - (float)(Math.Abs(Globals.Core.Actions.LocationZ - cell.Z) / 6) * 0.5F;
                        matrix = new ColorMatrix(new float[][]{
                            new float[] {1, 0, 0, 0, 0},
                            new float[] {0, 1, 0, 0, 0},
                            new float[] {0, 0, 1, 0, 0},
                            new float[] {0, 0, 0, b, 0},
                            new float[] {0, 0, 0, 0, 1},
                        });
                    }
                    // current floor
                    else if (Math.Abs(Globals.Core.Actions.LocationZ - cell.Z) < 3) {
                        float b = 1.0F;
                        matrix = new ColorMatrix(new float[][]{
                            new float[] {b, 0, 0, 0, 0},
                            new float[] {0, b, 0, 0, 0},
                            new float[] {0, 0, b, 0, 0},
                            new float[] {0, 0, 0, 1, 0},
                            new float[] {0, 0, 0, 0, 1},
                        });
                    }
                    // floors below
                    else {
                        float b = 1.0F - (float)(Math.Abs(Globals.Core.Actions.LocationZ - cell.Z) / 6) * 0.4F;
                        matrix = new ColorMatrix(new float[][]{
                            new float[] {b, 0, 0, 0, 0},
                            new float[] {0, b, 0, 0, 0},
                            new float[] {0, 0, b, 0, 0},
                            new float[] {0, 0, 0, 1, 0},
                            new float[] {0, 0, 0, 0, 1},
                        });
                    }

                    // only draw isVisited tiles on the current floor..
                    if (isVisited && Math.Abs(Globals.Core.Actions.LocationZ - cell.Z) < 3) {
                        using (Bitmap rotatedVisited = new Bitmap(rotated)) {
                            using (var rotatedGfx = Graphics.FromImage(rotatedVisited)) {
                                attributes.SetColorMatrix(visitedMatrix);
                                rotatedGfx.DrawImage(rotated, new Rectangle(0, 0, rotated.Width, rotated.Height), 0, 0, rotated.Width, rotated.Height, GraphicsUnit.Pixel, attributes);
                                if (matrix != null) {
                                    attributes.SetColorMatrix(matrix);
                                }
                                drawGfx.DrawImage(rotatedVisited, new Rectangle(-5, -5, rotatedVisited.Width + 1, rotatedVisited.Height + 1), 0, 0, rotatedVisited.Width, rotatedVisited.Height, GraphicsUnit.Pixel, attributes);
                                drawCount++;
                            }
                        }
                    }
                    // non visited tiles
                    else {
                        if (matrix != null) {
                            attributes.SetColorMatrix(matrix);
                        }
                        drawGfx.DrawImage(rotated, new Rectangle(-5, -5, rotated.Width + 1, rotated.Height + 1), 0, 0, rotated.Width, rotated.Height, GraphicsUnit.Pixel, attributes);
                        drawCount++;
                    }

                    drawGfx.RotateTransform(-cell.R);
                    drawGfx.TranslateTransform(-x, -y);
                    drawGfx.Restore(gs1);
                }

                // portal dots
                DrawPortalDots(drawGfx, zLayer);

                // nav lines
                DrawNavLines(drawGfx, zLayer);
            }

            DrawPlayerMarker(drawGfx);

            drawGfx.TranslateTransform(-mapDrawOffsetX, -mapDrawOffsetY);
            drawGfx.ScaleTransform(1 / scale, 1 / scale);
            
            drawGfx.RotateTransform(-rotation);

            drawGfx.Restore(gs);
            attributes.Dispose();

            return drawBitmap;
        }

        private void DrawPortalDots(Graphics drawGfx, int zLayer) {
            if (!Globals.Settings.DungeonMaps.Display.Portals.Enabled) return;

            var opacity = Math.Max(Math.Min(1 - (Math.Abs(Globals.Core.Actions.LocationZ - zLayer) / 6) * 0.4F, 1), 0) * 255;
            portalBrush.Color = Color.FromArgb(Globals.Settings.DungeonMaps.Display.Portals.Color);

            // draw portal markers
            if (zPortals.ContainsKey(zLayer)) {
                foreach (var portal in zPortals[zLayer]) {
                    drawGfx.FillEllipse(portalBrush, -(float)(portal.X + 1), (float)(portal.Y - 1), 2F, 2F);
                }
            }
        }

        private void DrawPlayerMarker(Graphics drawGfx) {
            if (!Globals.Settings.DungeonMaps.Display.Player.Enabled) return;

            var playerXOffset = -(float)Globals.Core.Actions.LocationX;
            var playerYOffset = (float)Globals.Core.Actions.LocationY;

            playerBrush.Color = Color.FromArgb(Globals.Settings.DungeonMaps.Display.Player.Color);
            drawGfx.FillEllipse(playerBrush, playerXOffset - 1, playerYOffset - 1, 2f, 2f);
        }

        private bool CellIsInView(DungeonCell cell, Rectangle window, float centerX, float centerY, float scale) {
            var drawDistance = (Math.Max(window.Width, window.Height) / 2) + 50;
            var cellDistance = Math.Abs(Geometry.Distance2d(centerX, centerY, -cell.X, -cell.Y)) * scale;
            return cellDistance < drawDistance;
        }

        private void DrawNavLines(Graphics drawGfx, int zLayer) {
            var route = Globals.VisualVTankRoutes.currentRoute;
            if (route == null) return;

            switch (route.NavType) {
                case VTNav.eNavType.Circular:
                case VTNav.eNavType.Linear:
                    DrawPointRoute(drawGfx, zLayer, route);
                    break;
            }
        }

        public void DrawPointRoute(Graphics drawGfx, int zLayer, VTNav.VTNavRoute route) {
            var allPoints = route.points.Where((p) => p.Type == VTNav.eWaypointType.Point).ToArray();

            // sticky point
            if (allPoints.Length == 1 && route.NavType == VTNav.eNavType.Circular) {
                if (!Globals.Settings.DungeonMaps.Display.VisualNavStickyPoint.Enabled) return;

                VTNPoint point = allPoints[0];
                var landblock = Geometry.GetLandblockFromCoordinates((float)point.EW, (float)point.NS);
                var pointOffset = Geometry.LandblockOffsetFromCoordinates(LandBlockId, (float)point.EW, (float)point.NS);

                navPen.Color = Color.FromArgb(Globals.Settings.DungeonMaps.Display.VisualNavStickyPoint.Color);
                drawGfx.DrawEllipse(navPen, -pointOffset.X - 1.5f, pointOffset.Y - 1.5f, 3f, 3f);
                return;
            }

            // circular / once / linear routes.. currently not discriminating
            if (!Globals.Settings.DungeonMaps.Display.VisualNavLines.Enabled) return;
         
            foreach (var point in route.points) {
                var prev = point.GetPreviousPoint();
                if (prev == null) continue;

                // we use this to make sure we only draw a line once
                var lineKey = $"{prev.NS},{prev.EW},{prev.Z},{point.NS},{point.EW},{point.Z}";
                if (drawNavLines.Contains(lineKey)) continue;

                if (point.Type == VTNav.eWaypointType.Point) {
                    var landblock = Geometry.GetLandblockFromCoordinates((float)point.EW, (float)point.NS);
                    var pointOffset = Geometry.LandblockOffsetFromCoordinates(LandBlockId, (float)point.EW, (float)point.NS);
                    var prevOffset = Geometry.LandblockOffsetFromCoordinates(LandBlockId, (float)prev.EW, (float)prev.NS);

                    // we pump these up a bit so they get drawn preferentially on a higher layer
                    var prevZ = (prev.Z * 240) + 3f;
                    var pointZ = (point.Z * 240) + 3f;

                    bool prevIsOnActiveLayer = Math.Abs((prevZ - zLayer)) < 6;
                    bool pointIsOnActiveLayer = Math.Abs(pointZ - zLayer) < 6;

                    // skip if neither of the points fall on this layer
                    if (!prevIsOnActiveLayer && !pointIsOnActiveLayer) continue;

                    // if prevZ is lower than pointZ, and pointZ is *not* the current level,
                    // wait to draw this line on the next layer,
                    // otherwise the cell tile bitmap will overlay it
                    if (prevZ < pointZ && !pointIsOnActiveLayer) continue;
                    if (prevZ > pointZ && !prevIsOnActiveLayer) continue;

                    navPen.Color = Color.FromArgb(Globals.Settings.DungeonMaps.Display.VisualNavLines.Color);

                    drawGfx.DrawLine(navPen, -prevOffset.X, prevOffset.Y, -pointOffset.X, pointOffset.Y);

                    drawNavLines.Add(lineKey);
                }
            }
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
