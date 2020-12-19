using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UBLoader.Lib.Settings {
    public class CharacterState<T> : Setting<T> {
        public CharacterState(T initialValue) : base(initialValue) {
            SettingType = SettingType.State;
        }
    }
}
