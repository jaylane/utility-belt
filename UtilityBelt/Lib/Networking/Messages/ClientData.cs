using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UtilityBelt.Networking;
using UtilityBelt.Service;

namespace UtilityBelt.Lib.Networking.Messages {
    [ProtoContract]
    public class ClientData {
        [ProtoMember(1)]
        public uint Id { get; set; }

        [ProtoIgnore]
        public string Name  => RemoteClient?.Name ?? "Unknown";

        [ProtoMember(3)]
        public string WorldName { get; set; } = "Unknown";

        [ProtoMember(4)]
        public int PlayerId { get; set; }

        [ProtoMember(5)]
        public int CurrentHealth { get; set; }

        [ProtoMember(6)]
        public int CurrentMana { get; set; }

        [ProtoMember(7)]
        public int CurrentStamina { get; set; }

        [ProtoMember(8)]
        public int MaxHealth { get; set; }

        [ProtoMember(9)]
        public int MaxMana { get; set; }

        [ProtoMember(10)]
        public int MaxStamina { get; set; }

        [ProtoMember(12)]
        public List<TrackedItemStatus> TrackedItems { get; set; } = new List<TrackedItemStatus>();

        [ProtoMember(13)]
        public uint LandCell { get; set; }

        [ProtoMember(14)]
        public double Heading { get; set; }

        [ProtoMember(15)]
        public double NS { get; set; }

        [ProtoMember(16)]
        public double EW { get; set; }

        [ProtoMember(17)]
        public double Z { get; set; }

        [ProtoMember(18)]
        public bool HasPositionInfo { get; set; }

        [ProtoIgnore]
        public RemoteClient RemoteClient => UBService.UBNet?.UBNetClient?.Clients?.Select(c => c.Value).FirstOrDefault(c => c.Id == Id);


        [ProtoMember(19)]
        public bool HasVitalInfo { get; set; }

        public ClientData() { }

        public ClientData(uint id) {
            Id = id;
        }

        public bool HasAnyTags(List<string> tagsToCheck) {
            if (RemoteClient?.Tags == null || RemoteClient.Tags.Count == 0) {
                return false;
            }

            foreach (var tag in tagsToCheck) {
                if (RemoteClient?.Tags?.Contains(tag) == true) {
                    return true;
                }
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

        public override string ToString() {
            return $"ClientData<{Id}, {Name}>";
        }
    }
}
