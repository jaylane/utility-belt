using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Settings;
using UBLoader.Lib.Settings;
using VirindiViewService.Controls;
using System.IO;
using UtilityBelt.Views;
using System.Drawing;
using System.Runtime.InteropServices;
using Decal.Interop.Input;
using VirindiViewService;
using UtilityBelt.Lib.Dungeon;

namespace UtilityBelt.Tools {
    [Name("Movement")]
    public class Movement : ToolBase {
        private DxTexture arrowTexture = null;
        private DxTexture arrowMapTexture = null;
        private UBHud hud = null;
        private string fontFace;
        private int fontWeight;
        private TimerClass drawTimer;
        private int LabelFontSize = 12;

        public Dictionary<Motion, bool> WantedMotionStatus = new Dictionary<Motion, bool> { { Motion.Forward, false }, { Motion.Backward, false }, { Motion.TurnRight, false }, { Motion.TurnLeft, false }, { Motion.StrafeRight, false }, { Motion.StrafeLeft, false }, { Motion.Walk, false } };
        public Dictionary<Motion, bool> CurrentMotionStatus = new Dictionary<Motion, bool> { { Motion.Forward, false }, { Motion.Backward, false }, { Motion.TurnRight, false }, { Motion.TurnLeft, false }, { Motion.StrafeRight, false }, { Motion.StrafeLeft, false }, { Motion.Walk, false } };

        #region Config
        [Summary("Show movement keys debug ui")]
        public readonly Setting<bool> ShowMovementKeysDebugUI = new Setting<bool>(false);

        [Summary("Movement HUD Position X")]
        public readonly CharacterState<int> HudX = new CharacterState<int>(615);

        [Summary("Movement HUD Position Y")]
        public readonly CharacterState<int> HudY = new CharacterState<int>(5);

        [Summary("Movement HUD Key Size")]
        public readonly CharacterState<int> KeySize = new CharacterState<int>(24);
        #endregion

        #region Commands
        #region /ub getmotion
        [Summary("Gets motion of currently selected object.")]
        [Usage("/ub getmotion")]
        [Example("/ub getmotion", "Tells you which way you're going")]
        [CommandPattern("getmotion", @"^$", false)]
        unsafe public void getmotion(string _, Match _2) {
            int phy = get_physics(selectedID);
            WriteToChat($"Current (UB) Held Keys: Forward: {WantedMotionStatus[Motion.Forward]}, Backward: {WantedMotionStatus[Motion.Backward]}, TurnRight: {WantedMotionStatus[Motion.TurnRight]}, TurnLeft: {WantedMotionStatus[Motion.TurnLeft]}, StrafeRight: {WantedMotionStatus[Motion.StrafeRight]}, StrafeLeft: {WantedMotionStatus[Motion.StrafeLeft]}");
            if (phy == 0) {
                WriteToChat($"Your ID: {character_id(player_object):X8} Combat Style: {current_style(player_object)} forward_speed: {(forward_command(player_object) == 0x41000003 ? 0 : forward_speed(player_object))} sidestep_speed: {(sidestep_command(player_object) == 0 ? 0 : sidestep_speed(player_object))} turn_speed: {(turn_command(player_object) == 0 ? 0 : turn_speed(player_object))}");
                WriteToChat($"State: {state(player_object):X8} Location: 0x{landblock(player_object):X8} [{x(player_object):n6}, {y(player_object):n6}, {z(player_object):n6}] {qw(player_object):n6} {qx(player_object):n6} {qy(player_object):n6} {qz(player_object):n6}");
            }
            else {
                WriteToChat($"Target ID: {character_id(phy):X8} Combat Style: {current_style(phy)} forward_speed: {(forward_command(phy) == 0x41000003 ? 0 : forward_speed(phy))} sidestep_speed: {(sidestep_command(phy) == 0 ? 0 : sidestep_speed(phy))} turn_speed: {(turn_command(phy) == 0 ? 0 : turn_speed(phy))}");
                WriteToChat($"State: {state(player_object):X8} Location: 0x{landblock(phy):X8} [{x(phy):n6}, {y(phy):n6}, {z(phy):n6}] {qw(phy):n6} {qx(phy):n6} {qy(phy):n6} {qz(phy):n6}");
            }
        }
        #endregion
        #region /ub setmotion <motion> <fOn>
        [Summary("Sets a wanted motion, in the client.")]
        [Usage("/ub setmotion <Forward|Backward|TurnRight|TurnLeft|StrafeRight|StrafeLeft|Walk> <0|1>")]
        [Example("/ub setmotion Forward 1", "Makes your character run forward forever.")]
        [Example("/ub setmotion Forward 0", "Might make your character stop running forward.")]
        [CommandPattern("setmotion", @"^(?<motion>\w.+) (?<fOn>[01])$", false)]
        public void setmotion(string _, Match args) {
            int.TryParse(args.Groups["fOn"].Value, out int fOn);
            Motion motion;
            try {
                motion = (Motion)Enum.Parse(typeof(Motion), args.Groups["motion"].Value, true);
            }
            catch {
                Logger.Error($"Invalid option ({args.Groups["motion"].Value}). Valid values are: {string.Join(", ", Enum.GetNames(typeof(Motion)))}");
                return;
            }
            SetMotion(motion, fOn == 1 ? true : false);
        }
        #endregion
        #region /ub clearmotion
        [Summary("Clears all wanted motions, in the client.")]
        [Usage("/ub clearmotion")]
        [Example("/ub clearmotion", "Clears all wanted motions, in the client.")]
        [CommandPattern("clearmotion", @"^$", false)]
        public void clearmotions(string _, Match args) {
            ClearMotions();
        }
        #endregion
        #endregion Commands

        #region Expressions
        #region setmotion[]
        [ExpressionMethod("setmotion")]
        [ExpressionParameter(0, typeof(string), "motion", "The motion to set. Valid motions are Forward|Backward|TurnRight|TurnLeft|StrafeRight|StrafeLeft|Walk")]
        [ExpressionParameter(0, typeof(double), "state", "0 = off, 1 = on")]
        [ExpressionReturn(typeof(double), "Returns 1 if successful, 0 otherwise")]
        [Summary("Sets a characters wanted motion state.")]
        [Example("setmotion[Forward, 1]", "Makes your character move forward until turned off")]
        [Example("setmotion[Forward, 0]", "Makes your character stop moving forward")]
        unsafe public object SetMotion(string wantedMotion, double state) {
            Motion motion;
            try {
                motion = (Motion)Enum.Parse(typeof(Motion), wantedMotion, true);
            }
            catch {
                Logger.Error($"Invalid motion ({wantedMotion}). Valid values are: {string.Join(", ", Enum.GetNames(typeof(Motion)))}");
                return 0;
            }
            SetMotion(motion, state == 0 ? false : true);
            return 1;
        }
        #endregion //setmotion[]
        #region getmotion[]
        [ExpressionMethod("getmotion")]
        [ExpressionParameter(0, typeof(string), "motion", "The motion to get. Valid motions are Forward|Backward|TurnRight|TurnLeft|StrafeRight|StrafeLeft")]
        [ExpressionReturn(typeof(double), "Returns 0 if the motion is inactive and unwanted, -1 if the motion is unwanted but active, 1 if the motion is wanted but inactive, 2 if the motion is wanted and active.")]
        [Summary("Gets a character motion state.")]
        [Example("getmotion[Forward]", "Returns 1 if your character is moving forward, 0 if not")]
        public object GetMotion(string wantedMotion) {
            Motion motion;
            try {
                motion = (Motion)Enum.Parse(typeof(Motion), wantedMotion, true);
            }
            catch {
                Logger.Error($"Invalid motion ({wantedMotion}). Valid values are: {string.Join(", ", Enum.GetNames(typeof(Motion)))}");
                return 0;
            }

            UpdateMovementStatus();
            if (!WantedMotionStatus[motion] && CurrentMotionStatus[motion])
                return -1;
            else if (!WantedMotionStatus[motion] && !CurrentMotionStatus[motion])
                return 0;
            else if (WantedMotionStatus[motion] && !CurrentMotionStatus[motion])
                return 1;
            else
                return 2;
        }
        #endregion //getmotion[]
        #region clearmotion[]
        [ExpressionMethod("clearmotion")]
        [ExpressionReturn(typeof(double), "Returns 1")]
        [Summary("Clears all motion states")]
        [Example("clearmotion[]", "Clears all motion states")]
        public object ClearMotions() {
            foreach (Motion motion in Enum.GetValues(typeof(Motion))) {
                SetMotion(motion, false);
            }

            return 1;
        }
        #endregion //clearmotion[]
        #endregion Expressions

        #region ac client fun
        public enum Motion { Forward = 0x45000005, Backward = 0x45000006, TurnRight = 0x6500000D, TurnLeft = 0x6500000E, StrafeRight = 0x6500000F, StrafeLeft = 0x65000010, Walk = 0x11112222 }
        public unsafe void SetMotion(Motion motion, bool fOn) {
            WantedMotionStatus[motion] = fOn;
            if (motion == Motion.Walk) {
                var myPhysics = (int)UB.Core.Actions.PhysicsObject(UB.Core.CharacterFilter.Id);
                if (*(int*)(*(int*)((*(int*)(myPhysics + 0xC4))) + 0x18) != (fOn ? 1 : 2)) {
                    *(int*)(*(int*)((*(int*)(myPhysics + 0xC4))) + 0x18) = fOn ? 1 : 2;
                    var keys = WantedMotionStatus.Keys.ToList();
                    foreach (var key in keys) {
                        if (key == Motion.Walk)
                            continue;
                        if (WantedMotionStatus[key]) {
                            SetMotion(key, false);
                            SetMotion(key, true);
                        }
                    }
                }
            }
            else {
                ((def_ACCmdInterp__SetMotion)Marshal.GetDelegateForFunctionPointer((IntPtr)0x0058C140, typeof(def_ACCmdInterp__SetMotion)))(*(int*)(*(int*)0x0083DA58 + 0xB8), (int)motion, fOn ? 1 : 0);
            }
        }
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)] internal delegate void def_ACCmdInterp__SetMotion(int ACCmdInterp, int motion, int fOn); // void __thiscall ACCmdInterp::SetMotion(ACCmdInterp *this, unsigned int motion, bool fOn)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] internal delegate bool c();
        public static unsafe void Client_GodMode() => ((c)Marshal.GetDelegateForFunctionPointer((IntPtr)0x006A2920, typeof(c)))();

        double todo; // this does not belong here. Ultimately we need the equivilant of UBHelper.Core and UBHelper.P, for all of this ugly boilerplate
        internal static unsafe int get_physics(int object_id) => ((def_CObjectMaint__GetObjectA)Marshal.GetDelegateForFunctionPointer((IntPtr)0x00508890, typeof(def_CObjectMaint__GetObjectA)))(*(int*)0x00842ADC, object_id);
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)] internal delegate int def_CObjectMaint__GetObjectA(int CObjectMaint, int object_id); // HashBaseData<unsigned long> *__thiscall CObjectMaint::GetObjectA(CObjectMaint *this, unsigned int object_id)


        internal static unsafe StanceMode current_style(int physics) { try { return (StanceMode)(*(int*)(*(int*)((*(int*)(physics + 0xC4))) + 0x48) & 0xFF); } catch { return 0; } }
        internal static unsafe int forward_command(int physics) { try { return *(int*)(*(int*)((*(int*)(physics + 0xC4))) + 0x4C); } catch { return 0; } }
        internal static unsafe float forward_speed(int physics) { try { return *(float*)(*(int*)((*(int*)(physics + 0xC4))) + 0x50); } catch { return 0; } }
        internal static unsafe int sidestep_command(int physics) { try { return *(int*)(*(int*)((*(int*)(physics + 0xC4))) + 0x54); } catch { return 0; } }
        internal static unsafe float sidestep_speed(int physics) { try { return *(float*)(*(int*)((*(int*)(physics + 0xC4))) + 0x58); } catch { return 0; } }
        internal static unsafe int turn_command(int physics) { try { return *(int*)(*(int*)((*(int*)(physics + 0xC4))) + 0x5C); } catch { return 0; } }
        internal static unsafe float turn_speed(int physics) { try { return *(float*)(*(int*)((*(int*)(physics + 0xC4))) + 0x60); } catch { return 0; } }
        internal static unsafe int character_id(int physics) { try { return *(int*)(physics + 0x08); } catch { return 0; } }
        internal static unsafe int landblock(int physics) { try { return *(int*)(physics + 0x4C); } catch { return 0; } }
        internal static unsafe float qw(int physics) { try { return *(float*)(physics + 0x50); } catch { return 0; } }
        internal static unsafe float qx(int physics) { try { return *(float*)(physics + 0x54); } catch { return 0; } }
        internal static unsafe float qy(int physics) { try { return *(float*)(physics + 0x58); } catch { return 0; } }
        internal static unsafe float qz(int physics) { try { return *(float*)(physics + 0x5C); } catch { return 0; } }
        internal static unsafe float x(int physics) { try { return *(float*)(physics + 0x84); } catch { return 0; } }
        internal static unsafe float y(int physics) { try { return *(float*)(physics + 0x88); } catch { return 0; } }
        internal static unsafe float z(int physics) { try { return *(float*)(physics + 0x8C); } catch { return 0; } }
        internal static unsafe int state(int physics) { try { return *(int*)(physics + 0xAC); } catch { return 0; } }

        internal static unsafe int player_object => *(int*)0x00844D68; // this is really a pointer. handled as an int, to keep C#'s pants on.

        internal static unsafe int selectedID => *(int*)0x00871E54;

        // ACE.Entity.StanceMode
        public enum StanceMode {
            Invalid = 0x0,
            HandCombat = 0x3c,
            NonCombat = 0x3d,
            SwordCombat = 0x3e,
            BowCombat = 0x3f,
            SwordShieldCombat = 0x40,
            CrossbowCombat = 0x41,
            UnusedCombat = 0x42,
            SlingCombat = 0x43,
            TwoHandedSwordCombat = 0x44,
            TwoHandedStaffCombat = 0x45,
            DualWieldCombat = 0x46,
            ThrownWeaponCombat = 0x47,
            Magic = 0x49,
        }
        #endregion ac client fun

        public Movement(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        public override void Init() {
            base.Init();
            try {
                fontFace = UB.LandscapeMapView.view.MainControl.Theme.GetVal<string>("DefaultTextFontFace");
                fontWeight = UB.LandscapeMapView.view.MainControl.Theme.GetVal<int>("ViewTextFontWeight");

                Changed += Movement_PropertyChanged;

                if (UBHelper.Core.GameState != UBHelper.GameState.In_Game)
                    UB.Core.CharacterFilter.LoginComplete += CharacterFilter_LoginComplete;
                else
                    TryEnable();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        #region event handlers
        private void Movement_PropertyChanged(object sender, SettingChangedEventArgs e) {
            if (UBHelper.Core.GameState != UBHelper.GameState.In_Game)
                return;
            switch (e.PropertyName) {
                case "ShowMovementKeysDebugUI":
                    TryEnable();
                    break;
                case "KeySize":
                    CreateHud();
                    break;
            }
        }

        private void CharacterFilter_LoginComplete(object sender, EventArgs e) {
            UB.Core.CharacterFilter.LoginComplete -= CharacterFilter_LoginComplete;
            TryEnable();
        }

        private void DrawTimer_Timeout(Timer Source) {
            hud.Render();
        }
        #endregion event handlers

        private void TryEnable() {
            if (ShowMovementKeysDebugUI) {
                CreateHud();
                if (drawTimer == null) {
                    drawTimer = new TimerClass();
                    drawTimer.Timeout += DrawTimer_Timeout;
                    drawTimer.Start(1000 / 30); // 30 fps max
                }
            }
            else {
                if (drawTimer != null)
                    drawTimer.Stop();
                drawTimer = null;
                ClearHud();
            }
        }

        unsafe private void UpdateMovementStatus() {
            var myPhysics = (int)UB.Core.Actions.PhysicsObject(UB.Core.CharacterFilter.Id);
            CurrentMotionStatus[Motion.Forward] = forward_command(myPhysics) != 0x41000003 && forward_speed(myPhysics) > 0;
            CurrentMotionStatus[Motion.Backward] = forward_command(myPhysics) != 0x41000003 && forward_speed(myPhysics) < 0;
            CurrentMotionStatus[Motion.TurnLeft] = turn_command(myPhysics) != 0 && turn_speed(myPhysics) < 0;
            CurrentMotionStatus[Motion.TurnRight] = turn_command(myPhysics) != 0 && turn_speed(myPhysics) > 0;
            CurrentMotionStatus[Motion.StrafeLeft] = sidestep_command(myPhysics) != 0 && sidestep_speed(myPhysics) < 0;
            CurrentMotionStatus[Motion.StrafeRight] = sidestep_command(myPhysics) != 0 && sidestep_speed(myPhysics) > 0;
            CurrentMotionStatus[Motion.Walk] = *(int*)(*(int*)((*(int*)(myPhysics + 0xC4))) + 0x18) == 1;
        }

        #region hud

        internal void CreateHud() {
            try {
                if (hud != null) {
                    ClearHud();
                }
                if (!ShowMovementKeysDebugUI)
                    return;
                hud = UB.Huds.CreateHud(HudX, HudY, KeySize * 3, (KeySize * 2) + 12);
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

        private void Hud_OnRender(object sender, EventArgs e) {
            if (hud == null || hud.Texture == null)
                return;

            try {
                hud.Texture.BeginRender();
                hud.Texture.Clear();
                hud.Texture.Fill(new Rectangle(0, 0, hud.Texture.Width, hud.Texture.Height), Color.FromArgb(0, 0, 0, 0));

                var outerWalkBarColor = CurrentMotionStatus[Motion.Walk] ? Color.Red : Color.White;
                var innerWalkBarColor = WantedMotionStatus[Motion.Walk] ? Color.Red : Color.White;

                hud.Texture.DrawLine(new PointF(1, hud.Texture.Height - 10), new PointF(hud.Texture.Width - 2, hud.Texture.Height - 10), outerWalkBarColor, 5);
                hud.Texture.DrawLine(new PointF(3, hud.Texture.Height - 9), new PointF(hud.Texture.Width - 4, hud.Texture.Height - 9), innerWalkBarColor, 2);

                DrawMovementKey(Motion.TurnLeft, 0, 0, KeySize);
                DrawMovementKey(Motion.Forward, KeySize, 0, KeySize);
                DrawMovementKey(Motion.TurnRight, KeySize * 2, 0, KeySize);
                DrawMovementKey(Motion.StrafeLeft, 0, KeySize, KeySize);
                DrawMovementKey(Motion.Backward, KeySize, KeySize, KeySize);
                DrawMovementKey(Motion.StrafeRight, KeySize * 2, KeySize, KeySize);
            }
            catch (Exception ex) { Logger.LogException(ex); }
            finally {
                hud.Texture.EndRender();
            }
        }

        private void DrawMovementKey(Motion motion, int x, int y, int size) {
            //motionStatus[Motion.Forward]
            var borderSize = 2;
            UpdateMovementStatus();
            bool isPressed = CurrentMotionStatus[motion];
            bool wantsPress = WantedMotionStatus[motion];
            var borderColor = isPressed ? Color.Red : Color.White;
            var textColor = wantsPress ? Color.Red : Color.White;

            hud.Texture.DrawLine(new PointF(x + borderSize, y + borderSize), new PointF(x + size - borderSize * 2, y + borderSize), borderColor, borderSize);
            hud.Texture.DrawLine(new PointF(x + borderSize, y + size - borderSize * 2), new PointF(x + size - borderSize * 2, y + size - borderSize * 2), borderColor, borderSize);
            hud.Texture.DrawLine(new PointF(x + borderSize, y + borderSize), new PointF(x + borderSize, y + size - borderSize * 2), borderColor, borderSize);
            hud.Texture.DrawLine(new PointF(x + size - borderSize * 2, y + borderSize), new PointF(x + size - borderSize * 2, y + size - borderSize * 2), borderColor, borderSize);

            var motionString = motion.ToString();
            if (motionString.Equals("Backward"))
                motionString = "Backup";
            var keyChar = ((char)UB.Core.QueryKeyBoardMap($"Movement{motionString}")).ToString();

            hud.Texture.BeginText(fontFace, (int)(KeySize * 0.4), 200, false);
            hud.DrawShadowText(keyChar, x, y, size, size, textColor, Color.Black, VirindiViewService.WriteTextFormats.Center | VirindiViewService.WriteTextFormats.VerticalCenter);
            hud.Texture.EndText();
        }

        private void Hud_OnClose(object sender, EventArgs e) {
            ShowMovementKeysDebugUI.Value = false;
        }

        private void Hud_OnMove(object sender, EventArgs e) {
            HudX.Value = hud.X;
            HudY.Value = hud.Y;
        }

        private void Hud_OnReMake(object sender, EventArgs e) {

        }
        #endregion hud

        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    Changed -= Movement_PropertyChanged;
                    ClearHud();
                }
                disposedValue = true;
            }
        }
    }
}
