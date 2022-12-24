using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UtilityBelt.Scripting.Enums;
using UtilityBelt.Scripting.Interop;

namespace UBLoader.Lib {
    public class GameLogger : ILogger {
        public void Log(string message, LogLevel logLevel = LogLevel.Info) {
            FilterCore.LogError($"{message}");
        }

        public void Log(Exception ex) {
            FilterCore.LogError($"Exception: {ex}");
        }
    }
}
