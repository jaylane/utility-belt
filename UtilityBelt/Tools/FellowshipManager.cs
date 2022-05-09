using System;
using System.Collections.Generic;
using System.Linq;
using UtilityBelt.Lib;
using System.Text.RegularExpressions;
using Decal.Adapter.Wrappers;
using Decal.Adapter;
using System.Runtime.InteropServices;
using UBLoader.Lib.Settings;
using UtilityBelt.Lib.Expressions;

namespace UtilityBelt.Tools {
    [Name("Fellowships")]
    public class FellowshipManager : ToolBase {

        public FellowshipManager(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }


        #region Expressions

        #region getfellowshipstatus[]
        [ExpressionMethod("getfellowshipstatus")]
        [ExpressionReturn(typeof(double), "Returns 1 if you are in a fellowship, 0 otherwise")]
        [Summary("Checks if you are currently in a fellowship")]
        [Example("getfellowshipstatus[]", "returns 1 if you are in a fellowship, 0 otherwise")]
        public object Getfellowshipstatus() {
            return UBHelper.Fellow.InFellowship;
        }
        #endregion

        #region getfellowshipname[]
        [ExpressionMethod("getfellowshipname")]
        [ExpressionReturn(typeof(string), "Returns the name of a fellowship, or an empty string if none")]
        [Summary("Gets the name of your current fellowship")]
        [Example("getfellowshipname[]", "returns the name of your current fellowship")]
        public object Getfellowshipname() {
            return UBHelper.Fellow.Name;
        }
        #endregion

        #region getfellowshipcount[]
        [ExpressionMethod("getfellowshipcount")]
        [ExpressionReturn(typeof(double), "Returns the number of members in your fellowship, or 0 if you are not in a fellowship")]
        [Summary("Gets the name of your current fellowship")]
        [Example("getfellowshipcount[]", "Returns the number of members in your fellowship, or 0 if you are not in a fellowship")]
        public object GetFellowshipCount() {
            return UBHelper.Fellow.MemberCount;
        }
        #endregion

        #region getfellowshipleaderid[]
        [ExpressionMethod("getfellowshipleaderid")]
        [ExpressionReturn(typeof(double), "Returns the character id of the fellowship leader")]
        [Summary("Returns the character id of the fellowship leader")]
        [Example("getfellowshipleaderid[]", "Returns the character id of the fellowship leader")]
        public object GetFellowshipLeaderID() {
            return UBHelper.Fellow.Leader;
        }
        #endregion

        #region getfellowid[x]
        [ExpressionMethod("getfellowid")]
        [ExpressionParameter(0, typeof(double), "iterator", "fellowship member to return (0-8)")]
        [ExpressionReturn(typeof(double), "Character ID of requested Fellowship Member, or 0")]
        [Summary("Returns a fellowship member's character id. It is recommended to save the output to a variable.")]
        [Example("getfellowid[0]", "Returns the id of the first Fellowship Member")]
        public object GetfellowID(double value) {
            if (value < UBHelper.Fellow.MemberCount)
                return UBHelper.Fellow.Members.ElementAt((int)value).Value.id;
            return 0;
        }
        #endregion

        #region getfellowname[x]
        [ExpressionMethod("getfellowname")]
        [ExpressionParameter(0, typeof(double), "iterator", "fellowship member to return (0-8)")]
        [ExpressionReturn(typeof(string), "Character name of requested Fellowship Member, or \"\"")]
        [Summary("Returns a fellowship member's character name. It is recommended to save the output to a variable.")]
        [Example("getfellowname[0]", "Returns the name of the first Fellowship Member")]
        public object GetfellowName(double value) {
            if (value < UBHelper.Fellow.MemberCount)
                return UBHelper.Fellow.Members.ElementAt((int)value).Value.name;
            return "";
        }
        #endregion

        #region getfellowshiplocked[]
        [ExpressionMethod("getfellowshiplocked")]
        [ExpressionReturn(typeof(double), "Returns 1 if your fellowship is Locked, 0 otherwise")]
        [Summary("Returns 1 if your fellowship is Locked")]
        [Example("getfellowshiplocked[]", "Returns 1 if your fellowship is Locked")]
        public object GetfellowshipLocked() {
            return UBHelper.Fellow.Locked;
        }
        #endregion

        #region getfellowshipisleader[]
        [ExpressionMethod("getfellowshipisleader")]
        [ExpressionReturn(typeof(double), "Returns 1 if you are the leader of your fellowship, 0 otherwise")]
        [Summary("Returns 1 if you are the leader of your fellowship")]
        [Example("getfellowshipisleader[]", "Returns 1 if you are the leader of your fellowship")]
        public object GetfellowshipIsLeader() {
            return UBHelper.Fellow.IsLeader;
        }
        #endregion

        #region getfellowshipisopen[]
        [ExpressionMethod("getfellowshipisopen")]
        [ExpressionReturn(typeof(double), "Returns 1 if your fellowship is open, 0 otherwise")]
        [Summary("Returns 1 if your fellowship is open")]
        [Example("getfellowshipisopen[]", "Returns 1 if your fellowship is open")]
        public object GetfellowshipIsOpen() {
            return UBHelper.Fellow.Open;
        }
        #endregion

        #region getfellowshipisfull[]
        [ExpressionMethod("getfellowshipisfull")]
        [ExpressionReturn(typeof(double), "Returns 1 if you're in a fellowship and it is full. 0 otherwise")]
        [Summary("Returns 1 if you're in a fellowship, and it is full")]
        [Example("getfellowshipisfull[]", "Returns 1 if you're in a fellowship, and it is full")]
        public object GetfellowshipIsFull() {
            return UBHelper.Fellow.MemberCount == 9;
        }
        #endregion

        #region getfellowshipcanrecruit[]
        [ExpressionMethod("getfellowshipcanrecruit")]
        [ExpressionReturn(typeof(double), "Returns 1 if you're in a fellowship, and are the leader, or the fellowship is open, and the fellowship is not full")]
        [Summary("Returns 1 if your fellowship is open, or you are the leader, and the fellowship is not full")]
        [Example("getfellowshipcanrecruit[]", "Returns 1 if you're in a fellowship, and are the leader, or the fellowship is open, and the fellowship is not full")]
        public object GetfellowshipCanRecruit() {
            return (UBHelper.Fellow.IsLeader || UBHelper.Fellow.Open) && UBHelper.Fellow.MemberCount < 9;
        }
        #endregion

        #region getfellownames[]
        [ExpressionMethod("getfellownames")]
        [ExpressionReturn(typeof(ExpressionList), "List of character names in the fellowship, sorted alphabetically")]
        [Summary("Returns a list of character names in your current fellowship, sorted alphabetically.")]
        [Example("getfellownames[]", "Returns a list of character names in your current fellowship, sorted alphabetically")]
        public object GetfellowNames() {
            var list = new ExpressionList();
            if (UBHelper.Fellow.MemberCount > 0) {
                var members = UBHelper.Fellow.Members.Select(kv => kv.Value.name).ToList();
                members.Sort();
                foreach (var member in members)
                    list.Items.Add(member);
            }
            return list;
        }
        #endregion

        #region getfellowids[]
        [ExpressionMethod("getfellowids")]
        [ExpressionReturn(typeof(ExpressionList), "List of character ids in the fellowship, sorted numerically")]
        [Summary("Returns a list of character ids in your current fellowship, sorted numerically.")]
        [Example("getfellowids[]", "Returns a list of character ids in your current fellowship, sorted numerically")]
        public object GetfellowIds() {
            var list = new ExpressionList();
            if (UBHelper.Fellow.MemberCount > 0) {
                var members = UBHelper.Fellow.Members.Select(kv => kv.Value.id).ToList();
                members.Sort();
                foreach (var member in members)
                    list.Items.Add(member);
            }
            return list;
        }
        #endregion

        #endregion Expressions

        #region Commands
        #region /ub fellow create <Name>|quit|disband|open|close|status|recruit[p][ Name]|dismiss[p][ Name]|leader[p][ Name]
        [Summary("UB Fellowship Commands")]
        [Usage("/ub fellow create <Name>|quit|disband|open|close|status|recruit[p][ Name]|dismiss[p][ Name]|leader[p][ Name]")]
        [CommandPattern("fellow", @"^(create .+|quit|disband|open|close|status|recruit(p)?( .+)?|dismiss(p)?( .+)?|leader(p)?( .+)?)$")]
        public void DoFellow(string _, Match args) {
            string[] a = args.Value.Split(' ');
            string Name = "";
            if (!UBHelper.Fellow.InFellowship && !a[0].Equals("create")) {
                Logger.WriteToChat($"Your are not currently in a fellowship.");
                return;
            }

            switch (a[0]) {
                //[Summary("Creates a fellowship")]
                //[Usage("/ub fellow create <name>")]
                //[Example("/ub fellow create trevis-haters' club", "Creates a fellowship named `trevis-haters' club`")]
                //[CommandPattern("fellow create", @"^(?<Name>.*)$")]
                case "create":
                    if (UBHelper.Fellow.InFellowship) { Logger.WriteToChat($"You are already in a fellowship."); return; }
                    UBHelper.Fellow.Create(args.Value.Substring(7));
                    break;

                //[Summary("Leaves your current fellowship")]
                //[Usage("/ub fellow quit")]
                //[CommandPattern("fellow quit", @"^$")]
                case "quit":
                    UBHelper.Fellow.Quit();
                    break;

                //[Summary("Disbands your current fellowship")]
                //[Usage("/ub fellow disband")]
                //[CommandPattern("fellow disband", @"^$")]
                case "disband":
                    if (!UBHelper.Fellow.IsLeader) { Logger.WriteToChat("You are not the fellowship leader!"); return; }
                    UBHelper.Fellow.Disband();
                    break;

                //[Summary("Changes your current fellowship to Open")]
                //[Usage("/ub fellow open")]
                //[CommandPattern("fellow open", @"^$")]
                case "open":
                    UBHelper.Fellow.Open = true;
                    break;

                //[Summary("Changes your current fellowship to Closed")]
                //[Usage("/ub fellow close")]
                //[CommandPattern("fellow close", @"^$")]
                case "close":
                    UBHelper.Fellow.Open = false;
                    break;

                //[Summary("Shows the status, and a list of the members in your current fellowship")]
                //[Usage("/ub fellow status")]
                //[CommandPattern("fellow status", @"^$")]
                case "status":
                    Logger.WriteToChat($"Your current fellowship, \"{UBHelper.Fellow.Name}\", has {UBHelper.Fellow.MemberCount} member{(UBHelper.Fellow.MemberCount != 1 ? "s" : "")}, {(UBHelper.Fellow.ShareXP ? "" : "NOT ")}Sharing XP{(UBHelper.Fellow.EvenXPSplit ? "" : ", Uneven Split")}. {(UBHelper.Fellow.Open ? "Open" : "Closed")}, {(UBHelper.Fellow.Locked ? "**LOCKED**" : "Not Locked")}.");
                    Dictionary<int, UBHelper.FellowMember> members = UBHelper.Fellow.Members;
                    int leaderId = UBHelper.Fellow.Leader;
                    foreach (KeyValuePair<int, UBHelper.FellowMember> thisOne in members) {
                        Logger.WriteToChat($" {thisOne.Value.name}[{thisOne.Value.level}] H:{thisOne.Value.cur_health}/{thisOne.Value.max_health}{(leaderId == thisOne.Value.id ? " (Leader) " : "")}");
                    }
                    members.Clear();
                    break;

                //[Summary("Recruits character to your fellowship")]
                //[Usage("/ub fellow recruit[p][ <name|id|selected>]")]
                //[Example("/ub fellow recruit Yonneh", "Recruits `Yonneh`, to your fellowship")]
                //[Example("/ub fellow recruitp Yo", "Recruits a character with a name partially matching `Yo`, to your fellowship")]
                //[Example("/ub fellow recruit", "Recruits the closest character, to your fellowship")]
                //[Example("/ub fellow recruit selected", "Recruits the selected character, to your fellowship")]
                //[CommandPattern("fellow recruit", @"^(?<charName>.+)?$")]
                case "recruit":
                    try { Name = args.Value.Substring(8); } catch { }
                    DoRecruit(Name, false);
                    break;

                case "recruitp":
                    try { Name = args.Value.Substring(9); } catch { }
                    DoRecruit(Name, true);
                    break;

                //[Summary("Dismiss character from your fellowship")]
                //[Usage("/ub fellow dismiss[p][ <name|id|selected>]")]
                //[Example("/ub fellow dismiss Yonneh", "Dismiss `Yonneh`, from your fellowship")]
                //[Example("/ub fellow dismissp Yo", "Dismiss a character with a name partially matching `Yo`, from your fellowship")]
                //[Example("/ub fellow dismiss", "Dismiss the closest character, from your fellowship")]
                //[Example("/ub fellow dismiss selected", "Dismiss the selected character, from your fellowship")]
                //[CommandPattern("fellow dismiss", @"^(?<charName>.+)?$")]
                case "dismiss":
                    try { Name = args.Value.Substring(8); } catch { }
                    DoDismiss(Name, false);
                    break;

                case "dismissp":
                    try { Name = args.Value.Substring(9); } catch { }
                    DoDismiss(Name, true);
                    break;

                //[Summary("Transfer leadership of your fellowship")]
                //[Usage("/ub fellow leader[p][ <name|id|selected>]")]
                //[Example("/ub fellow leader Yonneh", "Give `Yonneh` leader, in your fellowship")]
                //[Example("/ub fellow leaderp Yo", "Give a character with a name partially matching `Yo` leader, in your fellowship")]
                //[Example("/ub fellow leader", "Give the closest character leader, in your fellowship")]
                //[Example("/ub fellow leader selected", "Give the selected character leader, in your fellowship")]
                //[CommandPattern("fellow leader", @"^(?<charName>.+)?$")]
                case "leader":
                    try { Name = args.Value.Substring(7); } catch { }
                    DoLeader(Name, false);
                    break;

                case "leaderp":
                    try { Name = args.Value.Substring(8); } catch { }
                    DoLeader(Name, true);
                    break;

                default:
                    Logger.WriteToChat($"/ub fellow unknown command: {a[0]}");
                    break;
            }
        }
        public void DoRecruit(string Name, bool partial) {
            WorldObject fellowChar = Util.FindName(Name, partial, new ObjectClass[] { ObjectClass.Player });
            if (fellowChar != null) {
                if (UB.Plugin.Debug) Logger.WriteToChat($"Recruiting {fellowChar.Name}[0x{fellowChar.Id:X8}]");
                UBHelper.Fellow.Recruit(fellowChar.Id);
                return;
            }
            if (UB.Plugin.Debug) Logger.Error($"Could not find {(Name == null ? "closest player" : $"player {Name}")}");
        }
        public void DoDismiss(string Name, bool partial) {
            UBHelper.FellowMember fellowChar = FindName(Name, partial);
            if (fellowChar != null) {
                if (UB.Plugin.Debug) Logger.WriteToChat($"Dismissing {fellowChar.name}[0x{fellowChar.id:X8}]");
                UBHelper.Fellow.Dismiss(fellowChar.id);
                return;
            }
            if (UB.Plugin.Debug) Logger.Error($"Could not find {(Name == null ? "closest player" : $"player {Name}")}");
        }
        public void DoLeader(string Name, bool partial) {
            UBHelper.FellowMember fellowChar = FindName(Name, partial);
            if (fellowChar != null) {
                if (UB.Plugin.Debug) Logger.WriteToChat($"Transferring leader to {fellowChar.name}[0x{fellowChar.id:X8}]");
                UBHelper.Fellow.Leader = fellowChar.id;
                return;
            }
            if (UB.Plugin.Debug) Logger.Error($"Could not find {(Name == null ? "closest player" : $"player {Name}")}");
        }
        #endregion

        #endregion

        #region UBHelper.FellowMember FellowshipManager.FindName(searchname, partial)
        /// <summary>
        /// Find a character by name, from the current fellowship, excluding the player (unless searching specifically by ID, or "selected")
        /// </summary>
        /// <param name="searchname">if blank, finds the closest member in the fellowship that is not the player. if "selected", attempts to return the selected player. You can find the player "Selected" by using appropriate capitalization. if numeric or hexadecimal, attempts to return the given character</param>
        /// <param name="partial">allow partial name matches (blank name overrides this)</param>
        /// <returns></returns>
        public static UBHelper.FellowMember FindName(string searchname, bool partial) {
            if (!UBHelper.Fellow.InFellowship) return null;
            Dictionary<int, UBHelper.FellowMember> members = UBHelper.Fellow.Members;
            if (int.TryParse(searchname, out int id))
                if (members.Keys.Contains(id))
                    return members[id];
            try {
                int intValue = Convert.ToInt32(searchname, 16);
                if (members.Keys.Contains(intValue))
                    return members[intValue];
            }
            catch { }
            if (searchname.Equals("selected") && CoreManager.Current.Actions.CurrentSelection != 0 && members.Keys.Contains(CoreManager.Current.Actions.CurrentSelection)) {
                return members[CoreManager.Current.Actions.CurrentSelection];
            }
            searchname = searchname.ToLower();
            UBHelper.FellowMember found = null;
            double lastDistance = double.MaxValue;
            double thisDistance;
            foreach (KeyValuePair<int, UBHelper.FellowMember> thisOne in members) {
                thisDistance = UBHelper.Core.DirtyDistance(thisOne.Key);
                if (thisOne.Key != CoreManager.Current.CharacterFilter.Id && (found == null || lastDistance > thisDistance)) {
                    string thisLowerName = thisOne.Value.name.ToLower();
                    if (partial && (searchname.Length == 0 || thisLowerName.Contains(searchname))) {
                        found = thisOne.Value;
                        lastDistance = thisDistance;
                    }
                    else if ((searchname.Length == 0 || thisLowerName.Equals(searchname))) {
                        found = thisOne.Value;
                        lastDistance = thisDistance;
                    }
                }
            }
            return found;
        }
        #endregion

    }
}
