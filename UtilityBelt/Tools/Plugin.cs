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
using UBLoader.Lib.Settings;
using System.Collections.ObjectModel;
using Hellosam.Net.Collections;

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
        readonly private List<DelayedCommand> delayedCommands = new List<DelayedCommand>();

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
                    option = UBLoader.FilterCore.Settings.Get(name);
                else if (UB.Settings.Exists(name))
                    option = UB.Settings.Get(name);
                else
                    option = UB.State.Get(name);

                if (option == null || option.Setting == null) {
                    Logger.Error("Invalid option: " + name);
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
                                    Logger.Debug("Missing item to add");
                                    return;
                                }
                                list.Add(parts[1]);
                                break;

                            case "remove":
                                if (parts.Length < 2 || string.IsNullOrEmpty(parts[1].Trim())) {
                                    Logger.Debug("Missing item to remove");
                                    return;
                                }
                                list.Remove(parts[1]);
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
            var globalSettings = UBLoader.FilterCore.Settings.GetAll();

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

            DelayedCommand delayed = new DelayedCommand(command, delay);

            delayedCommands.Add(delayed);
            delayedCommands.Sort((x, y) => x.RunAt.CompareTo(y.RunAt));

            if (!isDelayListening) {
                isDelayListening = true;
                UB.Core.RenderFrame += Core_RenderFrame_Delay;
            }
        }
        public void Core_RenderFrame_Delay(object sender, EventArgs e) {
            try {
                while (delayedCommands.Count > 0 && delayedCommands[0].RunAt <= DateTime.UtcNow) {
                    LogDebug($"Executing command `{delayedCommands[0].Command}` (delay was {delayedCommands[0].Delay}ms)");
                    Util.DispatchChatToBoxWithPluginIntercept(delayedCommands[0].Command);
                    delayedCommands.RemoveAt(0);
                }

                if (delayedCommands.Count == 0) {
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
            }
            catch {
                Logger.Error($"Failed to follow {UB.Core.WorldFilter[id].Name}[0x{id:X8}] (is vTank loaded?)");
            }

        }
        private void CharacterFilter_Logoff_Follow(object sender, LogoffEventArgs e) {
            try {
                if (e.Type == LogoffEventType.Requested) UB_Follow_Clear();
            }
            catch (Exception ex) { Logger.LogException(ex); }
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
        [CommandPattern("mexec", @"^(?<Expression>.*)?$")]
        public void EvaluateExpressionCommand(string command, Match args) {
            EvaluateExpression(args.Groups["Expression"].Value);
        }

        public void EvaluateExpression(string expression) {
            try {
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

            Logger.WriteToChat($"Offset: {wo.Offset()}");
            Logger.WriteToChat($"Coords: {wo.Coordinates()}");
            Logger.WriteToChat($"RawCoords: {wo.RawCoordinates()}"); //same as offset?
            Logger.WriteToChat($"Phys lb: {lc.ToString("X8")}");
            Logger.WriteToChat($"Phys pos: x:{pos.X} y:{pos.Y} z:{pos.Z}");
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

            Logger.WriteToChat($"Property Dump for {wo.Name}");

            Logger.WriteToChat($"Id = {wo.Id} (0x{wo.Id.ToString("X8")})");
            Logger.WriteToChat($"Name = {wo.Name}");
            Logger.WriteToChat($"ActiveSpellCount = {wo.ActiveSpellCount}");
            Logger.WriteToChat($"Category = {wo.Category}");
            Logger.WriteToChat($"Coordinates = {wo.Coordinates()}");
            Logger.WriteToChat($"GameDataFlags1 = {wo.GameDataFlags1}");
            Logger.WriteToChat($"HasIdData = {wo.HasIdData}");
            Logger.WriteToChat($"LastIdTime = {wo.LastIdTime}");
            Logger.WriteToChat($"ObjectClass = {wo.ObjectClass} ({(int)wo.ObjectClass})");
            Logger.WriteToChat($"Offset = {wo.Offset()}");
            Logger.WriteToChat($"Orientation = {wo.Orientation()}");
            Logger.WriteToChat($"RawCoordinates = {wo.RawCoordinates()}");
            Logger.WriteToChat($"SpellCount = {wo.SpellCount}");

            Logger.WriteToChat("String Values:");
            foreach (var sk in wo.StringKeys) {
                Logger.WriteToChat($"  {(StringValueKey)sk}({sk}) = {wo.Values((StringValueKey)sk)}");
            }

            Logger.WriteToChat("Long Values:");
            foreach (var sk in wo.LongKeys) {
                switch ((LongValueKey)sk) {
                    case LongValueKey.Behavior:
                        Logger.WriteToChat($"  {(LongValueKey)sk}({sk}) = {wo.Values((LongValueKey)sk)}");
                        foreach (BehaviorFlag v in Enum.GetValues(typeof(BehaviorFlag))) {
                            if ((wo.Values(LongValueKey.DescriptionFormat) & (int)v) != 0) {
                                Logger.WriteToChat($"    Has Flag: {v.ToString()}");
                            }
                        }
                        break;

                    case LongValueKey.Unknown10:
                        Logger.WriteToChat($"  UseablityFlags({sk}) = {wo.Values((LongValueKey)sk)}");
                        foreach (UseFlag v in Enum.GetValues(typeof(UseFlag))) {
                            if ((wo.Values(LongValueKey.Flags) & (int)v) != 0) {
                                Logger.WriteToChat($"    Has Flag: {v.ToString()}");
                            }
                        }
                        break;

                    case LongValueKey.PhysicsDataFlags:
                        foreach (PhysicsState v in Enum.GetValues(typeof(PhysicsState))) {
                            if ((wo.PhysicsDataFlags & (int)v) != 0) {
                                Logger.WriteToChat($"    Has Flag: {v.ToString()}");
                            }
                        }
                        break;

                    case LongValueKey.Landblock:
                        Logger.WriteToChat($"  {(LongValueKey)sk}({sk}) = {wo.Values((LongValueKey)sk)} ({wo.Values((LongValueKey)sk).ToString("X8")})");
                        break;

                    case LongValueKey.Icon:
                        Logger.WriteToChat($"  {(LongValueKey)sk}({sk}) = {wo.Values((LongValueKey)sk)} (0x{(0x06000000 + wo.Values((LongValueKey)sk)).ToString("X8")})");
                        break;

                    default:
                        Logger.WriteToChat($"  {(LongValueKey)sk}({sk}) = {wo.Values((LongValueKey)sk)}");
                        break;
                }
            }

            Logger.WriteToChat("Bool Values:");
            foreach (var sk in wo.BoolKeys) {
                Logger.WriteToChat($"  {(BoolValueKey)sk}({sk}) = {wo.Values((BoolValueKey)sk)}");
            }

            Logger.WriteToChat("Double Values:");
            foreach (var sk in wo.DoubleKeys) {
                Logger.WriteToChat($"  {(DoubleValueKey)sk}({sk}) = {wo.Values((DoubleValueKey)sk)}");
            }

            Logger.WriteToChat("Spells:");
            FileService service = UB.Core.Filter<FileService>();
            for (var i = 0; i < wo.SpellCount; i++) {
                var spell = service.SpellTable.GetById(wo.Spell(i));
                Logger.WriteToChat($"  {spell.Name} ({wo.Spell(i)})");
            }
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
        #endregion

        #region Expressions
        #region vitae[]
        [ExpressionMethod("vitae")]
        [ExpressionReturn(typeof(double), "Returns a number")]
        [Summary("Gets your character's current vitae percentage as a number")]
        [Example("vitae[]", "returns your current vitae % as a number")]
        public object Vitae() {
            return UB.Core.CharacterFilter.Vitae;
        }
        #endregion //vitae[]
        #region getworldname[]
        [ExpressionMethod("getworldname")]
        [ExpressionReturn(typeof(string), "Returns the name of the current world/server")]
        [Summary("Gets the name of the currently connected world/server")]
        [Example("getworldname[]", "Returns the name of the current world/server")]
        public object Getworldname() {
            return UBHelper.Core.WorldName;
        }
        #endregion //getworldname[]
        #region wobjectfindnearestbytemplatetype[int templatetype]
        [ExpressionMethod("wobjectfindnearestbytemplatetype")]
        [ExpressionParameter(0, typeof(double), "templatetype", "templatetype to filter by")]
        [ExpressionReturn(typeof(ExpressionWorldObject), "Returns a worldobject")]
        [Summary("Attempted to find the nearest landscape world object with the specified template type")]
        [Example("wobjectfindnearestbytemplatetype[42137]", "Returns a worldobject with templaye type 42137 (level 10 ice tachi warden)")]
        public object Wobjectfindnearestbytemplatetype(double templateType) {
            WorldObject closest = null;
            var closestDistance = float.MaxValue;
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
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "obj", "World object to get intproperty of")]
        [ExpressionParameter(1, typeof(double), "property", "IntProperty to return")]
        [ExpressionReturn(typeof(double), "Returns an int property value")]
        [Summary("Returns an int property from a specific world object, or 0 if it's undefined")]
        [Example("wobjectgetintprop[wobjectgetselection[],218103808]", "Returns the template type of the currently selected object")]
        public object Wobjectgetintprop(ExpressionWorldObject wobject, double property) {
            return wobject.Wo.Values((LongValueKey)Convert.ToInt32(property), 0);
        }
        #endregion //wobjectgetintprop[wobject obj, int property]
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
        #region getheading[wobject obj]
        [ExpressionMethod("getheading")]
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "obj", "World object to get the heading of")]
        [ExpressionReturn(typeof(double), "Returns the heading in degrees that the target object is facing")]
        [Summary("Returns the heading in degrees (0-360 clockwise, 0 is north) the target object is facing")]
        [Example("getheading[wobjectgetselection[]]", "Returns the heading in degrees that your current selection is facing")]
        public object Wobjectgetheadingto(ExpressionWorldObject wobject) {
            var rot = PhysicsObject.GetRot(wobject.Wo.Id);
            var heading = Geometry.QuaternionToHeading(rot);

            Logger.WriteToChat(heading.ToString());
            Logger.WriteToChat(rot.ToString());

            return (heading * 180f / Math.PI) % 360 + 180;
        }
        #endregion //getheading[wobject obj]
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
        [Example("listremove[getvar[myList]1]", "Removes the second item from the list stored in `myList` variable")]
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
        #endregion //lsitclear[list]
        #endregion //List Expresions
        #endregion //Expressions

        public Plugin(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        public override void Init() {
            base.Init();

            UB.Core.CharacterFilter.Logoff += CharacterFilter_Logoff_Follow;
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
                    UB.Core.CharacterFilter.Logoff -= CharacterFilter_Logoff_Follow;
                    UB.Core.CharacterFilter.LoginComplete -= CharacterFilter_LoginComplete_Follow;
                }
                disposedValue = true;
            }
        }
    }
}
