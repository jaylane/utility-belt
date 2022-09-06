using Decal.Adapter;
using Decal.Adapter.Wrappers;
using ImGuiNET;
using Microsoft.DirectX.Direct3D;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Contexts;
using System.Security;
using System.Text;
using System.Xml.Linq;

namespace UBService {

    /// <summary>
    /// Manages ImGui huds
    /// </summary>
    public static class HudManager {
        internal static DateTime _lastRender = DateTime.MinValue;
        internal static bool didInit;
        internal static List<Hud> huds = new List<Hud>();

        internal static Guid IID_IDirect3DDevice9 = new Guid("{D0223B96-BF7A-43fd-92BD-A43B0D82B9EB}");
        internal static Device D3Ddevice;
        internal static IntPtr unmanagedD3dPtr;
        internal static IntPtr _context;

        private static readonly float MAX_UI_FRAMERATE = 60;

        private static bool _barIsHorizontal = true;
        private static bool _barIsOpen = true;
        private static bool needsTextures = true;
        private static bool isResetting;

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
                _context = ImGui.CreateContext(null);
                ImGui.SetCurrentContext(_context);
                ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;

                // todo: themes
                ImGui.StyleColorsDark(null);

                object d3dDevice = UBService.iDecal.GetD3DDevice(ref IID_IDirect3DDevice9);
                Marshal.QueryInterface(Marshal.GetIUnknownForObject(d3dDevice), ref IID_IDirect3DDevice9, out unmanagedD3dPtr);
                D3Ddevice = new Device(unmanagedD3dPtr);
                var ret1 = ImGuiImpl.ImGui_ImplWin32_Init((IntPtr)UBService.iDecal.HWND);
                var ret2 = ImGuiImpl.ImGui_ImplDX9_Init(unmanagedD3dPtr);

                didInit = true;
            }
        }

        private unsafe static void RenderHudBar() {
            try {
                var _huds = huds.ToList();
                if (!_huds.Any(h => h != null && h.ShowInBar)) {
                    return;
                }

                uint i = 0;
                ImGuiWindowFlags windowSettings = ImGuiWindowFlags.NoDecoration;
                windowSettings |= ImGuiWindowFlags.NoDocking;
                windowSettings |= ImGuiWindowFlags.NoTitleBar;
                windowSettings |= ImGuiWindowFlags.NoScrollbar;
                windowSettings |= ImGuiWindowFlags.NoResize;
                windowSettings |= ImGuiWindowFlags.NoNav;
                windowSettings |= ImGuiWindowFlags.NoFocusOnAppearing;
                windowSettings |= ImGuiWindowFlags.AlwaysAutoResize;

                var initialPos = new Vector2(150, 4);
                var minSize = new Vector2(20, 20);
                var pivot = new Vector2(0, 0);
                var framePadding = new Vector2(6, 6);
                ImGui.SetNextWindowBgAlpha(0.5f);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, framePadding);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, minSize);
                ImGui.SetNextWindowPos(initialPos, ImGuiCond.FirstUseEver, pivot);

                ImGui.Begin("UBService HudBar", ref _barIsOpen, windowSettings);

                ImGui.PushID((++i).ToString()); // need to give each image button a unique id (why?) and pop it later
                if (ImGui.Button("", new Vector2(_barIsHorizontal ? 6 : 18, _barIsHorizontal ? 18 : 6 ))) {
                    _barIsHorizontal = !_barIsHorizontal;
                }
                ImGui.PopID(); // pop button id
                if (_barIsHorizontal) ImGui.SameLine(0, 2);

                foreach (var hud in _huds) {
                    if (hud == null || !hud.ShowInBar)
                        continue;

                    ImGui.PushID($"hudIcon_{i}"); // need to give each image button a unique id (why?) and pop it later
                    var buttonSize = new Vector2(18, 18);

                    if (hud.Visible) {
                        //bgColor.w = 0.2f;
                    }

                    ImGui.PushStyleColor(ImGuiCol.Button, (uint)Color.Red.ToArgb());
                    if (hud.iconTexture == null) {
                        var letter = hud.Name.FirstOrDefault().ToString();
                        if (string.IsNullOrEmpty(letter)) letter = "?";
                        if (ImGui.Button(letter, buttonSize)) {
                            hud.Visible = !hud.Visible;
                        }
                    }
                    else {
                        var uv0 = new Vector2(0, 0);
                        var uv1 = new Vector2(1, 1);
                        if (ImGui.ImageButton((IntPtr)hud.iconTexture.UnmanagedComPointer, buttonSize, uv0, uv1, 0, new Vector4(255, 255, 0, 255))) {
                            hud.Visible = !hud.Visible;
                        }
                    }
                    ImGui.PopStyleColor(1); // ImGuiColButton
                    if (ImGui.IsItemHovered(0)) {
                        ImGui.BeginTooltip();
                        ImGui.Text(hud.Name);
                        ImGui.EndTooltip();
                    }
                    if (_barIsHorizontal) ImGui.SameLine(0, 2);
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

            if ((io.WantCaptureMouse > 0 && isMouseEvent) || (io.WantTextInput > 0 && isKeyboardEvent)) {
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

            if (needsTextures) {
                UBService.WriteLog("Making textures");
                ImGuiImpl.ImGui_ImplDX9_CreateDeviceObjects();
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

        internal static void PreReset() {
            isResetting = true;
            ImGuiImpl.ImGui_ImplDX9_InvalidateDeviceObjects();

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
    }
}
