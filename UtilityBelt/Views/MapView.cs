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
using VirindiViewService.Controls;
using VirindiViewService.XMLParsers;

namespace UtilityBelt.Views {
    public class MapView : BaseView {
        Timer timer;
        HudFixedLayout DungeonMapsRenderContainer;

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
                view.ForcedZOrder = -1;

                DungeonMapsRenderContainer = (HudFixedLayout)view["DungeonMapsRenderContainer"];

                timer = new Timer(2000);

                timer.Elapsed += (s, e) => {
                    timer.Stop();
                    UB.DungeonMaps.MapWindowX = view.Location.X;
                    UB.DungeonMaps.MapWindowY = view.Location.Y;
                    UB.DungeonMaps.MapWindowWidth = view.Width;
                    UB.DungeonMaps.MapWindowHeight = view.Height;
                };

                view.Moved += (s, e) => {
                    ResizeMapHud();
                    if (timer.Enabled) timer.Stop();
                    timer.Start();
                };

                view.Resize += (s, e) => {
                    ResizeMapHud();
                    if (timer.Enabled) timer.Stop();
                    timer.Start();
                };

                view.VisibleChanged += (s, e) => {
                    ResizeMapHud();
                };

                view.ShowInBar = UB.DungeonMaps.Enabled;
                UB.DungeonMaps.PropertyChanged += DungeonMaps_PropertyChanged;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void DungeonMaps_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            view.Icon = GetIcon();
            view.ShowInBar = UB.DungeonMaps.Enabled;
        }

        public void ResizeMapHud() {
            UB.DungeonMaps.CreateHud(DungeonMapsRenderContainer.SavedViewRect.Width, DungeonMapsRenderContainer.SavedViewRect.Height, DungeonMapsRenderContainer.SavedViewRect.X, DungeonMapsRenderContainer.SavedViewRect.Y);
        }

        internal override ACImage GetIcon() {
            return GetIcon("UtilityBelt.Resources.icons.dungeonmaps.png");
        }
        ~MapView() {
            if (timer != null) timer.Dispose();
        }
    }
}
