using Jint.Native.Object;
using Jint.Parser;
using Jint.Runtime;
using Jint.Runtime.Environments;

namespace Jint.Native.Function
{
    public class EvalFunctionInstance: FunctionInstance
    {
        private readonly Engine _engine;

        public EvalFunctionInstance(Engine engine, ObjectInstance prototype, string[] parameters, LexicalEnvironment scope, bool strict) : base(engine, parameters, scope, strict)
        {
            _engine = engine;
        }

        public override object Call(object thisObject, object[] arguments)
        {
            if (Engine.Options.IsStrict())
            {
                throw new JavaScriptException(Engine.SyntaxError, "eval() is not allowed in strict mode.");
            }
            var code = TypeConverter.ToString(arguments[0]);

            var parser = new JavaScriptParser();
            var program = parser.Parse(code);
            return _engine.ExecuteStatement(program).Value ?? Undefined.Instance;
        }
    }
}
