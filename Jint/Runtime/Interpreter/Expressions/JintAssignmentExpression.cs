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

        protected override ExpressionResult EvaluateInternal(EvaluationContext context)
        {
            var lref = _left.Evaluate(context).Value as Reference;
            if (lref is null)
            {
                ExceptionHelper.ThrowReferenceError(context.Engine.Realm, "not a valid reference");
            }

            var engine = context.Engine;
            var lval = context.Engine.GetValue(lref, false);
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
                    var rval = _right.GetValue(context).Value;
                    if (JintBinaryExpression.TryOperatorOverloading(context, lval, rval, operatorClrName, out var result))
                    {
                        lval = JsValue.FromObject(context.Engine, result);
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
                        var rval = _right.GetValue(context).Value;
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
                        var rval = _right.GetValue(context).Value;
                        lval = AreIntegerOperands(lval, rval)
                            ? JsNumber.Create(lval.AsInteger() - rval.AsInteger())
                            : JsNumber.Create(TypeConverter.ToNumber(lval) - TypeConverter.ToNumber(rval));
                        break;
                    }

                    case AssignmentOperator.TimesAssign:
                    {
                        var rval = _right.GetValue(context).Value;
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
                        var rval = _right.GetValue(context).Value;
                        lval = Divide(lval, rval);
                        break;
                    }

                    case AssignmentOperator.ModuloAssign:
                    {
                        var rval = _right.GetValue(context).Value;
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
                        var rval = _right.GetValue(context).Value;
                        lval = TypeConverter.ToInt32(lval) & TypeConverter.ToInt32(rval);
                        break;
                    }

                    case AssignmentOperator.BitwiseOrAssign:
                    {
                        var rval = _right.GetValue(context).Value;
                        lval = TypeConverter.ToInt32(lval) | TypeConverter.ToInt32(rval);
                        break;
                    }

                    case AssignmentOperator.BitwiseXOrAssign:
                    {
                        var rval = _right.GetValue(context).Value;
                        lval = TypeConverter.ToInt32(lval) ^ TypeConverter.ToInt32(rval);
                        break;
                    }

                    case AssignmentOperator.LeftShiftAssign:
                    {
                        var rval = _right.GetValue(context).Value;
                        lval = TypeConverter.ToInt32(lval) << (int) (TypeConverter.ToUint32(rval) & 0x1F);
                        break;
                    }

                    case AssignmentOperator.RightShiftAssign:
                    {
                        var rval = _right.GetValue(context).Value;
                        lval = TypeConverter.ToInt32(lval) >> (int) (TypeConverter.ToUint32(rval) & 0x1F);
                        break;
                    }

                    case AssignmentOperator.UnsignedRightShiftAssign:
                    {
                        var rval = _right.GetValue(context).Value;
                        lval = (uint) TypeConverter.ToInt32(lval) >> (int) (TypeConverter.ToUint32(rval) & 0x1F);
                        break;
                    }

                    case AssignmentOperator.NullishAssign:
                    {
                        if (!lval.IsNullOrUndefined())
                        {
                            return NormalCompletion(lval);
                        }

                        var rval = NamedEvaluation(context, _right);
                        lval = rval;
                        break;
                    }

                    case AssignmentOperator.AndAssign:
                    {
                        if (!TypeConverter.ToBoolean(lval))
                        {
                            return NormalCompletion(lval);
                        }

                        var rval = NamedEvaluation(context, _right);
                        lval = rval;
                        break;
                    }

                    case AssignmentOperator.OrAssign:
                    {
                        if (TypeConverter.ToBoolean(lval))
                        {
                            return NormalCompletion(lval);
                        }

                        var rval = NamedEvaluation(context, _right);
                        lval = rval;
                        break;
                    }

                    default:
                        ExceptionHelper.ThrowNotImplementedException();
                        return default;
                }
            }

            engine.PutValue(lref, lval);

            engine._referencePool.Return(lref);
            return NormalCompletion(lval);
        }

        private JsValue NamedEvaluation(EvaluationContext context, JintExpression expression)
        {
            var rval = expression.GetValue(context).Value;
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

            protected override ExpressionResult EvaluateInternal(EvaluationContext context)
            {
                ExpressionResult? completion = null;
                if (_leftIdentifier != null)
                {
                    completion = AssignToIdentifier(context, _leftIdentifier, _right, _evalOrArguments);
                }
                return completion ?? SetValue(context);
            }

            private ExpressionResult SetValue(EvaluationContext context)
            {
                // slower version
                var engine = context.Engine;
                var lref = _left.Evaluate(context).Value as Reference;
                if (lref is null)
                {
                    ExceptionHelper.ThrowReferenceError(engine.Realm, "not a valid reference");
                }

                lref.AssertValid(engine.Realm);

                var rval = _right.GetValue(context).GetValueOrDefault();

                engine.PutValue(lref, rval);
                engine._referencePool.Return(lref);
                return NormalCompletion(rval);
            }

            internal static ExpressionResult? AssignToIdentifier(
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

                    var completion = right.GetValue(context);
                    if (completion.IsAbrupt())
                    {
                        return completion;
                    }

                    var rval = completion.Value.Clone();

                    if (right._expression.IsFunctionDefinition())
                    {
                        ((FunctionInstance) rval).SetFunctionName(left._expressionName.StringValue);
                    }

                    environmentRecord.SetMutableBinding(left._expressionName, rval, strict);
                    return new Completion(CompletionType.Normal, rval, default);
                }

                return null;
            }
        }
    }
}