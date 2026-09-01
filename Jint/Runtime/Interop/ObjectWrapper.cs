using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop.Reflection;

#pragma warning disable IL2067
#pragma warning disable IL2072
#pragma warning disable IL2075

namespace Jint.Runtime.Interop;

/// <summary>
/// Wraps a CLR instance
/// </summary>
public class ObjectWrapper : ObjectInstance, IObjectWrapper, IEquatable<ObjectWrapper>
{
    internal readonly TypeDescriptor _typeDescriptor;
    private bool _lengthPropertyPending;

    /// <summary>
    /// Whether the host declared <see cref="Target"/>'s type immutable for the crossing
    /// (<see cref="Options.InteropOptions.ImmutableCrossingTypes"/>). Fixed for the wrapper's lifetime.
    /// </summary>
    private readonly bool _immutableCrossing;

    /// <summary>
    /// Memoized results of the dictionary-key lane, populated only while <see cref="_immutableCrossing"/> —
    /// the host's promise that the target's key set and values do not change is the whole warrant for it.
    /// <para>
    /// Deliberately a store of its own rather than the inherited <c>_properties</c>. That keeps every existing
    /// observable of the wrapper untouched: the memo is invisible to <see cref="GetOwnPropertyKeys"/> (which
    /// stays live from the target), never bumps <c>_propertiesVersion</c> so no unrelated inline cache is
    /// invalidated as keys are memoized, and stays unambiguously distinguishable from a descriptor a script
    /// actually defined — which is what lets a write evict exactly the memo and nothing else. It also cannot
    /// shadow <c>_properties</c>: a key is only memoized once that store has been shown not to hold it, and
    /// every path that can add one afterwards drops the memo first.
    /// </para>
    /// </summary>
    private PropertyDictionary? _crossingMemo;

    internal ObjectWrapper(
        Engine engine,
        object obj,
        Type? type = null)
        // Member access resolves against the wrapped CLR object, not ordinary own-property-then-prototype
        // lookup, so the prototype-method inline cache must skip this receiver and any object whose
        // prototype is a wrapper. See InternalTypes.ExoticGet. Stated through the internal constructor rather
        // than derived from the type: wrapping is hot enough that even a cached per-instance type lookup is
        // not worth paying for an answer that is fixed for the whole hierarchy.
        : base(engine, ObjectClass.Object, InternalTypes.Object | InternalTypes.ExoticGet)
    {
        Target = obj;
        ClrType = GetClrType(obj, type);
        _typeDescriptor = TypeDescriptor.Get(ClrType);

        // The promise is about instances, so it is the runtime type that is asked rather than the exposed
        // one: an object handed over under an interface view still is what it is. Costs one null check when
        // no host declared anything, and one memoized type probe when one did.
        _immutableCrossing = engine._immutableCrossingFilter?.Claims(obj.GetType()) == true;

        if (_typeDescriptor.LengthProperty is not null)
        {
            // the "length" forwarder (produced from Count or Length) is materialized lazily on first
            // own-property consultation: plain length reads are served by the ICollection fast path in
            // Get, so most wrappers never observe the descriptor itself
            _lengthPropertyPending = true;

            if (_typeDescriptor.IsArrayLike && engine.Options.Interop.AttachArrayPrototype)
            {
                // if we have array-like object, we can attach array prototype
                _prototype = engine.Intrinsics.Array.PrototypeObject;
            }
        }

        // The three members below are the only ones this constructor builds eagerly, and each is a function
        // of the realm this wrapper is being created in — the same realm ObjectInstance's constructor just
        // took this object's own prototype from, and the one ArrayLikeWrapper takes Array.prototype from a
        // few lines above. That is what an ObjectWrapper answers with: not a realm of its own, but whichever
        // one is running when it is built, which ShadowRealm.SetValue brackets deliberately (#3367).
        //
        // The public ClrFunction(Engine, string, …) constructor is the wrong one here, and not by accident:
        // it pins engine._originalIntrinsics so that a function a *host* wires up against an engine belongs
        // to the principal realm whatever was constructed since (#2893, HostClrFunctionRealmTests). Used
        // here it made these three principal-realm functions hanging off a shadow-realm object, so
        // `handle.Dispose instanceof Function` was true while `handle[Symbol.dispose] instanceof Function`
        // was false on the same object — every lazily resolved member reads engine.Realm at materialization
        // and was already right (#3365).
        var realm = engine.Realm;

        if (_typeDescriptor.IsDisposable)
        {
            SetProperty(GlobalSymbolRegistry.Dispose, new PropertyDescriptor(new ClrFunction(engine, realm, "dispose", static (thisObject, _) =>
            {
                ((thisObject as ObjectWrapper)?.Target as IDisposable)?.Dispose();
                return Undefined;
            }, 0), PropertyFlag.NonEnumerable));
        }

#if SUPPORTS_ASYNC_DISPOSE
        if (_typeDescriptor.IsAsyncDisposable)
        {
            SetProperty(GlobalSymbolRegistry.AsyncDispose, new PropertyDescriptor(new ClrFunction(engine, realm, "asyncDispose", (thisObject, _) =>
            {
                var target = ((thisObject as ObjectWrapper)?.Target as IAsyncDisposable)?.DisposeAsync();
                if (target is not null)
                {
                    return ConvertAwaitableToPromise(engine, target);
                }
                return Undefined;
            }, 0), PropertyFlag.NonEnumerable));
        }
#endif

        if (_typeDescriptor.ToJsonMethod is not null)
        {
            // Wrap the toJSON method in a ClrFunction with the expected signature for JSON.stringify
            var toJsonFunction = new ClrFunction(engine, realm, "toJSON", (thisObject, arguments) =>
            {
                var wrapper = thisObject as ObjectWrapper;
                if (wrapper is null)
                {
                    return Undefined;
                }

                try
                {
                    // Call the CLR toJSON method with no arguments (as expected by JSON.stringify)
                    var result = _typeDescriptor.ToJsonMethod.Invoke(wrapper.Target, null);
                    return FromObject(engine, result);
                }
                catch (TargetInvocationException exception)
                {
                    Throw.MeaningfulException(engine, exception);
                    return Undefined;
                }
            }, 0);

            // toJSON should be writable, configurable, and non-enumerable to match JavaScript standard
            // (e.g., Date.prototype.toJSON has these same flags)
            SetProperty("toJSON", new PropertyDescriptor(toJsonFunction, PropertyFlag.Writable | PropertyFlag.Configurable | PropertyFlag.NonEnumerable));
        }
    }

    /// <summary>
    /// Creates a new object wrapper for given object instance and exposed type.
    /// </summary>
    public static ObjectInstance Create(Engine engine, object target, Type? type = null)
    {
        if (target == null)
        {
            Throw.ArgumentNullException(nameof(target));
        }

        // STJ integration
        if (string.Equals(type?.FullName, "System.Text.Json.Nodes.JsonNode", StringComparison.Ordinal))
        {
            // we need to always expose the actual type instead of the type nodes provide
            type = target.GetType();
        }

        type ??= target.GetType();

        if (TryBuildArrayLikeWrapper(engine, target, type, out var wrapper))
        {
            return wrapper;
        }

        return new ObjectWrapper(engine, target, type);
    }

    private static readonly ConcurrentDictionary<Type, ArrayLikeWrapperFactory?> _arrayLikeWrapperResolution = new();

    private static bool TryBuildArrayLikeWrapper(
        Engine engine,
        object target,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type type,
        [NotNullWhen(true)] out ArrayLikeWrapper? result)
    {
        result = null;

        // resolved once per exposed type: reflection (interface scan + generic instantiation +
        // Activator) runs only on the first sighting, every later wrapper creation is a single
        // virtual call into the cached factory
        var factory = _arrayLikeWrapperResolution.GetOrAdd(type, static t =>
        {
#pragma warning disable IL2055
#pragma warning disable IL2070
#pragma warning disable IL3050

            Type? factoryType = null;

            // single-rank zero-based CLR arrays (T[]) get a fixed-size live wrapper; T[] implements
            // IList<T> and would otherwise flow into GenericListWrapper<T> below, whose growth paths
            // call IList<T>.Add and would leak NotSupportedException from the underlying array.
            // The MakeArrayType equality intentionally excludes multi-rank (T[,]) and non-zero-based
            // (T[*]) arrays, which keep their previous handling.
            if (t.IsArray && t.GetElementType() is { } elementType && t == elementType.MakeArrayType())
            {
                factoryType = typeof(ArrayWrapperFactory<>).MakeGenericType(elementType);
            }
            else
            {
                // The exposed type itself, ahead of its interfaces. An interface is not among its own
                // GetInterfaces() - typeof(IList<int>) yields ICollection<int>, IEnumerable<int> and
                // IEnumerable, and never IList<int> - so exposing a collection *as* one of the two
                // contracts this scan is written to recognize found nothing at all, and the caller fell
                // back to a wrapper the exposure had not named: a plain ObjectWrapper for a target that is
                // not an IList, and a target-writable ListWrapper for one that is. Both are exposures a
                // WrapObjectDelegate writes and the member lane produces for a declared IList<T> /
                // IReadOnlyList<T> property (#3421).
                factoryType = TypedFactoryTypeFor(t);

                if (factoryType is null)
                {
                    // check for generic interfaces
                    foreach (var i in t.GetInterfaces())
                    {
                        factoryType = TypedFactoryTypeFor(i);
                        if (factoryType is not null)
                        {
                            break;
                        }
                    }
                }
            }
#pragma warning restore IL3050
#pragma warning restore IL2070
#pragma warning restore IL2055

            if (factoryType is null)
            {
                return null;
            }

            // Activator.CreateInstance may fail in trimmed/AOT scenarios where the constructor
            // was removed by the linker - fall back to the non-generic ListWrapper in that case
            try
            {
                return (ArrayLikeWrapperFactory) Activator.CreateInstance(factoryType)!;
            }
            catch (MissingMethodException)
            {
                return null;
            }
        });

        // IsInstanceOfType is what keeps the factory's cast honest. Every factory casts the target to the
        // contract the exposed type named - (T[]), (IList<T>), (IReadOnlyList<T>) - and until the exposed
        // type itself was considered (#3421) a factory could only be found by scanning the target's own
        // interface set, so the cast could not fail. A host is free to hand Create a type its target does
        // not implement, and that exposure keeps the answer it has always had rather than becoming an
        // InvalidCastException out of a wrapper creation.
        if (factory is not null && type.IsInstanceOfType(target))
        {
            result = factory.Create(engine, target, type);
        }
        else if (target is IList list)
        {
            // least specific
            result = new ListWrapper(engine, list, type);
        }
        else if (engine.Options.Interop.EnumerableConversion == EnumerableConversionMode.Snapshot
                 && target is IEnumerable enumerable
                 && target is not string)
        {
            // Everything above had a count and an indexer to build a view from. A sequence that has neither -
            // a LINQ iterator, a generator method's result - can only be given one by enumerating it, which
            // the host has to ask for because it is eager and one-shot (#2987). Deliberately narrow: the type
            // descriptor must report neither array-like nor dictionary, so an ICollection<T> such as
            // HashSet<T> keeps its live exposure and a Dictionary<K, V> is not mistaken for a sequence of
            // pairs merely because it enumerates as one.
            var descriptor = TypeDescriptor.Get(type);
            if (!descriptor.IsArrayLike && !descriptor.IsDictionary)
            {
                result = ResolveEnumerableSnapshotFactory(type).Create(engine, enumerable);
            }
        }

        return result is not null;
    }

    /// <summary>
    /// The closed factory type for one contract, or <see langword="null"/> when it names neither of the two
    /// the typed wrappers are written for. Shared by the exposed type and its interfaces, which is what
    /// makes the two answer identically.
    /// </summary>
    private static Type? TypedFactoryTypeFor(Type contract)
    {
#pragma warning disable IL2055
#pragma warning disable IL3050

        if (!contract.IsGenericType)
        {
            return null;
        }

        var definition = contract.GetGenericTypeDefinition();

        if (definition == typeof(IList<>))
        {
            return typeof(GenericListWrapperFactory<>).MakeGenericType(contract.GenericTypeArguments[0]);
        }

        if (definition == typeof(IReadOnlyList<>))
        {
            return typeof(ReadOnlyListWrapperFactory<>).MakeGenericType(contract.GenericTypeArguments[0]);
        }

        return null;

#pragma warning restore IL3050
#pragma warning restore IL2055
    }

    private static readonly ConcurrentDictionary<Type, EnumerableSnapshotFactory> _enumerableSnapshotResolution = new();

    private static EnumerableSnapshotFactory ResolveEnumerableSnapshotFactory(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type type)
    {
        return _enumerableSnapshotResolution.GetOrAdd(type, static t =>
        {
#pragma warning disable IL2055
#pragma warning disable IL2070
#pragma warning disable IL3050
            foreach (var i in t.GetInterfaces())
            {
                if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    try
                    {
                        var factoryType = typeof(EnumerableSnapshotFactory<>).MakeGenericType(i.GenericTypeArguments[0]);
                        return (EnumerableSnapshotFactory) Activator.CreateInstance(factoryType)!;
                    }
                    catch (MissingMethodException)
                    {
                        // trimmed/AOT: the closed factory was removed, snapshot as objects instead
                        break;
                    }
                }
            }
#pragma warning restore IL3050
#pragma warning restore IL2070
#pragma warning restore IL2055

            // a type implementing IEnumerable<T> more than once takes the first one, the same arbitrary
            // choice every other interface scan on this path makes
            return ObjectEnumerableSnapshotFactory.Instance;
        });
    }

    public object Target { get; }
    public Type ClrType { get; }

    internal override bool IsArrayLike => _typeDescriptor.IsArrayLike;

    /// <summary>
    /// Whether reading indices <c>0..length-1</c> off this wrapper produces the target's elements.
    /// </summary>
    /// <remarks>
    /// <see cref="TypeDescriptor.IsArrayLike"/> answers the weaker question of whether the target has a
    /// <c>Count</c>, and <see cref="ICollection"/>, <see cref="ICollection{T}"/> and
    /// <see cref="IReadOnlyCollection{T}"/> are all count-and-enumerate contracts with no index in them:
    /// <see cref="Queue{T}"/>, <see cref="Stack{T}"/>, <see cref="LinkedList{T}"/> and
    /// <see cref="HashSet{T}"/> are every one of them array-like with no element at index 0. An indexed lane
    /// must gate on this rather than on array-likeness (#3302).
    /// </remarks>
    internal virtual bool HasIndexedElements => (_typeDescriptor.IsIntegerIndexed || Target is IList) && IndexedElementsExposed;

    /// <summary>
    /// Whether the host's <see cref="TypeResolver.MemberFilter"/> admits the indexer an indexed lane would
    /// reach <see cref="Target"/>'s elements through. A filter that hides a member hides it from every lane,
    /// and an element lane that answers from a view rather than from a resolved accessor is the one place
    /// that has to ask on its own behalf (#3558).
    /// </summary>
    private protected bool IndexedElementsExposed
        => _engine.Options.Interop.TypeResolver.ExposesIndexedElements(_engine, ClrType, _typeDescriptor.IntegerIndexerProperty);

    // A wrapper's Symbol.iterator is never the array iterator — it enumerates the CLR target — so the
    // index-reading fast path this enables (array destructuring) is indistinguishable from running that
    // iterator only where index reads reproduce what enumeration yields. For a countable but non-indexable
    // collection they do not: [...queue] yields the elements while every index reads undefined.
    internal override bool HasOriginalIterator => IsArrayLike && HasIndexedElements;

    internal override bool IsIntegerIndexedArray => _typeDescriptor.IsIntegerIndexed;

    /// <summary>
    /// What a property key names on a wrapped indexed collection. Every element lane is keyed on the
    /// <em>index</em> rather than on how script spelled it: <c>x[3]</c> and <c>x["3"]</c> are one property
    /// key, because <see href="https://tc39.es/ecma262/#sec-topropertykey">ToPropertyKey</see> turns both
    /// into the String <c>"3"</c>. Answering the two spellings from different lanes is answering one
    /// question twice, and the lane that used to serve the string spelling was the reflected indexer, which
    /// reaches the collection with whatever index it parses out of the key (#3384).
    /// </summary>
    /// <remarks>
    /// It lives on the base class rather than on <see cref="ArrayLikeWrapper"/> because a plain wrapper over
    /// a bounded collection asks the same question of the same keys — it simply has no array-like view to
    /// answer it (#3422). A second definition of "index-shaped key" is the thing that would drift.
    /// </remarks>
    private protected enum ElementKey
    {
        /// <summary>Not a position: a member name, a symbol, a fractional number, or any string key of a dictionary-shaped target.</summary>
        None,

        /// <summary>A position the target could address. May be at or past the collection's current count.</summary>
        Position,

        /// <summary>Index-shaped but never a position: negative, non-canonical (<c>"08"</c>, <c>"+3"</c>), or past what the target can address.</summary>
        OutOfBand,
    }

    /// <summary>
    /// The highest position an element lane will address, one below <see cref="int.MaxValue"/> so that
    /// growing to <c>index + 1</c> cannot overflow.
    /// </summary>
    private protected const int MaxPosition = int.MaxValue - 1;

    private protected ElementKey ClassifyElementKey(JsValue property, out int index)
    {
        index = 0;

        if (property is JsNumber number)
        {
            var value = number._value;
            if (!TypeConverter.IsIntegralNumber(value))
            {
                return ElementKey.None;
            }

            if (value < 0 || value > MaxPosition)
            {
                return ElementKey.OutOfBand;
            }

            index = (int) value;
            return ElementKey.Position;
        }

        // a symbol names no position, and a dictionary-shaped target (e.g. Newtonsoft's JObject: both
        // IDictionary<string,_> and IList<_>) answers a string key from its own keys rather than by index
        if (property is not JsString jsString || _typeDescriptor.IsDictionary)
        {
            return ElementKey.None;
        }

        var member = jsString.ToString();
        var parsed = ArrayInstance.ParseArrayIndex(member);
        if (parsed != uint.MaxValue)
        {
            if (parsed > MaxPosition)
            {
                return ElementKey.OutOfBand;
            }

            index = (int) parsed;
            return ElementKey.Position;
        }

        // One character rules out every ordinary member name that reaches a wrapper — "length", "Count",
        // "push" — which would otherwise pay a whole long.TryParse only to be rejected by it.
        if (member.Length == 0 || !IsIntegerShapedStart(member[0]))
        {
            return ElementKey.None;
        }

        // an integer-shaped but non-canonical key ("-1", "08") is not a position either, and must not fall
        // through to the reflected indexer, which parses one out of it and reaches the collection with an
        // index the collection rejects
        return long.TryParse(member, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            ? ElementKey.OutOfBand
            : ElementKey.None;
    }

    /// <summary>
    /// Whether <paramref name="c"/> can begin something <see cref="NumberStyles.Integer"/> parses: a digit,
    /// a sign, or the leading white space it tolerates.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsIntegerShapedStart(char c)
        => (uint) (c - '0') <= 9 || c == '-' || c == '+' || char.IsWhiteSpace(c);

    /// <summary>
    /// Whether <paramref name="property"/> spells a position <see cref="Target"/> cannot address, resolving
    /// the member lane itself. For callers that have already resolved it, see the overload below.
    /// </summary>
    private bool IsUnaddressablePosition(JsValue property)
    {
        if (!_typeDescriptor.IsArrayLike)
        {
            return false;
        }

        var key = ClassifyElementKey(property, out var index);
        return key != ElementKey.None
               && IsUnaddressablePosition(key, index, ResolveMemberAccessor(property.ToString(), MemberResolutionRequirement.None));
    }

    /// <summary>
    /// Whether <paramref name="property"/> spells a position <see cref="Target"/> cannot address, on a
    /// wrapper whose element lane is <paramref name="accessor"/>.
    /// </summary>
    /// <remarks>
    /// Two facts have to hold at once and neither is enough alone. The target must be <em>bounded</em>:
    /// <see cref="TypeDescriptor.IsArrayLike"/> means it has a <c>Count</c> and is not a dictionary, and a
    /// dictionary is exactly where a key outside what it holds is a legitimate add — <c>d[99] = "x"</c> on a
    /// <c>Dictionary&lt;int, string&gt;</c> must keep working. And the member being resolved must be an
    /// indexer keyed by an integer, so that a string-keyed indexer on an array-like target — a
    /// <c>NameValueCollection</c> asked for <c>x["3"]</c> — goes on answering for its own key.
    /// </remarks>
    private bool IsUnaddressablePosition(JsValue property, ReflectionAccessor accessor)
    {
        if (!_typeDescriptor.IsArrayLike)
        {
            return false;
        }

        var key = ClassifyElementKey(property, out var index);
        return key != ElementKey.None && IsUnaddressablePosition(key, index, accessor);
    }

    private bool IsUnaddressablePosition(ElementKey key, int index, ReflectionAccessor accessor)
    {
        if (accessor is not IndexerAccessor indexer
            || !IsIntegerIndexParameter(indexer.FirstIndexParameter.ParameterType))
        {
            return false;
        }

        // a negative, non-canonical or unaddressable index is a position no collection can hold, so it
        // needs no count read to be refused
        return key == ElementKey.OutOfBand
               || (TryGetTargetCount(out var count) && (uint) index >= (uint) count);
    }

    /// <summary>
    /// Whether an indexer parameter of this type addresses a position. Anything else — a
    /// <see cref="string"/>, an enum, a host key type — names something that is not one, and is none of the
    /// bound check's business.
    /// </summary>
    internal static bool IsIntegerIndexParameter(Type parameterType)
        => parameterType == typeof(int)
           || parameterType == typeof(long)
           || parameterType == typeof(short)
           || parameterType == typeof(sbyte)
           || parameterType == typeof(uint)
           || parameterType == typeof(ulong)
           || parameterType == typeof(ushort)
           || parameterType == typeof(byte);

    /// <summary>
    /// The number of positions <see cref="Target"/> currently has. Only ever asked of an array-like target,
    /// which is what says there is a count to read at all; <see langword="false"/> means it could not be
    /// read, and the caller then refuses nothing.
    /// </summary>
    private bool TryGetTargetCount(out int count)
    {
        if (Target is ICollection collection)
        {
            count = collection.Count;
            _engine.CheckAmortizedConstraintsAtHostBoundary();
            return true;
        }

        // ICollection<T> and IReadOnlyCollection<T> carry a Count with no non-generic ICollection behind it.
        // LengthProperty is the same member the wrapper's own "length" forwarder reads.
        if (_typeDescriptor.LengthProperty?.GetValue(Target) is int length)
        {
            _engine.CheckAmortizedConstraintsAtHostBoundary();
            count = length;
            return true;
        }

        count = 0;
        return false;
    }

    public override bool Set(JsValue property, JsValue value, JsValue receiver)
    {
        // check if we can take shortcuts for empty object, no need to generate properties
        if (property is JsString stringKey)
        {
            var member = stringKey.ToString();

            // A write must never be answered from the memo afterwards. The host promised the target does
            // not change, so in principle nothing here can invalidate it - this is insurance for the host
            // that was wrong, not a licence to be, and it costs one probe on a path that is about to run
            // a CLR write anyway. Only the memo is dropped: _properties is untouched, so everything below
            // (the accessor fast path, freeze, an expando added by a previous extend) behaves exactly as
            // it did before the memo existed.
            _crossingMemo?.Remove(member);

            if (_lengthPropertyPending && string.Equals(member, "length", StringComparison.Ordinal))
            {
                MaterializeLengthProperty();
            }
            if (_properties is null || !_properties.ContainsKey(member))
            {
                // can try utilize fast path
                var accessor = ResolveMemberAccessor(member, MemberResolutionRequirement.Writable);

                if (IsUnaddressablePosition(property, accessor))
                {
                    // The indexer below takes the index straight to the collection, which is where the
                    // CLR's own ArgumentOutOfRangeException came from - invisible to a script try/catch and
                    // to a host catch (JavaScriptException) alike. There is no position to write to, so the
                    // answer owed is the ordinary [[Set]] refusal: silent outside strict mode, a TypeError
                    // inside it (#3422).
                    return false;
                }

                if (ReferenceEquals(accessor, ConstantValueAccessor.NullAccessor))
                {
                    // The writable-filtered resolution answers with the same NullAccessor for "no such
                    // member" and for "the member exists but nothing writable answers for it", so probe
                    // again without the requirement before treating this as an unknown name. A member
                    // that exists but cannot be written must behave like a non-writable ordinary
                    // property - [[Set]] returns false, which PutValue turns into a silent no-op in
                    // sloppy mode and a TypeError in strict mode - rather than being shadowed by a
                    // JS-side own property that hides the CLR member from every later read.
                    // https://tc39.es/ecma262/#sec-ordinarysetwithowndescriptor
                    // https://tc39.es/ecma262/#sec-putvalue
                    if (!ReferenceEquals(ResolveMemberAccessor(member, MemberResolutionRequirement.None), ConstantValueAccessor.NullAccessor))
                    {
                        return false;
                    }

                    if (_engine.Options.Interop.ThrowOnUnresolvedMember)
                    {
                        throw new MissingMemberException($"Cannot access property '{member}' on type '{ClrType.FullName}");
                    }

                    // there's no such property, but we can allow extending by calling base
                    // which will add properties, this allows for example JS class to extend a CLR type
                    return base.Set(property, value, receiver);
                }

                // CanPut logic
                if (!accessor.Writable || !_engine.Options.Interop.AllowWrite)
                {
                    return false;
                }

                if (!Extensible)
                {
                    // object is frozen/sealed, cannot add new properties
                    return false;
                }

                accessor.SetValue(_engine, Target, member, value);
                return true;
            }
        }
        else if (property is JsSymbol jsSymbol)
        {
            // symbol addition will never hit any known CLR object properties, so if write is allowed, allow writing symbols too
            if (_engine.Options.Interop.AllowWrite)
            {
                return base.Set(jsSymbol, value, receiver);
            }

            return false;
        }
        else if (ReferenceEquals(receiver, this) && _typeDescriptor.IsNonStringKeyedGenericDictionary)
        {
            // non-string-keyed CLR generic dictionary (e.g. Dictionary<TestModel, string>).
            // Matches the receiver gate in Get: when [[Set]] arrives via Proxy/Reflect.set with a
            // different receiver, fall through to the spec-compliant slow path instead of mutating
            // the underlying dict directly.
            if (!_engine.Options.Interop.AllowWrite || !Extensible)
            {
                return false;
            }

            var keyType = _typeDescriptor.GenericDictionaryKeyType!;
            var valueType = _typeDescriptor.GenericDictionaryValueType!;
            if (!TryConvertJsValueToDictionaryKey(property, keyType, out var clrKey)
                || !TryConvertJsValueToDictionaryValue(value, valueType, out var clrValue))
            {
                return false;
            }

            var written = _typeDescriptor.TrySetDictionaryValue(Target, clrKey!, clrValue);
            _engine.CheckAmortizedConstraintsAtHostBoundary();
            return written;
        }

        // A number key and its string spelling are one property key, and the lane below resolves the same
        // reflected indexer by member name - so the bound the string lane above just applied has to be
        // applied here too, before SetSlow reaches it (#3422).
        if (!property.IsSymbol() && IsUnaddressablePosition(property))
        {
            return false;
        }

        return SetSlow(property, value);
    }

    /// <summary>
    /// Resolves the accessor the write lane in <see cref="Set"/> consults, under the given
    /// <paramref name="requirement"/>. Falls back to the runtime type when the exposed type differs,
    /// mirroring <see cref="GetOwnProperty(JsValue, MemberResolutionRequirement, bool)"/>.
    /// </summary>
    private ReflectionAccessor ResolveMemberAccessor(string member, MemberResolutionRequirement requirement)
    {
        var typeResolver = _engine.Options.Interop.TypeResolver;
        var accessor = typeResolver.GetAccessor(_engine, ClrType, member, requirement, throwOnError: false);

        var actualType = Target.GetType();
        if (ClrType == actualType)
        {
            return accessor;
        }

        // When the declared type differs from the actual runtime type:
        // If only an indexer was found, check if the runtime type has a direct property/field/method
        // that should take precedence over the indexer
        if (accessor is IndexerAccessor)
        {
            var runtimeAccessor = typeResolver.GetAccessor(_engine, actualType, member, requirement, throwOnError: false);
            if (runtimeAccessor is not IndexerAccessor && runtimeAccessor != ConstantValueAccessor.NullAccessor)
            {
                accessor = runtimeAccessor;
            }
        }
        else if (ReferenceEquals(accessor, ConstantValueAccessor.NullAccessor))
        {
            accessor = typeResolver.GetAccessor(_engine, actualType, member, requirement, throwOnError: false);
        }

        return accessor;
    }

    private bool SetSlow(JsValue property, JsValue value)
    {
        if (!CanPut(property))
        {
            return false;
        }

        var ownDesc = GetOwnProperty(property);
        ownDesc.Value = value;
        return true;
    }

    public override bool DefineOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        // whatever this defines lands in _properties, which is consulted ahead of the dictionary lane from
        // here on, so the memo must not answer for it any more
        DropCrossingMemo();

        if (_typeDescriptor.IsStringKeyedGenericDictionary && property.IsString() && !TryGetProperty(property, out _))
        {
            // For dictionary-backed objects, GetOwnProperty returns fresh descriptors that are not stored
            // in _properties. ValidateAndApplyPropertyDescriptor mutates descriptors in-place, so mutations
            // (e.g. from Object.freeze/seal) would be lost without pre-storing the descriptor.
            var current = GetOwnProperty(property);
            if (current != PropertyDescriptor.Undefined)
            {
                SetProperty(property, current);
            }
        }

        return base.DefineOwnProperty(property, desc);
    }

    public override object ToObject() => Target;

    protected internal override void SetOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        // same reason as DefineOwnProperty: a descriptor in _properties outranks the memo from now on
        DropCrossingMemo();
        base.SetOwnProperty(property, desc);
    }

    public override void RemoveOwnProperty(JsValue property)
    {
        if (property is JsString removedKey)
        {
            _crossingMemo?.Remove(removedKey.ToString());
        }

        if (_lengthPropertyPending && CommonProperties.Length.Equals(property))
        {
            // an explicit removal of the not-yet-materialized forwarder must behave like removing the
            // eagerly-created one did: the property is gone and does not come back
            _lengthPropertyPending = false;
        }

        if (_engine.Options.Interop.AllowWrite)
        {
            if (property is JsString jsString && _typeDescriptor.IsStringKeyedGenericDictionary)
            {
                _typeDescriptor.TryRemoveDictionaryValue(Target, jsString.ToString());
                _engine.CheckAmortizedConstraintsAtHostBoundary();
            }
            else if (!property.IsString()
                && !property.IsSymbol()
                && _typeDescriptor.IsNonStringKeyedGenericDictionary
                && TryConvertJsValueToDictionaryKey(property, _typeDescriptor.GenericDictionaryKeyType!, out var clrKey))
            {
                _typeDescriptor.TryRemoveDictionaryValue(Target, clrKey!);
                _engine.CheckAmortizedConstraintsAtHostBoundary();
            }
        }

        // also remove from _properties cache to avoid stale entries
        base.RemoveOwnProperty(property);
    }

    public override bool HasProperty(JsValue property)
    {
        if (!property.IsString()
            && !property.IsSymbol()
            && _typeDescriptor.IsNonStringKeyedGenericDictionary
            && TryConvertJsValueToDictionaryKey(property, _typeDescriptor.GenericDictionaryKeyType!, out var clrKey))
        {
            // Prototype chain is intentionally skipped: non-string non-symbol keys can't resolve
            // to Object.prototype members (which are all string/symbol-keyed). Same rationale as Get.
            var contains = _typeDescriptor.ContainsDictionaryKey(Target, clrKey!);
            _engine.CheckAmortizedConstraintsAtHostBoundary();
            return contains;
        }

        return base.HasProperty(property);
    }

    private bool TryConvertJsValueToDictionaryKey(JsValue property, Type keyType, out object? key)
    {
        var raw = property.ToObject();
        if (raw is null)
        {
            // standard Dictionary<,> throws ArgumentNullException on null keys; bail before invoking
            key = null;
            return false;
        }

        if (keyType.IsInstanceOfType(raw))
        {
            key = raw;
            return true;
        }
        return _engine.TypeConverter.TryConvert(raw, keyType, CultureInfo.InvariantCulture, out key);
    }

    private bool TryConvertJsValueToDictionaryValue(JsValue value, Type valueType, out object? converted)
    {
        // Pass the JsValue through only for an exact JsValue target. A broader IsAssignableFrom check
        // would also match Dictionary<_, object>, where callers expect the unwrapped CLR value.
        if (valueType == typeof(JsValue))
        {
            converted = value;
            return true;
        }

        var raw = value.ToObject();
        if (raw is null)
        {
            if (!valueType.IsValueType || Nullable.GetUnderlyingType(valueType) is not null)
            {
                converted = null;
                return true;
            }
            converted = null;
            return false;
        }

        if (valueType.IsInstanceOfType(raw))
        {
            converted = raw;
            return true;
        }

        return _engine.TypeConverter.TryConvert(raw, valueType, CultureInfo.InvariantCulture, out converted);
    }

    public override JsValue Get(JsValue property, JsValue receiver)
    {
        // check fast path before producing properties
        if (ReferenceEquals(receiver, this) && property.IsString())
        {
            // try some fast paths
            if (!_typeDescriptor.IsDictionary)
            {
                if (Target is ICollection c && CommonProperties.Length.Equals(property))
                {
                    var count = c.Count;
                    _engine.CheckAmortizedConstraintsAtHostBoundary();
                    return JsNumber.Create(count);
                }
            }
            else
            {
                if (_typeDescriptor.IsStringKeyedGenericDictionary)
                {
                    var member = property.ToString();

                    // Immutability promise: a memoized key answers with no dictionary probe, no conversion
                    // and no host boundary crossing at all - there is no host code left to run.
                    if (_crossingMemo is not null && _crossingMemo.TryGetValue(member, out var memoized))
                    {
                        return UnwrapJsValue(memoized, receiver);
                    }

                    var found = _typeDescriptor.TryGetDictionaryValue(Target, member, out var value);
                    if (found)
                    {
                        // the miss path needs no check here: it falls through to GetOwnProperty,
                        // whose dictionary lane re-probes and checks
                        _engine.CheckAmortizedConstraintsAtHostBoundary();
                        // Check stored properties first - frozen/sealed objects have descriptors in _properties
                        // that must be respected to return the same (frozen) instance
                        if (TryGetProperty(property, out var stored))
                        {
                            return UnwrapJsValue(stored, receiver);
                        }

                        var converted = FromObject(_engine, value);
                        if (_immutableCrossing)
                        {
                            // reached only once _properties has been shown not to hold the key, so the memo
                            // can never shadow a descriptor a script defined
                            MemoizeDictionaryValue(member, converted);
                        }

                        return converted;
                    }
                }
            }
        }
        else if (ReferenceEquals(receiver, this)
            && _typeDescriptor.IsNonStringKeyedGenericDictionary
            && !property.IsSymbol()
            && !property.IsString()
            && TryConvertJsValueToDictionaryKey(property, _typeDescriptor.GenericDictionaryKeyType!, out var clrKey))
        {
            // Prototype chain is intentionally skipped on miss: non-string non-symbol keys can't
            // resolve to Object.prototype members (which are all string/symbol-keyed).
            var found = _typeDescriptor.TryGetDictionaryValue(Target, clrKey!, out var raw);
            _engine.CheckAmortizedConstraintsAtHostBoundary();
            return found ? FromObject(_engine, raw) : Undefined;
        }

        // slow path requires us to create a property descriptor that might get cached or not
        // suppress ThrowOnUnresolvedMember here so we can fall back to the prototype chain
        // (e.g. valueOf/toString from Object.prototype during implicit coercion)
        var desc = GetOwnProperty(property, MemberResolutionRequirement.Readable, throwOnError: false);
        if (desc != PropertyDescriptor.Undefined)
        {
            return UnwrapJsValue(desc, receiver);
        }

        var protoResult = Prototype?.Get(property, receiver) ?? Undefined;
        if (protoResult.IsUndefined()
            && property is JsString
            && !_typeDescriptor.IsDictionary
            && _engine.Options.Interop.ThrowOnUnresolvedMember
            // an index outside a bounded collection is a hole, not a member the host forgot to declare:
            // GetOwnProperty refused it rather than failing to resolve it (#3422)
            && !IsUnaddressablePosition(property))
        {
            throw new MissingMemberException($"Cannot access property '{property}' on type '{ClrType.FullName}");
        }

        return protoResult;
    }

    public override List<JsValue> GetOwnPropertyKeys(Types types = Types.Empty | Types.String | Types.Symbol)
    {
        return [.. EnumerateOwnPropertyKeys(types)];
    }

    public override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
    {
        foreach (var key in EnumerateOwnPropertyKeys(Types.String | Types.Symbol))
        {
            yield return new KeyValuePair<JsValue, PropertyDescriptor>(key, GetOwnProperty(key));
        }
    }

    private IEnumerable<JsValue> EnumerateOwnPropertyKeys(Types types)
    {
        // prefer object order, add possible other properties after
        var includeStrings = (types & Types.String) != Types.Empty;

        if (includeStrings)
        {
            var customKeys = _engine.Options.Interop.ObjectWrapperReportedPropertyKeys(_engine, Target);
            if (customKeys is not null)
            {
                // each step pulls from the user-supplied sequence
                foreach (var key in customKeys)
                {
                    _engine.CheckAmortizedConstraintsAtHostBoundary();
                    yield return key;
                }
                yield break; // non-null replaces the default key set
            }
        }

        if (includeStrings && _typeDescriptor.IsStringKeyedGenericDictionary) // expando object for instance
        {
            var keys = (ICollection<string>) _typeDescriptor.KeysAccessor!.GetValue(Target)!;
            _engine.CheckAmortizedConstraintsAtHostBoundary();
            // each step pulls from the user dictionary's key enumerator
            foreach (var key in keys)
            {
                _engine.CheckAmortizedConstraintsAtHostBoundary();
                yield return JsString.Create(key);
            }
        }
        else if (includeStrings && Target is IDictionary dictionary)
        {
            // we take values exposed as dictionary keys only; each step pulls from the user enumerator
            foreach (var key in dictionary.Keys)
            {
                _engine.CheckAmortizedConstraintsAtHostBoundary();
                object? stringKey = key as string;
                if (stringKey is not null
                    || _engine.TypeConverter.TryConvert(key, typeof(string), CultureInfo.InvariantCulture, out stringKey))
                {
                    yield return JsString.Create((string) stringKey!);
                }
            }
        }
        else if (includeStrings && this is ArrayLikeWrapper arrayLike)
        {
            // array-like views enumerate like JS arrays: index keys, not reflected CLR member names —
            // Object.keys / for-in / spread over a wrapped array or list must yield "0".."n-1"
            // (members like Length or Count stay accessible, they just don't enumerate). Dictionary-shaped
            // targets (e.g. Newtonsoft's JObject, which is both IDictionary<string,_> and IList<_>)
            // are handled by the dictionary branches above. A view whose indexer the host's member filter
            // rejects has no element properties to report, which is the answer Object.keys already gave -
            // every key it yielded was dropped again by the enumerability probe (#3558).
            var length = arrayLike.HasIndexedElements ? arrayLike.Length : 0;
            for (var i = 0; i < length; i++)
            {
                yield return JsString.Create(i);
            }
        }
        else if (includeStrings)
        {
            var interopOptions = _engine.Options.Interop;

            // we take properties, fields and methods
            if ((interopOptions.ObjectWrapperReportedMemberTypes & MemberTypes.Property) == MemberTypes.Property)
            {
                foreach (var p in ClrType.GetProperties(interopOptions.ObjectWrapperReportedPropertyBindingFlags))
                {
                    if (!interopOptions.TypeResolver.Filter(_engine, ClrType, p))
                    {
                        continue;
                    }

                    var indexParameters = p.GetIndexParameters();
                    if (indexParameters.Length == 0)
                    {
                        yield return JsString.Create(p.Name);
                    }
                }
            }

            if ((interopOptions.ObjectWrapperReportedMemberTypes & MemberTypes.Field) == MemberTypes.Field)
            {
                foreach (var f in ClrType.GetFields(interopOptions.ObjectWrapperReportedFieldBindingFlags))
                {
                    if (!interopOptions.TypeResolver.Filter(_engine, ClrType, f))
                    {
                        continue;
                    }

                    yield return JsString.Create(f.Name);
                }
            }

            if ((interopOptions.ObjectWrapperReportedMemberTypes & MemberTypes.Method) == MemberTypes.Method)
            {
                foreach (var m in ClrType.GetMethods(interopOptions.ObjectWrapperReportedMethodBindingFlags))
                {
                    // we won't report anything from base object as it would usually not be something to expect from JS perspective
                    if (m.DeclaringType == typeof(object) || m.IsSpecialName || !interopOptions.TypeResolver.Filter(_engine, ClrType, m))
                    {
                        continue;
                    }

                    yield return JsString.Create(m.Name);
                }
            }
        }
    }

    /// <summary>
    /// Existence and enumerability of a string-keyed generic dictionary's key, answered by the target's own
    /// <c>ContainsKey</c> instead of by building the descriptor <see cref="GetOwnProperty(JsValue)"/> would.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A dictionary member is the one kind this wrapper never caches: <see cref="GetOwnProperty(JsValue)"/> stores the
    /// descriptor it resolves for a reflected member in <c>_properties</c>, so a second question about that
    /// name is a bag lookup, but it deliberately does not for a dictionary key — the dictionary can change
    /// under the wrapper. So every existence question re-ran the whole read: <c>TryGetValue</c>, then
    /// <c>FromObject</c> on the value — which for an object value builds a whole <see cref="ObjectWrapper"/>
    /// of its own — then a <see cref="PropertyDescriptor"/> around it, and then discarded both, purely to
    /// learn that the key is there. <c>Object.keys</c> over a wrapped document paid that once per key, and
    /// <c>for..in</c>, <c>in</c>, <c>hasOwnProperty</c>, <c>Object.assign</c>, spread and
    /// <c>JSON.stringify</c> all reach it through the same probe.
    /// </para>
    /// <para>
    /// The lane only ever <em>answers</em>; it never decides a key is missing. A <see langword="false"/> from
    /// <c>ContainsKey</c> falls through to the descriptor path, because a dictionary wrapper still resolves
    /// CLR members (<c>Count</c> and friends) for names the dictionary does not carry, and that arm is not
    /// this method's to reproduce. So the only way the probe and <see cref="GetOwnProperty(JsValue)"/> can disagree is
    /// a target whose <c>ContainsKey</c> and <c>TryGetValue</c> disagree with each other, which the compiled
    /// dictionary lanes already trust everywhere else.
    /// </para>
    /// <para>
    /// The three things that outrank a dictionary key are checked in <see cref="GetOwnProperty(JsValue)"/>'s own
    /// order: a stored descriptor (<c>Object.freeze</c>, <c>Object.defineProperty</c>, a host
    /// <c>SetOwnProperty</c>, or the memoized descriptor an immutability promise put in <c>_properties</c>'
    /// place), the pending <c>length</c> forwarder, and a symbol key. The crossing memo needs no check of its
    /// own — it holds descriptors for keys the dictionary has, with the flags
    /// <see cref="DictionaryMemberFlags"/> gives every dictionary member, so it can only agree with this
    /// answer.
    /// </para>
    /// </remarks>
    protected internal override OwnPropertyProbe ProbeOwnProperty(JsValue property)
    {
        if (property is JsString jsString
            && _typeDescriptor.IsStringKeyedGenericDictionary
            && _typeDescriptor.CanTestDictionaryKey
            && !(_lengthPropertyPending && CommonProperties.Length.Equals(property))
            && !TryGetProperty(property, out _))
        {
            var contains = _typeDescriptor.ContainsDictionaryKey(Target, jsString.ToString());
            _engine.CheckAmortizedConstraintsAtHostBoundary();
            if (contains)
            {
                // DictionaryMemberFlags is Enumerable for every key; only configurability varies.
                return OwnPropertyProbe.Enumerable;
            }
        }

        return base.ProbeOwnProperty(property);
    }

    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        // we do not know if we need to read or write
        return GetOwnProperty(property, MemberResolutionRequirement.None);
    }

    private PropertyDescriptor GetOwnProperty(JsValue property, MemberResolutionRequirement requirement, bool throwOnError = true)
    {
        if (TryGetProperty(property, out var x))
        {
            return x;
        }

        if (_lengthPropertyPending && CommonProperties.Length.Equals(property))
        {
            return MaterializeLengthProperty();
        }

        // if we have array-like or dictionary or expando, we can provide iterator
        if (property.IsSymbol())
        {
            if (property == GlobalSymbolRegistry.Iterator && _typeDescriptor.Iterable)
            {
                var iteratorFunction = new ClrFunction(
                    Engine,
                    "iterator",
                    Iterator,
                    1,
                    PropertyFlag.Configurable);

                var iteratorProperty = new PropertyDescriptor(iteratorFunction, PropertyFlag.Configurable | PropertyFlag.Writable);
                SetProperty(GlobalSymbolRegistry.Iterator, iteratorProperty);
                return iteratorProperty;
            }

            // not that safe
            return PropertyDescriptor.Undefined;
        }

        if (!property.IsString() && _typeDescriptor.IsNonStringKeyedGenericDictionary)
        {
            // non-string-keyed CLR generic dictionary — resolve via underlying CLR key, not string
            if (TryConvertJsValueToDictionaryKey(property, _typeDescriptor.GenericDictionaryKeyType!, out var clrKey))
            {
                var found = _typeDescriptor.TryGetDictionaryValue(Target, clrKey!, out var raw);
                _engine.CheckAmortizedConstraintsAtHostBoundary();
                if (found)
                {
                    var flags = PropertyFlag.Enumerable;
                    if (_engine.Options.Interop.AllowWrite)
                    {
                        flags |= PropertyFlag.Configurable;
                    }
                    return new PropertyDescriptor(FromObject(_engine, raw), flags);
                }
            }
            return PropertyDescriptor.Undefined;
        }

        var member = property.ToString();

        // if type is dictionary, we cannot enumerate anything other than keys
        // and we cannot store accessors as dictionary can change dynamically

        var isDictionary = _typeDescriptor.IsStringKeyedGenericDictionary;
        if (isDictionary)
        {
            // see the matching lane in Get: under the immutability promise the memoized descriptor is the
            // answer, and it is the same instance every read, so this path stops allocating one per access
            if (_crossingMemo is not null && _crossingMemo.TryGetValue(member, out var memoized))
            {
                return memoized;
            }

            var found = _typeDescriptor.TryGetDictionaryValue(Target, member, out var value);
            _engine.CheckAmortizedConstraintsAtHostBoundary();
            if (found)
            {
                var converted = FromObject(_engine, value);
                if (_immutableCrossing)
                {
                    return MemoizeDictionaryValue(member, converted);
                }

                return new PropertyDescriptor(converted, DictionaryMemberFlags());
            }
        }

        if (!isDictionary
            && _engine.Options.Interop.PreferJsPrototypeMethods
            && _prototype is not null
            && !ReferenceEquals(_prototype, _engine.Realm.Intrinsics.Object.PrototypeObject)
            && _prototype.Get(property, this) is { } protoValue
            && protoValue.IsCallable)
        {
            // Let outer Get fall through to the attached prototype (Array.prototype, etc.)
            // rather than dispatching to a same-named CLR method whose semantics may differ.
            return PropertyDescriptor.Undefined;
        }

        var result = Engine.Options.Interop.MemberAccessor(Engine, Target, member);
        Engine.CheckAmortizedConstraintsAtHostBoundary();
        if (result is not null)
        {
            return new PropertyDescriptor(result, PropertyFlag.OnlyEnumerable);
        }

        var accessor = _engine.Options.Interop.TypeResolver.GetAccessor(_engine, ClrType, member, requirement, throwOnError);
        var actualType = Target.GetType();
        if (ClrType != actualType)
        {
            // When the declared type differs from the actual runtime type:
            // - If no accessor was found, fall back to the runtime type (original behavior)
            // - If only an indexer was found, check if the runtime type has a direct property/field/method
            //   that should take precedence over the indexer
            if (accessor == ConstantValueAccessor.NullAccessor)
            {
                accessor = _engine.Options.Interop.TypeResolver.GetAccessor(_engine, actualType, member, requirement, throwOnError);
            }
            else if (accessor is IndexerAccessor)
            {
                var runtimeAccessor = _engine.Options.Interop.TypeResolver.GetAccessor(_engine, actualType, member, requirement, throwOnError);
                if (runtimeAccessor is not IndexerAccessor && runtimeAccessor != ConstantValueAccessor.NullAccessor)
                {
                    // Prefer direct property/field/method from runtime type over indexer from declared type
                    accessor = runtimeAccessor;
                }
            }
        }

        if (IsUnaddressablePosition(property, accessor))
        {
            // Reading the indexer at this position is the collection's own ArgumentOutOfRangeException, and
            // reporting a descriptor for it is what made `3 in x` and hasOwnProperty answer true for a
            // position that cannot be read at all. There is no element here, so there is no property (#3422).
            return PropertyDescriptor.Undefined;
        }

        // A member resolving purely to registered extension methods must not shadow a same-named
        // Array.prototype native on an indexed array-like view (#2976): return Undefined WITHOUT
        // caching, so the outer Get falls through to the attached prototype and a later mutation of
        // Array.prototype stays observable. The probe is an exact-case own-property read on the
        // prototype itself - a chain Get would also see the extension attachments living on
        // Object.prototype and defer names like Sum that Array.prototype never carried. The type
        // tests keep non-indexed array-likes (e.g. HashSet<T>), whose elements the index-driven
        // natives cannot see, and replaced prototypes on today's extension-first behavior.
        if (!isDictionary
            && accessor is MethodAccessor { AllExtensionMethods: true }
            && this is ArrayLikeWrapper
            && _prototype is ArrayPrototype arrayPrototype)
        {
            var protoDesc = arrayPrototype.GetOwnProperty(property);
            if (!ReferenceEquals(protoDesc, PropertyDescriptor.Undefined)
                && protoDesc.Value is { IsCallable: true })
            {
                return PropertyDescriptor.Undefined;
            }
        }

        var descriptor = accessor.CreatePropertyDescriptor(_engine, Target, member, enumerable: !isDictionary);
        if (!isDictionary
            && !ReferenceEquals(descriptor, PropertyDescriptor.Undefined)
            && requirement.IsSatisfiedBy(accessor))
        {
            // cache the accessor for faster subsequent accesses
            SetProperty(member, descriptor);
        }

        return descriptor;
    }

    /// <summary>
    /// The flags a dictionary-backed member is reported with. Configurability follows
    /// <see cref="Options.InteropOptions.AllowWrite"/>, exactly as it did before the memo existed.
    /// </summary>
    private PropertyFlag DictionaryMemberFlags()
    {
        var flags = PropertyFlag.Enumerable;
        if (_engine.Options.Interop.AllowWrite)
        {
            flags |= PropertyFlag.Configurable;
        }

        return flags;
    }

    /// <summary>
    /// Records the descriptor a dictionary key resolves to, so every later read of that key on this wrapper
    /// answers from it. Only ever called while <see cref="_immutableCrossing"/>, and only for a key
    /// <c>_properties</c> has been shown not to carry.
    /// </summary>
    private PropertyDescriptor MemoizeDictionaryValue(string member, JsValue value)
    {
        var descriptor = new PropertyDescriptor(value, DictionaryMemberFlags());
        (_crossingMemo ??= new PropertyDictionary())[member] = descriptor;
        return descriptor;
    }

    /// <summary>
    /// Drops the whole crossing memo. Called from every path that can put a descriptor for an arbitrary key
    /// into <c>_properties</c>, which must win over the memo from then on — <c>Object.freeze</c>,
    /// <c>Object.defineProperty</c> and a direct host <see cref="SetOwnProperty"/>. Dropping everything
    /// rather than one key is deliberate: these are rare, and a key whose descriptor now lives in
    /// <c>_properties</c> is answered from there before the dictionary lane is reached again, so it simply
    /// never re-enters the memo.
    /// <para>
    /// Also the escape hatch for a subclass whose write path does not reach <see cref="Set"/>'s per-key
    /// eviction — <see cref="ArrayLikeWrapper"/> serves length and fixed-size index writes itself. Such a
    /// target only ever carries a memo when it is dictionary-shaped as well (Newtonsoft's <c>JObject</c> is
    /// both), which is exactly when a length write can invalidate arbitrary keys.
    /// </para>
    /// </summary>
    private protected void DropCrossingMemo() => _crossingMemo = null;

    /// <summary>
    /// Per-AST-node inline-cache support (see the wrapper member lane in
    /// <see cref="Runtime.Interpreter.Expressions.JintMemberExpression"/>): returns the already-stored own
    /// descriptor a member read/write would consult, when — and only when — caching it per call site is
    /// sound. The result is exactly what <see cref="Get"/> / <see cref="Set"/> resolve through
    /// <c>TryGetProperty</c> once <see cref="GetOwnProperty(JsValue)"/> has stored the member (a live
    /// <see cref="Descriptors.Specialized.ReflectionDescriptor"/> for CLR properties/fields, so every use
    /// still flows through the CLR accessors), making a receiver-identity + <c>_propertiesVersion</c>
    /// guard sufficient: any define/redefine/delete on the wrapper bumps the version and invalidates.
    /// </summary>
    /// <remarks>
    /// Bails with <c>null</c> when:
    /// <list type="bullet">
    /// <item>the receiver is a subclass (e.g. <see cref="ArrayLikeWrapper"/> overrides Get/Set with its own semantics),</item>
    /// <item>the target is a dictionary (member values are dynamic; descriptors are created fresh per access),</item>
    /// <item>a custom <see cref="Options.InteropOptions.MemberAccessor"/> is configured (stay conservative),</item>
    /// <item>the name is <c>length</c> on an <see cref="ICollection"/> target (<see cref="Get"/> serves the live count ahead of any stored descriptor),</item>
    /// <item>the member has not been resolved-and-stored yet — the caller's slow path performs the store (bumping the version) so a later populate succeeds.</item>
    /// </list>
    /// </remarks>
    internal PropertyDescriptor? TryGetInlineCacheableDescriptor(JsString property)
    {
        if (GetType() != typeof(ObjectWrapper)
            || _typeDescriptor.IsDictionary
            || _properties is null
            || !ReferenceEquals(_engine.Options.Interop.MemberAccessor, Options.InteropOptions._defaultMemberAccessor))
        {
            return null;
        }

        if (Target is ICollection && CommonProperties.Length.Equals(property))
        {
            return null;
        }

        if (!_properties.TryGetValue(property.ToString(), out var descriptor)
            || ReferenceEquals(descriptor, PropertyDescriptor.Undefined))
        {
            return null;
        }

        return descriptor;
    }

    // need to be public for advanced cases like RavenDB yielding properties from CLR objects
    public static PropertyDescriptor GetPropertyDescriptor(Engine engine, object target, MemberInfo member)
    {
        // fast path which uses slow search if not found for some reason
        ReflectionAccessor? Factory()
        {
            return member switch
            {
                PropertyInfo pi => new PropertyAccessor(pi),
                MethodBase mb => new MethodAccessor(target.GetType(), MethodDescriptor.Build(new[] { mb })),
                FieldInfo fi => new FieldAccessor(fi),
                _ => null
            };
        }

        var accessor = engine.Options.Interop.TypeResolver.GetAccessor(engine, target.GetType(), member.Name, MemberResolutionRequirement.None, accessorFactory: Factory);
        return accessor.CreatePropertyDescriptor(engine, target, member.Name);
    }

    internal static Type GetClrType(object obj, Type? type)
    {
        if (type is null || type == typeof(object))
        {
            return obj.GetType();
        }

        var underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType is not null)
        {
            return underlyingType;
        }

        return type;
    }

    private static JsValue Iterator(JsValue thisObject, JsCallArguments arguments)
    {
        var wrapper = UnwrapReceiver(thisObject, "[Symbol.iterator]");

        return wrapper._typeDescriptor.IsDictionary
            ? new DictionaryIterator(wrapper._engine, wrapper)
            : new EnumerableIterator(wrapper._engine, (IEnumerable) wrapper.Target);
    }

    private GetSetPropertyDescriptor MaterializeLengthProperty()
    {
        _lengthPropertyPending = false;
        // create a forwarder to produce length from Count or Length if one of them is present
        var functionInstance = new ClrFunction(_engine, "length", GetLength);
        var descriptor = new GetSetPropertyDescriptor(functionInstance, Undefined, PropertyFlag.Configurable);
        SetProperty(KnownKeys.Length, descriptor);
        return descriptor;
    }

    private static JsNumber GetLength(JsValue thisObject, JsCallArguments arguments)
    {
        var wrapper = UnwrapReceiver(thisObject, "length");
        return JsNumber.Create((int) (wrapper._typeDescriptor.LengthProperty?.GetValue(wrapper.Target) ?? 0));
    }

    /// <summary>
    /// Resolves the <see cref="ObjectWrapper"/> receiver for the host helper functions above (the
    /// Symbol.iterator implementation and the length getter). Script code can extract these functions
    /// and re-target them (e.g. <c>f.call({})</c>) or invoke them through a (possibly revoked) Proxy,
    /// so a foreign or revoked receiver must surface as a JavaScript TypeError instead of a CLR crash.
    /// </summary>
    private static ObjectWrapper UnwrapReceiver(JsValue thisObject, string functionName)
    {
        var current = thisObject;
        while (current is JsProxy proxy)
        {
            if (proxy.IsRevoked)
            {
                Throw.TypeError(proxy.Engine.Realm, $"Cannot perform '{functionName}' on a proxy that has been revoked");
            }

            current = proxy._target;
        }

        if (current is ObjectWrapper wrapper)
        {
            return wrapper;
        }

        var message = $"Method '{functionName}' called on incompatible receiver";
        if (current is ObjectInstance objectInstance)
        {
            Throw.TypeError(objectInstance.Engine.Realm, message);
        }

        // primitive receiver, no engine reachable - converted to a JS TypeError by the interpreter
        Throw.TypeErrorNoEngine(message);
        return null!;
    }

    internal override ulong GetSmallestIndex(ulong length)
    {
        return Target is ICollection ? 0 : base.GetSmallestIndex(length);
    }

    public override bool Equals(object? obj) => Equals(obj as ObjectWrapper);

    public override bool Equals(JsValue? other) => Equals(other as ObjectWrapper);

    public bool Equals(ObjectWrapper? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Equals(Target, other.Target);
    }

    public override int GetHashCode() => Target.GetHashCode();

    private sealed class DictionaryIterator : IteratorInstance
    {
        private readonly ObjectWrapper _target;
        private readonly IEnumerator<JsValue> _enumerator;

        public DictionaryIterator(Engine engine, ObjectWrapper target) : base(engine)
        {
            _target = target;
            _enumerator = target.EnumerateOwnPropertyKeys(Types.String).GetEnumerator();
        }

        public override bool TryIteratorStep(out ObjectInstance nextItem)
        {
            var hasNext = _enumerator.MoveNext();
            try
            {
                _engine.CheckAmortizedConstraintsAtHostBoundary();
            }
            catch
            {
                // for-of only starts closing the iterator record after the first successful
                // step, so dispose the user's enumerator here or it would leak
                _enumerator.Dispose();
                throw;
            }

            if (hasNext)
            {
                var key = _enumerator.Current;
                var value = _target.Get(key);

                nextItem = IteratorResult.CreateKeyValueIteratorPosition(_engine, key, value);
                return true;
            }

            nextItem = IteratorResult.CreateKeyValueIteratorPosition(_engine);
            return false;
        }
    }

    private sealed class EnumerableIterator : IteratorInstance
    {
        private readonly IEnumerator _enumerator;

        public EnumerableIterator(Engine engine, IEnumerable target) : base(engine)
        {
            _enumerator = target.GetEnumerator();
        }

        public override void Close(CompletionType completion)
        {
            (_enumerator as IDisposable)?.Dispose();
            base.Close(completion);
        }

        public override bool TryIteratorStep(out ObjectInstance nextItem)
        {
            var hasNext = _enumerator.MoveNext();
            try
            {
                _engine.CheckAmortizedConstraintsAtHostBoundary();
            }
            catch
            {
                // for-of only starts closing the iterator record after the first successful
                // step, so dispose the user's enumerator here or it would leak
                Close(CompletionType.Throw);
                throw;
            }

            if (hasNext)
            {
                var value = _enumerator.Current;
                nextItem = IteratorResult.CreateValueIteratorPosition(_engine, FromObject(_engine, value));
                return true;
            }

            nextItem = IteratorResult.CreateKeyValueIteratorPosition(_engine);
            return false;
        }
    }
}
