using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using ACE.Entity;
using Decal.Adapter;
using UBCommon.Enums;
using UtilityBelt.Scripting.Interop;

namespace UBLoader.Lib.ScriptInterface {
    public class ACClientActions : IClientActionsRaw {
        private ILogger _log;

        public ACClientActions(ILogger logger) {
            _log = logger;
        }

        public void AttributeAddExperience(AttributeId attribute, uint experienceToSpend) {
            try {
                CoreManager.Current.Actions.AddAttributeExperience((Decal.Adapter.Wrappers.AttributeType)attribute, (int)experienceToSpend);
            }
            catch (Exception ex) { _log.Log(ex); }
        }

        public void SkillAddExperience(SkillId skill, uint experienceToSpend) {
            try {
                CoreManager.Current.Actions.AddSkillExperience((Decal.Adapter.Wrappers.SkillType)skill, (int)experienceToSpend);
            }
            catch (Exception ex) { _log.Log(ex); }
        }

        public void VitalAddExperience(VitalId vital, uint experienceToSpend) {
            try {
                CoreManager.Current.Actions.AddVitalExperience((Decal.Adapter.Wrappers.VitalType)(vital + 1), (int)experienceToSpend);
            }
            catch (Exception ex) { _log.Log(ex); }
        }

        public void ApplyWeenie(uint sourceObjectId, uint targetObjectId) {
            try {
                CoreManager.Current.Actions.ApplyItem((int)sourceObjectId, (int)targetObjectId);
            }
            catch (Exception ex) { _log.Log(ex); }
        }

        public void AutoWield(uint objectId, int slot) {
            try {
                if (slot > -1) {
                    CoreManager.Current.Actions.AutoWield((int)objectId, slot, 1, 0);
                }
                else {
                    CoreManager.Current.Actions.AutoWield((int)objectId);
                }
            }
            catch (Exception ex) { _log.Log(ex); }
        }

        public void CastSpell(uint spellId, uint targetId) {
            try {
                CoreManager.Current.Actions.CastSpell((int)spellId, (int)targetId);
            }
            catch (Exception ex) { _log.Log(ex); }
        }

        public void Dispose() {

        }

        public void DropObject(uint objectId) {
            try {
                CoreManager.Current.Actions.DropItem((int)objectId);
            }
            catch (Exception ex) { _log.Log(ex); }
        }

        public void FellowshipCreate(string name, bool shareExperience) {
            try {
                UBHelper.Fellow.Create(name/*, shareExperience */);
            }
            catch (Exception ex) { _log.Log(ex); }
        }

        public void FellowshipDisband() {
            try {
                UBHelper.Fellow.Disband();
            }
            catch (Exception ex) { _log.Log(ex); }
        }

        public void FellowshipDismiss(uint objectId) {
            try {
                UBHelper.Fellow.Dismiss((int)objectId);
            }
            catch (Exception ex) { _log.Log(ex); }
        }

        public void FellowshipSetLeader(uint objectId) {
            try {
                UBHelper.Fellow.Leader = (int)objectId;
            }
            catch (Exception ex) { _log.Log(ex); }
        }

        public void FellowshipQuit(bool disband) {
            try {
                UBHelper.Fellow.Quit(/* disband */);
            }
            catch (Exception ex) { _log.Log(ex); }
        }

        public void FellowshipRecruit(uint objectId) {
            try {
                UBHelper.Fellow.Recruit((int)objectId);
            }
            catch (Exception ex) { _log.Log(ex); }
        }

        public void FellowshipSetOpen(bool open) {
            try {
                UBHelper.Fellow.Open = open;
            }
            catch (Exception ex) { _log.Log(ex); }
        }

        public void GiveWeenie(uint objectId, uint targetId) {
            try {
                CoreManager.Current.Actions.GiveItem((int)objectId, (int)targetId);
            }
            catch (Exception ex) { _log.Log(ex); }
        }

        public void InvokeChatParser(string text) {
            try {
                CoreManager.Current.Actions.InvokeChatParser(text);
            }
            catch (Exception ex) { _log.Log(ex); }
        }

        public void MoveWeenie(uint objectId, uint containerId, uint slot, bool stack) {
            try {
                CoreManager.Current.Actions.MoveItem((int)objectId, (int)containerId, (int)slot, stack);
            }
            catch (Exception ex) { _log.Log(ex); }
        }


        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool RequestIdDelegate(int objectId);
        private RequestIdDelegate _requestIdInternal;

        public void Appraise(uint objectId) {
            try {
                if (_requestIdInternal == null) {
                    _requestIdInternal = (RequestIdDelegate)Marshal.GetDelegateForFunctionPointer((IntPtr)(436554 << 4), typeof(RequestIdDelegate));
                }

                _requestIdInternal((int)objectId);
            }
            catch (Exception ex) { _log.Log(ex); }
        }

        public void SalvagePanelAdd(uint objectId) {
            try {
                CoreManager.Current.Actions.SalvagePanelAdd((int)objectId);
            }
            catch (Exception ex) { _log.Log(ex); }
        }

        public void SalvagePanelSalvage() {
            try {
                CoreManager.Current.Actions.SalvagePanelSalvage();
            }
            catch (Exception ex) { _log.Log(ex); }
        }

        public void SelectWeenie(uint objectId) {
            try {
                CoreManager.Current.Actions.SelectItem((int)objectId);
            }
            catch (Exception ex) { _log.Log(ex); }
        }

        public void SetAutorun(bool enabled) {
            try {
                CoreManager.Current.Actions.SetAutorun(enabled);
            }
            catch (Exception ex) { _log.Log(ex); }
        }

        public void SetCombatMode(CombatMode combatMode) {
            try {
                CoreManager.Current.Actions.SetCombatMode((Decal.Adapter.Wrappers.CombatState)combatMode);
            }
            catch (Exception ex) { _log.Log(ex); }
        }

        public void TradeAccept() {
            try {
                CoreManager.Current.Actions.TradeAccept();
            }
            catch (Exception ex) { _log.Log(ex); }
        }

        public void TradeAdd(uint objectId) {
            try {
                CoreManager.Current.Actions.TradeAdd((int)objectId);
            }
            catch (Exception ex) { _log.Log(ex); }
        }

        public void TradeDecline() {
            try {
                CoreManager.Current.Actions.TradeDecline();
            }
            catch (Exception ex) {
                _log.Log(ex);
            }
        }

        public void TradeEnd() {
            try {
                CoreManager.Current.Actions.TradeEnd();
            }
            catch (Exception ex) {
                _log.Log(ex);
            }
        }

        public void TradeReset() {
            try {
                CoreManager.Current.Actions.TradeReset();
            }
            catch (Exception ex) {
                _log.Log(ex);
            }
        }

        public void UseWeenie(uint objectId) {
            try {
                CoreManager.Current.Actions.UseItem((int)objectId, 0);
            }
            catch (Exception ex) { _log.Log(ex); }
        }

        public uint SelectedObject() {
            try {
                return (uint)CoreManager.Current.Actions.CurrentSelection;
            }
            catch (Exception ex) { _log.Log(ex); }
            return 0;
        }

        unsafe public void Login(uint id) {
            try {
                AcClient.CPlayerSystem.GetPlayerSystem()->LogOnCharacter(id);
            }
            catch (Exception ex) { _log.Log(ex); }
        }

        unsafe public void Logout() {
            try {
                AcClient.CPlayerSystem.GetPlayerSystem()->LogOffCharacter(0);
            }
            catch (Exception ex) { _log.Log(ex); }
        }

        public void SkillAdvance(SkillId skill, uint creditsToSpend) {
            try {
                AcClient.CM_Train.Event_TrainSkillAdvancementClass((uint)skill, creditsToSpend);
            }
            catch (Exception ex) { _log.Log(ex); }
        }

        public void AllegianceBreak(uint objectId) {
            try {
                AcClient.CM_Allegiance.Event_BreakAllegiance(objectId);
            }
            catch (Exception ex) { _log.Log(ex); }
        }

        public void AllegianceSwear(uint objectId) {
            try {
                AcClient.CM_Allegiance.Event_SwearAllegiance(objectId);
            }
            catch (Exception ex) { _log.Log(ex); }
        }
    }
}