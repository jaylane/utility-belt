using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Timers;
using VirindiViewService.Controls;
using UtilityBelt.Service.Lib.Settings;
using UtilityBelt.Lib.Networking;

namespace UtilityBelt.Views {
    public class NetworkClientsView : BaseView {
        private Timer moveTimer;
        internal HudFixedLayout TabLayout;
        internal HudTabView NotebookTags;
        internal HudButton All_FollowMe;
        internal HudButton All_UseSelected;
        internal HudTextBox All_Delay;
        private DateTime lastDraw = DateTime.MinValue;
        private string currentTab = "All";

        public class TabInfo {
            public HudFixedLayout Layout { get; set; }
            public Dictionary<int, TabCharacterInfo> Characters { get; } = new Dictionary<int, TabCharacterInfo>();

            public TabInfo() { }
        }

        public class TabCharacterInfo {
            public HudFixedLayout Layout { get; set; }
            public List<HudControl> Children { get; } = new List<HudControl>();

            public ClientInfo Client { get; set; } = null;

            public string CharacterName {
                get {
                    if (Client != null)
                        return Client.Name;
                    return UtilityBeltPlugin.Instance.Core.CharacterFilter.Name;
                }
            }

            public TabCharacterInfo() { }

            internal void Draw() {
                var characterNameText = new HudStaticText() {
                    Text = CharacterName,
                    TextColor = Color.White
                };
                Children.Add(characterNameText);
                Layout.AddControl(characterNameText, new Rectangle(0, 0, 150, 20));
            }
        }

        private Dictionary<string, TabInfo> tabs = new Dictionary<string, TabInfo>();

        public NetworkClientsView(UtilityBeltPlugin ub) : base(ub) {
            CreateFromXMLResource("UtilityBelt.Views.NetworkClientsView.xml", false, false);
            Init();
        }

        public void Init() {
            try {
                view.Location = new Point(
                    UB.NetworkUI.WindowPositionX,
                    UB.NetworkUI.WindowPositionY
                );

                moveTimer = new Timer(2000);

                moveTimer.Elapsed += (s, e) => {
                    UB.NetworkUI.WindowPositionX.Value = view.Location.X;
                    UB.NetworkUI.WindowPositionY.Value = view.Location.Y;
                    moveTimer.Stop();
                };

                view.Moved += (s, e) => {
                    if (moveTimer.Enabled) moveTimer.Stop();
                    moveTimer.Start();
                };

                UB.NetworkUI.WindowPositionX.Changed += NetClientWindowPosition_Changed;
                UB.NetworkUI.WindowPositionY.Changed += NetClientWindowPosition_Changed;

                TabLayout = (HudFixedLayout)view["TabLayout"];
                All_FollowMe = (HudButton)view["All_FollowMe"];
                All_UseSelected = (HudButton)view["All_UseSelected"];
                All_Delay = (HudTextBox)view["All_Delay"];

                All_FollowMe.Hit += All_FollowMe_Hit;
                All_UseSelected.Hit += All_UseSelected_Hit;

                UB.Networking.Tags.Changed += Tags_Changed;
                view.VisibleChanged += View_VisibleChanged;
                UpdateTabs();

                view.ShowInBar = true;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private bool TryGetDelay(out int delay) {
            return int.TryParse(All_Delay.Text, out delay);
        }

        private IEnumerable<ClientInfo> GetActiveClients() {
            return UB.Networking.Clients.Values.Select(c => c).Where(c => currentTab == "All" || c.Tags.Contains(currentTab));
        }

        private void All_UseSelected_Hit(object sender, EventArgs e) {
            if (!UB.Core.Actions.IsValidObject(UB.Core.Actions.CurrentSelection)) {
                Logger.Error($"Nothing selected");
                return;
            }
            if (!TryGetDelay(out int delay)) {
                Logger.Error($"Unable to parse client delay to int: {All_Delay.Text}");
            }
            var cmd = $"/ub mexec actiontryuseitem[wobjectfindbyid[{UB.Core.Actions.CurrentSelection}]]";
            UB.Networking.DoBroadcast(GetActiveClients(), cmd, delay);
        }

        private void All_FollowMe_Hit(object sender, EventArgs e) {
            if (!TryGetDelay(out int delay)) {
                Logger.Error($"Unable to parse client delay to int: {All_Delay.Text}");
            }
            var cmd = $"/ub follow {UB.Core.CharacterFilter.Name}";
            UB.Networking.DoBroadcast(GetActiveClients(), cmd, delay);
        }

        private void View_VisibleChanged(object sender, EventArgs e) {
            try {
                if (view.Visible)
                    UpdateTabs();
            }
            catch (Exception ex) { Logger.LogException(ex);  }
        }

        private void Tags_Changed(object sender, SettingChangedEventArgs e) {
            UpdateTabs();
        }

        private void UpdateTabs() {
            if (NotebookTags != null) {
                currentTab = NotebookTags[NotebookTags.CurrentTab].Name;
                tabs.Clear();
                NotebookTags.Dispose();
                NotebookTags = null;
            }

            NotebookTags = new HudTabView();
            TabLayout.AddControl(NotebookTags, new Rectangle(0, 0, 600, 800));

            var tags = UB.Networking.Tags.Value.ToList();
            tags.Insert(0, "All");
            foreach (var tag in tags) {
                var parent = new HudFixedLayout();
                parent.InternalName = tag;
                NotebookTags.AddTab(parent, tag);
                tabs.Add(tag, new TabInfo() {
                    Layout = parent
                });
                if (!string.IsNullOrEmpty(currentTab) && currentTab == tag)
                    NotebookTags.CurrentTab = NotebookTags.TabCount - 1;
            }
        }

        private void WindowPosition_Changed(object sender, SettingChangedEventArgs e) {
            if (!moveTimer.Enabled)
                view.Location = new Point(UB.NetworkUI.WindowPositionX, UB.NetworkUI.WindowPositionY);
        }

        private void NetClientWindowPosition_Changed(object sender, SettingChangedEventArgs e) {
            if (!moveTimer.Enabled)
                view.Location = new Point(UB.NetworkUI.WindowPositionX, UB.NetworkUI.WindowPositionY);
        }
        protected override void Dispose(bool disposing) {
            UB.NetworkUI.WindowPositionX.Changed -= NetClientWindowPosition_Changed;
            UB.NetworkUI.WindowPositionY.Changed -= NetClientWindowPosition_Changed;
            UB.Networking.Tags.Changed -= Tags_Changed;
            view.VisibleChanged -= View_VisibleChanged;
            base.Dispose(disposing);
        }
    }
}
