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
using System.Threading;
using UtilityBelt;
using UtilityBelt.Lib.DungeonMaps;
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
        private int counter = 0;
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
        private double lastHeading = 0;
        private bool needsDraw = true;

        const int COL_ENABLED = 0;
        const int COL_ICON = 1;
        const int COL_NAME = 2;

        System.Windows.Forms.Timer zoomSaveTimer;

        HudCheckBox UIDungeonMapsEnabled { get; set; }
        HudCheckBox UIDungeonMapsDebug { get; set; }
        HudCheckBox UIDungeonMapsDrawWhenClosed { get; set; }
        HudCheckBox UIDungeonMapsShowVisitedTiles { get; set; }
        HudHSlider UIDungeonMapsOpacity { get; set; }
        HudButton UIDungeonMapsClearTileCache { get; set; }
        
        HudButton UIFollowCharacter { get; set; }

        HudList UIDungeonMapsSettingsList { get; set; }

        public DungeonMaps() {
            try {
                scale = Globals.Config.DungeonMaps.MapZoom.Value;

                #region UI Setup
                UIFollowCharacter = (HudButton)Globals.MapView.view["FollowCharacter"];

                UIFollowCharacter.Hit += UIFollowCharacter_Hit;

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

                UIDungeonMapsShowVisitedTiles = (HudCheckBox)Globals.MainView.view["DungeonMapsShowVisitedTiles"];
                UIDungeonMapsShowVisitedTiles.Checked = Globals.Config.DungeonMaps.ShowVisitedTiles.Value;
                UIDungeonMapsShowVisitedTiles.Change += UIDungeonMapsShowVisitedTiles_Change;
                Globals.Config.DungeonMaps.ShowVisitedTiles.Changed += Config_DungeonMaps_ShowVisitedTiles_Changed;

                UIDungeonMapsOpacity = (HudHSlider)Globals.MainView.view["DungeonMapsOpacity"];
                UIDungeonMapsOpacity.Position = Globals.Config.DungeonMaps.Opacity.Value;
                UIDungeonMapsOpacity.Changed += UIDungeonMapsOpacity_Changed;

                UIDungeonMapsSettingsList = (HudList)Globals.MainView.view["DungeonMapsSettingsList"];
                UIDungeonMapsSettingsList.Click += UIDungeonMapsSettingsList_Click;

                #endregion

                using (Stream manifestResourceStream = typeof(MainView).Assembly.GetManifestResourceStream("UtilityBelt.Resources.icons.compass.png")) {
                    if (manifestResourceStream != null) {
                        compassBitmap = new Bitmap(manifestResourceStream);
                        compassBitmap.MakeTransparent(Color.White);
                    }
                }

                Globals.Core.RegionChange3D += Core_RegionChange3D;
                Globals.Core.WorldFilter.CreateObject += WorldFilter_CreateObject;

                Globals.MapView.view.Resize += View_Resize;
                Globals.MapView.view.Moved += View_Moved;

                Toggle();

                var currentLandblock = DungeonCache.Get(Globals.Core.Actions.Landcell);

                if (currentLandblock != null) {
                    foreach (var portal in Globals.Core.WorldFilter.GetByObjectClass(ObjectClass.Portal)) {
                        currentLandblock.AddPortal(portal);
                    }
                }

                zoomSaveTimer = new System.Windows.Forms.Timer();
                zoomSaveTimer.Interval = 2000; // save the window position 2 seconds after it has stopped moving
                zoomSaveTimer.Tick += (s, e) => {
                    zoomSaveTimer.Stop();
                    Globals.Config.DungeonMaps.MapZoom.Value = scale;
                };

                uTank2.PluginCore.PC.NavRouteChanged += PC_NavRouteChanged;
                uTank2.PluginCore.PC.NavWaypointChanged += PC_NavWaypointChanged;

                PopulateSettings();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void PC_NavWaypointChanged() {
            needsDraw = true;
        }

        private void PC_NavRouteChanged() {
            needsDraw = true;
        }

        #region UI Event Handlers
        private void UIFollowCharacter_Hit(object sender, EventArgs e) {
            try {
                isFollowingCharacter = true;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Config_DungeonMaps_Enabled_Changed(Setting<bool> obj) {
            try {
                UIDungeonMapsEnabled.Checked = Globals.Config.DungeonMaps.Enabled.Value;
                Toggle();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UIDungeonMapsEnabled_Change(object sender, EventArgs e) {
            try {
                Globals.Config.DungeonMaps.Enabled.Value = UIDungeonMapsEnabled.Checked;
                Toggle();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Config_DungeonMaps_Debug_Changed(Setting<bool> obj) {
            try {
                UIDungeonMapsDebug.Checked = Globals.Config.DungeonMaps.Debug.Value;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UIDungeonMapsDebug_Change(object sender, EventArgs e) {
            try {
                Globals.Config.DungeonMaps.Debug.Value = UIDungeonMapsDebug.Checked;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Config_DungeonMaps_DrawWhenClosed_Changed(Setting<bool> obj) {
            try {
                UIDungeonMapsDrawWhenClosed.Checked = Globals.Config.DungeonMaps.DrawWhenClosed.Value;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UIDungeonMapsDrawWhenClosed_Change(object sender, EventArgs e) {
            try {
                Globals.Config.DungeonMaps.DrawWhenClosed.Value = UIDungeonMapsDrawWhenClosed.Checked;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Config_DungeonMaps_ShowVisitedTiles_Changed(Setting<bool> obj) {
            try {
                UIDungeonMapsShowVisitedTiles.Checked = Globals.Config.DungeonMaps.ShowVisitedTiles.Value;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UIDungeonMapsShowVisitedTiles_Change(object sender, EventArgs e) {
            try {
                Globals.Config.DungeonMaps.ShowVisitedTiles.Value = UIDungeonMapsShowVisitedTiles.Checked;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UIDungeonMapsOpacity_Changed(int min, int max, int pos) {
            if (pos != Globals.Config.DungeonMaps.Opacity.Value) {
                Globals.Config.DungeonMaps.Opacity.Value = pos;
                needsDraw = true;
            }
        }

        private void View_Resize(object sender, EventArgs e) {
            try {
                RemoveHud();
                CreateHud();
                needsDraw = true;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void View_Moved(object sender, EventArgs e) {
            try {
                if (!Globals.Config.DungeonMaps.Enabled.Value) return;

                RemoveHud();
                CreateHud();
                needsDraw = true;
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
                            needsDraw = true;
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
                        needsDraw = true;
                        break;

                    case ControlMouseEventArgs.MouseEventType.MouseUp:
                        isPanning = false;
                        needsDraw = true;
                        break;

                    case ControlMouseEventArgs.MouseEventType.MouseMove:
                        if (isPanning) {
                            var angle = 180 - rotation - (Math.Atan2(e.Y - dragStartY, dragStartX - e.X) * 180.0 / Math.PI);
                            var distance = Math.Sqrt(Math.Pow(dragStartX - e.X, 2) + Math.Pow(dragStartY - e.Y, 2));
                            var np = Util.MovePoint(new PointF(dragOffsetStartX, dragOffsetStartY), angle, distance / scale);

                            dragOffsetX = np.X;
                            dragOffsetY = np.Y;
                            needsDraw = true;
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

                switch (col) {
                    case COL_ENABLED:
                        Globals.Config.DungeonMaps.GetFieldValue<Setting<bool>>($"Show{name}").Value = ((HudCheckBox)clickedRow[COL_ENABLED]).Checked;
                        needsDraw = true;
                        break;

                    case COL_ICON:
                        int originalColor = Globals.Config.DungeonMaps.GetFieldValue<Setting<int>>($"{name}Color").Value;
                        var picker = new ColorPicker(Globals.MainView, name, Color.FromArgb(originalColor));

                        Globals.Config.DisableSaving();
                        
                        picker.RaiseColorPickerCancelEvent += (s, e) => {
                            // restore color
                            SetDisplayColor(name, originalColor);
                            Globals.Config.EnableSaving();
                            picker.Dispose();
                            needsDraw = true;
                        };

                        picker.RaiseColorPickerSaveEvent += (s, e) => {
                            // this is to force a change event
                            SetDisplayColor(name, originalColor);
                            Globals.Config.EnableSaving();
                            SetDisplayColor(name, e.Color.ToArgb());
                            PopulateSettings();
                            picker.Dispose();
                            needsDraw = true;
                        };

                        picker.RaiseColorPickerChangeEvent += (s, e) => {
                            SetDisplayColor(name, e.Color.ToArgb());
                            needsDraw = true;
                        };

                        picker.view.VisibleChanged += (s, e) => {
                            // restore color
                            SetDisplayColor(name, originalColor);
                            Globals.Config.EnableSaving();
                            if (!picker.view.Visible) {
                                picker.Dispose();
                            }
                            needsDraw = true;
                        };
                        
                        break;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        #endregion

        private void SetDisplayColor(string name, int color) {
            Util.WriteToChat($"set {name}Color = {color} {Config.ShouldSave}");
            Globals.Config.DungeonMaps.GetFieldValue<Setting<int>>($"{name}Color").Value = color;

            if (Globals.Config.DungeonMaps.TileSettings.Contains(name)) {
                ClearTileCache();
            }
        }

        private void PopulateSettings() {
            try {
                int scroll = 0;
                if (Globals.MainView.view.Visible) {
                    scroll = UIDungeonMapsSettingsList.ScrollPosition;
                }

                UIDungeonMapsSettingsList.ClearRows();

                foreach (var setting in Globals.Config.DungeonMaps.Settings) {
                    HudList.HudListRowAccessor row = UIDungeonMapsSettingsList.AddRow();

                    bool isChecked = Globals.Config.DungeonMaps.GetFieldValue<Setting<bool>>($"Show{setting}").Value;

                    ((HudCheckBox)row[COL_ENABLED]).Checked = isChecked;
                    ((HudStaticText)row[COL_NAME]).Text = setting;
                    ((HudPictureBox)row[COL_ICON]).Image = GetSettingIcon(setting);
                }

                UIDungeonMapsSettingsList.ScrollPosition = scroll;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private ACImage GetSettingIcon(string setting) {
            int color = Globals.Config.DungeonMaps.GetFieldValue<Setting<int>>($"{setting}Color").Value;

            var bmp = new Bitmap(32, 32);
            using (Graphics gfx = Graphics.FromImage(bmp)) {
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(color))) {
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
                var enabled = Globals.Config.DungeonMaps.Enabled.Value;
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
            needsDraw = true;
        }

        private void ClearVisitedTiles() {
            if (currentBlock != null) {
                currentBlock.visitedTiles.Clear();
            }
        }

        private void Core_RegionChange3D(object sender, RegionChange3DEventArgs e) {
            try {
                if (!Globals.Config.DungeonMaps.Enabled.Value) return;
                RemoveHud();
                CreateHud();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void WorldFilter_CreateObject(object sender, CreateObjectEventArgs e) {
            try {
                if (!Globals.Config.DungeonMaps.Enabled.Value) return;

                if (e.New.ObjectClass == ObjectClass.Portal) {
                    var currentLandblock = DungeonCache.Get(Globals.Core.Actions.Landcell);

                    if (currentLandblock != null && e.New.Name != "Gateway") {
                        currentLandblock.AddPortal(e.New);
                    }
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
            if (!Globals.Config.DungeonMaps.Enabled.Value) return false;

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
                    DrawPortalLabels(hud);
                    DrawMapDebug(hud);
                }
                catch (Exception ex) { Logger.LogException(ex); }
                finally {
                    hud.EndRender();
                    hud.Alpha = (int)Math.Round(((Globals.Config.DungeonMaps.Opacity.Value * 5) / 100F)*255);
                    hud.Enabled = true;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void DrawCompass(Hud hud) {
            // compass icon that always points north
            if (Globals.Config.DungeonMaps.ShowCompass.Value && compassBitmap != null) {
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

        private void DrawPortalLabels(Hud hud) {
            if (!Globals.Config.DungeonMaps.ShowPortalsLabel.Value) return;

            // draw portal labels, the portal icons are drawn on the map itself
            // we only draw portal labels if the portal is on the same zLevel as us
            // todo: only draw portal labels within the window
            var zLayer = (int)Math.Round(Globals.Core.Actions.LocationZ / 6) * 6;
            if (currentBlock.zPortals.ContainsKey(zLayer)) {
                hud.BeginText("mono", 12, Decal.Adapter.Wrappers.FontWeight.Normal, false);
                foreach (var portal in currentBlock.zPortals[zLayer]) {
                    var x = ((portal.X - Globals.Core.Actions.LocationX)) * scale;
                    var y = (((Globals.Core.Actions.LocationY - portal.Y)) * scale);

                    if (!isFollowingCharacter) {
                        x = (portal.X - dragOffsetX) * scale;
                        y = (-dragOffsetY - portal.Y) * scale;
                    }

                    var rpoint = Util.RotatePoint(new Point((int)x, (int)y), new Point(0, 0), rotation + 180);
                    var rect = new Rectangle(rpoint.X + (hud.Region.Width / 2), rpoint.Y + (hud.Region.Height / 2), 200, 12);
                    var labelColor = Globals.Config.DungeonMaps.PortalsLabelColor.Value;

                    hud.WriteText(portal.Name, labelColor, Decal.Adapter.Wrappers.WriteTextFormats.SingleLine, rect);
                }
                hud.EndText();
            }
        }

        private void DrawMapDebug(Hud hud) {
            // debug cell / environment debug text
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
        #endregion

        public bool NeedsDraw() {
            var _needsDraw = needsDraw;
            needsDraw = false;

            if (!Globals.Config.DungeonMaps.Enabled.Value) return false;

            if (Globals.Config.DungeonMaps.DrawWhenClosed.Value == false && Globals.MapView.view.Visible == false) {
                hud.Clear();
                return false;
            }
            
            if (lastHeading != Globals.Core.Actions.Heading) {
                lastHeading = Globals.Core.Actions.Heading;
                _needsDraw = true;
            }

            if ((Globals.Core.Actions.LocationX != lastPosition.X || Globals.Core.Actions.LocationY != lastPosition.Y || Globals.Core.Actions.LocationZ != lastPosition.Z)) {
                lastPosition = new Vector3Object(Globals.Core.Actions.LocationX, Globals.Core.Actions.LocationY, Globals.Core.Actions.LocationZ);
                _needsDraw = true;
            }

            return _needsDraw;
        }

        public void Think() {
            try {
                if (DateTime.UtcNow - lastDrawTime > TimeSpan.FromMilliseconds(DRAW_INTERVAL)) {
                    lastDrawTime = DateTime.UtcNow;

                    if (!NeedsDraw()) return;

                    if (currentLandBlock != Globals.Core.Actions.Landcell >> 16 << 16) {
                        ClearHud();
                        currentLandBlock = Globals.Core.Actions.Landcell >> 16 << 16;

                        ClearVisitedTiles();
                    }

                    currentBlock = DungeonCache.Get(Globals.Core.Actions.Landcell);

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
                    if (counter % 60 == 0) {
                        counter = 0;
                        if (Globals.Config.DungeonMaps.Debug.Value) {
                            Util.WriteToChat(string.Format("DungeonMaps: draw: {0}ms update: {1}ms (drew {2} tiles)", watch.ElapsedMilliseconds, watch2.ElapsedMilliseconds, currentBlock.drawCount));
                        }
                    }
                    ++counter;
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
                    Globals.Core.WorldFilter.CreateObject += WorldFilter_CreateObject;
                    Globals.MapView.view["DungeonMapsRenderContainer"].MouseEvent -= DungeonMaps_MouseEvent;
                    Globals.MapView.view.Resize -= View_Resize;
                    Globals.MapView.view.Moved -= View_Moved;
                    uTank2.PluginCore.PC.NavRouteChanged -= PC_NavRouteChanged;
                    uTank2.PluginCore.PC.NavWaypointChanged -= PC_NavWaypointChanged;

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
