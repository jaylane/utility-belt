using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.DungeonMaps {
    public static class LandBlockCache {
        private static Dictionary<int, LandBlock> cache = new Dictionary<int, LandBlock>();

        public static LandBlock Get(int cellId) {
            if (cache.ContainsKey(cellId >> 16 << 16)) return cache[cellId >> 16 << 16];
            if ((uint)(cellId << 16 >> 16) < 0x0100) return null;

            var watch = System.Diagnostics.Stopwatch.StartNew();
            var block = new LandBlock(cellId);
            watch.Stop();

            if (Globals.Config.DungeonMaps.Debug.Value == true) {
                Util.WriteToChat(string.Format("DungeonMaps: took {0}ms to cache LandBlock {1} (isDungeon? {2} ({3}))", watch.ElapsedMilliseconds, (cellId).ToString("X8"), block.IsDungeon(), ((uint)(Globals.Core.Actions.Landcell << 16 >> 16)).ToString("X4")));
            }

            cache.Add(block.LandBlockId, block);

            return Get(cellId);
        }

        public static void Clear() {
            cache.Clear();
        }
    }
}
