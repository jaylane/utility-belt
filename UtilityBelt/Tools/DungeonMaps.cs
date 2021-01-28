using Decal.Adapter;
using Decal.Adapter.Wrappers;
using Microsoft.DirectX;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Dungeon;
using UtilityBelt.Lib.Settings;
using UBLoader.Lib.Settings;
using UtilityBelt.Lib.VTNav.Waypoints;
using VirindiViewService;
using VirindiViewService.Controls;

namespace UtilityBelt.Tools {
    [Name("DungeonMaps")]
    [Summary("Draws an overlay with dungeon maps on your screen, with data courtesy of lifestoned.org")]
    [FullDescription(@"
Draws an overlay with dungeon maps on your screen, with data courtesy of lifestoned.org

* Open the UtilityBelt decal window, and click the Dungeon Maps button to enable.
* A new flashlight icon will appear on the virindi bar under the utilitybelt icon.
* Open the maps window to be able to view any dungeon map, or access the controls.
* Checking the 'All' checkbox will draw all dungeon levels at once, or you can use the slider to switch between levels.
* When the maps window is open you can use your scrollwheel to zoom, or click and drag to pan the map. Holding shift while dragging will rotate the map.
* Click the follow button to snap the map to your character position and have it follow you around as you move.
* If a lever/button is connected to a door, both the door and lever will get a [A] tag appended to it.  IE Lever[A] opens Door[A], Button[C] opens Door[C].
* If a Chest or a door is locked and has a difficulty, it will be displayed as (D:50). IE Door(D:50) has a lockpick difficulty of 50.
    ")]

    public class DungeonMaps : ToolBase {
        private DxTexture mapTexture;
        private DxTexture labelsTexture;
        private DxTexture compassTexture;
        private DxHud hud;
        private TimeSpan mapUpdateInterval = TimeSpan.FromMilliseconds(1000 / 30);
        private DateTime lastDraw = DateTime.MinValue;
        private uint currentLandblock = 0;
        private bool isManualLoad;
        private Dungeon dungeon;
        private long lastDrawDuration = 0;
        private Rectangle tileRect = new Rectangle(0,0,10 * TextureCache.TileScale,10 * TextureCache.TileScale);
        private string fontFace;
        private int fontWeight;
        private float scale = 3.0f;
        private bool needsMapDraw = true;
        private double drawZ = 0f;
        private double lastPlayerZ = 0.0f;

        private Dictionary<int, DxTexture> zLayerCache = new Dictionary<int, DxTexture>();
        private Dictionary<int, DxTexture> dynamicZLayerCache = new Dictionary<int, DxTexture>();
        private List<int> visitedTiles = new List<int>();

        HudTabView UIMapNotebook;
        HudButton UIFollowCharacter;
        HudHSlider UIOpacitySlider;
        HudHSlider UIZSlider;
        HudList UIDungeonList;
        HudTextBox UISearch;
        HudCheckBox UIShowAllLayers;
        private bool needsClear = false;
        private bool isPanning = false;
        private bool isFollowingCharacter = true;
        private double rotationStart;
        private float dragOffsetX;
        private float dragOffsetY;
        private float dragOffsetStartX;
        private float dragOffsetStartY;
        private int dragStartX;
        private int dragStartY;
        private double mapRotation;
        private float centerX;
        private float centerY;
        private System.Timers.Timer zoomSaveTimer;
        private bool isRunning = false;
        private bool isPortaling = true;

        Dictionary<int, TrackedObject> trackedObjects = new Dictionary<int, TrackedObject>();
        private int spawnId;
        private bool isRotating;
        private bool needsNewHud;
        private int landcell = 0;

        #region Config
        public const int MIN_ZOOM = 0;
        public const int MAX_ZOOM = 16;
        private const double Z_REDRAW_DISTANCE = 0.5f;

        [Summary("Enabled")]
        [Hotkey("DungeonMaps", "Toggle DungeonMaps display")]
        public readonly Setting<bool> Enabled = new Setting<bool>(true);

        [Summary("Show debug information while drawing maps")]
        public readonly Setting<bool> Debug = new Setting<bool>(false);

        [Summary("Draw dungeon maps even when map window is closed")]
        public readonly Setting<bool> DrawWhenClosed = new Setting<bool>(true);

        [Summary("Show visited tiles")]
        public readonly Setting<bool> ShowVisitedTiles = new Setting<bool>(true);

        [Summary("Visited tiles tint color")]
        public readonly Setting<int> VisitedTilesColor = new Setting<int>(-26881);

        [Summary("Show compass")]
        public readonly Setting<bool> ShowCompass = new Setting<bool>(true);

        [Summary("Map opacity")]
        public readonly Setting<int> Opacity = new Setting<int>(16);

        [Summary("Map zoom level")]
        public readonly Setting<float> MapZoom = new Setting<float>(4.2f);

        [Summary("Map Window X")]
        public readonly CharacterState<int> MapWindowX = new CharacterState<int>(40);

        [Summary("Map Window Y")]
        public readonly CharacterState<int> MapWindowY = new CharacterState<int>(200);

        [Summary("Map Window width")]
        public readonly CharacterState<int> MapWindowWidth = new CharacterState<int>(320);

        [Summary("Map Window height")]
        public readonly CharacterState<int> MapWindowHeight = new CharacterState<int>(280);

        [Summary("Label Font Size")]
        public readonly Setting<int> LabelFontSize = new Setting<int>(10);

        [Summary("Map display options")]
        public readonly MapDisplayOptions Display = new MapDisplayOptions();

        #region MapDisplayOptions
        [Summary("DungeonMaps display options")]
        public class MapDisplayOptions : ISetting {
            public static List<string> TileOptions = new List<string>() {
                "Walls",
                "InnerWalls",
                "RampedWalls",
                "Stairs",
                "Floors"
            };
            
            public static List<string> ValidSettings = new List<string>() {
                "Walls",
                "InnerWalls",
                "RampedWalls",
                "Stairs",
                "Floors",
                "VisualNavLines",
                "VisualNavStickyPoint"
            };

            [Summary("Dungeon name header")]
            public readonly ColorToggleOption DungeonName = new ColorToggleOption(true, -1);

            [Summary("Walls")]
            public readonly ColorToggleOption Walls = new ColorToggleOption(true, -16777089);

            [Summary("Inner wall")]
            public readonly ColorToggleOption InnerWalls = new ColorToggleOption(true, -8404993);

            [Summary("Ramped wall")]
            public readonly ColorToggleOption RampedWalls = new ColorToggleOption(true, -11622657);

            [Summary("Stairs")]
            public readonly ColorToggleOption Stairs = new ColorToggleOption(true, -16760961);

            [Summary("Floor")]
            public readonly ColorToggleOption Floors = new ColorToggleOption(true, -16744513);

            [Summary("VisualNav sticky point")]
            public readonly ColorToggleOption VisualNavStickyPoint = new ColorToggleOption(true, -5374161);

            [Summary("VisualNav lines")]
            public readonly ColorToggleOption VisualNavLines = new ColorToggleOption(true, -65281);

            [Summary("Marker display options")]
            public readonly MarkerDisplayOptions Markers = new MarkerDisplayOptions();

            public MapDisplayOptions() {

            }
        }
        #endregion

        #region MarkerDisplayOptions
        [Section("DungeonMaps marker display options")]
        public class MarkerDisplayOptions : ISetting {
            public static List<string> ValidSettings = new List<string>() {
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
            public readonly MarkerToggleOption You = new MarkerToggleOption(true, false, false, -65536, 3);

            [Summary("Others")]
            public readonly MarkerToggleOption Others = new MarkerToggleOption(true, true, true, -1, 3);

            [Summary("Items")]
            public readonly MarkerToggleOption Items = new MarkerToggleOption(true, true, true, -1, 3);

            [Summary("Monsters")]
            public readonly MarkerToggleOption Monsters = new MarkerToggleOption(true, true, false, -23296, 3);

            [Summary("NPCs")]
            public readonly MarkerToggleOption NPCs = new MarkerToggleOption(true, true, false, -256, 3);

            [Summary("My Corpse")]
            public readonly MarkerToggleOption MyCorpse = new MarkerToggleOption(true, true, true, -65536, 3);

            [Summary("Other Corpses")]
            public readonly MarkerToggleOption OtherCorpses = new MarkerToggleOption(false, true, false, -657931, 3);

            [Summary("Portals")]
            public readonly MarkerToggleOption Portals = new MarkerToggleOption(true, true, true, -3841, 3);

            [Summary("Containers")]
            public readonly MarkerToggleOption Containers = new MarkerToggleOption(true, true, false, -744352, 3);

            [Summary("Doors")]
            public readonly MarkerToggleOption Doors = new MarkerToggleOption(true, false, false, -5952982, 3);

            [Summary("Everything Else")]
            public readonly MarkerToggleOption EverythingElse = new MarkerToggleOption(false, true, false, -657931, 3);

            public MarkerDisplayOptions() {

            }

            public int GetMarkerColor(TrackedObject obj) {
                var propName = GetMarkerNameFromTO(obj);
                var opt = (MarkerToggleOption)((ISetting)this.GetFieldValue(propName)).GetValue();

                return opt == null ? Color.White.ToArgb() : opt.Color;
            }

            public bool ShouldShowlabel(TrackedObject wo) {
                var propName = GetMarkerNameFromTO(wo);
                var opt = (MarkerToggleOption)((ISetting)this.GetFieldValue(propName)).GetValue();

                return opt == null ? false : opt.ShowLabel;
            }

            internal bool ShouldDraw(TrackedObject wo) {
                var propName = GetMarkerNameFromTO(wo);
                var opt = (MarkerToggleOption)((ISetting)this.GetFieldValue(propName)).GetValue();

                return opt == null ? false : opt.Enabled;
            }

            internal int GetLabelColor(TrackedObject wo) {
                var propName = GetMarkerNameFromTO(wo);
                var opt = (MarkerToggleOption)((ISetting)this.GetFieldValue(propName)).GetValue();

                return opt == null ? Color.White.ToArgb() : opt.Color;
            }

            internal bool ShouldUseIcon(TrackedObject wo) {
                var propName = GetMarkerNameFromTO(wo);
                var opt = (MarkerToggleOption)((ISetting)this.GetFieldValue(propName)).GetValue();

                return opt == null ? true : opt.UseIcon;
            }

            internal int GetSize(TrackedObject wo) {
                var propName = GetMarkerNameFromTO(wo);
                var opt = (MarkerToggleOption)((ISetting)this.GetFieldValue(propName)).GetValue();

                return opt == null ? 4 : opt.Size;
            }

            public string GetMarkerNameFromTO(TrackedObject obj) {
                // check marker display settings
                switch (obj.ObjectClass) {
                    case ObjectClass.Player:
                        if (obj.Id == UtilityBeltPlugin.Instance.Core.CharacterFilter.Id) {
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
                        if (obj.Name == $"Corpse of {UtilityBeltPlugin.Instance.Core.CharacterFilter.Name}") {
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
                        //if ((wo.Values(LongValueKey.Behavior, 0) & (int)BehaviorFlag.Stuck) == 0) {
                        //    return "Items";
                        //}

                        return "EverythingElse";
                }
            }
        }
        #endregion
        #endregion

        public DungeonMaps(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        public override void Init() {
            base.Init();

            zoomSaveTimer = new System.Timers.Timer {
                AutoReset = true,
                Interval = 2000 // save the window position 2 seconds after it has stopped moving
            };

            UIMapNotebook = (HudTabView)UB.DungeonMapView.view["MapNotebook"];
            UISearch = (HudTextBox)UB.DungeonMapView.view["Search"];
            UIDungeonList = (HudList)UB.DungeonMapView.view["DungeonList"];
            UIFollowCharacter = (HudButton)UB.DungeonMapView.view["FollowCharacter"];
            UIOpacitySlider = (HudHSlider)UB.DungeonMapView.view["OpacitySlider"];
            UIZSlider = (HudHSlider)UB.DungeonMapView.view["ZSlider"];
            UIShowAllLayers = (HudCheckBox)UB.DungeonMapView.view["ShowAllLayers"];

            UIOpacitySlider.Changed += UIOpacitySlider_Changed;
            UIOpacitySlider.Position = Opacity;
            UIZSlider.Changed += UIZSlider_Changed;
            UIFollowCharacter.Hit += UIFollowCharacter_Hit;
            UISearch.Change += UISearch_Change;
            UIDungeonList.Click += UIDungeonList_Click;

            UB.DungeonMapView.view["DungeonMapsRenderContainer"].MouseEvent += DungeonMaps_MouseEvent;
            UIShowAllLayers.Change += UIShowAllLayers_Change;

            scale = (5f - MapZoom);
            fontFace = (string)((HudControl)UB.DungeonMapView.view.MainControl).Theme.GetVal<string>("DefaultTextFontFace");
            fontWeight = (int)((HudControl)UB.DungeonMapView.view.MainControl).Theme.GetVal<int>("ViewTextFontWeight");

            CreateHud(MapWindowWidth, MapWindowHeight, MapWindowX, MapWindowY);
            
            zoomSaveTimer.Elapsed += (s, e) => {
                zoomSaveTimer.Stop();
                MapZoom.Value = (5f - scale);
            };

            if (UBHelper.Core.GameState != UBHelper.GameState.In_Game) {
                UB.Core.CharacterFilter.LoginComplete += CharacterFilter_LoginComplete;
            }

            Opacity.Changed += DungeonMaps_PropertyChanged;
            DrawWhenClosed.Changed += DungeonMaps_PropertyChanged;
            Enabled.Changed += DungeonMaps_PropertyChanged;

            UIOpacitySlider.Position = Opacity;

            UpdateDungeonList();
        }

        #region Event Handlers
        private void DungeonMaps_PropertyChanged(object sender, SettingChangedEventArgs e) {
            switch (e.PropertyName) {
                case "Opacity":
                    return;
                case "DrawWhenClosed":
                case "Enabled":
                    UB.DungeonMapView.ResizeMapHud();
                    break;
            }
            if (Enabled)
                needsClear = true;
        }

        private void UISearch_Change(object sender, EventArgs e) {
            try {
                UpdateDungeonList();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UIShowAllLayers_Change(object sender, EventArgs e) {
            try {
                needsClear = true;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UIDungeonList_Click(object sender, int row, int col) {
            try {
                if (int.TryParse(((HudStaticText)UIDungeonList[row][0]).Text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int lb)) {
                    mapRotation = 0;
                    LoadLandblock(lb << 16);
                    UIMapNotebook.CurrentTab = 0;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void VisualNav_NavChanged(object sender, EventArgs e) {
            try {
                needsClear = true;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void VisualNav_NavUpdated(object sender, EventArgs e) {
            try {
                if (!Display.VisualNavLines.Enabled || UB.VisualNav.currentRoute == null || UB.VisualNav.currentRoute.NavType != Lib.VTNav.eNavType.Once) return;

                var z = (int)(Math.Floor((PhysicsObject.GetPosition(UB.Core.CharacterFilter.Id).Z + 3) / 6) * 6);
                var lastZ = (int)(Math.Floor((lastPlayerZ + 3) / 6) * 6);

                if (zLayerCache.ContainsKey(z)) {
                    zLayerCache[z].Dispose();
                    zLayerCache.Remove(z);
                }

                if (z != lastZ && zLayerCache.ContainsKey(lastZ)) {
                    zLayerCache[lastZ].Dispose();
                    zLayerCache.Remove(lastZ);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UIFollowCharacter_Hit(object sender, EventArgs e) {
            try {
                isFollowingCharacter = true;
                isManualLoad = false;
                if ((UB.Core.Actions.Landcell & 0xFFFF0000) != currentLandblock) {
                    LoadLandblock(UB.Core.Actions.Landcell);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UIOpacitySlider_Changed(int min, int max, int pos) {
            try {
                Opacity.Value = pos;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UIZSlider_Changed(int min, int max, int pos) {
            try {
                if (dungeon != null) {
                    drawZ = (double)dungeon.minZ + ((double)dungeon.Depth * ((double)pos/100.0));
                    if (isFollowingCharacter) {
                        dragOffsetX = (float)UB.Core.Actions.LocationX;
                        dragOffsetY = -(float)UB.Core.Actions.LocationY;
                        dragOffsetStartX = dragOffsetX;
                        dragOffsetStartY = dragOffsetY;
                    }
                    isFollowingCharacter = false;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void DungeonMaps_MouseEvent(object sender, VirindiViewService.Controls.ControlMouseEventArgs e) {
            try {
                switch (e.EventType) {
                    case ControlMouseEventArgs.MouseEventType.MouseWheel:
                        if ((e.WheelAmount > 0 && scale < 5) || (e.WheelAmount < 0 && scale > 0.3)) {
                            zoomSaveTimer.Stop();
                            zoomSaveTimer.Start();
                            // todo: smooth zooming steps
                            if (e.WheelAmount < 0) {
                                scale -= 0.2f;
                            }
                            else {
                                scale += 0.2f;
                            }
                            hud.ZPriority = 9999;
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
                        isFollowingCharacter = false;

                        if ((Control.ModifierKeys & Keys.Shift) == Keys.Shift) {
                            isRotating = true;
                            rotationStart = mapRotation;
                        }
                        else {
                            isPanning = true;
                        }
                        break;

                    case ControlMouseEventArgs.MouseEventType.MouseUp:
                        isPanning = false;
                        isRotating = false;
                        break;

                    case ControlMouseEventArgs.MouseEventType.MouseMove:
                        if (isPanning) {
                            var angle =  mapRotation - (Math.Atan2(e.Y - dragStartY, dragStartX - e.X) * 180.0 / Math.PI);
                            var distance = Math.Sqrt(Math.Pow(dragStartX - e.X, 2) + Math.Pow(dragStartY - e.Y, 2));
                            var np = Util.MovePoint(new PointF(dragOffsetStartX, dragOffsetStartY), angle, distance / scale / TextureCache.TileScale);

                            dragOffsetX = np.X;
                            dragOffsetY = np.Y;
                        }
                        if (isRotating) {
                            var distance = dragStartX - e.X + dragStartY - e.Y;
                            mapRotation = rotationStart + distance;
                        }
                        break;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void CharacterFilter_LoginComplete(object sender, EventArgs e) {
            try {
                UB.Core.CharacterFilter.LoginComplete -= CharacterFilter_LoginComplete;
                CreateHud(MapWindowWidth, MapWindowHeight, MapWindowX, MapWindowY);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Core_RegionChange3D(object sender, Decal.Adapter.RegionChange3DEventArgs e) {
            try {
                needsNewHud = true;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void EchoFilter_ServerDispatch(object sender, Decal.Adapter.NetworkMessageEventArgs e) {
            try {
                if (e.Message.Type == 0xF747) {
                    var id = e.Message.Value<int>("object");
                    if (trackedObjects.ContainsKey(id)) {
                        RemoveTrackedObject(id);
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Core_RenderFrame(object sender, EventArgs e) {
            try {
                if (DateTime.UtcNow - lastDraw < mapUpdateInterval)
                    return;
                lastDraw = DateTime.UtcNow;

                if (dungeon == null) {
                    if (Debug == true && hud != null && !hud.Texture.IsDisposed && landcell != UB.Core.Actions.Landcell) {
                        landcell = UB.Core.Actions.Landcell;
                        hud.Texture.Clear();
                        try {
                            hud.Texture.BeginRender();
                            hud.Texture.Fill(new Rectangle(0, 0, hud.Texture.Width, hud.Texture.Height), Color.Transparent);
                            try {
                                hud.Texture.BeginText(fontFace, 10f, 150, false, 1, (int)byte.MaxValue);
                                if (Display.DungeonName.Enabled) {
                                    hud.Texture.WriteText(UB.Core.Actions.Landcell.ToString("X8"), Color.FromArgb(Display.DungeonName.Color), VirindiViewService.WriteTextFormats.Center, new Rectangle(0, 0, hud.Texture.Width, 20));
                                }
                            }
                            finally {
                                hud.Texture.EndText();
                            }
                        }
                        catch (Exception ex) { Logger.LogException(ex); }
                        finally { hud.Texture.EndRender(); }
                    }
                    return;
                }

                if (isFollowingCharacter) {
                    drawZ = UB.Core.Actions.LocationZ;
                }

                var watch = System.Diagnostics.Stopwatch.StartNew();
                if (Math.Abs(lastPlayerZ - drawZ) > Z_REDRAW_DISTANCE) {
                    lastPlayerZ = drawZ;
                    needsMapDraw = true;
                    UIZSlider.Position = (int)(dungeon.GetZLevelIndex(drawZ) * 100);
                }

                if (!visitedTiles.Contains(UB.Core.Actions.Landcell)) {
                    visitedTiles.Add(UB.Core.Actions.Landcell);
                    var z = (int)(Math.Floor((PhysicsObject.GetPosition(UB.Core.CharacterFilter.Id).Z + 3) / 6) * 6);
                    if (zLayerCache.ContainsKey(z)) {
                        zLayerCache[z].Dispose();
                        zLayerCache.Remove(z);
                    }
                }

                UpdateTrackedObjects();
                RenderMapTexture();
                RenderHud();

                watch.Stop();
                lastDrawDuration = watch.ElapsedTicks;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void CharacterFilter_ChangePortalMode(object sender, ChangePortalModeEventArgs e) {
            try {
                isPortaling = (e.Type == PortalEventType.EnterPortal);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void WorldFilter_CreateObject(object sender, CreateObjectEventArgs e) {
            try {
                if (e.New.Id == UB.Core.CharacterFilter.Id) return;
                if (trackedObjects.ContainsKey(e.New.Id)) return;
                if (e.New.Values(LongValueKey.Wielder, 0) != 0) return;

                trackedObjects.Add(e.New.Id, new TrackedObject(e.New.Id));
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Core_RadarUpdate(double uptime) {
            try {
                if (needsNewHud) {
                    needsNewHud = false;
                    isRunning = false;
                    CreateHud(MapWindowWidth, MapWindowHeight, MapWindowX, MapWindowY);
                }

                if (isPortaling && (UB.Core.Actions.Landcell & 0xFFFF0000) != currentLandblock) {
                    if (isFollowingCharacter)
                        LoadLandblock(UB.Core.Actions.Landcell, true);
                    else
                        isManualLoad = true;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void WorldFilter_ChangeObject(object sender, ChangeObjectEventArgs e) {
            try {
                if (e.Change == WorldChangeType.StorageChange && trackedObjects.ContainsKey(e.Changed.Id)) {
                    if (e.Changed.Container != 0) {
                        RemoveTrackedObject(e.Changed.Id);
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        #endregion

        #region misc
        private void UpdateDungeonList() {
            var search = UISearch.Text.ToLower();
            UIDungeonList.ClearRows();
            foreach (var kv in Dungeon.DungeonNames) {
                var lb = (((uint)kv.Key >> 16)).ToString("X4");
                if (lb.ToLower().Contains(search) || kv.Value.ToLower().Contains(search)) {
                    var row = UIDungeonList.AddRow();
                    ((HudStaticText)row[0]).Text = lb;
                    ((HudStaticText)row[1]).Text = kv.Value;
                }
            }
        }
        private void UpdateTrackedObjects() {
            var dirtyLayers = new List<int>();
            var dirtyDynamicLayers = new List<int>();
            var keys = trackedObjects.Keys.ToArray();
            foreach (var to in keys) {
                if (trackedObjects[to].Update(false)) {
                    if (trackedObjects[to].Static) {
                        if (!dirtyLayers.Contains(trackedObjects[to].ZLayer)) dirtyLayers.Add(trackedObjects[to].ZLayer);
                        if (!dirtyLayers.Contains(trackedObjects[to].PrevZLayer)) dirtyLayers.Add(trackedObjects[to].PrevZLayer);
                    }
                    else {
                        if (!dirtyDynamicLayers.Contains(trackedObjects[to].ZLayer)) dirtyDynamicLayers.Add(trackedObjects[to].ZLayer);
                        if (!dirtyDynamicLayers.Contains(trackedObjects[to].PrevZLayer)) dirtyDynamicLayers.Add(trackedObjects[to].PrevZLayer);
                    }

                    if (trackedObjects[to].IsDisposed) trackedObjects.Remove(to);
                }
            }

            if (dirtyLayers.Count > 0) {
                needsMapDraw = true;
                foreach (var z in dirtyLayers) {
                    if (zLayerCache.ContainsKey(z)) {
                        zLayerCache[z].Dispose();
                        zLayerCache.Remove(z);
                    }
                }
            }

            if (dirtyDynamicLayers.Count > 0) {
                needsMapDraw = true;
                foreach (var z in dirtyDynamicLayers) {
                    if (dynamicZLayerCache.ContainsKey(z)) {
                        dynamicZLayerCache[z].Dispose();
                        dynamicZLayerCache.Remove(z);
                    }
                }
            }
        }

        private void RemoveTrackedObject(int id) {
            if (!trackedObjects.ContainsKey(id))
                return;

            if (trackedObjects[id].Static) {
                if (zLayerCache.ContainsKey(trackedObjects[id].ZLayer)) {
                    zLayerCache[trackedObjects[id].ZLayer].Dispose();
                    zLayerCache.Remove(trackedObjects[id].ZLayer);
                }
            }
            else {
                if (dynamicZLayerCache.ContainsKey(trackedObjects[id].ZLayer)) {
                    dynamicZLayerCache[trackedObjects[id].ZLayer].Dispose();
                    dynamicZLayerCache.Remove(trackedObjects[id].ZLayer);
                }
            }
            trackedObjects[id].Dispose();
            trackedObjects.Remove(id);
            needsMapDraw = true;
        }

        private int GetTint(int zLayer) {
            if (UIShowAllLayers.Checked) return -1;

            // floors directly above your character
            if (drawZ - zLayer < -3) {
                return Color.FromArgb(121, 151, 151, 151).ToArgb();
            }
            // current floor
            else if (Math.Abs(drawZ - zLayer) < 3) {
                return -1;
            }
            // floors below
            else {
                var d = (int)(Math.Min(1, (Math.Abs(drawZ - zLayer) / 6f) * 0.4f) * 255);
                return Color.FromArgb(255, 255 - d, 255 - d, 255 - d).ToArgb();
            }
        }

        private void LoadLandblock(int landcell, bool loadExisting=false) {
            var dungeonWatch = System.Diagnostics.Stopwatch.StartNew();
            currentLandblock = (uint)(landcell & 0xFFFF0000);
            isManualLoad = (UB.Core.Actions.Landcell & 0xFFFF0000) != currentLandblock;
            dungeon = Dungeon.GetCached(landcell);

            ClearCache(true);
            TrackedObject.Clear();
            visitedTiles.Clear();
            if (mapTexture != null) mapTexture.Dispose();
            if (labelsTexture != null) labelsTexture.Dispose();

            if (!isManualLoad && !dungeon.IsDungeon()) {
                dungeon = null;
                ClearHud();
            }
            else if (dungeon.Width > 0 && dungeon.Height > 0) {
                dungeon.LoadCells();
                mapTexture = new DxTexture(new Size(dungeon.Width * TextureCache.TileScale, dungeon.Height * TextureCache.TileScale));
                labelsTexture = new DxTexture(new Size(dungeon.Width * TextureCache.TileScale, dungeon.Height * TextureCache.TileScale));

                if (!isManualLoad) {
                    using (var landscape = CoreManager.Current.WorldFilter.GetLandscape()) {
                        foreach (var wo in landscape) {
                            if (!CoreManager.Current.Actions.IsValidObject(wo.Id)) continue;
                            if (trackedObjects.ContainsKey(wo.Id)) continue;
                            int wolc = 0;
                            try {
                                wolc = PhysicsObject.GetLandcell(wo.Id);
                            }
                            catch { }
                            if (wolc == 0)
                                continue;
                            if ((wolc & 0xFFFF0000) == currentLandblock) {
                                trackedObjects.Add(wo.Id, new TrackedObject(wo.Id));
                            }
                        }
                    }

                    if (!trackedObjects.ContainsKey(UB.Core.CharacterFilter.Id)) {
                        trackedObjects.Add(UB.Core.CharacterFilter.Id, new TrackedObject(UB.Core.CharacterFilter.Id));
                    }
                    drawZ = UB.Core.Actions.LocationZ;
                    UIZSlider.Position = (int)(dungeon.GetZLevelIndex(drawZ) * 100);
                }
                else {
                    isFollowingCharacter = false;
                    drawZ = dungeon.maxZ;
                    UIZSlider.Position = 100;
                    dragOffsetX = dungeon.minX + (dungeon.Width / 2);
                    dragOffsetY = -(dungeon.minY + (dungeon.Height / 2));
                    dragOffsetStartX = dragOffsetX;
                    dragOffsetStartY = dragOffsetY;
                }

                if (UB.LSD.EnsureLandblockSpawnsReady(currentLandblock)) {
                    LoadLSDObjects();
                }
                else {
                    UB.LSD.DataUpdated += LSD_DataUpdated;
                }
            }

            dungeonWatch.Stop();
            LogDebug($"Took {dungeonWatch.ElapsedTicks / 10000f:N4}ms to check landblock {currentLandblock:X8}");
        }

        private void LoadLSDObjects() {
            var lb = UB.Database.Landblocks.Include(new string[] { "$.Weenies[*].Weenie" }).FindById((int)currentLandblock);
            if (lb == null)
                return;

            var startSpawnId = spawnId;
            var lbNeedsUpdate = false;
            foreach (var spawn in lb.Weenies) {
                if (spawn.Weenie == null) {
                    spawn.Weenie = UB.Database.Weenies.FindById(spawn.Wcid);
                    if (spawn.Weenie == null)
                        LogDebug($"Weenie {spawn.Wcid}({spawn.Description}) needs downloading");
                    else
                        lbNeedsUpdate = true;
                }
                if (spawn.Weenie == null || (!isManualLoad && spawn.Weenie.GetObjectClass() == ObjectClass.Monster))
                    continue;
                trackedObjects.Add(++spawnId, new TrackedObject(spawn, lb));
            }

            if (lbNeedsUpdate)
                UB.Database.Landblocks.Update(lb);

            needsClear = true;
            needsMapDraw = true;
        }

        private void LSD_DataUpdated(object sender, DataUpdatedEventArgs e) {
            try {
                UB.LSD.DataUpdated -= LSD_DataUpdated;
                LoadLSDObjects();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void ClearCache(bool clearTracked=false) {
            foreach (var kp in zLayerCache) {
                kp.Value.Dispose();
            }
            zLayerCache.Clear();

            foreach (var kp in dynamicZLayerCache) {
                kp.Value.Dispose();
            }
            dynamicZLayerCache.Clear();

            if (clearTracked) {
                foreach (var to in trackedObjects) {
                    to.Value.Dispose();
                }
                trackedObjects.Clear();
            }
            TextureCache.Clear();
        }
        #endregion

        #region Hud
        private void ClearHud() {
            if (hud == null || hud.Texture == null || hud.Texture.IsDisposed)
                return;
            try {
                hud.Texture.BeginRender();
                hud.Texture.Fill(new Rectangle(0, 0, hud.Texture.Width, hud.Texture.Height), Color.Transparent);
            }
            catch (Exception ex) { Logger.LogException(ex); }
            finally { hud.Texture.EndRender(); }
        }

        internal void CreateHud(int width, int height, int x, int y) {
            try {
                if (hud != null) {
                    ClearHud();
                    hud.Dispose();
                }

                if (!Enabled || (!DrawWhenClosed && !UB.DungeonMapView.view.Visible)) {
                    ClearCache();
                    return;
                }

                if (isRunning) {
                    UB.VisualNav.NavChanged -= VisualNav_NavChanged;
                    UB.VisualNav.NavUpdated -= VisualNav_NavUpdated;
                    UB.Core.RenderFrame -= Core_RenderFrame;
                    UB.Core.RegionChange3D -= Core_RegionChange3D;
                    UB.Core.EchoFilter.ServerDispatch -= EchoFilter_ServerDispatch;
                    UB.Core.CharacterFilter.ChangePortalMode -= CharacterFilter_ChangePortalMode;
                    UB.Core.WorldFilter.CreateObject -= WorldFilter_CreateObject;
                    UB.Core.WorldFilter.ChangeObject -= WorldFilter_ChangeObject;
                    UBHelper.Core.RadarUpdate -= Core_RadarUpdate;
                    isRunning = false;
                }

                int landcell = 0;
                try {
                    landcell = UB.Core.Actions.Landcell;
                }
                catch { }
                if (landcell == 0) return;

                UB.VisualNav.NavChanged += VisualNav_NavChanged;
                UB.VisualNav.NavUpdated += VisualNav_NavUpdated;
                UB.Core.RenderFrame += Core_RenderFrame;
                UB.Core.RegionChange3D += Core_RegionChange3D;
                UB.Core.EchoFilter.ServerDispatch += EchoFilter_ServerDispatch;
                UB.Core.CharacterFilter.ChangePortalMode += CharacterFilter_ChangePortalMode;
                UB.Core.WorldFilter.CreateObject += WorldFilter_CreateObject;
                UB.Core.WorldFilter.ChangeObject += WorldFilter_ChangeObject;
                UBHelper.Core.RadarUpdate += Core_RadarUpdate;

                hud = new DxHud(new Point(x + 5, y + 69), new Size(width - 10, height - 74), 0);
                hud.Enabled = true;
                isRunning = true;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        #endregion

        #region Rendering
        private void RenderHud() {
            if (hud == null || hud.Texture == null || hud.Texture.IsDisposed || mapTexture == null || mapTexture.IsDisposed) return;

            if (UB.DungeonMapView?.view == null || (UB.DungeonMapView.view.Visible && UIMapNotebook.CurrentTab != 0)) {
                hud.Enabled = false;
                return;
            }
            hud.Enabled = true;

            if (UIShowAllLayers.Checked) {
                foreach (var key in TrackedObject.ByZLayer.Keys) {
                    PrecacheLabels(key);
                }
            }
            else {
                PrecacheLabels((int)(Math.Floor((drawZ + 3) / 6) * 6));
            }

            if (ShowCompass && compassTexture == null) {
                LoadCompassTexture();
            }

            try {
                hud.Texture.BeginRender();
                hud.Texture.Fill(new Rectangle(0, 0, hud.Texture.Width, hud.Texture.Height), Color.FromArgb(0, 111, 111, 111));

                Matrix transform = new Matrix();
                float sx, sy;
                Quaternion rotQuat;

                if (isFollowingCharacter) {
                    mapRotation = UB.Core.Actions.Heading;
                    centerX = (float)((dungeon.Width - 5 - UB.Core.Actions.LocationX) * TextureCache.TileScale) * scale;
                    centerY = (float)((dungeon.Height - 5 + UB.Core.Actions.LocationY) * TextureCache.TileScale) * scale;
                }
                else {
                    centerX = (float)((dungeon.Width - 5 -dragOffsetX) * TextureCache.TileScale) * scale;
                    centerY = (float)((dungeon.Height - 5 + -dragOffsetY) * TextureCache.TileScale) * scale;
                }

                rotQuat = Geometry.HeadingToQuaternion((float)mapRotation - 180f);
                sx = -(centerX - (hud.Texture.Width / 2));
                sy = -(centerY - (hud.Texture.Height / 2));

                var rotationCenter = new Vector3(centerX, centerY, 0);
                var tint = Color.FromArgb((int)((Opacity / 20f) * 255), 255, 255, 255).ToArgb();

                transform.AffineTransformation(scale, rotationCenter, rotQuat, new Vector3(sx, sy, 0));
                hud.Texture.DrawTextureWithTransform(mapTexture, transform, tint);

                if (ShowCompass && compassTexture != null && !compassTexture.IsDisposed) {
                    hud.Texture.DrawTextureRotated(compassTexture, new Rectangle(0, 0, compassTexture.Width, compassTexture.Height), new Point(hud.Texture.Width - (compassTexture.Width / 2), (compassTexture.Height / 2)), tint, (float)((360f - mapRotation) * (Math.PI / 180)));
                }

                if (UIShowAllLayers.Checked) {
                    foreach (var key in TrackedObject.ByZLayer.Keys) {
                        DrawLabels(key);
                    }
                }
                else {
                    DrawLabels((int)(Math.Floor((drawZ + 3) / 6) * 6));
                }

                if (isManualLoad)
                    DrawLegend();

                try {
                    hud.Texture.BeginText(fontFace, 10f, 150, false, 1, (int)byte.MaxValue);
                    if (Display.DungeonName.Enabled) {
                        var name = dungeon.Name + $" (Z:{(int)(Math.Floor((drawZ + 3) / 6))})";
                        if (Debug)
                            name += $" {UB.Core.Actions.Landcell:X8}";
                        hud.Texture.WriteText(name, Color.FromArgb(Display.DungeonName.Color), VirindiViewService.WriteTextFormats.Center, new Rectangle(0, 0, hud.Texture.Width, 20));
                    }
                    if (Debug) {
                        var sobjCount = 0;
                        foreach (var kv in TrackedObject.ByZLayer)
                            sobjCount += kv.Value.Count;
                        var text = $"Obj:{trackedObjects.Keys.Count} SObj:{sobjCount} Ico:{TextureCache.iconCache.Count} Tile:{TextureCache.tileCache.Count} Mark:{TextureCache.markerCache.Count} Text:{TextureCache.textCache.Count} - {lastDrawDuration:D8}";
                        hud.Texture.WriteText(text, Color.White, VirindiViewService.WriteTextFormats.Center, new Rectangle(0, hud.Texture.Height - 20, hud.Texture.Width, 20));
                    }
                }
                finally {
                    hud.Texture.EndText();
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
            finally {
                hud.Texture.EndRender();
            }
        }

        private void DrawLegend() {
            try {
                hud.Texture.BeginText(fontFace, 8f, fontWeight, false, 1, (int)byte.MaxValue);
                var offset = 0;
                foreach (var kv in TrackedObject.Legend) {
                    var icon = TextureCache.GetIcon(kv.Value.Icon);
                    if (icon == null)
                        continue;
                    hud.Texture.DrawTextureTinted(icon, new Rectangle(0, 0, icon.Width, icon.Height), new Rectangle(0, offset, 25, 25), kv.Value.Color.ToArgb());
                    hud.Texture.WriteText(kv.Value.Name,
                        kv.Value.Color,
                        VirindiViewService.WriteTextFormats.VerticalCenter,
                        new Rectangle(25, offset, 200, 25));
                    offset += 25;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
            finally {
                hud.Texture.EndText();
            }
        }

        private void LoadCompassTexture() {
            if (compassTexture != null)
                return;

            using (Stream manifestResourceStream = typeof(TextureCache).Assembly.GetManifestResourceStream($"UtilityBelt.Resources.icons.compass.png")) {
                if (manifestResourceStream != null) {
                    using (Bitmap bitmap = new Bitmap(manifestResourceStream)) {
                        bitmap.MakeTransparent(Color.White);
                        compassTexture = new DxTexture(new Size(bitmap.Width, bitmap.Height));
                        try {
                            compassTexture.BeginRender();
                            compassTexture.Fill(new Rectangle(0, 0, compassTexture.Width, compassTexture.Height), Color.Transparent);
                            compassTexture.DrawImage(bitmap, new Rectangle(0, 0, compassTexture.Width, compassTexture.Height), Color.White);
                        }
                        catch (Exception ex) { Logger.LogException(ex); }
                        finally {
                            compassTexture.EndRender();
                        }
                    }
                }
            }
        }

        private void PrecacheLabels(int z) {
            if (TrackedObject.ByZLayer.ContainsKey(z)) {
                if (!dungeon.ZLayers.ContainsKey(z)) return;
                var objs = TrackedObject.ByZLayer[z].ToArray();
                foreach (var obj in objs) {
                    if (!ShouldDrawLabel(obj))
                        continue;
                    Color color = Color.FromArgb(Display.Markers.GetMarkerColor(obj));

                    TextureCache.GetText(obj.Name, LabelFontSize, color, fontFace, fontWeight);
                }
            }
        }

        private void DrawLabels(int z) {
            if (TrackedObject.ByZLayer.ContainsKey(z)) {
                if (!dungeon.ZLayers.ContainsKey(z)) return; 

                try {
                    foreach (var obj in TrackedObject.ByZLayer[z]) {
                        if (!ShouldDrawLabel(obj))
                            continue;

                        Color color = Color.FromArgb(Display.Markers.GetMarkerColor(obj));
                        if (obj.IsLSD && obj.ObjectClass == ObjectClass.Monster) {
                                continue;
                        }
                        var textTexture = TextureCache.GetText(obj.Name, LabelFontSize, color, fontFace, fontWeight, 1, false);

                        if (textTexture == null) continue;
                        var tint = Color.FromArgb((int)((Opacity / 20f) * 255), 255, 255, 255).ToArgb();
                        float x, y;
                        if (isFollowingCharacter) {
                            x = (float)((obj.Position.X - UB.Core.Actions.LocationX) * scale * TextureCache.TileScale);
                            y = (float)((UB.Core.Actions.LocationY - obj.Position.Y) * scale * TextureCache.TileScale);
                        }
                        else {
                            x = (float)((obj.Position.X - dragOffsetX) * scale * TextureCache.TileScale);
                            y = (float)((-obj.Position.Y - dragOffsetY) * scale * TextureCache.TileScale);
                        }

                        PointF rotatedPoint = Util.RotatePoint(new PointF(x, y), new PointF(0, 0), 360f - mapRotation);
                        var transmat = new Matrix();
                        transmat.Translate(rotatedPoint.X - (textTexture.Width / 2) + (hud.Texture.Width / 2), rotatedPoint.Y - (textTexture.Height / 2) + (hud.Texture.Height / 2), 0);

                        hud.Texture.DrawTextureWithTransform(textTexture, transmat, tint);
                    }
                }
                catch (Exception ex) { Logger.LogException(ex); }
            }
        }

        private void RenderMapTexture() {
            if (hud == null || hud.Texture == null || hud.Texture.IsDisposed) return;
            if (mapTexture == null || mapTexture.IsDisposed) return;

            if (needsClear) {
                needsMapDraw = true;
                TextureCache.Clear();
                ClearCache(false);
                needsClear = false;
            }
            
            if (!needsMapDraw) return;

            needsMapDraw = false;
            foreach (var kp in dungeon.ZLayers) {
                // floors more than one level above or four levels below your char are not drawn
                if (!UIShowAllLayers.Checked && (drawZ - kp.Key < -10 || drawZ - kp.Key > 24)) {
                    continue;
                }

                if (zLayerCache.ContainsKey(kp.Key)) {
                    if (!dynamicZLayerCache.ContainsKey(kp.Key)) DrawDynamicZLayer(kp);
                    continue;
                }

                needsMapDraw = true;

                foreach (var cell in kp.Value.Cells) {
                    // precache tiles outside of render
                    TextureCache.GetTile(cell.EnvironmentId);
                }

                DrawZLayer(kp);
                DrawDynamicZLayer(kp);
            }

            try {
                mapTexture.BeginRender();
                if (Debug) {
                    mapTexture.Fill(new Rectangle(0, 0, mapTexture.Width, mapTexture.Height), Color.FromArgb(110, 255, 255, 0));
                }
                else {
                    mapTexture.Fill(new Rectangle(0, 0, mapTexture.Width, mapTexture.Height), Color.Transparent);
                }

                foreach (var kp in dungeon.ZLayers) {
                    // floors more than one level above or four levels below your char are not drawn
                    if (!UIShowAllLayers.Checked && (drawZ - kp.Key < -10 || drawZ - kp.Key > 24)) {
                        continue;
                    }

                    if (zLayerCache.ContainsKey(kp.Key)) {
                        mapTexture.DrawTextureTinted(zLayerCache[kp.Key], new Rectangle(
                            0, 0, zLayerCache[kp.Key].Width, zLayerCache[kp.Key].Height
                        ), new Rectangle(
                            kp.Value.OffsetX * TextureCache.TileScale,
                            kp.Value.OffsetY * TextureCache.TileScale,
                            kp.Value.Width * TextureCache.TileScale,
                            kp.Value.Height * TextureCache.TileScale
                        ), GetTint(kp.Key));
                    }
                    if (dynamicZLayerCache.ContainsKey(kp.Key)) {
                        mapTexture.DrawTextureTinted(dynamicZLayerCache[kp.Key], new Rectangle(
                            0, 0, dynamicZLayerCache[kp.Key].Width, dynamicZLayerCache[kp.Key].Height
                        ), new Rectangle(
                            kp.Value.OffsetX * TextureCache.TileScale,
                            kp.Value.OffsetY * TextureCache.TileScale,
                            kp.Value.Width * TextureCache.TileScale,
                            kp.Value.Height * TextureCache.TileScale
                        ), GetTint(kp.Key));
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
            finally { mapTexture.EndRender(); }
        }

        private void DrawDynamicZLayer(KeyValuePair<int, DungeonLayer> kp) {
            DxTexture texture;
            if (dynamicZLayerCache.ContainsKey(kp.Key)) {
                texture = dynamicZLayerCache[kp.Key];
            }
            else {
                texture = new DxTexture(new Size(kp.Value.Width * TextureCache.TileScale, kp.Value.Height * TextureCache.TileScale));
            }

            try {
                texture.BeginRender();
                texture.Fill(new Rectangle(0, 0, texture.Width, texture.Height), Color.Transparent);

                if (TrackedObject.ByZLayer.ContainsKey(kp.Key)) {
                    foreach (TrackedObject to in TrackedObject.ByZLayer[kp.Key]) {
                       if (!to.Static) DrawTrackedObject(to, kp.Key, texture);
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
            finally { texture.EndRender(); }

            if (!dynamicZLayerCache.ContainsKey(kp.Key))
                dynamicZLayerCache.Add(kp.Key, texture);
        }

        private void DrawZLayer(KeyValuePair<int, DungeonLayer> kp) {
            DxTexture zLayerTexture;
            if (zLayerCache.ContainsKey(kp.Key)) {
                zLayerTexture = zLayerCache[kp.Key];
            }
            else {
                zLayerTexture = new DxTexture(new Size(kp.Value.Width * TextureCache.TileScale, kp.Value.Height * TextureCache.TileScale));
            }

            try {
                zLayerTexture.BeginRender();
                if (Debug) {
                    zLayerTexture.Fill(new Rectangle(0, 0, zLayerTexture.Width, zLayerTexture.Height), Color.FromArgb(110, 255, 0, 255));
                }
                else {
                    zLayerTexture.Fill(new Rectangle(0, 0, zLayerTexture.Width, zLayerTexture.Height), Color.FromArgb(0, 255, 0, 255));
                }

                foreach (var cell in kp.Value.Cells) {
                    var x = ((kp.Value.Width - 10 - (cell.X - kp.Value.minX)) * TextureCache.TileScale);
                    var y = ((cell.Y - kp.Value.minY) * TextureCache.TileScale);
                    var tile = TextureCache.GetTile(cell.EnvironmentId);
                    var tilePoint = new Point(x + (TextureCache.TileScale * 5), y + (TextureCache.TileScale * 5));
                    var tint = visitedTiles.Contains(cell.Landcell) ? VisitedTilesColor : -1;
                    var transmat = new Matrix();
                    transmat.AffineTransformation(TextureCache.TileScale, new Vector3(5 * TextureCache.TileScale,5 * TextureCache.TileScale,0), Geometry.ToQuaternion(cell.R,0,0), new Vector3(x,y,0));

                    zLayerTexture.DrawTextureWithTransform(tile, transmat, tint);

                    if (Debug) {
                        try {
                            zLayerTexture.BeginText(fontFace, 7f, fontWeight, false, 1, (int)byte.MaxValue);
                            zLayerTexture.WriteText($"{cell.EnvironmentId}\n{cell.X},{cell.Y}\n{cell.Z}",
                                Color.White,
                                VirindiViewService.WriteTextFormats.Center | VirindiViewService.WriteTextFormats.VerticalCenter,
                                new Rectangle(x, y, tileRect.Width, tileRect.Height));
                        }
                        catch (Exception ex) { Logger.LogException(ex); }
                        finally {
                            zLayerTexture.EndText();
                        }
                    }
                }

                if (Debug) {
                    try {
                        zLayerTexture.BeginText(fontFace, 7f, fontWeight, false, 1, (int)byte.MaxValue);
                        zLayerTexture.WriteText($"Z: {kp.Value.roundedZ}",
                            Color.White,
                            VirindiViewService.WriteTextFormats.VerticalCenter,
                            new Rectangle(0, 0, zLayerTexture.Width, 10));
                    }
                    catch (Exception ex) { Logger.LogException(ex); }
                    finally {
                        zLayerTexture.EndText();
                    }
                }

                DrawMarkers(zLayerTexture, kp.Key);
                DrawNavLines(zLayerTexture, kp.Key);
                zLayerCache.Add(kp.Key, zLayerTexture);
            }
            catch (Exception ex) { Logger.LogException(ex); }
            finally {
                zLayerTexture.EndRender();
            }
        }

        private void DrawMarkers(DxTexture texture, int zLayer) {
            if (!TrackedObject.ByZLayer.ContainsKey(zLayer)) return;
            var tobjs = TrackedObject.ByZLayer[zLayer].ToArray();
            foreach (TrackedObject to in tobjs) {
                if (to.Static)
                    DrawTrackedObject(to, zLayer, texture);
            }
        }

        private bool ShouldDrawMarker(TrackedObject obj) {
            if (obj.IsLSD && isManualLoad)
                return obj.Type > 1;

            if (obj.IsLSD && obj.Type <= 1)
                return false;

            // make sure the client knows about this object
            var valid = UB.Core.Actions.IsValidObject(obj.Id);
            if ((!obj.IsLSD && !valid) || (isManualLoad && valid))
                return false;

            // too far?
            // TODO: get distance from physics object
            if (UB.Core.WorldFilter.Distance(obj.Id, UB.Core.CharacterFilter.Id) * 240 > 300) return false;

            return Display.Markers.ShouldDraw(obj);
        }

        private bool ShouldDrawLabel(TrackedObject obj) {
            if (obj.IsLSD && isManualLoad)
                return obj.Type > 1;

            if (obj.IsLSD && obj.Type <= 1)
                return false;

            return Display.Markers.ShouldShowlabel(obj) && ShouldDrawMarker(obj);
        }

        private void DrawTrackedObject(TrackedObject obj, int roundedZ, DxTexture texture) {
            try {
                if (!ShouldDrawMarker(obj))
                    return;

                if (obj.IsLSD && !isManualLoad && obj.ObjectClass == ObjectClass.Monster)
                    return;

                DungeonLayer zLayer = dungeon.ZLayers[roundedZ];
                bool shouldUseIcon = Display.Markers.ShouldUseIcon(obj);
                Color color = Color.FromArgb(Display.Markers.GetMarkerColor(obj));
                Color tintColor = Color.White;
                var size = Display.Markers.GetSize(obj) / 5f;
                if (TrackedObject.Legend.ContainsKey(obj.Wcid)) {
                    color = TrackedObject.Legend[obj.Wcid].Color;
                    tintColor = color;
                }

                float x = (float)((zLayer.Width - 5 - (obj.Position.X - zLayer.minX)) * TextureCache.TileScale);
                float y = (float)((obj.Position.Y + 5 - zLayer.minY) * TextureCache.TileScale);

                Matrix transform = new Matrix();

                DxTexture marker;
                if (shouldUseIcon && TextureCache.GetIcon(obj.Icon) != null) {
                    marker = TextureCache.GetIcon(obj.Icon);
                }
                else {
                    marker = TextureCache.GetMarker(color.ToArgb());
                }

                Quaternion q = Geometry.HeadingToQuaternion(180);
                transform.AffineTransformation(size, new Vector3((marker.Width * size / 2), (marker.Height * size / 2), 0), q, new Vector3(x - (marker.Width / 2) * size, y - (marker.Height / 2) * size, 0));
                texture.DrawTextureWithTransform(marker, transform, tintColor.ToArgb());
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        #region VisualNav
        private void DrawNavLines(DxTexture texture, int zLayer) {
            if (!Display.VisualNavLines.Enabled) return;

            var route = UB.VisualNav.currentRoute;
            if (route == null) return;

            switch (route.NavType) {
                case Lib.VTNav.eNavType.Circular:
                case Lib.VTNav.eNavType.Linear:
                case Lib.VTNav.eNavType.Once:
                    DrawPointRoute(texture, zLayer, route);
                    break;
            }
        }

        public void DrawPointRoute(DxTexture texture, int zLayer, Lib.VTNav.VTNavRoute route) {
            var allPoints = route.points.Where((p) => (p.Type == Lib.VTNav.eWaypointType.Point && p.index >= route.NavOffset)).ToArray();

            // todo: follow routes
            if (route.NavType == Lib.VTNav.eNavType.Target) return;
            var dz = dungeon.ZLayers[zLayer];

            // sticky point
            if (allPoints.Length == 1 && (route.NavType == Lib.VTNav.eNavType.Circular || route.NavType == Lib.VTNav.eNavType.Linear)) {
                if (!Display.VisualNavStickyPoint.Enabled) return;

                VTNPoint point = allPoints[0];
                var landblock = Geometry.GetLandblockFromCoordinates((float)point.EW, (float)point.NS);
                var pointOffset = Geometry.LandblockOffsetFromCoordinates(currentLandblock, (float)point.EW, (float)point.NS);
                var x = ((dz.Width - 10 - (pointOffset.X - dz.minX) + 5) * TextureCache.TileScale);
                var y = ((pointOffset.Y - dz.minY + 5) * TextureCache.TileScale);
                var pointZ = (point.Z * 240) + 1;
                bool pointIsOnActiveLayer = Math.Abs(pointZ - zLayer) < 4;

                if (!pointIsOnActiveLayer) 
                    return;

                texture.DrawPortalImage(100667897, new Rectangle((int)x - 15, (int)y - 15, 30, 30));
                return;
            }

            // circular / once / linear routes.. currently not discriminating
            if (!Display.VisualNavLines.Enabled) return;

            for (var i = route.NavOffset; i < route.points.Count; i++) {
                var point = route.points[i];
                var prev = point.GetPreviousPoint();

                if (prev == null) continue;

                if (point.Type == Lib.VTNav.eWaypointType.Point) {
                    var landblock = Geometry.GetLandblockFromCoordinates((float)point.EW, (float)point.NS);
                    var pointOffset = Geometry.LandblockOffsetFromCoordinates(currentLandblock, (float)point.EW, (float)point.NS);
                    var prevOffset = Geometry.LandblockOffsetFromCoordinates(currentLandblock, (float)prev.EW, (float)prev.NS);
                    var x = ((dz.Width - 10 - (pointOffset.X - dz.minX) + 5) * TextureCache.TileScale);
                    var y = ((pointOffset.Y - dz.minY + 5) * TextureCache.TileScale);
                    var px = ((dz.Width - 10 - (prevOffset.X - dz.minX) + 5) * TextureCache.TileScale);
                    var py = ((prevOffset.Y - dz.minY + 5) * TextureCache.TileScale);

                    // we pump these up a bit so they get drawn preferentially on a higher layer
                    // todo: this is still broken on ramps, some nav lines get drawn under the ramp tile
                    var prevZ = (prev.Z * 240)+1;
                    var pointZ = (point.Z * 240)+1;

                    bool prevIsOnActiveLayer = Math.Abs((prevZ - zLayer)) < 4;
                    bool pointIsOnActiveLayer = Math.Abs(pointZ - zLayer) < 4;

                    // skip if neither of the points fall on this layer
                    if (!prevIsOnActiveLayer && !pointIsOnActiveLayer) continue;

                    texture.DrawLine(new PointF(x, y), new PointF(px, py), Color.FromArgb(Display.VisualNavLines.Color), 5);
                }
            }
        }
        #endregion
        #endregion

        protected override void Dispose(bool disposing) {
            if (disposing) {
                Opacity.Changed += DungeonMaps_PropertyChanged;
                DrawWhenClosed.Changed += DungeonMaps_PropertyChanged;
                Enabled.Changed += DungeonMaps_PropertyChanged;

                if (isRunning) {
                    zoomSaveTimer.Stop();
                    UB.VisualNav.NavChanged -= VisualNav_NavChanged;
                    UB.VisualNav.NavUpdated -= VisualNav_NavUpdated;
                    UB.LSD.DataUpdated -= LSD_DataUpdated;
                    UB.Core.RenderFrame -= Core_RenderFrame;
                    UB.Core.RegionChange3D -= Core_RegionChange3D;
                    UB.Core.EchoFilter.ServerDispatch -= EchoFilter_ServerDispatch;
                    UB.Core.CharacterFilter.ChangePortalMode -= CharacterFilter_ChangePortalMode;
                    UB.Core.WorldFilter.CreateObject -= WorldFilter_CreateObject;
                    UB.Core.WorldFilter.ChangeObject -= WorldFilter_ChangeObject;
                    UBHelper.Core.RadarUpdate -= Core_RadarUpdate;
                }

                if (compassTexture != null) compassTexture.Dispose();
                if (mapTexture != null) mapTexture.Dispose();
                if (hud != null) hud.Dispose();

                ClearCache(true);

                dungeon = null;
                base.Dispose(disposing);
            }
        }
    }
}
