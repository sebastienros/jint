using Jint.Native.Iterator;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Object;

[JsObject]
public sealed partial class ObjectConstructor : Constructor
{
    private static readonly JsString _name = new JsString("Object");

    internal ObjectConstructor(
        Engine engine,
        Realm realm)
        : base(engine, realm, _name)
    {
        PrototypeObject = new ObjectPrototype(engine, realm, this);
        _length = PropertyDescriptor.AllForbiddenDescriptor.NumberOne;
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    public ObjectPrototype PrototypeObject { get; }

    protected override void Initialize()
    {
        _prototype = _realm.Intrinsics.Function.PrototypeObject;
        CreateProperties_Generated();
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object.assign
    /// </summary>
    [JsFunction(Length = 2)]
    private ObjectInstance Assign(JsValue thisObject, JsValue target, [Rest] ReadOnlySpan<JsValue> sources)
    {
        var to = TypeConverter.ToObject(_realm, target);
        for (var i = 0; i < sources.Length; i++)
        {
            var nextSource = sources[i];
            if (nextSource.IsNullOrUndefined())
            {
                continue;
            }

            var from = TypeConverter.ToObject(_realm, nextSource);
            var keys = from.GetOwnPropertyKeys();
            foreach (var nextKey in keys)
            {
                var desc = from.GetOwnProperty(nextKey);
                if (desc != PropertyDescriptor.Undefined && desc.Enumerable)
                {
                    var propValue = from.Get(nextKey);
                    to.Set(nextKey, propValue, throwOnError: true);
                }
            }
        }
        return to;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object.entries
    /// </summary>
    [JsFunction(Length = 1)]
    private JsArray Entries(JsValue thisObject, JsValue value)
    {
        var obj = TypeConverter.ToObject(_realm, value);
        return obj.EnumerableOwnProperties(EnumerableOwnPropertyNamesKind.KeyValue);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object.fromentries
    /// </summary>
    [JsFunction(Length = 1)]
    private ObjectInstance FromEntries(JsValue thisObject, JsValue iterable)
    {
        TypeConverter.RequireObjectCoercible(_engine, iterable);

        var obj = _realm.Intrinsics.Object.Construct(0);

        var adder = CreateDataPropertyOnObject.Instance;
        var iterator = iterable.GetIterator(_realm);

        IteratorProtocol.AddEntriesFromIterable(obj, iterator, adder);

        return obj;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object.is
    /// </summary>
    [JsFunction(Length = 2)]
    private static JsValue Is(JsValue thisObject, JsValue value1, JsValue value2)
    {
        return SameValue(value1, value2);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object-value
    /// </summary>
    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        if (arguments.Length == 0)
        {
            return Construct(arguments);
        }

        if (arguments[0].IsNullOrUndefined())
        {
            return Construct(arguments);
        }

        return TypeConverter.ToObject(_realm, arguments[0]);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object-value
    /// </summary>
    public ObjectInstance Construct(JsCallArguments arguments)
    {
        return Construct(arguments, this);
    }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        if (!ReferenceEquals(this, newTarget) && !newTarget.IsUndefined())
        {
            return OrdinaryCreateFromConstructor(
                newTarget,
                static intrinsics => intrinsics.Object.PrototypeObject,
                static (Engine engine, Realm _, object? _) => new JsObject(engine));
        }

        if (arguments.Length > 0)
        {
            var value = arguments[0];
            if (value is ObjectInstance oi)
            {
                return oi;
            }

            var type = value.Type;
            if (type is Types.String or Types.Number or Types.Boolean)
            {
                return TypeConverter.ToObject(_realm, value);
            }
        }


        return new JsObject(_engine);
    }

    internal ObjectInstance Construct(int propertyCount)
    {
        var obj = new JsObject(_engine);
        obj.SetProperties(propertyCount > 0 ? new PropertyDictionary(propertyCount, checkExistingKeys: true) : null);
        return obj;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object.getprototypeof
    /// </summary>
    [JsFunction(Length = 1)]
    public JsValue GetPrototypeOf(JsValue thisObject, JsValue value)
    {
        var obj = TypeConverter.ToObject(_realm, value);
        return obj.Prototype ?? Null;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object.setprototypeof
    /// </summary>
    [JsFunction(Length = 2)]
    private JsValue SetPrototypeOf(JsValue thisObject, JsValue oArg, JsValue prototype)
    {
        TypeConverter.RequireObjectCoercible(_engine, oArg);

        if (!prototype.IsObject() && !prototype.IsNull())
        {
            Throw.TypeError(_realm, $"Object prototype may only be an Object or null: {prototype}");
        }

        if (oArg is not ObjectInstance o)
        {
            return oArg;
        }

        if (!o.SetPrototypeOf(prototype))
        {
            Throw.TypeError(_realm, "#<Object> is not extensible");
        }
        return o;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object.hasown
    /// </summary>
    [JsFunction(Length = 2)]
    private JsBoolean HasOwn(JsValue thisObject, JsValue value, JsValue propertyKey)
    {
        var o = TypeConverter.ToObject(_realm, value);
        var property = TypeConverter.ToPropertyKey(propertyKey);
        return o.HasOwnProperty(property) ? JsBoolean.True : JsBoolean.False;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object.getownpropertydescriptor
    /// </summary>
    [JsFunction(Length = 2)]
    internal JsValue GetOwnPropertyDescriptor(JsValue thisObject, JsValue value, JsValue propertyKey)
    {
        var o = TypeConverter.ToObject(_realm, value);
        var name = TypeConverter.ToPropertyKey(propertyKey);

        var desc = o.GetOwnProperty(name);
        return PropertyDescriptor.FromPropertyDescriptor(Engine, desc);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object.getownpropertydescriptors
    /// </summary>
    [JsFunction(Length = 1)]
    private ObjectInstance GetOwnPropertyDescriptors(JsValue thisObject, JsValue value)
    {
        var o = TypeConverter.ToObject(_realm, value);
        var ownKeys = o.GetOwnPropertyKeys();
        var descriptors = _realm.Intrinsics.Object.Construct(0);
        foreach (var key in ownKeys)
        {
            var desc = o.GetOwnProperty(key);
            var descriptor = PropertyDescriptor.FromPropertyDescriptor(Engine, desc);
            if (!ReferenceEquals(descriptor, Undefined))
            {
                descriptors.CreateDataProperty(key, descriptor);
            }
        }
        return descriptors;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object.getownpropertynames
    /// </summary>
    [JsFunction(Length = 1)]
    private JsArray GetOwnPropertyNames(JsValue thisObject, JsValue value)
    {
        var o = TypeConverter.ToObject(_realm, value);
        var names = o.GetOwnPropertyKeys(Types.String);
        return _realm.Intrinsics.Array.ConstructFast(names);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object.getownpropertysymbols
    /// </summary>
    [JsFunction(Length = 1)]
    private JsArray GetOwnPropertySymbols(JsValue thisObject, JsValue value)
    {
        var o = TypeConverter.ToObject(_realm, value);
        var keys = o.GetOwnPropertyKeys(Types.Symbol);
        return _realm.Intrinsics.Array.ConstructFast(keys);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object.create
    /// </summary>
    [JsFunction(Length = 2)]
    private ObjectInstance Create(JsValue thisObject, JsValue prototype, JsValue properties)
    {
        if (!prototype.IsObject() && !prototype.IsNull())
        {
            Throw.TypeError(_realm, "Object prototype may only be an Object or null: " + prototype);
        }

        var obj = Engine.Realm.Intrinsics.Object.Construct(Arguments.Empty);
        obj._prototype = prototype.IsNull() ? null : prototype.AsObject();

        if (!properties.IsUndefined())
        {
            ObjectDefineProperties(obj, properties);
        }

        return obj;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object.defineproperty
    /// </summary>
    [JsFunction(Length = 3)]
    private JsValue DefineProperty(JsValue thisObject, JsValue value, JsValue propertyKey, JsValue attributes)
    {
        if (value is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "Object.defineProperty called on non-object");
            return null;
        }

        var name = TypeConverter.ToPropertyKey(propertyKey);

        var desc = PropertyDescriptor.ToPropertyDescriptor(_realm, attributes);

        o.DefinePropertyOrThrow(name, desc);

        return value;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object.defineproperties
    /// </summary>
    [JsFunction(Length = 2)]
    private ObjectInstance DefineProperties(JsValue thisObject, JsValue value, JsValue properties)
    {
        var o = value as ObjectInstance;
        if (o is null)
        {
            Throw.TypeError(_realm, "Object.defineProperties called on non-object");
        }

        return ObjectDefineProperties(o, properties);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-objectdefineproperties
    /// </summary>
    private ObjectInstance ObjectDefineProperties(ObjectInstance o, JsValue properties)
    {
        var props = TypeConverter.ToObject(_realm, properties);
        var keys = props.GetOwnPropertyKeys();
        var descriptors = new List<KeyValuePair<JsValue, PropertyDescriptor>>();
        for (var i = 0; i < keys.Count; i++)
        {
            var nextKey = keys[i];
            var propDesc = props.GetOwnProperty(nextKey);
            if (propDesc == PropertyDescriptor.Undefined || !propDesc.Enumerable)
            {
                continue;
            }

            var descObj = props.Get(nextKey);
            var desc = PropertyDescriptor.ToPropertyDescriptor(_realm, descObj);
            descriptors.Add(new KeyValuePair<JsValue, PropertyDescriptor>(nextKey, desc));
        }

        foreach (var pair in descriptors)
        {
            o.DefinePropertyOrThrow(pair.Key, pair.Value);
        }

        return o;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object.seal
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Seal(JsValue thisObject, JsValue value)
    {
        if (value is not ObjectInstance o)
        {
            return value;
        }

        var status = o.SetIntegrityLevel(IntegrityLevel.Sealed);

        if (!status)
        {
            Throw.TypeError(_realm, "Cannot seal");
        }

        return o;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object.freeze
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Freeze(JsValue thisObject, JsValue value)
    {
        if (value is not ObjectInstance o)
        {
            return value;
        }

        var status = o.SetIntegrityLevel(IntegrityLevel.Frozen);

        if (!status)
        {
            Throw.TypeError(_realm, "Cannot freeze");
        }

        return o;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object.preventextensions
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue PreventExtensions(JsValue thisObject, JsValue value)
    {
        if (value is not ObjectInstance o)
        {
            return value;
        }

        if (!o.PreventExtensions())
        {
            Throw.TypeError(_realm, "Cannot prevent extensions");
        }

        return o;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object.issealed
    /// </summary>
    [JsFunction(Length = 1)]
    private static JsBoolean IsSealed(JsValue thisObject, JsValue value)
    {
        if (value is not ObjectInstance o)
        {
            return JsBoolean.True;
        }

        return TestIntegrityLevel(o, IntegrityLevel.Sealed);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object.isfrozen
    /// </summary>
    [JsFunction(Length = 1)]
    private static JsBoolean IsFrozen(JsValue thisObject, JsValue value)
    {
        if (value is not ObjectInstance o)
        {
            return JsBoolean.True;
        }

        return TestIntegrityLevel(o, IntegrityLevel.Frozen);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-testintegritylevel
    /// </summary>
    private static JsBoolean TestIntegrityLevel(ObjectInstance o, IntegrityLevel level)
    {
        if (o.Extensible)
        {
            return JsBoolean.False;
        }

        foreach (var k in o.GetOwnPropertyKeys())
        {
            var currentDesc = o.GetOwnProperty(k);
            if (currentDesc != PropertyDescriptor.Undefined)
            {
                if (currentDesc.Configurable)
                {
                    return JsBoolean.False;
                }

                if (level == IntegrityLevel.Frozen && currentDesc.IsDataDescriptor())
                {
                    if (currentDesc.Writable)
                    {
                        return JsBoolean.False;
                    }
                }
            }
        }

        return JsBoolean.True;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object.isextensible
    /// </summary>
    [JsFunction(Length = 1)]
    private static JsValue IsExtensible(JsValue thisObject, JsValue value)
    {
        if (value is not ObjectInstance o)
        {
            return JsBoolean.False;
        }

        return o.Extensible;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object.keys
    /// </summary>
    [JsFunction(Length = 1)]
    private JsArray Keys(JsValue thisObject, JsValue value)
    {
        var o = TypeConverter.ToObject(_realm, value);
        return o.EnumerableOwnProperties(EnumerableOwnPropertyNamesKind.Key);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object.values
    /// </summary>
    [JsFunction(Length = 1)]
    private JsArray Values(JsValue thisObject, JsValue value)
    {
        var o = TypeConverter.ToObject(_realm, value);
        return o.EnumerableOwnProperties(EnumerableOwnPropertyNamesKind.Value);
    }

    /// <summary>
    /// https://tc39.es/proposal-array-grouping/#sec-object.groupby
    /// </summary>
    [JsFunction(Length = 2)]
    private JsObject GroupBy(JsValue thisObject, JsValue items, JsValue callbackfn)
    {
        var grouping = GroupByHelper.GroupBy(_engine, _realm, items, callbackfn, mapMode: false);

        var obj = OrdinaryObjectCreate(_engine, null);
        foreach (var pair in grouping)
        {
            obj.FastSetProperty(pair.Key, new PropertyDescriptor(pair.Value, PropertyFlag.ConfigurableEnumerableWritable));
        }

        return obj;
    }

    private sealed class CreateDataPropertyOnObject : ICallable
    {
        internal static readonly CreateDataPropertyOnObject Instance = new();

        private CreateDataPropertyOnObject()
        {
        }

        public JsValue Call(JsValue thisObject, params JsCallArguments arguments)
        {
            var o = (ObjectInstance) thisObject;
            var key = arguments.At(0);
            var value = arguments.At(1);
            var propertyKey = TypeConverter.ToPropertyKey(key);

            o.CreateDataPropertyOrThrow(propertyKey, value);

            return Undefined;
        }
    }
}
