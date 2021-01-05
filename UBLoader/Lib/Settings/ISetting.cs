using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Collections.ObjectModel;
using Newtonsoft.Json;

namespace UBLoader.Lib.Settings {
    public enum SettingType {
        Unknown,
        Global,
        Profile,
        State,

        ClientUI,
        SpellBars,
        CharacterSettings
    };

    public abstract class ISetting {
        public event EventHandler<SettingChangedEventArgs> Changed;

        public string FullName { get; protected set; } = "";
        public string Name => FullName.Split('.').Last();
        public bool IsContainer { get => GetValue() != null && typeof(ISetting).IsAssignableFrom(GetValue().GetType()); }
        public bool IsDefault { get => !HasChanges(p => true); }
        public ISetting Parent { get; internal set; } = null;
        public Settings Settings { get; internal set; } = null;
        public FieldInfo FieldInfo { get; internal set; } = null;
        public string Summary { get; internal set; } = "";
        public SettingType SettingType { get; set; }

        public bool HasParent { get => Parent != null; }
        public IEnumerable<ISetting> Children { get; private set; }

        internal void InvokeChange() {
            var eventArgs = new SettingChangedEventArgs(Name, FullName, this);
            InvokeChange(this, eventArgs);
        }

        internal void InvokeChange(ISetting s, SettingChangedEventArgs e) {
            if (Settings != null && Settings.EventsEnabled) {
                Changed?.Invoke(s, e);
                Settings.InvokeChange(s, e);
                if (HasParent)
                    Parent.InvokeChange(Parent, e);
            }
        }

        public void SetName(string name) {
            FullName = name;
        }

        public string GetName() {
            return FullName;
        }

        public bool HasChanges(Func<ISetting, bool> shouldCheck) {
            if (GetValue() == null)
                return false;
            else if (Nullable.GetUnderlyingType(GetValue().GetType()) == typeof(bool)) {
                FilterCore.LogError($"GET UNDER");
                var vBool = (bool?)GetValue();
                return vBool.HasValue;
            }
            else if (GetChildren().Count() > 0) {
                foreach (var child in GetChildren()) {
                    if (child.HasChanges(shouldCheck))
                        return true;
                }
                return false;
            }
            else if (shouldCheck != null && !shouldCheck(this))
                return false;
            else if (!(GetValue() is ISetting)) {
                if (GetValue() is System.Collections.IList list) {
                    return !JsonConvert.SerializeObject(GetValue()).Equals(JsonConvert.SerializeObject(GetDefaultValue()));
                }
                return GetDefaultValue() == null ? GetDefaultValue() != GetValue() : !GetDefaultValue().Equals(GetValue());
            }

            return false;
        }

        public bool HasChildren() {
            return GetChildren().Count() > 0;
        }

        public IEnumerable<ISetting> GetChildren() {
            if (GetValue() == null)
                return new List<ISetting>();
            if (Children == null)
                Children = GetValue().GetType().GetFields(Settings.BindingFlags)
                   .Where(f => typeof(ISetting).IsAssignableFrom(f.FieldType))
                   .Select(f => (ISetting)f.GetValue(GetValue()));
            return Children;
        }

        public virtual object GetDefaultValue() {
            return null;
        }

        public virtual object GetValue() {
            return this;
        }

        public virtual void SetValue(object newValue) {
            throw new NotImplementedException();
        }

        public string DisplayValue(bool expandLists = false, bool useDefault = false) {
            var value = useDefault ? GetDefaultValue() : GetValue();

            if (value == null)
                return "null";
            else if (value.GetType().IsEnum) {
                var supportsFlagsAttributes = FieldInfo.GetCustomAttributes(typeof(SupportsFlagsAttribute), true);

                if (supportsFlagsAttributes.Length > 0) {
                    return "0x" + ((uint)value).ToString("X8");
                }
                else {
                    return GetValue().ToString();
                }
            }
            else if (value is System.Collections.IList valueList) {
                if (expandLists) {
                    var results = new string[valueList.Count];
                    for (var i = 0; i < valueList.Count; i++)
                        results[i] = valueList[i].ToString();

                    return $"[{string.Join(",", results)}]";
                }
                else {
                    return "[List]";
                }
            }
            else if (value is Hellosam.Net.Collections.IDict) {
                if (expandLists) {
                    var results = new List<string>();
                    var dict = value as Hellosam.Net.Collections.ObservableDictionary<string, string>;

                    foreach (var dk in dict.Keys) {
                        results.Add($"{dk}: {dict[dk]}");
                    }

                    return $"{{{string.Join(",", results.ToArray())}}}";
                }
                else {
                    return "{Dictionary}";
                }
            }
            else if (value.GetType() == typeof(int) && FieldInfo.Name.Contains("Color")) {
                return "0x" + ((int)value).ToString("X8");
            }
            else {
                return value.ToString();
            }
        }

        public string FullDisplayValue() {
            return $"{FullName} ({SettingType}) = {DisplayValue(true)}";
        }
    }
}
