using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Hellosam.Net.Collections;
using System.Collections.Specialized;
using Newtonsoft.Json;
using System.Collections;

namespace UBService.Lib.Settings {
    public class Setting<T> : ISetting {
        private bool hasDefault = false;
        private T _value;

        public T Value {
            get { return _value; }
            set {
                var validationError = ValidateFunction == null ? null : ValidateFunction(value);
                if (!string.IsNullOrEmpty(validationError)) {
                    //FilterCore.LogError($"Unable to set {FullName} to {value}: {validationError}");
                    return;
                }

                var original = _value;
                _value = value;
                if (!hasDefault)
                    AssignDefault();
                if (!_value.Equals(original))
                    InvokeChange();
            }
        }

        public T DefaultValue { get; private set; }
        public Func<T, string> ValidateFunction { get; }

        public Setting() {

        }

        public Setting(T initialValue, Func<T, string> validateFunc=null) {
            Value = initialValue;
            ValidateFunction = validateFunc;
            AssignDefault();

            if (Value != null && Value is INotifyCollectionChanged collection) {
                collection.CollectionChanged += (s, e) => {
                    InvokeChange();
                };
            }
            /*
            else if (Value != null && Value is ObservableDictionary<string, string> dict) {
                DefaultValue = (T)Convert.ChangeType(new ObservableDictionary<string, string>(), Value.GetType());
                var defaultDict = DefaultValue as ObservableDictionary<string, string>;
                dict.CollectionChanged += (s, e) => {
                    InvokeChange();
                };
            }
            else if (Value != null && Value is ObservableDictionary<XpTarget, double> ddict) {
                DefaultValue = (T)Convert.ChangeType(new ObservableDictionary<XpTarget, double>(), Value.GetType());
                var defaultDict = DefaultValue as ObservableDictionary<XpTarget, double>;
                ddict.CollectionChanged += (s, e) => {
                    InvokeChange();
                };
            }
            */
        }

        public void AssignDefault(bool force=false) {
            if (hasDefault && !force)
                return;
            if (Value != null && Value.GetType().IsGenericType) {
                DefaultValue = (T)Activator.CreateInstance(Value.GetType());
            }
            else {
                DefaultValue = Value;
            }
            hasDefault = true;
        }

        public override object GetDefaultValue() {
            return DefaultValue;
        }

        public override object GetValue() {
            return Value;
        }

        public override void SetValue(object newValue) {
            if (Value == null) {
                try {
                    Value = (T)Convert.ChangeType(newValue, typeof(T));
                }
                catch {
                    Value = (T)newValue;
                }
            }
            else if (Value is ObservableCollection<string>) {
                foreach (var v in (IEnumerable)newValue) {
                    (Value as ObservableCollection<string>).Add(v.ToString());
                }
            }
            else if (Value.GetType().IsGenericType && Value is IList collection) {
                (Value as IList).Clear();
                foreach (var v in (newValue as IList)) {
                    (Value as IList).Add(v);
                }
            }
            /*
            else if (Value is ObservableDictionary<string, string>) {
                throw new NotImplementedException();
            }
            else if (Value is ObservableDictionary<XpTarget, double>) {
                throw new NotImplementedException();
            }
            */
            else {
                try {
                    Value = (T)Convert.ChangeType(newValue, typeof(T));
                }
                catch {
                    Value = (T)newValue;
                }
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
