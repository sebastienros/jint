using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Jint.Collections;
using Jint.Native.Array;
using Jint.Native.BigInt;
using Jint.Native.Boolean;
using Jint.Native.Json;
using Jint.Native.Number;
using Jint.Native.Promise;
using Jint.Native.String;
using Jint.Native.Symbol;
using Jint.Native.TypedArray;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Interop;
using PropertyDescriptor = Jint.Runtime.Descriptors.PropertyDescriptor;
using TypeConverter = Jint.Runtime.TypeConverter;

namespace Jint.Native.Object;

/// <summary>
/// Base class for every JavaScript object.
/// </summary>
/// <remarks>
/// An <see cref="ObjectInstance"/> is bound to the <see cref="Engine"/> (and realm) that created it and
/// holds a hard reference to it. Handing an instance to another engine — as an argument, a global, a
/// prototype or a return value — is unsupported: nothing validates it, and the object will resolve its
/// prototype chain, intrinsics and interop services against the wrong engine. Convert through a
/// serialization boundary instead, or create the value on the engine that will consume it.
/// </remarks>
[DebuggerTypeProxy(typeof(ObjectInstanceDebugView))]
public partial class ObjectInstance : JsValue, IEquatable<ObjectInstance>
{
    private protected bool _initialized;
    private readonly ObjectClass _class;

    internal PropertyDictionary? _properties;
    internal SymbolDictionary? _symbols;

    // Hidden-class shape storage (a Shape + flat JsValue[] slot array) lives on JsObject, not here, so
    // the broad ObjectInstance population (JsDate, JsArray, TypedArray, wrappers, built-ins) keeps its
    // size. Only JsObject (plain object literals, `new Ctor()` this, `new Object()`) can be in shape
    // mode; the base property methods reach it via `this is JsObject`. Shape mode is mutually exclusive
    // with _properties for string keys; _symbols is orthogonal. Anything a shape can't represent (delete,
    // accessor/non-CEW define, freeze/seal, prototype change, bulk install) deopts back to _properties.

    /// <summary>
    /// Bumped whenever own-property shape changes (descriptor added/replaced/removed via SetProperty / RemoveOwnProperty).
    /// Plain in-place value updates of an existing data descriptor (the hot Set fast path) do NOT bump this.
    /// Used by inline caches (e.g. <see cref="Jint.Runtime.Interpreter.Expressions.JintMemberExpression"/>) to validate cached descriptor references.
    /// </summary>
    internal uint _propertiesVersion;

    internal ObjectInstance? _prototype;
    protected readonly Engine _engine;

    /// <summary>
    /// The constructor a subclass defined outside Jint reaches. It resolves the subclass's
    /// <see cref="PropertyAccessSemantics"/> from the type itself, so a host never has to declare anything for
    /// the engine to read it correctly — see <see cref="DeriveAccessSemantics"/>. A subclass that disagrees
    /// with the derived answer overrides it from its own constructor body, which runs after this one.
    /// </summary>
    protected ObjectInstance(Engine engine) : this(engine, ObjectClass.Object)
    {
        // GetType() in a base constructor is the most-derived runtime type, which is exactly what the
        // derivation is about. Jint's own types that want a specific set of flags (or the hot ones, which
        // cannot afford even a cached lookup per instance) pass them through the internal constructor and
        // never reach this line.
        _type |= DeriveAccessSemantics(GetType());
    }

    internal ObjectInstance(
        Engine engine,
        ObjectClass objectClass = ObjectClass.Object,
        InternalTypes type = InternalTypes.Object)
        : base(type)
    {
        _engine = engine;
        _class = objectClass;
        // if engine is ready, we can take default prototype for object
        _prototype = engine.Realm.Intrinsics?.Object?.PrototypeObject;
#pragma warning disable MA0056
        Extensible = true;
#pragma warning restore MA0056
    }

    public Engine Engine
    {
        [DebuggerStepThrough]
        get => _engine;
    }

    /// <summary>
    /// The prototype of this object.
    /// </summary>
    public ObjectInstance? Prototype
    {
        [DebuggerStepThrough]
        get => GetPrototypeOf();
        set => SetPrototypeOf(value!);
    }

    /// <summary>
    /// If true, own properties may be added to the
    /// object.
    /// </summary>
    public virtual bool Extensible { get; internal set; }

    internal PropertyDictionary? Properties
    {
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _properties;
    }

    /// <summary>
    /// A value indicating a specification defined classification of objects.
    /// </summary>
    internal ObjectClass Class
    {
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _class;
    }

    public JsValue this[JsValue property]
    {
        get => Get(property);
        set => Set(property, value);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-construct
    /// </summary>
    internal static ObjectInstance Construct(IConstructor f, IConstructor? newTarget, JsCallArguments argumentsList)
    {
        newTarget ??= f;
        return f.Construct(argumentsList, (JsValue) newTarget);
    }

    internal static ObjectInstance Construct(IConstructor f, JsCallArguments argumentsList)
    {
        return f.Construct(argumentsList, (JsValue) f);
    }

    internal static ObjectInstance Construct(IConstructor f)
    {
        return f.Construct([], (JsValue) f);
    }


    /// <summary>
    /// https://tc39.es/ecma262/#sec-speciesconstructor
    /// </summary>
    internal static IConstructor SpeciesConstructor(ObjectInstance o, IConstructor defaultConstructor)
    {
        var c = o.Get(CommonProperties.Constructor);
        if (c.IsUndefined())
        {
            return defaultConstructor;
        }

        var oi = c as ObjectInstance;
        if (oi is null)
        {
            Throw.TypeError(o._engine.Realm, "Species constructor is not an object");
        }

        var s = oi.Get(GlobalSymbolRegistry.Species);
        if (s.IsNullOrUndefined())
        {
            return defaultConstructor;
        }

        if (s.IsConstructor)
        {
            return (IConstructor) s;
        }

        Throw.TypeError(o._engine.Realm, "The [Symbol.species] property is not a constructor");
        return null;
    }

    internal void SetProperties(StringDictionarySlim<PropertyDescriptor> properties) => SetProperties(new PropertyDictionary(properties));

    internal void SetProperties(PropertyDictionary? properties)
    {
        if (properties != null)
        {
            properties.CheckExistingKeys = true;
        }
        // Bulk install forces dictionary mode (string keys live in the dictionary, not a shape).
        if (this is JsObject jo)
        {
            jo.ClearShape();
        }
        _properties = properties;
        unchecked { _propertiesVersion++; }
    }

    internal void SetSymbols(SymbolDictionary? symbols)
    {
        _symbols = symbols;
    }

    /// <summary>
    /// Falls back from shape mode to the legacy dictionary representation, copying each slot into a
    /// freshly-built <see cref="PropertyDictionary"/> as an ordinary CEW data descriptor (in slot =
    /// insertion order). After this the object is byte-for-byte the pre-shapes representation, so every
    /// consumer runs the unchanged dictionary code. No-op when not a shape-mode <see cref="JsObject"/>.
    /// <para>
    /// A slot still holding a lazy layout sentinel becomes a <see cref="LazySlotPropertyDescriptor"/> rather
    /// than being materialized, so deleting one key — or freezing the object — does not force every lazy
    /// member's factory to run.
    /// </para>
    /// </summary>
    internal void ConvertToDictionaryMode()
    {
        if ((_type & InternalTypes.ShapeMode) == InternalTypes.Empty)
        {
            return;
        }

        var jo = Unsafe.As<JsObject>(this);
        var shape = jo.ShapeOf;
        var slotCount = shape.SlotCount;
        // checkExistingKeys: false makes the initial fill cheap (shape keys are distinct by construction).
        var properties = new PropertyDictionary(slotCount, checkExistingKeys: false);
        if (slotCount > 0)
        {
            var keys = new Key[slotCount];
            shape.CollectKeys(keys);
            if ((_type & InternalTypes.HasLazySlots) == InternalTypes.Empty)
            {
                for (var i = 0; i < slotCount; i++)
                {
                    properties[keys[i]] = new PropertyDescriptor(jo.GetSlot(i), PropertyFlag.ConfigurableEnumerableWritable);
                }
            }
            else
            {
                for (var i = 0; i < slotCount; i++)
                {
                    var value = jo.GetSlot(i);
                    properties[keys[i]] = value is JsObject.UnmaterializedSlots sentinel
                        ? new LazySlotPropertyDescriptor(jo, sentinel, i)
                        : new PropertyDescriptor(value, PropertyFlag.ConfigurableEnumerableWritable);
                }
            }
        }
        // The object is now a live mutable dictionary; re-setting an existing key (e.g. defineProperty
        // replacing a data property with an accessor) must replace, not append a duplicate. Mirrors
        // SetProperties.
        properties.CheckExistingKeys = true;

        jo.ClearShape();
        _properties = properties;
        unchecked { _propertiesVersion++; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetProperty(JsValue property, PropertyDescriptor value)
    {
        if (property is JsString jsString)
        {
            SetProperty(jsString.ToString(), value);
        }
        else
        {
            SetPropertyUnlikely(property, value);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetProperty(string property, PropertyDescriptor value)
    {
        Key key = property;
        SetProperty(key, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetProperty(Key property, PropertyDescriptor value)
    {
        // A raw store must apply on top of the initialized state: on a lazily-initialized host the
        // pending Initialize() would otherwise replace the property bag and silently drop this write.
        EnsureInitialized();

        // Storing a raw descriptor is a dictionary-mode operation; deopt first if needed.
        if ((_type & InternalTypes.ShapeMode) != InternalTypes.Empty)
        {
            ConvertToDictionaryMode();
        }
        else if ((_type & InternalTypes.BuiltinShapeMode) != InternalTypes.Empty)
        {
            var shaped = Unsafe.As<IBuiltinShaped>(this);
            if (shaped.BuiltinShape.Index.TryGetValue(property.Name, out var slot))
            {
                // raw replace of an existing shape slot happens in place
                shaped.BuiltinDescriptors![slot] = value;
                unchecked { _propertiesVersion++; }
                return;
            }

            if (TryHybridAddToShapedHost(property, value))
            {
                return;
            }

            // integer-like key: the fixed layout can't express the required own-key order
            DeoptBuiltinShape();
        }
        _properties ??= new PropertyDictionary();
        _properties[property] = value;
        unchecked { _propertiesVersion++; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void SetPropertyUnlikely(JsValue property, PropertyDescriptor value)
    {
        var propertyKey = TypeConverter.ToPropertyKey(property);
        if (!property.IsSymbol())
        {
            // route through the Key overload so shape/builtin-shape modes are handled
            SetProperty(TypeConverter.ToString(propertyKey), value);
        }
        else
        {
            // same pre-initialization hazard as SetProperty(Key): Initialize() replaces _symbols
            EnsureInitialized();
            _symbols ??= new SymbolDictionary();
            _symbols[(JsSymbol) propertyKey] = value;
        }
    }

    internal void ClearProperties()
    {
        if (this is JsObject jo)
        {
            jo.ClearShape();
        }
        _properties?.Clear();
        _symbols?.Clear();
    }

    /// <summary>
    /// Enumerates this object's own properties as key/descriptor pairs. The base implementation yields the
    /// stored string keys first, then the symbols; the string keys come out in storage order (slot order
    /// when shaped, insertion order otherwise), which is not the specification's own-key order —
    /// integer-like keys are not hoisted and sorted the way <see cref="GetOwnPropertyKeys"/> does it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Script-visible enumeration does not go through this method.</b> <c>Object.keys</c> /
    /// <c>values</c> / <c>entries</c>, <c>for..in</c>, object spread and rest, <c>Object.assign</c>,
    /// <c>JSON.stringify</c> and <see cref="Native.Json.JsonSerializer"/> all list keys with
    /// <see cref="GetOwnPropertyKeys"/> and then filter them with
    /// <see cref="ProbeOwnProperty"/> — neither of which consults <c>GetOwnProperties</c>. Overriding only
    /// this method therefore leaves every one of those seeing whatever the base
    /// <see cref="GetOwnPropertyKeys"/> reports, which for a host object projecting its properties from
    /// native state is the engine's own (usually empty) property tables. A host that wants its properties
    /// enumerable to script must override <see cref="GetOwnPropertyKeys"/>, and should override
    /// <see cref="ProbeOwnProperty"/> alongside it so existence and enumerability are answered without
    /// materializing a descriptor per key.
    /// </para>
    /// <para>
    /// What does route through it: converting this object to a CLR value
    /// (<see cref="JsValue.ToObject()"/> when <c>Options.Interop.CreateClrObject</c> is configured),
    /// <c>GetSmallestIndex</c> on the array-like operation path, the debugger's binding-name enumeration
    /// (<c>GlobalEnvironment</c> / <c>ObjectEnvironment</c>), and the debug view. Overrides in the box chain
    /// to <c>base.GetOwnProperties()</c> to combine their exotic own properties with the stored ones.
    /// </para>
    /// </remarks>
    public virtual IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
    {
        EnsureInitialized();

        if ((_type & InternalTypes.ShapeMode) != InternalTypes.Empty)
        {
            var jo = Unsafe.As<JsObject>(this);
            var shape = jo.ShapeOf;
            var slotCount = shape.SlotCount;
            if (slotCount > 0)
            {
                var keys = new Key[slotCount];
                shape.CollectKeys(keys);
                for (var i = 0; i < slotCount; i++)
                {
                    yield return new KeyValuePair<JsValue, PropertyDescriptor>(new JsString(keys[i].Name), new SlotPropertyDescriptor(jo, i));
                }
            }
        }
        else if ((_type & InternalTypes.BuiltinShapeMode) != InternalTypes.Empty)
        {
            var shaped = Unsafe.As<IBuiltinShaped>(this);
            var names = shaped.BuiltinShape.Names;
            for (var i = 0; i < names.Length; i++)
            {
                yield return new KeyValuePair<JsValue, PropertyDescriptor>(JsString.Create(names[i].Name), MaterializeBuiltinSlot(shaped, i));
            }

            // hybrid additions (added after every shape name, preserving insertion order)
            if (_properties != null)
            {
                foreach (var pair in _properties)
                {
                    yield return new KeyValuePair<JsValue, PropertyDescriptor>(new JsString(pair.Key), pair.Value);
                }
            }
        }
        else if (_properties != null)
        {
            foreach (var pair in _properties)
            {
                yield return new KeyValuePair<JsValue, PropertyDescriptor>(new JsString(pair.Key), pair.Value);
            }
        }

        if (_symbols != null)
        {
            foreach (var pair in _symbols)
            {
                yield return new KeyValuePair<JsValue, PropertyDescriptor>(pair.Key, pair.Value);
            }
        }
    }

    public virtual List<JsValue> GetOwnPropertyKeys(Types types = Types.String | Types.Symbol)
    {
        EnsureInitialized();

        if ((_type & InternalTypes.ShapeMode) != InternalTypes.Empty)
        {
            return GetOwnPropertyKeysFromShape(Unsafe.As<JsObject>(this).ShapeOf, types);
        }

        if ((_type & InternalTypes.BuiltinShapeMode) != InternalTypes.Empty)
        {
            return GetBuiltinShapeOwnPropertyKeys(types);
        }

        var returningSymbols = (types & Types.Symbol) != Types.Empty && _symbols?.Count > 0;
        var returningStringKeys = (types & Types.String) != Types.Empty && _properties?.Count > 0;

        var propertyKeys = new List<JsValue>();
        if ((types & Types.String) != Types.Empty)
        {
            var initialOwnStringPropertyKeys = GetInitialOwnStringPropertyKeys();
            if (!ReferenceEquals(initialOwnStringPropertyKeys, System.Linq.Enumerable.Empty<JsValue>()))
            {
                propertyKeys.AddRange(initialOwnStringPropertyKeys);
            }
        }

        // check fast case where we don't need to sort, which should be the common case
        if (!returningSymbols)
        {
            if (!returningStringKeys)
            {
                return propertyKeys;
            }

            var propertyKeyCount = propertyKeys.Count;
            propertyKeys.Capacity += _properties!.Count;
            foreach (var pair in _properties)
            {
                // check if we can rely on the property name not being an unsigned number
                var c = pair.Key.Name.Length > 0 ? pair.Key.Name[0] : 'a';
                if (char.IsDigit(c) && propertyKeyCount + _properties.Count > 1)
                {
                    // jump to slow path, return list to original state
                    propertyKeys.RemoveRange(propertyKeyCount, propertyKeys.Count - propertyKeyCount);
                    return GetOwnPropertyKeysSorted(propertyKeys, returningStringKeys, returningSymbols);
                }
                propertyKeys.Add(new JsString(pair.Key.Name));
            }

            // seems good
            return propertyKeys;
        }

        if ((types & Types.String) == Types.Empty && (types & Types.Symbol) != Types.Empty)
        {
            // only symbols requested
            if (_symbols != null)
            {
                foreach (var pair in _symbols!)
                {
                    propertyKeys.Add(pair.Key);
                }
            }
            return propertyKeys;
        }

        return GetOwnPropertyKeysSorted(propertyKeys, returningStringKeys, returningSymbols);
    }

    /// <summary>
    /// Own string-keyed property names for for-in enumeration. Shape- and builtin-shape-mode objects hand
    /// back a shared, memoized <see cref="JsValue"/>[] of <see cref="JsString"/> instances — no
    /// per-enumeration List / JsString / scratch Key[] allocation; every other object (dictionaries, and
    /// exotics like proxies whose ownKeys trap must still fire) builds the usual fresh list through the
    /// virtual <see cref="GetOwnPropertyKeys"/>. The returned list MUST be treated as read-only: the shared
    /// arrays back every object of that layout. for-in re-probes each candidate against the live object, so
    /// a snapshot that is a shared immutable array is safe (deletes/adds are handled at step time).
    /// </summary>
    internal IReadOnlyList<JsValue> GetForInStringKeys()
    {
        EnsureInitialized();

        if ((_type & InternalTypes.ShapeMode) != InternalTypes.Empty)
        {
            var shape = Unsafe.As<JsObject>(this).ShapeOf;
            if (shape.TryGetOrderedKeyStrings(out var keyStrings))
            {
                return keyStrings;
            }

            // integer-index-like keys need numeric-sorted order → fall through to the dictionary path
            // (GetOwnPropertyKeys deopts and sorts).
        }
        else if ((_type & InternalTypes.BuiltinShapeMode) != InternalTypes.Empty
                 && _properties is null
                 && (this is not Array.ArrayInstance arrayInstance || !arrayInstance.HasAnyOwnIndex())
                 && ReferenceEquals(GetInitialOwnStringPropertyKeys(), System.Linq.Enumerable.Empty<JsValue>()))
        {
            // No function-derived length/name/prototype prefix, no hybrid dictionary additions, and no
            // exotic index storage (Array.prototype is array-backed: script CAN place elements on it,
            // which the shared name list would miss), so the built-in's shared, ordered name list is
            // exactly its own string keys — e.g. Object.prototype (the common deeper for-in level).
            return Unsafe.As<IBuiltinShaped>(this).BuiltinShape.NamesAsJsStrings;
        }

        return GetOwnPropertyKeys(Types.String);
    }

    /// <summary>
    /// True when this object provably has no enumerable own string-keyed property, used by the
    /// for-in array fast path to prove a prototype chain contributes nothing to the enumeration.
    /// The default is a conservative <c>false</c> ("cannot prove it"), which routes for-in through
    /// the exact snapshot machinery; only types with ordinary own-key semantics override this with
    /// <see cref="HasNoEnumerableOwnStringKeysCore"/>. A wrong <c>true</c> would silently drop keys
    /// from enumeration, so exotic key providers (proxies, string wrappers, arguments) must never
    /// opt in.
    /// </summary>
    internal virtual bool HasNoEnumerableOwnStringKeys() => false;

    /// <summary>
    /// Shared implementation for <see cref="HasNoEnumerableOwnStringKeys"/> overrides: handles the
    /// shape, builtin-shape and dictionary representations. Callers layer their own exotic keys on
    /// top (e.g. array indices).
    /// </summary>
    private protected bool HasNoEnumerableOwnStringKeysCore()
    {
        EnsureInitialized();

        if ((_type & InternalTypes.ShapeMode) != InternalTypes.Empty)
        {
            // every shape slot is a configurable+enumerable+writable data property
            return Unsafe.As<JsObject>(this).ShapeOf.SlotCount == 0;
        }

        if (!ReferenceEquals(GetInitialOwnStringPropertyKeys(), System.Linq.Enumerable.Empty<JsValue>()))
        {
            // an exotic initial-keys provider (function length/name, string indices); give up
            return false;
        }

        if ((_type & InternalTypes.BuiltinShapeMode) != InternalTypes.Empty)
        {
            var shaped = Unsafe.As<IBuiltinShaped>(this);
            if (!shaped.BuiltinShape.FixedSlotsAllNonEnumerable)
            {
                return false;
            }

            // Instance slots, materialized functions/accessors and in-place redefinitions all live
            // here; a null entry is an untouched slot whose declared flags were checked above.
            var descriptors = shaped.BuiltinDescriptors;
            if (descriptors is not null)
            {
                foreach (var descriptor in descriptors)
                {
                    if (descriptor is not null && descriptor.Enumerable)
                    {
                        return false;
                    }
                }
            }
        }

        return HasNoEnumerableEntryInPropertyDictionary();
    }

    private bool HasNoEnumerableEntryInPropertyDictionary()
    {
        var properties = _properties;
        if (properties is null || properties.Count == 0)
        {
            return true;
        }

        foreach (var entry in properties)
        {
            if (entry.Value.Enumerable)
            {
                return false;
            }
        }

        return true;
    }

    private List<JsValue> GetOwnPropertyKeysFromShape(Shape shape, Types types)
    {
        var slotCount = shape.SlotCount;
        var propertyKeys = new List<JsValue>();

        if ((types & Types.String) != Types.Empty)
        {
            var initialOwnStringPropertyKeys = GetInitialOwnStringPropertyKeys();
            if (!ReferenceEquals(initialOwnStringPropertyKeys, System.Linq.Enumerable.Empty<JsValue>()))
            {
                propertyKeys.AddRange(initialOwnStringPropertyKeys);
            }

            if (slotCount > 0)
            {
                // Spec ordering puts integer-index keys first (ascending) then string keys in insertion
                // order. Shape slots are insertion order; if any key looks like an array index the memo
                // declines, so we deopt and reuse the dictionary path's numeric sort (rare for literals).
                // Otherwise reuse the shape's shared JsString instances — no per-enumeration JsString and
                // no scratch Key[] allocation.
                if (shape.TryGetOrderedKeyStrings(out var keyStrings))
                {
                    propertyKeys.AddRange(keyStrings);
                }
                else
                {
                    ConvertToDictionaryMode();
                    return GetOwnPropertyKeys(types);
                }
            }
        }

        if ((types & Types.Symbol) != Types.Empty && _symbols != null)
        {
            foreach (var pair in _symbols)
            {
                propertyKeys.Add(pair.Key);
            }
        }

        return propertyKeys;
    }

    private List<JsValue> GetOwnPropertyKeysSorted(List<JsValue> initialOwnPropertyKeys, bool returningStringKeys, bool returningSymbols)
    {
        var keys = new List<JsValue>((_properties?.Count ?? 0) + (_symbols?.Count ?? 0) + initialOwnPropertyKeys.Count);
        if (returningStringKeys && _properties != null)
        {
            // Integer-index keys must come first in ascending numeric order. Collect their already-parsed
            // uint indices and sort those directly, instead of materializing JsStrings and re-parsing each
            // one back to a Number on every sort comparison — TypeConverter.ToNumber → Number.TryParseNumber
            // dominated JSON.stringify / enumeration of index-keyed objects (Kraken json-stringify-tinderbox).
            List<uint>? indices = null;
            foreach (var pair in _properties)
            {
                var propertyName = pair.Key.Name;
                var arrayIndex = ArrayInstance.ParseArrayIndex(propertyName);

                if (arrayIndex < ArrayOperations.MaxArrayLength)
                {
                    (indices ??= new List<uint>(_properties.Count)).Add(arrayIndex);
                }
                else
                {
                    initialOwnPropertyKeys.Add(new JsString(propertyName));
                }
            }

            if (indices != null)
            {
                indices.Sort();
                foreach (var arrayIndex in indices)
                {
                    keys.Add(JsString.Create(arrayIndex));
                }
            }
        }

        keys.AddRange(initialOwnPropertyKeys);

        if (returningSymbols)
        {
            foreach (var pair in _symbols!)
            {
                keys.Add(pair.Key);
            }
        }

        return keys;
    }

    // Returns the shared Enumerable.Empty<JsValue>() sentinel (not a fresh []): callers detect "no
    // function-derived length/name/prototype prefix" via ReferenceEquals against that same singleton.
    internal virtual IEnumerable<JsValue> GetInitialOwnStringPropertyKeys() => System.Linq.Enumerable.Empty<JsValue>();

    protected virtual bool TryGetProperty(JsValue property, [NotNullWhen(true)] out PropertyDescriptor? descriptor)
    {
        descriptor = null;

        var key = TypeConverter.ToPropertyKey(property);
        if (!key.IsSymbol())
        {
            var name = TypeConverter.ToString(key);
            if ((_type & InternalTypes.ShapeMode) != InternalTypes.Empty)
            {
                var jo = Unsafe.As<JsObject>(this);
                if (jo.ShapeOf.TryGetSlot(name, out var slot))
                {
                    descriptor = new SlotPropertyDescriptor(jo, slot);
                    return true;
                }

                return false;
            }

            return _properties?.TryGetValue(name, out descriptor) == true;
        }

        return _symbols?.TryGetValue((JsSymbol) key, out descriptor) == true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasOwnProperty(JsValue property)
    {
        return ProbeOwnPropertyChecked(property) != OwnPropertyProbe.Missing;
    }

    public virtual void RemoveOwnProperty(JsValue property)
    {
        EnsureInitialized();

        var key = TypeConverter.ToPropertyKey(property);
        if (!key.IsSymbol())
        {
            var name = TypeConverter.ToString(key);

            // Removing a string property can't be expressed as a shape / built-in-shape layout;
            // deopt first — except a hybrid addition on a shaped host, which lives in the side
            // dictionary and removes directly.
            if ((_type & InternalTypes.ShapeMode) != InternalTypes.Empty)
            {
                ConvertToDictionaryMode();
            }
            else if ((_type & InternalTypes.BuiltinShapeMode) != InternalTypes.Empty
                     && Unsafe.As<IBuiltinShaped>(this).BuiltinShape.Index.TryGetValue(name, out _))
            {
                DeoptBuiltinShape();
            }
            _properties?.Remove(name);
            unchecked { _propertiesVersion++; }
            return;
        }

        _symbols?.Remove((JsSymbol) key);
    }

    /// <summary>
    /// Overrides the <see cref="PropertyAccessSemantics"/> the engine derived for this type. Needed only for
    /// the two shapes the derivation rule cannot see: a type that overrides
    /// <see cref="Get(JsValue, JsValue)"/> and is nevertheless ordinary (declare
    /// <see cref="PropertyAccessSemantics.Ordinary"/> to get the short read path back), and a type that does
    /// not override it yet still is not ordinary (declare <see cref="PropertyAccessSemantics.Exotic"/>).
    /// See <see cref="PropertyAccessSemantics"/> for the invariant each value promises.
    /// </summary>
    /// <remarks>
    /// Must be called from the constructor, before the instance becomes reachable from script: the engine
    /// caches reads against the resolved semantics and does not re-check them. The last call wins.
    /// </remarks>
    protected void SetPropertyAccessSemantics(PropertyAccessSemantics semantics)
    {
        switch (semantics)
        {
            case PropertyAccessSemantics.Ordinary:
                _type = (_type & ~InternalTypes.ExoticGet) | InternalTypes.OrdinaryGet;
                break;
            case PropertyAccessSemantics.Exotic:
                _type = (_type & ~InternalTypes.OrdinaryGet) | InternalTypes.ExoticGet;
                break;
            default:
                Throw.ArgumentOutOfRangeException(nameof(semantics), semantics.ToString());
                break;
        }
    }

    /// <summary>
    /// The derived <see cref="PropertyAccessSemantics"/> flag for a subclass, resolved from the type once and
    /// cached. Keyed by <see cref="Type"/> and shared process-wide because the answer depends only on the
    /// type's own virtual dispatch, never on the engine, the realm or the instance — so a per-engine cache
    /// would multiply the reflection without changing an answer.
    /// <para>
    /// Retention: the value is an enum and holds no back-reference, so an entry retains nothing but its
    /// <see cref="Type"/> key — the same shape as the reflection caches in <c>ReflectionExtensions</c> and
    /// <see cref="JsValue"/>. Only subclasses reaching the public constructor are ever added.
    /// </para>
    /// </summary>
    private static readonly ConcurrentDictionary<Type, InternalTypes> _derivedAccessSemantics = new();

    /// <summary>
    /// How many times the reflection probe actually ran for a type — one, however many instances are built.
    /// Exposed so a test can pin that, since the derivation is otherwise indistinguishable from probing per
    /// instance. Written only on a cache miss.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, int> _accessSemanticsProbes = new();

    internal static int AccessSemanticsProbeCount(Type type)
        => _accessSemanticsProbes.TryGetValue(type, out var count) ? count : 0;

    /// <summary>
    /// Whether a type's <see cref="ProbeOwnProperty"/> override was declared outside the Jint assembly, which is
    /// exactly when <see cref="ProbeOwnPropertyChecked"/> has something to verify. Cached per <see cref="Type"/>
    /// and shared process-wide for the same reason <see cref="_derivedAccessSemantics"/> is: the answer depends
    /// only on the type, and an entry retains nothing but its <see cref="Type"/> key.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, bool> _hostDeclaredProbe = new();

    private static bool HasHostDeclaredProbe(Type type)
        => _hostDeclaredProbe.TryGetValue(type, out var declared)
            ? declared
            : HasHostDeclaredProbeUncached(type);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool HasHostDeclaredProbeUncached(Type type)
        => _hostDeclaredProbe.GetOrAdd(type, static t => ProbeIsHostDeclared(t));

    /// <summary>
    /// In-box overrides of <see cref="ProbeOwnProperty"/> are covered by this repository's own tests, and
    /// <see cref="ArrayLikeObject"/>'s is verified through its <c>HasIndex</c>/<c>TryGetIndex</c> agreement
    /// check instead, so verifying them again would only make every enumeration in the engine cost a descriptor
    /// per key with nothing to find. An inconclusive probe answers <see langword="false"/>, which forgoes a
    /// check and never changes behaviour.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2070",
        Justification = "Reads one well-known virtual member of this very type's own hierarchy, which the trimmer keeps because the engine calls it virtually. If the metadata is unavailable the probe answers false, which only forgoes a verification that is itself opt-in.")]
    private static bool ProbeIsHostDeclared(Type type)
    {
        var probe = type.GetMethod(
            nameof(ProbeOwnProperty),
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            types: [typeof(JsValue)],
            modifiers: null);

        return probe?.DeclaringType is { } declaringType
            && declaringType.Assembly != typeof(ObjectInstance).Assembly;
    }

    private static InternalTypes DeriveAccessSemantics(Type type)
        => _derivedAccessSemantics.TryGetValue(type, out var semantics)
            ? semantics
            : DeriveAccessSemanticsUncached(type);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static InternalTypes DeriveAccessSemanticsUncached(Type type)
        => _derivedAccessSemantics.GetOrAdd(type, static t =>
        {
            _accessSemanticsProbes.AddOrUpdate(t, 1, static (_, count) => count + 1);
            return ProbeAccessSemantics(t);
        });

    /// <summary>
    /// Decides a subclass's read semantics from the one thing that can make them deviate: whether the type
    /// overrides <see cref="Get(JsValue, JsValue)"/>. A type that does not override it <em>has</em> the ordinary
    /// implementation, so <see cref="PropertyAccessSemantics.Ordinary"/> is correct for it by construction —
    /// overriding <see cref="GetOwnProperty"/> alone does not change that, because the single probe the
    /// interpreter then does is exactly the one <c>base.Get</c> would have done. A type that overrides it may
    /// deviate, and the engine cannot tell whether it does, so it is treated as
    /// <see cref="PropertyAccessSemantics.Exotic"/> until the type says otherwise.
    /// <para>
    /// The same reflection answers a second, orthogonal question: whether the type overrides
    /// <see cref="TryGetOwnPropertyValue"/>, in which case it also gets
    /// <see cref="InternalTypes.OwnValueHook"/> and the read lanes ask it instead of building a descriptor.
    /// That one is a pure routing decision — the base implementation answers exactly what
    /// <see cref="GetOwnProperty"/> does, so a missing flag costs a descriptor and never an answer.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Everything the probe cannot answer resolves to <see cref="InternalTypes.ExoticGet"/> without
    /// <see cref="InternalTypes.OwnValueHook"/>: routing every read through <c>Get</c> and building the
    /// descriptor are both always observably correct, so an inconclusive probe costs speed and never
    /// correctness. A member hidden with <c>new</c> rather than overridden also lands there for <c>Get</c> —
    /// the engine would never dispatch to it, so the answer is merely pessimistic — and lands on the
    /// <em>set</em> side for the hook, where the engine then calls the base implementation through the vtable
    /// and gets the descriptor-driven answer anyway.
    /// </remarks>
    [UnconditionalSuppressMessage("Trimming", "IL2070",
        Justification = "Reads two well-known virtual members of this very type's own hierarchy, which the trimmer keeps because the engine calls them virtually. If the metadata is unavailable the probe answers ExoticGet without OwnValueHook, which is the conservative outcome for both and preserves observable behaviour.")]
    private static InternalTypes ProbeAccessSemantics(Type type)
    {
        var get = type.GetMethod(
            nameof(Get),
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: [typeof(JsValue), typeof(JsValue)],
            modifiers: null);

        var semantics = get is not null && get.DeclaringType == typeof(ObjectInstance)
            ? InternalTypes.OrdinaryGet
            : InternalTypes.ExoticGet;

        var ownValue = type.GetMethod(
            nameof(TryGetOwnPropertyValue),
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            types: [typeof(JsValue), typeof(JsValue), typeof(JsValue).MakeByRefType()],
            modifiers: null);

        if (ownValue is not null && ownValue.DeclaringType != typeof(ObjectInstance))
        {
            semantics |= InternalTypes.OwnValueHook;
        }

        return semantics;
    }

    public override JsValue Get(JsValue property, JsValue receiver)
    {
        if ((_type & (InternalTypes.PlainObject | InternalTypes.BuiltinShapeMode)) == InternalTypes.PlainObject && _initialized && ReferenceEquals(this, receiver) && property.IsString())
        {
            EnsureInitialized();
            if ((_type & InternalTypes.ShapeMode) != InternalTypes.Empty)
            {
                var jo = Unsafe.As<JsObject>(this);
                if (jo.ShapeOf.TryGetSlot(property.ToString(), out var slot))
                {
                    // Checked read rather than the raw slot: this is the semi-hot generic lane (computed
                    // keys, Reflect.get, with, proxy-forwarded reads) and it serves every shaped object, so
                    // it carries the lazy-slot check unconditionally instead of being duplicated per flag.
                    return jo.GetSlotForRead(slot);
                }

                return Prototype?.Get(property, receiver) ?? Undefined;
            }

            if (_properties?.TryGetValue(property.ToString(), out var ownDesc) == true)
            {
                return UnwrapJsValue(ownDesc, receiver);
            }

            return Prototype?.Get(property, receiver) ?? Undefined;
        }

        // slow path — a host that overrides TryGetOwnPropertyValue answers the own-property question from its
        // own storage, so a read it serves costs no descriptor at all. `false` is an *authoritative* own miss
        // (see the contract on the member), so the read continues up the prototype chain without asking a
        // second time. Covers the reads the interpreter's member lane never sees: computed keys (obj[i]),
        // Reflect.get, super, built-ins, and the base of a member call. Gated on the derived flag rather than
        // called unconditionally, so every object that does not override the hook — which is every in-box type
        // and every host that never heard of it — pays one test on the already-loaded _type instead of a
        // virtual call that would only turn around and probe GetOwnProperty.
        if ((_type & InternalTypes.OwnValueHook) != InternalTypes.Empty)
        {
            if (TryGetOwnPropertyValue(property, receiver, out var ownValue))
            {
                if (HostContractVerification.Enabled)
                {
                    AssertOwnValueAgreesWithDescriptor(this, property, receiver, answered: true, ownValue);
                }

                return ownValue;
            }

            if (HostContractVerification.Enabled)
            {
                AssertOwnValueAgreesWithDescriptor(this, property, receiver, answered: false, Undefined);
            }

            return Prototype?.Get(property, receiver) ?? Undefined;
        }

        var desc = GetOwnProperty(property);
        if (desc != PropertyDescriptor.Undefined)
        {
            return UnwrapJsValue(desc, receiver);
        }

        return Prototype?.Get(property, receiver) ?? Undefined;
    }

    /// <summary>
    /// Verifier for <see cref="TryGetOwnPropertyValue"/>, checking <b>both</b> directions of its
    /// contract against <see cref="GetOwnProperty"/> for the same key: a <c>true</c> answer must carry exactly
    /// what the descriptor would have unwrapped to, and a <c>false</c> answer must mean the descriptor is
    /// <see cref="PropertyDescriptor.Undefined"/>. Gated on <see cref="HostContractVerification.Enabled"/>, so a
    /// host's own suite run against a Debug Jint — or against the shipped Release package with the
    /// <c>Jint.EnableHostContractVerification</c> switch set — becomes the checker, and every other process pays
    /// nothing. The value comparison is skipped when unwrapping would be observable (an accessor or a
    /// custom-valued descriptor).
    /// <para>
    /// It is a second verifier rather than a reuse of <c>AssertOrdinaryGetAgrees</c>: that one recomputes the
    /// read through <c>Get</c>, and <c>Get</c> consults this hook too, so it can no longer catch a hook that
    /// disagrees with <c>GetOwnProperty</c>. This one asks <c>GetOwnProperty</c> directly.
    /// </para>
    /// </summary>
    internal static void AssertOwnValueAgreesWithDescriptor(ObjectInstance target, JsValue property, JsValue receiver, bool answered, JsValue value)
    {
        var descriptor = target.GetOwnProperty(property);
        var owns = !ReferenceEquals(descriptor, PropertyDescriptor.Undefined);

        if (!owns)
        {
            if (answered)
            {
                HostContractVerification.Fail($"{target.GetType()}.TryGetOwnPropertyValue answered '{property}' but its GetOwnProperty reports that property as absent.");
            }

            return;
        }

        if (!answered)
        {
            HostContractVerification.Fail($"{target.GetType()}.TryGetOwnPropertyValue returned false for '{property}' but its GetOwnProperty reports that property as present. Returning false states that there is no own property of that name and the read continues up the prototype chain; call base.TryGetOwnPropertyValue to hand the key back to GetOwnProperty instead.");
        }

        if ((descriptor._flags & (PropertyFlag.NonData | PropertyFlag.CustomJsValue)) != PropertyFlag.None)
        {
            return;
        }

        if (!SameValue(UnwrapJsValue(descriptor, receiver), value))
        {
            HostContractVerification.Fail($"{target.GetType()}.TryGetOwnPropertyValue answered '{property}' with a value its GetOwnProperty descriptor does not unwrap to.");
        }
    }

    /// <summary>
    /// Answers whether this object has the named <em>own</em> property and, if so, what it reads as — without
    /// materializing a <see cref="PropertyDescriptor"/>. A host that projects values out of native storage
    /// overrides this to hand the value over directly, instead of allocating a descriptor per read purely for
    /// <c>UnwrapJsValue</c> to throw away.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Contract:</b> the answer must be what <see cref="GetOwnProperty"/> would have given for the same key
    /// at the same instant. Return <c>true</c> with <paramref name="value"/> equal to that descriptor unwrapped
    /// for <paramref name="receiver"/>; return <c>false</c> <em>exactly</em> when <c>GetOwnProperty</c> would
    /// return <see cref="PropertyDescriptor.Undefined"/>. <c>false</c> is an authoritative statement that this
    /// object has no own property of that name — the engine trusts it, does not re-probe, and continues the
    /// read up the prototype chain. A <c>false</c> returned merely because the value was awkward to produce
    /// therefore does not fall back: it makes the read resolve on the prototype, or evaluate to
    /// <c>undefined</c>, for a property that exists. This is the same obligation
    /// <see cref="ProbeOwnProperty"/> carries, and a Debug build of Jint verifies both directions of it on
    /// every read.
    /// </para>
    /// <para>
    /// <b>When you cannot answer,</b> call <c>base.TryGetOwnPropertyValue(property, receiver, out value)</c>.
    /// The base implementation is the descriptor-driven answer — <c>GetOwnProperty</c> plus the unwrap — so
    /// deferring to it is always correct and costs exactly what not overriding this member would have. That is
    /// what an implementation which only understands, say, string names should do with everything else:
    /// <paramref name="property"/> is the key exactly as the caller supplied it and has <em>not</em> been
    /// through <c>ToPropertyKey</c>, which is the same key <c>GetOwnProperty</c> receives.
    /// </para>
    /// <para>
    /// <paramref name="receiver"/> is the <c>[[Get]]</c> receiver and differs from <c>this</c> only when the
    /// read reached this object as somebody else's prototype; it matters solely for accessor properties, which
    /// a value projection does not have.
    /// </para>
    /// <para>
    /// Overriding this is what puts the object on the lane — the engine derives
    /// <see cref="InternalTypes.OwnValueHook"/> from the runtime type once, so a type that does not override it
    /// is never asked and pays nothing.
    /// </para>
    /// </remarks>
    protected internal virtual bool TryGetOwnPropertyValue(JsValue property, JsValue receiver, out JsValue value)
    {
        var descriptor = GetOwnProperty(property);
        if (ReferenceEquals(descriptor, PropertyDescriptor.Undefined))
        {
            value = Undefined;
            return false;
        }

        value = UnwrapJsValue(descriptor, receiver);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal JsValue UnwrapJsValue(PropertyDescriptor desc)
    {
        return UnwrapJsValue(desc, this);
    }

    internal static JsValue UnwrapJsValue(PropertyDescriptor desc, JsValue thisObject)
    {
        var value = (desc._flags & PropertyFlag.CustomJsValue) != PropertyFlag.None
            ? desc.CustomValue
            : desc._value;

        // IsDataDescriptor inlined
        if ((desc._flags & (PropertyFlag.WritableSet | PropertyFlag.Writable)) != PropertyFlag.None || value is not null)
        {
            return value ?? Undefined;
        }

        return UnwrapFromGetter(desc, thisObject);
    }

    /// <summary>
    /// A rarer case.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static JsValue UnwrapFromGetter(PropertyDescriptor desc, JsValue thisObject)
    {
        var getter = desc.Get ?? Undefined;
        if (getter.IsUndefined())
        {
            return Undefined;
        }

        if (!getter.IsCallable)
        {
            return Undefined;
        }

        var callable = (ICallable) getter;
        return ((ObjectInstance) getter)._engine.Call(callable, thisObject, Arguments.Empty, null);
    }

    /// <summary>
    /// Returns the Property Descriptor of the named
    /// own property of this object, or undefined if
    /// absent.
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.12.1
    /// </summary>
    public virtual PropertyDescriptor GetOwnProperty(JsValue property)
    {
        EnsureInitialized();

        PropertyDescriptor? descriptor = null;
        var key = TypeConverter.ToPropertyKey(property);
        if (!key.IsSymbol())
        {
            var name = TypeConverter.ToString(key);
            if ((_type & (InternalTypes.ShapeMode | InternalTypes.BuiltinShapeMode)) != InternalTypes.Empty)
            {
                if ((_type & InternalTypes.ShapeMode) != InternalTypes.Empty)
                {
                    var jo = Unsafe.As<JsObject>(this);
                    if (jo.ShapeOf.TryGetSlot(name, out var slot))
                    {
                        return new SlotPropertyDescriptor(jo, slot);
                    }
                }
                else
                {
                    var shaped = Unsafe.As<IBuiltinShaped>(this);
                    if (shaped.BuiltinShape.Index.TryGetValue(name, out var slot))
                    {
                        return MaterializeBuiltinSlot(shaped, slot);
                    }

                    // hybrid addition on a shaped host (side dictionary holds post-init adds)
                    _properties?.TryGetValue(name, out descriptor);
                }
                // string key absent from the shape (and hybrid additions) ⇒ no own property
            }
            else
            {
                _properties?.TryGetValue(name, out descriptor);
            }
        }
        else
        {
            _symbols?.TryGetValue((JsSymbol) key, out descriptor);
        }

        return descriptor ?? PropertyDescriptor.Undefined;
    }

    /// <summary>
    /// Answers whether the named own property exists and is enumerable without materializing a
    /// <see cref="PropertyDescriptor"/>. Shape-mode objects (sealed <see cref="JsObject"/>,
    /// whose slots are always configurable/enumerable/writable — anything else deopts to
    /// dictionary mode) answer straight from the shape, and a builtin-shaped host answers a
    /// declared member from its shared layout without creating that member's function; every
    /// other object — including exotics like proxies (traps still fire), typed arrays and interop
    /// wrappers — routes through the virtual <see cref="GetOwnProperty"/>. Read-only callers that
    /// don't need the descriptor's value (existence checks, enumerability filters) should prefer
    /// this over GetOwnProperty.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Host objects whose <see cref="GetOwnProperty"/> is expensive — because it reflects over a CLR
    /// member, allocates a descriptor per key, or consults a backing store — can override this to answer
    /// existence and enumerability directly. It backs <c>in</c>, <c>hasOwnProperty</c>,
    /// <c>propertyIsEnumerable</c>, <c>Object.keys</c>/<c>values</c>/<c>entries</c>, <c>Object.assign</c>,
    /// <c>Object.defineProperties</c>, object spread and <c>JSON.stringify</c>, each of which otherwise
    /// materializes one descriptor per key purely to test it and a second one to read the value.
    /// </para>
    /// <para>
    /// <b>Contract:</b> the answer must agree with <see cref="GetOwnProperty"/> for the same key at the
    /// same point in time — <see cref="OwnPropertyProbe.Missing"/> exactly when GetOwnProperty returns
    /// <see cref="PropertyDescriptor.Undefined"/>, and otherwise
    /// <see cref="OwnPropertyProbe.Enumerable"/>/<see cref="OwnPropertyProbe.NonEnumerable"/> matching
    /// that descriptor's <see cref="PropertyDescriptor.Enumerable"/> flag. The engine trusts the probe
    /// and does not re-verify it on the hot path: an override that wrongly answers
    /// <see cref="OwnPropertyProbe.Missing"/> silently drops the key from every enumeration and copy
    /// above, with no error. Because that failure is so quiet, a build with host-contract verification on
    /// — a Debug build of Jint, or the shipped Release package with the
    /// <c>Jint.EnableHostContractVerification</c> AppContext switch set before its first use — checks
    /// every probe against <c>GetOwnProperty</c> and throws on the first disagreement. Running an
    /// integration suite that way is how a host proves its override honours this. The probe must also
    /// be side-effect free with respect to observable state; it is a filter, not an accessor invocation,
    /// and callers that need the value still call <c>Get</c> afterwards.
    /// </para>
    /// </remarks>
    /// <param name="property">The property key to probe: a <see cref="JsString"/>, a <see cref="JsSymbol"/> or a number-like key, never a private name.</param>
    protected internal virtual OwnPropertyProbe ProbeOwnProperty(JsValue property)
    {
        if ((_type & InternalTypes.ShapeMode) != InternalTypes.Empty && property is JsString jsString)
        {
            return Unsafe.As<JsObject>(this).ShapeOf.TryGetSlot(jsString.ToString(), out _)
                ? OwnPropertyProbe.Enumerable
                : OwnPropertyProbe.Missing;
        }

        if ((_type & InternalTypes.BuiltinShapeMode) != InternalTypes.Empty && property is JsString declaredName)
        {
            if (TryProbeBuiltinShapeSlot(declaredName.ToString(), out var slotProbe, out var nameDeclared))
            {
                if (HostContractVerification.Enabled)
                {
                    AssertBuiltinShapeProbeAgrees(this, property, slotProbe);
                }

                return slotProbe;
            }

            // Fast miss. The name is not in the shared layout index, no hybrid addition can own it (the side
            // dictionary is still empty) and this type declares nothing ahead of the layout, so nothing on
            // this object can own it either — the answer is already final and the confirming virtual
            // GetOwnProperty below would only redo the key conversion and the same index lookup. A DECLARED
            // name that merely could not be answered here (an unmaterialized Factory slot) reports
            // nameDeclared and keeps falling through, where GetOwnProperty materializes it and answers.
            if (!nameDeclared
                && _properties is null
                && (_type & InternalTypes.BuiltinShapeIndexAuthoritative) != InternalTypes.Empty)
            {
                if (HostContractVerification.Enabled)
                {
                    AssertBuiltinShapeProbeAgrees(this, property, OwnPropertyProbe.Missing);
                }

                return OwnPropertyProbe.Missing;
            }
        }

        var desc = GetOwnProperty(property);
        if (ReferenceEquals(desc, PropertyDescriptor.Undefined))
        {
            return OwnPropertyProbe.Missing;
        }

        return desc.Enumerable ? OwnPropertyProbe.Enumerable : OwnPropertyProbe.NonEnumerable;
    }

    /// <summary>
    /// The engine's own entry point to <see cref="ProbeOwnProperty"/>: identical to calling the virtual
    /// directly, plus the host-contract verification the virtual's documented obligation deserves. Every place
    /// the engine <em>consumes</em> a probe goes through here — the enumerations, the copies,
    /// <c>JSON.stringify</c>, the existence operators and the prototype-method cache's own-miss re-proof — so a
    /// host whose override contradicts its <see cref="GetOwnProperty"/> is caught wherever the damage would
    /// have been done. A <c>base.ProbeOwnProperty</c> call inside an override is not a consumption site and
    /// stays on the virtual.
    /// <para>
    /// A pure pass-through when verification is off: <see cref="HostContractVerification.Enabled"/> is a JIT
    /// constant, so this inlines to the virtual call alone and the hot member-read lane is unaffected.
    /// </para>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal OwnPropertyProbe ProbeOwnPropertyChecked(JsValue property)
    {
        var probe = ProbeOwnProperty(property);
        if (HostContractVerification.Enabled)
        {
            AssertHostProbeAgreesWithDescriptor(this, property, probe);
        }

        return probe;
    }

    /// <summary>
    /// Verifier for a host's <see cref="ProbeOwnProperty"/> override, checked against
    /// <see cref="GetOwnProperty"/> for the same key at the same instant. It is the hook whose failure mode is
    /// the quietest — the engine trusts the probe and does not re-verify it, so a wrong
    /// <see cref="OwnPropertyProbe.Missing"/> removes the key from every enumeration and copy with no error
    /// anywhere — which is exactly why it is worth paying a descriptor per key to check when a host asks for
    /// verification.
    /// </summary>
    private static void AssertHostProbeAgreesWithDescriptor(ObjectInstance target, JsValue property, OwnPropertyProbe probe)
    {
        if (!HasHostDeclaredProbe(target.GetType()))
        {
            return;
        }

        var expected = ExpectedProbe(target.GetOwnProperty(property));
        if (expected != probe)
        {
            HostContractVerification.Fail($"{target.GetType()}.ProbeOwnProperty answered '{property}' with {probe} but its GetOwnProperty reports {expected}. A probe must agree with GetOwnProperty at the same instant; the engine trusts it without re-verifying, so a wrong {nameof(OwnPropertyProbe.Missing)} silently drops the key from `in`, hasOwnProperty, propertyIsEnumerable, Object.keys/values/entries, Object.assign, spread and JSON.stringify.");
        }
    }

    /// <summary>
    /// Answers an existence/enumerability question about a <b>declared member</b> of a builtin-shaped host
    /// without materializing it. Web IDL-style prototypes declare their members enumerable, so a page walking
    /// them (<c>for-in</c>, <c>Object.keys</c>, <c>JSON.stringify</c>, spread) previously created every
    /// member's function object purely to read one flag off the descriptor it hung on.
    /// <para>
    /// A slot that already carries a live descriptor — a constant, a per-realm instance slot filled at
    /// initialization, a redefined member, or one materialized by an earlier read — is answered from that
    /// descriptor, so a redefine that changed enumerability is respected. An untouched function or accessor
    /// slot is answered from the enumerability the shape declares for it. A <see cref="BuiltinSlotKind.Factory"/>
    /// slot has no declared flags before its factory runs, so it declines, exactly as
    /// <see cref="BuiltinShape.FixedSlotsAllNonEnumerable"/> gives up on one.
    /// </para>
    /// <para>
    /// It declines for a name the shape does not declare too, but distinguishes that case through
    /// <paramref name="nameDeclared"/>: <c>false</c> means the shared layout index genuinely has no such
    /// slot, <c>true</c> means the name IS declared and merely could not be answered from the layout alone
    /// (the Factory case above). Only the first lets the caller answer an authoritative miss, and only for
    /// an object that qualifies — the two other ways a shaped host can own a string key the index does not
    /// list are excluded by the caller's gate, not here: a post-initialization addition lives in the side
    /// dictionary, and — since this runs on the base class — a subclass may resolve names ahead of the
    /// shared layout, as a <c>Function</c>-derived shaped host does for
    /// <c>length</c>/<c>name</c>/<c>prototype</c> and an array-backed one does for indices. Declining
    /// otherwise routes the name to the virtual <see cref="GetOwnProperty"/>, which is what every shaped
    /// host did before this lane existed.
    /// </para>
    /// </summary>
    private bool TryProbeBuiltinShapeSlot(string name, out OwnPropertyProbe probe, out bool nameDeclared)
    {
        var shaped = Unsafe.As<IBuiltinShaped>(this);
        var shape = shaped.BuiltinShape;
        if (!shape.Index.TryGetValue(name, out var slot))
        {
            probe = OwnPropertyProbe.Missing;
            nameDeclared = false;
            return false;
        }

        nameDeclared = true;

        // An alias carries no flags of its own: materializing it hands back the descriptor of the slot it
        // shares, so that slot's state is the one to read.
        while (shape.Kinds[slot] == BuiltinSlotKind.Alias)
        {
            slot = shape.FunctionSlots[slot];
        }

        var descriptor = shaped.BuiltinDescriptors![slot];
        if (descriptor is not null)
        {
            probe = descriptor.Enumerable ? OwnPropertyProbe.Enumerable : OwnPropertyProbe.NonEnumerable;
            return true;
        }

        if (shape.Kinds[slot] == BuiltinSlotKind.Factory)
        {
            probe = OwnPropertyProbe.Missing;
            return false;
        }

        probe = (shape.FunctionFlags[slot] & PropertyFlag.Enumerable) != PropertyFlag.None
            ? OwnPropertyProbe.Enumerable
            : OwnPropertyProbe.NonEnumerable;
        return true;
    }

    /// <summary>
    /// Checker for the lane above, gated on <see cref="HostContractVerification.Enabled"/>. The probe contract
    /// is trusted and never re-verified on the hot path — a wrong <see cref="OwnPropertyProbe.Missing"/>
    /// silently drops the key from every enumeration and copy that consults it — so a verifying build
    /// recomputes the answer the way the lane exists to avoid, straight from the virtual
    /// <see cref="GetOwnProperty"/>, and fails loudly on any disagreement. Materializing the slot is the price
    /// of checking: with verification on, a build therefore behaves exactly as every build did before this
    /// lane, which is also why a test that proves the lane materializes nothing must read figures from a
    /// process with verification off.
    /// </summary>
    private static void AssertBuiltinShapeProbeAgrees(ObjectInstance target, JsValue property, OwnPropertyProbe probe)
    {
        var expected = ExpectedProbe(target.GetOwnProperty(property));
        if (expected != probe)
        {
            HostContractVerification.Fail($"{target.GetType()} answered the shared-layout probe for '{property}' with {probe} but its GetOwnProperty reports {expected}. A probe must agree with GetOwnProperty at the same instant; the engine trusts it without re-verifying.");
        }
    }

    /// <summary>
    /// The <see cref="OwnPropertyProbe"/> a descriptor obliges <see cref="ProbeOwnProperty"/> to answer. Shared
    /// by both probe verifiers so the agreement matrix is stated once.
    /// </summary>
    private static OwnPropertyProbe ExpectedProbe(PropertyDescriptor descriptor)
    {
        if (ReferenceEquals(descriptor, PropertyDescriptor.Undefined))
        {
            return OwnPropertyProbe.Missing;
        }

        return descriptor.Enumerable ? OwnPropertyProbe.Enumerable : OwnPropertyProbe.NonEnumerable;
    }

    // Built-in-shape storage helpers (InternalTypes.BuiltinShapeMode). Shared by every host that implements
    // IBuiltinShaped — BuiltinShapeObject-derived namespaces today, generator-emitted prototypes/constructors
    // later — so the storage is composable across base classes that cannot share a single base. See BuiltinShape.

    // Materialize a shaped slot's descriptor. Constants point at the shared static descriptor; functions are
    // created on first access and stored so their identity is stable (the inline caches rely on this — a
    // materialize must never bump _propertiesVersion).
    private protected static PropertyDescriptor MaterializeBuiltinSlot(IBuiltinShaped shaped, int slot)
    {
        var descriptors = shaped.BuiltinDescriptors!;
        var descriptor = descriptors[slot];
        if (descriptor is null)
        {
            var shape = shaped.BuiltinShape;
            if (shape.Kinds[slot] == BuiltinSlotKind.Accessor)
            {
                var getterSlot = shape.FunctionSlots[slot];
                var setterSlot = shape.SetterSlots[slot];
                var getter = getterSlot == BuiltinShape.NotAFunction ? null : shaped.MakeBuiltinFunction(getterSlot);
                var setter = setterSlot == BuiltinShape.NotAFunction ? null : shaped.MakeBuiltinFunction(setterSlot);
                descriptor = new GetSetPropertyDescriptor(getter, setter, shape.FunctionFlags[slot]);
            }
            else if (shape.Kinds[slot] == BuiltinSlotKind.Alias)
            {
                // Share the target slot's descriptor so the two names resolve to the same function object
                // (spec identity, e.g. Set.prototype.keys === Set.prototype.values).
                descriptor = MaterializeBuiltinSlot(shaped, shape.FunctionSlots[slot]);
            }
            else if (shape.Kinds[slot] == BuiltinSlotKind.Factory)
            {
                descriptor = shape.Factories![slot]!(Unsafe.As<ObjectInstance>(shaped));
            }
            else
            {
                descriptor = new PropertyDescriptor(shaped.MakeBuiltinFunction(shape.FunctionSlots[slot]), shape.FunctionFlags[slot]);
            }
            descriptors[slot] = descriptor;
        }
        return descriptor;
    }

    /// <summary>
    /// Guarantees the ordinary dictionary representation for callers that add own properties
    /// through the raw property bag (e.g. the global var-binding protocol); no-op unless the
    /// host is currently in builtin-shape mode.
    /// </summary>
    internal void EnsureDictionaryProperties()
    {
        if ((_type & InternalTypes.BuiltinShapeMode) != InternalTypes.Empty)
        {
            DeoptBuiltinShape();
        }
    }

    // Fall back to the ordinary dictionary representation. Already-materialized slots keep their
    // descriptor instance (inline caches and spec identity depend on it); unmaterialized function
    // slots become lazy wrappers instead of forcing every dispatcher function into existence —
    // for a host like the global object, an eager deopt (triggered by any top-level `var`) would
    // otherwise instantiate dozens of functions nobody asked for. Unmaterialized accessors (rare)
    // materialize eagerly since a data-descriptor wrapper cannot defer them; aliases share their
    // target's entry so both names keep one function identity. Called when a shaped host gains or
    // loses an own string property (which the fixed layout cannot express).
    private void DeoptBuiltinShape()
    {
        var shaped = Unsafe.As<IBuiltinShaped>(this);
        var descriptors = shaped.BuiltinDescriptors;
        if (descriptors is null)
        {
            return;
        }

        var shape = shaped.BuiltinShape;
        var names = shape.Names;

        // First fill non-alias slots (reusing the per-realm array as scratch), then let aliases
        // pick up their target's instance — whether it was already materialized or is now lazy.
        for (var i = 0; i < names.Length; i++)
        {
            if (descriptors[i] is null && shape.Kinds[i] != BuiltinSlotKind.Alias)
            {
                descriptors[i] = shape.Kinds[i] switch
                {
                    // a data-descriptor wrapper cannot defer accessors; they materialize eagerly (rare)
                    BuiltinSlotKind.Accessor => MaterializeBuiltinSlot(shaped, i),
                    // factory results are cheap/lazy by contract (intrinsic refs resolve on first read)
                    BuiltinSlotKind.Factory => shape.Factories![i]!(this),
                    _ => new LazyBuiltinSlotDescriptor(shaped, shape.FunctionSlots[i], shape.FunctionFlags[i]),
                };
            }
        }

        var additions = _properties; // hybrid side-dictionary entries added after the shape names
        var properties = new PropertyDictionary(names.Length + (additions?.Count ?? 0), checkExistingKeys: false);
        for (var i = 0; i < names.Length; i++)
        {
            var descriptor = descriptors[i];
            if (descriptor is null)
            {
                // alias (possibly chained) — resolve to the ultimately shared instance
                var target = i;
                while (shape.Kinds[target] == BuiltinSlotKind.Alias)
                {
                    target = shape.FunctionSlots[target];
                }

                descriptor = descriptors[target]!;
                descriptors[i] = descriptor;
            }

            properties[names[i]] = descriptor;
        }

        if (additions is not null)
        {
            // preserve insertion order: every addition came after initialization
            foreach (var pair in additions)
            {
                properties[pair.Key] = pair.Value;
            }
        }

        _type &= ~(InternalTypes.BuiltinShapeMode | InternalTypes.BuiltinShapeIndexAuthoritative);
        shaped.BuiltinDescriptors = null;
        SetProperties(properties); // sets _properties, bumps version (symbols stay in _symbols)
    }

    private List<JsValue> GetBuiltinShapeOwnPropertyKeys(Types types)
    {
        var shaped = Unsafe.As<IBuiltinShaped>(this);
        var names = shaped.BuiltinShape.Names;
        var keys = new List<JsValue>(names.Length + (_properties?.Count ?? 0) + (_symbols?.Count ?? 0));
        if ((types & Types.String) != Types.Empty)
        {
            // Function-derived hosts surface length/name/prototype ahead of their shape members (matching the
            // dictionary path); ordinary hosts return Enumerable.Empty here.
            var initialOwnStringPropertyKeys = GetInitialOwnStringPropertyKeys();
            if (!ReferenceEquals(initialOwnStringPropertyKeys, System.Linq.Enumerable.Empty<JsValue>()))
            {
                keys.AddRange(initialOwnStringPropertyKeys);
            }
            // Reuse the built-in's shared JsString name instances instead of recreating them each call.
            keys.AddRange(shaped.BuiltinShape.NamesAsJsStrings);

            // hybrid additions came after initialization; integer-like keys force a full deopt
            // before ever landing here, so shape-names-then-additions is the spec own-key order
            if (_properties is not null)
            {
                foreach (var pair in _properties)
                {
                    keys.Add(JsString.Create(pair.Key.Name));
                }
            }
        }
        if ((types & Types.Symbol) != Types.Empty && _symbols is not null)
        {
            foreach (var pair in _symbols)
            {
                keys.Add(pair.Key);
            }
        }
        return keys;
    }

    // Install the shared layout + a per-realm descriptor array cloned from the shape's constant template,
    // and flip on BuiltinShapeMode. Called from a shaped host's generated CreateProperties_Generated (works
    // for both BuiltinShapeObject-derived hosts and generator-emitted IBuiltinShaped prototypes/constructors).
    private protected void InitializeBuiltinShape()
    {
        var shaped = Unsafe.As<IBuiltinShaped>(this);
        shaped.BuiltinDescriptors = (PropertyDescriptor?[]) shaped.BuiltinShape.ConstTemplate.Clone();
        _type |= BuiltinShapeModeBits();
    }

    /// <summary>
    /// The <c>_type</c> bits an object takes on when it enters built-in-shape storage. Besides
    /// <see cref="InternalTypes.BuiltinShapeMode"/> this derives
    /// <see cref="InternalTypes.BuiltinShapeIndexAuthoritative"/>, which gates the fast-miss lane in
    /// <see cref="ProbeOwnProperty"/>: a type overriding <see cref="GetInitialOwnStringPropertyKeys"/>
    /// resolves string names from its own fields ahead of the shared layout — <c>Function</c>'s
    /// <c>length</c>/<c>name</c>/<c>prototype</c>, <c>StringInstance</c>'s indices — so a layout-index miss
    /// there says nothing about whether the name is an own property, and such a type must not answer one.
    /// Everything else leaves the layout index and the hybrid side dictionary as the only declarers of a
    /// string key on the object. Asking is free: both overrides are sealed iterator methods, and an iterator
    /// that is never enumerated runs none of its body.
    /// </summary>
    internal InternalTypes BuiltinShapeModeBits()
        => InternalTypes.BuiltinShapeMode
           | (ReferenceEquals(GetInitialOwnStringPropertyKeys(), System.Linq.Enumerable.Empty<JsValue>())
               ? InternalTypes.BuiltinShapeIndexAuthoritative
               : InternalTypes.Empty);

    // Fill a per-realm instance-property slot (reserved via BuiltinShape.Builder.Instance) with its value
    // for this realm. Called from generated CreateProperties_Generated after InitializeBuiltinShape.
    private protected void SetBuiltinInstanceDescriptor(int slot, JsValue value, PropertyFlag flags)
    {
        Unsafe.As<IBuiltinShaped>(this).BuiltinDescriptors![slot] = new PropertyDescriptor(value, flags);
    }

    // Fills a slot reserved by [JsInstanceSlot] with a host-computed descriptor (e.g. a lazy cross-realm
    // alias). Call from a shaped host's Initialize, after CreateProperties_Generated.
    private protected void SetBuiltinSlotByName(string name, PropertyDescriptor descriptor)
    {
        var shaped = Unsafe.As<IBuiltinShaped>(this);
        if (shaped.BuiltinShape.Index.TryGetValue(name, out var slot))
        {
            shaped.BuiltinDescriptors![slot] = descriptor;
        }
    }

    protected internal virtual void SetOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        EnsureInitialized();
        if ((_type & InternalTypes.BuiltinShapeMode) != InternalTypes.Empty && property is JsString jsString)
        {
            var shaped = Unsafe.As<IBuiltinShaped>(this);
            if (shaped.BuiltinShape.Index.TryGetValue(jsString.ToString(), out var slot))
            {
                // Redefine an existing own property (e.g. data -> accessor) in place; no deopt needed.
                shaped.BuiltinDescriptors![slot] = desc;
                unchecked { _propertiesVersion++; }
                return;
            }

            // A brand-new own string property joins the hybrid side dictionary (the shape keeps
            // serving its slots); only integer-like keys force the full dictionary fallback.
            if (TryHybridAddToShapedHost(jsString.ToString(), desc))
            {
                return;
            }

            DeoptBuiltinShape();
        }
        SetProperty(property, desc);
    }

    /// <summary>
    /// Adds a post-initialization own property to a builtin-shaped host's side dictionary,
    /// keeping the shape alive for its slots — the caller has verified the name is not a shape
    /// slot. Integer-like keys must sort before string keys in own-key order, which the
    /// shape-then-additions enumeration cannot express, so they refuse and the caller deopts.
    /// </summary>
    internal bool TryHybridAddToShapedHost(Key name, PropertyDescriptor value)
    {
        var s = name.Name;
        if (s.Length > 0 && char.IsDigit(s[0]))
        {
            return false;
        }

        _properties ??= new PropertyDictionary();
        _properties[name] = value;
        unchecked { _propertiesVersion++; }
        return true;
    }

    public bool TryGetValue(JsValue property, out JsValue value)
        => TryGetValue(property, this, out value);

    // Same as the public overload, but threads the ORIGINAL receiver through the
    // prototype-chain walk. Spec: OrdinaryGet(O, P, Receiver) calls an inherited
    // accessor's getter with `Receiver` - the object the lookup STARTED on - not
    // with the prototype the accessor happened to be found on. The previous
    // implementation recursed as `Prototype?.TryGetValue(property, out value)`,
    // which re-entered with `this` rebound to the prototype and therefore invoked
    // the getter with the holder as its `this`. Any class whose prototype accessor
    // reads instance state then saw an "empty" prototype object - e.g.
    // `get length() { return this._arr.length; }` threw "Cannot read properties of
    // undefined (reading 'length')" because the prototype has no `_arr`. Reached
    // most visibly through IsArrayLike, which probes 'length' before every array
    // destructuring, so `const [a, b] = someInstanceOfSuchAClass;` threw instead of
    // falling through to the iterator protocol.
    private bool TryGetValue(JsValue property, JsValue receiver, out JsValue value)
    {
        value = Undefined;
        var desc = GetOwnProperty(property);
        if (desc != PropertyDescriptor.Undefined)
        {
            var descValue = desc.Value;
            if (desc.WritableSet && descValue is not null)
            {
                value = descValue;
                return true;
            }

            var getter = desc.Get ?? Undefined;
            if (getter.IsUndefined())
            {
                value = Undefined;
                return false;
            }

            // if getter is not undefined it must be ICallable
            var callable = (ICallable) getter;
            value = callable.Call(receiver, Arguments.Empty);
            return true;
        }

        return Prototype?.TryGetValue(property, receiver, out value) == true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Set(JsValue p, JsValue v, bool throwOnError)
    {
        if (!Set(p, v) && throwOnError)
        {
            Throw.TypeError(_engine.Realm, $"Cannot assign to read only property '{p}' of object '#<Object>'");
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Set(JsValue property, JsValue value)
    {
        if ((_type & (InternalTypes.PlainObject | InternalTypes.BuiltinShapeMode)) == InternalTypes.PlainObject && _initialized && property is JsString jsString)
        {
            if ((_type & InternalTypes.ShapeMode) != InternalTypes.Empty)
            {
                var jo = Unsafe.As<JsObject>(this);
                if (jo.ShapeOf.TryGetSlot(jsString.ToString(), out var slot))
                {
                    jo.SetSlot(slot, value); // shape-mode properties are always writable (CEW)
                    return true;
                }
            }
            else if (_properties?.TryGetValue(jsString.ToString(), out var ownDesc) == true)
            {
                if ((ownDesc._flags & PropertyFlag.Writable) != PropertyFlag.None)
                {
                    ownDesc._value = value;
                    return true;
                }
            }
        }

        return Set(property, value, this);
    }

    private static readonly PropertyDescriptor _marker = new(Undefined, PropertyFlag.ConfigurableEnumerableWritable);

    /// <summary>
    /// https://tc39.es/ecma262/#sec-ordinarysetwithowndescriptor
    /// </summary>
    public override bool Set(JsValue property, JsValue value, JsValue receiver)
    {
        if ((_type & (InternalTypes.PlainObject | InternalTypes.BuiltinShapeMode)) == InternalTypes.PlainObject && _initialized && ReferenceEquals(this, receiver) && property.IsString())
        {
            var key = (Key) property.ToString();
            if ((_type & InternalTypes.ShapeMode) != InternalTypes.Empty)
            {
                var jo = Unsafe.As<JsObject>(this);
                if (jo.ShapeOf.TryGetSlot(in key, out var slot))
                {
                    jo.SetSlot(slot, value); // shape-mode properties are always writable (CEW)
                    return true;
                }

                var shapeParent = GetPrototypeOf();
                if (shapeParent is not null)
                {
                    return shapeParent.Set(property, value, receiver);
                }
            }
            else if (_properties?.TryGetValue(key, out var ownDesc) == true)
            {
                if ((ownDesc._flags & PropertyFlag.Writable) != PropertyFlag.None)
                {
                    ownDesc._value = value;
                    return true;
                }
            }
            else
            {
                var parent = GetPrototypeOf();
                if (parent is not null)
                {
                    return parent.Set(property, value, receiver);
                }
            }
        }

        return SetUnlikely(property, value, receiver);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool SetUnlikely(JsValue property, JsValue value, JsValue receiver)
    {
        var ownDesc = GetOwnProperty(property);

        if (ownDesc == PropertyDescriptor.Undefined)
        {
            var parent = GetPrototypeOf();
            if (parent is not null)
            {
                return parent.Set(property, value, receiver);
            }

            ownDesc = _marker;
        }

        if (ownDesc.IsDataDescriptor())
        {
            if (!ownDesc.Writable)
            {
                return false;
            }

            if (receiver is not ObjectInstance oi)
            {
                return false;
            }

            var existingDescriptor = oi.GetOwnProperty(property);
            if (existingDescriptor != PropertyDescriptor.Undefined)
            {
                if (existingDescriptor.IsAccessorDescriptor())
                {
                    return false;
                }

                if (!existingDescriptor.Writable)
                {
                    return false;
                }

                var valueDesc = new PropertyDescriptor(value, PropertyFlag.None);
                return oi.DefineOwnProperty(property, valueDesc);
            }
            else
            {
                return oi.CreateDataProperty(property, value);
            }
        }

        if (ownDesc.Set is not Function.Function setter)
        {
            return false;
        }

        _engine.Call(setter, receiver, [
            value
        ], expression: null);

        return true;
    }

    /// <summary>
    /// Returns a Boolean value indicating whether a
    /// [[Put]] operation with PropertyName can be
    /// performed.
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.12.4
    /// </summary>
    internal bool CanPut(JsValue property)
    {
        var desc = GetOwnProperty(property);

        if (desc != PropertyDescriptor.Undefined)
        {
            if (desc.IsAccessorDescriptor())
            {
                var set = desc.Set;
                if (set is null || set.IsUndefined())
                {
                    return false;
                }

                return true;
            }

            return desc.Writable;
        }

        if (Prototype is null)
        {
            return Extensible;
        }

        var inherited = Prototype.GetOwnProperty(property);

        if (inherited == PropertyDescriptor.Undefined)
        {
            return Extensible;
        }

        if (inherited.IsAccessorDescriptor())
        {
            var set = inherited.Set;
            if (set is null || set.IsUndefined())
            {
                return false;
            }

            return true;
        }

        if (!Extensible)
        {
            return false;
        }

        return inherited.Writable;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-ordinary-object-internal-methods-and-internal-slots-hasproperty-p
    /// </summary>
    public virtual bool HasProperty(JsValue property)
    {
        var key = TypeConverter.ToPropertyKey(property);
        if (ProbeOwnPropertyChecked(key) != OwnPropertyProbe.Missing)
        {
            return true;
        }

        var parent = GetPrototypeOf();
        if (parent is not null)
        {
            return parent.HasProperty(key);
        }

        return false;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-deletepropertyorthrow
    /// </summary>
    internal bool DeletePropertyOrThrow(JsValue property)
    {
        if (!Delete(property))
        {
            Throw.TypeError(_engine.Realm, $"Cannot delete property '{property}' of #<Object>");
        }
        return true;
    }

    /// <summary>
    /// Removes the specified named own property
    /// from the object. The flag controls failure
    /// handling.
    /// </summary>
    public virtual bool Delete(JsValue property)
    {
        var desc = GetOwnProperty(property);

        if (desc == PropertyDescriptor.Undefined)
        {
            return true;
        }

        if (desc.Configurable)
        {
            RemoveOwnProperty(property);
            return true;
        }

        return false;
    }

    internal bool DefinePropertyOrThrow(JsValue property, PropertyDescriptor desc)
    {
        if (!DefineOwnProperty(property, desc))
        {
            Throw.TypeError(_engine.Realm, "Cannot redefine property: " + property);
        }

        return true;
    }

    /// <summary>
    /// Creates or alters the named own property to have the state described by a PropertyDescriptor.
    /// </summary>
    public virtual bool DefineOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        // Defining a string property can change attributes / install an accessor / mutate the current
        // descriptor in place (ValidateAndApplyPropertyDescriptor), none of which shape mode represents.
        // Deopt before reading current so it is a real dictionary descriptor. Symbol defines are
        // orthogonal to the string-key shape and stay in _symbols, so they don't deopt.
        if ((_type & InternalTypes.ShapeMode) != InternalTypes.Empty && !property.IsSymbol())
        {
            ConvertToDictionaryMode();
        }

        var current = GetOwnProperty(property);

        if (current == desc)
        {
            return true;
        }

        return ValidateAndApplyPropertyDescriptor(this, property, Extensible, desc, current);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-validateandapplypropertydescriptor
    /// </summary>
    protected static bool ValidateAndApplyPropertyDescriptor(ObjectInstance? o, JsValue property, bool extensible, PropertyDescriptor desc, PropertyDescriptor current)
    {
        var descValue = desc.Value;
        if (current == PropertyDescriptor.Undefined)
        {
            if (!extensible)
            {
                return false;
            }

            if (o is not null)
            {
                if (desc.IsGenericDescriptor() || desc.IsDataDescriptor())
                {
                    PropertyDescriptor propertyDescriptor;
                    if ((desc._flags & PropertyFlag.ConfigurableEnumerableWritable) == PropertyFlag.ConfigurableEnumerableWritable)
                    {
                        propertyDescriptor = new PropertyDescriptor(descValue ?? Undefined, PropertyFlag.ConfigurableEnumerableWritable);
                    }
                    else if ((desc._flags & PropertyFlag.ConfigurableEnumerableWritable) == PropertyFlag.None)
                    {
                        propertyDescriptor = new PropertyDescriptor(descValue ?? Undefined, PropertyFlag.AllForbidden);
                    }
                    else
                    {
                        propertyDescriptor = new PropertyDescriptor(desc)
                        {
                            Value = descValue ?? Undefined
                        };
                    }

                    o.SetOwnProperty(property, propertyDescriptor);
                }
                else
                {
                    var descriptor = new GetSetPropertyDescriptor(desc.Get, desc.Set, PropertyFlag.None)
                    {
                        Enumerable = desc.Enumerable,
                        Configurable = desc.Configurable
                    };

                    o.SetOwnProperty(property, descriptor);
                }
            }

            return true;
        }

        // Step 3
        var currentGet = current.Get;
        var currentSet = current.Set;

        // current.[[Value]] is fetched where it is first needed rather than here. Reading it runs a
        // PropertyFlag.CustomJsValue override, and for the engine's lazy descriptors — a lazy global, a lazy
        // layout slot — that RESOLVES the value, which is a side effect an attribute-only redefinition must
        // not have: Object.freeze and Object.seal redefine every own key with attributes and nothing else,
        // and would otherwise force every lazy property on the object into existence. Each fetch site below
        // sits behind a test such a redefinition never passes. Still fetched at most once, so a host
        // CustomValue override projecting live state is observed exactly as often as it was before.
        JsValue? currentValue = null;
        var currentValueFetched = false;

        // 4. If every field in Desc is absent, return true.
        if ((current._flags & (PropertyFlag.ConfigurableSet | PropertyFlag.EnumerableSet | PropertyFlag.WritableSet)) == PropertyFlag.None &&
            currentGet is null &&
            currentSet is null)
        {
            currentValue = current.Value;
            currentValueFetched = true;
            if (currentValue is null)
            {
                return true;
            }
        }

        // Step 6
        var descGet = desc.Get;
        var descSet = desc.Set;
        if (
            current.Configurable == desc.Configurable && current.ConfigurableSet == desc.ConfigurableSet &&
            current.Writable == desc.Writable && current.WritableSet == desc.WritableSet &&
            current.Enumerable == desc.Enumerable && current.EnumerableSet == desc.EnumerableSet &&
            ((currentGet is null && descGet is null) || (currentGet is not null && descGet is not null && SameValue(currentGet, descGet))) &&
            ((currentSet is null && descSet is null) || (currentSet is not null && descSet is not null && SameValue(currentSet, descSet)))
        )
        {
            if (!currentValueFetched)
            {
                currentValue = current.Value;
                currentValueFetched = true;
            }

            if ((currentValue is null && descValue is null) || (currentValue is not null && descValue is not null && currentValue == descValue))
            {
                return true;
            }
        }

        if (!current.Configurable)
        {
            if (desc.Configurable)
            {
                return false;
            }

            if (desc.EnumerableSet && (desc.Enumerable != current.Enumerable))
            {
                return false;
            }
        }

        if (!desc.IsGenericDescriptor())
        {
            if (current.IsDataDescriptor() != desc.IsDataDescriptor())
            {
                if (!current.Configurable)
                {
                    return false;
                }

                if (o is not null)
                {
                    var flags = current.Flags & ~(PropertyFlag.Writable | PropertyFlag.WritableSet | PropertyFlag.CustomJsValue);
                    if (current.IsDataDescriptor())
                    {
                        o.SetOwnProperty(property, current = new GetSetPropertyDescriptor(
                            get: Undefined,
                            set: Undefined,
                            flags
                        ));
                    }
                    else
                    {
                        o.SetOwnProperty(property, current = new PropertyDescriptor(
                            value: Undefined,
                            flags
                        ));
                    }
                }
            }
            else if (current.IsDataDescriptor() && desc.IsDataDescriptor())
            {
                if (!current.Configurable)
                {
                    if (!current.Writable && desc.Writable)
                    {
                        return false;
                    }

                    if (!current.Writable && descValue is not null)
                    {
                        if (!currentValueFetched)
                        {
                            currentValue = current.Value;
                            currentValueFetched = true;
                        }

                        if (!SameValue(descValue, currentValue!))
                        {
                            return false;
                        }
                    }
                }
            }
            else if (current.IsAccessorDescriptor() && desc.IsAccessorDescriptor())
            {
                if (!current.Configurable)
                {
                    if ((descSet is not null && !SameValue(descSet, currentSet ?? Undefined))
                        ||
                        (descGet is not null && !SameValue(descGet, currentGet ?? Undefined)))
                    {
                        return false;
                    }
                }
            }
        }

        if (o is not null)
        {
            if (descValue is not null)
            {
                current.Value = descValue;
            }

            if (desc.WritableSet)
            {
                current.Writable = desc.Writable;
            }

            if (desc.EnumerableSet)
            {
                current.Enumerable = desc.Enumerable;
            }

            if (desc.ConfigurableSet)
            {
                current.Configurable = desc.Configurable;
            }

            PropertyDescriptor? mutable = null;
            if (descGet is not null)
            {
                mutable = new GetSetPropertyDescriptor(mutable ?? current);
                ((GetSetPropertyDescriptor) mutable).SetGet(descGet);
            }

            if (descSet is not null)
            {
                mutable = new GetSetPropertyDescriptor(mutable ?? current);
                ((GetSetPropertyDescriptor) mutable).SetSet(descSet);
            }

            if (mutable != null)
            {
                // replace old with new type that supports get and set
                o.SetOwnProperty(property, mutable);
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected internal void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        // we need to set flag eagerly to prevent wrong recursion
        _initialized = true;
        Initialize();
    }

    protected virtual void Initialize()
    {
    }

    public override object ToObject()
    {
        var stack = _engine._objectTraverseStackPool.Rent(_engine);
        var result = ToObject(stack);
        _engine._objectTraverseStackPool.Return(stack);
        return result;
    }

    private object ToObject(ObjectTraverseStack stack)
    {
        if (this is IObjectWrapper wrapper)
        {
            return wrapper.Target;
        }

        stack.Enter(this);
        object? converted = null;
        switch (Class)
        {
            case ObjectClass.String:
                if (this is StringInstance stringInstance)
                {
                    converted = stringInstance.StringData.ToString();
                }
                break;

            case ObjectClass.Date:
                if (this is JsDate dateInstance)
                {
                    converted = dateInstance.ToDateTime();
                }
                break;

            case ObjectClass.Boolean:
                if (this is BooleanInstance booleanInstance)
                {
                    converted = booleanInstance.BooleanData._value
                        ? JsBoolean.BoxedTrue
                        : JsBoolean.BoxedFalse;
                }
                break;

            case ObjectClass.Function:
                if (this is ICallable)
                {
                    converted = (JsCallDelegate) CallFromHost;
                }

                break;

            case ObjectClass.Number:
                if (this is NumberInstance numberInstance)
                {
                    converted = numberInstance.NumberData._value;
                }

                break;

            case ObjectClass.RegExp:
                if (this is JsRegExp regeExpInstance)
                {
                    converted = regeExpInstance.Value;
                }
                break;

            case ObjectClass.Arguments:
            case ObjectClass.Object:

                if ((Engine.Options.ExperimentalFeatures & ExperimentalFeature.TaskInterop) != ExperimentalFeature.None)
                {
                    if (this is JsPromise asPromise)
                    {
                        var promsiseResult = asPromise.UnwrapIfPromise(Engine.Options.Constraints.PromiseTimeout);

                        converted = promsiseResult is ObjectInstance oi
                                    ? oi.ToObject(stack)
                                    : promsiseResult.ToObject();
                        break;
                    }
                }
                if (this is JsArray arrayInstance)
                {
                    var result = new object?[arrayInstance.GetLength()];
                    for (uint i = 0; i < result.Length; i++)
                    {
                        var value = arrayInstance[i];
                        object? valueToSet = null;
                        if (!value.IsUndefined())
                        {
                            valueToSet = value is ObjectInstance oi
                                ? oi.ToObject(stack)
                                : value.ToObject();
                        }
                        result[i] = valueToSet;
                    }
                    converted = result;
                    break;
                }

                if (this is JsTypedArray typedArrayInstance)
                {
                    converted = typedArrayInstance._arrayElementType switch
                    {
                        TypedArrayElementType.Int8 => typedArrayInstance.ToNativeArray<sbyte>(),
                        TypedArrayElementType.Int16 => typedArrayInstance.ToNativeArray<short>(),
                        TypedArrayElementType.Int32 => typedArrayInstance.ToNativeArray<int>(),
                        TypedArrayElementType.BigInt64 => typedArrayInstance.ToNativeArray<long>(),
#if SUPPORTS_HALF
                        TypedArrayElementType.Float16 => typedArrayInstance.ToNativeArray<Half>(),
#endif
                        TypedArrayElementType.Float32 => typedArrayInstance.ToNativeArray<float>(),
                        TypedArrayElementType.Float64 => typedArrayInstance.ToNativeArray<double>(),
                        TypedArrayElementType.Uint8 => typedArrayInstance.ToNativeArray<byte>(),
                        TypedArrayElementType.Uint8C => typedArrayInstance.ToNativeArray<byte>(),
                        TypedArrayElementType.Uint16 => typedArrayInstance.ToNativeArray<ushort>(),
                        TypedArrayElementType.Uint32 => typedArrayInstance.ToNativeArray<uint>(),
                        TypedArrayElementType.BigUint64 => typedArrayInstance.ToNativeArray<ulong>(),
                        _ => throw new NotSupportedException("cannot handle element type")
                    };

                    break;
                }

                if (this is JsArrayBuffer arrayBuffer)
                {
                    // TODO: What to do here when buffer is detached? We're not allowed to return null
                    arrayBuffer.AssertNotDetached();
                    converted = arrayBuffer.ArrayBufferData;
                    break;
                }

                if (this is JsDataView dataView)
                {
                    // TODO: What to do here when buffer is detached? We're not allowed to return null
                    dataView._viewedArrayBuffer!.AssertNotDetached();
                    var res = new byte[dataView._byteLength];
                    System.Array.Copy(dataView._viewedArrayBuffer._arrayBufferData!, dataView._byteOffset, res, 0, dataView._byteLength);
                    converted = res;
                    break;
                }

                if (this is BigIntInstance bigIntInstance)
                {
                    converted = bigIntInstance.BigIntData._value;
                    break;
                }

                var func = _engine.Options.Interop.CreateClrObject;
                if (func is null)
                {
                    goto default;
                }

                var o = func(this);
                foreach (var p in GetOwnProperties())
                {
                    if (!p.Value.Enumerable)
                    {
                        continue;
                    }

                    var key = p.Key.ToString();
                    var propertyValue = Get(p.Key);
                    var value = propertyValue is ObjectInstance oi
                        ? oi.ToObject(stack)
                        : propertyValue.ToObject();
                    o.Add(key, value);
                }

                converted = o;
                break;
            default:
                converted = this;
                break;
        }

        stack.Exit();
        return converted!;
    }

    private JsValue CallFromHost(JsValue thisObject, JsValue[] arguments)
    {
        using var ownership = Engine.EnterHostCallback();
        return ((ICallable) this).Call(thisObject, arguments);
    }

    /// <summary>
    /// Handles the generic find of (callback[, thisArg]). The two arguments are taken positionally rather
    /// than as a <c>JsCallArguments</c> array so the callers can be reached through the fast-call lane,
    /// which carries its arguments in registers; an absent argument is <c>Undefined</c> either way.
    /// </summary>
    internal virtual bool FindWithCallback(
        JsValue callbackfn,
        JsValue thisArg,
        out ulong index,
        out JsValue value,
        bool visitUnassigned,
        bool fromEnd = false)
    {
        ulong GetLength()
        {
            var descValue = Get(CommonProperties.Length);
            var len = TypeConverter.ToNumber(descValue);

            return (ulong) System.Math.Max(
                0,
                System.Math.Min(len, ArrayOperations.MaxArrayLikeLength));
        }

        bool TryGetValue(ulong idx, out JsValue jsValue)
        {
            var property = JsString.Create(idx);
            var kPresent = HasProperty(property);
            jsValue = kPresent ? Get(property) : Undefined;
            return kPresent;
        }

        var length = GetLength();
        if (length == 0)
        {
            index = 0;
            value = Undefined;
            return false;
        }

        // `this` is the receiver the generic was applied to, not the built-in that owns the algorithm,
        // so no callee realm is reachable here and the running one is the best available answer.
        var callable = callbackfn.GetCallable(_engine.Realm);

        var invoker = CallbackInvoker.Rent(_engine, callable, 3, this);

        // try/finally so the rented pool array is returned on every exit path: the early
        // return-on-match below and a periodic Check() throw both previously leaked it.
        try
        {
            if (!fromEnd)
            {
                for (ulong k = 0; k < length; k++)
                {
                    if (k > 0 && k % Engine.ConstraintCheckInterval == 0)
                    {
                        _engine.Constraints.Check();
                    }

                    if (TryGetValue(k, out var kvalue) || visitUnassigned)
                    {
                        var testResult = invoker.Call(thisArg, kvalue, k);
                        if (TypeConverter.ToBoolean(testResult))
                        {
                            index = k;
                            value = kvalue;
                            return true;
                        }
                    }
                }
            }
            else
            {
                for (var k = (long) (length - 1); k >= 0; k--)
                {
                    if (k % Engine.ConstraintCheckInterval == 0)
                    {
                        _engine.Constraints.Check();
                    }

                    if (TryGetValue((ulong) k, out var kvalue) || visitUnassigned)
                    {
                        kvalue ??= Undefined;
                        var testResult = invoker.Call(thisArg, kvalue, k);
                        if (TypeConverter.ToBoolean(testResult))
                        {
                            index = (ulong) k;
                            value = kvalue;
                            return true;
                        }
                    }
                }
            }
        }
        finally
        {
            invoker.Return();
        }

        index = 0;
        value = Undefined;
        return false;
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal bool IsConcatSpreadable
    {
        get
        {
            var spreadable = Get(GlobalSymbolRegistry.IsConcatSpreadable);
            if (!spreadable.IsUndefined())
            {
                return TypeConverter.ToBoolean(spreadable);
            }
            return IsArray();
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal virtual bool IsArrayLike => TryGetValue(CommonProperties.Length, out var lengthValue)
                                         && lengthValue.IsNumber()
                                         && ((JsNumber) lengthValue)._value >= 0;

    // safe default
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal virtual bool HasOriginalIterator => false;

    internal override bool IsIntegerIndexedArray => false;

    internal virtual uint GetLength() => (uint) TypeConverter.ToLength(Get(CommonProperties.Length));

    /// <summary>
    /// https://tc39.es/ecma262/#sec-ordinarypreventextensions
    /// </summary>
    public virtual bool PreventExtensions()
    {
        Extensible = false;
        return true;
    }

    protected internal virtual ObjectInstance? GetPrototypeOf()
    {
        return _prototype;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-ordinarysetprototypeof
    /// </summary>
    internal virtual bool SetPrototypeOf(JsValue value)
    {
        if (!value.IsObject() && !value.IsNull())
        {
            Throw.ArgumentException();
        }

        var current = _prototype ?? Null;
        if (ReferenceEquals(value, current))
        {
            return true;
        }

        if (!Extensible)
        {
            return false;
        }

        if (value.IsNull())
        {
            _prototype = null;
            return true;
        }

        // validate chain
        var p = value as ObjectInstance;
        bool done = false;
        while (!done)
        {
            if (p is null)
            {
                done = true;
            }
            else if (ReferenceEquals(p, this))
            {
                return false;
            }
            else
            {
                p = p._prototype;
            }
        }

        _prototype = value as ObjectInstance;
        return true;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-setfunctionname
    /// </summary>
    internal void SetFunctionName(JsValue name, string? prefix = null)
    {
        if (name is JsSymbol symbol)
        {
            name = symbol._value.IsUndefined()
                ? JsString.Empty
                : new JsString("[" + symbol._value + "]");
        }
        if (!string.IsNullOrWhiteSpace(prefix))
        {
            name = prefix + " " + name;
        }

        DefinePropertyOrThrow(CommonProperties.Name, new PropertyDescriptor(name, PropertyFlag.Configurable));
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-createmethodproperty
    /// </summary>
    internal virtual bool CreateMethodProperty(JsValue p, JsValue v)
    {
        var newDesc = new PropertyDescriptor(v, PropertyFlag.NonEnumerable);
        return DefineOwnProperty(p, newDesc);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-createdataproperty
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CreateDataProperty(JsValue p, JsValue v)
    {
        // Fast path for an ordinary, extensible object gaining a brand-new string-keyed data property.
        // The generic DefineOwnProperty → ValidateAndApplyPropertyDescriptor route allocates a transient
        // descriptor here and then a second, identical one to actually store (ValidateAndApply re-creates
        // it), so an object filled via this.x= / spread / Object.assign pays two PropertyDescriptor
        // allocations per property. When the receiver is a PlainObject (no exotic [[GetOwnProperty]]),
        // extensible, and the key is absent, the result is simply "store one CEW data descriptor".
        //
        // Store through the virtual SetOwnProperty (exactly what ValidateAndApplyPropertyDescriptor uses)
        // — NOT the raw SetProperty primitive — so side-effecting overrides still run. ObjectPrototype is
        // itself a PlainObject and overrides SetOwnProperty to flip ObjectChangeFlags.ArrayIndex (which
        // disables the array fast-access path); skipping it silently breaks inherited-index iteration in
        // Array.prototype.concat/sort. No PlainObject overrides [[DefineOwnProperty]] without also
        // overriding SetOwnProperty, so this routing is equivalent to the full path for every PlainObject.
        //
        // The `_initialized` gate keeps the absence check honest: a lazily-initialized PlainObject
        // (intrinsic prototypes, GlobalObject, NumberPrototype) populates _properties in Initialize(), so
        // before that runs a built-in key could be misread as absent and overwritten. Those objects are
        // skipped here and handled by the generic path (whose GetOwnProperty runs EnsureInitialized).
        // Ordinary user objects (JsObject) are born initialized, so they take this path with no per-object
        // virtual Initialize() call.
        if ((_type & (InternalTypes.PlainObject | InternalTypes.BuiltinShapeMode)) == InternalTypes.PlainObject
            && _initialized
            && Extensible
            && p is JsString jsString)
        {
            Key key = jsString.ToString();
            if ((_type & InternalTypes.ShapeMode) != InternalTypes.Empty)
            {
                var jo = Unsafe.As<JsObject>(this);
                if (jo.ShapeOf.TryGetSlot(in key, out var slot))
                {
                    // Existing CEW data property: CreateDataProperty just updates the value (spec: a CEW
                    // DefineOwnProperty overwrite — last value wins, first-occurrence position kept). This
                    // probe is what keeps TryShapeAdd's known-absent contract honest for re-added keys:
                    // duplicate class field initializers (`class A { x=1; x=2 }`) and spread re-copies
                    // (`{a:1, ...{a:2}}`) both land here instead of transitioning a duplicate slot.
                    jo.SetSlot(slot, v);
                    return true;
                }

                // Brand-new property: a hot constructor's `this` or a copy-idiom target — spread/rest/
                // fromEntries/assign (ShapeBuilding) — grows its shape via an interned transition shared
                // across instances. Plain shaped objects (literals) lack the flag and fall through to
                // deopt, since a one-off literal gaining a key is not a reused layout. The megamorphic
                // guard inside TryShapeAdd also deopts object-as-hashmap usage. Integer-like keys
                // (digit-leading, e.g. `{...arr}` / string wrappers / this["0"]=) never enter a shape:
                // spec enumeration orders them first, which the shape's insertion-ordered keys cannot
                // express (GetOwnPropertyKeysFromShape deopts), so admitting them would guarantee a
                // build-then-deopt and intern junk "0"→"1"→… chains under the shared per-prototype root;
                // they take the dictionary fallback below instead.
                if ((_type & InternalTypes.ShapeBuilding) != InternalTypes.Empty
                    && !Shape.IsIntegerIndexLikeKey(key.Name)
                    && jo.TryShapeAdd(in key, v))
                {
                    return true;
                }

                ConvertToDictionaryMode();
                SetOwnProperty(p, new PropertyDescriptor(v, PropertyFlag.ConfigurableEnumerableWritable));
                return true;
            }

            if (_properties is null || !_properties.TryGetValue(key, out _))
            {
                SetOwnProperty(p, new PropertyDescriptor(v, PropertyFlag.ConfigurableEnumerableWritable));
                return true;
            }
        }

        return DefineOwnProperty(p, new PropertyDescriptor(v, PropertyFlag.ConfigurableEnumerableWritable));
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-createdatapropertyorthrow
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool CreateDataPropertyOrThrow(JsValue p, JsValue v)
    {
        if (!CreateDataProperty(p, v))
        {
            Throw.TypeError(_engine.Realm, $"Cannot define property {p}, object is not extensible");
        }

        return true;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-createnonenumerabledatapropertyorthrow
    /// </summary>
    internal void CreateNonEnumerableDataPropertyOrThrow(JsValue p, JsValue v)
    {
        var newDesc = new PropertyDescriptor(v, true, false, true);
        DefinePropertyOrThrow(p, newDesc);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-ordinaryobjectcreate
    /// </summary>
    internal static JsObject OrdinaryObjectCreate(Engine engine, ObjectInstance? proto)
    {
        var prototype = new JsObject(engine)
        {
            _prototype = proto
        };
        return prototype;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ICallable? GetMethod(JsValue property)
    {
        return GetMethod(_engine.Realm, this, property);
    }

    internal ICallable? GetDisposeMethod(DisposeHint hint)
    {
        if (hint == DisposeHint.Async)
        {
            var method = GetMethod(GlobalSymbolRegistry.AsyncDispose);
            if (method is null)
            {
                method = GetMethod(GlobalSymbolRegistry.Dispose);
                if (method is not null)
                {
                    JsCallDelegate closure = (_, _) =>
                    {
                        var promiseCapability = PromiseConstructor.NewPromiseCapability(_engine, _engine.Intrinsics.Promise);
                        try
                        {
                            method.Call(this);
                            promiseCapability.Resolve(Undefined);
                        }
                        catch (JavaScriptException e)
                        {
                            promiseCapability.Reject(e.Error);
                        }
                        return promiseCapability.PromiseInstance;
                    };

                    return new ClrFunction(_engine, string.Empty, closure);
                }
            }

            return method;
        }

        return GetMethod(GlobalSymbolRegistry.Dispose);
    }


    internal void CopyDataProperties(
        ObjectInstance target,
        HashSet<JsValue>? excludedItems)
    {
        // Fast path for the object-spread copy idiom `{ ...src }` (nothing excluded): when the target is a
        // fresh shape-building object and the source (this) is a shape-mode plain object with the same
        // prototype and no symbol properties, the target's resulting layout is exactly the source's. Adopt
        // the interned shape and shallow-copy the slots — O(slots) — instead of streaming every key through
        // CreateDataProperty. Object rest (`{ a, ...r }`) always passes a non-null excludedItems set, so it
        // stays on the streaming path below where per-key exclusion is honored.
        if (excludedItems is null
            && target is JsObject targetJo
            && this is JsObject sourceJo
            && targetJo.TryAdoptShapeFrom(sourceJo))
        {
            return;
        }

        var keys = GetOwnPropertyKeys();
        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            if (excludedItems == null || !excludedItems.Contains(key))
            {
                if (ProbeOwnPropertyChecked(key) == OwnPropertyProbe.Enumerable)
                {
                    var propValue = Get(key);
                    target.CreateDataProperty(key, propValue);
                }
            }
        }
    }

    internal JsArray EnumerableOwnProperties(EnumerableOwnPropertyNamesKind kind)
    {
        var ownKeys = GetOwnPropertyKeys(Types.String);

        // ArrayCreate would validate this through the constructor; the ownership-taking
        // constructor used below does not, so keep constraint parity explicitly.
        if ((uint) ownKeys.Count > _engine.Options.Constraints.MaxArraySize)
        {
            ArrayInstance.ThrowMaximumArraySizeReachedException(_engine, (uint) ownKeys.Count);
        }

        // The output is bounded by (and usually equal to) the key count, so values are
        // written straight into the final backing array and the result takes ownership
        // without copying; only when keys get filtered out is an exact-size copy made,
        // instead of retaining the over-allocated backing.
        var target = new JsValue[ownKeys.Count];
        var count = 0;

        for (var i = 0; i < ownKeys.Count; i++)
        {
            // Pure native enumeration over a JS-controlled key count; check constraints periodically.
            if (i > 0 && i % Engine.ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

            var property = ownKeys[i];

            if (!property.IsString())
            {
                continue;
            }

            if (ProbeOwnPropertyChecked(property) == OwnPropertyProbe.Enumerable)
            {
                if (kind == EnumerableOwnPropertyNamesKind.Key)
                {
                    target[count++] = property;
                }
                else
                {
                    var value = Get(property);
                    if (kind == EnumerableOwnPropertyNamesKind.Value)
                    {
                        target[count++] = value;
                    }
                    else
                    {
                        target[count++] = new JsArray(_engine, [property, value]);
                    }
                }
            }
        }

        if (count == target.Length)
        {
            return new JsArray(_engine, target);
        }

        var exact = new JsValue[count];
        System.Array.Copy(target, exact, count);
        return new JsArray(_engine, exact);
    }

    internal enum EnumerableOwnPropertyNamesKind
    {
        Key,
        Value,
        KeyValue
    }

    internal ObjectInstance AssertThisIsObjectInstance(JsValue value, string methodName)
    {
        var instance = value as ObjectInstance;
        if (instance is null)
        {
            ThrowIncompatibleReceiver(value, methodName);
        }
        return instance!;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowIncompatibleReceiver(JsValue value, string methodName)
    {
        Throw.TypeError(_engine.Realm, $"Method {methodName} called on incompatible receiver {value}");
    }

    public override bool Equals(object? obj) => Equals(obj as ObjectInstance);

    public override bool Equals(JsValue? other) => Equals(other as ObjectInstance);

    public bool Equals(ObjectInstance? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return false;
    }

    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);

    public override string ToString()
    {
        return TypeConverter.ToString(this);
    }

    internal virtual ulong GetSmallestIndex(ulong length)
    {
        // there are some evil tests that iterate a lot with unshift..
        if (Properties == null && (_type & InternalTypes.ShapeMode) == InternalTypes.Empty)
        {
            return 0;
        }

        var min = length;
        foreach (var entry in GetOwnProperties())
        {
            if (ulong.TryParse(entry.Key.ToString(), out var index))
            {
                min = System.Math.Min(index, min);
            }
        }

        if (Prototype?.Properties != null)
        {
            foreach (var entry in Prototype.GetOwnProperties())
            {
                if (ulong.TryParse(entry.Key.ToString(), out var index))
                {
                    min = System.Math.Min(index, min);
                }
            }
        }

        return min;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-invoke
    /// </summary>
    internal JsValue Invoke(JsValue v, JsValue p, JsCallArguments arguments)
    {
        var func = v.GetV(_engine.Realm, p);
        if (func is not ICallable callable)
        {
            Throw.TypeError(_engine.Realm, $"Property '{p}' of object is not a function");
            return default;
        }

        return callable.Call(v, arguments);
    }


    /// <summary>
    /// https://tc39.es/ecma262/#sec-setintegritylevel
    /// </summary>
    internal bool SetIntegrityLevel(IntegrityLevel level)
    {
        var status = PreventExtensions();
        if (!status)
        {
            return false;
        }

        var keys = GetOwnPropertyKeys();
        if (level == IntegrityLevel.Sealed)
        {
            for (var i = 0; i < keys.Count; i++)
            {
                var k = keys[i];
                DefinePropertyOrThrow(k, new PropertyDescriptor { Configurable = false });
            }
        }
        else
        {
            for (var i = 0; i < keys.Count; i++)
            {
                var k = keys[i];
                var currentDesc = GetOwnProperty(k);
                if (currentDesc != PropertyDescriptor.Undefined)
                {
                    PropertyDescriptor desc;
                    if (currentDesc.IsAccessorDescriptor())
                    {
                        desc = new PropertyDescriptor { Configurable = false };
                    }
                    else
                    {
                        desc = new PropertyDescriptor { Configurable = false, Writable = false };
                    }

                    DefinePropertyOrThrow(k, desc);
                }
            }
        }

        return true;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-definefield
    /// </summary>
    internal static void DefineField(ObjectInstance receiver, ClassFieldDefinition fieldRecord)
    {
        var fieldName = fieldRecord.Name;
        var initializer = fieldRecord.Initializer;
        var initValue = Undefined;
        if (initializer is not null)
        {
            initValue = receiver._engine.Call(initializer, thisObject: receiver, Arguments.Empty);
            if (initValue is Function.Function functionInstance)
            {
                functionInstance.SetFunctionName(fieldName);
            }
        }

        if (fieldName is PrivateName privateName)
        {
            receiver.PrivateFieldAdd(privateName, initValue);
        }
        else
        {
            receiver.CreateDataPropertyOrThrow(fieldName, initValue);
        }
    }

    internal enum IntegrityLevel
    {
        Sealed,
        Frozen
    }

    private sealed class ObjectInstanceDebugView
    {
        private readonly ObjectInstance _obj;

        public ObjectInstanceDebugView(ObjectInstance obj)
        {
            _obj = obj;
        }

        public ObjectInstance? Prototype => _obj.Prototype;

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public KeyValuePair<JsValue, JsValue>[] Entries
        {
            get
            {
                var shape = (_obj._type & InternalTypes.ShapeMode) != InternalTypes.Empty ? ((JsObject) _obj).ShapeOf : null;
                var stringCount = shape?.SlotCount ?? _obj._properties?.Count ?? 0;
                var keys = new KeyValuePair<JsValue, JsValue>[stringCount + (_obj._symbols?.Count ?? 0)];

                var i = 0;
                if (shape is not null)
                {
                    foreach (var pair in _obj.GetOwnProperties())
                    {
                        if (pair.Key.IsSymbol())
                        {
                            continue;
                        }
                        keys[i++] = new KeyValuePair<JsValue, JsValue>(pair.Key, UnwrapJsValue(pair.Value, _obj));
                    }
                }
                else if (_obj._properties is not null)
                {
                    foreach (var key in _obj._properties)
                    {
                        keys[i++] = new KeyValuePair<JsValue, JsValue>(key.Key.Name, UnwrapJsValue(key.Value, _obj));
                    }
                }
                if (_obj._symbols is not null)
                {
                    foreach (var key in _obj._symbols)
                    {
                        keys[i++] = new KeyValuePair<JsValue, JsValue>(key.Key, UnwrapJsValue(key.Value, _obj));
                    }
                }
                return keys;
            }
        }

        private string DebugToString() => new JsonSerializer(_obj._engine).Serialize(_obj, Undefined, "  ").ToString();
    }
}
