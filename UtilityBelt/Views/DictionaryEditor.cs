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
using UBLoader.Lib.Settings;
using Hellosam.Net.Collections;

namespace UtilityBelt.Views {

    public class DictionaryEditor : IDisposable {
        public VirindiViewService.ViewProperties properties;
        public VirindiViewService.ControlGroup controls;
        public VirindiViewService.HudView view;
        bool disposed = false;

        public enum Columns : int {
            Key = 0,
            Value = 1,
            Delete = 2
        };

        private HudList ChildList;
        private HudFixedLayout SettingsFormLayout;
        private HudButton Cancel;
        private HudButton AddOrUpdate;
        private MainView mainView;
        private string selectedKey = "";
        private SettingsForm form;
        private Setting<ObservableDictionary<string, string>> Setting;

        public DictionaryEditor(MainView mainView, ISetting setting) {
            this.mainView = mainView;
            Setting = setting as Setting<ObservableDictionary<string, string>>;

            VirindiViewService.XMLParsers.Decal3XMLParser parser = new VirindiViewService.XMLParsers.Decal3XMLParser();
            parser.ParseFromResource("UtilityBelt.Views.DictionaryEditor.xml", out properties, out controls);

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
                HudList.HudListRowAccessor selectedRow = ChildList[row];
                var keyCol = (HudStaticText)selectedRow[(int)Columns.Key];
                var valCol = (HudStaticText)selectedRow[(int)Columns.Value];
                switch ((Columns)col) {
                    case Columns.Key:
                    case Columns.Value: // edit
                        if (selectedKey != "") {
                            keyCol.TextColor = view.Theme.GetColor("ListText");
                            valCol.TextColor = view.Theme.GetColor("ListText");
                        }

                        selectedKey = keyCol.Text;
                        keyCol.TextColor = Color.Red;
                        valCol.TextColor = Color.Red;
                        form.SetValue(new KeyValuePair<string, string>(keyCol.Text, valCol.Text));
                        AddOrUpdate.Text = "Update";
                        Cancel.Visible = true;
                        Redraw();
                        break;

                    case Columns.Delete: // delete
                        Setting.Value.Remove(keyCol.Text);
                        ResetForm();
                        break;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void AddOrUpdate_Hit(object sender, EventArgs e) {
            try {
                KeyValuePair<string, string> kv = (KeyValuePair<string, string>)form.Value;

                if (selectedKey != "") {
                    Setting.Value.Remove(selectedKey);
                }

                Setting.Value.Remove(kv.Key);
                Setting.Value.Add(kv.Key, kv.Value);

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
            selectedKey = "";
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

            foreach (var key in Setting.Value.Keys) {
                var row = ChildList.AddRow();

                if (selectedKey == key) {
                    ((HudStaticText)row[(int)Columns.Key]).TextColor = Color.Red;
                    ((HudStaticText)row[(int)Columns.Value]).TextColor = Color.Red;
                    form = new SettingsForm(Setting, SettingsFormLayout, typeof(KeyValuePair<string, string>), new KeyValuePair<string, string>(key, Setting.Value[key]));
                }

                ((HudStaticText)row[(int)Columns.Key]).Text = key;
                ((HudStaticText)row[(int)Columns.Value]).Text = Setting.Value[key];
                ((HudStaticText)row[(int)Columns.Value]).TextAlignment = WriteTextFormats.Right;
                ((HudPictureBox)row[(int)Columns.Delete]).Image = 0x060011F8; // delete

                i++;
            }
            ChildList.ScrollPosition = scrollPosition;

            if (form == null) {
                form = new SettingsForm(Setting, SettingsFormLayout, typeof(KeyValuePair<string, string>), new KeyValuePair<string, string>("",""));
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
