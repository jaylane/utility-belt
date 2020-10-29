using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Decal.Adapter;
using Decal.Adapter.Wrappers;
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

    #region Expression wrapper
    public class ExpressionMethod {
        public string Name { get; }
        public Type[] ArgumentTypes { get; }
        public Type ReturnType { get; }
        public PropertyInfo PropInfo { get; }
        public MethodInfo Method { get; }
        public string Usage { get; }
        public Dictionary<string, string> Examples = new Dictionary<string, string>();
        public string Summary { get; }

        public ExpressionMethod(PropertyInfo propInfo, MethodInfo method) {
            PropInfo = propInfo;
            Method = method;

            var expressionMethodAttrs = method.GetCustomAttributes(typeof(ExpressionMethodAttribute), true);
            foreach (var attr in expressionMethodAttrs) {
                Name = ((ExpressionMethodAttribute)attr).Name;
            }

            var expressionParameterAttrs = method.GetCustomAttributes(typeof(ExpressionParameterAttribute), true);
            var argTypes = new List<Type>();
            foreach (var attr in expressionParameterAttrs) {
                argTypes.Add(((ExpressionParameterAttribute)attr).Type);
            }
            ArgumentTypes = argTypes.ToArray();

            var expressionReturnAttrs = method.GetCustomAttributes(typeof(ExpressionReturnAttribute), true);
            foreach (var attr in expressionReturnAttrs) {
                ReturnType = ((ExpressionReturnAttribute)attr).Type;
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

        internal readonly Dictionary<string, ExpressionMethod> RegisteredExpressions = new Dictionary<string, ExpressionMethod>();

        internal CoreManager Core;
        internal NetServiceHost Host;
        internal string PluginName = "UtilityBelt";
        internal string DatabaseFile;
        internal string AccountName;
        internal string ServerName;
        internal string CharacterName;
        internal MainView MainView;
        internal ItemGiverView ItemGiverView;
        internal MapView MapView;
        internal Settings Settings;
        internal Database Database;

        internal List<ToolBase> LoadedTools = new List<ToolBase>();

        #region Tools
        public Plugin Plugin { get; private set; }
        public Assessor Assessor { get; private set; }
        //public AutoImbue AutoImbue { get; private set; }
        public AutoSalvage AutoSalvage { get; private set; }
        public AutoTinker AutoTinker { get; private set; }
        public AutoTrade AutoTrade { get; private set; }
        public AutoVendor AutoVendor { get; private set; }
        public ChatLogger ChatLogger { get; private set; }
        public ChatNameClickHandler ChatNameClickHandler { get; private set; }
        public Counter Counter { get; private set; }
        public DungeonMaps DungeonMaps { get; private set; }
        public EquipmentManager EquipmentManager { get; private set; }
        public FellowshipManager FellowshipManager { get; private set; }
        public HealthTracker HealthTracker { get; private set; }
        public InventoryManager InventoryManager { get; private set; }
        public Jumper Jumper { get; private set; }
        public LSD LSD { get; private set; }
        public Nametags Nametags { get; private set; }
        public Professors Professors { get; private set; }
        public QuestTracker QuestTracker { get; private set; }
        public VisualNav VisualNav { get; private set; }
        public VTankControl VTank { get; private set; }
        public VTankFellowHeals VTankFellowHeals { get; private set; }
        public PrepClick PrepClick { get; private set; }
        #endregion

        public UtilityBeltPlugin() {
            Instance = this;
        }


        /// <summary>
        /// Called automatically when the plugin is first initialized.  This will only ever be called once for this plugin instance.
        /// </summary>
        /// <param name="assemblyLocation">The full path including filename to this assembly dll</param>
        /// <param name="storagePath">The directory where this plugin should store files</param>
        /// <param name="databaseFile">The absolute file name where the database should be stored</param>
        /// <param name="host">NetServiceHost instance passed from UBLoader filter</param>
        /// <param name="core">CoreManager instance passed from UBLoader filter</param>
        /// <param name="accountName">Account name for the current session</param>
        /// <param name="characterName">Name of the logged in character</param>
        /// <param name="serverName">Name of the server currently logged in to</param>
        public void Startup(string assemblyLocation, string storagePath, string databaseFile, NetServiceHost host, CoreManager core, string accountName, string characterName, string serverName) {
			try {
                Core = core;
                Host = host;
                DatabaseFile = databaseFile;
                AccountName = accountName;
                ServerName = serverName;
                CharacterName = characterName;

                Util.Init(this, assemblyLocation, storagePath); //static classes can not have constructors, but still need to init variables.

                UBHelper.Core.Startup(assemblyLocation, storagePath, characterName);

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
        public IEnumerable<PropertyInfo> GetToolProps() {
            return GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(
                p => p.PropertyType.IsSubclassOf(typeof(ToolBase))
            );
        }


        /// <summary>
        /// Called on CharacterFilter_Login, this is used to initialize our plugin globals / tools / ui.
        /// </summary>
        public void Init() {
            Util.CreateDataDirectories();
            // CharacterFilter_Login will be called multiple times if the character was already in the world,
            // so make sure we only actually init once
            if (didInit) return;
            didInit = true;

            Logger.Init();
            Settings = new Settings();
            Database = new Database(DatabaseFile);

            MainView = new MainView(this);
            ItemGiverView = new ItemGiverView(this);
            MapView = new MapView(this);

            HotkeyWrapperManager.Startup("UB");

            LoadTools();
            Settings.Load();
            InitTools();
            InitCommands();
            InitHotkeys();
            InitExpressions();

            MainView.Init();
            ItemGiverView.Init();
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
                    Logger.Error(ex.ToString());
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
                        RunCommand(RegisteredCommands[verb], verb, parts.Skip(1).ToArray());
                    }
                    else {
                        // fall back to searching for command that *starts* with this verb
                        var partialMatches = new List<string>();
                        var commandText = String.Join(" ", e.Text.Split(' ').Skip(1).ToArray());
                        foreach (var kp in RegisteredCommands) {
                            if (string.IsNullOrEmpty(kp.Key))
                                continue;

                            if (commandText.StartsWith(kp.Key)) {
                                if (commandText == kp.Key) {
                                    verb = commandText;
                                    parts = new string[0];
                                }
                                else {
                                    verb = commandText.Substring(0, commandText.Substring(kp.Key.Length).IndexOf(' ') + kp.Key.Length).ToString();
                                    parts = commandText.Substring(verb.Length + 1).Split(' ');
                                }
                                RunCommand(kp.Value, verb, parts);
                                return;
                            }
                            else if (!string.IsNullOrEmpty(kp.Key) && Util.GetDamerauLevenshteinDistance(verb, kp.Key) <= 2) {
                                partialMatches.Add(kp.Key);
                            }
                        }

                        Logger.Error($"Command not found! Type \"ub help\" for a list of commands.");

                        if (partialMatches.Count > 0) {
                            Logger.Error($"Did you mean one of these? { string.Join(", ", partialMatches.ToArray())}");
                        }
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void RunCommand(Command command, string verb, string[] parts) {
            var args = string.Join(" ", parts.ToArray());
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

            Logger.WriteToChat(help.ToString().Replace("\r", ""));
        }
        #endregion

        #region Expressions
        /// <summary>
        /// Looks through all the loaded tools and initializes all defined expression methods
        /// </summary>
        private void InitExpressions() {
            var toolProps = GetToolProps();

            foreach (var toolProp in toolProps) {
                foreach (var method in toolProp.PropertyType.GetMethods()) {
                    var expressionMethodAttrs = method.GetCustomAttributes(typeof(ExpressionMethodAttribute), true);

                    foreach (var attr in expressionMethodAttrs) {
                        var name = ((ExpressionMethodAttribute)attr).Name;
                        var key = $"{name}";


                        var expressionParameterAttrs = method.GetCustomAttributes(typeof(ExpressionParameterAttribute), true);
                        var argCount = 0;
                        foreach (var expressionParameterAttr in expressionParameterAttrs) {
                            argCount++;
                            //key += $":{((ExpressionParameterAttribute)expressionParameterAttr).Type.ToString().Split('.').Last().ToLower()}";
                        }

                        key += $":{argCount}";

                        try {
                            if (RegisteredExpressions.ContainsKey(key)) {
                                Logger.Error($"Unable to register expression {key} from {toolProp.PropertyType} because it was already registered by {RegisteredExpressions[name].PropInfo.DeclaringType}.");
                                continue;
                            }

                            RegisteredExpressions.Add(key, new ExpressionMethod(toolProp, method));
                        }
                        catch (Exception ex) {
                            Logger.LogException(ex);
                            Logger.Error(ex.ToString());
                            Logger.Error($"Unable to register expression: {key} from {toolProp.PropertyType}");
                        }
                    }
                }
            }
        }

        private static List<string> expressionExceptions = new List<string>();
        /// <summary>
        /// Runs a registered expression method
        /// </summary>
        /// <param name="expressionMethod">expression method to run</param>
        /// <param name="args">method arguments</param>
        /// <returns></returns>
        public object RunExpressionMethod(ExpressionMethod expressionMethod, object[] args) {
            var instance = expressionMethod.PropInfo.GetValue(this, null);
            return expressionMethod.Method.Invoke(instance, args);
        }
        #endregion

        #region Hotkeys
        /// <summary>
        /// Looks through all the loaded tools and initializes all defined hotkeys
        /// </summary>
        private void InitHotkeys() {
            var toolProps = GetToolProps();

            // toggle boolean settings
            foreach (var toolProp in toolProps) {
                foreach (var prop in toolProp.PropertyType.GetProperties()) {
                    var hotkeyAttrs = prop.GetCustomAttributes(typeof(HotkeyAttribute), true);

                    foreach (var attr in hotkeyAttrs) {
                        var title = ((HotkeyAttribute)attr).Title;
                        var description = ((HotkeyAttribute)attr).Description;
                        try {
                            delHotkeyAction del = () => {
                                try {
                                    prop.SetValue(toolProp.GetValue(this, null), !(bool)prop.GetValue(toolProp.GetValue(this, null), null), null);
                                    Logger.WriteToChat($"Toggle Hotkey Pressed: {toolProp.Name}.{prop.Name} = {prop.GetValue(toolProp.GetValue(this, null), null)}");
                                }
                                catch (Exception ex) {
                                    Logger.LogException(ex);
                                    Logger.Error(ex.Message);
                                }
                                return true;
                            };
                            HotkeyWrapperManager.AddHotkey(del, title, description, 0, false, false, false);
                        }
                        catch (Exception ex) {
                            Logger.LogException(ex);
                            Logger.Error($"Unable to add hotkey {title}: {ex.Message}");
                        }
                    }
                }
            }
        }
        #endregion

        /// <summary>
        /// Called once during plugin shutdown
        /// </summary>
        public void Shutdown() {
            try {
                if (Core != null)
                    Core.CommandLineText -= Core_CommandLineText;
                HotkeyWrapperManager.Shutdown();
                foreach (var tool in LoadedTools) {
                    try {
                        tool.Dispose();
                    }
                    catch (Exception ex) { Logger.LogException(ex); }
                }

                if (MainView != null)
                    MainView.Dispose();
                if (MapView != null)
                    MapView.Dispose();
                if (ItemGiverView != null)
                    ItemGiverView.Dispose();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
    }
}
