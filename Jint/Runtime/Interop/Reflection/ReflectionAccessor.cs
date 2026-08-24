using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using Jint.Extensions;
using Jint.Native;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

#pragma warning disable IL2098
#pragma warning disable IL2072
#pragma warning disable IL2077

namespace Jint.Runtime.Interop.Reflection;

/// <summary>
/// Strategy to read and write CLR object properties and fields.
/// </summary>
internal abstract class ReflectionAccessor
{
    private readonly Type? _memberType;
    private readonly PropertyInfo? _indexer;

    /// <summary>
    /// The indexer's getter, resolved once instead of on every read, and only when the probe below could
    /// ever answer: it is invoked with the member <em>name</em>, so an indexer whose single index parameter
    /// cannot accept a <see cref="string"/> (a <c>this[int]</c>, or one taking several indices) makes
    /// <see cref="MethodBase.Invoke(object, object[])"/> throw before reaching the target — which the probe
    /// swallowed into "no value" anyway. Not resolving it is that same answer without the per-read cost.
    /// </summary>
    private readonly MethodInfo? _indexerGetter;

    // per-accessor L1 cache over the process-wide compiled probe, resolved lazily and racily
    private CompiledKeyedAccessor.KeyedIndexerGetter? _compiledIndexerGetter;
    private bool _compiledIndexerGetterResolved;

    public Type? MemberType => _memberType;

    protected ReflectionAccessor(
        Type? memberType,
        PropertyInfo? indexer = null)
    {
        _memberType = memberType;

        var indexerGetter = indexer?.GetGetMethod();
        if (indexerGetter is not null
            && indexerGetter.GetParameters() is [{ ParameterType: var indexType }]
            && indexType.IsAssignableFrom(typeof(string)))
        {
            _indexer = indexer;
            _indexerGetter = indexerGetter;
        }
    }

    public virtual bool Readable => true;

    public abstract bool Writable { get; }

    /// <summary>
    /// Whether an indexer probe runs before the member itself (see <see cref="GetValue"/>), in which case
    /// the one compiled lane that would answer <em>instead of</em> <see cref="GetValue"/> —
    /// <see cref="CompilableMemberAccessor.TryGetJsValue"/>, which produces the <see cref="JsValue"/>
    /// itself — must decline. An indexer that cannot be handed a member name is not one: the constructor
    /// does not record it, so such a member keeps the lane rather than paying for a probe whose only
    /// possible outcome was a swallowed exception. The other compiled lanes need no such guard:
    /// <see cref="SetValue"/> never probes the indexer at all, so
    /// <see cref="CompilableMemberAccessor.TrySetJsValue"/> has nothing to decline for, and the compiled
    /// raw getter runs from inside <see cref="GetValue"/>, only after the indexer probe has already
    /// missed.
    /// </summary>
    protected bool HasIndexer => _indexer is not null;

    protected abstract object? DoGetValue(object target, string memberName);

    protected abstract void DoSetValue(object target, string memberName, object? value);

    /// <summary>
    /// Compiled fast lane: reads the member and produces the <see cref="JsValue"/> directly,
    /// skipping the boxed round-trip through <see cref="JsValue.FromObjectWithType"/>. Returns
    /// <see langword="false"/> when this accessor has no such lane, leaving the caller to run
    /// <see cref="GetValue"/> plus its conversion.
    /// </summary>
    public virtual bool TryGetJsValue(Engine engine, object target, [NotNullWhen(true)] out JsValue? value)
    {
        value = null;
        return false;
    }

    /// <summary>
    /// Compiled fast lane: writes an exact-typed <see cref="JsValue"/> straight to the member.
    /// Returns <see langword="false"/> — having written nothing — when this accessor has no such
    /// lane or the value is not the exact expected JavaScript type, leaving the caller to run the
    /// full conversion path.
    /// </summary>
    public virtual bool TrySetJsValue(Engine engine, object target, JsValue value) => false;

    /// <summary>
    /// Reads the member. <paramref name="valueType"/> receives the declared type of whatever
    /// produced the value: the member's own type normally, or the indexer's when the indexer probe
    /// below answered instead. They are not interchangeable — an indexer can answer for a name a
    /// declared member also carries, and its value is then described by the indexer's type — so the
    /// caller must convert by this type rather than by <see cref="MemberType"/>.
    /// </summary>
    public object? GetValue(Engine engine, object target, string memberName, out Type? valueType)
    {
        valueType = _memberType;

        var constantValue = ConstantValue;
        if (constantValue is not null)
        {
            return constantValue;
        }

        // first check indexer so we don't confuse inherited properties etc
        var value = TryReadFromIndexer(target, memberName);

        if (value is null)
        {
            try
            {
                value = DoGetValue(target, memberName);
            }
            catch (TargetInvocationException tie)
            {
                engine.CheckAmortizedConstraintsAtHostBoundary();
                Throw.MeaningfulException(engine, tie);
            }
        }
        else
        {
            valueType = _indexer!.PropertyType;
        }

        // the success-path boundary check runs in ReflectionDescriptor.DoGet after result
        // conversion, so an awaitable value gets its continuation attached before a throw
        return value;
    }

    protected virtual JsValue? ConstantValue => null;

    private object? TryReadFromIndexer(object target, string memberName)
    {
        var getter = _indexerGetter;
        if (getter is null)
        {
            return null;
        }

        if (!_compiledIndexerGetterResolved)
        {
            _compiledIndexerGetter = CompiledKeyedAccessor.GetIndexerGetter(getter);
            _compiledIndexerGetterResolved = true;
        }

        // Both lanes are equivalent here because every failure is swallowed the same way: reflection wraps
        // whatever the indexer throws in a TargetInvocationException, the compiled delegate rethrows it
        // as-is, and the catch below turns either into "the indexer did not answer".
        try
        {
            var compiled = _compiledIndexerGetter;
            if (compiled is not null)
            {
                return compiled(target, memberName);
            }

            object[] parameters = [memberName];
            return getter.Invoke(target, parameters);
        }
        catch
        {
            return null;
        }
    }

    public void SetValue(Engine engine, object target, string memberName, JsValue value)
    {
        // compiled fast lane: an exact-typed value is written without boxing and without the
        // conversion dispatch below; anything else declines here having written nothing
        if (TrySetJsValue(engine, target, value))
        {
            engine.CheckAmortizedConstraintsAtHostBoundary();
            return;
        }

        object? converted;
        if (_memberType == typeof(JsValue))
        {
            converted = value;
        }
        else if (!ReflectionExtensions.TryConvertViaTypeCoercion(_memberType, engine.Options.Interop.ValueCoercion, value, out converted))
        {
            // attempt to convert the JsValue to the target type
            converted = value.ToObject();
            if (converted != null && converted.GetType() != _memberType)
            {
                converted = ConvertValueToSet(engine, converted);
            }
        }

        try
        {
            DoSetValue(target, memberName, converted);
        }
        catch (TargetInvocationException exception)
        {
            engine.CheckAmortizedConstraintsAtHostBoundary();
            Throw.MeaningfulException(engine, exception);
        }

        engine.CheckAmortizedConstraintsAtHostBoundary();
    }

    /// <remarks>
    /// <paramref name="value"/> carried a <c>[DynamicallyAccessedMembers]</c> annotation until it was
    /// noticed that the attribute is honoured only on a <see cref="Type"/> or a <see cref="string"/>
    /// parameter — on an <see cref="object"/> it is inert, which is what <c>IL2098</c> was reporting in
    /// every embedder's Native AOT publish. The requirement it meant to express is not expressible from
    /// here: the conversion target is <c>_memberType</c> when there is one and <c>value.GetType()</c>
    /// otherwise, and neither is a parameter the caller could annotate.
    /// </remarks>
    protected virtual object? ConvertValueToSet(Engine engine, object value)
    {
        var memberType = _memberType ?? value.GetType();
        return engine._typeConverter.Convert(value, memberType, CultureInfo.InvariantCulture);
    }

    public virtual PropertyDescriptor CreatePropertyDescriptor(Engine engine, object target, string memberName, bool enumerable = true)
    {
        return new ReflectionDescriptor(engine, this, target, memberName, enumerable);
    }
}
