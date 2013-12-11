using Jint.Native.Array;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Number
{
    public sealed class NumberConstructor : FunctionInstance, IConstructor
    {
        public NumberConstructor(Engine engine)
            : base(engine, null, null, false)
        {
            
        }

        public static NumberConstructor CreateNumberConstructor(Engine engine)
        {
            var obj = new NumberConstructor(engine);
            obj.Extensible = true;

            // The value of the [[Prototype]] internal property of the Number constructor is the Function prototype object 
            obj.Prototype = engine.Function.PrototypeObject;
            obj.PrototypeObject = NumberPrototype.CreatePrototypeObject(engine, obj);

            obj.FastAddProperty("length", 1, false, false, false);

            // The initial value of Number.prototype is the Number prototype object
            obj.FastAddProperty("prototype", obj.PrototypeObject, false, false, false);

            return obj;
        }

        public void Configure()
        {
            FastAddProperty("MAX_VALUE", double.MaxValue, false, false, false);
            FastAddProperty("MIN_VALUE", double.Epsilon, false, false, false);
            FastAddProperty("NaN", double.NaN, false, false, false);
            FastAddProperty("NEGATIVE_INFINITY", double.NegativeInfinity, false, false, false);
            FastAddProperty("POSITIVE_INFINITY", double.PositiveInfinity, false, false, false);
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            if (arguments.Length == 0)
            {
                return 0d;
            }

            return TypeConverter.ToNumber(arguments[0]);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.7.2.1
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public ObjectInstance Construct(JsValue[] arguments)
        {
            return Construct(arguments.Length > 0 ? TypeConverter.ToNumber(arguments[0]) : 0);
        }

        public NumberPrototype PrototypeObject { get; private set; }

        public NumberInstance Construct(double value)
        {
            var instance = new NumberInstance(Engine);
            instance.Prototype = PrototypeObject;
            instance.PrimitiveValue = value;
            instance.Extensible = true;

            return instance;
        }


    }
}
