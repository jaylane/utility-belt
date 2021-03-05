using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UtilityBelt.Lib;
using UtilityBelt.Views;
using UBLoader.Lib.Settings;
using System.Collections.ObjectModel;
using UtilityBelt.Lib.Networking;
using UtilityBelt.Lib.Settings;
using Decal.Interop.Input;
using System.Drawing;
using VirindiViewService;
using UtilityBelt.Lib.Dungeon;
using UBNetworking.Lib;
using UBNetworking.Messages;
using UtilityBelt.Lib.Networking.Messages;
using Microsoft.DirectX;
using System.Windows.Forms;

namespace UtilityBelt.Tools {
    [Name("NetworkUI")]
    [Summary("Provides a hud for drawing active network clients. There are also options to follow / use selected item when the full window is show.")]
    [FullDescription(@"
Provides a hud for drawing active network clients. There are also options to follow / use selected item when the full window is show.

![](/screenshots/NetworkUI/mainwindow.png)

To enable, set `NetworkUI.Enabled` to true.  A new icon will appear on your vvs bar, and the hud should start drawing immediately. Other characters need to have `VTank.VitalSharing` enabled for information to be sent across the wire. This option will likely change in the future to be specific to ub networking.

Holding ctrl allows you to click and drag the hud to reposition it, while the window is closed.  Holding shift and clicking on character vitals will select that character.
    ")]
    public class NetworkUI : ToolBase {
        private NetworkClientsView clientsView;
        private UBHud hud = null;
        private string fontFace;
        private TimerClass drawTimer;
        private DxTexture arrowTexture = null;
        private int lastClientCount = 0;
        private IEnumerable<ClientInfo> clients;
        private ClientInfo mouseDownClient;
        private const int HUD_X_OFFSET = 4;
        private const int HUD_Y_OFFSET = 60;
        private const int ROW_SIZE = 20;
        private const int HUD_WIDTH = 355;
        private const int CHAR_NAME_WIDTH = 180;
        private const int HEALTH_BAR_HEIGHT = 11;
        private const int TRACKED_ITEM_WIDTH = 24;
        private const int RANGE_WIDTH = 34;
        private const int PADDING = 4;

        private const short WM_MOUSEMOVE = 0x0200;
        private const short WM_LBUTTONDOWN = 0x0201;
        private const short WM_LBUTTONUP = 0x0202;

        #region Config
        [Summary("Show network clients ui (for viewing / controlling networked characters)")]
        public readonly Setting<bool> Enabled = new Setting<bool>(false);

        [Summary("Show hud when main window is closed")]
        public readonly Setting<bool> ShowHudWhenClosed = new Setting<bool>(true);

        [Summary("Network UI Window X position for this character (left is 0)")]
        public readonly CharacterState<int> WindowPositionX = new CharacterState<int>(22);

        [Summary("Network UI Window Y position for this character (top is 0)")]
        public readonly CharacterState<int> WindowPositionY = new CharacterState<int>(105);

        [Summary("Tracked Items")]
        public readonly Setting<ObservableCollection<TrackedItem>> TrackedItems = new Setting<ObservableCollection<TrackedItem>>(new ObservableCollection<TrackedItem>());
        #endregion Config

        public NetworkUI(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        public override void Init() {
            base.Init();
            fontFace = UB.LandscapeMapView.view.MainControl.Theme.GetVal<string>("DefaultTextFontFace");
            Enabled.Changed += Enabled_Changed;
            ShowHudWhenClosed.Changed += ShowHudWhenClosed_Changed;

            TryStart();
        }

        private void TryStart() {
            try {
                if (Enabled) {
                    CreateTextures();
                    clientsView = new NetworkClientsView(UB);
                    CreateHud();
                    UB.Networking.AddMessageHandler<PlayerUpdateMessage>(Handle_PlayerUpdateMessage);
                    UB.Networking.AddMessageHandler<TrackedItemUpdateMessage>(Handle_TrackedItemUpdateMessage);
                    UB.Networking.AddMessageHandler<CharacterPositionMessage>(Handle_CharacterPositionMessage);
                    clientsView.view.Moved += View_Moved;
                    clientsView.view.VisibleChanged += View_VisibleChanged;
                    
                    if (drawTimer == null) {
                        drawTimer = new TimerClass();
                        drawTimer.Timeout += DrawTimer_Timeout;
                        drawTimer.Start(1000 / 20); // 20 fps max
                    }
                }
                else {
                    Stop();
                }
            }
            catch (Exception ex) { Logger.LogException(ex); } 
        }

        private void Stop() {
            if (drawTimer != null)
                drawTimer.Stop();
            drawTimer = null;
            UB.Networking.RemoveMessageHandler<PlayerUpdateMessage>(Handle_PlayerUpdateMessage);
            UB.Networking.RemoveMessageHandler<TrackedItemUpdateMessage>(Handle_TrackedItemUpdateMessage);
            UB.Networking.RemoveMessageHandler<CharacterPositionMessage>(Handle_CharacterPositionMessage);
            ClearHud();
            if (clientsView != null) {
                clientsView.view.Moved -= View_Moved;
                clientsView.view.VisibleChanged -= View_VisibleChanged;
                clientsView.Dispose();
                clientsView = null;
            }
            arrowTexture?.Dispose();
        }

        #region event handlers
        private void Enabled_Changed(object sender, SettingChangedEventArgs e) {
            TryStart();
        }

        private void ShowHudWhenClosed_Changed(object sender, SettingChangedEventArgs e) {
            if (hud != null && clientsView != null) {
                hud.Enabled = clientsView.view.Visible ? true : ShowHudWhenClosed;
                if (hud.Enabled)
                    hud.Render();
            }
        }

        private void DrawTimer_Timeout(Decal.Interop.Input.Timer Source) {
            try {
                hud.Render();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void View_Moved(object sender, EventArgs e) {
            hud.Move(clientsView.view.Location.X + HUD_X_OFFSET, clientsView.view.Location.Y + HUD_Y_OFFSET);
            hud.Hud.ZPriority = clientsView.view.ForcedZOrder + 1;
        }

        private void View_VisibleChanged(object sender, EventArgs e) {
            hud.IsCloseable = !clientsView.view.Visible;
            hud.IsDraggable = !clientsView.view.Visible;
            if (clientsView.view.Visible) {
                hud.Enabled = true;
                hud.Render();
            }
            else if (!ShowHudWhenClosed) {
                hud.Enabled = false;
            }
        }

        private void Hud_OnClose(object sender, EventArgs e) {
            //Visible.Value = false;
        }

        private void Hud_OnMove(object sender, EventArgs e) {
            clientsView.view.Location = new Point(hud.X - HUD_X_OFFSET, hud.Y - HUD_Y_OFFSET);
            hud.Hud.ZPriority = clientsView.view.ForcedZOrder + 1;
        }

        private void Hud_OnReMake(object sender, EventArgs e) {
            CreateTextures();
            hud.Hud.ZPriority = clientsView.view.ForcedZOrder + 1;
        }

        private void Hud_OnWindowMessage(object sender, Decal.Adapter.WindowMessageEventArgs e) {
            var isShift = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;
            var mousePos = new Point(e.LParam);
            var x = mousePos.X - hud.X;
            var y = mousePos.Y - hud.Y;
            if (!isShift || e.Msg == WM_MOUSEMOVE || clients == null || x > CHAR_NAME_WIDTH || y > ROW_SIZE * clients.Count())
                return;
            var offset = (int)Math.Floor((double)y / ROW_SIZE);
            if (offset >= 0 && offset < clients.Count()) {
                var activeClient = clients.ElementAt(offset);
                switch (e.Msg) {
                    case WM_LBUTTONDOWN:
                        mouseDownClient = activeClient;
                        e.Eat = true;
                        break;
                    case WM_LBUTTONUP:
                        if (mouseDownClient != null && mouseDownClient == activeClient) {
                            // clicked activeClient
                            Logger.WriteToChat($"Selecting Character: {activeClient.Name} (0x{activeClient.PlayerId:X8})");
                            UB.Core.Actions.SelectItem(activeClient.PlayerId);
                            e.Eat = true;
                        }
                        break;
                }
            }
        }

        private void Handle_PlayerUpdateMessage(MessageHeader header, PlayerUpdateMessage message) {
            var client = UB.Networking.Clients.Where(c => c.Key == header.SendingClientId).FirstOrDefault();
            if (client.Value != null) {
                DrawClient(client.Value);
            }
        }

        private void Handle_TrackedItemUpdateMessage(MessageHeader header, TrackedItemUpdateMessage message) {
            var client = UB.Networking.Clients.Where(c => c.Key == header.SendingClientId).FirstOrDefault();
            if (client.Value != null) {
                DrawClient(client.Value);
            }
        }

        private void Handle_CharacterPositionMessage(MessageHeader header, CharacterPositionMessage message) {
            var client = UB.Networking.Clients.Where(c => c.Key == header.SendingClientId).FirstOrDefault();
            if (client.Value != null) {
                DrawClient(client.Value);
            }
        }
        #endregion event handlers

        #region helper methods
        public string FormatNumberForDisplay(double number, int defaultPrecision = 0) {
            if (number < 1000)
                return number.ToString($"N{defaultPrecision}");
            else if (number < 10000)
                return (number / 1000).ToString("N1") + "k";
            else
                return (number / 1000).ToString("N0") + "k";
        }

        private string GetActiveTab() {
            string tag = "All";
            if (clientsView.NotebookTags != null && clientsView.NotebookTags.TabCount > 0) {
                var currentTab = clientsView.NotebookTags[clientsView.NotebookTags.CurrentTab];
                tag = currentTab.InternalName;
            }
            return tag;
        }

        private int GetActiveClientCount() {
            var tag = GetActiveTab();
            return UB.Networking.Clients.Values.Where(c => tag == "All" || c.Tags.Contains(tag)).Count();
        }

        private void CreateTextures() {
            if (arrowTexture != null)
                arrowTexture.Dispose();

            arrowTexture = TextureCache.TextureFromBitmapResource("UtilityBelt.Resources.icons.arrow.png");
        }
        #endregion helper methods

        #region Hud Rendering
        internal void CreateHud() {
            try {
                if (!Enabled)
                    return;

                var activeClientCount = GetActiveClientCount();
                var x = clientsView.view.Location.X + 4;
                var y = clientsView.view.Location.Y + 60;
                var width = HUD_WIDTH;
                var height = Math.Max(activeClientCount * ROW_SIZE, ROW_SIZE) + 5;
                if (hud != null) {
                    var enabled = hud.Enabled;
                    hud.Move(x, y);
                    hud.Resize(width, height);
                    hud.Enabled = enabled;
                    clientsView.view.Height = 60 + activeClientCount * 20;
                    return;
                }
                hud = UB.Huds.CreateHud(x, y, width, height);
                hud.IsCloseable = !clientsView.view.Visible;
                hud.IsDraggable = !clientsView.view.Visible;
                hud.Enabled = clientsView.view.Visible ? true : ShowHudWhenClosed;
                hud.OnMove += Hud_OnMove;
                hud.OnClose += Hud_OnClose;
                hud.OnRender += Hud_OnRender;
                hud.OnReMake += Hud_OnReMake;
                hud.OnWindowMessage += Hud_OnWindowMessage;
                hud.Render();
                hud.Hud.ZPriority = clientsView.view.ForcedZOrder + 1;
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
            hud.OnWindowMessage += Hud_OnWindowMessage;
            hud.Dispose();
            hud = null;
        }

        private void Hud_OnRender(object sender, EventArgs e) {
            try {
                var tag = GetActiveTab();
                clients = UB.Networking.Clients.Where((c) => {
                    return (tag == "All" || c.Value.Tags.Contains(tag)) && c.Value.LastUpdate != DateTime.MinValue;
                }).Select(c => c.Value);
                if (GetActiveClientCount() != lastClientCount)
                    CreateHud();
                lastClientCount = GetActiveClientCount();
                hud.Hud.ZPriority = clientsView.view.ForcedZOrder + 1;

                hud.Texture.BeginRender();
                hud.Texture.Fill(new Rectangle(0, 0, hud.Texture.Width, hud.Texture.Height), Color.Transparent);
                var offset = 0;
                foreach (var client in clients) {
                    if (tag == "All" || client.Tags.Contains(tag)) {
                        DrawClientTextures(hud.Texture, offset, client);
                        offset += ROW_SIZE;
                    }
                }
                try {
                    hud.Texture.BeginText("Arial", 7, 100, false, 1, 255);
                    offset = 0;
                    foreach (var client in clients) {
                        if (tag == "All" || client.Tags.Contains(tag)) {
                            DrawClientText(hud.Texture, offset, client);
                            offset += ROW_SIZE;
                        }
                    }
                }
                catch (Exception ex) { Logger.LogException(ex); }
                finally {
                    hud.Texture.EndText();
                }

                try {
                    hud.Texture.BeginText("Arial", 5, 100, false);
                    //DrawClientTextSmall(hud.Texture, 0, null);
                    offset = 0;
                    foreach (var client in clients) {
                        if (tag == "All" || client.Tags.Contains(tag)) {
                            DrawClientTextSmall(hud.Texture, offset, client);
                            offset += ROW_SIZE;
                        }
                    }
                }
                catch (Exception ex) { Logger.LogException(ex); }
                finally {
                    hud.Texture.EndText();
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
            finally {
                hud.Texture.EndRender();
            }
        }

        private void DrawClient(ClientInfo client) {
            //todo: draw only the updated client..
            //hud.Render();
        }

        private void DrawClientTextures(DxTexture texture, int offset, ClientInfo client) {
            DrawCharNameBackground(texture, offset, client);
            DrawRangeIndicator(texture, offset, client);
            DrawCharTrackedItems(texture, offset, client);
        }

        private void DrawRangeIndicator(DxTexture texture, int offset, ClientInfo client) {
            var distance = client.DistanceTo();
            var me = UB.Core.CharacterFilter.Id;
            var ew = Geometry.LandblockToEW((uint)PhysicsObject.GetLandcell(me), PhysicsObject.GetPosition(me).X);
            var ns = Geometry.LandblockToNS((uint)PhysicsObject.GetLandcell(me), PhysicsObject.GetPosition(me).Y);
            float scale = (ROW_SIZE * 0.75f) / arrowTexture.Width;
            var rotationCenter = new Vector3((float)arrowTexture.Width / 2 * scale, (float)arrowTexture.Height / 2 * scale, 0);
            var heading = -(((Math.Atan2(client.NS - ns, client.EW - ew) * 180 / Math.PI) + UB.Core.Actions.Heading) % 360);
            if (UB.Core.CharacterFilter.Id == client.PlayerId)
                heading = 270;
            Quaternion rotQuat = Geometry.HeadingToQuaternion(360f - (float)(heading - 270f));
            var transform = new Matrix();
            var c = (int)Math.Max(0, Math.Min(255, 255 - (distance * 2)));
            var tint = Color.FromArgb(255, 255, c, c);

            offset = (int)(offset + (ROW_SIZE - (arrowTexture.Width * scale)) / 2);
            transform.AffineTransformation(scale, rotationCenter, rotQuat, new Vector3(CHAR_NAME_WIDTH + PADDING, offset, 0));
            hud.Texture.DrawTextureWithTransform(arrowTexture, transform, tint.ToArgb());
        }

        private void DrawCharTrackedItems(DxTexture texture, int offset, ClientInfo client) {
            var x = CHAR_NAME_WIDTH + PADDING + RANGE_WIDTH;
            if (client == null)
                return;
            int iconSize = (int)(ROW_SIZE - 1);
            foreach (var item in client.TrackedItems) {
                texture.DrawPortalImageNoBorder(item.Icon, new Rectangle(x + ((TRACKED_ITEM_WIDTH - iconSize) / 2), offset + 1, iconSize, iconSize));
                x += TRACKED_ITEM_WIDTH;
            }
        }

        private void DrawCharNameBackground(DxTexture texture, int offset, ClientInfo client) {
            var opacity = 180;
            var area = new Rectangle(1, offset + 1, HUD_WIDTH - 2, ROW_SIZE - 2);
            var topLeft = new PointF(area.Left, area.Top);
            var topRight = new PointF(area.Left + CHAR_NAME_WIDTH, area.Top);
            var bottomRight = new PointF(area.Left + CHAR_NAME_WIDTH, area.Top + area.Height);
            var bottomLeft = new PointF(area.Left, area.Top + area.Height);
            var health = (double)client.CurrentHealth / client.MaxHealth;
            var stamina = (double)client.CurrentStamina / client.MaxStamina;
            var mana = (double)client.CurrentMana / client.MaxMana;
            // health bar
            texture.Fill(new Rectangle(area.Left, area.Top, CHAR_NAME_WIDTH, HEALTH_BAR_HEIGHT), Color.FromArgb(opacity, 60, 0, 0));
            texture.Fill(new Rectangle(area.Left, area.Top, (int)(CHAR_NAME_WIDTH * health), HEALTH_BAR_HEIGHT), Color.FromArgb(opacity, 255, 0, 0));
            // stamina bar
            texture.Fill(new Rectangle(area.Left, area.Top + HEALTH_BAR_HEIGHT, CHAR_NAME_WIDTH / 2, area.Height - HEALTH_BAR_HEIGHT), Color.FromArgb(opacity, 70, 30, 0));
            texture.Fill(new Rectangle(area.Left, area.Top + HEALTH_BAR_HEIGHT, (int)(CHAR_NAME_WIDTH / 2 * stamina), area.Height - HEALTH_BAR_HEIGHT), Color.FromArgb(opacity, 255, 180, 0));
            // mana bar
            texture.Fill(new Rectangle(area.Left + (CHAR_NAME_WIDTH / 2), area.Top + HEALTH_BAR_HEIGHT, (CHAR_NAME_WIDTH / 2), area.Height - HEALTH_BAR_HEIGHT), Color.FromArgb(opacity, 0, 20, 55));
            texture.Fill(new Rectangle(area.Left + (CHAR_NAME_WIDTH / 2), area.Top + HEALTH_BAR_HEIGHT, (int)(CHAR_NAME_WIDTH / 2 * mana), area.Height - HEALTH_BAR_HEIGHT), Color.FromArgb(opacity, 0, 80, 255));
            // outside border of vitals area
            texture.DrawLine(topLeft, topRight, Color.Black, 1);
            texture.DrawLine(topRight, bottomRight, Color.Black, 1);
            texture.DrawLine(bottomRight, bottomLeft, Color.Black, 1);
            texture.DrawLine(bottomLeft, topLeft, Color.Black, 1);
        }

        private void DrawClientText(DxTexture texture, int offset, ClientInfo client) {
            var area = new Rectangle(0, offset, HUD_WIDTH, ROW_SIZE);
            // character name in the vitals area
            var characterName = client == null ? UB.Core.CharacterFilter.Name : $"{client.Name}";
            texture.WriteText(characterName, Color.White, VirindiViewService.WriteTextFormats.SingleLine, new Rectangle(area.X + 2, area.Y + 1, CHAR_NAME_WIDTH, ROW_SIZE));
            // health % in the vitals area
            texture.WriteText($"{(client.CurrentHealth / Math.Max(client.MaxHealth, 1)) * 100:N0}%", Color.White, VirindiViewService.WriteTextFormats.Right, new Rectangle(area.X + 2, area.Y + 1, CHAR_NAME_WIDTH, ROW_SIZE));

            // range
            var c = (int)Math.Max(0, Math.Min(255, 255 - (client.DistanceTo() * 2)));
            if (UB.Core.Actions.IsValidObject(client.PlayerId) || client.HasPositionInfo) {
                texture.WriteText(FormatNumberForDisplay(client.DistanceTo()), Color.FromArgb(255, 255, c, c), VirindiViewService.WriteTextFormats.SingleLine, new Rectangle(CHAR_NAME_WIDTH + PADDING + ROW_SIZE - 4, offset + 5, RANGE_WIDTH, 12));
            }
            else {
                texture.WriteText("??", Color.Gray, VirindiViewService.WriteTextFormats.SingleLine, new Rectangle(CHAR_NAME_WIDTH + PADDING + ROW_SIZE - 4, offset + 5, RANGE_WIDTH, 12));
            }

            // tracked item counts
            if (client != null) {
                var x = CHAR_NAME_WIDTH + PADDING + RANGE_WIDTH;
                foreach (var item in client.TrackedItems) {
                    texture.WriteText(FormatNumberForDisplay(item.Count), Color.White, VirindiViewService.WriteTextFormats.Center, new Rectangle(x, area.Y + ROW_SIZE - 10, TRACKED_ITEM_WIDTH, ROW_SIZE));
                    x += TRACKED_ITEM_WIDTH;
                }
            }
        }

        private void DrawClientTextSmall(DxTexture texture, int offset, ClientInfo client) {
            var area = new Rectangle(0, offset, HUD_WIDTH, ROW_SIZE);
            // stamina % in the vitals area
            texture.WriteText($"{((double)client.CurrentStamina / Math.Max(client.MaxStamina, 1)) * 100:N0}%", Color.FromArgb(255, 0, 0, 0), VirindiViewService.WriteTextFormats.SingleLine, new Rectangle(area.X + 2, area.Y + HEALTH_BAR_HEIGHT, CHAR_NAME_WIDTH, ROW_SIZE));
            // mana % in the vitals area
            texture.WriteText($"{((double)client.CurrentMana / Math.Max(client.MaxMana, 1)) * 100:N0}%", Color.FromArgb(255, 0, 0, 0), VirindiViewService.WriteTextFormats.SingleLine, new Rectangle(area.X + 2 + (CHAR_NAME_WIDTH / 2), area.Y + HEALTH_BAR_HEIGHT, CHAR_NAME_WIDTH, ROW_SIZE));
        }
        #endregion Hud Rendering

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            Stop();
        }
    }
}
