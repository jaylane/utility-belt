using Decal.Adapter.Wrappers;
using Decal.Filters;
using System;
using System.Collections.Generic;
using System.Drawing;
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

                switch (EnvironmentId) {
                    // 3 window bridge/tunnel thing e/w direction? (facility hub)
                    case 679:
                        EnvironmentId = 671;
                        R = 1;
                        break;

                    // 3 window bridge/tunnel thing n/s direction? (facility hub)
                    case 672:
                        EnvironmentId = 671;
                        R = 0.77F;
                        break;
                }
            }
            catch (Exception ex) { Util.LogException(ex); }
        }
    }

    public class LandBlock {
        public int LandBlockId;
        private Dictionary<int, DungeonCell> Cells = new Dictionary<int, DungeonCell>();
        private List<int> checkedCells = new List<int>();

        public LandBlock(int landCell) {

            LandBlockId = landCell >> 16 << 16;

            LoadAll();
        }

        internal void LoadAll() {
            FileService service = Globals.Core.Filter<FileService>();
            int int32 = BitConverter.ToInt32(service.GetCellFile(65534 + LandBlockId), 4);
            for (uint index = 0; (long)index < (long)int32; ++index) {
                int num = ((int)index + LandBlockId + 256);
                if (!this.Cells.ContainsKey(num))
                    this.Cells.Add(num, new DungeonCell(num));
            }
        }

        public bool IsDungeon() {
            return Cells.Count > 0;
        }

        public List<DungeonCell> GetCells() {
            var list = new List<DungeonCell>();

            foreach (var key in Cells.Keys) {
                //if (Cells[key].X % 10 == 0) {
                    list.Add(Cells[key]);
                //}
            }

            return list;
        }
    }

    public static class LandBlockCache {
        private static Dictionary<int, LandBlock> cache = new Dictionary<int, LandBlock>();

        public static LandBlock Get(int cellId) {
            var block = new LandBlock(cellId);
            if (cache.ContainsKey(block.LandBlockId)) return cache[block.LandBlockId];

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
                Util.WriteToChat("found: " + bitmapFile);
            }
            else {
                image = new Bitmap(16, 16);
                Util.WriteToChat("not found: " + bitmapFile);
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
        private Bitmap drawBitmap = new Bitmap(100,100);

        public DungeonMaps() {
            Draw();
        }

        public void Draw() {
            try {
                /*
                if (Globals.View.view.Visible != true) {
                    if (hud != null) {
                        hud.Clear();
                        hud.Enabled = false;
                    }
                    return;
                }
                */

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
            var playerZ = Globals.Core.WorldFilter[Globals.Core.CharacterFilter.Id].RawCoordinates().Z;

            var c = Globals.Core.WorldFilter[Globals.Core.CharacterFilter.Id].RawCoordinates();

            if (drawBitmap == null) drawBitmap.Dispose();
            drawBitmap = new Bitmap(Globals.View.view.TotalSize.Width, Globals.View.view.TotalSize.Height);

            drawBitmap.MakeTransparent();

            Graphics g = Graphics.FromImage(drawBitmap);
            // center
            g.TranslateTransform(200,200);
            g.TranslateTransform((float)c.X, (float)c.Y);
            g.RotateTransform(360 - (float)Globals.Core.Actions.Heading);

            //g.RotateTransform(360 - (float)Globals.Core.Actions.Heading);
            //g.TranslateTransform(-(float)drawBitmap.Width / 2, -(float)drawBitmap.Height / 2);

            foreach (var cell in currentBlock.GetCells()) {
                if (cell.Z > playerZ) continue;

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

                g.DrawImage(rotated, (cell.X * -1)- (float)(c.X*-1), (float)c.Y - cell.Y, 10, 10);
                //g.DrawString(cell.EnvironmentId.ToString(), new Font("Arial", 5), new SolidBrush(Color.Red), cell.X*-1, cell.Y);
                //Util.WriteToChat(string.Format("Draw: {0} {1} {2} {3}", cell.CellId, cell.EnvironmentId, cell.X, cell.Y));

                //g.DrawImage()

                rotated.Dispose();
            }

            g.FillRectangle(new SolidBrush(Color.Red), new Rectangle(2,2, 4,4));



            //g.ScaleTransform(10, 10);
            g.Save();

            hud.DrawImage(drawBitmap, new Rectangle(0, 0, Globals.View.view.TotalSize.Width, Globals.View.view.TotalSize.Height));
        }

        public void Think() {
            Draw();
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
