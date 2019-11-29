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
using System.Windows.Media;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Constants;
using UtilityBelt.Lib.Settings;

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
    public class Plugin : ToolBase {
        private static readonly MediaPlayer mediaPlayer = new MediaPlayer();
        private DateTime vendorTimestamp = DateTime.MinValue;
        private int vendorOpening = 0;
        private static WorldObject vendor = null;

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

                if (UBHelper.Core.version < 1911140303) {
                    Util.WriteToChat($"Error UBHelper.dll is out of date!");
                    return;
                }

                if (value) UBHelper.VideoPatch.Enable();
                else UBHelper.VideoPatch.Disable();
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
        public void ShowVersion(string command, Match args) {
            Util.WriteToChat("UtilityBelt Version v" + Util.GetVersion(true) + "\n Type `/ub help` or `/ub help <command>` for help.");
        }
        #endregion
        #region /ub delay
        [Summary("Thinks to yourself with your current vitae percentage")]
        [Usage("/ub delay <millisecondDelay> <command>")]
        [Example("/ub delay 5000 /say hello", "Runs \"/say hello\" after a 3000ms delay (3 seconds)")]
        [CommandPattern("delay", @"^ *(?<params>\d+ .+) *$")]
        public void DoDelay(string command, Match args) {
            UB_delay(command + " " + args.Groups["params"].Value);
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
        #region /ub closestportal
        [Summary("Uses the closest portal.")]
        [Usage("/ub closestportal")]
        [Example("/ub closestportal", "Uses the closest portal")]
        [CommandPattern("closestportal", @"^$")]
        public void DoClosestPortal(string command, Match args) {
            UB_portal("", true);
        }
        #endregion
        #region /ub fixbusy
        [Summary("Fixes busystate bugs on the client side.")]
        [Usage("/ub fixbusy")]
        [CommandPattern("fixbusy", @"^$")]
        public void DoFixBusy(string command, Match args) {
            UB_fixbusy();
        }
        #endregion
        #region /ub follow
        [Summary("Follow player commands")]
        [Usage("/ub follow[p] <name>")]
        [Example("/ub follow Zero Cool", "Sets a VTank nav route to follow \"Zero Cool\"")]
        [Example("/ub followp Zero", "Sets a VTank nav route to follow a character with a name partially matching \"Zero\"")]
        [CommandPattern("follow", @"^ *(?<params>.+) *$", true)]
        public void DoFollow(string command, Match args) {
            var flags = command.Replace("follow", "");
            UB_follow(args.Groups["params"].Value, flags.Contains("p"));
        }
        #endregion
        #region /ub opt
        [Summary("Manage plugin settings from the command line")]
        [Usage("/ub opt {list | get <option> | set <option> <newValue>}")]
        [Example("/ub opt list", "Lists all available options.")]
        [Example("/ub get Plugin.Debug", "Gets the current value for the \"Plugin.Debug\" setting")]
        [Example("/ub set Plugin.Debug true", "Sets the \"Plugin.Debug\" setting to True")]
        [CommandPattern("opt", @"^ *(?<params>(list|get \S+|set \S+ \S+)) *$")]
        public void DoOpt(string command, Match args) {
            UB_opt(args.Groups["params"].Value);
        }
        #endregion
        #region /ub pos
        [Summary("Prints position information for the currently selected object")]
        [Usage("/ub pos")]
        [CommandPattern("pos", @"^$")]
        public void DoPos(string command, Match args) {
            UB_pos();
        }
        #endregion
        #region /ub portal
        [Summary("Portal commands, with build in VTank pausing.")]
        [Usage("/ub portal[p] <portalName>")]
        [Example("/ub portal Gateway", "Uses portal with exact name \"Gateway\"")]
        [Example("/ub portalp Portal", "Uses portal with name partially matching \"Portal\"")]
        [CommandPattern("portal", @"^ *(?<params>.+) *$", true)]
        public void DoPortal(string command, Match args) {
            var flags = command.Replace("portal", "");
            UB_portal(args.Groups["params"].Value, flags.Contains("p"));
        }
        #endregion
        #region /ub printcolors
        [Summary("Prints out all available chat colors")]
        [Usage("/ub printcolors")]
        [Example("/ub printcolors", "Prints out all available chat colors")]
        [CommandPattern("printcolors", @"^$")]
        public void PrintChatColors(string command, Match args) {
            foreach (var type in Enum.GetValues(typeof(ChatMessageType)).Cast<ChatMessageType>()) {
                WriteToChat($"{type} ({(int)type})", (int)type);
            }
        }
        #endregion
        #region /ub propertydump
        [Summary("Prints information for the currently selected object")]
        [Usage("/ub propertydump")]
        [CommandPattern("propertydump", @"^$")]
        public void DoPropertyDump(string command, Match args) {
            UB_propertydump();
        }
        #endregion
        #region /ub playeroption
        [Summary("Disables rendering of the 3d world to conserve CPU")]
        [Usage("/ub playeroption <option> {on | true | off | false}")]
        [Example("/ub playeroption AutoRepeatAttack on", "Enables the AutoRepeatAttack player option.")]
        [CommandPattern("playeroption", @"^ *(?<params>\s+ (on|off|true|false)) *$")]
        public void DoPlayerOption(string command, Match args) {
            UB_playeroption(args.Groups["params"].Value);
        }
        #endregion
        #region /ub playsound
        [Summary("Play a sound from the client")]
        [Usage("/ub pcap [volume] <filepath>")]
        [Example("/ub playsound 100 C:\test.wav", "Plays absolute path to music file at 100% volume")]
        [Example("/ub playsound 50 test.wav", "Plays test.wav from the UB plugin storage directory at 50% volume")]
        [CommandPattern("playsound", @"^ *(?<params>(\d+ )?.+) *$")]
        public void DoPlaySound(string command, Match args) {
            UB_playsound(args.Groups["params"].Value);
        }
        #endregion
        #region /ub pcap
        [Summary("Manage packet captures")]
        [Usage("pcap {enable [bufferDepth] | disable | print}")]
        [Example("/ub pcap enable", "Enable pcap functionality (nothing will be saved until you call /ub pcap print)")]
        [Example("/ub pcap print", "Saves the current pcap buffer to a new file in your plugin storage directory.")]
        [CommandPattern("pcap", @"^ *(?<params>(enable( \d+)?|disable|print)) *$")]
        public void DoPcap(string command, Match args) {
            UB_pcap(args.Groups["params"].Value);
        }
        #endregion
        #region /ub vendor
        [Summary("Vendor commands, with build in VTank pausing.")]
        [Usage("/ub vendor {open[p] <vendorname,vendorid,vendorhex> | buyall | sellall | clearbuy | clearsell | opencancel}")]
        [Example("/ub vendor open Tunlok Weapons Master", "Opens vendor with name \"Tunlok Weapons Master\"")]
        [Example("/ub vendor opencancel", "Quietly cancels the last /ub vendor open* command")]
        [CommandPattern("vendor", @"^ *(?<params>(openp? .+|buy(all)?|sell(all)?|clearbuy|clearsell|opencancel)) *$")]
        public void DoVendor(string command, Match args) {
            UB_vendor(args.Groups["params"].Value);
        }
        #endregion
        #region /ub vitae
        [Summary("Thinks to yourself with your current vitae percentage")]
        [Usage("/ub vitae")]
        [CommandPattern("vitae", @"^$")]
        public void DoVitae(string command, Match args) {
            UB_vitae();
        }
        #endregion
        #region /ub videopatch
        [Summary("Disables rendering of the 3d world to conserve CPU")]
        [Usage("/ub videopatch {enable | disable | toggle}")]
        [Example("/ub videopatch enable", "Enables the video patch")]
        [Example("/ub videopatch disable", "Disables the video patch")]
        [Example("/ub videopatch toggle", "Toggles the video patch")]
        [CommandPattern("videopatch", @"^ *(?<params>(enable|disable|toggle)) *$")]
        public void DoVideoPatch(string command, Match args) {
            UB_delay(command + " " + args.Groups["params"].Value);
        }
        #endregion
        #region /ub autostack
        [Summary("Auto Stack your inventory")]
        [Usage("/ub autostack")]
        [CommandPattern("autostack", @"^$")]
        public void DoAutoStack(string command, Match args) {
            if (UBHelper.InventoryManager.AutoStack()) {
                UBHelper.ActionQueue.InventoryEvent += ActionQueue_InventoryEvent_AutoStack;
                Util.WriteToChat("AutoStack running");
            }
            else {
                Util.WriteToChat("AutoStack - nothing to do");
            }
        }

        private void ActionQueue_InventoryEvent_AutoStack(object sender, EventArgs e) {
            Util.WriteToChat("AutoStack complete.");
            UBHelper.ActionQueue.InventoryEvent -= ActionQueue_InventoryEvent_AutoStack;
        }
        #endregion
        #region /ub autocram
        [Summary("Auto Cram into side packs")]
        [Usage("/ub autocram")]
        [CommandPattern("autocram", @"^$")]
        public void DoAutoCram(string command, Match args) {
            if (UBHelper.InventoryManager.AutoCram()) {
                UBHelper.ActionQueue.InventoryEvent += ActionQueue_InventoryEvent_AutoCram;
                Util.WriteToChat("AutoCram running");
            }
            else {
                Util.WriteToChat("AutoCram - nothing to do");
            }
        }

        private void ActionQueue_InventoryEvent_AutoCram(object sender, EventArgs e) {
            Util.WriteToChat("AutoCram complete.");
            UBHelper.ActionQueue.InventoryEvent -= ActionQueue_InventoryEvent_AutoCram;
        }
        #endregion
        #region /ub testitem
        [Summary("Dump some info about the selected item")]
        [Usage("/ub testitem")]
        [CommandPattern("testitem", @"^$")]
        public void TestItem(string command, Match args) {
            UBHelper.InventoryManager.TestItem(UB.Core.Actions.CurrentSelection);
        }
        #endregion
        #region /ub unstack
        [Summary("Insta-UnStack dev test. Remove for production!")]
        [Usage("/ub unstack")]
        [CommandPattern("unstack", @"^$")]
        public void DoUnStack(string command, Match args) {
            UBHelper.ActionQueue.InventoryEvent += ActionQueue_InventoryEvent_Unstack;
            UBHelper.InventoryManager.UnStack();
        }

        private void ActionQueue_InventoryEvent_Unstack(object sender, EventArgs e) {
            Util.WriteToChat("UnStack complete");
            UBHelper.ActionQueue.InventoryEvent -= ActionQueue_InventoryEvent_Unstack;
        }
        #endregion
        #endregion

        public Plugin(UtilityBeltPlugin ub, string name) : base(ub, name) {
            try {
                // TODO: do we need to always be listening for these?
                UB.Core.RenderFrame += Core_RenderFrame;
                UB.Core.WorldFilter.ApproachVendor += WorldFilter_ApproachVendor;
                UB.Core.EchoFilter.ServerDispatch += EchoFilter_ServerDispatch;
                UB.Core.CharacterFilter.Logoff += CharacterFilter_Logoff;
                if (VTankControl.vTankInstance != null && VTankControl.vTankInstance.GetNavProfile().Equals("UBFollow"))
                    VTankControl.vTankInstance.LoadNavProfile(null);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private static readonly Regex PlaySoundParamRegex = new Regex(@"^(?<volume>\d*)?\s*(?<path>.*)$");
        private void UB_playsound(string @params) {
            string absPath = null;
            double volume = 0.5;
            string path = null;
            var m = PlaySoundParamRegex.Match(@params);
            if (m != null && m.Success) {
                path = m.Groups["path"].Value;
                if (!string.IsNullOrEmpty(m.Groups["volume"].Value) && int.TryParse(m.Groups["volume"].Value, out var volumeInt)) {
                    volume = Math.Max(0, Math.Min(100, volumeInt)) * 0.01;
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
                mediaPlayer.Open(new Uri(absPath));
                mediaPlayer.Volume = volume;
                mediaPlayer.Play();
            }
        }

        #region /ub fixbusy
        public void UB_fixbusy() {
            UBHelper.Core.ClearBusyCount();
            UBHelper.Core.ClearBusyState();
            Util.WriteToChat($"Busy State and Busy Count have been reset");
        }
        #endregion
        #region /ub playeroption <option> <on/true|off/false>
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
        #region /ub videopatch {enable,disable,toggle}
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
        #endregion
        #region /ub pcap {enable [bufferDepth],disable,print}
        public void UB_pcap(string parameters) {
            if (UBHelper.Core.version < 1911220544) {
                Util.WriteToChat($"Error UBHelper.dll is out of date!");
                return;
            }
            char[] stringSplit = { ' ' };
            string[] parameter = parameters.Split(stringSplit, 2);
            switch (parameter[0]) {
                case "enable":
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
        #region /ub vendor {open[p] [vendorname,vendorid,vendorhex],opencancel,buyall,sellall,clearbuy,clearsell}
        public void UB_vendor(string parameters) {
            char[] stringSplit = { ' ' };
            string[] parameter = parameters.Split(stringSplit, 2);
            if (parameter.Length == 0) {
                Util.WriteToChat("Usage: /ub vendor {open[p] [vendorname,vendorid,vendorhex],opencancel,buyall,sellall,clearbuy,clearsell}");
                return;
            }

            switch (parameter[0]) {
                case "buy":
                case "buyall":
                    CoreManager.Current.Actions.VendorBuyAll();
                    break;

                case "sell":
                case "sellall":
                    CoreManager.Current.Actions.VendorSellAll();
                    break;

                case "clearbuy":
                    CoreManager.Current.Actions.VendorClearBuyList();
                    break;

                case "clearsell":
                    CoreManager.Current.Actions.VendorClearSellList();
                    break;

                case "open":
                    if (parameter.Length != 2)
                        UB_vendor_open("", true);
                    else
                        UB_vendor_open(parameter[1], false);
                    break;

                case "openp":
                    if (parameter.Length != 2)
                        UB_vendor_open("", true);
                    else
                        UB_vendor_open(parameter[1], true);
                    break;
                case "opencancel":
                    UB.Core.Actions.FaceHeading(UB.Core.Actions.Heading - 1, true);
                    vendor = null;
                    vendorOpening = 0;
                    VTankControl.Nav_UnBlock();
                    break;
            }
        }
        private void UB_vendor_open(string vendorname, bool partial) {
            vendor = FindName(vendorname, partial, new ObjectClass[] { ObjectClass.Vendor });
            if (vendor != null) {
                OpenVendor();
                return;
            }
            Util.ThinkOrWrite("AutoVendor failed to open vendor", UB.AutoVendor.Think);
        }
        #endregion
        #region /ub closestportal || /ub portal <name> || /ub portalp <name>
        public void UB_portal(string portalName, bool partial) {
            portal = FindName(portalName, partial, new ObjectClass[] { ObjectClass.Portal, ObjectClass.Npc });
            if (portal != null) {
                UsePortal();
                return;
            }

            Util.ThinkOrWrite("Could not find a portal", PortalThink);
        }
        #endregion
        #region /ub follow [name]
        public void UB_follow(string characterName, bool partial) {
            WorldObject followChar = FindName(characterName, partial, new ObjectClass[] { ObjectClass.Player });
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
            if (VTankControl.vTankInstance == null) {
                LogError("Could not connect to VTank");
                return;
            }
            try {
                Util.WriteToChat($"Following {UB.Core.WorldFilter[id].Name}[0x{id:X8}]");
                VTankControl.vTankInstance.LoadNavProfile("UBFollow");
                VTankControl.vTankInstance.NavSetFollowTarget(id, "");
                if (!(bool)VTankControl.vTankInstance.GetSetting("EnableNav"))
                    VTankControl.vTankInstance.SetSetting("EnableNav", true);
            }
            catch { }

        }
        #endregion


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

            var phys = PhysicsObject.FromId(selected);

            Util.WriteToChat($"Offset: {wo.Offset()}");
            Util.WriteToChat($"Coords: {wo.Coordinates()}");
            Util.WriteToChat($"RawCoords: {wo.RawCoordinates()}"); //same as offset?
            Util.WriteToChat($"Phys lb: {phys.Landblock.ToString("X8")}");
            Util.WriteToChat($"Phys pos: x:{phys.Position.X} y:{phys.Position.Y} z:{phys.Position.Z}");
            Util.WriteToChat($"Phys heading: x:{phys.Heading.X} y:{phys.Position.Y} z:{phys.Position.Z}");
        }

        private void UB_vitae() {
            Util.Think($"My vitae is {UB.Core.CharacterFilter.Vitae}%");
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

        readonly private Regex optionRe = new Regex(@"^((get|set) )?(?<option>[^\s]+)\s?(?<value>.*)", RegexOptions.IgnoreCase);
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

        private void OpenVendor() {
            VTankControl.Nav_Block(500 + UB.AutoVendor.TriesTime, false);
            vendorOpening = 1;

            vendorTimestamp = DateTime.UtcNow - TimeSpan.FromMilliseconds(UB.AutoVendor.TriesTime - 250); // fudge timestamp so next think hits in 500ms
            UB.Core.Actions.SetAutorun(false);
            LogDebug("Attempting to open vendor " + vendor.Name);

        }
        private void UsePortal() {
            VTankControl.Nav_Block(500 + PortalTimeout, false);
            portalAttempts = 1;

            portalTimestamp = DateTime.UtcNow - TimeSpan.FromMilliseconds(PortalTimeout - 250); // fudge timestamp so next think hits in 500ms
            UB.Core.Actions.SetAutorun(false);
            LogDebug("Attempting to use portal " + portal.Name);
        }

        //Do not use this in a loop, it gets an F for eFFiciency.
        public WorldObject FindName(string searchname, bool partial, ObjectClass[] oc) {

            //try int id
            if (int.TryParse(searchname, out int id)) {
                if (UB.Core.WorldFilter[id] != null && CheckObjectClassArray(UB.Core.WorldFilter[id].ObjectClass, oc)) {
                    // Util.WriteToChat("Found by id");
                    return UB.Core.WorldFilter[id];
                }
            }
            //try hex...
            try {
                int intValue = Convert.ToInt32(searchname, 16);
                if (UB.Core.WorldFilter[intValue] != null && CheckObjectClassArray(UB.Core.WorldFilter[intValue].ObjectClass, oc)) {
                    // Util.WriteToChat("Found vendor by hex");
                    return UB.Core.WorldFilter[intValue];
                }
            }
            catch { }

            searchname = searchname.ToLower();

            //try "selected"
            if (searchname.Equals("selected") && UB.Core.Actions.CurrentSelection != 0 && UB.Core.WorldFilter[UB.Core.Actions.CurrentSelection] != null && CheckObjectClassArray(UB.Core.WorldFilter[UB.Core.Actions.CurrentSelection].ObjectClass, oc)) {
                return UB.Core.WorldFilter[UB.Core.Actions.CurrentSelection];
            }
            //try slow search...
            WorldObject found = null;

            double lastDistance = double.MaxValue;
            double thisDistance;
            foreach (WorldObject thisOne in CoreManager.Current.WorldFilter.GetLandscape()) {
                if (!CheckObjectClassArray(thisOne.ObjectClass, oc)) continue;
                thisDistance = UB.Core.WorldFilter.Distance(CoreManager.Current.CharacterFilter.Id, thisOne.Id);
                if (thisOne.Id != UB.Core.CharacterFilter.Id && (found == null || lastDistance > thisDistance)) {
                    string thisLowerName = thisOne.Name.ToLower();
                    if (partial && thisLowerName.Contains(searchname) && CheckObjectClassArray(thisOne.ObjectClass, oc)) {
                        found = thisOne;
                        lastDistance = thisDistance;
                    }
                    else if (thisLowerName.Equals(searchname) && CheckObjectClassArray(thisOne.ObjectClass, oc)) {
                        found = thisOne;
                        lastDistance = thisDistance;
                    }
                }
            }
            return found;
        }
        private bool CheckObjectClassArray(ObjectClass needle, ObjectClass[] haystack) {
            if (haystack.Length == 0) return true;
            foreach (ObjectClass o in haystack)
                if (needle == o) return true;
            return false;
        }

        public void Core_RenderFrame(object sender, EventArgs e) {
            try {
                if (vendorOpening > 0 && DateTime.UtcNow - vendorTimestamp > TimeSpan.FromMilliseconds(UB.AutoVendor.TriesTime)) {

                    if (vendorOpening <= UB.AutoVendor.Tries) {
                        if (vendorOpening > 1)
                            Logger.Debug("Vendor Open Timed out, trying again");

                        VTankControl.Nav_Block(500 + UB.AutoVendor.TriesTime, false);
                        vendorOpening++;
                        vendorTimestamp = DateTime.UtcNow;
                        CoreManager.Current.Actions.UseItem(vendor.Id, 0);
                    }
                    else {
                        UB.Core.Actions.FaceHeading(UB.Core.Actions.Heading - 1, true); // Cancel the previous useitem call (don't ask)
                        Util.ThinkOrWrite("AutoVendor failed to open vendor", UB.AutoVendor.Think);
                        vendor = null;
                        vendorOpening = 0;
                        VTankControl.Nav_UnBlock();
                    }
                }
                if (portalAttempts > 0 && DateTime.UtcNow - portalTimestamp > TimeSpan.FromMilliseconds(PortalTimeout)) {

                    if (portalAttempts <= PortalAttempts) {
                        if (portalAttempts > 1)
                            LogDebug("Use Portal Timed out, trying again");

                        VTankControl.Nav_Block(500 + PortalTimeout, false);
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
                        VTankControl.Nav_UnBlock();
                    }
                }

                while (delayedCommands.Count > 0 && delayedCommands[0].RunAt <= DateTime.UtcNow) {
                    LogDebug($"Executing command `{delayedCommands[0].Command}` (delay was {delayedCommands[0].Delay}ms)");
                    Util.DispatchChatToBoxWithPluginIntercept(delayedCommands[0].Command);
                    delayedCommands.RemoveAt(0);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        private void WorldFilter_ApproachVendor(object sender, ApproachVendorEventArgs e) {
            if (vendorOpening > 0 && e.Vendor.MerchantId == vendor.Id) {
                LogDebug("vendor " + vendor.Name + " opened successfully");
                vendor = null;
                vendorOpening = 0;
                // VTankControl.Nav_UnBlock(); Let it bleed over into AutoVendor; odds are there's a reason this vendor was opened, and letting vtank run off prolly isn't it.
            }
        }

        private void EchoFilter_ServerDispatch(object sender, NetworkMessageEventArgs e) {
            try {
                if (portalAttempts > 0 && e.Message.Type == 0xF74B && (int)e.Message["object"] == CoreManager.Current.CharacterFilter.Id && (short)e.Message["portalType"] == 17424) { //17424 is the magic sauce for entering a portal. 1032 is the magic sauce for exiting a portal.
                    LogDebug("portal used successfully");
                    portal = null;
                    portalAttempts = 0;
                    VTankControl.Nav_UnBlock();
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        private void CharacterFilter_Logoff(object sender, LogoffEventArgs e) {
            if (e.Type == LogoffEventType.Requested && VTankControl.vTankInstance != null && VTankControl.vTankInstance.GetNavProfile().Equals("UBFollow"))
                VTankControl.vTankInstance.LoadNavProfile(null);
        }

        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    UB.Core.RenderFrame -= Core_RenderFrame;
                    UB.Core.WorldFilter.ApproachVendor -= WorldFilter_ApproachVendor;
                    UB.Core.EchoFilter.ServerDispatch -= EchoFilter_ServerDispatch;
                    UB.Core.CharacterFilter.Logoff -= CharacterFilter_Logoff;
                }
                disposedValue = true;
            }
        }
    }
}