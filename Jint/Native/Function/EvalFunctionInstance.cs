using Esprima;
using Esprima.Ast;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Statements;

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
            return PerformEval(arguments, false);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-performeval
        /// </summary>
        public JsValue PerformEval(JsValue[] arguments, bool direct)
        {
            if (!(arguments.At(0) is JsString x))
            {
                return arguments.At(0);
            }

            var inFunction = false;
            var inMethod = false;
            var inDerivedConstructor = false;

            if (direct)
            {
                var thisEnvRec = _engine.GetThisEnvironment();
                if (thisEnvRec is FunctionEnvironmentRecord functionEnvironmentRecord)
                {
                    var F = functionEnvironmentRecord._functionObject;
                    inFunction = true;
                    inMethod = thisEnvRec.HasSuperBinding();
                    
                    if (F._constructorKind == ConstructorKind.Derived)
                    {
                        inDerivedConstructor = true;
                    }
                }
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

            if (!inFunction)
            {
                // if body Contains NewTarget, throw a SyntaxError exception.
            } 
            if (!inMethod)
            {
                // if body Contains SuperProperty, throw a SyntaxError exception.
            } 
            if (!inDerivedConstructor)
            {
                // if body Contains SuperCall, throw a SyntaxError exception.
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
    }
}
