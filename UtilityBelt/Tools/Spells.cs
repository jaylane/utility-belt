using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UtilityBelt.Lib;

namespace UtilityBelt.Tools {
    internal class ProfessorCommand {
        public int Id;
        public string Command;
        public double StartedAt;

        public ProfessorCommand(int id, string command) {
            Id = id;
            Command = command;
        }
    }

    [Name("Spells")]
    public class Spells : ToolBase {
        private bool isRunning = false;
        private double lastTellSent = 0;
        private ProfessorCommand currentCommand;
        private Queue<ProfessorCommand> commandQueue = new Queue<ProfessorCommand>();

        #region Config
        [Summary("Spell Professors timeout in seconds")]
        [DefaultValue(60 * 10)] // 10 minutes
        public int Timeout {
            get { return (int)GetSetting("Timeout"); }
            set { UpdateSetting("Timeout", value); }
        }
        #endregion

        #region Commands
        #region /ub professor creature 1 
        [Summary("Attempts to talk to a nearby spell professor to learn a certain school/level of spells")]
        [Usage("/ub professor <creature|item|life|war|void> <level>")]
        [Example("/ub professor creature 1", "Attempts to learn level 1 creature spells")]
        [Example("/ub professor void 7", "Attempts to learn level 7 void spells")]
        [Example("/ub professor cancel", "Cancels all current attempts to use spell professors")]
        [CommandPattern("professor", @"^(?<verb>(cancel|creature|item|life|war|void)) *(?<level>\d)?$")]
        public void UseProfessorCommand(string command, Match args) {
            var verb = args.Groups["verb"].Value;
            switch (verb) {
                case "cancel":
                    commandQueue.Clear();
                    if (isRunning) {
                        Stop();
                    }
                    return;
            }

            if (verb.Length < 2) {
                LogError($"Invalid professor choice");
                return;
            }
            var name = $"Professor of {char.ToUpper(verb[0]) + verb.Substring(1)} Magic";
            var wo = Util.FindName(name, false, new ObjectClass[] {
                ObjectClass.Npc
            });

            if (wo == null) {
                LogError($"Unable to find object with name: {name}");
            }

            commandQueue.Enqueue(new ProfessorCommand(wo.Id, $"level {args.Groups["level"].Value}"));

            if (!isRunning) {
                UBHelper.Core.RadarUpdate += Core_RadarUpdate;
                UBHelper.ConfirmationRequest.ConfirmationRequestEvent += ConfirmationRequest_ConfirmationRequestEvent;
                UB.Core.ChatBoxMessage += Core_ChatBoxMessage;
                Core_RadarUpdate(UBHelper.Core.Uptime);
                isRunning = true;
            }
        }
        #endregion
        #endregion

        public Spells(UtilityBeltPlugin ub, string name) : base(ub, name) {
            //UBHelper.Core.SendTellByGUID();
        }

        private void Stop() {
            if (isRunning) {
                isRunning = false;
                UBHelper.Core.RadarUpdate -= Core_RadarUpdate;
                UBHelper.ConfirmationRequest.ConfirmationRequestEvent -= ConfirmationRequest_ConfirmationRequestEvent;
                UB.Core.ChatBoxMessage -= Core_ChatBoxMessage;
                WriteToChat("Finished");
            }
        }

        private void Core_ChatBoxMessage(object sender, Decal.Adapter.ChatTextInterceptEventArgs e) {
            try {
                Util.WriteToChat($"{e.Color} {e.Text}");
                if (e.Color == 3 && e.Text.Contains("tells you, \"I'm afraid you're lacking in funds.\"")) {
                    DequeueNext();
                }
                else if (e.Color == 3 && e.Text.Contains("tells you, \"I'm afraid you are not skilled enough")) {
                    DequeueNext();
                }
                else if (e.Color == 0 && e.Text.StartsWith("You hand over ")) {
                    DequeueNext();
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void ConfirmationRequest_ConfirmationRequestEvent(object sender, UBHelper.ConfirmationRequest.ConfirmationRequestEventArgs e) {
            try {
                if (e.Confirm == 7 && e.Text.StartsWith("I can teach you")) {
                    e.ClickYes = true;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Core_RadarUpdate(double uptime) {
            try {
                if (currentCommand == null) {
                    if (!DequeueNext())
                        return;
                }

                if (UBHelper.Core.Uptime - currentCommand.StartedAt > Timeout) {
                    LogError($"Professor command timed out");
                    if (!DequeueNext())
                        return;
                }

                if (UBHelper.Core.Uptime - lastTellSent > 15) {
                    lastTellSent = UBHelper.Core.Uptime;
                    UBHelper.Core.SendTellByGUID(currentCommand.Id, currentCommand.Command);
                    Logger.Debug($"Sending tell to {currentCommand.Id:X8}, {currentCommand.Command}.  Will try again in 15 seconds if no response. `/ub professor cancel` to cancel");
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private bool DequeueNext() {
            lastTellSent = 0;
            if (commandQueue.Count == 0) {
                currentCommand = null;
                Stop();
                return false;
            }

            currentCommand = commandQueue.Dequeue();
            currentCommand.StartedAt = UBHelper.Core.Uptime;

            return true;
        }

        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    Stop();
                }
                disposedValue = true;
            }
        }
    }
}
