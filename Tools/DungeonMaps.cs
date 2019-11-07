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
using System.Threading;
using UtilityBelt;
using UtilityBelt.Lib;
using UtilityBelt.Lib.DungeonMaps;
using UtilityBelt.Lib.Settings;
using UtilityBelt.Tools;
using UtilityBelt.Views;
using VirindiViewService;
using VirindiViewService.Controls;

namespace UtilityBelt.Tools {
    public class DungeonMaps : IDisposable {
        private const int THINK_INTERVAL = 100;
        private int DRAW_INTERVAL = 45;
        private SolidBrush TEXT_BRUSH = new SolidBrush(Color.White);
        private SolidBrush TEXT_BRUSH_GREEN = new SolidBrush(Color.LightGreen);
        private const float QUALITY = 1F;
        private Font DEFAULT_FONT = new Font("Mono", 8);
        private Font PORTAL_FONT = new Font("Mono", 3);
        private DateTime lastDrawTime = DateTime.UtcNow;
        private bool disposed = false;
        private Hud hud = null;
        private Rectangle hudRect;
        internal Bitmap drawBitmap = null;
        private Bitmap compassBitmap = null;
        private float scale = 1;
        private int rawScale = 12;
        private int currentLandBlock = 0;
        private bool isFollowingCharacter = true;
        private bool isPanning = false;
        private float dragOffsetX = 0;
        private float dragOffsetY = 0;
        private float dragOffsetStartX = 0;
        private float dragOffsetStartY = 0;
        private int dragStartX = 0;
        private int dragStartY = 0;
        private int rotation = 0;
        private Dungeon currentBlock = null;
        private Vector3Object lastPosition = new Vector3Object(0, 0, 0);
        private int markerCount = 0;

        const int COL_ENABLED = 0;
        const int COL_ICON = 1;
        const int COL_NAME = 2;

        System.Windows.Forms.Timer zoomSaveTimer;
        private long lastDrawMs = 0;
        private long lastHudMs = 0;

        HudCheckBox UIDungeonMapsEnabled { get; set; }
        HudCheckBox UIDungeonMapsDrawWhenClosed { get; set; }
        HudCheckBox UIDungeonMapsShowVisitedTiles { get; set; }
        HudHSlider UIDungeonMapsOpacity { get; set; }
        HudButton UIDungeonMapsClearTileCache { get; set; }
        
        HudButton UIFollowCharacter { get; set; }

        HudList UIDungeonMapsSettingsList { get; set; }
        HudList UIDungeonMapsMarkersList { get; set; }

        public DungeonMaps() {
            try {
                scale = Globals.Settings.DungeonMaps.MapZoom;

                #region UI Setup
                UIFollowCharacter = (HudButton)Globals.MapView.view["FollowCharacter"];

                UIFollowCharacter.Hit += UIFollowCharacter_Hit;

                Globals.MapView.view["DungeonMapsRenderContainer"].MouseEvent += DungeonMaps_MouseEvent;

                UIDungeonMapsClearTileCache = (HudButton)Globals.MainView.view["DungeonMapsClearTileCache"];
                UIDungeonMapsClearTileCache.Hit += UIDungeonMapsClearTileCache_Hit;

                UIDungeonMapsEnabled = (HudCheckBox)Globals.MainView.view["DungeonMapsEnabled"];
                UIDungeonMapsEnabled.Change += UIDungeonMapsEnabled_Change;

                UIDungeonMapsDrawWhenClosed = (HudCheckBox)Globals.MainView.view["DungeonMapsDrawWhenClosed"];
                UIDungeonMapsDrawWhenClosed.Change += UIDungeonMapsDrawWhenClosed_Change;

                UIDungeonMapsShowVisitedTiles = (HudCheckBox)Globals.MainView.view["DungeonMapsShowVisitedTiles"];
                UIDungeonMapsShowVisitedTiles.Change += UIDungeonMapsShowVisitedTiles_Change;

                UIDungeonMapsOpacity = (HudHSlider)Globals.MainView.view["DungeonMapsOpacity"];
                UIDungeonMapsOpacity.Changed += UIDungeonMapsOpacity_Changed;

                UIDungeonMapsSettingsList = (HudList)Globals.MainView.view["DungeonMapsSettingsList"];
                UIDungeonMapsSettingsList.Click += UIDungeonMapsSettingsList_Click;

                UIDungeonMapsMarkersList = (HudList)Globals.MainView.view["DungeonMapsMarkersList"];
                UIDungeonMapsMarkersList.Click += UIDungeonMapsMarkersList_Click; ;

                UpdateUI();

                #endregion

                using (Stream manifestResourceStream = typeof(MainView).Assembly.GetManifestResourceStream("UtilityBelt.Resources.icons.compass.png")) {
                    if (manifestResourceStream != null) {
                        compassBitmap = new Bitmap(manifestResourceStream);
                        compassBitmap.MakeTransparent(Color.White);
                    }
                }

                Globals.Core.RegionChange3D += Core_RegionChange3D;

                Globals.MapView.view.Resize += View_Resize;
                Globals.MapView.view.Moved += View_Moved;

                Toggle();

                zoomSaveTimer = new System.Windows.Forms.Timer();
                zoomSaveTimer.Interval = 2000; // save the window position 2 seconds after it has stopped moving
                zoomSaveTimer.Tick += (s, e) => {
                    zoomSaveTimer.Stop();
                    Globals.Settings.DungeonMaps.MapZoom = scale;
                };

                PopulateSettings();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UpdateUI() {
            UIDungeonMapsEnabled.Checked = Globals.Settings.DungeonMaps.Enabled;
            UIDungeonMapsDrawWhenClosed.Checked = Globals.Settings.DungeonMaps.DrawWhenClosed;
            UIDungeonMapsShowVisitedTiles.Checked = Globals.Settings.DungeonMaps.ShowVisitedTiles;
            UIDungeonMapsOpacity.Position = Globals.Settings.DungeonMaps.Opacity;
        }

        #region UI Event Handlers
        private void UIFollowCharacter_Hit(object sender, EventArgs e) {
            try {
                isFollowingCharacter = true;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UIDungeonMapsEnabled_Change(object sender, EventArgs e) {
            try {
                Globals.Settings.DungeonMaps.Enabled = UIDungeonMapsEnabled.Checked;
                Toggle();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UIDungeonMapsDrawWhenClosed_Change(object sender, EventArgs e) {
            try {
                Globals.Settings.DungeonMaps.DrawWhenClosed = UIDungeonMapsDrawWhenClosed.Checked;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UIDungeonMapsShowVisitedTiles_Change(object sender, EventArgs e) {
            try {
                Globals.Settings.DungeonMaps.ShowVisitedTiles = UIDungeonMapsShowVisitedTiles.Checked;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UIDungeonMapsOpacity_Changed(int min, int max, int pos) {
            if (pos != Globals.Settings.DungeonMaps.Opacity) {
                Globals.Settings.DungeonMaps.Opacity = pos;
            }
        }

        private void View_Resize(object sender, EventArgs e) {
            try {
                RemoveHud();
                CreateHud();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void View_Moved(object sender, EventArgs e) {
            try {
                if (!Globals.Settings.DungeonMaps.Enabled) return;

                RemoveHud();
                CreateHud();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void DungeonMaps_MouseEvent(object sender, VirindiViewService.Controls.ControlMouseEventArgs e) {
            try {
                switch (e.EventType) {
                    case ControlMouseEventArgs.MouseEventType.MouseWheel:
                        if ((e.WheelAmount < 0 && rawScale < 16) || (e.WheelAmount > 0 && rawScale > 0)) {
                            var s = e.WheelAmount < 0 ? ++rawScale : --rawScale;

                            // todo: something else, i dont even know what this is anymore...
                            scale = 8.4F - Map(s, 0, 16, 0.4F, 8);

                            if (zoomSaveTimer.Enabled) zoomSaveTimer.Stop();
                            zoomSaveTimer.Start();
                        }
                        break;

                    case ControlMouseEventArgs.MouseEventType.MouseDown:
                        if (isFollowingCharacter) {
                            dragOffsetX = (float)Globals.Core.Actions.LocationX;
                            dragOffsetY = -(float)Globals.Core.Actions.LocationY;
                        }

                        dragOffsetStartX = dragOffsetX;
                        dragOffsetStartY = dragOffsetY;
                        dragStartX = e.X;
                        dragStartY = e.Y;

                        isPanning = true;
                        isFollowingCharacter = false;
                        break;

                    case ControlMouseEventArgs.MouseEventType.MouseUp:
                        isPanning = false;
                        break;

                    case ControlMouseEventArgs.MouseEventType.MouseMove:
                        if (isPanning) {
                            var angle = 180 - rotation - (Math.Atan2(e.Y - dragStartY, dragStartX - e.X) * 180.0 / Math.PI);
                            var distance = Math.Sqrt(Math.Pow(dragStartX - e.X, 2) + Math.Pow(dragStartY - e.Y, 2));
                            var np = Util.MovePoint(new PointF(dragOffsetStartX, dragOffsetStartY), angle, distance / scale);

                            dragOffsetX = np.X;
                            dragOffsetY = np.Y;
                        }
                        break;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UIDungeonMapsSettingsList_Click(object sender, int row, int col) {
            try {
                HudList.HudListRowAccessor clickedRow = UIDungeonMapsSettingsList[row];
                var name = ((HudStaticText)clickedRow[COL_NAME]).Text;
                var option = Globals.Settings.DungeonMaps.Display.GetPropValue<ColorToggleOption>(name);

                if (option == null) {
                    Util.WriteToChat("Bad option clicked: " + name);
                    return;
                }

                switch (col) {
                    case COL_ENABLED:
                        option.Enabled = ((HudCheckBox)clickedRow[COL_ENABLED]).Checked;
                        break;

                    case COL_ICON:
                        int originalColor = option.Color;
                        var picker = new ColorPicker(Globals.MainView, name, Color.FromArgb(originalColor));

                        Globals.Settings.DisableSaving();
                        
                        picker.RaiseColorPickerCancelEvent += (s, e) => {
                            // restore color
                            SetDisplayColor(name, originalColor);
                            Globals.Settings.EnableSaving();
                            picker.Dispose();
                        };

                        picker.RaiseColorPickerSaveEvent += (s, e) => {
                            // this is to force a change event
                            SetDisplayColor(name, originalColor);
                            Globals.Settings.EnableSaving();
                            SetDisplayColor(name, e.Color.ToArgb());
                            PopulateSettings();
                            picker.Dispose();
                        };

                        picker.RaiseColorPickerChangeEvent += (s, e) => {
                            SetDisplayColor(name, e.Color.ToArgb());
                        };

                        picker.view.VisibleChanged += (s, e) => {
                            // restore color
                            SetDisplayColor(name, originalColor);
                            Globals.Settings.EnableSaving();
                            if (!picker.view.Visible) {
                                picker.Dispose();
                            }
                        };
                        
                        break;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UIDungeonMapsMarkersList_Click(object sender, int row, int col) {
            try {
                HudList.HudListRowAccessor clickedRow = UIDungeonMapsMarkersList[row];
                var name = ((HudStaticText)clickedRow[COL_NAME]).Text;
                var option = Globals.Settings.DungeonMaps.Display.Markers.GetPropValue<MarkerToggleOption>(name);

                if (option == null) {
                    Util.WriteToChat("Bad option clicked: " + name);
                    return;
                }

                switch (col) {
                    case COL_ENABLED:
                        option.Enabled = ((HudCheckBox)clickedRow[COL_ENABLED]).Checked;
                        break;

                    case COL_ICON:
                        // todo: make this only disable saving this specific setting
                        Globals.Settings.DisableSaving();

                        var originalOptions = option.Clone();
                        var markerOptionsView = new MarkerOptions(Globals.MainView, option);

                        markerOptionsView.RaiseCancelEvent += (s, e) => {
                            option.RestoreFrom(originalOptions);
                            Globals.Settings.EnableSaving();
                            markerOptionsView.Dispose();
                        };

                        markerOptionsView.RaiseSaveEvent += (s, e) => {
                            var newOptions = option.Clone();
                            option.RestoreFrom(originalOptions);
                            Globals.Settings.EnableSaving();
                            option.RestoreFrom(newOptions);
                            PopulateSettings();
                            markerOptionsView.Dispose();
                        };

                        markerOptionsView.RaiseChangeEvent += (s, e) => {
                        };

                        markerOptionsView.view.VisibleChanged += (s, e) => {
                            if (!markerOptionsView.view.Visible) {
                                option.RestoreFrom(originalOptions);
                                Globals.Settings.EnableSaving();
                                markerOptionsView.Dispose();
                            }
                        };

                        break;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        #endregion

        private void SetDisplayColor(string name, int color) {
            var option = Globals.Settings.DungeonMaps.Display.GetPropValue<ColorToggleOption>(name);
            option.Color = color;

            if (Globals.Settings.DungeonMaps.Display.TileOptions.Contains(name)) {
                ClearTileCache();
            }
        }

        private void PopulateSettings() {
            try {
                PopulateDisplaySettings();
                PopulateMarkerSettings();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void PopulateMarkerSettings() {
            int scroll = 0;
            if (Globals.MainView.view.Visible) {
                scroll = UIDungeonMapsMarkersList.ScrollPosition;
            }

            UIDungeonMapsMarkersList.ClearRows();

            foreach (var setting in Globals.Settings.DungeonMaps.Display.Markers.ValidSettings) {
                var option = Globals.Settings.DungeonMaps.Display.Markers.GetPropValue<MarkerToggleOption>(setting);

                if (option != null) {
                    HudList.HudListRowAccessor row = UIDungeonMapsMarkersList.AddRow();
                    ((HudCheckBox)row[COL_ENABLED]).Checked = option.Enabled;
                    ((HudStaticText)row[COL_NAME]).Text = setting;
                    ((HudPictureBox)row[COL_ICON]).Image = GetSettingIcon(Color.FromArgb(option.Color));
                }
                else {
                    Util.WriteToChat("Bad DisplayOption: " + setting);
                }
            }

            UIDungeonMapsMarkersList.ScrollPosition = scroll;
        }

        private void PopulateDisplaySettings() {
            int scroll = 0;
            if (Globals.MainView.view.Visible) {
                scroll = UIDungeonMapsSettingsList.ScrollPosition;
            }

            UIDungeonMapsSettingsList.ClearRows();

            foreach (var setting in Globals.Settings.DungeonMaps.Display.ValidSettings) {
                var option = Globals.Settings.DungeonMaps.Display.GetPropValue<ColorToggleOption>(setting);

                if (option != null) {
                    HudList.HudListRowAccessor row = UIDungeonMapsSettingsList.AddRow();
                    ((HudCheckBox)row[COL_ENABLED]).Checked = option.Enabled;
                    ((HudStaticText)row[COL_NAME]).Text = setting;
                    ((HudPictureBox)row[COL_ICON]).Image = GetSettingIcon(Color.FromArgb(option.Color));
                }
                else {
                    Util.WriteToChat("Bad DisplayOption: " + setting);
                }
            }

            UIDungeonMapsSettingsList.ScrollPosition = scroll;
        }

        private ACImage GetSettingIcon(Color color) {
            var bmp = new Bitmap(32, 32);
            using (Graphics gfx = Graphics.FromImage(bmp)) {
                using (SolidBrush brush = new SolidBrush(color)) {
                    gfx.FillRectangle(brush, 0, 0, 32, 32);
                }
            }

            return new ACImage(bmp);
        }

        private void UIDungeonMapsClearTileCache_Hit(object sender, EventArgs e) {
            try {
                ClearTileCache();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Toggle() {
            try {
                var enabled = Globals.Settings.DungeonMaps.Enabled;
                Globals.MapView.view.ShowInBar = enabled;

                if (!enabled) {
                    if (Globals.MapView.view.Visible) {
                        Globals.MapView.view.Visible = false;
                    }
                    if (hud != null) {
                        ClearHud();
                    }
                }
                else {
                    CreateHud();
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void ClearTileCache() {
            TileCache.Clear();
        }

        private void ClearVisitedTiles() {
            if (currentBlock != null) {
                currentBlock.visitedTiles.Clear();
            }
        }

        private void Core_RegionChange3D(object sender, RegionChange3DEventArgs e) {
            try {
                RemoveHud();

                if (Globals.Settings.DungeonMaps.Enabled) {
                    CreateHud();
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public float Map(float value, float fromSource, float toSource, float fromTarget, float toTarget) {
            return (value - fromSource) / (toSource - fromSource) * (toTarget - fromTarget) + fromTarget;
        }

        #region Hud Stuff
        public Rectangle GetHudRect() {
            var rect = (hudRect != null) ? hudRect : new Rectangle(Globals.MapView.view.Location.X, Globals.MapView.view.Location.Y,
                    Globals.MapView.view.Width, Globals.MapView.view.Height);

            hudRect.Y = Globals.MapView.view.Location.Y + Globals.MapView.view["DungeonMapsRenderContainer"].ClipRegion.Y + 20;
            hudRect.X = Globals.MapView.view.Location.X + Globals.MapView.view["DungeonMapsRenderContainer"].ClipRegion.X;

            hudRect.Height = Globals.MapView.view.Height - 20;
            hudRect.Width = Globals.MapView.view.Width;

            return hudRect;
        }

        public void CreateHud() {
            if (hud != null) return;

            hud = Globals.Core.RenderService.CreateHud(GetHudRect());

            hud.Region = GetHudRect();
        }

        private void RemoveHud() {
            try {
                if (hud != null) {
                    hud.Enabled = false;
                    hud.Dispose();
                    hud = null;
                }
            }
            catch { }
        }

        public void ClearHud() {
            if (hud != null) {
                hud.Enabled = false;
                hud.Clear();
            }
        }

        public void UpdateHud() {
            DrawHud();
        }

        public bool DoesHudNeedUpdate() {
            if (!Globals.Settings.DungeonMaps.Enabled) return false;

            return false;
        }

        public void DrawHud() {
            try {
                if (hud == null) {
                    CreateHud();
                }

                hud.Clear();
                hud.Fill(Color.Transparent);
                hud.BeginRender();

                try {
                    // the whole "map", includes all icons/map tiles but not text
                    hud.DrawImage(drawBitmap, new Rectangle(0, 0, hud.Region.Width, hud.Region.Height));

                    DrawCompass(hud);
                    DrawMarkerLabels(hud);
                    DrawMapDebug(hud);
                }
                catch (Exception ex) { Logger.LogException(ex); }
                finally {
                    hud.EndRender();
                    hud.Alpha = (int)Math.Round(((Globals.Settings.DungeonMaps.Opacity * 5) / 100F)*255);
                    hud.Enabled = true;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void DrawCompass(Hud hud) {
            // compass icon that always points north
            if (Globals.Settings.DungeonMaps.ShowCompass && compassBitmap != null) {
                using (Bitmap rotatedCompass = new Bitmap(compassBitmap.Width, compassBitmap.Height)) {
                    using (Graphics compassGfx = Graphics.FromImage(rotatedCompass)) {
                        compassGfx.TranslateTransform(compassBitmap.Width / 2, compassBitmap.Height / 2);
                        compassGfx.RotateTransform(180 + rotation);
                        compassGfx.DrawImage(compassBitmap, -compassBitmap.Width / 2, -compassBitmap.Height / 2);
                        compassGfx.Save();
                        hud.DrawImage(rotatedCompass, new Rectangle(hud.Region.Width - compassBitmap.Width, 0, compassBitmap.Width, compassBitmap.Height));
                    }
                }
            }
        }

        private void DrawMarkerLabels(Hud hud) {
            // too zoomed out to draw marker labels?
            if (scale < 1.4) return;

            try {
                var brush = new SolidBrush(Color.Orange);
                hud.BeginText("Terminal", 10, Decal.Adapter.Wrappers.FontWeight.Normal, false);

                markerCount = 0;

                foreach (var wo in Globals.Core.WorldFilter.GetLandscape()) {
                    if (!ShouldDrawLabel(wo)) continue;

                    var objPos = wo.Offset();
                    var obj = PhysicsObject.FromId(wo.Id);
                    if (obj != null) {
                        objPos = new Vector3Object(obj.Position.X, obj.Position.Y, obj.Position.Z);
                        obj = null;
                    }

                    // clamp objects to the floor
                    var objZ = Math.Round(objPos.Z / 6) * 6;

                    // dont draw if its not on the same floor as us
                    if (Math.Abs(objZ - Globals.Core.Actions.LocationZ) > 5) continue;

                    var x = (objPos.X - Globals.Core.Actions.LocationX) * scale;
                    var y = (Globals.Core.Actions.LocationY - objPos.Y) * scale;

                    if (!isFollowingCharacter) {
                        x = (objPos.X - dragOffsetX) * scale;
                        y = (-dragOffsetY - objPos.Y) * scale;
                    }

                    var name = wo.Name;

                    if (wo.ObjectClass == ObjectClass.Portal) {
                        name = name.Replace("Portal to ", "").Replace(" Portal", "");
                    }

                    var rpoint = Util.RotatePoint(new Point((int)x, (int)y), new Point(0, 0), rotation + 180);
                    var textWidth = name.Length * 6;
                    var rect = new Rectangle(rpoint.X - (textWidth/2) + (hud.Region.Width / 2), rpoint.Y - 18 + (hud.Region.Height / 2), textWidth, 12);
                    var labelColor = Globals.Settings.DungeonMaps.Display.Markers.GetLabelColor(wo);

                    // inside map window?
                    if (rect.X < -(Dungeon.CELL_SIZE * scale) || rect.X > hud.Region.Width + (Dungeon.CELL_SIZE * scale)) {
                        continue;
                    }
                    if (rect.Y < -(Dungeon.CELL_SIZE * scale) || rect.Y > hud.Region.Height + (Dungeon.CELL_SIZE * scale)) {
                        continue;
                    }

                    hud.WriteText(name, labelColor, Decal.Adapter.Wrappers.WriteTextFormats.SingleLine, rect);
                    markerCount++;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
            finally {
                hud.EndText();
            }
        }

        private bool ShouldDrawLabel(WorldObject wo) {
            // make sure the client knows about this object
            if (!Globals.Core.Actions.IsValidObject(wo.Id)) return false;

            // make sure its close enough
            if (Globals.Core.WorldFilter.Distance(wo.Id, Globals.Core.CharacterFilter.Id) * 240 > 300) return false;

            if (!Globals.Settings.DungeonMaps.Display.Markers.ShouldDraw(wo)) return false;

            if (!Globals.Settings.DungeonMaps.Display.Markers.ShouldShowlabel(wo)) return false;

            return true;
        }

        private void DrawMapDebug(Hud hud) {
            // debug cell / environment debug text
            if (Globals.Settings.Plugin.Debug) {
                hud.BeginText("mono", 14, Decal.Adapter.Wrappers.FontWeight.Heavy, false);
                var cells = currentBlock.GetCurrentCells();
                var offset = 15;

                if (currentBlock != null) {
                    var stats = $"Tiles: {currentBlock.drawCount:D3} Markers: {markerCount:D3} Map: {lastDrawMs:D3}ms Hud: {lastHudMs:D3}ms";
                    var rect = new Rectangle(0, 0, hud.Region.Width, 15);

                    using (var bmp = new Bitmap(hud.Region.Width, 15)) {
                        var bgColor = Color.FromArgb(150, 0, 0, 0);
                        bmp.MakeTransparent();

                        using (var gfx = Graphics.FromImage(bmp)) {
                            gfx.FillRectangle(new SolidBrush(bgColor), 0, 0, bmp.Width, bmp.Height);
                            hud.DrawImage(bmp, rect);
                            hud.WriteText(stats, Color.White, Decal.Adapter.Wrappers.WriteTextFormats.SingleLine, rect);
                        }
                    }
                }

                foreach (var cell in cells) {
                    var message = string.Format("cell: {0}, env: {1}, r: {2}, pos: {3},{4},{5}",
                        cell.CellId.ToString("X8"),
                        cell.EnvironmentId,
                        cell.R.ToString(),
                        cell.X,
                        cell.Y,
                        cell.Z);
                    var color = Math.Abs(cell.Z - Globals.Core.Actions.LocationZ) < 2 ? Color.LightGreen : Color.White;
                    var rect2 = new Rectangle(0, offset, hud.Region.Width, offset + 15);

                    hud.WriteText(message, color, Decal.Adapter.Wrappers.WriteTextFormats.SingleLine, rect2);
                    offset += 15;
                }
                hud.EndText();
            }
        }
        #endregion

        public bool NeedsDraw() {
            if (!Globals.Settings.DungeonMaps.Enabled) return false;

            if (Globals.Settings.DungeonMaps.DrawWhenClosed == false && Globals.MapView.view.Visible == false) {
                hud.Clear();
                return false;
            }

            return true;
        }

        public void Think() {
            try {
                if (DateTime.UtcNow - lastDrawTime > TimeSpan.FromMilliseconds(DRAW_INTERVAL)) {
                    lastDrawTime = DateTime.UtcNow;

                    if (!NeedsDraw()) return;

                    currentBlock = DungeonCache.Get(Globals.Core.Actions.Landcell);

                    if (currentLandBlock != Globals.Core.Actions.Landcell >> 16 << 16) {
                        ClearHud();
                        currentLandBlock = Globals.Core.Actions.Landcell >> 16 << 16;

                        ClearVisitedTiles();
                    }

                    if (currentBlock == null) return;
                    if (!currentBlock.IsDungeon()) return;

                    if (!currentBlock.visitedTiles.Contains((uint)(Globals.Core.Actions.Landcell << 16 >> 16))) {
                        currentBlock.visitedTiles.Add((uint)(Globals.Core.Actions.Landcell << 16 >> 16));
                    }

                    var watch = System.Diagnostics.Stopwatch.StartNew();

                    float x = isFollowingCharacter ? (float)Globals.Core.Actions.LocationX : dragOffsetX;
                    float y = isFollowingCharacter ? - (float)Globals.Core.Actions.LocationY : dragOffsetY;

                    if (isFollowingCharacter) {
                        rotation = (int)(360 - (((float)Globals.Core.Actions.Heading + 180) % 360));
                    }

                    drawBitmap = currentBlock.Draw(x, y, scale, rotation, new Rectangle(0, 0, hud.Region.Width, hud.Region.Height));

                    watch.Stop();
                    var watch2 = System.Diagnostics.Stopwatch.StartNew();
                    UpdateHud();
                    watch2.Stop();

                    lastDrawMs = watch.ElapsedMilliseconds;
                    lastHudMs = watch2.ElapsedMilliseconds;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        #region IDisposable Support
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    Globals.Core.RegionChange3D -= Core_RegionChange3D;
                    Globals.MapView.view["DungeonMapsRenderContainer"].MouseEvent -= DungeonMaps_MouseEvent;
                    Globals.MapView.view.Resize -= View_Resize;
                    Globals.MapView.view.Moved -= View_Moved;

                    ClearTileCache();
                    ClearHud();

                    if (hud != null) {
                        Globals.Core.RenderService.RemoveHud(hud);
                        hud.Dispose();
                    }
                    if (drawBitmap != null) drawBitmap.Dispose();
                }
                disposed = true;
            }
        }
        #endregion
    }
}
