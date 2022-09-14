using ImGuiNET;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UBService.Lib.Settings;
using UBService.Views;

namespace UBService.Views.SettingsEditor {
    /// <summary>
    /// A hud that allows easy editing of UBService.Lib.Settings
    /// </summary>
    public class SettingsEditor : IDisposable {
        internal static uint MAX_STRING_LENGTH = 10000;
        private bool isDisposed = false;
        public Hud Hud { get; private set; }
        private static uint _id = 0;
        private List<ISetting> childSettings = new List<ISetting>();
        private int _selectedChildSettingIndex;
        private ISetting _selectedSetting = null;
        private Dictionary<string, byte[]> _stringInputBuffers = new Dictionary<string, byte[]>();
        private List<SettingsListEditor> listEditors = new List<SettingsListEditor>();

        /// <summary>
        /// Name of this settings editor, used it the title
        /// </summary>
        public string Name { get; }
        public object ParentObject { get; }

        /// <summary>
        /// The settings containers being edited
        /// </summary>
        public IEnumerable<object> SettingsContainers { get; }

        /// <summary>
        /// Create and display a new settings editor window.
        /// </summary>
        /// <param name="name">The name (shown in window title)</param>
        /// <param name="settingsContainers">The settings object to edit</param> 
        /// <param name="icon">The icon to use</param> 
        public SettingsEditor(string name, object parent, IEnumerable<object> settingsContainers, Bitmap icon = null) {
            Name = name;
            ParentObject = parent;
            SettingsContainers = settingsContainers.ToArray();

            if (icon == null) {
                using (Stream manifestResourceStream = GetType().Assembly.GetManifestResourceStream("UBService.Resources.icons.settings-editor.png")) {
                    icon = new Bitmap(manifestResourceStream);
                }
            }

            using (Stream manifestResourceStream = GetType().Assembly.GetManifestResourceStream("UBService.Resources.icons.info.png")) {
                infoIcon = new ManagedTexture(manifestResourceStream);
            }

            Hud = HudManager.CreateHud($"SettingsEditor: {Name}##SettingsEditor{_id++}", icon);

            Hud.Title = Name;
            Hud.Render += Hud_Render;
            Hud.PreRender += Hud_PreRender;

            foreach (var container in SettingsContainers) {
                IEnumerable<ISetting> containerSettings = GetContainerSettings(container);
                
                if (containerSettings != null) {
                    childSettings.AddRange(containerSettings);
                } 
            }
        }

        private void Hud_PreRender(object sender, EventArgs e) {
            ImGui.SetNextWindowSizeConstraints(new Vector2(550, 250), new Vector2(float.MaxValue, float.MaxValue));
        }

        private void Hud_Render(object sender, EventArgs e) {
            if (childSettings.Count > 1) {
                ImGui.BeginTable("Settings", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.NoSavedSettings);
                {
                    ImGui.TableSetupColumn("ObjectTree", ImGuiTableColumnFlags.WidthFixed, 150);
                    ImGui.TableNextRow();

                    ImGui.TableSetColumnIndex(0);
                    ImGui.BeginChild("ObjectTree", new Vector2(-1, ImGui.GetContentRegionAvail().Y - 4)); // object tree 
                    try {
                        RenderSettingCategories();
                    }
                    catch (Exception ex) { UBService.LogException(ex); }
                    ImGui.EndChild(); // object tree

                    ImGui.TableSetColumnIndex(1);
                    ImGui.BeginChild("ObjectInfo", new Vector2(-1, ImGui.GetContentRegionAvail().Y - 4)); // object info
                    try {
                        RenderSettingsDetails();
                    }
                    catch (Exception ex) { UBService.LogException(ex); }
                    ImGui.EndChild(); // object info
                }
                ImGui.EndTable();
            }
            else {
                try {
                    RenderSettingsDetails();
                }
                catch (Exception ex) { UBService.LogException(ex); }
            }
        }

        private void RenderSettingsDetails() {
            // all settings get special treatment by merging the container children together
            if (_selectedChildSettingIndex == 0) {
                RenderSettingsTree(SettingsContainers);
            }
            else {
                RenderSettingsTree(childSettings.ElementAt(_selectedChildSettingIndex - 1));
            }
        }

        private void RenderSettingCategories() {
            ImGui.PushID("SettingCategories");
            if (ImGui.BeginListBox("", ImGui.GetContentRegionAvail())) {
                var hasChanges = childSettings.Any(s => !s.IsDefault);
                if (ImGui.Selectable($"All Settings{(hasChanges ? "*" : "")}", _selectedChildSettingIndex == 0)) {
                    _selectedChildSettingIndex = 0;
                }
                if (_selectedChildSettingIndex == 0) {
                    ImGui.SetItemDefaultFocus();
                }
                ImGui.Indent(6);
                for (var i = 1; i < childSettings.Count + 1; i++) {
                    var setting = childSettings[i - 1];
                    if (ImGui.Selectable($"{setting.Name}{(setting.IsDefault ? "" : "*")}", i == _selectedChildSettingIndex)) {
                        _selectedChildSettingIndex = i;
                    }
                    if (i == _selectedChildSettingIndex) {
                        ImGui.SetItemDefaultFocus();
                    }
                }
                ImGui.Unindent();
                ImGui.EndListBox();
            }
            ImGui.PopID();
        }

        private void RenderSettingsTree(ISetting setting) {
            foreach (var child in setting.GetChildren()) {
                RenderSettingTree(child, setting, "", 0);
            }
        }

        private void RenderSettingsTree(IEnumerable<object> containers) {
            foreach (var container in containers) {
                foreach (var setting in GetContainerSettings(container)) {
                    RenderSettingTree(setting, container, "", 0);
                }
            }
        }

        private void RenderSettingTree(ISetting setting, object parent, string history, int depth = 0) {
            if (setting.FieldInfo.GetCustomAttributes(true).Any(a => a is DontShowInSettingsAttribute)) {
                return;
            }

            var hasChildren = setting.GetChildren().Count() > 0;
            var flags = ImGuiTreeNodeFlags.None;
            flags |= hasChildren ? ImGuiTreeNodeFlags.None : ImGuiTreeNodeFlags.Leaf;

            // only leafs with no children are selectable
            if (!hasChildren && setting == _selectedSetting)
                flags |= ImGuiTreeNodeFlags.Selected;

            if (hasChildren) {
                if (childSettings.Count == 1) {
                    flags |= ImGuiTreeNodeFlags.DefaultOpen;
                }
                var isExpanded = ImGui.TreeNodeEx(setting.FullName, flags, $"{setting.Name}{(setting.IsDefault ? "" : "*")}");

                if (isExpanded) {
                    foreach (var child in setting.GetChildren()) {
                        RenderSettingTree(child, setting, $"{(string.IsNullOrEmpty(history) ? $"{history}." : "")}{setting.Name}", depth + 1);
                    }
                    ImGui.TreePop();
                }
            }
            else {
                RenderEditRow(setting);
                if (ImGui.IsItemClicked()) {
                    _selectedSetting = setting;
                    //Logger.WriteToChat($"Selected: {setting.FullName}"); 
                }
            }

        }

        internal static readonly Type[] NumberTypes = new Type[] {
            typeof(byte),
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            typeof(float),
            typeof(double)
        };
        private ManagedTexture infoIcon;

        private unsafe void RenderEditRow(ISetting setting) {
            var type = setting.GetValue().GetType();
            var label = $"{setting.Name}{(setting.IsDefault ? "" : "*")}###edit{setting.FullName}";
            var clear = new Vector4(0,0,0,0);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, clear); // button bg
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, clear); // button bg
            ImGui.TextureButton($"info.{setting.FullName}", infoIcon, new Vector2(16, 16), 0, clear, *ImGui.GetStyleColorVec4(ImGuiCol.TextDisabled));
            ImGui.PopStyleColor(2); // button bg
            ImGui.SameLine();
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip(setting.Summary);
            }

            ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X * 0.45f);
            if (type == typeof(string)) {
                if (!_stringInputBuffers.ContainsKey(setting.FullName)) {
                    _stringInputBuffers.Add(setting.FullName, new byte[MAX_STRING_LENGTH]);
                }
                _stringInputBuffers[setting.FullName] = Encoding.UTF8.GetBytes(setting.GetValue().ToString());
                if (ImGui.InputText(label, _stringInputBuffers[setting.FullName], MAX_STRING_LENGTH, ImGuiInputTextFlags.None, null)) {
                    var newValue = Encoding.UTF8.GetString(_stringInputBuffers[setting.FullName]).ToString();

                    setting.SetValue(newValue);
                }
            }
            else if (type.IsEnum) {
                var enumValues = Enum.GetValues(type).Cast<object>().ToList();
                enumValues.Sort((a, b) => a.ToString().CompareTo(b.ToString()));
                var selected = enumValues.IndexOf(setting.GetValue());
                if (selected < 0) {
                    selected = 0;
                }
                if (ImGui.Combo(label, ref selected, enumValues.Select(e => e.ToString()).ToArray(), enumValues.Count)) {
                    setting.SetValue(enumValues.ElementAt(selected));
                }
            }
            else if (type.IsPrimitive) {
                if (NumberTypes.Contains(type) && setting.Name.Contains("Color")) {
                    Vector4 color = ColorToVector4(Color.FromArgb((int)setting.GetValue()));
                    var flags = ImGuiColorEditFlags.None | ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar;
                    if (ImGui.ColorEdit4(label, ref color, flags)) {
                        setting.SetValue(Vector4ToColor(color).ToArgb());
                    }
                }
                else {
                    InputPrimitive(label, setting, _stringInputBuffers);
                }
            }
            else if (setting.GetValue() is IList iList) {
                ImGui.PushID($"EditList.{setting.FullName}");
                if (ImGui.Button($"Edit list ({iList.Count} items)")) {
                    Vector2 center = ImGui.GetMainViewport().GetCenter();
                    ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
                    var showAt = new Vector2(
                            ImGui.GetWindowPos().X + (ImGui.GetWindowWidth() / 2),
                            ImGui.GetWindowPos().Y + (ImGui.GetWindowHeight() / 2)
                        );
                    var editor = new SettingsListEditor(setting.FullName, ParentObject, iList, showAt, (newValue) => {
                        //setting.SetValue(newValue);
                    });
                    listEditors.Add(editor);
                }
                ImGui.SameLine();
                ImGui.Text(setting.Name);
                ImGui.PopID();
            }
            // dictionaries, they *should* all be Hellosam.Net.Collections.ObservableDictionary<TKey, TValue>
            else if (setting.GetValue() is ICollection && setting.GetValue().GetType().GetGenericArguments().Length == 2) {
                ImGui.PushID($"EditDictionary.{setting.FullName}");
                if (ImGui.Button($"Edit dictionary")) {
                    //listEditors.Add(new ListEditor(setting));
                }
                ImGui.SameLine();
                ImGui.Text(setting.Name);
                ImGui.PopID();
            }
            else {
                ImGui.Text(setting.GetValue().GetType().ToString());
            }
            ImGui.PopItemWidth();

            if (!setting.IsDefault) {
                ImGui.SameLine(0, 5);
                ImGui.PushID($"revert.{setting.FullName}");
                if (ImGui.Button("revert")) {
                    setting.SetValue(setting.GetDefaultValue());
                }
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip($"Default: {setting.DisplayValue(true, true)}");
                }
                ImGui.PopID();
            }
        }

        internal static Color Vector4ToColor(Vector4 color) {
            return Color.FromArgb(
                (int)Math.Max(0, Math.Min(255, 255 * color.Z)),
                (int)Math.Max(0, Math.Min(255, 255 * color.W)),
                (int)Math.Max(0, Math.Min(255, 255 * color.X)),
                (int)Math.Max(0, Math.Min(255, 255 * color.Y))
            );
        }

        internal static Vector4 ColorToVector4(Color color) {
            return new Vector4(
                Math.Max(0, Math.Min(1, color.R / 255f)),
                Math.Max(0, Math.Min(1, color.G / 255f)),
                Math.Max(0, Math.Min(1, color.B / 255f)),
                Math.Max(0, Math.Min(1, color.A / 255f))
                );
        }

        internal static bool InputPrimitive(string label, ISetting setting, Dictionary<string, byte[]> _stringInputBuffers) {
            var ret = false;
            var type = setting.GetValue().GetType();

            if (setting.GetValue() is bool boolValue) {
                if (ImGui.Checkbox(label, ref boolValue)) {
                    setting.SetValue(boolValue);
                    ret = true;
                }
            }
            else if (NumberTypes.Contains(type)) {
                if (!_stringInputBuffers.ContainsKey(setting.FullName)) {
                    _stringInputBuffers.Add(setting.FullName, new byte[MAX_STRING_LENGTH]);
                }
                _stringInputBuffers[setting.FullName] = Encoding.UTF8.GetBytes(setting.GetValue().ToString());
                var initialValue = (byte[])_stringInputBuffers[setting.FullName].Clone();
                bool paramIsHex = false;
                var latestValue = setting.GetValue().ToString();

                var flags = ImGuiInputTextFlags.None;
                if (type == typeof(float) || type == typeof(double)) {
                    flags |= ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.CharsScientific;
                }
                else if (paramIsHex) {
                    flags |= ImGuiInputTextFlags.CharsHexadecimal;
                }
                else {
                    flags |= ImGuiInputTextFlags.CharsDecimal;
                }

                if (ImGui.InputText(label, _stringInputBuffers[setting.FullName], MAX_STRING_LENGTH, ImGuiInputTextFlags.None, null)) {
                    var newValue = Encoding.UTF8.GetString(_stringInputBuffers[setting.FullName]).ToString();

                    if (TryParseNumberType(type, newValue, out object result, paramIsHex)) {
                        setting.SetValue(result);
                    }
                    else {
                        initialValue.CopyTo(_stringInputBuffers[setting.FullName], 0);
                    }

                    ret = true;
                }
            }
            else {
                ImGui.Text(setting.FullName);
            }

            return ret;
        }

        internal static  bool TryParseNumberType(Type type, string newValue, out object result, bool paramIsHex) {
            var numberStyle = paramIsHex ? NumberStyles.HexNumber : NumberStyles.Integer;

            if (paramIsHex)
                newValue = Regex.Replace(newValue, @"^0x", "");

            if (type == typeof(byte) && byte.TryParse(newValue, numberStyle, null, out byte byteResult)) {
                result = byteResult;
                return true;
            }
            else if (type == typeof(short) && short.TryParse(newValue, numberStyle, null, out short shortResult)) {
                result = shortResult;
                return true;
            }
            else if (type == typeof(ushort) && ushort.TryParse(newValue, numberStyle, null, out ushort ushortResult)) {
                result = ushortResult;
                return true;
            }
            else if (type == typeof(int) && int.TryParse(newValue, numberStyle, null, out int intResult)) {
                result = intResult;
                return true;
            }
            else if (type == typeof(uint) && uint.TryParse(newValue, numberStyle, null, out uint uintResult)) {
                result = uintResult;
                return true;
            }
            else if (type == typeof(long) && long.TryParse(newValue, numberStyle, null, out long longtResult)) {
                result = longtResult;
                return true;
            }
            else if (type == typeof(ulong) && ulong.TryParse(newValue, numberStyle, null, out ulong ulongResult)) {
                result = ulongResult;
                return true;
            }
            else if (type == typeof(float) && float.TryParse(newValue, NumberStyles.AllowDecimalPoint, null, out float floatResult)) {
                result = floatResult;
                return true;
            }
            else if (type == typeof(double) && double.TryParse(newValue, NumberStyles.AllowDecimalPoint, null, out double doubleResult)) {
                result = doubleResult;
                return true;
            }

            result = 0;

            return false;
        }

        private IEnumerable<ISetting> GetContainerSettings(object container) {
            IEnumerable<ISetting> containerSettings = null;
            if (container is Type containerType) {
                containerSettings = containerType.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                    .Where(f => typeof(ISetting).IsAssignableFrom(f.FieldType))
                    .Select(f => (ISetting)f.GetValue(null))
                    .Where(f => f != null && f.GetChildren().Count() > 0)
                    .OrderBy(s => s.Name);
            }
            else {
                containerSettings = container.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                    .Where(f => typeof(ISetting).IsAssignableFrom(f.FieldType))
                    .Select(f => (ISetting)f.GetValue(container))
                    .Where(f => f != null && f.GetChildren().Count() > 0)
                    .OrderBy(s => s.Name);
            }
            return containerSettings;
        }

        public void Dispose() {
            if (isDisposed)
                return;

            foreach (var listEditor in listEditors) {
                listEditor?.Dispose();
            }
            listEditors.Clear();

            infoIcon?.Dispose();
            infoIcon = null;

            Hud.Render -= Hud_Render;
            Hud.PreRender -= Hud_PreRender;
            Hud?.Dispose();

            isDisposed = true;
        }
    }
}
