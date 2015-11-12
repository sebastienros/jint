using System.Collections.Generic;
using System.Linq;
using Jint.Native.Function;
using Jint.Native.String;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Object
{
    public sealed class ObjectConstructor : FunctionInstance, IConstructor
    {
        private readonly Engine _engine;

        private ObjectConstructor(Engine engine) : base(engine, null, null, false)
        {
            _engine = engine;
        }

        public static ObjectConstructor CreateObjectConstructor(Engine engine)
        {
            var obj = new ObjectConstructor(engine);
            obj.Extensible = true;

            obj.PrototypeObject = ObjectPrototype.CreatePrototypeObject(engine, obj);

            obj.FastAddProperty("length", 1, false, false, false);
            obj.FastAddProperty("prototype", obj.PrototypeObject, false, false, false);

            return obj;
        }

        public void Configure()
        {
            Prototype = Engine.Function.PrototypeObject;

            FastAddProperty("getPrototypeOf", new ClrFunctionInstance(Engine, GetPrototypeOf, 1), true, false, true);
            FastAddProperty("getOwnPropertyDescriptor", new ClrFunctionInstance(Engine, GetOwnPropertyDescriptor, 2), true, false, true);
            FastAddProperty("getOwnPropertyNames", new ClrFunctionInstance(Engine, GetOwnPropertyNames, 1), true, false, true);
            FastAddProperty("create", new ClrFunctionInstance(Engine, Create, 2), true, false, true);
            FastAddProperty("defineProperty", new ClrFunctionInstance(Engine, DefineProperty, 3), true, false, true);
            FastAddProperty("defineProperties", new ClrFunctionInstance(Engine, DefineProperties, 2), true, false, true);
            FastAddProperty("seal", new ClrFunctionInstance(Engine, Seal, 1), true, false, true);
            FastAddProperty("freeze", new ClrFunctionInstance(Engine, Freeze, 1), true, false, true);
            FastAddProperty("preventExtensions", new ClrFunctionInstance(Engine, PreventExtensions, 1), true, false, true);
            FastAddProperty("isSealed", new ClrFunctionInstance(Engine, IsSealed, 1), true, false, true);
            FastAddProperty("isFrozen", new ClrFunctionInstance(Engine, IsFrozen, 1), true, false, true);
            FastAddProperty("isExtensible", new ClrFunctionInstance(Engine, IsExtensible, 1), true, false, true);
            FastAddProperty("keys", new ClrFunctionInstance(Engine, Keys, 1), true, false, true);
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
            
            if(arguments[0] == Null.Instance || arguments[0] == Undefined.Instance)
            {
                return Construct(arguments);
            }

            return TypeConverter.ToObject(_engine, arguments[0]);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.2.2.1
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public ObjectInstance Construct(JsValue[] arguments)
        {
            if (arguments.Length > 0)
            {
                var value = arguments[0];
                var valueObj = value.TryCast<ObjectInstance>();
                if (valueObj != null)
                {
                    return valueObj;
                }
                var type = value.Type;
                if (type == Types.String || type == Types.Number || type == Types.Boolean)
                {
                    return TypeConverter.ToObject(_engine, value);
                }
            }

            var obj = new ObjectInstance(_engine)
                {
                    Extensible = true,
                    Prototype = Engine.Object.PrototypeObject
                };

            return obj;
        }

        public JsValue GetPrototypeOf(JsValue thisObject, JsValue[] arguments)
        {
            var oArg = arguments.At(0);
            var o = oArg.TryCast<ObjectInstance>();
            if (o == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }
            
            return o.Prototype ?? Null.Instance;
        }

        public JsValue GetOwnPropertyDescriptor(JsValue thisObject, JsValue[] arguments)
        {
            var oArg = arguments.At(0);
            var o = oArg.TryCast<ObjectInstance>();
            if (o == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var p = arguments.At(1);
            var name = TypeConverter.ToString(p);

            var desc = o.GetOwnProperty(name);
            return PropertyDescriptor.FromPropertyDescriptor(Engine, desc);
        }

        public JsValue GetOwnPropertyNames(JsValue thisObject, JsValue[] arguments)
        {
            var oArg = arguments.At(0);
            var o = oArg.TryCast<ObjectInstance>();
            if (o == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var array = Engine.Array.Construct(Arguments.Empty);
            var n = 0;

            var s = o as StringInstance;
            if (s != null)
            {
                for (var i = 0; i < s.PrimitiveValue.AsString().Length; i++)
                {
                    array.DefineOwnProperty(n.ToString(), new PropertyDescriptor(i.ToString(), true, true, true), false);
                    n++;
                }  
            }

            foreach (var p in o.GetOwnProperties())
            {
                array.DefineOwnProperty(n.ToString(), new PropertyDescriptor(p.Key, true, true, true), false);
                n++;
            }

            return array;
        }

        public JsValue Create(JsValue thisObject, JsValue[] arguments)
        {
            var oArg = arguments.At(0);

            var o = oArg.TryCast<ObjectInstance>();
            if (o == null && oArg != Null.Instance)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var obj = Engine.Object.Construct(Arguments.Empty);
            obj.Prototype = o;

            var properties = arguments.At(1);
            if (properties != Undefined.Instance)
            {
                DefineProperties(thisObject, new [] {obj, properties});
            }

            return obj;
        }

        public JsValue DefineProperty(JsValue thisObject, JsValue[] arguments)
        {
            var oArg = arguments.At(0);
            var o = oArg.TryCast<ObjectInstance>();
            if (o == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var p = arguments.At(1);
            var name = TypeConverter.ToString(p);

            var attributes = arguments.At(2);
            var desc = PropertyDescriptor.ToPropertyDescriptor(Engine, attributes);

            o.DefineOwnProperty(name, desc, true);
            return o;
        }

        public JsValue DefineProperties(JsValue thisObject, JsValue[] arguments)
        {
            var oArg = arguments.At(0);
            var o = oArg.TryCast<ObjectInstance>();
            if (o == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var properties = arguments.At(1);
            var props = TypeConverter.ToObject(Engine, properties);
            var descriptors = new List<KeyValuePair<string, PropertyDescriptor>>();
            foreach (var p in props.GetOwnProperties())
            {
                if (!p.Value.Enumerable.HasValue || !p.Value.Enumerable.Value)
                {
                    continue;
                }

                var descObj = props.Get(p.Key);
                var desc = PropertyDescriptor.ToPropertyDescriptor(Engine, descObj);
                descriptors.Add(new KeyValuePair<string, PropertyDescriptor>(p.Key, desc));
            }
            foreach (var pair in descriptors)
            {
                o.DefineOwnProperty(pair.Key, pair.Value, true);
            }

            return o;
        }

        public JsValue Seal(JsValue thisObject, JsValue[] arguments)
        {
            var oArg = arguments.At(0);
            var o = oArg.TryCast<ObjectInstance>();
            if (o == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            foreach (var prop in o.GetOwnProperties())
            {
                if (prop.Value.Configurable.HasValue && prop.Value.Configurable.Value)
                {
                    prop.Value.Configurable = false;
                }

                o.DefineOwnProperty(prop.Key, prop.Value, true);
            }

            o.Extensible = false;

            return o;
        }

        public JsValue Freeze(JsValue thisObject, JsValue[] arguments)
        {
            var oArg = arguments.At(0);
            var o = oArg.TryCast<ObjectInstance>();
            if (o == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var keys = o.GetOwnProperties().Select(x => x.Key);
            foreach (var p in keys)
            {
                var desc = o.GetOwnProperty(p);
                if (desc.IsDataDescriptor())
                {
                    if (desc.Writable.HasValue && desc.Writable.Value)
                    {
                        desc.Writable = false;
                    }
                }
                if (desc.Configurable.HasValue && desc.Configurable.Value)
                {
                    desc.Configurable = false;
                }
                o.DefineOwnProperty(p, desc, true);
            }
            
            o.Extensible = false;
         
            return o;
        }

        public JsValue PreventExtensions(JsValue thisObject, JsValue[] arguments)
        {
            var oArg = arguments.At(0);
            var o = oArg.TryCast<ObjectInstance>();
            if (o == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            o.Extensible = false;

            return o;
        }

        public JsValue IsSealed(JsValue thisObject, JsValue[] arguments)
        {
            var oArg = arguments.At(0);
            var o = oArg.TryCast<ObjectInstance>();
            if (o == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            foreach (var prop in o.GetOwnProperties())
            {
                if (prop.Value.Configurable.Value == true)
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
            var oArg = arguments.At(0);
            var o = oArg.TryCast<ObjectInstance>();
            if (o == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            foreach (var p in o.GetOwnProperties().Select(x => x.Key))
            {
                var desc = o.GetOwnProperty(p);
                if (desc.IsDataDescriptor())
                {
                    if (desc.Writable.HasValue && desc.Writable.Value)
                    {
                        return false;
                    }
                }
                if (desc.Configurable.HasValue && desc.Configurable.Value)
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
            var oArg = arguments.At(0);
            var o = oArg.TryCast<ObjectInstance>();
            if (o == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            return o.Extensible;
        }

        public JsValue Keys(JsValue thisObject, JsValue[] arguments)
        {
            var oArg = arguments.At(0);
            var o = oArg.TryCast<ObjectInstance>();
            if (o == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var enumerableProperties = o.GetOwnProperties()
                .Where(x => x.Value.Enumerable.HasValue && x.Value.Enumerable.Value)
                .ToArray();
            var n = enumerableProperties.Length;
            var array = Engine.Array.Construct(new JsValue[] {n});
            var index = 0;
            foreach (var prop in enumerableProperties)
            {
                var p = prop.Key;
                array.DefineOwnProperty(
                    TypeConverter.ToString(index), 
                    new PropertyDescriptor(p, true, true, true), 
                    false);
                index++;
            }
            return array;
        }
    }
}
