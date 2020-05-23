using System.Runtime.CompilerServices;
using Esprima.Ast;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;

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

                    var result = _function.Body.Execute();

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

        public override bool Set(JsValue property, JsValue value, JsValue receiver)
        {
            AssertValidPropertyName(property);
            return base.Set(property, value, receiver);
        }

        public override JsValue Get(JsValue property, JsValue receiver)
        {
            AssertValidPropertyName(property);
            return base.Get(property, receiver);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AssertValidPropertyName(JsValue property)
        {
            if (property == CommonProperties.Caller
                || property ==  CommonProperties.Callee
                || property == CommonProperties.Arguments)
            {
                ExceptionHelper.ThrowTypeError(_engine, "'caller', 'callee', and 'arguments' properties may not be accessed on strict mode functions or the arguments objects for calls to them");
            }
        }
    }
}