using Decal.Adapter;
using Decal.Adapter.Wrappers;
using Decal.Filters;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Constants;
using UtilityBelt.Lib.Dungeon;

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
        private readonly Mp3Player mediaPlayer;

        private DateTime portalTimestamp = DateTime.MinValue;
        private int portalAttempts = 0;
        private static WorldObject portal = null;

        readonly private List<DelayedCommand> delayedCommands = new List<DelayedCommand>();

        #region Config
        [Summary("Check for plugin updates on login")]
        [DefaultValue(true)]
        public bool CheckForUpdates {
            get { return (bool)GetSetting("CheckForUpdates"); }
            set { UpdateSetting("CheckForUpdates", value); }
        }

        [Summary("Show debug messages")]
        [DefaultValue(false)]
        public bool Debug {
            get { return (bool)GetSetting("Debug"); }
            set { UpdateSetting("Debug", value); }
        }

        [Summary("Main UB Window X position for this character (left is 0)")]
        [DefaultValue(100)]
        public int WindowPositionX {
            get { return (int)GetSetting("WindowPositionX"); }
            set { UpdateSetting("WindowPositionX", value); }
        }

        [Summary("Main UB Window Y position for this character (top is 0)")]
        [DefaultValue(100)]
        public int WindowPositionY {
            get { return (int)GetSetting("WindowPositionY"); }
            set { UpdateSetting("WindowPositionY", value); }
        }

        [Summary("Think to yourself when portal use success/fail")]
        [DefaultValue(false)]
        public bool PortalThink {
            get { return (bool)GetSetting("PortalThink"); }
            set { UpdateSetting("PortalThink", value); }
        }

        [Summary("Timeout to retry portal use")]
        [DefaultValue(5000)]
        public int PortalTimeout {
            get { return (int)GetSetting("PortalTimeout"); }
            set { UpdateSetting("PortalTimeout", value); }
        }

        [Summary("Attempts to retry using a portal")]
        [DefaultValue(3)]
        public int PortalAttempts {
            get { return (int)GetSetting("PortalAttempts"); }
            set { UpdateSetting("PortalAttempts", value); }
        }

        [Summary("Patches the client (in realtime) to disable 3d rendering")]
        [DefaultValue(false)]
        public bool VideoPatch {
            get { return (bool)GetSetting("VideoPatch"); }
            set {
                UpdateSetting("VideoPatch", value);
                VideoPatchToggle(value);
            }
        }

        [Summary("Disables VideoPatch while the client has focus")]
        [DefaultValue(false)]
        public bool VideoPatchFocus {
            get { return (bool)GetSetting("VideoPatchFocus"); }
            set {
                UpdateSetting("VideoPatchFocus", value);
                VideoPatchFocusToggle(value);
            }
        }

        [Summary("Enables a rolling PCAP buffer, to export recent packets")]
        [DefaultValue(false)]
        public bool PCap {
            get { return (bool)GetSetting("PCap"); }
            set {
                UpdateSetting("PCap", value);

                if (UBHelper.Core.version < 1911220544) {
                    Util.WriteToChat($"Error UBHelper.dll is out of date!");
                    return;
                }

                if (value) {
                    UBHelper.PCap.Enable(PCapBufferDepth);
                }
                else {
                    UBHelper.PCap.Disable();
                }
            }
        }

        [Summary("PCap rolling buffer depth")]
        [DefaultValue(5000)]
        public int PCapBufferDepth {
            get { return (int)GetSetting("PCapBufferDepth"); }
            set {
                if (value < 200) value = 200;
                else if (value > 524287) value = 524287;
                UpdateSetting("PCapBufferDepth", value);
                if (PCap) UBHelper.PCap.Enable(value);
            }
        }
        #endregion

        #region Commands
        #region /ub
        [Summary("Prints current build version to chat")]
        [Usage("/ub")]
        [Example("/ub", "Prints current build version to chat")]
        [CommandPattern("", @"^$")]
        public void ShowVersion(string _, Match _1) {
            Util.WriteToChat("UtilityBelt Version v" + Util.GetVersion(true) + "\n Type `/ub help` or `/ub help <command>` for help.");
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

                Util.WriteToChat(help);
            }
        }

        #endregion
        #region /ub opt {list | get <option> | set <option> <newValue> | toggle <option>}
        [Summary("Manage plugin settings from the command line")]
        [Usage("/ub opt {list | get <option> | set <option> <newValue> | toggle <options>}")]
        [Example("/ub opt list", "Lists all available settings.")]
        [Example("/ub get Plugin.Debug", "Gets the current value for the \"Plugin.Debug\" setting")]
        [Example("/ub toggle Plugin.Debug", "Toggles the current value for the \"Plugin.Debug\" setting")]
        [Example("/ub set Plugin.Debug true", "Sets the \"Plugin.Debug\" setting to True")]
        [CommandPattern("opt", @"^ *(?<params>(list|toggle \S+|get \S+|set \S+ \S+)) *$")]
        public void DoOpt(string command, Match args) {
            UB_opt(args.Groups["params"].Value);
        }

        readonly private Regex optionRe = new Regex(@"^((get|set|toggle) )?(?<option>[^\s]+)\s?(?<value>.*)", RegexOptions.IgnoreCase);
        private void UB_opt(string args) {
            try {
                if (args.ToLower().Trim() == "list") {
                    Util.WriteToChat("All Settings:\n" + ListOptions(UB, ""));
                    return;
                }

                if (!optionRe.IsMatch(args.Trim())) return;

                var match = optionRe.Match(args.Trim());
                var option = UB.Settings.GetOptionProperty(match.Groups["option"].Value);
                string name = match.Groups["option"].Value;
                string newValue = match.Groups["value"].Value;

                if (option == null || option.Object == null) {
                    Util.WriteToChat("Invalid option: " + name);
                    return;
                }

                if (args.ToLower().Trim().StartsWith("toggle ")) {
                    try {
                        option.Property.SetValue(option.Parent, Convert.ChangeType(!(bool)option.Property.GetValue(option.Parent, null), option.Property.PropertyType), null);
                        if (!UB.Plugin.Debug) Util.WriteToChat(name + " = " + UB.Settings.DisplayValue(name));
                    }
                    catch (Exception ex) { Util.WriteToChat($"Unable to toggle setting {name}: {ex.Message}"); }
                    return;
                }

                if (option.Object is System.Collections.IList list) {
                    var b = new StringBuilder();
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
                                Util.WriteToChat($"Unknown verb: {parts[1]}");
                                return;
                        }
                    }

                    b.Append(name);
                    b.Append(" = [ ");
                    int i = 0;
                    foreach (var o in list) {
                        if (i++ > 0)
                            b.Append(", ");
                        b.Append(o);
                    }
                    b.Append(" ]");
                    Util.WriteToChat(b.ToString());
                }
                else if (string.IsNullOrEmpty(newValue)) {
                    Util.WriteToChat(name + " = " + UB.Settings.DisplayValue(name));
                }
                else {
                    try {
                        option.Property.SetValue(option.Parent, Convert.ChangeType(newValue, option.Property.PropertyType), null);
                        if (!UB.Plugin.Debug) Util.WriteToChat(name + " = " + UB.Settings.DisplayValue(name));
                    }
                    catch (Exception ex) { Logger.LogException(ex); }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private string ListOptions(object obj, string history) {
            var results = "";
            obj = obj ?? UB;

            if (string.IsNullOrEmpty(history)) {
                var props = UB.GetToolProps();

                foreach (var prop in props) {
                    results += ListOptions(prop.GetValue(UB, null), $"{history}{prop.Name}.");
                }
            }
            else {
                var props = obj.GetType().GetProperties();

                foreach (var prop in props) {
                    var summaryAttributes = prop.GetCustomAttributes(typeof(SummaryAttribute), true);
                    var defaultValueAttributes = prop.GetCustomAttributes(typeof(DefaultValueAttribute), true);

                    if (defaultValueAttributes.Length > 0) {
                        results += $"{history}{prop.Name} = {UB.Settings.DisplayValue(history + prop.Name, true)}\n";
                    }
                    else if (summaryAttributes.Length > 0) {
                        results += ListOptions(prop.GetValue(obj, null), $"{history}{prop.Name}.");
                    }
                }
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
                    Util.WriteToChat("Current Date: " + DateTime.UtcNow.ToString(format));
                else 
                    Util.WriteToChat("Current Date: " + DateTime.Now.ToString(format));
            }
            catch (Exception ex) {
                Util.WriteToChat(ex.Message);
            }
        }
        #endregion
        #region /ub delay <millisecondDelay> <command>
        [Summary("Thinks to yourself with your current vitae percentage")]
        [Usage("/ub delay <millisecondDelay> <command>")]
        [Example("/ub delay 5000 /say hello", "Runs \"/say hello\" after a 3000ms delay (3 seconds)")]
        [CommandPattern("delay", @"^ *(?<params>\d+ .+) *$")]
        public void DoDelay(string _, Match args) {
            UB_delay(args.Groups["params"].Value);
        }
        private void UB_delay(string theRest) {
            string[] rest = theRest.Split(' ');
            if (string.IsNullOrEmpty(theRest)
                || rest.Length < 2
                || !double.TryParse(rest[0], out double delay)
                || delay <= 0
            ) {
                Util.WriteToChat("Usage: /ub delay <milliseconds> <command>");
                return;
            }

            var command = string.Join(" ", rest.Skip(1).ToArray());

            Logger.Debug($"Scheduling command `{command}` with delay of {delay}ms");

            DelayedCommand delayed = new DelayedCommand(command, delay);

            delayedCommands.Add(delayed);
            delayedCommands.Sort((x, y) => x.RunAt.CompareTo(y.RunAt));
        }
        public void Core_RenderFrame_Delay(object sender, EventArgs e) {
            try {
                while (delayedCommands.Count > 0 && delayedCommands[0].RunAt <= DateTime.UtcNow) {
                    LogDebug($"Executing command `{delayedCommands[0].Command}` (delay was {delayedCommands[0].Delay}ms)");
                    Util.DispatchChatToBoxWithPluginIntercept(delayedCommands[0].Command);
                    delayedCommands.RemoveAt(0);
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
                        Util.ThinkOrWrite("failed to use portal", PortalThink);
                        portal = null;
                        portalAttempts = 0;
                        UBHelper.vTank.Decision_UnLock(uTank2.ActionLockType.Navigation);
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        private void EchoFilter_ServerDispatch_PortalOpen(object sender, NetworkMessageEventArgs e) {
            try {
                if (portalAttempts > 0 && e.Message.Type == 0xF74B && (int)e.Message["object"] == CoreManager.Current.CharacterFilter.Id && (short)e.Message["portalType"] == 17424) { //17424 is the magic sauce for entering a portal. 1032 is the magic sauce for exiting a portal.
                    LogDebug("portal used successfully");
                    portal = null;
                    portalAttempts = 0;
                    UBHelper.vTank.Decision_UnLock(uTank2.ActionLockType.Navigation);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        #endregion
        #region /ub fixbusy
        [Summary("Fixes busystate bugs on the client side.")]
        [Usage("/ub fixbusy")]
        [CommandPattern("fixbusy", @"^$")]
        public void DoFixBusy(string _, Match _1) {
            UB_fixbusy();
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
            Util.WriteToChat($"Could not find {(characterName == null ? "closest player" : $"player {characterName}")}");
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
                Util.WriteToChat($"Following {UB.Core.WorldFilter[id].Name}[0x{id:X8}]");
            }
            catch {
                Util.WriteToChat($"Failed to follow {UB.Core.WorldFilter[id].Name}[0x{id:X8}] (is vTank loaded?)");
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
            if (UBHelper.vTank.Instance != null && UBHelper.vTank.Instance.GetNavProfile().Equals("UBFollow"))
                Util.Decal_DispatchOnChatCommand("/vt nav load ");
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
                Util.WriteToChat("pos: No object selected");
                return;
            }

            var wo = UB.Core.WorldFilter[selected];

            if (wo == null) {
                Util.WriteToChat("pos: null object selected");
                return;
            }

            var pos = PhysicsObject.GetPosition(wo.Id);
            var d = PhysicsObject.GetDistance(wo.Id);
            var lc = PhysicsObject.GetLandcell(wo.Id);

            Util.WriteToChat($"Offset: {wo.Offset()}");
            Util.WriteToChat($"Coords: {wo.Coordinates()}");
            Util.WriteToChat($"RawCoords: {wo.RawCoordinates()}"); //same as offset?
            Util.WriteToChat($"Phys lb: {lc.ToString("X8")}");
            Util.WriteToChat($"Phys pos: x:{pos.X} y:{pos.Y} z:{pos.Z}");
        }
        #endregion
        #region /ub printcolors
        [Summary("Prints out all available chat colors")]
        [Usage("/ub printcolors")]
        [Example("/ub printcolors", "Prints out all available chat colors")]
        [CommandPattern("printcolors", @"^$")]
        public void PrintChatColors(string _, Match _1) {
            foreach (var type in Enum.GetValues(typeof(ChatMessageType)).Cast<ChatMessageType>()) {
                WriteToChat($"{type} ({(int)type})", (int)type);
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
                Util.WriteToChat("propertydump: No object selected");
                return;
            }

            var wo = UB.Core.WorldFilter[selected];

            if (wo == null) {
                Util.WriteToChat("propertydump: null object selected");
                return;
            }

            Util.WriteToChat($"Property Dump for {wo.Name}");

            Util.WriteToChat($"Id = {wo.Id} (0x{wo.Id.ToString("X8")})");
            Util.WriteToChat($"Name = {wo.Name}");
            Util.WriteToChat($"ActiveSpellCount = {wo.ActiveSpellCount}");
            Util.WriteToChat($"Category = {wo.Category}");
            Util.WriteToChat($"Coordinates = {wo.Coordinates()}");
            Util.WriteToChat($"GameDataFlags1 = {wo.GameDataFlags1}");
            Util.WriteToChat($"HasIdData = {wo.HasIdData}");
            Util.WriteToChat($"LastIdTime = {wo.LastIdTime}");
            Util.WriteToChat($"ObjectClass = {wo.ObjectClass} ({(int)wo.ObjectClass})");
            Util.WriteToChat($"Offset = {wo.Offset()}");
            Util.WriteToChat($"Orientation = {wo.Orientation()}");
            Util.WriteToChat($"RawCoordinates = {wo.RawCoordinates()}");
            Util.WriteToChat($"SpellCount = {wo.SpellCount}");

            Util.WriteToChat("String Values:");
            foreach (var sk in wo.StringKeys) {
                Util.WriteToChat($"  {(StringValueKey)sk}({sk}) = {wo.Values((StringValueKey)sk)}");
            }

            Util.WriteToChat("Long Values:");
            foreach (var sk in wo.LongKeys) {
                switch ((LongValueKey)sk) {
                    case LongValueKey.Behavior:
                        Util.WriteToChat($"  {(LongValueKey)sk}({sk}) = {wo.Values((LongValueKey)sk)}");
                        foreach (BehaviorFlag v in Enum.GetValues(typeof(BehaviorFlag))) {
                            if ((wo.Values(LongValueKey.DescriptionFormat) & (int)v) != 0) {
                                Util.WriteToChat($"    Has Flag: {v.ToString()}");
                            }
                        }
                        break;

                    case LongValueKey.Unknown10:
                        Util.WriteToChat($"  UseablityFlags({sk}) = {wo.Values((LongValueKey)sk)}");
                        foreach (UseFlag v in Enum.GetValues(typeof(UseFlag))) {
                            if ((wo.Values(LongValueKey.Flags) & (int)v) != 0) {
                                Util.WriteToChat($"    Has Flag: {v.ToString()}");
                            }
                        }
                        break;

                    case LongValueKey.PhysicsDataFlags:
                        foreach (PhysicsState v in Enum.GetValues(typeof(PhysicsState))) {
                            if ((wo.PhysicsDataFlags & (int)v) != 0) {
                                Util.WriteToChat($"    Has Flag: {v.ToString()}");
                            }
                        }
                        break;

                    case LongValueKey.Landblock:
                        Util.WriteToChat($"  {(LongValueKey)sk}({sk}) = {wo.Values((LongValueKey)sk)} ({wo.Values((LongValueKey)sk).ToString("X8")})");
                        break;

                    case LongValueKey.Icon:
                        Util.WriteToChat($"  {(LongValueKey)sk}({sk}) = {wo.Values((LongValueKey)sk)} (0x{(0x06000000 + wo.Values((LongValueKey)sk)).ToString("X8")})");
                        break;

                    default:
                        Util.WriteToChat($"  {(LongValueKey)sk}({sk}) = {wo.Values((LongValueKey)sk)}");
                        break;
                }
            }

            Util.WriteToChat("Bool Values:");
            foreach (var sk in wo.BoolKeys) {
                Util.WriteToChat($"  {(BoolValueKey)sk}({sk}) = {wo.Values((BoolValueKey)sk)}");
            }

            Util.WriteToChat("Double Values:");
            foreach (var sk in wo.DoubleKeys) {
                Util.WriteToChat($"  {(DoubleValueKey)sk}({sk}) = {wo.Values((DoubleValueKey)sk)}");
            }

            Util.WriteToChat("Spells:");
            FileService service = UB.Core.Filter<FileService>();
            for (var i = 0; i < wo.SpellCount; i++) {
                var spell = service.SpellTable.GetById(wo.Spell(i));
                Util.WriteToChat($"  {spell.Name} ({wo.Spell(i)})");
            }
        }
        #endregion
        #region /ub playeroption <option> <on/true|off/false>
        [Summary("Turns on/off acclient player options.")]
        [Usage("/ub playeroption <option> {on | true | off | false}")]
        [Example("/ub playeroption AutoRepeatAttack on", "Enables the AutoRepeatAttack player option.")]
        [CommandPattern("playeroption", @"^ *(?<params>.+ (on|off|true|false)) *$")]
        public void DoPlayerOption(string _, Match args) {
            UB_playeroption(args.Groups["params"].Value);
        }
        public void UB_playeroption(string parameters) {
            string[] p = parameters.Split(' ');
            if (p.Length != 2) {
                Util.WriteToChat($"Usage: /ub playeroption <option> <on/true|off/false>");
                return;
            }
            int option;
            try {
                option = (int)Enum.Parse(typeof(UBHelper.Player.PlayerOption), p[0], true);
            }
            catch {
                Util.WriteToChat($"Invalid option. Valid values are: {string.Join(", ", Enum.GetNames(typeof(UBHelper.Player.PlayerOption)))}");
                return;
            }
            bool value = false;
            string inval = p[1].ToLower();
            if (inval.Equals("on") || inval.Equals("true"))
                value = true;

            UBHelper.Player.SetOption((UBHelper.Player.PlayerOption)option, value);
            Util.WriteToChat($"Setting {(((UBHelper.Player.PlayerOption)option).ToString())} = {value.ToString()}");
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
                Util.WriteToChat($"Could not find file: <{path}>");
            else {
                mediaPlayer.PlaySound(absPath, volume);
            }
        }

        #endregion
        #region /ub pcap {enable [bufferDepth],disable,print}
        [Summary("Manage packet captures")]
        [Usage("/ub pcap {enable [bufferDepth] | disable | print}")]
        [Example("/ub pcap enable", "Enable pcap functionality (nothing will be saved until you call /ub pcap print)")]
        [Example("/ub pcap print", "Saves the current pcap buffer to a new file in your plugin storage directory.")]
        [CommandPattern("pcap", @"^ *(?<params>(enable( \d+)?|disable|print)) *$")]
        public void DoPcap(string _, Match args) {
            UB_pcap(args.Groups["params"].Value);
        }
        public void UB_pcap(string parameters) {
            if (UBHelper.Core.version < 1911220544) {
                Util.WriteToChat($"Error UBHelper.dll is out of date!");
                return;
            }
            char[] stringSplit = { ' ' };
            string[] parameter = parameters.Split(stringSplit, 2);
            switch (parameter[0]) {
                case "enable":
                    if (parameter.Length == 2 && Int32.TryParse(parameter[1], out int parsedBufferDepth)) {
                        PCapBufferDepth = parsedBufferDepth;
                    }

                    if (PCapBufferDepth > 65535)
                        Util.WriteToChat($"WARNING: Large buffers can have negative performance impacts on the game. Buffer depths between 1000 and 20000 are recommended.");
                    Util.WriteToChat($"Enabled rolling PCap logger with a bufferDepth of {PCapBufferDepth:n0}. This will consume {(PCapBufferDepth * 505):n0} bytes of memory.");
                    Util.WriteToChat($"Issue the command [/ub pcap print] to write this out to a .pcap file for submission!");
                    PCap = true;
                    break;
                case "disable":
                    PCap = false;
                    break;
                case "print":
                    string filename = $"{Util.GetPluginDirectory()}\\pkt_{DateTime.UtcNow:yyyy-M-d}_{(int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds}_log.pcap";
                    UBHelper.PCap.Print(filename);
                    break;
                default:
                    Util.WriteToChat("Usage: /ub pcap {enable,disable,print}");
                    break;
            }
        }
        #endregion
        #region /ub resolution <width> <height>
        [Summary("Set client resolution. This will take effect immediately, but will not change the settings page, or persist through relogging.")]
        [Usage("/ub resolution <width> <height>")]
        [Example("/ub resolution 640 600", "Set client resolution to 640 x 600")]
        [CommandPattern("resolution", @"^(?<Width>\d+)[x ](?<Height>\d+)$")]
        public void DoResolution(string _, Match args) {
            Util.Rect windowStartRect = new Util.Rect();
            DateTime lastResolutionChange = DateTime.MinValue;
            ushort.TryParse(args.Groups["Width"].Value, out ushort width);
            ushort.TryParse(args.Groups["Height"].Value, out ushort height);
            if (height < 100 || height > 10000 || width < 100 || width > 10000) {
                LogError($"Requested resolution was not valid ({width}x{height})");
                return;
            }

            void core_WindowMessage(object sender, WindowMessageEventArgs e) {
                try {
                    var elapsed = DateTime.UtcNow - lastResolutionChange;
                    var didMove = false;
                    if ((e.Msg == 0x0046 || e.Msg == 0x0047) && elapsed < TimeSpan.FromSeconds(1)) {
                        Util.Rect windowCurrentRect = new Util.Rect();
                        Util.GetWindowRect(UB.Core.Decal.Hwnd, ref windowCurrentRect);
                        if (windowCurrentRect.Left != windowStartRect.Left || windowCurrentRect.Top != windowStartRect.Top) {
                            Util.SetWindowPos(UB.Core.Decal.Hwnd, 0, windowStartRect.Left, windowStartRect.Top, 0, 0, 0x0005);
                            didMove = true;
                        }
                    }
                    if (didMove || elapsed > TimeSpan.FromSeconds(1)) {
                        UB.Core.WindowMessage -= core_WindowMessage;
                    }
                }
                catch (Exception ex) { Logger.LogException(ex); }
            }

            Util.GetWindowRect(UB.Core.Decal.Hwnd, ref windowStartRect);
            UB.Core.WindowMessage += core_WindowMessage;
            lastResolutionChange = DateTime.UtcNow;

            WriteToChat($"Setting Resolution {width}x{height}");
            UBHelper.Core.SetResolution(width, height);
        }
        #endregion
        #region /ub textures <landscape> <landscapeDetail> <environment> <environmentDetail> <sceneryDraw> <landscapeDraw>
        [Summary("Sets Client texture options. This will take effect immediately, but will not change the settings page, or persist through relogging.")]
        [Usage("/ub textures <landscape[0-4]> <landscapeDetail[0-1]> <environment[0-4]> <environmentDetail[0-1]> <sceneryDraw[1-25]> <landscapeDraw[1-25]>")]
        [Example("/ub textures 0 1 0 1 25 25", "Sets max settings")]
        [Example("/ub textures 4 0 4 0 1 1", "Sets min settings")]
        [CommandPattern("textures", @"^(?<landscape>[0-4]) (?<landscapeDetail>[01]) (?<environment>[0-4]) (?<environmentDetail>[01]) (?<sceneryDraw>\d+) (?<landscapeDraw>\d+)$")]
        public void DoTextures(string _, Match args) {
            uint.TryParse(args.Groups["landscape"].Value, out uint landscape);
            byte.TryParse(args.Groups["landscapeDetail"].Value, out byte landscapeDetail);
            uint.TryParse(args.Groups["environment"].Value, out uint environment);
            byte.TryParse(args.Groups["environmentDetail"].Value, out byte environmentDetail);
            uint.TryParse(args.Groups["sceneryDraw"].Value, out uint sceneryDraw);
            uint.TryParse(args.Groups["landscapeDraw"].Value, out uint landscapeDraw);

            if (sceneryDraw > 25 || landscapeDraw > 25) {
                LogError($"Requested Texture options were not valid");
                return;
            }
            WriteToChat($"Setting Textures...");
            UBHelper.Core.SetTextures(landscape, landscapeDetail, environment, environmentDetail, sceneryDraw, landscapeDraw);
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
        #region /ub videopatch {enable,disable,toggle}
        [Summary("Disables rendering of the 3d world to conserve CPU")]
        [Usage("/ub videopatch {enable | disable | toggle}")]
        [Example("/ub videopatch enable", "Enables the video patch")]
        [Example("/ub videopatch disable", "Disables the video patch")]
        [Example("/ub videopatch toggle", "Toggles the video patch")]
        [CommandPattern("videopatch", @"^ *(?<params>(enable|disable|toggle)) *$")]
        public void DoVideoPatch(string _, Match args) {
            UB_video(args.Groups["params"].Value);
        }
        public void UB_video(string parameters) {
            if (UBHelper.Core.version < 1911140303) {
                Util.WriteToChat($"Error UBHelper.dll is out of date!");
                return;
            }
            char[] stringSplit = { ' ' };
            string[] parameter = parameters.Split(stringSplit, 2);
            switch (parameter[0]) {
                case "enable":
                    VideoPatch = true;
                    break;

                case "disable":
                    VideoPatch = false;
                    break;

                case "toggle":
                    VideoPatch = !VideoPatch;
                    break;
                default:
                    Util.WriteToChat("Usage: /ub videopatch {enable,disable,toggle}");
                    break;
            }
        }
        public void VideoPatchToggle(bool enabled) {
            if (enabled) {
                if (VideoPatchFocus) VideoPatchFocusToggle(true);
                else UBHelper.VideoPatch.Enable();
            }
            else {
                if (VideoPatchFocus) VideoPatchFocusToggle(false);
                else UBHelper.VideoPatch.Disable();
            }
        }

        private bool VideoPatchFocusEventRegistered = false;
        public void VideoPatchFocusToggle(bool enabled) {
            if (enabled) {
                if (!VideoPatchFocusEventRegistered) {
                    if (VideoPatch) {
                        VideoPatchFocusEventRegistered = true;
                        UB.Core.WindowMessage += Core_WindowMessage_VideoPatchFocusToggle;
                        if (Util.IsClientActive()) UBHelper.VideoPatch.Disable();
                        else UBHelper.VideoPatch.Enable();
                    }
                }
            }
            else {
                if (VideoPatchFocusEventRegistered) {
                    VideoPatchFocusEventRegistered = false;
                    UB.Core.WindowMessage -= Core_WindowMessage_VideoPatchFocusToggle;
                    if (VideoPatch) UBHelper.VideoPatch.Enable();
                }
            }
        }
        private void Core_WindowMessage_VideoPatchFocusToggle(object sender, WindowMessageEventArgs e) {
            try {
                switch (e.Msg) {
                    case 0x0007: // WM_SETFOCUS
                    case 0x0021: // WM_MOUSEACTIVATE
                    case 0x0086: // WM_NCACTIVATE
                        UBHelper.VideoPatch.Disable();
                        break;
                    case 0x0008: // WM_KILLFOCUS
                        UBHelper.VideoPatch.Enable();
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        #endregion
        #region /ub fixbusy
        public void UB_fixbusy() {
            UBHelper.Core.ClearBusyCount();
            UBHelper.Core.ClearBusyState();
            Util.WriteToChat($"Busy State and Busy Count have been reset");
        }
        #endregion
        #endregion

        public Plugin(UtilityBeltPlugin ub, string name) : base(ub, name) {
            try {
                // TODO: do we need to always be listening for these?
                UB.Core.RenderFrame += Core_RenderFrame_PortalOpen;
                UB.Core.RenderFrame += Core_RenderFrame_Delay;
                UB.Core.EchoFilter.ServerDispatch += EchoFilter_ServerDispatch_PortalOpen;
                UB.Core.CharacterFilter.Logoff += CharacterFilter_Logoff_Follow;
                if (UB.Core.CharacterFilter.LoginStatus != 0) UB_Follow_Clear();
                else UB.Core.CharacterFilter.LoginComplete += CharacterFilter_LoginComplete_Follow;

                mediaPlayer = new Mp3Player(UB.Core);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }



        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    UB.Core.RenderFrame -= Core_RenderFrame_PortalOpen;
                    UB.Core.RenderFrame -= Core_RenderFrame_Delay;
                    UB.Core.EchoFilter.ServerDispatch -= EchoFilter_ServerDispatch_PortalOpen;
                    UB.Core.CharacterFilter.Logoff -= CharacterFilter_Logoff_Follow;
                    UB.Core.CharacterFilter.LoginComplete -= CharacterFilter_LoginComplete_Follow;
                    UB.Core.WindowMessage -= Core_WindowMessage_VideoPatchFocusToggle;
                }
                disposedValue = true;
            }
        }
    }
}