using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;

namespace Jint.Native.Function
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-function-constructor
    /// </summary>
    public sealed class FunctionConstructor : FunctionInstance, IConstructor
    {
        private static readonly JsString _functionName = new JsString("Function");

        internal FunctionConstructor(
            Engine engine,
            Realm realm,
            ObjectPrototype objectPrototype)
            : base(engine, realm, _functionName)
        {
            PrototypeObject = new FunctionPrototype(engine, realm, objectPrototype);
            _prototype = PrototypeObject;
            _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
            _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
        }

        public FunctionPrototype PrototypeObject { get; }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return Construct(arguments, thisObject);
        }

        ObjectInstance IConstructor.Construct(JsValue[] arguments, JsValue newTarget) => Construct(arguments, newTarget);

        private ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            var function = CreateDynamicFunction(
                this,
                newTarget,
                FunctionKind.Normal,
                arguments);

            return function;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-runtime-semantics-instantiatefunctionobject
        /// </summary>
        internal FunctionInstance InstantiateFunctionObject(
            JintFunctionDefinition functionDeclaration,
            EnvironmentRecord scope,
            PrivateEnvironmentRecord? privateScope)
        {
            var function = functionDeclaration.Function;
            if (!function.Generator)
            {
                return function.Async
                    ? InstantiateAsyncFunctionObject(functionDeclaration, scope, privateScope)
                    : InstantiateOrdinaryFunctionObject(functionDeclaration, scope, privateScope);
            }
            else
            {
                return InstantiateGeneratorFunctionObject(functionDeclaration, scope, privateScope);
            }
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-runtime-semantics-instantiateasyncfunctionobject
        /// </summary>
        private FunctionInstance InstantiateAsyncFunctionObject(
            JintFunctionDefinition functionDeclaration,
            EnvironmentRecord env,
            PrivateEnvironmentRecord? privateEnv)
        {
            var F = OrdinaryFunctionCreate(
                _realm.Intrinsics.AsyncFunction.PrototypeObject,
                functionDeclaration,
                functionDeclaration.ThisMode,
                env,
                privateEnv);

            F.SetFunctionName(functionDeclaration.Name ?? "default");

            return F;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-runtime-semantics-instantiateordinaryfunctionobject
        /// </summary>
        private FunctionInstance InstantiateOrdinaryFunctionObject(
            JintFunctionDefinition functionDeclaration,
            EnvironmentRecord env,
            PrivateEnvironmentRecord? privateEnv)
        {
            var F = OrdinaryFunctionCreate(
                _realm.Intrinsics.Function.PrototypeObject,
                functionDeclaration,
                functionDeclaration.ThisMode,
                env,
                privateEnv);

            var name = functionDeclaration.Name ?? "default";
            F.SetFunctionName(name);
            F.MakeConstructor();
            return F;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-runtime-semantics-instantiategeneratorfunctionobject
        /// </summary>
        private FunctionInstance InstantiateGeneratorFunctionObject(
            JintFunctionDefinition functionDeclaration,
            EnvironmentRecord scope,
            PrivateEnvironmentRecord? privateScope)
        {
            // TODO generators
            return InstantiateOrdinaryFunctionObject(functionDeclaration, scope, privateScope);
        }
    }
}
