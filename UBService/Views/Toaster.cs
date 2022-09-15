using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UBService.Views {
    public class Toaster : IDisposable {
        public enum ToastType {
            Success,
            Warning,
            Info,
            Error
        }

        public Hud Hud { get; private set; }

        private List<Toast> _toasts = new List<Toast>();
        private static int _nextId = int.MinValue;

        private class Toast {
            public int Id;
            public string Message;
            public TimeSpan Duration = TimeSpan.FromSeconds(10);
            public ToastType Type;
            public DateTime StartTime;

            public Toast(string message, TimeSpan duration, ToastType type) {
                Id = _nextId++;
                Message = message;
                Duration = duration;
                Type = type;
                StartTime = DateTime.UtcNow;
            }
        }

        public void Init() {
            Hud = UBService.Huds.CreateHud("Toaster");
            Hud.ShowInBar = false;
            Hud.DontDrawDefaultWindow = true;
            Hud.Render += Hud_Render;
        }

        public void Add(string message, ToastType type) {
            _toasts.Add(new Toast(message, TimeSpan.FromSeconds(10), type));
        }

        private void Hud_Render(object sender, EventArgs e) {
            var toasts = _toasts.ToList();
            var offset = 0f;

            foreach (var toast in toasts) {
                if (DateTime.UtcNow - toast.StartTime >= toast.Duration) {
                    _toasts.Remove(toast);
                    continue;
                }

                var io = ImGui.GetIO();
                ImGuiWindowFlags window_flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav;
                var PAD = 10.0f;
                var viewport = ImGui.GetMainViewport();
                var work_pos = viewport.WorkPos; // Use work area to avoid menu-bar/task-bar, if any!
                var work_size = viewport.WorkSize;
                var window_pos = new Vector2();
                var window_pos_pivot = new Vector2();
                window_pos.X = work_pos.X + work_size.X - PAD;
                window_pos.Y = work_pos.Y + PAD + offset;
                window_pos_pivot.X = 1f;
                window_pos_pivot.Y = 0.0f;
                ImGui.SetNextWindowPos(window_pos, ImGuiCond.Always, window_pos_pivot);
                ImGui.SetNextWindowViewport(viewport.ID);
                window_flags |= ImGuiWindowFlags.NoMove;
                ImGui.SetNextWindowBgAlpha(0.75f); // Transparent background
                if (ImGui.Begin(toast.Id.ToString(), window_flags)) {
                    switch (toast.Type) {
                        case ToastType.Success:
                            ImGui.TextColored(UBService.Huds.CurrentTheme.Colors.Success, toast.Message);
                            break;
                        case ToastType.Warning:
                            ImGui.TextColored(UBService.Huds.CurrentTheme.Colors.Warning, toast.Message);
                            break;
                        case ToastType.Error:
                            ImGui.TextColored(UBService.Huds.CurrentTheme.Colors.Error, toast.Message);
                            break;
                        case ToastType.Info:
                            ImGui.TextColored(UBService.Huds.CurrentTheme.Colors.Text, toast.Message);
                            break;
                    }
                    if (ImGui.IsItemClicked()) {
                        _toasts.Remove(toast);
                    }
                }
                offset += ImGui.GetWindowSize().Y + PAD;
                ImGui.End();
            }
        }

        public void Dispose() {
            Hud.Render -= Hud_Render;
            Hud.Dispose();
            Hud = null;
        }
    }
}
