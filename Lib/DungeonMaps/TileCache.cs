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
            
            ColorMap[] colorMap = GetColorMap();
            ImageAttributes attr = new ImageAttributes();
            attr.SetRemapTable(colorMap);

            if (File.Exists(bitmapFile)) {
                using (Bitmap bmp = new Bitmap(bitmapFile)) {
                    bmp.MakeTransparent(Color.White);
                    image = new Bitmap(bmp.Width, bmp.Height);
                    Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
                    using (Graphics g = Graphics.FromImage(image)) {
                        g.DrawImage(bmp, rect, 0, 0, rect.Width, rect.Height, GraphicsUnit.Pixel, attr);
                        g.Save();
                    }
                }
            }
            else {
                image = null;
            }

            cache.Add(environmentId, image);

            return Get(environmentId);
        }

        private static ColorMap[] GetColorMap() {
            ColorMap[] colorMap = new ColorMap[5];

            colorMap[0] = new ColorMap();
            colorMap[0].OldColor = Color.FromArgb(Globals.Config.DungeonMaps.WallColor.DefaultValue);
            colorMap[0].NewColor = Color.FromArgb(Globals.Config.DungeonMaps.WallColor.Value);

            colorMap[1] = new ColorMap();
            colorMap[1].OldColor = Color.FromArgb(Globals.Config.DungeonMaps.InnerWallColor.DefaultValue);
            colorMap[1].NewColor = Color.FromArgb(Globals.Config.DungeonMaps.InnerWallColor.Value);

            colorMap[2] = new ColorMap();
            colorMap[2].OldColor = Color.FromArgb(Globals.Config.DungeonMaps.RampedWallColor.DefaultValue);
            colorMap[2].NewColor = Color.FromArgb(Globals.Config.DungeonMaps.RampedWallColor.Value);

            colorMap[3] = new ColorMap();
            colorMap[3].OldColor = Color.FromArgb(Globals.Config.DungeonMaps.FloorColor.DefaultValue);
            colorMap[3].NewColor = Color.FromArgb(Globals.Config.DungeonMaps.FloorColor.Value);

            colorMap[4] = new ColorMap();
            colorMap[4].OldColor = Color.FromArgb(Globals.Config.DungeonMaps.StairsColor.DefaultValue);
            colorMap[4].NewColor = Color.FromArgb(Globals.Config.DungeonMaps.StairsColor.Value);

            return colorMap;
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
