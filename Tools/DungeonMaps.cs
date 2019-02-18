using Decal.Adapter.Wrappers;
using Decal.Filters;
using System;
using System.Drawing;
using System.IO;
using UtilityBelt.Views;

namespace UtilityBelt.Tools {
    class DungeonMaps : IDisposable {
        private const int THINK_INTERVAL = 300;
        private const int DRAW_INTERVAL = 100;
        private DateTime lastThought = DateTime.MinValue;
        private bool disposed = false;
        private Hud hud = null;
        private Rectangle hudRect;
        private Bitmap drawBitmap = new Bitmap(100,100);

        public DungeonMaps() {
            Draw();
        }

        public void Draw() {
            try {
                /*
                if (Globals.View.view.Visible != true) {
                    if (hud != null) {
                        hud.Clear();
                        hud.Enabled = false;
                    }
                    return;
                }
                */

                if (hudRect == null) {
                    hudRect = new Rectangle(Globals.View.view.Location, Globals.View.view.TotalSize);
                }

                hudRect.Location = Globals.View.view.Location;
                hudRect.Size = Globals.View.view.TotalSize;

                if (hud == null) {
                    hud = Globals.Core.RenderService.CreateHud(hudRect);
                }

                hud.Region = hudRect;

                hud.Clear();

                hud.Fill(Color.Transparent);
                hud.BeginRender();

                try {
                    FileService service = Globals.Core.Filter<FileService>();
                    byte[] cellFile = service.GetCellFile(Globals.Core.Actions.Landcell);

                    try {
                        if (cellFile == null) {
                            throw new Exception();
                        }

                        ushort environmentId = BitConverter.ToUInt16(cellFile, 16 + (int)cellFile[12] * 2);
                        Vector3Object position = new Vector3Object(
                            BitConverter.ToSingle(cellFile, 20 + (int)cellFile[12] * 2),
                            BitConverter.ToSingle(cellFile, 24 + (int)cellFile[12] * 2),
                            BitConverter.ToSingle(cellFile, 28 + (int)cellFile[12] * 2)
                        );

                        float rotW = BitConverter.ToSingle(cellFile, 32 + (int)cellFile[12] * 2);
                        float rotX = BitConverter.ToSingle(cellFile, 36 + (int)cellFile[12] * 2);
                        float rotY = BitConverter.ToSingle(cellFile, 40 + (int)cellFile[12] * 2);
                        float rotZ = BitConverter.ToSingle(cellFile, 44 + (int)cellFile[12] * 2);

                        Util.WriteToChat(string.Format("{0}: environment: {1} position: x:{2} y:{3} z:{4} r: {5}",
                            (Globals.Core.Actions.Landcell).ToString("X"),
                            environmentId,
                            Math.Round(position.X),
                            Math.Round(position.Y),
                            Math.Round(position.Z),
                            rotW));

                    }
                    catch (Exception ex) { Util.LogException(ex); }
                    
                    using (Stream manifestResourceStream = typeof(MainView).Assembly.GetManifestResourceStream("UtilityBelt.icons.dungeonmaps.png")) {
                        if (manifestResourceStream != null) {
                            using (Bitmap bitmap = new Bitmap(manifestResourceStream)) {
                                if (drawBitmap != null) drawBitmap.Dispose();
                                drawBitmap = new Bitmap(Globals.View.view.TotalSize.Width, Globals.View.view.TotalSize.Height);

                                drawBitmap.MakeTransparent();

                                Graphics g = Graphics.FromImage(drawBitmap);

                                g.TranslateTransform((float)drawBitmap.Width / 2, (float)drawBitmap.Height / 2);

                                g.RotateTransform(360 - (float)Globals.Core.Actions.Heading);
                                g.TranslateTransform(-(float)drawBitmap.Width / 2, -(float)drawBitmap.Height / 2);
                                g.DrawImage(bitmap, ((float)drawBitmap.Width / 2) - 50, ((float)drawBitmap.Height / 2) - 50, 100, 100);
                                g.Save();

                                hud.DrawImage(drawBitmap, new Rectangle(0,0, Globals.View.view.TotalSize.Width, Globals.View.view.TotalSize.Height));
                            }
                        }
                    }
                }
                catch (Exception ex) { Util.LogException(ex); }
                finally {
                    hud.EndRender();
                    hud.Alpha = 200;
                    hud.Enabled = true;
                }
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        public void Think() {
            if (DateTime.UtcNow - lastThought > TimeSpan.FromMilliseconds(DRAW_INTERVAL)) {
                lastThought = DateTime.UtcNow;
            }
            Draw();
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    Globals.Core.RenderService.RemoveHud(hud);
                    hud.Dispose();
                }
                disposed = true;
            }
        }
    }
}
