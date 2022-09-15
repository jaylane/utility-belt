using Decal.Adapter.Wrappers;
using ImGuiNET;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UBService.Lib;
using UBService.Lib.Settings;

namespace UBService.Views {
    public class HudBar : ISetting, IDisposable {
        private bool _barIsOpen = true;
        private bool _needsDeltaReset = false;

        private Vector2 _lastBarDragDelta = new Vector2(0, 0);
        private ManagedTexture settingsIcon;

        private ThemeEditor themeEditor = null;
        private SettingsEditor.SettingsEditor settingsEditor = null;

        #region Config
        [Summary("Horizontal bar")]
        public ViewsProfileSetting<bool> HorizontalBar = new ViewsProfileSetting<bool>(true);

        [Summary("Lock bar position")]
        public ViewsProfileSetting<bool> PositionLocked = new ViewsProfileSetting<bool>(false);
        #endregion // Config

        public void Init() {
            using (Stream manifestResourceStream = typeof(UBService).Assembly.GetManifestResourceStream("UBService.Resources.icons.settings.png")) {
                settingsIcon = new ManagedTexture(new Bitmap(manifestResourceStream));
            }
        }

        internal unsafe void Render() {
            var scale = ImGui.GetIO().FontGlobalScale;

            try {
                var _huds = UBService.Huds.huds.Where(h => h.ShowInBar).ToList();
                //if (!_huds.Any(h => h != null && h.ShowInBar)) {
                //    return;
                //}

                int _id = 0;
                ImGuiWindowFlags windowSettings = ImGuiWindowFlags.NoDecoration;
                windowSettings |= ImGuiWindowFlags.NoDocking;
                windowSettings |= ImGuiWindowFlags.NoTitleBar;
                windowSettings |= ImGuiWindowFlags.NoScrollbar;
                windowSettings |= ImGuiWindowFlags.NoResize;
                windowSettings |= ImGuiWindowFlags.NoNav;
                windowSettings |= ImGuiWindowFlags.NoFocusOnAppearing;

                var hasViewports = (ImGui.GetIO().ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0;
                var initialX = hasViewports ? ImGui.GetWindowViewport().Pos.X + 150 : 150;
                var initialY = hasViewports ? ImGui.GetWindowViewport().Pos.Y + 4 : 4;
                var initialPos = new Vector2(initialX, initialY);

                var minSize = new Vector2(11, 11);
                var pivot = new Vector2(0, 0);
                var framePadding = new Vector2(2, 2);
                var originalWindowPadding = new Vector2(ImGui.GetStyle().WindowPadding.X, ImGui.GetStyle().WindowPadding.Y);
                ImGui.SetNextWindowBgAlpha(0.5f);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, framePadding);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, minSize);
                ImGui.SetNextWindowPos(initialPos, ImGuiCond.FirstUseEver, pivot);

                var bSize = 16;
                var buttonSize = new Vector2(bSize * scale, bSize * scale);
                var shortSize = (bSize + 4) * scale + (2 * scale);
                var longSize = ((_huds.Count + 1) * (bSize + 2) * scale) + ((_huds.Count) * 2 * scale) + 4;
                if (HorizontalBar)
                    ImGui.SetNextWindowSize(new Vector2(longSize, shortSize));
                else
                    ImGui.SetNextWindowSize(new Vector2(shortSize, longSize));

                ImGui.Begin("UBService HudBar", ref _barIsOpen, windowSettings);
                ImGui.PushStyleColor(ImGuiCol.Button, 0); // button bg
                if (ImGui.TextureButton("SettingsIcon", settingsIcon, buttonSize, 1)) {
                    if (settingsEditor == null) {
                        settingsEditor = new SettingsEditor.SettingsEditor("UBService Settings", new object[] { typeof(UBService) });
                        settingsEditor.Hud.ShouldHide += (s, e) => {
                            settingsEditor.Dispose();
                            settingsEditor = null;
                        };
                    }
                }
                ImGui.PopStyleColor(); // button bg

                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, originalWindowPadding); // menu padding
                if (ImGui.BeginPopupContextItem()) {
                    if (ImGui.BeginMenu("Themes")) {
                        var themesList = Directory.GetFiles(UBService.Huds.ThemeStorageDirectory, "*.json").Select(p => p.Split('\\').Last()).ToList();
                        themesList.Sort();
                        foreach (var theme in themesList) {
                            if (ImGui.MenuItem(theme.Replace(".json", ""), "", UBService.Huds.CurrentThemeName.Value.Equals(theme.Replace(".json", "")))) {
                                var themePath = Path.Combine(Path.Combine(Path.GetDirectoryName(Assembly.GetAssembly(typeof(UBService)).Location), "themes"), theme);
                                if (File.Exists(themePath)) {
                                    try {
                                        UBService.Huds.CurrentThemeName.Value = theme.Replace(".json", "");
                                    }
                                    catch (Exception ex) { UBService.LogException(ex); }
                                }
                            }
                        }
                        ImGui.Separator();
                        if (ImGui.MenuItem("Theme Editor")) {
                            if (themeEditor == null) {
                                var themesDir = Path.Combine(Path.GetDirectoryName(Assembly.GetAssembly(typeof(UBService)).Location), "themes");
                                themeEditor = new ThemeEditor(themesDir);
                                themeEditor.Hud.ShouldHide += (s, e) => {
                                    themeEditor?.Dispose();
                                    themeEditor = null;
                                };
                            }
                        }
                        ImGui.EndMenu();
                    }
                    bool barLocked = PositionLocked;
                    if (ImGui.MenuItem("Lock bar", "", ref barLocked)) {
                        PositionLocked.Value = barLocked;
                        //Logger.WriteToChat("Clicked Locked");
                    }
                    bool isHorizontal = HorizontalBar;
                    if (ImGui.MenuItem("Horizontal bar", "", ref isHorizontal)) {
                        HorizontalBar.Value = isHorizontal;
                    }
                    ImGui.EndPopup();
                }
                else if (!PositionLocked && ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left)) {
                    var x = ImGui.GetMouseDragDelta();
                    var currentWindowPos = ImGui.GetWindowPos();
                    ImGui.SetWindowPos(new Vector2(currentWindowPos.X + x.X - _lastBarDragDelta.X, currentWindowPos.Y + x.Y - _lastBarDragDelta.Y));
                    _lastBarDragDelta.X = x.X;
                    _lastBarDragDelta.Y = x.Y;
                    _needsDeltaReset = true;
                }
                else if (_needsDeltaReset) {
                    _lastBarDragDelta.X = 0;
                    _lastBarDragDelta.Y = 0;
                    _needsDeltaReset = false;
                }
                ImGui.PopStyleVar();  // menu padding

                if (HorizontalBar) ImGui.SameLine(0, 2 * scale);
                else ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (-2 * scale));

                foreach (var hud in _huds) {
                    if (hud == null || !hud.ShowInBar)
                        continue;

                    ImGui.PushID($"hudIcon_{_id++}"); // need to give each image button a unique id (why?) and pop it later

                    if (hud.Visible) {
                        //bgColor.w = 0.2f;
                    }

                    ImGui.PushStyleColor(ImGuiCol.Button, 0);
                    if (hud.iconTexture == null) {
                        var letter = hud.Name.FirstOrDefault().ToString();
                        if (string.IsNullOrEmpty(letter)) letter = "?";
                        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 0));
                        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, hud.Visible ? 1f : 0.5f);
                        if (ImGui.Button(letter, new Vector2(buttonSize.X + 2 * scale, buttonSize.Y + 2 * scale))) {
                            hud.Visible = !hud.Visible;
                        }
                        ImGui.PopStyleVar(2);
                    }
                    else {
                        var uv0 = new Vector2(0, 0);
                        var uv1 = new Vector2(1, 1);
                        var tint = hud.Visible ? new Vector4(1, 1, 1, 1) : new Vector4(1, 1, 1, 0.5f);
                        if (ImGui.ImageButton((IntPtr)hud.iconTexture.UnmanagedComPointer, buttonSize, uv0, uv1, 1, new Vector4(), tint)) {
                            hud.Visible = !hud.Visible;
                        }
                    }
                    ImGui.PopStyleColor(1); // ImGuiColButton
                    if (ImGui.IsItemHovered()) {
                        ImGui.SetTooltip(hud.Title);
                    }
                    if (HorizontalBar) ImGui.SameLine(0, 2 * scale);
                    else ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (-2 * scale));
                    ImGui.PopID(); // pop button id
                }
                ImGui.PopStyleVar(2); // pop off ImGuiStyleVarWindowPadding / ImGuiStyleVarWindowMinSize
                ImGui.End(); // end hud bar window
            }
            catch (Exception ex) { UBService.LogException(ex); }
        }

        public void Dispose() {
            settingsIcon?.Dispose();
        }
    }
}
