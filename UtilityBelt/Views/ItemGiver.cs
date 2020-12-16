using MetaViewWrappers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Timers;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Settings;
using VirindiViewService;
using VirindiViewService.Controls;
using VirindiViewService.XMLParsers;
using UBLoader.Lib.Settings;

namespace UtilityBelt.Views {
    public class ItemGiverView : BaseView {
        private Timer timer;

        HudButton StartGiveItems;
        HudButton SetTargetButton;
        HudButton SetItemButton;
        HudStaticText Target;
        HudStaticText Item;
        HudTextBox GiveItemDelay;
        HudTextBox ItemCount;

        HudButton LP_StartGiveItems;
        HudButton LP_SetTarget;
        HudCombo LP_LootProfile;
        HudStaticText LP_Target;
        HudTextBox LP_GiveItemDelay;
        HudTabView ItemGiverTabs;

        public ItemGiverView(UtilityBeltPlugin ub) : base(ub) {
            CreateFromXMLResource("UtilityBelt.Views.ItemGiver.xml", false, false);
        }

        public void Init() {
            try {
                view.Location = new Point(
                    UB.InventoryManager.IGWindowX,
                    UB.InventoryManager.IGWindowY
                );

                timer = new Timer(2000);

                timer.Elapsed += (s, e) => {
                    timer.Stop();
                    UB.InventoryManager.IGWindowX.Value = view.Location.X;
                    UB.InventoryManager.IGWindowY.Value = view.Location.Y;
                };

                view.Moved += (s, e) => {
                    try {
                        if (timer.Enabled) timer.Stop();
                        timer.Start();
                    }
                    catch (Exception ex) { Logger.LogException(ex); }
                };

                ItemGiverTabs = (HudTabView)view["ItemGiverTabs"];

                StartGiveItems = (HudButton)view["StartGiveItems"];
                SetTargetButton = (HudButton)view["SetTargetButton"];
                SetItemButton = (HudButton)view["SetItemButton"];
                Target = (HudStaticText)view["Target"];
                Item = (HudStaticText)view["Item"];
                GiveItemDelay = (HudTextBox)view["GiveItemDelay"];
                ItemCount = (HudTextBox)view["ItemCount"];

                LP_StartGiveItems = (HudButton)view["LP_StartGiveItems"];
                LP_SetTarget = (HudButton)view["LP_SetTarget"];
                LP_Target = (HudStaticText)view["LP_Target"];
                LP_LootProfile = (HudCombo)view["LP_LootProfile"];
                LP_GiveItemDelay = (HudTextBox)view["LP_GiveItemDelay"];

                SetTargetButton.Hit += SetTargetButton_Hit;
                LP_SetTarget.Hit += SetTargetButton_Hit;
                SetItemButton.Hit += SetItemButton_Hit;
                StartGiveItems.Hit += StartGiveItems_Hit;
                LP_StartGiveItems.Hit += LP_StartGiveItems_Hit;
                LP_LootProfile.Change += LP_LootProfile_Change;

                view.ShowInBar = UB.InventoryManager.IGUIEnabled;
                UB.InventoryManager.IGUIEnabled.Changed += InventoryManager_PropertyChanged;
                UB.InventoryManager.Started += InventoryManager_Started;
                UB.InventoryManager.Finished += InventoryManager_Finished;

                ItemGiverTabs.OpenTabChange += ItemGiverTabs_OpenTabChange;

                UB.InventoryManager.IGWindowX.Changed += IGWindow_Changed;
                UB.InventoryManager.IGWindowY.Changed += IGWindow_Changed;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void IGWindow_Changed(object sender, SettingChangedEventArgs e) {
            view.Location = new Point(UB.InventoryManager.IGWindowX, UB.InventoryManager.IGWindowY);
        }

        private void LP_LootProfile_Change(object sender, EventArgs e) {
            try {
                RefreshProfiles();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void ItemGiverTabs_OpenTabChange(object sender, EventArgs e) {
            try {
                if (ItemGiverTabs.CurrentTab != 1) // Give Profile
                    return;

                RefreshProfiles();
            }
            catch (Exception ex) { Logger.LogException(ex); Logger.Error(ex.ToString()); }
        }

        private void RefreshProfiles() {
            string[] profiles = Directory.GetFiles(Path.Combine(Util.GetPluginDirectory(), "itemgiver"));

            string selected = LP_LootProfile.Count > 0 ? ((HudStaticText)LP_LootProfile[LP_LootProfile.Current]).Text : null;
            LP_LootProfile.Clear();
            for (var i = 0; i < profiles.Length; i++) {
                var name = profiles[i].Split('\\').Last();
                LP_LootProfile.AddItem(name, name);
                if (!string.IsNullOrEmpty(selected) && name == selected)
                    LP_LootProfile.Current = i;
            }
        }

        private void LP_StartGiveItems_Hit(object sender, EventArgs e) {
            try {
                if (UB.InventoryManager.IGRunning) {
                    UB.InventoryManager.IGStop();
                    return;
                }
                
                if (!Int32.TryParse(LP_GiveItemDelay.Text, out int delay)) {
                    Logger.Error($"ItemGiver: could not parse delay to int: {LP_GiveItemDelay.Text}");
                    return;
                }

                var profile = ((HudStaticText)LP_LootProfile[LP_LootProfile.Current]).Text;
                UB.InventoryManager.UB_ig_Start(LP_Target.Text, false, profile, delay);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void StartGiveItems_Hit(object sender, EventArgs e) {
            try {
                if (UB.InventoryManager.IGRunning) {
                    UB.InventoryManager.IGStop();
                    return;
                }

                if (!Int32.TryParse(ItemCount.Text, out int count)) {
                    Logger.Error($"ItemGiver: could not parse count to int: {ItemCount.Text}");
                    return;
                }
                if (!Int32.TryParse(GiveItemDelay.Text, out int delay)) {
                    Logger.Error($"ItemGiver: could not parse delay to int: {GiveItemDelay.Text}");
                    return;
                }
                UB.InventoryManager.GiveItem(Target.Text, false, Item.Text, false, false, count, delay);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void SetItemButton_Hit(object sender, EventArgs e) {
            try {
                var item = UB.Core.WorldFilter[UB.Core.Actions.CurrentSelection];

                if (item == null) {
                    Logger.Error($"ItemGiver: nothing selected");
                    return;
                }

                Item.Text = Util.GetObjectName(item.Id);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void SetTargetButton_Hit(object sender, EventArgs e) {
            try {
                var target = UB.Core.WorldFilter[UB.Core.Actions.CurrentSelection];

                if (target == null) {
                    Logger.Error($"ItemGiver: nothing selected");
                    return;
                }

                Target.Text = Util.GetObjectName(target.Id);
                LP_Target.Text = Util.GetObjectName(target.Id);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void InventoryManager_Started(object sender, EventArgs e) {
            StartGiveItems.Text = "Stop";
            LP_StartGiveItems.Text = "Stop";
        }

        private void InventoryManager_Finished(object sender, EventArgs e) {
            StartGiveItems.Text = "Start";
            LP_StartGiveItems.Text = "Start";
        }

        private void InventoryManager_PropertyChanged(object sender, SettingChangedEventArgs e) {
            view.ShowInBar = UB.InventoryManager.IGUIEnabled;
        }

        ~ItemGiverView() {
            UB.InventoryManager.IGWindowX.Changed -= IGWindow_Changed;
            UB.InventoryManager.IGWindowY.Changed -= IGWindow_Changed;
            UB.InventoryManager.IGUIEnabled.Changed -= InventoryManager_PropertyChanged;
            if (timer != null) timer.Dispose();
        }
    }
}
