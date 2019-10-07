using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UtilityBelt.Lib.Settings {
    public abstract class SectionBase : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;

        [JsonIgnore]
        public string Name { get; protected set; } = "unknown";

        private bool hasValueSet = false;
        private bool internallySettingValue = false;

        protected object GetSetting() {
            try {
                var propName = new StackFrame(1).GetMethod().Name.Substring(4);
                var prop = GetType().GetProperty(propName);

                if (prop == null) return null;

                // no value has been set, so return the DefaultValue attribute value
                if (!hasValueSet) {
                    var d = prop.GetCustomAttributes(typeof(DefaultValueAttribute), true);
                    if (d.Length == 1) {
                        return ((DefaultValueAttribute)d[0]).Value;
                    }
                }

                return prop.GetValue(this, null);
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return null;
        }

        protected void UpdateSetting(object value) {
            try {
                if (internallySettingValue) return;

                var propName = new StackFrame(1).GetMethod().Name.Substring(4);
                var prop = GetType().GetProperty(propName);

                if (prop != null) {
                    try {
                        if (Globals.Settings != null && Globals.Settings.Debug) {
                            Util.WriteToChat($"Setting: {Name}.{propName} = {value.ToString()}");
                        }

                        internallySettingValue = true;
                        prop.SetValue(this, value, null);
                        hasValueSet = true;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
                    }
                    finally {
                        internallySettingValue = false;
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
    }
}
