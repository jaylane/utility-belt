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
        private Dictionary<string, ColorToggleOption> colorToggleOptions = new Dictionary<string, ColorToggleOption>();
        private Dictionary<string, MarkerToggleOption> markerToggleOptions = new Dictionary<string, MarkerToggleOption>();
        private Dictionary<string, PluginMessageDisplay> messageDisplayOptions = new Dictionary<string, PluginMessageDisplay>();

        public DisplaySectionBase(SectionBase parent) : base(parent) {
            InitProperties();
        }

        private void InitProperties() {
            try {
                var props = GetType().GetProperties();

                foreach (var prop in props) {
                    try {
                        var x = prop.GetValue(this, null);
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        // get this setting's value.  if it has not been requested yet
        // we need to create it with defaults from the attributes
        protected override object GetSetting(string propName) {
            try {
                var prop = GetType().GetProperty(propName);

                if (prop.PropertyType == typeof(ColorToggleOption)) {
                    return GetColorToggleOption(prop);
                }
                else if (prop.PropertyType == typeof(MarkerToggleOption)) {
                    return GetMarkerToggleOption(prop);
                }
                else if (prop.PropertyType == typeof(PluginMessageDisplay)) {
                    return GetPluginMessageDisplayOption(prop);
                }
                else {
                    return base.GetSetting(propName);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return null;
        }

        private PluginMessageDisplay GetPluginMessageDisplayOption(PropertyInfo prop) {
            if (prop == null) return null;

            if (messageDisplayOptions.ContainsKey(prop.Name)) {
                return messageDisplayOptions[prop.Name];
            }

            // no value has been set, so make a new ColorToggleOption instance
            if (!messageDisplayOptions.ContainsKey(prop.Name)) {
                var defaultEnabled = true;
                short defaultColor = 5;

                var defaultEnabledAttr = prop.GetCustomAttributes(typeof(DefaultEnabledAttribute), true);
                if (defaultEnabledAttr.Length == 1) {
                    defaultEnabled = ((DefaultEnabledAttribute)defaultEnabledAttr[0]).Enabled;
                }

                var defaultColorAttr = prop.GetCustomAttributes(typeof(DefaultChatColorAttribute), true);
                if (defaultColorAttr.Length == 1) {
                    defaultColor = ((DefaultChatColorAttribute)defaultColorAttr[0]).Color;
                }

                var messageOption = new PluginMessageDisplay(this, defaultEnabled, defaultColor);
                messageOption.Name = prop.Name;
                messageDisplayOptions.Add(prop.Name, messageOption);

                return messageOption;
            }

            return null;
        }

        private MarkerToggleOption GetMarkerToggleOption(PropertyInfo prop) {
            if (prop == null) return null;

            if (markerToggleOptions.ContainsKey(prop.Name)) {
                return markerToggleOptions[prop.Name];
            }

            // no value has been set, so make a new ColorToggleOption instance
            if (!markerToggleOptions.ContainsKey(prop.Name)) {
                var defaultEnabled = false;
                var defaultColor = System.Drawing.Color.White.ToArgb();
                var defaultShowLabel = false;
                var defaultUseIcon = true;
                var defaultSize = 4;

                var defaultEnabledAttr = prop.GetCustomAttributes(typeof(DefaultEnabledAttribute), true);
                if (defaultEnabledAttr.Length == 1) {
                    defaultEnabled = ((DefaultEnabledAttribute)defaultEnabledAttr[0]).Enabled;
                }

                var defaultColorAttr = prop.GetCustomAttributes(typeof(DefaultColorAttribute), true);
                if (defaultColorAttr.Length == 1) {
                    defaultColor = ((DefaultColorAttribute)defaultColorAttr[0]).Color;
                }

                var defaultShowLabelAttr = prop.GetCustomAttributes(typeof(DefaultShowLabelAttribute), true);
                if (defaultShowLabelAttr.Length == 1) {
                    defaultShowLabel = ((DefaultShowLabelAttribute)defaultShowLabelAttr[0]).Enabled;
                }

                var defaultUseIconAttr = prop.GetCustomAttributes(typeof(DefaultUseIconAttribute), true);
                if (defaultUseIconAttr.Length == 1) {
                    defaultUseIcon = ((DefaultUseIconAttribute)defaultUseIconAttr[0]).UseIcon;
                }

                var defaultSizeAttr = prop.GetCustomAttributes(typeof(DefaultSizeAttribute), true);
                if (defaultSizeAttr.Length == 1) {
                    defaultSize = ((DefaultSizeAttribute)defaultSizeAttr[0]).Size;
                }

                var toggleOption = new MarkerToggleOption(this, defaultEnabled, defaultUseIcon, defaultShowLabel, defaultColor, defaultSize);
                toggleOption.Name = prop.Name;
                markerToggleOptions.Add(prop.Name, toggleOption);

                return toggleOption;
            }

            return null;
        }

        private ColorToggleOption GetColorToggleOption(PropertyInfo prop) {
            if (prop == null) return null;

            if (colorToggleOptions.ContainsKey(prop.Name)) {
                return colorToggleOptions[prop.Name];
            }

            // no value has been set, so make a new ColorToggleOption instance
            if (!colorToggleOptions.ContainsKey(prop.Name)) {
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
                colorToggleOptions.Add(prop.Name, toggleOption);

                return toggleOption;
            }

            return null;
        }

        protected void UpdateSetting(string propName, ColorToggleOption value) {
            try {
                var prop = GetType().GetProperty(propName);

                if (prop != null) {
                    if (colorToggleOptions.ContainsKey(propName)) {
                        // todo isEqual
                        if (colorToggleOptions[prop.Name].Equals(value)) return;
                        colorToggleOptions[propName] = value;
                    }
                    else {
                        colorToggleOptions.Add(propName, value);
                    }

                    OnPropertyChanged(propName);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        protected void UpdateSetting(string propName, MarkerToggleOption value) {
            try {
                var prop = GetType().GetProperty(propName);

                if (prop != null) {
                    if (markerToggleOptions.ContainsKey(propName)) {
                        if (markerToggleOptions[prop.Name].Equals(value)) return;
                        markerToggleOptions[propName] = value;
                    }
                    else {
                        markerToggleOptions.Add(propName, value);
                    }

                    OnPropertyChanged(propName);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
    }
}
