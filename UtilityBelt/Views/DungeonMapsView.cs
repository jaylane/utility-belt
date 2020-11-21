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
    public class DungeonMapsView : BaseView {
        Timer timer;
        HudFixedLayout DungeonMapsRenderContainer;

        public DungeonMapsView(UtilityBeltPlugin ub) : base(ub) {
            CreateFromXMLResource("UtilityBelt.Views.DungeonMapsView.xml");
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
                    try {
                        ResizeMapHud();
                        if (timer.Enabled) timer.Stop();
                        timer.Start();
                    }
                    catch (Exception ex) { Logger.LogException(ex); }
                };

                view.Resize += (s, e) => {
                    try {
                        ResizeMapHud();
                        if (timer.Enabled) timer.Stop();
                        timer.Start();
                    }
                    catch (Exception ex) { Logger.LogException(ex); }
                };

                view.VisibleChanged += (s, e) => {
                    try {
                        ResizeMapHud();
                    }
                    catch (Exception ex) { Logger.LogException(ex); }
                };

                view.ShowInBar = UB.DungeonMaps.Enabled;
                UB.DungeonMaps.PropertyChanged += DungeonMaps_PropertyChanged;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void DungeonMaps_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName != "Enabled")
                return;
            view.Icon = GetIcon();
            view.ShowInBar = UB.DungeonMaps.Enabled;
            if (UB.DungeonMaps.Enabled && !UB.DungeonMaps.DrawWhenClosed) {
                view.Visible = true;
            }
            else if (!UB.DungeonMaps.Enabled && view.Visible) {
                view.Visible = false;
            }
        }

        public void ResizeMapHud() {
            UB.DungeonMaps.CreateHud(DungeonMapsRenderContainer.SavedViewRect.Width, DungeonMapsRenderContainer.SavedViewRect.Height, DungeonMapsRenderContainer.SavedViewRect.X, DungeonMapsRenderContainer.SavedViewRect.Y);
        }

        internal override ACImage GetIcon() {
            return GetIcon("UtilityBelt.Resources.icons.dungeonmaps.png");
        }
        ~DungeonMapsView() {
            if (timer != null) timer.Dispose();
        }
    }
}
