using Decal.Adapter;
using System;
using System.Text.RegularExpressions;
using UtilityBelt.Lib;
using UBLoader.Lib.Settings;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UtilityBelt.Tools {
    [Name("LoginManagerCommand")]
    [Summary("Set a character to automatically login as by part of name of relative index.")]
    public class LoginManager : ToolBase {

        #region Expressions
        #region getcharacterindex[string name]
        [ExpressionMethod("getcharacterindex")]
        [ExpressionParameter(0, typeof(string), "name", "Name substring")]
        [ExpressionReturn(typeof(double), "Returns alphabetic index of the specified character or -1")]
        [Summary("Gets the alphabetic index of the specified character or -1")]
        [Example("getcharacterindex[``]", "Get index of the current character")]
        [Example("getcharacterindex[`mule`]", "Get index the first character with `mule` in the name")]
        public object Getcharacterindex(string name) {
            var names = GetSortedCharacters();
            if (String.IsNullOrEmpty(name))
                name = CoreManager.Current.CharacterFilter.Name;
            Logger.WriteToChat($"Check name of {names}");
            return names.FindIndex(x => x.Name.ContainsCaseInsensitive(name));
        }
        #endregion getcharacterindex[string name]
        #region setnextlogin[string nameOrIndex]
        [ExpressionMethod("setnextlogin")]
        [ExpressionParameter(0, typeof(string), "nameOrIndex", "name substring or index offset")]
        [ExpressionReturn(typeof(double), "Returns 1 if login was set")]
        [Summary("Sets the next login by name substring or relative index")]
        [Example("setnextlogin[`1`]", "Sets next login to the next alphabetically")]
        [Example("setnextlogin[`mule`]", "Sets next login to the first with `mule` in their name")]
        public object Setnextlogin(string nameOrIndex) {
            if (int.TryParse(nameOrIndex, out int index))
                return SetNextLogin(index, true, true, true);
            else
                return SetNextLoginByName(nameOrIndex, true);
        }
        #endregion clearnextlogin[]
        #region clearnextlogin[]
        [ExpressionMethod("clearnextlogin")]
        [ExpressionReturn(typeof(double), "Returns 1 if login was cleared")]
        [Summary("Clears the next login")]
        [Example("clearnextlogin[]", "Clears the next login")]
        public object Clearnextlogin() {
            ClearLogin(true);
            return 1;
        }
        #endregion clearnextlogin[]
        #endregion Expressions

        #region Commands
        const string CommandPattern = @"^(?<command>next|clear|list)(?<args>.+)?$";  //Should match LoginManagerCommand enum
        static string HostCommands = String.Join("|", Enum.GetNames(typeof(LoginManagerCommand)).OrderBy(x => x).ToArray()).ToLower();

        [Summary("Sets or clears a character to login as after logging out")]
        [Usage(@"/ub login { clear | list | next[r][l] [part of name|index]}>")]
        [Example("/ub login next 2", "Logs in the character at index 2 (the third one)")]
        [Example("/ub login nextr -1", "Logs in the character with a name before the current one alphabetically")]
        [Example("/ub login nextrl 1", "Logs in the character that comes next alphabetically, looping to the beginning")]
        [Example("/ub login next salv", "Logs in the first character with 'salv' as part of their name")]
        [Example("/ub login clear", "Clears the next login.")]
        [Example("/ub login list", "Lists the characters and indices for this account.")]
        [CommandPattern("login", CommandPattern)]
        public void HandleLoginCommand(string command, Match args) {
            if (!args.Groups["command"].Value.EnumTryParse<LoginManagerCommand>(out var cmd)) {
                Logger.WriteToChat($"Available /ub login commands are: {HostCommands}");
                return;
            }

            var argsText = args.Groups["args"].Value.Trim();
            bool noArgs = String.IsNullOrEmpty(argsText);

            switch (cmd) {
                case LoginManagerCommand.Next:
                    if (noArgs) {
                        Logger.WriteToChat("Specify part of name or index: /ub login next <name|index>");
                        return;
                    }
                    bool relative = false, looping = false;
                    if (argsText.StartsWith("r")) {
                        relative = true;
                        argsText = argsText.Substring(1);
                    }
                    if (argsText.StartsWith("l")) {
                        looping = true;
                        argsText = argsText.Substring(1);
                    }

                    if (int.TryParse(argsText, out int index))
                        SetNextLogin(index, relative, looping);
                    else
                        SetNextLoginByName(argsText);
                    break;

                case LoginManagerCommand.List:
                    PrintLogins();
                    break;

                case LoginManagerCommand.Clear:
                    ClearLogin();
                    break;
            }
        }
        #endregion

        #region Enums / Factory
        //Enums used in regex and parsing commands
        enum LoginManagerCommand {
            Next,
            List,
            Clear,
        }
        #endregion

        public LoginManager(UtilityBeltPlugin ub, string name) : base(ub, name) { }

        struct CharInfo {
            public string Name;
            public int Id;
            public int Index;

            public CharInfo(string name, int id, int index) {
                Name = name;
                Id = id;
                Index = index;
            }
        }
        private List<CharInfo> GetSortedCharacters() {
            var sortedChars = new List<CharInfo>();
            var chars = CoreManager.Current.CharacterFilter.Characters;

            for (var i = 0; i < chars.Count; i++)
                sortedChars.Add(new CharInfo(chars[i].Name, chars[i].Id, chars[i].Index));

            sortedChars = sortedChars.OrderBy(x => x.Name).ToList();

            return sortedChars;
        }

        public void ClearLogin(bool silent = false) {
            if (!silent)
                Logger.WriteToChat("Next login cleared.");
            UBLoader.LoaderLogin.ClearNextLogin();
        }

        public bool SetNextLoginByName(string name, bool silent = false) {
            var chars = GetSortedCharacters();

            var index = chars.FindIndex(x => x.Name.Contains(name, StringComparison.InvariantCultureIgnoreCase));
            if (index < 0) {
                if (!silent)
                    Logger.WriteToChat($"No character found with name {name}.  Clearing next login.");
                ClearLogin(silent);
                return false;
            }

            return SetNextLogin(index, silent: silent);
        }

        /// <summary>
        /// Set next login by name or relative index.
        /// </summary>
        public bool SetNextLogin(int index, bool relative = false, bool looping = false, bool silent = false) {
            //Get a sorted list of characters
            var chars = GetSortedCharacters();
            var currentName = CoreManager.Current.CharacterFilter.Name;

            if (relative) {
                //Find current login to offset
                var currentIndex = chars.FindIndex(x => x.Name.Contains(currentName, StringComparison.InvariantCultureIgnoreCase));

                if (currentIndex < 0) {
                    if (!silent)
                        Logger.WriteToChat("Unable to login relative to newly created characters.");
                    ClearLogin(silent);
                    return false;
                }
                index += currentIndex;
            }

            //If looping convert index its in-range equivalent
            if (looping)
                index = ((index + chars.Count) % chars.Count + chars.Count) % chars.Count;

            if (index < 0 || index >= chars.Count) {
                if (!silent)
                    Logger.WriteToChat($"Login index is out of bounds.  Use the [l]oop option to wrap around: /ub login nextl -100");
                ClearLogin(silent);
                return false;
            }

            if (!silent)
                Logger.WriteToChat($"Logging in as {chars[index].Name} next at index {index}");

            UBLoader.LoaderLogin.SetNextLogin((uint)chars[index].Id);
            return true;
        }

        public void PrintLogins() {
            var chars = GetSortedCharacters();
            var currentName = CoreManager.Current.CharacterFilter.Name;

            var sb = new StringBuilder();

            sb.Append($"Listing {chars.Count} logins.\n");
            sb.Append($"{"Index",-10}{"Name",-30}{"ID",-20}Filter Index\n");

            for (var i = 0; i < chars.Count; i++)
                sb.Append($"{i,-10}{(chars[i].Name == currentName ? $"**{chars[i].Name}**" : chars[i].Name),-30}{chars[i].Id,-20}{chars[i].Index}\n");

            Logger.WriteToChat(sb.ToString());
        }

        public override void Init() {
            base.Init();
        }
    }
}