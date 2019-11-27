using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib {
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class)]
    class NameAttribute : Attribute {
        public string Name { get; }

        public NameAttribute(string name) {
            Name = name;
        }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Method)]
    class SummaryAttribute : Attribute {
        public string Summary { get; }

        public SummaryAttribute(string summary) {
            Summary = summary;
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    class SectionAttribute : Attribute {
        public string Section { get; }

        public SectionAttribute(string section) {
            Section = section;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    class DefaultColorAttribute : Attribute {
        public int Color { get; }

        public DefaultColorAttribute(int color) {
            Color = color;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    class DefaultEnabledAttribute : Attribute {
        public bool Enabled { get; }

        public DefaultEnabledAttribute(bool enabled) {
            Enabled = enabled;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    class DefaultUseIconAttribute : Attribute {
        public bool UseIcon { get; }

        public DefaultUseIconAttribute(bool useIcon) {
            UseIcon = useIcon;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    class DefaultSizeAttribute : Attribute {
        public int Size { get; }

        public DefaultSizeAttribute(int size) {
            Size = size;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    class DefaultShowLabelAttribute : Attribute {
        public bool Enabled { get; }

        public DefaultShowLabelAttribute(bool enabled) {
            Enabled = enabled;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    class UsageAttribute : Attribute {
        public string Usage { get; }

        public UsageAttribute(string usage) {
            Usage = usage;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    class ExampleAttribute : Attribute {
        public string Command { get; }
        public string Description { get; }

        public ExampleAttribute(string command, string description) {
            Command = command;
            Description = description;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    class CommandPatternAttribute : Attribute {
        public string Verb { get; }
        public bool AllowPartialVerbMatch { get; }
        public string ArgumentsPattern { get; }

        public CommandPatternAttribute(string command, string argumentsRegexPattern, bool allowPartialVerbMatch=false) {
            ArgumentsPattern = argumentsRegexPattern;
            Verb = command;
            AllowPartialVerbMatch = allowPartialVerbMatch;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    class SupportsFlagsAttribute : Attribute {
    }
}
