using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Error
{
    public class ErrorConstructor : FunctionInstance, IConstructor
    {
        public ErrorConstructor(Engine engine) : base(engine, null, null, false)
        {
        }

        public static ErrorConstructor CreateErrorConstructor(Engine engine, string name)
        {
            var obj = new ErrorConstructor(engine);
            obj.Extensible = true;

            // The value of the [[Prototype]] internal property of the Error constructor is the Function prototype object (15.11.3)
            obj.Prototype = engine.Function.PrototypeObject;
            obj.PrototypeObject = ErrorPrototype.CreatePrototypeObject(engine, obj, name);

            obj.FastAddProperty("length", 1, false, false, false);

            // The initial value of Error.prototype is the Error prototype object
            obj.FastAddProperty("prototype", obj.PrototypeObject, false, false, false);

            return obj;
        }

        public void Configure()
        {
            
        }

        public override object Call(object thisObject, object[] arguments)
        {
            return Construct(arguments);
        }

        public ObjectInstance Construct(object[] arguments)
        {
            var instance = new ErrorInstance(Engine, null);
            instance.Prototype = PrototypeObject;
            instance.Extensible = true;

            if (arguments.At(0) != Undefined.Instance)
            {
                instance.Put("message", TypeConverter.ToString(arguments.At(0)), false);
            }

            return instance;
        }

        public ErrorPrototype PrototypeObject { get; private set; }
    }
}
