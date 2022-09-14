﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UBService.Lib.Settings {
    public class GameEvent<T> : Setting<T> {
        public GameEvent(T initialValue) : base(initialValue) {
            SettingType = SettingType.GameEvent;
        }
    }
}
