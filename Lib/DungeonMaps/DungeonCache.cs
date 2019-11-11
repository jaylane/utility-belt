using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.DungeonMaps {
    public static class DungeonCache {
        private static Dictionary<uint, Dungeon> cache = new Dictionary<uint, Dungeon>();

        public static Dungeon Get(uint cellId) {
            if (cache.ContainsKey(cellId & 0xFFFF0000)) return cache[cellId & 0xFFFF0000];
            if ((uint)(cellId & 0x0000FFFF) < 0x0100) return null;

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
