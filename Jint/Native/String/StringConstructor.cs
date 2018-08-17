using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.String
{
    public sealed class StringConstructor : FunctionInstance, IConstructor
    {
        public StringConstructor(Engine engine)
            : base(engine, "String", null, null, false)
        {
        }

        public static StringConstructor CreateStringConstructor(Engine engine)
        {
            var obj = new StringConstructor(engine);
            obj.Extensible = true;

            // The value of the [[Prototype]] internal property of the String constructor is the Function prototype object
            obj.Prototype = engine.Function.PrototypeObject;
            obj.PrototypeObject = StringPrototype.CreatePrototypeObject(engine, obj);

            obj.SetOwnProperty("length", new PropertyDescriptor(1, PropertyFlag.AllForbidden));

            // The initial value of String.prototype is the String prototype object
            obj.SetOwnProperty("prototype", new PropertyDescriptor(obj.PrototypeObject, PropertyFlag.AllForbidden));

            return obj;
        }

        public void Configure()
        {
            SetOwnProperty("fromCharCode", new PropertyDescriptor(new ClrFunctionInstance(Engine, "fromCharCode", FromCharCode, 1), PropertyFlag.NonEnumerable));
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
            return Construct(JsString.Create(value));
        }

        public StringInstance Construct(JsString value)
        {
            var instance = new StringInstance(Engine)
            {
                Prototype = PrototypeObject,
                PrimitiveValue = value,
                Extensible = true,
                _length = new PropertyDescriptor(value.Length, PropertyFlag.AllForbidden)
            };

            return instance;
        }
    }
}
