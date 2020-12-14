using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;

namespace UtilityBelt.Lib.Settings {
    public class SettingChangedEventArgs : EventArgs {
        public string PropertyName { get; private set; }
        public string FullName { get; private set; }
        public ISetting Setting { get; private set; }

        public SettingChangedEventArgs(string propertyName, string fullName, ISetting setting) {
            PropertyName = propertyName;
            FullName = fullName;
            Setting = setting;
        }
    }

    public class Setting<T> : ISetting {
        private bool hasDefault = false;

        private T _value;

        public T Value {
            get { return _value; }
            set {
                var original = _value;
                _value = value;
                if (!hasDefault) {
                    DefaultValue = _value;
                    hasDefault = true;
                }
                if (!_value.Equals(original)) {
                    InvokeChange();
                }
            }
        }

        public T DefaultValue { get; private set; }

        public Setting() {

        }

        public Setting(T initialValue) {
            DefaultValue = initialValue;
            Value = initialValue;
            hasDefault = true;

            if (Value is ObservableCollection<string>) {
                (Value as ObservableCollection<string>).CollectionChanged += (s, e) => {
                    InvokeChange();
                };
            }
        }

        public override object GetDefaultValue() {
            return DefaultValue;
        }

        public override object GetValue() {
            return Value;
        }

        public override void SetValue(object newValue) {
            if (typeof(T) != typeof(string) && typeof(T).GetInterfaces().Contains(typeof(IList<string>))) {
                foreach (var v in (System.Collections.IEnumerable)newValue) {
                    ((IList<string>)Value).Add(v.ToString());
                }
            }
            else if (!typeof(ISetting).IsAssignableFrom(newValue.GetType())) {
                Value = (T)Convert.ChangeType(newValue, Value.GetType());
            }
        }

        public static implicit operator T(Setting<T> value) {
            return value.Value;
        }

        public override string ToString() {
            return Value.ToString();
        }
    }
}
