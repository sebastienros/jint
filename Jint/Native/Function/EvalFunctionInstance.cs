using Jint.Parser;
using Jint.Runtime;
using Jint.Runtime.Environments;

namespace Jint.Native.Function
{
    public class EvalFunctionInstance: FunctionInstance
    {
        private readonly Engine _engine;

        public EvalFunctionInstance(Engine engine, string[] parameters, LexicalEnvironment scope, bool strict) : base(engine, parameters, scope, strict)
        {
            _engine = engine;
        }

        public override object Call(object thisObject, object[] arguments)
        {
            return Call(thisObject, arguments, false);
        }

        public object Call(object thisObject, object[] arguments, bool directCall)
        {
            var code = TypeConverter.ToString(arguments.At(0));

            try
            {
                var parser = new JavaScriptParser(StrictModeScope.IsStrictModeCode);
                var program = parser.Parse(code);
                using (new StrictModeScope(program.Strict))
                {
                    using (new EvalCodeScope())
                    {
                        LexicalEnvironment strictVarEnv = null;

                        try
                        {
                            if (!directCall)
                            {
                                Engine.EnterExecutionContext(Engine.GlobalEnvironment, Engine.GlobalEnvironment, Engine.Global);
                            }

                            if (StrictModeScope.IsStrictModeCode)
                            {
                                strictVarEnv = LexicalEnvironment.NewDeclarativeEnvironment(Engine, Engine.ExecutionContext.LexicalEnvironment);
                                Engine.EnterExecutionContext(strictVarEnv, strictVarEnv, Engine.ExecutionContext.ThisBinding);
                            }

                            Engine.DeclarationBindingInstantiation(DeclarationBindingType.EvalCode, program.FunctionDeclarations, program.VariableDeclarations, this, arguments);
                            
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
                        finally
                        {
                            if (strictVarEnv != null)
                            {
                                Engine.LeaveExecutionContext();
                            }

                            if (!directCall)
                            {
                                Engine.LeaveExecutionContext();
                            }
                        }
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
