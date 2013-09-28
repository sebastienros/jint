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
            if (StrictModeScope.IsStrictModeCode)
            {
                throw new JavaScriptException(Engine.SyntaxError, "eval() is not allowed in strict mode.");
            }

            var code = TypeConverter.ToString(arguments.At(0));

            var parser = new JavaScriptParser();
            try
            {
                var program = parser.Parse(code);
                using (new StrictModeScope(program.Strict))
                {
                    return _engine.ExecuteStatement(program).Value ?? Undefined.Instance;
                }
            }
            catch (ParserError e)
            {
                throw new JavaScriptException(Engine.SyntaxError);
            }

            
        }
    }
}
