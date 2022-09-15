using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UBService.Lib.Settings {
    public class CharacterSetting<T> : Setting<T> {
        public CharacterSetting(T initialValue) : base(initialValue) {
            SettingType = SettingType.CharacterSettings;
        }
    }
}
