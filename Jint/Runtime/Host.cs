using Jint.Native.Global;
using Jint.Native.Object;
using Jint.Runtime.Environments;

namespace Jint.Runtime
{
    public class Host
    {
        protected Engine Engine { get; private set; }

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
    }
}