using Decal.Adapter;
using System;
using Decal.Adapter.Wrappers;
using Decal.Filters;
using UtilityBelt.MagTools.Shared;
using System.Text.RegularExpressions;

namespace UtilityBelt.Tools {
    class Jumper : IDisposable {
        private bool disposed = false;
        private bool isTurning = false;
        private bool finishedTurning = false;
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
        private DateTime jumpReadyThoughtSeconds = DateTime.MinValue;
        private DateTime navSettingTimer = DateTime.MinValue;
        private int enableNavTimer;
        private bool waitToJump = false;
        private bool waitToTurn = false;

        public Jumper() {
            Globals.Core.CommandLineText += Current_CommandLineText;
        }

        public void Think() {
            if (DateTime.UtcNow - lastThought >= TimeSpan.FromMilliseconds(100)) {
                lastThought = DateTime.UtcNow;
                if (isTurning && needToTurn) {
                    if (targetDirection != Math.Round(CoreManager.Current.Actions.Heading, 0)) {
                        return;
                        //Check for turning complete
                    } else if (targetDirection == Math.Round(CoreManager.Current.Actions.Heading, 0)) {
                        waitToTurn = true;
                        navSettingTimer = DateTime.Now;
                        isTurning = false;
                        finishedTurning = true;
                        needToTurn = false;
                        lastThought = DateTime.UtcNow;
                    }
                }
            }

            //abort turning if takes longer than 3 seconds
            if (DateTime.UtcNow - turningSeconds >= TimeSpan.FromSeconds(3)) {
                turningSeconds = DateTime.UtcNow;
                if (isTurning) {
                    isTurning = false;
                    needToTurn = false;
                    VTankControl.PopSetting("EnableNav");
                    turningSeconds = DateTime.UtcNow;
                }
            }
            //Do the jump thing
            if (DateTime.UtcNow - jumpReadyThoughtSeconds >= TimeSpan.FromSeconds(1)) {
                jumpReadyThoughtSeconds = DateTime.UtcNow;
                if (!isTurning && finishedTurning && needToJump) {
                    isTurning = false;
                    if (enableNavTimer > 0) {
                        enableNavTimer += enableNavTimer + msToHoldDown + 1000;
                    }
                    else {
                        enableNavTimer = msToHoldDown + 1000;
                    }
                    Util.WriteToChat(enableNavTimer.ToString());
                    

                    PostMessageTools.SendSpace(msToHoldDown, addShift, addW, addZ, addX, addC);
                    finishedTurning = false;
                    addShift = addW = addZ = addX = addC = false;
                    needToJump = false;
                    msToHoldDown = 0;
                    VTankControl.PushSetting("EnableNav", false);
                    navSettingTimer = DateTime.UtcNow;
                    waitToJump = true;
                }
            }
            //Set vtank nav setting back to original state after jump/turn complete
            if (DateTime.UtcNow - navSettingTimer >= TimeSpan.FromMilliseconds(enableNavTimer) && (waitToJump || waitToTurn)) {
                navSettingTimer = DateTime.UtcNow;
                VTankControl.PopSetting("EnableNav");
                waitToJump = false;
                waitToTurn = false;
                enableNavTimer = 0;
            }
        }

        private static readonly Regex directionFace = new Regex(@"/ub face ");
        private static readonly Regex jumpRegex = new Regex(@"/ub (?<faceDirection>\d+)? ?(?<shift>s)?jump(?<jumpDirection>[wzxc]?) (?<msToHoldDown>\d+)?");
        void Current_CommandLineText(object sender, ChatParserInterceptEventArgs e) {
            try {
                if (directionFace.IsMatch(e.Text)) {
                    e.Eat = true;
                    if (e.Text.Length > 9) {
                        if (!int.TryParse(e.Text.Substring(9, e.Text.Length - 9), out targetDirection))
                            return;
                        isTurning = true;
                        needToTurn = true;
                        turningSeconds = DateTime.UtcNow;
                        CoreManager.Current.Actions.FaceHeading(targetDirection, true);

                        VTankControl.PushSetting("EnableNav",false);
                    }
                    return;
                }

                if (jumpRegex.IsMatch(e.Text)) {
                    //Util.WriteToChat(e.Text);
                    e.Eat = true;
                    finishedTurning = false;
                    needToJump = true;
                    Match jumpMatch = jumpRegex.Match(e.Text);
                    string jumpDirection = "";
                    //set jump duration in ms
                    if (!string.IsNullOrEmpty(jumpMatch.Groups["msToHoldDown"].Value) && (!int.TryParse(jumpMatch.Groups["msToHoldDown"].Value, out msToHoldDown))) {
                        //Util.WriteToChat(msToHoldDown.ToString() + " is not a valid number");
                        needToJump = false;
                        return;
                    } //check jump held for 0-1000 ms
                    else if (!string.IsNullOrEmpty(jumpMatch.Groups["msToHoldDown"].Value) && (msToHoldDown < 0 || msToHoldDown > 1000)) {
                        needToJump = false;
                        needToTurn = false;
                        return;
                    } //set face direction
                    else if (string.IsNullOrEmpty(jumpMatch.Groups["faceDirection"].Value)) {
                        finishedTurning = true;
                        needToTurn = false;
                        isTurning = false;
                        needToJump = true;
                    } //check face direction is a int
                    else if (!string.IsNullOrEmpty(jumpMatch.Groups["faceDirection"].Value) && !int.TryParse(jumpMatch.Groups["faceDirection"].Value, out faceDirectionInt)) {
                        needToJump = false;
                        return;
                    } //check face direction falls between 0-360
                    else if (!string.IsNullOrEmpty(jumpMatch.Groups["faceDirection"].Value) && (faceDirectionInt < 0 || faceDirectionInt > 359)) {
                        needToJump = false;
                        return;
                    }
                    else {
                        needToTurn = true;
                        isTurning = true;
                        targetDirection = faceDirectionInt;
                    }

                    //set jump direction
                    jumpDirection = jumpMatch.Groups["jumpDirection"].Value.ToLower();
                    switch (jumpDirection) {
                        case "w":
                            addW = true;
                            break;
                        case "z":
                            addZ = true;
                            break;
                        case "x":
                            addX = true;
                            break;
                        case "c":
                            addC = true;
                            break;
                    }

                    if (string.IsNullOrEmpty(jumpMatch.Groups["shift"].Value)) {
                        addShift = false;
                    } else {
                        addShift = true;
                    }

                    turningSeconds = DateTime.UtcNow;
                    //start turning and set nav
                    if (needToTurn) {
                        CoreManager.Current.Actions.FaceHeading(targetDirection, true);
                        VTankControl.PushSetting("EnableNav",false);
                    }
                    return;
                }
                else if (e.Text.StartsWith("/ub jump")) {
                    e.Eat = true;
                    if (e.Text.Equals("/ub jump")) {
                        needToJump = true;
                        finishedTurning = true;
                    }
                }
                else {
                    finishedTurning = true;
                }
            } catch (Exception ex) { Logger.LogException(ex); }
        }
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    Globals.Core.CommandLineText -= Current_CommandLineText;
                }
                disposed = true;
            }
        }
    }
}
