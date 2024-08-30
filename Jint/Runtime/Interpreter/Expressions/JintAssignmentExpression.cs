using System.Numerics;
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime.Environments;

using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintAssignmentExpression : JintExpression
{
    private readonly JintExpression _left;
    private readonly JintIdentifierExpression? _leftIdentifier;

    private readonly JintExpression _right;
    private readonly Operator _operator;

    private JintAssignmentExpression(AssignmentExpression expression) : base(expression)
    {
        _left = Build((Expression) expression.Left);
        _leftIdentifier = _left as JintIdentifierExpression;

        _right = Build(expression.Right);
        _operator = expression.Operator;
    }

    internal static JintExpression Build(AssignmentExpression expression)
    {
        if (expression.Operator == Operator.Assignment)
        {
            if (expression.Left is DestructuringPattern)
            {
                return new DestructuringPatternAssignmentExpression(expression);
            }

            return new SimpleAssignmentExpression(expression);
        }

        return new JintAssignmentExpression(expression);
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        var engine = context.Engine;
        var strict = StrictModeScope.IsStrictModeCode;

        JsValue originalLeftValue;
        Reference lref;
        if (_leftIdentifier is not null && JintEnvironment.TryGetIdentifierEnvironmentWithBindingValue(
                engine.ExecutionContext.LexicalEnvironment,
                _leftIdentifier.Identifier,
                strict,
                out var identifierEnvironment,
                out var temp))
        {
            originalLeftValue = temp;
            lref = engine._referencePool.Rent(identifierEnvironment, _leftIdentifier.Identifier.Value, strict, thisValue: null);
        }
        else
        {
            // fast lookup with binding name failed, we need to go through the reference
            lref = (_left.Evaluate(context) as Reference)!;
            if (lref is null)
            {
                ExceptionHelper.ThrowReferenceError(context.Engine.Realm, "not a valid reference");
            }
            originalLeftValue = context.Engine.GetValue(lref, returnReferenceToPool: false);
        }

        var handledByOverload = false;
        JsValue? newLeftValue = null;

        if (context.OperatorOverloadingAllowed)
        {
            newLeftValue = EvaluateOperatorOverloading(context, originalLeftValue, newLeftValue, ref handledByOverload);
        }

        var wasMutatedInPlace = false;
        if (!handledByOverload)
        {
            switch (_operator)
            {
                case Operator.AdditionAssignment:
                    {
                        var rval = _right.GetValue(context);
                        if (AreIntegerOperands(originalLeftValue, rval))
                        {
                            newLeftValue = (long) originalLeftValue.AsInteger() + rval.AsInteger();
                        }
                        else
                        {
                            var lprim = TypeConverter.ToPrimitive(originalLeftValue);
                            var rprim = TypeConverter.ToPrimitive(rval);

                            if (lprim.IsString() || rprim.IsString())
                            {
                                wasMutatedInPlace = lprim is JsString.ConcatenatedString;
                                if (lprim is not JsString jsString)
                                {
                                    jsString = new JsString.ConcatenatedString(TypeConverter.ToString(lprim));
                                }

                                newLeftValue = jsString.Append(rprim);
                            }
                            else if (JintBinaryExpression.AreNonBigIntOperands(originalLeftValue, rval))
                            {
                                newLeftValue = TypeConverter.ToNumber(lprim) + TypeConverter.ToNumber(rprim);
                            }
                            else
                            {
                                JintBinaryExpression.AssertValidBigIntArithmeticOperands(lprim, rprim);
                                newLeftValue = JsBigInt.Create(TypeConverter.ToBigInt(lprim) + TypeConverter.ToBigInt(rprim));
                            }
                        }

                        break;
                    }

                case Operator.SubtractionAssignment:
                    {
                        var rval = _right.GetValue(context);
                        if (AreIntegerOperands(originalLeftValue, rval))
                        {
                            newLeftValue = JsNumber.Create(originalLeftValue.AsInteger() - rval.AsInteger());
                        }
                        else if (JintBinaryExpression.AreNonBigIntOperands(originalLeftValue, rval))
                        {
                            newLeftValue = JsNumber.Create(TypeConverter.ToNumber(originalLeftValue) - TypeConverter.ToNumber(rval));
                        }
                        else
                        {
                            JintBinaryExpression.AssertValidBigIntArithmeticOperands(originalLeftValue, rval);
                            newLeftValue = JsBigInt.Create(TypeConverter.ToBigInt(originalLeftValue) - TypeConverter.ToBigInt(rval));
                        }

                        break;
                    }

                case Operator.MultiplicationAssignment:
                    {
                        var rval = _right.GetValue(context);
                        if (AreIntegerOperands(originalLeftValue, rval))
                        {
                            newLeftValue = (long) originalLeftValue.AsInteger() * rval.AsInteger();
                        }
                        else if (originalLeftValue.IsUndefined() || rval.IsUndefined())
                        {
                            newLeftValue = JsValue.Undefined;
                        }
                        else if (JintBinaryExpression.AreNonBigIntOperands(originalLeftValue, rval))
                        {
                            newLeftValue = TypeConverter.ToNumber(originalLeftValue) * TypeConverter.ToNumber(rval);
                        }
                        else
                        {
                            JintBinaryExpression.AssertValidBigIntArithmeticOperands(originalLeftValue, rval);
                            newLeftValue = JsBigInt.Create(TypeConverter.ToBigInt(originalLeftValue) * TypeConverter.ToBigInt(rval));
                        }

                        break;
                    }

                case Operator.DivisionAssignment:
                    {
                        var rval = _right.GetValue(context);
                        newLeftValue = Divide(context, originalLeftValue, rval);
                        break;
                    }

                case Operator.RemainderAssignment:
                    {
                        var rval = _right.GetValue(context);
                        if (originalLeftValue.IsUndefined() || rval.IsUndefined())
                        {
                            newLeftValue = JsValue.Undefined;
                        }
                        else
                        {
                            newLeftValue = TypeConverter.ToNumber(originalLeftValue) % TypeConverter.ToNumber(rval);
                        }

                        break;
                    }

                case Operator.BitwiseAndAssignment:
                    {
                        var rval = _right.GetValue(context);
                        newLeftValue = TypeConverter.ToInt32(originalLeftValue) & TypeConverter.ToInt32(rval);
                        break;
                    }

                case Operator.BitwiseOrAssignment:
                    {
                        var rval = _right.GetValue(context);
                        newLeftValue = TypeConverter.ToInt32(originalLeftValue) | TypeConverter.ToInt32(rval);
                        break;
                    }

                case Operator.BitwiseXorAssignment:
                    {
                        var rval = _right.GetValue(context);
                        newLeftValue = TypeConverter.ToInt32(originalLeftValue) ^ TypeConverter.ToInt32(rval);
                        break;
                    }

                case Operator.LeftShiftAssignment:
                    {
                        var rval = _right.GetValue(context);
                        newLeftValue = TypeConverter.ToInt32(originalLeftValue) << (int) (TypeConverter.ToUint32(rval) & 0x1F);
                        break;
                    }

                case Operator.RightShiftAssignment:
                    {
                        var rval = _right.GetValue(context);
                        newLeftValue = TypeConverter.ToInt32(originalLeftValue) >> (int) (TypeConverter.ToUint32(rval) & 0x1F);
                        break;
                    }

                case Operator.UnsignedRightShiftAssignment:
                    {
                        var rval = _right.GetValue(context);
                        newLeftValue = (uint) TypeConverter.ToInt32(originalLeftValue) >> (int) (TypeConverter.ToUint32(rval) & 0x1F);
                        break;
                    }

                case Operator.NullishCoalescingAssignment:
                    {
                        if (!originalLeftValue.IsNullOrUndefined())
                        {
                            return originalLeftValue;
                        }

                        var rval = NamedEvaluation(context, _right);
                        newLeftValue = rval;
                        break;
                    }

                case Operator.LogicalAndAssignment:
                    {
                        if (!TypeConverter.ToBoolean(originalLeftValue))
                        {
                            return originalLeftValue;
                        }

                        var rval = NamedEvaluation(context, _right);
                        newLeftValue = rval;
                        break;
                    }

                case Operator.LogicalOrAssignment:
                    {
                        if (TypeConverter.ToBoolean(originalLeftValue))
                        {
                            return originalLeftValue;
                        }

                        var rval = NamedEvaluation(context, _right);
                        newLeftValue = rval;
                        break;
                    }

                case Operator.ExponentiationAssignment:
                    {
                        var rval = _right.GetValue(context);
                        if (!originalLeftValue.IsBigInt() && !rval.IsBigInt())
                        {
                            newLeftValue = JsNumber.Create(Math.Pow(TypeConverter.ToNumber(originalLeftValue), TypeConverter.ToNumber(rval)));
                        }
                        else
                        {
                            var exponent = TypeConverter.ToBigInt(rval);
                            if (exponent > int.MaxValue || exponent < int.MinValue)
                            {
                                ExceptionHelper.ThrowTypeError(context.Engine.Realm, "Cannot do exponentiation with exponent not fitting int32");
                            }
                            newLeftValue = JsBigInt.Create(BigInteger.Pow(TypeConverter.ToBigInt(originalLeftValue), (int) exponent));
                        }

                        break;
                    }

                default:
                    ExceptionHelper.ThrowNotImplementedException();
                    return default;
            }
        }

        // if we did string concatenation in-place, we don't need to update records, objects might have evil setters
        if (!wasMutatedInPlace || lref.Base is not Environment)
        {
            engine.PutValue(lref, newLeftValue!);
        }

        engine._referencePool.Return(lref);
        return newLeftValue!;
    }

    private JsValue? EvaluateOperatorOverloading(EvaluationContext context, JsValue originalLeftValue, JsValue? newLeftValue, ref bool handledByOverload)
    {
        string? operatorClrName = null;
        switch (_operator)
        {
            case Operator.AdditionAssignment:
                operatorClrName = "op_Addition";
                break;
            case Operator.SubtractionAssignment:
                operatorClrName = "op_Subtraction";
                break;
            case Operator.MultiplicationAssignment:
                operatorClrName = "op_Multiply";
                break;
            case Operator.DivisionAssignment:
                operatorClrName = "op_Division";
                break;
            case Operator.RemainderAssignment:
                operatorClrName = "op_Modulus";
                break;
            case Operator.BitwiseAndAssignment:
                operatorClrName = "op_BitwiseAnd";
                break;
            case Operator.BitwiseOrAssignment:
                operatorClrName = "op_BitwiseOr";
                break;
            case Operator.BitwiseXorAssignment:
                operatorClrName = "op_ExclusiveOr";
                break;
            case Operator.LeftShiftAssignment:
                operatorClrName = "op_LeftShift";
                break;
            case Operator.RightShiftAssignment:
                operatorClrName = "op_RightShift";
                break;
            case Operator.UnsignedRightShiftAssignment:
                operatorClrName = "op_UnsignedRightShift";
                break;
            case Operator.ExponentiationAssignment:
            case Operator.Assignment:
            default:
                break;
        }

        if (operatorClrName != null)
        {
            var rval = _right.GetValue(context);
            if (JintBinaryExpression.TryOperatorOverloading(context, originalLeftValue, rval, operatorClrName, out var result))
            {
                newLeftValue = JsValue.FromObject(context.Engine, result);
                handledByOverload = true;
            }
        }

        return newLeftValue;
    }

    private JsValue NamedEvaluation(EvaluationContext context, JintExpression expression)
    {
        var rval = expression.GetValue(context);
        if (expression._expression.IsAnonymousFunctionDefinition() && _left._expression.Type == NodeType.Identifier)
        {
            ((Function) rval).SetFunctionName(((Identifier) _left._expression).Name);
        }

        return rval;
    }

    internal sealed class SimpleAssignmentExpression : JintExpression
    {
        private JintExpression _left = null!;
        private JintExpression _right = null!;

        private JintIdentifierExpression? _leftIdentifier;
        private bool _evalOrArguments;
        private bool _initialized;

        public SimpleAssignmentExpression(AssignmentExpression expression) : base(expression)
        {
        }

        private void Initialize()
        {
            var assignmentExpression = (AssignmentExpression) _expression;
            _left = Build((Expression) assignmentExpression.Left);
            _leftIdentifier = _left as JintIdentifierExpression;
            _evalOrArguments = _leftIdentifier?.HasEvalOrArguments == true;

            _right = Build(assignmentExpression.Right);
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            if (!_initialized)
            {
                Initialize();
                _initialized = true;
            }

            object? completion = null;
            if (_leftIdentifier != null)
            {
                completion = AssignToIdentifier(context, _leftIdentifier, _right, _evalOrArguments);
            }
            return completion ?? SetValue(context);
        }

        // https://262.ecma-international.org/5.1/#sec-11.13.1
        private JsValue SetValue(EvaluationContext context)
        {
            // slower version
            var engine = context.Engine;
            var lref = _left.Evaluate(context) as Reference;
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

        internal static object? AssignToIdentifier(
            EvaluationContext context,
            JintIdentifierExpression left,
            JintExpression right,
            bool hasEvalOrArguments)
        {
            var engine = context.Engine;
            var env = engine.ExecutionContext.LexicalEnvironment;
            var strict = StrictModeScope.IsStrictModeCode;
            var identifier = left.Identifier;
            if (JintEnvironment.TryGetIdentifierEnvironmentWithBinding(
                    env,
                    identifier,
                    out var environmentRecord))
            {
                if (strict && hasEvalOrArguments && identifier.Key != KnownKeys.Eval)
                {
                    ExceptionHelper.ThrowSyntaxError(engine.Realm, "Invalid assignment target");
                }

                var completion = right.GetValue(context);
                if (context.IsAbrupt())
                {
                    return completion;
                }

                var rval = completion.Clone();

                if (right._expression.IsFunctionDefinition())
                {
                    ((Function) rval).SetFunctionName(identifier.Value);
                }

                environmentRecord.SetMutableBinding(identifier, rval, strict);
                return rval;
            }

            return null;
        }
    }
}
