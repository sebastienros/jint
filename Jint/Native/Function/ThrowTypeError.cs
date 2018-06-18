using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Function
{
    public sealed class ThrowTypeError : FunctionInstance
    {
        public ThrowTypeError(Engine engine): base(engine, System.Array.Empty<string>(), engine.GlobalEnvironment, false)
        {
            DefineOwnProperty("length", new PropertyDescriptor(0, PropertyFlag.AllForbidden), false);
            Extensible = false;
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            throw new JavaScriptException(_engine.TypeError);
        }
    }
}
