#nullable enable

using Jint.Native;
using Jint.Native.Global;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interop;
using Jint.Runtime.Modules;

namespace Jint.Runtime
{
    public class Host
    {
        protected Engine Engine { get; private set; } = null!;

        /// <summary>
        /// Initializes the host.
        /// </summary>
        public void Initialize(Engine engine)
        {
            Engine = engine;
            InitializeHostDefinedRealm();
            PostInitialize();
        }

        protected virtual void PostInitialize()
        {
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-initializehostdefinedrealm
        /// </summary>
        protected virtual void InitializeHostDefinedRealm()
        {
            var realm = CreateRealm();

            var newContext = new ExecutionContext(
                scriptOrModule: null,
                lexicalEnvironment: realm.GlobalEnv,
                variableEnvironment: realm.GlobalEnv,
                privateEnvironment: null,
                realm: realm,
                function: null);

            Engine.EnterExecutionContext(newContext);
        }

        protected virtual GlobalEnvironmentRecord CreateGlobalEnvironment(ObjectInstance globalObject)
        {
            return JintEnvironment.NewGlobalEnvironment(Engine, globalObject, globalObject);
        }

        protected virtual ObjectInstance CreateGlobalObject(Realm realm)
        {
            var globalObject = new GlobalObject(Engine, realm);
            // Because the properties might need some of the built-in object
            // their configuration is delayed to a later step
            // trigger initialization
            globalObject.EnsureInitialized();
            return globalObject;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-createrealm
        /// </summary>
        protected internal virtual Realm CreateRealm()
        {
            var realmRec = new Realm();
            Engine._realmInConstruction = realmRec;

            CreateIntrinsics(realmRec);

            var globalObject = CreateGlobalObject(realmRec);

            var globalEnv = CreateGlobalEnvironment(globalObject);
            realmRec.GlobalEnv = globalEnv;
            realmRec.GlobalObject = globalObject;

            Engine._realmInConstruction = null;

            return realmRec;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-createintrinsics
        /// </summary>
        protected virtual void CreateIntrinsics(Realm realmRec)
        {
            var intrinsics = new Intrinsics(Engine, realmRec);
            realmRec.Intrinsics = intrinsics;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-hostensurecancompilestrings
        /// </summary>
        /// <param name="callerRealm"></param>
        /// <param name="evalRealm"></param>
        public virtual void EnsureCanCompileStrings(Realm callerRealm, Realm evalRealm)
        {
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-hostresolveimportedmodule
        /// </summary>
        internal virtual JsModule ResolveImportedModule(IScriptOrModule? referencingScriptOrModule, string specifier)
        {
            return Engine.LoadModule(referencingScriptOrModule?.Location, specifier);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-hostimportmoduledynamically
        /// </summary>
        internal virtual void ImportModuleDynamically(IScriptOrModule? referencingScriptOrModule, string specifier, PromiseCapability promiseCapability)
        {
            var promise = Engine.RegisterPromise();

            try
            {
                Engine.LoadModule(referencingScriptOrModule?.Location, specifier);
                promise.Resolve(JsValue.Undefined);

            }
            catch (JavaScriptException ex)
            {
                promise.Reject(ex.Error);
            }

            FinishDynamicImport(referencingScriptOrModule, specifier, promiseCapability, (PromiseInstance)promise.Promise);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-finishdynamicimport
        /// </summary>
        internal virtual void FinishDynamicImport(IScriptOrModule? referencingScriptOrModule, string specifier, PromiseCapability promiseCapability, PromiseInstance innerPromise)
        {
            var onFulfilled = new ClrFunctionInstance(Engine, "", (thisObj, args) =>
            {
                var moduleRecord = ResolveImportedModule(referencingScriptOrModule, specifier);
                try
                {
                    var ns = JsModule.GetModuleNamespace(moduleRecord);
                    promiseCapability.Resolve.Call(JsValue.Undefined, new[] { ns });
                }
                catch (JavaScriptException ex)
                {
                    promiseCapability.Reject.Call(JsValue.Undefined, new [] { ex.Error });
                }
                return JsValue.Undefined;
            }, 0, PropertyFlag.Configurable);

            var onRejected = new ClrFunctionInstance(Engine, "", (thisObj, args) =>
            {
                var error = args.At(0);
                promiseCapability.Reject.Call(JsValue.Undefined, new [] { error });
                return JsValue.Undefined;
            }, 0, PropertyFlag.Configurable);

            PromiseOperations.PerformPromiseThen(Engine, innerPromise, onFulfilled, onRejected);
        }
    }
}
