using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Decal.Adapter;
using Decal.Adapter.Wrappers;
using Newtonsoft.Json;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Settings;
using UtilityBelt.Tools;
using UtilityBelt.Views;
using UBService.Lib.Settings;
using UtilityBelt.Lib.Expressions;
using System.Threading;
using ACE.DatLoader;
using System.IO;
using Microsoft.DirectX.Direct3D;
using System.Runtime.InteropServices;

namespace UtilityBelt {

    #region Command wrapper
    public class Command {
        public string Verb { get; }
        public Regex ArgumentRegex { get; }
        public FieldInfo FieldInfo { get; }
        public MethodInfo Method { get; }
        public bool AllowPartialVerbMatch { get; }
        public string Usage { get; }
        public Dictionary<string, string> Examples = new Dictionary<string, string>();
        public string Summary { get; }

        public Command(FieldInfo fieldInfo, MethodInfo method) {
            FieldInfo = fieldInfo;
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
        public FieldInfo FieldInfo { get; }
        public MethodInfo Method { get; }
        public string Usage { get; }
        public Dictionary<string, string> Examples = new Dictionary<string, string>();
        public string Summary { get; }
        public string Signature { get => $"{Name}[{string.Join(", ", ArgumentTypes.Select(t => ExpressionVisitor.GetFriendlyType(t)).ToArray())}]"; }

        public ExpressionMethod(FieldInfo fieldInfo, MethodInfo method) {
            FieldInfo = fieldInfo;
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
        private PerfMonitor PerfMonitor;
        private bool didInit = false;
        internal delegate void CommandHandlerDelegate(string verb, Match args);
        internal readonly Dictionary<string, Command> RegisteredCommands = new Dictionary<string, Command>();

        internal readonly Dictionary<string, ExpressionMethod> RegisteredExpressions = new Dictionary<string, ExpressionMethod>();

        internal CoreManager Core;
        internal NetServiceHost Host;
        internal string PluginName = "UtilityBelt";
        internal string DatabaseFile;
        internal string WorldName;
        internal string CharacterName;
        internal MainView MainView;
        internal ItemGiverView ItemGiverView;
        internal DungeonMapsView DungeonMapView;
        internal LandscapeMapsView LandscapeMapView;
        public Settings Settings;
        public Settings State;
        public Settings ClientUISettings;
        public Settings PlayerOptionsSettings;
        public Settings AliasSettings;
        public Settings GameEventsSettings;
        internal Database Database;
        internal UBHudManager Huds;

        internal static Guid IID_IDirect3DDevice9 = new Guid("{D0223B96-BF7A-43fd-92BD-A43B0D82B9EB}");
        internal IntPtr unmanagedD3dPtr;
        internal Device D3Ddevice;

        internal List<ToolBase> LoadedTools = new List<ToolBase>();
        private static List<string> expressionExceptions = new List<string>();
        public CellDatDatabase CellDat => UBLoader.FilterCore.CellDat;
        public PortalDatDatabase PortalDat => UBLoader.FilterCore.PortalDat;

        #region Tools
        public Plugin Plugin;
        public Aliases Aliases;
        public Arrow Arrow;
        public Assessor Assessor;
        public AutoSalvage AutoSalvage;
        public AutoTinker AutoTinker;
        public AutoTrade AutoTrade;
        public AutoVendor AutoVendor;
        public AutoXp AutoXp;
        public ChatFilter ChatFilter;
        public ChatHandler ChatHandler;
        public ChatLogger ChatLogger;
        public ChatNameClickHandler ChatNameClickHandler;
        public Client Client;
        public Counter Counter;
        public DerethTime DerethTime;
        public DungeonMaps DungeonMaps;
        public EquipmentManager EquipmentManager;
        public FellowshipManager FellowshipManager;
        public GameEvents GameEvents;
        public GUI GUI;
        public HealthTracker HealthTracker;
        public InventoryManager InventoryManager;
        public ItemInfo ItemInfo;
        public Jumper Jumper;
        public LandscapeMaps LandscapeMaps;
        public LoginManager LoginManager;
        public Looter Looter;
        public LSD LSD;
        public Movement Movement;
        public Nametags Nametags;
        public Networking Networking;
        public NetworkUI NetworkUI;
        public OverlayMap OverlayMap;
        public Player Player;
        public PlayerOptions PlayerOptions;
        public Professors Professors;
        public QuestTracker QuestTracker;
        public SpellManager SpellManager;
        //public Tags Tags { get; private set; }
        public VisualNav VisualNav;
        public VTankControl VTank;
        public VTankExtensions VTankExtensions;
        public VTankFellowHeals VTankFellowHeals;
        public XPMeter XPMeter;
        public PrepClick PrepClick;
        #endregion

        public UtilityBeltPlugin() {
            Instance = this;
            PerfMonitor = new PerfMonitor();

            //this will kill performance, it hooks every method in Tools/Lib
            //PerfMonitor.HookAll();
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
        public void Startup(string assemblyLocation, string storagePath, string databaseFile, NetServiceHost host, CoreManager core) {
			try {
                if (UBLoader.FilterCore.Global.UploadExceptions) {
                    Exceptionless.ExceptionlessClient.Current.Configuration.IncludePrivateInformation = false;
                    Exceptionless.ExceptionlessClient.Current.Startup();
                }
                Core = core;
                Host = host;
                DatabaseFile = databaseFile;
                unsafe {
                    WorldName = (*AcClient.Client.m_instance)->m_worldName.ToString();
                    CharacterName = UBHelper.Core.CharacterSet[UBHelper.Core.LoginCharacterID];
                }
                // throw new Exception($"{WorldName} {CharacterName}"); // can't log here.
                object a = CoreManager.Current.Decal.Underlying.GetD3DDevice(ref IID_IDirect3DDevice9);
                Marshal.QueryInterface(Marshal.GetIUnknownForObject(a), ref IID_IDirect3DDevice9, out unmanagedD3dPtr);
                D3Ddevice = new Device(unmanagedD3dPtr);

                Util.Init(this, assemblyLocation, storagePath); //static classes can not have constructors, but still need to init variables.
                UBHelper.Core.Startup(CharacterName);

                Core.CommandLineText += Core_CommandLineText;

                // if we are logged in already, we need to init manually.
                // this happens during hot reloads while logged in.
                if (UBHelper.Core.GameState == UBHelper.GameState.In_Game) {
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
        /// Returns a list of FieldInfo's for all the loaded tools.
        /// </summary>
        /// <returns>A list of FieldInfo's for all the loaded tools.</returns>
        public IEnumerable<FieldInfo> GetToolInfos() {
            return GetType().GetFields(BindingFlags.Instance | BindingFlags.Public).Where(
                p => p.FieldType.IsSubclassOf(typeof(ToolBase))
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
            Huds = new UBHudManager();

            Database = new Database(DatabaseFile);

            MainView = new MainView(this);
            ItemGiverView = new ItemGiverView(this);
            DungeonMapView = new DungeonMapsView(this);
            LandscapeMapView = new LandscapeMapsView(this);

            HotkeyWrapperManager.Startup("UB");

            LoadTools();
            InitSettings();
            InitCommands();
            InitHotkeys();
            InitExpressions();
            InitTools();

            MainView.Init();
            ItemGiverView.Init();
            DungeonMapView.Init();
            LandscapeMapView.Init();

            Logger.Debug($"UB Initialized {DateTime.UtcNow} v{Util.GetVersion(true)}.");

            if (Plugin.CheckForUpdates) {
                UpdateChecker.CheckForUpdate();
            }
            
            if (UBHelper.Core.version < 2020112000) {
                Logger.Error($"UBHelper.dll is out of date. 2020112000 Expected, received {UBHelper.Core.version}");
            }

            Settings.Changed += Settings_Changed;
            State.Changed += State_Changed;
            UBLoader.FilterCore.Settings.Changed += FilterSettings_Changed;
        }

        private void InitSettings() {
            var defaultSettingsPath = System.IO.Path.Combine(Util.AssemblyDirectory, "settings.default.json");
            var statePath = System.IO.Path.Combine(Util.GetCharacterDirectory(), "state.json");

            var binder = new SettingsBinder();

            State = new Settings(this, statePath, p => p.SettingType == SettingType.State, null, "State");
            State.Load();

            Settings = new Settings(this, Plugin.SettingsProfilePath, p => p.SettingType == SettingType.Profile, defaultSettingsPath, "Settings");
            Settings.Load();

            ClientUISettings = new Settings(Client.ClientUI, Client.ClientUIProfilePath, p => p.SettingType == SettingType.ClientUI, null, "ClientUI");
            ClientUISettings.Load();

            PlayerOptionsSettings = new Settings(PlayerOptions.PlayerOption, PlayerOptions.CharacterOptionsProfilePath, p => p.SettingType == SettingType.CharacterSettings, null, "PlayerOptions");
            PlayerOptionsSettings.Load();

            AliasSettings = new Settings(Aliases, Aliases.AliasesProfilePath, p => p.SettingType == SettingType.Alias, null, "Aliases");
            AliasSettings.Load();

            GameEventsSettings = new Settings(GameEvents, GameEvents.GameEventsProfilePath, p => p.SettingType == SettingType.GameEvent, null, "GameEvents");
            GameEventsSettings.Load();
        }

        private void State_Changed(object sender, SettingChangedEventArgs e) {
            if (State.ShouldSave || (State.IsLoaded && State.IsLoading))
                Logger.Debug(e.Setting.FullDisplayValue());
        }

        private void FilterSettings_Changed(object sender, SettingChangedEventArgs e) {
            if (UBLoader.FilterCore.Settings.ShouldSave || (UBLoader.FilterCore.Settings.IsLoaded && UBLoader.FilterCore.Settings.IsLoading))
                Logger.Debug(e.Setting.FullDisplayValue());
        }

        private void Settings_Changed(object sender, SettingChangedEventArgs e) {
            if (Settings.ShouldSave || (Settings.IsLoaded && Settings.IsLoading))
                Logger.Debug(e.Setting.FullDisplayValue());
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
        public void LoadTools() {
            var toolInfos = GetToolInfos();

            foreach (var toolInfo in toolInfos) {
                try {
                    var nameAttrs = toolInfo.FieldType.GetCustomAttributes(typeof(NameAttribute), true);
                    if (nameAttrs.Length == 1) {
                        var tool = Activator.CreateInstance(toolInfo.FieldType, new object[] {
                            this,
                            ((NameAttribute)nameAttrs[0]).Name
                        });
                        toolInfo.SetValue(this, tool);
                        LoadedTools.Add((ToolBase)tool);
                    }
                    else {
                        throw new Exception($"{toolInfo.FieldType} has {nameAttrs.Length} NameAttributes defined, but requires exactly one.");
                    }
                }
                catch (Exception ex) {
                    if (didInit) {
                        Logger.LogException(ex);
                        Logger.Error(ex.ToString());
                        Logger.Error($"Error loading tool: {toolInfo.FieldType}");
                    }
                    else {
                        Console.WriteLine($"Unable to load {toolInfo.FieldType}: {ex.ToString()}");
                    }
                }
            }
        }

        #region Command Handling
        /// <summary>
        /// Looks through all the loaded tools and initializes all defined commands
        /// </summary>
        private void InitCommands() {
            var toolInfos = GetToolInfos();

            foreach (var toolInfo in toolInfos) {
                foreach (var method in toolInfo.FieldType.GetMethods()) {
                    var commandPatternAttrs = method.GetCustomAttributes(typeof(CommandPatternAttribute), true);

                    foreach (var attr in commandPatternAttrs) {
                        var verb = ((CommandPatternAttribute)attr).Verb;
                        var argsPattern = ((CommandPatternAttribute)attr).ArgumentsPattern;

                        try {
                            if (RegisteredCommands.ContainsKey(verb)) {
                                Logger.Error($"Unable to register {verb} from {toolInfo.FieldType} because it was already registered by {RegisteredCommands[verb].FieldInfo.DeclaringType}.");
                                continue;
                            }

                            RegisteredCommands.Add(verb, new Command(toolInfo, method));
                        }
                        catch (Exception ex) {
                            Logger.LogException(ex);
                            Logger.Error($"Unable to register command: {verb} from {toolInfo.FieldType} with regex: {argsPattern}");
                        }
                    }
                }
            }
        }

        private readonly Regex slashUB = new Regex(@"^[/@]+ub( |$)");
        private void Core_CommandLineText(object sender, ChatParserInterceptEventArgs e) {
            try {
                if (slashUB.IsMatch(e.Text)) {
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
                            Logger.Error($"Did you mean one of these? { string.Join(", ", partialMatches.ToArray())}");
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
                var instance = command.FieldInfo.GetValue(this);
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
            var toolInfos = GetToolInfos();

            foreach (var toolInfo in toolInfos) {
                foreach (var method in toolInfo.FieldType.GetMethods()) {
                    var expressionMethodAttrs = method.GetCustomAttributes(typeof(ExpressionMethodAttribute), true);

                    foreach (var attr in expressionMethodAttrs) {
                        var name = ((ExpressionMethodAttribute)attr).Name;

                        try {
                            if (RegisteredExpressions.ContainsKey(name)) {
                                Logger.Error($"Unable to register expression {name} from {toolInfo.FieldType} because it was already registered by {RegisteredExpressions[name].FieldInfo.DeclaringType}.");
                                continue;
                            }

                            RegisteredExpressions.Add(name, new ExpressionMethod(toolInfo, method));
                        }
                        catch (Exception ex) {
                            Logger.LogException(ex);
                            Logger.Error(ex.ToString());
                            Logger.Error($"Unable to register expression: {name} from {toolInfo.FieldType}");
                        }
                    }
                }
            }
        }

        private Dictionary<ExpressionMethod, object> _expressionMethodInstanceLookup = new Dictionary<ExpressionMethod, object>();
        /// <summary>
        /// Runs a registered expression method
        /// </summary>
        /// <param name="expressionMethod">expression method to run</param>
        /// <param name="args">method arguments</param>
        /// <returns></returns>
        public object RunExpressionMethod(ExpressionMethod expressionMethod, object[] args) {
            if (_expressionMethodInstanceLookup.TryGetValue(expressionMethod, out object instance)) {
                return expressionMethod.Method.Invoke(instance, args);
            }
            instance = expressionMethod.FieldInfo.GetValue(this);
            _expressionMethodInstanceLookup.Add(expressionMethod, instance);
            return expressionMethod.Method.Invoke(instance, args);
        }
        #endregion

        #region Hotkeys
        /// <summary>
        /// Looks through all the loaded tools and initializes all defined hotkeys
        /// </summary>
        private void InitHotkeys() {
            var toolInfos = GetToolInfos();

            // toggle boolean settings
            foreach (var toolInfo in toolInfos) {
                var tool = toolInfo.GetValue(this);
                foreach (var field in toolInfo.FieldType.GetFields()) {
                    var hotkeyAttrs = field.GetCustomAttributes(typeof(HotkeyAttribute), true);

                    foreach (var attr in hotkeyAttrs) {
                        var title = ((HotkeyAttribute)attr).Title;
                        var description = ((HotkeyAttribute)attr).Description;
                        try {
                            delHotkeyAction del = () => {
                                try {
                                    var setting = ((ISetting)field.GetValue(tool));
                                    setting.SetValue(!(bool)setting.GetValue());
                                    if (!Plugin.Debug)
                                        Logger.WriteToChat($"Toggle Hotkey Pressed: {toolInfo.Name}.{field.Name} = {field.GetValue(tool)}");
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
                UBLoader.FilterCore.Settings.Changed -= FilterSettings_Changed;
                if (Settings != null) {
                    Settings.Changed -= Settings_Changed;
                    if (Settings.NeedsSave)
                        Settings.Save();
                }
                if (State != null) {
                    State.Changed -= State_Changed;
                    if (State.NeedsSave)
                        State.Save();
                }

                if (ClientUISettings != null && ClientUISettings.NeedsSave)
                    ClientUISettings.Save();
                if (PlayerOptionsSettings != null && PlayerOptionsSettings.NeedsSave)
                    PlayerOptionsSettings.Save();
                if (AliasSettings != null && AliasSettings.NeedsSave)
                    AliasSettings.Save();
                if (GameEventsSettings != null && GameEventsSettings.NeedsSave)
                    GameEventsSettings.Save();

                if (Core != null) {
                    Core.CommandLineText -= Core_CommandLineText;
                    Core.CharacterFilter.Login -= CharacterFilter_Login;
                }
                HotkeyWrapperManager.Shutdown();
                foreach (var tool in LoadedTools) {
                    try {
                        tool.Dispose();
                    }
                    catch (Exception ex) { Logger.LogException(ex); }
                }

                MainView?.Dispose();
                DungeonMapView?.Dispose();
                LandscapeMapView?.Dispose();
                ItemGiverView?.Dispose();

                Lib.ActionQueue.Dispose();
                PerfMonitor?.Dispose();
                UBLoader.Lib.File.FlushFiles();
                Settings?.Dispose();
                State?.Dispose();
                ClientUISettings?.Dispose();
                PlayerOptionsSettings?.Dispose();
                AliasSettings?.Dispose();
                GameEventsSettings?.Dispose();
                Huds?.Dispose();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
    }
}
