using System;
using UtilityBelt.Lib;
using UBLoader.Lib.Settings;
using System.Drawing;
using System.Linq;
using UBService;
using System.IO;
using UtilityBelt.Views;
using ImGuiNET;
using System.Collections.Generic;
using ACE.DatLoader.FileTypes;
using ACE.DatLoader.Entity;
using Decal.Filters;

namespace UtilityBelt.Tools {
    [Name("GUI")]
    [Summary("GUI test")]
    public class GUI : ToolBase {
        private bool demoIsOpen;
        private Hud demoHudWithCustomWindow;

        #region Config
        [Summary("Enabled")]
        public Setting<bool> Enabled = new Setting<bool>(true);

        [Summary("UI max framerate")]
        public Setting<uint> MaxFramerate = new Setting<uint>(60);

        [Summary("Show Demo UI")]
        public Setting<bool> ShowDemoUI = new Setting<bool>(true);
        #endregion // Config

        #region Commands
        #endregion

        public CharGen CharGen { get; private set; }

        public GUI(UtilityBeltPlugin ub, string name) : base(ub, name) {
        }

        public override void Init() {
            base.Init();

            ShowDemoUI.Changed += ShowDemoUI_Changed; 
            demoIsOpen = ShowDemoUI.Value;

            if (ShowDemoUI) {
                CreateHuds();
            }
        }

        private void CreateHuds() {
            if (demoHudWithCustomWindow == null) {
                demoHudWithCustomWindow = HudManager.CreateHud("Demo Hud (Custom Window)");
                demoHudWithCustomWindow.CustomWindowDrawing = true;
                demoHudWithCustomWindow.Render += DemoHudWithCustomWindow_Render;
                demoHudWithCustomWindow.ShouldHide += DemoHudWithCustomWindow_ShouldHide;
                demoHudWithCustomWindow.ShouldShow += DemoHudWithCustomWindow_ShouldShow;
            }
        }

        private void DemoHudWithCustomWindow_ShouldShow(object sender, EventArgs e) {
            demoIsOpen = true;
        }

        private void DemoHudWithCustomWindow_ShouldHide(object sender, EventArgs e) {
            demoIsOpen = false;
        }

        private void DemoHudWithCustomWindow_Render(object sender, EventArgs e) {
            demoHudWithCustomWindow.Visible = demoIsOpen;
            if (demoIsOpen)
                ImGui.ShowDemoWindow(ref demoIsOpen);
        }

        private void ShowDemoUI_Changed(object sender, SettingChangedEventArgs e) {
            if (ShowDemoUI)
                CreateHuds();
            else
                DestroyHuds();
        }

        private void DestroyHuds() {
            if (demoHudWithCustomWindow != null) {
                demoHudWithCustomWindow.Render -= DemoHudWithCustomWindow_Render;
                demoHudWithCustomWindow.ShouldHide -= DemoHudWithCustomWindow_ShouldHide;
                demoHudWithCustomWindow.ShouldShow -= DemoHudWithCustomWindow_ShouldShow;
                demoHudWithCustomWindow.Dispose();
                demoHudWithCustomWindow = null;
            }
        }

        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    DestroyHuds();
                }
                disposedValue = true;
            }
        }
    }
}
