using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UtilityBelt.Lib.Settings {
    public abstract class DisplaySectionBase : SectionBase {
        private Dictionary<string, ColorToggleOption> toggleOptions = new Dictionary<string, ColorToggleOption>();

        public DisplaySectionBase(SectionBase parent) : base(parent) {
        }

        // get this setting's value.  if it has not been requested yet
        // we need to create it with defaults from the attributes
        new protected object GetSetting(string propName) {
            try {
                var prop = GetType().GetProperty(propName);

                if (prop == null) return new ColorToggleOption(this, false, 0);

                if (toggleOptions.ContainsKey(prop.Name)) {
                    return toggleOptions[prop.Name];
                }

                // no value has been set, so make a new displayoption instance
                if (!toggleOptions.ContainsKey(prop.Name)) {
                    var defaultEnabled = false;
                    var defaultColor = System.Drawing.Color.White.ToArgb();

                    var defaultEnabledAttr = prop.GetCustomAttributes(typeof(DefaultEnabledAttribute), true);
                    if (defaultEnabledAttr.Length == 1) {
                        defaultEnabled = ((DefaultEnabledAttribute)defaultEnabledAttr[0]).Enabled;
                    }

                    var defaultColorAttr = prop.GetCustomAttributes(typeof(DefaultColorAttribute), true);
                    if (defaultColorAttr.Length == 1) {
                        defaultColor = ((DefaultColorAttribute)defaultColorAttr[0]).Color;
                    }

                    var toggleOption = new ColorToggleOption(this, defaultEnabled, defaultColor);
                    toggleOption.Name = prop.Name;
                    toggleOptions.Add(prop.Name, toggleOption);

                    return toggleOption;
                }

                return null;
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return new ColorToggleOption(this, false, 0);
        }

        protected void UpdateSetting(string propName, ColorToggleOption value) {
            try {
                var prop = GetType().GetProperty(propName);

                if (prop != null) {
                    if (toggleOptions.ContainsKey(propName)) {
                        if (toggleOptions[prop.Name].Enabled == value.Enabled && toggleOptions[prop.Name].Color == value.Color) return;
                        toggleOptions[propName] = value;
                    }
                    else {
                        toggleOptions.Add(propName, value);
                    }

                    OnPropertyChanged(propName);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
    }
}
