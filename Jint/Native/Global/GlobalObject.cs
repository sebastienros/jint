using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Native.Global
{
    public sealed class GlobalObject : ObjectInstance
    {
        private readonly Engine _engine;

        private GlobalObject(Engine engine, ObjectInstance prototype)
            : base(engine, prototype)
        {
            _engine = engine;
        }

        public static GlobalObject CreateGlobalObject(Engine engine, ObjectInstance prototype)
        {
            var global = new GlobalObject(engine, prototype);
            
            // Global object properties
            global.DefineOwnProperty("NaN", new DataDescriptor(double.NaN), false);
            global.DefineOwnProperty("Infinity", new DataDescriptor(double.PositiveInfinity), false);
            global.DefineOwnProperty("undefined", new DataDescriptor(Undefined.Instance), false);

            // Global object functions
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
