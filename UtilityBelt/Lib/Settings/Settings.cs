using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;

namespace UtilityBelt.Lib.Settings {
    public class OptionResult {
        public object Object;
        public object Parent;
        public PropertyInfo Property;
        public PropertyInfo ParentProperty;

        public OptionResult(object obj, PropertyInfo propertyInfo, object parent, PropertyInfo parentProperty) {
            Object = obj;
            Parent = parent;
            Property = propertyInfo;
            ParentProperty = parentProperty;
        }
    }

    public class Settings {
        public bool ShouldSave = false;

        public event EventHandler Changed;

        private Dictionary<string, OptionResult> optionResultCache = new Dictionary<string, OptionResult>();

        #region Public Properties
        public bool HasCharacterSettingsLoaded { get; set; } = false;

        // path to global plugin config
        public string DefaultCharacterSettingsFilePath {
            get {
                return Path.Combine(Util.AssemblyDirectory, "settings.default.json");
            }
        }
        #endregion


        public Settings() {
            try {
                //Load();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        internal OptionResult GetOptionProperty(string key) {
            try {
                if (optionResultCache.ContainsKey(key)) return optionResultCache[key];

                var parts = key.Split('.');
                object obj = UtilityBeltPlugin.Instance;
                PropertyInfo parentProp = null;
                PropertyInfo lastProp = null;
                object lastObj = obj;
                for (var i = 0; i < parts.Length; i++) {
                    if (obj == null) return null;

                    var found = false;
                    foreach (var prop in obj.GetType().GetProperties()) {
                        if (prop.Name.ToLower() == parts[i].ToLower()) {
                            parentProp = lastProp;
                            lastProp = prop;
                            lastObj = obj;
                            obj = prop.GetValue(obj, null);
                            found = true;
                            break;
                        }
                    }

                    if (!found) return null;
                }

                if (lastProp != null) {
                    var d = lastProp.GetCustomAttributes(typeof(DefaultValueAttribute), true);

                    if (d.Length > 0) {
                        optionResultCache[key] = new OptionResult(obj, lastProp, lastObj, parentProp);
                        return optionResultCache[key];
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return null;
        }

        internal object Get(string key) {
            try {
                var prop = GetOptionProperty(key);

                if (prop == null || prop.Property == null) {
                    Logger.Debug($"Get:prop is {key}: {prop == null}");
                    return "";
                }

                return prop.Property.GetValue(prop.Parent, null);
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return null;
        }

        public void EnableSaving() {
            ShouldSave = true;
        }

        public void DisableSaving() {
            ShouldSave = false;
        }

        // section setup / events
        internal void SetupSection(SectionBase section) {
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
                    JsonConvert.PopulateObject(File.ReadAllText(DefaultCharacterSettingsFilePath), UtilityBeltPlugin.Instance);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        // load character specific settings
        public void Load() {
            try {
                var path = Path.Combine(Util.GetCharacterDirectory(), "settings.json");

                DisableSaving();
                LoadDefaults();

                if (File.Exists(path)) {
                    JsonConvert.PopulateObject(File.ReadAllText(path), UtilityBeltPlugin.Instance);
                }
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
                if (!ShouldSave && !force) return;

                var json = JsonConvert.SerializeObject(UtilityBeltPlugin.Instance, Newtonsoft.Json.Formatting.Indented);
                var path = Path.Combine(Util.GetCharacterDirectory(), "settings.json");

                File.WriteAllText(path, json);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        #endregion

        internal string DisplayValue(string key, bool expandLists = false, object value = null) {
            try {
                var prop = GetOptionProperty(key);
                value = value ?? Get(key);

                if (prop == null) {
                    Logger.Error($"prop is null for {key}");
                    return "";
                }

                if (value.GetType().IsEnum) {
                    var supportsFlagsAttributes = prop.Property.GetCustomAttributes(typeof(SupportsFlagsAttribute), true);

                    if (supportsFlagsAttributes.Length > 0) {
                        return "0x" + ((uint)value).ToString("X8");
                    }
                    else {
                        return value.ToString();
                    }
                }
                else if (value.GetType() != typeof(string) && value.GetType().GetInterfaces().Contains(typeof(IEnumerable))) {
                    if (expandLists) {
                        var results = new List<string>();

                        foreach (var item in (IEnumerable)value) {
                            results.Add(DisplayValue(key, false, item));
                        }

                        return $"[{string.Join(",", results.ToArray())}]";
                    }
                    else {
                        return "[List]";
                    }
                }
                else if (prop.Property.Name.Contains("Color")) {
                    return "0x" + ((int)value).ToString("X8");
                }
                else {
                    return value.ToString();
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return "null";
        }

    }
}
