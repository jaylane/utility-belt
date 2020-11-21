using MetaViewWrappers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    public class LandscapeMapsView : BaseView {
        Timer timer;
        HudFixedLayout LandscapeMapsRenderContainer;

        public LandscapeMapsView(UtilityBeltPlugin ub) : base(ub) {
            CreateFromXMLResource("UtilityBelt.Views.LandscapeMapsView.xml", false, false);
        }

        public void Init() {
            try {
                view.UserResizeable = true;
                
                view.Location = new Point(
                    UB.LandscapeMaps.MapWindowX,
                    UB.LandscapeMaps.MapWindowY
                );
                view.Width = UB.LandscapeMaps.MapWindowWidth;
                view.Height = UB.LandscapeMaps.MapWindowHeight;
                

                LandscapeMapsRenderContainer = (HudFixedLayout)view["LandscapeMapsRenderContainer"];

                timer = new Timer(2000);

                timer.Elapsed += (s, e) => {
                    timer.Stop();
                    UB.LandscapeMaps.MapWindowX = view.Location.X;
                    UB.LandscapeMaps.MapWindowY = view.Location.Y;
                    UB.LandscapeMaps.MapWindowWidth = view.Width;
                    UB.LandscapeMaps.MapWindowHeight = view.Height;
                };

                view.Moved += (s, e) => {
                    try {
                        UB.LandscapeMaps.CreateHud();
                        if (timer.Enabled) timer.Stop();
                        timer.Start();
                    }
                    catch (Exception ex) { Logger.LogException(ex); }
                };

                view.Resize += (s, e) => {
                    try {
                        UB.LandscapeMaps.CreateHud();
                        if (timer.Enabled) timer.Stop();
                        timer.Start();
                    }
                    catch (Exception ex) { Logger.LogException(ex); }
                };

                //view.ShowInBar = UB.DungeonMaps.Enabled;
                UB.LandscapeMaps.PropertyChanged += LandscapeMaps_PropertyChanged;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void LandscapeMaps_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            
        }

        ~LandscapeMapsView() {
            if (timer != null) timer.Dispose();
        }
    }
}
