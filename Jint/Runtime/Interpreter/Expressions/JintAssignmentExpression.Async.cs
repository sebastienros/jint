using Esprima.Ast;
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime.Environments;
using Jint.Runtime.References;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed partial class JintAssignmentExpression : JintExpression
    {
        protected async override Task<object> EvaluateInternalAsync()
        {
            var lref = await _left.EvaluateAsync() as Reference ?? ExceptionHelper.ThrowReferenceError<Reference>(_engine);

            var rval = await _right.GetValueAsync();
            var lval = _engine.GetValue(lref, false);

            switch (_operator)
            {
                case AssignmentOperator.PlusAssign:
                    if (AreIntegerOperands(lval, rval))
                    {
                        lval = (long)lval.AsInteger() + rval.AsInteger();
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
                        lval = (long)lval.AsInteger() * rval.AsInteger();
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
                    lval = TypeConverter.ToInt32(lval) << (int)(TypeConverter.ToUint32(rval) & 0x1F);
                    break;

                case AssignmentOperator.RightShiftAssign:
                    lval = TypeConverter.ToInt32(lval) >> (int)(TypeConverter.ToUint32(rval) & 0x1F);
                    break;

                case AssignmentOperator.UnsignedRightShiftAssign:
                    lval = (uint)TypeConverter.ToInt32(lval) >> (int)(TypeConverter.ToUint32(rval) & 0x1F);
                    break;

                default:
                    return ExceptionHelper.ThrowNotImplementedException<object>();
            }

            _engine.PutValue(lref, lval);

            _engine._referencePool.Return(lref);
            return lval;
        }

        internal sealed partial class SimpleAssignmentExpression : JintExpression
        {
            protected async override Task<object> EvaluateInternalAsync()
            {
                JsValue rval = null;
                if (_leftIdentifier != null)
                {
                    rval = await AssignToIdentifierAsync(_engine, _leftIdentifier, _right, _evalOrArguments);
                }
                return rval ?? (await SetValueAsync());
            }

            private async Task<JsValue> SetValueAsync()
            {
                // slower version
                var lref = await _left.EvaluateAsync() as Reference ?? ExceptionHelper.ThrowReferenceError<Reference>(_engine);
                lref.AssertValid(_engine);

                var rval = await _right.GetValueAsync();

                _engine.PutValue(lref, rval);
                _engine._referencePool.Return(lref);
                return rval;
            }

            internal async static Task<JsValue> AssignToIdentifierAsync(Engine engine, JintIdentifierExpression left, JintExpression right, bool hasEvalOrArguments)
            {
                var env = engine.ExecutionContext.LexicalEnvironment;
                var strict = StrictModeScope.IsStrictModeCode;
                if (LexicalEnvironment.TryGetIdentifierEnvironmentWithBindingValue(
                    env,
                    left._expressionName,
                    strict,
                    out var environmentRecord,
                    out _))
                {
                    if (strict && hasEvalOrArguments)
                    {
                        ExceptionHelper.ThrowSyntaxError(engine);
                    }

                    var rval = (await right.GetValueAsync()).Clone();

                    if (right._expression.IsFunctionWithName())
                    {
                        ((FunctionInstance)rval).SetFunctionName(left._expressionName.StringValue);
                    }

                    environmentRecord.SetMutableBinding(left._expressionName, rval, strict);
                    return rval;
                }

                return null;
            }
        }
    }
}