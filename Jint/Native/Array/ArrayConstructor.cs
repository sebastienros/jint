using System.Collections;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
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

            obj.SetOwnProperty("length", new PropertyDescriptor(1, PropertyFlag.AllForbidden));

            // The initial value of Array.prototype is the Array prototype object
            obj.SetOwnProperty("prototype", new PropertyDescriptor(obj.PrototypeObject, PropertyFlag.AllForbidden));

            return obj;
        }

        public void Configure()
        {
            SetOwnProperty("isArray", new PropertyDescriptor(new ClrFunctionInstance(Engine, IsArray, 1), PropertyFlag.NonEnumerable));
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
                var number = ((JsNumber) arguments[0])._value;
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
                ExceptionHelper.ThrowArgumentException("invalid array length", nameof(capacity));
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
                    ExceptionHelper.ThrowRangeError(_engine, "Invalid array length");
                }

                instance.SetOwnProperty("length", new PropertyDescriptor(length, PropertyFlag.OnlyWritable));
            }
            else if (arguments.Length == 1 && arguments[0] is ObjectWrapper objectWrapper)
            {
                if (objectWrapper.Target is IEnumerable enumerable)
                {
                    var jsArray = (ArrayInstance) Engine.Array.Construct(Arguments.Empty);
                    var tempArray = Engine.JsValueArrayPool.RentArray(1);
                    foreach (var item in enumerable)
                    {
                        var jsItem = FromObject(Engine, item);
                        tempArray[0] = jsItem;
                        Engine.Array.PrototypeObject.Push(jsArray, tempArray);
                    }
                    Engine.JsValueArrayPool.ReturnArray(tempArray);
                    return jsArray;
                }
            }
            else
            {
                instance.SetOwnProperty("length", new PropertyDescriptor(0, PropertyFlag.OnlyWritable));
                if (arguments.Length > 0)
                {
                    PrototypeObject.Push(instance, arguments);
                }
            }

            return instance;
        }

        internal ArrayInstance ConstructFast(uint length)
        {
            var instance = new ArrayInstance(Engine, length);
            instance.Prototype = PrototypeObject;
            instance.Extensible = true;
            instance.SetOwnProperty("length", new PropertyDescriptor(length, PropertyFlag.OnlyWritable));
            return instance;
        }
    }
}
