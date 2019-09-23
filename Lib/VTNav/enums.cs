using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.VTNav {
    public enum eWaypointType {
        Point = 0,
        Portal = 1,
        Recall = 2,
        Pause = 3,
        ChatCommand = 4,
        OpenVendor = 5,
        Portal2 = 6,
        UseNPC = 7,
        Checkpoint = 8,
        Jump = 9,
        Other = 99, // 0x00000063
    }

    public enum eNavType {
        Linear,
        Circular,
        Target,
        Once,
    }
}
