using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Decal.Adapter;
using Decal.Adapter.Wrappers;
using Decal.Interop.Core;
using Decal.Interop.Render;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Diagnostics;
using UBLoader.Lib.Settings;
using Exceptionless;
using System.Text;
using System.Text.RegularExpressions;

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
            public Setting<bool> UploadExceptions = new Setting<bool>(true);
        }
        public static GlobalSettings Global = new GlobalSettings();
        #endregion Global Settings

        public FilterCore() {
            System.Resources.ResourceManager rm = new System.Resources.ResourceManager(GetType().Namespace + ".Properties.Resources", System.Reflection.Assembly.GetExecutingAssembly());
            System.Reflection.Assembly.Load((byte[])rm.GetObject("LiteDB"));
            System.Reflection.Assembly.Load((byte[])rm.GetObject("Newtonsoft_Json"));
            System.Reflection.Assembly.Load((byte[])rm.GetObject("Antlr4_Runtime"));
            System.Reflection.Assembly.Load((byte[])rm.GetObject("UBHelper"));
        }

        /// <summary>
        /// This is called when the plugin is started up. This happens only once.
        /// </summary>
        protected override void Startup() {
            try {
                Settings = new Settings(this, System.IO.Path.Combine(PluginAssemblyDirectory, "utilitybelt.settings.json"));
                Settings.Load();

                if (Global.UploadExceptions) {
                    Exceptionless.ExceptionlessClient.Current.Configuration.IncludePrivateInformation = false;
                    Exceptionless.ExceptionlessClient.Current.Startup();
                }

                Global.FrameRate.Changed += FrameRate_Changed;
                LoadAssemblyConfig();
                UBHelper.Core.GameStateChanged += Core_GameStateChanged;

                UBHelper.Core.FilterStartup(PluginAssemblyPath, Global.PluginStorageDirectory);

                if (!Global.FrameRate.IsDefault)
                    UBHelper.SimpleFrameLimiter.globalMax = Global.FrameRate;
            }
            catch (Exception ex) { LogException(ex); }
        }

        private void FrameRate_Changed(object sender, SettingChangedEventArgs e) {
            UBHelper.SimpleFrameLimiter.globalMax = Global.FrameRate;
        }

        private void Core_GameStateChanged(UBHelper.GameState previous, UBHelper.GameState new_state) {
            try {
                switch (new_state) {
                    case UBHelper.GameState.Character_Select_Screen:
                        VersionWatermark.Display(Host, $"{PluginName} v{FileVersionInfo.GetVersionInfo(PluginAssemblyPath).ProductVersion}");
                        UnloadPluginAssembly();
                        break;
                    case UBHelper.GameState.Creating_Character:
                        VersionWatermark.Destroy();
                        break;
                    case UBHelper.GameState.Entering_Game:
                        VersionWatermark.Destroy();
                        PluginsReady = true;
                        LoadPluginAssembly();
                        break;
                    case UBHelper.GameState.Logging_Out:
                        PluginsReady = false;
                        UnloadPluginAssembly();
                        break;
                }
            }
            catch(Exception e) { LogException(e); }
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
                if (Settings.NeedsSave)
                    Settings.Save();
                Global.FrameRate.Changed -= FrameRate_Changed;
                Settings.Dispose();
                UBHelper.Core.GameStateChanged -= Core_GameStateChanged;
                UnloadPluginAssembly();
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