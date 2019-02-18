using Decal.Adapter.Wrappers;
using Decal.Filters;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
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

                    byte[] cellData = service.GetCellFile(Globals.Core.Actions.Landcell);

                    if (cellData == null) {
                        Util.WriteToChat("No such cell file: " + Globals.Core.Actions.Landcell);
                        throw new Exception("No such cell file: " + Globals.Core.Actions.Landcell);
                    }
                    else {
                        //Util.WriteToChat("Found cell file: " + Globals.Core.Actions.Landcell);
                    }

                    
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
