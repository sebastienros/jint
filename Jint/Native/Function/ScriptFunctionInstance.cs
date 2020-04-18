using System.Collections.Generic;
using Esprima.Ast;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;

namespace Jint.Native.Function
{
    public sealed class ScriptFunctionInstance : FunctionInstance, IConstructor
    {
        internal readonly JintFunctionDefinition _function;

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-13.2
        /// </summary>
        public ScriptFunctionInstance(
            Engine engine,
            IFunction functionDeclaration,
            LexicalEnvironment scope,
            bool strict)
            : this(engine, new JintFunctionDefinition(engine, functionDeclaration), scope, strict)
        {
        }

        internal ScriptFunctionInstance(
            Engine engine,
            JintFunctionDefinition function,
            LexicalEnvironment scope,
            bool strict)
            : base(engine, function, scope, strict)
        {
            _function = function;

            _prototype = _engine.Function.PrototypeObject;

            _length = new PropertyDescriptor(JsNumber.Create(function._length), PropertyFlag.Configurable);

            var proto = new ObjectInstanceWithConstructor(engine, this)
            {
                _prototype = _engine.Object.PrototypeObject
            };

            _prototypeDescriptor = new PropertyDescriptor(proto, PropertyFlag.OnlyWritable);

            if (strict)
            {
                DefineOwnProperty(CommonProperties.Caller, engine._getSetThrower);
                DefineOwnProperty(CommonProperties.Arguments, engine._getSetThrower);
            }
        }

        // for example RavenDB wants to inspect this
        public IFunction FunctionDeclaration => _function._function;

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-13.2.1
        /// </summary>
        public override JsValue Call(JsValue thisArg, JsValue[] arguments)
        {
            var strict = _strict || _engine._isStrict;
            using (new StrictModeScope(strict, true))
            {
                // setup new execution context http://www.ecma-international.org/ecma-262/5.1/#sec-10.4.3
                JsValue thisBinding;
                if (StrictModeScope.IsStrictModeCode)
                {
                    thisBinding = thisArg;
                }
                else if (thisArg.IsNullOrUndefined())
                {
                    thisBinding = _engine.Global;
                }
                else if (!thisArg.IsObject())
                {
                    thisBinding = TypeConverter.ToObject(_engine, thisArg);
                }
                else
                {
                    thisBinding = thisArg;
                }

                var localEnv = LexicalEnvironment.NewDeclarativeEnvironment(_engine, _scope);

                _engine.EnterExecutionContext(localEnv, localEnv, thisBinding);

                try
                {
                    var argumentsInstance = _engine.FunctionDeclarationInstantiation(
                        functionInstance: this,
                        arguments);

                    var result = _function._body.Execute();

                    var value = result.GetValueOrDefault().Clone();

                    argumentsInstance?.FunctionWasCalled();

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

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-13.2.2
        /// </summary>
        public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            var thisArgument = OrdinaryCreateFromConstructor(TypeConverter.ToObject(_engine, newTarget), _engine.Object.PrototypeObject);

            var result = Call(thisArgument, arguments).TryCast<ObjectInstance>();
            if (!ReferenceEquals(result, null))
            {
                return result;
            }

            return thisArgument;
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