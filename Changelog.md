## 0.0.4 (2019-9-24)
- Fixed hardcoded vtank profiles path :)
- VTank nav parser now only reads recordCount records, so it ignores trailing blank lines in the file

## 0.0.3 (2019-9-24) [UtilityBeltInstaller-0.0.3.exe](/uploads/d41ff70af22e6874cf8d7b28a497546f/UtilityBeltInstaller-0.0.3.exe)
- Think when AutoSalvage bails due to open vendor (thanks Cosmic Jester)
- Visual VTank Nav Lines (like vi2 blue nav lines, but fully customizable and local)
- Hopefully fix issue where AutoVendor would get stuck with pyreals (instead of buying notes)
- Dont try to sell / salvage retained
- Added `/ub ig` ItemGiver command to give items matching a loot profile to another player (thanks Schneebly)
- Added `/ub jump` commands (thanks Schneebly)
- AutoVendor now requires id data for everything, to properly check for tinkered and retained. (TODO: inventory cache)
- AutoVendor will no longer sell inscribed items (thanks Cosmic Jester)

## 0.0.2 (2019-3-30) [UtilityBeltInstaller-0.0.2.exe](/uploads/c37e201894413fa61bc2397a10cc3af5/UtilityBeltInstaller-0.0.2.exe)
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

## 0.0.1 (2019-02-26) [UtilityBeltyInstaller-0.0.1.exe](/uploads/cab22eca3752a8c6d09eb9f95b6906b9/UtilityBeltInstaller-0.0.1.exe)
- Initial Release