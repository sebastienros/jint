using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Esprima.Ast;
using Jint.Native.Object;
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

        protected internal LexicalEnvironment _environment;
        internal readonly JintFunctionDefinition _functionDefinition;
        internal readonly FunctionThisMode _thisMode;
        internal JsValue _homeObject = Undefined;
        internal ConstructorKind _constructorKind = ConstructorKind.Base;

        internal FunctionInstance(
            Engine engine,
            JintFunctionDefinition function,
            LexicalEnvironment scope,
            FunctionThisMode thisMode)
            : this(engine, !string.IsNullOrWhiteSpace(function.Name) ? new JsString(function.Name) : null, thisMode)
        {
            _functionDefinition = function;
            _environment = scope;
        }

        internal FunctionInstance(
            Engine engine,
            JsString name,
            FunctionThisMode thisMode = FunctionThisMode.Global,
            ObjectClass objectClass = ObjectClass.Function)
            : base(engine, objectClass)
        {
            if (name is not null)
            {
                _nameDescriptor = new PropertyDescriptor(name, PropertyFlag.Configurable);
            }
            _thisMode = thisMode;
        }

        protected FunctionInstance(
            Engine engine,
            JsString name)
            : this(engine, name, FunctionThisMode.Global, ObjectClass.Function)
        {
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

        public virtual bool HasInstance(JsValue v)
        {
            if (!(v is ObjectInstance o))
            {
                return false;
            }

            var p = Get(CommonProperties.Prototype);
            if (!(p is ObjectInstance prototype))
            {
                ExceptionHelper.ThrowTypeError(_engine, $"Function has non-object prototype '{TypeConverter.ToString(p)}' in instanceof check");
            }

            while (true)
            {
                o = o.Prototype;

                if (o is null)
                {
                    return false;
                }

                if (SameValue(p, o))
                {
                    return true;
                }
            }
        }

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
            var keys = new List<JsValue>();
            if (_prototypeDescriptor != null)
            {
                keys.Add(CommonProperties.Prototype);
            }
            if (_length != null)
            {
                keys.Add(CommonProperties.Length);
            }
            if (_nameDescriptor != null)
            {
                keys.Add(CommonProperties.Name);
            }

            keys.AddRange(base.GetOwnPropertyKeys(types));

            return keys;
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
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal T OrdinaryCreateFromConstructor<T>(
            JsValue constructor,
            ObjectInstance intrinsicDefaultProto,
            Func<Engine, JsValue, T> objectCreator,
            JsValue state = null) where T : ObjectInstance
        {
            var proto = GetPrototypeFromConstructor(constructor, intrinsicDefaultProto);

            var obj = objectCreator(_engine, state);
            obj._prototype = proto;
            return obj;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ObjectInstance GetPrototypeFromConstructor(JsValue constructor, ObjectInstance intrinsicDefaultProto)
        {
            var proto = constructor.Get(CommonProperties.Prototype, constructor) as ObjectInstance;
            // If Type(proto) is not Object, then
            //    Let realm be ? GetFunctionRealm(constructor).
            //    Set proto to realm's intrinsic object named intrinsicDefaultProto.
            return proto ?? intrinsicDefaultProto;
        }
        
        internal void MakeMethod(ObjectInstance homeObject)
        {
            _homeObject = homeObject;
        }
        
        /// <summary>
        /// https://tc39.es/ecma262/#sec-ordinarycallbindthis
        /// </summary>
        protected void OrdinaryCallBindThis(ExecutionContext calleeContext, JsValue thisArgument)
        {
            var thisMode = _thisMode;
            if (thisMode == FunctionThisMode.Lexical)
            {
                return;
            }

            // Let calleeRealm be F.[[Realm]].

            var localEnv = (FunctionEnvironmentRecord) calleeContext.LexicalEnvironment._record;
            
            JsValue thisValue;
            if (_thisMode == FunctionThisMode.Strict)
            {
                thisValue = thisArgument;
            }
            else
            {
                if (thisArgument.IsNullOrUndefined())
                {
                    // Let globalEnv be calleeRealm.[[GlobalEnv]].
                    var globalEnv = _engine.GlobalEnvironment;
                    var globalEnvRec = (GlobalEnvironmentRecord) globalEnv._record;
                    thisValue = globalEnvRec.GlobalThisValue;
                }
                else
                {
                    thisValue = TypeConverter.ToObject(_engine, thisArgument);
                }
            }

            localEnv.BindThisValue(thisValue);
        }

        protected Completion OrdinaryCallEvaluateBody(
            JsValue[] arguments,
            ExecutionContext calleeContext)
        {
            var argumentsInstance = _engine.FunctionDeclarationInstantiation(
                functionInstance: this,
                arguments,
                calleeContext.LexicalEnvironment);

            var result = _functionDefinition.Execute();
            var value = result.GetValueOrDefault().Clone();

            argumentsInstance?.FunctionWasCalled();

            return new Completion(result.Type, value, result.Identifier, result.Location);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-prepareforordinarycall
        /// </summary>
        protected ExecutionContext PrepareForOrdinaryCall(JsValue newTarget)
        {
            // ** PrepareForOrdinaryCall **
            // var callerContext = _engine.ExecutionContext;
            // Let calleeRealm be F.[[Realm]].
            // Set the Realm of calleeContext to calleeRealm.
            // Set the ScriptOrModule of calleeContext to F.[[ScriptOrModule]].
            var calleeContext = LexicalEnvironment.NewFunctionEnvironment(_engine, this, newTarget);
            // If callerContext is not already suspended, suspend callerContext.
            // Push calleeContext onto the execution context stack; calleeContext is now the running execution context.
            // NOTE: Any exception objects produced after this point are associated with calleeRealm.
            // Return calleeContext.

            return _engine.EnterExecutionContext(calleeContext, calleeContext);
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
            return "function " + name + "() {{[native code]}}";
        }
    }
}
