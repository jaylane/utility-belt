using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Networking.Messages {
    [Serializable()]
    public class CharacterPositionMessage {
        public int LandCell { get; set; }
        public double Heading { get; set; }

        public double NS { get; set; }
        public double EW { get; set; }
        public double Z { get; set; }

        public CharacterPositionMessage() {

        }
    }
}
