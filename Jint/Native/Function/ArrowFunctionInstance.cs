using System.Runtime.CompilerServices;
using Esprima.Ast;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;

namespace Jint.Native.Function
{
    public sealed class ArrowFunctionInstance : FunctionInstance
    {
        private readonly JintFunctionDefinition _function;
        private readonly JsValue _thisBinding;

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
            : base(engine, (string) null, function._parameterNames, scope, strict)
        {
            _function = function;

            Extensible = false;
            Prototype = Engine.Function.PrototypeObject;

            _length = new PropertyDescriptor(JsNumber.Create(function._length), PropertyFlag.Configurable);
            _thisBinding = _engine.ExecutionContext.ThisBinding;
        }

        // for example RavenDB wants to inspect this
        public IFunction FunctionDeclaration => _function._function;

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-13.2.1
        /// </summary>
        /// <param name="thisArg"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public override JsValue Call(JsValue thisArg, JsValue[] arguments)
        {
            var localEnv = LexicalEnvironment.NewDeclarativeEnvironment(_engine, _scope);

            var strict = Strict || _engine._isStrict;
            using (new StrictModeScope(strict, true))
            {
                _engine.EnterExecutionContext(
                    localEnv,
                    localEnv,
                    _thisBinding);

                try
                {
                    var argumentInstanceRented = _engine.DeclarationBindingInstantiation(
                        DeclarationBindingType.FunctionCode,
                        _function._hoistingScope,
                        functionInstance: this,
                        arguments);

                    var result = _function._body.Execute();

                    var value = result.GetValueOrDefault();

                    if (argumentInstanceRented)
                    {
                        _engine.ExecutionContext.LexicalEnvironment?._record?.FunctionWasCalled();
                        _engine.ExecutionContext.VariableEnvironment?._record?.FunctionWasCalled();
                    }

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

        public override void Put(in Key propertyName, JsValue value, bool throwOnError)
        {
            AssertValidPropertyName(propertyName);
            base.Put(propertyName, value, throwOnError);
        }

        public override JsValue Get(in Key propertyName)
        {
            AssertValidPropertyName(propertyName);
            return base.Get(propertyName);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AssertValidPropertyName(in Key propertyName)
        {
            if (propertyName == KnownKeys.Caller
                || propertyName ==  KnownKeys.Callee
                || propertyName == KnownKeys.Arguments)
            {
                ExceptionHelper.ThrowTypeError(_engine, "'caller', 'callee', and 'arguments' properties may not be accessed on strict mode functions or the arguments objects for calls to them");
            }
        }
    }
}