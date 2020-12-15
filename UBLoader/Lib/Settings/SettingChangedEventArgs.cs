using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UBLoader.Lib.Settings {
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

}
