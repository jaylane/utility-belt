using System;
using System.Collections.Generic;
using System.IO;
using Decal.Adapter;
using Decal.Adapter.Wrappers;
using System.Text.RegularExpressions;
using System.Linq;

namespace UtilityBelt.Tools {
    class ItemGiver : IDisposable {

        private bool isRunning = false;
        private WorldObject currentItem;

        List<WorldObject> inventoryItems = new List<WorldObject>();
        List<int> blacklistedItems = new List<int>();
        List<WorldObject> giveObjects = new List<WorldObject>();
        List<WorldObject> givenItems = new List<WorldObject>();
        List<WorldObject> idItems = new List<WorldObject>();
        private DateTime lastThought = DateTime.MinValue;
        private DateTime lastScanUpdate = DateTime.MinValue;
        private DateTime lastAction = DateTime.MinValue;
        private bool disposed = false;
        bool doneScanning = false;
        private int destinationId;
        bool waitingForIds = false;

        private bool needsGiving = false;
        private object lootProfile;
        private DateTime lastIdSpam = DateTime.MinValue;
        private int retryCount;
        private double playerDistance;
        private string targetPlayer = "";
        private string utlProfile = "";
        private int giveSpeed = 300;
        int failedItems = 0;
        bool gaveItem = false;
        private DateTime stopGive;
        private DateTime startGive;

        public ItemGiver() {
            try {

                Directory.CreateDirectory(Path.Combine(Util.GetPluginDirectory(), "itemgiver"));

                CoreManager.Current.CommandLineText += new EventHandler<ChatParserInterceptEventArgs>(Current_CommandLineText);
                Globals.Core.ChatBoxMessage += new EventHandler<ChatTextInterceptEventArgs>(Current_ChatBoxMessage);
                Globals.Core.CharacterFilter.ChangePortalMode += new EventHandler<ChangePortalModeEventArgs>(WorldFilter_PortalChange);
                lastThought = DateTime.UtcNow;
            } catch (Exception ex) { Logger.LogException(ex); }
        }


        public void Think() {
            if (DateTime.UtcNow - lastScanUpdate > TimeSpan.FromSeconds(10) && idItems.Count > 0 ) {
                lastScanUpdate = DateTime.UtcNow;
                Util.WriteToChat("Items remaining to ID: " + idItems.Count());


            }
            

            if (DateTime.UtcNow - lastThought > TimeSpan.FromMilliseconds(giveSpeed) && needsGiving) {
                lastThought = DateTime.UtcNow;

                if (isRunning ) {
                    giveObjects = GetGiveItems();

                    foreach (WorldObject item in giveObjects) {
                        currentItem = item;
                        if (playerDistance > 5 || !needsGiving) {
                            Stop();
                            Util.WriteToChat("player is too far away");
                            return;
                        }

                        //if (!item.HasIdData) return;

                            if (!string.IsNullOrEmpty(item.Name) && !gaveItem) {
                                retryCount++;
                                CoreManager.Current.Actions.GiveItem(item.Id, destinationId);
                                if (retryCount > 10) {
                                    giveObjects.Remove(item);
                                    Util.WriteToChat("unable to give " + item.Name + " skipping....");
                                    failedItems++;
                                    retryCount = 0;
                                }
                            if (failedItems > 3) Stop();
                                return;
                            }

                            return;
                        }
                    //Util.WriteToChat("giveObjects count left: " + giveObjects.Count);
                    //Util.WriteToChat("idObjects count left: " + idItems.Count);

                    if (giveObjects.Count == 0 && idItems.Count == 0) {
                        Stop();
                    }

                    if (giveObjects.Count > 0) {
                        Util.WriteToChat("reached end of list... restarting at top");
                        return;
                    }
                }
                else {
                    Stop();
                }
            }
        }

        public static WorldObject GetLandscapeObject(string objectName) {
            WorldObject landscapeObject = null;

            foreach (WorldObject wo in CoreManager.Current.WorldFilter.GetLandscape()){
                if (wo.Name == objectName) {
                    landscapeObject = wo;
                }
            }
            return landscapeObject;
        }

        int FindPlayerID(string name) {
            // Exact match attempt first
                WorldObject playerObject = GetLandscapeObject(name);

                if (playerObject != null)
                    return playerObject.Id;

            return -1;
        }
        
        private static readonly Regex giveRegex = new Regex(@"\/ub ig (?<giveSpeed>\d+)?(?<utlProfile>.*) to (?<targetPlayer>.*)");
        private void Current_CommandLineText(object sender, ChatParserInterceptEventArgs e) {
            try {
                Match giveMatch = giveRegex.Match(e.Text);

                if (e.Text.StartsWith("/ub player ")) {
                    targetPlayer = e.Text.Replace("/ub player ", "").Trim();
                    destinationId = FindPlayerID(targetPlayer);
                    playerDistance = CoreManager.Current.WorldFilter.Distance(CoreManager.Current.CharacterFilter.Id, destinationId) * 240;

                    e.Eat =  true;

                    if (destinationId == -1) {
                        Util.WriteToChat(targetPlayer + " " + destinationId.ToString());
				    return ;
                    }
                    Util.WriteToChat(targetPlayer + " " + destinationId.ToString());
                    Util.WriteToChat(targetPlayer + " is " + playerDistance + " away");
                }
                if (e.Text.StartsWith("/ub ig abort") || e.Text.StartsWith("/ub ig stop")) {
                    e.Eat = true;
                    Stop();
                }
                if (e.Text.StartsWith("/ub scan inventory")) {
                    GetInventory();
                    e.Eat = true;
                }
                if (giveMatch.Success) {
                    utlProfile = giveMatch.Groups["utlProfile"].Value.Trim();
                    targetPlayer = giveMatch.Groups["targetPlayer"].Value.Trim();
                    destinationId = FindPlayerID(targetPlayer);
                    playerDistance = CoreManager.Current.WorldFilter.Distance(CoreManager.Current.CharacterFilter.Id, destinationId) * 240;
                        e.Eat = true;


                    if (targetPlayer == CoreManager.Current.CharacterFilter.Name) {
                        Util.WriteToChat("You can't give to yourself dumbass");
                        return;
                    }

                        string giveSpeedStr = giveMatch.Groups["giveSpeed"].Value.Trim();
                    
                    if(Int32.TryParse(giveSpeedStr,out int result) && !(string.IsNullOrEmpty(giveSpeedStr))) {
                        giveSpeed = int.Parse(giveSpeedStr);
                    }


                    if (destinationId == -1) {
                        Util.WriteToChat(targetPlayer + " " + destinationId.ToString());
                        return;
                    }

                    Util.WriteToChat(utlProfile);
                    
                    var pluginPath = Path.Combine(Util.GetPluginDirectory(), @"itemgiver");
                    var profilePath = Path.Combine(pluginPath, utlProfile);
                    Util.WriteToChat(profilePath.ToString());

                    if (!File.Exists(profilePath))
                        {
                        Util.WriteToChat("utl path: " + profilePath);
                        Util.WriteToChat("Profile does not exist: " + utlProfile.ToString());
                        return;
                        }

                    //C:\Users\Caleb\Documents\Decal Plugins\UtilityBelt\itemgiver\Electric Weapons.utl

                    var hasLootCore = false;
                    if (lootProfile == null) {
                        try {
                            lootProfile = new VTClassic.LootCore();
                            hasLootCore = true;
                        }
                        catch (Exception ex) { Logger.LogException(ex); }

                        if (!hasLootCore) {
                            Util.WriteToChat("Unable to load VTClassic, something went wrong.");
                            return;
                        }
                    }

                    if (isRunning) Util.WriteToChat("ItemGiver is already running.  Please wait until it completes or use /ub ig stop to quit previous session");

                    ((VTClassic.LootCore)lootProfile).LoadProfile(profilePath, false);

                    Start();
                }
                    
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }


        public void Current_ChatBoxMessage(object sender, ChatTextInterceptEventArgs e) {
            try {
                if (e.Text.StartsWith("You give ") && needsGiving && !gaveItem) {
                    gaveItem = true;
                    givenItems.Add(currentItem);
                    giveObjects.Remove(currentItem);
                    idItems.Remove(currentItem);
                    gaveItem = false;
                    retryCount = 0;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        void WorldFilter_PortalChange(object sender, ChangePortalModeEventArgs e) {
            Util.WriteToChat("portal changed");
        }

        public void Start() {
            startGive = DateTime.Now;
            isRunning = true;
            giveObjects = GetGiveItems();
            needsGiving = true;
            //WriteItemsToChat(giveObjects);
        }

        private void WriteItemsToChat(List<WorldObject> list) {
            foreach (WorldObject item in list) {
                Util.WriteToChat("Name: " + item.Name + "ID: " + item.Id + "Attuned: " + item.Values(LongValueKey.Attuned));
            }
        }

        private void Stop() {

            if (Globals.Config.AutoSalvage.Think.Value == true) {
                Util.Think("ItemGiver finished: " + utlProfile + " to " + targetPlayer);
            }
            else {
                Util.WriteToChat("ItemGiver complete.");
            }

            stopGive = DateTime.Now;
            TimeSpan duration = stopGive - startGive;
            Util.WriteToChat(stopGive.ToString());
            Util.WriteToChat(startGive.ToString());
            Util.WriteToChat("took " + duration.ToString() + " to complete");
            Reset();
            needsGiving = false;
            isRunning = false;
            targetPlayer = "";
            destinationId = 0;
            retryCount = 0;
            failedItems = 0;

        }

        public void Reset() {
            inventoryItems.Clear();
            giveObjects.Clear();
            givenItems.Clear();
            idItems.Clear();
        }

        private bool NeedsID(int id) {
            return uTank2.PluginCore.PC.FLootPluginQueryNeedsID(id);
        }

        //this only runs when using /ub scan inventory
        private void GetInventory() {
            try {
                //inventoryItems.Clear();
                //if (!idsRequested) {
                    foreach (WorldObject item in CoreManager.Current.WorldFilter.GetInventory()) {
                
                        // If the item is equipped or wielded, don't process it.
                        if (item.Values(LongValueKey.EquippedSlots, 0) > 0 || item.Values(LongValueKey.Slot, -1) == -1)
                            continue;

                        // If the item is equipped or wielded, don't process it.
                        if (item.Values(LongValueKey.Attuned) > 0)
                            continue;

                        if (inventoryItems.Contains(item)) continue;

                        // Convert the item into a VT GameItemInfo object
                        uTank2.LootPlugins.GameItemInfo itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithID(item.Id);

                        if (itemInfo == null) {
                            // This happens all the time for aetheria that has been converted
                            Util.WriteToChat(item.Name + " has no data");
                            continue;
                        }

                        if (((VTClassic.LootCore)lootProfile).DoesPotentialItemNeedID(itemInfo)) {
                            CoreManager.Current.Actions.RequestId(item.Id);
                            Util.WriteToChat("VTANK - gathering info for " + item.Name);
                        //}

                        //idsRequested = true;
                        inventoryItems.Add(item);
                    }
                    Util.WriteToChat("scanned items: " + inventoryItems.Count());
                }

                
            } catch (Exception ex) { Logger.LogException(ex); }

        }

            private List<WorldObject> GetGiveItems() {
            
            foreach (WorldObject item in CoreManager.Current.WorldFilter.GetInventory()) {

                // If the item is equipped or wielded, don't process it.
                if (item.Values(LongValueKey.EquippedSlots, 0) > 0 || item.Values(LongValueKey.Slot, -1) == -1)
                    continue;

                // If the item is equipped or wielded, don't process it.
                //if (item.Values(LongValueKey.Attuned) > 0)
                //    continue;

                uTank2.LootPlugins.GameItemInfo itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithID(item.Id);

                if (itemInfo == null) {
                    // This happens all the time for aetheria that has been converted
                    continue;
                }

                if (givenItems.Contains(item))
                    continue;

                if (giveObjects.Contains(item))
                    continue;

                if (!((VTClassic.LootCore)lootProfile).DoesPotentialItemNeedID(itemInfo) && idItems.Contains(item)) {
                    //Util.WriteToChat("GATHERED INFO FOR " + item.Name);
                    idItems.Remove(item);
                }

                if (((VTClassic.LootCore)lootProfile).DoesPotentialItemNeedID(itemInfo)) {
                    CoreManager.Current.Actions.RequestId(item.Id);
                    //Util.WriteToChat("VTANK - gathering info for " + item.Name);
                    if (!idItems.Contains(item)) {
                        //Util.WriteToChat(item.Name.ToString());
                        idItems.Add(item);
                    }
                    continue;
                } 

                //if (idItems.Count < 10) {
                //    foreach (WorldObject wo in idItems) {
                //        Util.WriteToChat(wo.Name.ToString());
                //    }
                //}


                //ub ig Electric Weapons.utl to Schneebly Tinker
                //Util.WriteToChat("made it to loot decision");
                uTank2.LootPlugins.LootAction result = ((VTClassic.LootCore)lootProfile).GetLootDecision(itemInfo);
                if (!result.IsKeep) {
                    continue;
                }

                //var result2 = uTank2.PluginCore.PC.FLootPluginClassifyImmediate(item.Id);
                //if (!result2.IsKeep) {
                //    continue;
                //}

                //Util.WriteToChat("VTANK - have info for " + item.Name);
                Util.WriteToChat("adding object: " + item.Name);
                giveObjects.Add(item);

                //inventoryItems.Add(item);

                //if (!((VTClassic.LootCore)lootProfile).DoesPotentialItemNeedID(itemInfo)) {
                //    uTank2.LootPlugins.LootAction result = ((VTClassic.LootCore)lootProfile).GetLootDecision(itemInfo);
                //
                //    if (!result.IsKeep) {
                //        continue;
                //    }
                //
                //    giveObjects.Add(item);
                //}
                //else {
                //    CoreManager.Current.Actions.RequestId(item.Id);
                //}
            }
            return giveObjects;
        }


        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    Globals.Core.CommandLineText -= Current_CommandLineText;
                    CoreManager.Current.CommandLineText -= new EventHandler<ChatParserInterceptEventArgs>(Current_CommandLineText);
                    Globals.Core.ChatBoxMessage -= new EventHandler<ChatTextInterceptEventArgs>(Current_ChatBoxMessage);
                    Globals.Core.CharacterFilter.ChangePortalMode -= new EventHandler<ChangePortalModeEventArgs>(WorldFilter_PortalChange);
                }
                disposed = true;
            }
        }
    }
}


