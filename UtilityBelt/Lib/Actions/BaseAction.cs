using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VirindiViewService.Controls;

namespace UtilityBelt.Lib.Actions {
    public enum ActionType {
        Expression = 0,
        ChatExpression = 1,
        ChatCommand = 2,
    }
    public abstract class BaseAction {

        public ActionType Type { get; set; }

        private List<HudControl> Controls { get; } = new List<HudControl>();

        public static BaseAction FromType(ActionType type) {
            switch (type) {
                case ActionType.Expression:
                    return new ExpressionAction();
                case ActionType.ChatExpression:
                    return new ChatExpressionAction();
                case ActionType.ChatCommand:
                    return new ChatCommandAction();
                default:
                    return null;
            }
        }

        public abstract void Run();

        public List<HudControl> GetControls() {
            return Controls;
        }

        public abstract void DrawForm(HudFixedLayout layout);

        public virtual void ClearForm(HudFixedLayout layout) {
            foreach (var control in Controls) {
                control.Visible = false;
                control.Dispose();
            }
            Controls.Clear();
        }

        public virtual BaseAction Clone() {
            var settings = UtilityBeltPlugin.Instance.Settings.SerializerSettings;
            return (BaseAction)JsonConvert.DeserializeObject(JsonConvert.SerializeObject(this, settings), settings);
        }
    }
}
