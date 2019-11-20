using System;
using System.IO;
using System.Reflection;
using Decal.Adapter;
using Decal.Adapter.Wrappers;

namespace UBLoader {

    //Attaches events from core
	[WireUpBaseEvents]
    
	[FriendlyName("UtilityBelt")]
	public class PluginCore : PluginBase {
        public object PluginInstance;
        public Assembly CurrentAssembly;
        public Type PluginType;
        public FileSystemWatcher PluginWatcher = null;

        private bool needsReload = false;
        private DateTime lastFileChange = DateTime.UtcNow;

        public string PluginAssemblyName { get { return "UtilityBelt.dll"; } }
        public string PluginAssemblyPath {
            get {
                string fullPath = System.Reflection.Assembly.GetAssembly(typeof(PluginCore)).Location;
                return System.IO.Path.GetDirectoryName(fullPath);
            }
        }

        public bool IsLoggedIn = false;

        /// <summary>
        /// This is called when the plugin is started up. This happens only once.
        /// </summary>
        protected override void Startup() {
			try {
                Core.RenderFrame += Core_RenderFrame;
                Core.CharacterFilter.LoginComplete += CharacterFilter_LoginComplete;

                LoadPluginAssembly();
                PluginWatcher = new FileSystemWatcher();
                PluginWatcher.Path = PluginAssemblyPath;
                PluginWatcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite;
                PluginWatcher.Filter = PluginAssemblyName;
                PluginWatcher.Changed += PluginWatcher_Changed; ;
                PluginWatcher.EnableRaisingEvents = true;
            }
			catch (Exception ex) { LogException(ex); }
        }

        private void CharacterFilter_LoginComplete(object sender, EventArgs e) {
            try {
                IsLoggedIn = true;
            }
            catch (Exception ex) { LogException(ex); }
        }

        private void Core_RenderFrame(object sender, EventArgs e) {
            try {
                if (needsReload && DateTime.UtcNow - lastFileChange > TimeSpan.FromSeconds(3)) {
                    needsReload = false;
                    Core.Actions.AddChatText("Reloading UtilityBelt", 1);
                    UnloadPluginAssembly();
                    LoadPluginAssembly();
                }
            }
            catch (Exception ex) { LogException(ex); }
        }

        private void PluginWatcher_Changed(object sender, FileSystemEventArgs e) {
            try {
                needsReload = true;
                lastFileChange = DateTime.UtcNow;
            }
            catch (Exception ex) { LogException(ex); }
        }

        private void LoadPluginAssembly() {
            try {
                var assemblyPath = System.IO.Path.Combine(PluginAssemblyPath, PluginAssemblyName);
                CurrentAssembly = Assembly.Load(File.ReadAllBytes(assemblyPath));
                PluginType = CurrentAssembly.GetType("UtilityBelt.UtilityBeltPlugin");
                MethodInfo startupMethod = PluginType.GetMethod("Startup");
                PluginInstance = Activator.CreateInstance(PluginType);
                startupMethod.Invoke(PluginInstance, new object[] { assemblyPath, Host, Core });

                if (IsLoggedIn) {
                    MethodInfo initMethod = PluginType.GetMethod("Init");
                    initMethod.Invoke(PluginInstance, null);
                }
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

        private string GetAssemblyDirectory() {
            string fullPath = System.Reflection.Assembly.GetAssembly(typeof(PluginCore)).Location;

            return System.IO.Path.GetDirectoryName(fullPath);
        }

        /// <summary>
        /// This is called when the plugin is shut down. This happens only once.
        /// </summary>
        protected override void Shutdown() {
			try {
                Core.RenderFrame -= Core_RenderFrame;
                Core.CharacterFilter.LoginComplete -= CharacterFilter_LoginComplete;
                UnloadPluginAssembly();
            }
			catch (Exception ex) { LogException(ex); }
        }

        public static void LogException(Exception ex) {
            try {
                var path = System.IO.Path.Combine(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Decal Plugins"), "UtilityBelt");
                using (StreamWriter writer = new StreamWriter(System.IO.Path.Combine(path, "exceptions.txt"), true)) {
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
    }
}
