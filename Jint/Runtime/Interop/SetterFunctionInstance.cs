using Jint.Native;
using Jint.Native.Function;

namespace Jint.Runtime.Interop
{
    /// <summary>
    /// Represents a FunctionInstance wrapping a Clr setter.
    /// </summary>
    public sealed class SetterFunctionInstance : FunctionInstance
    {
        private static readonly JsString _name = new JsString("set");
        private readonly Action<JsValue, JsValue> _setter;

        public SetterFunctionInstance(Engine engine, Action<JsValue, JsValue> setter)
            : base(engine, engine.Realm, _name)
        {
            _setter = setter;
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            _setter(thisObject, arguments[0]);

            return Null;
        }
    }
}
