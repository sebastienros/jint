using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Number
{
    public sealed class NumberConstructor : FunctionInstance, IConstructor
    {
        public NumberConstructor(Engine engine)
            : base(engine, "Number", null, null, false)
        {

        }

        public static NumberConstructor CreateNumberConstructor(Engine engine)
        {
            var obj = new NumberConstructor(engine);
            obj.Extensible = true;

            // The value of the [[Prototype]] internal property of the Number constructor is the Function prototype object
            obj.Prototype = engine.Function.PrototypeObject;
            obj.PrototypeObject = NumberPrototype.CreatePrototypeObject(engine, obj);

            obj.SetOwnProperty("length", new PropertyDescriptor(1, PropertyFlag.AllForbidden));

            // The initial value of Number.prototype is the Number prototype object
            obj.SetOwnProperty("prototype", new PropertyDescriptor(obj.PrototypeObject, PropertyFlag.AllForbidden));

            return obj;
        }

        public void Configure()
        {
            SetOwnProperty("MAX_VALUE", new PropertyDescriptor(double.MaxValue, PropertyFlag.AllForbidden));
            SetOwnProperty("MIN_VALUE", new PropertyDescriptor(double.Epsilon, PropertyFlag.AllForbidden));
            SetOwnProperty("NaN", new PropertyDescriptor(double.NaN, PropertyFlag.AllForbidden));
            SetOwnProperty("NEGATIVE_INFINITY", new PropertyDescriptor(double.NegativeInfinity, PropertyFlag.AllForbidden));
            SetOwnProperty("POSITIVE_INFINITY", new PropertyDescriptor(double.PositiveInfinity, PropertyFlag.AllForbidden));
            SetOwnProperty("EPSILON", new PropertyDescriptor(double.Epsilon, PropertyFlag.AllForbidden));
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
            return Construct(JsNumber.Create(value));
        }

        public NumberInstance Construct(JsNumber value)
        {
            var instance = new NumberInstance(Engine)
            {
                Prototype = PrototypeObject,
                NumberData = value,
                Extensible = true
            };

            return instance;
        }
    }
}
