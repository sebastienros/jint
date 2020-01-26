using System.Collections.Generic;
using Jint.Collections;
using Jint.Native.Array;
using Jint.Native.Function;
using Jint.Native.String;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Object
{
    public sealed class ObjectConstructor : FunctionInstance, IConstructor
    {
        private ObjectConstructor(Engine engine)
            : base(engine, "Object", null, null, false)
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

            var properties = new PropertyDictionary(15, checkExistingKeys: false)
            {
                ["getPrototypeOf"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getPrototypeOf", GetPrototypeOf, 1), true, false, true),
                ["getOwnPropertyDescriptor"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getOwnPropertyDescriptor", GetOwnPropertyDescriptor, 2), true, false, true),
                ["getOwnPropertyNames"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getOwnPropertyNames", GetOwnPropertyNames, 1), true, false, true),
                ["getOwnPropertySymbols"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getOwnPropertySymbols", GetOwnPropertySymbols, 1), true, false, true),
                ["create"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "create", Create, 2), true, false, true),
                ["defineProperty"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "defineProperty", DefineProperty, 3), true, false, true),
                ["defineProperties"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "defineProperties", DefineProperties, 2), true, false, true),
                ["seal"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "seal", Seal, 1), true, false, true),
                ["freeze"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "freeze", Freeze, 1), true, false, true),
                ["preventExtensions"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "preventExtensions", PreventExtensions, 1), true, false, true),
                ["isSealed"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "isSealed", IsSealed, 1), true, false, true),
                ["isFrozen"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "isFrozen", IsFrozen, 1), true, false, true),
                ["isExtensible"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "isExtensible", IsExtensible, 1), true, false, true),
                ["keys"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "keys", Keys, 1), true, false, true),
                ["setPrototypeOf"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setPrototypeOf", SetPrototypeOf, 2), true, false, true)
            };
            SetProperties(properties);
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

        public JsValue SetPrototypeOf(JsValue thisObject, JsValue[] arguments)
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

        public JsValue GetOwnPropertyDescriptor(JsValue thisObject, JsValue[] arguments)
        {
            var o = arguments.As<ObjectInstance>(0, _engine);

            var p = arguments.At(1);
            var name = TypeConverter.ToPropertyKey(p);

            var desc = o.GetOwnProperty(name);
            return PropertyDescriptor.FromPropertyDescriptor(Engine, desc);
        }

        public JsValue GetOwnPropertyNames(JsValue thisObject, JsValue[] arguments)
        {
            var o = arguments.As<ObjectInstance>(0, _engine);

            uint n = 0;

            ArrayInstance array = null;
            var ownProperties = o.GetOwnPropertyKeys(Types.String);
            if (o is StringInstance s)
            {
                var length = s.PrimitiveValue.Length;
                array = Engine.Array.ConstructFast((uint) (ownProperties.Count + length + 1));
                for (var i = 0; i < length; i++)
                {
                    array.SetIndexValue(n++, TypeConverter.ToString(i), updateLength: false);
                }

                array.SetIndexValue(n++, CommonProperties.Length, updateLength: false);
            }

            array = array ?? Engine.Array.ConstructFast((uint) ownProperties.Count);
            for (var i = 0; i < ownProperties.Count; i++)
            {
                var p = ownProperties[i];
                array.SetIndexValue(n++, p, false);
            }

            array.SetLength(n);
            return array;
        }

        public JsValue GetOwnPropertySymbols(JsValue thisObject, JsValue[] arguments)
        {
            var o = arguments.As<ObjectInstance>(0, _engine);
            var keys = o.GetOwnPropertyKeys(Types.Symbol);
            return _engine.Array.Construct(keys.ToArray());
        }

        public JsValue Create(JsValue thisObject, JsValue[] arguments)
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

        public JsValue DefineProperty(JsValue thisObject, JsValue[] arguments)
        {
            var o = arguments.As<ObjectInstance>(0, _engine);
            var p = arguments.At(1);
            var name = TypeConverter.ToPropertyKey(p);

            var attributes = arguments.At(2);
            var desc = PropertyDescriptor.ToPropertyDescriptor(Engine, attributes);
            o.DefinePropertyOrThrow(name, desc);

            return o;
        }

        public JsValue DefineProperties(JsValue thisObject, JsValue[] arguments)
        {
            var o = arguments.As<ObjectInstance>(0, _engine);
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

        public JsValue Seal(JsValue thisObject, JsValue[] arguments)
        {
            var o = arguments.As<ObjectInstance>(0, _engine);
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

        public JsValue Freeze(JsValue thisObject, JsValue[] arguments)
        {
            var o = arguments.As<ObjectInstance>(0, _engine);
            var properties = new List<KeyValuePair<JsValue, PropertyDescriptor>>(o.GetOwnProperties());
            foreach (var p in properties)
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

        public JsValue PreventExtensions(JsValue thisObject, JsValue[] arguments)
        {
            var o = arguments.As<ObjectInstance>(0, _engine);
            o.PreventExtensions();
            return o;
        }

        public JsValue IsSealed(JsValue thisObject, JsValue[] arguments)
        {
            var o = arguments.As<ObjectInstance>(0, _engine);
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

        public JsValue IsFrozen(JsValue thisObject, JsValue[] arguments)
        {
            var o = arguments.As<ObjectInstance>(0, _engine);
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

        public JsValue IsExtensible(JsValue thisObject, JsValue[] arguments)
        {
            var o = arguments.As<ObjectInstance>(0, _engine);
            return o.Extensible;
        }

        public JsValue Keys(JsValue thisObject, JsValue[] arguments)
        {
            return EnumerableOwnPropertyNames(arguments, EnumerableOwnPropertyNamesKind.Key);
        }

        private JsValue EnumerableOwnPropertyNames(JsValue[] arguments, EnumerableOwnPropertyNamesKind kind)
        {
            var o = arguments.As<ObjectInstance>(0, _engine);
            var ownKeys = o.GetOwnPropertyKeys(Types.String);

            var array = Engine.Array.ConstructFast((uint) ownKeys.Count);
            uint index = 0;

            for (var i = 0; i < ownKeys.Count; i++)
            {
                var property = ownKeys[i];
                var desc = o.GetOwnProperty(property);
                if (desc != PropertyDescriptor.Undefined && desc.Enumerable)
                {
                    if (kind == EnumerableOwnPropertyNamesKind.Key)
                    {
                        array.SetIndexValue(index, property, updateLength: false);
                    }
                    else
                    {
                        var value = o.Get(property, o);
                        if (kind == EnumerableOwnPropertyNamesKind.Value)
                        {
                            array.SetIndexValue(index, value, updateLength: false);
                        }
                        else
                        {
                            array.SetIndexValue(index, _engine.Array.Construct(new[]
                            {
                                property,
                                value
                            }), updateLength: false);
                        }
                    }

                    index++;
                }
            }

            array.SetLength(index);
            return array;
        }

        private enum EnumerableOwnPropertyNamesKind
        {
            Key,
            Value,
            KeyValue
        }
    }
}
