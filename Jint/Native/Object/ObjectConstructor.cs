using System;
using Jint.Native.Function;
using Jint.Runtime;
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
            // obj.Prototype = engine.Function.PrototypeObject;
            obj.PrototypeObject = ObjectPrototype.CreatePrototypeObject(engine, obj);

            obj.FastAddProperty("length", 1, false, false, false);
            obj.FastAddProperty("prototype", obj.PrototypeObject, false, false, false);

            obj.FastAddProperty("getPrototypeOf", new ClrFunctionInstance<object, object>(engine, obj.GetPrototypeOf), false, false, false);
            obj.FastAddProperty("getOwnPropertyDescriptor", new ClrFunctionInstance<object, object>(engine, obj.GetOwnPropertyDescriptor), false, false, false);
            obj.FastAddProperty("getOwnPropertyNames", new ClrFunctionInstance<object, object>(engine, obj.GetOwnPropertyNames), false, false, false);
            obj.FastAddProperty("create", new ClrFunctionInstance<object, object>(engine, obj.Create), false, false, false);
            obj.FastAddProperty("defineProperty", new ClrFunctionInstance<object, object>(engine, obj.DefineProperty), false, false, false);
            obj.FastAddProperty("defineProperties", new ClrFunctionInstance<object, object>(engine, obj.DefineProperties), false, false, false);
            obj.FastAddProperty("seal", new ClrFunctionInstance<object, object>(engine, obj.Seal), false, false, false);
            obj.FastAddProperty("freeze", new ClrFunctionInstance<object, object>(engine, obj.Freeze), false, false, false);
            obj.FastAddProperty("preventExtensions", new ClrFunctionInstance<object, object>(engine, obj.PreventExtensions), false, false, false);
            obj.FastAddProperty("isSealed", new ClrFunctionInstance<object, object>(engine, obj.IsSealed), false, false, false);
            obj.FastAddProperty("isFrozen", new ClrFunctionInstance<object, object>(engine, obj.IsFrozen), false, false, false);
            obj.FastAddProperty("isExtensible", new ClrFunctionInstance<object, object>(engine, obj.IsExtensible), false, false, false);
            obj.FastAddProperty("keys", new ClrFunctionInstance<object, object>(engine, obj.Keys), false, false, false);

            return obj;
        }

        public ObjectInstance PrototypeObject { get; private set; }

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
            throw new NotImplementedException();
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
