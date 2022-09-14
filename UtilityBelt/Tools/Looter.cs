using System;
using Decal.Adapter;
using System.Collections.Generic;
using System.Linq;
using UtilityBelt.Lib;
using uTank2;
using Decal.Adapter.Wrappers;
using UBService.Lib.Settings;
using Decal.Interop.Input;
using Timer = Decal.Interop.Input.Timer;
using UBHelper;


namespace UtilityBelt.Tools {
    [Name("Looter")]
    [Summary("Loot functionality from chests and your own corpse.")]
    [FullDescription(@"
<span style='color:red'>I **highly** suggest double checking your active vtank loot profile before using looter, especially if utilizing the autosalvage functionality.  It will only salvage items it loots, but is not forgiving.</span>

### How to use

* Modify the Looter settings as needed, they are pretty self explanatory.
* Open a chest manually and let looter do it's thing.  This is recommended before using the /ub use command on a chest or corpse.
* If you want, use the /ub use <name> on a chest or corpse.  This will open the chest, loot the items that match the current vtank profile and **close the chest once complete**.

### Info

* Looter uses the active vtank loot profile
* It is capable of looting the following:
  - Chests
  - Your own corpse
  - Monster corpses if /ub use command was used.
* Supported rule actions: Keep, Keep #, and Salvage
  - Keep (loot all matching items).
  - Keep # (loot # of this item)
  - Salvage (loot and salvage items matching this rule)
* Red loot rules are supported (the ones that need id data)
* Jump looting works best when client has focus or at a force higher fps.  It will not try to jump over 200 burden or under 5 stamina. **Does not work on GDLE**
* Jump height is customizable and the container will close if you jump too high.
* Autosavage will only work if /ub use command or the UI was used.
* Autosalvage will only attempt to salvage items that it looted from the targetted container. It does not currently combine bags.
* Things that it *won't* do:
  - Give you better loot
  - Open your corpse for you
    ")]



    public class Looter : ToolBase {

        private enum itemstate { None = 0x0001, InContainer = 0x0002, RequestingInfo = 0x0004, NeedsToBeLooted = 0x0008, Ignore = 0x0016, Looted = 0x0032, Blacklisted = 0x0064 }

        private enum looterstate { Closed = 0x0001, Unlocking = 0x0002, Locked = 0x0004, Unlocked = 0x0008, Opening = 0x00016, Open = 0x0032, Looting = 0x0064, Closing = 0x0128, Salvaging = 0x0256, Done = 0x0512 }

        private enum runtype { None = 0x0001, UBUse = 0x0002, UI = 0x0004 }
        private enum containertype { Chest = 0x0001, MyCorpse = 0x0002, MonsterCorpse = 0x0004, Unknown = 0x0008 }

        private Dictionary<int, itemstate> containerItems = new Dictionary<int, itemstate>();
        bool dispatchEnabled = false;
        public event EventHandler LooterFinished;
        public event EventHandler LooterFinishedForceStop;
        private bool UbOwnedContainer = false;
        private int targetContainerID = 0;
        private int targetKeyID = 0;
        private string targetKeyName = "";
        private looterstate looterState = looterstate.Done;
        private runtype runType = runtype.None;
        private int lootAttemptCount = 0;
        private containertype containerType = containertype.Unknown;
        private DateTime lastAttempt = DateTime.UtcNow;
        private DateTime salvageDelay = DateTime.UtcNow;

        //private int lastOpenContainer = 0;

        private int unlockAttempt = 0;
        private int openAttempt = 0;


        private DateTime startTime = DateTime.MinValue;

        //private looterstate lastLooterState = looterstate.Closed;

        private List<int> containerItemsList = new List<int>();

        private bool needToSalvage = false;
        private bool inAir = false;
        private int jumpCount = 0;
        private bool jumping = false;

        private TimerClass baseTimer;

        private bool salvaging = false;
        private TimerClass salvageDelayTimer;


        [Summary("Enabled")]
        public Setting<bool> Enabled = new Setting<bool>(false);

        [Summary("Enable the looting of chests")]
        public Setting<bool> EnableChests = new Setting<bool>(false);

        [Summary("Enable the looting of your corpse.  This will not open your corpse for you.")]
        public Setting<bool> EnableMyCorpse = new Setting<bool>(false);

        [Summary("Block melee/missile attacks while looting your own corpse.  If left off, you may attack and run away from your corpse while looting.")]
        public Setting<bool> BlockVtankMelee = new Setting<bool>(false);

        [Summary("Jump when looting.  This only applies when there is more than one item scanned and needs to be looted. Do *not* use this on GDLE.")]
        public Setting<bool> JumpWhenLooting = new Setting<bool>(false);

        [Summary("Jump height. Full bar is 1000)")]
        public Setting<int> JumpHeight = new Setting<int>(100);

        [Summary("Number of chest open attempts before quitting.")]
        public Setting<int> MaxOpenAttempts = new Setting<int>(10);

        [Summary("Number of chest unlock attempts before quitting.")]
        public Setting<int> MaxUnlockAttempts = new Setting<int>(10);

        [Summary("Number of loot attempts before blacklisting item.  This applies per item.")]
        public Setting<int> AttemptsBeforeBlacklisting = new Setting<int>(500);

        [Summary("Autosalvage after looting (only applies when using /ub open(p) <item> and will not run when looting your own corpse).")]
        public Setting<bool> AutoSalvageAfterLooting = new Setting<bool>(false);

        [Summary("Delay associated with unlocking, opening and closing a chest in milliseconds.  Increase this number if issues start to occur")]
        public Setting<int> DelaySpeed = new Setting<int>(1000);

        [Summary("Overall speed of looter in milliseconds (approximate)")]
        public Setting<int> OverallSpeed = new Setting<int>(60);

        [Summary("Test mode")]
        public Setting<bool> TestMode = new Setting<bool>(true);

        public Looter(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        private void EnableBaseTimer() {
            try {
                if (baseTimer == null) {
                    baseTimer = new TimerClass();
                    baseTimer.Timeout += BaseTimer_Timeout;
                    baseTimer.Start(OverallSpeed);
                }
            }
            catch (Exception ex) {
                Logger.LogException(ex);
            }
        }


        private void EnableDispatch() {
            try {
                if (!dispatchEnabled) {
                    UB.Core.EchoFilter.ServerDispatch += EchoFilter_ServerDispatch;
                    UB.Core.EchoFilter.ClientDispatch += EchoFilter_ClientDispatch;
                    dispatchEnabled = true;
                }
            }
            catch (Exception ex) {
                Logger.LogException(ex);
            }
        }

        private bool IsValidContainer(int chest) {
            UB.Assessor.Request(chest);
            WorldObject chestWO = UB.Core.WorldFilter[chest];
            if (chestWO.ObjectClass != ObjectClass.Container && chestWO.ObjectClass != ObjectClass.Corpse) {
                return false;
            }
            if (chestWO.Values(LongValueKey.ItemSlots) < 48) {
                return false;
            }
            return true;
        }

        public override void Init() {
            try {
                base.Init();
                UtilityBeltPlugin.Instance.Looter.Changed += Looter_Changed;
                if (UBHelper.Core.GameState != UBHelper.GameState.In_Game) {
                    UB.Core.CharacterFilter.LoginComplete += CharacterFilter_LoginComplete;
                }
                else {
                    if (Enabled) {
                        EnableDispatch();
                    }
                }
            }
            catch (Exception ex) {
                Logger.LogException(ex);
            }
        }

        private void CharacterFilter_LoginComplete(object sender, EventArgs e) {
            if (UtilityBeltPlugin.Instance.Looter.Enabled) {
                EnableDispatch();
            }
            UB.Core.CharacterFilter.LoginComplete -= CharacterFilter_LoginComplete;
        }

        private void Looter_Changed(object sender, SettingChangedEventArgs e) {
            try {
                if (OverallSpeed < 30) {
                    Logger.WriteToChat("OverallSpeed under 30 is not allowed.");
                    OverallSpeed.Value = 30;
                }
                if (UtilityBeltPlugin.Instance.Looter.Enabled) {
                    EnableDispatch();
                    return;
                }
                else {
                    UB.Core.EchoFilter.ServerDispatch -= EchoFilter_ServerDispatch;
                    UB.Core.EchoFilter.ClientDispatch -= EchoFilter_ClientDispatch;
                    dispatchEnabled = false;
                }

            }
            catch (Exception ex) {
                Logger.LogException(ex);
            }
        }


        private void EchoFilter_ClientDispatch(object sender, Decal.Adapter.NetworkMessageEventArgs e) {
            try {
                switch (e.Message.Type) {
                    case 0xF7B1:
                        switch (e.Message.Value<int>("action")) {
                            case 0x0019:
                                int itemIntoContainer = e.Message.Value<int>("item");
                                if (containerItems.ContainsKey(itemIntoContainer)) {
                                    lootAttemptCount++;
                                }
                                break;
                            case 0xF61B: //packet for jumping in the air - GDLE
                                break;
                        }
                        break;
                }
            }
            catch (Exception ex) {
                Logger.LogException(ex);
            }
        }

        private void EchoFilter_ServerDispatch(object sender, Decal.Adapter.NetworkMessageEventArgs e) {
            try {
                switch (e.Message.Type) {
                    case 0xF7B0:
                        switch (e.Message.Value<int>("event")) {
                            case 0x0196: //Item_OnViewContents - when chest is opened it sees all the items in chest
                                if (Enabled || runType == runtype.UI) {
                                    int containerID = e.Message.Value<int>("container");
                                    var itemCount = e.Message.Value<int>("itemCount");
                                    var items = e.Message.Struct("items"); //vector for chest contents
                                    if (itemCount <= 0) return; //return if chest is empty
                                    if (containerID == 0) return; //return if containerID is 0
                                    if (!IsValidContainer(containerID)) return;
                                    containerType = GetContainerType(containerID);

                                    //return if container isn't enabled in settings
                                    if (containerType == containertype.Unknown) return;
                                    if (containerType == containertype.Chest && !EnableChests && runType != runtype.UI) return;
                                    if (containerType == containertype.MyCorpse && !EnableMyCorpse) return;
                                    if (containerType == containertype.MonsterCorpse && targetContainerID == 0) return;

                                    //past returns, now we do things
                                    //Logger.Debug("*********STARTING TO LOOT************");
                                    startTime = DateTime.UtcNow;
                                    targetContainerID = containerID;
                                    looterState = looterstate.Open;

                                    int woid = 0;
                                    for (int i = 0; i < itemCount; i++) { //add world object id's within chest to itemstoid list
                                        itemstate state = itemstate.None;
                                        woid = items.Struct(i).Value<int>("item");
                                        if (containerType != containertype.MyCorpse) {
                                            state = itemstate.InContainer;
                                        }
                                        else {
                                            state = itemstate.NeedsToBeLooted;
                                        }
                                        UpdateContainerItems(woid, state);
                                    }
                                    EnableBaseTimer();
                                }
                                else {
                                    looterState = looterstate.Open;
                                    DoneLooting(false, false);
                                }
                                break;
                            case 0x0052: //close container
                                if (UbOwnedContainer && looterState == looterstate.Closing && e.Message.Value<int>("object") == targetContainerID) {
                                    looterState = looterstate.Closed;
                                }
                                break;
                            case 0x01C7: //Item_UseDone (Failure Type)
                                //1199 WERROR_LOCK_ALREADY_UNLOCKED
                                //1201 WERROR_LOCK_ALREADY_OPEN
                                if (e.Message.Value<int>("unknown") == 1199) {
                                    looterState = looterstate.Unlocked;
                                }
                                else if (e.Message.Value<int>("unknown") == 1201) {
                                    if (UB.Core.Actions.OpenedContainer == targetContainerID) {
                                        looterState = looterstate.Open;
                                    }
                                }
                                break;
                            case 0x0022: //Item_ServerSaysContainID (moved object)
                                int movedObjectId = e.Message.Value<int>("item");
                                int movedContainerId = e.Message.Value<int>("container");
                                if (containerItems.ContainsKey(movedObjectId) && IsContainerPlayer(movedContainerId)) {
                                    LootedItem(movedObjectId);
                                }
                                break;
                        }
                        break;
                    case 0x0024: //object deleted, this happens when looting a stacked item
                        int removedObjectId = e.Message.Value<int>("object");
                        LootedItem(removedObjectId);
                        break;
                    case 0xF74E: //packet for jumping in the air - ACE
                        int me = e.Message.Value<int>("object");
                        if ((Enabled || runType == runtype.UI) && me == UB.Core.CharacterFilter.Id && !inAir && targetContainerID != 0) {
                            inAir = true;
                            jumpCount++;
                        }
                        break;
                    case 0xF750: //chest unlocked/locked (147/148)
                        int lockedObjID = e.Message.Value<int>("object");
                        int lockedObjIDEffect = e.Message.Value<int>("effect");
                        if (lockedObjID == targetContainerID) {
                            if (lockedObjIDEffect == 148) {
                                if (looterState == looterstate.Closing) {
                                    looterState = looterstate.Closed;
                                }
                                else {
                                    looterState = looterstate.Locked;
                                }

                            }
                            else if (lockedObjIDEffect == 147) {
                                looterState = looterstate.Unlocked;
                            }
                        }
                        break;
                    case 0x02D2: // Qualities_UpdateBool
                        int chestID = e.Message.Value<int>("object");
                        int lockedStatus = e.Message.Value<int>("value");
                        break;
                }
            }
            catch (Exception ex) {
                Logger.LogException(ex);
            }
        }

        private void DoneLooting(bool writeChat = true, bool force = false) {
            try {
                var itemsInContainer = containerItems.Where(x => x.Value == itemstate.InContainer);
                var needsToBeLootedItems = containerItems.Where(x => x.Value == itemstate.NeedsToBeLooted);
                var lootedItems = containerItems.Where(x => x.Value == itemstate.Looted);
                var blacklistedItems = containerItems.Where(x => x.Value == itemstate.Blacklisted);
                var ignoredItems = containerItems.Where(x => x.Value == itemstate.Ignore);
                var requestingInfoItems = containerItems.Where(x => x.Value == itemstate.RequestingInfo);

                if (writeChat) {
                    //Logger.WriteToChat("*********************DONE LOOTING*****************************");
                    if (needsToBeLootedItems.Count() + blacklistedItems.Count() <= 0) {
                        Logger.WriteToChat("Looter completed in " + Util.GetFriendlyTimeDifference(DateTime.UtcNow - startTime).ToString() + " Scanned " + containerItems.Count() + " items and looted " + lootedItems.Count() + " items");
                    }
                    else {
                        Logger.WriteToChat("Looter completed in " + Util.GetFriendlyTimeDifference(DateTime.UtcNow - startTime).ToString() + " --- Scanned " + containerItems.Count() + " items and looted " + lootedItems.Count() + " items --- Failed to loot " + (needsToBeLootedItems.Count() + blacklistedItems.Count()).ToString() + " items");
                    }
                }
                UnLockVtankSettings();

                //lastOpenContainer = 0;

                containerItems.Clear();
                containerItemsList.Clear();

                looterState = looterstate.Done;
                //lastLooterState = looterstate.Done;
                UbOwnedContainer = false;
                lootAttemptCount = 0;

                needToSalvage = false;


                unlockAttempt = 0;
                openAttempt = 0;

                targetKeyID = 0;
                containerType = containertype.Unknown;

                startTime = DateTime.MinValue;

                inAir = false;
                jumpCount = 0;
                jumping = false;

                lastAttempt = DateTime.UtcNow;
                salvageDelay = DateTime.UtcNow;

                //if (salvaging) salvageDelayTimer.Timeout -= SalvageDelay_Timeout;
                salvaging = false;


                if (salvageDelayTimer != null) {
                    salvageDelayTimer.Stop();
                    salvageDelayTimer = null;
                }


                if (runType == runtype.UI && HasMoreKeys(targetKeyName)) {
                    //if (writeChat) Logger.WriteToChat("*******END OF DONE LOOTING RESTARTING********");
                    StartUI(targetContainerID, targetKeyName);
                }
                else {
                    //if (writeChat) Logger.WriteToChat("*******END OF DONE LOOTING********");
                    targetKeyName = "";
                    targetContainerID = 0;
                    runType = runtype.None;
                    if (baseTimer != null) {
                        baseTimer.Stop();
                        baseTimer = null;
                    }
                    if (!Enabled && dispatchEnabled) {
                        UB.Core.EchoFilter.ServerDispatch -= EchoFilter_ServerDispatch;
                        UB.Core.EchoFilter.ClientDispatch -= EchoFilter_ClientDispatch;
                        dispatchEnabled = false;
                    }
                    LooterFinishedForceStop?.Invoke(this, EventArgs.Empty);
                }

                if (force) {
                    //if (writeChat) Logger.WriteToChat("*******END OF DONE LOOTING - FORCE STOP********");
                    LooterFinishedForceStop?.Invoke(this, EventArgs.Empty);
                    targetKeyName = "";
                    targetContainerID = 0;
                    runType = runtype.None;
                    if (baseTimer != null) {
                        baseTimer.Stop();
                        baseTimer = null;
                    }
                    if (!Enabled && dispatchEnabled) {
                        UB.Core.EchoFilter.ServerDispatch -= EchoFilter_ServerDispatch;
                        UB.Core.EchoFilter.ClientDispatch -= EchoFilter_ClientDispatch;
                        dispatchEnabled = false;
                    }
                }


            }

            catch (Exception ex) {
                Logger.LogException(ex);
            }
        }

        private bool HasMoreKeys(string key) {
            if (Util.GetItemCountInInventoryByName(key) <= 0) {
                return false;
            }
            return true;
        }
        private bool IsContainerPlayer(int container) {
            if (container == 0) return false;
            if (UB.Core.WorldFilter[container].Id == UB.Core.CharacterFilter.Id) { // main pack
                return true;
            }
            if (UB.Core.WorldFilter[container].Container == UB.Core.CharacterFilter.Id) { // side pack
                return true;
            }
            return false;
        }

        private void UpdateContainerItems(int item, itemstate state) {
            try {
                if (!containerItems.ContainsKey(item)) {
                    containerItems.Add(item, state);
                }
                if (!containerItemsList.Contains(item)) {
                    containerItemsList.Add(item);
                }

            }
            catch (Exception ex) {
                Logger.LogException(ex);
            }
        }

        private containertype GetContainerType(int container) {
            try {
                switch (UB.Core.WorldFilter[container].ObjectClass) {
                    case ObjectClass.Container:
                        return containertype.Chest;
                    case ObjectClass.Corpse:
                        if (UB.Core.WorldFilter[container].Name == string.Concat("Corpse of ", UB.Core.CharacterFilter.Name)) {
                            return containertype.MyCorpse;
                        }
                        else {
                            return containertype.MonsterCorpse;
                        }
                }
                return containertype.Unknown;
            }
            catch (Exception ex) {
                Logger.LogException(ex);
                return containertype.Unknown;
            }
        }

        private void GetLootDecision(int item) {
            bool waitingForInvItems = false;
            if (!UB.Core.Actions.IsValidObject(item)) {
                return;
            }
            int itemCount = 0;
            uTank2.LootPlugins.GameItemInfo itemInfo = uTank2.PluginCore.PC.FWorldTracker_GetWithID(item);

            if (!uTank2.PluginCore.PC.FLootPluginQueryNeedsID(item)) {

                var result = uTank2.PluginCore.PC.FLootPluginClassifyImmediate(item);
                if (result.IsKeep) {
                    containerItems[item] = itemstate.NeedsToBeLooted;
                }
                if (result.IsKeepUpTo) { //iskeepid has to request id's for red rules in profiles in order to work properly
                    var inv = CoreManager.Current.WorldFilter.GetInventory();
                    foreach (WorldObject invItem in inv) {
                        waitingForInvItems = false;
                        if (uTank2.PluginCore.PC.FLootPluginQueryNeedsID(invItem.Id)) {
                            UB.Assessor.Request(invItem.Id);
                            waitingForInvItems = true;
                            break;
                        }
                        else {
                            var invItemResult = uTank2.PluginCore.PC.FLootPluginClassifyImmediate(invItem.Id);
                            if (result.RuleName == invItemResult.RuleName) {
                                if (invItem.Values(LongValueKey.StackMax, 0) > 0) {
                                    itemCount += invItem.Values(LongValueKey.StackCount, 1);
                                }
                                else {
                                    itemCount++;
                                }
                            }
                        }
                    }
                    inv.Dispose();
                    if (!waitingForInvItems) {
                        if (itemCount >= result.Data1) {
                            containerItems[item] = itemstate.Ignore;
                        }
                        else {
                            containerItems[item] = itemstate.NeedsToBeLooted;
                        }
                    }
                }
                if (result.IsSalvage) {
                    containerItems[item] = itemstate.NeedsToBeLooted;
                    needToSalvage = true;
                }
                if (result.IsNoLoot) {
                    containerItems[item] = itemstate.Ignore;
                }

                if (containerItems[item] == itemstate.NeedsToBeLooted && UtilityBeltPlugin.Instance.ItemDescriptions.DescribeOnLoot)
                    UtilityBeltPlugin.Instance.ItemDescriptions.DisplayItem(item, itemInfo, result.RuleName, true);

                if (TestMode)
                    containerItems[item] = itemstate.Looted;
            }
            else {
                if (containerItems[item] != itemstate.RequestingInfo) {
                    UB.Assessor.Request(item);
                    UB.Core.Actions.RequestId(item);
                    containerItems[item] = itemstate.RequestingInfo;
                }
            }
        }

        public void StartUI(int container, string key) {
            try {
                //Logger.WriteToChat("*******START OF UI USING KEY******");
                targetContainerID = container;
                targetKeyName = key;
                int keyCount = Util.GetItemCountInInventoryByName(key);
                if (keyCount <= 0) {
                    Logger.WriteToChat("Looter: O/ut of keys to use");
                    LooterFinished?.Invoke(this, EventArgs.Empty);
                    DoneLooting(false, true);
                    return;
                }
                targetKeyID = Util.FindInventoryObjectByName(key).Id;
                EnableDispatch();
                EnableBaseTimer();
                looterState = looterstate.Unlocking;
                lastAttempt = DateTime.UtcNow;
                runType = runtype.UI;
                UbOwnedContainer = true;
            }
            catch (Exception ex) {
                Logger.LogException(ex);
            }
        }

        public void StopLooter() {
            try {
                //uiRunning = false;
                DoneLooting(false, true);
                //UB.Plugin.UB_UseItem(key, Util.WOSearchFlags.Inventory, false, null);
            }
            catch (Exception ex) {
                Logger.LogException(ex);
            }
        }


        private void LootedItem(int item) {
            try {
                if (containerItems.ContainsKey(item)) {
                    if (containerItems[item] != itemstate.Looted) {
                        lootAttemptCount = 0;
                        containerItems[item] = itemstate.Looted;
                    }
                }
            }
            catch (Exception ex) {
                Logger.LogException(ex);
            }
        }

        private void LockVtankSettings() {
            if (!UBHelper.vTank.Instance.Decision_IsLocked(ActionLockType.Navigation)) {
                UBHelper.vTank.Decision_Lock(ActionLockType.Navigation, TimeSpan.FromMilliseconds(30000));
            }
            if (!UBHelper.vTank.Instance.Decision_IsLocked(ActionLockType.VoidSpellLockedOut)) {
                UBHelper.vTank.Decision_Lock(ActionLockType.VoidSpellLockedOut, TimeSpan.FromMilliseconds(30000));
            }
            if (!UBHelper.vTank.Instance.Decision_IsLocked(ActionLockType.WarSpellLockedOut)) {
                UBHelper.vTank.Decision_Lock(ActionLockType.WarSpellLockedOut, TimeSpan.FromMilliseconds(30000));
            }
            if (BlockVtankMelee && !UBHelper.vTank.Instance.Decision_IsLocked(ActionLockType.MeleeAttackShot)) {
                UBHelper.vTank.Decision_Lock(ActionLockType.MeleeAttackShot, TimeSpan.FromMilliseconds(30000));
            }
            if (!UBHelper.vTank.Instance.Decision_IsLocked(ActionLockType.ItemUse)) {
                UBHelper.vTank.Decision_Lock(ActionLockType.ItemUse, TimeSpan.FromMilliseconds(30000));
            }
        }

        private void UnLockVtankSettings() {
            if (UBHelper.vTank.Instance.Decision_IsLocked(ActionLockType.Navigation)) {
                UBHelper.vTank.Decision_UnLock(ActionLockType.Navigation);
            }
            if (UBHelper.vTank.Instance.Decision_IsLocked(ActionLockType.VoidSpellLockedOut)) {
                UBHelper.vTank.Decision_UnLock(ActionLockType.VoidSpellLockedOut);
            }
            if (UBHelper.vTank.Instance.Decision_IsLocked(ActionLockType.WarSpellLockedOut)) {
                UBHelper.vTank.Decision_UnLock(ActionLockType.WarSpellLockedOut);
            }
            if (UBHelper.vTank.Instance.Decision_IsLocked(ActionLockType.MeleeAttackShot)) {
                UBHelper.vTank.Decision_UnLock(ActionLockType.MeleeAttackShot);
            }
            if (UBHelper.vTank.Instance.Decision_IsLocked(ActionLockType.ItemUse)) {
                UBHelper.vTank.Decision_UnLock(ActionLockType.ItemUse);
            }
        }

        private bool CanIJump() {
            if (jumpCount > 3) return false;
            if (UB.Core.CharacterFilter.Stamina <= 5) return false;
            if (Util.GetFriendlyBurden() > 200) return false;
            return true;
        }

        public void OpenContainer(int chest) {
            try {
                targetContainerID = chest;
                EnableBaseTimer();
                if (runType == runtype.None) runType = runtype.UBUse;
                UbOwnedContainer = true;
                looterState = looterstate.Opening;
            }
            catch (Exception ex) {
                Logger.LogException(ex);
            }
        }

        private void BaseTimer_Timeout(Timer Source) {
            try {

                //if (lastOpenContainer != UB.Core.Actions.OpenedContainer) {
                //    Logger.WriteToChat("lastOpenContainer changed from " + lastOpenContainer + " to " + UB.Core.Actions.OpenedContainer);
                //    
                //    lastOpenContainer = UB.Core.Actions.OpenedContainer;
                //}
                //
                //if (lastLooterState != looterState) {
                //    Logger.WriteToChat("looterState changed from " + lastLooterState + " to " + looterState);
                //    lastLooterState = looterState;
                //}

                switch (looterState) {
                    case looterstate.Closed:
                        if (UbOwnedContainer) {
                            if (AutoSalvageAfterLooting && needToSalvage && containerType != containertype.MyCorpse) {
                                salvageDelay = DateTime.UtcNow;
                                looterState = looterstate.Salvaging;
                            }
                            else {
                                looterState = looterstate.Done;
                                DoneLooting();
                            }
                        }
                        else {
                            looterState = looterstate.Done;
                        }
                        break;
                    case looterstate.Locked:
                        if (runType == runtype.UI) {
                            looterState = looterstate.Unlocking;
                        }
                        else if (runType == runtype.UBUse) {
                            WriteToChat("Chest is locked... bailing");
                            DoneLooting(false, false);
                        }
                        break;
                    case looterstate.Unlocked:
                        looterState = looterstate.Opening;
                        lastAttempt = DateTime.UtcNow;
                        break;
                    case looterstate.Unlocking:
                        if (DateTime.UtcNow - lastAttempt >= TimeSpan.FromMilliseconds(DelaySpeed)) {
                            if (unlockAttempt >= MaxUnlockAttempts) {
                                Logger.WriteToChat("Reached max number of unlock attempts");
                                DoneLooting(false, false);
                            }
                            lastAttempt = DateTime.UtcNow;
                            UB.Core.Actions.SelectItem(targetKeyID);
                            UB.Core.Actions.ApplyItem(targetKeyID, targetContainerID);
                            unlockAttempt++;
                        }
                        break;
                    case looterstate.Open:
                        looterState = looterstate.Looting;
                        break;
                    case looterstate.Opening:
                        if ((!Enabled && runType != runtype.UI) && UB.Core.Actions.OpenedContainer == targetContainerID) {
                            DoneLooting(false, false);
                        }
                        if (DateTime.UtcNow - lastAttempt >= TimeSpan.FromMilliseconds(DelaySpeed)) {
                            if (openAttempt >= MaxOpenAttempts) {
                                Logger.WriteToChat("Reached max number of opening attempts");
                                DoneLooting(false, false);
                            }
                            lastAttempt = DateTime.UtcNow;
                            UB.Core.Actions.UseItem(targetContainerID, 0);
                            openAttempt++;
                        }
                        break;
                    case looterstate.Looting:
                        var freeSlots = (new Weenie(UB.Core.CharacterFilter.Id)).FreeSpace;
                        var itemsInContainer = containerItems.Where(x => x.Value == itemstate.InContainer);
                        var needsToBeLootedItems = containerItems.Where(x => x.Value == itemstate.NeedsToBeLooted);
                        var lootedItems = containerItems.Where(x => x.Value == itemstate.Looted);
                        var blacklistedItems = containerItems.Where(x => x.Value == itemstate.Blacklisted);
                        var ignoredItems = containerItems.Where(x => x.Value == itemstate.Ignore);
                        var requestingInfoItems = containerItems.Where(x => x.Value == itemstate.RequestingInfo);

                        if (itemsInContainer.Count() > 0) {
                            GetLootDecision(itemsInContainer.First().Key);
                        }
                        else if (requestingInfoItems.Count() > 0) {
                            GetLootDecision(requestingInfoItems.First().Key);
                        }

                        foreach (int item in needsToBeLootedItems.Select(w => w.Key).ToList()) {
                            if (containerType == containertype.MyCorpse) {
                                LockVtankSettings();
                            }
                            if (JumpWhenLooting && CanIJump()) {

                                if (requestingInfoItems.Count() == 0 && containerItems.Where(x => x.Value != itemstate.InContainer).Count() == containerItems.Count()) {

                                    if (needsToBeLootedItems.Count() == 1) {
                                        UB.Core.Actions.MoveItem(item, UB.Core.CharacterFilter.Id);
                                        if (needsToBeLootedItems.First().Key == item) {
                                            lootAttemptCount++;
                                        }
                                    }
                                    else if (inAir) {
                                        UB.Core.Actions.MoveItem(item, UB.Core.CharacterFilter.Id);
                                        if (needsToBeLootedItems.First().Key == item) {
                                            lootAttemptCount++;
                                        }
                                    }
                                    else if (!inAir && !jumping && needsToBeLootedItems.Count() > 1) {
                                        UB.Jumper.SimpleJump(JumpHeight);
                                        UB.Jumper.JumperFinished += Jumper_JumperFinished;
                                        jumping = true;
                                    }
                                }
                            }
                            else {
                                UB.Core.Actions.MoveItem(item, UB.Core.CharacterFilter.Id);
                                if (needsToBeLootedItems.First().Key == item) {
                                    lootAttemptCount++;
                                }
                            }
                            if (lootAttemptCount >= AttemptsBeforeBlacklisting) {
                                int blackListedItem = needsToBeLootedItems.First().Key;
                                containerItems[blackListedItem] = itemstate.Blacklisted;
                                Logger.WriteToChat("Looter: Failed to loot " + UB.Core.WorldFilter[blackListedItem].Name.ToString());
                            }
                        }


                        if (containerItems.Count() > 0 && lootedItems.Count() + blacklistedItems.Count() + ignoredItems.Count() == containerItemsList.Count()) {
                            if (UbOwnedContainer) {
                                lastAttempt = DateTime.UtcNow;
                                looterState = looterstate.Closing;
                            }
                            else {
                                DoneLooting();
                            }
                        }
                        break;
                    case looterstate.Closing:
                        if (DateTime.UtcNow - lastAttempt >= TimeSpan.FromMilliseconds(DelaySpeed)) {
                            //Logger.WriteToChat("attempting to close");
                            lastAttempt = DateTime.UtcNow;
                            UB.Core.Actions.UseItem(targetContainerID, 0);
                        }
                        break;
                    case looterstate.Salvaging:
                        if (!salvaging && DateTime.UtcNow - salvageDelay >= TimeSpan.FromMilliseconds(500)) {
                            salvageDelay = DateTime.UtcNow;
                            var lootedList = containerItems.Where(w => w.Value == itemstate.Looted).Select(w => w.Key).ToList();
                            UB.Assessor.RequestAll(lootedList);
                            UB.AutoSalvage.AutoSalvageFinished += AutoSalvage_AutoSalvageFinished;
                            UB.AutoSalvage.Start(true, lootedList);
                            salvaging = true;
                        }
                        break;
                }

            }
            catch (Exception ex) {
                Logger.LogException(ex);
            }
        }

        private void AutoSalvage_AutoSalvageFinished(object sender, EventArgs e) {
            try {
                DoneLooting();
                UB.AutoSalvage.AutoSalvageFinished -= AutoSalvage_AutoSalvageFinished;
            }
            catch (Exception ex) {
                Logger.LogException(ex);
            }
        }

        private void Jumper_JumperFinished(object sender, EventArgs e) {
            try {
                jumping = false;
                inAir = false;
                UB.Jumper.JumperFinished -= Jumper_JumperFinished;
            }
            catch (Exception ex) {
                Logger.LogException(ex);
            }
        }

        protected override void Dispose(bool disposing) {
            try {
                if (!disposedValue) {
                    if (disposing) {
                        UB.Core.EchoFilter.ServerDispatch -= EchoFilter_ServerDispatch;
                        UB.Core.EchoFilter.ClientDispatch -= EchoFilter_ClientDispatch;
                        UB.Core.CharacterFilter.LoginComplete -= CharacterFilter_LoginComplete;
                        UB.Jumper.JumperFinished -= Jumper_JumperFinished;
                        if (baseTimer != null) baseTimer.Timeout -= BaseTimer_Timeout;
                        base.Dispose(disposing);
                    }
                    disposedValue = true;
                }
            }
            catch (Exception ex) {
                Logger.LogException(ex);
            }
        }
    }
}
