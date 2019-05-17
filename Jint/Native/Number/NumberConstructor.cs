using Jint.Collections;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Number
{
    public sealed class NumberConstructor : FunctionInstance, IConstructor
    {
        private static readonly JsString _functionName = new JsString("Number");

        private const long MinSafeInteger = -9007199254740991;
        internal const long MaxSafeInteger = 9007199254740991;

        public NumberConstructor(Engine engine)
            : base(engine, _functionName, strict: false)
        {

        }

        public static NumberConstructor CreateNumberConstructor(Engine engine)
        {
            var obj = new NumberConstructor(engine)
            {
                Extensible = true,
                Prototype = engine.Function.PrototypeObject
            };

            // The value of the [[Prototype]] internal property of the Number constructor is the Function prototype object
            obj.PrototypeObject = NumberPrototype.CreatePrototypeObject(engine, obj);

            obj._length = new PropertyDescriptor(1, PropertyFlag.AllForbidden);

            // The initial value of Number.prototype is the Number prototype object
            obj._prototype = new PropertyDescriptor(obj.PrototypeObject, PropertyFlag.AllForbidden);

            return obj;
        }

        protected override void Initialize()
        {
            _properties = new StringDictionarySlim<PropertyDescriptor>(15)
            {
                ["MAX_VALUE"] = new PropertyDescriptor(new PropertyDescriptor(double.MaxValue, PropertyFlag.AllForbidden)),
                ["MIN_VALUE"] = new PropertyDescriptor(new PropertyDescriptor(double.Epsilon, PropertyFlag.AllForbidden)),
                ["NaN"] = new PropertyDescriptor(new PropertyDescriptor(double.NaN, PropertyFlag.AllForbidden)),
                ["NEGATIVE_INFINITY"] = new PropertyDescriptor(new PropertyDescriptor(double.NegativeInfinity, PropertyFlag.AllForbidden)),
                ["POSITIVE_INFINITY"] = new PropertyDescriptor(new PropertyDescriptor(double.PositiveInfinity, PropertyFlag.AllForbidden)),
                ["EPSILON"] = new PropertyDescriptor(new PropertyDescriptor(JsNumber.JavaScriptEpsilon, PropertyFlag.AllForbidden)),
                ["MIN_SAFE_INTEGER"] = new PropertyDescriptor(new PropertyDescriptor(MinSafeInteger, PropertyFlag.AllForbidden)),
                ["MAX_SAFE_INTEGER"] = new PropertyDescriptor(new PropertyDescriptor(MaxSafeInteger, PropertyFlag.AllForbidden)),
                ["isFinite"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "isFinite", IsFinite, 1, PropertyFlag.Configurable), true, false, true),
                ["isInteger"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "isInteger", IsInteger, 1, PropertyFlag.Configurable), true, false, true),
                ["isNaN"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "isNaN", IsNaN, 1, PropertyFlag.Configurable), true, false, true),
                ["isSafeInteger"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "isSafeInteger", IsSafeInteger, 1, PropertyFlag.Configurable), true, false, true),
                ["parseFloat"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "parseFloat", _engine.Global.ParseFloat, 0, PropertyFlag.Configurable), true, false, true),
                ["parseInt"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "parseInt", _engine.Global.ParseInt, 0, PropertyFlag.Configurable), true, false, true)
            };
        }

        private static JsValue IsFinite(JsValue thisObj, JsValue[] arguments)
        {
            if (!(arguments.At(0) is JsNumber num))
            {
                return false;
            }

            return double.IsInfinity(num._value) || double.IsNaN(num._value) ? JsBoolean.False : JsBoolean.True;
        }

        private static JsValue IsInteger(JsValue thisObj, JsValue[] arguments)
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

        private static JsValue IsNaN(JsValue thisObj, JsValue[] arguments)
        {
            if (!(arguments.At(0) is JsNumber num))
            {
                return false;
            }

            return double.IsNaN(num._value);
        }

        private static JsValue IsSafeInteger(JsValue thisObj, JsValue[] arguments)
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
