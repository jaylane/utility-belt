using LiteDB;

namespace UtilityBelt.Lib.Models {
    public class WeenieSpawn {
        public int Id { get; set; }
        public int Wcid { get; set; }
        public string Description { get; set; }
        public Position Position { get; set; }
        [BsonRef("weenies")]
        public Weenie Weenie { get; set; } 
    }
}
