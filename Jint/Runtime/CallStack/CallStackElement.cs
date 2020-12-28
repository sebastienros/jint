using Esprima;
using Jint.Native.Function;

namespace Jint.Runtime.CallStack
{
    internal readonly struct CallStackElement
    {
        public CallStackElement(
            FunctionInstance function,
            Location? location)
        {
            Function = function;
            Location = location;
        }

        public readonly FunctionInstance Function;
        public readonly Location? Location;

        public override string ToString()
        {
            return TypeConverter.ToString(Function?.Get(CommonProperties.Name));
        }
    }
}
