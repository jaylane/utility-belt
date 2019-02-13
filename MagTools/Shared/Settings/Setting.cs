using UtilityBelt;
using System;
using System.Collections.Generic;

// https://github.com/Mag-nus/Mag-Plugins/blob/master/Shared/Settings/Setting.cs

namespace Mag.Shared.Settings {
    public class ValidateSettingEventArgs<T> : EventArgs {
        public bool IsValid { get; set; }
        public string InvalidReason { get; set; }
        public readonly T Value;

        public ValidateSettingEventArgs(T value) {
            IsValid = true;
            InvalidReason = "Unknown";
            Value = value;
        }

        public void Invalidate(string reason) {
            IsValid = false;
            InvalidReason = reason;
        }
    }

    public class Setting<T> : IDisposable {
        public readonly string Xpath;

        public readonly string Description;

        public readonly T DefaultValue;
        private bool disposed = false;

        private T value;
        public T Value {
            get {
                return value;
            }
            set {
                try {
                    // If we're setting it to the value its already at, don't continue with the set.
                    if (Object.Equals(this.value, value) && typeof(T) != typeof(List<string>) && typeof(T) != typeof(List<int>))
                        return;

                    if (Validate != null) {
                        ValidateSettingEventArgs<T> eventArgs = new ValidateSettingEventArgs<T>(value);
                        Validate(this, eventArgs);

                        if (!eventArgs.IsValid) {
                            Util.WriteToChat(String.Format("Cannot set {0} to {1}: {2}", this.Xpath, value, eventArgs.InvalidReason));
                            return;
                        }
                    }

                    // The value differs, set it.
                    this.value = value;
                    if (typeof(T) != typeof(List<string>) && typeof(T) != typeof(List<int>)) {
                        Util.WriteToChat(String.Format("{0} = {1}", this.Xpath, this.value.ToString()));
                    }

                    StoreValueInConfigFile();

                    if (Changed != null)
                        Changed(this);
                }
                catch (Exception e) { Util.LogException(e); }
            }
        }

        public event EventHandler<ValidateSettingEventArgs<T>> Validate;
        public event Action<Setting<T>> Changed;

        public Setting(string xpath, string description = null, T defaultValue = default(T)) {
            try {
                Xpath = xpath;

                Description = description;

                DefaultValue = defaultValue;

                LoadValueFromConfig(defaultValue);
            }
            catch (Exception e) { Util.LogException(e); }
        }

        void LoadValueFromConfig(T defaultValue) {
            try {
                value = SettingsFile.GetSetting(Xpath, defaultValue, Description);
            }
            catch (Exception e) { Util.LogException(e); }
        }

        void StoreValueInConfigFile() {
            try {
                SettingsFile.PutSetting(Xpath, value, Description, true);
            }
            catch (Exception e) { Util.LogException(e); }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                }
                disposed = true;
            }
        }
    }
}
