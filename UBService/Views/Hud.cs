using Decal.Adapter.Wrappers;
using ImGuiNET;
using Microsoft.DirectX.Direct3D;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace UBService.Views {
    /// <summary>
    /// Window hud
    /// </summary>
    public class Hud : IDisposable {
        private bool windowIsOpen = true;
        public Texture iconTexture = null;
        public Bitmap iconBitmap;
        private bool _lastVisible = false;
        private bool _needsWindowPositionUpdate = false;

        /// <summary>
        /// Fired before the device is reset, you should unload any textures you have, and recreate them
        /// inside the CreateTextures event.
        /// </summary>
        public event EventHandler DestroyTextures;

        /// <summary>
        /// Fired after the device is reset. You should recreate your textures here.
        /// </summary>
        public event EventHandler CreateTextures;

        /// <summary>
        /// Use this to set state for the next window, like with `ImGui::SetNextWindowPos`
        /// </summary>
        public event EventHandler PreRender;

        /// <summary>
        /// When this is raised, redraw your hud
        /// </summary>
        public event EventHandler Render;

        /// <summary>
        /// When this is raised, show your hud if you are managing your own windows
        /// </summary>
        public event EventHandler ShouldShow;

        /// <summary>
        /// When this is raised, hide your hud if you are managing your own windows
        /// </summary>
        public event EventHandler ShouldHide;

        /// <summary>
        /// Wether to show this hud in the bar
        /// </summary>
        public bool ShowInBar { get; set; } = true;

        /// <summary>
        /// If this is true, you manage showing/hiding/drawing your own window. Use ShouldShow / ShouldHide events
        /// </summary>
        public bool DontDrawDefaultWindow { get; set; } = false;

        /// <summary>
        /// Wether the window is visible or not.  If CustomWindowDrawing is true, this only reflects what the bar thinks.
        /// It's up to you to use ShouldShow/ShouldHide to manage its actual state.
        /// </summary>
        public bool Visible {
            get => windowIsOpen;
            set {
                try {
                    if (_lastVisible != value) {
                        _lastVisible = value;
                        windowIsOpen = value;
                        if (windowIsOpen) {
                            ShouldShow?.Invoke(this, EventArgs.Empty);
                        }
                        else {
                            ShouldHide?.Invoke(this, EventArgs.Empty);
                        }
                    }
                }
                finally { }
            }
        }

        /// <summary>
        /// Window settings flags
        /// </summary>
        public ImGuiWindowFlags WindowSettings { get; set; } = ImGuiWindowFlags.None;

        /// <summary>
        /// Name of the hud. This should be unique to your window. Two windows with the same name will share
        /// state.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Window title if not using CustomWindowDrawing
        /// </summary>
        public string Title { get; set; }

        private uint _windowX = 0;
        private uint _windowY = 0;
        public uint WindowX {
            get => _windowX;
            set {
                if (value != _windowX) {
                    _windowX = value;
                    _needsWindowPositionUpdate = true;
                }
            }
        }
        public uint WindowY { get; set; }
        public ImGuiViewportPtr WindowViewport { get; private set; }

        public Vector2 WindowPositionRelative {
            get {
                var isMainViewport = (WindowViewport.ID == ImGui.GetWindowViewport().ID);
                var x = isMainViewport ? WindowX : WindowViewport.Pos.X;
                return new Vector2(WindowX, WindowY);
            }
        }

        internal Hud() {
            Visible = true;
        }

        internal Hud(string name, Bitmap icon) {
            iconBitmap = icon;
            Name = name;
            Title = name;
            Visible = true;

            if (iconTexture == null && iconBitmap != null) {
                iconTexture = new Texture(UBService.Huds.D3Ddevice, iconBitmap, Usage.Dynamic, Pool.Default);
            }
        }

        internal void CallRender() {
            bool startedWindow = false;
            bool isCollapsed = false;
            try {
                Visible = windowIsOpen;
                if (!windowIsOpen)
                    return;

                if (!DontDrawDefaultWindow && Visible && _needsWindowPositionUpdate) {
                    ImGui.SetNextWindowPos(WindowPositionRelative);
                    _needsWindowPositionUpdate = false;
                }

                PreRender?.Invoke(this, EventArgs.Empty);
                if (!DontDrawDefaultWindow && Visible) {
                    if (ImGui.Begin($"{Title}###{Name}", ref windowIsOpen, WindowSettings)) {
                        startedWindow = true;
                        // turns out exiting early can mess with hud logic that is inside Render event,
                        // so we call Render event if the window is collapsed. NonVisible windows do
                        // not get Render events called, since they shouldn't be doing anything.
                        // return; // early exit if window is collapsed
                        Render?.Invoke(this, EventArgs.Empty);
                    }
                    else {
                        startedWindow = false;
                        isCollapsed = true;
                    }
                    WindowX = (uint)ImGui.GetWindowPos().X;
                    WindowY = (uint)ImGui.GetWindowPos().Y;
                    WindowViewport = ImGui.GetWindowViewport();
                }
                else if (DontDrawDefaultWindow && Visible) {
                    Render?.Invoke(this, EventArgs.Empty);
                }
            }
            finally {
                if (startedWindow) {
                    ImGui.End();
                }
            }
        }

        internal void CallDestroyTextures() {
            UBService.WriteLog($"{Name}: CallDestroyTextures");
            // need to recreate textures when the device is reset
            if (iconTexture != null) {
                iconTexture.Dispose();
                iconTexture = null;
            }
            DestroyTextures?.Invoke(this, EventArgs.Empty);
        }

        internal void CallCreateTextures() {
            if (iconTexture == null && iconBitmap != null) {
                iconTexture = new Texture(UBService.Huds.D3Ddevice, iconBitmap, Usage.Dynamic, Pool.Default);
            }
            CreateTextures?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Destroy the hud
        /// </summary>
        public void Dispose() {
            try {
                iconTexture?.Dispose();
                iconBitmap?.Dispose();
                UBService.Huds.RemoveHud(this);
            }
            catch (Exception ex) { UBService.LogException(ex); }
        }
    }
}
