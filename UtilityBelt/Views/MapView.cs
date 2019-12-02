using MetaViewWrappers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Timers;
using UtilityBelt.Lib;
using VirindiViewService;
using VirindiViewService.XMLParsers;

namespace UtilityBelt.Views {
    public class MapView : BaseView {
        Timer timer;

        public MapView(UtilityBeltPlugin ub) : base(ub) {
            CreateFromXMLResource("UtilityBelt.Views.MapView.xml");
        }

        public void Init() {
            try {
                view.Icon = GetIcon();
                view.UserResizeable = true;

                view.Location = new Point(
                    UB.DungeonMaps.MapWindowX,
                    UB.DungeonMaps.MapWindowY
                );
                view.Width = UB.DungeonMaps.MapWindowWidth;
                view.Height = UB.DungeonMaps.MapWindowHeight;

                timer = new Timer(2000);

                timer.Elapsed += (s, e) => {
                    timer.Stop();
                    UB.DungeonMaps.MapWindowX = view.Location.X;
                    UB.DungeonMaps.MapWindowY = view.Location.Y;
                    UB.DungeonMaps.MapWindowWidth = view.Width;
                    UB.DungeonMaps.MapWindowHeight = view.Height;
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

        internal override ACImage GetIcon() {
            return GetIcon("UtilityBelt.Resources.icons.dungeonmaps.png");
        }
        ~MapView() {
            if (timer != null) timer.Dispose();
        }
    }
}
