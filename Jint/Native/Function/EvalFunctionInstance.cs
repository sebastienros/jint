using Esprima;
using Esprima.Ast;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Statements;
using System.Threading.Tasks;

namespace Jint.Native.Function
{
    public sealed class EvalFunctionInstance : FunctionInstance
    {
        private static readonly ParserOptions ParserOptions = new ParserOptions { AdaptRegexp = true, Tolerant = false };
        private static readonly JsString _functionName = new JsString("eval");

        public EvalFunctionInstance(Engine engine) 
            : base(engine, _functionName, StrictModeScope.IsStrictModeCode ? FunctionThisMode.Strict : FunctionThisMode.Global)
        {
            _prototype = Engine.Function.PrototypeObject;
            _length = PropertyDescriptor.AllForbiddenDescriptor.NumberOne;
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return Call(thisObject, arguments, false);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-performeval
        /// </summary>
        public JsValue Call(JsValue thisObject, JsValue[] arguments, bool direct)
        {
            if (!(arguments.At(0) is JsString x))
            {
                return arguments.At(0);
            }

            var parser = new JavaScriptParser(x.ToString(), ParserOptions);
            Script script;
            try
            {
                script = parser.ParseScript(StrictModeScope.IsStrictModeCode);
            }
            catch (ParserException e)
            {
                return e.Description == Messages.InvalidLHSInAssignment 
                    ? ExceptionHelper.ThrowReferenceError<JsValue>(_engine)
                    : ExceptionHelper.ThrowSyntaxError<JsValue>(_engine);
            }

            var body = script.Body;
            if (body.Count == 0)
            {
                return Undefined;
            }

            var strictEval = script.Strict || _engine._isStrict;
            var ctx = _engine.ExecutionContext;

            using (new StrictModeScope(strictEval))
            {
                LexicalEnvironment lexEnv;
                LexicalEnvironment varEnv;
                if (direct)
                {
                    lexEnv = LexicalEnvironment.NewDeclarativeEnvironment(_engine, ctx.LexicalEnvironment);
                    varEnv = ctx.VariableEnvironment;
                }
                else
                {
                    lexEnv = LexicalEnvironment.NewDeclarativeEnvironment(_engine, Engine.GlobalEnvironment);
                    varEnv = Engine.GlobalEnvironment;
                }

                if (strictEval)
                {
                    varEnv = lexEnv;
                }

                // If ctx is not already suspended, suspend ctx.
                
                Engine.EnterExecutionContext(lexEnv, varEnv);
                
                try
                {
                    Engine.EvalDeclarationInstantiation(script, varEnv, lexEnv, strictEval);

                    var statement = new JintScript(_engine, script);
                    var result = statement.Execute();
                    var value = result.GetValueOrDefault();

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
                    Engine.LeaveExecutionContext();
                }
            }
        }

        public override Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments)
        {
            return CallAsync(thisObject, arguments, false);
        }

        public async Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments, bool directCall)
        {
            var arg = arguments.At(0);
            if (arg.Type != Types.String)
            {
                return arg;
            }

            var code = TypeConverter.ToString(arg);

            try
            {
                var parser = new JavaScriptParser(code, ParserOptions);
                var program = parser.ParseScript(StrictModeScope.IsStrictModeCode);
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

                            var lexicalEnvironment = _engine.ExecutionContext.LexicalEnvironment;
                            if (StrictModeScope.IsStrictModeCode)
                            {
                                strictVarEnv = LexicalEnvironment.NewDeclarativeEnvironment(Engine, lexicalEnvironment);
                                Engine.EnterExecutionContext(strictVarEnv, strictVarEnv, Engine.ExecutionContext.ThisBinding);
                            }

                            var argumentsInstance = Engine.DeclarationBindingInstantiation(
                                DeclarationBindingType.EvalCode,
                                program.HoistingScope,
                                functionInstance: this,
                                arguments);

                            var statement = JintStatement.Build(_engine, program);
                            var result = await statement.ExecuteAsync();
                            var value = result.GetValueOrDefault();

                            argumentsInstance?.FunctionWasCalled();

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
                    ExceptionHelper.ThrowReferenceError(_engine, (string)null);
                }

                return ExceptionHelper.ThrowSyntaxError<JsValue>(_engine);
            }
        }
    }
}
