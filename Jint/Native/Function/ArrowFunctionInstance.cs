using Esprima.Ast;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;
using System.Threading.Tasks;

namespace Jint.Native.Function
{
    public sealed class ArrowFunctionInstance : FunctionInstance
    {
        private readonly JintFunctionDefinition _function;

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/6.0/#sec-arrow-function-definitions
        /// </summary>
        public ArrowFunctionInstance(
            Engine engine,
            IFunction functionDeclaration,
            LexicalEnvironment scope,
            bool strict)
            : this(engine, new JintFunctionDefinition(engine, functionDeclaration), scope, strict)
        {
        }

        internal ArrowFunctionInstance(
            Engine engine,
            JintFunctionDefinition function,
            LexicalEnvironment scope,
            bool strict)
            : base(engine, function, scope, strict ? FunctionThisMode.Strict : FunctionThisMode.Lexical)
        {
            _function = function;

            PreventExtensions();
            _prototype = Engine.Function.PrototypeObject;

            _length = new LazyPropertyDescriptor(() => JsNumber.Create(function.Initialize(engine, this).Length), PropertyFlag.Configurable);
        }

        // for example RavenDB wants to inspect this
        public IFunction FunctionDeclaration => _function.Function;

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-13.2.1
        /// </summary>
        public override JsValue Call(JsValue thisArg, JsValue[] arguments)
        {
            var strict = Strict || _engine._isStrict;
            using (new StrictModeScope(strict, true))
            {
                var localEnv = LexicalEnvironment.NewFunctionEnvironment(_engine, this, Undefined);
                _engine.EnterExecutionContext(localEnv, localEnv);

                try
                {
                    _engine.FunctionDeclarationInstantiation(
                        functionInstance: this,
                        arguments,
                        localEnv);

                    var result = _function.Execute();

                    var value = result.GetValueOrDefault().Clone();

                    if (result.Type == CompletionType.Throw)
                    {
                        ExceptionHelper.ThrowJavaScriptException(_engine, value, result);
                    }

                    if (result.Type == CompletionType.Return)
                    {
                        return value;
                    }
                }
                finally
                {
                    _engine.LeaveExecutionContext();
                }

                return Undefined;
            }
        }

        public async override Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments)
        {
            var strict = Strict || _engine._isStrict;
            using (new StrictModeScope(strict, true))
            {
                var localEnv = LexicalEnvironment.NewFunctionEnvironment(_engine, this, Undefined);
                _engine.EnterExecutionContext(localEnv, localEnv);

                try
                {
                    _engine.FunctionDeclarationInstantiation(
                        functionInstance: this,
                        arguments,
                        localEnv);

                    var result = await _function.ExecuteAsync();

                    var value = result.GetValueOrDefault().Clone();

                    if (result.Type == CompletionType.Throw)
                    {
                        ExceptionHelper.ThrowJavaScriptException(_engine, value, result);
                    }

                    if (result.Type == CompletionType.Return)
                    {
                        return value;
                    }
                }
                finally
                {
                    _engine.LeaveExecutionContext();
                }

                return Undefined;
            }
        }
    }
}