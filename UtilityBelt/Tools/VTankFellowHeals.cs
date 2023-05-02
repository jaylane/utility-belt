using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using uTank2;
using static uTank2.PluginCore;
using Decal.Adapter.Wrappers;
using System.Runtime.InteropServices;
using UtilityBelt.Lib;
using System.Threading;
using System.Diagnostics;
using System.ComponentModel;
using UtilityBelt.Service.Lib.Settings;
using UtilityBelt.Lib.Networking.Messages;
using UtilityBelt.Networking.Lib;
using UtilityBelt.Networking.Messages;
using UtilityBelt.Service;
using Newtonsoft.Json;
using UtilityBelt.Common.Enums;
using AcClient;
using static UtilityBelt.Tools.Networking;
using ACE.DatLoader.Entity;

namespace UtilityBelt.Tools {
    [Name("VTankFellowHeals")]
    [Summary("Automatically forwards vital and spellcasting information to VTank.")]
    [FullDescription(@"
If enabled, this will automatically share vital information for all clients on the same PC with VTank.  Spell casting information will also be shared. If you have two characters vulning, they should choose different targets and not overlap spells.

This allows VTank to heal/restam/remana characters on your same pc, even when they do not share in ingame fellowship.
    ")]
    public class VTankFellowHeals : ToolBase {
        DateTime lastVitalUpdate = DateTime.MinValue;
        private DateTime lastCastAttempt = DateTime.MinValue;
        private DateTime lastCastSuccess = DateTime.MinValue;
        private bool isRunning;
        private bool needsVitalUpdate = true;
        private TimeSpan vitalReportInterval = TimeSpan.FromMilliseconds(150);
        private DateTime lastTrackedItemUpdate = DateTime.MinValue;
        private TimeSpan trackedItemUpdateInterval = TimeSpan.FromSeconds(5);
        private DateTime lastPositionUpdate = DateTime.MinValue;
        private TimeSpan positionUpdateInterval = TimeSpan.FromMilliseconds(300);

        private bool isInPortalSpace = true;
        private CharacterPositionMessage lastPosition;
        private TrackedItemUpdateMessage lastTrackedItem;
        private bool needsForcedTrackedItemsUpdate;
        private bool needsForcedPositionUpdate;
        private VitalUpdateMessage lastVitals;
        private DateTime lastClientDataUpdate;

        public int LastAttemptedSpellId { get; private set; }
        public int LastAttemptedTarget { get; private set; }
        public int LastCastSpellId { get; private set; }

        public VTankFellowHeals(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        public override void Init() {
            base.Init();

            if (UB.VTank.VitalSharing)
                TryEnable();
        }

        private void VitalSharing_Changed(object sender, SettingChangedEventArgs e) {
            try {
                if (UB.VTank.VitalSharing)
                    TryEnable();
                else
                    Stop();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void TryEnable() {
            if (isRunning)
                return;

            UB.VTank.VitalSharing.Changed += VitalSharing_Changed;
            UB.Core.EchoFilter.ClientDispatch += EchoFilter_ClientDispatch;
            UB.Core.ChatBoxMessage += Core_ChatBoxMessage;
            UB.Core.RenderFrame += Core_RenderFrame;
            UB.Core.CharacterFilter.ChangePortalMode += CharacterFilter_ChangePortalMode;
            UB.Networking.OnRemoteClientConnected += Networking_OnRemoteClientConnected;
            UB.Networking.OnRemoteClientDisconnected += Networking_OnRemoteClientDisconnected;
            UB.Networking.OnRemoteClientUpdated += Networking_OnRemoteClientUpdated;
            UB.Networking.OnConnected += Networking_OnConnected;
            isRunning = true;

            try {

                UBService.UBNet.UBNetClient.OnTypedChannelMessage += UBNetClient_OnTypedChannelMessage;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UBNetClient_OnTypedChannelMessage(object sender, TypedChannelMessageReceivedEventArgs e) {
            if (e.IsType(typeof(CastAttemptMessage)) && e.TryDeserialize<CastAttemptMessage>(out var castAttemptMessage)) {
                Handle_CastAttempt(e.SendingClientId, castAttemptMessage);
            }
            else if (e.IsType(typeof(CastSuccessMessage)) && e.TryDeserialize<CastSuccessMessage>(out var castSuccessMessage)) {
                Handle_CastSuccess(e.SendingClientId, castSuccessMessage);
            }
            else if (e.IsType(typeof(VitalUpdateMessage)) && e.TryDeserialize<VitalUpdateMessage>(out var vitalUpdateMessage)) {
                Handle_VitalUpdateMessage(e.SendingClientId, vitalUpdateMessage);
            }
        }

        private void Handle_CastSuccess(uint sendingClientId, CastSuccessMessage message) {
            try {
                if (sendingClientId == UBService.UBNet.UBNetClient?.Id || !UB.Core.Actions.IsValidObject(message.Target))
                    return;
                /*
                string tWo = "Unknown";
                if (UB.Core.Actions.IsValidObject(message.Target)) {
                    tWo = UB.Core.WorldFilter[message.Target].Name;
                }

                var spell = Spells.GetSpell(message.SpellId);
                string spellName = "Unknown";
                if (spell != null) {
                    spellName = spell.Name;
                }

                Logger.Debug($"vTank.LogSpellCast: Target: 0x{message.Target:X8} ({tWo}) // Spell: 0x{message.SpellId:X8} ({spellName}) // Duration: {message.Duration}");
                */
                UBHelper.vTank.Instance?.LogSpellCast(message.Target, message.SpellId, message.Duration);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Handle_CastAttempt(uint sendingClientId, CastAttemptMessage message) {
            try {
                if (sendingClientId == UBService.UBNet.UBNetClient?.Id || !UB.Core.Actions.IsValidObject(message.Target))
                    return;
                /*
                string tWo = "Unknown";
                if (UB.Core.Actions.IsValidObject(message.Target)) {
                    tWo = UB.Core.WorldFilter[message.Target].Name;
                }

                var spell = Spells.GetSpell(message.SpellId);
                string spellName = "Unknown";
                if (spell != null) {
                    spellName = spell.Name;
                }
                Logger.Debug($"vTank.LogCastAttempt: Target: 0x{message.Target:X8} ({tWo}) // Spell: 0x{message.SpellId:X8} ({spellName}) // Skill: {message.Skill}");
                */
                UBHelper.vTank.Instance?.LogCastAttempt(message.SpellId, message.Target, message.Skill);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }


        private void Networking_OnRemoteClientUpdated(object sender, Networking.RemoteClientEventArgs e) {
            //UpdateVTankVitalInfo(e.ClientData);
        }

        private void Networking_OnRemoteClientDisconnected(object sender, Networking.RemoteClientEventArgs e) {
            
        }

        private void Networking_OnRemoteClientConnected(object sender, Networking.RemoteClientEventArgs e) {
            needsVitalUpdate = true;
            needsForcedTrackedItemsUpdate = true;
            needsForcedPositionUpdate = true;
        }

        private void CharacterFilter_ChangePortalMode(object sender, ChangePortalModeEventArgs e) {
            isInPortalSpace = e.Type == PortalEventType.EnterPortal;
            if (!isInPortalSpace) {
                needsForcedPositionUpdate = true;
            }
        }

        private void Networking_OnConnected(object sender, EventArgs e) {
            needsVitalUpdate = true;
            needsForcedTrackedItemsUpdate = true;
            needsForcedPositionUpdate = true;
        }

        private void Stop() {
            if (!isRunning)
                return;

            UB.VTank.VitalSharing.Changed -= VitalSharing_Changed;
            UB.Core.EchoFilter.ClientDispatch -= EchoFilter_ClientDispatch;
            UB.Core.ChatBoxMessage -= Core_ChatBoxMessage;
            UB.Core.RenderFrame -= Core_RenderFrame;
            UB.Core.CharacterFilter.ChangePortalMode -= CharacterFilter_ChangePortalMode;
            if (UB?.Networking != null) {
                UB.Networking.OnRemoteClientConnected -= Networking_OnRemoteClientConnected;
                UB.Networking.OnRemoteClientDisconnected -= Networking_OnRemoteClientDisconnected;
                UB.Networking.OnRemoteClientUpdated -= Networking_OnRemoteClientUpdated;
                UB.Networking.OnConnected -= Networking_OnConnected;

                UBService.UBNet.UBNetClient.OnTypedChannelMessage -= UBNetClient_OnTypedChannelMessage;
            }

            isRunning = false;
        }

        private void Core_RenderFrame(object sender, EventArgs e) {
            try {
                if (!UBService.UBNet?.UBNetClient?.IsConnected == true)
                    return;

                // todo: we need inventory tracking so we dont have to poll tracked items
                if (needsForcedTrackedItemsUpdate || DateTime.UtcNow - lastTrackedItemUpdate > trackedItemUpdateInterval) {
                    UpdateMyTrackedItems(needsForcedTrackedItemsUpdate);
                    needsForcedTrackedItemsUpdate = false;
                }
                if (needsForcedPositionUpdate || DateTime.UtcNow - lastPositionUpdate > positionUpdateInterval) {
                    UpdateMyPosition(needsForcedPositionUpdate);
                    needsForcedPositionUpdate = false;
                }
                if (needsVitalUpdate || DateTime.UtcNow - lastVitalUpdate > vitalReportInterval) {
                    UpdateMySharedVitals(needsVitalUpdate);
                    needsVitalUpdate = false;
                }

                // todo: not so spammy
                if (DateTime.UtcNow - lastClientDataUpdate > TimeSpan.FromSeconds(3)) {
                    lastClientDataUpdate = DateTime.UtcNow;
                    UB.Networking.SendClientData(true);
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void EchoFilter_ClientDispatch(object sender, Decal.Adapter.NetworkMessageEventArgs e) {
            try {
                // Magic_CastTargetedSpell
                if (e.Message.Type == 0xF7B1 && e.Message.Value<int>("action") == 0x004A) {
                    var target = e.Message.Value<int>("target");
                    var spellId = e.Message.Value<int>("spell");
                    if (LastAttemptedSpellId == spellId && LastAttemptedTarget == target && DateTime.UtcNow - lastCastAttempt < TimeSpan.FromMilliseconds(1200))
                        return;

                    var skill = Spells.GetEffectiveSkillForSpell(spellId);
                    var spell = Spells.GetSpell(spellId);
                    // we currently only send debuffs, nothing else is used
                    if (!spell.IsDebuff)
                        return;
                    lastCastAttempt = DateTime.UtcNow;
                    UB.Networking.ChannelBroadcast("utilitybelt", new CastAttemptMessage(spellId, target, skill));
                    LastAttemptedSpellId = spellId;
                    LastAttemptedTarget = target;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void Core_ChatBoxMessage(object sender, Decal.Adapter.ChatTextInterceptEventArgs e) {
            try {
                if (LastAttemptedSpellId != 0 && e.Text.StartsWith("You cast ")) {
                    if (LastCastSpellId == LastAttemptedSpellId && DateTime.UtcNow - lastCastSuccess < TimeSpan.FromMilliseconds(50))
                        return;
                    lastCastSuccess = DateTime.UtcNow;
                    LastCastSpellId = LastAttemptedSpellId;
                    var spell = Spells.GetSpell(LastAttemptedSpellId);
                    // we currently only send debuffs, nothing else is used
                    if (!spell.IsDebuff)
                        return;
                    var duration = Spells.GetSpellDuration(LastAttemptedSpellId) * 1000;
                    UB.Networking.ChannelBroadcast("utilitybelt", new CastSuccessMessage(LastAttemptedSpellId, LastAttemptedTarget, duration));
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public void UpdateMySharedVitals(bool forced) {
            try {
                if (UBHelper.vTank.Instance == null || !UB.VTank.VitalSharing || UBHelper.Core.GameState != UBHelper.GameState.In_Game)
                    return;

                var update = GetVitals();

                if (forced || !update.Equals(lastVitals)) {
                    UB.Networking.ChannelBroadcast("utilitybelt", update);
                    lastVitals = update;
                    lastVitalUpdate = DateTime.UtcNow;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public VitalUpdateMessage GetVitals() {
            return new VitalUpdateMessage() {
                CurrentHealth = UB.Core.Actions.Vital[VitalType.CurrentHealth],
                CurrentStamina = UB.Core.Actions.Vital[VitalType.CurrentStamina],
                CurrentMana = UB.Core.Actions.Vital[VitalType.CurrentMana],

                MaxHealth = UB.Core.Actions.Vital[VitalType.MaximumHealth],
                MaxStamina = UB.Core.Actions.Vital[VitalType.MaximumStamina],
                MaxMana = UB.Core.Actions.Vital[VitalType.MaximumMana],
            };
        }

        public void UpdateMyPosition(bool force) {
            try {
                if (UBHelper.vTank.Instance == null || !UB.VTank.VitalSharing || UBHelper.Core.GameState != UBHelper.GameState.In_Game)
                    return;
                var me = UtilityBeltPlugin.Instance.Core.CharacterFilter.Id;
                var pos = PhysicsObject.GetPosition(me);
                var lc = PhysicsObject.GetLandcell(me);
                var update = new CharacterPositionMessage() {
                    Z = pos.Z,
                    EW = Geometry.LandblockToEW((uint)lc, pos.X),
                    NS = Geometry.LandblockToNS((uint)lc, pos.Y),
                    Heading = UtilityBeltPlugin.Instance.Core.Actions.Heading
                };
                if (force || !update.Equals(lastPosition)) {
                    UB.Networking.ChannelBroadcast("utilitybelt", update);
                    lastPosition = update;
                    lastPositionUpdate = DateTime.UtcNow;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void UpdateMyTrackedItems(bool force) {
            try {
                if (UBHelper.vTank.Instance == null || !UB.VTank.VitalSharing || UBHelper.Core.GameState != UBHelper.GameState.In_Game)
                    return;

                var update = new TrackedItemUpdateMessage() {
                    TrackedItems = GetTrackedItems() // 
                };

                if (force || !update.Equals(lastTrackedItem)) {
                    UB.Networking.ChannelBroadcast("utilitybelt", update);
                    lastTrackedItem = update;
                    lastTrackedItemUpdate = DateTime.UtcNow;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        internal List<TrackedItemStatus> GetTrackedItems() {
            if (UBHelper.vTank.Instance == null || !UB.VTank.VitalSharing || UBHelper.Core.GameState != UBHelper.GameState.In_Game)
                return new List<TrackedItemStatus>();

            var trackedItems = new Dictionary<string, TrackedItemStatus>();

            foreach (var item in UB.NetworkUI.TrackedItems.Value) {
                if (!trackedItems.ContainsKey(item.Name))
                    trackedItems.Add(item.Name, new TrackedItemStatus() {
                        Name = item.Name,
                        Icon = item.Icon,
                        Count = 0
                    });
            }

            var inv = new List<int>();
            UBHelper.InventoryManager.GetInventory(ref inv, UBHelper.InventoryManager.GetInventoryType.Everything, UBHelper.Weenie.INVENTORY_LOC.ALL_LOC);

            foreach (var id in inv) {
                var item = new UBHelper.Weenie(id);
                var name = item.GetName(UBHelper.NameType.NAME_SINGULAR);
                if (trackedItems.ContainsKey(name))
                    trackedItems[name].Count += Math.Max(item.StackCount, 1);
            }

            return trackedItems.Select(kv => kv.Value).ToList();
        }

        private void Handle_VitalUpdateMessage(uint sendingClientId, VitalUpdateMessage vitalUpdate) {
            var existing = UB.Networking.Clients.FirstOrDefault(c => c.Id == sendingClientId);
            if (existing is not null) {
                existing.HasVitalInfo = true;
                existing.CurrentHealth = vitalUpdate.CurrentHealth;
                existing.CurrentMana = vitalUpdate.CurrentMana;
                existing.CurrentStamina = vitalUpdate.CurrentStamina;
                existing.MaxHealth = vitalUpdate.MaxHealth;
                existing.MaxMana = vitalUpdate.MaxMana;
                existing.MaxStamina = vitalUpdate.MaxStamina;
                UpdateVTankVitalInfo(existing);
            }
        }

        private void UpdateVTankVitalInfo(ClientData update) {
            try {
                if (UBHelper.vTank.Instance == null || update == null) return;
                if (update.MaxHealth <= 0)
                    return;
                var helperUpdate = new sPlayerInfoUpdate() {
                    PlayerID = update.PlayerId,
                    HasHealthInfo = true,
                    HasManaInfo = true,
                    HasStamInfo = true,
                    curHealth = update.CurrentHealth,
                    curMana = update.CurrentMana,
                    curStam = update.CurrentStamina,
                    maxHealth = update.MaxHealth,
                    maxMana = update.MaxMana,
                    maxStam = update.MaxStamina
                };
                try {
                    UBHelper.vTank.Instance.HelperPlayerUpdate(helperUpdate);
                }
                catch { }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        #region IDisposable Support
        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    UB.VTank.VitalSharing.Changed -= VitalSharing_Changed;
                    Stop();

                    base.Dispose(disposing);
                }
                
                disposedValue = true;
            }
        }
        #endregion
    }
}
