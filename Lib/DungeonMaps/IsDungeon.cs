using Decal.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.DungeonMaps {
    partial class Dungeon {
        /* Copyright (c) 2007 Ben Howell
         * This software is licensed under the MIT License
         * 
         * Permission is hereby granted, free of charge, to any person obtaining a 
         * copy of this software and associated documentation files (the "Software"), 
         * to deal in the Software without restriction, including without limitation 
         * the rights to use, copy, modify, merge, publish, distribute, sublicense, 
         * and/or sell copies of the Software, and to permit persons to whom the 
         * Software is furnished to do so, subject to the following conditions:
         * 
         * The above copyright notice and this permission notice shall be included in 
         * all copies or substantial portions of the Software.
         * 
         * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
         * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
         * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
         * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
         * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
         * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
         * DEALINGS IN THE SOFTWARE.
         */
        private static Dictionary<int, bool> mIsDungeonCache = new Dictionary<int, bool>();

        public bool IsDungeon() {
            if ((LandCellId & 0x0000FFFF) < 0x0100) {
                return false;
            }

            int dungeonId = (LandCellId >> 16) & 0xFFFF;
            bool isDungeon;
            if (mIsDungeonCache.TryGetValue(dungeonId, out isDungeon)) {
                return isDungeon;
            }

            FileService service = Globals.Core.Filter<FileService>();
            byte[] dungeonBlock = service.GetCellFile(LandCellId);

            if (dungeonBlock == null || dungeonBlock.Length < 5) {
                // This shouldn't happen...
                isDungeon = true;
                if (dungeonBlock == null) {
                    Logger.Debug("Null cell file for landblock: " + LandCellId.ToString("X8"));
                }
                else {
                    Logger.Debug("Cell file is only " + dungeonBlock.Length
                        + " bytes long for landblock: " + LandCellId.ToString("X8"));
                }
            }
            else {
                // Check whether it's a surface dwelling or a dungeon
                isDungeon = (dungeonBlock[4] & 0x01) == 0;
            }

            mIsDungeonCache.Add(dungeonId, isDungeon);
            return isDungeon;
        }
    }
}
