using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.DungeonMaps {
    public static class DungeonCache {
        private static Dictionary<int, Dungeon> cache = new Dictionary<int, Dungeon>();

        public static Dungeon Get(int cellId) {
            if (cache.ContainsKey(cellId >> 16 << 16)) return cache[cellId >> 16 << 16];
            if ((uint)(cellId << 16 >> 16) < 0x0100) return null;

            var watch = System.Diagnostics.Stopwatch.StartNew();
            var block = new Dungeon(cellId);
            watch.Stop();

            Logger.Debug(string.Format("DungeonMaps: took {0}ms to cache LandBlock {1} (isDungeon? {2} ({3}))", watch.ElapsedMilliseconds, (cellId).ToString("X8"), block.IsDungeon(), ((uint)(Globals.Core.Actions.Landcell << 16 >> 16)).ToString("X4")));

            cache.Add(block.LandBlockId, block);

            return Get(cellId);
        }

        public static void Clear() {
            cache.Clear();
        }
    }
}
