using System.Collections.Generic;
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
        internal bool _isClassConstructor;

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-13.2
        /// </summary>
        public ScriptFunctionInstance(
            Engine engine,
            IFunction functionDeclaration,
            EnvironmentRecord scope,
            bool strict,
            ObjectInstance proto = null)
            : this(
                engine,
                new JintFunctionDefinition(engine, functionDeclaration),
                scope,
                strict ? FunctionThisMode.Strict : FunctionThisMode.Global,
                proto)
        {
        }

        internal ScriptFunctionInstance(
            Engine engine,
            JintFunctionDefinition function,
            EnvironmentRecord scope,
            FunctionThisMode thisMode,
            ObjectInstance proto = null)
            : base(engine, engine.Realm, function, scope, thisMode)
        {
            _prototype = proto ?? _engine.Realm.Intrinsics.Function.PrototypeObject;
            _length = new LazyPropertyDescriptor(null, _ => JsNumber.Create(function.Initialize(this).Length), PropertyFlag.Configurable);

            if (!function.Strict && !engine._isStrict && function.Function is not ArrowFunctionExpression)
            {
                DefineOwnProperty(CommonProperties.Arguments, new GetSetPropertyDescriptor.ThrowerPropertyDescriptor(engine, PropertyFlag.Configurable | PropertyFlag.CustomJsValue));
                DefineOwnProperty(CommonProperties.Caller, new PropertyDescriptor(Undefined, PropertyFlag.Configurable));
            }
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-ecmascript-function-objects-call-thisargument-argumentslist
        /// </summary>
        public override JsValue Call(JsValue thisArgument, JsValue[] arguments)
        {
            var strict = _thisMode == FunctionThisMode.Strict || _engine._isStrict;
            using (new StrictModeScope(strict, true))
            {
                try
                {
                    var calleeContext = PrepareForOrdinaryCall(Undefined);

                    if (_isClassConstructor)
                    {
                        ExceptionHelper.ThrowTypeError(calleeContext.Realm, $"Class constructor {_functionDefinition.Name} cannot be invoked without 'new'");
                    }

                    OrdinaryCallBindThis(calleeContext, thisArgument);

                    // actual call
                    var context = _engine._activeEvaluationContext ?? new EvaluationContext(_engine);
                    var result = OrdinaryCallEvaluateBody(context, arguments, calleeContext);

                    if (result.Type == CompletionType.Throw)
                    {
                        ExceptionHelper.ThrowJavaScriptException(_engine, result.Value, result);
                    }

                    // The DebugHandler needs the current execution context before the return for stepping through the return point
                    if (context.DebugMode)
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
        ObjectInstance IConstructor.Construct(JsValue[] arguments, JsValue newTarget)
        {
            var callerContext = _engine.ExecutionContext;
            var kind = _constructorKind;

            var thisArgument = Undefined;

            if (kind == ConstructorKind.Base)
            {
                thisArgument = OrdinaryCreateFromConstructor(
                    newTarget,
                    static intrinsics => intrinsics.Object.PrototypeObject,
                    static (engine, realm, _) => new ObjectInstance(engine));
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
                    var result = OrdinaryCallEvaluateBody(_engine._activeEvaluationContext, arguments, calleeContext);

                    // The DebugHandler needs the current execution context before the return for stepping through the return point
                    if (_engine._activeEvaluationContext.DebugMode && result.Type != CompletionType.Throw)
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
                            ExceptionHelper.ThrowTypeError(callerContext.Realm);
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

        internal void MakeConstructor(bool writableProperty = true, ObjectInstance prototype = null)
        {
            _constructorKind = ConstructorKind.Base;
            if (prototype is null)
            {
                prototype = new ObjectInstanceWithConstructor(_engine, this)
                {
                    _prototype = _realm.Intrinsics.Object.PrototypeObject
                };
            }

            _prototypeDescriptor = new PropertyDescriptor(prototype, writableProperty, enumerable: false, configurable: false);
        }

        internal void MakeClassConstructor()
        {
            _isClassConstructor = true;
        }

        private class ObjectInstanceWithConstructor : ObjectInstance
        {
            private PropertyDescriptor _constructor;

            public ObjectInstanceWithConstructor(Engine engine, ObjectInstance thisObj) : base(engine)
            {
                _constructor = new PropertyDescriptor(thisObj, PropertyFlag.NonEnumerable);
            }

            public override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
            {
                if (_constructor != null)
                {
                    yield return new KeyValuePair<JsValue, PropertyDescriptor>(CommonProperties.Constructor, _constructor);
                }

                foreach (var entry in base.GetOwnProperties())
                {
                    yield return entry;
                }
            }

            public override PropertyDescriptor GetOwnProperty(JsValue property)
            {
                if (property == CommonProperties.Constructor)
                {
                    return _constructor ?? PropertyDescriptor.Undefined;
                }

                return base.GetOwnProperty(property);
            }

            protected internal override void SetOwnProperty(JsValue property, PropertyDescriptor desc)
            {
                if (property == CommonProperties.Constructor)
                {
                    _constructor = desc;
                }
                else
                {
                    base.SetOwnProperty(property, desc);
                }
            }

            public override bool HasOwnProperty(JsValue property)
            {
                if (property == CommonProperties.Constructor)
                {
                    return _constructor != null;
                }

                return base.HasOwnProperty(property);
            }

            public override void RemoveOwnProperty(JsValue property)
            {
                if (property == CommonProperties.Constructor)
                {
                    _constructor = null;
                }
                else
                {
                    base.RemoveOwnProperty(property);
                }
            }
        }
    }
}
