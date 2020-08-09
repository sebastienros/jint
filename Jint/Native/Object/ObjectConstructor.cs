using System.Collections.Generic;
using Jint.Collections;
using Jint.Native.Function;
using Jint.Native.Iterator;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Object
{
    public sealed class ObjectConstructor : FunctionInstance, IConstructor
    {
        private static readonly JsString _name = new JsString("delegate");

        private ObjectConstructor(Engine engine)
            : base(engine, _name)
        {
        }

        public static ObjectConstructor CreateObjectConstructor(Engine engine)
        {
            var obj = new ObjectConstructor(engine);

            obj.PrototypeObject = ObjectPrototype.CreatePrototypeObject(engine, obj);

            obj._length = PropertyDescriptor.AllForbiddenDescriptor.NumberOne;
            obj._prototypeDescriptor = new PropertyDescriptor(obj.PrototypeObject, PropertyFlag.AllForbidden);

            return obj;
        }

        protected override void Initialize()
        {
            _prototype = Engine.Function.PrototypeObject;

            const PropertyFlag propertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
            const PropertyFlag lengthFlags = PropertyFlag.Configurable;
            var properties = new PropertyDictionary(15, checkExistingKeys: false)
            {
                ["assign"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "assign", Assign, 2, lengthFlags), propertyFlags),
                ["entries"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "entries", Entries, 1, lengthFlags), propertyFlags),
                ["fromEntries"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "fromEntries", FromEntries, 1, lengthFlags), propertyFlags),
                ["getPrototypeOf"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getPrototypeOf", GetPrototypeOf, 1), propertyFlags),
                ["getOwnPropertyDescriptor"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getOwnPropertyDescriptor", GetOwnPropertyDescriptor, 2, lengthFlags), propertyFlags),
                ["getOwnPropertyDescriptors"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getOwnPropertyDescriptors", GetOwnPropertyDescriptors, 1, lengthFlags), propertyFlags),
                ["getOwnPropertyNames"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getOwnPropertyNames", GetOwnPropertyNames, 1), propertyFlags),
                ["getOwnPropertySymbols"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getOwnPropertySymbols", GetOwnPropertySymbols, 1, lengthFlags), propertyFlags),
                ["create"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "create", Create, 2), propertyFlags),
                ["defineProperty"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "defineProperty", DefineProperty, 3), propertyFlags),
                ["defineProperties"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "defineProperties", DefineProperties, 2), propertyFlags),
                ["is"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "is", Is, 2, lengthFlags), propertyFlags),
                ["seal"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "seal", Seal, 1), propertyFlags),
                ["freeze"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "freeze", Freeze, 1), propertyFlags),
                ["preventExtensions"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "preventExtensions", PreventExtensions, 1), propertyFlags),
                ["isSealed"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "isSealed", IsSealed, 1), propertyFlags),
                ["isFrozen"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "isFrozen", IsFrozen, 1), propertyFlags),
                ["isExtensible"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "isExtensible", IsExtensible, 1), propertyFlags),
                ["keys"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "keys", Keys, 1, lengthFlags), propertyFlags),
                ["values"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "values", Values, 1, lengthFlags), propertyFlags),
                ["setPrototypeOf"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setPrototypeOf", SetPrototypeOf, 2, lengthFlags), propertyFlags)
            };
            SetProperties(properties);
        }

        private JsValue Assign(JsValue thisObject, JsValue[] arguments)
        {
            var to = TypeConverter.ToObject(_engine, arguments.At(0));
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

                var from = TypeConverter.ToObject(_engine, nextSource);
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

        private JsValue Entries(JsValue thisObject, JsValue[] arguments)
        {
            var obj = TypeConverter.ToObject(_engine, arguments.At(0));
            var nameList = obj.EnumerableOwnPropertyNames(EnumerableOwnPropertyNamesKind.KeyValue);
            return nameList;
        }

        private JsValue FromEntries(JsValue thisObject, JsValue[] arguments)
        {
            var iterable = arguments.At(0);
            TypeConverter.CheckObjectCoercible(_engine, iterable);

            var obj = _engine.Object.Construct(0);

            var adder = CreateDataPropertyOnObject.Instance;
            var iterator = arguments.At(0).GetIterator(_engine);

            IteratorProtocol.AddEntriesFromIterable(obj, iterator, adder);

            return obj;
        }

        private static JsValue Is(JsValue thisObject, JsValue[] arguments)
        {
            return SameValue(arguments.At(0), arguments.At(1));
        }

        public ObjectPrototype PrototypeObject { get; private set; }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.2.1.1
        /// </summary>
        /// <param name="thisObject"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            if (arguments.Length == 0)
            {
                return Construct(arguments);
            }

            if(arguments[0].IsNullOrUndefined())
            {
                return Construct(arguments);
            }

            return TypeConverter.ToObject(_engine, arguments[0]);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.2.2.1
        /// </summary>
        public ObjectInstance Construct(JsValue[] arguments)
        {
            return Construct(arguments, this);
        }

        public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            if (!ReferenceEquals(this, newTarget) && !newTarget.IsUndefined())
            {
                return OrdinaryCreateFromConstructor(newTarget, _engine.Object.PrototypeObject, (engine, state) => new ObjectInstance(engine));
            }
            
            if (arguments.Length > 0)
            {
                var value = arguments[0];
                if (value is ObjectInstance oi)
                {
                    return oi;
                }

                var type = value.Type;
                if (type == Types.String || type == Types.Number || type == Types.Boolean)
                {
                    return TypeConverter.ToObject(_engine, value);
                }
            }

            var obj = new ObjectInstance(_engine)
            {
                _prototype = Engine.Object.PrototypeObject
            };

            return obj;
        }

        internal ObjectInstance Construct(int propertyCount)
        {
            var obj = new ObjectInstance(_engine)
            {
                _prototype = Engine.Object.PrototypeObject,
            };

            obj.SetProperties(propertyCount > 0  ? new PropertyDictionary(propertyCount, checkExistingKeys: true) : null);

            return obj;
        }

        public JsValue GetPrototypeOf(JsValue thisObject, JsValue[] arguments)
        {
            var obj = TypeConverter.ToObject(_engine, arguments.At(0));
            return obj.Prototype ?? Null;
        }

        private JsValue SetPrototypeOf(JsValue thisObject, JsValue[] arguments)
        {
            var oArg = arguments.At(0);
            TypeConverter.CheckObjectCoercible(_engine, oArg);

            var prototype = arguments.At(1);
            if (!prototype.IsObject() && !prototype.IsNull())
            {
                ExceptionHelper.ThrowTypeError(_engine, $"Object prototype may only be an Object or null: {prototype}");
            }

            if (!(oArg is ObjectInstance o))
            {
                return oArg;
            }

            if (!o.SetPrototypeOf(prototype))
            {
                ExceptionHelper.ThrowTypeError(_engine);
            }
            return o;
        }

        internal JsValue GetOwnPropertyDescriptor(JsValue thisObject, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(_engine, arguments.At(0));

            var p = arguments.At(1);
            var name = TypeConverter.ToPropertyKey(p);

            var desc = o.GetOwnProperty(name);
            return PropertyDescriptor.FromPropertyDescriptor(Engine, desc);
        }

        private JsValue GetOwnPropertyDescriptors(JsValue thisObject, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(_engine, arguments.At(0));
            var ownKeys = o.GetOwnPropertyKeys();
            var descriptors = _engine.Object.Construct(0);
            foreach (var key in ownKeys)
            {
                var desc = o.GetOwnProperty(key);
                var descriptor = PropertyDescriptor.FromPropertyDescriptor(Engine, desc);
                if (descriptor != Undefined)
                {
                    descriptors.CreateDataProperty(key, descriptor);
                }
            }
            return descriptors;
        }

        public JsValue GetOwnPropertyNames(JsValue thisObject, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(_engine, arguments.At(0));
            var names = o.GetOwnPropertyKeys(Types.String);
            return _engine.Array.Construct(names.ToArray());
        }

        private JsValue GetOwnPropertySymbols(JsValue thisObject, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(_engine, arguments.At(0));
            var keys = o.GetOwnPropertyKeys(Types.Symbol);
            return _engine.Array.Construct(keys.ToArray());
        }

        private JsValue Create(JsValue thisObject, JsValue[] arguments)
        {
            var prototype = arguments.At(0);
            if (!prototype.IsObject() && !prototype.IsNull())
            {
                ExceptionHelper.ThrowTypeError(_engine, "Object prototype may only be an Object or null: " + prototype);
            }

            var obj = Engine.Object.Construct(Arguments.Empty);
            obj._prototype = prototype.IsNull() ? null : prototype.AsObject();

            var properties = arguments.At(1);
            if (!properties.IsUndefined())
            {
                var jsValues = _engine._jsValueArrayPool.RentArray(2);
                jsValues[0] = obj;
                jsValues[1] = properties;
                DefineProperties(thisObject, jsValues);
                _engine._jsValueArrayPool.ReturnArray(jsValues);
            }

            return obj;
        }

        private JsValue DefineProperty(JsValue thisObject, JsValue[] arguments)
        {
            if (!(arguments.At(0) is ObjectInstance o))
            {
                return ExceptionHelper.ThrowTypeError<JsValue>(_engine, "Object.defineProperty called on non-object");
            }

            var p = arguments.At(1);
            var name = TypeConverter.ToPropertyKey(p);

            var attributes = arguments.At(2);
            var desc = PropertyDescriptor.ToPropertyDescriptor(Engine, attributes);
            o.DefinePropertyOrThrow(name, desc);

            return arguments.At(0);
        }

        private JsValue DefineProperties(JsValue thisObject, JsValue[] arguments)
        {
            if (!(arguments.At(0) is ObjectInstance o))
            {
                return ExceptionHelper.ThrowTypeError<JsValue>(_engine, "Object.defineProperty called on non-object");
            }

            var properties = arguments.At(1);
            var props = TypeConverter.ToObject(Engine, properties);
            var descriptors = new List<KeyValuePair<JsValue, PropertyDescriptor>>();
            foreach (var p in props.GetOwnProperties())
            {
                if (!p.Value.Enumerable)
                {
                    continue;
                }

                var descObj = props.Get(p.Key, props);
                var desc = PropertyDescriptor.ToPropertyDescriptor(Engine, descObj);
                descriptors.Add(new KeyValuePair<JsValue, PropertyDescriptor>(p.Key, desc));
            }
            foreach (var pair in descriptors)
            {
                o.DefinePropertyOrThrow(pair.Key, pair.Value);
            }

            return o;
        }

        private JsValue Seal(JsValue thisObject, JsValue[] arguments)
        {
            if (!(arguments.At(0) is ObjectInstance o))
            {
                return arguments.At(0);
            }

            var properties = new List<KeyValuePair<JsValue, PropertyDescriptor>>(o.GetOwnProperties());
            foreach (var prop in properties)
            {
                var propertyDescriptor = prop.Value;
                if (propertyDescriptor.Configurable)
                {
                    propertyDescriptor.Configurable = false;
                    FastSetProperty(prop.Key, propertyDescriptor);
                }

                o.DefinePropertyOrThrow(prop.Key, propertyDescriptor);
            }

            o.PreventExtensions();

            return o;
        }

        private static JsValue Freeze(JsValue thisObject, JsValue[] arguments)
        {
            if (!(arguments.At(0) is ObjectInstance o))
            {
                return arguments.At(0);
            }

            foreach (var p in o.GetOwnProperties())
            {
                var desc = o.GetOwnProperty(p.Key);
                if (desc.IsDataDescriptor())
                {
                    if (desc.Writable)
                    {
                        var mutable = desc;
                        mutable.Writable = false;
                        desc = mutable;
                    }
                }
                if (desc.Configurable)
                {
                    var mutable = desc;
                    mutable.Configurable = false;
                    desc = mutable;
                }
                o.DefinePropertyOrThrow(p.Key, desc);
            }

            o.PreventExtensions();

            return o;
        }

        private static JsValue PreventExtensions(JsValue thisObject, JsValue[] arguments)
        {
            if (!(arguments.At(0) is ObjectInstance o))
            {
                return arguments.At(0);
            }

            o.PreventExtensions();
            return o;
        }

        private static JsValue IsSealed(JsValue thisObject, JsValue[] arguments)
        {
            if (!(arguments.At(0) is ObjectInstance o))
            {
                return arguments.At(0);
            }

            foreach (var prop in o.GetOwnProperties())
            {
                if (prop.Value.Configurable)
                {
                    return false;
                }
            }

            if (o.Extensible == false)
            {
                return true;
            }

            return false;
        }

        private static JsValue IsFrozen(JsValue thisObject, JsValue[] arguments)
        {
            if (!(arguments.At(0) is ObjectInstance o))
            {
                return arguments.At(0);
            }

            foreach (var pair in o.GetOwnProperties())
            {
                var desc = pair.Value;
                if (desc.IsDataDescriptor())
                {
                    if (desc.Writable)
                    {
                        return false;
                    }
                }
                if (desc.Configurable)
                {
                    return false;
                }
            }

            if (o.Extensible == false)
            {
                return true;
            }

            return false;
        }

        private static JsValue IsExtensible(JsValue thisObject, JsValue[] arguments)
        {
            if (!(arguments.At(0) is ObjectInstance o))
            {
                return arguments.At(0);
            }

            return o.Extensible;
        }

        private JsValue Keys(JsValue thisObject, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(_engine, arguments.At(0));
            return o.EnumerableOwnPropertyNames(EnumerableOwnPropertyNamesKind.Key);
        }

        private JsValue Values(JsValue thisObject, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(_engine, arguments.At(0));
            return o.EnumerableOwnPropertyNames(EnumerableOwnPropertyNamesKind.Value);
        }

        private sealed class CreateDataPropertyOnObject : ICallable
        {
            internal static readonly CreateDataPropertyOnObject Instance = new CreateDataPropertyOnObject();

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
