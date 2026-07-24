using System.Reflection;
using Jint.Native;
using Jint.Runtime.Interop;
using Jint.Runtime.Interop.Reflection;

namespace Jint.Runtime.Descriptors.Specialized;

internal sealed class ReflectionDescriptor : PropertyDescriptor
{
    private readonly Engine _engine;
    private readonly ReflectionAccessor _reflectionAccessor;
    private readonly object _target;
    private readonly string _propertyName;

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

    private JsValue DoGet(JsValue? thisObj)
    {
        // compiled fast lane: produces the JsValue straight off the CLR member, skipping the boxed
        // value and the FromObjectWithType dispatch below. Only the member shapes whose conversion
        // it reproduces exactly take it; everything else declines.
        if (_reflectionAccessor.TryGetJsValue(_engine, _target, out var fastResult))
        {
            _engine.CheckAmortizedConstraintsAtHostBoundary();
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

        // Only memoize when wrapper reuse is already the contract: the recent-wrapper ring is on and
        // the authoritative identity map is off. With the ring off the caller opted into a fresh
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

        _engine.CheckAmortizedConstraintsAtHostBoundary();
        return result;
    }

    private void DoSet(JsValue? thisObj, JsValue? v)
    {
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
