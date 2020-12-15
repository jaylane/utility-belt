using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Decal.Interop.Input;

namespace UBLoader.Lib.Settings {
    public class Settings : IDisposable {
        public bool ShouldSave = false;

        public event EventHandler<SettingChangedEventArgs> Changed;

        private Dictionary<string, OptionResult> optionResultCache = new Dictionary<string, OptionResult>();
        private IEnumerable<FieldInfo> SettingFieldInfos;
        private FileSystemWatcher fileWatcher = null;
        private TimerClass fileTimer = null;

        #region Public Properties
        public bool IsLoaded { get; set; } = false;

        public bool NeedsSave { get; private set; }
        public double LastSettingsChange { get; private set; }
        public bool EventsEnabled { get; set; }
        public object Parent { get; }
        public string SettingsPath { get; }
        public string DefaultSettingsPath { get; }
        public static BindingFlags BindingFlags { get => BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static; }
        public bool NeedsLoad { get; private set; }
        public bool IsLoading { get; private set; }
        #endregion


        public Settings(object parent, string settingsPath, string defaultSettingsPath=null) {
            Parent = parent;
            SettingsPath = settingsPath;
            DefaultSettingsPath = defaultSettingsPath;
            Changed += Settings_Changed;
        }

        #region Event Handlers
        private void FileTimer_Timeout(Decal.Interop.Input.Timer Source) {
            try {
                if (NeedsLoad)
                    Load();
                if (NeedsSave)
                    Save();

                if (!NeedsSave && !NeedsLoad) {
                    fileTimer.Stop();
                }
            }
            catch (Exception ex) { FilterCore.LogException(ex); }
        }

        private void FileWatcher_Changed(object sender, FileSystemEventArgs e) {
            try {
                NeedsLoad = true;
                if (!fileTimer.Running)
                    fileTimer.Start(1000);
            }
            catch (Exception ex) { FilterCore.LogException(ex); }
        }

        private void Settings_Changed(object sender, EventArgs e) {
            if (ShouldSave) {
                NeedsSave = true;
                LastSettingsChange = UBHelper.Core.Uptime;
                if (!fileTimer.Running)
                    fileTimer.Start(1000);
            }
        }
        #endregion Event Handlers

        #region util
        private void Setup(FieldInfo field, object parent, string history = "") {
            var name = string.IsNullOrEmpty(history) ? field.Name : $"{history}.{field.Name}";
            var setting = (ISetting)field.GetValue(parent);
            IEnumerable<FieldInfo> childFields;

            setting.Settings = this;
            setting.FieldInfo = field;

            if (typeof(ISetting).IsAssignableFrom(parent.GetType())) {
                setting.Parent = (ISetting)parent;
                setting.Changed += (s, e) => {
                    ((ISetting)parent).InvokeChange((ISetting)parent, e);
                };
            }

            childFields = setting.GetType().GetFields(BindingFlags)
                                .Where(f => typeof(ISetting).IsAssignableFrom(f.FieldType));

            if (childFields.Count() == 0) {
                ((ISetting)field.GetValue(parent)).SetName(name);
                optionResultCache.Add(name, new OptionResult(setting, field, parent));
            }
            else {
                foreach (var childField in childFields) {
                    Setup(childField, setting, name);
                }
            }
        }

        private IEnumerable<FieldInfo> GetSettingFieldsFromParent() {
            if (SettingFieldInfos == null)
                SettingFieldInfos = Parent.GetType().GetFields(BindingFlags)
                    .Where(f => typeof(ISetting).IsAssignableFrom(f.FieldType));
            return SettingFieldInfos;
        }
        #endregion util

        public OptionResult Get(string key) {
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
        public List<ISetting> GetAll() {
            var results = new List<ISetting>();

            foreach (var kv in optionResultCache) {
                results.Add(kv.Value.Setting);
            }

            return results;
        }

        // load default plugin settings
        private List<string> LoadDefaults() {
            if (!string.IsNullOrEmpty(DefaultSettingsPath) && System.IO.File.Exists(DefaultSettingsPath)) {
                try {
                    var settings = DeserializeSettings(JObject.Parse(System.IO.File.ReadAllText(DefaultSettingsPath)), Parent);
                    return settings;
                }
                catch (Exception ex) {
                    FilterCore.LogError($"Unable to load default settings from: {DefaultSettingsPath}");
                    FilterCore.LogException(ex);
                }
            }
            return new List<string>();
        }

        // load character specific settings
        public void Load() {
            try {
                IsLoading = true;
                DisableSaving();

                List<string> deserializedSettings = new List<string>();

                if (!IsLoaded) {
                    fileTimer = new TimerClass();
                    fileTimer.Timeout += FileTimer_Timeout;

                    fileWatcher = new FileSystemWatcher();
                    fileWatcher.Path = Path.GetDirectoryName(SettingsPath);
                    fileWatcher.NotifyFilter = NotifyFilters.LastWrite;
                    fileWatcher.Filter = Path.GetFileName(SettingsPath);
                    fileWatcher.Changed += FileWatcher_Changed;
                    fileWatcher.EnableRaisingEvents = true;

                    EventsEnabled = false;
                    IEnumerable<FieldInfo> settings = GetSettingFieldsFromParent();
                    foreach (var setting in settings) {
                        Setup(setting, Parent);
                    }

                    deserializedSettings.AddRange(LoadDefaults());
                }

                if (System.IO.File.Exists(SettingsPath)) {
                    deserializedSettings.AddRange(DeserializeSettings(JObject.Parse(System.IO.File.ReadAllText(SettingsPath)), Parent));

                    if (IsLoaded) {
                        foreach (var kv in optionResultCache) {
                            if (!deserializedSettings.Contains(kv.Value.Setting.FullName) && !kv.Value.Setting.IsContainer) {
                                kv.Value.Setting.SetValue(kv.Value.Setting.GetDefaultValue());
                            }
                        }
                    }
                }
            }
            catch (Exception ex) {
                FilterCore.LogError($"Unable to load settings from: {SettingsPath}");
                FilterCore.LogException(ex);
            }
            finally {
                // even if it fails to load... this is just for making sure
                // not to try to do stuff until we have settings loaded
                NeedsLoad = false;
                IsLoaded = true;
                IsLoading = false;
                EventsEnabled = true;
                EnableSaving();
            }
        }

        private List<string> DeserializeSettings(JToken jToken, object setting, string path="") {
            var deserializedSettings = new List<string>();
            if (jToken.Type == JTokenType.Object) {
                foreach (var kv in (JObject)jToken) {
                    var field = setting.GetType().GetField(kv.Key, BindingFlags);
                    if (field != null && typeof(ISetting).IsAssignableFrom(field.FieldType)) {
                        var newHistory = $"{(string.IsNullOrEmpty(path) ? "" : path + ".")}{field.Name}";
                        deserializedSettings.AddRange(DeserializeSettings(kv.Value, ((ISetting)field.GetValue(setting)), newHistory));
                    }
                }
            }
            else {
                deserializedSettings.Add(((ISetting)setting).FullName);
                ((ISetting)setting).SetValue(jToken);
            }

            return deserializedSettings;
        }

        // save character specific settings
        public void Save(bool force = false) {
            try {
                if (!ShouldSave && !force)
                    return;

                var jObj = new JObject();
                IEnumerable<FieldInfo> settings = GetSettingFieldsFromParent();
                foreach (var setting in settings) {
                    BuildSerializableSetting(jObj, setting, Parent, (ISetting)setting.GetValue(Parent));
                }
                var json = jObj.ToString();
                UBLoader.File.TryWrite(SettingsPath, json, false);

                NeedsSave = false;
            }
            catch (Exception ex) {
                FilterCore.LogException(ex);
            }
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
                jObj.Add(field.Name, JToken.FromObject(setting.GetValue()));
            }
            return true;
        }
        #endregion

        public string DisplayValue(string key, bool expandLists = false) {
            var prop = Get(key);

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
            if (!EventsEnabled || setting.FullName != eventArgs.FullName)
                return;
            Changed?.Invoke(setting, eventArgs);
        }

        public void Dispose() {
            fileWatcher.Changed -= FileWatcher_Changed;
            fileWatcher.Dispose();
            Changed -= Settings_Changed;
            if (fileTimer.Running)
                fileTimer.Stop();
        }
    }
}
