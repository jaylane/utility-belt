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
using VirindiViewService;
using VirindiViewService.Controls;

namespace UtilityBelt.Tools {
    public class DungeonCell {
        public int CellId;
        public ushort EnvironmentId;
        public float X;
        public float Y;
        public float Z;
        public int R = 0;

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
            catch (Exception ex) { Util.LogException(ex); }
        }

        public string GetCoords() {
            return string.Format("{0},{1},{2}", X, Y, Z);
        }
    }

    public class Portal {
        public int Id;
        public string Name;
        public double X;
        public double Y;
        public double Z;

        public Portal(WorldObject portal) {
            Id = portal.Id;
            Name = portal.Name.Replace("Portal to", "").Replace("Portal", "");

            var offset = portal.Offset();

            X = offset.X;
            Y = offset.Y;
            Z = Math.Round(offset.Z);
        }
    }

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
            catch (Exception ex) { Util.LogException(ex); }
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

    public static class LandBlockCache {
        private static Dictionary<int, LandBlock> cache = new Dictionary<int, LandBlock>();

        public static LandBlock Get(int cellId) {
            if (cache.ContainsKey(cellId >> 16 << 16)) return cache[cellId >> 16 << 16];
            if ((uint)(cellId << 16 >> 16) < 0x0100) return null;

            var watch = System.Diagnostics.Stopwatch.StartNew();
            var block = new LandBlock(cellId);
            watch.Stop();

            Util.WriteToChat(string.Format("DungeonMaps: took {0}ms to cache LandBlock {1} (isDungeon? {2} ({3}))", watch.ElapsedMilliseconds, (cellId).ToString("X8"), block.IsDungeon(), ((uint)(Globals.Core.Actions.Landcell << 16 >> 16)).ToString("X4")));

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
            string bitmapFile = Path.Combine(Util.GetTilePath(), environmentId + @".bmp");

            if (File.Exists(bitmapFile)) {
                using (Bitmap bmp = new Bitmap(bitmapFile)) {
                    bmp.MakeTransparent(Color.White);
                    image = new Bitmap(bmp.Clone(new Rectangle(0, 0, bmp.Width, bmp.Height), PixelFormat.Format32bppPArgb));
                }
            }
            else {
                image = null;
            }

            cache.Add(environmentId, image);

            return Get(environmentId);
        }

        public static void Clear() {
            var keys = cache.Keys.ToArray();
            foreach (var key in keys) {
                if (cache[key] != null) {
                    cache[key].Dispose();
                    cache[key] = null;
                }
            }

            cache.Clear();
        }
    }

    class DungeonMaps : IDisposable {
        private const int THINK_INTERVAL = 100;
        private const int DRAW_INTERVAL = 60;
        private SolidBrush PLAYER_BRUSH = new SolidBrush(Color.Red);
        private SolidBrush TEXT_BRUSH = new SolidBrush(Color.White);
        private SolidBrush TEXT_BRUSH_GREEN = new SolidBrush(Color.LightGreen);
        private const float QUALITY = 1F;
        private Font DEFAULT_FONT = new Font("Mono", 8);
        private Font PORTAL_FONT = new Font("Mono", 3);
        private const int PLAYER_SIZE = 4;
        private Rectangle PLAYER_RECT = new Rectangle(-(PLAYER_SIZE / 2), -(PLAYER_SIZE / 2), PLAYER_SIZE, PLAYER_SIZE);
        private DateTime lastDrawTime = DateTime.UtcNow;
        private bool disposed = false;
        private Hud hud = null;
        private Rectangle hudRect;
        private Bitmap drawBitmap;
        private int counter = 0;
        private float scale = 1;
        private int rawScale = 12;
        private  int MIN_SCALE = 0;
        private  int MAX_SCALE = 16;
        private int currentLandCell = 0;
        private Graphics drawGfx;

        HudCheckBox UIDungeonMapsEnabled { get; set; }
        HudCheckBox UIDungeonMapsDebug { get; set; }
        HudCheckBox UIDungeonMapsDrawWhenClosed { get; set; }
        HudHSlider UIDungeonMapsOpacity { get; set; }
        HudButton UIDungeonMapsClearTileCache { get; set; }

        public DungeonMaps() {
            scale = 8.4F - Map(rawScale, MIN_SCALE, MAX_SCALE, 0.4F, 8);

            Globals.MapView.view["DungeonMapsRenderContainer"].MouseEvent += DungeonMaps_MouseEvent;

            UIDungeonMapsClearTileCache = (HudButton)Globals.MainView.view["DungeonMapsClearTileCache"];
            UIDungeonMapsClearTileCache.Hit += UIDungeonMapsClearTileCache_Hit;

            UIDungeonMapsEnabled = (HudCheckBox)Globals.MainView.view["DungeonMapsEnabled"];
            UIDungeonMapsEnabled.Checked = Globals.Config.DungeonMaps.Enabled.Value;
            UIDungeonMapsEnabled.Change += UIDungeonMapsEnabled_Change;
            Globals.Config.DungeonMaps.Enabled.Changed += Config_DungeonMaps_Enabled_Changed;

            UIDungeonMapsDebug = (HudCheckBox)Globals.MainView.view["DungeonMapsDebug"];
            UIDungeonMapsDebug.Checked = Globals.Config.DungeonMaps.Debug.Value;
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

            Globals.Core.RegionChange3D += Core_RegionChange3D;
            Globals.Core.WorldFilter.CreateObject += WorldFilter_CreateObject;

            Globals.MapView.view.Resize += View_Resize;
            Globals.MapView.view.Moved += View_Moved;

            Toggle();

            var currentLandblock = LandBlockCache.Get(Globals.Core.Actions.Landcell);

            if (currentLandblock != null) {
                foreach (var portal in Globals.Core.WorldFilter.GetByObjectClass(ObjectClass.Portal)) {
                    currentLandblock.AddPortal(portal);
                }
            }
        }

        private void Toggle() {
            try {
                var enabled = Globals.Config.DungeonMaps.Enabled.Value;
                Globals.MapView.view.ShowInBar = enabled;

                if (!enabled) {
                    if (Globals.MapView.view.Visible) {
                        Globals.MapView.view.Visible = false;
                    }
                    if (hud != null) {
                        DestroyHud();
                    }
                }
                else {
                    CreateHud();
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
                ClearCache();
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        private void ClearCache() {
            LandBlockCache.Clear();
            TileCache.Clear();
            LandBlock.portalIds.Clear();
            DestroyHud();
            CreateHud();
        }

        private void View_Resize(object sender, EventArgs e) {
            try {
                DestroyHud();
                CreateHud();
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

        private void Core_RegionChange3D(object sender, RegionChange3DEventArgs e) {
            try {
                if (!Globals.Config.DungeonMaps.Enabled.Value) return;

                DestroyHud();
                CreateHud();
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        private void View_Moved(object sender, EventArgs e) {
            try {
                if (!Globals.Config.DungeonMaps.Enabled.Value) return;

                DestroyHud();
                CreateHud();
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        private void WorldFilter_CreateObject(object sender, CreateObjectEventArgs e) {
            try {
                if (!Globals.Config.DungeonMaps.Enabled.Value) return;

                if (e.New.ObjectClass == ObjectClass.Portal) {
                    var currentLandblock = LandBlockCache.Get(Globals.Core.Actions.Landcell);

                    if (currentLandblock != null && e.New.Name != "Gateway") {
                        currentLandblock.AddPortal(e.New);
                    }
                }
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        public float Map(float value, float fromSource, float toSource, float fromTarget, float toTarget) {
            return (value - fromSource) / (toSource - fromSource) * (toTarget - fromTarget) + fromTarget;
        }

        public Rectangle GetHudRect() {
            var rect = (hudRect != null) ? hudRect : new Rectangle(Globals.MapView.view.Location.X, Globals.MapView.view.Location.Y,
                    Globals.MapView.view.Width, Globals.MapView.view.Height);

            hudRect.Y = Globals.MapView.view.Location.Y + Globals.MapView.view["DungeonMapsRenderContainer"].ClipRegion.Y;
            hudRect.X = Globals.MapView.view.Location.X + Globals.MapView.view["DungeonMapsRenderContainer"].ClipRegion.X;

            hudRect.Height = Globals.MapView.view.Height;
            hudRect.Width = Globals.MapView.view.Width;

            return hudRect;
        }

        public void CreateHud() {
            if (hud != null) Util.WriteToChat("Tried to create hud when it already exists!");

            hud = Globals.Core.RenderService.CreateHud(GetHudRect());

            hud.Region = GetHudRect();

            drawBitmap = new Bitmap(hud.Region.Width, hud.Region.Height, PixelFormat.Format32bppPArgb);
            drawBitmap.MakeTransparent();

            drawGfx = Graphics.FromImage(drawBitmap);
        }

        public void DestroyHud() {
            if (hud != null) {
                hud.Enabled = false;
                hud.Clear();
                hud.Dispose();
                hud = null;
            }

            if (drawGfx != null) {
                drawGfx.Dispose();
                drawGfx = null;
            }

            if (drawBitmap != null) {
                drawBitmap.Dispose();
                drawBitmap = null;
            }
        }

        public void UpdateHud() {
            if (!Globals.Config.DungeonMaps.Enabled.Value) return;

            Draw();
        }

        public bool DoesHudNeedUpdate() {
            if (!Globals.Config.DungeonMaps.Enabled.Value) return false;

            return false;
        }

        public void Draw() {
            try {
                if (!Globals.Config.DungeonMaps.Enabled.Value) return;
                if (hud == null) {
                    CreateHud();
                }

                if (Globals.Config.DungeonMaps.DrawWhenClosed.Value == false && Globals.MapView.view.Visible == false) {
                    if (hud != null) hud.Clear();
                    return;
                }

                LandBlock currentBlock = LandBlockCache.Get(Globals.Core.Actions.Landcell);

                if (currentBlock == null || (currentBlock != null && !currentBlock.IsDungeon())) {
                    if (hud != null) hud.Clear();
                    return;
                }
                
                hud.Clear();
                hud.Fill(Color.Transparent);

                hud.BeginRender();

                try {
                    var ratio = GetBitmapToWindowRatio();

                    hud.DrawImage(drawBitmap, new Rectangle(0, 0, hud.Region.Width, hud.Region.Height));

                    // draw portal labels
                    var zLayer = (int)Math.Round(Globals.Core.Actions.LocationZ / 6) * 6;
                    if (currentBlock.zPortals.ContainsKey(zLayer)) {
                        hud.BeginText("mono", 12, Decal.Adapter.Wrappers.FontWeight.Normal, false);
                        foreach (var portal in currentBlock.zPortals[zLayer]) {
                            var x = ((portal.X - Globals.Core.Actions.LocationX)) * scale;
                            var y = (((Globals.Core.Actions.LocationY - portal.Y)) * scale);
                            var rpoint = Util.RotatePoint(new Point((int)x, (int)y), new Point(0, 0), 360-Globals.Core.Actions.Heading); 
                            var rect = new Rectangle(rpoint.X + (hud.Region.Width/2), rpoint.Y + (hud.Region.Height / 2), 200, 12);

                            hud.WriteText(portal.Name, Color.White, Decal.Adapter.Wrappers.WriteTextFormats.SingleLine, rect);
                        }
                        hud.EndText();
                    }

                    if (Globals.Config.DungeonMaps.Debug.Value) {
                        hud.BeginText("mono", 14, Decal.Adapter.Wrappers.FontWeight.Heavy, false);
                        var cells = currentBlock.GetCurrentCells();
                        var offset = 0;

                        foreach (var cell in cells) {
                            var message = string.Format("cell: {0}, env: {1}, r: {2}, pos: {3},{4},{5}",
                                cell.CellId.ToString("X8"),
                                cell.EnvironmentId,
                                cell.R.ToString(),
                                cell.X,
                                cell.Y,
                                cell.Z);
                            var color = Math.Abs(cell.Z - Globals.Core.Actions.LocationZ) < 2 ? Color.LightGreen : Color.White;
                            var rect = new Rectangle(0, offset, hud.Region.Width, offset + 15);

                            hud.WriteText(message, color, Decal.Adapter.Wrappers.WriteTextFormats.SingleLine, rect);
                            offset += 15;
                        }
                        hud.EndText();
                    }
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
            if (drawBitmap == null || drawGfx == null) return;

            float playerXOffset = (float)Globals.Core.Actions.LocationX;
            float playerYOffset = -(float)Globals.Core.Actions.LocationY;
            int offsetX = (int)(drawBitmap.Width / 2);
            int offsetY = (int)(drawBitmap.Height / 2);
            int rotation = (int)(360 - (((float)Globals.Core.Actions.Heading + 180) % 360));


            drawGfx.SmoothingMode = SmoothingMode.HighSpeed;
            drawGfx.InterpolationMode = InterpolationMode.Low;
            drawGfx.CompositingQuality = CompositingQuality.HighSpeed;
            drawGfx.Clear(Color.Transparent);
            GraphicsState gs = drawGfx.Save();

            drawGfx.TranslateTransform(offsetX, offsetY);
            drawGfx.RotateTransform(rotation);
            drawGfx.ScaleTransform(scale, scale);
            drawGfx.TranslateTransform(playerXOffset, playerYOffset);
            var zLayers = currentBlock.bitmapLayers.Keys.ToList();
            var portals = Globals.Core.WorldFilter.GetByObjectClass(ObjectClass.Portal);

            foreach (var zLayer in currentBlock.zLayers.Keys) {
                foreach (var cell in currentBlock.zLayers[zLayer]) {
                    var rotated = TileCache.Get(cell.EnvironmentId);
                    var x = (int)Math.Round(cell.X);
                    var y = (int)Math.Round(cell.Y);

                    if (rotated == null) continue;

                    GraphicsState gs1 = drawGfx.Save();
                    drawGfx.TranslateTransform(x, y);
                    drawGfx.RotateTransform(cell.R);

                    ImageAttributes attributes = new ImageAttributes();

                    // floors above your char
                    if (Globals.Core.Actions.LocationZ - cell.Z < -3) {
                        float b = 1.0F - (float)(Math.Abs(Globals.Core.Actions.LocationZ - cell.Z) / 6) * 0.5F;
                        ColorMatrix matrix = new ColorMatrix(new float[][]{
                            new float[] {1, 0, 0, 0, 0},
                            new float[] {0, 1, 0, 0, 0},
                            new float[] {0, 0, 1, 0, 0},
                            new float[] {0, 0, 0, b, 0},
                            new float[] {0, 0, 0, 0, 1},
                        });
                        attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                    }
                    // current floor
                    else if (Math.Abs(Globals.Core.Actions.LocationZ - cell.Z) < 3) {

                    }
                    // floors below
                    else {
                        float b = 1.0F - (float)(Math.Abs(Globals.Core.Actions.LocationZ - cell.Z) / 6) * 0.4F;
                        ColorMatrix matrix = new ColorMatrix(new float[][]{
                            new float[] {b, 0, 0, 0, 0},
                            new float[] {0, b, 0, 0, 0},
                            new float[] {0, 0, b, 0, 0},
                            new float[] {0, 0, 0, 1, 0},
                            new float[] {0, 0, 0, 0, 1},
                        });
                        attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                    }

                    // TODO: bitblt/LockBitmap
                    drawGfx.DrawImage(rotated, new Rectangle(-5, -5, rotated.Width + 1, rotated.Height + 1), 0, 0, rotated.Width, rotated.Height, GraphicsUnit.Pixel, attributes);

                    drawGfx.RotateTransform(-cell.R);
                    drawGfx.TranslateTransform(-x, -y);
                    attributes.Dispose();
                    drawGfx.Restore(gs1);
                }
                var opacity = Math.Max(Math.Min(1 - (Math.Abs(Globals.Core.Actions.LocationZ - zLayer) / 6) * 0.4F, 1), 0) * 255;
                var portalBrush = new SolidBrush(Color.FromArgb((int)opacity, 153, 0, 204));

                // draw portal markers
                if (currentBlock.zPortals.ContainsKey(zLayer)) {
                    foreach (var portal in currentBlock.zPortals[zLayer]) {
                        drawGfx.FillEllipse(portalBrush, -(float)(portal.X + 1.5), (float)(portal.Y - 1.5), 2F, 2F);
                    }
                }

                portalBrush.Dispose();
            }

            drawGfx.TranslateTransform(-playerXOffset, -playerYOffset);
            drawGfx.ScaleTransform(1 / scale, 1 / scale);
            drawGfx.RotateTransform(-rotation);

            drawGfx.FillRectangle(PLAYER_BRUSH, PLAYER_RECT);

            drawGfx.Restore(gs);
        }

        private double GetBitmapToWindowRatio() {
            double ratioX = (double)Globals.MapView.view.Width / (double)drawBitmap.Width;
            double ratioY = (double)Globals.MapView.view.Height / (double)drawBitmap.Height;
            return ratioX > ratioY ? ratioX : ratioY;
        }

        public void Think() {
            if (DateTime.UtcNow - lastDrawTime > TimeSpan.FromMilliseconds(DRAW_INTERVAL)) {
                lastDrawTime = DateTime.UtcNow;

                if ((uint)(Globals.Core.Actions.Landcell << 16 >> 16) < 0x0100) {
                    if (hud != null) {
                        hud.Clear();
                        DestroyHud();
                    }
                    currentLandCell = Globals.Core.Actions.Landcell >> 16 << 16;
                    return;
                }

                if (currentLandCell != Globals.Core.Actions.Landcell >> 16 << 16) {
                    DestroyHud();
                    CreateHud();
                    currentLandCell = Globals.Core.Actions.Landcell >> 16 << 16;

                    if (Globals.Config.DungeonMaps.Debug.Value) {
                        Util.WriteToChat("DungeonMaps: Redraw hud because landcell changed");
                    }
                }

                var watch = System.Diagnostics.Stopwatch.StartNew();
                LandBlock currentBlock = LandBlockCache.Get(Globals.Core.Actions.Landcell);

                DrawDungeon(currentBlock);
                watch.Stop();
                var watch2 = System.Diagnostics.Stopwatch.StartNew();
                UpdateHud();
                watch2.Stop();
                if (counter % 30 == 0) {
                    counter = 0;
                    if (Globals.Config.DungeonMaps.Debug.Value) {
                        Util.WriteToChat(string.Format("DungeonMaps: draw: {0}ms update: {1}ms", watch.ElapsedMilliseconds, watch2.ElapsedMilliseconds));
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
                    Globals.Core.RegionChange3D -= Core_RegionChange3D;
                    Globals.Core.WorldFilter.CreateObject += WorldFilter_CreateObject;
                    Globals.MapView.view["DungeonMapsRenderContainer"].MouseEvent -= DungeonMaps_MouseEvent;
                    Globals.MapView.view.Resize -= View_Resize;
                    Globals.MapView.view.Moved -= View_Moved;

                    ClearCache();
                    DestroyHud();

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
