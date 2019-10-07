using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Settings.Sections {
    [Section("DungeonMaps")]
    public class DungeonMaps : SectionBase {
        [JsonIgnore]
        public const int MIN_ZOOM = 0;

        [JsonIgnore]
        public const int MAX_ZOOM = 16;

        [Summary("Enabled")]
        [DefaultValue(false)]
        public bool Enabled {
            get { return (bool)GetSetting("Enabled"); }
            set { UpdateSetting("Enabled", value); }
        }

        [Summary("Draw dungeon maps even when map window is closed")]
        [DefaultValue(true)]
        public bool DrawWhenClosed {
            get { return (bool)GetSetting("DrawWhenClosed"); }
            set { UpdateSetting("DrawWhenClosed", value); }
        }

        [Summary("Show visited tiles")]
        [DefaultValue(true)]
        public bool ShowVisitedTiles {
            get { return (bool)GetSetting("ShowVisitedTiles"); }
            set { UpdateSetting("ShowVisitedTiles", value); }
        }

        [Summary("Show compass")]
        [DefaultValue(true)]
        public bool ShowCompass {
            get { return (bool)GetSetting("ShowCompass"); }
            set { UpdateSetting("ShowCompass", value); }
        }

        [Summary("Map opacity")]
        [DefaultValue(16)]
        public int Opacity {
            get { return (int)GetSetting("Opacity"); }
            set { UpdateSetting("Opacity", value); }
        }

        [Summary("Map Window X")]
        [DefaultValue(40)]
        public int MapWindowX {
            get { return (int)GetSetting("MapWindowX"); }
            set { UpdateSetting("MapWindowX", value); }
        }

        [Summary("Map Window Y")]
        [DefaultValue(150)]
        public int MapWindowY {
            get { return (int)GetSetting("MapWindowY"); }
            set { UpdateSetting("MapWindowY", value); }
        }

        [Summary("Map Window width")]
        [DefaultValue(300)]
        public int MapWindowWidth {
            get { return (int)GetSetting("MapWindowWidth"); }
            set { UpdateSetting("MapWindowWidth", value); }
        }

        [Summary("Map Window height")]
        [DefaultValue(280)]
        public int MapWindowHeight {
            get { return (int)GetSetting("MapWindowHeight"); }
            set { UpdateSetting("MapWindowHeight", value); }
        }

        [Summary("Map zoom level")]
        [DefaultValue(4.20f)]
        public float MapZoom {
            get { return (float)GetSetting("MapZoom"); }
            set { UpdateSetting("MapZoom", value); }
        }

        [Summary("Map display options")]
        public MapDisplayOptions Display { get; set; } = null;

        public DungeonMaps(SectionBase parent) : base(parent) {
            Name = "DungeonMaps";
            Display = new MapDisplayOptions(this);
            //Display.PropertyChanged += (s, e) => {
                //OnPropertyChanged("Display");
            //};
        }
    }

    [Section("DungeonMaps display options")]
    public class MapDisplayOptions : DisplaySectionBase {
        [JsonIgnore]
        public List<string> TileOptions = new List<string>() {
                "Walls",
                "InnerWalls",
                "RampedWalls",
                "Stairs",
                "Floors"
            };

        [JsonIgnore]
        public List<string> ValidSettings = new List<string>() {
                "Walls",
                "InnerWalls",
                "RampedWalls",
                "Stairs",
                "Floors",
                "Portals",
                "PortalLabels",
                "Player",
                "PlayerLabel",
                //"OtherPlayers",
                //"OtherPlayersLabel",
                "VisualNavLines",
                "VisualNavStickyPoint"
            };

        [Summary("Walls")]
        [DefaultEnabled(true)]
        [DefaultColor(-16777089)]
        public ColorToggleOption Walls {
            get { return (ColorToggleOption)GetSetting("Walls"); }
            private set { UpdateSetting("Walls", value); }
        }

        [Summary("Inner wall")]
        [DefaultEnabled(true)]
        [DefaultColor(-8404993)]
        public ColorToggleOption InnerWalls {
            get { return (ColorToggleOption)GetSetting("InnerWalls"); }
            private set { UpdateSetting("InnerWalls", value); }
        }
        
        [Summary("Ramped wall")]
        [DefaultEnabled(true)]
        [DefaultColor(-11622657)]
        public ColorToggleOption RampedWalls {
            get { return (ColorToggleOption)GetSetting("RampedWalls"); }
            private set { UpdateSetting("RampedWalls", value); }
        }
        
        [Summary("Stairs")]
        [DefaultEnabled(true)]
        [DefaultColor(-16760961)]
        public ColorToggleOption Stairs {
            get { return (ColorToggleOption)GetSetting("Stairs"); }
            private set { UpdateSetting("Stairs", value); }
        }
        
        [Summary("Floor")]
        [DefaultEnabled(true)]
        [DefaultColor(-16744513)]
        public ColorToggleOption Floors {
            get { return (ColorToggleOption)GetSetting("Floors"); }
            private set { UpdateSetting("Floors", value); }
        }
        
        [Summary("Portals")]
        [DefaultEnabled(true)]
        [DefaultColor(-8388480)]
        public ColorToggleOption Portals {
            get { return (ColorToggleOption)GetSetting("Portals"); }
            private set { UpdateSetting("Portals", value); }
        }
        
        [Summary("Portal text labels")]
        [DefaultEnabled(true)]
        [DefaultColor(-18751)]
        public ColorToggleOption PortalLabels {
            get { return (ColorToggleOption)GetSetting("PortalLabels"); }
            private set { UpdateSetting("PortalLabels", value); }
        }
        
        [Summary("Player (you) indicator")]
        [DefaultEnabled(true)]
        [DefaultColor(-65536)]
        public ColorToggleOption Player {
            get { return (ColorToggleOption)GetSetting("Player"); }
            private set { UpdateSetting("Player", value); }
        }
        
        [Summary("Player (you) text label")]
        [DefaultEnabled(true)]
        [DefaultColor(-256)]
        public ColorToggleOption PlayerLabel {
            get { return (ColorToggleOption)GetSetting("PlayerLabel"); }
            private set { UpdateSetting("PlayerLabel", value); }
        }

        [Summary("Other players")]
        [DefaultEnabled(true)]
        [DefaultColor(-1)]
        public ColorToggleOption OtherPlayers {
            get { return (ColorToggleOption)GetSetting("OtherPlayers"); }
            private set { UpdateSetting("OtherPlayers", value); }
        }

        [Summary("Other player text labels")]
        [DefaultEnabled(true)]
        [DefaultColor(-1)]
        public ColorToggleOption OtherPlayerLabels {
            get { return (ColorToggleOption)GetSetting("OtherPlayerLabels"); }
            private set { UpdateSetting("OtherPlayerLabels", value); }
        }

        [Summary("VisualNav sticky point")]
        [DefaultEnabled(true)]
        [DefaultColor(-5374161)]
        public ColorToggleOption VisualNavStickyPoint {
            get { return (ColorToggleOption)GetSetting("VisualNavStickyPoint"); }
            private set { UpdateSetting("VisualNavStickyPoint", value); }
        }

        [Summary("VisualNav lines")]
        [DefaultEnabled(true)]
        [DefaultColor(-65281)]
        public ColorToggleOption VisualNavLines {
            get { return (ColorToggleOption)GetSetting("VisualNavLines"); }
            private set { UpdateSetting("VisualNavLines", value); }
        }

        public MapDisplayOptions(SectionBase parent) : base(parent) {
            Name = "Display";
        }
    }
}
