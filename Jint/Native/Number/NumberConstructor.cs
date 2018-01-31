using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors.Specialized;

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

            obj.SetOwnProperty("length", new AllForbiddenPropertyDescriptor(1));

            // The initial value of Number.prototype is the Number prototype object
            obj.SetOwnProperty("prototype", new AllForbiddenPropertyDescriptor(obj.PrototypeObject));

            return obj;
        }

        public void Configure()
        {
            SetOwnProperty("MAX_VALUE", new AllForbiddenPropertyDescriptor(double.MaxValue));
            SetOwnProperty("MIN_VALUE", new AllForbiddenPropertyDescriptor(double.Epsilon));
            SetOwnProperty("NaN", new AllForbiddenPropertyDescriptor(double.NaN));
            SetOwnProperty("NEGATIVE_INFINITY", new AllForbiddenPropertyDescriptor(double.NegativeInfinity));
            SetOwnProperty("POSITIVE_INFINITY", new AllForbiddenPropertyDescriptor(double.PositiveInfinity));
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
            instance.NumberData = value;
            instance.Extensible = true;

            return instance;
        }
    }
}
