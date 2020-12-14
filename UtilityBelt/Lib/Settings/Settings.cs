using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace UtilityBelt.Lib.Settings {
    public class Settings : IDisposable {
        public bool ShouldSave = false;

        public event EventHandler<SettingChangedEventArgs> Changed;

        private Dictionary<string, OptionResult> optionResultCache = new Dictionary<string, OptionResult>();
        private JsonSerializerSettings serializerSettings;

        #region Public Properties
        public bool HasCharacterSettingsLoaded { get; set; } = false;

        // path to global plugin config
        public string DefaultCharacterSettingsFilePath {
            get {
                return Path.Combine(Util.AssemblyDirectory, "settings.default.json");
            }
        }

        public bool NeedsSave { get; private set; }
        public double LastSettingsChange { get; private set; }
        public bool EventsEnabled { get; set; }
        #endregion


        public Settings() {
            Changed += Settings_Changed;
        }

        #region Event Handlers
        private void Core_RadarUpdate(double uptime) {
            if (NeedsSave && uptime - LastSettingsChange > 1) {
                NeedsSave = false;
                Save();
            }
        }

        private void Settings_Changed(object sender, EventArgs e) {
            if (ShouldSave && !NeedsSave) {
                NeedsSave = true;
                UBHelper.Core.RadarUpdate += Core_RadarUpdate;
                LastSettingsChange = UBHelper.Core.Uptime;
            }
        }
        #endregion Event Handlers

        #region util
        private void Setup(FieldInfo field, object parent, string history = "") {
            var name = string.IsNullOrEmpty(history) ? field.Name : $"{history}.{field.Name}";
            var setting = (ISetting)field.GetValue(parent);
            IEnumerable<FieldInfo> childFields;

            if (typeof(ISetting).IsAssignableFrom(parent.GetType())) {
                setting.Changed += (s, e) => {
                    ((ISetting)parent).InvokeChange((ISetting)parent, e);
                };
            }

            childFields = setting.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)
                                .Where(f => typeof(ISetting).IsAssignableFrom(f.FieldType));

            if (childFields.Count() == 0 && !(setting is ToolBase)) {
                ((ISetting)field.GetValue(parent)).SetName(name);
                optionResultCache.Add(name, new OptionResult(setting, field, parent));
            }
            else {
                foreach (var childField in childFields) {
                    Setup(childField, setting, name);
                }
            }
        }

        static bool IsSubclassOfRawGeneric(Type generic, Type toCheck) {
            while (toCheck != null && toCheck != typeof(object)) {
                var cur = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
                if (generic == cur) {
                    return true;
                }
                toCheck = toCheck.BaseType;
            }
            return false;
        }
        #endregion util

        internal OptionResult Get(string key) {
            if (optionResultCache.ContainsKey(key))
                return optionResultCache[key];
            else
                return null;
        }

        public void EnableSaving() {
            ShouldSave = true;
        }

        public void DisableSaving() {
            ShouldSave = false;
        }

        #region Saving / Loading
        private JsonSerializerSettings GetSerializerSettings() {
            if (serializerSettings == null) {
                serializerSettings = new JsonSerializerSettings();
                serializerSettings.ContractResolver = new ShouldSerializeContractResolver();
            }

            return serializerSettings;
        }

        internal List<ISetting> GetAll() {
            var results = new List<ISetting>();

            foreach (var kv in optionResultCache) {
                results.Add(kv.Value.Setting);
            }

            return results;
        }

        // load default plugin settings
        private void LoadDefaults() {
            if (File.Exists(DefaultCharacterSettingsFilePath)) {
                try {
                    JsonConvert.PopulateObject(File.ReadAllText(DefaultCharacterSettingsFilePath), UtilityBeltPlugin.Instance, GetSerializerSettings());
                }
                catch (Exception ex) {
                    Logger.LogException(ex);
                    Logger.WriteToChat("Unable to load settings file: " + DefaultCharacterSettingsFilePath);
                }
            }
        }

        // load character specific settings
        public void Load() {
            try {
                EventsEnabled = false;
                var path = Path.Combine(Util.GetCharacterDirectory(), "settings.json");

                var tools = UtilityBeltPlugin.Instance.GetToolInfos();
                foreach (var tool in tools) {
                    Setup(tool, UtilityBeltPlugin.Instance);
                }

                DisableSaving();
                LoadDefaults();

                if (File.Exists(path)) {
                    try {
                        JsonConvert.PopulateObject(File.ReadAllText(path), UtilityBeltPlugin.Instance, GetSerializerSettings());
                    }
                    catch (Exception ex) {
                        Logger.LogException(ex);
                        Logger.WriteToChat("Unable to load settings file: " + path);
                    }
                }
                EventsEnabled = true;
            }
            catch (Exception ex) { Logger.LogException(ex); }
            finally {
                // even if it fails to load... this is just for making sure
                // not to try to do stuff until we have settings loaded
                HasCharacterSettingsLoaded = true;
                EnableSaving();
            }
        }

        // save character specific settings
        public void Save(bool force = false) {
            try {
                if (!ShouldSave && !force)
                    return;

                var ub = UtilityBeltPlugin.Instance;
                var json = "";
                var toolInfos = ub.GetToolInfos();
                var jObj = new JObject();

                foreach (var tool in toolInfos) {
                    if (BuildSerializableSetting(jObj, tool, ub, (ISetting)tool.GetValue(ub))) {
                        NeedsSave = true;
                    }
                }

                json = jObj.ToString();

                var path = Path.Combine(Util.GetCharacterDirectory(), "settings.json");
                UBLoader.File.TryWrite(path, json, false);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private bool BuildSerializableSetting(JObject jObj, FieldInfo field, object parent, ISetting setting) {
            if (!setting.HasChanges())
                return false;

            if (setting.HasChildren()) {
                var children = setting.GetChildren();
                var cObj = new JObject();
                foreach (var child in children) {
                    var childSetting = (ISetting)child.GetValue(setting.GetValue());
                    BuildSerializableSetting(cObj, child, setting.GetValue(), childSetting);
                }
                jObj.Add(field.Name, cObj);
            }
            else {
                var j = JsonConvert.SerializeObject(setting, Newtonsoft.Json.Formatting.Indented, GetSerializerSettings());
                jObj.Add(field.Name, JToken.Parse(j));
            }
            return true;
        }
        #endregion

        internal string DisplayValue(string key, bool expandLists = false) {
            var prop = Get(key);

            if (prop == null) {
                Logger.Error($"prop is null for {key}");
                return "";
            }

            if (prop.Setting.GetValue().GetType().IsEnum) {
                var supportsFlagsAttributes = prop.FieldInfo.GetCustomAttributes(typeof(SupportsFlagsAttribute), true);

                if (supportsFlagsAttributes.Length > 0) {
                    return "0x" + ((uint)prop.Setting.GetValue()).ToString("X8");
                }
                else {
                    return prop.Setting.GetValue().ToString();
                }
            }
            else if (prop.Setting.GetValue().GetType() != typeof(string) && prop.Setting.GetValue().GetType().GetInterfaces().Contains(typeof(IEnumerable))) {
                if (expandLists) {
                    var results = new List<string>();

                    foreach (var item in (IEnumerable)(prop.Setting.GetValue())) {
                        results.Add(item.ToString());
                    }

                    return $"[{string.Join(",", results.ToArray())}]";
                }
                else {
                    return "[List]";
                }
            }
            else if (prop.Setting.GetValue().GetType() == typeof(int) && prop.FieldInfo.Name.Contains("Color")) {
                return "0x" + ((int)(prop.Setting.GetValue())).ToString("X8");
            }
            else {
                return prop.Setting.GetValue().ToString();
            }
        }

        internal void InvokeChange(ISetting setting, SettingChangedEventArgs eventArgs) {
            if (!EventsEnabled || !setting.HasParent || setting.FullName != eventArgs.FullName)
                return;
            Logger.Debug($"{setting.FullName} = {setting.GetValue()}");
            Changed?.Invoke(setting, eventArgs);
        }

        public void Dispose() {
            Changed -= Settings_Changed;
            UBHelper.Core.RadarUpdate -= Core_RadarUpdate;
        }
    }
}
