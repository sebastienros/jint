using Jint.Collections;
using Jint.Native.Iterator;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Object
{
    public sealed class ObjectConstructor : Constructor
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

            const PropertyFlag PropertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
            const PropertyFlag LengthFlags = PropertyFlag.Configurable;
            var properties = new PropertyDictionary(16, checkExistingKeys: false)
            {
                ["assign"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "assign", Assign, 2, LengthFlags), PropertyFlags),
                ["entries"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "entries", Entries, 1, LengthFlags), PropertyFlags),
                ["fromEntries"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "fromEntries", FromEntries, 1, LengthFlags), PropertyFlags),
                ["getPrototypeOf"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getPrototypeOf", GetPrototypeOf, 1), PropertyFlags),
                ["getOwnPropertyDescriptor"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getOwnPropertyDescriptor", GetOwnPropertyDescriptor, 2, LengthFlags), PropertyFlags),
                ["getOwnPropertyDescriptors"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getOwnPropertyDescriptors", GetOwnPropertyDescriptors, 1, LengthFlags), PropertyFlags),
                ["getOwnPropertyNames"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getOwnPropertyNames", GetOwnPropertyNames, 1), PropertyFlags),
                ["getOwnPropertySymbols"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getOwnPropertySymbols", GetOwnPropertySymbols, 1, LengthFlags), PropertyFlags),
                ["groupBy"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "groupBy", GroupBy, 2, PropertyFlag.Configurable), PropertyFlags),
                ["create"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "create", Create, 2), PropertyFlags),
                ["defineProperty"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "defineProperty", DefineProperty, 3), PropertyFlags),
                ["defineProperties"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "defineProperties", DefineProperties, 2), PropertyFlags),
                ["is"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "is", Is, 2, LengthFlags), PropertyFlags),
                ["seal"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "seal", Seal, 1, LengthFlags), PropertyFlags),
                ["freeze"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "freeze", Freeze, 1), PropertyFlags),
                ["preventExtensions"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "preventExtensions", PreventExtensions, 1), PropertyFlags),
                ["isSealed"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "isSealed", IsSealed, 1), PropertyFlags),
                ["isFrozen"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "isFrozen", IsFrozen, 1), PropertyFlags),
                ["isExtensible"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "isExtensible", IsExtensible, 1), PropertyFlags),
                ["keys"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "keys", Keys, 1, LengthFlags), PropertyFlags),
                ["values"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "values", Values, 1, LengthFlags), PropertyFlags),
                ["setPrototypeOf"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setPrototypeOf", SetPrototypeOf, 2, LengthFlags), PropertyFlags),
                ["hasOwn"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "hasOwn", HasOwn, 2, LengthFlags), PropertyFlags),
            };
            SetProperties(properties);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object.assign
        /// </summary>
        private JsValue Assign(JsValue thisObject, JsValue[] arguments)
        {
            var to = TypeConverter.ToObject(_realm, arguments.At(0));
            if (arguments.Length < 2)
            {
                return to;
            }

            for (var i = 1; i < arguments.Length; i++)
            {
                var nextSource = arguments[i];
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
        private JsValue Entries(JsValue thisObject, JsValue[] arguments)
        {
            var obj = TypeConverter.ToObject(_realm, arguments.At(0));
            var nameList = obj.EnumerableOwnPropertyNames(EnumerableOwnPropertyNamesKind.KeyValue);
            return nameList;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object.fromentries
        /// </summary>
        private JsValue FromEntries(JsValue thisObject, JsValue[] arguments)
        {
            var iterable = arguments.At(0);
            TypeConverter.CheckObjectCoercible(_engine, iterable);

            var obj = _realm.Intrinsics.Object.Construct(0);

            var adder = CreateDataPropertyOnObject.Instance;
            var iterator = arguments.At(0).GetIterator(_realm);

            IteratorProtocol.AddEntriesFromIterable(obj, iterator, adder);

            return obj;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object.is
        /// </summary>
        private static JsValue Is(JsValue thisObject, JsValue[] arguments)
        {
            return SameValue(arguments.At(0), arguments.At(1));
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object-value
        /// </summary>
        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            if (arguments.Length == 0)
            {
                return Construct(arguments);
            }

            if(arguments[0].IsNullOrUndefined())
            {
                return Construct(arguments);
            }

            return TypeConverter.ToObject(_realm, arguments[0]);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object-value
        /// </summary>
        public ObjectInstance Construct(JsValue[] arguments)
        {
            return Construct(arguments, this);
        }

        public override ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
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
            obj.SetProperties(propertyCount > 0  ? new PropertyDictionary(propertyCount, checkExistingKeys: true) : null);
            return obj;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object.getprototypeof
        /// </summary>
        public JsValue GetPrototypeOf(JsValue thisObject, JsValue[] arguments)
        {
            var obj = TypeConverter.ToObject(_realm, arguments.At(0));
            return obj.Prototype ?? Null;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object.setprototypeof
        /// </summary>
        private JsValue SetPrototypeOf(JsValue thisObject, JsValue[] arguments)
        {
            var oArg = arguments.At(0);
            TypeConverter.CheckObjectCoercible(_engine, oArg);

            var prototype = arguments.At(1);
            if (!prototype.IsObject() && !prototype.IsNull())
            {
                ExceptionHelper.ThrowTypeError(_realm, $"Object prototype may only be an Object or null: {prototype}");
            }

            if (!(oArg is ObjectInstance o))
            {
                return oArg;
            }

            if (!o.SetPrototypeOf(prototype))
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }
            return o;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object.hasown
        /// </summary>
        private JsValue HasOwn(JsValue thisObject, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(_realm, arguments.At(0));
            var property = TypeConverter.ToPropertyKey(arguments.At(1));
            return o.HasOwnProperty(property) ? JsBoolean.True : JsBoolean.False;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object.getownpropertydescriptor
        /// </summary>
        internal JsValue GetOwnPropertyDescriptor(JsValue thisObject, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(_realm, arguments.At(0));

            var p = arguments.At(1);
            var name = TypeConverter.ToPropertyKey(p);

            var desc = o.GetOwnProperty(name);
            return PropertyDescriptor.FromPropertyDescriptor(Engine, desc);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object.getownpropertydescriptors
        /// </summary>
        private JsValue GetOwnPropertyDescriptors(JsValue thisObject, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(_realm, arguments.At(0));
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
        private JsValue GetOwnPropertyNames(JsValue thisObject, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(_realm, arguments.At(0));
            var names = o.GetOwnPropertyKeys(Types.String);
            return _realm.Intrinsics.Array.ConstructFast(names);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object.getownpropertysymbols
        /// </summary>
        private JsValue GetOwnPropertySymbols(JsValue thisObject, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(_realm, arguments.At(0));
            var keys = o.GetOwnPropertyKeys(Types.Symbol);
            return _realm.Intrinsics.Array.ConstructFast(keys);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object.create
        /// </summary>
        private JsValue Create(JsValue thisObject, JsValue[] arguments)
        {
            var prototype = arguments.At(0);
            if (!prototype.IsObject() && !prototype.IsNull())
            {
                ExceptionHelper.ThrowTypeError(_realm, "Object prototype may only be an Object or null: " + prototype);
            }

            var obj = Engine.Realm.Intrinsics.Object.Construct(Arguments.Empty);
            obj._prototype = prototype.IsNull() ? null : prototype.AsObject();

            var properties = arguments.At(1);
            if (!properties.IsUndefined())
            {
                ObjectDefineProperties(obj, properties);
            }

            return obj;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object.defineproperty
        /// </summary>
        private JsValue DefineProperty(JsValue thisObject, JsValue[] arguments)
        {
            if (arguments.At(0) is not ObjectInstance o)
            {
                ExceptionHelper.ThrowTypeError(_realm, "Object.defineProperty called on non-object");
                return null;
            }

            var p = arguments.At(1);
            var name = TypeConverter.ToPropertyKey(p);

            var attributes = arguments.At(2);
            var desc = PropertyDescriptor.ToPropertyDescriptor(_realm, attributes);

            o.DefinePropertyOrThrow(name, desc);

            return arguments.At(0);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object.defineproperties
        /// </summary>
        private JsValue DefineProperties(JsValue thisObject, JsValue[] arguments)
        {
            var o = arguments.At(0) as ObjectInstance;
            if (o is null)
            {
                ExceptionHelper.ThrowTypeError(_realm, "Object.defineProperty called on non-object");
            }

            var properties = arguments.At(1);
            return ObjectDefineProperties(o, properties);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-objectdefineproperties
        /// </summary>
        private JsValue ObjectDefineProperties(ObjectInstance o, JsValue properties)
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
        private JsValue Seal(JsValue thisObject, JsValue[] arguments)
        {
            if (arguments.At(0) is not ObjectInstance o)
            {
                return arguments.At(0);
            }

            var status = o.SetIntegrityLevel(IntegrityLevel.Sealed);

            if (!status)
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            return o;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object.freeze
        /// </summary>
        private JsValue Freeze(JsValue thisObject, JsValue[] arguments)
        {
            if (arguments.At(0) is not ObjectInstance o)
            {
                return arguments.At(0);
            }

            var status = o.SetIntegrityLevel(IntegrityLevel.Frozen);

            if (!status)
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            return o;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object.preventextensions
        /// </summary>
        private JsValue PreventExtensions(JsValue thisObject, JsValue[] arguments)
        {
            if (arguments.At(0) is not ObjectInstance o)
            {
                return arguments.At(0);
            }

            if (!o.PreventExtensions())
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            return o;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object.issealed
        /// </summary>
        private static JsValue IsSealed(JsValue thisObject, JsValue[] arguments)
        {
            if (arguments.At(0) is not ObjectInstance o)
            {
                return JsBoolean.True;
            }

            return TestIntegrityLevel(o, IntegrityLevel.Sealed);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object.isfrozen
        /// </summary>
        private static JsValue IsFrozen(JsValue thisObject, JsValue[] arguments)
        {
            if (arguments.At(0) is not ObjectInstance o)
            {
                return JsBoolean.True;
            }

            return TestIntegrityLevel(o, IntegrityLevel.Frozen);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-testintegritylevel
        /// </summary>
        private static JsValue TestIntegrityLevel(ObjectInstance o, IntegrityLevel level)
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
        private static JsValue IsExtensible(JsValue thisObject, JsValue[] arguments)
        {
            if (arguments.At(0) is not ObjectInstance o)
            {
                return JsBoolean.False;
            }

            return o.Extensible;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object.keys
        /// </summary>
        private JsValue Keys(JsValue thisObject, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(_realm, arguments.At(0));
            return o.EnumerableOwnPropertyNames(EnumerableOwnPropertyNamesKind.Key);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object.values
        /// </summary>
        private JsValue Values(JsValue thisObject, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(_realm, arguments.At(0));
            return o.EnumerableOwnPropertyNames(EnumerableOwnPropertyNamesKind.Value);
        }

        /// <summary>
        /// https://tc39.es/proposal-array-grouping/#sec-object.groupby
        /// </summary>
        private JsValue GroupBy(JsValue thisObject, JsValue[] arguments)
        {
            var items = arguments.At(0);
            var callbackfn = arguments.At(1);
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

            public JsValue Call(JsValue thisObject, JsValue[] arguments)
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
}
