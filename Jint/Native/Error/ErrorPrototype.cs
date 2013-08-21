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

            // Error prototype properties
            obj.DefineOwnProperty("message", new ClrAccessDescriptor<ErrorInstance>(engine, x => x.Message), false);
            obj.DefineOwnProperty("name", new ClrAccessDescriptor<ErrorInstance>(engine, x => x.Name), false);

            // Error prototype functions
            obj.DefineOwnProperty("toString", new ClrDataDescriptor<ErrorInstance, object>(engine, ToErrorString), false);


            return obj;
        }

        private static object ToErrorString(ErrorInstance thisObject, object[] arguments)
        {
            return thisObject.ToErrorString();
        }
    }
}
