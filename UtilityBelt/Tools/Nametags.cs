using Decal.Adapter;
using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Settings;
using UBLoader.Lib.Settings;

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
            if (UBHelper.VideoPatch.Enabled && !(UBHelper.VideoPatch.bgOnly && UBHelper.Core.isFocused)) DisableInternal();
            else EnableInternal();
        }

        /// <summary>
        /// Internal Enable function- for enabling internally, without affecting user preference
        /// </summary>
        private void EnableInternal() {
            if (!enabled) {
                enabled = true;
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
                    return;
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
            UpdateDisplay();
            UpdateData();
        }

        public void UpdateDisplay() {
            NametagDisplay display = (NametagDisplay)UtilityBeltPlugin.Instance.Settings.GetSetting($"Nametags.{tagType}");
            float height = 1.1f;

            if (heritage == 8) // lugian
                height += 0.21f;
            else if (heritage > 5 && heritage < 10) // other non-humans
                height += 0.1f;

            ticker.Color = display.TickerColor;
            ticker.Scale(display.TickerSize);
            ticker.Anchor(id, height, 0f, 0f, 0f);

            tag.Color = display.TagColor;
            tag.Scale(display.TagSize);
            tag.Anchor(id, height + (showTicker ? ticker.ScaleY/2 : 0), 0f, 0f, 0f);
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
                                    if (sharesAllegiance(monarch)) {
                                        tagType = "AllegiancePlayer";
                                        showTicker = true;
                                    }
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
