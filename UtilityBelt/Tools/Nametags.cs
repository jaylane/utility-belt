using Decal.Adapter;
using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Settings;
using UtilityBelt.Service.Lib.Settings;
using AcClient;
using System.Runtime.InteropServices;

namespace UtilityBelt.Tools {
    [Name("Nametags")]
    [Summary("Shows nametags above certain landscape objects")]
    [FullDescription(@"
When enabled, this will draw nametags above players/npcs/vendors/monsters/portals.

For players, it also shows the allegiance and level of the player.

For portals, it will show the destination.
    ")]
    public class Nametags : ToolBase {
        private static Dictionary<int, BitcoinMiner> tags = new Dictionary<int, BitcoinMiner>();
        internal static List<int> destructionQueue = new List<int>();
        internal static bool enabled = false;
        private static DateTime evaluate_tags_time = DateTime.MinValue;

        #region Config
        [Summary("Enabled")]
        [Hotkey("NameTags", "Toggle NameTags display")]
        public readonly Setting<bool> Enabled = new Setting<bool>(true);

        [Summary("Maximum Range for Nametags")]
        public readonly Setting<float> MaxRange = new Setting<float>(35f);

        [Summary("Player Nametag")]
        public readonly NametagDisplay Player = new NametagDisplay(true, -16711681, 0.15f, -16711681, 0.1f);

        [Summary("Pet Nametag")]
        public readonly NametagDisplay Pet = new NametagDisplay(true, -16711681, 0.15f, -16711681, 0.1f);

        [Summary("Allegiance Player Nametag")]
        public readonly NametagDisplay AllegiancePlayer = new NametagDisplay(true, -16711936, 0.15f, -16711936, 0.1f);

        [Summary("Portal Nametag")]
        public readonly NametagDisplay Portal = new NametagDisplay(true, -16711936, 0.15f, -16711936, 0.1f);

        [Summary("Npc Nametag")]
        public readonly NametagDisplay Npc = new NametagDisplay(true, -256, 0.15f, -256, 0.1f);

        [Summary("Vendor Nametag")]
        public readonly NametagDisplay Vendor = new NametagDisplay(true, -65281, 0.15f, -65281, 0.1f);

        [Summary("Monster Nametag")]
        public readonly NametagDisplay Monster = new NametagDisplay(true, -65536, 0.15f, -65536, 0.1f);
        #endregion

        public Nametags(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        public override void Init() {
            base.Init();
            Changed += Nametags_PropertyChanged;

            if (Enabled) Enable();
        }

        /// <summary>
        /// Overall Enable function- based purely on user preference
        /// </summary>
        private void Enable() {
            if (UBHelper.Core.GameState == UBHelper.GameState.In_Game) {
                UBHelper.VideoPatch.Changed += VideoPatch_Changed;
                VideoPatch_Changed();
            }
            else
                UB.Core.CharacterFilter.LoginComplete += CharacterFilter_LoginComplete;
        }
        private void CharacterFilter_LoginComplete(object sender, EventArgs e) {
            UB.Core.CharacterFilter.LoginComplete -= CharacterFilter_LoginComplete;
            UBHelper.VideoPatch.Changed += VideoPatch_Changed;
            VideoPatch_Changed();
        }

        /// <summary>
        /// Overall Disable function- based purely on user preference
        /// </summary>
        public void Disable() {
            UBHelper.VideoPatch.Changed -= VideoPatch_Changed;
            DisableInternal();
        }

        private void VideoPatch_Changed() {
            if (!UB.OverlayMap.Enabled && UBHelper.VideoPatch.Enabled && !(UBHelper.VideoPatch.bgOnly && UBHelper.Core.isFocused)) DisableInternal();
            else EnableInternal();
        }

        /// <summary>
        /// Internal Enable function- for enabling internally, without affecting user preference
        /// </summary>
        private unsafe void EnableInternal() {
            if (!enabled) {
                enabled = true;
                //if (!CPhysicsObj__TurnToObject_hook.Setup(new CPhysicsObj__TurnToObject_def(CPhysicsObj__TurnToObject)))
                //    LogError($"HOOK>CPhysicsObj__TurnToObject instaill falure");
                //if (!CPhysicsObj__MoveToObject_hook.Setup(new CPhysicsObj__MoveToObject_def(CPhysicsObj__MoveToObject)))
                //    LogError($"HOOK>CPhysicsObj__MoveToObject instaill falure");
                //if (!H__remove_hook.Setup(new H__remove_def(H__remove)))
                //    LogError($"HOOK>H__remove instaill falure");
                UB.Core.WorldFilter.CreateObject += WorldFilter_CreateObject;
                UB.Core.WorldFilter.ChangeObject += WorldFilter_ChangeObject;
                UB.Core.RenderFrame += Core_RenderFrame;
                evaluate_tags_time = DateTime.MinValue;
            }
        }

        /// <summary>
        /// Internal Disable function- for disabling internally, without affecting user preference
        /// </summary>
        public void DisableInternal() {
            if (enabled) {
                //if (!CPhysicsObj__TurnToObject_hook.Remove())
                //    LogError($"HOOK>CPhysicsObj__TurnToObject removal failure");
                //if (!CPhysicsObj__MoveToObject_hook.Remove())
                //    LogError($"HOOK>CPhysicsObj__MoveToObject removal failure");
                //if (!H__remove_hook.Remove())
                //    LogError($"HOOK>H__remove removal failure");
                enabled = false;
                UB.Core.WorldFilter.CreateObject -= WorldFilter_CreateObject;
                UB.Core.WorldFilter.ChangeObject -= WorldFilter_ChangeObject;
                UB.Core.RenderFrame -= Core_RenderFrame;
                UB.Core.CharacterFilter.LoginComplete -= CharacterFilter_LoginComplete;
                foreach (var i in tags) i.Value.Dispose();
                tags.Clear();
                destructionQueue.Clear();
            }
        }

        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    Changed -= Nametags_PropertyChanged;
                    Disable();
                    base.Dispose(disposing);
                }
                disposedValue = true;
            }
        }
        private void Nametags_PropertyChanged(object sender, SettingChangedEventArgs e) {
            switch (e.PropertyName) {
                case "Enabled":
                    if (Enabled) Enable();
                    else Disable();
                    break;
            }
            evaluate_tags_time = DateTime.UtcNow + TimeSpan.FromMilliseconds(250);
        }
        private void AddTag(WorldObject wo) {
            if (wo.Id == CoreManager.Current.CharacterFilter.Id) return;
            if (tags.ContainsKey(wo.Id)) return;

            var setting = Settings.GetSetting($"Nametags.{wo.ObjectClass}.Enabled");
            if (setting != null && (bool)setting.GetValue()) tags.Add(wo.Id, new BitcoinMiner(wo));
        }
        private void EvaluateTags() {
            foreach (KeyValuePair<int, BitcoinMiner> i in tags) {
                NametagDisplay tagSettings = (NametagDisplay)Settings.GetSetting($"Nametags.{i.Value.oc}");
                if (tagSettings != null && tagSettings.Enabled) {
                    i.Value.UpdateDisplay();
                    continue;
                }
                i.Value.Dispose();
            }
        }
        private static void WorldFilter_ChangeObject(object sender, ChangeObjectEventArgs e) {
            if (e.Change == WorldChangeType.IdentReceived && tags.ContainsKey(e.Changed.Id)) {
                tags[e.Changed.Id].heritage = e.Changed.Values(LongValueKey.Heritage, -1);
                tags[e.Changed.Id].UpdateData(true);
            }
        }
        private void WorldFilter_CreateObject(object sender, CreateObjectEventArgs e) {
            try {
                AddTag(e.New);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        private void Core_RenderFrame(object sender, EventArgs e) {
            try {
                foreach (KeyValuePair<int, BitcoinMiner> i in tags) i.Value.UpdateData();
                if (destructionQueue.Count > 0) {
                    foreach (int i in destructionQueue) tags.Remove(i);
                    destructionQueue.Clear();
                }
                if (DateTime.UtcNow > evaluate_tags_time) {
                    evaluate_tags_time = DateTime.MaxValue;
                    EvaluateTags();
                    using (var landscape = CoreManager.Current.WorldFilter.GetLandscape()) {
                        foreach (WorldObject wo in landscape) AddTag(wo);
                    }
                }
            } catch (Exception ex) { Logger.LogException(ex); }
        }

        //WIP code for tracking targets of targets

        //internal Hook CPhysicsObj__TurnToObject_hook = new AcClient.Hook(0x00513440, 0x005252D0);
        //// .text:005252D0                 call    ?TurnToObject@CPhysicsObj@@QAEXKABVMovementParameters@@@Z ; CPhysicsObj::TurnToObject(ulong,MovementParameters const &)
        //// .text:00513440 ; public: void __thiscall CPhysicsObj::TurnToObject(unsigned long,class MovementParameters const &)
        //[UnmanagedFunctionPointer(CallingConvention.ThisCall)] internal unsafe delegate void CPhysicsObj__TurnToObject_def(CPhysicsObj* This, UInt32 object_id, MovementParameters* _params);

        ///// <summary>
        ///// Detour function- the client thinks this is CPhysicsObj::TurnToObject, so make sure you call the real thing
        ///// </summary>
        //private unsafe void CPhysicsObj__TurnToObject(CPhysicsObj* This, UInt32 object_id, MovementParameters* _params) {
        //    //WriteToChat($"HOOK>CPhysicsObj__TurnToObject({This->a0.a0.id:X8}, {object_id:X8}, {*_params})");
        //    AddUpdate_Object(This->a0.a0.id, object_id);
        //    This->TurnToObject(object_id, _params);
        //}


        //internal Hook CPhysicsObj__MoveToObject_hook = new AcClient.Hook(0x00513360, 0x00525204);
        //// .text:00525204                 call    ?MoveToObject@CPhysicsObj@@QAEXKABVMovementParameters@@@Z ; CPhysicsObj::MoveToObject(ulong,MovementParameters const &)
        //// .text:00513360 ; public: void __thiscall CPhysicsObj::MoveToObject(unsigned long,class MovementParameters const &)
        //[UnmanagedFunctionPointer(CallingConvention.ThisCall)] internal unsafe delegate void CPhysicsObj__MoveToObject_def(CPhysicsObj* This, UInt32 object_id, MovementParameters* _params);

        ///// <summary>
        ///// Detour function- the client thinks this is CPhysicsObj::TurnToObject, so make sure you call the real thing
        ///// </summary>
        //private unsafe void CPhysicsObj__MoveToObject(CPhysicsObj* This, UInt32 object_id, MovementParameters* _params) {
        //    //WriteToChat($"HOOK>CPhysicsObj__MoveToObject({This->a0.a0.id:X8}, {object_id:X8}, {*_params})");
        //    AddUpdate_Object(This->a0.a0.id, object_id);
        //    This->MoveToObject(object_id, _params);
        //}


        ////not all of these are pretty.
        //internal Hook H__remove_hook = new AcClient.Hook(0x004171E0, 0x00509576);
        //// .text:00509576                 call    ?remove@?$IntrusiveHashTable@V?$IDClass@U_tagDataID@@$0CA@$0A@@@PAV?$HashSetData@V?$IDClass@U_tagDataID@@$0CA@$0A@@@@@$00@@QAEPAV?$HashSetData@V?$IDClass@U_tagDataID@@$0CA@$0A@@@@@ABV?$IDClass@U_tagDataID@@$0CA@$0A@@@@Z ; IntrusiveHashTable<IDClass<_tagDataID,32,0>,HashSetData<IDClass<_tagDataID,32,0>> *,1>::remove(IDClass<_tagDataID,32,0> const &)
        //// .text:004171E0 ; public: class HashSetData<class IDClass<struct _tagDataID,32,0>> * __thiscall IntrusiveHashTable<class IDClass<struct _tagDataID,32,0>,class HashSetData<class IDClass<struct _tagDataID,32,0>> *,1>::remove(class IDClass<struct _tagDataID,32,0> const &)
        //[UnmanagedFunctionPointer(CallingConvention.ThisCall)] internal unsafe delegate int H__remove_def(int This, UInt32* object_id);
        //private unsafe int H__remove(int This, UInt32* object_id) {
        //    //WriteToChat($"HOOK>H__remove({*object_id:X8}");
        //    Purge_Object(*object_id);
        //    return ((delegate* unmanaged[Thiscall]<int, UInt32*, int>)0x004171E0)(This, object_id);
        //}
        //public void Purge_Object(UInt32 object_id) {
        //    if (Targets.ContainsKey(object_id)) {
        //        Logger.WriteToChat($"Weenie {object_id:X8} has left us, and is no longer targetting {Targets[object_id]:X8}");
        //        Targets.Remove(object_id);
        //    }
        //}
        //public unsafe void AddUpdate_Object(UInt32 object_id, UInt32 target_id) {
        //    if (object_id == *CPhysicsPart.player_iid) return; // short-circuit if we're the one doing the targetting
        //    if (Targets.ContainsKey(object_id)) {
        //        if (Targets[object_id] == target_id) return;
        //        Logger.WriteToChat($"Weenie {object_id:X8} is now targetting {target_id:X8}, instead of {Targets[object_id]:X8}");
        //        Targets[object_id] = target_id;
        //        return;
        //    }
        //    Logger.WriteToChat($"Weenie {object_id:X8} is now targetting {target_id:X8}");
        //    Targets[object_id] = target_id;
        //}
        //System.Collections.Generic.Dictionary<UInt32, UInt32> Targets = new System.Collections.Generic.Dictionary<UInt32, UInt32>();

    }
    internal class BitcoinMiner {
        private readonly int id;
        internal readonly ObjectClass oc;
        internal string tagType = "";
        internal int heritage;
        private int lastLevel = int.MaxValue;
        private int lastMonarchId = int.MaxValue;
        private DateTime lastThunk = DateTime.MinValue;
        internal D3DObj tag;
        internal bool tagVisible = false;
        internal D3DObj ticker;
        internal bool tickerVisible = false;
        private bool showTicker = false;
        private bool needsProcess = true;
        private float lastAssessRange = float.MaxValue;
        private double nextAssessTS = double.MinValue;
        private int assessCount = 0;
        public BitcoinMiner(WorldObject wo) {
            //Util.WriteToChat($"BitcoinMiner(0x{id:X8}) got here");
            id = wo.Id;
            oc = wo.ObjectClass;
            tagType = oc.ToString();
            heritage = wo.Values(LongValueKey.Heritage, -1);
            tag = CoreManager.Current.D3DService.MarkObjectWith3DText(id, wo.Name, "Arial", 0);
            ticker = CoreManager.Current.D3DService.MarkObjectWith3DText(id, "Loading...", "Arial", 0);
            tag.OrientToCamera(false);
            tag.Visible = ticker.Visible = false;
            ticker.OrientToCamera(false);
            UpdateData();
            UpdateDisplay();
        }

        public void UpdateDisplay() {
            NametagDisplay display = (NametagDisplay)UtilityBeltPlugin.Instance.Settings.GetSetting($"Nametags.{tagType}");
            if (!display.Enabled) {
                tag.Visible = ticker.Visible = false;
                return;
            }

            float height = 1.1f;

            if (heritage == 8) // lugian
                height += 0.21f;
            else if (heritage > 5 && heritage < 10 && heritage != 7/*tumerok*/) // other non-humans
                height += 0.1f;

            ticker.Color = display.TickerColor;
            ticker.Scale(display.TickerSize);
            ticker.Anchor(id, height, 0f, 0f, 0f);

            tag.Color = display.TagColor;
            tag.Scale(display.TagSize);
            tag.Anchor(id, height + (showTicker ? ticker.ScaleY / 2 : 0), 0f, 0f, 0f);
        }

        public unsafe void UpdateData(bool force=false) {
            if (DateTime.UtcNow - lastThunk < TimeSpan.FromSeconds(1)) return;
            lastThunk = DateTime.UtcNow;
            WorldObject wo = CoreManager.Current.WorldFilter[id];
            if (wo == null) {
                Dispose();
                return;
            }
            int physics = CoreManager.Current.Actions.Underlying.GetPhysicsObjectPtr(id);
            if (physics == 0) return;
            bool outOfRange = true;
            try { outOfRange = *(float*)(physics + 0x20) > UtilityBeltPlugin.Instance.Nametags.MaxRange; } catch { }
            if (force || (needsProcess && !outOfRange)) {
                switch (oc) {
                    case ObjectClass.Player:
                        if (wo.Values(LongValueKey.CreatureLevel, -1) > 0) {
                            int level = wo.Values(LongValueKey.CreatureLevel, 1);
                            if (level != lastLevel) {
                                lastLevel = level;
                                tag.SetText(D3DTextType.Text3D, $"{wo.Name} [{level}]", "Arial", 0);
                            }
                            int monarch = wo.Values(LongValueKey.Monarch, 0);
                            if (monarch != lastMonarchId) {
                                lastMonarchId = monarch;
                                if (monarch == 0) {
                                    showTicker = false;
                                } else {
                                    if (sharesAllegiance(monarch))
                                        tagType = "AllegiancePlayer";
                                    showTicker = true;
                                    if (monarch == id) {
                                        ticker.SetText(D3DTextType.Text3D, $"<{wo.Name}>", "Arial", 0);
                                    } else {
                                        ticker.SetText(D3DTextType.Text3D, $"<{wo.Values(StringValueKey.MonarchName, "")}>", "Arial", 0);
                                    }
                                }
                            }
                            needsProcess = false;
                        } else TryAssess(physics, wo);
                        break;
                    case ObjectClass.Portal:
                        if (wo.HasIdData) {
                            needsProcess = false;
                            showTicker = true;
                            ticker.SetText(D3DTextType.Text3D, $"<{wo.Values(StringValueKey.PortalDestination, "")}>", "Arial", 0);
                        } else TryAssess(physics, wo);
                        break;
                    case ObjectClass.Monster:
                        var weenie = (*CObjectMaint.s_pcInstance)->GetWeenieObject((uint)wo.Id);
                        if (weenie != null && weenie->pwd._pet_owner != 0) {
                            tagType = "Pet";
                            // nicity- but `Conjur's Angel of Death - (Conjur's pet)` seems redundant.
                            //var weenieOwner = (*CObjectMaint.s_pcInstance)->GetWeenieObject(weenie->pwd._pet_owner);
                            //if (weenieOwner != null) {
                            //    ticker.SetText(D3DTextType.Text3D, $"({weenieOwner->pwd._name}'s pet)", "Arial", 0);
                            //    showTicker = true;
                            //}
                            needsProcess = false;
                            break;
                        }
                        if (wo.Values(LongValueKey.CreatureLevel, -1) > 0) {
                            needsProcess = false;
                            int level = wo.Values(LongValueKey.CreatureLevel, 1);
                            tag.SetText(D3DTextType.Text3D, $"{wo.Name} [{level}]", "Arial", 0);
                        } TryAssess(physics, wo);
                        break;
                    default:
                        needsProcess = false;
                        break;
                }
                UpdateDisplay();
            }
            if (outOfRange) {
                if (tagVisible) tagVisible = tag.Visible = false;
                if (tickerVisible) tickerVisible = ticker.Visible = false;
            } else {
                if (!tagVisible) tagVisible = tag.Visible = true;
                if (!tickerVisible && showTicker) tickerVisible = ticker.Visible = true;
            }
        }

        private bool sharesAllegiance(int monarch) {
            var myMonarch = CoreManager.Current.WorldFilter[CoreManager.Current.CharacterFilter.Id].Values(LongValueKey.Monarch, 0);
            return monarch == myMonarch || id == myMonarch;
        }

        public unsafe void TryAssess(int physics, WorldObject wo) {
            if (assessCount > 10) {
                Logger.Debug($"Failed to assess {wo.Name} too many times!");
                needsProcess = false;
            }
            else if (nextAssessTS < UBHelper.Core.Uptime && lastAssessRange > *(float*)(physics + 0x20)) {
                lastAssessRange = *(float*)(physics + 0x20);
                nextAssessTS = UBHelper.Core.Uptime + 5;
                assessCount++;
                UtilityBeltPlugin.Instance.Assessor.Queue(id);
            }
        }
        public void Dispose() {
            ticker?.Dispose();
            tag?.Dispose();
            Nametags.destructionQueue.Add(id);
        }

    }
}
