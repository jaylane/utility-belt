using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace UtilityBelt.Lib.Settings {
    [JsonObject(MemberSerialization.OptIn)]
    public class Settings {
        private string pluginStorageDirectory;
        public bool ShouldSave = false;

        public event EventHandler Changed;

        #region Public Properties
        public bool HasCharacterSettingsLoaded { get; set; } = false;

        // no JsonProperty because we dont want to store this with each character,
        // todo: make sure this gets loaded in properly with PopulateObject from settings.default.json
        //[JsonProperty]
        [SummaryAttribute("Path where plugin will store all of its data")]
        public string PluginStorageDirectory {
            get {
                // read from plugin.json if available
                if (!string.IsNullOrEmpty(pluginStorageDirectory)) {
                    return pluginStorageDirectory;
                }

                // default is documents\decal plugins\<plugin name>\
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                var decalDocumentsPath = Path.Combine(documentsPath, "Decal Plugins");

                return Path.Combine(decalDocumentsPath, Globals.PluginName);
            }
            private set {
                pluginStorageDirectory = value;
            }
        }

        // path to global plugin config
        public string DefaultCharacterSettingsFilePath {
            get {
                return Path.Combine(Util.GetAssemblyDirectory(), "settings.default.json");
            }
        }

        // current character's storage path
        public string CharacterStoragePath {
            get {
                var path = Path.Combine(PluginStorageDirectory, Globals.Core.CharacterFilter.Server);
                return Path.Combine(path, Globals.Core.CharacterFilter.Name);
            }
        }
        #endregion

        #region Settings Sections
        [JsonProperty]
        [Summary("Global plugin Settings")]
        public Sections.Plugin Plugin { get; set; }

        [JsonProperty]
        [Summary("AutoSalvage Settings")]
        public Sections.AutoSalvage AutoSalvage { get; set; }

        [JsonProperty]
        [Summary("AutoVendor Settings")]
        public Sections.AutoVendor AutoVendor { get; set; }

        [JsonProperty]
        [Summary("DungeonMaps Settings")]
        public Sections.DungeonMaps DungeonMaps { get; set; }

        [JsonProperty]
        [Summary("InventoryManager Settings")]
        public Sections.InventoryManager InventoryManager { get; set; }

        [JsonProperty]
        [Summary("VisualNav Settings")]
        public Sections.VisualNav VisualNav { get; set; }
        #endregion

        public Settings() {
            try {
                SetupSection(Plugin = new Sections.Plugin(null));
                SetupSection(AutoSalvage = new Sections.AutoSalvage(null));
                SetupSection(AutoVendor = new Sections.AutoVendor(null));
                SetupSection(DungeonMaps = new Sections.DungeonMaps(null));
                SetupSection(InventoryManager = new Sections.InventoryManager(null));
                SetupSection(VisualNav = new Sections.VisualNav(null));

                ShouldSave = false;
                Load();
                ShouldSave = true;

                Logger.Debug("Finished loading settings");
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public void EnableSaving() {
            ShouldSave = true;
        }

        public void DisableSaving() {
            ShouldSave = false;
        }

        // section setup / events
        private void SetupSection(SectionBase section) {
            section.PropertyChanged += HandleSectionChange;
        }

        #region Events / Handlers
        // notify any subcribers that this has changed
        protected void OnChanged() {
            Changed?.Invoke(this, new EventArgs());
        }

        // called when one of the child sections has been changed
        private void HandleSectionChange(object sender, EventArgs e) {
            OnChanged();
            Save();
        }
        #endregion

        #region Saving / Loading
        // load default plugin settings
        private void LoadDefaults() {
            try {
                if (File.Exists(DefaultCharacterSettingsFilePath)) {
                    JsonConvert.PopulateObject(File.ReadAllText(DefaultCharacterSettingsFilePath), this);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        // load character specific settings
        public void Load() {
            try {
                var path = Path.Combine(CharacterStoragePath, "settings.json");
                
                LoadDefaults();
                LoadOldXML();

                if (File.Exists(path)) {
                    JsonConvert.PopulateObject(File.ReadAllText(path), this);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
            finally {
                // even if it fails to load... this is just for making sure
                // not to try to do stuff until we have settings loaded
                HasCharacterSettingsLoaded = true;
            }
        }

        // save character specific settings
        public void Save() {
            try {
                if (!ShouldSave) return;

                var json = JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
                var path = Path.Combine(CharacterStoragePath, "settings.json");

                File.WriteAllText(path, json);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        #endregion

        #region Old Mag-Tools style config.xml migration
        // load old mag-tools style xml config, this is just for migrating
        // it will be deleted afterwards
        private void LoadOldXML() {
            try {
                var path = Path.Combine(CharacterStoragePath, "config.xml");

                if (!File.Exists(path)) return;

                XmlDocument doc = new XmlDocument();
                doc.Load(path);
                XmlNode config = doc.DocumentElement.SelectSingleNode("/UtilityBelt/Config");

                
                foreach (XmlNode node in config.ChildNodes) {
                    switch (node.Name) {
                        case "AutoSalvage":
                            AutoSalvage.Think = ParseOldNode(node, "Think", AutoSalvage.Think);
                            AutoSalvage.OnlyFromMainPack = ParseOldNode(node, "OnlyFromMainPack", AutoSalvage.OnlyFromMainPack);
                            break;

                        case "AutoVendor":
                            AutoVendor.Enabled = ParseOldNode(node, "Enabled", AutoVendor.Enabled);
                            AutoVendor.Think = ParseOldNode(node, "Think", AutoVendor.Think);
                            AutoVendor.TestMode = ParseOldNode(node, "TestMode", AutoVendor.TestMode);
                            AutoVendor.ShowMerchantInfo = ParseOldNode(node, "ShowMerchantInfo", AutoVendor.ShowMerchantInfo);
                            AutoVendor.OnlyFromMainPack = ParseOldNode(node, "OnlyFromMainPack", AutoVendor.OnlyFromMainPack);
                            AutoVendor.Speed = ParseOldNode(node, "Speed", AutoVendor.Speed);
                            break;

                        case "DungeonMaps":
                            DungeonMaps.Enabled = ParseOldNode(node, "Enabled", DungeonMaps.Enabled);
                            DungeonMaps.DrawWhenClosed = ParseOldNode(node, "DrawWhenClosed ", DungeonMaps.DrawWhenClosed);
                            DungeonMaps.ShowVisitedTiles = ParseOldNode(node, "ShowVisitedTiles", DungeonMaps.ShowVisitedTiles);
                            DungeonMaps.ShowCompass = ParseOldNode(node, "ShowCompass", DungeonMaps.ShowCompass);
                            DungeonMaps.MapWindowX = ParseOldNode(node, "MapWindowX", DungeonMaps.MapWindowX);
                            DungeonMaps.MapWindowY = ParseOldNode(node, "MapWindowY", DungeonMaps.MapWindowY);
                            DungeonMaps.MapWindowWidth = ParseOldNode(node, "MapWindowWidth", DungeonMaps.MapWindowWidth);
                            DungeonMaps.MapWindowHeight = ParseOldNode(node, "MapWindowHeight", DungeonMaps.MapWindowHeight);
                            DungeonMaps.Opacity = ParseOldNode(node, "Opacity", DungeonMaps.Opacity);
                            DungeonMaps.MapZoom = ParseOldNode(node, "MapZoom", DungeonMaps.MapZoom);
                            
                            DungeonMaps.Display.Walls.Color = ParseOldNode(node, "WallColor", DungeonMaps.Display.Walls.Color);
                            DungeonMaps.Display.InnerWalls.Color = ParseOldNode(node, "InnerWallColor", DungeonMaps.Display.InnerWalls.Color);
                            DungeonMaps.Display.RampedWalls.Color = ParseOldNode(node, "RampedWallColor", DungeonMaps.Display.RampedWalls.Color);
                            DungeonMaps.Display.Stairs.Color = ParseOldNode(node, "StairsColor", DungeonMaps.Display.Stairs.Color);
                            DungeonMaps.Display.Floors.Color = ParseOldNode(node, "FloorColor", DungeonMaps.Display.Floors.Color);
                            DungeonMaps.Display.Portals.Color = ParseOldNode(node, "PortalsColor", DungeonMaps.Display.Portals.Color);
                            DungeonMaps.Display.PortalLabels.Color = ParseOldNode(node, "PortalsLabelColor", DungeonMaps.Display.PortalLabels.Color);
                            DungeonMaps.Display.Player.Color = ParseOldNode(node, "PlayerColor", DungeonMaps.Display.Player.Color);
                            DungeonMaps.Display.PlayerLabel.Color = ParseOldNode(node, "PlayerLabelColor", DungeonMaps.Display.PlayerLabel.Color);
                            DungeonMaps.Display.OtherPlayers.Color = ParseOldNode(node, "OtherPlayersColor", DungeonMaps.Display.OtherPlayers.Color);
                            DungeonMaps.Display.OtherPlayerLabels.Color = ParseOldNode(node, "OtherPlayersLabelColor", DungeonMaps.Display.OtherPlayerLabels.Color);
                            DungeonMaps.Display.VisualNavStickyPoint.Color = ParseOldNode(node, "VisualNavStickyPointColor", DungeonMaps.Display.VisualNavStickyPoint.Color);
                            DungeonMaps.Display.VisualNavLines.Color = ParseOldNode(node, "VisualNavLineColor", DungeonMaps.Display.VisualNavLines.Color);

                            DungeonMaps.Display.Walls.Enabled = ParseOldNode(node, "ShowWall", DungeonMaps.Display.Walls.Enabled);
                            DungeonMaps.Display.InnerWalls.Enabled = ParseOldNode(node, "ShowInnerWall", DungeonMaps.Display.InnerWalls.Enabled);
                            DungeonMaps.Display.RampedWalls.Enabled = ParseOldNode(node, "ShowRampedWall", DungeonMaps.Display.RampedWalls.Enabled);
                            DungeonMaps.Display.Stairs.Enabled = ParseOldNode(node, "ShowStairs", DungeonMaps.Display.Stairs.Enabled);
                            DungeonMaps.Display.Floors.Enabled = ParseOldNode(node, "ShowFloor", DungeonMaps.Display.Floors.Enabled);
                            DungeonMaps.Display.Portals.Enabled = ParseOldNode(node, "ShowPortals", DungeonMaps.Display.Portals.Enabled);
                            DungeonMaps.Display.PortalLabels.Enabled = ParseOldNode(node, "ShowPortalsLabel", DungeonMaps.Display.PortalLabels.Enabled);
                            DungeonMaps.Display.Player.Enabled = ParseOldNode(node, "ShowPlayer", DungeonMaps.Display.Player.Enabled);
                            DungeonMaps.Display.PlayerLabel.Enabled = ParseOldNode(node, "ShowPlayerLabel", DungeonMaps.Display.PlayerLabel.Enabled);
                            DungeonMaps.Display.OtherPlayers.Enabled = ParseOldNode(node, "ShowOtherPlayers", DungeonMaps.Display.OtherPlayers.Enabled);
                            DungeonMaps.Display.OtherPlayerLabels.Enabled = ParseOldNode(node, "ShowOtherPlayersLabel", DungeonMaps.Display.OtherPlayerLabels.Enabled);
                            DungeonMaps.Display.VisualNavStickyPoint.Enabled = ParseOldNode(node, "ShowVisualNavStickyPoint", DungeonMaps.Display.VisualNavStickyPoint.Enabled);
                            DungeonMaps.Display.VisualNavLines.Enabled = ParseOldNode(node, "ShowVisualNavLine", DungeonMaps.Display.VisualNavLines.Enabled);
                            break;

                        case "InventoryManager":
                            InventoryManager.AutoCram = ParseOldNode(node, "AutoCram", InventoryManager.AutoCram);
                            InventoryManager.AutoStack = ParseOldNode(node, "AutoStack", InventoryManager.AutoStack);
                            break;
                            
                        case "VisualNav":
                            VisualNav.SaveNoneRoutes = ParseOldNode(node, "SaveNoneRoutes", VisualNav.SaveNoneRoutes);

                            VisualNav.Display.Lines.Color = ParseOldNode(node, "LineColor", VisualNav.Display.Lines.Color);
                            VisualNav.Display.ChatText.Color = ParseOldNode(node, "ChatTextColor", VisualNav.Display.ChatText.Color);
                            VisualNav.Display.JumpText.Color = ParseOldNode(node, "JumpTextColor", VisualNav.Display.JumpText.Color);
                            VisualNav.Display.JumpArrow.Color = ParseOldNode(node, "JumpArrowColor", VisualNav.Display.JumpArrow.Color);
                            VisualNav.Display.OpenVendor.Color = ParseOldNode(node, "OpenVendorColor", VisualNav.Display.OpenVendor.Color);
                            VisualNav.Display.Pause.Color = ParseOldNode(node, "PauseColor", VisualNav.Display.Pause.Color);
                            VisualNav.Display.Portal.Color = ParseOldNode(node, "PortalColor", VisualNav.Display.Portal.Color);
                            VisualNav.Display.Recall.Color = ParseOldNode(node, "RecallColor", VisualNav.Display.Recall.Color);
                            VisualNav.Display.UseNPC.Color = ParseOldNode(node, "UseNPCColor", VisualNav.Display.UseNPC.Color);
                            VisualNav.Display.FollowArrow.Color = ParseOldNode(node, "FollowArrowColor", VisualNav.Display.FollowArrow.Color);

                            VisualNav.Display.Lines.Enabled = ParseOldNode(node, "ShowLine", VisualNav.Display.Lines.Enabled);
                            VisualNav.Display.ChatText.Enabled = ParseOldNode(node, "ShowChatText", VisualNav.Display.ChatText.Enabled);
                            VisualNav.Display.JumpText.Enabled = ParseOldNode(node, "ShowJumpText", VisualNav.Display.JumpText.Enabled);
                            VisualNav.Display.JumpArrow.Enabled = ParseOldNode(node, "ShowJumpArrow", VisualNav.Display.JumpArrow.Enabled);
                            VisualNav.Display.OpenVendor.Enabled = ParseOldNode(node, "ShowOpenVendor", VisualNav.Display.OpenVendor.Enabled);
                            VisualNav.Display.Pause.Enabled = ParseOldNode(node, "ShowPause", VisualNav.Display.Pause.Enabled);
                            VisualNav.Display.Portal.Enabled = ParseOldNode(node, "ShowPortal", VisualNav.Display.Portal.Enabled);
                            VisualNav.Display.Recall.Enabled = ParseOldNode(node, "ShowRecall", VisualNav.Display.Recall.Enabled);
                            VisualNav.Display.UseNPC.Enabled = ParseOldNode(node, "ShowUseNPC", VisualNav.Display.UseNPC.Enabled);
                            VisualNav.Display.FollowArrow.Enabled = ParseOldNode(node, "ShowFollowArrow", VisualNav.Display.FollowArrow.Enabled);
                            break;
                            
                        case "Main":
                            Plugin.WindowPositionX = ParseOldNode(node, "WindowPositionX", Plugin.WindowPositionX);
                            Plugin.WindowPositionY = ParseOldNode(node, "WindowPositionY", Plugin.WindowPositionY);
                            break;
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private float ParseOldNode(XmlNode parentNode, string childTag, float defaultValue) {
            try {
                XmlNode node = parentNode.SelectSingleNode(childTag);
                if (node != null) {
                    float value = 0;

                    if (float.TryParse(node.InnerText, out value)) {
                        return value;
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return defaultValue;
        }

        private int ParseOldNode(XmlNode parentNode, string childTag, int defaultValue) {
            try {
                XmlNode node = parentNode.SelectSingleNode($"{childTag}");
                if (node != null) {
                    int value = 0;

                    if (int.TryParse(node.InnerText, out value)) {
                        return value;
                    }
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return defaultValue;
        }

        private bool ParseOldNode(XmlNode parentNode, string childTag, bool defaultValue) {
            try {
                XmlNode node = parentNode.SelectSingleNode($"{childTag}");
                if (node != null) {
                    return node.InnerText.ToLower().Trim() == "true";
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return defaultValue;
        }
        #endregion
    }
}
