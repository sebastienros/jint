using Jint.Native.Object;
using Jint.Parser;
using Jint.Parser.Ast;
using Jint.Runtime;
using Jint.Runtime.Environments;

namespace Jint.Native.Function
{
    public class EvalFunctionInstance: FunctionInstance
    {
        private readonly Engine _engine;

        public EvalFunctionInstance(Engine engine, ObjectInstance prototype, Identifier[] parameters, LexicalEnvironment scope, bool strict) : base(engine, prototype, parameters, scope, strict)
        {
            _engine = engine;
        }

        public override object Call(object thisObject, object[] arguments)
        {
            var code = TypeConverter.ToString(arguments[0]);

            var parser = new JavascriptParser();
            var program = parser.Parse(code);
            return _engine.ExecuteStatement(program).Value;
        }
    }
}
