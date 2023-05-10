using Decal.Adapter;
using Decal.Adapter.Wrappers;
using Decal.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UtilityBelt.Service.Lib.Settings;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Constants;
using VirindiViewService;
using static uTank2.PluginCore;
using HarmonyLib;
using System.Reflection.Emit;
using System.Collections.ObjectModel;
using uTank2;

namespace UtilityBelt.Tools {
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    internal class VTankPatchSetting : Attribute {}
    [Name("VTankExtensions")]
    public class VTankExtensions : ToolBase {
        internal static Harmony harmonyClassic;
        internal static Dictionary<string, Harmony> HarmonyPatches = new();
        internal static Harmony harmonyExpressions;
        internal static VTankExtensions instance;

        private struct CastReplacementInfo {
            public string Name { get; set; }
            public int FallbackId { get; set; }
            public object Spell { get; set; }
            public int Family { get; set; }
        }

        #region config
        [Summary("Enable automatically attempting to detect classic servers and patching vtank to support old skills.")]
        public readonly Setting<bool> EnableAutoClassicPatch = new Setting<bool>(true);
        [VTankPatchSetting]
        [Summary("Additional debuffs to cast. Debuffs cast in order directly after Magic Yield.")]
        public readonly Setting<ObservableCollection<string>> AdditionalDebuffs = new Setting<ObservableCollection<String>>(new ObservableCollection<string>());
        [VTankPatchSetting]
        [Summary("Will attempt to ignore some errors that can crash VTank")]       
        public readonly Setting<bool> DontStopOnError = new Setting<bool>(true);
        [VTankPatchSetting]
        [Summary("Use real client locations from UB Network when following")]       
        public readonly Setting<bool> BetterFollowing = new Setting<bool>(true);
        #endregion // config

        private Dictionary<string, Harmony> VTankSettingPatches = new();

        #region Commands
        #region /ub dumpskills
        [Summary("Prints all skills and training levels contained in Login_PlayerDesc (0x0013) to chat")]
        [Usage("/ub dumpskills")]
        [Example("/ub dumpskills", "Prints all skills and training levels to chat")]
        [CommandPattern("dumpskills", @"^$")]
        public void DumpSkills(string _, Match _1) {
            var skillIds = UBLoader.FilterCore.PlayerDescSkillState.Keys.ToList();
            skillIds.Sort();
            Logger.WriteToChat($"Login_PlayerDesc (0x0013) Skills:");
            foreach (var skillId in skillIds) {
                var skillTraining = UBLoader.FilterCore.PlayerDescSkillState[skillId];
                Logger.WriteToChat($"  {(Skills)skillId} ({skillId}) = {(TrainingType)skillTraining} ({skillTraining})");
            }
        }
        #endregion
        #endregion // Commands

        private static Dictionary<string, object> PatchedAuraSpells = new Dictionary<string, object>();
        private static Dictionary<int, CastReplacementInfo> CastReplacements = new Dictionary<int, CastReplacementInfo>();
        private static Dictionary<int, int> LevelSevenLookup = new Dictionary<int, int>();

        private static Dictionary<int, int> PlayerDescSkillState { get => UBLoader.FilterCore.PlayerDescSkillState; }

        public VTankExtensions(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        public override void Init() {
            base.Init();

            if (UBHelper.Core.GameState == UBHelper.GameState.In_Game) {
                CheckForClassic();
            }
            else {
                UBHelper.Core.GameStateChanged += Core_GameStateChanged;
            }

            // so we can reference settings and things from static methods used in the transpiler patch
            instance = this;
            PatchVTankExtensions();          
        }

        private void VTankPatchSettingChanged(object sender, SettingChangedEventArgs e) {
            var state = e.Setting.GetValue();
            var patchName = e.Setting.Name;
            var patchNamespace = harmonyNamespace + "." + patchName;
            if (IsTruthy(state) && !VTankSettingPatches.ContainsKey(patchNamespace)) {
                var harmony = new Harmony(patchNamespace);
                this.GetType().GetMethod(e.Setting.Name + "Patch", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(this, new[] { harmony });
                VTankSettingPatches[patchNamespace] = harmony;
            } else if (!IsTruthy(state) && VTankSettingPatches.ContainsKey(patchNamespace)) {
                VTankSettingPatches[patchNamespace].UnpatchAll(patchNamespace);
                VTankSettingPatches.Remove(patchNamespace);
            }
        }

        private void Core_GameStateChanged(UBHelper.GameState previous, UBHelper.GameState new_state) {
            try {
                if (new_state == UBHelper.GameState.In_Game) {
                    CheckForClassic();
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void CheckForClassic() {
            if (!EnableAutoClassicPatch)
                return;
            if (!UBLoader.FilterCore.PlayerDescSkillState.ContainsKey((int)UtilityBelt.Common.Enums.SkillId.Summoning)) {
                PatchVTankClassic();
            }
        }

        #region VTank Patches
        /// <summary>
        /// EvaluateExpression delegate
        /// </summary>
        /// <param name="expressionString">Expression string to evaluate</param>
        public delegate object Del_EvaluateExpression(string expressionString, bool silent = false);
        private static Del_EvaluateExpression expressionHandler = null;
        private static string harmonyNamespace = "com.ubhelper.vtank";

        private static object[] nl = new object[1000];
        private static object spellFactory;
        private static MethodInfo getMySpellFunc;
        private static bool isClassicPatched;

        public void PatchVTankExtensions() {
            try {
                // appply all the patches by their settings
                this.GetType()
                    .GetFields()
                    .Where(field => field.IsDefined(typeof(VTankPatchSetting)))
                    .Do(field => {
                        var patchHandler = this.GetType().GetMethod(nameof(VTankPatchSettingChanged), BindingFlags.NonPublic | BindingFlags.Instance);
                        var settingField = field.GetValue(this);
                        var eventField = settingField.GetType().GetEvent("Changed");
                        eventField.AddEventHandler(field.GetValue(this), Delegate.CreateDelegate(eventField.EventHandlerType, this, patchHandler));
                        patchHandler.Invoke(this, new[] {null, new SettingChangedEventArgs((string)settingField.GetPropValue("Name"), (string)settingField.GetPropValue("FullName"), (ISetting)settingField) });
                    });
            }
            catch (Exception e) {
                Logger.LogException(e);
            }
        }

        private void AdditionalDebuffsPatch(object patchHarmony) {
            var harmony = (Harmony)patchHarmony;
            Type pickSpell = typeof(uTank2.PluginCore).Assembly.GetType("hi");
            Type arg1 = typeof(uTank2.PluginCore).Assembly.GetType("hi+a");
            harmony.Patch(AccessTools.Method(pickSpell, "a", new Type[] { arg1.MakeByRefType() }), transpiler: new HarmonyMethod(typeof(VTankExtensions), nameof(PickSpellTranspiler)));
        }

        private void DontStopOnErrorPatch(object patchHarmony) {
            var harmony = (Harmony)patchHarmony;
            // This is the vtank message for doing meta message matching
            Type messageProcessor = typeof(uTank2.PluginCore).Assembly.GetType("hl");
            harmony.Patch(AccessTools.Method(messageProcessor, "f"), finalizer: new HarmonyMethod(typeof(VTankExtensions), nameof(IgnoreMessageExceptions)));
        }

        private void BetterFollowingPatch(object patchHarmony) {
            var harmony = (Harmony)patchHarmony;
            Type vtankWorld = typeof(uTank2.PluginCore).Assembly.GetType("f9");
            // This is the VTank method is for getting location of world objects
            // The patch is to retrieve more exact positions of clients within UB Networking
            harmony.Patch(AccessTools.Method(vtankWorld, "a", new Type[] { typeof(int), typeof(HooksWrapper) }), prefix: new HarmonyMethod(typeof(VTankExtensions), nameof(FollowExactly)));
        }

        public static void FollowExactly(ref sCoord __result, int A_0, HooksWrapper A_1) {
            var clients = VTankExtensions.instance.UB.Networking.Clients.ToList();
            foreach(var client in clients) {
                if (client.PlayerId == A_0 && client.HasPositionInfo) {
                    __result = new sCoord(client.EW, client.NS, client.Z);
                }
            }
        }

        public static void PatchVTankExpressions(Del_EvaluateExpression handler) {
            try {
                if (harmonyExpressions == null)
                    harmonyExpressions = new Harmony(harmonyNamespace + ".expressions");

                if (expressionHandler != null) {
                    Logger.Error("[UB] VTank expressions are already patched! Skipping");
                    return;
                }

                expressionHandler = handler;

                #region ExpAct
                Type vtExprAct = typeof(uTank2.PluginCore).Assembly.GetType("dv");
                MethodInfo vtExprAct_original = vtExprAct.GetMethod("c", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { }, null);
                MethodInfo vtExprAct_prefix = typeof(VTankExtensions).GetMethod("ExecuteExpressionActionPatch_Prefix");
                harmonyExpressions.Patch(vtExprAct_original, new HarmonyMethod(vtExprAct_prefix));
                #endregion

                #region Expression
                Type vtExpression = typeof(uTank2.PluginCore).Assembly.GetType("b3");
                MethodInfo vtExpression_original = vtExpression.GetMethod("c", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { }, null);
                MethodInfo vtExpression_prefix = typeof(VTankExtensions).GetMethod("ExecuteExpressionPatch_Prefix");
                harmonyExpressions.Patch(vtExpression_original, new HarmonyMethod(vtExpression_prefix));
                #endregion

                #region ChatCapture
                Type vtChatCapture = typeof(uTank2.PluginCore).Assembly.GetType("c5");
                MethodInfo vtChatCapture_original = vtChatCapture.GetMethod("c", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { }, null);
                MethodInfo vtChatCapture_prefix = typeof(VTankExtensions).GetMethod("ExecuteChatCapturePatch_Prefix");
                harmonyExpressions.Patch(vtChatCapture_original, new HarmonyMethod(vtChatCapture_prefix));
                #endregion

                #region ButtonExpression
                Type vtButtonExpression = typeof(uTank2.PluginCore).Assembly.GetType("c9");
                MethodInfo vtButtonExpression_original = vtButtonExpression.GetMethod("a", BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(object), typeof(EventArgs) }, null);
                MethodInfo vtButtonExpression_prefix = typeof(VTankExtensions).GetMethod("ExecuteButtonExpressionPatch_Prefix");
                harmonyExpressions.Patch(vtButtonExpression_original, new HarmonyMethod(vtButtonExpression_prefix));
                #endregion

                #region ExpressionChatAction
                Type vtExpressionChatAction = typeof(uTank2.PluginCore).Assembly.GetType("n");
                MethodInfo vtExpressionChatAction_original = vtExpressionChatAction.GetMethod("c", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { }, null);
                MethodInfo vtExpressionChatAction_prefix = typeof(VTankExtensions).GetMethod("ExecuteExpressionChatActionPatch_Prefix");
                harmonyExpressions.Patch(vtExpressionChatAction_original, new HarmonyMethod(vtExpressionChatAction_prefix));
                #endregion

                #region VTOptGet
                Type vtOptGet = typeof(uTank2.PluginCore).Assembly.GetType("fl");
                MethodInfo vtOptGet_original = vtOptGet.GetMethod("c", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { }, null);
                MethodInfo vtOptGet_prefix = typeof(VTankExtensions).GetMethod("ExecuteOptGet_Prefix");
                harmonyExpressions.Patch(vtOptGet_original, new HarmonyMethod(vtOptGet_prefix));
                #endregion

                #region VTOptSet
                Type vtOptSet = typeof(uTank2.PluginCore).Assembly.GetType("dt");
                MethodInfo vtOptSet_original = vtOptSet.GetMethod("c", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { }, null);
                MethodInfo vtOptSet_prefix = typeof(VTankExtensions).GetMethod("ExecuteOptSet_Prefix");
                harmonyExpressions.Patch(vtOptSet_original, new HarmonyMethod(vtOptSet_prefix));
                #endregion
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public static Exception IgnoreMessageExceptions(Exception __exception, ref bool __result) {
            //harmony itself can generate a null reference exception
            if (__exception != null && __exception is InvalidOperationException || __exception is NullReferenceException) {
                Logger.LogException(__exception);
                __result = false;
                return null;
            }

            return __exception;
        }

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> PickSpellTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilg) {
            var instructionList = instructions.ToList();
            var targetLabel = ilg.DefineLabel();
            var seenLdloc3 = 0;
            var retSeen = 0;
            Label retLabel = ilg.DefineLabel();
            //this gets the label from the end that most branches jump to return
            for (var i = 0; i < instructionList.Count; i++) {
                var instr = instructionList[i];
                if (instr.opcode == OpCodes.Ret) {
                    retSeen++;
                    if (retSeen == 2) {
                        retLabel = instr.labels[0];
                    }
                }
            }
            for (var i = 0; i < instructionList.Count; i++) {
                var instr = instructionList[i];
                yield return instr;
                if (instr.opcode == OpCodes.Ldloc_3) {
                    seenLdloc3++;
                    if (seenLdloc3 != 5) {
                        continue;
                    }
                    //inject at the 5th ldloc.3, this injects after the magic yield check before the weakening curse check
                    var continueLabel = ilg.DefineLabel();
                    var method = AccessTools.Method(typeof(VTankExtensions), nameof(CastAdditionalDebuffs));
                    yield return new CodeInstruction(OpCodes.Pop); //remove the just pushed value
                    yield return new CodeInstruction(OpCodes.Ldarg_0); //push this (hi.cs)
                    yield return new CodeInstruction(OpCodes.Ldloc_3); //push local variable a2 that has the monster data
                    yield return new CodeInstruction(OpCodes.Call, method); //receives this
                    yield return new CodeInstruction(OpCodes.Brfalse_S, continueLabel); //jump to the nop if we didnt choose to cast a debuff
                    yield return new CodeInstruction(OpCodes.Ldarg_1); //A_0 = hi.a.b; this gets set after every debuff
                    yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                    yield return new CodeInstruction(OpCodes.Stind_I4);
                    yield return new CodeInstruction(OpCodes.Leave, retLabel); //leave the protected area to return.                 
                    var nop = new CodeInstruction(OpCodes.Nop); //jump to a nop to continue back to checking weakening curse
                    nop.labels.Add(continueLabel);
                    yield return nop;
                    yield return instr; //redo the pushed value
                }
            }
        }

        public static bool CastAdditionalDebuffs(object hi, object d1_a) {
            var debuffs = VTankExtensions.instance.AdditionalDebuffs;
            try {
                //logic to copy from vtank to test a spell at the right level, find the remaining time, then cast it
                // if (a2.i && dz.j.b(a1.b, dz.p.a(dz.f.b("Magic Yield Other I"), a1)) <= timeSpan)
                /*
                {
                    A_0 = hi.a.b;
                    this.c = dz.f.b("Magic Yield Other I");
                    this.d = hi.b.b;
                    return;
                }
                */
                //first check is only continue if yield is enabled for the monster. a2.i &&
                var shouldYield = (bool)d1_a.GetFieldValue("i");

                if (!shouldYield) {
                    return false;
                }

                for (int i = 0; i < debuffs.Value.Count; i++) {
                    var debuff = debuffs.Value[i];
                    var dz = typeof(uTank2.PluginCore).GetField("dz", BindingFlags.NonPublic | BindingFlags.Static).GetValue(uTank2.PluginCore.PC);
                    spellFactory = dz.GetType().GetField("f", BindingFlags.Public | BindingFlags.Instance).GetValue(dz);
                    getMySpellFunc = spellFactory.GetType().GetMethod("b", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(string) }, null);

                    // dz.f.b("spell name") get a spell from whatever spell factory/dict
                    var spell = (uTank2.MySpell)getMySpellFunc.Invoke(spellFactory, new object[] { debuff });

                    Type pickSpell = typeof(uTank2.PluginCore).Assembly.GetType("hi");
                    var something = typeof(uTank2.PluginCore).Assembly.GetType("hi+b");

                    var a1 = dz.GetFieldValue("p").GetFieldValue("a");
                    var dzpa = dz.GetFieldValue("p").GetType().GetMethod("a", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(uTank2.MySpell), typeof(uTank2.PluginCore).Assembly.GetType("f7") }, null);

                    // dz.p.a(spellresult, a1) looks like this changes the chosen spell to be debuff fallback and maybe the right level to cast?
                    var dzpaResult = dzpa.Invoke(dz.GetFieldValue("p"), new object[] { spell, a1 });

                    // dz.j.b(a1.b, dzparesult) = timespan
                    var timeRemaining = (TimeSpan)dz.GetFieldValue("j").GetType().GetMethod("b", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { a1.GetFieldValue("b").GetType(), dzpaResult.GetType() }, null).Invoke(dz.GetFieldValue("j"), new object[] { a1.GetFieldValue("b"), dzpaResult });
                    // if timeremainging on the debuff is under 10 seconds. todo implement checking the rebuff time setting.
                    if (timeRemaining.Seconds < 10) {
                        var spellField = pickSpell.GetField("c", BindingFlags.NonPublic | BindingFlags.Instance);
                        //this.c = spell
                        spellField.SetValue(hi, spell);
                        //this.d = hi.b.something looks like this a flag to track what debuff was applied. 1 is the magic yield debuff.
                        pickSpell.GetField("d", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(hi, System.Enum.GetValues(something).GetValue(1));
                        return true;
                    }
                }
            }
            catch (Exception e) {
                Logger.LogException(e);
            }
            return false;
        }

        public static void UnpatchVTankExpressions() {
            if (expressionHandler == null)
                return;
            harmonyExpressions.UnpatchAll(harmonyNamespace + ".expressions");
            expressionHandler = null;
        }

        public static void PatchVTankClassic() {
            try {
                if (isClassicPatched)
                    return;

                isClassicPatched = true;

                Logger.WriteToChat($"Classic server detected, automatically patching vtank to support old skills.");

                // make sure we are using eor dats...
                var fs = CoreManager.Current.Filter<FileService>();
                if (fs.SkillTable.GetByName("Axe") != null) {
                    for (var i = 0; i < 5; i++) {
                        Logger.Error("Error applying classic patch.  Decal is pointing to classic dats but should be pointing to EOR dats. Re-run the decal installer and upon opening decal make sure it is pointing at EOR dats.");
                    }
                    return;
                }

                if (harmonyClassic == null)
                    harmonyClassic = new Harmony(harmonyNamespace + ".classic");

                #region patch AddSpellsBySkillId
                Type vtankBuffing = typeof(uTank2.PluginCore).Assembly.GetType("eq");

                if (vtankBuffing == null) {
                    Logger.Error("Error applying classic patch.  Could not find type 'eq'. Reinstall vtank to ensure you are running the correct version.");
                    return;
                }

                MethodInfo addSpellsBySkillId_original = vtankBuffing.GetMethod("a", BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(int), typeof(string), typeof(uTank2.MyList<int>).MakeByRefType() }, null);

                if (addSpellsBySkillId_original == null) {
                    Logger.Error("Error applying classic patch.  Could not find type 'eq.a'. Reinstall vtank to ensure you are running the correct version.");
                    return;
                }
                MethodInfo addSpellsBySkillId_Prefix = typeof(VTankExtensions).GetMethod("AddSpellsBySkillId_Prefix");
                harmonyClassic.Patch(addSpellsBySkillId_original, new HarmonyMethod(addSpellsBySkillId_Prefix));
                #endregion patch AddSpellsBySkillId

                #region patch GetSkillNameFromId
                MethodInfo getSkillNameFromId_original = vtankBuffing.GetMethod("a", BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(int) }, null);
                MethodInfo getSkillNameFromId_prefix = typeof(VTankExtensions).GetMethod("GetSkillNameFromId_Prefix");
                harmonyClassic.Patch(getSkillNameFromId_original, new HarmonyMethod(getSkillNameFromId_prefix));
                #endregion patch GetSkillNameFromId

                #region patch translateSpell
                MethodInfo translateSpell_original = typeof(uTank2.PluginCore).Assembly.GetType("fk").GetMethod("b", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(uTank2.MySpell), typeof(bool) }, null);
                MethodInfo translateSpell_postfix = typeof(VTankExtensions).GetMethod("TranslateSpell_Postfix");
                harmonyClassic.Patch(translateSpell_original, null, new HarmonyMethod(translateSpell_postfix));
                #endregion patch translateSpell

                #region patch CastSpell
                var s = PC.GetType().GetField("dz", BindingFlags.Static | BindingFlags.NonPublic).GetValue(PC);
                var sh = s.GetType().GetField("h", BindingFlags.Instance | BindingFlags.Public).GetValue(s);
                MethodInfo castSpell_original = sh.GetType().GetMethod("a", BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(uTank2.MySpell), typeof(int), typeof(bool) }, null);
                MethodInfo castSpell_Prefix = typeof(VTankExtensions).GetMethod("CastSpell_Prefix");
                harmonyClassic.Patch(castSpell_original, new HarmonyMethod(castSpell_Prefix));
                #endregion patch CastSpell


                if (PatchedAuraSpells.Count != 0)
                    return;

                var dz = typeof(uTank2.PluginCore).GetField("dz", BindingFlags.NonPublic | BindingFlags.Static).GetValue(uTank2.PluginCore.PC);
                spellFactory = dz.GetType().GetField("f", BindingFlags.Public | BindingFlags.Instance).GetValue(dz);
                getMySpellFunc = spellFactory.GetType().GetMethod("b", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(string) }, null);
                FieldInfo field = null;
                FieldInfo fieldL = null; // untargeted
                FieldInfo fieldB = null; // name
                var itemEnchantment = SpellSchool.GetByName("Item Enchantment");
                for (var i = 0; i < fs.SpellTable.Length; i++) {
                    var spell = fs.SpellTable[i];
                    var vSpell = (uTank2.MySpell)getMySpellFunc.Invoke(spellFactory, new object[] { spell.Name });
                    if (vSpell == null)
                        continue;
                    if (field == null)
                        field = vSpell.GetType().GetField("a", BindingFlags.NonPublic | BindingFlags.Instance);
                    var a = field.GetValue(vSpell);
                    if (fieldL == null)
                        fieldL = a.GetType().GetField("l", BindingFlags.Public | BindingFlags.Instance);
                    if (fieldB == null)
                        fieldB = a.GetType().GetField("b", BindingFlags.Public | BindingFlags.Instance);
                    if (spell.School == itemEnchantment && spell.Name.StartsWith("Aura of")) {
                        var newName = spell.Name.Replace("Aura of ", "").Replace("Self ", "");
                        fieldB.SetValue(a, newName);
                        fieldL.SetValue(a, false);
                        if (!PatchedAuraSpells.ContainsKey(spell.Name))
                            PatchedAuraSpells.Add(spell.Name, vSpell);
                    }
                    else if (spell.Id >= 298 && spell.Id <= 303) { // Axe mastery self 1-6
                        CastReplacements.Add(spell.Id, new CastReplacementInfo() {
                            Name = spell.Name.Replace("Light Weapon", "Axe"),
                            Spell = vSpell,
                            Family = 17,
                            FallbackId = spell.Id == 298 ? 0 : spell.Id - 1
                        });
                        LevelSevenLookup.Add(spell.Id, 2203);
                    }
                    else if (spell.Id == 2203) { // Axe mastery self 7
                        CastReplacements.Add(spell.Id, new CastReplacementInfo() {
                            Name = "Reenigne's Blessing",
                            Spell = vSpell,
                            Family = 17,
                            FallbackId = 303
                        });
                    }
                    else if (spell.Id >= 467 && spell.Id <= 472) { // Bow mastery self 1-6
                        CastReplacements.Add(spell.Id, new CastReplacementInfo() {
                            Name = spell.Name.Replace("Missile Weapon", "Bow"),
                            Spell = vSpell,
                            Family = 19,
                            FallbackId = spell.Id == 467 ? 0 : spell.Id - 1
                        });
                        LevelSevenLookup.Add(spell.Id, 2207);
                    }
                    else if (spell.Id == 2207) { // Bow mastery self 7
                        CastReplacements.Add(spell.Id, new CastReplacementInfo() {
                            Name = "Strathelar's Blessing",
                            Spell = vSpell,
                            Family = 19,
                            FallbackId = 472
                        });
                    }
                    else if (spell.Id >= 491 && spell.Id <= 496) { // Crossbow mastery self 1-6
                        CastReplacements.Add(spell.Id, new CastReplacementInfo() {
                            Name = spell.Name.Replace("Missile Weapon", "Crossbow"),
                            Spell = vSpell,
                            Family = 21,
                            FallbackId = spell.Id == 491 ? 0 : spell.Id - 1
                        });
                        LevelSevenLookup.Add(spell.Id, 2219);
                    }
                    else if (spell.Id == 2219) { // Crossbow mastery self 7
                        CastReplacements.Add(spell.Id, new CastReplacementInfo() {
                            Name = "Barnar's Blessing",
                            Spell = vSpell,
                            Family = 21,
                            FallbackId = 496
                        });
                    }
                    else if (spell.Id >= 322 && spell.Id <= 327) { // Dagger mastery self 1-6
                        CastReplacements.Add(spell.Id, new CastReplacementInfo() {
                            Name = spell.Name.Replace("Finesse Weapon", "Dagger"),
                            Spell = vSpell,
                            Family = 23,
                            FallbackId = spell.Id == 322 ? 0 : spell.Id - 1
                        });
                        LevelSevenLookup.Add(spell.Id, 2223);
                    }
                    else if (spell.Id == 2223) { // Dagger mastery self 7
                        CastReplacements.Add(spell.Id, new CastReplacementInfo() {
                            Name = "Gertarh's Blessing",
                            Spell = vSpell,
                            Family = 23,
                            FallbackId = 327
                        });
                    }
                    else if (spell.Id >= 346 && spell.Id <= 351) { // Mace mastery self 1-6
                        CastReplacements.Add(spell.Id, new CastReplacementInfo() {
                            Name = spell.Name.Replace("Light Weapon", "Mace"),
                            Spell = vSpell,
                            Family = 25,
                            FallbackId = spell.Id == 346 ? 0 : spell.Id - 1
                        });
                        LevelSevenLookup.Add(spell.Id, 2275);
                    }
                    else if (spell.Id == 2275) { // Mace mastery self 7
                        CastReplacements.Add(spell.Id, new CastReplacementInfo() {
                            Name = "Mi-Krauli's Blessing",
                            Spell = vSpell,
                            Family = 25,
                            FallbackId = 351
                        });
                    }
                    else if (spell.Id >= 370 && spell.Id <= 375) { // Spear mastery self 1-6
                        CastReplacements.Add(spell.Id, new CastReplacementInfo() {
                            Name = spell.Name.Replace("Light Weapon", "Spear"),
                            Spell = vSpell,
                            Family = 27,
                            FallbackId = spell.Id == 370 ? 0 : spell.Id - 1
                        });
                        LevelSevenLookup.Add(spell.Id, 2299);
                    }
                    else if (spell.Id == 2299) { // Spear mastery self 7
                        CastReplacements.Add(spell.Id, new CastReplacementInfo() {
                            Name = "Tibri's Blessing",
                            Spell = vSpell,
                            Family = 27,
                            FallbackId = 375
                        });
                    }
                    else if (spell.Id >= 394 && spell.Id <= 399) { // Staff mastery self 1-6
                        CastReplacements.Add(spell.Id, new CastReplacementInfo() {
                            Name = spell.Name.Replace("Light Weapon", "Staff"),
                            Spell = vSpell,
                            Family = 29,
                            FallbackId = spell.Id == 394 ? 0 : spell.Id - 1
                        });
                        LevelSevenLookup.Add(spell.Id, 2305);
                    }
                    else if (spell.Id == 2305) { // Staff mastery self 7
                        CastReplacements.Add(spell.Id, new CastReplacementInfo() {
                            Name = "Anadil's Blessing",
                            Spell = vSpell,
                            Family = 29,
                            FallbackId = 399
                        });
                    }
                    else if (spell.Id >= 418 && spell.Id <= 423) { // Sword mastery self 1-6
                        CastReplacements.Add(spell.Id, new CastReplacementInfo() {
                            Name = spell.Name.Replace("Heavy Weapon", "Sword"),
                            Spell = vSpell,
                            Family = 31,
                            FallbackId = spell.Id == 418 ? 0 : spell.Id - 1
                        });
                        LevelSevenLookup.Add(spell.Id, 2309);
                    }
                    else if (spell.Id == 2309) { // Sword mastery self 7
                        CastReplacements.Add(spell.Id, new CastReplacementInfo() {
                            Name = "MacNiall's Blessing",
                            Spell = vSpell,
                            Family = 31,
                            FallbackId = 423
                        });
                    }
                    else if (spell.Id >= 539 && spell.Id <= 544) { // Thrown Weapons mastery self 1-6
                        CastReplacements.Add(spell.Id, new CastReplacementInfo() {
                            Name = spell.Name.Replace("Missile Weapon", "Thrown Weapons"),
                            Spell = vSpell,
                            Family = 33,
                            FallbackId = spell.Id == 539 ? 0 : spell.Id - 1
                        });
                        LevelSevenLookup.Add(spell.Id, 2313);
                    }
                    else if (spell.Id == 2313) { // Thrown Weapons mastery self 7
                        CastReplacements.Add(spell.Id, new CastReplacementInfo() {
                            Name = "Asmolum's Blessing",
                            Spell = vSpell,
                            Family = 33,
                            FallbackId = 544
                        });
                    }
                    else if (spell.Id >= 443 && spell.Id <= 448) { // Unarmed Combat mastery self 1-6
                        CastReplacements.Add(spell.Id, new CastReplacementInfo() {
                            Name = spell.Name.Replace("Light Weapon", "Unarmed Combat"),
                            Spell = vSpell,
                            Family = 35,
                            FallbackId = spell.Id == 443 ? 0 : spell.Id - 1
                        });
                        LevelSevenLookup.Add(spell.Id, 2316);
                    }
                    else if (spell.Id == 2316) { // Unarmed Combat mastery self 7
                        CastReplacements.Add(spell.Id, new CastReplacementInfo() {
                            Name = "Hamud's Blessing",
                            Spell = vSpell,
                            Family = 35,
                            FallbackId = 448
                        });
                    }
                }
            }
            catch (Exception ex) {
                Logger.Error($"Error applying classic patch. {ex}"); 
                Logger.LogException(ex);
            }
        }

        public static void UnpatchVTankClassic() {
            try {
                isClassicPatched = false;
                harmonyExpressions.UnpatchAll(harmonyNamespace + ".classic");
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        #region classic vtank overrides
        public static bool CastSpell_Prefix(ref uTank2.MySpell A_0, ref int A_1, ref bool A_2) {
            try {
                //Core.WriteToChat($"CastSpell_Prefix: {A_0.Id} {A_0.Name} // {A_1} // {A_2}");
                //Core.WriteToDebugLog(String.Join(", ", CastReplacements.Select(kv => $"{kv.Key}:{kv.Value}").ToArray()));
                if (CastReplacements.ContainsKey(A_0.Id)) {
                    var a = A_0.GetType().GetField("a", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(A_0);
                    a.GetType().GetField("b", BindingFlags.Public | BindingFlags.Instance).SetValue(a, CastReplacements[A_0.Id].Name);
                    Logger.Debug($"CastSpell Replace: {A_0.Id} {A_0.Name} // {CastReplacements[A_0.Id].Name}");
                }
                //Core.WriteToDebugLog($"CastSpell_postfix: {A_0.Id} {A_0.Name} // {A_1} // {A_2}");
            }
            catch (Exception ex) { Logger.LogException(ex); }
            return true;
        }

        public static void TranslateSpell_Postfix(ref uTank2.MySpell __result, ref uTank2.MySpell A_0, ref bool A_1) {
            try {
                if (!CastReplacements.ContainsKey(A_0.Id))
                    return;

                var spellLevel = 7;
                if (__result.Name.EndsWith(" I"))
                    spellLevel = 1;
                else if (__result.Name.EndsWith(" II"))
                    spellLevel = 2;
                else if (__result.Name.EndsWith(" III"))
                    spellLevel = 3;
                else if (__result.Name.EndsWith(" IV"))
                    spellLevel = 4;
                else if (__result.Name.EndsWith(" V"))
                    spellLevel = 5;
                else if (__result.Name.EndsWith(" VI"))
                    spellLevel = 6;

                var key = spellLevel == 7 ? LevelSevenLookup[A_0.Id] : A_0.Id + spellLevel - 1;
                while (key >= A_0.Id) {
                    if (!CastReplacements.ContainsKey(key))
                        break;
                    var spell = (uTank2.MySpell)CastReplacements[key].Spell;
                    if (CoreManager.Current.CharacterFilter.IsSpellKnown(key) && spell.HasScarabsInInventory)
                        break;
                    key = CastReplacements[key].FallbackId;
                }
                if (key == 0 || !CastReplacements.ContainsKey(key) || !CoreManager.Current.CharacterFilter.IsSpellKnown(key) || !((uTank2.MySpell)CastReplacements[key].Spell).HasScarabsInInventory) {
                    Logger.Debug($"Spell failed: key:{key} isKnown:{CoreManager.Current.CharacterFilter.IsSpellKnown(key)} hasScarabs:{((uTank2.MySpell)CastReplacements[key].Spell).HasScarabsInInventory}");
                    __result = uTank2.MySpell.InvalidSpell;
                    return;
                }
                var actualSpell = CastReplacements[key];
                var a = ((uTank2.MySpell)actualSpell.Spell).GetType().GetField("a", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(actualSpell.Spell);
                a.GetType().GetField("c", BindingFlags.Public | BindingFlags.Instance).SetValue(a, key);
                a.GetType().GetField("e", BindingFlags.Public | BindingFlags.Instance).SetValue(a, actualSpell.Family);
                a.GetType().GetField("d", BindingFlags.Public | BindingFlags.Instance).SetValue(a, actualSpell.Family);

                Logger.Debug($"TranslateSpell_Postfix 2: {A_0.Id} {A_0.Name} // {A_1} -- real: {((uTank2.MySpell)actualSpell.Spell).Id} {actualSpell.Name} {((uTank2.MySpell)actualSpell.Spell).RealFamily} {actualSpell.Family}");
                __result = ((uTank2.MySpell)actualSpell.Spell);
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public static bool AddSpellsBySkillId_Prefix(ref object __instance, int A_0, string A_1, ref uTank2.MyList<int> A_2) {
            try {
                //Core.WriteToChat($"AddSpellsBySkillId_Prefix: A_0: {A_0} A_1: {A_1}  A_2: {string.Join(", ", A_2.Select(i => i.ToString()).ToArray())}");
                if (string.IsNullOrEmpty(A_1))
                    return false;
                switch (A_1) {
                    case "Axe":
                        if (PlayerDescSkillState.ContainsKey(1) && PlayerDescSkillState[1] >= 2)
                            A_2.Add(298);
                        return false;
                    case "Bow":
                        if (PlayerDescSkillState.ContainsKey(2) && PlayerDescSkillState[2] >= 2)
                            A_2.Add(467);
                        return false;
                    case "Crossbow":
                        if (PlayerDescSkillState.ContainsKey(3) && PlayerDescSkillState[3] >= 2)
                            A_2.Add(491);
                        return false;
                    case "Dagger":
                        if (PlayerDescSkillState.ContainsKey(4) && PlayerDescSkillState[4] >= 2)
                            A_2.Add(322);
                        return false;
                    case "Mace":
                        if (PlayerDescSkillState.ContainsKey(5) && PlayerDescSkillState[5] >= 2)
                            A_2.Add(346);
                        return false;
                    case "Spear":
                        if (PlayerDescSkillState.ContainsKey(9) && PlayerDescSkillState[9] >= 2)
                            A_2.Add(370);
                        return false;
                    case "Staff":
                        if (PlayerDescSkillState.ContainsKey(10) && PlayerDescSkillState[10] >= 2)
                            A_2.Add(394);
                        return false;
                    case "Sword":
                        if (PlayerDescSkillState.ContainsKey(11) && PlayerDescSkillState[11] >= 2)
                            A_2.Add(418);
                        return false;
                    case "ThrownWeapon":
                        if (PlayerDescSkillState.ContainsKey(12) && PlayerDescSkillState[12] >= 2)
                            A_2.Add(539);
                        return false;
                    case "UnarmedCombat":
                        if (PlayerDescSkillState.ContainsKey(13) && PlayerDescSkillState[13] >= 2)
                            A_2.Add(443);
                        return false;
                    default:
                        //Core.WriteToChat($"AddSpellsBySkillId a: A_0:{A_0} spellName:{A_1} A_2:{string.Join(", ", A_2.Select(i => i.ToString()).ToArray())}");
                        return true;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return true;
        }

        public static bool GetSkillNameFromId_Prefix(int A_0, ref string __result) {
            try {
                //Core.WriteToChat($"GetSkillNameFromId_Prefix: A_0: {A_0}");
                switch (A_0) {
                    case 1:
                        if (PlayerDescSkillState.ContainsKey(1) && PlayerDescSkillState[1] >= 2)
                            __result = "Axe";
                        return false;
                    case 2:
                        if (PlayerDescSkillState.ContainsKey(2) && PlayerDescSkillState[2] >= 2)
                            __result = "Bow";
                        return false;
                    case 3:
                        if (PlayerDescSkillState.ContainsKey(3) && PlayerDescSkillState[3] >= 2)
                            __result = "Crossbow";
                        return false;
                    case 4:
                        if (PlayerDescSkillState.ContainsKey(4) && PlayerDescSkillState[4] >= 2)
                            __result = "Dagger";
                        return false;
                    case 5:
                        if (PlayerDescSkillState.ContainsKey(5) && PlayerDescSkillState[5] >= 2)
                            __result = "Mace";
                        return false;
                    case 9:
                        if (PlayerDescSkillState.ContainsKey(9) && PlayerDescSkillState[9] >= 2)
                            __result = "Spear";
                        return false;
                    case 10:
                        if (PlayerDescSkillState.ContainsKey(10) && PlayerDescSkillState[10] >= 2)
                            __result = "Staff";
                        return false;
                    case 11:
                        if (PlayerDescSkillState.ContainsKey(11) && PlayerDescSkillState[11] >= 2)
                            __result = "Sword";
                        return false;
                    case 12:
                        if (PlayerDescSkillState.ContainsKey(12) && PlayerDescSkillState[12] >= 2)
                            __result = "ThrownWeapon";
                        return false;
                    case 13:
                        if (PlayerDescSkillState.ContainsKey(13) && PlayerDescSkillState[13] >= 2)
                            __result = "UnarmedCombat";
                        return false;
                    default:
                        //var fs = Decal.Adapter.CoreManager.Current?.Filter<FileService>();
                        //var skill = fs.SkillTable.GetById(A_0);
                        //if (skill == null || string.IsNullOrEmpty(skill.Name))
                        //    Core.WriteToChat($"GetSkillNameFromId: Bad Skill {A_0}");
                        return true;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return true;
        }

        #endregion classic vtank overrides

        #region expression patch overrides
        public static bool ExecuteExpressionActionPatch_Prefix(ref object __instance, ref bool __result) {
            try {
                if (expressionHandler == null)
                    return true;

                var gy = typeof(uTank2.PluginCore).Assembly.GetType("gy");
                var a = __instance.GetType().GetField("a", BindingFlags.NonPublic | BindingFlags.Instance);
                System.Collections.IDictionary ad = (System.Collections.IDictionary)a.GetValue(__instance);
                string e = ad["e"].ToString();
                var res = expressionHandler.Invoke(e);
                __result = true;
                return false;
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return false;
        }

        public static bool ExecuteOptGet_Prefix(ref object __instance, ref bool __result) {
            try {
                if (expressionHandler == null)
                    return true;

                var gy = typeof(uTank2.PluginCore).Assembly.GetType("gy");
                var a = __instance.GetType().GetField("a", BindingFlags.NonPublic | BindingFlags.Instance);
                System.Collections.IDictionary ad = (System.Collections.IDictionary)a.GetValue(__instance);
                string optionName = ad["o"].ToString();
                string variableName = ad["v"].ToString();
                var res = expressionHandler.Invoke($"setvar[{variableName},{UBHelper.vTank.Instance.GetSetting(optionName)}]");
                __result = true;
                return false;
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return false;
        }

        public static bool ExecuteOptSet_Prefix(ref object __instance, ref bool __result) {
            try {
                if (expressionHandler == null)
                    return true;

                var gy = typeof(uTank2.PluginCore).Assembly.GetType("gy");
                var a = __instance.GetType().GetField("a", BindingFlags.NonPublic | BindingFlags.Instance);
                System.Collections.IDictionary ad = (System.Collections.IDictionary)a.GetValue(__instance);
                string optionName = ad["o"].ToString();
                string newValueExpression = ad["v"].ToString();
                var settingType = UBHelper.vTank.Instance.GetSettingType(optionName);
                var res = expressionHandler.Invoke(newValueExpression);
                if (settingType == typeof(string)) {
                    UBHelper.vTank.Instance.SetSetting(optionName, res.ToString());
                }
                else if (settingType == typeof(bool)) {
                    if (!(res is double))
                        Logger.Error($"VTOptSet: Attempted to set a setting of type boolean with a {res.GetType()}.  Should be a number.");
                    else
                        UBHelper.vTank.Instance.SetSetting(optionName, ((double)res == 1) ? true : false);
                }
                else if (settingType == typeof(double)) {
                    if (!(res is double))
                        Logger.Error($"VTOptSet: Attempted to set a setting of type double with a {res.GetType()}.  Should be a number.");
                    else
                        UBHelper.vTank.Instance.SetSetting(optionName, (double)res);
                }
                else if (settingType == typeof(int)) {
                    if (!(res is double))
                        Logger.Error($"VTOptSet: Attempted to set a setting of type int with a {res.GetType()}.  Should be a number.");
                    else
                        UBHelper.vTank.Instance.SetSetting(optionName, Convert.ToInt32((double)res));
                }
                else if (settingType == typeof(float)) {
                    if (!(res is double))
                        Logger.Error($"VTOptSet: Attempted to set a setting of type float with a {res.GetType()}.  Should be a number.");
                    else
                        UBHelper.vTank.Instance.SetSetting(optionName, Convert.ToSingle((double)res));
                }
                else {
                    Logger.Error($"VTOptSet: Attempted to set a setting of unknown type: {settingType} from {res.GetType()}.");
                }
                __result = true;
                return false;
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return false;
        }

        public static bool ExecuteExpressionPatch_Prefix(ref object __instance, ref bool __result) {
            try {
                if (expressionHandler == null)
                    return true;

                var gy = typeof(uTank2.PluginCore).Assembly.GetType("gy");
                var a = __instance.GetType().GetField("a", BindingFlags.NonPublic | BindingFlags.Instance);
                System.Collections.IDictionary ad = (System.Collections.IDictionary)a.GetValue(__instance);
                string e = ad["e"].ToString();
                var res = expressionHandler.Invoke(e, true);
                __result = IsTruthy(res);
                return false;
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return false;
        }

        public static bool ExecuteChatCapturePatch_Prefix(ref object __instance, ref bool __result) {
            try {
                if (expressionHandler == null)
                    return true;
                Regex re = (Regex)__instance.GetType().GetField("a", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance);

                if (re == null) {
                    __result = false;
                    return false;
                }

                Type a7 = typeof(uTank2.PluginCore).Assembly.GetType("a7");
                Type c5 = typeof(uTank2.PluginCore).Assembly.GetType("c5");
                MethodInfo a = a7.GetMethod("a", BindingFlags.Public | BindingFlags.Static, null, CallingConventions.Standard, new Type[] { }, null);
                bool c = (bool)c5.GetField("c", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance);
                Dictionary<int, bool> b = (Dictionary<int, bool>)__instance.GetType().GetField("b", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance);
                System.Collections.IList l = (System.Collections.IList)a.Invoke(null, new object[] { });
                var count = l.Count;
                l.CopyTo(nl, 0);
                for (var i = 0; i < count; i++) {
                    var o = nl[i];
                    var message = o.GetType().GetField("a").GetValue(o).ToString();
                    var color = (int)o.GetType().GetField("b").GetValue(o);
                    var oc = (int)o.GetType().GetField("c").GetValue(o); // ? always 0?

                    if (c || b.ContainsKey(color)) {
                        Match match = re.Match(message);
                        if (match.Success) {
                            foreach (string groupName in re.GetGroupNames()) {
                                Group group = match.Groups[groupName];
                                string key = "capturegroup_" + groupName;
                                if (group.Success)
                                    expressionHandler.Invoke($"setvar[`{key}`,`{group.Value}`]", true);
                                else
                                    expressionHandler.Invoke($"clearvar[`{key}`]", true);
                            }
                            expressionHandler.Invoke($"setvar[`capturecolor`,{color}]", true);
                            __result = true;
                            return false;
                        }
                    }
                }

                __result = false;
                return false;
            }
            catch (Exception ex) { Logger.LogException(ex); }
            __result = false;
            return false;
        }

        public static bool ExecuteButtonExpressionPatch_Prefix(ref object __instance) {
            try {
                var a = (string)__instance.GetType().GetField("a", BindingFlags.Public | BindingFlags.Instance).GetValue(__instance);
                var b = (string)__instance.GetType().GetField("b", BindingFlags.Public | BindingFlags.Instance).GetValue(__instance);
                if (!String.IsNullOrEmpty(a))
                    expressionHandler.Invoke(a);

                if (!String.IsNullOrEmpty(b))
                    expressionHandler.Invoke($"vtsetmetastate[`{b}`]", true);
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return false;
        }

        public static bool ExecuteExpressionChatActionPatch_Prefix(ref object __instance, ref bool __result) {
            try {
                var a = __instance.GetType().GetField("a", BindingFlags.NonPublic | BindingFlags.Instance);
                System.Collections.IDictionary ad = (System.Collections.IDictionary)a.GetValue(__instance);
                string e = ad["e"].ToString();
                var res = expressionHandler.Invoke($"chatbox[{e}]", true);
            }
            catch (Exception ex) { Logger.LogException(ex); }

            return false;
        }
        #endregion expression patch overrides

        #region Operator Helpers
        /// <summary>
        /// Checks if an object is "truthy"
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        internal static bool IsTruthy(object obj) {
            if (obj.GetType() == typeof(double))
                return (double)obj != 0;
            if (obj.GetType() == typeof(string))
                return ((string)obj).Length > 0;
            if (obj.GetType() == typeof(bool))
                return (bool)obj;
            var genArgs = obj.GetType().GetGenericArguments();
            if (genArgs.Length == 1 && typeof(Collection<>).MakeGenericType(genArgs).IsAssignableFrom(obj.GetType().BaseType)) {
                return (int)obj.GetType().GetProperty("Count").GetValue(obj) > 0;
            }

            return true;
        }
        #endregion

        #region vtank internals
        public static bool TryEquipAnyWand() {
            //TODO: implement this fully in ub
            FieldInfo fieldInfo = uTank2.PluginCore.PC.GetType().GetField("dz", BindingFlags.NonPublic | BindingFlags.Static);
            var dz = fieldInfo.GetValue(uTank2.PluginCore.PC);
            FieldInfo ofieldInfo = dz.GetType().GetField("o", BindingFlags.Public | BindingFlags.Instance);
            var o = ofieldInfo.GetValue(dz);
            var method = o.GetType().GetMethod("a", BindingFlags.Public | BindingFlags.Instance, null, CallingConventions.Standard, new Type[] { typeof(CombatState), typeof(int), typeof(bool) }, null);
            return (bool)method.Invoke(o, new object[] { CombatState.Magic, 0, true });
        }

        private static object GetMetaHudControl(string windowName, string controlName, out string error) {
            //TODO: implement this fully in ub, needs vt meta views entirely replaced
            var bw = typeof(uTank2.PluginCore).Assembly.GetType("bw");
            var b = (System.Collections.IDictionary)bw.GetField("b", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            if (!b.Contains(windowName)) {
                error = $"Unable to find window named `{windowName}`";
                return null;
            }
            var metaWindow = b[windowName];
            var windowControls = (System.Collections.IDictionary)metaWindow.GetType().GetField("c", BindingFlags.Public | BindingFlags.Instance).GetValue(metaWindow);

            if (!windowControls.Contains(controlName)) {
                error = $"Unable to find control named `{controlName}`";
                return null;
            }
            var metaControl = windowControls[controlName];
            error = "";
            return metaControl.GetType().BaseType.GetField("a", BindingFlags.Public | BindingFlags.Instance).GetValue(metaControl);
        }

        public static bool UISetLabel(string windowName, string controlName, string newLabel, out string error) {
            var hudControl = GetMetaHudControl(windowName, controlName, out error);
            if (hudControl == null)
                return false;

            var textProp = hudControl.GetType().GetProperty("Text", BindingFlags.Public | BindingFlags.Instance);
            textProp.SetValue(hudControl, newLabel, null);

            error = "";
            return true;
        }

        public static bool UISetVisible(string windowName, string controlName, bool visible, out string error) {
            var hudControl = GetMetaHudControl(windowName, controlName, out error);
            if (hudControl == null)
                return false;

            var visibleProp = hudControl.GetType().GetProperty("Visible", BindingFlags.Public | BindingFlags.Instance);
            visibleProp.SetValue(hudControl, visible, null);

            error = "";
            return true;
        }

        public static bool UIMetaViewExists(string windowName) {
            //TODO: implement this fully in ub, needs vt meta views entirely replaced
            var bw = typeof(uTank2.PluginCore).Assembly.GetType("bw");
            var b = (System.Collections.IDictionary)bw.GetField("b", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            return b.Contains(windowName);
        }

        public static bool UIMetaViewIsVisible(string windowName) {
            //TODO: implement this fully in ub, needs vt meta views entirely replaced
            var bw = typeof(uTank2.PluginCore).Assembly.GetType("bw");
            var b = (System.Collections.IDictionary)bw.GetField("b", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            if (!b.Contains(windowName))
                return false;
            var metaWindow = b[windowName];
            var hudView = (HudView)metaWindow.GetType().GetField("a", BindingFlags.Public | BindingFlags.Instance).GetValue(metaWindow);
            return hudView.Visible;
        }

        public static bool UIControlExists(string windowName, string controlName, out string error) {
            var hudControl = GetMetaHudControl(windowName, controlName, out error);
            if (hudControl == null)
                return false;

            return true;
        }

        public static bool ChatboxPaste(string text) {
            Type ab = typeof(uTank2.PluginCore).Assembly.GetType("ab");
            Type br = typeof(uTank2.PluginCore).Assembly.GetType("br");
            MethodInfo a = ab.GetMethod("a", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(int), br, typeof(bool) }, null);
            FieldInfo dzFieldInfo = uTank2.PluginCore.PC.GetType().GetField("dz", BindingFlags.NonPublic | BindingFlags.Static);
            var dz = dzFieldInfo.GetValue(uTank2.PluginCore.PC);
            FieldInfo nfieldInfo = dz.GetType().GetField("n", BindingFlags.Public | BindingFlags.Instance);
            var n = nfieldInfo.GetValue(dz);
            FieldInfo bFieldInfo = n.GetType().GetField("b", BindingFlags.Public | BindingFlags.Instance);
            var b = bFieldInfo.GetValue(n);
            MethodInfo a2 = n.GetType().GetMethod("a", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { br, typeof(bool) }, null);
            MethodInfo a3 = ab.GetMethod("a", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(int), typeof(string) }, null);
            a.Invoke(null, new object[] { b, 13, false });
            a2.Invoke(n, new object[] { 13, true });
            a2.Invoke(n, new object[] { 13, false });
            a3.Invoke(null, new object[] { b, text });

            return true;
        }
        #endregion vtank internals
        #endregion


        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);

            UBHelper.Core.GameStateChanged -= Core_GameStateChanged;
        }
    }
}
