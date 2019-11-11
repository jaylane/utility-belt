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
    public partial class Dungeon {
        private const int PLAYER_SIZE = 4;
        private SolidBrush playerBrush = new SolidBrush(Color.Red);
        private SolidBrush portalBrush = new SolidBrush(Color.Purple);
        private Pen navPen = new Pen(new SolidBrush(Color.Magenta), 1);

        public const int CELL_SIZE = 10;
        public Color TRANSPARENT_COLOR = Color.White;
        public uint LandBlockId;
        public uint LandCellId;
        private List<int> checkedCells = new List<int>();
        private List<string> filledCoords = new List<string>();
        public Dictionary<int, List<DungeonCell>> zLayers = new Dictionary<int, List<DungeonCell>>();
        private Dictionary<int, DungeonCell> allCells = new Dictionary<int, DungeonCell>();
        public static List<int> portalIds = new List<int>();
        public Dictionary<int, List<Portal>> zPortals = new Dictionary<int, List<Portal>>();
        private float minX = 0;
        private float maxX = 0;
        private float minY = 0;
        private float maxY = 0;
        public int dungeonWidth = 0;
        public int dungeonHeight = 0;
        public List<uint> visitedTiles = new List<uint>();
        private List<string> drawNavLines = new List<string>();

        public Dungeon(uint landCell) {
            LandBlockId = landCell & 0xFFFF0000;
            LandCellId = landCell;

            if (!IsDungeon()) return;

            LoadCells();

            dungeonWidth = (int)(Math.Abs(maxX) + Math.Abs(minX) + CELL_SIZE);
            dungeonHeight = (int)(Math.Abs(maxY) + Math.Abs(minY) + CELL_SIZE);
        }

        internal void LoadCells() {
            try {
                int cellCount;
                FileService service = Globals.Core.Filter<FileService>();

                try {
                    cellCount = BitConverter.ToInt32(service.GetCellFile((int)(65534 + LandBlockId)), 4);
                }
                catch {
                    return;
                }

                for (uint index = 0; (long)index < (long)cellCount; ++index) {
                    int num = ((int)(index + LandBlockId + 256));
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

                // floors more than four levels above your char are not drawn
                if (Globals.Core.Actions.LocationZ - zLayer > 24) {
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

                // nav lines
                DrawNavLines(drawGfx, zLayer);

                DrawMarkers(drawGfx, zLayer, rotation);
            }

            drawGfx.TranslateTransform(-mapDrawOffsetX, -mapDrawOffsetY);
            drawGfx.ScaleTransform(1 / scale, 1 / scale);
            
            drawGfx.RotateTransform(-rotation);

            drawGfx.Restore(gs);
            attributes.Dispose();

            return drawBitmap;
        }

        private void DrawMarkers(Graphics drawGfx, int zLayer, int rotation) {
            foreach (var wo in Globals.Core.WorldFilter.GetLandscape()) {
                if (!ShouldDrawMarker(wo)) continue;

                var objPos = wo.Offset();
                var obj = PhysicsObject.FromId(wo.Id);
                if (obj != null) {
                    objPos = new Vector3Object(obj.Position.X, obj.Position.Y, obj.Position.Z);
                    obj = null;
                }

                // clamp objects to the floor
                var objZ = Math.Round(objPos.Z / 6) * 6;

                // only draw stuff on the current zlayer
                if (Math.Abs(objZ - zLayer) > 5) continue;

                DrawObjectClassMarker(wo, drawGfx, -(float)objPos.X, (float)objPos.Y, (float)objZ, rotation);
            }
        }

        private bool ShouldDrawMarker(WorldObject wo) {
            // make sure the client knows about this object
            if (!Globals.Core.Actions.IsValidObject(wo.Id)) return false;

            // too far?
            if (Globals.Core.WorldFilter.Distance(wo.Id, Globals.Core.CharacterFilter.Id) * 240 > 300) return false;

            return Globals.Settings.DungeonMaps.Display.Markers.ShouldDraw(wo);
        }

        private void DrawObjectClassMarker(WorldObject wo, Graphics gfx, float x, float y, float z, int rotation) {
            try {
                var brush = new SolidBrush(Color.FromArgb(wo.Values((LongValueKey)95, Color.Pink.ToArgb())));

                ImageAttributes attributes = new ImageAttributes();
                ColorMatrix matrix = null;

                // floors directly above your character
                if (Globals.Core.Actions.LocationZ - z < -3) {
                    float b = 1.0F - (float)(Math.Abs(Globals.Core.Actions.LocationZ - z) / 6) * 0.5F;
                    brush.Color = Color.FromArgb((int)(b * 255), brush.Color.R, brush.Color.G, brush.Color.B);
                    matrix = new ColorMatrix(new float[][]{
                            new float[] {1, 0, 0, 0, 0},
                            new float[] {0, 1, 0, 0, 0},
                            new float[] {0, 0, 1, 0, 0},
                            new float[] {0, 0, 0, b, 0},
                            new float[] {0, 0, 0, 0, 1},
                        });
                }
                // current floor
                else if (Math.Abs(Globals.Core.Actions.LocationZ - z) < 3) {

                }
                // floors below
                else {
                    float b = 1.0F - (float)(Math.Abs(Globals.Core.Actions.LocationZ - z) / 6) * 0.4F;
                    var ca = (int)Math.Max(Math.Min((int)(b * 255), 255), 0);
                    brush.Color = Color.FromArgb(ca, brush.Color.R, brush.Color.G, brush.Color.B);
                    matrix = new ColorMatrix(new float[][]{
                            new float[] {b, 0, 0, 0, 0},
                            new float[] {0, b, 0, 0, 0},
                            new float[] {0, 0, b, 0, 0},
                            new float[] {0, 0, 0, 1, 0},
                            new float[] {0, 0, 0, 0, 1},
                        });
                }

                if (matrix != null) {
                    attributes.SetColorMatrix(matrix);
                }

                var color = Color.FromArgb(Globals.Settings.DungeonMaps.Display.Markers.GetMarkerColor(wo));
                var size = Globals.Settings.DungeonMaps.Display.Markers.GetSize(wo);
                var useIcon = Globals.Settings.DungeonMaps.Display.Markers.ShouldUseIcon(wo);
                var a = (int)Math.Min(Math.Max(color.A * (float)((float)brush.Color.A / 255f), 0), 255);

                brush.Color = Color.FromArgb(a, color.R, color.G, color.B);

                if (wo.ObjectClass == ObjectClass.Door && !useIcon) {
                    DrawDoor(wo, gfx, brush, x, y);
                    return;
                }
                else if (useIcon) {
                    var icon = IconCache.Get(wo.Icon);
                    // translate to keep the icon always facing up relative to the map window
                    gfx.TranslateTransform(x, y);
                    gfx.RotateTransform(-rotation);
                    gfx.DrawImage(icon, new Rectangle(-(size/2), -(size /2), size, size), 0, 0, 32, 32, GraphicsUnit.Pixel, attributes);
                    gfx.RotateTransform(rotation);
                    gfx.TranslateTransform(-x, -y);
                }
                else {
                    gfx.FillEllipse(brush, x - (size / 2), y - (size / 2), size, size);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void DrawDoor(WorldObject wo, Graphics gfx, SolidBrush brush, float x, float y) {
            var onYAxis = Math.Abs(wo.Orientation().W) > 0.6 && Math.Abs(wo.Orientation().W) < 0.8;

            // currently we are only drawing closed doors...
            if (!Globals.DoorWatcher.GetOpenStatus(wo.Id)) {
                if (onYAxis) {
                    gfx.FillRectangle(brush, x - 0.2f, y - 1.5f, 0.4f, 3);
                }
                else {
                    gfx.FillRectangle(brush, x - 1.5f, y - 0.2f, 3, 0.4f);
                }
            }
        }

        private void DrawPlayerMarker(Graphics gfx, int rotation) {
            if (!Globals.Settings.DungeonMaps.Display.Markers.You.Enabled) return;

            var playerXOffset = -(float)Globals.Core.Actions.LocationX;
            var playerYOffset = (float)Globals.Core.Actions.LocationY;
            var size = Globals.Settings.DungeonMaps.Display.Markers.You.Size;

            if (Globals.Settings.DungeonMaps.Display.Markers.You.UseIcon) {
                var icon = IconCache.Get(Globals.Core.WorldFilter[Globals.Core.CharacterFilter.Id].Icon);
                var rect = new RectangleF(-(size / 2), -(size / 2), size, size);

                // keep player icon always facing up relative to the map window
                gfx.TranslateTransform(playerXOffset, playerYOffset);
                gfx.RotateTransform(-rotation);
                gfx.DrawImage(icon, rect, new RectangleF(0, 0, 32, 32), GraphicsUnit.Pixel);
                gfx.RotateTransform(rotation);
                gfx.TranslateTransform(-playerXOffset, -playerYOffset);
            }
            else {
                playerBrush.Color = Color.FromArgb(Globals.Settings.DungeonMaps.Display.Markers.You.Color);
                gfx.FillEllipse(playerBrush, playerXOffset - (size / 2), playerYOffset - (size / 2), size, size);
            }
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
                case VTNav.eNavType.Once:
                    DrawPointRoute(drawGfx, zLayer, route);
                    break;
            }
        }

        public void DrawPointRoute(Graphics drawGfx, int zLayer, VTNav.VTNavRoute route) {
            var allPoints = route.points.Where((p) => (p.Type == VTNav.eWaypointType.Point && p.index >= route.NavOffset)).ToArray();

            // todo: follow routes
            if (route.NavType == VTNav.eNavType.Target) return;

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
         
            for (var i = route.NavOffset; i < route.points.Count; i++) {
                var point = route.points[i];
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
                    // todo: this is still broken on ramps, some nav lines get drawn under the ramp tile
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
    }
}
