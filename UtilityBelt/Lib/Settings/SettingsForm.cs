using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using UtilityBelt.Views;
using VirindiViewService;
using VirindiViewService.Controls;
using UBLoader.Lib.Settings;
using UtilityBelt.Lib.Settings;
using UBLoader.Lib;

namespace UtilityBelt.Lib.Settings {
    public class SettingsForm : IDisposable {
        public HudFixedLayout ParentLayout;
        public ISetting Setting;
        public object Value;
        public Type Type;

        public event EventHandler Changed;

        private List<HudControl> ChildViews = new List<HudControl>();
        private static ACImage colorIcon;
        private static Bitmap colorPreviewBitmap;
        private LongStringEditor longStringEditor;

        public SettingsForm(ISetting setting, HudFixedLayout parentLayout, Type type=null, object value=null) {
            Setting = setting;
            ParentLayout = parentLayout;
            Type = type == null ? Setting.GetValue().GetType() : type;
            Value = value == null ? setting.GetValue() : value;

            ChildViews = DrawSettingsForm(parentLayout, Setting);
        }

        internal void SetValue(object newValue) {
            try {
                if (Type == typeof(string)) {
                    Value = ((string)newValue);
                    Changed?.Invoke(this, null);
                }
                else if (Type == typeof(bool)) {
                    var lower = ((string)newValue).ToLower();
                    if (lower == "on" || lower == "true" || lower == "enabled") {
                        Value = true;
                    }
                    else {
                        Value = false;
                    }
                    Changed?.Invoke(this, null);
                }
                else if (Type == typeof(int) && int.TryParse(((string)newValue), out int parsedInt)) {
                    Value = parsedInt;
                    Changed?.Invoke(this, null);
                }
                else if (Type == typeof(float) && float.TryParse(((string)newValue), out float parsedFloat)) {
                    Value = parsedFloat;
                    Changed?.Invoke(this, null);
                }
                else if (Type == typeof(double) && double.TryParse(((string)newValue), out double parsedDouble)) {
                    Value = parsedDouble;
                    Changed?.Invoke(this, null);
                }
                else if (Type == typeof(short) && short.TryParse(((string)newValue), out short parsedShort)) {
                    Value = parsedShort;
                    Changed?.Invoke(this, null);
                }
                else {
                    Value = newValue;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public static ACImage GetColorIcon(int v) {
            if (colorPreviewBitmap != null)
                colorPreviewBitmap.Dispose();
            colorPreviewBitmap = new Bitmap(32, 32);

            using (Graphics gfx = Graphics.FromImage(colorPreviewBitmap)) {
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(v))) {
                    gfx.FillRectangle(brush, 0, 0, 32, 32);
                }
            }
            if (colorIcon != null)
                colorIcon.Dispose();
            colorIcon = new ACImage(colorPreviewBitmap);

            return colorIcon;
        }

        private List<HudControl> DrawSettingsForm(HudFixedLayout settingsForm, ISetting setting) {
            //todo: fix this
            if (Type == typeof(bool)) {
                return DrawBooleanSettingsForm(settingsForm);
            }
            else if (Type.IsEnum) {
                var supportsFlagsAttributes = Setting.FieldInfo.GetCustomAttributes(typeof(SupportsFlagsAttribute), true);

                if (supportsFlagsAttributes.Length > 0) {
                    new EnumFlagEditor(UtilityBeltPlugin.Instance.MainView.view, setting);
                }
                else {
                    return DrawEnumSettingsForm(settingsForm);
                }
            }
            else if (Type == typeof(int) && setting.Name.Contains("Color")) {
                return DrawColorSettingsForm(settingsForm);
            }
            else if (Type == typeof(int) || Type == typeof(float) || Type == typeof(double) || Type == typeof(short)) {
                return DrawNumberSettingsForm(settingsForm);
            }
            else if (Type == typeof(string)) {
                return DrawStringSettingsForm(settingsForm);
            }
            else if (Type == typeof(KeyValuePair<string, string>)) {
                return DrawKeyValueSettingsForm(settingsForm);
            }
            else if (Type == typeof(KeyValuePair<XpTarget, double>)) {
                return DrawKeyValueSettingsForm(settingsForm);
            }
            else if (Type == typeof(TrackedItem)) {
                return DrawTrackedItemSettingsForm(settingsForm);
            }
            else if (Type == typeof(System.Collections.ObjectModel.ObservableCollection<string>)) {
                new ListEditor<string>(UtilityBeltPlugin.Instance.MainView, setting);
            }
            else if (Type == typeof(System.Collections.ObjectModel.ObservableCollection<TrackedItem>)) {
                new ListEditor<TrackedItem>(UtilityBeltPlugin.Instance.MainView, setting);
            }
            else if (Type == typeof(Hellosam.Net.Collections.ObservableDictionary<string, string>)) {
                new DictionaryEditor(UtilityBeltPlugin.Instance.MainView, setting);
            }
            else if (Type == typeof(Hellosam.Net.Collections.ObservableDictionary<XpTarget, double>)) {
                new DictionaryEditor(UtilityBeltPlugin.Instance.MainView, setting);
            }

            return new List<HudControl>();
        }

        private List<HudControl> DrawTrackedItemSettingsForm(HudFixedLayout settingsForm) {
            var childViews = new List<HudControl>();
            var edit = new HudTextBox();
            edit.Text = Value.ToString();
            edit.Change += (s, e) => {
                Value = edit.Text;
                Changed?.Invoke(this, null);
            };

            Changed += (s, e) => {
                edit.Text = Value.ToString();
            };

            var button = new HudButton();
            button.Text = "Selected";
            button.Hit += (s, e) => {
                var selected = UtilityBeltPlugin.Instance.Core.Actions.CurrentSelection;
                if (!UtilityBeltPlugin.Instance.Core.Actions.IsValidObject(selected)) {
                    Logger.Error($"Nothing selected!");
                    return;
                }
                var item = new UBHelper.Weenie(selected);
                ((TrackedItem)Value).Icon = item.Icon;
                ((TrackedItem)Value).Name = item.GetName(UBHelper.NameType.NAME_SINGULAR);
                edit.Text = item.GetName(UBHelper.NameType.NAME_SINGULAR);
            };

            childViews.Add(edit);
            childViews.Add(button);
            settingsForm.AddControl(edit, new Rectangle(0, 0, 220, 20));
            settingsForm.AddControl(button, new Rectangle(224, 0, 120, 20));

            return childViews;
        }

        private List<HudControl> DrawEnumSettingsForm(HudFixedLayout settingsForm) {
            var childViews = new List<HudControl>();
            var combo = new HudCombo(new ControlGroup());
            var values = Enum.GetValues(Type);

            for (var i = 0; i < values.Length; i++) {
                // this could probably be improved, but we want to sort this list
                bool didAdd = false;
                for (var y = 0; y < combo.Count; y++) {
                    if (string.Compare(values.GetValue(i).ToString(), ((HudStaticText)combo[y]).Text) < 0) {
                        combo.InsertItem(y, values.GetValue(i).ToString(), values.GetValue(i).ToString());
                        didAdd = true;
                        break;
                    }
                }

                if (!didAdd) {
                    combo.AddItem(values.GetValue(i).ToString(), values.GetValue(i).ToString());
                    if (values.GetValue(i).ToString() == Value.ToString()) {
                        combo.Current = combo.Count;
                    }
                }
            }

            for (var i = 0; i < combo.Count; i++) {
                if (((HudStaticText)combo[i]).Text == Value.ToString()) {
                    combo.Current = i;
                }
            }

            combo.Change += (s, e) => {
                try {
                    Value = Enum.ToObject(Type, Enum.Parse(Type, ((HudStaticText)combo[combo.Current]).Text));
                    Changed?.Invoke(this, null);
                }
                catch (Exception ex) {
                    Logger.LogException(ex);
                    Logger.Error($"Invalid option selected: {((HudStaticText)combo[combo.Current]).Text}");
                }
            };

            childViews.Add(combo);
            settingsForm.AddControl(combo, new Rectangle(0, 0, 180, 20));

            return childViews;
        }

        private List<HudControl> DrawStringSettingsForm(HudFixedLayout settingsForm) {
            var childViews = new List<HudControl>();
            var edit = new HudTextBox();
            edit.Text = Value.ToString();
            edit.Change += (s, e) => {
                Value = edit.Text;
                Changed?.Invoke(this, null);
            };

            Changed += (s, e) => {
                edit.Text = Value.ToString();
            };

            var button = new HudButton();
            button.Text = "E";
            button.Hit += (s, e) => {
                if (longStringEditor == null || longStringEditor.IsDisposed) {
                    longStringEditor = new LongStringEditor(UtilityBeltPlugin.Instance.MainView.view, edit);
                    longStringEditor.Saved += (s2, e2) => {
                        Value = edit.Text;
                        Changed?.Invoke(this, null);
                    };
                }
            };

            childViews.Add(edit);
            childViews.Add(button);
            settingsForm.AddControl(edit, new Rectangle(0, 0, 320, 20));
            settingsForm.AddControl(button, new Rectangle(324, 0, 20, 20));

            return childViews;
        }

        private List<HudControl> DrawKeyValueSettingsForm(HudFixedLayout settingsForm) {
            var childViews = new List<HudControl>();
            var keyLabel = new HudStaticText();
            var valueLabel = new HudStaticText();

            keyLabel.Text = "Key:";
            valueLabel.Text = "Value:";
            Type[] arguments = Value.GetType().GetGenericArguments();
            var keyType = arguments[0];
            var valueType = arguments[1];
            var valueEdit = new HudTextBox();

            if (keyType == typeof(string) && valueType == typeof(string)) {
                var keyEdit = new HudTextBox();
                keyEdit.Text = ((KeyValuePair<string, string>)Value).Key;
                keyEdit.Change += (s, e) => {
                    Value = new KeyValuePair<string, string>(keyEdit.Text, valueEdit.Text);
                    Changed?.Invoke(this, null);
                };
                valueEdit.Text = ((KeyValuePair<string, string>)Value).Value;
                valueEdit.Change += (s, e) => {
                    Value = new KeyValuePair<string, string>(keyEdit.Text, valueEdit.Text);
                    Changed?.Invoke(this, null);
                };

                Changed += (s, e) => {
                    keyEdit.Text = ((KeyValuePair<string, string>)Value).Key;
                    valueEdit.Text = ((KeyValuePair<string, string>)Value).Value;
                };

                childViews.Add(keyEdit);
                childViews.Add(valueEdit);
                settingsForm.AddControl(keyEdit, new Rectangle(50, 0, 300, 20));
                settingsForm.AddControl(valueEdit, new Rectangle(50, 20, 300, 20));
            }
            else if (keyType == typeof(XpTarget) && valueType == typeof(double)) {
                var combo = new HudCombo(new ControlGroup());
                var values = Enum.GetValues(keyType);

                for (var i = 0; i < values.Length; i++) {
                    // this could probably be improved, but we want to sort this list
                    bool didAdd = false;
                    for (var y = 0; y < combo.Count; y++) {
                        if (string.Compare(values.GetValue(i).ToString(), ((HudStaticText)combo[y]).Text) < 0) {
                            combo.InsertItem(y, values.GetValue(i).ToString(), values.GetValue(i).ToString());
                            didAdd = true;
                            break;
                        }
                    }

                    if (!didAdd) {
                        combo.AddItem(values.GetValue(i).ToString(), values.GetValue(i).ToString());
                        if (values.GetValue(i).ToString() == ((KeyValuePair<XpTarget, double>)Value).Key.ToString()) {
                            combo.Current = combo.Count;
                        }
                    }
                }

                for (var i = 0; i < combo.Count; i++) {
                    if (((HudStaticText)combo[i]).Text == ((KeyValuePair<XpTarget, double>)Value).Key.ToString()) {
                        combo.Current = i;
                    }
                }

                combo.Change += (s, e) => {
                    try {
                        Value = Enum.ToObject(keyType, Enum.Parse(keyType, ((HudStaticText)combo[combo.Current]).Text));
                        Changed?.Invoke(this, null);
                    }
                    catch (Exception ex) {
                        Logger.LogException(ex);
                        Logger.Error($"Invalid option selected: {((HudStaticText)combo[combo.Current]).Text}");
                    }
                };

                valueEdit.Text = ((KeyValuePair<XpTarget, double>)Value).Value.ToString();
                valueEdit.Change += (s, e) => {
                    if (!double.TryParse(valueEdit.Text, out double pv)) {
                        Logger.Error($"Could not parse number value: {valueEdit.Text}");
                        return;
                    }
                    Value = new KeyValuePair<XpTarget, double>((XpTarget)Enum.Parse(typeof(XpTarget), ((HudStaticText)combo[combo.Current]).Text), pv);
                    Changed?.Invoke(this, null);
                };

                childViews.Add(valueEdit);
                settingsForm.AddControl(valueEdit, new Rectangle(50, 20, 300, 20));
                childViews.Add(combo);
                settingsForm.AddControl(combo, new Rectangle(50, 0, 300, 20));
            }
            childViews.Add(keyLabel);
            childViews.Add(valueLabel);
            settingsForm.AddControl(keyLabel, new Rectangle(5, 0, 40, 20));
            settingsForm.AddControl(valueLabel, new Rectangle(5, 20, 40, 20));

            return childViews;
        }

        private List<HudControl> DrawColorSettingsForm(HudFixedLayout settingsForm) {
            var colorPickerPreview = new HudImageStack();
            var colorPickerRect = new Rectangle(0, 0, 20, 20);
            var childViews = new List<HudControl>();

            colorPickerPreview.Add(colorPickerRect, GetColorIcon((int)Value));

            var edit = new HudTextBox();
            edit.Text = ((int)Value).ToString("X8");
            edit.Change += (s, e) => {
                var type = Type;

                if (int.TryParse(edit.Text, System.Globalization.NumberStyles.HexNumber, null, out int parsedInt)) {
                    colorPickerPreview.Clear();
                    colorPickerPreview.Add(colorPickerRect, GetColorIcon((int)Value));
                    Setting.SetValue(parsedInt);
                    Value = parsedInt;
                    Changed?.Invoke(this, null);
                }
                else {
                    Logger.Error($"Can't parse hex: {edit.Text}");
                }
            };

            var pickerButton = new HudButton();
            pickerButton.Text = "Color Picker";
            pickerButton.Hit += (sender, evt) => {
                var originalColor = Color.FromArgb((int)Value);
                var picker = new ColorPicker(UtilityBeltPlugin.Instance.MainView, "Test", originalColor);

                Setting.Settings.DisableSaving();

                picker.RaiseColorPickerCancelEvent += (s, e) => {
                    // restore color
                    edit.Text = originalColor.ToArgb().ToString("X8");
                    Setting.SetValue(originalColor.ToArgb());
                    Value = originalColor.ToArgb();
                    Changed?.Invoke(this, null);
                    colorPickerPreview.Clear();
                    colorPickerPreview.Add(colorPickerRect, GetColorIcon(originalColor.ToArgb()));
                    Setting.Settings.EnableSaving();
                    picker.Dispose();
                };

                picker.RaiseColorPickerSaveEvent += (s, e) => {
                    Setting.SetValue(originalColor.ToArgb());
                    Value = originalColor.ToArgb();
                    Setting.Settings.EnableSaving();
                    Setting.SetValue(e.Color.ToArgb());
                    Value = e.Color.ToArgb();
                    Changed?.Invoke(this, null);
                    picker.Dispose();
                };

                picker.RaiseColorPickerChangeEvent += (s, e) => {
                    edit.Text = e.Color.ToArgb().ToString("X8");
                    Setting.SetValue(e.Color.ToArgb());
                    Value = e.Color.ToArgb();
                    Changed?.Invoke(this, null);
                    colorPickerPreview.Clear();
                    colorPickerPreview.Add(colorPickerRect, GetColorIcon(e.Color.ToArgb()));
                };

                picker.view.VisibleChanged += (s, e) => {
                    // restore color
                    edit.Text = originalColor.ToArgb().ToString("X8");
                    Setting.SetValue(originalColor.ToArgb());
                    Value = originalColor.ToArgb();
                    Changed?.Invoke(this, null);
                    colorPickerPreview.Clear();
                    colorPickerPreview.Add(colorPickerRect, GetColorIcon(originalColor.ToArgb()));
                    Setting.Settings.EnableSaving();
                    if (!picker.view.Visible) {
                        picker.Dispose();
                    }
                };
            };

            childViews.Add(colorPickerPreview);
            childViews.Add(edit);
            childViews.Add(pickerButton);
            settingsForm.AddControl(colorPickerPreview, new Rectangle(0, 0, 20, 20));
            settingsForm.AddControl(edit, new Rectangle(25, 0, 150, 20));
            settingsForm.AddControl(pickerButton, new Rectangle(180, 0, 80, 20));

            return childViews;
        }

        private List<HudControl> DrawNumberSettingsForm(HudFixedLayout settingsForm) {
            var childViews = new List<HudControl>();
            var edit = new HudTextBox();
            edit.Text = Value.ToString();
            edit.Change += (s, e) => {
                if (Type == typeof(int) && int.TryParse(edit.Text, out int parsedInt)) {
                    Value = parsedInt;
                    Changed?.Invoke(this, null);
                }
                else if (Type == typeof(float) && float.TryParse(edit.Text, out float parsedFloat)) {
                    Value = parsedFloat;
                    Changed?.Invoke(this, null);
                }
                else if (Type == typeof(double) && double.TryParse(edit.Text, out double parsedDouble)) {
                    Value = parsedDouble;
                    Changed?.Invoke(this, null);
                }
                else if (Type == typeof(short) && short.TryParse(edit.Text, out short parsedShort)) {
                    Value = parsedShort;
                    Changed?.Invoke(this, null);
                }
                else {
                    Logger.Error($"Error, can't parse {Type}: {edit.Text}");
                }
            };

            childViews.Add(edit);
            settingsForm.AddControl(edit, new Rectangle(0, 0, 150, 20));

            return childViews;
        }

        private List<HudControl> DrawBooleanSettingsForm(HudFixedLayout settingsForm) {
            var enabled = new HudCheckBox();
            var disabled = new HudCheckBox();
            var childViews = new List<HudControl>();

            enabled.Text = "True";
            enabled.Checked = (bool)Value;
            enabled.Change += (s, e) => {
                disabled.Checked = !enabled.Checked;
                Setting.SetValue(enabled.Checked);
                Value = enabled.Checked;
                Changed?.Invoke(this, null);
            };
            disabled.Text = "False";
            disabled.Checked = !(bool)Value;
            disabled.Change += (s, e) => {
                enabled.Checked = !disabled.Checked;
                Setting.SetValue(!disabled.Checked);
                Value = enabled.Checked;
                Changed?.Invoke(this, null);
            };

            childViews.Add(enabled);
            childViews.Add(disabled);

            settingsForm.AddControl(enabled, new Rectangle(0, 0, 50, 20));
            settingsForm.AddControl(disabled, new Rectangle(55, 0, 50, 20));

            return childViews;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    longStringEditor?.Dispose();
                    foreach (var view in ChildViews) {
                        try {
                            view.Visible = false;
                            view.Dispose();
                        }
                        catch { }
                    }
                    if (colorIcon != null)
                        colorIcon.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose() {
            Dispose(true);
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
