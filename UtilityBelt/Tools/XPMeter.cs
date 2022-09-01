using System;
using UtilityBelt.Lib;
using UBLoader.Lib.Settings;
using AcClient;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Collections.Generic;

namespace UtilityBelt.Tools {
    [Name("XPMeter")]
    [Summary("Provides an XP Meter overlay that works past 275")]
    [FullDescription(@"TODO: Write this. This is still under development.")]
    public class XPMeter : ToolBase {

        /// <summary>
        /// "current" totals
        /// </summary>
        public Int64 lastXP = 0;
        public Int64 lastLUM = 0;

        /// <summary>
        /// amount accumulated since `start_time`
        /// </summary>
        public Int64 accum_XP = 0;
        public Int64 accum_LUM = 0;

        /// <summary>
        /// client timestamp of last reset
        /// </summary>
        public Double start_time = 0;

        public bool hasLuminance = false;

        /// <summary>
        ///  XP/hr
        /// </summary>
        public Double XP() => (accum_XP / RunTime()) * 3600;

        /// <summary>
        ///  LUM/hr
        /// </summary>
        public Double LUM() => (accum_LUM / RunTime()) * 3600;

        /// <summary>
        ///  # of seconds since reset
        /// </summary>
        public unsafe Double RunTime() => *Timer.cur_time - start_time;

        private UBHud hud;
        private UBHud.Button ResetBtn;
        private UBHud.Label XPLabel;

        private const short WM_MOUSEMOVE = 0x0200;
        private const short WM_LBUTTONDOWN = 0x0201;
        private const short WM_LBUTTONUP = 0x0202;

        #region Config
        [Summary("Enabled")]
        public Setting<bool> Enabled = new Setting<bool>(true);

        //[Summary("Wether the hud is visible or not.")]
        //public readonly CharacterState<bool> Visible = new CharacterState<bool>(true);

        [Summary("HUD Position X")]
        public readonly CharacterState<int> HudX = new CharacterState<int>(5);

        [Summary("HUD Position Y")]
        public readonly CharacterState<int> HudY = new CharacterState<int>(45);

        [Summary("HUD Font")]
        public readonly CharacterState<string> HudFont = new CharacterState<string>("Arial");

        [Summary("HUD Font Size")]
        public readonly CharacterState<int> HudFontSize = new CharacterState<int>(10);

        [Summary("text face color")]
        public readonly CharacterState<UInt32> TextColor = new CharacterState<UInt32>(0xB0FFFFFF);

        [Summary("background color")]
        public readonly CharacterState<UInt32> BackgroundColor = new CharacterState<UInt32>(0x7F000000);

        #endregion // Config

        #region Expressions

        #region xpreset[]
        [ExpressionMethod("xpreset")]
        [ExpressionReturn(typeof(double), "Returns 1.")]
        [Summary("Resets the XP Meter")]
        [Example("xpreset[]", "1")]
        public object resetxp() {
            ResetBtn_OnClick();
            return (double)1;
        }
        #endregion //resetxp[]

        #region xpduration[]
        [ExpressionMethod("xpduration")]
        [ExpressionReturn(typeof(double), "Number of seconds the xp meter has been running.")]
        [Summary("Returns the number of seconds the xp meter has been running.")]
        [Example("xpduration[]", "420.69")]
        public unsafe object xpduration() => *Timer.cur_time - start_time;
        #endregion //xpduration[]

        #region xptotal[]
        [ExpressionMethod("xptotal")]
        [ExpressionReturn(typeof(double), "Total XP accumulated")]
        [Summary("Returns the total XP accumulated since last reset.")]
        [Example("xptotal[]", "42")]
        public unsafe object xptotal() => (double)accum_XP;
        #endregion //xptotal[]

        #region lumtotal[]
        [ExpressionMethod("lumtotal")]
        [ExpressionReturn(typeof(double), "Total LUM accumulated")]
        [Summary("Returns the total LUM accumulated since last reset.")]
        [Example("lumtotal[]", "212")]
        public unsafe object lumtotal() => (double)accum_LUM;
        #endregion //lumtotal[]

        #region xpavg[]
        [ExpressionMethod("xpavg")]
        [ExpressionReturn(typeof(double), "Average XP/hr")]
        [Summary("Returns the average XP/hr, since the last reset.")]
        [Example("xpavg[]", "234567890")]
        public unsafe object xpavg() => XP();
        #endregion //xpavg[]

        #region lumavg[]
        [ExpressionMethod("lumavg")]
        [ExpressionReturn(typeof(double), "Average LUM/hr")]
        [Summary("Returns the average LUM/hr, since the last reset.")]
        [Example("lumavg[]", "420")]
        public unsafe object lumavg() => LUM();
        #endregion //lumavg[]

        #endregion //Expressions


        public XPMeter(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        public override void Init() {
            base.Init();
            try {
                Changed += XPMeter_Changed;
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
            if (Enabled) SetupMeter();
            else ClearHud();
        }


        private Decal.Interop.Input.TimerClass drawTimer;
        /// <summary>
        /// safely reset counters, setup hook, start counter, and start hud
        /// </summary>
        public unsafe void SetupMeter() {
            ClearHud();
            CreateHud();
            _hook.Setup(new def(hook));
            try {
                CBaseQualities* playerQualities = &(*CPhysicsObj.player_object)->weenie_obj->m_pQualities->a0.a1;
                lastXP = playerQualities->Get(STypeInt64.AVAILABLE_EXPERIENCE);
                lastLUM = playerQualities->Get(STypeInt64.AVAILABLE_LUMINANCE);
                hasLuminance = playerQualities->Get(STypeInt64.MAXIMUM_LUMINANCE) != 0 && playerQualities->Get(STypeInt.LEVEL) >= 200;
            }
            catch { }
            accum_XP = 0;
            accum_LUM = 0;
            start_time = *Timer.cur_time;
            hud.Render();

        }



        private void XPMeter_Changed(object sender, SettingChangedEventArgs e) {
            if (UBHelper.Core.GameState != UBHelper.GameState.In_Game)
                return;
            switch (e.PropertyName) {
                case "Enabled":
                    TryEnable();
                    break;
                case "HudX":
                case "HudY":
                case "TextColor":
                case "BackgroundColor":
                case "HudFont":
                case "HudFontSize":
                    ClearHud();
                    CreateHud();
                    break;
            }
        }


        /// <summary>
        /// safely remove hook, stop counter, and remove hud
        /// </summary>
        public void Stop() {
            _hook.Remove();
            ClearHud();
        }

        /// <summary>
        /// super secret squirel numbers. ohh wait- these are the entrypoint, and call location for `ClientObjMaintSystem.Handle_Qualities__PrivateUpdateInt64`
        /// </summary>
        internal Hook _hook = new AcClient.Hook(0x00559C40, 0x006AF9DB);

        /// <summary>
        /// boilerplate delegate stuff
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)] internal unsafe delegate UInt32 def(ClientObjMaintSystem* This, char wts, UInt32 stype, Int64 val);

        /// <summary>
        /// Detour function- the client thinks this is ClientObjMaintSystem.Handle_Qualities__PrivateUpdateInt64, so make sure you call the real thing
        /// </summary>
        private unsafe UInt32 hook(ClientObjMaintSystem* This, char wts, UInt32 stype, Int64 val) {
            switch (stype) {
                case 2:
                    if (val - lastXP > 0) accum_XP += val - lastXP;
                    lastXP = val;
                    break;
                case 6:
                    if (val - lastLUM > 0) accum_LUM += val - lastLUM;
                    lastLUM = val;
                    break;
            }
            return This->Handle_Qualities__PrivateUpdateInt64(wts, stype, val);
        }

        private void DrawTimer_Timeout(Decal.Interop.Input.Timer Source) {

            hud.Render();
        }

        private void Hud_OnMove() {
            int hud_y = hud.BBox.Y;
            HudX.Value = hud.BBox.X;
            HudY.Value = hud_y;
        }

        private void Hud_OnClose() {
            WriteToChat("XP Meter closed. It can be re-opened in the UtilityBelt Settings, under XPMeter, or with the command:  /ub opt set XPMeter.Enabled true");
            Enabled.Value = false;
        }

        internal void CreateHud() {
            if (drawTimer == null) {
                drawTimer = new Decal.Interop.Input.TimerClass();
                drawTimer.Timeout += DrawTimer_Timeout;
                drawTimer.Start(1000 * 5); // 0.2 fps max
            }
            if (hud == null) {
                Size size = new Size(61 * HudFontSize, HudFontSize + 6);
                hud = UB.Huds.CreateHud(HudX, HudY, size.Width, size.Height);
                hud.BackgroundColor = BackgroundColor;
                ResetBtn = new UBHud.Button(hud, new Rectangle(0, 0, 80, size.Height), "Reset", ResetBtn_OnClick, true);
                XPLabel = new UBHud.Label(hud, new Rectangle(0, 0, size.Width, size.Height), "... 275+ XP Meter - UtilityBelt ...", null);
                XPLabel.FontColor = TextColor;
                XPLabel.FontFace = HudFont;
                XPLabel.FontSize = HudFontSize;

                hud.OnRender += Hud_OnRender;
                hud.OnMove += Hud_OnMove;
                hud.OnClose += Hud_OnClose;
                hud.OnKey += Hud_OnKey;
            }
        }
        private bool isShiftHeld = false;
        private void Hud_OnKey(UBHud.WinKeys _key, bool isDown, double holdTime) {
            if (_key == UBHud.WinKeys.VK_SHIFT && isDown != isShiftHeld) {
                isShiftHeld = isDown;
                hud.Render();
            }
        }

        private unsafe void ResetBtn_OnClick() {
            Logger.WriteToChat($"Resetting XP Meter after {*Timer.cur_time - start_time:n0} seconds, {accum_XP:n0} XP, and {accum_LUM:n0} LUM");
            accum_XP = 0;
            accum_LUM = 0;
            start_time = *Timer.cur_time;
            hud.Render();
        }
        public void ClearHud() {
            if (hud != null) {
                ResetBtn = null;
                XPLabel = null;
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
        private void Hud_OnRender() {
            // also hud.HudManager.Keys[UBHud.WinKeys.VK_SHIFT]
            if (isShiftHeld) {
                ResetBtn.Visible = true;
                XPLabel.Text = "";
                XPLabel.BBox = new Rectangle(ResetBtn.BBox.Width, 0, hud.BBox.Width - ResetBtn.BBox.Width, hud.BBox.Height);
            }
            else {
                ResetBtn.Visible = false;
                XPLabel.BBox = new Rectangle(0, 0, hud.BBox.Width, hud.BBox.Height);
                if (hasLuminance)
                    XPLabel.Text = $"Gained {Util.formatExperience(accum_XP):n0} XP and {Util.formatExperience(accum_LUM):n0} LUM over {Util.formatDuration(RunTime()):n0}, for {Util.formatExperience(XP()):n0} XP/hr and {Util.formatExperience(LUM()):n0} LUM/hr";
                else
                    XPLabel.Text = $"Gained {accum_XP:n0} XP over {RunTime():n0} seconds, for {XP():n0} XP/hr";
            }
            //}

        }

        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    Stop();
                    Changed -= XPMeter_Changed;
                    UB.Core.CharacterFilter.LoginComplete -= CharacterFilter_LoginComplete;
                }
                disposedValue = true;
            }
        }


        //private struct StatisticsData<T> {
        //    internal T accum;
        //    internal Double accumTime;
        //    internal T startValue;
        //    internal Queue<T> sinceLastN;
        //    internal T[] Nth;
        //    internal double NextNthPeriod;
        //    internal int period = 10;
        //}

    }


}
