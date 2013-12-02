using System;
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

            FastAddProperty("getPrototypeOf", new ClrFunctionInstance<object, object>(Engine, GetPrototypeOf, 1), true, false, true);
            FastAddProperty("getOwnPropertyDescriptor", new ClrFunctionInstance<object, object>(Engine, GetOwnPropertyDescriptor, 2), true, false, true);
            FastAddProperty("getOwnPropertyNames", new ClrFunctionInstance<object, object>(Engine, GetOwnPropertyNames, 1), true, false, true);
            FastAddProperty("create", new ClrFunctionInstance<object, object>(Engine, Create, 2), true, false, true);
            FastAddProperty("defineProperty", new ClrFunctionInstance<object, object>(Engine, DefineProperty), true, false, true);
            FastAddProperty("defineProperties", new ClrFunctionInstance<object, object>(Engine, DefineProperties), true, false, true);
            FastAddProperty("seal", new ClrFunctionInstance<object, object>(Engine, Seal, 1), true, false, true);
            FastAddProperty("freeze", new ClrFunctionInstance<object, object>(Engine, Freeze, 1), true, false, true);
            FastAddProperty("preventExtensions", new ClrFunctionInstance<object, object>(Engine, PreventExtensions, 1), true, false, true);
            FastAddProperty("isSealed", new ClrFunctionInstance<object, object>(Engine, IsSealed, 1), true, false, true);
            FastAddProperty("isFrozen", new ClrFunctionInstance<object, object>(Engine, IsFrozen, 1), true, false, true);
            FastAddProperty("isExtensible", new ClrFunctionInstance<object, object>(Engine, IsExtensible, 1), true, false, true);
            FastAddProperty("keys", new ClrFunctionInstance<object, object>(Engine, Keys), true, false, true);
        }

        public ObjectPrototype PrototypeObject { get; private set; }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.2.1.1
        /// </summary>
        /// <param name="thisObject"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public override object Call(object thisObject, object[] arguments)
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
        public ObjectInstance Construct(object[] arguments)
        {
            if (arguments.Length > 0)
            {
                var value = arguments[0];
                var valueObj = value as ObjectInstance;
                if (valueObj != null)
                {
                    return valueObj;
                }
                var type = TypeConverter.GetType(value);
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

        public object GetPrototypeOf(object thisObject, object[] arguments)
        {
            var oArg = arguments.Length > 0 ? arguments[0] : Undefined.Instance;
            if (TypeConverter.GetType(oArg) != Types.Object)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var o = oArg as ObjectInstance;

            return o.Prototype ?? Null.Instance;
        }

        public object GetOwnPropertyDescriptor(object thisObject, object[] arguments)
        {
            var oArg = arguments.Length > 0 ? arguments[0] : Undefined.Instance;
            if (TypeConverter.GetType(oArg) != Types.Object)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var o = oArg as ObjectInstance;

            var p = arguments.Length > 1 ? arguments[1] : Undefined.Instance;
            var name = TypeConverter.ToString(p);

            var desc = o.GetOwnProperty(name);
            return PropertyDescriptor.FromPropertyDescriptor(Engine, desc);
        }

        public object GetOwnPropertyNames(object thisObject, object[] arguments)
        {
            var oArg = arguments.Length > 0 ? arguments[0] : Undefined.Instance;
            if (TypeConverter.GetType(oArg) != Types.Object)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var o = oArg as ObjectInstance;

            var array = Engine.Array.Construct(Arguments.Empty);
            var n = 0;

            var s = o as StringInstance;
            if (s != null)
            {
                for (var i = 0; i < s.PrimitiveValue.Length; i++)
                {
                    array.DefineOwnProperty(n.ToString(), new DataDescriptor(i.ToString()) { Writable = true, Enumerable = true, Configurable = true }, false);
                    n++;
                }  
            }

            foreach (var p in o.Properties)
            {
                array.DefineOwnProperty(n.ToString(), new DataDescriptor(p.Key) { Writable = true, Enumerable = true, Configurable = true }, false);
                n++;
            }

            return array;
        }

        public object Create(object thisObject, object[] arguments)
        {
            var oArg = arguments.At(0);
            if (TypeConverter.GetType(oArg) != Types.Object && oArg != Null.Instance)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var obj = Engine.Object.Construct(Arguments.Empty);
            obj.Prototype = oArg as ObjectInstance;

            var properties = arguments.Length > 1 ? arguments[1] : Undefined.Instance;
            if (properties != Undefined.Instance)
            {
                DefineProperties(thisObject, new [] {obj, properties});
            }

            return obj;
        }

        public object DefineProperty(object thisObject, object[] arguments)
        {
            var o = arguments.At(0);
            if (TypeConverter.GetType(o) != Types.Object)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var p = arguments.At(1);
            var name = TypeConverter.ToString(p);

            var attributes = arguments.At(2);
            var desc = PropertyDescriptor.ToPropertyDescriptor(Engine, attributes);

            ((ObjectInstance)o).DefineOwnProperty(name, desc, true);
            return o;
        }

        public object DefineProperties(object thisObject, object[] arguments)
        {
            var oArg = arguments.Length > 0 ? arguments[0] : Undefined.Instance;
            if (TypeConverter.GetType(oArg) != Types.Object)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var o = oArg as ObjectInstance;

            var properties = arguments.Length > 1 ? arguments[1] : Undefined.Instance;
            var props = TypeConverter.ToObject(Engine, properties);
            var descriptors = new List<KeyValuePair<string, PropertyDescriptor>>();
            foreach (var p in props.Properties)
            {
                if (!p.Value.EnumerableIsSet)
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

        public object Seal(object thisObject, object[] arguments)
        {
            var oArg = arguments.Length > 0 ? arguments[0] : Undefined.Instance;
            if (TypeConverter.GetType(oArg) != Types.Object)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var o = oArg as ObjectInstance;

            foreach (var prop in o.Properties)
            {
                if (prop.Value.ConfigurableIsSetToTrue)
                {
                    prop.Value.Configurable = false;
                }

                o.DefineOwnProperty(prop.Key, prop.Value, true);
            }

            o.Extensible = false;

            return o;
        }

        public object Freeze(object thisObject, object[] arguments)
        {
            var oArg = arguments.Length > 0 ? arguments[0] : Undefined.Instance;
            if (TypeConverter.GetType(oArg) != Types.Object)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var o = (ObjectInstance)oArg;

            var keys = o.Properties.Keys.ToArray();
            foreach (var key in keys)
            {
                var prop = o.Properties[key];
                if (prop.IsDataDescriptor())
                {
                    var datadesc = prop.As<DataDescriptor>();
                    if (datadesc.WritableIsSet)
                    {
                        datadesc.Writable = false;
                    }
                }
                if (prop.ConfigurableIsSetToTrue)
                {
                    prop.Configurable = false;
                }
                o.DefineOwnProperty(key, prop, true);
            }
            
            o.Extensible = false;
         
            return o;
        }

        public object PreventExtensions(object thisObject, object[] arguments)
        {
            var oArg = arguments.Length > 0 ? arguments[0] : Undefined.Instance;
            if (TypeConverter.GetType(oArg) != Types.Object)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var o = oArg as ObjectInstance;

            o.Extensible = false;

            return o;
        }

        public object IsSealed(object thisObject, object[] arguments)
        {
            var oArg = arguments.Length > 0 ? arguments[0] : Undefined.Instance;
            if (TypeConverter.GetType(oArg) != Types.Object)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var o = oArg as ObjectInstance;

            foreach (var prop in o.Properties)
            {
                if (prop.Value.ConfigurableIsSetToTrue)
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

        public object IsFrozen(object thisObject, object[] arguments)
        {
            var oArg = arguments.Length > 0 ? arguments[0] : Undefined.Instance;
            if (TypeConverter.GetType(oArg) != Types.Object)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var o = oArg as ObjectInstance;

            foreach (var prop in o.Properties)
            {
                if (prop.Value.IsDataDescriptor())
                {
                    if (prop.Value.As<DataDescriptor>().WritableIsSet)
                    {
                        return false;
                    }
                }
                if (prop.Value.ConfigurableIsSetToTrue)
                {
                    return false;
                }
            }

            if (o.Extensible)
            {
                return true;
            }

            return false;
        }

        public object IsExtensible(object thisObject, object[] arguments)
        {
            var oArg = arguments.Length > 0 ? arguments[0] : Undefined.Instance;
            if (TypeConverter.GetType(oArg) != Types.Object)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var o = oArg as ObjectInstance;

            return o.Extensible;
        }

        public object Keys(object thisObject, object[] arguments)
        {
            var oArg = arguments.Length > 0 ? arguments[0] : Undefined.Instance;
            if (TypeConverter.GetType(oArg) != Types.Object)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var o = oArg as ObjectInstance;

            var n = o.Properties.Values.Count(x => x.EnumerableIsSet);
            var array = Engine.Array.Construct(new object[] {n});
            var index = 0;
            foreach (var prop in o.Properties.Where(x => x.Value.EnumerableIsSet))
            {
                array.DefineOwnProperty(index.ToString(), new DataDescriptor(prop.Key) { Writable = true, Enumerable = true, Configurable = true }, false);
                index++;
            }
            return array;
        }
    }
}
