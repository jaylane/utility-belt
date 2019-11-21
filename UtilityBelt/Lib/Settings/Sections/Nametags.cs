using System.ComponentModel;

namespace UtilityBelt.Lib.Settings.Sections {
    [Section("Nametags")]
    public class Nametags : DisplaySectionBase {
        [Summary("Enabled")]
        [DefaultValue(true)]
        public bool Enabled {
            get { return (bool)GetSetting("Enabled"); }
            set { UpdateSetting("Enabled", value); }
        }

        [Summary("Maximum Range for Nametags")]
        [DefaultValue(35f)]
        public float MaxRange {
            get { return (float)GetSetting("MaxRange"); }
            set { UpdateSetting("MaxRange", value); }
        }

        [Summary("Player Nametag")]
        [DefaultEnabled(true)]
        [DefaultColor(-16711681)]
        public ColorToggleOption Player {
            get { return (ColorToggleOption)GetSetting("Player"); }
            private set { UpdateSetting("Player", value); }
        }
        [Summary("Portal Nametag")]
        [DefaultEnabled(true)]
        [DefaultColor(-16711936)]
        public ColorToggleOption Portal {
            get { return (ColorToggleOption)GetSetting("Portal"); }
            private set { UpdateSetting("Portal", value); }
        }
        [Summary("Npc Nametag")]
        [DefaultEnabled(true)]
        [DefaultColor(-256)]
        public ColorToggleOption Npc {
            get { return (ColorToggleOption)GetSetting("Npc"); }
            private set { UpdateSetting("Npc", value); }
        }
        [Summary("Vendor Nametag")]
        [DefaultEnabled(true)]
        [DefaultColor(-65281)]
        public ColorToggleOption Vendor {
            get { return (ColorToggleOption)GetSetting("Vendor"); }
            private set { UpdateSetting("Vendor", value); }
        }
        [Summary("Monster Nametag")]
        [DefaultEnabled(true)]
        [DefaultColor(-65536)]
        public ColorToggleOption Monster {
            get { return (ColorToggleOption)GetSetting("Monster"); }
            private set { UpdateSetting("Monster", value); }
        }

        public Nametags(SectionBase parent) : base(parent) {
            Name = "Nametags";
        }
    }
}