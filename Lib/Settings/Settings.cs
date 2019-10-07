using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using UtilityBelt.Lib.Settings.Sections;

namespace UtilityBelt.Lib.Settings {
    [JsonObject(MemberSerialization.OptIn)]
    public class Settings {
        private string pluginStorageDirectory;
        private bool shouldSave = false;

        public event EventHandler Changed;

        #region Public Properties
        public bool HasCharacterSettingsLoaded { get; set; } = false;

        [JsonProperty]
        [SummaryAttribute("Check for plugin updates on login")]
        public bool CheckForUpdates { get; private set; } = true;

        [JsonProperty]
        [SummaryAttribute("Show debug messages")]
        public bool Debug { get; private set; } = false;

        // no JsonProperty because we dont want to store this with each character,
        // todo: make sure this gets loaded in properly with PopulateObject from settings.default.json
        //[JsonProperty]
        [SummaryAttribute("Path where plugin will store all of its data")]
        public string PluginStorageDirectory {
            get {
                // read from plugin.json if available
                if (!string.IsNullOrEmpty(pluginStorageDirectory)) {
                    return pluginStorageDirectory;
                }

                // default is documents\decal plugins\<plugin name>\
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                var decalDocumentsPath = Path.Combine(documentsPath, "Decal Plugins");

                return Path.Combine(decalDocumentsPath, Globals.PluginName);
            }
            private set {
                pluginStorageDirectory = value;
            }
        }

        // path to global plugin config
        public string DefaultCharacterSettingsFilePath {
            get {
                return Path.Combine(Util.GetAssemblyDirectory(), "settings.default.json");
            }
        }

        // current character's storage path
        public string CharacterStoragePath {
            get {
                var path = Path.Combine(PluginStorageDirectory, Globals.Core.CharacterFilter.Server);
                return Path.Combine(path, Globals.Core.CharacterFilter.Name);
            }
        }
        #endregion

        #region Settings Sections
        [JsonProperty]
        [SummaryAttribute("AutoSalvage Settings")]
        public AutoSalvage AutoSalvage { get; set; }
        #endregion

        public Settings() {
            try {
                SetupSection(AutoSalvage = new AutoSalvage());

                Load();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        // section setup / events
        private void SetupSection(SectionBase section) {
            section.PropertyChanged += HandleSectionChange;
        }

        #region Events / Handlers
        // notify any subcribers that this has changed
        protected void OnChanged() {
            Changed?.Invoke(this, new EventArgs());
        }

        // called when one of the child sections has been changed
        private void HandleSectionChange(object sender, EventArgs e) {
            OnChanged();
            Save();
        }
        #endregion

        #region Saving / Loading
        // load default plugin settings
        private void LoadDefaults() {
            try {
                if (File.Exists(DefaultCharacterSettingsFilePath)) {
                    JsonConvert.PopulateObject(File.ReadAllText(DefaultCharacterSettingsFilePath), this);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        // load character specific settings
        public void Load() {
            try {
                var path = Path.Combine(CharacterStoragePath, "settings.json");

                shouldSave = false;
                LoadDefaults();
                LoadOldXML();

                if (File.Exists(path)) {
                    JsonConvert.PopulateObject(File.ReadAllText(path), this);
                }
                shouldSave = true;
            }
            catch (Exception ex) { Logger.LogException(ex); }
            finally {
                // even if it fails to load... this is just for making sure
                // not to try to do stuff until we have settings loaded
                HasCharacterSettingsLoaded = true;
            }
        }

        // save character specific settings
        public void Save() {
            try {
                if (!shouldSave) return;

                var json = JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
                var path = Path.Combine(CharacterStoragePath, "settings.json");

                File.WriteAllText(path, json);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        #endregion

        #region Old Mag-Tools style settings migration
        // load old mag-tools style xml config, this is just for migrating
        private void LoadOldXML() {
            try {
                var path = Path.Combine(CharacterStoragePath, "config.xml");

                if (!File.Exists(path)) return;

                XmlDocument doc = new XmlDocument();
                doc.Load(path);
                XmlNode config = doc.DocumentElement.SelectSingleNode("/UtilityBelt/Config");
                
                foreach (XmlNode node in config.ChildNodes) {
                    switch (node.Name) {
                        case "AutoSalvage":
                            AutoSalvage.Think = ParseOldBoolNode(node, "Think", AutoSalvage.Think);
                            AutoSalvage.OnlyFromMainPack = ParseOldBoolNode(node, "OnlyFromMainPack", AutoSalvage.OnlyFromMainPack);
                            break;

                        case "AutoVendor":
                            AutoSalvage.Think = ParseOldBoolNode(node, "Think", AutoSalvage.Think);
                            AutoSalvage.OnlyFromMainPack = ParseOldBoolNode(node, "OnlyFromMainPack", AutoSalvage.OnlyFromMainPack);
                            break;

                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private bool ParseOldBoolNode(XmlNode parentNode, string childTag, bool defaultValue) {
            try {
                XmlNode node = parentNode.SelectSingleNode($"{childTag}");
                if (node != null) {
                    return node.InnerText.ToLower().Trim() == "true";
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return defaultValue;
        }
        #endregion
    }
}
