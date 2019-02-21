using Decal.Adapter;
using Decal.Adapter.Wrappers;
using Decal.Filters;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using UtilityBelt;
using UtilityBelt.Tools;
using UtilityBelt.Views;

namespace UtilityBelt.Tools {
    public class DungeonCell {
        public int CellId;
        public ushort EnvironmentId;
        public float X;
        public float Y;
        public float Z;
        public RotateFlipType R = RotateFlipType.RotateNoneFlipNone;

        public DungeonCell(int landCell) {
            FileService service = Globals.Core.Filter<FileService>();
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

                switch (EnvironmentId) {
                    // 3 window bridge/tunnel thing e/w direction? (facility hub)
                    case 679:
                    case 672:
                        EnvironmentId = 671;
                        if (Math.Abs(rot) > 0.6 && Math.Abs(rot) < 0.8) {
                            rot = 1F;
                        }
                        else {
                            rot = 0.77F;
                        }
                        break;

                    // right inside town meeting halls
                    case 2:
                        EnvironmentId = 443;
                        break;

                    // right inside town meeting halls
                    case 331:
                        EnvironmentId = 411;
                        rot = 1;
                        break;
                }

                if (rot == 1) {
                    R = RotateFlipType.Rotate180FlipNone;
                }
                else if (rot < -0.70 && rot > -0.8) {
                    R = RotateFlipType.Rotate90FlipNone;
                }
                else if (rot > 0.70 && rot < 0.8) {
                    R = RotateFlipType.Rotate270FlipNone;
                }
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        public string GetCoords() {
            return string.Format("{0},{1},{2}", X, Y, Z);
        }
    }

    public class LandBlock {
        public const int CELL_SIZE = 10;
        public Color TRANSPARENT_COLOR = Color.White;
        public int LandBlockId;
        private List<int> checkedCells = new List<int>();
        private List<string> filledCoords = new List<string>();
        private Dictionary<int, List<DungeonCell>> zLayers = new Dictionary<int, List<DungeonCell>>();
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

            LoadAll();

            dungeonWidth = (int)(Math.Abs(maxX) + Math.Abs(minX) + CELL_SIZE);
            dungeonHeight = (int)(Math.Abs(maxY) + Math.Abs(minY) + CELL_SIZE);

            Util.WriteToChat("w:" + dungeonWidth + " h:" + dungeonHeight);

            DrawZLayers();
        }

        internal void LoadAll() {
            FileService service = Globals.Core.Filter<FileService>();
            int int32 = BitConverter.ToInt32(service.GetCellFile(65534 + LandBlockId), 4);
            for (uint index = 0; (long)index < (long)int32; ++index) {
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

                    zLayers[roundedZ].Add(cell);
                }
            }
        }

        public void DrawZLayers() {
            ImageAttributes attributes = new ImageAttributes();

            foreach (var zKey in zLayers.Keys) {
                bitmapLayers[zKey] = new Bitmap(dungeonWidth, dungeonHeight);
                bitmapLayers[zKey].MakeTransparent();

                Graphics g = Graphics.FromImage(bitmapLayers[zKey]);

                g.TranslateTransform((float)dungeonWidth - CELL_SIZE, (float)dungeonHeight - CELL_SIZE);
                foreach (DungeonCell cell in zLayers[zKey]) {
                    Bitmap rotated = new Bitmap(TileCache.Get(cell.EnvironmentId));

                    rotated.MakeTransparent(TRANSPARENT_COLOR);
                    rotated.RotateFlip(cell.R);

                    g.DrawImage(rotated, new Rectangle((int)Math.Round(cell.X), (int)Math.Round(cell.Y), rotated.Width, rotated.Height), 0, 0, rotated.Width, rotated.Height, GraphicsUnit.Pixel, attributes);
                    rotated.Dispose();
                }
                //g.TranslateTransform(-(float)dungeonWidth / 2, -(float)dungeonHeight / 2);
                g.Save();
                g.Dispose();
            }

            attributes.Dispose();
        }

        public bool IsDungeon() {
            if (isDungeon.HasValue) {
                return isDungeon.Value;
            }
            
            bool _hasCells = false;
            bool _hasOutdoorCells = false;
            
            if (zLayers.Count > 0) {
                foreach (var zKey in zLayers.Keys) {
                    foreach (var cell in zLayers[zKey]) {
                        _hasCells = true;
                        break;
                        // When this value is >= 0x0100 you are inside (either in a building or in a dungeon).
                        //if ((cell.CellId << 16) < 0x0100) {
                        //    _hasOutdoorCells = true;
                        //    break;
                        //}
                    }

                    if (_hasOutdoorCells) break;
                }
            }

            isDungeon = _hasCells && !_hasOutdoorCells;
            
            return isDungeon.Value;
        }
    }

    public static class LandBlockCache {
        private static Dictionary<int, LandBlock> cache = new Dictionary<int, LandBlock>();

        public static LandBlock Get(int cellId) {
            if (cache.ContainsKey(cellId >> 16 << 16)) return cache[cellId >> 16 << 16];

            var watch = System.Diagnostics.Stopwatch.StartNew();
            var block = new LandBlock(cellId);
            watch.Stop();

            Util.WriteToChat(string.Format("DungeonMaps: took {0}ms to cache LandBlock {1} (isDungeon? {2})", watch.ElapsedMilliseconds, (cellId >> 16).ToString("X"), block.IsDungeon()));

            cache.Add(block.LandBlockId, block);

            return Get(cellId);
        }
    }

    public static class TileCache {
        public static Dictionary<ushort, Bitmap> cache = new Dictionary<ushort, Bitmap>();
        
        public static Bitmap Get(ushort environmentId) {
            if (cache.ContainsKey(environmentId)) return cache[environmentId];

            Bitmap image;
            string assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string bitmapFile = Path.Combine(assemblyFolder, @"tiles\" + environmentId + @".bmp");

            if (File.Exists(bitmapFile)) {
                image = new Bitmap(bitmapFile);
            }
            else {
                image = new Bitmap(10, 10);
            }

            cache.Add(environmentId, image);

            return Get(environmentId);
        }
    }

    class DungeonMaps : IDisposable {
        private const int THINK_INTERVAL = 100;
        private const int DRAW_INTERVAL = 50;
        private SolidBrush PLAYER_BRUSH = new SolidBrush(Color.Red);
        private const int PLAYER_SIZE = 5;
        private Rectangle PLAYER_RECT = new Rectangle(-(PLAYER_SIZE / 2), -(PLAYER_SIZE / 2), PLAYER_SIZE, PLAYER_SIZE);
        private DateTime lastDrawTime = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        private bool disposed = false;
        private Hud hud = null;
        private Rectangle hudRect;
        private Bitmap drawBitmap;
        private int counter = 0;
        private float scale = 3;
        private Graphics drawGfx;

        public DungeonMaps() {
            //Draw();
            Util.WriteToChat(Globals.MapView.view.Width +"   " + Globals.MapView.view.Height);
            drawBitmap = new Bitmap(Globals.MapView.view.Width, Globals.MapView.view.Height);
            drawBitmap.MakeTransparent();

            drawGfx = Graphics.FromImage(drawBitmap);
        }

        public void Draw() {
            try {
                if (hudRect == null) {
                    hudRect = new Rectangle(Globals.MapView.view.Location.X, Globals.MapView.view.Location.Y,
                        Globals.MapView.view.Width, Globals.MapView.view.Height);
                }

                hudRect.Location = Globals.MapView.view.Location;

                hudRect.Height = Globals.MapView.view.Height;
                hudRect.Width = Globals.MapView.view.Width;

                if (hud != null && (hud.Region.Width != hudRect.Width || hud.Region.Height != hudRect.Height)) {
                    hud.Enabled = false;
                    hud.Clear();
                    hud.Dispose();
                    hud = null;
                    /*
                    drawBitmap.Dispose();
                    drawBitmap = null;
                    drawBitmap = new Bitmap(hudRect.Width, hudRect.Height);
                    drawBitmap.MakeTransparent();

                    drawGfx.Dispose();
                    drawGfx = null;
                    drawGfx = Graphics.FromImage(drawBitmap);
                    */
                }

                if (hud == null) {
                    hud = Globals.Core.RenderService.CreateHud(hudRect);
                }

                hud.Clear();

                hud.Fill(Color.Transparent);
                hud.BeginRender();

                try {
                    LandBlock currentBlock = LandBlockCache.Get(Globals.Core.Actions.Landcell);

                    DrawDungeon(currentBlock);
                }
                catch (Exception ex) { Util.LogException(ex); }
                finally {
                    hud.EndRender();
                    hud.Alpha = 200;
                    hud.Enabled = true;
                }
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        private void DrawDungeon(LandBlock currentBlock) {
            float xOffset = (float)Globals.Core.Actions.LocationX;
            float yOffset = -(float)Globals.Core.Actions.LocationY;

            drawGfx.SmoothingMode = SmoothingMode.HighSpeed;
            drawGfx.Clear(Color.Transparent);

            drawGfx.TranslateTransform((float)Globals.MapView.view.Width / 2, (float)Globals.MapView.view.Height / 2);
            drawGfx.RotateTransform(360 - (((float)Globals.Core.Actions.Heading + 180) % 360));
            drawGfx.ScaleTransform(scale, scale);
            drawGfx.TranslateTransform(xOffset, yOffset);
            foreach (var zLayer in currentBlock.bitmapLayers.Keys) {
                ImageAttributes attributes = new ImageAttributes();
                var bmp = currentBlock.bitmapLayers[zLayer];

                // floors above your char
                if (Globals.Core.Actions.LocationZ - zLayer < -3) {
                    // opacity
                    float b = 1.0F - (float)(Math.Abs(Globals.Core.Actions.LocationZ - zLayer) / 6) * 0.4F;
                    ColorMatrix matrix = new ColorMatrix();
                    matrix.Matrix33 = b;
                    attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                }
                // floor we are on
                else if (Math.Abs(Globals.Core.Actions.LocationZ - zLayer) < 3) {
                    
                }
                // floors below your char
                else {
                    // darken
                    float b = 1.0F - (float)(Math.Abs(Globals.Core.Actions.LocationZ - zLayer) / 6) * 0.4F;
                    ColorMatrix matrix = new ColorMatrix(new float[][]{
                            new float[] {b, 0, 0, 0, 0},
                            new float[] {0, b, 0, 0, 0},
                            new float[] {0, 0, b, 0, 0},
                            new float[] {0, 0, 0, 1, 0},
                            new float[] {0, 0, 0, 0, 1},
                        });
                    attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                }

                drawGfx.DrawImage(bmp,
                    new Rectangle(-bmp.Width + 5, -bmp.Height + 5, bmp.Width, bmp.Height),
                    0, 0, bmp.Width, bmp.Height,
                    GraphicsUnit.Pixel, attributes);

                attributes.Dispose();
            }
            drawGfx.TranslateTransform(-xOffset, -yOffset);
            drawGfx.ScaleTransform(1/scale, 1/scale);
            drawGfx.RotateTransform(-(360 - (((float)Globals.Core.Actions.Heading + 180) % 360)));
            drawGfx.FillRectangle(PLAYER_BRUSH, PLAYER_RECT);
            drawGfx.TranslateTransform(-(float)Globals.MapView.view.Width / 2, -(float)Globals.MapView.view.Height / 2);

            drawGfx.Save();
            hud.DrawImage(drawBitmap, new Rectangle(0, 0, Globals.MapView.view.Width, Globals.MapView.view.Height));
        }

        public void Think() {
            if (DateTime.UtcNow - lastDrawTime > TimeSpan.FromMilliseconds(DRAW_INTERVAL)) {
                lastDrawTime = DateTime.UtcNow;

                //var watch = System.Diagnostics.Stopwatch.StartNew();
                Draw();
                //watch.Stop();
                //if (counter % 50 == 0) {
                //    counter = 0;
                    //Util.WriteToChat(string.Format("DungeonMaps: took {0}ms to draw", watch.ElapsedMilliseconds));
                //}
                //++counter;
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    Globals.Core.RenderService.RemoveHud(hud);
                    hud.Dispose();
                    drawGfx.Dispose();
                    drawBitmap.Dispose();
                }
                disposed = true;
            }
        }
    }
}
