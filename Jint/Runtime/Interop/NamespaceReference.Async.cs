using Jint.Native;
using Jint.Native.Object;
using System.Threading.Tasks;

namespace Jint.Runtime.Interop
{
    /// <summary>
    /// Any instance on this class represents a reference to a CLR namespace.
    /// Accessing its properties will look for a class of the full name, or instantiate
    /// a new <see cref="NamespaceReference"/> as it assumes that the property is a deeper
    /// level of the current namespace
    /// </summary>
    public partial class NamespaceReference : ObjectInstance, ICallable
    {
        public Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments)
            => Task.FromResult(Call(thisObject, arguments));
    }
}