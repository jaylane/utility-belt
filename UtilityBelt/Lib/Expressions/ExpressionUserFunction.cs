using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UtilityBelt.Lib.Models;

namespace UtilityBelt.Lib.Expressions {
    public class ExpressionUserFunction : ExpressionObjectBase {
        public List<string> ArgNames { get; set; }
        public string Body { get; set; }

        public CompiledExpression Compiled { get; set; }

        public ExpressionUserFunction() {
            IsSerializable = true;
        }

        public ExpressionUserFunction(List<string> argNames, string body) {
            IsSerializable = true;
            ShouldFireEvents = false;
            ArgNames = argNames;
            Body = body;
            ShouldFireEvents = true;
            try {
                Compiled = UtilityBeltPlugin.Instance.VTank.CompileExpression(Body);
            }
            catch (Exception ex) {
                Logger.WriteToChat($"Failed to compile user expression function: {this} : {ex.Message}");
            }
        }

        public object Call(List<object> args=null) {
            if (args == null) args = new List<object>();

            try {
                object oldSpecial = null;
                var oldVars = new Dictionary<string, object>();
                for (var i = 0; i < ArgNames.Count; i++) {
                    if (UtilityBeltPlugin.Instance.VTank.Testvar(ArgNames[i]) == (object)true) {
                        oldVars.Add(ArgNames[i], UtilityBeltPlugin.Instance.VTank.Getvar(ArgNames[i]));
                    }
                    UtilityBeltPlugin.Instance.VTank.Setvar(ArgNames[i], args.Count > i ? args[i] : 0);
                }

                if (UtilityBeltPlugin.Instance.VTank.Testvar("_") == (object)true) {
                    oldSpecial = UtilityBeltPlugin.Instance.VTank.Getvar("_");
                }
                UtilityBeltPlugin.Instance.VTank.Setvar("_", new ExpressionList(args));

                var res = Compiled.Run();

                for (var i = 0; i < ArgNames.Count; i++) {
                    if (oldVars.ContainsKey(ArgNames[i])) {
                        UtilityBeltPlugin.Instance.VTank.Setvar(ArgNames[i], oldVars[ArgNames[i]]);
                    }
                    else {
                        UtilityBeltPlugin.Instance.VTank.Clearvar(ArgNames[i]);
                    }
                }
                if (oldSpecial != null) {
                    UtilityBeltPlugin.Instance.VTank.Setvar("_", oldSpecial);
                }
                else {
                    UtilityBeltPlugin.Instance.VTank.Clearvar("_");
                }

                return res;
            }
            catch (Exception ex) {
                Logger.WriteToChat($"Failed to run user function: {this} : {ex.Message}");
            }

            return 0;
        }

        public override string ToString() {
            return $"({string.Join(", ", ArgNames)}) => {{ {Body.Replace("\r"," ").Replace("\n"," ").Trim(' ')} }}";
        }

        public override bool Equals(object obj) {
            return false;
        }

        public override int GetHashCode() {
            return 2108858624 + ArgNames.GetHashCode() + Body.GetHashCode();
        }
    }
}
