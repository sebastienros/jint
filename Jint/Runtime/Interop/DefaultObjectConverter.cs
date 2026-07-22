using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint;

internal static class DefaultObjectConverter
{
    private static Dictionary<Type, Func<Engine, object, JsValue>> _typeMappers = new()
    {
        { typeof(bool), (engine, v) => (bool)v ? JsBoolean.True : JsBoolean.False },
        { typeof(byte), (engine, v) => JsNumber.Create((byte)v) },
        { typeof(char), (engine, v) => JsString.Create((char)v) },
        { typeof(DateTime), (engine, v) => engine.Realm.Intrinsics.Date.Construct((DateTime)v) },
        { typeof(DateTimeOffset), (engine, v) => engine.Realm.Intrinsics.Date.Construct((DateTimeOffset)v) },
        { typeof(decimal), (engine, v) => (JsValue)(double)(decimal)v },
        { typeof(double), (engine, v) => (JsValue)(double)v },
        { typeof(short), (engine, v) => JsNumber.Create((short)v) },
        { typeof(int), (engine, v) => JsNumber.Create((int)v) },
        { typeof(long), (engine, v) => (JsValue)(long)v },
        { typeof(sbyte), (engine, v) => JsNumber.Create((sbyte)v) },
        { typeof(float), (engine, v) => (JsValue)(float)v },
        { typeof(string), (engine, v) => JsString.Create((string)v) },
        { typeof(ushort), (engine, v) => JsNumber.Create((ushort)v) },
        { typeof(uint), (engine, v) => JsNumber.Create((uint)v) },
        { typeof(ulong), (engine, v) => JsNumber.Create((ulong)v) },
        {
            typeof(System.Text.RegularExpressions.Regex),
            (engine, v) => engine.Realm.Intrinsics.RegExp.Construct((System.Text.RegularExpressions.Regex)v)
        }
    };

    public static bool TryConvert(Engine engine, object value, Type? type, [NotNullWhen(true)] out JsValue? result)
    {
        result = null;
        Type valueType = ObjectWrapper.GetClrType(value, type);

        var typeMappers = _typeMappers;

        if (typeMappers.TryGetValue(valueType, out var typeMapper))
        {
            result = typeMapper(engine, value);
        }
        else
        {
            if (value is Array a)
            {
                if (valueType.IsArray)
                {
                    // memoization is only valid when the exposed type itself is an array type: every
                    // future value crossing under it is an Array. A non-array exposed type (IEnumerable<T>,
                    // IReadOnlyList<T>, ...) can later carry a List<T> or other non-array value, and
                    // _typeMappers is a static cross-engine map — baking ConvertArray under such a type
                    // would poison every engine in the process.
                    // racy, we don't care, worst case we'll catch up later
                    Interlocked.CompareExchange(ref _typeMappers,
                        new Dictionary<Type, Func<Engine, object, JsValue>>(typeMappers)
                        {
                            [valueType] = ConvertArray
                        }, typeMappers);

                    result = ConvertArray(engine, a);
                    return result is not null;
                }

                if (engine.Options.Interop.ArrayConversion == ArrayConversionMode.Copy)
                {
                    result = ConvertArray(engine, a);
                    return result is not null;
                }

                // LiveView with a non-array exposed type: fall through to the wrapper lane below so the
                // view honors the declared contract the same way other collections do — an array exposed
                // as IReadOnlyList<T> must produce a read-only view, not a writable one keyed off the
                // runtime type.
            }

            if (value is IConvertible convertible && TryConvertConvertible(engine, convertible, out result))
            {
                return true;
            }

            if (value is Delegate d)
            {
                if (d is JsCallDelegate jsCallDelegate
                    && jsCallDelegate.Target is JsValue jsFunction
                    && jsFunction is ICallable)
                {
                    result = jsFunction;
                    return true;
                }

                result = new DelegateWrapper(engine, d);
                return result is not null;
            }

            if ((engine.Options.ExperimentalFeatures & ExperimentalFeature.TaskInterop) != ExperimentalFeature.None)
            {
                if (value is Task task)
                {
                    result = JsValue.ConvertAwaitableToPromise(engine, task);
                    return result is not null;
                }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
                if (value is ValueTask valueTask)
                {
                    result = JsValue.ConvertAwaitableToPromise(engine, valueTask);
                    return result is not null;
                }

                // ValueTask<T> is not derived from ValueTask, so we need to check for it explicitly
                var valueType2 = value.GetType();
                if (valueType2.IsGenericType && valueType2.GetGenericTypeDefinition() == typeof(ValueTask<>))
                {
                    result = JsValue.ConvertAwaitableToPromise(engine, value);
                    return result is not null;
                }
#endif
            }

#if NET8_0_OR_GREATER
            if (value is System.Text.Json.Nodes.JsonValue jsonValue)
            {
                result = ConvertSystemTextJsonValue(engine, jsonValue);
                return result is not null;
            }
#endif

            var t = value.GetType();

            if (!engine.Options.Interop.AllowSystemReflection
                && t.Namespace?.StartsWith("System.Reflection", StringComparison.Ordinal) == true)
            {
                const string Message = "Cannot access System.Reflection namespace, check Engine's interop options";
                Throw.InvalidOperationException(Message);
            }

            if (t.IsEnum)
            {
                var ut = Enum.GetUnderlyingType(t);

                if (ut == typeof(ulong))
                {
                    result = JsNumber.Create(Convert.ToDouble(value, CultureInfo.InvariantCulture));
                }
                else
                {
                    if (ut == typeof(uint) || ut == typeof(long))
                    {
                        result = JsNumber.Create(Convert.ToInt64(value, CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        result = JsNumber.Create(Convert.ToInt32(value, CultureInfo.InvariantCulture));
                    }
                }
            }
            else
            {
                // check global cache, have we already wrapped the value? A cached wrapper is only
                // valid for the same exposed CLR type — the same object may also cross as an
                // explicit interface/superclass view, which resolves a different member set.
                if (engine._objectWrapperCache?.TryGetValue(value, out var cached) == true
                    && (cached is not ObjectWrapper cachedWrapper || cachedWrapper.ClrType == valueType))
                {
                    result = cached;
                }
                else if (engine._recentObjectWrapperCache?.TryGet(value, valueType) is { } recentlyWrapped)
                {
                    result = recentlyWrapped;
                }
                else
                {
                    var wrapped = engine.Options.Interop.WrapObjectHandler.Invoke(engine, value, type);

                    if (ReferenceEquals(wrapped?.GetPrototypeOf(), engine.Realm.Intrinsics.Object.PrototypeObject)
                        && engine._typeReferences?.TryGetValue(t, out var typeReference) == true)
                    {
                        wrapped.SetPrototypeOf(typeReference);
                    }

                    result = wrapped;

                    if (wrapped is not null)
                    {
                        if (engine.Options.Interop.TrackObjectWrapperIdentity)
                        {
                            engine._objectWrapperCache ??= new ConditionalWeakTable<object, ObjectInstance>();
                            // the table may hold a wrapper for a different exposed view of the same
                            // object (the type-guarded lookup above missed it) — last view wins
                            engine._objectWrapperCache.Remove(value);
                            engine._objectWrapperCache.Add(value, wrapped);
                        }
                        else if (engine.Options.Interop.CacheRecentObjectWrappers)
                        {
                            engine._recentObjectWrapperCache ??= new RecentObjectWrapperCache();
                            engine._recentObjectWrapperCache.Add(value, wrapped);
                        }
                    }
                }
            }

            // if no known type could be guessed, use the default of wrapping using ObjectWrapper
        }

        return result is not null;
    }

#if NET8_0_OR_GREATER
    private static JsValue? ConvertSystemTextJsonValue(Engine engine, System.Text.Json.Nodes.JsonNode value)
    {
        return value.GetValueKind() switch
        {
            System.Text.Json.JsonValueKind.Object => JsValue.FromObject(engine, value),
            System.Text.Json.JsonValueKind.Array => JsValue.FromObject(engine, value),
            System.Text.Json.JsonValueKind.String => JsString.Create(value.ToString()),
#pragma warning disable IL2026, IL3050
            System.Text.Json.JsonValueKind.Number => ((System.Text.Json.Nodes.JsonValue) value).TryGetValue<int>(out var intValue)
                ? JsNumber.Create(intValue)
                : System.Text.Json.JsonSerializer.Deserialize<double>(value),
#pragma warning restore IL2026, IL3050
            System.Text.Json.JsonValueKind.True => JsBoolean.True,
            System.Text.Json.JsonValueKind.False => JsBoolean.False,
            System.Text.Json.JsonValueKind.Undefined => JsValue.Undefined,
            System.Text.Json.JsonValueKind.Null => JsValue.Null,
            _ => null,
        };
    }
#endif

    private static bool TryConvertConvertible(Engine engine, IConvertible convertible, [NotNullWhen(true)] out JsValue? result)
    {
        result = convertible.GetTypeCode() switch
        {
            TypeCode.Boolean => convertible.ToBoolean(engine.Options.Culture) ? JsBoolean.True : JsBoolean.False,
            TypeCode.Byte => JsNumber.Create(convertible.ToByte(engine.Options.Culture)),
            TypeCode.Char => JsString.Create(convertible.ToChar(engine.Options.Culture)),
            TypeCode.Double => JsNumber.Create(convertible.ToDouble(engine.Options.Culture)),
            TypeCode.SByte => JsNumber.Create(convertible.ToSByte(engine.Options.Culture)),
            TypeCode.Int16 => JsNumber.Create(convertible.ToInt16(engine.Options.Culture)),
            TypeCode.Int32 => JsNumber.Create(convertible.ToInt32(engine.Options.Culture)),
            TypeCode.UInt16 => JsNumber.Create(convertible.ToUInt16(engine.Options.Culture)),
            TypeCode.Int64 => JsNumber.Create(convertible.ToInt64(engine.Options.Culture)),
            TypeCode.Single => JsNumber.Create(convertible.ToSingle(engine.Options.Culture)),
            TypeCode.String => JsString.Create(convertible.ToString(engine.Options.Culture)),
            TypeCode.UInt32 => JsNumber.Create(convertible.ToUInt32(engine.Options.Culture)),
            TypeCode.UInt64 => JsNumber.Create(convertible.ToUInt64(engine.Options.Culture)),
            TypeCode.DateTime => engine.Realm.Intrinsics.Date.Construct(convertible.ToDateTime(engine.Options.Culture)),
            TypeCode.Decimal => JsNumber.Create(convertible.ToDecimal(engine.Options.Culture)),
            TypeCode.DBNull => JsValue.Null,
            TypeCode.Empty => JsValue.Null,
            _ => null
        };

        return result is not null;
    }

    private static JsValue ConvertArray(Engine e, object v)
    {
        // The identity caches (CacheRecentObjectWrappers default on since 4.14, the identity map
        // opt-in) also cover arrays: repeated conversions of the same CLR array instance return the
        // same result (JsArray snapshot under Copy, wrapper view under LiveView) instead of
        // re-converting on every property read. With caching disabled each conversion still produces
        // a fresh copy/wrapper. All option checks must stay inside this method — _typeMappers is a
        // static cross-engine memoization, so per-engine option state can never be baked into the
        // memoized mapper.
        var arrayType = v.GetType();
        if (e._objectWrapperCache?.TryGetValue(v, out var cached) == true
            && (cached is not ObjectWrapper cachedWrapper || cachedWrapper.ClrType == arrayType))
        {
            return cached;
        }

        if (e._recentObjectWrapperCache?.TryGet(v, arrayType) is { } recentlyConverted)
        {
            return recentlyConverted;
        }

        if (e.Options.Interop.ArrayConversion == ArrayConversionMode.LiveView
            && TryConvertArrayLiveView(e, v, arrayType, out var liveView))
        {
            return liveView;
        }

        var array = (Array) v;
        var arrayLength = (uint) array.Length;

        var values = new JsValue[arrayLength];
        for (uint i = 0; i < arrayLength; ++i)
        {
            values[i] = JsValue.FromObject(e, array.GetValue(i));
        }

        var result = new JsArray(e, values);

        if (e.Options.Interop.TrackObjectWrapperIdentity)
        {
            e._objectWrapperCache ??= new ConditionalWeakTable<object, ObjectInstance>();
            // the table may hold a wrapper for a different exposed view of the same
            // object (the type-guarded lookup above missed it) — last view wins
            e._objectWrapperCache.Remove(v);
            e._objectWrapperCache.Add(v, result);
        }
        else if (e.Options.Interop.CacheRecentObjectWrappers)
        {
            e._recentObjectWrapperCache ??= new RecentObjectWrapperCache();
            e._recentObjectWrapperCache.Add(v, result);
        }

        return result;
    }

    /// <summary>
    /// LiveView lane of <see cref="ConvertArray"/>: routes a single-rank zero-based array (T[])
    /// through the same wrapper machinery as any other CLR object (<see cref="Options.InteropOptions.WrapObjectHandler"/>,
    /// which by default builds an <see cref="ArrayWrapper{T}"/> via <see cref="ObjectWrapper.Create"/>,
    /// plus the flag-gated identity caches — already consulted by the caller). Multi-rank (T[,]) and
    /// non-zero-based (T[*]) arrays return false so they keep the Copy-mode failure behavior.
    /// </summary>
    private static bool TryConvertArrayLiveView(Engine e, object v, Type arrayType, [NotNullWhen(true)] out JsValue? result)
    {
        if (arrayType.GetElementType() is not { } elementType
            || arrayType != elementType.MakeArrayType())
        {
            result = null;
            return false;
        }

        var wrapped = e.Options.Interop.WrapObjectHandler.Invoke(e, v, arrayType);
        if (wrapped is null)
        {
            // custom handler opted out, fall back to the Copy lane
            result = null;
            return false;
        }

        if (e.Options.Interop.TrackObjectWrapperIdentity)
        {
            e._objectWrapperCache ??= new ConditionalWeakTable<object, ObjectInstance>();
            // the table may hold a wrapper for a different exposed view of the same
            // object (the type-guarded lookup above missed it) — last view wins
            e._objectWrapperCache.Remove(v);
            e._objectWrapperCache.Add(v, wrapped);
        }
        else if (e.Options.Interop.CacheRecentObjectWrappers)
        {
            e._recentObjectWrapperCache ??= new RecentObjectWrapperCache();
            e._recentObjectWrapperCache.Add(v, wrapped);
        }

        result = wrapped;
        return true;
    }
}

/// <summary>
/// Bounded ring of most recently wrapped CLR objects, looked up by reference identity.
/// Strongly roots at most <see cref="Capacity"/> targets and their wrappers, so unlike
/// the ConditionalWeakTable identity map it cannot grow without bound.
/// </summary>
internal sealed class RecentObjectWrapperCache
{
    private const int Capacity = 8;

    private readonly object?[] _targets = new object?[Capacity];
    private readonly ObjectInstance[] _wrappers = new ObjectInstance[Capacity];
    private int _next;

    public ObjectInstance? TryGet(object target, Type clrType)
    {
        var targets = _targets;
        for (var i = 0; i < targets.Length; i++)
        {
            if (ReferenceEquals(targets[i], target))
            {
                // the same object can cross under different exposed CLR types (explicit interface /
                // superclass views resolve different members), so a wrapper is only reusable for the
                // same exposed type. A mismatched slot is skipped rather than a terminal miss —
                // wrappers for both views can coexist in the ring.
                var wrapper = _wrappers[i];
                if (wrapper is not ObjectWrapper objectWrapper || objectWrapper.ClrType == clrType)
                {
                    return wrapper;
                }
            }
        }

        return null;
    }

    public void Add(object target, ObjectInstance wrapper)
    {
        var index = _next;
        _targets[index] = target;
        _wrappers[index] = wrapper;
        _next = (index + 1) & (Capacity - 1);
    }

    public void Clear()
    {
        Array.Clear(_targets, 0, _targets.Length);
        Array.Clear(_wrappers, 0, _wrappers.Length);
        _next = 0;
    }
}
