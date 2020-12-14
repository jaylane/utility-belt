using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;

namespace UtilityBelt.Lib.Settings {
    public abstract class ISetting {
        public event EventHandler<SettingChangedEventArgs> Changed;

        public string FullName { get; protected set; } = "";
        public string Name => FullName.Split('.').Last();

        public bool HasParent { get => !(this is ToolBase); }
        public IEnumerable<FieldInfo> Children { get; private set; }

        internal void InvokeChange() {
            var eventArgs = new SettingChangedEventArgs(Name, FullName, this);
            InvokeChange(this, eventArgs);
        }

        internal void InvokeChange(ISetting s, SettingChangedEventArgs e) {
            if (UtilityBeltPlugin.Instance.Settings.EventsEnabled) {
                Changed?.Invoke(s, e);
                UtilityBeltPlugin.Instance.Settings.InvokeChange(s, e);
            }
        }

        public void SetName(string name) {
            FullName = name;
        }

        public string GetName() {
            return FullName;
        }

        public bool HasChanges() {
            if (GetChildren().Count() > 0) {
                foreach (var child in GetChildren()) {
                    if (((ISetting)child.GetValue(GetValue())).HasChanges())
                        return true;
                }
                return false;
            }
            else
                return GetDefaultValue() == null ? false : !GetDefaultValue().Equals(GetValue());
        }

        public bool HasChildren() {
            return GetChildren().Count() > 0;
        }

        public IEnumerable<FieldInfo> GetChildren() {
            if (Children == null)
             Children = GetValue().GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => typeof(ISetting).IsAssignableFrom(f.FieldType));
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
    }
}
