using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Models {
    class QuestFlag {
        public int Id { get; set; }
        public string Server { get; set; }
        public string Character { get; set; }
        public string Key { get; set; }
        public string Description { get; set; }
        public int Solves { get; set; }
        public int MaxSolves { get; set; }
        public DateTime CompletedOn { get; set; }
        public int RepeatTime { get; set; }
    }
}
