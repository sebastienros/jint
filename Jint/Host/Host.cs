using Jint.Native.Global;
using Jint.Native.Object;
using Jint.Runtime.Environments;

namespace Jint
{
    public abstract class Host
    {
        protected Host(Engine engine)
        {
            Engine = engine;
        }

        protected Engine Engine { get; }

        public void Initialize()
        {
            var realm = CreateRealm();

            // create the global execution context http://www.ecma-international.org/ecma-262/5.1/#sec-10.4.1.1
            var context = new ExecutionContext(
                lexicalEnvironment: realm.GlobalEnv,
                variableEnvironment: realm.GlobalEnv,
                privateEnvironment: null,
                realm: realm,
                function: null);

            Engine.EnterExecutionContext(context);
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
        public Realm CreateRealm()
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
    }
}