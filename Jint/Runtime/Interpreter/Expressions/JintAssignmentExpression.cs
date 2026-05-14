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
        var suspendable = engine.ExecutionContext.Suspendable;

        JsValue originalLeftValue;
        Reference lref;
        bool lhsHasSideEffects;
        if (suspendable is { IsResuming: true }
            && suspendable.Data.TryGet(this, out AssignmentSuspendData? suspendData))
        {
            // Resuming: skip LHS re-evaluation. The slow path may have observable
            // side effects (obj[++i]), so we reuse the saved Reference + original value.
            lref = suspendData!.Lref;
            originalLeftValue = suspendData.OriginalLeftValue;
            lhsHasSideEffects = true;
        }
        else if (_leftIdentifier is not null && JintEnvironment.TryGetIdentifierEnvironmentWithBindingValue(
                engine.ExecutionContext.LexicalEnvironment,
                _leftIdentifier.Identifier,
                strict,
                out var identifierEnvironment,
                out var temp))
        {
            originalLeftValue = temp;
            lref = engine._referencePool.Rent(identifierEnvironment, _leftIdentifier.Identifier.Value, strict, thisValue: null);
            lhsHasSideEffects = false;
        }
        else
        {
            // fast lookup with binding name failed, we need to go through the reference
            lref = (_left.Evaluate(context) as Reference)!;
            if (lref is null)
            {
                Throw.ReferenceError(context.Engine.Realm, "Invalid left-hand side in assignment");
            }
            originalLeftValue = context.Engine.GetValue(lref, returnReferenceToPool: false);
            lhsHasSideEffects = true;
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
                        if (context.IsSuspended())
                        {
                            HandleSuspendedRight(engine, suspendable, lref, originalLeftValue, lhsHasSideEffects);
                            return rval;
                        }

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
                        if (context.IsSuspended())
                        {
                            HandleSuspendedRight(engine, suspendable, lref, originalLeftValue, lhsHasSideEffects);
                            return rval;
                        }

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
                        if (context.IsSuspended())
                        {
                            HandleSuspendedRight(engine, suspendable, lref, originalLeftValue, lhsHasSideEffects);
                            return rval;
                        }

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
                        if (context.IsSuspended())
                        {
                            HandleSuspendedRight(engine, suspendable, lref, originalLeftValue, lhsHasSideEffects);
                            return rval;
                        }

                        newLeftValue = Divide(context, originalLeftValue, rval);
                        break;
                    }

                case Operator.RemainderAssignment:
                    {
                        var rval = _right.GetValue(context);
                        if (context.IsSuspended())
                        {
                            HandleSuspendedRight(engine, suspendable, lref, originalLeftValue, lhsHasSideEffects);
                            return rval;
                        }

                        newLeftValue = Remainder(context, originalLeftValue, rval);
                        break;
                    }

                case Operator.BitwiseAndAssignment:
                    {
                        var rval = _right.GetValue(context);
                        if (context.IsSuspended())
                        {
                            HandleSuspendedRight(engine, suspendable, lref, originalLeftValue, lhsHasSideEffects);
                            return rval;
                        }

                        newLeftValue = TypeConverter.ToInt32(originalLeftValue) & TypeConverter.ToInt32(rval);
                        break;
                    }

                case Operator.BitwiseOrAssignment:
                    {
                        var rval = _right.GetValue(context);
                        if (context.IsSuspended())
                        {
                            HandleSuspendedRight(engine, suspendable, lref, originalLeftValue, lhsHasSideEffects);
                            return rval;
                        }

                        newLeftValue = TypeConverter.ToInt32(originalLeftValue) | TypeConverter.ToInt32(rval);
                        break;
                    }

                case Operator.BitwiseXorAssignment:
                    {
                        var rval = _right.GetValue(context);
                        if (context.IsSuspended())
                        {
                            HandleSuspendedRight(engine, suspendable, lref, originalLeftValue, lhsHasSideEffects);
                            return rval;
                        }

                        newLeftValue = TypeConverter.ToInt32(originalLeftValue) ^ TypeConverter.ToInt32(rval);
                        break;
                    }

                case Operator.LeftShiftAssignment:
                    {
                        var rval = _right.GetValue(context);
                        if (context.IsSuspended())
                        {
                            HandleSuspendedRight(engine, suspendable, lref, originalLeftValue, lhsHasSideEffects);
                            return rval;
                        }

                        newLeftValue = TypeConverter.ToInt32(originalLeftValue) << (int) (TypeConverter.ToUint32(rval) & 0x1F);
                        break;
                    }

                case Operator.RightShiftAssignment:
                    {
                        var rval = _right.GetValue(context);
                        if (context.IsSuspended())
                        {
                            HandleSuspendedRight(engine, suspendable, lref, originalLeftValue, lhsHasSideEffects);
                            return rval;
                        }

                        newLeftValue = TypeConverter.ToInt32(originalLeftValue) >> (int) (TypeConverter.ToUint32(rval) & 0x1F);
                        break;
                    }

                case Operator.UnsignedRightShiftAssignment:
                    {
                        var rval = _right.GetValue(context);
                        if (context.IsSuspended())
                        {
                            HandleSuspendedRight(engine, suspendable, lref, originalLeftValue, lhsHasSideEffects);
                            return rval;
                        }

                        newLeftValue = (uint) TypeConverter.ToInt32(originalLeftValue) >> (int) (TypeConverter.ToUint32(rval) & 0x1F);
                        break;
                    }

                case Operator.NullishCoalescingAssignment:
                    {
                        if (!originalLeftValue.IsNullOrUndefined())
                        {
                            engine._referencePool.Return(lref);
                            suspendable?.Data.Clear(this);
                            return originalLeftValue;
                        }

                        var rval = NamedEvaluation(context, _right);
                        if (context.IsSuspended())
                        {
                            HandleSuspendedRight(engine, suspendable, lref, originalLeftValue, lhsHasSideEffects);
                            return rval;
                        }

                        newLeftValue = rval;
                        break;
                    }

                case Operator.LogicalAndAssignment:
                    {
                        if (!TypeConverter.ToBoolean(originalLeftValue))
                        {
                            engine._referencePool.Return(lref);
                            suspendable?.Data.Clear(this);
                            return originalLeftValue;
                        }

                        var rval = NamedEvaluation(context, _right);
                        if (context.IsSuspended())
                        {
                            HandleSuspendedRight(engine, suspendable, lref, originalLeftValue, lhsHasSideEffects);
                            return rval;
                        }

                        newLeftValue = rval;
                        break;
                    }

                case Operator.LogicalOrAssignment:
                    {
                        if (TypeConverter.ToBoolean(originalLeftValue))
                        {
                            engine._referencePool.Return(lref);
                            suspendable?.Data.Clear(this);
                            return originalLeftValue;
                        }

                        var rval = NamedEvaluation(context, _right);
                        if (context.IsSuspended())
                        {
                            HandleSuspendedRight(engine, suspendable, lref, originalLeftValue, lhsHasSideEffects);
                            return rval;
                        }

                        newLeftValue = rval;
                        break;
                    }

                case Operator.ExponentiationAssignment:
                    {
                        var rval = _right.GetValue(context);
                        if (context.IsSuspended())
                        {
                            HandleSuspendedRight(engine, suspendable, lref, originalLeftValue, lhsHasSideEffects);
                            return rval;
                        }

                        if (!originalLeftValue.IsBigInt() && !rval.IsBigInt())
                        {
                            newLeftValue = JsNumber.Create(Math.Pow(TypeConverter.ToNumber(originalLeftValue), TypeConverter.ToNumber(rval)));
                        }
                        else
                        {
                            var exponent = TypeConverter.ToBigInt(rval);
                            if (exponent < 0)
                            {
                                Throw.RangeError(context.Engine.Realm, "Exponent must be positive");
                            }

                            if (exponent > int.MaxValue)
                            {
                                Throw.RangeError(context.Engine.Realm, "Maximum BigInt size exceeded");
                            }

                            var intExponent = (int) exponent;
                            var baseValue = TypeConverter.ToBigInt(originalLeftValue);
                            JintBinaryExpression.ValidateBigIntPowSize(context.Engine.Realm, baseValue, intExponent);
                            newLeftValue = JsBigInt.Create(BigInteger.Pow(baseValue, intExponent));
                        }

                        break;
                    }

                default:
                    Throw.NotImplementedException();
                    return default;
            }
        }

        // if we did string concatenation in-place, we don't need to update records, objects might have evil setters
        if (!wasMutatedInPlace || lref.Base is not Environment)
        {
            engine.PutValue(lref, newLeftValue!);
        }

        engine._referencePool.Return(lref);
        suspendable?.Data.Clear(this);
        return newLeftValue!;
    }

    private void HandleSuspendedRight(Engine engine, ISuspendable? suspendable, Reference lref, JsValue originalLeftValue, bool lhsHasSideEffects)
    {
        // Only the slow path's LHS can have observable side effects (e.g. obj[++i]).
        // For that case, hold the Reference + value across the suspension so the next
        // resume reuses them. For the side-effect-free fast path, return the Reference
        // to the pool — no benefit in retaining it.
        if (lhsHasSideEffects && suspendable is not null)
        {
            var data = suspendable.Data.GetOrCreate<AssignmentSuspendData>(this);
            data.Lref = lref;
            data.OriginalLeftValue = originalLeftValue;
        }
        else
        {
            engine._referencePool.Return(lref);
        }
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
        // IsIdentifierRef is false for a CoverParenthesizedExpression. Acornima strips parens by default,
        // but the AssignmentExpression's Range starts at the leading '(' while the inner Identifier's Range
        // starts after it — so a positional difference indicates the LHS was parenthesized.
        if (expression._expression.IsAnonymousFunctionDefinition()
            && _left._expression.Type == NodeType.Identifier
            && _left._expression.Range.Start == _expression.Range.Start)
        {
            var name = ((Identifier) _left._expression).Name;
            if (expression is JintClassExpression classExpression)
            {
                return classExpression.EvaluateWithName(context, name);
            }

            var rval = expression.GetValue(context);
            ((Function) rval).SetFunctionName(name);
            return rval;
        }

        return expression.GetValue(context);
    }

    internal sealed class SimpleAssignmentExpression : JintExpression
    {
        private JintExpression _left = null!;
        private JintExpression _right = null!;

        private JintIdentifierExpression? _leftIdentifier;
        private bool _evalOrArguments;
        private bool _leftIsCoverParenthesized;
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

            // IsIdentifierRef is false for a CoverParenthesizedExpression. Acornima strips parens by default,
            // so we detect them by comparing ranges: the AssignmentExpression starts at the leading '(' but
            // the inner Identifier starts after it.
            _leftIsCoverParenthesized = _left._expression.Range.Start != assignmentExpression.Range.Start;

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
                completion = AssignToIdentifier(context, _leftIdentifier, _right, _evalOrArguments, !_leftIsCoverParenthesized);
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
                Throw.ReferenceError(engine.Realm, "Invalid left-hand side in assignment");
            }

            lref.AssertValid(engine.Realm);

            var rval = _right.GetValue(context);

            // If generator suspended or return requested during right-hand side evaluation, don't assign
            if (context.IsGeneratorAborted())
            {
                engine._referencePool.Return(lref);
                return rval;
            }

            // Set LastSyntaxElement for proper error location if PutValue throws
            context.LastSyntaxElement = _left._expression;
            engine.PutValue(lref, rval);
            engine._referencePool.Return(lref);
            return rval;
        }

        internal static object? AssignToIdentifier(
            EvaluationContext context,
            JintIdentifierExpression left,
            JintExpression right,
            bool hasEvalOrArguments,
            bool nameAnonymousFunction = true)
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
                    Throw.SyntaxError(engine.Realm, "Invalid assignment target");
                }

                JsValue completion;
                if (nameAnonymousFunction && right is JintClassExpression classExpression && right._expression.IsAnonymousFunctionDefinition())
                {
                    completion = classExpression.EvaluateWithName(context, identifier.Value.ToString());
                }
                else
                {
                    completion = right.GetValue(context);
                }

                if (context.IsAbrupt())
                {
                    return completion;
                }

                // If generator suspended or return requested during right-hand side evaluation, don't assign
                if (context.IsGeneratorAborted())
                {
                    return completion;
                }

                var rval = completion.Clone();

                if (nameAnonymousFunction && right._expression.IsFunctionDefinition() && right is not JintClassExpression)
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
