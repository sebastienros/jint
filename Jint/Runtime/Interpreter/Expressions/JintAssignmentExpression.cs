using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Environments;
using Jint.Runtime.References;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintAssignmentExpression : JintExpression
    {
        private readonly JintExpression _left;
        private readonly JintExpression _right;

        private JintAssignmentExpression(Engine engine, AssignmentExpression expression) : base(engine, expression)
        {
            _left = Build(engine, (Expression) expression.Left);
            _right = Build(engine, expression.Right);
        }

        internal static JintExpression Build(Engine engine, AssignmentExpression expression)
        {
            if (expression.Operator == AssignmentOperator.Assign)
            {
                return new Assignment(engine, expression);
            }

            return new JintAssignmentExpression(engine, expression);
        }

        protected override object EvaluateInternal()
        {
            var lref = _left.Evaluate() as Reference ?? ExceptionHelper.ThrowReferenceError<Reference>(_engine);
            JsValue rval = _right.GetValue();

            JsValue lval = _engine.GetValue(lref, false);

            var expression = (AssignmentExpression) _expression;
            switch (expression.Operator)
            {
                case AssignmentOperator.PlusAssign:
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

                    break;

                case AssignmentOperator.MinusAssign:
                    lval = TypeConverter.ToNumber(lval) - TypeConverter.ToNumber(rval);
                    break;

                case AssignmentOperator.TimesAssign:
                    if (lval.IsUndefined() || rval.IsUndefined())
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

        private class Assignment : JintExpression
        {
            private readonly JintExpression _left;
            private readonly JintExpression _right;

            private readonly JintIdentifierExpression _leftIdentifier;
            private bool _evalOrArguments;

            public Assignment(Engine engine, AssignmentExpression expression) : base(engine, expression)
            {
                _left = Build(engine, (Expression) expression.Left);
                _right = Build(engine, expression.Right);

                _leftIdentifier = _left as JintIdentifierExpression;
                _evalOrArguments = _leftIdentifier?._expressionName == "eval" || _leftIdentifier?._expressionName == "arguments";
            }

            protected override object EvaluateInternal()
            {
                JsValue rval = _right.GetValue();

                if (_leftIdentifier == null || !SetReferenceValueFast(rval))
                {
                    // slower version
                    var lref = _left.Evaluate() as Reference ?? ExceptionHelper.ThrowReferenceError<Reference>(_engine);
                    lref.AssertValid(_engine);

                    _engine.PutValue(lref, rval);
                    _engine._referencePool.Return(lref);

                }
                return rval;
            }

            private bool SetReferenceValueFast(JsValue right)
            {
                var env = _engine.ExecutionContext.LexicalEnvironment;
                var strict = StrictModeScope.IsStrictModeCode;
                if (LexicalEnvironment.TryGetIdentifierEnvironmentWithBindingValue(
                    env,
                    _leftIdentifier._expressionName,
                    strict,
                    out var environmentRecord,
                    out _))
                {
                    if (strict && _evalOrArguments)
                    {
                        ExceptionHelper.ThrowSyntaxError(_engine);
                    }

                    environmentRecord.SetMutableBinding(_leftIdentifier._expressionName, right, strict);
                    return true;
                }

                return false;
            }
        }
    }
}