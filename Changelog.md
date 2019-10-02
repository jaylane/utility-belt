## 0.0.9
- Add support to VisualNav for follow type navs
- Default autovendor autostack while vendoring to true
- Added `/ub count item <regexMatch>` command (thanks Schneebly)
- Added ability to "think" about a quest flag status with `/ub quests check <flag>` (click on a quest in questtracker to see the flag)

## 0.0.8 (2019-9-28) [UtilityBeltInstaller-0.0.8.exe](/uploads/f1b2c542a7630438732e2553dec3a725/UtilityBeltInstaller-0.0.8.exe)
- Added experimental option to show embedded nav routes
- Fix potential exception in AutoVendor
- Check gitlab for updates on login
- Remove debug spam

## 0.0.7 (2019-9-27) [UtilityBeltInstaller-0.0.7.exe](/uploads/caaf8eec17828a2fb60d7e20d8fba793/UtilityBeltInstaller-0.0.7.exe)
- Added option to only sell salvage from main pack
- Automatically disable and restore vtank auto cram/stack settings while vendoring
- Fix aetheria and autovendoring with red rules
- VTankFellowHeals performance and bugfixes
- Fix exception in certain routes causing VisualNav to stop drawing

## 0.0.6 (2019-9-27) [UtilityBeltInstaller-0.0.6.exe](/uploads/764c570d2d65f563bcf7832bab7707ad/UtilityBeltInstaller-0.0.6.exe)
- Fix VTank location from registry
- Fix gem names so they don't prepend material names and end up like "Jet Jet"
- AutoSalvage performance
- Assessor performance

## 0.0.5 (2019-9-25) [UtilityBeltInstaller-0.0.5.exe](/uploads/58ce3ed901ef81ad776a7e973f7ba704/UtilityBeltInstaller-0.0.5.exe)
- UB now passes all local character vitals to vTank, to support Healing/Restaming/Infusing all characters running on the same computer, regardless of being in a fellowship.

## 0.0.4 (2019-9-24) [UtilityBeltInstaller-0.0.4.exe](/uploads/931df39dc0449bc0c5f05dcee59af12e/UtilityBeltInstaller-0.0.4.exe)
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