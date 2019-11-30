using Decal.Adapter;
using Decal.Adapter.Wrappers;
using Decal.Filters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using UtilityBelt;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Constants;
using UtilityBelt.Lib.DungeonMaps;
using UtilityBelt.Lib.Settings;
using UtilityBelt.Tools;
using UtilityBelt.Views;
using VirindiViewService;
using VirindiViewService.Controls;

namespace UtilityBelt.Tools {
    [Name("DungeonMaps")]
    [Summary("Draws an overlay with dungeon maps on your screen")]
    [FullDescription(@"
Draws an overlay with dungeon maps on your screen

* Open the UtilityBelt decal window, go to the DungeonMaps tab and enable maps.
* A new flashlight icon will appear on the virindi bar under the utilitybelt icon.
* Use the new map window to resize/move the map.
* When the maps window is open you can use your scrollwheel to zoom.
    ")]
    public class DungeonMaps : ToolBase {
        private const int DRAW_INTERVAL = 45;
        private DateTime lastDrawTime = DateTime.UtcNow;
        private Hud hud = null;
        private Rectangle hudRect = new Rectangle();
        internal Bitmap drawBitmap = null;
        readonly private Bitmap compassBitmap = null;
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
        private int markerCount = 0;

        readonly System.Windows.Forms.Timer zoomSaveTimer;
        private long lastDrawMs = 0;
        private long lastHudMs = 0;
        HudButton UIFollowCharacter;

        #region Config
        public const int MIN_ZOOM = 0;
        public const int MAX_ZOOM = 16;

        [Summary("Enabled")]
        [DefaultValue(false)]
        public bool Enabled {
            get { return (bool)GetSetting("Enabled"); }
            set { UpdateSetting("Enabled", value); }
        }

        [Summary("Draw dungeon maps even when map window is closed")]
        [DefaultValue(true)]
        public bool DrawWhenClosed {
            get { return (bool)GetSetting("DrawWhenClosed"); }
            set { UpdateSetting("DrawWhenClosed", value); }
        }

        [Summary("Show visited tiles")]
        [DefaultValue(true)]
        public bool ShowVisitedTiles {
            get { return (bool)GetSetting("ShowVisitedTiles"); }
            set { UpdateSetting("ShowVisitedTiles", value); }
        }

        [Summary("Show compass")]
        [DefaultValue(true)]
        public bool ShowCompass {
            get { return (bool)GetSetting("ShowCompass"); }
            set { UpdateSetting("ShowCompass", value); }
        }

        [Summary("Map opacity")]
        [DefaultValue(16)]
        public int Opacity {
            get { return (int)GetSetting("Opacity"); }
            set { UpdateSetting("Opacity", value); }
        }

        [Summary("Map Window X")]
        [DefaultValue(40)]
        public int MapWindowX {
            get { return (int)GetSetting("MapWindowX"); }
            set { UpdateSetting("MapWindowX", value); }
        }

        [Summary("Map Window Y")]
        [DefaultValue(150)]
        public int MapWindowY {
            get { return (int)GetSetting("MapWindowY"); }
            set { UpdateSetting("MapWindowY", value); }
        }

        [Summary("Map Window width")]
        [DefaultValue(300)]
        public int MapWindowWidth {
            get { return (int)GetSetting("MapWindowWidth"); }
            set { UpdateSetting("MapWindowWidth", value); }
        }

        [Summary("Map Window height")]
        [DefaultValue(280)]
        public int MapWindowHeight {
            get { return (int)GetSetting("MapWindowHeight"); }
            set { UpdateSetting("MapWindowHeight", value); }
        }

        [Summary("Map zoom level")]
        [DefaultValue(4.20f)]
        public float MapZoom {
            get { return (float)GetSetting("MapZoom"); }
            set { UpdateSetting("MapZoom", value); }
        }

        [Summary("Map display options")]
        public MapDisplayOptions Display { get; set; } = null;

        #region MapDisplayOptions
        [Section("DungeonMaps display options")]
        public class MapDisplayOptions : DisplaySectionBase {
            [JsonIgnore]
            public List<string> TileOptions = new List<string>() {
                "Walls",
                "InnerWalls",
                "RampedWalls",
                "Stairs",
                "Floors"
            };

            [JsonIgnore]
            public List<string> ValidSettings = new List<string>() {
                "Walls",
                "InnerWalls",
                "RampedWalls",
                "Stairs",
                "Floors",
                "VisualNavLines",
                "VisualNavStickyPoint"
            };

            [Summary("Walls")]
            [DefaultEnabled(true)]
            [DefaultColor(-16777089)]
            public ColorToggleOption Walls {
                get { return (ColorToggleOption)GetSetting("Walls"); }
                private set { UpdateSetting("Walls", value); }
            }

            [Summary("Inner wall")]
            [DefaultEnabled(true)]
            [DefaultColor(-8404993)]
            public ColorToggleOption InnerWalls {
                get { return (ColorToggleOption)GetSetting("InnerWalls"); }
                private set { UpdateSetting("InnerWalls", value); }
            }

            [Summary("Ramped wall")]
            [DefaultEnabled(true)]
            [DefaultColor(-11622657)]
            public ColorToggleOption RampedWalls {
                get { return (ColorToggleOption)GetSetting("RampedWalls"); }
                private set { UpdateSetting("RampedWalls", value); }
            }

            [Summary("Stairs")]
            [DefaultEnabled(true)]
            [DefaultColor(-16760961)]
            public ColorToggleOption Stairs {
                get { return (ColorToggleOption)GetSetting("Stairs"); }
                private set { UpdateSetting("Stairs", value); }
            }

            [Summary("Floor")]
            [DefaultEnabled(true)]
            [DefaultColor(-16744513)]
            public ColorToggleOption Floors {
                get { return (ColorToggleOption)GetSetting("Floors"); }
                private set { UpdateSetting("Floors", value); }
            }

            [Summary("VisualNav sticky point")]
            [DefaultEnabled(true)]
            [DefaultColor(-5374161)]
            public ColorToggleOption VisualNavStickyPoint {
                get { return (ColorToggleOption)GetSetting("VisualNavStickyPoint"); }
                private set { UpdateSetting("VisualNavStickyPoint", value); }
            }

            [Summary("VisualNav lines")]
            [DefaultEnabled(true)]
            [DefaultColor(-65281)]
            public ColorToggleOption VisualNavLines {
                get { return (ColorToggleOption)GetSetting("VisualNavLines"); }
                private set { UpdateSetting("VisualNavLines", value); }
            }

            [Summary("Marker display options")]
            public MarkerDisplayOptions Markers { get; set; } = null;

            public MapDisplayOptions(SectionBase parent) : base(parent) {
                Name = "Display";
                Markers = new MarkerDisplayOptions(this);
                Markers.Name = "Markers";
            }
        }
        #endregion

        #region MarkerDisplayOptions
        [Section("DungeonMaps marker display options")]
        public class MarkerDisplayOptions : DisplaySectionBase {
            [JsonIgnore]
            public List<string> ValidSettings = new List<string>() {
                "You",
                "Others",
                "Items",
                "Monsters",
                "NPCs",
                "Portals",
                "MyCorpse",
                "OtherCorpses",
                "Containers",
                "Doors",
                "EverythingElse"
            };

            [Summary("You")]
            [DefaultEnabled(true)]
            [DefaultColor(-65536)] // red
            [DefaultUseIcon(true)]
            [DefaultShowLabel(false)]
            [DefaultSize(3)]
            public MarkerToggleOption You {
                get { return (MarkerToggleOption)GetSetting("You"); }
                private set { UpdateSetting("You", value); }
            }

            [Summary("Others")]
            [DefaultEnabled(true)]
            [DefaultColor(-1)] // white
            [DefaultUseIcon(true)]
            [DefaultShowLabel(true)]
            [DefaultSize(3)]
            public MarkerToggleOption Others {
                get { return (MarkerToggleOption)GetSetting("Others"); }
                private set { UpdateSetting("Others", value); }
            }

            [Summary("Items")]
            [DefaultEnabled(true)]
            [DefaultColor(-1)] // white
            [DefaultUseIcon(true)]
            [DefaultShowLabel(true)]
            [DefaultSize(3)]
            public MarkerToggleOption Items {
                get { return (MarkerToggleOption)GetSetting("Items"); }
                private set { UpdateSetting("Items", value); }
            }

            [Summary("Monsters")]
            [DefaultEnabled(true)]
            [DefaultColor(-23296)] // orange
            [DefaultUseIcon(true)]
            [DefaultShowLabel(false)]
            [DefaultSize(3)]
            public MarkerToggleOption Monsters {
                get { return (MarkerToggleOption)GetSetting("Monsters"); }
                private set { UpdateSetting("Monsters", value); }
            }

            [Summary("NPCs")]
            [DefaultEnabled(true)]
            [DefaultColor(-256)] // yellow
            [DefaultUseIcon(true)]
            [DefaultShowLabel(false)]
            [DefaultSize(3)]
            public MarkerToggleOption NPCs {
                get { return (MarkerToggleOption)GetSetting("NPCs"); }
                private set { UpdateSetting("NPCs", value); }
            }

            [Summary("My Corpse")]
            [DefaultEnabled(true)]
            [DefaultColor(-65536)] // red 
            [DefaultUseIcon(true)]
            [DefaultShowLabel(true)]
            [DefaultSize(3)]
            public MarkerToggleOption MyCorpse {
                get { return (MarkerToggleOption)GetSetting("MyCorpse"); }
                private set { UpdateSetting("MyCorpse", value); }
            }

            [Summary("Other Corpses")]
            [DefaultEnabled(false)]
            [DefaultColor(-657931)] // white smoke
            [DefaultUseIcon(true)]
            [DefaultShowLabel(false)]
            [DefaultSize(3)]
            public MarkerToggleOption OtherCorpses {
                get { return (MarkerToggleOption)GetSetting("OtherCorpses"); }
                private set { UpdateSetting("OtherCorpses", value); }
            }

            [Summary("Portals")]
            [DefaultEnabled(true)]
            [DefaultColor(-3841)] // very light purple/pink (mostly white)
            [DefaultUseIcon(true)]
            [DefaultShowLabel(true)]
            [DefaultSize(3)]
            public MarkerToggleOption Portals {
                get { return (MarkerToggleOption)GetSetting("Portals"); }
                private set { UpdateSetting("Portals", value); }
            }

            [Summary("Containers")]
            [DefaultEnabled(true)]
            [DefaultColor(-744352)] // sandy brown
            [DefaultUseIcon(true)]
            [DefaultShowLabel(false)]
            [DefaultSize(3)]
            public MarkerToggleOption Containers {
                get { return (MarkerToggleOption)GetSetting("Containers"); }
                private set { UpdateSetting("Containers", value); }
            }

            [Summary("Doors")]
            [DefaultEnabled(true)]
            [DefaultColor(-5952982)] // brown
            [DefaultUseIcon(false)]
            [DefaultShowLabel(false)]
            [DefaultSize(3)]
            public MarkerToggleOption Doors {
                get { return (MarkerToggleOption)GetSetting("Doors"); }
                private set { UpdateSetting("Doors", value); }
            }

            [Summary("Everything Else")]
            [DefaultEnabled(false)]
            [DefaultColor(-657931)] // white smoke
            [DefaultUseIcon(true)]
            [DefaultShowLabel(false)]
            [DefaultSize(3)]
            public MarkerToggleOption EverythingElse {
                get { return (MarkerToggleOption)GetSetting("EverythingElse"); }
                private set { UpdateSetting("EverythingElse", value); }
            }

            public MarkerDisplayOptions(SectionBase parent) : base(parent) {
                Name = "Markers";
            }

            public int GetMarkerColor(WorldObject wo) {
                var propName = GetMarkerNameFromWO(wo);
                var prop = (MarkerToggleOption)(this.GetPropValue(propName));

                return prop == null ? Color.White.ToArgb() : prop.Color;
            }

            public bool ShouldShowlabel(WorldObject wo) {
                var propName = GetMarkerNameFromWO(wo);
                var prop = (MarkerToggleOption)(this.GetPropValue(propName));

                return prop == null ? false : prop.ShowLabel;
            }

            internal bool ShouldDraw(WorldObject wo) {
                var propName = GetMarkerNameFromWO(wo);
                var prop = (MarkerToggleOption)(this.GetPropValue(propName));

                return prop == null ? false : prop.Enabled;
            }

            internal int GetLabelColor(WorldObject wo) {
                var propName = GetMarkerNameFromWO(wo);
                var prop = (MarkerToggleOption)(this.GetPropValue(propName));

                return prop == null ? Color.White.ToArgb() : prop.Color;
            }

            internal bool ShouldUseIcon(WorldObject wo) {
                var propName = GetMarkerNameFromWO(wo);
                var prop = (MarkerToggleOption)(this.GetPropValue(propName));

                return prop == null ? true : prop.UseIcon;
            }

            internal int GetSize(WorldObject wo) {
                var propName = GetMarkerNameFromWO(wo);
                var prop = (MarkerToggleOption)(this.GetPropValue(propName));

                return prop == null ? 4 : prop.Size;
            }

            public string GetMarkerNameFromWO(WorldObject wo) {
                // check marker display settings
                switch (wo.ObjectClass) {
                    case ObjectClass.Player:
                        if (wo.Id == UtilityBeltPlugin.Instance.Core.CharacterFilter.Id) {
                            return "You";
                        }
                        else {
                            return "Others";
                        }

                    case ObjectClass.Monster:
                        return "Monsters";

                    case ObjectClass.Npc:
                    case ObjectClass.Vendor:
                        return "NPCs";

                    case ObjectClass.Portal:
                        return "Portals";

                    case ObjectClass.Corpse:
                        if (wo.Name == $"Corpse of {UtilityBeltPlugin.Instance.Core.CharacterFilter.Name}") {
                            return "MyCorpse";
                        }
                        else {
                            return "OtherCorpses";
                        }

                    case ObjectClass.Door:
                        return "Doors";

                    case ObjectClass.Container:
                        return "Containers";

                    default:
                        // draw anything not "stuck" as an item
                        if ((wo.Values(LongValueKey.Behavior, 0) & (int)BehaviorFlag.Stuck) == 0) {
                            return "Items";
                        }

                        return "EverythingElse";
                }
            }
        }
        #endregion
        #endregion

        public DungeonMaps(UtilityBeltPlugin ub, string name) : base(ub, name) {
            try {
                Display = new MapDisplayOptions(this);

                scale = MapZoom;

                #region UI Setup
                UIFollowCharacter = (HudButton)UB.MapView.view["FollowCharacter"];

                UIFollowCharacter.Hit += UIFollowCharacter_Hit;

                UB.MapView.view["DungeonMapsRenderContainer"].MouseEvent += DungeonMaps_MouseEvent;
                #endregion

                using (Stream manifestResourceStream = typeof(MainView).Assembly.GetManifestResourceStream("UtilityBelt.Resources.icons.compass.png")) {
                    if (manifestResourceStream != null) {
                        compassBitmap = new Bitmap(manifestResourceStream);
                        compassBitmap.MakeTransparent(Color.White);
                    }
                }

                UB.Core.RegionChange3D += Core_RegionChange3D;
                UB.Core.RenderFrame += Core_RenderFrame;
                PropertyChanged += DungeonMaps_PropertyChanged;
                UB.MapView.view.Resize += View_Resize;
                UB.MapView.view.Moved += View_Moved;

                Toggle();

                zoomSaveTimer = new System.Windows.Forms.Timer {
                    Interval = 2000 // save the window position 2 seconds after it has stopped moving
                };
                zoomSaveTimer.Tick += (s, e) => {
                    zoomSaveTimer.Stop();
                    MapZoom = scale;
                };
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void DungeonMaps_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            Toggle();
        }

        #region UI Event Handlers
        private void UIFollowCharacter_Hit(object sender, EventArgs e) {
            try {
                isFollowingCharacter = true;
            }
            catch (Exception ex) { Logger.LogException(ex); }
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
                if (!Enabled) return;

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
                            dragOffsetX = (float)UB.Core.Actions.LocationX;
                            dragOffsetY = -(float)UB.Core.Actions.LocationY;
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
        #endregion

        private void Toggle() {
            try {
                UB.MapView.view.Icon = UB.MapView.GetIcon();
                UB.MapView.view.ShowInBar = Enabled;

                if (!Enabled) {
                    if (UB.MapView.view.Visible) {
                        UB.MapView.view.Visible = false;
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

                if (Enabled) {
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
            hudRect.Y = UB.MapView.view.Location.Y + UB.MapView.view["DungeonMapsRenderContainer"].ClipRegion.Y + 20;
            hudRect.X = UB.MapView.view.Location.X + UB.MapView.view["DungeonMapsRenderContainer"].ClipRegion.X;

            hudRect.Height = UB.MapView.view.Height - 20;
            hudRect.Width = UB.MapView.view.Width;

            return hudRect;
        }

        public void CreateHud() {
            if (hud != null) {
                hud.Enabled = true;
                return;
            }

            hud = UB.Core.RenderService.CreateHud(GetHudRect());

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
            if (hud != null && hud.Enabled) {
                hud.Clear();
                hud.Enabled = false;
            }
        }

        public void UpdateHud() {
            DrawHud();
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
                    hud.Alpha = (int)Math.Round(((Opacity * 5) / 100F)*255);
                    hud.Enabled = true;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void DrawCompass(Hud hud) {
            // compass icon that always points north
            if (ShowCompass && compassBitmap != null) {
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
                hud.BeginText("Terminal", 10, Decal.Adapter.Wrappers.FontWeight.Normal, false);

                markerCount = 0;

                foreach (var wo in UB.Core.WorldFilter.GetLandscape()) {
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
                    if (Math.Abs(objZ - UB.Core.Actions.LocationZ) > 5) continue;

                    var x = (objPos.X - UB.Core.Actions.LocationX) * scale;
                    var y = (UB.Core.Actions.LocationY - objPos.Y) * scale;

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
                    var labelColor = Display.Markers.GetLabelColor(wo);

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
            if (!UB.Core.Actions.IsValidObject(wo.Id)) return false;

            // make sure its close enough
            if (UB.Core.WorldFilter.Distance(wo.Id, UB.Core.CharacterFilter.Id) * 240 > 300) return false;

            if (!Display.Markers.ShouldDraw(wo)) return false;

            if (!Display.Markers.ShouldShowlabel(wo)) return false;

            return true;
        }

        private void DrawMapDebug(Hud hud) {
            // debug cell / environment debug text
            if (UB.Plugin.Debug) {
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
                    var color = Math.Abs(cell.Z - UB.Core.Actions.LocationZ) < 2 ? Color.LightGreen : Color.White;
                    var rect2 = new Rectangle(0, offset, hud.Region.Width, offset + 15);

                    hud.WriteText(message, color, Decal.Adapter.Wrappers.WriteTextFormats.SingleLine, rect2);
                    offset += 15;
                }
                hud.EndText();
            }
        }
        #endregion

        public bool NeedsDraw() {
            if (!Enabled) return false;

            if (DrawWhenClosed == false && UB.MapView.view.Visible == false) {
                hud.Clear();
                return false;
            }

            return true;
        }

        public void Core_RenderFrame(object sender, EventArgs e) {
            try {
                if (!Enabled) return;

                if (DateTime.UtcNow - lastDrawTime > TimeSpan.FromMilliseconds(DRAW_INTERVAL)) {
                    lastDrawTime = DateTime.UtcNow;

                    if (!NeedsDraw()) return;

                    currentBlock = DungeonCache.Get((uint)UB.Core.Actions.Landcell);

                    if (currentLandBlock != UB.Core.Actions.Landcell >> 16 << 16) {
                        ClearHud();
                        currentLandBlock = UB.Core.Actions.Landcell >> 16 << 16;

                        ClearVisitedTiles();
                    }

                    if (currentBlock == null) return;
                    if (!currentBlock.IsDungeon()) return;

                    if (!currentBlock.visitedTiles.Contains((uint)(UB.Core.Actions.Landcell << 16 >> 16))) {
                        currentBlock.visitedTiles.Add((uint)(UB.Core.Actions.Landcell << 16 >> 16));
                    }

                    if (hud == null || hud.Region == null) return;

                    var watch = System.Diagnostics.Stopwatch.StartNew();

                    float x = isFollowingCharacter ? (float)UB.Core.Actions.LocationX : dragOffsetX;
                    float y = isFollowingCharacter ? - (float)UB.Core.Actions.LocationY : dragOffsetY;

                    if (isFollowingCharacter) {
                        rotation = (int)(360 - (((float)UB.Core.Actions.Heading + 180) % 360));
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
        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    UB.Core.RegionChange3D -= Core_RegionChange3D;
                    UB.Core.RenderFrame -= Core_RenderFrame;

                    ClearTileCache();
                    ClearHud();

                    if (hud != null) {
                        UB.Core.RenderService.RemoveHud(hud);
                        hud.Dispose();
                    }
                    if (drawBitmap != null) drawBitmap.Dispose();
                    if (compassBitmap != null) compassBitmap.Dispose();
                    if (zoomSaveTimer != null) zoomSaveTimer.Dispose();

                    base.Dispose(disposing);
                }
                disposedValue = true;
            }
        }
        #endregion
    }
}
