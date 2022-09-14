using System;
using System.Drawing;
using UtilityBelt.Lib;
using UBService.Lib.Settings;
using AcClient;

namespace UtilityBelt.Tools {
    [Name("Player")]
    [Summary("Player")]
    [FullDescription(@"TODO: Write this. This is still under development.")]
    public class Player : ToolBase {

        private UBHud hud;
        private UBHud.Titlebar titlebar;
        private System.Collections.Generic.List<UBHud.Block> Blocks = new System.Collections.Generic.List<UBHud.Block>();
        private int HudX = 200;
        private int HudY = 200;
        public int StartIDX;

        public Player(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }
        public override void Init() {
            base.Init();
        }
        #region Commands
        #region /ub player augs
        [Summary("Open Player Data")]
        [Usage("/ub player (aug|int|int64|bool|float|did|iid|str|pos|skill)s?")]
        [CommandPattern("player", @"^(?<cmd>aug|int|int64|bool|float|did|iid|str|pos|skill)s?$", false)]
        public unsafe void DoPlayer(string _, System.Text.RegularExpressions.Match args) {
            StartIDX = 0;
            switch (args.Groups["cmd"].Value) {
                case "aug":
                    Panel_ChangeWidth(340);
                    Panel_LoadPage = Player_Augs_LoadPage;
                    Panel_Title = "Player Augmentations";
                    break;
                case "int":
                    Panel_ChangeWidth(340);
                    Panel_LoadPage = Player_Ints_LoadPage;
                    Panel_Title = "Player Ints";
                    break;
                case "int64":
                    Panel_ChangeWidth(340);
                    Panel_LoadPage = Player_Int64s_LoadPage;
                    Panel_Title = "Player Int64s";
                    break;
                case "bool":
                    Panel_ChangeWidth(340);
                    Panel_LoadPage = Player_Bools_LoadPage;
                    Panel_Title = "Player Bools";
                    break;
                case "float":
                    Panel_ChangeWidth(340);
                    Panel_LoadPage = Player_Floats_LoadPage;
                    Panel_Title = "Player Floats";
                    break;
                case "did":
                    Panel_ChangeWidth(340);
                    Panel_LoadPage = Player_DIDs_LoadPage;
                    Panel_Title = "Player DIDs";
                    break;
                case "iid":
                    Panel_ChangeWidth(340);
                    Panel_LoadPage = Player_IIDs_LoadPage;
                    Panel_Title = "Player IIDs";
                    break;
                case "str":
                    Panel_ChangeWidth(600);
                    Panel_LoadPage = Player_Strings_LoadPage;
                    Panel_Title = "Player Strings";
                    break;
                case "pos":
                    Panel_ChangeWidth(900);
                    Panel_LoadPage = Player_Positions_LoadPage;
                    Panel_Title = "Player Positions";
                    break;
                case "skill":
                    Panel_ChangeWidth(900);
                    Panel_LoadPage = Player_Skills_LoadPage;
                    Panel_Title = "Player Skills";
                    break;




            }
            Panel_Display();
        }
        #endregion
        #endregion
        public unsafe void Panel_Display() {
            Panel_Clear();
            Panel_Create();
            hud.Render();
        }
        public UBHud.Event Panel_LoadPage = null;
        public string Panel_Title = "";

        private int panelWidth = 340;
        internal void Panel_ChangeWidth(int _width) {
            panelWidth = _width;
            if (hud != null) {
                titlebar.BBox = new Rectangle(0, 0, panelWidth, 20);
                hud.Resize(panelWidth, 340);
            }
        }
        internal void Panel_Create() {
            if (hud == null) {
                hud = UB.Huds.CreateHud(HudX, HudY, panelWidth, 340);
                hud.Transparent = false;
                titlebar = new UBHud.Titlebar(hud, new Rectangle(0, 0, panelWidth, 20), Panel_Title, null, true);
                titlebar.OnNextClick += Panel_NextBtn_OnClick;
                titlebar.OnPrevClick += Panel_PrevBtn_OnClick;

                Panel_LoadPage();
                //CoreManager.Current.WindowMessage += Current_WindowMessage;
                hud.OnClose += Panel_Clear;
                hud.OnMove += Panel_OnMove;
            }
            else {
                Logger.WriteToChat("Fatal error- CreateHud while hud was not null");
            }
        }
        public unsafe void Player_Augs_LoadPage() {
            CBaseQualities* playerQualities = &(*CPhysicsObj.player_object)->weenie_obj->m_pQualities->a0.a1;
            var table = playerQualities->_intStatsTable;
            int iter = 0;
            for (int rowNum = StartIDX; rowNum < augTypes.Length; rowNum++) {
                if (iter == 16) break;
                uint _data = 0;
                var entry = table->lookup((uint)augTypes[rowNum].stype);
                if (entry != null) _data = entry->_data;
                string left = $"{(uint)augTypes[rowNum].stype}";
                string text = $"{augTypes[rowNum].name}";
                string right = $"{_data}/{augTypes[rowNum].repeat_count}";
                int barWidth = (int)(_data * (339 / (float)augTypes[rowNum].repeat_count)) % 340;
                Blocks.Add(new UBHud.Block(hud, new Rectangle(0, (20 * (iter + 1)), panelWidth, 20), left, text, right, augTypes[rowNum], barWidth));
                iter++;
            }
            titlebar.IndexLabel.Text = $"{StartIDX + 1}-{((StartIDX + 17) > augTypes.Length ? augTypes.Length : (StartIDX + 17))}";
        }
        public unsafe void Player_Ints_LoadPage() {
            CBaseQualities* playerQualities = &(*CPhysicsObj.player_object)->weenie_obj->m_pQualities->a0.a1;
            var table = playerQualities->_intStatsTable;
            AcClient.PackableHashData<uint, uint>*[] mytable = new AcClient.PackableHashData<uint, uint>*[16];
            if (StartIDX >= table->_currNum) StartIDX = 0;
            table->CopyTo(mytable, StartIDX);
            int iter = 0;
            foreach (var j in mytable) {
                if (j == null) continue;
                string left = $"{j->_key}";
                string text = $"{(STypeInt)j->_key}";
                string right = $"{j->_data}";
                Blocks.Add(new UBHud.Block(hud, new Rectangle(0, (20 * (iter + 1)), panelWidth, 20), left, text, right, left + ':' + right, 0));
                iter++;
            }
            titlebar.IndexLabel.Text = $"{StartIDX + 1}-{((StartIDX + 17) > table->_currNum ? table->_currNum : (StartIDX + 17))}";
        }
        public unsafe void Player_Int64s_LoadPage() {
            CBaseQualities* playerQualities = &(*CPhysicsObj.player_object)->weenie_obj->m_pQualities->a0.a1;
            var table = playerQualities->_int64StatsTable;
            AcClient.PackableHashData<uint, Int64>*[] mytable = new AcClient.PackableHashData<uint, Int64>*[16];
            if (StartIDX >= table->_currNum) StartIDX = 0;
            table->CopyTo(mytable, StartIDX);
            int iter = 0;
            foreach (var j in mytable) {
                if (j == null) continue;
                string left = $"{j->_key}";
                string text = $"{(STypeInt64)j->_key}";
                string right = $"{j->_data}";
                Blocks.Add(new UBHud.Block(hud, new Rectangle(0, (20 * (iter + 1)), panelWidth, 20), left, text, right, left + ':' + right, 0));
                iter++;
            }
            titlebar.IndexLabel.Text = $"{StartIDX + 1}-{((StartIDX + 17) > table->_currNum ? table->_currNum : (StartIDX + 17))}";
        }
        public unsafe void Player_Bools_LoadPage() {
            CBaseQualities* playerQualities = &(*CPhysicsObj.player_object)->weenie_obj->m_pQualities->a0.a1;
            var table = playerQualities->_boolStatsTable;
            AcClient.PackableHashData<uint, bool>*[] mytable = new AcClient.PackableHashData<uint, bool>*[16];
            if (StartIDX >= table->_currNum) StartIDX = 0;
            table->CopyTo(mytable, StartIDX);
            int iter = 0;
            foreach (var j in mytable) {
                if (j == null) continue;
                string left = $"{j->_key}";
                string text = $"{(STypeBool)j->_key}";
                string right = $"{j->_data}";
                Blocks.Add(new UBHud.Block(hud, new Rectangle(0, (20 * (iter + 1)), panelWidth, 20), left, text, right, left + ':' + right, 0));
                iter++;
            }
            titlebar.IndexLabel.Text = $"{StartIDX + 1}-{((StartIDX + 17) > table->_currNum ? table->_currNum : (StartIDX + 17))}";
        }
        public unsafe void Player_Floats_LoadPage() {
            CBaseQualities* playerQualities = &(*CPhysicsObj.player_object)->weenie_obj->m_pQualities->a0.a1;
            var table = playerQualities->_floatStatsTable;
            AcClient.PackableHashData<uint, double>*[] mytable = new AcClient.PackableHashData<uint, double>*[16];
            if (StartIDX >= table->_currNum) StartIDX = 0;
            table->CopyTo(mytable, StartIDX);
            int iter = 0;
            foreach (var j in mytable) {
                if (j == null) continue;
                string left = $"{j->_key}";
                string text = $"{(STypeFloat)j->_key}";
                string right = $"{j->_data}";
                Blocks.Add(new UBHud.Block(hud, new Rectangle(0, (20 * (iter + 1)), panelWidth, 20), left, text, right, left + ':' + right, 0));
                iter++;
            }
            titlebar.IndexLabel.Text = $"{StartIDX + 1}-{((StartIDX + 17) > table->_currNum ? table->_currNum : (StartIDX + 17))}";
        }
        public unsafe void Player_Strings_LoadPage() {
            CBaseQualities* playerQualities = &(*CPhysicsObj.player_object)->weenie_obj->m_pQualities->a0.a1;
            var table = playerQualities->_strStatsTable;
            AcClient.PackableHashData<uint, AC1Legacy.PStringBase<char>>*[] mytable = new AcClient.PackableHashData<uint, AC1Legacy.PStringBase<char>>*[16];
            if (StartIDX >= table->_currNum) StartIDX = 0;
            table->CopyTo(mytable, StartIDX);
            int iter = 0;
            foreach (var j in mytable) {
                if (j == null) continue;
                string left = $"{j->_key}";
                string text = $"{(STypeString)j->_key}";
                string right = $"{j->_data}";
                Blocks.Add(new UBHud.Block(hud, new Rectangle(0, (20 * (iter + 1)), panelWidth, 20), left, text, right, left + ':' + right, 0));
                iter++;
            }
            titlebar.IndexLabel.Text = $"{StartIDX + 1}-{((StartIDX + 17) > table->_currNum ? table->_currNum : (StartIDX + 17))}";
        }
        public unsafe void Player_DIDs_LoadPage() {
            CBaseQualities* playerQualities = &(*CPhysicsObj.player_object)->weenie_obj->m_pQualities->a0.a1;
            var table = playerQualities->_didStatsTable;
            AcClient.PackableHashData<uint, UInt32>*[] mytable = new AcClient.PackableHashData<uint, UInt32>*[16];
            if (StartIDX >= table->_currNum) StartIDX = 0;
            table->CopyTo(mytable, StartIDX);
            int iter = 0;
            foreach (var j in mytable) {
                if (j == null) continue;
                string left = $"{j->_key}";
                string text = $"{(STypeDID)j->_key}";
                string right = $"{j->_data:X8}";
                Blocks.Add(new UBHud.Block(hud, new Rectangle(0, (20 * (iter + 1)), panelWidth, 20), left, text, right, left + ':' + right, 0));
                iter++;
            }
            titlebar.IndexLabel.Text = $"{StartIDX + 1}-{((StartIDX + 17) > table->_currNum ? table->_currNum : (StartIDX + 17))}";
        }
        public unsafe void Player_IIDs_LoadPage() {
            CBaseQualities* playerQualities = &(*CPhysicsObj.player_object)->weenie_obj->m_pQualities->a0.a1;
            var table = playerQualities->_iidStatsTable;
            AcClient.PackableHashData<uint, UInt32>*[] mytable = new AcClient.PackableHashData<uint, UInt32>*[16];
            if (StartIDX >= table->_currNum) StartIDX = 0;
            table->CopyTo(mytable, StartIDX);
            int iter = 0;
            foreach (var j in mytable) {
                if (j == null) continue;
                string left = $"{j->_key}";
                string text = $"{(STypeIID)j->_key}";
                string right = $"{j->_data:X8}";
                Blocks.Add(new UBHud.Block(hud, new Rectangle(0, (20 * (iter + 1)), panelWidth, 20), left, text, right, left + ':' + right, 0));
                iter++;
            }
            titlebar.IndexLabel.Text = $"{StartIDX + 1}-{((StartIDX + 17) > table->_currNum ? table->_currNum : (StartIDX + 17))}";
        }
        public unsafe void Player_Positions_LoadPage() {
            CBaseQualities* playerQualities = &(*CPhysicsObj.player_object)->weenie_obj->m_pQualities->a0.a1;
            var table = playerQualities->_posStatsTable;
            AcClient.PackableHashData<uint, Position>*[] mytable = new AcClient.PackableHashData<uint, Position>*[16];
            if (StartIDX >= table->_currNum) StartIDX = 0;
            table->CopyTo(mytable, StartIDX);
            int iter = 0;
            foreach (var j in mytable) {
                if (j == null) continue;
                string left = $"{j->_key}";
                string text = $"{(STypePosition)j->_key}";
                string right = $"{j->_data}";
                Blocks.Add(new UBHud.Block(hud, new Rectangle(0, (20 * (iter + 1)), panelWidth, 20), left, text, right, left + ':' + right, 0));
                iter++;
            }
            titlebar.IndexLabel.Text = $"{StartIDX + 1}-{((StartIDX + 17) > table->_currNum ? table->_currNum : (StartIDX + 17))}";
        }
        public unsafe void Player_Skills_LoadPage() {
            PackableHashTable<uint, Skill>* table = (*CPhysicsObj.player_object)->weenie_obj->m_pQualities->a0._skillStatsTable;
            AcClient.PackableHashData<uint, Skill>*[] mytable = new AcClient.PackableHashData<uint, Skill>*[16];
            if (StartIDX >= table->_currNum) StartIDX = 0;
            table->CopyTo(mytable, StartIDX);
            int iter = 0;
            foreach (var j in mytable) {
                if (j == null) continue;
                AC1Legacy.PStringBase<char>* skillName = AC1Legacy.PStringBase<char>.null_string;
                SkillSystem.InqSkillName(j->_key, skillName);
                string left = $"{j->_key}";
                string text = $"({j->_key}){*skillName}";
                string right = $"{j->_data}";

                Blocks.Add(new UBHud.Block(hud, new Rectangle(0, (20 * (iter + 1)), panelWidth, 20), left, text, right, left + ':' + right, 0));
                iter++;
            }
            titlebar.IndexLabel.Text = $"{StartIDX + 1}-{((StartIDX + 17) > table->_currNum ? table->_currNum : (StartIDX + 17))}";
        }

        public void Panel_Clear() {
            if (hud != null) {
                titlebar = null;
                for (int i = Blocks.Count - 1; i >= 0; i--) {
                    if (Blocks[i] != null) hud.UnRegisterElement(Blocks[i]);
                    Blocks[i] = null;
                }
                Blocks = new System.Collections.Generic.List<UBHud.Block>();
                hud.Dispose();
                hud = null;
            }
        }
        private void Panel_OnMove() {
            HudX = hud.BBox.X;
            HudY = hud.BBox.Y;
        }
        private void Panel_NextBtn_OnClick() {
            StartIDX += 16;
            if (StartIDX > augTypes.Length) StartIDX -= 16;
            for (int i = Blocks.Count - 1; i >= 0; i--) {
                if (Blocks[i] != null) hud.UnRegisterElement(Blocks[i]);
                Blocks[i] = null;
            }
            Blocks = new System.Collections.Generic.List<UBHud.Block>();
            Panel_LoadPage();
            hud.Render();
        }
        private void Panel_PrevBtn_OnClick() {
            StartIDX -= 16;
            if (StartIDX < 0) StartIDX = 0;
            for (int i = Blocks.Count - 1; i >= 0; i--) {
                if (Blocks[i] != null) hud.UnRegisterElement(Blocks[i]);
                Blocks[i] = null;
            }
            Blocks = new System.Collections.Generic.List<UBHud.Block>();
            Panel_LoadPage();
            hud.Render();
        }


        //TODO:
        /*
[11:30 PM] Hells: for credits i think its just a quest flag + adds one to TotalSkillCredits int 23 / AvailableSkillCredits int 24
quest flag for lum skill credit LumAugSkillQuest
https://github.com/ACEmulator/ACE-World-16PY-Patches/blob/master/Database/Patches/9%20WeenieDefaults/Creature/Human/43398%20Nalicana.sql#L365 
[11:36 PM] Hells: quest flag for benediction LesserBenedictionAug 
https://github.com/ACEmulator/ACE-World-16PY-Patches/blob/master/Database/Patches/9%20WeenieDefaults/Creature/Human/34259%20Donatello%20Linante.sql 
[11:36 PM] Hells: blackmoors favor is a bool
[11:36 PM] Hells: https://github.com/ACEmulator/ACE/blob/master/Source/ACE.Server/WorldObjects/Player_Networking.cs#L485
        */
        #region Static Augmentation Data
        public struct AugType {
            public STypeInt stype;
            public string name;
            public int repeat_count;
            public string poc;
            public AugType(STypeInt _stype, string _name, int _repeat_count, string _poc) {
                stype = _stype;
                name = _name;
                repeat_count = _repeat_count;
                poc = _poc;
            }
            public override string ToString() {
                return $"{stype}({(int)stype}) {name} {(repeat_count > 1 ? $"(*{repeat_count})" : "")} poc:{poc}";
            }
        }

        public AugType[] augTypes = new AugType[] {
            new AugType(STypeInt.LUM_AUG_DAMAGE_RATING,"Aura of Valor",5,"Nalicana,Asheron's Castle"),
            new AugType(STypeInt.LUM_AUG_DAMAGE_REDUCTION_RATING,"Aura of Protection",5,"Nalicana,Asheron's Castle"),
            new AugType(STypeInt.LUM_AUG_CRIT_DAMAGE_RATING,"Aura of Glory",5,"Nalicana,Asheron's Castle"),
            new AugType(STypeInt.LUM_AUG_CRIT_REDUCTION_RATING,"Aura of Temperance",5,"Nalicana,Asheron's Castle"),
//            new AugType(STypeInt.LUM_AUG_SURGE_EFFECT_RATING,"Aura of Surge Effect",5,"Nalicana,Asheron's Castle"),
            new AugType(STypeInt.LUM_AUG_SURGE_CHANCE_RATING,"Aura of Aetheric Vision",5,"Nalicana,Asheron's Castle"),
            new AugType(STypeInt.LUM_AUG_ITEM_MANA_USAGE,"Aura of Mana Flow",5,"Nalicana,Asheron's Castle"),
            new AugType(STypeInt.LUM_AUG_ITEM_MANA_GAIN,"Aura of Mana Infusion",5,"Nalicana,Asheron's Castle"),
            new AugType(STypeInt.LUM_AUG_HEALING_RATING,"Aura of Purity",5,"Nalicana,Asheron's Castle"),
            new AugType(STypeInt.LUM_AUG_SKILLED_CRAFT,"Aura of Craftsman",5,"Nalicana,Asheron's Castle"),
            new AugType(STypeInt.LUM_AUG_SKILLED_SPEC,"Aura of Specialization",5,"Nalicana,Asheron's Castle"),
            new AugType(STypeInt.LUM_AUG_ALL_SKILLS,"Aura of World",10,"Nalicana,Asheron's Castle"),
            new AugType(STypeInt.AUGMENTATION_INCREASED_CARRYING_CAPACITY,"Might of the Seventh Mule",5,"Husoon,Zaikhal"),
            new AugType(STypeInt.AUGMENTATION_EXTRA_PACK_SLOT,"Shadow of the Seventh Mule",1,"Dumida bint Ruminre,Zaikhal"),
            new AugType(STypeInt.AUGMENTATION_INFUSED_WAR_MAGIC,"Infused War Magic",1,"Raphel Detante,Silyun"),
            new AugType(STypeInt.AUGMENTATION_INFUSED_LIFE_MAGIC,"Infused Life Magic",1,"Akemi Fei,Hebian-To"),
            new AugType(STypeInt.AUGMENTATION_INFUSED_ITEM_MAGIC,"Infused Item Magic",1,"Gan Fo,Hebian-To"),
            new AugType(STypeInt.AUGMENTATION_INFUSED_CREATURE_MAGIC,"Infused Creature Magic",1,"Gustuv Lansdown,Cragstone"),
            new AugType(STypeInt.AUGMENTATION_INFUSED_VOID_MAGIC,"Infused Void Magic",1,"Morathe,Candeth Keep"),
            new AugType(STypeInt.AUGMENTATION_LESS_DEATH_ITEM_LOSS,"Clutch of the Miser",3,"Rohula bint Ludun,Ayan Baqur"),
            new AugType(STypeInt.AUGMENTATION_SPELLS_REMAIN_PAST_DEATH,"Enduring Enchantment",1,"Erik Festus,Ayan Baqur"),
            new AugType(STypeInt.AUGMENTATION_BONUS_XP,"Quick Learner",1,"Rickard Dumalia,Silyun"),
            new AugType(STypeInt.AUGMENTATION_FASTER_REGEN,"Innate Renewal",2,"Alison Dulane,Bandit Castle"),
//            new AugType(STypeInt.AUGMENTATION_STAT,"Stat",10,",Fiun Outpost"),
//            new AugType(STypeInt.AUGMENTATION_FAMILY_STAT,"Family Stat",10,",Fiun Outpost"),
            new AugType(STypeInt.AUGMENTATION_INNATE_FAMILY,"Innate Family",10,",Fiun Outpost"),
            new AugType(STypeInt.AUGMENTATION_INNATE_STRENGTH,"Reinforcement of the Lugians",10,"Fiun Luunere,Fiun Outpost"),
            new AugType(STypeInt.AUGMENTATION_INNATE_ENDURANCE,"Bleeargh's Fortitude",10,"Fiun Ruun,Fiun Outpost"),
            new AugType(STypeInt.AUGMENTATION_INNATE_COORDINATION,"Oswald's Enhancement",10,"Fiun Bayaas,Fiun Outpost"),
            new AugType(STypeInt.AUGMENTATION_INNATE_QUICKNESS,"Siraluun's Blessing",10,"Fiun Riish,Fiun Outpost"),
            new AugType(STypeInt.AUGMENTATION_INNATE_FOCUS,"Enduring Calm",10,"Fiun Vasherr,Fiun Outpost"),
            new AugType(STypeInt.AUGMENTATION_INNATE_SELF,"Steadfast Will",10,"Fiun Noress,Fiun Outpost"),
            new AugType(STypeInt.AUGMENTATION_RESISTANCE_FAMILY,"Resistance Family",2,"N/A,N/A"),
            new AugType(STypeInt.AUGMENTATION_RESISTANCE_BLUNT,"Enhancement of the Mace Turner",2,"Nawamara Dia,Hebian-To"),
            new AugType(STypeInt.AUGMENTATION_RESISTANCE_SLASH,"Enhancement of the Blade Turner",2,"Ilin Wis,Hebian-To"),
            new AugType(STypeInt.AUGMENTATION_RESISTANCE_PIERCE,"Enhancement of the Arrow Turner",2,"Kyujo Rujen,Hebian-To"),
            new AugType(STypeInt.AUGMENTATION_RESISTANCE_LIGHTNING,"Storm's Enhancement",2,"Enli Yuo,Hebian-To"),
            new AugType(STypeInt.AUGMENTATION_RESISTANCE_FIRE,"Fiery Enhancement",2,"Rikshen Ri,Hebian-To"),
            new AugType(STypeInt.AUGMENTATION_RESISTANCE_FROST,"Icy Enhancement",2,"Lu Bao,Hebian-To"),
            new AugType(STypeInt.AUGMENTATION_RESISTANCE_ACID,"Caustic Enhancement",2,"Shujio Milao,Hebian-To"),
            new AugType(STypeInt.AUGMENTATION_CRITICAL_DEFENSE,"Critical Protection",1,"Piersanti Linante,Sanamar"),
            new AugType(STypeInt.AUGMENTATION_DAMAGE_BONUS,"Frenzy of the Slayer",1,"Neela Nashua,Bandit Castle"),
            new AugType(STypeInt.AUGMENTATION_DAMAGE_REDUCTION,"Iron Skin of the Invincible",1,"Emily Yarow,Cragstone"),
            new AugType(STypeInt.AUGMENTATION_CRITICAL_EXPERTISE,"Eye of the Remorseless",1,"Anfram Mellow,Ayan Baqur"),
            new AugType(STypeInt.AUGMENTATION_CRITICAL_POWER,"Hand of the Remorseless",1,"Alishia bint Aldan,Ayan Baqur"),
            new AugType(STypeInt.AUGMENTATION_BONUS_SALVAGE,"Ciandra's Fortune",4,"Kris Cennis,Cragstone"),
            new AugType(STypeInt.AUGMENTATION_BONUS_IMBUE_CHANCE,"Charmed Smith",1,"Lug,Oolutanga's Refuge"),
            new AugType(STypeInt.AUGMENTATION_SPECIALIZE_ARMOR_TINKERING,"Jibril's Essence",1,"Joshun Felden,Cragstone"),
            new AugType(STypeInt.AUGMENTATION_SPECIALIZE_ITEM_TINKERING,"Yoshi's Essence",1,"Brienne Carlus,Cragstone"),
            new AugType(STypeInt.AUGMENTATION_SPECIALIZE_MAGIC_ITEM_TINKERING,"Celdiseth's Essence",1,"Burrell Sammrun,Cragstone"),
            new AugType(STypeInt.AUGMENTATION_SPECIALIZE_WEAPON_TINKERING,"Koga's Essence",1,"Lenor Turk,Cragstone"),
            new AugType(STypeInt.AUGMENTATION_SPECIALIZE_SALVAGING,"Ciandra's Essence",1,"Robert Crow,Cragstone"),
//            new AugType(STypeInt.AUGMENTATION_SPECIALIZE_GEARCRAFT,"Specialize Gearcraft",1,"N/A,N/A"),
            new AugType(STypeInt.AUGMENTATION_SKILLED_MELEE,"Master of the Steel Circle",1,"Carlito Gallo,Silyun"),
            new AugType(STypeInt.AUGMENTATION_SKILLED_MAGIC,"Master of the Five Fold Path",1,"Rahina bint Zalanis,Zaikhal"),
            new AugType(STypeInt.AUGMENTATION_SKILLED_MISSILE,"Master of the Focused Eye",1,"Kilaf,Zaikhal"),
            new AugType(STypeInt.AUGMENTATION_JACK_OF_ALL_TRADES,"Jack of All Trades",1,"Arianna the Adept,Bandit Castle"),
            new AugType(STypeInt.AUGMENTATION_INCREASED_SPELL_DURATION,"Archmage's Endurance",5,"Nawamara Ujio,Mayoi"),
        };
        #endregion
        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    Panel_Clear();
                }
                disposedValue = true;
            }
        }
    }


}

