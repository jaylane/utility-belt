using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Decal.Adapter;
using Decal.Adapter.Wrappers;
using MyClasses.MetaViewWrappers;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Settings;
using UtilityBelt.Tools;
using UtilityBelt.Views;

namespace UtilityBelt {

    #region Command wrapper
    public class Command {
        public string Verb { get; }
        public Regex ArgumentRegex { get; }
        public PropertyInfo PropInfo { get; }
        public MethodInfo Method { get; }
        public bool AllowPartialVerbMatch { get; }
        public string Usage { get; }
        public Dictionary<string, string> Examples = new Dictionary<string, string>();
        public string Summary { get; }

        public Command(PropertyInfo propInfo, MethodInfo method) {
            PropInfo = propInfo;
            Method = method;

            var commandPatternAttrs = method.GetCustomAttributes(typeof(CommandPatternAttribute), true);
            foreach (var attr in commandPatternAttrs) {
                Verb = ((CommandPatternAttribute)attr).Verb;
                ArgumentRegex = new Regex(((CommandPatternAttribute)attr).ArgumentsPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                AllowPartialVerbMatch = ((CommandPatternAttribute)attr).AllowPartialVerbMatch;
            }

            var usageAttrs = method.GetCustomAttributes(typeof(UsageAttribute), true);
            foreach (var attr in usageAttrs) {
                Usage = ((UsageAttribute)attr).Usage;
            }

            var exampleAttrs = method.GetCustomAttributes(typeof(ExampleAttribute), true);
            foreach (var attr in exampleAttrs) {
                Examples.Add(((ExampleAttribute)attr).Command, ((ExampleAttribute)attr).Description);
            }

            var summaryAttrs = method.GetCustomAttributes(typeof(SummaryAttribute), true);
            foreach (var attr in summaryAttrs) {
                Summary = ((SummaryAttribute)attr).Summary;
            }
        }
    }
    #endregion

    public class UtilityBeltPlugin {
        internal static UtilityBeltPlugin Instance;
        private bool didInit = false;
        internal delegate void CommandHandlerDelegate(string verb, Match args);
        internal readonly Dictionary<string, Command> RegisteredCommands = new Dictionary<string, Command>();

        internal CoreManager Core;
        internal NetServiceHost Host;
        internal string PluginName = "UtilityBelt";
        internal string AccountName;
        internal string ServerName;
        internal string CharacterName;
        internal MainView MainView;
        internal MapView MapView;
        internal Settings Settings;

        internal List<ToolBase> LoadedTools = new List<ToolBase>();

        #region Tools
        public Plugin Plugin { get; private set; }
        public Assessor Assessor { get; private set; }
        public AutoImbue AutoImbue { get; private set; }
        public AutoSalvage AutoSalvage { get; private set; }
        public AutoTinker AutoTinker { get; private set; }
        public AutoTrade AutoTrade { get; private set; }
        public AutoVendor AutoVendor { get; private set; }
        public ChatLogger ChatLogger { get; private set; }
        public Counter Counter { get; private set; }
        public DoorWatcher DoorWatcher { get; private set; }
        public DungeonMaps DungeonMaps { get; private set; }
        public EquipmentManager EquipmentManager { get; private set; }
        public InventoryManager InventoryManager { get; private set; }
        public Jumper Jumper { get; private set; }
        public Nametags Nametags { get; private set; }
        public QuestTracker QuestTracker { get; private set; }
        public VisualNav VisualNav { get; private set; }
        public VTankControl VTank { get; private set; }
        public VTankFellowHeals VTankFellowHeals { get; private set; }
        #endregion

        public UtilityBeltPlugin() {
            Instance = this;
            System.Resources.ResourceManager rm = new System.Resources.ResourceManager(GetType().Namespace + ".Properties.Resources", System.Reflection.Assembly.GetExecutingAssembly());

            // if you add a new embedded assembly, you should load it here.
            System.Reflection.Assembly.Load((byte[])rm.GetObject("UBHelper"));
            System.Reflection.Assembly.Load((byte[])rm.GetObject("Newtonsoft_Json"));
            System.Reflection.Assembly.Load((byte[])rm.GetObject("SharedMemory"));
        }


        /// <summary>
        /// Called automatically when the plugin is first initialized.  This will only ever be called once for this plugin instance.
        /// </summary>
        /// <param name="assemblyLocation">The full path including filename to this assembly dll</param>
        /// <param name="storagePath">The directory where this plugin should store files</param>
        /// <param name="host">NetServiceHost instance passed from UBLoader filter</param>
        /// <param name="core">CoreManager instance passed from UBLoader filter</param>
        /// <param name="accountName">Account name for the current session</param>
        /// <param name="characterName">Name of the logged in character</param>
        /// <param name="serverName">Name of the server currently logged in to</param>
        public void Startup(string assemblyLocation, string storagePath, NetServiceHost host, CoreManager core, string accountName, string characterName, string serverName) {
			try {
                Core = core;
                Host = host;
                AccountName = accountName;
                ServerName = serverName;
                CharacterName = characterName;

                Util.Init(this, assemblyLocation, storagePath); //static classes can not have constructors, but still need to init variables.
                Logger.Debug("UtilityBelt.Startup");

                UBHelper.Core.Startup();

                Core.CommandLineText += Core_CommandLineText;

                // if we are logged in already, we need to init manually.
                // this happens during hot reloads while logged in.
                if (Core.CharacterFilter.LoginStatus != 0) {
                    Init();
                }
                else {
                    Core.CharacterFilter.Login += CharacterFilter_Login;
                }
            }
			catch (Exception ex) { Logger.LogException(ex); }
        }

        private void CharacterFilter_Login(object sender, EventArgs e) {
			try {
                Core.CharacterFilter.Login -= CharacterFilter_Login;
                Init();
            }
			catch (Exception ex) { Logger.LogException(ex); }
        }

        /// <summary>
        /// Returns a list of PropertyInfo's for all the loaded tools.
        /// </summary>
        /// <returns>A list of PropertyInfo's for all the loaded tools.</returns>
        internal IEnumerable<PropertyInfo> GetToolProps() {
            return GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(
                p => p.PropertyType.IsSubclassOf(typeof(ToolBase))
            );
        }


        /// <summary>
        /// Called on CharacterFilter_Login, this is used to initialize our plugin globals / tools / ui.
        /// </summary>
        public void Init() {
            Logger.Debug("UtilityBelt.Init");
            // CharacterFilter_Login will be called multiple times if the character was already in the world,
            // so make sure we only actually init once
            if (didInit) return;
            didInit = true;

            Util.CreateDataDirectories();
            Logger.Init();
            Settings = new Settings();

            MainView = new MainView(this);
            MapView = new MapView(this);

            LoadTools();
            Settings.Load();
            InitTools();
            InitCommands();

            MainView.Init();
            MapView.Init();

            Logger.Debug($"UB Initialized {DateTime.UtcNow} v{Util.GetVersion(true)}.");

            if (Plugin.CheckForUpdates) {
                UpdateChecker.CheckForUpdate();
            }
        }

        /// <summary>
        /// Called once all tools and settings have been loaded, this calls Init() on each tool.
        /// </summary>
        private void InitTools() {
            foreach (var tool in LoadedTools) {
                try {
                    tool.Init();
                }
                catch (Exception ex) { Logger.LogException(ex); }
            }
        }

        /// <summary>
        ///  Called once on CharacterFiler_Login (or on a hot reload), this will iterate over all properties in the
        ///  class and automatically populate properties that inherit from ToolBase with a newly created instance.
        /// </summary>
        private void LoadTools() {
            var toolProps = GetToolProps();

            foreach (var toolProp in toolProps) {
                try {
                    var nameAttrs = toolProp.PropertyType.GetCustomAttributes(typeof(NameAttribute), true);

                    if (nameAttrs.Length == 1) {
                        var tool = Activator.CreateInstance(toolProp.PropertyType, new object[] {
                            this,
                            ((NameAttribute)nameAttrs[0]).Name
                        });

                        toolProp.SetValue(this, tool, null);
                        LoadedTools.Add((ToolBase)tool);

                        Settings.SetupSection((SectionBase)toolProp.GetValue(this, null)); 
                    }
                    else {
                        throw new Exception($"{toolProp.PropertyType} has {nameAttrs.Length} NameAttributes defined, but requires exactly one.");
                    }
                }
                catch (Exception ex) {
                    Logger.LogException(ex);
                    Logger.Error($"Error loading tool: {toolProp.PropertyType}");
                }
            }
        }

        #region Command Handling
        /// <summary>
        /// Looks through all the loaded tools and initializes all defined commands
        /// </summary>
        private void InitCommands() {
            var toolProps = GetToolProps();

            foreach (var toolProp in toolProps) {
                foreach (var method in toolProp.PropertyType.GetMethods()) {
                    var commandPatternAttrs = method.GetCustomAttributes(typeof(CommandPatternAttribute), true);

                    foreach (var attr in commandPatternAttrs) {
                        var verb = ((CommandPatternAttribute)attr).Verb;
                        var argsPattern = ((CommandPatternAttribute)attr).ArgumentsPattern;

                        try {
                            if (RegisteredCommands.ContainsKey(verb)) {
                                Logger.Error($"Unable to register {verb} from {toolProp.PropertyType} because it was already registered by {RegisteredCommands[verb].PropInfo.DeclaringType}.");
                                continue;
                            }

                            RegisteredCommands.Add(verb, new Command(toolProp, method));
                        }
                        catch (Exception ex) {
                            Logger.LogException(ex);
                            Logger.Error($"Unable to register command: {verb} from {toolProp.PropertyType} with regex: {argsPattern}");
                        }
                    }
                }
            }
        }

        private void Core_CommandLineText(object sender, ChatParserInterceptEventArgs e) {
            try {
                if (e.Text.StartsWith("/ub ") || e.Text.StartsWith("@ub ") || e.Text == "/ub") {
                    e.Eat = true;

                    var parts = e.Text.Split(' ');
                    var verb = parts.Length > 1 ? parts[1] : "";

                    if (RegisteredCommands.ContainsKey(verb)) {
                        RunCommand(RegisteredCommands[verb], verb, parts);
                    }
                    else {
                        // fall back to searching for command that *starts* with this verb (if available for this verb)
                        var partialMatches = new List<string>();
                        foreach (var kp in RegisteredCommands) {
                            if (kp.Value.AllowPartialVerbMatch && verb.StartsWith(kp.Key)) {
                                RunCommand(kp.Value, verb, parts);
                                return;
                            }
                            else if (!string.IsNullOrEmpty(kp.Key) && Util.GetDamerauLevenshteinDistance(verb, kp.Key) <= 2) {
                                partialMatches.Add(kp.Key);
                            }
                        }

                        Logger.Error($"Command not found! Type \"ub help\" for a list of commands.");

                        if (partialMatches.Count > 0) {
                            Util.WriteToChat($"Did you mean one of these? { string.Join(", ", partialMatches.ToArray())}");
                        }
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void RunCommand(Command command, string verb, string[] parts) {
            var args = parts.Length > 2 ? string.Join(" ", parts.Skip(2).ToArray()) : "";
            var argMatch = command.ArgumentRegex.Match(args);

            if (argMatch.Success) {
                var instance = command.PropInfo.GetValue(this, null);
                var del = (CommandHandlerDelegate)Delegate.CreateDelegate(typeof(CommandHandlerDelegate), instance, command.Method);
                del?.Invoke(verb, argMatch);
            }
            else {
                Logger.Error("Bad command syntax");
                PrintHelp(command);
            }
        }

        /// <summary>
        /// Prints the help for a specific command.
        /// </summary>
        /// <param name="command">Command to print help for</param>
        /// <param name="fullDescription">Include full description in the output</param>
        internal void PrintHelp(Command command, bool fullDescription = true) {
            var help = new StringBuilder();

            help.AppendLine($"Usage: {command.Usage}");

            if (fullDescription) {
                help.AppendLine($"Description: {command.Summary}");
                help.AppendLine($"Examples:");

                foreach (var example in command.Examples) {
                    help.AppendLine(" " + example.Key);
                    help.AppendLine("  " + example.Value);
                }
            }

            Util.WriteToChat(help.ToString().Replace("\r", ""));
        }
        #endregion

        /// <summary>
        /// Called once during plugin shutdown
        /// </summary>
        public void Shutdown() {
            try {
                Core.CommandLineText -= Core_CommandLineText;

                foreach (var tool in LoadedTools) {
                    try {
                        tool.Dispose();
                    }
                    catch (Exception ex) { Logger.LogException(ex); }
                }

                MainView.Dispose();
                MapView.Dispose();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
    }
}
