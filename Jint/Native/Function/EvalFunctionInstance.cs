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
            var code = TypeConverter.ToString(arguments.At(0));

            try
            {
                var parser = new JavaScriptParser(StrictModeScope.IsStrictModeCode);
                var program = parser.Parse(code);
                using (new StrictModeScope(program.Strict))
                {
                    var result = _engine.ExecuteStatement(program);

                    if (result.Type == Completion.Throw)
                    {
                        throw new JavaScriptException(result.Value);
                    }
                    else
                    {
                        return result.Value ?? Undefined.Instance;
                    }
                }
            }
            catch (ParserError)
            {
                throw new JavaScriptException(Engine.SyntaxError);
            }
        }
    }
}
