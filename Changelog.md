## 0.2.2 (TBD)
- Changes go here

## 0.2.1 (2021-01-02) [UtilityBeltInstaller-0.2.1.exe](https://gitlab.com/utilitybelt/utilitybelt.gitlab.io/uploads/8552636461a1e2d1ec0c8c140cc7c9dd/UtilityBeltInstaller-0.2.1.exe)
- Disabled character options profiles -- too buggy

## 0.2.0 (2020-12-31) [UtilityBeltInstaller-0.2.0.exe](https://gitlab.com/utilitybelt/utilitybelt.gitlab.io/uploads/61bc3860576e115c1b8b1b02cc93f526/UtilityBeltInstaller-0.2.0.exe)
- Fix issue where calling get[gp]var expression would set an invalid value to the cache, causing test[gp]var to have incorrect results
- Add Landscape Maps marker for current Arrow target
- Fix getitemcountininventorybyname and getitemcountininventorybynamerx expressions now count containers and equipped items
- Fix wobjectfindininventorybynamerx and wobjectfindininventorybyname to use generated ingame names, rather than the weenie name
- Add Iron/Granite (max damage) to AutoTinker
- Fix p(partial) flag on swearallegiance and breakallegiance commands
- Add exposed global settings to plugin settings ui and ub opt commands
- Fix corrupt db no longer breaks the plugin from loading, it is deleted and remade
- Add new settings ui
- Fix coordinates parsed from strings now read in Z data
- Add shared settings profiles, check the new profiles tab
- Add client ui profiles, for managing shared client ui window positions.
- Add `/ub (get|set)ui` to get/set the client ui window positions as a string
- Add profile support for player options
- Add list support to expressions, see expression documentation
- Add shortcut for getting expression variables ie `$myVariable`, see expression documentation
- Fix `/ub tinkcalc` not showing output
- Fix `getcharquadprop[]` luminance properties
- Add friendlier error messages for expressions
- Add `&&` and `||` expression operators now short-circuit (incompatible with vtank boolean operators)
- Fix a number of expression parse issues
- Fix `-` character must now be escaped in expression strings
- Add hexadecimal number format support to expressions, ie `0xff`
- Fix issue where plugin was not loading after creating a new character
- Fix issue where plugin could load multiple times if another character was already in world
- Fix issue where huds would stop rendering after a resolution change
- Fix AutoTinker no longer tries to tinker untinkerable items
- Add Networking - Now spellcast attempts / success are shared with vtank (vuln/imp overlapping)
- Add `/ub bc [msDelayBetweenClients] <command>` for broadcasting commands to all open clients
- Add Exceptions are now uploaded to the mothership by defualt, set `Global.UploadExceptions` to false to disable (pls dont, i want to fix bugs)

## 0.1.8 (2020-11-30) [UtilityBeltInstaller-0.1.8.exe](https://gitlab.com/utilitybelt/utilitybelt.gitlab.io/uploads/3914a8775e8d4643d4c967761ff9b9d2/UtilityBeltInstaller-0.1.8.exe)
- Add expression `isrefreshingquests[]`
- Add command `/ub myquests` - runs /myquests command and hides chat output
- Fix issue where plugin could load multiple instances of itself
- Fix issue where Plugin.VideoPatch could break physics

## 0.1.7 (2020-11-27) [UtilityBeltInstaller-0.1.7.exe](https://gitlab.com/utilitybelt/utilitybelt.gitlab.io/uploads/03886a9b9c41e8b05703da8ba237dae1/UtilityBeltInstaller-0.1.7.exe)
- Fix salvage name check in autotinker
- Add basic landscape maps (visualnav capable)
- Fix ColorPicker view in settings
- Fix issue where ItemGiver could get in a bugged state and prevent usage between relogs
- Add pointable arrow. By default it will look for coordinates in the chatbox and make them clickable.
- Add command `/ub arrow point <coordinates>` for pointing the arrow
- Add command `/ub arrow face` to face your character in the same direction as the arrow
- Add UtilityBelt version watermark to character selection screen
- Fix bug in persistent expression variables where they could be serialized to settings
- Add DerethTime hud that shows day/night cycles
- Add DerethTime expressions: `getgameyear[]`, `getgamemonth[]`, `getgamemonthname[i]`, `getgameday[]`, `getgamehour[]`, `getgamehourname[i]`, `getminutesuntilday[]`, `getminutesuntilnight[]`, `getgameticks[]`, `getisday[]`, `getisnight[]`
- Fix bug where multiple instances of the plugin could be loaded on ACE servers
- add a very rudimentary frame rate limiter UBHelper.SimpleFrameLimiter
- add `/ub globalframerate <frameRate>` and `FrameRate` in utilitybelt.dll.config, to globally limit frame rate (including login screen)
- add `/ub bgframerate <frameRate>` and setting `Plugin.BackgroundFrameRate`, to limit frame rate while the client does not have focus

## 0.1.6 (2020-11-08) [UtilityBeltInstaller-0.1.6.exe](https://gitlab.com/utilitybelt/utilitybelt.gitlab.io/uploads/718dcc4607d717c7db6772edd033fcf3/UtilityBeltInstaller-0.1.6.exe)
- Fix getitemcountininventorybynamerx[] and getitemcountininventorybyname[] now properly search salvage names like `Silk Salvage (100)`
- Fix coordinateparse[] inverting ordinal directions
- Add expressions `getheadingto[wobject obj]` and `getheading[wobject obj]` to get the heading *to* or *of* and object
- Add math expressions: `acos, asin, atan, atan2, cos, cosh, sin, sinh, sqrt, tan, tanh`
- Fix expression distance methods so they properly error out on invalid objects/coordinates
- Fix expression strings to be more compatible with vtank
- UB expressions are now compatible with IBControl and BuffCaster
- Fix autovendor for servers with custom pyreal max stack size
- Fix Wobjectfindnearestbynameandobjectclass[] is now returning *nearest* found wobject, instead of the first one it finds
- Add caching to Persistent Expression Variables for better performance
- Disable plugin hot reloading for general users
- Fix global/persistent string storage expressions with special characters
- add `/ub fellow create <Name>|quit|disband|open|close|status|recruit[p][ Name]|dismiss[p][ Name]|leader[p][ Name]`
- add `/ub swearallegiance[p][ <name|id|selected>]`
- add `/ub breakallegiance[p][ <name|id|selected>]` (TODO: scan Allegiance Heirarchy, instead of visible)
- replace expressions `getfellowshipstatus[]`, `getfellowshipname[]`, with DMA methods (performance)
- add expressions `getfellowshipcount[]`, `getfellowshipleaderid[]`, `getfellowid[x]`, `getfellowname[x]`, `getfellowshiplocked[]`, `getfellowshipisleader[]`, `getfellowshipisopen[]`, `getfellowshipisfull[]`, `getfellowshipcanrecruit[]`
- add error on init, if UBHelper is out of date
- Fix issue with Town Network causing bad dungeon maps lag in some instances

## 0.1.5 (2020-05-18) [UtilityBeltInstaller-0.1.5.exe](https://gitlab.com/utilitybelt/utilitybelt.gitlab.io/uploads/f924bff1dc10218e007a73e8bf0d3f45/UtilityBeltInstaller-0.1.5.exe)
- Fix AutoTinker now supports tinkering clothing with gold/linen/moonstone/pine
- Fix blood shreth killtask, now shows up correctly under killtasks. (maybe fixes others too?)
- Fix some quests that were showing up on the questtracker once tab that were actually solvable multiple times
- Fix @myquests output not showing when manually typing the command
- Add expression `wobjectgethealth[wobject]`, Returns the health percentage of a mob/player
- Add Virindi Chat System support
- Add settings to toggle/recolor ub chat categories: general, debug, errors, expressions
- Add Rend All option to Autotinker
- Add Granite/Iron calculator for melee weapons
- Add `/ub tinkcalc` for best granite/iron combination
- Add Setting `DungeonMaps.LabelFontSize`
- Fix dungeonmap portal names from lsd data
- Add support to expressions for floats without a preceding digit ie `.5`

## 0.1.4 (2020-04-04) [UtilityBeltInstaller-0.1.4.exe](https://gitlab.com/utilitybelt/utilitybelt.gitlab.io/uploads/0e205b055afc488ef2a4e4194cb6b72f/UtilityBeltInstaller-0.1.4.exe)
- Fix `/ub count profile <profile>` to actually output results to chat
- Add `/ub date[utc] [format]` command to echo the current date, with optional format
- Fix bug where long chat nav point texts would crash the client when visualnav is enabled
- Show current landcell when `DungeonMaps.Debug` is set to true
- Add `/ub opt toggle <setting>` command to toggle boolean values
- Add support for DHS/VHS hotkeys for common settings
- Fix some memory leaks
- Fix bug where item counts from `/ub count profile` were not properly resetting
- Add ItemGiver UI
- Experimental: Optionally replace vtanks meta expression system with one from UB. Read more here: [Expression Docs](http://utilitybelty.gitlab.io/docs/expressions/)
- Add settings for `AutoVendor.EnableBuying` and `AutoVendor.EnableSelling` to disable specific autovendor functionality
- Fix AutoTinker now supports quest/pathwarden salvage
- Fix AutoTinker now works with tailored items that no longer have a material
- Fix AutoTinker now works with gold/linen/moonstone/pine
- Fix issue where quest flags were not being cleared from the tracker
- Fix issue where autovendor would bail early sometimes when buying trade notes
- Fix issue where autovendor would not buy packs properly

## 0.1.2 (2020-01-02) [UtilityBeltInstaller-0.1.2.exe](https://gitlab.com/utilitybelt/utilitybelt.gitlab.io/uploads/b1bcdf8401543ba32afd4cf480ff6408/UtilityBeltInstaller-0.1.2.exe)
- "Fix" `/ub playeroption` on ACE, by sending the entire PlayerSystem packet
- Feed all UB messages to vTank, so they can be scraped by metas without needing a /think
- Fix manually typing /myquests not updating questtracker
- Rewrite of Assessor backend, to be more redundant, and repeat failed ids
- Moderate changes to Jumper backend, to remove keyboard dependancy
- Fixed Quest timers for quests that are longer than 11.5 day repeat
- Added `/ub translateroute` command to convert a nav route from one landblock to another
- Fix itemgiver: negative keep # wasn't properly tracking items already given
- Add QuestTracker.AutoRequest setting
- Rewrite DungeonMaps with performance in mind
- Fix equip mgr command pattern, default profile selection, and clean up event handling
- Fix `/ub count item` output when no items were found
- Add `/ub resolution <width> <height>` to change the client resolution
- Add `/ub textures <landscape> <landscapeDetail> <environment> <environmentDetail> <sceneryDraw> <landscapeDraw>` to set the client texture options
- Fixed a few potential crash to desktop bugs
- Nav routes now properly show on dungeon maps when video patch is enabled
- Add `/ub professor <type> <level>` command to learn spells from professors in Arwic
- Add the ability to view any dungeon map, with data courtesy of lifestoned.org
- Fix issue where AutoTrade/AutoVendor/ItemGiver was not respecting red loot rules for ObjectClass.Misc (summoning essences)
- Fix AutoVendor only selling stacks one at a time (still sells mmds one at a time)
- Fix AutoVendor selling until your inventory was full and not buying mmds on gdle
- Fix AutoVendor now stops when disabling it mid session

## 0.1.1 (2019-12-03) [UtilityBeltInstaller-0.1.1.exe](https://gitlab.com/utilitybelt/utilitybelt.gitlab.io/uploads/c94b459edb29649f99f1d84eae3f76ce/UtilityBeltInstaller-0.1.1.exe)
- Increase AutoStack/AutoCram timeout to 30s
- Add `Plugin.VideoPatchFocus`, to automatically enable/disable VideoPatch based on client focus
- Migrate Auto-Accept list to separate config so it can be shared
- Fix ProfileWatcher attempting to watch a null file and causing vtank to overwrite settings
- Fix Quest spam on login not properly being eaten
- Fix VTankVitalSharing not always sharing vitals
- Fix VisualNav routes not always redrawing properly
- Fix DungeonMaps Zoom now gets properly saved between sessions
- Fix DPI-scaling issue when /ub playsound is run
- Fix VideoPatchFocus on login, no longer needs to be fiddled with to work after relogging
- Fix Auto disable nametags/visualnav while videopatch is enabled.
- Add Dungeon name to dungeon maps display (Disable with DungeonMaps.DungeonName.Enabled)
- Fix Autotinker/imbue throwing an exception
- Add Opacity slider to Dungeon Maps window
- Fix UB now properly loads on new characters

## 0.1.0 (2019-11-30) [UtilityBeltInstaller-0.1.0.exe](https://gitlab.com/utilitybelt/utilitybelt.gitlab.io/uploads/d92c9e54fb61ab148bc2e1d2342ee204/UtilityBeltInstaller-0.1.0.exe)
- Added AutoTinker and AutoImbue (thanks Schneebly)
- Added `VisualNav.ScaleCurrentWaypoint` option to enable/disable scaling current waypoint using vTankMinNavDistance
- Added `/ub fixbusy` command to reset the client's Busy State and Busy Count both to 0.
- Added Beta builds are now available in #utilitybelt on https://discord.gg/c75pPaz
- Added resiliency to auto trade
- Added New UI!
- Added `Plugin.VideoPatch` setting to ammend/replace `/ub videopatch` command
- Added Equipment Manager for all your equipping needs
- Added negative Keep # functionality (gives away all but {Keep#} count items)
- Added setting `InventoryManager.TreatStackAsSingleItem` (currently only used in itemgiver)
- Added `/ub playsounds` to play mp3 files from command line
- Added `/ub pcap` to export pcap files from a rolling buffer
- Added ability to auto-generate equip profile based on currently equipped items
- Added `/ub autostack` to quickly:tm: stack all of the things
- Added `/ub autocram` to quickly:tm: cram all of the things in packs other than your main pack
- Added Re-enable autostack and autocram while vendoring, if enabled with AutoVendor.AutoStack and AutoVendor.AutoCram
- BROKE: `/ub jump` format has changed, use `/ub help jump` for more info
- BROKE: AutoSalvage finished think message has been changed.

## 0.0.14 (2019-11-15) [UtilityBeltInstaller-0.0.14.exe](https://gitlab.com/utilitybelt/utilitybelt.gitlab.io/uploads/0d29c14ccc27377941cb5f92c37ffaef/UtilityBeltInstaller-0.0.14.exe)
- Added VisualNav.Enabled setting
- Added `/ub playeroption <option> <on/true|off/false>` - command to set player options (thanks Yonneh)
- Added `/ub videopatch {enable,disable,toggle}` - online toggling of Mag's video patch (thanks Yonneh)
- Fixed bug with `/ub delay` causing vTank to barf
- Fixed bugs/crash with LootProfileWatcher (thanks Yonneh)
- Added ability to modify settings arrays from cli (thanks Cosmic Jester)

## 0.0.13 (2019-11-11) [UtilityBeltInstaller-0.0.13.exe](https://gitlab.com/utilitybelt/utilitybelt.gitlab.io/uploads/5502b71f708d32f45f5bef073d3759b7/UtilityBeltInstaller-0.0.13.exe)
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

## 0.0.12 (2019-11-03) [UtilityBeltInstaller-0.0.12.exe](https://gitlab.com/utilitybelt/utilitybelt.gitlab.io/uploads/6de18c1fe018bcd48e0620d25070db29/UtilityBeltInstaller-0.0.12.exe)
- Added `/ub vitae` command to check current vitae %
- Added Chat Logger (thanks Cosmic Jester)
- Added `/ub follow[p] [character|selected]` - follows the named character, selected, or closest (thanks Yonneh)
- Fixed autovendor bailing early when waiting on id requests (thanks Yonneh)
- Fixed splitting stacks when those stacks were already added to a vendor (thanks Yonneh)

## 0.0.11 (2019-10-27) [UtilityBeltInstaller-0.0.11.exe](https://gitlab.com/utilitybelt/utilitybelt.gitlab.io/uploads/eeffc7b5a32903ec241d894fbcc7e9af/UtilityBeltInstaller-0.0.11.exe)
- All new event-based autovendor code! (thanks Yonneh)
- Added AutoAddToTrade (thanks Cosmic Jester)
- Added `VTank.ShareVitals` option to enable/disable vtank vital sharing
- Fixed bug where InventoryManager settings were not displaying properly in the ui.
- Added `/ub closestportal with nav` blocking and retries (thanks Yonneh)
- Added `/ub portal[p] <portal name>` with nav blocking and retries (thanks Yonneh)
- `/ub count` now shows cumulative item count (thanks Cosmic Jester)
- Added /ig give[P(partial item name),r(regex)][p(partial character)] [count] <itemname> to <character> to give item(s) by name (thanks Yonneh)

## 0.0.10 (2019-10-13) [UtilityBeltInstaller-0.0.10.exe](https://gitlab.com/utilitybelt/utilitybelt.gitlab.io/uploads/0e26748b43ba78352b2c047f0cc0061a/UtilityBeltInstaller-0.0.10.exe)
- Customary remove debug spam release

## 0.0.9 (2019-10-13) [UtilityBeltInstaller-0.0.9.exe](https://gitlab.com/utilitybelt/utilitybelt.gitlab.io/uploads/f0ba230502e0e62969724d36f6a3d8ed/UtilityBeltInstaller-0.0.9.exe)
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

## 0.0.8 (2019-09-28) [UtilityBeltInstaller-0.0.8.exe](https://gitlab.com/utilitybelt/utilitybelt.gitlab.io/uploads/f1b2c542a7630438732e2553dec3a725/UtilityBeltInstaller-0.0.8.exe)
- Added experimental option to show embedded nav routes
- Fix potential exception in AutoVendor
- Check gitlab for updates on login
- Remove debug spam

## 0.0.7 (2019-09-27) [UtilityBeltInstaller-0.0.7.exe](https://gitlab.com/utilitybelt/utilitybelt.gitlab.io/uploads/caaf8eec17828a2fb60d7e20d8fba793/UtilityBeltInstaller-0.0.7.exe)
- Added option to only sell salvage from main pack
- Automatically disable and restore vtank auto cram/stack settings while vendoring
- Fix aetheria and autovendoring with red rules
- VTankFellowHeals performance and bugfixes
- Fix exception in certain routes causing VisualNav to stop drawing

## 0.0.6 (2019-09-27) [UtilityBeltInstaller-0.0.6.exe](https://gitlab.com/utilitybelt/utilitybelt.gitlab.io/uploads/764c570d2d65f563bcf7832bab7707ad/UtilityBeltInstaller-0.0.6.exe)
- Fix VTank location from registry
- Fix gem names so they don't prepend material names and end up like "Jet Jet"
- AutoSalvage performance
- Assessor performance

## 0.0.5 (2019-09-25) [UtilityBeltInstaller-0.0.5.exe](https://gitlab.com/utilitybelt/utilitybelt.gitlab.io/uploads/58ce3ed901ef81ad776a7e973f7ba704/UtilityBeltInstaller-0.0.5.exe)
- UB now passes all local character vitals to vTank, to support Healing/Restaming/Infusing all characters running on the same computer, regardless of being in a fellowship.

## 0.0.4 (2019-09-24) [UtilityBeltInstaller-0.0.4.exe](https://gitlab.com/utilitybelt/utilitybelt.gitlab.io/uploads/931df39dc0449bc0c5f05dcee59af12e/UtilityBeltInstaller-0.0.4.exe)
- Fixed hardcoded vtank profiles path :)
- VTank nav parser now only reads recordCount records, so it ignores trailing blank lines in the file

## 0.0.3 (2019-09-24) [UtilityBeltInstaller-0.0.3.exe](https://gitlab.com/utilitybelt/utilitybelt.gitlab.io/uploads/d41ff70af22e6874cf8d7b28a497546f/UtilityBeltInstaller-0.0.3.exe)
- Think when AutoSalvage bails due to open vendor (thanks Cosmic Jester)
- Visual VTank Nav Lines (like vi2 blue nav lines, but fully customizable and local)
- Hopefully fix issue where AutoVendor would get stuck with pyreals (instead of buying notes)
- Dont try to sell / salvage retained
- Added `/ub ig` ItemGiver command to give items matching a loot profile to another player (thanks Schneebly)
- Added `/ub jump` commands (thanks Schneebly)
- AutoVendor now requires id data for everything, to properly check for tinkered and retained. (TODO: inventory cache)
- AutoVendor will no longer sell inscribed items (thanks Cosmic Jester)

## 0.0.2 (2019-03-30) [UtilityBeltInstaller-0.0.2.exe](https://gitlab.com/utilitybelt/utilitybelt.gitlab.io/uploads/c37e201894413fa61bc2397a10cc3af5/UtilityBeltInstaller-0.0.2.exe)
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

## 0.0.1 (2019-02-26) [UtilityBeltyInstaller-0.0.1.exe](https://gitlab.com/utilitybelt/utilitybelt.gitlab.io/uploads/cab22eca3752a8c6d09eb9f95b6906b9/UtilityBeltInstaller-0.0.1.exe)
- Initial Release
