using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace UBService.Lib.Settings {
    internal static class SettingsFile {
        private static bool isHooked_pendingFile = false;
        // this is never Disposed. It's static- it will live until acclient dies.
        internal static Queue<pendingWrite> pendingWrites = new Queue<pendingWrite>();
        internal static Timer saveTimer = new Timer(TimeSpan.FromSeconds(1));

        public static string ReadAllText(string path) {
            using (FileStream fs = new FileStream(path,
                                      FileMode.Open,
                                      FileAccess.Read,
                                      FileShare.ReadWrite)) {
                using (StreamReader sr = new StreamReader(fs)) {
                    return sr.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// Use this for ANY attempt to write a file.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="data"></param>
        /// <param name="append">append, or overwrite?</param>
        /// <param name="writeTries">do not supply- used internally.</param>
        public static void TryWrite(string fileName, string data, bool append = true, int writeTries = 0) {
            try {
                if (!Directory.Exists(Path.GetDirectoryName(fileName)))
                    Directory.CreateDirectory(Path.GetDirectoryName(fileName));

                using (FileStream fs = new FileStream(fileName,
                                          append ? FileMode.Append : FileMode.OpenOrCreate,
                                          FileAccess.Write,
                                          FileShare.ReadWrite)) {
                    using (StreamWriter sr = new StreamWriter(fs)) {
                        if (!append) {
                            sr.BaseStream.SetLength(0);
                        }
                        sr.Write(data);
                    }
                }
            }
            catch (IOException) {
                pendingWrites.Enqueue(new pendingWrite { writeTries = writeTries + 1, fileName = fileName, data = data, append = append });
                if (!isHooked_pendingFile) {
                    saveTimer.OnTick += SaveTimer_OnTick;
                    isHooked_pendingFile = true;
                }
            }
            catch { }

        }

        /// <summary>
        /// Makes a final attempt to flush all files.
        /// </summary>
        public static void FlushFiles() => SaveTimer_OnTick(null, null);
        internal static void SaveTimer_OnTick(object sender, EventArgs e) {
            int cascade = pendingWrites.Count;
            int failSafe = 5;
            while (pendingWrites.Count > 0) {
                cascade--;
                if (cascade < 0) {
                    failSafe--;
                    if (failSafe < 0) break;
                    cascade = pendingWrites.Count;
                    System.Threading.Thread.Sleep(20);
                }
                pendingWrite it = pendingWrites.Dequeue();

                // If 5 write attempts have already been completed on this; prepend a "_" to the filename, and try again.
                if (it.writeTries == 5) {
                    it.fileName = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(it.fileName), "_" + System.IO.Path.GetFileName(it.fileName));
                    it.writeTries = 0;
                }
                TryWrite(it.fileName, it.data, it.append, it.writeTries);
            }
            if (pendingWrites.Count == 0 && isHooked_pendingFile) {
                saveTimer.OnTick -= SaveTimer_OnTick;
                isHooked_pendingFile = false;
            }
        }
        internal struct pendingWrite {
            public int writeTries;
            public string fileName;
            public string data;
            public bool append;
        }
    }
}
