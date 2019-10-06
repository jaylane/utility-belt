using Decal.Adapter;
using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Mag.Shared.Settings;
using System.Drawing;

//
// this is a total shitshow and needs to be rewritten to support more complex types
//

namespace UtilityBelt {
    public class Config : IDisposable {
        private bool disposed;

        public AutoSalvageConfig AutoSalvage;
        public AutoVendorConfig AutoVendor;
        public DungeonMapsConfig DungeonMaps;
        public InventoryManagerConfig InventoryManager;
        public VisualNavConfig VisualNav;
        public MainConfig Main;

        public static bool ShouldSave = true;

        public void EnableSaving() {
            ShouldSave = true;
        }

        public void DisableSaving() {
            ShouldSave = false;
        }

        public class MainConfig : IDisposable {
            public Setting<int> WindowPositionX;
            public Setting<int> WindowPositionY;
            
            private bool disposed = false;

            public MainConfig() {
                try {
                    WindowPositionX = new Setting<int>("Config/Main/WindowPositionX", "Main UB Window X position for this character (left is 0)", 100);
                    WindowPositionY = new Setting<int>("Config/Main/WindowPositionY", "Main UB Window Y position for this character (top is 0)", 150);
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
                        if (WindowPositionX != null) WindowPositionX.Dispose();
                        if (WindowPositionY != null) WindowPositionY.Dispose();
                    }
                    disposed = true;
                }
            }
        }

        public class InventoryManagerConfig : IDisposable {
            public Setting<bool> AutoCram;
            public Setting<bool> AutoStack;
            public Setting<bool> Debug;

            private bool disposed = false;

            public InventoryManagerConfig() {
                try {
                    AutoCram = new Setting<bool>("Config/InventoryManager/AutoCram", "Automatically cram items into side packs", false);
                    AutoStack = new Setting<bool>("Config/InventoryManager/AutoStack", "Automatically combine stacked items", true);
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
            public Setting<bool> OnlyFromMainPack;

            private bool disposed = false;

            public AutoSalvageConfig() {
                try {
                    Think = new Setting<bool>("Config/AutoSalvage/Think", "Think to yourself when finished", true);
                    Debug = new Setting<bool>("Config/AutoSalvage/Debug", "Show debug messages", false);
                    OnlyFromMainPack = new Setting<bool>("Config/AutoSalvage/OnlyFromMainPack", "Only salvage things in your main pack", false);
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
                        if (OnlyFromMainPack != null) OnlyFromMainPack.Dispose();
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
            public Setting<bool> OnlyFromMainPack;

            private bool disposed = false;

            public AutoVendorConfig() {
                try {
                    Enabled = new Setting<bool>("Config/AutoVendor/Enabled", "Enable AutoVendor", false);
                    TestMode = new Setting<bool>("Config/AutoVendor/TestMode", "Enable TestMode", false);
                    ShowMerchantInfo = new Setting<bool>("Config/AutoVendor/ShowMerchantInfo", "Show merchant info on approach", true);
                    Think = new Setting<bool>("Config/AutoVendor/Think", "Think to yourself when finished", true);
                    Debug = new Setting<bool>("Config/AutoVendor/Debug", "Show debug messages", false);
                    Speed = new Setting<int>("Config/AutoVendor/Speed", "Delay between autovendor actions", 500);
                    OnlyFromMainPack = new Setting<bool>("Config/AutoVendor/OnlyFromMainPack", "Only sell things in your main pack", false);
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
                        if (OnlyFromMainPack != null) OnlyFromMainPack.Dispose();
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
            public Setting<int> MapWindowWidth;
            public Setting<int> MapWindowHeight;
            public Setting<bool> ShowVisitedTiles;
            public Setting<float> MapZoom;
            public Setting<bool> ShowCompass;

            public Setting<int> WallColor;
            public Setting<int> InnerWallColor;
            public Setting<int> RampedWallColor;
            public Setting<int> StairsColor;
            public Setting<int> FloorColor;
            public Setting<int> PortalsColor;
            public Setting<int> PortalsLabelColor;
            public Setting<int> PlayerColor;
            public Setting<int> PlayerLabelColor;
            public Setting<int> OtherPlayersColor;
            public Setting<int> OtherPlayersLabelColor;
            public Setting<int> VisualNavStickyPointColor;
            public Setting<int> VisualNavLineColor;

            public Setting<bool> ShowWall;
            public Setting<bool> ShowInnerWall;
            public Setting<bool> ShowRampedWall;
            public Setting<bool> ShowStairs;
            public Setting<bool> ShowFloor;
            public Setting<bool> ShowPortals;
            public Setting<bool> ShowPortalsLabel;
            public Setting<bool> ShowPlayer;
            public Setting<bool> ShowPlayerLabel;
            public Setting<bool> ShowOtherPlayers;
            public Setting<bool> ShowOtherPlayersLabel;
            public Setting<bool> ShowVisualNavStickyPoint;
            public Setting<bool> ShowVisualNavLine;

            public const int MIN_ZOOM = 0;
            public const int MAX_ZOOM = 16;

            public List<string> TileSettings = new List<string>() {
                "Wall",
                "InnerWall",
                "RampedWall",
                "Stairs",
                "Floor"
            };

            public List<string> Settings = new List<string>() {
                "Wall",
                "InnerWall",
                "RampedWall",
                "Stairs",
                "Floor",
                "Portals",
                "PortalsLabel",
                "Player",
                "PlayerLabel",
                //"OtherPlayers",
                //"OtherPlayersLabel",
                "VisualNavLine",
                "VisualNavStickyPoint"
            };

            private bool disposed = false;

            public DungeonMapsConfig() {
                try {
                    Enabled = new Setting<bool>("Config/DungeonMaps/Enabled", "Enable Dungeon Maps", false);
                    DrawWhenClosed = new Setting<bool>("Config/DungeonMaps/DrawWhenClosed", "Draw maps even when the decal window is closed", true);
                    Debug = new Setting<bool>("Config/DungeonMaps/Debug", "Show debug messages", false);
                    Opacity = new Setting<int>("Config/DungeonMaps/Opacity", "Overall map opacity (0-20, 20 is opaque)", 15);
                    MapWindowX = new Setting<int>("Config/DungeonMaps/MapWindowX", "Map window X position (left is 0)", 40);
                    MapWindowY = new Setting<int>("Config/DungeonMaps/MapWindowY", "Map window Y position (top is 0)", 150);
                    MapWindowWidth = new Setting<int>("Config/DungeonMaps/MapWindowWidth", "Map window width", 300);
                    MapWindowHeight = new Setting<int>("Config/DungeonMaps/MapWindowHeight", "Map window height", 280);
                    ShowVisitedTiles = new Setting<bool>("Config/DungeonMaps/ShowVisitedTiles", "Show visited tiles", true);
                    MapZoom = new Setting<float>("Config/DungeonMaps/MapZoom", "Map zoom amount", 8.4F - Map(12, MIN_ZOOM, MAX_ZOOM, 0.4F, 8));
                    ShowCompass = new Setting<bool>("Config/DungeonMaps/ShowCompass", "Show Compass icon on dungeon maps", true);

                    WallColor = new Setting<int>("Config/DungeonMaps/WallColor", "Outer wall color", Color.FromArgb(0, 0, 127).ToArgb());
                    InnerWallColor = new Setting<int>("Config/DungeonMaps/InnerWallColor", "Wall inside color", Color.FromArgb(127, 191, 255).ToArgb());
                    RampedWallColor = new Setting<int>("Config/DungeonMaps/RampedWallColor", "Ramped wall color", Color.FromArgb(78, 166, 255).ToArgb());
                    StairsColor = new Setting<int>("Config/DungeonMaps/StairsColor", "Stairs color", Color.FromArgb(0, 63, 127).ToArgb());
                    FloorColor = new Setting<int>("Config/DungeonMaps/FloorColor", "Floor color", Color.FromArgb(0, 127, 191).ToArgb());
                    PortalsColor = new Setting<int>("Config/DungeonMaps/PortalsColor", "Portal dots color", Color.Purple.ToArgb());
                    PortalsLabelColor = new Setting<int>("Config/DungeonMaps/PortalsLabelColor", "Portal text color", Color.LightPink.ToArgb());
                    PlayerColor = new Setting<int>("Config/DungeonMaps/PlayerColor", "Player color (you)", Color.Red.ToArgb());
                    PlayerLabelColor = new Setting<int>("Config/DungeonMaps/PlayerLabelColor", "Player label color (your name)", Color.Red.ToArgb());
                    OtherPlayersColor = new Setting<int>("Config/DungeonMaps/OtherPlayersColor", "Other players color", Color.White.ToArgb());
                    OtherPlayersLabelColor = new Setting<int>("Config/DungeonMaps/OtherPlayersLabelColor", "Other players label color", Color.White.ToArgb());
                    VisualNavStickyPointColor = new Setting<int>("Config/DungeonMaps/VisualNavStickyPointColor", "visual nav stick point color (vtank nav routes)", Color.GreenYellow.ToArgb());
                    VisualNavLineColor = new Setting<int>("Config/DungeonMaps/VisualNavLineColor", "visual nav line color (vtank nav routes)", Color.Fuchsia.ToArgb());

                    ShowWall = new Setting<bool>("Config/DungeonMaps/ShowWall", "Outer wall color", true);
                    ShowInnerWall = new Setting<bool>("Config/DungeonMaps/ShowInnerWall", "Inner wall color", true);
                    ShowRampedWall = new Setting<bool>("Config/DungeonMaps/ShowRampedWall", "Ramped wall color", true);
                    ShowStairs = new Setting<bool>("Config/DungeonMaps/ShowStairs", "Stairs color", true);
                    ShowFloor = new Setting<bool>("Config/DungeonMaps/ShowFloor", "Outer wall color", true);
                    ShowPortals = new Setting<bool>("Config/DungeonMaps/ShowPortals", "Show portals", true);
                    ShowPortalsLabel = new Setting<bool>("Config/DungeonMaps/ShowPortalsLabel", "Show portal labels", true);
                    ShowPlayer = new Setting<bool>("Config/DungeonMaps/ShowPlayer", "Show player (you)", true);
                    ShowPlayerLabel = new Setting<bool>("Config/DungeonMaps/ShowPlayerLabel", "Show player label (your name)", true);
                    ShowOtherPlayers = new Setting<bool>("Config/DungeonMaps/ShowOtherPlayers", "Show other players", true);
                    ShowOtherPlayersLabel = new Setting<bool>("Config/DungeonMaps/ShowOtherPlayersLabel", "Show other players label", true);
                    ShowVisualNavStickyPoint = new Setting<bool>("Config/DungeonMaps/ShowVisualNavStickyPoint", "Show visual nav sticky point (vtank nav route with a single point)", true);
                    ShowVisualNavLine = new Setting<bool>("Config/DungeonMaps/ShowVisualNavLine", "Show visual nav lines (vtank nav route)", true);
                }
                catch (Exception e) { Logger.LogException(e); }
            }

            public float Map(float value, float fromSource, float toSource, float fromTarget, float toTarget) {
                return (value - fromSource) / (toSource - fromSource) * (toTarget - fromTarget) + fromTarget;
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
                        if (MapWindowWidth != null) MapWindowWidth.Dispose();
                        if (MapWindowHeight != null) MapWindowHeight.Dispose();
                        if (ShowVisitedTiles != null) ShowVisitedTiles.Dispose();
                        if (ShowCompass != null) ShowCompass.Dispose();
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
            public Setting<int> FollowArrowColor;

            public Setting<bool> ShowLine;
            public Setting<bool> ShowChatText;
            public Setting<bool> ShowJumpText;
            public Setting<bool> ShowJumpArrow;
            public Setting<bool> ShowOpenVendor;
            public Setting<bool> ShowPause;
            public Setting<bool> ShowPortal;
            public Setting<bool> ShowRecall;
            public Setting<bool> ShowUseNPC;
            public Setting<bool> ShowFollowArrow;

            public Setting<bool> SaveNoneRoutes;

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
                "UseNPC",
                "FollowArrow"
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
                    FollowArrowColor = new Setting<int>("Config/VisualNav/FollowArrowColor", "FollowArrow color", Color.Orange.ToArgb());

                    ShowLine = new Setting<bool>("Config/VisualNav/ShowLine", "Show navigation lines", true);
                    ShowChatText = new Setting<bool>("Config/VisualNav/ShowChatText", "Show Chat waypoint text", true);
                    ShowJumpText = new Setting<bool>("Config/VisualNav/ShowJumpText", "Show Jump waypoint text", true);
                    ShowJumpArrow = new Setting<bool>("Config/VisualNav/ShowJumpArrow", "Show Jump waypoint arrow", true);
                    ShowOpenVendor = new Setting<bool>("Config/VisualNav/ShowOpenVendor", "Show OpenVendor waypoint text", true);
                    ShowPause = new Setting<bool>("Config/VisualNav/ShowPause", "Show Pause waypoint text", true);
                    ShowPortal = new Setting<bool>("Config/VisualNav/ShowPortal", "Show Portal waypoint text", true);
                    ShowRecall = new Setting<bool>("Config/VisualNav/ShowRecall", "Show Recall waypoint text", true);
                    ShowUseNPC = new Setting<bool>("Config/VisualNav/ShowUseNPC", "Show UseNPC waypoint text", true);
                    ShowFollowArrow = new Setting<bool>("Config/VisualNav/ShowFollowArrow", "Show FollowArrow", true);

                    SaveNoneRoutes = new Setting<bool>("Config/VisualNav/SaveNoneRoutes", "Automatically save [None] routes. Enabling this allows embedded routes to be drawn.", false);
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
                        if (ChatTextColor != null) ChatTextColor.Dispose();
                        if (JumpTextColor != null) JumpTextColor.Dispose();
                        if (JumpArrowColor != null) JumpArrowColor.Dispose();
                        if (OpenVendorColor != null) OpenVendorColor.Dispose();
                        if (PauseColor != null) PauseColor.Dispose();
                        if (PortalColor != null) PortalColor.Dispose();
                        if (RecallColor != null) RecallColor.Dispose();
                        if (UseNPCColor != null) UseNPCColor.Dispose();

                        if (ShowLine != null) ShowLine.Dispose();
                        if (ShowChatText != null) ShowChatText.Dispose();
                        if (ShowJumpText != null) ShowJumpText.Dispose();
                        if (ShowJumpArrow != null) ShowJumpArrow.Dispose();
                        if (ShowOpenVendor != null) ShowOpenVendor.Dispose();
                        if (ShowPause != null) ShowPause.Dispose();
                        if (ShowPortal != null) ShowPortal.Dispose();
                        if (ShowRecall != null) ShowRecall.Dispose();
                        if (ShowUseNPC != null) ShowUseNPC.Dispose();

                        if (SaveNoneRoutes != null) SaveNoneRoutes.Dispose();
                    }
                    disposed = true;
                }
            }
        }

        public Config() {
            Main = new MainConfig();
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
                    if (Main != null) Main.Dispose();
                }
                disposed = true;
            }
        }
    }
}
