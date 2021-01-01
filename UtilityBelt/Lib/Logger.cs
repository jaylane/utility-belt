using Exceptionless;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace UtilityBelt {
    public static class Logger {
        private const int MAX_LOG_SIZE = 1024 * 1024 * 20; // 20mb
        private const int MAX_LOG_AGE = 14; // in days
        private const int MAX_LOG_EXCEPTION = 50;
        private static uint exceptionCount = 0;

        public enum LogMessageType {
            Generic = 1,
            Expression = 2,
            Debug = 3,
            Error = 4
        }

        public class ExceptionlessUserData {
            public string WorldName { get; set; }
            public string UBVersion { get; set; }

            public ExceptionlessUserData() {
                WorldName = UBHelper.Core.WorldName;
                UBVersion = Util.GetVersion(true);
            }
        }

        public static void Init() {
            TruncateLogFiles();
            PruneOldLogs();
            SetupVCS();
        }

        private static void SetupVCS() {
            try {
                MyClasses.VCS_Connector.Initialize(UtilityBeltPlugin.Instance.Host, "UtilityBelt");
                
                // register message categories with vcs
                var logMessageTypes = Enum.GetValues(typeof(LogMessageType)).Cast<LogMessageType>();
                foreach (var logMessageType in logMessageTypes) {
                    var typeStr = logMessageType.ToString();
                    MyClasses.VCS_Connector.InitializeCategory(typeStr, typeStr + " messages");
                }
            }
            catch { }
        }

        public static void Debug(string message) {
            try {
                if (UtilityBeltPlugin.Instance != null && UtilityBeltPlugin.Instance.Plugin != null && UtilityBeltPlugin.Instance.Plugin.Debug) {
                    Logger.WriteToChat(message, LogMessageType.Debug);
                }
                else {
                    Util.WriteToDebugLog(message);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public static void WriteToChat(string message, LogMessageType messageType=LogMessageType.Generic, bool addUBTag = true, bool sendToVTank = true) {
            try {
                message = (addUBTag && !message.StartsWith("[UB]") ? "[UB] " : "") + message;
                var color = GetChatColor(messageType);
                var shouldShow = GetChatShouldShow(messageType);

                if (shouldShow)
                    MyClasses.VCS_Connector.SendChatTextCategorized(messageType.ToString(), message, color, 0);
                if (sendToVTank)
                    UBHelper.vTank.Tell(message, color, 0);
                Util.WriteToDebugLog(message);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private static bool GetChatShouldShow(LogMessageType messageType) {
            if (UtilityBeltPlugin.Instance == null || UtilityBeltPlugin.Instance.Plugin == null)
                return true;
            switch (messageType) {
                case LogMessageType.Debug:
                    return UtilityBeltPlugin.Instance.Plugin.DebugMessageDisplay.Enabled;
                case LogMessageType.Error:
                    return UtilityBeltPlugin.Instance.Plugin.ErrorMessageDisplay.Enabled;
                case LogMessageType.Expression:
                    return UtilityBeltPlugin.Instance.Plugin.ExpressionMessageDisplay.Enabled;
                default:
                    return UtilityBeltPlugin.Instance.Plugin.GenericMessageDisplay.Enabled;
            }
        }

        private static int GetChatColor(LogMessageType messageType) {
            if (UtilityBeltPlugin.Instance == null || UtilityBeltPlugin.Instance.Plugin == null)
                return 5;
            switch (messageType) {
                case LogMessageType.Debug:
                    return (int)UtilityBeltPlugin.Instance.Plugin.DebugMessageDisplay.Color.Value;
                case LogMessageType.Error:
                    return (int)UtilityBeltPlugin.Instance.Plugin.ErrorMessageDisplay.Color.Value;
                case LogMessageType.Expression:
                    return (int)UtilityBeltPlugin.Instance.Plugin.ExpressionMessageDisplay.Color.Value;
                default:
                    return (int)UtilityBeltPlugin.Instance.Plugin.GenericMessageDisplay.Color.Value;
            }
        }

        internal static void Error(string message) {
            try {
                Logger.WriteToChat("Error: " + message, LogMessageType.Error);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private static void PruneOldLogs() {
            PruneLogDirectory(Path.Combine(Util.GetCharacterDirectory(), "logs"));
        }

        private static void PruneLogDirectory(string logDirectory) {
            if (!Directory.Exists(logDirectory))
                return;
            string[] files = Directory.GetFiles(logDirectory, "*.txt", SearchOption.TopDirectoryOnly);

            var logFileRe = new Regex(@"^\w+\.(?<date>\d+\-\d+\-\d+)\.txt$");

            foreach (var file in files) {
                var fName = file.Split('\\').Last();
                var match = logFileRe.Match(fName);
                if (match.Success) {
                    DateTime.TryParse(match.Groups["date"].ToString(), out DateTime logDate);

                    if (logDate != null && (DateTime.Now - logDate).TotalDays > MAX_LOG_AGE) {
                        File.Delete(file);
                    }
                }
            }
        }
        private static string exceptionsLog => Path.Combine(Util.GetPluginDirectory(), "exceptions.txt");
        private static void TruncateLogFiles() {
            TruncateLogFile(exceptionsLog);
        }

        private static void TruncateLogFile(string logFile) {
            try {
                if (!File.Exists(logFile)) return;

                long length = new System.IO.FileInfo(logFile).Length;

                if (length > MAX_LOG_SIZE) {
                    File.Delete(logFile);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public static void LogException(Exception ex, bool logToMothership=true) {
            if (exceptionCount > MAX_LOG_EXCEPTION) return;
            exceptionCount++;

            UBLoader.Lib.File.TryWrite(exceptionsLog, $"== {DateTime.Now} ==================================================\r\n{ex.ToString()}\r\n============================================================================\r\n\r\n", true);

            try {
                if (logToMothership && UBLoader.FilterCore.Global.UploadExceptions && !UBLoader.FilterCore.IsDevelopmentVersion()) {
                    ex.ToExceptionless(false)
                        .SetUserName(UBLoader.FilterCore.GetAnonymousUserId())
                        .AddObject(new ExceptionlessUserData())
                        .Submit();
                }
            }
            catch { }
        }
        public static void LogException(string ex) {
            UBLoader.Lib.File.TryWrite(exceptionsLog, $"== {DateTime.Now} {ex}\r\n", true);
        }
    }
}
