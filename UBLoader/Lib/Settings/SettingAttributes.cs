using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UBLoader.Lib.Settings {
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
}
