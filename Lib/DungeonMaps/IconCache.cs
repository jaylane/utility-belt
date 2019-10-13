using Decal.Filters;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.DungeonMaps {
    public static class IconCache {
        public static Dictionary<int, Bitmap> cache = new Dictionary<int, Bitmap>();

        public static Bitmap Get(int iconId) {
            if (cache.ContainsKey(iconId)) return cache[iconId];

            var image = TryGetIcon(iconId);

            cache.Add(iconId, image);

            return Get(iconId);
        }

        private static Bitmap TryGetIcon(int id) {
            try {
                FileService service = Globals.Core.Filter<FileService>();
                byte[] portalFile = service.GetPortalFile(0x06000000 + id);
                byte[] bytes = portalFile.Skip(28).Take(4096).ToArray();

                Bitmap bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb);

                BitmapData bmpData = bmp.LockBits(
                                     new Rectangle(0, 0, bmp.Width, bmp.Height),
                                     ImageLockMode.WriteOnly, bmp.PixelFormat);

                System.Runtime.InteropServices.Marshal.Copy(bytes, 0, bmpData.Scan0, bytes.Length);
                bmp.UnlockBits(bmpData);
                bmp.MakeTransparent(Color.White);

                return bmp;
            }
            catch (Exception ex) { }

            return null;
        }

        public static void Clear() {
            var keys = cache.Keys.ToArray();
            foreach (var key in keys) {
                if (cache[key] != null) {
                    cache[key].Dispose();
                    cache[key] = null;
                }
            }

            cache.Clear();
        }
    }
}
