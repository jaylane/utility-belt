using AcClient;
using ACE.DatLoader;
using ACE.DatLoader.Entity;
using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using Harmony;
using ImGuiNET;
using Microsoft.DirectX.Direct3D;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using UBService;
using SkillBase = ACE.DatLoader.Entity.SkillBase;
using Vector4 = ImGuiNET.Vector4;
using WeaponType = ACE.Entity.Enum.WeaponType;

namespace UBLoader.Lib {
    public class CharacterCreation : IDisposable {
        private const uint MAX_CHAR_NAME_LENGTH = 32;

        private static readonly Random _random = new Random();

        public enum Skill {
            None,
            Axe,                 /* Retired */
            Bow,                 /* Retired */
            Crossbow,            /* Retired */
            Dagger,              /* Retired */
            Mace,                /* Retired */
            MeleeDefense,
            MissileDefense,
            Sling,               /* Retired */
            Spear,               /* Retired */
            Staff,               /* Retired */
            Sword,               /* Retired */
            ThrownWeapon,        /* Retired */
            UnarmedCombat,       /* Retired */
            ArcaneLore,
            MagicDefense,
            ManaConversion,
            Spellcraft,          /* Unimplemented */
            ItemTinkering,
            AssessPerson,
            Deception,
            Healing,
            Jump,
            Lockpick,
            Run,
            Awareness,           /* Unimplemented */
            ArmsAndArmorRepair,  /* Unimplemented */
            AssessCreature,
            WeaponTinkering,
            ArmorTinkering,
            MagicItemTinkering,
            CreatureEnchantment,
            ItemEnchantment,
            LifeMagic,
            WarMagic,
            Leadership,
            Loyalty,
            Fletching,
            Alchemy,
            Cooking,
            Salvaging,
            TwoHandedCombat,
            Gearcraft,           /* Retired */
            VoidMagic,
            HeavyWeapons,
            LightWeapons,
            FinesseWeapons,
            MissileWeapons,
            Shield,
            DualWield,
            Recklessness,
            SneakAttack,
            DirtyFighting,
            Challenge,          /* Unimplemented */
            Summoning
        }

        private class CharAttribute {
            public PropertyAttribute Type;
            public int Value = 10;
            public bool Locked = false;
            public byte[] ByteValue = new byte[4]; // max 3 + null

            public string Description { get; private set; } = "";

            public CharAttribute(PropertyAttribute type) {
                Type = type;
                PopulateData();
                Update();
            }

            private void PopulateData() {
                switch (Type) {
                    case PropertyAttribute.Strength:
                        Description = GetStringFromId(UI_Pregame_Strings, 0x06592518);
                        break;
                    case PropertyAttribute.Endurance:
                        Description = GetStringFromId(UI_Pregame_Strings, 0x06fecf05);
                        break;
                    case PropertyAttribute.Coordination:
                        Description = GetStringFromId(UI_Pregame_Strings, 0x04d1159e);
                        break;
                    case PropertyAttribute.Quickness:
                        Description = GetStringFromId(UI_Pregame_Strings, 0x02b8f5c3);
                        break;
                    case PropertyAttribute.Focus:
                        Description = GetStringFromId(UI_Pregame_Strings, 0x06ce87f3);
                        break;
                    case PropertyAttribute.Self:
                        Description = GetStringFromId(UI_Pregame_Strings, 0x036b87f6);
                        break;
                }
            }

            public void Update() {
                var empty = new byte[4] { 0, 0, 0, 0 };
                empty.CopyTo(ByteValue, 0);
                Encoding.ASCII.GetBytes(Value.ToString()).CopyTo(ByteValue, 0);
            }
        }

        private class CharSkill {
            public Skill Type { get; set; }
            public SkillBase Dat { get; set; }
            public SkillAdvancementClass Training { get; set; }
            public SkillAdvancementClass MinTraining { get; set; }
            public SkillAdvancementClass MaxTraining => Type == Skill.Salvaging ? SkillAdvancementClass.Trained : SkillAdvancementClass.Specialized;
            public int CreditsToLower {
                get {
                    if (Training == MinTraining)
                        return 0;
                    else if (Training == SkillAdvancementClass.Specialized)
                        return Dat.UpgradeCostFromTrainedToSpecialized;
                    else
                        return Dat.TrainedCost;
                }
            }
            public int CreditsToRaise {
                get {
                    if (Type == Skill.Salvaging)
                        return 0;
                    else if (Training == SkillAdvancementClass.Specialized)
                        return 0;
                    else if (Training == SkillAdvancementClass.Trained)
                        return Dat.UpgradeCostFromTrainedToSpecialized;
                    else
                        return Dat.TrainedCost;
                }
            }

            public int TrainedCost => HeritageSkill == null ? Dat.TrainedCost : HeritageSkill.NormalCost;
            public int SpecializedCost => HeritageSkill == null ? Dat.UpgradeCostFromTrainedToSpecialized : HeritageSkill.PrimaryCost;

            public bool CanLower => Training > MinTraining;
            public bool CanRaise => Training < MaxTraining;

            public int CreditsSpent {
                get {
                    if (Training == MinTraining)
                        return 0;
                    else if (Training == SkillAdvancementClass.Specialized)
                        return TrainedCost + SpecializedCost;
                    else
                        return TrainedCost;
                }
            }
            public SkillCG HeritageSkill { get; internal set; } = null;
            public string FormulaString {
                get {
                    var strBuilder = new StringBuilder();
                    if (Dat.Formula.Attr2 > 0) {
                        strBuilder.Append($"({(PropertyAttribute)Dat.Formula.Attr1} + {(PropertyAttribute)Dat.Formula.Attr2})");
                    }
                    else {
                        strBuilder.Append($"{(PropertyAttribute)Dat.Formula.Attr1}");
                    }
                    if (Dat.Formula.Z > 1)
                        strBuilder.Append($" / {Dat.Formula.Z}");

                    return strBuilder.ToString();
                }
            }

            public CharSkill(Skill type, SkillBase skill) {
                Type = type;
                Dat = skill;

                PopulateData();
            }

            private void PopulateData() {

            }

            internal bool TryRaise() {
                if (Training < MaxTraining) {
                    if (Training == SkillAdvancementClass.Inactive) {
                        Training += 2;
                    }
                    else {
                        Training++;
                    }
                    return true;
                }
                return false;
            }

            internal bool TryLower() {
                if (Training > MinTraining) {
                    if (Training == SkillAdvancementClass.Trained) {
                        Training -= 2;
                    }
                    else {
                        Training--;
                    }
                    return true;
                }
                return false;
            }
        }

        private class CharGenData {
            private PropertyAttribute _nextAttribute = PropertyAttribute.Strength;

            public string Name { get; set; } = "";
            public Heritage Heritage { get; set; }
            public Template Template { get; set; }

            public Dictionary<PropertyAttribute, CharAttribute> Attributes = new Dictionary<PropertyAttribute, CharAttribute>();
            public Dictionary<Skill, CharSkill> Skills = new Dictionary<Skill, CharSkill>();

            public int AvailableAttributeCredits => (int)Heritage.Dat.AttributeCredits - SpentAttributeCredits;
            public int SpentAttributeCredits => Attributes.Sum(kv => kv.Value.Value);

            public int AvailableSkillCredits => TotalSkillCredits - SpentSkillCredits;
            public int SpentSkillCredits => Skills.Values.Sum(s => s.CreditsSpent);

            public int TotalSkillCredits => (int)Heritage.Dat.SkillCredits;

            public CharGenData() {
                Attributes.Add(PropertyAttribute.Strength, new CharAttribute(PropertyAttribute.Strength));
                Attributes.Add(PropertyAttribute.Endurance, new CharAttribute(PropertyAttribute.Endurance));
                Attributes.Add(PropertyAttribute.Coordination, new CharAttribute(PropertyAttribute.Coordination));
                Attributes.Add(PropertyAttribute.Quickness, new CharAttribute(PropertyAttribute.Quickness));
                Attributes.Add(PropertyAttribute.Focus, new CharAttribute(PropertyAttribute.Focus));
                Attributes.Add(PropertyAttribute.Self, new CharAttribute(PropertyAttribute.Self));
            }

            internal void ApplyCurrentTemplate() {
                Attributes[PropertyAttribute.Strength].Value = (int)Template.Dat.Strength;
                Attributes[PropertyAttribute.Endurance].Value = (int)Template.Dat.Endurance;
                Attributes[PropertyAttribute.Coordination].Value = (int)Template.Dat.Coordination;
                Attributes[PropertyAttribute.Quickness].Value = (int)Template.Dat.Quickness;
                Attributes[PropertyAttribute.Focus].Value = (int)Template.Dat.Focus;
                Attributes[PropertyAttribute.Self].Value = (int)Template.Dat.Self;

                foreach (var attribute in Attributes) {
                    attribute.Value.Locked = false;
                    attribute.Value.Update();
                }

                foreach (var skillId in Template.Dat.NormalSkillsList) {
                    Skills[(Skill)skillId].Training = SkillAdvancementClass.Trained;
                }

                foreach (var skillId in Template.Dat.PrimarySkillsList) {
                    Skills[(Skill)skillId].Training = SkillAdvancementClass.Specialized;
                }
            }

            internal bool TryAdjustAttributesToFit(PropertyAttribute adjustedAttribute) {
                int hitReset = 0;
                while (AvailableAttributeCredits < 0) {
                    if (!Attributes[_nextAttribute].Locked && Attributes[_nextAttribute].Value > 10) {
                        Attributes[_nextAttribute].Value--;
                        Attributes[_nextAttribute].Update();
                    }

                    _nextAttribute++;
                    if (_nextAttribute > PropertyAttribute.Self) {
                        _nextAttribute = PropertyAttribute.Strength;
                        hitReset++;
                    }

                    if (hitReset >= 2)
                        return false;
                }
                return true;
            }

            internal void ResetSkills() {
                foreach (var kv in PortalDat.SkillTable.SkillBaseHash) {
                    var training = SkillAdvancementClass.Untrained;
                    if (AlwaysTrained.Contains((Skill)kv.Key)) {
                        training = SkillAdvancementClass.Trained;
                    }
                    else if ((SkillAdvancementClass)kv.Value.MinLevel > SkillAdvancementClass.Untrained) {
                        training = SkillAdvancementClass.Inactive;
                    }

                    if (!Skills.ContainsKey((Skill)kv.Key)) {
                        Skills.Add((Skill)kv.Key, new CharSkill((Skill)kv.Key, kv.Value));
                    }

                    Skills[(Skill)kv.Key].Training = training;
                    if (AlwaysTrained.Contains((Skill)kv.Key)) {
                        Skills[(Skill)kv.Key].MinTraining = SkillAdvancementClass.Trained;
                    }
                    else {
                        Skills[(Skill)kv.Key].MinTraining = training;
                    }

                    if (Heritage != null) {
                        var heritageSkill = Heritage.Dat.Skills.Where(s => s.SkillNum == (uint)kv.Key).FirstOrDefault();
                        if (heritageSkill != null) {
                            Skills[(Skill)kv.Key].HeritageSkill = heritageSkill;
                        }
                    }

                    if (Template != null) {
                        foreach (var skillId in Template.Dat.NormalSkillsList) {
                            Skills[(Skill)skillId].Training = SkillAdvancementClass.Trained;
                        }

                        foreach (var skillId in Template.Dat.PrimarySkillsList) {
                            Skills[(Skill)skillId].Training = SkillAdvancementClass.Specialized;
                        }
                    }
                }
            }

            internal bool CanRaiseSkill(CharSkill skill) {
                if (AvailableSkillCredits >= skill.CreditsToRaise) {
                    return skill.CanRaise;
                }
                return false;
            }

            internal bool TryRaiseSkill(CharSkill skill) {
                if (CanRaiseSkill(skill)) {
                    return skill.TryRaise();
                }
                return false;
            }

            internal bool CanLowerSkill(CharSkill skill) {
                return skill.CanLower;
            }

            internal bool TryLowerSkill(CharSkill skill) {
                if (CanLowerSkill(skill)) {
                    return skill.TryLower();
                }
                return false;
            }

            internal int SkillLevel(CharSkill skill) {
                var skillLevel = 0;
                if (skill.Training > SkillAdvancementClass.Inactive && skill.Dat.Formula.X > 0) {
                    skillLevel += Attributes[(PropertyAttribute)skill.Dat.Formula.Attr1].Value;
                    if (skill.Dat.Formula.Attr2 > 0) {
                        skillLevel += Attributes[(PropertyAttribute)skill.Dat.Formula.Attr2].Value;
                    }

                    skillLevel = (int)Math.Floor(((float)skillLevel / (float)skill.Dat.Formula.Z) + 0.5f);
                }

                if (skill.Training == SkillAdvancementClass.Trained)
                    skillLevel += 5;
                if (skill.Training == SkillAdvancementClass.Specialized)
                    skillLevel += 10;

                return skillLevel;
            }
        }

        private class Heritage {
            public uint Id { get; set; }
            public HeritageGroup Group { get; set; }
            public WeaponType MeleeMastery { get; set; }
            public WeaponType RangedMastery { get; set; }
            public HeritageGroupCG Dat { get; set; }
            public uint RacialBonusStringId { get; internal set; }
            public uint RacialDescriptionStringId { get; internal set; }
            public string RacialBonusString { get; internal set; } = "";
            public string RacialDescriptionString { get; internal set; } = "";
            public string DisplayName { get; internal set; } = "";
            public string TrainedStartingSkillsString { get; internal set; } = "";
            public string Tooltip { get; internal set; } = "";
            public uint TooltipStringId { get; internal set; }

            public int SelectedGenderIndex = 0;
            public int SelectedStarterArea = 0;

            public List<CharGender> Genders { get; } = new List<CharGender>();

            public Heritage(uint id, HeritageGroupCG dat) {
                Id = id;
                Dat = dat;
                PopulateHeritageData(this);

                foreach (var kv in Dat.Genders) {
                    Genders.Add(new CharGender(kv.Key, kv.Value));
                }
            }
        }

        private class CharGender {
            internal int SelectedEyeColor;
            internal int SelectedHairStyle;
            internal int SelectedHairColor;
            internal int SelectedEyeStrip;
            internal int SelectedNoseStrip;
            internal int SelectedMouthStrip;
            internal int SelectedHeadgear;
            internal int SelectedShirt;
            internal int SelectedPants;
            internal int SelectedFootwear;

            public int Id { get; set; }
            public SexCG Dat { get; set; }

            public CharGender(int id, SexCG dat) {
                Id = id;
                Dat = dat;
            }
        }

        private class Template {
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
            public string ToolTip { get; set; } = "";
            public TemplateCG Dat { get; set; }

            public Template(TemplateCG dat) {
                Dat = dat;
                PopulateData();
            }

            private void PopulateData() {
                Name = Dat.Name;

                switch (Name) {
                    case "Adventurer":
                        Description = GetStringFromId(UI_Pregame_Strings, 0x04ffcb24);
                        ToolTip = GetStringFromId(UI_Pregame_Strings, 0x0cb17a3d);
                        break;
                    case "Bow Hunter":
                        Description = GetStringFromId(UI_Pregame_Strings, 0x05db3724);
                        ToolTip = GetStringFromId(UI_Pregame_Strings, 0x06ce9072);
                        break;
                    case "Life Caster":
                        Description = GetStringFromId(UI_Pregame_Strings, 0x04b31214);
                        ToolTip = GetStringFromId(UI_Pregame_Strings, 0x0af598d2);
                        break;
                    case "War Mage":
                        Description = GetStringFromId(UI_Pregame_Strings, 0x01303754);
                        ToolTip = GetStringFromId(UI_Pregame_Strings, 0x0c5c0165);
                        break;
                    case "Soldier":
                        Description = GetStringFromId(UI_Pregame_Strings, 0x046159c4);
                        ToolTip = GetStringFromId(UI_Pregame_Strings, 0x099f9972);
                        break;
                    case "Swashbuckler":
                        Description = GetStringFromId(UI_Pregame_Strings, 0x0f060a94);
                        ToolTip = GetStringFromId(UI_Pregame_Strings, 0x020a5842);
                        break;
                    case "Wayfarer":
                        Description = GetStringFromId(UI_Pregame_Strings, 0x01393754);
                        ToolTip = GetStringFromId(UI_Pregame_Strings, 0x052fe302);
                        break;
                }
            }
        }

        private static PortalDatDatabase PortalDat => UBLoader.FilterCore.PortalDat;
        private static LanguageDatDatabase LanguageDat => UBLoader.FilterCore.LanguageDat;
        private static StringTable UI_Pregame_Strings;
        private readonly List<Template> Templates = new List<Template>();
        private readonly List<Heritage> Heritages = new List<Heritage>();
        private readonly List<StarterArea> StarterAreas = new List<StarterArea>();
        private string skillHelpText;
        private string attributeLockHelpText;
        private string attributeCreditesTooltipText;
        private string healthTooltipText;
        private string staminaTooltipText;
        private string manaTooltipText;
        private CharGenData charData;
        private string holtburgDescription;
        private string sanamarDescription;
        private string yaraqDescription;
        private string shoushiDescription;
        private UBService.Views.Hud hud;
        private Microsoft.DirectX.Direct3D.Texture LockOnTexture;
        private Microsoft.DirectX.Direct3D.Texture LockOffTexture;
        private Microsoft.DirectX.Direct3D.Texture ArrowUpTexture;
        private Microsoft.DirectX.Direct3D.Texture ArrowDownTexture;
        private Microsoft.DirectX.Direct3D.Texture ACMapTexture;
        // private Microsoft.DirectX.Direct3D.Texture CharAppearanceTexture;

        public class FinishedEventArgs : EventArgs {
            public AcClient.ACCharGenResult ACCharGenResult { get; }

            public FinishedEventArgs(AcClient.ACCharGenResult state) {
                ACCharGenResult = state;
            }
        }

        /// <summary>
        /// Called when the finish button is clicked. TODO: final validation
        /// </summary>
        public event EventHandler<FinishedEventArgs> OnFinished;

        public CharacterCreation() {
            LoadNeededData();

            hud = UBService.Views.HudManager.CreateHud("Create a character");
            hud.ShowInBar = true;
            hud.WindowSettings |= ImGuiWindowFlags.AlwaysAutoResize;
            hud.Render += Hud_Render;
            hud.PreRender += Hud_PreRender;
            hud.DestroyTextures += Hud_DestroyTextures;
            hud.CreateTextures += Hud_CreateTextures;
        }

        private void LoadNeededData() {
            try {
                UI_Pregame_Strings = LanguageDat.ReadFromDat<StringTable>(0x23000002);

                foreach (var startArea in PortalDat.CharGen.StarterAreas) {
                    StarterAreas.Add(startArea);
                }

                foreach (var t in PortalDat.CharGen.HeritageGroups.Values.First().Templates) {
                    Templates.Add(new Template(t));
                }

                foreach (var kv in PortalDat.CharGen.HeritageGroups) {
                    Heritages.Add(new Heritage(kv.Key, kv.Value));
                }

                skillHelpText = GetStringFromId(UI_Pregame_Strings, 0x01706653);
                attributeLockHelpText = GetStringFromId(UI_Pregame_Strings, 0x0d4852a2);
                attributeCreditesTooltipText = GetStringFromId(UI_Pregame_Strings, 0x011f0c43);
                healthTooltipText = GetStringFromId(UI_Pregame_Strings, 0x0cf6b0c8);
                staminaTooltipText = GetStringFromId(UI_Pregame_Strings, 0x0b7b0af1);
                manaTooltipText = GetStringFromId(UI_Pregame_Strings, 0x036d2b91);
                charData = new CharGenData();

                holtburgDescription = GetStringFromId(UI_Pregame_Strings, 0x0f4011c4);
                sanamarDescription = GetStringFromId(UI_Pregame_Strings, 0x08227f04);
                yaraqDescription = GetStringFromId(UI_Pregame_Strings, 0x0f1d0fc4);
                shoushiDescription = GetStringFromId(UI_Pregame_Strings, 0x024bcda4);

                charData.Heritage = Heritages[_currentHeritageIndex];
                charData.Template = Templates[_currentTemplateIndex];
                charData.ResetSkills();
                CreateTextures();
            }
            catch (Exception ex) { UBLoader.FilterCore.LogException(ex); }
        }
        public string SanitizeName(string _name) {
            // remove non-character/space characters
            string ret = Regex.Replace(_name, "[^a-zA-Z ]", String.Empty);
            // remove leading/trailing space
            ret = ret.Trim();
            // remove consecutive spaces
            ret = Regex.Replace(ret, @"[\s]{2,}", " ");
            // capitalize first character
            ret = char.ToUpper(ret[0]) + ret.Substring(1);
            return ret;
        }
        public unsafe AcClient.ACCharGenResult ToACCharGenResult() {
            ACCharGenResult result = new ACCharGenResult();
            int numSkills = (int)Skill.Summoning + 1;
            SKILL_ADVANCEMENT_CLASS* sacs = (SKILL_ADVANCEMENT_CLASS*)Marshal.AllocHGlobal(numSkills * 4);
            for (int i = 0; i < numSkills; i++) {
                sacs[i] = charData.Skills.ContainsKey((Skill)i) ? (SKILL_ADVANCEMENT_CLASS)charData.Skills[(Skill)i].Training : SKILL_ADVANCEMENT_CLASS.UNDEF;
            }
            AC1Legacy.PStringBase<char> name = SanitizeName(charData.Name);
            // todo- call this after return... because... yeah. that's possible...
            // temporary memory leak here.
            // Marshal.FreeHGlobal((IntPtr)sacs);

            result.CharGenResult = new AcClient.CharGenResult() { PackObj = new AcClient.PackObj() { vfptr = (AcClient.PackObj.Vtbl*)0x007E8940 } };
            result.heritageGroup = (uint)charData.Heritage.Group;
            result.gender = (uint)charData.Heritage.SelectedGenderIndex + 1;
            result.eyesStrip = charData.Heritage.Genders[charData.Heritage.SelectedGenderIndex].SelectedEyeStrip;
            result.noseStrip = charData.Heritage.Genders[charData.Heritage.SelectedGenderIndex].SelectedNoseStrip;
            result.mouthStrip = charData.Heritage.Genders[charData.Heritage.SelectedGenderIndex].SelectedMouthStrip;
            result.hairColor = charData.Heritage.Genders[charData.Heritage.SelectedGenderIndex].SelectedHairColor;
            result.eyeColor = charData.Heritage.Genders[charData.Heritage.SelectedGenderIndex].SelectedEyeColor;
            result.hairStyle = charData.Heritage.Genders[charData.Heritage.SelectedGenderIndex].SelectedHairStyle;
            result.headgearStyle = charData.Heritage.Genders[charData.Heritage.SelectedGenderIndex].SelectedHeadgear;
            result.shirtStyle = charData.Heritage.Genders[charData.Heritage.SelectedGenderIndex].SelectedShirt;
            result.trousersStyle = charData.Heritage.Genders[charData.Heritage.SelectedGenderIndex].SelectedPants;
            result.footwearStyle = charData.Heritage.Genders[charData.Heritage.SelectedGenderIndex].SelectedFootwear;
            result.headgearColor = 0; // TODO
            result.shirtColor = 0;
            result.trousersColor = 0;
            result.footwearColor = 0;
            result.skinShade = 0.71;
            result.hairShade = 0.74;
            result.headgearShade = 0.6;
            result.shirtShade = 0.48;
            result.trousersShade = 0.43;
            result.footwearShade = 0.69;
            result.templateNum = _currentTemplateIndex;
            result.strength = charData.Attributes[PropertyAttribute.Strength].Value; // this is total value, like 10 is 10 (minumum)
            result.endurance = charData.Attributes[PropertyAttribute.Endurance].Value; // this is total value, like 10 is 10 (minumum)
            result.coordination = charData.Attributes[PropertyAttribute.Coordination].Value; // this is total value, like 10 is 10 (minumum)
            result.quickness = charData.Attributes[PropertyAttribute.Quickness].Value; // this is total value, like 10 is 10 (minumum)
            result.focus = charData.Attributes[PropertyAttribute.Focus].Value; // this is total value, like 10 is 10 (minumum)
            result.self = charData.Attributes[PropertyAttribute.Self].Value; // this is total value, like 10 is 10 (minumum)
            result.numSkills = numSkills;
            result.skillAdvancementClasses = sacs;
            result.name = name;
            result.slot = 3;
            result.classID = 1;
            result.startArea = (uint)charData.Heritage.SelectedStarterArea;
            result.isAdmin = 0;
            result.isEnvoy = 0;

            return result;
        }

        // ui state
        private int _currentHeritageIndex = 0;
        private readonly byte[] _charNameData = new byte[MAX_CHAR_NAME_LENGTH + 1];
        private int _currentTemplateIndex = 0;

        private unsafe void Hud_Render(object sender, EventArgs e) {
            try {
                var io = ImGui.GetIO();
                charData.Heritage = Heritages[_currentHeritageIndex];
                charData.Template = Templates[_currentTemplateIndex];
                //charData.Update();
                bool isOlthoi = charData.Heritage.MeleeMastery == WeaponType.Undef;

                if (ImGui.BeginTabBar("Character Gen", ImGuiTabBarFlags.None)) {
                    if (ImGui.BeginTabItem("Heritage")) {
                        var headerColor = new Vector4(0, 255, 0, 255);

                        ImGui.BeginGroup(); // heritage selection radios
                        for (var i = 0; i < Heritages.Count; i++) {
                            if (ImGui.RadioButton(Heritages[i].DisplayName, i == _currentHeritageIndex)) {
                                _currentHeritageIndex = i;
                            }

                            if (ImGui.IsItemHovered()) {
                                ImGui.BeginTooltip(); // mana tooltip
                                {
                                    ImGui.BeginChild($"Tooltip{Heritages[i].DisplayName}", new Vector2(280, 28));
                                    ImGui.TextWrapped(Heritages[i].Tooltip);
                                    ImGui.EndChild();
                                }
                                ImGui.EndTooltip(); // mana tooltip
                            }
                        }
                        ImGui.EndGroup(); // heritage selection radios
                        ImGui.SameLine(0, 20);

                        ImGui.BeginGroup(); // heritage description text
                        {
                            // force child size to enable scrollable 
                            ImGui.BeginChild(1, new Vector2(360, 300)); // racial information
                            {
                                ImGui.TextColored(headerColor, "Trained Starting Skills:");
                                ImGui.TextWrapped(charData.Heritage.TrainedStartingSkillsString);
                                ImGui.Spacing(); ImGui.Spacing(); ImGui.Spacing();

                                ImGui.TextColored(headerColor, "Bonus Racial Skills:");
                                ImGui.TextWrapped(charData.Heritage.RacialBonusString);
                                ImGui.Spacing(); ImGui.Spacing(); ImGui.Spacing(); ImGui.Spacing();

                                ImGui.TextWrapped(charData.Heritage.RacialDescriptionString);
                            }
                            ImGui.EndChild(); // racial information 
                        }
                        ImGui.EndGroup(); // heritage description text
                        ImGui.EndTabItem();
                    }

                    if (!isOlthoi && ImGui.BeginTabItem("Profession")) {
                        ImGui.BeginGroup(); // template selection
                        {
                            for (var i = 0; i < Templates.Count; i++) {
                                var template = Templates[i];
                                var name = i == 0 ? "Custom" : template.Name;
                                if (ImGui.RadioButton(name, i == _currentTemplateIndex)) {
                                    _currentTemplateIndex = i;
                                    charData.Template = Templates[_currentTemplateIndex];
                                    charData.ResetSkills();
                                    charData.ApplyCurrentTemplate();
                                }
                                if (ImGui.IsItemHovered()) {
                                    ImGui.BeginTooltip(); // template info
                                    {
                                        ImGui.BeginChild(template.Name, new Vector2(240, 50));
                                        ImGui.TextWrapped(template.ToolTip);
                                        ImGui.EndChild();
                                    }
                                    ImGui.EndTooltip(); // template info
                                }
                            }

                            ImGui.BeginChild(1, new Vector2(120, 100)); // template description
                            {
                                ImGui.TextWrapped(charData.Template.Description);
                            }
                            ImGui.EndChild(); // template description
                        }
                        ImGui.EndGroup(); // template selection
                        ImGui.SameLine(0, 10);
                        ImGui.BeginGroup(); // attribute selections
                        {
                            ImGui.Text($"Attribute Credits: {charData.AvailableAttributeCredits}");

                            if (ImGui.IsItemHovered()) {
                                ImGui.BeginTooltip(); // Attribute Credits
                                {
                                    ImGui.BeginChild("attribute credits", new Vector2(300, 54));
                                    ImGui.TextWrapped(attributeCreditesTooltipText);
                                    ImGui.EndChild();
                                }
                                ImGui.EndTooltip(); // Attribute Credits
                            }

                            var _id = 0;
                            var lockIconSize = new Vector2(18, 18);
                            foreach (var attr in charData.Attributes.Values) {
                                if (LockOnTexture == null || LockOffTexture == null)
                                    continue;
                                int startValue = attr.Value;
                                ImGui.PushID(_id++); // image button id 
                                {
                                    ImGui.PushStyleColor(ImGuiCol.Button, 0x00000000); // button color
                                    if (ImGui.ImageButton(attr.Locked ? (IntPtr)LockOnTexture.UnmanagedComPointer : (IntPtr)LockOffTexture.UnmanagedComPointer, lockIconSize, new Vector2(0, 0), new Vector2(1, 1), 0, new Vector4())) {
                                        attr.Locked = !attr.Locked;
                                        attr.Update();
                                    }
                                    ImGui.PopStyleColor(); // button color
                                    ImGui.SameLine();
                                }
                                ImGui.PopID(); // image button id
                                if (ImGui.IsItemHovered()) {
                                    ImGui.BeginTooltip(); // attr lock button
                                    {
                                        ImGui.BeginChild(attr.Type.ToString(), new Vector2(240, 54));
                                        ImGui.TextWrapped(attributeLockHelpText);
                                        ImGui.EndChild();
                                    }
                                    ImGui.EndTooltip(); // attr lock button
                                }

                                ImGui.BeginGroup();
                                {
                                    if (ImGui.SliderInt(attr.Type.ToString(), ref attr.Value, 10, 100, "%d", ImGuiSliderFlags.AlwaysClamp)) {
                                        if (!charData.TryAdjustAttributesToFit(attr.Type)) {
                                            attr.Value = startValue;
                                        }
                                        attr.Update();
                                    }
                                    ImGui.SameLine(306);
                                    if (ImGui.InputText($"##edit{attr.Type}", attr.ByteValue, (uint)attr.ByteValue.Length, ImGuiInputTextFlags.CharsDecimal, null)) {
                                        if (int.TryParse(Encoding.ASCII.GetString(attr.ByteValue), System.Globalization.NumberStyles.Integer, null, out int result)) {
                                            attr.Value = Math.Min(100, Math.Max(10, result));
                                            if (!charData.TryAdjustAttributesToFit(attr.Type)) {
                                                attr.Value = startValue;
                                            }
                                        }
                                        attr.Update();
                                    }
                                }
                                ImGui.EndGroup();
                                if (ImGui.IsItemHovered()) {
                                    ImGui.BeginTooltip(); // template info
                                    {
                                        ImGui.BeginChild(attr.Type.ToString(), new Vector2(280, 80));
                                        ImGui.TextWrapped(attr.Description);
                                        ImGui.EndChild();
                                    }
                                    ImGui.EndTooltip(); // template info
                                }
                            }
                        }
                        for (var i = 0; i < 5; i++) {
                            ImGui.Spacing();
                        }

                        RenderVital("Health: ", ((int)Math.Floor(charData.Attributes[PropertyAttribute.Endurance].Value / 2f)).ToString(), healthTooltipText);
                        RenderVital("Stamina: ", charData.Attributes[PropertyAttribute.Endurance].Value.ToString(), staminaTooltipText);
                        RenderVital("Mana: ", charData.Attributes[PropertyAttribute.Self].Value.ToString(), manaTooltipText);

                        ImGui.EndGroup(); // attribute selections
                        ImGui.EndTabItem();
                    }

                    if (!isOlthoi && ImGui.BeginTabItem("Skills")) {
                        ImGui.Text($"Available Skill Credits: {charData.AvailableSkillCredits}");
                        ImGui.BeginGroup(); // left col
                        {
                            ImGui.BeginChild(1, new Vector2(340, 240)); // skills table
                            {
                                RenderSkillTable(SkillAdvancementClass.Specialized);
                                RenderSkillTable(SkillAdvancementClass.Trained);
                                RenderSkillTable(SkillAdvancementClass.Untrained);
                                RenderSkillTable(SkillAdvancementClass.Inactive);
                            }
                            ImGui.EndChild(); // skills table
                            ImGui.Spacing();
                            ImGui.Spacing();
                            ImGui.BeginChild(2, new Vector2(340, 80)); // skills description
                            {
                                if (_selectedSkill != 0) {
                                    var selectedSkill = charData.Skills[_selectedSkill];
                                    if (selectedSkill != null) {
                                        ImGui.Text($"{selectedSkill.Dat.Name} ({charData.SkillLevel(selectedSkill)})");
                                        ImGui.TextWrapped(selectedSkill.Dat.Description);
                                        ImGui.Text($"Formula: {selectedSkill.FormulaString}");
                                    }
                                }
                            }
                            ImGui.EndChild(); // skills description
                        }
                        ImGui.EndGroup(); // left col

                        ImGui.SameLine(0, 10);
                        ImGui.BeginChild(3, new Vector2(150, 280)); // skills help text
                        {
                            ImGui.TextWrapped(skillHelpText);
                        }
                        ImGui.EndChild(); // skills help text
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Appearance")) {
                        ImGui.Text("Appearance");

                        var genderNames = charData.Heritage.Genders.Select(g => g.Dat.Name + "\0").ToArray();
                        ImGui.Combo("Gender", ref charData.Heritage.SelectedGenderIndex, string.Join("", genderNames));

                        var currentGender = charData.Heritage.Genders[charData.Heritage.SelectedGenderIndex];

                        if (charData.Heritage.Group == HeritageGroup.Gearknight || isOlthoi) {
                            ImGui.Text($"Olthoi / Gear Knights appearance not supported...");
                        }
                        else {
                            var hairStyles = currentGender.Dat.HairStyleList.Select(g => $"0x{g.ObjDesc.GetHashCode():X8}\0").ToArray();
                            ImGui.Combo("Hair Style", ref currentGender.SelectedHairStyle, string.Join("", hairStyles));

                            var hairColors = currentGender.Dat.HairColorList.Select(g => $"0x{g:X8}\0").ToArray();
                            ImGui.Combo("Hair Color", ref currentGender.SelectedHairColor, string.Join("", hairColors));

                            var eyeStrips = currentGender.Dat.EyeStripList.Select(g => $"0x{g.ObjDesc.GetHashCode():X8}\0").ToArray();
                            ImGui.Combo("Eye Strip", ref currentGender.SelectedEyeStrip, string.Join("", eyeStrips));

                            var eyeColors = currentGender.Dat.EyeColorList.Select(g => $"0x{g:X8}\0").ToArray();
                            ImGui.Combo("Eye Color", ref currentGender.SelectedEyeColor, string.Join("", eyeColors));

                            var noseStrips = currentGender.Dat.NoseStripList.Select(g => $"0x{g.ObjDesc.GetHashCode():X8}\0").ToArray();
                            ImGui.Combo("Nose Strip", ref currentGender.SelectedNoseStrip, string.Join("", noseStrips));

                            var mouthStrips = currentGender.Dat.MouthStripList.Select(g => $"0x{g.ObjDesc.GetHashCode():X8}\0").ToArray();
                            ImGui.Combo("Mouth Strip", ref currentGender.SelectedMouthStrip, string.Join("", mouthStrips));

                            var headgear = currentGender.Dat.HeadgearList.Select(g => $"{g.Name}\0").ToArray();
                            ImGui.Combo("Headgear", ref currentGender.SelectedHeadgear, string.Join("", headgear));

                            var shirts = currentGender.Dat.ShirtList.Select(g => $"{g.Name}\0").ToArray();
                            ImGui.Combo("Shirt", ref currentGender.SelectedShirt, string.Join("", shirts));

                            var pants = currentGender.Dat.PantsList.Select(g => $"{g.Name}\0").ToArray();
                            ImGui.Combo("Pants", ref currentGender.SelectedPants, string.Join("", pants));

                            var footwear = currentGender.Dat.FootwearList.Select(g => $"{g.Name}\0").ToArray();
                            ImGui.Combo("Footwear", ref currentGender.SelectedFootwear, string.Join("", footwear));
                        }

                        ImGui.EndTabItem();
                    }

                    if (!isOlthoi && ImGui.BeginTabItem("Town")) {
                        var starterArea = StarterAreas[charData.Heritage.SelectedStarterArea];

                        ImGui.Text(starterArea.Name);
                        ImGui.Spacing();
                        ImGui.BeginChild("StarterAreaDescription", new Vector2(188, 300)); // starter area description
                        {
                            switch (starterArea.Name) {
                                case "Holtburg":
                                    ImGui.TextWrapped(holtburgDescription);
                                    break;
                                case "Sanamar":
                                    ImGui.TextWrapped(sanamarDescription);
                                    break;
                                case "Yaraq":
                                    ImGui.TextWrapped(yaraqDescription);
                                    break;
                                case "Shoushi":
                                    ImGui.TextWrapped(shoushiDescription);
                                    break;
                            }
                        }
                        ImGui.EndChild(); // starter area description
                        ImGui.SameLine();

                        ImGui.PushID(1); // image button id 
                        {
                            ImGui.PushStyleColor(ImGuiCol.Button, 0x00000000); // button color
                            var tint = new Vector4(1, 1, 1, 1);
                            var size = new Vector2(300, 300);
                            if (ImGui.ImageButton((IntPtr)ACMapTexture.UnmanagedComPointer, size, new Vector2(0, 0), new Vector2(1, 1), 0, new Vector4(), tint)) {
                                var windowPos = ImGui.GetWindowPos();
                                var c = new Vector2(io.MousePos.X - windowPos.X, io.MousePos.Y - windowPos.Y);
                                //Logger.WriteToChat($"Clicked: {io.MousePos.X - windowPos.X}, {io.MousePos.Y - windowPos.Y}");
                                // 19:21:04 [UB] Clicked: 372, 150
                                // 19:21:08 [UB] Clicked: 438, 169
                                if (c.X >= 372 && c.X <= 438 && c.Y >= 150 && c.Y <= 169) {
                                    charData.Heritage.SelectedStarterArea = 0;
                                }
                                // 19:24:19 [UB] Clicked: 437, 271
                                // 19:24:22 [UB] Clicked: 495, 290
                                else if (c.X >= 437 && c.X <= 495 && c.Y >= 271 && c.Y <= 290) {
                                    charData.Heritage.SelectedStarterArea = 1;
                                }
                                // 19:28:56 [UB] Clicked: 337, 238
                                // 19:28:58 [UB] Clicked: 382, 261
                                else if (c.X >= 337 && c.X <= 382 && c.Y >= 238 && c.Y <= 261) {
                                    charData.Heritage.SelectedStarterArea = 2;
                                }
                                // 19:30:38 [UB] Clicked: 237, 98
                                // 19:30:40 [UB] Clicked: 296, 120
                                else if (c.X >= 237 && c.X <= 296 && c.Y >= 98 && c.Y <= 120) {
                                    charData.Heritage.SelectedStarterArea = 3;
                                }
                            }
                            ImGui.PopStyleColor(); // button color
                        }
                        ImGui.PopID(); // image button id

                        var cursor = new Vector2(ImGui.GetCursorPosX(), ImGui.GetCursorPosY());

                        ImGui.PushStyleColor(ImGuiCol.Button, charData.Heritage.SelectedStarterArea == 0 ? 0xff008800 : 0x55008800); // button color
                        var holtPos = new Vector2(cursor.X + 365, cursor.Y - 225);
                        ImGui.SetCursorPos(holtPos);
                        if (ImGui.Button("Holtburg")) {
                            charData.Heritage.SelectedStarterArea = 0;
                        }
                        ImGui.PopStyleColor(); // button color


                        ImGui.PushStyleColor(ImGuiCol.Button, charData.Heritage.SelectedStarterArea == 1 ? 0xff008800 : 0x55008800); // button color
                        var shoushiPos = new Vector2(cursor.X + 430, cursor.Y - 105);
                        ImGui.SetCursorPos(shoushiPos);
                        if (ImGui.Button("Shoushi")) {
                            charData.Heritage.SelectedStarterArea = 1;
                        }
                        ImGui.PopStyleColor(); // button color


                        ImGui.PushStyleColor(ImGuiCol.Button, charData.Heritage.SelectedStarterArea == 2 ? 0xff008800 : 0x55008800); // button color
                        var yaraqPos = new Vector2(cursor.X + 330, cursor.Y - 135);
                        ImGui.SetCursorPos(yaraqPos);
                        if (ImGui.Button("Yaraq")) {
                            charData.Heritage.SelectedStarterArea = 2;
                        }
                        ImGui.PopStyleColor(); // button color

                        ImGui.PushStyleColor(ImGuiCol.Button, charData.Heritage.SelectedStarterArea == 3 ? 0xff008800 : 0x55008800); // button color
                        var sanamarPos = new Vector2(cursor.X + 230, cursor.Y - 275);
                        ImGui.SetCursorPos(sanamarPos);
                        if (ImGui.Button("Sanamar")) {
                            charData.Heritage.SelectedStarterArea = 3;
                        }
                        ImGui.PopStyleColor(); // button color


                        ImGui.SetCursorPos(cursor);

                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Summary")) {
                        ImGui.BeginChild("Summary", new Vector2(240, 300)); // summary
                        {
                            //var indent = 10;
                            var colSize = 140;
                            ImGui.Text("Profession:"); ImGui.SameLine(colSize); ImGui.Text(charData.Template.Name);
                            ImGui.Text("Gender:"); ImGui.SameLine(colSize); ImGui.Text(charData.Heritage.Genders[charData.Heritage.SelectedGenderIndex].Dat.Name);
                            ImGui.Text("Heritage:"); ImGui.SameLine(colSize); ImGui.Text(charData.Heritage.DisplayName);
                            ImGui.Text("Starting Town:"); ImGui.SameLine(colSize); ImGui.Text(StarterAreas[charData.Heritage.SelectedStarterArea].Name);
                            ImGui.Spacing();
                            ImGui.Spacing();
                            ImGui.Text("Attributes:");
                            ImGui.Spacing();

                            foreach (var attribute in charData.Attributes.Values) {
                                ImGui.Text(attribute.Type.ToString()); ImGui.SameLine(colSize); ImGui.Text(attribute.Value.ToString());
                            }

                            ImGui.Text("Health"); ImGui.SameLine(colSize);
                            ImGui.Text(((int)Math.Floor(charData.Attributes[PropertyAttribute.Endurance].Value / 2f)).ToString());
                            ImGui.Text("Stamina"); ImGui.SameLine(colSize);
                            ImGui.Text(charData.Attributes[PropertyAttribute.Endurance].Value.ToString());
                            ImGui.Text("Mana"); ImGui.SameLine(colSize);
                            ImGui.Text(charData.Attributes[PropertyAttribute.Self].Value.ToString());

                            RenderSkillSummary(SkillAdvancementClass.Specialized, colSize);
                            RenderSkillSummary(SkillAdvancementClass.Trained, colSize);
                            RenderSkillSummary(SkillAdvancementClass.Untrained, colSize);
                            RenderSkillSummary(SkillAdvancementClass.Inactive, colSize);
                        }
                        ImGui.EndChild(); // summary
                        ImGui.SameLine();

                        ImGui.BeginGroup(); // name / finish
                        if (ImGui.InputText("Name", _charNameData, MAX_CHAR_NAME_LENGTH, ImGuiInputTextFlags.None, null)) {
                            charData.Name = Encoding.UTF8.GetString(_charNameData).ToString();
                        }

                        ImGui.Spacing();
                        ImGui.Spacing();
                        ImGui.Spacing();

                        if (ImGui.Button("Finish", new Vector2(240, 100))) {
                            var state = ToACCharGenResult();
                            //Logger.WriteToChat($"FINISHED: {state}");
                            OnFinished?.Invoke(this, new FinishedEventArgs(state));
                        }
                        ImGui.EndGroup(); // name / finish

                        ImGui.EndTabItem();
                    }
                    ImGui.EndTabBar();
                }
            }
            catch (Exception ex) { UBLoader.FilterCore.LogException(ex); }
        }

        private void RenderSkillSummary(SkillAdvancementClass training, int colSize) {
            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.Text($"{training} Skills:");
            ImGui.Spacing();
            var skills = charData.Skills.Values.Where(s => s.Training == training).ToList();
            skills.Sort((a, b) => a.Dat.Name.CompareTo(b.Dat.Name));
            foreach (var skill in skills) {
                ImGui.Text(skill.Type.ToString()); ImGui.SameLine(colSize); ImGui.Text(charData.SkillLevel(skill).ToString());
            }
        }

        private void RenderVital(string label, string value, string tooltip) {

            ImGui.BeginGroup(); // vital
            {
                ImGui.Spacing();
                ImGui.SameLine(238);
                ImGui.Text(label);
                ImGui.SameLine(330);
                ImGui.Text(value);
            }
            ImGui.EndGroup(); // vital
            if (ImGui.IsItemHovered()) {
                ImGui.BeginTooltip(); // vital tooltip
                {
                    ImGui.BeginChild($"{label}Tooltip", new Vector2(280, 72));
                    ImGui.TextWrapped(tooltip);
                    ImGui.EndChild();
                }
                ImGui.EndTooltip(); // vital tooltip
            }
        }

        private void RenderSkillTable(SkillAdvancementClass training) {
            if (ImGui.BeginTable($"{training}SkillsTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.NoPadOuterX | ImGuiTableFlags.RowBg)) {
                RenderSkillTrainingHeader(training == SkillAdvancementClass.Inactive ? "Unuseable" : training.ToString());
                RenderSkillRows(training);
                ImGui.EndTable();
            }
        }

        private void RenderSkillRows(SkillAdvancementClass training) {
            var skills = charData.Skills.Values.Where(s => s.Training == training).ToList();
            skills.Sort((a, b) => a.Dat.Name.CompareTo(b.Dat.Name));
            foreach (var skill in skills) {
                RenderSkillRow(skill);
            }
        }

        private void RenderSkillTrainingHeader(string v) {
            ImGui.TableSetupColumn($"{v} Skills", ImGuiTableColumnFlags.WidthFixed, 150);
            ImGui.TableSetupColumn("Level");
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableHeadersRow();
            ImGui.TableNextColumn();
        }

        private unsafe void RenderSkillRow(CharSkill skill) {
            ImGui.TableNextRow(ImGuiTableRowFlags.None);
            ImGui.TableNextColumn();

            ImGui.AlignTextToFramePadding();
            if (ImGui.Selectable($"{skill.Dat.Name}", _selectedSkill == skill.Type)) {
                //Logger.WriteToChat($"Selected: {skill.Dat.Name}");
                _selectedSkill = skill.Type;
            }
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text(charData.SkillLevel(skill).ToString());
            ImGui.TableNextColumn();
            ImGui.BeginGroup(); // actions
            {
                var iconSize = new Vector2(15, 15);
                ImGui.BeginTable($"actions{skill.Type}", 4, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.NoPadInnerX | ImGuiTableFlags.NoPadOuterX); // actions
                {
                    ImGui.TableNextRow(ImGuiTableRowFlags.None);
                    ImGui.TableNextColumn();
                    ImGui.Text(skill.CreditsToRaise.ToString());
                    ImGui.TableNextColumn();
                    ImGui.PushID(skill.Type.GetHashCode()); // image button id 
                    {
                        ImGui.PushStyleColor(ImGuiCol.Button, 0x00000000); // button color
                        var tint = charData.CanRaiseSkill(skill) ? new Vector4(1, 1, 1, 1) : new Vector4(0.5f, 0.5f, 0.5f, 1);
                        if (ImGui.ImageButton((IntPtr)ArrowUpTexture.UnmanagedComPointer, iconSize, new Vector2(0, 0), new Vector2(1, 1), 0, new Vector4(), tint)) {
                            charData.TryRaiseSkill(skill);
                        }
                        ImGui.PopStyleColor(); // button color
                    }
                    ImGui.PopID(); // image button id
                    ImGui.TableNextColumn();
                    ImGui.PushID(skill.Type.GetHashCode() + 1); // image button id 
                    {
                        ImGui.PushStyleColor(ImGuiCol.Button, 0x00000000); // button color
                        var tint = charData.CanLowerSkill(skill) ? new Vector4(1, 1, 1, 1) : new Vector4(0.5f, 0.5f, 0.5f, 1);
                        if (ImGui.ImageButton((IntPtr)ArrowDownTexture.UnmanagedComPointer, iconSize, new Vector2(0, 0), new Vector2(1, 1), 0, new Vector4(), tint)) {
                            charData.TryLowerSkill(skill);
                        }
                        ImGui.PopStyleColor(); // button color
                    }
                    ImGui.PopID(); // image button id
                    ImGui.TableNextColumn();
                    ImGui.Text(skill.CreditsToLower.ToString());
                }
                ImGui.EndTable(); // actions
            }
            ImGui.EndGroup(); // actions
        }

        private void Hud_PreRender(object sender, EventArgs e) {
            try {
                ImGui.SetNextWindowSize(new Vector2(510, -1));
            }
            catch (Exception ex) { UBLoader.FilterCore.LogException(ex); }
        }

        private Microsoft.DirectX.Direct3D.Texture LoadTextureFromResouce(string resourcePath) {
            using (Stream manifestResourceStream = GetType().Assembly.GetManifestResourceStream(resourcePath)) {
                using (Bitmap bmp = new Bitmap(manifestResourceStream)) {
                    return new Microsoft.DirectX.Direct3D.Texture(UBLoader.FilterCore.D3Ddevice, bmp, Usage.Dynamic, Pool.Default);
                }
            }
        }

        private void Hud_CreateTextures(object sender, EventArgs e) {
            try {
                CreateTextures();
            }
            catch (Exception ex) { UBLoader.FilterCore.LogException(ex); }
        }

        private void Hud_DestroyTextures(object sender, EventArgs e) {
            try {
                DestroyTextures();
            }
            catch (Exception ex) { UBLoader.FilterCore.LogException(ex); }
        }

        private void CreateTextures() {
            try {
                CreateTextureFromResource(ref LockOnTexture, "UBLoader.Resources.outline-lock-on.png");
                CreateTextureFromResource(ref LockOffTexture, "UBLoader.Resources.outline-lock-off.png");
                CreateTextureFromResource(ref ArrowUpTexture, "UBLoader.Resources.outline-arrow-up-round.png");
                CreateTextureFromResource(ref ArrowDownTexture, "UBLoader.Resources.outline-arrow-down-round.png");
                CreateTextureFromResource(ref ACMapTexture, "UBLoader.Resources.acmap-small.png");
            }
            catch (Exception ex) { UBLoader.FilterCore.LogException(ex); }
        }

        private void CreateTextureFromResource(ref Microsoft.DirectX.Direct3D.Texture lockOnTexture, string v) {
            if (lockOnTexture == null)
                lockOnTexture = LoadTextureFromResouce(v);
        }

        private void DestroyTextures() {
            try {
                DestroyTexture(ref LockOnTexture);
                DestroyTexture(ref LockOffTexture);
                DestroyTexture(ref ArrowUpTexture);
                DestroyTexture(ref ArrowDownTexture);
                DestroyTexture(ref ACMapTexture);
            }
            catch (Exception ex) { UBLoader.FilterCore.LogException(ex); }
        }

        private void DestroyTexture(ref Microsoft.DirectX.Direct3D.Texture texture) {
            texture?.Dispose();
            texture = null;
        }

        private static readonly List<Skill> AlwaysTrained = new List<Skill>()
        {
            Skill.ArcaneLore,
            Skill.Jump,
            Skill.Loyalty,
            Skill.MagicDefense,
            Skill.Run,
            Skill.Salvaging
        };
        private Skill _selectedSkill;
        // private int _selectedGenderIndex;

        private static void PopulateHeritageData(Heritage heritage) {
            switch (heritage.Dat.Name) {
                case "Aluvian":
                    heritage.Group = HeritageGroup.Aluvian;
                    heritage.DisplayName = "Aluvian";
                    heritage.MeleeMastery = WeaponType.Dagger;
                    heritage.RangedMastery = WeaponType.Bow;
                    heritage.RacialBonusStringId = 0x0206bbd4;
                    heritage.RacialDescriptionStringId = 0x04bf27a4;
                    heritage.TooltipStringId = 0x0ea7c2fe;
                    break;
                case "Gharu'ndim":
                    heritage.Group = HeritageGroup.Gharundim;
                    heritage.DisplayName = "Gharu'ndim";
                    heritage.MeleeMastery = WeaponType.Staff;
                    heritage.RangedMastery = WeaponType.Magic;
                    heritage.RacialBonusStringId = 0x0630f8a4;
                    heritage.RacialDescriptionStringId = 0x03e311e4;
                    heritage.TooltipStringId = 0x0397cd8d;
                    break;
                case "Sho":
                    heritage.Group = HeritageGroup.Sho;
                    heritage.DisplayName = "Sho";
                    heritage.MeleeMastery = WeaponType.Unarmed;
                    heritage.RangedMastery = WeaponType.Bow;
                    heritage.RacialBonusStringId = 0x0335ba44;
                    heritage.RacialDescriptionStringId = 0x05633754;
                    heritage.TooltipStringId = 0x0b0df37f;
                    break;
                case "Viamontian":
                    heritage.Group = HeritageGroup.Viamontian;
                    heritage.DisplayName = "Viamontian";
                    heritage.MeleeMastery = WeaponType.Sword;
                    heritage.RangedMastery = WeaponType.Crossbow;
                    heritage.RacialBonusStringId = 0x00166f04;
                    heritage.RacialDescriptionStringId = 0x00413754;
                    heritage.TooltipStringId = 0x049ac9ae;
                    break;
                case "Penumbraen":
                    heritage.Group = HeritageGroup.Penumbraen;
                    heritage.DisplayName = "Penumbraen";
                    heritage.MeleeMastery = WeaponType.Unarmed;
                    heritage.RangedMastery = WeaponType.Crossbow;
                    heritage.RacialBonusStringId = 0x0335ba44;
                    heritage.RacialDescriptionStringId = 0x050016a4;
                    heritage.TooltipStringId = 0x0ce6445e;
                    break;
                case "Umbraen":
                    heritage.Group = HeritageGroup.Shadowbound;
                    heritage.DisplayName = "Umbrean";
                    heritage.MeleeMastery = WeaponType.Unarmed;
                    heritage.RangedMastery = WeaponType.Crossbow;
                    heritage.RacialBonusStringId = 0x0335ba44;
                    heritage.RacialDescriptionStringId = 0x050016a4;
                    heritage.TooltipStringId = 0x0b2c18f4;
                    break;
                case "Gear":
                    heritage.Group = HeritageGroup.Gearknight;
                    heritage.DisplayName = "Gear Knight";
                    heritage.MeleeMastery = WeaponType.Mace;
                    heritage.RangedMastery = WeaponType.Crossbow;
                    heritage.RacialBonusStringId = 0x0d30ff94;
                    heritage.RacialDescriptionStringId = 0x00fe11e4;
                    heritage.TooltipStringId = 0x09dc8fc4;
                    break;
                case "Tumerok":
                    heritage.Group = HeritageGroup.Tumerok;
                    heritage.DisplayName = "Aun Tumerok";
                    heritage.MeleeMastery = WeaponType.Spear;
                    heritage.RangedMastery = WeaponType.Thrown;
                    heritage.RacialBonusStringId = 0x0330ce04;
                    heritage.RacialDescriptionStringId = 0x014011b4;
                    heritage.TooltipStringId = 0x0b7becdb;
                    break;
                case "Undead":
                    heritage.Group = HeritageGroup.Undead;
                    heritage.DisplayName = "Undead";
                    heritage.MeleeMastery = WeaponType.Axe;
                    heritage.RangedMastery = WeaponType.Thrown;
                    heritage.RacialBonusStringId = 0x0d166794;
                    heritage.RacialDescriptionStringId = 0x08fe3754;
                    heritage.TooltipStringId = 0x0f2d1ba4;
                    break;
                case "Lugian":
                    heritage.Group = HeritageGroup.Lugian;
                    heritage.DisplayName = "Lugian";
                    heritage.MeleeMastery = WeaponType.Axe;
                    heritage.RangedMastery = WeaponType.Thrown;
                    heritage.RacialBonusStringId = 0x0e162c44;
                    heritage.RacialDescriptionStringId = 0x0f0b3714;
                    heritage.TooltipStringId = 0x09926fae;
                    break;
                case "Empyrean":
                    heritage.Group = HeritageGroup.Empyrean;
                    heritage.DisplayName = "Empyrean";
                    heritage.MeleeMastery = WeaponType.Sword;
                    heritage.RangedMastery = WeaponType.Magic;
                    heritage.RacialBonusStringId = 0x01163794;
                    heritage.RacialDescriptionStringId = 0x08f23724;
                    heritage.TooltipStringId = 0x0dd0535e;
                    break;
                case "Olthoi":
                    heritage.Group = HeritageGroup.Olthoi;
                    heritage.DisplayName = "Olthoi Soldier";
                    heritage.MeleeMastery = WeaponType.Undef;
                    heritage.RangedMastery = WeaponType.Undef;
                    heritage.RacialBonusStringId = 0x032337a4;
                    heritage.RacialDescriptionStringId = 0x01214f24;
                    heritage.TooltipStringId = 0x0a0a1f89;
                    break;
                case "OlthoiAcid":
                    heritage.Group = HeritageGroup.OlthoiAcid;
                    heritage.DisplayName = "Olthoi Spitter";
                    heritage.MeleeMastery = WeaponType.Undef;
                    heritage.RangedMastery = WeaponType.Undef;
                    heritage.RacialBonusStringId = 0x032337a4;
                    heritage.RacialDescriptionStringId = 0x01214f24;
                    heritage.TooltipStringId = 0x0f8773e4;
                    break;
                default:
                    heritage.DisplayName = "DAT: " + heritage.Dat.Name;
                    heritage.Group = HeritageGroup.Invalid;
                    heritage.MeleeMastery = WeaponType.Undef;
                    heritage.RangedMastery = WeaponType.Undef;
                    break;
            }

            if (heritage.Dat.PrimaryStartAreas.Count > 0) {
                heritage.SelectedStarterArea = heritage.Dat.PrimaryStartAreas.First();
            }

            heritage.RacialBonusString = GetStringFromId(UI_Pregame_Strings, heritage.RacialBonusStringId);
            heritage.RacialDescriptionString = GetStringFromId(UI_Pregame_Strings, heritage.RacialDescriptionStringId);
            heritage.Tooltip = GetStringFromId(UI_Pregame_Strings, heritage.TooltipStringId);
            heritage.TrainedStartingSkillsString = GetStringFromId(UI_Pregame_Strings, 0x07c77f43);
        }

        private static string GetStringFromId(StringTable stringsTable, uint stringId) {
            try {
                return stringsTable.StringTableData.Where(s => s.Id == stringId).First().Strings.First()
                    .Replace("\\n", "\n")
                    .Replace("%", "%%");
            }
            catch { }
            return "";
        }

        internal static AcClient.Hook CPlayerSystem__Handle_CharGenVerificationResponse_hook = new AcClient.Hook(0x0055F620, 0x0055D61F);
        //.text:0055F620 ; public: void __thiscall CPlayerSystem::Handle_CharGenVerificationResponse(void*, unsigned int)
        //.text:0055D61F                 call? Handle_CharGenVerificationResponse@CPlayerSystem @@QAEXPAXI@Z ; CPlayerSystem::Handle_CharGenVerificationResponse(void*, uint)
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)] internal unsafe delegate void CPlayerSystem__Handle_CharGenVerificationResponse_def(CPlayerSystem* This, void* buff, uint size);

        /// <summary>
        /// Detour function- the client thinks this is CPlayerSystem::Handle_CharGenVerificationResponse, so make sure you call the real thing
        /// </summary>
        internal unsafe static void CPlayerSystem__Handle_CharGenVerificationResponse(CPlayerSystem* This, void* buff, uint size) {
            if (!CPlayerSystem__Handle_CharGenVerificationResponse_hook.Remove())
                FilterCore.LogError($"HOOK>CPlayerSystem__Handle_CharGenVerificationResponse removal failure");
            if (buff != null) {
                int buf = (int)buff;
                CG_VERIFICATION_RESPONSE verificationResponse = *(CG_VERIFICATION_RESPONSE*)buf;
                switch (verificationResponse) {
                    case CG_VERIFICATION_RESPONSE.CG_VERIFICATION_RESPONSE_OK:
                        This->Handle_CharGenVerificationResponse(buff, size);
                        UInt32 gid = *(UInt32*)(buf + 4);
                        //ushort nameLen = *(ushort*)(buf + 8);
                        //string name = new((sbyte*)buf + 10);
                        //int secondsGreyedOut = *(int*)(buf + ((10 + 3 + nameLen) & ~3));
                        //FilterCore.LogError($"Created character \"{name}\". Entering game..."); // to make multiple characters at once, cache the above info, and drop the following LogOnCharacter. This will leave CharacterSet in a bad state though.
                        (*CPlayerSystem.s_pPlayerSystem)->LogOnCharacter(gid);
                        break;
                    case CG_VERIFICATION_RESPONSE.CG_VERIFICATION_RESPONSE_NAME_IN_USE:
                        FilterCore.LogError($"{verificationResponse}: ID_Character_Err_NameReserved");
                        //TODO: make dialog.
                        double todo1;
                        break;
                    case CG_VERIFICATION_RESPONSE.CG_VERIFICATION_RESPONSE_NAME_BANNED:
                        FilterCore.LogError($"{verificationResponse}: ID_Character_Err_NameBanned");
                        //TODO: make dialog.
                        double todo2;
                        break;
                    case CG_VERIFICATION_RESPONSE.CG_VERIFICATION_RESPONSE_ADMIN_PRIVILEGE_DENIED:
                        FilterCore.LogError($"{verificationResponse}: ID_Character_Err_NameAdminDenied");
                        //TODO: make dialog.
                        double todo3;
                        break;
                    default:
                        FilterCore.LogError($"{verificationResponse}: ID_Character_Err_NameDBDown");
                        //TODO: make dialog.
                        double todo4;
                        break;
                }
            }
        }


        public void Dispose() {
            if (hud != null) {
                hud.Render -= Hud_Render;
                hud.PreRender -= Hud_PreRender;
                hud.DestroyTextures -= Hud_DestroyTextures;
                hud.CreateTextures -= Hud_CreateTextures;
                hud.Dispose();
                hud = null;
            }
            DestroyTextures();
        }
    }
}
