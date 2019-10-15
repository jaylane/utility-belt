using Decal.Adapter;
using System;
using Decal.Adapter.Wrappers;
using Decal.Filters;
using UtilityBelt.MagTools.Shared;
using System.Text.RegularExpressions;
using VirindiViewService.Controls;

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

        HudCheckBox UIJumperPauseNav { get; set; }
        HudCheckBox UIJumperThinkComplete { get; set; }
        HudCheckBox UIJumperThinkFail { get; set; }
        HudHSlider UIJumperAttempts { get; set; }
        HudStaticText UIJumperAttemptsText { get; set; }


        public Jumper() {
            try {

                UIJumperAttemptsText = Globals.MainView.view != null ? (HudStaticText)Globals.MainView.view["JumperAttemptsText"] : new HudStaticText();

                UIJumperPauseNav = Globals.MainView.view != null ? (HudCheckBox)Globals.MainView.view["JumperPauseNav"] : new HudCheckBox();
                UIJumperPauseNav.Change += UIJumperPauseNav_Change;

                UIJumperThinkComplete = Globals.MainView.view != null ? (HudCheckBox)Globals.MainView.view["JumperThinkComplete"] : new HudCheckBox();
                UIJumperThinkComplete.Change += UIJumperThinkComplete_Change;

                UIJumperThinkFail = Globals.MainView.view != null ? (HudCheckBox)Globals.MainView.view["JumperThinkFail"] : new HudCheckBox();
                UIJumperThinkFail.Change += UIJumperThinkFail_Change;

                UIJumperAttempts = Globals.MainView.view != null ? (HudHSlider)Globals.MainView.view["JumperAttempts"] : new HudHSlider();
                UIJumperAttempts.Changed += UIJumperAttempts_Changed;


            Globals.Core.CommandLineText += Current_CommandLineText;
            Globals.Core.EchoFilter.ServerDispatch += EchoFilter_ServerDispatch;

                UpdateUI();
            } catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UpdateUI() {
            UIJumperPauseNav.Checked = Globals.Settings.Jumper.PauseNav;
            UIJumperAttemptsText.Text = Globals.Settings.Jumper.Attempts.ToString();
            UIJumperThinkComplete.Checked = Globals.Settings.Jumper.ThinkComplete;
            UIJumperThinkFail.Checked = Globals.Settings.Jumper.ThinkFail;
            UIJumperAttempts.Position = Globals.Settings.Jumper.Attempts;
        }

        private void UIJumperPauseNav_Change(object sender, EventArgs e) {
            Globals.Settings.Jumper.PauseNav = UIJumperPauseNav.Checked;
        }

        private void UIJumperThinkComplete_Change(object sender, EventArgs e) {
            Globals.Settings.Jumper.ThinkComplete = UIJumperThinkComplete.Checked;
        }

        private void UIJumperThinkFail_Change(object sender, EventArgs e) {
            Globals.Settings.Jumper.ThinkFail = UIJumperThinkFail.Checked;
        }

        private void UIJumperAttempts_Changed(int min, int max, int pos) {
            if (pos != Globals.Settings.Jumper.Attempts) {
                Globals.Settings.Jumper.Attempts = pos;
                UIJumperAttemptsText.Text = pos.ToString();
            }
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
                Util.ThinkOrWrite("Turning failed", Globals.Settings.Jumper.ThinkFail);
            }
            //Do the jump thing
            if (needToJump && !isTurning) {
                needToJump = false;
                enableNavTimer = TimeSpan.FromMilliseconds(msToHoldDown + 1000);
                //Util.WriteToChat("Jumper enableNavTimer: "+enableNavTimer);

                if (Globals.Settings.Jumper.PauseNav)
                    VTankControl.Nav_Block(15000, Globals.Settings.Plugin.Debug);
                PostMessageTools.SendSpace(msToHoldDown, addShift, addW, addZ, addX, addC);
                waitingForJump = true;
                
                navSettingTimer = DateTime.UtcNow;
            }
            //Set vtank nav setting back to original state after jump/turn complete
            if (waitingForJump && DateTime.UtcNow - navSettingTimer >= enableNavTimer)
            {
                if (jumpTries < Globals.Settings.Jumper.Attempts) {
                    navSettingTimer = DateTime.UtcNow;
                    Logger.Debug("Timeout waiting for jump, trying again...");
                    if (Globals.Settings.Jumper.PauseNav)
                        VTankControl.Nav_Block(15000, Globals.Settings.Plugin.Debug);
                    jumpTries++;
                    PostMessageTools.SendSpace(msToHoldDown, addShift, addW, addZ, addX, addC);
                } else {
                    Util.ThinkOrWrite("You have failed to jump too many times.", Globals.Settings.Jumper.ThinkFail);

                    if (Globals.Settings.Jumper.PauseNav)
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
                    if (Globals.Settings.Jumper.ThinkComplete)
                        Util.Think("Jumper Success");
                    if (Globals.Settings.Jumper.PauseNav)
                        VTankControl.Nav_UnBlock();
                    waitingForJump = false;
                    //clear settings
                    addShift = addW = addZ = addX = addC = false;
                    jumpTries = 0;

                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        private static readonly Regex faceRegex = new Regex(@"^\/ub face (?<faceDirection>\d+)");
        private static readonly Regex jumpRegex = new Regex(@"^\/ub (?<faceDirection>\d+)? ?(?<shift>s)?jump(?<jumpDirection>[wzxc])?( )?(?<msToHoldDown>\d+)?");
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
                    Logger.Debug("Jumper: Turning to " + targetDirection);
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
