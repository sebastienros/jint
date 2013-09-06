using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Native.RegExp
{
    public sealed class RegExpConstructor : FunctionInstance, IConstructor
    {
        public RegExpConstructor(Engine engine)
            : base(engine, null, null, false)
        {
        }

        public static RegExpConstructor CreateRegExpConstructor(Engine engine)
        {
            var obj = new RegExpConstructor(engine);
            obj.Extensible = true;

            // The value of the [[Prototype]] internal property of the RegExp constructor is the Function prototype object 
            obj.Prototype = engine.Function.PrototypeObject;
            obj.PrototypeObject = RegExpPrototype.CreatePrototypeObject(engine, obj);

            obj.FastAddProperty("length", 1, false, false, false);

            // The initial value of RegExp.prototype is the RegExp prototype object
            obj.FastAddProperty("prototype", obj.PrototypeObject, false, false, false);

            return obj;
        }

        public void Configure()
        {
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

        public RegExpPrototype PrototypeObject { get; private set; }

        public RegExpInstance Construct(string value)
        {
            var instance = new RegExpInstance(Engine);
            instance.Prototype = PrototypeObject;
            instance.PrimitiveValue = value;
            instance.Extensible = true;

            return instance;
        }
    }
}
