using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace UtilityBelt.Lib.Expressions {
    class ExpressionVisitor : MetaExpressionsBaseVisitor<object> {
        /// <summary>
        /// Generic Visitor, this gets called on everything and will convert types to meta expression compatible types
        /// </summary>
        /// <param name="tree"></param>
        /// <returns></returns>
        ///
        public override object Visit([NotNull] IParseTree tree) {
            object v;
            try {
                v = base.Visit(tree);
            }
            catch (NullReferenceException) {
                return (double)0;
            }

            if (v == null)
                return (double)0;

            // all bools should be doubles
            if (v.GetType() == typeof(bool))
                return (double)(((bool)v) ? 1 : 0);

            // all numbers should be doubles
            if (v.GetType() == typeof(int))
                return Convert.ToDouble(v);
            if (v.GetType() == typeof(float))
                return Convert.ToDouble(v);
            if (v.GetType() == typeof(long))
                return Convert.ToDouble(v);
            if (v.GetType() == typeof(short))
                return Convert.ToDouble(v);
            if (v.GetType() == typeof(byte))
                return Convert.ToDouble(v);

            return v;
        }


        /// <summary>
        /// Entry parse rule. This will run multiple statements seperated by semicolon
        /// </summary>
        /// <param name="context"></param>
        /// <returns>Returns the result of the last expression</returns>
        public override object VisitParse([NotNull] MetaExpressionsParser.ParseContext context) {
            object lastRet = 0;

            for (var i = 0; i < context.expression().Length; i++)
                lastRet = Visit(context.expression(i));

            return lastRet;
        }

        /// <summary>
        /// Handle numeric atoms
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override object VisitNumericAtomExp(MetaExpressionsParser.NumericAtomExpContext context) {
            return double.Parse(context.NUMBER().GetText(), System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Handle string atoms, this includes removing quotes or escaped characters
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>

        public override object VisitStringAtomExp(MetaExpressionsParser.StringAtomExpContext context) {
            var str = context.GetText();
            if (str.StartsWith("`"))
                return str.Trim('`');
            else
                return Regex.Replace(str, @"\\(.)", "$1");
        }

        /// <summary>
        /// Handle bool atoms
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override object VisitBoolAtomExp(MetaExpressionsParser.BoolAtomExpContext context) {
            return (context.GetText().ToLower().Trim() == "true");
        }

        /// <summary>
        /// Handle parenthesis
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override object VisitParenthesisExp(MetaExpressionsParser.ParenthesisExpContext context) {
            return Visit(context.expression());
        }

        /// <summary>
        /// Handle meta expression function calls
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>

        public override object VisitFunctionCall([NotNull] MetaExpressionsParser.FunctionCallContext context) {
            var methodName = context.ID().GetText().Replace("[", "");
            var arguments = new List<object>();

            for (var i = 0; i < context.expressionList().expression().Length; i++) {
                var val = Visit(context.expressionList().expression(i));
                arguments.Add(val);
            }

            var key = $"{methodName}:{arguments.Count}";

            // TODO: argument type checking?

            if (UtilityBeltPlugin.Instance.RegisteredExpressions.ContainsKey(key)) {
                var v = UtilityBeltPlugin.Instance.RunExpressionMethod(UtilityBeltPlugin.Instance.RegisteredExpressions[key], arguments.ToArray());

                if (v.GetType() == typeof(bool)) {
                    return ((bool)v == true) ? 1 : 0;
                }

                return v;
            }
            else {
                throw new Exception($"Unknown expression method: {key}");
            }
        }

        #region Operators
        #region Operator Helpers
        /// <summary>
        /// Checks if an object is "truthy"
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        private bool IsTruthy(object obj) {
            if (obj.GetType() == typeof(double))
                return (double)obj != 0;
            if (obj.GetType() == typeof(string))
                return ((string)obj).Length > 0;

            return true;
        }
        #endregion

        /// <summary>
        /// Handle bbolean comparison operators like && and ||
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override object VisitBooleanComparisonExp(MetaExpressionsParser.BooleanComparisonExpContext context) {
            object left = Visit(context.expression(0));
            object right = Visit(context.expression(1));

            if (context.AND() != null) {
                if (!IsTruthy(left)) return false;
                if (!IsTruthy(right)) return false;
                return true;
            }
            if (context.OR() != null) {
                if (IsTruthy(left)) return true;
                if (IsTruthy(right)) return true;
                return false;
            }

            return false;
        }

        /// <summary>
        /// Handle comparison operators like >, >=, ==, etc
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override object VisitComparisonExp(MetaExpressionsParser.ComparisonExpContext context) {
            object left = Visit(context.expression(0));
            object right = Visit(context.expression(1));

            if (context.EQTO() != null) {
                if (left.GetType() == typeof(string))
                    return left.ToString().ToLower().Equals(right.ToString().ToLower());
                return left.Equals(right);
            }
            if (context.NEQTO() != null) {
                if (left.GetType() == typeof(string))
                    return !left.ToString().ToLower().Equals(right.ToString().ToLower());
                return !left.Equals(right);
            }

            if (left.GetType() != typeof(double) || right.GetType() != typeof(double))
                throw new Exception("Invalid comparison of non number types");

            if (context.LT() != null)
                return (double)left < (double)right;
            if (context.GT() != null)
                return (double)left > (double)right;
            if (context.LTEQTO() != null)
                return (double)left <= (double)right;
            if (context.GTEQTO() != null)
                return (double)left >= (double)right;

            return false;
        }

        /// <summary>
        /// Handle multiplication and division operators
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override object VisitMulDivExp(MetaExpressionsParser.MulDivExpContext context) {
            double left = (double)Visit(context.expression(0));
            double right = (double)Visit(context.expression(1));
            double result = 0;

            if (context.ASTERISK() != null)
                result = left * right;
            if (context.SLASH() != null)
                result = left / right;

            return result;
        }

        /// <summary>
        /// Handle modulo operator
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override object VisitModuloExp(MetaExpressionsParser.ModuloExpContext context) {
            double left = (double)Visit(context.expression(0));
            double right = (double)Visit(context.expression(1));
            return left % right;
        }

        /// <summary>
        /// Handle addition and subtraction operators
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override object VisitAddSubExp(MetaExpressionsParser.AddSubExpContext context) {
            object left = (object)Visit(context.expression(0));
            object right = (object)Visit(context.expression(1));

            if (context.PLUS() != null) {
                if (left.GetType() == typeof(double))
                    return (double)left + (double)right;
                if (left.GetType() == typeof(string))
                    return (string)left + right.ToString();
            }

            if (context.MINUS() != null && left.GetType() == typeof(double)) {
                return (double)left - (double)right;
            }

            return null;
        }

        /// <summary>
        /// Handle power operation (^)
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override object VisitPowerExp(MetaExpressionsParser.PowerExpContext context) {
            double left = (double)Visit(context.expression(0));
            double right = (double)Visit(context.expression(1));
            double result = 0;

            result = Math.Pow(left, right);

            return result;
        }

        /// <summary>
        /// Handle regex operation, this handles expr#regex format
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override object VisitRegexExp([NotNull] MetaExpressionsParser.RegexExpContext context) {
            var inputstr = Visit(context.expression(0)).ToString();
            var matchstr = Visit(context.expression(1)).ToString();
            var re = new Regex(matchstr, RegexOptions.IgnoreCase);
            return re.IsMatch(inputstr);
        }
        #endregion

        public override object VisitErrorNode([NotNull] IErrorNode node) {
            Logger.Error($"Some error or something: ({node.Payload}) {node.GetText()}");
            return base.VisitErrorNode(node);
        }
    }
}
