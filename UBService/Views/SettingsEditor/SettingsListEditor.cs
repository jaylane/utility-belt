using ImGuiNET;
using Microsoft.DirectX.Direct3D;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Text;
using UBService.Lib.Settings;
using UBService.Views;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Text.RegularExpressions;
using System.Globalization;
using SerializationBinder = UBService.Lib.Settings.SerializationBinder;

namespace UBService.Views.SettingsEditor {
    public class SettingsListEditor : IDisposable {
        private Hud hud;
        private bool isDisposed = false;
        private ManagedTexture cancelIcon;
        private static bool dontAskToDelete = false;
        private int indexToRemove = -1;
        private object objectToRemove = null;
        private static uint _nextId = 0;
        private uint id;
        private Vector2 showAt;
        private Action<object> updateAction;

        private bool isDeleteOpen = false;
        private object currentlyEditing = null;
        private object currentlyEditingClone = null;
        private List<SettingsListEditor> listEditors = new List<SettingsListEditor>();

        public string Name { get; }
        public Type ValueType { get; }
        public object Value { get; private set; }

        public SettingsListEditor(string name, IList editList, Vector2 showAt, Action<object> updateAction) {
            Value = editList;
            this.showAt = showAt;
            this.updateAction = updateAction;
            Name = name;
            ValueType = Value.GetType().GetGenericArguments().FirstOrDefault();

            id = ++_nextId;
            hud = UBService.Huds.CreateHud($"ListEditor_{id}_{name}");
            hud.Title = $"Edit List: {name}";
            hud.ShowInBar = false;
            hud.PreRender += Hud_PreRender;
            hud.Render += Hud_Render;
            hud.ShouldHide += Hud_ShouldHide;

            currentlyEditingClone = MakeDefaultClone();

            using (Stream manifestResourceStream = GetType().Assembly.GetManifestResourceStream("UBService.Resources.icons.delete.png")) {
                cancelIcon = new ManagedTexture(manifestResourceStream);
            }
        }

        private object MakeDefaultClone() {
            if (ValueType == typeof(bool)) {
                return false;
            }
            else if (ValueType == typeof(string)) {
                return "";
            }
            else if (ValueType.IsEnum) {
                // todo: use first defined enum?
                return 0;
            }
            else if (ValueType.IsPrimitive) {
                return Convert.ChangeType(0, ValueType);
            }
            else {
                return Activator.CreateInstance(ValueType);
            }
        }

        private void Hud_PreRender(object sender, EventArgs e) {
            ImGui.SetNextWindowPos(showAt, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            ImGui.SetNextWindowSizeConstraints(new Vector2(250, 300), new Vector2(float.MaxValue, float.MaxValue));
        }

        private void Hud_ShouldHide(object sender, EventArgs e) {
            Dispose();
        }

        private void Hud_Render(object sender, EventArgs e) {
            try {
                ImGui.BeginChild(1, new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y / 2));
                RenderEntries();
                ImGui.EndChildFrame();
                ImGui.BeginChild(2, new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y));
                RenderEditor();
                ImGui.EndChildFrame();
            }
            catch (Exception ex) { UBService.LogException(ex); }
        }

        private void RenderEditor() {
            var type = ValueType;
            var obj = currentlyEditingClone;
            if (obj == null) {
                ImGui.Text($"null edit object 1");
                return;
            }


            if (type == null) {
                ImGui.Text($"Type has no generic arguments...");
                return;
            }

            if (type.IsPrimitive || type.IsEnum || type == typeof(string)) {
                if (InputObject(type.Name, ref obj)) {
                    currentlyEditingClone = obj;
                }
            }
            else if (type.IsClass) {
                var members = type.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f as MemberInfo).ToList();
                members.AddRange(type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(f => f as MemberInfo));

                members = members.Where(m => !m.GetCustomAttributes(true).Any(a => a is DontShowInSettingsAttribute)).ToList();

                members.Sort((a, b) => a.Name.CompareTo(b.Name));

                foreach (var member in members) {
                    try {
                        if (member is FieldInfo fieldInfo) {
                            object value = fieldInfo.GetValue(currentlyEditingClone);
                            if (InputObject(member.Name, ref value)) {
                                fieldInfo.SetValue(currentlyEditingClone, value);
                            }
                        }
                        else if (member is PropertyInfo propInfo) {
                            object value = propInfo.GetValue(currentlyEditingClone, null);
                            if (InputObject(member.Name, ref value)) {
                                propInfo.SetValue(currentlyEditingClone, value, null);
                            }
                        }
                    }
                    catch (Exception ex) { UBService.LogException(ex); }
                }
            }
            else {
                ImGui.Text($"type is {type}");
            }

            if (currentlyEditing == null) {
                if (ImGui.Button("Add")) {
                    (Value as IList).Add(currentlyEditingClone);
                    currentlyEditingClone = MakeDefaultClone();
                    currentlyEditing = null;
                }
            }
            else {
                ImGui.Spacing();
                ImGui.Spacing();
                if (ImGui.Button("Cancel")) {
                    currentlyEditingClone = MakeDefaultClone();
                    currentlyEditing = null;
                }
                ImGui.SameLine();
                if (ImGui.Button("Save")) {
                    var index = (Value as IList).IndexOf(currentlyEditing);
                    (Value as IList).Remove(currentlyEditing);
                    (Value as IList).Insert(index, currentlyEditingClone);
                    currentlyEditingClone = MakeDefaultClone();
                    currentlyEditing = null;
                }
            }
        }

        private bool InputObject(string label, ref object obj) {
            if (obj == null) {
                ImGui.Text($"null edit object: {label}");
                return false;
            }

            var type = obj.GetType();
            var ret = false;

            ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X * 0.45f);
            if (type == typeof(string)) {
                var buff = new byte[SettingsEditor.MAX_STRING_LENGTH];
                Encoding.UTF8.GetBytes(obj.ToString()+"\0").CopyTo(buff, 0);
                if (ImGui.InputText(label, buff, SettingsEditor.MAX_STRING_LENGTH + 1, ImGuiInputTextFlags.None, null)) {
                    obj = Encoding.UTF8.GetString(buff).ToString().Split('\0').FirstOrDefault();
                    ret = true;
                }
            }
            else if (type.IsEnum) {
                var enumValues = Enum.GetValues(type).Cast<object>().ToList();
                enumValues.Sort((a, b) => a.ToString().CompareTo(b.ToString()));
                var selected = enumValues.IndexOf(obj);
                if (selected < 0) {
                    selected = 0;
                }
                if (ImGui.Combo(label, ref selected, enumValues.Select(e => e.ToString()).ToArray(), enumValues.Count)) {
                    obj = Convert.ChangeType(enumValues.ElementAt(selected), type);
                    ret = true;
                }
            }
            else if (type.IsPrimitive) {
                if (type == typeof(int) && Name.Contains("Color")) {
                    Vector4 color = SettingsEditor.ColorToVector4(Color.FromArgb((int)obj));
                    var flags = ImGuiColorEditFlags.None | ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar;
                    if (ImGui.ColorEdit4(label, ref color, flags)) {
                        obj = SettingsEditor.Vector4ToColor(color).ToArgb();
                        ret = true;
                    }
                }
                else {
                    var editObj = obj;
                    if (InputPrimitive(label, ref editObj)) {
                        obj = editObj;
                    }
                }
            }
            else if (obj is IList iList) {
                ImGui.PushID($"EditList.{label}");
                if (ImGui.Button($"Edit list ({iList.Count} items)")) {
                    Vector2 center = ImGui.GetMainViewport().GetCenter();
                    ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
                    var showAt = new Vector2(
                            ImGui.GetWindowPos().X + (ImGui.GetWindowWidth() / 2),
                            ImGui.GetWindowPos().Y + (ImGui.GetWindowHeight() / 2)
                        );
                    var editor = new SettingsListEditor($"{Name} // {label}", iList, showAt, (newValue) => {
                        //iList.Clear();
                    });
                    listEditors.Add(editor);
                }
                ImGui.SameLine();
                ImGui.Text(label);
                ImGui.PopID();
            }
            // dictionaries, they *should* all be Hellosam.Net.Collections.ObservableDictionary<TKey, TValue>
            else if (Value is ICollection && Value.GetType().GetGenericArguments().Length == 2) {
                ImGui.Text("todo: IDict");
                /*
                ImGui.PushID($"EditDictionary.{setting.FullName}");
                if (ImGui.Button($"Edit dictionary")) {
                    //listEditors.Add(new ListEditor(setting));
                }
                ImGui.SameLine();
                ImGui.Text(setting.Name); 
                ImGui.PopID();
                */
            }
            else {
                ImGui.Text(Value.GetType().ToString());
            }
            ImGui.PopItemWidth();

            return ret;
        }

        internal static bool InputPrimitive(string label, ref object obj) {
            var ret = false;
            var type = obj.GetType();

            if (obj is bool boolValue) {
                if (ImGui.Checkbox(label, ref boolValue)) {
                    obj = boolValue;
                    ret = true;
                }
            }
            else if (SettingsEditor.NumberTypes.Contains(type)) {
                var buff = new byte[SettingsEditor.MAX_STRING_LENGTH];
                Encoding.UTF8.GetBytes(obj.ToString() + "\0").CopyTo(buff, 0);
                if (ImGui.InputText(label, buff, SettingsEditor.MAX_STRING_LENGTH + 1, ImGuiInputTextFlags.None, null)) {
                    obj = Encoding.UTF8.GetString(buff).ToString().Split('\0').FirstOrDefault();
                    ret = true;
                }

                bool paramIsHex = false;

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

                if (ImGui.InputText(label, buff, SettingsEditor.MAX_STRING_LENGTH + 1, flags, null)) {
                    var newValue = Encoding.UTF8.GetString(buff).ToString().Split('\0').FirstOrDefault();

                    if (SettingsEditor.TryParseNumberType(type, newValue, out object result, paramIsHex)) {
                        obj = result;
                        ret = true;
                    }
                }
            }
            else {
                ImGui.Text(label);
            }

            return ret;
        }

        private unsafe void RenderEntries() {
            var list = Value as IList;
            var i = 0;
            var confirmedDelete = dontAskToDelete;
            bool showingPopup = false;

            int swapA = -1;
            int swapB = -1;
            List<object> _listCache = new List<object>();
            foreach (var item in list) {
                ImGui.PushID(item.GetHashCode());
                _listCache.Add(item);
                var deleteTitle = $"Delete {Name} Entry #{i}?";
                if (ImGui.TextureButton($"DeleteEntry.{i}", cancelIcon, new Vector2(14, 14), 0)) {
                    indexToRemove = i;
                    objectToRemove = item;
                    if (!dontAskToDelete) {
                        isDeleteOpen = true;
                        ImGui.OpenPopup(deleteTitle);
                    }
                }

                // Always center this window when appearing
                Vector2 center = ImGui.GetMainViewport().GetCenter();
                ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

                if (ImGui.BeginPopupModal(deleteTitle, ref isDeleteOpen, ImGuiWindowFlags.AlwaysAutoResize)) {
                    showingPopup = true;
                    ImGui.Text($"Are you sure you want to delete Entry #{indexToRemove} from {Name}?\n\nValue:\n\n{objectToRemove}\n\n");
                    ImGui.Separator();

                    ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 0));
                    ImGui.Checkbox("Don't ask me next time", ref dontAskToDelete);
                    ImGui.PopStyleVar();

                    if (ImGui.Button("OK", new Vector2(120, 0))) {
                        confirmedDelete = true;
                        showingPopup = false;
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.SetItemDefaultFocus();
                    ImGui.SameLine();
                    if (ImGui.Button("Cancel", new Vector2(120, 0))) {
                        indexToRemove = -1;
                        showingPopup = false;
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                }

                ImGui.SameLine();
                int _ptr = i + 1;
                bool selected = currentlyEditing != null && currentlyEditing.Equals(item);
                ImGui.Selectable(item.ToString(), selected);
                if (ImGui.IsItemActive() && !ImGui.IsItemHovered()) {
                    int n_next = i + (ImGui.GetMouseDragDelta(0).Y < 0f ? -1 : 1);
                    if (n_next >= 0 && n_next < list.Count) {
                        swapA = i;
                        swapB = n_next;
                        ImGui.ResetMouseDragDelta();
                    }
                }
                else if (ImGui.IsItemActive()) {
                    currentlyEditing = item;
                    currentlyEditingClone = CreateDeepCopy(item);
                }
                i++;
                ImGui.PopID();
            }

            if (swapA >= 0 && swapB >= 0 && swapA != swapB) {
                list[swapA] = _listCache[swapB];
                list[swapB] = _listCache[swapA];
                updateAction?.Invoke(Value);
            }

            if (!showingPopup && confirmedDelete && indexToRemove >= 0) {
                DeleteListIndex(indexToRemove);
            }
        }

        private void DeleteListIndex(int indexToRemove) {
            (Value as IList).RemoveAt(indexToRemove);
            this.indexToRemove = -1;
            objectToRemove = null;
            updateAction?.Invoke(Value);
        }
        internal static T CreateDeepCopy<T>(T obj) {
            if (obj.GetType().IsPrimitive || obj.GetType().IsEnum)
                return obj;

            var settings = new JsonSerializerSettings() {
                TypeNameHandling = TypeNameHandling.All,
                TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full,
                Formatting = Formatting.Indented
            };
            var json = JsonConvert.SerializeObject(obj, settings); 
            var copy = JsonConvert.DeserializeObject<T>(json, settings);
            return copy;
        }

        private void ClearListEditors() {
            foreach (var listEditor in listEditors) {
                listEditor?.Dispose();
            }
            listEditors.Clear();
        }

        public void Dispose() {
            if (isDisposed)
                return;

            ClearListEditors();
            cancelIcon?.Dispose();

            if (hud != null) {
                hud.PreRender -= Hud_PreRender;
                hud.Render -= Hud_Render;
                hud.ShouldHide -= Hud_ShouldHide;
                hud.Dispose();
            }

            isDisposed = true;
        }
    }
}