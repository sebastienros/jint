using Esprima.Ast;
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime.Environments;
using Jint.Runtime.References;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintAssignmentExpression : JintExpression
    {
        private readonly JintExpression _left;
        private readonly JintExpression _right;
        private readonly AssignmentOperator _operator;

        private JintAssignmentExpression(Engine engine, AssignmentExpression expression) : base(expression)
        {
            _left = Build(engine, expression.Left);
            _right = Build(engine, expression.Right);
            _operator = expression.Operator;
        }

        internal static JintExpression Build(Engine engine, AssignmentExpression expression)
        {
            if (expression.Operator == AssignmentOperator.Assign)
            {
                if (expression.Left is BindingPattern)
                {
                    return new BindingPatternAssignmentExpression(expression);
                }

                return new SimpleAssignmentExpression(expression);
            }

            return new JintAssignmentExpression(engine, expression);
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            var lref = _left.Evaluate(context) as Reference;
            var engine = context.Engine;
            if (lref is null)
            {
                ExceptionHelper.ThrowReferenceError(engine.Realm, "not a valid reference");
            }

            var lval = engine.GetValue(lref, false);
            var handledByOverload = false;

            if (context.OperatorOverloadingAllowed)
            {
                string operatorClrName = null;
                switch (_operator)
                {
                    case AssignmentOperator.PlusAssign:
                        operatorClrName = "op_Addition";
                        break;
                    case AssignmentOperator.MinusAssign:
                        operatorClrName = "op_Subtraction";
                        break;
                    case AssignmentOperator.TimesAssign:
                        operatorClrName = "op_Multiply";
                        break;
                    case AssignmentOperator.DivideAssign:
                        operatorClrName = "op_Division";
                        break;
                    case AssignmentOperator.ModuloAssign:
                        operatorClrName = "op_Modulus";
                        break;
                    case AssignmentOperator.BitwiseAndAssign:
                        operatorClrName = "op_BitwiseAnd";
                        break;
                    case AssignmentOperator.BitwiseOrAssign:
                        operatorClrName = "op_BitwiseOr";
                        break;
                    case AssignmentOperator.BitwiseXOrAssign:
                        operatorClrName = "op_ExclusiveOr";
                        break;
                    case AssignmentOperator.LeftShiftAssign:
                        operatorClrName = "op_LeftShift";
                        break;
                    case AssignmentOperator.RightShiftAssign:
                        operatorClrName = "op_RightShift";
                        break;
                    case AssignmentOperator.UnsignedRightShiftAssign:
                        operatorClrName = "op_UnsignedRightShift";
                        break;
                    case AssignmentOperator.ExponentiationAssign:
                    case AssignmentOperator.Assign:
                    default:
                        break;
                }

                if (operatorClrName != null)
                {
                    var rval = _right.GetValue(context);
                    if (JintBinaryExpression.TryOperatorOverloading(context, lval, rval, operatorClrName, out var result))
                    {
                        lval = JsValue.FromObject(engine, result);
                        handledByOverload = true;
                    }
                }
            }

            if (!handledByOverload)
            {
                switch (_operator)
                {
                    case AssignmentOperator.PlusAssign:
                    {
                        var rval = _right.GetValue(context);
                        if (AreIntegerOperands(lval, rval))
                        {
                            lval = (long) lval.AsInteger() + rval.AsInteger();
                        }
                        else
                        {
                            var lprim = TypeConverter.ToPrimitive(lval);
                            var rprim = TypeConverter.ToPrimitive(rval);

                            if (lprim.IsString() || rprim.IsString())
                            {
                                if (!(lprim is JsString jsString))
                                {
                                    jsString = new JsString.ConcatenatedString(TypeConverter.ToString(lprim));
                                }

                                lval = jsString.Append(rprim);
                            }
                            else
                            {
                                lval = TypeConverter.ToNumber(lprim) + TypeConverter.ToNumber(rprim);
                            }
                        }

                        break;
                    }

                    case AssignmentOperator.MinusAssign:
                    {
                        var rval = _right.GetValue(context);
                        lval = AreIntegerOperands(lval, rval)
                            ? JsNumber.Create(lval.AsInteger() - rval.AsInteger())
                            : JsNumber.Create(TypeConverter.ToNumber(lval) - TypeConverter.ToNumber(rval));
                        break;
                    }

                    case AssignmentOperator.TimesAssign:
                    {
                        var rval = _right.GetValue(context);
                        if (AreIntegerOperands(lval, rval))
                        {
                            lval = (long) lval.AsInteger() * rval.AsInteger();
                        }
                        else if (lval.IsUndefined() || rval.IsUndefined())
                        {
                            lval = Undefined.Instance;
                        }
                        else
                        {
                            lval = TypeConverter.ToNumber(lval) * TypeConverter.ToNumber(rval);
                        }

                        break;
                    }

                    case AssignmentOperator.DivideAssign:
                    {
                        var rval = _right.GetValue(context);
                        lval = Divide(lval, rval);
                        break;
                    }

                    case AssignmentOperator.ModuloAssign:
                    {
                        var rval = _right.GetValue(context);
                        if (lval.IsUndefined() || rval.IsUndefined())
                        {
                            lval = Undefined.Instance;
                        }
                        else
                        {
                            lval = TypeConverter.ToNumber(lval) % TypeConverter.ToNumber(rval);
                        }

                        break;
                    }

                    case AssignmentOperator.BitwiseAndAssign:
                    {
                        var rval = _right.GetValue(context);
                        lval = TypeConverter.ToInt32(lval) & TypeConverter.ToInt32(rval);
                        break;
                    }

                    case AssignmentOperator.BitwiseOrAssign:
                    {
                        var rval = _right.GetValue(context);
                        lval = TypeConverter.ToInt32(lval) | TypeConverter.ToInt32(rval);
                        break;
                    }

                    case AssignmentOperator.BitwiseXOrAssign:
                    {
                        var rval = _right.GetValue(context);
                        lval = TypeConverter.ToInt32(lval) ^ TypeConverter.ToInt32(rval);
                        break;
                    }

                    case AssignmentOperator.LeftShiftAssign:
                    {
                        var rval = _right.GetValue(context);
                        lval = TypeConverter.ToInt32(lval) << (int) (TypeConverter.ToUint32(rval) & 0x1F);
                        break;
                    }

                    case AssignmentOperator.RightShiftAssign:
                    {
                        var rval = _right.GetValue(context);
                        lval = TypeConverter.ToInt32(lval) >> (int) (TypeConverter.ToUint32(rval) & 0x1F);
                        break;
                    }

                    case AssignmentOperator.UnsignedRightShiftAssign:
                    {
                        var rval = _right.GetValue(context);
                        lval = (uint) TypeConverter.ToInt32(lval) >> (int) (TypeConverter.ToUint32(rval) & 0x1F);
                        break;
                    }

                    case AssignmentOperator.NullishAssign:
                    {
                        if (!lval.IsNullOrUndefined())
                        {
                            return lval;
                        }

                        var rval = NamedEvaluation(context, _right);
                        lval = rval;
                        break;
                    }

                    case AssignmentOperator.AndAssign:
                    {
                        if (!TypeConverter.ToBoolean(lval))
                        {
                            return lval;
                        }

                        var rval = NamedEvaluation(context, _right);
                        lval = rval;
                        break;
                    }

                    case AssignmentOperator.OrAssign:
                    {
                        if (TypeConverter.ToBoolean(lval))
                        {
                            return lval;
                        }

                        var rval = NamedEvaluation(context, _right);
                        lval = rval;
                        break;
                    }

                    default:
                        ExceptionHelper.ThrowNotImplementedException();
                        return null;
                }
            }

            engine.PutValue(lref, lval);

            engine._referencePool.Return(lref);
            return lval;
        }

        private JsValue NamedEvaluation(EvaluationContext context, JintExpression expression)
        {
            var rval = expression.GetValue(context);
            if (expression._expression.IsAnonymousFunctionDefinition() && _left._expression.Type == Nodes.Identifier)
            {
                ((FunctionInstance) rval).SetFunctionName(((Identifier) _left._expression).Name);
            }

            return rval;
        }

        internal sealed class SimpleAssignmentExpression : JintExpression
        {
            private JintExpression _left;
            private JintExpression _right;

            private JintIdentifierExpression _leftIdentifier;
            private bool _evalOrArguments;

            public SimpleAssignmentExpression(AssignmentExpression expression) : base(expression)
            {
                _initialized = false;
            }

            protected override void Initialize(EvaluationContext context)
            {
                var assignmentExpression = ((AssignmentExpression) _expression);
                _left = Build(context.Engine, assignmentExpression.Left);
                _leftIdentifier = _left as JintIdentifierExpression;
                _evalOrArguments = _leftIdentifier?.HasEvalOrArguments == true;

                _right = Build(context.Engine, assignmentExpression.Right);
            }

            protected override object EvaluateInternal(EvaluationContext context)
            {
                JsValue rval = null;
                if (_leftIdentifier != null)
                {
                    rval = AssignToIdentifier(context, _leftIdentifier, _right, _evalOrArguments);
                }
                return rval ?? SetValue(context);
            }

            private JsValue SetValue(EvaluationContext context)
            {
                // slower version
                var lref = _left.Evaluate(context) as Reference;
                var engine = context.Engine;
                if (lref is null)
                {
                    ExceptionHelper.ThrowReferenceError(engine.Realm, "not a valid reference");
                }
                lref.AssertValid(engine.Realm);

                var rval = _right.GetValue(context);

                engine.PutValue(lref, rval);
                engine._referencePool.Return(lref);
                return rval;
            }

            internal static JsValue AssignToIdentifier(
                EvaluationContext context,
                JintIdentifierExpression left,
                JintExpression right,
                bool hasEvalOrArguments)
            {
                var engine = context.Engine;
                var env = engine.ExecutionContext.LexicalEnvironment;
                var strict = StrictModeScope.IsStrictModeCode;
                if (JintEnvironment.TryGetIdentifierEnvironmentWithBindingValue(
                    engine,
                    env,
                    left._expressionName,
                    strict,
                    out var environmentRecord,
                    out _))
                {
                    if (strict && hasEvalOrArguments)
                    {
                        ExceptionHelper.ThrowSyntaxError(engine.Realm);
                    }

                    var rval = right.GetValue(context).Clone();

                    if (right._expression.IsFunctionDefinition())
                    {
                        ((FunctionInstance) rval).SetFunctionName(left._expressionName.StringValue);
                    }

                    environmentRecord.SetMutableBinding(left._expressionName, rval, strict);
                    return rval;
                }

                return null;
            }
        }
    }
}