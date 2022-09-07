using System;
using UtilityBelt.Lib;
using UBLoader.Lib.Settings;
using System.Drawing;
using System.Linq;
using UBService;
using ImGuiNET;
using System.Collections.Generic;
using ACE.DatLoader.FileTypes;
using ACE.DatLoader.Entity;
using Decal.Filters;
using UtilityBelt.Views.Inspector;
using AcClient;

namespace UtilityBelt.Tools {
    [Name("GUI")]
    [Summary("GUI test")]
    public class GUI : ToolBase {
        private bool demoIsOpen;
        private Hud demoHudWithCustomWindow;
        private Inspector gameStateInspector;

        #region Config
        [Summary("Enabled")]
        public Setting<bool> Enabled = new Setting<bool>(true);

        [Summary("UI max framerate")]
        public Setting<uint> MaxFramerate = new Setting<uint>(60);

        [Summary("Enable Demo UI")]
        public Setting<bool> EnableDemoUI = new Setting<bool>(false);

        [Summary("Enable GameState Inspector")]
        public Setting<bool> EnableGameStateInspector = new Setting<bool>(false);
        #endregion // Config

        #region Commands
        #endregion

        public CharGen CharGen { get; private set; }

        public GUI(UtilityBeltPlugin ub, string name) : base(ub, name) {
        }

        public override void Init() {
            base.Init();

            EnableDemoUI.Changed += ShowHud_Changed;
            EnableGameStateInspector.Changed += ShowHud_Changed;
            demoIsOpen = EnableDemoUI.Value;

            CreateHuds();
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
                demoHudWithCustomWindow.CustomWindowDrawing = true;
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
                }
            }
        }
    }
}
