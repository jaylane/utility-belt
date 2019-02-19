using Decal.Adapter;
using Decal.Adapter.Wrappers;
using Decal.Filters;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
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
        public float R;

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
                R = BitConverter.ToSingle(cellFile, 32 + (int)cellFile[12] * 2);

                if (X % 10 != 0) {
                    CellId = 0;
                    return;
                }

                switch (EnvironmentId) {
                    // 3 window bridge/tunnel thing e/w direction? (facility hub)
                    case 679:
                    case 672:
                        EnvironmentId = 671;
                        if (Math.Abs(R) > 0.6 && Math.Abs(R) < 0.8) {
                            R = 1F;
                        }
                        else {
                            R = 0.77F;
                        }
                        break;
                }
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        public string GetCoords() {
            return string.Format("{0},{1},{2}", X, Y, Z);
        }
    }

    public class LandBlock {
        public int LandBlockId;
        private Dictionary<int, DungeonCell> Cells = new Dictionary<int, DungeonCell>();
        private List<int> checkedCells = new List<int>();
        private List<string> filledCoords = new List<string>();

        public LandBlock(int landCell) {

            LandBlockId = landCell >> 16 << 16;

            LoadAll();
        }

        internal void LoadAll() {
            FileService service = Globals.Core.Filter<FileService>();
            int int32 = BitConverter.ToInt32(service.GetCellFile(65534 + LandBlockId), 4);
            for (uint index = 0; (long)index < (long)int32; ++index) {
                int num = ((int)index + LandBlockId + 256);
                if (!this.Cells.ContainsKey(num)) {
                    var cell = new DungeonCell(num);
                    if (!this.filledCoords.Contains(cell.GetCoords())) {
                        this.filledCoords.Add(cell.GetCoords());
                        this.Cells.Add(num, cell);
                    }
                }
            }
        }

        public bool IsDungeon() {
            return Cells.Count > 0;
        }

        public DungeonCell GetCurrentCell() {
            if (Cells.ContainsKey(Globals.Core.Actions.Landcell)) {
                return Cells[Globals.Core.Actions.Landcell];
            }

            return null;
        }

        public List<DungeonCell> GetCells() {
            var list = new List<DungeonCell>();

            foreach (var key in Cells.Keys) {
                list.Add(Cells[key]);
            }

            return list;
        }
    }

    public static class LandBlockCache {
        private static Dictionary<int, LandBlock> cache = new Dictionary<int, LandBlock>();

        public static LandBlock Get(int cellId) {
            if (cache.ContainsKey(cellId >> 16 << 16)) return cache[cellId >> 16 << 16];
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var block = new LandBlock(cellId);
            watch.Stop();
            Util.WriteToChat(string.Format("took {0}ms to cache {1}", watch.ElapsedMilliseconds, (cellId >> 16 << 16).ToString("X")));

            cache.Add(block.LandBlockId, block);

            return Get(cellId);
        }

        public static int Count() {
            return cache.Count;
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
                image = new Bitmap(16, 16);
            }

            cache.Add(environmentId, image);

            return Get(environmentId);
        }
    }

    class DungeonMaps : IDisposable {
        private const int THINK_INTERVAL = 100;
        private const int DRAW_INTERVAL = 100;
        private DateTime lastThought = DateTime.MinValue;
        private bool disposed = false;
        private Hud hud = null;
        private Rectangle hudRect;
        private Bitmap drawBitmap;
        private int counter = 0;
        private float scale = 3;

        public DungeonMaps() {
            Draw();
        }

        public void Draw() {
            try {
                if (hudRect == null) {
                    hudRect = new Rectangle(Globals.View.view.Location, Globals.View.view.TotalSize);
                }

                hudRect.Location = Globals.View.view.Location;
                hudRect.Size = Globals.View.view.TotalSize;

                if (hud == null) {
                    hud = Globals.Core.RenderService.CreateHud(hudRect);
                }

                hud.Region = hudRect;

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
            if (drawBitmap != null) drawBitmap.Dispose();
            drawBitmap = new Bitmap(Globals.View.view.TotalSize.Width, Globals.View.view.TotalSize.Height);

            drawBitmap.MakeTransparent();

            Graphics g = Graphics.FromImage(drawBitmap);
            //g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.TranslateTransform((float)drawBitmap.Width / 2, (float)drawBitmap.Height / 2);
            g.RotateTransform(360 - (((float)Globals.Core.Actions.Heading + 180) % 360));
            g.ScaleTransform(scale, scale);

            g.TranslateTransform((float)Globals.Core.Actions.LocationX, -(float)Globals.Core.Actions.LocationY);

            foreach (var cell in currentBlock.GetCells()) {

                var rotated = new Bitmap(TileCache.Get(cell.EnvironmentId));

                rotated.MakeTransparent(Color.White);

                if (cell.R == 1) {
                    rotated.RotateFlip(RotateFlipType.Rotate180FlipNone);
                }
                else if (cell.R < -0.70 && cell.R > -0.8) {
                    rotated.RotateFlip(RotateFlipType.Rotate90FlipNone);
                }
                else if (cell.R < 0.10 && cell.R > -0.1) {
                    rotated.RotateFlip(RotateFlipType.RotateNoneFlipNone);
                }
                else if (cell.R > 0.70 && cell.R < 0.8) {
                    rotated.RotateFlip(RotateFlipType.Rotate270FlipNone);
                }

                // floors above your char
                if (Globals.Core.Actions.LocationZ - cell.Z < -3) {
                    float b = 1.0F - (float)(Math.Abs(Globals.Core.Actions.LocationZ - cell.Z) / 6) * 0.4F;
                    ColorMatrix matrix = new ColorMatrix();
                    // opacity
                    matrix.Matrix33 = b;
                    ImageAttributes attributes = new ImageAttributes();
                    attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                    g.DrawImage(rotated, new Rectangle((int)Math.Round(cell.X), (int)Math.Round(cell.Y), rotated.Width, rotated.Height), 0, 0, rotated.Width-1, rotated.Height-1, GraphicsUnit.Pixel, attributes);
                }
                else if (Math.Abs(Globals.Core.Actions.LocationZ - cell.Z) < 3) {
                    ImageAttributes attributes = new ImageAttributes();
                    g.DrawImage(rotated, new Rectangle((int)Math.Round(cell.X), (int)Math.Round(cell.Y), rotated.Width, rotated.Height), 0, 0, rotated.Width - 1, rotated.Height - 1, GraphicsUnit.Pixel, attributes);

                }
                else {
                    float b = 1.0F - (float)(Math.Abs(Globals.Core.Actions.LocationZ - cell.Z) / 6) * 0.4F;
                    ColorMatrix matrix = new ColorMatrix(new float[][]{
                            new float[] {b, 0, 0, 0, 0},
                            new float[] {0, b, 0, 0, 0},
                            new float[] {0, 0, b, 0, 0},
                            new float[] {0, 0, 0, 1, 0},
                            new float[] {0, 0, 0, 0, 1},
                        });
                    ImageAttributes attributes = new ImageAttributes();
                    attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                    g.DrawImage(rotated, new Rectangle((int)Math.Round(cell.X), (int)Math.Round(cell.Y), rotated.Width, rotated.Height), 0, 0, rotated.Width-1, rotated.Height-1, GraphicsUnit.Pixel, attributes);
                }

                rotated.Dispose();
            }

            int playerSize = 2;
            int pY = (int)Math.Round(Globals.Core.Actions.LocationY + (playerSize*2));
            int pX = -(int)Math.Round(Globals.Core.Actions.LocationX - (playerSize*2));
            g.FillRectangle(new SolidBrush(Color.Red), new Rectangle(pX, pY, playerSize, playerSize));

            g.Save();
            hud.DrawImage(drawBitmap, new Rectangle(0, 0, Globals.View.view.TotalSize.Width, Globals.View.view.TotalSize.Height));
        }

        public void Think() {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Draw();
            watch.Stop();
            if (counter % 60 == 0) {
                Util.WriteToChat(string.Format("DungeonMaps: took {0}ms to draw", watch.ElapsedMilliseconds));
            }
            ++counter;

            if (DateTime.UtcNow - lastThought > TimeSpan.FromMilliseconds(THINK_INTERVAL)) {
                lastThought = DateTime.UtcNow;
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
                }
                disposed = true;
            }
        }
    }
}
