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
using UBService.Lib.Settings;

namespace UtilityBelt.Views {

    public class ListEditor<T> : IDisposable {
        public VirindiViewService.ViewProperties properties;
        public VirindiViewService.ControlGroup controls;
        public VirindiViewService.HudView view;
        bool disposed = false;

        private HudList ChildList;
        private HudFixedLayout SettingsFormLayout;
        private HudButton Cancel;
        private HudButton AddOrUpdate;
        private MainView mainView;
        private int selectedIndex = -1;
        private SettingsForm form;
        private ISetting Setting;
        private ObservableCollection<T> Collection;

        public ListEditor(MainView mainView, ISetting setting) {
            this.mainView = mainView;
            Setting = setting;
            Collection = setting.GetValue() as ObservableCollection<T>;

            VirindiViewService.XMLParsers.Decal3XMLParser parser = new VirindiViewService.XMLParsers.Decal3XMLParser();
            parser.ParseFromResource("UtilityBelt.Views.ListEditor.xml", out properties, out controls);

            view = new VirindiViewService.HudView(properties, controls);

            view.Title = $"Editing {Setting.Name}";

            int x = (mainView.view.Location.X + (mainView.view.Width / 2)) - (view.Width / 2);
            int y = (mainView.view.Location.Y + (mainView.view.Height / 2)) - (view.Height / 2);

            view.Location = new System.Drawing.Point(x, y);

            view.ForcedZOrder = 9999;
            view.Visible = true;

            ChildList = (HudList)view["ChildList"];
            SettingsFormLayout = (HudFixedLayout)view["SettingsForm"];
            Cancel = (HudButton)view["Cancel"];
            AddOrUpdate = (HudButton)view["AddOrUpdate"];

            Cancel.Visible = false;

            Cancel.Hit += Cancel_Hit;
            AddOrUpdate.Hit += AddOrUpdate_Hit;
            ChildList.Click += ChildList_Click;

            view.VisibleChanged += View_VisibleChanged;

            Redraw();
        }

        private void View_VisibleChanged(object sender, EventArgs e) {
            Dispose();
        }

        private void ChildList_Click(object sender, int row, int col) {
            try {
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
                        Collection.RemoveAt(row);
                        ResetForm();
                        break;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void AddOrUpdate_Hit(object sender, EventArgs e) {
            try {
                if (selectedIndex != -1) {
                    Collection.RemoveAt(selectedIndex);
                    Collection.Insert(selectedIndex, (T)form.Value);
                }
                else {
                    Collection.Add((T)form.Value);
                }

                ResetForm();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Cancel_Hit(object sender, EventArgs e) {
            try {
                ResetForm();
            }
            catch (Exception ex) { Logger.LogException(ex); }
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

            foreach (var child in Collection) {
                var row = ChildList.AddRow();

                if (selectedIndex == i) {
                    ((HudStaticText)row[0]).TextColor = Color.Red;
                    form = new SettingsForm(Setting, SettingsFormLayout, typeof(T), Collection[selectedIndex]);
                }

                ((HudStaticText)row[0]).Text = child.ToString();
                //((HudPictureBox)row[1]).Image = 100673788;  // up arrow
                //((HudPictureBox)row[2]).Image = 100673789;  // down arrow
                ((HudPictureBox)row[1]).Image = 0x060011F8; // delete

                i++;
            }
            ChildList.ScrollPosition = scrollPosition;

            if (form == null) {
                if (typeof(T).GetConstructor(new Type[0]) != null) {
                    object newInstance = Activator.CreateInstance(typeof(T));
                    form = new SettingsForm(Setting, SettingsFormLayout, typeof(T), newInstance);
                }
                else {
                    form = new SettingsForm(Setting, SettingsFormLayout, typeof(T), "");
                }
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    view.VisibleChanged -= View_VisibleChanged;

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
