using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Esprima.Ast;
using Jint.Native.Object;
using Jint.Native.Proxy;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;

namespace Jint.Native.Function
{
    public abstract class FunctionInstance : ObjectInstance, ICallable
    {
        protected PropertyDescriptor _prototypeDescriptor;

        protected internal PropertyDescriptor _length;
        private PropertyDescriptor _nameDescriptor;

        protected internal EnvironmentRecord _environment;
        internal readonly JintFunctionDefinition _functionDefinition;
        internal readonly FunctionThisMode _thisMode;
        internal JsValue _homeObject = Undefined;
        internal ConstructorKind _constructorKind = ConstructorKind.Base;

        internal Realm _realm;
        private PrivateEnvironmentRecord _privateEnvironment;

        protected FunctionInstance(
            Engine engine,
            Realm realm,
            JsString name)
            : this(engine, realm, name, FunctionThisMode.Global, ObjectClass.Function)
        {
        }

        internal FunctionInstance(
            Engine engine,
            Realm realm,
            JintFunctionDefinition function,
            EnvironmentRecord scope,
            FunctionThisMode thisMode)
            : this(
                engine,
                realm,
                !string.IsNullOrWhiteSpace(function.Name) ? new JsString(function.Name) : null,
                thisMode)
        {
            _functionDefinition = function;
            _environment = scope;
        }

        internal FunctionInstance(
            Engine engine,
            Realm realm,
            JsString name,
            FunctionThisMode thisMode = FunctionThisMode.Global,
            ObjectClass objectClass = ObjectClass.Function)
            : base(engine, objectClass)
        {
            if (name is not null)
            {
                _nameDescriptor = new PropertyDescriptor(name, PropertyFlag.Configurable);
            }
            _realm = realm;
            _thisMode = thisMode;
        }

        // for example RavenDB wants to inspect this
        public IFunction FunctionDeclaration => _functionDefinition.Function;

        /// <summary>
        /// Executed when a function object is used as a function
        /// </summary>
        /// <param name="thisObject"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public abstract JsValue Call(JsValue thisObject, JsValue[] arguments);

        public bool Strict => _thisMode == FunctionThisMode.Strict;

        internal override bool IsConstructor => this is IConstructor;

        public override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
        {
            if (_prototypeDescriptor != null)
            {
                yield return new KeyValuePair<JsValue, PropertyDescriptor>(CommonProperties.Prototype, _prototypeDescriptor);
            }
            if (_length != null)
            {
                yield return new KeyValuePair<JsValue, PropertyDescriptor>(CommonProperties.Length, _length);
            }
            if (_nameDescriptor != null)
            {
                yield return new KeyValuePair<JsValue, PropertyDescriptor>(CommonProperties.Name, GetOwnProperty(CommonProperties.Name));
            }

            foreach (var entry in base.GetOwnProperties())
            {
                yield return entry;
            }
        }

        public override List<JsValue> GetOwnPropertyKeys(Types types)
        {
            var keys = base.GetOwnPropertyKeys(types);

            // works around a problem where we don't use property for function names and classes should report it last
            // as it's the last operation when creating a class constructor
            if ((types & Types.String) != 0 && _nameDescriptor != null && this is ScriptFunctionInstance { _isClassConstructor: true })
            {
                keys.Add(CommonProperties.Name);
            }

            return keys;
        }

        internal override IEnumerable<JsValue> GetInitialOwnStringPropertyKeys()
        {
            if (_length != null)
            {
                yield return CommonProperties.Length;
            }

            // works around a problem where we don't use property for function names and classes should report it last
            // as it's the last operation when creating a class constructor
            if (_nameDescriptor != null && this is not ScriptFunctionInstance { _isClassConstructor: true })
            {
                yield return CommonProperties.Name;
            }

            if (_prototypeDescriptor != null)
            {
                yield return CommonProperties.Prototype;
            }
        }

        public override PropertyDescriptor GetOwnProperty(JsValue property)
        {
            if (property == CommonProperties.Prototype)
            {
                return _prototypeDescriptor ?? PropertyDescriptor.Undefined;
            }
            if (property == CommonProperties.Length)
            {
                return _length ?? PropertyDescriptor.Undefined;
            }
            if (property == CommonProperties.Name)
            {
                return _nameDescriptor ?? PropertyDescriptor.Undefined;
            }

            return base.GetOwnProperty(property);
        }

        protected internal override void SetOwnProperty(JsValue property, PropertyDescriptor desc)
        {
            if (property == CommonProperties.Prototype)
            {
                _prototypeDescriptor = desc;
            }
            else if (property == CommonProperties.Length)
            {
                _length = desc;
            }
            else if (property == CommonProperties.Name)
            {
                _nameDescriptor = desc;
            }
            else
            {
                base.SetOwnProperty(property, desc);
            }
        }

        public override bool HasOwnProperty(JsValue property)
        {
            if (property == CommonProperties.Prototype)
            {
                return _prototypeDescriptor != null;
            }
            if (property == CommonProperties.Length)
            {
                return _length != null;
            }
            if (property == CommonProperties.Name)
            {
                return _nameDescriptor != null;
            }

            return base.HasOwnProperty(property);
        }

        public override void RemoveOwnProperty(JsValue property)
        {
            if (property == CommonProperties.Prototype)
            {
                _prototypeDescriptor = null;
            }
            if (property == CommonProperties.Length)
            {
                _length = null;
            }
            if (property == CommonProperties.Name)
            {
                _nameDescriptor = null;
            }

            base.RemoveOwnProperty(property);
        }

        internal void SetFunctionName(JsValue name, string prefix = null, bool force = false)
        {
            if (!force && _nameDescriptor != null && !UnwrapJsValue(_nameDescriptor).IsUndefined())
            {
                return;
            }

            if (name is JsSymbol symbol)
            {
                name = symbol._value.IsUndefined()
                    ? JsString.Empty
                    : new JsString("[" + symbol._value + "]");
            }
            if (!string.IsNullOrWhiteSpace(prefix))
            {
                name = prefix + " " + name;
            }

            _nameDescriptor = new PropertyDescriptor(name, PropertyFlag.Configurable);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-ordinarycreatefromconstructor
        /// </summary>
        /// <remarks>
        /// Uses separate builder to get correct type with state support to prevent allocations.
        /// In spec intrinsicDefaultProto is string pointing to intrinsic, but we do a selector.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal T OrdinaryCreateFromConstructor<T>(
            JsValue constructor,
            Func<Intrinsics, ObjectInstance> intrinsicDefaultProto,
            Func<Engine, Realm, JsValue, T> objectCreator,
            JsValue state = null) where T : ObjectInstance
        {
            var proto = GetPrototypeFromConstructor(constructor, intrinsicDefaultProto);

            var obj = objectCreator(_engine, _realm, state);
            obj._prototype = proto;
            return obj;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-getprototypefromconstructor
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ObjectInstance GetPrototypeFromConstructor(JsValue constructor, Func<Intrinsics, ObjectInstance> intrinsicDefaultProto)
        {
            var proto = constructor.Get(CommonProperties.Prototype, constructor) as ObjectInstance;
            if (proto is null)
            {
                var realm = GetFunctionRealm(constructor);
                proto = intrinsicDefaultProto(realm.Intrinsics);
            }
            return proto;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-getfunctionrealm
        /// </summary>
        internal Realm GetFunctionRealm(JsValue obj)
        {
            if (obj is FunctionInstance functionInstance && functionInstance._realm is not null)
            {
                return functionInstance._realm;
            }

            if (obj is BindFunctionInstance bindFunctionInstance)
            {
                return GetFunctionRealm(bindFunctionInstance.TargetFunction);
            }

            if (obj is ProxyInstance proxyInstance)
            {
                if (proxyInstance._handler is null)
                {
                    ExceptionHelper.ThrowTypeErrorNoEngine();
                }

                return GetFunctionRealm(proxyInstance._target);
            }

            return _engine.ExecutionContext.Realm;
        }

        internal void MakeMethod(ObjectInstance homeObject)
        {
            _homeObject = homeObject;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-ordinarycallbindthis
        /// </summary>
        internal void OrdinaryCallBindThis(ExecutionContext calleeContext, JsValue thisArgument)
        {
            var thisMode = _thisMode;
            if (thisMode == FunctionThisMode.Lexical)
            {
                return;
            }

            var calleeRealm = _realm;

            var localEnv = (FunctionEnvironmentRecord) calleeContext.LexicalEnvironment;

            JsValue thisValue;
            if (_thisMode == FunctionThisMode.Strict)
            {
                thisValue = thisArgument;
            }
            else
            {
                if (thisArgument.IsNullOrUndefined())
                {
                    var globalEnv = calleeRealm.GlobalEnv;
                    thisValue = globalEnv.GlobalThisValue;
                }
                else
                {
                    thisValue = TypeConverter.ToObject(calleeRealm, thisArgument);
                }
            }

            localEnv.BindThisValue(thisValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Completion OrdinaryCallEvaluateBody(
            EvaluationContext context,
            JsValue[] arguments,
            ExecutionContext calleeContext)
        {
            var argumentsInstance = _engine.FunctionDeclarationInstantiation(
                functionInstance: this,
                arguments);

            var result = _functionDefinition.Execute(context);
            argumentsInstance?.FunctionWasCalled();

            return result;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-prepareforordinarycall
        /// </summary>
        internal ExecutionContext PrepareForOrdinaryCall(JsValue newTarget)
        {
            var callerContext = _engine.ExecutionContext;

            var localEnv = JintEnvironment.NewFunctionEnvironment(_engine, this, newTarget);
            var calleeRealm = _realm;

            var calleeContext = new ExecutionContext(
                localEnv,
                localEnv,
                _privateEnvironment,
                calleeRealm,
                this);

            // If callerContext is not already suspended, suspend callerContext.
            // Push calleeContext onto the execution context stack; calleeContext is now the running execution context.
            // NOTE: Any exception objects produced after this point are associated with calleeRealm.
            // Return calleeContext.

            return _engine.EnterExecutionContext(calleeContext);
        }

        public override string ToString()
        {
            // TODO no way to extract SourceText from Esprima at the moment, just returning native code
            var nameValue = _nameDescriptor != null ? UnwrapJsValue(_nameDescriptor) : JsString.Empty;
            var name = "";
            if (!nameValue.IsUndefined())
            {
                name = TypeConverter.ToString(nameValue);
            }
            return "function " + name + "() { [native code] }";
        }
    }
}
