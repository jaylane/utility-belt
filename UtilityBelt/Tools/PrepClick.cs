using Decal.Adapter;
using System;
using Decal.Adapter.Wrappers;
using Decal.Filters;
using System.Text.RegularExpressions;
using VirindiViewService.Controls;
using UtilityBelt.Lib;
using System.ComponentModel;
using UtilityBelt.Service.Lib.Settings;

namespace UtilityBelt.Tools {
    [Name("Prepclick")]
    public class PrepClick : ToolBase {
        private double secondsToWatch = 0;
        private double TimeToStopWatching = 0d;
        private string clickSelection;

        #region /ub prepclick
        [Summary("Used to prepare for the first message box selection to appear after running the command.")]
        [Usage("/ub prepclick {stop|yes <secondstowatch>|no <secondstowatch>}")]
        [Example("Click yes within 10s", "/ub prepclick yes 10")]
        [Example("Stop watching for message box", "/ub prepclick stop")]
        [CommandPattern("prepclick", @"^(?<clickSelection>(yes|no|stop))( (?<secondstowatch>\d+))?$", false)]
        public void DoPrepClick(string command, Match args) {
            if (args.Groups[1].ToString().ToLower() == "stop") {
                if (secondsToWatch != 0) {
                    WriteToChat("Stopping... " + Math.Round(secondsToWatch - (TimeToStopWatching - UBHelper.Core.Uptime),2).ToString() + "s passed out of expected " + secondsToWatch.ToString() + "s");
                    Stop();
                }
                else {
                    WriteToChat("Message boxes are not currently being watched");
                }
            }
            else {
            bool alreadyRunning;
                if (secondsToWatch.Equals(0)) {
                    alreadyRunning = false;
                }
                else {
                    alreadyRunning = true;
                }
                double.TryParse(args.Groups[2].ToString(), out secondsToWatch);
                if (secondsToWatch > 3600) {
                    WriteToChat($"{secondsToWatch} is not a valid number of seconds to wait");
                    return;
                }
                TimeToStopWatching = secondsToWatch + UBHelper.Core.Uptime;
                clickSelection = args.Groups[1].ToString().ToLower();
                if (!alreadyRunning) {
                    WriteToChat($"Will click {clickSelection} on the next dialog to appear within {secondsToWatch} seconds");
                    alreadyRunning = true;
                    UBHelper.Core.RadarUpdate += Core_RadarUpdate;
                    UBHelper.ConfirmationRequest.ConfirmationRequestEvent += UBHelper_ConfirmationRequest;
                }
                else if (alreadyRunning) {
                    WriteToChat($"Will click {clickSelection} on the next dialog to appear within {secondsToWatch} seconds");
                }
                //else {
                //    WriteToChat($"Timer extended for {secondsToWatch} seconds");
                //}
            }
        }

        public PrepClick(UtilityBeltPlugin ub, string name) : base(ub, name) {
        }

        private void Stop() {
            secondsToWatch = 0;
            clickSelection = "";
            UBHelper.Core.RadarUpdate -= Core_RadarUpdate;
            UBHelper.ConfirmationRequest.ConfirmationRequestEvent -= UBHelper_ConfirmationRequest;
            TimeToStopWatching = 0d;
        }
       
        private void Core_RadarUpdate(double uptime) {
            if (uptime > TimeToStopWatching) {
                WriteToChat($"Time has expired: {secondsToWatch}");
                Stop();
            }
        }

        private void UBHelper_ConfirmationRequest(object sender, UBHelper.ConfirmationRequest.ConfirmationRequestEventArgs e) {
            try {
                if (secondsToWatch != 0) {
                    WriteToChat($"{e.Confirm}");
                    if (clickSelection == "yes") {
                        WriteToChat("Click Yes on " + e.Text.ToString());
                        e.ClickYes = true;
                    }
                    else if (clickSelection == "no") {
                        WriteToChat("Click No on " + e.Text.ToString());
                        e.ClickNo = true;
                    }
                }
                Stop();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        #endregion

        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    Stop();
                    base.Dispose(disposing);
                }
                disposedValue = true;
            }
        }
    }
}
