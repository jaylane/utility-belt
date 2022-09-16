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
using System.Windows;
using System.Xml.Linq;
using UBService.Lib;
using UBService.Lib.Settings;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace UBService.Views {

    /// <summary>
    /// Manages ImGui huds
    /// </summary>
    public class HudManager : ISetting, IDisposable {
        internal DateTime _lastRender = DateTime.MinValue;
        internal bool didInit;
        internal List<Hud> huds = new List<Hud>();
        internal List<ManagedTexture> textures = new List<ManagedTexture>();

        internal Guid IID_IDirect3DDevice9 = new Guid("{D0223B96-BF7A-43fd-92BD-A43B0D82B9EB}");
        internal Device D3Ddevice;
        internal IntPtr unmanagedD3dPtr;
        internal IntPtr _context;

        private readonly float MAX_UI_FRAMERATE = 60;

        private bool needsTextures = true;
        private bool isResetting;

        internal string themesDir = Path.Combine(UBService.AssemblyDirectory, "themes");
        internal string profilesDir = Path.Combine(UBService.AssemblyDirectory, "settings");

        /// <summary>
        /// The file path to the currently loaded views profile
        /// </summary>
        public string CurrentProfilePath {
            get {
                if (!UBService.IsInGame) {
                    return Path.Combine(profilesDir, $"global.views.json");
                }
                else if (Profile == "[character]") {
                    try {
                        return Path.Combine(profilesDir, $"__{CoreManager.Current.CharacterFilter.AccountName}_{CoreManager.Current.CharacterFilter.Name}.views.json");
                    }
                    catch {
                        return Path.Combine(profilesDir, $"global.views.json");
                    }
                }
                else {
                    return Path.Combine(profilesDir, $"{Profile}.views.json");
                }
            }
        }

        public Toaster Toaster;
        public HudBar HudBar;

        #region Config
        [Summary("Theme storage directory path")]
        public ViewsProfileSetting<string> ThemeStorageDirectory = new ViewsProfileSetting<string>(Path.Combine(Path.GetDirectoryName(Assembly.GetAssembly(typeof(UBService)).Location), "themes"));

        [Summary("Current Theme")]
        public ViewsProfileSetting<string> CurrentThemeName = new ViewsProfileSetting<string>("Dark");

        [Summary("Enable viewports. (Ability to render plugin windows outside of the client window)")]
        public ViewsProfileSetting<bool> Viewports = new ViewsProfileSetting<bool>(true);

        [Summary("View Settings Profile")]
        public CharacterSetting<string> Profile = new CharacterSetting<string>("[character]");

        [Summary("Font")]
        [Choices("ProggyClean.ttf", typeof(GetAvailableFonts))]
        public ViewsProfileSetting<string> Font = new ViewsProfileSetting<string>("ProggyClean.ttf");

        [Summary("Font Size (requires client restart)")]
        public ViewsProfileSetting<int> FontSize = new ViewsProfileSetting<int>(13);
        #endregion // Config

        internal class GetAvailableFonts : IChoiceResults {
            public IList<string> GetChoices() {
                var fonts = new List<string>();
                for (var i = 0; i < ImGui.GetIO().Fonts.Fonts.Size; i++) {
                    var font = ImGui.GetIO().Fonts.Fonts[i].GetDebugName().Split(',').First();
                    fonts.Add(font);
                }
                return fonts;
            }
        }

        /// <summary>
        /// Current global theme
        /// </summary>
        public UBServiceTheme CurrentTheme { get; internal set; } = new UBServiceTheme();

        public HudManager() {
            Toaster = new Toaster();
            HudBar = new HudBar();
        }

        internal unsafe void Init() {
            _context = ImGui.CreateContext();
            ImGui.SetCurrentContext(_context);
            if (Viewports)
                ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;
            else
                ImGui.GetIO().ConfigFlags &= ~ImGuiConfigFlags.ViewportsEnable;
            ImGui.GetIO().IniSavingRate = float.MinValue; // no ini saving

            var defaultThemePath = Path.Combine(themesDir, $"{CurrentThemeName}.json");
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
            //ImGuiImpl.ImGui_ImplWin32_EnableDpiAwareness();
            var ret2 = ImGuiImpl.ImGui_ImplDX9_Init(unmanagedD3dPtr);
            CurrentThemeName.Changed += CurrentThemeName_Changed;
            Viewports.Changed += Viewports_Changed;
            Font.Changed += Font_Changed;

            Toaster.Init();
            HudBar.Init();

            var fontsList = Directory.GetFiles(Path.Combine(UBService.AssemblyDirectory, "fonts"), "*.ttf");
            if (fontsList.Length > 0) {
                var fonts = ImGui.GetIO().Fonts;

                //fonts.AddFontDefault();
                foreach (var fontPath in fontsList) {
                    var cFontPath = fontPath.Replace("\\", "\\\\");
                    fonts.AddFontFromFileTTF(cFontPath, FontSize, null, ImGui.GetIO().Fonts.GetGlyphRangesDefault());
                }

                ImGui.GetIO().Fonts.Build();

                Font_Changed(null, null);
            }

            didInit = true;
        }

        internal unsafe void Font_Changed(object sender, SettingChangedEventArgs e) {
            var io = ImGui.GetIO();
            for (var i = 0; i < io.Fonts.Fonts.Size; i++) {
                var font = io.Fonts.Fonts[i];
                UBService.WriteLog($"Check font: {font.GetDebugName()} vs {Font.Value}");
                if (font.GetDebugName().Split(',').First().Equals(Font.Value)) {
                    io.FontDefault = font;
                    UBService.WriteLog($"Push font: {font.GetDebugName()}");
                    break;
                }
            }
        }

        private void Viewports_Changed(object sender, SettingChangedEventArgs e) {
            //*
            if (Viewports)
                ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;
            else
                ImGui.GetIO().ConfigFlags &= ~ImGuiConfigFlags.ViewportsEnable;
            //*/
        }

        private void CurrentThemeName_Changed(object sender, SettingChangedEventArgs e) {
            var defaultThemePath = Path.Combine(themesDir, $"{CurrentThemeName}.json");
            UBService.WriteLog($"CurrentThemeName_Changed: {defaultThemePath}");
            if (File.Exists(defaultThemePath)) {
                try {
                    var themeJson = File.ReadAllText(defaultThemePath);
                    CurrentTheme = JsonConvert.DeserializeObject<UBServiceTheme>(themeJson);
                    CurrentTheme.Apply();
                    UBService.WriteLog($"ApplyTheme: {defaultThemePath}");
                }
                catch (Exception ex) { UBService.LogException(ex); }
            }
        }

        /// <summary>
        /// Create a new hud
        /// </summary>
        /// <param name="name">Name of the hud</param>
        /// <param name="icon">Icon for the HudBar, if null uses first letter of Hud.Name</param>
        /// <returns>A new hud</returns>
        public Hud CreateHud(string name, Bitmap icon = null) {
            var hud = new Hud(name, icon);

            huds.Add(hud);

            return hud;
        }

        internal void RemoveHud(Hud hud) {
            huds.Remove(hud);
        }

        internal unsafe bool WindowMessage(int hWND, short uMsg, int wParam, int lParam) {
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

        internal unsafe void DoRender() {
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

                HudBar.Render();

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

        private void CheckTextures() {
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

        internal void PreReset() {
            UBService.WriteLog("PreReset -> Clearing textures");
            isResetting = true;

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
            ImGuiImpl.ImGui_ImplDX9_InvalidateDeviceObjects();

            needsTextures = true;
        }

        internal void PostReset() {
            isResetting = false;
        }

        internal void AddManagedTexture(ManagedTexture managedTexture) {
            textures.Add(managedTexture);
        }

        internal void RemoveManagedTexture(ManagedTexture managedTexture) {
            textures.Remove(managedTexture);
        }

        public void Dispose() {
            CurrentThemeName.Changed -= CurrentThemeName_Changed;
            Viewports.Changed -= Viewports_Changed;
            Toaster?.Dispose();
            HudBar?.Dispose();
        }
    }
}
