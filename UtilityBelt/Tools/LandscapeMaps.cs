using Decal.Adapter;
using Microsoft.DirectX;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Dungeon;
using UtilityBelt.Lib.Maps;
using UtilityBelt.Lib.Maps.Markers;
using UtilityBelt.Lib.Settings;
using VirindiViewService;
using VirindiViewService.Controls;
using UtilityBelt.Service.Lib.Settings;

namespace UtilityBelt.Tools {
    [Name("LandscapeMaps")]
    [Summary("Provides a landscape map with markers for points of interest")]
    [FullDescription(@"

Click the map looking icon under the main utility belt icon in the VVS bar to show.  You can click the `Follow` button to make the map stick to your character position. Click and dragging on the map pans, and changes to manual positioning.  Scroll wheel changes the zoom level.

#### Screenshots

![UtilityBelt plugin landscape maps window preview](../../images/screenshots/landscapemaps.png)
")]
    public class LandscapeMaps : ToolBase {
        private DxTexture mapTexture = null;
        private DxTexture playerArrowTexture = null;
        private DxHud hud = null;

        private double maxCoord = 102.1;
        private bool needsRedraw = true;
        
        private bool isFollowingCharacter = false;
        private double mapTextureStartOffsetX;
        private double mapTextureStartOffsetY;
        private int dragStartX;
        private int dragStartY;
        private bool isPanning = false;
        private string fontFace;
        private int fontWeight;

        private double mapTextureOffsetX;
        private double mapTextureOffsetY;

        private double scale {
            get { return Math.Pow(1.5, zoom * 12f) - 0.8f; }
        }

        private double _zoom = 0.04;
        private int lastMouseX;
        private int lastMouseY;
        private double zoomTarget;
        private HudButton followButton;
        private double lastHeading;
        private int lastPlayerHudY;
        private int lastPlayerHudX;
        private DxTexture horizontalBorderTexture;
        private int coordinateBorderWidth;
        private DxTexture verticalBorderTexture;

        private Dictionary<string, DxTexture> _CoordsfontCache = new Dictionary<string, DxTexture>();
        private DateTime lastDraw;
        private TimeSpan mapUpdateInterval = TimeSpan.FromMilliseconds(1000 / 30);
        private LandscapeMarkers markerData = null;
        private Dictionary<LandscapeMarkers.MarkerType, DxTexture> markerCache = new Dictionary<LandscapeMarkers.MarkerType, DxTexture>();

        private double centerX { get { return mapTextureOffsetX * scale; } }
        private double centerY { get { return mapTextureOffsetY * scale; } }

        private double zoom {
            get { return _zoom; }
            set { _zoom = Math.Max(Math.Min(value, 1), 0); }
        }

        /// <summary>
        /// the left x value of the map texture, relative to the top left of the hud
        /// </summary>
        private double canvasLeft {
            get {
                if (hud == null || hud.Texture == null)
                    return 0;
                return -(centerX - (hud.Texture.Width / 2));
            }
        }

        /// <summary>
        /// the right x value of the map texture, relative to the top left of the hud
        /// </summary>
        private double canvasRight {
            get {
                if (hud == null || hud.Texture == null)
                    return 0;
                return canvasLeft + (mapTexture.Width * scale);
            }
        }

        /// <summary>
        /// the top y value of the map texture, relative to the top left of the hud
        /// </summary>
        private double canvasTop {
            get {
                if (hud == null || hud.Texture == null)
                    return 0;
                return -(centerY - (hud.Texture.Height / 2));
            }
        }

        /// <summary>
        /// the bottom y value of the map texture, relative to the top left of the hud
        /// </summary>
        private double canvasBottom {
            get {
                if (hud == null || hud.Texture == null)
                    return 0;
                return canvasTop + (mapTexture.Height * scale);
            }
        }
        
        private PointF minVisibleCoords { get { return HudToCoordinates(0, 0); } }
        private PointF maxVisibleCoords { get { return HudToCoordinates(hud.Texture.Width, hud.Texture.Height); } }

        #region Config
        [Summary("Enabled")]
        public readonly Setting<bool> Enabled = new Setting<bool>(true);

        [Summary("Map opacity")]
        public readonly Setting<int> Opacity = new Setting<int>(16);

        [Summary("Map Window X")]
        public readonly CharacterState<int> MapWindowX = new CharacterState<int>(40);

        [Summary("Map Window Y")]
        public readonly CharacterState<int> MapWindowY = new CharacterState<int>(200);

        [Summary("Map Window width")]
        public readonly CharacterState<int> MapWindowWidth = new CharacterState<int>(320);

        [Summary("Map Window height")]
        public readonly CharacterState<int> MapWindowHeight = new CharacterState<int>(280);
        #endregion // Config

        public LandscapeMaps(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        public override void Init() {
            base.Init();
            followButton = (HudButton)UB.LandscapeMapView.view["FollowCharacter"];

            followButton.Hit += FollowButton_Hit;

            fontFace = UB.LandscapeMapView.view.MainControl.Theme.GetVal<string>("DefaultTextFontFace");
            fontWeight = UB.LandscapeMapView.view.MainControl.Theme.GetVal<int>("ViewTextFontWeight");

            UB.LandscapeMapView.view.VisibleChanged += View_VisibleChanged;
            Enabled.Changed += LandscapeMaps_PropertyChanged;
            UB.LandscapeMapView.view.ShowInBar = Enabled;
        }

        #region event handlers
        private void LandscapeMaps_PropertyChanged(object sender, SettingChangedEventArgs e) {
            if (e.PropertyName == "Enabled") {
                if (!Enabled)
                    UB.LandscapeMapView.view.Visible = false;
                UB.LandscapeMapView.view.ShowInBar = Enabled;
            }
        }

        private void View_VisibleChanged(object sender, EventArgs e) {
            try {
                var renderContainer = UB.LandscapeMapView.view["LandscapeMapsRenderContainer"];
                if (Enabled && UB.LandscapeMapView.view.Visible) {
                    LoadResources();

                    UB.LandscapeMaps.CreateHud();

                    AddDefaultMarkers();

                    renderContainer.MouseEvent += LandscapeMaps_MouseEvent;
                    UB.Core.RenderFrame += Core_RenderFrame;

                    HudView.FocusChanged += HudView_FocusChanged;
                    UB.Core.RegionChange3D += Core_RegionChange3D;
                }
                else {
                    renderContainer.MouseEvent -= LandscapeMaps_MouseEvent;
                    UB.Core.RenderFrame -= Core_RenderFrame;

                    HudView.FocusChanged -= HudView_FocusChanged;
                    UB.Core.RegionChange3D -= Core_RegionChange3D;
                    UB.LandscapeMaps.ClearHud();
                }
            }
            catch (Exception ex) {
                Logger.LogException(ex);
            }
        }

        private void Core_RegionChange3D(object sender, RegionChange3DEventArgs e) {
            if (hud != null && UB.LandscapeMapView.view.Visible)
                needsNewHud = true;

            mapTexture.Dispose();
            mapTexture = null;
            playerArrowTexture.Dispose();
            playerArrowTexture = null;
            markerCache.Clear();
            addedMarkerIcons = false;
            markerData = null;
            _CoordsfontCache.Clear();
            LoadResources();
            AddDefaultMarkers();
        }

        private void HudView_FocusChanged(object sender, EventArgs e) {
            if (hud == null)
                return;

            if (!UB.LandscapeMapView.view.IsTopView)
                hud.ZPriority = 0;
            else
                hud.ZPriority = 1;
        }

        private void FollowButton_Hit(object sender, EventArgs e) {
            try {
                var me = UB.Core.WorldFilter[UB.Core.CharacterFilter.Id].Coordinates();
                AbsoluteZoomTo(me.EastWest, me.NorthSouth, zoom > 0.24 ? zoom : 0.24f);
                isFollowingCharacter = true;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Core_RenderFrame(object sender, EventArgs e) {
            if (DateTime.UtcNow - lastDraw < mapUpdateInterval)
                return;
            lastDraw = DateTime.UtcNow;

            if (needsNewHud) {
                CreateHud();
                needsNewHud = false;
            }

            if (lastHeading != UB.Core.Actions.Heading) {
                lastHeading = UB.Core.Actions.Heading;
                needsRedraw = true;
            }
            var me = UB.Core.CharacterFilter.Id;
            var ew = Geometry.LandblockToEW((uint)PhysicsObject.GetLandcell(me), PhysicsObject.GetPosition(me).X);
            var ns = Geometry.LandblockToNS((uint)PhysicsObject.GetLandcell(me), PhysicsObject.GetPosition(me).Y);
            var currentPlayerHudPos = CoordinatesToHud(ew, ns);
            if (lastPlayerHudY != currentPlayerHudPos.Y || lastPlayerHudX != currentPlayerHudPos.X) {
                lastPlayerHudY = currentPlayerHudPos.Y;
                lastPlayerHudX = currentPlayerHudPos.X;

                if (isFollowingCharacter) {
                    CenterMapOn(ew, ns);
                }

                if (HudContainsPoint(currentPlayerHudPos))
                    needsRedraw = true;
            }

            if (needsRedraw) {
                try {
                    RenderHud();
                    needsRedraw = false;
                }
                catch (Exception ex) { Logger.LogException(ex); }
            }
        }

        private void LandscapeMaps_MouseEvent(object sender, VirindiViewService.Controls.ControlMouseEventArgs e) {
            try {
                switch (e.EventType) {
                    case ControlMouseEventArgs.MouseEventType.MouseWheel:
                        hud.ZPriority = 1;
                        if (e.WheelAmount != 0) {
                            var originalMousePos = HudToCoordinates(lastMouseX, lastMouseY);
                            zoom += e.WheelAmount < 0 ? -0.04f : 0.04f;
                            if (isFollowingCharacter == false) {
                                var newMousePos = HudToCoordinates(lastMouseX, lastMouseY);
                                var xo = CoordinatesToHud(originalMousePos.X, originalMousePos.Y).X - CoordinatesToHud(newMousePos.X, newMousePos.Y).X;
                                var yo = CoordinatesToHud(originalMousePos.X, originalMousePos.Y).Y - CoordinatesToHud(newMousePos.X, newMousePos.Y).Y;
                                mapTextureOffsetX += xo / scale;
                                mapTextureOffsetY += yo / scale;
                            }

                            needsRedraw = true;
                        }
                        break;
                    case ControlMouseEventArgs.MouseEventType.MouseDown:
                        mapTextureStartOffsetX = mapTextureOffsetX;
                        mapTextureStartOffsetY = mapTextureOffsetY;
                        dragStartX = e.X;
                        dragStartY = e.Y;
                        isFollowingCharacter = false;
                        hud.ZPriority = 1;

                        if ((Control.ModifierKeys & Keys.Shift) == Keys.Shift) {
                            //isRotating = true;
                            //rotationStart = mapRotation;
                        }
                        else {
                            isPanning = true;
                        }
                        needsRedraw = true;
                        break;

                    case ControlMouseEventArgs.MouseEventType.MouseUp:
                        isPanning = false;
                        needsRedraw = true;
                        break;

                    case ControlMouseEventArgs.MouseEventType.MouseMove:
                        lastMouseX = e.X;
                        lastMouseY = e.Y - 29 /* hud vertical offset from container */;
                        if (isPanning) {
                            var angle = -(Math.Atan2(e.Y - dragStartY, dragStartX - e.X) * 180.0 / Math.PI);
                            var distance = Math.Sqrt(Math.Pow(dragStartX - e.X, 2) + Math.Pow(dragStartY - e.Y, 2));
                            var np = Util.MovePoint(new PointF((float)mapTextureStartOffsetX, (float)mapTextureStartOffsetY), angle, distance / scale);

                            mapTextureOffsetX = np.X;
                            mapTextureOffsetY = np.Y;
                        }
                        needsRedraw = true;
                        break;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        #endregion

        #region util
        private void LoadResources() {
            if (mapTexture == null) {
                mapTexture = TextureCache.TextureFromBitmapResource("UtilityBelt.Resources.acmap.png");

                // center map on first load
                mapTextureOffsetX = mapTexture.Width / 2;
                mapTextureOffsetY = mapTexture.Height / 2;
            }

            if (playerArrowTexture == null)
                playerArrowTexture = TextureCache.TextureFromBitmapResource("UtilityBelt.Resources.icons.arrow.png");

            if (markerData == null) {
                markerData = new LandscapeMarkers();

                foreach (var kv in markerData.DisplayOptions) {
                    var texture = new DxTexture(new Size(16, 16));
                    texture.BeginRender();
                    texture.Fill(new Rectangle(0, 0, texture.Width, texture.Height), Color.Transparent);
                    texture.DrawPortalImageNoBorder(kv.Value.Icon, new Rectangle(0, 0, 16, 16));
                    texture.EndRender();
                    markerCache.Add(kv.Key, texture);
                }
            }
        }

        private void AddDefaultMarkers() {
            if (addedMarkerIcons)
                return;
            foreach (var marker in markerData.Markers) {
                var icon = new IconMarker(marker.Value.EW, marker.Value.NS, markerCache[marker.Value.Type]) {
                    MinZoomLevel = markerData.DisplayOptions[marker.Value.Type].MinMarkerZoomLevel,
                    MaxZoomLevel = markerData.DisplayOptions[marker.Value.Type].MaxMarkerZoomLevel
                };
                var label = new LabelMarker(marker.Value.Name, marker.Value.EW, marker.Value.NS) {
                    MinZoomLevel = markerData.DisplayOptions[marker.Value.Type].MinLabelZoomLevel,
                    MaxZoomLevel = markerData.DisplayOptions[marker.Value.Type].MaxLabelZoomLevel
                };
                label.SetParent(icon);
                AddMarker(icon);
            }
            addedMarkerIcons = true;
        }
        #endregion

        #region public interface
        /// <summary>
        /// Force the map to redraw
        /// </summary>
        public void Redraw() {
            needsRedraw = true;
        }

        /// <summary>
        /// Turns ingame coordinates into hud relative x/y point
        /// </summary>
        /// <param name="ew"></param>
        /// <param name="ns"></param>
        /// <returns></returns>
        public Point CoordinatesToHud(double ew, double ns) {
            var w = mapTexture == null ? 0 : mapTexture.Width;
            var h = mapTexture == null ? 0 : mapTexture.Height;
            var percentX = (ew + maxCoord) / (maxCoord * 2);
            var percentY = (ns + maxCoord) / (maxCoord * 2);
            var hudX = canvasLeft + (percentX * (w * scale));
            var hudY = canvasBottom - (percentY * (h * scale));
            return new Point(
                (int)hudX,
                (int)hudY
            );
        }

        /// <summary>
        /// converts hud relative x/y to ingame coordinates
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public PointF HudToCoordinates(int x, int y) {
            //if (x < 0 || x > hud.Texture.Width || y < 0 || y > hud.Texture.Height)
            //    return new PointF(0, 0);

            var hudOffsetLeft = -canvasLeft + x;
            var hudOffsetTop = -canvasTop + y;
            var ew = ((hudOffsetLeft / scale / mapTexture.Width) * maxCoord * 2) - maxCoord;
            var ns = ((hudOffsetTop / scale / mapTexture.Height) * maxCoord * 2) - maxCoord;
            return new PointF(
                (float)ew,
                (float)-ns
            );
        }

        /// <summary>
        /// Returns true if the hud x/y point is inside the hud render area
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public bool HudContainsPoint(Point point) {
            return Geometry.RectangleContainsPoint(new Rectangle(0, 0, hud?.Texture?.Width ?? 0, hud?.Texture?.Height ?? 0), point);
        }

        /// <summary>
        /// Returns true if the given coordinates are visible on the map
        /// </summary>
        /// <param name="ew"></param>
        /// <param name="ns"></param>
        /// <returns></returns>
        public bool CoordinatesVisible(double ew, double ns) {
            return HudContainsPoint(CoordinatesToHud(ew, ns));
        }

        /// <summary>
        /// Zoom into a coordinate location
        /// </summary>
        /// <param name="ew"></param>
        /// <param name="ns"></param>
        /// <param name="zoom">absolute zoom level</param>
        public void AbsoluteZoomTo(double ew, double ns, double zoom = 0.24f) {
            zoomTarget = zoom;
            this.zoom = zoom;
            CenterMapOn(ew, ns);
            needsRedraw = true;
        }

        /// <summary>
        /// Centers the map on the coordinates without changing zoom level
        /// </summary>
        /// <param name="ew"></param>
        /// <param name="ns"></param>
        public void CenterMapOn(double ew, double ns) {
            mapTextureOffsetX = (mapTexture.Width * ((ew + maxCoord) / (maxCoord * 2)));
            mapTextureOffsetY = (mapTexture.Height * ((-ns + maxCoord) / (maxCoord * 2)));
        }

        /// <summary>
        /// Adds a Marker to be drawn on the map
        /// </summary>
        /// <param name="marker"></param>
        public void AddMarker(BaseMarker marker) {
            if (markers.ContainsKey(marker.Id))
                return;
            markers.Add(marker.Id, marker);
            if (CoordinatesVisible(marker.EW, marker.NS))
                needsRedraw = true;
            needsMarkerSort = true;
        }
        #endregion //public interface

        #region Hud Rendering
        public void ClearHud() {
            if (horizontalBorderTexture != null) {
                horizontalBorderTexture.Dispose();
                horizontalBorderTexture = null;
            }
            if (verticalBorderTexture != null) {
                verticalBorderTexture.Dispose();
                verticalBorderTexture = null;
            }

            if (hud == null || hud.Texture == null || hud.Texture.IsDisposed)
                return;
            try {
                hud.Texture.BeginRender();
                hud.Texture.Fill(new Rectangle(0, 0, hud.Texture.Width, hud.Texture.Height), Color.Transparent);
            }
            catch (Exception ex) { Logger.LogException(ex); }
            finally { hud.Texture.EndRender(); }

            if (!Enabled) {
                hud.Dispose();
                hud = null;
            }

        }

        internal void CreateHud() {
            try {
                if (hud != null) {
                    ClearHud();
                    hud.Dispose();
                }
                if (!Enabled || !UB.LandscapeMapView.view.Visible)
                    return;

                var renderContainer = UB.LandscapeMapView.view["LandscapeMapsRenderContainer"];
                int width = renderContainer.SavedViewRect.Width;
                int height = renderContainer.SavedViewRect.Height;
                int x = renderContainer.SavedViewRect.X;
                int y = renderContainer.SavedViewRect.Y;

                hud = new DxHud(new Point(x + 5, y + 69), new Size(width - 10, height - 74), 0);
                hud.Enabled = true;
                needsRedraw = true;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        internal void RenderHud() {
            if (hud == null || hud.Texture == null)
                return;
            try {
                RenderCoordinateBorders();
                hud.Texture.BeginRender();
                hud.Texture.Fill(new Rectangle(0, 0, hud.Texture.Width, hud.Texture.Height), Color.FromArgb(255, 29, 33, 59));

                double sx, sy;
                var rotationCenter = new Vector3((float)centerX, (float)centerY, 0);
                Quaternion rotQuat = Geometry.HeadingToQuaternion(0f);
                Matrix transform = new Matrix();
                var tint = Color.White.ToArgb();
                rotQuat = Geometry.HeadingToQuaternion(0);
                sx = -(centerX - (hud.Texture.Width / 2));
                sy = -(centerY - (hud.Texture.Height / 2));

                transform.AffineTransformation((float)scale, rotationCenter, rotQuat, new Vector3((float)sx, (float)sy, 0));
                hud.Texture.DrawTextureWithTransform(mapTexture, transform, tint);

                RenderMarkers();

                // draw coordinate borders
                hud.Texture.DrawTexture(horizontalBorderTexture, new Rectangle(0, 0, horizontalBorderTexture.Width, coordinateBorderWidth));
                hud.Texture.DrawTexture(horizontalBorderTexture, new Rectangle(0, hud.Texture.Height - coordinateBorderWidth, horizontalBorderTexture.Width, coordinateBorderWidth));
                hud.Texture.DrawTexture(verticalBorderTexture, new Rectangle(0, coordinateBorderWidth, coordinateBorderWidth, verticalBorderTexture.Height));
                hud.Texture.DrawTexture(verticalBorderTexture, new Rectangle(hud.Texture.Width - coordinateBorderWidth, coordinateBorderWidth, coordinateBorderWidth, verticalBorderTexture.Height));

                RenderPlayerMarker();

                try {
                    hud.Texture.BeginText(fontFace, 10f, 150, false, 1, (int)byte.MaxValue);
                    var coords = HudToCoordinates(lastMouseX, lastMouseY);
                    var text = $"{Math.Abs(coords.Y).ToString("F2")}{(coords.Y > 0 ? "N" : "S")}, {Math.Abs(coords.X).ToString("F2")}{(coords.X > 0 ? "E" : "W")}";
                    hud.Texture.WriteText(text, Color.White, VirindiViewService.WriteTextFormats.Center, new Rectangle(0, hud.Texture.Height - 40, hud.Texture.Width, 20));
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
        private bool needsNewHud;
        private bool addedMarkerIcons;
        private Dictionary<uint, BaseMarker> markers = new Dictionary<uint, BaseMarker>();
        private bool needsMarkerSort = true;
        private List<uint> sortedMarkerKeys = new List<uint>();

        private void RenderMarkers() {
            BaseMarker.ResetDraw();
            IconMarker.ResetDraw();
            LabelMarker.ResetDraw();
            uint mouseOverId = 0;

            if (needsMarkerSort) {
                sortedMarkerKeys = new List<uint>(markers.Keys);
                sortedMarkerKeys.Sort((x, y) => markers[x].ZOrder.CompareTo(markers[y].ZOrder)); ;
            }

            // draw markers
            foreach (var k in sortedMarkerKeys) {
                if (!markers.ContainsKey(k))
                    continue;
                var marker = markers[k];
                if (marker.IsDisposed) {
                    markers.Remove(k);
                    continue;
                }
                var pos = CoordinatesToHud(marker.EW, marker.NS);

                if (markers[k].Draw(hud.Texture, pos.X, pos.Y, zoom, false))
                    if (Geometry.RectangleContainsPoint(marker.MouseOverRect, new Point(lastMouseX - pos.X, lastMouseY - pos.Y)))
                        mouseOverId = marker.Id;
            }

            // draw labels
            foreach (var k in sortedMarkerKeys) {
                if (!markers.ContainsKey(k) || markers[k].Label == null)
                    continue;
                var pos = CoordinatesToHud(markers[k].EW, markers[k].NS);
                markers[k].Label.Draw(hud.Texture, pos.X, pos.Y, zoom, false);
            }


            // draw highlighted pair on top
            if (mouseOverId != 0 && markers.ContainsKey(mouseOverId)) {
                var pos = CoordinatesToHud(markers[mouseOverId].EW, markers[mouseOverId].NS);
                markers[mouseOverId].Draw(hud.Texture, pos.X, pos.Y, zoom, true);
                if (markers[mouseOverId].Label != null)
                    markers[mouseOverId].Label.Draw(hud.Texture, pos.X, pos.Y, zoom, true);
            }
        }

        private void RenderPlayerMarker() {
            var rotationCenter = new Vector3((float)playerArrowTexture.Width/4, (float)playerArrowTexture.Height/4, 0);
            Quaternion rotQuat = Geometry.HeadingToQuaternion(360f - (float)UB.Core.Actions.Heading);
            Matrix transform = new Matrix();
            var tint = Color.Magenta.ToArgb();
            var x = isFollowingCharacter ? hud.Texture.Width / 2 : lastPlayerHudX;
            var y = isFollowingCharacter ? hud.Texture.Height / 2 : lastPlayerHudY;

            transform.AffineTransformation(0.5f, rotationCenter, rotQuat, new Vector3(x - 8, y - 8, 0));
            hud.Texture.DrawTextureWithTransform(playerArrowTexture, transform, tint);
        }

        #region Coordinate borders

        private void RenderCoordinateBorders() {
            coordinateBorderWidth = 20;
            var borderBackgroundOpacity = 0.55f;
            var sx = -(centerX - (hud.Texture.Width / 2));
            var sy = -(centerY - (hud.Texture.Height / 2));
            var fontSize = 9f;

            if (horizontalBorderTexture == null)
                horizontalBorderTexture = new DxTexture(new Size(hud.Texture.Width, coordinateBorderWidth));
            try {
                horizontalBorderTexture.BeginRender();
                horizontalBorderTexture.Fill(new Rectangle(0, 0, horizontalBorderTexture.Width, horizontalBorderTexture.Height), Color.FromArgb((int)(255 * borderBackgroundOpacity), 0, 0, 0));

                double inc = GetCoordinateIncrement();

                var minX = minVisibleCoords.X;
                var maxX = maxVisibleCoords.X;
                var wantedMarkers = (maxX - minX) / inc;
                var offset = -((minX % inc)/inc) * (horizontalBorderTexture.Width / wantedMarkers);
                var current = minX - (minX % inc);

                // markers
                horizontalBorderTexture.BeginText(fontFace, fontSize, 150, false, 1, (int)byte.MaxValue);
                try {
                    for (var i = -1; i <= wantedMarkers; i++) {
                        var cx = offset + ((horizontalBorderTexture.Width/wantedMarkers) * i);
                        horizontalBorderTexture.DrawLine(new PointF((float)cx, 0), new PointF((float)cx, 4), Color.Gray, 1);
                        horizontalBorderTexture.DrawLine(new PointF((float)cx, coordinateBorderWidth - 3), new PointF((float)cx, coordinateBorderWidth), Color.Gray, 1);
                        var c = current + (i * inc);
                        var text = $"{Math.Abs(c).ToString("F1")}{(c > 0 ? "E" : "W")}";
                        horizontalBorderTexture.WriteText(text, Color.LightGray, WriteTextFormats.Center, new Rectangle((int)cx - 17, 2, 40, (int)2));
                    }
                }
                finally {
                    horizontalBorderTexture.EndText();
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
            finally {
                horizontalBorderTexture.EndRender();
            }

            if (verticalBorderTexture == null)
                verticalBorderTexture = new DxTexture(new Size(coordinateBorderWidth, hud.Texture.Height - (coordinateBorderWidth * 2)));
            try {
                verticalBorderTexture.BeginRender();
                verticalBorderTexture.Fill(new Rectangle(0, 0, verticalBorderTexture.Width, verticalBorderTexture.Height), Color.FromArgb((int)(255 * borderBackgroundOpacity), 0, 0, 0));

                double inc = GetCoordinateIncrement();

                var minY = HudToCoordinates(0, coordinateBorderWidth).Y;
                var maxY = HudToCoordinates(hud.Texture.Width, verticalBorderTexture.Height + coordinateBorderWidth).Y;
                var wantedMarkers = (minY - maxY) / inc;
                var offset = -((maxY % inc) / inc) * (verticalBorderTexture.Height / wantedMarkers);
                var current = maxY - (maxY % inc);

                // markers
                for (var i = -1; i <= wantedMarkers; i++) {
                    var cy = offset + ((verticalBorderTexture.Height / wantedMarkers) * i);
                    cy = verticalBorderTexture.Height - cy;
                    verticalBorderTexture.DrawLine(new PointF(0, (float)cy), new PointF(4, (float)cy), Color.Gray, 1);
                    verticalBorderTexture.DrawLine(new PointF(coordinateBorderWidth - 3, (float)cy), new PointF(coordinateBorderWidth, (float)cy), Color.Gray, 1);
                    var c = current - (i * inc);
                    var text = $"{Math.Abs(c).ToString(inc < 1 ? "F1" : "F0")}{(c > 0 ? "N" : "S")}";

                    if (!_CoordsfontCache.ContainsKey(text)) {
                        var fontTexture = new DxTexture(new Size(50, 20));
                        verticalBorderTexture.EndRender();
                        fontTexture.BeginRender();
                        fontTexture.Fill(new Rectangle(0, 0, fontTexture.Width, fontTexture.Height), Color.Transparent);
                        fontTexture.BeginText(fontFace, fontSize, 150, false, 1, (int)byte.MaxValue);
                        fontTexture.WriteText(text, Color.LightGray, WriteTextFormats.Center | WriteTextFormats.VerticalCenter, new Rectangle(0, 0, fontTexture.Width, fontTexture.Height));
                        fontTexture.EndText();
                        fontTexture.EndRender();
                        verticalBorderTexture.BeginRender();
                        _CoordsfontCache.Add(text, fontTexture);
                    }
                    verticalBorderTexture.DrawTextureRotated(_CoordsfontCache[text], new Rectangle(0, 0, _CoordsfontCache[text].Width, _CoordsfontCache[text].Height), new Point(10, (int)cy), Color.White.ToArgb(), (float)(-90 * Math.PI / 180));
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
            finally {
                verticalBorderTexture.EndRender();
            }
        }

        private double GetCoordinateIncrement() {
            double inc = 1;
            if (zoom < 0.01)
                inc = 50;
            else if (zoom < 0.05)
                inc = 20;
            else if (zoom < 0.13)
                inc = 10;
            else if (zoom < 0.3)
                inc = 5;
            else if (zoom < 0.5)
                inc = 2;
            else if (zoom < 0.65)
                inc = 1;
            else if (zoom < 0.85)
                inc = 0.5;
            else if (zoom < 0.94)
                inc = 0.2;
            else
                inc = 0.1;
            return inc;
        }
        #endregion // Coordinate borders
        #endregion // Hud Rendering

        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    Enabled.Changed -= LandscapeMaps_PropertyChanged;
                    UB.LandscapeMapView.view.VisibleChanged -= View_VisibleChanged;
                    UB.Core.RegionChange3D -= Core_RegionChange3D;
                    UB.Core.RenderFrame -= Core_RenderFrame;
                    VirindiViewService.HudView.FocusChanged -= HudView_FocusChanged;
                    UB.LandscapeMapView.view["LandscapeMapsRenderContainer"].MouseEvent -= LandscapeMaps_MouseEvent;
                    if (mapTexture != null) mapTexture.Dispose();
                    if (hud != null) hud.Dispose();
                    if (horizontalBorderTexture != null) horizontalBorderTexture.Dispose();
                    if (verticalBorderTexture != null) verticalBorderTexture.Dispose();
                    foreach (var kv in _CoordsfontCache) {
                        kv.Value.Dispose();
                    }
                    foreach (var kv in markerCache) {
                        kv.Value.Dispose();
                    }
                    foreach (var kv in markers) {
                        kv.Value.Dispose();
                    }

                    base.Dispose(disposing);
                }
                disposedValue = true;
            }
        }
    }
}
