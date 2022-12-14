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
using UtilityBelt.Service.Lib.Settings;
using Hellosam.Net.Collections;
using UBLoader.Lib;

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
        private ISetting Setting;
        private Type keyType;
        private Type valueType;

        public DictionaryEditor(MainView mainView, ISetting setting) {
            this.mainView = mainView;
            Setting = setting;
            Type[] arguments = setting.GetValue().GetType().GetGenericArguments();
            keyType = arguments[0];
            valueType = arguments[1];

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

                        // ehhhhhh
                        if (keyType == typeof(string) && valueType == typeof(string)) {
                            form.SetValue(new KeyValuePair<string, string>(keyCol.Text, valCol.Text));
                        }
                        else if (keyType == typeof(XpTarget) && valueType == typeof(double)) {
                            var k = (XpTarget)Enum.Parse(typeof(XpTarget), selectedKey);
                            var v = (double)(Setting.GetValue() as ObservableDictionary<XpTarget, double>)[k];
                            form.SetValue(new KeyValuePair<XpTarget, double>(k, v));
                        }

                        AddOrUpdate.Text = "Update";
                        Cancel.Visible = true;
                        Redraw();
                        break;

                    case Columns.Delete: // delete
                        RemoveKey(keyCol.Text);
                        ResetForm();
                        break;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void AddOrUpdate_Hit(object sender, EventArgs e) {
            try {
                if (selectedKey != "") {
                    RemoveKey(selectedKey);
                }

                // ehhhhhh
                if (keyType == typeof(string) && valueType == typeof(string)) {
                    KeyValuePair<string, string> kv = (KeyValuePair<string, string>)form.Value;
                    RemoveKey(kv.Key);
                    AddKey(kv.Key, kv.Value);
                }
                else if (keyType == typeof(XpTarget) && valueType == typeof(double)) {
                    KeyValuePair<XpTarget, double> kv = (KeyValuePair<XpTarget, double>)form.Value;
                    Logger.WriteToChat($"kv is {kv.Key} -> {kv.Value}");
                    RemoveKey(kv.Key);
                    AddKey(kv.Key, kv.Value);
                }

                ResetForm();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private object GetKeyValue(object key) {
            // ehhhhhh
            if (keyType == typeof(string) && valueType == typeof(string)) {
                return (Setting.GetValue() as ObservableDictionary<string, string>)[(string)key];
            }
            else if (keyType == typeof(XpTarget) && valueType == typeof(double)) {
                var k = key.GetType() == typeof(string) ? (XpTarget)Enum.Parse(typeof(XpTarget), (string)key) : (XpTarget)key;
                return (Setting.GetValue() as ObservableDictionary<XpTarget, double>)[k];
            }

            return null;
        }

        private void RemoveKey(object key) {
            // ehhhhhh
            if (keyType == typeof(string) && valueType == typeof(string)) {
                (Setting.GetValue() as ObservableDictionary<string, string>).Remove((string)key);
            }
            else if (keyType == typeof(XpTarget) && valueType == typeof(double)) {
                var k = key.GetType() == typeof(string) ? (XpTarget)Enum.Parse(typeof(XpTarget), (string)key) : (XpTarget)key;
                (Setting.GetValue() as ObservableDictionary<XpTarget, double>).Remove(k);
            }
        }

        private void AddKey(object key, object value) {
            // ehhhhhh
            if (keyType == typeof(string) && valueType == typeof(string)) {
                (Setting.GetValue() as ObservableDictionary<string, string>).Add((string)key, (string)value);
            }
            else if (keyType == typeof(XpTarget) && valueType == typeof(double)) {
                var k = key.GetType() == typeof(string) ? (XpTarget)Enum.Parse(typeof(XpTarget), (string)key) : (XpTarget)key;
                (Setting.GetValue() as ObservableDictionary<XpTarget, double>).Add(k, (double)value);
            }
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

            IEnumerable<object> keys = null;

            // ehhhhhh
            if (keyType == typeof(string) && valueType == typeof(string)) {
                keys = (Setting.GetValue() as ObservableDictionary<string, string>).Keys.Select(k => (object)k);
            }
            else if (keyType == typeof(XpTarget) && valueType == typeof(double)) {
                keys = (Setting.GetValue() as ObservableDictionary<XpTarget, double>).Keys.Select(k => (object)k);
            }

            if (keys != null) {
                foreach (var key in keys) {
                    var row = ChildList.AddRow();

                    if (selectedKey == key.ToString()) {
                        ((HudStaticText)row[(int)Columns.Key]).TextColor = Color.Red;
                        ((HudStaticText)row[(int)Columns.Value]).TextColor = Color.Red;

                        // ehhhhhh
                        if (keyType == typeof(string) && valueType == typeof(string)) {
                            form = new SettingsForm(Setting, SettingsFormLayout, typeof(KeyValuePair<string, string>), new KeyValuePair<string, string>((string)key, (string)GetKeyValue(key)));
                        }
                        else if (keyType == typeof(XpTarget) && valueType == typeof(double)) {
                            form = new SettingsForm(Setting, SettingsFormLayout, typeof(KeyValuePair<XpTarget, double>), new KeyValuePair<XpTarget, double>((XpTarget)key, (double)GetKeyValue(key)));
                        }
                    }

                    ((HudStaticText)row[(int)Columns.Key]).Text = key.ToString();
                    ((HudStaticText)row[(int)Columns.Value]).Text = GetKeyValue(key).ToString();
                    ((HudStaticText)row[(int)Columns.Value]).TextAlignment = WriteTextFormats.Right;
                    ((HudPictureBox)row[(int)Columns.Delete]).Image = 0x060011F8; // delete

                    i++;
                }
            }
            ChildList.ScrollPosition = scrollPosition;

            if (form == null) {
                // ehhhhhh
                if (keyType == typeof(string) && valueType == typeof(string)) {
                    form = new SettingsForm(Setting, SettingsFormLayout, typeof(KeyValuePair<string, string>), new KeyValuePair<string, string>("", ""));
                }
                else if (keyType == typeof(XpTarget) && valueType == typeof(double)) {
                    form = new SettingsForm(Setting, SettingsFormLayout, typeof(KeyValuePair<XpTarget, double>), new KeyValuePair<XpTarget, double>(XpTarget.Alchemy, 0));
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
