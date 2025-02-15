using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Jint.Collections;
using Jint.Native.Array;
using Jint.Native.BigInt;
using Jint.Native.Boolean;
using Jint.Native.Json;
using Jint.Native.Number;
using Jint.Native.String;
using Jint.Native.Symbol;
using Jint.Native.TypedArray;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using PropertyDescriptor = Jint.Runtime.Descriptors.PropertyDescriptor;
using TypeConverter = Jint.Runtime.TypeConverter;

namespace Jint.Native.Object;

[DebuggerTypeProxy(typeof(ObjectInstanceDebugView))]
public partial class ObjectInstance : JsValue, IEquatable<ObjectInstance>
{
    private bool _initialized;
    private readonly ObjectClass _class;

    internal PropertyDictionary? _properties;
    internal SymbolDictionary? _symbols;

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
    public virtual bool Extensible { get; private set; }

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
            ExceptionHelper.ThrowTypeError(o._engine.Realm);
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

        ExceptionHelper.ThrowTypeError(o._engine.Realm);
        return null;
    }

    internal void SetProperties(StringDictionarySlim<PropertyDescriptor> properties) => SetProperties(new PropertyDictionary(properties));

    internal void SetProperties(PropertyDictionary? properties)
    {
        if (properties != null)
        {
            properties.CheckExistingKeys = true;
        }
        _properties = properties;
    }

    internal void SetSymbols(SymbolDictionary? symbols)
    {
        _symbols = symbols;
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
        _properties ??= new PropertyDictionary();
        _properties[property] = value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void SetPropertyUnlikely(JsValue property, PropertyDescriptor value)
    {
        var propertyKey = TypeConverter.ToPropertyKey(property);
        if (!property.IsSymbol())
        {
            _properties ??= new PropertyDictionary();
            _properties[TypeConverter.ToString(propertyKey)] = value;
        }
        else
        {
            _symbols ??= new SymbolDictionary();
            _symbols[(JsSymbol) propertyKey] = value;
        }
    }

    internal void ClearProperties()
    {
        _properties?.Clear();
        _symbols?.Clear();
    }

    public virtual IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
    {
        EnsureInitialized();

        if (_properties != null)
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

    private List<JsValue> GetOwnPropertyKeysSorted(List<JsValue> initialOwnPropertyKeys, bool returningStringKeys, bool returningSymbols)
    {
        var keys = new List<JsValue>(_properties?.Count ?? 0 + _symbols?.Count ?? 0 + initialOwnPropertyKeys.Count);
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
            return _properties?.TryGetValue(TypeConverter.ToString(key), out descriptor) == true;
        }

        return _symbols?.TryGetValue((JsSymbol) key, out descriptor) == true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasOwnProperty(JsValue property)
    {
        return !ReferenceEquals(GetOwnProperty(property), PropertyDescriptor.Undefined);
    }

    public virtual void RemoveOwnProperty(JsValue property)
    {
        EnsureInitialized();

        var key = TypeConverter.ToPropertyKey(property);
        if (!key.IsSymbol())
        {
            _properties?.Remove(TypeConverter.ToString(key));
            return;
        }

        _symbols?.Remove((JsSymbol) key);
    }

    public override JsValue Get(JsValue property, JsValue receiver)
    {
        if ((_type & InternalTypes.PlainObject) != InternalTypes.Empty && ReferenceEquals(this, receiver) && property.IsString())
        {
            EnsureInitialized();
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

        var functionInstance = (Function.Function) getter;
        return functionInstance._engine.Call(functionInstance, thisObject);
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
            _properties?.TryGetValue(TypeConverter.ToString(key), out descriptor);
        }
        else
        {
            _symbols?.TryGetValue((JsSymbol) key, out descriptor);
        }

        return descriptor ?? PropertyDescriptor.Undefined;
    }

    protected internal virtual void SetOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        EnsureInitialized();
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
            ExceptionHelper.ThrowTypeError(_engine.Realm);
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Set(JsValue property, JsValue value)
    {
        if ((_type & InternalTypes.PlainObject) != InternalTypes.Empty && property is JsString jsString)
        {
            if (_properties?.TryGetValue(jsString.ToString(), out var ownDesc) == true)
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
        if ((_type & InternalTypes.PlainObject) != InternalTypes.Empty && ReferenceEquals(this, receiver) && property.IsString())
        {
            var key = (Key) property.ToString();
            if (_properties?.TryGetValue(key, out var ownDesc) == true)
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
        var hasOwn = GetOwnProperty(key);
        if (hasOwn != PropertyDescriptor.Undefined)
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
            ExceptionHelper.ThrowTypeError(_engine.Realm);
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
            ExceptionHelper.ThrowTypeError(_engine.Realm, "Cannot redefine property: " + property);
        }

        return true;
    }

    /// <summary>
    /// Creates or alters the named own property to have the state described by a PropertyDescriptor.
    /// </summary>
    public virtual bool DefineOwnProperty(JsValue property, PropertyDescriptor desc)
    {
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
        return ToObject(new ObjectTraverseStack(_engine));
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

        if (!fromEnd)
        {
            for (ulong k = 0; k < length; k++)
            {
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

        _engine._jsValueArrayPool.ReturnArray(args);

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
            ExceptionHelper.ThrowArgumentException();
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
            ExceptionHelper.ThrowTypeError(_engine.Realm);
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

    internal static ICallable? GetMethod(Realm realm, JsValue v, JsValue p)
    {
        var jsValue = v.Get(p);
        if (jsValue.IsNullOrUndefined())
        {
            return null;
        }

        var callable = jsValue as ICallable;
        if (callable is null)
        {
            ExceptionHelper.ThrowTypeError(realm, "Value returned for property '" + p + "' of object is not a function");
        }
        return callable;
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
                var desc = GetOwnProperty(key);
                if (desc.Enumerable)
                {
                    target.CreateDataProperty(key, UnwrapJsValue(desc, this));
                }
            }
        }
    }

    internal JsArray EnumerableOwnProperties(EnumerableOwnPropertyNamesKind kind)
    {
        var ownKeys = GetOwnPropertyKeys(Types.String);

        var array = Engine.Realm.Intrinsics.Array.ArrayCreate((uint) ownKeys.Count);
        uint index = 0;

        for (var i = 0; i < ownKeys.Count; i++)
        {
            var property = ownKeys[i];

            if (!property.IsString())
            {
                continue;
            }

            var desc = GetOwnProperty(property);
            if (desc != PropertyDescriptor.Undefined && desc.Enumerable)
            {
                if (kind == EnumerableOwnPropertyNamesKind.Key)
                {
                    array.SetIndexValue(index, property, updateLength: false);
                }
                else
                {
                    var value = Get(property);
                    if (kind == EnumerableOwnPropertyNamesKind.Value)
                    {
                        array.SetIndexValue(index, value, updateLength: false);
                    }
                    else
                    {
                        var objectInstance = _engine.Realm.Intrinsics.Array.ArrayCreate(2);
                        objectInstance.SetIndexValue(0, property, updateLength: false);
                        objectInstance.SetIndexValue(1, value, updateLength: false);
                        array.SetIndexValue(index, objectInstance, updateLength: false);
                    }
                }

                index++;
            }
        }

        array.SetLength(index);
        return array;
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
        ExceptionHelper.ThrowTypeError(_engine.Realm, $"Method {methodName} called on incompatible receiver {value}");
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
            var desc = GetOwnProperty(key);
            if (desc != PropertyDescriptor.Undefined)
            {
                visited.Add(key);
                if (desc.Enumerable)
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
        if (Properties == null)
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
            ExceptionHelper.ThrowTypeError(_engine.Realm, "Can only invoke functions");
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
            initValue = receiver._engine.Call(initializer, receiver);
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
                var keys = new KeyValuePair<JsValue, JsValue>[(_obj._properties?.Count ?? 0) + (_obj._symbols?.Count ?? 0)];

                var i = 0;
                if (_obj._properties is not null)
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
