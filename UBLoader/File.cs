using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace UBLoader {
    public static class File {
        private static bool isHooked_pendingFile = false;
        // this is never Disposed. It's static- it will live until acclient dies.
        internal static Queue<pendingWrite> pendingWrites = new Queue<pendingWrite>();

        /// <summary>
        /// Use this for ANY attempt to write a file.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="data"></param>
        /// <param name="append">append, or overwrite?</param>
        /// <param name="writeTries">do not supply- used internally.</param>
        public static void TryWrite(string fileName, string data, bool append = true, int writeTries = 0) {
            try {
                using (StreamWriter writer = new StreamWriter(fileName, append)) {
                    writer.Write(data);
                    writer.Close();
                }
            }
            catch (IOException) {
                pendingWrites.Enqueue(new pendingWrite { writeTries = writeTries + 1, fileName = fileName, data = data, append = append });
                if (!isHooked_pendingFile) {
                    UBHelper.Core.RadarUpdate += RadarUpdate_pendingFile;
                    isHooked_pendingFile = true;
                }
            }
            catch { }

        }

        /// <summary>
        /// Makes a final attempt to flush all files.
        /// </summary>
        public static void FlushFiles() => RadarUpdate_pendingFile(0);
        internal static void RadarUpdate_pendingFile(double _) {
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
                UBHelper.Core.RadarUpdate -= RadarUpdate_pendingFile;
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
