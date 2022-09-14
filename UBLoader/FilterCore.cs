using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Decal.Adapter;
using System.Runtime.InteropServices;
using System.Diagnostics;
using UBService.Lib.Settings;
using Exceptionless;
using System.Text;
using System.Text.RegularExpressions;
using AcClient;
using ACE.DatLoader;
using Microsoft.DirectX.Direct3D;
using UBLoader.Lib;
using UBService.Views;
using UBService.Views.SettingsEditor;
using System.Drawing;

namespace UBLoader {
    [FriendlyName("UtilityBelt")]
    public class FilterCore : FilterBase {
        public static object PluginInstance;
        public static Assembly CurrentAssembly;
        public static Type PluginType;
        public static FileSystemWatcher PluginWatcher = null;
        public static Settings Settings { get; set; }
        public static bool PluginsReady = false;

        private bool needsReload = false;
        private DateTime lastFileChange = DateTime.UtcNow;
        private static bool hasLoaded = false;

        internal static Guid IID_IDirect3DDevice9 = new Guid("{D0223B96-BF7A-43fd-92BD-A43B0D82B9EB}");
        internal IntPtr unmanagedD3dPtr;
        internal static Device D3Ddevice;
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool PostMessage(IntPtr hhwnd, uint msg, IntPtr wparam, UIntPtr lparam);

        // we store player skills from PlayerDesc message because decal doesn't like old skills
        public static Dictionary<int, int> PlayerDescSkillState = new Dictionary<int, int>();

        public static string PluginName { get { return "UtilityBelt"; } }
        public static string PluginAssemblyNamespace { get { return "UtilityBelt.UtilityBeltPlugin"; } }
        public static string PluginAssemblyName { get { return "UtilityBelt.dll"; } }

        public static string PluginAssemblyDirectory {
            get {
                string fullPath = System.Reflection.Assembly.GetAssembly(typeof(FilterCore)).Location;
                return System.IO.Path.GetDirectoryName(fullPath);
            }
        }

        public static string PluginAssemblyPath {
            get {
                return System.IO.Path.Combine(PluginAssemblyDirectory, PluginAssemblyName);
            }
        }

        public static string DllConfigPath {
            get {
                return System.IO.Path.Combine(PluginAssemblyDirectory, $"{PluginAssemblyName}.config");
            }
        }

        public static CellDatDatabase CellDat { get; private set; }
        public static PortalDatDatabase PortalDat { get; private set; }
        public static LanguageDatDatabase LanguageDat { get; private set; }

        #region Global Settings
        public class GlobalSettings : ISetting {
            [Summary("Plugin storage directory path")]
            public Setting<string> PluginStorageDirectory = new Setting<string>(System.IO.Path.Combine(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Decal Plugins"), PluginName));

            [Summary("Database storage file path")]
            public Setting<string> DatabaseFile = new Setting<string>(System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Decal Plugins"), PluginName), "utilitybelt.db"));

            [Summary("Log directory")]
            public Setting<string> LogDirectory = new Setting<string>(System.IO.Path.Combine(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Decal Plugins"), PluginName));

            [Summary("Enable plugin hot reloading when utilitybelt.dll changes")]
            public Setting<bool> HotReload = new Setting<bool>(false);

            [Summary("Global frame rate limit. Set to 0 to disable.")]
            public Setting<int> FrameRate = new Setting<int>(0);

            [Summary("Upload exceptions to the mothership")]
            public Setting<bool> UploadExceptions = new Setting<bool>(false);

            [Summary("Show Character Creation UI")]
            public Setting<bool> ShowCharacterCreationUI = new Setting<bool>(false);

            [Summary("Kill clients when they reach the disconnect screen")]
            public Setting<bool> KillDisconnectedClients = new Setting<bool>(false);
        }
        public static GlobalSettings Global = new GlobalSettings();
        private Lib.CharacterCreation characterCreationUI;
        private ManagedTexture settingsIcon;
        private SettingsEditor settingsUIHud;
        #endregion Global Settings

        public FilterCore() {
            System.Resources.ResourceManager rm = new System.Resources.ResourceManager(GetType().Namespace + ".Properties.Resources", System.Reflection.Assembly.GetExecutingAssembly());
            System.Reflection.Assembly.Load((byte[])rm.GetObject("LiteDB"));
            //System.Reflection.Assembly.Load((byte[])rm.GetObject("Newtonsoft_Json"));
            System.Reflection.Assembly.Load((byte[])rm.GetObject("Antlr4_Runtime"));
            System.Reflection.Assembly.Load((byte[])rm.GetObject("UBHelper"));
            System.Reflection.Assembly.Load((byte[])rm.GetObject("ACE_DatLoader"));
        }

        /// <summary>
        /// This is called when the plugin is started up. This happens only once.
        /// </summary>
        protected override void Startup() {
            try {
                Settings = new Settings(this, System.IO.Path.Combine(PluginAssemblyDirectory, "utilitybelt.settings.json"), (setting) => {
                    return setting.FieldInfo.DeclaringType == typeof(GlobalSettings);
                });
                Settings.Load();

                if (Global.UploadExceptions) {
                    Exceptionless.ExceptionlessClient.Current.Configuration.IncludePrivateInformation = false;
                    Exceptionless.ExceptionlessClient.Current.Startup();
                }

                Global.FrameRate.Changed += FrameRate_Changed;
                LoadAssemblyConfig();
                LoadDats();
                UBHelper.Core.GameStateChanged += Core_GameStateChanged;
                UBHelper.Core.Kevorkian += Core_Kevorkian;

                UBHelper.Core.FilterStartup(PluginAssemblyPath, Global.PluginStorageDirectory);

                if (!Global.FrameRate.IsDefault)
                    UBHelper.SimpleFrameLimiter.globalMax = Global.FrameRate;
            }
            catch (Exception ex) { LogException(ex); }
        }

        private void CreateCharacterCreateUI() {
            if (Global.ShowCharacterCreationUI && characterCreationUI == null) {
                object a = CoreManager.Current.Decal.Underlying.GetD3DDevice(ref IID_IDirect3DDevice9);
                Marshal.QueryInterface(Marshal.GetIUnknownForObject(a), ref IID_IDirect3DDevice9, out unmanagedD3dPtr);
                D3Ddevice = new Device(unmanagedD3dPtr);

                characterCreationUI = new UBLoader.Lib.CharacterCreation();
                characterCreationUI.OnFinished += CharacterCreationUI_OnFinished;
            }
        }

        private void DestroyCharacterCreateUI() {
            if (characterCreationUI != null) {
                characterCreationUI.OnFinished -= CharacterCreationUI_OnFinished;
                characterCreationUI.Dispose();
                characterCreationUI = null;
            }
        }

        private unsafe void CharacterCreationUI_OnFinished(object sender, CharacterCreation.FinishedEventArgs e) {
            try {
                //LogError($"Character Creation Result: {e.ACCharGenResult}");
                LogError($"Creating Character: {e.ACCharGenResult.name}");
                ACCharGenResult res = e.ACCharGenResult;
                accountID account = new accountID();
                account.__Ctor(&(*CPlayerSystem.s_pPlayerSystem)->account_);
                (*CPlayerSystem.s_pPlayerSystem)->m_pCharGenState->CharGenState.verificationState = CG_VERIFICATION_RESPONSE.CG_VERIFICATION_RESPONSE_PENDING;
                (*CPlayerSystem.s_pPlayerSystem)->m_pCharGenState->CharGenState.slot = -1;
                CharacterCreation.CPlayerSystem__Handle_CharGenVerificationResponse_hook.Setup(new CharacterCreation.CPlayerSystem__Handle_CharGenVerificationResponse_def(CharacterCreation.CPlayerSystem__Handle_CharGenVerificationResponse));
                Proto_UI.SendCharGenResult(&res, account);
            }
            catch (Exception ex) { LogException(ex); }
        }

        private void FrameRate_Changed(object sender, SettingChangedEventArgs e) {
            UBHelper.SimpleFrameLimiter.globalMax = Global.FrameRate;
        }

        /// <summary>
        /// This event is triggered in UBHelper, when an inventory request hangs for over 30 seconds.
        /// </summary>
        /// <param name="prevRequest">request type (AcClient.InventoryRequest)</param>
        /// <param name="prevRequestTime">request time</param>
        /// <param name="curTime">current (ac) time in seconds</param>
        /// <param name="objectID">the ID of the object that the request was performed on</param>
        private void Core_Kevorkian(int prevRequest, double prevRequestTime, double curTime, int objectID) {
            if (Global.KillDisconnectedClients) {
                LogError($"AC Server Request {(InventoryRequest)prevRequest} on objectID 0x{objectID:X8} timeout!");
                PostMessage(Core.Decal.Hwnd, 0x0002 /* WM_DESTROY */, (IntPtr)0, (UIntPtr)0);
            }
            else
                LogError($"AC Server Request {(InventoryRequest)prevRequest} on objectID 0x{objectID:X8} timeout! (enable Global.KillDisconnectedClients to kill client when this happens)");
        }
        private void Core_GameStateChanged(UBHelper.GameState previous, UBHelper.GameState new_state) {
            try {
                switch (new_state) {
                    case UBHelper.GameState.Character_Select_Screen:
                        MakeSettingsUI();
                        settingsUIHud.Hud.ShowInBar = true;
                        VersionWatermark.Display(Host, $"{PluginName} v{FileVersionInfo.GetVersionInfo(PluginAssemblyPath).ProductVersion}");
                        CreateCharacterCreateUI();
                        UnloadPluginAssembly();
                        Decal.Adapter.CoreManager.Current.EchoFilter.ServerDispatch += EchoFilter_ServerDispatch;
                        if (previous == UBHelper.GameState.Logging_Out)
                            LoaderLogin.Login();
                        break;
                    case UBHelper.GameState.In_Game:
                        LoaderLogin.ClearNextLogin();
                        break;
                    case UBHelper.GameState.Creating_Character:
                        DestroyCharacterCreateUI();
                        VersionWatermark.Destroy();
                        break;
                    case UBHelper.GameState.Entering_Game:
                        settingsUIHud.Hud.ShowInBar = false;
                        DestroyCharacterCreateUI();
                        VersionWatermark.Destroy();
                        PluginsReady = true;
                        LoadPluginAssembly();
                        break;
                    case UBHelper.GameState.Logging_Out:
                        PluginsReady = false;
                        UnloadPluginAssembly();
                        PlayerDescSkillState.Clear();
                        Decal.Adapter.CoreManager.Current.EchoFilter.ServerDispatch -= EchoFilter_ServerDispatch;
                        break;
                    case UBHelper.GameState.Disconnected:
                        if (Global.KillDisconnectedClients) {
                            PostMessage(Core.Decal.Hwnd, 0x0002 /* WM_DESTROY */, (IntPtr)0, (UIntPtr)0);
                        }
                        break;
                }
            }
            catch (Exception e) { LogException(e); }
        }

        private void MakeSettingsUI() {
            if (settingsUIHud != null)
                return;

            using (Stream manifestResourceStream = GetType().Assembly.GetManifestResourceStream("UBLoader.Resources.settings.png")) {
                settingsIcon = new ManagedTexture(new Bitmap(manifestResourceStream));
            }

            settingsUIHud = new SettingsEditor("UBLoader Settings", this, new object[] { typeof(UBLoader.FilterCore) });
            settingsUIHud.Hud.Visible = false;
        }

        private void LoadAssemblyConfig() {
            if (!System.IO.File.Exists(DllConfigPath))
                return;

            System.Configuration.Configuration config = null;
            try {
                config = System.Configuration.ConfigurationManager.OpenExeConfiguration(PluginAssemblyPath);
                var keys = config.AppSettings.Settings.AllKeys;
                if (keys.Contains("PluginDirectory")) {
                    Global.PluginStorageDirectory.Value = config.AppSettings.Settings["PluginDirectory"].Value;
                }
                if (keys.Contains("DatabaseFile")) {
                    Global.DatabaseFile.Value = config.AppSettings.Settings["DatabaseFile"].Value;
                }
                if (keys.Contains("HotReload")) {
                    Global.HotReload.Value = config.AppSettings.Settings["HotReload"].Value == "true";
                }
                if (keys.Contains("FrameRate")) {
                    int.TryParse(config.AppSettings.Settings["FrameRate"].Value, out int frameRate);
                    Global.FrameRate.Value = frameRate;
                }

                System.IO.File.Delete(DllConfigPath);
            }
            catch { }
        }

        private void LoadDats() {
            var datDirectory = "";
            var cellDatPath = System.IO.Path.Combine(datDirectory, "client_cell_1.dat");
            var portalDatPath = System.IO.Path.Combine(datDirectory, "client_portal.dat");
            var languageDatPath = System.IO.Path.Combine(datDirectory, "client_local_English.dat");

            if (!System.IO.File.Exists(cellDatPath)) {
                LogError($"Unable to load cellDat: {cellDatPath}");
                return;
            }
            if (!System.IO.File.Exists(portalDatPath)) {
                LogError($"Unable to load portalDat: {portalDatPath}");
                return;
            }
            if (!System.IO.File.Exists(languageDatPath)) {
                LogError($"Unable to load languageDat: {portalDatPath}");
                return;
            }

            CellDat = new CellDatDatabase(cellDatPath, true);
            PortalDat = new PortalDatDatabase(portalDatPath, true);
            LanguageDat = new LanguageDatDatabase(languageDatPath, true);
        }

        private void Core_RenderFrame(object sender, EventArgs e) {
            try {
                if (needsReload && PluginsReady && DateTime.UtcNow - lastFileChange > TimeSpan.FromSeconds(1)) {
                    needsReload = false;
                    Core.RenderFrame -= Core_RenderFrame;
                    try {
                        if (hasLoaded) Core.Actions.AddChatText("Reloading UtilityBelt", 1);
                    }
                    catch { }
                    UnloadPluginAssembly();
                    LoadPluginAssembly();
                }
            }
            catch (Exception ex) { LogException(ex); }
        }

        private static void EchoFilter_ServerDispatch(object sender, NetworkMessageEventArgs e) {
            try {
                if (e.Message.Type == 0x02DD) {
                    var key = e.Message.Value<int>("key");
                    var skill = e.Message.Struct("value");
                    var state = skill.Value<int>("state");
                    if (PlayerDescSkillState.ContainsKey(key))
                        PlayerDescSkillState[key] = state;
                    else
                        PlayerDescSkillState.Add(key, state);
                }
                else if (e.Message.Type == 0xF7B0 && e.Message.Value<int>("event") == 0x0013) {
                    var vectors = e.Message.Struct("vectors");
                    var flags = vectors.Value<int>("flags");
                    if ((flags & 0x00000002) != 0) {
                        var skillCount = vectors.Value<short>("skillCount");
                        var skills = vectors.Struct("skills");
                        PlayerDescSkillState.Clear();
                        for (var i = 0; i < skillCount; i++) {
                            var key = skills.Struct(i).Value<short>("key");
                            var skill = skills.Struct(i).Struct("value");
                            var state = skill.Value<int>("state");
                            if (PlayerDescSkillState.ContainsKey(key))
                                PlayerDescSkillState[key] = state;
                            else
                                PlayerDescSkillState.Add(key, state);
                        }
                    }
                }
            }
            catch (Exception ex) { LogException(ex); }
        }

        private void PluginWatcher_Changed(object sender, FileSystemEventArgs e) {
            try {
                if (!Global.HotReload)
                    return;
                if (needsReload == false) {
                    Core.RenderFrame += Core_RenderFrame;
                }
                needsReload = true;
                lastFileChange = DateTime.UtcNow;
            }
            catch (Exception ex) { LogException(ex); }
        }

        internal void LoadPluginAssembly() {
            try {
                if (Global.HotReload && PluginWatcher == null) {
                    PluginWatcher = new FileSystemWatcher();
                    PluginWatcher.Path = PluginAssemblyDirectory;
                    PluginWatcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite;
                    PluginWatcher.Filter = PluginAssemblyName;
                    PluginWatcher.Changed += PluginWatcher_Changed;
                    PluginWatcher.EnableRaisingEvents = true;
                }
                if (PluginInstance != null) {
                    LogError("************* Attempt to LoadPluginAssembly() when PluginInstance != null! ***************");
                    UnloadPluginAssembly();
                }

                var assemblyPath = System.IO.File.ReadAllBytes(PluginAssemblyPath);
                var pdbPath = System.IO.File.ReadAllBytes(PluginAssemblyPath.Replace(".dll", ".pdb"));
                CurrentAssembly = Assembly.Load(assemblyPath, pdbPath);
                PluginType = CurrentAssembly.GetType(PluginAssemblyNamespace);
                MethodInfo startupMethod = PluginType.GetMethod("Startup");
                PluginInstance = Activator.CreateInstance(PluginType);
                startupMethod.Invoke(PluginInstance, new object[] {
                    PluginAssemblyPath,
                    Global.PluginStorageDirectory.Value,
                    Global.DatabaseFile.Value,
                    Host,
                    Core
                });

                hasLoaded = true;
            }
            catch (Exception ex) { LogException(ex); }
        }

        private void UnloadPluginAssembly() {
            try {
                if (PluginInstance != null && PluginType != null) {
                    MethodInfo shutdownMethod = PluginType.GetMethod("Shutdown");
                    shutdownMethod.Invoke(PluginInstance, null);
                    PluginInstance = null;
                    CurrentAssembly = null;
                    PluginType = null;
                }
            }
            catch (Exception ex) { LogException(ex); }
        }

        /// <summary>
        /// This is called when the plugin is shut down. This happens only once.
        /// </summary>
        protected override void Shutdown() {
            try {
                settingsIcon?.Dispose();
                settingsUIHud?.Dispose();
                if (Settings != null && Settings.NeedsSave)
                    Settings.Save();
                Global.FrameRate.Changed -= FrameRate_Changed;
                Settings.Dispose();
                UBHelper.Core.GameStateChanged -= Core_GameStateChanged;
                UBHelper.Core.Kevorkian -= Core_Kevorkian;
                UnloadPluginAssembly();
                DestroyCharacterCreateUI();
                UBHelper.Core.FilterShutdown();
            }
            catch (Exception ex) { LogException(ex); }
        }

        public static string GetAnonymousUserId() {
            var world = string.IsNullOrEmpty(UBHelper.Core.WorldName) ? "NoWorldInfo" : UBHelper.Core.WorldName;
            var character = $"{0:X16}";
            if (UBHelper.Core.CharacterSet.ContainsKey(UBHelper.Core.LoginCharacterID)) {
                byte[] stringbytes = Encoding.UTF8.GetBytes(UBHelper.Core.CharacterSet[UBHelper.Core.LoginCharacterID]);
                byte[] hashedBytes = new System.Security.Cryptography
                    .SHA1CryptoServiceProvider()
                    .ComputeHash(stringbytes);
                character = Convert.ToBase64String(hashedBytes);
            }

            return $"{world}:{character}";
        }

        public static bool IsDevelopmentVersion() {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetAssembly(typeof(FilterCore));

            if (assembly != null) {
                var location = System.IO.Path.Combine(PluginAssemblyDirectory, "UBLoader.dll");
                var productVersion = FileVersionInfo.GetVersionInfo(location).ProductVersion;
                return !(new Regex(@"^\d+\.\d+.\d+\.(release|master)")).IsMatch(productVersion);
            }

            return false;
        }

        public static void LogException(Exception ex) {
            Lib.File.TryWrite(System.IO.Path.Combine(Global.LogDirectory, "exceptions.txt"), $"== {DateTime.Now} ==================================================\r\n{ex.ToString()}\r\n============================================================================\r\n\r\n", true);

            if (Global.UploadExceptions && !IsDevelopmentVersion()) {
                try {
                    ex.ToExceptionless(false)
                        .SetUserName(GetAnonymousUserId())
                        .Submit();
                }
                catch { }
            }
        }

        public static void LogError(string ex) {
            Lib.File.TryWrite(System.IO.Path.Combine(Global.LogDirectory, "exceptions.txt"), $"== {DateTime.Now} {ex}\r\n", true);
            try {
                if (hasLoaded) CoreManager.Current.Actions.AddChatText($"[UB] {ex}", 15);
            }
            catch { }
        }


    }
}
