using System.Numerics;
using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Function;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;

using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintAssignmentExpression : JintExpression
{
    private readonly JintExpression _left;
    private readonly JintIdentifierExpression? _leftIdentifier;

    private readonly JintExpression _right;
    private readonly Operator _operator;

    // Slot-location cache for the discard-mode fast path; see SlotLocationCache for the
    // validity reasoning (with-statement shadowing, pooling, cross-engine sharing).
    private SlotLocationCache _slotCache;

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

        // Value-producing twin of the discard-mode slot lane (completion-value positions such
        // as eval/script bodies route here even for expression statements). Same gates: the
        // logical/nullish forms short-circuit, overloading and suspension need the Reference.
        if (suspendable is null
            && _leftIdentifier is not null
            && !context.OperatorOverloadingAllowed
            && _operator is not (Operator.NullishCoalescingAssignment or Operator.LogicalAndAssignment or Operator.LogicalOrAssignment)
            && _slotCache.TryResolve(engine, engine.ExecutionContext.LexicalEnvironment, _leftIdentifier.Identifier, out var slotEnvironment, out var fastSlotIndex)
            && TryCompoundSlotValue(context, slotEnvironment, fastSlotIndex, out var fastResult))
        {
            return fastResult;
        }

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
                out var temp)
            && temp is not null) // an uninitialized (TDZ) binding reports null; the Reference path below produces the proper ReferenceError
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

            // RHS suspended during the overloading attempt; save LHS state and bail
            // so the operator-switch below doesn't double-evaluate _right.
            if (context.IsSuspended())
            {
                HandleSuspendedRight(engine, suspendable, lref, originalLeftValue, lhsHasSideEffects);
                return newLeftValue!;
            }
        }

        var wasMutatedInPlace = false;
        if (!handledByOverload)
        {
            switch (_operator)
            {
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

                default:
                    {
                        var rval = _right.GetValue(context);
                        if (context.IsSuspended())
                        {
                            HandleSuspendedRight(engine, suspendable, lref, originalLeftValue, lhsHasSideEffects);
                            return rval;
                        }

                        newLeftValue = ComputeCompound(context, originalLeftValue, rval, ref wasMutatedInPlace);
                        break;
                    }
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

    /// <summary>
    /// Computes the result of a compound assignment operator (everything except the
    /// logical/nullish forms, which short-circuit before evaluating the right operand).
    /// Operands are fully evaluated; shared by the materialized and discard-mode lanes.
    /// </summary>
    private JsValue ComputeCompound(EvaluationContext context, JsValue originalLeftValue, JsValue rval, ref bool wasMutatedInPlace)
    {
        switch (_operator)
        {
            case Operator.AdditionAssignment:
                {
                    if (AreIntegerOperands(originalLeftValue, rval))
                    {
                        return JsNumber.Create((long) originalLeftValue.AsInteger() + rval.AsInteger());
                    }

                    var lprim = TypeConverter.ToPrimitive(originalLeftValue);
                    var rprim = TypeConverter.ToPrimitive(rval);

                    if (lprim.IsString() || rprim.IsString())
                    {
                        wasMutatedInPlace = lprim is JsString.ConcatenatedString;
                        if (lprim is not JsString jsString)
                        {
                            jsString = new JsString.ConcatenatedString(TypeConverter.ToString(lprim));
                        }

                        return jsString.Append(rprim);
                    }

                    if (JintBinaryExpression.AreNonBigIntOperands(originalLeftValue, rval))
                    {
                        return JsNumber.Create(TypeConverter.ToNumber(lprim) + TypeConverter.ToNumber(rprim));
                    }

                    JintBinaryExpression.AssertValidBigIntArithmeticOperands(lprim, rprim);
                    return JsBigInt.Create(TypeConverter.ToBigInt(lprim) + TypeConverter.ToBigInt(rprim));
                }

            case Operator.SubtractionAssignment:
                {
                    if (AreIntegerOperands(originalLeftValue, rval))
                    {
                        return JsNumber.Create((long) originalLeftValue.AsInteger() - rval.AsInteger());
                    }

                    var leftNumeric = TypeConverter.ToNumeric(originalLeftValue);
                    var rightNumeric = TypeConverter.ToNumeric(rval);

                    if (JintBinaryExpression.AreNonBigIntOperands(leftNumeric, rightNumeric))
                    {
                        return JsNumber.Create(leftNumeric.AsNumber() - rightNumeric.AsNumber());
                    }

                    JintBinaryExpression.AssertValidBigIntArithmeticOperands(leftNumeric, rightNumeric);
                    return JsBigInt.Create(TypeConverter.ToBigInt(leftNumeric) - TypeConverter.ToBigInt(rightNumeric));
                }

            case Operator.MultiplicationAssignment:
                {
                    if (AreIntegerOperands(originalLeftValue, rval))
                    {
                        return JsNumber.Create((long) originalLeftValue.AsInteger() * rval.AsInteger());
                    }

                    var leftNumeric = TypeConverter.ToNumeric(originalLeftValue);
                    var rightNumeric = TypeConverter.ToNumeric(rval);

                    if (JintBinaryExpression.AreNonBigIntOperands(leftNumeric, rightNumeric))
                    {
                        return leftNumeric.AsNumber() * rightNumeric.AsNumber();
                    }

                    JintBinaryExpression.AssertValidBigIntArithmeticOperands(leftNumeric, rightNumeric);
                    return JsBigInt.Create(TypeConverter.ToBigInt(leftNumeric) * TypeConverter.ToBigInt(rightNumeric));
                }

            case Operator.DivisionAssignment:
                return Divide(context, TypeConverter.ToNumeric(originalLeftValue), TypeConverter.ToNumeric(rval));

            case Operator.RemainderAssignment:
                return Remainder(context, originalLeftValue, rval);

            case Operator.BitwiseAndAssignment:
                return JintBinaryExpression.EvaluateBitwiseOperation(Operator.BitwiseAnd, TypeConverter.ToNumeric(originalLeftValue), TypeConverter.ToNumeric(rval), _left._expression);

            case Operator.BitwiseOrAssignment:
                return JintBinaryExpression.EvaluateBitwiseOperation(Operator.BitwiseOr, TypeConverter.ToNumeric(originalLeftValue), TypeConverter.ToNumeric(rval), _left._expression);

            case Operator.BitwiseXorAssignment:
                return JintBinaryExpression.EvaluateBitwiseOperation(Operator.BitwiseXor, TypeConverter.ToNumeric(originalLeftValue), TypeConverter.ToNumeric(rval), _left._expression);

            case Operator.LeftShiftAssignment:
                return JintBinaryExpression.EvaluateBitwiseOperation(Operator.LeftShift, TypeConverter.ToNumeric(originalLeftValue), TypeConverter.ToNumeric(rval), _left._expression);

            case Operator.RightShiftAssignment:
                return JintBinaryExpression.EvaluateBitwiseOperation(Operator.RightShift, TypeConverter.ToNumeric(originalLeftValue), TypeConverter.ToNumeric(rval), _left._expression);

            case Operator.UnsignedRightShiftAssignment:
                return JintBinaryExpression.EvaluateBitwiseOperation(Operator.UnsignedRightShift, TypeConverter.ToNumeric(originalLeftValue), TypeConverter.ToNumeric(rval), _left._expression);

            case Operator.ExponentiationAssignment:
                return JintBinaryExpression.Exponentiate(context, TypeConverter.ToNumeric(originalLeftValue), TypeConverter.ToNumeric(rval));

            default:
                Throw.NotImplementedException();
                return null!;
        }
    }

    internal override bool HasDiscardFastPath => true;

    internal override void EvaluateAndDiscard(EvaluationContext context)
    {
        var oldSyntaxElement = context.LastSyntaxElement;
        context.PrepareFor(_expression);

        if (!TryCompoundUnboxed(context))
        {
            EvaluateInternal(context);
        }

        context.LastSyntaxElement = oldSyntaxElement;
    }

    /// <summary>
    /// Discard-mode fast path: a numeric compound assignment to a slot-stored number binding
    /// computes on raw doubles and stores unboxed, with no materialization of the old or new
    /// value; other slot-stored values complete through <see cref="TryCompoundSlotValue"/>.
    /// The right-hand side runs exactly once. Everything that needs the full semantics
    /// (operator overloading, generators/async, logical/nullish forms, TDZ, const,
    /// dictionary/global/object environments) falls back before any evaluation.
    /// </summary>
    private bool TryCompoundUnboxed(EvaluationContext context)
    {
        var engine = context.Engine;
        if (_leftIdentifier is null
            || context.OperatorOverloadingAllowed
            || engine.ExecutionContext.Suspendable is not null
            || _operator is Operator.NullishCoalescingAssignment or Operator.LogicalAndAssignment or Operator.LogicalOrAssignment)
        {
            return false;
        }

        if (!_slotCache.TryResolve(engine, engine.ExecutionContext.LexicalEnvironment, _leftIdentifier.Identifier, out var declarativeEnvironment, out var slotIndex))
        {
            return false;
        }

        if (!declarativeEnvironment.TryGetNumberSlot(slotIndex, out var left))
        {
            return TryCompoundSlotValue(context, declarativeEnvironment, slotIndex, out _);
        }

        var rval = _right.GetValue(context);

        if (rval is JsNumber rightNumber && _operator != Operator.ExponentiationAssignment)
        {
            var right = rightNumber._value;
            double result;
            switch (_operator)
            {
                case Operator.AdditionAssignment:
                    result = left + right;
                    break;
                case Operator.SubtractionAssignment:
                    result = left - right;
                    break;
                case Operator.MultiplicationAssignment:
                    result = left * right;
                    break;
                case Operator.DivisionAssignment:
                    // IEEE 754 division matches the ECMAScript algorithm for all special cases
                    result = left / right;
                    break;
                case Operator.RemainderAssignment:
                    // IEEE 754 remainder (fmod) matches the ECMAScript algorithm for all special cases
                    result = left % right;
                    break;
                case Operator.BitwiseAndAssignment:
                    result = TypeConverter.ToInt32(left) & TypeConverter.ToInt32(right);
                    break;
                case Operator.BitwiseOrAssignment:
                    result = TypeConverter.ToInt32(left) | TypeConverter.ToInt32(right);
                    break;
                case Operator.BitwiseXorAssignment:
                    result = TypeConverter.ToInt32(left) ^ TypeConverter.ToInt32(right);
                    break;
                case Operator.LeftShiftAssignment:
                    result = TypeConverter.ToInt32(left) << (int) (TypeConverter.ToUint32(right) & 0x1F);
                    break;
                case Operator.RightShiftAssignment:
                    result = TypeConverter.ToInt32(left) >> (int) (TypeConverter.ToUint32(right) & 0x1F);
                    break;
                case Operator.UnsignedRightShiftAssignment:
                    result = (uint) TypeConverter.ToInt32(left) >> (int) (TypeConverter.ToUint32(right) & 0x1F);
                    break;
                default:
                    Throw.NotImplementedException();
                    return false;
            }

            declarativeEnvironment.SetNumberSlot(slotIndex, result);
            return true;
        }

        // rare: number op= non-number (string concat, BigInt mix error, exponentiation).
        // The left operand is a number, never a ConcatenatedString, so in-place mutation
        // cannot apply and the result is always stored.
        var wasMutatedInPlace = false;
        var newLeftValue = ComputeCompound(context, JsNumber.Create(left), rval, ref wasMutatedInPlace);
        declarativeEnvironment.SetMutableBinding(_leftIdentifier.Identifier.Key, newLeftValue, StrictModeScope.IsStrictModeCode);
        return true;
    }

    /// <summary>
    /// Fast path for compound assignment to a slot-stored non-number binding (the string
    /// `s += "x"` loop shape), shared by the discard lane and the value-producing lane:
    /// reads the slot directly, runs the right-hand side exactly once, and writes back through
    /// <see cref="DeclarativeEnvironment.SetMutableBinding(Key, JsValue, bool)"/> unless the
    /// rope concat mutated the value in place (matching the materialized lane's skip). Bails
    /// before any evaluation for const (mutable check) and TDZ/unboxed bindings so the slow
    /// path produces the exact error ordering.
    /// </summary>
    private bool TryCompoundSlotValue(EvaluationContext context, DeclarativeEnvironment environment, int slotIndex, out JsValue result)
    {
        result = null!;

        ref var binding = ref environment._slots![slotIndex];
        if (!binding.Mutable || !binding.HasReferenceValue)
        {
            return false;
        }

        var originalLeftValue = binding.Value;
        var rval = _right.GetValue(context);

        var wasMutatedInPlace = false;
        var newLeftValue = ComputeCompound(context, originalLeftValue, rval, ref wasMutatedInPlace);
        if (!wasMutatedInPlace)
        {
            environment.SetMutableBinding(_leftIdentifier!.Identifier.Key, newLeftValue, StrictModeScope.IsStrictModeCode);
        }

        result = newLeftValue;
        return true;
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

            // If RHS suspended, return immediately so the caller can detect
            // IsSuspended and save state. Without this, the operator-switch
            // below would call _right.GetValue again, re-running side effects
            // inside the await argument and registering a second pair of
            // promise handlers.
            if (context.IsSuspended())
            {
                return rval;
            }

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

        // Unboxed numeric assignment fast path (WS-5): `lhs = a op b` where lhs is a number-slot
        // identifier and a, b are each a number-slot identifier or a numeric literal, for op in
        // {+,-,*,/,%}. Eligibility is decided once in Initialize; everything else falls through.
        private bool _numericFastPath;
        private Operator _numericOperator;
        private SlotLocationCache _lhsSlotCache;
        private JintIdentifierExpression? _numericLeftId;   // null => use _numericLeftLiteral
        private double _numericLeftLiteral;
        private JintIdentifierExpression? _numericRightId;  // null => use _numericRightLiteral
        private double _numericRightLiteral;

        // Whether the assignment has the structural shape `identifier = (id|lit) op (id|lit)` for an
        // arithmetic op. Computed eagerly (from the AST, before the lazy Initialize) so HasDiscardFastPath
        // only routes this shape through EvaluateAndDiscard; every other assignment keeps its exact path.
        private readonly bool _structurallyNumeric;

        public SimpleAssignmentExpression(AssignmentExpression expression) : base(expression)
        {
            _structurallyNumeric = IsNumericAssignmentShape(expression);
        }

        private static bool IsNumericAssignmentShape(AssignmentExpression expression)
        {
            if (expression.Left is not Identifier || expression.Right is not NonLogicalBinaryExpression binary)
            {
                return false;
            }

            // Multiplication is intentionally excluded: the boxed binary path computes 0 * negative as
            // integers and yields +0, whereas raw-double multiplication yields the spec-correct -0, so a
            // double-based fast path would change observable behaviour. Addition/subtraction/division/
            // remainder produce identical results on both paths, keeping this a pure optimization.
            switch (binary.Operator)
            {
                case Operator.Addition:
                case Operator.Subtraction:
                case Operator.Division:
                case Operator.Remainder:
                    return IsIdentifierOrNumericLiteral(binary.Left) && IsIdentifierOrNumericLiteral(binary.Right);
                default:
                    return false;
            }
        }

        private static bool IsIdentifierOrNumericLiteral(Expression node) => node is Identifier or NumericLiteral;

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

            TryEnableNumericFastPath(assignmentExpression);
        }

        private void TryEnableNumericFastPath(AssignmentExpression assignmentExpression)
        {
            if (_leftIdentifier is null || _evalOrArguments || _leftIsCoverParenthesized
                || assignmentExpression.Right is not NonLogicalBinaryExpression binary)
            {
                return;
            }

            switch (binary.Operator)
            {
                case Operator.Addition:
                case Operator.Subtraction:
                case Operator.Division:
                case Operator.Remainder:
                    break;
                default:
                    return;
            }

            if (!TryClassifyNumericOperand(binary.Left, out _numericLeftId, out _numericLeftLiteral)
                || !TryClassifyNumericOperand(binary.Right, out _numericRightId, out _numericRightLiteral))
            {
                return;
            }

            _numericOperator = binary.Operator;
            _numericFastPath = true;
        }

        private static bool TryClassifyNumericOperand(Expression node, out JintIdentifierExpression? id, out double literal)
        {
            id = null;
            literal = 0;
            switch (node)
            {
                case Identifier identifier:
                    id = new JintIdentifierExpression(identifier);
                    return true;
                case NumericLiteral numeric:
                    literal = numeric.Value;
                    return true;
                default:
                    return false;
            }
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

        internal override bool HasDiscardFastPath => _structurallyNumeric;

        internal override void EvaluateAndDiscard(EvaluationContext context)
        {
            if (!_initialized)
            {
                Initialize();
                _initialized = true;
            }

            if (_numericFastPath && TryAssignNumeric(context))
            {
                return;
            }

            EvaluateInternal(context);
        }

        /// <summary>
        /// Discard-mode fast path: a plain assignment whose left side is a slot-stored number and whose
        /// right side is `a op b` over number-slot/literal operands computes on raw doubles and stores
        /// unboxed, with no JsNumber materialization. Operand reads are pure slot reads (never getters),
        /// so declining after a partial read has no observable effect; anything not matching the exact
        /// shape — operator overloading, generators/async, non-number operands, non-slot bindings — falls
        /// back to the boxed path before storing.
        /// </summary>
        private bool TryAssignNumeric(EvaluationContext context)
        {
            var engine = context.Engine;
            if (context.OperatorOverloadingAllowed || engine.ExecutionContext.Suspendable is not null)
            {
                return false;
            }

            // The left side is overwritten, but SetNumberSlot requires a mutable, initialized number
            // slot; TryGetNumberSlot confirms exactly that (and that it is slot-stored at all).
            if (!_lhsSlotCache.TryResolve(engine, engine.ExecutionContext.LexicalEnvironment, _leftIdentifier!.Identifier, out var lhsEnv, out var lhsSlot)
                || !lhsEnv.TryGetNumberSlot(lhsSlot, out _))
            {
                return false;
            }

            if (!TryReadNumericOperand(context, _numericLeftId, _numericLeftLiteral, out var left)
                || !TryReadNumericOperand(context, _numericRightId, _numericRightLiteral, out var right))
            {
                return false;
            }

            double result;
            switch (_numericOperator)
            {
                case Operator.Addition:
                    result = left + right;
                    break;
                case Operator.Subtraction:
                    result = left - right;
                    break;
                case Operator.Division:
                    // IEEE 754 division matches the ECMAScript algorithm for all special cases
                    result = left / right;
                    break;
                case Operator.Remainder:
                    // IEEE 754 remainder (fmod) matches the ECMAScript algorithm for all special cases
                    result = left % right;
                    break;
                default:
                    return false;
            }

            lhsEnv.SetNumberSlot(lhsSlot, result);
            return true;
        }

        private static bool TryReadNumericOperand(EvaluationContext context, JintIdentifierExpression? id, double literal, out double value)
        {
            if (id is null)
            {
                value = literal;
                return true;
            }

            return id.TryReadNumber(context, out value);
        }

        // https://262.ecma-international.org/5.1/#sec-11.13.1
        private JsValue SetValue(EvaluationContext context)
        {
            var engine = context.Engine;

            // Write-side inline cache for `obj.prop = rhs` (mirrors JintMemberExpression.GetValue's read cache):
            // resolves base+rhs once and stores straight into a live writable data descriptor, otherwise completes
            // through PutValue. Only declines (returns false) before evaluating anything, so the slow path stays sound.
            if (_left is JintMemberExpression memberLeft
                && memberLeft.TryAssignFast(context, _right, out var fastResult))
            {
                return fastResult;
            }

            // slower version
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

            // Fast path for dense array element overwrite: arr[intIndex] = rval. Only overwrites an
            // existing in-range slot (TryWriteExistingDense), so it never grows length, fills a hole,
            // or triggers sparse conversion — those defer to the full PutValue path below.
            if (lref.Base is JsArray array
                && array.CanUseFastAccess
                && lref.ReferencedName is JsNumber indexNumber
                && ArrayInstance.IsArrayIndex(indexNumber, out var arrayIndex)
                && array.TryWriteExistingDense(arrayIndex, rval))
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

            // Global-binding fast path: write directly through the cached plain writable
            // MutableBinding data descriptor. The null pre-check keeps the cost for non-global
            // identifiers to a single field test; the rest stays out-of-line.
            if (left._cachedGlobalEnv is not null)
            {
                var cachedGlobalDescriptor = left.TryGetValidatedGlobalDescriptor(engine, env);
                if (cachedGlobalDescriptor is not null)
                {
                    return AssignToCachedGlobalBinding(context, left, right, cachedGlobalDescriptor, hasEvalOrArguments, nameAnonymousFunction, strict);
                }
            }

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

                // Populate the global-binding cache from the write side too, so write-first
                // patterns benefit from the next access on. Must run after the set: the set
                // may have created the property (bumping the shape version).
                if (ReferenceEquals(environmentRecord, env) && environmentRecord is GlobalEnvironment globalEnv)
                {
                    left.TryRememberGlobalBinding(globalEnv);
                }

                return rval;
            }

            return null;
        }

        /// <summary>
        /// The cached-global-binding arm of <see cref="AssignToIdentifier"/>; semantics are
        /// identical to its slow path with the final store mirroring
        /// GlobalObject.SetFromMutableBinding's writable MutableBinding data-property fast path.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static JsValue AssignToCachedGlobalBinding(
            EvaluationContext context,
            JintIdentifierExpression left,
            JintExpression right,
            PropertyDescriptor descriptor,
            bool hasEvalOrArguments,
            bool nameAnonymousFunction,
            bool strict)
        {
            var engine = context.Engine;
            var identifier = left.Identifier;

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

            descriptor._value = rval;
            return rval;
        }
    }
}
