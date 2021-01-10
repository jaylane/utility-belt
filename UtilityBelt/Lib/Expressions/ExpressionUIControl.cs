using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace UtilityBelt.Lib.Expressions {
    public class ExpressionUIControl : ExpressionObjectBase {
        [JsonIgnore]
        public string WindowName { get; set; }
        [JsonIgnore]
        public string ControlName { get; set; }

        public ExpressionUIControl() {
            IsSerializable = false;
        }

        public ExpressionUIControl(string windowName, string controlName) {
            IsSerializable = false;
            ShouldFireEvents = false;
            WindowName = windowName;
            ControlName = controlName;
            ShouldFireEvents = true;
        }

        public override string ToString() {
            return $"{WindowName}::{ControlName}";
        }
    }
}
