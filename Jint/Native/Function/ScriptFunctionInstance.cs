using Esprima.Ast;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;

namespace Jint.Native.Function
{
    public sealed class ScriptFunctionInstance : FunctionInstance, IConstructor
    {
        private bool _isClassConstructor;

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-13.2
        /// </summary>
        public ScriptFunctionInstance(
            Engine engine,
            IFunction functionDeclaration,
            EnvironmentRecord scope,
            bool strict,
            ObjectInstance proto = null)
            : this(engine, new JintFunctionDefinition(engine, functionDeclaration), scope, strict ? FunctionThisMode.Strict : FunctionThisMode.Global, proto)
        {
        }

        internal ScriptFunctionInstance(
            Engine engine,
            JintFunctionDefinition function,
            EnvironmentRecord scope,
            FunctionThisMode thisMode,
            ObjectInstance proto = null)
            : base(engine, function, scope, thisMode)
        {
            _prototype = proto ?? _engine.Function.PrototypeObject;
            _length = new LazyPropertyDescriptor(() => JsNumber.Create(function.Initialize(engine, this).Length), PropertyFlag.Configurable);

            if (!function.Strict && !engine._isStrict && function.Function is not ArrowFunctionExpression)
            {
                DefineOwnProperty(CommonProperties.Arguments, engine._callerCalleeArgumentsThrowerConfigurable);
                DefineOwnProperty(CommonProperties.Caller, new PropertyDescriptor(Undefined, PropertyFlag.Configurable));
            }
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-ecmascript-function-objects-call-thisargument-argumentslist
        /// </summary>
        public override JsValue Call(JsValue thisArgument, JsValue[] arguments)
        {
            if (_isClassConstructor)
            {
                ExceptionHelper.ThrowTypeError(_engine, $"Class constructor {_functionDefinition.Name} cannot be invoked without 'new'");
            }

            var calleeContext = PrepareForOrdinaryCall(Undefined);

            OrdinaryCallBindThis(calleeContext, thisArgument);

            // actual call

            var strict = _thisMode == FunctionThisMode.Strict || _engine._isStrict;
            using (new StrictModeScope(strict, true))
            {
                try
                {
                    var result = OrdinaryCallEvaluateBody(arguments);

                    if (result.Type == CompletionType.Throw)
                    {
                        ExceptionHelper.ThrowJavaScriptException(_engine, result.Value, result);
                    }

                    // The DebugHandler needs the current execution context before the return for stepping through the return point
                    if (_engine._isDebugMode)
                    {
                        // We don't have a statement, but we still need a Location for debuggers. DebugHandler will infer one from
                        // the function body:
                        _engine.DebugHandler.OnReturnPoint(
                            _functionDefinition.Function.Body,
                            result.Type == CompletionType.Normal ? Undefined : result.Value
                        );
                    }

                    if (result.Type == CompletionType.Return)
                    {
                        return result.Value;
                    }
                }
                finally
                {
                    _engine.LeaveExecutionContext();
                }

                return Undefined;
            }
        }

        internal override bool IsConstructor =>
            (_homeObject.IsUndefined() || _isClassConstructor) 
            && _functionDefinition?.Function is not ArrowFunctionExpression;

        /// <summary>
        /// https://tc39.es/ecma262/#sec-ecmascript-function-objects-construct-argumentslist-newtarget
        /// </summary>
        public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            var kind = _constructorKind;

            var thisArgument = Undefined;
            
            if (kind == ConstructorKind.Base)
            {
                thisArgument = OrdinaryCreateFromConstructor(newTarget, _engine.Object.PrototypeObject, static (engine, _) => new ObjectInstance(engine));
            }

            var calleeContext = PrepareForOrdinaryCall(newTarget);

            if (kind == ConstructorKind.Base)
            {
                OrdinaryCallBindThis(calleeContext, thisArgument);
            }

            var constructorEnv = (FunctionEnvironmentRecord) calleeContext.LexicalEnvironment;
            
            var strict = _thisMode == FunctionThisMode.Strict || _engine._isStrict;
            using (new StrictModeScope(strict, force: true))
            {
                try
                {
                    var result = OrdinaryCallEvaluateBody(arguments);

                    // The DebugHandler needs the current execution context before the return for stepping through the return point
                    if (_engine._isDebugMode && result.Type != CompletionType.Throw)
                    {
                        // We don't have a statement, but we still need a Location for debuggers. DebugHandler will infer one from
                        // the function body:
                        _engine.DebugHandler.OnReturnPoint(
                            _functionDefinition.Function.Body,
                            result.Type == CompletionType.Normal ? thisArgument : result.Value
                        );
                    }

                    if (result.Type == CompletionType.Return)
                    {
                        if (result.Value is ObjectInstance oi)
                        {
                            return oi;
                        }

                        if (kind == ConstructorKind.Base)
                        {
                            return (ObjectInstance) thisArgument!;
                        }

                        if (!result.Value.IsUndefined())
                        {
                            ExceptionHelper.ThrowTypeError(_engine);
                        }
                    }
                    else if (result.Type == CompletionType.Throw)
                    {
                        ExceptionHelper.ThrowJavaScriptException(_engine, result.Value, result);
                    }
                }
                finally
                {
                    _engine.LeaveExecutionContext();
                }
            }

            return (ObjectInstance) constructorEnv.GetThisBinding();
        }

        internal void MakeClassConstructor()
        {
            _isClassConstructor = true;
        }
    }
}