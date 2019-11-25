using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;

namespace UtilityBelt.Tools {
    public static class Nametags {
        private static Dictionary<int, BitcoinMiner> tags = new Dictionary<int, BitcoinMiner>();
        public static List<int> destructionQueue = new List<int>();
        public static Dictionary<ObjectClass, int> colors = new Dictionary<ObjectClass, int>(){{ObjectClass.Player,-16711681},{ObjectClass.Portal,-16711936},{ObjectClass.Npc,-256},{ObjectClass.Vendor,-65281},{ObjectClass.Monster,-65536}};
        public static Dictionary<ObjectClass, bool> enabled_types = new Dictionary<ObjectClass, bool>(){{ObjectClass.Player,true},{ObjectClass.Portal,true},{ObjectClass.Npc,true},{ObjectClass.Vendor,true},{ObjectClass.Monster,true}};
        public static float maxRange = 35f;
        public static bool enabled = false;
        private static DateTime evaluate_tags_time = DateTime.MinValue;
        public static void Init() {
            Globals.Settings.Nametags.PropertyChanged += Nametags_PropertyChanged;
            if (Globals.Settings.Nametags.Enabled) Enable();
        }
        private static void Enable() {
            enabled = true;
            Globals.Core.WorldFilter.CreateObject += WorldFilter_CreateObject;
            Globals.Core.WorldFilter.ChangeObject += WorldFilter_ChangeObject;
            Globals.Core.RenderFrame += Core_RenderFrame;
            evaluate_tags_time = DateTime.MinValue;
        }
        public static void Dispose() {
            Globals.Settings.Nametags.PropertyChanged -= Nametags_PropertyChanged;
            if (enabled) Disable();
        }
        public static void Disable() {
            enabled = false;
            Globals.Core.WorldFilter.CreateObject -= WorldFilter_CreateObject;
            Globals.Core.WorldFilter.ChangeObject -= WorldFilter_ChangeObject;
            Globals.Core.RenderFrame -= Core_RenderFrame;
            foreach (var i in tags) i.Value.Dispose();
            tags.Clear();
            destructionQueue.Clear();
        }
        private static void Nametags_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case "Enabled":
                    if (Globals.Settings.Nametags.Enabled && !enabled) Enable();
                    else if (!Globals.Settings.Nametags.Enabled && enabled) Disable();
                    return;
                case "MaxRange":
                    maxRange = Globals.Settings.Nametags.MaxRange;
                    return;
                case "Player":
                    enabled_types[ObjectClass.Player] = Globals.Settings.Nametags.Player.Enabled;
                    colors[ObjectClass.Player] = Globals.Settings.Nametags.Player.Color;
                    break;
                case "Portal":
                    enabled_types[ObjectClass.Portal] = Globals.Settings.Nametags.Portal.Enabled;
                    colors[ObjectClass.Portal] = Globals.Settings.Nametags.Portal.Color;
                    break;
                case "Npc":
                    enabled_types[ObjectClass.Npc] = Globals.Settings.Nametags.Npc.Enabled;
                    colors[ObjectClass.Npc] = Globals.Settings.Nametags.Npc.Color;
                    break;
                case "Vendor":
                    enabled_types[ObjectClass.Vendor] = Globals.Settings.Nametags.Vendor.Enabled;
                    colors[ObjectClass.Vendor] = Globals.Settings.Nametags.Vendor.Color;
                    break;
                case "Monster":
                    enabled_types[ObjectClass.Player] = Globals.Settings.Nametags.Monster.Enabled;
                    colors[ObjectClass.Player] = Globals.Settings.Nametags.Monster.Color;
                    break;
            }
            evaluate_tags_time = DateTime.UtcNow + TimeSpan.FromMilliseconds(250);
        }
        private static void AddTag(WorldObject wo) {
            if (wo.Id == Globals.Core.CharacterFilter.Id) return;
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
            AddTag(e.New);
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
                    foreach (WorldObject wo in Globals.Core.WorldFilter.GetLandscape()) AddTag(wo);
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
        public BitcoinMiner(WorldObject wo) {
            //Util.WriteToChat($"BitcoinMiner(0x{id:X8}) got here");
            id = wo.Id;
            oc = wo.ObjectClass;
            float lugianOffset = 0f;
            var heritage = wo.Values(LongValueKey.Heritage, -1);
            if (oc == ObjectClass.Player && heritage > 5 && heritage < 10) lugianOffset = 0.20f;
            tag = Globals.Core.D3DService.MarkObjectWith3DText(id, wo.Name, "Arial", 0);
            ticker = Globals.Core.D3DService.MarkObjectWith3DText(id, "Loading...", "Arial", 0);
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
            WorldObject wo = Globals.Core.WorldFilter[id];
            if (wo == null) {
                Dispose();
                return;
            }
            int physics = Globals.Core.Actions.Underlying.GetPhysicsObjectPtr(id);
            if (physics == 0) return;

            if (needsProcess) {
                switch (oc) {
                    case ObjectClass.Player:
                        if (wo.HasIdData) {
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
                        } else Globals.Assessor.Queue(id);
                        break;
                    case ObjectClass.Portal:
                        if (wo.HasIdData) {
                            needsProcess = false;
                            showTicker = true;
                            ticker.SetText(D3DTextType.Text3D, $"<{wo.Values(StringValueKey.PortalDestination, "")}>", "Arial", 0);
                        } else Globals.Assessor.Queue(id);
                        break;
                    case ObjectClass.Monster:
                        if (wo.HasIdData) {
                            needsProcess = false;
                            int level = wo.Values(LongValueKey.CreatureLevel, 1);
                            tag.SetText(D3DTextType.Text3D, $"{wo.Name} [{level}]", "Arial", 0);
                        } else Globals.Assessor.Queue(id);
                        break;
                    default:
                        needsProcess = false;
                        break;
                }
            }
            if (*(float*)(physics + 0x20) > Nametags.maxRange) {
                if (tagVisible) tagVisible = tag.Visible = false;
                if (tickerVisible) tickerVisible = ticker.Visible = false;
            } else {
                if (!tagVisible) tagVisible = tag.Visible = true;
                if (!tickerVisible && showTicker) tickerVisible = ticker.Visible = true;
            }
        }
        public void Dispose() {
            ticker?.Dispose();
            tag?.Dispose();
            Nametags.destructionQueue.Add(id);
        }

    }
}
