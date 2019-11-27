using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using UtilityBelt.Lib.Settings;
using VirindiViewService;
using VirindiViewService.Controls;

namespace UtilityBelt.Views {

    public class ListEditor : IDisposable {
        public VirindiViewService.ViewProperties properties;
        public VirindiViewService.ControlGroup controls;
        public VirindiViewService.HudView view;
        bool disposed = false;

        private HudList ChildList;
        private HudFixedLayout SettingsFormLayout;
        private HudButton Cancel;
        private HudButton AddOrUpdate;
        private MainView mainView;
        private OptionResult prop;
        private int selectedIndex = -1;
        private SettingsForm form;
        private string Setting;

        public ListEditor(MainView mainView, string setting) {
            this.mainView = mainView;
            this.prop = UtilityBeltPlugin.Instance.Settings.GetOptionProperty(setting);
            Setting = setting;

            VirindiViewService.XMLParsers.Decal3XMLParser parser = new VirindiViewService.XMLParsers.Decal3XMLParser();
            parser.ParseFromResource("UtilityBelt.Views.ListEditor.xml", out properties, out controls);

            view = new VirindiViewService.HudView(properties, controls);

            view.Title = $"Editing {setting}";

            int x = (mainView.view.Location.X + (mainView.view.Width / 2)) - (view.Width / 2);
            int y = (mainView.view.Location.Y + (mainView.view.Height / 2)) - (view.Height / 2);

            view.Location = new System.Drawing.Point(x, y);

            view.ForcedZOrder = 9999;
            view.Visible = true;

            ChildList = view != null ? (HudList)view["ChildList"] : new HudList();
            SettingsFormLayout = view != null ? (HudFixedLayout)view["SettingsForm"] : new HudFixedLayout();
            Cancel = view != null ? (HudButton)view["Cancel"] : new HudButton();
            AddOrUpdate = view != null ? (HudButton)view["AddOrUpdate"] : new HudButton();

            Cancel.Visible = false;

            Cancel.Hit += Cancel_Hit;
            AddOrUpdate.Hit += AddOrUpdate_Hit;
            ChildList.Click += ChildList_Click;

            Redraw();
        }

        private void ChildList_Click(object sender, int row, int col) {
            switch (col) {
                case 0: // edit
                    if (selectedIndex != -1) {
                        ((HudStaticText)((HudList.HudListRowAccessor)ChildList[selectedIndex])[0]).TextColor = view.Theme.GetColor("ListText");
                    }

                    selectedIndex = row;
                    ((HudStaticText)((HudList.HudListRowAccessor)ChildList[selectedIndex])[0]).TextColor = Color.Red;
                    form.SetValue(((HudStaticText)((HudList.HudListRowAccessor)ChildList[selectedIndex])[0]).Text);
                    AddOrUpdate.Text = "Update";
                    Cancel.Visible = true;
                    Redraw();
                    break;

                case 1: // delete
                    var bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod;
                    var value = prop.Property.GetValue(prop.Parent, null);
                    value.GetType().InvokeMember("RemoveAt", bindingFlags, null, value, new object[] { row });
                    ResetForm();
                    break;
            }
        }

        private void AddOrUpdate_Hit(object sender, EventArgs e) {
            try {
                var bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod;
                var value = prop.Property.GetValue(prop.Parent, null);

                if (selectedIndex != -1) {
                    value.GetType().InvokeMember("RemoveAt", bindingFlags, null, value, new object[] { selectedIndex });
                    value.GetType().InvokeMember("Insert", bindingFlags, null, value, new object[] { selectedIndex, form.Value });
                }
                else {
                    value.GetType().InvokeMember("Add", bindingFlags, null, value, new object[] { form.Value });
                    ChildList.ScrollPosition = ChildList.MaxScroll;
                }

                ResetForm();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Cancel_Hit(object sender, EventArgs e) {
            ResetForm();
        }

        private void ResetForm() {
            Cancel.Visible = false;
            AddOrUpdate.Text = "Add";
            selectedIndex = -1;
            Redraw();
        }

        private void Redraw() {
            var i = 0;
            var scrollPosition = ChildList.ScrollPosition;
            ChildList.ClearRows();

            if (form != null) {
                form.Dispose();
                form = null;
            }

            foreach (object child in (IEnumerable)prop.Property.GetValue(prop.Parent, null)) {
                var row = ChildList.AddRow();

                if (selectedIndex == i) {
                    ((HudStaticText)row[0]).TextColor = Color.Red;
                    form = new SettingsForm(Setting, SettingsFormLayout, prop.Object.GetType().GetGenericArguments().Single());
                }

                ((HudStaticText)row[0]).Text = child.ToString();
                //((HudPictureBox)row[1]).Image = 100673788;  // up arrow
                //((HudPictureBox)row[2]).Image = 100673789;  // down arrow
                ((HudPictureBox)row[1]).Image = 0x060011F8; // delete

                i++;
            }
            ChildList.ScrollPosition = scrollPosition;

            if (form == null) {
                form = new SettingsForm(Setting, SettingsFormLayout, prop.Object.GetType().GetGenericArguments().Single());
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    if (view != null) view.Dispose();
                    if (Cancel != null) Cancel.Hit -= Cancel_Hit;
                    if (AddOrUpdate != null) AddOrUpdate.Hit += AddOrUpdate_Hit;
                    if (ChildList != null) ChildList.Click += ChildList_Click;
                }
                disposed = true;
            }
        }
    }
}
