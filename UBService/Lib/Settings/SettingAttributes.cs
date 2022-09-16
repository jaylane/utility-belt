using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UBService.Lib.Settings {
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Field)]
    public class SummaryAttribute : Attribute {
        public string Summary { get; }

        public SummaryAttribute(string summary) {
            Summary = summary;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class SupportsFlagsAttribute : Attribute {

    }


    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class DontShowInSettingsAttribute : Attribute {

    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class MinMaxAttribute : Attribute {
        public float MinValue { get; }
        public float MaxValue { get; }

        public MinMaxAttribute(float minValue, float maxValue) {
            MinValue = minValue;
            MaxValue = maxValue;
        }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Field)]
    public class CategoryAttribute : Attribute {
        public string Category { get; }

        public CategoryAttribute(string category) {
            Category = category;
        }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Field)]
    public class FormatAttribute : Attribute {
        public string Format { get; }

        public FormatAttribute(string format) {
            Format = format;
        }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Field)]
    public class ChoicesAttribute : Attribute {
        public string DefaultValue { get; }
        public Type ResultClass { get; }

        public ChoicesAttribute(string defaultValue, Type iChoiceResults) {
            DefaultValue = defaultValue;
            ResultClass = iChoiceResults;
        }
    }

    public interface IChoiceResults {
        public IList<string> GetChoices();
    }
}
