using Jint.Runtime.Environments;

namespace Jint.Native.Function
{
    public sealed class FunctionShim : FunctionInstance
    {
        public FunctionShim(Engine engine, string[] parameters, LexicalEnvironment scope) : base(engine, parameters, scope, false)
        {
        }

        public override object Call(object thisObject, object[] arguments)
        {
            return Undefined.Instance;
        }
    }
}
