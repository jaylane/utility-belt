## 0.0.2 (2019-3-30)
- Added option to change display of visited DungeonMaps tiles
- InventoryManager options are no longer hardcoded, so you can turn off stack/cram during AutoVendor
- Fixed bug where LootCore was getting referenced too early, causing everything to break
- Fixed QuestTracker categorizing of certain quests (Facility Hub, maybe others as well)
- New logger (limit overall log file sizes, limit exceptions logged per session)
- Fixed DungeonMaps debug setting ui display (was showing the value of DungeonMaps.Enabled on first load)
- Better isDungeon detection on landcells
- Fixed a few exceptions.
- QuestTracker now shows quest timers in realtime (active countdown on ui)
- QuestTracker can now filter quests by regex from the ui
- AutoSalvage w/ force now salvages one item at a time (so vtank can combine properly)
- QuestTracker now has the ability to look up quest keys in xml, for friendlier names
- Updated dungeon map tiles
- AutoSalvage no longer runs while a vendor is open

## 0.0.1 (2019-02-26)
- Initial Release