using Decal.Adapter;
using Decal.Adapter.Wrappers;
using Decal.Interop.Input;
using Microsoft.DirectX;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Text.RegularExpressions;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Dungeon;
using UtilityBelt.Lib.Maps.Markers;
using UtilityBelt.Lib.Settings;
using VirindiViewService;
using static UtilityBelt.Tools.VTankControl;
using UBService.Lib.Settings;

namespace UtilityBelt.Tools {
    [Name("Arrow")]
    [Summary("Provides an arrow overlay that can be pointed to different coordinates by clicking them in chat or issuing a command")]
    [FullDescription(@"This tool provides an arrow overlay that can point to target coordinates. By default it makes coordinates in chat clickable.

Hold ctrl and drag to move the overlay position.  You can click the exit icon while holding ctrl to dismiss the overlay. ")]
    public class Arrow : ToolBase {
        private DxTexture arrowTexture = null;
        private DxTexture arrowMapTexture = null;
        private UBHud hud = null;
        private string fontFace;
        private int fontWeight;
        private double lastHeading;
        private double lastDistance;

        private int LabelFontSize = 10;

        private TimerClass drawTimer;

        Regex ChatCoordinatesRe = new Regex(@"[^>](?<NS>[0-9]{1,3}(?:\.[0-9]{1,3})?)(?<NSChar>(?:[ns]))(?:[,\s]+)?(?<EW>[0-9]{1,3}(?:\.[0-9]{1,3})?)(?<EWChar>(?:[ew]))", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private IconMarker mapMarker;
        private LabelMarker mapLabel;

        #region Config
        [Summary("Enabled")]
        public Setting<bool> Enabled = new Setting<bool>(true);

        [Summary("Wether the hud is visible or not.")]
        public readonly CharacterState<bool> Visible = new CharacterState<bool>(true);

        [Summary("Arrow HUD Size")]
        public readonly CharacterState<int> HudSize = new CharacterState<int>(32);

        [Summary("Arrow HUD Position X")]
        public readonly CharacterState<int> HudX = new CharacterState<int>(215);

        [Summary("Arrow HUD Position Y")]
        public readonly CharacterState<int> HudY = new CharacterState<int>(5);

        [Summary("Target EW coordinate")]
        public readonly CharacterState<double> TargetEW = new CharacterState<double>(0);

        [Summary("Target NS coordinate")]
        public readonly CharacterState<double> TargetNS = new CharacterState<double>(0);
        #endregion // Config

        #region Commands

        #region /ub arrow
        [Summary("Points the arrow towards the specified coordinates")]
        [Usage("/ub arrow [point <coordinates>|face]")]
        [Example("/ub arrow point 32.7N, 45.5E", "Points the arrow towards 32.7N, 45.5E")]
        [Example("/ub arrow face", "Faces your character in the same direction the arrow is currently pointing")]
        [CommandPattern("arrow", @"^(point \d+.?\d*[nNsS],?\s*\d+.?\d*[eEwW]|face)$")]
        public void DoArrow(string command, Match args) {
            if (args.Value.ToLower() == "face") {
                var me = UB.Core.CharacterFilter.Id;
                var ew = Geometry.LandblockToEW((uint)PhysicsObject.GetLandcell(me), PhysicsObject.GetPosition(me).X);
                var ns = Geometry.LandblockToNS((uint)PhysicsObject.GetLandcell(me), PhysicsObject.GetPosition(me).Y);

                var heading = (270 + 180 - (Math.Atan2(TargetNS - ns, TargetEW - ew) * 180 / Math.PI)) % 360;
                UBHelper.Core.TurnToHeading((float)heading);
                Logger.WriteToChat($"Turning to face towards {heading:N2} degrees");
            }
            else {
                var coords = Coordinates.FromString(args.Value.ToLower().Replace("point ", ""));
                PointTo(coords.EW, coords.NS);
            }
        }
        #endregion
        #endregion

        public Arrow(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        public override void Init() {
            base.Init();
            try {
                fontFace = UB.LandscapeMapView.view.MainControl.Theme.GetVal<string>("DefaultTextFontFace");
                fontWeight = UB.LandscapeMapView.view.MainControl.Theme.GetVal<int>("ViewTextFontWeight");

                Changed += Arrow_PropertyChanged;

                if (UBHelper.Core.GameState != UBHelper.GameState.In_Game)
                    UB.Core.CharacterFilter.LoginComplete += CharacterFilter_LoginComplete;
                else
                    TryEnable();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void CharacterFilter_LoginComplete(object sender, EventArgs e) {
            UB.Core.CharacterFilter.LoginComplete -= CharacterFilter_LoginComplete;
            TryEnable();
        }

        private void TryEnable() {
            if (Enabled) {
                CreateTextures();
                CreateHud();
                if (drawTimer == null) {
                    drawTimer = new TimerClass();
                    drawTimer.Timeout += DrawTimer_Timeout;
                    drawTimer.Start(1000 / 15); // 15 fps max
                }
                UB.Core.ChatBoxMessage += Core_ChatBoxMessage;
                UB.Core.ChatNameClicked += Core_ChatNameClicked;
                UpdateMapMarker();
            }
            else {
                if (drawTimer != null)
                    drawTimer.Stop();
                drawTimer = null;
                UB.Core.ChatBoxMessage -= Core_ChatBoxMessage;
                UB.Core.ChatNameClicked -= Core_ChatNameClicked;
                RemoveMapMarker();
                ClearHud();
            }
        }

        private void CreateTextures() {
            if (arrowTexture != null)
                arrowTexture.Dispose();

            arrowTexture = TextureCache.TextureFromBitmapResource("UtilityBelt.Resources.icons.arrow.png");

            if (arrowMapTexture != null)
                arrowMapTexture.Dispose();

            arrowMapTexture = new DxTexture(new Size(arrowTexture.Width, arrowTexture.Height));
            arrowMapTexture.BeginRender();
            arrowMapTexture.Fill(new Rectangle(0, 0, arrowMapTexture.Width, arrowMapTexture.Height), Color.Transparent);
            arrowMapTexture.DrawTextureRotated(arrowTexture, new Rectangle(0, 0, arrowTexture.Width, arrowTexture.Height), new Point(arrowMapTexture.Width / 2, arrowMapTexture.Height / 2), Color.White.ToArgb(), (float)Math.PI);
            arrowMapTexture.EndRender();
        }

        #region event handlers
        private void Core_ChatBoxMessage(object sender, ChatTextInterceptEventArgs e) {
            try {
                if (e.Eat != true && ChatCoordinatesRe.IsMatch(e.Text)) {
                    var text = e.Text;
                    var matches = ChatCoordinatesRe.Matches(e.Text);
                    foreach (Match match in matches) {
                        // this uses the same IIDString as goarrow for compatibility
                        text = text.Replace(match.Value, $"<Tell:IIDString:110011:{match.Value}>{match.Value}</Tell>");
                    }
                    e.Eat = true;
                    UB.Core.Actions.AddChatText(text, e.Color);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Core_ChatNameClicked(object sender, ChatClickInterceptEventArgs e) {
            try {
                if (e.Id == 110011) {
                    e.Eat = true;
                    var coordinates = Coordinates.FromString(e.Text);
                    PointTo(coordinates.EW, coordinates.NS);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Arrow_PropertyChanged(object sender, SettingChangedEventArgs e) {
            if (UBHelper.Core.GameState != UBHelper.GameState.In_Game)
                return;
            switch (e.PropertyName) {
                case "Enabled":
                    TryEnable();
                    break;
                default:
                    if (hud != null) {
                        hud.Enabled = Visible;
                        if (Visible)
                            hud.Render();
                    }
                    break;
            }
        }

        private void DrawTimer_Timeout(Decal.Interop.Input.Timer Source) {
            bool needsRedraw = false;
            if (lastHeading != UB.Core.Actions.Heading) {
                lastHeading = UB.Core.Actions.Heading;
                needsRedraw = true;
            }

            if (lastDistance != DistanceToTarget) {
                lastDistance = DistanceToTarget;
                needsRedraw = true;
            }

            if (needsRedraw)
                hud.Render();
        }

        private void Hud_OnClose() {
            Visible.Value = false;
        }

        private void Hud_OnMove() {
            HudX.Value = hud.BBox.X;
            HudY.Value = hud.BBox.Y;
        }

        private void Hud_OnReMake() {
            CreateTextures();
        }
        #endregion

        #region public interface
        /// <summary>
        /// Force the map to redraw
        /// </summary>
        public void Redraw() {
            hud?.Render();
        }

        /// <summary>
        /// Points the arrow towards the specified coordinates
        /// </summary>
        /// <param name="ew"></param>
        /// <param name="ns"></param>
        /// <param name="text">currently unused</param>
        /// <returns></returns>
        public void PointTo(double ew, double ns, string text = "") {
            if (!Enabled)
                return;

            TargetEW.Value = ew;
            TargetNS.Value = ns;
            Visible.Value = true;
            hud?.Render();

            UpdateMapMarker();
        }

        public double DistanceToTarget {
            get {
                var me = UB.Core.CharacterFilter.Id;
                var ew = Geometry.LandblockToEW((uint)PhysicsObject.GetLandcell(me), PhysicsObject.GetPosition(me).X);
                var ns = Geometry.LandblockToNS((uint)PhysicsObject.GetLandcell(me), PhysicsObject.GetPosition(me).Y);

                return (new Coordinates(ew, ns)).DistanceTo(new Coordinates(TargetEW, TargetNS));
            }
        }
        #endregion //public interface

        #region landscape map marker
        private void RemoveMapMarker() {
            if (mapMarker != null) {
                mapMarker.Dispose();
                mapMarker = null;
            }
            if (mapLabel != null) {
                mapLabel.Dispose();
                mapLabel = null;
            }
        }

        private void UpdateMapMarker() {
            if (mapMarker == null) {
                mapMarker = new IconMarker(TargetEW, TargetNS, arrowMapTexture) {
                    MinZoomLevel = 0,
                    MaxZoomLevel = 1,
                    ManageDxTexture = false
                };
                mapLabel = new LabelMarker("Arrow Target", TargetEW, TargetNS) {
                    MinZoomLevel = 0,
                    MaxZoomLevel = 1
                };
                mapMarker.AttachLabel(mapLabel);
                UB.LandscapeMaps?.AddMarker(mapMarker);
            }
            else {
                mapMarker.NS = TargetNS;
                mapMarker.EW = TargetEW;
                mapLabel.NS = TargetNS;
                mapLabel.EW = TargetEW;
                UB.LandscapeMaps?.Redraw();
            }
        }
        #endregion // landscape map marker

        #region Hud Rendering
        internal void CreateHud() {
            try {
                if (hud != null) {
                    ClearHud();
                }
                if (!Enabled)
                    return;
                hud = UB.Huds.CreateHud(HudX, HudY, 150, HudSize);
                hud.OnMove += Hud_OnMove;
                hud.OnClose += Hud_OnClose;
                hud.OnRender += Hud_OnRender;
                hud.OnReMake += Hud_OnReMake;
                hud.Render();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public void ClearHud() {
            if (hud == null)
                return;
            hud.OnMove -= Hud_OnMove;
            hud.OnClose -= Hud_OnClose;
            hud.OnRender -= Hud_OnRender;
            hud.OnReMake -= Hud_OnReMake;
            hud.Dispose();
            hud = null;
        }

        private void Hud_OnRender() {
            if (hud == null || hud.Texture == null)
                return;

            try {
                hud.Texture.BeginRender();
                hud.Texture.Clear();
                hud.Texture.Fill(new Rectangle(0, 0, hud.Texture.Width, hud.Texture.Height), Color.FromArgb(0, 0, 0, 0));

                if (!Visible)
                    return;

                var me = UB.Core.CharacterFilter.Id;
                var ew = Geometry.LandblockToEW((uint)PhysicsObject.GetLandcell(me), PhysicsObject.GetPosition(me).X);
                var ns = Geometry.LandblockToNS((uint)PhysicsObject.GetLandcell(me), PhysicsObject.GetPosition(me).Y);

                float scale = (HudSize * 0.75f) / arrowTexture.Width;
                var rotationCenter = new Vector3((float)arrowTexture.Width / 2 * scale, (float)arrowTexture.Height / 2 * scale, 0);
                var heading = -(((Math.Atan2(TargetNS - ns, TargetEW - ew) * 180 / Math.PI) + UB.Core.Actions.Heading) % 360);
                Quaternion rotQuat = Geometry.HeadingToQuaternion(360f - (float)(heading - 270f));
                Matrix transform = new Matrix();
                var tint = Color.White.ToArgb();

                var offset = (HudSize - (arrowTexture.Width * scale)) / 2;
                transform.AffineTransformation(scale, rotationCenter, rotQuat, new Vector3(offset, offset, 0));
                hud.Texture.DrawTextureWithTransform(arrowTexture, transform, tint);

                var leftOffset = (int)(offset * 2) + (int)(arrowTexture.Width * scale);
                hud.Texture.BeginText(fontFace, LabelFontSize, 200, false);
                var coordsText = $"{Math.Abs(TargetNS).ToString("F2")}{(TargetNS >= 0 ? "N" : "S")}, {Math.Abs(TargetEW).ToString("F2")}{(TargetEW >= 0 ? "E" : "W")}";
                var distanceText = $"{DistanceToTarget:N2}m";

                hud.DrawShadowText(coordsText, leftOffset, 1, hud.BBox.Width - leftOffset, LabelFontSize, Color.White, Color.Black);
                hud.DrawShadowText(distanceText, leftOffset, LabelFontSize + 4, hud.BBox.Width - leftOffset, LabelFontSize, Color.White, Color.Black);
                hud.Texture.EndText();
            }
            catch (Exception ex) { Logger.LogException(ex); }
            finally {
                hud.Texture.EndRender();
            }
        }
        #endregion

        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    if (drawTimer != null) {
                        drawTimer.Stop();
                        drawTimer = null;
                    }
                    UB.Core.ChatBoxMessage -= Core_ChatBoxMessage;
                    UB.Core.ChatNameClicked -= Core_ChatNameClicked;

                    Changed -= Arrow_PropertyChanged;

                    RemoveMapMarker();
                    ClearHud();
                    if (arrowTexture != null) arrowTexture.Dispose();
                    if (arrowMapTexture != null) arrowMapTexture.Dispose();
                }
                disposedValue = true;
            }
        }
    }
}
