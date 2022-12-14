using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UBScript.Enums;
using UBScript.Interop;

namespace UBLoader.Lib {
    public class GameLogger : ILogger {
        public void Log(string message, LogLevel logLevel = LogLevel.Info) {
            FilterCore.LogError($"{message}");
        }

        public void Log(Exception ex) {
            FilterCore.LogError($"Exception: ");
            FilterCore.LogException(ex);
        }
    }
}
