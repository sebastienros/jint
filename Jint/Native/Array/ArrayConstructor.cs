using System;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Native.Array
{
    public sealed class ArrayConstructor : FunctionInstance, IConstructor
    {
        private ArrayConstructor(Engine engine) :  base(engine, null, null, false)
        {
        }

        public ArrayPrototype PrototypeObject { get; private set; }

        public static ArrayConstructor CreateArrayConstructor(Engine engine)
        {
            var obj = new ArrayConstructor(engine);
            obj.Extensible = true;

            // The value of the [[Prototype]] internal property of the Array constructor is the Function prototype object 
            obj.Prototype = engine.Function.PrototypeObject;
            obj.PrototypeObject = ArrayPrototype.CreatePrototypeObject(engine, obj);

            obj.FastAddProperty("length", 1, false, false, false);

            // The initial value of Array.prototype is the Array prototype object
            obj.FastAddProperty("prototype", obj.PrototypeObject, false, false, false);

            return obj;
        }

        public void Configure()
        {
            FastAddProperty("isArray", new ClrFunctionInstance<object, object>(Engine, IsArray, 1), true, false, true);
        }

        private object IsArray(object thisObj, object[] arguments)
        {
            if (arguments.Length == 0)
            {
                return false;
            }

            var o = arguments[0] as ObjectInstance;
            return o != null && o.Class == "Array";
        }

        public override object Call(object thisObject, object[] arguments)
        {
            return Construct(arguments);
        }

        public ObjectInstance Construct(object[] arguments)
        {
            var instance = new ArrayInstance(Engine);
            instance.Prototype = PrototypeObject;
            instance.Extensible = true;

            if (arguments.Length == 1 && TypeConverter.GetType(arguments[0]) == Types.Number)
            {
                var length = TypeConverter.ToUint32(arguments[0]);
                if (TypeConverter.ToNumber(arguments[0]) != length)
                {
                    throw new JavaScriptException(Engine.RangeError);
                }
                
                instance.FastAddProperty("length", length, true, false, false);
            }
            else
            {
                instance.FastAddProperty("length", 0, true, false, false);
                PrototypeObject.Push(instance, arguments);
            }

            return instance;
        }

    }
}
