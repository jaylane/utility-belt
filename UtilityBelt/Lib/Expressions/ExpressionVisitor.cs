using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace UtilityBelt.Lib.Expressions {
    public class ExpressionVisitor : MetaExpressionsBaseVisitor<object> {
        public class ExpressionState {
            public Dictionary<string, object> PersistentVariables = new Dictionary<string, object>();
            public Dictionary<string, object> GlobalVariables = new Dictionary<string, object>();
        }

        public ExpressionState State = new ExpressionState();

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

            v = FixTypes(v);

            return v;
        }

        /// <summary>
        /// Returns a friendly version of the type, used for displaying to the user
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string GetFriendlyType(Type type) {
            if (type == typeof(string))
                return "string";
            if (type == typeof(bool))
                return "number";
            if (type == typeof(float))
                return "number";
            if (type == typeof(int))
                return "number";
            if (type == typeof(double))
                return "number";
            if (type == typeof(ParamArrayAttribute))
                return "...items";

            return type.ToString().Split('.').Last().Replace("Expression","");
        }

        public static object FixTypes(object v) {
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
            var parsed = double.Parse(context.NUMBER().GetText(), System.Globalization.CultureInfo.InvariantCulture);

            if (context.MINUS() != null)
                parsed *= -1;

            return parsed;
        }

        /// <summary>
        /// Handle hexidecimal atoms
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override object VisitHexNumberAtomExp([NotNull] MetaExpressionsParser.HexNumberAtomExpContext context) {
            return (double)Convert.ToInt32(context.HEXNUMBER().GetText(), 16);
        }

        /// <summary>
        /// Handle string atoms, this includes removing quotes or escaped characters
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>

        public override object VisitStringAtomExp(MetaExpressionsParser.StringAtomExpContext context) {
            var str = context.GetText();
            if (str.StartsWith("`"))
                return Regex.Replace(str.Substring(1, str.Length - 2), @"\\(.)", "$1");
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
            var methodName = context.STRING().GetText();
            var arguments = new List<object>();

            for (var i = 0; i < context.expressionList().expression().Length; i++) {
                var val = Visit(context.expressionList().expression(i));
                arguments.Add(val);
            }

            if (UtilityBeltPlugin.Instance.RegisteredExpressions.ContainsKey(methodName)) {
                var expressionMethod = UtilityBeltPlugin.Instance.RegisteredExpressions[methodName];
                var argTypes = expressionMethod.ArgumentTypes;
                var parameters = expressionMethod.Method.GetParameters();

                var isParamsKeyword = argTypes.Length > 0 && argTypes.Last() == typeof(ParamArrayAttribute);
                var hasDefaults = parameters.Length > 0 && parameters.Last().IsOptional;

                for (var i=0; i < parameters.Length; i++) {
                    // dependency injection
                    if (parameters[i].ParameterType == typeof(ExpressionState)) {
                        arguments.Insert(i, State);
                        continue;
                    }

                    // object type means allow anything
                    if (argTypes[i] == typeof(object))
                        continue;

                    // ParamArrayAttribute means params keyword for this argument
                    if (argTypes[i] == typeof(ParamArrayAttribute)) {
                        var paramArgs = arguments.Skip(i).ToArray();
                        arguments = arguments.Take(i).ToList();
                        arguments.Add(paramArgs);
                        break;
                    }

                    // default param and it wasn't passed
                    if (parameters[i].IsOptional && i > arguments.Count - 1) {
                        arguments.Add(parameters[i].DefaultValue);
                        continue;
                    }

                    if (i < arguments.Count && arguments[i].GetType() != argTypes[i]) {
                        throw new Exception($"{expressionMethod.Signature} expects argument #{i + 1}/{argTypes.Length} to be a {GetFriendlyType(argTypes[i])} but a {GetFriendlyType(arguments[i].GetType())} was passed instead. Passed value: {arguments[i].ToString()}");
                    }
                }

                if (arguments.Count == 0 && isParamsKeyword) {
                    arguments = new List<object>(new object[] { null });
                }

                if (arguments.Count != parameters.Length && !isParamsKeyword) {
                    throw new Exception($"{expressionMethod.Signature} expects {argTypes.Length} arguments. {arguments.Count} arguments were passed.");
                }
                var v = UtilityBeltPlugin.Instance.RunExpressionMethod(expressionMethod, arguments.ToArray());

                return FixTypes(v);
            }
            else {
                throw new Exception($"Unknown expression method: {methodName}");
            }
        }

        #region Operators
        #region Operator Helpers
        /// <summary>
        /// Checks if an object is "truthy"
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        internal static bool IsTruthy(object obj) {
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
            if (context.op.Text == "&&") {
                object left = Visit(context.expression(0));
                if (!IsTruthy(left)) return false;
                object right = Visit(context.expression(1));
                if (!IsTruthy(right)) return false;
                return right;
            }

            if (context.op.Text == "||") {
                object left = Visit(context.expression(0));
                if (IsTruthy(left)) return true;
                object right = Visit(context.expression(1));
                if (IsTruthy(right)) return true;
                return right;
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

            if (context.op.Text == "==") {
                if (left.GetType() == typeof(string))
                    return left.ToString().ToLower().Equals(right.ToString().ToLower());
                return left.Equals(right) ? 1 : 0;
            }
            if (context.op.Text == "!=") {
                if (left.GetType() == typeof(string))
                    return !left.ToString().ToLower().Equals(right.ToString().ToLower());
                return !left.Equals(right) ? 1 : 0;
            }

            if (left.GetType() != typeof(double) || right.GetType() != typeof(double))
                throw new Exception("Invalid comparison of non number types");

            if (context.op.Text == "<")
                return (double)left < (double)right ? 1 : 0;
            if (context.op.Text == ">")
                return (double)left > (double)right ? 1 : 0;
            if (context.op.Text == "<=")
                return (double)left <= (double)right ? 1 : 0;
            if (context.op.Text == ">=")
                return (double)left >= (double)right ? 1 : 0;

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

            switch (context.op.Text) {
                case "*":
                    result = left * right;
                    break;
                case "/":
                    result = left / right;
                    break;
                case "%":
                    result = left % right;
                    break;
            }

            return result;
        }

        /// <summary>
        /// Handle addition and subtraction operators
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override object VisitAddSubExp(MetaExpressionsParser.AddSubExpContext context) {
            object left = (object)Visit(context.expression(0));
            object right = (object)Visit(context.expression(1));

            if (context.op.Text == "+") {
                if (left.GetType() == typeof(double))
                    return (double)left + (double)right;
                if (left.GetType() == typeof(string))
                    return (string)left + right.ToString();
            }
            else if (context.op.Text == "-" && left.GetType() == typeof(double) && right.GetType() == typeof(double)) {
                return (double)left - (double)right;
            }
            // this is a dirty hack to fix dashes in strings, which vtank supports
            else if (context.op.Text == "-" && left.GetType() == typeof(string) && right.GetType() == typeof(string)) {
                return context.GetText();
            }

            throw new Exception($"Unable to {(context.op.Text == "+" ? "add" : "subtract")} type {GetFriendlyType(left.GetType())} {(context.op.Text == "+" ? "to" : "from")} type {GetFriendlyType(right.GetType())}");
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
        /// bitwise complement operator
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override object VisitBitwiseComplementOp(MetaExpressionsParser.BitwiseComplementOpContext context) {
            int num = (int)Visit(context.expression());

            return (double)~num;
        }

        /// <summary>
        /// bitshift operators
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override object VisitBitshiftOps(MetaExpressionsParser.BitshiftOpsContext context) {
            int left = Convert.ToInt32(Visit(context.expression(0)));
            int right = Convert.ToInt32(Visit(context.expression(1)));
            int result = 0;

            switch (context.op.Text) {
                case ">>":
                    result = left >> right;
                    break;
                case "<<":
                    result = left << right;
                    break;
            }

            return (double)result;
        }

        /// <summary>
        /// bitshift operators
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override object VisitBitwiseOps(MetaExpressionsParser.BitwiseOpsContext context) {
            int left = Convert.ToInt32(Visit(context.expression(0)));
            int right = Convert.ToInt32(Visit(context.expression(1)));
            int result = 0;

            switch (context.op.Text) {
                case "&":
                    result = left & right;
                    break;
                case "|":
                    result = left | right;
                    break;
                case "^":
                    result = left ^ right;
                    break;
            }

            return (double)result;
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

        /// <summary>
        /// Shortcut for getting variables
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override object VisitGetvarAtomExp([NotNull] MetaExpressionsParser.GetvarAtomExpContext context) {
            var varname = Visit(context.expression());

            if (context.id.Text == "$")
                return UtilityBeltPlugin.Instance.VTank.Getvar(varname.ToString());
            else if (context.id.Text == "@")
                return UtilityBeltPlugin.Instance.VTank.Getpvar(varname.ToString(), State);
            else if (context.id.Text == "&")
                return UtilityBeltPlugin.Instance.VTank.Getgvar(varname.ToString(), State);
            else
                return null;
        }

        /// <summary>
        /// Shortcut for list/dict/string indice chunks. $something{0:4}
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override object VisitGetindexAtomExp([NotNull] MetaExpressionsParser.GetindexAtomExpContext context) {
            var obj = Visit(context.expression(0));

            if (obj is ExpressionList exprList) {
                bool isRange = false;
                var lstart = IndiceFromExpression(context.i1, exprList.Items.Count, 0);
                var lend = 0;

                if (context.c != null) {
                    lend = IndiceFromExpression(context.i2, exprList.Items.Count, exprList.Items.Count);
                    isRange = true;
                }

                if (isRange) {
                    var newList = new ExpressionList();
                    newList.AddRange(exprList.Items.Skip(lstart).Take(lend - lstart));
                    return newList;
                }
                else {
                    return exprList.Items[lstart];
                }
            }
            else if (obj is string exprString) {
                bool isRange = false;
                var lstart = IndiceFromExpression(context.i1, exprString.Length, 0);
                var lend = 0;

                if (context.c != null) {
                    lend = IndiceFromExpression(context.i2, exprString.Length, exprString.Length);
                    isRange = true;
                }

                if (isRange) {
                    return exprString.Substring(lstart, lend - lstart < 0 ? 0 : lend - lstart);
                }
                else {
                    return exprString.Substring(lstart, 1);
                }
            }
            else if (obj is ExpressionDictionary exprDict) {
                if (context.c != null)
                    throw new Exception($"Range indices not supported with dictionaries");
                var key = Visit(context.i1);

                if (!(key is string keyString))
                    throw new Exception($"Dictionary key must be a string, tried to use: {key.GetType()}");

                exprDict.Items.TryGetValue(keyString, out object val);

                return val;
            }

            throw new Exception($"{obj.GetType()} doest not support indice access");
        }

        private int IndiceFromExpression(MetaExpressionsParser.ExpressionContext expressionContext, int listLength, int defaultValue=0) {
            int indice;

            if (expressionContext != null && !string.IsNullOrEmpty(expressionContext.GetText())) {
                var exprResult = Visit(expressionContext);
                if (!(exprResult is double dindice))
                    throw new Exception($"Indice was type {exprResult.GetType()} ({exprResult}). Indices need to be numbers.");
                indice = (int)dindice;
            }
            else {
                indice = defaultValue;
            }

            if (indice < 0) {
                indice = listLength + indice;
                if (indice < 0)
                    throw new Exception($"Indice was {indice}. Cannot be less than zero.");
            }

            if (indice > listLength)
                throw new Exception($"Indice was {indice}. Cannot be greater than length ({listLength}).");

            return indice;
        }
        #endregion

        /// <summary>
        /// Shortcut for list/dict/string indice chunks. $something{0:4}
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override object VisitCatchallAtomExp([NotNull] MetaExpressionsParser.CatchallAtomExpContext context) {
            throw new Exception($"Unexpected character: {context.GetText()} @ {context.Start.StartIndex}");
        }

        public override object VisitErrorNode([NotNull] IErrorNode node) {
            Logger.Error($"Some error or something: ({node.Payload}) {node.GetText()}");
            return base.VisitErrorNode(node);
        }
    }
}
