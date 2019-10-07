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
}
