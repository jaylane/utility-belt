using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UBService.Lib.Settings {
    public class Alias<T> : Setting<T> {
        public Alias(T initialValue) : base(initialValue) {
            SettingType = SettingType.Alias;
        }
    }
}
