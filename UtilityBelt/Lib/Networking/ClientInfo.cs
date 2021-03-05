using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UtilityBelt.Lib.Settings;

namespace UtilityBelt.Lib.Networking {

    public class ClientInfo {
        public int ClientId { get; set; }

        public DateTime LastUpdate = DateTime.UtcNow;

        public string Name { get; set; } = "Unknown";
        public string WorldName { get; set; } = "Unknown";
        public int PlayerId { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public int CurrentHealth { get; set; }
        public int CurrentMana { get; set; }
        public int CurrentStamina { get; set; }
        public int MaxHealth { get; set; }
        public int MaxMana { get; set; }
        public int MaxStamina { get; set; }
        public bool HasLoginInfo { get; set; }
        public List<TrackedItemStatus> TrackedItems { get; set; } = new List<TrackedItemStatus>();
        public int LandCell { get; set; }
        public double Heading { get; set; }

        public double NS { get; set; }
        public double EW { get; set; }
        public double Z { get; set; }
        public bool HasPositionInfo { get; set; }

        public ClientInfo(int id) {
            ClientId = id;
        }

        public bool HasTags(List<string> tagsToCheck) {
            if (Tags == null || Tags.Count == 0)
                return false;

            foreach (var tag in tagsToCheck) {
                if (Tags.Contains(tag))
                    return true;
            }
            return false;
        }

        public double DistanceTo() {
            if (PlayerId == UtilityBeltPlugin.Instance.Core.CharacterFilter.Id)
                return 0;
            if (PlayerId != 0 && UtilityBeltPlugin.Instance.Core.Actions.IsValidObject(PlayerId))
                return PhysicsObject.GetDistance(PlayerId);
            return (new Coordinates(EW, NS, Z)).DistanceTo(Coordinates.Me);
        }
    }
}
