using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Jint.Native;
using Jint.Native.Array;
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

            // An enum is IConvertible and reports its underlying type code, so the convertible lane just
            // below is what turns it into a number — there is no enum-specific handling left to reach.
            // The string form is Enum.ToString(): the member name, the comma separated names of a matching
            // Flags combination, or the number for a nameless value.
            if (engine._enumsAsStrings && value is Enum enumValue)
            {
                result = JsString.Create(enumValue.ToString());
                return true;
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

#if !NETFRAMEWORK && !NETSTANDARD2_0
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

            // NOTE: enums never reach this point. Every enum is an IConvertible whose GetTypeCode()
            // reports its underlying type, so the convertible lane above already converted it to the
            // JsNumber for its underlying value and returned. Any enum-specific handling therefore
            // belongs in TryConvertConvertible (or ahead of the convertible check), never here.

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
            System.Text.Json.JsonValueKind.Number => ConvertSystemTextJsonNumber((System.Text.Json.Nodes.JsonValue) value),
            System.Text.Json.JsonValueKind.True => JsBoolean.True,
            System.Text.Json.JsonValueKind.False => JsBoolean.False,
            System.Text.Json.JsonValueKind.Undefined => JsValue.Undefined,
            System.Text.Json.JsonValueKind.Null => JsValue.Null,
            _ => null,
        };
    }

    /// <summary>
    /// A JSON number as the nearest JavaScript number, asked of the node itself rather than of the
    /// serializer.
    /// </summary>
    /// <remarks>
    /// This used to fall through to <c>JsonSerializer.Deserialize&lt;double&gt;</c>, which is both
    /// <c>[RequiresUnreferencedCode]</c> and <c>[RequiresDynamicCode]</c> - it goes through the whole
    /// converter machinery, and reported so in every embedder's trimming and Native AOT build - to read a
    /// number out of a node that is already parsed and already answers the question directly. The
    /// <see cref="int"/> attempt above it always did.
    /// </remarks>
    private static JsNumber ConvertSystemTextJsonNumber(System.Text.Json.Nodes.JsonValue value)
    {
        if (value.TryGetValue<int>(out var intValue))
        {
            return JsNumber.Create(intValue);
        }

        if (value.TryGetValue<double>(out var doubleValue))
        {
            return JsNumber.Create(doubleValue);
        }

        // A JsonValue built around a CLR number the double conversion declines (a decimal, say) still
        // renders as its JSON text, which is what a number literal in a script would have been read from.
        if (double.TryParse(value.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out doubleValue))
        {
            return JsNumber.Create(doubleValue);
        }

        return JsNumber.DoubleNaN;
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
        var copyContext = e._arrayCopyContext;
        if (copyContext?.TryGetGraphCopy(v, out var inProgress) == true)
        {
            copyContext.ObserveDependency(inProgress);
            return inProgress;
        }

        if (e._objectWrapperCache?.TryGetValue(v, out var cached) == true
            && (cached is not ObjectWrapper cachedWrapper || cachedWrapper.ClrType == arrayType)
            && (copyContext is null || copyContext.CanReuseCachedArray((Array) v, cached)))
        {
            copyContext?.ObserveDependency(cached);
            return cached;
        }

        if (e._recentObjectWrapperCache?.TryGet(v, arrayType) is { } recentlyConverted
            && (copyContext is null || copyContext.CanReuseCachedArray((Array) v, recentlyConverted)))
        {
            copyContext?.ObserveDependency(recentlyConverted);
            return recentlyConverted;
        }

        if (e.Options.Interop.ArrayConversion == ArrayConversionMode.LiveView
            && TryConvertArrayLiveView(e, v, arrayType, out var liveView))
        {
            if (liveView is ObjectInstance liveViewObject)
            {
                copyContext?.ObserveDependency(liveViewObject);
            }

            return liveView;
        }

        copyContext ??= e._arrayCopyContext = new ArrayCopyContext();
        var result = copyContext.IsActive
            ? CopyArrayGraph(e, (Array) v, copyContext)
            : e.ExecuteWithMemoryAccounting(() => CopyArrayGraph(e, (Array) v, copyContext));
        copyContext.ObserveDependency(result);
        return result;
    }

    private static JsArray CopyArrayGraph(Engine engine, Array source, ArrayCopyContext copyContext)
    {
        engine.CheckInteropProjectionConstraints();
        var result = CreateArraySnapshot(engine, source);
        engine.CheckInteropProjectionConstraints();
        var isRootCopy = !copyContext.IsActive;
        var savepoint = copyContext.CreateSavepoint(engine);
        var stack = new List<ArrayCopyFrame>(capacity: 4);
        var workUntilConstraintCheck = Engine.ConstraintCheckInterval;
        var succeeded = false;
        copyContext.Begin(source, result);
        stack.Add(new ArrayCopyFrame(source, result, Index: 0));
        try
        {
            while (stack.Count > 0)
            {
                var frameIndex = stack.Count - 1;
                var frame = stack[frameIndex];
                if (frame.Index >= frame.Result.Length)
                {
                    copyContext.Complete(
                        engine,
                        frame.Source,
                        frame.Result,
                        engine.Options.Interop.TrackObjectWrapperIdentity,
                        engine.Options.Interop.CacheRecentObjectWrappers);
                    copyContext.End(frame.Source);
                    stack.RemoveAt(frameIndex);
                    continue;
                }

                var index = frame.Index;
                stack[frameIndex] = frame with { Index = index + 1 };
                if (--workUntilConstraintCheck == 0)
                {
                    engine.CheckInteropProjectionConstraints();
                    workUntilConstraintCheck = Engine.ConstraintCheckInterval;
                }

                var element = frame.Source.GetValue(index);
                JsValue converted;
                if (element is Array child)
                {
                    if (!TryConvertNestedArrayObserved(
                            engine,
                            child,
                            copyContext,
                            frame.Result,
                            out var nestedConverted))
                    {
                        if (child.Length >= Engine.ConstraintCheckInterval)
                        {
                            engine.CheckInteropProjectionConstraints();
                        }

                        var childResult = CreateArraySnapshot(engine, child);
                        if (child.Length >= Engine.ConstraintCheckInterval)
                        {
                            engine.CheckInteropProjectionConstraints();
                        }

                        copyContext.Begin(child, childResult);
                        copyContext.RecordDependency(frame.Result, childResult);
                        frame.Result.SetIndexValue(index, childResult, updateLength: false);
                        stack.Add(new ArrayCopyFrame(child, childResult, Index: 0));
                        continue;
                    }

                    converted = nestedConverted;
                }
                else
                {
                    if (engine._objectConverters is null)
                    {
                        converted = JsValue.FromObject(engine, element);
                    }
                    else
                    {
                        var observation = copyContext.BeginDependencyObservation();
                        try
                        {
                            converted = JsValue.FromObject(engine, element);
                        }
                        finally
                        {
                            copyContext.EndDependencyObservation(frame.Result, observation);
                        }
                    }
                }

                copyContext.RecordDependency(frame.Result, converted);
                frame.Result.SetIndexValue(index, converted, updateLength: false);
            }

            if (isRootCopy)
            {
                copyContext.Publish(engine);
                engine.CheckInteropProjectionConstraints();
                copyContext.CommitDiagnostics(engine);
            }

            succeeded = true;
            return result;
        }
        finally
        {
            for (var i = stack.Count - 1; i >= 0; i--)
            {
                copyContext.End(stack[i].Source);
            }

            if (!succeeded)
            {
                copyContext.Rollback(engine, in savepoint);
            }
            else
            {
                ArrayCopyContext.Commit(in savepoint);
            }

            if (isRootCopy)
            {
                copyContext.DiscardCompleted();
            }
        }
    }

    private static JsArray CreateArraySnapshot(Engine engine, Array source)
    {
        var length = (uint) source.Length;
        if (engine._untrustedCodeLimits is not null
            && length > engine.Options.Constraints.MaxArraySize)
        {
            ArrayInstance.ThrowMaximumArraySizeReachedException(engine, length);
        }

        return new JsArray(engine, new JsValue[length]);
    }

    private static bool TryConvertNestedArray(
        Engine engine,
        Array array,
        ArrayCopyContext copyContext,
        [NotNullWhen(true)] out JsValue? result)
    {
        if (engine._objectConverters is not null)
        {
            foreach (var converter in engine._objectConverters)
            {
                if (converter.TryConvert(engine, array, out result))
                {
                    return true;
                }
            }
        }

        var arrayType = array.GetType();
        // A nested array may reuse a snapshot from an older graph unless its CLR graph reaches an
        // array currently being copied. In that case the older snapshot points back to an older
        // root and would splice two generations of a cycle together.
        if (engine._objectWrapperCache?.TryGetValue(array, out var cached) == true
            && (cached is not ObjectWrapper cachedWrapper || cachedWrapper.ClrType == arrayType)
            && copyContext.CanReuseCachedArray(array, cached))
        {
            result = cached;
            return true;
        }

        if (engine._recentObjectWrapperCache?.TryGet(array, arrayType) is { } recentlyConverted
            && copyContext.CanReuseCachedArray(array, recentlyConverted))
        {
            result = recentlyConverted;
            return true;
        }

        if (copyContext.TryGetGraphCopy(array, out var graphCopy))
        {
            result = graphCopy;
            return true;
        }

        if (engine.Options.Interop.ArrayConversion == ArrayConversionMode.LiveView
            && TryConvertArrayLiveView(engine, array, arrayType, out result))
        {
            return true;
        }

        result = null;
        return false;
    }

    private static bool TryConvertNestedArrayObserved(
        Engine engine,
        Array array,
        ArrayCopyContext copyContext,
        ObjectInstance parent,
        [NotNullWhen(true)] out JsValue? result)
    {
        if (engine._objectConverters is null)
        {
            return TryConvertNestedArray(engine, array, copyContext, out result);
        }

        var observation = copyContext.BeginDependencyObservation();
        try
        {
            return TryConvertNestedArray(engine, array, copyContext, out result);
        }
        finally
        {
            copyContext.EndDependencyObservation(parent, observation);
        }
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
        if (!arrayType.IsSZArray)
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

        e._arrayLiveViewConversions++;

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

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct ArrayCopyFrame(Array Source, JsArray Result, uint Index);
}

internal sealed class ArrayCopyContext
{
    private const int MaximumRetainedBookkeepingCapacity = 1024;

    private readonly ConditionalWeakTable<object, ObjectInstance> _inProgress = new();
    private readonly ConditionalWeakTable<object, ObjectInstance> _completedIdentityCopies = new();
    private readonly ConditionalWeakTable<object, ArrayToken> _arrayTokens = new();
    private readonly ConditionalWeakTable<ObjectInstance, ArrayToken> _snapshotTokens = new();
    private readonly ConditionalWeakTable<ObjectInstance, ArrayDependencies> _arrayDependencies = new();
    private readonly HashSet<ArrayToken> _inProgressTokens = [];
    private readonly List<CompletedArrayCopy> _completed = [];
    private readonly List<PublishedIdentityCopy> _publishedIdentityCopies = [];
    private readonly List<List<ObjectInstance>?> _dependencyObservations = [];
    private int _depth;

    public bool IsActive => _depth > 0;

    public bool TryGetGraphCopy(object target, [NotNullWhen(true)] out ObjectInstance? result)
        => _inProgress.TryGetValue(target, out result)
            || _completedIdentityCopies.TryGetValue(target, out result);

    public ArrayCopySavepoint CreateSavepoint(Engine engine)
    {
        if (!engine.Options.Interop.TrackObjectWrapperIdentity
            && engine.Options.Interop.CacheRecentObjectWrappers)
        {
            var createdRecentCache = engine._recentObjectWrapperCache is null;
            engine._recentObjectWrapperCache ??= new RecentObjectWrapperCache();
            return new(
                _completed.Count,
                _publishedIdentityCopies.Count,
                engine._recentObjectWrapperCache,
                engine._recentObjectWrapperCache.BeginTransaction(),
                createdRecentCache);
        }

        return new(
            _completed.Count,
            _publishedIdentityCopies.Count,
            RecentCache: null,
            RecentTransaction: null,
            CreatedRecentCache: false);
    }

    public void Begin(object target, ObjectInstance result)
    {
        if (!_arrayTokens.TryGetValue(target, out var token))
        {
            token = new ArrayToken();
            _arrayTokens.Add(target, token);
        }

        _inProgress.Add(target, result);
        _snapshotTokens.Add(result, token);
        _inProgressTokens.Add(token);
        _depth++;
    }

    public void Complete(
        Engine engine,
        object target,
        ObjectInstance result,
        bool trackIdentity,
        bool cacheRecent)
    {
        _completed.Add(new CompletedArrayCopy(target, result, trackIdentity));
        if (trackIdentity)
        {
            // Defer publication to the engine caches until the root copy succeeds, but preserve the
            // same reuse semantics for repeated references within the graph being converted.
            _completedIdentityCopies.Add(target, result);
        }
        else if (cacheRecent)
        {
            // Add to the real bounded ring so array and non-array conversions share one eviction timeline.
            // Its active transaction restores the previous state if the graph later fails.
            engine._recentObjectWrapperCache ??= new RecentObjectWrapperCache();
            engine._recentObjectWrapperCache.Add(target, result);
        }
    }

    public void End(object target)
    {
        if (_arrayTokens.TryGetValue(target, out var token))
        {
            _inProgressTokens.Remove(token);
        }

        _inProgress.Remove(target);
        _depth--;
    }

    public void RecordDependency(ObjectInstance parent, JsValue value)
    {
        if (value is not ObjectInstance child
            || !_snapshotTokens.TryGetValue(child, out var token))
        {
            return;
        }

        if (!_arrayDependencies.TryGetValue(parent, out var dependencies))
        {
            dependencies = new ArrayDependencies();
            _arrayDependencies.Add(parent, dependencies);
        }

        dependencies.Add(token, child);
    }

    public DependencyObservation BeginDependencyObservation()
    {
        var observation = new DependencyObservation(_dependencyObservations.Count);
        _dependencyObservations.Add(null);
        return observation;
    }

    public void ObserveDependency(ObjectInstance snapshot)
    {
        if (!_snapshotTokens.TryGetValue(snapshot, out _))
        {
            return;
        }

        for (var i = 0; i < _dependencyObservations.Count; i++)
        {
            var items = _dependencyObservations[i] ??= [];
            if (!items.Contains(snapshot))
            {
                items.Add(snapshot);
            }
        }
    }

    public void EndDependencyObservation(ObjectInstance parent, DependencyObservation observation)
    {
        var last = _dependencyObservations.Count - 1;
        if (last != observation.Depth)
        {
            Throw.InvalidOperationException("Array dependency observations must end in stack order.");
        }

        var items = _dependencyObservations[last];
        _dependencyObservations.RemoveAt(last);
        if (items is null)
        {
            return;
        }

        foreach (var snapshot in items)
        {
            RecordDependency(parent, snapshot);
        }
    }

    public bool IsCompletedByCurrentGraph(object target, ObjectInstance result)
    {
        for (var i = _completed.Count - 1; i >= 0; i--)
        {
            var copy = _completed[i];
            if (ReferenceEquals(copy.Target, target) && ReferenceEquals(copy.Result, result))
            {
                return true;
            }
        }

        return false;
    }

    public bool CanReuseCachedArray(Array source, ObjectInstance result)
    {
        if (!IsActive
            || IsCompletedByCurrentGraph(source, result))
        {
            return true;
        }

        if (!_arrayDependencies.TryGetValue(result, out _))
        {
            return true;
        }

        var pending = new List<ObjectInstance>(capacity: 4) { result };
        var visited = new HashSet<ObjectInstance> { result };
        while (pending.Count > 0)
        {
            var last = pending.Count - 1;
            var current = pending[last];
            pending.RemoveAt(last);
            if (!_arrayDependencies.TryGetValue(current, out var dependencies))
            {
                continue;
            }

            foreach (var dependency in dependencies.Items)
            {
                if (_inProgressTokens.Contains(dependency.Token))
                {
                    return false;
                }

                if (dependency.Snapshot.TryGetTarget(out var snapshot)
                    && visited.Add(snapshot))
                {
                    pending.Add(snapshot);
                }
            }
        }

        return true;
    }

    public void Publish(Engine engine)
    {
        foreach (var copy in _completed)
        {
            if (copy.TrackIdentity)
            {
                engine._objectWrapperCache ??= new ConditionalWeakTable<object, ObjectInstance>();
                engine._objectWrapperCache.TryGetValue(copy.Target, out var previous);
                _publishedIdentityCopies.Add(new PublishedIdentityCopy(copy.Target, previous));
                // The table may hold a wrapper for a different exposed view of the same object.
                engine._objectWrapperCache.Remove(copy.Target);
                engine._objectWrapperCache.Add(copy.Target, copy.Result);
            }
        }

    }

    public void CommitDiagnostics(Engine engine)
    {
        engine._arrayCopyConversions += _completed.Count;
    }

    public void DiscardCompleted()
    {
        foreach (var copy in _completed)
        {
            if (copy.TrackIdentity)
            {
                _completedIdentityCopies.Remove(copy.Target);
            }
        }

        _completed.Clear();
        _publishedIdentityCopies.Clear();
        if (_completed.Capacity > MaximumRetainedBookkeepingCapacity)
        {
            _completed.Capacity = 0;
        }

        if (_publishedIdentityCopies.Capacity > MaximumRetainedBookkeepingCapacity)
        {
            _publishedIdentityCopies.Capacity = 0;
        }

    }

    public static void Commit(in ArrayCopySavepoint savepoint)
    {
        if (savepoint.RecentCache is { } recentCache
            && savepoint.RecentTransaction is { } recentTransaction)
        {
            recentCache.Commit(in recentTransaction);
        }
    }

    public void Rollback(Engine engine, in ArrayCopySavepoint savepoint)
    {
        for (var i = _publishedIdentityCopies.Count - 1; i >= savepoint.PublishedIdentityCount; i--)
        {
            var publication = _publishedIdentityCopies[i];
            engine._objectWrapperCache!.Remove(publication.Target);
            if (publication.Previous is not null)
            {
                engine._objectWrapperCache.Add(publication.Target, publication.Previous);
            }
        }

        _publishedIdentityCopies.RemoveRange(
            savepoint.PublishedIdentityCount,
            _publishedIdentityCopies.Count - savepoint.PublishedIdentityCount);

        for (var i = _completed.Count - 1; i >= savepoint.CompletedCount; i--)
        {
            var copy = _completed[i];
            if (copy.TrackIdentity)
            {
                _completedIdentityCopies.Remove(copy.Target);
            }
        }

        if (savepoint.RecentCache is { } recentCache
            && savepoint.RecentTransaction is { } recentTransaction)
        {
            recentCache.Rollback(in recentTransaction);
            if (savepoint.CreatedRecentCache)
            {
                engine._recentObjectWrapperCache = null;
            }
        }

        _completed.RemoveRange(savepoint.CompletedCount, _completed.Count - savepoint.CompletedCount);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct CompletedArrayCopy(
        object Target,
        ObjectInstance Result,
        bool TrackIdentity);

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct PublishedIdentityCopy(object Target, ObjectInstance? Previous);

    private sealed class ArrayToken;

    private sealed class ArrayDependencies
    {
        private readonly List<ArrayDependency> _items = [];

        public IReadOnlyList<ArrayDependency> Items => _items;

        public void Add(ArrayToken token, ObjectInstance snapshot)
        {
            for (var i = 0; i < _items.Count; i++)
            {
                if (_items[i].Snapshot.TryGetTarget(out var existing)
                    && ReferenceEquals(existing, snapshot))
                {
                    return;
                }
            }

            _items.Add(new ArrayDependency(token, new WeakReference<ObjectInstance>(snapshot)));
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct ArrayDependency(
        ArrayToken Token,
        WeakReference<ObjectInstance> Snapshot);

    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct DependencyObservation(int Depth);
}

[StructLayout(LayoutKind.Auto)]
internal readonly record struct ArrayCopySavepoint(
    int CompletedCount,
    int PublishedIdentityCount,
    RecentObjectWrapperCache? RecentCache,
    RecentObjectWrapperCache.Transaction? RecentTransaction,
    bool CreatedRecentCache);

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
    private List<Mutation>? _mutations;
    private int _next;
    private int _transactionDepth;

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
        if (_transactionDepth > 0)
        {
            _mutations ??= [];
            _mutations.Add(new Mutation(index, _targets[index], _wrappers[index]));
        }

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

    public Transaction BeginTransaction()
    {
        _transactionDepth++;
        return new(_mutations?.Count ?? 0, _next);
    }

    public void Commit(in Transaction transaction)
    {
        _transactionDepth--;
        if (_transactionDepth == 0)
        {
            ReleaseMutationsIfOversized();
        }
    }

    public void Rollback(in Transaction transaction)
    {
        if (_mutations is { } mutations)
        {
            for (var i = mutations.Count - 1; i >= transaction.MutationCount; i--)
            {
                var mutation = mutations[i];
                _targets[mutation.Index] = mutation.Target;
                _wrappers[mutation.Index] = mutation.Wrapper!;
            }

            mutations.RemoveRange(transaction.MutationCount, mutations.Count - transaction.MutationCount);
        }

        _next = transaction.Next;
        _transactionDepth--;
        if (_transactionDepth == 0)
        {
            ReleaseMutationsIfOversized();
        }
    }

    private void ReleaseMutationsIfOversized()
    {
        if (_mutations is not { } mutations)
        {
            return;
        }

        if (mutations.Capacity > 1024)
        {
            _mutations = null;
        }
        else
        {
            mutations.Clear();
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct Mutation(int Index, object? Target, ObjectInstance? Wrapper);

    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct Transaction(int MutationCount, int Next);
}
