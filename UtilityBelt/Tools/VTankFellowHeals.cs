using System;
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
using UBLoader.Lib.Settings;
using UtilityBelt.Lib.Networking.Messages;

namespace UtilityBelt.Tools {
    [Name("VTankFellowHeals")]
    [Summary("Automatically forwards vital and spellcasting information to VTank.")]
    [FullDescription(@"
If enabled, this will automatically share vital information for all clients on the same PC with VTank.  Spell casting information will also be shared. If you have two characters vulning, they should choose different targets and not overlap spells.

This allows VTank to heal/restam/remana characters on your same pc, even when they do not share in ingame fellowship.
    ")]
    public class VTankFellowHeals : ToolBase {
        DateTime lastThought = DateTime.UtcNow;
        DateTime lastVitalUpdate = DateTime.MinValue;
        private DateTime lastCastAttempt = DateTime.MinValue;
        private DateTime lastCastSuccess = DateTime.MinValue;
        private bool isRunning;
        private bool needsVitalUpdate;

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
            UB.Networking.OnPlayerUpdateMessage += Networking_OnPlayerUpdateMessage;
            UB.Networking.OnCastAttemptMessage += Networking_OnCastAttemptMessage;
            UB.Networking.OnCastSuccessMessage += Networking_OnCastSuccessMessage;
            isRunning = true;
        }

        private void Stop() {
            if (!isRunning)
                return;
            UB.Core.CharacterFilter.ChangeVital -= CharacterFilter_ChangeVital;
            UB.Core.EchoFilter.ClientDispatch -= EchoFilter_ClientDispatch;
            UB.Core.ChatBoxMessage -= Core_ChatBoxMessage;
            UB.Core.RenderFrame -= Core_RenderFrame;
            UB.Networking.OnPlayerUpdateMessage -= Networking_OnPlayerUpdateMessage;
            UB.Networking.OnCastAttemptMessage -= Networking_OnCastAttemptMessage;
            UB.Networking.OnCastSuccessMessage -= Networking_OnCastSuccessMessage;
            isRunning = false;
        }

        private void Core_RenderFrame(object sender, EventArgs e) {
            if (needsVitalUpdate && DateTime.UtcNow - lastVitalUpdate > TimeSpan.FromMilliseconds(800)) {
                lastVitalUpdate = DateTime.UtcNow;
                UpdateMySharedVitals();
                needsVitalUpdate = false;
            }
        }

        private void Networking_OnPlayerUpdateMessage(object sender, EventArgs e) {
            if (sender is PlayerUpdateMessage message)
                UpdateVTankVitalInfo(message);
        }

        private void Networking_OnCastAttemptMessage(object sender, EventArgs e) {
            if (sender is CastAttemptMessage message) {
                try {
                    UBHelper.vTank.Instance?.LogCastAttempt(message.SpellId, message.Target, message.Skill);
                }
                catch { }
            }
        }

        private void Networking_OnCastSuccessMessage(object sender, EventArgs e) {
            if (sender is CastSuccessMessage message) {
                try {
                    UBHelper.vTank.Instance?.LogSpellCast(message.Target, message.SpellId, message.Duration);
                }
                catch { }
            }
        }

        private void CharacterFilter_ChangeVital(object sender, ChangeVitalEventArgs e) {
            try {
                needsVitalUpdate = true;
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
                    UB.Networking.SendObject("CastAttemptMessage", new CastAttemptMessage(spellId, target, skill));
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
                    if (!spell.IsDebuff)
                        return;
                    var duration = Spells.GetSpellDuration(LastAttemptedSpellId) * 1000;
                    UB.Networking.SendObject("CastSuccessMessage", new CastSuccessMessage(LastAttemptedSpellId, LastAttemptedTarget, duration));
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public void UpdateMySharedVitals() {
            if (UBHelper.vTank.Instance == null || !UB.VTank.VitalSharing)
                return;

            UB.Networking.SendObject("PlayerUpdateMessage", GetMyPlayerUpdate());
        }

        private void UpdateVTankVitalInfo(PlayerUpdateMessage update) {
            try {
                if (UBHelper.vTank.Instance == null || update == null) return;
                if (update.Server != UB.Core.CharacterFilter.Server) return;

                var helperUpdate = new sPlayerInfoUpdate() {
                    PlayerID = update.PlayerID,
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
                PlayerID = UB.Core.CharacterFilter.Id,

                CurHealth = UB.Core.CharacterFilter.Vitals[CharFilterVitalType.Health].Current,
                CurMana = UB.Core.CharacterFilter.Vitals[CharFilterVitalType.Mana].Current,
                CurStam = UB.Core.CharacterFilter.Vitals[CharFilterVitalType.Stamina].Current,

                MaxHealth = UB.Core.CharacterFilter.EffectiveVital[CharFilterVitalType.Health],
                MaxMana = UB.Core.CharacterFilter.EffectiveVital[CharFilterVitalType.Mana],
                MaxStam = UB.Core.CharacterFilter.EffectiveVital[CharFilterVitalType.Stamina],

                Server = UB.Core.CharacterFilter.Server
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
