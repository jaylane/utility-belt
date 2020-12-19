using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Hellosam.Net.Collections;

namespace UBLoader.Lib.Settings {
    public class Setting<T> : ISetting {
        private bool hasDefault = false;

        new public bool IsDefault { get => HasChanges(p => true); }

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

            if (Value is ObservableCollection<string> collection) {
                DefaultValue = (T)Convert.ChangeType(new ObservableCollection<string>(), Value.GetType());
                var defaultCollection = DefaultValue as ObservableCollection<string>;
                foreach (var item in collection)
                    defaultCollection.Add(item);

                collection.CollectionChanged += (s, e) => {
                    InvokeChange();
                };
            }
            else if (Value is ObservableDictionary<string, string> dict) {
                DefaultValue = (T)Convert.ChangeType(new ObservableDictionary<string, string>(), Value.GetType());
                var defaultDict = DefaultValue as ObservableDictionary<string, string>;
                dict.CollectionChanged += (s, e) => {
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
            if (Value is ObservableCollection<string>) {
                foreach (var v in (System.Collections.IEnumerable)newValue) {
                    (Value as ObservableCollection<string>).Add(v.ToString());
                }
            }
            else if (Value is ObservableDictionary<string, string>) {
                throw new NotImplementedException();
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
