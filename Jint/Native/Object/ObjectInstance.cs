using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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

    protected ObjectInstance(Engine engine) : this(engine, ObjectClass.Object)
    {
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

        Throw.TypeError(o._engine.Realm, $"{s} is not a constructor");
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
            for (var i = 0; i < slotCount; i++)
            {
                properties[keys[i]] = new PropertyDescriptor(jo.GetSlot(i), PropertyFlag.ConfigurableEnumerableWritable);
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
        // Storing a raw descriptor is a dictionary-mode operation; deopt first if needed.
        // Without the builtin-shape deopt, the write would land in a side dictionary that the
        // shape-mode read paths never consult — the property would silently not exist.
        if ((_type & InternalTypes.ShapeMode) != InternalTypes.Empty)
        {
            ConvertToDictionaryMode();
        }
        else if ((_type & InternalTypes.BuiltinShapeMode) != InternalTypes.Empty)
        {
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
            if ((_type & InternalTypes.ShapeMode) != InternalTypes.Empty)
            {
                ConvertToDictionaryMode();
            }
            _properties ??= new PropertyDictionary();
            _properties[TypeConverter.ToString(propertyKey)] = value;
            unchecked { _propertiesVersion++; }
        }
        else
        {
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
                var keys = new Key[slotCount];
                shape.CollectKeys(keys);

                // Spec ordering puts integer-index keys first (ascending) then string keys in insertion
                // order. Shape slots are insertion order; if any key looks like an array index, deopt and
                // reuse the dictionary path that already implements the numeric sort (rare for literals).
                for (var i = 0; i < slotCount; i++)
                {
                    var name = keys[i].Name;
                    if (name.Length > 0 && char.IsDigit(name[0]))
                    {
                        ConvertToDictionaryMode();
                        return GetOwnPropertyKeys(types);
                    }
                }

                for (var i = 0; i < slotCount; i++)
                {
                    propertyKeys.Add(new JsString(keys[i].Name));
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
            foreach (var pair in _properties)
            {
                var propertyName = pair.Key.Name;
                var arrayIndex = ArrayInstance.ParseArrayIndex(propertyName);

                if (arrayIndex < ArrayOperations.MaxArrayLength)
                {
                    keys.Add(JsString.Create(arrayIndex));
                }
                else
                {
                    initialOwnPropertyKeys.Add(new JsString(propertyName));
                }
            }
        }

        keys.Sort((v1, v2) => TypeConverter.ToNumber(v1).CompareTo(TypeConverter.ToNumber(v2)));
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

    internal virtual IEnumerable<JsValue> GetInitialOwnStringPropertyKeys() => [];

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
        return ProbeOwnProperty(property) != OwnPropertyProbe.Missing;
    }

    public virtual void RemoveOwnProperty(JsValue property)
    {
        EnsureInitialized();

        var key = TypeConverter.ToPropertyKey(property);
        if (!key.IsSymbol())
        {
            // Removing a string property can't be expressed as a shape / built-in-shape layout; deopt first.
            if ((_type & InternalTypes.ShapeMode) != InternalTypes.Empty)
            {
                ConvertToDictionaryMode();
            }
            else if ((_type & InternalTypes.BuiltinShapeMode) != InternalTypes.Empty)
            {
                DeoptBuiltinShape();
            }
            _properties?.Remove(TypeConverter.ToString(key));
            unchecked { _propertiesVersion++; }
            return;
        }

        _symbols?.Remove((JsSymbol) key);
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
                    return jo.GetSlot(slot);
                }

                return Prototype?.Get(property, receiver) ?? Undefined;
            }

            if (_properties?.TryGetValue(property.ToString(), out var ownDesc) == true)
            {
                return UnwrapJsValue(ownDesc, receiver);
            }

            return Prototype?.Get(property, receiver) ?? Undefined;
        }

        // slow path
        var desc = GetOwnProperty(property);
        if (desc != PropertyDescriptor.Undefined)
        {
            return UnwrapJsValue(desc, receiver);
        }

        return Prototype?.Get(property, receiver) ?? Undefined;
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
                }
                // string key absent from the shape ⇒ no own property (descriptor stays null → Undefined)
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
    /// dictionary mode) answer straight from the shape; every other object — including exotics
    /// like proxies (traps still fire), typed arrays and interop wrappers — routes through the
    /// virtual <see cref="GetOwnProperty"/>. Read-only callers that don't need the descriptor's
    /// value (existence checks, enumerability filters) should prefer this over GetOwnProperty.
    /// </summary>
    internal virtual OwnPropertyProbe ProbeOwnProperty(JsValue property)
    {
        if ((_type & InternalTypes.ShapeMode) != InternalTypes.Empty && property is JsString jsString)
        {
            return Unsafe.As<JsObject>(this).ShapeOf.TryGetSlot(jsString.ToString(), out _)
                ? OwnPropertyProbe.Enumerable
                : OwnPropertyProbe.Missing;
        }

        var desc = GetOwnProperty(property);
        if (ReferenceEquals(desc, PropertyDescriptor.Undefined))
        {
            return OwnPropertyProbe.Missing;
        }

        return desc.Enumerable ? OwnPropertyProbe.Enumerable : OwnPropertyProbe.NonEnumerable;
    }

    // Built-in-shape storage helpers (InternalTypes.BuiltinShapeMode). Shared by every host that implements
    // IBuiltinShaped — BuiltinShapeObject-derived namespaces today, generator-emitted prototypes/constructors
    // later — so the storage is composable across base classes that cannot share a single base. See BuiltinShape.

    // Materialize a shaped slot's descriptor. Constants point at the shared static descriptor; functions are
    // created on first access and stored so their identity is stable (the inline caches rely on this — a
    // materialize must never bump _propertiesVersion).
    private static PropertyDescriptor MaterializeBuiltinSlot(IBuiltinShaped shaped, int slot)
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

        var properties = new PropertyDictionary(names.Length, checkExistingKeys: false);
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

        _type &= ~InternalTypes.BuiltinShapeMode;
        shaped.BuiltinDescriptors = null;
        SetProperties(properties); // sets _properties, bumps version (symbols stay in _symbols)
    }

    private List<JsValue> GetBuiltinShapeOwnPropertyKeys(Types types)
    {
        var shaped = Unsafe.As<IBuiltinShaped>(this);
        var names = shaped.BuiltinShape.Names;
        var keys = new List<JsValue>(names.Length + (_symbols?.Count ?? 0));
        if ((types & Types.String) != Types.Empty)
        {
            // Function-derived hosts surface length/name/prototype ahead of their shape members (matching the
            // dictionary path); ordinary hosts return Enumerable.Empty here.
            var initialOwnStringPropertyKeys = GetInitialOwnStringPropertyKeys();
            if (!ReferenceEquals(initialOwnStringPropertyKeys, System.Linq.Enumerable.Empty<JsValue>()))
            {
                keys.AddRange(initialOwnStringPropertyKeys);
            }
            foreach (var name in names)
            {
                keys.Add(JsString.Create(name.Name));
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
        _type |= InternalTypes.BuiltinShapeMode;
    }

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
            // Adding a brand-new own string property can't be expressed in the fixed layout.
            DeoptBuiltinShape();
        }
        SetProperty(property, desc);
    }

    public bool TryGetValue(JsValue property, out JsValue value)
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
            value = callable.Call(this, Arguments.Empty);
            return true;
        }

        return Prototype?.TryGetValue(property, out value) == true;
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
                if (jo.ShapeOf.TryGetSlot(key, out var slot))
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
        if (ProbeOwnProperty(key) != OwnPropertyProbe.Missing)
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
        var currentValue = current.Value;

        // 4. If every field in Desc is absent, return true.
        if ((current._flags & (PropertyFlag.ConfigurableSet | PropertyFlag.EnumerableSet | PropertyFlag.WritableSet)) == PropertyFlag.None &&
            currentGet is null &&
            currentSet is null &&
            currentValue is null)
        {
            return true;
        }

        // Step 6
        var descGet = desc.Get;
        var descSet = desc.Set;
        if (
            current.Configurable == desc.Configurable && current.ConfigurableSet == desc.ConfigurableSet &&
            current.Writable == desc.Writable && current.WritableSet == desc.WritableSet &&
            current.Enumerable == desc.Enumerable && current.EnumerableSet == desc.EnumerableSet &&
            ((currentGet is null && descGet is null) || (currentGet is not null && descGet is not null && SameValue(currentGet, descGet))) &&
            ((currentSet is null && descSet is null) || (currentSet is not null && descSet is not null && SameValue(currentSet, descSet))) &&
            ((currentValue is null && descValue is null) || (currentValue is not null && descValue is not null && currentValue == descValue))
        )
        {
            return true;
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

                    if (!current.Writable)
                    {
                        if (descValue is not null && !SameValue(descValue, currentValue!))
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
                if (this is ICallable function)
                {
                    converted = (JsCallDelegate) function.Call;
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

    /// <summary>
    /// Handles the generic find of (callback[, thisArg])
    /// </summary>
    internal virtual bool FindWithCallback(
        JsCallArguments arguments,
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

        var callbackfn = arguments.At(0);
        var thisArg = arguments.At(1);
        var callable = GetCallable(callbackfn);

        var args = _engine._jsValueArrayPool.RentArray(3);
        args[2] = this;

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
                        args[0] = kvalue;
                        args[1] = k;
                        var testResult = callable.Call(thisArg, args);
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
                        args[0] = kvalue;
                        args[1] = k;
                        var testResult = callable.Call(thisArg, args);
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
            _engine._jsValueArrayPool.ReturnArray(args);
        }

        index = 0;
        value = Undefined;
        return false;
    }

    internal ICallable GetCallable(JsValue source) => source.GetCallable(_engine.Realm);

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
                if (jo.ShapeOf.TryGetSlot(key, out var slot))
                {
                    // Existing CEW data property: CreateDataProperty just updates the value.
                    jo.SetSlot(slot, v);
                    return true;
                }

                // Brand-new property: a hot constructor's `this` (ShapeBuilding) grows its shape via an
                // interned transition shared across instances. Plain shaped objects (literals) lack the flag
                // and fall through to deopt, since a one-off literal gaining a key is not a reused layout.
                // The megamorphic guard inside TryShapeAdd also deopts object-as-hashmap usage.
                if ((_type & InternalTypes.ShapeBuilding) != InternalTypes.Empty && jo.TryShapeAdd(key, v))
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
                            promiseCapability.Resolve.Call(Undefined, Undefined);
                        }
                        catch (JavaScriptException e)
                        {
                            promiseCapability.Reject.Call(Undefined, e.Error);
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
        var keys = GetOwnPropertyKeys();
        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            if (excludedItems == null || !excludedItems.Contains(key))
            {
                if (ProbeOwnProperty(key) == OwnPropertyProbe.Enumerable)
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

            if (ProbeOwnProperty(property) == OwnPropertyProbe.Enumerable)
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

    internal IEnumerable<JsValue> GetKeys()
    {
        var visited = new HashSet<JsValue>();
        foreach (var key in GetOwnPropertyKeys(Types.String))
        {
            var probe = ProbeOwnProperty(key);
            if (probe != OwnPropertyProbe.Missing)
            {
                visited.Add(key);
                if (probe == OwnPropertyProbe.Enumerable)
                {
                    yield return key;
                }
            }
        }

        if (Prototype is null)
        {
            yield break;
        }

        foreach (var protoKey in Prototype.GetKeys())
        {
            if (!visited.Contains(protoKey))
            {
                yield return protoKey;
            }
        }
    }

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
            Throw.TypeError(_engine.Realm, $"{v}.{p} is not a function");
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
