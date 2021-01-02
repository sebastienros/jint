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
        private bool _isClassConstructor;

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-13.2
        /// </summary>
        public ScriptFunctionInstance(
            Engine engine,
            IFunction functionDeclaration,
            LexicalEnvironment scope,
            bool strict,
            ObjectInstance proto = null)
            : this(engine, new JintFunctionDefinition(engine, functionDeclaration), scope, strict ? FunctionThisMode.Strict : FunctionThisMode.Global, proto)
        {
        }

        internal ScriptFunctionInstance(
            Engine engine,
            JintFunctionDefinition function,
            LexicalEnvironment scope,
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
                    var result = OrdinaryCallEvaluateBody(arguments, calleeContext);

                    if (result.Type == CompletionType.Throw)
                    {
                        ExceptionHelper.ThrowJavaScriptException(_engine, result.Value, result);
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

            var constructorEnv = (FunctionEnvironmentRecord) calleeContext.LexicalEnvironment._record;
            
            var strict = _thisMode == FunctionThisMode.Strict || _engine._isStrict;
            using (new StrictModeScope(strict, force: true))
            {
                try
                {
                    var result = OrdinaryCallEvaluateBody(arguments, calleeContext);

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
        
        internal void MakeConstructor(bool writableProperty = true, ObjectInstance prototype = null)
        {
            _constructorKind = ConstructorKind.Base;
            if (prototype is null)
            {
                prototype = new ObjectInstanceWithConstructor(_engine, this)
                {
                    _prototype = _engine.Object.PrototypeObject
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