using Jint.Runtime;

namespace Jint.Native
{
    public interface IPrimitiveType
    {
        Types Type { get; } 
        object PrimitiveValue { get; }
    }
}
