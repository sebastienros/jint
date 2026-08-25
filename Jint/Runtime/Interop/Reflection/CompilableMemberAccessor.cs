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

    /// <summary>
    /// The last converter-type filter that answered "does not claim <see cref="ReflectionAccessor.MemberType"/>",
    /// or <see langword="null"/> if none has. See <see cref="Claims(ObjectConverterTypeFilter)"/>.
    /// </summary>
    private ObjectConverterTypeFilter? _unclaimedBy;

    /// <summary>
    /// The write-side twin of <see cref="_unclaimedBy"/>: the last type-converter filter that answered "does
    /// not claim <see cref="ReflectionAccessor.MemberType"/> as a conversion target". Same memo, same safety
    /// argument — see <see cref="Claims(ObjectConverterTypeFilter)"/>.
    /// </summary>
    private TypeConverterTargetFilter? _unclaimedAsTargetBy;

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

        // An indexer is probed before the member itself (see GetValue), so the lane cannot run when one
        // is present. These accessors never expose a ConstantValue, the third thing GetValue would consult.
        if (HasIndexer)
        {
            return false;
        }

        // Registered object converters must see every CLR value before it becomes a JsValue, and this lane
        // produces the JsValue itself. Only the converters that could actually be handed a value of this
        // member's declared type matter though: a converter that declared its handled types (see
        // OptionsExtensions.AddObjectConverter) leaves every other member on the fast lane. A converter
        // registered without declaring them claims everything, which is the pre-existing behaviour.
        var converterTypeFilter = engine._objectConverterTypeFilter;
        if (converterTypeFilter is not null && Claims(converterTypeFilter))
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
        // The fallback conversion consults the engine's ClrTypeConverter for some of these member types (a
        // JsNumber outside the int range assigned to a long member reaches ConvertValueToSet, for example), so
        // a host-installed converter must keep seeing them. Only for the member types it could answer
        // differently, though: a converter that declared its target types (see OptionsExtensions
        // .SetTypeConverter) leaves every other member on the fast lane. A converter registered without
        // declaring them claims everything, which is the pre-existing behaviour.
        var converterTargetFilter = engine._typeConverterTargetFilter;
        if (converterTargetFilter is not null && Claims(converterTargetFilter))
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
        // widening conversions, which an unbox.any cannot do, so anything not exactly typed keeps
        // the reflection path. A null for a value-type member likewise stays on reflection, whose
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

    /// <summary>
    /// Whether <paramref name="filter"/> claims this accessor's member type, with the affirmative answer —
    /// the one every read on the lane has to have — memoized.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The answer is constant for a given <c>(accessor, filter)</c> pair: <see cref="ReflectionAccessor.MemberType"/>
    /// is fixed at construction and a filter's declared type set is immutable. It cannot be stored on the
    /// accessor outright, because <c>TypeResolver</c>'s accessor cache key deliberately excludes the converter
    /// set, so one accessor is shared by engines whose filters differ — hence the filter reference is part of
    /// the memo and a mismatch simply recomputes.
    /// </para>
    /// <para>
    /// Only the <see langword="false"/> verdict is remembered, which is what makes a single reference field
    /// enough and keeps the memo safe to read and write without synchronization: the field is written only
    /// after that filter answered "does not claim", and it is only ever compared for reference equality, so a
    /// stale or half-published read can at worst repeat the computation — never produce a wrong
    /// <see langword="false"/>, which would silently bypass a converter. The affirmative verdict needs no memo:
    /// it takes the read off this lane entirely, onto a path far more expensive than the probe saved.
    /// </para>
    /// </remarks>
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

    /// <inheritdoc cref="Claims(ObjectConverterTypeFilter)"/>
    private bool Claims(TypeConverterTargetFilter filter)
    {
        if (ReferenceEquals(_unclaimedAsTargetBy, filter))
        {
            return false;
        }

        if (filter.Claims(MemberType))
        {
            return true;
        }

        _unclaimedAsTargetBy = filter;
        return false;
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
