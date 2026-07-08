using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Jint.Extensions;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Environments;
using Jint.Runtime.Interop;

namespace Jint.Runtime.Interpreter.Expressions;

internal abstract class JintBinaryExpression : JintExpression
{
    private readonly record struct OperatorKey(string? OperatorName, Type Left, Type Right);
    private static readonly ConcurrentDictionary<OperatorKey, MethodDescriptor> _knownOperators = new();

    private protected readonly JintExpression _left;
    private protected readonly JintExpression _right;

    private JintBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
    {
        // TODO check https://tc39.es/ecma262/#sec-applystringornumericbinaryoperator
        _left = Build(expression.Left);
        _right = Build(expression.Right);
    }

    /// <summary>
    /// Per-node fast lane for the canonical loop-test shapes `identifier &lt;op&gt; numericConstant`
    /// and `identifier &lt;op&gt; identifier` (a variable bound): when the operands resolve to
    /// slot-stored numbers (or a constant), the comparison runs on raw doubles without
    /// materializing anything — which also keeps an unboxed counter unboxed instead of
    /// ping-ponging between its update (stores raw) and its loop test (would materialize).
    /// Slot reads are pure, so declining after a partial read has no observable effect.
    /// IEEE double comparisons reproduce the abstract relational operator for all four forms,
    /// including NaN operands (spec result undefined, coerced to false). For the equality
    /// operators the operands are runtime-proven Numbers, so ==/===/!=/!== degenerate to the
    /// numeric compare (IsLooselyEqual step 1 defers to IsStrictlyEqual for same-type operands):
    /// IEEE == gives NaN == NaN false and +0 == -0 true, both spec-exact.
    /// </summary>
    private protected struct NumericConstantComparisonLane
    {
        private JintIdentifierExpression? _identifier;
        private JintIdentifierExpression? _rightIdentifier;   // null => use _constant
        private double _constant;
        private SlotLocationCache _slotCache;
        private SlotLocationCache _rightSlotCache;

        public void Initialize(JintExpression left, JintExpression right)
        {
            if (left is not JintIdentifierExpression identifier)
            {
                return;
            }

            switch (right)
            {
                case JintConstantExpression { Value: JsNumber number }:
                    _identifier = identifier;
                    _constant = number._value;
                    break;
                case JintIdentifierExpression rightIdentifier:
                    _identifier = identifier;
                    _rightIdentifier = rightIdentifier;
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetOperands(EvaluationContext context, out double left, out double right)
        {
            left = 0;
            right = _constant;

            var identifier = _identifier;
            if (identifier is null || context.OperatorOverloadingAllowed)
            {
                return false;
            }

            var engine = context.Engine;
            if (engine.ExecutionContext.Suspendable is not null)
            {
                return false;
            }

            var env = engine.ExecutionContext.LexicalEnvironment;
            if (!_slotCache.TryResolve(engine, env, identifier.Identifier, out var environment, out var slotIndex)
                || !environment.TryGetNumberSlot(slotIndex, out left))
            {
                // top-level vars are global-object properties, never slots: fall back to the
                // validated global-descriptor cache (the stopwatch-shape loop test). The null
                // pre-check keeps the cost for plain slot misses to a single field test.
                if (identifier._cachedGlobalEnv is null || !TryReadGlobalNumber(identifier, engine, env, out left))
                {
                    return false;
                }
            }

            var rightIdentifier = _rightIdentifier;
            if (rightIdentifier is null)
            {
                return true;
            }

            if (_rightSlotCache.TryResolve(engine, env, rightIdentifier.Identifier, out var rightEnvironment, out var rightSlotIndex)
                && rightEnvironment.TryGetNumberSlot(rightSlotIndex, out right))
            {
                return true;
            }

            return rightIdentifier._cachedGlobalEnv is not null && TryReadGlobalNumber(rightIdentifier, engine, env, out right);
        }
    }

    /// <summary>
    /// Global-binding arm of the lane operand reads: validated-cache probe plus a field read,
    /// both pure — so a decline (or a left read followed by a right-side decline) has no
    /// observable effect. The value type is re-checked on every read because in-place value
    /// mutations bump no version. Out of line so lane misses don't grow the inlined probes.
    /// Callers pre-check <see cref="JintIdentifierExpression._cachedGlobalEnv"/> for null.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private protected static bool TryReadGlobalNumber(JintIdentifierExpression identifier, Engine engine, Environments.Environment env, out double value)
    {
        if (identifier.TryGetValidatedGlobalDescriptor(engine, env) is { _value: JsNumber number })
        {
            value = number._value;
            return true;
        }

        value = 0;
        return false;
    }

    /// <summary>
    /// Fused lane for the `identifier % numericConstant == numericConstant` test — the
    /// stopwatch.js if-chain shape. Reads the identifier through the same slot/global arms as
    /// <see cref="NumericConstantComparisonLane"/> and computes the whole test on raw doubles.
    /// C# double % implements Number::remainder exactly, and the ways integer and double
    /// remainders can disagree (−0 vs +0 results) are erased by the equality consumer
    /// (IEEE == treats ±0 as equal), so the fused result is spec-exact for any proven-Number
    /// operand: NaN dividends/divisors (and % 0) compare false, v % ±Infinity == v.
    /// </summary>
    private protected struct ModuloEqualityLane
    {
        private JintIdentifierExpression? _identifier;
        private double _divisor;
        private double _expected;
        private SlotLocationCache _slotCache;

        public void Initialize(JintExpression left, JintExpression right)
        {
            if (left is ModuloBinaryExpression { _left: JintIdentifierExpression identifier, _right: JintConstantExpression { Value: JsNumber divisor } }
                && right is JintConstantExpression { Value: JsNumber expected })
            {
                _identifier = identifier;
                _divisor = divisor._value;
                _expected = expected._value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEvaluate(EvaluationContext context, out bool equal)
        {
            equal = false;

            var identifier = _identifier;
            if (identifier is null || context.OperatorOverloadingAllowed)
            {
                return false;
            }

            var engine = context.Engine;
            if (engine.ExecutionContext.Suspendable is not null)
            {
                return false;
            }

            var env = engine.ExecutionContext.LexicalEnvironment;
            if (!_slotCache.TryResolve(engine, env, identifier.Identifier, out var environment, out var slotIndex)
                || !environment.TryGetNumberSlot(slotIndex, out var value))
            {
                if (identifier._cachedGlobalEnv is null || !TryReadGlobalNumber(identifier, engine, env, out value))
                {
                    return false;
                }
            }

            equal = value % _divisor == _expected;
            return true;
        }
    }

    /// <summary>
    /// Evaluates both operands with proper suspension checks for async/generator functions.
    /// Returns false if evaluation was suspended (caller should return early).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool TryEvaluateOperands(EvaluationContext context, out JsValue left, out JsValue right)
    {
        var suspendable = context.Engine?.ExecutionContext.Suspendable;
        if (suspendable is { IsResuming: true }
            && suspendable.Data.TryGet(this, out LeftOperandSuspendData? suspendData))
        {
            left = suspendData!.LeftValue;
        }
        else
        {
            left = _left.GetValue(context);
            if (context.IsSuspended())
            {
                right = JsValue.Undefined;
                return false;
            }
        }

        right = _right.GetValue(context);
        if (context.IsSuspended())
        {
            if (suspendable is not null)
            {
                suspendable.Data.GetOrCreate<LeftOperandSuspendData>(this).LeftValue = left;
            }

            return false;
        }

        suspendable?.Data.Clear(this);
        return true;
    }

    private readonly record struct MethodResolverState(JsCallArguments Arguments);

    internal static bool TryOperatorOverloading(
        EvaluationContext context,
        JsValue leftValue,
        JsValue rightValue,
        string? clrName,
        [NotNullWhen(true)] out object? result)
    {
        var left = leftValue.ToObject();
        var right = rightValue.ToObject();

        if (left != null && right != null)
        {
            var leftType = left.GetType();
            var rightType = right.GetType();
            var arguments = new[] { leftValue, rightValue };

            var key = new OperatorKey(clrName, leftType, rightType);
            var method = _knownOperators.GetOrAdd(key, _ =>
            {
                var leftMethods = leftType.GetOperatorOverloadMethods();
                var rightMethods = rightType.GetOperatorOverloadMethods();

                var methods = leftMethods.Concat(rightMethods).Where(x => string.Equals(x.Name, clrName, StringComparison.Ordinal) && x.GetParameters().Length == 2);
                var methodDescriptors = MethodDescriptor.Build(methods.ToArray());

                return InteropHelper.FindBestMatch(context.Engine, methodDescriptors, static (_, state) => state.Arguments, new MethodResolverState(arguments)).FirstOrDefault().Method;
            });

            if (method != null)
            {
                try
                {
                    result = method.Call(context.Engine, null, arguments);
                    return true;
                }
                catch (Exception e)
                {
                    Throw.MeaningfulException(context.Engine, new TargetInvocationException(e.InnerException));
                    result = null;
                    return false;
                }
            }
        }

        result = null;
        return false;
    }

    internal static JintExpression Build(NonLogicalBinaryExpression expression)
    {
        JintBinaryExpression? result = null;
        switch (expression.Operator)
        {
            case Operator.StrictEquality:
                result = new StrictlyEqualBinaryExpression(expression);
                break;
            case Operator.StrictInequality:
                result = new StrictlyNotEqualBinaryExpression(expression);
                break;
            case Operator.LessThan:
                result = new LessBinaryExpression(expression);
                break;
            case Operator.GreaterThan:
                result = new GreaterBinaryExpression(expression);
                break;
            case Operator.Addition:
                if (TryBuildStringConcatenation(expression, out var concatExpr))
                {
                    return concatExpr;
                }
                result = new PlusBinaryExpression(expression);
                break;
            case Operator.Subtraction:
                result = new MinusBinaryExpression(expression);
                break;
            case Operator.Multiplication:
                result = new TimesBinaryExpression(expression);
                break;
            case Operator.Division:
                result = new DivideBinaryExpression(expression);
                break;
            case Operator.Equality:
                result = new EqualBinaryExpression(expression);
                break;
            case Operator.Inequality:
                result = new EqualBinaryExpression(expression, invert: true);
                break;
            case Operator.GreaterThanOrEqual:
                result = new CompareBinaryExpression(expression, leftFirst: true);
                break;
            case Operator.LessThanOrEqual:
                result = new CompareBinaryExpression(expression, leftFirst: false);
                break;
            case Operator.BitwiseAnd:
            case Operator.BitwiseOr:
            case Operator.BitwiseXor:
            case Operator.LeftShift:
            case Operator.RightShift:
            case Operator.UnsignedRightShift:
                result = new BitwiseBinaryExpression(expression);
                break;
            case Operator.InstanceOf:
                result = new InstanceOfBinaryExpression(expression);
                break;
            case Operator.Exponentiation:
                result = new ExponentiationBinaryExpression(expression);
                break;
            case Operator.Remainder:
                result = new ModuloBinaryExpression(expression);
                break;
            case Operator.In:
                result = new InBinaryExpression(expression);
                break;
            default:
                Throw.ArgumentOutOfRangeException(nameof(expression.Operator), "cannot handle operator");
                break;
        }

        if (expression.Operator != Operator.InstanceOf
            && expression.Operator != Operator.In
            && expression.Left is Literal leftLiteral
            && expression.Right is Literal rightLiteral)
        {
            var lval = JintLiteralExpression.ConvertToJsValue(leftLiteral);
            var rval = JintLiteralExpression.ConvertToJsValue(rightLiteral);

            if (lval is not null && rval is not null)
            {
                // we have fixed result
                try
                {
                    var context = new EvaluationContext();
                    return new JintConstantExpression(expression, (JsValue) result.EvaluateWithoutNodeTracking(context));
                }
                catch
                {
                    // probably caused an error and error reporting doesn't work without engine
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Detects left-recursive chains of '+' operations that include at least one string literal,
    /// and flattens them into a single StringConcatenationExpression to avoid intermediate allocations.
    /// </summary>
    private static bool TryBuildStringConcatenation(
        NonLogicalBinaryExpression expression,
        [NotNullWhen(true)] out JintExpression? result)
    {
        result = null;

        // Only optimize chains of 3+ operands (at least 2 nested additions)
        if (expression.Left is not NonLogicalBinaryExpression { Operator: Operator.Addition })
        {
            return false;
        }

        // Collect all operands by walking the left-recursive chain
        var operands = new List<Expression>();
        CollectAdditionOperands(expression, operands);

        // Must have a string literal in the first two operands to guarantee string concatenation semantics
        // from the very first operation. A string literal at index 2+ is not enough because earlier operands
        // could be numerically added (e.g., 2.0 + 3.0 + 'm' should yield '5m', not '23m').
        // The early return above guarantees at least 3 operands, so operands[0] and operands[1] are always valid.
        if (operands.Count < 2 || (operands[0] is not Literal { Value: string } && operands[1] is not Literal { Value: string }))
        {
            return false;
        }

        result = new StringConcatenationExpression(expression, operands.ToArray());
        return true;
    }

    private static void CollectAdditionOperands(Expression expression, List<Expression> operands)
    {
        if (expression is NonLogicalBinaryExpression { Operator: Operator.Addition } binary)
        {
            CollectAdditionOperands(binary.Left, operands);
            operands.Add(binary.Right);
        }
        else
        {
            operands.Add(expression);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool AreNonBigIntOperands(JsValue left, JsValue right)
    {
        return left._type != InternalTypes.BigInt && right._type != InternalTypes.BigInt;
    }

    internal static void AssertValidBigIntArithmeticOperands(JsValue left, JsValue right)
    {
        if (left.Type != right.Type)
        {
            Throw.TypeErrorNoEngine("Cannot mix BigInt and other types, use explicit conversions");
        }
    }

    /// <summary>
    /// Validates that BigInteger.Pow(base, exponent) won't produce an excessively large result.
    /// Limits result to ~1 million bits (~125 KB) to prevent memory exhaustion.
    /// </summary>
    internal static void ValidateBigIntPowSize(Realm realm, BigInteger baseValue, int exponent)
    {
        if (exponent > 0)
        {
            var absBase = BigInteger.Abs(baseValue);
            if (absBase > BigInteger.One
                && (double) exponent * BigInteger.Log(absBase, 2.0) > 1_000_000)
            {
                Throw.RangeError(realm, "Maximum BigInt size exceeded");
            }
        }
    }

    private sealed class StrictlyEqualBinaryExpression : JintBinaryExpression
    {
        private NumericConstantComparisonLane _numericLane;
        private ModuloEqualityLane _moduloLane;

        public StrictlyEqualBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
        {
            _numericLane.Initialize(_left, _right);
            _moduloLane.Initialize(_left, _right);
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            if (_numericLane.TryGetOperands(context, out var unboxedLeft, out var unboxedRight))
            {
                return unboxedLeft == unboxedRight ? JsBoolean.True : JsBoolean.False;
            }

            if (_moduloLane.TryEvaluate(context, out var moduloEqual))
            {
                return moduloEqual ? JsBoolean.True : JsBoolean.False;
            }

            if (!TryEvaluateOperands(context, out var left, out var right))
            {
                return JsValue.Undefined;
            }

            var equal = left == right;
            return equal ? JsBoolean.True : JsBoolean.False;
        }

        public override bool GetBooleanValue(EvaluationContext context)
        {
            if (_numericLane.TryGetOperands(context, out var unboxedLeft, out var unboxedRight))
            {
                return unboxedLeft == unboxedRight;
            }

            if (_moduloLane.TryEvaluate(context, out var moduloEqual))
            {
                return moduloEqual;
            }

            if (!TryEvaluateOperands(context, out var left, out var right))
            {
                return false;
            }

            return left == right;
        }
    }

    private sealed class StrictlyNotEqualBinaryExpression : JintBinaryExpression
    {
        private NumericConstantComparisonLane _numericLane;
        private ModuloEqualityLane _moduloLane;

        public StrictlyNotEqualBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
        {
            _numericLane.Initialize(_left, _right);
            _moduloLane.Initialize(_left, _right);
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            if (_numericLane.TryGetOperands(context, out var unboxedLeft, out var unboxedRight))
            {
                return unboxedLeft == unboxedRight ? JsBoolean.False : JsBoolean.True;
            }

            if (_moduloLane.TryEvaluate(context, out var moduloEqual))
            {
                return moduloEqual ? JsBoolean.False : JsBoolean.True;
            }

            if (!TryEvaluateOperands(context, out var left, out var right))
            {
                return JsValue.Undefined;
            }

            return left == right ? JsBoolean.False : JsBoolean.True;
        }

        public override bool GetBooleanValue(EvaluationContext context)
        {
            if (_numericLane.TryGetOperands(context, out var unboxedLeft, out var unboxedRight))
            {
                return unboxedLeft != unboxedRight;
            }

            if (_moduloLane.TryEvaluate(context, out var moduloEqual))
            {
                return !moduloEqual;
            }

            if (!TryEvaluateOperands(context, out var left, out var right))
            {
                return false;
            }

            return left != right;
        }
    }

    private sealed class LessBinaryExpression : JintBinaryExpression
    {
        private NumericConstantComparisonLane _numericLane;

        public LessBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
        {
            _numericLane.Initialize(_left, _right);
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            if (_numericLane.TryGetOperands(context, out var unboxedLeft, out var unboxedRight))
            {
                return unboxedLeft < unboxedRight ? JsBoolean.True : JsBoolean.False;
            }

            if (!TryEvaluateOperands(context, out var left, out var right))
            {
                return JsValue.Undefined;
            }

            if (context.OperatorOverloadingAllowed
                && TryOperatorOverloading(context, left, right, "op_LessThan", out var opResult))
            {
                return JsValue.FromObject(context.Engine, opResult);
            }

            var value = Compare(left, right);

            return value._type == InternalTypes.Undefined ? JsBoolean.False : value;
        }

        public override bool GetBooleanValue(EvaluationContext context)
        {
            if (_numericLane.TryGetOperands(context, out var unboxedLeft, out var unboxedRight))
            {
                return unboxedLeft < unboxedRight;
            }

            if (!TryEvaluateOperands(context, out var left, out var right))
            {
                return false;
            }

            if (context.OperatorOverloadingAllowed
                && TryOperatorOverloading(context, left, right, "op_LessThan", out var opResult))
            {
                return TypeConverter.ToBoolean(JsValue.FromObject(context.Engine, opResult));
            }

            var value = Compare(left, right);
            return value._type != InternalTypes.Undefined && ((JsBoolean) value)._value;
        }
    }

    private sealed class GreaterBinaryExpression : JintBinaryExpression
    {
        private NumericConstantComparisonLane _numericLane;

        public GreaterBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
        {
            _numericLane.Initialize(_left, _right);
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            if (_numericLane.TryGetOperands(context, out var unboxedLeft, out var unboxedRight))
            {
                return unboxedLeft > unboxedRight ? JsBoolean.True : JsBoolean.False;
            }

            if (!TryEvaluateOperands(context, out var left, out var right))
            {
                return JsValue.Undefined;
            }

            if (context.OperatorOverloadingAllowed
                && TryOperatorOverloading(context, left, right, "op_GreaterThan", out var opResult))
            {
                return JsValue.FromObject(context.Engine, opResult);
            }

            var value = Compare(right, left, false);

            return value._type == InternalTypes.Undefined ? JsBoolean.False : value;
        }

        public override bool GetBooleanValue(EvaluationContext context)
        {
            if (_numericLane.TryGetOperands(context, out var unboxedLeft, out var unboxedRight))
            {
                return unboxedLeft > unboxedRight;
            }

            if (!TryEvaluateOperands(context, out var left, out var right))
            {
                return false;
            }

            if (context.OperatorOverloadingAllowed
                && TryOperatorOverloading(context, left, right, "op_GreaterThan", out var opResult))
            {
                return TypeConverter.ToBoolean(JsValue.FromObject(context.Engine, opResult));
            }

            var value = Compare(right, left, false);
            return value._type != InternalTypes.Undefined && ((JsBoolean) value)._value;
        }
    }

    private sealed class PlusBinaryExpression : JintBinaryExpression
    {
        public PlusBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
        {
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            if (!TryEvaluateOperands(context, out var left, out var right))
            {
                return JsValue.Undefined;
            }

            if (context.OperatorOverloadingAllowed
                && TryOperatorOverloading(context, left, right, "op_Addition", out var opResult))
            {
                return JsValue.FromObject(context.Engine, opResult);
            }

            if (AreIntegerOperands(left, right))
            {
                return JsNumber.Create((long) left.AsInteger() + right.AsInteger());
            }

            if (left._type == InternalTypes.Number && right._type == InternalTypes.Number)
            {
                return JsNumber.Create(((JsNumber) left)._value + ((JsNumber) right)._value);
            }

            var lprim = TypeConverter.ToPrimitive(left);
            var rprim = TypeConverter.ToPrimitive(right);
            JsValue result;
            if (lprim.IsString() || rprim.IsString())
            {
                result = JsString.Create(TypeConverter.ToString(lprim) + TypeConverter.ToString(rprim));
            }
            else if (AreNonBigIntOperands(left, right))
            {
                result = JsNumber.Create(TypeConverter.ToNumber(lprim) + TypeConverter.ToNumber(rprim));
            }
            else
            {
                AssertValidBigIntArithmeticOperands(lprim, rprim);
                result = JsBigInt.Create(TypeConverter.ToBigInt(lprim) + TypeConverter.ToBigInt(rprim));
            }

            return result;
        }
    }

    /// <summary>
    /// Optimized expression for chains of string concatenation (e.g., a + b + c + d).
    /// Evaluates all operands and concatenates in a single pass using a ValueStringBuilder,
    /// avoiding intermediate JsString allocations.
    /// </summary>
    private sealed class StringConcatenationExpression : JintExpression
    {
        private readonly Expression[] _operandExpressions;
        private JintExpression[]? _operands;

        public StringConcatenationExpression(Expression expression, Expression[] operandExpressions)
            : base(expression)
        {
            _operandExpressions = operandExpressions;
        }

        private void EnsureInitialized()
        {
            if (_operands is not null)
            {
                return;
            }

            _operands = new JintExpression[_operandExpressions.Length];
            for (var i = 0; i < _operandExpressions.Length; i++)
            {
                _operands[i] = Build(_operandExpressions[i]);
            }
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            EnsureInitialized();

            var operands = _operands!;
            var count = operands.Length;

            // Fast path for small chains — use string.Concat overloads
            if (count == 3)
            {
                var v0 = operands[0].GetValue(context);
                if (context.IsSuspended()) return JsValue.Undefined;
                var v1 = operands[1].GetValue(context);
                if (context.IsSuspended()) return JsValue.Undefined;
                var v2 = operands[2].GetValue(context);
                if (context.IsSuspended()) return JsValue.Undefined;

                return JsString.Create(string.Concat(
                    TypeConverter.ToString(TypeConverter.ToPrimitive(v0)),
                    TypeConverter.ToString(TypeConverter.ToPrimitive(v1)),
                    TypeConverter.ToString(TypeConverter.ToPrimitive(v2))));
            }

            if (count == 4)
            {
                var v0 = operands[0].GetValue(context);
                if (context.IsSuspended()) return JsValue.Undefined;
                var v1 = operands[1].GetValue(context);
                if (context.IsSuspended()) return JsValue.Undefined;
                var v2 = operands[2].GetValue(context);
                if (context.IsSuspended()) return JsValue.Undefined;
                var v3 = operands[3].GetValue(context);
                if (context.IsSuspended()) return JsValue.Undefined;

                return JsString.Create(string.Concat(
                    TypeConverter.ToString(TypeConverter.ToPrimitive(v0)),
                    TypeConverter.ToString(TypeConverter.ToPrimitive(v1)),
                    TypeConverter.ToString(TypeConverter.ToPrimitive(v2)),
                    TypeConverter.ToString(TypeConverter.ToPrimitive(v3))));
            }

            // General path for 5+ operands
            var strings = new string[count];
            for (var i = 0; i < count; i++)
            {
                var val = operands[i].GetValue(context);
                if (context.IsSuspended()) return JsValue.Undefined;
                strings[i] = TypeConverter.ToString(TypeConverter.ToPrimitive(val));
            }

            return JsString.Create(string.Concat(strings));
        }
    }

    private sealed class MinusBinaryExpression : JintBinaryExpression
    {
        public MinusBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
        {
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            if (!TryEvaluateOperands(context, out var left, out var right))
            {
                return JsValue.Undefined;
            }

            if (context.OperatorOverloadingAllowed
                && TryOperatorOverloading(context, left, right, "op_Subtraction", out var opResult))
            {
                return JsValue.FromObject(context.Engine, opResult);
            }

            if (AreIntegerOperands(left, right))
            {
                return JsNumber.Create((long) left.AsInteger() - right.AsInteger());
            }

            if (left._type == InternalTypes.Number && right._type == InternalTypes.Number)
            {
                return JsNumber.Create(((JsNumber) left)._value - ((JsNumber) right)._value);
            }

            left = TypeConverter.ToNumeric(left);
            right = TypeConverter.ToNumeric(right);

            JsValue number;
            if (AreNonBigIntOperands(left, right))
            {
                number = JsNumber.Create(left.AsNumber() - right.AsNumber());
            }
            else
            {
                JintBinaryExpression.AssertValidBigIntArithmeticOperands(left, right);
                number = JsBigInt.Create(TypeConverter.ToBigInt(left) - TypeConverter.ToBigInt(right));
            }

            return number;
        }
    }

    private sealed class TimesBinaryExpression : JintBinaryExpression
    {
        public TimesBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
        {
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            if (!TryEvaluateOperands(context, out var left, out var right))
            {
                return JsValue.Undefined;
            }

            JsValue result;
            if (context.OperatorOverloadingAllowed
                && TryOperatorOverloading(context, left, right, "op_Multiply", out var opResult))
            {
                result = JsValue.FromObject(context.Engine, opResult);
            }
            else if (AreIntegerOperands(left, right))
            {
                result = JsNumber.Create((long) left.AsInteger() * right.AsInteger());
            }
            else if (left._type == InternalTypes.Number && right._type == InternalTypes.Number)
            {
                result = JsNumber.Create(((JsNumber) left)._value * ((JsNumber) right)._value);
            }
            else
            {
                var leftNumeric = TypeConverter.ToNumeric(left);
                var rightNumeric = TypeConverter.ToNumeric(right);

                if (leftNumeric.IsNumber() && rightNumeric.IsNumber())
                {
                    result = JsNumber.Create(leftNumeric.AsNumber() * rightNumeric.AsNumber());
                }
                else
                {
                    AssertValidBigIntArithmeticOperands(leftNumeric, rightNumeric);
                    result = JsBigInt.Create(leftNumeric.AsBigInt() * rightNumeric.AsBigInt());
                }
            }

            return result;
        }
    }

    private sealed class DivideBinaryExpression : JintBinaryExpression
    {
        public DivideBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
        {
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            if (!TryEvaluateOperands(context, out var left, out var right))
            {
                return JsValue.Undefined;
            }

            if (context.OperatorOverloadingAllowed
                && TryOperatorOverloading(context, left, right, "op_Division", out var opResult))
            {
                return JsValue.FromObject(context.Engine, opResult);
            }

            if (left._type == InternalTypes.Number && right._type == InternalTypes.Number)
            {
                return JsNumber.Create(((JsNumber) left)._value / ((JsNumber) right)._value);
            }

            left = TypeConverter.ToNumeric(left);
            right = TypeConverter.ToNumeric(right);
            return Divide(context, left, right);
        }
    }

    private sealed class EqualBinaryExpression : JintBinaryExpression
    {
        private readonly bool _invert;
        private NumericConstantComparisonLane _numericLane;
        private ModuloEqualityLane _moduloLane;

        public EqualBinaryExpression(NonLogicalBinaryExpression expression, bool invert = false) : base(expression)
        {
            _invert = invert;
            _numericLane.Initialize(_left, _right);
            _moduloLane.Initialize(_left, _right);
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            if (_numericLane.TryGetOperands(context, out var unboxedLeft, out var unboxedRight))
            {
                return (unboxedLeft == unboxedRight) == !_invert ? JsBoolean.True : JsBoolean.False;
            }

            if (_moduloLane.TryEvaluate(context, out var moduloEqual))
            {
                return moduloEqual == !_invert ? JsBoolean.True : JsBoolean.False;
            }

            if (!TryEvaluateOperands(context, out var left, out var right))
            {
                return JsValue.Undefined;
            }

            if (context.OperatorOverloadingAllowed
                && TryOperatorOverloading(context, left, right, _invert ? "op_Inequality" : "op_Equality", out var opResult))
            {
                return JsValue.FromObject(context.Engine, opResult);
            }

            // if types match, we can take faster strict equality
            var equality = left.Type == right.Type
                ? left.Equals(right)
                : left.IsLooselyEqual(right);

            return equality == !_invert ? JsBoolean.True : JsBoolean.False;
        }

        public override bool GetBooleanValue(EvaluationContext context)
        {
            if (_numericLane.TryGetOperands(context, out var unboxedLeft, out var unboxedRight))
            {
                var equal = unboxedLeft == unboxedRight;
                return _invert ? !equal : equal;
            }

            if (_moduloLane.TryEvaluate(context, out var moduloEqual))
            {
                return _invert ? !moduloEqual : moduloEqual;
            }

            if (!TryEvaluateOperands(context, out var left, out var right))
            {
                return false;
            }

            if (context.OperatorOverloadingAllowed
                && TryOperatorOverloading(context, left, right, _invert ? "op_Inequality" : "op_Equality", out var opResult))
            {
                return TypeConverter.ToBoolean(JsValue.FromObject(context.Engine, opResult));
            }

            var equality = left.Type == right.Type
                ? left.Equals(right)
                : left.IsLooselyEqual(right);

            return _invert ? !equality : equality;
        }
    }

    private sealed class CompareBinaryExpression : JintBinaryExpression
    {
        private readonly bool _leftFirst;
        private NumericConstantComparisonLane _numericLane;

        public CompareBinaryExpression(NonLogicalBinaryExpression expression, bool leftFirst) : base(expression)
        {
            _leftFirst = leftFirst;
            _numericLane.Initialize(_left, _right);
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            if (_numericLane.TryGetOperands(context, out var unboxedLeft, out var unboxedRight))
            {
                // leftFirst == true is `>=`, false is `<=`; NaN yields false either way,
                // matching the undefined completion below
                var result = _leftFirst ? unboxedLeft >= unboxedRight : unboxedLeft <= unboxedRight;
                return result ? JsBoolean.True : JsBoolean.False;
            }

            if (!TryEvaluateOperands(context, out var leftValue, out var rightValue))
            {
                return JsValue.Undefined;
            }

            if (context.OperatorOverloadingAllowed
                && TryOperatorOverloading(context, leftValue, rightValue, _leftFirst ? "op_GreaterThanOrEqual" : "op_LessThanOrEqual", out var opResult))
            {
                return JsValue.FromObject(context.Engine, opResult);
            }

            var left = _leftFirst ? leftValue : rightValue;
            var right = _leftFirst ? rightValue : leftValue;

            var value = Compare(left, right, _leftFirst);
            return value.IsUndefined() || ((JsBoolean) value)._value ? JsBoolean.False : JsBoolean.True;
        }

        public override bool GetBooleanValue(EvaluationContext context)
        {
            if (_numericLane.TryGetOperands(context, out var unboxedLeft, out var unboxedRight))
            {
                return _leftFirst ? unboxedLeft >= unboxedRight : unboxedLeft <= unboxedRight;
            }

            if (!TryEvaluateOperands(context, out var leftValue, out var rightValue))
            {
                return false;
            }

            if (context.OperatorOverloadingAllowed
                && TryOperatorOverloading(context, leftValue, rightValue, _leftFirst ? "op_GreaterThanOrEqual" : "op_LessThanOrEqual", out var opResult))
            {
                return TypeConverter.ToBoolean(JsValue.FromObject(context.Engine, opResult));
            }

            var left = _leftFirst ? leftValue : rightValue;
            var right = _leftFirst ? rightValue : leftValue;

            var value = Compare(left, right, _leftFirst);
            return !value.IsUndefined() && !((JsBoolean) value)._value;
        }
    }

    private sealed class InstanceOfBinaryExpression : JintBinaryExpression
    {
        public InstanceOfBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
        {
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            if (!TryEvaluateOperands(context, out var leftValue, out var rightValue))
            {
                return JsValue.Undefined;
            }

            return leftValue.InstanceofOperator(rightValue) ? JsBoolean.True : JsBoolean.False;
        }
    }

    private sealed class ExponentiationBinaryExpression : JintBinaryExpression
    {
        public ExponentiationBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
        {
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            if (!TryEvaluateOperands(context, out var leftReference, out var rightReference))
            {
                return JsValue.Undefined;
            }

            var left = TypeConverter.ToNumeric(leftReference);
            var right = TypeConverter.ToNumeric(rightReference);

            return Exponentiate(context, left, right);
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-numeric-types-number-exponentiate (+ BigInt variant).
    /// Operands must already be numeric (ToNumeric applied). Shared by ** and **=.
    /// </summary>
    internal static JsValue Exponentiate(EvaluationContext context, JsValue left, JsValue right)
    {
        JsValue result;
        if (AreNonBigIntOperands(left, right))
        {
            // validation
            var baseNumber = (JsNumber) left;
            var exponentNumber = (JsNumber) right;

            if (exponentNumber.IsNaN())
            {
                return JsNumber.DoubleNaN;
            }

            if (exponentNumber.IsZero())
            {
                return JsNumber.PositiveOne;
            }

            if (baseNumber.IsNaN())
            {
                return JsNumber.DoubleNaN;
            }

            var exponentValue = exponentNumber._value;
            if (baseNumber.IsPositiveInfinity())
            {
                return exponentValue > 0 ? JsNumber.DoublePositiveInfinity : JsNumber.PositiveZero;
            }

            static bool IsOddIntegral(double value) => TypeConverter.IsIntegralNumber(value) && value % 2 != 0;

            if (baseNumber.IsNegativeInfinity())
            {
                if (exponentValue > 0)
                {
                    return IsOddIntegral(exponentValue) ? JsNumber.DoubleNegativeInfinity : JsNumber.DoublePositiveInfinity;
                }

                return IsOddIntegral(exponentValue) ? JsNumber.NegativeZero : JsNumber.PositiveZero;
            }

            if (baseNumber.IsPositiveZero())
            {
                return exponentValue > 0 ? JsNumber.PositiveZero : JsNumber.DoublePositiveInfinity;
            }

            if (baseNumber.IsNegativeZero())
            {
                if (exponentValue > 0)
                {
                    return IsOddIntegral(exponentValue) ? JsNumber.NegativeZero : JsNumber.PositiveZero;
                }
                return IsOddIntegral(exponentValue) ? JsNumber.DoubleNegativeInfinity : JsNumber.DoublePositiveInfinity;
            }

            var baseValue = baseNumber._value;
            if (exponentNumber.IsPositiveInfinity())
            {
                var absBase = Math.Abs(baseValue);
                if (absBase > 1)
                {
                    return JsNumber.DoublePositiveInfinity;
                }
                if (absBase == 1)
                {
                    return JsNumber.DoubleNaN;
                }

                return JsNumber.PositiveZero;
            }

            if (exponentNumber.IsNegativeInfinity())
            {
                var absBase = Math.Abs(baseValue);
                if (absBase > 1)
                {
                    return JsNumber.PositiveZero;
                }
                if (absBase == 1)
                {
                    return JsNumber.DoubleNaN;
                }

                return JsNumber.DoublePositiveInfinity;
            }

            if (baseValue < 0 && !TypeConverter.IsIntegralNumber(exponentValue))
            {
                return JsNumber.DoubleNaN;
            }

            result = JsNumber.Create(Math.Pow(baseNumber._value, exponentValue));
        }
        else
        {
            AssertValidBigIntArithmeticOperands(left, right);

            var exponent = right.AsBigInt();
            if (exponent < 0)
            {
                Throw.RangeError(context.Engine.Realm, "Exponent must be positive");
            }

            if (exponent > int.MaxValue)
            {
                Throw.RangeError(context.Engine.Realm, "Maximum BigInt size exceeded");
            }

            var intExponent = (int) exponent;
            var baseValue = left.AsBigInt();
            ValidateBigIntPowSize(context.Engine.Realm, baseValue, intExponent);
            result = JsBigInt.Create(BigInteger.Pow(baseValue, intExponent));
        }

        return result;
    }

    private sealed class InBinaryExpression : JintBinaryExpression
    {
        public InBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
        {
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            if (!TryEvaluateOperands(context, out var left, out var right))
            {
                return JsValue.Undefined;
            }

            var oi = right as ObjectInstance;
            if (oi is null)
            {
                Throw.TypeError(context.Engine.Realm, $"Cannot use 'in' operator to search for '{left}' in {right}");
            }

            if (left.IsPrivateName())
            {
                var privateEnv = context.Engine.ExecutionContext.PrivateEnvironment!;
                var privateName = privateEnv.ResolvePrivateIdentifier(((PrivateName) left).ToString());
                return privateName is not null && oi.PrivateElementFind(privateName) is not null ? JsBoolean.True : JsBoolean.False;
            }

            return oi.HasProperty(left) ? JsBoolean.True : JsBoolean.False;
        }
    }

    private sealed class ModuloBinaryExpression : JintBinaryExpression
    {
        public ModuloBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
        {
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            if (!TryEvaluateOperands(context, out var left, out var right))
            {
                return JsValue.Undefined;
            }

            if (context.OperatorOverloadingAllowed
                && TryOperatorOverloading(context, left, right, "op_Modulus", out var opResult))
            {
                return JsValue.FromObject(context.Engine, opResult);
            }

            return Remainder(context, left, right);
        }
    }

    private sealed class BitwiseBinaryExpression : JintBinaryExpression
    {
        private string? OperatorClrName
        {
            get
            {
                return _operator switch
                {
                    Operator.BitwiseAnd => "op_BitwiseAnd",
                    Operator.BitwiseOr => "op_BitwiseOr",
                    Operator.BitwiseXor => "op_ExclusiveOr",
                    Operator.LeftShift => "op_LeftShift",
                    Operator.RightShift => "op_RightShift",
                    Operator.UnsignedRightShift => "op_UnsignedRightShift",
                    _ => null
                };
            }
        }

        private readonly Operator _operator;

        public BitwiseBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
        {
            _operator = expression.Operator;
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            if (!TryEvaluateOperands(context, out var lval, out var rval))
            {
                return JsValue.Undefined;
            }

            if (context.OperatorOverloadingAllowed
                && TryOperatorOverloading(context, lval, rval, OperatorClrName, out var opResult))
            {
                return JsValue.FromObject(context.Engine, opResult);
            }

            var lnum = TypeConverter.ToNumeric(lval);
            var rnum = TypeConverter.ToNumeric(rval);

            return EvaluateBitwiseOperation(_operator, lnum, rnum, _left._expression);
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-numberbitwiseop (+ BigInt variants and shift operators).
    /// Operands must already be numeric (ToNumeric applied). Shared by the bitwise/shift
    /// binary operators and their compound assignment forms.
    /// </summary>
    internal static JsValue EvaluateBitwiseOperation(Operator op, JsValue left, JsValue right, Node? location)
    {
        if (left.Type != right.Type)
        {
            Throw.TypeErrorNoEngine("Cannot mix BigInt and other types, use explicit conversions", location);
        }

        if (AreIntegerOperands(left, right))
        {
            int leftValue = left.AsInteger();
            int rightValue = right.AsInteger();

            JsValue? result = null;
            switch (op)
            {
                case Operator.BitwiseAnd:
                    result = JsNumber.Create(leftValue & rightValue);
                    break;
                case Operator.BitwiseOr:
                    result = JsNumber.Create(leftValue | rightValue);
                    break;
                case Operator.BitwiseXor:
                    result = JsNumber.Create(leftValue ^ rightValue);
                    break;
                case Operator.LeftShift:
                    result = JsNumber.Create(leftValue << (int) ((uint) rightValue & 0x1F));
                    break;
                case Operator.RightShift:
                    result = JsNumber.Create(leftValue >> (int) ((uint) rightValue & 0x1F));
                    break;
                case Operator.UnsignedRightShift:
                    result = JsNumber.Create((uint) leftValue >> (int) ((uint) rightValue & 0x1F));
                    break;
                default:
                    Throw.ArgumentOutOfRangeException(nameof(op), "unknown shift operator");
                    break;
            }

            return result!;
        }

        switch (op)
        {
            case Operator.BitwiseAnd:
                {
                    if (!left.IsBigInt())
                    {
                        return JsNumber.Create(TypeConverter.ToInt32(left) & TypeConverter.ToInt32(right));
                    }

                    return JsBigInt.Create(TypeConverter.ToBigInt(left) & TypeConverter.ToBigInt(right));
                }

            case Operator.BitwiseOr:
                {
                    if (!left.IsBigInt())
                    {
                        return JsNumber.Create(TypeConverter.ToInt32(left) | TypeConverter.ToInt32(right));
                    }
                    return JsBigInt.Create(TypeConverter.ToBigInt(left) | TypeConverter.ToBigInt(right));
                }

            case Operator.BitwiseXor:
                {
                    if (!left.IsBigInt())
                    {
                        return JsNumber.Create(TypeConverter.ToInt32(left) ^ TypeConverter.ToInt32(right));
                    }
                    return JsBigInt.Create(TypeConverter.ToBigInt(left) ^ TypeConverter.ToBigInt(right));
                }

            case Operator.LeftShift:
                {
                    if (!left.IsBigInt())
                    {
                        return JsNumber.Create(TypeConverter.ToInt32(left) << (int) (TypeConverter.ToUint32(right) & 0x1F));
                    }
                    return JsBigInt.Create(TypeConverter.ToBigInt(left) << (int) TypeConverter.ToBigInt(right));
                }

            case Operator.RightShift:
                {
                    if (!left.IsBigInt())
                    {
                        return JsNumber.Create(TypeConverter.ToInt32(left) >> (int) (TypeConverter.ToUint32(right) & 0x1F));
                    }
                    return JsBigInt.Create(TypeConverter.ToBigInt(left) >> (int) TypeConverter.ToBigInt(right));
                }

            case Operator.UnsignedRightShift:
                {
                    if (!left.IsBigInt())
                    {
                        return JsNumber.Create((uint) TypeConverter.ToInt32(left) >> (int) (TypeConverter.ToUint32(right) & 0x1F));
                    }
                    Throw.TypeErrorNoEngine("BigInts have no unsigned right shift, use >> instead", location);
                    return null;
                }

            default:
                {
                    Throw.ArgumentOutOfRangeException(nameof(op), "unknown shift operator");
                    return null;
                }
        }
    }
}
