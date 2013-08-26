using System;
using Jint.Native.Function;
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
            obj.PrototypeObject = ObjectPrototype.CreatePrototypeObject(engine, obj);

            obj.FastAddProperty("length", 1, false, false, false);
            obj.FastAddProperty("prototype", obj.PrototypeObject, false, false, false);

            return obj;
        }

        public void Configure()
        {
            Prototype = Engine.Function.PrototypeObject;

            FastAddProperty("getPrototypeOf", new ClrFunctionInstance<object, object>(Engine, GetPrototypeOf), false, false, false);
            FastAddProperty("getOwnPropertyDescriptor", new ClrFunctionInstance<object, object>(Engine, GetOwnPropertyDescriptor), false, false, false);
            FastAddProperty("getOwnPropertyNames", new ClrFunctionInstance<object, object>(Engine, GetOwnPropertyNames), false, false, false);
            FastAddProperty("create", new ClrFunctionInstance<object, object>(Engine, Create), false, false, false);
            FastAddProperty("defineProperty", new ClrFunctionInstance<object, object>(Engine, DefineProperty), false, false, false);
            FastAddProperty("defineProperties", new ClrFunctionInstance<object, object>(Engine, DefineProperties), false, false, false);
            FastAddProperty("seal", new ClrFunctionInstance<object, object>(Engine, Seal), false, false, false);
            FastAddProperty("freeze", new ClrFunctionInstance<object, object>(Engine, Freeze), false, false, false);
            FastAddProperty("preventExtensions", new ClrFunctionInstance<object, object>(Engine, PreventExtensions), false, false, false);
            FastAddProperty("isSealed", new ClrFunctionInstance<object, object>(Engine, IsSealed), false, false, false);
            FastAddProperty("isFrozen", new ClrFunctionInstance<object, object>(Engine, IsFrozen), false, false, false);
            FastAddProperty("isExtensible", new ClrFunctionInstance<object, object>(Engine, IsExtensible), false, false, false);
            FastAddProperty("keys", new ClrFunctionInstance<object, object>(Engine, Keys), false, false, false);
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
                if (type == TypeCode.String || type == TypeCode.Double || type == TypeCode.Boolean)
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
            throw new NotImplementedException();
        }

        public object GetOwnPropertyDescriptor(object thisObject, object[] arguments)
        {
            throw new NotImplementedException();
        }

        public object GetOwnPropertyNames(object thisObject, object[] arguments)
        {
            throw new NotImplementedException();
        }

        public object Create(object thisObject, object[] arguments)
        {
            throw new NotImplementedException();
        }

        public object DefineProperty(object thisObject, object[] arguments)
        {
            var o = arguments[0] as ObjectInstance;
            var p = arguments[1];
            var attributes = arguments[2] as ObjectInstance;
            
            if (o == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            if (attributes == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var name = TypeConverter.ToString(p);
            var desc = PropertyDescriptor.ToPropertyDescriptor(Engine, attributes);
            o.DefineOwnProperty(name, desc, true);
            return o;
        }

        public object DefineProperties(object thisObject, object[] arguments)
        {
            throw new NotImplementedException();
        }

        public object Seal(object thisObject, object[] arguments)
        {
            throw new NotImplementedException();
        }

        public object Freeze(object thisObject, object[] arguments)
        {
            throw new NotImplementedException();
        }

        public object PreventExtensions(object thisObject, object[] arguments)
        {
            throw new NotImplementedException();
        }

        public object IsSealed(object thisObject, object[] arguments)
        {
            throw new NotImplementedException();
        }

        public object IsFrozen(object thisObject, object[] arguments)
        {
            throw new NotImplementedException();
        }

        public object IsExtensible(object thisObject, object[] arguments)
        {
            throw new NotImplementedException();
        }

        public object Keys(object thisObject, object[] arguments)
        {
            throw new NotImplementedException();
        }
    }
}
