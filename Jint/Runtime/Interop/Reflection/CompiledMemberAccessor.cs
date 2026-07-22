using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Jint.Native;

#if NET8_0_OR_GREATER
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Expression = System.Linq.Expressions.Expression;
using ConditionalExpression = System.Linq.Expressions.ConditionalExpression;
#endif

// The delegates are compiled with System.Linq.Expressions; every member they reference is public
// (JsValue implicit operators, JsValueExtensions accessors, JsValue.Null, Math.Floor) so the
// generated dynamic methods need no reflection-visibility relaxation. IL2075/IL3050 cover the
// reflection + dynamic-code use, which is gated behind RuntimeFeature.IsDynamicCodeCompiled.
#pragma warning disable IL2075
#pragma warning disable IL3050

namespace Jint.Runtime.Interop.Reflection;

/// <summary>
/// Builds and caches strongly-typed delegates that read and write CLR properties and fields, so a
/// member access does not go through <see cref="PropertyInfo.GetValue(object, object[])"/> /
/// <see cref="PropertyInfo.SetValue(object, object, object[])"/> reflection on every hit.
/// <para>
/// Two lanes are built per member. The <b>JsValue lane</b> covers the dominant member shapes
/// (<see cref="int"/>, <see cref="long"/>, <see cref="double"/>, <see cref="bool"/>,
/// <see cref="string"/>, <see cref="JsValue"/>) and produces or consumes the
/// <see cref="JsValue"/> directly — no boxing, and no <see cref="JsValue.FromObjectWithType"/>
/// dispatch on reads. The <b>raw lane</b> covers every other member type and merely replaces the
/// reflection call, leaving the surrounding conversion untouched.
/// </para>
/// <para>
/// Like <c>CompiledMethodInvoker</c>, the JsValue write lane only accepts exact-type hits:
/// any value that is not the exact expected JavaScript type (including a fractional number bound to
/// an integer member, or a number outside the target range) makes it return <see langword="false"/>
/// so the caller falls back to the full conversion path, preserving today's behaviour bit-for-bit.
/// </para>
/// <para>
/// The compiled delegates are cached process-wide keyed by the <see cref="MemberInfo"/>, because
/// they are engine-independent: nothing they close over is affine to an <see cref="Engine"/>. This
/// matters because <see cref="ReflectionAccessor"/> instances live in a per-engine cache
/// (<c>Engine._reflectionAccessors</c>), so a per-accessor cache would recompile every member for
/// every engine — the cost this type exists to remove. The trade-off is the same one the other
/// process-wide reflection caches in this assembly make (<see cref="TypeDescriptor"/>,
/// <see cref="TypeReference"/>, <c>JintBinaryExpression._knownOperators</c>): the cached
/// <see cref="MemberInfo"/> keeps its declaring assembly alive for the process lifetime.
/// </para>
/// </summary>
internal static class CompiledMemberAccessor
{
    /// <summary>
    /// Writes an exact-typed <paramref name="value"/> to the member of <paramref name="target"/>.
    /// Returns <see langword="false"/> (declining, writing nothing) when the value is not the exact
    /// expected JavaScript type, so the caller can run the full conversion path instead.
    /// </summary>
    internal delegate bool JsValueSetter(object target, JsValue value);

#if NET8_0_OR_GREATER
    private static readonly ConcurrentDictionary<MemberInfo, Func<object, JsValue>?> _jsValueGetters = new();
    private static readonly ConcurrentDictionary<MemberInfo, Func<object, object?>?> _rawGetters = new();
    private static readonly ConcurrentDictionary<MemberInfo, JsValueSetter?> _jsValueSetters = new();
    private static readonly ConcurrentDictionary<MemberInfo, Action<object, object?>?> _rawSetters = new();

    private static readonly MethodInfo _asNumber = typeof(JsValueExtensions).GetMethod(nameof(JsValueExtensions.AsNumber), [typeof(JsValue)])!;
    private static readonly MethodInfo _asBoolean = typeof(JsValueExtensions).GetMethod(nameof(JsValueExtensions.AsBoolean), [typeof(JsValue)])!;
    private static readonly MethodInfo _objectToString = typeof(object).GetMethod(nameof(ToString), Type.EmptyTypes)!;
    private static readonly MethodInfo _doubleIsNaN = typeof(double).GetMethod(nameof(double.IsNaN), [typeof(double)])!;
    private static readonly MethodInfo _doubleIsInfinity = typeof(double).GetMethod(nameof(double.IsInfinity), [typeof(double)])!;
    private static readonly MethodInfo _mathFloor = typeof(Math).GetMethod(nameof(Math.Floor), [typeof(double)])!;

    // (double) long.MaxValue rounds up to 2^63 which overflows long, the bound must be exclusive -
    // this mirrors InteropHelper.TryConvertNumberFast exactly.
    private const double LongMaxValueExclusiveUpperBound = 9223372036854775808d;
#endif

    /// <summary>
    /// Returns a delegate producing the member value as a <see cref="JsValue"/> without boxing, or
    /// <see langword="null"/> when the member is not eligible for the JsValue lane.
    /// </summary>
    internal static Func<object, JsValue>? GetJsValueGetter(MemberInfo member)
    {
#if NET8_0_OR_GREATER
        return _jsValueGetters.GetOrAdd(member, static m => BuildJsValueGetter(m));
#else
        return null;
#endif
    }

    /// <summary>
    /// Returns a delegate reading the member into a boxed CLR value (replacing the reflection call
    /// only), or <see langword="null"/> when the member is not eligible.
    /// </summary>
    internal static Func<object, object?>? GetRawGetter(MemberInfo member)
    {
#if NET8_0_OR_GREATER
        return _rawGetters.GetOrAdd(member, static m => BuildRawGetter(m));
#else
        return null;
#endif
    }

    /// <summary>
    /// Returns a delegate writing an exact-typed <see cref="JsValue"/> to the member without
    /// boxing, or <see langword="null"/> when the member is not eligible for the JsValue lane.
    /// </summary>
    internal static JsValueSetter? GetJsValueSetter(MemberInfo member)
    {
#if NET8_0_OR_GREATER
        return _jsValueSetters.GetOrAdd(member, static m => BuildJsValueSetter(m));
#else
        return null;
#endif
    }

    /// <summary>
    /// Returns a delegate writing an already-converted CLR value whose runtime type is exactly the
    /// member type (replacing the reflection call only), or <see langword="null"/> when the member
    /// is not eligible.
    /// </summary>
    internal static Action<object, object?>? GetRawSetter(MemberInfo member)
    {
#if NET8_0_OR_GREATER
        return _rawSetters.GetOrAdd(member, static m => BuildRawSetter(m));
#else
        return null;
#endif
    }

#if NET8_0_OR_GREATER
    private static Func<object, JsValue>? BuildJsValueGetter(MemberInfo member)
    {
        if (!TryBuildRead(member, out var target, out var access, out var memberType)
            || !IsSupportedMemberType(memberType))
        {
            return null;
        }

        return Expression.Lambda<Func<object, JsValue>>(BuildJsValueConversion(access, memberType), target).Compile();
    }

    private static Func<object, object?>? BuildRawGetter(MemberInfo member)
    {
        if (!TryBuildRead(member, out var target, out var access, out _))
        {
            return null;
        }

        return Expression.Lambda<Func<object, object?>>(Expression.Convert(access, typeof(object)), target).Compile();
    }

    private static JsValueSetter? BuildJsValueSetter(MemberInfo member)
    {
        if (!TryGetEligibleMember(member, out var declaringType, out var memberType)
            || !IsSupportedWrittenMemberType(memberType))
        {
            return null;
        }

        var targetParameter = Expression.Parameter(typeof(object), "target");
        var valueParameter = Expression.Parameter(typeof(JsValue), "value");
        var returnLabel = Expression.Label(typeof(bool), "return");

        var locals = new List<ParameterExpression>();
        var body = new List<Expression>();

        // emits the type test; a non-exact match jumps to the label with false, writing nothing
        var bound = BuildValueBinding(memberType, valueParameter, returnLabel, locals, body);

        if (!TryBuildWrite(member, declaringType, memberType, targetParameter, bound, out var write))
        {
            return null;
        }

        body.Add(write);
        body.Add(Expression.Return(returnLabel, Expression.Constant(true)));
        body.Add(Expression.Label(returnLabel, Expression.Constant(false)));

        var lambda = Expression.Lambda<JsValueSetter>(
            Expression.Block(typeof(bool), locals, body), targetParameter, valueParameter);

        return lambda.Compile();
    }

    private static Action<object, object?>? BuildRawSetter(MemberInfo member)
    {
        if (!TryGetEligibleMember(member, out var declaringType, out var memberType))
        {
            return null;
        }

        var targetParameter = Expression.Parameter(typeof(object), "target");
        var valueParameter = Expression.Parameter(typeof(object), "value");

        // the caller only takes this lane when the value's runtime type is exactly the member type,
        // so the unbox/cast can never fail (see CompilableMemberAccessor.DoSetValue)
        var typedValue = Expression.Convert(valueParameter, memberType);
        if (!TryBuildWrite(member, declaringType, memberType, targetParameter, typedValue, out var write))
        {
            return null;
        }

        return Expression.Lambda<Action<object, object?>>(write, targetParameter, valueParameter).Compile();
    }

    /// <summary>
    /// Builds the expression reading <paramref name="member"/> off an <see cref="object"/> typed
    /// target parameter, as a value of the member's own type.
    /// </summary>
    private static bool TryBuildRead(
        MemberInfo member,
        [NotNullWhen(true)] out ParameterExpression? target,
        [NotNullWhen(true)] out Expression? access,
        [NotNullWhen(true)] out Type? memberType)
    {
        target = null;
        access = null;
        memberType = null;

        if (!TryGetEligibleMember(member, out var declaringType, out var type))
        {
            return false;
        }

        var targetParameter = Expression.Parameter(typeof(object), "target");
        var typedTarget = Expression.Convert(targetParameter, declaringType);

        if (member is PropertyInfo property)
        {
            var getMethod = property.GetGetMethod();
            if (getMethod is null || getMethod.IsStatic)
            {
                return false;
            }

            // The getter is invoked through an open delegate (built once, embedded as a constant)
            // rather than a direct call: a direct call lets the JIT inline the host getter into the
            // compiled lambda, which erases the getter's frame from the stack trace of an exception
            // it throws. The reflection path never inlines, so bubbling CLR exceptions would stop
            // looking identical. (Same reasoning as CompiledMethodInvoker.)
            if (!TryCreateOpenDelegate(getMethod, [declaringType], type, out var accessorDelegate, out var delegateType))
            {
                return false;
            }

            access = Expression.Invoke(Expression.Constant(accessorDelegate, delegateType), typedTarget);
        }
        else
        {
            access = Expression.Field(typedTarget, (FieldInfo) member);
        }

        target = targetParameter;
        memberType = type;
        return true;
    }

    /// <summary>
    /// Builds the expression writing <paramref name="value"/> (already of the member's type) to
    /// <paramref name="member"/> on an <see cref="object"/> typed target parameter.
    /// </summary>
    private static bool TryBuildWrite(
        MemberInfo member,
        Type declaringType,
        Type memberType,
        ParameterExpression target,
        Expression value,
        [NotNullWhen(true)] out Expression? write)
    {
        write = null;
        var typedTarget = Expression.Convert(target, declaringType);

        if (member is PropertyInfo property)
        {
            var setMethod = property.GetSetMethod();
            if (setMethod is null || setMethod.IsStatic)
            {
                return false;
            }

            // open delegate for the same stack-trace-fidelity reason as the read path
            if (!TryCreateOpenDelegate(setMethod, [declaringType, memberType], returnType: null, out var accessorDelegate, out var delegateType))
            {
                return false;
            }

            write = Expression.Invoke(Expression.Constant(accessorDelegate, delegateType), typedTarget, value);
            return true;
        }

        var field = (FieldInfo) member;
        if (field.IsInitOnly || field.IsLiteral)
        {
            return false;
        }

        write = Expression.Assign(Expression.Field(typedTarget, field), value);
        return true;
    }

    /// <summary>
    /// Members reachable through a compiled delegate: an instance property or field of a visible
    /// reference type. Value-type receivers are excluded because an open-instance delegate over one
    /// needs a by-ref receiver, and a write through the boxed copy would not reach the original.
    /// </summary>
    private static bool TryGetEligibleMember(
        MemberInfo member,
        [NotNullWhen(true)] out Type? declaringType,
        [NotNullWhen(true)] out Type? memberType)
    {
        declaringType = null;
        memberType = null;

        // Under AOT and an interpreted-only Expression.Compile (e.g. the Mono interpreter) an
        // interpreted lambda is slower than the reflection path it replaces, so decline entirely.
        if (!RuntimeFeature.IsDynamicCodeCompiled)
        {
            return false;
        }

        var type = member.DeclaringType;
        if (type is null || !type.IsVisible || type.IsValueType)
        {
            return false;
        }

        switch (member)
        {
            case PropertyInfo property:
                // indexed properties are served by IndexerAccessor, not by this lane
                if (property.GetIndexParameters().Length != 0)
                {
                    return false;
                }
                memberType = property.PropertyType;
                break;

            case FieldInfo field:
                if (field.IsStatic)
                {
                    return false;
                }
                memberType = field.FieldType;
                break;

            default:
                return false;
        }

        if (memberType.IsByRef || memberType.IsPointer)
        {
            return false;
        }

        declaringType = type;
        return true;
    }

    private static bool TryCreateOpenDelegate(
        MethodInfo method,
        Type[] argumentTypes,
        Type? returnType,
        [NotNullWhen(true)] out Delegate? accessorDelegate,
        [NotNullWhen(true)] out Type? delegateType)
    {
        accessorDelegate = null;
        delegateType = null;

        if (returnType is null)
        {
            if (!Expression.TryGetActionType(argumentTypes, out delegateType))
            {
                return false;
            }
        }
        else
        {
            var typeArguments = new Type[argumentTypes.Length + 1];
            Array.Copy(argumentTypes, typeArguments, argumentTypes.Length);
            typeArguments[argumentTypes.Length] = returnType;
            if (!Expression.TryGetFuncType(typeArguments, out delegateType))
            {
                return false;
            }
        }

        try
        {
            accessorDelegate = method.CreateDelegate(delegateType);
        }
        catch (Exception)
        {
            // an accessibility/binding quirk the eligibility checks did not anticipate
            accessorDelegate = null;
            delegateType = null;
            return false;
        }

        return true;
    }

    // On reads a JsValue of any subtype flows straight through, matching FromObjectWithType's
    // "value is JsValue" short-circuit.
    private static bool IsSupportedMemberType(Type type)
    {
        return type == typeof(int)
               || type == typeof(long)
               || type == typeof(double)
               || type == typeof(bool)
               || type == typeof(string)
               || typeof(JsValue).IsAssignableFrom(type);
    }

    // Writes accept exactly JsValue and no subtype: ReflectionAccessor.SetValue only assigns the
    // JsValue straight through for `_memberType == typeof(JsValue)`, and routes a member typed as a
    // JsValue subtype through ToObject() + the type converter instead. Accepting subtypes here
    // would change that (a JsString assigned to a JsString-typed member would start succeeding),
    // so they keep the existing path.
    private static bool IsSupportedWrittenMemberType(Type type)
    {
        return type == typeof(int)
               || type == typeof(long)
               || type == typeof(double)
               || type == typeof(bool)
               || type == typeof(string)
               || type == typeof(JsValue);
    }

    /// <summary>
    /// Converts the read value to a <see cref="JsValue"/> exactly as
    /// <see cref="JsValue.FromObjectWithType"/> would for these types: the public implicit
    /// operators produce the same instances as <c>DefaultObjectConverter</c>, and a null reference
    /// becomes <see cref="JsValue.Null"/>.
    /// </summary>
    private static Expression BuildJsValueConversion(Expression access, Type memberType)
    {
        if (memberType == typeof(int)
            || memberType == typeof(long)
            || memberType == typeof(double)
            || memberType == typeof(bool)
            || memberType == typeof(string))
        {
            // the string operator maps null to JsValue.Null internally
            var op = typeof(JsValue).GetMethod("op_Implicit", [memberType])!;
            return Expression.Call(op, access);
        }

        return Expression.Coalesce(
            Expression.Convert(access, typeof(JsValue)),
            Expression.Constant(JsValue.Null, typeof(JsValue)));
    }

    /// <summary>
    /// Emits the type test + typed read for the assigned value, returning an expression of the
    /// member type. A non-exact match jumps to <paramref name="returnLabel"/> with
    /// <see langword="false"/>. Mirrors <see cref="CompiledMethodInvoker"/>'s argument binding.
    /// </summary>
    private static Expression BuildValueBinding(
        Type memberType,
        ParameterExpression value,
        LabelTarget returnLabel,
        List<ParameterExpression> locals,
        List<Expression> body)
    {
        if (memberType == typeof(JsValue))
        {
            // matches SetValue's `_memberType == typeof(JsValue)` branch: assigned as-is,
            // JsValue.Null and JsValue.Undefined included
            return value;
        }

        if (memberType == typeof(string))
        {
            // accept only JsString; ToString() returns the underlying string without a copy
            body.Add(DeclineIfNotType(value, typeof(JsString), returnLabel));
            return Expression.Call(value, _objectToString);
        }

        if (memberType == typeof(bool))
        {
            body.Add(DeclineIfNotType(value, typeof(JsBoolean), returnLabel));
            return Expression.Call(_asBoolean, value);
        }

        // int / long / double
        body.Add(DeclineIfNotType(value, typeof(JsNumber), returnLabel));

        var number = Expression.Variable(typeof(double), "d");
        locals.Add(number);
        body.Add(Expression.Assign(number, Expression.Call(_asNumber, value)));

        if (memberType == typeof(double))
        {
            return number;
        }

        // Only convert integral values within the target range, otherwise decline so the fallback
        // reproduces today's conversion behaviour. Math.Floor(v) == v is the integral test (a JIT
        // intrinsic, unlike v % 1 == 0 which lowers to a native fmod call); NaN and infinities are
        // rejected by the tests that short-circuit before it.
        var isNaN = Expression.Call(_doubleIsNaN, number);
        var isInfinity = Expression.Call(_doubleIsInfinity, number);
        var notIntegral = Expression.NotEqual(Expression.Call(_mathFloor, number), number);

        Expression belowRange;
        Expression aboveRange;
        if (memberType == typeof(int))
        {
            belowRange = Expression.LessThan(number, Expression.Constant((double) int.MinValue));
            aboveRange = Expression.GreaterThan(number, Expression.Constant((double) int.MaxValue));
        }
        else
        {
            belowRange = Expression.LessThan(number, Expression.Constant((double) long.MinValue));
            aboveRange = Expression.GreaterThanOrEqual(number, Expression.Constant(LongMaxValueExclusiveUpperBound));
        }

        var decline = Expression.OrElse(
            Expression.OrElse(Expression.OrElse(isNaN, isInfinity), notIntegral),
            Expression.OrElse(belowRange, aboveRange));

        body.Add(Expression.IfThen(decline, Expression.Return(returnLabel, Expression.Constant(false))));

        return Expression.Convert(number, memberType);
    }

    private static ConditionalExpression DeclineIfNotType(Expression value, Type jsType, LabelTarget returnLabel)
    {
        return Expression.IfThen(
            Expression.Not(Expression.TypeIs(value, jsType)),
            Expression.Return(returnLabel, Expression.Constant(false)));
    }
#endif
}
