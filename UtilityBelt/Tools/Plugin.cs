using Antlr4.Runtime;
using Decal.Adapter;
using Decal.Adapter.Wrappers;
using Decal.Filters;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Constants;
using UtilityBelt.Lib.Dungeon;
using UtilityBelt.Lib.Expressions;
using UtilityBelt.Lib.Settings;
using static UtilityBelt.Tools.VTankControl;
using UtilityBelt.Service.Lib.Settings;
using System.Collections.ObjectModel;
using Hellosam.Net.Collections;
using System.Diagnostics;
using UtilityBelt.Common.Enums;
using ChatMessageType = UtilityBelt.Lib.Settings.ChatMessageType;
using AcClient;

namespace UtilityBelt.Tools {
    public class DelayedCommand {
        public string Command;
        public double Delay;
        public DateTime RunAt;

        public DelayedCommand(string command, double delayMilliseconds) {
            Command = command;
            Delay = delayMilliseconds;
            RunAt = DateTime.UtcNow.AddMilliseconds(delayMilliseconds);
        }
    }

    public class DelayedExpression {

        private static int _nextId = 1;

        public int Id;
        public string Expression;
        public double Delay;
        public DateTime RunAt;

        public DelayedExpression(string expression, double delayMilliseconds) {
            Id = _nextId++;
            Expression = expression;
            Delay = delayMilliseconds;
            RunAt = DateTime.UtcNow.AddMilliseconds(delayMilliseconds);
        }
    }

    [Name("Plugin")]
    [Summary("Provides misc commands")]
    [FullDescription(@"
'The Junk Drawer of UtilityBelt' -Cosmic Jester
    ")]
    public class Plugin : ToolBase {
        private Mp3Player mediaPlayer;

        private DateTime portalTimestamp = DateTime.MinValue;
        private int portalAttempts = 0;
        private static WorldObject portal = null;
        private bool isDelayListening;
        private bool inPortal;
        readonly private List<DelayedCommand> delayedCommands = new List<DelayedCommand>();
        readonly private List<DelayedExpression> delayedExpressions = new List<DelayedExpression>();

        /// <summary>
        /// The default character settings path
        /// </summary>
        public string CharacterSettingsFile { get => Path.Combine(Util.GetCharacterDirectory(), SettingsProfileExtension); }

        /// <summary>
        /// The file path to the currently loaded settings profile
        /// </summary>
        public string SettingsProfilePath {
            get {
                if (SettingsProfile == "[character]")
                    return CharacterSettingsFile;
                else
                    return Path.Combine(Util.GetProfilesDirectory(), $"{SettingsProfile}.{SettingsProfileExtension}");
            }
        }

        public static readonly string SettingsProfileExtension = "settings.json";

        #region Config
        [Summary("Check for plugin updates on login")]
        public readonly Setting<bool> CheckForUpdates = new Setting<bool>(true);

        [Summary("Show debug messages")]
        [Hotkey("Debug", "Toggle Debug logging")]
        public readonly Setting<bool> Debug = new Setting<bool>(false);

        [Summary("Settings profile. Choose [character] to use a private copy of settings for this character.")]
        public readonly CharacterState<string> SettingsProfile = new CharacterState<string>("[character]");

        [Summary("Main UB Window X position for this character (left is 0)")]
        public readonly CharacterState<int> WindowPositionX = new CharacterState<int>(100);

        [Summary("Main UB Window Y position for this character (top is 0)")]
        public readonly CharacterState<int> WindowPositionY = new CharacterState<int>(100);

        [Summary("Settings Window X position for this character (left is 0)")]
        public readonly CharacterState<int> SettingsWindowPositionX = new CharacterState<int>(140);

        [Summary("Settings Window Y position for this character (top is 0)")]
        public readonly CharacterState<int> SettingsWindowPositionY = new CharacterState<int>(140);

        [Summary("Think to yourself when portal use success/fail")]
        public readonly Setting<bool> PortalThink = new Setting<bool>(false);

        [Summary("Timeout to retry portal use, in milliseconds")]
        public readonly Setting<int> PortalTimeout = new Setting<int>(5000);

        [Summary("Attempts to retry using a portal")]
        public readonly Setting<int> PortalAttempts = new Setting<int>(3);

        [Summary("Patches the client (in realtime) to disable 3d rendering")]
        [Hotkey("VideoPatch", "Toggle VideoPatch functionality")]
        public readonly Setting<bool> VideoPatch = new Setting<bool>(false);

        [Summary("Disables VideoPatch while the client has focus")]
        public readonly Setting<bool> VideoPatchFocus = new Setting<bool>(false);

        [Summary("Limits frame while the client does not have focus")]
        public readonly Setting<int> BackgroundFrameLimit = new Setting<int>(0);

        [Summary("Enables a rolling PCAP buffer, to export recent packets")]
        public readonly Setting<bool> PCap = new Setting<bool>(false);

        [Summary("PCap rolling buffer depth")]
        public readonly Setting<int> PCapBufferDepth = new Setting<int>(5000, (value) => {
            if (value < 200 || value > 524287)
                return "Must be between 200-524287.";
            return null;
        });

        [Summary("Plugin generic messages")]
        public readonly PluginMessageDisplay GenericMessageDisplay = new PluginMessageDisplay(true, ChatMessageType.System);

        [Summary("Plugin debug messages")]
        public readonly PluginMessageDisplay DebugMessageDisplay = new PluginMessageDisplay(true, ChatMessageType.Abuse);

        [Summary("Plugin expression messages")]
        public readonly PluginMessageDisplay ExpressionMessageDisplay = new PluginMessageDisplay(true, ChatMessageType.System);

        [Summary("Plugin error messages")]
        public readonly PluginMessageDisplay ErrorMessageDisplay = new PluginMessageDisplay(true, ChatMessageType.Help);
        #endregion

        #region Commands
        #region /ub
        [Summary("Prints current build version to chat")]
        [Usage("/ub")]
        [Example("/ub", "Prints current build version to chat")]
        [CommandPattern("", @"^$")]
        public void ShowVersion(string _, Match _1) {
            Logger.WriteToChat("UtilityBelt Version v" + Util.GetVersion(true) + "\n Type `/ub help` or `/ub help <command>` for help.");
        }
        #endregion
        #region /ub help
        [Summary("Prints help for UB command line usage")]
        [Usage("/ub help [command]")]
        [Example("/ub help", "Prints out all available UB commands")]
        [Example("/ub help printcolors", "Prints out help and usage information for the printcolors command")]
        [CommandPattern("help", @"^(?<Command>\S+)?$")]
        public void PrintHelp(string command, Match args) {
            var argCommand = args.Groups["Command"].Value;

            if (!string.IsNullOrEmpty(argCommand) && UB.RegisteredCommands.ContainsKey(argCommand)) {
                UB.PrintHelp(UB.RegisteredCommands[argCommand]);
            }
            else {
                var help = "All available UB commands: /ub {";

                var availableVerbs = UB.RegisteredCommands.Keys.ToList();
                availableVerbs.Remove("");
                availableVerbs.Sort();

                help += string.Join(", ", availableVerbs.ToArray());
                help += "}\nFor help with a specific command, use `/ub help [command]`";

                Logger.WriteToChat(help);
            }
        }

        #endregion

        byte repeat = 0;

        #region /ub test
        [Summary("test")]
        [Usage("/ubtest")]
        [Example("/ub test", "test")]
        [CommandPattern("test", @"^(?<Command>\S+)?$")]
        unsafe public void Test(string command, Match args) {
            var argCommand = args.Groups["Command"].Value;
           
            var amt = (float)(new Random()).NextDouble();

            CM_Character.Event_PlayerOptionChangedEvent(PlayerOption.AutoRepeatAttack_PlayerOption, repeat);
            CM_Character.SendNotice_ReloadOptions();
            repeat = repeat == 0 ? (byte)1 : (byte)0;

            if (CoreManager.Current.Actions.CombatMode == CombatState.Melee) {
                CM_Combat.SendNotice_DesiredAttackPowerChanged(amt);
                CM_Combat.Event_TargetedMeleeAttack((uint)0x812208E1, ATTACK_HEIGHT.MEDIUM_ATTACK_HEIGHT, amt);
            }
            else if (CoreManager.Current.Actions.CombatMode == CombatState.Missile) {
                CM_Combat.SendNotice_DesiredAttackPowerChanged(amt);
                CM_Combat.Event_TargetedMissileAttack((uint)0x812208E1, ATTACK_HEIGHT.MEDIUM_ATTACK_HEIGHT, amt);
            }
        }

        #endregion
        #region /ub opt {list | get <option> | set <option> <newValue> | toggle <option>}
        [Summary("Manage plugin settings from the command line")]
        [Usage("/ub opt {list | get <option> | set <option> <newValue> | toggle <options>}")]
        [Example("/ub opt list", "Lists all available settings.")]
        [Example("/ub opt get Plugin.Debug", "Gets the current value for the \"Plugin.Debug\" setting")]
        [Example("/ub opt toggle Plugin.Debug", "Toggles the current value for the \"Plugin.Debug\" setting")]
        [Example("/ub opt set Plugin.Debug true", "Sets the \"Plugin.Debug\" setting to True")]
        [CommandPattern("opt", @"^ *(?<params>(list|toggle \S+|get \S+|set \S+ .*)) *$")]
        public void DoOpt(string command, Match args) {
            UB_opt(args.Groups["params"].Value);
        }

        readonly private Regex optionRe = new Regex(@"^((get|set|toggle) )?(?<option>[^\s]+)\s?(?<value>.*)", RegexOptions.IgnoreCase);
        private void UB_opt(string args) {
            try {
                if (args.ToLower().Trim() == "list") {
                    Logger.WriteToChat("All Settings:\n" + ListOptions(UB, null, ""));
                    return;
                }

                if (!optionRe.IsMatch(args.Trim())) return;

                var match = optionRe.Match(args.Trim());
                string name = match.Groups["option"].Value.ToLower();
                string newValue = match.Groups["value"].Value;
                OptionResult option;
                if (name.StartsWith("global."))
                    option = UBLoader.FilterCore.SettingsGlobal.Get(name);
                else if (UB.Settings.Exists(name))
                    option = UB.Settings.Get(name);
                else
                    option = UB.State.Get(name);

                if (option == null || option.Setting == null) {
                    Logger.Error("Invalid option: " + name);
                    foreach (var opt in UB.State.GetAll()) {
                        Logger.Error($"  - {opt.FullName}");
                    }
                    return;
                }

                if (args.ToLower().Trim().StartsWith("toggle ")) {
                    try {
                        option.Setting.SetValue(!(bool)option.Setting.GetValue());
                        if (!UB.Plugin.Debug) Logger.WriteToChat(option.Setting.FullDisplayValue());
                    }
                    catch (Exception ex) { Logger.Error($"Unable to toggle setting {option.Setting.FullName}: {ex.Message}"); }
                    return;
                }

                if (option.Setting.GetValue() is System.Collections.IList list) {
                    if (!string.IsNullOrEmpty(newValue)) {
                        var parts = newValue.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                        switch (parts[0].ToLower()) {
                            case "add":
                                if (parts.Length < 2 || string.IsNullOrEmpty(parts[1].Trim())) {
                                    Logger.Debug("Missing items to add");
                                    return;
                                }
                                var subpartsa = parts[1].Split(new[] { ',' });
                                foreach (var spa in subpartsa) {
                                    //Logger.WriteToChat($"Add item: {spa}");
                                    if (!list.Contains(spa))
                                        list.Add(spa);
                                }
                                break;

                            case "remove":
                                if (parts.Length < 2 || string.IsNullOrEmpty(parts[1].Trim())) {
                                    Logger.Debug("Missing items to remove");
                                    return;
                                }
                                var subparts = parts[1].Split(new[] { ',' });
                                foreach (var sp in subparts) {
                                    //Logger.WriteToChat($"Remove item: {sp}");
                                    list.Remove(sp);
                                }
                                break;

                            case "clear":
                                list.Clear();
                                break;

                            default:
                                Logger.Error($"Unknown verb: {parts[1]}");
                                return;
                        }
                    }
                    Logger.WriteToChat(option.Setting.FullDisplayValue());
                }
                else if (string.IsNullOrEmpty(newValue)) {
                    Logger.WriteToChat(option.Setting.FullDisplayValue());
                }
                else {
                    try {
                        option.Setting.SetValue(newValue);
                        if (!UB.Plugin.Debug)
                            Logger.WriteToChat(option.Setting.FullDisplayValue());
                    }
                    catch (Exception ex) { Logger.LogException(ex); }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private string ListOptions(object obj, object parentObj, string history) {
            var results = "";
            var settings = UB.Settings.GetAll();
            var state = UB.State.GetAll();
            var globalSettings = UBLoader.FilterCore.SettingsGlobal.GetAll();

            foreach (var setting in globalSettings) {
                results += $"{setting.FullName} ({setting.SettingType}) = {setting.DisplayValue(true)}\n";
            }

            foreach (var setting in settings) {
                results += $"{setting.FullName} ({setting.SettingType}) = {setting.DisplayValue(true)}\n";
            }

            foreach (var setting in state) {
                results += $"{setting.FullName} ({setting.SettingType}) = {setting.DisplayValue(true)}\n";
            }
            return results;
        }

        #endregion
        #region /ub date [format]
        [Summary("Prints current date with an optional format. See https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings for formatting options.")]
        [Usage("/ub date[utc] [format]")]
        [Example("/ub date hh:mm:ss tt", "Prints current local time '06:09:01 PM'")]
        [Example("/ub dateutc dddd dd MMMM", "Prints current utc date 'Friday 29 August'")]
        [CommandPattern("date", @"^(?<format>.*)$")]
        public void PrintDate(string cmd, Match args) {
            try {
                var format = args.Groups["format"].Value;
                if (string.IsNullOrEmpty(format))
                    format = "dddd dd MMMM HH:mm:ss";
                if (cmd.Contains("utc"))
                    Logger.WriteToChat("Current Date: " + DateTime.UtcNow.ToString(format));
                else
                    Logger.WriteToChat("Current Date: " + DateTime.Now.ToString(format));
            }
            catch (Exception ex) {
                Logger.WriteToChat(ex.Message);
            }
        }
        #endregion
        #region /ub delay <millisecondDelay> <command>
        [Summary("Thinks to yourself with your current vitae percentage")]
        [Usage("/ub delay <millisecondDelay> <command>")]
        [Example("/ub delay 5000 /say hello", "Runs \"/say hello\" after a 3000ms delay (3 seconds)")]
        [CommandPattern("delay", @"^ *(?<params>\d+ .+) *$")]
        public void DoDelay(string _, Match args) {
            var theRest = args.Groups["params"].Value;
            string[] rest = theRest.Split(' ');
            if (string.IsNullOrEmpty(theRest)
                || rest.Length < 2
                || !double.TryParse(rest[0], out double delay)
                || delay <= 0
            ) {
                Logger.Error("Usage: /ub delay <milliseconds> <command>");
                return;
            }
            var command = string.Join(" ", rest.Skip(1).ToArray());
            AddDelayedCommand(command, delay);
        }

        public void AddDelayedCommand(string command, double delay) {
            Logger.Debug($"Scheduling command `{command}` with delay of {delay}ms");

            var delayed = new DelayedCommand(command, delay);

            delayedCommands.Add(delayed);
            delayedCommands.Sort((x, y) => x.RunAt.CompareTo(y.RunAt));

            if (!isDelayListening) {
                isDelayListening = true;
                UB.Core.RenderFrame += Core_RenderFrame_Delay;
            }
        }
        public DelayedExpression AddDelayedExpression(string expression, double delay) {
            var delayed = new DelayedExpression(expression, delay);
            Logger.Debug($"Scheduling expression `{expression}` (id {delayed.Id}) with delay of {delay}ms. Clear with clearexec[{delayed.Id}]");

            delayedExpressions.Add(delayed);
            delayedExpressions.Sort((x, y) => x.RunAt.CompareTo(y.RunAt));

            if (!isDelayListening) {
                isDelayListening = true;
                UB.Core.RenderFrame += Core_RenderFrame_Delay;
            }

            return delayed;
        }

        public void Core_RenderFrame_Delay(object sender, EventArgs e) {
            try {
                while (delayedCommands.Count > 0 && delayedCommands[0].RunAt <= DateTime.UtcNow) {
                    LogDebug($"Executing command `{delayedCommands[0].Command}` (delay was {delayedCommands[0].Delay}ms)");
                    Util.DispatchChatToBoxWithPluginIntercept(delayedCommands[0].Command);
                    delayedCommands.RemoveAt(0);
                }

                while (delayedExpressions.Count > 0 && delayedExpressions[0].RunAt <= DateTime.UtcNow) {
                    LogDebug($"Executing expression `{delayedExpressions[0].Expression}` (delay was {delayedExpressions[0].Delay}ms)");
                    UB.VTank.EvaluateExpression(delayedExpressions[0].Expression, true);
                    delayedExpressions.RemoveAt(0);
                }

                if (delayedCommands.Count == 0 && delayedExpressions.Count == 0) {
                    UB.Core.RenderFrame -= Core_RenderFrame_Delay;
                    isDelayListening = false;
                }

            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        #endregion
        #region /ub closestportal || /ub portal <name> || /ub portalp <name>
        [Summary("Uses the closest portal.")]
        [Usage("/ub closestportal")]
        [Example("/ub closestportal", "Uses the closest portal")]
        [CommandPattern("closestportal", @"^$")]
        public void DoClosestPortal(string _, Match _1) {
            UB_portal("", true);
        }
        [Summary("Portal commands, with build in VTank pausing.")]
        [Usage("/ub portal[p] <portalName>")]
        [Example("/ub portal Gateway", "Uses portal with exact name \"Gateway\"")]
        [Example("/ub portalp Portal", "Uses portal with name partially matching \"Portal\"")]
        [CommandPattern("portal", @"^ *(?<params>.+) *$", true)]
        public void DoPortal(string command, Match args) {
            var flags = command.Replace("portal", "");
            UB_portal(args.Groups["params"].Value, flags.Contains("p"));
        }
        public void UB_portal(string portalName, bool partial) {
            portal = Util.FindName(portalName, partial, new ObjectClass[] { ObjectClass.Portal, ObjectClass.Npc });
            if (portal != null) {
                Logger.WriteToChat($"Attempting to use portal: {portal.Name}");
                UsePortal();
                return;
            }

            Util.ThinkOrWrite("Could not find a portal", PortalThink);
        }
        private void UsePortal() {
            UBHelper.vTank.Decision_Lock(uTank2.ActionLockType.Navigation, TimeSpan.FromMilliseconds(500 + PortalTimeout));
            portalAttempts = 1;

            portalTimestamp = DateTime.UtcNow - TimeSpan.FromMilliseconds(PortalTimeout - 250); // fudge timestamp so next think hits in 500ms
            UB.Core.Actions.SetAutorun(false);
            LogDebug("Attempting to use portal " + portal.Name);
            UB.Core.RenderFrame += Core_RenderFrame_PortalOpen;
            UB.Core.EchoFilter.ServerDispatch += EchoFilter_ServerDispatch_PortalOpen;

        }
        private void FinishPortal(bool success) {
            Util.ThinkOrWrite((success? "portal used successfully" : "failed to use portal"), PortalThink);
            portal = null;
            portalAttempts = 0;
            UBHelper.vTank.Decision_UnLock(uTank2.ActionLockType.Navigation);
            UB.Core.RenderFrame -= Core_RenderFrame_PortalOpen;
            UB.Core.EchoFilter.ServerDispatch -= EchoFilter_ServerDispatch_PortalOpen;
        }
        public void Core_RenderFrame_PortalOpen(object sender, EventArgs e) {
            try {
                if (portalAttempts > 0 && DateTime.UtcNow - portalTimestamp > TimeSpan.FromMilliseconds(PortalTimeout)) {

                    if (portalAttempts <= PortalAttempts) {
                        if (portalAttempts > 1)
                            LogDebug("Use Portal Timed out, trying again");

                        UBHelper.vTank.Decision_Lock(uTank2.ActionLockType.Navigation, TimeSpan.FromMilliseconds(500 + PortalTimeout));
                        portalAttempts++;
                        portalTimestamp = DateTime.UtcNow;
                        CoreManager.Current.Actions.UseItem(portal.Id, 0);
                    }
                    else {
                        WriteToChat("Unable to use portal " + portal.Name);
                        UB.Core.Actions.FaceHeading(UB.Core.Actions.Heading - 1, true); // Cancel the previous useitem call (don't ask)
                        FinishPortal(false);
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        private void EchoFilter_ServerDispatch_PortalOpen(object sender, NetworkMessageEventArgs e) {
            try {
                if (portalAttempts > 0 && e.Message.Type == 0xF74B && (int)e.Message["object"] == CoreManager.Current.CharacterFilter.Id && (short)e.Message["portalType"] == 17424) { //17424 is the magic sauce for entering a portal. 1032 is the magic sauce for exiting a portal.
                    FinishPortal(true);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        #endregion
        #region /ub follow [name]
        [Summary("Follow player commands")]
        [Usage("/ub follow[p] <name>")]
        [Example("/ub follow Zero Cool", "Sets a VTank nav route to follow \"Zero Cool\"")]
        [Example("/ub followp Zero", "Sets a VTank nav route to follow a character with a name partially matching \"Zero\"")]
        [CommandPattern("follow", @"^ *(?<params>.+) *$", true)]
        public void DoFollow(string command, Match args) {
            var flags = command.Replace("follow", "");
            UB_follow(args.Groups["params"].Value, flags.Contains("p"));
        }
        public void UB_follow(string characterName, bool partial) {
            WorldObject followChar = Util.FindName(characterName, partial, new ObjectClass[] { ObjectClass.Player });
            if (followChar != null) {
                FollowChar(followChar.Id);
                return;
            }
            Logger.Error($"Could not find {(characterName == null ? "closest player" : $"player {characterName}")}");
        }
        private void FollowChar(int id) {
            if (UB.Core.WorldFilter[id] == null) {
                LogError($"Character 0x{id:X8} does not exist");
                return;
            }
            try { // intentionally setup to throw early.
                UBHelper.vTank.Instance.LoadNavProfile("UBFollow");
                UBHelper.vTank.Instance.NavSetFollowTarget(id, "");
                if (!(bool)UBHelper.vTank.Instance.GetSetting("EnableNav"))
                    UBHelper.vTank.Instance.SetSetting("EnableNav", true);
                Logger.WriteToChat($"Following {UB.Core.WorldFilter[id].Name}[0x{id:X8}]");
                UBHelper.vTank.Instance.SetSetting("EnableNav", true);
            }
            catch {
                Logger.Error($"Failed to follow {UB.Core.WorldFilter[id].Name}[0x{id:X8}] (is vTank loaded?)");
            }

        }
        private void CharacterFilter_LoginComplete_Follow(object sender, EventArgs e) {
            try {
                UB_Follow_Clear();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        private void UB_Follow_Clear() {
            try {
                if (UBHelper.vTank.Instance != null && UBHelper.vTank.Instance.GetNavProfile().Equals("UBFollow"))
                    Util.Decal_DispatchOnChatCommand("/vt nav load ");
            }
            catch { }
        }

        #endregion
        #region /ub mexec
        [Summary("Evaluates a meta expression")]
        [Usage("/ub mexec <expression>")]
        [Example("/ub mexec <expression>", "Evaluates expression")]
        [CommandPattern("mexec", @"^(?<Expression>.*)?$", true)]
        public void EvaluateExpressionCommand(string command, Match args) {
            EvaluateExpression(args.Groups["Expression"].Value, command.Replace("mexec", "").Equals("m"));
        }

        public void EvaluateExpression(string expression, bool silent=false) {
            try {
                if (silent) {
                    UB.VTank.EvaluateExpression(expression);
                    return;
                }
                var watch = new System.Diagnostics.Stopwatch();
                Logger.WriteToChat($"Evaluating expression: \"{expression}\"", Logger.LogMessageType.Expression, true, false);
                watch.Start();
                var res = UB.VTank.EvaluateExpression(expression);
                watch.Stop();
                Logger.WriteToChat($"Result: [{ExpressionVisitor.GetFriendlyType(res.GetType())}] {res} ({Math.Round(watch.ElapsedTicks / 10000.0, 3)}ms)", Logger.LogMessageType.Expression);
            }
            catch (Exception ex) {
                Logger.LogException(ex, false);
            }
        }
        #endregion
        #region /ub pos
        [Summary("Prints position information for the currently selected object")]
        [Usage("/ub pos")]
        [CommandPattern("pos", @"^$")]
        public void DoPos(string _, Match _1) {
            UB_pos();
        }
        private void UB_pos() {
            var selected = UB.Core.Actions.CurrentSelection;

            if (selected == 0 || !UB.Core.Actions.IsValidObject(selected)) {
                Logger.Error("pos: No object selected");
                return;
            }

            var wo = UB.Core.WorldFilter[selected];

            if (wo == null) {
                Logger.Error("pos: null object selected");
                return;
            }

            var pos = PhysicsObject.GetPosition(wo.Id);
            var d = PhysicsObject.GetDistance(wo.Id);
            var lc = PhysicsObject.GetLandcell(wo.Id);

            Logger.WriteToChat($"Id: {wo.Id} ( 0x{wo.Id:X8} )");
            Logger.WriteToChat($"Offset: {wo.Offset()}");
            Logger.WriteToChat($"Coords: {wo.Coordinates()}");
            Logger.WriteToChat($"RawCoords: {wo.RawCoordinates()}"); //same as offset?
            Logger.WriteToChat($"Phys lb: {lc.ToString("X8")}");
            Logger.WriteToChat($"Phys pos: x:{pos.X} y:{pos.Y} z:{pos.Z}");
        }
        #endregion
        #region /ub id
        [Summary("Prints object id for the currently selected object")]
        [Usage("/ub id")]
        [CommandPattern("id", @"^$")]
        public void DoId(string _, Match _1) {
            var selected = UB.Core.Actions.CurrentSelection;

            if (selected == 0 || !UB.Core.Actions.IsValidObject(selected)) {
                Logger.Error("Id: No object selected");
                return;
            }

            var wo = UB.Core.WorldFilter[selected];

            if (wo == null) {
                Logger.Error("Id: null object selected");
                return;
            }
            Logger.WriteToChat($"Id: {wo.Id} ( 0x{wo.Id:X8} )");
        }
        #endregion
        #region /ub printcolors
        [Summary("Prints out all available chat colors")]
        [Usage("/ub printcolors")]
        [Example("/ub printcolors", "Prints out all available chat colors")]
        [CommandPattern("printcolors", @"^$")]
        public void PrintChatColors(string _, Match _1) {
            foreach (var type in Enum.GetValues(typeof(ChatMessageType)).Cast<ChatMessageType>()) {
                UB.Core.Actions.AddChatText($"[PrintColors]{type} ({(int)type})", (int)type);
            }
        }
        #endregion
        #region /ub propertydump
        [Summary("Prints information for the currently selected object")]
        [Usage("/ub propertydump")]
        [CommandPattern("propertydump", @"^$")]
        public void DoPropertyDump(string _, Match _1) {
            UB_propertydump();
        }
        private void UB_propertydump() {
            var selected = UB.Core.Actions.CurrentSelection;

            if (selected == 0 || !UB.Core.Actions.IsValidObject(selected)) {
                Logger.Error("propertydump: No object selected");
                return;
            }

            var wo = UB.Core.WorldFilter[selected];

            if (wo == null) {
                Logger.Error("propertydump: null object selected");
                return;
            }

            var output = new StringBuilder();

            output.AppendLine($"Property Dump for {wo.Name}");

            output.AppendLine($"Id = {wo.Id} (0x{wo.Id.ToString("X8")})");
            output.AppendLine($"PhysicsId = {UB.Core.Actions.PhysicsObject(wo.Id)} (0x{UB.Core.Actions.PhysicsObject(wo.Id).ToString("X8")})");
            output.AppendLine($"Type = {wo.Type} (0x{wo.Type.ToString("X8")})");
            output.AppendLine($"Name = {wo.Name}");
            output.AppendLine($"ActiveSpellCount = {wo.ActiveSpellCount}");
            output.AppendLine($"Category = {wo.Category}");
            output.AppendLine($"Coordinates = {wo.Coordinates()}");
            output.AppendLine($"GameDataFlags1 = {wo.GameDataFlags1}");
            output.AppendLine($"HasIdData = {wo.HasIdData}");
            output.AppendLine($"LastIdTime = {wo.LastIdTime}");
            output.AppendLine($"ObjectClass = {wo.ObjectClass} ({(int)wo.ObjectClass})");
            output.AppendLine($"Offset = {wo.Offset()}");
            output.AppendLine($"Orientation = {wo.Orientation()}");
            output.AppendLine($"RawCoordinates = {wo.RawCoordinates()}");

            var stringKeys = wo.StringKeys.ToList();
            if (stringKeys.Count > 0) {
                stringKeys.Sort((a, b) => ((StringId)a).ToString().CompareTo(((StringId)b).ToString()));
                output.AppendLine("String Values:");
                foreach (var sk in stringKeys) {
                    output.AppendLine($"  {(StringId)sk}({sk}) = {wo.Values((StringValueKey)sk)}");
                }
            }

            var intKeys = wo.LongKeys.ToList();
            if (intKeys.Count > 0) {
                intKeys.Sort((a, b) => ((IntId)a).ToString().CompareTo(((IntId)b).ToString()));
                output.AppendLine("Int Values:");
                foreach (var sk in intKeys) {
                    output.AppendLine($"  {(IntId)sk}({sk}) = {wo.Values((LongValueKey)sk)}");
                }
            }

            var boolKeys = wo.BoolKeys.ToList();
            if (boolKeys.Count > 0) {
                boolKeys.Sort((a, b) => ((BoolId)a).ToString().CompareTo(((BoolId)b).ToString()));
                output.AppendLine("Bool Values:");
                foreach (var sk in boolKeys) {
                    output.AppendLine($"  {(BoolId)sk}({sk}) = {wo.Values((BoolValueKey)sk)}");
                }
            }

            var floatKeys = wo.DoubleKeys.ToList();
            if (floatKeys.Count > 0) {
                floatKeys.Sort((a, b) => ((FloatId)a).ToString().CompareTo(((FloatId)b).ToString()));
                output.AppendLine("Float Values:");
                foreach (var sk in floatKeys) {
                    output.AppendLine($"  {(FloatId)sk}({sk}) = {wo.Values((DoubleValueKey)sk)}");
                }
            }

            if (wo.SpellCount > 0) {
                output.AppendLine($"Spells ({wo.SpellCount}):");
                FileService service = UB.Core.Filter<FileService>();
                for (var i = 0; i < wo.SpellCount; i++) {
                    var spell = service.SpellTable.GetById(wo.Spell(i));
                    output.AppendLine($"  {spell.Name} ({wo.Spell(i)})");
                }
            }

            Logger.WriteToChat(output.ToString().Replace("\r", ""));
        }
        #endregion
        #region /ub playsound [volume] <filepath>
        [Summary("Play a sound from the client")]
        [Usage("/ub playsound [volume] <filepath>")]
        [Example("/ub playsound 100 C:\\test.wav", "Plays absolute path to music file at 100% volume")]
        [Example("/ub playsound 50 test.wav", "Plays test.wav from the UB plugin storage directory at 50% volume")]
        [CommandPattern("playsound", @"^ *(?<params>(\d+ )?.+) *$")]
        public void DoPlaySound(string _, Match args) {
            UB_playsound(args.Groups["params"].Value);
        }
        private static readonly Regex PlaySoundParamRegex = new Regex(@"^(?<volume>\d*)?\s*(?<path>.*)$");
        private void UB_playsound(string @params) {
            string absPath = null;
            int volume = 50;
            string path = null;
            var m = PlaySoundParamRegex.Match(@params);
            if (m != null && m.Success) {
                path = m.Groups["path"].Value;
                if (!string.IsNullOrEmpty(m.Groups["volume"].Value) && int.TryParse(m.Groups["volume"].Value, out var volumeInt)) {
                    volume = Math.Max(0, Math.Min(100, volumeInt));
                }
            }

            if (File.Exists(path))
                absPath = path;
            else if (Regex.IsMatch(path, @"$[a-zA-Z0-9.-_ ]*$")) {
                var ext = Path.GetExtension(path);
                if (string.IsNullOrEmpty(ext))
                    path = Path.Combine(Util.GetPluginDirectory(), path + ".mp3");
                else
                    path = Path.Combine(Util.GetPluginDirectory(), path);
                if (File.Exists(path))
                    absPath = path;
            }

            if (string.IsNullOrEmpty(absPath))
                Logger.Error($"Could not find file: <{path}>");
            else {
                mediaPlayer.PlaySound(absPath, volume);
            }
        }

        #endregion
        #region /ub vitae
        [Summary("Thinks to yourself with your current vitae percentage")]
        [Usage("/ub vitae")]
        [CommandPattern("vitae", @"^$")]
        public void DoVitae(string _, Match _1) {
            UB_vitae();
        }
        private void UB_vitae() {
            Util.Think($"My vitae is {UB.Core.CharacterFilter.Vitae}%");
        }
        #endregion
        #region /ub combatstate
        [Summary("Sets combat state")]
        [Usage("/ub combatstate (peace|melee|missile|magic)")]
        [CommandPattern("combatstate", @"^(?<combatState>.+)?$")]
        public void DoCombatState(string command, Match args) {
            string combatState = args.Groups["combatState"].Value;
            setcombatstate(combatState.ToString());
        }
        #endregion
        #region /ub swearallegiance[p] <name|id|selected>
        /// <summary>
        /// Temporary Home. TODO: Finish Allegiance.cs
        /// </summary>
        /// <param name="command"></param>
        /// <param name="args"></param>
        [Summary("Swear Allegiance")]
        [Usage("/ub swearallegiance[p][ <name|id|selected>]")]
        [Example("/ub swearallegiance Yonneh", "Swear Allegiance to `Yonneh`")]
        [Example("/ub swearallegiancep Yo", "Swear Allegiance to a character with a name partially matching `Yo`.")]
        [Example("/ub swearallegiance", "Swear Allegiance to the closest character")]
        [Example("/ub swearallegiance selected", "Swear Allegiance to the selected character")]
        [CommandPattern("swearallegiance", @"^(?<charName>.+)?$", true)]
        public void DoSwearAllegiance(string command, Match args) {
            string charName = args.Groups["charName"].Value;
            if (charName.Length == 0) charName = null;
            WorldObject fellowChar = Util.FindName(charName, command.Replace("swearallegiance", "").Equals("p"), new ObjectClass[] { ObjectClass.Player });
            if (fellowChar != null) {
                Logger.WriteToChat($"Swearing Allegiance to {fellowChar.Name}[0x{fellowChar.Id:X8}]");
                UBHelper.Allegiance.SwearAllegiance(fellowChar.Id);
                return;
            }
            Logger.Error($"Could not find {(charName == null ? "closest player" : $"player {charName}")} Command:{command}");
        }
        #endregion
        #region /ub breakallegiance[p] <name|id|selected>
        [Summary("Break Allegiance (TODO: scan Allegiance Heirarchy, instead of visible)")]
        [Usage("/ub breakallegiance[p][ <name|id|selected>]")]
        [Example("/ub breakallegiance Yonneh", "Break your Allegiance to `Yonneh` (noooooooooooo)")]
        [Example("/ub breakallegiancep Yo", "Break Allegiance from a character with a name partially matching `Yo`.")]
        [Example("/ub breakallegiance", "Break Allegiance from the closest character")]
        [Example("/ub breakallegiance selected", "Break Allegiance from the selected character")]
        [CommandPattern("breakallegiance", @"^(?<charName>.+)?$", true)]
        public void DoBreakAllegiance(string command, Match args) {
            string charName = args.Groups["charName"].Value;
            if (charName.Length == 0) charName = null;
            WorldObject fellowChar = Util.FindName(charName, command.Replace("breakallegiance", "").Equals("p"), new ObjectClass[] { ObjectClass.Player });
            if (fellowChar != null) {
                Logger.WriteToChat($"Breaking Allegiance from {fellowChar.Name}[0x{fellowChar.Id:X8}]");
                UBHelper.Allegiance.BreakAllegiance(fellowChar.Id);
                return;
            }
            Logger.Error($"Could not find {(charName == null ? "closest player" : $"player {charName}")} Command:{command}");
        }
        #endregion

        #region /ub use[li][p] <itemOne> on <itemTwo>
        [Summary("Use Item")]
        [Usage("/ub use[li][p] [itemOne] on [itemTwo]")]
        [Example("/ub use Cake", "Use an item with exact name of Cake")]
        [Example("/ub usepi splitting on gold pea", "Use a partial match (splitting) splitting tool on a gold pea")]
        [Example("/ub uselp plant", "Use a plant on the landscape.")]
        [Example("/ub usei Stamina Elixer", "Use a stamina elixer in your inventory.")]
        [CommandPattern("use", @"^(?<itemOne>.+?)(?=\son|$)( on (?<itemTwo>.+))?$", true)]
        public void UseItem(string command, Match args) {
            string itemOne = args.Groups["itemOne"].Value;
            string itemTwo = args.Groups["itemTwo"].Value;
            string parameters = command.Replace("use","");
            bool partial = false;

            //return if item is empty or used both i and l
            if (string.IsNullOrEmpty(itemOne)) return;
            if (parameters.Contains("l") && parameters.Contains("i")) {
                Logger.WriteToChat("l and i cannot be used in the same command"); 
                return;
            }

            if (parameters.Contains("p")) partial = true;
            if (!parameters.Contains("l") && !parameters.Contains("i"))
                UB_UseItem(itemOne, Util.WOSearchFlags.All, partial, itemTwo);
            else if (parameters.Contains("i"))
                UB_UseItem(itemOne, Util.WOSearchFlags.Inventory, partial, itemTwo);
            else if (parameters.Contains("l"))
                UB_UseItem(itemOne, Util.WOSearchFlags.Landscape, partial, itemTwo);
        }

        public void UB_UseItem(string itemOne, Util.WOSearchFlags flags, bool partial, string itemTwo = null) {
            WorldObject woOne = null;
            WorldObject woTwo = null;
            WorldObject excludeObject = null;

            if (string.IsNullOrEmpty(itemOne)) return;

            woOne = excludeObject = Util.FindObjectByName(itemOne, flags, partial, null);

            //if only itemOne exists, run use on that item
            if (woOne != null) {
                if (string.IsNullOrEmpty(itemTwo)) {
                    Logger.WriteToChat("using " + woOne.Name);
                    if (woOne.ObjectClass == ObjectClass.Portal) UB_portal(woOne.Name, partial);
                    else if (woOne.ObjectClass == ObjectClass.Vendor) UB.AutoVendor.UB_vendor_open(woOne.Name, partial);
                    else if (woOne.ObjectClass == ObjectClass.Container || woOne.ObjectClass == ObjectClass.Corpse) UB.Looter.OpenContainer(woOne.Id);
                    else UB.Core.Actions.UseItem(woOne.Id, 0);
                }
                else if (!string.IsNullOrEmpty(itemTwo)) {
                    woTwo = Util.FindObjectByName(itemTwo, Util.WOSearchFlags.All, partial, excludeObject);
                    if (woTwo == null) {
                        Logger.WriteToChat(itemTwo + " is null");
                        return;
                    }
                    Logger.WriteToChat("using " + woOne.Name + " on " + woTwo.Name);
                    if (woOne.ObjectClass == ObjectClass.WandStaffOrb) {
                        if (UB.Core.Actions.CombatMode == CombatState.Magic && UB.Core.Actions.BusyState == 0) {
                            UB.Core.Actions.SelectItem(woTwo.Id);
                            UB.Core.Actions.UseItem(woOne.Id, 1, woTwo.Id);
                        }
                    }
                    else UB.Core.Actions.ApplyItem(woOne.Id, woTwo.Id);
                }
            }
            else {
                Logger.Error($"Could not find object: {itemOne}");
            }
        }
        #endregion
        #region /ub select[li][p] <itemOne>
        [Summary("Select Item")]
        [Usage("/ub select[li][p] [item]")]
        [Example("/ub select Cake", "Select an item with exact name of Cake")]
        [Example("/ub selectpi gold", "Select a partial match to the word gold ")]
        [Example("/ub selectlp plant", "Select a partial match of a plant on the landscape.")]
        [CommandPattern("select", @"^(?<itemOne>.+?)$", true)]
        public void SelectItem(string command, Match args) {
            string itemOne = args.Groups["itemOne"].Value;
            string parameters = command.Replace("select", "");
            bool partial = false;
            WorldObject woOne = null;

            //return if item is empty or used both i and l
            if (string.IsNullOrEmpty(itemOne)) return;

            if (parameters.Contains("l") && parameters.Contains("i")) {
                Logger.WriteToChat("l and i cannot be used in the same command");
                return;
            }

            if (parameters.Contains("p")) partial = true;
            if (!parameters.Contains("l") && !parameters.Contains("i"))
                woOne = Util.FindObjectByName(itemOne, Util.WOSearchFlags.All, partial, null);
            else if (parameters.Contains("i"))
                woOne = Util.FindObjectByName(itemOne, Util.WOSearchFlags.Inventory, partial, null);
            else if (parameters.Contains("l"))
                woOne = Util.FindObjectByName(itemOne, Util.WOSearchFlags.Landscape, partial, null);

            if (woOne != null) UB.Core.Actions.SelectItem(woOne.Id);

        }
        #endregion
        #region /ub close <corpse|chest>
        [Summary("Close the open corpse")]
        [Usage("/ub close corpse")]
        [Example("/ub close corpse", "Close the corpse if one is open")]
        [CommandPattern("close", @"^(?<objectclass>.+)?$", true)]
        public void DoClose(string command, Match args) {
            string objectClass = args.Groups["objectclass"].Value;
            if (objectClass.Length == 0) objectClass = null;
            int openContainer = 0;
            openContainer = UB.Core.Actions.OpenedContainer;
            if (openContainer == 0) {
                Logger.WriteToChat("No container is currently open.");
                return;
            }
            if (objectClass == "corpse" && UB.Core.WorldFilter[openContainer].ObjectClass == ObjectClass.Corpse) {
                UB.Core.Actions.UseItem(openContainer, 0);
            }
            else if (objectClass == "chest" && UB.Core.WorldFilter[openContainer].ObjectClass == ObjectClass.Container) {
                UB.Core.Actions.UseItem(openContainer, 0);
            }

        }
        #endregion
        #endregion

        #region Expressions
        #region exec[]
        [ExpressionMethod("exec")]
        [ExpressionParameter(0, typeof(string), "text", "The expression string to evaluate")]
        [ExpressionReturn(typeof(object), "Returns the result of evaluating the expression string")]
        [Summary("Evaluates a string as an expression")]
        [Example("exec[`1+1`]", "returns 2")]
        public object Exec(string expression) {
            return UB.VTank.EvaluateExpression(expression, true);
        }
        #endregion //exec[]
        #region delayexec[]
        [ExpressionMethod("delayexec")]
        [ExpressionParameter(0, typeof(double), "delay", "The delay in milliseconds. 1 second = 1000 milliseconds.")]
        [ExpressionParameter(1, typeof(string), "text", "The expression string to evaluate")]
        [ExpressionReturn(typeof(object), "Returns the id of the delayexec")]
        [Summary("Evaluates a string as an expression, after the specified delay")]
        [Example("delayexec[1000, `1+1`]", "evaluates the expression `1+1` after a 1000ms (1s) delay")]
        public object DelayExec(double delay, string expression) {
            var delayed = AddDelayedExpression(expression, delay);
            return (double)delayed.Id;
        }
        #endregion //delayexec[]
        #region clearexec[]
        [ExpressionMethod("clearexec")]
        [ExpressionParameter(0, typeof(double), "id", "The delayexec id to clear")]
        [ExpressionReturn(typeof(object), "Returns 1 if successful, 0 if delayexec with that id was not found")]
        [Summary("Cancels the delayexec with the specified id")]
        [Example("clearexec[delayexec[1000, `1+1`]]", "Creates a delayed exec statement and immediately cancels it.")]
        public object Clearexec(double id) {
            var intId = (int)id;
            DelayedExpression exp = delayedExpressions.Find(e => e.Id == intId);

            if (exp == null) {
                return 0;
            }
            else {
                delayedExpressions.Remove(exp);
                return 1;
            }
        }
        #endregion //clearexec[]
        #region ifthen[]
        [ExpressionMethod("ifthen")]
        [ExpressionParameter(0, typeof(object), "value", "value to check for truthiness")]
        [ExpressionParameter(1, typeof(string), "trueexpr", "expression string to evaluate if value is truthy")]
        [ExpressionParameter(2, typeof(string), "falseexpr", "expression string to evaluate if value is *not* truthy, optional")]
        [ExpressionReturn(typeof(object), "Returns the result of evaluating the conditional expression string")]
        [Summary("If value is truthy, will execute the trueexpr string and return the results. If value is *not* truthy it will evaluate falseexpr string and return the results")]
        [Example("ifthen[1, `chatbox[\\`true\\`]`,`chatbox[\\`false\\`]`]", "writes `true` to the chatbox")]
        [Example("ifthen[0, `chatbox[\\`true\\`]`,`chatbox[\\`false\\`]`]", "writes `false` to the chatbox")]
        public object IfThen(object condition, string trueExpression, string falseExpression=null) {
            if (ExpressionVisitor.IsTruthy(condition)) {
                return UB.VTank.EvaluateExpression(trueExpression, true);
            }
            else if (falseExpression != null) {
                return UB.VTank.EvaluateExpression(falseExpression, true);
            }

            return (double)0;
        }
        #endregion //ifthen[]
        #region tostring[object obj]
        [ExpressionMethod("tostring")]
        [ExpressionParameter(0, typeof(object), "obj", "The object to convert to a string")]
        [ExpressionReturn(typeof(object), "Returns the object, converted to a string")]
        [Summary("Converts an object to a string")]
        [Example("tostring[2]", "returns the string `2`")]
        public object ObjToString(object obj) {
            return obj.ToString();
        }
        #endregion //vitae[]
        #region vitae[]
        [ExpressionMethod("vitae")]
        [ExpressionReturn(typeof(double), "Returns a number")]
        [Summary("Gets your character's current vitae percentage as a number")]
        [Example("vitae[]", "returns your current vitae % as a number")]
        public object Vitae() {
            return UB.Core.CharacterFilter.Vitae;
        }
        #endregion //vitae[]
        #region getaccounthash[]
        [ExpressionMethod("getaccounthash")]
        [ExpressionReturn(typeof(string), "Returns a hash of the current account name")]
        [Summary("Returns a hash of the current account name")]
        [Example("getaccounthash[]", "Returns a hash of the current account name")]
        public object Getaccounthash() {
            return $"{CoreManager.Current.CharacterFilter.AccountName.GetHashCode():X}";
        }
        #endregion //getaccounthash[]
        #region getworldname[]
        [ExpressionMethod("getworldname")]
        [ExpressionReturn(typeof(string), "Returns the name of the current world/server")]
        [Summary("Gets the name of the currently connected world/server")]
        [Example("getworldname[]", "Returns the name of the current world/server")]
        public object Getworldname() {
            return UBHelper.Core.WorldName;
        }
        #endregion //getworldname[]
        #region getdatetimelocal[string format]
        [ExpressionMethod("getdatetimelocal")]
        [ExpressionParameter(0, typeof(string), "text", "The format to return the date/time")]
        [ExpressionReturn(typeof(string), "Returns the local date and time")]
        [Summary("Gets the local date and time using the format provided")]
        [Example("getdatetimelocal[`hh:mm:ss tt`]", "Returns current local time '06:09:01 PM'")]
        public string Getdatetimelocal(string format = "hh:mm:ss tt") {
            try {
                return DateTime.Now.ToString(format).ToString();
            }
            catch (Exception ex) {
                Logger.WriteToChat(ex.Message);
            }
            return "";
        }
        #endregion //getdatetimelocal[string format]
        #region getdatetimeutc[string format]
        [ExpressionMethod("getdatetimeutc")]
        [ExpressionParameter(0, typeof(string), "text", "The format to return the date/time")]
        [ExpressionReturn(typeof(string), "Returns the universal date and time")]
        [Summary("Gets the universal date and time using the format provided")]
        [Example("getdatetimeutc[`hh:mm:ss tt`]", "Returns current universal time '06:09:01 PM'")]
        public string Getdatetimeutc(string format = "hh:mm:ss tt") {
            try {
                return DateTime.UtcNow.ToString(format).ToString();
            }
            catch (Exception ex) {
                Logger.WriteToChat(ex.Message);
            }
            return "";
        }
        #endregion //getdatetimeutc[string format]
        #region getunixtime[]
        [ExpressionMethod("getunixtime")]
        [ExpressionReturn(typeof(string), "Returns the number of seconds since the unix epoch")]
        [Summary("Gets the total number of seconds since the unix epoch (jan 1, 1970)")]
        [Example("getunixtime[]", "Returns the number of seconds since the unix epoch")]
        public double Getunixtime() {
            return (double)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }
        #endregion //getunixtime[]

        #region wobjectfindnearestbytemplatetype[int templatetype]
        [ExpressionMethod("wobjectfindnearestbytemplatetype")]
        [ExpressionParameter(0, typeof(double), "templatetype", "templatetype to filter by")]
        [ExpressionReturn(typeof(ExpressionWorldObject), "Returns a worldobject")]
        [Summary("Attempted to find the nearest landscape world object with the specified template type")]
        [Example("wobjectfindnearestbytemplatetype[42137]", "Returns a worldobject with templaye type 42137 (level 10 ice tachi warden)")]
        public object Wobjectfindnearestbytemplatetype(double templateType) {
            WorldObject closest = null;
            var closestDistance = double.MaxValue;
            var wos = UtilityBeltPlugin.Instance.Core.WorldFilter.GetLandscape();
            var typeInt = Convert.ToInt32(templateType);
            foreach (var wo in wos) {
                if (wo.Type == typeInt) {
                    if (wo.Id == UtilityBeltPlugin.Instance.Core.CharacterFilter.Id)
                        continue;
                    if (PhysicsObject.GetDistance(wo.Id) < closestDistance) {
                        closest = wo;
                        closestDistance = PhysicsObject.GetDistance(wo.Id);
                    }
                }
            }
            wos.Dispose();

            if (closest != null)
                return new ExpressionWorldObject(closest.Id);

            return 0;
        }
        #endregion //wobjectfindnearestbytemplatetype[int templatetype]
        #region wobjectgetintprop[wobject obj, int property]
        [ExpressionMethod("wobjectgetintprop")]
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "obj", "World object to get int property of")]
        [ExpressionParameter(1, typeof(double), "property", "IntProperty to return")]
        [ExpressionReturn(typeof(double), "Returns an int property value")]
        [Summary("Returns an int property from a specific world object, or 0 if it's undefined")]
        [Example("wobjectgetintprop[wobjectgetselection[],218103808]", "Returns the template type of the currently selected object")]
        public object Wobjectgetintprop(ExpressionWorldObject wobject, double property) {
            return wobject.Wo.Values((LongValueKey)Convert.ToInt32(property), 0);
        }
        #endregion //wobjectgetintprop[wobject obj, int property]
        #region wobjectgetdoubleprop[wobject obj, int property]
        [ExpressionMethod("wobjectgetdoubleprop")]
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "obj", "World object to get double property of")]
        [ExpressionParameter(1, typeof(double), "property", "DoubleProperty to return")]
        [ExpressionReturn(typeof(double), "Returns a double property value")]
        [Summary("Returns a double property from a specific world object, or 0 if it's undefined")]
        [Example("wobjectgetdoubleprop[wobjectgetselection[],167772169]", "Returns the salvage workmanship of the currently selected object")]
        public object Wobjectgetdoubleprop(ExpressionWorldObject wobject, double property) {
            return wobject.Wo.Values((DoubleValueKey)Convert.ToInt32(property), 0);
        }
        #endregion //wobjectgetdoubleprop[wobject obj, int property]
        #region wobjectgetboolprop[wobject obj, int property]
        [ExpressionMethod("wobjectgetboolprop")]
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "obj", "World object to get bool property of")]
        [ExpressionParameter(1, typeof(double), "property", "BoolProperty to return")]
        [ExpressionReturn(typeof(double), "Returns a bool property value")]
        [Summary("Returns a bool property from a specific world object, or 0 if it's undefined")]
        [Example("wobjectgetboolprop[wobjectgetselection[],99]", "Returns 1 if the currently selected object is ivoryable")]
        public object Wobjectgetboolprop(ExpressionWorldObject wobject, double property) {
            return wobject.Wo.Values((BoolValueKey)Convert.ToInt32(property), false);
        }
        #endregion //wobjectgetboolprop[wobject obj, int property]
        #region wobjectgetstringprop[wobject obj, int property]
        [ExpressionMethod("wobjectgetstringprop")]
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "obj", "World object to get string property of")]
        [ExpressionParameter(1, typeof(double), "property", "StringProperty to return")]
        [ExpressionReturn(typeof(string), "Returns a string property value")]
        [Summary("Returns a string property from a specific world object, or and empty string if it's undefined")]
        [Example("wobjectgetstringprop[wobjectgetselection[],1]", "Returns the name of the currently selected object")]
        public object Wobjectgetstringprop(ExpressionWorldObject wobject, double property) {
            return wobject.Wo.Values((StringValueKey)Convert.ToInt32(property), "");
        }
        #endregion //wobjectgetstringprop[wobject obj, int property]
        #region wobjecthasdata[wobject obj]
        [ExpressionMethod("wobjecthasdata")]
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "obj", "World object to check if we have assess data for")]
        [ExpressionReturn(typeof(double), "Returns 1 if we have assess data for the object")]
        [Summary("Checks if a wobject has assess data from the server")]
        [Example("wobjecthasdata[wobjectgetselection[]]", "Returns 1 if the currently selected object has assess data from the server")]
        public object WobjectHasData(ExpressionWorldObject wobject) {
            return wobject.Wo.HasIdData;
        }
        #endregion //wobjecthasdata[wobject obj]
        #region wobjectrequestdata[wobject obj]
        [ExpressionMethod("wobjectrequestdata")]
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "obj", "World object to request assess data for")]
        [ExpressionReturn(typeof(double), "Returns 1")]
        [Summary("Requests assess data for a wobject from the server")]
        [Example("wobjectrequestdata[wobjectgetselection[]]", "Requests assess data from the server for the currently selected object")]
        public object wobjectrequestdata(ExpressionWorldObject wobject) {
            UB.Core.Actions.RequestId(wobject.Id);
            return 1;
        }
        #endregion //wobjectrequestdata[wobject obj]
        #region wobjectlastidtime[wobject obj]
        [ExpressionMethod("wobjectlastidtime")]
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "obj", "World object to check")]
        [ExpressionReturn(typeof(double), "Returns 1")]
        [Summary("Gets the last time a wobjects data was recieved from the server")]
        [Example("wobjectlastidtime[wobjectgetselection[]]", "Checks the last time the server sent id data for the currently selected object")]
        public object wobjectlastidtime(ExpressionWorldObject wobject) {
            return wobject.Wo.LastIdTime;
        }
        #endregion //wobjectlastidtime[wobject obj]
        #region wobjectgetheadingto[wobject obj]
        [ExpressionMethod("getheadingto")]
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "obj", "World object to calculate heading towards")]
        [ExpressionReturn(typeof(double), "Returns the heading in degrees from your player to the target object")]
        [Summary("Calculates the heading in degrees (0-360 clockwise, 0 is north) from your player to the target object")]
        [Example("getheadingto[wobjectgetselection[]]", "Returns the heading in degrees from your player to the target object")]
        public object Getheadingto(ExpressionWorldObject wobject) {
            var me = PhysicsObject.GetPosition(UB.Core.CharacterFilter.Id);
            var target = PhysicsObject.GetPosition(wobject.Wo.Id);

            return Geometry.CalculateHeading(me, target);
        }
        #endregion //wobjectgetheadingto[wobject obj]
        #region wobjecthasvalidphysics[wobject obj]
        [ExpressionMethod("wobjectisvalid")]
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "obj", "World object to calculate heading towards")]
        [ExpressionReturn(typeof(double), "Returns the heading in degrees from your player to the target object")]
        [Summary("Calculates the heading in degrees (0-360 clockwise, 0 is north) from your player to the target object")]
        [Example("wobjectisvalid[wobjectgetselection[]]", "Returns the heading in degrees from your player to the target object")]
        public object wobjectisvalid(ExpressionWorldObject wobject) {
            return UB.Core.Actions.IsValidObject(wobject.Id) && UB.Core.Actions.Underlying.GetPhysicsObjectPtr(wobject.Id) != 0 ? 1 : 0;
        }
        #endregion //wobjectgetheadingto[wobject obj]
        #region getequippedweapontype[]
        [ExpressionMethod("getequippedweapontype")]
        [ExpressionReturn(typeof(string), "Returns the equipped weapons object class")]
        [Summary("Returns the equipped weapons object class")]
        [Example("getequippedweapontype[]", "Returns the equipped weapons object class")]
        public object getequippedweapontype() {
            List<WorldObject> equippedList = UB.EquipmentManager.GetEquippedItems();
            string result = "";
            foreach (WorldObject item in equippedList) {
                switch (item.Values(LongValueKey.EquipableSlots)) {
                    case 4194304:
                        result = "Missile";
                        break;
                    case 16777216:
                        result = "Wand";
                        break;
                    case 1048576:
                        result = "Melee";
                        break;
                    default:
                        result = "None";
                        break;
                }
                if (result != "None") break;
            }
            return result;
        }
        #endregion //wobjectgetequippedweapon[worldobject wo]
        #region getheading[wobject obj]
        [ExpressionMethod("getheading")]
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "obj", "World object to get the heading of")]
        [ExpressionReturn(typeof(double), "Returns the heading in degrees that the target object is facing")]
        [Summary("Returns the heading in degrees (0-360 clockwise, 0 is north) the target object is facing")]
        [Example("getheading[wobjectgetselection[]]", "Returns the heading in degrees that your current selection is facing")]
        public object Wobjectgetheadingto(ExpressionWorldObject wobject) {
            var rot = PhysicsObject.GetRot(wobject.Wo.Id);
            var heading = Geometry.QuaternionToHeading(rot);

            //Logger.WriteToChat(heading.ToString());
            //Logger.WriteToChat(rot.ToString());

            return (heading * 180f / Math.PI) % 360 + 180;
        }
        #endregion //getheading[wobject obj]
        #region getcombatstate[]
        [ExpressionMethod("getcombatstate")]
        [ExpressionReturn(typeof(string), "Returns your current combat state")]
        [Summary("Get the combat state of your character")]
        [Example("getcombatstate[]", "Returns the string *peace* if you are in peace mode")]
        public object getcombatstate() {
            return (string)UB.Core.Actions.CombatMode.ToString();
        }
        #endregion getcombatstate[]
        #region setcombatstate[]
        [ExpressionMethod("setcombatstate")]
        [ExpressionParameter(0, typeof(string), "str", "combatstate to set")]
        [ExpressionReturn(typeof(double), "Returns 1 if executed successfully.")]
        [Summary("Sets the combat state of your character")]
        [Example("setcombatstate[peace]", "Sets your combat state to peace mode")]
        public object setcombatstate(string combatState) {
            combatState = combatState.ToLower();
            switch (combatState) {
               case "peace":
                   UB.Core.Actions.SetCombatMode(CombatState.Peace);
                   break;
               case "melee":
                   UB.Core.Actions.SetCombatMode(CombatState.Melee);
                   break;
               case "missile":
                   UB.Core.Actions.SetCombatMode(CombatState.Missile);
                   break;
               case "magic":
                   UB.Core.Actions.SetCombatMode(CombatState.Magic);
                   break;
                default:
                    Logger.Error(combatState + " is not a valid option");
                    return (double)0;
            }
            return (double)1;
        }
        #endregion //setcombatstate[]
        #region getbusystate[]
        [ExpressionMethod("getbusystate")]
        [ExpressionReturn(typeof(double), "Returns the number corresponding to your busy state (moving an item)")]
        [Summary("Get the busy state of your character and returns a number for the state (0=idle, 1=combining a stack, 2=splitting a stack, 3=???, 4=picking up an item from the ground, 5=moving or unequipping an item, 6=dropping an item to the ground, 7=equipping an item)")]
        [Example("getbusystate[]", "Returns 0 if your character is idle, otherwise returns the appropriate value")]
        public object getbusystate() {
            return (double)UB.Core.Actions.BusyState;
        }
        #endregion //getbusystate[]
        #region hexstr[number d, number h]
        [ExpressionMethod("hexstr")]
        [ExpressionParameter(0, typeof(double), "d", "the number to convert")]
        [ExpressionReturn(typeof(double), "Returns a string")]
        [Summary("converts a number to a hexadecimal string")]
        public object Hexstr(double d) {
            return $"0x{(int)d:X}";
        }
        #endregion //acos[number d]
        #region Math Expressions
        #region acos[number d]
        [ExpressionMethod("acos")]
        [ExpressionParameter(0, typeof(double), "d", "A number representing a cosine, where d must be greater than or equal to -1, but less than or equal to 1.")]
        [ExpressionReturn(typeof(double), "Returns the angle whos cosine is the specified number")]
        [Summary("Returns the angle whos cosine is the specified number")]
        public object Acos(double d) {
            return Math.Acos(d);
        }
        #endregion //acos[number d]
        #region asin[number d]
        [ExpressionMethod("asin")]
        [ExpressionParameter(0, typeof(double), "d", "A number representing a sine, where d must be greater than or equal to -1, but less than or equal to 1.")]
        [ExpressionReturn(typeof(double), "Returns the angle whose sine is the specified number")]
        [Summary("Returns the angle whose sine is the specified number")]
        public object Asin(double d) {
            return Math.Asin(d);
        }
        #endregion //asin[number d]
        #region atan[number d]
        [ExpressionMethod("atan")]
        [ExpressionParameter(0, typeof(double), "d", "A number representing a tangent.")]
        [ExpressionReturn(typeof(double), "Returns the angle whose tangent is the specified number.")]
        [Summary("Returns the angle whose tangent is the specified number.")]
        public object Atan(double d) {
            return Math.Atan(d);
        }
        #endregion //atan[number d]
        #region atan2[number d]
        [ExpressionMethod("atan2")]
        [ExpressionParameter(0, typeof(double), "y", "The y coordinate of a point.")]
        [ExpressionParameter(0, typeof(double), "x", "The x coordinate of a point.")]
        [ExpressionReturn(typeof(double), "Returns the angle whose tangent is the quotient of two specified numbers.")]
        [Summary("Returns the angle whose tangent is the quotient of two specified numbers.")]
        public object Atan2(double y, double x) {
            return Math.Atan2(y, x);
        }
        #endregion //atan2[number d]
        #region cos[number d]
        [ExpressionMethod("cos")]
        [ExpressionParameter(0, typeof(double), "d", "An angle, measured in radians.")]
        [ExpressionReturn(typeof(double), "Returns the cosine of the specified angle.")]
        [Summary("Returns the cosine of the specified angle.")]
        public object Cos(double d) {
            return Math.Cos(d);
        }
        #endregion //cos[number d]
        #region cosh[number d]
        [ExpressionMethod("cosh")]
        [ExpressionParameter(0, typeof(double), "d", "An angle, measured in radians.")]
        [ExpressionReturn(typeof(double), "Returns the hyperbolic cosine of the specified angle.")]
        [Summary("Returns the hyperbolic cosine of the specified angle.")]
        public object Cosh(double d) {
            return Math.Cosh(d);
        }
        #endregion //cosh[number d]
        #region sin[number d]
        [ExpressionMethod("sin")]
        [ExpressionParameter(0, typeof(double), "d", "An angle, measured in radians.")]
        [ExpressionReturn(typeof(double), "Returns the sine of the specified angle.")]
        [Summary("Returns the sine of the specified angle.")]
        public object Sin(double d) {
            return Math.Sin(d);
        }
        #endregion //sin[number d]
        #region sinh[number d]
        [ExpressionMethod("sinh")]
        [ExpressionParameter(0, typeof(double), "d", "An angle, measured in radians.")]
        [ExpressionReturn(typeof(double), "Returns the hyperbolic sine of the specified angle.")]
        [Summary("Returns the hyperbolic sine of the specified angle.")]
        public object Sinh(double d) {
            return Math.Sinh(d);
        }
        #endregion //sinh[number d]
        #region sqrt[number d]
        [ExpressionMethod("sqrt")]
        [ExpressionParameter(0, typeof(double), "d", "The number whose square root is to be found")]
        [ExpressionReturn(typeof(double), "Returns the square root of a specified number.")]
        [Summary("Returns the square root of a specified number.")]
        public object Sqrt(double d) {
            return Math.Sqrt(d);
        }
        #endregion //sqrt[number d]
        #region tan[number d]
        [ExpressionMethod("tan")]
        [ExpressionParameter(0, typeof(double), "d", "An angle, measured in radians.")]
        [ExpressionReturn(typeof(double), "Returns the tangent of the specified angle.")]
        [Summary("Returns the tangent of the specified angle.")]
        public object Tan(double d) {
            return Math.Tan(d);
        }
        #endregion //tan[number d]
        #region tanh[number d]
        [ExpressionMethod("tanh")]
        [ExpressionParameter(0, typeof(double), "d", "An angle, measured in radians.")]
        [ExpressionReturn(typeof(double), "Returns the hyperbolic tangent of the specified angle.")]
        [Summary("Returns the hyperbolic tangent of the specified angle.")]
        public object Tanh(double d) {
            return Math.Tanh(d);
        }
        #endregion //tanh[number d]
        #endregion Math
        #region List Expressions
        #region listcreate[...items]
        [ExpressionMethod("listcreate")]
        [ExpressionReturn(typeof(ExpressionList), "Creates and returns a new list")]
        [ExpressionParameter(0, typeof(ParamArrayAttribute), "items", "items to isntantiate the list with")]
        [Summary("Creates a new list object.  The list is empty by default.")]
        [Example("listcreate[]", "Returns a new empty list")]
        [Example("listcreate[1,2,3]", "Returns a new list with 3 items: 1,2,3")]
        [Example("setvar[myList,listcreate[]]", "Creates a new list and stores it in `myList` variable")]
        public object ListCreate(params object[] items) {
            var list = new ExpressionList();
            if (items != null && items.Length > 0) {
                foreach(var item in items)
                    list.Items.Add(item);
            }
            return list;
        }
        #endregion //listcreate[...items]
        #region listadd[list list, object item]
        [ExpressionMethod("listadd")]
        [ExpressionParameter(0, typeof(ExpressionList), "list", "The list to add the item to")]
        [ExpressionParameter(1, typeof(object), "item", "Item to add to list, can be any type. Lists cannot contain references to themselves.")]
        [ExpressionReturn(typeof(ExpressionList), "Returns the list object")]
        [Summary("Adds an item to the end of a list")]
        [Example("listadd[getvar[myList],`some value`]", "Adds `some value` string to the list stored in `myList` variable")]
        public object ListAdd(ExpressionList list, object item) {
            list.Items.Add(item);
            return list;
        }
        #endregion //listadd[list list, object item]
        #region listinsert[list list, object item, number index]
        [ExpressionMethod("listinsert")]
        [ExpressionParameter(0, typeof(ExpressionList), "list", "The list to add the item to")]
        [ExpressionParameter(1, typeof(object), "item", "Item to add to list, can be any type. Lists cannot contain references to themselves.")]
        [ExpressionParameter(0, typeof(double), "index", "The index in the list where the item should be inserted")]
        [ExpressionReturn(typeof(ExpressionList), "Returns the list object")]
        [Summary("Inserts an item at the specified index of the list")]
        [Example("listinsert[getvar[myList],`some value`,1]", "Inserts `some value` at index 1 (second item) in the list")]
        public object ListInsert(ExpressionList list, object item, double index) {
            if (index > list.Items.Count) {
                throw new Exception($"Attempted to insert item at index {index} of list, but list only has {list.Items.Count} items");
            }
            list.Items.Insert((int)index, item);
            return list;
        }
        #endregion listinsert[list list, object item, number index]
        #region listremove[list list, object item]
        [ExpressionMethod("listremove")]
        [ExpressionParameter(0, typeof(ExpressionList), "list", "The list to remove the item from")]
        [ExpressionParameter(1, typeof(object), "item", "Item to remove from the list.")]
        [ExpressionReturn(typeof(ExpressionList), "Returns the list object")]
        [Summary("Removes the first occurance of an item from the list")]
        [Example("listremove[getvar[myList],`some value`]", "Removes the first occurance of `some value` string from the list stored in `myList` variable")]
        public object ListRemove(ExpressionList list, object item) {
            list.Items.Remove(item);
            return list;
        }
        #endregion //listremove[list list, object item]
        #region listremoveat[list list, number index]
        [ExpressionMethod("listremoveat")]
        [ExpressionParameter(0, typeof(ExpressionList), "list", "The list to remove the item from")]
        [ExpressionParameter(1, typeof(double), "index", "Index to remove from the list.")]
        [ExpressionReturn(typeof(ExpressionList), "Returns the list object")]
        [Summary("Removes the specified index from the list")]
        [Example("listremoveat[getvar[myList],1]", "Removes the second item from the list stored in `myList` variable")]
        public object ListRemoveAt(ExpressionList list, double index) {
            if (index > list.Items.Count - 1) {
                throw new Exception($"Attempted to remove item at index {index} of list, but list only has {list.Items.Count} items");
            }
            list.Items.RemoveAt((int)index);
            return list;
        }
        #endregion //listremoveat[list list, number index]
        #region listgetitem[list list, number index]
        [ExpressionMethod("listgetitem")]
        [ExpressionParameter(0, typeof(ExpressionList), "list", "The list to retrieve the item from")]
        [ExpressionParameter(1, typeof(double), "index", "Index to retrieve from the list. Lists are zero based, so the first item is at index 0")]
        [ExpressionReturn(typeof(object), "Returns the item at the specified list index")]
        [Summary("Retrieves the item at the specified list index. Lists are zero based, so the first item in the list is at index 0. If the index does not exist, it throws an error")]
        [Example("listgetitem[getvar[myList],0]", "Retrieves the first item from the list stored in `myList` variable")]
        public object ListGetItem(ExpressionList list, double index) {
            if (index > list.Items.Count - 1) {
                throw new Exception($"Attempted to get item at index {index} of list, but list only has {list.Items.Count} items");
            }
            return list.Items[(int)index];
        }
        #endregion //listgetitem[list list, number index]
        #region listcontains[list list, object item]
        [ExpressionMethod("listcontains")]
        [ExpressionParameter(0, typeof(ExpressionList), "list", "The list to check")]
        [ExpressionParameter(1, typeof(object), "item", "The item to check if the list contains")]
        [ExpressionReturn(typeof(double), "Returns 1 if found, 0 otherwise")]
        [Summary("Checks if the specified item is contained in the list")]
        [Example("listcontains[getvar[myList],`some value`]", "Checks if the list stored in `myList` variable contains the string `some value`")]
        public object ListContains(ExpressionList list, object item) {
            return (double)(list.Items.Contains(item) ? 1 : 0);
        }
        #endregion //listremove[list list, object item]
        #region listindexof[list list, object item]
        [ExpressionMethod("listindexof")]
        [ExpressionParameter(0, typeof(ExpressionList), "list", "The list to check")]
        [ExpressionParameter(1, typeof(object), "item", "The item to check if the list contains")]
        [ExpressionReturn(typeof(double), "Returns the index of the first occurence of the specified item, or -1 if not found")]
        [Summary("Finds the index of the first occurance of an item in a list. Indexes are zero based.")]
        [Example("listindexof[getvar[myList],`some value`]", "Returns the index of the first occurence of the string `some value`")]
        public object ListIndexOf(ExpressionList list, object item) {
            return (double)list.Items.IndexOf(item);
        }
        #endregion //listindexof[list list, object item]
        #region listlastindexof[list list, object item]
        [ExpressionMethod("listlastindexof")]
        [ExpressionParameter(0, typeof(ExpressionList), "list", "The list to check")]
        [ExpressionParameter(1, typeof(object), "item", "The item to check if the list contains")]
        [ExpressionReturn(typeof(double), "Returns the index of the last occurence of the specified item, or -1 if not found")]
        [Summary("Finds the index of the last occurance of an item in a list. Indexes are zero based.")]
        [Example("listlastindexof[getvar[myList],`some value`]", "Returns the index of the last occurence of the string `some value`")]
        public object ListLastIndexOf(ExpressionList list, object item) {
            var reversedItems = list.Items.ToList();
            reversedItems.Reverse();
            return (double)((reversedItems.Count - 1) - reversedItems.IndexOf(item));
        }
        #endregion //listlastindexof[list list, object item]
        #region listcopy[list list]
        [ExpressionMethod("listcopy")]
        [ExpressionParameter(0, typeof(ExpressionList), "list", "The list to copy")]
        [ExpressionReturn(typeof(ExpressionList), "Returns a copy of the specified list")]
        [Summary("Creates a copy of a list")]
        [Example("setvar[myListTwo,listcopy[getvar[myList]]", "Copies the list stored in myList variable to a variable called myListTwo")]
        public object ListCopy(ExpressionList list) {
            var newList = new ExpressionList();
            if (list.Items.Count > 0)
                foreach (var item in list.Items)
                    newList.Items.Add(item);
            return newList;
        }
        #endregion //listcopy[list]
        #region listreverse[list list]
        [ExpressionMethod("listreverse")]
        [ExpressionParameter(0, typeof(ExpressionList), "list", "The list to reverse")]
        [ExpressionReturn(typeof(ExpressionList), "Returns a copy of the specified list, but the order is reversed")]
        [Summary("Creates a copy of a list, and reverses the order")]
        [Example("setvar[myListReversed,listreverse[getvar[myList]]", "Copies the list stored in myList variable, reverses it, and stores it to a variable called myListReversed")]
        public object ListReverse(ExpressionList list) {
            var reversedList = new ExpressionList();
            for (var i = list.Items.Count - 1; i >= 0; i--)
                reversedList.Items.Add(list.Items[i]);
            return reversedList;
        }
        #endregion //listreverse[list]
        #region listpop[list list, number? index]
        [ExpressionMethod("listpop")]
        [ExpressionParameter(0, typeof(ExpressionList), "list", "The list to pop the item from")]
        [ExpressionParameter(1, typeof(double), "index", "Optional index to remove, if not provided, uses the index of the last item in the array", -1)]
        [ExpressionReturn(typeof(object), "Returns the value removed from the array")]
        [Summary("Removes the value at the specified index of the array and returns the item. If no index is passed, it uses the index of the last item in the array. Indexes are zero based.")]
        [Example("listpop[getvar[myList]]", "Removes and returns the last item in the list stored in myList variable")]
        [Example("listpop[getvar[myList],0]", "Removes and returns the first item in the list stored in myList variable")]
        public object ListPop(ExpressionList list, double index=-1) {
            if (index > list.Items.Count - 1) {
                throw new Exception($"Attempted to pop item at index {index} of list, but list only has {list.Items.Count} items");
            }
            var item = index == -1 ? list.Items.Last() : list.Items[(int)index];
            list.Items.RemoveAt((int)(index == -1 ? list.Items.Count-1 : index));
            return item;
        }
        #endregion //listpop[list, number? index]
        #region listcount[list list]
        [ExpressionMethod("listcount")]
        [ExpressionParameter(0, typeof(ExpressionList), "list", "The list to count the items in")]
        [ExpressionReturn(typeof(double), "Returns a count of how many items are contained in the list")]
        [Summary("Counts the number of items contained in a list")]
        [Example("listcount[getvar[myList]]", "Returns the number of items stored in the list stored in `myList` variable")]
        public object ListCount(ExpressionList list) {
            return (double)list.Items.Count;
        }
        #endregion //listcount[list]
        #region listclear[list list]
        [ExpressionMethod("listclear")]
        [ExpressionParameter(0, typeof(ExpressionList), "list", "The list to clear")]
        [ExpressionReturn(typeof(ExpressionList), "Returns the list")]
        [Summary("Clears all items from the specified list")]
        [Example("listclear[getvar[myList]]", "Removes all items from the list stored in the variable `myList`")]
        public object ListClear(ExpressionList list) {
            list.Items.Clear();
            return list;
        }
        #endregion //lsitclear[list list, string expression]
        #region listfilter[list list, string expression]
        [ExpressionMethod("listfilter")]
        [ExpressionParameter(0, typeof(ExpressionList), "list", "The list to filter")]
        [ExpressionParameter(1, typeof(string), "expression", "The expression to use for filtering. $0 (getvar[\\`0\\`]) will be set to the current iteration count, $1 will be set to the current item. return 1 to include, 0 to exclude")]
        [ExpressionReturn(typeof(ExpressionList), "Returns a new filtered list")]
        [Summary("Creates a new list filtered by the specified expression")]
        [Example("listfilter[wobjectfindallbyobjectclass[24],`wobjectgetintprop[$1,25]==275`]", "creates a new list with all nearby level 275 characters")]
        public object ListFilter(ExpressionList list, string expression) {
            var results = new ExpressionList();
            var compiled = UB.VTank.CompileExpression(expression);
            var i = 0;
            var _0 = UB.VTank.Getvar("0");
            var _1 = UB.VTank.Getvar("1");
            var _2 = UB.VTank.Getvar("2");
            foreach (var item in list.Items) {
                UB.VTank.Setvar("0", i++);
                UB.VTank.Setvar("1", item);
                if (ExpressionVisitor.IsTruthy(compiled.Run())) {
                    results.Items.Add(item);
                    UB.VTank.Setvar("0", _0);
                    UB.VTank.Setvar("1", _1);
                    UB.VTank.Setvar("2", _2);
                }
            }
            return results;
        }
        #endregion //listfilter[list list, string expression]
        #region listmap[list list, string expression]
        [ExpressionMethod("listmap")]
        [ExpressionParameter(0, typeof(ExpressionList), "list", "The list to map")]
        [ExpressionParameter(0, typeof(string), "expression", "The expression to use for mapping. $0 (getvar[\\`0\\`]) will be set to the current iteration count, $1 will be set to the current item. return the mapped result")]
        [ExpressionReturn(typeof(ExpressionList), "Returns a new mapped list")]
        [Summary("Creates a new list mapped by the specified expression")]
        [Example("listmap[wobjectfindallbyobjectclass[24],`wobjectgetstringprop[$1,5]", "creates a new list with all nearby players titles")]
        public object ListMap(ExpressionList list, string expression) {
            var results = new ExpressionList();
            var compiled = UB.VTank.CompileExpression(expression);
            var i = 0;
            var _0 = UB.VTank.Getvar("0");
            var _1 = UB.VTank.Getvar("1");
            var _2 = UB.VTank.Getvar("2");
            foreach (var item in list.Items) {
                UB.VTank.Setvar("0", i++);
                UB.VTank.Setvar("1", item);
                results.Items.Add(compiled.Run());
                UB.VTank.Setvar("0", _0);
                UB.VTank.Setvar("1", _1);
                UB.VTank.Setvar("2", _2);
            }
            return results;
        }
        #endregion //listmap[list list, string expression]
        #region listreduce[list list, string expression]
        [ExpressionMethod("listreduce")]
        [ExpressionParameter(0, typeof(ExpressionList), "list", "The list to reduce to a single value")]
        [ExpressionParameter(1, typeof(string), "expression", "The expression to use for reducing. $0 (getvar[\\`0\\`]) will be set to the current iteration count, $1 will be set to the current item, $2 will be set to the current reduced value, return the modified result")]
        [ExpressionReturn(typeof(ExpressionList), "Returns a list reduced to a single value")]
        [Summary("Reduces a list down into a single value")]
        [Example("listreduce[wobjectfindallbyobjectclass[24],`$2+wobjectgetintprop[$1,25]`]", "find the sum of all nearby character levels")]
        public object ListReduce(ExpressionList list, string expression) { 
            object result = 0;
            var compiled = UB.VTank.CompileExpression(expression);
            var i = 0;
            var _0 = UB.VTank.Getvar("0");
            var _1 = UB.VTank.Getvar("1");
            var _2 = UB.VTank.Getvar("2");
            foreach (var item in list.Items) {
                UB.VTank.Setvar("0", i++);
                UB.VTank.Setvar("1", item);
                UB.VTank.Setvar("2", result);
                result = compiled.Run();
                UB.VTank.Setvar("0", _0);
                UB.VTank.Setvar("1", _1);
                UB.VTank.Setvar("2", _2);
            }
            return result;
        }
        #endregion //listreduce[list list, string expression]
        #region listsort[list list, string expression]
        [ExpressionMethod("listsort")]
        [ExpressionParameter(0, typeof(ExpressionList), "list", "The list to sort")]
        [ExpressionParameter(1, typeof(string), "expression", "The expression to use for sorting. $1 (getvar[\\`1\\`]) will be set to item a, $2 will be set item b, return 0 if they are equal, -1 if $1 is less than $2, and 1 if $1 is greater than $2")]
        [ExpressionReturn(typeof(ExpressionList), "Returns a new list sorted by expression")]
        [Summary("Creates a list from another, sorted by expression")]
        [Example("listsort[wobjectfindallbyobjectclass[24],`setvar[la,wobjectgetintprop[$1,25]];setvar[lb,wobjectgetintprop[$2,25]];iif[$la==$lb,0,iif[$la<$lb,-1,1]]`]", "get a list of all characters sorted by level")]
        public object ListSort(ExpressionList list, string expression="") {
            var results = new ExpressionList();
            var orig = list.Items.ToList();
            if (string.IsNullOrEmpty(expression)) {
                orig.Sort();
            }
            else {
                var compiled = UB.VTank.CompileExpression(expression);
                orig.Sort((a, b) => {
                    UB.VTank.Setvar("1", a);
                    UB.VTank.Setvar("2", b);
                    return Convert.ToInt32(compiled.Run());
                });
            }
            foreach (var item in orig) {
                results.Items.Add(item);
            }
            return results;
        }
        #endregion //listsort[list list, string expression]
        #region listfromrange[int start, int end]
        [ExpressionMethod("listfromrange")]
        [ExpressionParameter(0, typeof(double), "start", "inclusive start")]
        [ExpressionParameter(1, typeof(double), "end", "inclusive end")]
        [ExpressionReturn(typeof(ExpressionList), "Returns a list of numbers between start to end, inclusive")]
        [Summary("Create a list of numbers between start and end inclusively")]
        [Example("listfromrange[1,10]", "returns a list with numbers 1-10")]
        public object ListFromRange(double start, double end) {
            int iStart = (int)start;
            int iEnd = (int)end;
            var results = new ExpressionList();

            if (iStart.Equals(iEnd)) {
                results.Items.Add(iStart);
                return results;
            }

            for (var i = iStart; i <= iEnd; i += (start < end) ? 1 : -1) {
                results.Items.Add(i);
            }
            return results;
        }
        #endregion listfromrange[int start, int end]
        #endregion //List Expresions
        #region Dictionary Expressions


        #region dictcreate[...items]
        [ExpressionMethod("dictcreate")]
        [ExpressionReturn(typeof(ExpressionDictionary), "Creates and returns a new dictionary")]
        [ExpressionParameter(0, typeof(ParamArrayAttribute), "items",
            "items to instantiate the dictionary with. alternates keys and values")]
        [Summary("Creates a new dictionary object.  The dictionary is empty by default. The list of arguments must be even and broken in key-value pairs. Keys must be strings. Values may be any type.")]
        [Example("dictcreate[]", "Returns a new empty dictionary")]
        [Example("dictcreate[a,2,b,listcreate[4,5]]", "Returns a new dictionary with 2 items: a=>2,b=>[4,5]")]
        [Example("setvar[myDict,dictcreate[]]", "Creates a new empty dictionary and stores it in `myDict` variable")]
        public object DictCreate(params object[] items) {
            ExpressionDictionary result = new ExpressionDictionary();

            if (items.Length > 0) {
                if (items.Length % 2 == 1) {
                    throw new Exception("You passed in an odd number of arguments which cannot be used to create a dictionary");
                }

                for (int i = 0; i < items.Length; i += 2) {
                    if (items[i].GetType() != typeof(string)) {
                        throw new Exception($"Error with key {items[i]}. Keys must be strings");
                    }

                    result.Items.Add((string)items[i], items[i + 1]);
                }
            }

            return result;
        }
        #endregion

        #region dictgetitem[dict dict, string key]
        [ExpressionMethod("dictgetitem")]
        [ExpressionParameter(0, typeof(ExpressionDictionary), "dict", "The dict to retrieve the item from")]
        [ExpressionParameter(1, typeof(string), "key", "Key to retrieve from the dict.")]
        [ExpressionReturn(typeof(object), "Returns the item for the specified key")]
        [Summary("Retrieves the item at the specified dictionary key. If the key does not exist, it throws an error")]
        [Example("dictgetitem[getvar[myDict],foo]", "Retrieves the item stored in `myDict` for the key `foo`")]
        public object DictionaryGetItem(ExpressionDictionary dict, string key) {
            if (!dict.Items.ContainsKey(key)) {
                throw new Exception($"Attempted to get item at for key {key} of dictionary but it was not found");
            }
            return dict.Items[key];
        }
        #endregion //dictgetitem[dict dict, string key]

        #region dictadditem[dict dict, string key, object item]
        [ExpressionMethod("dictadditem")]
        [ExpressionParameter(0, typeof(ExpressionDictionary), "dict", "The dict to add key item pair to")]
        [ExpressionParameter(1, typeof(string), "key", "Key to add to the dict")]
        [ExpressionParameter(2, typeof(object), "value", "Value to add to the dict")]
        [ExpressionReturn(typeof(double), "Returns True if it overwrote a key, False otherwise")]
        [Summary("Adds the Key Value pair to the dictionary. Returns true if it overwrote a key, false otherwise. Will throw an exception if the key is not a string")]
        [Example("dictadditem[getvar[myDict],foo,1]", "adds a key foo to `myDict` and sets the value to 1")]
        public double DictionaryAddItem(ExpressionDictionary dict, string key, object value) {
            double result = 0;
            if (dict.Items.ContainsKey(key)) {
                result = 1;
                dict.Items.Remove(key);
            }

            dict.Items.Add(key, value);

            return result;
        }
        #endregion //dictadditem[dict dict, string key, object item]

        #region dicthaskey[dict dict, string key]
        [ExpressionMethod("dicthaskey")]
        [ExpressionParameter(0, typeof(ExpressionDictionary), "dict", "The dict to check for a key")]
        [ExpressionParameter(1, typeof(string), "key", "Key to look for")]
        [ExpressionReturn(typeof(double), "Returns True if dictionary has the key, False otherwise")]
        [Summary("Checks if the dictionary contains the key. Returns true if it does, false otherwise")]
        [Example("dicthaskey[getvar[myDict],foo]", "Returns true if `myDict` has the key `foo`, false otherwise")]
        public double DictionaryHasKey(ExpressionDictionary dict, string key) {
            double result = 0;
            if (dict.Items.ContainsKey(key)) {
                result = 1;
            }

            if (key.GetType() != typeof(string)) {
                throw new Exception($"Error with key {key}. Keys must be strings");
            }

            return result;
        }
        #endregion //dicthaskey[dict dict, string key]

        #region dictremovekey[dict dict, string key]
        [ExpressionMethod("dictremovekey")]
        [ExpressionParameter(0, typeof(ExpressionDictionary), "dict", "The dict to delete the key from")]
        [ExpressionParameter(1, typeof(string), "key", "Key to remove")]
        [ExpressionReturn(typeof(double), "Returns True if dictionary had the key, False otherwise")]
        [Summary("Deletes the specified key from the dictionary.")]
        [Example("dictremovekey[getvar[myDict],foo]", "Removes the key `foo` from the dictionary `myDict`")]
        public double DictionaryRemoveKey(ExpressionDictionary dict, string key) {
            double result = 0;
            if (dict.Items.ContainsKey(key)) {
                result = 1;
            }

            if (key.GetType() != typeof(string)) {
                throw new Exception($"Error with key {key}. Keys must be strings");
            }

            dict.Items.Remove(key);

            return result;
        }
        #endregion //dicthaskey[dict dict, string key]

        #region dictkeys[dict dict]
        [ExpressionMethod("dictkeys")]
        [ExpressionParameter(0, typeof(ExpressionDictionary), "dict", "The dictionary to return keys from")]
        [ExpressionReturn(typeof(ExpressionList), "Returns a list of keys")]
        [Summary("Returns a list of keys")]
        [Example("dictkeys[getvar[myDict]]", "Returns a list of all the keys in `myDict`")]
        public ExpressionList DictionaryKeys(ExpressionDictionary dict) {
            ExpressionList result = new ExpressionList();

            foreach (var key in dict.Items.Keys) {
                result.Items.Add(key); //there's really no AddAll?
            }

            return result;
        }
        #endregion //dictkeys[dict dict]

        #region dictvalues[dict dict]
        [ExpressionMethod("dictvalues")]
        [ExpressionParameter(0, typeof(ExpressionDictionary), "dict", "The dictionary to return values from")]
        [ExpressionReturn(typeof(ExpressionList), "Returns a list of values")]
        [Summary("Returns a list of keys")]
        [Example("dictkeys[getvar[myDict]]", "Returns a list of all the values in `myDict`")]
        public ExpressionList DictionaryValues(ExpressionDictionary dict) {
            ExpressionList result = new ExpressionList();

            foreach (var key in dict.Items.Values) {
                result.Items.Add(key); //there's really no AddAll?
            }

            return result;
        }
        #endregion //dictvalues[dict dict]

        #region dictsize[dict dict]
        [ExpressionMethod("dictsize")]
        [ExpressionParameter(0, typeof(ExpressionDictionary), "dict", "The dictionary to return size of")]
        [ExpressionReturn(typeof(double), "Returns a number size")]
        [Summary("Returns the size of the dictionary")]
        [Example("dictsize[dictcreate[a,b,c,d]]", "Returns 2 as the example dict is `a=>b, c=>d`")]
        public double DictionarySize(ExpressionDictionary dict) {
            return dict.Items.Count;
        }
        #endregion //dictsize[dict dict]

        #region dictclear[dict dict]
        [ExpressionMethod("dictclear")]
        [ExpressionParameter(0, typeof(ExpressionDictionary), "dict", "The dictionary to clear")]
        [ExpressionReturn(typeof(ExpressionDictionary), "Returns the dictionary")]
        [Summary("Clears the dictionary contents and returns the dictionary")]
        [Example("dictclear[getvar[myDict]]", "Returns the empty dictionary `myDict`")]
        public ExpressionDictionary DictionaryClear(ExpressionDictionary dict) {
            foreach (var key in dict.Items.Keys) {
                //there is no clear or removeAll
                dict.Items.Remove(key);
            }

            return dict;
        }
        #endregion //dictclear[dict dict]

        #region dictcopy[dict dict]
        [ExpressionMethod("dictcopy")]
        [ExpressionParameter(0, typeof(ExpressionDictionary), "dict", "The dictionary copy")]
        [ExpressionReturn(typeof(ExpressionDictionary), "Returns the new copy")]
        [Summary("Creates a new dictionary and copies over all the key value pairs. This is a shallow copy.")]
        [Example("setvar[myDict2,dictcopy[getvar[myDict]]]", "Creates a shallow copy of `myDict` and stores it in `myDict2`")]
        public ExpressionDictionary DictionaryCopy(ExpressionDictionary dict) {
            ExpressionDictionary result = new ExpressionDictionary();

            foreach (var key in dict.Items.Keys) {
                result.Items[key] = dict.Items[key];
            }

            return result;
        }
        #endregion //dictcopy[dict dict]
        #endregion //Dictionar Expressions
        #region getregexmatch[string text, string regex]
        [ExpressionMethod("getregexmatch")]
        [ExpressionParameter(0, typeof(string), "text", "The text to check the regex against")]
        [ExpressionParameter(0, typeof(string), "regex", "The regex")]
        [ExpressionReturn(typeof(string), "Returns the part of the text that matches the specified regex")]
        [Summary("Returns the part of the text that matches the specified regex")]
        [Example("getregexmatch[`test 123`,`\\d+`]", "Returns the string `123`")]
        public object getregexmatch(string text, string regex) {
            var re = new Regex(regex);
            if (!re.IsMatch(text))
                return false;

            return re.Match(text).Value;
        }
        #endregion //getregexmatch[string text, string regex]
        #region uboptget[string name]
        [ExpressionMethod("uboptget")]
        [ExpressionParameter(0, typeof(string), "name", "The name of the variable to get. Prepend global options with `global.`.")]
        [ExpressionReturn(typeof(object), "Returns the value of the setting, if valid. 0 if invalid setting.")]
        [Summary("Returns the value of a ub setting.")]
        [Example("uboptget[`Plugin.Debug`]", "Gets the current value for the `Plugin.Debug` setting")]
        public object uboptget(string name) {
            name = name.ToLower();
            OptionResult option;
            if (name.StartsWith("global."))
                option = UBLoader.FilterCore.SettingsGlobal.Get(name);
            else if (UB.Settings.Exists(name))
                option = UB.Settings.Get(name);
            else
                option = UB.State.Get(name);

            if (option == null || option.Setting == null) {
                Logger.Error("Invalid option: " + name);
                return 0;
            }

            if (option.Setting.GetValue() is System.Collections.IList list) {
                var elist = new ExpressionList();
                foreach (var item in list) {
                    elist.Items.Add(item);
                }

                return elist;
            }

            return option.Setting.GetValue();
        }
        #endregion //uboptget[string name]
        #region uboptset[string name, object newValue]
        [ExpressionMethod("uboptset")]
        [ExpressionParameter(0, typeof(string), "name", "The name of the variable to set. Prepend global options with `global.`.")]
        [ExpressionParameter(1, typeof(object), "newValue", "The new value to set.")]
        [ExpressionReturn(typeof(object), "Returns 1 if successful, 0 if it failed.")]
        [Summary("Changes the value of a ub setting.")]
        [Example("uboptset[`Plugin.Debug`, 1]", "Sets the current value for the `Plugin.Debug` setting to true")]
        public object uboptset(string name, object newValue) {
            name = name.ToLower();
            OptionResult option;
            if (name.StartsWith("global."))
                option = UBLoader.FilterCore.SettingsGlobal.Get(name);
            else if (UB.Settings.Exists(name))
                option = UB.Settings.Get(name);
            else
                option = UB.State.Get(name);

            if (option == null || option.Setting == null) {
                Logger.Error("Invalid option: " + name);
                return 0;
            }

            if (option.Setting.GetValue() is System.Collections.IList list) {
                if (!(newValue is ExpressionList)) {
                    Logger.Error($"{name} expects a value of type list");
                    return 0;
                }
                list.Clear();
                foreach (var eItem in ((ExpressionList)newValue).Items) {
                    list.Add(eItem);
                }
                return 1;
            }
            else {
                try {
                    option.Setting.SetValue(newValue);
                    if (!UB.Plugin.Debug)
                        Logger.WriteToChat(option.Setting.FullDisplayValue());
                    return 1;
                }
                catch (Exception ex) {
                    Logger.Error(ex.Message);
                    return 0;
                }
            }

            //return 0;
        }
        #endregion //uboptset[string name, object value]
        #region isportaling[]
        [ExpressionMethod("isportaling")]
        [ExpressionReturn(typeof(double), "Returns 1 if in portalspace, 0 otherwise")]
        [Summary("Checks if your character is currently in portalspace")]
        [Example("isportaling[]", "Returns 1 if in portalspace, 0 otherwise")]
        public object isportaling() {
            if (UBHelper.Core.GameState != UBHelper.GameState.In_Game)
                return (double)1;
            return (double)(inPortal ? 1 : 0);
        }
        #endregion //isportaling[]

        #endregion //Expressions

        public Plugin(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        public override void Init() {
            base.Init();

            UBHelper.Core.GameStateChanged += Core_GameStateChanged;
            UB.Core.CharacterFilter.ChangePortalMode += CharacterFilter_ChangePortalMode;
            if (UBHelper.Core.GameState == UBHelper.GameState.In_Game) UB_Follow_Clear();
            else UB.Core.CharacterFilter.LoginComplete += CharacterFilter_LoginComplete_Follow;

            mediaPlayer = new Mp3Player(UB.Core);

            BackgroundFrameLimit.Changed += BackgroundFrameLimit_Changed;
            PCap.Changed += PCap_Changed;
            PCapBufferDepth.Changed += PCapBufferDepth_Changed;
            VideoPatch.Changed += VideoPatch_Changed;
            VideoPatchFocus.Changed += VideoPatchFocus_Changed;

            if (!Debug.IsDefault)
                UBHelper.Core.Debug = Debug;
            if (!VideoPatchFocus.IsDefault)
                UBHelper.VideoPatch.bgOnly = VideoPatchFocus;
            if (!VideoPatch.IsDefault)
                UBHelper.VideoPatch.Enabled = VideoPatch;
            if (!PCap.IsDefault)
                UBHelper.PCap.Enable(PCapBufferDepth);
            if (!BackgroundFrameLimit.IsDefault)
                UBHelper.SimpleFrameLimiter.bgMax = BackgroundFrameLimit;
        }

        private void CharacterFilter_ChangePortalMode(object sender, ChangePortalModeEventArgs e) {
            inPortal = e.Type == PortalEventType.EnterPortal;
        }

        private void VideoPatchFocus_Changed(object sender, SettingChangedEventArgs e) {
            UBHelper.VideoPatch.bgOnly = VideoPatchFocus;
        }

        private void VideoPatch_Changed(object sender, SettingChangedEventArgs e) {
            UBHelper.VideoPatch.Enabled = VideoPatch;
        }

        private void PCapBufferDepth_Changed(object sender, SettingChangedEventArgs e) {
            if (PCap)
                UBHelper.PCap.Enable(PCapBufferDepth);
        }

        private void PCap_Changed(object sender, SettingChangedEventArgs e) {
            if (PCap)
                UBHelper.PCap.Enable(PCapBufferDepth);
            else
                UBHelper.PCap.Disable();
        }

        private void BackgroundFrameLimit_Changed(object sender, SettingChangedEventArgs e) {
            UBHelper.SimpleFrameLimiter.bgMax = BackgroundFrameLimit;
        }

        private void Core_GameStateChanged(UBHelper.GameState previous, UBHelper.GameState new_state) {
            try {
                if (new_state == UBHelper.GameState.Logging_Out) {
                    UB_Follow_Clear();
                    UBHelper.VideoPatch.Enabled = false;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    VideoPatch.Changed -= VideoPatch_Changed;
                    VideoPatchFocus.Changed -= VideoPatchFocus_Changed;
                    BackgroundFrameLimit.Changed -= BackgroundFrameLimit_Changed;
                    PCap.Changed -= PCap_Changed;
                    PCapBufferDepth.Changed -= PCapBufferDepth_Changed;
                    UB.Core.RenderFrame -= Core_RenderFrame_PortalOpen;
                    UB.Core.RenderFrame -= Core_RenderFrame_Delay;
                    UB.Core.EchoFilter.ServerDispatch -= EchoFilter_ServerDispatch_PortalOpen;
                    UB.Core.CharacterFilter.LoginComplete -= CharacterFilter_LoginComplete_Follow;
                    UB.Core.CharacterFilter.ChangePortalMode -= CharacterFilter_ChangePortalMode;
                }
                disposedValue = true;
            }
        }
    }
}
