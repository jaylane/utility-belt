using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace UtilityBelt.Lib.Settings
{
    public abstract class SectionBase : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;

        [JsonIgnore]
        public string Name { get; set; } = "unknown";

        protected SectionBase parent;
        private Dictionary<string, object> propValues = new Dictionary<string, object>();

        public SectionBase(SectionBase parent) {
            this.parent = parent;
        }

        // get this setting's value.  this keeps track of if this settings has
        // been set, and if not returns a DefaultValueAttribute
        protected virtual object GetSetting(string propName) {
            try {
                var prop = GetType().GetProperty(propName);

                if (prop == null) return null;

                if (propValues.ContainsKey(prop.Name)) {
                    return propValues[prop.Name];
                }

                // no value has been set, so return the DefaultValue attribute value
                if (!propValues.ContainsKey(prop.Name)) {
                    var d = prop.GetCustomAttributes(typeof(DefaultValueAttribute), true);
                    if (d.Length == 1) {
                        var defaultValue = ((DefaultValueAttribute)d[0]).Value;
                        propValues.Add(prop.Name, defaultValue);

                        return defaultValue;
                    }
                }

                return null;
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return null;
        }

        // update this setting's value. this fires the PropertyChanged event with the name
        // of the property that was changed
        protected void UpdateSetting(string propName, object value, bool force=false) {
            try {
                var prop = GetType().GetProperty(propName);

                // skip if this value hasn't changed
                if (!force && GetSetting(propName).ToString() == value.ToString()) return;

                if (prop != null) {
                    if (propValues.ContainsKey(prop.Name)) {
                        if (propValues[prop.Name] == value) return;

                        propValues[prop.Name] = value;
                    }
                    else {
                        if (GetSetting(propName) == value) return;

                        propValues.Add(prop.Name, value);
                    }

                    OnPropertyChanged(propName);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        protected string GetAncestry() {
            var ancestry = "";
            try {
                var node = this;

                while (node != null) {
                    ancestry = node.Name + "." + ancestry;
                    node = node.parent;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return ancestry;
        }

        protected void OnPropertyChanged(string propName, bool direct=true) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

            var prop = GetType().GetProperty(propName);

            if (direct && prop != null && Globals.Settings != null && Globals.Settings.ShouldSave) {
                var name = $"{GetAncestry()}{propName}";
                Logger.Debug($"{name} = {Globals.Settings.DisplayValue(name, true)}");
            }

            if (parent != null) parent.OnPropertyChanged(Name, false);
        }
    }
}
