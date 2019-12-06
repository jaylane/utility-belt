using Decal.Adapter;
using System;
using Decal.Adapter.Wrappers;
using Decal.Filters;
using UtilityBelt.MagTools.Shared;
using System.Text.RegularExpressions;
using VirindiViewService.Controls;
using UtilityBelt.Lib;
using System.ComponentModel;

namespace UtilityBelt.Tools {
    [Name("Jumper")]
    [Summary("Used to jump and face heading, with built-in VTank pausing.")]
    [FullDescription(@"
Jumper is used for well... Jumping and turning. These commands will turn off Vtank navigation while running and set back to previous state once complete
    ")]
    public class Jumper : ToolBase {
        private bool isTurning = false;
        private bool needToTurn = false;
        private bool needToJump = false;
        private bool addW = false;
        private bool addZ = false;
        private bool addX = false;
        private bool addC = false;
        private bool addShift = false;
        private int msToHoldDown;
        private int faceDirectionInt;
        private int targetDirection;
        private DateTime lastThought = DateTime.MinValue;
        private DateTime turningSeconds = DateTime.MinValue;
        private DateTime navSettingTimer = DateTime.MinValue;
        private TimeSpan enableNavTimer;
        private bool waitingForJump = false;
        private int jumpTries = 0;

        #region Config
        [Summary("PauseNav")]
        [DefaultValue(true)]
        public bool PauseNav {
            get { return (bool)GetSetting("PauseNav"); }
            set { UpdateSetting("PauseNav", value); }
        }

        [Summary("ThinkComplete")]
        [DefaultValue(false)]
        public bool ThinkComplete {
            get { return (bool)GetSetting("ThinkComplete"); }
            set { UpdateSetting("ThinkComplete", value); }
        }

        [Summary("ThinkFail")]
        [DefaultValue(false)]
        public bool ThinkFail {
            get { return (bool)GetSetting("ThinkFail"); }
            set { UpdateSetting("ThinkFail", value); }
        }

        [Summary("Attempts")]
        [DefaultValue(3)]
        public int Attempts {
            get { return (int)GetSetting("Attempts"); }
            set { UpdateSetting("Attempts", value); }
        }
        #endregion

        #region Commands

        #region /ub face
        [Summary("Face heading commands with built in VTank pausing and retries")]
        [Usage("/ub face <heading>")]
        [Example("/ub face 180", "Faces your character towards 180 degrees (south).")]
        [CommandPattern("face", @"^ *(?<Heading>\d+) *$")]
        public void DoFace(string command, Match args) {
            if (!int.TryParse(args.Groups["Heading"].Value, out targetDirection)) {
                LogError("Invalid jump heading: " + args.Groups["Heading"].Value);
                return;
            }
            isTurning = true;
            turningSeconds = DateTime.UtcNow;
            if (PauseNav)
                UBHelper.vTank.Decision_Lock(uTank2.ActionLockType.Navigation, TimeSpan.FromMilliseconds(15000));

            needToTurn = false;
            UBHelper.Core.TurnToHeading(targetDirection);
            LogDebug("Turning to " + targetDirection);
        }
        #endregion

        #region /ub jump
        [Summary("Jump commands with built in VTank pausing and retries")]
        [Usage("/ub jump[swzxc] [heading] [holdtime]")]
        [Example("/ub jumpsw 180 500", "Face 180 degrees (south) and jump forward with 500/1000 power.")]
        [Example("/ub jumpsx 300", "Jump backward with 300/1000 power.")]
        [Example("/ub jump", "Taps jump.")]
        [CommandPattern("jump", @"^ *((?<faceDirection>\d+)\s+)?(?<msToHoldDown>\d+)?$", true)]
        public void DoJump(string command, Match args) {
            if (needToJump || waitingForJump) {
                LogError("You are already jumping. try again later.");
                return;
            }
            needToJump = true;

            //set jump duration in ms
            if (!string.IsNullOrEmpty(args.Groups["msToHoldDown"].Value)) {
                if (!int.TryParse(args.Groups["msToHoldDown"].Value, out msToHoldDown)) return;
                if (msToHoldDown < 0 || msToHoldDown > 1000) {  //check jump held for 0-1000 ms
                    LogError("holdtime should be a number between 0 and 1000");
                    return;
                }
            } else { msToHoldDown = 0; }
            if (!string.IsNullOrEmpty(args.Groups["faceDirection"].Value)) {
                if (!int.TryParse(args.Groups["faceDirection"].Value, out faceDirectionInt)) return;
                if (faceDirectionInt < 0 || faceDirectionInt > 359) {
                    LogError("direction should be a number between 0 and 359");
                    return;
                }
                needToTurn = true;
                targetDirection = faceDirectionInt;
            }
            else needToTurn = false;

            //set jump direction
            if (command.Contains("w")) addW = true;
            if (command.Contains("z")) addZ = true;
            if (command.Contains("x")) addX = true;
            if (command.Contains("c")) addC = true;
            if (command.Contains("s")) addShift = true;

            if (PauseNav) {
                UBHelper.vTank.Decision_Lock(uTank2.ActionLockType.Navigation, TimeSpan.FromMilliseconds(20000));
                UBHelper.vTank.Decision_Lock(uTank2.ActionLockType.CorpseOpenAttempt, TimeSpan.FromMilliseconds(20000));
            }
            needToJump = true;
            //start turning
            isTurning = needToTurn;
            if (needToTurn) {
                turningSeconds = DateTime.UtcNow;
                needToTurn = false;
                UBHelper.Core.TurnToHeading(targetDirection);
            }
            UB.Core.RenderFrame += Core_RenderFrame;
        }
        #endregion
        #endregion

        public Jumper(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        public void Core_RenderFrame(object sender, EventArgs e) {
            if (isTurning && DateTime.UtcNow - lastThought >= TimeSpan.FromMilliseconds(100)) {
                lastThought = DateTime.UtcNow;
                if (targetDirection == Math.Round(CoreManager.Current.Actions.Heading, 0)) {
                    if (ThinkComplete && !needToJump)
                        Util.Think("Turning Success");
                    isTurning = false;
                    if (!needToJump) {
                        UBHelper.vTank.Decision_UnLock(uTank2.ActionLockType.Navigation);
                        UBHelper.vTank.Decision_UnLock(uTank2.ActionLockType.CorpseOpenAttempt);
                    }
                } else UBHelper.Core.TurnToHeading(targetDirection); // giv'er!
            }

            //abort turning if takes longer than 15 seconds
            if (isTurning && DateTime.UtcNow - turningSeconds >= TimeSpan.FromSeconds(15)) {
                isTurning = needToJump = false;
                Util.ThinkOrWrite("Turning failed", ThinkFail);
                UBHelper.vTank.Decision_UnLock(uTank2.ActionLockType.Navigation);
                UBHelper.vTank.Decision_UnLock(uTank2.ActionLockType.CorpseOpenAttempt);
            }
            //Do the jump thing
            if (needToJump && !isTurning) {
                needToJump = false;
                enableNavTimer = TimeSpan.FromMilliseconds(msToHoldDown + 15000);
                UBHelper.Jumper.JumpComplete += Jumper_JumpComplete;
                UBHelper.Jumper.Jump((float)(msToHoldDown / 1000), addShift, addW, addX, addZ, addC);
                waitingForJump = true;
                
                navSettingTimer = DateTime.UtcNow;
            }
            //Set vtank nav setting back to original state after jump/turn complete
            if (waitingForJump && DateTime.UtcNow - navSettingTimer >= enableNavTimer) {
                jumpTries++;
                if (jumpTries < Attempts) {
                    navSettingTimer = DateTime.UtcNow;
                    //I don't know if this can even still fail.....
                    Logger.Debug("Timeout waiting for jump, trying again...");
                    if (PauseNav)
                        UBHelper.vTank.Decision_Lock(uTank2.ActionLockType.Navigation, TimeSpan.FromMilliseconds(15000));
                    UBHelper.Jumper.Jump((float)(msToHoldDown / 1000), addShift, addW, addX, addZ, addC);
                } else {
                    Util.ThinkOrWrite("You have failed to jump too many times.", ThinkFail);

                    UBHelper.vTank.Decision_UnLock(uTank2.ActionLockType.Navigation);
                    UBHelper.vTank.Decision_UnLock(uTank2.ActionLockType.CorpseOpenAttempt);
                    waitingForJump = false;
                    UB.Core.RenderFrame -= Core_RenderFrame;
                    //clear settings
                    addShift = addW = addZ = addX = addC = false;
                    jumpTries = 0;
                }
            }
        }


        private void Jumper_JumpComplete() {
            UBHelper.Jumper.JumpComplete -= Jumper_JumpComplete;
            UB.Core.RenderFrame -= Core_RenderFrame;
            if (ThinkComplete)
                Util.Think("Jumper Success");
            UBHelper.vTank.Decision_UnLock(uTank2.ActionLockType.Navigation);
            UBHelper.vTank.Decision_UnLock(uTank2.ActionLockType.CorpseOpenAttempt);
            waitingForJump = false;
            //clear settings
            addShift = addW = addZ = addX = addC = false;
            jumpTries = 0;
        }

        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    UB.Core.RenderFrame -= Core_RenderFrame;
                    UBHelper.Jumper.JumpComplete -= Jumper_JumpComplete;
                    UBHelper.Jumper.JumpCancel();
                    base.Dispose(disposing);
                }
                disposedValue = true;
            }
        }
    }
}
