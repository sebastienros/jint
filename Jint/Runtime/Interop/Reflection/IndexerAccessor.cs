using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using Jint.Native;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Runtime.Interop.Reflection;

internal sealed class IndexerAccessor : ReflectionAccessor
{
    private readonly object _key;

    // the key never changes, safe to share between getter/ContainsKey invocations (contents are never mutated)
    private readonly object[] _keyParameters;

    private readonly MethodInfo? _getter;
    private readonly MethodInfo? _setter;
    private readonly MethodInfo? _containsKey;

    // Whether the compiled lanes may cast the baked-in key straight to the parameter type each member
    // declares. The key was produced for the indexer's index type, and ContainsKey/Contains is looked up by
    // that same type or by object, so both hold in practice - but the key also comes from a host-installed
    // ClrTypeConverter, so it is verified rather than assumed. Anything else keeps the reflection path, whose
    // ArgumentException for a mismatched argument is the established behaviour.
    private readonly bool _keyFitsAccessors;
    private readonly bool _keyFitsContainsKey;

    // per-accessor L1 caches over the process-wide compiled delegates, resolved lazily and racily
    private CompiledKeyedAccessor.KeyedIndexerGetter? _compiledGetter;
    private bool _compiledGetterResolved;

    private CompiledKeyedAccessor.KeyedValueSetter? _compiledSetter;
    private bool _compiledSetterResolved;

    private CompiledKeyedAccessor.KeyedPredicate? _compiledContainsKey;
    private bool _compiledContainsKeyResolved;

    // Indexer.CanWrite, and false for a key that cannot be an entry. Every string-keyed write the wrapper
    // serves reads it, so it is taken once here rather than asked of the PropertyInfo per write. Declared after
    // the fields the read lane touches, so that the runtime's auto layout moves none of them for it.
    private readonly bool _writable;

    private IndexerAccessor(PropertyInfo indexer, MethodInfo? containsKey, object key, bool keyCannotBeAnEntry = false) : base(indexer.PropertyType)
    {
        Indexer = indexer;
        FirstIndexParameter = indexer.GetIndexParameters()[0];
        KeyCannotBeAnEntry = keyCannotBeAnEntry;

        _containsKey = containsKey;
        _key = key;
        _keyParameters = [key];

        _getter = indexer.GetGetMethod();
        _setter = indexer.GetSetMethod();

        _keyFitsAccessors = key is not null && FirstIndexParameter.ParameterType.IsInstanceOfType(key);
        _keyFitsContainsKey = key is not null
                              && containsKey?.GetParameters() is [{ ParameterType: var containsKeyType }]
                              && containsKeyType.IsInstanceOfType(key);
        _writable = indexer.CanWrite && !keyCannotBeAnEntry;

        if (keyCannotBeAnEntry)
        {
            // No entry, answered through the state KeyIsPresent already reads: the containment probe is resolved
            // here to one that never finds the key, so the read lane, which every name a dictionary resolves to its
            // indexer runs, carries no test of its own for the mark (#4518). ForTarget marks only the non-generic
            // IDictionary indexer, which always carries IDictionary.Contains, so KeyIsPresent never takes its
            // "no probe, assume present" exit for a marked copy.
            Debug.Assert(containsKey is not null, "a marked indexer is IDictionary's, which has Contains");
            _compiledContainsKey = NoEntry.Predicate;
            _compiledContainsKeyResolved = true;
        }
    }

    /// <summary>
    /// The containment probe of an indexer marked <see cref="KeyCannotBeAnEntry"/>, which never asks the target.
    /// </summary>
    /// <remarks>
    /// The target's own <c>Contains</c> would not always answer: <c>ImmutableDictionary</c>'s casts the key to
    /// <c>TKey</c> and throws <see cref="InvalidCastException"/>.
    /// </remarks>
    private static class NoEntry
    {
        internal static readonly CompiledKeyedAccessor.KeyedPredicate Predicate = static (_, _) => false;
    }

    internal PropertyInfo Indexer { get; }

    internal ParameterInfo FirstIndexParameter { get; }

    /// <summary>
    /// Whether this indexer stands in for a name the target cannot hold as a key: the non-generic
    /// <see cref="IDictionary"/> indexer of a generic dictionary, keyed by something that is not the
    /// dictionary's <c>TKey</c>. It addresses no entry, so it is not <see cref="Writable"/>, and
    /// <see cref="ObjectWrapper.Set(JsValue, JsValue, JsValue)"/> keeps such a property on the wrapper instead (#4518).
    /// </summary>
    internal bool KeyCannotBeAnEntry { get; }

    /// <summary>
    /// Whether the type this accessor was resolved for is a dictionary (<see cref="TypeDescriptor.IsDictionary"/>),
    /// the one kind of target whose refusal <see cref="RefusesAsReadOnly"/> reports. Stamped by
    /// <see cref="ForTarget"/>.
    /// </summary>
    internal bool TargetIsDictionary => _targetIsDictionary;

    // set once, in ForTarget, on an accessor TryFindIndexer has just built and the resolver has not yet
    // published, so no reader can observe it changing
    private bool _targetIsDictionary;

    /// <summary>
    /// This accessor as it applies to <paramref name="targetType"/>: stamped with
    /// <see cref="TargetIsDictionary"/>, and a copy that <see cref="KeyCannotBeAnEntry"/> marks when this is the
    /// non-generic <see cref="IDictionary"/> indexer and the target's generic dictionary cannot hold the key.
    /// Asked once per resolved member, before the resolver publishes the accessor.
    /// </summary>
    /// <remarks>
    /// That indexer takes any object, because the interface does. Every generic dictionary the base class
    /// library ships then rejects a key that is not a <c>TKey</c>: the setter throws (an
    /// <see cref="ArgumentException"/>, or a <see cref="NotSupportedException"/> when read-only), the getter
    /// answers <see langword="null"/> and <c>Contains</c> answers <see langword="false"/>, or throws, as
    /// <c>ImmutableDictionary</c>'s does. So the marked copy is not <see cref="Writable"/> and reports no entry
    /// without asking. A name that converts to <c>TKey</c> never reaches this indexer, since the dictionary's
    /// own <c>this[TKey]</c> takes it first.
    /// </remarks>
    internal IndexerAccessor ForTarget(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.Interfaces)] Type targetType)
    {
        var descriptor = TypeDescriptor.Get(targetType);

        var accessor = this;
        if (Indexer.DeclaringType == typeof(IDictionary)
            && descriptor.IsNonStringKeyedGenericDictionary
            && !descriptor.GenericDictionaryKeyType!.IsInstanceOfType(_key))
        {
            accessor = new IndexerAccessor(Indexer, _containsKey, _key, keyCannotBeAnEntry: true);
        }

        accessor._targetIsDictionary = descriptor.IsDictionary;
        return accessor;
    }

    internal static bool TryFindIndexer(
        Engine engine,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties)] Type targetType,
        string propertyName,
        [NotNullWhen(true)] out IndexerAccessor? indexerAccessor,
        [NotNullWhen(true)] out PropertyInfo? indexer)
    {
        indexerAccessor = null;
        indexer = null;
        var paramTypeArray = new Type[1];

        // integer keys can be ambiguous as we only know string keys
        int? integerKey = null;

        if (int.TryParse(propertyName, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intKeyTemp))
        {
            integerKey = intKeyTemp;
        }

        var filter = new Func<MemberInfo, bool>(m => engine.Options.Interop.TypeResolver.Filter(engine, targetType, m));

        // There is deliberately no TypeDescriptor.IntegerIndexerProperty shortcut here: that member
        // is the interface IList.Item (object-typed), so serving it would bypass both the member
        // filter and the declared indexer's parameter typing — the scan below finds the type's own
        // correctly-typed indexer and consults the filter with the polarity the resolver documents.
        // An inverted filter test used to hide this branch for default configurations while letting
        // a filter-rejected indexer through for writes; removing it fixes both.

        // try to find first indexer having either public getter or setter with matching argument type
        PropertyInfo? fallbackIndexer = null;
        foreach (var candidate in targetType.GetProperties())
        {
            if (!filter(candidate))
            {
                continue;
            }

            var indexParameters = candidate.GetIndexParameters();
            if (indexParameters.Length != 1)
            {
                continue;
            }

            if (candidate.GetGetMethod() != null || candidate.GetSetMethod() != null)
            {
                var paramType = indexParameters[0].ParameterType;
                indexerAccessor = ComposeIndexerFactory(engine, targetType, candidate, paramType, propertyName, integerKey, paramTypeArray);
                if (indexerAccessor != null)
                {
                    if (paramType != typeof(string) || integerKey is null)
                    {
                        // exact match, we don't need to check for integer key
                        indexer = candidate;
                        return true;
                    }

                    if (fallbackIndexer is null)
                    {
                        // our fallback
                        fallbackIndexer = candidate;
                    }
                }
            }
        }

        if (fallbackIndexer is not null)
        {
            indexer = fallbackIndexer;
            // just to keep compiler happy, we know we have a value
            indexerAccessor ??= new IndexerAccessor(indexer, containsKey: null, key: null!);
            return true;
        }

        indexerAccessor = default;
        indexer = default;
        return false;
    }

    private static IndexerAccessor? ComposeIndexerFactory(
        Engine engine,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties)] Type targetType,
        PropertyInfo candidate,
        Type paramType,
        string propertyName,
        int? integerKey,
        Type[] paramTypeArray)
    {
        // check for known incompatible types
#if NET8_0_OR_GREATER
        if (typeof(System.Text.Json.Nodes.JsonNode).IsAssignableFrom(targetType)
            && (targetType != typeof(System.Text.Json.Nodes.JsonArray) || paramType != typeof(int))
            && (targetType != typeof(System.Text.Json.Nodes.JsonObject) || paramType != typeof(string)))
        {
            // we cannot access this[string] with anything else than JsonObject, otherwise itw will throw
            // we cannot access this[int] with anything else than JsonArray, otherwise itw will throw
            return null;
        }
#endif

        object? key = null;
        // int key is quite common case
        if (paramType == typeof(int))
        {
            if (integerKey is not null)
            {
                key = integerKey;
            }
        }
        else
        {
            engine._typeConverter.TryConvert(propertyName, paramType, CultureInfo.InvariantCulture, out key);
        }

        if (key is not null)
        {
            // the key can be converted for this indexer
            var indexerProperty = candidate;
            // get contains key method to avoid index exception being thrown in dictionaries
            paramTypeArray[0] = paramType;
            var containsKeyMethod = targetType.GetMethod(nameof(IDictionary<string, string>.ContainsKey), paramTypeArray);
            if (containsKeyMethod is null && targetType.IsAssignableFrom(typeof(IDictionary)))
            {
                paramTypeArray[0] = typeof(object);
                containsKeyMethod = targetType.GetMethod(nameof(IDictionary.Contains), paramTypeArray);
            }

            return new IndexerAccessor(indexerProperty, containsKeyMethod, key);
        }

        // the key type doesn't work for this indexer
        return null;
    }


    public override bool Readable => Indexer.CanRead;

    public override bool Writable => _writable;

    protected override object? DoGetValue(object target, string memberName)
    {
        if (_getter is null)
        {
            Throw.InvalidOperationException("Indexer has no public getter.");
            return null;
        }

        if (!KeyIsPresent(target))
        {
            return JsValue.Undefined;
        }

        if (!_compiledGetterResolved)
        {
            _compiledGetter = _keyFitsAccessors ? CompiledKeyedAccessor.GetIndexerGetter(_getter) : null;
            _compiledGetterResolved = true;
        }

        try
        {
            var compiled = _compiledGetter;
            return compiled is not null
                ? InvokeCompiled(compiled, target, _key)
                : _getter.Invoke(target, _keyParameters);
        }
        catch (TargetInvocationException tie) when (tie.InnerException is KeyNotFoundException)
        {
            return JsValue.Undefined;
        }
    }

    protected override void DoSetValue(object target, string memberName, object? value)
    {
        if (_setter is null)
        {
            Throw.InvalidOperationException("Indexer has no public setter.");
        }

        if (!_compiledSetterResolved)
        {
            _compiledSetter = _keyFitsAccessors ? CompiledKeyedAccessor.GetValueSetter(_setter) : null;
            _compiledSetterResolved = true;
        }

        // The compiled setter casts the value straight to the indexer's value type, so it can only accept a
        // value whose runtime type is exactly that. Reflection additionally performs widening conversions,
        // which an unbox.any cannot do, and a null for a value-typed indexer would throw a
        // NullReferenceException instead of reflection's ArgumentException - both keep the reflection path.
        var setter = _compiledSetter;
        if (setter is null || value is null || value.GetType() != MemberType)
        {
            object?[] parameters = [_key, value];
            _setter.Invoke(target, parameters);
            return;
        }

        try
        {
            setter(target, _key, value);
        }
        catch (Exception exception) when (exception is not TargetInvocationException)
        {
            throw new TargetInvocationException(exception);
        }
    }

    /// <summary>
    /// A dictionary's indexer refuses a write when the dictionary is read-only and says so with
    /// <see cref="NotSupportedException"/>, the collection contracts' answer for a read-only
    /// <see cref="IDictionary"/> or <see cref="IDictionary{TKey,TValue}"/>. Only a dictionary: a keyed host type
    /// that is not one has no such contract, and its exception reaches the host as any setter's does. That is
    /// also what keeps a refusable descriptor out of the interpreter's member lane, which declines dictionaries
    /// (<see cref="ObjectWrapper.TryGetInlineCacheableDescriptor"/>) and could not report one.
    /// </summary>
    protected override bool RefusesAsReadOnly(TargetInvocationException exception)
        => _targetIsDictionary
           && _containsKey is not null
           && exception.InnerException is NotSupportedException inner
           && !Throw.MustPropagateHostException(inner);

    public override PropertyDescriptor CreatePropertyDescriptor(Engine engine, object target, string memberName, bool enumerable = true)
    {
        if (!KeyIsPresent(target))
        {
            return PropertyDescriptor.Undefined;
        }

        return new ReflectionDescriptor(engine, this, target, memberName, enumerable: true);
    }

    /// <summary>
    /// Runs the <c>ContainsKey</c>/<c>Contains</c> probe that keeps a dictionary indexer from throwing on a
    /// missing key. No probe means "assume present", exactly as before.
    /// </summary>
    /// <remarks>
    /// An indexer marked <see cref="KeyCannotBeAnEntry"/> answers <see langword="false"/> here without asking
    /// the target, through the probe its constructor resolves (<see cref="NoEntry"/>), so this method carries
    /// no test of its own for it: every read, <c>in</c> and <c>hasOwnProperty</c> that reaches a dictionary's
    /// indexer runs through it (#4518).
    /// </remarks>
    private bool KeyIsPresent(object target)
    {
        if (_containsKey is null)
        {
            return true;
        }

        if (!_compiledContainsKeyResolved)
        {
            _compiledContainsKey = _keyFitsContainsKey ? CompiledKeyedAccessor.GetKeyPredicate(_containsKey) : null;
            _compiledContainsKeyResolved = true;
        }

        var compiled = _compiledContainsKey;
        if (compiled is null)
        {
            return _containsKey.Invoke(target, _keyParameters) as bool? == true;
        }

        return InvokeCompiled(compiled, target, _key);
    }

    private static object? InvokeCompiled(CompiledKeyedAccessor.KeyedIndexerGetter getter, object target, object key)
    {
        try
        {
            return getter(target, key);
        }
        catch (Exception exception) when (exception is not TargetInvocationException)
        {
            // reflection wraps whatever the indexer throws, the compiled delegate rethrows it as-is:
            // normalize so the callers' TargetInvocationException handling stays identical
            throw new TargetInvocationException(exception);
        }
    }

    private static bool InvokeCompiled(CompiledKeyedAccessor.KeyedPredicate predicate, object target, object key)
    {
        try
        {
            return predicate(target, key);
        }
        catch (Exception exception) when (exception is not TargetInvocationException)
        {
            throw new TargetInvocationException(exception);
        }
    }
}
