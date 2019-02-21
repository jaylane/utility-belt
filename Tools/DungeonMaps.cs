using Decal.Adapter;
using Decal.Adapter.Wrappers;
using Decal.Filters;
using Mag.Shared.Settings;
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
using VirindiViewService.Controls;

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

                    // diagonal hallway
                    //case 402:
                    //    EnvironmentId = 238;
                    //    break;
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
        private Dictionary<int, DungeonCell> allCells = new Dictionary<int, DungeonCell>();
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

            DrawZLayers();
        }

        internal void LoadCells() {
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

                    allCells.Add(num, cell);

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

        public static void Clear() {
            cache.Clear();
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
                using (Bitmap bmp = new Bitmap(bitmapFile)) {
                    image = new Bitmap(bmp);
                }
            }
            else {
                image = new Bitmap(10, 10);
            }

            cache.Add(environmentId, image);

            return Get(environmentId);
        }

        public static void Clear() {
            foreach (var key in cache.Keys) {
                cache[key].Dispose();
            }

            cache.Clear();
        }
    }

    class DungeonMaps : IDisposable {
        private const int THINK_INTERVAL = 100;
        private const int DRAW_INTERVAL = 50;
        private SolidBrush PLAYER_BRUSH = new SolidBrush(Color.Red);
        private SolidBrush TEXT_BRUSH = new SolidBrush(Color.White);
        private Font DEFAULT_FONT = new Font("Mono", 8);
        private const int PLAYER_SIZE = 5;
        private Rectangle PLAYER_RECT = new Rectangle(-(PLAYER_SIZE / 2), -(PLAYER_SIZE / 2), PLAYER_SIZE, PLAYER_SIZE);
        private DateTime lastDrawTime = DateTime.UtcNow;
        private bool disposed = false;
        private Hud hud = null;
        private Rectangle hudRect;
        private Bitmap drawBitmap;
        private int counter = 0;
        private float scale = 1;
        private int rawScale = 9;
        private  int MIN_SCALE = 0;
        private  int MAX_SCALE = 16;
        private Graphics drawGfx;

        HudCheckBox UIDungeonMapsEnabled { get; set; }
        HudCheckBox UIDungeonMapsDebug { get; set; }
        HudCheckBox UIDungeonMapsDrawWhenClosed { get; set; }
        HudHSlider UIDungeonMapsOpacity { get; set; }
        HudButton UIDungeonMapsClearTileCache { get; set; }

        public DungeonMaps() {
            drawBitmap = new Bitmap(Globals.MapView.view.Width, Globals.MapView.view.Height);
            drawBitmap.MakeTransparent();

            drawGfx = Graphics.FromImage(drawBitmap);
            scale = 8.4F - Map(rawScale, MIN_SCALE, MAX_SCALE, 0.4F, 8);

            Globals.MapView.view["DungeonMapsRenderContainer"].MouseEvent += DungeonMaps_MouseEvent;

            UIDungeonMapsClearTileCache = (HudButton)Globals.MainView.view["DungeonMapsClearTileCache"];
            UIDungeonMapsClearTileCache.Hit += UIDungeonMapsClearTileCache_Hit;

            UIDungeonMapsEnabled = (HudCheckBox)Globals.MainView.view["DungeonMapsEnabled"];
            UIDungeonMapsEnabled.Checked = Globals.Config.DungeonMaps.Enabled.Value;
            UIDungeonMapsEnabled.Change += UIDungeonMapsEnabled_Change;
            Globals.Config.DungeonMaps.Enabled.Changed += Config_DungeonMaps_Enabled_Changed;

            UIDungeonMapsDebug = (HudCheckBox)Globals.MainView.view["DungeonMapsDebug"];
            UIDungeonMapsDebug.Checked = Globals.Config.DungeonMaps.Enabled.Value;
            UIDungeonMapsDebug.Change += UIDungeonMapsDebug_Change;
            Globals.Config.DungeonMaps.Debug.Changed += Config_DungeonMaps_Debug_Changed;

            UIDungeonMapsDrawWhenClosed = (HudCheckBox)Globals.MainView.view["DungeonMapsDrawWhenClosed"];
            UIDungeonMapsDrawWhenClosed.Checked = Globals.Config.DungeonMaps.DrawWhenClosed.Value;
            UIDungeonMapsDrawWhenClosed.Change += UIDungeonMapsDrawWhenClosed_Change;
            Globals.Config.DungeonMaps.DrawWhenClosed.Changed += Config_DungeonMaps_DrawWhenClosed_Changed;

            UIDungeonMapsOpacity = (HudHSlider)Globals.MainView.view["DungeonMapsOpacity"];
            UIDungeonMapsOpacity.Position = Globals.Config.DungeonMaps.Opacity.Value;
            UIDungeonMapsOpacity.Changed += UIDungeonMapsOpacity_Changed;
            Globals.Config.DungeonMaps.Opacity.Changed += Config_DungeonMaps_Opacity_Changed;
        }

        private void Toggle() {
            try {
                var enabled = Globals.Config.DungeonMaps.Enabled.Value;
                Globals.MapView.view.ShowInBar = enabled;

                if (!enabled) {
                    if (Globals.MapView.view.Visible) {
                        Globals.MapView.view.Visible = false;
                    }
                    if (hud.Enabled) {
                        hud.Clear();
                        hud.Enabled = false;
                    }
                }
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        private void Config_DungeonMaps_Enabled_Changed(Setting<bool> obj) {
            try {
                UIDungeonMapsEnabled.Checked = Globals.Config.DungeonMaps.Enabled.Value;
                Toggle();
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        private void UIDungeonMapsEnabled_Change(object sender, EventArgs e) {
            try {
                Globals.Config.DungeonMaps.Enabled.Value = UIDungeonMapsEnabled.Checked;
                Toggle();
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        private void Config_DungeonMaps_Debug_Changed(Setting<bool> obj) {
            try {
                UIDungeonMapsDebug.Checked = Globals.Config.DungeonMaps.Debug.Value;
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        private void UIDungeonMapsDebug_Change(object sender, EventArgs e) {
            try {
                Globals.Config.DungeonMaps.Debug.Value = UIDungeonMapsDebug.Checked;
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        private void Config_DungeonMaps_DrawWhenClosed_Changed(Setting<bool> obj) {
            try {
                UIDungeonMapsDrawWhenClosed.Checked = Globals.Config.DungeonMaps.DrawWhenClosed.Value;
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        private void UIDungeonMapsDrawWhenClosed_Change(object sender, EventArgs e) {
            try {
                Globals.Config.DungeonMaps.DrawWhenClosed.Value = UIDungeonMapsDrawWhenClosed.Checked;
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        private void UIDungeonMapsOpacity_Changed(int min, int max, int pos) {
            if (pos != Globals.Config.DungeonMaps.Opacity.Value) {
                Globals.Config.DungeonMaps.Opacity.Value = pos;
            }
        }

        private void Config_DungeonMaps_Opacity_Changed(Setting<int> obj) {
            //UIDungeonMapsOpacity.Position = (Globals.Config.DungeonMaps.Opacity.Value / 100) - 300;
        }

        private void UIDungeonMapsClearTileCache_Hit(object sender, EventArgs e) {
            try {
                LandBlockCache.Clear();
                TileCache.Clear();
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        private void DungeonMaps_MouseEvent(object sender, VirindiViewService.Controls.ControlMouseEventArgs e) {
            try {
                switch (e.EventType) {
                    case VirindiViewService.Controls.ControlMouseEventArgs.MouseEventType.MouseWheel:
                        if ((e.WheelAmount < 0 && rawScale < MAX_SCALE) || (e.WheelAmount > 0 && rawScale > MIN_SCALE)) {
                            var s = e.WheelAmount < 0 ? ++rawScale : --rawScale;

                            scale = 8.4F - Map(s, MIN_SCALE, MAX_SCALE, 0.4F, 8);
                        }
                        break;
                }
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        public float Map(float value, float fromSource, float toSource, float fromTarget, float toTarget) {
            return (value - fromSource) / (toSource - fromSource) * (toTarget - fromTarget) + fromTarget;
        }

        public void Draw() {
            try {
                if (!Globals.Config.DungeonMaps.Enabled.Value) return;

                if (Globals.Config.DungeonMaps.DrawWhenClosed.Value == false && Globals.MapView.view.Visible == false) {
                    if (hud != null) hud.Clear();
                    return;
                }
                LandBlock currentBlock = LandBlockCache.Get(Globals.Core.Actions.Landcell);

                if (!currentBlock.IsDungeon()) {
                    if (hud != null) hud.Clear();
                    return;
                }

                if (hudRect == null) {
                    hudRect = new Rectangle(Globals.MapView.view.Location.X, Globals.MapView.view.Location.Y,
                        Globals.MapView.view.Width, Globals.MapView.view.Height);
                }

                hudRect.Y = Globals.MapView.view.Location.Y + Globals.MapView.view["DungeonMapsRenderContainer"].ClipRegion.Y;
                hudRect.X = Globals.MapView.view.Location.X + Globals.MapView.view["DungeonMapsRenderContainer"].ClipRegion.X;
                
                hudRect.Height = Globals.MapView.view.Height;
                hudRect.Width = Globals.MapView.view.Width;

                if (hud != null && (hud.Region.Width != hudRect.Width || hud.Region.Height != hudRect.Height)) {
                    hud.Enabled = false;
                    hud.Clear();
                    hud.Dispose();
                    hud = null;

                    drawGfx.Dispose();
                    drawGfx = null;

                    drawBitmap.Dispose();
                    drawBitmap = null;
                    drawBitmap = new Bitmap(hudRect.Width, hudRect.Height);
                    drawBitmap.MakeTransparent();

                    drawGfx = Graphics.FromImage(drawBitmap);
                }

                if (hud == null) {
                    hud = Globals.Core.RenderService.CreateHud(hudRect);
                }

                hud.Region = hudRect;

                hud.Clear();

                hud.Fill(Color.Transparent);
                hud.BeginRender();

                try {

                    DrawDungeon(currentBlock);
                }
                catch (Exception ex) { Util.LogException(ex); }
                finally {
                    hud.EndRender();
                    hud.Alpha = (int)Math.Round(((Globals.Config.DungeonMaps.Opacity.Value * 5) / 100F)*255);
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

            if (Globals.Config.DungeonMaps.Debug.Value) {
                var cells = currentBlock.GetCurrentCells();
                var offset = 0;
                foreach (var cell in cells) {
                    drawGfx.DrawString(string.Format("landcell: {0}, env: {1}, r: {2}, c: {3},{4},{5}",
                        Globals.Core.Actions.Landcell.ToString("X"),
                        cell.EnvironmentId,
                        cell.R,
                        cell.X,
                        cell.Y,
                        cell.Z), DEFAULT_FONT, TEXT_BRUSH, 0, offset);
                    offset += 15;
                }
            }

            drawGfx.Save();
            hud.DrawImage(drawBitmap, new Rectangle(0, 0, Globals.MapView.view.Width, Globals.MapView.view.Height));
        }

        public void Think() {
            if (DateTime.UtcNow - lastDrawTime > TimeSpan.FromMilliseconds(DRAW_INTERVAL)) {
                lastDrawTime = DateTime.UtcNow;

                var watch = System.Diagnostics.Stopwatch.StartNew();
                Draw();
                watch.Stop();
                if (counter % 50 == 0) {
                    counter = 0;
                    if (Globals.Config.DungeonMaps.Debug.Value) {
                        Util.WriteToChat(string.Format("DungeonMaps: took {0}ms to draw", watch.ElapsedMilliseconds));
                    }
                }
                ++counter;
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    Globals.MapView.view["DungeonMapsRenderContainer"].MouseEvent -= DungeonMaps_MouseEvent;
                    if (hud != null) {
                        Globals.Core.RenderService.RemoveHud(hud);
                        hud.Dispose();
                    }
                    if (drawGfx != null) drawGfx.Dispose();
                    if (drawBitmap != null) drawBitmap.Dispose();
                }
                disposed = true;
            }
        }
    }
}
