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

        public Jumper() {
            Globals.Core.CommandLineText += Current_CommandLineText;
        }

        public void Think() {
            if (DateTime.UtcNow - lastThought >= TimeSpan.FromMilliseconds(100)) {
                lastThought = DateTime.UtcNow;
                if (isTurning && needToTurn) {
                    //Util.WriteToChat("i'm turning to " + targetDirection);
                    if (targetDirection != Math.Round(CoreManager.Current.Actions.Heading, 0)) {
                        return;
                    } else if (targetDirection == Math.Round(CoreManager.Current.Actions.Heading, 0)) {
                        //Util.WriteToChat("done turning");
                        isTurning = false;
                        finishedTurning = true;
                        needToTurn = false;
                        //Util.WriteToChat("isTurning :" + isTurning + " finishedTurning: " + finishedTurning);
                        lastThought = DateTime.UtcNow;
                    }
                }
            }
            if (DateTime.UtcNow - turningSeconds >= TimeSpan.FromSeconds(3)) {
                turningSeconds = DateTime.UtcNow;
                if (isTurning) {
                    isTurning = false;
                    needToTurn = false;
                    //Util.WriteToChat("failed to turn in 3 seconds");
                    turningSeconds = DateTime.UtcNow;
                }
            }
            if (DateTime.UtcNow - jumpReadyThoughtSeconds >= TimeSpan.FromSeconds(1)) {
                jumpReadyThoughtSeconds = DateTime.UtcNow;
                if (!isTurning && finishedTurning && needToJump) {
                    isTurning = false;
                    //Util.WriteToChat("ready to jump");
                    //Util.WriteToChat("finishedTurning: " + finishedTurning.ToString());
                    PostMessageTools.SendSpace(msToHoldDown, addShift, addW, addZ, addX, addC);
                    finishedTurning = false;
                    needToJump = false;
                    msToHoldDown = 0;
                    addShift = addW = addZ = addX = addC = false;
                }
            }
        }

        private static readonly Regex directionFace = new Regex(@"/ub face ");
        private static readonly Regex jumpRegex = new Regex(@"/ub (?<faceDirection>\d+)? ?(?<shift>s)?jump(?<jumpDirection>[wzxc]?) (?<msToHoldDown>\d+)?");
        //Regex(@"\/ub ?(face )?(\d+)?( (s)jump(w|z|x|c))?( \d+)");
        void Current_CommandLineText(object sender, ChatParserInterceptEventArgs e) {
            try {
                if (directionFace.IsMatch(e.Text)) {
                    e.Eat = true;
                    if (e.Text.Length > 9) {
                        if (!int.TryParse(e.Text.Substring(9, e.Text.Length - 9), out targetDirection))
                            return;
                        isTurning = true;
                        turningSeconds = DateTime.UtcNow;
                        CoreManager.Current.Actions.FaceHeading(targetDirection, true);
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
                    }
                    else if (!string.IsNullOrEmpty(jumpMatch.Groups["msToHoldDown"].Value) && (msToHoldDown < 0 || msToHoldDown > 1000)) {
                        //Util.WriteToChat(msToHoldDown.ToString() + " is out of range 0-1000");
                        needToJump = false;
                        needToTurn = false;
                        return;
                    }
                    //set face direction
                    else if (string.IsNullOrEmpty(jumpMatch.Groups["faceDirection"].Value)) {
                        //Util.WriteToChat("No direction defined: not turning");
                        finishedTurning = true;
                        needToTurn = false;
                        isTurning = false;
                        needToJump = true;
                    }
                    else if (!string.IsNullOrEmpty(jumpMatch.Groups["faceDirection"].Value) && !int.TryParse(jumpMatch.Groups["faceDirection"].Value, out faceDirectionInt)) {
                        //Util.WriteToChat(faceDirectionInt.ToString() + " is not a valid number");
                        needToJump = false;
                        return;
                    }
                    else if (!string.IsNullOrEmpty(jumpMatch.Groups["faceDirection"].Value) && (faceDirectionInt < 0 || faceDirectionInt > 359)) {
                        //Util.WriteToChat(faceDirectionInt.ToString() + " is not within range 0-259");
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

                    //Util.WriteToChat("targetDirection: " + targetDirection.ToString());

                    //Util.WriteToChat("faceDirection: " + targetDirection.ToString() + "  addShift: " + addShift.ToString() + "  jumpDirection: " + jumpDirection.ToString() + "  msToHoldDown: " + msToHoldDown.ToString());
                    turningSeconds = DateTime.UtcNow;
                    if(needToTurn) CoreManager.Current.Actions.FaceHeading(targetDirection, true);
                    return;
                }
                else if (e.Text.StartsWith("/ub jump")) {
                    e.Eat = true;
                    if (e.Text.Equals("/ub jump")) {
                        //Util.WriteToChat("just a jump");
                        needToJump = true;
                        finishedTurning = true;
                    }
                    else {
                        //Util.WriteToChat("not sure what happened");
                    }
                }

                else {
                    //Util.WriteToChat("not turning");
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
