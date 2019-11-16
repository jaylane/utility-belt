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

        public static void Init() {
            TruncateLogFiles();
            PruneOldLogs();
            Debug($"UB Initialized {DateTime.UtcNow} v{Util.GetVersion(true)}");
        }

        public static void Debug(string message) {
            try {
                if (Globals.Settings != null && Globals.Settings.Plugin.Debug) {
                    Globals.Host.Actions.AddChatText("[UB] " + message, 5);
                }
                Util.WriteToDebugLog(message);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        internal static void Error(string message) {
            try {
                Globals.Host.Actions.AddChatText("[UB] Error: " + message, 2);
                Util.WriteToDebugLog(message);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private static void PruneOldLogs() {
            PruneLogDirectory(Path.Combine(Util.GetCharacterDirectory(), "logs"));
        }

        private static void PruneLogDirectory(string logDirectory) {
            try {
                string[] files = Directory.GetFiles(logDirectory, "*.txt", SearchOption.TopDirectoryOnly);

                var logFileRe = new Regex(@"^\w+\.(?<date>\d+\-\d+\-\d+)\.txt$");

                foreach (var file in files) {
                    var fName = file.Split('\\').Last();
                    var match = logFileRe.Match(fName);
                    if (match.Success) {
                        DateTime logDate;
                        DateTime.TryParse(match.Groups["date"].ToString(), out logDate);

                        if (logDate != null && (DateTime.Now - logDate).TotalDays > MAX_LOG_AGE) {
                            File.Delete(file);
                        }
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private static void TruncateLogFiles() {
            TruncateLogFile(Path.Combine(Util.GetPluginDirectory(), "exceptions.txt"));
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

        public static void LogException(Exception ex) {
            try {

                if (exceptionCount > MAX_LOG_EXCEPTION) return;

                exceptionCount++;

                using (StreamWriter writer = new StreamWriter(Path.Combine(Util.GetPluginDirectory(), "exceptions.txt"), true)) {
                    writer.WriteLine("============================================================================");
                    writer.WriteLine(DateTime.Now.ToString());
                    writer.WriteLine("Error: " + ex.Message);
                    writer.WriteLine("Source: " + ex.Source);
                    writer.WriteLine("Stack: " + ex.StackTrace);
                    if (ex.InnerException != null) {
                        writer.WriteLine("Inner: " + ex.InnerException.Message);
                        writer.WriteLine("Inner Stack: " + ex.InnerException.StackTrace);
                    }
                    writer.WriteLine("============================================================================");
                    writer.WriteLine("");
                    writer.Close();
                }
            }
            catch {
            }
        }
    }
}
