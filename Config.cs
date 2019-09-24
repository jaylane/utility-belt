using Decal.Adapter;
using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Mag.Shared.Settings;
using System.Drawing;

namespace UtilityBelt {
    public class Config : IDisposable {
        private bool disposed;

        public AutoSalvageConfig AutoSalvage;
        public AutoVendorConfig AutoVendor;
        public DungeonMapsConfig DungeonMaps;
        public InventoryManagerConfig InventoryManager;
        public VisualNavConfig VisualNav;

        public class InventoryManagerConfig : IDisposable {
            public Setting<bool> AutoCram;
            public Setting<bool> AutoStack;
            public Setting<bool> Debug;

            private bool disposed = false;

            public InventoryManagerConfig() {
                try {
                    AutoCram = new Setting<bool>("Config/InventoryManager/AutoCram", "Automatically cram items into side packs", false);
                    AutoStack = new Setting<bool>("Config/InventoryManager/AutoStack", "Automatically combine stacked items", false);
                    Debug = new Setting<bool>("Config/InventoryManager/Debug", "Show debug messages", false);
                }
                catch (Exception e) { Logger.LogException(e); }
            }

            public void Dispose() {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing) {
                if (!disposed) {
                    if (disposing) {
                        if (AutoCram != null) Debug.Dispose();
                        if (AutoStack != null) Debug.Dispose();
                        if (Debug != null) Debug.Dispose();
                    }
                    disposed = true;
                }
            }
        }

        public class AutoSalvageConfig : IDisposable {
            public Setting<bool> Think;
            public Setting<bool> Debug;

            private bool disposed = false;

            public AutoSalvageConfig() {
                try {
                    Think = new Setting<bool>("Config/AutoSalvage/Think", "Think to yourself when finished", true);
                    Debug = new Setting<bool>("Config/AutoSalvage/Debug", "Show debug messages", false);
                }
                catch (Exception e) { Logger.LogException(e); }
            }

            public void Dispose() {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing) {
                if (!disposed) {
                    if (disposing) {
                        if (Think != null) Think.Dispose();
                        if (Debug != null) Debug.Dispose();
                    }
                    disposed = true;
                }
            }
        }
        
        public class AutoVendorConfig : IDisposable {
            public Setting<bool> Enabled;
            public Setting<bool> TestMode;
            public Setting<bool> ShowMerchantInfo;
            public Setting<bool> Think;
            public Setting<bool> Debug;
            public Setting<int> Speed;

            private bool disposed = false;

            public AutoVendorConfig() {
                try {
                    Enabled = new Setting<bool>("Config/AutoVendor/Enabled", "Enable AutoVendor", false);
                    TestMode = new Setting<bool>("Config/AutoVendor/TestMode", "Enable TestMode", false);
                    ShowMerchantInfo = new Setting<bool>("Config/AutoVendor/ShowMerchantInfo", "Show merchant info on approach", true);
                    Think = new Setting<bool>("Config/AutoVendor/Think", "Think to yourself when finished", true);
                    Debug = new Setting<bool>("Config/AutoVendor/Debug", "Show debug messages", false);
                    Speed = new Setting<int>("Config/AutoVendor/Speed", "Delay between autovendor actions", 500);
                }
                catch (Exception e) { Logger.LogException(e); }
            }

            public void Dispose() {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing) {
                if (!disposed) {
                    if (disposing) {
                        if (Enabled != null) Enabled.Dispose();
                        if (TestMode != null) TestMode.Dispose();
                        if (ShowMerchantInfo != null) ShowMerchantInfo.Dispose();
                        if (Think != null) Think.Dispose();
                        if (Speed != null) Speed.Dispose();
                        if (Debug != null) Debug.Dispose();
                    }
                    disposed = true;
                }
            }
        }

        public class DungeonMapsConfig : IDisposable {
            public Setting<bool> Enabled;
            public Setting<bool> DrawWhenClosed;
            public Setting<bool> Debug;
            public Setting<int> Opacity;
            public Setting<int> MapWindowX;
            public Setting<int> MapWindowY;
            public Setting<bool> ShowVisitedTiles;

            private bool disposed = false;

            public DungeonMapsConfig() {
                try {
                    Enabled = new Setting<bool>("Config/DungeonMaps/Enabled", "Enable Dungeon Maps", false);
                    DrawWhenClosed = new Setting<bool>("Config/DungeonMaps/DrawWhenClosed", "Draw maps even when the decal window is closed", true);
                    Debug = new Setting<bool>("Config/DungeonMaps/Debug", "Show debug messages", false);
                    Opacity = new Setting<int>("Config/DungeonMaps/Opacity", "Overall map opacity (0-20, 20 is opaque)", 15);
                    MapWindowX = new Setting<int>("Config/DungeonMaps/MapWindowX", "Saved map window X position (left is 0)", 200);
                    MapWindowY = new Setting<int>("Config/DungeonMaps/MapWindowY", "Saved map window Y position (top is 0)", 200);
                    ShowVisitedTiles = new Setting<bool>("Config/DungeonMaps/ShowVisitedTiles", "Show visited tiles", true);
                }
                catch (Exception e) { Logger.LogException(e); }
            }

            public void Dispose() {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing) {
                if (!disposed) {
                    if (disposing) {
                        if (Enabled != null) Enabled.Dispose();
                        if (DrawWhenClosed != null) DrawWhenClosed.Dispose();
                        if (Opacity != null) Opacity.Dispose();
                        if (Debug != null) Debug.Dispose();
                        if (MapWindowX != null) MapWindowX.Dispose();
                        if (MapWindowY != null) MapWindowY.Dispose();
                        if (ShowVisitedTiles != null) ShowVisitedTiles.Dispose();
                    }
                    disposed = true;
                }
            }
        }

        public class VisualNavConfig : IDisposable {
            public Setting<float> LineOffset;

            public Setting<int> LineColor;
            public Setting<int> ChatTextColor;
            public Setting<int> JumpTextColor;
            public Setting<int> JumpArrowColor;
            public Setting<int> OpenVendorColor;
            public Setting<int> PauseColor;
            public Setting<int> PortalColor;
            public Setting<int> RecallColor;
            public Setting<int> UseNPCColor;

            public Setting<bool> ShowLine;
            public Setting<bool> ShowChatText;
            public Setting<bool> ShowJumpText;
            public Setting<bool> ShowJumpArrow;
            public Setting<bool> ShowOpenVendor;
            public Setting<bool> ShowPause;
            public Setting<bool> ShowPortal;
            public Setting<bool> ShowRecall;
            public Setting<bool> ShowUseNPC;

            private bool disposed = false;

            public List<string> Settings = new List<string>() {
                "Line",
                "ChatText",
                "JumpText",
                "JumpArrow",
                "OpenVendor",
                "Pause",
                "Portal",
                "Recall",
                "UseNPC"
            };

            public VisualNavConfig() {
                try {
                    LineOffset = new Setting<float>("Config/VisualNav/LineOffset", "Point to point navigation line z offset", 0.05f);

                    LineColor = new Setting<int>("Config/VisualNav/LineColor", "Point to point navigation line color", Color.Fuchsia.ToArgb());
                    ChatTextColor = new Setting<int>("Config/VisualNav/ChatTextColor", "Chat waypoint text color", Color.White.ToArgb());
                    JumpTextColor = new Setting<int>("Config/VisualNav/JumpTextColor", "Jump waypoint text color", Color.White.ToArgb());
                    JumpArrowColor = new Setting<int>("Config/VisualNav/JumpArrowColor", "Jump waypoint arrow color", Color.Yellow.ToArgb());
                    OpenVendorColor = new Setting<int>("Config/VisualNav/OpenVendorColor", "OpenVendor waypoint text color", Color.White.ToArgb());
                    PauseColor = new Setting<int>("Config/VisualNav/PauseColor", "Pause waypoint text color", Color.White.ToArgb());
                    PortalColor = new Setting<int>("Config/VisualNav/PortalColor", "Portal waypoint text color", Color.White.ToArgb());
                    RecallColor = new Setting<int>("Config/VisualNav/RecallColor", "Recall waypoint text color", Color.White.ToArgb());
                    UseNPCColor = new Setting<int>("Config/VisualNav/UseNPCColor", "UseNPC waypoint text color", Color.White.ToArgb());

                    ShowLine = new Setting<bool>("Config/VisualNav/ShowLine", "Show navigation lines", true);
                    ShowChatText = new Setting<bool>("Config/VisualNav/ShowChatText", "Show Chat waypoint text", true);
                    ShowJumpText = new Setting<bool>("Config/VisualNav/ShowJumpText", "Show Jump waypoint text", true);
                    ShowJumpArrow = new Setting<bool>("Config/VisualNav/ShowJumpArrow", "Show Jump waypoint arrow", true);
                    ShowOpenVendor = new Setting<bool>("Config/VisualNav/ShowOpenVendor", "Show OpenVendor waypoint text", true);
                    ShowPause = new Setting<bool>("Config/VisualNav/ShowPause", "Show Pause waypoint text", true);
                    ShowPortal = new Setting<bool>("Config/VisualNav/ShowPortal", "Show Portal waypoint text", true);
                    ShowRecall = new Setting<bool>("Config/VisualNav/ShowRecall", "Show Recall waypoint text", true);
                    ShowUseNPC = new Setting<bool>("Config/VisualNav/ShowUseNPC", "Show UseNPC waypoint text", true);
                }
                catch (Exception e) { Logger.LogException(e); }
            }

            public void Dispose() {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing) {
                if (!disposed) {
                    if (disposing) {
                        if (LineOffset != null) LineOffset.Dispose();

                        if (LineColor != null) LineColor.Dispose();
                        if (ChatTextColor != null) LineOffset.Dispose();
                        if (JumpTextColor != null) LineOffset.Dispose();
                        if (JumpArrowColor != null) LineOffset.Dispose();
                        if (OpenVendorColor != null) LineOffset.Dispose();
                        if (PauseColor != null) LineOffset.Dispose();
                        if (PortalColor != null) LineOffset.Dispose();
                        if (RecallColor != null) LineOffset.Dispose();
                        if (UseNPCColor != null) LineOffset.Dispose();

                        if (ShowLine != null) LineOffset.Dispose();
                        if (ShowChatText != null) LineOffset.Dispose();
                        if (ShowJumpText != null) LineOffset.Dispose();
                        if (ShowJumpArrow != null) LineOffset.Dispose();
                        if (ShowOpenVendor != null) LineOffset.Dispose();
                        if (ShowPause != null) LineOffset.Dispose();
                        if (ShowPortal != null) LineOffset.Dispose();
                        if (ShowRecall != null) LineOffset.Dispose();
                        if (ShowUseNPC != null) LineOffset.Dispose();
                    }
                    disposed = true;
                }
            }
        }

        public Config() {
            AutoSalvage = new AutoSalvageConfig();
            AutoVendor = new AutoVendorConfig();
            DungeonMaps = new DungeonMapsConfig();
            InventoryManager = new InventoryManagerConfig();
            VisualNav = new VisualNavConfig();
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    if (AutoSalvage != null) AutoSalvage.Dispose();
                    if (AutoVendor != null) AutoVendor.Dispose();
                    if (InventoryManager != null) InventoryManager.Dispose();
                    if (VisualNav != null) VisualNav.Dispose();
                }
                disposed = true;
            }
        }
    }
}
