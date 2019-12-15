using Decal.Adapter;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace UtilityBelt.Lib {
    public class Mp3Player {
        private readonly CoreManager core;

        [DllImport("winmm.dll")]
        private static extern long mciSendString(string strCommand, StringBuilder strReturn, int iReturnLength, IntPtr hWnd);

        public Mp3Player(CoreManager core) {
            this.core = core;
        }

        public void PlaySound(string path, int volume) {
            var soundId = Guid.NewGuid().ToString("N");

            EventHandler<EventArgs> ev = null;
            ev = (s, e) => {
                try {
                    var sb = new StringBuilder();
                    mciSendString($"status {soundId} mode", sb, 128, core.Decal.Hwnd);
                    if (sb.ToString().Equals("stopped", StringComparison.OrdinalIgnoreCase)) {
                        mciSendString($"close {soundId}", null, 0, core.Decal.Hwnd);
                        core.RenderFrame -= ev;
                    }
                }
                catch (Exception ex) { Logger.LogException(ex); }
            };

            core.RenderFrame += ev;
            var c = $"open \"{path}\" type mpegvideo alias {soundId}";
            mciSendString(c, null, 0, core.Decal.Hwnd);
            mciSendString($"setaudio {soundId} volume to {volume * 10}", null, 0, core.Decal.Hwnd);
            mciSendString($"play {soundId}", null, 0, core.Decal.Hwnd);
        }
    }
}
