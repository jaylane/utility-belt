using Antlr4.Runtime;
using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UBHelper;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Expressions;
using UtilityBelt.Lib.Settings;
using UtilityBelt.Lib.VTNav;
using UBLoader.Lib.Settings;
using Hellosam.Net.Collections;
using Newtonsoft.Json;
using VirindiViewService.Controls;
using System.Collections;
using Harmony;
using Decal.Filters;
using Decal.Adapter;
using System.Security.Cryptography;

namespace UtilityBelt.Tools {
    [Name("VTank")]
    public class VTankControl : ToolBase {
        internal Dictionary<string, object> ExpressionVariables = new Dictionary<string, object>();
        internal Dictionary<string, object> PersistentExpressionVariableCache = new Dictionary<string, object>();
        private Random rnd = new Random();

        public static Regex castRe = new Regex("^You cast (?<spellname>.*) on .*$", RegexOptions.Compiled);
        public Dictionary<string, string> PatchedAuraSpells { get; private set; }
        public Dictionary<int, int> PlayerDescSkillState { get; private set; } = new Dictionary<int, int>();

        #region Config
        [Summary("VitalSharing")]
        [Hotkey("VitalSharing", "Toggle VitalSharing functionality")]
        public readonly Setting<bool> VitalSharing = new Setting<bool>(true);

        [Summary("PatchExpressionEngine")]
        [Hotkey("PatchExpressionEngine", "Overrides vtank's meta expression engine. This allows for new meta expression functions and language features.")]
        public readonly Setting<bool> PatchExpressionEngine = new Setting<bool>(false);

        [Summary("Detect and fix vtank nav portal loops")]
        public readonly Setting<bool> FixPortalLoops = new Setting<bool>(false);

        [Summary("Number of portal loops to the same location to trigger portal loop fix")]
        public readonly Setting<int> PortalLoopCount = new Setting<int>(3);
        #endregion

        #region Commands
        #region /ub translateroute
        [Summary("Translates a VTank nav route from one landblock to another. Add force flag to overwrite the output nav. **NOTE**: This will translate **ALL** points, even if some are in a dungeon and some are not, it doesn't care.")]
        [Usage("/ub translateroute <startLandblock> <routeToLoad> <endLandblock> <routeToSaveAs> [force]")]
        [Example("/ub translateroute 0x00640371 eo-east.nav 0x002B0371 eo-main.nav", "Translates eo-east.nav to landblock 0x002B0371(eo main) and saves it as eo-main.nav if the file doesn't exist")]
        [Example("/ub translateroute 0x00640371 eo-east.nav 0x002B0371 eo-main.nav force", "Translates eo-east.nav to landblock 0x002B0371(eo main) and saves it as eo-main.nav, overwriting if the file exists")]
        [CommandPattern("translateroute", @"^ *(?<StartLandblock>[0-9A-Fx]+) +(?<RouteToLoad>.+\.(nav)) +(?<EndLandblock>[0-9A-Fx]+) +(?<RouteToSaveAs>.+\.(nav)) *(?<Force>force)?$")]
        public void TranslateRoute(string command, Match args) {
            try {
                LogDebug($"Translating route: RouteToLoad:{args.Groups["RouteToLoad"].Value} StartLandblock:{args.Groups["StartLandblock"].Value} EndLandblock:{args.Groups["EndLandblock"].Value} RouteToSaveAs:{args.Groups["RouteToSaveAs"].Value} Force:{!string.IsNullOrEmpty(args.Groups["Force"].Value)}");

                if (!uint.TryParse(args.Groups["StartLandblock"].Value.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out uint startLandblock)) {
                    LogError($"Could not parse hex value from StartLandblock: {args.Groups["StartLandblock"].Value}");
                    return;
                }

                if (!uint.TryParse(args.Groups["EndLandblock"].Value.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out uint endLandblock)) {
                    LogError($"Could not parse hex value from EndLandblock: {args.Groups["EndLandblock"].Value}");
                    return;
                }

                var loadPath = Path.Combine(Util.GetVTankProfilesDirectory(), args.Groups["RouteToLoad"].Value);
                if (!File.Exists(loadPath)) {
                    LogError($"Could not find route to load: {loadPath}");
                    return;
                }

                var savePath = Path.Combine(Util.GetVTankProfilesDirectory(), args.Groups["RouteToSaveAs"].Value);
                if (string.IsNullOrEmpty(args.Groups["Force"].Value) && File.Exists(savePath)) {
                    LogError($"Output path already exists! Run with force flag to overwrite: {savePath}");
                    return;
                }

                var route = new Lib.VTNav.VTNavRoute(loadPath, UB);
                if (!route.Parse()) {
                    LogError($"Unable to parse route");
                    return;
                }
                var allPoints = route.points.Where((p) => (p.Type == Lib.VTNav.eWaypointType.Point)).ToArray();
                if (allPoints.Length <= 0) {
                    LogError($"Unable to translate route, no nav points found! Type:{route.NavType}");
                    return;
                }

                var ewOffset = Geometry.LandblockXDifference(startLandblock, endLandblock) / 240f;
                var nsOffset = Geometry.LandblockYDifference(startLandblock, endLandblock) / 240f;

                foreach (var point in route.points) {
                    point.EW += ewOffset;
                    point.NS += nsOffset;
                }

                using (StreamWriter file = new StreamWriter(savePath)) {
                    route.Write(file);
                    file.Flush();
                }

                LogDebug($"Translated {route.RecordCount} records from {startLandblock:X8} to {endLandblock:X8} by adding offsets NS:{nsOffset} EW:{ewOffset}\nSaved to file: {savePath}");
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }
        #endregion //ub translateroute

        #region /ub listvars
        [Summary("Prints out all defined variables")]
        [Usage("/ub listvars")]
        [Example("/ub listvars", "Prints out all defined variables")]
        [CommandPattern("listvars", @"^$")]
        public void ListVars(string _, Match _1) {
            var results = "Defined variables:\n";
            foreach (var kv in ExpressionVariables) {
                results += $"{kv.Key} ({ExpressionVisitor.GetFriendlyType(kv.Value.GetType())}) = {kv.Value}\n";
            }

            Logger.WriteToChat(results);
        }
        #endregion
        #region /ub listpvars
        [Summary("Prints out all defined persistent variables for this character")]
        [Usage("/ub listpvars")]
        [Example("/ub listpvars", "Prints out all defined persistent variables for this character")]
        [CommandPattern("listpvars", @"^$")]
        public void ListPersistenVars(string _, Match _1) {
            var pvars = UB.Database.PersistentVariables.Find(
                LiteDB.Query.And(
                    LiteDB.Query.EQ("Server", UB.Core.CharacterFilter.Server),
                    LiteDB.Query.EQ("Character", UB.Core.CharacterFilter.Name)
                )
            );
            var results = "Defined persistent variables:\n";
            foreach (var v in pvars) {
                var obj = DeserializeExpressionValue(v.Value, string.IsNullOrEmpty(v.Type) ? null : Type.GetType(v.Type));
                results += $"{v.Name} ({ExpressionVisitor.GetFriendlyType(obj.GetType())}) = {obj.ToString()}\n";
            }

            Logger.WriteToChat(results);
        }
        #endregion
        #region /ub listgvars
        [Summary("Prints out all defined global variables on this server")]
        [Usage("/ub listgvars")]
        [Example("/ub listgvars", "Prints out all defined global variables for this server")]
        [CommandPattern("listgvars", @"^$")]
        public void ListGlobalVars(string _, Match _1) {
            var pvars = UB.Database.GlobalVariables.Find(
                LiteDB.Query.EQ("Server", UB.Core.CharacterFilter.Server)
            );
            var results = "Defined global variables:\n";
            foreach (var v in pvars) {
                var obj = DeserializeExpressionValue(v.Value, string.IsNullOrEmpty(v.Type) ? null : Type.GetType(v.Type));
                results += $"{v.Name} ({ExpressionVisitor.GetFriendlyType(obj.GetType())}) = {obj.ToString()}\n";
            }

            Logger.WriteToChat(results);
        }
        #endregion
        #endregion

        #region Expressions
        #region Variables
        #region testvar[string varname]
        [ExpressionMethod("testvar")]
        [ExpressionParameter(0, typeof(string), "varname", "Variable name to check")]
        [ExpressionReturn(typeof(double), "Returns 1 if the variable is defined, 0 if it isn't")]
        [Summary("Checks if a variable is defined")]
        [Example("testvar[myvar]", "Returns 1 if `myvar` variable is defined")]
        public object Testvar(string varname) {
            return ExpressionVariables.ContainsKey(varname);
        }
        #endregion //testvar[string varname]
        #region getvar[string varname]
        [ExpressionMethod("getvar")]
        [ExpressionParameter(0, typeof(string), "varname", "Variable name to get")]
        [ExpressionReturn(typeof(double), "Returns the value of a variable, or 0 if undefined")]
        [Summary("Returns the value stored in a variable")]
        [Example("getvar[myvar]", "Returns the value stored in `myvar` variable")]
        public object Getvar(string varname) {
            if (ExpressionVariables.TryGetValue(varname, out object value)) {
                return value;
            }

            return 0;
        }
        #endregion //getvar[string varname]
        #region setvar[string varname, object value]
        [ExpressionMethod("setvar")]
        [ExpressionParameter(0, typeof(string), "varname", "Variable name to set")]
        [ExpressionParameter(1, typeof(object), "value", "Value to store")]
        [ExpressionReturn(typeof(object), "Returns the newly set value")]
        [Summary("Stores a value in a variable")]
        [Example("setvar[myvar,1]", "Stores the number value `1` inside of `myvar` variable")]
        public object Setvar(string varname, object value) {
            object v = (value == null) ? 0 : value;
            if (ExpressionVariables.ContainsKey(varname))
                ExpressionVariables[varname] = v;
            else
                ExpressionVariables.Add(varname, v);

            return value;
        }
        #endregion //setvar[string varname, object value]
        #region touchvar[string varname]
        [ExpressionMethod("touchvar")]
        [ExpressionParameter(0, typeof(string), "varname", "Variable name to touch")]
        [ExpressionReturn(typeof(double), "Returns 1 if the variable was previously defined, 0 otherwise")]
        [Summary("Sets the value of a variable to 0 if the variable was previously undefined")]
        [Example("touchvar[myvar]", "Ensures that `myvar` has a value set")]
        public object Touchvar(string varname) {
            if (ExpressionVariables.ContainsKey(varname))
                return true;
            else
                ExpressionVariables.Add(varname, false);

            return false;
        }
        #endregion //touchvar[string varname]
        #region clearallvars[]
        [ExpressionMethod("clearallvars")]
        [Summary("Unsets all variables")]
        [ExpressionReturn(typeof(double), "Returns 1")]
        [Example("clearallvars[]", "Unset all variables")]
        public object Clearallvars() {
            ExpressionVariables.Clear();
            return true;
        }
        #endregion //clearallvars[]
        #region clearvar[string varname]
        [ExpressionMethod("clearvar")]
        [ExpressionParameter(0, typeof(string), "varname", "Variable name to clear")]
        [Summary("Clears the value of a variable")]
        [ExpressionReturn(typeof(double), "Returns 1 if the variable was defined, 0 otherwise")]
        [Example("clearvar[myvar]", "Clears the value stored in `myvar` variable")]
        public object Clearvar(string varname) {
            if (ExpressionVariables.ContainsKey(varname)) {
                ExpressionVariables.Remove(varname);
                return true;
            }

            return false;
        }
        #endregion //clearvar[string varname]
        #region testpvar[string varname]
        [ExpressionMethod("testpvar")]
        [ExpressionParameter(0, typeof(string), "varname", "Variable name to check")]
        [ExpressionReturn(typeof(double), "Returns 1 if the variable is defined, 0 if it isn't")]
        [Summary("Checks if a persistent variable is defined")]
        [Example("testpvar[myvar]", "Returns 1 if `myvar` persistent variable is defined")]
        public object Testpvar(string varname) {
            if (PersistentExpressionVariableCache.ContainsKey(varname))
                return PersistentExpressionVariableCache[varname] != null;
            var variable = UB.Database.PersistentVariables.FindOne(
                LiteDB.Query.And(
                    LiteDB.Query.EQ("Name", varname),
                    LiteDB.Query.And(
                        LiteDB.Query.EQ("Server", UB.Core.CharacterFilter.Server),
                        LiteDB.Query.EQ("Character", UB.Core.CharacterFilter.Name)
                    )
                )
            );

            return variable != null;
        }
        #endregion //testpvar[string varname]
        #region getpvar[string varname]
        [ExpressionMethod("getpvar")]
        [ExpressionParameter(0, typeof(string), "varname", "Variable name to get")]
        [ExpressionReturn(typeof(double), "Returns the value of a variable, or 0 if undefined")]
        [Summary("Returns the value stored in a persistent variable")]
        [Example("getpvar[myvar]", "Returns the value stored in the persistent `myvar` variable")]
        public object Getpvar(string varname, ExpressionVisitor.ExpressionState state) {
            // ensure we return the same instance of the variable if it has already been used
            if (state.PersistentVariables.ContainsKey(varname))
                return state.PersistentVariables[varname];

            if (PersistentExpressionVariableCache.ContainsKey(varname)) {
                state.PersistentVariables.Add(varname, PersistentExpressionVariableCache[varname]);
                return PersistentExpressionVariableCache[varname];
            }

            var variable = UB.Database.PersistentVariables.FindOne(
                LiteDB.Query.And(
                    LiteDB.Query.EQ("Name", varname),
                    LiteDB.Query.And(
                        LiteDB.Query.EQ("Server", UB.Core.CharacterFilter.Server),
                        LiteDB.Query.EQ("Character", UB.Core.CharacterFilter.Name)
                    )
                )
            );

            object val = null;
            if (variable != null && !string.IsNullOrEmpty(variable.Type)) {
                var t = Type.GetType(variable.Type);
                val = variable == null ? "0" : DeserializeExpressionValue(variable.Value, t);
            }
            else if (variable != null) {
                val = DeserializeExpressionValue(variable.Value);
            }
            PersistentExpressionVariableCache.Add(varname, val);
            state.PersistentVariables.Add(varname, val);

            return val;
        }
        #endregion //getpvar[string varname]
        #region setpvar[string varname, object value]
        [ExpressionMethod("setpvar")]
        [ExpressionParameter(0, typeof(string), "varname", "Variable name to set")]
        [ExpressionParameter(1, typeof(object), "value", "Value to store")]
        [ExpressionReturn(typeof(object), "Returns the newly set value")]
        [Summary("Stores a value in a persistent variable that is available ever after relogging.  Persistent variables are not shared between characters.")]
        [Example("setpvar[myvar,1]", "Stores the number value `1` inside of `myvar` variable")]
        public object Setpvar(string varname, object value) {
            string serializedValue = "";
            if (!SerializeExpressionValue(value, ref serializedValue))
                return 0;

            var variable = UB.Database.PersistentVariables.FindOne(
                LiteDB.Query.And(
                    LiteDB.Query.EQ("Name", varname),
                    LiteDB.Query.And(
                        LiteDB.Query.EQ("Server", UB.Core.CharacterFilter.Server),
                        LiteDB.Query.EQ("Character", UB.Core.CharacterFilter.Name)
                    )
                )
            );

            if (variable == null) {
                UB.Database.PersistentVariables.Insert(new Lib.Models.PersistentVariable() {
                    Server = UB.Core.CharacterFilter.Server,
                    Character = UB.Core.CharacterFilter.Name,
                    Name = varname,
                    Value = serializedValue,
                    Type = value.GetType().ToString()
                });
            }
            else {
                variable.Value = serializedValue;
                variable.Type = value.GetType().ToString();
                UB.Database.PersistentVariables.Update(variable);
            }

            if (!PersistentExpressionVariableCache.ContainsKey(varname))
                PersistentExpressionVariableCache.Add(varname, value);
            else
                PersistentExpressionVariableCache[varname] = value;

            return value;
        }
        #endregion //setpvar[string varname, object value]
        #region touchpvar[string varname]
        [ExpressionMethod("touchpvar")]
        [ExpressionParameter(0, typeof(string), "varname", "Variable name to touch")]
        [ExpressionReturn(typeof(double), "Returns 1 if the variable was previously defined, 0 otherwise")]
        [Summary("Sets the value of a persistent variable to 0 if the variable was previously undefined")]
        [Example("touchpvar[myvar]", "Ensures that `myvar` has a value set")]
        public object Touchpvar(string varname) {
            if ((bool)Testpvar(varname)) {
                return true;
            }
            else {
                Setpvar(varname, 0);
                return false;
            }
        }
        #endregion //touchpvar[string varname]
        #region clearallpvars[]
        [ExpressionMethod("clearallpvars")]
        [Summary("Unsets all persistent variables")]
        [ExpressionReturn(typeof(double), "Returns 1")]
        [Example("clearallpvars[]", "Unset all persistent variables")]
        public object Clearallpvars() {
            PersistentExpressionVariableCache.Clear();
            UB.Database.PersistentVariables.Delete(
                LiteDB.Query.And(
                    LiteDB.Query.EQ("Server", UB.Core.CharacterFilter.Server),
                    LiteDB.Query.EQ("Character", UB.Core.CharacterFilter.Name)
                )
            );
            return true;
        }
        #endregion //clearallpvars[]
        #region clearpvar[string varname]
        [ExpressionMethod("clearpvar")]
        [ExpressionParameter(0, typeof(string), "varname", "Variable name to clear")]
        [Summary("Clears the value of a persistent variable")]
        [ExpressionReturn(typeof(double), "Returns 1 if the variable was defined, 0 otherwise")]
        [Example("clearpvar[myvar]", "Clears the value stored in `myvar` persistent variable")]
        public object Clearpvar(string varname) {
            if ((bool)Testpvar(varname)) {
                PersistentExpressionVariableCache.Remove(varname);
                UB.Database.PersistentVariables.Delete(
                    LiteDB.Query.And(
                        LiteDB.Query.EQ("Name", varname),
                        LiteDB.Query.And(
                            LiteDB.Query.EQ("Server", UB.Core.CharacterFilter.Server),
                            LiteDB.Query.EQ("Character", UB.Core.CharacterFilter.Name)
                        )
                    )
                );
                return true;
            }

            return false;
        }
        #endregion //clearpvar[string varname]
        #region testgvar[string varname]
        [ExpressionMethod("testgvar")]
        [ExpressionParameter(0, typeof(string), "varname", "Variable name to check")]
        [ExpressionReturn(typeof(double), "Returns 1 if the variable is defined, 0 if it isn't")]
        [Summary("Checks if a global variable is defined")]
        [Example("testgvar[myvar]", "Returns 1 if `myvar` global variable is defined")]
        public object Testgvar(string varname) {
            var variable = UB.Database.GlobalVariables.FindOne(
                LiteDB.Query.And(
                    LiteDB.Query.EQ("Name", varname),
                    LiteDB.Query.EQ("Server", UB.Core.CharacterFilter.Server)
                )
            );

            return variable != null;
        }
        #endregion //testgvar[string varname]
        #region getgvar[string varname]
        [ExpressionMethod("getgvar")]
        [ExpressionParameter(0, typeof(string), "varname", "Variable name to get")]
        [ExpressionReturn(typeof(double), "Returns the value of a variable, or 0 if undefined")]
        [Summary("Returns the value stored in a variable")]
        [Example("getgvar[myvar]", "Returns the value stored in `myvar` global variable")]
        public object Getgvar(string varname, ExpressionVisitor.ExpressionState state) {
            // ensure we return the same instance of the variable if it has already been used
            if (state.GlobalVariables.ContainsKey(varname))
                return state.GlobalVariables[varname];

            var variable = UB.Database.GlobalVariables.FindOne(
                LiteDB.Query.And(
                    LiteDB.Query.EQ("Name", varname),
                    LiteDB.Query.EQ("Server", UB.Core.CharacterFilter.Server)
                )
            );
            object val = null;
            if (variable != null && !string.IsNullOrEmpty(variable.Type)) {
                var t = Type.GetType(variable.Type);
                val = variable == null ? "0" : DeserializeExpressionValue(variable.Value, t);
            }
            else if (variable != null) {
                val = DeserializeExpressionValue(variable.Value);
            }

            state.GlobalVariables.Add(varname, val);

            return val;
        }
        #endregion //getgvar[string varname]
        #region setgvar[string varname, object value]
        [ExpressionMethod("setgvar")]
        [ExpressionParameter(0, typeof(string), "varname", "Variable name to set")]
        [ExpressionParameter(1, typeof(object), "value", "Value to store")]
        [ExpressionReturn(typeof(object), "Returns the newly set value")]
        [Summary("Stores a value in a global variable. This variable is shared between all characters on the same server.")]
        [Example("setgvar[myvar,1]", "Stores the number value `1` inside of global `myvar` variable")]
        public object Setgvar(string varname, object value) {
            string serializedValue = "";
            if (!SerializeExpressionValue(value, ref serializedValue))
                return 0;

            var variable = UB.Database.GlobalVariables.FindOne(
                LiteDB.Query.And(
                    LiteDB.Query.EQ("Name", varname),
                    LiteDB.Query.EQ("Server", UB.Core.CharacterFilter.Server)
                )
            );

            if (variable == null) {
                UB.Database.GlobalVariables.Insert(new Lib.Models.GlobalVariable() {
                    Server = UB.Core.CharacterFilter.Server,
                    Name = varname,
                    Value = serializedValue,
                    Type = value.GetType().ToString()
                });
            }
            else {
                variable.Value = serializedValue;
                variable.Type = value.GetType().ToString();
                UB.Database.GlobalVariables.Update(variable);
            }

            return value;
        }
        #endregion //setgvar[string varname, object value]
        #region touchgvar[string varname]
        [ExpressionMethod("touchgvar")]
        [ExpressionParameter(0, typeof(string), "varname", "Variable name to touch")]
        [ExpressionReturn(typeof(double), "Returns 1 if the variable was previously defined, 0 otherwise")]
        [Summary("Sets the value of a global variable to 0 if the variable was previously undefined")]
        [Example("touchgvar[myvar]", "Ensures that `myvar` global variable has a value set")]
        public object Touchgvar(string varname) {
            if ((bool)Testgvar(varname)) {
                return true;
            }
            else {
                Setgvar(varname, 0);
                return false;
            }
        }
        #endregion //touchgvar[string varname]
        #region clearallgvars[]
        [ExpressionMethod("clearallgvars")]
        [Summary("Unsets all global variables")]
        [ExpressionReturn(typeof(double), "Returns 1")]
        [Example("clearallgvars[]", "Unset all global variables")]
        public object Clearallgvars() {
            UB.Database.GlobalVariables.Delete(
                LiteDB.Query.EQ("Server", UB.Core.CharacterFilter.Server)
            );
            return true;
        }
        #endregion //clearallgvars[]
        #region cleargvar[string varname]
        [ExpressionMethod("cleargvar")]
        [ExpressionParameter(0, typeof(string), "varname", "Variable name to clear")]
        [Summary("Clears the value of a global variable")]
        [ExpressionReturn(typeof(double), "Returns 1 if the variable was defined, 0 otherwise")]
        [Example("cleargvar[myvar]", "Clears the value stored in `myvar` global variable")]
        public object Cleargvar(string varname) {
            if ((bool)Testgvar(varname)) {
                UB.Database.GlobalVariables.Delete(
                    LiteDB.Query.And(
                        LiteDB.Query.EQ("Name", varname),
                        LiteDB.Query.EQ("Server", UB.Core.CharacterFilter.Server)
                    )
                );
                return true;
            }

            return false;
        }
        #endregion //cleargvar[string varname]
        #endregion //Variables
        #region Chat
        #region chatbox[string message]
        [ExpressionMethod("chatbox")]
        [ExpressionParameter(0, typeof(string), "message", "Message to send")]
        [ExpressionReturn(typeof(string), "Returns the string sent to the charbox")]
        [Summary("Sends a message to the chatbox as if you had typed it in")]
        [Example("chatbox[test]", "sends 'test' to the chatbox")]
        public object Chatbox(string text) {
            Util.DispatchChatToBoxWithPluginIntercept(text);
            return text;
        }
        #endregion //chatboxpaste[string message]
        #region chatboxpaste[string message]
        [ExpressionMethod("chatboxpaste")]
        [ExpressionParameter(0, typeof(string), "message", "Message to paste")]
        [ExpressionReturn(typeof(double), "Returns 1 if successful")]
        [Summary("Pastes a message to the chatbox, leaving focus, so that the user can complete typing it")]
        [Example("chatboxpaste[test]", "pastes `test` to the chatbox, without sending")]
        public object Chatboxpaste(string text) {
            return VTankExtensions.ChatboxPaste(text);
        }
        #endregion //chatboxpaste[string message]
        #region echo[string message, number color]
        [ExpressionMethod("echo")]
        [ExpressionParameter(0, typeof(string), "message", "Message to echo")]
        [ExpressionParameter(1, typeof(double), "color", "Message color")]
        [ExpressionReturn(typeof(double), "Returns 1 on success, 0 on failure")]
        [Summary("Echos chat to the chatbox with a color. Use `/ub printcolors` to view all colors")]
        [Example("echo[test,15]", "echos 'test' to the chatbox in red (Help)")]
        public object Echo(object text, double color) {
            UB.Core.Actions.AddChatText(text.ToString(), Convert.ToInt32(color));
            return true;
        }
        #endregion //echo[string message, int color]
        #endregion //Chat
        #region Character
        #region getcharintprop[int property]
        [ExpressionMethod("getcharintprop")]
        [ExpressionParameter(0, typeof(double), "property", "IntProperty to return")]
        [ExpressionReturn(typeof(double), "Returns an int property from your character")]
        [Summary("Returns an int property from your character, or 0 if it's undefined")]
        [Example("getcharintprop[25]", "Returns your character's current level")]
        public object Getcharintprop(double property) {
            var v = (double)UtilityBeltPlugin.Instance.Core.CharacterFilter.GetCharProperty(Convert.ToInt32(property));
            return (v != -1) ? v : 0;
        }
        #endregion //getcharintprop[int property]
        #region getchardoubleprop[int property]
        [ExpressionMethod("getchardoubleprop")]
        [ExpressionParameter(0, typeof(double), "property", "DoubleProperty to return")]
        [ExpressionReturn(typeof(double), "Returns a double property from your character")]
        [Summary("Returns a double property from your character, or 0 if it's undefined")]
        [Example("getchardoubleprop[25]", "Returns your character's current level")]
        public object Getchardoubleprop(double property) {
            return GetCharacter().Values((DoubleValueKey)Convert.ToInt32(property), 0);
        }
        #endregion //getchardoubleprop[int property]
        #region getcharquadprop[int property]
        [ExpressionMethod("getcharquadprop")]
        [ExpressionParameter(0, typeof(double), "property", "QuadProperty to return")]
        [ExpressionReturn(typeof(double), "Returns a quad property from your character")]
        [Summary("Returns a quad property from your character, or 0 if it's undefined")]
        [Example("getcharquadprop[1]", "Returns your character's TotalExperience")]
        public object Getcharquadprop(double property) {
            long? propVal = UBHelper.Player.InqInt64((int)property);
            return propVal.HasValue ? (double)propVal.Value : 0;
        }
        #endregion //getcharquadprop[int property]
        #region getcharboolprop[int property]
        [ExpressionMethod("getcharboolprop")]
        [ExpressionParameter(0, typeof(double), "property", "BoolProperty to return")]
        [ExpressionReturn(typeof(double), "Returns a bool property from your character")]
        [Summary("Returns a bool property from your character, or 0 if it's undefined")]
        [Example("getcharboolprop[110]", "Returns your character's AwayFromKeyboard status")]
        public object Getcharboolprop(double property) {
            return GetCharacter().Values((BoolValueKey)Convert.ToInt32(property), false);
        }
        #endregion //getcharboolprop[int property]
        #region getcharstringprop[int property]
        [ExpressionMethod("getcharstringprop")]
        [ExpressionParameter(0, typeof(double), "property", "StringProperty to return")]
        [ExpressionReturn(typeof(string), "Returns a string property from your character")]
        [Summary("Returns a string property from your character, or false if it's undefined")]
        [Example("getcharstringprop[1]", "Returns your character's name")]
        public object Getcharstringprop(double property) {
            var v = GetCharacter().Values((StringValueKey)Convert.ToInt32(property), "");

            if (string.IsNullOrEmpty(v))
                return 0;

            return v;
        }
        #endregion //getcharstringprop[int property]
        #region getisspellknown[int spellId]
        [ExpressionMethod("getisspellknown")]
        [ExpressionParameter(0, typeof(double), "spellId", "Spell ID to check")]
        [ExpressionReturn(typeof(double), "Returns 1 if your character knows this spell, 0 otherwise")]
        [Summary("Checks if your character knowns a spell by id")]
        [Example("getisspellknown[2931]", "Checks if your character knowns the Recall Aphus Lassel spell")]
        public object Getisspellknown(double spellId) {
            return Spells.IsKnown(Convert.ToInt32(spellId));
        }
        #endregion //getisspellknown[int spellId]
        #region getcancastspell_hunt[int spellId]
        [ExpressionMethod("getcancastspell_hunt")]
        [ExpressionParameter(0, typeof(double), "spellId", "Spell ID to check")]
        [ExpressionReturn(typeof(double), "Returns 1 if your character can cast this spell while hunting, 0 otherwise")]
        [Summary("Checks if your character is capable of casting spellId while hunting")]
        [Example("getcancastspell_hunt[2931]", "Checks if your character is capable of casting the Recall Aphus Lassel spell while hunting")]
        public object Getcancastspell_hunt(double spellId) {
            var id = Convert.ToInt32(spellId);
            return Spells.IsKnown(id) && Spells.HasComponents(id) && Spells.HasSkillHunt(id);
        }
        #endregion //getcancastspell_hunt[int spellId]
        #region getcancastspell_buff[int spellId]
        [ExpressionMethod("getcancastspell_buff")]
        [ExpressionParameter(0, typeof(double), "spellId", "Spell ID to check")]
        [ExpressionReturn(typeof(double), "Returns 1 if your character can cast this spell while buffing, 0 otherwise")]
        [Summary("Checks if your character is capable of casting spellId while buffing")]
        [Example("getcancastspell_hunt[2931]", "Checks if your character is capable of casting the Recall Aphus Lassel spell while buffing")]
        public object Getcancastspell_buff(double spellId) {
            var id = Convert.ToInt32(spellId);
            return Spells.IsKnown(id) && Spells.HasComponents(id) && Spells.HasSkillBuff(id);
        }
        #endregion //getcancastspell_buff[int spellId]
        #region getspellexpiration[int spellId]
        [ExpressionMethod("getspellexpiration")]
        [ExpressionParameter(0, typeof(double), "spellId", "Spell ID to check")]
        [ExpressionReturn(typeof(double), "Returns the number of seconds until a spell expires.  0 if spell not active, MaxInt if it doesn't expire.")]
        [Summary("Gets the number of seconds until a spell expires by id")]
        [Example("getspellexpiration[515]", "Get the number of seconds until Acid Protection Self I expires")]
        public object Getspellexpiration(double spellId) {
            var spell = CoreManager.Current.CharacterFilter.Enchantments.Where(x => x.SpellId == (int)spellId).FirstOrDefault();
            if (spell is null)
                return 0;
            //Max value for spells that don't expire
            if (spell.TimeRemaining < 0)
                return double.MaxValue;
            else
                return (double)spell.TimeRemaining;
        }
        #endregion //getspellexpiration[int spellId]
        #region getspellexpirationbyname[string spellName]
        [ExpressionMethod("getspellexpirationbyname")]
        [ExpressionParameter(0, typeof(string), "spellName", "Spell name to check")]
        [ExpressionReturn(typeof(double), "Returns the number of seconds until a spell expires.  0 if spell not active, MaxInt if it doesn't expire, -1 if an error occurred.")]
        [Summary("Gets the number of seconds until a spell expires by name")]
        [Example("getspellexpirationbyname[`acid prot`]", "Get the number of seconds until an Acid Protection spell expires")]
        public object Getspellexpirationbyname(string spellName) {
            EnchantmentWrapper spell = null;
            var enchants = CoreManager.Current.CharacterFilter.Enchantments;

            //Look through each enchantment on the player
            for (var i = 0; i < enchants.Count; i++) {
                var enchant = enchants[i];
                var enchantId = enchant.SpellId;
                //Lookup spell information based on the ID
                var spellInfo = Util.FileService?.SpellTable?.GetById(enchantId);
                if (spellInfo is null) {
                    Logger.Debug("An error occurred obtaining the SpellTable: try using getspellexpirationbyid");
                    return -1;
                }
                //Check for a match with the expression
                if (spellInfo.Name.IndexOf(spellName, StringComparison.OrdinalIgnoreCase) >= 0) {
                    spell = enchant;
                    break;
                }
            }

            if (spell is null)
                return 0;
            //Max value for values that don't expire
            if (spell.TimeRemaining < 0)
                return double.MaxValue;
            else
                return (double)spell.TimeRemaining;
        }
        #endregion //getspellexpirationbyname[string spellName]
        #region getcharvital_base[int vitalId]
        [ExpressionMethod("getcharvital_base")]
        [ExpressionParameter(0, typeof(double), "vitalId", "Which vital to get. 1 = Health, 2 = Stamina, 3 = Mana.")]
        [ExpressionReturn(typeof(double), "Returns the base (unbuffed) vital value")]
        [Summary("Gets your characters base (unbuffed) vital value")]
        [Example("getcharvital_base[1]", "Returns your character's base health")]
        public object Getcharvital_base(double vitalId) {
            var id = Convert.ToInt32(vitalId * 2);
            return UtilityBeltPlugin.Instance.Core.CharacterFilter.Vitals[(CharFilterVitalType)id].Base;
        }
        #endregion //getcharvital_base[int vitalId]
        #region getcharvital_current[int vitalId]
        [ExpressionMethod("getcharvital_current")]
        [ExpressionParameter(0, typeof(double), "vitalId", "Which vital to get. 1 = Health, 2 = Stamina, 3 = Mana.")]
        [ExpressionReturn(typeof(double), "Returns the current vital value")]
        [Summary("Gets your characters current vital value")]
        [Example("getcharvital_current[2]", "Returns your character's current stamina")]
        public object Getcharvital_current(double vitalId) {
            var id = Convert.ToInt32(vitalId * 2);
            return UtilityBeltPlugin.Instance.Core.CharacterFilter.Vitals[(CharFilterVitalType)id].Current;
        }
        #endregion //getcharvital_current[int vitalId]
        #region getcharvital_buffedmax[int vitalId]
        [ExpressionMethod("getcharvital_buffedmax")]
        [ExpressionParameter(0, typeof(double), "vitalId", "Which vital to get. 1 = Health, 2 = Stamina, 3 = Mana.")]
        [ExpressionReturn(typeof(double), "Returns the buffed maximum vital value")]
        [Summary("Gets your characters buffed maximum vital value")]
        [Example("getcharvital_current[3]", "Returns your character's buffed maximum mana")]
        public object Getcharvital_buffedmax(double vitalId) {
            var id = Convert.ToInt32(vitalId * 2);
            return UtilityBeltPlugin.Instance.Core.CharacterFilter.Vitals[(CharFilterVitalType)id].Buffed;
        }
        #endregion //getcharvital_buffedmax[int vitalId]
        #region getcharskill_traininglevel[int skillId]
        [ExpressionMethod("getcharskill_traininglevel")]
        [ExpressionParameter(0, typeof(double), "skillId", "Which skill to check.")]
        [ExpressionReturn(typeof(double), "Returns current training level of a skill. 0 = Unusable, 1 = Untrained, 2 = Trained, 3 = Specialized")]
        [Summary("Gets your characters training level for a specified skill")]
        [Example("getcharskill_traininglevel[23]", "Returns your character's LockPick skill training level")]
        public object Getcharskill_traininglevel(double skillId) {
            return (double)UtilityBeltPlugin.Instance.Core.CharacterFilter.Underlying.Skill[(Decal.Interop.Filters.eSkillID)Convert.ToInt32(skillId)].Training;
        }
        #endregion //getcharskill_traininglevel[int vitalId]
        #region getcharskill_base[int skillId]
        [ExpressionMethod("getcharskill_base")]
        [ExpressionParameter(0, typeof(double), "skillId", "Which skill to check.")]
        [ExpressionReturn(typeof(double), "Returns base skill level of the specified skill")]
        [Summary("Gets your characters base skill level for a speficied skill")]
        [Example("getcharskill_base[43]", "Returns your character's base Void skill level")]
        public object Getcharskill_base(double skillId) {
            return (double)UtilityBeltPlugin.Instance.Core.Actions.Skill[(Decal.Adapter.Wrappers.SkillType)Convert.ToInt32(skillId) + 50];
        }
        #endregion //getcharskill_base[int vitalId]
        #region getcharskill_buffed[int skillId]
        [ExpressionMethod("getcharskill_buffed")]
        [ExpressionParameter(0, typeof(double), "skillId", "Which skill to check.")]
        [ExpressionReturn(typeof(double), "Returns buffed skill level of the specified skill")]
        [Summary("Gets your characters buffed skill level for a speficied skill")]
        [Example("getcharskill_buffed[33]", "Returns your character's buffed Life Magic skill level")]
        public object Getcharskill_buffed(double skillId) {
            var buffedSkill = (double)UtilityBeltPlugin.Instance.Core.CharacterFilter.Underlying.Skill[(Decal.Interop.Filters.eSkillID)Convert.ToInt32(skillId)].Buffed;

            if (UB.Core.CharacterFilter.GetCharProperty(326/*Jack of All Trades*/) == 1)
                buffedSkill += 5;
            if (UB.Core.CharacterFilter.GetCharProperty((int)Augmentations.MasterFiveFoldPath) == 1)
                buffedSkill += 10;

            return buffedSkill;
        }
        #endregion //getcharskill_buffed[int vitalId]
        #region getcharattribute_buffed[int attributeId]
        [ExpressionMethod("getcharattribute_buffed")]
        [ExpressionParameter(0, typeof(double), "attributeId", "Which attribute to check. 1 = Strength, 2 = Endurance, 3 = Quickness, 4 = Coordination, 5 = Focus, 6 = Self")]
        [ExpressionReturn(typeof(double), "Returns buffed attribute level of the specified attribute")]
        [Summary("Gets your characters buffed attribute level for a speficied attribute")]
        [Example("getcharattribute_buffed[1]", "Returns your character's buffed Strength attribute level")]
        public object Getcharattribute_buffed(double attributeId) {
            var buffedAttribute = (double)UtilityBeltPlugin.Instance.Core.CharacterFilter.Underlying.Attribute[(Decal.Interop.Filters.eAttributeID)Convert.ToInt32(attributeId)].Buffed;

            return buffedAttribute;
        }
        #endregion //getcharattribute_buffed[int attributeId]
        #region getcharattribute_base[int attributeId]
        [ExpressionMethod("getcharattribute_base")]
        [ExpressionParameter(0, typeof(double), "attributeId", "Which attribute to check. 1 = Strength, 2 = Endurance, 3 = Quickness, 4 = Coordination, 5 = Focus, 6 = Self")]
        [ExpressionReturn(typeof(double), "Returns base attribute level of the specified attribute")]
        [Summary("Gets your characters base attribute level for a speficied attribute")]
        [Example("getcharattribute_base[1]", "Returns your character's base Strength attribute level")]
        public object Getcharattribute_base(double attributeId) {
            var baseAttribute = (double)UtilityBeltPlugin.Instance.Core.CharacterFilter.Underlying.Attribute[(Decal.Interop.Filters.eAttributeID)Convert.ToInt32(attributeId)].Base;

            return baseAttribute;
        }
        #endregion //getcharattribute_base[int attributeId]
        #region getcharburden[]
        [ExpressionMethod("getcharburden")]
        [ExpressionReturn(typeof(double), "Return current burden shown in character panel")]
        [Summary("Gets your current burden shown on the character panel")]
        [Example("getcharburden[0]", "Returns your character's current burden level")]
        public object Getcharburden() {
            return Util.GetFriendlyBurden();
        }
        #endregion //getcharattribute_base[int attributeId]
        #region getplayerlandcell[]
        [ExpressionMethod("getplayerlandcell")]
        [ExpressionReturn(typeof(double), "Returns the landcell your character is currently standing in")]
        [Summary("Gets the landcell your character is currently standing in")]
        [Example("getplayerlandcell[]", "Returns your character's current landcell as in uint")]
        public object Getplayerlandcell() {
            return (double)(uint)UtilityBeltPlugin.Instance.Core.Actions.Landcell;
        }
        #endregion //getplayerlandcell[]
        #region getplayerlandblock[]
        [ExpressionMethod("getplayerlandblock")]
        [ExpressionReturn(typeof(double), "Returns the landblock your character is currently standing in")]
        [Summary("Gets the landblock your character is currently standing in")]
        [Example("getplayerlandblock[]", "Returns your character's current landblock as in uint")]
        public object Getplayerlandblock() {
            return (double)((uint)UtilityBeltPlugin.Instance.Core.Actions.Landcell & 0xFFFF0000); ;
        }
        #endregion //getplayerlandblock[]
        #region getplayercoordinates[]
        [ExpressionMethod("getplayercoordinates")]
        [ExpressionReturn(typeof(ExpressionCoordinates), "Returns your character's global coordinates object")]
        [Summary("Gets the a coordinates object representing your characters current global position")]
        [Example("getplayercoordinates[]", "Returns your character's current position as coordinates object")]
        public object Getplayercoordinates() {
            return ExpressionWorldObjectgetphysicscoordinates((ExpressionWorldObject)ExpressionWorldObjectgetplayer());
        }
        #endregion //getplayercoordinates[]
        #endregion //Character
        #region Coordinates
        #region coordinategetns[coordinates obj]
        [ExpressionMethod("coordinategetns")]
        [ExpressionParameter(0, typeof(ExpressionCoordinates), "obj", "coordinates object to get the NS position of")]
        [ExpressionReturn(typeof(double), "Returns the NS position of a coordinates object as a number")]
        [Summary("Gets the NS position of a coordinates object as a number")]
        [Example("coordinategetns[getplayercoordinates[]]", "Returns your character's current NS position")]
        public object Coordinategetns(ExpressionCoordinates coords) {
            return coords.NS;
        }
        #endregion //coordinategetns[coordinates obj]
        #region coordinategetwe[coordinates obj]
        [ExpressionMethod("coordinategetwe")]
        [ExpressionParameter(0, typeof(ExpressionCoordinates), "obj", "coordinates object to get the EW position of")]
        [ExpressionReturn(typeof(double), "Returns the EW position of a coordinates object as a number")]
        [Summary("Gets the EW position of a coordinates object as a number")]
        [Example("coordinategetwe[getplayercoordinates[]]", "Returns your character's current EW position")]
        public object Coordinategetew(ExpressionCoordinates coords) {
            return coords.EW;
        }
        #endregion //coordinategetwe[coordinates obj]
        #region coordinategetz[coordinates obj]
        [ExpressionMethod("coordinategetz")]
        [ExpressionParameter(0, typeof(ExpressionCoordinates), "obj", "coordinates object to get the Z position of")]
        [ExpressionReturn(typeof(double), "Returns the Z position of a coordinates object as a number")]
        [Summary("Gets the Z position of a coordinates object as a number")]
        [Example("coordinategetz[getplayercoordinates[]]", "Returns your character's current Z position")]
        public object Coordinategetz(ExpressionCoordinates coords) {
            return coords.Z / 240;
        }
        #endregion //coordinategetz[coordinates obj]
        #region coordinatetostring[coordinates obj]
        [ExpressionMethod("coordinatetostring")]
        [ExpressionParameter(0, typeof(ExpressionCoordinates), "obj", "coordinates object to convert to a string")]
        [ExpressionReturn(typeof(double), "Returns a string representation of a coordinates object, like `1.2N, 34.5E`")]
        [Summary("Converts a coordinates object to a string representation")]
        [Example("coordinatetostring[getplayercoordinates[]]", "Returns your character's current coordinates as a string, eg `1.2N, 34.5E`")]
        public object Coordinatetostring(ExpressionCoordinates coords) {
            return coords.ToString();
        }
        #endregion //coordinatetostring[coordinates obj]
        #region coordinateparse[string coordstring]
        [ExpressionMethod("coordinateparse")]
        [ExpressionParameter(0, typeof(string), "coordstring", "coordinates string to parse")]
        [ExpressionReturn(typeof(Coordinates), "Returns a coordinates object")]
        [Summary("Converts a coordinate string like `1.2N, 3.4E` to a coordinates object")]
        [Example("coordinateparse[`1.2N, 3.4E`]", "Returns a coordinates object representing `1.2N, 3.4E`")]
        public object Coordinateparse(string coordsToParse) {
            var coords = Coordinates.FromString(coordsToParse);
            return new ExpressionCoordinates(coords.EW, coords.NS, coords.Z);
        }
        #endregion //coordinateparse[string coordstring]
        #region coordinatedistancewithz[coordinates obj1, coordinates obj2]
        [ExpressionMethod("coordinatedistancewithz")]
        [ExpressionParameter(0, typeof(ExpressionCoordinates), "obj1", "first coordinates object")]
        [ExpressionParameter(1, typeof(ExpressionCoordinates), "obj2", "second coordinates object")]
        [ExpressionReturn(typeof(double), "Returns the 3d distance in meters between obj1 and obj2")]
        [Summary("Gets the 3d distance in meters between two coordinates objects")]
        [Example("coordinatedistancewithz[coordinateparse[`1.2N, 3.4E`], coordinateparse[`5.6N, 7.8E`]]", "Returns the 3d distance between `1.2N, 3.4E` and `5.6N, 7.8E`")]
        public object Coordinatedistancewithz(ExpressionCoordinates obj1, ExpressionCoordinates obj2) {
            return obj1.Coordinates.DistanceTo(obj2.Coordinates);
        }
        #endregion //coordinatedistancewithz[coordinates obj1, coordinates obj2]
        #region coordinatedistanceflat[coordinates obj1, coordinates obj2]
        [ExpressionMethod("coordinatedistanceflat")]
        [ExpressionParameter(0, typeof(ExpressionCoordinates), "obj1", "first coordinates object")]
        [ExpressionParameter(1, typeof(ExpressionCoordinates), "obj2", "second coordinates object")]
        [ExpressionReturn(typeof(double), "Returns the 2d distance in meters between obj1 and obj2 (ignoring Z)")]
        [Summary("Gets the 2d distance in meters between two coordinates objects (ignoring Z)")]
        [Example("coordinatedistancewithz[coordinateparse[`1.2N, 3.4E`], coordinateparse[`5.6N, 7.8E`]]", "Returns the 2d distance between `1.2N, 3.4E` and `5.6N, 7.8E` (ignoring Z)")]
        public object Coordinatedistanceflat(ExpressionCoordinates obj1, ExpressionCoordinates obj2) {
            return obj1.Coordinates.DistanceToFlat(obj2.Coordinates);
        }
        #endregion //coordinatedistanceflat[coordinates obj1, coordinates obj2]
        #endregion //Coordinates
        #region WorldObjects
        #region wobjectgetphysicscoordinates[worldobject wo]
        [ExpressionMethod("wobjectgetphysicscoordinates")]
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "wo", "world object to get coordinates of")]
        [ExpressionReturn(typeof(ExpressionCoordinates), "Returns a coordinates object representing the passed wobject")]
        [Summary("Gets a coordinates object representing a world objects current position")]
        [Example("wobjectgetphysicscoordinates[wobjectgetplayer[]]", "Returns a coordinates object representing the current player's position")]
        public object ExpressionWorldObjectgetphysicscoordinates(ExpressionWorldObject wo) {
            var pos = PhysicsObject.GetPosition(wo.Wo.Id);
            var landcell = PhysicsObject.GetLandcell(wo.Wo.Id);
            return new ExpressionCoordinates() {
                NS = Geometry.LandblockToNS((uint)landcell, pos.Y),
                EW = Geometry.LandblockToEW((uint)landcell, pos.X),
                Z = pos.Z
            };
        }
        #endregion //wobjectgetphysicscoordinates[worldobject wo]
        #region wobjectgetname[worldobject wo]
        [ExpressionMethod("wobjectgetname")]
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "wo", "world object to get the name of")]
        [ExpressionReturn(typeof(string), "Returns a string of wobject's name")]
        [Summary("Gets the name string of a wobject")]
        [Example("wobjectgetname[wobjectgetplayer[]]", "Returns a string representing the name of the world object")]
        public object ExpressionWorldObjectgetname(ExpressionWorldObject wo) {
            return Util.GetObjectName(wo.Wo.Id);
        }
        #endregion //wobjectgetname[worldobject wo]
        #region wobjectgetid[worldobject wo]
        [ExpressionMethod("wobjectgetid")]
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "wo", "world object to get the id of")]
        [ExpressionReturn(typeof(double), "Returns the id of the passed wobject")]
        [Summary("Gets the id of a wobject")]
        [Example("wobjectgetid[wobjectgetplayer[]]", "Returns the id of the current player")]
        public object ExpressionWorldObjectgetid(ExpressionWorldObject wo) {
            return (double)wo.Wo.Id;
        }
        #endregion //wobjectgetid[worldobject wo]
        #region wobjectgetobjectclass[worldobject wo]
        [ExpressionMethod("wobjectgetobjectclass")]
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "wo", "world object to get the objectclass of")]
        [ExpressionReturn(typeof(double), "Returns a number representing the passed wobjects objectclass")]
        [Summary("Gets the objectclass as a number from a wobject")]
        [Example("wobjectgetobjectclass[wobjectgetplayer[]]", "Returns 24 (Player ObjectClass)")]
        public object ExpressionWorldObjectgetobjectclass(ExpressionWorldObject wo) {
            return Convert.ToDouble(wo.Wo.ObjectClass);
        }
        #endregion //wobjectgetobjectclass[worldobject wo]
        #region wobjectgettemplatetype[worldobject wo]
        [ExpressionMethod("wobjectgettemplatetype")]
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "wo", "world object to get the template type of")]
        [ExpressionReturn(typeof(double), "Returns a number representing the passed wobjects template type")]
        [Summary("Gets the template type as a number from a wobject")]
        [Example("wobjectgettemplatetype[wobjectgetplayer[]]", "Returns 1 (Player template type)")]
        public object ExpressionWorldObjectgettemplatetype(ExpressionWorldObject wo) {
            return Convert.ToDouble(wo.Wo.Type);
        }
        #endregion //wobjectgettemplatetype[worldobject wo]
        #region wobjectgetisdooropen[worldobject wo]
        [ExpressionMethod("wobjectgetisdooropen")]
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "wo", "door world object to check")]
        [ExpressionReturn(typeof(double), "Returns 1 if the wo door is open, 0 otherwise")]
        [Summary("Checks if a door wobject is open")]
        [Example("wobjectgetisdooropen[wobjectfindnearestdoor[]]", "Returns 1 if the door is open, 0 otherwise")]
        public object ExpressionWorldObjectgetisdooropen(ExpressionWorldObject wo) {
            return UBHelper.InventoryManager.IsDoorOpen(wo.Wo.Id);
        }
        #endregion //wobjectgetisdooropen[worldobject wo]
        #region wobjectfindnearestmonster[]
        [ExpressionMethod("wobjectfindnearestmonster")]
        [ExpressionReturn(typeof(ExpressionWorldObject), "Returns a worldobject representing the nearest monster")]
        [Summary("Gets a worldobject representing the nearest monster, or 0 if none was found")]
        [Example("wobjectfindnearestmonster[]", "Returns a worldobject representing the nearest monster")]
        public object ExpressionWorldObjectfindnearestmonster() {
            WorldObject closest = Util.FindClosestByObjectClass(ObjectClass.Monster);

            if (closest != null)
                return new ExpressionWorldObject(closest.Id);

            return 0;
        }
        #endregion //wobjectfindnearestmonster[]
        #region wobjectfindnearestdoor[]
        [ExpressionMethod("wobjectfindnearestdoor")]
        [ExpressionReturn(typeof(ExpressionWorldObject), "Returns a worldobject representing the nearest door")]
        [Summary("Gets a worldobject representing the nearest door, or 0 if none was found")]
        [Example("wobjectfindnearestdoor[]", "Returns a worldobject representing the nearest door")]
        public object ExpressionWorldObjectfindnearestdoor() {
            WorldObject closest = Util.FindClosestByObjectClass(ObjectClass.Door);

            if (closest != null)
                return new ExpressionWorldObject(closest.Id);

            return 0;
        }
        #endregion //wobjectfindnearestdoor[]
        #region wobjectfindnearestbyobjectclass[int objectclass]
        [ExpressionMethod("wobjectfindnearestbyobjectclass")]
        [ExpressionParameter(0, typeof(double), "objectclass", "objectclass to filter by")]
        [ExpressionReturn(typeof(ExpressionWorldObject), "Returns a worldobject representing the nearest matching objectclass")]
        [Summary("Gets a worldobject representing the nearest object matching objectclass, or 0 if none was found")]
        [Example("wobjectfindnearestbyobjectclass[24]", "Returns a worldobject of the nearest matching objectclass")]
        public object ExpressionWorldObjectfindnearestbyobjectclass(double objectClass) {
            WorldObject closest = null;
            var wos = UtilityBeltPlugin.Instance.Core.WorldFilter.GetByObjectClass((ObjectClass)Convert.ToInt32(objectClass));
            var closestDistance = double.MaxValue;
            foreach (var wo in wos) {
                if (wo.Id == UtilityBeltPlugin.Instance.Core.CharacterFilter.Id)
                    continue;
                if (PhysicsObject.GetDistance(wo.Id) < closestDistance) {
                    closest = wo;
                    closestDistance = PhysicsObject.GetDistance(wo.Id);
                }
            }
            wos.Dispose();

            if (closest != null)
                return new ExpressionWorldObject(closest.Id);

            return 0;
        }
        #endregion //wobjectfindnearestbyobjectclass[int objectclass]
        #region wobjectfindininventorybytemplatetype[int objectclass]
        [ExpressionMethod("wobjectfindininventorybytemplatetype")]
        [ExpressionParameter(0, typeof(double), "templatetype", "templatetype to filter by")]
        [ExpressionReturn(typeof(ExpressionWorldObject), "Returns a worldobject")]
        [Summary("Gets a worldobject representing the first inventory item matching template type, or 0 if none was found")]
        [Example("wobjectfindininventorybytemplatetype[9060]", "Returns a worldobject of the first inventory item that is a Titan Mana Charge (template type 9060)")]
        public object ExpressionWorldObjectfindininventorybytemplatetype(double templateType) {
            var wos = UtilityBeltPlugin.Instance.Core.WorldFilter.GetInventory();
            var typeInt = Convert.ToInt32(templateType);
            foreach (var wo in wos) {
                if (wo.Type == typeInt) {
                    var r = new ExpressionWorldObject(wo.Id);
                    wos.Dispose();
                    return r;
                }
            }
            wos.Dispose();

            return 0;
        }
        #endregion //wobjectfindininventorybytemplatetype[int objectclass]
        #region wobjectfindininventorybyname[string name]
        [ExpressionMethod("wobjectfindininventorybyname")]
        [ExpressionParameter(0, typeof(string), "name", "exact name to filter by")]
        [ExpressionReturn(typeof(ExpressionWorldObject), "Returns a worldobject")]
        [Summary("Gets a worldobject representing the first inventory item matching an exact name, or 0 if none was found")]
        [Example("wobjectfindininventorybyname[Massive Mana Charge]", "Returns a worldobject of the first inventory item that is named `Massive Mana Charge`")]
        public object ExpressionWorldObjectfindininventorybyname(string name) {
            List<int> weenies = new List<int>();
            UBHelper.InventoryManager.GetInventory(ref weenies, UBHelper.InventoryManager.GetInventoryType.Everything, Weenie.INVENTORY_LOC.ALL_LOC);

            foreach (var id in weenies) {
                if (Util.GetObjectName(id) == name) {
                    return new ExpressionWorldObject(id);
                }
            }
            return 0;
        }
        #endregion //wobjectfindininventorybyname[string name]
        #region wobjectfindininventorybynamerx[string namerx]
        [ExpressionMethod("wobjectfindininventorybynamerx")]
        [ExpressionParameter(0, typeof(string), "namerx", "name regex to filter by")]
        [ExpressionReturn(typeof(ExpressionWorldObject), "Returns a worldobject")]
        [Summary("Gets a worldobject representing the first inventory item matching name regex, or 0 if none was found")]
        [Example("wobjectfindininventorybynamerx[`Massive.*`]", "Returns a worldobject of the first inventory item that matches regex `Massive.*`")]
        public object ExpressionWorldObjectfindininventorybynamerx(string namerx) {
            var re = new Regex(namerx);
            List<int> weenies = new List<int>();
            UBHelper.InventoryManager.GetInventory(ref weenies, UBHelper.InventoryManager.GetInventoryType.Everything, Weenie.INVENTORY_LOC.ALL_LOC);

            foreach (var id in weenies) {
                if (re.IsMatch(Util.GetObjectName(id))) {
                    return new ExpressionWorldObject(id);
                }
            }

            return 0;
        }
        #endregion //wobjectfindininventorybynamerx[string name]
        #region wobjectgetselection[]
        [ExpressionMethod("wobjectgetselection")]
        [ExpressionReturn(typeof(ExpressionWorldObject), "Returns a worldobject representing the currently selected object, or 0 if none")]
        [Summary("Gets a worldobject representing the currently selected object")]
        [Example("wobjectgetselection[]", "Returns a worldobject representing the currently selected object")]
        public object ExpressionWorldObjectgetselection() {
            if (UtilityBeltPlugin.Instance.Core.Actions.IsValidObject(UtilityBeltPlugin.Instance.Core.Actions.CurrentSelection))
                return new ExpressionWorldObject(UtilityBeltPlugin.Instance.Core.Actions.CurrentSelection);
            return false;
        }
        #endregion //wobjectgetselection[]
        #region wobjectgetplayer[]
        [ExpressionMethod("wobjectgetplayer")]
        [ExpressionReturn(typeof(ExpressionWorldObject), "Returns a worldobject representing the current player")]
        [Summary("Gets a worldobject representing the current player")]
        [Example("wobjectgetplayer[]", "Returns a worldobject representing the current player")]
        public object ExpressionWorldObjectgetplayer() {
            return new ExpressionWorldObject(UtilityBeltPlugin.Instance.Core.CharacterFilter.Id);
        }
        #endregion //wobjectgetplayer[worldobject wo]
        #region wobjectfindnearestbynameandobjectclass[int objectclass, string namerx]
        [ExpressionMethod("wobjectfindnearestbynameandobjectclass")]
        [ExpressionParameter(0, typeof(double), "objectclass", "objectclass to filter by")]
        [ExpressionParameter(1, typeof(string), "namerx", "name regex to filter by")]
        [ExpressionReturn(typeof(ExpressionWorldObject), "Returns a worldobject")]
        [Summary("Gets a worldobject representing the first object matching objectclass and name regex, or 0 if none was found")]
        [Example("wobjectfindnearestbynameandobjectclass[24,`Crash.*`]", "Returns a worldobject of the first object found matching objectlass 24 (player) and name regex `Crash.*`")]
        public object ExpressionWorldObjectfindnearestbynameandobjectclass(double objectClass, string namerx) {
            var wos = UtilityBeltPlugin.Instance.Core.WorldFilter.GetByObjectClass((ObjectClass)Convert.ToInt32(objectClass));
            var re = new Regex(namerx);
            var closestDistance = double.MaxValue;
            WorldObject closest = null;
            foreach (var wo in wos) {
                if (wo.Id == UtilityBeltPlugin.Instance.Core.CharacterFilter.Id)
                    continue;
                if (re.IsMatch(wo.Name) && PhysicsObject.GetDistance(wo.Id) < closestDistance) {
                    closest = wo;
                    closestDistance = PhysicsObject.GetDistance(wo.Id);
                }
            }

            wos.Dispose();

            if (closest != null)
                return new ExpressionWorldObject(closest.Id);

            return 0;
        }
        #endregion //wobjectfindnearestbynameandobjectclass[int objectclass, string namerx]
        #region wobjectfindbyid[int id]
        [ExpressionMethod("wobjectfindbyid")]
        [ExpressionParameter(0, typeof(double), "id", "object id to find")]
        [ExpressionReturn(typeof(ExpressionWorldObject), "Returns a worldobject")]
        [Summary("Gets a worldobject representing the object with the passed id")]
        [Example("wobjectfindbyid[123412]", "Returns a worldobject of the object with id 123412")]
        public object ExpressionWorldObjectfindbyid(double id) {
            var idInt = Convert.ToInt32(id);
            if (UB.Core.Actions.IsValidObject(idInt)) {
                var wo = UB.Core.WorldFilter[idInt];
                if (wo != null)
                    return new ExpressionWorldObject(wo.Id);
            }

            return 0;
        }
        #endregion //wobjectfindbyid[int id]
        #region wobjectgetopencontainer[]
        [ExpressionMethod("wobjectgetopencontainer")]
        [ExpressionReturn(typeof(ExpressionWorldObject), "Returns a worldobject representing the currently opened container")]
        [Summary("Gets a worldobject representing the currentlty opened container, or 0 if none")]
        [Example("wobjectgetopencontainer[]", "Returns a worldobject representing the currently opened container")]
        public object ExpressionWorldObjectgetopencontainer() {
            if (UB.Core.Actions.OpenedContainer == 0)
                return 0;
            return new ExpressionWorldObject(UB.Core.Actions.OpenedContainer);
        }
        #endregion //wobjectgetopencontainer[]
        #region getfreeitemslots[worldobject? container]
        [ExpressionMethod("getfreeitemslots")]
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "container", "Optional container to get free item slots of, defaulting to player.", null)]
        [ExpressionReturn(typeof(double), "Returns the number of free slots of container, or -1 if the WorldObject was invalid")]
        [Summary("Gets the number of free item slots in a container")]
        [Example("getfreeitemslots[]", "Returns the free item slots of the player")]
        [Example("getfreeitemslots[wobjectgetselection[]]", "Returns the free item slots of the selected object")]
        public double Getfreeitemslots(ExpressionWorldObject container = null) {
            //Check if container was left blank or is explicitly the player
            if (container is null || container.Id == UB.Core.CharacterFilter.Id)
                return new Weenie(UB.Core.CharacterFilter.Id).FreeSpace;
            else if (container.Wo.ObjectClass == ObjectClass.Container)
                return new Weenie(container.Id).FreeSpace;
            return -1;
        }
        #endregion getfreeitemslots[worldobject? container]
        #region getfreecontainerslots[worldobject? container]
        [ExpressionMethod("getfreecontainerslots")]
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "container", "Optional container to get free item slots of, defaulting to player.", null)]
        [ExpressionReturn(typeof(double), "Returns the number of free container slots, or -1 if the WorldObject was invalid")]
        [Summary("Gets the number of free container slots")]
        [Example("getfreecontainerslots[]", "Returns the free container slots of the player")]
        [Example("getfreecontainerslots[wobjectgetselection[]]", "Returns the free container slots of the selected object")]
        public double Getfreecontainerslots(ExpressionWorldObject container = null) {
            var w = new Weenie((container is null || container.Id == UB.Core.CharacterFilter.Id) ? UB.Core.CharacterFilter.Id : container.Id);

            if (w.ObjectClass != ObjectClass.Container && w.ObjectClass != ObjectClass.Player)
                return -1;

            return w.ContainersCapacity - w.ContainersContained;
        }
        #endregion getfreecontainerslots[worldobject? container]
        #region getcontaineritemcount[worldobject? container]
        [ExpressionMethod("getcontaineritemcount")]
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "container", "Optional container to get number of items in, defaulting to player.", null)]
        [ExpressionReturn(typeof(double), "Returns the number of items in a container, or -1 if the WorldObject was invalid")]
        [Summary("Gets the number of items in a container")]
        [Example("getcontaineritemcount[]", "Returns the used container slots of the player")]
        [Example("getcontaineritemcount[wobjectgetselection[]]", "Returns the number of items in the selected item")]
        public double getcontaineritemcount(ExpressionWorldObject container = null) {
            //Check if container was left blank or is explicitly the player
            if (container is null || container.Id == UB.Core.CharacterFilter.Id)
                return new Weenie(UB.Core.CharacterFilter.Id).ItemsContained;
            else if (container.Wo.ObjectClass == ObjectClass.Container)
                return new Weenie(container.Id).ItemsContained;
            return -1;
        }
        #endregion getcontaineritemcount[worldobject? container]
        #region wobject find lists
        #region wobjectfindall[]
        [ExpressionMethod("wobjectfindall")]
        [ExpressionReturn(typeof(ExpressionList), "Returns a list of *all* worldobjects")]
        [Summary("Gets a list of all worldobjects known by the client")]
        [Example("wobjectfindall[]", "Returns a list of *all* wobjects")]
        public object ExpressionWorldObjectfindall() {
            var list = new ExpressionList();
            var wos = UB.Core.WorldFilter.GetAll();
            foreach (var wo in wos) {
                if (UB.Core.Actions.IsValidObject(wo.Id)) {
                    list.Items.Add(new ExpressionWorldObject(wo.Id));
                }
            }
            wos.Dispose();

            return list;
        }
        #endregion //wobjectfindall[]
        #region wobjectfindallinventory[]
        [ExpressionMethod("wobjectfindallinventory")]
        [ExpressionReturn(typeof(ExpressionList), "Returns a list of all worldobjects in the player inventory")]
        [Summary("Gets a list of all worldobjects in the players inventory")]
        [Example("wobjectfindallinventory[]", "Returns a list of all your inventory wobjects")]
        public object ExpressionWorldObjectfindallinventory() {
            var list = new ExpressionList();
            var wos = UB.Core.WorldFilter.GetInventory();
            foreach (var wo in wos) {
                if (UB.Core.Actions.IsValidObject(wo.Id)) {
                    list.Items.Add(new ExpressionWorldObject(wo.Id));
                }
            }
            wos.Dispose();

            return list;
        }
        #endregion //wobjectfindallinventory[]
        #region wobjectfindalllandscape[]
        [ExpressionMethod("wobjectfindalllandscape")]
        [ExpressionReturn(typeof(ExpressionList), "Returns a list of all worldobjects in the landscape")]
        [Summary("Gets a list of all worldobjects in the landscape")]
        [Example("wobjectfindalllandscape[]", "Returns a list of all landscape wobjects")]
        public object ExpressionWorldObjectfindalllandscape() {
            var list = new ExpressionList();
            var wos = UB.Core.WorldFilter.GetLandscape();
            foreach (var wo in wos) {
                if (UB.Core.Actions.IsValidObject(wo.Id)) {
                    list.Items.Add(new ExpressionWorldObject(wo.Id));
                }
            }
            wos.Dispose();

            return list;
        }
        #endregion //wobjectfindalllandscape[]
        #region wobjectfindallbyobjectclass[int objectclass]
        [ExpressionMethod("wobjectfindallbyobjectclass")]
        [ExpressionParameter(0, typeof(double), "objectclass", "objectclass to filter by")]
        [ExpressionReturn(typeof(ExpressionList), "Returns a list of all matching worldobjects")]
        [Summary("Gets a list of all worldobjects of the passed objectclass")]
        [Example("wobjectfindallbyobjectclass[24]", "Returns a list of all players the client is aware of")]
        public object ExpressionWorldObjectfindallbyobjectclass(double objectClass) {
            var list = new ExpressionList();
            var wos = UB.Core.WorldFilter.GetByObjectClass((ObjectClass)Convert.ToInt32(objectClass));
            foreach (var wo in wos) {
                if (UB.Core.Actions.IsValidObject(wo.Id)) {
                    list.Items.Add(new ExpressionWorldObject(wo.Id));
                }
            }
            wos.Dispose();

            return list;
        }
        #endregion //wobjectfindallbyobjectclass[int objectclass]
        #region wobjectfindallbytemplatetype[int templatetype]
        [ExpressionMethod("wobjectfindallbytemplatetype")]
        [ExpressionParameter(0, typeof(double), "templatetype", "templatetype to filter by")]
        [ExpressionReturn(typeof(ExpressionList), "Returns a list of all matching worldobjects")]
        [Summary("Gets a list of all wobjects matching templatetype")]
        [Example("wobjectfindallbytemplatetype[9060]", "Returns a list of worldobjects that are a Titan Mana Charge (template type 9060)")]
        public object ExpressionWorldObjectfindallbytemplatetype(double templateType) {
            var list = new ExpressionList();
            var wos = UtilityBeltPlugin.Instance.Core.WorldFilter.GetAll();
            var typeInt = Convert.ToInt32(templateType);
            foreach (var wo in wos) {
                if (wo.Type == typeInt && UB.Core.Actions.IsValidObject(wo.Id)) {
                    list.Items.Add(new ExpressionWorldObject(wo.Id));
                }
            }
            wos.Dispose();

            return list;
        }
        #endregion //wobjectfindallbytemplatetype[int templatetype]
        #region wobjectfindallbynamerx[string namerx]
        [ExpressionMethod("wobjectfindallbynamerx")]
        [ExpressionParameter(0, typeof(string), "namerx", "regular expression to filter name by")]
        [ExpressionReturn(typeof(ExpressionList), "Returns a list of all matching worldobjects")]
        [Summary("Gets a list of all wobjects matching the passed regular expression")]
        [Example("wobjectfindallbynamerx[`Crash.*`]", "Returns a list of all worldobjects that match the regex `Crash.*`")]
        public object ExpressionWorldObjectfindallbynamerx(string namerx) {
            var re = new Regex(namerx);
            var list = new ExpressionList();
            var wos = UtilityBeltPlugin.Instance.Core.WorldFilter.GetAll();
            foreach (var wo in wos) {
                if (re.IsMatch(Util.GetObjectName(wo.Id)) && UB.Core.Actions.IsValidObject(wo.Id)) {
                    list.Items.Add(new ExpressionWorldObject(wo.Id));
                }
            }
            wos.Dispose();

            return list;
        }
        #endregion //wobjectfindallbynamerx[string namerx]
        #region wobjectfindallinventorybyobjectclass[int objectclass]
        [ExpressionMethod("wobjectfindallinventorybyobjectclass")]
        [ExpressionParameter(0, typeof(double), "objectclass", "objectclass to filter by")]
        [ExpressionReturn(typeof(ExpressionList), "Returns a list of all matching worldobjects")]
        [Summary("Gets a list of all inventory worldobjects of the passed objectclass")]
        [Example("wobjectfindallinventorybyobjectclass[16]", "Returns a list of all mana stones in the players inventory")]
        public object ExpressionWorldObjectfindallinventorybyobjectclass(double objectClass) {
            var oc = (ObjectClass)Convert.ToInt32(objectClass);
            var list = new ExpressionList();
            var wos = UB.Core.WorldFilter.GetInventory();
            foreach (var wo in wos) {
                if (wo.ObjectClass == oc && UB.Core.Actions.IsValidObject(wo.Id)) {
                    list.Items.Add(new ExpressionWorldObject(wo.Id));
                }
            }
            wos.Dispose();

            return list;
        }
        #endregion //wobjectfindallinventorybyobjectclass[int objectclass]
        #region wobjectfindallinventorybytemplatetype[int templatetype]
        [ExpressionMethod("wobjectfindallinventorybytemplatetype")]
        [ExpressionParameter(0, typeof(double), "templatetype", "templatetype to filter by")]
        [ExpressionReturn(typeof(ExpressionList), "Returns a list of all matching inventory worldobjects")]
        [Summary("Gets a list of all inventory items matching templatetype")]
        [Example("wobjectfindallinventorybytemplatetype[9060]", "Returns a list of inventory worldobjects that are a Titan Mana Charge (template type 9060)")]
        public object ExpressionWorldObjectfindallininventorybytemplatetype(double templateType) {
            var list = new ExpressionList();
            var wos = UtilityBeltPlugin.Instance.Core.WorldFilter.GetInventory();
            var typeInt = Convert.ToInt32(templateType);
            foreach (var wo in wos) {
                if (wo.Type == typeInt && UB.Core.Actions.IsValidObject(wo.Id)) {
                    list.Items.Add(new ExpressionWorldObject(wo.Id));
                }
            }
            wos.Dispose();

            return list;
        }
        #endregion //wobjectfindallinventorybytemplatetype[int templatetype]
        #region wobjectfindallinventorybynamerx[string namerx]
        [ExpressionMethod("wobjectfindallinventorybynamerx")]
        [ExpressionParameter(0, typeof(string), "namerx", "regular expression to filter name by")]
        [ExpressionReturn(typeof(ExpressionList), "Returns a list of all matching worldobjects")]
        [Summary("Gets a list of all inventory wobjects with name matching the passed regular expression")]
        [Example("wobjectfindallinventorybynamerx[`Crash.*`]", "Returns a list of all inventory worldobjects that match the regex `Crash.*`")]
        public object ExpressionWorldObjectfindallinventorybynamerx(string namerx) {
            var re = new Regex(namerx);
            var list = new ExpressionList();
            var wos = UtilityBeltPlugin.Instance.Core.WorldFilter.GetInventory();
            foreach (var wo in wos) {
                if (re.IsMatch(Util.GetObjectName(wo.Id)) && UB.Core.Actions.IsValidObject(wo.Id)) {
                    list.Items.Add(new ExpressionWorldObject(wo.Id));
                }
            }
            wos.Dispose();

            return list;
        }
        #endregion //wobjectfindallinventorybynamerx[string namerx]
        #region wobjectfindalllandscapebyobjectclass[int objectclass]
        [ExpressionMethod("wobjectfindalllandscapebyobjectclass")]
        [ExpressionParameter(0, typeof(double), "objectclass", "objectclass to filter by")]
        [ExpressionReturn(typeof(ExpressionList), "Returns a list of all matching worldobjects")]
        [Summary("Gets a list of all landscape worldobjects of the passed objectclass")]
        [Example("wobjectfindalllandscapebyobjectclass[16]", "Returns a list of all mana stones in the landscape")]
        public object ExpressionWorldObjectfindalllandscapebyobjectclass(double objectClass) {
            var oc = (ObjectClass)Convert.ToInt32(objectClass);
            var list = new ExpressionList();
            var wos = UB.Core.WorldFilter.GetLandscape();
            foreach (var wo in wos) {
                if (wo.ObjectClass == oc && UB.Core.Actions.IsValidObject(wo.Id)) {
                    list.Items.Add(new ExpressionWorldObject(wo.Id));
                }
            }
            wos.Dispose();

            return list;
        }
        #endregion //wobjectfindalllandscapebyobjectclass[int objectclass]
        #region wobjectfindalllandscapebytemplatetype[int templatetype]
        [ExpressionMethod("wobjectfindalllandscapebytemplatetype")]
        [ExpressionParameter(0, typeof(double), "templatetype", "templatetype to filter by")]
        [ExpressionReturn(typeof(ExpressionList), "Returns a list of all matching worldobjects")]
        [Summary("Gets a list of all landscape items matching templatetype")]
        [Example("wobjectfindalllandscapebytemplatetype[9060]", "Returns a list of landscape worldobjects that are a Titan Mana Charge (template type 9060)")]
        public object ExpressionWorldObjectfindalllandscapebytemplatetype(double templateType) {
            var list = new ExpressionList();
            var wos = UtilityBeltPlugin.Instance.Core.WorldFilter.GetLandscape();
            var typeInt = Convert.ToInt32(templateType);
            foreach (var wo in wos) {
                if (wo.Type == typeInt && UB.Core.Actions.IsValidObject(wo.Id)) {
                    list.Items.Add(new ExpressionWorldObject(wo.Id));
                }
            }
            wos.Dispose();

            return list;
        }
        #endregion //wobjectfindalllandscapebytemplatetype[int templatetype]
        #region wobjectfindalllandscapebynamerx[string namerx]
        [ExpressionMethod("wobjectfindalllandscapebynamerx")]
        [ExpressionParameter(0, typeof(string), "namerx", "regular expression to filter name by")]
        [ExpressionReturn(typeof(ExpressionList), "Returns a list of all matching worldobjects")]
        [Summary("Gets a list of all landscape wobjects with name matching the passed regular expression")]
        [Example("wobjectfindalllandscapebynamerx[`Crash.*`]", "Returns a list of all landscape worldobjects that match the regex `Crash.*`")]
        public object ExpressionWorldObjectfindalllandscapebynamerx(string namerx) {
            var re = new Regex(namerx);
            var list = new ExpressionList();
            var wos = UtilityBeltPlugin.Instance.Core.WorldFilter.GetLandscape();
            foreach (var wo in wos) {
                if (re.IsMatch(Util.GetObjectName(wo.Id)) && UB.Core.Actions.IsValidObject(wo.Id)) {
                    list.Items.Add(new ExpressionWorldObject(wo.Id));
                }
            }
            wos.Dispose();

            return list;
        }
        #endregion //wobjectfindalllandscapebynamerx[string namerx]
        #region wobjectfindallbycontainer[object container]
        [ExpressionMethod("wobjectfindallbycontainer")]
        [ExpressionParameter(0, typeof(object), "container", "container to find objects in. can be an id or a wobject")]
        [ExpressionReturn(typeof(ExpressionList), "Returns a list of worldobjects")]
        [Summary("Gets a list of all worldobjects inside the specified container")]
        [Example("wobjectfindallbycontainer[wobjectgetselection[]]", "Returns a list of all wobjects in the currently selected container")]
        [Example("wobjectfindallbycontainer[0x1234ABCD]", "Returns a list of all wobjects in the container with id 0x1234ABCD")]
        public object ExpressionWorldObjectfindallbycontainer(object container) {
            var list = new ExpressionList();
            var id = container is ExpressionWorldObject cwo ? cwo.Id : Convert.ToInt32(container);
            var wos = UB.Core.WorldFilter.GetByContainer(id);
            foreach (var wo in wos) {
                if (UB.Core.Actions.IsValidObject(wo.Id)) {
                    list.Items.Add(new ExpressionWorldObject(wo.Id));
                }
            }
            wos.Dispose();

            return list;
        }
        #endregion //wobjectfindallbycontainer[object container]
        #endregion //wobject find lists
        #endregion //WorldObjects
        #region Actions
        #region actiontryselect[wobject obj]
        [ExpressionMethod("actiontryselect")]
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "obj", "wobject to select")]
        [ExpressionReturn(typeof(double), "Returns 0")]
        [Summary("Attempts to select a worldobject")]
        [Example("actiontryselect[wobjectgetplayer[]]", "Attempts to select the current player")]
        public object Actiontryselect(ExpressionWorldObject obj) {
            UtilityBeltPlugin.Instance.Core.Actions.SelectItem(obj.Wo.Id);
            return 0;
        }
        #endregion //actiontryselect[wobject obj]
        #region actiontryuseitem[wobject obj]
        [ExpressionMethod("actiontryuseitem")]
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "obj", "wobject to try to use")]
        [ExpressionReturn(typeof(double), "Returns 0")]
        [Summary("Attempts to use a worldobject")]
        [Example("actiontryuseitem[wobjectgetplayer[]]", "Attempts to use the current player (opens backpack)")]
        public object Actiontryuseitem(ExpressionWorldObject obj) {
            UtilityBeltPlugin.Instance.Core.Actions.UseItem(obj.Wo.Id, 0);
            return 0;
        }
        #endregion //actiontryuseitem[wobject obj]
        #region actiontryapplyitem[wobject useObj, wobject onObj]
        [ExpressionMethod("actiontryapplyitem")]
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "useObj", "wobject to use first")]
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "onObj", "wobject to be used on")]
        [ExpressionReturn(typeof(double), "Returns 0 if failed, and 1 if it *could* succeed")]
        [Summary("Attempts to use a worldobject on another worldobject")]
        [Example("actiontryapplyitem[wobjectfindininventorybynamerx[`.* Healing Kit`],wobjectwobjectgetplayer[]]", "Attempts to use any healing kit in your inventory, on yourself")]
        public object Actiontryapplyitem(ExpressionWorldObject useObj, ExpressionWorldObject onObj) {
            if (UtilityBeltPlugin.Instance.Core.Actions.BusyState != 0)
                return 0;
            if (useObj.Wo.ObjectClass == ObjectClass.WandStaffOrb && UtilityBeltPlugin.Instance.Core.Actions.CombatMode != CombatState.Magic)
                return 0;
            UtilityBeltPlugin.Instance.Core.Actions.SelectItem(onObj.Id);
            if (useObj.Wo.ObjectClass == ObjectClass.WandStaffOrb)
                UtilityBeltPlugin.Instance.Core.Actions.UseItem(useObj.Wo.Id, onObj.Wo.Id);
            else
                UtilityBeltPlugin.Instance.Core.Actions.ApplyItem(useObj.Wo.Id, onObj.Wo.Id);
            return 1;
        }
        #endregion //actiontryapplyitem[wobject useObj, wobject onObj]
        #region actiontrygiveitem[wobject item, wobject destination]
        [ExpressionMethod("actiontrygiveitem")]
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "give", "wobject to give")]
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "destination", "wobject to be given to")]
        [ExpressionReturn(typeof(double), "Returns 0 if failed, and 1 if it *could* succeed")]
        [Summary("Attempts to give a worldobject to another worlobject, like an npc")]
        [Example("actiontrygiveitem[wobjectfindininventorybynamerx[`.* Healing Kit`],wobjectgetselection[]]", "Attempts to to give any healing kit in your inventory to the currently selected object")]
        public object Actiontrygiveitem(ExpressionWorldObject give, ExpressionWorldObject destination) {
            if (UtilityBeltPlugin.Instance.Core.Actions.BusyState != 0)
                return 0;
            UtilityBeltPlugin.Instance.Core.Actions.GiveItem(give.Wo.Id, destination.Wo.Id);
            return 1;
        }
        #endregion //actiontrygiveitem[wobject item, wobject destination]
        #region actiontryequipanywand[]
        [ExpressionMethod("actiontryequipanywand")]
        [ExpressionReturn(typeof(double), "Returns 1 if a wand is already equipped, 0 otherwise")]
        [Summary("Attempts to take one step towards equipping any wand from the current profile's items list")]
        [Example("actiontryequipanywand[]", "Attempts to equip any wand")]
        public object Actiontryequipanywand() {
            return VTankExtensions.TryEquipAnyWand();
        }
        #endregion //actiontryequipanywand[]
        #region actiontrycastbyid[int spellId]
        [ExpressionMethod("actiontrycastbyid")]
        [ExpressionParameter(0, typeof(double), "spellId", "spellId to cast")]
        [ExpressionReturn(typeof(double), "Returns 1 if the attempt has begun, 0 if the attempt has not yet been made, or 2 if the attempt is impossible")]
        [Summary("Attempts to cast a spell by id. Checks spell requirements as if it were a vtank 'hunting' spell. If the character is not in magic mode, one step is taken towards equipping any wand")]
        [Example("actiontrycastbyid[2931]", "Attempts to cast Recall Aphus Lassel")]
        public object Actiontrycastbyid(double spellId) {
            var id = Convert.ToInt32(spellId);
            if (!Spells.IsKnown(id) || !Spells.HasSkillHunt(id))
                return 2;
            if (!VTankExtensions.TryEquipAnyWand())
                return 0;

            // this uses vtanks spell casting system so that peace while idle registers the action properly
            var spell = UBHelper.vTank.Instance.SpellSystem_GetSpellById(id);
            if (spell == null)
                return 2;

            UBHelper.vTank.Instance.SpellSystem_CastNormalSpell(spell, 0);

            return 1;
        }
        #endregion //actiontrycastbyid[int spellId]
        #region actiontrycastbyidontarget[int spellId, wobject target]
        [ExpressionMethod("actiontrycastbyidontarget")]
        [ExpressionParameter(0, typeof(double), "spellId", "spellId to cast")]
        [ExpressionParameter(0, typeof(ExpressionWorldObject), "target", "target to cast on")]
        [ExpressionReturn(typeof(double), "Returns 1 if the attempt has begun, 0 if the attempt has not yet been made, or 2 if the attempt is impossible")]
        [Summary("Attempts to cast a spell by id on a worldobject. Checks spell requirements as if it were a vtank 'hunting' spell. If the character is not in magic mode, one step is taken towards equipping any wand")]
        [Example("actiontrycastbyidontarget[1,wobjectgetselection[]]", "Attempts to cast Strength Other, on your currently selected target")]
        public object Actiontrycastbyidontarget(double spellId, ExpressionWorldObject target) {
            var id = Convert.ToInt32(spellId);
            if (!Spells.HasSkillHunt(id))
                return 2;
            if (!VTankExtensions.TryEquipAnyWand())
                return 0;

            // this uses vtanks spell casting system so that peace while idle registers the action properly
            var spell = UBHelper.vTank.Instance.SpellSystem_GetSpellById(id);
            if (spell == null)
                return 2;

            UBHelper.vTank.Instance.SpellSystem_CastNormalSpell(spell, target.Wo.Id);

            return 1;
        }
        #endregion //actiontrycastbyidontarget[int spellId, wobject target]
        #endregion //Actions
        #region HUDs
        #region statushud[string key, string value]
        [ExpressionMethod("statushud")]
        [ExpressionParameter(0, typeof(string), "key", "key to update")]
        [ExpressionParameter(0, typeof(string), "value", "value to update with")]
        [ExpressionReturn(typeof(double), "Returns 1")]
        [Summary("Updates an entry in the Virindi HUDs Status HUD.")]
        [Example("statushud[test,my value]", "Updates the V Status Hud key `test` to `my value`")]
        public object Statushud(string key, string value) {
            // TODO: ub huds
            Type vhud = typeof(uTank2.PluginCore).Assembly.GetType("aw");
            var updateKeyMethod = vhud.GetMethod("b", BindingFlags.Public | BindingFlags.Static, null, CallingConventions.Standard, new Type[] { typeof(string), typeof(string), typeof(string) }, null);
            updateKeyMethod.Invoke(null, new object[] { "VTank Meta", key, value });
            return 1;
        }
        #endregion //statushud[string key, string value]
        #region statushudcolored[string key, string value, int color]
        [ExpressionMethod("statushudcolored")]
        [ExpressionParameter(0, typeof(string), "key", "key to update")]
        [ExpressionParameter(0, typeof(string), "value", "value to update with")]
        [ExpressionParameter(0, typeof(double), "color", "The color, in RGB number format. For example, pure red is 16711680 (0xFF0000)")]
        [ExpressionReturn(typeof(double), "Returns 1")]
        [Summary("Updates an entry in the Virindi HUDs Status HUD with color")]
        [Example("statushudcolored[test,my value,16711680]", "Updates the V Status Hud key `test` to `my value` in red")]
        public object Statushudcolored(string key, string value, double color) {
            // TODO: ub huds
            Type vhud = typeof(uTank2.PluginCore).Assembly.GetType("aw");
            var updateKeyMethod = vhud.GetMethod("b", BindingFlags.Public | BindingFlags.Static, null, CallingConventions.Standard, new Type[] { typeof(string), typeof(string), typeof(string), typeof(Color) }, null);
            var c = Convert.ToInt32(color);
            var r = (0xFF0000 & c) >> 0x10;
            var g = (0x00FF00 & c) >> 0x08;
            var b = (0x0000FF & c) >> 0x00;
            updateKeyMethod.Invoke(null, new object[] { "VTank Meta", key, value, Color.FromArgb(0xFF, r, g, b) });

            return 1;
        }
        #endregion //statushudcolored[string key, string value, int color]
        #endregion //HUDs
        #region Misc
        #region isfalse[int value]
        [ExpressionMethod("isfalse")]
        [ExpressionParameter(0, typeof(object), "value", "value to check")]
        [ExpressionReturn(typeof(double), "Returns 1 if value is false, 0 otherwise")]
        [Summary("Checks if a value is equal to false (0)")]
        [Example("isfalse[0]", "Checks that 0 is false, and returns true because it is")]
        public object Isfalse(object value) {
            if (value.GetType() == typeof(double))
                return ((double)value).Equals(0);
            else if (value.GetType() == typeof(Boolean))
                return ((bool)value).Equals(false);

            return false;
        }
        #endregion //isfalse[int value]
        #region istrue[int value]
        [ExpressionMethod("istrue")]
        [ExpressionParameter(0, typeof(object), "value", "value to check")]
        [ExpressionReturn(typeof(double), "Returns 1 if value is true, 0 otherwise")]
        [Summary("Checks if a value is equal to true (1)")]
        [Example("istrue[1]", "Checks that 0 is true, and returns true because it is")]
        public object Istrue(object value) {
            return ExpressionVisitor.IsTruthy(value) ? 1 : 0;
        }
        #endregion //istrue[int value]
        #region iif[int value, object truevalue, object falsevalue]
        [ExpressionMethod("iif")]
        [ExpressionParameter(0, typeof(object), "value", "value to check")]
        [ExpressionParameter(0, typeof(object), "truevalue", "value to return if value is true")]
        [ExpressionParameter(0, typeof(object), "falsevalue", "value to return if value is false")]
        [ExpressionReturn(typeof(double), "Returns 1 if value is true, 0 otherwise")]
        [Summary("Checks if the first parameter is true, if so returns the second argument.  If the first parameter is false or not a number, returns the third argument.  Both arguments will always be evaluated.  if you want conditional evaluation use `if[]`")]
        [Example("iif[1,2,3]", "Returns 2 (second param) because 1 (first param) is true")]
        public object Iif(object value, object truevalue, object falsevalue) {
            return ExpressionVisitor.IsTruthy(value) ? truevalue : falsevalue;
        }
        #endregion //iif[int value, any truevalue, any falsevalue]
        #region randint[int min, int max]
        [ExpressionMethod("randint")]
        [ExpressionParameter(0, typeof(double), "min", "minimum value to return")]
        [ExpressionParameter(0, typeof(double), "max", "maximum value to return (-1)")]
        [ExpressionReturn(typeof(double), "Returns a number between min and (max-1)")]
        [Summary("Generates a random number between min and (max-1)")]
        [Example("randint[0,2]", "Returns a random number, 0 or 1, but not 2")]
        public object Randint(double min, double max) {
            return rnd.Next(Convert.ToInt32(min), Convert.ToInt32(max));
        }
        #endregion //randint[int min, int max]
        #region cstr[int number]
        [ExpressionMethod("cstr")]
        [ExpressionParameter(0, typeof(double), "number", "number to convert")]
        [ExpressionReturn(typeof(string), "Returns a string representation of number")]
        [Summary("Converts a number to a string")]
        [Example("cstr[2]", "Returns a string of `2`")]
        public object Cstr(double number) {
            return number.ToString();
        }
        #endregion //cstr[int number]
        #region strlen[string tocheck]
        [ExpressionMethod("strlen")]
        [ExpressionParameter(0, typeof(string), "tocheck", "string to check")]
        [ExpressionReturn(typeof(double), "Returns the length of tocheck string")]
        [Summary("Gets the length of a string")]
        [Example("strlen[test]", "Returns a length of 4")]
        public object Strlen(string tocheck) {
            return tocheck.Length;
        }
        #endregion //strlen[string tocheck]
        #region getobjectinternaltype[object tocheck]
        [ExpressionMethod("getobjectinternaltype")]
        [ExpressionParameter(0, typeof(object), "tocheck", "object to check")]
        [ExpressionReturn(typeof(double), "Values are: 0=none, 1=number, 3=string, 7=object")]
        [Summary("Gets internal type of an object, as a number")]
        [Example("getobjectinternaltype[test]", "Returns a length of 4")]
        public object Getobjectinternaltype(object tocheck) {
            if (tocheck == null)
                return 0;
            if (tocheck.GetType() == typeof(double))
                return 1;
            if (tocheck.GetType() == typeof(string))
                return 3;
            if (tocheck.GetType() == typeof(ExpressionWorldObject) && ((ExpressionWorldObject)tocheck).Wo == null)
                return 0;

            return 7;
        }
        #endregion //getobjectinternaltype[object tocheck]
        #region cstrf[int number, string format]
        [ExpressionMethod("cstrf")]
        [ExpressionParameter(0, typeof(double), "number", "number to convert")]
        [ExpressionParameter(1, typeof(string), "format", "string format. See: http://msdn.microsoft.com/en-us/library/kfsatb94.aspx")]
        [ExpressionReturn(typeof(string), "formatted string, from a number")]
        [Summary("Converts a number to a string using a specified format")]
        [Example("cstrf[3.14159,`N3`]", "Formats 3.14159 to a string with 3 decimal places")]
        public object Cstrf(double number, string format) {
            if (format.Contains("X"))
                return ((uint)number).ToString(format);
            else
                return number.ToString(format);
        }
        #endregion //cstrf[int number, string format]
        #region cnumber[string number]
        [ExpressionMethod("cnumber")]
        [ExpressionParameter(0, typeof(string), "number", "string number to convert")]
        [ExpressionReturn(typeof(double), "floating point number from a string")]
        [Summary("Converts a string to a floating point number")]
        [Example("cnumber[`3.14159`]", "Converts `3.14159` to a number")]
        public object Cnumber(string number) {
            if (Double.TryParse(number, out double result))
                return result;
            return 0;
        }
        #endregion //cnumber[string number]
        #region floor[int number]
        [ExpressionMethod("floor")]
        [ExpressionParameter(0, typeof(double), "number", "number to floot")]
        [ExpressionReturn(typeof(double), "Returns a whole number")]
        [Summary("returns the largest integer less than or equal to a given number.")]
        [Example("floor[3.14159]", "returns 3")]
        public object Floor(double number) {
            return Math.Floor(number);
        }
        #endregion //floor[int number]
        #region ceiling[int number]
        [ExpressionMethod("ceiling")]
        [ExpressionParameter(0, typeof(double), "number", "number to ceil")]
        [ExpressionReturn(typeof(double), "Returns a whole number")]
        [Summary("rounds a number up to the next largest whole number or integer")]
        [Example("ceiling[3.14159]", "returns 3")]
        public object Ceiling(double number) {
            return Math.Ceiling(number);
        }
        #endregion //ceiling[int number]
        #region round[int number]
        [ExpressionMethod("round")]
        [ExpressionParameter(0, typeof(double), "number", "number to round")]
        [ExpressionReturn(typeof(double), "Returns a whole number")]
        [Summary("returns the value of a number rounded to the nearest integer")]
        [Example("round[3.14159]", "returns 3")]
        public object Round(double number) {
            return Math.Round(number);
        }
        #endregion //round[int number]
        #region abs[int number]
        [ExpressionMethod("abs")]
        [ExpressionParameter(0, typeof(double), "number", "number to get absolute value of")]
        [ExpressionReturn(typeof(double), "Returns a positive number")]
        [Summary("Returns the absolute value of a number")]
        [Example("abs[-3.14159]", "returns 3.14159")]
        public object Abs(double number) {
            return Math.Abs(number);
        }
        #endregion //abs[int number]
        #region vtsetmetastate[string state]
        [ExpressionMethod("vtsetmetastate")]
        [ExpressionParameter(0, typeof(string), "state", "new state to switch to")]
        [ExpressionReturn(typeof(double), "Returns 1")]
        [Summary("Changes the current vtank meta state")]
        [Example("vtsetmetastate[myState]", "sets vtank meta state to `myState`")]
        public object Vtsetmetastate(string state) {
            Util.Decal_DispatchOnChatCommand($"/vt setmetastate {state}");
            return 1;
        }
        #endregion //vtsetmetastate[string state]
        #region vtgetmetastate[]
        [ExpressionMethod("vtgetmetastate")]
        [ExpressionReturn(typeof(string), "Returns the current vt meta state as a string")]
        [Summary("Gets the current vtank meta state as a string")]
        [Example("vtgetmetastate[]", "Returns the current vt meta state as a string")]
        public object Vtgetmetastate() {
            return UBHelper.vTank.Instance.CurrentMetaState;
        }
        #endregion //vtgetmetastate[]
        #region vtsetsetting[string setting, string value]
        [ExpressionMethod("vtsetsetting")]
        [ExpressionParameter(0, typeof(string), "setting", "setting to set the value of")]
        [ExpressionParameter(1, typeof(string), "setting", "value to set the setting to")]
        [ExpressionReturn(typeof(double), "Returns 0 if known to fail and 1 if it may have succeeded")]
        [Summary("Sets the specified vtank setting")]
        [Example("vtsetsetting[EnableCombat,`1`]", "Sets the vtank EnableCombat setting to true.  Any other number sets to false")]
        [Example("vtsetsetting[RingDistance,cstrf[cnumber[vtgetsetting[DoorOpenRange]],`N5`]", "Sets the vtank RingDistance setting to match the DoorOpenRange")]
        public object Vtsetsetting(string setting, string value) {
            try {
                var settingType = vTank.Instance.GetSettingType(setting);

                if (settingType == typeof(string)) {
                    vTank.Instance.SetSetting(setting, value.ToString());
                }
                else if (double.TryParse(value, out double number)) {
                    if (settingType == typeof(bool)) {
                        vTank.Instance.SetSetting(setting, (number == 1) ? true : false);
                    }
                    else if (settingType == typeof(double)) {
                        vTank.Instance.SetSetting(setting, number);
                    }
                    else if (settingType == typeof(int)) {
                        vTank.Instance.SetSetting(setting, Convert.ToInt32(number));
                    }
                    else if (settingType == typeof(float)) {
                        vTank.Instance.SetSetting(setting, Convert.ToSingle(number));
                    }
                }
                else {
                    //Fail here?
                    //Logger.WriteToChat($"Attempted to set a setting of unknown type: {settingType}");
                    return 0;
                }
            }
            //Known failures
            catch (FormatException ex) {
                return 0;
            }
            catch (InvalidCastException ex) {
                return 0;
            }
            catch (Exception ex) {
                //Eat the error thrown even on successes
            }
            return 1;
        }
        #endregion //vtsetsetting[string setting, string value]
        #region vtgetsetting[string setting]
        [ExpressionMethod("vtgetsetting")]
        [ExpressionParameter(0, typeof(string), "setting", "setting to get the state of")]
        [ExpressionReturn(typeof(string), "Returns the string value of the specific setting or an empty string if undefined")]
        [Summary("Gets the value of a vtank setting")]
        [Example("vtgetsetting[EnableCombat]", "Gets the value of the vtank EnableCombat setting")]
        [Example("cnumber[vtgetsetting[RingDistance]]", "Gets the number value of the vtank RingDistance setting")]
        public object Vtgetsetting(string setting) {
            //try {
            return UBHelper.vTank.Instance.GetSetting(setting);
            //}
            //catch (Exception ex) {
            //    return string.Empty;
            //}
        }
        #endregion //vtgetsetting[string setting]
        #region ord[string character]
        [ExpressionMethod("ord")]
        [ExpressionParameter(0, typeof(string), "character", "string to convert")]
        [ExpressionReturn(typeof(double), "number representing unicode character")]
        [Summary("returns an integer representing the Unicode character")]
        [Example("ord[c]", "Converts 'c' to a number representing its unicode character (99)")]
        public object Ord(string character) {
            return (double)(character.First());
        }
        #endregion //ord[string character]
        #region chr[number character]
        [ExpressionMethod("chr")]
        [ExpressionParameter(0, typeof(double), "character", "number to convert")]
        [ExpressionReturn(typeof(string), "string representing unicode character")]
        [Summary("returns a string representing a character whose Unicode code point is an integer.")]
        [Example("chr[99]", "Converts `99` to a strin representing its unicode character 'c'")]
        public object Chr(double character) {
            return ((char)character).ToString();
        }
        #endregion //chr[string character]
        #endregion //Misc
        #region ExpressionStopwatch
        #region stopwatchcreate[]
        [ExpressionMethod("stopwatchcreate")]
        [ExpressionReturn(typeof(ExpressionStopwatch), "Returns a stopwatch object")]
        [Summary("Creates a new stopwatch object.  The stopwatch object is stopped by default.")]
        [Example("stopwatchcreate[]", "returns a new stopwatch")]
        public object ExpressionStopwatchcreate() {
            return new ExpressionStopwatch();
        }
        #endregion //stopwatchcreate[]
        #region stopwatchstart[stopwatch watch]
        [ExpressionMethod("stopwatchstart")]
        [ExpressionParameter(0, typeof(ExpressionStopwatch), "watch", "stopwatch to start")]
        [ExpressionReturn(typeof(ExpressionStopwatch), "Returns a stopwatch object")]
        [Summary("Starts a stopwatch if not already started")]
        [Example("stopwatchstart[stopwatchcreate[]]", "starts a new stopwatch")]
        public object ExpressionStopwatchstart(ExpressionStopwatch watch) {
            watch.Start();
            return watch;
        }
        #endregion //stopwatchstart[stopwatch watch]
        #region stopwatchstop[stopwatch watch]
        [ExpressionMethod("stopwatchstop")]
        [ExpressionParameter(0, typeof(ExpressionStopwatch), "watch", "stopwatch to stop")]
        [ExpressionReturn(typeof(ExpressionStopwatch), "Returns a stopwatch object")]
        [Summary("Stops a stopwatch if not already stopped")]
        [Example("stopwatchstop[stopwatchcreate[]]", "stops a new stopwatch")]
        public object ExpressionStopwatchstop(ExpressionStopwatch watch) {
            watch.Stop();
            return watch;
        }
        #endregion //stopwatchstart[stopwatch watch]
        #region stopwatchelapsedseconds[stopwatch watch]
        [ExpressionMethod("stopwatchelapsedseconds")]
        [ExpressionParameter(0, typeof(ExpressionStopwatch), "watch", "stopwatch to check")]
        [ExpressionReturn(typeof(ExpressionStopwatch), "Returns a int elapsed seconds")]
        [Summary("Gets the amount of seconds a stopwatch has been running for")]
        [Example("stopwatchelapsedseconds[stopwatchcreate[]]", "Returns the amount of a seconds a new stopwatch has been running for (0 obv)")]
        public object ExpressionStopwatchelapsedseconds(ExpressionStopwatch watch) {
            return watch.Elapsed();
        }
        #endregion //stopwatchelapsedseconds[stopwatch watch]
        #endregion //ExpressionStopwatch
        #region VVS UI
        #region uigetcontrol[string windowName, string controlName]
        [ExpressionMethod("uigetcontrol")]
        [ExpressionParameter(0, typeof(string), "windowName", "Name of the window the control belongs to")]
        [ExpressionParameter(0, typeof(string), "controlName", "Name of the control to get")]
        [ExpressionReturn(typeof(ExpressionUIControl), "Returns a UIControl, or 0 if not found")]
        [Summary("Gets a reference to the named control in the named window")]
        [Example("uigetcontrol[myWindow, myControl]", "Gets a reference to the control `myControl` in the view `myWindow`")]
        public object uigetcontrol(string windowName, string controlName) {
            if (!VTankExtensions.UIControlExists(windowName, controlName, out string error)) {
                Logger.Error($"uigetcontrol: {error}");
                return 0;
            }

            return new ExpressionUIControl(windowName, controlName);
        }
        #endregion //uigetcontrol[string windowName, string controlName]
        #region uisetlabel[UIControl control, string label]
        [ExpressionMethod("uisetlabel")]
        [ExpressionParameter(0, typeof(ExpressionUIControl), "control", "UIControl of the control to change")]
        [ExpressionParameter(0, typeof(string), "label", "The new label text for the control")]
        [ExpressionReturn(typeof(double), "Returns 1 on success, 0 on failure")]
        [Summary("Changes the label text for the given UIControl. Currently only supports buttons.")]
        [Example("uisetlabel[uigetcontrol[myWindow, myControl], new label]", "Changes the control `myControl` label in the view `myWindow` to `new label`")]
        public object uisetlabel(ExpressionUIControl control, string text) {
            if (!VTankExtensions.UISetLabel(control.WindowName, control.ControlName, text, out string error)) {
                Logger.Error($"uisetlabel: {error}");
                return 0;
            }

            return 1;
        }
        #endregion //uisetlabel[UIControl control, string label]
        #region uisetvisible[UIControl control, number visible]
        [ExpressionMethod("uisetvisible")]
        [ExpressionParameter(0, typeof(ExpressionUIControl), "control", "UIControl of the control to change")]
        [ExpressionParameter(0, typeof(double), "visible", "pass 1 to make the control visible, 0 to make it hidden")]
        [ExpressionReturn(typeof(double), "Returns 1 on success, 0 on failure")]
        [Summary("Changes the visibility for the given UIControl. Currently only supports buttons.")]
        [Example("uisetvisible[uigetcontrol[myWindow, myControl], 0]", "Changes the control `myControl` visibility in the view `myWindow` to hidden")]
        public object uisetvisible(ExpressionUIControl control, double visible) {
            if (!VTankExtensions.UISetVisible(control.WindowName, control.ControlName, visible >= 1, out string error)) {
                Logger.Error($"uisetvisible: {error}");
                return 0;
            }

            return 1;
        }
        #endregion //uisetvisible[UIControl control, number visible]
        #region uiviewexists[string windowName]
        [ExpressionMethod("uiviewexists")]
        [ExpressionParameter(0, typeof(string), "windowName", "Name of the window to check the existence of")]
        [ExpressionReturn(typeof(ExpressionUIControl), "Returns 1 if it exists, 0 if not")]
        [Summary("Checks if a meta view exists")]
        [Example("uiviewexists[myWindow]", "returns true if a view with the title `myWindow` exists")]
        public object uiviewexists(string windowName) {
            return VTankExtensions.UIMetaViewExists(windowName);
        }
        #endregion //uiviewexists[string windowName]
        #region uiviewvisible[string windowName]
        [ExpressionMethod("uiviewvisible")]
        [ExpressionParameter(0, typeof(string), "windowName", "Name of the window to check the visibilty of")]
        [ExpressionReturn(typeof(ExpressionUIControl), "Returns 1 if it exists, 0 if not")]
        [Summary("Checks if a meta view is visible")]
        [Example("uiviewvisible[myWindow]", "returns true if a view with the title `myWindow` is visible")]
        public object uiviewvisible(string windowName) {
            return VTankExtensions.UIMetaViewIsVisible(windowName);
        }
        #endregion //uiviewvisible[string windowName]
        #endregion //VVS UI
        #endregion //Expressions

        private bool isFixingPortalLoops = false;
        private int portalExitCount = 0;
        private int lastPortalExitLandcell = 0;
        private List<string> expressionExceptions = new List<string>();
        private bool isClassicPatched;

        public VTankControl(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        public override void Init() {
            base.Init();

            if (UBHelper.Core.GameState == UBHelper.GameState.In_Game) {
                Enable();
                DoVTankExpressionPatches();
            }
            else UB.Core.CharacterFilter.LoginComplete += CharacterFilter_LoginComplete;

            FixPortalLoops.Changed += VTankControl_PropertyChanged;
            PatchExpressionEngine.Changed += VTankControl_PropertyChanged;

            if (FixPortalLoops) {
                UB.Core.CharacterFilter.ChangePortalMode += CharacterFilter_ChangePortalMode;
                isFixingPortalLoops = true;
            }
        }

        class ExpressionErrorListener : DefaultErrorStrategy {
            public override void ReportError(Parser recognizer, RecognitionException e) {
                var error = e.GetType() == typeof(Antlr4.Runtime.NoViableAltException) ? "Syntax Error" : e.Message;
                var ex = new Exception($"{error}: Offending character: '{e.OffendingToken.Text}' @ position {e.OffendingToken.Column}");
                ex.Source = e.OffendingToken.Column.ToString();
                throw ex;
            }
        }

        public object EvaluateExpression(string expression, bool silent = true) {
            try {
                return CompileExpression(expression).Run();
            }
            catch (Exception ex) {
                if (!silent && expressionExceptions.Contains(ex.ToString()))
                    return null;
                expressionExceptions.Add(ex.ToString());

                var message = UB.Plugin.Debug ? ex.ToString() : (ex.InnerException != null ? ex.InnerException.Message : ex.Message);
                //expression = expression.Insert(int.Parse(ex.Source, NumberStyles.Integer), "<Tell:IIDString:{Util.GetChatId()}:errorpos>errorpos</Tell>");
                //UB.Core.Actions.AddChatTextRaw(expression, 1);
                Logger.Error($"Error in expression: {expression}\n  {message}", false, false);
                throw ex;
            }
        }

        Dictionary<string, Lib.Models.CompiledExpression> _compiledExpressions = new Dictionary<string, Lib.Models.CompiledExpression>();
        public Lib.Models.CompiledExpression CompileExpression(string expression) {
            Lib.Models.CompiledExpression compiledExpression = null;
            if (_compiledExpressions.TryGetValue(expression, out compiledExpression)) {
                return compiledExpression;
                //_compiledExpressions.Remove()
            }

            try {
                AntlrInputStream inputStream = new AntlrInputStream(expression);
                MetaExpressionsLexer spreadsheetLexer = new MetaExpressionsLexer(inputStream);
                CommonTokenStream commonTokenStream = new CommonTokenStream(spreadsheetLexer);
                MetaExpressionsParser expressionParser = new MetaExpressionsParser(commonTokenStream);
                expressionParser.ErrorHandler = new ExpressionErrorListener();
                MetaExpressionsParser.ParseContext parseContext = expressionParser.parse();
                compiledExpression = new Lib.Models.CompiledExpression(parseContext);
                _compiledExpressions.Add(expression, compiledExpression);

                return compiledExpression;
            }
            catch (Exception ex) {
                expressionExceptions.Add(ex.ToString());

                var message = UB.Plugin.Debug ? ex.ToString() : (ex.InnerException != null ? ex.InnerException.Message : ex.Message);
                //expression = expression.Insert(int.Parse(ex.Source, NumberStyles.Integer), "<Tell:IIDString:{Util.GetChatId()}:errorpos>errorpos</Tell>");
                //UB.Core.Actions.AddChatTextRaw(expression, 1);
                Logger.Error($"Error in expression: {expression}\n  {message}", false, false);
                throw ex;
            }
        }

        #region VTank Patches
        private void DoVTankExpressionPatches() {
            try {
                VTankExtensions.UnpatchVTankExpressions();
                if (!PatchExpressionEngine)
                    return;
                VTankExtensions.PatchVTankExpressions(new VTankExtensions.Del_EvaluateExpression(EvaluateExpression));
            }
            catch (Exception ex) { Logger.LogException(ex); Logger.Error(ex.ToString()); }
        }
        #endregion

        #region Helpers
        private object DeserializeExpressionValue(string value, Type type = null) {
            if (type != null)
                return ExpressionObjectBase.DeserializeRecord(value, type);
            else
                return EvaluateExpression(value);
            //return JsonConvert.DeserializeObject(value, ExpressionObjectBase.SerializerSettings);
        }

        private bool SerializeExpressionValue(object value, ref string expressionValue) {
            if (value is ExpressionObjectBase obj && !obj.IsSerializable)
                throw new Exception($"There is currently no support for serializing {ExpressionVisitor.GetFriendlyType(value.GetType())} types");
            expressionValue = ExpressionObjectBase.SerializeRecord(value);

            return true;
        }

        private WorldObject GetCharacter() {
            return UtilityBeltPlugin.Instance.Core.WorldFilter[UtilityBeltPlugin.Instance.Core.CharacterFilter.Id];
        }
        #endregion

        private void VTankControl_PropertyChanged(object sender, SettingChangedEventArgs e) {
            if (e.PropertyName.Equals("FixPortalLoops")) {
                if (FixPortalLoops && !isFixingPortalLoops) {
                    UB.Core.CharacterFilter.ChangePortalMode += CharacterFilter_ChangePortalMode;
                    isFixingPortalLoops = true;
                }
                else if (!FixPortalLoops && isFixingPortalLoops) {
                    UB.Core.CharacterFilter.ChangePortalMode -= CharacterFilter_ChangePortalMode;
                    isFixingPortalLoops = false;
                }
            }
            else if (e.PropertyName.Equals("PatchExpressionEngine")) {
                DoVTankExpressionPatches();
            }
        }

        private void CharacterFilter_ChangePortalMode(object sender, Decal.Adapter.Wrappers.ChangePortalModeEventArgs e) {
            try {
                if (e.Type != Decal.Adapter.Wrappers.PortalEventType.ExitPortal)
                    return;

                if (lastPortalExitLandcell == UB.Core.Actions.Landcell) {
                    portalExitCount++;
                }
                else {
                    portalExitCount = 1;
                    lastPortalExitLandcell = UB.Core.Actions.Landcell;
                }

                if (portalExitCount >= PortalLoopCount) {
                    DoPortalLoopFix();
                    return;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void CharacterFilter_LoginComplete(object sender, EventArgs e) {
            try {
                UB.Core.CharacterFilter.LoginComplete -= CharacterFilter_LoginComplete;
                DoVTankExpressionPatches();
                Enable();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        public void Enable() {
            UBHelper.vTank.Enable();
            UB.Core.CharacterFilter.Logoff += CharacterFilter_Logoff;
        }

        private void CharacterFilter_Logoff(object sender, Decal.Adapter.Wrappers.LogoffEventArgs e) {
            try {
                if (e.Type == Decal.Adapter.Wrappers.LogoffEventType.Authorized)
                    UBHelper.vTank.Disable();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void DoPortalLoopFix() {
            // TODO: fixy
            return;
            Logger.WriteToChat($"Nav: {UBHelper.vTank.Instance.NavCurrent}");
            Util.DispatchChatToBoxWithPluginIntercept($"/vt nav save {VTNavRoute.NoneNavName}");
            UBHelper.vTank.Instance.NavDeletePoint(0);
        }

        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    FixPortalLoops.Changed -= VTankControl_PropertyChanged;
                    PatchExpressionEngine.Changed -= VTankControl_PropertyChanged;
                    UB.Core.CharacterFilter.LoginComplete -= CharacterFilter_LoginComplete;
                    UB.Core.CharacterFilter.Logoff -= CharacterFilter_Logoff;
                    //UB.Core.EchoFilter.ServerDispatch -= EchoFilter_ServerDispatch;

                    if (isFixingPortalLoops)
                        UB.Core.CharacterFilter.ChangePortalMode -= CharacterFilter_ChangePortalMode;
                    if (PatchExpressionEngine)
                        VTankExtensions.UnpatchVTankExpressions();

                    base.Dispose(disposing);
                }
                disposedValue = true;
            }
        }
    }
}
