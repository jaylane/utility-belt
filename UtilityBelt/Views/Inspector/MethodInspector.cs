using ImGuiNET;
using LiteDB;
using Microsoft.DirectX.Direct3D;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using UBService;
using static UtilityBelt.Lib.UBHud;

namespace UtilityBelt.Views.Inspector {
    public class MethodInspector : IDisposable {
        private const int MAX_STRING_LENGTH = 1000;
        private static uint _id = 0;
        private static uint _pushId = 0;
        private UBService.Views.Hud hud;
        private Vector2 minWindowSize = new Vector2(300, 200);
        private Vector2 maxWindowSize = new Vector2(99999, 99999);
        private List<Inspector> inspectors = new List<Inspector>();
        private List<object> results = new List<object>();
        private Dictionary<string, int> _paramSelectedComboIndex = new Dictionary<string, int>();
        private Dictionary<string, bool> _paramIsHex = new Dictionary<string, bool>();
        private Dictionary<string, byte[]> _stringBuffers = new Dictionary<string, byte[]>();
        private Dictionary<string, object> arguments = new Dictionary<string, object>();

        public string Name { get; private set; }
        public MethodInfo MethodInfo { get; }
        public object Parent { get; }

        public MethodInspector(string name, MethodInfo methodInfo, object parent) {
            Name = name;
            MethodInfo = methodInfo;
            Parent = parent;

            using (Stream manifestResourceStream = GetType().Assembly.GetManifestResourceStream("UtilityBelt.Resources.icons.eye.png")) {
                hud = UBService.UBService.Huds.CreateHud($"MethodInspector: {Name}##EventMonitor{_id++}", new Bitmap(manifestResourceStream));
            }

            var _params = MethodInfo.GetParameters();
            for (var i = 0; i < _params.Length; i++) {
                arguments.Add(_params[i].Name, null);
                if (_params[i].ParameterType == typeof(string)) {
                    _stringBuffers.Add(_params[i].Name, new byte[MAX_STRING_LENGTH + 1]);
                    arguments[_params[i].Name] = "";
                }
                else if (_params[i].ParameterType.IsEnum) {
                    _paramSelectedComboIndex.Add(_params[i].Name, 0);
                }
                else if (_params[i].ParameterType.IsPrimitive) {
                    _paramIsHex.Add(_params[i].Name, false);
                    _stringBuffers.Add(_params[i].Name, new byte[MAX_STRING_LENGTH + 1]);
                }
            }

            hud.Render += Hud_Render;
            hud.PreRender += Hud_PreRender;
            hud.ShouldHide += Hud_ShouldHide;
            hud.CreateTextures += Hud_CreateTextures;
            hud.DestroyTextures += Hud_DestroyTextures;
            CreateTextures();
        }

        private void Hud_PreRender(object sender, EventArgs e) {
            ImGui.SetNextWindowSizeConstraints(minWindowSize, maxWindowSize);
        }

        private unsafe void Hud_Render(object sender, EventArgs e) {
            try {
                _pushId = 0;
                ImGui.Text($"Method: {Inspector.GetMethodDisplayString(MethodInfo)}");
                ImGui.Spacing();
                ImGui.Spacing();

                ImGui.Text($"Parameters: ");
                ImGui.Spacing();
                var _params = MethodInfo.GetParameters();
                for (var i = 0; i < _params.Length; i++) {
                    ImGui.PushID(i);
                    RenderParamEdit(_params[i]);
                    ImGui.PopID();
                }
                ImGui.Separator();

                if (ImGui.Button("Call Method", new Vector2(ImGui.GetContentRegionAvail().X, 20))) {
                    var methodArgs = GetMethodArguments();
                    Logger.WriteToChat($"Calling {MethodInfo.Name} with: ");
                    for (var i = 0; i < _params.Length; i++) {
                        Logger.WriteToChat($" - {_params[i].Name} = {methodArgs[i]}");
                    }
                    var result = MethodInfo.Invoke(Parent, methodArgs);
                    Logger.WriteToChat($"Got result: {((result == null) ? "null" : result.ToString())}");
                    if (result != null && result.GetType() != typeof(string) && result is IEnumerable iEnum) {
                        foreach (var ev in iEnum) {
                            Logger.WriteToChat($"  - {ev}");
                        }
                    }
                    results.Add(result);
                }
                ImGui.Separator();

                ImGui.BeginChild("results", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y));
                var j = 0;
                foreach (var ev in results) {
                    var tint = new Vector4(1, 1, 1, 1);
                    var size = new Vector2(12, 12);
                    if (ev != null) {
                        ImGui.PushID(j);
                        if (ImGui.ImageButton((IntPtr)InspectorIcon.UnmanagedComPointer, size, new Vector2(0, 0), new Vector2(1, 1), 1, new Vector4(), tint)) {
                            inspectors.Add(new Inspector($"{Name} Results #{j}", ev) {
                                DisposeOnClose = true
                            });
                        }
                        ImGui.PopID();
                        ImGui.SameLine();
                    }
                    ImGui.Text($"Result#{j}: {Inspector.DetailsDisplayString(ev)}");
                    if (ev != null && ImGui.IsItemHovered()) {
                        ImGui.SetTooltip(Inspector.TypeDisplayString(ev.GetType(), false));
                    }
                    if ((true && ImGui.GetScrollY() >= ImGui.GetScrollMaxY()))
                        ImGui.SetScrollHereY(1.0f);
                    j++;
                }
                ImGui.EndChild();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private object[] GetMethodArguments() {
            var args = new object[arguments.Count];
            var _params = MethodInfo.GetParameters();
            for (var i = 0; i < _params.Length; i++) {
                args[i] = arguments[_params[i].Name];
            }
            return args;
        }

        private void RenderParamEdit(ParameterInfo param) {
            var label = $"{param.Name} ({Inspector.TypeDisplayString(param.ParameterType)})";
            if (param.ParameterType.IsEnum) {
                var enumValues = Enum.GetValues(param.ParameterType).Cast<object>().ToList();
                enumValues.Sort((a, b) => a.ToString().CompareTo(b.ToString()));
                var selected = _paramSelectedComboIndex[param.Name];
                arguments[param.Name] = enumValues.Count == 0 ? null : enumValues[selected];
                if (ImGui.Combo(label, ref selected, enumValues.Select(e => e.ToString()).ToArray(), enumValues.Count)) {
                    _paramSelectedComboIndex[param.Name] = selected;
                    arguments[param.Name] = enumValues.ElementAt(selected);
                }
            }
            else if (param.ParameterType == typeof(string)) {
                if (ImGui.InputText(label, _stringBuffers[param.Name], MAX_STRING_LENGTH, ImGuiInputTextFlags.None, null)) {
                    arguments[param.Name] = Encoding.UTF8.GetString(_stringBuffers[param.Name]).ToString();
                }
            }
            else if (param.ParameterType.IsPrimitive) {
                object value = arguments[param.Name] = (arguments[param.Name] == null) ? (param.ParameterType == typeof(bool) ? false : 0) : arguments[param.Name];
                if (InputPrimitive(label, param, ref value)) {
                    arguments[param.Name] = value;
                }
            }
            else if (param.ParameterType.IsClass) {
                var hasValue = arguments[param.Name] != null;
                var color = hasValue ? new Vector4(0, 1, 0, 1) : new Vector4(1, 0, 0, 1);
                var text = hasValue ? $"{Inspector.DetailsDisplayString(arguments[param.Name])}" : "drag object here to set";
                var buf = Encoding.UTF8.GetBytes($"{text}\0");
                ImGui.InputText(label, buf, (uint)buf.Length - 1, ImGuiInputTextFlags.ReadOnly);
                if (ImGui.BeginDragDropTarget()) {
                    try {
                        var payload = ImGui.AcceptDragDropPayload("OBJECT_INSTANCE");
                        if (payload.Data != IntPtr.Zero) {
                            arguments[param.Name] = Inspector.DRAG_SOURCE_OBJECT;
                        }
                    }
                    catch { }
                    ImGui.EndDragDropTarget();
                }
                ImGui.SameLine(0, 10);
                ImGui.Text(label);
            }
            else {
                ImGui.Text($"{Inspector.TypeDisplayString(param.ParameterType)}");
            }
        }

        private List<Type> _numberTypes = new List<Type>() {
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
        private bool InputPrimitive(string label, ParameterInfo param, ref object value) {
            var ret = false;
            if (value is bool boolValue) {
                if (ImGui.Checkbox(label, ref boolValue)) {
                    value = boolValue;
                    ret = true;
                }
            }
            else if (_numberTypes.Contains(param.ParameterType)) {
                bool paramIsHex = _paramIsHex[param.Name];
                var initialValue = (byte[])_stringBuffers[param.Name].Clone();
                var latestValue = arguments[param.Name];
                if (ImGui.InputText(label, _stringBuffers[param.Name], MAX_STRING_LENGTH, ImGuiInputTextFlags.None, null)) {
                    var newValue = Encoding.UTF8.GetString(_stringBuffers[param.Name]).ToString();

                    if (TryParseNumberType(param.ParameterType, newValue, out object result, paramIsHex)) {
                        value = result;
                        latestValue = result;
                    }
                    else {
                        initialValue.CopyTo(_stringBuffers[param.Name], 0);
                    }

                    ret = true;
                }
                if (param.ParameterType != typeof(float) && param.ParameterType != typeof(double)) {
                    ImGui.SameLine();
                    if (ImGui.Checkbox("Hex", ref paramIsHex)) {
                        if (paramIsHex) {
                            Encoding.UTF8.GetBytes($"0x{latestValue:X}\0").CopyTo(_stringBuffers[param.Name], 0);
                        }
                        else {
                            Encoding.UTF8.GetBytes($"{latestValue}\0").CopyTo(_stringBuffers[param.Name], 0);
                        }
                        _paramIsHex[param.Name] = paramIsHex;
                    }
                }
            }
            else {
                ImGui.Text(param.ParameterType.FullName);
            }

            return ret;
        }

        private bool TryParseNumberType(Type type, string newValue, out object result, bool paramIsHex) {
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

        private void Hud_ShouldHide(object sender, EventArgs e) {
            Dispose();
        }

        private void Hud_CreateTextures(object sender, EventArgs e) {
            CreateTextures();
        }

        private void Hud_DestroyTextures(object sender, EventArgs e) {
            DestroyTextures();
        }

        private void CreateTextures() {
            try {
                CreateTextureFromResource(ref InspectorIcon, "UtilityBelt.Resources.icons.inspector.png");
            }
            catch (Exception ex) { UBLoader.FilterCore.LogException(ex); }
        }

        private void CreateTextureFromResource(ref Microsoft.DirectX.Direct3D.Texture texture, string resourcePath) {
            if (texture == null)
                texture = LoadTextureFromResouce(resourcePath);
        }

        private Microsoft.DirectX.Direct3D.Texture LoadTextureFromResouce(string resourcePath) {
            using (Stream manifestResourceStream = GetType().Assembly.GetManifestResourceStream(resourcePath)) {
                using (Bitmap bmp = new Bitmap(manifestResourceStream)) {
                    return new Microsoft.DirectX.Direct3D.Texture(UtilityBeltPlugin.Instance.D3Ddevice, bmp, Usage.Dynamic, Pool.Default);
                }
            }
        }

        private void DestroyTextures() {
            try {
                DestroyTexture(ref InspectorIcon);
            }
            catch (Exception ex) { UBLoader.FilterCore.LogException(ex); }
        }

        private void DestroyTexture(ref Microsoft.DirectX.Direct3D.Texture texture) {
            texture?.Dispose();
            texture = null;
        }

        private bool isDisposed = false;
        private Texture InspectorIcon;

        public void Dispose() {
            if (!isDisposed) {
                UBLoader.FilterCore.LogError($"Dispose: MethodInspector {Name}");
                foreach (var inspector in inspectors) {
                    inspector.Dispose();
                }
                inspectors.Clear();
                results.Clear();
                DestroyTextures();
                hud.Visible = false;
                hud.Render -= Hud_Render;
                hud.PreRender -= Hud_PreRender;
                hud.ShouldHide -= Hud_ShouldHide;
                hud.CreateTextures -= Hud_CreateTextures;
                hud.DestroyTextures -= Hud_DestroyTextures;
                hud.Dispose();
                isDisposed = true;
            }
        }
    }
}
