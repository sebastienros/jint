using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Native.Global
{
    public sealed class GlobalObject : ObjectInstance
    {
        private GlobalObject(ObjectInstance prototype)
            : base(prototype)
        {
        }

        public static GlobalObject CreateGlobalObject(Engine engine, ObjectInstance prototype)
        {
            var global = new GlobalObject(prototype);
            global.DefineOwnProperty("isNaN", new ClrDataDescriptor<object, bool>(engine, IsNaN), false);

            return global;
        }

        private static bool IsNaN(object thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            return double.IsNaN(x);
        }

    }
}
