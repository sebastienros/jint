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
        private Engine? _engine;

        protected Engine Engine
        {
            get
            {
                if (_engine is null)
                {
                    ExceptionHelper.ThrowInvalidOperationException("Initialize has not been called");
                }
                return _engine!;
            }
            private set => _engine = value;
        }

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

            Engine._realmInConstruction = null!;

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
        public virtual void EnsureCanCompileStrings(Realm callerRealm, Realm evalRealm)
        {
            if (!Engine.Options.StringCompilationAllowed)
            {
                ExceptionHelper.ThrowJavaScriptException(callerRealm.Intrinsics.TypeError, "String compilation has been disabled in engine options");
            }
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-hostresolveimportedmodule
        /// </summary>
        internal virtual ModuleRecord ResolveImportedModule(IScriptOrModule? referencingScriptOrModule, string specifier)
        {
            return Engine.LoadModule(referencingScriptOrModule?.Location, specifier);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-hostimportmoduledynamically
        /// </summary>
        internal virtual void ImportModuleDynamically(IScriptOrModule? referencingModule, string specifier, PromiseCapability promiseCapability)
        {
            var promise = Engine.RegisterPromise();

            try
            {
                // This should instead return the PromiseInstance returned by ModuleRecord.Evaluate (currently done in Engine.EvaluateModule), but until we have await this will do.
                Engine.ImportModule(specifier, referencingModule?.Location);
                promise.Resolve(JsValue.Undefined);
            }
            catch (JavaScriptException ex)
            {
                promise.Reject(ex.Error);
            }

            FinishDynamicImport(referencingModule, specifier, promiseCapability, (JsPromise) promise.Promise);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-finishdynamicimport
        /// </summary>
        internal virtual void FinishDynamicImport(IScriptOrModule? referencingModule, string specifier, PromiseCapability promiseCapability, JsPromise innerPromise)
        {
            var onFulfilled = new ClrFunctionInstance(Engine, "", (thisObj, args) =>
            {
                var moduleRecord = ResolveImportedModule(referencingModule, specifier);
                try
                {
                    var ns = ModuleRecord.GetModuleNamespace(moduleRecord);
                    promiseCapability.Resolve.Call(JsValue.Undefined, new JsValue[] { ns });
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

            PromiseOperations.PerformPromiseThen(Engine, innerPromise, onFulfilled, onRejected, promiseCapability);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-hostgetimportmetaproperties
        /// </summary>
        public virtual List<KeyValuePair<JsValue, JsValue>> GetImportMetaProperties(ModuleRecord moduleRecord)
        {
            return new List<KeyValuePair<JsValue, JsValue>>();
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-hostfinalizeimportmeta
        /// </summary>
        public virtual void FinalizeImportMeta(ObjectInstance importMeta, ModuleRecord moduleRecord)
        {
        }

        /// <summary>
        /// https://tc39.es/proposal-shadowrealm/#sec-host-initialize-shadow-shadowrealm
        /// </summary>
        public virtual void InitializeShadowRealm(Realm realm)
        {
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-hostmakejobcallback
        /// </summary>
        internal virtual JobCallback MakeJobCallBack(ICallable cleanupCallback)
        {
            return new JobCallback(cleanupCallback, null);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-hostenqueuepromisejob
        /// </summary>
        internal void HostEnqueuePromiseJob(Action job, Realm realm)
        {
            Engine.AddToEventLoop(job);
        }
    }
}

internal sealed record JobCallback(ICallable Callback, object? HostDefined);
