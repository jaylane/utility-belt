using System;
using UtilityBelt.Lib;
using UBLoader.Lib.Settings;
using AcClient;
using System.Runtime.InteropServices;
using ImGuiNET;

namespace UtilityBelt.Tools {
    [Name("XPMeter")]
    [Summary("Provides an XP Meter overlay that works past 275")]
    [FullDescription(@"TODO: Write this. This is still under development.")]
    public class XPMeter : ToolBase {
        private UBService.Hud hud;
        #region Config
        [Summary("Enabled")]
        public Setting<bool> Enabled = new Setting<bool>(true);
        [Summary("Show Totals")]
        public Setting<bool> ShowTotals = new Setting<bool>(true);
        [Summary("Show Time")]
        public Setting<bool> ShowTime = new Setting<bool>(true);

        //todo- detach Enabled from hud visibility
        //[Summary("Wether the hud is visible or not.")]
        //public readonly CharacterState<bool> Visible = new CharacterState<bool>(true);

        private void XPMeter_Enabled_Changed(object sender, SettingChangedEventArgs e) {
            if (UBHelper.Core.GameState != UBHelper.GameState.In_Game)
                return;
            TryEnable();
        }
        #endregion // Config
        #region Commands
        #region /ub xpmeter (on|off|reset)
        [Summary("Enable/Disable/Reset XP Meter")]
        [Usage("/ub xpmeter (on|off|reset)")]
        [CommandPattern("xpmeter", @"^(?<cmd>true|false|on|off|reset)$", false)]
        public unsafe void DoXP(string _, System.Text.RegularExpressions.Match args) {
            switch (args.Groups["cmd"].Value.ToLower()) {
                case "off":
                case "false":
                    Enabled.Value = false;
                    WriteToChat($"Disabled.");
                    break;
                case "on":
                case "true":
                    Enabled.Value = true;
                    WriteToChat($"Enabled.");
                    break;
                case "reset":
                    ResetMeter();
                    break;
            }
        }
        #endregion
        #endregion
        #region Expressions
        #region xpreset[]
        [ExpressionMethod("xpreset")]
        [ExpressionReturn(typeof(double), "Returns 1.")]
        [Summary("Resets the XP Meter")]
        [Example("xpreset[]", "1")]
        public object resetxp() {
            ResetMeter();
            return (double)1;
        }
        #endregion //resetxp[]
        #region xpmeter[]
        [ExpressionMethod("xpmeter")]
        [ExpressionReturn(typeof(string), "XP Meter Text")]
        [Summary("Returns the XP Meter Text")]
        [Example("xpmeter[]", "500m XP, 51s, 35.39b XP/hr")]
        public unsafe object xpmeter() => Gloat();
        #endregion //xptotal[]
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
        #region Common Interface
        private unsafe void ResetMeter() {
            WriteToChat($"Resetting XP Meter after {*Timer.cur_time - start_time:n0} seconds, {accum_XP:n0} XP, and {accum_LUM:n0} LUM");
            accum_XP = 0;
            accum_LUM = 0;
            start_time = *Timer.cur_time;
        }
        private unsafe string Gloat() {
            if (hasLuminance) return $"{(ShowTotals ? $"{Util.formatExperience(accum_XP):n0} XP and {Util.formatExperience(accum_LUM):n0} LUM, " : "")}{(ShowTime ? $"{Util.formatDuration(RunTime()):n0}, " : "")}{Util.formatExperience(XP()):n0} XP/hr and {Util.formatExperience(LUM()):n0} LUM/hr";
            else return $"{(ShowTotals ? $"{Util.formatExperience(accum_XP):n0} XP, " : "")}{(ShowTime ? $"{Util.formatDuration(RunTime()):n0}, " : "")}{Util.formatExperience(XP()):n0} XP/hr";
        }
        public Int64 lastXP = 0; // current total XP
        public Int64 lastLUM = 0; // current total LUM
        public Int64 accum_XP = 0; // XP accumulated since `start_time`
        public Int64 accum_LUM = 0; // LUM accumulated since `start_time`
        public Double start_time = 0; // client timestamp of last reset
        public Double XP() => (accum_XP / RunTime()) * 3600; // XP/hr
        public Double LUM() => (accum_LUM / RunTime()) * 3600; // LUM/hr
        public unsafe Double RunTime() => *Timer.cur_time - start_time; //  # of seconds since reset
        #endregion
        #region Initialization
        public XPMeter(UtilityBeltPlugin ub, string name) : base(ub, name) { }
        public override void Init() {
            base.Init();
            Enabled.Changed += XPMeter_Enabled_Changed;
            if (UBHelper.Core.GameState != UBHelper.GameState.In_Game)
                UB.Core.CharacterFilter.LoginComplete += CharacterFilter_LoginComplete;
            else
                TryEnable();
        }
        private void CharacterFilter_LoginComplete(object sender, EventArgs e) {
            UB.Core.CharacterFilter.LoginComplete -= CharacterFilter_LoginComplete;
            TryEnable();
        }
        private void TryEnable() {
            if (Enabled) EnableMeter();
            else Hud_Hide();
        }
        #endregion
        #region Overall Enable/Disable
        private bool hasLuminance = false;
        public unsafe void EnableMeter() {
            if ((*CPhysicsObj.player_object) != null && (*CPhysicsObj.player_object)->weenie_obj != null && (*CPhysicsObj.player_object)->weenie_obj->m_pQualities != null) {
                CBaseQualities* playerQualities = &(*CPhysicsObj.player_object)->weenie_obj->m_pQualities->a0.a1;
                lastXP = playerQualities->Get(STypeInt64.AVAILABLE_EXPERIENCE);
                lastLUM = playerQualities->Get(STypeInt64.AVAILABLE_LUMINANCE);
                hasLuminance = playerQualities->Get(STypeInt64.MAXIMUM_LUMINANCE) != 0 && playerQualities->Get(STypeInt.LEVEL) >= 200;
                // LogError($"Success reading qualities. xp:{lastXP:n0}, lum:{lastLUM:n0}, hasLuminance: {hasLuminance}");
            }
            else {
                LogError($"Error accessing player qualities");
                return;
            }
            if (!ClientObjMaintSystem__Handle_Qualities__PrivateUpdateInt64_hook.Setup(new ClientObjMaintSystem__Handle_Qualities__PrivateUpdateInt64_def(ClientObjMaintSystem__Handle_Qualities__PrivateUpdateInt64))) {
                LogError($"HOOK>ClientObjMaintSystem__Handle_Qualities__PrivateUpdateInt64 install falure");
                return;
            } // else LogError($"HOOK>ClientObjMaintSystem__Handle_Qualities__PrivateUpdateInt64 install success");
            Hud_Show();
            accum_XP = 0;
            accum_LUM = 0;
            start_time = *Timer.cur_time;
        }
        public void DisableMeter() {
            if (!ClientObjMaintSystem__Handle_Qualities__PrivateUpdateInt64_hook.Remove()) {
                LogError($"HOOK>ClientObjMaintSystem__Handle_Qualities__PrivateUpdateInt64 remove falure");
                return;
            } // else LogError($"HOOK>ClientObjMaintSystem__Handle_Qualities__PrivateUpdateInt64 remove success");
            Hud_Hide();
        }
        #endregion
        #region HOOK ClientObjMaintSystem::Handle_Qualities__PrivateUpdateInt64(ClientObjMaintSystem* This, char wts, UInt32 stype, Int64 val);
        internal Hook ClientObjMaintSystem__Handle_Qualities__PrivateUpdateInt64_hook = new AcClient.Hook(0x00559C40, 0x006AF9DB);
        // .text:00559C40 ; public: unsigned long __thiscall ClientObjMaintSystem::Handle_Qualities__PrivateUpdateInt64(unsigned char,unsigned long,__int64)
        // .text:006AF9DB                 call    ?Handle_Qualities__PrivateUpdateInt64@ClientObjMaintSystem@@QAEKEK_J@Z ; ClientObjMaintSystem::Handle_Qualities__PrivateUpdateInt64(uchar,ulong,__int64)
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)] internal unsafe delegate UInt32 ClientObjMaintSystem__Handle_Qualities__PrivateUpdateInt64_def(ClientObjMaintSystem* This, char wts, UInt32 stype, Int64 val);
        private unsafe UInt32 ClientObjMaintSystem__Handle_Qualities__PrivateUpdateInt64(ClientObjMaintSystem* This, char wts, UInt32 stype, Int64 val) {
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
        #endregion
        #region Hud Show/Hide
        internal void Hud_Show() {
            Hud_Hide();
            hud = UBService.HudManager.CreateHud("XP Meter");
            hud.ShowInBar = false;
            hud.ShouldHide += Hud_ShouldHide;
            hud.Render += Hud_Render;
            hud.PreRender += Hud_PreRender;

            hud.WindowSettings |= ImGuiWindowFlags.AlwaysAutoResize;
            hud.WindowSettings |= ImGuiWindowFlags.NoResize;
            hud.WindowSettings |= ImGuiWindowFlags.NoScrollbar;
            hud.WindowSettings |= ImGuiWindowFlags.NoCollapse;
        }
        public void Hud_Hide() {
            if (hud != null) {
                hud.ShouldHide -= Hud_ShouldHide;
                hud.Render -= Hud_Render;
                hud.PreRender -= Hud_PreRender;
                hud.Dispose();
                hud = null;
            }
        }
        #endregion
        #region Hud Events
        private string hudText = "";
        private float additionalHeight = 0;
        private void Hud_PreRender(object sender, EventArgs e) {
            hudText = Gloat();
            if (ImGui.GetIO().KeyShift > 0) {
                if (additionalHeight < 32)
                    additionalHeight += 4f;
            }
            else if (additionalHeight > 0)
                additionalHeight -= 4f;
            hud.Title = hudText;
            var size = new Vector2(-1, 20 + additionalHeight);
            ImGui.SetNextWindowCollapsed(additionalHeight == 0, ImGuiCond.Always);
            ImGui.SetNextWindowSize(size, ImGuiCond.Always);
        }
        private unsafe void Hud_Render(object sender, EventArgs e) {
            if (additionalHeight == 0) {
                ImGui.Text($"{hudText} +++");
            }
            else {
                if (ImGui.Button("Reset", new Vector2(100, 20))) {
                    ResetMeter();
                }
                ImGui.SameLine();
                if (ImGui.Button("/say", new Vector2(50, 20))) {
                    AC1Legacy.PStringBase<char> text = Gloat();
                    CM_Communication.Event_Talk(&text);
                }
            }
        }
        private void Hud_ShouldHide(object sender, EventArgs e) {
            WriteToChat("XP Meter closed. It can be re-opened in the UtilityBelt Settings, under XPMeter, or with the command:  /ub opt set XPMeter.Enabled true");
            Enabled.Value = false;
        }
        #endregion
        #region IDisposable Support
        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    DisableMeter();
                    Enabled.Changed -= XPMeter_Enabled_Changed;
                    UB.Core.CharacterFilter.LoginComplete -= CharacterFilter_LoginComplete;
                }
                disposedValue = true;
            }
        }
        #endregion
    }
}
