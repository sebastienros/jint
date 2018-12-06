using System.Collections.Generic;
using Esprima.Ast;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Statements;

namespace Jint.Native.Function
{
    public sealed class ScriptFunctionInstance : FunctionInstance, IConstructor
    {
        private readonly IFunction _functionDeclaration;
        private readonly JintStatement _functionBody;

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-13.2
        /// </summary>
        public ScriptFunctionInstance(
            Engine engine, 
            IFunction functionDeclaration,
            LexicalEnvironment scope,
            bool strict)
            : base(engine, functionDeclaration.Id?.Name ?? "", GetParameterNames(functionDeclaration), scope, strict)
        {
            _functionDeclaration = functionDeclaration;
            _functionBody = JintStatement.Build(engine, functionDeclaration.Body);

            Extensible = true;
            Prototype = _engine.Function.PrototypeObject;

            _length = new PropertyDescriptor(JsNumber.Create(_formalParameters.Length), PropertyFlag.AllForbidden);

            var proto = new ObjectInstanceWithConstructor(engine, this)
            {
                Extensible = true,
                Prototype = _engine.Object.PrototypeObject
            };

            _prototype = new PropertyDescriptor(proto, PropertyFlag.OnlyWritable);

            if (_functionDeclaration.Id != null)
            {
                DefineOwnProperty("name", new PropertyDescriptor(_functionDeclaration.Id.Name, PropertyFlag.None), false);
            }

            if (strict)
            {
                var thrower = engine.Function.ThrowTypeError;
                const PropertyFlag flags = PropertyFlag.EnumerableSet | PropertyFlag.ConfigurableSet;
                DefineOwnProperty("caller", new GetSetPropertyDescriptor(thrower, thrower, flags), false);
                DefineOwnProperty("arguments", new GetSetPropertyDescriptor(thrower, thrower, flags), false);
            }
        }

        private static string[] GetParameterNames(IFunction functionDeclaration)
        {
            var list = functionDeclaration.Params;
            var count = list.Count;

            if (count == 0)
            {
                return System.ArrayExt.Empty<string>();
            }

            var names = new string[count];
            for (var i = 0; i < count; ++i)
            {
                names[i] = ((Identifier) list[i]).Name;
            }

            return names;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-13.2.1
        /// </summary>
        /// <param name="thisArg"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public override JsValue Call(JsValue thisArg, JsValue[] arguments)
        {
            var strict = Strict || _engine._isStrict;
            using (new StrictModeScope(strict, true))
            {
                // setup new execution context http://www.ecma-international.org/ecma-262/5.1/#sec-10.4.3
                JsValue thisBinding;
                if (StrictModeScope.IsStrictModeCode)
                {
                    thisBinding = thisArg;
                }
                else if (thisArg._type == Types.Undefined || thisArg._type == Types.Null)
                {
                    thisBinding = _engine.Global;
                }
                else if (thisArg._type != Types.Object)
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
                    var argumentInstanceRented = _engine.DeclarationBindingInstantiation(
                        DeclarationBindingType.FunctionCode,
                        _functionDeclaration.HoistingScope.FunctionDeclarations,
                        _functionDeclaration.HoistingScope.VariableDeclarations,
                        this,
                        arguments);

                    var result = _functionBody.Execute();
                    
                    var value = result.GetValueOrDefault();
                    
                    if (argumentInstanceRented)
                    {
                        _engine.ExecutionContext.LexicalEnvironment?._record?.FunctionWasCalled();
                        _engine.ExecutionContext.VariableEnvironment?._record?.FunctionWasCalled();
                    }

                    if (result.Type == CompletionType.Throw)
                    {
                        var ex = new JavaScriptException(value).SetCallstack(_engine, result.Location);
                        throw ex;
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
        /// <param name="arguments"></param>
        /// <returns></returns>
        public ObjectInstance Construct(JsValue[] arguments)
        {
            var proto = Get("prototype").TryCast<ObjectInstance>();
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
            private const string PropertyNameConstructor = "constructor";
            private const int PropertyNameConstructorLength = 11;
            private PropertyDescriptor _constructor;

            public ObjectInstanceWithConstructor(Engine engine, ObjectInstance thisObj) : base(engine)
            {
                _constructor = new PropertyDescriptor(thisObj, PropertyFlag.NonEnumerable);
            }

            public override IEnumerable<KeyValuePair<string, PropertyDescriptor>> GetOwnProperties()
            {
                if (_constructor != null)
                {
                    yield return new KeyValuePair<string, PropertyDescriptor>(PropertyNameConstructor, _constructor);
                }

                foreach (var entry in base.GetOwnProperties())
                {
                    yield return entry;
                }
            }

            public override PropertyDescriptor GetOwnProperty(string propertyName)
            {
                if (propertyName.Length == PropertyNameConstructorLength && propertyName == PropertyNameConstructor)
                {
                    return _constructor ?? PropertyDescriptor.Undefined;
                }

                return base.GetOwnProperty(propertyName);
            }

            protected internal override void SetOwnProperty(string propertyName, PropertyDescriptor desc)
            {
                if (propertyName.Length == PropertyNameConstructorLength && propertyName == PropertyNameConstructor)
                {
                    _constructor = desc;
                }
                else
                {
                    base.SetOwnProperty(propertyName, desc);
                }
            }

            public override bool HasOwnProperty(string propertyName)
            {
                if (propertyName.Length == PropertyNameConstructorLength && propertyName == PropertyNameConstructor)
                {
                    return _constructor != null;
                }

                return base.HasOwnProperty(propertyName);
            }

            public override void RemoveOwnProperty(string propertyName)
            {
                if (propertyName.Length == PropertyNameConstructorLength && propertyName == PropertyNameConstructor)
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