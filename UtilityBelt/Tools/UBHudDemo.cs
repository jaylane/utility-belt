using System;
using System.Drawing;
using UBLoader.Lib.Settings;
using UtilityBelt.Lib;

namespace UtilityBelt.Tools {
    [Name("UBHudDemo")]
    [Summary("Demo UI and testbed for UBHud related things")]
    [FullDescription(@"TODO: Write this. This is still under development.")]
    public class UBHudDemo : ToolBase {
        public UBHudDemo(UtilityBeltPlugin ub, string name) : base(ub, name) {
        }
        private UBHud hud;
        private UBHud.Titlebar titlebar;
        private UBHud.Button button;
        private UBHud.Block block;
        private UBHud.Label label;

        #region Config
        [Summary("Enabled")]
        public Setting<bool> Enabled = new Setting<bool>(false);

        [Summary("HUD Position X")]
        public readonly CharacterState<int> HudX = new CharacterState<int>(300);

        [Summary("HUD Position Y")]
        public readonly CharacterState<int> HudY = new CharacterState<int>(225);

        #endregion // Config

        public override void Init() {
            base.Init();
            try {
                Changed += Settings_Changed; ;
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
            if (Enabled) Setup();
            else ClearHud();
        }

        private Decal.Interop.Input.TimerClass drawTimer;
        /// <summary>
        /// safely reset counters, setup hook, start counter, and start hud
        /// </summary>
        public unsafe void Setup() {
            ClearHud();
            CreateHud();
            hud.Render();
        }

        private void Settings_Changed(object sender, SettingChangedEventArgs e) {
            if (UBHelper.Core.GameState != UBHelper.GameState.In_Game)
                return;
            switch (e.PropertyName) {
                case "Enabled":
                    TryEnable();
                    break;
                case "HudX":
                case "HudY":
                    ClearHud();
                    CreateHud();
                    break;
            }
        }

        public void Stop() {
            ClearHud();
        }
        private void Hud_OnMove() {
            int hud_y = hud.BBox.Y;
            HudX.Value = hud.BBox.X;
            HudY.Value = hud_y;
        }
        private void Hud_OnClose() {
            WriteToChat("UBHud Demo has been closed! You can open it again with: /ub opt set UBHudDemo.Enabled True");
            Enabled.Value = false;
        }

        internal void CreateHud() {
            if (drawTimer == null) {
                drawTimer = new Decal.Interop.Input.TimerClass();
                drawTimer.Timeout += DrawTimer_Timeout;
                drawTimer.Start(1000 * 1); // 1 fps max
            }
            if (hud == null) {
                Size size = new Size(300, 200);
                hud = UB.Huds.CreateHud(HudX, HudY, size.Width, size.Height);
                hud.BackgroundColor = 0x7F7F7F7F;
                hud.Transparent = false;

                titlebar = new UBHud.Titlebar(hud, new Rectangle(0, 0, 300, 20), "UBHud Demo", Titlebar_OnClick, true);

                label = new UBHud.Label(hud, new Rectangle(0, 20, size.Width, 25), "Look, a label", null);
                label.FontColor = 0xB0FF4040;
                label.FontSize = 14;
                button = new UBHud.Button(hud, new Rectangle(10, 45, 80, 14), "Button", Button_OnClick, true);
                block = new UBHud.Block(hud, new Rectangle(0, 59, size.Width, 20), "0.475", "Block", "12", "onclick text, whatever", size.Width / 4, Block_OnClick);

                hud.OnRender += Hud_OnRender;
                hud.OnMove += Hud_OnMove;
                hud.OnClose += Hud_OnClose;
            }
        }

        public void ClearHud() {
            if (hud != null) {
                label = null;
                block = null;
                button = null;
                hud.OnRender -= Hud_OnRender;
                hud.OnMove -= Hud_OnMove;
                hud.OnClose -= Hud_OnClose;
                hud.Dispose();
                hud = null;
            }
            if (drawTimer != null) {
                drawTimer.Timeout -= DrawTimer_Timeout;
                drawTimer.Stop();
                drawTimer = null;
            }
        }
        private void DrawTimer_Timeout(Decal.Interop.Input.Timer Source) {

            hud.Render();
        }
        private void Titlebar_OnClick() {
            Logger.WriteToChat($"ooo a titlebar click!");
            //hud.Render();
        }
        private void Button_OnClick() {
            Logger.WriteToChat($"ooo a button click!");
            //hud.Render();
        }
        private void Block_OnClick() {
            Logger.WriteToChat($"ooo a block click!");
            //hud.Render();
        }

        private void Hud_OnRender() {
            try {
                if (hud == null || hud.Texture == null || hud.Texture.IsDisposed)
                    return;
                hud.Texture.BeginRender();
                hud.Texture.Clear();
            }
            catch (Exception ex) { Logger.LogException(ex); }
            finally {
                hud.Texture.EndRender();
            }
        }


        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    Stop();
                    Changed -= Settings_Changed;
                    UB.Core.CharacterFilter.LoginComplete -= CharacterFilter_LoginComplete;
                }
                disposedValue = true;
            }
        }

















    }
}
