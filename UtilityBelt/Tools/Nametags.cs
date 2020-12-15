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
        internal static Dictionary<ObjectClass, int> colors = new Dictionary<ObjectClass, int>(){{ObjectClass.Player,-16711681},{ObjectClass.Portal,-16711936},{ObjectClass.Npc,-256},{ObjectClass.Vendor,-65281},{ObjectClass.Monster,-65536}};
        internal static Dictionary<ObjectClass, bool> enabled_types = new Dictionary<ObjectClass, bool>(){{ObjectClass.Player,true},{ObjectClass.Portal,true},{ObjectClass.Npc,true},{ObjectClass.Vendor,true},{ObjectClass.Monster,true}};
        internal static float maxRange = 35f;
        internal static bool enabled = false;
        private static DateTime evaluate_tags_time = DateTime.MinValue;

        #region Config
        [Summary("Enabled")]
        [Hotkey("NameTags", "Toggle NameTags display")]
        public readonly Setting<bool> Enabled = new Setting<bool>(true);

        [Summary("Maximum Range for Nametags")]
        public readonly Setting<float> MaxRange = new Setting<float>(35f);

        [Summary("Player Nametag")]
        public readonly ColorToggleOption Player = new ColorToggleOption(true, -16711681);

        [Summary("Portal Nametag")]
        public readonly ColorToggleOption Portal = new ColorToggleOption(true, -16711936);

        [Summary("Npc Nametag")]
        public readonly ColorToggleOption Npc = new ColorToggleOption(true, -256);

        [Summary("Vendor Nametag")]
        public readonly ColorToggleOption Vendor = new ColorToggleOption(true, -65281);

        [Summary("Monster Nametag")]
        public readonly ColorToggleOption Monster = new ColorToggleOption(true, -65536);
        #endregion

        public Nametags(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        public override void Init() {
            base.Init();
            Enabled.Changed += Nametags_PropertyChanged;
            MaxRange.Changed += Nametags_PropertyChanged;
            Player.Changed += Nametags_DisplayPropertyChanged;
            Portal.Changed += Nametags_DisplayPropertyChanged;
            Npc.Changed += Nametags_DisplayPropertyChanged;
            Vendor.Changed += Nametags_DisplayPropertyChanged;
            Monster.Changed += Nametags_DisplayPropertyChanged;
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
                    Enabled.Changed -= Nametags_PropertyChanged;
                    MaxRange.Changed -= Nametags_PropertyChanged;

                    Player.Changed -= Nametags_DisplayPropertyChanged;
                    Portal.Changed -= Nametags_DisplayPropertyChanged;
                    Npc.Changed -= Nametags_DisplayPropertyChanged;
                    Vendor.Changed -= Nametags_DisplayPropertyChanged;
                    Monster.Changed -= Nametags_DisplayPropertyChanged;
                    Disable();
                    base.Dispose(disposing);
                }
                disposedValue = true;
            }
        }
        private void Nametags_DisplayPropertyChanged(object sender, SettingChangedEventArgs e) {
            var parts = e.FullName.Split('.');
            var displayProperty = parts[parts.Length - 2];
            switch (displayProperty) {
                case "Player":
                    enabled_types[ObjectClass.Player] = Player.Enabled;
                    colors[ObjectClass.Player] = Player.Color;
                    break;
                case "Portal":
                    enabled_types[ObjectClass.Portal] = Portal.Enabled;
                    colors[ObjectClass.Portal] = Portal.Color;
                    break;
                case "Npc":
                    enabled_types[ObjectClass.Npc] = Npc.Enabled;
                    colors[ObjectClass.Npc] = Npc.Color;
                    break;
                case "Vendor":
                    enabled_types[ObjectClass.Vendor] = Vendor.Enabled;
                    colors[ObjectClass.Vendor] = Vendor.Color;
                    break;
                case "Monster":
                    enabled_types[ObjectClass.Monster] = Monster.Enabled;
                    colors[ObjectClass.Monster] = Monster.Color;
                    break;
            }
            evaluate_tags_time = DateTime.UtcNow + TimeSpan.FromMilliseconds(250);
        }
        private void Nametags_PropertyChanged(object sender, SettingChangedEventArgs e) {
            switch (e.PropertyName) {
                case "Enabled":
                    if (Enabled) Enable();
                    else Disable();
                    return;
                case "MaxRange":
                    maxRange = MaxRange;
                    return;
            }
            evaluate_tags_time = DateTime.UtcNow + TimeSpan.FromMilliseconds(250);
        }
        private static void AddTag(WorldObject wo) {
            if (wo.Id == CoreManager.Current.CharacterFilter.Id) return;
            if (tags.ContainsKey(wo.Id)) return;
            if (enabled_types.ContainsKey(wo.ObjectClass) && enabled_types[wo.ObjectClass]) tags.Add(wo.Id, new BitcoinMiner(wo));
        }
        private static void EvaluateTags() {
            foreach (KeyValuePair<int, BitcoinMiner> i in tags) {
                if (enabled_types.ContainsKey(i.Value.oc) && enabled_types[i.Value.oc]) {
                    i.Value.tag.Color = colors[i.Value.oc];
                    i.Value.ticker.Color = colors[i.Value.oc];
                    continue;
                }
                i.Value.Dispose();
            }
        }
        private static void WorldFilter_ChangeObject(object sender, ChangeObjectEventArgs e) {
            if (e.Change == WorldChangeType.IdentReceived) {
                if (tags.ContainsKey(e.Changed.Id)) {
                    var heritage = e.Changed.Values(LongValueKey.Heritage, -1);
                    if (e.Changed.ObjectClass == ObjectClass.Player && heritage > 5 && heritage < 10) {
                        tags[e.Changed.Id].tag.Anchor(e.Changed.Id, 0.20f + 1.22f, 0f, 0f, 0f);
                        tags[e.Changed.Id].ticker.Anchor(e.Changed.Id, 0.20f + 1.17f, 0f, 0f, 0f);
                    }
                }
            }
        }
        private static void WorldFilter_CreateObject(object sender, CreateObjectEventArgs e) {
            try {
                AddTag(e.New);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        private static void Core_RenderFrame(object sender, EventArgs e) {
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
            float lugianOffset = 0f;
            var heritage = wo.Values(LongValueKey.Heritage, -1);
            if (oc == ObjectClass.Player && heritage > 5 && heritage < 10) lugianOffset = 0.20f;
            tag = CoreManager.Current.D3DService.MarkObjectWith3DText(id, wo.Name, "Arial", 0);
            ticker = CoreManager.Current.D3DService.MarkObjectWith3DText(id, "Loading...", "Arial", 0);
            tag.Color = ticker.Color = Nametags.colors[oc];
            tag.Scale(0.15f);
            tag.Anchor(id, lugianOffset + 1.22f, 0f, 0f, 0f);
            tag.OrientToCamera(false);
            tag.Visible = ticker.Visible = false;
            ticker.Scale(0.1f);
            ticker.Anchor(id, lugianOffset + 1.17f, 0f, 0f, 0f);
            ticker.OrientToCamera(false);
            UpdateData();
        }

        public unsafe void UpdateData() {
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
            try { outOfRange = *(float*)(physics + 0x20) > Nametags.maxRange; } catch { }
            if (needsProcess && !outOfRange) {
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
                                    showTicker = true;
                                    if (monarch == id) {
                                        ticker.SetText(D3DTextType.Text3D, $"<{wo.Name}>", "Arial", 0);
                                    } else {
                                        ticker.SetText(D3DTextType.Text3D, $"<{wo.Values(StringValueKey.MonarchName, "")}>", "Arial", 0);
                                    }
                                }
                            }
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
            }
            if (outOfRange) {
                if (tagVisible) tagVisible = tag.Visible = false;
                if (tickerVisible) tickerVisible = ticker.Visible = false;
            } else {
                if (!tagVisible) tagVisible = tag.Visible = true;
                if (!tickerVisible && showTicker) tickerVisible = ticker.Visible = true;
            }
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
