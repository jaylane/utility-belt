using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Models {

    public class Landblock {
        public int Id { get; set; }
        public List<Link> Links { get; set; } = new List<Link>();
        public List<WeenieSpawn> Weenies { get; set; } = new List<WeenieSpawn>();
        public int CheckFailCount { get; set; }
        public DateTime LastCheck { get; set; }
    }
}
