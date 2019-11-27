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
        private bool pluginsReady = false;
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

        private string pluginStorageDirectory;
        public string PluginStorageDirectory {
            get {
                if (!string.IsNullOrEmpty(pluginStorageDirectory)) return pluginStorageDirectory;

                System.Configuration.Configuration config = null;
                System.Configuration.KeyValueConfigurationElement element = null;
                try {
                    config = System.Configuration.ConfigurationManager.OpenExeConfiguration(PluginAssemblyPath);
                    element = config.AppSettings.Settings["PluginDirectory"];
                }
                catch { }
                if (element != null && !string.IsNullOrEmpty(element.Value)) {
                    pluginStorageDirectory = element.Value;
                }
                else {
                    pluginStorageDirectory = System.IO.Path.Combine(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Decal Plugins"), PluginName);
                    try {
                        config.AppSettings.Settings.Add("PluginDirectory", pluginStorageDirectory);
                        config.Save();
                    }
                    catch { }
                }

                return pluginStorageDirectory;
            }
        }

        public string AccountName;
        public string CharacterName;
        public string ServerName;
        public Dictionary<int, string> Characters = new Dictionary<int, string>();
        private bool hasLoaded = false;

        /// <summary>
        /// This is called when the plugin is started up. This happens only once.
        /// </summary>
        protected override void Startup() {
            try {
                ServerDispatch += FilterCore_ServerDispatch;
                ClientDispatch += FilterCore_ClientDispatch;
                Core.PluginInitComplete += Core_PluginInitComplete;
                Core.PluginTermComplete += Core_PluginTermComplete;
            }
            catch (Exception ex) { LogException(ex); }
        }

        private void FilterCore_ClientDispatch(object sender, NetworkMessageEventArgs e) {
            try {
                if (e.Message.Type == 0xF657) { // SendEnterWorld C2S
                    int loginId = Convert.ToInt32(e.Message["character"]);

                    if (Characters.ContainsKey(loginId)) {
                        CharacterName = Characters[loginId];
                    }
                    else {
                        throw new Exception($"Character id not in character list! " + Characters.Keys.ToArray());
                    }
                }
            }
            catch (Exception ex) { LogException(ex); }
        }

        private void FilterCore_ServerDispatch(object sender, NetworkMessageEventArgs e) {
            try {
                switch (e.Message.Type) {
                    case 0xF658: // LoginCharacterSet S2C
                        AccountName = e.Message.Value<string>("zonename");
                        int characterCount = e.Message.Value<int>("characterCount");
                        MessageStruct characters = e.Message.Struct("characters");

                        Characters.Clear();

                        for (int i = 0; i < characterCount; i++) {
                            int id = characters.Struct(i).Value<int>("character");
                            string name = characters.Struct(i).Value<string>("name");
                            Characters.Add(id, name);
                        }
                        break;

                    case 0xF7E1:
                        ServerName = e.Message.Value<string>("server");
                        break;
                }
            }
            catch (Exception ex) { LogException(ex); }
        }

        private void Core_PluginInitComplete(object sender, EventArgs e) {
            try {
                pluginsReady = true;

                if (needsReload == false) {
                    Core.RenderFrame += Core_RenderFrame;
                }

                needsReload = true;
                lastFileChange = DateTime.UtcNow;

                if (PluginWatcher == null) {
                    PluginWatcher = new FileSystemWatcher();
                    PluginWatcher.Path = PluginAssemblyDirectory;
                    PluginWatcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite;
                    PluginWatcher.Filter = PluginAssemblyName;
                    PluginWatcher.Changed += PluginWatcher_Changed; ;
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
                if (needsReload && pluginsReady && DateTime.UtcNow - lastFileChange > TimeSpan.FromSeconds(1)) {
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
                if (needsReload == false) {
                    Core.RenderFrame += Core_RenderFrame;
                }
                needsReload = true;
                lastFileChange = DateTime.UtcNow;
            }
            catch (Exception ex) { LogException(ex); }
        }

        private void LoadPluginAssembly() {
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
                    Host,
                    Core,
                    AccountName,
                    CharacterName,
                    ServerName
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
