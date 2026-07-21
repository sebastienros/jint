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
        private JintIdentifierExpression? _rightIdentifier;   // null => use _constant; the member base when _rightIsLengthBound
        private bool _rightIsLengthBound;                     // `identifier < base.length` bound
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
                case JintMemberExpression member when member.TryGetIdentifierLengthShape(out var lengthBase):
                    _identifier = identifier;
                    _rightIdentifier = lengthBase;
                    _rightIsLengthBound = true;
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
                || !environment.TryGetNumberSlotForRead(slotIndex, out left))
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

            if (_rightIsLengthBound)
            {
                return TryReadLengthBound(rightIdentifier, ref _rightSlotCache, engine, env, out right);
            }

            if (_rightSlotCache.TryResolve(engine, env, rightIdentifier.Identifier, out var rightEnvironment, out var rightSlotIndex)
                && rightEnvironment.TryGetNumberSlotForRead(rightSlotIndex, out right))
            {
                return true;
            }

            return rightIdentifier._cachedGlobalEnv is not null && TryReadGlobalNumber(rightIdentifier, engine, env, out right);
        }

        /// <summary>
        /// The `identifier &lt; base.length` bound: resolves the base purely via its slot and reads
        /// the length without materializing a JsNumber. Array length is always an own data property
        /// (the length machinery forbids accessors) and JsString.Length is the virtual that custom
        /// string types override, so neither read can run user code or materialize a lazy string.
        /// Everything else — typed arrays, proxies, arguments, plain objects — declines.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryReadLengthBound(JintIdentifierExpression baseIdentifier, ref SlotLocationCache cache, Engine engine, Environments.Environment env, out double value)
        {
            value = 0;
            if (!cache.TryResolve(engine, env, baseIdentifier.Identifier, out var baseEnv, out var slot)
                || !baseEnv.TryGetSlotValueForRead(slot, out var baseValue))
            {
                return false;
            }

            if (baseValue is JsArray array)
            {
                value = array.GetLength();
                return true;
            }

            if (baseValue is JsString jsString)
            {
                value = jsString.Length;
                return true;
            }

            return false;
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
    /// Unboxed operand lane for bitwise/shift operators over `identifier OP identifier` and
    /// `identifier OP numericConstant` shapes (`x ^ y`, `z &amp; 3` — the stopwatch/masking loop
    /// shapes). Both operands are read as raw doubles through the slot/global caches (pure reads,
    /// so a decline re-evaluates generically with no observable double effect) and must be
    /// int32-representable — exactly the values for which ToInt32(ToNumeric(v)) equals the plain
    /// cast, so the integer op matches the generic path bit for bit. Fractional, NaN, infinite,
    /// BigInt and non-number operands all decline to the generic path (which owns the valueOf
    /// coercion, BigInt arms and error cases). The result is boxed once by the consumer.
    /// </summary>
    private protected struct BitwiseIdentifierLane
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
        public bool TryGetInt32Operands(EvaluationContext context, out int left, out int right)
        {
            left = 0;
            right = 0;

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

            double leftValue;
            var env = engine.ExecutionContext.LexicalEnvironment;
            if (!_slotCache.TryResolve(engine, env, identifier.Identifier, out var environment, out var slotIndex)
                || !environment.TryGetNumberSlotForRead(slotIndex, out leftValue))
            {
                if (identifier._cachedGlobalEnv is null || !TryReadGlobalNumber(identifier, engine, env, out leftValue))
                {
                    return false;
                }
            }

            double rightValue;
            var rightIdentifier = _rightIdentifier;
            if (rightIdentifier is null)
            {
                rightValue = _constant;
            }
            else if (!(_rightSlotCache.TryResolve(engine, env, rightIdentifier.Identifier, out var rightEnvironment, out var rightSlotIndex)
                       && rightEnvironment.TryGetNumberSlotForRead(rightSlotIndex, out rightValue)))
            {
                if (rightIdentifier._cachedGlobalEnv is null || !TryReadGlobalNumber(rightIdentifier, engine, env, out rightValue))
                {
                    return false;
                }
            }

            // int32-representable check: NaN/±Infinity/fractional values fail the round-trip
            // equality and decline; -0 passes and converts to +0 exactly like ToInt32
            left = (int) leftValue;
            right = (int) rightValue;
            return left == leftValue && right == rightValue;
        }
    }

    /// <summary>
    /// Unboxed operand lane for the arithmetic operators (+ - * /) over identifier and
    /// numeric-constant leaf shapes (`a * b`, `x - 1`, `2 * x`). Both operands are read as raw
    /// doubles through the slot/global caches — pure reads, so a decline (non-number binding,
    /// TDZ, slot miss) falls into the generic path which replays left-then-right evaluation with
    /// no observable double effect. For two runtime-proven Numbers the spec operator degenerates
    /// to the Number:: op, which IEEE double arithmetic implements exactly (including the
    /// sign-of-zero rules), and JsNumber.Create normalizes integral results to the same
    /// Integer-typed instances the boxed arms produce, so downstream integer fast paths stay
    /// armed. Embedded directly as a struct in Minus/Times/Divide (almost always numeric —
    /// same precedent as the comparison classes' embedded lane); Plus wraps it in
    /// <see cref="BoxedNumericOperandLane"/> because string-concat Plus nodes are numerous and
    /// should pay one reference field, not an embedded slot-cache pair, for a lane they never
    /// arm. The embedded form matters on hit-heavy loops: a separate lane object costs an extra
    /// dereference (cache line) per evaluation.
    /// </summary>
    private protected struct NumericOperandLane
    {
        private JintIdentifierExpression? _leftIdentifier;   // null => _leftConstant
        private JintIdentifierExpression? _rightIdentifier;  // null => _rightConstant
        private double _leftConstant;
        private double _rightConstant;
        private bool _armed;
        private SlotLocationCache _leftSlotCache;
        private SlotLocationCache _rightSlotCache;

        public readonly bool IsArmed => _armed;

        public void Initialize(JintExpression left, JintExpression right)
        {
            var leftIdentifier = left as JintIdentifierExpression;
            var rightIdentifier = right as JintIdentifierExpression;

            // constant-op-constant is folded before these nodes are built; requiring at least one
            // identifier keeps the lane off shapes other machinery owns
            if (leftIdentifier is null && rightIdentifier is null)
            {
                return;
            }

            double leftConstant = 0;
            if (leftIdentifier is null)
            {
                if (left is not JintConstantExpression { Value: JsNumber leftNumber })
                {
                    return;
                }
                leftConstant = leftNumber._value;
            }

            double rightConstant = 0;
            if (rightIdentifier is null)
            {
                if (right is not JintConstantExpression { Value: JsNumber rightNumber })
                {
                    return;
                }
                rightConstant = rightNumber._value;
            }

            _leftIdentifier = leftIdentifier;
            _leftConstant = leftConstant;
            _rightIdentifier = rightIdentifier;
            _rightConstant = rightConstant;
            _armed = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetOperands(EvaluationContext context, out double left, out double right)
        {
            left = _leftConstant;
            right = _rightConstant;

            if (!_armed || context.OperatorOverloadingAllowed)
            {
                return false;
            }

            var engine = context.Engine;
            if (engine.ExecutionContext.Suspendable is not null)
            {
                // generator/async resume replays a saved left operand through the suspend
                // protocol; live slot re-reads could observe a different value
                return false;
            }

            var env = engine.ExecutionContext.LexicalEnvironment;

            var leftIdentifier = _leftIdentifier;
            if (leftIdentifier is not null
                && (!_leftSlotCache.TryResolve(engine, env, leftIdentifier.Identifier, out var leftEnvironment, out var leftSlotIndex)
                    || !leftEnvironment.TryGetNumberSlotForRead(leftSlotIndex, out left)))
            {
                if (leftIdentifier._cachedGlobalEnv is null || !TryReadGlobalNumber(leftIdentifier, engine, env, out left))
                {
                    return false;
                }
            }

            var rightIdentifier = _rightIdentifier;
            if (rightIdentifier is not null
                && (!_rightSlotCache.TryResolve(engine, env, rightIdentifier.Identifier, out var rightEnvironment, out var rightSlotIndex)
                    || !rightEnvironment.TryGetNumberSlotForRead(rightSlotIndex, out right)))
            {
                if (rightIdentifier._cachedGlobalEnv is null || !TryReadGlobalNumber(rightIdentifier, engine, env, out right))
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Heap-allocated <see cref="NumericOperandLane"/> for PlusBinaryExpression: allocated only
    /// when the operand shape matches at build time, so the many string-concat Plus nodes carry
    /// a single null reference field instead of the embedded lane struct.
    /// </summary>
    private protected sealed class BoxedNumericOperandLane
    {
        public NumericOperandLane Lane;

        public static BoxedNumericOperandLane? TryBuild(JintExpression left, JintExpression right)
        {
            var lane = new NumericOperandLane();
            lane.Initialize(left, right);
            return lane.IsArmed ? new BoxedNumericOperandLane { Lane = lane } : null;
        }
    }

    /// <summary>
    /// Whole-tree lane for sum-of-products arithmetic over pure-readable numeric leaves —
    /// `M1[i][0]*M2[0][j] + M1[i][1]*M2[1][j] + …`, the dromaeo-3d-cube MMulti/VMulti kernel shape,
    /// which otherwise boxes a transient JsNumber per binary node. The whole tree is computed on
    /// raw doubles (the spec arithmetic — integer-valued leaves produce identical results) and
    /// boxed once by the consumer.
    ///
    /// Armed only for trees of two or more ± terms that are ALL products — a shape integer-heavy
    /// workloads don't produce, so their dedicated per-op integer paths never see this probe.
    /// Leaves are numeric constants, slot/global identifiers and dense computed reads with
    /// identifier bases: every runtime read is pure, so a decline (any leaf missing the dense
    /// fast path or not holding a Number) falls back to generic tree evaluation with no
    /// observable double effects.
    /// </summary>
    internal sealed class SumOfProductsLane
    {
        private struct Leaf
        {
            public byte Kind;                                    // 0 constant, 1 identifier, 2 a[i], 3 a[i][j]
            public double Constant;
            public JintIdentifierExpression? Identifier;          // identifier leaf, or member base
            public JintIdentifierExpression? Index1Identifier;    // first index (null => Constant1)
            public JintIdentifierExpression? Index2Identifier;    // second index (null => Constant2)
            public uint ConstantIndex1;
            public uint ConstantIndex2;
            public SlotLocationCache Cache;
            public SlotLocationCache Index1Cache;
            public SlotLocationCache Index2Cache;
        }

        private readonly Leaf[] _leaves;      // term k = leaves[2k] * leaves[2k+1]
        private readonly bool[] _negated;     // sign per term (a right operand of Minus flips)

        private SumOfProductsLane(Leaf[] leaves, bool[] negated)
        {
            _leaves = leaves;
            _negated = negated;
        }

        internal static SumOfProductsLane? TryBuild(JintExpression expression)
        {
            var leaves = new List<Leaf>();
            var negated = new List<bool>();
            if (!CollectTerms(expression, leaves, negated) || negated.Count < 2)
            {
                return null;
            }

            return new SumOfProductsLane(leaves.ToArray(), negated.ToArray());
        }

        private static bool CollectTerms(JintExpression expression, List<Leaf> leaves, List<bool> negated)
        {
            // Only left-deep chains arm (the natural parse of `p1 + p2 - p3 …`), so the linear
            // left-to-right fold reproduces the tree's association — and therefore its floating
            // point rounding — exactly. A parenthesized right-nested sum stays on the generic path.
            switch (expression)
            {
                case PlusBinaryExpression plus:
                    return CollectTerms(plus._left, leaves, negated)
                           && CollectProduct(plus._right, negate: false, leaves, negated);
                case MinusBinaryExpression minus:
                    return CollectTerms(minus._left, leaves, negated)
                           && CollectProduct(minus._right, negate: true, leaves, negated);
                case TimesBinaryExpression times:
                    return CollectProduct(times, negate: false, leaves, negated);
                default:
                    return false;
            }
        }

        private static bool CollectProduct(JintExpression expression, bool negate, List<Leaf> leaves, List<bool> negated)
        {
            if (expression is not TimesBinaryExpression times
                || !TryClassifyLeaf(times._left, out var left)
                || !TryClassifyLeaf(times._right, out var right))
            {
                return false;
            }

            leaves.Add(left);
            leaves.Add(right);
            negated.Add(negate);
            return negated.Count <= 8;
        }

        private static bool TryClassifyLeaf(JintExpression expression, out Leaf leaf)
        {
            leaf = default;
            switch (expression)
            {
                case JintConstantExpression { Value: JsNumber number }:
                    leaf.Kind = 0;
                    leaf.Constant = number._value;
                    return true;

                case JintIdentifierExpression identifier:
                    leaf.Kind = 1;
                    leaf.Identifier = identifier;
                    return true;

                case JintMemberExpression member when member.TryGetComputedIndexShape(out var objectExpression, out var indexIdentifier, out var constantIndex):
                    if (objectExpression is JintIdentifierExpression identifierBase)
                    {
                        // a[i] / a[0]
                        leaf.Kind = 2;
                        leaf.Identifier = identifierBase;
                        leaf.Index1Identifier = indexIdentifier;
                        leaf.ConstantIndex1 = constantIndex;
                        return true;
                    }

                    if (objectExpression is JintMemberExpression innerMember
                        && innerMember.TryGetComputedIndexShape(out var innerObject, out var innerIndexIdentifier, out var innerConstantIndex)
                        && innerObject is JintIdentifierExpression chainBase)
                    {
                        // m[i][j] / m[i][0] — the matrix-kernel leaf
                        leaf.Kind = 3;
                        leaf.Identifier = chainBase;
                        leaf.Index1Identifier = innerIndexIdentifier;
                        leaf.ConstantIndex1 = innerConstantIndex;
                        leaf.Index2Identifier = indexIdentifier;
                        leaf.ConstantIndex2 = constantIndex;
                        return true;
                    }

                    return false;

                default:
                    return false;
            }
        }

        public bool TryEvaluate(EvaluationContext context, out double result)
        {
            result = 0;

            var engine = context.Engine;
            if (context.OperatorOverloadingAllowed || engine.ExecutionContext.Suspendable is not null)
            {
                return false;
            }

            var env = engine.ExecutionContext.LexicalEnvironment;
            var leaves = _leaves;
            var negated = _negated;

            // the first term seeds the accumulator (never negated in a left-deep chain) — seeding
            // with +0 would inject a phantom term and lose a -0 sum's sign
            if (!TryReadLeaf(ref leaves[0], engine, env, out var seedLeft)
                || !TryReadLeaf(ref leaves[1], engine, env, out var seedRight))
            {
                return false;
            }

            result = seedLeft * seedRight;
            for (var term = 1; term < negated.Length; term++)
            {
                if (!TryReadLeaf(ref leaves[2 * term], engine, env, out var left)
                    || !TryReadLeaf(ref leaves[2 * term + 1], engine, env, out var right))
                {
                    result = 0;
                    return false;
                }

                var product = left * right;
                result = negated[term] ? result - product : result + product;
            }

            return true;
        }

        private static bool TryReadLeaf(ref Leaf leaf, Engine engine, Environments.Environment env, out double value)
        {
            switch (leaf.Kind)
            {
                case 0:
                    value = leaf.Constant;
                    return true;

                case 1:
                    if (leaf.Cache.TryResolve(engine, env, leaf.Identifier!.Identifier, out var identifierEnv, out var slotIndex)
                        && identifierEnv.TryGetNumberSlotForRead(slotIndex, out value))
                    {
                        return true;
                    }

                    value = 0;
                    return leaf.Identifier._cachedGlobalEnv is not null && TryReadGlobalNumber(leaf.Identifier, engine, env, out value);

                default:
                    value = 0;
                    if (!leaf.Cache.TryResolve(engine, env, leaf.Identifier!.Identifier, out var baseEnv, out var baseSlot)
                        || !baseEnv.TryGetSlotValueForRead(baseSlot, out var baseValue)
                        || baseValue is not JsArray array
                        || !array.CanUseFastAccess)
                    {
                        return false;
                    }

                    if (!TryReadIndex(leaf.Index1Identifier, ref leaf.Index1Cache, leaf.ConstantIndex1, engine, env, out var index)
                        || !array.TryGetValueFast(index, out var element))
                    {
                        return false;
                    }

                    if (leaf.Kind == 3)
                    {
                        if (element is not JsArray innerArray
                            || !innerArray.CanUseFastAccess
                            || !TryReadIndex(leaf.Index2Identifier, ref leaf.Index2Cache, leaf.ConstantIndex2, engine, env, out var innerIndex)
                            || !innerArray.TryGetValueFast(innerIndex, out element))
                        {
                            return false;
                        }
                    }

                    if (element is JsNumber number)
                    {
                        value = number._value;
                        return true;
                    }

                    return false;
            }
        }

        private static bool TryReadIndex(JintIdentifierExpression? indexIdentifier, ref SlotLocationCache cache, uint constantIndex, Engine engine, Environments.Environment env, out uint index)
        {
            if (indexIdentifier is null)
            {
                index = constantIndex;
                return true;
            }

            index = 0;
            if (!cache.TryResolve(engine, env, indexIdentifier.Identifier, out var indexEnv, out var indexSlot)
                || !indexEnv.TryGetNumberSlotForRead(indexSlot, out var indexNumber))
            {
                return false;
            }

            index = (uint) indexNumber;
            return index == indexNumber;
        }
    }

    /// <summary>
    /// Fused lane for the `identifier % numericConstant == numericConstant` test — the
    /// stopwatch.js if-chain shape. Reads the identifier through the same slot/global arms as
    /// <see cref="NumericConstantComparisonLane"/> and computes the whole test on raw doubles
    /// via <see cref="JintExpression.RemainderUnboxed"/>, which implements Number::remainder
    /// exactly (int32 lane for integral operands, IEEE 754 fmod otherwise), so the fused
    /// result is spec-exact for any proven-Number operand: NaN dividends/divisors (and % 0)
    /// compare false, v % ±Infinity == v, and ±0 results compare equal under IEEE ==.
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
                || !environment.TryGetNumberSlotForRead(slotIndex, out var value))
            {
                if (identifier._cachedGlobalEnv is null || !TryReadGlobalNumber(identifier, engine, env, out value))
                {
                    return false;
                }
            }

            equal = RemainderUnboxed(value, _divisor) == _expected;
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
                }
                catch (Exception e)
                {
                    Throw.MeaningfulException(context.Engine, new TargetInvocationException(e.InnerException));
                    result = null;
                    return false;
                }

                // outside the catch-all above so a constraint exception is not laundered into
                // a TargetInvocationException
                context.Engine.CheckAmortizedConstraintsAtHostBoundary();
                return true;
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
                result = TryBuildFusedStrictEquality(expression, invert: false) ?? new StrictlyEqualBinaryExpression(expression);
                break;
            case Operator.StrictInequality:
                result = TryBuildFusedStrictEquality(expression, invert: true) ?? new StrictlyNotEqualBinaryExpression(expression);
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
                result = TryBuildFusedLooseEquality(expression, invert: false);
                result ??= new EqualBinaryExpression(expression);
                break;
            case Operator.Inequality:
                result = TryBuildFusedLooseEquality(expression, invert: true);
                result ??= new EqualBinaryExpression(expression, invert: true);
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
    /// Build-time fusion for the guard idioms `x === undefined`, `x === null` and
    /// `typeof x === "literal"` (plus the !== forms): the constant side is known statically, so
    /// the fused node evaluates only the interesting operand and answers with one type test or
    /// one reference compare — no evaluation of the constant side and no generic strict-equality
    /// dispatch. Returns null when the expression is not one of these shapes (including when both
    /// sides are constants, which keeps the existing literal-vs-literal constant fold).
    /// </summary>
    private static JintBinaryExpression? TryBuildFusedStrictEquality(NonLogicalBinaryExpression expression, bool invert)
    {
        // typeof x === "literal" (either orientation)
        if (expression.Left is NonUpdateUnaryExpression { Operator: Operator.TypeOf } && expression.Right is Literal { Value: string rightString })
        {
            return new TypeofStrictEqualityExpression(expression, typeofIsLeft: true, MapTypeofResultSingleton(rightString), invert);
        }

        if (expression.Right is NonUpdateUnaryExpression { Operator: Operator.TypeOf } && expression.Left is Literal { Value: string leftString })
        {
            return new TypeofStrictEqualityExpression(expression, typeofIsLeft: false, MapTypeofResultSingleton(leftString), invert);
        }

        // x === undefined / x === null (either orientation)
        var leftSingleton = GetKnownSingletonType(expression.Left);
        var rightSingleton = GetKnownSingletonType(expression.Right);
        if (rightSingleton is not null && leftSingleton is null)
        {
            return new SingletonStrictEqualityExpression(expression, operandIsLeft: true, rightSingleton.Value, invert);
        }

        if (leftSingleton is not null && rightSingleton is null)
        {
            return new SingletonStrictEqualityExpression(expression, operandIsLeft: false, leftSingleton.Value, invert);
        }

        return null;
    }

    /// <summary>
    /// Build-time fusion for the guard idioms `x == null` and `x == undefined` (plus the != forms):
    /// the constant side is known statically, so the fused node evaluates only the interesting
    /// operand and answers with one internal-type test — no evaluation of the constant side and no
    /// generic loose-equality dispatch. Loose equality against null/undefined is true exactly when
    /// the value is null, undefined, or has the [[IsHTMLDDA]] internal slot (Annex B), so the single
    /// mask covers all three. Returns null when the expression is not one of these shapes, including
    /// when BOTH sides are singletons (e.g. `null == undefined`) — that is left to the generic path,
    /// mirroring <see cref="TryBuildFusedStrictEquality"/>, so cross-singleton semantics are preserved.
    /// </summary>
    private static SingletonLooseEqualityExpression? TryBuildFusedLooseEquality(NonLogicalBinaryExpression expression, bool invert)
    {
        var leftSingleton = GetKnownSingletonType(expression.Left);
        var rightSingleton = GetKnownSingletonType(expression.Right);

        if (rightSingleton is not null && leftSingleton is null)
        {
            return new SingletonLooseEqualityExpression(expression, operandIsLeft: true, invert);
        }

        if (leftSingleton is not null && rightSingleton is null)
        {
            return new SingletonLooseEqualityExpression(expression, operandIsLeft: false, invert);
        }

        return null;
    }

    // The identifier `undefined` matches the same compile-time folding the identifier itself
    // evaluates through (Environment.BindingName.CalculatedValue), so the fused semantics are
    // exactly the unfused ones. `null` is a literal; ConvertToJsValue screens out the literals
    // that don't convert statically (regex).
    private static InternalTypes? GetKnownSingletonType(Expression expression) => expression switch
    {
        Identifier { Name: "undefined" } => InternalTypes.Undefined,
        Literal literal when JintLiteralExpression.ConvertToJsValue(literal) is JsNull => InternalTypes.Null,
        _ => null,
    };

    // typeof only ever returns these interned singletons (JintTypeOfExpression.GetTypeOfString and
    // the unresolvable-reference shortcut), so a mapped literal compares by reference and an
    // unmapped literal can never match — the operand still evaluates for its side effects.
    private static JsString? MapTypeofResultSingleton(string literal) => literal switch
    {
        "undefined" => JsString.UndefinedString,
        "object" => JsString.ObjectString,
        "boolean" => JsString.BooleanString,
        "number" => JsString.NumberString,
        "bigint" => JsString.BigIntString,
        "string" => JsString.StringString,
        "symbol" => JsString.SymbolString,
        "function" => JsString.FunctionString,
        _ => null,
    };

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

    /// <summary>
    /// Fused `x === undefined` / `x === null` (and the !== forms): only the interesting operand
    /// evaluates, and the answer is a single internal-type test. Strict equality against these
    /// singletons is exactly a type-identity check (IsHTMLDDA is only LOOSELY equal to them, so
    /// no carve-out is needed on the strict path).
    /// </summary>
    private sealed class SingletonStrictEqualityExpression : JintBinaryExpression
    {
        private readonly JintExpression _operand;
        private readonly InternalTypes _expectedType;
        private readonly bool _invert;

        public SingletonStrictEqualityExpression(NonLogicalBinaryExpression expression, bool operandIsLeft, InternalTypes expectedType, bool invert) : base(expression)
        {
            _operand = operandIsLeft ? _left : _right;
            _expectedType = expectedType;
            _invert = invert;
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            var value = _operand.GetValue(context);
            if (context.IsSuspended())
            {
                return JsValue.Undefined;
            }

            return (value._type == _expectedType) != _invert ? JsBoolean.True : JsBoolean.False;
        }

        public override bool GetBooleanValue(EvaluationContext context)
        {
            var value = _operand.GetValue(context);
            if (context.IsSuspended())
            {
                return false;
            }

            return (value._type == _expectedType) != _invert;
        }
    }

    /// <summary>
    /// Fused `x == null` / `x == undefined` (and the != forms): only the interesting operand
    /// evaluates, and the answer is a single internal-type test. Loose equality against null or
    /// undefined is true exactly when the value is null, undefined, or carries the [[IsHTMLDDA]]
    /// internal slot — Annex B makes `document.all == null` true — which is precisely the union of
    /// those three type bits (see JsNull.IsLooselyEqual / JsUndefined.IsLooselyEqual).
    /// The constant side is a `null` literal or the folded `undefined` identifier, both of which
    /// have <c>ToObject()</c> == null, so operator overloading can never apply here; the fusion is
    /// declined at build time when BOTH sides are singletons, preserving `null == undefined`.
    /// </summary>
    private sealed class SingletonLooseEqualityExpression : JintBinaryExpression
    {
        private const InternalTypes NullOrUndefinedOrHtmlDda = InternalTypes.Null | InternalTypes.Undefined | InternalTypes.IsHTMLDDA;

        private readonly JintExpression _operand;
        private readonly bool _invert;

        public SingletonLooseEqualityExpression(NonLogicalBinaryExpression expression, bool operandIsLeft, bool invert) : base(expression)
        {
            _operand = operandIsLeft ? _left : _right;
            _invert = invert;
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            var value = _operand.GetValue(context);
            if (context.IsSuspended())
            {
                return JsValue.Undefined;
            }

            var nullish = (value._type & NullOrUndefinedOrHtmlDda) != InternalTypes.Empty;
            return nullish != _invert ? JsBoolean.True : JsBoolean.False;
        }

        public override bool GetBooleanValue(EvaluationContext context)
        {
            var value = _operand.GetValue(context);
            if (context.IsSuspended())
            {
                return false;
            }

            var nullish = (value._type & NullOrUndefinedOrHtmlDda) != InternalTypes.Empty;
            return nullish != _invert;
        }
    }

    /// <summary>
    /// Fused `typeof x === "literal"` (and !==): the typeof side evaluates through the normal
    /// JintTypeOfExpression — keeping the unresolvable-identifier shortcut and host-object
    /// classification untouched — and since typeof only ever produces the interned JsString
    /// singletons, the comparison is a reference test against the singleton the literal mapped
    /// to at build time (null for a literal typeof can never produce).
    /// </summary>
    private sealed class TypeofStrictEqualityExpression : JintBinaryExpression
    {
        private readonly JintExpression _typeofExpression;
        private readonly JsString? _expectedSingleton;
        private readonly bool _invert;

        public TypeofStrictEqualityExpression(NonLogicalBinaryExpression expression, bool typeofIsLeft, JsString? expectedSingleton, bool invert) : base(expression)
        {
            _typeofExpression = typeofIsLeft ? _left : _right;
            _expectedSingleton = expectedSingleton;
            _invert = invert;
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            var actual = _typeofExpression.GetValue(context);
            if (context.IsSuspended())
            {
                return JsValue.Undefined;
            }

            return ReferenceEquals(actual, _expectedSingleton) != _invert ? JsBoolean.True : JsBoolean.False;
        }

        public override bool GetBooleanValue(EvaluationContext context)
        {
            var actual = _typeofExpression.GetValue(context);
            if (context.IsSuspended())
            {
                return false;
            }

            return ReferenceEquals(actual, _expectedSingleton) != _invert;
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
            // Compare only ever returns JsBoolean.True/False or undefined
            return value._type != InternalTypes.Undefined && Unsafe.As<JsBoolean>(value)._value;
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
            // Compare only ever returns JsBoolean.True/False or undefined
            return value._type != InternalTypes.Undefined && Unsafe.As<JsBoolean>(value)._value;
        }
    }

    private sealed class PlusBinaryExpression : JintBinaryExpression
    {
        private readonly BoxedNumericOperandLane? _numericLane;

        public PlusBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
        {
            _numericLane = BoxedNumericOperandLane.TryBuild(_left, _right);
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            if (_numericLane is not null && _numericLane.Lane.TryGetOperands(context, out var unboxedLeft, out var unboxedRight))
            {
                return JsNumber.Create(unboxedLeft + unboxedRight);
            }

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
                return JsNumber.Create(Unsafe.As<JsNumber>(left)._value + Unsafe.As<JsNumber>(right)._value);
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
        private NumericOperandLane _numericLane;

        public MinusBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
        {
            _numericLane.Initialize(_left, _right);
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            if (_numericLane.TryGetOperands(context, out var unboxedLeft, out var unboxedRight))
            {
                return JsNumber.Create(unboxedLeft - unboxedRight);
            }

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
                return JsNumber.Create(Unsafe.As<JsNumber>(left)._value - Unsafe.As<JsNumber>(right)._value);
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
        private NumericOperandLane _numericLane;

        public TimesBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
        {
            _numericLane.Initialize(_left, _right);
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            if (_numericLane.TryGetOperands(context, out var unboxedLeft, out var unboxedRight))
            {
                // raw-double multiply is Number::multiply bit for bit, including -0 for zero
                // products of opposite signs
                return JsNumber.Create(unboxedLeft * unboxedRight);
            }

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
                var product = (long) left.AsInteger() * right.AsInteger();
                // Number::multiply gives -0 for zero products of opposite signs (0 * -5), which
                // integer math cannot represent — route zero products through double arithmetic
                result = product != 0
                    ? JsNumber.Create(product)
                    : JsNumber.Create((double) left.AsInteger() * right.AsInteger());
            }
            else if (left._type == InternalTypes.Number && right._type == InternalTypes.Number)
            {
                result = JsNumber.Create(Unsafe.As<JsNumber>(left)._value * Unsafe.As<JsNumber>(right)._value);
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
        private NumericOperandLane _numericLane;

        public DivideBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
        {
            _numericLane.Initialize(_left, _right);
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            if (_numericLane.TryGetOperands(context, out var unboxedLeft, out var unboxedRight))
            {
                return JsNumber.Create(unboxedLeft / unboxedRight);
            }

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
                return JsNumber.Create(Unsafe.As<JsNumber>(left)._value / Unsafe.As<JsNumber>(right)._value);
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
            // Compare only ever returns JsBoolean.True/False or undefined
            return value.IsUndefined() || Unsafe.As<JsBoolean>(value)._value ? JsBoolean.False : JsBoolean.True;
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
            // Compare only ever returns JsBoolean.True/False or undefined
            return !value.IsUndefined() && !Unsafe.As<JsBoolean>(value)._value;
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
        private BitwiseIdentifierLane _lane;

        public BitwiseBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
        {
            _operator = expression.Operator;
            _lane.Initialize(_left, _right);
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            // `x ^ y` / `z & 3` over slot/global numbers: both operands read unboxed, the
            // integer op boxed once. Declines (non-int32 values, non-number bindings, BigInt,
            // suspendable frames) re-evaluate generically — the lane reads are pure.
            if (_lane.TryGetInt32Operands(context, out var leftInt, out var rightInt))
            {
                return EvaluateIntegerBitwise(_operator, leftInt, rightInt);
            }

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
    /// The integer arm of <see cref="EvaluateBitwiseOperation"/>: operands already converted to
    /// int32 (via integer-typed JsNumbers or the lane's int32-representability check, both of
    /// which agree with ToInt32 exactly).
    /// </summary>
    internal static JsValue EvaluateIntegerBitwise(Operator op, int leftValue, int rightValue)
    {
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
            return EvaluateIntegerBitwise(op, left.AsInteger(), right.AsInteger());
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
