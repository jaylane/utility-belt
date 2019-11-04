using MetaViewWrappers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using UtilityBelt.Lib;
using VirindiViewService;
using VirindiViewService.XMLParsers;

namespace UtilityBelt.Views {
    public class MapView : BaseView {
        public MapView() {
            try {
                CreateFromXMLResource("UtilityBelt.Views.MapView.xml");
                view.Icon = GetIcon();
                view.UserResizeable = true;

                view.Location = new Point(
                    Globals.Settings.DungeonMaps.MapWindowX,
                    Globals.Settings.DungeonMaps.MapWindowY
                );
                view.Width = Globals.Settings.DungeonMaps.MapWindowWidth;
                view.Height = Globals.Settings.DungeonMaps.MapWindowHeight;

                var timer = new Timer();
                timer.Interval = 2000; // save the window position 2 seconds after it has stopped moving
                timer.Tick += (s, e) => {
                    timer.Stop();
                    Globals.Settings.DungeonMaps.MapWindowX = view.Location.X;
                    Globals.Settings.DungeonMaps.MapWindowY = view.Location.Y;
                    Globals.Settings.DungeonMaps.MapWindowWidth = view.Width;
                    Globals.Settings.DungeonMaps.MapWindowHeight = view.Height;
                };

                view.Moved += (s, e) => {
                    if (timer.Enabled) timer.Stop();
                    timer.Start();
                };

                view.Resize += (s, e) => {
                    if (timer.Enabled) timer.Stop();
                    timer.Start();
                };
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        protected override ACImage GetIcon() {
            return GetIcon("UtilityBelt.Resources.icons.dungeonmaps.png");
        }
    }
}
