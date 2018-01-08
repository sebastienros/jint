using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Interop;

namespace Jint.Native.String
{
    public sealed class StringConstructor : FunctionInstance, IConstructor
    {
        public StringConstructor(Engine engine)
            : base(engine, null, null, false)
        {
        }

        public static StringConstructor CreateStringConstructor(Engine engine)
        {
            var obj = new StringConstructor(engine);
            obj.Extensible = true;

            // The value of the [[Prototype]] internal property of the String constructor is the Function prototype object
            obj.Prototype = engine.Function.PrototypeObject;
            obj.PrototypeObject = StringPrototype.CreatePrototypeObject(engine, obj);

            obj.SetOwnProperty("length", new AllForbiddenPropertyDescriptor(1));

            // The initial value of String.prototype is the String prototype object
            obj.SetOwnProperty("prototype", new AllForbiddenPropertyDescriptor(obj.PrototypeObject));

            return obj;
        }

        public void Configure()
        {
            SetOwnProperty("fromCharCode", new NonEnumerablePropertyDescriptor(new ClrFunctionInstance(Engine, FromCharCode, 1)));
        }

        private static JsValue FromCharCode(JsValue thisObj, JsValue[] arguments)
        {
            var chars = new char[arguments.Length];
            for (var i = 0; i < chars.Length; i++ )
            {
                chars[i] = (char)TypeConverter.ToUint16(arguments[i]);
            }

            return new System.String(chars);
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            if (arguments.Length == 0)
            {
                return "";
            }

            return TypeConverter.ToString(arguments[0]);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.7.2.1
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public ObjectInstance Construct(JsValue[] arguments)
        {
            return Construct(arguments.Length > 0 ? TypeConverter.ToString(arguments[0]) : "");
        }

        public StringPrototype PrototypeObject { get; private set; }

        public StringInstance Construct(string value)
        {
            var instance = new StringInstance(Engine);
            instance.Prototype = PrototypeObject;
            instance.PrimitiveValue = value;
            instance.Extensible = true;

            instance.SetOwnProperty("length", new AllForbiddenPropertyDescriptor(value.Length));

            return instance;
        }
    }
}
