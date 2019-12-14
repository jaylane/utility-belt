using Decal.Adapter;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using VirindiViewService;

namespace UtilityBelt.Lib.Dungeon {
    public static class TextureCache {
        public static Dictionary<ushort, DxTexture> tileCache = new Dictionary<ushort, DxTexture>();
        public static Dictionary<int, DxTexture> markerCache = new Dictionary<int, DxTexture>();
        public static Dictionary<int, DxTexture> iconCache = new Dictionary<int, DxTexture>();
        public static Dictionary<string, DxTexture> textCache = new Dictionary<string, DxTexture>();
        public static int TileScale = 7;

        public static DxTexture GetTile(ushort environmentId) {
            if (tileCache.ContainsKey(environmentId)) return tileCache[environmentId];

            try {

                Bitmap image = null;
                string bitmapFile = Path.Combine(Util.GetTilePath(), environmentId + ".bmp");

                ColorMap[] colorMap = GetColorMap();
                ImageAttributes attr = new ImageAttributes();
                attr.SetRemapTable(colorMap);

                using (Stream manifestResourceStream = typeof(TextureCache).Assembly.GetManifestResourceStream($"UtilityBelt.Resources.tiles.{environmentId}.bmp")) {
                    if (manifestResourceStream != null) {
                        using (Bitmap bitmap = new Bitmap(manifestResourceStream)) {
                            if (bitmap != null) {
                                bitmap.MakeTransparent(Color.White);
                                image = new Bitmap(bitmap, bitmap.Width, bitmap.Height);
                            }
                        }
                    }
                }

                if (image != null) {
                    var texture = new DxTexture(new Size(10 * TileScale, 10 * TileScale));
                    try {
                        texture.BeginRender();
                        texture.Fill(new Rectangle(0, 0, texture.Width, texture.Height), Color.Transparent);
                        texture.DrawImage(image, new Rectangle(0, 0, texture.Width, texture.Height), Color.White);
                    }
                    catch (Exception ex) { Logger.LogException(ex); }
                    finally {
                        texture.EndRender();
                    }
                    tileCache.Add(environmentId, texture);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
            finally {
                if (!tileCache.ContainsKey(environmentId)) {
                    tileCache.Add(environmentId, null);
                }
            }

            return tileCache[environmentId];
        }

        public static DxTexture GetMarker(int color) {
            if (markerCache.ContainsKey(color)) return markerCache[color];

            using (Bitmap image = new Bitmap(16, 16)) {
                using (Graphics g = Graphics.FromImage(image)) {
                    g.Clear(Color.Transparent);
                    using (var brush = new SolidBrush(Color.FromArgb(color))) {
                        g.FillEllipse(brush, new Rectangle(0, 0, 16, 16));
                    }
                    g.Save();
                }

                markerCache.Add(color, image == null ? null : new DxTexture(image));
            }

            return GetMarker(color);
        }

        internal static DxTexture GetIcon(int icon) {
            try {
                if (iconCache.ContainsKey(icon)) return iconCache[icon];

                byte[] portalFile = Util.FileService.GetPortalFile(0x06000000 + icon);
                byte[] bytes = portalFile.Skip(28).Take(4096).ToArray();

                Bitmap bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb);

                BitmapData bmpData = bmp.LockBits(
                                     new Rectangle(0, 0, bmp.Width, bmp.Height),
                                     ImageLockMode.WriteOnly, bmp.PixelFormat);

                System.Runtime.InteropServices.Marshal.Copy(bytes, 0, bmpData.Scan0, bytes.Length);
                bmp.UnlockBits(bmpData);
                bmp.MakeTransparent(Color.White);

                iconCache.Add(icon, new DxTexture(bmp));

                return iconCache[icon];
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return null;
        }

        public static DxTexture GetText(string text, int height, Color color, string fontFace, int fontWeight, int shadowSize=1, bool allowRender=true) {
            if (textCache.ContainsKey(text)) return textCache[text];

            if (!allowRender) return null;

            using (var measureTexture = new DxTexture(new Size(200, 10))) {
                var textRect = measureTexture.MeasureText(text, WriteTextFormats.Center, fontFace, height, fontWeight, false, shadowSize);
                DxTexture texture = new DxTexture(new Size(textRect.Width, textRect.Height));

                try {
                    texture.BeginRender();
                    texture.Fill(new Rectangle(0, 0, texture.Width, texture.Height), Color.Transparent);
                    try {
                        texture.BeginText(fontFace, height, fontWeight, false, shadowSize, (int)byte.MaxValue);
                        texture.WriteText(text, color, WriteTextFormats.Center | WriteTextFormats.VerticalCenter, textRect);
                    }
                    finally {
                        texture.EndText();
                    }
                }
                catch (Exception ex) { Logger.LogException(ex); }
                finally { texture.EndRender(); }

                textCache.Add(text, texture);

                return texture;
            }
        }

        private static ColorMap[] GetColorMap() {
            ColorMap[] colorMap = new ColorMap[5];

            colorMap[0] = new ColorMap();
            colorMap[0].OldColor = Color.FromArgb(UtilityBeltPlugin.Instance.DungeonMaps.Display.Walls.DefaultColor);
            if (UtilityBeltPlugin.Instance.DungeonMaps.Display.Walls.Enabled)
                colorMap[0].NewColor = Color.FromArgb(UtilityBeltPlugin.Instance.DungeonMaps.Display.Walls.Color);
            else
                colorMap[0].NewColor = Color.Transparent;

            colorMap[1] = new ColorMap();
            colorMap[1].OldColor = Color.FromArgb(UtilityBeltPlugin.Instance.DungeonMaps.Display.InnerWalls.DefaultColor);
            if (UtilityBeltPlugin.Instance.DungeonMaps.Display.InnerWalls.Enabled)
                colorMap[1].NewColor = Color.FromArgb(UtilityBeltPlugin.Instance.DungeonMaps.Display.InnerWalls.Color);
            else
                colorMap[1].NewColor = Color.Transparent;

            colorMap[2] = new ColorMap();
            colorMap[2].OldColor = Color.FromArgb(UtilityBeltPlugin.Instance.DungeonMaps.Display.RampedWalls.DefaultColor);
            if (UtilityBeltPlugin.Instance.DungeonMaps.Display.RampedWalls.Enabled)
                colorMap[2].NewColor = Color.FromArgb(UtilityBeltPlugin.Instance.DungeonMaps.Display.RampedWalls.Color);
            else
                colorMap[2].NewColor = Color.Transparent;

            colorMap[3] = new ColorMap();
            colorMap[3].OldColor = Color.FromArgb(UtilityBeltPlugin.Instance.DungeonMaps.Display.Floors.DefaultColor);
            if (UtilityBeltPlugin.Instance.DungeonMaps.Display.Floors.Enabled)
                colorMap[3].NewColor = Color.FromArgb(UtilityBeltPlugin.Instance.DungeonMaps.Display.Floors.Color);
            else
                colorMap[3].NewColor = Color.Transparent;

            colorMap[4] = new ColorMap();
            colorMap[4].OldColor = Color.FromArgb(UtilityBeltPlugin.Instance.DungeonMaps.Display.Stairs.DefaultColor);
            if (UtilityBeltPlugin.Instance.DungeonMaps.Display.Stairs.Enabled)
                colorMap[4].NewColor = Color.FromArgb(UtilityBeltPlugin.Instance.DungeonMaps.Display.Stairs.Color);
            else
                colorMap[4].NewColor = Color.Transparent;

            return colorMap;
        }

        public static void Clear() {
            var tileKeys = tileCache.Keys.ToArray();
            foreach (var key in tileKeys) {
                if (tileCache[key] != null) {
                    tileCache[key].Dispose();
                    tileCache[key] = null;
                }
            }

            tileCache.Clear();

            var markerKeys = markerCache.Keys.ToArray();
            foreach (var key in markerKeys) {
                if (markerCache[key] != null) {
                    markerCache[key].Dispose();
                    markerCache[key] = null;
                }
            }

            markerCache.Clear();

            var iconKeys = iconCache.Keys.ToArray();
            foreach (var key in iconKeys) {
                if (iconCache[key] != null) {
                    iconCache[key].Dispose();
                    iconCache[key] = null;
                }
            }

            iconCache.Clear();

            var textKeys = textCache.Keys.ToArray();
            foreach (var key in textKeys) {
                if (textCache[key] != null) {
                    textCache[key].Dispose();
                    textCache[key] = null;
                }
            }

            textCache.Clear();
        }
    }
}
