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

        private JintAssignmentExpression(Engine engine, AssignmentExpression expression) : base(engine, expression)
        {
            _left = Build(engine, (Expression) expression.Left);
            _right = Build(engine, expression.Right);
            _operator = expression.Operator;
        }

        internal static JintExpression Build(Engine engine, AssignmentExpression expression)
        {
            if (expression.Operator == AssignmentOperator.Assign)
            {
                if (expression.Left is Expression)
                {
                    return new SimpleAssignmentExpression(engine, expression);
                }

                if (expression.Left is BindingPattern)
                {
                    return new BindingPatternAssignmentExpression(engine, expression);
                }
            }

            return new JintAssignmentExpression(engine, expression);
        }

        protected override object EvaluateInternal()
        {
            var lref = _left.Evaluate() as Reference ?? ExceptionHelper.ThrowReferenceError<Reference>(_engine);

            var rval = _right.GetValue();
            var lval = _engine.GetValue(lref, false);

            switch (_operator)
            {
                case AssignmentOperator.PlusAssign:
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

                case AssignmentOperator.MinusAssign:
                    lval = AreIntegerOperands(lval, rval)
                        ? JsNumber.Create(lval.AsInteger() - rval.AsInteger())
                        : JsNumber.Create(TypeConverter.ToNumber(lval) - TypeConverter.ToNumber(rval));
                    break;

                case AssignmentOperator.TimesAssign:
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

                case AssignmentOperator.DivideAssign:
                    lval = Divide(lval, rval);
                    break;

                case AssignmentOperator.ModuloAssign:
                    if (lval.IsUndefined() || rval.IsUndefined())
                    {
                        lval = Undefined.Instance;
                    }
                    else
                    {
                        lval = TypeConverter.ToNumber(lval) % TypeConverter.ToNumber(rval);
                    }

                    break;

                case AssignmentOperator.BitwiseAndAssign:
                    lval = TypeConverter.ToInt32(lval) & TypeConverter.ToInt32(rval);
                    break;

                case AssignmentOperator.BitwiseOrAssign:
                    lval = TypeConverter.ToInt32(lval) | TypeConverter.ToInt32(rval);
                    break;

                case AssignmentOperator.BitwiseXOrAssign:
                    lval = TypeConverter.ToInt32(lval) ^ TypeConverter.ToInt32(rval);
                    break;

                case AssignmentOperator.LeftShiftAssign:
                    lval = TypeConverter.ToInt32(lval) << (int) (TypeConverter.ToUint32(rval) & 0x1F);
                    break;

                case AssignmentOperator.RightShiftAssign:
                    lval = TypeConverter.ToInt32(lval) >> (int) (TypeConverter.ToUint32(rval) & 0x1F);
                    break;

                case AssignmentOperator.UnsignedRightShiftAssign:
                    lval = (uint) TypeConverter.ToInt32(lval) >> (int) (TypeConverter.ToUint32(rval) & 0x1F);
                    break;

                default:
                    ExceptionHelper.ThrowNotImplementedException();
                    return null;
            }

            _engine.PutValue(lref, lval);

            _engine._referencePool.Return(lref);
            return lval;
        }

        internal sealed class SimpleAssignmentExpression : JintExpression
        {
            private readonly JintExpression _left;
            private readonly JintExpression _right;

            private readonly JintIdentifierExpression _leftIdentifier;
            private readonly bool _evalOrArguments;
            private readonly ArrayPattern _arrayPattern;

            public SimpleAssignmentExpression(Engine engine, AssignmentExpression expression) : base(engine, expression)
            {
                if (expression.Left is ArrayPattern arrayPattern)
                {
                    _arrayPattern = arrayPattern;
                }
                else
                {
                    _left = Build(engine, (Expression) expression.Left);
                    _leftIdentifier = _left as JintIdentifierExpression;
                    _evalOrArguments = _leftIdentifier?.ExpressionName == KnownKeys.Eval
                                       || _leftIdentifier?.ExpressionName == KnownKeys.Arguments;
                }

                _right = Build(engine, expression.Right);
            }

            protected override object EvaluateInternal()
            {
                JsValue rval = null;
                if (_leftIdentifier != null)
                {
                    rval = AssignToIdentifier(_engine, _leftIdentifier, _right, _evalOrArguments);
                }
                else if (_arrayPattern != null)
                {
                    foreach (var element in _arrayPattern.Elements)
                    {
                        AssignToIdentifier(_engine, _leftIdentifier, _right, false);
                    }
                }

                return rval ?? SetValue();
            }

            private JsValue SetValue()
            {
                // slower version
                var lref = _left.Evaluate() as Reference ?? ExceptionHelper.ThrowReferenceError<Reference>(_engine);
                lref.AssertValid(_engine);

                var rval = _right.GetValue();

                if (_right._expression.IsFunctionWithName())
                {
                    ((FunctionInstance) rval).SetFunctionName(lref.GetReferencedName());
                }

                _engine.PutValue(lref, rval);
                _engine._referencePool.Return(lref);
                return rval;
            }

            internal static JsValue AssignToIdentifier(
                Engine engine,
                JintIdentifierExpression left,
                JintExpression right,
                bool hasEvalOrArguments)
            {
                var env = engine.ExecutionContext.LexicalEnvironment;
                var strict = StrictModeScope.IsStrictModeCode;
                if (LexicalEnvironment.TryGetIdentifierEnvironmentWithBindingValue(
                    env,
                    left.ExpressionName,
                    strict,
                    out var environmentRecord,
                    out _))
                {
                    if (strict && hasEvalOrArguments)
                    {
                        ExceptionHelper.ThrowSyntaxError(engine);
                    }

                    var rval = right.GetValue();

                    if (right._expression.IsFunctionWithName())
                    {
                        ((FunctionInstance) rval).SetFunctionName(left.ExpressionName);
                    }

                    environmentRecord.SetMutableBinding(left.ExpressionName, rval, strict);
                    return rval;
                }

                return null;
            }
        }
    }
}