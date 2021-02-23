using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Timers;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Settings;
using VirindiViewService;
using VirindiViewService.Controls;
using VirindiViewService.XMLParsers;
using UBLoader.Lib.Settings;
using Hellosam.Net.Collections;

namespace UtilityBelt.Views {
    public class NetworkStatsView : BaseView {
        private Timer moveTimer;
        private Timer drawTimer;
        private HudList StatsList;

        public NetworkStatsView(UtilityBeltPlugin ub) : base(ub) {
            CreateFromXMLResource("UtilityBelt.Views.NetworkStatsView.xml", false, false);
            Init();
        }

        public void Init() {
            try {
                view.Location = new Point(
                    UB.Networking.StatsWindowPositionX,
                    UB.Networking.StatsWindowPositionY
                );

                drawTimer = new Timer(1000);
                drawTimer.Elapsed += DrawTimer_Elapsed;
                drawTimer.Start();

                moveTimer = new Timer(2000);

                moveTimer.Elapsed += (s, e) => {
                    UB.Networking.StatsWindowPositionX.Value = view.Location.X;
                    UB.Networking.StatsWindowPositionY.Value = view.Location.Y;
                    moveTimer.Stop();
                };

                view.Moved += (s, e) => {
                    if (moveTimer.Enabled) moveTimer.Stop();
                    moveTimer.Start();
                };

                UB.Networking.StatsWindowPositionX.Changed += WindowPosition_Changed;
                UB.Networking.StatsWindowPositionY.Changed += WindowPosition_Changed;

                StatsList = (HudList)view["StatsList"];
                StatsList.WPadding = 0;
                StatsList.Click += StatsList_Click;

                view.ShowInBar = true;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void DrawTimer_Elapsed(object sender, ElapsedEventArgs e) {
            if (!view.Visible)
                return;
            StatsList.ClearRows();
            var keys = UB.Networking.MessageStats.Keys.ToList();
            keys.Sort((a,b) => UB.Networking.MessageStats[a].Type.CompareTo(UB.Networking.MessageStats[b].Type));

            foreach (var k in keys) {
                var row = StatsList.AddRow();
                var v = UB.Networking.MessageStats[k];
                ((HudStaticText)row[0]).Text = k;
                ((HudStaticText)row[1]).Text = $"{v.LastMinuteSentStats.Count()}";
                ((HudStaticText)row[2]).Text = $"{v.LastMinuteRecvStats.Count()}";
                ((HudStaticText)row[3]).Text = $"{v.Last5MinuteSentStats.Count()}";
                ((HudStaticText)row[4]).Text = $"{v.Last5MinuteRecvStats.Count()}";
                ((HudStaticText)row[5]).Text = $"{v.LastHourSentStats.Count()}";
                ((HudStaticText)row[6]).Text = $"{v.LastHourRecvStats.Count()}";
                if (v.LastHourRecvStats.Count > 0)
                    ((HudStaticText)row[7]).Text = $"{v.LastHourRecvStats.Select(s => s.Size).Average():N1}";
                else
                    ((HudStaticText)row[7]).Text = "?";
            }
        }

        private void StatsList_Click(object sender, int row, int col) {
            //throw new NotImplementedException();
        }

        private void WindowPosition_Changed(object sender, SettingChangedEventArgs e) {
            if (!moveTimer.Enabled)
                view.Location = new Point(UB.Networking.StatsWindowPositionX, UB.Networking.StatsWindowPositionY);
        }
    }
}
