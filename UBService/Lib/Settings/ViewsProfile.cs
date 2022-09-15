using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UBService.Lib.Settings {
    public class ViewsProfileSetting<T> : Setting<T> {
        public ViewsProfileSetting(T initialValue) : base(initialValue) {
            SettingType = SettingType.Views; 
        }
    }
}
