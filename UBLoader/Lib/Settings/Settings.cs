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
using Hellosam.Net.Collections;
using System.Collections.ObjectModel;

namespace UBLoader.Lib.Settings {
    public class Settings : IDisposable {
        public bool ShouldSave = true;

        public event EventHandler<SettingChangedEventArgs> Changed;

        private Dictionary<string, OptionResult> optionResultCache = new Dictionary<string, OptionResult>();
        private Dictionary<string, ISetting> settingLookupCache = new Dictionary<string, ISetting>();
        private IEnumerable<FieldInfo> SettingFieldInfos;
        private FileSystemWatcher settingsFileWatcher = null;
        private TimerClass fileTimer = null;
        private double lastSettingsChange = 0;
        public static BindingFlags BindingFlags { get => BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static; }
        private string _settingsPath;
        public JsonSerializerSettings SerializerSettings;

        #region Public Properties
        public bool IsLoaded { get; set; } = false;
        public bool EventsEnabled { get; set; }
        public string Name { get; }
        public object Parent { get; }
        public string InitialSettingsPath { get; }
        public string SettingsPath {
            get => _settingsPath;
            set {
                if (_settingsPath == value)
                    return;
                if (NeedsSave)
                    Save();
                _settingsPath = value;
                if (IsLoaded)
                    Load();
            }
        }
        public string DefaultSettingsPath { get; private set; }
        public Func<ISetting, bool> ShouldSerializeCheck { get; private set; }
        public bool IsLoading { get; private set; }
        public bool NeedsSave { get; private set; }
        public bool NeedsLoad { get; private set; }
        #endregion


        public Settings(object parent, string settingsPath, Func<ISetting, bool> serializeTest=null, string defaultSettingsPath=null, string name="") {
            Name = name;
            Parent = parent;
            InitialSettingsPath = settingsPath;
            SettingsPath = settingsPath;
            DefaultSettingsPath = defaultSettingsPath;
            ShouldSerializeCheck = serializeTest;
            Changed += Settings_Changed;

            SerializerSettings = new JsonSerializerSettings() {
                TypeNameHandling = TypeNameHandling.All,
                TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
                SerializationBinder = new SerializationBinder()
            };
        }

        #region Event Handlers
        private void FileTimer_Timeout(Decal.Interop.Input.Timer Source) {
            try {
                if (NeedsSave && UBHelper.Core.Uptime - lastSettingsChange > 1)
                    Save();

                if (!NeedsSave)
                    fileTimer.Stop();
            }
            catch (Exception ex) { FilterCore.LogException(ex); }
        }

        private void Settings_Changed(object sender, SettingChangedEventArgs e) {
            if (ShouldSave && IsLoaded && !IsLoading) {
                if (ShouldSerializeCheck == null || ShouldSerializeCheck(e.Setting)) {
                    NeedsSave = true;
                    lastSettingsChange = UBHelper.Core.Uptime;
                    if (!fileTimer.Running)
                        fileTimer.Start(1000);
                }
            }
        }
        #endregion Event Handlers

        #region util
        private void Setup(FieldInfo field, object parent, string history = "") {
            var name = string.IsNullOrEmpty(history) ? field.Name : $"{history}.{field.Name}";
            var setting = (ISetting)field.GetValue(parent);
            IEnumerable<FieldInfo> childFields;

            if (setting.SettingType == SettingType.Unknown) {
                if (parent is ISetting && ((ISetting)parent).SettingType != SettingType.Unknown)
                    setting.SettingType = ((ISetting)parent).SettingType;
                else
                    setting.SettingType = Parent.GetType() == typeof(UBLoader.FilterCore) ? SettingType.Global : SettingType.Profile;
            }

            if (setting.Settings == null && (ShouldSerializeCheck == null || ShouldSerializeCheck(setting))) {
                setting.Settings = this;
            }

            setting.SetName(name);
            setting.FieldInfo = field;
            if (parent is ISetting psetting)
                setting.Parent = psetting;

            var summary = field.GetCustomAttributes(typeof(SummaryAttribute), false).FirstOrDefault();
            if (summary != null)
                setting.Summary = ((SummaryAttribute)summary).Summary;

            childFields = setting.GetType().GetFields(BindingFlags)
                                .Where(f => typeof(ISetting).IsAssignableFrom(f.FieldType));

            settingLookupCache.Add(name.ToLower(), setting);

            if (childFields.Count() == 0) {
                if (!setting.IsContainer && (ShouldSerializeCheck == null || ShouldSerializeCheck(setting)))
                    optionResultCache.Add(name.ToLower(), new OptionResult(setting, field, parent));
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

        public bool Exists(string key) {
            return optionResultCache.ContainsKey(key.ToLower());
        }

        public OptionResult Get(string key) {
            if (Exists(key.ToLower()))
                return optionResultCache[key.ToLower()];
            else
                return null;
        }

        public ISetting GetSetting(string key) {
            if (settingLookupCache.ContainsKey(key.ToLower()))
                return settingLookupCache[key.ToLower()];
            else
                return null;
        }

        public void EnableSaving() {
            ShouldSave = true;
        }

        public void DisableSaving() {
            ShouldSave = false;
        }
        #endregion Public API

        #region Saving / Loading
        // load default plugin settings
        private List<string> LoadDefaults() {
            if (!string.IsNullOrEmpty(DefaultSettingsPath) && System.IO.File.Exists(DefaultSettingsPath)) {
                try {
                    var token = JObject.Parse(System.IO.File.ReadAllText(DefaultSettingsPath));
                    var settings = Deserialize(token, Parent, "");
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
                LoadDefaults();
                LoadSettings();
            }
            finally {
                IsLoaded = true;
                IsLoading = false;
                EventsEnabled = true;
            }
        }

        private void LoadSettings() {
            try {
                List<string> deserializedSettings = new List<string>();

                if (settingsFileWatcher != null && (settingsFileWatcher.Path != Path.GetDirectoryName(SettingsPath) || settingsFileWatcher.Filter != Path.GetFileName(SettingsPath))) {
                    settingsFileWatcher.Dispose();
                    settingsFileWatcher = null;
                }

                if (settingsFileWatcher == null && !string.IsNullOrEmpty(SettingsPath)) {
                    settingsFileWatcher = new FileSystemWatcher();
                    settingsFileWatcher.Path = Path.GetDirectoryName(SettingsPath);
                    settingsFileWatcher.NotifyFilter = NotifyFilters.LastWrite;
                    settingsFileWatcher.Filter = Path.GetFileName(SettingsPath);
                    settingsFileWatcher.Changed += (s, e) => {
                        try {
                            DisableSaving();
                            LoadSettings();
                        }
                        catch (Exception ex) {
                            FilterCore.LogException(ex);
                        }
                        finally {
                            EnableSaving();
                        }
                    };
                    settingsFileWatcher.EnableRaisingEvents = true;
                }

                if (System.IO.File.Exists(SettingsPath)) {
                    var settings = Deserialize(JObject.Parse(File.ReadAllText(SettingsPath)), Parent, "");
                    deserializedSettings.AddRange(settings);
                }

                if (IsLoaded) {
                    EventsEnabled = true;
                    // on reload, ensure settings no longer in the json are reset to default
                    foreach (var kv in optionResultCache) {
                        if (!deserializedSettings.Contains(kv.Value.Setting.FullName) && !kv.Value.Setting.IsContainer) {
                            kv.Value.Setting.SetValue(kv.Value.Setting.GetDefaultValue());
                        }
                    }
                }

                NeedsLoad = false;
            }
            catch (Exception ex) {
                FilterCore.LogError($"Unable to load settings from: {SettingsPath}");
                FilterCore.LogException(ex);
            }
        }

        public void Save() {
            try {
                if (!ShouldSave || IsLoading || string.IsNullOrEmpty(SettingsPath))
                    return;
                settingsFileWatcher.EnableRaisingEvents = false;

                var jObj = new JObject();
                IEnumerable<FieldInfo> settings = GetSettingFieldsFromParent();
                foreach (var setting in settings) {
                    Serialize(jObj, Parent, (ISetting)setting.GetValue(Parent));
                }
                var json = jObj.ToString();
                File.TryWrite(SettingsPath, json, false);
                NeedsSave = false;
            }
            catch (Exception ex) {
                FilterCore.LogException(ex);
            }
            finally {
                settingsFileWatcher.EnableRaisingEvents = true;
            }
        }
        #endregion  Saving / Loading

        #region Serialization
        private List<string> Deserialize(JToken jToken, object setting, string path) {
            var deserializedSettings = new List<string>();
            if (jToken.Type == JTokenType.Object) {
                if (setting is ISetting && !((ISetting)setting).IsContainer) {
                    var dict = ((ISetting)setting).GetValue() as ObservableDictionary<string, string>;
                    deserializedSettings.Add(((ISetting)setting).FullName);
                    dict.Clear();
                    foreach (var kv in (JObject)jToken) {
                        dict.Add(kv.Key, kv.Value.ToString());
                    }
                }
                else {
                    foreach (var kv in (JObject)jToken) {
                        var field = setting.GetType().GetField(kv.Key, BindingFlags);
                        if (field != null && typeof(ISetting).IsAssignableFrom(field.FieldType)) {
                            var newHistory = $"{(string.IsNullOrEmpty(path) ? "" : path + ".")}{field.Name}";
                            var settings = Deserialize(kv.Value, ((ISetting)field.GetValue(setting)), newHistory);
                            deserializedSettings.AddRange(settings);
                        }
                    }
                }
            }
            else if (jToken.Type == JTokenType.Array && setting is ISetting) {
                var value = ((ISetting)setting).GetValue();
                var collection = value as IList;
                if (collection != null) {
                    Type typeParameter = value.GetType().GetGenericArguments().Single();
                    var eventsEnabled = EventsEnabled;
                    EventsEnabled = false;
                    collection.Clear();
                    foreach (var item in (JArray)jToken) {
                        if (typeParameter.GetConstructor(new Type[0]) != null) {
                            object newInstance = Activator.CreateInstance(typeParameter);
                            JsonConvert.PopulateObject(item.ToString(), newInstance, SerializerSettings);
                            collection.Add(newInstance);
                        }
                        else {
                            if (item.Type == JTokenType.Integer)
                                collection.Add(item.ToObject<int>());
                            else if (item.Type == JTokenType.Float)
                                collection.Add(item.ToObject<double>());
                            else if (item.Type == JTokenType.Boolean)
                                collection.Add(item.ToObject<bool>());
                            else if (item.Type == JTokenType.String)
                                collection.Add(item.ToObject<string>());
                        }
                    }
                    EventsEnabled = eventsEnabled;
                    deserializedSettings.Add(((ISetting)setting).FullName);
                    ((ISetting)setting).InvokeChange();
                }
            }
            else if (ShouldSerializeCheck == null || ShouldSerializeCheck((ISetting)setting)) {
                deserializedSettings.Add(((ISetting)setting).FullName);
                ((ISetting)setting).SetValue(jToken);
            }

            return deserializedSettings;
        }

        private bool Serialize(JObject jObj, object parent, ISetting setting) {
            if (!setting.HasChanges(ShouldSerializeCheck))
                return false;

            if (setting.HasChildren()) {
                var children = setting.GetChildren();
                var cObj = new JObject();
                foreach (var child in children) {
                    Serialize(cObj, setting.GetValue(), child);
                }
                jObj.Add(setting.Name, cObj);
            }
            else if (ShouldSerializeCheck == null || ShouldSerializeCheck(setting)) {
                if (setting.GetValue() is IList) {
                    var collection = setting.GetValue() as IList;
                    var jArray = new JArray();
                    foreach (var item in collection) {
                        // ugly... but not sure how else to get type definitions serialized
                        var json = JsonConvert.SerializeObject(item, SerializerSettings);
                        jArray.Add(JToken.Parse(json));
                    }
                    jObj.Add(setting.Name, jArray);
                }
                else if (setting.GetValue() is IDict) {
                    var dict = setting.GetValue() as ObservableDictionary<string, string>;
                    var dObj = new JObject();
                    foreach (var key in dict.Keys) {
                        dObj.Add(key, dict[key]);
                    }
                    jObj.Add(setting.Name, dObj);
                }
                else {
                    // ugly... but not sure how else to get type definitions serialized
                    var json = JsonConvert.SerializeObject(setting.GetValue(), SerializerSettings);
                    jObj.Add(setting.Name, JToken.Parse(json));
                }
            }
            return true;
        }
        #endregion Serialization

        internal void InvokeChange(ISetting setting, SettingChangedEventArgs eventArgs) {
            if (!EventsEnabled || setting.FullName != eventArgs.FullName)
                return;
            Changed?.Invoke(setting, eventArgs);
        }

        public void Dispose() {
            if (settingsFileWatcher != null) settingsFileWatcher.Dispose();
            Changed -= Settings_Changed;
            if (fileTimer.Running)
                fileTimer.Stop();
        }
    }
}
