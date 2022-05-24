using Jint.Native.Object;

namespace Jint.Native
{
    /// <summary>
    /// Needs to be public, https://github.com/sebastienros/jint/issues/1144
    /// </summary>
    public interface IConstructor
    {
        ObjectInstance Construct(JsValue[] arguments, JsValue newTarget);
    }
}
