using System;
using UtilityBelt.Lib;
using UBService.Lib.Settings;
using System.Drawing;
using System.Linq;
using UBService;
using ImGuiNET;
using System.Collections.Generic;
using ACE.DatLoader.FileTypes;
using ACE.DatLoader.Entity;
using Decal.Filters;
using UtilityBelt.Views.Inspector;
using Vector4 = ImGuiNET.Vector4;
using UBService.Views;
using Decal.Adapter.Wrappers;
using System.IO;
using System.Security.Cryptography;
using Hud = UBService.Views.Hud;
using UBService.Views.SettingsEditor;
using UtilityBelt.Views;
using System.Reflection;

namespace UtilityBelt.Tools {
    [Name("GUI")]
    [Summary("GUI test")]
    public class GUI : ToolBase {
        private bool demoIsOpen;
        private Hud demoHudWithCustomWindow;
        private Inspector gameStateInspector;
        private ManagedTexture settingsIcon;

        #region Config
        [Summary("Enabled")]
        public Setting<bool> Enabled = new Setting<bool>(true);

        [Summary("UI max framerate")]
        public Setting<uint> MaxFramerate = new Setting<uint>(60);

        [Summary("Enable Demo UI")]
        public Setting<bool> EnableDemoUI = new Setting<bool>(true);

        [Summary("Enable GameState Inspector")]
        public Setting<bool> EnableGameStateInspector = new Setting<bool>(false);
        #endregion // Config

        #region Commands
        #endregion

        public CharGen CharGen { get; private set; }

        public GUI(UtilityBeltPlugin ub, string name) : base(ub, name) {
        }

        public override void Init() {
            using (Stream manifestResourceStream = GetType().Assembly.GetManifestResourceStream("UtilityBelt.Resources.icons.settings.png")) {
                settingsIcon = new ManagedTexture(new Bitmap(manifestResourceStream));
            }
            CreateHuds();
            demoIsOpen = true;
            base.Init();

            EnableDemoUI.Changed += ShowHud_Changed;
            EnableGameStateInspector.Changed += ShowHud_Changed;
            demoIsOpen = EnableDemoUI.Value;
        }

        private void DemoHudWithCustomWindow_ShouldShow(object sender, EventArgs e) {
            demoIsOpen = true;
        }

        private void DemoHudWithCustomWindow_ShouldHide(object sender, EventArgs e) {
            demoIsOpen = false;
        }

        private Random rand = new Random();

        private unsafe void DemoHudWithCustomWindow_Render(object sender, EventArgs e) {
            demoHudWithCustomWindow.Visible = demoIsOpen;

            var flags = ImGuiWindowFlags.MenuBar;
            var windowIsOpen = ImGui.Begin("Test Window", ref demoIsOpen, flags);


            if (false&& windowIsOpen) {
                if (ImGui.BeginMenuBar()) {
                    if (ImGui.BeginMenu("test")) {
                        ImGui.MenuItem("one");
                        ImGui.MenuItem("two");
                        ImGui.MenuItem("one");
                        ImGui.EndMenu();
                    }
                    if (ImGui.BeginMenu("test 2")) {
                        ImGui.MenuItem("one");
                        ImGui.MenuItem("two");
                        ImGui.MenuItem("one");
                        ImGui.EndMenu();
                    }
                    ImGui.EndMenuBar();
                }
                var pos = ImGui.GetCursorPos();
                //ImGui.InvisibleButton("canvas", size);
                if (ImGui.Button("test body button")) {
                    WriteToChat("test body button clicked");
                }
                ImGui.SetCursorPos(pos);
                ImGui.InvisibleButton("renderbutton", ImGui.GetContentRegionAvail());
                Vector2 p0 = ImGui.GetItemRectMin();
                Vector2 p1 = ImGui.GetItemRectMax();
                var size = new Vector2(p1.X - p0.X, p1.Y - p0.Y);

                var drawList = ImGui.GetWindowDrawList();
                drawList.PushClipRect(p0, p1);
                var t = ImGui.GetTime();
                for (int n = 0; n < (1.0f + Math.Sin(t * 5.7f)) * 40.0f; n++)
                    drawList.AddCircle(new Vector2(p0.X + size.X * 0.5f, p0.Y + size.Y * 0.5f), size.X * (0.01f + n * 0.03f),
                        (0xFF000000 + (((uint)Math.Min(n * 8, 255)) << 16 ) + +(((uint)Math.Min(n * 8, 255)))), 50, 3);

                drawList.PopClipRect();
            }
            ImGui.End();

            ImGui.StyleColorsDark();
            //if (demoIsOpen) 
            ImGui.ShowDemoWindow(ref demoIsOpen);
        }

        private void ShowHud_Changed(object sender, SettingChangedEventArgs e) {
            RefreshHuds();
        }

        private void DestroyHuds() {
            if ((!EnableDemoUI || disposedValue) && demoHudWithCustomWindow != null) {
                demoHudWithCustomWindow.Render -= DemoHudWithCustomWindow_Render;
                demoHudWithCustomWindow.ShouldHide -= DemoHudWithCustomWindow_ShouldHide;
                demoHudWithCustomWindow.ShouldShow -= DemoHudWithCustomWindow_ShouldShow;
                demoHudWithCustomWindow.Dispose();
                demoHudWithCustomWindow = null;
            }

            if ((!EnableGameStateInspector || disposedValue) && gameStateInspector != null) {
                gameStateInspector.Dispose();
                gameStateInspector = null;
            }
        }

        private unsafe void CreateHuds() {
            if (EnableDemoUI && demoHudWithCustomWindow == null) {
                demoHudWithCustomWindow = HudManager.CreateHud("Demo Hud (Custom Window)");
                demoHudWithCustomWindow.DontDrawDefaultWindow = true;
                demoHudWithCustomWindow.Render += DemoHudWithCustomWindow_Render;
                demoHudWithCustomWindow.ShouldHide += DemoHudWithCustomWindow_ShouldHide;
                demoHudWithCustomWindow.ShouldShow += DemoHudWithCustomWindow_ShouldShow;
            }

            if (EnableGameStateInspector && gameStateInspector == null) {
                gameStateInspector = new Inspector("UB", UB);
                //gameStateInspector = new Inspector("GameState", **CObjectMaint.s_pcInstance);
            }
        }

        private void RefreshHuds() {
            DestroyHuds();
            CreateHuds();
        }

        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    EnableDemoUI.Changed += ShowHud_Changed;
                    EnableGameStateInspector.Changed += ShowHud_Changed;

                    disposedValue = true;
                    DestroyHuds();
                    settingsIcon?.Dispose();
                }
            }
        }
    }
}
