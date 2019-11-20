## 0.0.15 (TBD)
- Added `VisualNav.ScaleCurrentWaypoint` option to enable/disable scaling current waypoint using vTankMinNavDistance
- Added `/ub fixbusy` command to reset the client's Busy State and Busy Count both to 0.
- Beta builds are now available in #utilitybelt on https://discord.gg/c75pPaz
- Add resiliency to auto trade

## 0.0.14 (2019-11-15) [UtilityBeltInstaller-0.0.14.exe](/uploads/0d29c14ccc27377941cb5f92c37ffaef/UtilityBeltInstaller-0.0.14.exe)
- Added VisualNav.Enabled setting
- Added `/ub playeroption <option> <on/true|off/false>` - command to set player options (thanks Yonneh)
- Added `/ub videopatch {enable,disable,toggle}` - online toggling of Mag's video patch (thanks Yonneh)
- Fixed bug with `/ub delay` causing vTank to barf
- Fixed bugs/crash with LootProfileWatcher (thanks Yonneh)
- Added ability to modify settings arrays from cli (thanks Cosmic Jester)

## 0.0.13 (2019-11-11) [UtilityBeltInstaller-0.0.13.exe](/uploads/5502b71f708d32f45f5bef073d3759b7/UtilityBeltInstaller-0.0.13.exe)
- Added new and improved AutoVendor test mode (thanks Yonneh)
- Added `/ub delay <milliseconds> <command>` to run <command> after <milliseconds> delay
- Fixed IDQueue stuckage (no more getting stuck waiting on item id!) (thanks Yonneh)
- Added VTankLootProfileWatcher - Automatically reloads loot profiles when they change! (thanks Yonneh)
- Added UtilityBelt.dll.config - Global plugin settings (this file autogenerates next to UtilityBelt.dll) (thanks Yonneh)
- All plugin resources and dependencies are now embedded in the dll file
- Fixed DungeonMaps failing to draw in some circumstances
- Fixed once nav routes now properly update in DungeonMaps as each waypoint is hit
- Fixed to allow autotrade when running from cli even if not explicitly enabled. (thanks Cosmic Jester)
- Fixed isDungeon check, DungeonMaps should no longer draw inside dwellings
- Fixed `/ub portal` now works with portals that are ObjectClass==NPC (thanks Yonneh)
- Fixed `/ub ig` now works with NPCs again (thanks Yonneh)
- Fixed issue with bail timers bailing early (thanks Yonneh)

## 0.0.12 (2019-11-03) [UtilityBeltInstaller-0.0.12.exe](/uploads/6de18c1fe018bcd48e0620d25070db29/UtilityBeltInstaller-0.0.12.exe)
- Added `/ub vitae` command to check current vitae %
- Added Chat Logger (thanks Cosmic Jester)
- Added `/ub follow[p] [character|selected]` - follows the named character, selected, or closest (thanks Yonneh)
- Fixed autovendor bailing early when waiting on id requests (thanks Yonneh)
- Fixed splitting stacks when those stacks were already added to a vendor (thanks Yonneh)

## 0.0.11 (2019-10-27) [UtilityBeltInstaller-0.0.11.exe](/uploads/eeffc7b5a32903ec241d894fbcc7e9af/UtilityBeltInstaller-0.0.11.exe)
- All new event-based autovendor code! (thanks Yonneh)
- Added AutoAddToTrade (thanks Cosmic Jester)
- Added `VTank.ShareVitals` option to enable/disable vtank vital sharing
- Fixed bug where InventoryManager settings were not displaying properly in the ui.
- Added `/ub closestportal with nav` blocking and retries (thanks Yonneh)
- Added `/ub portal[p] <portal name>` with nav blocking and retries (thanks Yonneh)
- `/ub count` now shows cumulative item count (thanks Cosmic Jester)
- Added /ig give[P(partial item name),r(regex)][p(partial character)] [count] <itemname> to <character> to give item(s) by name (thanks Yonneh)

## 0.0.10 (2019-10-13) [UtilityBeltInstaller-0.0.10.exe](/uploads/0e26748b43ba78352b2c047f0cc0061a/UtilityBeltInstaller-0.0.10.exe)
- Customary remove debug spam release

## 0.0.9 (2019-10-13) [UtilityBeltInstaller-0.0.9.exe](/uploads/f0ba230502e0e62969724d36f6a3d8ed/UtilityBeltInstaller-0.0.9.exe)
- Add support to VisualNav for follow type navs
- Default autovendor autostack while vendoring to true
- Added `/ub count item <regexMatch>` command (thanks Schneebly)
- Added ability to "think" about a quest flag status with `/ub quests check <flag>` (click on a quest in questtracker to see the flag)
- Fix potential exception in UpdateVTankVitalInfo
- AutoVendor no longer ids items that the vendor wont buy
- Plugin Windows now remember size / position between sessions (per character)
- Fix free packspace check when autovendoring (thanks Yonneh)
- Dungeon Maps can now be panned (move around the map manually, instead of always following character)
- Dungeon Maps now show vTank nav routes
- AutoVendor/ItemGiver now pauses vTank (nav/cram/stack) as needed (thanks Yonneh)
- Dungeon Maps colors are now customizable
- Added `/ub vendor {buyall,sellall,clearbuy,clearsell}` commands
- Added `/ub vendor open[p] {vendorname,vendorid,vendorhex}` command, that pauses vTank navigation, and retries opening the vendor if it fails
- `/ub [faceDirection] [s]jump[wzxc] [msToHoldDown]` now pauses vTank navigation, and retries jumps when the server does not respond.
- Dungeon Maps can now show markers / labels for *everything*
- Added `/ub opt <option> [value]` command for getting/setting options. `/ub opt list` to list

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