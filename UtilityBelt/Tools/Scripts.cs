using System;
using System.Collections.Generic;
using System.Linq;
using UtilityBelt.Service.Lib.Settings;
using UtilityBelt.Lib;
using System.Text.RegularExpressions;
using System.IO;
using UtilityBelt.Common.Enums;
using System.Collections.ObjectModel;
using UtilityBelt.Scripting.Interop;
using Skill = UtilityBelt.Scripting.Interop.Skill;
using UtilityBelt.Scripting.Events;
using ImGuiNET;
using UtilityBelt.Common.Messages.Events;
using System.Drawing;
using UtilityBelt.Service.Views;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using System.Text;
using Microsoft.DirectX.Direct3D;
using Newtonsoft.Json;
using System.Threading;
using System.Diagnostics;
using System.Data;
using MoonSharp.Interpreter;
using SyntaxErrorException = MoonSharp.Interpreter.SyntaxErrorException;
using Newtonsoft.Json.Serialization;
using System.Reflection;
using System.IO.Compression;
using WebSocketSharp;
using LogLevel = UtilityBelt.Scripting.Enums.LogLevel;
using UtilityBelt.Lib.ScriptInterface;
using System.Numerics;

namespace UtilityBelt.Tools {
    [Name("Scripts")]
    [Summary("TODO Scripts")]
    [FullDescription(@"TODO Scripts  ")]
    public class Scripts : ToolBase {
        private const uint MAX_LOG_LINE_SIZE = 1000;
        private class LogLine {
            public string Text { get; set; }
            public LogLevel Level { get; set; }
            public Vector4 Color { get; set; }
            public bool UseColor { get; set; } = false;
        }
        private List<LogLine> _scriptLogs = new List<LogLine>();
        #region Settings
        [Summary("List of scripts to auto load for this character. Does not take effect until client is restarted.")]
        public readonly Global<ObservableCollection<string>> AutoLoadScripts = new Global<ObservableCollection<string>>(new ObservableCollection<string>());
        #endregion //Settings

        #region Commands

        #region /ub lexec
        [Summary("Run lua code in the global script context.")]
        [Usage("/ub lexec <script>")]
        [Example("/ub lexec 1+1", "returns 2")]
        [CommandPattern("lexec", @"^ *(?<script>.*)$")]
        public void Lexec(string command, Match args) {
            try {
                var watch = new System.Diagnostics.Stopwatch();
                Logger.WriteToChat($"Evaluating script (context `Global`): \"{args.Groups["script"].Value}\"", Logger.LogMessageType.Expression, true, false);
                watch.Start();
                var res = UBLoader.FilterCore.Scripts.GlobalScriptContext.RunText(args.Groups["script"].Value);
                watch.Stop();
                Logger.WriteToChat($"Result: {PrettyPrint(res)} ({1000.0 * (double)watch.ElapsedTicks / Stopwatch.Frequency:N3}ms)");
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public static string PrettyPrint(object res) {
            if (res == null)
                return "null";

            return $"({res.GetType().Name}) {res}";
        }
        #endregion // /ub lexec

        #region /ub lexec
        [Summary("Run lua code in a specified script context.")]
        [Usage("/ub lexecs <name> <script>")]
        [Example("/ub lexecs test 1+1", "returns 2. Runs in the context of a loaded script called 'test'")]
        [CommandPattern("lexecs", @"^ *(?<name>\S+) +(?<script>.*)$")]
        public void Lexecs(string command, Match args) {
            
            try {
                var name = args.Groups["name"].Value;
                var script = UBLoader.FilterCore.Scripts.GetScript(name);
                if (script == null) {
                    var loadedScripts = string.Join(", ", UBLoader.FilterCore.Scripts.GetAll().Select(s => s.Name).ToArray());
                    Logger.Error($"Unable to find script named `{name}`. Loaded scripts: {loadedScripts}");
                    return;
                }

                var watch = new System.Diagnostics.Stopwatch();
                Logger.WriteToChat($"Evaluating script (context `{name}`): \"{args.Groups["script"].Value}\"", Logger.LogMessageType.Expression, true, false);
                watch.Start();
                var res = script.RunText(args.Groups["script"].Value);
                watch.Stop();
                Logger.WriteToChat($"Result: [{(res == null ? "null" : res.GetType().ToString())}] {res} ({Math.Round(watch.ElapsedTicks / 10000.0, 3)}ms)");
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        #endregion // /ub lexec

        #region /ub script
        [Summary("Control running ub scripts. Scripts are stored in `Global.ScriptStorageDirectory`. Script names/directories must not contain any spaces.")]
        [Usage("/ub script {start <scriptname> | stop <scriptname>}")]
        [Example("/ub script start myscript", "Starts running the script: `Global.ScriptStorageDirectory` + `/myscript/index.lua` ")]
        [Example("/ub script stop myscript", "Stops running the script: `Global.ScriptStorageDirectory` + `/myscript/index.lua` ")]
        [CommandPattern("script", @"^ *(?<command>start|stop|remote) +(?<scriptname>\S*)$")]
        public void ScriptCommand(string command, Match args) {
            var scriptname = args.Groups["scriptname"].Value;
            switch (args.Groups["command"].Value.ToLower()) {
                case "start":
                    var script = UBLoader.FilterCore.Scripts.StartScript(scriptname);
                    _scripts.Add(scriptname.ToLower());
                    break;
                case "stop":
                    _scripts.Remove(scriptname.ToLower());
                    UBLoader.FilterCore.Scripts.StopScript(scriptname);
                    break;
                case "remote":
                    var scriptId = scriptname.Trim();
                    Logger.WriteToChat($"Attempting to connect to remote script: {scriptId}");
                    _remoteScripts.Add(new RemoteScript(scriptId));
                    //});
                    break;
            }
        }
        #endregion // /ub script

        public IEnumerable<Spell> GetSelfBuffs(SkillId skill, TimeSpan? buffTimeLeft = null) {
            //return _selfBuffsBySkill[skill] ?? new List<Spell>();
            return null;
        }

        Dictionary<uint, int> allCharacterComponents = null;
        public bool HasComponents(Spell spell) {
            //if (allCharacterComponents == null)
                allCharacterComponents = UBLoader.FilterCore.Scripts.GameState.Character.Weenie.AllItems.Where(w => w.ObjectType == ObjectType.SpellComponents)
                            .GroupBy(w => w.WeenieClassId).ToDictionary(x => x.Key, x => x.Sum(w => w.Value(IntId.StackSize)));
            var components = spell.Components.GroupBy(c => c.ClassId).ToDictionary(c => c.Key, c => c.Count());

            foreach (var component in components) {
                if (!allCharacterComponents.TryGetValue(component.Key, out int amount) || amount < component.Value) {
                    return false;
                }
            }

            return true;
        }

        public IEnumerable<Spell> GetSelfBuffs() {
            var canCastSpell = (Spell s) => s.HasSkill(-40) && HasComponents(s);
            var flags = (SpellFlags.SelfTargeted | SpellFlags.Beneficial);
            var knownSpells = UBLoader.FilterCore.Scripts.GameState.Character.SpellBook.KnownSpellsIds.Select(spellId => UBLoader.FilterCore.Scripts.GameState.Character.SpellBook[spellId]);

            var _selfBuffsBySkill = UBLoader.FilterCore.Scripts.GameState.Character.Weenie.Skills.Values
                .Where(skill => skill.Training >= SkillTrainingType.Trained)
                .Select(skill => {
                    var values = knownSpells.Where(spell => spell.StatModSkill == skill.Type && (spell.Flags & flags) == flags)
                            .GroupBy(spell => spell.Category)
                            .Select(category => {
                                return category.OrderByDescending(spell => spell.Level);
                            });
                    return new KeyValuePair<SkillId, IEnumerable<IOrderedEnumerable<Spell>>>(skill.Type, values);
                });

            var _selfBuffsByAttribute = UBLoader.FilterCore.Scripts.GameState.Character.Weenie.Attributes.Keys
                .Select(attrId => {
                    var values = knownSpells.Where(spell => spell.StatModAttribute == attrId && (spell.Flags & flags) == flags)
                            .GroupBy(spell => spell.Category)
                            .Select(category => {
                                return category.OrderByDescending(spell => spell.Level);
                            });
                    return new KeyValuePair<AttributeId, IEnumerable<IOrderedEnumerable<Spell>>>(attrId, values);
                });

            var _selfBuffsByVital = UBLoader.FilterCore.Scripts.GameState.Character.Weenie.Vitals.Keys
                .Select(vitalId => {
                    var values = knownSpells.Where(spell => spell.StatModVital == (UtilityBelt.Common.Enums.Vital)vitalId && (spell.Flags & flags) == flags)
                            .GroupBy(spell => spell.Category)
                            .Select(category => {
                                return category.OrderByDescending(spell => spell.Level);
                            });
                    return new KeyValuePair<VitalId, IEnumerable<IOrderedEnumerable<Spell>>>(vitalId, values);

                });

            var _selfLifes = knownSpells.Where(spell => spell.School == MagicSchool.LifeMagic && spell.Duration > 0 && (spell.Flags & flags) == flags)
                            .GroupBy(spell => spell.Category)
                            .Select(category => {
                                return new KeyValuePair<SpellCategory, IOrderedEnumerable<Spell>>(category.Key, category.OrderByDescending(spell => spell.Power));
                            });

            var _selfItems = knownSpells.Where(spell => spell.School == MagicSchool.ItemEnchantment && spell.Duration > 0 && (spell.Flags & flags) == flags)
                            .GroupBy(spell => spell.Category)
                            .Select(category => {
                                return new KeyValuePair<SpellCategory, IOrderedEnumerable<Spell>>(category.Key, category.OrderByDescending(spell => spell.Power));
                            });

            var creatureMagicBuffs = _selfBuffsBySkill.FirstOrDefault(kv => kv.Key == SkillId.CreatureEnchantment).Value
                .Select(set => set.Where(canCastSpell).FirstOrDefault());

            var magicAttrBuffs = _selfBuffsByAttribute.Where(kv => kv.Key == AttributeId.Focus || kv.Key == AttributeId.Self)
                .Select(buffs => buffs.Value)
                .SelectMany(s => s)
                .Select(set => set.Where(canCastSpell).FirstOrDefault());

            var altMagicSkillBuffs = _selfBuffsBySkill.Where(kv => kv.Key == SkillId.LifeMagic || kv.Key == SkillId.ItemEnchantment || kv.Key == SkillId.ManaConversion)
                .Select(buffs => buffs.Value)
                .SelectMany(s => s)
                .Select(set => set.Where(canCastSpell).FirstOrDefault());

            var wandBuffs = new List<Spell>() {
                    _selfItems.FirstOrDefault(kv => kv.Key == SpellCategory.ManaConversionModRaising).Value
                        .Where(canCastSpell).FirstOrDefault()
                };

            var preBuffs = creatureMagicBuffs.Concat(magicAttrBuffs).Concat(altMagicSkillBuffs).Concat(wandBuffs);

            var lifeBuffs = _selfLifes.Select(set => set.Value.Where(canCastSpell).FirstOrDefault());
            var itemBuffs = _selfItems.Select(set => set.Value.Where(canCastSpell).FirstOrDefault());
            var skillBuffs = _selfBuffsBySkill.Select(skillKv => {
                return skillKv.Value.Select(q => q.Where(canCastSpell).FirstOrDefault());
            }).SelectMany(i => i);
            var attrBuffs = _selfBuffsByAttribute.Select(attrKv => {
                return attrKv.Value.Select(q => q.Where(canCastSpell).FirstOrDefault());
            }).SelectMany(i => i);

            Logger.WriteToChat($"{string.Join(", ", _selfBuffsByAttribute.Select(k => k.Key))}");
            Logger.WriteToChat($"{string.Join(", ", attrBuffs.Select(k => k))}");

            return preBuffs.Concat(attrBuffs).Concat(itemBuffs).Concat(skillBuffs).Concat(lifeBuffs)
                .Where(b => b != null).Distinct();
                //.Where(b => b != null && !UBLoader.FilterCore.Scripts.GameState.Character.ActiveEnchantments.Any(e => e.SpellId == b.Id)).Distinct();
        }


        public class NonPublicPropertiesResolver : DefaultContractResolver {
            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization) {
                var prop = base.CreateProperty(member, memberSerialization);
                if (member is PropertyInfo pi) {
                    prop.Readable = (pi.GetMethod != null);
                    prop.Writable = (pi.SetMethod != null);
                }
                return prop;
            }
        }
        public static void save(Stream source, Stream destination) {
            byte[] bytes = new byte[4096];

            int count;

            while ((count = source.Read(bytes, 0, bytes.Length)) != 0) {
                destination.Write(bytes, 0, count);
            }
        }

        public static byte[] Compress(string str) {
            var bytes = Encoding.UTF8.GetBytes(str);

            using (var msi = new MemoryStream(bytes))
            using (var memoryStream = new MemoryStream()) {
                using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Compress)) {
                    save(msi, gZipStream);
                }

                return memoryStream.ToArray();
            }
        }

        #region /ub remote
        [Summary("remote")]
        [Usage("/ub remote")]
        [Example("/ub remote <script id>", "Connects to remote <script id> running in a browser instance somewhere")]
        [CommandPattern("remote", @"^(?<scriptId>.*)$")]
        public void RunRemoteScript(string command, Match args) {
            var scriptId = args.Value.Trim();
            Logger.WriteToChat($"Attempting to connect to remote script: {scriptId}");
            var exitEvent = new ManualResetEvent(false);
            //Task.Run(() => {
                try {
                using (var ws = new WebSocket("ws://localhost:8181/acclient-server")) {
                    ws.OnMessage += (sender, e) =>
                        Console.WriteLine("Laputa says: " + e.Data);

                    ws.Connect();
                    ws.Send($"{{ \"command\": \"init\", \"clientId\": \"${scriptId}\" }}");
                    Console.ReadKey(true);
                }
            }
                catch (Exception ex) { Logger.LogException(ex); }
            //});
        }
        #endregion // /ub remote

        #region /ub asdf
        [Summary("asdf")]
        [Usage("/ub asdf")]
        [Example("/ub asdf", "asdf")]
        [CommandPattern("asdf", @"^ *$")] 
        public void asdf(string command, Match args) {
            var t = new System.Diagnostics.Stopwatch();
            t.Start();
            var settings = new JsonSerializerSettings() {
                TypeNameHandling = TypeNameHandling.Objects,
                TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
                ContractResolver = new NonPublicPropertiesResolver()
            };

            UBLoader.FilterCore.PortalDat.FileCache.Remove(234881026); // chargen
            UBLoader.FilterCore.PortalDat.FileCache.Remove(150994945); // motions
            
            UBLoader.FilterCore.PortalDat.SpellTable.SpellSet.Clear();

            var keys = UBLoader.FilterCore.PortalDat.SpellTable.Spells.Keys.ToArray();
            foreach (var key in keys) {
                if (!UBLoader.FilterCore.Scripts.GameState.Character.SpellBook.IsKnown(key)) {
                    UBLoader.FilterCore.PortalDat.SpellTable.Spells.Remove(key);
                }
            }

            var s = JsonConvert.SerializeObject(UBLoader.FilterCore.PortalDat.FileCache, Formatting.Indented, settings);
            var s2 = new StringBuilder();

            foreach (var p in s.Split('\n')) {
                if (p.Trim(' ').StartsWith("\"$type\": \"System.Collections.Generic"))
                    continue;
                s2.Append(p);
            }

            File.WriteAllBytes(@"C:\games\portaldat.json.gz", Compress(s2.ToString()));
            File.WriteAllText(@"C:\games\portaldat.json", s2.ToString());

            var cellDatCache = JsonConvert.SerializeObject(UBLoader.FilterCore.CellDat.FileCache, Formatting.Indented, settings);
            var cellDatCacheStr = new StringBuilder();

            foreach (var p in cellDatCache.Split('\n')) {
                if (p.Trim(' ').StartsWith("\"$type\": \"System.Collections.Generic"))
                    continue;
                cellDatCacheStr.Append(p);
            }

            File.WriteAllBytes(@"C:\games\celldat.json.gz", Compress(cellDatCacheStr.ToString()));
            File.WriteAllText(@"C:\games\celldat.json", cellDatCacheStr.ToString());


            var languageDatCache = JsonConvert.SerializeObject(UBLoader.FilterCore.LanguageDat.FileCache, Formatting.Indented, settings);
            var languageDatCacheStr = new StringBuilder();

            foreach (var p in languageDatCache.Split('\n')) {
                if (p.Trim(' ').StartsWith("\"$type\": \"System.Collections.Generic"))
                    continue;
                languageDatCacheStr.Append(p);
            }

            File.WriteAllBytes(@"C:\games\languagedat.json.gz", Compress(languageDatCacheStr.ToString()));
            File.WriteAllText(@"C:\games\languagedat.json", languageDatCacheStr.ToString());


            string json = File.ReadAllText(@"C:\games\portaldat.json");
            var cache = JsonConvert.DeserializeObject<Dictionary<uint, ACE.DatLoader.FileTypes.FileType>>(json, settings);
            var PortalDat = new PortalDatDatabase(cache);
            Logger.WriteToChat($"Spells 1: {PortalDat.SpellTable.Spells[3].Name}");
            Logger.WriteToChat($"Spells 2: {PortalDat.ReadFromDat<SpellTable>(0x0E00000E).Spells[3].Name}");
            Logger.WriteToChat($"Spells 3: {UBLoader.FilterCore.PortalDat.ReadFromDat<SpellTable>(0x0E00000E).Spells[3].Name}");

            Logger.WriteToChat($"Message Length: {UBLoader.FilterCore._messageDatas.Count}");
            var s3 = JsonConvert.SerializeObject(UBLoader.FilterCore._messageDatas, new JsonSerializerSettings() {
            });

            File.WriteAllBytes(@"C:\games\gamestate.json.gz", Compress(s3.ToString()));
            File.WriteAllText(@"C:\games\gamestate.json", s3);
            /*
            var skillKeys = UBLoader.FilterCore.Scripts.GameState.Character.Weenie.Skills.Values.Where(s => s.Training >= SkillTrainingType.Trained).Select(s => s.Type).ToList();
            foreach (var skey in skillKeys) {
                var skillBuffs = GetBuffs(skey);
                str.AppendLine($"{skey} = {string.Join(", ", skillBuffs.Select(s => s?.Name).ToArray())}");
            }
            */
            t.Stop();
            Logger.WriteToChat($"Result: ({UBLoader.FilterCore.Scripts.GameState.Character.SpellBook.KnownSpellsIds.Count()} spells) ({Math.Round(t.ElapsedTicks / 10000.0, 3)}ms)");

            return;

            var watch = new System.Diagnostics.Stopwatch();
            //Logger.WriteToChat($"Evaluating expression: \"{expression}\"", Logger.LogMessageType.Expression, true, false);
            watch.Start();

            var gameState = UBLoader.FilterCore.Scripts.GameState;
            var physEnv = gameState.WorldState.PhysicsEnvironment;
            Logger.WriteToChat($"Current Cell: {physEnv.CurrentLandcell:X8} Visible Cells: {string.Join(", ", physEnv.VisibleLandcells.Select(c => $"{c:X8}").ToArray())}");

            //Logger.WriteToChat($"GameState Hash: {gameState.GetHashCode()}");
            //Logger.WriteToChat($"Vitae is: {gameState.CharacterState.Vitae}%");

            //Logger.WriteToChat($"Equipment Ids: {string.Join(", ", gameState.CharacterState.Weenie.EquipmentIds.Select(id => $"0x{id:X8}").ToArray())}");
            /*
            foreach (var equipmentId in gameState.CharacterState.Weenie.EquipmentIds) {
                if (gameState.WorldState.Weenies.TryGetValue(equipmentId, out UBScript.Lib.Weenie equipmentWeenie)) {
                    //Logger.WriteToChat($"  Found Equipment: 0x{equipmentId:X8} {equipmentWeenie.Name} // {(EquipMask)equipmentWeenie.Value(IntPropertyID.CurrentWieldedLocation)}");
                }
                else {
                    //Logger.WriteToChat($"  Couldn't Find Equipment Weenie: 0x{equipmentId:X8}");
                }
            }

            //Logger.WriteToChat($"Inventory Ids: {string.Join(", ", gameState.CharacterState.Weenie.ItemIds.Select(id => $"0x{id:X8}").ToArray())}");

            foreach (var itemId in gameState.CharacterState.Weenie.ItemIds) {
                if (gameState.WorldState.Weenies.TryGetValue(itemId, out UBScript.Lib.Weenie itemWeenie)) {
                    //Logger.WriteToChat($"  Found Item: 0x{itemWeenie.Id:X8} {itemWeenie.Name} // {itemWeenie.ObjectType}");
                }
                else {
                    //Logger.WriteToChat($"  Couldn't Find Item Weenie: 0x{itemId:X8}");
                }
            }

            //Logger.WriteToChat($"Inventory Container Ids: {string.Join(", ", gameState.CharacterState.Weenie.ContainerIds.Select(id => $"0x{id:X8}").ToArray())}");

            foreach (var itemId in gameState.CharacterState.Weenie.ContainerIds) {
                if (gameState.WorldState.Weenies.TryGetValue(itemId, out UBScript.Lib.Weenie containerWeenie)) {
                    //Logger.WriteToChat($"  Found Container: 0x{containerWeenie.Id:X8}  {containerWeenie.Name} // {containerWeenie.ContainerProperties}");
                    foreach (var childId in containerWeenie.ItemIds) {
                        if (gameState.WorldState.Weenies.TryGetValue(childId, out UBScript.Lib.Weenie childWeenie)) {
                            //Logger.WriteToChat($"    Found Item: 0x{childWeenie.Id:X8} {childWeenie.Name} // {childWeenie.ObjectType}");
                        }
                        else {
                            //Logger.WriteToChat($"    Couldn't Find Item Weenie: 0x{childId:X8}");
                        }
                    }
                }
                else {
                    //Logger.WriteToChat($"  Couldn't Find Item Weenie: 0x{itemId:X8}");
                }
            }
            */
            var testSpell = gameState.Character.SpellBook[1161];

            if (testSpell != null) {
                //Logger.WriteToChat($"Old Spell components: {string.Join(", ", testSpell.OldSchoolComponents.Select(c => c.Name).ToArray())}");
               // Logger.WriteToChat($"Foci Spell components: {string.Join(", ", testSpell.FociComponents.Select(c => c.Name).ToArray())}");
                //Logger.WriteToChat($"Using Spell components: {string.Join(", ", testSpell.Components.Select(c => c.Name).ToArray())}");

                //Logger.WriteToChat($"IsKnown: {testSpell.IsKnown()}");
                //Logger.WriteToChat($"Level: {testSpell.Level}");

                var missingComponentsList = new List<SpellComponent>();
                var allCharacterComponents = gameState.Character.Weenie.AllItems.Where(w => w.ObjectType == ObjectType.SpellComponents);

               // Logger.WriteToChat($"My Components: {string.Join(", ", allCharacterComponents.Select(w => $"{w.Name}({w.ClassId}): {w.Value(IntPropertyID.StackSize)}").ToArray())}");
                var hasComponents = testSpell.HasComponents(out IEnumerable<SpellComponent> missingComponents);
                //Logger.WriteToChat($"HasComponents: {hasComponents} // missing: {string.Join(", ", missingComponents.Select(c => c.Name).ToArray())}");

            }
            else {
                //Logger.WriteToChat($"spell was null");
            }


            foreach (var enchantment in gameState.Character.AllEnchantments) {
                var enchantmentSpell = gameState.Character.SpellBook[enchantment.SpellId];
                var statName = "";
                if ((enchantment.Flags & EnchantmentFlags.Skill) != 0) {
                    statName = ((SkillId)enchantment.StatKey).ToString();
                }
                else if ((enchantment.Flags & EnchantmentFlags.Attribute) != 0) {
                    statName = ((AttributeId)enchantment.StatKey).ToString();
                }
                else if ((enchantment.Flags & EnchantmentFlags.Attribute2nd) != 0) {
                    statName = ((VitalId)enchantment.StatKey).ToString();
                }


                //Logger.WriteToChat($"Found enchantment: {(enchantmentSpell == null ? "nullspell" : enchantmentSpell.Name)} // id {enchantment.SpellId} // Layer: {enchantment.SpellLayer} // Category: {enchantment.Category} // Power: {enchantment.Power} // Stat: {statName} // Value: {enchantment.StatValue} // Expires: {enchantment.ExpiresAt}");
            }

            //Logger.WriteToChat($"GameState weenie count: {gameState.WorldState.Weenies.Count}");

            //Logger.WriteToChat($"Character {gameState.CharacterState.Weenie.IntId} {gameState.CharacterState.Weenie.Id:X8} {gameState.CharacterState.Weenie.Name}: {gameState.CharacterState.Weenie.Skills.Count} {gameState.CharacterState.Weenie.Attributes.Count} {gameState.CharacterState.Weenie.Vitals.Count}");

            var attributeIds = Enum.GetValues(typeof(AttributeId)).OfType<AttributeId>().ToList();

            //attributeIds.Sort((a, b) => a.ToString().CompareTo(b.ToString()));

            foreach (AttributeId attributeId in attributeIds) {
                //Logger.WriteToChat($"Attribute: {attributeId}");
                var attributeEnchantments = gameState.Character.GetActiveEnchantments(attributeId);
                var enchantmentBonus = attributeEnchantments.Sum(enchantment => enchantment.StatValue);

                if (gameState.Character.Weenie.Attributes.TryGetValue(attributeId, out UtilityBelt.Scripting.Interop.Attribute ubAttr)) {
                    //Logger.WriteToChat($"   UB: {attributeId} Base: {ubAttr.Base} // Current: {ubAttr.Current}");
                    var multEnchants = gameState.Character.GetActiveEnchantments(attributeId).Where(e => (e.Flags & EnchantmentFlags.Multiplicative) != 0);
                    var addEnchants = gameState.Character.GetActiveEnchantments(attributeId).Where(e => (e.Flags & EnchantmentFlags.Additive) != 0);
                    foreach (var enchantment in multEnchants) {
                        var spell = UB.PortalDat.SpellTable.Spells[enchantment.SpellId];
                        //Logger.WriteToChat($"     - Mult {spell.Name} // Flags: {enchantment.Flags} // {enchantment.StatValue}");
                    }
                    foreach (var enchantment in addEnchants) {
                        var spell = UB.PortalDat.SpellTable.Spells[enchantment.SpellId];
                        //Logger.WriteToChat($"     - Add {spell.Name} // Flags: {enchantment.Flags} // {enchantment.StatValue}");
                    }
                }
                else {
                    //Logger.WriteToChat($"   UB: NA"); 
                }

                //var deAttr = UB.Core.CharacterFilter.Attributes[(Decal.Adapter.Wrappers.CharFilterAttributeType)attributeId];
                //var deEffective = UB.Core.CharacterFilter.EffectiveAttribute[(Decal.Adapter.Wrappers.CharFilterAttributeType)attributeId];

                //Logger.WriteToChat($"   DE: Base: {deAttr.Base} // Effective: {deEffective}");
            }

            var xpPercentageBonus = UB.Core.CharacterFilter.Enchantments.Where(e => e.Family == 615).OrderByDescending(e => e.Layer).Select(e => e.Adjustment).FirstOrDefault();
            xpPercentageBonus = Math.Max(xpPercentageBonus, 1f);
            xpPercentageBonus += UB.Core.WorldFilter[UB.Core.CharacterFilter.Id].Values((Decal.Adapter.Wrappers.LongValueKey)234) * 0.05f; // AugmentationBonusXP

            var vitalIds = Enum.GetValues(typeof(VitalId)).OfType<VitalId>().ToList();

            //attributeIds.Sort((a, b) => a.ToString().CompareTo(b.ToString()));

            foreach (VitalId vitalId in vitalIds) {
                if (gameState.Character.Weenie.Vitals.TryGetValue(vitalId, out UtilityBelt.Scripting.Interop.Vital ubAttr)) {
                    //Logger.WriteToChat($"   UB: {ubAttr.Type} Base: {ubAttr.Base} // Current: {ubAttr.Current} // Max: {ubAttr.Max}");
                    var multEnchants = gameState.Character.GetActiveEnchantments(vitalId).Where(e => (e.Flags & EnchantmentFlags.Multiplicative) != 0);
                    var addEnchants = gameState.Character.GetActiveEnchantments(vitalId).Where(e => (e.Flags & EnchantmentFlags.Additive) != 0);
                    foreach (var enchantment in multEnchants) {
                        var spell = UB.PortalDat.SpellTable.Spells[enchantment.SpellId];
                        //Logger.WriteToChat($"     - Mult {spell.Name} // Flags: {enchantment.Flags} // {enchantment.StatValue}");
                    }
                    foreach (var enchantment in addEnchants) {
                        var spell = UB.PortalDat.SpellTable.Spells[enchantment.SpellId];
                        //Logger.WriteToChat($"     - Add {spell.Name} // Flags: {enchantment.Flags} // {enchantment.StatValue}");
                    }
                }

                //var deAttr = UB.Core.CharacterFilter.Attributes[(Decal.Adapter.Wrappers.CharFilterAttributeType)attributeId];
                //var deEffective = UB.Core.CharacterFilter.EffectiveAttribute[(Decal.Adapter.Wrappers.CharFilterAttributeType)attributeId];

                //Logger.WriteToChat($"   DE: Base: {deAttr.Base} // Effective: {deEffective}");
            }

            //*
            var skillIds = Enum.GetValues(typeof(SkillId)).OfType<SkillId>().ToList();

            //skillIds.Sort((a, b) => a.ToString().CompareTo(b.ToString()));
            foreach (SkillId skillId in skillIds) {
                Skill ubSkill = null;

                string ubSkillText = "";
                string deSkillText = "";
                bool hasUBSkill = false;
                bool hasDESkill = false;

                if (!gameState.Character.Weenie.Skills.TryGetValue(skillId, out ubSkill)) {
                    //ubSkillText = $"   UB: No skill ({skillId})";
                }
                else {
                    hasUBSkill = true;

                    //ubSkillText = $"   UB: {skillId} Base: {ubSkill.Base} Effective: {ubSkill.Effective} Points:{ubSkill.InitLevel + ubSkill.PointsRaised} Training: {ubSkill.Training}";
                }

                if (hasUBSkill || hasDESkill) {
                    //Logger.WriteToChat($"Check Skill {skillId}:");
                    //Logger.WriteToChat(ubSkillText);
                    //Logger.WriteToChat(deSkillText);
                }
            }

            //Logger.WriteToChat($"Vitae: {gameState.CharacterState.Vitae}");
            //*/

            watch.Stop();
            Logger.WriteToChat($"Result: ({Math.Round(watch.ElapsedTicks / 10000.0, 3)}ms)", Logger.LogMessageType.Expression);

            //try {  
            /*
                Logger.WriteToChat($"Found spell id asdf: {0x00000006}");
                
                var components = ACE.DatLoader.FileTypes.SpellTable.GetSpellFormula(UB.PortalDat.SpellTable, 4311, UB.Core.CharacterFilter.AccountName);

                Logger.WriteToChat($"Found {components.Count} components");

                foreach (var componentId in components) {
                    var component = UB.PortalDat.SpellComponentsTable.SpellComponents[componentId];
                    Logger.WriteToChat($"Needs component: 9x{componentId:X8} // {component.Name} // {component.Type} // {component.Time}");
                }
              */
            //}
            //catch (Exception ex) { Logger.LogException(ex); }
        }
        #endregion // /ub asdf

        #region /ub asdf2
        [Summary("asdf2")]
        [Usage("/ub asdf2")]
        [Example("/ub asdf2", "asdf")]
        [CommandPattern("asdf2", @"^ *$")]
        public unsafe void asdf2(string command, Match args) {
            //AcClient.CM_Allegiance.Event_BreakAllegiance(0x50006885);
            //AcClient.CM_Allegiance.Event_SwearAllegiance(0x50006885);

            //for (uint i = 0; i < 10000; i++) {
            //    AcClient.CM_UI.SendNotice_SetPanelVisibility(i, 0);
            //}

            //for (var i = 0; i < 5; i++) {
            //AcClient.CM_Combat.Event_TargetedMeleeAttack((uint)UB.Core.Actions.CurrentSelection, AcClient.ATTACK_HEIGHT.HIGH_ATTACK_HEIGHT, 1);
            AcClient.CM_Combat.Event_TargetedMissileAttack((uint)UB.Core.Actions.CurrentSelection, AcClient.ATTACK_HEIGHT.LOW_ATTACK_HEIGHT, 1);
            //AcClient.CM_Inventory.Event_GetAndWieldItem((uint)UB.Core.Actions.CurrentSelection, (uint)EquipMask.Shield);
            //}

            /*
            var str = new StringBuilder();
            foreach (var spell in _castBuffs) {
                str.AppendLine($"game.actions.castSpell({spell.SpellId}, {spell.TargetId})");
            }
            Logger.WriteToChat(str.ToString());
            _castBuffs.Clear();
            */
        }
        #endregion // /ub asdf2

        #endregion //Commands 

        private PhysicsEnvironment _physEnv;
        private List<string> _scripts = new List<string>();
        private Hud hud;
        private string selectedScript = "";

        public Scripts(UtilityBeltPlugin ub, string name) : base(ub, name) {
            
        }


        public override void Init() {
            base.Init();

            if (!UBLoader.FilterCore.Global.EnableScripts)
                return;

            using (Stream manifestResourceStream = GetType().Assembly.GetManifestResourceStream("UtilityBelt.Resources.icons.scripts-manager.png")) {
                hud = UtilityBelt.Service.UBService.Huds.CreateHud("UtilityBelt Scripts Manager", new Bitmap(manifestResourceStream));
            }

            hud.ShowInBar = true;
            hud.OnRender += Hud_Render;
            hud.OnPreRender += Hud_PreRender;
            hud.Visible = false;

            UB.Core.WorldFilter.ReleaseObject += WorldFilter_ReleaseObject;

            UBLoader.FilterCore.Scripts.GameState.Character.Weenie.OnPositionChanged += Weenie_OnPositionChanged;

            //_physEnv = new PhysicsEnvironment(UBLoader.FilterCore.Scripts);

            LoadScripts();

            UBLoader.FilterCore.Scripts.GameState.Character.Weenie.ItemIds.CollectionChanged += ItemIds_CollectionChanged;

            UBLoader.FilterCore.Scripts.MessageHandler.Outgoing.Magic_CastTargetedSpell += Outgoing_Magic_CastTargetedSpell;
            UBLoader.FilterCore.Scripts.MessageHandler.Outgoing.Magic_CastUntargetedSpell += Outgoing_Magic_CastUntargetedSpell;
            UBLoader.FilterCore.Scripts.MessageHandler.Incoming.Communication_TextboxString += Incoming_Communication_TextboxString;
        }


        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        private void Incoming_Communication_TextboxString(object sender, Communication_TextboxString_S2C_EventArgs e) {

            if (e.Data.Text.StartsWith("You cast Adja's Blessing on yourself,")) {
                Logger.WriteToChat($"Buff timer started");
                stopwatch = new System.Diagnostics.Stopwatch();
                stopwatch.Start();
            }

            if (e.Data.Text.StartsWith("You cast Honed Control on yourself")) {
                stopwatch.Stop();
                Logger.WriteToChat($"Buffing took: ({Math.Round(stopwatch.ElapsedTicks / 10000.0, 3)}ms)");
            }
        }

        private struct SpellCast {
            public uint SpellId;
            public uint TargetId;

            public SpellCast(uint spellId, uint targetId) {
                SpellId = spellId;
                TargetId = targetId;
            }
        }

        private List<SpellCast> _castBuffs = new List<SpellCast>();
        private UtilityBelt.Scripting.UBScript currentScriptInstance;
        private string _consoleInput = "";
        private List<RemoteScript> _remoteScripts = new List<RemoteScript>();

        public bool ScrollToBottom { get; private set; }

        private void Outgoing_Magic_CastUntargetedSpell(object sender, Magic_CastUntargetedSpell_C2S_EventArgs e) {
            if (!_castBuffs.Any(c => c.SpellId == e.Data.SpellId.FullId && c.TargetId == 0))
                _castBuffs.Add(new SpellCast(e.Data.SpellId.FullId, 0));
        }

        private void Outgoing_Magic_CastTargetedSpell(object sender, Magic_CastTargetedSpell_C2S_EventArgs e) {
            if (!_castBuffs.Any(c => c.SpellId == e.Data.SpellId.FullId && c.TargetId == e.Data.ObjectId))
                _castBuffs.Add(new SpellCast(e.Data.SpellId.FullId, e.Data.ObjectId));
        }

        private void ItemIds_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
            allCharacterComponents = null;
        }

        private void Core_GameStateChanged(UBHelper.GameState previous, UBHelper.GameState new_state) {
        }

        private void Hud_PreRender(object sender, EventArgs e) {
            ImGui.SetNextWindowSizeConstraints(new Vector2(500, 250), new Vector2(float.MaxValue, float.MaxValue));
        }

        private void Hud_Render(object sender, EventArgs e) {
            if (UBHelper.Core.GameState != UBHelper.GameState.In_Game)
                return;
            var pad = 15;
            ImGui.BeginTable("ScriptsTable", 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.NoSavedSettings);
            {
                ImGui.TableSetupColumn("ObjectTree", ImGuiTableColumnFlags.WidthFixed, ImGui.GetContentRegionAvail().X * 0.4f);
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.BeginChild("Object Tree", new Vector2(-1, ImGui.GetContentRegionAvail().Y - 4));
                {
                    var listedScripts = new List<string>();
                    var runningScripts = UBLoader.FilterCore.Scripts.GetAll();

                    if (ImGui.TreeNodeEx("Running Scripts", runningScripts.Count() > 0 ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.Leaf)) {
                        foreach (var script in runningScripts) {
                            var isGlobalAutoLoad = UBLoader.FilterCore.Global.AutoLoadScripts.Value.Contains(script.Name);
                            var isAccountAutoLoad = UBLoader.FilterCore.Account.AutoLoadScripts.Value.Contains(script.Name);
                            var isCharacterAutoLoad = AutoLoadScripts.Value.Contains(script.Name);

                            var flags = ImGuiTreeNodeFlags.Leaf;
                            if (selectedScript.ToLower().Equals(script.Name.ToLower())) {
                                flags |= ImGuiTreeNodeFlags.Selected;
                            }

                            ImGui.TreeNodeEx(script.Name, flags);
                            if (ImGui.IsItemClicked()) {
                                selectedScript = script.Name;
                                SelectScript(script.Name);
                            }
                            ImGui.TreePop();
                            listedScripts.Add(script.Name.ToLower());
                        }
                        ImGui.TreePop(); // Running Scripts
                    }

                    var availableScripts = UBLoader.FilterCore.Scripts.GetAvailable().Where(s => !listedScripts.Contains(s.ToLower()));
                    if (ImGui.TreeNodeEx("Available Scripts", availableScripts.Count() > 0 ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.Leaf)) {
                        foreach (var script in availableScripts) {
                            if (listedScripts.Contains(script))
                                continue;

                            var isGlobalAutoLoad = UBLoader.FilterCore.Global.AutoLoadScripts.Value.Contains(script);
                            var isAccountAutoLoad = UBLoader.FilterCore.Account.AutoLoadScripts.Value.Contains(script);
                            var isCharacterAutoLoad = AutoLoadScripts.Value.Contains(script);
                            var flags = ImGuiTreeNodeFlags.Leaf;
                            if (selectedScript.ToLower().Equals(script.ToLower())) {
                                flags |= ImGuiTreeNodeFlags.Selected;
                            }
                            ImGui.TreeNodeEx(script, flags);
                            if (ImGui.IsItemClicked()) {
                                selectedScript = script;
                                SelectScript(script);
                            }
                            ImGui.TreePop();

                            listedScripts.Add(script.ToLower());
                        }
                        ImGui.TreePop(); // Available Scripts
                    }
                }
                ImGui.EndChild(); // object tree

                ImGui.Indent(10);
                ImGui.Unindent(-10);

                ImGui.TableSetColumnIndex(1);
                ImGui.BeginChild("Object Info", new Vector2(-1, ImGui.GetContentRegionAvail().Y - 4));
                {
                    if (string.IsNullOrEmpty(selectedScript)) {
                        ImGui.TextWrapped("Select a script using the menu to the left.");
                    }
                    else {
                        ImGui.Text($"Script: {selectedScript}");

                        var isRunning = UBLoader.FilterCore.Scripts.GetAll().Any(s => s.Name.ToLower().Equals(selectedScript.ToLower()));

                        if (isRunning) {
                            ImGui.SameLine();
                            if (ImGui.Button("Restart")) {
                                UBLoader.FilterCore.Scripts.RestartScript(selectedScript);
                            }
                            ImGui.SameLine(0, 10);
                            if (ImGui.Button("Stop")) {
                                UBLoader.FilterCore.Scripts.StopScript(selectedScript);
                            }
                            ImGui.SameLine(0, 10);
                            if (ImGui.Button("Clear Console")) {
                                _scriptLogs.Clear();
                            }

                            // Reserve enough left-over height for 1 separator + 1 input text
                            float footer_height_to_reserve = ImGui.GetStyle().ItemSpacing.Y + ImGui.GetFrameHeightWithSpacing();
                            ImGui.BeginChild("ScrollingRegion", new Vector2(0, -footer_height_to_reserve), false, ImGuiWindowFlags.HorizontalScrollbar);

                            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4, 1)); // Tighten spacing

                            foreach (var line in _scriptLogs) {
                                Vector4 color = new Vector4(1, 1, 1, 1);
                                if (line.UseColor) {
                                    color = line.Color;
                                }
                                else {
                                    switch (line.Level) {
                                        case LogLevel.Error:
                                            color = new Vector4(0.8f, 0f, 0f, 1);
                                            break;
                                        case LogLevel.Warning:
                                            color = new Vector4(0.8f, 0.3f, 0f, 1);
                                            break;
                                        case LogLevel.Info:
                                            color = new Vector4(1f, 1f, 1f, 1);
                                            break;
                                        case LogLevel.Verbose:
                                            color = new Vector4(0.8f, 0.8f, 0.8f, 1);
                                            break;
                                    }
                                }
                                ImGui.PushStyleColor(ImGuiCol.Text, color);
                                ImGui.TextUnformatted(line.Text);
                                ImGui.PopStyleColor();
                            }

                            if (ScrollToBottom)
                                ImGui.SetScrollHereY(1.0f);

                            ImGui.PopStyleVar();
                            ImGui.EndChild();
                            ImGui.Separator();

                            // Command-line
                            bool reclaim_focus = false;
                            ImGuiInputTextFlags input_text_flags = ImGuiInputTextFlags.EnterReturnsTrue;

                            if (ImGui.InputText("Input", ref _consoleInput, 512, input_text_flags)) {
                                AddLogLine($"> {_consoleInput}", new Vector4(0, 1, 0, 1));
                                RunInput(_consoleInput);
                                _consoleInput = "";
                                reclaim_focus = true;
                            }

                            // Auto-focus on window apparition
                            ImGui.SetItemDefaultFocus();
                            if (reclaim_focus)
                                ImGui.SetKeyboardFocusHere(-1); // Auto focus previous widget
                        }
                        else {
                            if (ImGui.Button("Start")) {
                                UBLoader.FilterCore.Scripts.StartScript(selectedScript);
                                SelectScript(selectedScript);
                            }
                        }
                    }
                }
                ImGui.EndChild(); // Object Info
            }
            ImGui.EndTable();
        }

        private void RunInput(string consoleInput) {
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            object res = null;
            try {
                res = currentScriptInstance?.RunTextNoCatch(_consoleInput);
            }
            catch (ScriptRuntimeException ex) {
                AddLogLine($"An error occured! {ex.DecoratedMessage}", LogLevel.Error);
            }
            catch (SyntaxErrorException ex) {
                AddLogLine($"A syntax error occured! {ex.DecoratedMessage}", LogLevel.Error);
            }
            catch (Exception ex) {
                AddLogLine($"Error running script: ", LogLevel.Error);
                AddLogLine(ex.ToString(), LogLevel.Error);
            }
            watch.Stop();
            AddLogLine($"{PrettyPrint(res)} ({Math.Round(watch.ElapsedTicks / 10000.0, 3)}ms)", new Vector4(1, 0, 1, 1));
        }

        private void SelectScript(string script) {
            
            selectedScript = script;

            _scriptLogs.Clear();
            if (currentScriptInstance != null) {
                currentScriptInstance.OnLogText -= CurrentScriptInstance_OnLogText;
                currentScriptInstance = null;
            }

            var scriptInstance = UBLoader.FilterCore.Scripts.GetScript(script);
            if (scriptInstance != null) {
                currentScriptInstance = scriptInstance;
                currentScriptInstance.OnLogText += CurrentScriptInstance_OnLogText;
            }
        }

        private void CurrentScriptInstance_OnLogText(object sender, UtilityBelt.Scripting.UBScript.LogEventArgs e) {
            AddLogLine(e.Text, LogLevel.Info);
        }

        private void AddLogLine(string text, Vector4 color) {
            AddLogLine(new LogLine() {
                Text = text,
                Color = color,
                UseColor = true
            });
        }

        private void AddLogLine(string text, LogLevel level) {
            AddLogLine(new LogLine() {
                Text = text,
                Level = level
            });
        }

        private void AddLogLine(LogLine logLine) {
            _scriptLogs.Add(logLine);
            if (_scriptLogs.Count > MAX_LOG_LINE_SIZE)
                _scriptLogs.RemoveAt(0);

            ScrollToBottom = true;
        }

        private void Weenie_OnPositionChanged(object sender, ServerPositionChangedEventArgs e) {
            try {
                //_physEnv.SetLocation(e.Position);
            } catch (Exception ex) { Logger.LogException(ex); }
        }

        private void WorldFilter_ReleaseObject(object sender, Decal.Adapter.Wrappers.ReleaseObjectEventArgs e) {
            try {
                //Logger.WriteToChat($"WorldFilter_ReleaseObject: 0x{e.Released.Id:X8} {e.Released.Name}");
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void LoadScripts() {
            foreach (var script in AutoLoadScripts.Value) {
                try {
                    var scriptInstance = UBLoader.FilterCore.Scripts.StartScript(script);
                    _scripts.Add(script.ToLower());
                }
                catch (Exception ex) { Logger.LogException(ex); }
            }
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);

            hud.OnRender -= Hud_Render;
            hud.OnPreRender -= Hud_PreRender;
            hud.Dispose();

            if (currentScriptInstance != null) {
                currentScriptInstance.OnLogText -= CurrentScriptInstance_OnLogText;
            }

            foreach (var script in _scripts) {
                UBLoader.FilterCore.Scripts.StopScript(script);
            }
            _scripts.Clear();

            foreach (var remoteScript in _remoteScripts) {
                remoteScript.Dispose();
            }
            _remoteScripts.Clear();

            if (UBLoader.FilterCore.Scripts?.GameState?.Character?.Weenie != null) {
                UBLoader.FilterCore.Scripts.GameState.Character.Weenie.OnPositionChanged -= Weenie_OnPositionChanged;
                UBLoader.FilterCore.Scripts.GameState.Character.Weenie.ItemIds.CollectionChanged -= ItemIds_CollectionChanged;

                UBLoader.FilterCore.Scripts.MessageHandler.Outgoing.Magic_CastTargetedSpell -= Outgoing_Magic_CastTargetedSpell;
                UBLoader.FilterCore.Scripts.MessageHandler.Outgoing.Magic_CastUntargetedSpell -= Outgoing_Magic_CastUntargetedSpell;
                UBLoader.FilterCore.Scripts.MessageHandler.Incoming.Communication_TextboxString -= Incoming_Communication_TextboxString;
            }
        }
    }
}
