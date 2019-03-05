using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace UtilityBelt {
    public static class Logger {
        private const int MAX_LOG_SIZE = 1024 * 1024 * 20; // 20mb

        public static void Init() {
            TruncateLogFiles();
        }

        private static void TruncateLogFiles() {
            TruncateLogFile(Path.Combine(Util.GetPluginDirectory(), "exceptions.txt"));
        }

        private static void TruncateLogFile(string logFile) {
            if (!File.Exists(logFile)) return;

            long length = new System.IO.FileInfo(logFile).Length;

            if (length > MAX_LOG_SIZE) {
                File.Delete(logFile);
            }
        }
    }
}
