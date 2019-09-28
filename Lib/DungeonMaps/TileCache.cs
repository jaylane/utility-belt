using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.DungeonMaps {
    public static class TileCache {
        public static Dictionary<ushort, Bitmap> cache = new Dictionary<ushort, Bitmap>();

        public static Bitmap Get(ushort environmentId) {
            if (cache.ContainsKey(environmentId)) return cache[environmentId];

            Bitmap image;
            string bitmapFile = Path.Combine(Util.GetTilePath(), environmentId + @".bmp");

            if (File.Exists(bitmapFile)) {
                using (Bitmap bmp = new Bitmap(bitmapFile)) {
                    bmp.MakeTransparent(Color.White);
                    image = new Bitmap(bmp.Clone(new Rectangle(0, 0, bmp.Width, bmp.Height), PixelFormat.Format32bppPArgb));
                }
            }
            else {
                image = null;
            }

            cache.Add(environmentId, image);

            return Get(environmentId);
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
