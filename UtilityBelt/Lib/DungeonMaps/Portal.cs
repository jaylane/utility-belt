using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.DungeonMaps {
    public class Portal {
        public int Id;
        public string Name;
        public double X;
        public double Y;
        public double Z;

        public Portal(WorldObject portal) {
            Id = portal.Id;
            Name = portal.Name.Replace("Portal to", "").Replace("Portal", "");

            var offset = portal.Offset();

            X = offset.X;
            Y = offset.Y;
            Z = Math.Round(offset.Z);
        }
    }
}
