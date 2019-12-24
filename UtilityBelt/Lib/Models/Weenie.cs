using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UtilityBelt.Lib.Constants;

namespace UtilityBelt.Lib.Models {
    public class Weenie {
        public int Id { get; set; }
        public int Type { get; set; }
        public Dictionary<string, WeenieAttribute> Attributes { get; set; } = new Dictionary<string, WeenieAttribute>();
        public Dictionary<int, bool> BoolStats { get; set; } = new Dictionary<int, bool>();
        public Dictionary<int, int> IntStats { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> DIDStats { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> IIDStats { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, long> Int64Stats { get; set; } = new Dictionary<int, long>();
        public Dictionary<int, float> FloatStats { get; set; } = new Dictionary<int, float>();
        public Dictionary<int, string> StringStats { get; set; } = new Dictionary<int, string>();
        public int CheckFailCount { get; set; }
        public DateTime LastCheck { get; set; }

        public bool HasStringValue(int key) {
            return StringStats.ContainsKey(key);
        }
        public string StringValue(int key, string defaultValue) {
            if (HasStringValue(key))
                return StringStats[key];
            return defaultValue;
        }

        public bool HasDIDValue(int key) {
            return DIDStats.ContainsKey(key);
        }
        public int DIDValue(int key, int defaultValue) {
            if (HasDIDValue(key))
                return DIDStats[key];
            return defaultValue;
        }

        public bool HasIntValue(int key) {
            return IntStats.ContainsKey(key);
        }
        public int IntValue(int key, int defaultValue) {
            if (HasIntValue(key))
                return IntStats[key];
            return defaultValue;
        }

        public bool HasBoolValue(int key) {
            return BoolStats.ContainsKey(key);
        }
        public bool BoolValue(int key, bool defaultValue) {
            if (HasBoolValue(key))
                return BoolStats[key];
            return defaultValue;
        }

        public ObjectClass GetObjectClass() {
            ObjectClass objectClass = ObjectClass.Misc;

            switch ((WeenieType)Type) {
                case WeenieType.Admin:
                    return ObjectClass.Player;
                case WeenieType.Book:
                    return ObjectClass.Book;
                case WeenieType.Caster:
                    return ObjectClass.WandStaffOrb;
                case WeenieType.Chest:
                    return ObjectClass.Container;
                case WeenieType.Clothing:
                    return ObjectClass.Clothing;
                case WeenieType.Coin:
                    return ObjectClass.Money;
                case WeenieType.Container:
                    return ObjectClass.Container;
                case WeenieType.Corpse:
                    return ObjectClass.Corpse;
                case WeenieType.Cow:
                    return ObjectClass.Monster;
                case WeenieType.CraftTool:
                    return ObjectClass.Misc;
                case WeenieType.Creature:
                    if (IntValue(95, 0)== 8) {
                        return ObjectClass.Npc;
                    }
                    return ObjectClass.Monster;
                case WeenieType.Door:
                    return ObjectClass.Door;
                case WeenieType.Food:
                    return ObjectClass.Food;
                case WeenieType.Gem:
                    return ObjectClass.Gem;
                case WeenieType.House:
                    return ObjectClass.Housing;
                case WeenieType.HousePortal:
                    return ObjectClass.Portal;
                case WeenieType.PressurePlate:
                    return ObjectClass.Misc;
                case WeenieType.Scroll:
                    return ObjectClass.Scroll;
                case WeenieType.Sentinel:
                    return ObjectClass.Player;
                case WeenieType.SpellComponent:
                    return ObjectClass.SpellComponent;
                case WeenieType.Vendor:
                    return ObjectClass.Vendor;
                case WeenieType.Portal:
                    return ObjectClass.Portal;
            }

            return objectClass;
        }
    }
}
