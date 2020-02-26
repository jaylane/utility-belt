using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib {
    public static class Inventory {
        public static int CountByName(string name) {
            var count = 0;
            var inventory = new List<int>();

            UBHelper.InventoryManager.GetInventory(ref inventory, UBHelper.InventoryManager.GetInventoryType.AllItems);
            foreach (var item in inventory) {
                var weenie = new UBHelper.Weenie(item);
                if (weenie.Name.ToLower().Equals(weenie.Name.ToLower()))
                    count += weenie.StackCount;
            }

            return count;
        }
    }
}
