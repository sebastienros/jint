using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
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

            obj.FastAddProperty("length", 1, false, false, false);

            // The initial value of String.prototype is the String prototype object
            obj.FastAddProperty("prototype", obj.PrototypeObject, false, false, false);

            return obj;
        }

        public void Configure()
        {
            FastAddProperty("fromCharCode", new ClrFunctionInstance<object, object>(Engine, FromCharCode), false, false, false);
        }

        private object FromCharCode(object thisObj, object[] arguments)
        {
            throw new System.NotImplementedException();
        }

        public override object Call(object thisObject, object[] arguments)
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
        public ObjectInstance Construct(object[] arguments)
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

            return instance;
        }
    }
}
