using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Settings {
    [AttributeUsage(AttributeTargets.Property)]
    class SummaryAttribute : Attribute {
        public string Text { get; set; }

        public SummaryAttribute(string text) {
            Text = text;
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    class SectionAttribute : Attribute {
        public string Text { get; set; }

        public SectionAttribute(string text) {
            Text = text;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    class DefaultColorAttribute : Attribute {
        public int Color { get; set; }

        public DefaultColorAttribute(int color) {
            Color = color;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    class DefaultEnabledAttribute : Attribute {
        public bool Enabled { get; set; }

        public DefaultEnabledAttribute(bool enabled) {
            Enabled = enabled;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    class DefaultUseIconAttribute : Attribute {
        public bool UseIcon { get; set; }

        public DefaultUseIconAttribute(bool useIcon) {
            UseIcon = useIcon;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    class DefaultSizeAttribute : Attribute {
        public int Size { get; set; }

        public DefaultSizeAttribute(int size) {
            Size = size;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    class DefaultShowLabelAttribute : Attribute {
        public bool Enabled { get; set; }

        public DefaultShowLabelAttribute(bool enabled) {
            Enabled = enabled;
        }
    }
}
