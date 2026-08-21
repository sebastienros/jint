using System.Reflection;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Interop;
using Jint.Runtime.Interop.Reflection;

namespace Jint.Runtime.Descriptors.Specialized;

internal sealed class ReflectionDescriptor : PropertyDescriptor
{
    private readonly Engine _engine;
    private readonly ReflectionAccessor _reflectionAccessor;
    private readonly object _target;
    private readonly string _propertyName;
    private ObjectInstance? _owner;

    private JsValue? _get;
    private JsValue? _set;

    // Single-entry memo of the last reference-typed member value and the JsValue it converted to.
    // A live member getter must still run on every read (side effects preserved), but when it returns
    // the same instance as last time - the common case for a property backed by a stable field, e.g.
    // `int[] numbers { get; }` read in a loop - the FromObjectWithType conversion and its identity-cache
    // probe are skipped. Only populated for reference types (a value-type member boxes a fresh object
    // each read, so identity never matches) and only while the recent-wrapper cache is the active
    // reuse mode - see the store site in DoGet for the exact gate.
    private object? _memoValue;
    private JsValue? _memoResult;

    // Whether the host declared this target's type immutable for the crossing
    // (Options.Interop.ImmutableCrossingTypes). Then the memo above is not merely a conversion cache but the
    // answer itself: the member is read exactly once and every later access returns the same JsValue without
    // invoking the CLR getter at all. Derived from the target rather than plumbed through
    // ReflectionAccessor.CreatePropertyDescriptor on purpose - the accessors are cached process-wide on a
    // shared TypeResolver, so nothing that depends on one engine's options may be recorded on them.
    private readonly bool _immutableCrossing;

    public ReflectionDescriptor(
        Engine engine,
        ReflectionAccessor reflectionAccessor,
        object target,
        string propertyName,
        bool enumerable)
        : base((enumerable ? PropertyFlag.Enumerable : PropertyFlag.None) | PropertyFlag.CustomJsValue)
    {
        _flags |= PropertyFlag.NonData;
        _engine = engine;
        _reflectionAccessor = reflectionAccessor;
        _target = target;
        _propertyName = propertyName;
        _immutableCrossing = engine._immutableCrossingFilter?.Claims(target.GetType()) == true;
    }

    public override JsValue? Get
    {
        get
        {
            if (_reflectionAccessor.Readable)
            {
                return _get ??= new GetterFunction(_engine, DoGet);
            }

            return null;
        }
    }

    public override JsValue? Set
    {
        get
        {
            if (_reflectionAccessor.Writable && _engine.Options.Interop.AllowWrite)
            {
                return _set ??= new SetterFunction(_engine, DoSet);
            }

            return null;
        }
    }

    protected internal override JsValue? CustomValue
    {
        get => DoGet(thisObj: null);
        set => DoSet(thisObj: null, value);
    }

    internal void AttachOwner(ObjectInstance owner) => _owner = owner;

    private JsValue DoGet(JsValue? thisObj)
    {
        // Immutability promise (Options.Interop.ImmutableCrossingTypes): the member has already been read
        // once and the host declared that it does not change, so this answers with no CLR call, no
        // conversion and no host boundary crossing - there is no host code left to run.
        if (_immutableCrossing && _memoResult is not null)
        {
            return _memoResult;
        }

        // compiled fast lane: produces the JsValue straight off the CLR member, skipping the boxed
        // value and the FromObjectWithType dispatch below. Only the member shapes whose conversion
        // it reproduces exactly take it; everything else declines.
        if (_reflectionAccessor.TryGetJsValue(_engine, _target, out var fastResult))
        {
            _engine.CheckAmortizedConstraintsAtHostBoundary();
            if (_immutableCrossing)
            {
                _memoResult = fastResult;
            }
            return fastResult;
        }

        var value = _reflectionAccessor.GetValue(_engine, _target, _propertyName, out var valueType);

        // same reference instance as the previous read -> reuse the converted JsValue, skipping
        // FromObjectWithType and the object-wrapper identity caches it consults.
        if (value is not null && ReferenceEquals(value, _memoValue))
        {
            _engine.CheckAmortizedConstraintsAtHostBoundary();
            return _memoResult!;
        }

        var type = valueType ?? value?.GetType();
        // conversion before the check so an awaitable result gets its continuation attached
        var result = JsValue.FromObjectWithType(_engine, value, type);

        if (_immutableCrossing)
        {
            // A declared-immutable target supersedes every condition below: the promise covers value types
            // as well, and holds whatever wrapper-reuse mode is configured, so the memo is populated
            // unconditionally and the CLR read above never runs again for this member.
            _memoValue = value;
            _memoResult = result;
        }
        else
        {
            // Otherwise only memoize when wrapper reuse is already the contract: the recent-wrapper ring is
            // on and the authoritative identity map is off. With the ring off the caller opted into a fresh
            // conversion per crossing (host.X !== host.X), which the memo must not silently undo; with the
            // identity map on the ConditionalWeakTable owns instance -> wrapper mapping. Reference types
            // only - a value-type member boxes a fresh object each read so its identity never matches.
            var interop = _engine.Options.Interop;
            if (value is not null && value is not ValueType
                && interop.CacheRecentObjectWrappers && !interop.TrackObjectWrapperIdentity)
            {
                _memoValue = value;
                _memoResult = result;
            }
        }

        _engine.CheckAmortizedConstraintsAtHostBoundary();
        return result;
    }

    private void DoSet(JsValue? thisObj, JsValue? v)
    {
        if (!_engine.Options.Interop.AllowWrite || _owner is { Extensible: false })
        {
            Throw.TypeError(_engine.Realm, $"Cannot assign to read only property '{_propertyName}' of object '#<Object>'");
        }

        // A write invalidates the memo whether or not the target was declared immutable: without this a
        // declared-immutable member would keep serving the pre-write value to the very script that wrote it.
        // Insurance for a host whose promise was wrong, on a path that is about to run a CLR write anyway.
        _memoValue = null;
        _memoResult = null;

        try
        {
            _reflectionAccessor.SetValue(_engine, _target, _propertyName, v!);
        }
        catch (TargetInvocationException exception)
        {
            Throw.MeaningfulException(_engine, exception);
        }
    }
}
