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
using UtilityBelt.Lib.Networking;

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
        private bool needsVitalUpdate;
        private DateTime lastVitalChange = DateTime.MinValue;
        private TimeSpan vitalReportDelay = TimeSpan.FromMilliseconds(50);
        private TimeSpan vitalReportInterval = TimeSpan.FromSeconds(2);
        private DateTime lastTrackedItemUpdate = DateTime.MinValue;
        private TimeSpan trackedItemUpdateInterval = TimeSpan.FromSeconds(5);
        private DateTime lastPositionUpdate = DateTime.MinValue;
        private TimeSpan positionUpdateInterval = TimeSpan.FromMilliseconds(300);

        private bool isInPortalSpace = true;

        public int LastAttemptedSpellId { get; private set; }
        public int LastAttemptedTarget { get; private set; }
        public int LastCastSpellId { get; private set; }

        public VTankFellowHeals(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        public override void Init() {
            base.Init();

            if (UB.VTank.VitalSharing)
                TryEnable();

            UB.VTank.VitalSharing.Changed += VitalSharing_Changed;
        }

        private void VitalSharing_Changed(object sender, SettingChangedEventArgs e) {
            if (UB.VTank.VitalSharing)
                TryEnable();
            else
                Stop();
        }

        private void TryEnable() {
            if (isRunning)
                return;
            UB.Core.CharacterFilter.ChangeVital += CharacterFilter_ChangeVital;
            UB.Core.EchoFilter.ClientDispatch += EchoFilter_ClientDispatch;
            UB.Core.ChatBoxMessage += Core_ChatBoxMessage;
            UB.Core.RenderFrame += Core_RenderFrame;
            UB.Core.CharacterFilter.ChangePortalMode += CharacterFilter_ChangePortalMode;
            UB.Networking.AddMessageHandler<PlayerUpdateMessage>(Handle_PlayerUpdateMessage);
            UB.Networking.AddMessageHandler<CastAttemptMessage>(Handle_CastAttemptMessage);
            UB.Networking.AddMessageHandler<CastSuccessMessage>(Handle_CastSuccessMessage);
            UB.Networking.AddMessageHandler<LoginMessage>(Handle_LoginMessage);
            UB.Networking.OnConnected += Networking_OnConnected;
            UB.Networking.OnRemoteClientConnected += Networking_OnRemoteClientConnected;
            isRunning = true;
        }

        private void CharacterFilter_ChangePortalMode(object sender, ChangePortalModeEventArgs e) {
            isInPortalSpace = e.Type == PortalEventType.EnterPortal;
        }

        private void Networking_OnConnected(object sender, EventArgs e) {
            needsVitalUpdate = true;
        }

        private void Networking_OnRemoteClientConnected(object sender, RemoteClientConnectionEventArgs e) {
            needsVitalUpdate = true;
        }

        private void Handle_PlayerUpdateMessage(MessageHeader header, PlayerUpdateMessage message) {
            UpdateVTankVitalInfo(message);
        }

        private void Handle_CastSuccessMessage(MessageHeader header, CastSuccessMessage message) {
            if (header.SendingClientId == UB.Networking.ClientId)
                return;
            UBHelper.vTank.Instance?.LogSpellCast(message.Target, message.SpellId, message.Duration);
        }

        private void Handle_CastAttemptMessage(MessageHeader header, CastAttemptMessage message) {
            if (header.SendingClientId == UB.Networking.ClientId)
                return;
            UBHelper.vTank.Instance?.LogCastAttempt(message.SpellId, message.Target, message.Skill);
        }

        private void Handle_LoginMessage(MessageHeader arg1, LoginMessage arg2) {
            needsVitalUpdate = true;
        }

        private void Stop() {
            if (!isRunning)
                return;
            UB.Core.CharacterFilter.ChangeVital -= CharacterFilter_ChangeVital;
            UB.Core.EchoFilter.ClientDispatch -= EchoFilter_ClientDispatch;
            UB.Core.ChatBoxMessage -= Core_ChatBoxMessage;
            UB.Core.RenderFrame -= Core_RenderFrame;
            UB.Core.CharacterFilter.ChangePortalMode -= CharacterFilter_ChangePortalMode;
            if (UB?.Networking != null) {
                UB.Networking.RemoveMessageHandler<PlayerUpdateMessage>(Handle_PlayerUpdateMessage);
                UB.Networking.RemoveMessageHandler<CastAttemptMessage>(Handle_CastAttemptMessage);
                UB.Networking.RemoveMessageHandler<CastSuccessMessage>(Handle_CastSuccessMessage);
                UB.Networking.RemoveMessageHandler<LoginMessage>(Handle_LoginMessage);
                UB.Networking.OnConnected -= Networking_OnConnected;
                UB.Networking.OnRemoteClientConnected -= Networking_OnRemoteClientConnected;
            }
            isRunning = false;
        }

        private void Core_RenderFrame(object sender, EventArgs e) {
            try {
                if (!UB.Networking.Connected)
                    return;

                // todo: we need inventory tracking so we dont have to poll tracked items
                if (DateTime.UtcNow - lastTrackedItemUpdate > trackedItemUpdateInterval) {
                    UpdateMyTrackedItems();
                    lastTrackedItemUpdate = DateTime.UtcNow;
                }
                if (DateTime.UtcNow - lastPositionUpdate > positionUpdateInterval) {
                    UpdateMyPosition();
                    lastPositionUpdate = DateTime.UtcNow;
                }
                if ((needsVitalUpdate && DateTime.UtcNow - lastVitalUpdate > TimeSpan.FromMilliseconds(100) && DateTime.UtcNow - lastVitalChange > vitalReportDelay) || DateTime.UtcNow - lastVitalUpdate > vitalReportInterval) {
                    lastVitalUpdate = DateTime.UtcNow;
                    UpdateMySharedVitals();
                    needsVitalUpdate = false;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void CharacterFilter_ChangeVital(object sender, ChangeVitalEventArgs e) {
            try {
                needsVitalUpdate = true;
                if (DateTime.UtcNow - lastVitalChange > vitalReportDelay)
                    lastVitalChange = DateTime.UtcNow;
            }
            catch (Exception ex) { Logger.LogException(ex);  }
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
                    UB.Networking.SendObject(new CastAttemptMessage(spellId, target, skill));
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
                    UB.Networking.SendObject(new CastSuccessMessage(LastAttemptedSpellId, LastAttemptedTarget, duration));
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public void UpdateMySharedVitals() {
            if (UBHelper.vTank.Instance == null || !UB.VTank.VitalSharing)
                return;
            var update = GetMyPlayerUpdate();
            UB.Networking.SendObject(update);
        }

        public void UpdateMyPosition() {
            if (isInPortalSpace)
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
            UB.Networking.SendObject(update);
        }

        private void UpdateMyTrackedItems() {
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

            UB.Networking.SendObject(new TrackedItemUpdateMessage() {
                TrackedItems = trackedItems.Select(kv => kv.Value).ToList()
            });
        }

        private void UpdateVTankVitalInfo(PlayerUpdateMessage update) {
            try {
                if (UBHelper.vTank.Instance == null || update == null) return;
                var helperUpdate = new sPlayerInfoUpdate() {
                    PlayerID = update.PlayerId,
                    HasHealthInfo = true,
                    HasManaInfo = true,
                    HasStamInfo = true,
                    curHealth = update.CurHealth,
                    curMana = update.CurMana,
                    curStam = update.CurStam,
                    maxHealth = update.MaxHealth,
                    maxMana = update.MaxMana,
                    maxStam = update.MaxStam
                };
                try {
                    UBHelper.vTank.Instance.HelperPlayerUpdate(helperUpdate);
                }
                catch { }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private PlayerUpdateMessage GetMyPlayerUpdate() {
            return new PlayerUpdateMessage {
                PlayerId = UB.Core.CharacterFilter.Id,
                CurHealth = UB.Core.Actions.Vital[VitalType.CurrentHealth],
                CurStam = UB.Core.Actions.Vital[VitalType.CurrentStamina],
                CurMana = UB.Core.Actions.Vital[VitalType.CurrentMana],

                MaxHealth = UB.Core.Actions.Vital[VitalType.MaximumHealth],
                MaxStam = UB.Core.Actions.Vital[VitalType.MaximumStamina],
                MaxMana = UB.Core.Actions.Vital[VitalType.MaximumMana],
            };
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
