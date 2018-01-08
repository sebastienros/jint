using System;
using System.Collections;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors.Specialized;
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

            obj.SetOwnProperty("length", new AllForbiddenPropertyDescriptor(1));

            // The initial value of Array.prototype is the Array prototype object
            obj.SetOwnProperty("prototype", new AllForbiddenPropertyDescriptor(obj.PrototypeObject));

            return obj;
        }

        public void Configure()
        {
            SetOwnProperty("isArray", new NonEnumerablePropertyDescriptor(new ClrFunctionInstance(Engine, IsArray, 1)));
        }

        private static JsValue IsArray(JsValue thisObj, JsValue[] arguments)
        {
            if (arguments.Length == 0)
            {
                return false;
            }

            var o = arguments.At(0);

            return o.IsObject() && o.AsObject().Class == "Array";
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return Construct(arguments);
        }

        public ObjectInstance Construct(JsValue[] arguments)
        {
            // check if we can figure out good size
            var capacity = arguments.Length > 0 ? (uint) arguments.Length : 0;
            if (arguments.Length == 1 && arguments[0].Type == Types.Number)
            {
                var number = arguments[0].AsNumber();
                if (number > 0)
                {
                    capacity = (uint) number;
                }
            }
            return Construct(arguments, capacity);
        }

        public ArrayInstance Construct(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentException("invalid array length", nameof(capacity));
            }
            return Construct(System.Array.Empty<JsValue>(), (uint) capacity);
        }

        public ArrayInstance Construct(uint capacity)
        {
            return Construct(System.Array.Empty<JsValue>(), capacity);
        }

        public ArrayInstance Construct(JsValue[] arguments, uint capacity)
        {
            var instance = new ArrayInstance(Engine, capacity);
            instance.Prototype = PrototypeObject;
            instance.Extensible = true;

            if (arguments.Length == 1 && arguments.At(0).IsNumber())
            {
                var length = TypeConverter.ToUint32(arguments.At(0));
                if (!TypeConverter.ToNumber(arguments[0]).Equals(length))
                {
                    throw new JavaScriptException(Engine.RangeError, "Invalid array length");
                }

                instance.SetOwnProperty("length", new WritablePropertyDescriptor(length));
            }
            else if (arguments.Length == 1 && arguments.At(0).IsObject() && arguments.At(0).As<ObjectWrapper>() != null )
            {
                if (arguments.At(0).As<ObjectWrapper>().Target is IEnumerable enumerable)
                {
                    var jsArray = (ArrayInstance) Engine.Array.Construct(Arguments.Empty);
                    foreach (var item in enumerable)
                    {
                        var jsItem = JsValue.FromObject(Engine, item);
                        Engine.Array.PrototypeObject.Push(jsArray, Arguments.From(jsItem));
                    }

                    return jsArray;
                }
            }
            else
            {
                instance.SetOwnProperty("length", new WritablePropertyDescriptor(0));
                if (arguments.Length > 0)
                {
                    PrototypeObject.Push(instance, arguments);
                }
            }

            return instance;
        }

    }
}
