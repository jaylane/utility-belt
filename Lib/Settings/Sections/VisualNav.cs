using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Settings.Sections {
    [Section("VisualNav")]
    public class VisualNav : SectionBase {
        [Summary("Line offset from the ground, in meters")]
        [DefaultValue(0.05f)]
        public float LineOffset {
            get { return (float)GetSetting("LineOffset"); }
            set { UpdateSetting("LineOffset", value); }
        }

        [Summary("Automatically save [None] routes. Enabling this allows embedded routes to be drawn.")]
        [DefaultValue(false)]
        public bool SaveNoneRoutes {
            get { return (bool)GetSetting("SaveNoneRoutes"); }
            set { UpdateSetting("SaveNoneRoutes", value); }
        }

        [Summary("VisualNav display options")]
        public VisualNavDisplayOptions Display { get; set; } = null;

        public VisualNav(SectionBase parent) : base(parent) {
            Name = "VisualNav";
            Display = new VisualNavDisplayOptions(this);
        }
    }

    [Section("VisualNav display options")]
    public class VisualNavDisplayOptions : DisplaySectionBase {
        [JsonIgnore]
        public List<string> ValidSettings = new List<string>() {
                "Lines",
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

        [Summary("Point to point lines")]
        [DefaultEnabled(true)]
        [DefaultColor(-65281)]
        public ColorToggleOption Lines {
            get { return (ColorToggleOption)GetSetting("Lines"); }
            private set { UpdateSetting("Lines", value); }
        }

        [Summary("Chat commands")]
        [DefaultEnabled(true)]
        [DefaultColor(-1)]
        public ColorToggleOption ChatText {
            get { return (ColorToggleOption)GetSetting("ChatText"); }
            private set { UpdateSetting("ChatText", value); }
        }

        [Summary("Jump commands")]
        [DefaultEnabled(true)]
        [DefaultColor(-1)]
        public ColorToggleOption JumpText {
            get { return (ColorToggleOption)GetSetting("JumpText"); }
            private set { UpdateSetting("JumpText", value); }
        }

        [Summary("Jump heading arrow")]
        [DefaultEnabled(true)]
        [DefaultColor(-256)]
        public ColorToggleOption JumpArrow {
            get { return (ColorToggleOption)GetSetting("JumpArrow"); }
            private set { UpdateSetting("JumpArrow", value); }
        }

        [Summary("Open vendor")]
        [DefaultEnabled(true)]
        [DefaultColor(-1)]
        public ColorToggleOption OpenVendor {
            get { return (ColorToggleOption)GetSetting("OpenVendor"); }
            private set { UpdateSetting("OpenVendor", value); }
        }

        [Summary("Pause commands")]
        [DefaultEnabled(true)]
        [DefaultColor(-1)]
        public ColorToggleOption Pause {
            get { return (ColorToggleOption)GetSetting("Pause"); }
            private set { UpdateSetting("Pause", value); }
        }

        [Summary("Portal commands")]
        [DefaultEnabled(true)]
        [DefaultColor(-1)]
        public ColorToggleOption Portal {
            get { return (ColorToggleOption)GetSetting("Portal"); }
            private set { UpdateSetting("Portal", value); }
        }

        [Summary("Recall spells")]
        [DefaultEnabled(true)]
        [DefaultColor(-1)]
        public ColorToggleOption Recall {
            get { return (ColorToggleOption)GetSetting("Recall"); }
            private set { UpdateSetting("Recall", value); }
        }

        [Summary("Use NPC commands")]
        [DefaultEnabled(true)]
        [DefaultColor(-1)]
        public ColorToggleOption UseNPC {
            get { return (ColorToggleOption)GetSetting("UseNPC"); }
            private set { UpdateSetting("UseNPC", value); }
        }

        [Summary("Follow character arrow")]
        [DefaultEnabled(true)]
        [DefaultColor(-23296)]
        public ColorToggleOption FollowArrow {
            get { return (ColorToggleOption)GetSetting("FollowArrow"); }
            private set { UpdateSetting("FollowArrow", value); }
        }

        public VisualNavDisplayOptions(SectionBase parent) : base(parent) {
            Name = "Display";
        }
    }
}
