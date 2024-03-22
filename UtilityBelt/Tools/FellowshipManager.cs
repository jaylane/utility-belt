using System;
using System.Collections.Generic;
using System.Linq;
using UtilityBelt.Lib;
using System.Text.RegularExpressions;
using Decal.Adapter.Wrappers;
using Decal.Adapter;
using UtilityBelt.Service.Lib.Settings;
using UtilityBelt.Lib.Expressions;
using AcClient;
using System.Windows.Forms.VisualStyles;

namespace UtilityBelt.Tools {
    [Name("Fellowships")]
    public class FellowshipManager : ToolBase {

        public FellowshipManager(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        #region Config
        [Summary("Enabled")]
        public Setting<bool> Enabled = new Setting<bool>(false);
        [Summary("My AutoFellow Password")]
        public Setting<string> MyAutoFellowPassword = new Setting<string>("xp");
        [Summary("Fellowship Banned List")]
        public CharacterState<System.Collections.ObjectModel.ObservableCollection<string>> BanList = new CharacterState<System.Collections.ObjectModel.ObservableCollection<string>>(new System.Collections.ObjectModel.ObservableCollection<string>());
        #endregion // Config

        public override void Init() {
            base.Init();
            //try {
            //    Enabled.Changed += Enabled_Changed;
            //    if (UBHelper.Core.GameState != UBHelper.GameState.In_Game)
            //        UB.Core.CharacterFilter.LoginComplete += CharacterFilter_LoginComplete;
            //    else
            //        TryEnable();
            //}
            //catch (Exception ex) { Logger.LogException(ex); }
        }

        //private void Enabled_Changed(object sender, SettingChangedEventArgs e) {
        //    Logger.WriteToChat($"TODO Enabled Changed -> {e.Setting}");
        //}

        //private void CharacterFilter_LoginComplete(object sender, EventArgs e) {
        //    UB.Core.CharacterFilter.LoginComplete -= CharacterFilter_LoginComplete;
        //    TryEnable();
        //}
        //private void TryEnable() {
        //    //if (Enabled) Setup();
        //    //else ClearHud();
        //}

        //protected override void Dispose(bool disposing) {
        //    if (!disposedValue) {
        //        if (disposing) {
        //            Enabled.Changed -= Enabled_Changed;
        //            UB.Core.CharacterFilter.LoginComplete -= CharacterFilter_LoginComplete;
        //            RecruitQueue.Clear();
        //        }
        //        disposedValue = true;
        //    }
        //}







        //#region Chat Commands (currently bastard code- WIP)

        ////stub-todo
        //public bool sendTell(uint _player_id, string _msg) { Logger.WriteToChat($"sendTell(0x{_player_id:X8},\"{_msg}\");"); return true; }
        ////stub-todo

        //private List<string> RecruitQueue = new List<string>();
        //private bool RecruitQueueEmpty = true;
        ///// <summary>
        ///// 
        ///// </summary>
        ///// <param name="_senderName"></param>
        ///// <param name="_senderID"></param>
        ///// <param name="_msg"></param>
        ///// <returns>true if the message was properly parsed</returns>
        //public bool processChat(string _senderName, uint _senderID, string _msg) {
        //    #region (chat message) MyAutoFellowPassword
        //    if (_msg == MyAutoFellowPassword) {
        //        if (IsInFellowship(_senderID)) {
        //            sendTell(_senderID, $"[VT Fellow Manager] You are already in the fellowship.  -v-");
        //            return true;
        //        }
        //        if (BanList.Value.Contains(_senderName)) {
        //            sendTell(_senderID, $"[VT Fellow Manager] Sorry, but you have been banned from this fellowship.  -v-");
        //            return true;
        //        }
        //        if (!Open && !IsLeader) {
        //            sendTell(_senderID, $"[VT Fellow Manager] I'm sorry, but the fellowship is closed and I am not the leader. The leader is currently: {Leader}  -v-");
        //            return true;
        //        }
        //        if (MemberCount < 9) {
        //            if (RecruitQueue.Contains(_senderName)) {
        //                sendTell(_senderID, $"[VT Fellow Manager] You are already number {RecruitQueue.IndexOf(_senderName)} of {RecruitQueue.Count} on the waiting list.  -v-");
        //                return true;
        //            }
        //            RecruitQueue.Add(_senderName);
        //            RecruitQueueEmpty = false;
        //            sendTell(_senderID, $"[VT Fellow Manager] I will recruit you in a moment. Please stand close to me.  -v-");
        //            return true;
        //        }
        //        //if (IsLeader) {
        //        //    sendTell(_senderID, $"[VT Fellow Manager] The fellow is full, and I am the leader. I am adding you to the waiting list at position {0}  -v-");
        //        //    sendTell(_senderID, $"[VT Fellow Manager] Please wait nearby. I will try to recruit you in a moment.  -v-");
        //        //    if (RecruitQueue.Contains(_senderName)) {
        //        //        sendTell(_senderID, $"[VT Fellow Manager] You are already number {RecruitQueue.IndexOf(_senderName)} of {RecruitQueue.Count} on the waiting list.  -v-");
        //        //        return true;
        //        //    }
        //        //    sendTell(_senderID, $"[VT Fellow Manager] Please wait nearby. I will try to recruit you in a moment.  -v-");
        //        //    sendTell(_senderID, $"[VT Fellow Manager] Please wait nearby. I will try to recruit you in a moment.  -v-");
        //        //    return true;
        //        //}
        //        sendTell(_senderID, $"[VT Fellow Manager] I'm sorry, but the fellowship is full. The leader is currently: {Leader}  -v-");
        //        return true;
        //    }
        //    #endregion
        //    //
        //    var msg = _msg.Split(new char[] { ' ' }, 2);
        //    switch (msg[0]) {
        //        #region (chat message) status
        //        case "status":
        //            sendTell(_senderID, $"[VT Fellow Manager] The leader is currently: {Leader}. The fellowship is {(Open ? "open" : "closed")}.  -v-");
        //            sendTell(_senderID, $"[VT Fellow Manager] There is no waiting list. The fellowship has {0} members.  -v-");
        //            return true;
        //        #endregion
        //        #region (chat message) remove
        //        case "remove":
        //            sendTell(_senderID, $"[VT Fellow Manager] You have been removed from the list.  -v-");
        //            return true;
        //        #endregion
        //        #region (chat message) leader
        //        case "leader":
        //            if (IsLeader) {
        //                sendTell(_senderID, $"[VT Fellow Manager] I am the fellowship leader. The fellowship is {(Open ? "open" : "closed")}.  -v-");
        //                return true;
        //            }
        //            sendTell(_senderID, $"[VT Fellow Manager] The leader is currently: {Leader}. The fellowship is {(Open ? "open" : "closed")}.  -v-");
        //            return true;
        //        #endregion
        //        #region (chat message) location
        //        case "location":
        //            unsafe {
        //                if (IsInFellowship(_senderID)) {
        //                    sendTell(_senderID, $"[VT Fellow Manager] I am currently located in landcell: {(*CPhysicsObj.player_object)->m_position.objcell_id:X8}  -v-");
        //                    return true;
        //                }
        //                sendTell(_senderID, $"[VT Fellow Manager] Sorry, I can only send my location to members of the fellowship.  -v-");
        //                return true;
        //            }
        //        #endregion
        //        #region (chat message) help
        //        case "help":
        //            sendTell(_senderID, $"[VT Fellow Manager] Available commands: xp, leader, location, status, help  -v-");
        //            return true;
        //            #endregion
        //    }

        //    return false;
        //}
        //#endregion

        #region Wrappers to AcClient
        #region bool InFellowship { get; } // Returns true if the the player is in a fellowship
        public unsafe bool InFellowship {
            get => ((*ClientFellowshipSystem.s_pFellowshipSystem)->m_pFellowship != null);
        }
        #endregion
        #region string Name { get; } // Returns the name of the current fellowship
        public unsafe new string Name {
            get => (InFellowship ? (*ClientFellowshipSystem.s_pFellowshipSystem)->m_pFellowship->a0._name.ToString() : "");
        }
        #endregion
        #region uint Leader { get; set; } // get: Returns the current fellowship leader's character id
        // set: Give the leader position of the current fellowship to character_id. This only works if you are the fellowship leader.
        public unsafe uint Leader {
            get => (InFellowship ? (*ClientFellowshipSystem.s_pFellowshipSystem)->m_pFellowship->a0._leader : 0);
            set {
                if (!InFellowship) {
                    return;
                }
                if (!IsLeader) {
                    return;
                }
                if (*CPhysicsPart.player_iid == value) {
                    return;
                }
                if ((*ClientFellowshipSystem.s_pFellowshipSystem)->m_pFellowship->a0.IsFellow(value) == 0) {
                    return;
                }
                CM_Fellowship.Event_AssignNewLeader(value);
            }
        }
        #endregion
        #region bool IsLeader { get; } // Returns true if the player is the leader of the current fellowship
        public unsafe bool IsLeader { get => Leader == *CPhysicsPart.player_iid; }
        #endregion
        #region uint MemberCount { get; } // Returns the number of players in the current fellowship (very cheap)
        public unsafe uint MemberCount { get => (InFellowship ? (*ClientFellowshipSystem.s_pFellowshipSystem)->m_pFellowship->a0._fellowship_table._currNum : 0); }
        #endregion
        #region bool ShareXP { get; } // Returns true if the current fellowship is sharing xp
        public unsafe bool ShareXP { get => !InFellowship || (*ClientFellowshipSystem.s_pFellowshipSystem)->m_pFellowship->a0._share_xp == 1; }
        #endregion
        #region bool EvenXPSplit { get; } //  Returns true if the current fellowship is evenly splitting xp
        public unsafe bool EvenXPSplit { get => !InFellowship || (*ClientFellowshipSystem.s_pFellowshipSystem)->m_pFellowship->a0._even_xp_split == 1; }
        #endregion
        #region bool Open { get; set; } // Gets or Sets the current fellowship Openess. Set only works if you are the fellowship leader.
        public unsafe bool Open {
            get => !InFellowship || (*ClientFellowshipSystem.s_pFellowshipSystem)->m_pFellowship->a0._open_fellow == 1;
            set {
                if (!InFellowship) return;
                if (!IsLeader) return;
                if (value) {
                    if (Open) return;
                    CM_Fellowship.Event_ChangeFellowOpeness(1);
                }
                else {
                    if (!Open) return;
                    CM_Fellowship.Event_ChangeFellowOpeness(0);
                }
            }
        }
        #endregion
        #region bool Locked { get; } // Returns true if the current fellowship is Locked.
        public unsafe bool Locked { get => !InFellowship || (*ClientFellowshipSystem.s_pFellowshipSystem)->m_pFellowship->a0._locked == 1; }
        #endregion
        #region bool IsInFellowship(uint character_id) // Returns true if character_id is in your fellowship
        public unsafe bool IsInFellowship(uint character_id) {
            if (!InFellowship) return false;

            return (*ClientFellowshipSystem.s_pFellowshipSystem)->m_pFellowship->a0.IsFellow(character_id) != 0;
        }
        #endregion
        #region void Disband() // Disband the current ac fellowship
        public unsafe void Disband() {
            if (!InFellowship) return;
            if (!IsLeader) return;
            AcClient.CM_Fellowship.Event_Quit(1);
        }
        #endregion
        #region void Quit() // Quit the current ac fellowship
        public unsafe void Quit() {
            if (!InFellowship) return;
            CM_Fellowship.Event_Quit(0);
        }
        #endregion
        #region void Dismiss(uint character_id) // Dismiss a character from the fellowship.  Only works if you are the fellowship leader.
        public unsafe void Dismiss(uint character_id) {
            if (character_id == *CPhysicsPart.player_iid) { // workaround for Ace's janky behaviors.
                Quit();
                return;
            }
            if (!InFellowship) return;
            if (!IsLeader) return;
            if (!IsInFellowship(character_id)) return;
            CM_Fellowship.Event_Dismiss(character_id);
        }
        #endregion
        #region void Recruit(uint character_id) // Recruit a character into the current ac fellowship. The fellowship must be open, or you must be the leader.
        public unsafe void Recruit(uint character_id) {
            if (!InFellowship) return;
            if ((!IsLeader && !Open)) return;
            if (IsInFellowship(character_id)) return;

            var physics = (*CObjectMaint.s_pcInstance)->GetObjectA(character_id);
            if (physics == null) return;


            float range = physics->player_distance;
            if (range > 75) return;
            CM_Fellowship.Event_Recruit(character_id);
        }
        #endregion
        #region void Update(bool i_on) // Request that the server (i_on?) sends periodic fellowship updates

        /// <summary>
        /// Request a fellowship update from the server
        /// </summary>
        /// <param name="i_on">Set to true to get full data (player vitals)</param>
        public unsafe void Update(bool i_on) {
            if (!InFellowship) return;

            CM_Fellowship.Event_UpdateRequest((i_on ? 1 : 0));
        }
        #endregion
        #region void Create(string _name) // create fellowship with name
        public unsafe void Create(string _name) {
            if (InFellowship) return;
            if (string.IsNullOrEmpty(_name)) return;
            AC1Legacy.PStringBase<byte> pStringBase = _name.TrimEnd('\0') + '\0';

            int share_xp = (int)(*CPlayerSystem.s_pPlayerSystem)->playerModule.PlayerModule.FellowshipShareXP();
            CM_Fellowship.Event_Create(&pStringBase, share_xp);

        }
        #endregion
        #endregion

        #region Expressions
        #region getfellowshipstatus[]
        [ExpressionMethod("getfellowshipstatus")]
        [ExpressionReturn(typeof(double), "Returns 1 if you are in a fellowship, 0 otherwise")]
        [Summary("Checks if you are currently in a fellowship")]
        [Example("getfellowshipstatus[]", "returns 1 if you are in a fellowship, 0 otherwise")]
        public object Getfellowshipstatus() {
            return InFellowship;
        }
        #endregion
        #region getfellowshipname[]
        [ExpressionMethod("getfellowshipname")]
        [ExpressionReturn(typeof(string), "Returns the name of a fellowship, or an empty string if none")]
        [Summary("Gets the name of your current fellowship")]
        [Example("getfellowshipname[]", "returns the name of your current fellowship")]
        public object Getfellowshipname() {
            return Name;
        }
        #endregion
        #region getfellowshipcount[]
        [ExpressionMethod("getfellowshipcount")]
        [ExpressionReturn(typeof(double), "Returns the number of members in your fellowship, or 0 if you are not in a fellowship")]
        [Summary("Gets the name of your current fellowship")]
        [Example("getfellowshipcount[]", "Returns the number of members in your fellowship, or 0 if you are not in a fellowship")]
        public object GetFellowshipCount() {
            return MemberCount;
        }
        #endregion
        #region getfellowshipleaderid[]
        [ExpressionMethod("getfellowshipleaderid")]
        [ExpressionReturn(typeof(double), "Returns the character id of the fellowship leader")]
        [Summary("Returns the character id of the fellowship leader")]
        [Example("getfellowshipleaderid[]", "Returns the character id of the fellowship leader")]
        public object GetFellowshipLeaderID() {
            return Leader;
        }
        #endregion
        #region getfellowid[x]
        [ExpressionMethod("getfellowid")]
        [ExpressionParameter(0, typeof(double), "iterator", "fellowship member to return (0-8)")]
        [ExpressionReturn(typeof(double), "Character ID of requested Fellowship Member, or 0")]
        [Summary("Returns a fellowship member's character id. It is recommended to save the output to a variable.")]
        [Example("getfellowid[0]", "Returns the id of the first Fellowship Member")]
        public unsafe object GetfellowID(double value) {
            if (value < MemberCount)
                return (double)(*ClientFellowshipSystem.s_pFellowshipSystem)->m_pFellowship->a0._fellowship_table.GetByIndex((int)value)->_key;
            return 0;
        }
        #endregion
        #region getfellowname[x]
        [ExpressionMethod("getfellowname")]
        [ExpressionParameter(0, typeof(double), "iterator", "fellowship member to return (0-8)")]
        [ExpressionReturn(typeof(string), "Character name of requested Fellowship Member, or \"\"")]
        [Summary("Returns a fellowship member's character name. It is recommended to save the output to a variable.")]
        [Example("getfellowname[0]", "Returns the name of the first Fellowship Member")]
        public unsafe object GetfellowName(double value) {
            if (value < MemberCount)
                return (*ClientFellowshipSystem.s_pFellowshipSystem)->m_pFellowship->a0._fellowship_table.GetByIndex((int)value)->_data._name.ToString();
            return "";
        }
        #endregion
        #region getfellowshiplocked[]
        [ExpressionMethod("getfellowshiplocked")]
        [ExpressionReturn(typeof(double), "Returns 1 if your fellowship is Locked, 0 otherwise")]
        [Summary("Returns 1 if your fellowship is Locked")]
        [Example("getfellowshiplocked[]", "Returns 1 if your fellowship is Locked")]
        public object GetfellowshipLocked() {
            return Locked;
        }
        #endregion
        #region getfellowshipisleader[]
        [ExpressionMethod("getfellowshipisleader")]
        [ExpressionReturn(typeof(double), "Returns 1 if you are the leader of your fellowship, 0 otherwise")]
        [Summary("Returns 1 if you are the leader of your fellowship")]
        [Example("getfellowshipisleader[]", "Returns 1 if you are the leader of your fellowship")]
        public object GetfellowshipIsLeader() {
            return IsLeader;
        }
        #endregion
        #region getfellowshipisopen[]
        [ExpressionMethod("getfellowshipisopen")]
        [ExpressionReturn(typeof(double), "Returns 1 if your fellowship is open, 0 otherwise")]
        [Summary("Returns 1 if your fellowship is open")]
        [Example("getfellowshipisopen[]", "Returns 1 if your fellowship is open")]
        public object GetfellowshipIsOpen() {
            return Open;
        }
        #endregion
        #region getfellowshipisfull[]
        [ExpressionMethod("getfellowshipisfull")]
        [ExpressionReturn(typeof(double), "Returns 1 if you're in a fellowship and it is full. 0 otherwise")]
        [Summary("Returns 1 if you're in a fellowship, and it is full")]
        [Example("getfellowshipisfull[]", "Returns 1 if you're in a fellowship, and it is full")]
        public object GetfellowshipIsFull() {
            return MemberCount == 9;
        }
        #endregion
        #region getfellowshipcanrecruit[]
        [ExpressionMethod("getfellowshipcanrecruit")]
        [ExpressionReturn(typeof(double), "Returns 1 if you're in a fellowship, and are the leader, or the fellowship is open, and the fellowship is not full")]
        [Summary("Returns 1 if your fellowship is open, or you are the leader, and the fellowship is not full")]
        [Example("getfellowshipcanrecruit[]", "Returns 1 if you're in a fellowship, and are the leader, or the fellowship is open, and the fellowship is not full")]
        public object GetfellowshipCanRecruit() {
            return (IsLeader || Open) && MemberCount < 9;
        }
        #endregion
        #region getfellownames[]
        [ExpressionMethod("getfellownames")]
        [ExpressionReturn(typeof(ExpressionList), "List of character names in the fellowship, sorted alphabetically")]
        [Summary("Returns a list of character names in your current fellowship, sorted alphabetically.")]
        [Example("getfellownames[]", "Returns a list of character names in your current fellowship, sorted alphabetically")]
        public unsafe object GetfellowNames() {
            var list = new ExpressionList();
            if (MemberCount > 0) {
                List<string> members = new List<string>();
                var table = (*ClientFellowshipSystem.s_pFellowshipSystem)->m_pFellowship->a0._fellowship_table;
                for (int i = 0; i < MemberCount; i++) members.Add(table[i]->_data._name.ToString());
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
        public unsafe object GetfellowIds() {
            var list = new ExpressionList();
            if (MemberCount > 0) {
                var table = (*ClientFellowshipSystem.s_pFellowshipSystem)->m_pFellowship->a0._fellowship_table;
                for (int i = 0; i < MemberCount; i++) list.Items.Add((double)table[i]->_key);
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
        public unsafe void DoFellow(string _, Match args) {
            string[] a = args.Value.Split(' ');
            string Name = "";
            if (!InFellowship && !a[0].Equals("create")) {
                Logger.WriteToChat($"Your are not currently in a fellowship.");
                return;
            }

            switch (a[0]) {
                //[Summary("Creates a fellowship")]
                //[Usage("/ub fellow create <name>")]
                //[Example("/ub fellow create trevis-haters' club", "Creates a fellowship named `trevis-haters' club`")]
                //[CommandPattern("fellow create", @"^(?<Name>.*)$")]
                case "create":
                    if (InFellowship) { Logger.WriteToChat($"You are already in a fellowship."); return; }
                    Create(args.Value.Substring(7));
                    break;

                //[Summary("Leaves your current fellowship")]
                //[Usage("/ub fellow quit")]
                //[CommandPattern("fellow quit", @"^$")]
                case "quit":
                    Quit();
                    break;

                //[Summary("Disbands your current fellowship")]
                //[Usage("/ub fellow disband")]
                //[CommandPattern("fellow disband", @"^$")]
                case "disband":
                    if (!IsLeader) { Logger.WriteToChat("You are not the fellowship leader!"); return; }
                    Disband();
                    break;

                //[Summary("Changes your current fellowship to Open")]
                //[Usage("/ub fellow open")]
                //[CommandPattern("fellow open", @"^$")]
                case "open":
                    Open = true;
                    break;

                //[Summary("Changes your current fellowship to Closed")]
                //[Usage("/ub fellow close")]
                //[CommandPattern("fellow close", @"^$")]
                case "close":
                    Open = false;
                    break;


                //[Summary("Shows the status, and a list of the members in your current fellowship")]
                //[Usage("/ub fellow status")]
                //[CommandPattern("fellow status", @"^$")]
                case "status":
                    var c = &(*ClientFellowshipSystem.s_pFellowshipSystem)->m_pFellowship->a0;
                    Logger.WriteToChat($"{Leader:X8} {(*(CPhysicsPart.player_iid)):X8} Your current fellowship, \"{this.Name}\", has {MemberCount} member{(MemberCount != 1 ? "s" : "")}, {(ShareXP ? "" : "NOT ")}Sharing XP{(EvenXPSplit ? "" : ", Uneven Split")}. {(Open ? "Open" : "Closed")}, {(Locked ? "**LOCKED**" : "Not Locked")}.");
                    uint leaderId = Leader;
                    for (int i = 0; i < MemberCount; i++) {
                        var thisOne = c->_fellowship_table.GetByIndex(i);
                        Logger.WriteToChat($" {thisOne->_data._name}[{thisOne->_data._level}] H:{thisOne->_data._current_health}/{thisOne->_data._max_health}{(leaderId == thisOne->_key ? " (Leader) " : "")}");
                    }
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
                Recruit((uint)fellowChar.Id);
                return;
            }
            if (UB.Plugin.Debug) Logger.Error($"Could not find {(Name == null ? "closest player" : $"player {Name}")}");
        }
        public void DoDismiss(string Name, bool partial) {
            PackableHashData<uint, Fellow>? fellowChar = FindName(Name, partial);
            if (fellowChar != null) {
                if (UB.Plugin.Debug) Logger.WriteToChat($"Dismissing {fellowChar.Value._data._name}[0x{fellowChar.Value._key:X8}]");
                Dismiss(fellowChar.Value._key);
                return;
            }
            if (UB.Plugin.Debug) Logger.Error($"Could not find {(Name == null ? "closest player" : $"player {Name}")}");
        }
        public void DoLeader(string Name, bool partial) {
            PackableHashData<uint, Fellow>? fellowChar = FindName(Name, partial);
            if (fellowChar != null) {
                if (UB.Plugin.Debug) Logger.WriteToChat($"Transferring leader to {fellowChar.Value._data._name}[0x{fellowChar.Value._key:X8}]");
                Leader = fellowChar.Value._key;
                return;
            }
            if (UB.Plugin.Debug) Logger.Error($"Could not find {(Name == null ? "closest player" : $"player {Name}")}");
        }
        #endregion
        #endregion

        #region PackableHashData<uint, Fellow>? FindName(string searchname, bool partial)
        /// <summary>
        /// Find a character by name, from the current fellowship, excluding the player (unless searching specifically by ID, or "selected")
        /// </summary>
        /// <param name="searchname">if blank, finds the closest member in the fellowship that is not the player. if "selected", attempts to return the selected player. You can find the player "Selected" by using appropriate capitalization. if numeric or hexadecimal, attempts to return the given character</param>
        /// <param name="partial">allow partial name matches (blank name overrides this)</param>
        /// <returns></returns>
        public unsafe PackableHashData<uint, Fellow>? FindName(string searchname, bool partial) {
            if (!InFellowship) return null;
            var table = (*ClientFellowshipSystem.s_pFellowshipSystem)->m_pFellowship->a0._fellowship_table;
            if (uint.TryParse(searchname, out uint id))
                if (table.Contains(id))
                    return *table.lookup(id);
            try {
                uint intValue = Convert.ToUInt32(searchname, 16);
                if (table.Contains(intValue))
                    return *table.lookup(intValue);
            }
            catch { }
            if (searchname.Equals("selected") && CoreManager.Current.Actions.CurrentSelection != 0 && table.Contains(*ACCWeenieObject.selectedID)) {
                return *table.lookup(*ACCWeenieObject.selectedID);
            }
            searchname = searchname.ToLower();
            PackableHashData<uint, Fellow>? found = null;
            double lastDistance = double.MaxValue;
            double thisDistance;
            for (int i = 0; i < MemberCount; i++) {
                var thisOne = table.GetByIndex(i);
                thisDistance = (*CObjectMaint.s_pcInstance)->GetObjectA(thisOne->_key)->player_distance;
                if (thisOne->_key != *CPhysicsPart.player_iid && (found == null || lastDistance > thisDistance)) {
                    string thisLowerName = thisOne->_data._name.ToString().ToLower();
                    if (partial && (searchname.Length == 0 || thisLowerName.Contains(searchname))) {
                        found = *thisOne;
                        lastDistance = thisDistance;
                    }
                    else if ((searchname.Length == 0 || thisLowerName.Equals(searchname))) {
                        found = *thisOne;
                        lastDistance = thisDistance;
                    }
                }
            }
            return found;
        }
        #endregion

    }
}
