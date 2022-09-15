using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UBService.Views {
    /// <summary>
    /// Shows a popup window that can't be clicked through until an action button is clicked.
    /// </summary>
    public class PopupModal : IDisposable {
        private static int _nextId = int.MinValue;
        private int _id;
        private bool _didOpen = false;
        private string _title;
        private string _body;
        private IDictionary<string, Action<string>> _buttons;

        /// <summary>
        /// The hud this popup modal is using
        /// </summary>
        public Hud Hud { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="title">The title of the popup</param>
        /// <param name="body">The body text of the popup</param>
        /// <param name="buttons">A dictionary of buttonName/Actions for each button</param>
        public PopupModal(string title, string body, IDictionary<string, Action<string>> buttons = null) {
            _id = _nextId++;
            _title = title;
            _body = body;
            _buttons = buttons;

            Hud = UBService.Huds.CreateHud(title);
            Hud.DontDrawDefaultWindow = true;
            Hud.ShowInBar = true;

            Hud.ShouldHide += Hud_ShouldHide;
            Hud.Render += Hud_Render;
        }

        private void Hud_Render(object sender, EventArgs e) {
            Vector2 center = ImGui.GetMainViewport().GetCenter();
            ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            if (ImGui.BeginPopupModal($"{_title}###{_id}")) {
                ImGui.Spacing();
                ImGui.Spacing();
                ImGui.Text(_body);
                ImGui.Spacing();
                ImGui.Spacing();
                ImGui.Separator();

                if (_buttons == null || _buttons.Count == 0) {
                    if (ImGui.Button("Ok", new Vector2(120, 0))) {
                        ImGui.CloseCurrentPopup();
                        Dispose();
                    }
                }
                else {
                    foreach (var kv in _buttons) {
                        if (ImGui.Button(kv.Key, new Vector2(120, 0))) {
                            kv.Value?.Invoke(kv.Key);
                            ImGui.CloseCurrentPopup();
                            Dispose();
                        }
                        ImGui.SameLine();
                    }
                }
                ImGui.SetItemDefaultFocus();
                ImGui.EndPopup();
            }

            if (!_didOpen) {
                ImGui.OpenPopup($"{_title}###{_id}");
                _didOpen = true;
            }
        }

        private void Hud_ShouldHide(object sender, EventArgs e) {
            Dispose();
        }

        private bool _isDisposed = false;
        /// <summary>
        /// Dispose this to close it.
        /// </summary>
        public void Dispose() {
            if (_isDisposed)
                return;

            Hud.ShouldHide -= Hud_ShouldHide;
            Hud.Render -= Hud_Render;
            Hud.Dispose();
            Hud = null;
            _isDisposed = true;
        }
    }
}
