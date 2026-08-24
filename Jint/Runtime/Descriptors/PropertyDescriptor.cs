using System.Diagnostics;
using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Runtime.Descriptors;

[DebuggerDisplay("Value: {Value}, Flags: {Flags}")]
public class PropertyDescriptor
{
    public static readonly PropertyDescriptor Undefined = new UndefinedPropertyDescriptor();

    internal PropertyFlag _flags;
    internal JsValue? _value;

    public PropertyDescriptor() : this(PropertyFlag.None)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected PropertyDescriptor(PropertyFlag flags)
    {
        _flags = flags & ~PropertyFlag.NonData;
    }

    /// <summary>
    /// Creates a descriptor with the attribute bits given directly, instead of the three
    /// <see cref="Nullable{T}"/> attributes the <see cref="PropertyDescriptor(JsValue?, bool?, bool?, bool?)"/>
    /// overload has to translate into flags one branchy setter at a time. Host code that already
    /// knows the attributes it wants — the common case when bridging host state into JavaScript —
    /// should prefer this constructor and a ready-made combination such as
    /// <see cref="PropertyFlag.ConfigurableEnumerableWritable"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="PropertyFlag.CustomJsValue"/> is reserved for subclasses that override
    /// <see cref="CustomValue"/>: passing it routes <paramref name="value"/> through that virtual
    /// setter, so on a type that does not override it the flag only adds an indirection on every
    /// read and write. Do not pass it from ordinary host descriptors. For the common reason to want it —
    /// a value that should be produced on the first read rather than now — use
    /// <see cref="CreateLazy{TState}(TState, Func{TState, JsValue}, PropertyFlag)"/>, which builds the
    /// descriptor for you and, unlike a hand-written <see cref="CustomValue"/> override, drops the flag
    /// once the value exists so the descriptor rejoins the write and global-binding inline caches.
    /// </remarks>
    /// <param name="value">The property value, or <see langword="null"/> for a descriptor whose value is supplied later.</param>
    /// <param name="flags">The property attributes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PropertyDescriptor(JsValue? value, PropertyFlag flags) : this(flags)
    {
        if ((_flags & PropertyFlag.CustomJsValue) != PropertyFlag.None)
        {
#pragma warning disable MA0056
            CustomValue = value;
#pragma warning restore MA0056
        }
        _value = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PropertyDescriptor(JsValue? value, bool? writable, bool? enumerable, bool? configurable)
    {
        if ((_flags & PropertyFlag.CustomJsValue) != PropertyFlag.None)
        {
#pragma warning disable MA0056
            CustomValue = value;
#pragma warning restore MA0056
        }
        _value = value;

        if (writable != null)
        {
            Writable = writable.Value;
            WritableSet = true;
        }

        if (enumerable != null)
        {
            Enumerable = enumerable.Value;
            EnumerableSet = true;
        }

        if (configurable != null)
        {
            Configurable = configurable.Value;
            ConfigurableSet = true;
        }
    }

    public PropertyDescriptor(PropertyDescriptor descriptor)
    {
        Value = descriptor.Value;

        Enumerable = descriptor.Enumerable;
        EnumerableSet = descriptor.EnumerableSet;

        Configurable = descriptor.Configurable;
        ConfigurableSet = descriptor.ConfigurableSet;

        Writable = descriptor.Writable;
        WritableSet = descriptor.WritableSet;
    }

    public virtual JsValue? Get => null;
    public virtual JsValue? Set => null;

    public bool Enumerable
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_flags & PropertyFlag.Enumerable) != PropertyFlag.None;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            _flags |= PropertyFlag.EnumerableSet;
            if (value)
            {
                _flags |= PropertyFlag.Enumerable;
            }
            else
            {
                _flags &= ~(PropertyFlag.Enumerable);
            }
        }
    }

    public bool EnumerableSet
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_flags & (PropertyFlag.EnumerableSet | PropertyFlag.Enumerable)) != PropertyFlag.None;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private set
        {
            if (value)
            {
                _flags |= PropertyFlag.EnumerableSet;
            }
            else
            {
                _flags &= ~(PropertyFlag.EnumerableSet);
            }
        }
    }

    public bool Writable
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_flags & PropertyFlag.Writable) != PropertyFlag.None;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            _flags |= PropertyFlag.WritableSet;
            if (value)
            {
                _flags |= PropertyFlag.Writable;
            }
            else
            {
                _flags &= ~(PropertyFlag.Writable);
            }
        }
    }

    public bool WritableSet
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_flags & (PropertyFlag.WritableSet | PropertyFlag.Writable)) != PropertyFlag.None;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private set
        {
            if (value)
            {
                _flags |= PropertyFlag.WritableSet;
            }
            else
            {
                _flags &= ~(PropertyFlag.WritableSet);
            }
        }
    }

    public bool Configurable
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_flags & PropertyFlag.Configurable) != PropertyFlag.None;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            _flags |= PropertyFlag.ConfigurableSet;
            if (value)
            {
                _flags |= PropertyFlag.Configurable;
            }
            else
            {
                _flags &= ~(PropertyFlag.Configurable);
            }
        }
    }

    public bool ConfigurableSet
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_flags & (PropertyFlag.ConfigurableSet | PropertyFlag.Configurable)) != PropertyFlag.None;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private set
        {
            if (value)
            {
                _flags |= PropertyFlag.ConfigurableSet;
            }
            else
            {
                _flags &= ~(PropertyFlag.ConfigurableSet);
            }
        }
    }

    public JsValue Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((_flags & PropertyFlag.CustomJsValue) != PropertyFlag.None)
            {
                return CustomValue!;
            }

            return _value!;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            if ((_flags & PropertyFlag.CustomJsValue) != PropertyFlag.None)
            {
                CustomValue = value;
            }
            _value = value;
        }
    }

    protected internal virtual JsValue? CustomValue
    {
        get => null;
        set => Throw.NotImplementedException();
    }

    internal PropertyFlag Flags
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _flags;
    }

    // Bits a lazy data descriptor must not be handed. CustomJsValue is applied by the factory itself, and a
    // caller passing it would be claiming a hook it cannot implement (the returned type is not derivable);
    // NonData marks a descriptor as an accessor, which this is not; MutableBinding is the global object's
    // marker for a property created by a var declaration, not an attribute a host describes a property with.
    private const PropertyFlag ReservedLazyFlags =
        PropertyFlag.CustomJsValue | PropertyFlag.NonData | PropertyFlag.MutableBinding;

    /// <summary>
    /// Creates a data property descriptor whose value is produced by <paramref name="valueFactory"/> the first
    /// time something reads it, instead of now. Attributes are decided immediately, so existence and
    /// enumerability questions — <c>in</c>, <c>hasOwnProperty</c>, <c>Object.keys</c>, spread,
    /// <c>JSON.stringify</c> — are answered without the factory running.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the sanctioned way to build a lazy property by hand. <see cref="PropertyFlag.CustomJsValue"/>
    /// and a <see cref="CustomValue"/> override remain the hook for a value that stays computed — one
    /// projected live out of host state, recomputed on every read. Prefer this factory whenever the value is
    /// lazy exactly <em>once</em>: the descriptor it returns memoizes the produced value and then drops the
    /// flag, which readmits it to the two lanes that decline a custom-valued descriptor — the interpreter's
    /// member-write fast path and the global-identifier cache. A hand-written <see cref="CustomValue"/>
    /// override cannot do that, because the engine has no way to know the override became a constant, so such
    /// a descriptor stays declined by both lanes for the rest of its life. (Read caching is unaffected either
    /// way: every read lane returns through <c>UnwrapJsValue</c>, which re-reads the flag on each hit.)
    /// </para>
    /// <para>
    /// A <em>write</em> counts as materialization: once a value is stored the factory can never run, so the
    /// descriptor rejoins the caches then too.
    /// </para>
    /// <para>
    /// <b>Where this belongs.</b> It describes one property, so it fits wherever a host stores or returns
    /// descriptors: <c>SetOwnProperty</c> and <c>GetOwnProperty</c> on an <see cref="ObjectInstance"/>
    /// subclass, <see cref="ObjectInstance.FastSetProperty(string, PropertyDescriptor)"/> on a
    /// dictionary-mode object, a hand-rolled global or prototype install. It is <em>not</em> the tool for the
    /// two shaped storage worlds, and storing one into them is a deoptimization rather than a lazy member:
    /// a string-keyed <c>FastSetProperty</c> of any raw descriptor moves a shape-mode object to the
    /// dictionary representation permanently. Use <c>JsObjectLayout.CreateBuilder().AddLazy</c> for a lazy
    /// member of a fixed-shape record and <c>JsObjectShape</c> for a lazily materialized member of a
    /// prototype; each of the three mechanisms owns one storage world.
    /// </para>
    /// <para>
    /// <b>Threading.</b> The returned descriptor is engine-affine in practice — it is materialized under the
    /// engine's single-thread contract, like every other descriptor — and performs no synchronization. Do not
    /// share one instance between engines that run concurrently.
    /// </para>
    /// </remarks>
    /// <typeparam name="TState">Type of the state handed back to the factory.</typeparam>
    /// <param name="state">
    /// Passed to <paramref name="valueFactory"/> when it runs. Exists so a <c>static</c> lambda can serve the
    /// property without the closure allocation a captured variable would cost; may be <see langword="null"/>.
    /// </param>
    /// <param name="valueFactory">
    /// Produces the value. Invoked at most once per descriptor, on the engine's thread, during property
    /// resolution — so it is morally a getter body and must not read the very property it computes. A
    /// <see langword="null"/> return is stored as <see cref="JsValue.Undefined"/>, so that it cannot silently
    /// turn into a factory that re-runs on every read. If it throws, the exception propagates out of the
    /// operation performing the read and the descriptor stays unmaterialized, so the next read runs it again.
    /// </param>
    /// <param name="flags">
    /// The property attributes; defaults to configurable, enumerable and writable.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="valueFactory"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="flags"/> contains <see cref="PropertyFlag.CustomJsValue"/>,
    /// <see cref="PropertyFlag.NonData"/> or <see cref="PropertyFlag.MutableBinding"/>, none of which a host
    /// describes a lazy data property with.
    /// </exception>
    /// <example>
    /// <code>
    /// // On a host ObjectInstance subclass, in its Initialize():
    /// SetOwnProperty("report", PropertyDescriptor.CreateLazy(
    ///     _row,
    ///     static row => JsValue.FromObject(row.Engine, row.ParseReport()),
    ///     PropertyFlag.NonEnumerable));
    /// </code>
    /// </example>
    public static PropertyDescriptor CreateLazy<TState>(
        TState state,
        Func<TState, JsValue> valueFactory,
        PropertyFlag flags = PropertyFlag.ConfigurableEnumerableWritable)
    {
        if (valueFactory is null)
        {
            Throw.ArgumentNullException(nameof(valueFactory));
        }

        if ((flags & ReservedLazyFlags) != PropertyFlag.None)
        {
            Throw.ArgumentException(
                $"{flags & ReservedLazyFlags} is reserved for the engine and cannot be part of a lazy property's attributes.",
                nameof(flags));
        }

        // Handed over unwrapped. The descriptor treats a null value as "not materialized yet", so the null
        // return documented above has to become Undefined somewhere - but that substitution lives in
        // LazyPropertyDescriptor.CustomValue, which every route into the descriptor passes through, so
        // guarding here too would only add a display class and a delegate per call on a public factory.
        return new LazyPropertyDescriptor<TState>(state, valueFactory, flags);
    }

    /// <summary>
    /// Creates a data property descriptor whose value is produced by <paramref name="valueFactory"/> the first
    /// time something reads it, instead of now. Convenience form of
    /// <see cref="CreateLazy{TState}(TState, Func{TState, JsValue}, PropertyFlag)"/> for a factory that needs
    /// no state handed to it; prefer that overload with a <c>static</c> lambda when the factory would
    /// otherwise capture, so the delegate does not allocate a closure per property.
    /// </summary>
    /// <param name="valueFactory">
    /// Produces the value. Invoked at most once per descriptor; a <see langword="null"/> return is stored as
    /// <see cref="JsValue.Undefined"/>.
    /// </param>
    /// <param name="flags">
    /// The property attributes; defaults to configurable, enumerable and writable.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="valueFactory"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="flags"/> contains a flag reserved for the engine — see the other overload.
    /// </exception>
    public static PropertyDescriptor CreateLazy(
        Func<JsValue> valueFactory,
        PropertyFlag flags = PropertyFlag.ConfigurableEnumerableWritable)
    {
        if (valueFactory is null)
        {
            Throw.ArgumentNullException(nameof(valueFactory));
        }

        return CreateLazy(valueFactory, static factory => factory(), flags);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-topropertydescriptor
    /// </summary>
    public static PropertyDescriptor ToPropertyDescriptor(Realm realm, JsValue o)
    {
        if (o is not ObjectInstance obj)
        {
            Throw.TypeError(realm, $"Property description must be an object: {o}");
            return null;
        }

        bool? enumerable = null;
        var hasEnumerable = obj.HasProperty(CommonProperties.Enumerable);
        if (hasEnumerable)
        {
            enumerable = TypeConverter.ToBoolean(obj.Get(CommonProperties.Enumerable));
        }

        bool? configurable = null;
        var hasConfigurable = obj.HasProperty(CommonProperties.Configurable);
        if (hasConfigurable)
        {
            configurable = TypeConverter.ToBoolean(obj.Get(CommonProperties.Configurable));
        }

        JsValue? value = null;
        var hasValue = obj.HasProperty(CommonProperties.Value);
        if (hasValue)
        {
            value = obj.Get(CommonProperties.Value);
        }

        bool? writable = null;
        var hasWritable = obj.HasProperty(CommonProperties.Writable);
        if (hasWritable)
        {
            writable = TypeConverter.ToBoolean(obj.Get(CommonProperties.Writable));
        }

        JsValue? get = null;
        var hasGet = obj.HasProperty(CommonProperties.Get);
        if (hasGet)
        {
            get = obj.Get(CommonProperties.Get);
        }

        JsValue? set = null;
        var hasSet = obj.HasProperty(CommonProperties.Set);
        if (hasSet)
        {
            set = obj.Get(CommonProperties.Set);
        }

        if ((hasValue || hasWritable) && (hasGet || hasSet))
        {
            Throw.TypeError(realm, "Invalid property descriptor. Cannot both specify accessors and a value or writable attribute");
        }

        var desc = hasGet || hasSet
            ? new GetSetPropertyDescriptor(null, null, PropertyFlag.None)
            : new PropertyDescriptor(PropertyFlag.None);

        if (hasEnumerable)
        {
            desc.Enumerable = enumerable!.Value;
            desc.EnumerableSet = true;
        }

        if (hasConfigurable)
        {
            desc.Configurable = configurable!.Value;
            desc.ConfigurableSet = true;
        }

        if (hasValue)
        {
            desc.Value = value!;
        }

        if (hasWritable)
        {
            desc.Writable = writable!.Value;
            desc.WritableSet = true;
        }

        if (hasGet)
        {
            if (!get!.IsUndefined() && get is not ICallable)
            {
                Throw.TypeError(realm, "Getter must be a function");
            }

            ((GetSetPropertyDescriptor) desc).SetGet(get!);
        }

        if (hasSet)
        {
            if (!set!.IsUndefined() && set is not ICallable)
            {
                Throw.TypeError(realm, "Setter must be a function");
            }

            ((GetSetPropertyDescriptor) desc).SetSet(set!);
        }

        // NOTE: the accessor-vs-data conflict is already rejected above (before desc was built); the has*
        // flags are immutable thereafter, so a second identical check here would be dead code.

        return desc;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-frompropertydescriptor
    /// </summary>
    public static JsValue FromPropertyDescriptor(Engine engine, PropertyDescriptor desc, bool strictUndefined = false)
    {
        if (ReferenceEquals(desc, Undefined))
        {
            return JsValue.Undefined;
        }

        var obj = engine.Realm.Intrinsics.Object.Construct(Arguments.Empty);
        var properties = new PropertyDictionary(4, checkExistingKeys: false);

        // TODO should not check for strictUndefined, but needs a bigger cleanup
        // we should have possibility to leave out the properties in property descriptors as newer tests
        // also assert properties to be undefined

        if (strictUndefined)
        {
            // Field-driven, as the specification writes it: an attribute is created exactly when the
            // descriptor *has* that field. This is the mode a partial descriptor is rendered in — the one
            // ToPropertyDescriptor built from whatever fields the caller actually wrote, and which the Proxy
            // "defineProperty" trap is handed. `Object.defineProperty(proxy, k, { configurable: true, set() {} })`
            // must therefore show the trap exactly "set" and "configurable"; deriving the shape from
            // IsDataDescriptor() instead (below) invents a "get" beside the "set", a "writable" beside a
            // "value", and a "get"/"set" pair for a descriptor carrying nothing but attributes.
            // Only the trap *argument* is shaped this way: the post-trap checks in JsProxy still run against
            // Desc itself, per steps 12-15 of
            // https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-defineownproperty-p-desc
            var value = desc.Value;
            if (value is not null)
            {
                properties["value"] = new PropertyDescriptor(value, PropertyFlag.ConfigurableEnumerableWritable);
            }

            if (desc.WritableSet)
            {
                properties["writable"] = new PropertyDescriptor(desc.Writable, PropertyFlag.ConfigurableEnumerableWritable);
            }

            var get = desc.Get;
            if (get is not null)
            {
                properties["get"] = new PropertyDescriptor(get, PropertyFlag.ConfigurableEnumerableWritable);
            }

            var set = desc.Set;
            if (set is not null)
            {
                properties["set"] = new PropertyDescriptor(set, PropertyFlag.ConfigurableEnumerableWritable);
            }
        }
        else if (desc.IsDataDescriptor())
        {
            properties["value"] = new PropertyDescriptor(desc.Value ?? JsValue.Undefined, PropertyFlag.ConfigurableEnumerableWritable);
            if (desc._flags != PropertyFlag.None || desc.WritableSet)
            {
                properties["writable"] = new PropertyDescriptor(desc.Writable, PropertyFlag.ConfigurableEnumerableWritable);
            }
        }
        else
        {
            properties["get"] = new PropertyDescriptor(desc.Get ?? JsValue.Undefined, PropertyFlag.ConfigurableEnumerableWritable);
            properties["set"] = new PropertyDescriptor(desc.Set ?? JsValue.Undefined, PropertyFlag.ConfigurableEnumerableWritable);
        }

        if (!strictUndefined || desc.EnumerableSet)
        {
            properties["enumerable"] = new PropertyDescriptor(desc.Enumerable, PropertyFlag.ConfigurableEnumerableWritable);
        }

        if (!strictUndefined || desc.ConfigurableSet)
        {
            properties["configurable"] = new PropertyDescriptor(desc.Configurable, PropertyFlag.ConfigurableEnumerableWritable);
        }

        obj.SetProperties(properties);
        return obj;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsAccessorDescriptor()
    {
        return Get is not null || Set is not null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsDataDescriptor()
    {
        if ((_flags & PropertyFlag.NonData) != PropertyFlag.None)
        {
            return false;
        }
        return (_flags & (PropertyFlag.WritableSet | PropertyFlag.Writable)) != PropertyFlag.None
               || (_flags & PropertyFlag.CustomJsValue) != PropertyFlag.None && CustomValue is not null
               || _value is not null;
    }

    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.10.3
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsGenericDescriptor()
    {
        return !IsDataDescriptor() && !IsAccessorDescriptor();
    }

    private sealed class UndefinedPropertyDescriptor : PropertyDescriptor
    {
        public UndefinedPropertyDescriptor() : base(PropertyFlag.None | PropertyFlag.CustomJsValue)
        {
        }

        protected internal override JsValue? CustomValue
        {
            set => Throw.InvalidOperationException("making changes to undefined property's descriptor is not allowed");
        }
    }

    internal sealed class AllForbiddenDescriptor : PropertyDescriptor
    {
        private static readonly PropertyDescriptor[] _cache;

        public static readonly AllForbiddenDescriptor NumberZero = new AllForbiddenDescriptor(JsNumber.Create(0));
        public static readonly AllForbiddenDescriptor NumberOne = new AllForbiddenDescriptor(JsNumber.Create(1));

        public static readonly AllForbiddenDescriptor BooleanFalse = new AllForbiddenDescriptor(JsBoolean.False);
        public static readonly AllForbiddenDescriptor BooleanTrue = new AllForbiddenDescriptor(JsBoolean.True);

        static AllForbiddenDescriptor()
        {
            _cache = new PropertyDescriptor[10];
            for (int i = 0; i < _cache.Length; ++i)
            {
                _cache[i] = new AllForbiddenDescriptor(JsNumber.Create(i));
            }
        }

        private AllForbiddenDescriptor(JsValue value)
            : base(PropertyFlag.AllForbidden)
        {
            _value = value;
        }

        public static PropertyDescriptor ForNumber(int number)
        {
            var temp = _cache;
            return (uint) number < temp.Length
                ? temp[number]
                : new PropertyDescriptor(number, PropertyFlag.AllForbidden);
        }
    }
}
