using Decal.Adapter;
using Decal.Adapter.Wrappers;
using System;
using System.Text.RegularExpressions;
using UtilityBelt.Lib;
using UBService.Lib.Settings;
using UtilityBelt.Lib.Expressions;
using AcClient;
using Microsoft.DirectX.Direct3D;
using System.Linq;
using Hellosam.Net.Collections;
using System.Text;

namespace UtilityBelt.Tools {
    [Name("SpellManager")]
    [Summary("Manages the contents of spelltabs.")]
    [FullDescription(@"
SpellManager exports to chat or saves to a named profile the contents of specified spelltabs.

Spells may be loaded/imported to specified spelltabs or completely replace all of your spells.  

When loading you can use the best version of a spell available, determined by your components and your casting skill.
")]

    public class SpellManager : ToolBase {
        private bool SpellCast_Hooked = false;
        private const int MAX_SPELL_TAB = 8;
        #region Expressions
        #region spellnamefromid[number spellid]
        [ExpressionMethod("spellname")]
        [ExpressionParameter(0, typeof(double), "spellid", "Spell ID")]
        [ExpressionReturn(typeof(string), "Returns a string name of the passed spell id.")]
        [Summary("Gets the name of a spell by id")]
        [Example("spellname[1]", "Returns `Strength Other I` for spell id 1")]
        public object spellname(double id) {
            return Lib.Spells.GetName((uint)id);
        }
        #endregion //spellnamefromid[number spellid]
        #region componentname[number componentid]
        [ExpressionMethod("componentname")]
        [ExpressionParameter(0, typeof(double), "componentid", "Spell ID")]
        [ExpressionReturn(typeof(string), "Returns a string name of the passed spell id.")]
        [Summary("Gets the name of a spell component by id")]
        [Example("componentname[1]", "Returns total count of prismatic tapers in your inventory")]
        public object componentnamefromid(double id) {
            return Spells.GetComponentName((int)id);
        }
        #endregion //componentnamefromid[number componentid]
        #region componentdata[number componentid]
        [ExpressionMethod("componentdata")]
        [ExpressionParameter(0, typeof(double), "componentid", "component ID")]
        [ExpressionReturn(typeof(ExpressionDictionary), "Returns a dictionary containing component data")]
        [Summary("Returns a dictionary containing component data for the passed component id")]
        [Example("componentdata[1]", "returns a dictionary of information about component id 1")]
        public object componentdata(double id) {
            var component = Spells.GetComponent((int)id);
            var componentData = new ExpressionDictionary();
            componentData.Items.Add("BurnRate", (double)component.BurnRate);
            componentData.Items.Add("GestureId", (double)component.GestureId);
            componentData.Items.Add("GestureSpeed", (double)component.GestureSpeed);
            componentData.Items.Add("IconId", (double)component.IconId);
            componentData.Items.Add("Id", (double)component.Id);
            componentData.Items.Add("Name", component.Name);
            componentData.Items.Add("BurnRate", (double)component.SortKey);
            componentData.Items.Add("Type", component.Type.Name);
            componentData.Items.Add("Word", component.Word);

            return componentData;
        }
        #endregion //componentdata[number componentid]
        #region getknownspells[]
        [ExpressionMethod("getknownspells")]
        [ExpressionReturn(typeof(ExpressionList), "Returns a list of spell ids known by this character")]
        [Summary("Returns a list of spell ids known by this character")]
        [Example("getknownspells[]", "Returns a list of spell ids known by this character")]
        public object getknownspells() {
            var spells = new ExpressionList();
            foreach (var x in UtilityBeltPlugin.Instance.Core.CharacterFilter.SpellBook) {
                spells.Items.Add(x);
            }

            return spells;
        }
        #endregion //getknownspells[]
        #region spelldata[number spellid]
        [ExpressionMethod("spelldata")]
        [ExpressionParameter(0, typeof(double), "spellid", "Spell ID")]
        [ExpressionReturn(typeof(ExpressionDictionary), "Returns a dictionary of spell data")]
        [Summary("Gets a dictionary of information about the passed spell id")]
        [Example("spelldata[1]", "returns spell data for spellid 1")]
        public object spelldata(double id) {
            var spell = Lib.Spells.GetSpell((int)id);
            var spellData = new ExpressionDictionary();
            if (spell == null)
                return spellData;

            var componentIds = new ExpressionList();
            for (var i = 0; i < spell.ComponentIDs.Length; i++) {
                componentIds.Items.Add((double)spell.ComponentIDs[i]);
            }
            spellData.Items.Add("CasterEffect", (double)spell.CasterEffect);
            spellData.Items.Add("ComponentIds", componentIds);
            spellData.Items.Add("Description", spell.Description);
            spellData.Items.Add("Difficulty", (double)spell.Difficulty);
            spellData.Items.Add("Duration", (double)spell.Duration);
            spellData.Items.Add("Family", (double)spell.Family);
            spellData.Items.Add("Flags", (double)spell.Flags);
            spellData.Items.Add("Generation", (double)spell.Generation);
            spellData.Items.Add("IconId", (double)spell.IconId);
            spellData.Items.Add("Id", (double)spell.Id);
            spellData.Items.Add("IsDebuff", (double)(spell.IsDebuff ? 1 : 0));
            spellData.Items.Add("IsFastWindup", (double)(spell.IsFastWindup ? 1 : 0));
            spellData.Items.Add("IsFellowship", (double)(spell.IsFellowship ? 1 : 0));
            spellData.Items.Add("IsIrresistible", (double)(spell.IsIrresistible ? 1 : 0));
            spellData.Items.Add("IsOffensive", (double)(spell.IsOffensive ? 1 : 0));
            spellData.Items.Add("IsUntargetted", (double)(spell.IsUntargetted ? 1 : 0));
            spellData.Items.Add("Mana", (double)spell.Mana);
            spellData.Items.Add("Name", spell.Name);
            spellData.Items.Add("School", spell.School.Name);
            spellData.Items.Add("SortKey", (double)spell.SortKey);
            spellData.Items.Add("Speed", (double)spell.Speed);
            spellData.Items.Add("TargetEffect", (double)spell.TargetEffect);
            spellData.Items.Add("TargetMask", (double)spell.TargetMask);
            spellData.Items.Add("Type", (double)spell.Type);

            return spellData;
        }
        #endregion //spelldata[number spellid]
        #endregion Expressions

        #region Config
        [Summary("Loads the best comparable version of a spell if enabled or only by ID if disabled.")]
        public Setting<bool> LoadBest = new Setting<bool>(true);

        [Summary("Ignore missing spell components.")]
        public Setting<bool> IgnoreComps = new Setting<bool>(true);

        [Summary("Ignore skill requirements.")]
        public Setting<bool> IgnoreSkill = new Setting<bool>(false);

        [Summary("Adds an amount to the required casting skill of spells.")]
        public Setting<int> DifficultyModifier = new Setting<int>(0);

        [Summary("Skip missing tab information, otherwise considered blank.")]
        public Setting<bool> IgnoreMissingTabs = new Setting<bool>(true);

        [Summary("Auto-add spells on cast.")]
        public Setting<bool> AddSpellsOnCast = new Setting<bool>(false);

        [Summary("Don't generate messages when automatically adding spells.")]
        public Setting<bool> Quiet = new Setting<bool>(false);

        [Summary("Saved spelltabs.")]
        public Setting<ObservableDictionary<string, string>> Tabs = new Setting<ObservableDictionary<string, string>>(
            new ObservableDictionary<string, string>() {
                {"Default", "1:1635,1636,2645,48,2647,2644,47,2646,157,2648;" },
            });
        #endregion Config

        #region Commands
        #region /ub spell
        [Summary("Loads the spell from the profile")]
        [Usage("/ub spell ^\\s*(?<verb>load|save|clear|test)(?<tabs>[1-8]{0,8})?( (?<name>\\w+))?\\s*$")]
        [Example("/ub spell export5", "Exports spelltab 5 to the chat window")]
        [Example("/ub spell import5 1635,1636,2644,2645,48,2647,47,2646,158,2649", "Imports some recalls into tab 5")]
        [Example("/ub spell save5 Recalls", "Saves spells from Tab 5, into the 'Recalls' profile")]
        [Example("/ub spell load147", "Loads spelltabs 1,4,7 from the 'Default' profile")]
        [Example("/ub spell clear", "Clears ALL 8 tabs")]
        [Example("/ub spell swap123 456", "Exchanges tabs 1,2,3 with 4,5,6")]
        [Example("/ub spell move123 456", "Moves tabs 1,2,3 to 4,5,6")]
        [Example("/ub spell list 1030", "List all the spells that are comparable to 1030 - Cold Protection Self I")]
        [CommandPattern("spell", @"^(?<verb>load|save|export|import|clear|list|swap|move)(?<tabs>[1-8]{0,8})?(?: (?<name>.+))?$", false)]
        public void DoSpell(string _, Match args) {
            string verb = args.Groups["verb"].Value;

            int[] tabs;
            if (String.IsNullOrEmpty(args.Groups["tabs"].Value))
                tabs = Enumerable.Range(1, MAX_SPELL_TAB).ToArray();
            else
                tabs = args.Groups["tabs"].Value.ToCharArray().Select(x => int.Parse(x.ToString())).Distinct().ToArray();

            var name = String.IsNullOrEmpty(args.Groups["name"].Value) ? "Default" : args.Groups["name"].Value;

            switch (verb.ToLower()) {
                case "load":
                    LoadTabs(name, tabs);
                    break;
                case "save":
                    SaveTabs(name, tabs);
                    break;
                case "export":
                    foreach (int b in tabs) Logger.WriteToChat($"SpellTab {b}: {Export(b)}");
                    break;
                case "import":
                    foreach (int b in tabs) Import(b, name.Split(','));
                    break;
                case "clear":
                    ClearTabs(tabs);
                    break;
                case "list":
                    if (!uint.TryParse(name, out uint id)) {
                        Logger.WriteToChat("/ub spell list <spellID>");
                        return;
                    }
                    ListComparable(id);
                    break;
                case "swap":
                    if (!Regex.IsMatch(name, @"\s*[1-8]+\s*")) {
                        Logger.WriteToChat("Specify tabs to swap to: /ub spell swap12 34");
                        return;
                    }

                    var swaps = name.ToCharArray().Select(x => int.Parse(x.ToString())).Distinct().ToArray();
                    var numSwapped = Math.Min(tabs.Length, swaps.Length);

                    for (var i = 0; i < numSwapped; i++)
                        SwapTab(tabs[i], swaps[i]);
                    break;
                case "move":
                    if (!Regex.IsMatch(name, @"\s*[1-8]+\s*")) {
                        Logger.WriteToChat("Specify tabs to move to: /ub spell move12 34");
                        return;
                    }

                    var moves = name.ToCharArray().Select(x => int.Parse(x.ToString())).Distinct().ToArray();
                    var numMoved = Math.Min(tabs.Length, moves.Length);

                    for (var i = 0; i < numMoved; i++)
                        MoveTab(tabs[i], moves[i]);
                    break;
            }
        }
        #endregion
        #endregion Commands

        public SpellManager(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        public override void Init() {
            base.Init();
            Changed += SpellManager_Changed;
            if (UBHelper.Core.GameState != UBHelper.GameState.In_Game) {
                UB.Core.CharacterFilter.LoginComplete += CharacterFilter_LoginComplete;
            }
            else {
                if (AddSpellsOnCast) Enable();
            }
        }

        /// <summary>
        /// Swaps the contents of the source and destination spelltabs.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="dest"></param>
        /// <param name="loud"></param>
        private void SwapTab(int source, int dest, bool loud = true) {
            if (loud)
                Logger.WriteToChat($"Swapping tab {source} and {dest}");
            var swap = ExportTab(dest);
            MoveTab(source, dest);
            ClearTabs(source);
            Import(source, swap);
        }

        /// <summary>
        /// Moves the contents of the source spelltab to the destination and clears the source tab.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="dest"></param>
        private void MoveTab(int source, int dest, bool loud = true) {
            if (loud)
                Logger.WriteToChat($"Moving tab {source} to {dest}");
            ClearTabs(dest);
            Import(dest, ExportTab(source));
            ClearTabs(source);    //Todo: without this it copies.  Not sure if that's preferable
        }

        private void LoadTabs(string name = "Default", params int[] tabs) {
            if (!Tabs.Value.TryGetValue(name, out var saveString)) {
                Logger.WriteToChat($"No saved tab named {name}");
                return;
            }

            foreach (var tab in tabs) {
                var match = Regex.Match(saveString, $@"{tab}:(?<spells>(\d,?)+);");
                if (match.Success) {
                    var spells = match.Groups["spells"].Value.Split(',');
                    Logger.WriteToChat($"Loading {spells.Count()} spells from tab {tab} of {name}:\n  {match.Groups["spells"].Value}");
                    ClearTabs(tab);
                    Import(tab, spells, true);
                }
                else if (!IgnoreMissingTabs)
                    ClearTabs(tab);
            }
        }

        private void SaveTabs(string name = "Default", params int[] tabs) {
            if (!Tabs.Value.TryGetValue(name, out string saveString))
                Tabs.Value.Add(new System.Collections.Generic.KeyValuePair<string, string>(name, ""));
            if (saveString is null)
                saveString = "";

            var sb = new StringBuilder();

            //todo- logic here is .... off.
            for (var i = 1; i <= MAX_SPELL_TAB; i++) {
                //Save spells for specified tabs
                if (tabs.Contains(i))
                    sb.Append(Export(i));

                //Keeping any already saved spells
                else {
                    //Todo: rework this
                    // I habs idea pack/unpack class!
                    var match = Regex.Match(saveString, @$"(?<tab>{i}:(\d+,?)+;)");
                    sb.Append(match.Groups["tab"].Value);
                }
            }

            Logger.WriteToChat($"Saving tab(s) {String.Join(",", tabs.Select(x => x.ToString()).ToArray())} to {name}:\n  {sb} ");
            Tabs.Value[name] = sb.ToString();
        }

        private void ClearTabs(params int[] tabs) {
            Logger.WriteToChat($"Clearing tab(s) {String.Join(",", tabs.Select(x => x.ToString()).ToArray())}");
            foreach (var tab in tabs)
                Wipe(tab);
        }

        private void ListComparable(uint id) {
            var spells = Spells.GetComparableSpells(id);

            Logger.WriteToChat($"Similar to {id} ({spells.Count}):");
            foreach (var s in spells) {
                var spell = Spells.GetSpell(s);
                Logger.WriteToChat($"  {s,-6} - " +
                    $"{(Spells.HasSkill(s, DifficultyModifier) ? "S" : "s")}" +
                    $"{(Spells.HasComponents(s) ? "C" : "c")}" +
                    $"{(Spells.HasSkillBuff(s) ? "B" : "b")}" +
                    $"{(Spells.HasSkillHunt(s) ? "H" : "h")}" +
                    $"{(Spells.IsKnown(s) ? "K" : "k")}" +
                $" - {Spells.GetName(s)}");
            }

            if (Spells.TryGetBestCastable(id, out var best, IgnoreSkill ? 0 : DifficultyModifier, IgnoreComps))
                Logger.WriteToChat($"Best castable version is: {best} - {Spells.GetName(best)}");
            else
                Logger.WriteToChat("No castable version.");
        }

        #region Events
        private void SpellManager_Changed(object sender, SettingChangedEventArgs e) {
            switch (e.PropertyName) {
                case "AddSpellsOnCast":
                    if (AddSpellsOnCast) Enable();
                    else Disable();
                    break;
            }
        }

        private void CharacterFilter_LoginComplete(object sender, EventArgs e) {
            UB.Core.CharacterFilter.LoginComplete -= CharacterFilter_LoginComplete;
            if (AddSpellsOnCast) Enable();
        }
        protected override void Dispose(bool disposing) {
            try {
                if (!disposedValue) {
                    if (disposing) {
                        Disable();
                        UB.Core.CharacterFilter.LoginComplete -= CharacterFilter_LoginComplete;
                        base.Dispose(disposing);
                    }
                    disposedValue = true;
                }
            }
            catch (Exception ex) {
                Logger.LogException(ex);
            }
        }
        private void Enable() {
            if (!SpellCast_Hooked) {
                Decal.Adapter.CoreManager.Current.CharacterFilter.SpellCast += CharacterFilter_SpellCast;
                SpellCast_Hooked = true;
            }
        }
        private void Disable() {
            if (SpellCast_Hooked) {
                SpellCast_Hooked = false;
                Decal.Adapter.CoreManager.Current.CharacterFilter.SpellCast -= CharacterFilter_SpellCast;
            }
        }
        #endregion

        #region Unsafe -- Might move some to Spells
        /// <summary>
        /// export tab into a comma-separated string of spell ids
        /// </summary>
        /// <param name="tab"></param>
        /// <returns></returns>
        public unsafe string Export(int tab) => $"{tab}:{(*CPlayerSystem.s_pPlayerSystem)->playerModule.PlayerModule.GetFavoriteSpellsList(tab - 1)->GetList()};";

        /// <summary>
        /// export tab into a string array of ids
        /// </summary>
        /// <param name="tab"></param>
        /// <returns></returns>
        public unsafe string[] ExportTab(int tab) => (*CPlayerSystem.s_pPlayerSystem)->playerModule.PlayerModule.GetFavoriteSpellsList(tab - 1)->GetList().Split(',');

        /// <summary>
        /// Import a comma-separated string of spell ids into the given tab
        /// </summary>
        /// <param name="tab">tab id, 0-7</param>
        /// <param name="data"></param>
        /// <param name="loud"></param>
        /// <returns></returns>
        public unsafe int Import(int tab, string[] ids, bool loud = false) {
            tab--;  //Adjust for zero-index
            if (ids.Length == 0) return 0;
            var ret = 0;
            foreach (string d in ids) {
                if (!uint.TryParse(d, out uint spellID)) {
                    if (loud)
                        Logger.WriteToChat($"Failed to parse: {d}");
                    continue;
                }
                if (LoadBest && Spells.TryGetBestCastable(spellID, out var bestID, IgnoreSkill ? 0 : DifficultyModifier, IgnoreComps)) {
                    if (loud)
                        Logger.WriteToChat($"{(bestID != spellID ? $"Replacing {Spells.GetName(spellID)} with" : "Loading")} {Spells.GetName(bestID)}");
                    spellID = bestID;
                }
                // add favorite in client
                AddFavorite(spellID, tab, -1, false, true, !loud);
                ret++;
            }

            UpdateUI();
            return ret;
        }

        /// <summary>
        /// remove all spells from given tab
        /// </summary>
        /// <param name="tab"></param>
        public unsafe void Wipe(int tab) {
            tab--;  //Adjust for zero-index
            // I see you looking at this, but.... sigh
            var favs = (*CPlayerSystem.s_pPlayerSystem)->playerModule.PlayerModule.GetFavoriteSpellsList(tab);
            var myUI = (gmSpellcastingUI*)GlobalEventHandler.geh->ResolveHandler(5100110);
            SpellCastSubMenu* menu = (SpellCastSubMenu*)((&(myUI->m_subMenus)) + ((tab) * (sizeof(SpellCastSubMenu) / 4)));
            while (menu->m_numSpells > 0) menu->RemoveSpellFromMenu(favs->tail->data);
            UpdateUI();
        }
        public unsafe void UpdateUI(int newTab = -1, uint newSpellID = 0) {
            var myUI = (gmSpellcastingUI*)GlobalEventHandler.geh->ResolveHandler(5100110);
            if (newTab >= 0) {
                myUI->m_spellcastPanel->m_OpenPageToken = 0;
                myUI->m_spellcastPanel->m_OpenTabToken = 0;
                myUI->m_spellcastPanel->OpenTab(SpellTabElements[newTab]);
                if (newSpellID > 0) {
                    SpellCastSubMenu* menu = (SpellCastSubMenu*)((&(myUI->m_subMenus)) + ((newTab) * (sizeof(SpellCastSubMenu) / 4)));
                    WriteToChat(menu->ToString());
                    menu->SetSelected(newSpellID);
                }
            }
            // update selected icon
            myUI->UpdateEndowmentIcon(); //throws AccessViolation when tab is active
            // update selected spell name
            myUI->UpdateCastButtonTooltip();
        }
        public System.Collections.Generic.List<UInt32> SpellTabElements = new System.Collections.Generic.List<uint> { 0x100000A3, 0x100000A4, 0x100000A5, 0x100000A6, 0x100000A7, 0x100000A8, 0x100000A9, 0x100005C2 };

        public unsafe void populate_tabbedSpells() {
            var pm = (*CPlayerSystem.s_pPlayerSystem)->playerModule.PlayerModule;
            System.Collections.Generic.Dictionary<UInt32, UInt16[]> ret = new System.Collections.Generic.Dictionary<UInt32, UInt16[]>();
            ushort index = 0;
            for (sbyte tab = 0; tab < 8; tab++) {
                PackableLLNode<uint>* iter = pm.GetFavoriteSpellsList(tab)->head;
                while (iter != null) {
                    UInt32 spellId = iter->data;
                    if (ret.ContainsKey(spellId)) {
                        ret[spellId][tab] = index;
                    }
                    else {
                        var tsa = new UInt16[8];
                        tsa[tab] = index;
                        ret.Add(spellId, tsa);
                    }
                    iter = iter->next;
                    index++;
                }
                index = 0;
            }
            tabbedSpells = ret;
        }
        public System.Collections.Generic.Dictionary<UInt32, UInt16[]> tabbedSpells = null;
        public string[] numerals = new string[] { "I", "II", "III", "IV", "V", "VI", "VII", "VIII" };

        public unsafe int TabFromSpellID(UInt32 spellId) {
            var cSpellBase = ClientMagicSystem.s_pMagicSystem->spellTable->GetSpellBase(spellId);
            switch (cSpellBase->_school) {
                case 1: // War Magic
                    return 4;
                case 2: // Life Magic
                    return 5;
                case 3: // Item Enchantment
                    return 7;
                case 4: // Creature Enchantment
                    return 6;
                case 5: // Void Magic
                    return 4;
            }
            return 0;
        }
        private unsafe void CharacterFilter_SpellCast(object sender, SpellCastEventArgs e) {
            if (!Quiet && Spells.TryGetBestCastable((uint)e.SpellId, out var bestId, IgnoreSkill ? 0 : DifficultyModifier, IgnoreComps) && bestId != (uint)e.SpellId)
                Logger.WriteToChat($"You could be casting {Spells.GetName(bestId)} instead.");

            int tab = TabFromSpellID((uint)e.SpellId);

            // does not contain spell on any tab, create array, and add it
            if (tabbedSpells == null || !tabbedSpells.ContainsKey((uint)e.SpellId)) {
                // re-populate our hash, and to see if the user manually added it
                populate_tabbedSpells();
                if (!tabbedSpells.ContainsKey((uint)e.SpellId)) AddFavorite((uint)e.SpellId, tab, (int)(*CPlayerSystem.s_pPlayerSystem)->playerModule.PlayerModule.GetFavoriteSpellsList(tab)->curNum, false, false, Quiet);
            }
        }

        public unsafe void AddFavorite(UInt32 _spellID, Int32 _tab, Int32 _index, bool _allowReplace, bool _skipUI, bool quiet) {
            if (tabbedSpells == null) populate_tabbedSpells();
            if (!tabbedSpells.ContainsKey(_spellID)) tabbedSpells.Add(_spellID, new UInt16[8]);
            tabbedSpells[_spellID][_tab] = (UInt16)_index;
            // I see you looking at this, but.... sigh
            var myUI = (gmSpellcastingUI*)GlobalEventHandler.geh->ResolveHandler(5100110);
            SpellCastSubMenu* menu = (SpellCastSubMenu*)((&(myUI->m_subMenus)) + ((_tab) * (sizeof(SpellCastSubMenu) / 4)));

            // get spell name, for prettiness
            AC1Legacy.PStringBase<char> name = new AC1Legacy.PStringBase<char>();
            ClientMagicSystem.s_pMagicSystem->GetSpellName(&name, _spellID);

            // if index came in as -1, add to end:
            if (_index == -1) _index = (int)menu->m_numSpells;

            // first run, populate:
            if (tabbedSpells == null) populate_tabbedSpells();
            // does not contain spell on any tab, create array, and add it
            if (!tabbedSpells.ContainsKey(_spellID)) tabbedSpells.Add(_spellID, new UInt16[8]);

            tabbedSpells[_spellID][_tab] = (UInt16)_index;

            if (!quiet) WriteToChat($"Adding {name} (0x{_spellID:X4}) to tab {numerals[_tab]}, index {_index}");

            // add favorite in client
            menu->AddFavorite(_spellID, _index, (byte)(_allowReplace ? 1 : 0));

            if (!_skipUI) UpdateUI();
        }

        /// <summary>
        /// Focus Spell, or add it if missing.
        /// </summary>
        /// <param name="_spellID">spell id</param>
        /// <param name="_index">0-nnn, -1 = add to end</param>
        /// <param name="_allowReplace">if index collides with existing spell, replace? (false=insert)</param>
        /// <param name="quiet">report actions in chat window</param>
        public unsafe void FocusSpellorAdd(UInt32 _spellID, Int32 _index, bool _allowReplace, bool quiet) {
            if (FocusSpell(_spellID)) return;
            var _tab = TabFromSpellID(_spellID);
            AddFavorite(_spellID, _tab, (int)(*CPlayerSystem.s_pPlayerSystem)->playerModule.PlayerModule.GetFavoriteSpellsList(_tab)->curNum, false, false, Quiet);

        }

        /// <summary>
        /// change to appropriate tab, and select spell.
        /// </summary>
        /// <param name="_spellID"></param>
        /// <returns></returns>
        public unsafe bool FocusSpell(UInt32 _spellID) {
            // first run, populate:
            if (tabbedSpells == null) populate_tabbedSpells();
            if (!tabbedSpells.ContainsKey(_spellID)) return false;
            int _needle = -1;
            for (int _tab = 0; _tab < 8; _tab++) {
                if (tabbedSpells[_spellID][_tab] > 0) {
                    _needle = _tab;
                    break;
                }
            }
            UpdateUI(_needle, _spellID);
            return true;
        }
        #endregion
    }
}
