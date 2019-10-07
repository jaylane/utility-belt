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

        public Jumper() {
            Globals.Core.CommandLineText += Current_CommandLineText;
            Globals.Core.EchoFilter.ServerDispatch += EchoFilter_ServerDispatch;
        }

        public void Think() {
            if (isTurning && DateTime.UtcNow - lastThought >= TimeSpan.FromMilliseconds(100)) {
                lastThought = DateTime.UtcNow;
                if (targetDirection == Math.Round(CoreManager.Current.Actions.Heading, 0)) {
                    isTurning = false;
                }
            }

            //abort turning if takes longer than 3 seconds
            if (isTurning && DateTime.UtcNow - turningSeconds >= TimeSpan.FromSeconds(3)) {
                isTurning = false;
                Util.WriteToChat("Turning failed");
            }
            //Do the jump thing
            if (needToJump && !isTurning) {
                needToJump = false;
                enableNavTimer = TimeSpan.FromMilliseconds(msToHoldDown + 1000);
                //Util.WriteToChat("Jumper enableNavTimer: "+enableNavTimer);

                VTankControl.Nav_Block(15000, false);
                PostMessageTools.SendSpace(msToHoldDown, addShift, addW, addZ, addX, addC);
                waitingForJump = true;
                
                navSettingTimer = DateTime.UtcNow;
            }
            //Set vtank nav setting back to original state after jump/turn complete
            if (waitingForJump && DateTime.UtcNow - navSettingTimer >= enableNavTimer)
            {
                if (jumpTries < 3) {
                    navSettingTimer = DateTime.UtcNow;
                    Util.WriteToChat("Timeout waiting for jump, trying again...");
                    VTankControl.Nav_Block(15000, false);
                    jumpTries++;
                    PostMessageTools.SendSpace(msToHoldDown, addShift, addW, addZ, addX, addC);
                } else {
                    Util.WriteToChat("You have failed to jump too many times.");
                    VTankControl.Nav_UnBlock();
                    waitingForJump = false;
                    //clear settings
                    addShift = addW = addZ = addX = addC = false;
                    jumpTries = 0;
                }
            }
        }

        private void EchoFilter_ServerDispatch(object sender, NetworkMessageEventArgs e) {
            try {
                if (waitingForJump && e.Message.Type == 0xF74E && (int)e.Message["object"] == CoreManager.Current.CharacterFilter.Id) {
                    // Util.WriteToChat(string.Format("You Jumped. height: {0}", e.Message["height"]));
                    VTankControl.Nav_UnBlock();
                    waitingForJump = false;
                    //clear settings
                    addShift = addW = addZ = addX = addC = false;
                    jumpTries = 0;

                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        private static readonly Regex faceRegex = new Regex(@"/ub face (?<faceDirection>\d+)");
        private static readonly Regex jumpRegex = new Regex(@"/ub (?<faceDirection>\d+)? ?(?<shift>s)?jump(?<jumpDirection>[wzxc])?( )?(?<msToHoldDown>\d+)?");
        void Current_CommandLineText(object sender, ChatParserInterceptEventArgs e) {
            try {
                // handle /ub face command
                Match faceMatch = faceRegex.Match(e.Text);
                if (faceMatch.Success) {
                    e.Eat = true;
                    if (!int.TryParse(faceMatch.Groups["faceDirection"].Value, out targetDirection))
                        return;
                    isTurning = true;
                    needToTurn = true;
                    turningSeconds = DateTime.UtcNow;
                    CoreManager.Current.Actions.FaceHeading(targetDirection, true);
                    Util.WriteToChat("Jumper Debug: Turning to " + targetDirection);
                    return;
                }

                //handle /ub jump command
                Match jumpMatch = jumpRegex.Match(e.Text);
                if (jumpMatch.Success) {
                    e.Eat = true;
                    if (needToJump || waitingForJump) {
                        Util.WriteToChat("Error: You are already jumping. try again later.");
                        return;
                    }
                    needToJump = true;
                    string jumpDirection = "";

                    //set jump duration in ms
                    if (!string.IsNullOrEmpty(jumpMatch.Groups["msToHoldDown"].Value)) {
                        
                        if (!int.TryParse(jumpMatch.Groups["msToHoldDown"].Value, out msToHoldDown)) {
                            return;
                        }
                        if (msToHoldDown < 0 || msToHoldDown > 1000) {  //check jump held for 0-1000 ms
                            Util.WriteToChat("holdtime should be a number between 0 and 1000");
                            jumper_usage();
                            return;
                        }
                    } else { msToHoldDown = 0; }


                    if (!string.IsNullOrEmpty(jumpMatch.Groups["faceDirection"].Value)) {
                        if (!int.TryParse(jumpMatch.Groups["faceDirection"].Value, out faceDirectionInt)) {
                            return;
                        }
                        if (faceDirectionInt < 0 || faceDirectionInt > 359)
                        {
                            Util.WriteToChat("direction should be a number between 0 and 360");
                            jumper_usage();
                            return;
                        }
                        needToTurn = true;
                        targetDirection = faceDirectionInt;
                    } else {
                        needToTurn = false;
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

                    needToJump = true;
                    //start turning
                    if (needToTurn) {
                        turningSeconds = DateTime.UtcNow;
                        isTurning = true;
                        needToTurn = false;
                        CoreManager.Current.Actions.FaceHeading(targetDirection, true);
                    } else {
                        isTurning = false;
                    }
                    return;
                }
            } catch (Exception ex) { Logger.LogException(ex); }
        }

        private void jumper_usage()
        {
            Util.WriteToChat("Usage: /ub [direction] [s]jump[wzxc] [holdtime]");
        }
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    Globals.Core.CommandLineText -= Current_CommandLineText;
                    Globals.Core.EchoFilter.ServerDispatch -= EchoFilter_ServerDispatch;
                }
                disposed = true;
            }
        }
    }
}
