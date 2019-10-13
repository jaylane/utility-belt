using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Constants {
    public enum UseFlag {
        SourceUnusable = 0x00000001,
        SourceSelf = 0x00000002,
        SourceWielded = 0x00000004,
        SourceContained = 0x00000008,
        SourceViewed = 0x00000010,
        SourceRemote = 0x00000020,
        SourceNoApproach = 0x00000040,
        SourceObjectSelf = 0x00000080,
        TargetUnusable = 0x00010000,
        TargetSelf = 0x00020000,
        TargetWielded = 0x00040000,
        TargetContained = 0x00080000,
        TargetViewed = 0x00100000,
        TargetRemote = 0x00200000,
        TargetNoApproach = 0x00400000,
        TargetObjectSelf = 0x00800000
    }
}
