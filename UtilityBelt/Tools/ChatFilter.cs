using System;
using Decal.Adapter;
using Decal.Filters;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Chat;
using System.Reflection;
using UtilityBelt.Lib.Settings.Chat;
using UBService.Lib.Settings;

namespace UtilityBelt.Tools {
    [Name("ChatFilter")]
    [Summary("Filters various chat messages")]
    [FullDescription(@"
Choose specific filters to hide chat line messages.
    ")]
    public class ChatFilter : ToolBase {

        public ChatFilter(UtilityBeltPlugin ub, string name) : base(ub, name) {
        }

        public List<PropertyInfo[]> options = new List<PropertyInfo[]>();
        private Dictionary<string, ISetting> filterSettingsCache = new Dictionary<string, ISetting>();


        public override void Init() {
            base.Init();
            
            foreach (var setting in GetChildren()) {
                foreach (var cSetting in setting.GetChildren())
                    filterSettingsCache.Add(cSetting.Name, cSetting);
            }

            UB.ChatHandler.ChatBoxMessage += ChatHandler_ChatBoxMessage;
        }


        #region Config

        [Summary("Enabled")]
        public Setting<bool> Enabled = new Setting<bool>(false);

        [Summary("AttackOnYou")]
        public readonly AttackOnYouOption AttackOnYou = new AttackOnYouOption(false, false, false, false, false, false, false, false);
        
        [Summary("AttackOnTarget")]
        public readonly AttackOnTargetOption AttackOnTarget = new AttackOnTargetOption(false, false, false, false, false);

        [Summary("Misc")]
        public readonly MiscOption Misc = new MiscOption(false, false, false, false, false, false);

        [Summary("YouCast")]
        public readonly YouCastOption YouCast = new YouCastOption(false, false, false, false, false, false, false, false);
        
        [Summary("OthersCast")]
        public readonly OthersCastOption OthersCast = new OthersCastOption(false, false, false, false);
        
        [Summary("Recharge")]
        public readonly RechargeOption Recharge = new RechargeOption(false, false, false);

        [Summary("Message")] //This is broken right now - GDLE and ACE packets don't match and NPC chat cannot be eaten
        public readonly MessageOption Message = new MessageOption(false, false);

        #endregion

        #region Commands

        [Summary("Filter or unfilter all chat messages")]
        [Usage("/ub chatfilter (recharge|otherscast|youcast|misc|attackonyou|attackontarget|all|test) (true|false)")]
        [Example("/ub chatfilter recharge true", "filters all recharge group chat messages")]
        [CommandPattern("chatfilter", @"^ *(?<option>(recharge|otherscast|youcast|misc|attackonyou|attackontarget|all|test)) (?<toggle>(true|false))$", true)]
        public void FilterAllCommand(string command, Match args) {
            var option = args.Groups["option"].Value.ToLower();
            var toggle = args.Groups["toggle"].Value.ToLower();
            bool.TryParse(toggle, out bool toggleBool);

            if (option == "all") {
                foreach (var setting in GetChildren()) {
                    if (setting.HasChildren())
                        UpdateFieldSettings(setting.GetChildren(), toggleBool);
                }
            }
            else {
                var setting = Settings.GetSetting($"ChatFilter.{option}");
                UpdateFieldSettings(setting.GetChildren(), toggleBool);
            }
        }

        #endregion

        private void ChatHandler_ChatBoxMessage(object sender, MyBaseEventArgs e) {
            try {
                if (filterSettingsCache.ContainsKey(e.ChatMessageType.ToString()) && (bool)filterSettingsCache[e.ChatMessageType.ToString()].GetValue() == true)
                    e.DecalChatEvent.Eat = true;
            }
            catch (Exception ex) {
                Logger.LogException(ex);
            }
        }
        
        public void UpdateFieldSettings(IEnumerable<ISetting> settings, bool filter) {
            foreach (ISetting setting in settings) {
                setting.SetValue(filter);
            }
        }
        
        protected override void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    UB.ChatHandler.ChatBoxMessage -= ChatHandler_ChatBoxMessage;
                    base.Dispose(disposing);
                }
                disposedValue = true;
            }
        }

    }
}
