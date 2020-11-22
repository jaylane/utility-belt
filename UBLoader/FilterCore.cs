using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Decal.Adapter;
using Decal.Adapter.Wrappers;

namespace UBLoader {

    //Attaches events from core
	[WireUpBaseEvents]
    
	[FriendlyName("UtilityBelt")]
	public class FilterCore : FilterBase {
        public object PluginInstance;
        public Assembly CurrentAssembly;
        public Type PluginType;
        public FileSystemWatcher PluginWatcher = null;

        private bool needsReload = false;
        public bool pluginsReady = false;
        private DateTime lastFileChange = DateTime.UtcNow;

        public string PluginName { get { return "UtilityBelt"; } }
        public string PluginAssemblyNamespace { get { return "UtilityBelt.UtilityBeltPlugin"; } }
        public string PluginAssemblyName { get { return "UtilityBelt.dll"; } }
        public string PluginAssemblyDirectory {
            get {
                string fullPath = System.Reflection.Assembly.GetAssembly(typeof(FilterCore)).Location;
                return System.IO.Path.GetDirectoryName(fullPath);
            }
        }
        public string PluginAssemblyPath {
            get {
                return System.IO.Path.Combine(PluginAssemblyDirectory, PluginAssemblyName);
            }
        }

        public string PluginStorageDirectory { get; private set; }
        public string DatabaseFile { get; private set; }
        public bool HotReload { get; private set; }

        private bool hasLoaded = false;
        private bool needsLoginLoad = false;

        public FilterCore() {
            System.Resources.ResourceManager rm = new System.Resources.ResourceManager(GetType().Namespace + ".Properties.Resources", System.Reflection.Assembly.GetExecutingAssembly());
            System.Reflection.Assembly.Load((byte[])rm.GetObject("LiteDB"));
            System.Reflection.Assembly.Load((byte[])rm.GetObject("Newtonsoft_Json"));
            System.Reflection.Assembly.Load((byte[])rm.GetObject("SharedMemory"));
            System.Reflection.Assembly.Load((byte[])rm.GetObject("Antlr4_Runtime"));
            System.Reflection.Assembly.Load((byte[])rm.GetObject("UBHelper"));
        }

        /// <summary>
        /// This is called when the plugin is started up. This happens only once.
        /// </summary>
        protected override void Startup() {
            try {
                LoadAssemblyConfig();
                
                Core.PluginInitComplete += Core_PluginInitComplete;
                Core.PluginTermComplete += Core_PluginTermComplete;
                UBHelper.Core.FilterStartup(PluginAssemblyPath, PluginStorageDirectory);
            }
            catch (Exception ex) { LogException(ex); }
        }

        private void LoadAssemblyConfig() {
            System.Configuration.Configuration config = null;
            try {
                config = System.Configuration.ConfigurationManager.OpenExeConfiguration(PluginAssemblyPath);
                var keys = config.AppSettings.Settings.AllKeys;
                if (keys.Contains("PluginDirectory"))
                    PluginStorageDirectory = config.AppSettings.Settings["PluginDirectory"].Value;
                if (keys.Contains("DatabaseFile"))
                    DatabaseFile = config.AppSettings.Settings["DatabaseFile"].Value;
                if (keys.Contains("HotReload"))
                    HotReload = config.AppSettings.Settings["HotReload"].Value == "true";
            }
            catch { }
            if (string.IsNullOrEmpty(PluginStorageDirectory)) {
                PluginStorageDirectory = System.IO.Path.Combine(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Decal Plugins"), PluginName);
                try {
                    config.AppSettings.Settings.Add("PluginDirectory", PluginStorageDirectory);
                    config.Save();
                }
                catch { }
            }
            if (string.IsNullOrEmpty(DatabaseFile)) {
                DatabaseFile = System.IO.Path.Combine(PluginStorageDirectory, "utilitybelt.db");
                try {
                    config.AppSettings.Settings.Add("DatabaseFile", DatabaseFile);
                    config.Save();
                }
                catch { }
            }
        }

        private void Core_PluginInitComplete(object sender, EventArgs e) {
            try {
                pluginsReady = true;
                lastFileChange = DateTime.UtcNow;
                LoadPluginAssembly();

                if (PluginWatcher == null) {
                    PluginWatcher = new FileSystemWatcher();
                    PluginWatcher.Path = PluginAssemblyDirectory;
                    PluginWatcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite;
                    PluginWatcher.Filter = PluginAssemblyName;
                    PluginWatcher.Changed += PluginWatcher_Changed;
                    PluginWatcher.EnableRaisingEvents = true;
                }
            }
            catch (Exception ex) { LogException(ex); }
        }

        private void Core_PluginTermComplete(object sender, EventArgs e) {
            try {
                pluginsReady = false;
                UnloadPluginAssembly();
            }
            catch (Exception ex) { LogException(ex); }
        }

        private void Core_RenderFrame(object sender, EventArgs e) {
            try {
                if (!needsLoginLoad && needsReload && pluginsReady && DateTime.UtcNow - lastFileChange > TimeSpan.FromSeconds(1)) {
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
                if (!HotReload)
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
                if (!pluginsReady) {
                    needsReload = true;
                    Core.RenderFrame += Core_RenderFrame;
                    return;
                }

                CurrentAssembly = Assembly.Load(File.ReadAllBytes(PluginAssemblyPath));
                PluginType = CurrentAssembly.GetType(PluginAssemblyNamespace);
                MethodInfo startupMethod = PluginType.GetMethod("Startup");
                PluginInstance = Activator.CreateInstance(PluginType);
                startupMethod.Invoke(PluginInstance, new object[] {
                    PluginAssemblyPath,
                    PluginStorageDirectory,
                    DatabaseFile,
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
                Core.PluginInitComplete -= Core_PluginInitComplete;
                Core.PluginTermComplete -= Core_PluginTermComplete;
                UnloadPluginAssembly();
                UBHelper.Core.FilterShutdown();
            }
			catch (Exception ex) { LogException(ex); }
        }

        public void LogException(Exception ex) {
            try {
                using (StreamWriter writer = new StreamWriter(System.IO.Path.Combine(PluginStorageDirectory, "exceptions.txt"), true)) {
                    writer.WriteLine("============================================================================");
                    writer.WriteLine(DateTime.Now.ToString());
                    writer.WriteLine("Error: " + ex.Message);
                    writer.WriteLine("Source: " + ex.Source);
                    writer.WriteLine("Stack: " + ex.StackTrace);
                    if (ex.InnerException != null) {
                        writer.WriteLine("Inner: " + ex.InnerException.Message);
                        writer.WriteLine("Inner Stack: " + ex.InnerException.StackTrace);
                    }
                    writer.WriteLine("============================================================================");
                    writer.WriteLine("");
                    writer.Close();
                }
            }
            catch {
            }
        }

        public void LogError(string message) {
            try {
                using (StreamWriter writer = new StreamWriter(System.IO.Path.Combine(PluginStorageDirectory, "exceptions.txt"), true)) {
                    writer.WriteLine(message);
                    writer.Close();
                }
            }
            catch {
            }
        }
    }
}
