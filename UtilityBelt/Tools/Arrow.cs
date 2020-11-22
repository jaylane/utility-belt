using Decal.Adapter;
using Decal.Adapter.Wrappers;
using Microsoft.DirectX;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Dungeon;
using UtilityBelt.Lib.Maps;
using UtilityBelt.Lib.Maps.Markers;
using UtilityBelt.Lib.Settings;
using VirindiViewService;
using VirindiViewService.Controls;
using static UtilityBelt.Tools.VTankControl;

namespace UtilityBelt.Tools {
    [Name("Arrow")]
    public class Arrow : ToolBase {
        private DxTexture arrowTexture = null;
        private DxHud hud = null;
        private string fontFace;
        private int fontWeight;
        private DateTime lastDraw;
        private TimeSpan drawUpdateInterval = TimeSpan.FromMilliseconds(1000 / 20);
        private bool needsRedraw;
        private double lastHeading;
        private bool isDragging;
        private Point dragOffset;
        private Point lastMousePos;
        private bool needsNewHud;
        private Point dragStartPos;
        private bool isHoldingControl;

        private int LabelFontSize = 10;

        const short WM_MOUSEMOVE = 0x0200;
        const short WM_LBUTTONDOWN = 0x0201;
        const short WM_LBUTTONUP = 0x0202;

        #region Config
        [Summary("Enabled")]
        [DefaultValue(true)]
        public bool Enabled {
            get { return (bool)GetSetting("Enabled"); }
            set {
                UpdateSetting("Enabled", value);
            }
        }

        [Summary("Arrow HUD Size")]
        [DefaultValue(32)]
        public int HudSize {
            get { return (int)GetSetting("HudSize"); }
            set { UpdateSetting("HudSize", value); }
        }

        [Summary("Arrow HUD Position X")]
        [DefaultValue(215)]
        public int HudX {
            get { return (int)GetSetting("HudX"); }
            set { UpdateSetting("HudX", value); }
        }

        [Summary("Arrow HUD Position Y")]
        [DefaultValue(5)]
        public int HudY {
            get { return (int)GetSetting("HudY"); }
            set { UpdateSetting("HudY", value); }
        }

        [Summary("TargetEW")]
        [DefaultValue(0.00)]
        public double TargetEW {
            get { return (double)GetSetting("TargetEW"); }
            set { UpdateSetting("TargetEW", value); }
        }

        [Summary("TargetNS")]
        [DefaultValue(0.00)]
        public double TargetNS {
            get { return (double)GetSetting("TargetNS"); }
            set { UpdateSetting("TargetNS", value); }
        }

        [Summary("TargetText")]
        [DefaultValue("")]
        public string TargetText {
            get { return (string)GetSetting("TargetText"); }
            set { UpdateSetting("TargetText", value); }
        }
        #endregion // Config

        #region Commands

        #region /ub arrow
        [Summary("Points the arrow towards the specified coordinates")]
        [Usage("/ub arrow point <coordinates>")]
        [Example("/ub arrow point 32.7N, 45.5E", "Points the arrow towards 32.7N, 45.5E")]
        [CommandPattern("arrow", @"^point \d+.?\d*[nNsS],\w+\d+.?\d*[eEwW]$")]
        public void DoArrow(string command, Match args) {
            var coords = Coordinates.FromString(args.Value.ToLower().Replace("point ", ""));
            PointTo(coords.EW, coords.NS);
        }

        #endregion
        #endregion

        public Arrow(UtilityBeltPlugin ub, string name) : base(ub, name) {
            try {
                fontFace = UB.LandscapeMapView.view.MainControl.Theme.GetVal<string>("DefaultTextFontFace");
                fontWeight = UB.LandscapeMapView.view.MainControl.Theme.GetVal<int>("ViewTextFontWeight");

                PropertyChanged += Arrow_PropertyChanged;
                TryEnable();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void TryEnable() {
            if (Enabled) {
                if (arrowTexture == null)
                    arrowTexture = TextureCache.TextureFromBitmapResource("UtilityBelt.Resources.icons.arrow.png");
                CreateHud();
                UB.Core.RenderFrame += Core_RenderFrame;
                UB.Core.WindowMessage += Core_WindowMessage;
                UB.Core.ChatBoxMessage += Core_ChatBoxMessage;
                UB.Core.ChatNameClicked += Core_ChatNameClicked;
            }
            else {
                ClearHud();
                UB.Core.RenderFrame -= Core_RenderFrame;
                UB.Core.WindowMessage -= Core_WindowMessage;
                UB.Core.ChatBoxMessage -= Core_ChatBoxMessage;
                UB.Core.ChatNameClicked -= Core_ChatNameClicked;
            }
        }

        #region event handlers
        Regex ChatCoordinatesRe = new Regex(@"[^>](?<NS>\d+\.?\d*)(?<NSChar>[ns]),?\s*(?<EW>\d+\.?\d*)(?<EWChar>[ew])", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private void Core_ChatBoxMessage(object sender, ChatTextInterceptEventArgs e) {
            try {
                if (e.Eat != true && ChatCoordinatesRe.IsMatch(e.Text)) {
                    var text = e.Text;
                    var matches = ChatCoordinatesRe.Matches(e.Text);
                    foreach (Match match in matches) {
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
                    var coordinates = Coordinates.FromString(e.Text);
                    PointTo(coordinates.EW, coordinates.NS);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Arrow_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case "Enabled":
                    TryEnable();
                    break;
                case "HudSize":
                case "HudX":
                case "HudY":
                case "LabelFontSize":
                    needsNewHud = true;
                    needsRedraw = true;
                    break;
            }
        }

        private void Core_WindowMessage(object sender, WindowMessageEventArgs e) {
            var ctrl = (Control.ModifierKeys & Keys.Control) == Keys.Control;
            if (ctrl != isHoldingControl) {
                isHoldingControl = ctrl;
                needsRedraw = true;
            }
            if (!isHoldingControl)
                return;

            if (e.Msg == WM_MOUSEMOVE || e.Msg == WM_LBUTTONDOWN) {
                var mousePos = new Point(e.LParam);
                if (!isDragging && (mousePos.X < HudX || mousePos.X > HudX + hud.Texture.Width || mousePos.Y < HudY || mousePos.Y > HudY + hud.Texture.Height))
                    return;
            }

            switch (e.Msg) {
                case WM_LBUTTONDOWN:
                    var newMousePos = new Point(e.LParam);
                    hud.ZPriority = 1;
                    isDragging = true;
                    dragStartPos = newMousePos;
                    dragOffset = new Point(0,0);
                    break;

                case WM_LBUTTONUP:
                    if (isDragging) {
                        isDragging = false;
                        HudX += dragOffset.X;
                        HudY += dragOffset.Y;
                        dragOffset = new Point(0,0);
                    }
                    break;

                case WM_MOUSEMOVE:
                    lastMousePos = new Point(e.LParam);
                    if (isDragging) {
                        dragOffset.X = lastMousePos.X - dragStartPos.X;
                        dragOffset.Y = lastMousePos.Y - dragStartPos.Y;
                    }
                    break;
            }
        }

        private void Core_RegionChange3D(object sender, RegionChange3DEventArgs e) {
            if (Enabled)
                needsNewHud = true;
        }

        private void Core_RenderFrame(object sender, EventArgs e) {
            if (DateTime.UtcNow - lastDraw < drawUpdateInterval)
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

            hud.Location = new Point(HudX + dragOffset.X, HudY + dragOffset.Y);

            RenderHud();
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
        /// Points the arrow towards the specified coordinates
        /// </summary>
        /// <param name="ew"></param>
        /// <param name="ns"></param>
        /// <param name="text"></param>
        /// <returns></returns>
        public void PointTo(double ew, double ns, string text="") {
            TargetEW = ew;
            TargetNS = ns;
            TargetText = text;
            needsRedraw = true;
        }
        #endregion //public interface

        #region Hud Rendering
        internal void CreateHud() {
            try {
                if (hud != null) {
                    ClearHud();
                }
                if (!Enabled)
                    return;

                hud = new DxHud(new Point(HudX, HudY), new Size(150, HudSize), 0);
                hud.Enabled = true;
                needsRedraw = true;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public void ClearHud() {
            if (hud == null || hud.Texture == null || hud.Texture.IsDisposed)
                return;
            try {
                hud.Texture.BeginRender();
                hud.Texture.Fill(new Rectangle(0, 0, hud.Texture.Width, hud.Texture.Height), Color.Transparent);
            }
            catch (Exception ex) { Logger.LogException(ex); }
            finally { hud.Texture.EndRender(); }
            hud.Dispose();
            hud = null;
        }

        internal void RenderHud() {
            if (hud == null || hud.Texture == null)
                return;

            try {
                hud.Texture.BeginRender();
                hud.Texture.Clear();
                hud.Texture.Fill(new Rectangle(0, 0, hud.Texture.Width, hud.Texture.Height), Color.FromArgb(0, 0, 0, 0));

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

                if (isHoldingControl) {
                    hud.Texture.DrawLine(new PointF(0, 0), new PointF(hud.Texture.Width - 1, 0), Color.Yellow, 1);
                    hud.Texture.DrawLine(new PointF(hud.Texture.Width - 1, 0), new PointF(hud.Texture.Width - 1, hud.Texture.Height - 1), Color.Yellow, 1);
                    hud.Texture.DrawLine(new PointF(hud.Texture.Width -1, hud.Texture.Height - 1), new PointF(0, hud.Texture.Height - 1), Color.Yellow, 1);
                    hud.Texture.DrawLine(new PointF(0, hud.Texture.Height - 1), new PointF(0, 0), Color.Yellow, 1);
                }

                var leftOffset = (int)(offset * 2) + (int)(arrowTexture.Width * scale);
                hud.Texture.BeginText(fontFace, LabelFontSize, 200, false);
                var coordsText = $"{Math.Abs(TargetNS).ToString("F2")}{(TargetNS > 0 ? "N" : "S")}, {Math.Abs(TargetEW).ToString("F2")}{(TargetEW > 0 ? "E" : "W")}";
                hud.Texture.WriteText(coordsText, Color.White, VirindiViewService.WriteTextFormats.None, new Rectangle(leftOffset, 0, hud.Texture.Width - leftOffset, LabelFontSize));
                var distanceText = $"{(Coordinates.FromString(coordsText).DistanceTo(new Coordinates(ew, ns))):N2}m";
                hud.Texture.WriteText(distanceText, Color.White, VirindiViewService.WriteTextFormats.None, new Rectangle(leftOffset, LabelFontSize + 2, hud.Texture.Width - leftOffset, LabelFontSize));
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
                    UB.Core.WindowMessage -= Core_WindowMessage;
                    UB.Core.RenderFrame -= Core_RenderFrame;
                    UB.Core.ChatBoxMessage -= Core_ChatBoxMessage;
                    UB.Core.ChatNameClicked -= Core_ChatNameClicked;
                    PropertyChanged -= Arrow_PropertyChanged;
                    ClearHud();
                    base.Dispose(disposing);
                }
                disposedValue = true;
            }
        }
    }
}
