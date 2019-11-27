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

            Bitmap image = null;
            string bitmapFile = Path.Combine(Util.GetTilePath(), environmentId + @".bmp");
            
            ColorMap[] colorMap = GetColorMap();
            ImageAttributes attr = new ImageAttributes();
            attr.SetRemapTable(colorMap);

            // attempt to load from file first, then fall back to embedded.
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
                using (Stream manifestResourceStream = typeof(TileCache).Assembly.GetManifestResourceStream($"UtilityBelt.Resources.tiles.{environmentId}.bmp")) {
                    if (manifestResourceStream != null) {
                        using (Bitmap bitmap = new Bitmap(manifestResourceStream)) {
                            bitmap.MakeTransparent(Color.White);
                            image = new Bitmap(bitmap.Width, bitmap.Height);
                            Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
                            using (Graphics g = Graphics.FromImage(image)) {
                                g.DrawImage(bitmap, rect, 0, 0, rect.Width, rect.Height, GraphicsUnit.Pixel, attr);
                                g.Save();
                            }
                        }
                    }
                }
            }

            cache.Add(environmentId, image);

            return Get(environmentId);
        }

        private static ColorMap[] GetColorMap() {
            ColorMap[] colorMap = new ColorMap[5];

            colorMap[0] = new ColorMap();
            colorMap[0].OldColor = Color.FromArgb(UtilityBeltPlugin.Instance.DungeonMaps.Display.Walls.DefaultColor);
            colorMap[0].NewColor = Color.FromArgb(UtilityBeltPlugin.Instance.DungeonMaps.Display.Walls.Color);

            colorMap[1] = new ColorMap();
            colorMap[1].OldColor = Color.FromArgb(UtilityBeltPlugin.Instance.DungeonMaps.Display.InnerWalls.DefaultColor);
            colorMap[1].NewColor = Color.FromArgb(UtilityBeltPlugin.Instance.DungeonMaps.Display.InnerWalls.Color);

            colorMap[2] = new ColorMap();
            colorMap[2].OldColor = Color.FromArgb(UtilityBeltPlugin.Instance.DungeonMaps.Display.RampedWalls.DefaultColor);
            colorMap[2].NewColor = Color.FromArgb(UtilityBeltPlugin.Instance.DungeonMaps.Display.RampedWalls.Color);

            colorMap[3] = new ColorMap();
            colorMap[3].OldColor = Color.FromArgb(UtilityBeltPlugin.Instance.DungeonMaps.Display.Floors.DefaultColor);
            colorMap[3].NewColor = Color.FromArgb(UtilityBeltPlugin.Instance.DungeonMaps.Display.Floors.Color);

            colorMap[4] = new ColorMap();
            colorMap[4].OldColor = Color.FromArgb(UtilityBeltPlugin.Instance.DungeonMaps.Display.Stairs.DefaultColor);
            colorMap[4].NewColor = Color.FromArgb(UtilityBeltPlugin.Instance.DungeonMaps.Display.Stairs.Color);

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
