using System;
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

        public EvalFunctionInstance(Engine engine, string[] parameters, LexicalEnvironment scope, bool strict)
            : base(engine, _functionName, parameters, scope, strict)
        {
            Prototype = Engine.Function.PrototypeObject;
            _length = PropertyDescriptor.AllForbiddenDescriptor.NumberOne;
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return Call(thisObject, arguments, false);
        }

        public JsValue Call(JsValue thisObject, JsValue[] arguments, bool directCall)
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
                var program = parser.ParseProgram(StrictModeScope.IsStrictModeCode);
                return StrictModeScope.WithStrictModeScope(() =>
                {
                    return EvalCodeScope.WithEvalCodeScope(() =>
                    {
                        if (!directCall)
                        {
                            return Engine.WithExecutionContext(
                                Engine.GlobalEnvironment,
                                Engine.GlobalEnvironment,
                                Engine.Global,
                                () => ExecuteCallWithStrictCheck(program, arguments)).GetAwaiter().GetResult();
                        }
                        else
                        {
                            return ExecuteCallWithStrictCheck(program, arguments);
                        }
                    }).GetAwaiter().GetResult();

                }, program.Strict).GetAwaiter().GetResult();
            }
            catch (ParserException e)
            {
                if (e.Description == Messages.InvalidLHSInAssignment)
                {
                    ExceptionHelper.ThrowReferenceError(_engine, (string)null);
                }

                ExceptionHelper.ThrowSyntaxError(_engine);
                return null;
            }
        }

        private JsValue ExecuteCallWithStrictCheck(Program program, JsValue[] arguments)
        {
            var lexical = Engine.ExecutionContext.LexicalEnvironment;

            if (StrictModeScope.IsStrictModeCode)
            {
                var strictVarEnv = LexicalEnvironment.NewDeclarativeEnvironment(Engine, lexical);
                return Engine
                    .WithExecutionContext(
                        strictVarEnv, 
                        strictVarEnv, 
                        Engine.ExecutionContext.ThisBinding, 
                        () => ExecuteCall(program, arguments, lexical))
                    .GetAwaiter()
                    .GetResult();
            }
            else
            {
                return ExecuteCall(program, arguments, lexical);
            }
        }

        private JsValue ExecuteCall(Program program, JsValue[] arguments, LexicalEnvironment lexical)
        {
            bool argumentInstanceRented = Engine.DeclarationBindingInstantiation(
            DeclarationBindingType.EvalCode,
            program.HoistingScope,
            functionInstance: this,
            arguments);

            var statement = JintStatement.Build(_engine, program);
            var result = statement.Execute();
            var value = result.GetValueOrDefault();

            if (argumentInstanceRented)
            {
                lexical?._record?.FunctionWasCalled();
                _engine.ExecutionContext.VariableEnvironment?._record?.FunctionWasCalled();
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
    }
}
