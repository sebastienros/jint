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
        private LexicalEnvironment _localEnv;


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
            : base(engine, function._name, function._parameterNames, scope, strict)
        {
            _function = function;

            Extensible = true;
            Prototype = _engine.Function.PrototypeObject;

            _length = new PropertyDescriptor(JsNumber.Create(function._length), PropertyFlag.Configurable);

            var proto = new ObjectInstanceWithConstructor(engine, this)
            {
                Extensible = true,
                Prototype = _engine.Object.PrototypeObject
            };

            _prototype = new PropertyDescriptor(proto, PropertyFlag.OnlyWritable);

            if (strict)
            {
                DefineOwnProperty(KnownKeys.Caller, engine._getSetThrower, false);
                DefineOwnProperty(KnownKeys.Arguments, engine._getSetThrower, false);
            }
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
            var strict = _strict || _engine._isStrict;
            using (new StrictModeScope(strict, true))
            {
                // setup new execution context http://www.ecma-international.org/ecma-262/5.1/#sec-10.4.3
                JsValue thisBinding;
                if (StrictModeScope.IsStrictModeCode)
                {
                    thisBinding = thisArg;
                }
                else if (thisArg._type == InternalTypes.Undefined || thisArg._type == InternalTypes.Null)
                {
                    thisBinding = _engine.Global;
                }
                else if (thisArg._type != InternalTypes.Object)
                {
                    thisBinding = TypeConverter.ToObject(_engine, thisArg);
                }
                else
                {
                    thisBinding = thisArg;
                }

                var localEnv = _localEnv ?? LexicalEnvironment.NewDeclarativeEnvironment(_engine, _scope);
                localEnv.Reset(_scope);
                _localEnv = null;

                _engine.EnterExecutionContext(localEnv, localEnv, thisBinding);

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
                    _localEnv = localEnv;
                }

                return Undefined;
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-13.2.2
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public ObjectInstance Construct(JsValue[] arguments)
        {
            var proto = Get(KnownKeys.Prototype).TryCast<ObjectInstance>();

            var obj = new ObjectInstance(_engine)
            {
                Extensible = true,
                Prototype = proto ?? _engine.Object.PrototypeObject
            };

            var result = Call(obj, arguments).TryCast<ObjectInstance>();
            if (!ReferenceEquals(result, null))
            {
                return result;
            }

            return obj;
        }

        private class ObjectInstanceWithConstructor : ObjectInstance
        {
            private PropertyDescriptor _constructor;

            public ObjectInstanceWithConstructor(Engine engine, ObjectInstance thisObj) : base(engine)
            {
                _constructor = new PropertyDescriptor(thisObj, PropertyFlag.NonEnumerable);
            }

            public override IEnumerable<KeyValuePair<string, PropertyDescriptor>> GetOwnProperties()
            {
                if (_constructor != null)
                {
                    yield return new KeyValuePair<string, PropertyDescriptor>(KnownKeys.Constructor, _constructor);
                }

                foreach (var entry in base.GetOwnProperties())
                {
                    yield return entry;
                }
            }

            public override PropertyDescriptor GetOwnProperty(in Key propertyName)
            {
                if (propertyName == KnownKeys.Constructor)
                {
                    return _constructor ?? PropertyDescriptor.Undefined;
                }

                return base.GetOwnProperty(propertyName);
            }

            protected internal override void SetOwnProperty(in Key propertyName, PropertyDescriptor desc)
            {
                if (propertyName == KnownKeys.Constructor)
                {
                    _constructor = desc;
                }
                else
                {
                    base.SetOwnProperty(propertyName, desc);
                }
            }

            public override bool HasOwnProperty(in Key propertyName)
            {
                if (propertyName == KnownKeys.Constructor)
                {
                    return _constructor != null;
                }

                return base.HasOwnProperty(propertyName);
            }

            public override void RemoveOwnProperty(in Key propertyName)
            {
                if (propertyName == KnownKeys.Constructor)
                {
                    _constructor = null;
                }
                else
                {
                    base.RemoveOwnProperty(propertyName);
                }
            }
        }
    }
}