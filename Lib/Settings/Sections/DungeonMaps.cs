using Decal.Adapter.Wrappers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using UtilityBelt.Lib.Constants;

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

        [Summary("Marker display options")]
        public MarkerDisplayOptions Markers { get; set; } = null;

        public MapDisplayOptions(SectionBase parent) : base(parent) {
            Name = "Display";
            Markers = new MarkerDisplayOptions(this);
            Markers.Name = "Markers";
        }
    }


    [Section("DungeonMaps marker display options")]
    public class MarkerDisplayOptions : DisplaySectionBase {
        [JsonIgnore]
        public List<string> ValidSettings = new List<string>() {
                "You",
                "Others",
                "Items",
                "Monsters",
                "NPCs",
                "Portals",
                "MyCorpse",
                "OtherCorpses",
                "Containers",
                "Doors",
                "EverythingElse"
            };

        [Summary("You")]
        [DefaultEnabled(true)]
        [DefaultColor(-65536)] // red
        [DefaultUseIcon(true)]
        [DefaultShowLabel(false)]
        [DefaultSize(3)]
        public MarkerToggleOption You {
            get { return (MarkerToggleOption)GetSetting("You"); }
            private set { UpdateSetting("You", value); }
        }

        [Summary("Others")]
        [DefaultEnabled(true)]
        [DefaultColor(-1)] // white
        [DefaultUseIcon(true)]
        [DefaultShowLabel(true)]
        [DefaultSize(3)]
        public MarkerToggleOption Others {
            get { return (MarkerToggleOption)GetSetting("Others"); }
            private set { UpdateSetting("Others", value); }
        }

        [Summary("Items")]
        [DefaultEnabled(true)]
        [DefaultColor(-1)] // white
        [DefaultUseIcon(true)]
        [DefaultShowLabel(true)]
        [DefaultSize(3)]
        public MarkerToggleOption Items {
            get { return (MarkerToggleOption)GetSetting("Items"); }
            private set { UpdateSetting("Items", value); }
        }

        [Summary("Monsters")]
        [DefaultEnabled(true)]
        [DefaultColor(-23296)] // orange
        [DefaultUseIcon(true)]
        [DefaultShowLabel(false)]
        [DefaultSize(3)]
        public MarkerToggleOption Monsters {
            get { return (MarkerToggleOption)GetSetting("Monsters"); }
            private set { UpdateSetting("Monsters", value); }
        }

        [Summary("NPCs")]
        [DefaultEnabled(true)]
        [DefaultColor(-256)] // yellow
        [DefaultUseIcon(true)]
        [DefaultShowLabel(false)]
        [DefaultSize(3)]
        public MarkerToggleOption NPCs {
            get { return (MarkerToggleOption)GetSetting("NPCs"); }
            private set { UpdateSetting("NPCs", value); }
        }

        [Summary("My Corpse")]
        [DefaultEnabled(true)]
        [DefaultColor(-65536)] // red 
        [DefaultUseIcon(true)]
        [DefaultShowLabel(true)]
        [DefaultSize(3)]
        public MarkerToggleOption MyCorpse {
            get { return (MarkerToggleOption)GetSetting("MyCorpse"); }
            private set { UpdateSetting("MyCorpse", value); }
        }

        [Summary("Other Corpses")]
        [DefaultEnabled(false)]
        [DefaultColor(-657931)] // white smoke
        [DefaultUseIcon(true)]
        [DefaultShowLabel(false)]
        [DefaultSize(3)]
        public MarkerToggleOption OtherCorpses {
            get { return (MarkerToggleOption)GetSetting("OtherCorpses"); }
            private set { UpdateSetting("OtherCorpses", value); }
        }

        [Summary("Portals")]
        [DefaultEnabled(true)]
        [DefaultColor(-3841)] // very light purple/pink (mostly white)
        [DefaultUseIcon(true)]
        [DefaultShowLabel(true)]
        [DefaultSize(3)]
        public MarkerToggleOption Portals {
            get { return (MarkerToggleOption)GetSetting("Portals"); }
            private set { UpdateSetting("Portals", value); }
        }

        [Summary("Containers")]
        [DefaultEnabled(true)]
        [DefaultColor(-744352)] // sandy brown
        [DefaultUseIcon(true)]
        [DefaultShowLabel(false)]
        [DefaultSize(3)]
        public MarkerToggleOption Containers {
            get { return (MarkerToggleOption)GetSetting("Containers"); }
            private set { UpdateSetting("Containers", value); }
        }

        [Summary("Doors")]
        [DefaultEnabled(true)]
        [DefaultColor(-5952982)] // brown
        [DefaultUseIcon(false)]
        [DefaultShowLabel(false)]
        [DefaultSize(3)]
        public MarkerToggleOption Doors {
            get { return (MarkerToggleOption)GetSetting("Doors"); }
            private set { UpdateSetting("Doors", value); }
        }

        [Summary("Everything Else")]
        [DefaultEnabled(false)]
        [DefaultColor(-657931)] // white smoke
        [DefaultUseIcon(true)]
        [DefaultShowLabel(false)]
        [DefaultSize(3)]
        public MarkerToggleOption EverythingElse {
            get { return (MarkerToggleOption)GetSetting("EverythingElse"); }
            private set { UpdateSetting("EverythingElse", value); }
        }

        public MarkerDisplayOptions(SectionBase parent) : base(parent) {
            Name = "Markers";
        }

        public int GetMarkerColor(WorldObject wo) {
            var propName = GetMarkerNameFromWO(wo);
            var prop = (MarkerToggleOption)(this.GetPropValue(propName));

            return prop == null ? Color.White.ToArgb() : prop.Color;
        }

        public bool ShouldShowlabel(WorldObject wo) {
            var propName = GetMarkerNameFromWO(wo);
            var prop = (MarkerToggleOption)(this.GetPropValue(propName));

            return prop == null ? false : prop.ShowLabel;
        }

        internal bool ShouldDraw(WorldObject wo) {
            var propName = GetMarkerNameFromWO(wo);
            var prop = (MarkerToggleOption)(this.GetPropValue(propName));

            return prop == null ? false : prop.Enabled;
        }

        internal int GetLabelColor(WorldObject wo) {
            var propName = GetMarkerNameFromWO(wo);
            var prop = (MarkerToggleOption)(this.GetPropValue(propName));

            return prop == null ? Color.White.ToArgb() : prop.Color;
        }

        internal bool ShouldUseIcon(WorldObject wo) {
            var propName = GetMarkerNameFromWO(wo);
            var prop = (MarkerToggleOption)(this.GetPropValue(propName));

            return prop == null ? true : prop.UseIcon;
        }

        internal int GetSize(WorldObject wo) {
            var propName = GetMarkerNameFromWO(wo);
            var prop = (MarkerToggleOption)(this.GetPropValue(propName));

            return prop == null ? 4 : prop.Size;
        }

        public string GetMarkerNameFromWO(WorldObject wo) {
            // check marker display settings
            switch (wo.ObjectClass) {
                case ObjectClass.Player:
                    if (wo.Id == Globals.Core.CharacterFilter.Id) {
                        return "You";
                    }
                    else {
                        return "Others";
                    }

                case ObjectClass.Monster:
                    return "Monsters";

                case ObjectClass.Npc:
                case ObjectClass.Vendor:
                    return "NPCs";

                case ObjectClass.Portal:
                    return "Portals";

                case ObjectClass.Corpse:
                    if (wo.Name == $"Corpse of {Globals.Core.CharacterFilter.Name}") {
                        return "MyCorpse";
                    }
                    else {
                        return "OtherCorpses";
                    }

                case ObjectClass.Door:
                    return "Doors";

                case ObjectClass.Container:
                    return "Containers";

                default:
                    // draw anything not "stuck" as an item
                    if ((wo.Values(LongValueKey.Behavior, 0) & (int)BehaviorFlag.Stuck) == 0) {
                        return "Items";
                    }

                    return "EverythingElse";
            }
        }
    }
}
