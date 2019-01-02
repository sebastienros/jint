using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Number
{
    public sealed class NumberConstructor : FunctionInstance, IConstructor
    {
        private const long MinSafeInteger = -9007199254740991;
        internal const long MaxSafeInteger = 9007199254740991;

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
            SetOwnProperty("EPSILON", new PropertyDescriptor(JsNumber.JavaScriptEpsilon, PropertyFlag.AllForbidden));
            SetOwnProperty("MIN_SAFE_INTEGER", new PropertyDescriptor(MinSafeInteger, PropertyFlag.AllForbidden));
            SetOwnProperty("MAX_SAFE_INTEGER", new PropertyDescriptor(MaxSafeInteger, PropertyFlag.AllForbidden));

            FastAddProperty("isFinite", new ClrFunctionInstance(Engine, "isFinite", IsFinite, 1, PropertyFlag.Configurable), true, false, true);
            FastAddProperty("isInteger", new ClrFunctionInstance(Engine, "isInteger", IsInteger, 1, PropertyFlag.Configurable), true, false, true);
            FastAddProperty("isNaN", new ClrFunctionInstance(Engine, "isNaN", IsNaN, 1, PropertyFlag.Configurable), true, false, true);
            FastAddProperty("isSafeInteger", new ClrFunctionInstance(Engine, "isSafeInteger", IsSafeInteger, 1, PropertyFlag.Configurable), true, false, true);

            FastAddProperty("parseFloat", new ClrFunctionInstance(Engine, "parseFloat", _engine.Global.ParseFloat, 0, PropertyFlag.Configurable), true, false, true);
            FastAddProperty("parseInt", new ClrFunctionInstance(Engine, "parseInt", _engine.Global.ParseInt, 0, PropertyFlag.Configurable), true, false, true);
        }

        private JsValue IsFinite(JsValue thisObj, JsValue[] arguments)
        {
            if (!(arguments.At(0) is JsNumber num))
            {
                return false;
            }

            return double.IsInfinity(num._value) || double.IsNaN(num._value) ? JsBoolean.False : JsBoolean.True;
        }

        private JsValue IsInteger(JsValue thisObj, JsValue[] arguments)
        {
            if (!(arguments.At(0) is JsNumber num))
            {
                return false;
            }

            if (double.IsInfinity(num._value) || double.IsNaN(num._value))
            {
                return JsBoolean.False;
            }

            var integer = TypeConverter.ToInteger(num);

            return integer == num._value;
        }

        private JsValue IsNaN(JsValue thisObj, JsValue[] arguments)
        {
            if (!(arguments.At(0) is JsNumber num))
            {
                return false;
            }

            return double.IsNaN(num._value);
        }

        private JsValue IsSafeInteger(JsValue thisObj, JsValue[] arguments)
        {
            if (!(arguments.At(0) is JsNumber num))
            {
                return false;
            }

            if (double.IsInfinity(num._value) || double.IsNaN(num._value))
            {
                return JsBoolean.False;
            }

            var integer = TypeConverter.ToInteger(num);

            if (integer != num._value)
            {
                return false;
            }

            return System.Math.Abs(integer) <= MaxSafeInteger;
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
