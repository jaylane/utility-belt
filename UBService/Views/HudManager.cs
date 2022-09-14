using Decal.Adapter;
using Decal.Adapter.Wrappers;
using ImGuiNET;
using Microsoft.DirectX.Direct3D;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Contexts;
using System.Security;
using System.Text;
using System.Xml.Linq;
using UBService.Lib;

namespace UBService.Views {

    /// <summary>
    /// Manages ImGui huds
    /// </summary>
    public static class HudManager {
        internal static DateTime _lastRender = DateTime.MinValue;
        internal static bool didInit;
        public static List<Hud> huds = new List<Hud>();
        public static List<ManagedTexture> textures = new List<ManagedTexture>();

        internal static Guid IID_IDirect3DDevice9 = new Guid("{D0223B96-BF7A-43fd-92BD-A43B0D82B9EB}");
        internal static Device D3Ddevice;
        internal static IntPtr unmanagedD3dPtr;
        internal static IntPtr _context;

        private static readonly float MAX_UI_FRAMERATE = 60;

        private static bool needsTextures = true;
        private static bool isResetting;

        private static bool _barIsHorizontal = true;
        private static bool _barIsOpen = true;
        private static bool _needsDeltaReset = false;
        private static bool _barLocked = false;

        private static Vector2 _lastBarDragDelta = new Vector2(0, 0);
        private static string _selectedTheme = "dark";
        private static ManagedTexture settingsIcon;

        private static ThemeEditor themeEditor = null;

        private static string themesDir = Path.Combine(Path.GetDirectoryName(Assembly.GetAssembly(typeof(UBService)).Location), "themes");

        public static Toaster Toaster { get; private set; } = new Toaster();

        /// <summary>
        /// Current global theme
        /// </summary>
        public static UBServiceTheme CurrentTheme { get; private set; } = new UBServiceTheme();


        /// <summary>
        /// Create a new hud
        /// </summary>
        /// <param name="name">Name of the hud</param>
        /// <param name="icon">Icon for the HudBar, if null uses first letter of Hud.Name</param>
        /// <returns>A new hud</returns>
        public static Hud CreateHud(string name, Bitmap icon = null) {
            var hud = new Hud(name, icon);

            huds.Add(hud);

            return hud;
        }

        internal static void RemoveHud(Hud hud) {
            huds.Remove(hud);
        }

        internal static void ChangeDirectX() {
            //UBService.WriteLog($"ChangeDirectX: {didInit}");
            if (!didInit) {
                _context = ImGui.CreateContext();
                ImGui.SetCurrentContext(_context);
                ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;
                ImGui.GetIO().IniSavingRate = float.MinValue; // no ini saving
                //var nullString = Encoding.UTF8.GetBytes("\0");
                //ImGui.GetIO().IniFilename = new NullTerminatedString(); // no ini saving

                // todo: themes
                var defaultThemePath = Path.Combine(themesDir, "Dark.json");
                if (File.Exists(defaultThemePath)) {
                    try {
                        var themeJson = File.ReadAllText(defaultThemePath);
                        CurrentTheme = JsonConvert.DeserializeObject<UBServiceTheme>(themeJson);
                    }
                    catch (Exception ex) { UBService.LogException(ex); }
                }

                CurrentTheme.Apply();

                object d3dDevice = UBService.iDecal.GetD3DDevice(ref IID_IDirect3DDevice9);
                Marshal.QueryInterface(Marshal.GetIUnknownForObject(d3dDevice), ref IID_IDirect3DDevice9, out unmanagedD3dPtr);
                D3Ddevice = new Device(unmanagedD3dPtr);
                var ret1 = ImGuiImpl.ImGui_ImplWin32_Init((IntPtr)UBService.iDecal.HWND);
                var ret2 = ImGuiImpl.ImGui_ImplDX9_Init(unmanagedD3dPtr);

                didInit = true;

                using (Stream manifestResourceStream = typeof(UBService).Assembly.GetManifestResourceStream("UBService.Resources.icons.settings.png")) {
                    settingsIcon = new ManagedTexture(new Bitmap(manifestResourceStream));
                }
            }
        }

        private unsafe static void RenderHudBar() {
            var scale = ImGui.GetIO().FontGlobalScale;

            try {
                var _huds = huds.Where(h => h.ShowInBar).ToList();
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
                if (_barIsHorizontal)
                    ImGui.SetNextWindowSize(new Vector2(longSize, shortSize));
                else
                    ImGui.SetNextWindowSize(new Vector2(shortSize, longSize));

                ImGui.Begin("UBService HudBar", ref _barIsOpen, windowSettings);
                ImGui.PushStyleColor(ImGuiCol.Button, 0); // button bg
                if (ImGui.TextureButton("SettingsIcon", settingsIcon, buttonSize, 1)) {
                    //WriteToChat($"Clicked Settings");
                }
                ImGui.PopStyleColor(); // button bg

                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, originalWindowPadding); // menu padding
                if (ImGui.BeginPopupContextItem()) {
                    if (ImGui.BeginMenu("Themes")) {
                        var themesList = Directory.GetFiles(themesDir, "*.json").Select(p => p.Split('\\').Last()).ToList();
                        themesList.Sort();
                        foreach (var theme in themesList) {
                            if (ImGui.MenuItem(theme.Replace(".json", ""))) {
                                var themePath = Path.Combine(Path.Combine(Path.GetDirectoryName(Assembly.GetAssembly(typeof(UBService)).Location), "themes"), theme);
                                if (File.Exists(themePath)) {
                                    try {
                                        var themeJson = File.ReadAllText(themePath);
                                        CurrentTheme = JsonConvert.DeserializeObject<UBServiceTheme>(themeJson);
                                        CurrentTheme.Apply();
                                    }
                                    catch (Exception ex) { UBService.LogException(ex); }
                                }
                            }
                        }
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
                    if (ImGui.MenuItem("Lock bar", "", ref _barLocked)) {
                        //Logger.WriteToChat("Clicked Locked");
                    }
                    ImGui.MenuItem("Horizontal bar", "", ref _barIsHorizontal);
                    ImGui.EndPopup();
                }
                else if (!_barLocked && ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left)) {
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

                if (_barIsHorizontal) ImGui.SameLine(0, 2 * scale);
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
                    if (_barIsHorizontal) ImGui.SameLine(0, 2 * scale);
                    else ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (-2 * scale));
                    ImGui.PopID(); // pop button id
                }
                ImGui.PopStyleVar(2); // pop off ImGuiStyleVarWindowPadding / ImGuiStyleVarWindowMinSize
                ImGui.End(); // end hud bar window
            }
            catch (Exception ex) { UBService.LogException(ex); }
        }

        internal unsafe static bool WindowMessage(int hWND, short uMsg, int wParam, int lParam) {
            bool eat = false;

            if (ImGui.GetCurrentContext() == IntPtr.Zero)
                return eat;

            ImGuiImpl.ImGui_ImplWin32_WndProcHandler((void*)(IntPtr)hWND, (uint)uMsg, (IntPtr)wParam, (IntPtr)lParam);

            var io = ImGui.GetIO();
            bool isMouseEvent = uMsg >= 0x0201 && uMsg <= 0x020e;
            bool isKeyboardEvent = uMsg >= 0x0100 && uMsg <= 0x0102;

            if ((io.WantCaptureMouse > 0 && isMouseEvent) || (io.WantCaptureKeyboard > 0 && isKeyboardEvent)) {
                // handling input should cause an immediate re-render
                _lastRender = DateTime.MinValue;
                eat = true;
            }

            /*
            try {
                if (io.WantCaptureKeyboard > 0 && isKeyboardEvent) {
                    CoreManager.Current.Actions.AddChatText($"EAT (eat {eat}) 0x{uMsg:X4} WantCaptureKeyboard: {io.WantCaptureKeyboard} WantTextInput: {io.WantTextInput} IsKeyboardEvent: {isKeyboardEvent}", 1);
                }
                else if (uMsg != 0x0084) {
                    CoreManager.Current.Actions.AddChatText($"PASS (eat: {eat}) 0x{uMsg:X4} WantCaptureKeyboard: {io.WantCaptureKeyboard} WantTextInput: {io.WantTextInput} IsKeyboardEvent: {isKeyboardEvent}", 1);
                }
            }
            catch { }
            */
            return eat;
        }

        internal static unsafe void DoRender() {
            if (!didInit || isResetting)
                return;

            CheckTextures();

            // limit uis to MaxFramerate
            if ((DateTime.UtcNow - _lastRender).TotalMilliseconds > (1000f / (float)MAX_UI_FRAMERATE)) {
                var io = ImGui.GetIO();
                _lastRender = DateTime.UtcNow;
                ImGuiImpl.ImGui_ImplDX9_NewFrame();
                ImGuiImpl.ImGui_ImplWin32_NewFrame();
                ImGui.NewFrame();

                var _huds = huds.ToArray();
                foreach (var hud in _huds) {
                    hud?.CallRender();
                }

                RenderHudBar();

                ImGui.EndFrame();
                ImGui.Render();
                
                // Update and Render additional Platform Windows
                if ((io.ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0) {
                    ImGui.UpdatePlatformWindows();
                    ImGui.RenderPlatformWindowsDefault();
                }
            }

            ImGuiImpl.ImGui_ImplDX9_RenderDrawData((IntPtr)ImGui.GetDrawData().NativePtr);
        }

        private static void CheckTextures() {
            if (needsTextures) {
                UBService.WriteLog("Making textures");
                ImGuiImpl.ImGui_ImplDX9_CreateDeviceObjects();

                var _managedTextures = textures.ToArray();
                foreach (var managedTexture in _managedTextures) {
                    managedTexture.CreateTexture();
                }

                var _huds = huds.ToArray();
                foreach (var hud in _huds) {
                    try {
                        hud.CallCreateTextures();
                    }
                    catch (Exception ex) { UBService.LogException(ex); }
                }
                needsTextures = false;
                UBService.WriteLog("done Making textures");
            }
        }

        internal static void PreReset() {
            isResetting = true;
            ImGuiImpl.ImGui_ImplDX9_InvalidateDeviceObjects();

            var _managedTextures = textures.ToArray();
            foreach (var managedTexture in _managedTextures) {
                managedTexture.ReleaseTexture();
            }

            var _huds = huds.ToArray();
            foreach (var hud in _huds) {
                try {
                    hud.CallDestroyTextures();
                }
                catch (Exception ex) { UBService.LogException(ex); }
            }

            needsTextures = true;
        }

        internal static void PostReset() {
            isResetting = false;
        }

        internal static void AddManagedTexture(ManagedTexture managedTexture) {
            textures.Add(managedTexture);
        }

        internal static void RemoveManagedTexture(ManagedTexture managedTexture) {
            textures.Remove(managedTexture);
        }
    }
}
