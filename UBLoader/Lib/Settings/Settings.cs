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
        private FileSystemWatcher settingsFileWatcher = null;
        private FileSystemWatcher stateFileWatcher = null;
        private TimerClass fileTimer = null;
        private double lastStateChange = 0;
        private double lastSettingsChange = 0;
        private Func<ISetting, bool> isSettingLambda;
        private Func<ISetting, bool> isStateLambda;

        public static BindingFlags BindingFlags { get => BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static; }

        #region Public Properties
        public bool IsLoaded { get; set; } = false;
        public bool EventsEnabled { get; set; }
        public object Parent { get; }
        public string SettingsPath { get; }
        public string DefaultSettingsPath { get; }
        public string CharacterStatePath { get; }
        public bool IsLoading { get; private set; }
        public bool NeedsSettingsSave { get; private set; }
        public bool NeedsSettingsLoad { get; private set; }
        public bool NeedsStateLoad { get; private set; }
        public bool NeedsStateSave { get; private set; }
        #endregion


        public Settings(object parent, string settingsPath, string defaultSettingsPath=null, string characterStatePath=null) {
            isSettingLambda = s => !s.IsCharacterState;
            isStateLambda = s => s.IsCharacterState;

            Parent = parent;
            SettingsPath = settingsPath;
            DefaultSettingsPath = defaultSettingsPath;
            CharacterStatePath = characterStatePath;
            Changed += Settings_Changed;
        }

        #region Event Handlers
        private void FileTimer_Timeout(Decal.Interop.Input.Timer Source) {
            try {
                if (NeedsSettingsSave && UBHelper.Core.Uptime - lastSettingsChange > 1)
                    SaveSettings();
                if (NeedsStateSave && UBHelper.Core.Uptime - lastStateChange > 1)
                    SaveState();

                if (!NeedsSettingsSave && !NeedsStateSave) {
                    fileTimer.Stop();
                }
            }
            catch (Exception ex) { FilterCore.LogException(ex); }
        }

        private void Settings_Changed(object sender, SettingChangedEventArgs e) {
            if (ShouldSave) {
                if (sender is ISetting && ((ISetting)sender).IsCharacterState) {
                    NeedsStateSave = true;
                    lastStateChange = UBHelper.Core.Uptime;
                }
                else {
                    NeedsSettingsSave = true;
                    lastSettingsChange = UBHelper.Core.Uptime;
                }
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

        #region Public API
        public List<ISetting> GetAll() {
            var results = new List<ISetting>();

            foreach (var kv in optionResultCache) {
                results.Add(kv.Value.Setting);
            }

            return results;
        }

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
        #endregion Public API

        #region Saving / Loading
        // load default plugin settings
        private List<string> LoadDefaults() {
            if (!string.IsNullOrEmpty(DefaultSettingsPath) && System.IO.File.Exists(DefaultSettingsPath)) {
                try {
                    var token = JObject.Parse(System.IO.File.ReadAllText(DefaultSettingsPath));
                    var settings = Deserialize(token, Parent, "", isSettingLambda);
                    return settings;
                }
                catch (Exception ex) {
                    FilterCore.LogError($"Unable to load default settings from: {DefaultSettingsPath}");
                    FilterCore.LogException(ex);
                }
            }
            return new List<string>();
        }

        public void Load() {
            try {
                if (!IsLoaded) {
                    EventsEnabled = false;
                    IEnumerable<FieldInfo> settings = GetSettingFieldsFromParent();
                    foreach (var setting in settings) {
                        Setup(setting, Parent);
                    }

                    fileTimer = new TimerClass();
                    fileTimer.Timeout += FileTimer_Timeout;
                }

                IsLoading = true;
                DisableSaving();
                LoadDefaults();
                LoadSettings();
                LoadState();
            }
            finally {
                IsLoaded = true;
                IsLoading = false;
                EventsEnabled = true;
                EnableSaving();
            }
        }

        private void LoadState() {
            Load(CharacterStatePath, isStateLambda, ref stateFileWatcher);
            NeedsStateLoad = false;
        }

        private void LoadSettings() {
            Load(SettingsPath, isSettingLambda, ref settingsFileWatcher);
            NeedsSettingsLoad = false;
        }

        private void Load(string path, Func<ISetting, bool> shouldDeserialize) {
            var temp = new FileSystemWatcher();
            Load(path, shouldDeserialize, ref temp);
            temp.Dispose();
        }

        private void Load(string path, Func<ISetting, bool> shouldDeserialize, ref FileSystemWatcher watcher) {
            try {
                List<string> deserializedSettings = new List<string>();

                if (!IsLoaded && !string.IsNullOrEmpty(path)) {
                    watcher = new FileSystemWatcher();
                    watcher.Path = Path.GetDirectoryName(path);
                    watcher.NotifyFilter = NotifyFilters.LastWrite;
                    watcher.Filter = Path.GetFileName(path);
                    watcher.Changed += (s, e) => {
                        try {
                            DisableSaving();
                            if (path == CharacterStatePath)
                                LoadState();
                            else
                                LoadSettings();
                        }
                        catch (Exception ex) {
                            FilterCore.LogException(ex);
                        }
                        finally {
                            EnableSaving();
                        }
                    };
                    watcher.EnableRaisingEvents = true;
                }

                if (System.IO.File.Exists(path)) {
                    var settings = Deserialize(JObject.Parse(File.ReadAllText(path)), Parent, "", shouldDeserialize);
                    deserializedSettings.AddRange(settings);

                    if (IsLoaded) {
                        // on reload, ensure settings no longer in the json are reset to default
                        foreach (var kv in optionResultCache) {
                            if (!deserializedSettings.Contains(kv.Value.Setting.FullName) && !kv.Value.Setting.IsContainer) {
                                kv.Value.Setting.SetValue(kv.Value.Setting.GetDefaultValue());
                            }
                        }
                    }
                }
            }
            catch (Exception ex) {
                FilterCore.LogError($"Unable to load settings from: {path}");
                FilterCore.LogException(ex);
            }
        }

        private List<string> Deserialize(JToken jToken, object setting, string path, Func<ISetting, bool> shouldDeserialize) {
            var deserializedSettings = new List<string>();
            if (jToken.Type == JTokenType.Object) {
                foreach (var kv in (JObject)jToken) {
                    var field = setting.GetType().GetField(kv.Key, BindingFlags);
                    if (field != null && typeof(ISetting).IsAssignableFrom(field.FieldType)) {
                        var newHistory = $"{(string.IsNullOrEmpty(path) ? "" : path + ".")}{field.Name}";
                        var settings = Deserialize(kv.Value, ((ISetting)field.GetValue(setting)), newHistory, shouldDeserialize);
                        deserializedSettings.AddRange(settings);
                    }
                }
            }
            else if (shouldDeserialize != null && shouldDeserialize((ISetting)setting)) {
                deserializedSettings.Add(((ISetting)setting).FullName);
                ((ISetting)setting).SetValue(jToken);
            }

            return deserializedSettings;
        }

        public void SaveState() {
            try {
                stateFileWatcher.EnableRaisingEvents = false;
                Save(CharacterStatePath, isStateLambda);
                NeedsStateSave = false;
            }
            catch (Exception ex) {
                FilterCore.LogException(ex);
            }
            finally {
                stateFileWatcher.EnableRaisingEvents = true;
            }
        }

        public void SaveSettings() {
            try {
                settingsFileWatcher.EnableRaisingEvents = false;
                Save(SettingsPath, isSettingLambda);
                NeedsSettingsSave = false;
            }
            catch (Exception ex) {
                FilterCore.LogException(ex);
            }
            finally {
                settingsFileWatcher.EnableRaisingEvents = true;
            }
        }

        private void Save(string path, Func<ISetting, bool> shouldSerialize) {
            try {
                if (!ShouldSave || string.IsNullOrEmpty(path))
                    return;

                var jObj = new JObject();
                IEnumerable<FieldInfo> settings = GetSettingFieldsFromParent();
                foreach (var setting in settings) {
                    Serialize(jObj, setting, Parent, (ISetting)setting.GetValue(Parent), shouldSerialize);
                }
                var json = jObj.ToString();
                File.TryWrite(path, json, false);
            }
            catch (Exception ex) {
                FilterCore.LogException(ex);
            }
        }

        private bool Serialize(JObject jObj, FieldInfo field, object parent, ISetting setting, Func<ISetting, bool> serializeCheck) {
            if (!setting.HasChanges(serializeCheck))
                return false;

            if (setting.HasChildren()) {
                var children = setting.GetChildren();
                var cObj = new JObject();
                foreach (var child in children) {
                    var childSetting = (ISetting)child.GetValue(setting.GetValue());
                    Serialize(cObj, child, setting.GetValue(), childSetting, serializeCheck);
                }
                jObj.Add(field.Name, cObj);
            }
            else if (serializeCheck == null || serializeCheck(setting)) {
                jObj.Add(field.Name, JToken.FromObject(setting.GetValue()));
            }
            return true;
        }
        #endregion

        internal void InvokeChange(ISetting setting, SettingChangedEventArgs eventArgs) {
            if (!EventsEnabled || setting.FullName != eventArgs.FullName)
                return;
            Changed?.Invoke(setting, eventArgs);
        }

        public void Dispose() {
            if (settingsFileWatcher != null) settingsFileWatcher.Dispose();
            if (stateFileWatcher != null) stateFileWatcher.Dispose();
            Changed -= Settings_Changed;
            if (fileTimer.Running)
                fileTimer.Stop();
        }
    }
}
