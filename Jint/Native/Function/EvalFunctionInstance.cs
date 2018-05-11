using Esprima;
using Jint.Native.Argument;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;

namespace Jint.Native.Function
{
    public class EvalFunctionInstance: FunctionInstance
    {
        private static readonly ParserOptions ParserOptions = new ParserOptions { AdaptRegexp = true, Tolerant = false };

        public EvalFunctionInstance(Engine engine, string[] parameters, LexicalEnvironment scope, bool strict) : base(engine, parameters, scope, strict)
        {
            Prototype = Engine.Function.PrototypeObject;
            SetOwnProperty("length", new PropertyDescriptor(1, PropertyFlag.AllForbidden));
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return Call(thisObject, arguments, false);
        }

        public JsValue Call(JsValue thisObject, JsValue[] arguments, bool directCall)
        {
            if (arguments.At(0).Type != Types.String)
            {
                return arguments.At(0);
            }

            var code = TypeConverter.ToString(arguments.At(0));

            try
            {
                var parser = new JavaScriptParser(code, ParserOptions);
                var program = parser.ParseProgram(StrictModeScope.IsStrictModeCode);
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

                            bool argumentInstanceRented = Engine.DeclarationBindingInstantiation(
                                DeclarationBindingType.EvalCode,
                                program.HoistingScope.FunctionDeclarations,
                                program.HoistingScope.VariableDeclarations,
                                this, 
                                arguments);

                            var result = _engine.ExecuteStatement(program);
                            var value = result.GetValueOrDefault();

                            // we can safely release arguments if they don't escape the scope
                            if (argumentInstanceRented
                                && Engine.ExecutionContext.LexicalEnvironment?.Record is DeclarativeEnvironmentRecord der
                                && !(result.Value is ArgumentsInstance))
                            {
                                der.ReleaseArguments();
                            }

                            if (result.Type == CompletionType.Throw)
                            {
                                var ex = new JavaScriptException(value).SetCallstack(_engine, result.Location);
                                throw ex;
                            }
                            else
                            {
                                return value;
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
            catch (ParserException e)
            {
                if (e.Description == Messages.InvalidLHSInAssignment)
                {
                    throw new JavaScriptException(Engine.ReferenceError);
                }

                throw new JavaScriptException(Engine.SyntaxError);
            }
        }
    }
}
