using Jint.Native.Object;

namespace Jint.Native
{
    public interface IConstructor
    {
        object Call(object thisObject, object[] arguments);
        ObjectInstance Construct(object[] arguments);

        ObjectInstance PrototypeObject { get; }
    }
}
