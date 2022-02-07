using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UtilityBelt.Lib.Expressions;

namespace UtilityBelt.Lib.Models {
    public class CompiledExpression {
        MetaExpressionsParser.ParseContext Context { get; set; }
        ExpressionVisitor Visitor { get; set; }

        public CompiledExpression(MetaExpressionsParser.ParseContext context, ExpressionVisitor visitor) {
            Context = context;
            Visitor = visitor;
        }

        public object Run() {
            try {
                var result = Visitor.Visit(Context);

                // check for any modifications to global/persistent variables that were accessed,
                // and save them to the database
                foreach (var kv in Visitor.State.GlobalVariables) {
                    if (kv.Value is ExpressionObjectBase obj && obj.HasChanges)
                        UtilityBeltPlugin.Instance.VTank.Setgvar(kv.Key, kv.Value);
                }

                foreach (var kv in Visitor.State.PersistentVariables) {
                    if (kv.Value is ExpressionObjectBase obj && obj.HasChanges)
                        UtilityBeltPlugin.Instance.VTank.Setpvar(kv.Key, kv.Value);
                }

                return result;
            }
            catch (Exception ex) {
                Logger.Error($"Error running string expression: {Context.GetText()}");
                Logger.Error(UtilityBeltPlugin.Instance.Plugin.Debug ? ex.ToString() : ex.Message.ToString());
                return 0;
            }
        }
    }
}
