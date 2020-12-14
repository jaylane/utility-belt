using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using UtilityBelt.Views;
using VirindiViewService;
using VirindiViewService.Controls;

namespace UtilityBelt.Lib.Settings {
    public class SettingsForm : IDisposable {
        public HudFixedLayout ParentLayout;
        public string Setting;
        public object Value;
        public Type Type;

        public event EventHandler Changed;

        private List<HudControl> ChildViews = new List<HudControl>();

        public SettingsForm(string setting, HudFixedLayout parentLayout, Type type=null) {
            Setting = setting;
            ParentLayout = parentLayout;
            Type = type;

            if (Type == null) {
                Value = UtilityBeltPlugin.Instance.Settings.Get(setting).Setting;
                Type = ((ISetting)UtilityBeltPlugin.Instance.Settings.Get(Setting).Setting).GetValue().GetType();
            }
            else {
                Value = "";
            }

            ChildViews = DrawSettingsForm(parentLayout, setting);
        }

        internal void SetValue(string newValue) {
            try {
                Logger.WriteToChat("SetValue: " + newValue);
                if (Type == typeof(string)) {
                    Value = newValue;
                    Changed?.Invoke(this, null);
                }
                else if (Type == typeof(bool)) {
                    var lower = newValue.ToLower();
                    if (lower == "on" || lower == "true" || lower == "enabled") {
                        Value = true;
                    }
                    else {
                        Value = false;
                    }
                    Changed?.Invoke(this, null);
                }
                else if (Type == typeof(int) && int.TryParse(newValue, out int parsedInt)) {
                    Value = parsedInt;
                    Changed?.Invoke(this, null);
                }
                else if (Type == typeof(float) && float.TryParse(newValue, out float parsedFloat)) {
                    Value = parsedFloat;
                    Changed?.Invoke(this, null);
                }
                else if (Type == typeof(double) && double.TryParse(newValue, out double parsedDouble)) {
                    Value = parsedDouble;
                    Changed?.Invoke(this, null);
                }
                else if (Type == typeof(short) && short.TryParse(newValue, out short parsedShort)) {
                    Value = parsedShort;
                    Changed?.Invoke(this, null);
                }
                else {
                    Logger.Error($"Error, can't parse {Type}: {newValue}");
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

        private List<HudControl> DrawSettingsForm(HudFixedLayout settingsForm, string setting) {
            if (Type == typeof(bool)) {
                return DrawBooleanSettingsForm(settingsForm, setting);
            }
            else if (Type.IsEnum) {
                var prop = UtilityBeltPlugin.Instance.Settings.Get(setting);
                var supportsFlagsAttributes = prop.FieldInfo.GetCustomAttributes(typeof(SupportsFlagsAttribute), true);

                if (supportsFlagsAttributes.Length > 0) {
                    new EnumFlagEditor(UtilityBeltPlugin.Instance.MainView.view, setting);
                }
                else {
                    return DrawEnumSettingsForm(settingsForm, setting);
                }
            }
            else if (Type == typeof(int) && setting.Contains("Color")) {
                return DrawColorSettingsForm(settingsForm, setting);
            }
            else if (Type == typeof(int) || Type == typeof(float) || Type == typeof(double) || Type == typeof(short)) {
                return DrawNumberSettingsForm(settingsForm, setting);
            }
            else if (Type == typeof(string)) {
                return DrawStringSettingsForm(settingsForm, setting);
            }
            else if (Type.GetInterfaces().Contains(typeof(IEnumerable))) {
                new ListEditor(UtilityBeltPlugin.Instance.MainView, setting);
            }

            return new List<HudControl>();
        }

        private List<HudControl> DrawEnumSettingsForm(HudFixedLayout settingsForm, string setting) {
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

        private List<HudControl> DrawStringSettingsForm(HudFixedLayout settingsForm, string setting) {
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

            childViews.Add(edit);
            settingsForm.AddControl(edit, new Rectangle(0, 0, 350, 20));

            return childViews;
        }

        private List<HudControl> DrawColorSettingsForm(HudFixedLayout settingsForm, string setting) {
            var colorPickerPreview = new HudImageStack();
            var colorPickerRect = new Rectangle(0, 0, 20, 20);
            var childViews = new List<HudControl>();

            colorPickerPreview.Add(colorPickerRect, GetColorIcon((int)((ISetting)Value).GetValue()));

            var edit = new HudTextBox();
            edit.Text = ((int)((ISetting)Value).GetValue()).ToString("X8");
            edit.Change += (s, e) => {
                var type = Type;

                if (int.TryParse(edit.Text, System.Globalization.NumberStyles.HexNumber, null, out int parsedInt)) {
                    colorPickerPreview.Clear();
                    colorPickerPreview.Add(colorPickerRect, GetColorIcon((int)((ISetting)Value).GetValue()));
                    ((ISetting)Value).SetValue(parsedInt);
                    Changed?.Invoke(this, null);
                }
                else {
                    Logger.Error($"Can't parse hex: {edit.Text}");
                }
            };

            var pickerButton = new HudButton();
            pickerButton.Text = "Color Picker";
            pickerButton.Hit += (sender, evt) => {
                var originalColor = Color.FromArgb((int)((ISetting)Value).GetValue());
                var picker = new ColorPicker(UtilityBeltPlugin.Instance.MainView, "Test", originalColor);

                UtilityBeltPlugin.Instance.Settings.DisableSaving();

                picker.RaiseColorPickerCancelEvent += (s, e) => {
                    // restore color
                    edit.Text = originalColor.ToArgb().ToString("X8");
                    ((ISetting)Value).SetValue(originalColor.ToArgb());
                    Changed?.Invoke(this, null);
                    colorPickerPreview.Clear();
                    colorPickerPreview.Add(colorPickerRect, GetColorIcon(originalColor.ToArgb()));
                    UtilityBeltPlugin.Instance.Settings.EnableSaving();
                    picker.Dispose();
                };

                picker.RaiseColorPickerSaveEvent += (s, e) => {
                    ((ISetting)Value).SetValue(originalColor.ToArgb());
                    UtilityBeltPlugin.Instance.Settings.EnableSaving();
                    ((ISetting)Value).SetValue(e.Color.ToArgb());
                    Changed?.Invoke(this, null);
                    picker.Dispose();
                };

                picker.RaiseColorPickerChangeEvent += (s, e) => {
                    edit.Text = e.Color.ToArgb().ToString("X8");
                    ((ISetting)Value).SetValue(e.Color.ToArgb());
                    Changed?.Invoke(this, null);
                    colorPickerPreview.Clear();
                    colorPickerPreview.Add(colorPickerRect, GetColorIcon(e.Color.ToArgb()));
                };

                picker.view.VisibleChanged += (s, e) => {
                    // restore color
                    edit.Text = originalColor.ToArgb().ToString("X8");
                    ((ISetting)Value).SetValue(originalColor.ToArgb());
                    Changed?.Invoke(this, null);
                    colorPickerPreview.Clear();
                    colorPickerPreview.Add(colorPickerRect, GetColorIcon(originalColor.ToArgb()));
                    UtilityBeltPlugin.Instance.Settings.EnableSaving();
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

        private List<HudControl> DrawNumberSettingsForm(HudFixedLayout settingsForm, string setting) {
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

        private List<HudControl> DrawBooleanSettingsForm(HudFixedLayout settingsForm, string setting) {
            var enabled = new HudCheckBox();
            var disabled = new HudCheckBox();
            var childViews = new List<HudControl>();

            enabled.Text = "True";
            enabled.Checked = (bool)((ISetting)Value).GetValue();
            enabled.Change += (s, e) => {
                disabled.Checked = !enabled.Checked;
                Value = enabled.Checked;
                Changed?.Invoke(this, null);
            };
            disabled.Text = "False";
            disabled.Checked = !(bool)((ISetting)Value).GetValue();
            disabled.Change += (s, e) => {
                enabled.Checked = !disabled.Checked;
                Value = !disabled.Checked;
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
        private static ACImage colorIcon;
        private static Bitmap colorPreviewBitmap;

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
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
