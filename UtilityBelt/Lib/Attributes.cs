using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib {
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class)]
    public class NameAttribute : Attribute {
        public string Name { get; }

        public NameAttribute(string name) {
            Name = name;
        }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Field)]
    public class SummaryAttribute : Attribute {
        public string Summary { get; }

        public SummaryAttribute(string summary) {
            Summary = summary;
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class SectionAttribute : Attribute {
        public string Section { get; }

        public SectionAttribute(string section) {
            Section = section;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class DefaultColorAttribute : Attribute {
        public int Color { get; }

        public DefaultColorAttribute(int color) {
            Color = color;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class DefaultEnabledAttribute : Attribute {
        public bool Enabled { get; }

        public DefaultEnabledAttribute(bool enabled) {
            Enabled = enabled;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class DefaultUseIconAttribute : Attribute {
        public bool UseIcon { get; }

        public DefaultUseIconAttribute(bool useIcon) {
            UseIcon = useIcon;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class DefaultSizeAttribute : Attribute {
        public int Size { get; }

        public DefaultSizeAttribute(int size) {
            Size = size;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class DefaultShowLabelAttribute : Attribute {
        public bool Enabled { get; }

        public DefaultShowLabelAttribute(bool enabled) {
            Enabled = enabled;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class UsageAttribute : Attribute {
        public string Usage { get; }

        public UsageAttribute(string usage) {
            Usage = usage;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class ExampleAttribute : Attribute {
        public string Command { get; }
        public string Description { get; }

        public ExampleAttribute(string command, string description) {
            Command = command;
            Description = description;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class CommandPatternAttribute : Attribute {
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
    public class SupportsFlagsAttribute : Attribute {
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class FullDescriptionAttribute : Attribute {
        public string Description { get; }

        public FullDescriptionAttribute(string description) {
            Description = description;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class HotkeyAttribute : Attribute {
        public string Title { get; }
        public string Description { get; }

        public HotkeyAttribute(string title, string description) {
            Title = title;
            Description = description;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class ExpressionMethodAttribute : Attribute {
        public string Name { get; }

        public ExpressionMethodAttribute(string name) {
            Name = name;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class ExpressionParameterAttribute : Attribute {
        public int Index { get; }
        public Type Type { get; }
        public string Name { get; }
        public string Description { get; }

        public ExpressionParameterAttribute(int index, Type type, string name, string description) {
            Index = index;
            Type = type;
            Name = name;
            Description = description;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class ExpressionReturnAttribute : Attribute {
        public Type Type { get; }
        public string Description { get; }

        public ExpressionReturnAttribute(Type type, string description) {
            Type = type;
            Description = description;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class DefaultChatColorAttribute : Attribute {
        public short Color { get; }

        public DefaultChatColorAttribute(short color) {
            Color = color;
        }
    }
}
