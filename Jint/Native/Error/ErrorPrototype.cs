using Jint.Native.Object;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Native.Error
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.11.4
    /// </summary>
    public sealed class ErrorPrototype : ObjectInstance
    {
        private ErrorPrototype(Engine engine)
            : base(engine)
        {
        }

        public static ErrorPrototype CreatePrototypeObject(Engine engine, ErrorConstructor errorConstructor, string name)
        {
            var obj = new ErrorPrototype(engine) { Extensible = true };

            obj.FastAddProperty("constructor", errorConstructor, false, false, false);


            return obj;
        }

        public void Configure()
        {
            // Error prototype properties
            DefineOwnProperty("message", new ClrAccessDescriptor<ErrorInstance>(Engine, x => x.Message), false);
            DefineOwnProperty("name", new ClrAccessDescriptor<ErrorInstance>(Engine, x => x.Name), false);

            // Error prototype functions
            DefineOwnProperty("toString", new ClrDataDescriptor<ErrorInstance, object>(Engine, ToErrorString), false);
        }

        private static object ToErrorString(ErrorInstance thisObject, object[] arguments)
        {
            return thisObject.ToErrorString();
        }
    }
}
