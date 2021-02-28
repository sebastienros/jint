using Jint.Native;
using Jint.Native.Function;
using System.Threading.Tasks;

namespace Jint.Runtime.Interop
{
    /// <summary>
    /// Represents a FunctionInstance wrapping a Clr setter.
    /// </summary>
    public sealed partial class SetterFunctionInstance : FunctionInstance
    {
        public override Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments) => Task.FromResult(Call(thisObject, arguments));
    }
}