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
    public class SettingsView : BaseView {
        private Timer timer;
        private HudList CategoryList;
        private HudCheckBox ExpandAll;
        private HudFixedLayout FormLayout;
        private HudFixedLayout SettingEditLayout;
        private HudTextBox SettingsFilter;
        private ACImage icon;
        private Dictionary<string, SettingCategory> SettingCategories = new Dictionary<string, SettingCategory>();
        private SettingsForm currentForm = null;
        private HudStaticText SummaryText = null;
        private SettingCategory _selectedCategory = null;
        private SettingCategory SelectedCategory {
            get { return _selectedCategory; }
            set {
                if (_selectedCategory == value)
                    return;
                if (_selectedCategory != null)
                    _selectedCategory.SetInactive();
                ClearSettingsForm();
                _selectedCategory = value;
                _selectedCategory.SetActive();
            }
        }

        private class SettingCategory : IDisposable {
            public HudView View { get; private set; }
            public SettingsView SettingsView { get; private set; }
            public HudList CategoryList { get; private set; }
            public HudFixedLayout SettingsContainer { get; private set; }
            public KeyValueTreeView SettingsTreeView { get; private set; }
            public int ListIndex { get; private set; }
            public string Category { get; private set; }
            public IEnumerable<ISetting> Children { get; private set; }
            public HudList.HudListRowAccessor Row { get => CategoryList[ListIndex]; }
            private IEnumerable<KeyValueTreeView.TreeItem> TreeItems = null;

            public SettingCategory(HudView view, SettingsView settingsView, IEnumerable<ISetting> settings, string category) {
                View = view;
                SettingsView = settingsView;
                CategoryList = (HudList)view["CategoryList"];
                SettingsContainer = (HudFixedLayout)view["SettingsContainer"];
                Category = category;
                var row = CategoryList.AddRow();
                ((HudStaticText)row[1]).Text = Category;
                ListIndex = CategoryList.RowCount - 1;
                Children = settings;
            }

            public void SetActive() {
                try {
                    ((HudStaticText)Row[0]).TextColor = Color.Red;
                    ((HudStaticText)Row[1]).TextColor = Color.Red;
                    ((HudStaticText)Row[2]).TextColor = Color.Red;
                    ((HudStaticText)Row[2]).Text = ">";

                    SettingsTreeView = new KeyValueTreeView();
                    SettingsContainer.AddControl(SettingsTreeView, new Rectangle(0, 0, 400, 200));
                    
                    if (TreeItems == null)
                        TreeItems = GetTreeViewItems();
                    SettingsTreeView.AddTreeItems(TreeItems);
                    SettingsTreeView.FilterText = SettingsView.SettingsFilter.Text;
                }
                catch (Exception ex) { Logger.LogException(ex); }
            }

            private IEnumerable<KeyValueTreeView.TreeItem> GetTreeViewItems(ISetting parentSetting=null, KeyValueTreeView.TreeItem parentTreeItem=null) {
                List<KeyValueTreeView.TreeItem> items = new List<KeyValueTreeView.TreeItem>();

                var settings = parentSetting == null ? Children : parentSetting.GetChildren().Select(f => (ISetting)f.GetValue(parentSetting));

                foreach (var setting in settings) {
                    var node = new KeyValueTreeView.TreeItem(setting.Name, setting.IsContainer ? "" : setting.DisplayValue(), parentTreeItem, setting.FullName + " " + setting.Summary);
                    node.Clicked += (s, e) => {
                        if (setting.IsContainer)
                            SettingsView.ClearSettingsForm();
                        else
                            SettingsView.DrawSettingsForm(setting);
                    };
                    if (!setting.IsContainer) {
                        node.HasChanges = !setting.IsDefault;
                        setting.Changed += (s, e) => {
                            node.Value = setting.DisplayValue();
                            node.HasChanges = !setting.IsDefault;
                        };
                    }
                    node.AddChildren(GetTreeViewItems(setting, node));
                    items.Add(node);
                }

                return items;
            }

            public void SetInactive() {
                ((HudStaticText)Row[0]).TextColor = View.Theme.GetColor("ListText");
                ((HudStaticText)Row[1]).TextColor = View.Theme.GetColor("ListText");
                ((HudStaticText)Row[2]).TextColor = View.Theme.GetColor("ListText");
                ((HudStaticText)Row[2]).Text = "";

                SettingsContainer.RemovedChild(SettingsTreeView);
                SettingsTreeView.Dispose();
                SettingsTreeView = null;
            }

            public void Dispose() {
            }
        }

        public SettingsView(UtilityBeltPlugin ub) : base(ub) {
            CreateFromXMLResource("UtilityBelt.Views.SettingsView.xml");
        }

        public void Init() {
            try {
                view.Location = new Point(
                    UB.Plugin.SettingsWindowPositionX,
                    UB.Plugin.SettingsWindowPositionY
                );

                timer = new Timer(2000);

                timer.Elapsed += (s, e) => {
                    UB.Plugin.SettingsWindowPositionX.Value = view.Location.X;
                    UB.Plugin.SettingsWindowPositionY.Value = view.Location.Y;
                    timer.Stop();
                };

                view.Moved += (s, e) => {
                    if (timer.Enabled) timer.Stop();
                    timer.Start();
                };

                UB.Plugin.SettingsWindowPositionX.Changed += WindowPosition_Changed;
                UB.Plugin.SettingsWindowPositionY.Changed += WindowPosition_Changed;

                SettingEditLayout = (HudFixedLayout)view["SettingEditLayout"];
                SettingsFilter = (HudTextBox)view["SettingsFilter"];
                SettingsFilter.Change += SettingsFilter_Change;

                ExpandAll = (HudCheckBox)view["ExpandAll"];
                ExpandAll.Change += ExpandAll_Change;

                CategoryList = (HudList)view["CategoryList"];
                CategoryList.WPadding = 0;
                CategoryList.Click += CategoryList_Click;

                SetupCategories();
                SelectedCategory = SettingCategories["All"];
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void WindowPosition_Changed(object sender, SettingChangedEventArgs e) {
            if (!timer.Enabled)
                view.Location = new Point(UB.Plugin.SettingsWindowPositionX, UB.Plugin.SettingsWindowPositionY);
        }

        private void SettingsFilter_Change(object sender, EventArgs e) {
            SelectedCategory.SettingsTreeView.FilterText = SettingsFilter.Text;
            ClearSettingsForm();
        }

        private void ExpandAll_Change(object sender, EventArgs e) {
            SelectedCategory.SettingsTreeView.ExpandAll(ExpandAll.Checked);
            ClearSettingsForm();
        }

        private void CategoryList_Click(object sender, int row, int col) {
            try {
                SelectedCategory = SettingCategories.Where(t => t.Value.ListIndex == row).FirstOrDefault().Value;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void SetupCategories() {
            var allSettings = new List<ISetting>();
            var loaderSettings = typeof(UBLoader.FilterCore).GetFields(Settings.BindingFlags)
                .Where(f => typeof(ISetting).IsAssignableFrom(f.FieldType))
                .Select(f => (ISetting)f.GetValue(null));
            allSettings.AddRange(loaderSettings);
            var globalSettings = typeof(UBLoader.FilterCore.GlobalSettings).GetFields(Settings.BindingFlags)
                .Where(f => typeof(ISetting).IsAssignableFrom(f.FieldType))
                .Select(f => (ISetting)f.GetValue(UBLoader.FilterCore.Global));

            foreach (var tool in UB.GetToolInfos()) {
                var toolInstance = (ToolBase)tool.GetValue(UB);
                if (toolInstance.HasChildren())
                    allSettings.Add(toolInstance as ISetting);
            }

            SettingCategories.Add("All", new SettingCategory(view, this, allSettings, "All"));
            SettingCategories.Add("Global", new SettingCategory(view, this, globalSettings, "Global"));

            foreach (var tool in UB.GetToolInfos()) {
                var toolInstance = (ToolBase)tool.GetValue(UB);
                if (toolInstance.HasChildren()) {
                    var settings = toolInstance.GetChildren().Select(f => f.GetValue(toolInstance) as ISetting);
                    SettingCategories.Add(tool.Name, new SettingCategory(view, this, settings, tool.Name));
                }
            }
        }

        private OptionResult GetSettingPropFromText(string setting) {
            if (setting.StartsWith("Global."))
                return UBLoader.FilterCore.Settings.Get(setting.Substring(7));
            else
                return UB.Settings.Get(setting);
        }

        public void ClearSettingsForm() {
            if (currentForm != null) {
                currentForm.Dispose();
                currentForm = null;
            }
            if (SummaryText != null)
                SummaryText.Text = "";
        }

        public void DrawSettingsForm(ISetting setting) {
            ClearSettingsForm();

            if (SummaryText == null) {
                SummaryText = new HudStaticText();

                SettingEditLayout.AddControl(SummaryText, new Rectangle(5, 5, 390, 40));
            }
            if (FormLayout == null) {
                FormLayout = new HudFixedLayout();
                SettingEditLayout.AddControl(FormLayout, new Rectangle(5, 40, 390, 25));
            }

            SummaryText.TextAlignment = WriteTextFormats.WordBreak;

            SummaryText.Text = setting.FullName;
            SummaryText.Text += " (" + setting.SettingType.ToString() + "): ";
            SummaryText.Text += setting.Summary;

            currentForm = new SettingsForm(setting, FormLayout, setting.GetValue().GetType());
            SummaryText.Text += " (Default: " + setting.DisplayValue(false, true) + ")";

            currentForm.Changed += (s, e) => {
                setting.SetValue(currentForm.Value);
            };
        }

        internal override ACImage GetIcon() {
            if (icon != null)
                return icon;
            icon = GetIcon("UtilityBelt.Resources.icons.utilitybelt.png");
            return icon;
        }
        ~SettingsView() {
            if (icon != null) icon.Dispose();
        }
    }
}
