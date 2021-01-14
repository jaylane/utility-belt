using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using UtilityBelt.Views;
using VirindiViewService.Controls;

namespace UtilityBelt.Lib.Actions {
    public class ChatExpressionAction : BaseAction {
        private LongStringEditor longStringEditor;

        public string Expression { get; set; } = "";

        public ChatExpressionAction() {
            Type = ActionType.ChatExpression;
        }

        public ChatExpressionAction(string expression) {
            Type = ActionType.ChatExpression;
            Expression = expression;
        }

        public override void Run() {
            var result = UtilityBeltPlugin.Instance.VTank.EvaluateExpression(Expression);
            Util.DispatchChatToBoxWithPluginIntercept(result.ToString());
        }

        public override string ToString() {
            return $"ChatExpression: {Expression}";
        }

        public override void DrawForm(HudFixedLayout layout) {
            var label = new HudStaticText();
            label.Text = "Expression:";

            var edit = new HudTextBox();
            edit.Text = Expression;
            edit.Change += (s, e) => {
                Expression = edit.Text;
            };

            var button = new HudButton();
            button.Text = "E";
            button.Hit += (s, e) => {
                if (longStringEditor == null || longStringEditor.IsDisposed)
                    longStringEditor = new LongStringEditor(UtilityBeltPlugin.Instance.MainView.view, edit);
            };

            Controls.Add(label);
            Controls.Add(edit);
            Controls.Add(button);

            layout.AddControl(label, new Rectangle(0, 0, 60, 16));
            layout.AddControl(edit, new Rectangle(60, 0, 315, 16));
            layout.AddControl(button, new Rectangle(380, 0, 16, 16));
        }

        public override void ClearForm(HudFixedLayout layout) {
            base.ClearForm(layout);
            longStringEditor?.Dispose();
        }
    }
}
