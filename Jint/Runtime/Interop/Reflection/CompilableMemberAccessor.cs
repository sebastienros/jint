using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Jint.Native;

namespace Jint.Runtime.Interop.Reflection;

/// <summary>
/// Base for the accessors that read and write a single CLR property or field, adding the compiled
/// fast lanes built by <see cref="CompiledMemberAccessor"/> on top of the reflection path each
/// subclass still provides as the fallback.
/// <para>
/// Two lanes exist. The <b>JsValue lane</b> (<see cref="TryGetJsValue"/> / <see cref="TrySetJsValue"/>)
/// short-circuits the whole conversion round-trip for the common member types, so a read never
/// boxes and a write of an exact-typed value goes straight to the CLR member. The <b>raw lane</b>
/// only replaces the reflection call inside <see cref="DoGetValue"/> / <see cref="DoSetValue"/>,
/// leaving every surrounding conversion exactly as it was; it covers every other member type.
/// </para>
/// <para>
/// The compiled delegates themselves are cached process-wide by <see cref="CompiledMemberAccessor"/>;
/// the fields here are only per-accessor L1 caches so the steady-state path is a field read. They
/// are resolved lazily and racily — the delegates are pure, so a duplicate resolve is harmless.
/// </para>
/// </summary>
internal abstract class CompilableMemberAccessor : ReflectionAccessor
{
    private readonly MemberInfo _member;

    private Func<object, JsValue>? _jsValueGetter;
    private bool _jsValueGetterResolved;

    private Func<object, object?>? _rawGetter;
    private bool _rawGetterResolved;

    private CompiledMemberAccessor.JsValueSetter? _jsValueSetter;
    private bool _jsValueSetterResolved;

    private Action<object, object?>? _rawSetter;
    private bool _rawSetterResolved;

    protected CompilableMemberAccessor(MemberInfo member, Type memberType, PropertyInfo? indexer)
        : base(memberType, indexer)
    {
        _member = member;
    }

    /// <summary>Reads the member through reflection (the fallback when no lane applies).</summary>
    protected abstract object? ReflectionGetValue(object target);

    /// <summary>Writes the member through reflection (the fallback when no lane applies).</summary>
    protected abstract void ReflectionSetValue(object target, object? value);

    public sealed override bool TryGetJsValue(Engine engine, object target, [NotNullWhen(true)] out JsValue? value)
    {
        value = null;

        // An indexer is probed before the member itself (see GetValue), and registered object
        // converters must see every CLR value before it becomes a JsValue - the JsValue lane
        // produces the JsValue itself, so it cannot run in either case. These accessors never
        // expose a ConstantValue, the third thing GetValue would consult.
        if (HasIndexer || engine._objectConverters is not null)
        {
            return false;
        }

        if (!_jsValueGetterResolved)
        {
            _jsValueGetter = CompiledMemberAccessor.GetJsValueGetter(_member);
            _jsValueGetterResolved = true;
        }

        var getter = _jsValueGetter;
        if (getter is null)
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

    public sealed override bool TrySetJsValue(Engine engine, object target, JsValue value)
    {
        // The fallback conversion consults the engine's ITypeConverter for some of these member
        // types (an integer JsNumber assigned to a double member reaches ConvertValueToSet, for
        // example), so a host-installed converter must keep seeing them. The flag is maintained by
        // the TypeConverter setter, so this costs a field read instead of a GetType() per write.
        if (!engine._typeConverterIsDefault)
        {
            return false;
        }

        if (!_jsValueSetterResolved)
        {
            _jsValueSetter = CompiledMemberAccessor.GetJsValueSetter(_member);
            _jsValueSetterResolved = true;
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

    protected sealed override object? DoGetValue(object target, string memberName)
    {
        if (!_rawGetterResolved)
        {
            _rawGetter = CompiledMemberAccessor.GetRawGetter(_member);
            _rawGetterResolved = true;
        }

        var getter = _rawGetter;
        if (getter is null)
        {
            return ReflectionGetValue(target);
        }

        try
        {
            return getter(target);
        }
        catch (Exception exception) when (exception is not TargetInvocationException)
        {
            // reflection wraps whatever the member throws, the compiled delegate rethrows it as-is:
            // normalize so the callers' TargetInvocationException handling stays identical
            throw new TargetInvocationException(exception);
        }
    }

    protected sealed override void DoSetValue(object target, string memberName, object? value)
    {
        if (!_rawSetterResolved)
        {
            _rawSetter = CompiledMemberAccessor.GetRawSetter(_member);
            _rawSetterResolved = true;
        }

        // The compiled setter casts the incoming value straight to the member type, so it can only
        // accept a value whose runtime type is exactly that. Reflection additionally performs
        // widening conversions - and the coercion path above produces exactly such a case, handing
        // a boxed int to a long member - which an unbox.any cannot do, so those keep the
        // reflection path. A null for a value-type member likewise stays on reflection, whose
        // ArgumentException is the established behaviour (unbox.any would throw a
        // NullReferenceException instead).
        var setter = _rawSetter;
        if (setter is null || value is null || value.GetType() != MemberType)
        {
            ReflectionSetValue(target, value);
            return;
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

    [DoesNotReturn]
    private static void ThrowMeaningful(Engine engine, Exception exception)
    {
        // mirrors GetValue/SetValue: the boundary check runs before the throw so a constraint
        // violation is reported the same way it is on the reflection path
        engine.CheckAmortizedConstraintsAtHostBoundary();
        var normalized = exception as TargetInvocationException ?? new TargetInvocationException(exception);
        Throw.MeaningfulException(engine, normalized);
        throw normalized; // unreachable, MeaningfulException does not return
    }
}
