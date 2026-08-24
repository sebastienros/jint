using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Jint.Native;

namespace Jint.Runtime.Interop.Reflection;

/// <summary>
/// The accessor a <see cref="JsAccessibleAttribute"/>-annotated property or field resolves to. It is
/// <see cref="CompilableMemberAccessor"/> with the four delegates supplied by generated code instead of
/// compiled from a <see cref="MemberInfo"/> at run time: same lanes, same order, same decline rules, so the
/// member behaves exactly as the reflected one it replaces — including on the target frameworks and under
/// the runtimes where the compiled lanes decline outright and reflection is otherwise the only path.
/// </summary>
/// <remarks>
/// The one thing it cannot inherit is the reflection fallback. <see cref="CompilableMemberAccessor"/> keeps a
/// <see cref="PropertyInfo"/> around for the values its compiled write lane cannot take — a null for a value
/// type, a boxed value the member type does not accept — and hands those to the runtime binder. Generated
/// code has no binder, so this accessor raises the exception the binder would have raised, which is what
/// <see cref="ThrowUnassignable"/> exists for.
/// </remarks>
internal sealed class GeneratedMemberAccessor : ReflectionAccessor
{
    private readonly Func<object, JsValue>? _jsValueGetter;
    private readonly Func<object, object?>? _rawGetter;
    private readonly Func<object, JsValue, bool>? _jsValueSetter;
    private readonly Action<object, object?>? _rawSetter;

    /// <summary>
    /// The type a written value must be an instance of. <see cref="ReflectionAccessor.MemberType"/> for
    /// everything but a nullable member, where the boxed form of <c>int?</c> is a boxed <c>int</c>.
    /// </summary>
    private readonly Type _assignableType;

    /// <inheritdoc cref="CompilableMemberAccessor" />
    private ObjectConverterTypeFilter? _unclaimedBy;

    internal GeneratedMemberAccessor(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields)] Type memberType,
        Func<object, JsValue>? jsValueGetter,
        Func<object, object?>? rawGetter,
        Func<object, JsValue, bool>? jsValueSetter,
        Action<object, object?>? rawSetter)
        : base(memberType)
    {
        _jsValueGetter = jsValueGetter;
        _rawGetter = rawGetter;
        _jsValueSetter = jsValueSetter;
        _rawSetter = rawSetter;
        _assignableType = Nullable.GetUnderlyingType(memberType) ?? memberType;
    }

    public override bool Readable => _rawGetter is not null;

    public override bool Writable => _rawSetter is not null;

    public override bool TryGetJsValue(Engine engine, object target, [NotNullWhen(true)] out JsValue? value)
    {
        value = null;

        var getter = _jsValueGetter;
        if (getter is null)
        {
            return false;
        }

        // Registered object converters must see every CLR value before it becomes a JsValue, and this lane
        // produces the JsValue itself — the same rule, and the same memoized probe, CompilableMemberAccessor
        // applies. A generated accessor carries no indexer, so the other reason that lane declines cannot
        // arise here.
        var converterTypeFilter = engine._objectConverterTypeFilter;
        if (converterTypeFilter is not null && Claims(converterTypeFilter))
        {
            return false;
        }

        try
        {
            value = getter(target);
        }
        catch (Exception exception)
        {
            ThrowMeaningful(engine, exception);
        }

        return true;
    }

    public override bool TrySetJsValue(Engine engine, object target, JsValue value)
    {
        // The fallback conversion consults the engine's ITypeConverter for some member types, so a
        // host-installed one must keep seeing them.
        if (!engine._typeConverterIsDefault)
        {
            return false;
        }

        var setter = _jsValueSetter;
        if (setter is null)
        {
            return false;
        }

        try
        {
            return setter(target, value);
        }
        catch (Exception exception)
        {
            ThrowMeaningful(engine, exception);
            return false; // unreachable, ThrowMeaningful does not return
        }
    }

    protected override object? DoGetValue(object target, string memberName)
    {
        var getter = _rawGetter;
        if (getter is null)
        {
            return null;
        }

        try
        {
            return getter(target);
        }
        catch (Exception exception) when (exception is not TargetInvocationException)
        {
            // reflection wraps whatever the member throws, a direct call rethrows it as-is: normalize so the
            // callers' TargetInvocationException handling stays identical
            throw new TargetInvocationException(exception);
        }
    }

    protected override void DoSetValue(object target, string memberName, object? value)
    {
        var setter = _rawSetter;
        if (setter is null)
        {
            return;
        }

        // A null is always accepted: the runtime binder writes default(T) for a value-type member rather
        // than refusing, and the generated writer does the same. Anything else has to be an instance of the
        // member's type - the one thing the binder would additionally accept is a widening primitive
        // conversion, which nothing on the conversion path above can produce (every branch of
        // ReflectionAccessor.SetValue lands on the member type exactly).
        if (value is not null && !_assignableType.IsInstanceOfType(value))
        {
            ThrowUnassignable(value);
        }

        try
        {
            setter(target, value);
        }
        catch (Exception exception) when (exception is not TargetInvocationException)
        {
            throw new TargetInvocationException(exception);
        }
    }

    /// <summary>
    /// Raised where <see cref="CompilableMemberAccessor"/> would have handed the value to the runtime binder
    /// and had it refuse: <see cref="ArgumentException"/> is what both <see cref="PropertyInfo.SetValue(object, object)"/>
    /// and <see cref="FieldInfo.SetValue(object, object)"/> throw for a value the member cannot take, and it
    /// is deliberately not wrapped — reflection does not wrap it either, so the engine's
    /// <see cref="TargetInvocationException"/> handling must see the same thing in both cases.
    /// </summary>
    [DoesNotReturn]
    private void ThrowUnassignable(object value)
    {
        var actual = value.GetType().ToString();
#pragma warning disable MA0015 // reflection's own message carries no parameter name, and matching it is the point
        throw new ArgumentException($"Object of type '{actual}' cannot be converted to type '{MemberType}'.");
#pragma warning restore MA0015
    }

    /// <inheritdoc cref="CompilableMemberAccessor" />
    private bool Claims(ObjectConverterTypeFilter filter)
    {
        if (ReferenceEquals(_unclaimedBy, filter))
        {
            return false;
        }

        if (filter.Claims(MemberType))
        {
            return true;
        }

        _unclaimedBy = filter;
        return false;
    }

    [DoesNotReturn]
    private static void ThrowMeaningful(Engine engine, Exception exception)
    {
        engine.CheckAmortizedConstraintsAtHostBoundary();
        var normalized = exception as TargetInvocationException ?? new TargetInvocationException(exception);
        Throw.MeaningfulException(engine, normalized);
        throw normalized; // unreachable, MeaningfulException does not return
    }
}
