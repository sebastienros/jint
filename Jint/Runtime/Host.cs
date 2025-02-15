using Jint.Native;
using Jint.Native.Global;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interop;
using Jint.Runtime.Modules;
using Module = Jint.Runtime.Modules.Module;

namespace Jint.Runtime;

public class Host
{
    private Engine? _engine;
    private readonly List<string> _supportedImportAttributes = ["type"];

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

    internal virtual GlobalEnvironment CreateGlobalEnvironment(ObjectInstance globalObject)
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
        if (!Engine.Options.Host.StringCompilationAllowed)
        {
            ExceptionHelper.ThrowJavaScriptException(callerRealm.Intrinsics.TypeError, "String compilation has been disabled in engine options");
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-GetImportedModule
    /// </summary>
    internal virtual Module GetImportedModule(IScriptOrModule? referrer, ModuleRequest request)
    {
        return Engine.Modules.Load(referrer?.Location, request);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-HostLoadImportedModule
    /// </summary>
    internal virtual void LoadImportedModule(IScriptOrModule? referrer, ModuleRequest moduleRequest, PromiseCapability payload)
    {
        var promise = Engine.RegisterPromise();

        try
        {
            // This should instead return the PromiseInstance returned by ModuleRecord.Evaluate (currently done in Engine.EvaluateModule), but until we have await this will do.
            Engine.Modules.Import(moduleRequest, referrer?.Location);
            promise.Resolve(JsValue.Undefined);
        }
        catch (JavaScriptException ex)
        {
            promise.Reject(ex.Error);
        }

        FinishLoadingImportedModule(referrer, moduleRequest, payload, (JsPromise) promise.Promise);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-FinishLoadingImportedModule
    /// </summary>
    internal virtual void FinishLoadingImportedModule(IScriptOrModule? referrer, ModuleRequest moduleRequest, PromiseCapability payload, JsPromise result)
    {
        var onFulfilled = new ClrFunction(Engine, "", (thisObj, args) =>
        {
            var moduleRecord = GetImportedModule(referrer, moduleRequest);
            try
            {
                var ns = Module.GetModuleNamespace(moduleRecord);
                payload.Resolve.Call(JsValue.Undefined, ns);
            }
            catch (JavaScriptException ex)
            {
                payload.Reject.Call(JsValue.Undefined, ex.Error);
            }
            return JsValue.Undefined;
        }, 0, PropertyFlag.Configurable);

        var onRejected = new ClrFunction(Engine, "", (thisObj, args) =>
        {
            var error = args.At(0);
            payload.Reject.Call(JsValue.Undefined, error);
            return JsValue.Undefined;
        }, 0, PropertyFlag.Configurable);

        PromiseOperations.PerformPromiseThen(Engine, result, onFulfilled, onRejected, payload);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-hostgetimportmetaproperties
    /// </summary>
    public virtual List<KeyValuePair<JsValue, JsValue>> GetImportMetaProperties(Module moduleRecord)
    {
        return new List<KeyValuePair<JsValue, JsValue>>();
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-hostfinalizeimportmeta
    /// </summary>
    public virtual void FinalizeImportMeta(ObjectInstance importMeta, Module moduleRecord)
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

    internal virtual List<string> GetSupportedImportAttributes()
    {
        return _supportedImportAttributes;
    }
}

internal sealed record JobCallback(ICallable Callback, object? HostDefined);
